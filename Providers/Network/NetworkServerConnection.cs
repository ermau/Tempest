//
// NetworkServerConnection.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011 Eric Maupin
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public sealed class NetworkServerConnection
		: NetworkConnection, IServerConnection
	{
		internal NetworkServerConnection (IEnumerable<Protocol> protocols, Socket reliableSocket, NetworkConnectionProvider provider)
			: base (protocols)
		{
			if (reliableSocket == null)
				throw new ArgumentNullException ("reliableSocket");
			if (provider == null)
				throw new ArgumentNullException ("provider");

			this.provider = provider;

			RemoteEndPoint = reliableSocket.RemoteEndPoint;

			this.reliableSocket = reliableSocket;

			var asyncArgs = new SocketAsyncEventArgs();
			asyncArgs.UserToken = this;
			asyncArgs.SetBuffer (this.rmessageBuffer, 0, 20480);
			asyncArgs.Completed += ReliableReceiveCompleted;

			this.reliableSocket.ReceiveAsync (asyncArgs);
			this.rreader = new BufferValueReader (this.rmessageBuffer);

			provider.PingFrequencyChanged += ProviderOnPingFrequencyChanged;

			this.pingTimer = new Timer (PingCallback, null, provider.PingFrequency, provider.PingFrequency);
		}

		private static int nextNetworkId;

		private readonly NetworkConnectionProvider provider;

		private bool formallyConnected;
		private readonly object pingSync = new object();
		private Timer pingTimer;

		private void PingCallback (object state)
		{
			if (this.pingsOut >= 2)
			{
				Disconnect (true); // Connection timed out
				return;
			}

			Send (new PingMessage { Interval = provider.PingFrequency });
		}

		protected override void OnMessageReceived (MessageEventArgs e)
		{
			if (!this.formallyConnected)
			{
				Disconnect (true);
				return;
			}

			base.OnMessageReceived(e);
		}

		protected override void OnTempestMessageReceived (MessageEventArgs e)
		{
			switch (e.Message.MessageType)
			{
				case (ushort)TempestMessageType.Connect:
					var msg = (ConnectMessage)e.Message;

					NetworkId = Interlocked.Increment (ref nextNetworkId);

					IEnumerable<Protocol> ps = msg.Protocols.Intersect (this.protocols.Values);

					this.formallyConnected = true;
					this.provider.Connect (this);

					e.Connection.Send (new ConnectedMessage
					{
						EnabledProtocols = ps,
						NetworkId = NetworkId
					});					
					break;
			}

			base.OnTempestMessageReceived(e);
		}

		protected override void OnMessageSent (MessageEventArgs e)
		{
			var pingMsg = (e.Message as PingMessage);
			if (pingMsg != null)
				Interlocked.Increment (ref this.pingsOut);

			base.OnMessageSent(e);
		}

		private void ProviderOnPingFrequencyChanged (object sender, EventArgs e)
		{
			lock (this.pingSync)
			{
				if (this.pingTimer != null)
					this.pingTimer.Change (0, this.provider.PingFrequency);
			}
		}

		protected override void Recycle()
		{
			lock (this.pingSync)
			{
				this.pingTimer.Dispose();
				this.pingTimer = null;
			}

			this.provider.PingFrequencyChanged -= ProviderOnPingFrequencyChanged;
			this.provider.Disconnect (this);

			if (this.reliableSocket == null)
				return;

			#if !NET_4
			lock (NetworkConnectionProvider.ReliableSockets)
			#endif
				NetworkConnectionProvider.ReliableSockets.Push (this.reliableSocket);

			this.reliableSocket = null;
		}
		
		internal int NetworkId
		{
			get; private set;
		}
	}
}
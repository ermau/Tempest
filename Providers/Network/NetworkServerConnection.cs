//
// NetworkServerConnection.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010 Eric Maupin
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
using System.Linq;
using System.Net.Sockets;

namespace Tempest.Providers.Network
{
	public class NetworkServerConnection
		: NetworkConnection, IServerConnection
	{
		

		internal NetworkServerConnection (Socket reliableSocket, NetworkConnectionProvider provider)
		{
			this.provider = provider;
			if (reliableSocket == null)
				throw new ArgumentNullException ("reliableSocket");
			if (provider == null)
				throw new ArgumentNullException ("provider");

			this.reliableSocket = reliableSocket;

			var asyncArgs = new SocketAsyncEventArgs();
			asyncArgs.UserToken = this;
			asyncArgs.SetBuffer (this.rmessageBuffer, 0, 20480);
			asyncArgs.Completed += ReliableReceiveCompleted;

			this.reliableSocket.ReceiveAsync (asyncArgs);
			this.rreader = new BufferValueReader (this.rmessageBuffer);
		}

		public override void Disconnect (bool now)
		{
			if (this.reliableSocket != null && this.reliableSocket.Connected)
			{
				var args = new SocketAsyncEventArgs { DisconnectReuseSocket = true };
				args.Completed += OnDisconnectCompleted;
				if (!this.reliableSocket.DisconnectAsync (args))
					OnDisconnectCompleted (this.reliableSocket, args);
			}
			else
			{
				Recycle();
				OnDisconnected (new ConnectionEventArgs (this));
			}
		}

		private readonly NetworkConnectionProvider provider;

		private void Recycle()
		{
			this.provider.Disconnect (this);

			if (this.reliableSocket == null)
				return;

			#if !NET_4
			lock (NetworkConnectionProvider.ReliableSockets)
			#endif
				NetworkConnectionProvider.ReliableSockets.Push (this.reliableSocket);

			this.reliableSocket = null;
		}

		private void OnDisconnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			Recycle();
			OnDisconnected (new ConnectionEventArgs (this));
		}

		internal int NetworkId
		{
			get; private set;
		}
	}
}
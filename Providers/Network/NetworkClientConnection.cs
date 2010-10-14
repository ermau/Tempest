//
// NetworkClientConnection.cs
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
using System.Net;
using System.Net.Sockets;

namespace Tempest.Providers.Network
{
	public class NetworkClientConnection
		: NetworkConnection, IClientConnection
	{
		public NetworkClientConnection (byte appId)
			: base (appId)
		{
		}

		public event EventHandler<ClientConnectionEventArgs> Connected;
		public event EventHandler<ClientConnectionEventArgs> ConnectionFailed;

		public override bool IsConnected
		{
			get { return (this.reliableSocket != null && this.reliableSocket.Connected); }
		}

		public void Connect (EndPoint endpoint, MessageTypes messageTypes)
		{
			if (endpoint == null)
				throw new ArgumentNullException ("endpoint");
			if (messageTypes.HasFlag (MessageTypes.Unreliable))
				throw new NotSupportedException();

			SocketAsyncEventArgs args = new SocketAsyncEventArgs();
			args.RemoteEndPoint = endpoint;
			args.Completed += ConnectCompleted;

			this.reliableSocket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			if (!this.reliableSocket.ConnectAsync (args))
				ConnectCompleted (this.reliableSocket, args);
		}

		public override void Send (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			throw new NotImplementedException ();
		}

		private void ConnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			if (e.SocketError != SocketError.Success)
			{
				OnConnectionFailed (new ClientConnectionEventArgs (this));
				Disconnect();
				return;
			}

			OnConnected (new ClientConnectionEventArgs (this));

			e.Completed -= ConnectCompleted;
			e.Completed += ReliableIOCompleted;
			e.SetBuffer (this.rmessageBuffer, 0, this.rmessageBuffer.Length);

			if (!this.reliableSocket.ReceiveAsync (e))
				ReliableIOCompleted (this.reliableSocket, e);
		}

		private void OnConnected (ClientConnectionEventArgs e)
		{
			var connected = Connected;
			if (connected != null)
				connected (this, e);
		}

		private void OnConnectionFailed (ClientConnectionEventArgs e)
		{
			var handler = this.ConnectionFailed;
			if (handler != null)
				handler (this, e);
		}
	}
}
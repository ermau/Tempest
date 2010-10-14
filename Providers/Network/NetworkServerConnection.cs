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
		internal NetworkServerConnection (NetworkConnectionProvider provider, Socket reliableSocket, byte sanityByte)
			: base (sanityByte)
		{
			if (provider == null)
				throw new ArgumentNullException ("provider");
			if (reliableSocket == null)
				throw new ArgumentNullException ("reliableSocket");

			this.provider = provider;
			this.reliableSocket = reliableSocket;
			this.sanityByte = sanityByte;

			var asyncArgs = new SocketAsyncEventArgs();
			asyncArgs.UserToken = this;
			asyncArgs.SetBuffer (this.rmessageBuffer, 0, 20480);
			asyncArgs.Completed += ReliableIOCompleted;

			this.reliableSocket.ReceiveAsync (asyncArgs);
			this.rreader = new BufferValueReader (this.rmessageBuffer);
		}

		public override bool IsConnected
		{
			get { return this.reliableSocket.Connected; }
		}

		public override void Disconnect ()
		{
			if (this.reliableSocket != null)
			{
				this.reliableSocket.Disconnect (true);
				NetworkConnectionProvider.ReliableSockets.Push (this.reliableSocket);
			}

			OnDisconnected (new ConnectionEventArgs (this));
		}

		private readonly NetworkConnectionProvider provider;

		internal int NetworkId
		{
			get; private set;
		}
	}
}
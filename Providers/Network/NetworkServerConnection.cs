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
		internal NetworkServerConnection (Socket reliableSocket, byte sanityByte)
		{
			if (reliableSocket == null)
				throw new ArgumentNullException ("reliableSocket");

			this.reliableSocket = reliableSocket;
			this.sanityByte = sanityByte;

			var asyncArgs = new SocketAsyncEventArgs();
			asyncArgs.UserToken = this;
			asyncArgs.SetBuffer (messageBuffer, 0, 20480);
			asyncArgs.Completed += ReliableIOCompleted;

			this.reliableSocket.ReceiveAsync (asyncArgs);
		}

		public override bool IsConnected
		{
			get { return this.reliableSocket.Connected; }
		}

		public override void Send (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			throw new NotImplementedException ();
		}

		public override void Disconnect ()
		{
			if (this.reliableSocket != null)
				this.reliableSocket.Disconnect (true);
		}

		private readonly Socket reliableSocket;
		private readonly byte sanityByte;
		private bool midMessage;
		private readonly byte[] messageBuffer = new byte[20480];

		internal int NetworkId
		{
			get; private set;
		}

		private void ReliableIOCompleted (object sender, SocketAsyncEventArgs e)
		{
			if (this.midMessage)
			{
				
			}
			else if (this.sanityByte == this.messageBuffer[0])
			{
				
			}
			else
			{
				// TODO: Log sanity failure
			}

			if (!this.reliableSocket.ReceiveAsync (e))
				ReliableIOCompleted (sender, e);
		}
	}
}
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
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;

namespace Tempest.Providers.Network
{
	public class NetworkServerConnection
		: NetworkConnection, IServerConnection
	{
		internal NetworkServerConnection (NetworkConnectionProvider provider, Socket reliableSocket, byte sanityByte)
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

		public override void Send (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (!IsConnected)
				return;

			BufferValueWriter writer;
			if (!writers.TryPop (out writer))
				writer = new BufferValueWriter (new byte[20480]);

			message.Serialize (writer);

			SocketAsyncEventArgs e;
			if (!writerAsyncArgs.TryPop (out e))
			{
				e = new SocketAsyncEventArgs ();
				e.Completed += ReliableSendCompleted;
			}
			else
				e.AcceptSocket = null;

			e.SetBuffer (writer.Buffer, 0, writer.Length);

			lock (sync)
			{
				if (IsConnected)
				{
					if (!this.reliableSocket.SendAsync (e))
						ReliableSendCompleted (this.reliableSocket, e);
				}
			}

			writerAsyncArgs.Push (e);
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

		private readonly object sync = new object();
		private readonly byte sanityByte;

		private readonly NetworkConnectionProvider provider;
		private readonly Socket reliableSocket;
		private byte[] rmessageBuffer = new byte[20480];
		private BufferValueReader rreader;
		private int rmessageOffset = 0;
		private int rmessageLoaded = 0;
		private int currentRMessageLength = 0;

		private const int BaseHeaderLength = 7;

		internal int NetworkId
		{
			get; private set;
		}

		private void ReliableSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				Disconnect();
				return;
			}

			writerAsyncArgs.Push (e);
		}

		private void ReliableIOCompleted (object sender, SocketAsyncEventArgs e)
		{
			int bytesTransferred = e.BytesTransferred;

			if (bytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				Disconnect();
				return;
			}

			byte[] buffer = this.rmessageBuffer;
			this.rmessageLoaded += bytesTransferred;

			int messageLength = 0;
			
			if (this.currentRMessageLength == 0)
			{
				if (this.rmessageLoaded >= BaseHeaderLength)
					this.currentRMessageLength = messageLength = BitConverter.ToInt32 (buffer, this.rmessageOffset + 3);
			}
			else
				messageLength = this.currentRMessageLength;

			int messageAndHeaderLength = messageLength + BaseHeaderLength;

			if (messageLength >= this.provider.MaxMessageLength)
			{
				Disconnect();
				return;
			}

			bool loaded = (messageLength != 0 && this.rmessageLoaded >= messageAndHeaderLength);

			if (buffer[this.rmessageOffset] != this.sanityByte)
			{
				Disconnect();
				return;
			}

			if (loaded)
			{
				DeliverMessage (buffer, this.rmessageOffset, messageLength);
				
				int remaining = this.rmessageLoaded - messageAndHeaderLength;

				if (remaining == 0)
				{
					e.SetBuffer (0, buffer.Length);
					this.rmessageOffset = 0;
					this.rmessageLoaded = 0;
				}
				else
				{
					int offset = 0;
					while (remaining > 0)
					{
						offset = e.Offset + bytesTransferred - remaining;

						if (remaining <= BaseHeaderLength)
							break;

						if (buffer[offset] != this.sanityByte)
						{
							Disconnect();
							return;
						}

						messageLength = BitConverter.ToInt16 (buffer, offset + 1);
						messageAndHeaderLength = BaseHeaderLength + messageLength;

						if (remaining > messageAndHeaderLength)
						{
							DeliverMessage (buffer, offset, messageLength);
							offset += messageAndHeaderLength;
							remaining -= messageAndHeaderLength;
						}
					}

					if (messageAndHeaderLength >= buffer.Length - offset)
					{
						Array.Copy (buffer, offset, buffer, 0, remaining);
						this.rmessageOffset = 0;
					}
					else
						this.rmessageOffset = offset;

					this.rmessageLoaded = remaining;
				}
			}
			else if (messageAndHeaderLength > buffer.Length - this.rmessageOffset)
			{
				byte[] newBuffer = new byte[messageAndHeaderLength];
				Array.Copy (buffer, this.rmessageOffset, newBuffer, 0, this.rmessageLoaded);
				this.rreader = new BufferValueReader (newBuffer, BaseHeaderLength, newBuffer.Length);
				this.rmessageBuffer = newBuffer;
				this.rmessageOffset = 0;
			}

			if (!this.reliableSocket.ReceiveAsync (e))
				ReliableIOCompleted (sender, e);
		}

		private void DeliverMessage (byte[] buffer, int offset, int length)
		{
			byte[] payload = new byte[length];
			Array.Copy (buffer, offset + BaseHeaderLength, payload, 0, length);

			ushort mtype = BitConverter.ToUInt16 (buffer, offset + 1);

			Message m = Message.Factory.Create (mtype);
			m.Deserialize (this.rreader);

			OnMessageReceived (new MessageReceivedEventArgs (this, m));
		}

		private static readonly ConcurrentStack<BufferValueWriter> writers = new ConcurrentStack<BufferValueWriter>();
		private static readonly ConcurrentStack<SocketAsyncEventArgs> writerAsyncArgs = new ConcurrentStack<SocketAsyncEventArgs>();
	}
}
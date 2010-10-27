//
// NetworkConnection.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

#if NET_4
using System.Collections.Concurrent;
#endif

namespace Tempest.Providers.Network
{
	public abstract class NetworkConnection
		: IConnection
	{
		protected NetworkConnection (byte appId)
		{
			this.sanityByte = appId;
		}

		/// <summary>
		/// Raised when a message is received.
		/// </summary>
		public event EventHandler<MessageReceivedEventArgs> MessageReceived;

		/// <summary>
		/// Raised when the connection is lost or manually disconnected.
		/// </summary>
		public event EventHandler<ConnectionEventArgs> Disconnected;

		public bool IsConnected
		{
			get { return (this.reliableSocket != null && this.reliableSocket.Connected); }
		}

		public MessagingModes Modes
		{
			get { return MessagingModes.Async; }
		}

		public EndPoint RemoteEndPoint
		{
			get;
			protected set;
		}

		public IEnumerable<MessageReceivedEventArgs> Tick()
		{
			throw new NotSupportedException();
		}

		public virtual void Send (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (!IsConnected)
				return;

			BufferValueWriter writer = null;
			#if NET_4
			if (!writers.TryPop (out writer))
				writer = new BufferValueWriter (new byte[20480]);
			#else
			if (writers.Count != 0)
			{
				lock (writers)
				{
					if (writers.Count != 0)
						writer = writers.Pop();
				}
			}

			if (writer == null)
				writer = new BufferValueWriter (new byte[20480]);
			#endif
			

			writer.WriteByte (sanityByte);
			writer.WriteUInt16 (message.MessageType);
			writer.WriteInt32 (0); // Length placeholder

			message.Serialize (writer);
			// Copy length in
			Array.Copy (BitConverter.GetBytes (writer.Length - BaseHeaderLength), 0, writer.Buffer, BaseHeaderLength - sizeof(int), sizeof(int));

			SocketAsyncEventArgs e = null;
			#if NET_4
			if (!writerAsyncArgs.TryPop (out e))
			{
				e = new SocketAsyncEventArgs ();
				e.Completed += ReliableSendCompleted;
			}
			else
				e.AcceptSocket = null;
			#else
			if (writerAsyncArgs.Count != 0)
			{
				lock (writerAsyncArgs)
				{
					if (writerAsyncArgs.Count != 0)
					{
						e = writerAsyncArgs.Pop();
						e.AcceptSocket = null;
					}
				}
			}

			if (e == null)
			{
				e = new SocketAsyncEventArgs();
				e.Completed += ReliableSendCompleted;
			}
			#endif

			e.SetBuffer (writer.Buffer, 0, writer.Length);
			e.UserToken = writer;
			writer.Flush();

			if (!IsConnected)
			{
				#if !NET_4
				lock (writerAsyncArgs)
				#endif
				writerAsyncArgs.Push (e);

				#if !NET_4
				lock (writers)
				#endif
				writers.Push (writer);
				return;
			}

			if (!this.reliableSocket.SendAsync (e))
				ReliableSendCompleted (this.reliableSocket, e);
		}

		public virtual void Disconnect (bool now)
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
				OnDisconnected (new ConnectionEventArgs (this));
				this.reliableSocket = null;
			}
		}

		private void OnDisconnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			OnDisconnected (new ConnectionEventArgs (this));
			this.reliableSocket = null;
		}

		public void Dispose()
		{
			Dispose (true);
		}

		protected bool disposed;

		private const int BaseHeaderLength = 7;
		private int maxMessageLength = 104857600;

		protected byte sanityByte;
		protected Socket reliableSocket;
		protected byte[] rmessageBuffer = new byte[20480];
		protected BufferValueReader rreader;
		private int rmessageOffset = 0;
		private int rmessageLoaded = 0;
		private int currentRMessageLength = 0;
		private bool disconnecting;

		protected virtual void Dispose (bool disposing)
		{
			if (this.disposed)
				return;

			Disconnect (true);

			this.disposed = true;
		}

		protected void OnMessageReceived (MessageReceivedEventArgs e)
		{
			var mr = this.MessageReceived;
			if (mr != null)
				mr (this, e);
		}

		protected void OnDisconnected (ConnectionEventArgs e)
		{
			var dc = this.Disconnected;
			if (dc != null)
				dc (this, e);
		}

		protected void ReliableReceiveCompleted (object sender, SocketAsyncEventArgs e)
		{
			int bytesTransferred = e.BytesTransferred;

			if (bytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				Disconnect (true);
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

			if (messageLength >= this.maxMessageLength)
			{
				Disconnect (true);
				return;
			}

			bool loaded = (messageLength != 0 && this.rmessageLoaded >= messageAndHeaderLength);

			if (buffer[this.rmessageOffset] != this.sanityByte)
			{
				Disconnect (true);
				return;
			}

			if (loaded)
			{
				DeliverMessage(buffer, this.rmessageOffset, messageLength);

				int remaining = this.rmessageLoaded - messageAndHeaderLength;

				if (remaining == 0)
				{
					e.SetBuffer(0, buffer.Length);
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
							Disconnect (true);
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
			else
			{
				if (messageAndHeaderLength > buffer.Length - this.rmessageOffset)
				{
					byte[] newBuffer = new byte[messageAndHeaderLength];
					Array.Copy (buffer, this.rmessageOffset, newBuffer, 0, this.rmessageLoaded);
					this.rreader = new BufferValueReader (newBuffer, BaseHeaderLength, newBuffer.Length);
					this.rmessageBuffer = newBuffer;

					                                newBuffer.Length - this.rmessageLoaded));
					
					this.rmessageOffset = 0;
				}

				e.SetBuffer (this.rmessageBuffer, this.rmessageLoaded, this.rmessageBuffer.Length - this.rmessageLoaded);
			}

			if (!IsConnected)
				return;

			if (!this.reliableSocket.ReceiveAsync (e))
				ReliableReceiveCompleted (sender, e);
		}

		private void DeliverMessage (byte[] buffer, int offset, int length)
		{
			ushort mtype = BitConverter.ToUInt16 (buffer, offset + 1);

			this.rreader.Position = offset + BaseHeaderLength;

			Message m = Message.Factory.Create (mtype);
			m.Deserialize (this.rreader);

			OnMessageReceived (new MessageReceivedEventArgs (this, m));
		}

		private void ReliableSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				Disconnect (true);
				return;
			}
			
			#if !NET_4
			lock (writers)
			#endif
			writers.Push ((BufferValueWriter)e.UserToken);

			#if !NET_4
			lock (writerAsyncArgs)
			#endif
			writerAsyncArgs.Push (e);
		}

		#if NET_4
		private static readonly ConcurrentStack<BufferValueWriter> writers = new ConcurrentStack<BufferValueWriter>();
		private static readonly ConcurrentStack<SocketAsyncEventArgs> writerAsyncArgs = new ConcurrentStack<SocketAsyncEventArgs>();
		#else
		private static readonly Stack<BufferValueWriter> writers = new Stack<BufferValueWriter>();
		private static readonly Stack<SocketAsyncEventArgs> writerAsyncArgs = new Stack<SocketAsyncEventArgs>();
		#endif
	}
}
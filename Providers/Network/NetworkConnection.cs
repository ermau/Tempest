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
		/// <summary>
		/// Raised when a message is received.
		/// </summary>
		public event EventHandler<MessageEventArgs> MessageReceived;

		/// <summary>
		/// Raised when a message has completed sending.
		/// </summary>
		public event EventHandler<MessageEventArgs> MessageSent;

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

		public IEnumerable<MessageEventArgs> Tick()
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
			

			writer.WriteByte (message.Protocol.Id);
			writer.WriteUInt16 (message.MessageType);
			writer.WriteInt32 (0); // Length placeholder

			message.WritePayload (writer);
			// Copy length in
			Buffer.BlockCopy (BitConverter.GetBytes (writer.Length - BaseHeaderLength), 0, writer.Buffer, BaseHeaderLength - sizeof(int), sizeof(int));

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
			e.UserToken = new SendHolder { Writer = writer, Message = message };
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
			if (this.disconnecting)
				return;

			this.disconnecting = true;

			if (this.reliableSocket == null || !this.reliableSocket.Connected)
				return;

			if (now)
			{
				this.reliableSocket.Shutdown (SocketShutdown.Both);
				this.reliableSocket.Disconnect (true);
				this.reliableSocket = null;
				OnDisconnected (new ConnectionEventArgs(this));
				this.disconnecting = false;
			}
			else
			{
				var args = new SocketAsyncEventArgs { DisconnectReuseSocket = true };
				args.Completed += OnDisconnectCompleted;
				if (!this.reliableSocket.DisconnectAsync (args))
					OnDisconnectCompleted (this.reliableSocket, args);
			}
		}

		private void OnDisconnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			this.disconnecting = false;
			this.reliableSocket = null;
			OnDisconnected (new ConnectionEventArgs (this));
		}

		public void Dispose()
		{
			Dispose (true);
		}

		private class SendHolder
		{
			public BufferValueWriter Writer;
			public Message Message;
		}

		protected bool disposed;

		private const int BaseHeaderLength = 7;
		private int maxMessageLength = 1048576;

		protected volatile bool disconnecting;
		protected Socket reliableSocket;

		protected byte[] rmessageBuffer = new byte[20480];
		protected BufferValueReader rreader;
		private int rmessageOffset = 0;
		private int rmessageLoaded = 0;

		protected virtual void Dispose (bool disposing)
		{
			if (this.disposed)
				return;

			Disconnect (true);

			this.disposed = true;
		}

		protected void OnMessageReceived (MessageEventArgs e)
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

		protected void OnMessageSent (MessageEventArgs e)
		{
			var sent = this.MessageSent;
			if (sent != null)
				sent (this, e);
		}

		private void BufferMessages (ref byte[] buffer, ref int bufferOffset, ref int messageOffset, ref int remainingData, ref BufferValueReader reader)
		{
			int length = 0;
			while (remainingData > BaseHeaderLength)
			{
				Protocol p = Protocol.Get (buffer[messageOffset]);
				if (p == null)
				{
					Disconnect (true);
					return;
				}

				length = BitConverter.ToInt32 (buffer, messageOffset + 3) + BaseHeaderLength;
				if (length > maxMessageLength)
				{
					Disconnect (true);
					return;
				}

				if (remainingData < length)
				{
					bufferOffset += remainingData;
					break;
				}

				DeliverMessage (p, buffer, messageOffset);
				messageOffset += length;
				bufferOffset = messageOffset;
				remainingData -= length;
			}

			if (remainingData > 0)
			{
				byte[] newBuffer = new byte[buffer.Length];
				reader = new BufferValueReader(newBuffer, 0, newBuffer.Length);
				Buffer.BlockCopy(buffer, messageOffset, newBuffer, 0, remainingData);
				buffer = newBuffer;
				bufferOffset = remainingData;
				messageOffset = 0;
			}

			return;

			if ((buffer.Length - messageOffset) < length)
			{
				byte[] newBuffer = buffer;
				if (buffer.Length < length)
				{
					newBuffer = new byte[length];
					reader = new BufferValueReader (newBuffer, 0, newBuffer.Length);
				}

				Buffer.BlockCopy (buffer, messageOffset, newBuffer, 0, remainingData);
				buffer = newBuffer;

				bufferOffset = remainingData;
				messageOffset = 0;
			}
		}

		protected void ReliableReceiveCompleted (object sender, SocketAsyncEventArgs e)
		{
			if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				Disconnect (true);
				return;
			}

			this.rmessageLoaded += e.BytesTransferred;

			int bufferOffset = e.Offset;
			BufferMessages (ref this.rmessageBuffer, ref bufferOffset, ref this.rmessageOffset, ref this.rmessageLoaded, ref this.rreader);
			e.SetBuffer (this.rmessageBuffer, bufferOffset, this.rmessageBuffer.Length - bufferOffset);

			if (!IsConnected)
				return;
			if (!this.reliableSocket.ReceiveAsync (e))
				ReliableReceiveCompleted (sender, e);
		}

		private void DeliverMessage (Protocol protocol, byte[] buffer, int offset)
		{
			ushort mtype = BitConverter.ToUInt16 (buffer, offset + 1);

			this.rreader.Position = offset + BaseHeaderLength;

			Message m = Message.Factory.Create (protocol, mtype);
			if (m == null)
			{
				Disconnect (true);
				return;
			}

			m.ReadPayload (this.rreader);

			OnMessageReceived (new MessageEventArgs (this, m));
		}

		private void ReliableSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				Disconnect (true);
				return;
			}

			SendHolder holder = (SendHolder)e.UserToken;
			
			#if !NET_4
			lock (writers)
			#endif
			writers.Push (holder.Writer);

			#if !NET_4
			lock (writerAsyncArgs)
			#endif
			writerAsyncArgs.Push (e);

			OnMessageSent (new MessageEventArgs (this, holder.Message));
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
﻿//
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

			byte[] buffer = null;
			#if NET_4
			if (!buffers.TryPop (out buffer))
				buffer = new byte[1024];
			#else
			if (buffers.Count != 0)
			{
				lock (buffers)
				{
					if (buffers.Count != 0)
						buffer = buffers.Pop();
				}
			}

			if (buffer == null)
				buffer = new byte[1024];
			#endif
			
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

			int length;
			buffer = message.Protocol.GetBytes (message, out length, buffer);

			e.SetBuffer (buffer, 0, buffer.Length);
			e.UserToken = new SendHolder { Buffer = buffer, Message = message };

			if (!IsConnected)
			{
				#if !NET_4
				lock (writerAsyncArgs)
				#endif
				writerAsyncArgs.Push (e);

				#if !NET_4
				lock (buffers)
				#endif
				buffers.Push (buffer);
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
			public byte[] Buffer;
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
				MessageHeader header = Protocol.FindHeader (buffer, bufferOffset, remainingData);
				if (header == null)
				{
					Disconnect (true);
					return;
				}

				length = header.Length;
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

				DeliverMessage (header, messageOffset);
				messageOffset += length;
				bufferOffset = messageOffset;
				remainingData -= length;
			}

			if (remainingData > 0)
			{
				byte[] newBuffer = new byte[buffer.Length];
				reader = new BufferValueReader (newBuffer, 0, newBuffer.Length);
				Buffer.BlockCopy (buffer, messageOffset, newBuffer, 0, remainingData);
				buffer = newBuffer;
				bufferOffset = remainingData;
				messageOffset = 0;
			}

//			if ((buffer.Length - messageOffset) < length)
//			{
//				byte[] newBuffer = buffer;
//				if (buffer.Length < length)
//				{
//					newBuffer = new byte[length];
//					reader = new BufferValueReader (newBuffer, 0, newBuffer.Length);
//				}
//
//				Buffer.BlockCopy (buffer, messageOffset, newBuffer, 0, remainingData);
//				buffer = newBuffer;
//
//				bufferOffset = remainingData;
//				messageOffset = 0;
//			}
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

		private void DeliverMessage (MessageHeader header, int offset)
		{
			this.rreader.Position = offset + BaseHeaderLength;

			header.Message.ReadPayload (this.rreader);

			OnMessageReceived (new MessageEventArgs (this, header.Message));
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
			lock (buffers)
			#endif
			buffers.Push (holder.Buffer);

			#if !NET_4
			lock (writerAsyncArgs)
			#endif
			writerAsyncArgs.Push (e);

			OnMessageSent (new MessageEventArgs (this, holder.Message));
		}

		#if NET_4
		private static readonly ConcurrentStack<byte[]> buffers = new ConcurrentStack<byte[]>();
		private static readonly ConcurrentStack<SocketAsyncEventArgs> writerAsyncArgs = new ConcurrentStack<SocketAsyncEventArgs>();
		#else
		private static readonly Stack<byte[]> buffers = new Stack<byte[]>();
		private static readonly Stack<SocketAsyncEventArgs> writerAsyncArgs = new Stack<SocketAsyncEventArgs>();
		#endif
	}
}
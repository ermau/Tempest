//
// NetworkConnection.cs
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
using System.Net;
using System.Net.Sockets;

#if NET_4
using System.Collections.Concurrent;
using System.Threading;
using Tempest.InternalProtocol;

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
		public event EventHandler<DisconnectedEventArgs> Disconnected;

		public bool IsConnected
		{
			get
			{
				if (this.disconnecting || this.reliableSocket != null)
					return false;

				lock (this.stateSync)
					return (this.disconnecting || this.reliableSocket == null || !this.reliableSocket.Connected);
			}
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

			SocketAsyncEventArgs eargs = null;
			#if NET_4
			if (!writerAsyncArgs.TryPop (out eargs))
			{
				while (eargs == null)
				{
					int count = bufferCount;
					if (count == BufferLimit)
					{
						SpinWait wait = new SpinWait();
						while (!writerAsyncArgs.TryPop (out eargs))
							wait.SpinOnce();

						eargs.AcceptSocket = null;
					}
					else if (count == Interlocked.CompareExchange (ref bufferCount, count + 1, count))
					{
						eargs = new SocketAsyncEventArgs();
						eargs.Completed += ReliableSendCompleted;
						eargs.SetBuffer (new byte[1024], 0, 1024);
					}
				}
			}
			else
				eargs.AcceptSocket = null;
			#else
			while (eargs == null)
			{
				lock (writerAsyncArgs)
				{
					if (writerAsyncArgs.Count != 0)
					{
						eargs = writerAsyncArgs.Pop();
						eargs.AcceptSocket = null;
					}
					else
					{
						if (bufferCount != BufferLimit)
						{
							bufferCount++;
							eargs = new SocketAsyncEventArgs();
							eargs.Completed += ReliableSendCompleted;
							eargs.SetBuffer (new byte[1024], 0, 1024);
						}
					}
				}
			}
			#endif

			int length;
			byte[] buffer = message.Protocol.GetBytes (message, out length, eargs.Buffer);

			eargs.SetBuffer (buffer, 0, length);
			eargs.UserToken = message;

			bool sent;
			lock (this.stateSync)
			{
				if (!IsConnected)
				{
					#if !NET_4
					lock (writerAsyncArgs)
					#endif
					writerAsyncArgs.Push (eargs);

					return;
				}

				Interlocked.Increment (ref this.pendingAsync);
				sent = !this.reliableSocket.SendAsync (eargs);
			}

			if (sent)
				ReliableSendCompleted (this.reliableSocket, eargs);
		}

		public void Disconnect (bool now, DisconnectedReason reason = DisconnectedReason.Unknown)
		{
			lock (this.stateSync)
			{
				if (this.disconnecting || this.reliableSocket == null)
					return;

				if (!this.reliableSocket.Connected)
				{
					this.reliableSocket = null;
					OnDisconnected (new DisconnectedEventArgs (this, DisconnectedReason.Unknown));
				}
				else if (now)
				{
					this.reliableSocket.Shutdown (SocketShutdown.Both);
					this.reliableSocket.Disconnect (true);
					Recycle();
					this.reliableSocket = null;
					OnDisconnected (new DisconnectedEventArgs (this, reason));
				}
				else
				{
					this.disconnectingReason = reason;
					this.disconnecting = true;
					Interlocked.Increment (ref this.pendingAsync);

					var args = new SocketAsyncEventArgs { DisconnectReuseSocket = true };
					args.Completed += OnDisconnectCompleted;
					if (!this.reliableSocket.DisconnectAsync (args))
						OnDisconnectCompleted (this.reliableSocket, args);
				}
			}
		}

		public void Dispose()
		{
			Dispose (true);
		}

		protected bool disposed;

		private const int BaseHeaderLength = 7;
		private int maxMessageLength = 1048576;

		protected readonly object stateSync = new object();
		protected int pendingAsync = 0;
		protected bool disconnecting = false;
		protected DisconnectedReason disconnectingReason;

		protected Socket reliableSocket;

		protected byte[] rmessageBuffer = new byte[20480];
		protected BufferValueReader rreader;
		private int rmessageOffset = 0;
		private int rmessageLoaded = 0;

		protected void Dispose (bool disposing)
		{
			if (this.disposed)
				return;

			Disconnect (true);

			this.disposed = true;
		}

		protected virtual void Recycle()
		{
		}

		protected void OnMessageReceived (MessageEventArgs e)
		{
			var mr = this.MessageReceived;
			if (mr != null)
				mr (this, e);
		}

		protected virtual void OnDisconnected (DisconnectedEventArgs e)
		{
			var dc = this.Disconnected;
			if (dc != null)
				dc (this, e);
		}

		protected virtual void OnMessageSent (MessageEventArgs e)
		{
			var sent = this.MessageSent;
			if (sent != null)
				sent (this, e);
		}

		private void BufferMessages (ref byte[] buffer, ref int bufferOffset, ref int messageOffset, ref int remainingData, ref BufferValueReader reader)
		{
			this.lastReceived = DateTime.Now;

			int length = 0;
			while (remainingData > BaseHeaderLength)
			{
				MessageHeader header = null;
				try
				{
					header = Protocols.FindHeader (buffer, messageOffset, remainingData);
				}
				catch
				{
				}

				if (header == null)
				{
					Disconnect (true);
					Interlocked.Decrement (ref this.pendingAsync);
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

			if (remainingData > 0 || messageOffset + BaseHeaderLength >= buffer.Length)
			{
				byte[] newBuffer = new byte[(length > buffer.Length) ? length : buffer.Length];
				reader = new BufferValueReader (newBuffer, 0, newBuffer.Length);
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
				Interlocked.Decrement (ref this.pendingAsync);
				return;
			}

			this.rmessageLoaded += e.BytesTransferred;

			int bufferOffset = e.Offset;
			BufferMessages (ref this.rmessageBuffer, ref bufferOffset, ref this.rmessageOffset, ref this.rmessageLoaded, ref this.rreader);
			e.SetBuffer (this.rmessageBuffer, bufferOffset, this.rmessageBuffer.Length - bufferOffset);
			Interlocked.Decrement (ref this.pendingAsync);

			bool sent;
			lock (this.stateSync)
			{
				if (!IsConnected)
					return;

				Interlocked.Increment (ref this.pendingAsync);
				sent = !this.reliableSocket.ReceiveAsync (e);
			}

			if (sent)
				ReliableReceiveCompleted (sender, e);
		}

		protected DateTime lastReceived;
		protected int pingsOut = 0;

		private void DeliverMessage (MessageHeader header, int offset)
		{
			this.rreader.Position = offset + BaseHeaderLength;

			try
			{
				header.Message.ReadPayload (this.rreader);
			}
			catch
			{
				Disconnect (true);
				return;
			}

			var tmessage = (header.Message as TempestMessage);
			if (tmessage == null)
				OnMessageReceived (new MessageEventArgs (this, header.Message));
			else
				OnTempestMessageReceived (new MessageEventArgs (this, header.Message));
		}

		protected virtual void OnTempestMessageReceived (MessageEventArgs e)
		{
			switch (e.Message.MessageType)
			{
				case (ushort)TempestMessageType.Ping:
					Send (new PongMessage());
					break;

				case (ushort)TempestMessageType.Pong:
					this.pingsOut = 0;
					break;

				case (ushort)TempestMessageType.Disconnect:
					var msg = (DisconnectMessage)e.Message;
					Disconnect (true, msg.Reason);
					break;
			}
		}

		private void ReliableSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			var message = (Message)e.UserToken;

			#if !NET_4
			lock (writerAsyncArgs)
			#endif
			writerAsyncArgs.Push (e);

			if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				Disconnect (true);
				Interlocked.Decrement (ref this.pendingAsync);
				return;
			}

			OnMessageSent (new MessageEventArgs (this, message));
			Interlocked.Decrement (ref this.pendingAsync);
		}

		private void OnDisconnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			lock (this.stateSync)
			{
				this.disconnecting = false;
				Recycle();
				this.reliableSocket = null;
			}

			OnDisconnected (new DisconnectedEventArgs (this, this.disconnectingReason));
			Interlocked.Decrement (ref this.pendingAsync);
		}

		// TODO: Better buffer limit
		private static readonly int BufferLimit = Environment.ProcessorCount * 10;
		private static volatile int bufferCount = 0;

		#if NET_4
		private static readonly ConcurrentStack<SocketAsyncEventArgs> writerAsyncArgs = new ConcurrentStack<SocketAsyncEventArgs>();
		#else
		private static readonly Stack<SocketAsyncEventArgs> writerAsyncArgs = new Stack<SocketAsyncEventArgs>();
		#endif
	}
}
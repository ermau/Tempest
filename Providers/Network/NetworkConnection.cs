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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Tempest.InternalProtocol;
using System.Threading;

#if NET_4
using System.Collections.Concurrent;
#endif

namespace Tempest.Providers.Network
{
	public abstract class NetworkConnection
		: IConnection
	{
		protected NetworkConnection (IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> publicKeyCryptoFactory)
		{
			if (protocols == null)
				throw new ArgumentNullException ("protocols");
			if (publicKeyCryptoFactory == null)
				throw new ArgumentNullException ("publicKeyCrypto");

			this.publicKeyCryptoFactory = publicKeyCryptoFactory;
			this.pkAuthentication = publicKeyCryptoFactory();

			this.protocols = new Dictionary<byte, Protocol>();
			foreach (Protocol p in protocols)
			{
				if (p == null)
					throw new ArgumentNullException ("protocols", "protocols contains a null protocol");
				if (this.protocols.ContainsKey (p.id))
					throw new ArgumentException ("Only one version of a protocol may be specified");

				this.protocols.Add (p.id, p);
			}

			this.protocols[1] = TempestMessage.InternalProtocol;
			
			#if TRACE
			this.connectionId = Interlocked.Increment (ref nextConnectionId);
			this.typeName = GetType().Name;
			#endif
		}

		protected NetworkConnection (IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> publicKeyCryptoFactory, IAsymmetricKey authenticationKey)
			: this (protocols, publicKeyCryptoFactory)
		{
			if (authenticationKey == null)
				throw new ArgumentNullException ("authenticationKey");

			this.authenticationKey = authenticationKey;
		}

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
				lock (this.stateSync)
					return (!this.disconnecting && this.reliableSocket != null && this.reliableSocket.Connected);
			}
		}

		public IEnumerable<Protocol> Protocols
		{
			get { return this.protocols.Values; }
		}

		public int ResponseTime
		{
			get;
			private set;
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

		public IAsymmetricKey PublicAuthenticationKey
		{
			get { return this.publicAuthenticationKey; }
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
							eargs.SetBuffer (new byte[1024], 0, 1024);
						}
					}
				}
			}
			#endif

			int length;
			byte[] buffer = GetBytes (message, out length, eargs.Buffer);

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
				
				eargs.Completed += ReliableSendCompleted;
				int p = Interlocked.Increment (ref this.pendingAsync);
				Trace.WriteLine (String.Format ("Increment pending: {0}", p), String.Format ("{1} Send({0})", message, this.typeName));
				sent = !this.reliableSocket.SendAsync (eargs);
			}

			if (sent)
				ReliableSendCompleted (this.reliableSocket, eargs);
		}

		public void Disconnect (bool now, ConnectionResult reason = ConnectionResult.FailedUnknown)
		{
			Disconnect (now, reason, null);
		}

		public void Dispose()
		{
			Dispose (true);
		}

		protected bool disposed;

		private const int BaseHeaderLength = 7;
		private int maxMessageLength = 1048576;

		#if TRACE
		protected int connectionId;
		#endif

		protected Dictionary<byte, Protocol> protocols;

		protected AesManaged aes;
		protected HMACSHA256 hmac;

		protected string signingHashAlgorithm = "SHA256";
		protected readonly Func<IPublicKeyCrypto> publicKeyCryptoFactory;

		protected IPublicKeyCrypto pkAuthentication;
		protected IAsymmetricKey authenticationKey;
		protected IAsymmetricKey publicAuthenticationKey;

		protected readonly object stateSync = new object();
		protected int pendingAsync = 0;
		protected bool disconnecting = false;
		protected bool formallyConnected = false;
		protected ConnectionResult disconnectingReason;
		protected string disconnectingCustomReason;

		protected Socket reliableSocket;

		protected byte[] rmessageBuffer = new byte[20480];
		protected BufferValueReader rreader;
		private int rmessageOffset = 0;
		private int rmessageLoaded = 0;

		internal int NetworkId
		{
			get;
			set;
		}

		protected void Dispose (bool disposing)
		{
			if (this.disposed)
				return;

			Disconnect (true);

			this.disposed = true;
		}

		protected virtual void Recycle()
		{
			NetworkId = 0;

			if (this.hmac != null)
			{
				this.hmac.Dispose();
				this.hmac = null;
			}

			if (this.aes != null)
			{
				this.aes.Dispose();
				this.aes = null;
			}

			this.reliableSocket = null;
			this.rmessageOffset = 0;
			this.rmessageLoaded = 0;
		}

		protected virtual void OnMessageReceived (MessageEventArgs e)
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

		protected void EncryptMessage (BufferValueWriter writer, ref int headerLength)
		{
			if (this.aes == null)
				throw new InvalidOperationException ("Attempting to encrypt a message without an encryptor");

			ICryptoTransform encryptor = null;
			byte[] iv = null;
			lock (this.aes)
			{
				this.aes.GenerateIV();
				iv = this.aes.IV;
				encryptor = this.aes.CreateEncryptor();
			}

			int r = ((writer.Length - BaseHeaderLength) % encryptor.OutputBlockSize);
			if (r != 0)
				writer.Pad (encryptor.OutputBlockSize - r);

			byte[] payload = encryptor.TransformFinalBlock (writer.Buffer, BaseHeaderLength, writer.Length - BaseHeaderLength);

			writer.Length = BaseHeaderLength;
			writer.InsertBytes (BaseHeaderLength, iv, 0, iv.Length);
			writer.WriteBytes (payload);

			headerLength += iv.Length;
		}

		protected void DecryptMessage (MessageHeader header, ref BufferValueReader r, ref byte[] message, ref int moffset)
		{
			byte[] payload = r.ReadBytes();

			ICryptoTransform decryptor;
			lock (this.aes)
			{
				this.aes.IV = header.IV;
				decryptor = this.aes.CreateDecryptor();
			}

			message = decryptor.TransformFinalBlock (payload, 0, payload.Length);
			moffset = 0;

			r = new BufferValueReader (message);
		}

		protected virtual void SignMessage (string hashAlg, BufferValueWriter writer, int headerLength)
		{
			if (this.hmac == null)
				throw new InvalidOperationException();

			writer.WriteBytes (this.hmac.ComputeHash (writer.Buffer, headerLength, writer.Length - headerLength));
		}

		protected virtual bool VerifyMessage (string hashAlg, Message message, byte[] signature, byte[] data, int moffset, int length)
		{
			byte[] ourhash = this.hmac.ComputeHash (data, moffset, length);

			if (signature.Length != ourhash.Length)
				return false;

			for (int i = 0; i < signature.Length; i++)
			{
				if (signature[i] != ourhash[i])
					return false;
			}

			return true;
		}

		protected byte[] GetBytes (Message message, out int length, byte[] buffer)
		{
			BufferValueWriter writer = new BufferValueWriter (buffer);
			writer.WriteByte (message.Protocol.id);
			writer.WriteUInt16 (message.MessageType);
			writer.Length += sizeof (int); // length  placeholder

			message.WritePayload (writer);

			int headerLength = BaseHeaderLength;

			if (message.Encrypted)
				EncryptMessage (writer, ref headerLength);

			if (message.Authenticated)
				SignMessage (this.signingHashAlgorithm, writer, headerLength);

			byte[] rawMessage = writer.Buffer;
			length = writer.Length;
			Buffer.BlockCopy (BitConverter.GetBytes (length), 0, rawMessage, BaseHeaderLength - sizeof(int), sizeof(int));

			return rawMessage;
		}

		/// <returns><c>true</c> if there was sufficient data to retrieve the message's header.</returns>
		/// <remarks>
		/// If <see cref="TryGetHeader"/> returns <c>true</c> and <paramref name="header"/> is <c>null</c>,
		/// disconnect.
		/// </remarks>
		protected bool TryGetHeader (byte[] buffer, int offset, int remaining, out MessageHeader header)
		{
			header = null;

			ushort type;
			int mlen;
			try
			{
				type = BitConverter.ToUInt16 (buffer, offset + 1);
				mlen = BitConverter.ToInt32 (buffer, offset + 1 + sizeof (ushort));

				Protocol p;
				if (!this.protocols.TryGetValue (buffer[offset], out p))
					return true;

				Message msg = p.Create (type);
				if (msg == null)
					return true;

				offset += BaseHeaderLength;

				int headerLength = BaseHeaderLength;

				byte[] iv = null;
				if (msg.Encrypted && this.aes != null)
				{
					int length = this.aes.IV.Length;
					iv = new byte[length];

					headerLength += length;
					if (remaining < headerLength)
						return false;

					Buffer.BlockCopy (buffer, offset, iv, 0, length);
					offset += length;
				}

				header = new MessageHeader (p, msg, mlen, headerLength, iv);
				return true;
			}
			catch (Exception ex)
			{
				header = null;
				return true;
			}
		}

		private void BufferMessages (ref byte[] buffer, ref int bufferOffset, ref int messageOffset, ref int remainingData, ref BufferValueReader reader)
		{
			#if TRACE
			int c = Interlocked.Increment (ref nextCallId);
			#endif
			Trace.WriteLine ("Entering", String.Format ("{0}:{6} {1}:BufferMessages({2},{3},{4},{5})", this.typeName, c, buffer.Length, bufferOffset, messageOffset, remainingData, connectionId));

			this.lastReceived = DateTime.Now;

			int length = 0;
			while (remainingData >= BaseHeaderLength)
			{
				MessageHeader header;
				if (!TryGetHeader (buffer, messageOffset, remainingData, out header))
					break;

				if (header == null)
				{
					Disconnect (true);
					Trace.WriteLine ("Exiting (header not found)",
					                 String.Format ("{0}:{6} {1}:BufferMessages({2},{3},{4},{5})", this.typeName, c, buffer.Length,
					                                bufferOffset, messageOffset, remainingData, connectionId));
					return;
				}

				length = header.MessageLength;
				if (length > maxMessageLength)
				{
					Disconnect (true);
					Trace.WriteLine ("Exiting (bad message size)",
					                 String.Format ("{0}:{6} {1}:BufferMessages({2},{3},{4},{5})", this.typeName, c, buffer.Length,
					                                bufferOffset, messageOffset, remainingData, connectionId));
					return;
				}

				if (remainingData < length)
				{
					bufferOffset += remainingData;
					break;
				}

				if (!IsConnected)
					return;

				try
				{
					int moffset = messageOffset + header.HeaderLength;
					byte[] message = buffer;
					BufferValueReader r = reader;

					r.Position = moffset;
					if (header.Message.Encrypted)
						DecryptMessage (header, ref r, ref message, ref moffset);

					r.Position = moffset;
					header.Message.ReadPayload (r);

					if (header.Message.Authenticated)
					{
						byte[] signature = reader.ReadBytes(); // Need the original reader here, sig is after payload
						if (!VerifyMessage (this.signingHashAlgorithm, header.Message, signature, buffer, messageOffset + header.HeaderLength, header.MessageLength - header.HeaderLength - signature.Length - sizeof(int)))
						{
							Disconnect (true, ConnectionResult.MessageAuthenticationFailed);
							Trace.WriteLine ("Exiting (message auth failed)",
											 String.Format ("{0}:{6} {1}:BufferMessages({2},{3},{4},{5})", this.typeName, c, buffer.Length,
															bufferOffset, messageOffset, remainingData, connectionId));
							return;
						}
					}
				}
				catch (Exception ex)
				{
					Disconnect (true);
					Trace.WriteLine ("Exiting for error: " + ex,
					                 String.Format ("{0}:{6} {1}:BufferMessages({2},{3},{4},{5})", this.typeName, c, buffer.Length,
					                                bufferOffset, messageOffset, remainingData, connectionId));
					return;
				}

				var tmessage = (header.Message as TempestMessage);
				if (tmessage == null)
					OnMessageReceived (new MessageEventArgs (this, header.Message));
				else
					OnTempestMessageReceived (new MessageEventArgs (this, header.Message));

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

			Trace.WriteLine ("Exiting", String.Format ("{0}:{6} {1}:BufferMessages({2},{3},{4},{5})", this.typeName, c, buffer.Length, bufferOffset, messageOffset, remainingData, connectionId));
		}

		protected void ReliableReceiveCompleted (object sender, SocketAsyncEventArgs e)
		{
			#if TRACE
			int c = Interlocked.Increment (ref nextCallId);
			#endif

			int p;
			Trace.WriteLine ("Entering", String.Format ("{2}:{4} {3}:ReliableReceiveCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
			bool async;
			do
			{
				if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
				{
					Disconnect (true);
					p = Interlocked.Decrement (ref this.pendingAsync);
					Trace.WriteLine (String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{4} {3}:ReliableReceiveCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
					Trace.WriteLine ("Exiting (error)", String.Format ("{2}:{4} {3}:ReliableReceiveCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
					return;
				}

				p = Interlocked.Decrement (ref this.pendingAsync);
				Trace.WriteLine (String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{4} {3}:ReliableReceiveCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));

				this.rmessageLoaded += e.BytesTransferred;

				int bufferOffset = e.Offset;
				BufferMessages (ref this.rmessageBuffer, ref bufferOffset, ref this.rmessageOffset, ref this.rmessageLoaded,
				                ref this.rreader);
				e.SetBuffer (this.rmessageBuffer, bufferOffset, this.rmessageBuffer.Length - bufferOffset);

				lock (this.stateSync)
				{
					if (!IsConnected)
					{
						Trace.WriteLine ("Exiting (not connected)", String.Format ("{2}:{4} {3}:ReliableReceiveCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
						return;
					}

					p = Interlocked.Increment (ref this.pendingAsync);
					Trace.WriteLine (String.Format ("Increment pending: {0}", p), String.Format ("{2}:{4} {3}:ReliableReceiveCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
					async = this.reliableSocket.ReceiveAsync (e);
				}
			} while (!async);

			Trace.WriteLine ("Exiting", String.Format ("{2}:{4} {3}:ReliableReceiveCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
		}

		protected long lastSent;
		protected DateTime lastReceived;
		protected int pingsOut = 0;

		protected virtual void OnTempestMessageReceived (MessageEventArgs e)
		{
			switch (e.Message.MessageType)
			{
				case (ushort)TempestMessageType.Ping:
					Send (new PongMessage());
					this.lastSent = Stopwatch.GetTimestamp();
					break;

				case (ushort)TempestMessageType.Pong:
					ResponseTime = (int)Math.Round (TimeSpan.FromTicks (Stopwatch.GetTimestamp() - this.lastSent).TotalMilliseconds, 0);
					this.pingsOut = 0;
					break;

				case (ushort)TempestMessageType.Disconnect:
					var msg = (DisconnectMessage)e.Message;
					Disconnect (true, msg.Reason, msg.CustomReason);
					break;
			}
		}

		private void Disconnect (bool now, ConnectionResult reason, string customReason)
		{
			#if TRACE
			int c = Interlocked.Increment (ref nextCallId);
			#endif
			Trace.WriteLine (String.Format ("Entering {0}", Environment.StackTrace), String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

			if (this.disconnecting || this.reliableSocket == null)
			{
				Trace.WriteLine ("Already disconnected, exiting", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));
				return;
			}

			lock (this.stateSync)
			{
				Trace.WriteLine ("Got state lock.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

				if (this.disconnecting || this.reliableSocket == null)
				{
					Trace.WriteLine ("Already disconnected, exiting", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));
					return;
				}

				this.disconnecting = true;

				if (!this.reliableSocket.Connected)
				{
					Trace.WriteLine ("Socket not connected, finishing cleanup.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					Recycle();

					while (this.pendingAsync > 1) // If called from *Completed, there'll be a pending.
						Thread.Sleep (0);

					this.disconnecting = false;
				}
				else if (now)
				{
					Trace.WriteLine ("Shutting down socket.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					this.reliableSocket.Shutdown (SocketShutdown.Both);
					this.reliableSocket.Disconnect (true);
					Recycle();

					Trace.WriteLine (String.Format ("Waiting for pending ({0}) async.", pendingAsync), String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					while (this.pendingAsync > 1)
						Thread.Sleep (0);

					Trace.WriteLine ("Finished waiting, raising Disconnected.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					this.disconnecting = false;
				}
				else
				{
					Trace.WriteLine ("Disconnecting asynchronously.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					this.disconnectingReason = reason;
					this.disconnectingCustomReason = customReason;

					int p = Interlocked.Increment (ref this.pendingAsync);
					Trace.WriteLine (String.Format ("Increment pending: {0}", p), String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					ThreadPool.QueueUserWorkItem (s =>
					{
						Trace.WriteLine (String.Format ("Async DC waiting for pending ({0}) async.", pendingAsync), String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

						while (this.pendingAsync > 2) // Disconnect is pending.
							Thread.Sleep (0);

						Trace.WriteLine ("Finished waiting, disconnecting async.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

						var args = new SocketAsyncEventArgs { DisconnectReuseSocket = true };
						args.Completed += OnDisconnectCompleted;
						if (!this.reliableSocket.DisconnectAsync (args))
							OnDisconnectCompleted (this.reliableSocket, args);
					});

					return;
				}
			}

			OnDisconnected (new DisconnectedEventArgs (this, reason, customReason));
			Trace.WriteLine ("Raised Disconnected, exiting", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));
		}

		private void ReliableSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			#if TRACE
			int c = Interlocked.Increment (ref nextCallId);
			#endif
			Trace.WriteLine ("Entering", String.Format ("{2}:{4} {3}:ReliableSendCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));

			e.Completed -= ReliableSendCompleted;

			var message = (Message)e.UserToken;

			#if !NET_4
			lock (writerAsyncArgs)
			#endif
			writerAsyncArgs.Push (e);

			int p;
			if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				Disconnect (true);
				p = Interlocked.Decrement (ref this.pendingAsync);
				Trace.WriteLine (String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{4} {3}:ReliableSendCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
				Trace.WriteLine ("Exiting (error)", String.Format ("{2}:{4} {3}:ReliableSendCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
				return;
			}

			if (!(message is TempestMessage))
				OnMessageSent (new MessageEventArgs (this, message));

			p = Interlocked.Decrement (ref this.pendingAsync);
			Trace.WriteLine (String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{4} {3}:ReliableSendCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
			Trace.WriteLine ("Exiting", String.Format ("{2}:{4} {3}:ReliableSendCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
		}

		private void OnDisconnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			#if TRACE
			int c = Interlocked.Increment (ref nextCallId);
			#endif
			Trace.WriteLine ("Entering", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));

			lock (this.stateSync)
			{
				Trace.WriteLine ("Got lock", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));

				this.disconnecting = false;
				Recycle();
			}

			Trace.WriteLine ("Raising Disconnected", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));

			OnDisconnected (new DisconnectedEventArgs (this, this.disconnectingReason, this.disconnectingCustomReason));
			int p = Interlocked.Decrement (ref this.pendingAsync);
			Trace.WriteLine (String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
			Trace.WriteLine ("Exiting", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
		}

		// TODO: Better buffer limit
		private static readonly int BufferLimit = Environment.ProcessorCount * 10;
		private static volatile int bufferCount = 0;
		
		#if TRACE
		protected static int nextCallId = 0;
		protected static int nextConnectionId;
		protected readonly string typeName;
		#endif

		#if NET_4
		private static readonly ConcurrentStack<SocketAsyncEventArgs> writerAsyncArgs = new ConcurrentStack<SocketAsyncEventArgs>();
		#else
		private static readonly Stack<SocketAsyncEventArgs> writerAsyncArgs = new Stack<SocketAsyncEventArgs>();
		#endif
	}
}
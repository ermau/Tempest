//
// NetworkConnection.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012 Eric Maupin
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
using System.Text;
using Tempest.InternalProtocol;
using System.Threading;
using System.Threading.Tasks;

#if NET_4
using System.Collections.Concurrent;
#endif

namespace Tempest.Providers.Network
{
	public abstract class NetworkConnection
		: IConnection
	{
		/// <summary>
		/// Gets or sets the global limit for number of send buffers (for both <see cref="NetworkServerConnection"/> and <see cref="NetworkClientConnection" />).
		/// (Default: <see cref="Environment.ProcessorCount"/>.)
		/// </summary>
		/// <remarks>
		/// <para>
		/// Can not be adjusted dynamically. If reduced below previous levels, it will prevent new buffers from being created.
		/// However, it won't remove buffers past the limit that already existed.
		/// </para>
		/// <para>
		/// You should consider <see cref="SendBufferLimit"/> * <see cref="MaxMessageSize"/> for memory usage potential.
		/// </para>
		/// </remarks>
		/// <seealso cref="MaxMessageSize"/>
		public static int SendBufferLimit
		{
			get { return sendBufferLimit; }
			set { sendBufferLimit = value; }
		}

		/// <summary>
		/// Gets or sets whether the global limit for number of send buffers should grow with additional connections. (Default: <c>true</c>).
		/// </summary>
		public static bool AutoSizeSendBufferLimit
		{
			get { return autoSizeSendBufferLimit; }
			set { autoSizeSendBufferLimit = value; }
		}

		/// <summary>
		/// Gets or sets the number of send buffers added per connection when <see cref="AutoSizeSendBufferLimit"/> is <c>true</c>.
		/// </summary>
		public static int AutoSizeFactor
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the maximum message size.
		/// </summary>
		/// <seealso cref="SendBufferLimit" />
		/// <remarks>
		/// <para>
		/// You should consider <see cref="MaxMessageSize"/> * <c>maxConnections</c> (<see cref="NetworkConnectionProvider(Tempest.Protocol,System.Net.IPEndPoint,int)"/>) for receive memory usage.
		/// </para>
		/// </remarks>
		public static int MaxMessageSize
		{
			get { return maxMessageSize; }
			set { maxMessageSize = value; }
		}

		protected NetworkConnection (IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> publicKeyCryptoFactory, IAsymmetricKey authKey, bool generateKey)
		{
			if (protocols == null)
				throw new ArgumentNullException ("protocols");
			if (publicKeyCryptoFactory == null)
				throw new ArgumentNullException ("publicKeyCrypto");

			this.authenticationKey = authKey;
			this.requiresHandshake = protocols.Any (p => p.RequiresHandshake);
			if (this.requiresHandshake)
			{
				this.publicKeyCryptoFactory = publicKeyCryptoFactory;

				ThreadPool.QueueUserWorkItem (s =>
				{
					this.pkAuthentication = this.publicKeyCryptoFactory();

					if (this.authenticationKey == null)
					{
						if (generateKey)
						{
							this.publicAuthenticationKey = this.pkAuthentication.ExportKey (false);
							this.authenticationKey = this.pkAuthentication.ExportKey (true);
						}
					}
					else
					{
						this.pkAuthentication.ImportKey (authKey);
						this.publicAuthenticationKey = this.pkAuthentication.ExportKey (false);
					}

					this.authReady = true;
				});
			}

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

			//this.sendArgs.SetBuffer (new byte[1024], 0, 1024);
			//this.sendArgs.Completed += ReliableSendCompleted;
			
			#if TRACE
			this.connectionId = Interlocked.Increment (ref nextConnectionId);
			this.typeName = GetType().Name;
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Empty, this.typeName + ":" + this.connectionId + " Ctor");
			#endif
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
				Socket rs = this.reliableSocket;
				return (!this.disconnecting && rs != null && rs.Connected);
			}
		}

		public long ConnectionId
		{
			get;
			protected set;
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

		/// <summary>
		/// Gets the total number of bytes sent in the lifetime of this connection.
		/// </summary>
		public long BytesSent
		{
			get { return this.bytesSent; }
		}

		/// <summary>
		/// Gets the total number of bytes received in the lifetime of this connection.
		/// </summary>
		public long BytesReceived
		{
			get { return this.bytesReceived; }
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

		public abstract IAsymmetricKey RemoteKey
		{
			get;
		}

		public IAsymmetricKey LocalKey
		{
			get
			{
				while (this.requiresHandshake && !this.authReady)
					Thread.Sleep (0);

				return this.publicAuthenticationKey;
			}
		}

		public IEnumerable<MessageEventArgs> Tick()
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Adds an accepted protocol to the connection
		/// </summary>
		/// <param name="protocol">The protocol to add.</param>
		/// <exception cref="ArgumentNullException"><paramref name="protocol"/> is <c>null</c>.</exception>
		/// <exception cref="InvalidOperationException"><see cref="IsConnected"/> is <c>true</c>.</exception>
		public void AddProtocol (Protocol protocol)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			if (IsConnected)
				throw new InvalidOperationException ("Can not add a protocol while connected");

			this.protocols.Add (protocol.id, protocol);
		}

		public virtual void Send (Message message)
		{
			SendMessage (message);
		}

		public Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0)
			where TResponse : Message
		{
			var tcs = new TaskCompletionSource<Message>();
			SendMessage (message, false, tcs, timeout);

			return tcs.Task.ContinueWith (t => (TResponse)t.Result);
		}

		public void SendResponse (Message originalMessage, Message response)
		{
			if (originalMessage == null)
				throw new ArgumentNullException ("originalMessage");
			if (originalMessage.IsResponse)
				throw new ArgumentException ("originalMessage can't be a response", "originalMessage");
			if (response == null)
				throw new ArgumentNullException ("response");

			response.MessageId = originalMessage.MessageId;
			SendMessage (response, true);
		}

		public void DisconnectAsync()
		{
			Disconnect (false);
		}

		public void DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			Disconnect (false, reason, customReason);
		}

		public void Disconnect()
		{
			Disconnect (true);
		}

		public void Disconnect (ConnectionResult reason, string customReason = null)
		{
			Disconnect (true, reason, customReason);
		}

		public void Dispose()
		{
			Dispose (true);
		}

		protected bool authReady;
		protected bool disposed;

		private const int ResponseFlag = 16777216;
		private const int MaxMessageId = 8388608;
		private const int BaseHeaderLength = 11;

		protected int connectionId;
		protected readonly string typeName;

		protected Dictionary<byte, Protocol> protocols;
		protected bool requiresHandshake;

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

		protected readonly object messageIdSync = new object();
		protected int nextMessageId;
		protected int lastMessageId;

		protected Socket reliableSocket;

		protected MessageHeader currentHeader;
		protected byte[] rmessageBuffer = new byte[20480];
		protected BufferValueReader rreader;
		private int rmessageOffset = 0;
		private int rmessageLoaded = 0;

		protected long lastActivity;
		private long bytesReceived;
		private long bytesSent;

		protected void Dispose (bool disposing)
		{
			if (this.disposed)
				return;

			this.disposed = true;
			Disconnect (true);

			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Waiting for {0} pending asyncs", this.pendingAsync), String.Format ("{0}:{1} Dispose()", this.typeName, connectionId));

			while (this.pendingAsync > 0)
				Thread.Sleep (1);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Disposed", String.Format ("{0}:{1} Dispose()", this.typeName, connectionId));
		}

		protected virtual void Recycle()
		{
			lock (this.stateSync)
			{
				ConnectionId = 0;

				if (this.hmac != null)
				{
					lock (this.hmac)
					{
						((IDisposable)this.hmac).Dispose();
						this.hmac = null;
					}
				}

				if (this.aes != null)
				{
					lock (this.aes)
					{
						((IDisposable)this.aes).Dispose();
						this.aes = null;
					}
				}

				if (this.reliableSocket != null)
					this.reliableSocket.Dispose();

				this.reliableSocket = null;
				this.rmessageOffset = 0;
				this.rmessageLoaded = 0;
				this.bytesReceived = 0;
				this.bytesSent = 0;
				this.lastMessageId = 0;
				this.nextMessageId = 0;

				#if NET_4
				lock (this.messageResponses)
					this.messageResponses.Clear();
				#endif
			}
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
			AesManaged am = this.aes;
			if (am == null)
				return;

			ICryptoTransform encryptor = null;
			byte[] iv = null;
			lock (am)
			{
				am.GenerateIV();
				iv = am.IV;
				encryptor = am.CreateEncryptor();
			}

			const int workingHeaderLength = 7; // right after length

			int r = ((writer.Length - workingHeaderLength) % encryptor.OutputBlockSize);
			if (r != 0)
			    writer.Pad (encryptor.OutputBlockSize - r);

			byte[] payload = encryptor.TransformFinalBlock (writer.Buffer, workingHeaderLength, writer.Length - workingHeaderLength);

			writer.Length = workingHeaderLength;
			writer.InsertBytes (workingHeaderLength, iv, 0, iv.Length);
			writer.WriteInt32 (payload.Length);
			writer.InsertBytes (writer.Length, payload, 0, payload.Length);

			headerLength += iv.Length;
		}

		protected void DecryptMessage (MessageHeader header, ref BufferValueReader r)
		{
			int c = 0;
			#if TRACE
			c = GetNextCallId();
			#endif

			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", String.Format ("{0}:{2} {1}:DecryptMessage({3},{4})", this.typeName, c, connectionId, header.IV.Length, r.Position));

			int payloadLength = r.ReadInt32();

			AesManaged am = this.aes;
			if (am == null)
				return;

			ICryptoTransform decryptor;
			lock (am)
			{
				am.IV = header.IV;
				decryptor = am.CreateDecryptor();
			}

			byte[] message = decryptor.TransformFinalBlock (r.Buffer, r.Position, payloadLength);
			r.Position += payloadLength; // Advance original reader position
			r = new BufferValueReader (message);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", String.Format ("{0}:{2} {1}:DecryptMessage({3},{4},{5})", this.typeName, c, connectionId, header.IV.Length, r.Position, message.Length));
		}

		protected virtual void SignMessage (string hashAlg, BufferValueWriter writer)
		{
			if (this.hmac == null)
				throw new InvalidOperationException();
			
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{0}:{2} {1}:SignMessage ({3},{4})", this.typeName, c, connectionId, hashAlg, writer.Length);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			byte[] hash;
			lock (this.hmac)
				 hash = this.hmac.ComputeHash (writer.Buffer, 0, writer.Length);

			//Trace.WriteLineIf (NTrace.TraceVerbose, "Got hash:  " + GetHex (hash), callCategory);

			writer.WriteBytes (hash);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}

		protected virtual bool VerifyMessage (string hashAlg, Message message, byte[] signature, byte[] data, int moffset, int length)
		{
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{0}:{2} {1}:VerifyMessage({3},{4},{5},{6},{7},{8})", this.typeName, c, connectionId, hashAlg, message, signature.Length, data.Length, moffset, length);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			byte[] ourhash;
			lock (this.hmac)
				ourhash = this.hmac.ComputeHash (data, moffset, length);
			
			//Trace.WriteLineIf (NTrace.TraceVerbose, "Their hash: " + GetHex (signature), callCategory);
			//Trace.WriteLineIf (NTrace.TraceVerbose, "Our hash:   " + GetHex (ourhash), callCategory);

			if (signature.Length != ourhash.Length)
				return false;

			for (int i = 0; i < signature.Length; i++)
			{
				if (signature[i] != ourhash[i])
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (false)", callCategory);
					return false;
				}
			}

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (true)", callCategory);
			return true;
		}

		private string GetHex (byte[] array)
		{
			//return array.Aggregate (String.Empty, (s, b) => s + b.ToString ("X2"));
			char[] hex = new char[array.Length * 2];
			for (int i = 0; i < array.Length; ++i)
			{
			    string x2 = array[i].ToString ("X2");
			    hex[i * 2] = x2[0];
			    hex[(i * 2) + 1] = x2[1];
			}
			
			return new string (hex);
		}

		#if SAFE
		protected byte[] GetBytes (Message message, out int length, byte[] buffer, bool isResponse)
		#else
		protected unsafe byte[] GetBytes (Message message, out int length, byte[] buffer, bool isResponse)
		#endif
		{
			int messageId = message.MessageId;
			if (isResponse)
				messageId |= ResponseFlag;

			BufferValueWriter writer = new BufferValueWriter (buffer);
			writer.WriteByte (message.Protocol.id);
			writer.WriteUInt16 (message.MessageType);
			writer.Length += sizeof (int); // length placeholder
			const int lengthOffset = 1 + sizeof (ushort);

			writer.WriteInt32 (messageId);

			var context = new SerializationContext (this, this.protocols[message.Protocol.id], new TypeMap());

			message.WritePayload (context, writer);

			int headerLength = BaseHeaderLength;

			IList<KeyValuePair<Type, ushort>> types;
			bool hasTypes = context.TypeMap.TryGetNewTypes (out types);
			if (hasTypes)
			{
				if (types.Count > Int16.MaxValue)
					throw new ArgumentException ("Too many different types for serialization");

				int payloadLen = writer.Length;
				byte[] payload = writer.Buffer;
				writer = new BufferValueWriter (new byte[1024 + writer.Length]);
				writer.WriteByte (message.Protocol.id);
				writer.WriteUInt16 (message.MessageType);
				writer.Length += sizeof (int); // length placeholder
				writer.WriteInt32 (messageId);

				writer.Length += sizeof (ushort); // type header length placeholder
				writer.WriteUInt16 ((ushort)types.Count);
				for (int i = 0; i < types.Count; ++i)
					writer.WriteString (types[i].Key.GetSimplestName());

				#if SAFE
				Buffer.BlockCopy (BitConverter.GetBytes ((ushort)(writer.Length - BaseHeaderLength)), 0, writer.Buffer, BaseHeaderLength, sizeof (ushort));
				#else
				fixed (byte* mptr = writer.Buffer)
					*((ushort*) (mptr + BaseHeaderLength)) = (ushort)(writer.Length - BaseHeaderLength);
				#endif

				headerLength = writer.Length;
				writer.InsertBytes (headerLength, payload, BaseHeaderLength, payloadLen - BaseHeaderLength);
			}

			if (message.Encrypted)
			{
				EncryptMessage (writer, ref headerLength);
			}
			else if (message.Authenticated)
			{
				for (int i = lengthOffset; i < lengthOffset + sizeof(int); ++i)
					writer.Buffer[i] = 0;

				SignMessage (this.signingHashAlgorithm, writer);
			}

			byte[] rawMessage = writer.Buffer;
			length = writer.Length;
			int len = length << 1;
			if (hasTypes)
				len |= 1; // serialization header

			#if SAFE
			Buffer.BlockCopy (BitConverter.GetBytes (len), 0, rawMessage, lengthOffset, sizeof(int));
			#else
			fixed (byte* mptr = rawMessage)
				*((int*) (mptr + lengthOffset)) = len;
			#endif

			return rawMessage;
		}

		private readonly object sendSync = new object();
		private SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();
		private readonly Dictionary<int, TaskCompletionSource<Message>> messageResponses = new Dictionary<int, TaskCompletionSource<Message>>();	
	
		private void SendMessage (Message message, bool isResponse = false, TaskCompletionSource<Message> future = null, int timeout = 0)
		{
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{1}:{2} {3}:Send({0})", message, this.typeName, this.connectionId, c);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			if (message == null)
				throw new ArgumentNullException ("message");
			if (!this.IsConnected)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (not connected)", callCategory);
				return;
			}

			SocketAsyncEventArgs eargs = null;
			#if NET_4
			if (timeout > 0)
			    throw new NotSupportedException ("Response timeout not support");

			if (!writerAsyncArgs.TryPop (out eargs))
			{
				while (eargs == null)
				{
					int count = bufferCount;
					if (count == sendBufferLimit)
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
						eargs.Completed += ReliableSendCompleted;
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
						#if !SILVERLIGHT
						eargs.AcceptSocket = null;
						#endif
					}
					else
					{
						if (bufferCount != sendBufferLimit)
						{
							bufferCount++;
							eargs = new SocketAsyncEventArgs();
							eargs.SetBuffer (new byte[1024], 0, 1024);
						}
					}
				}
			}
			#endif

			bool sent;
			try
			{
				if (!isResponse)
				{
					Monitor.Enter (this.sendSync);
					lock (this.messageIdSync)
					{
						message.MessageId = this.nextMessageId++;

						if (this.nextMessageId > MaxMessageId)
							this.nextMessageId = 0;
					}
				}

				if (future != null)
				{
					lock (this.messageResponses)
						this.messageResponses.Add (message.MessageId, future);
				}

				int length;
				byte[] buffer = GetBytes (message, out length, eargs.Buffer, isResponse);

				eargs.SetBuffer (buffer, 0, length);
				eargs.UserToken = new KeyValuePair<NetworkConnection, Message> (this, message);

				int p = Interlocked.Increment (ref this.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), callCategory);

				if (!IsConnected)
				{
					Interlocked.Decrement (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);

					#if !NET_4
					lock (writerAsyncArgs)
					#endif
					writerAsyncArgs.Push (eargs);

					return;
				}

				sent = !this.reliableSocket.SendAsync (eargs);
			}
			finally
			{
				if (!isResponse)
					Monitor.Exit (this.sendSync);
			}

			if (sent)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Send completed synchronously", callCategory);
				ReliableSendCompleted (this.reliableSocket, eargs);
			}

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}

		/// <returns><c>true</c> if there was sufficient data to retrieve the message's header.</returns>
		/// <remarks>
		/// If <see cref="TryGetHeader"/> returns <c>true</c> and <paramref name="header"/> is <c>null</c>,
		/// disconnect.
		/// </remarks>
		protected bool TryGetHeader (BufferValueReader reader, int remaining, ref MessageHeader header)
		{
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{0}:{4} {1}:TryGetHeader({2},{3})", this.typeName, c, reader.Position, remaining, this.connectionId);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Entering {0}", (header == null) ? "without existing header" : "with existing header"), callCategory);

			int mlen; bool hasTypeHeader; Message msg = null; Protocol p;

			int headerLength = BaseHeaderLength;

			if (header == null)
				header = new MessageHeader();
			else if (header.HeaderLength > 0)
				headerLength = header.HeaderLength;

			try
			{
				if (header.State >= HeaderState.Protocol)
				{
					p = header.Protocol;
					//reader.Position += sizeof(byte);
				}
				else
				{
					byte pid = reader.ReadByte();

					if (!this.protocols.TryGetValue (pid, out p))
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (Protocol " + pid + " not found)", callCategory);
						return true;
					}

					header.Protocol = p;
					header.State = HeaderState.Protocol;
				}

				if (header.State >= HeaderState.Type)
				{
					msg = header.Message;
					//reader.Position += sizeof(ushort);
				}
				else
				{
					ushort type = reader.ReadUInt16();

					msg = header.Message = p.Create (type);
					header.State = HeaderState.Type;

					if (msg == null)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (Message " + type + " not found)", callCategory);
						return true;
					}
					
					if (msg.Encrypted)
						header.IsStillEncrypted = true;

					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Have " + msg.GetType().Name), callCategory);
				}

				if (header.State >= HeaderState.Length)
				{
					mlen = header.MessageLength;
					hasTypeHeader = header.HasTypeHeader;
					//reader.Position += sizeof(int);
				}
				else
				{
					mlen = reader.ReadInt32();
					hasTypeHeader = (mlen & 1) == 1;
					mlen >>= 1;

					if (mlen <= 0)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (length invalid)", callCategory);
						return true;
					}

					header.MessageLength = mlen;
					header.HasTypeHeader = hasTypeHeader;
					header.State = HeaderState.Length;

					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Have message of length: {0}, {1} type header", mlen, (hasTypeHeader) ? "with" : "without"), callCategory);
				}

				if (header.State == HeaderState.IV)
				{
					if (header.IsStillEncrypted)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (message not buffered)", callCategory);
						return !(remaining < mlen);
					}
					else if (header.Message.Encrypted)
						reader.Position = 0;
				}
				else if (msg.Encrypted && this.aes != null)
				{
					int ivLength = this.aes.IV.Length;
					headerLength += ivLength;

					if (remaining < headerLength)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not buffered (IV))", callCategory);
						return false;
					}

					byte[] iv = reader.ReadBytes (ivLength);

					header.HeaderLength = headerLength;
					header.State = HeaderState.IV;
					header.IV = iv;

					if (remaining < mlen)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (message not buffered)", callCategory);
						return false;
					}

					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (need to decrypt)", callCategory);
					return true;
				}

				if (header.State < HeaderState.MessageId)
				{
					int identV = reader.ReadInt32();
					header.MessageId = identV & ~ResponseFlag;
					header.IsResponse = (identV & ResponseFlag) == ResponseFlag;

					msg.MessageId = header.MessageId;

					header.State = HeaderState.MessageId;

					Trace.WriteLineIf (NTrace.TraceVerbose, "Have message ID: " + header.MessageId, callCategory);
				}

				if (header.State < HeaderState.TypeMap)
				{
					TypeMap map;
					if (hasTypeHeader)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Has type header, reading types", callCategory);

						if (remaining < headerLength + (sizeof(ushort) * 2))
						{
							Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not buffered (types))", callCategory);
							return false;
						}

						if (header.TypeHeaderLength == 0)
						{
							ushort typeheaderLength = reader.ReadUInt16();
							headerLength += typeheaderLength;
							header.TypeHeaderLength = typeheaderLength;
						}

						if (remaining < headerLength)
						{
							Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not buffered (types))", callCategory);
							return false;
						}

						ushort numTypes = reader.ReadUInt16();
						var types = new Dictionary<Type, ushort> (numTypes);
						for (ushort i = 0; i < numTypes; ++i)
							types[Type.GetType (reader.ReadString())] = i;

						map = new TypeMap (types);
					}
					else
						map = new TypeMap();

					var context = new SerializationContext (this, p, map);

					header.SerializationContext = context;
					header.HeaderLength = headerLength;
					header.State = HeaderState.TypeMap;
				}

				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
				return true;
			}
			catch (Exception ex)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (error): " + ex, callCategory);
				header = null;
				return true;
			}
		}

		private void BufferMessages (ref byte[] buffer, ref int bufferOffset, ref int messageOffset, ref int remainingData, ref BufferValueReader reader)
		{
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{0}:{6} {1}:BufferMessages({2},{3},{4},{5},{7})", this.typeName, c, buffer.Length, bufferOffset, messageOffset, remainingData, this.connectionId, reader.Position);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			MessageHeader header = this.currentHeader;
			BufferValueReader currentReader = reader;

			int length = 0;
			while (remainingData >= BaseHeaderLength)
			{
				if (!TryGetHeader (currentReader, remainingData, ref header))
				{
					this.currentHeader = header;
					Trace.WriteLineIf (NTrace.TraceVerbose, "Failed to get header", callCategory);
					break;
				}

				this.currentHeader = header;

				if (header == null || header.Message == null)
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not found)", callCategory);
					return;
				}

				length = header.MessageLength;
				if (length > maxMessageSize)
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (bad message size)", callCategory);
					return;
				}

				if (header.State == HeaderState.IV)
				{
					DecryptMessage (header, ref currentReader);
					header.IsStillEncrypted = false;
					continue;
				}

				if (!header.IsResponse)
				{
					if (header.MessageId == MaxMessageId)
						this.lastMessageId = -1;
					else if (header.MessageId < this.lastMessageId)
					{
						Disconnect (true);
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (replay attack / reliable out of order)", callCategory);
						return;
					}

					this.lastMessageId = (header.MessageId != MaxMessageId) ? header.MessageId : 0; // BUG: Skipped messages will break this
				}
				else if (header.MessageId > this.nextMessageId)
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (response is replay attack / reliable out of order)", callCategory);
					return;
				}

				if (remainingData < length)
				{
					bufferOffset += remainingData;
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Message not fully received (boffset={0})", bufferOffset), callCategory);
					break;
				}

				if (!IsConnected)
					return;

				try
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Reading payload for message {0}", header.Message), callCategory);
					header.Message.ReadPayload (header.SerializationContext, currentReader);

					if (!header.Message.Encrypted && header.Message.Authenticated)
					{
						// Zero out length for message signing comparison
						for (int i = 3 + messageOffset; i < 7 + messageOffset; ++i)
							buffer[i] = 0;

						int payloadLength = reader.Position;
						byte[] signature = reader.ReadBytes();
						if (!VerifyMessage (this.signingHashAlgorithm, header.Message, signature, buffer, messageOffset, payloadLength - messageOffset))
						{
							Disconnect (true, ConnectionResult.MessageAuthenticationFailed);
							Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (message auth failed)", callCategory);
							return;
						}
					}
				}
				catch (Exception ex)
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting for error: " + ex, callCategory);
					return;
				}

				var tmessage = (header.Message as TempestMessage);
				if (tmessage == null)
				{
					OnMessageReceived (new MessageEventArgs (this, header.Message));
					#if NET_4
					if (header.IsResponse)
					{
						bool found;
						TaskCompletionSource<Message> tcs;
						lock (this.messageResponses)
						{
							found = this.messageResponses.TryGetValue (header.MessageId, out tcs);
							if (found)
								this.messageResponses.Remove (header.MessageId);
						}

						if (found)
							tcs.SetResult (header.Message);
					}
					#endif
				}
				else
					OnTempestMessageReceived (new MessageEventArgs (this, header.Message));

				currentReader = reader;
				header = null;
				this.currentHeader = null;
				messageOffset += length;
				bufferOffset = messageOffset;
				remainingData -= length;

				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("EOL: moffset={0},boffest={1},rdata={2},rpos={3}", messageOffset, bufferOffset, remainingData, reader.Position), callCategory);
			}

			if (remainingData > 0 || messageOffset + BaseHeaderLength >= buffer.Length)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, (remainingData > 0) ? String.Format ("Data remaining: {0:N0}", remainingData) : "Insufficient room for a header", callCategory);

				int knownRoomNeeded = (remainingData > BaseHeaderLength) ? remainingData : BaseHeaderLength;
				if (header != null && remainingData >= BaseHeaderLength)
					knownRoomNeeded = header.MessageLength;

				int pos = reader.Position - messageOffset;

				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format("Room needed: {0:N0} bytes", knownRoomNeeded), callCategory);
				if (messageOffset + knownRoomNeeded <= buffer.Length)
				{
					// bufferOffset is only moved on complete headers, so it's still == messageOffset.
					bufferOffset = messageOffset + remainingData;
					//reader.Position = pos;

					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Exiting (sufficient room; boffest={0},rpos={1})", bufferOffset, pos), callCategory);
					return;
				}

				byte[] destinationBuffer = buffer;
				if (knownRoomNeeded > buffer.Length)
				{
					destinationBuffer = new byte[header.MessageLength];
					reader = new BufferValueReader (destinationBuffer);
				}

				Buffer.BlockCopy (buffer, messageOffset, destinationBuffer, 0, remainingData);
				reader.Position = pos;
				messageOffset = 0;
				bufferOffset = remainingData;
				buffer = destinationBuffer;

				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Exiting (moved message to front, moffset={1},boffset={2},rpos={0})", reader.Position, messageOffset, bufferOffset), callCategory);
			}
			else
				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}

		protected void ReliableReceiveCompleted (object sender, SocketAsyncEventArgs e)
		{
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{2}:{4} {3}:ReliableReceiveCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, this.connectionId);
			#endif

			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			bool receivedAsync;
			do
			{
				int p;
				if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
				{
					Disconnect (true); // This is right, don't mess with it anymore.
					p = Interlocked.Decrement (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (error)", callCategory);
					return;
				}

				#if !SILVERLIGHT
				this.lastActivity = Stopwatch.GetTimestamp();
				#else
				this.lastActivity = DateTime.Now.Ticks;
				#endif

				Interlocked.Add (ref this.bytesReceived, e.BytesTransferred);

				p = Interlocked.Decrement (ref this.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);

				this.rmessageLoaded += e.BytesTransferred;
				
				int bufferOffset = e.Offset;
				BufferMessages (ref this.rmessageBuffer, ref bufferOffset, ref this.rmessageOffset, ref this.rmessageLoaded, ref this.rreader);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Exited BufferMessages with new values: {0},{1},{2},{3},{4}", this.rmessageBuffer.Length, bufferOffset, this.rmessageOffset, this.rmessageLoaded, this.rreader.Position), callCategory);
				e.SetBuffer (this.rmessageBuffer, bufferOffset, this.rmessageBuffer.Length - bufferOffset);

				p = Interlocked.Increment (ref this.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), callCategory);

				lock (this.stateSync)
				{
					if (!IsConnected)
					{
						p = Interlocked.Decrement (ref this.pendingAsync);
						Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);

						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (not connected)", callCategory);
						return;
					}

					receivedAsync = this.reliableSocket.ReceiveAsync (e);
				}
			} while (!receivedAsync);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}

		protected long lastSent;
		protected int pingsOut = 0;

		protected virtual void OnTempestMessageReceived (MessageEventArgs e)
		{
			switch (e.Message.MessageType)
			{
				case (ushort)TempestMessageType.Ping:
					Send (new PongMessage());
					
					#if !SILVERLIGHT
					this.lastSent = Stopwatch.GetTimestamp();
					#else
					this.lastSent = DateTime.Now.Ticks;
					#endif
					break;

				case (ushort)TempestMessageType.Pong:
					#if !SILVERLIGHT
					long timestamp = Stopwatch.GetTimestamp();
					#else
					long timestamp = DateTime.Now.Ticks;
					#endif
					
					// BUG: Doesn't really track response times, last sent to last received could be seen as really short with high delay
					ResponseTime = (int)Math.Round (TimeSpan.FromTicks (timestamp - this.lastSent).TotalMilliseconds, 0);
					this.pingsOut = 0;
					break;

				case (ushort)TempestMessageType.Disconnect:
					var msg = (DisconnectMessage)e.Message;
					Disconnect (true, msg.Reason, msg.CustomReason);
					break;
			}
		}

		private void Disconnect (bool now, ConnectionResult reason = ConnectionResult.FailedUnknown, string customReason = null)
		{
			int c = GetNextCallId();
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Entering {0}", new Exception().StackTrace), String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, GetType().Name, c, connectionId));

			if (this.disconnecting || this.reliableSocket == null)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Already disconnected, exiting", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));
				return;
			}

			lock (this.stateSync)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Shutting down socket", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

				if (this.disconnecting || this.reliableSocket == null)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Already disconnected, exiting", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));
					return;
				}

				this.disconnecting = true;

				if (!this.reliableSocket.Connected)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Socket not connected, finishing cleanup.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					while (this.pendingAsync > 1) // If called from *Completed, there'll be a pending.
						Thread.Sleep (0);

					// Shouldn't cleanup while we're still running messages.
					Recycle();

					this.disconnecting = false;
				}
				else if (now)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Shutting down socket.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					//#if !SILVERLIGHT
					//this.reliableSocket.Shutdown (SocketShutdown.Both);
					//this.reliableSocket.Disconnect (true);
					//#else
					this.reliableSocket.Close();
					//#endif
					Recycle();

					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Waiting for pending ({0}) async.", pendingAsync), String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					while (this.pendingAsync > 1)
						Thread.Sleep (0);

					Trace.WriteLineIf (NTrace.TraceVerbose, "Finished waiting, raising Disconnected.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					this.disconnecting = false;
				}
				else
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Disconnecting asynchronously.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					this.disconnectingReason = reason;
					this.disconnectingCustomReason = customReason;

					int p = Interlocked.Increment (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

					ThreadPool.QueueUserWorkItem (s =>
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Async DC waiting for pending ({0}) async.", pendingAsync), String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

						while (this.pendingAsync > 2) // Disconnect is pending.
							Thread.Sleep (0);

						Trace.WriteLineIf (NTrace.TraceVerbose, "Finished waiting, disconnecting async.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

						#if !SILVERLIGHT
						var args = new SocketAsyncEventArgs();// { DisconnectReuseSocket = true };
						args.Completed += OnDisconnectCompleted;
						
						if (!this.reliableSocket.DisconnectAsync (args))				
							OnDisconnectCompleted (this.reliableSocket, args);
						#else
						this.reliableSocket.Close();
						#endif
					});

					return;
				}
			}

			OnDisconnected (new DisconnectedEventArgs (this, reason, customReason));
			Trace.WriteLineIf (NTrace.TraceVerbose, "Raised Disconnected, exiting", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));
		}

		private void OnDisconnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			int c = GetNextCallId();
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));

			lock (this.stateSync)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Got state lock", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
				this.disconnecting = false;
				Recycle();
			}

			Trace.WriteLineIf (NTrace.TraceVerbose, "Raising Disconnected", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));

			OnDisconnected (new DisconnectedEventArgs (this, this.disconnectingReason, this.disconnectingCustomReason));
			int p = Interlocked.Decrement (ref this.pendingAsync);
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
		}

		protected int GetNextCallId()
		{
			#if TRACE
			return (NTrace.TraceVerbose) ? Interlocked.Increment (ref nextCallId) : 0;
			#else
			return 0;
			#endif
		}

		#if TRACE
		protected static int nextCallId = 0;
		protected static int nextConnectionId;
		#endif

		private static volatile int bufferCount = 0;

		internal static readonly TraceSwitch NTrace = new TraceSwitch ("Tempest.Networking", "NetworkConnectionProvider");
		internal static int maxMessageSize = 1048576;
		internal static int sendBufferLimit = Environment.ProcessorCount;
		private static bool autoSizeSendBufferLimit = true;

		#if NET_4
		private static readonly ConcurrentStack<SocketAsyncEventArgs> writerAsyncArgs = new ConcurrentStack<SocketAsyncEventArgs>();
		#else
		private static readonly Stack<SocketAsyncEventArgs> writerAsyncArgs = new Stack<SocketAsyncEventArgs>();
		#endif

		private static void ReliableSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			var t = (KeyValuePair<NetworkConnection, Message>) e.UserToken;

			NetworkConnection con = t.Key;

			string callCategory = null;
			#if TRACE
			int c = con.GetNextCallId();
			callCategory = String.Format ("{2}:{4} {3}:ReliableSendCompleted({0},{1})", e.BytesTransferred, e.SocketError, con.typeName, c, con.connectionId);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);
			#endif

			#if !NET_4
			lock (writerAsyncArgs)
			#endif
			writerAsyncArgs.Push (e);

			int p;
			if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				con.Disconnect (true);
				p = Interlocked.Decrement (ref con.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);
				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (error)", callCategory);
				return;
			}

			Interlocked.Add (ref con.bytesSent, e.BytesTransferred);

			if (!(t.Value is TempestMessage))
				con.OnMessageSent (new MessageEventArgs (con, t.Value));

			p = Interlocked.Decrement (ref con.pendingAsync);
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}
	}
}

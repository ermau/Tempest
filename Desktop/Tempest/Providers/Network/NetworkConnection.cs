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
using System.Text;
using Tempest.InternalProtocol;
using System.Threading;

#if NET_4
using System.Threading.Tasks;
using System.Collections.Concurrent;
#endif

namespace Tempest.Providers.Network
{
	public abstract class NetworkConnection
		: IConnection
	{
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

				//lock (this.stateSync)
				//	return (!this.disconnecting && this.reliableSocket != null && this.reliableSocket.Connected);
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

		public long BytesSent
		{
			get { return this.bytesSent; }
		}

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
			SendMessage (message);
		}

		#if NET_4
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
		#endif

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
		private int maxMessageLength = 1048576;

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

		//protected readonly object stateSync = new object();
		protected int pendingAsync = 0;
		protected bool disconnecting = false;
		protected bool formallyConnected = false;
		protected ConnectionResult disconnectingReason;
		protected string disconnectingCustomReason;

		protected readonly object messageIdSync = new object();
		protected int nextMessageId;
		protected int lastMessageId;

		protected Socket reliableSocket;

		protected byte[] rmessageBuffer = new byte[20480];
		protected BufferValueReader rreader;
		private int rmessageOffset = 0;
		private int rmessageLoaded = 0;

		private long bytesReceived;
		private long bytesSent;

		internal int NetworkId
		{
			get;
			set;
		}

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
			//lock (this.stateSync)
			//{
				NetworkId = 0;

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
			//}
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

			const int workingHeaderLength = BaseHeaderLength;// - sizeof (int); // Need to encrypt message id

			int r = ((writer.Length - workingHeaderLength) % encryptor.OutputBlockSize);
			if (r != 0)
				writer.Pad (encryptor.OutputBlockSize - r);

			byte[] payload = encryptor.TransformFinalBlock (writer.Buffer, workingHeaderLength, writer.Length - workingHeaderLength);

			writer.Length = workingHeaderLength;
			writer.InsertBytes (workingHeaderLength, iv, 0, iv.Length);
			writer.WriteBytes (payload);

			headerLength += iv.Length;
		}

		protected void DecryptMessage (MessageHeader header, ref BufferValueReader r, ref byte[] message)
		{
			int c = GetNextCallId();
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", String.Format ("{0}:{2} {1}:DecryptMessage({3},{4},{5})", this.typeName, c, connectionId, header.IV.Length, r.Position, message.Length));

			byte[] payload = r.ReadBytes();

			ICryptoTransform decryptor;
			lock (this.aes)
			{
				this.aes.IV = header.IV;
				decryptor = this.aes.CreateDecryptor();
			}

			message = decryptor.TransformFinalBlock (payload, 0, payload.Length);

			r = new BufferValueReader (message);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", String.Format ("{0}:{2} {1}:DecryptMessage({3},{4},{5})", this.typeName, c, connectionId, header.IV.Length, r.Position, message.Length));
		}

		protected virtual void SignMessage (string hashAlg, BufferValueWriter writer)
		{
			if (this.hmac == null)
				throw new InvalidOperationException();
			
			int c = GetNextCallId();
			string callCategory = String.Format ("{0}:{2} {1}:SignMessage ({3},{4})", this.typeName, c, connectionId, hashAlg, writer.Length);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			byte[] hash;
			lock (this.hmac)
				 hash = this.hmac.ComputeHash (writer.Buffer, 0, writer.Length);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Got hash:  " + GetHex (hash), callCategory);

			writer.WriteBytes (hash);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}

		protected virtual bool VerifyMessage (string hashAlg, Message message, byte[] signature, byte[] data, int moffset, int length)
		{
			int c = GetNextCallId();
			string callCateogry = String.Format ("{0}:{2} {1}:VerifyMessage({3},{4},{5},{6},{7},{8})", this.typeName, c, connectionId, hashAlg, message, signature.Length, data.Length, moffset, length);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCateogry);

			byte[] ourhash;
			lock (this.hmac)
				ourhash = this.hmac.ComputeHash (data, moffset, length);
			
			Trace.WriteLineIf (NTrace.TraceVerbose, "Their hash: " + GetHex (signature), callCateogry);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Our hash:   " + GetHex (ourhash), callCateogry);

			if (signature.Length != ourhash.Length)
				return false;

			for (int i = 0; i < signature.Length; i++)
			{
				if (signature[i] != ourhash[i])
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (false)", callCateogry);
					return false;
				}
			}

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (true)", callCateogry);
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

		protected byte[] GetBytes (Message message, out int length, byte[] buffer, bool isResponse)
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

			var types = context.TypeMap.GetNewTypes().OrderBy (kvp => kvp.Value).ToList();
			if (types.Count > 0)
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
					writer.WriteString (types[i].Key.GetSimpleName());

				Buffer.BlockCopy (BitConverter.GetBytes ((ushort)(writer.Length - BaseHeaderLength)), 0, writer.Buffer, BaseHeaderLength, sizeof (ushort));

				headerLength = writer.Length;
				writer.InsertBytes (headerLength, payload, BaseHeaderLength, payloadLen - BaseHeaderLength);
			}

			if (message.Encrypted)
			{
				for (int i = lengthOffset; i < lengthOffset + sizeof(int); ++i)
					writer.Buffer[i] = 0;

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
			if (types.Count > 0)
				len |= 1; // serialization header

			Buffer.BlockCopy (BitConverter.GetBytes (len), 0, rawMessage, lengthOffset, sizeof(int));

			return rawMessage;
		}

		private readonly object sendSync = new object();

		private SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();

		#if NET_4
		private readonly Dictionary<int, TaskCompletionSource<Message>> messageResponses = new Dictionary<int, TaskCompletionSource<Message>>();		
		private void SendMessage (Message message, bool isResponse = false, TaskCompletionSource<Message> future = null, int timeout = 0)
		#else
		private void SendMessage (Message message, bool isResponse = false)
		#endif
		{
			int c = GetNextCallId();
			string callCategory = String.Format ("{1}:{2} {4}:Send({0}:{3})", message, this.typeName, this.connectionId, message.MessageId, c);
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
						#if !SILVERLIGHT
						eargs.AcceptSocket = null;
						#endif
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

			//#if NET_4
			//SpinWait wait = new SpinWait();
			//#endif

			//Trace.WriteLineIf (NTrace.TraceVerbose, "Waiting for buffer", callCategory);

			//SocketAsyncEventArgs eargs = null;
			////while (!this.disconnecting && (eargs = Interlocked.CompareExchange (ref this.sendArgs, null, sendArgs)) == null)
			//while (!this.disconnecting && (eargs = Interlocked.Exchange (ref this.sendArgs, null)) == null)
			//{
			//    #if !NET_4
			//    Thread.Sleep (1);
			//    #else
			//    if (wait.NextSpinWillYield)
			//        Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Spun {0:N0} times, yielding", wait.Count), callCategory);

			//    wait.SpinOnce();
			//    #endif
			//}

			//if (eargs == null)
			//{
			//    Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (null buffer)", callCategory);
			//    return;
			//}

			//Trace.WriteLineIf (NTrace.TraceVerbose, "Have buffer", callCategory);

			bool sent;
			lock (this.sendSync)
			{
			if (!isResponse)
			{
				lock (this.messageIdSync)
				{
					if (this.nextMessageId == MaxMessageId)
						this.nextMessageId = 0;
					else
						message.MessageId = this.nextMessageId++;
				}
			}
			
			#if NET_4
			if (future != null)
			{
				lock (this.messageResponses)
					this.messageResponses.Add (message.MessageId, future);
			}
			#endif

			int length;
			byte[] buffer = GetBytes (message, out length, eargs.Buffer, isResponse);

			//eargs.AcceptSocket = null;
			eargs.SetBuffer (buffer, 0, length);
			eargs.UserToken = message;

			//lock (this.stateSync)
			//{
				int p = Interlocked.Increment (ref this.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), callCategory);

				if (!this.IsConnected)
				{
					Interlocked.Decrement (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);

					//if (eargs != null)
					//    Interlocked.Exchange (ref this.sendArgs, eargs);
					#if !NET_4
					lock (writerAsyncArgs)
					#endif
					writerAsyncArgs.Push (eargs);

					return;
				}

				eargs.Completed += ReliableSendCompleted;

				sent = !this.reliableSocket.SendAsync (eargs);
			//}
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
		protected bool TryGetHeader (BufferValueReader reader, int remaining, out MessageHeader header)
		{
			int c = GetNextCallId();
			string callCategory = String.Format ("{0}:{4} {1}:TryGetHeader({2},{3})", this.typeName, c, reader.Position, remaining, this.connectionId);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);			

			header = null;

			try
			{
				byte pid = reader.ReadByte();
				Protocol p;
				if (!this.protocols.TryGetValue (pid, out p))
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (Protocol " + pid + " not found)", callCategory);
					return true;
				}

				ushort type = reader.ReadUInt16();
				int mlen = reader.ReadInt32();
				bool hasTypeHeader = (mlen & 1) == 1;
				mlen >>= 1;

				if (mlen <= 0)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (length invalid)", callCategory);
					return true;
				}

				int identV = reader.ReadInt32();

				Message msg = p.Create (type);
				if (msg == null)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (Message " + type + " not found)", callCategory);
					return true;
				}

				header = new MessageHeader (p, msg)
				{
					MessageLength = mlen,
					MessageId = identV & ~ResponseFlag,
					IsResponse = (identV & ResponseFlag) == ResponseFlag
				};

				msg.MessageId = header.MessageId;

				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Have {0}:{2} ({1:N0})", msg.GetType().Name, mlen, msg.MessageId), callCategory);

				int headerLength = BaseHeaderLength;

				TypeMap map;
				if (hasTypeHeader)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Has type header, reading types", callCategory);

					if (remaining < headerLength + (sizeof(ushort) * 2))
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not buffered)", callCategory);
						return false;
					}

					ushort typeheaderLength = reader.ReadUInt16();
					headerLength += typeheaderLength;

					if (remaining < headerLength)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not buffered)", callCategory);
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

				byte[] iv = null;
				if (msg.Encrypted && this.aes != null)
				{
					int length = this.aes.IV.Length;
					iv = reader.ReadBytes (length);

					headerLength += length;
					if (remaining < headerLength)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not buffered)", callCategory);
						return false;
					}

					header.IV = iv;
				}

				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);

				header.SerializationContext = context;
				header.HeaderLength = headerLength;

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
			int c = GetNextCallId();
			string callCategory = String.Format ("{0}:{6} {1}:BufferMessages({2},{3},{4},{5})", this.typeName, c, buffer.Length, bufferOffset, messageOffset, remainingData, this.connectionId);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			this.lastReceived = DateTime.Now;

			MessageHeader header = null;

			int length = 0;
			while (remainingData >= BaseHeaderLength)
			{
				if (!TryGetHeader (reader, remainingData, out header))
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Failed to get header", callCategory);
					break;
				}

				if (header == null)
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not found)", callCategory);
					return;
				}

				length = header.MessageLength;
				if (length > maxMessageLength)
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (bad message size)", callCategory);
					return;
				}

				if (!header.IsResponse)
				{
					if (header.MessageId < this.lastMessageId)
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
					Trace.WriteLineIf (NTrace.TraceVerbose, "Message not fully received", callCategory);
					bufferOffset += remainingData;
					break;
				}

				if (!IsConnected)
					return;

				try
				{
					byte[] message = buffer;
					BufferValueReader r = reader;

					if (header.Message.Encrypted)
						DecryptMessage (header, ref r, ref message);

					header.Message.ReadPayload (header.SerializationContext, r);

					if (!header.Message.Encrypted && header.Message.Authenticated)
					{
						// Zero out length for message signing comparison
						for (int i = 3 + messageOffset; i < 7 + messageOffset; ++i)
							buffer[i] = 0;

						int payloadLength = reader.Position;
						byte[] signature = reader.ReadBytes(); // Need the original reader here, sig is after payload
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

				messageOffset += length;
				bufferOffset = messageOffset;
				remainingData -= length;
			}

			if (remainingData > 0 || messageOffset + BaseHeaderLength >= buffer.Length)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, (remainingData > 0) ? String.Format ("Data remaining: {0:N0}", remainingData) : "Insufficient room for a header", callCategory);

				int knownRoomNeeded = (remainingData > BaseHeaderLength) ? remainingData : BaseHeaderLength;
				if (header != null && remainingData >= BaseHeaderLength)
					knownRoomNeeded = header.MessageLength;

				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format("Room needed: {0:N0} bytes", knownRoomNeeded), callCategory);
				if (messageOffset + knownRoomNeeded <= buffer.Length)
				{
					// bufferOffset is only moved on complete headers, so it's still == messageOffset.
					bufferOffset = messageOffset + remainingData;
					reader.Position = messageOffset;

					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (sufficient room)", callCategory);
					return;
				}

				byte[] destinationBuffer = buffer;
				if (knownRoomNeeded > buffer.Length)
				{
					destinationBuffer = new byte[header.MessageLength];
					reader = new BufferValueReader (destinationBuffer);
				}

				Buffer.BlockCopy (buffer, messageOffset, destinationBuffer, 0, remainingData);
				messageOffset = 0;
				reader.Position = 0;
				bufferOffset = remainingData;
				buffer = destinationBuffer;
			}

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}

		protected void ReliableReceiveCompleted (object sender, SocketAsyncEventArgs e)
		{
			int c = GetNextCallId();
			string callCategory = String.Format ("{2}:{4} {3}:ReliableReceiveCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, this.connectionId);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			bool async;
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

				Interlocked.Add (ref this.bytesReceived, e.BytesTransferred);

				p = Interlocked.Decrement (ref this.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);

				this.rmessageLoaded += e.BytesTransferred;
				//lock (this.stateSync)
				//{
					int bufferOffset = e.Offset;
					BufferMessages (ref this.rmessageBuffer, ref bufferOffset, ref this.rmessageOffset, ref this.rmessageLoaded, ref this.rreader);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Exited BufferMessages with new values: {0},{1},{2},{3},{4}", this.rmessageBuffer.Length, bufferOffset, this.rmessageOffset, this.rmessageLoaded, this.rreader.Position), callCategory);
					e.SetBuffer (this.rmessageBuffer, bufferOffset, this.rmessageBuffer.Length - bufferOffset);

					p = Interlocked.Increment (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), callCategory);

					if (!IsConnected)
					{
						p = Interlocked.Decrement (ref this.pendingAsync);
						Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);

						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (not connected)", callCategory);
						return;
					}

					async = this.reliableSocket.ReceiveAsync (e);
				//}
			} while (!async);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
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

			//lock (this.stateSync)
			//{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Got state lock.", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));

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
			//}

			OnDisconnected (new DisconnectedEventArgs (this, reason, customReason));
			Trace.WriteLineIf (NTrace.TraceVerbose, "Raised Disconnected, exiting", String.Format ("{2}:{4} {3}:Disconnect({0},{1})", now, reason, this.typeName, c, connectionId));
		}

		private void ReliableSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			int c = GetNextCallId();
			string callCategory = String.Format ("{2}:{4} {3}:ReliableSendCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			e.Completed -= ReliableSendCompleted;

			var message = (Message)e.UserToken;

			#if !NET_4
			lock (writerAsyncArgs)
			#endif
			writerAsyncArgs.Push (e);

			//Interlocked.Exchange (ref this.sendArgs, e);
			//Trace.WriteLineIf (NTrace.TraceVerbose, "Buffer returned", callCategory);

			int p;
			if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
			{
				Disconnect (true);
				p = Interlocked.Decrement (ref this.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);
				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (error)", callCategory);
				return;
			}

			Interlocked.Add (ref this.bytesSent, e.BytesTransferred);

			if (!(message is TempestMessage))
				OnMessageSent (new MessageEventArgs (this, message));

			p = Interlocked.Decrement (ref this.pendingAsync);
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}

		private void OnDisconnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			int c = GetNextCallId();
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));

			//lock (this.stateSync)
			//{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Got state lock", String.Format ("{2}:{4} {3}:OnDisconnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, c, connectionId));
				this.disconnecting = false;
				Recycle();
			//}

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

		// TODO: Better buffer limit
		private static readonly int BufferLimit = Environment.ProcessorCount * 10;
		private static volatile int bufferCount = 0;

		internal static readonly TraceSwitch NTrace = new TraceSwitch ("Tempest.Networking", "NetworkConnectionProvider");

		#if NET_4
		private static readonly ConcurrentStack<SocketAsyncEventArgs> writerAsyncArgs = new ConcurrentStack<SocketAsyncEventArgs>();
		#else
		private static readonly Stack<SocketAsyncEventArgs> writerAsyncArgs = new Stack<SocketAsyncEventArgs>();
		#endif
	}
}

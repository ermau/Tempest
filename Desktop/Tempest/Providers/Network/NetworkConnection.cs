//
// NetworkConnection.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010-2013 Eric Maupin
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
using System.Net.Sockets;
using Tempest.InternalProtocol;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Tempest.Providers.Network
{
	public abstract class NetworkConnection
		: IConnection
	{
		static NetworkConnection()
		{
			MaxMessageSize = 1048576;
		}

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
			get { return autoSizeFactor; }
			set { autoSizeFactor = value; }
		}

		/// <summary>
		/// Gets or sets the maximum message size.
		/// </summary>
		/// <seealso cref="SendBufferLimit" />
		/// <remarks>
		/// <para>
		/// You should consider <see cref="MaxMessageSize"/> * <c>maxConnections</c> (<see cref="NetworkConnectionProvider(Tempest.Protocol,Tempest.Target,int)"/>) for receive memory usage.
		/// </para>
		/// </remarks>
		public static int MaxMessageSize
		{
			get;
			set;
		}

		protected NetworkConnection (IEnumerable<Protocol> protocols, RSAAsymmetricKey authKey, bool generateKey)
		{
			if (protocols == null)
				throw new ArgumentNullException ("protocols");

			this.authenticationKey = authKey;
			this.requiresHandshake = protocols.Any (p => p.RequiresHandshake);
			if (this.requiresHandshake)
			{
				ThreadPool.QueueUserWorkItem (s =>
				{
					this.pkAuthentication = new RSACrypto();

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

		public int ConnectionId
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
			protected set;
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

		public Target RemoteTarget
		{
			get;
			protected set;
		}

		public abstract RSAAsymmetricKey RemoteKey
		{
			get;
		}

		public RSAAsymmetricKey LocalKey
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

		public Task<bool> SendAsync (Message message)
		{
			return SendMessage (message, isResponse: false);
		}

		public Task<Message> SendFor (Message message, int timeout = 0)
		{
			Task<Message> responseTask;
			SendMessage (message, false, true, timeout, out responseTask);
			return responseTask;
		}

		public Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0)
			where TResponse : Message
		{
			return SendFor (message, timeout).ContinueWith (t => (TResponse)t.Result, TaskScheduler.Default);
		}

		public Task<bool> SendResponseAsync (Message originalMessage, Message response)
		{
			if (originalMessage == null)
				throw new ArgumentNullException ("originalMessage");
			if (originalMessage.Header.IsResponse)
				throw new ArgumentException ("originalMessage can't be a response", "originalMessage");
			if (response == null)
				throw new ArgumentNullException ("response");

			response.Header = new MessageHeader {
				IsResponse = true,
				ResponseMessageId = originalMessage.Header.MessageId
			};

			return SendMessage (response, isResponse: true);
		}

		public Task DisconnectAsync()
		{
			return DisconnectAsync (ConnectionResult.FailedUnknown);
		}

		public Task DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			return Disconnect (reason, customReason);
		}

		public virtual void Dispose()
		{
			if (this.disposed)
				return;

			this.disposed = true;
			Disconnect();

			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Waiting for {0} pending asyncs", this.pendingAsync), String.Format ("{0}:{1} Dispose()", this.typeName, connectionId));

			while (this.pendingAsync > 0)
				Thread.Sleep (1);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Disposed", String.Format ("{0}:{1} Dispose()", this.typeName, connectionId));
		}

		protected bool authReady;
		protected bool disposed;

		private const int MaxMessageId = 8388608;

		protected int connectionId;
		protected readonly string typeName;

		protected Dictionary<byte, Protocol> protocols;
		protected bool requiresHandshake;

		internal RSACrypto pkAuthentication;
		protected RSAAsymmetricKey authenticationKey;
		internal RSAAsymmetricKey publicAuthenticationKey;

		protected readonly object stateSync = new object();
		protected int pendingAsync;
		protected bool disconnecting;
		protected bool formallyConnected;
		protected ConnectionResult disconnectingReason;
		protected string disconnectingCustomReason;

		protected int nextMessageId;
		protected int lastMessageId;

		protected Socket reliableSocket;

		protected MessageHeader currentHeader;
		protected byte[] rmessageBuffer = new byte[20480];
		protected BufferValueReader rreader;
		private int rmessageOffset;
		private int rmessageLoaded;

		protected long lastActivity;
		private long bytesReceived;
		private long bytesSent;

		internal MessageSerializer serializer;

		private readonly object sendSync = new object();

		#if !SILVERLIGHT
		private readonly Lazy<MessageResponseManager> responses =
			new Lazy<MessageResponseManager> (() => new MessageResponseManager());

		protected MessageResponseManager Responses
		{
			get { return this.responses.Value; }
		}
		#else
		private readonly MessageResponseManager responses = new MessageResponseManager();

		protected MessageResponseManager Responses
		{
			get { return this.responses; }
		}
		#endif

		private int pingFrequency;
		protected virtual int PingFrequency
		{
			get { return this.pingFrequency; }
		}
		
		private DateTime lastPing;
		protected void Ping()
		{
			var now = DateTime.Now;
			var last = (now - this.lastPing);

			if (this.pingsOut >= 2)
			{
			//    Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (" + this.pingsOut + " pings out)", callCategory);
			    Disconnect(); // Connection timed out
			    return;
			}

			if (last.TotalMilliseconds >= PingFrequency) {
				this.lastPing = now;
				Interlocked.Increment (ref this.pingsOut);

				long sent = Stopwatch.GetTimestamp();
				SendFor (new PingMessage { Interval = this.pingFrequency }).ContinueWith (t => {
					long responseTime = Stopwatch.GetTimestamp() - sent;
					ResponseTime = (int)TimeSpan.FromTicks (responseTime).TotalMilliseconds;
				}, TaskScheduler.Current);
			}

			#if !SILVERLIGHT
			if (this.responses.IsValueCreated)
				this.responses.Value.CheckTimeouts();
			#else
			Responses.CheckTimeouts();
			#endif
		}

		protected virtual void Recycle()
		{
			lock (this.stateSync)
			{
				ConnectionId = 0;

				if (this.reliableSocket != null)
					this.reliableSocket.Dispose();

				this.reliableSocket = null;
				this.rmessageOffset = 0;
				this.rmessageLoaded = 0;
				this.bytesReceived = 0;
				this.bytesSent = 0;
				this.lastMessageId = 0;
				this.nextMessageId = 0;

				#if !SILVERLIGHT
				if (this.responses.IsValueCreated)
					this.responses.Value.Clear();
				#else
				Responses.Clear();
				#endif

				this.serializer = null;
			}
		}

		protected virtual void OnMessageReceived (MessageEventArgs e)
		{
			var mr = MessageReceived;
			if (mr != null)
				mr (this, e);
		}

		protected virtual void OnDisconnected (DisconnectedEventArgs e)
		{
			var dc = Disconnected;
			if (dc != null)
				dc (this, e);
		}

		private Task<bool> SendMessage (Message message, bool isResponse)
		{
			Task<Message> responseTask;
			return SendMessage (message, isResponse, false, 0, out responseTask);
		}

		private Task<bool> SendMessage (Message message, bool isResponse, bool requestingResponse, int responseTimeout, out Task<Message> responseTask)
		{
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{1}:{2} {3}:Send({0})", message, this.typeName, this.connectionId, c);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			responseTask = null;

			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool> (message);

			if (message == null)
				throw new ArgumentNullException ("message");
			if (!IsConnected)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (not connected)", callCategory);
				tcs.SetResult (false);

				if (requestingResponse) {
					var responseTcs = new TaskCompletionSource<Message>();
					responseTcs.SetCanceled();
					responseTask = responseTcs.Task;
				}

				return tcs.Task;
			}

			SocketAsyncEventArgs eargs = null;
			#if NET_4
			if (!writerAsyncArgs.TryPop (out eargs))
			{
				while (eargs == null)
				{
					int count = bufferCount;
					if (count == sendBufferLimit)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Waiting for writer args", callCategory);

						SpinWait wait = new SpinWait();
						while (!writerAsyncArgs.TryPop (out eargs))
							wait.SpinOnce();
					}
					else if (count == Interlocked.CompareExchange (ref bufferCount, count + 1, count))
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Creating new writer args", callCategory);

						eargs = new SocketAsyncEventArgs();
						eargs.SetBuffer (new byte[1024], 0, 1024);
						eargs.Completed += ReliableSendCompleted;
					}
				}
			}
			#else
			while (eargs == null)
			{
				lock (writerAsyncArgs)
				{
					if (writerAsyncArgs.Count != 0)
						eargs = writerAsyncArgs.Pop();
					else if (bufferCount != sendBufferLimit)
					{
						bufferCount++;
						eargs = new SocketAsyncEventArgs();
						eargs.SetBuffer (new byte[1024], 0, 1024);
						eargs.Completed += ReliableSendCompleted;
					}
				}
			}
			#endif
			
			#if !SILVERLIGHT
			eargs.AcceptSocket = null;
			#endif

			Trace.WriteLineIf (NTrace.TraceVerbose, "Have writer args", callCategory);

			bool sent;
			lock (this.sendSync) {
				if (message.Header == null)
					message.Header = new MessageHeader();

				message.Header.MessageId = MessageSerializer.GetNextMessageId (ref this.nextMessageId);

				if (requestingResponse)
					responseTask = Responses.SendFor (message, tcs.Task, responseTimeout);

				MessageSerializer slzr = this.serializer;
				if (slzr == null)
				{
					int sp = Interlocked.Decrement (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", sp), callCategory);
					
					#if !NET_4
					lock (writerAsyncArgs)
					#endif
					writerAsyncArgs.Push (eargs);
					
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (serializer is null, probably disconnecting)", callCategory);
					tcs.SetResult (false);
					return tcs.Task;
				}

				int length;
				byte[] buffer = slzr.GetBytes (message, out length, eargs.Buffer);

				eargs.SetBuffer (buffer, 0, length);
				eargs.UserToken = new KeyValuePair<NetworkConnection, TaskCompletionSource<bool>> (this, tcs);

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

					tcs.SetResult (false);
					return tcs.Task;
				}

				Trace.WriteLineIf (NTrace.TraceVerbose, "Sending", callCategory);
				sent = !this.reliableSocket.SendAsync (eargs);
			}

			if (sent)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Send completed synchronously", callCategory);
				ReliableSendCompleted (this.reliableSocket, eargs);
			}

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
			return tcs.Task;
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
					Disconnect(); // This is right, don't mess with it anymore.
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

				MessageSerializer slzr = this.serializer;
				if (slzr == null)
				{
					p = Interlocked.Decrement (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);

					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (no serializer, probably disconnecting)", callCategory);
					return;
				}

				List<Message> messages = slzr.BufferMessages (ref this.rmessageBuffer, ref bufferOffset, ref this.rmessageOffset, ref this.rmessageLoaded, ref this.currentHeader, ref this.rreader, CheckMessageId);
				if (messages != null)
				{
					foreach (Message message in messages)
					{
						var args = new MessageEventArgs (this, message);
						
						if (message is TempestMessage)
							OnTempestMessageReceived (args);
						else
						{
							OnMessageReceived (args);

							if (message.Header.IsResponse)
								Responses.Receive (message);
						}
					}
				}

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

		private bool CheckMessageId (MessageHeader header)
		{
			if (!header.IsResponse)
			{
				if (header.MessageId == MaxMessageId)
					this.lastMessageId = -1;
				else if (header.MessageId < this.lastMessageId)
				{
					Disconnect();
					return false;
				}

				this.lastMessageId = (header.MessageId != MaxMessageId) ? header.MessageId : 0; // BUG: Skipped messages will break this
			}
			else if (header.ResponseMessageId > this.nextMessageId)
			{
				Disconnect();
				return false;
			}

			return true;
		}

		protected int pingsOut = 0;

		protected virtual void OnTempestMessageReceived (MessageEventArgs e)
		{
			switch (e.Message.MessageType)
			{
				case (ushort)TempestMessageType.Ping:
					var ping = (PingMessage)e.Message;
					this.pingFrequency = ping.Interval;
					SendResponseAsync (e.Message, new PongMessage());
					break;

				case (ushort)TempestMessageType.Pong:
					this.pingsOut = 0;
					break;

				case (ushort)TempestMessageType.Disconnect:
					var msg = (DisconnectMessage)e.Message;
					Disconnect (msg.Reason, msg.CustomReason, notify: false);
					break;
			}
		}

		private Task Disconnect (ConnectionResult reason = ConnectionResult.FailedUnknown, string customReason = null, bool notify = true)
		{
			string category = null;
			#if TRACE
			int c = GetNextCallId();
			category = String.Format ("{1}:{3} {2}:Disconnect({0})", reason, GetType().Name, c, connectionId);
			#endif

			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Entering {0}", new Exception().StackTrace), category);

			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

			if (this.disconnecting || this.reliableSocket == null)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Already disconnected, exiting", category);
				tcs.SetResult (true);
				return tcs.Task;
			}

			if (notify)
			{
				Task<bool> ntask = SendAsync (new DisconnectMessage { Reason = reason, CustomReason = customReason });
				ntask.Wait();
			}

			lock (this.stateSync)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Shutting down socket", category);

				if (this.disconnecting || this.reliableSocket == null)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Already disconnected, exiting", category);
					tcs.SetResult (true);
					return tcs.Task;
				}

				this.disconnecting = true;

				if (!this.reliableSocket.Connected)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Socket not connected, finishing cleanup.", category);

					while (this.pendingAsync > 1) // If called from *Completed, there'll be a pending.
						Thread.Sleep (0);

					// Shouldn't cleanup while we're still running messages.
					Recycle();

					this.disconnecting = false;

					tcs.SetResult (true);
				}
				else
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Disconnecting asynchronously.", category);

					this.disconnectingReason = reason;
					this.disconnectingCustomReason = customReason;

					int p = Interlocked.Increment (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), category);

					ThreadPool.QueueUserWorkItem (s =>
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Async DC waiting for pending ({0}) async.", pendingAsync), category);

						while (this.pendingAsync > 2) // Disconnect is pending.
							Thread.Sleep (0);

						Trace.WriteLineIf (NTrace.TraceVerbose, "Finished waiting, disconnecting async.", category);

						#if !SILVERLIGHT
						var args = new SocketAsyncEventArgs();// { DisconnectReuseSocket = true };
						args.Completed += OnDisconnectCompleted;
						
						if (!this.reliableSocket.DisconnectAsync (args))				
							OnDisconnectCompleted (this.reliableSocket, args);
						#else
						this.reliableSocket.Close();
						#endif

						tcs.SetResult (true);
					});

					return tcs.Task;
				}
			}

			OnDisconnected (new DisconnectedEventArgs (this, reason, customReason));
			Trace.WriteLineIf (NTrace.TraceVerbose, "Raised Disconnected, exiting", category);

			return tcs.Task;
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

		internal static int GetNextCallId()
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
		internal static int sendBufferLimit = Environment.ProcessorCount;
		private static bool autoSizeSendBufferLimit = true;
		private static int autoSizeFactor = 1;

		#if NET_4
		private static readonly ConcurrentStack<SocketAsyncEventArgs> writerAsyncArgs = new ConcurrentStack<SocketAsyncEventArgs>();
		#else
		private static readonly Stack<SocketAsyncEventArgs> writerAsyncArgs = new Stack<SocketAsyncEventArgs>();
		#endif

		private static void ReliableSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			var t = (KeyValuePair<NetworkConnection, TaskCompletionSource<bool>>) e.UserToken;

			NetworkConnection con = t.Key;

			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
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
				con.Disconnect();
				p = Interlocked.Decrement (ref con.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);
				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (error)", callCategory);
				
				t.Value.TrySetResult (false);
				return;
			}

			Interlocked.Add (ref con.bytesSent, e.BytesTransferred);
			
			p = Interlocked.Decrement (ref con.pendingAsync);
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), callCategory);
			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);

			t.Value.TrySetResult (true);
		}
	}
}

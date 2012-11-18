//
// NetworkClientConnection.cs
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
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public sealed class NetworkClientConnection
		: NetworkConnection, IClientConnection
	{
		public NetworkClientConnection (Protocol protocol)
			: this (new [] { protocol })
		{
		}

		public NetworkClientConnection (IEnumerable<Protocol> protocols)
			: this (protocols, () => new RSACrypto())
		{
		}

		public NetworkClientConnection (Protocol protocol, Func<IPublicKeyCrypto> publicKeyCryptoFactory)
			: this (new [] { protocol }, publicKeyCryptoFactory)
		{
		}

		public NetworkClientConnection (IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> publicKeyCryptoFactory)
			: base (protocols, publicKeyCryptoFactory, null, true)
		{
			Interlocked.Add (ref sendBufferLimit, AutoSizeFactor);
		}

		public NetworkClientConnection (Protocol protocol, Func<IPublicKeyCrypto> publicKeyCryptoFactory, IAsymmetricKey authKey)
			: base (new [] { protocol }, publicKeyCryptoFactory, authKey, false)
		{
			Interlocked.Add (ref sendBufferLimit, AutoSizeFactor);

			if (authKey == null)
				throw new ArgumentNullException ("authKey");
		}

		public NetworkClientConnection (IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> publicKeyCryptoFactory, IAsymmetricKey authKey)
			: base (protocols, publicKeyCryptoFactory, authKey, false)
		{
			Interlocked.Add (ref sendBufferLimit, AutoSizeFactor);

			if (authKey == null)
				throw new ArgumentNullException ("authKey");
		}

		public event EventHandler<ClientConnectionEventArgs> Connected;
		public event EventHandler<ClientConnectionEventArgs> ConnectionFailed;

		public override IAsymmetricKey RemoteKey
		{
			get { return this.serverAuthenticationKey; }
		}

		public Task<ClientConnectionResult> ConnectAsync (EndPoint endpoint, MessageTypes messageTypes)
		{
			int c = GetNextCallId();
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", String.Format ("{2}:{3} {4}:ConnectAsync({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));

			if (endpoint == null)
				throw new ArgumentNullException ("endpoint");
			if ((messageTypes & MessageTypes.Unreliable) == MessageTypes.Unreliable)
				throw new NotSupportedException();

			var ntcs = new TaskCompletionSource<ClientConnectionResult>();
			
			ThreadPool.QueueUserWorkItem (s =>
			{
				SocketAsyncEventArgs args;
				bool connected;

				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Waiting for pending ({0}) async..", this.pendingAsync), String.Format ("{2}:{3} {4}:ConnectAsync({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));

				while (this.pendingAsync > 0 || Interlocked.CompareExchange (ref this.connectCompletion, ntcs, null) != null)
					Thread.Sleep (0);

				lock (this.stateSync)
				{
					if (IsConnected)
						throw new InvalidOperationException ("Already connected");

					RemoteEndPoint = endpoint;

					args = new SocketAsyncEventArgs();
					args.RemoteEndPoint = endpoint;
					args.Completed += ConnectCompleted;

					this.reliableSocket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

					int p = Interlocked.Increment (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), String.Format ("{2}:{3} {4}:ConnectAsync({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));
					connected = !this.reliableSocket.ConnectAsync (args);
				}

				if (connected)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Connected synchronously", String.Format ("{2}:{3} {4}:ConnectAsync({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));
					ConnectCompleted (this.reliableSocket, args);
				}
				else
					Trace.WriteLineIf (NTrace.TraceVerbose, "Connecting asynchronously", String.Format ("{2}:{3} {4}:ConnectAsync({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));
			});

			return ntcs.Task;
		}
		
		private int pingFrequency;
		private Tempest.Timer activityTimer;

		internal IPublicKeyCrypto serverAuthentication;
		internal IAsymmetricKey serverAuthenticationKey;

		private IPublicKeyCrypto serverEncryption;
		private IAsymmetricKey serverEncryptionKey;

		private TaskCompletionSource<ClientConnectionResult> connectCompletion;
		
		protected override void Recycle()
		{
			Interlocked.Add (ref sendBufferLimit, AutoSizeFactor * -1);

			this.serverEncryption = null;
			this.serverEncryptionKey = null;
			this.serverAuthenticationKey = null;

			base.Recycle();
		}

		private ConnectionResult GetConnectFromError (SocketError error)
		{
			switch (error)
			{
				case SocketError.Success:
					return ConnectionResult.Success;
				case SocketError.TimedOut:
					return ConnectionResult.TimedOut;
				default:
					return ConnectionResult.ConnectionFailed;
			}
		}

		private void ConnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			int c = GetNextCallId();
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));

			this.serializer = new ClientMessageSerializer (this, Protocols);

			int p;

			if (e.SocketError != SocketError.Success)
			{
				p = Interlocked.Decrement (ref this.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));
				Disconnect (ConnectionResult.ConnectionFailed);
				OnConnectionFailed (new ClientConnectionEventArgs (this));

				var tcs = Interlocked.Exchange (ref this.connectCompletion, null);
				if (tcs != null)
				{
					ConnectionResult result = GetConnectFromError (e.SocketError);
					tcs.SetResult (new ClientConnectionResult (result, null));
				}

				return;
			}

			e.Completed -= ConnectCompleted;
			e.Completed += ReliableReceiveCompleted;
			e.SetBuffer (this.rmessageBuffer, 0, this.rmessageBuffer.Length);
			this.rreader = new BufferValueReader (this.rmessageBuffer);

			bool received;
			lock (this.stateSync)
			{
				if (!IsConnected)
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Already disconnected", String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));
					p = Interlocked.Decrement (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));
					return;
				}

				//p = Interlocked.Increment (ref this.pendingAsync);
				//Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));

				received = !this.reliableSocket.ReceiveAsync (e);
			}

			//p = Interlocked.Decrement (ref this.pendingAsync);
			//Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));

			if (received)
				ReliableReceiveCompleted (this.reliableSocket, e);

			var connectMsg = new ConnectMessage { Protocols = this.protocols.Values };
			if (this.requiresHandshake)
			{
				while (!this.authReady)
					Thread.Sleep (0);

				connectMsg.SignatureHashAlgorithms = this.pkAuthentication.SupportedHashAlgs;
			}

			Send (connectMsg);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));
		}

		protected override void OnTempestMessageReceived (MessageEventArgs e)
		{
			switch (e.Message.MessageType)
			{
				case (ushort)TempestMessageType.Ping:
					var ping = (PingMessage)e.Message;
					if (this.pingFrequency == 0 || this.activityTimer == null)
					{
						if (this.activityTimer != null)
							this.activityTimer.Dispose();

						if (ping.Interval != 0)
						{
							this.activityTimer = new Tempest.Timer (ping.Interval);
							this.activityTimer.TimesUp += ActivityCallback;
							this.activityTimer.Start();
						}
					}
					else if (ping.Interval != this.pingFrequency)
						this.activityTimer.Interval = ping.Interval;
					
					this.pingFrequency = ((PingMessage)e.Message).Interval;
					break;

				case (ushort)TempestMessageType.AcknowledgeConnect:
				    var msg = (AcknowledgeConnectMessage)e.Message;

				    this.protocols = this.protocols.Values.Intersect (msg.EnabledProtocols).ToDictionary (pr => pr.id);
					ConnectionId = msg.ConnectionId;

					this.serverEncryption = this.publicKeyCryptoFactory();
					this.serverEncryption.ImportKey (msg.PublicEncryptionKey);
					this.serverEncryptionKey = msg.PublicEncryptionKey;

					var encryption = new AesManaged { KeySize = 256 };
					encryption.GenerateKey();
					
					BufferValueWriter authKeyWriter = new BufferValueWriter (new byte[1600]);
					this.publicAuthenticationKey.Serialize (authKeyWriter, this.serverEncryption);

					Send (new FinalConnectMessage
					{
						AESKey = this.serverEncryption.Encrypt (encryption.Key),
						PublicAuthenticationKeyType = this.publicAuthenticationKey.GetType(),
						PublicAuthenticationKey = authKeyWriter.ToArray()
					});

					this.serializer.AES = encryption;
					this.serializer.HMAC = new HMACSHA256 (encryption.Key);
				    break;

				case (ushort)TempestMessageType.Connected:
					var connected = (ConnectedMessage) e.Message;
					ConnectionId = connected.ConnectionId;

					OnConnected (new ClientConnectionEventArgs (this));

					var tcs = Interlocked.Exchange (ref this.connectCompletion, null);
					if (tcs != null)
						tcs.SetResult (new ClientConnectionResult (ConnectionResult.Success, this.serverAuthenticationKey));

					break;
			}

			base.OnTempestMessageReceived(e);
		}

		protected override void OnDisconnected (DisconnectedEventArgs e)
		{
			var tcs = Interlocked.Exchange (ref this.connectCompletion, null);
			if (tcs != null)
				tcs.TrySetResult (new ClientConnectionResult (e.Result, null));

			if (this.activityTimer != null)
			{
				this.activityTimer.Dispose();
				this.activityTimer = null;
			}

			base.OnDisconnected(e);
		}

		private void ActivityCallback (object sender, EventArgs e)
		{
			#if !SILVERLIGHT
			long now = Stopwatch.GetTimestamp();
			#else
			long now = DateTime.Now.Ticks;
			#endif

			//if ((now - this.lastActivity) > (this.pingFrequency * 10000) * 2)
			//    Disconnect (ConnectionResult.TimedOut);
		}

		private void OnConnected (ClientConnectionEventArgs e)
		{
			var connected = Connected;
			if (connected != null)
				connected (this, e);
		}

		private void OnConnectionFailed (ClientConnectionEventArgs e)
		{
			var handler = this.ConnectionFailed;
			if (handler != null)
				handler (this, e);
		}
	}
}
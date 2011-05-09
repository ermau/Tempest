//
// NetworkClientConnection.cs
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
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public sealed class NetworkClientConnection
		: NetworkConnection, IClientConnection
	{
		#if !SILVERLIGHT
		public NetworkClientConnection (Protocol protocol)
			: this (new [] { protocol })
		{
		}

		public NetworkClientConnection (IEnumerable<Protocol> protocols)
			: this (protocols, () => new RSACrypto())
		{
		}
		#endif

		public NetworkClientConnection (Protocol protocol, Func<IPublicKeyCrypto> publicKeyCryptoFactory)
			: this (new [] { protocol }, publicKeyCryptoFactory)
		{
		}

		public NetworkClientConnection (IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> publicKeyCryptoFactory)
			: base (protocols, publicKeyCryptoFactory, null)
		{
		}

		public NetworkClientConnection (IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> publicKeyCryptoFactory, IAsymmetricKey authKey)
			: base (protocols, publicKeyCryptoFactory, authKey)
		{
			if (authKey == null)
				throw new ArgumentNullException ("authKey");
		}

		public event EventHandler<ClientConnectionEventArgs> Connected;
		public event EventHandler<ClientConnectionEventArgs> ConnectionFailed;

		public void Connect (EndPoint endpoint, MessageTypes messageTypes)
		{
			#if TRACE
			int c = Interlocked.Increment (ref nextCallId);
			#endif

			Trace.WriteLine ("Entering", String.Format ("{2}:{3} {4}:Connect({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));

			if (endpoint == null)
				throw new ArgumentNullException ("endpoint");
			if ((messageTypes & MessageTypes.Unreliable) == MessageTypes.Unreliable)
				throw new NotSupportedException();

			SocketAsyncEventArgs args;
			bool connected;

			Trace.WriteLine (String.Format ("Waiting for pending ({0}) async..", this.pendingAsync), String.Format ("{2}:{3} {4}:Connect({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));

			while (this.pendingAsync > 0)
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
				Trace.WriteLine (String.Format ("Increment pending: {0}", p), String.Format ("{2}:{3} {4}:Connect({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));
				connected = !this.reliableSocket.ConnectAsync (args);
			}

			if (connected)
			{
				Trace.WriteLine ("Connected synchronously", String.Format ("{2}:{3} {4}:Connect({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));
				ConnectCompleted (this.reliableSocket, args);
			}
			else
				Trace.WriteLine ("Connecting asynchronously", String.Format ("{2}:{3} {4}:Connect({0},{1})", endpoint, messageTypes, this.typeName, connectionId, c));
		}
		
		private int pingFrequency;
		private Timer activityTimer;

		private IPublicKeyCrypto serverAuthentication;
		private IAsymmetricKey serverAuthenticationKey;

		private IPublicKeyCrypto serverEncryption;
		private IAsymmetricKey serverEncryptionKey;
		
		protected override void Recycle()
		{
			this.serverEncryption = null;
			this.serverEncryptionKey = null;
			this.serverAuthenticationKey = null;

			base.Recycle();
		}

		private void ConnectCompleted (object sender, SocketAsyncEventArgs e)
		{
			#if TRACE
			int c = Interlocked.Increment (ref nextCallId);
			#endif

			Trace.WriteLine ("Entering", String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));

			int p;

			if (e.SocketError != SocketError.Success)
			{
				p = Interlocked.Decrement (ref this.pendingAsync);
				Trace.WriteLine (String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));
				Disconnect (true, DisconnectedReason.ConnectionFailed);
				OnConnectionFailed (new ClientConnectionEventArgs (this));
				return;
			}

			e.Completed -= ConnectCompleted;
			e.Completed += ReliableReceiveCompleted;
			e.SetBuffer (this.rmessageBuffer, 0, this.rmessageBuffer.Length);
			this.rreader = new BufferValueReader (this.rmessageBuffer);

			bool recevied;
			lock (this.stateSync)
			{
				if (!IsConnected)
				{
					Trace.WriteLine ("Already disconnected", String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));
					p = Interlocked.Decrement (ref this.pendingAsync);
					Trace.WriteLine (String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));
					return;
				}

				p = Interlocked.Increment (ref this.pendingAsync);
				Trace.WriteLine (String.Format ("Increment pending: {0}", p), String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));
				recevied = !this.reliableSocket.ReceiveAsync (e);
			}

			if (recevied)
				ReliableReceiveCompleted (this.reliableSocket, e);

			p = Interlocked.Decrement (ref this.pendingAsync);
			Trace.WriteLine (String.Format ("Decrement pending: {0}", p), String.Format ("{2}:{3} {4}:ConnectCompleted({0},{1})", e.BytesTransferred, e.SocketError, this.typeName, connectionId, c));
			Send (new ConnectMessage { Protocols = this.protocols.Values });
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
							this.activityTimer = new Timer (ActivityCallback, null, ping.Interval, ping.Interval);
					}
					else if (ping.Interval != this.pingFrequency)
						this.activityTimer.Change (ping.Interval, ping.Interval);
					
					this.pingFrequency = ((PingMessage)e.Message).Interval;
					break;

				case (ushort)TempestMessageType.AcknowledgeConnect:
				    var msg = (AcknowledgeConnectMessage)e.Message;
				    this.protocols = this.protocols.Values.Intersect (msg.EnabledProtocols).ToDictionary (pr => pr.id);
					NetworkId = msg.NetworkId;

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

					this.aes = encryption;
					this.hmac = new HMACSHA256 (this.aes.Key);
				    break;

				case (ushort)TempestMessageType.Connected:
					OnConnected (new ClientConnectionEventArgs (this));
					break;
			}

			base.OnTempestMessageReceived(e);
		}

		protected override void SignMessage (BufferValueWriter writer, int headerLength)
		{
			if (this.hmac == null)
				writer.WriteBytes (this.pkAuthentication.HashAndSign (writer.Buffer, headerLength, writer.Length - headerLength));
			else
				base.SignMessage (writer, headerLength);
		}

		protected override bool VerifyMessage (Message message, byte[] signature, byte[] data, int moffset, int length)
		{
			if (this.hmac == null)
			{
				byte[] resized = new byte[length];
				Buffer.BlockCopy (data, moffset, resized, 0, length);

				var msg = (AcknowledgeConnectMessage)message;

				this.serverAuthentication = this.publicKeyCryptoFactory();
				this.serverAuthenticationKey = msg.PublicAuthenticationKey;
				this.serverAuthentication.ImportKey (this.serverAuthenticationKey);

				return this.serverAuthentication.VerifySignedHash (resized, signature);
			}
			else
				return base.VerifyMessage (message, signature, data, moffset, length);
		}

		protected override void OnDisconnected (DisconnectedEventArgs e)
		{
			if (this.activityTimer != null)
			{
				this.activityTimer.Dispose();
				this.activityTimer = null;
			}

			base.OnDisconnected(e);
		}

		private void ActivityCallback (object state)
		{
			if (!this.disconnecting && DateTime.Now.Subtract (this.lastReceived).TotalMilliseconds > this.pingFrequency * 2)
				Disconnect (true);
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
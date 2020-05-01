//
// NetworkServerConnection.cs
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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public sealed class NetworkServerConnection
		: NetworkConnection, IServerConnection, IAuthenticatedConnection
	{
		internal NetworkServerConnection (IEnumerable<string> signatureHashAlgs, IEnumerable<Protocol> protocols, Socket reliableSocket, NetworkConnectionProvider provider)
			: base (protocols, null, false)
		{
			if (signatureHashAlgs == null)
				throw new ArgumentNullException ("signatureHashAlgs");
			if (reliableSocket == null)
				throw new ArgumentNullException ("reliableSocket");
			if (provider == null)
				throw new ArgumentNullException ("provider");

			this.signatureHashAlgs = signatureHashAlgs;
			this.provider = provider;

			RemoteTarget = reliableSocket.RemoteEndPoint.ToTarget();

			this.reliableSocket = reliableSocket;

			var asyncArgs = new SocketAsyncEventArgs();
			asyncArgs.UserToken = this;
			asyncArgs.SetBuffer (this.rmessageBuffer, 0, 20480);
			asyncArgs.Completed += ReliableReceiveCompleted;

			int p = Interlocked.Increment (ref this.pendingAsync);
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), "new NetworkServerConnection:" + connectionId);

			this.rreader = new BufferValueReader (this.rmessageBuffer);

			this.serializer = new ServerMessageSerializer (this, protocols);

			if (!this.reliableSocket.ReceiveAsync (asyncArgs))
				ReliableReceiveCompleted (this, asyncArgs);
		}

		public override IAsymmetricKey RemoteKey
		{
			get { return this.publicAuthenticationKey; }
		}

		private bool receivedProtocols;
		private readonly IEnumerable<string> signatureHashAlgs;
		internal readonly NetworkConnectionProvider provider;

		protected override int PingFrequency
		{
			get { return this.provider.PingFrequency; }
		}

		internal void PingTimerCallback (object sender, EventArgs e)
		{
			string callCategory = null;
			#if TRACE
			callCategory = String.Format ("NetworkServerConnection:{1} PingCallback({0})", this.pingsOut, connectionId);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			Ping();

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}

		protected override void OnMessageReceived (MessageEventArgs e)
		{
			if (!this.formallyConnected)
			{
				DisconnectAsync();
				return;
			}

			base.OnMessageReceived(e);
		}

		protected override void OnTempestMessageReceived (MessageEventArgs e)
		{
		    switch (e.Message.MessageType)
		    {
		        case (ushort)TempestMessageType.Connect:
		            var msg = (ConnectMessage)e.Message;

					if (!IsConnected)
						return;

					if (!msg.Protocols.Any())
					{
						DisconnectAsync (ConnectionResult.FailedHandshake);
						return;
					}

					if (this.requiresHandshake)
					{
						bool foundHashAlg = false;
						foreach (string hashAlg in this.signatureHashAlgs)
						{
							if (msg.SignatureHashAlgorithms.Contains (hashAlg))
							{
								this.serializer.SigningHashAlgorithm = hashAlg;
								foundHashAlg = true;
								break;
							}
						}

						if (!foundHashAlg)
						{
							DisconnectAsync (ConnectionResult.FailedHandshake);
							return;
						}
					}

					ConnectionId = this.provider.GetConnectionId();

					foreach (Protocol ip in msg.Protocols)
					{
						Protocol lp;
						if (!this.protocols.TryGetValue (ip.id, out lp) || !lp.CompatibleWith (ip))
						{
							DisconnectAsync (ConnectionResult.IncompatibleVersion);
							return;
						}
					}

					this.protocols = this.protocols.Values.Intersect (msg.Protocols).ToDictionary (p => p.id);
					this.receivedProtocols = true;

					if (!this.requiresHandshake)
					{
						this.formallyConnected = true;
						this.provider.Connect (this);
						e.Connection.SendAsync (new ConnectedMessage());
					}
					else
					{
						while (!this.authReady)
							Thread.Sleep(0);

						MessageSerializer s = this.serializer;
						if (s == null)
							return;

						e.Connection.SendAsync (new AcknowledgeConnectMessage
						{
							SignatureHashAlgorithm = s.SigningHashAlgorithm,
							EnabledProtocols = this.protocols.Values,
							ConnectionId = ConnectionId,
							PublicAuthenticationKey = this.provider.PublicAuthenticationKey,
							PublicEncryptionKey = this.provider.PublicEncryptionKey
						});
					}
		    		break;

				case (ushort)TempestMessageType.FinalConnect:
					if (!this.receivedProtocols)
					{
						DisconnectAsync (ConnectionResult.FailedHandshake);
						return;
					}

					var finalConnect = (FinalConnectMessage)e.Message;

					if (finalConnect.AESKey == null || finalConnect.AESKey.Length == 0 || finalConnect.PublicAuthenticationKey == null)
					{
						DisconnectAsync (ConnectionResult.FailedHandshake);
						return;
					}

					try
					{
						byte[] aeskey = this.provider.pkEncryption.Decrypt (finalConnect.AESKey);

						this.serializer.HMAC = new HMACSHA256 (aeskey);
						this.serializer.AES = new AesManaged { KeySize = 256, Key = aeskey };
					}
					catch
					{
						DisconnectAsync (ConnectionResult.FailedHandshake);
						return;
					}

					this.formallyConnected = true;
					this.provider.Connect (this);

					SendAsync (new ConnectedMessage { ConnectionId = ConnectionId });

					break;
		    }

		    base.OnTempestMessageReceived(e);
		}

		protected override void Recycle()
		{
			this.provider.Disconnect (this);

			if (this.reliableSocket == null)
				return;

			//NetworkConnectionProvider.ReliableSockets.Push (this.reliableSocket);

			base.Recycle();
		}

		RSACrypto IAuthenticatedConnection.LocalCrypto
		{
			get { return this.provider.authentication; }
		}

		IAsymmetricKey IAuthenticatedConnection.LocalKey
		{
			get { return LocalKey; }
			set { this.authenticationKey = (RSAAsymmetricKey) value; }
		}

		RSACrypto IAuthenticatedConnection.RemoteCrypto
		{
			get { return this.pkAuthentication; }
		}

		IAsymmetricKey IAuthenticatedConnection.RemoteKey
		{
			get { return this.RemoteKey; }
			set { this.publicAuthenticationKey = (RSAAsymmetricKey) value; }
		}

		RSACrypto IAuthenticatedConnection.Encryption
		{
			get { return this.provider.pkEncryption; }
		}
	}
}
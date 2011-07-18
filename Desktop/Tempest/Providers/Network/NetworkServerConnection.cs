//
// NetworkServerConnection.cs
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
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public sealed class NetworkServerConnection
		: NetworkConnection, IServerConnection
	{
		internal NetworkServerConnection (IEnumerable<string> signatureHashAlgs, IEnumerable<Protocol> protocols, Socket reliableSocket, NetworkConnectionProvider provider)
			: base (protocols, provider.pkCryptoFactory, null, false)
		{
			if (signatureHashAlgs == null)
				throw new ArgumentNullException ("signatureHashAlgs");
			if (reliableSocket == null)
				throw new ArgumentNullException ("reliableSocket");
			if (provider == null)
				throw new ArgumentNullException ("provider");

			this.signatureHashAlgs = signatureHashAlgs;
			this.provider = provider;

			RemoteEndPoint = reliableSocket.RemoteEndPoint;

			this.reliableSocket = reliableSocket;

			var asyncArgs = new SocketAsyncEventArgs();
			asyncArgs.UserToken = this;
			asyncArgs.SetBuffer (this.rmessageBuffer, 0, 20480);
			asyncArgs.Completed += ReliableReceiveCompleted;

			int p = Interlocked.Increment (ref this.pendingAsync);
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p), "new NetworkServerConnection:" + connectionId);

			if (!this.reliableSocket.ReceiveAsync (asyncArgs))
				ReliableReceiveCompleted (this, asyncArgs);

			this.rreader = new BufferValueReader (this.rmessageBuffer);

			provider.PingFrequencyChanged += ProviderOnPingFrequencyChanged;

			this.pingTimer = new Timer (provider.PingFrequency, PingCallback);
		}

		private static int nextNetworkId;

		private bool receivedProtocols;
		private readonly IEnumerable<string> signatureHashAlgs;
		private readonly NetworkConnectionProvider provider;

		private readonly object pingSync = new object();
		private Timer pingTimer;

		private void PingCallback()
		{
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", String.Format ("NetworkServerConnection:{1} PingCallback({0})", this.pingsOut, connectionId));

			if (this.pingsOut >= 2)
			{
				Disconnect(); // Connection timed out
				return;
			}

			Send (new PingMessage { Interval = provider.PingFrequency });
		}

		protected override void OnMessageReceived (MessageEventArgs e)
		{
			if (!this.formallyConnected)
			{
				Disconnect();
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
						Disconnect (ConnectionResult.FailedHandshake);
						return;
					}

					if (this.requiresHandshake)
					{
						bool foundHashAlg = false;
						foreach (string hashAlg in this.signatureHashAlgs)
						{
							if (msg.SignatureHashAlgorithms.Contains (hashAlg))
							{
								this.signingHashAlgorithm = hashAlg;
								foundHashAlg = true;
								break;
							}
						}

						if (!foundHashAlg)
						{
							this.NotifyAndDisconnect (ConnectionResult.FailedHandshake);
							return;
						}
					}

		    		NetworkId = Interlocked.Increment (ref nextNetworkId);

					foreach (Protocol ip in msg.Protocols)
					{
						Protocol lp;
						if (!this.protocols.TryGetValue (ip.id, out lp) || !lp.CompatibleWith (ip))
						{
							this.NotifyAndDisconnect (ConnectionResult.IncompatibleVersion);
							return;
						}
					}

					this.protocols = this.protocols.Values.Intersect (msg.Protocols).ToDictionary (p => p.id);
					this.receivedProtocols = true;

					if (!this.requiresHandshake)
					{
						this.formallyConnected = true;
						this.provider.Connect (this);
						e.Connection.Send (new ConnectedMessage());
					}
					else
					{
						while (!this.authReady)
							Thread.Sleep(0);

						e.Connection.Send (new AcknowledgeConnectMessage
						{
							SignatureHashAlgorithm = this.signingHashAlgorithm,
							EnabledProtocols = this.protocols.Values,
							NetworkId = NetworkId,
							PublicAuthenticationKey = this.provider.PublicAuthenticationKey,
							PublicEncryptionKey = this.provider.PublicEncryptionKey
						});
					}
		    		break;

				case (ushort)TempestMessageType.FinalConnect:
					if (!this.receivedProtocols)
					{
						Disconnect (ConnectionResult.FailedHandshake);
						return;
					}

					var finalConnect = (FinalConnectMessage)e.Message;

					if (finalConnect.AESKey == null || finalConnect.AESKey.Length == 0 || finalConnect.PublicAuthenticationKey == null)
					{
						Disconnect (ConnectionResult.FailedHandshake);
						return;
					}

					try
					{
						byte[] aeskey = this.provider.pkEncryption.Decrypt (finalConnect.AESKey);

						this.hmac = new HMACSHA256 (aeskey);
						this.aes = new AesManaged { KeySize = 256, Key = aeskey };
					}
					catch
					{
						Disconnect (ConnectionResult.FailedHandshake);
						return;
					}

					this.formallyConnected = true;
					this.provider.Connect (this);

					Send (new ConnectedMessage());

					break;
		    }

		    base.OnTempestMessageReceived(e);
		}

		protected override void SignMessage (string hashAlg, BufferValueWriter writer, int headerLength)
		{
			if (this.hmac == null)
				writer.WriteBytes (this.provider.authentication.HashAndSign (hashAlg, writer.Buffer, headerLength, writer.Length - headerLength));
			else
				base.SignMessage (hashAlg, writer, headerLength);
		}

		protected override bool VerifyMessage (string hashAlg, Message message, byte[] signature, byte[] data, int moffset, int length)
		{
			if (this.hmac == null)
			{
				var msg = (FinalConnectMessage)message;

				byte[] resized = new byte[length];
				Buffer.BlockCopy (data, moffset, resized, 0, length);

				IAsymmetricKey key = (IAsymmetricKey)Activator.CreateInstance (msg.PublicAuthenticationKeyType);
				key.Deserialize (new BufferValueReader (msg.PublicAuthenticationKey), this.provider.pkEncryption);

				this.publicAuthenticationKey = key;
				this.pkAuthentication.ImportKey (key);

				return this.pkAuthentication.VerifySignedHash (hashAlg, resized, signature);
			}
			else
				return base.VerifyMessage (hashAlg, message, signature, data, moffset, length);
		}

		protected override void OnMessageSent (MessageEventArgs e)
		{
			var pingMsg = (e.Message as PingMessage);
			if (pingMsg != null)
				Interlocked.Increment (ref this.pingsOut);

			base.OnMessageSent(e);
		}

		private void ProviderOnPingFrequencyChanged (object sender, EventArgs e)
		{
			lock (this.pingSync)
			{
				if (this.pingTimer != null)
					this.pingTimer.Interval = this.provider.PingFrequency;
			}
		}

		protected override void Recycle()
		{
			lock (this.pingSync)
			{
				if (this.pingTimer != null)
				{
					this.pingTimer.Dispose();
					this.pingTimer = null;
				}
			}

			this.provider.PingFrequencyChanged -= ProviderOnPingFrequencyChanged;
			this.provider.Disconnect (this);

			if (this.reliableSocket == null)
				return;

			//#if !NET_4
			//lock (NetworkConnectionProvider.ReliableSockets)
			//#endif
			//    NetworkConnectionProvider.ReliableSockets.Push (this.reliableSocket);

			base.Recycle();
		}
	}
}
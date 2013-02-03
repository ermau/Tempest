//
// UdpClientConnection.cs
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
using System.Threading;
using System.Threading.Tasks;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public sealed class UdpClientConnection
		: UdpConnection, IClientConnection, IConnectionlessMessenger, IAuthenticatedConnection
	{
		public UdpClientConnection (Protocol protocol)
			: base (new[] { protocol })
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
		}

		public UdpClientConnection (IEnumerable<Protocol> protocols)
			: base (protocols)
		{
			if (protocols == null)
				throw new ArgumentNullException ("protocols");
		}

		public UdpClientConnection (Protocol protocol, Func<IPublicKeyCrypto> cryptoFactory, IAsymmetricKey key)
			: base (new[] { protocol }, cryptoFactory(), cryptoFactory(), key)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			if (cryptoFactory == null)
				throw new ArgumentNullException ("cryptoFactory");
			if (key == null)
				throw new ArgumentNullException ("key");

			this.cryptoFactory = cryptoFactory;
			this.serializer = new ClientMessageSerializer (this, new[] { protocol });
		}

		public UdpClientConnection (IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> cryptoFactory, IAsymmetricKey key)
			: base (protocols, cryptoFactory(), cryptoFactory(), key)
		{
			if (protocols == null)
				throw new ArgumentNullException ("protocols");
			if (cryptoFactory == null)
				throw new ArgumentNullException ("cryptoFactory");
			if (key == null)
				throw new ArgumentNullException ("key");

			this.cryptoFactory = cryptoFactory;
		}

		public event EventHandler<ClientConnectionEventArgs> Connected;
		public event EventHandler<ConnectionlessMessageEventArgs> ConnectionlessMessageReceived;

		public IEnumerable<Target> LocalTargets
		{
			get { return this.listener.LocalTargets; }
		}

		public void SendConnectionlessMessage (Message message, Target target)
		{
			IConnectionlessMessenger messenger = this.listener;
			if (messenger != null)
				messenger.SendConnectionlessMessage (message, target);
		}

		public bool IsRunning
		{
			get
			{
				IListener l = this.listener;
				return l != null && l.IsRunning;
			}
		}

		public void Start (MessageTypes messageTypes)
		{
			this.listener = new UdpClientConnectionlessListener (this, Protocols);
			this.listener.ConnectionlessMessageReceived += OnListenerConnectionlessMessageReceived;
			this.listener.Start (messageTypes);
		}

		public void Stop()
		{
			IConnectionlessMessenger messenger = this.listener;
			if (messenger != null)
			{
				messenger.ConnectionlessMessageReceived -= OnListenerConnectionlessMessageReceived;
				messenger.Stop();
				messenger.Dispose();
			}
		}

		public Task<ClientConnectionResult> ConnectAsync (Target target, MessageTypes messageTypes)
		{
			if (target == null)
				throw new ArgumentNullException ("target");
			if (!Enum.IsDefined (typeof (MessageTypes), messageTypes))
				throw new ArgumentOutOfRangeException ("messageTypes");

			EndPoint endPoint = target.ToEndPoint();
			if (endPoint.AddressFamily != AddressFamily.InterNetwork && endPoint.AddressFamily != AddressFamily.InterNetworkV6)
				throw new ArgumentException ("Unsupported endpoint AddressFamily");

			var ntcs = new TaskCompletionSource<ClientConnectionResult>();

			ThreadPool.QueueUserWorkItem (s =>
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Waiting for pending ({0}) async..", this.pendingAsync));

				while (this.pendingAsync > 0 || Interlocked.CompareExchange (ref this.connectTcs, ntcs, null) != null)
					Thread.Sleep (0);

				int p = Interlocked.Increment (ref this.pendingAsync);
				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Increment pending: {0}", p));

				this.serializer = new ClientMessageSerializer (this, this.originalProtocols);

				IEnumerable<string> hashAlgs = null;
				if (this.localCrypto != null)
					hashAlgs = this.localCrypto.SupportedHashAlgs;

				Start (messageTypes);

				this.socket = this.listener.GetSocket (target);

				Timer dtimer = new Timer (100);
				dtimer = new Timer (100);
				dtimer.TimesUp += OnDeliveryTimer;
				dtimer.Start();
				this.deliveryTimer = dtimer;

				Timer t = new Timer (30000);
				Timer previousTimer = Interlocked.Exchange (ref this.connectTimer, t);
				if (previousTimer != null)
					previousTimer.Dispose();

				t.AutoReset = false;
				t.TimesUp += (sender, args) =>
				{
					var tcs = this.connectTcs;
					if (tcs != null)
						tcs.TrySetResult (new ClientConnectionResult (ConnectionResult.ConnectionFailed, null));

					Disconnect (ConnectionResult.ConnectionFailed);
					t.Dispose();
				};
				t.Start();

				RemoteTarget = target;
				SendAsync (new ConnectMessage
				{
					Protocols = Protocols,
					SignatureHashAlgorithms = hashAlgs
				}).ContinueWith (st =>
				{
					int pa = Interlocked.Decrement (ref this.pendingAsync);
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Decrement pending: {0}", pa));
				});
			});

			return ntcs.Task;
		}

		public override void Dispose()
		{
			Stop();
			base.Dispose();
		}

		private UdpClientConnectionlessListener listener;

		private TaskCompletionSource<ClientConnectionResult> connectTcs;
		private Timer connectTimer;
		private Timer deliveryTimer;

		private readonly Func<IPublicKeyCrypto> cryptoFactory;

		private class UdpClientConnectionlessListener
			: UdpConnectionlessListener
		{
			public UdpClientConnectionlessListener (UdpClientConnection connection, IEnumerable<Protocol> protocols)
				: base (protocols, 0)
			{
				this.connection = connection;
			}

			private readonly UdpClientConnection connection;

			protected override bool TryGetConnection (int connectionId, out UdpConnection oconnection)
			{
				oconnection = null;
				if (this.connection.ConnectionId == 0 || connectionId == this.connection.ConnectionId)
				{
					oconnection = this.connection;
					return true;
				}

				return false;
			}

			protected override void OnConnectionlessTempestMessage (TempestMessage tempestMessage, Target target)
			{
				this.connection.OnConnectionlessTempestMessage (tempestMessage, target);
			}
		}

		private void OnListenerConnectionlessMessageReceived (object sender, ConnectionlessMessageEventArgs e)
		{
			var received = ConnectionlessMessageReceived;
			if (received != null)
				received (this, e);
		}

		protected override bool IsConnecting
		{
			get { return this.connectTcs != null; }
		}

		private void OnDeliveryTimer (object sender, EventArgs e)
		{
			ResendPending();
		}

		protected override void Cleanup()
		{
			base.Cleanup();

			Stop();

			Timer timer = Interlocked.Exchange (ref this.deliveryTimer, null);
			if (timer != null)
			{
				timer.TimesUp -= OnDeliveryTimer;
				timer.Dispose();
			}

			this.remoteEncryption = null;

			Timer t = Interlocked.Exchange (ref this.connectTimer, null);
			if (t != null)
				t.Dispose();

			var tcs = Interlocked.Exchange (ref this.connectTcs, null);
			if (tcs != null)
				tcs.TrySetCanceled();
		}

		private void OnConnectionlessTempestMessage (TempestMessage tempestMessage, Target target)
		{
			OnTempestMessage (new MessageEventArgs (this, tempestMessage));
		}

		protected override void OnTempestMessage (MessageEventArgs e)
		{
			switch (e.Message.MessageType)
			{
				case (ushort)TempestMessageType.AcknowledgeConnect:
				{
					var msg = (AcknowledgeConnectMessage)e.Message;

					this.serializer.Protocols = this.serializer.Protocols.Intersect (msg.EnabledProtocols);
					ConnectionId = msg.ConnectionId;

					this.remoteEncryption = this.cryptoFactory();
					this.remoteEncryption.ImportKey (msg.PublicEncryptionKey);

					var encryption = new AesManaged { KeySize = 256 };
					encryption.GenerateKey();

					BufferValueWriter authKeyWriter = new BufferValueWriter (new byte[1600]);
					LocalKey.Serialize (authKeyWriter, this.remoteEncryption, includePrivate: false);

					SendAsync (new FinalConnectMessage
					{
						AESKey = this.remoteEncryption.Encrypt (encryption.Key),
						PublicAuthenticationKeyType = LocalKey.GetType(),
						PublicAuthenticationKey = authKeyWriter.ToArray()
					});

					this.serializer.AES = encryption;
					this.serializer.HMAC = new HMACSHA256 (encryption.Key);

					break;
				}

				case (ushort)TempestMessageType.Connected:
				{
					var msg = (ConnectedMessage)e.Message;
					
					ConnectionId = msg.ConnectionId;

					this.formallyConnected = true;

					Timer t = Interlocked.Exchange (ref this.connectTimer, null);
					if (t != null)
						t.Dispose();

					var tcs = Interlocked.Exchange (ref this.connectTcs, null);
					if (tcs != null)
					{
						if (tcs.TrySetResult (new ClientConnectionResult (ConnectionResult.Success, RemoteKey)))
							OnConnected();
					}

					break;
				}

				default:
					base.OnTempestMessage (e);
					break;
			}
		}

		private void OnConnected()
		{
			var connected = this.Connected;
			if (connected != null)
				connected (this, new ClientConnectionEventArgs (this));
		}

		IPublicKeyCrypto IAuthenticatedConnection.LocalCrypto
		{
			get { return this.localCrypto; }
		}

		IAsymmetricKey IAuthenticatedConnection.RemoteKey
		{
			get { return RemoteKey; }
			set { RemoteKey = value; }
		}

		private IPublicKeyCrypto remoteEncryption;
		IPublicKeyCrypto IAuthenticatedConnection.Encryption
		{
			get
			{
				if (this.remoteEncryption == null)
					this.remoteEncryption = this.cryptoFactory();

				return this.remoteEncryption;
			}
		}

		IAsymmetricKey IAuthenticatedConnection.LocalKey
		{
			get { return LocalKey; }
			set { LocalKey = value; }
		}

		IPublicKeyCrypto IAuthenticatedConnection.RemoteCrypto
		{
			get { return this.remoteCrypto; }
		}
	}
}
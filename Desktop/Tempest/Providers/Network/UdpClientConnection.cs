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
		: UdpConnection, IClientConnection, IAuthenticatedConnection
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

		public Task<ClientConnectionResult> ConnectAsync (EndPoint endPoint, MessageTypes messageTypes)
		{
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");
			if (!Enum.IsDefined (typeof (MessageTypes), messageTypes))
				throw new ArgumentOutOfRangeException ("messageTypes");
			if (endPoint.AddressFamily != AddressFamily.InterNetwork && endPoint.AddressFamily != AddressFamily.InterNetworkV6)
				throw new ArgumentException ("Unsupported endpoint AddressFamily");

			var ntcs = new TaskCompletionSource<ClientConnectionResult>();
			while (Interlocked.CompareExchange (ref this.connectTcs, ntcs, null) != null)
				Thread.Sleep (0);

			this.serializer = new ClientMessageSerializer (this, this.originalProtocols);

			IEnumerable<string> hashAlgs = null;
			if (this.localCrypto != null)
				hashAlgs = this.localCrypto.SupportedHashAlgs;

			this.socket = new Socket (endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
			IPAddress localIP = (endPoint.AddressFamily == AddressFamily.InterNetwork) ? IPAddress.Any : IPAddress.IPv6Any;
			this.socket.Bind (new IPEndPoint (localIP, 0));
			
			SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
			receiveArgs.SetBuffer (new byte[65507], 0, 65507);
			receiveArgs.RemoteEndPoint = this.socket.LocalEndPoint;
			receiveArgs.Completed += OnReceiveCompleted;

			this.reader = new BufferValueReader (receiveArgs.Buffer);

			while (!this.socket.ReceiveFromAsync (receiveArgs))
				OnReceiveCompleted (this, receiveArgs);

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

				Disconnect (true, ConnectionResult.ConnectionFailed);
				t.Dispose();
			};
			t.Start();

			RemoteEndPoint = endPoint;
			Send (new ConnectMessage
			{
				Protocols = Protocols,
				SignatureHashAlgorithms = hashAlgs
			});

			return ntcs.Task;
		}

		public override void Dispose()
		{
			base.Dispose();
		}

		private BufferValueReader reader;
		private TaskCompletionSource<ClientConnectionResult> connectTcs;
		private Timer connectTimer;
		private readonly Func<IPublicKeyCrypto> cryptoFactory;

		protected override bool IsConnecting
		{
			get { return this.connectTcs != null; }
		}

		protected override void Cleanup()
		{
			base.Cleanup();

			Timer t = Interlocked.Exchange (ref this.connectTimer, null);
			if (t != null)
				t.Dispose();

			var tcs = Interlocked.Exchange (ref this.connectTcs, null);
			if (tcs != null)
				tcs.TrySetCanceled();

			Socket sock = Interlocked.Exchange (ref this.socket, null);
			if (sock != null)
				sock.Dispose();
		}

		private void OnReceiveCompleted (object sender, SocketAsyncEventArgs e)
		{
			byte[] buffer = e.Buffer;
			int offset = e.Offset;
			int moffset = e.Offset;
			int remaining = e.BytesTransferred;

			if (e.BytesTransferred != 0 && e.SocketError == SocketError.Success)
			{
				MessageHeader header = null;
				List<Message> messages = this.serializer.BufferMessages (ref buffer, ref offset, ref moffset, ref remaining, ref header, ref this.reader);
				if (messages != null)
				{
					foreach (Message message in messages)
						Receive (message);
				}
			}

			Socket sock = this.socket;
			if (sock != null)
			{
				this.reader.Position = 0;
				e.SetBuffer (buffer, 0, buffer.Length);
				try
				{
					while (!sock.ReceiveFromAsync (e))
						OnReceiveCompleted (this, e);
				}
				catch (ObjectDisposedException)
				{
				}
			}
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

					Send (new FinalConnectMessage
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
			}

			base.OnTempestMessage (e);
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
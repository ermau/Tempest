// UdpConnectionProvider.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public sealed class UdpConnectionProvider
		: UdpConnectionlessListener, IConnectionProvider
	{
		public UdpConnectionProvider (int port, Protocol protocol)
			: base (new[] { protocol })
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			if (port <= 0)
				throw new ArgumentOutOfRangeException ("port");

			this.port = port;
			ValidateProtocols (this.protocols.Values);
		}

		public UdpConnectionProvider (int port, Protocol protocol, Func<IPublicKeyCrypto> cryptoFactory, IAsymmetricKey authKey)
			: base (new[] { protocol })
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			if (cryptoFactory == null)
				throw new ArgumentNullException ("cryptoFactory");
			if (authKey == null)
				throw new ArgumentNullException ("authKey");
			if (port <= 0)
				throw new ArgumentOutOfRangeException ("port");

			this.port = port;
			this.cryptoFactory = cryptoFactory;
			this.crypto = cryptoFactory();
			this.crypto.ImportKey (authKey);

			this.pkEncryption = cryptoFactory();
			this.pkEncryption.ImportKey (authKey);

			this.authKey = authKey;

			ValidateProtocols (this.protocols.Values);
		}

		public UdpConnectionProvider (int port, IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> cryptoFactory, IAsymmetricKey authKey)
			: base (protocols)
		{
			if (protocols == null)
				throw new ArgumentNullException ("protocols");
			if (cryptoFactory == null)
				throw new ArgumentNullException ("cryptoFactory");
			if (authKey == null)
				throw new ArgumentNullException ("authKey");
			if (port <= 0)
				throw new ArgumentOutOfRangeException ("port");

			this.port = port;
			this.cryptoFactory = cryptoFactory;
			this.crypto = cryptoFactory();
			this.crypto.ImportKey (authKey);
			this.authKey = authKey;

			ValidateProtocols (this.protocols.Values);
		}

		public event EventHandler<ConnectionMadeEventArgs> ConnectionMade;

		public IAsymmetricKey PublicKey
		{
			get { return this.authKey; }
		}

		public override void Start (MessageTypes types)
		{
			base.Start (types);

			Timer timer = new Timer (100);
			timer = new Timer (100);
			timer.TimesUp += OnDeliveryTimer;
			timer.Start();
			this.deliveryTimer = timer;
		}

		public override void Stop()
		{
			Timer timer = this.deliveryTimer;
			if (timer != null)
			{
				timer.TimesUp -= OnDeliveryTimer;
				timer.Dispose();
			}

			base.Stop();
		}

		private readonly Func<IPublicKeyCrypto> cryptoFactory;
		internal IPublicKeyCrypto pkEncryption;
		private IPublicKeyCrypto crypto;
		private IAsymmetricKey authKey;

		private int nextConnectionId;
		private readonly ConcurrentDictionary<int, UdpServerConnection> connections = new ConcurrentDictionary<int, UdpServerConnection>();

		private Timer deliveryTimer;

		protected override void HandleConnectionMessage (SocketAsyncEventArgs args, MessageHeader header, ref BufferValueReader reader)
		{
			byte[] buffer = args.Buffer;
			int offset = args.Offset;
			int moffset = offset;
			int remaining = args.BytesTransferred;

			UdpServerConnection connection;
			if (this.connections.TryGetValue (header.ConnectionId, out connection))
			{
				MessageSerializer serializer = connection.serializer;

				if (header.State == HeaderState.IV)
				{
					serializer.DecryptMessage (header, ref reader);
					header.IsStillEncrypted = false;

					if (!serializer.TryGetHeader (reader, args.BytesTransferred, ref header))
						return;
				}

				header.SerializationContext = ((SerializationContext)header.SerializationContext).WithConnection (connection);

				if (serializer != null)
				{
					List<Message> messages = connection.serializer.BufferMessages (ref buffer, ref offset, ref moffset, ref remaining, ref header, ref reader);
					if (messages != null)
					{
						foreach (Message message in messages)
							connection.Receive (message);
					}

					reader = new BufferValueReader (buffer);
				}
			}
		}

		internal void Connect (UdpServerConnection connection)
		{
			var args = new ConnectionMadeEventArgs (connection, connection.RemoteKey);
			OnConnectionMade (args);

			if (args.Rejected)
				connection.Dispose();
		}

		internal void Disconnect (UdpServerConnection connection)
		{
			UdpServerConnection o;
			this.connections.TryRemove (connection.ConnectionId, out o);
		}

		private void OnDeliveryTimer (object sender, EventArgs eventArgs)
		{
			foreach (UdpServerConnection connection in this.connections.Values)
				connection.ResendPending();
		}

		private void OnConnectionMade (ConnectionMadeEventArgs e)
		{
			var handler = this.ConnectionMade;
			if (handler != null)
				handler (this, e);
		}

		internal Socket GetSocket (EndPoint endPoint)
		{
			if (endPoint.AddressFamily == AddressFamily.InterNetwork)
				return this.socket4;
			else if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
				return this.socket6;
			else
				throw new ArgumentException();
		}

		protected override void OnConnectionlessTempestMessage (TempestMessage tempestMessage, EndPoint endPoint)
		{
			ConnectMessage connect = tempestMessage as ConnectMessage;
			if (connect != null)
			{
				UdpServerConnection connection;
				if (this.cryptoFactory != null)
					connection = new UdpServerConnection (GetConnectionId(), endPoint, this, this.cryptoFactory(), this.crypto, this.authKey);
				else
					connection = new UdpServerConnection (GetConnectionId(), endPoint, this);

				if (!this.connections.TryAdd (connection.ConnectionId, connection))
					throw new InvalidOperationException ("Reused connection ID");

				connection.Receive (connect);
			}
			else
				base.OnConnectionlessTempestMessage (tempestMessage, endPoint);
		}

		private int GetConnectionId()
		{
			return Interlocked.Increment (ref nextConnectionId);
		}

		private void ValidateProtocols (IEnumerable<Protocol> newProtocols)
		{
			foreach (Protocol newProtocol in newProtocols)
			{
				if (newProtocol == null)
					throw new ArgumentException ("null Protocol in protocols", "protocols");

				if (newProtocol.RequiresHandshake && this.crypto == null)
					throw new ArgumentException ("Protocol requires handshake, but no crypto provided", "protocols");
			}
		}
	}
}
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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public sealed class UdpConnectionProvider
		: IConnectionProvider
	{
		public UdpConnectionProvider (int port, Protocol protocol)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			if (port <= 0)
				throw new ArgumentOutOfRangeException ("port");

			this.port = port;
			SetupProtocols (new[] { protocol });
		}

		public UdpConnectionProvider (int port, Protocol protocol, Func<IPublicKeyCrypto> cryptoFactory, IAsymmetricKey authKey)
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

			SetupProtocols (new[] { protocol });
		}

		public UdpConnectionProvider (int port, IEnumerable<Protocol> protocols, Func<IPublicKeyCrypto> cryptoFactory, IAsymmetricKey authKey)
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

			SetupProtocols (protocols);
		}

		public event EventHandler<ConnectionMadeEventArgs> ConnectionMade;
		public event EventHandler<ConnectionlessMessageEventArgs> ConnectionlessMessageReceived;

		public bool SupportsConnectionless
		{
			get { return true; }
		}

		public bool IsRunning
		{
			get { return this.running; }
		}

		public IAsymmetricKey PublicKey
		{
			get { return this.authKey; }
		}

		public void Start (MessageTypes types)
		{
			if (this.running)
				return;

			this.running = true;
			this.mtypes = types;

			if (Socket.OSSupportsIPv4)
			{
				this.socket4 = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				this.socket4.EnableBroadcast = true;
				this.socket4.Bind (new IPEndPoint (IPAddress.Any, this.port));

				var args = new SocketAsyncEventArgs();
				byte[] buffer = new byte[65507];
				args.SetBuffer (buffer, 0, buffer.Length);
				args.UserToken = new Tuple<Socket, BufferValueReader> (this.socket4, new BufferValueReader (buffer));
				args.Completed += Receive;
				args.RemoteEndPoint = new IPEndPoint (IPAddress.Any, this.port);

				while (!this.socket4.ReceiveFromAsync (args))
					Receive (this, args);
			}

			if (Socket.OSSupportsIPv6)
			{
				this.socket6 = new Socket (AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
				this.socket6.EnableBroadcast = true;
				this.socket6.Bind (new IPEndPoint (IPAddress.IPv6Any, this.port));

				var args = new SocketAsyncEventArgs();
				byte[] buffer = new byte[65507];
				args.SetBuffer (buffer, 0, buffer.Length);
				args.UserToken = new Tuple<Socket, BufferValueReader> (this.socket6, new BufferValueReader (buffer));
				args.Completed += Receive;
				args.RemoteEndPoint = new IPEndPoint (IPAddress.IPv6Any, this.port);

				while (!this.socket6.ReceiveFromAsync (args))
					Receive (this, args);
			}
		}

		public void SendConnectionlessMessage (Message message, EndPoint endPoint)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");

			if (message.MustBeReliable)
				throw new NotSupportedException ("Reliable messages can not be sent connectionlessly");

			if (endPoint.AddressFamily == AddressFamily.InterNetwork && !Socket.OSSupportsIPv4)
				throw new NotSupportedException ("endPoint's AddressFamily not supported on this OS.");
			else if (endPoint.AddressFamily == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
				throw new NotSupportedException ("endPoint's AddressFamily not supported on this OS.");
			else if (endPoint.AddressFamily != AddressFamily.InterNetwork && endPoint.AddressFamily != AddressFamily.InterNetworkV6)
				throw new NotSupportedException ("endPoint's AddressFamily not supported on this provider.");

			if (!this.running)
				return;

			Socket socket = (endPoint.AddressFamily == AddressFamily.InterNetwork) ? this.socket4 : this.socket6;
			if (socket == null)
				return;

			int length;
			byte[] buffer = new byte[512];
			buffer = this.connectionlessSerializer.GetBytes (message, out length, buffer);

			var args = new SocketAsyncEventArgs();
			args.SetBuffer (buffer, 0, length);
			args.RemoteEndPoint = endPoint;

			try
			{
				socket.SendToAsync (args);
			}
			catch (SocketException)
			{
			}
		}

		public void Stop()
		{
			Socket four = Interlocked.Exchange (ref this.socket4, null);
			if (four != null)
				four.Dispose();

			Socket six = Interlocked.Exchange (ref this.socket6, null);
			if (six != null)
				six.Dispose();

			this.running = false;
		}

		public void Dispose()
		{
			Stop();
		}

		private readonly Func<IPublicKeyCrypto> cryptoFactory;
		private IPublicKeyCrypto crypto;
		private IAsymmetricKey authKey;
		internal readonly Dictionary<byte, Protocol> protocols = new Dictionary<byte, Protocol>();

		private volatile bool running;
		private MessageTypes mtypes;
		private readonly int port;
		private Socket socket4;
		private Socket socket6;

		private MessageSerializer connectionlessSerializer;

		private int nextConnectionId;
		private readonly ConcurrentDictionary<int, UdpServerConnection> connections = new ConcurrentDictionary<int, UdpServerConnection>();

		internal IPublicKeyCrypto pkEncryption;

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

		private void StartReceive (Socket socket, SocketAsyncEventArgs args, BufferValueReader reader)
		{
			try
			{
				args.SetBuffer (0, args.Buffer.Length);
				while (!socket.ReceiveFromAsync (args))
					Receive (this, args);
			}
			catch (ObjectDisposedException) // Socket is disposed, we're done.
			{
			}
		}

		private void Receive (object sender, SocketAsyncEventArgs args)
		{
			var cnd = (Tuple<Socket, BufferValueReader>)args.UserToken;
			Socket socket = cnd.Item1;
			BufferValueReader reader = cnd.Item2;

			byte[] buffer = args.Buffer;

			if (args.BytesTransferred == 0 || args.SocketError != SocketError.Success)
				return;

			int offset = args.Offset;
			reader.Position = offset;
			int moffset = offset;
			int remaining = args.BytesTransferred;

			MessageHeader header = null;
			
			// We don't currently support partial messages, so an incomplete message is a bad one.
			if (!this.connectionlessSerializer.TryGetHeader (reader, args.BytesTransferred, ref header) || header.Message == null)
			{
				StartReceive (socket, args, reader);
				return;
			}

			if (header.ConnectionId == 0)
				HandleConnectionlessMessage (args, header, reader);
			else
			{
				UdpServerConnection connection;
				if (this.connections.TryGetValue (header.ConnectionId, out connection))
				{
					MessageSerializer serializer = connection.serializer;

					if (header.State == HeaderState.IV)
					{
						serializer.DecryptMessage (header, ref reader);
						header.IsStillEncrypted = false;
						
						if (!serializer.TryGetHeader (reader, args.BytesTransferred, ref header))
						{
							StartReceive (socket, args, reader);
							return;
						}
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
					}

					reader = new BufferValueReader (buffer);
				}
			}

			StartReceive (socket, args, reader);
		}

		private void HandleConnectionlessMessage (SocketAsyncEventArgs args, MessageHeader header, BufferValueReader reader)
		{
			byte[] buffer = args.Buffer;

			int bufferOffset = 0;
			int messageOffset = 0;
			int remaining = args.BytesTransferred;

			List<Message> messages = this.connectionlessSerializer.BufferMessages (ref buffer, ref bufferOffset, ref messageOffset, ref remaining, ref header, ref reader);
			foreach (Message m in messages)
			{
				var tempestMessage = m as TempestMessage;
				if (tempestMessage != null)
				{
					ConnectMessage connect = tempestMessage as ConnectMessage;
					if (connect != null)
					{
						UdpServerConnection connection;
						if (this.cryptoFactory != null)
							connection = new UdpServerConnection (GetConnectionId(), args.RemoteEndPoint, this, this.cryptoFactory(), this.crypto, this.authKey);
						else
							connection = new UdpServerConnection (GetConnectionId(), args.RemoteEndPoint, this);

						if (!this.connections.TryAdd (connection.ConnectionId, connection))
							throw new InvalidOperationException ("Reused connection ID");

						connection.Receive (connect);
					}
					else
						OnTempestMessage (tempestMessage);
				}
				else
					OnConnectionlessMessageReceived (new ConnectionlessMessageEventArgs (m, args.RemoteEndPoint));
			}
		}

		private int GetConnectionId()
		{
			return Interlocked.Increment (ref nextConnectionId);
		}

		private void OnTempestMessage (TempestMessage tempestMessage)
		{
			throw new NotImplementedException();
		}

		private void OnConnectionlessMessageReceived (ConnectionlessMessageEventArgs e)
		{
			var handler = this.ConnectionlessMessageReceived;
			if (handler != null)
				handler (this, e);
		}

		private void SetupProtocols (IEnumerable<Protocol> newProtocols)
		{
			foreach (Protocol newProtocol in newProtocols)
			{
				if (newProtocol == null)
					throw new ArgumentException ("null Protocol in protocols", "protocols");

				if (newProtocol.RequiresHandshake && this.crypto == null)
					throw new ArgumentException ("Protocol requires handshake, but no crypto provided", "protocols");

				this.protocols.Add (newProtocol.id, newProtocol);
			}

			this.protocols.Add (1, TempestMessage.InternalProtocol);

			this.connectionlessSerializer = new MessageSerializer (this.protocols.Values);
		}
	}
}
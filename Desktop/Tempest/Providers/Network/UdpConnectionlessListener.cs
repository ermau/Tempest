using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public abstract class UdpConnectionlessListener
		: IConnectionlessMessenger
	{
		protected UdpConnectionlessListener (IEnumerable<Protocol> protocols)
		{
			this.protocols = protocols.ToDictionary (p => p.id);

			this.protocols.Add (1, TempestMessage.InternalProtocol);
			this.connectionlessSerializer = new MessageSerializer (this.protocols.Values);
		}

		public event EventHandler<ConnectionlessMessageEventArgs> ConnectionlessMessageReceived;

		public bool IsRunning
		{
			get { return this.running; }
		}

		public virtual void Start (MessageTypes types)
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

			if (message.Header == null)
				message.Header = new MessageHeader();

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

		public virtual void Stop()
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

		private volatile bool running;
		private MessageTypes mtypes;
		protected int port;
		protected Socket socket4;
		protected Socket socket6;

		private MessageSerializer connectionlessSerializer;
		internal readonly Dictionary<byte, Protocol> protocols = new Dictionary<byte, Protocol>();

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

			if (args.BytesTransferred == 0 || args.SocketError != SocketError.Success)
				return;

			int offset = args.Offset;
			reader.Position = offset;

			MessageHeader header = null;
			
			// We don't currently support partial messages, so an incomplete message is a bad one.
			if (!this.connectionlessSerializer.TryGetHeader (reader, args.BytesTransferred, ref header) || header.Message == null)
			{
				StartReceive (socket, args, reader);
				return;
			}

			if (header.ConnectionId == 0)
				HandleConnectionlessMessage (args, header, ref reader);
			else
				HandleConnectionMessage (args, header, ref reader);

			StartReceive (socket, args, reader);
		}

		protected abstract void HandleConnectionMessage (SocketAsyncEventArgs args, MessageHeader header, ref BufferValueReader reader);

		private void HandleConnectionlessMessage (SocketAsyncEventArgs args, MessageHeader header, ref BufferValueReader reader)
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
					OnConnectionlessTempestMessage (tempestMessage, args.RemoteEndPoint);
				else
					OnConnectionlessMessageReceived (new ConnectionlessMessageEventArgs (m, args.RemoteEndPoint));
			}
		}

		protected virtual void OnConnectionlessTempestMessage (TempestMessage tempestMessage, EndPoint endPoint)
		{
		}

		private void OnConnectionlessMessageReceived (ConnectionlessMessageEventArgs e)
		{
			var handler = ConnectionlessMessageReceived;
			if (handler != null)
				handler (this, e);
		}
	}
}
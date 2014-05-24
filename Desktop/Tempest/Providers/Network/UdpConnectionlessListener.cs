//
// UdpConnectionlessListener.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2013-2014 Xamarin Inc.
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
using System.Threading;
using System.Threading.Tasks;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public abstract class UdpConnectionlessListener
		: IConnectionlessMessenger
	{
		protected UdpConnectionlessListener (IEnumerable<Protocol> protocols, int port)
		{
			this.port = port;
			this.protocols = protocols.ToDictionary (p => p.id);

			if (!this.protocols.ContainsKey (1))
				this.protocols.Add (1, TempestMessage.InternalProtocol);

			this.connectionlessSerializer = new MessageSerializer (this.protocols.Values);
		}

		public event EventHandler<ConnectionlessMessageEventArgs> ConnectionlessMessageReceived;

		public bool IsRunning
		{
			get { return this.running; }
		}

		public IEnumerable<Target> LocalTargets
		{
			get
			{
				if (this.socket4 != null)
					yield return this.socket4.LocalEndPoint.ToTarget();
				if (this.socket6 != null)
					yield return this.socket6.LocalEndPoint.ToTarget();
			}
		}

		public virtual void Start (MessageTypes types)
		{
			if (this.running)
				return;

			while (this.pendingAsync > 0)
				Thread.Sleep (0);

			this.running = true;
			this.mtypes = types;

			//if (Socket.OSSupportsIPv4)
			{
				this.socket4 = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				#if !WINDOWS_PHONE
				this.socket4.EnableBroadcast = true;
				#endif
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
				#if !WINDOWS_PHONE
				this.socket6.EnableBroadcast = true;
				#endif
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

		public async Task SendConnectionlessMessageAsync (Message message, Target target)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (target == null)
				throw new ArgumentNullException ("target");

			if (message.MustBeReliable)
				throw new NotSupportedException ("Reliable messages can not be sent connectionlessly");

			if (!this.running)
				return;

			IPEndPoint endPoint = await target.ToIPEndPointAsync().ConfigureAwait (false);
			
			if (endPoint.AddressFamily == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
				throw new NotSupportedException ("endPoint's AddressFamily not supported on this OS.");
			else if (endPoint.AddressFamily != AddressFamily.InterNetwork && endPoint.AddressFamily != AddressFamily.InterNetworkV6)
				throw new NotSupportedException ("endPoint's AddressFamily not supported on this provider.");

			Socket socket = GetSocket (endPoint);

			if (message.Header == null)
				message.Header = new MessageHeader();

			int length;
			byte[] buffer = new byte[512];
			buffer = this.connectionlessSerializer.GetBytes (message, out length, buffer);

			var tcs = new TaskCompletionSource<bool>();

			var args = new SocketAsyncEventArgs();
			args.SetBuffer (buffer, 0, length);
			args.RemoteEndPoint = endPoint;
			args.Completed += (sender, e) => {
				tcs.SetResult (e.SocketError == SocketError.Success);
				Interlocked.Decrement (ref this.pendingAsync);
				e.Dispose();
			};

			try
			{
				Interlocked.Increment (ref this.pendingAsync);
				if (!socket.SendToAsync (args)) {
					Interlocked.Decrement (ref this.pendingAsync);
					args.Dispose();
				}
			}
			catch (SocketException)
			{
				Interlocked.Decrement (ref this.pendingAsync);
			}

			await tcs.Task.ConfigureAwait (false);
		}

		public virtual void Stop()
		{
			while (this.pendingAsync > 0)
				Thread.Sleep (0);

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

		private int pendingAsync;

		protected internal Socket GetSocket (EndPoint endPoint)
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
			if (!this.running)
				return;

			Interlocked.Increment (ref this.pendingAsync);

			try
			{
				args.SetBuffer (0, args.Buffer.Length);
				while (!socket.ReceiveFromAsync (args))
					Receive (this, args);
			}
			catch (ObjectDisposedException) // Socket is disposed, we're done.
			{
				Interlocked.Decrement (ref this.pendingAsync);
			}
		}

		private void Receive (object sender, SocketAsyncEventArgs args)
		{
			var cnd = (Tuple<Socket, BufferValueReader>)args.UserToken;
			Socket socket = cnd.Item1;
			BufferValueReader reader = cnd.Item2;

			if (args.BytesTransferred == 0 || args.SocketError != SocketError.Success) {
				reader.Dispose();
				args.Dispose();
				Interlocked.Decrement (ref this.pendingAsync);
				return;
			}

			int offset = args.Offset;
			reader.Position = offset;

			MessageHeader header = null;
			
			// We don't currently support partial messages, so an incomplete message is a bad one.
			if (!this.connectionlessSerializer.TryGetHeader (reader, args.BytesTransferred, ref header) || header.Message == null) {
				Interlocked.Decrement (ref this.pendingAsync);
				StartReceive (socket, args, reader);
				return;
			}

			if (header.ConnectionId == 0)
				HandleConnectionlessMessage (args, header, ref reader);
			else
				HandleConnectionMessage (args, header, ref reader);

			Interlocked.Decrement (ref this.pendingAsync);
			StartReceive (socket, args, reader);
		}

		protected abstract bool TryGetConnection (int connectionId, out UdpConnection connection);

		protected virtual void HandleConnectionMessage (SocketAsyncEventArgs args, MessageHeader header, ref BufferValueReader reader)
		{
			UdpConnection connection;
			if (!TryGetConnection (header.ConnectionId, out connection))
				return;

			byte[] buffer = args.Buffer;
			int offset = args.Offset;
			int moffset = offset;
			int remaining = args.BytesTransferred;

			MessageSerializer serializer = connection.serializer;
			if (serializer == null)
				return;

			if (header.State == HeaderState.IV)
			{
				serializer.DecryptMessage (header, ref reader);
				header.IsStillEncrypted = false;

				if (!serializer.TryGetHeader (reader, args.BytesTransferred, ref header))
					return;
			}

			List<Message> messages = serializer.BufferMessages (ref buffer, ref offset, ref moffset, ref remaining, ref header, ref reader);
			if (messages != null) {
				foreach (Message message in messages)
					connection.Receive (message);
			}

			reader.Position = 0;
		}

		private void HandleConnectionlessMessage (SocketAsyncEventArgs args, MessageHeader header, ref BufferValueReader reader)
		{
			byte[] buffer = args.Buffer;

			int bufferOffset = 0;
			int messageOffset = 0;
			int remaining = args.BytesTransferred;

			List<Message> messages = this.connectionlessSerializer.BufferMessages (ref buffer, ref bufferOffset, ref messageOffset, ref remaining, ref header, ref reader);
			if (messages == null)
				return;

			foreach (Message m in messages)
			{
				var tempestMessage = m as TempestMessage;
				if (tempestMessage != null)
					OnConnectionlessTempestMessage (tempestMessage, args.RemoteEndPoint.ToTarget());
				else
					OnConnectionlessMessageReceived (new ConnectionlessMessageEventArgs (m, args.RemoteEndPoint.ToTarget(), this));
			}
		}

		protected virtual void OnConnectionlessTempestMessage (TempestMessage tempestMessage, Target target)
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
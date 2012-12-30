//
// MockConnectionProvider.cs
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
using System.Threading;
using System.Threading.Tasks;
using Tempest.InternalProtocol;

namespace Tempest.Tests
{
	public class MockConnectionProvider
		: IConnectionProvider
	{
		public MockConnectionProvider (Protocol protocol)
			: this (new [] { protocol })
		{
		}

		public MockConnectionProvider (IEnumerable<Protocol> protocols)
		{
			this.protocols = protocols.ToDictionary (p => p.id);
		}

		public event EventHandler<ConnectionMadeEventArgs> ConnectionMade;
		
		public bool IsRunning
		{
			get { return this.running; }
		}

		public void Start (MessageTypes types)
		{
			this.running = true;
		}

		public void Stop()
		{
			this.running = false;
		}

		public IClientConnection GetClientConnection()
		{
			return new MockClientConnection (this)
			{
				ConnectionId = Interlocked.Increment (ref this.cid)
			};
		}

		public IClientConnection GetClientConnection (Protocol protocol)
		{
			return GetClientConnection (new[] { protocol });
		}

		public IClientConnection GetClientConnection (IEnumerable<Protocol> protocols)
		{
			return new MockClientConnection (this, protocols);
		}

		public MockServerConnection GetServerConnection()
		{
			return GetConnections (this.protocols.Values).Item2;
		}

		public MockServerConnection GetServerConnection (Protocol protocol)
		{
			return GetConnections (protocol).Item2;
		}

		public Tuple<MockClientConnection, MockServerConnection> GetConnections (IEnumerable<Protocol> protocols)
		{
			if (protocols == null)
				throw new ArgumentNullException ("protocols");
			
			MockClientConnection c = new MockClientConnection (this, protocols);
			c.ConnectionId = Interlocked.Increment (ref this.cid);
			c.ConnectAsync (new Target (Target.AnyIP, 0), MessageTypes.Reliable);

			return new Tuple<MockClientConnection, MockServerConnection> (c, c.connection);
		}

		public Tuple<MockClientConnection, MockServerConnection> GetConnections (Protocol protocol)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");

			return GetConnections (new[] { protocol });
		}

		public void Dispose()
		{
		}

		private int cid;
		private bool running;
		private readonly Dictionary<byte, Protocol> protocols;

		internal void Connect (MockClientConnection connection)
		{
			var c = new MockServerConnection (connection);
			c.ConnectionId = connection.ConnectionId;
			connection.connection = c;

			if (connection.protocols != null)
			{
				foreach (Protocol ip in connection.protocols)
				{
					Protocol lp;
					if (!this.protocols.TryGetValue (ip.id, out lp) || !lp.CompatibleWith (ip))
					{
						connection.Disconnect (false, ConnectionResult.IncompatibleVersion);
						return;
					}
				}
			}

			var e = new ConnectionMadeEventArgs (c, null);
			OnConnectionMade (e);

			if (e.Rejected)
			{
				connection.Disconnect (true, ConnectionResult.ConnectionFailed);
				c.Disconnect (true, ConnectionResult.ConnectionFailed);
			}
		}

		private void OnConnectionMade (ConnectionMadeEventArgs e)
		{
			EventHandler<ConnectionMadeEventArgs> handler = ConnectionMade;
			if (handler != null)
				handler (this, e);
		}
	}

	public class MockServerConnection
		: MockConnection, IServerConnection
	{
		private MockClientConnection connection;

		internal MockServerConnection (MockClientConnection connection)
		{
			this.connected = true;
			this.connection = connection;
		}

		public override void Send (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (!IsConnected)
				return;

			base.Send (message);
			connection.Receive (new MessageEventArgs (connection, message));
		}

		public override Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0)
		{
			Task<TResponse> task = base.SendFor<TResponse> (message, timeout);
			Send (message);
			return task;
		}

		public override void SendResponse (Message originalMessage, Message response)
		{
			response.Header = new MessageHeader();
			response.Header.MessageId = originalMessage.Header.MessageId;
			
			OnMessageSent (new MessageEventArgs (this, response));
			connection.ReceiveResponse (new MessageEventArgs (connection, response));
		}

		protected internal override void Disconnect (bool now, ConnectionResult reason = ConnectionResult.FailedUnknown, string customReason = null)
		{
			if (connection == null)
				return;

			var c = connection;
			connection = null;
			c.Disconnect (now, reason, customReason);

			base.Disconnect (now, reason, customReason);
		}
	}

	public class MockClientConnection
		: MockConnection, IClientConnection
	{
		internal readonly IEnumerable<Protocol> protocols;
		private readonly MockConnectionProvider provider;

		public MockClientConnection (MockConnectionProvider provider)
		{
			if (provider == null)
				throw new ArgumentNullException ("provider");

			this.provider = provider;
			//this.connection = new MockServerConnection (this);
		}

		public MockClientConnection (MockConnectionProvider provider, IEnumerable<Protocol> protocols)
			: this (provider)
		{
			this.protocols = protocols;
		}

		public event EventHandler<ClientConnectionEventArgs> Connected;

		public Task<ClientConnectionResult> ConnectAsync (Target target, MessageTypes messageTypes)
		{
			if (target == null)
				throw new ArgumentNullException ("target");

			var tcs = new TaskCompletionSource<ClientConnectionResult>();

			this.connected = true;
			if (provider.IsRunning)
			{
				provider.Connect (this);

				if (this.connected)
				{
					OnConnected (new ClientConnectionEventArgs (this));
					tcs.SetResult (new ClientConnectionResult (ConnectionResult.Success, null));
				}
			}
			else
			{
				OnDisconnected (new DisconnectedEventArgs (this, ConnectionResult.ConnectionFailed));
				tcs.SetResult (new ClientConnectionResult (ConnectionResult.ConnectionFailed, null));
			}

			return tcs.Task;
		}

		public override void Send (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (!IsConnected)
				return;
			
			base.Send (message);
			connection.Receive (new MessageEventArgs (connection, message));
		}

		public override Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0)
		{
			Task<TResponse> task = base.SendFor<TResponse> (message, timeout);
			connection.Receive (new MessageEventArgs (connection, message));
			return task;
		}

		public override void SendResponse (Message originalMessage, Message response)
		{
			response.Header.MessageId = originalMessage.Header.MessageId;
			
			OnMessageSent (new MessageEventArgs (this, response));
			connection.ReceiveResponse (new MessageEventArgs (connection, response));
		}

		protected internal override void Disconnect (bool now, ConnectionResult reason = ConnectionResult.FailedUnknown, string customReason = null)
		{
			if (connection == null)
				return;

			var c = connection;
			connection = null;
			c.Disconnect (now, reason, customReason);
			
			base.Disconnect (now, reason, customReason);
		}

		internal MockServerConnection connection;

		private void OnConnected (ClientConnectionEventArgs e)
		{
			EventHandler<ClientConnectionEventArgs> handler = Connected;
			if (handler != null)
				handler (this, e);
		}
	}

	public class MockAsymmetricKey
		: IAsymmetricKey
	{
		public void Serialize(ISerializationContext context, IValueWriter writer)
		{
		}

		public void Deserialize(ISerializationContext context, IValueReader reader)
		{
		}

		public byte[] PublicSignature
		{
			get { return new byte[0]; }
		}

		public void Serialize (IValueWriter writer, IPublicKeyCrypto crypto, bool includePrivate)
		{
		}

		public void Deserialize (IValueReader reader, IPublicKeyCrypto crypto)
		{
		}
	}

	public abstract class MockConnection
		: IConnection
	{
		protected bool connected;

		public void Dispose()
		{
		}

		public bool IsConnected
		{
			get { return this.connected; }
		}

		public int ConnectionId
		{
			get;
			internal set;
		}

		public IEnumerable<Protocol> Protocols
		{
			get { throw new NotImplementedException(); }
		}

		public int ResponseTime
		{
			get { return -1; }
		}

		public MessagingModes Modes
		{
			get { return MessagingModes.Async; }
		}

		public Target RemoteTarget
		{
			get;
			private set;
		}

		public IAsymmetricKey RemoteKey
		{
			get { return new MockAsymmetricKey(); }
		}

		private MockAsymmetricKey key = new MockAsymmetricKey();
		public IAsymmetricKey LocalKey
		{
			get { return this.key; }
		}

		public event EventHandler<MessageEventArgs> MessageReceived;
		public event EventHandler<MessageEventArgs> MessageSent;
		public event EventHandler<DisconnectedEventArgs> Disconnected;

		private int messageId;
		public virtual void Send (Message message)
		{
			message.Header = new MessageHeader();
			message.Header.MessageId = Interlocked.Increment (ref this.messageId);
			OnMessageSent (new MessageEventArgs (this, message));
		}

		private readonly Dictionary<int, TaskCompletionSource<Message>> responses = new Dictionary<int, TaskCompletionSource<Message>>();
		public virtual Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0) where TResponse : Message
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (timeout > 0)
				throw new NotSupportedException();

			var tcs = new TaskCompletionSource<TResponse>();
			var otcs = new TaskCompletionSource<Message>();
			otcs.Task.ContinueWith (t => tcs.SetResult ((TResponse)t.Result));

			int mid = Interlocked.Increment (ref this.messageId);
			lock (this.responses)
				this.responses.Add (mid, otcs);

			message.Header = new MessageHeader();
			message.Header.MessageId = mid;
			OnMessageSent (new MessageEventArgs (this, message));

			return tcs.Task;
		}

		public abstract void SendResponse (Message originalMessage, Message response);

		public IEnumerable<MessageEventArgs> Tick()
		{
			throw new NotSupportedException();
		}

		public void Disconnect()
		{
			Disconnect (true);
		}

		public void Disconnect (ConnectionResult reason, string customReason = null)
		{
			Disconnect (true, reason, customReason);
		}

		public void DisconnectAsync()
		{
			Disconnect (false);
		}

		public void DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			Disconnect (false, reason, customReason);
		}

		protected internal virtual void Disconnect (bool now, ConnectionResult reason = ConnectionResult.FailedUnknown, string customReason = null)
		{
			this.connected = false;

			var e = new DisconnectedEventArgs (this, reason, customReason);
			if (now)
				OnDisconnected (e);
			else
				ThreadPool.QueueUserWorkItem (s => OnDisconnected ((DisconnectedEventArgs)s), e);
		}

		internal void ReceiveResponse (MessageEventArgs e)
		{
			bool response = false;
			TaskCompletionSource<Message> tcs;
			lock (this.responses)
				response = this.responses.TryGetValue (e.Message.Header.MessageId, out tcs);

			if (response)
				tcs.SetResult (e.Message);

			Receive (e);
		}

		internal void Receive (MessageEventArgs e)
		{
			var context = new SerializationContext (new TypeMap());
			var writer = new BufferValueWriter (new byte[1024]);
			e.Message.WritePayload (context, writer);

			var reader = new BufferValueReader (writer.Buffer);
			var message = e.Message.Protocol.Create (e.Message.MessageType);
			message.ReadPayload (context, reader);
			message.Header = e.Message.Header;

			var tmessage = (e.Message as TempestMessage);
			if (tmessage == null)
				OnMessageReceived (new MessageEventArgs (e.Connection, message));
			else
				OnTempestMessageReceived (new MessageEventArgs (e.Connection, message));
		}

		protected virtual void OnTempestMessageReceived (MessageEventArgs e)
		{
			switch (e.Message.MessageType)
			{
				case (ushort)TempestMessageType.ReliablePing:
					Send (new ReliablePongMessage());
					break;

				case (ushort)TempestMessageType.ReliablePong:
					break;

				case (ushort)TempestMessageType.Disconnect:
					var msg = (DisconnectMessage)e.Message;
					Disconnect (true, msg.Reason, msg.CustomReason);
					break;
			}
		}

		protected void OnDisconnected (DisconnectedEventArgs e)
		{
			var handler = Disconnected;
			if (handler != null)
				handler (this, e);
		}

		protected void OnMessageReceived (MessageEventArgs e)
		{
			EventHandler<MessageEventArgs> handler = MessageReceived;
			if (handler != null)
				handler (this, e);
		}

		protected void OnMessageSent (MessageEventArgs e)
		{
			EventHandler<MessageEventArgs> handler = MessageSent;
			if (handler != null)
				handler (this, e);
		}
	}
}
//
// MockConnectionProvider.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012-2013 Eric Maupin
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
		
		public IEnumerable<Target> LocalTargets
		{
			get { return Enumerable.Empty<Target>(); }
		}

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
						connection.Disconnect (ConnectionResult.IncompatibleVersion);
						return;
					}
				}
			}

			var e = new ConnectionMadeEventArgs (c, null);
			OnConnectionMade (e);

			if (e.Rejected)
			{
				connection.Disconnect (ConnectionResult.ConnectionFailed);
				c.Disconnect (ConnectionResult.ConnectionFailed);
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

		public override Task<bool> SendAsync (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (!IsConnected)
			{
				TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
				tcs.SetResult (false);
				return tcs.Task;
			}

			Task<bool> task = base.SendAsync (message);
			connection.Receive (new MessageEventArgs (connection, message));
			return task;
		}

		public override Task<bool> SendResponseAsync (Message originalMessage, Message response)
		{
			var tcs = new TaskCompletionSource<bool>();

			response.Header = new MessageHeader();
			response.Header.MessageId = originalMessage.Header.MessageId;
			
			connection.ReceiveResponse (new MessageEventArgs (connection, response));

			tcs.SetResult (true);
			return tcs.Task;
		}

		protected internal override Task Disconnect (ConnectionResult reason = ConnectionResult.FailedUnknown, string customReason = null)
		{
			return base.Disconnect (reason, customReason).ContinueWith (t => {
				var c = Interlocked.Exchange (ref this.connection, null);
				if (c != null)
					c.Disconnect (reason, customReason).Wait();
			});
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

		public override Task<bool> SendAsync (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (!IsConnected)
			{
				TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
				tcs.SetResult (false);
				return tcs.Task;
			}
			
			Task<bool> task = base.SendAsync (message);
			connection.Receive (new MessageEventArgs (connection, message));
			return task;
		}

		public override Task<bool> SendResponseAsync (Message originalMessage, Message response)
		{
			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

			response.Header.MessageId = originalMessage.Header.MessageId;
			connection.ReceiveResponse (new MessageEventArgs (connection, response));
			tcs.SetResult (true);
			return tcs.Task;
		}

		internal MockServerConnection connection;

		protected internal override Task Disconnect (ConnectionResult reason = ConnectionResult.FailedUnknown, string customReason = null)
		{
			return base.Disconnect (reason, customReason).ContinueWith (t => {
				var c = Interlocked.Exchange (ref this.connection, null);
				if (c != null)
					c.Disconnect (reason, customReason).Wait();
			});
		}

		private void OnConnected (ClientConnectionEventArgs e)
		{
			EventHandler<ClientConnectionEventArgs> handler = Connected;
			if (handler != null)
				handler (this, e);
		}
	}

	public class MockAsymmetricKey
		: RSAAsymmetricKey
	{
		public void Serialize (ISerializationContext context, IValueWriter writer)
		{
		}

		public void Deserialize (ISerializationContext context, IValueReader reader)
		{
		}

		public byte[] PublicSignature
		{
			get { return new byte[0]; }
		}

		public void Serialize (IValueWriter writer, RSACrypto crypto, bool includePrivate)
		{
		}

		public void Deserialize (IValueReader reader, RSACrypto crypto)
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

		public RSAAsymmetricKey RemoteKey
		{
			get { return new MockAsymmetricKey(); }
		}

		private MockAsymmetricKey key = new MockAsymmetricKey();
		public RSAAsymmetricKey LocalKey
		{
			get { return this.key; }
		}

		public event EventHandler<MessageEventArgs> MessageReceived;
		public event EventHandler<DisconnectedEventArgs> Disconnected;

		private int messageId;
		public virtual Task<bool> SendAsync (Message message)
		{
			var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult (true);

			message.Header = new MessageHeader();
			message.Header.MessageId = Interlocked.Increment (ref this.messageId);

			return tcs.Task;
		}

		private readonly MessageResponseManager responses = new MessageResponseManager();

		public Task<Message> SendFor (Message message, int timeout = 0)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			Task<bool> sendTask = SendAsync (message);
			return this.responses.SendFor (message, sendTask, timeout);
		}

		public  Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0) where TResponse : Message
		{
			return SendFor (message, timeout).ContinueWith (t => (TResponse)t.Result, TaskScheduler.Default);
		}

		public abstract Task<bool> SendResponseAsync (Message originalMessage, Message response);

		public IEnumerable<MessageEventArgs> Tick()
		{
			throw new NotSupportedException();
		}

		public Task DisconnectAsync()
		{
			return DisconnectAsync (ConnectionResult.FailedUnknown);
		}

		public Task DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			return Disconnect (reason, customReason);
		}

		protected internal virtual Task Disconnect (ConnectionResult reason = ConnectionResult.FailedUnknown, string customReason = null)
		{
			this.connected = false;

			Cleanup();

			var e = new DisconnectedEventArgs (this, reason, customReason);
			return Task.Factory.StartNew (s => OnDisconnected ((DisconnectedEventArgs)s), e);
		}

		protected virtual void Cleanup()
		{
			this.responses.Clear();
		}

		internal void ReceiveResponse (MessageEventArgs e)
		{
			if (e.Message.Header.IsResponse)
				this.responses.Receive (e.Message);

			Receive (e);
		}

		internal void Receive (MessageEventArgs e)
		{
			var context = new SerializationContext ();
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
					SendAsync (new ReliablePongMessage());
					break;

				case (ushort)TempestMessageType.ReliablePong:
					break;

				case (ushort)TempestMessageType.Disconnect:
					var msg = (DisconnectMessage)e.Message;
					DisconnectAsync (msg.Reason, msg.CustomReason);
					break;
			}
		}

		protected internal void OnDisconnected (DisconnectedEventArgs e)
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
	}
}
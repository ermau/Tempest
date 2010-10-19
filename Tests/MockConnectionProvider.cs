//
// MockConnectionProvider.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010 Eric Maupin
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
using System.Text;
using System.Threading;

namespace Tempest.Tests
{
	public class MockConnectionProvider
		: IConnectionProvider
	{
		public event EventHandler<ConnectionMadeEventArgs> ConnectionMade;
		public event EventHandler<ConnectionlessMessageReceivedEventArgs> ConnectionlessMessageReceived
		{
			add { throw new NotSupportedException(); }
			remove { throw new NotSupportedException(); }
		}
		
		public bool SupportsConnectionless
		{
			get { return false; }
		}

		public void Start (MessageTypes types)
		{
			if (types.HasFlag (MessageTypes.Unreliable))
				throw new NotSupportedException();

			this.running = true;
		}

		public void SendConnectionlessMessage (Message message, EndPoint endPoint)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");

			throw new NotSupportedException();
		}

		public void Stop()
		{
			this.running = false;
		}

		public IClientConnection GetClientConnection()
		{
			return new MockClientConnection (this);
		}

		public void Dispose()
		{
		}

		private bool running;

		internal void Connect (MockClientConnection connection)
		{
			var c = new MockServerConnection (connection);
			connection.connection = c;
			OnConnectionMade (new ConnectionMadeEventArgs (c));
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
			this.connection = connection;
		}

		public override void Send (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			connection.Receive (new MessageReceivedEventArgs (connection, message));
		}

		public override void Disconnect(bool now)
		{
			if (connection == null)
				return;

			var c = connection;
			connection = null;
			c.Disconnect (now);

			base.Disconnect (now);
		}
	}

	public class MockClientConnection
		: MockConnection, IClientConnection
	{
		private MockConnectionProvider provider;

		public MockClientConnection (MockConnectionProvider provider)
		{
			if (provider == null)
				throw new ArgumentNullException ("provider");

			this.provider = provider;
		}

		public event EventHandler<ClientConnectionEventArgs> Connected;
		public event EventHandler<ClientConnectionEventArgs> ConnectionFailed;

		public void Connect (EndPoint endpoint, MessageTypes messageTypes)
		{
			if (endpoint == null)
				throw new ArgumentNullException ("endpoint");

			this.connected = true;
			provider.Connect (this);
			OnConnected (new ClientConnectionEventArgs (this));
		}

		public override void Send (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			
			connection.Receive (new MessageReceivedEventArgs (connection, message));
		}

		public override void Disconnect(bool now)
		{
			if (connection == null)
				return;

			var c = connection;
			connection = null;
			c.Disconnect (now);
			
			base.Disconnect (now);
		}

		internal MockServerConnection connection;

		private void OnConnected (ClientConnectionEventArgs e)
		{
			EventHandler<ClientConnectionEventArgs> handler = Connected;
			if (handler != null)
				handler (this, e);
		}

		private void OnConnectionFailed (ClientConnectionEventArgs e)
		{
			EventHandler<ClientConnectionEventArgs> handler = ConnectionFailed;
			if (handler != null)
				handler (this, e);
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

		public MessagingModes Modes
		{
			get { return MessagingModes.Async; }
		}

		public EndPoint RemoteEndPoint
		{
			get;
			private set;
		}

		public event EventHandler<MessageReceivedEventArgs> MessageReceived;
		public event EventHandler<ConnectionEventArgs> Disconnected;
		
		public abstract void Send (Message message);

		public IEnumerable<MessageReceivedEventArgs> Tick()
		{
			throw new NotSupportedException();
		}

		public virtual void Disconnect (bool now)
		{
			this.connected = false;

			OnDisconnected (new ConnectionEventArgs (this));
		}

		internal void Receive (MessageReceivedEventArgs e)
		{
			OnMessageReceived (e);
		}

		protected void OnDisconnected (ConnectionEventArgs e)
		{
			EventHandler<ConnectionEventArgs> handler = Disconnected;
			if (handler != null)
				handler (this, e);
		}

		protected void OnMessageReceived (MessageReceivedEventArgs e)
		{
			EventHandler<MessageReceivedEventArgs> handler = MessageReceived;
			if (handler != null)
				handler (this, e);
		}
	}
}
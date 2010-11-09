//
// ConnectionProviderTests.cs
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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace Tempest.Tests
{
	public abstract class ConnectionProviderTests
	{
		protected IConnectionProvider provider;
		protected static Protocol protocol;

		static ConnectionProviderTests()
		{
			protocol = ProtocolTests.GetTestProtocol();
			Message.Factory.Register (protocol, new[] { typeof(MockMessage) });
		}

		[SetUp]
		protected void Setup()
		{
			this.provider = SetUp();
		}

		[TearDown]
		protected void TearDown()
		{
			if (this.provider != null)
				this.provider.Dispose();
		}

		protected abstract EndPoint EndPoint { get; }
		protected abstract MessageTypes MessageTypes { get; }

		protected abstract IConnectionProvider SetUp();
		protected abstract IClientConnection GetNewClientConnection();

		[Test]
		public void ConnectionlessSupport()
		{
			EventHandler<ConnectionlessMessageReceivedEventArgs> cmr = (sender, e) => { };

			if (this.provider.SupportsConnectionless)
			{
				Assert.DoesNotThrow (() => this.provider.ConnectionlessMessageReceived += cmr);
				Assert.DoesNotThrow (() => this.provider.ConnectionlessMessageReceived -= cmr);
				Assert.DoesNotThrow (() => this.provider.SendConnectionlessMessage (new MockMessage(), new IPEndPoint (IPAddress.Loopback, 42)));
			}
			else
			{
				Assert.Throws<NotSupportedException> (() => this.provider.ConnectionlessMessageReceived += cmr);
				Assert.Throws<NotSupportedException> (() => this.provider.SendConnectionlessMessage (new MockMessage(), new IPEndPoint (IPAddress.Loopback, 42)));
				Assert.Throws<NotSupportedException> (() => this.provider.Start (MessageTypes.Unreliable));
			}
		}

		[Test]
		public void SendConnectionlessMessageNull()
		{
			Assert.Throws<ArgumentNullException> (() => this.provider.SendConnectionlessMessage (null, new IPEndPoint (IPAddress.Loopback, 42)));
			Assert.Throws<ArgumentNullException> (() => this.provider.SendConnectionlessMessage (new MockMessage(), null));
		}

		[Test]
		public void StartRepeatedly()
		{
			// *knock knock knock* Penny
			Assert.DoesNotThrow (() => this.provider.Start (MessageTypes));
			// *knock knock knock* Penny
			Assert.DoesNotThrow (() => this.provider.Start (MessageTypes));
			// *knock knock knock* Penny
			Assert.DoesNotThrow (() => this.provider.Start (MessageTypes));
		}

		[Test]
		public void StopRepeatedly()
		{
			// *knock knock knock* Sheldon
			Assert.DoesNotThrow (() => this.provider.Stop());
			// *knock knock knock* Sheldon
			Assert.DoesNotThrow (() => this.provider.Stop());
			// *knock knock knock* Sheldon
			Assert.DoesNotThrow (() => this.provider.Stop());
		}

		[Test]
		public void ConnectionMade()
		{
			this.provider.Start (MessageTypes);

			var test = new AsyncTest();
			this.provider.ConnectionMade += test.PassHandler;
			var c = GetNewClientConnection();
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void Connected()
		{
			this.provider.Start (MessageTypes);

			var c = GetNewClientConnection();

			var test = new AsyncTest();
			c.Connected += test.PassHandler;
			c.ConnectionFailed += test.FailHandler;
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void InlineSupport()
		{
			var c = GetNewClientConnection();
			
			if ((c.Modes & MessagingModes.Inline) == MessagingModes.Inline)
			{
				Assert.DoesNotThrow (() => c.Tick());
			}
			else
			{
				Assert.Throws<NotSupportedException> (() => c.Tick());
			}
		}

		[Test]
		public void ClientSendMessageInline()
		{
			var c = GetNewClientConnection();
			if ((c.Modes & MessagingModes.Inline) != MessagingModes.Inline)
				Assert.Ignore();

			throw new NotImplementedException();
		}

		[Test]
		public void ServerSendMessageInline()
		{
			var c = GetNewClientConnection();
			if ((c.Modes & MessagingModes.Inline) != MessagingModes.Inline)
				Assert.Ignore();

			throw new NotImplementedException();
		}

		[Test]
		public void ClientSendMessageAsync()
		{
			const string content = "Oh, hello there.";

			var c = GetNewClientConnection();
			if ((c.Modes & MessagingModes.Async) != MessagingModes.Async)
				Assert.Ignore();

			IServerConnection connection = null;

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (me.Connection, connection);

				var msg = (me.Message as MockMessage);
				Assert.IsNotNull (msg);
				Assert.AreEqual (content, msg.Content);
			});

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) =>
			{
				connection = e.Connection;
				e.Connection.MessageReceived += test.PassHandler;
			};
			
			c.Connected += (sender, e) => c.Send (new MockMessage { Content = content });
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void ClientMessageSent()
		{
			const string content = "Oh, hello there.";

			var c = GetNewClientConnection();
			if ((c.Modes & MessagingModes.Async) != MessagingModes.Async)
				Assert.Ignore();
			
			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (me.Connection, c);

				var msg = (me.Message as MockMessage);
				Assert.IsNotNull (msg);
				Assert.AreEqual (content, msg.Content);
			});

			this.provider.Start (MessageTypes);

			c.MessageSent += test.PassHandler;
			c.Connected += (sender, e) => c.Send (new MockMessage { Content = content });
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void ServerSendMessageAsync()
		{
			const string content = "Oh, hello there.";

			var c = GetNewClientConnection();
			if ((c.Modes & MessagingModes.Async) != MessagingModes.Async)
				Assert.Ignore();

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (c, me.Connection);

				var msg = (me.Message as MockMessage);
				Assert.IsNotNull (msg);
				Assert.AreEqual (content, msg.Content);
			});

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) => e.Connection.Send (new MockMessage { Content = content });

			c.ConnectionFailed += test.FailHandler;
			c.MessageReceived += test.PassHandler;
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void SendLongMessageAsync()
		{
			StringBuilder contentBuilder = new StringBuilder();
			Random r = new Random (42);
			for (int i = 0; i < 20480; ++i)
				contentBuilder.Append ((char)r.Next (0, 128));

			string content = contentBuilder.ToString();

			var c = GetNewClientConnection();
			if ((c.Modes & MessagingModes.Async) != MessagingModes.Async)
				Assert.Ignore();

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (c, me.Connection);

				var msg = (me.Message as MockMessage);
				Assert.IsNotNull (msg);
				Assert.AreEqual (content, msg.Content);
			});

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) => e.Connection.Send (new MockMessage { Content = content });

			c.ConnectionFailed += test.FailHandler;
			c.MessageReceived += test.PassHandler;
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000000);
		}

		[Test]
		public void DisconnectFromClientOnClient()
		{
			this.provider.Start (MessageTypes);

			var test = new AsyncTest();
			var c = GetNewClientConnection();

			c.Disconnected += test.PassHandler;
			c.Connected += (sender, e) => c.Disconnect (true);
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void DisconnectFromClientOnServer()
		{
			var test = new AsyncTest();

			var c = GetNewClientConnection();

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) =>
			{
				e.Connection.Disconnected += test.PassHandler;
				c.Disconnect (true);
			};
			
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void DisconnectFromServerOnClient()
		{
			var test = new AsyncTest();

			var c = GetNewClientConnection();
			c.Disconnected += test.PassHandler;

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) => e.Connection.Disconnect (true);
			
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void DisconnectFromServerOnServer()
		{
			var test = new AsyncTest();

			var c = GetNewClientConnection();

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) =>
			{
				e.Connection.Disconnected += test.PassHandler;
				e.Connection.Disconnect (true);
			};
			
			c.Connect (EndPoint, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void DisconnectAndReconnect()
		{
			AutoResetEvent wait = new AutoResetEvent (false);

			var c = GetNewClientConnection();
			c.Connected += (sender, e) => wait.Set();
			c.Disconnected += (sender, e) => wait.Set();

			this.provider.Start (MessageTypes);

			for (int i = 0; i < 5; ++i)
			{
				c.Connect (EndPoint, MessageTypes);
				if (!wait.WaitOne (10000))
					Assert.Fail ("Failed to connect.");

				c.Disconnect (true);
				if (!wait.WaitOne (10000))
					Assert.Fail ("Failed to disconnect.");
			}
		}
	}
}
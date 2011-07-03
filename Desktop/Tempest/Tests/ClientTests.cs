//
// ClientTests.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011 Eric Maupin
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
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class ClientTests
	{
		private static Protocol protocol;
		static ClientTests()
		{
			protocol = ProtocolTests.GetTestProtocol();
		}

		private MockClient client;
		private MockConnectionProvider provider;
		private MockClientConnection connection;

		[SetUp]
		public void Setup()
		{
			provider = new MockConnectionProvider (protocol);
			provider.Start (MessageTypes.Reliable);

			connection = new MockClientConnection (provider);
			client = new MockClient (connection, false);
		}

		[Test]
		public void CtorNull()
		{
			Assert.Throws<ArgumentNullException> (() => new MockClient (null, true));
		}

		[Test]
		public void ConnectNull()
		{
			Assert.Throws<ArgumentNullException> (() => client.ConnectAsync (null));
		}

		[Test, Repeat (3)]
		public void Connected()
		{
			var test = new AsyncTest();

			client.Connected += test.PassHandler;
			client.Disconnected += test.FailHandler;

			client.ConnectAsync (new IPEndPoint (IPAddress.Any, 0));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void Disconnect()
		{
			var test = new AsyncTest<ClientDisconnectedEventArgs> (e =>
			{
				Assert.AreEqual (ConnectionResult.Custom, e.Reason);
				Assert.IsTrue (e.Requested);
			});

			client.Connected += (sender, e) => client.Disconnect (true);
			client.Disconnected += test.PassHandler;

			client.ConnectAsync (new IPEndPoint (IPAddress.Any, 0));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectWithReason()
		{
			var test = new AsyncTest<ClientDisconnectedEventArgs> (e =>
			{
				Assert.AreEqual ("reason", e.CustomReason);
				Assert.AreEqual (ConnectionResult.Custom, e.Reason);
				Assert.IsTrue (e.Requested);
			});

			client.Connected += (sender, e) => client.DisconnectWithReason ("reason");
			client.Disconnected += test.PassHandler;

			client.ConnectAsync (new IPEndPoint (IPAddress.Any, 0));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectFromHandlerThread()
		{
			var test = new AsyncTest();

			Action<MessageEventArgs<MockMessage>> handler = e =>
			{
				client.Disconnect (true);
				test.PassHandler (null, EventArgs.Empty);
			};

			client.RegisterMessageHandler (handler);
			client.ConnectAsync (new IPEndPoint (IPAddress.Any, 0));
			connection.Receive (new MessageEventArgs (connection, new MockMessage { Content = "hi" }));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectedFromConnection()
		{
			var test = new AsyncTest<ClientDisconnectedEventArgs> (e =>
			{
				Assert.IsFalse (e.Requested);
				Assert.AreEqual (ConnectionResult.EncryptionMismatch, e.Reason);
			});

			client.Connected += (s, e) => connection.Disconnect (true, ConnectionResult.EncryptionMismatch);
			client.Disconnected += test.PassHandler;

			client.ConnectAsync (new IPEndPoint (IPAddress.Any, 0));

			test.Assert (1000);
		}

		[Test, Repeat (3)]
		public void MessageHandling()
		{
			var test = new AsyncTest (e =>
			{
				var me = (MessageEventArgs)e;
				Assert.AreSame (connection, me.Connection);
				Assert.IsInstanceOf (typeof(MockMessage), me.Message);
				Assert.AreEqual ("hi", ((MockMessage)me.Message).Content);
			});

			Action<MessageEventArgs> handler = e => test.PassHandler (test, e);

			((IContext)client).RegisterMessageHandler (MockProtocol.Instance, 1, handler);
			client.ConnectAsync (new IPEndPoint (IPAddress.Any, 0));
			connection.Receive (new MessageEventArgs (connection, new MockMessage { Content = "hi" }));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void GenericMessageHandling()
		{
			var test = new AsyncTest (e =>
			{
				var me = (MessageEventArgs<MockMessage>)e;
				Assert.AreSame (connection, me.Connection);
				Assert.AreEqual ("hi", me.Message.Content);
			});

			Action<MessageEventArgs<MockMessage>> handler = e => test.PassHandler (test, e);

			client.RegisterMessageHandler (handler);

			client.ConnectAsync (new IPEndPoint (IPAddress.Any, 0));
			connection.Receive (new MessageEventArgs (connection, new MockMessage { Content = "hi" }));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void MultiProtocolMessageHandling()
		{
			var test = new AsyncTest (e =>
			{
				var me = (MessageEventArgs)e;
				Assert.AreSame (connection, me.Connection);
				Assert.IsInstanceOf (typeof(MockMessage2), me.Message);
				Assert.AreEqual ("hi", ((MockMessage2)me.Message).Content);
			});

			Action<MessageEventArgs> handler = e => test.PassHandler (test, e);

			((IContext)client).RegisterMessageHandler (MockProtocol.Instance, 1, handler);
			((IContext)client).RegisterMessageHandler (MockProtocol2.Instance, 1, handler);

			client.ConnectAsync (new IPEndPoint (IPAddress.Any, 0));
			connection.Receive (new MessageEventArgs (connection, new MockMessage2 { Content = "hi" }));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void MultiProtocolGenericMessageHandling()
		{
			var test = new AsyncTest (e =>
			{
				var me = (MessageEventArgs<MockMessage2>)e;
				Assert.AreSame (connection, me.Connection);
				Assert.AreEqual ("hi", me.Message.Content);
			});

			Action<MessageEventArgs<MockMessage2>> handler = e => test.PassHandler (test, e);

			client.RegisterMessageHandler (handler);
			client.RegisterMessageHandler (handler);

			client.ConnectAsync (new IPEndPoint (IPAddress.Any, 0));
			connection.Receive (new MessageEventArgs (connection, new MockMessage2 { Content = "hi" }));

			test.Assert (10000);
		}
	}
}
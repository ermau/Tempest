//
// TempestClientTests.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012-2013 Eric Maupin
// Copyright © 2017 Microsoft
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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class TempestClientTests
		: ContextTests
	{
		private static readonly Protocol protocol = MockProtocol.Instance;

		private TempestClient client;
		private MockConnectionProvider provider;
		private MockClientConnection connection;

		protected override IContext Client
		{
			get { return this.client; }
		}

		[SetUp]
		public void Setup()
		{
			provider = new MockConnectionProvider (protocol);
			provider.Start (MessageTypes.Reliable);

			connection = new MockClientConnection (provider);
			client = new TempestClient (connection, MessageTypes.All);
		}

		[Test]
		public void CtorInvalid()
		{
			Assert.Throws<ArgumentNullException> (() => new TempestClient (null, MessageTypes.All));
			Assert.Throws<ArgumentException> (() => new TempestClient (connection, (MessageTypes)9999));
		}

		[Test]
		public void ConnectNull()
		{
			Assert.Throws<ArgumentNullException> (() => client.ConnectAsync (null));
		}

		[Test, Repeat (3)]
		public void Connected()
		{
			var test = new AsyncTest ();

			client.Connected += test.PassHandler;
			client.Disconnected += test.FailHandler;

			client.ConnectAsync (new Target (Target.AnyIP, 0));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void IsConnected()
		{
			var test = new AsyncTest (passTimes: 2);

			bool first = true;

			client.PropertyChanged += (o, e) => {
				if (e.PropertyName != "IsConnected")
					return;

				if (first) {
					Assert.IsTrue (client.IsConnected);
					first = false;

					client.DisconnectAsync();
				} else
					Assert.IsFalse (client.IsConnected);

				test.PassHandler (null, EventArgs.Empty);
			};

			client.ConnectAsync (new Target (Target.AnyIP, 0));

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

			client.Connected += (sender, e) => client.DisconnectAsync();
			client.Disconnected += test.PassHandler;

			client.ConnectAsync (new Target (Target.AnyIP, 0));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectWhileReceiving()
		{
			var test = new AsyncTest<ClientDisconnectedEventArgs> (e =>
			{
				Assert.AreEqual (ConnectionResult.Custom, e.Reason);
				Assert.IsTrue (e.Requested);
			});

			bool send = true;
			client.Connected += (sender, e) =>
			{
				new Thread (() =>
				{
					MockMessage m = new MockMessage { Content = "asdf" };
					for (int i = 0; i < 10000 && send; ++i)
						connection.Receive (new MessageEventArgs (connection, m));
				}).Start();
				
				Thread.Sleep (50);
				client.DisconnectAsync();
			};

			client.Disconnected += test.PassHandler;

			client.ConnectAsync (new Target (Target.AnyIP, 0));

			test.Assert (10000);
			send = false;
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

			client.Connected += (sender, e) => client.DisconnectAsync (ConnectionResult.Custom, "reason");
			client.Disconnected += test.PassHandler;

			client.ConnectAsync (new Target (Target.AnyIP, 0));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectFromHandlerThread()
		{
			var test = new AsyncTest();

			Action<MessageEventArgs<MockMessage>> handler = e => {
				client.DisconnectAsync().ContinueWith (t => {
					test.PassHandler (null, EventArgs.Empty);
				});
			};

			client.RegisterMessageHandler (handler);
			client.ConnectAsync (new Target (Target.AnyIP, 0));
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

			client.Connected += (s, e) => connection.Disconnect (ConnectionResult.EncryptionMismatch);
			client.Disconnected += test.PassHandler;

			client.ConnectAsync (new Target (Target.AnyIP, 0));

			test.Assert (1000);
		}

		[Test, Repeat (3)]
		public void MessageHandling()
		{
			var test = new AsyncTest (e =>
			{
				var me = (MessageEventArgs)e;
				Assert.AreSame (connection, me.Connection);
				Assert.IsTrue (me.Message is MockMessage);
				Assert.AreEqual ("hi", ((MockMessage)me.Message).Content);
			});

			Action<MessageEventArgs> handler = e => test.PassHandler (test, e);

			((IContext)client).RegisterMessageHandler (MockProtocol.Instance, 1, handler);
			client.ConnectAsync (new Target (Target.AnyIP, 0));
			connection.Receive (new MessageEventArgs (connection, new MockMessage { Content = "hi" }));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void LockedMessageHandling()
		{
			var test = new AsyncTest (e =>
			{
				var me = (MessageEventArgs)e;
				Assert.AreSame (connection, me.Connection);
				Assert.IsTrue (me.Message is MockMessage);
				Assert.AreEqual ("hi", ((MockMessage)me.Message).Content);
			});

			Action<MessageEventArgs> handler = e => test.PassHandler (test, e);

			((IContext)client).RegisterMessageHandler (MockProtocol.Instance, 1, handler);
			((IContext)client).LockHandlers();

			client.ConnectAsync (new Target (Target.AnyIP, 0));
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

			client.ConnectAsync (new Target (Target.AnyIP, 0));
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
				Assert.IsTrue (me.Message is MockMessage2);
				Assert.AreEqual ("hi", ((MockMessage2)me.Message).Content);
			});

			Action<MessageEventArgs> handler = e => test.PassHandler (test, e);

			((IContext)client).RegisterMessageHandler (MockProtocol.Instance, 1, handler);
			((IContext)client).RegisterMessageHandler (MockProtocol2.Instance, 1, handler);

			client.ConnectAsync (new Target (Target.AnyIP, 0));
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

			client.ConnectAsync (new Target (Target.AnyIP, 0));
			connection.Receive (new MessageEventArgs (connection, new MockMessage2 { Content = "hi" }));

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public async Task ProcessesMessagesAfterInternalReconnect ()
		{
			var target = new Target (Target.AnyIP, 0);

			var result = await this.client.ConnectAsync (target);
			Assume.That (result.Result, Is.EqualTo (ConnectionResult.Success));

			await this.connection.DisconnectAsync ();
			Assume.That (this.client.IsConnected, Is.False);

			await this.connection.ConnectAsync (target, MessageTypes.All);
			Assume.That (this.client.IsConnected, Is.True);

			const string content = "hi";

			var test = new AsyncTest (e => {
				var me = (MessageEventArgs<MockMessage>) e;
				Assert.AreSame (this.connection, me.Connection);
				Assert.AreEqual (content, me.Message.Content);
			});

			bool received = false;
			Action<MessageEventArgs<MockMessage>> handler = e => test.PassHandler (test, e);
			this.client.RegisterMessageHandler (handler);

			this.connection.Receive (new MessageEventArgs (this.connection, new MockMessage {
				Content = content
			}));

			test.Assert (10000);
		}
	}
}
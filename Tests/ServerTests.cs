//
// ServerTests.cs
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
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class ServerTests
	{
		private static readonly Protocol protocol;
		static ServerTests()
		{
			protocol = ProtocolTests.GetTestProtocol();
		}

		private MockServer server;
		private MockConnectionProvider provider;

		[SetUp]
		public void Setup()
		{
			provider = new MockConnectionProvider();
			server = new MockServer (provider, MessageTypes.Reliable);
		}

		[Test]
		public void ProviderCtorNull()
		{
			Assert.Throws<ArgumentNullException> (() => new MockServer (null, MessageTypes.Reliable));
		}

		[Test]
		public void AddConnectionProviderNull()
		{
			Assert.Throws<ArgumentNullException> (() => server.AddConnectionProvider (null));
		}

		[Test]
		public void RemoveConnectionProviderNull()
		{
			Assert.Throws<ArgumentNullException> (() => server.RemoveConnectionProvider (null));
		}

		[Test]
		public void AddConnectionProvider()
		{
			var p = new MockConnectionProvider();
			Assert.IsFalse (p.IsRunning);

			server.AddConnectionProvider (p);
			Assert.IsTrue (p.IsRunning);
		}

		[Test]
		public void RemoveConnectionProvider()
		{
			Assert.IsTrue (provider.IsRunning);

			server.RemoveConnectionProvider (provider);
			Assert.IsFalse (provider.IsRunning);
		}

		[Test]
		public void RemoveNotAddedConnectionProvider()
		{
			var p = new MockConnectionProvider();
			p.Start (MessageTypes.Reliable);
			Assert.IsTrue (p.IsRunning);

			server.RemoveConnectionProvider (p);
			Assert.IsTrue (p.IsRunning);
		}

		[Test]
		public void MessageHandling()
		{
			IServerConnection connection = null;

			var test = new AsyncTest(e =>
			{
				var me = (MessageEventArgs)e;
				Assert.AreSame (connection, me.Connection);
				Assert.IsInstanceOf (typeof(MockMessage), me.Message);
				Assert.AreEqual ("hi", ((MockMessage)me.Message).Content);
			});

			Action<MessageEventArgs> handler = e => test.PassHandler (test, e);
			((IContext)server).RegisterMessageHandler (1, handler);
			
			provider.ConnectionMade += (sender, e) => connection = e.Connection;
			
			var c = provider.GetClientConnection();
			c.Connect (new IPEndPoint (IPAddress.Any, 0), MessageTypes.Reliable);
			c.Send (new MockMessage () { Content = "hi" });

			test.Assert (10000);
		}
	}
}

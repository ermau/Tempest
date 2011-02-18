//
// NetworkProviderTests.cs
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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using Tempest.Providers.Network;

namespace Tempest.Tests
{
	[TestFixture]
	public class NetworkProviderTests
		: ConnectionProviderTests
	{
		private Protocol p = MockProtocol.Instance;
		private const int MaxConnections = 5;
		protected override EndPoint EndPoint
		{
			get { return new IPEndPoint (IPAddress.Loopback, 42000); }
		}

		protected override MessageTypes MessageTypes
		{
			get { return MessageTypes.Reliable; }
		}

		protected override IConnectionProvider SetUp()
		{
			return new NetworkConnectionProvider (p, new IPEndPoint (IPAddress.Any, 42000), MaxConnections);
		}

		protected override IClientConnection SetupClientConnection ()
		{
			return new NetworkClientConnection (p);
		}

		[Test]
		public void CtorInvalid()
		{
			Assert.Throws<ArgumentNullException> (() => new NetworkConnectionProvider ((Protocol)null, new IPEndPoint (IPAddress.Any, 42000), 20));
			Assert.Throws<ArgumentNullException> (() => new NetworkConnectionProvider ((IEnumerable<Protocol>)null, new IPEndPoint (IPAddress.Any, 42000), 20));
			Assert.Throws<ArgumentNullException> (() => new NetworkConnectionProvider (p, null, 20));
			Assert.Throws<ArgumentNullException> (() => new NetworkConnectionProvider (new [] { p }, null, 20));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NetworkConnectionProvider (p, new IPEndPoint (IPAddress.Any, 42000), 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NetworkConnectionProvider (new [] { p }, new IPEndPoint (IPAddress.Any, 42000), 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NetworkConnectionProvider (p, new IPEndPoint (IPAddress.Any, 42000), -1));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NetworkConnectionProvider (new [] { p }, new IPEndPoint (IPAddress.Any, 42000), -1));
		}

		[Test]
		public void ConnectionLimit()
		{
			provider.Start (MessageTypes);

			AutoResetEvent wait = new AutoResetEvent (false);
			int counter = 0;
			provider.ConnectionMade += (s, e) =>
			{
				if (Interlocked.Increment (ref counter) == MaxConnections)
					wait.Set();
			};

			IClientConnection client;
			AsyncTest test;
			for (int i = 0; i < MaxConnections; ++i)
			{
				test  = new AsyncTest (true);

				client = GetNewClientConnection();
				client.Connected += test.PassHandler;
				client.Disconnected += test.FailHandler;
				client.Connect (EndPoint, MessageTypes);

				test.Assert (3000);
			}

			if (!wait.WaitOne (10000))
				Assert.Fail ("MaxConnections was not reached in time");

			test = new AsyncTest();
			provider.ConnectionMade += test.FailHandler;
			client = GetNewClientConnection();
			client.Connect (EndPoint, MessageTypes);

			test.Assert (3000, false);
		}

		[Test]
		public void ConnectionLimitRestartListening()
		{
			IServerConnection connection = null;
			provider.ConnectionMade += (sender, e) =>
			{
				if (connection == null)
					connection = e.Connection;
			};

			ConnectionLimit();

			Assert.IsNotNull (connection);

			connection.Disconnect (true);

			AsyncTest test  = new AsyncTest();
			provider.ConnectionMade += test.PassHandler;

			IClientConnection client = GetNewClientConnection();
			client.Disconnected += test.FailHandler;
			client.Connect (EndPoint, MessageTypes);

			test.Assert (3000);
		}

		[Test]
		public void PingPong()
		{
			AsyncTest test = new AsyncTest();

			IServerConnection connection = null;
			provider.ConnectionMade += (sender, e) =>
			{
				if (connection == null)
					connection = e.Connection;
			};

			provider.Start (MessageTypes);

			((NetworkConnectionProvider)provider).PingFrequency = 1000;
			var client = GetNewClientConnection();
			client.Disconnected += test.FailHandler;
			client.Connect (EndPoint, MessageTypes);

			test.Assert (30000, false);
			Assert.IsNotNull (connection);
			Assert.IsTrue (connection.IsConnected);
			Assert.IsTrue (client.IsConnected);
		}
	}
}
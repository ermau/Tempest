//
// NetworkProviderTests.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011-2012 Eric Maupin
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
using System.Security.Cryptography;
using System.Threading;
using NUnit.Framework;
using Tempest.Providers.Network;

namespace Tempest.Tests
{
	[TestFixture]
	public class NetworkProviderTests
		: ConnectionProviderTests
	{
		private static RSAAsymmetricKey key;
		static NetworkProviderTests()
		{
			var rsa = new RSACrypto();
			key = rsa.ExportKey (true);
		}

		private Protocol p = MockProtocol.Instance;
		private const int MaxConnections = 5;
		protected override Target Target
		{
			get { return new Target (Target.LoopbackIP, 42000); }
		}

		protected override MessageTypes MessageTypes
		{
			get { return MessageTypes.Reliable; }
		}

		protected override IConnectionProvider SetUp()
		{
			return new NetworkConnectionProvider (new[] { p }, new Target (Target.AnyIP, 42000), MaxConnections, key) { PingFrequency = 2000 };
		}

		protected override IConnectionProvider SetUp (IEnumerable<Protocol> protocols)
		{
			return new NetworkConnectionProvider (protocols, new Target (Target.AnyIP, 42000), MaxConnections, key) { PingFrequency = 2000 };
		}

		protected override IClientConnection SetupClientConnection ()
		{
			return new NetworkClientConnection (p, key);
		}

		protected override IClientConnection SetupClientConnection (out RSAAsymmetricKey k)
		{
			var c = new NetworkClientConnection (p);
			k = (RSAAsymmetricKey) c.LocalKey;

			return c;
		}

		protected override IClientConnection SetupClientConnection (IEnumerable<Protocol> protocols)
		{
			return new NetworkClientConnection (protocols);
		}

		[Test]
		public void CtorInvalid()
		{
			Assert.Throws<ArgumentNullException> (() => new NetworkConnectionProvider ((Protocol)null, new Target (Target.AnyIP, 42000), 20));
			Assert.Throws<ArgumentNullException> (() => new NetworkConnectionProvider ((IEnumerable<Protocol>)null, new Target (Target.AnyIP, 42000), 20));
			Assert.Throws<ArgumentNullException> (() => new NetworkConnectionProvider (p, null, 20));
			Assert.Throws<ArgumentNullException> (() => new NetworkConnectionProvider (new [] { p }, null, 20));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NetworkConnectionProvider (p, new Target (Target.AnyIP, 42000), 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NetworkConnectionProvider (new [] { p }, new Target (Target.AnyIP, 42000), 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NetworkConnectionProvider (p, new Target (Target.AnyIP, 42000), -1));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NetworkConnectionProvider (new [] { p }, new Target (Target.AnyIP, 42000), -1));
		}
		
		[Test, Repeat (3)]
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
				test  = new AsyncTest();

				client = GetNewClientConnection();
				client.Connected += test.PassHandler;
				client.Disconnected += test.FailHandler;
				client.ConnectAsync (Target, MessageTypes);

				test.Assert (10000);
			}

			if (!wait.WaitOne (30000))
				Assert.Fail ("MaxConnections was not reached in time");

			test = new AsyncTest();
			provider.ConnectionMade += test.FailHandler;
			client = GetNewClientConnection();
			client.Disconnected += test.PassHandler;
			client.ConnectAsync (Target, MessageTypes);

			test.Assert (10000, false);
		}

		[Test, Repeat (3)]
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

			connection.DisconnectAsync();

			AsyncTest test  = new AsyncTest();
			provider.ConnectionMade += test.PassHandler;

			IClientConnection client = GetNewClientConnection();
			client.Disconnected += test.FailHandler;
			client.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
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
			client.ConnectAsync (Target, MessageTypes);

			test.Assert (30000, false);
			Assert.IsNotNull (connection);
			Assert.IsTrue (connection.IsConnected);
			Assert.IsTrue (client.IsConnected);
		}

		//[Test, Repeat (3)]
		//public void RejectSha1()
		//{
		//	TearDown();

		//	provider = new NetworkConnectionProvider (new [] { MockProtocol.Instance }, Target, MaxConnections, new string[] { "SHA256" } );
		//	provider.Start (MessageTypes);

		//	var test = new AsyncTest<DisconnectedEventArgs> (d => Assert.AreEqual (ConnectionResult.FailedHandshake, d.Result));

		//	var client = new NetworkClientConnection (new[] { MockProtocol.Instance });
		//	client.ConnectAsync (Target, MessageTypes);

		//	client.Connected += test.FailHandler;
		//	client.Disconnected += test.PassHandler;

		//	test.Assert (10000);
		//}
		
		//[Test, Repeat (3)]
		//public void SuppliedServerKey()
		//{
		//    TearDown();

		//    var crypto = new RSACryptoServiceProvider();
		//    byte[] csp = crypto.ExportCspBlob (true);
		//    var key = new RSAAsymmetricKey (csp);

		//    provider = new NetworkConnectionProvider (new [] { MockProtocol.Instance }, (IPEndPoint) EndPoint, MaxConnections, () => new RSACrypto(), key);
		//    provider.Start (MessageTypes);

		//    var c = new NetworkClientConnection (MockProtocol.Instance);

		//    var test = new AsyncTest (2);

		//    provider.ConnectionMade += test.PassHandler;
		//    c.Connected += test.PassHandler;

		//    c.ConnectAsync (EndPoint, MessageTypes);

		//    test.Assert (10000);
		//}

		//[Test, Repeat (3)]
		//public void SuppliedClientKey()
		//{
		//    provider.Start (MessageTypes);

		//    var crypto = new RSACryptoServiceProvider();
		//    byte[] csp = crypto.ExportCspBlob (true);
		//    var key = new RSAAsymmetricKey (csp);

		//    var c = new NetworkClientConnection (MockProtocol.Instance, () => new RSACrypto(), key);

		//    var test = new AsyncTest<ConnectionMadeEventArgs> (e => Assert.IsTrue (e.ClientPublicKey.PublicSignature.SequenceEqual (key.PublicSignature)));
		//    provider.ConnectionMade += test.PassHandler;

		//    c.ConnectAsync (EndPoint, MessageTypes);

		//    test.Assert (10000);
		//}

		//[Test, Repeat (3)]
		//public void SuppliedServer4096Key()
		//{
		//    TearDown();

		//    var crypto = new RSACryptoServiceProvider (4096);
		//    byte[] csp = crypto.ExportCspBlob (true);
		//    var key = new RSAAsymmetricKey (csp);

		//    provider = new NetworkConnectionProvider (new [] { MockProtocol.Instance }, (IPEndPoint) EndPoint, MaxConnections, () => new RSACrypto(), key);
		//    provider.Start (MessageTypes);

		//    var c = new NetworkClientConnection (MockProtocol.Instance);

		//    var test = new AsyncTest (2);

		//    provider.ConnectionMade += test.PassHandler;
		//    c.Connected += test.PassHandler;

		//    c.ConnectAsync (EndPoint, MessageTypes);

		//    test.Assert (10000);
		//}

		//[Test, Repeat (3)]
		//public void SuppliedClient4096Key()
		//{
		//    provider.Start (MessageTypes);

		//    var crypto = new RSACryptoServiceProvider (4096);
		//    byte[] csp = crypto.ExportCspBlob (true);
		//    var key = new RSAAsymmetricKey (csp);

		//    var c = new NetworkClientConnection (MockProtocol.Instance, () => new RSACrypto(), key);

		//    var test = new AsyncTest<ConnectionMadeEventArgs> (e => Assert.IsTrue (e.ClientPublicKey.PublicSignature.SequenceEqual (key.PublicSignature)));
		//    provider.ConnectionMade += test.PassHandler;

		//    c.ConnectAsync (EndPoint, MessageTypes);

		//    test.Assert (10000);
		//}

		private class MockSha1OnlyCrypto
			: RSACrypto
		{
			IEnumerable<string> SupportedHashAlgs
			{
				get { return new[] { "SHA1" }; }
			}
		}
	}
}
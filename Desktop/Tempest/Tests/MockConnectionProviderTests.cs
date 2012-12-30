//
// MockConnectionProviderTests.cs
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

using System.Collections.Generic;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class MockConnectionProviderTests
		: ConnectionProviderTests
	{
		private Protocol p = MockProtocol.Instance;

		protected override Target Target
		{
			get { return new Target (Target.AnyIP, 0); }
		}

		protected override MessageTypes MessageTypes
		{
			get { return MessageTypes.Reliable; }
		}

		protected override IConnectionProvider SetUp()
		{
			return new MockConnectionProvider (p);
		}

		protected override IConnectionProvider SetUp (IEnumerable<Protocol> protocols)
		{
			return new MockConnectionProvider (protocols);
		}

		protected override IClientConnection SetupClientConnection()
		{
			return ((MockConnectionProvider)this.provider).GetClientConnection();
		}

		protected override IClientConnection SetupClientConnection (IEnumerable<Protocol> protocols)
		{
			return ((MockConnectionProvider)this.provider).GetClientConnection (protocols);
		}

		[Test]
		public void ServerConnectionConnected()
		{
			var provider = new MockConnectionProvider (MockProtocol.Instance);
			provider.Start (MessageTypes.Reliable);

			var test = new AsyncTest<ConnectionMadeEventArgs> (e => Assert.AreEqual (true, e.Connection.IsConnected));
			provider.ConnectionMade += test.PassHandler;

			var client = provider.GetClientConnection (MockProtocol.Instance);
			client.ConnectAsync (new Target (Target.AnyIP, 0), MessageTypes.Reliable);
			
			test.Assert (5000);
		}
	}
}
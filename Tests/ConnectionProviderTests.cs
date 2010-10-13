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
using NUnit.Framework;

namespace Tempest.Tests
{
	public abstract class ConnectionProviderTests
	{
		private IConnectionProvider provider;

		[SetUp]
		protected void Setup()
		{
			this.provider = SetUp();
		}

		protected abstract IConnectionProvider SetUp();

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
			Assert.DoesNotThrow (() => this.provider.Start (MessageTypes.Reliable)); // TODO: Determine available message types
			// *knock knock knock* Penny
			Assert.DoesNotThrow (() => this.provider.Start (MessageTypes.Reliable));
			// *knock knock knock* Penny
			Assert.DoesNotThrow (() => this.provider.Start (MessageTypes.Reliable));
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
	}
}
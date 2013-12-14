//
// SerializationContextTests.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011-2013 Eric Maupin
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
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class SerializationContextTests
	{
		public static ISerializationContext GetContext (Protocol protocol)
		{
			return new SerializationContext (new MockClientConnection (new MockConnectionProvider (protocol)),
				new Dictionary<byte, Protocol> { { protocol.id, protocol } });
		}

		[Test]
		public void CtorNull()
		{
			Assert.Throws<ArgumentNullException> (() => new SerializationContext (null));
			Assert.Throws<ArgumentNullException> (() => new SerializationContext (null, new Dictionary<byte, Protocol>()));
			Assert.Throws<ArgumentNullException> (() => new SerializationContext (new MockClientConnection (new MockConnectionProvider (MockProtocol.Instance)), null));
		}

		[Test]
		public void Ctor()
		{
			var c = new MockClientConnection (new MockConnectionProvider (MockProtocol.Instance));

			var protocols = new Dictionary<byte, Protocol> {
				{ MockProtocol.Instance.id, MockProtocol.Instance }
			};

			var context = new SerializationContext (c, protocols);
			Assert.AreSame (c, context.Connection);
			Assert.AreSame (MockProtocol.Instance, context.Protocols.First().Value);
		}
	}
}
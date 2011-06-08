//
// SerializationContextTests.cs
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
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class SerializationContextTests
	{
		[Test]
		public void CtorNull()
		{
			Assert.Throws<ArgumentNullException> (() => new SerializationContext (null, MockProtocol.Instance, new TypeMap()));
			Assert.Throws<ArgumentNullException> (() => new SerializationContext (new MockClientConnection (new MockConnectionProvider (MockProtocol.Instance)), null, new TypeMap()));
			Assert.Throws<ArgumentNullException> (() => new SerializationContext (new MockClientConnection (new MockConnectionProvider (MockProtocol.Instance)), MockProtocol.Instance, null));
		}

		[Test]
		public void Ctor()
		{
			var c = new MockClientConnection (new MockConnectionProvider (MockProtocol.Instance));

			var context = new SerializationContext (c, MockProtocol.Instance, new TypeMap());
			Assert.AreSame (c, context.Connection);
			Assert.AreSame (MockProtocol.Instance, context.Protocol);
		}

		[Test]
		public void CtorTypeMap()
		{
			var map = new TypeMap();
			int id;
			map.TryGetTypeId (typeof (string), out id);
			map.TryGetTypeId (typeof (int), out id);

			var c = new MockClientConnection (new MockConnectionProvider (MockProtocol.Instance));

			var context = new SerializationContext (c, MockProtocol.Instance, map);

			int id2;
			Assert.IsFalse (context.TryGetTypeId (typeof (int), out id2));
			Assert.AreEqual (id, id2);
		}
	}
}
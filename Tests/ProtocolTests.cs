//
// ProtocolTests.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class ProtocolTests
	{
		private static int id = 3;
		public static Protocol GetTestProtocol()
		{
			return new Protocol ((byte)Interlocked.Increment (ref id));
		}

		[Test]
		public void ReservedIdCtor()
		{
			Assert.Throws<ArgumentException> (() => new Protocol (1));
			Assert.Throws<ArgumentException> (() => new Protocol (1, 2));
		}

		[Test]
		public void Equality()
		{
			var p1 = new Protocol (2, 1);
			var p2 = new Protocol (2, 1);

			Assert.AreEqual (p1, p2);
			Assert.AreEqual (p1.GetHashCode(), p2.GetHashCode());
		}

		[Test]
		public void Inequality()
		{
			var p1 = new Protocol (2, 1);
			var p2 = new Protocol (3, 4);

			Assert.AreNotEqual (p1, p2);
			Assert.AreNotEqual (p1.GetHashCode(), p2.GetHashCode());
		}

		[Test]
		public void InequalityDifferentVersions()
		{
			var p1 = new Protocol (2, 1);
			var p2 = new Protocol (2, 3);

			Assert.AreNotEqual (p1, p2);
			Assert.AreNotEqual (p1.GetHashCode(), p2.GetHashCode());
		}

		[Test]
		public void InequalityDifferentIds()
		{
			var p1 = new Protocol (3, 2);
			var p2 = new Protocol (4, 2);

			Assert.AreNotEqual (p1, p2);
			Assert.AreNotEqual (p1.GetHashCode(), p2.GetHashCode());
		}

		[Test]
		public void Serializer()
		{
			byte[] buffer = new byte[1024];
			var writer = new BufferValueWriter (buffer);

			var p = new Protocol (42, 248);
			p.Serialize (writer);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var p2 = new Protocol (reader);

			Assert.AreEqual (p.id, p2.id);
			Assert.AreEqual (p.Version, p2.Version);
		}
	}
}
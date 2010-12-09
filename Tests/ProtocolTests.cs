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
		private static int id = 2;
		public static Protocol GetTestProtocol()
		{
			var p = new Protocol ((byte)Interlocked.Increment (ref id));
			Protocol.Register (p);
			return p;
		}

		[Test]
		public void GetBytesNull()
		{
			int len;
			Assert.Throws<ArgumentNullException> (() => MockProtocol.Instance.GetBytes (null, out len, new byte[10]));
			Assert.Throws<ArgumentNullException> (() => MockProtocol.Instance.GetBytes (new MockMessage(), out len, null));
			Assert.Throws<ArgumentNullException> (() => MockProtocol.Instance.GetBytes (null, out len));
		}

		[Test]
		public void GetHeaderInvalid()
		{
			Assert.Throws<ArgumentNullException> (() => MockProtocol.Instance.GetHeader (null, 0, 10));
			Assert.Throws<ArgumentOutOfRangeException> (() => MockProtocol.Instance.GetHeader (new byte[10], -1, 5));
			Assert.Throws<ArgumentOutOfRangeException> (() => MockProtocol.Instance.GetHeader (new byte[10], 0, 11));
			Assert.Throws<ArgumentOutOfRangeException> (() => MockProtocol.Instance.GetHeader (new byte[10], 6, 5));
		}

		[Test]
		public void GetHeaderNoMatch()
		{
			Protocol p = MockProtocol.Instance;
			p.Register (new [] { new KeyValuePair<Type, Func<Message>> (typeof(MockMessage), () => new MockMessage()) });

			byte[] buffer = new byte[10];

			BufferValueWriter writer = new BufferValueWriter (buffer);
			writer.Length = 2;
			writer.WriteByte (p.id);
			writer.WriteUInt16 (new MockMessage().MessageType);
			writer.WriteInt32 (8);
			
			Assert.IsNull (p.GetHeader (buffer, 0, 5));
			Assert.IsNull (p.GetHeader (buffer, 1, 8));

			Array.Clear (buffer, 0, 10);
			Assert.IsNull (p.GetHeader (buffer, 2, 8));
		}

		[Test]
		public void GetHeader()
		{
			Protocol p = MockProtocol.Instance;
			p.Register (new [] { new KeyValuePair<Type, Func<Message>> (typeof(MockMessage), () => new MockMessage()) });

			byte[] buffer = new byte[10];

			BufferValueWriter writer = new BufferValueWriter (buffer);
			writer.Length = 2;
			writer.WriteByte (p.id);
			writer.WriteUInt16 (new MockMessage().MessageType);
			writer.WriteInt32 (8);
			writer.WriteByte (1);
			
			MessageHeader header = p.GetHeader (buffer, 2, 8);
			Assert.IsNotNull (header);
			Assert.AreEqual (p, header.Protocol);
			Assert.AreEqual (8, header.Length);
			Assert.AreEqual (typeof(MockMessage), header.Message.GetType());
		}

		[Test]
		public void FindHeaderInvalid()
		{
			Assert.Throws<ArgumentNullException> (() => Tempest.Protocol.FindHeader (null));
			Assert.Throws<ArgumentNullException> (() => Tempest.Protocol.FindHeader (null, 0, 10));
			Assert.Throws<ArgumentOutOfRangeException> (() => Protocol.FindHeader (new byte[10], 0, 11));
			Assert.Throws<ArgumentOutOfRangeException>(() => Tempest.Protocol.FindHeader (new byte[10], -1, 10));
			Assert.Throws<ArgumentOutOfRangeException>(() => Tempest.Protocol.FindHeader (new byte[10], 6, 5));
		}
	}
}
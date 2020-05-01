//
// MessageFactoryTests.cs
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
// The above copyright notice and permission notice shall be included in
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
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class MessageFactoryTests
	{
		private static Protocol protocol;

		[SetUp]
		public void Setup()
		{
			protocol = new Protocol (MockProtocol.Instance.id, MockProtocol.Instance.Version);
		}

		[Test]
		public void RegisterNull()
		{
			Assert.Throws<ArgumentNullException> (() => protocol.Register ((IEnumerable<KeyValuePair<Type, Func<Message>>>)null));
		}

		private class PrivateMessage
			: Message
		{
			public PrivateMessage (Protocol protocol, ushort type)
				: base (protocol, type)
			{
			}

			public override void WritePayload(ISerializationContext context, IValueWriter writer)
			{
			}

			public override void ReadPayload(ISerializationContext context, IValueReader reader)
			{
			}
		}

		private class GenericMessage<T>
			: Message
		{
			public GenericMessage ()
				: base (MockProtocol.Instance, 3)
			{
			}

			public T Element
			{
				get;
				set;
			}

			public override void WritePayload(ISerializationContext context, IValueWriter writer)
			{
				writer.Write (context, Element);
			}

			public override void ReadPayload(ISerializationContext context, IValueReader reader)
			{
				Element = reader.Read<T>(context);
			}
		}


		[Test]
		public void RegisterTypeAndCtorsInvalid()
		{
			Assert.Throws<ArgumentException> (() =>
				protocol.Register (new[] { new KeyValuePair<Type, Func<Message>> (typeof (string), () => new MockMessage ()) }));
			Assert.Throws<ArgumentException> (() =>
				protocol.Register (new[] { new KeyValuePair<Type, Func<Message>> (typeof (int), () => new MockMessage ()) }));
		}

		[Test]
		public void RegisterTypeAndCtorsDuplicates()
		{
			Assert.Throws<ArgumentException> (() =>
				protocol.Register (new[]
				{
					new KeyValuePair<Type, Func<Message>> (typeof (MockMessage), () => new MockMessage ()),
					new KeyValuePair<Type, Func<Message>> (typeof (MockMessage), () => new MockMessage ()),
				}));
		}

		[Test]
		public void RegisterTypeWithCtor()
		{
			protocol.Register (new []
			{
				new KeyValuePair<Type, Func<Message>> (typeof(MockMessage), () => new MockMessage ()), 
				new KeyValuePair<Type, Func<Message>> (typeof(PrivateMessage), () => new PrivateMessage (protocol, 2)), 
				new KeyValuePair<Type, Func<Message>> (typeof(PrivateMessage), () => new PrivateMessage (protocol, 3)), 
			});

			Message m = protocol.Create (1);
			Assert.IsNotNull (m);
			Assert.That (m, Is.TypeOf<MockMessage>());

			m = protocol.Create (2);
			Assert.IsNotNull (m);
			Assert.AreEqual (2, m.MessageType);
			Assert.That (m, Is.TypeOf<PrivateMessage>());

			m = protocol.Create (3);
			Assert.IsNotNull (m);
			Assert.AreEqual (3, m.MessageType);
			Assert.That (m, Is.TypeOf<PrivateMessage>());
		}
	}
}
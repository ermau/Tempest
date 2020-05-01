//
// ObjectSerializerTests.cs
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
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class ObjectSerializerTests
	{
		private static ISerializationContext context;
		
		[SetUp]
		public void Setup()
		{
			context = SerializationContextTests.GetContext (MockProtocol.Instance);
		}

		enum TestEnum
			: byte
		{
			Low = 1,
			High = 5
		}

		[Test]
		public void Enum()
		{
			byte[] buffer = new byte[1024];
			var writer = new BufferValueWriter (buffer);
			writer.Write (context, TestEnum.High);
			int len = writer.Length;
			writer.Flush();

			var reader = new BufferValueReader (buffer);

			Assert.AreEqual (TestEnum.High, reader.Read<TestEnum> (context));
			Assert.AreEqual (len, reader.Position);
		}

		#if !SILVERLIGHT && !NETFX_CORE
		// [Test] Testing core APIs that now have NETFX_CORE turned on everywhere
		public void Serializable()
		{
			var inner = new Exception ("Inner exception");
			var ex = new InvalidOperationException ("Don't do this, fool.", inner);

			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);
			
			writer.Write (context, ex);
			writer.Flush();

			var reader = new BufferValueReader (buffer);

			InvalidOperationException ioex = SerializerExtensions.Read<InvalidOperationException> (reader, context);

			Assert.IsNotNull (ioex);
			Assert.AreEqual (ex.Message, ioex.Message);
			Assert.AreEqual (ex.StackTrace, ioex.StackTrace);

			Assert.IsNotNull (ioex.InnerException);
			Assert.AreEqual (inner.Message, ioex.InnerException.Message);
			Assert.AreEqual (inner.StackTrace, ioex.InnerException.StackTrace);
		}
		#endif

		[Test]
		public void ISerializable()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			SerializableTester tester = new SerializableTester
			{
				Name = "MONKEY!",
				Numbers = new[] { 1, 2, 4, 8, 16, 32 }
			};

			var test = new AsyncTest();
			tester.SerializeCalled += test.PassHandler;

			writer.Write (context, tester);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var serialized = SerializerExtensions.Read<SerializableTester> (reader, context);

			Assert.IsNotNull (serialized);
			Assert.AreEqual (tester.Name, serialized.Name);
			Assert.IsTrue (tester.Numbers.SequenceEqual (serialized.Numbers), "Numbers does not match");

			test.Assert (1000);
		}

		public class SerializableTester
			: ISerializable
		{
			public SerializableTester()
			{
			}

			protected SerializableTester (IValueReader reader)
			{
				Deserialize (context, reader);
			}

			public event EventHandler SerializeCalled;

			public string Name
			{
				get;
				set;
			}

			public int[] Numbers
			{
				get;
				set;
			}

			public void Serialize (ISerializationContext context, IValueWriter writer)
			{
				OnSerializeCalled (EventArgs.Empty);

				writer.WriteString (Name);

				writer.WriteInt32 (Numbers.Length);
				for (int i = 0; i < Numbers.Length; ++i)
					writer.WriteInt32 (Numbers[i]);
			}

			public void Deserialize (ISerializationContext context, IValueReader reader)
			{
				Name = reader.ReadString();

				Numbers = new int[reader.ReadInt32()];
				for (int i = 0; i < Numbers.Length; ++i)
					Numbers[i] = reader.ReadInt32();
			}

			private void OnSerializeCalled (EventArgs e)
			{
				var called = SerializeCalled;
				if (called != null)
					called (this, e);
			}
		}
	}
}

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
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class ObjectSerializerTests
	{
		[Test]
		public void Serializing()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			var value = new SerializingTester
			{
				Child = new SerializingTester
				{
					Child = new SerializingTester (true)
					{
						Text = "Three",
						Number = 3,
					},
					Number = 2,
					Text = "two",
					Numbers = new int[] {3, 2, 1}
				},
				Number = 1,
				Text = "one"
			};

			writer.Write (value);
			writer.Flush();

			var reader = new BufferValueReader (buffer);

			var readvalue = reader.Read<SerializingTester>();
			Assert.AreEqual (1, readvalue.Number);
			Assert.AreEqual ("one", readvalue.Text);
			Assert.IsFalse (readvalue.IgnoredProperty);
			Assert.IsNull (readvalue.Numbers);
			Assert.IsNotNull (readvalue.Child);

			readvalue = readvalue.Child;
			Assert.AreEqual (2, readvalue.Number);
			Assert.AreEqual ("two", readvalue.Text);
			Assert.IsNotNull (readvalue.Numbers);
			Assert.AreEqual (3, readvalue.Numbers.Length);
			Assert.AreEqual (3, readvalue.Numbers[0]);
			Assert.AreEqual (2, readvalue.Numbers[1]);
			Assert.AreEqual (1, readvalue.Numbers[2]);
			Assert.IsFalse (readvalue.IgnoredProperty);
			Assert.IsNotNull (readvalue.Child);

			readvalue = readvalue.Child;
			Assert.AreEqual (3, readvalue.Number);
			Assert.AreEqual ("Three", readvalue.Text);
			Assert.IsNull (readvalue.Numbers);
			Assert.IsFalse (readvalue.IgnoredProperty);
			Assert.IsNull (readvalue.Child);
		}

		[Test]
		public void RepeatedSerialization()
		{
			Serializing();
			Serializing();
		}

		[Test]
		public void MostDerived()
		{
			object[] values = new object[2];
			values[0] = new SerializingTester {Text = "text", Number = 5};
			values[1] = new MoreDerivedSerializingTester {Text = "text2", Extra = "extra", Number = 42};

			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter(buffer);
			
			writer.Write (values);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			object[] values2 = reader.Read<object[]>();

			Assert.IsNotNull (values2);
			Assert.AreEqual (values.Length, values2.Length);

			Assert.IsInstanceOf (typeof(SerializingTester), values2[0]);
			Assert.IsInstanceOf (typeof(MoreDerivedSerializingTester), values2[1]);

			SerializingTester tester = (SerializingTester)values2[0];
			Assert.AreEqual ("text", tester.Text);
			Assert.AreEqual (5, tester.Number);

			MoreDerivedSerializingTester tester2 = (MoreDerivedSerializingTester)values2[1];
			Assert.AreEqual ("text2", tester2.Text);
			Assert.AreEqual ("extra", tester2.Extra);
			Assert.AreEqual (42, tester2.Number);
		}

		[Test]
		public void Serializable()
		{
			var inner = new Exception ("Inner exception");
			var ex = new InvalidOperationException ("Don't do this, fool.", inner);

			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);
			
			writer.Write (ex);
			writer.Flush();

			var reader = new BufferValueReader (buffer);

			InvalidOperationException ioex = reader.Read<InvalidOperationException>();

			Assert.IsNotNull (ioex);
			Assert.AreEqual (ex.Message, ioex.Message);
			Assert.AreEqual (ex.Source, ioex.Source);
			Assert.AreEqual (ex.StackTrace, ioex.StackTrace);

			Assert.IsNotNull (ioex.InnerException);
			Assert.AreEqual (inner.Message, ioex.InnerException.Message);
			Assert.AreEqual (inner.Source, ioex.InnerException.Source);
			Assert.AreEqual (inner.StackTrace, ioex.InnerException.StackTrace);
		}

		public class MoreDerivedSerializingTester
			: SerializingTester
		{
			public string Extra
			{
				get; set;
			}
		}

		public class SerializingTester
		{
			public SerializingTester()
			{
			}

			public SerializingTester (bool ignored)
			{
				IgnoredProperty = ignored;
			}

			public int Number;
			public string Text;

			public int[] Numbers { get; set; }

			public SerializingTester Child { get; set; }

			public bool IgnoredProperty { get; private set; }
		}
	}
}
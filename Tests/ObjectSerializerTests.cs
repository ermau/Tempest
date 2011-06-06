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
using System.Linq;
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
		public void MixedObjectArray()
		{
			byte[] buffer = new byte[1024];
			var writer = new BufferValueWriter (buffer);

			object[] values = new object[] { 15, "hi", new SerializingTester { Text = "asdf", Number = 5 }};
			writer.Write (values);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			object[] readValues = reader.Read<object[]>();

			Assert.IsNotNull (readValues);
			Assert.AreEqual (values.Length, readValues.Length);
			Assert.AreEqual (values[0], readValues[0]);
			Assert.AreEqual (values[1], readValues[1]);

			var test = values[2] as SerializingTester;
			Assert.IsNotNull (test);
			Assert.AreEqual ("asdf", test.Text);
			Assert.AreEqual (5, test.Number);
		}

		[Test]
		public void PrimitiveAsObject()
		{
			byte[] buffer = new byte[1024];
			var writer = new BufferValueWriter (buffer);

			writer.Write ((object)20f);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			object value = reader.Read<object>();

			Assert.IsNotNull (value);
			Assert.AreEqual (20f, value);
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

		[Test]
		public void ISerializable()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			SerializableTester test = new SerializableTester
			{
				Name = "MONKEY!",
				Numbers = new[] { 1, 2, 4, 8, 16, 32 }
			};

			writer.Write (test);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var serialized = reader.Read<SerializableTester>();

			Assert.IsNotNull (serialized);
			Assert.AreEqual (test.Name, serialized.Name);
			Assert.IsTrue (test.Numbers.SequenceEqual (serialized.Numbers), "Numbers does not match");
		}

		[Test]
		public void Contracted()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			ISerializableTester test = new SerializableTester
			{
				Name = "MONKEY!",
				Numbers = new[] { 1, 2, 4, 8, 16, 32 }
			};

			writer.Write (test);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var serialized = reader.Read<ISerializableTester>();

			Assert.IsNotNull (serialized);
			Assert.AreEqual (test.Name, serialized.Name);
			Assert.IsTrue (test.Numbers.SequenceEqual (serialized.Numbers), "Numbers does not match");
		}

		[Test]
		public void PrivateCtor()
		{
			var test = PrivateCtorTester.GetTester();
			test.Name = "confidential";

			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);
			writer.Write (test);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var serialized = reader.Read<PrivateCtorTester>();

			Assert.IsNotNull (serialized);
			Assert.AreEqual (test.Name, serialized.Name);
		}

		[Test]
		public void ValueReaderCtor()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			ValueReaderTester test = new ValueReaderTester ("TheName");

			writer.Write (test);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var serialized = reader.Read<ValueReaderTester>();

			Assert.AreEqual (test.Name, serialized.Name);
		}

		[Test]
		public void Decimal()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			DecimalTester test = new DecimalTester { Value = 5.6m };
			writer.Write (test);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var serialized = reader.Read<DecimalTester>();

			Assert.AreEqual (test.Value, serialized.Value);
		}

		public class DecimalTester
		{
			public decimal Value;
		}

		public class ValueReaderTester
			: ISerializable
		{
			public ValueReaderTester (string name)
			{
				Name = name;
			}

			public ValueReaderTester (IValueReader reader)
			{
				Deserialize (reader);
			}

			public string Name
			{
				get;
				set;
			}

			public void Serialize (IValueWriter writer)
			{
				writer.WriteString (Name);
			}

			public void Deserialize (IValueReader reader)
			{
				Name = reader.ReadString();
			}
		}

		public class PrivateCtorTester
		{
			private PrivateCtorTester()
			{
			}

			public string Name
			{
				get;
				set;
			}

			public static PrivateCtorTester GetTester()
			{
				return new PrivateCtorTester();
			}
		}

		public interface ISerializableTester
			: ISerializable
		{
			string Name { get; }
			int[] Numbers { get; }
		}

		public class SerializableTester
			: ISerializableTester
		{
			public SerializableTester()
			{
			}

			protected SerializableTester (IValueReader reader)
			{
				Deserialize (reader);
			}

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

			public void Serialize (IValueWriter writer)
			{
				writer.WriteString (Name);

				writer.WriteInt32 (Numbers.Length);
				for (int i = 0; i < Numbers.Length; ++i)
					writer.WriteInt32 (Numbers[i]);
			}

			public void Deserialize (IValueReader reader)
			{
				Name = reader.ReadString();

				Numbers = new int[reader.ReadInt32()];
				for (int i = 0; i < Numbers.Length; ++i)
					Numbers[i] = reader.ReadInt32();
			}
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

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
			Assert.IsFalse (readvalue.PrivateSetProperty);
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
			Assert.IsFalse (readvalue.PrivateSetProperty);
			Assert.IsNotNull (readvalue.Child);

			readvalue = readvalue.Child;
			Assert.AreEqual (3, readvalue.Number);
			Assert.AreEqual ("Three", readvalue.Text);
			Assert.IsNull (readvalue.Numbers);
			Assert.IsTrue (readvalue.PrivateSetProperty);
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

		#if !SILVERLIGHT
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

			writer.Write (tester);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var serialized = reader.Read<SerializableTester>();

			Assert.IsNotNull (serialized);
			Assert.AreEqual (tester.Name, serialized.Name);
			Assert.IsTrue (tester.Numbers.SequenceEqual (serialized.Numbers), "Numbers does not match");

			test.Assert (1000);
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

		private class EventSerializingTester
		{
			public event EventHandler TestEvent;
			public string Text;
		}

		[Test]
		public void WithEvent()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			EventSerializingTester test = new EventSerializingTester { Text = "thetext" };
			test.TestEvent += (s,e) => { };

			writer.Write (test);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var serialized = reader.Read<EventSerializingTester>();

			Assert.IsNotNull (serialized);
			Assert.AreEqual (test.Text, serialized.Text);
		}

		public class DelegateSerializingTester
		{
			public Action TestAction;
			public string Text;
		}

		[Test]
		public void WithDelegate()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			DelegateSerializingTester test = new DelegateSerializingTester { Text = "thetext" };
			test.TestAction = () => { };

			writer.Write (test);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var serialized = reader.Read<DelegateSerializingTester>();

			Assert.IsNotNull (serialized);
			Assert.AreEqual (test.Text, serialized.Text);
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

			public void Serialize (IValueWriter writer)
			{
				OnSerializeCalled (EventArgs.Empty);

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

			private void OnSerializeCalled (EventArgs e)
			{
				var called = SerializeCalled;
				if (called != null)
					called (this, e);
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
				PrivateSetProperty = ignored;
			}

			public int Number;
			public string Text;

			public int[] Numbers { get; set; }

			public SerializingTester Child { get; set; }

			public bool PrivateSetProperty { get; private set; }
		}
	}
}

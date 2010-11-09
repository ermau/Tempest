using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class ExtensionsTests
	{
		[Test]
		public void ReadWriteDate()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			DateTime d = DateTime.Now;

			writer.WriteDate (d);
			writer.Flush();

			var reader = new BufferValueReader (buffer);

			Assert.AreEqual (d, reader.ReadDate());
		}

		[Test]
		public void Serializing()
		{
			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			var value = new SerializingTester
			{
				Child = new SerializingTester
				{
					Child = new SerializingTester(true)
					{
						Text = "Three",
						Number = 3,
					},

					Number = 2,
					Text = "two",
					Numbers = new int[] { 3, 2, 1 }
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

			public int[] Numbers
			{
				get;
				set;
			}

			public SerializingTester Child
			{
				get;
				set;
			}

			public bool IgnoredProperty
			{
				get;
				private set;
			}
		}
	}
}
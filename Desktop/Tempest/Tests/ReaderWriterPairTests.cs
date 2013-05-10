//
// ReaderWriterPairTests.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010-2012 Eric Maupin
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
using System.Text;
using NUnit.Framework;

namespace Tempest.Tests
{
	public abstract class ReaderWriterPairTests
	{
		protected abstract IValueWriter GetWriter();
		protected abstract IValueReader GetReader (IValueWriter writer);
		protected virtual IValueReader GetReader()
		{
			return GetReader (GetWriter());
		}

		[Test]
		public void ReadStringNullEncoding()
		{
			Assert.Throws<ArgumentNullException> (() => GetReader().ReadString (null));
		}

		[Test]
		public void ReadBytesInvalidCount()
		{
			Assert.Throws<ArgumentOutOfRangeException> (() => GetReader().ReadBytes (-1));
		}

		[Test]
		public void WriteStringNullEncoding()
		{
			Assert.Throws<ArgumentNullException> (() => GetWriter().WriteString (null, null));
		}

		[Test]
		public void WriteBytesNull()
		{
			Assert.Throws<ArgumentNullException> (() => GetWriter().WriteBytes (null));
			Assert.Throws<ArgumentNullException> (() => GetWriter().WriteBytes (null, 0, 0));
		}

		[Test]
		public void WriteBytesInvalidRange()
		{
			byte[] data = new byte[5];
			Assert.Throws<ArgumentOutOfRangeException> (() => GetWriter().WriteBytes (data, 1, 5));
			Assert.Throws<ArgumentOutOfRangeException> (() => GetWriter().WriteBytes (data, 0, 6));
			Assert.Throws<ArgumentOutOfRangeException> (() => GetWriter().WriteBytes (data, 5, 0));
		}

		[Test]
		public void ReadWriteBool()
		{
			IValueWriter writer = GetWriter();
			writer.WriteBool (true);
			writer.WriteBool (false);
			writer.Flush();

			IValueReader reader = GetReader (writer);
			Assert.IsTrue (reader.ReadBool());
			Assert.IsFalse (reader.ReadBool());
		}

		[Test]
		public void ReadWriteByte ()
		{
			IValueWriter writer = GetWriter();
			writer.WriteByte (Byte.MaxValue);
			writer.WriteByte (128);
			writer.WriteByte (Byte.MinValue);
			writer.Flush();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (Byte.MaxValue, reader.ReadByte());
			Assert.AreEqual (128, reader.ReadByte());
			Assert.AreEqual (Byte.MinValue, reader.ReadByte());
		}

		[Test]
		public void ReadWriteBytes()
		{
			IValueWriter writer = GetWriter();
			byte[] data = new byte[] { 0x4, 0x8, 0xF, 0x10, 0x17, 0x2A };
			writer.WriteBytes (data);
			writer.Flush();

			data = GetReader (writer).ReadBytes();
			Assert.AreEqual (6, data.Length);
			Assert.AreEqual (0x4, data[0]);
			Assert.AreEqual (0x8, data[1]);
			Assert.AreEqual (0xF, data[2]);
			Assert.AreEqual (0x10, data[3]);
			Assert.AreEqual (0x17, data[4]);
			Assert.AreEqual (0x2A, data[5]);
		}

		[Test]
		public void ReadWriteBytesSubset()
		{
			IValueWriter writer = GetWriter();
			byte[] data = new byte[] { 0x4, 0x8, 0xF, 0x10, 0x17, 0x2A };
			writer.WriteBytes (data, 2, 3);
			writer.Flush();

			data = GetReader (writer).ReadBytes();
			Assert.AreEqual (3, data.Length);
			Assert.AreEqual (0xF, data[0]);
			Assert.AreEqual (0x10, data[1]);
			Assert.AreEqual (0x17, data[2]);
		}

		[Test]
		public void ReadCountWriteByte()
		{
			IValueWriter writer = GetWriter();
			byte[] data = new byte[] { 0x4, 0x8, 0xF, 0x10, 0x17, 0x2A };
			for (int i = 0; i < data.Length; ++i)
				writer.WriteByte (data[i]);

			writer.Flush();

			data = GetReader (writer).ReadBytes (5);
			Assert.AreEqual (5, data.Length);
			Assert.AreEqual (0x4, data[0]);
			Assert.AreEqual (0x8, data[1]);
			Assert.AreEqual (0xF, data[2]);
			Assert.AreEqual (0x10, data[3]);
			Assert.AreEqual (0x17, data[4]);
		}

		[Test]
		public void ReadWriteSByte()
		{
			IValueWriter writer = GetWriter();
			writer.WriteSByte (SByte.MaxValue);
			writer.WriteSByte (0);
			writer.WriteSByte (SByte.MinValue);
			writer.Flush();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (SByte.MaxValue, reader.ReadSByte());
			Assert.AreEqual (0, reader.ReadSByte());
			Assert.AreEqual (SByte.MinValue, reader.ReadSByte());
		}

		[Test]
		public void ReadWriteInt16()
		{
			IValueWriter writer = GetWriter();
			writer.WriteInt16 (Int16.MaxValue);
			writer.WriteInt16 (0);
			writer.WriteInt16 (Int16.MinValue);
			writer.Flush();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (Int16.MaxValue, reader.ReadInt16());
			Assert.AreEqual (0, reader.ReadInt16());
			Assert.AreEqual (Int16.MinValue, reader.ReadInt16());
		}

		[Test]
		public void ReadWriteUInt16()
		{
			IValueWriter writer = GetWriter();
			writer.WriteUInt16 (UInt16.MaxValue);
			writer.WriteUInt16 (UInt16.MaxValue / 2);
			writer.WriteUInt16 (UInt16.MinValue);
			writer.Flush();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (UInt16.MaxValue, reader.ReadUInt16());
			Assert.AreEqual (UInt16.MaxValue / 2, reader.ReadUInt16());
			Assert.AreEqual (UInt16.MinValue, reader.ReadUInt16());
		}

		[Test]
		public void ReadWriteInt32()
		{
			IValueWriter writer = GetWriter();
			writer.WriteInt32 (Int32.MaxValue);
			writer.WriteInt32 (0);
			writer.WriteInt32 (Int32.MinValue);
			writer.Flush();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (Int32.MaxValue, reader.ReadInt32());
			Assert.AreEqual (0, reader.ReadInt32());
			Assert.AreEqual (Int32.MinValue, reader.ReadInt32());
		}

		[Test]
		public void ReadWriteUInt32()
		{
			IValueWriter writer = GetWriter();
			writer.WriteUInt32 (UInt32.MaxValue);
			writer.WriteUInt32 (UInt32.MaxValue / 2);
			writer.WriteUInt32 (UInt32.MinValue);
			writer.Flush ();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (UInt32.MaxValue, reader.ReadUInt32());
			Assert.AreEqual (UInt32.MaxValue / 2, reader.ReadUInt32());			
			Assert.AreEqual (UInt32.MinValue, reader.ReadUInt32());
		}

		[Test]
		public void ReadWriteInt64()
		{
			IValueWriter writer = GetWriter();
			writer.WriteInt64 (Int64.MaxValue);
			writer.WriteInt64 (0);
			writer.WriteInt64 (Int64.MinValue);
			writer.Flush ();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (Int64.MaxValue, reader.ReadInt64());
			Assert.AreEqual (0, reader.ReadInt64());		
			Assert.AreEqual (Int64.MinValue, reader.ReadInt64());
		}

		[Test]
		public void ReadWriteUInt64()
		{
			IValueWriter writer = GetWriter();
			writer.WriteUInt64 (UInt64.MaxValue);
			writer.WriteUInt64 (UInt64.MaxValue / 2);
			writer.WriteUInt64 (UInt64.MinValue);
			writer.Flush();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (UInt64.MaxValue, reader.ReadUInt64());
			Assert.AreEqual (UInt64.MaxValue / 2, reader.ReadUInt64());
			Assert.AreEqual (UInt64.MinValue, reader.ReadUInt64());
		}


		[Test]
		public void ReadWriteString()
		{
			const string value = "The lazy fox..\n oh forget it.\0";

			IValueWriter writer = GetWriter();
			writer.WriteString (Encoding.UTF8, null);
			writer.WriteString (Encoding.UTF8, String.Empty);
			writer.WriteString (Encoding.UTF8, value);

			#if !SILVERLIGHT && !NETFX_CORE
			writer.WriteString (Encoding.UTF32, value);
			writer.WriteString (Encoding.ASCII, value);
			#endif

			writer.Flush ();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (null, reader.ReadString (Encoding.UTF8));
			Assert.AreEqual (String.Empty, reader.ReadString (Encoding.UTF8));
			Assert.AreEqual (value, reader.ReadString (Encoding.UTF8));

			#if !SILVERLIGHT && !NETFX_CORE
			Assert.AreEqual (value, reader.ReadString (Encoding.UTF32));
			Assert.AreEqual (value, reader.ReadString (Encoding.ASCII));
			#endif
		}

		[Test]
		public void ReadWriteLongString()
		{
			string longString = TestHelpers.GetLongString (new Random (43));

			IValueWriter writer = GetWriter();
			writer.WriteString (Encoding.UTF8, longString);
			writer.Flush();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (longString, reader.ReadString (Encoding.UTF8));
		}

		[Test]
		public void ReadWriteDecimal()
		{
			IValueWriter writer = GetWriter();
			writer.WriteDecimal (Decimal.MaxValue);
			writer.WriteDecimal (Decimal.MaxValue / 2);
			writer.WriteDecimal (Decimal.MinValue);
			writer.Flush();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (Decimal.MaxValue, reader.ReadDecimal());
			Assert.AreEqual (Decimal.MaxValue / 2, reader.ReadDecimal());
			Assert.AreEqual (Decimal.MinValue, reader.ReadDecimal());
		}

		[Test]
		public void ReadWriteDouble()
		{
			IValueWriter writer = GetWriter();
			writer.WriteDouble (Double.MaxValue);
			writer.WriteDouble (Double.MaxValue / 2);
			writer.WriteDouble (Double.MinValue);
			writer.Flush ();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (Double.MaxValue, reader.ReadDouble ());
			Assert.AreEqual (Double.MaxValue / 2, reader.ReadDouble ());
			Assert.AreEqual (Double.MinValue, reader.ReadDouble ());
		}

		[Test]
		public void ReadWriteSingle ()
		{
			IValueWriter writer = GetWriter();
			writer.WriteSingle (Single.MaxValue);
			writer.WriteSingle (Single.MaxValue / 2);
			writer.WriteSingle (Single.MinValue);
			writer.Flush ();

			IValueReader reader = GetReader (writer);
			Assert.AreEqual (Single.MaxValue, reader.ReadSingle ());
			Assert.AreEqual (Single.MaxValue / 2, reader.ReadSingle ());
			Assert.AreEqual (Single.MinValue, reader.ReadSingle ());
		}
	}
}
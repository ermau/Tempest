//
// ReaderWriterPairTests.cs
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
using System.Text;
using NUnit.Framework;

namespace Tempest.Tests
{
	public abstract class ReaderWriterPairTests
	{
		private IValueWriter writer;
		private IValueReader reader;

		protected void Setup (IValueWriter writer, IValueReader reader)
		{
			if (writer == null)
				throw new ArgumentNullException ("writer");
			if (reader == null)
				throw new ArgumentNullException ("reader");

			this.writer = writer;
			this.reader = reader;
		}

		[Test]
		public void ReadStringNullEncoding()
		{
			Assert.Throws<ArgumentNullException> (() => this.reader.ReadString (null));
		}

		[Test]
		public void ReadBytesInvalidCount()
		{
			Assert.Throws<ArgumentOutOfRangeException> (() => this.reader.ReadBytes (-1));
		}

		[Test]
		public void WriteStringNullEncoding()
		{
			Assert.AreEqual (Assert.Throws<ArgumentNullException> (() => this.writer.WriteString (null, null))
			               	.ParamName, "encoding");
		}

		[Test]
		public void WriteBytesNull()
		{
			Assert.Throws<ArgumentNullException> (() => this.writer.WriteBytes (null));
			Assert.Throws<ArgumentNullException> (() => this.writer.WriteBytes (null, 0, 0));
		}

		[Test]
		public void WriteBytesInvalidRange()
		{
			byte[] data = new byte[5];
			Assert.Throws<ArgumentOutOfRangeException> (() => this.writer.WriteBytes (data, 1, 5));
			Assert.Throws<ArgumentOutOfRangeException> (() => this.writer.WriteBytes (data, 0, 6));
			Assert.Throws<ArgumentOutOfRangeException> (() => this.writer.WriteBytes (data, 5, 0));
		}

		[Test]
		public void ReadWriteBool()
		{
			this.writer.WriteBool (true);
			this.writer.WriteBool (false);
			this.writer.Flush();

			Assert.IsTrue (this.reader.ReadBool());
			Assert.IsFalse (this.reader.ReadBool());
		}

		[Test]
		public void ReadWriteByte ()
		{
			this.writer.WriteByte (Byte.MaxValue);
			this.writer.WriteByte (128);
			this.writer.WriteByte (Byte.MinValue);
			this.writer.Flush();

			Assert.AreEqual (Byte.MaxValue, this.reader.ReadByte());
			Assert.AreEqual (128, this.reader.ReadByte());
			Assert.AreEqual (Byte.MinValue, this.reader.ReadByte());
		}

		[Test]
		public void ReadWriteBytes()
		{
			byte[] data = new byte[] { 0x4, 0x8, 0xF, 0x10, 0x17, 0x2A };
			this.writer.WriteBytes (data);
			this.writer.Flush();

			data = this.reader.ReadBytes();
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
			byte[] data = new byte[] { 0x4, 0x8, 0xF, 0x10, 0x17, 0x2A };
			this.writer.WriteBytes (data, 2, 3);
			this.writer.Flush();

			data = this.reader.ReadBytes();
			Assert.AreEqual (3, data.Length);
			Assert.AreEqual (0xF, data[0]);
			Assert.AreEqual (0x10, data[1]);
			Assert.AreEqual (0x17, data[2]);
		}

		[Test]
		public void ReadCountWriteByte()
		{
			byte[] data = new byte[] { 0x4, 0x8, 0xF, 0x10, 0x17, 0x2A };
			for (int i = 0; i < data.Length; ++i)
				this.writer.WriteByte (data[i]);

			this.writer.Flush();

			data = this.reader.ReadBytes (5);
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
			this.writer.WriteSByte (SByte.MaxValue);
			this.writer.WriteSByte (0);
			this.writer.WriteSByte (SByte.MinValue);
			this.writer.Flush();

			Assert.AreEqual (SByte.MaxValue, this.reader.ReadSByte());
			Assert.AreEqual (0, this.reader.ReadSByte());
			Assert.AreEqual (SByte.MinValue, this.reader.ReadSByte());
		}

		[Test]
		public void ReadWriteInt16()
		{
			this.writer.WriteInt16 (Int16.MaxValue);
			this.writer.WriteInt16 (0);
			this.writer.WriteInt16 (Int16.MinValue);
			this.writer.Flush();

			Assert.AreEqual (Int16.MaxValue, this.reader.ReadInt16());
			Assert.AreEqual (0, this.reader.ReadInt16());
			Assert.AreEqual (Int16.MinValue, this.reader.ReadInt16());
		}

		[Test]
		public void ReadWriteUInt16()
		{
			this.writer.WriteUInt16 (UInt16.MaxValue);
			this.writer.WriteUInt16 (UInt16.MaxValue / 2);
			this.writer.WriteUInt16 (UInt16.MinValue);
			this.writer.Flush();

			Assert.AreEqual (UInt16.MaxValue, this.reader.ReadUInt16());
			Assert.AreEqual (UInt16.MaxValue / 2, this.reader.ReadUInt16());
			Assert.AreEqual (UInt16.MinValue, this.reader.ReadUInt16());
		}

		[Test]
		public void ReadWriteInt32()
		{
			this.writer.WriteInt32 (Int32.MaxValue);
			this.writer.WriteInt32 (0);
			this.writer.WriteInt32 (Int32.MinValue);
			this.writer.Flush();

			Assert.AreEqual (Int32.MaxValue, this.reader.ReadInt32());
			Assert.AreEqual (0, this.reader.ReadInt32());
			Assert.AreEqual (Int32.MinValue, this.reader.ReadInt32());
		}

		[Test]
		public void ReadWriteUInt32()
		{
			this.writer.WriteUInt32 (UInt32.MaxValue);
			this.writer.WriteUInt32 (UInt32.MaxValue / 2);
			this.writer.WriteUInt32 (UInt32.MinValue);
			this.writer.Flush ();

			Assert.AreEqual (UInt32.MaxValue, this.reader.ReadUInt32());
			Assert.AreEqual (UInt32.MaxValue / 2, this.reader.ReadUInt32());			
			Assert.AreEqual (UInt32.MinValue, this.reader.ReadUInt32());
		}

		[Test]
		public void ReadWriteInt64()
		{
			this.writer.WriteInt64 (Int64.MaxValue);
			this.writer.WriteInt64 (0);
			this.writer.WriteInt64 (Int64.MinValue);
			this.writer.Flush ();

			Assert.AreEqual (Int64.MaxValue, this.reader.ReadInt64());
			Assert.AreEqual (0, this.reader.ReadInt64());		
			Assert.AreEqual (Int64.MinValue, this.reader.ReadInt64());
		}

		[Test]
		public void ReadWriteUInt64()
		{
			this.writer.WriteUInt64 (UInt64.MaxValue);
			this.writer.WriteUInt64 (UInt64.MaxValue / 2);
			this.writer.WriteUInt64 (UInt64.MinValue);
			this.writer.Flush();

			Assert.AreEqual (UInt64.MaxValue, this.reader.ReadUInt64());
			Assert.AreEqual (UInt64.MaxValue / 2, this.reader.ReadUInt64());
			Assert.AreEqual (UInt64.MinValue, this.reader.ReadUInt64());
		}

		[Test]
		public void ReadWriteString()
		{
			const string value = "The lazy fox..\n oh forget it.\0";

			this.writer.WriteString (Encoding.UTF8, value);
			this.writer.WriteString (Encoding.UTF32, value);
			this.writer.WriteString (Encoding.ASCII, value);
			this.writer.Flush ();

			Assert.AreEqual (value, this.reader.ReadString (Encoding.UTF8));
			Assert.AreEqual (value, this.reader.ReadString (Encoding.UTF32));
			Assert.AreEqual (value, this.reader.ReadString (Encoding.ASCII));
		}

		[Test]
		public void ReadWriteDecimal()
		{
			this.writer.WriteDecimal (Decimal.MaxValue);
			this.writer.WriteDecimal (Decimal.MaxValue / 2);
			this.writer.WriteDecimal (Decimal.MinValue);
			this.writer.Flush();

			Assert.AreEqual (Decimal.MaxValue, this.reader.ReadDecimal());
			Assert.AreEqual (Decimal.MaxValue / 2, this.reader.ReadDecimal());
			Assert.AreEqual (Decimal.MinValue, this.reader.ReadDecimal());
		}

		[Test]
		public void ReadWriteDouble()
		{
			this.writer.WriteDouble (Double.MaxValue);
			this.writer.WriteDouble (Double.MaxValue / 2);
			this.writer.WriteDouble (Double.MinValue);
			this.writer.Flush ();

			Assert.AreEqual (Double.MaxValue, this.reader.ReadDouble ());
			Assert.AreEqual (Double.MaxValue / 2, this.reader.ReadDouble ());
			Assert.AreEqual (Double.MinValue, this.reader.ReadDouble ());
		}

		[Test]
		public void ReadWriteSingle ()
		{
			this.writer.WriteSingle (Single.MaxValue);
			this.writer.WriteSingle (Single.MaxValue / 2);
			this.writer.WriteSingle (Single.MinValue);
			this.writer.Flush ();

			Assert.AreEqual (Single.MaxValue, this.reader.ReadSingle ());
			Assert.AreEqual (Single.MaxValue / 2, this.reader.ReadSingle ());
			Assert.AreEqual (Single.MinValue, this.reader.ReadSingle ());
		}
	}
}
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
			this.writer.Flush();
			Assert.IsTrue (this.reader.ReadBool());
			
			this.writer.WriteBool (false);
			this.writer.Flush();
			Assert.IsFalse (this.reader.ReadBool());
		}

		[Test]
		public void ReadWriteByte ()
		{
			this.writer.WriteByte (Byte.MaxValue);
			this.writer.Flush();
			Assert.AreEqual (Byte.MaxValue, this.reader.ReadByte());

			this.writer.WriteByte (128);
			this.writer.Flush();
			Assert.AreEqual (128, this.reader.ReadByte());

			this.writer.WriteByte (Byte.MinValue);
			this.writer.Flush();
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
			this.writer.Flush();
			Assert.AreEqual (SByte.MaxValue, this.reader.ReadSByte());

			this.writer.WriteSByte (0);
			this.writer.Flush();
			Assert.AreEqual (0, this.reader.ReadByte());

			this.writer.WriteSByte (SByte.MinValue);
			this.writer.Flush();
			Assert.AreEqual (SByte.MinValue, this.reader.ReadSByte());
		}
	}
}
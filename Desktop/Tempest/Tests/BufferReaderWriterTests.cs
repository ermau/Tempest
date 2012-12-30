//
// BufferReaderWriterTests.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class BufferReaderWriterTests
		: ReaderWriterPairTests
	{
		protected override IValueWriter GetWriter()
		{
			return new BufferValueWriter (new byte[20480]);
		}

		protected override IValueReader GetReader (IValueWriter writer)
		{
			BufferValueWriter bufferWriter = (BufferValueWriter)writer;
			return new BufferValueReader (bufferWriter.Buffer);
		}

		[Test]
		public void ReaderCtorNull()
		{
			Assert.Throws<ArgumentNullException> (() => new BufferValueReader (null));
		}

		[Test]
		public void WriterCtorNull()
		{
			Assert.Throws<ArgumentNullException> (() => new BufferValueWriter (null));
		}

		[Test]
		public void BufferOverflowResize()
		{
			byte[] buffer = new byte[4];
			var writer = new BufferValueWriter (buffer);
			writer.WriteInt64 (1);

			Assert.That (writer.Length, Is.EqualTo (8));
			Assert.That (writer.Buffer.Length, Is.AtLeast (8));
		}

		[Test]
		public void ReadWriteLongSet()
		{
			var writer = new BufferValueWriter (new byte[1]);

			for (int i = 0; i < 20480; ++i)
				writer.WriteInt32(i);

			writer.Flush();

			var reader = new BufferValueReader (writer.Buffer);
			for (int i = 0; i < 20480; ++i)
				Assert.AreEqual(i, reader.ReadInt32());
		}
	}
}
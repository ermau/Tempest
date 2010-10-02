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
	}
}
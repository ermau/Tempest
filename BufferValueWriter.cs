//
// BufferValueWriter.cs
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
using System.IO;
using System.Linq;
using System.Text;

namespace Tempest
{
	public class BufferValueWriter
		: IValueWriter
	{
		private readonly byte[] buffer;
		private int position;

		public BufferValueWriter (byte[] buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");
			
			this.buffer = buffer;
		}

		public void WriteByte (byte value)
		{
			if (this.position == this.buffer.Length)
				throw new InternalBufferOverflowException();

			this.buffer[this.position++] = value;
		}

		public void WriteSByte (sbyte value)
		{
			if (this.position == this.buffer.Length)
				throw new InternalBufferOverflowException();

			this.buffer[this.position++] = (byte)value;
		}

		public void WriteBool (bool value)
		{
			if (this.position == this.buffer.Length)
				throw new InternalBufferOverflowException();

			this.buffer[this.position++] = (byte)((value) ? 1 : 0);
		}

		public void WriteBytes (byte[] value)
		{
			if (value == null)
				throw new ArgumentNullException ("value");
			if (value.Length > this.buffer.Length - this.position)
				throw new InternalBufferOverflowException();

			Array.Copy (value, 0, this.buffer, this.position, value.Length);
			this.position += value.Length;
		}

		public void WriteBytes (byte[] value, int offset, int length)
		{
			if (value == null)
				throw new ArgumentNullException ("value");
			if (offset < 0 || offset >= value.Length)
				throw new ArgumentOutOfRangeException ("offset", offset, "offset can not negative or >=data.Length");
			if (length < 0 || offset + length >= value.Length)
				throw new ArgumentOutOfRangeException ("length", length, "length can not be negative or combined with offset longer than the array");
			if (length - offset > this.buffer.Length - this.position)
				throw new InternalBufferOverflowException();

			Array.Copy (value, offset, this.buffer, this.position, length);
			this.position += length;
		}

		public void WriteInt16 (short value)
		{
			if (this.position + sizeof(short) >= this.buffer.Length)
				throw new InternalBufferOverflowException();

			Array.Copy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof(short));
			this.position += sizeof (short);
		}

		public void WriteInt32 (int value)
		{
			if (this.position + sizeof (int) >= this.buffer.Length)
				throw new InternalBufferOverflowException ();

			Array.Copy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (int));
			this.position += sizeof (int);
		}

		public void WriteInt64 (long value)
		{
			if (this.position + sizeof (long) >= this.buffer.Length)
				throw new InternalBufferOverflowException ();

			Array.Copy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (long));
			this.position += sizeof (long);
		}

		public void WriteUInt16 (ushort value)
		{
			if (this.position + sizeof (ushort) >= this.buffer.Length)
				throw new InternalBufferOverflowException ();

			Array.Copy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (ushort));
			this.position += sizeof (ushort);
		}

		public void WriteUInt32 (uint value)
		{
			if (this.position + sizeof (uint) >= this.buffer.Length)
				throw new InternalBufferOverflowException ();

			Array.Copy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (uint));
			this.position += sizeof (uint);
		}

		public void WriteUInt64 (ulong value)
		{
			if (this.position + sizeof (ulong) >= this.buffer.Length)
				throw new InternalBufferOverflowException ();

			Array.Copy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (ulong));
			this.position += sizeof (ulong);
		}

		public void WriteString (Encoding encoding, string value)
		{
			if (encoding == null)
				throw new ArgumentNullException ("encoding");

			byte[] data = encoding.GetBytes (value);
			if (data.Length + sizeof(int) + this.position >= this.buffer.Length)
				throw new InternalBufferOverflowException();

			Array.Copy (BitConverter.GetBytes (data.Length), 0, this.buffer, this.position, sizeof(int));
			this.position += sizeof (int);
			Array.Copy (data, 0, this.buffer, this.position, data.Length);
			this.position += data.Length;
		}

		public void Flush()
		{
			this.position = 0;
		}
	}
}
//
// BufferValueWriter.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011 Eric Maupin
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
using System.Text;
using Buff = System.Buffer;

namespace Tempest
{
	public class BufferValueWriter
		: IValueWriter
	{
		public BufferValueWriter (byte[] buffer)
			: this (buffer, true)
		{
		}

		public BufferValueWriter (byte[] buffer, bool resizing)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			this.buffer = buffer;
			this.resizing = resizing;
		}

		public int Length
		{
			get { return this.position; }
			set
			{
				if (value > this.position)
					EnsureAdditionalCapacity (value - this.position);

				this.position = value;
			}
		}

		public byte[] Buffer
		{
			get { return this.buffer; }
		}

		public void WriteByte (byte value)
		{
			EnsureAdditionalCapacity (1);

			this.buffer[this.position++] = value;
		}

		public void WriteSByte (sbyte value)
		{
			EnsureAdditionalCapacity (1);

			this.buffer[this.position++] = (byte)value;
		}

		public bool WriteBool (bool value)
		{
			EnsureAdditionalCapacity (1);

			this.buffer[this.position++] = (byte)((value) ? 1 : 0);

			return value;
		}

		public void WriteBytes (byte[] value)
		{
			if (value == null)
				throw new ArgumentNullException ("value");
			
			EnsureAdditionalCapacity (sizeof(int) + value.Length);

			Buff.BlockCopy (BitConverter.GetBytes (value.Length), 0, this.buffer, this.position, sizeof(int));
			this.position += sizeof (int);
			Buff.BlockCopy (value, 0, this.buffer, this.position, value.Length);
			this.position += value.Length;
		}

		public void WriteBytes (byte[] value, int offset, int length)
		{
			if (value == null)
				throw new ArgumentNullException ("value");
			if (offset < 0 || offset >= value.Length)
				throw new ArgumentOutOfRangeException ("offset", offset, "offset can not negative or >=data.Length");
			if (length < 0 || offset + length > value.Length)
				throw new ArgumentOutOfRangeException ("length", length, "length can not be negative or combined with offset longer than the array");
			
			EnsureAdditionalCapacity (sizeof(int) + length);

			Buff.BlockCopy (BitConverter.GetBytes (length), 0, this.buffer, this.position, sizeof (int));
			this.position += sizeof (int);
			Buff.BlockCopy (value, offset, this.buffer, this.position, length);
			this.position += length;
		}

		public void InsertBytes (int offset, byte[] value, int valueOffset, int length)
		{
			if (value == null)
				throw new ArgumentNullException ("value");
			if (valueOffset < 0 || valueOffset >= value.Length)
				throw new ArgumentOutOfRangeException ("offset", offset, "offset can not negative or >=data.Length");
			if (length < 0 || valueOffset + length > value.Length)
				throw new ArgumentOutOfRangeException ("length", length, "length can not be negative or combined with offset longer than the array");

			EnsureAdditionalCapacity (length);

			if (offset != this.position)
				Buff.BlockCopy (this.buffer, offset, this.buffer, offset + length, this.position - offset);

			Buff.BlockCopy (value, valueOffset, this.buffer, offset, length);
			this.position += length;
		}

		public void WriteInt16 (short value)
		{
			EnsureAdditionalCapacity (sizeof (short));

			Buff.BlockCopy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof(short));
			this.position += sizeof (short);
		}

		public void WriteInt32 (int value)
		{
			EnsureAdditionalCapacity (sizeof (int));

			Buff.BlockCopy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (int));
			this.position += sizeof (int);
		}

		public void WriteInt64 (long value)
		{
			EnsureAdditionalCapacity (sizeof (long));

			Buff.BlockCopy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (long));
			this.position += sizeof (long);
		}

		public void WriteUInt16 (ushort value)
		{
			EnsureAdditionalCapacity (sizeof (ushort));

			Buff.BlockCopy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (ushort));
			this.position += sizeof (ushort);
		}

		public void WriteUInt32 (uint value)
		{
			EnsureAdditionalCapacity (sizeof (uint));

			Buff.BlockCopy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (uint));
			this.position += sizeof (uint);
		}

		public void WriteUInt64 (ulong value)
		{
			EnsureAdditionalCapacity (sizeof(ulong));

			Buff.BlockCopy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (ulong));
			this.position += sizeof (ulong);
		}

		public void WriteDecimal (decimal value)
		{
			int[] parts = Decimal.GetBits (value);
			for (int i = 0; i < parts.Length; ++i)
				WriteInt32 (parts[i]);
		}

		public void WriteSingle (float value)
		{
			EnsureAdditionalCapacity (sizeof(float));

			Buff.BlockCopy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (float));
			this.position += sizeof (float);
		}

		public void WriteDouble (double value)
		{
			EnsureAdditionalCapacity (sizeof (double));

			Buff.BlockCopy (BitConverter.GetBytes (value), 0, this.buffer, this.position, sizeof (double));
			this.position += sizeof (double);
		}

		public void WriteString (Encoding encoding, string value)
		{
			if (encoding == null)
				throw new ArgumentNullException ("encoding");

			byte[] data = encoding.GetBytes (value);
			EnsureAdditionalCapacity (sizeof(int) + data.Length);

			Buff.BlockCopy (BitConverter.GetBytes (data.Length), 0, this.buffer, this.position, sizeof(int));
			this.position += sizeof (int);
			Buff.BlockCopy (data, 0, this.buffer, this.position, data.Length);
			this.position += data.Length;
		}

		public void Flush()
		{
			this.position = 0;
		}

		private byte[] buffer;
		private int position;
		private readonly bool resizing;

		private void EnsureAdditionalCapacity (int additionalCapacity)
		{
			if (this.position + additionalCapacity < this.buffer.Length)
				return;
			if (!this.resizing)
				throw new InternalBufferOverflowException();

			int curLength = this.buffer.Length;
			int newLength = curLength * 2;
			while (newLength <= curLength + additionalCapacity)
				newLength *= 2;

			byte[] newbuffer = new byte[newLength];
			Buff.BlockCopy (this.buffer, 0, newbuffer, 0, Length);
			this.buffer = newbuffer;
		}
	}
}
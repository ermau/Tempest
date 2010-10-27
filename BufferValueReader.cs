//
// BufferValueReader.cs
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
	public class BufferValueReader
		: IValueReader
	{
		private readonly byte[] buffer;
		private readonly int length;

		public BufferValueReader (byte[] buffer, int offset, int length)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			this.buffer = buffer;
			this.Position = offset;
			this.length = length;
		}

		public byte[] Buffer
		{
			get { return this.buffer; }
		}

		public int Position
		{
			get;
			set;
		}

		public BufferValueReader (byte[] buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			this.buffer = buffer;
			this.length = buffer.Length;
		}

		public bool ReadBool()
		{
			if (this.Position == this.length)
				throw new InternalBufferOverflowException();

			return (this.buffer[this.Position++] == 1);
		}

		public byte[] ReadBytes()
		{
			int len = ReadInt32();
			if (this.Position + len > this.length)
				throw new InternalBufferOverflowException();

			byte[] b = new byte[len];
			Array.Copy (this.buffer, this.Position, b, 0, len);
			this.Position += len;

			return b;
		}

		public byte[] ReadBytes (int count)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException ("count", count, "count must be >= 0");
			if (count + this.Position >= this.length)
				throw new ArgumentOutOfRangeException ("count", count, "Count from position is longer than buffer.");

			byte[] b = new byte[count];
			Array.Copy (this.buffer, this.Position, b, 0, count);
			this.Position += count;

			return b;
		}

		public sbyte ReadSByte()
		{
			if (this.Position >= this.length)
				throw new InternalBufferOverflowException();

			return (sbyte)this.buffer[this.Position++];
		}

		public short ReadInt16()
		{
			short v = BitConverter.ToInt16 (this.buffer, this.Position);
			this.Position += sizeof (short);
			
			return v;
		}

		public int ReadInt32()
		{
			int v = BitConverter.ToInt32 (this.buffer, this.Position);
			this.Position += sizeof (int);
			
			return v;
		}

		public long ReadInt64()
		{
			long v = BitConverter.ToInt64 (this.buffer, this.Position);
			this.Position += sizeof (long);
			
			return v;
		}

		public byte ReadByte()
		{
			return this.buffer[this.Position++];
		}

		public ushort ReadUInt16()
		{
			ushort v = BitConverter.ToUInt16 (this.buffer, this.Position);
			this.Position += sizeof (ushort);
			
			return v;
		}

		public uint ReadUInt32()
		{
			uint v = BitConverter.ToUInt32 (this.buffer, this.Position);
			this.Position += sizeof (uint);
			
			return v;
		}

		public ulong ReadUInt64()
		{
			ulong v = BitConverter.ToUInt64 (this.buffer, this.Position);
			this.Position += sizeof (ulong);
			
			return v;
		}

		public decimal ReadDecimal()
		{
			int[] parts = new int[4];
			for (int i = 0; i < parts.Length; ++i)
				parts[i] = ReadInt32 ();

			return new decimal (parts);
		}

		public float ReadSingle()
		{
			float v = BitConverter.ToSingle (this.buffer, this.Position);
			this.Position += sizeof (float);

			return v;
		}

		public double ReadDouble ()
		{
			double v = BitConverter.ToDouble (this.buffer, this.Position);
			this.Position += sizeof (double);

			return v;
		}

		public string ReadString (Encoding encoding)
		{
			if (encoding == null)
				throw new ArgumentNullException ("encoding");

			int len = ReadInt32();
			if (len == 0)
				return null;

			string v = encoding.GetString (this.buffer, this.Position, len);
			this.Position += len;
			
			return v;
		}

		public void Flush()
		{
		}
	}
}
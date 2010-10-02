// The MIT License
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

namespace Tempest
{
	public class BufferValueReader
		: IValueReader
	{
		private int position;
		private readonly byte[] buffer;

		public BufferValueReader (byte[] buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			this.buffer = buffer;
		}

		public bool ReadBool()
		{
			return (this.buffer[this.position++] == 1);
		}

		public byte[] ReadBytes()
		{
			int length = ReadInt32();
			byte[] b = new byte[length];
			Array.Copy (this.buffer, this.position, b, 0, length);
			this.position += length;

			return b;
		}

		public byte[] ReadBytes (int count)
		{
			byte[] b = new byte[count];
			Array.Copy (this.buffer, this.position, b, 0, count);
			this.position += count;

			return b;
		}

		public sbyte ReadSByte()
		{
			return (sbyte)this.buffer[this.position++];
		}

		public short ReadInt16()
		{
			short v = BitConverter.ToInt16 (this.buffer, this.position);
			this.position += sizeof (short);
			
			return v;
		}

		public int ReadInt32()
		{
			int v = BitConverter.ToInt32 (this.buffer, this.position);
			this.position += sizeof (int);
			
			return v;
		}

		public long ReadInt64()
		{
			long v = BitConverter.ToInt64 (this.buffer, this.position);
			this.position += sizeof (long);
			
			return v;
		}

		public byte ReadByte()
		{
			return this.buffer[this.position++];
		}

		public ushort ReadUInt16()
		{
			ushort v = BitConverter.ToUInt16 (this.buffer, this.position);
			this.position += sizeof (ushort);
			
			return v;
		}

		public uint ReadUInt32()
		{
			uint v = BitConverter.ToUInt32 (this.buffer, this.position);
			this.position += sizeof (uint);
			
			return v;
		}

		public ulong ReadUInt64()
		{
			ulong v = BitConverter.ToUInt64 (this.buffer, this.position);
			this.position += sizeof (ulong);
			
			return v;
		}

		public string ReadString (Encoding encoding)
		{
			if (encoding == null)
				throw new ArgumentNullException ("encoding");

			int length = ReadInt32();
			if (length == 0)
				return null;

			string v = encoding.GetString (this.buffer, this.position, length);
			this.position += length;
			
			return v;
		}
	}
}
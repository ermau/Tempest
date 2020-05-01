//
// BufferValueReader.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010-2011 Eric Maupin
// Copyright (c) 2011-2014 Xamarin Inc.
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
using System.Runtime.InteropServices;
using System.Text;
using Buff = System.Buffer;

namespace Tempest
{
#if SAFE
	public class BufferValueReader
#else
	public unsafe class BufferValueReader
#endif
		: IValueReader, IDisposable
	{
		private readonly byte[] buffer;
		private readonly int length;

		public BufferValueReader (byte[] buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			this.buffer = buffer;
			this.length = buffer.Length;

			#if !SAFE
			this.handle = GCHandle.Alloc (buffer, GCHandleType.Pinned);
			this.ptr = (byte*)this.handle.AddrOfPinnedObject();
			#endif
		}

		#if !SAFE
		public BufferValueReader (byte[] buffer, int offset, int length)
			: this (buffer)
		{
			this.ptr += offset;
			this.length = length;
		}
		#endif

		/// <summary>
		/// Gets the underlying buffer.
		/// </summary>
		public byte[] Buffer
		{
			get { return this.buffer; }
		}

		/// <summary>
		/// Gets or sets the position of the reader in the buffer.
		/// </summary>
		public int Position
		{
			#if SAFE
			get { return this.position; }
			set { this.position = value; }
			#else
			get { return (int)((ulong)this.ptr - (ulong)this.handle.AddrOfPinnedObject()); }
			set { this.ptr = (byte*)(this.handle.AddrOfPinnedObject() + value); }
			#endif
		}

		public bool ReadBool()
		{
			#if !SAFE
			return (*this.ptr++ == 1);
			#else
			return (this.buffer[this.position++] == 1);
			#endif
		}

		public byte[] ReadBytes()
		{
			int len = ReadInt32();

			byte[] b = new byte[len];
			Buff.BlockCopy (this.buffer, this.Position, b, 0, len);
			this.Position += len;

			return b;
		}

		public byte[] ReadBytes (int count)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException ("count", "count must be >= 0");

			byte[] b = new byte[count];
			Buff.BlockCopy (this.buffer, Position, b, 0, count);
			Position += count;

			return b;
		}

		public sbyte ReadSByte()
		{
			#if !SAFE
			return *(sbyte*) this.ptr++;
			#else
			return (sbyte)this.buffer[this.position++];
			#endif
		}

		public short ReadInt16()
		{
			#if !SAFE
			short v = *(short*) this.ptr;
			this.ptr += 2;
			#else
			short v = BitConverter.ToInt16 (this.buffer, this.position);
			this.position += sizeof (short);
			#endif
			
			return v;
		}

		public int ReadInt32()
		{
			#if !SAFE
			int v = *((int*)(this.ptr));
			this.ptr += sizeof (int);
			#else
			int v = BitConverter.ToInt32 (this.buffer, this.position);
			this.position += sizeof (int);			
			#endif
			
			return v;
		}

		public long ReadInt64()
		{
			#if !SAFE
			long v = *(long*) this.ptr;
			this.ptr += sizeof (long);
			#else
			long v = BitConverter.ToInt64 (this.buffer, this.position);
			this.position += sizeof (long);
			#endif
			
			return v;
		}

		public byte ReadByte()
		{
			#if !SAFE
			return *this.ptr++;
			#else
			return this.buffer[this.position++];
			#endif
		}

		public ushort ReadUInt16()
		{
			#if !SAFE
			ushort v = *(ushort*) this.ptr;
			this.ptr += sizeof (ushort);
			#else
			ushort v = BitConverter.ToUInt16 (this.buffer, this.position);
			this.position += sizeof (ushort);
			#endif
			
			return v;
		}

		public uint ReadUInt32()
		{
			#if !SAFE
			uint v = *(uint*) this.ptr;
			this.ptr += sizeof (uint);
			#else
			uint v = BitConverter.ToUInt32 (this.buffer, this.position);
			this.position += sizeof (uint);
			#endif
			
			return v;
		}

		public ulong ReadUInt64()
		{
			#if !SAFE
			ulong v = *((ulong*) this.ptr);
			this.ptr += sizeof (long);
			#else
			ulong v = BitConverter.ToUInt64 (this.buffer, this.position);
			this.position += sizeof (ulong);
			#endif
			
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
			#if !SAFE
			float v = *(float*) this.ptr;
			this.ptr += sizeof (float);
			#else
			float v = BitConverter.ToSingle (this.buffer, this.position);
			this.position += sizeof (float);
			#endif

			return v;
		}

		public double ReadDouble ()
		{
			#if !SAFE
			double v = *(double*) this.ptr;
			this.ptr += sizeof (double);
			#else
			double v = BitConverter.ToDouble (this.buffer, this.position);
			this.position += sizeof (double);
			#endif

			return v;
		}

		public string ReadString (Encoding encoding)
		{
			if (encoding == null)
				throw new ArgumentNullException ("encoding");

			int len = this.Read7BitEncodedInt();
			if (len == -1)
				return null;

			string v = encoding.GetString (this.buffer, Position, len);
			Position += len;
			
			return v;
		}

		public void Flush()
		{
		}

		public void Dispose()
		{
			#if !SAFE
			this.handle.Free();
			#endif
		}

		#if !SAFE
		private GCHandle handle;
		private byte* ptr;
		#else
		private int position;
		#endif
	}
}
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
	public interface IValueReader
	{
		/// <summary>
		/// Reads a boolean from the transport.
		/// </summary>
		bool ReadBool();

		/// <summary>
		/// Reads an array of unsigned bytes from the transport.
		/// </summary>
		byte[] ReadBytes ();

		/// <summary>
		/// Reads the next <paramref name="count"/> bytes from the transport.
		/// </summary>
		/// <param name="count">The number of bytes to read.</param>
		byte[] ReadBytes (int count);

		/// <summary>
		/// Reads a signed byte (SByte) from the transport.
		/// </summary>
		SByte ReadSByte ();

		/// <summary>
		/// Reads a signed short (Int16) from the transport.
		/// </summary>
		Int16 ReadInt16 ();

		/// <summary>
		/// Reads a signed integer (Int32) from the transport.
		/// </summary>
		Int32 ReadInt32 ();

		/// <summary>
		/// Reads a signed long (Int64) from the transport.
		/// </summary>
		Int64 ReadInt64 ();

		/// <summary>
		/// Reads an unsigned byte (Byte) from the transport.
		/// </summary>
		Byte ReadByte ();
		
		/// <summary>
		/// Reads an unsigned integer (UInt16) from the transport.
		/// </summary>
		/// <returns></returns>
		UInt16 ReadUInt16 ();

		/// <summary>
		/// Reads an unsigned integer (UInt32) from the transport.
		/// </summary>
		/// <returns></returns>
		UInt32 ReadUInt32 ();
		
		/// <summary>
		/// Reads an unsigned long (UInt64) from the transport.
		/// </summary>
		UInt64 ReadUInt64 ();
		
		/// <summary>
		/// Reads a string with <paramref name="encoding"/> from the transport.
		/// </summary>
		/// <param name="encoding">The encoding of the string.</param>
		string ReadString (Encoding encoding);
	}
}
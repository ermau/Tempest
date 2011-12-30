//
// MessageHeader.cs
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

namespace Tempest
{
	public enum HeaderState
	{
		Protocol = 1,
		Type = 2,
		Length = 3,
		IV = 4,
		MessageId = 5,
		TypeMap = 6
	}

	public class MessageHeader
	{
		public HeaderState State
		{
			get;
			set;
		}

		public Protocol Protocol
		{
			get;
			internal set;
		}

		public Message Message
		{
			get;
			internal set;
		}

		/// <summary>
		/// Gets the total length of the message (including <see cref="HeaderLength"/>).
		/// </summary>
		public int MessageLength
		{
			get;
			internal set;
		}

		public bool HasTypeHeader
		{
			get;
			internal set;
		}

		public ushort TypeHeaderLength
		{
			get;
			internal set;
		}

		public int HeaderLength
		{
			get;
			internal set;
		}

		public ISerializationContext SerializationContext
		{
			get;
			internal set;
		}

		public byte[] IV
		{
			get;
			internal set;
		}

		public bool IsStillEncrypted
		{
			get;
			internal set;
		}

		/// <summary>
		/// Gets whether the message is a response to another message or not.
		/// </summary>
		public bool IsResponse
		{
			get;
			internal set;
		}

		public int MessageId
		{
			get;
			internal set;
		}
	}
}
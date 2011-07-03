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
	public class MessageHeader
	{
		internal MessageHeader (Protocol protocol, Message message, int messageLength, int headerLength, ISerializationContext serializationContext, byte[] iv)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			if (message == null)
				throw new ArgumentNullException ("message");
			if (serializationContext == null)
				throw new ArgumentNullException ("serializationContext");

			Protocol = protocol;
			Message = message;
			MessageLength = messageLength;
			HeaderLength = headerLength;
			SerializationContext = serializationContext;
			IV = iv;
		}

		public Protocol Protocol
		{
			get;
			private set;
		}

		public Message Message
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the total length of the message (including <see cref="HeaderLength"/>).
		/// </summary>
		public int MessageLength
		{
			get;
			private set;
		}

		public int HeaderLength
		{
			get;
			private set;
		}

		public ISerializationContext SerializationContext
		{
			get;
			private set;
		}

		public byte[] IV
		{
			get;
			private set;
		}
	}
}
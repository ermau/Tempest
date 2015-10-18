//
// MessageHeader.cs
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

namespace Tempest
{
	public enum HeaderState
	{
		/// <summary>
		/// Default state
		/// </summary>
		Empty = 0,

		/// <summary>
		/// The protocol identifier
		/// </summary>
		Protocol = 1,

		/// <summary>
		/// The connection ID
		/// </summary>
		CID = 2,

		/// <summary>
		/// The message type
		/// </summary>
		Type = 3,

		/// <summary>
		/// The message length
		/// </summary>
		Length = 4,

		/// <summary>
		/// The encryption initialization vector
		/// </summary>
		IV = 5,

		/// <summary>
		/// The message ID
		/// </summary>
		MessageId = 6,

		/// <summary>
		/// The message ID of a message being responded to.
		/// </summary>
		ResponseMessageId = 7,

		/// <summary>
		/// The header has been completely read.
		/// </summary>
		Complete = 8
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
			set;
		}

		public Message Message
		{
			get;
			set;
		}

		/// <summary>
		/// Gets the total length of the message (including <see cref="HeaderLength"/>).
		/// </summary>
		public int MessageLength
		{
			get;
			set;
		}

		public int HeaderLength
		{
			get;
			set;
		}

		public ISerializationContext SerializationContext
		{
			get;
			set;
		}

		public byte[] IV
		{
			get;
			set;
		}

		public bool IsStillEncrypted
		{
			get;
			set;
		}

		/// <summary>
		/// Gets whether the message is a response to another message or not.
		/// </summary>
		public bool IsResponse
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the message being responded to
		/// </summary>
		public int ResponseMessageId
		{
			get;
			set;
		}

		public int MessageId
		{
			get;
			set;
		}

		public int ConnectionId
		{
			get;
			set;
		}
	}
}
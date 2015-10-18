//
// Message.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010-2011 Eric Maupin
// Copyright (c) 2011-2015 Xamarin Inc.
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
	[System.Diagnostics.DebuggerDisplay ("{Header.MessageId}:{GetType()}")]
	public abstract class Message
	{
		protected Message (Protocol protocol, ushort messageType)
		{
			if (ReferenceEquals (protocol, null))
				throw new ArgumentNullException ("protocol");

			Protocol = protocol;
			MessageType = messageType;
		}

		/// <summary>
		/// Gets whether this message must be reliably delivered or not.
		/// </summary>
		public virtual bool MustBeReliable
		{
			get { return true; }
		}

		/// <summary>
		/// Gets whether this message prefers reliable delivery if possible.
		/// </summary>
		public virtual bool PreferReliable
		{
			get { return true; }
		}

		/// <summary>
		/// Gets whether this message can be received without forming a connection.
		/// </summary>
		public virtual bool AcceptedConnectionlessly
		{
			get { return false; }
		}

		/// <summary>
		/// Gets whether this message is hashed and signed.
		/// </summary>
		public virtual bool Authenticated
		{
			get { return false; }
		}

		/// <summary>
		/// Gets whether this message is encrypted.
		/// </summary>
		public virtual bool Encrypted
		{
			get { return false; }
		}

		/// <summary>
		/// Gets the protocol this message belongs to.
		/// </summary>
		public Protocol Protocol
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the unique identifier for this message within its protocol.
		/// </summary>
		public ushort MessageType
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets or sets information about the message for provider implementations.
		/// </summary>
		public MessageHeader Header
		{
			get;
			set;
		}

		/// <summary>
		/// Writes the message payload with <paramref name="writer"/>.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="writer">The writer to use for writing the payload.</param>
		/// <exception cref="ArgumentNullException"><paramref name="writer"/> is <c>null</c>.</exception>
		public abstract void WritePayload (ISerializationContext context, IValueWriter writer);

		/// <summary>
		/// Reads the message payload with <paramref name="reader"/>.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="reader">The reader to use for reading the payload.</param>
		/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <c>null</c>.</exception>
		public abstract void ReadPayload (ISerializationContext context, IValueReader reader);
	}
}
//
// PartialMessage.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2013 Eric Maupin
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

namespace Tempest.InternalProtocol
{
	public class PartialMessage
		: TempestMessage
	{
		public PartialMessage()
			: base (TempestMessageType.Partial)
		{
		}

		public ushort OriginalMessageId
		{
			get;
			set;
		}

		public byte Count
		{
			get;
			set;
		}

		public byte[] Payload
		{
			get { return this.payload; }
			set
			{
				if (value == null)
					throw new ArgumentNullException ("value");

				this.payload = value;
				this.offset = 0;
				this.length = value.Length;
			}
		}

		public void SetPayload (byte[] msgPayload, int payloadOffset, int payloadLength)
		{
			if (msgPayload == null)
				throw new ArgumentNullException ("msgPayload");

			this.payload = msgPayload;
			this.offset = payloadOffset;
			this.length = payloadLength;
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteUInt16 (OriginalMessageId);
			writer.WriteByte (Count);

			writer.WriteInt32 (this.length);
			writer.WriteBytes (Payload, this.offset, this.length);
		}

		public override void ReadPayload (ISerializationContext context, IValueReader reader)
		{
			OriginalMessageId = reader.ReadUInt16();
			Count = reader.ReadByte();

			int payloadLength = reader.ReadInt32();
			Payload = reader.ReadBytes (payloadLength);
		}

		private int offset;
		private int length;
		private byte[] payload;
	}
}
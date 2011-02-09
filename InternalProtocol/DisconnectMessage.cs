//
// DisconnectMessage.cs
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

namespace Tempest.InternalProtocol
{
	/// <summary>
	/// Internal Tempest protocol disconnect with reason message.
	/// </summary>
	public class DisconnectMessage
		: TempestMessage
	{
		/// <summary>
		/// Initializes a new instance of <see cref="DisconnectMessage"/>.
		/// </summary>
		public DisconnectMessage()
			: base (TempestMessageType.Disconnect)
		{
		}

		/// <summary>
		/// Gets or sets the reason for disconnection.
		/// </summary>
		public DisconnectedReason Reason
		{
			get;
			set;
		}

		public override void WritePayload (IValueWriter writer)
		{
			writer.WriteInt32 ((int)Reason);
		}

		public override void ReadPayload (IValueReader reader)
		{
			Reason = (DisconnectedReason)reader.ReadInt32();
		}
	}
}
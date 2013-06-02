//
// MockMessage.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tempest.Tests
{
	public class MockProtocol2
	{
		public static Protocol Instance
		{
			get { return p; }
		}

		private static readonly Protocol p;

		static MockProtocol2()
		{
			p = new Protocol (3);
			p.Register (new[]
			{
				new KeyValuePair<Type, Func<Message>> (typeof (MockMessage2), () => new MockMessage2())
			});
		}
	}

	public class MockMessage2
		: Message
	{
		public MockMessage2 ()
			: base (MockProtocol2.Instance, 1)
		{
		}

		public string Content
		{
			get; set;
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Encoding.UTF8, Content);
		}

		public override void ReadPayload (ISerializationContext context, IValueReader reader)
		{
			Content = reader.ReadString (Encoding.UTF8);
		}
	}

	public class MockProtocol
	{
		public static Protocol Instance
		{
			get { return p; }
		}

		private static readonly Protocol p;
		static MockProtocol()
		{
			p = new Protocol (2);
			p.Register (new[]
			{
				new KeyValuePair<Type, Func<Message>> (typeof (MockMessage), () => new MockMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof (BlankMessage), () => new BlankMessage()), 
				new KeyValuePair<Type, Func<Message>> (typeof (AuthenticatedMessage), () => new AuthenticatedMessage()), 
				new KeyValuePair<Type, Func<Message>> (typeof (EncryptedMessage), () => new EncryptedMessage()), 
				new KeyValuePair<Type, Func<Message>> (typeof (AuthenticatedAndEncryptedMessage), () => new AuthenticatedAndEncryptedMessage()), 
			});
		}
	}

	public class UnreliableMockMessage
		: Message
	{
		public UnreliableMockMessage()
			: base (MockProtocol.Instance, 9)
		{
		}

		public override bool MustBeReliable
		{
			get { return false; }
		}

		public override bool AcceptedConnectionlessly
		{
			get { return true; }
		}

		public override bool PreferReliable
		{
			get { return false; }
		}

		public string Content
		{
			get; set;
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Encoding.UTF8, Content);
		}

		public override void ReadPayload (ISerializationContext context, IValueReader reader)
		{
			Content = reader.ReadString (Encoding.UTF8);
		}
	}

	public class MockMessage
		: Message
	{
		public MockMessage ()
			: base (MockProtocol.Instance, 1)
		{
		}

		public string Content
		{
			get; set;
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Encoding.UTF8, Content);
		}

		public override void ReadPayload (ISerializationContext context, IValueReader reader)
		{
			Content = reader.ReadString (Encoding.UTF8);
		}
	}

	public abstract class ContentMessage
		: Message
	{
		protected ContentMessage (ushort id)
			: base (MockProtocol.Instance, id)
		{
		}

		public string Message
		{
			get;
			set;
		}

		public int Number
		{
			get;
			set;
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Message);
			writer.WriteInt32 (Number);
		}

		public override void ReadPayload (ISerializationContext context, IValueReader reader)
		{
			Message = reader.ReadString();
			Number = reader.ReadInt32();
		}
	}

	public class AuthenticatedMessage
		: ContentMessage
	{
		public AuthenticatedMessage()
			: base (3)
		{
		}

		public override bool Authenticated
		{
			get { return true; }
		}
	}

	public class EncryptedMessage
		: ContentMessage
	{
		public EncryptedMessage()
			: base (4)
		{
		}

		public override bool Encrypted
		{
			get { return true; }
		}
	}

	public class AuthenticatedAndEncryptedMessage
		: ContentMessage
	{
		public AuthenticatedAndEncryptedMessage()
			: base (5)
		{
		}

		public override bool Authenticated
		{
			get { return true; }
		}

		public override bool Encrypted
		{
			get { return true; }
		}
	}

	public class BlankMessage
		: Message
	{
		public BlankMessage()
			: base (MockProtocol.Instance, 2)
		{
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
		}

		public override void ReadPayload (ISerializationContext context, IValueReader reader)
		{
		}
	}
}
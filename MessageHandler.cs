//
// MessageHandler.cs
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
using Cadenza.Collections;

namespace Tempest
{
	public abstract class MessageHandler
		: IContext
	{
		private readonly MutableLookup<MessageType, Action<MessageEventArgs>> handlers = new MutableLookup<MessageType, Action<MessageEventArgs>>();

		void IContext.RegisterMessageHandler (Protocol protocol, ushort messageType, Action<MessageEventArgs> handler)
		{
			lock (this.handlers)
				this.handlers.Add (new MessageType (protocol, messageType), handler);
		}

		protected List<Action<MessageEventArgs>> GetHandlers (Message message)
		{
			List<Action<MessageEventArgs>> mhandlers = null;
			lock (this.handlers)
			{
				IEnumerable<Action<MessageEventArgs>> thandlers;
				if (this.handlers.TryGetValues (new MessageType (message), out thandlers))
					mhandlers = thandlers.ToList();
			}

			return mhandlers;
		}

		private class MessageType
			: IEquatable<MessageType>
		{
			public MessageType (Protocol protocol, ushort messageType)
			{
				this.protocol = protocol;
				this.messageType = messageType;
			}

			public MessageType (Message msg)
			{
				this.protocol = msg.Protocol;
				this.messageType = msg.MessageType;
			}

			private readonly Protocol protocol;
			private readonly ushort messageType;

			public override bool Equals(object obj)
			{
				if (ReferenceEquals (null, obj))
					return false;
				if (ReferenceEquals (this, obj))
					return true;
				if (obj.GetType() != typeof (MessageType))
					return false;
				return Equals ((MessageType)obj);
			}

			public bool Equals (MessageType other)
			{
				if (ReferenceEquals (null, other))
					return false;
				if (ReferenceEquals (this, other))
					return true;
				return Equals (other.protocol, this.protocol) && other.messageType == this.messageType;
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return (this.protocol.GetHashCode() * 397) ^ this.messageType.GetHashCode();
				}
			}

			public static bool operator == (MessageType left, MessageType right)
			{
				return Equals (left, right);
			}

			public static bool operator != (MessageType left, MessageType right)
			{
				return !Equals (left, right);
			}
		}
	}
}
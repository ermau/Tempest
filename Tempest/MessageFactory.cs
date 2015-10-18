//
// MessageFactory.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2009-2012 Eric Maupin
// Copyright (c) 2012-2015 Xamarin Inc.
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
using System.Reflection;
using System.Collections.Concurrent;

namespace Tempest
{
	public class MessageFactory
	{
		internal MessageFactory()
		{
		}

		public bool RequiresHandshake
		{
			get;
			private set;
		}

		/// <summary>
		/// Registers types with a method of construction.
		/// </summary>
		/// <param name="messageTypes">The types to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="messageTypes"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="messageTypes"/> contains non-implementations of <see cref="Message"/>
		/// or <paramref name="messageTypes"/> contains duplicate <see cref="Message.MessageType"/>s.</exception>
		public void Register (IEnumerable<KeyValuePair<Type, Func<Message>>> messageTypes)
		{
			RegisterTypesWithCtors (messageTypes, false);
		}

		/// <summary>
		/// Creates a new instance of the <paramref name="messageType"/>.
		/// </summary>
		/// <param name="messageType">The unique message identifier in the protocol for the desired message.</param>
		/// <returns>A new instance of the <paramref name="messageType"/>, or <c>null</c> if this type has not been registered.</returns>
		public Message Create (ushort messageType)
		{
			Func<Message> mCtor;
			if (!this.messageCtors.TryGetValue (messageType, out mCtor))
				return null;

			return mCtor();
		}

		private readonly ConcurrentDictionary<ushort, Func<Message>> messageCtors = new ConcurrentDictionary<ushort, Func<Message>>();

		private void RegisterTypesWithCtors (IEnumerable<KeyValuePair<Type, Func<Message>>> messageTypes, bool ignoreDupes)
		{
			if (messageTypes == null)
				throw new ArgumentNullException ("messageTypes");

			TypeInfo mtype = typeof (Message).GetTypeInfo();

			foreach (var kvp in messageTypes)
			{
				if (!mtype.IsAssignableFrom (kvp.Key.GetTypeInfo()))
					throw new ArgumentException (String.Format ("{0} is not an implementation of Message", kvp.Key.Name), "messageTypes");
				if (kvp.Key.GetTypeInfo().IsGenericType || kvp.Key.GetTypeInfo().IsGenericTypeDefinition)
					throw new ArgumentException (String.Format ("{0} is a generic type which is unsupported", kvp.Key.Name), "messageTypes");
					
				Message m = kvp.Value();
				if (m.Protocol != (Protocol)this)
					continue;

				if (m.Authenticated || m.Encrypted)
					RequiresHandshake = true;

				if (!this.messageCtors.TryAdd (m.MessageType, kvp.Value)) {
					if (ignoreDupes)
						continue;

					throw new ArgumentException (String.Format ("A message of type {0} has already been registered.", m.MessageType), "messageTypes");
				}
			}
		}

		private static readonly Type[] EmptyTypes = new Type[0];
	}
}
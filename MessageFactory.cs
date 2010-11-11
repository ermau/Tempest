//
// MessageFactory.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if !SAFE
using System.Reflection.Emit;
#endif
#if NET_4
using System.Collections.Concurrent;
#endif

namespace Tempest
{
	public class MessageFactory
	{
		internal MessageFactory()
		{
		}

		/// <summary>
		/// Discovers and registers message types from <paramref name="assembly"/>.
		/// </summary>
		/// <param name="protocol">The protocol to discover messages for.</param>
		/// <param name="assembly">The assembly to discover message types from.</param>
		/// <seealso cref="Discover()"/>
		/// <exception cref="ArgumentNullException"><paramref name="assembly"/> or <paramref name="protocol"/> is <c>null</c>.</exception>
		public void Discover (Protocol protocol, Assembly assembly)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			if (assembly == null)
				throw new ArgumentNullException ("assembly");
			
			Type mtype = typeof (Message);
			RegisterTypes (protocol, assembly.GetTypes().Where (t => mtype.IsAssignableFrom (t) && t.GetConstructor (Type.EmptyTypes) != null), true);
		}

		/// <summary>
		/// Discovers and registers messages from the calling assembly.
		/// </summary>
		/// <seealso cref="Discover(Tempest.Protocol,System.Reflection.Assembly)"/>
		/// <exception cref="ArgumentNullException"><paramref name="protocol"/> is <c>null</c>.</exception>
		public void Discover (Protocol protocol)
		{
			Discover (protocol, Assembly.GetCallingAssembly());
		}

		/// <summary>
		/// Registers types with a method of construction.
		/// </summary>
		/// <param name="protocol">Protocol to register messages for.</param>
		/// <param name="messageTypes">The types to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="messageTypes"/> or <paramref name="protocol"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="messageTypes"/> contains non-implementations of <see cref="Message"/>
		/// or <paramref name="messageTypes"/> contains duplicate <see cref="Message.MessageType"/>s.</exception>
		public void Register (Protocol protocol, IEnumerable<KeyValuePair<Type, Func<Message>>> messageTypes)
		{
			RegisterTypesWithCtors (protocol, messageTypes, false);
		}

		#if !SAFE

		/// <summary>
		/// Registers <paramref name="messageTypes"/> with their parameter-less constructor.
		/// </summary>
		/// <param name="protocol">Protocol to register messages for.</param>
		/// <param name="messageTypes">The types to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="protocol"/> or <paramref name="messageTypes"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="messageTypes"/> contains a type that is not an implementation of <see cref="Message"/>,
		/// has no parameter-less constructor or contains duplicate <see cref="Message.MessageType"/>s.
		/// </exception>
		public void Register (Protocol protocol, IEnumerable<Type> messageTypes)
		{
			RegisterTypes (protocol, messageTypes, false);
		}
		#endif

		/// <summary>
		/// Creates a new instance of the <paramref name="messageType"/>.
		/// </summary>
		/// <param name="protocol">The protocol to create a message for.</param>
		/// <param name="messageType">The unique message identifier in the protocol for the desired message.</param>
		/// <returns>A new instance of the <paramref name="messageType"/>, or <c>null</c> if this type has not been registered.</returns>
		public Message Create (Protocol protocol, ushort messageType)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");

			ProtocolMessageKey key = new ProtocolMessageKey (protocol, messageType);

			Func<Message> mCtor;
			#if !NET_4
			lock (this.messageCtors)
			#endif
				if (!this.messageCtors.TryGetValue (key, out mCtor))
					return null;

			return mCtor();
		}

		#if !NET_4
		private readonly Dictionary<ProtocolMessageKey, Func<Message>> messageCtors = new Dictionary<ProtocolMessageKey, Func<Message>>();
		#else
		private readonly ConcurrentDictionary<ProtocolMessageKey, Func<Message>> messageCtors = new ConcurrentDictionary<ProtocolMessageKey, Func<Message>>();
		#endif

		private class ProtocolMessageKey
			: IEquatable<ProtocolMessageKey>
		{
			private readonly Protocol protocol;
			private readonly ushort messageId;

			public ProtocolMessageKey (Protocol protocol, ushort messageId)
			{
				this.protocol = protocol;
				this.messageId = messageId;
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals (null, obj))
					return false;
				if (ReferenceEquals (this, obj))
					return true;
				if (obj.GetType() != typeof (ProtocolMessageKey))
					return false;
				return Equals ((ProtocolMessageKey)obj);
			}

			public bool Equals (ProtocolMessageKey other)
			{
				if (ReferenceEquals (null, other))
					return false;
				if (ReferenceEquals (this, other))
					return true;
				return Equals (other.protocol, protocol) && other.messageId == messageId;
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return (protocol.GetHashCode() * 397) ^ messageId.GetHashCode();
				}
			}

			public static bool operator == (ProtocolMessageKey left, ProtocolMessageKey right)
			{
				return Equals (left, right);
			}

			public static bool operator != (ProtocolMessageKey left, ProtocolMessageKey right)
			{
				return !Equals (left, right);
			}
		}

		private void RegisterTypes (Protocol protocol, IEnumerable<Type> messageTypes, bool ignoreDupes)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			if (messageTypes == null)
				throw new ArgumentNullException ("messageTypes");
			
			Type mtype = typeof (Message);

			Dictionary<Type, Func<Message>> types = new Dictionary<Type, Func<Message>>();
			foreach (Type t in messageTypes)
			{
				if (!mtype.IsAssignableFrom (t))
					throw new ArgumentException (String.Format ("{0} is not an implementation of Message", t.Name), "messageTypes");

				ConstructorInfo plessCtor = t.GetConstructor (Type.EmptyTypes);
				if (plessCtor == null)
					throw new ArgumentException (String.Format ("{0} has no parameter-less constructor", t.Name), "messageTypes");

				var dplessCtor = new DynamicMethod ("plessCtor", mtype, Type.EmptyTypes);
				var il = dplessCtor.GetILGenerator();
				il.Emit (OpCodes.Newobj, plessCtor);
				il.Emit (OpCodes.Ret);

				types.Add (t, (Func<Message>)dplessCtor.CreateDelegate (typeof (Func<Message>)));
			}

			RegisterTypesWithCtors (protocol, types, ignoreDupes);
		}

		private void RegisterTypesWithCtors (Protocol protocol, IEnumerable<KeyValuePair<Type, Func<Message>>> messageTypes, bool ignoreDupes)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			if (messageTypes == null)
				throw new ArgumentNullException ("messageTypes");

			Type mtype = typeof (Message);

			#if !NET_4
			lock (messageCtors)
			#endif
				foreach (var kvp in messageTypes)
				{
					if (!mtype.IsAssignableFrom (kvp.Key))
						throw new ArgumentException (String.Format ("{0} is not an implementation of Message", kvp.Key.Name), "messageTypes");

					Message m = kvp.Value();
					ProtocolMessageKey key = new ProtocolMessageKey (protocol, m.MessageType);
					#if !NET_4
					if (this.messageCtors.ContainsKey (key))
					{
						if (ignoreDupes)
							continue;

						throw new ArgumentException (String.Format ("A message of type {0} has already been registered.", m.MessageType), "messageTypes");
					}
					
					this.messageCtors.Add (key, kvp.Value);
					#else
					if (!this.messageCtors.TryAdd (key, kvp.Value))
					{
						if (ignoreDupes)
							continue;

						throw new ArgumentException (String.Format ("A message of type {0} has already been registered.", m.MessageType), "messageTypes");
					}
					#endif
				}
		}
	}
}
//
// MessageFactory.cs
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
using System.Reflection;
#if !SAFE
using System.Reflection.Emit;
#endif
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

		#if !NOEMIT
		/// <summary>
		/// Discovers and registers message types from <paramref name="assembly"/>.
		/// </summary>
		/// <param name="assembly">The assembly to discover message types from.</param>
		/// <seealso cref="Discover()"/>
		/// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <c>null</c>.</exception>
		public void Discover (Assembly assembly)
		{
			if (assembly == null)
				throw new ArgumentNullException ("assembly");
			
			Type mtype = typeof (Message);
			RegisterTypes (assembly.GetTypes().Where (t =>
			{
				var ti = t.GetTypeInfo();
				return !ti.IsGenericType
				       && !ti.IsGenericTypeDefinition
				       && mtype.IsAssignableFrom (t)
				       && t.GetConstructor (EmptyTypes) != null;
			}), true);
		}

		#if !NETFX_CORE
		/// <summary>
		/// Discovers and registers messages from the calling assembly.
		/// </summary>
		/// <seealso cref="Discover(System.Reflection.Assembly)"/>
		public void Discover()
		{
			Discover (Assembly.GetCallingAssembly());
		}
		#endif

		/// <summary>
		/// Discovers and registers messages from the assembly of type <typeparamref name="T"/>.
		/// </summary>
		/// <typeparam name="T">The type who's assembly to discover messages from.</typeparam>
		public void DiscoverFromAssemblyOf<T>()
		{
			Discover (typeof (T).GetTypeInfo().Assembly);
		}
		#endif

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

		#if !NOEMIT
		/// <summary>
		/// Registers <paramref name="messageTypes"/> with their parameter-less constructor.
		/// </summary>
		/// <param name="messageTypes">The types to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="messageTypes"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="messageTypes"/> contains a type that is not an implementation of <see cref="Message"/>,
		/// has no parameter-less constructor or contains duplicate <see cref="Message.MessageType"/>s.
		/// </exception>
		public void Register (IEnumerable<Type> messageTypes)
		{
			RegisterTypes (messageTypes, false);
		}
		#endif

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

		#if !NOEMIT
		private void RegisterTypes (IEnumerable<Type> messageTypes, bool ignoreDupes)
		{
		    if (messageTypes == null)
		        throw new ArgumentNullException ("messageTypes");
			
		    Type mtype = typeof (Message);

		    var types = new Dictionary<Type, Func<Message>>();
		    foreach (Type t in messageTypes)
		    {
		        if (!mtype.IsAssignableFrom (t))
		            throw new ArgumentException (String.Format ("{0} is not an implementation of Message", t.Name), "messageTypes");
				if (mtype.IsGenericType || mtype.IsGenericTypeDefinition)
					throw new ArgumentException (String.Format ("{0} is a generic type which is unsupported", t.Name), "messageTypes");

		        ConstructorInfo plessCtor = t.GetConstructor (Type.EmptyTypes);
		        if (plessCtor == null)
		            throw new ArgumentException (String.Format ("{0} has no parameter-less constructor", t.Name), "messageTypes");

		        var dplessCtor = new DynamicMethod ("plessCtor", mtype, Type.EmptyTypes);
		        var il = dplessCtor.GetILGenerator();
		        il.Emit (OpCodes.Newobj, plessCtor);
		        il.Emit (OpCodes.Ret);

		        types.Add (t, (Func<Message>)dplessCtor.CreateDelegate (typeof (Func<Message>)));
		    }

		    RegisterTypesWithCtors (types, ignoreDupes);
		}
		#endif

		private void RegisterTypesWithCtors (IEnumerable<KeyValuePair<Type, Func<Message>>> messageTypes, bool ignoreDupes)
		{
			if (messageTypes == null)
				throw new ArgumentNullException ("messageTypes");

			Type mtype = typeof (Message);

			foreach (var kvp in messageTypes)
			{
				if (!mtype.IsAssignableFrom (kvp.Key))
					throw new ArgumentException (String.Format ("{0} is not an implementation of Message", kvp.Key.Name), "messageTypes");
				if (kvp.Key.GetTypeInfo().IsGenericType || kvp.Key.GetTypeInfo().IsGenericTypeDefinition)
					throw new ArgumentException (String.Format ("{0} is a generic type which is unsupported", kvp.Key.Name), "messageTypes");
					
				Message m = kvp.Value();
				if (m.Protocol != (Protocol)this)
					continue;

				if (m.Authenticated || m.Encrypted)
					RequiresHandshake = true;

				if (!this.messageCtors.TryAdd (m.MessageType, kvp.Value))
				{
					if (ignoreDupes)
						continue;

					throw new ArgumentException (String.Format ("A message of type {0} has already been registered.", m.MessageType), "messageTypes");
				}
			}
		}

		private static readonly Type[] EmptyTypes = new Type[0];
	}
}
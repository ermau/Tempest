//
// TypeMap.cs
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

namespace Tempest
{
	/// <summary>
	/// Represents a map of <see cref="Type"/>s to identifiers for shorter bitstream references.
	/// </summary>
	public sealed class TypeMap
		: ISerializable
	{
		public TypeMap()
		{
		}

		public TypeMap (IDictionary<Type, ushort> mapping)
			: this (new Dictionary<Type, ushort> (mapping))
		{
		}

		internal TypeMap (Dictionary<Type, ushort> mapping)
		{
			if (mapping == null)
				throw new ArgumentNullException ("mapping");

			this.map = mapping;

			this.newMappings = new Dictionary<Type, ushort> (mapping.Count);
			this.reverseMap = new Dictionary<ushort, Type> (mapping.Count);
			foreach (var kvp in mapping)
			{
				this.newMappings.Add (kvp.Key, kvp.Value);
				this.reverseMap.Add (kvp.Value, kvp.Key);
			}
		}

		/// <summary>
		/// Gets the <see cref="Type"/>s and their IDs that have been added since <see cref="TryGetNewTypes"/> was last called.
		/// </summary>
		public bool TryGetNewTypes (out IList<KeyValuePair<Type, ushort>> types)
		{
			types = null;

			if (this.newMappings == null || this.newMappings.Count == 0)
				return false;

			lock (this.sync)
			{
				types = this.newMappings.OrderBy (kvp => kvp.Value).ToArray();
				this.newMappings.Clear();
			}

			return true;
		}

		/// <summary>
		/// Attempts to get the <paramref name="id"/> of the <paramref name="type"/>, or assigns a new one.
		/// </summary>
		/// <param name="type">The type to lookup the <paramref name="id"/> for.</param>
		/// <param name="id">The id of the <paramref name="type"/>.</param>
		/// <returns><c>true</c> if the type is new and needs to be transmitted, <c>false</c> otherwise.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
		public bool GetTypeId (Type type, out ushort id)
		{
			lock (this.sync)
			{
				id = 0;
				bool found;
				if (this.map == null)
				{
					found = false;
					this.map = new Dictionary<Type, ushort>();
					this.reverseMap = new Dictionary<ushort, Type>();
					this.newMappings = new Dictionary<Type, ushort>();
				}
				else
					found = this.map.TryGetValue (type, out id);

				if (!found)
				{
					this.newMappings.Add (type, id = this.nextId);
					this.reverseMap.Add (this.nextId, type);
					this.map.Add (type, this.nextId++);
				}

				return !found;
			}
		}

		/// <summary>
		/// Attempts to get the <paramref name="type"/> for <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The id to search for.</param>
		/// <param name="type">The type, if found.</param>
		/// <returns><c>true</c> if the type was found</returns>
		public bool TryGetType (ushort id, out Type type)
		{
			type = null;
			if (this.reverseMap == null)
				return false;

			lock (this.sync)
				return this.reverseMap.TryGetValue (id, out type);
		}

		public void Serialize (ISerializationContext context, IValueWriter writer)
		{
			if (this.map == null)
			{
				writer.WriteUInt16 (0);
				return;
			}

			lock (this.sync)
			{
				writer.WriteUInt16 ((ushort)this.map.Count);
				foreach (var kvp in this.map.OrderBy (kvp => kvp.Value))
					writer.WriteString (kvp.Key.GetSimplestName());
			}
		}

		public void Deserialize (ISerializationContext context, IValueReader reader)
		{
			ushort count = reader.ReadUInt16();
			if (count == 0)
				return;

			lock (this.sync)
			{
				if (this.map == null)
				{
					this.map = new Dictionary<Type, ushort>();
					this.reverseMap = new Dictionary<ushort, Type>();
					this.newMappings = new Dictionary<Type, ushort>();
				}

				for (ushort i = 0; i < count; ++i)
				{
					Type t = Type.GetType (reader.ReadString());
					this.map.Add (t, i);
					this.reverseMap.Add (i, t);
				}
			}
		}

		private ushort nextId = 0;
		private readonly object sync = new object();
		private Dictionary<ushort, Type> reverseMap;
		private Dictionary<Type, ushort> map;
		private Dictionary<Type, ushort> newMappings;
	}
}
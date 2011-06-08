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
	public class TypeMap
	{
		/// <summary>
		/// Gets the <see cref="Type"/>s and their IDs that have been added since <see cref="GetNewTypes"/> was last called.
		/// </summary>
		public IEnumerable<KeyValuePair<Type, int>> GetNewTypes()
		{
			List<KeyValuePair<Type, int>> newTypes;
			lock (this.map)
			{
				newTypes = this.newMappigns.ToList();
				this.newMappigns.Clear();
			}

			return newTypes;
		}

		/// <summary>
		/// Attempts to get the <paramref name="id"/> of the <paramref name="type"/>, or assigns a new one.
		/// </summary>
		/// <param name="type">The type to lookup the <paramref name="id"/> for.</param>
		/// <param name="id">The id of the <paramref name="type"/>.</param>
		/// <returns><c>true</c> if the type is new and needs to be transmitted, <c>false</c> otherwise.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
		public bool TryGetTypeId (Type type, out int id)
		{
			lock (this.map)
			{
				bool found = this.map.TryGetValue (type, out id);
				if (!found)
				{
					this.newMappigns.Add (type, id = this.nextId);
					this.map.Add (type, this.nextId++);
				}

				return !found;
			}
		}

		private int nextId = 0;
		private readonly Dictionary<Type, int> map = new Dictionary<Type, int>();
		private readonly Dictionary<Type, int> newMappigns = new Dictionary<Type, int>();
	}
}
//
// ISerializationContext.cs
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

namespace Tempest
{
	/// <summary>
	/// Contract representing the context for a given serialization.
	/// </summary>
	/// <seealso cref="ISerializer"/>
	public interface ISerializationContext
	{
		/// <summary>
		/// Gets the connection for this serialization.
		/// </summary>
		IConnection Connection { get; }

		/// <summary>
		/// Gets the protocol for this serialization.
		/// </summary>
		/// <remarks>
		/// This is not necessarily the same <see cref="Protocol"/> as <see cref="Message.Protocol"/>, as
		/// this represents the actual version of the protocol negotiated to communicate with.
		/// </remarks>
		Protocol Protocol { get; }

		/// <summary>
		/// Gets the <see cref="Type"/>s and their IDs that have been added since <see cref="GetNewTypes"/> was last called.
		/// </summary>
		IEnumerable<KeyValuePair<Type, ushort>> GetNewTypes();

		/// <summary>
		/// Attempts to get the <paramref name="id"/> of the <paramref name="type"/>, or assigns a new one.
		/// </summary>
		/// <param name="type">The type to lookup the <paramref name="id"/> for.</param>
		/// <param name="id">The id of the <paramref name="type"/>.</param>
		/// <returns><c>true</c> if the type is new and needs to be transmitted, <c>false</c> otherwise.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
		bool GetTypeId (Type type, out ushort id);

		/// <summary>
		/// Attempts to get the <paramref name="type"/> for <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The id to search for.</param>
		/// <param name="type">The type, if found.</param>
		/// <returns><c>true</c> if the type was found</returns>
		bool TryGetType (ushort id, out Type type);
	}
}
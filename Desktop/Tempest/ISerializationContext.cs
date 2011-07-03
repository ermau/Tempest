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
		/// Gets the <see cref="ISerializationContext"/>'s <see cref="TypeMap"/>. Internal use.
		/// </summary>
		TypeMap TypeMap { get; }
	}
}
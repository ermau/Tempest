//
// ConnectionExtensions.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011-2012 Eric Maupin
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
	/// Holds extension methods for <see cref="IConnection"/>
	/// </summary>
	public static class ConnectionExtensions
	{
		/// <summary>
		/// Sends a message to all of the connections.
		/// </summary>
		/// <param name="connections">The connections to send to.</param>
		/// <param name="msg">The message to send.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="connections"/> is <c>null</c>.</para><para>--or--</para>
		/// <para><paramref name="msg"/> is <c>null</c>.</para>
		/// </exception>
		public static void Send (this IEnumerable<IConnection> connections, Message msg)
		{
			if (connections == null)
				throw new ArgumentNullException ("connections");
			if (msg == null)
				throw new ArgumentNullException ("msg");

			foreach (IConnection connection in connections)
				connection.SendAsync (msg);
		}
	}
}
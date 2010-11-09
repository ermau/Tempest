//
// Protocol.cs
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
using System.Collections.Generic;
using System.Linq;

namespace Tempest
{
	/// <summary>
	/// Identifies a messaging protocol.
	/// </summary>
	/// <remarks>
	/// Multiple Tempest-build libraries and applications can run on a single
	/// set of connection provider and connections. Protocols are used to
	/// identify the various sets of messages so that the correct handlers
	/// receive the correct messages.
	/// </remarks>
	public class Protocol
	{
		private Protocol (byte id)
		{
			Id = id;
		}

		/// <summary>
		/// Gets the unique byte identifier for the protocol.
		/// </summary>
		public byte Id
		{
			get;
			private set;
		}

		/// <summary>
		/// Registers a new protocol with <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The unique identifier of the protocol.</param>
		/// <returns>A newly registered protocol.</returns>
		public static Protocol Register (byte id)
		{
			var p = new Protocol (id);

			#if !NET_4
			lock (Protocols)
				Protocols.Add (id, p);
			#else
			if (!Protocols.TryAdd (id, p))
				throw new InvalidOperationException ("Protocol already registered.");
			#endif

			return p;
		}

		#if !NET_4
		private static readonly Dictionary<byte, Protocol> Protocols = new Dictionary<byte, Protocol>();

		public static Protocol Get (byte id)
		{
			Protocol p;
			lock (Protocols)
			{
				if (!Protocols.TryGetValue (id, out p))
					return null;
			}

			return p;
		}
		#else
		private static readonly ConcurrentDictionary<byte, Protocol> Protocols = new ConcurrentDictionary<byte, Protocol>();

		public static Protocol Get (byte id)
		{
			Protocol p;
			if (!Protocols.TryGetValue (id, out p))
				return null;

			return p;
		}
		#endif
	}
}
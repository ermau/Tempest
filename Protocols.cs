//
// Protocols.cs
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

#if NET_4
using System.Collections.Concurrent;
#endif

namespace Tempest
{
	public static class Protocols
	{
		/// <summary>
		/// Attempts to find protocol for <paramref name="buffer"/> and return the corresponding header.
		/// </summary>
		/// <param name="buffer">The message buffer to search.</param>
		/// <returns>The header if found, <c>null</c> otherwise.</returns>
		public static MessageHeader FindHeader (byte[] buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			return FindHeader (buffer, 0, buffer.Length);
		}

		/// <summary>
		/// Attempts to find protocol for <paramref name="buffer"/> and return the corresponding header.
		/// </summary>
		/// <param name="buffer">The message buffer to search.</param>
		/// <param name="offset">The offset to start searching at.</param>
		/// <param name="length">The maximum length to search.</param>
		/// <returns>The header if found, <c>null</c> otherwise.</returns>
		public static MessageHeader FindHeader (byte[] buffer, int offset, int length)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");
			if (offset + length > buffer.Length || offset < 0)
				throw new ArgumentOutOfRangeException ("offset", "Offset and length fall within the size of the buffer");

			Protocol p = Get (buffer[offset]);
			if (p != null)
			{
				MessageHeader header = p.GetHeader (buffer, offset, length);
				if (header != null)
					return header;
			}

			lock (CustomProtocols)
			{
				for (int i = 0; i < CustomProtocols.Count; ++i)
				{
					MessageHeader header = CustomProtocols[i].GetHeader (buffer, offset, length);
					if (header != null)
						return header;
				}
			}

			return null;
		}

		/// <summary>
		/// Registers <paramref name="protocol"/>.
		/// </summary>
		/// <param name="protocol">The protocol to register.</param>
		/// <returns>The registered protocol.</returns>
		public static Protocol Register (Protocol protocol)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");

			if (protocol.id != 0)
			{
				#if !NET_4
				lock (TempestProtocols)
					TempestProtocols.Add (protocol.id, protocol);
				#else
				if (!TempestProtocols.TryAdd (protocol.id, protocol))
					throw new InvalidOperationException ("Protocol already registered.");
				#endif
			}
			else
			{
				lock (CustomProtocols)
					CustomProtocols.Add (protocol);
			}

			return protocol;
		}

		static Protocols()
		{
			InternalProtocol.TempestMessage.RegisterProtocol();
		}

		private static readonly List<Protocol> CustomProtocols = new List<Protocol>();
		#if !NET_4
		private static readonly Dictionary<byte, Protocol> TempestProtocols = new Dictionary<byte, Protocol>();

		public static Protocol Get (byte id)
		{
			Protocol p;
			lock (TempestProtocols)
			{
				if (!TempestProtocols.TryGetValue (id, out p))
					return null;
			}

			return p;
		}
		#else
		private static readonly ConcurrentDictionary<byte, Protocol> TempestProtocols = new ConcurrentDictionary<byte, Protocol>();

		public static Protocol Get (byte id)
		{
			Protocol p;
			if (!TempestProtocols.TryGetValue (id, out p))
				return null;

			return p;
		}
		#endif
	}
}
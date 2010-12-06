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
using System;
using System.Collections.Generic;
using System.Linq;

#if NET_4
using System.Collections.Concurrent;
#endif

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
		private readonly byte id;

		public Protocol (byte id)
		{
			this.id = id;
		}

		// TODO: Write docs clarifying that Id is only needed for
		// Tempest-based protocols
		protected Protocol()
		{
		}

		public virtual byte[] GetBytes (Message message, out int length)
		{
			return GetBytes (message, out length, new byte[1024]);
		}

		public virtual byte[] GetBytes (Message message, out int length, byte[] buffer)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			var writer = new BufferValueWriter (buffer);
			writer.WriteByte (this.id);
			writer.WriteUInt16 (message.MessageType);
			writer.Length += sizeof (int); // length placeholder

			message.WritePayload (writer);

			Buffer.BlockCopy (BitConverter.GetBytes (writer.Length), 0, writer.Buffer, TempestHeaderLength - sizeof(int), sizeof(int));

			length = writer.Length;

			return writer.Buffer;
		}

		public virtual MessageHeader GetHeader (byte[] buffer, int offset, int length)
		{
			if (length < TempestHeaderLength)
				return null;
			if (buffer[offset] != id)
				return null;

			ushort type;
			int mlen;
			try
			{
				type = BitConverter.ToUInt16 (buffer, offset + 1);
				mlen = BitConverter.ToInt32 (buffer, offset + 1 + sizeof (ushort));
			}
			catch
			{
				return null;
			}

			Message msg = Message.Factory.Create (this, type);
			if (msg == null)
				return null;

			return new MessageHeader (this, msg, mlen);
		}

		public static MessageHeader FindHeader (byte[] buffer)
		{
			return FindHeader (buffer, 0, buffer.Length);
		}

		public static MessageHeader FindHeader (byte[] buffer, int offset, int length)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");
			if (offset + length > buffer.Length || offset < 0)
				throw new ArgumentOutOfRangeException ("Offset and length fall within the size of the buffer");

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
		public static void Register (Protocol protocol)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");

			if (protocol.id != 0)
			{
				#if !NET_4
				lock (Protocols)
					Protocols.Add (protocol.id, protocol);
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
		}
		
		private const int TempestHeaderLength = 7;

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
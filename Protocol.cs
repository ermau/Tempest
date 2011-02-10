//
// Protocol.cs
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
	/// Identifies a messaging protocol.
	/// </summary>
	/// <remarks>
	/// Multiple Tempest-build libraries and applications can run on a single
	/// set of connection provider and connections. Protocols are used to
	/// identify the various sets of messages so that the correct handlers
	/// receive the correct messages.
	/// </remarks>
	public sealed class Protocol
		: MessageFactory, IEquatable<Protocol>, ISerializable
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Protocol"/> class.
		/// </summary>
		/// <param name="id">The ID of the protocol.</param>
		/// <exception cref="ArgumentException"><paramref name="id"/> is 1.</exception>
		/// <remarks>
		/// Protocol ID 1 is reserved for internal Tempest use.
		/// </remarks>
		public Protocol (byte id)
		{
			if (id == 1)
				throw new ArgumentException ("ID 1 is reserved for Tempest use.", "id");

			this.id = id;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Protocol"/> class.
		/// </summary>
		/// <param name="id">The ID of the protocol.</param>
		/// <param name="version">The version of the protocol.</param>
		/// <exception cref="ArgumentException"><paramref name="id"/> is 1.</exception>
		/// <remarks>
		/// Protocol ID 1 is reserved for internal Tempest use.
		/// </remarks>
		public Protocol (byte id, int version)
			: this (id)
		{
			this.version = version;
		}

		internal Protocol (IValueReader reader)
		{
			Deserialize (reader);
		}

		/// <summary>
		/// Gets the version of this protocol.
		/// </summary>
		public int Version
		{
			get { return this.version; }
		}

		// TODO: Write docs clarifying that Id is only needed for
		// Tempest-based protocols
		protected Protocol()
		{
		}

		public /*virtual*/ byte[] GetBytes (Message message, out int length)
		{
			return GetBytes (message, out length, new byte[1024]);
		}

		public /*virtual*/ byte[] GetBytes (Message message, out int length, byte[] buffer)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			var writer = new BufferValueWriter (buffer);
			writer.WriteByte (this.id);
			writer.WriteUInt16 (message.MessageType);
			writer.Length += sizeof (int); // length placeholder

			message.WritePayload (writer);

			Buffer.BlockCopy (BitConverter.GetBytes (writer.Length), 0, writer.Buffer, TempestHeaderLength - sizeof(int), sizeof(int));

			length = writer.Length;

			return writer.Buffer;
		}

		public /*virtual*/ MessageHeader GetHeader (byte[] buffer, int offset, int length)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");
			if (offset < 0 || length + offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset and length must be within the size of the array");
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

			Message msg = Create (type);
			if (msg == null)
				return null;

			return new MessageHeader (this, msg, mlen);
		}

		public void Serialize (IValueWriter writer)
		{
			writer.WriteByte (this.id);
			writer.WriteInt32 (this.version);
		}

		public void Deserialize (IValueReader reader)
		{
			this.id = reader.ReadByte();
			this.version = reader.ReadInt32();
		}

		public override bool Equals (object obj)
		{
			return Equals (obj as Protocol);
		}

		public bool Equals (Protocol other)
		{
			if (ReferenceEquals (null, other))
				return false;
			if (ReferenceEquals (this, other))
				return true;

			return (GetType() == other.GetType() && other.id == id && version == other.version);
		}

		public override int GetHashCode()
		{
			return id.GetHashCode() ^ version.GetHashCode() ^ GetType().GetHashCode();
		}

		public static bool operator == (Protocol left, Protocol right)
		{
			return Equals (left, right);
		}

		public static bool operator != (Protocol left, Protocol right)
		{
			return !Equals (left, right);
		}

		private int version;
		internal byte id;
		
		private const int TempestHeaderLength = 7;
	}
}
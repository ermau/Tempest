//
// Target.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012 Eric Maupin
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

namespace Tempest
{
	public sealed class Target
		: IEquatable<Target>, ISerializable
	{
		public const string AnyIP = "0.0.0.0";
		public const string AnyIPv6 = "::";
		public const string LoopbackIP = "127.0.0.1";
		public const string LoopbackIPv6 = "[::1]";

		public Target (string hostname, int port)
		{
			if (hostname == null)
				throw new ArgumentNullException ("hostname");

			Hostname = hostname;
			Port = port;
		}

		public Target (ISerializationContext context, IValueReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");

			Deserialize (context, reader);
		}

		public string Hostname
		{
			get;
			private set;
		}

		public int Port
		{
			get;
			private set;
		}

		public override bool Equals (object obj)
		{
			if (ReferenceEquals (null, obj))
				return false;
			if (ReferenceEquals (this, obj))
				return true;

			return obj is Target && Equals ((Target)obj);
		}

		public bool Equals (Target other)
		{
			if (ReferenceEquals (null, other))
				return false;
			if (ReferenceEquals (this, other))
				return true;

			return (String.Equals (Hostname, other.Hostname) && Port == other.Port);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (Hostname.GetHashCode() * 397) ^ Port;
			}
		}

		public static bool operator == (Target left, Target right)
		{
			return Equals (left, right);
		}

		public static bool operator != (Target left, Target right)
		{
			return !Equals (left, right);
		}

		public void Serialize (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Hostname);
			writer.WriteInt32 (Port);
		}

		public void Deserialize (ISerializationContext context, IValueReader reader)
		{
			Hostname = reader.ReadString();
			Port = reader.ReadInt32();
		}
	}
}
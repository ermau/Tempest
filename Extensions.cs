//
// Extensions.cs
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

#if NET_4
using System.Collections.Concurrent;
using System.Text;

#else
using System.Collections.Generic;
#endif

namespace Tempest
{
	public static class Extensions
	{
		/// <summary>
		/// Writes a date value.
		/// </summary>
		public static void WriteDate (this IValueWriter writer, DateTime date)
		{
			if (writer == null)
				throw new ArgumentNullException ("writer");

			writer.WriteInt64 (date.ToBinary());
		}

		/// <summary>
		/// Reads a date value.
		/// </summary>
		public static DateTime ReadDate (this IValueReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");

			return DateTime.FromBinary (reader.ReadInt64());
		}

		public static void WriteString (this IValueWriter writer, string value)
		{
			if (value == null)
				throw new ArgumentNullException ("value");

			writer.WriteString (Encoding.UTF8, value);
		}

		public static string ReadString (this IValueReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");

			return reader.ReadString (Encoding.UTF8);
		}

		public static void Write (this IValueWriter writer, object value)
		{
			if (writer == null)
				throw new ArgumentNullException ("writer");

			if (value == null)
			{
				writer.WriteBool (false);
				return;
			}

			ObjectSerializer serializer = ObjectSerializer.GetSerializer (value.GetType());
			serializer.Serialize (writer, value);
		}

		public static T Read<T> (this IValueReader reader)
		{
			return (T)reader.Read (typeof (T));
		}

		public static object Read (this IValueReader reader, Type type)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");

			return ObjectSerializer.GetSerializer (type).Deserialize (reader);
		}
	}
}
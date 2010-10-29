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
using System.Linq;
using System.Reflection;
using System.Text;

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

		public static void Write (this IValueWriter writer, object value)
		{
			if (writer == null)
				throw new ArgumentNullException ("writer");

			writer.WriteBool (value == null);
			if (value == null)
				return;

			// TODO: Circular reference handling (hashset passing down the rabit hole?)
			// TODO: Reflection caching optimization
			Type t = value.GetType();

			if (t.IsPrimitive)
			{
				if (t == typeof (bool))
					writer.WriteBool ((bool)value);
				if (t == typeof (byte))
					writer.WriteByte ((byte)value);
				else if (t == typeof (sbyte))
					writer.WriteSByte ((sbyte)value);
				else if (t == typeof (short))
					writer.WriteInt16 ((short)value);
				else if (t == typeof (ushort))
					writer.WriteUInt16 ((ushort)value);
				else if (t == typeof (int))
					writer.WriteInt32 ((int)value);
				else if (t == typeof (uint))
					writer.WriteUInt32 ((uint)value);
				else if (t == typeof (long))
					writer.WriteInt64 ((long)value);
				else if (t == typeof (ulong))
					writer.WriteUInt64 ((ulong)value);

				else if (t == typeof (float))
					writer.WriteSingle ((float)value);
				else if (t == typeof (double))
					writer.WriteDouble ((double)value);
				else if (t == typeof (decimal))
					writer.WriteDecimal ((decimal)value);

				return;
			}
			else if (t == typeof (DateTime))
			{
				writer.WriteDate ((DateTime)(object)value);
				return;
			}
			else if (t == typeof (string))
			{
				writer.WriteString (Encoding.UTF8, (string)value);
				return;
			}
			else if (t.IsArray)
			{
				Array a = (Array)value;
				writer.WriteInt32 (a.Length);
				for (int i = 0; i < a.Length; ++i)
					writer.Write (a.GetValue (i));
			}
			else
			{
				MemberInfo[] props =
					t.GetMembers (BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.GetField)
						.OrderBy (mi => mi.Name)
						.ToArray();

				for (int i = 0; i < props.Length; ++i)
				{
					if (props[i].MemberType == MemberTypes.Field)
					{
						var f = (FieldInfo)props[i];
						if (f.IsInitOnly)
							continue;

						Write (writer, f.GetValue (value));
					}
					else if (props[i].MemberType == MemberTypes.Property)
					{
						var p = (PropertyInfo)props[i];
						if (!p.CanRead || !p.CanWrite)
							continue;

						Write (writer, p.GetValue (value, null));
					}
				}
			}
		}

		public static T Read<T> (this IValueReader reader)
			where T : new()
		{
			return (T)reader.Read (typeof (T));
		}

		private static object Read (this IValueReader reader, Type type)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");

			if (reader.ReadBool())
				return null;

			if (type.IsPrimitive)
			{
				if (type == typeof(bool))
					return reader.ReadBool();
				if (type == typeof(byte))
					return reader.ReadByte();
				if (type == typeof(sbyte))
					return reader.ReadSByte();
				if (type == typeof(short))
					return reader.ReadInt16();
				if (type == typeof(ushort))
					return reader.ReadUInt16();
				if (type == typeof(int))
					return reader.ReadInt32 ();
				if (type == typeof(uint))
					return reader.ReadUInt32();
				if (type == typeof(long))
					return reader.ReadInt64();
				if (type == typeof(ulong))
					return reader.ReadUInt64();
				if (type == typeof(short))
					return reader.ReadString (Encoding.UTF8);
				if (type == typeof(float))
					return reader.ReadSingle();
				if (type == typeof(double))
					return reader.ReadDouble();
				if (type == typeof(decimal))
					return reader.ReadDecimal ();

				throw new ArgumentOutOfRangeException ("type"); // Shouldn't happen.
			}
			else if (type == typeof(DateTime))
				return reader.ReadDate();
			else if (type == typeof(string))
				return reader.ReadString (Encoding.UTF8);
			else if (type.IsArray)
			{
				Type etype = type.GetElementType();
				Array a = Array.CreateInstance (etype, reader.ReadInt32());
				for (int i = 0; i < a.Length; ++i)
					a.SetValue (reader.Read (etype), i);

				return a;
			}
			else
			{
				ConstructorInfo c = type.GetConstructor (Type.EmptyTypes);
				if (c == null)
					throw new ArgumentException ("Type must have an empty constructor.", "type");

				object value = c.Invoke (null);

				MemberInfo[] props =
					type.GetMembers (BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.GetField)
						.OrderBy (mi => mi.Name)
						.ToArray();

				for (int i = 0; i < props.Length; ++i)
				{
					if (props[i].MemberType == MemberTypes.Field)
					{
						var f = (FieldInfo)props[i];
						if (f.IsInitOnly)
							continue;

						f.SetValue (value, reader.Read (f.FieldType));
					}
					else if (props[i].MemberType == MemberTypes.Property)
					{
						var p = (PropertyInfo)props[i];
						if (p.GetSetMethod() == null ||p.GetIndexParameters().Length != 0)
							continue;

						p.SetValue (value, reader.Read (p.PropertyType), null);
					}
				}

				return value;
			}
		}
	}
}
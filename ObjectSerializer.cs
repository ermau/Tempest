//
// ObjectSerializer.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

#if !SAFE
using System.Reflection.Emit;
#endif

namespace Tempest
{
	internal class ObjectSerializer
	{
		private readonly Type type;

		public ObjectSerializer (Type type)
		{
			if (type == null)
				throw new ArgumentNullException ("type");

			this.type = type;

			GenerateSerialization();
		}

		public void Serialize (IValueWriter writer, object obj)
		{
			if (writer == null)
				throw new ArgumentNullException ("writer");

			serializer (writer, obj);
		}

		public T Deserialize<T> (IValueReader reader)
		{
			if (typeof(T) != this.type)
				throw new ArgumentException ("Type does not match serializer type");

			return (T)Deserialize (reader);
		}

		public object Deserialize (IValueReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");

			return deserializer (reader);
		}

		private Func<IValueReader, object> deserializer;
		private Action<IValueWriter, object> serializer;

		private void GenerateSerialization()
		{
			//#if SAFE
			if (this.type.GetCustomAttributes (true).OfType<SerializableAttribute>().Any ())
			{
				deserializer = SerializableDeserializer;
				serializer = SerializableSerializer;
			}
			else
			{
				deserializer = SafeDeserialize;
				serializer = SafeSerializer;
			}
			//#endif
		}

		//		#if !SAFE
//		private Func<IValueReader, object> CreateUnsafeSerializer()
//		{
//			
//		}
//		#endif

		private ConstructorInfo ctor;
		private MemberInfo[] members;

		private object SafeDeserialize (IValueReader reader)
		{
			var t = this.type;

			if (t.IsClass)
			{
				if (!reader.ReadBool())
					return null;

				t = Type.GetType (reader.ReadString (Encoding.UTF8));
				if (!this.type.IsAssignableFrom (t))
					return null;
			}

			if (t.IsPrimitive)
			{
				if (t == typeof(bool))
					return reader.ReadBool();
				if (t == typeof(byte))
					return reader.ReadByte();
				if (t == typeof(sbyte))
					return reader.ReadSByte();
				if (t == typeof(short))
					return reader.ReadInt16();
				if (t == typeof(ushort))
					return reader.ReadUInt16();
				if (t == typeof(int))
					return reader.ReadInt32 ();
				if (t == typeof(uint))
					return reader.ReadUInt32();
				if (t == typeof(long))
					return reader.ReadInt64();
				if (t == typeof(ulong))
					return reader.ReadUInt64();
				if (t == typeof(float))
					return reader.ReadSingle();
				if (t == typeof(double))
					return reader.ReadDouble();
				if (t == typeof(decimal))
					return reader.ReadDecimal ();

				throw new ArgumentOutOfRangeException ("type"); // Shouldn't happen.
			}
			else if (t == typeof(DateTime))
				return reader.ReadDate();
			else if (t == typeof(string))
				return reader.ReadString (Encoding.UTF8);
			else if (t.IsArray || t == typeof(Array))
			{
				Type etype = t.GetElementType();
				Array a = Array.CreateInstance (etype, reader.ReadInt32());
				for (int i = 0; i < a.Length; ++i)
					a.SetValue (reader.Read (etype), i);

				return a;
			}
			else
			{
				LoadMembers (t);

				object value = this.ctor.Invoke (null);
				MemberInfo[] props = this.members;

				for (int i = 0; i < props.Length; ++i)
				{
					if (props[i].MemberType == MemberTypes.Field)
					{
						var f = (FieldInfo)props[i];
						if (f.IsInitOnly)
							continue;

						f.SetValue (value, GetSerializer (f.FieldType).Deserialize (reader));
					}
					else if (props[i].MemberType == MemberTypes.Property)
					{
						var p = (PropertyInfo)props[i];
						if (p.GetSetMethod() == null || p.GetIndexParameters().Length != 0)
							continue;

						p.SetValue (value, GetSerializer (p.PropertyType).Deserialize (reader), null);
					}
				}

				return value;
			}
		}

		private void LoadMembers (Type t)
		{
			if (this.members != null)
				return;

			this.ctor = t.GetConstructor (Type.EmptyTypes);
			if (this.ctor == null)
				throw new ArgumentException ("Type must have an empty constructor.", "type");

			this.members = t.GetMembers (BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.GetField)
							.OrderBy (mi => mi.Name)
							.ToArray();
		}

		private object SerializableDeserializer (IValueReader reader)
		{
			bool isNull = false;
			if (this.type.IsClass)
				isNull = reader.ReadBool();

			if (isNull)
				return null;

			byte[] data = reader.ReadBytes();
			using (MemoryStream stream = new MemoryStream (data))
				return new BinaryFormatter().Deserialize (stream, null);
		}
		
		private void SerializableSerializer (IValueWriter writer, object value)
		{
			if (this.type.IsClass)
				writer.WriteBool (value == null);

			using (MemoryStream stream = new MemoryStream())
			{
				new BinaryFormatter().Serialize (stream, value);
				writer.WriteBytes (stream.ToArray());
			}
		}

		private void SafeSerializer (IValueWriter writer, object value)
		{
			if (writer == null)
				throw new ArgumentNullException ("writer");

			if (value == null)
			{
				writer.WriteBool (false);
				return;
			}
			
			// TODO: Circular reference handling (hashset passing down the rabit hole?)
			// TODO: Reflection caching optimization
			Type t = this.type;

			if (t.IsClass)
			{
				writer.WriteBool (true);
				writer.WriteString (Encoding.UTF8, String.Format ("{0}, {1}", t.FullName, t.Assembly.GetName().Name));
			}

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
			else if (t.IsArray || t == typeof(Array))
			{
				Array a = (Array)value;
				writer.WriteInt32 (a.Length);
				for (int i = 0; i < a.Length; ++i)
					writer.Write (a.GetValue (i));
			}
			else
			{
				LoadMembers (t);
				MemberInfo[] props = this.members;

				for (int i = 0; i < props.Length; ++i)
				{
					var m = props[i];
					if (m.MemberType == MemberTypes.Field)
					{
						var f = (FieldInfo)m;
						if (f.IsInitOnly)
							continue;

						GetSerializer (f.FieldType).Serialize (writer, f.GetValue (value));
					}
					else if (m.MemberType == MemberTypes.Property)
					{
						var p = (PropertyInfo)m;
						var setter = p.GetSetMethod();
						if (setter == null || p.GetIndexParameters().Length != 0)
							continue;

						GetSerializer (setter.GetParameters()[0].ParameterType).Serialize (writer, p.GetValue (value, null));
					}
				}
			}
		}

		#if NET_4
		private static readonly ConcurrentDictionary<Type, ObjectSerializer> Serializers = new ConcurrentDictionary<Type, ObjectSerializer>();
		#else
		private static readonly Dictionary<Type, ObjectSerializer> Serializers = new Dictionary<Type, ObjectSerializer> ();
		#endif

		internal static ObjectSerializer GetSerializer (Type type)
		{
			ObjectSerializer serializer;
			#if NET_4
			serializer = Serializers.GetOrAdd (type, t => new ObjectSerializer (t));
			#else
			bool exists;
			lock (Serializers)
				exists = Serializers.TryGetValue (type, out serializer);

			if (!exists)
			{
				serializer = new ObjectSerializer (type);
				lock (Serializers)
				{
					if (!Serializers.ContainsKey (type))
						Serializers.Add (type, serializer);
				}
			}
			#endif

			return serializer;
		}
	}
}
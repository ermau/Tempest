//
// TypeMapTests.cs
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
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class TypeMapTests
	{
		[Test]
		public void TryGetTypeIdNull()
		{
			var map = new TypeMap();
			ushort id;
			Assert.Throws<ArgumentNullException> (() => map.GetTypeId (null, out id));
		}

		[Test]
		public void CtorNull()
		{
			Assert.Throws<ArgumentNullException> (() => new TypeMap ((IDictionary<Type, ushort>)null));
			Assert.Throws<ArgumentNullException> (() => new TypeMap ((Dictionary<Type, ushort>)null));
		}

		[Test]
		public void Ctor()
		{
			var dict = new Dictionary<Type, ushort> { { typeof (string), 0 }, { typeof (int), 1 } };
			TypeMap map = null;
			Assert.DoesNotThrow (() => map = new TypeMap (dict));

			Type type;
			Assert.IsTrue (map.TryGetType (0, out type));
			Assert.AreEqual (typeof (string), type);
			Assert.IsTrue (map.TryGetType (1, out type));
			Assert.AreEqual (typeof (int), type);

			ushort id;
			Assert.IsFalse (map.GetTypeId (typeof (string), out id));
			Assert.AreEqual (0, id);
			Assert.IsFalse (map.GetTypeId (typeof (int), out id));
			Assert.AreEqual (1, id);
		}

		[Test]
		public void First()
		{
			var map = new TypeMap();

			ushort id;
			Assert.IsTrue (map.GetTypeId (typeof (string), out id));
			Assert.AreEqual (0, id);
		}

		[Test]
		public void Repeated()
		{
			var map = new TypeMap();

			ushort id, id2;
			Assert.IsTrue (map.GetTypeId (typeof (string), out id));
			Assert.AreEqual (0, id);
			
			Assert.IsFalse (map.GetTypeId (typeof (string), out id2));
			Assert.AreEqual (id, id2);
		}

		[Test]
		public void Multiple()
		{
			var map = new TypeMap();

			ushort id, id2;
			Assert.IsTrue (map.GetTypeId (typeof (string), out id));
			Assert.AreEqual (0, id);

			Assert.IsFalse (map.GetTypeId (typeof (string), out id2));
			Assert.AreEqual (id, id2);

			Assert.IsTrue (map.GetTypeId (typeof (int), out id));
			Assert.AreNotEqual (id2, id);

			Assert.IsFalse (map.GetTypeId (typeof (int), out id2));
			Assert.AreEqual (id, id2);
		}

		[Test]
		public void TryGetNewTypes()
		{
			var map = new TypeMap();

			ushort id;
			map.GetTypeId (typeof (string), out id);

			var exp = new KeyValuePair<Type, int> (typeof (string), 0);

			IList<KeyValuePair<Type, ushort>> types;
			Assert.IsTrue (map.TryGetNewTypes (out types));
			
			Assert.AreEqual (exp.Key, types[0].Key);
			Assert.AreEqual (exp.Value, types[0].Value);
		}

		[Test]
		public void TryGetNewTypes_Multiple()
		{
			var map = new TypeMap();

			ushort id;
			map.GetTypeId (typeof (string), out id);
			map.GetTypeId (typeof (int), out id);

			IList<KeyValuePair<Type, ushort>> newItems;
			Assert.IsTrue (map.TryGetNewTypes (out newItems));

			var exp = new KeyValuePair<Type, int> (typeof (string), 0);
			Assert.AreEqual (exp.Key, newItems[0].Key);
			Assert.AreEqual (exp.Value, newItems[0].Value);

			exp = new KeyValuePair<Type, int> (typeof (int), 1);
			Assert.AreEqual (exp.Key, newItems[1].Key);
			Assert.AreEqual (exp.Value, newItems[1].Value);
		}

		[Test]
		public void TryGetNewTypes_Repeated()
		{
			var map = new TypeMap();

			ushort id;
			map.GetTypeId (typeof (string), out id);

			var exp = new KeyValuePair<Type, int> (typeof (string), 0);

			IList<KeyValuePair<Type, ushort>> types;
			Assert.IsTrue (map.TryGetNewTypes (out types));
			
			Assert.AreEqual (exp.Key, types[0].Key);
			Assert.AreEqual (exp.Value, types[0].Value);

			Assert.IsFalse (map.TryGetNewTypes (out types));
			Assert.IsNull (types);
		}

		[Test]
		public void Serialization()
		{
			var dict = new Dictionary<Type, ushort> { { typeof (string), 0 }, { typeof (int), 1 } };
			TypeMap map = new TypeMap (dict);

			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);
			map.Serialize (null, writer);
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			map = new TypeMap();
			map.Deserialize (null, reader);

			Type type;
			Assert.IsTrue (map.TryGetType (0, out type));
			Assert.AreEqual (typeof (string), type);
			Assert.IsTrue (map.TryGetType (1, out type));
			Assert.AreEqual (typeof (int), type);

			ushort id;
			Assert.IsFalse (map.GetTypeId (typeof (string), out id));
			Assert.AreEqual (0, id);
			Assert.IsFalse (map.GetTypeId (typeof (int), out id));
			Assert.AreEqual (1, id);
		}
	}
}
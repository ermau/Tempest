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
		public void GetNew()
		{
			var map = new TypeMap();

			ushort id;
			map.GetTypeId (typeof (string), out id);

			var exp = new KeyValuePair<Type, int> (typeof (string), 0);
			var kvp = map.GetNewTypes().ToList().Single();
			
			Assert.AreEqual (exp.Key, kvp.Key);
			Assert.AreEqual (exp.Value, kvp.Value);
		}

		[Test]
		public void GetNewMultiple()
		{
			var map = new TypeMap();

			ushort id;
			map.GetTypeId (typeof (string), out id);
			map.GetTypeId (typeof (int), out id);

			var newItems = map.GetNewTypes().ToList();

			var exp = new KeyValuePair<Type, int> (typeof (string), 0);
			Assert.AreEqual (exp.Key, newItems[0].Key);
			Assert.AreEqual (exp.Value, newItems[0].Value);

			exp = new KeyValuePair<Type, int> (typeof (int), 1);
			Assert.AreEqual (exp.Key, newItems[1].Key);
			Assert.AreEqual (exp.Value, newItems[1].Value);
		}

		[Test]
		public void GetNewRepeated()
		{
			var map = new TypeMap();

			ushort id;
			map.GetTypeId (typeof (string), out id);

			var exp = new KeyValuePair<Type, int> (typeof (string), 0);
			var kvp = map.GetNewTypes().ToList().Single();
			
			Assert.AreEqual (exp.Key, kvp.Key);
			Assert.AreEqual (exp.Value, kvp.Value);

			Assert.IsFalse (map.GetNewTypes().Any());
		}
	}
}
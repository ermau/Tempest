//
// BufferPoolTests.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2013 Eric Maupin
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
using System.Net.Sockets;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Tempest.Providers.Network;

namespace Tempest.Tests
{
	[TestFixture]
	public class BufferPoolTests
	{
		[Test]
		public void CtorInvalid()
		{
			Assert.That (() => new BufferPool (0), Throws.TypeOf<ArgumentOutOfRangeException>());
			Assert.That (() => new BufferPool (0, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
			Assert.That (() => new BufferPool (1, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Ctor()
		{
			var buffer = new BufferPool (128, 2);
			Assert.AreEqual (2, buffer.Limit);
		}

		[Test]
		public void AddConnection()
		{
			var buffer = new BufferPool (128);
			buffer.AutoSizeFactor = 2;
			buffer.AutoSizeLimit = true;

			buffer.AddConnection();
			Assert.AreEqual (2, buffer.Limit);
		}

		[Test]
		public void AddConnectionNoAutosizing()
		{
			var buffer = new BufferPool (128);
			buffer.AutoSizeFactor = 2;
			buffer.AutoSizeLimit = false;

			buffer.AddConnection();
			Assert.AreEqual (0, buffer.Limit);
		}

		[Test]
		public void PushNull()
		{
			var buffer = new BufferPool (128);
			Assert.That (() => buffer.PushBuffer (null), Throws.TypeOf<ArgumentNullException>());
		}

		[Test]
		public void TryGetBufferNew()
		{
			var buffer = new BufferPool (128, 2);

			SocketAsyncEventArgs e;
			Assert.IsFalse (buffer.TryGetBuffer (out e), "TryGetBuffer did not signal a newly created buffer");
			Assert.IsNotNull (e);
			Assert.AreEqual (e.Buffer.Length, 128, "Buffer length was not the default size");
		}

		[Test]
		public void TryGetBufferBlocksAndExisting()
		{
			var buffer = new BufferPool (128, 1);

			SocketAsyncEventArgs e;
			buffer.TryGetBuffer (out e);

			DateTime now = DateTime.Now;

			SocketAsyncEventArgs second = null;
			var test = new AsyncTest (args => {
				Assert.That (DateTime.Now - now, Is.GreaterThan (TimeSpan.FromSeconds (1)));
				Assert.That (second, Is.SameAs (e));
			});

			Task.Run (() => {
				Assert.IsTrue (buffer.TryGetBuffer (out second));
			}).ContinueWith (t => {
				test.PassHandler (null, EventArgs.Empty);
			});

			Task.Delay (1000).ContinueWith (t => {
				buffer.PushBuffer (e);
			});

			test.Assert (2000);
		}

		[Test]
		[Description ("If the buffer limit has dropped below the max, pushing buffers back should destroy them")]
		public void PushBufferDestroys()
		{
			var buffer = new BufferPool (128) {
				AutoSizeLimit = true,
				AutoSizeFactor = 1
			};

			buffer.AddConnection();

			SocketAsyncEventArgs e;
			buffer.TryGetBuffer (out e);

			buffer.RemoveConnection();

			buffer.PushBuffer (e);

			Assert.That (() => e.SetBuffer (0, 128), Throws.TypeOf<ObjectDisposedException>());
		}
	}
}

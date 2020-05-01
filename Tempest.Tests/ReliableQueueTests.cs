//
// ReliableQueueTests.cs
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

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tempest.Providers.Network;

namespace Tempest.Tests
{
	[TestFixture]
	public class ReliableQueueTests
	{
		private ReliableQueue queue;
		private IClientConnection connection;
		
		[SetUp]
		public void SetUp()
		{
			this.queue = new ReliableQueue();

			var provider = new MockConnectionProvider (MockProtocol.Instance);
			this.connection = provider.GetClientConnection();
		}

		[Test]
		public void EnqueueInOrder()
		{
			var first = GetTestMessageArgs (1);
			List<MessageEventArgs> msgs;
			Assert.IsTrue (this.queue.TryEnqueue (first, out msgs));
			Assert.IsNotNull (msgs);
			CollectionAssert.IsNotEmpty (msgs);
			CollectionAssert.Contains (msgs, first);

			var second = GetTestMessageArgs (2);
			Assert.IsTrue (this.queue.TryEnqueue (second, out msgs));
			Assert.IsNotNull (msgs);
			CollectionAssert.IsNotEmpty (msgs);
			CollectionAssert.Contains (msgs, second);
		}

		[TestCase (1, 2)]
		[TestCase (2, 1)]
		[TestCase (1, 2, 4, 3)]
		[TestCase (2, 5, 4, 3, 1)]
		[TestCase (2, 4, 1, 3)]
		[TestCase (2, 2, 1)]
		[TestCase (3, 2, 2, 1)]
		[TestCase (1, 1, 2)]
		[TestCase (1, 2, 4, 5, 3)]
		public void Enqueue (params int[] order)
		{
			List<MessageEventArgs> returnedOrder = new List<MessageEventArgs>();
			for (int i = 0; i < order.Length; i++)
			{
				var args = GetTestMessageArgs (order[i]);
				List<MessageEventArgs> results;
				this.queue.TryEnqueue (args, out results);
				if (results != null)
					returnedOrder.AddRange (results);
			}

			order = order.OrderBy (i => i).Distinct().ToArray();
			CollectionAssert.AreEqual (order, returnedOrder.Select (m => m.Message.Header.MessageId).ToArray());
		}

		[Test]
		public void Clear()
		{
			Enqueue (1, 2, 3);
			this.queue.Clear();
			Enqueue (1, 2, 3);
		}

		private MessageEventArgs GetTestMessageArgs (int mid)
		{
			var msg = new BlankMessage();
			msg.Header = new MessageHeader();
			msg.Header.MessageId = mid;

			return new MessageEventArgs (this.connection, msg);
		}
	}
}
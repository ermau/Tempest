//
// ReliableQueue.cs
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

namespace Tempest.Providers.Network
{
	internal class ReliableQueue
	{
		public void Clear()
		{
			lock (this.queue)
				this.queue.Clear();
		}

		public List<MessageEventArgs> Enqueue (MessageEventArgs messageArgs)
		{
			var msg = messageArgs.Message;
			int mid = msg.Header.MessageId;

			if (mid <= this.lastMessageInOrder)
				return null;

			List<MessageEventArgs> received = null;
			lock (this.queue)
			{
				int d = mid - this.lastMessageInOrder;
				if (d == 1)
				{
					received = new List<MessageEventArgs>();
					received.Add (messageArgs);
					
					int i = 1;
					for (; i < this.queue.Count; i++)
					{
						MessageEventArgs a = this.queue[i];
						if (a == null)
							break;

						received.Add (a);
					}

					if (this.queue.Count > 0)
						this.queue.RemoveRange (0, i);

					this.lastMessageInOrder = received[received.Count - 1].Message.Header.MessageId;
				}
				else
				{
					while (d-- > this.queue.Count)
						this.queue.Add (null);

					var args = this.queue[this.queue.Count - 1];
					if (args != null)
					{
						int largeId = args.Message.Header.MessageId;
						int pos = (this.queue.Count - 1) - (largeId - mid);
						this.queue[pos] = messageArgs;
					}
					else
						this.queue.Add (messageArgs);
				}
			}

			return received;
		}
		
		// BUG: doesn't handle MID rollover
		private int lastMessageInOrder;
		private readonly List<MessageEventArgs> queue = new List<MessageEventArgs>();
	}
}
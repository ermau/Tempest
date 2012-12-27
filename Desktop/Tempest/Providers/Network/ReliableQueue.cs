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
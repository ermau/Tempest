//
// MessageResponseManagerTests.cs
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class MessageResponseManagerTests
	{
		[Test]
		public void SendFor()
		{
			var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult (true);

			var mrm = new MessageResponseManager();

			var msg = new BlankMessage { Header = new MessageHeader { MessageId = 1 } };

			Task<Message> response = mrm.SendFor (msg, tcs.Task);

			var responseMsg = new BlankMessage {
				Header = new MessageHeader {
					MessageId = 2,
					ResponseMessageId = 1,
					IsResponse = true
				}
			};

			mrm.Receive (responseMsg);

			if (!response.Wait (10000))
				Assert.Fail ("Task never completed");

			Assert.AreSame (responseMsg, response.Result);
		}

		[Test]
		public void SendForSendFailed()
		{
			var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult (false);

			var mrm = new MessageResponseManager();

			var msg = new BlankMessage { Header = new MessageHeader { MessageId = 1 } };

			Task<Message> response = mrm.SendFor (msg, tcs.Task);

			try {
				if (!response.Wait (10000))
					Assert.Fail ("Task never completed");

				Assert.Fail ("Did not throw cancel exception");
			} catch (AggregateException aex) {
				Assert.IsTrue (response.IsCanceled);
				Assert.That (aex.Flatten().InnerException, Is.InstanceOf<OperationCanceledException>());
			}
		}

		[Test]
		public void SendForTimeout()
		{
			var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult (true);

			var mrm = new MessageResponseManager();

			var msg = new BlankMessage { Header = new MessageHeader { MessageId = 1 } };

			Task<Message> response = mrm.SendFor (msg, tcs.Task, 1000);

			DateTime start = DateTime.Now;
			while ((DateTime.Now - start) < TimeSpan.FromSeconds (2)) {
				mrm.CheckTimeouts();
				Thread.Sleep (1);
			}

			try {
				if (!response.Wait (10000))
					Assert.Fail ("Task never completed");

				Assert.Fail ("Did not throw cancel exception");
			} catch (AggregateException aex) {
				Assert.IsTrue (response.IsCanceled);
				Assert.That (aex.Flatten().InnerException, Is.InstanceOf<OperationCanceledException>());
			}
		}

		[Test]
		public void SendForTimeoutZero()
		{
			var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult (true);

			var mrm = new MessageResponseManager();

			var msg = new BlankMessage { Header = new MessageHeader { MessageId = 1 } };

			Task<Message> response = mrm.SendFor (msg, tcs.Task, 0);

			mrm.CheckTimeouts();

			var responseMsg = new BlankMessage {
				Header = new MessageHeader {
					MessageId = 2,
					ResponseMessageId = 1,
					IsResponse = true
				}
			};

			mrm.Receive (responseMsg);

			Thread.Sleep (1000);

			if (!response.Wait (10000))
				Assert.Fail ("Task never completed");
		}

		[Test]
		public void SendForTimeoutSuccess()
		{
			var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult (true);

			var mrm = new MessageResponseManager();

			var msg = new BlankMessage { Header = new MessageHeader { MessageId = 1 } };

			Task<Message> response = mrm.SendFor (msg, tcs.Task, 1000);

			var responseMsg = new BlankMessage {
				Header = new MessageHeader {
					MessageId = 2,
					ResponseMessageId = 1,
					IsResponse = true
				}
			};

			mrm.Receive (responseMsg);

			Thread.Sleep (2000);

			mrm.CheckTimeouts();

			if (!response.Wait (10000))
				Assert.Fail ("Task never completed");
		}

		[Test]
		public void SendForCancel()
		{
			var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult (true);

			var mrm = new MessageResponseManager();

			var msg = new BlankMessage { Header = new MessageHeader { MessageId = 1 } };

			var source = new CancellationTokenSource();

			Task<Message> response = mrm.SendFor (msg, tcs.Task, source.Token);

			source.Cancel();

			try {
				if (!response.Wait (10000))
					Assert.Fail ("Task never completed");

				Assert.Fail ("Did not throw cancel exception");
			} catch (AggregateException aex) {
				Assert.IsTrue (response.IsCanceled);
				Assert.That (aex.Flatten().InnerException, Is.InstanceOf<OperationCanceledException>());
			}
		}

		[Test]
		public void SendForCancelSuccess()
		{
			var tcs = new TaskCompletionSource<bool>();
			tcs.SetResult (true);

			var mrm = new MessageResponseManager();

			var msg = new BlankMessage { Header = new MessageHeader { MessageId = 1 } };

			var source = new CancellationTokenSource();

			Task<Message> response = mrm.SendFor (msg, tcs.Task, source.Token);

			var responseMsg = new BlankMessage {
				Header = new MessageHeader {
					MessageId = 2,
					ResponseMessageId = 1,
					IsResponse = true
				}
			};

			mrm.Receive (responseMsg);

			source.Cancel();

			if (!response.Wait (10000))
				Assert.Fail ("Task never completed");
		}
	}
}
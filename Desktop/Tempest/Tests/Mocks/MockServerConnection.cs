//
// MockServerConnection.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012-2013 Eric Maupin
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
using System.Threading;
using System.Threading.Tasks;

namespace Tempest.Tests
{
	public class MockServerConnection
		: MockConnection, IServerConnection
	{
		private MockClientConnection connection;

		internal MockServerConnection (MockClientConnection connection)
		{
			this.connected = true;
			this.connection = connection;
		}

		public override Task<bool> SendAsync (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (!IsConnected)
			{
				TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
				tcs.SetResult (false);
				return tcs.Task;
			}

			Task<bool> task = base.SendAsync (message);
			this.connection.Receive (new MessageEventArgs (this.connection, message));
			return task;
		}

		public override Task<bool> SendResponseAsync (Message originalMessage, Message response)
		{
			var tcs = new TaskCompletionSource<bool>();

			// Sometimes we manually construct messages to test handlers, we'll go ahead and build a header
			// for those automatically to save ourselves time.
			PrepareMessage (originalMessage);

			PrepareMessage (response);
			response.Header.ResponseMessageId = originalMessage.Header.MessageId;
			response.Header.IsResponse = true;
			
			this.connection.ReceiveResponse (new MessageEventArgs (this.connection, response));

			tcs.SetResult (true);
			return tcs.Task;
		}

		protected internal override Task Disconnect (ConnectionResult reason = ConnectionResult.FailedUnknown, string customReason = null)
		{
			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
			if (!this.connected) {
				tcs.SetResult (true);
			} else {
				base.Disconnect (reason, customReason).ContinueWith (t => {
					var c = Interlocked.Exchange (ref this.connection, null);
					if (c != null)
						c.Disconnect (reason, customReason).ContinueWith (t2 => tcs.SetResult (true));
					else
						tcs.SetResult (false);
				});
			}

			return tcs.Task;
		}
	}
}
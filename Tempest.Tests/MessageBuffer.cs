//
// MessageBuffer.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011-2013 Eric Maupin
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
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tempest.Tests
{
	public class ConnectionBuffer
		: IConnection
	{
		private readonly IConnection connection;

		public ConnectionBuffer (IConnection connection)
		{
			this.connection = connection;
			this.connection.MessageReceived += OnMessageReceived;
		}

		public void Dispose ()
		{
			this.connection.Dispose();
		}

		public bool IsConnected
		{
			get { return this.connection.IsConnected; }
		}

		public int ConnectionId
		{
			get { return this.connection.ConnectionId; }
		}

		public IEnumerable<Protocol> Protocols
		{
			get { return this.connection.Protocols; }
		}

		public Target RemoteTarget
		{
			get { return this.connection.RemoteTarget; }
		}

		public IAsymmetricKey RemoteKey
		{
			get { return this.connection.RemoteKey; }
		}

		public IAsymmetricKey LocalKey
		{
			get { return this.connection.LocalKey; }
		}

		public int ResponseTime
		{
			get { return this.connection.ResponseTime; }
		}

		public event EventHandler<MessageEventArgs> MessageReceived;
		public event EventHandler<DisconnectedEventArgs> Disconnected;

		public Task<bool> SendAsync (Message message)
		{
			return this.connection.SendAsync (message);
		}

		public Task<Message> SendFor (Message message, int timeout = 0)
		{
			return this.connection.SendFor (message, timeout);
		}

		public Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0) where TResponse : Message
		{
			return this.connection.SendFor<TResponse> (message, timeout);
		}

		public Task<bool> SendResponseAsync (Message originalMessage, Message response)
		{
			return this.connection.SendResponseAsync (originalMessage, response);
		}

		public Task DisconnectAsync ()
		{
			return this.connection.DisconnectAsync();
		}

		public Task DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			return this.connection.DisconnectAsync (reason, customReason);
		}

		public T DequeueAndAssertMessage<T>()
			where T : Message
		{
			Message msg = DequeueMessage();

			if (!(msg is T))
				Assert.Fail ("Message was " + msg.GetType().Name + ", not expected " + typeof (T).Name);

			return (T)msg;
		}

		public Message DequeueMessage (bool wait = true)
		{
			ushort tick = 0;

			Message msg;
			while (!this.messages.TryDequeue (out msg) && tick++ < (UInt16.MaxValue - 1))
				Thread.Sleep (1);

			if (tick == UInt16.MaxValue)
				Assert.Fail ("Message never arrived");

			return msg;
		}

		public void AssertNoMessage()
		{
			Assert.IsTrue (this.messages.Count == 0);
		}

		private readonly ConcurrentQueue<Message> messages = new ConcurrentQueue<Message>();
		private void OnMessageReceived (object sender, MessageEventArgs e)
		{
			this.messages.Enqueue (e.Message);
		}
	}
}
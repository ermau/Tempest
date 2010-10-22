//
// Client.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Cadenza.Collections;

namespace Tempest
{
	public abstract class Client
		: MessageHandling
	{
		protected Client (IClientConnection connection, bool poll)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");

			this.connection = connection;
			this.connection.Connected += OnConnectionConnected;
			this.connection.Disconnected += OnConnectionDisconnected;
			this.connection.ConnectionFailed += OnConnectionConnectionFailed;

			if (this.connection.Modes.HasFlag (MessagingModes.Inline) && (Environment.ProcessorCount == 1 || !this.connection.Modes.HasFlag (MessagingModes.Async)))
				this.mode = MessagingModes.Inline;
			else
			{
				this.mqueue = new Queue<MessageReceivedEventArgs>();
				this.mwait = new AutoResetEvent (false);
				this.connection.MessageReceived += ConnectionOnMessageReceived;
				this.mode = MessagingModes.Async;
			}

			this.polling = poll;
		}

		public event EventHandler Connected;
		public event EventHandler ConnectionFailed;
		public event EventHandler Disconnected;

		/// <summary>
		/// Gets whether the client is currently connected or not.
		/// </summary>
		public virtual bool IsConnected
		{
			get { return this.connection.IsConnected; }
		}

		public abstract void Connect (EndPoint endPoint);

		/// <summary>
		/// Disconnects from the server.
		/// </summary>
		/// <param name="now">Whether or not to disconnect immediately or wait for pending messages.</param>
		public virtual void Disconnect (bool now)
		{
			this.connection.Disconnect (now);

			if (!now)
			{
				this.disconnecting = true;
				return;
			}

			this.running = false;

			if (this.mode == MessagingModes.Async)
			{
				this.mwait.Set();
					
				lock (this.mqueue)
					this.mqueue.Clear();
			}

			if (this.messageRunner != null)
			{
				this.messageRunner.Join();
				this.messageRunner = null;
			}
		}

		public void Poll()
		{
			if (!IsConnected)
				return;

			List<MessageReceivedEventArgs> messages;
			if (this.mode == MessagingModes.Async)
			{
				messages = new List<MessageReceivedEventArgs> (mqueue.Count);

				lock (this.mqueue)
				{
					while (this.mqueue.Count > 0)
						messages.Add (this.mqueue.Dequeue());
				}
			}
			else
				messages = this.connection.Tick().ToList();

			for (int i = 0; i < messages.Count; ++i)
			{
				var m = messages[i];
				var mhandlers = GetHandlers (m.Message.MessageType);
				if (mhandlers == null)
					continue;

				for (int n = 0; n < mhandlers.Count; ++n)
					mhandlers[n] (m);
			}

			if (this.disconnecting)
				Disconnect (true);
		}

		protected bool disconnecting;
		protected readonly IClientConnection connection;
		private readonly MessagingModes mode;
		private readonly bool polling;

		private readonly Queue<MessageReceivedEventArgs> mqueue;
		private readonly AutoResetEvent mwait;
		private Thread messageRunner;
		protected volatile bool running;	

		protected void Connect (EndPoint endPoint, MessageTypes types)
		{
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");

			this.connection.Connect (endPoint, types);
		}

		private void ConnectionOnMessageReceived (object sender, MessageReceivedEventArgs e)
		{
			lock (this.mqueue)
				this.mqueue.Enqueue (e);

			this.mwait.Set();
		}

		private void InlineMessageRunner()
		{
			SpinWait wait = new SpinWait();

			while (this.running)
			{
				IEnumerable<MessageReceivedEventArgs> messages = this.connection.Tick();
				while (this.running && messages.Any())
				{
					wait.Reset();

					foreach (MessageReceivedEventArgs e in messages)
					{
						if (!this.running)
							break;

						var mhandlers = GetHandlers (e.Message.MessageType);
						if (mhandlers == null)
							continue;

						for (int i = 0; i < mhandlers.Count; ++i)
							mhandlers[i] (e);
					}
				}

				if (this.disconnecting)
				{
					ThreadPool.QueueUserWorkItem (now => Disconnect ((bool)now), true);
					return;
				}

				wait.SpinOnce();
			}
		}

		private void AsyncMessageRunner ()
		{
			Queue<MessageReceivedEventArgs> q = this.mqueue;

			while (this.running)
			{
				while (q.Count > 0)
				{
					if (!this.running)
						break;

					MessageReceivedEventArgs e;
					lock (q)
					{
						if (q.Count == 0)
							continue;

						e = q.Dequeue();
					}

					var mhandlers = GetHandlers (e.Message.MessageType);
					if (mhandlers == null)
						continue;

					for (int i = 0; i < mhandlers.Count; ++i)
						mhandlers[i] (e);
				}

				if (this.disconnecting)
				{
					ThreadPool.QueueUserWorkItem (now => Disconnect ((bool)now), true);
					return;
				}

				if (q.Count == 0)
					this.mwait.WaitOne();
			}
		}

		private void OnConnectionConnectionFailed (object sender, ClientConnectionEventArgs e)
		{
			OnConnectionFailed (EventArgs.Empty);
		}

		private void OnConnectionDisconnected (object sender, ConnectionEventArgs e)
		{
			OnDisconnected (EventArgs.Empty);
		}

		private void OnConnectionConnected (object sender, ClientConnectionEventArgs e)
		{
			this.running = true;

			if (!this.polling)
			{
				this.messageRunner = (this.mode == MessagingModes.Inline) ? new Thread (InlineMessageRunner) : new Thread (AsyncMessageRunner);
				this.messageRunner.Name = "Client Message Runner";
				this.messageRunner.IsBackground = true;
				this.messageRunner.Start();
			}

			OnConnected (EventArgs.Empty);
		}

		protected virtual void OnConnected (EventArgs e)
		{
			EventHandler handler = Connected;
			if (handler != null)
				handler (this, e);
		}

		protected virtual void OnConnectionFailed (EventArgs e)
		{
			EventHandler handler = ConnectionFailed;
			if (handler != null)
				handler (this, e);
		}

		protected virtual void OnDisconnected (EventArgs e)
		{
			EventHandler handler = Disconnected;
			if (handler != null)
				handler (this, e);
		}
	}
}
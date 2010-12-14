//
// Server.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Tempest
{
	public enum ExecutionMode
	{
		/// <summary>
		/// Executes all message handlers independently, but in order per connection.
		/// </summary>
		ConnectionOrder,

		/// <summary>
		/// Executes all message handlers in order on a single thread.
		/// </summary>
		GlobalOrder
	}

	public abstract class Server
		: MessageHandling
	{
		protected Server (MessageTypes messageTypes)
		{
			this.messageTypes = messageTypes;
		}

		protected Server (IConnectionProvider provider, MessageTypes messageTypes)
			: this (messageTypes)
		{
			AddConnectionProvider (provider);
		}

		/// <summary>
		/// Adds and starts the connection <paramref name="provider"/>.
		/// </summary>
		/// <param name="provider">The connection provider to add.</param>
		/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
		public void AddConnectionProvider (IConnectionProvider provider, ExecutionMode mode = ExecutionMode.ConnectionOrder)
		{
			if (provider == null)
				throw new ArgumentNullException ("provider");

			lock (this.providers)
				this.providers.Add (provider, mode);

			switch (mode)
			{
				case ExecutionMode.ConnectionOrder:
					provider.ConnectionMade += OnConnectionMade;
					break;

				case ExecutionMode.GlobalOrder:
					provider.ConnectionMade += OnConnectionMadeGlobal;

					lock (this.providers)
					{
						if (!this.running)
						{
							this.running = true;
							(this.messageRunnerThread = new Thread (MessageRunner) { IsBackground = true }).Start();
						}
					}

					break;
			}
			
			provider.Start (this.messageTypes);
		}

		/// <summary>
		/// Stops and removes the connection <paramref name="provider"/>.
		/// </summary>
		/// <param name="provider">The connection provider to remove.</param>
		/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
		public void RemoveConnectionProvider (IConnectionProvider provider)
		{
			if (provider == null)
				throw new ArgumentNullException ("provider");

			bool others = false;
			ExecutionMode mode;
			lock (this.providers)
			{
				if (!this.providers.TryGetValue (provider, out mode))
					return;

				this.providers.Remove (provider);

				if (mode == ExecutionMode.GlobalOrder)
					others = this.providers.Any (kvp => kvp.Value == ExecutionMode.GlobalOrder);
			}

			provider.Stop();

			if (mode == ExecutionMode.ConnectionOrder)
				provider.ConnectionMade -= OnConnectionMade;
			else
			{
				provider.ConnectionMade -= OnConnectionMadeGlobalEvent;
				
				if (!others)
				{
					this.running = false;
					this.wait.Set();
					if (this.messageRunnerThread != null)
						this.messageRunnerThread.Join();
				}
			}
		}

		protected volatile bool running = false;
		protected readonly Dictionary<IConnection, ExecutionMode> connections = new Dictionary<IConnection, ExecutionMode>();
		private readonly Dictionary<IConnectionProvider, ExecutionMode> providers = new Dictionary<IConnectionProvider, ExecutionMode>();
		private readonly MessageTypes messageTypes;

		protected virtual void OnConnectionMade (object sender, ConnectionMadeEventArgs e)
		{
			if (e.Rejected)
				return;

			lock (this.connections)
				this.connections.Add (e.Connection, ExecutionMode.ConnectionOrder);
			
			e.Connection.MessageReceived += OnConnectionMessageReceived;
			e.Connection.Disconnected += OnConnectionDisconnected;
		}

		protected virtual void OnConnectionMadeGlobal (object sender, ConnectionMadeEventArgs e)
		{
			if (e.Rejected)
				return;

			lock (this.connections)
				this.connections.Add (e.Connection, ExecutionMode.GlobalOrder);
			
			e.Connection.MessageReceived += OnGlobalMessageReceived;
			e.Connection.Disconnected += OnConnectionDisconnected;
		}

		protected virtual void OnConnectionDisconnected (object sender, ConnectionEventArgs e)
		{
			ExecutionMode mode;
			lock (this.connections)
			{
				if (!this.connections.TryGetValue (e.Connection, out mode))
					return;

				this.connections.Remove (e.Connection);
			}

			e.Connection.MessageReceived -= OnConnectionMessageReceived;
			e.Connection.Disconnected -= OnConnectionDisconnected;
		}

		private void OnConnectionMadeGlobalEvent (object sender, ConnectionMadeEventArgs e)
		{
			#if !NET_4
			lock (this.mqueue)
			#endif
			this.mqueue.Enqueue (e);

			this.wait.Set();
		}

		private void OnGlobalMessageReceived (object sender, MessageEventArgs e)
		{
			#if !NET_4
			lock (this.mqueue)
			#endif
			this.mqueue.Enqueue (e);

			this.wait.Set();
		}

		private Thread messageRunnerThread;
		private readonly AutoResetEvent wait = new AutoResetEvent (false);
		#if NET_4
		private readonly ConcurrentQueue<EventArgs>  mqueue = new ConcurrentQueue<EventArgs>();
		private void HandleInlineEvent (EventArgs e)
		{
			var margs = (e as MessageEventArgs);
			if (margs != null)
				OnConnectionMessageReceived (this, margs);
			else
			{
				var cmargs = (e as ConnectionMadeEventArgs);
				if (cmargs != null)
					OnConnectionMadeGlobal (this, cmargs);
				else
				{
					var cdargs = (e as ConnectionEventArgs);
					if (cdargs != null)
						OnConnectionDisconnected (this, cdargs);
				}
			}
		}
		
		private void MessageRunner()
		{
			while (this.running)
			{
				this.wait.WaitOne();

				EventArgs e;
				while (this.mqueue.TryDequeue (out e))
					HandleInlineEvent (e);
			}
		}
		#else
		private readonly Queue<EventArgs> mqueue = new Queue<EventArgs>();
		private void MessageRunner()
		{
			while (this.running)
			{
				this.wait.WaitOne();

				while (this.mqueue.Count > 0)
				{
					EventArgs e = null;
					lock (this.mqueue)
					{
						if (this.mqueue.Count != 0)
							e = this.mqueue.Dequeue();
					}

					HandleInlineEvent (e);
				}
			}
		}
		#endif

		protected virtual void OnConnectionMessageReceived (object sender, MessageEventArgs e)
		{
			lock (this.connections)
			{
				if (!this.connections.ContainsKey (e.Connection))
					return;
			}

			var mhandlers = GetHandlers (e.Message.MessageType);
			if (mhandlers == null)
				return;

			for (int i = 0; i < mhandlers.Count; ++i)
				mhandlers[i] (e);
		}
	}
}

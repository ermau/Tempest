//
// Server.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010-2012 Eric Maupin
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
using System.Threading;
using Tempest.InternalProtocol;
using System.Collections.Concurrent;

namespace Tempest
{
	/// <summary>
	/// Options for determining how messages are executed.
	/// </summary>
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

	/// <summary>
	/// Tempest servers.
	/// </summary>
	public class Server
		: MessageHandler, IServerContext
	{
		public Server (MessageTypes messageTypes)
		{
			this.messageTypes = messageTypes;
		}

		public Server (IConnectionProvider provider, MessageTypes messageTypes)
			: this (messageTypes)
		{
			AddConnectionProvider (provider);
		}

		public event EventHandler<ConnectionMadeEventArgs> ConnectionMade;

		public bool IsRunning
		{
			get { return this.running; }
		}

		/// <summary>
		/// Adds and starts the connection <paramref name="provider"/>.
		/// </summary>
		/// <param name="provider">The connection provider to add.</param>
		/// <param name="mode">The <see cref="ExecutionMode"/> for <paramref name="provider"/>.</param>
		/// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
		public void AddConnectionProvider (IConnectionProvider provider, ExecutionMode mode = ExecutionMode.ConnectionOrder)
		{
			if (provider == null)
				throw new ArgumentNullException ("provider");

			lock (this.providers)
				this.providers.Add (provider, mode);

			IConnectionlessMessenger connectionless = provider as IConnectionlessMessenger;

			switch (mode)
			{
				case ExecutionMode.ConnectionOrder:
					provider.ConnectionMade += OnConnectionMade;

					if (connectionless != null)
						connectionless.ConnectionlessMessageReceived += OnConnectionlessMessageReceived;

					break;

				case ExecutionMode.GlobalOrder:
					provider.ConnectionMade += OnConnectionMadeGlobal;

					if (connectionless != null)
						connectionless.ConnectionlessMessageReceived += OnConnectionlessMessageReceivedGlobal;

					break;
			}

			if (this.running)
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

			ExecutionMode mode;
			lock (this.providers)
			{
				if (!this.providers.TryGetValue (provider, out mode))
					return;

				this.providers.Remove (provider);
			}

			if (mode == ExecutionMode.ConnectionOrder)
				provider.ConnectionMade -= OnConnectionMade;
			else
				provider.ConnectionMade -= OnConnectionMadeGlobalEvent;
		}

		/// <summary>
		/// Starts the server and all connection providers.
		/// </summary>
		public virtual void Start()
		{
			if (this.running)
				return;

			this.running = true;

			lock (this.providers)
			{
				foreach (var kvp in this.providers)
				{
					if (kvp.Value == ExecutionMode.GlobalOrder && this.messageRunnerThread == null)
						(this.messageRunnerThread = new Thread (MessageRunner) { IsBackground = true }).Start();

					kvp.Key.Start (this.messageTypes);
				}
			}
		}

		/// <summary>
		/// Stops the server and all connection providers.
		/// </summary>
		public virtual void Stop()
		{
			this.running = false;

			lock (this.providers)
			{
				foreach (var kvp in this.providers)
				{
					kvp.Key.Stop();

					if (kvp.Value == ExecutionMode.GlobalOrder && this.messageRunnerThread != null)
					{
						this.wait.Set();
						this.messageRunnerThread.Join();
						this.messageRunnerThread = null;
					}
				}
			}

			lock (this.connections)
				this.connections.Clear();
		}

		public void DisconnectWithReason (IConnection connection, string reason)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");
			if (reason == null)
				throw new ArgumentNullException ("reason");

			connection.Send (new DisconnectMessage { Reason = ConnectionResult.Custom, CustomReason = reason });
			connection.DisconnectAsync (ConnectionResult.Custom, reason);
		}

		private volatile bool running = false;
		private readonly Dictionary<IConnection, ExecutionMode> connections = new Dictionary<IConnection, ExecutionMode>();
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

			var cmade = ConnectionMade;
			if (cmade != null)
				cmade (this, e);
		}

		protected virtual void OnConnectionMadeGlobal (object sender, ConnectionMadeEventArgs e)
		{
			if (e.Rejected)
				return;

			lock (this.connections)
				this.connections.Add (e.Connection, ExecutionMode.GlobalOrder);
			
			e.Connection.MessageReceived += OnGlobalMessageReceived;
			e.Connection.Disconnected += OnConnectionDisconnectedGlobal;

			var cmade = ConnectionMade;
			if (cmade != null)
				cmade (this, e);
		}

		protected virtual void OnConnectionDisconnectedGlobal (object sender, DisconnectedEventArgs e)
		{
			lock (this.connections)
			{
				if (!this.connections.Remove (e.Connection))
					return;
			}

			this.mqueue.Enqueue (e);

			e.Connection.MessageReceived -= OnConnectionMessageReceived;
			e.Connection.Disconnected -= OnConnectionDisconnectedGlobal;
		}

		protected virtual void OnConnectionDisconnected (object sender, DisconnectedEventArgs e)
		{
			lock (this.connections)
			{
				if (!this.connections.Remove (e.Connection))
					return;
			}

			e.Connection.MessageReceived -= OnConnectionMessageReceived;
			e.Connection.Disconnected -= OnConnectionDisconnected;
		}

		protected virtual void OnConnectionlessMessageReceived (object sender, ConnectionlessMessageEventArgs e)
		{
			var mhandlers = GetConnectionlessHandlers (e.Message);
			if (mhandlers == null)
				return;

			for (int i = 0; i < mhandlers.Count; ++i)
				mhandlers[i] (e);
		}
		
		private void OnConnectionlessMessageReceivedGlobal (object sender, ConnectionlessMessageEventArgs e)
		{
			this.mqueue.Enqueue (e);
			this.wait.Set();
		}

		private void OnConnectionMadeGlobalEvent (object sender, ConnectionMadeEventArgs e)
		{
			this.mqueue.Enqueue (e);
			this.wait.Set();
		}

		private void OnGlobalMessageReceived (object sender, MessageEventArgs e)
		{
			this.mqueue.Enqueue (e);
			this.wait.Set();
		}

		private Thread messageRunnerThread;
		private readonly AutoResetEvent wait = new AutoResetEvent (false);

		private void HandleInlineEvent (EventArgs e)
		{
			var margs = (e as MessageEventArgs);
			if (margs != null)
				OnConnectionMessageReceived (this, margs);
			else
			{
				var clmargs = (e as ConnectionlessMessageEventArgs);
				if (clmargs != null)
					OnConnectionlessMessageReceived (this, clmargs);
				else
				{
					var cmargs = (e as ConnectionMadeEventArgs);
					if (cmargs != null)
						OnConnectionMadeGlobal (this, cmargs);
					else
					{
						var cdargs = (e as DisconnectedEventArgs);
						if (cdargs != null)
							OnConnectionDisconnected (this, cdargs);
					}
				}
			}
		}
		
		private readonly ConcurrentQueue<EventArgs>  mqueue = new ConcurrentQueue<EventArgs>();
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

		protected virtual void OnConnectionMessageReceived (object sender, MessageEventArgs e)
		{
			lock (this.connections)
			{
				if (!this.connections.ContainsKey (e.Connection))
					return;
			}

			var mhandlers = GetHandlers (e.Message);
			if (mhandlers == null)
				return;

			for (int i = 0; i < mhandlers.Count; ++i)
				mhandlers[i] (e);
		}
	}
}

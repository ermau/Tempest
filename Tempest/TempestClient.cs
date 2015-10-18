//
// TempestClient.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010-2011 Eric Maupin
// Copyright (c) 2011-2015 Xamarin Inc.
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
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Tempest
{
	/// <summary>
	/// Tempest clients.
	/// </summary>
	public class TempestClient
		: MessageHandler, IClientContext, INotifyPropertyChanged
	{
		public TempestClient (IClientConnection connection, MessageTypes mtypes)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");
			if (!Enum.IsDefined (typeof (MessageTypes), mtypes))
				throw new ArgumentException ("Not a valid MessageTypes value", "mtypes");

			this.messageTypes = mtypes;

			this.connection = connection;
			this.connection.Connected += OnConnectionConnected;
			this.connection.Disconnected += OnConnectionDisconnected;

			this.mqueue = new ConcurrentQueue<MessageEventArgs>();
			this.connection.MessageReceived += ConnectionOnMessageReceived;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Raised when the client connects.
		/// </summary>
		public event EventHandler<ClientConnectionEventArgs> Connected;

		/// <summary>
		/// Raised with the client is disconnected.
		/// </summary>
		public event EventHandler<ClientDisconnectedEventArgs> Disconnected;

		public IClientConnection Connection
		{
			get { return this.connection; }
		}

		/// <summary>
		/// Gets whether the client is currently connected or not.
		/// </summary>
		public virtual bool IsConnected
		{
			get { return this.connection.IsConnected; }
		}

		/// <summary>
		/// Attempts to asynchronously connect to <paramref name="target"/>.
		/// </summary>
		/// <param name="target">The target to connect to.</param>
		/// <exception cref="ArgumentNullException"><paramref name="target"/> is <c>null</c>.</exception>
		public virtual Task<ClientConnectionResult> ConnectAsync (Target target)
		{
			if (target == null)
				throw new ArgumentNullException ("target");

			var newWait = new AutoResetEvent (false);
			AutoResetEvent oldWait = Interlocked.Exchange (ref this.mwait, newWait);
			if (oldWait != null)
				oldWait.Set();

			return this.connection.ConnectAsync (target, this.messageTypes);
		}

		/// <summary>
		/// Disconnects from the server.
		/// </summary>
		public virtual Task DisconnectAsync()
		{
			return DisconnectAsyncCore (ConnectionResult.Custom, "Requested");
		}

		public virtual Task DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			return DisconnectAsyncCore (reason, customReason);
		}

		private readonly IClientConnection connection;
		private readonly ConcurrentQueue<MessageEventArgs> mqueue;

		private AutoResetEvent mwait;
		private Task messageRunnerTask;
		private volatile bool running;
		private readonly MessageTypes messageTypes;

		private Task DisconnectAsyncCore (ConnectionResult reason, string customReason)
		{
			Task task = this.connection.DisconnectAsync (reason, customReason);

			this.running = false;

			AutoResetEvent wait = Interlocked.Exchange (ref this.mwait, null);
			if (wait != null)
				wait.Set();

			MessageEventArgs e;
			while (this.mqueue.TryDequeue (out e)) {
			}

			Task runnerTask = Interlocked.Exchange (ref this.messageRunnerTask, null);
			if (runnerTask != null)
				task = Task.WhenAll (task, runnerTask);

			return task;
		}

		private void ConnectionOnMessageReceived (object sender, MessageEventArgs e)
		{
			this.mqueue.Enqueue (e);

			AutoResetEvent wait = this.mwait;
			if (wait != null)
				wait.Set();
		}

		private void MessageRunner ()
		{
			ConcurrentQueue<MessageEventArgs> q = this.mqueue;

			while (this.running)
			{
				while (q.Count > 0 && this.running)
				{
					MessageEventArgs e;
					if (!q.TryDequeue (out e))
						continue;

					List<Action<MessageEventArgs>> mhandlers = GetHandlers (e.Message);
					if (mhandlers == null)
						continue;

					for (int i = 0; i < mhandlers.Count; ++i)
						mhandlers[i] (e);
				}

				if (q.Count == 0)
				{
					AutoResetEvent wait = this.mwait;
					if (wait != null)
						wait.WaitOne();
					else
						return;
				}
			}
		}

		private void OnConnectionDisconnected (object sender, DisconnectedEventArgs e)
		{
			OnPropertyChanged (new PropertyChangedEventArgs ("IsConnected"));
			DisconnectAsyncCore (e.Result, e.CustomReason);
			OnDisconnected (new ClientDisconnectedEventArgs (e.Result == ConnectionResult.Custom, e.Result, e.CustomReason));
		}

		private void OnConnectionConnected (object sender, ClientConnectionEventArgs e)
		{
			OnPropertyChanged (new PropertyChangedEventArgs ("IsConnected"));

			this.running = true;

			SpinWait wait = new SpinWait();
			while (this.messageRunnerTask != null)
				wait.SpinOnce();

			this.messageRunnerTask = Task.Factory.StartNew (MessageRunner, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

			OnConnected (e);
		}

		protected virtual void OnPropertyChanged (PropertyChangedEventArgs e)
		{
			var changed = PropertyChanged;
			if (changed != null)
				changed (this, e);
		}

		protected virtual void OnConnected (ClientConnectionEventArgs e)
		{
			EventHandler<ClientConnectionEventArgs> handler = Connected;
			if (handler != null)
				handler (this, e);
		}

		protected virtual void OnDisconnected (ClientDisconnectedEventArgs e)
		{
			var handler = Disconnected;
			if (handler != null)
				handler (this, e);
		}
	}

	public class ClientDisconnectedEventArgs
		: EventArgs
	{
		public ClientDisconnectedEventArgs (bool requested, ConnectionResult reason, string customReason)
		{
			Requested = requested;
			Reason = reason;
			CustomReason = customReason;
		}

		/// <summary>
		/// Gets whether the disconnection was requested by LocalClient.
		/// </summary>
		public bool Requested
		{
			get;
			private set;
		}

		public ConnectionResult Reason
		{
			get;
			private set;
		}

		public string CustomReason
		{
			get;
			private set;
		}
	}
}
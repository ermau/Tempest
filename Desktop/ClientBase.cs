//
// ClientBase.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011 Eric Maupin
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
using System.Linq;
using System.Net;
using System.Threading;
using Cadenza.Collections;
using Tempest.InternalProtocol;

namespace Tempest
{
	/// <summary>
	/// Base class for Tempest clients.
	/// </summary>
	public abstract class ClientBase
		: MessageHandler, IClientContext, INotifyPropertyChanged
	{
		protected ClientBase (IClientConnection connection, MessageTypes mtypes, bool poll = false)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");

			this.messageTypes = mtypes;

			this.connection = connection;
			this.connection.Connected += OnConnectionConnected;
			this.connection.Disconnected += OnConnectionDisconnected;

			if ((this.connection.Modes & MessagingModes.Inline) == MessagingModes.Inline && (Environment.ProcessorCount == 1 || (this.connection.Modes & MessagingModes.Async) != MessagingModes.Async))
				this.mode = MessagingModes.Inline;
			else
			{
				this.mqueue = new Queue<MessageEventArgs>();
				this.mwait = new AutoResetEvent (false);
				this.connection.MessageReceived += ConnectionOnMessageReceived;
				this.mode = MessagingModes.Async;
			}

			this.polling = poll;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Raised when the client connects.
		/// </summary>
		public event EventHandler Connected;

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
		/// Attempts to asynchronously connect to <paramref name="endPoint"/>.
		/// </summary>
		/// <param name="endPoint">The endpoint to connect to.</param>
		/// <exception cref="ArgumentNullException"><paramref name="endPoint"/> is <c>null</c>.</exception>
		public virtual void ConnectAsync (EndPoint endPoint)
		{
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");

			this.connection.ConnectAsync (endPoint, this.messageTypes);
		}

		/// <summary>
		/// Attempts to connect to <paramref name="endPoint"/>.
		/// </summary>
		/// <param name="endPoint">The endpoint to connect to.</param>
		/// <param name="timeout">Maximum number of milliseconds to wait for a connection.</param>
		/// <exception cref="ArgumentNullException"><paramref name="endPoint"/> is <c>null</c>.</exception>
		/// <returns>
		/// The result of the connection attempt.
		/// </returns>
		public virtual ConnectionResult Connect (EndPoint endPoint, int timeout = -1)
		{
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");

			return this.connection.Connect (endPoint, this.messageTypes, timeout);
		}

		/// <summary>
		/// Disconnects from the server.
		/// </summary>
		/// <param name="now">Whether or not to disconnect immediately or wait for pending messages.</param>
		public virtual void Disconnect (bool now)
		{
			Disconnect (now, ConnectionResult.Custom, "Requested");
		}

		/// <summary>
		/// Disconnects after sending a disconnection message with <see cref="reason"/>.
		/// </summary>
		/// <param name="reason">The reason given for disconnection.</param>
		/// <exception cref="ArgumentNullException"><paramref name="reason"/> is <c>null</c>.</exception>
		public void DisconnectWithReason (string reason)
		{
			if (reason == null)
				throw new ArgumentNullException ("reason");

			this.connection.Send (new DisconnectMessage { Reason = ConnectionResult.Custom, CustomReason = reason });
			Disconnect (false, ConnectionResult.Custom, reason);
		}

		/// <summary>
		/// Manually polls the connection and invokes message handlers.
		/// </summary>
		public void Poll()
		{
			if (!IsConnected)
				return;

			List<MessageEventArgs> messages;
			if (this.mode == MessagingModes.Async)
			{
				messages = new List<MessageEventArgs> (mqueue.Count);

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
				var mhandlers = GetHandlers (m.Message);
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

		private readonly Queue<MessageEventArgs> mqueue;
		private readonly AutoResetEvent mwait;
		private Thread messageRunner;
		protected volatile bool running;
		private readonly MessageTypes messageTypes;

		private void Disconnect (bool now, ConnectionResult reason, string customReason)
		{
			this.disconnecting = true;
			this.connection.Disconnect (now, reason);

			if (!now)
				return;

			this.running = false;

			if (this.mode == MessagingModes.Async)
			{
				this.mwait.Set();
					
				lock (this.mqueue)
					this.mqueue.Clear();
			}

			Thread runner = this.messageRunner;
			this.messageRunner = null;

			if (runner != null && Thread.CurrentThread != runner)
				runner.Join();
		}

		private void ConnectionOnMessageReceived (object sender, MessageEventArgs e)
		{
			lock (this.mqueue)
				this.mqueue.Enqueue (e);

			this.mwait.Set();
		}

		private void InlineMessageRunner()
		{
			#if NET_4
			SpinWait wait = new SpinWait();
			#endif

		    while (this.running)
		    {
		        IEnumerable<MessageEventArgs> messages = this.connection.Tick();
		        while (this.running && messages.Any())
		        {
					#if NET_4
					wait.Reset();
					#endif

		            foreach (MessageEventArgs e in messages)
		            {
		                if (!this.running)
		                    break;

		                var mhandlers = GetHandlers (e.Message);
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

				#if NET_4
				wait.SpinOnce();
				#else
				Thread.Sleep (1);
				#endif
		    }
		}

		private void AsyncMessageRunner ()
		{
			Queue<MessageEventArgs> q = this.mqueue;

			while (this.running)
			{
				while (q.Count > 0)
				{
					if (!this.running)
						break;

					MessageEventArgs e;
					lock (q)
					{
						if (q.Count == 0)
							continue;

						e = q.Dequeue();
					}

					var mhandlers = GetHandlers (e.Message);
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

		private void OnConnectionDisconnected (object sender, DisconnectedEventArgs e)
		{
			OnPropertyChanged (new PropertyChangedEventArgs ("IsConnected"));
			Disconnect (true, e.Result, e.CustomReason);
			OnDisconnected (new ClientDisconnectedEventArgs (e.Result == ConnectionResult.Custom, e.Result, e.CustomReason));
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

		protected virtual void OnPropertyChanged (PropertyChangedEventArgs e)
		{
			var changed = PropertyChanged;
			if (changed != null)
				changed (this, e);
		}

		protected virtual void OnConnected (EventArgs e)
		{
			EventHandler handler = Connected;
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
		/// Gets whether the disconnection was requested by ClientBase.
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
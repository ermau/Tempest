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
using System.Collections.Generic;
using System.Linq;

namespace Tempest
{
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
		public void AddConnectionProvider (IConnectionProvider provider)
		{
			if (provider == null)
				throw new ArgumentNullException ("provider");

			lock (this.providers)
				this.providers.Add (provider);

			provider.ConnectionMade += OnConnectionMade;
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

			lock (this.providers)
			{
				if (!this.providers.Remove (provider))
					return;
			}

			provider.Stop();
			provider.ConnectionMade -= OnConnectionMade;
		}

		protected readonly HashSet<IConnection> connections = new HashSet<IConnection>();
		private readonly HashSet<IConnectionProvider> providers = new HashSet<IConnectionProvider>();
		private readonly MessageTypes messageTypes;

		protected virtual void OnConnectionMade (object sender, ConnectionMadeEventArgs e)
		{
			if (e.Rejected)
				return;

			lock (this.connections)
				this.connections.Add (e.Connection);
			
			e.Connection.MessageReceived += OnConnectionMessageReceived;
			e.Connection.Disconnected += OnConnectionDisconnected;
		}

		protected virtual void OnConnectionDisconnected (object sender, ConnectionEventArgs e)
		{
			lock (this.connections)
				this.connections.Remove (e.Connection);

			e.Connection.MessageReceived -= OnConnectionMessageReceived;
			e.Connection.Disconnected -= OnConnectionDisconnected;
		}

		protected virtual void OnConnectionMessageReceived (object sender, MessageEventArgs e)
		{
			lock (this.connections)
			{
				if (!this.connections.Contains (e.Connection))
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

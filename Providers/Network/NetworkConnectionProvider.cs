//
// NetworkConnectionProvider.cs
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
using System.Net.Sockets;

#if NET_4
using System.Collections.Concurrent;
#endif

namespace Tempest.Providers.Network
{
	/// <summary>
	/// High performance socket based <see cref="IConnectionProvider"/>.
	/// </summary>
	public class NetworkConnectionProvider
		: IConnectionProvider
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NetworkConnectionProvider"/> class.
		/// </summary>
		/// <param name="endPoint">The endpoint to listen to.</param>
		/// <param name="appId">The application identifier (used as a sanity byte).</param>
		public NetworkConnectionProvider (IPEndPoint endPoint, byte appId)
		{
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");

			this.sanityByte = appId;
			this.endPoint = endPoint;
		}
		
		public event EventHandler<ConnectionMadeEventArgs> ConnectionMade;
		
		public event EventHandler<ConnectionlessMessageReceivedEventArgs> ConnectionlessMessageReceived
		{
			add { throw new NotSupportedException(); }
			remove { throw new NotSupportedException(); }
		}

		public IPEndPoint EndPoint
		{
			get { return this.endPoint; }
		}

		public bool SupportsConnectionless
		{
			get { return false; }
		}

		public void Start (MessageTypes types)
		{
			if (this.running)
				return;

			this.running = true;
			this.mtypes = types;
			
			if ((types & MessageTypes.Reliable) == MessageTypes.Reliable)
			{
				this.reliableSocket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				this.reliableSocket.Bind (this.endPoint);
				this.reliableSocket.Listen ((int)SocketOptionName.MaxConnections);
				
				BeginAccepting (null);
			}

			if ((types & MessageTypes.Unreliable) == MessageTypes.Unreliable)
				throw new NotSupportedException();
		}

		public void SendConnectionlessMessage (Message message, EndPoint endPoint)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");
			
			throw new NotSupportedException();
		}

		public void Stop()
		{
			if (!this.running)
				return;

			this.running = false;

			if (this.reliableSocket != null)
			{
				this.reliableSocket.Close();
				this.reliableSocket = null;
			}

			if (this.unreliableSocket != null)
			{
				this.unreliableSocket.Close();
				this.unreliableSocket = null;
			}

			lock (this.serverConnections)
			{
				foreach (NetworkServerConnection c in this.serverConnections)
					c.Dispose();

				this.serverConnections.Clear();
			}
		}

		public void Dispose()
		{
			Dispose (true);
		}

		protected virtual void Dispose (bool disposing)
		{
			Stop();
		}

		private volatile bool running;
		private Socket reliableSocket;
		private Socket unreliableSocket;
		private MessageTypes mtypes;
		private readonly byte sanityByte;
		private IPEndPoint endPoint;

		private readonly List<NetworkServerConnection> serverConnections = new List<NetworkServerConnection> (100);

		private void Accept (object sender, SocketAsyncEventArgs e)
		{
			if (!this.running)
			{
				e.Dispose();
				return;
			}

			var connection = new NetworkServerConnection (e.AcceptSocket, this.sanityByte);

			BeginAccepting (e);

			var made = new ConnectionMadeEventArgs (connection);
			OnConnectionMade (made);
			
			if (made.Rejected)
				connection.Dispose();
			else
			{
				lock (this.serverConnections)
					this.serverConnections.Add (connection);
			}
		}

		private void BeginAccepting (SocketAsyncEventArgs e)
		{
			if (e == null)
			{
				e = new SocketAsyncEventArgs ();
				e.Completed += Accept;
			}
			else
			{
				Socket s = null;
				#if NET_4
				if (!ReliableSockets.TryPop (out s))
					s = null;
				#else
				if (ReliableSockets.Count != 0)
				{
					lock (ReliableSockets)
					{
						if (ReliableSockets.Count != 0)
							s = ReliableSockets.Pop();
					}
				}
				#endif

				e.AcceptSocket = s;
			}

			if (!this.reliableSocket.AcceptAsync (e))
				Accept (this, e);
		}

		private void OnConnectionMade (ConnectionMadeEventArgs e)
		{
			var cmade = this.ConnectionMade;
			if (cmade != null)
				cmade (this, e);
		}

		#if NET_4
		internal static readonly ConcurrentStack<Socket> ReliableSockets = new ConcurrentStack<Socket>();
		#else
		internal static readonly Stack<Socket> ReliableSockets = new Stack<Socket>();
		#endif
	}
}
//
// NetworkConnectionProvider.cs
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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;

#if NET_4
using System.Threading.Tasks;
#endif

using System.Threading;

#if NET_4
using System.Threading.Tasks;
#endif
namespace Tempest.Providers.Network
{
	/// <summary>
	/// High performance socket based <see cref="IConnectionProvider"/>.
	/// </summary>
	public sealed class NetworkConnectionProvider
		: IConnectionProvider
	{
		#if !SILVERLIGHT
		/// <summary>
		/// Initializes a new instance of the <see cref="NetworkConnectionProvider"/> class.
		/// </summary>
		/// <param name="endPoint">The endpoint to listen to.</param>
		/// <param name="maxConnections">Maximum number of connections to allow.</param>
		/// <param name="protocol">The protocol to accept.</param>
		/// <exception cref="ArgumentNullException"><paramref name="endPoint"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxConnections"/> is &lt;= 0</exception>
		public NetworkConnectionProvider (Protocol protocol, IPEndPoint endPoint, int maxConnections)
			: this (new [] { protocol }, endPoint, maxConnections)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NetworkConnectionProvider"/> class.
		/// </summary>
		/// <param name="endPoint">The endpoint to listen to.</param>
		/// <param name="maxConnections">Maximum number of connections to allow.</param>
		/// <param name="protocols">The protocols to accept.</param>
		/// <exception cref="ArgumentNullException"><paramref name="endPoint"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxConnections"/> is &lt;= 0</exception>
		public NetworkConnectionProvider (IEnumerable<Protocol> protocols, IPEndPoint endPoint, int maxConnections)
			: this (protocols, endPoint, maxConnections, () => new RSACrypto())
		{
		}
		#endif

		public NetworkConnectionProvider (IEnumerable<Protocol> protocols, IPEndPoint endPoint, int maxConnections, Func<IPublicKeyCrypto> pkCryptoFactory)
		{
			if (pkCryptoFactory == null)
				throw new ArgumentNullException ("pkCryptoFactory");
			if (protocols == null)
				throw new ArgumentNullException ("protocols");
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");
			if (maxConnections <= 0)
				throw new ArgumentOutOfRangeException ("maxConnections");

			this.protocols = protocols;
			this.endPoint = endPoint;
			MaxConnections = maxConnections;
			this.serverConnections = new List<NetworkServerConnection> (maxConnections);

			this.pkCryptoFactory = pkCryptoFactory;

			if (protocols.Any (p => p.RequiresHandshake))
			{
				ThreadPool.QueueUserWorkItem (s =>
				{
					#if NET_4
					Task encryptKeyGen = Task.Factory.StartNew (() =>
					{
					#endif
						this.pkEncryption = this.pkCryptoFactory();
						this.publicEncryptionKey = this.pkEncryption.ExportKey (false);
					#if NET_4
					});
					#endif

					#if NET_4
					Task authKeyGen = Task.Factory.StartNew (() =>
					{
					#endif
						this.authentication = this.pkCryptoFactory();
						this.authenticationKey = this.authentication.ExportKey (true);
						this.publicAuthenticationKey = this.authentication.ExportKey (false);
					#if NET_4
					});
					#endif

					#if NET_4
					authKeyGen.Wait();
					encryptKeyGen.Wait();
					#endif

					this.keyWait.Set();
				});
			}
			else
				this.keyWait.Set();
		}

		public NetworkConnectionProvider (IEnumerable<Protocol> protocols, IPEndPoint endPoint, int maxConnections, Func<IPublicKeyCrypto> pkCryptoFactory, IAsymmetricKey authKey)
			: this (protocols, endPoint, maxConnections, pkCryptoFactory)
		{
			if (authKey == null)
				throw new ArgumentNullException ("authKey");

			this.authenticationKey = authKey;

			if (this.protocols.Any (p => p.RequiresHandshake))
			{
				ThreadPool.QueueUserWorkItem (s =>
				{
					#if NET_4
					Task encryptKeyGen = Task.Factory.StartNew (() =>
					{
					#endif
						this.pkEncryption = this.pkCryptoFactory();
						this.publicEncryptionKey = this.pkEncryption.ExportKey (false);
					#if NET_4
					});
					#endif

					#if NET_4
					Task authKeyLoad = Task.Factory.StartNew (() =>
					{
					#endif
						this.authentication = this.pkCryptoFactory();
						this.authentication.ImportKey (authKey);
						this.publicAuthenticationKey = this.authentication.ExportKey (false);
					#if NET_4
					});
					#endif

					#if NET_4
					authKeyLoad.Wait();
					encryptKeyGen.Wait();
					#endif

					this.keyWait.Set();
				});
			}
		}

		public event EventHandler PingFrequencyChanged;
		public event EventHandler<ConnectionMadeEventArgs> ConnectionMade;
		
		public event EventHandler<ConnectionlessMessageEventArgs> ConnectionlessMessageReceived
		{
			add { throw new NotSupportedException(); }
			remove { throw new NotSupportedException(); }
		}

		/// <summary>
		/// Gets the maximum number of connections allowed on this provider.
		/// </summary>
		public int MaxConnections
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the end point that this provider listens to for connections.
		/// </summary>
		public IPEndPoint EndPoint
		{
			get { return this.endPoint; }
		}

		public bool SupportsConnectionless
		{
			get { return false; }
		}

		public bool IsRunning
		{
			get { return this.running; }
		}

		/// <summary>
		/// Gets the public authentication key for the server.
		/// </summary>
		public IAsymmetricKey PublicAuthenticationKey
		{
			get { return this.publicAuthenticationKey; }
		}

		/// <summary>
		/// Gets the public encryption key for the server.
		/// </summary>
		public IAsymmetricKey PublicEncryptionKey
		{
			get { return this.publicEncryptionKey; }
		}

		/// <summary>
		/// Gets or sets the frequency (in milliseconds) that the server pings the client. 0 to disable.
		/// </summary>
		public int PingFrequency
		{
			get { return this.pingFrequency; }
			set
			{
				this.pingFrequency = value;

				var changed = PingFrequencyChanged;
				if (changed != null)
					changed (this, EventArgs.Empty);
			}
		}

		public void Start (MessageTypes types)
		{
			if (this.running)
				return;

			this.running = true;
			this.mtypes = types;

			this.keyWait.WaitOne();
			
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

			List<NetworkServerConnection> connections;
			lock (this.serverConnections)
			{
				connections = this.serverConnections.ToList();
				this.serverConnections.Clear();
	
				connections.AddRange (this.pendingConnections.ToList());
				this.pendingConnections.Clear();
			}
			
			foreach (NetworkServerConnection c in connections)
				c.Dispose();
		}

		public void Dispose()
		{
			Dispose (true);
		}

		private void Dispose (bool disposing)
		{
			Stop();
		}

		private int pingFrequency = 15000;

		private volatile bool running;
		private Socket reliableSocket;
		private Socket unreliableSocket;
		private MessageTypes mtypes;

		private readonly ManualResetEvent keyWait = new ManualResetEvent (false);
		internal readonly Func<IPublicKeyCrypto> pkCryptoFactory;

		internal IPublicKeyCrypto pkEncryption;
		private IAsymmetricKey publicEncryptionKey;

		internal IPublicKeyCrypto authentication;
		internal IAsymmetricKey authenticationKey;
		private IAsymmetricKey publicAuthenticationKey;
		
		private readonly IEnumerable<Protocol> protocols;
		private IPEndPoint endPoint;

		private readonly List<NetworkServerConnection> serverConnections;
		private readonly List<NetworkServerConnection> pendingConnections = new List<NetworkServerConnection>();

		internal void Connect (NetworkServerConnection connection)
		{
			lock (this.serverConnections)
			{
				if (this.pendingConnections.Remove (connection))
					this.serverConnections.Add (connection);
			}

			if (!connection.IsConnected)
				return;

			var made = new ConnectionMadeEventArgs (connection, connection.PublicAuthenticationKey);
			OnConnectionMade (made);
			if (made.Rejected)
			{
				Trace.WriteLine ("Connection rejected", "NetworkConnectionProvider Connect");
				connection.Dispose();
			}
		}

		internal void Disconnect (NetworkServerConnection connection)
		{
			lock (this.serverConnections)
			{
				bool atMax = (this.pendingConnections.Count + this.serverConnections.Count == MaxConnections);
				if ((this.pendingConnections.Remove (connection) || this.serverConnections.Remove (connection)) && atMax)
					BeginAccepting (null);
			}
		}

		private void Accept (object sender, SocketAsyncEventArgs e)
		{
			Trace.WriteLine ("Entering", String.Format ("NetworkConnectionProvider Accept({0},{1})", e.BytesTransferred, e.SocketError));

			if (!this.running || e.SocketError != SocketError.Success)
			{
				if (!this.running)
					e.Dispose();
				else
				{
					e.AcceptSocket.Shutdown (SocketShutdown.Both);
					e.AcceptSocket.Disconnect (true);
					//#if !NET_4
					//lock (ReliableSockets)
					//#endif
					//    ReliableSockets.Push (this.reliableSocket);

					BeginAccepting (e);
				}

				Trace.WriteLine ("Exiting", String.Format ("NetworkConnectionProvider Accept({0},{1})", e.BytesTransferred, e.SocketError));
				return;
			}

			var connection = new NetworkServerConnection (this.protocols, e.AcceptSocket, this);

			lock (this.serverConnections)
			{
				if (!connection.IsConnected)
					return;

				if (this.pendingConnections.Count + this.serverConnections.Count == MaxConnections)
				{
					Trace.WriteLine (String.Format ("At MaxConnections ({0}), disconnecting", MaxConnections), String.Format ("NetworkConnectionProvider Accept({0},{1})", e.BytesTransferred, e.SocketError));
					connection.Disconnect (true);
					return;
				}

				BeginAccepting (e);

				this.pendingConnections.Add (connection);
			}
		}

		private void BeginAccepting (SocketAsyncEventArgs e)
		{
			if (!this.running)
				return;

			if (e == null)
			{
				e = new SocketAsyncEventArgs ();
				e.Completed += Accept;
			}
			else
			{
				if (e.SocketError != SocketError.Success)
					return;

				Socket s = null;
				//#if NET_4
				//if (!ReliableSockets.TryPop (out s))
				//    s = null;
				//#else
				//if (ReliableSockets.Count != 0)
				//{
				//    lock (ReliableSockets)
				//    {
				//        if (ReliableSockets.Count != 0)
				//            s = ReliableSockets.Pop();
				//    }
				//}
				//#endif

				e.AcceptSocket = s;
			}

			if (this.running && !this.reliableSocket.AcceptAsync (e))
				Accept (this, e);
		}

		private void OnConnectionMade (ConnectionMadeEventArgs e)
		{
			var cmade = this.ConnectionMade;
			if (cmade != null)
				cmade (this, e);
		}

		//#if NET_4
		//internal static readonly ConcurrentStack<Socket> ReliableSockets = new ConcurrentStack<Socket>();
		//#else
		//internal static readonly Stack<Socket> ReliableSockets = new Stack<Socket>();
		//#endif
	}
}

//
// NetworkConnectionProvider.cs
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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Tempest.Providers.Network
{
	/// <summary>
	/// High performance socket based <see cref="IConnectionProvider"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Potential memory usage is dependent on a number of parameters:
	/// <list type="bullet">
	///		<item>
	///			<description>Each connection maintains a receive buffer with a size potential of <see cref="NetworkConnection.MaxMessageSize"/>.</description>
	///		</item>
	///		<item>
	///			<description><see cref="MaxConnections"/> (set from constructor) determines the max number of connections on the individual <see cref="NetworkConnectionProvider"/>.</description>
	///		</item>
	///		<item>
	///			<description><see cref="NetworkConnection.SendBufferLimit"/> determines how many send buffers (globally) can be created (both clients and servers both), with each having a size potential of <see cref="NetworkConnection.MaxMessageSize"/>.</description>
	///		</item>
	/// </list>
	/// </para>
	/// </remarks>
	public sealed class NetworkConnectionProvider
		: IConnectionProvider
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="NetworkConnectionProvider"/> class.
		/// </summary>
		/// <param name="target">The target to listen to.</param>
		/// <param name="maxConnections">Maximum number of connections to allow.</param>
		/// <param name="protocol">The protocol to accept.</param>
		/// <exception cref="ArgumentNullException"><paramref name="target"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxConnections"/> is &lt;= 0</exception>
		public NetworkConnectionProvider (Protocol protocol, Target target, int maxConnections)
			: this (new [] { protocol }, target, maxConnections)
		{
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NetworkConnectionProvider" /> class.
		/// </summary>
		/// <param name="target">The target to listen to.</param>
		/// <param name="maxConnections">Maximum number of connections to allow.</param>
		/// <param name="protocols">The protocols to accept.</param>
		/// <param name="enabledHashAlgs">
		/// The signature hash algorithms (in order of preference) to enable.
		/// <c>null</c> or an empty collection will enable all of the signature hash algorithms.
		/// </param>
		/// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="protocols" /> is <c>null</c>.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxConnections"/> is &lt;= 0</exception>
		public NetworkConnectionProvider (IEnumerable<Protocol> protocols, Target target, int maxConnections, IEnumerable<string> enabledHashAlgs = null)
		{
			if (protocols == null)
				throw new ArgumentNullException ("protocols");
			if (target == null)
				throw new ArgumentNullException ("target");
			if (maxConnections <= 0)
				throw new ArgumentOutOfRangeException ("maxConnections");

			this.protocols = protocols;
			this.target = target;
			MaxConnections = maxConnections;
			this.serverConnections = new List<NetworkServerConnection> (maxConnections);

			if (protocols.Any (p => p != null && p.RequiresHandshake))
			{
				ThreadPool.QueueUserWorkItem (s =>
				{
					Task encryptKeyGen = Task.Factory.StartNew (() =>
					{
						this.pkEncryption = new RSACrypto();
						this.publicEncryptionKey = this.pkEncryption.ExportKey (false);
					});

					Task authKeyGen = Task.Factory.StartNew (() =>
					{
						this.authentication = new RSACrypto();
						if (this.authenticationKey == null)
							this.authenticationKey = this.authentication.ExportKey (true);
						else
							this.authentication.ImportKey (this.authenticationKey);

						this.publicAuthenticationKey = this.authentication.ExportKey (false);

						if (enabledHashAlgs == null || !enabledHashAlgs.Any())
							this.enabledHashAlgorithms.AddRange (this.authentication.SupportedHashAlgs);
						else // Need to maintain preference order
							this.enabledHashAlgorithms.AddRange (enabledHashAlgs.Where (a => this.authentication.SupportedHashAlgs.Contains (a)));
					});

					authKeyGen.Wait();
					encryptKeyGen.Wait();

					this.keyWait.Set();
				});
			}
			else
				this.keyWait.Set();
		}

		public NetworkConnectionProvider (IEnumerable<Protocol> protocols, Target target, int maxConnections, RSAAsymmetricKey authKey, IEnumerable<string> enabledHashAlgorithms = null)
			: this (protocols, target, maxConnections, enabledHashAlgorithms)
		{
			if (authKey == null)
				throw new ArgumentNullException ("authKey");

			this.authenticationKey = authKey;
		}

		public event EventHandler PingFrequencyChanged;
		public event EventHandler<ConnectionMadeEventArgs> ConnectionMade;

		public IEnumerable<Target> LocalTargets
		{
			get
			{
				if (this.reliableSocket != null && this.reliableSocket.IsBound)
					yield return this.reliableSocket.LocalEndPoint.ToTarget ();
			}
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
		public Target Target
		{
			get { return this.target; }
		}

		public bool IsRunning
		{
			get { return this.running; }
		}

		/// <summary>
		/// Gets the public authentication key for the server.
		/// </summary>
		public RSAAsymmetricKey PublicAuthenticationKey
		{
			get { return this.publicAuthenticationKey; }
		}

		/// <summary>
		/// Gets the public encryption key for the server.
		/// </summary>
		public RSAAsymmetricKey PublicEncryptionKey
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
			string category = "NetworkConnectionProvider Start";
			Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Entering", category);
			if (this.running)
			{
				Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Exiting (already running)", category);
				return;
			}

			this.running = true;
			this.mtypes = types;

			this.pingTimer = new Timer (100);
			this.pingTimer.Start();

			Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Waiting for keys..", category);
			this.keyWait.WaitOne();
			Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Keys ready", category);
			
			if ((types & MessageTypes.Reliable) == MessageTypes.Reliable)
			{
				Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Setting up reliable socket", category);
				this.reliableSocket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				this.reliableSocket.Bind (Target.ToEndPoint());
				this.reliableSocket.Listen (MaxConnections);
				
				Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Reliable socket ready, accepting", category);
				BeginAccepting (null);
			}

			if ((types & MessageTypes.Unreliable) == MessageTypes.Unreliable)
				throw new NotSupportedException();

			Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Exiting", category);
		}

		public void Stop()
		{
			Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Entering", "NetworkConnectionProvider Stop");
			if (!this.running)
			{
				Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Exiting (not running)", "NetworkConnectionProvider Stop");
				return;
			}

			this.running = false;

			Timer t = this.pingTimer;
			if (t != null)
			{
				t.Dispose();
				this.pingTimer = null;
			}

			Socket rs = this.reliableSocket;
			if (rs != null)
			{
				Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Closing reliable socket", "NetworkConnectionProvider Stop");
				rs.Close();
				this.reliableSocket = null;
			}

			Socket us = this.unreliableSocket;
			if (us != null)
			{
				Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Closing unreliable socket", "NetworkConnectionProvider Stop");
				us.Close();
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
			
			Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Disposing connections", "NetworkConnectionProvider Stop");
			foreach (NetworkServerConnection c in connections)
				c.Dispose();

			Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Exiting", "NetworkConnectionProvider Stop");
		}

		public void Dispose()
		{
			Dispose (true);
		}

		private void Dispose (bool disposing)
		{
			Stop();
		}

		private Timer pingTimer;
		private int pingFrequency = 15000;

		private volatile bool running;
		private Socket reliableSocket;
		private Socket unreliableSocket;
		private MessageTypes mtypes;

		private readonly ManualResetEvent keyWait = new ManualResetEvent (false);
		private readonly List<string> enabledHashAlgorithms = new List<string>();

		internal RSACrypto pkEncryption;
		private RSAAsymmetricKey publicEncryptionKey;

		internal RSACrypto authentication;
		internal RSAAsymmetricKey authenticationKey;
		private RSAAsymmetricKey publicAuthenticationKey;
		
		private readonly IEnumerable<Protocol> protocols;
		private Target target;

		private int nextConnectionId;
		private readonly List<NetworkServerConnection> serverConnections;
		private readonly List<NetworkServerConnection> pendingConnections = new List<NetworkServerConnection>();
		
		internal void Connect (NetworkServerConnection connection)
		{
			lock (this.serverConnections)
			{
				if (this.pendingConnections.Remove (connection))
				{
					this.serverConnections.Add (connection);
					this.pingTimer.TimesUp += connection.PingTimerCallback;
				}
			}

			if (!connection.IsConnected)
				return;

			var made = new ConnectionMadeEventArgs (connection, connection.LocalKey);
			OnConnectionMade (made);
			if (made.Rejected)
			{
				Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Connection rejected", "NetworkConnectionProvider ConnectAsync");
				connection.Dispose();
			}
		}

		internal void Disconnect (NetworkServerConnection connection)
		{
			lock (this.serverConnections)
			{
			    bool atMax = (this.pendingConnections.Count + this.serverConnections.Count == MaxConnections);

				bool connected = this.serverConnections.Remove (connection);
				if (connected) {
					this.pingTimer.TimesUp -= connection.PingTimerCallback;

					if (NetworkConnection.AutoSizeSendBufferLimit)
						Interlocked.Add (ref NetworkConnection.sendBufferLimit, NetworkConnection.AutoSizeFactor * -1);
				}

				if ((connected || this.pendingConnections.Remove (connection)) && atMax) {
					BeginAccepting (null);
				}
			}
		}

		internal int GetConnectionId()
		{
			while (true)
			{
				int id;
				do
				{
					id = Interlocked.Increment (ref this.nextConnectionId);
				} while (id == 0);

				bool found = false;
				lock (this.serverConnections)
				{
					for (int i = 0; i < this.serverConnections.Count; i++)
					{
						if (this.serverConnections[i].ConnectionId == id)
						{
							found = true;
							break;
						}
					}
				}

				if (!found)
					return id;
			}
		}

		private void Accept (object sender, SocketAsyncEventArgs e)
		{
			Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Entering", String.Format ("NetworkConnectionProvider Accept({0},{1})", e.BytesTransferred, e.SocketError));

			if (!this.running || e.SocketError != SocketError.Success)
			{
				if (!this.running)
					e.Dispose();
				else
				{
					#if !SILVERLIGHT
					e.AcceptSocket.Shutdown (SocketShutdown.Both);
					e.AcceptSocket.Disconnect (true);
					//    ReliableSockets.Push (this.reliableSocket);
					#else
					e.ConnectSocket.Dispose();
					#endif
					BeginAccepting (e);
				}

				Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, "Exiting", String.Format ("NetworkConnectionProvider Accept({0},{1})", e.BytesTransferred, e.SocketError));
				return;
			}

			var connection = new NetworkServerConnection (this.enabledHashAlgorithms, this.protocols, e.AcceptSocket, this);

			lock (this.serverConnections)
			{
				if (!connection.IsConnected)
					return;

				if (this.pendingConnections.Count + this.serverConnections.Count == MaxConnections)
				{
					Trace.WriteLineIf (NetworkConnection.NTrace.TraceVerbose, String.Format ("At MaxConnections ({0}), disconnecting", MaxConnections), String.Format ("NetworkConnectionProvider Accept({0},{1})", e.BytesTransferred, e.SocketError));
					connection.DisconnectAsync();
					return;
				}

				BeginAccepting (e);

				this.pendingConnections.Add (connection);

				if (NetworkConnection.AutoSizeSendBufferLimit)
					Interlocked.Add (ref NetworkConnection.sendBufferLimit, NetworkConnection.AutoSizeFactor);
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
				//if (!ReliableSockets.TryPop (out s))
				//    s = null;

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

		//internal static readonly ConcurrentStack<Socket> ReliableSockets = new ConcurrentStack<Socket>();
	}
}

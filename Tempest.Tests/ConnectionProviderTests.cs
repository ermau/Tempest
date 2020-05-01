//
// ConnectionProviderTests.cs
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
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Tempest.InternalProtocol;

namespace Tempest.Tests
{
	public abstract class ConnectionProviderTests
	{
		protected IConnectionProvider provider;
		protected readonly List<IClientConnection> connections = new List<IClientConnection>();

		private readonly List<Exception> exceptions = new List<Exception>();

		private Random random;

		[SetUp]
		protected void Setup()
		{
			#if !NETFX_CORE
			AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
			#endif

			Trace.WriteLine ("Entering", "Setup");
			random = new Random (43);
			this.provider = SetUp();
			Trace.WriteLine ("Exiting", "Setup");
		}

		#if !NETFX_CORE
		private void HandleUnhandledException (object sender, UnhandledExceptionEventArgs e)
		{
			lock (this.exceptions)
				this.exceptions.Add ((Exception)e.ExceptionObject);
		}
		#endif

		[TearDown]
		protected void TearDown()
		{
			Trace.WriteLine ("Entering", "TearDown");

			if (this.provider != null)
				this.provider.Dispose();

			lock (this.connections)
			{
			    foreach (var c in this.connections)
			        c.Dispose();

			    this.connections.Clear();
			}

			#if !NETFX_CORE
			AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;

			if (this.exceptions.Count > 0)
			{
				Exception[] exs = this.exceptions.ToArray();
				this.exceptions.Clear();

				throw new AggregateException (exs);
			}
			#endif

			Trace.WriteLine ("Exiting", "TearDown");
		}

		protected virtual int MaxPayloadSize
		{
			get { return 0; }
		}

		protected abstract Target Target { get; }
		protected abstract MessageTypes MessageTypes { get; }

		protected abstract IConnectionProvider SetUp();
		protected abstract IConnectionProvider SetUp (IEnumerable<Protocol> protocols);

		protected abstract IClientConnection SetupClientConnection();
		protected virtual IClientConnection SetupClientConnection (out RSAAsymmetricKey key)
		{
			key = null;
			return SetupClientConnection();
		}

		protected abstract IClientConnection SetupClientConnection (IEnumerable<Protocol> protocols);

		protected IClientConnection GetNewClientConnection()
		{
			var c = SetupClientConnection();

			lock (this.connections)
			    this.connections.Add (c);

			return c;
		}

		protected IClientConnection GetNewClientConnection (out RSAAsymmetricKey key)
		{
			var c = SetupClientConnection (out key);

			lock (this.connections)
				this.connections.Add (c);

			return c;
		}

		[Test]
		public void InvalidProtocolVersion()
		{
			TearDown();

			this.provider = SetUp (new[] { new Protocol (5, 5, 4) });
			this.provider.Start (MessageTypes);

			var client = SetupClientConnection (new[] { new Protocol (5, 3) });

			var test = new AsyncTest (e => Assert.AreEqual (ConnectionResult.IncompatibleVersion, ((DisconnectedEventArgs)e).Result));
			client.Connected += test.FailHandler;
			client.Disconnected += test.PassHandler;

			client.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void OlderCompatibleVersion()
		{
			TearDown();

			this.provider = SetUp (new[] { new Protocol (5, 5, 4) });
			this.provider.Start (MessageTypes);

			var client = SetupClientConnection (new[] { new Protocol (5, 4) });

			var test = new AsyncTest();
			client.Connected += test.PassHandler;
			client.Disconnected += test.FailHandler;

			client.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test]
		public void StartRepeatedly()
		{
			// *knock knock knock* Penny
			Assert.DoesNotThrow (() => this.provider.Start (MessageTypes));
			// *knock knock knock* Penny
			Assert.DoesNotThrow (() => this.provider.Start (MessageTypes));
			// *knock knock knock* Penny
			Assert.DoesNotThrow (() => this.provider.Start (MessageTypes));
		}

		[Test]
		public void StopRepeatedly()
		{
			// *knock knock knock* Sheldon
			Assert.DoesNotThrow (() => this.provider.Stop());
			// *knock knock knock* Sheldon
			Assert.DoesNotThrow (() => this.provider.Stop());
			// *knock knock knock* Sheldon
			Assert.DoesNotThrow (() => this.provider.Stop());
		}

		[Test, Repeat (3)]
		public void IsRunning()
		{
			Assert.IsFalse (provider.IsRunning);

			provider.Start (MessageTypes);
			Assert.IsTrue (provider.IsRunning);

			provider.Stop();
			Assert.IsFalse (provider.IsRunning);
		}

		[Test, Repeat (3)]
		public void ConnectionMade()
		{
			this.provider.Start (MessageTypes);

			var test = new AsyncTest();
			this.provider.ConnectionMade += test.PassHandler;
			var c = GetNewClientConnection();
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void ConnectedWithKey()
		{
			this.provider.Start (MessageTypes);

			RSAAsymmetricKey key = null;

			var test = new AsyncTest (e =>
			{
				var ce = (ConnectionMadeEventArgs)e;

				Assert.IsNotNull (ce.ClientPublicKey);
				Assert.AreEqual (key, ce.ClientPublicKey);
			});

			this.provider.ConnectionMade += test.PassHandler;

			var c = GetNewClientConnection (out key);
			if (key == null)
				Assert.Ignore();

			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void Connected()
		{
			this.provider.Start (MessageTypes);

			var c = GetNewClientConnection();

			var test = new AsyncTest();
			c.Connected += test.PassHandler;
			c.Disconnected += test.FailHandler;
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void ConnectFromDisconnected()
		{
			this.provider.Start (MessageTypes);

			var wait = new ManualResetEvent (false);
			var test = new AsyncTest();

			var c = GetNewClientConnection();
			bool first = false;
			c.Connected += (s, e) =>
			{
				if (!first)
				{
					first = true;
					wait.Set();
				}
				else
				{
					test.PassHandler (s, e);
				}
			};

			EventHandler<DisconnectedEventArgs> dhandler = null;
			dhandler = (s, e) =>
			{
				c.Disconnected -= dhandler;
				c.ConnectAsync (Target, this.MessageTypes);
			};

			c.Disconnected += dhandler;

			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			c.DisconnectAsync();

			test.Assert (5000);
		}

		[Test, Repeat (3)]
		public void ClientSendMessageAsync()
		{
			const string content = "Oh, hello there.";

			var c = GetNewClientConnection();

			IServerConnection connection = null;

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (me.Connection, connection);

				var msg = (me.Message as MockMessage);
				Assert.IsNotNull (msg);
				Assert.AreEqual (content, msg.Content);
			});

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) =>
			{
				connection = e.Connection;
				e.Connection.MessageReceived += test.PassHandler;
			};

			c.Connected += (sender, e) => ThreadPool.QueueUserWorkItem (o => c.SendAsync (new MockMessage { Content = content }));
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void ClientSendMessageConnectedHandler()
		{
			const string content = "Oh, hello there.";

			var c = GetNewClientConnection();

			IServerConnection connection = null;

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (me.Connection, connection);

				var msg = (me.Message as MockMessage);
				Assert.IsNotNull (msg);
				Assert.AreEqual (content, msg.Content);
			});

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) =>
			{
				connection = e.Connection;
				e.Connection.MessageReceived += test.PassHandler;
			};

			c.Connected += (sender, e) => c.SendAsync (new MockMessage { Content = content });
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void ClientMessageSent()
		{
			const string content = "Oh, hello there.";

			var c = GetNewClientConnection();

			var test = new AsyncTest();

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) =>
			{
				Task<bool> task = c.SendAsync (new MockMessage { Content = content });
				task.Wait();

				if (task.Result)
					test.PassHandler (null, EventArgs.Empty);
				else
					test.FailHandler (null, EventArgs.Empty);
			};
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void ServerSendMessageAsync()
		{
			const string content = "Oh, hello there.";

			var c = GetNewClientConnection();

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (c, me.Connection);

				var msg = (me.Message as MockMessage);
				Assert.IsNotNull (msg);
				Assert.AreEqual (content, msg.Content);
			});

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) => e.Connection.SendAsync (new MockMessage { Content = content });

			c.Disconnected += test.FailHandler;
			c.MessageReceived += test.PassHandler;
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void SendLongMessageAsync()
		{
			string content = TestHelpers.GetLongString (random, MaxPayloadSize - sizeof (int));

			var c = GetNewClientConnection();

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (c, me.Connection);

				var msg = (me.Message as MockMessage);
				Assert.IsNotNull (msg);
				Assert.AreEqual (content, msg.Content);
			});

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) => e.Connection.SendAsync (new MockMessage { Content = content });

			c.Disconnected += test.FailHandler;
			c.MessageReceived += test.PassHandler;
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (30000);
		}

		[Test, Repeat (3)]
		public void Stress()
		{
			var c = GetNewClientConnection();

			const int messages = 1000;
			int message = 0;

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull(me);
				Assert.AreSame (c, me.Connection);

				Assert.AreEqual ((message++).ToString(), ((MockMessage)me.Message).Content);
			}, messages);

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) => (new Thread (() =>
			{
				try
				{
					for (int i = 0; i < messages; ++i)
					{
						if (i > Int32.MaxValue)
							System.Diagnostics.Debugger.Break();

						if (!e.Connection.IsConnected)
							return;

						e.Connection.SendAsync (new MockMessage { Content = i.ToString() });
					}
				}
				catch (Exception ex)
				{
					test.FailWith (ex);
				}
			})).Start();

			c.Disconnected += test.FailHandler;
			c.MessageReceived += test.PassHandler;
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (60000);
		}

		[Test, Repeat (3)]
		public void StressConcurrentSends()
		{
			var c = GetNewClientConnection();

			const int messages = 10000;
			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull(me);
				Assert.AreSame (c, me.Connection);

				int n;
				Assert.IsTrue (Int32.TryParse (((MockMessage)me.Message).Content, out n));
			}, messages);

			const int threads = 4;
			ParameterizedThreadStart thread = s =>
			{
				IConnection cn = (IConnection)s;
				try
				{
					for (int i = 0; i < (messages / threads); ++i)
					{
						if (i > Int32.MaxValue)
							System.Diagnostics.Debugger.Break();

						if (!cn.IsConnected)
							return;

						cn.SendAsync (new MockMessage { Content = i.ToString() });
						Thread.Sleep (1);
					}
				}
				catch (Exception ex)
				{
					test.FailWith (ex);
				}
			};

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) =>
			{
				for (int i = 0; i < threads; ++i)
					new Thread (thread).Start (e.Connection);
			};

			c.Disconnected += test.FailHandler;
			c.MessageReceived += test.PassHandler;
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (600000);
		}

		[Test, Repeat (3)]
		public void StressAuthenticatedAndEncrypted()
		{
			var c = GetNewClientConnection();

			const int messages = 1000;
			int number = 0;

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (c, me.Connection);

				Assert.AreEqual (number, ((AuthenticatedAndEncryptedMessage)me.Message).Number);
				Assert.AreEqual (number++.ToString(), ((AuthenticatedAndEncryptedMessage)me.Message).Message);
			}, messages);

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) => (new Thread (() =>
			{
				try
				{
					for (int i = 0; i < messages; ++i)
					{
						if (i > Int32.MaxValue)
							System.Diagnostics.Debugger.Break();
						
						if (!e.Connection.IsConnected)
							return;

						e.Connection.SendAsync (new AuthenticatedAndEncryptedMessage { Number = i, Message = i.ToString() });
					}
				}
				catch (Exception ex)
				{
					test.FailWith (ex);
				}
			})).Start();

			c.Disconnected += test.FailHandler;
			c.MessageReceived += test.PassHandler;
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (80000);
		}

		[Test, Repeat (3)]
		public void ConnectionFailed()
		{
			Assert.IsFalse (provider.IsRunning);

			var test = new AsyncTest();
			var c = GetNewClientConnection();
			c.Connected += test.FailHandler;
			c.Disconnected += (s, e) =>
			{
				if (e.Result == ConnectionResult.ConnectionFailed)
					test.PassHandler (s, e);
				else
					test.FailHandler (s, e);
			};

			c.ConnectAsync (Target, MessageTypes);

			test.Assert (40000);
		}

		[Test, Repeat (3)]
		public void StressRandomLongAuthenticatedMessage()
		{
			var c = GetNewClientConnection();

			const int messages = 1000;
			int number = 0;

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);
				Assert.AreSame (c, me.Connection);

				Assert.AreEqual (number++, ((AuthenticatedMessage)me.Message).Number);
				Assert.IsTrue ((((AuthenticatedMessage)me.Message).Message.Length >= 7500));
			}, messages);

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) => (new Thread (() =>
			{
				try
				{
					for (int i = 0; i < messages; ++i)
					{
						if (!e.Connection.IsConnected)
							return;

						e.Connection.SendAsync (new AuthenticatedMessage
						{
							Number = i,
							Message = TestHelpers.GetLongString (random, MaxPayloadSize - sizeof(int) * 2)
						});
					}
				}
				catch (Exception ex)
				{
					test.FailWith (ex);
				}
			})).Start();

			c.Disconnected += test.FailHandler;
			c.MessageReceived += test.PassHandler;
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (900000);
		}

		[Test, Repeat (3)]
		public void DisconnectFromClientOnClient()
		{
			this.provider.Start (MessageTypes);

			var test = new AsyncTest (e =>
			{
				var args = (DisconnectedEventArgs)e;
				switch (args.Result)
				{
					case ConnectionResult.ConnectionFailed:
						return false;

					default:
						return true;
				}
			});

			this.provider.ConnectionMade += (s, e) => {
				e.Connection.Disconnected += (ds, de) => {
					Assert.IsFalse (e.Connection.IsConnected, "IsConnected was still true");
					test.PassHandler (ds, de);
				};
			};

			var c = GetNewClientConnection();

			var wait = new AutoResetEvent (false);

			c.Disconnected += (s, e) => {
				Assert.IsFalse (c.IsConnected, "IsConnected was still true");
				test.PassHandler (s, e);
			};

			c.Connected += (sender, e) => wait.Set();
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");
			
			c.DisconnectAsync();

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectFromClientOnClientInConnectedHandler()
		{
			this.provider.Start (MessageTypes);
			
			var test = new AsyncTest (e =>
			{
				var args = (DisconnectedEventArgs)e;
				switch (args.Result)
				{
					case ConnectionResult.ConnectionFailed:
						return false;

					default:
						return true;
				}
			}, 2);

			this.provider.ConnectionMade += (s, e) => {
				e.Connection.Disconnected += (ds, de) => {
					Assert.IsFalse (e.Connection.IsConnected, "IsConnected was still true");
					test.PassHandler (ds, de);
				};
			};

			var c = GetNewClientConnection();

			c.Disconnected += (ds, de) => {
				Assert.IsFalse (c.IsConnected, "IsConnected was still true");
				test.PassHandler (ds, de);
			};

			c.Connected += (sender, e) => c.DisconnectAsync();
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectFromClientOnServer()
		{
			var test = new AsyncTest (2);

			var c = GetNewClientConnection();
			c.Disconnected += (s, e) => {
				Assert.IsFalse (c.IsConnected, "IsConnected was still true");
				test.PassHandler (s, e);
			};;

			var wait = new AutoResetEvent (false);

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (s, e) =>
			{
				e.Connection.Disconnected += (ds, de) => {
					Assert.IsFalse (e.Connection.IsConnected, "IsConnected was still true");
					test.PassHandler (ds, de);
				};
				wait.Set();
			};

			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			c.DisconnectAsync();
			
			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectFromServerOnClient()
		{
			var test = new AsyncTest (2);

			var c = GetNewClientConnection();
			c.Disconnected += (ds, de) => {
				Assert.IsFalse (c.IsConnected, "IsConnected was still true");
				test.PassHandler (ds, de);
			};

			this.provider.Start (MessageTypes);

			IServerConnection sc = null;

			var wait = new AutoResetEvent (false);
			this.provider.ConnectionMade += (s, e) => {
				e.Connection.Disconnected += (ds, de) => {
					Assert.IsFalse (e.Connection.IsConnected, "IsConnected was still true");
					test.PassHandler (ds, de);
				};

				sc = e.Connection;
				wait.Set();
			};

			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000) || sc == null)
				Assert.Fail ("Failed to connect");

			sc.DisconnectAsync();

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void ConnectionRejected()
		{
			var test = new AsyncTest();

			var c = GetNewClientConnection();
			c.Disconnected += test.PassHandler;

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) => e.Rejected = true;
			
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectFromServerOnServerWithinConnectionMade()
		{
			var test = new AsyncTest (2);

			var c = GetNewClientConnection();
			c.Disconnected += test.PassHandler;

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) =>
			{
				e.Connection.Disconnected += test.PassHandler;
				e.Connection.DisconnectAsync();
			};
			
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectFromServerOnServer()
		{
			var test = new AsyncTest (2);

			var c = GetNewClientConnection();
			c.Disconnected += test.PassHandler;
			IServerConnection sc = null;

			var wait = new AutoResetEvent (false);

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) =>
			{
				e.Connection.Disconnected += test.PassHandler;
				sc = e.Connection;
				wait.Set();
			};
			
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000) || sc == null)
				Assert.Fail ("Failed to connect");

			sc.DisconnectAsync();

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void DisconnectAndReconnect()
		{
			var wait = new ManualResetEvent (false);

			var c = GetNewClientConnection();
			c.Connected += (sender, e) => wait.Set();
			c.Disconnected += (sender, e) => wait.Set();

			this.provider.Start (MessageTypes);

			for (int i = 0; i < 5; ++i)
			{
				Trace.WriteLine ("Connecting " + i);

				wait.Reset();
				c.ConnectAsync (Target, MessageTypes);
				if (!wait.WaitOne (10000))
					Assert.Fail ("Failed to connect. Attempt {0}.", i);

				Trace.WriteLine ("Connected & disconnecting " + i);

				wait.Reset();
				c.DisconnectAsync();
				if (!wait.WaitOne (10000))
					Assert.Fail ("Failed to disconnect. Attempt {0}.", i);

				Trace.WriteLine ("Disconnected " + i);
			}
		}

		[Test, Repeat (3)]
		public void DisconnectAndReconnectAsync()
		{
			AutoResetEvent wait = new AutoResetEvent (false);

			var c = GetNewClientConnection();
			c.Connected += (sender, e) => wait.Set();
			c.Disconnected += (sender, e) => wait.Set();

			this.provider.Start (MessageTypes);

			for (int i = 0; i < 5; ++i)
			{
				Trace.WriteLine ("Connecting " + i);

				c.ConnectAsync (Target, MessageTypes);
				if (!wait.WaitOne (10000))
					Assert.Fail ("Failed to connect. Attempt {0}.", i);

				Trace.WriteLine ("Connected & disconnecting " + i);

				c.DisconnectAsync();
				if (!wait.WaitOne (10000))
					Assert.Fail ("Failed to disconnect. Attempt {0}.", i);

				Trace.WriteLine ("Disconnected " + i);
			}
		}

		[Test, Repeat (3)]
		public void DisconnectAyncWithReason()
		{
			var test = new AsyncTest (e => ((DisconnectedEventArgs)e).Result == ConnectionResult.IncompatibleVersion, 2);

			this.provider.Start (MessageTypes);
			this.provider.ConnectionMade += (sender, e) =>
			{
				e.Connection.Disconnected += test.PassHandler;

				e.Connection.SendAsync (new DisconnectMessage { Reason = ConnectionResult.IncompatibleVersion });
				e.Connection.DisconnectAsync (ConnectionResult.IncompatibleVersion);
			};

			var c = GetNewClientConnection();
			c.Disconnected += test.PassHandler;
			c.ConnectAsync (Target, MessageTypes);

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void EncryptedMessage()
		{
			var cmessage = new EncryptedMessage
			{
				Message = "It's a secret!",
				Number = 42
			};

			var c = GetNewClientConnection();
			
			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);

				var msg = (me.Message as EncryptedMessage);
				Assert.IsNotNull (msg);
				Assert.IsTrue (msg.Encrypted);
				Assert.AreEqual (cmessage.Message, msg.Message);
				Assert.AreEqual (cmessage.Number, msg.Number);
			});

			IConnection sc;
			ManualResetEvent wait = new ManualResetEvent (false);
			this.provider.ConnectionMade += (s, e) =>
			{
				sc = e.Connection;
				sc.MessageReceived += test.PassHandler;
				sc.Disconnected += test.FailHandler;
				wait.Set();
			};

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) => c.SendAsync (cmessage);
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void EncryptedLongMessage()
		{
			string message = TestHelpers.GetLongString (random, MaxPayloadSize - sizeof (int));
			var cmessage = new EncryptedMessage
			{
				Message = message,
				Number = 42
			};

			var c = GetNewClientConnection();
			
			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);

				var msg = (me.Message as EncryptedMessage);
				Assert.IsNotNull (msg);
				Assert.IsTrue (msg.Encrypted);
				Assert.AreEqual (message, msg.Message);
				Assert.AreEqual (cmessage.Number, msg.Number);
			});

			IConnection sc;
			ManualResetEvent wait = new ManualResetEvent (false);
			this.provider.ConnectionMade += (s, e) =>
			{
				sc = e.Connection;
				sc.MessageReceived += test.PassHandler;
				sc.Disconnected += test.FailHandler;
				wait.Set();
			};

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) => c.SendAsync (cmessage);
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne(10000))
				Assert.Fail ("Failed to connect");

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void AuthenticatedLongMessage()
		{
			var message = TestHelpers.GetLongString (random, MaxPayloadSize - sizeof(int) * 2);
			var cmessage = new AuthenticatedMessage
			{
				Message = message,
				Number = 42
			};

			var c = GetNewClientConnection();
			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);

				var msg = (me.Message as AuthenticatedMessage);
				Assert.IsNotNull (msg);
				Assert.IsTrue (msg.Authenticated);
				Assert.AreEqual (cmessage.Message, msg.Message);
				Assert.AreEqual (cmessage.Number, msg.Number);
			});

			IConnection sc;
			ManualResetEvent wait = new ManualResetEvent (false);
			this.provider.ConnectionMade += (s, e) =>
			{
				sc = e.Connection;
				sc.MessageReceived += test.PassHandler;
				sc.Disconnected += test.FailHandler;
				wait.Set();
			};

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) => c.SendAsync (cmessage);
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void AuthenticatedMessage()
		{
			var cmessage = new AuthenticatedMessage
			{
				Message = "It's a secret!",
				Number = 42
			};

			var c = GetNewClientConnection();
			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);

				var msg = (me.Message as AuthenticatedMessage);
				Assert.IsNotNull (msg);
				Assert.IsTrue (msg.Authenticated);
				Assert.AreEqual (cmessage.Message, msg.Message);
				Assert.AreEqual (cmessage.Number, msg.Number);
			});

			IConnection sc;
			ManualResetEvent wait = new ManualResetEvent (false);
			this.provider.ConnectionMade += (s, e) =>
			{
				sc = e.Connection;
				sc.MessageReceived += test.PassHandler;
				sc.Disconnected += test.FailHandler;
				wait.Set();
			};

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) => c.SendAsync (cmessage);
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void BlankMessage()
		{
			var cmessage = new BlankMessage();

			var c = GetNewClientConnection();
			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);

				var msg = (me.Message as BlankMessage);
				Assert.IsNotNull (msg);
			});

			IConnection sc;
			ManualResetEvent wait = new ManualResetEvent (false);
			this.provider.ConnectionMade += (s, e) =>
			{
				sc = e.Connection;
				sc.MessageReceived += test.PassHandler;
				sc.Disconnected += test.FailHandler;
				wait.Set();
			};

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) => c.SendAsync (cmessage);
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void EncryptedAndAuthenticatedLongMessage()
		{
			string message = TestHelpers.GetLongString (random, MaxPayloadSize - sizeof (int) * 2);

			var cmessage = new AuthenticatedAndEncryptedMessage
			{
				Message = message,
				Number = 42
			};

			var c = GetNewClientConnection();
			
			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);

				var msg = (me.Message as AuthenticatedAndEncryptedMessage);
				Assert.IsNotNull (msg);
				Assert.IsTrue (msg.Encrypted);
				Assert.IsTrue (msg.Authenticated);
				Assert.AreEqual (cmessage.Message, msg.Message);
				Assert.AreEqual (cmessage.Number, msg.Number);
			});

			IConnection sc;
			ManualResetEvent wait = new ManualResetEvent (false);
			this.provider.ConnectionMade += (s, e) =>
			{
				sc = e.Connection;
				sc.MessageReceived += test.PassHandler;
				sc.Disconnected += test.FailHandler;
				wait.Set();
			};

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) => c.SendAsync (cmessage);
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void EncryptedAndAuthenticatedMessage()
		{
			var cmessage = new AuthenticatedAndEncryptedMessage()
			{
				Message = "It's a secret!",
				Number = 42
			};

			var c = GetNewClientConnection();
			
			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);

				var msg = (me.Message as AuthenticatedAndEncryptedMessage);
				Assert.IsNotNull (msg);
				Assert.IsTrue (msg.Encrypted);
				Assert.IsTrue (msg.Authenticated);
				Assert.AreEqual (cmessage.Message, msg.Message);
				Assert.AreEqual (cmessage.Number, msg.Number);
			});

			IConnection sc;
			ManualResetEvent wait = new ManualResetEvent (false);
			this.provider.ConnectionMade += (s, e) =>
			{
				sc = e.Connection;
				sc.MessageReceived += test.PassHandler;
				sc.Disconnected += test.FailHandler;
				wait.Set();
			};

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) => c.SendAsync (cmessage);
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			test.Assert (10000);
		}

		[Test, Repeat (3)]
		public void SendAndRespond()
		{
			var cmessage = new MockMessage();
			cmessage.Content = "Blah";

			var c = GetNewClientConnection();
			
			var test = new AsyncTest<MessageEventArgs<MockMessage>>  (e =>
			{
				Assert.IsNotNull (e.Message);
				Assert.AreEqual ("Response", e.Message.Content);
			});

			IConnection sc = null;
			ManualResetEvent wait = new ManualResetEvent (false);
			this.provider.ConnectionMade += (s, e) =>
			{
				sc = e.Connection;
				sc.MessageReceived += (ms, me) =>
				{
					var msg = (me.Message as MockMessage);
					me.Connection.SendResponseAsync (msg, new MockMessage { Content = "Response" });
				};
				sc.Disconnected += test.FailHandler;
				wait.Set();
			};

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) => {
				c.SendFor<MockMessage> (cmessage)
					.ContinueWith (t => test.PassHandler (sc, new MessageEventArgs<MockMessage> (sc, t.Result)));
			};
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			test.Assert (10000);
		}

		[Test]
		public async Task SendForRaisesCanceledNotAggregate()
		{
			var c = GetNewClientConnection();
			await c.ConnectAsync (Target, MessageTypes);
			Task send = c.SendFor<MockMessage> (new MockMessage());
			await c.DisconnectAsync();

			try {
				await send;
			} catch (OperationCanceledException) {
				Assert.Pass();
			} catch (Exception ex) {
				Assert.Fail ("Instead of a canceled exception, got a {0}", ex);
			}
		}

		private void AssertMessageReceived<T> (T message, Action<T> testResults)
			where T : Message
		{
			var c = GetNewClientConnection();

			var test = new AsyncTest (e =>
			{
				var me = (e as MessageEventArgs);
				Assert.IsNotNull (me);

				var msg = (me.Message as T);
				Assert.IsNotNull (msg);
				testResults (msg);
			});

			IConnection sc;
			ManualResetEvent wait = new ManualResetEvent (false);
			this.provider.ConnectionMade += (s, e) =>
			{
				sc = e.Connection;
				sc.MessageReceived += test.PassHandler;
				sc.Disconnected += test.FailHandler;
				wait.Set();
			};

			this.provider.Start (MessageTypes);

			c.Disconnected += test.FailHandler;
			c.Connected += (sender, e) => c.SendAsync (message);
			c.ConnectAsync (Target, MessageTypes);

			if (!wait.WaitOne (10000))
				Assert.Fail ("Failed to connect");

			test.Assert (10000);
		}
	}
}
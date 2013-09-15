using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tempest.Tests
{
	public class ConnectionBuffer
		: IConnection
	{
		private readonly IConnection connection;

		public ConnectionBuffer (IConnection connection)
		{
			this.connection = connection;
			this.connection.MessageReceived += OnMessageReceived;
		}

		public void Dispose ()
		{
			this.connection.Dispose();
		}

		public bool IsConnected
		{
			get { return this.connection.IsConnected; }
		}

		public int ConnectionId
		{
			get { return this.connection.ConnectionId; }
		}

		public IEnumerable<Protocol> Protocols
		{
			get { return this.connection.Protocols; }
		}

		public MessagingModes Modes
		{
			get { return this.connection.Modes; }
		}

		public Target RemoteTarget
		{
			get { return this.connection.RemoteTarget; }
		}

		public RSAAsymmetricKey RemoteKey
		{
			get { return this.connection.RemoteKey; }
		}

		public RSAAsymmetricKey LocalKey
		{
			get { return this.connection.LocalKey; }
		}

		public int ResponseTime
		{
			get { return this.connection.ResponseTime; }
		}

		public event EventHandler<MessageEventArgs> MessageReceived;
		public event EventHandler<MessageEventArgs> MessageSent;
		public event EventHandler<DisconnectedEventArgs> Disconnected;

		public Task<bool> SendAsync (Message message)
		{
			return this.connection.SendAsync (message);
		}

		public Task<Message> SendFor (Message message, int timeout = 0)
		{
			return this.connection.SendFor (message, timeout);
		}

		public Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0) where TResponse : Message
		{
			return this.connection.SendFor<TResponse> (message, timeout);
		}

		public Task<bool> SendResponseAsync (Message originalMessage, Message response)
		{
			return this.connection.SendResponseAsync (originalMessage, response);
		}

		public IEnumerable<MessageEventArgs> Tick ()
		{
			return this.connection.Tick();
		}

		public Task DisconnectAsync ()
		{
			return this.connection.DisconnectAsync();
		}

		public Task DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			return this.connection.DisconnectAsync (reason, customReason);
		}

		public T DequeueAndAssertMessage<T>()
			where T : Message
		{
			Message msg = DequeueMessage();

			if (!(msg is T))
				Assert.Fail ("Message was " + msg.GetType().Name + ", not expected " + typeof (T).Name);

			return (T)msg;
		}

		public Message DequeueMessage (bool wait = true)
		{
			ushort tick = 0;

			Message msg;
			while (!this.messages.TryDequeue (out msg) && tick++ < (UInt16.MaxValue - 1))
				Thread.Sleep (1);

			if (tick == UInt16.MaxValue)
				Assert.Fail ("Message never arrived");

			return msg;
		}

		public void AssertNoMessage()
		{
			Assert.IsTrue (this.messages.Count == 0);
		}

		private readonly ConcurrentQueue<Message> messages = new ConcurrentQueue<Message>();
		private void OnMessageReceived (object sender, MessageEventArgs e)
		{
			this.messages.Enqueue (e.Message);
		}
	}
}
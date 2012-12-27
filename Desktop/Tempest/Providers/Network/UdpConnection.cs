﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	public abstract class UdpConnection
		: IConnection
	{
		internal UdpConnection (IEnumerable<Protocol> protocols)
		{
			this.originalProtocols = protocols.ToArray();
			this.requiresHandshake = this.originalProtocols.Any (p => p.RequiresHandshake);
		}

		internal UdpConnection (IEnumerable<Protocol> protocols, IPublicKeyCrypto remoteCrypto, IPublicKeyCrypto localCrypto, IAsymmetricKey localKey)
			: this (protocols)
		{
			this.remoteCrypto = remoteCrypto;
			this.localCrypto = localCrypto;
			LocalKey = localKey;
		}

		public bool IsConnected
		{
			get { return this.formallyConnected; }
		}

		public int ConnectionId
		{
			get;
			protected set;
		}

		public IEnumerable<Protocol> Protocols
		{
			get { return this.serializer.Protocols; }
		}

		public MessagingModes Modes
		{
			get { return MessagingModes.Async; }
		}

		public EndPoint RemoteEndPoint
		{
			get;
			protected set;
		}

		public IAsymmetricKey RemoteKey
		{
			get;
			protected set;
		}

		public IAsymmetricKey LocalKey
		{
			get;
			protected set;
		}

		public int ResponseTime
		{
			get { throw new NotImplementedException(); }
		}

		public event EventHandler<MessageEventArgs> MessageReceived;
		public event EventHandler<MessageEventArgs> MessageSent;
		public event EventHandler<DisconnectedEventArgs> Disconnected;

		public void Send (Message message)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			Socket sock = this.socket;
			if (sock == null)
				return;

			int mid = Interlocked.Increment (ref this.nextMessageId);
			if (message.Header == null)
				message.Header = new MessageHeader();

			message.Header.MessageId = mid;

			int length;
			byte[] buffer = this.serializer.GetBytes (message, out length, new byte[2048]);

			SocketAsyncEventArgs args = new SocketAsyncEventArgs();
			args.SetBuffer (buffer, 0, length);
			args.RemoteEndPoint = RemoteEndPoint;
			args.Completed += OnSendCompleted;
			args.UserToken = message;

			try
			{
				if (!sock.SendToAsync (args))
					OnSendCompleted (this, args);
			}
			catch (ObjectDisposedException)
			{
			}
		}

		public Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0)
			where TResponse : Message
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			throw new NotImplementedException();
		}

		public void SendResponse (Message originalMessage, Message response)
		{
			if (originalMessage == null)
				throw new ArgumentNullException ("originalMessage");
			if (response == null)
				throw new ArgumentNullException ("response");

			throw new NotImplementedException();
		}

		public IEnumerable<MessageEventArgs> Tick()
		{
			throw new NotSupportedException();
		}

		public void Disconnect()
		{
			Disconnect (true, ConnectionResult.FailedUnknown);
		}

		public void Disconnect (ConnectionResult reason, string customReason = null)
		{
			Disconnect (true, ConnectionResult.FailedUnknown, customReason);
		}

		public void DisconnectAsync()
		{
			Disconnect (false, ConnectionResult.FailedUnknown);
		}

		public void DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			Disconnect (false, ConnectionResult.FailedUnknown, customReason);
		}

		public virtual void Dispose()
		{
			Cleanup();
		}

		protected bool formallyConnected;
		internal MessageSerializer serializer;
		protected readonly IPublicKeyCrypto localCrypto;

		protected readonly IPublicKeyCrypto remoteCrypto;

		protected Socket socket;

		protected int nextMessageId;

		protected readonly Dictionary<int, Tuple<DateTime, Message>> pendingAck = new Dictionary<int, Tuple<DateTime, Message>>();
		internal readonly ReliableQueue rqueue = new ReliableQueue();
		protected readonly Protocol[] originalProtocols;
		protected bool requiresHandshake;

		protected abstract bool IsConnecting
		{
			get;
		}

		protected virtual void Cleanup()
		{
			this.formallyConnected = false;

			this.serializer = null;

			this.rqueue.Clear();
			lock (this.pendingAck)
				this.pendingAck.Clear();
		}

		protected virtual void Disconnect (bool now, ConnectionResult reason, string customReason = null)
		{
			bool raise = IsConnected || IsConnecting;

			Cleanup();

			if (raise)
				OnDisconnected (new DisconnectedEventArgs (this, reason, customReason));
		}

		protected virtual void OnDisconnected (DisconnectedEventArgs e)
		{
			EventHandler<DisconnectedEventArgs> handler = this.Disconnected;
			if (handler != null)
				handler (this, e);
		}

		internal void ResendPending()
		{
			TimeSpan span = TimeSpan.FromSeconds (1);
			DateTime now = DateTime.UtcNow;

			List<Message> resending = new List<Message>();
			lock (this.pendingAck)
			{
				foreach (Tuple<DateTime, Message> pending in this.pendingAck.Values)
				{
					if (now - pending.Item1 > span)
						resending.Add (pending.Item2);
				}
			}

			foreach (Message message in resending)
				Send (message);
		}

		internal void Receive (Message message)
		{
			var args = new MessageEventArgs (this, message);

			if (args.Message.MustBeReliable || args.Message.PreferReliable)
			{
				bool acked = false;
				if (!(message is TempestMessage))
				{
					Send (new AcknowledgeMessage { MessageId = message.Header.MessageId });
					acked = true;
				}

				List<MessageEventArgs> messages = this.rqueue.Enqueue (args);
				if (messages != null)
				{
					foreach (MessageEventArgs messageEventArgs in messages)
						RouteMessage (messageEventArgs);
				}

				if (!acked)
					Send (new AcknowledgeMessage { MessageId = message.Header.MessageId });
			}
			else
				RouteMessage (args);
		}

		private void RouteMessage (MessageEventArgs args)
		{
			TempestMessage tempestMessage = args.Message as TempestMessage;
			if (tempestMessage != null)
				OnTempestMessage (args);
			else
			{
				OnMessageReceived (args);

				// TODO responses
			}
		}

		protected virtual void OnMessageReceived (MessageEventArgs e)
		{
			var received = MessageReceived;
			if (received != null)
				received (this, e);
		}

		protected virtual void OnTempestMessage (MessageEventArgs e)
		{
			switch (e.Message.MessageType)
			{
				case (ushort)TempestMessageType.Acknowledge:
					this.pendingAck.Remove (e.Message.Header.MessageId);
					break;

				case (ushort)TempestMessageType.Disconnect:
					var msg = (DisconnectMessage)e.Message;
					break;
			}
		}

		private void OnSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			if (e.UserToken is TempestMessage)
				return;

			var completed = MessageSent;
			if (completed != null)
				completed (this, new MessageEventArgs (this, (Message)e.UserToken));
		}
	}
}

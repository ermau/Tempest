//
// UdpConnection.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012 Eric Maupin
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
			var ps = protocols.ToList();
			this.requiresHandshake = ps.Any (p => p.id != 1 && p.RequiresHandshake);
			if (!ps.Contains (TempestMessage.InternalProtocol))
				ps.Add (TempestMessage.InternalProtocol);

			this.originalProtocols = ps;
		}

		internal UdpConnection (IEnumerable<Protocol> protocols, IPublicKeyCrypto remoteCrypto, IPublicKeyCrypto localCrypto, IAsymmetricKey localKey)
			: this (protocols)
		{
			this.remoteCrypto = remoteCrypto;
			this.localCrypto = localCrypto;
			this.localCrypto.ImportKey (localKey);
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
			SendCore (message);
		}

		public Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0)
			where TResponse : Message
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (!message.MustBeReliable && !message.PreferReliable)
				throw new NotSupportedException ("Sending unreliable messages for a response is not supported");

			var tcs = new TaskCompletionSource<Message>();
			SendCore (message, future: tcs);

			return tcs.Task.ContinueWith (t => (TResponse)t.Result);
		}

		public void SendResponse (Message originalMessage, Message response)
		{
			if (originalMessage == null)
				throw new ArgumentNullException ("originalMessage");
			if (response == null)
				throw new ArgumentNullException ("response");

			if (response.Header == null)
				response.Header = new MessageHeader();

			response.Header.IsResponse = true;
			response.Header.MessageId = originalMessage.Header.MessageId;

			SendCore (response, isResponse: true);
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
			Disconnect (true, reason, customReason);
		}

		public void DisconnectAsync()
		{
			Disconnect (true, ConnectionResult.FailedUnknown);
		}

		public void DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			Disconnect (true, reason, customReason);
		}

		public virtual void Dispose()
		{
			Disconnect (true, ConnectionResult.FailedUnknown);
		}

		protected bool formallyConnected;
		internal MessageSerializer serializer;
		protected IPublicKeyCrypto localCrypto;

		protected IPublicKeyCrypto remoteCrypto;

		protected Socket socket;

		protected int nextReliableMessageId;
		protected int nextMessageId;

		protected readonly Dictionary<int, Tuple<DateTime, Message>> pendingAck = new Dictionary<int, Tuple<DateTime, Message>>();
		internal readonly ReliableQueue rqueue = new ReliableQueue();
		protected readonly List<Protocol> originalProtocols;
		protected bool requiresHandshake;

		private readonly Dictionary<int, TaskCompletionSource<Message>> messageResponses = new Dictionary<int, TaskCompletionSource<Message>>();

		protected abstract bool IsConnecting
		{
			get;
		}

		protected void SendCore (Message message, bool isResponse = false, TaskCompletionSource<Message> future = null)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			Socket sock = this.socket;
			MessageSerializer mserialzier = this.serializer;

			if (sock == null || mserialzier == null)
			{
				if (future != null)
					future.TrySetCanceled();

				return;
			}

			if (message.Header == null)
				message.Header = new MessageHeader();

			if (!isResponse)
			{
				int mid;
				if (message.MustBeReliable || message.PreferReliable)
					mid = Interlocked.Increment (ref this.nextReliableMessageId);
				else
					mid = Interlocked.Increment (ref this.nextMessageId);

				message.Header.MessageId = mid;
			}

			if (future != null)
			{
				lock (this.messageResponses)
					this.messageResponses.Add (message.Header.MessageId, future);
			}

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

		protected virtual void Cleanup()
		{
			RemoteKey = null;

			ConnectionId = 0;
			this.formallyConnected = false;
			this.nextMessageId = 0;
			this.nextReliableMessageId = 0;

			this.serializer = null;

			this.rqueue.Clear();
			lock (this.pendingAck)
				this.pendingAck.Clear();
		}

		protected virtual void Disconnect (bool notify, ConnectionResult reason, string customReason = null)
		{
			bool raise = IsConnected || IsConnecting;

			if (raise && notify)
				Send (new DisconnectMessage { Reason = reason, CustomReason = customReason });

			Cleanup();

			if (raise)
				OnDisconnected (new DisconnectedEventArgs (this, reason, customReason));
		}

		protected virtual void OnDisconnected (DisconnectedEventArgs e)
		{
			EventHandler<DisconnectedEventArgs> handler = Disconnected;
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

			if (message.Header.MessageId != 0 && (args.Message.MustBeReliable || args.Message.PreferReliable))
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

				if (args.Message.Header.IsResponse)
				{
					TaskCompletionSource<Message> tcs;
					bool found;
					lock (this.messageResponses)
						found = this.messageResponses.TryGetValue (args.Message.Header.MessageId, out tcs);
					
					if (found)
						tcs.TrySetResult (args.Message);
				}
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
					Disconnect (false, msg.Reason, msg.CustomReason);
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

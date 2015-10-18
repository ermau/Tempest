//
// UdpConnection.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012-2014 Xamarin Inc.
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
using System.Diagnostics;
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
			this.requiresHandshake = ps.Any (p => p.Id != 1 && p.RequiresHandshake);
			if (!ps.Contains (TempestMessage.InternalProtocol))
				ps.Add (TempestMessage.InternalProtocol);

			this.originalProtocols = ps;

			BufferPool.AddConnection();
		}

		internal UdpConnection (IEnumerable<Protocol> protocols, RSACrypto remoteCrypto, RSACrypto localCrypto, RSAAsymmetricKey localKey)
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

		public Target RemoteTarget
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
			get { return -1; }
		}

		public event EventHandler<MessageEventArgs> MessageReceived;
		public event EventHandler<DisconnectedEventArgs> Disconnected;

		public Task<bool> SendAsync (Message message)
		{
			return SendCore (message);
		}

		public Task<Message> SendFor (Message message, int timeout = 0)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (!message.MustBeReliable && !message.PreferReliable)
				throw new NotSupportedException ("Sending unreliable messages for a response is not supported");

			Task<Message> responseTask;
			SendCore (message, false, true, timeout, out responseTask);
			return responseTask;
		}

		public Task<TResponse> SendFor<TResponse> (Message message, int timeout = 0)
			where TResponse : Message
		{
			return SendFor (message, timeout).ContinueWith (t => (TResponse)t.Result, TaskScheduler.Default);
		}

		public Task<bool> SendResponseAsync (Message originalMessage, Message response)
		{
			if (originalMessage == null)
				throw new ArgumentNullException ("originalMessage");
			if (response == null)
				throw new ArgumentNullException ("response");

			if (response.Header == null)
				response.Header = new MessageHeader();

			response.Header.IsResponse = true;
			response.Header.ResponseMessageId = originalMessage.Header.MessageId;

			return SendCore (response);
		}

		public Task DisconnectAsync()
		{
			return DisconnectAsync (ConnectionResult.FailedUnknown);
		}

		public Task DisconnectAsync (ConnectionResult reason, string customReason = null)
		{
			return Disconnect (reason, customReason);
		}

		public virtual void Dispose()
		{
			Disconnect (ConnectionResult.FailedUnknown);

			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Waiting for {0} pending asyncs", this.pendingAsync));

			while (this.pendingAsync > 0)
				Thread.Sleep (1);
		}

		protected int pendingAsync;

		protected bool formallyConnected;
		internal MessageSerializer serializer;
		protected RSACrypto localCrypto;

		protected RSACrypto remoteCrypto;

		protected IPEndPoint IPEndPoint;
		protected Socket socket;

		protected int nextReliableMessageId;
		protected int nextMessageId;

		protected readonly Dictionary<int, Tuple<DateTime, Message>> pendingAck = new Dictionary<int, Tuple<DateTime, Message>>();
		internal readonly ReliableQueue rqueue = new ReliableQueue();
		protected readonly List<Protocol> originalProtocols;
		protected bool requiresHandshake;
		internal readonly ConcurrentDictionary<ushort, ConcurrentQueue<PartialMessage>> partials = new ConcurrentDictionary<ushort, ConcurrentQueue<PartialMessage>>();

		internal long lastReceiveActivity, lastReliableSendActivity;

		static UdpConnection()
		{
			BufferPool = new BufferPool (512) {
				AutoSizeLimit = true,
				AutoSizeFactor = 2
			};
		}

		internal static BufferPool BufferPool
		{
			get;
			private set;
		}

		private readonly Lazy<MessageResponseManager> responses =
			new Lazy<MessageResponseManager> (() => new MessageResponseManager());

		protected abstract bool IsConnecting
		{
			get;
		}

		protected MessageResponseManager Responses
		{
			get { return this.responses.Value; }
		}

		private void SetMessageId (Message message)
		{
			if (message.MustBeReliable || message.PreferReliable)
				message.Header.MessageId = MessageSerializer.GetNextMessageId (ref this.nextReliableMessageId);
			else
				message.Header.MessageId = MessageSerializer.GetNextMessageId (ref this.nextMessageId);
		}

		protected Task<bool> SendCore (Message message, bool dontSetId = false)
		{
			Task<Message> messageTask;
			return SendCore (message, dontSetId, false, 0, out messageTask);
		}

		protected Task<bool> SendCore (Message message, bool dontSetId, bool responseRequested, int timeout, out Task<Message> responseTask)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			responseTask = null;

			Socket sock = this.socket;
			MessageSerializer mserialzier = this.serializer;

			TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool> (message);

			if (sock == null || mserialzier == null || (!IsConnected && !IsConnecting))
			{
				tcs.TrySetResult (false);

				if (responseRequested) {
					var responseTcs = new TaskCompletionSource<Message>();
					responseTcs.SetCanceled();
					responseTask = responseTcs.Task;
				}

				return tcs.Task;
			}

			if (message.Header == null)
				message.Header = new MessageHeader();

			if (!dontSetId) {
				SetMessageId (message);

				if (responseRequested)
					responseTask = Responses.SendFor (message, tcs.Task, timeout);
			}

			IPEndPoint endPoint = IPEndPoint;
			if (endPoint == null) {
				tcs.SetResult (false);
				return tcs.Task;
			}

			SocketAsyncEventArgs e;
			BufferPool.TryGetBuffer (out e);

			int length;
			byte[] buffer = mserialzier.GetBytes (message, out length, e.Buffer);
			if (!(message is PartialMessage) && length > 490)
			{
				byte count = (byte)Math.Ceiling ((length / 490f));

				int i = 0;
				
				int remaining = length;
				do
				{
					int payloadLen = Math.Min (490, remaining);

					var partial = new PartialMessage
					{
						OriginalMessageId = (ushort)message.Header.MessageId,
						Count = count,
						Header = new MessageHeader()
					};

					partial.SetPayload (buffer, i, payloadLen);
					if (i == 0) // We have to fill the gap the original id uses for reliability
						partial.Header.MessageId = message.Header.MessageId;
					else
						SetMessageId (partial);

					lock (this.pendingAck)
						this.pendingAck.Add (partial.Header.MessageId, new Tuple<DateTime, Message> (DateTime.UtcNow, partial));

					mserialzier.GetBytes (partial, out length, e.Buffer);

					e.SetBuffer (0, length);
					e.RemoteEndPoint = endPoint;

					remaining -= payloadLen;
					i += payloadLen;

					if (remaining == 0) {
						e.Completed += OnSendCompleted;
						e.UserToken = tcs;
					} else
						e.Completed += OnPartialSendCompleted;

					try {
						this.lastReliableSendActivity = Stopwatch.GetTimestamp();
						if (!sock.SendToAsync (e)) {
							if (remaining == 0)
								OnSendCompleted (this, e);
							else
								OnPartialSendCompleted (this, e);
						}
					} catch (ObjectDisposedException) {
						BufferPool.PushBuffer (e);

						if (remaining == 0)
							CleanupSend (e);
						else
							CleanupPartialSend (e);

						tcs.TrySetResult (false);
					}

					if (remaining > 0)
						BufferPool.TryGetBuffer (out e);
				} while (remaining > 0);
			}
			else
			{
				e.SetBuffer (0, length);
				e.RemoteEndPoint = endPoint;
				e.Completed += OnSendCompleted;
				e.UserToken = tcs;

				if (message.PreferReliable || message.MustBeReliable) {
					this.lastReliableSendActivity = Stopwatch.GetTimestamp();

					lock (this.pendingAck)
						this.pendingAck.Add (message.Header.MessageId, new Tuple<DateTime, Message> (DateTime.UtcNow, message));
				}

				try {
					if (!sock.SendToAsync (e))
						OnSendCompleted (this, e);
				} catch (ObjectDisposedException) {
					CleanupSend (e);
					tcs.TrySetResult (false);
				}
			}

			return tcs.Task;
		}

		protected virtual void Cleanup()
		{
			IPEndPoint = null;
			RemoteKey = null;

			ConnectionId = 0;
			this.formallyConnected = false;
			this.nextMessageId = 0;
			this.nextReliableMessageId = 0;
			this.lastReceiveActivity = 0;

			this.serializer = null;

			this.rqueue.Clear();
			this.partials.Clear();

			if (this.responses.IsValueCreated)
				this.responses.Value.Clear();

			lock (this.pendingAck)
				this.pendingAck.Clear();

			BufferPool.RemoveConnection();
		}

		protected virtual Task Disconnect (ConnectionResult reason, string customReason = null)
		{
			bool raise = IsConnected || IsConnecting;

			var tcs = new TaskCompletionSource<bool>();

			if (raise)
			{
				SendAsync (new DisconnectMessage { Reason = reason, CustomReason = customReason })
					.Wait();
			}

			Cleanup();

			if (raise)
				OnDisconnected (new DisconnectedEventArgs (this, reason, customReason));

			tcs.SetResult (true);
			return tcs.Task;
		}

		protected virtual void OnDisconnected (DisconnectedEventArgs e)
		{
			EventHandler<DisconnectedEventArgs> handler = Disconnected;
			if (handler != null)
				handler (this, e);
		}

		internal void CheckPendingTimeouts()
		{
			if (this.responses.IsValueCreated)
				this.responses.Value.CheckTimeouts();
		}

		internal void ResendPending()
		{
			TimeSpan span = TimeSpan.FromMilliseconds (600);
			DateTime now = DateTime.UtcNow;

			lock (this.pendingAck) {
				foreach (Tuple<DateTime, Message> pending in this.pendingAck.Values.Where (pending => now - pending.Item1 > span).ToArray()) {
					this.pendingAck.Remove (pending.Item2.Header.MessageId);
					SendCore (pending.Item2, dontSetId: true);
				}
			}
		}

		internal void Receive (Message message, bool fromPartials = false)
		{
			this.lastReceiveActivity = Stopwatch.GetTimestamp();
			var args = new MessageEventArgs (this, message);

			if (!fromPartials && message.Header.MessageId != 0 && (args.Message.MustBeReliable || args.Message.PreferReliable)) {
				// We generally always need to ACK. partials have their pieces acked individually.
				var ack = new AcknowledgeMessage { MessageIds = new[] { message.Header.MessageId } };
				bool isConnect = (message is ConnectMessage);
				if (!isConnect)
					SendAsync (ack);

				List<MessageEventArgs> messages;
				if (this.rqueue.TryEnqueue (args, out messages)) {
					if (messages != null) {
						foreach (MessageEventArgs messageEventArgs in messages)
							RouteMessage (messageEventArgs);
					}
				}

				// We ACK for Connect after it's routed so it can setup the serializers
				if (isConnect)
					SendAsync (ack);
			} else
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
					this.responses.Value.Receive (args.Message);
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
				case (ushort)TempestMessageType.Partial:
					ReceivePartialMessage ((PartialMessage)e.Message);
					break;

				case (ushort)TempestMessageType.Acknowledge:
					lock (this.pendingAck)
					{
						int[] msgIds = ((AcknowledgeMessage)e.Message).MessageIds;
						foreach (int id in msgIds)
							this.pendingAck.Remove (id);
					}
					break;

				case (ushort)TempestMessageType.Disconnect:
					var msg = (DisconnectMessage)e.Message;
					Disconnect (msg.Reason, msg.CustomReason);
					break;
			}
		}

		internal void ReceivePartialMessage (PartialMessage message)
		{
			var queue = this.partials.GetOrAdd (message.OriginalMessageId, id => new ConcurrentQueue<PartialMessage>());
			queue.Enqueue (message);

			if (queue.Count != message.Count)
				return;

			if (!this.partials.TryRemove (message.OriginalMessageId, out queue))
				return;

			byte[] payload = new byte[queue.Sum (p => p.Payload.Length)];

			int offset = 0;
			foreach (PartialMessage msg in queue.OrderBy (p => p.Header.MessageId))
			{
				byte[] partialPayload = msg.Payload;
				Buffer.BlockCopy (partialPayload, 0, payload, offset, partialPayload.Length);
				offset += partialPayload.Length;
			}

			MessageSerializer srlzr = this.serializer;
			if (srlzr == null)
				return;

			List<Message> messages = srlzr.BufferMessages (payload);
			if (messages != null && messages.Count == 1)
				Receive (messages[0], fromPartials: true);
			else
				DisconnectAsync();
		}

		private void CleanupPartialSend (SocketAsyncEventArgs e)
		{
			e.Completed -= OnPartialSendCompleted;
			BufferPool.PushBuffer (e);
		}

		private void OnPartialSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			CleanupPartialSend (e);
		}

		private void CleanupSend (SocketAsyncEventArgs e)
		{
			e.Completed -= OnSendCompleted;
			BufferPool.PushBuffer (e);
		}

		private void OnSendCompleted (object sender, SocketAsyncEventArgs e)
		{
			CleanupSend (e);

			var tcs = e.UserToken as TaskCompletionSource<bool>;
			if (tcs != null)
				tcs.TrySetResult (true);
		}

		internal static readonly TraceSwitch NTrace = new TraceSwitch ("Tempest.Networking", "UdpConnectionProvider");
	}
}

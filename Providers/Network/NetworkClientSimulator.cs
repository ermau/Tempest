//
// NetworkClientSimulator.cs
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
using System.Net;

namespace Tempest.Providers.Network
{
	class NetworkClientSimulator
		: IClientConnection
	{
		private readonly NetworkClientConnection connection;

		public NetworkClientSimulator (NetworkClientConnection connection)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");

			this.connection = connection;
		}

		public MessageTypes SendPacketLossTypes
		{
			get; set;
		}

		/// <summary>
		/// Gets or sets simulated packet loss sending. 0.0 to 1.0.
		/// </summary>
		public double SendPacketLoss
		{
			get { return this.sendPacketLoss; }
			set
			{
				if (value < 0 || value > 1)
					throw new ArgumentOutOfRangeException ("value", "value must be equal to or between 0.0 and 1.0");

				this.sendPacketLoss = value;
			}
		}

		public void Dispose()
		{
			this.connection.Dispose();
		}

		public bool IsConnected
		{
			get { return this.connection.IsConnected; }
		}

		public MessagingModes Modes
		{
			get { return this.connection.Modes; }
		}

		public EndPoint RemoteEndPoint
		{
			get { return this.connection.RemoteEndPoint; }
		}

		public event EventHandler<MessageEventArgs> MessageReceived
		{
			add { this.connection.MessageReceived += value; }
			remove { this.connection.MessageReceived -= value; }
		}

		public event EventHandler<MessageEventArgs> MessageSent
		{
			add { this.connection.MessageSent += value; }
			remove { this.connection.MessageSent -= value; }
		}

		public event EventHandler<DisconnectedEventArgs> Disconnected
		{
			add { this.connection.Disconnected += value; }
			remove { this.connection.Disconnected -= value;}
		}


		public event EventHandler<ClientConnectionEventArgs> Connected
		{
			add { this.connection.Connected += value; }
			remove { this.connection.Connected -= value; }
		}

		public void Send (Message message)
		{
			if (this.sendPacketLoss > 0 && ((message.Reliable && (SendPacketLossTypes & MessageTypes.Reliable) == MessageTypes.Reliable)
											|| (!message.Reliable && (SendPacketLossTypes & MessageTypes.Unreliable) == MessageTypes.Unreliable)))
			{
				double v;
				lock (random)
					v = random.NextDouble();

				if (v < this.sendPacketLoss)
					return;
			}

			this.connection.Send (message);
		}

		public IEnumerable<MessageEventArgs> Tick()
		{
			return this.connection.Tick();
		}

		public void Disconnect (bool now, DisconnectedReason reason = DisconnectedReason.Unknown)
		{
			this.connection.Disconnect (now, reason);
		}

		public void Connect (EndPoint endpoint, MessageTypes messageTypes)
		{
			this.connection.Connect (endpoint, messageTypes);
		}

		private double sendPacketLoss;
		private readonly Random random = new Random();
	}
}
//
// TempestMessage.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011-2012 Eric Maupin
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

namespace Tempest.InternalProtocol
{
	/// <summary>
	/// Internal Tempest protocol message type.
	/// </summary>
	public enum TempestMessageType
		: ushort
	{
		/// <summary>
		/// Ping over reliable connections.
		/// </summary>
		ReliablePing = 1,

		/// <summary>
		/// Pong over reliable connections.
		/// </summary>
		ReliablePong = 2,

		/// <summary>
		/// Disconnect with reason.
		/// </summary>
		Disconnect = 3,

		/// <summary>
		/// ClientHello
		/// </summary>
		Connect = 4,

		/// <summary>
		/// ServerConnected
		/// </summary>
		Connected = 5,

		/// <summary>
		/// ServerHello
		/// </summary>
		AcknowledgeConnect = 6,

		/// <summary>
		/// Client finalize connection
		/// </summary>
		FinalConnect = 7,
		
		/// <summary>
		/// Ping over unreliable connections.
		/// </summary>
		UnreliablePing = 8,

		/// <summary>
		/// Pong over unreliable connections.
		/// </summary>
		UnreliablePong = 9,

		/// <summary>
		/// Acknowledge message received.
		/// </summary>
		Acknowledge = 10,

		/// <summary>
		/// Partial message received.
		/// </summary>
		Partial = 11
	}

	/// <summary>
	/// Base class for all internal Tempest protocol messages.
	/// </summary>
	public abstract class TempestMessage
		: Message
	{
		protected TempestMessage (TempestMessageType type)
			: base (InternalProtocol, (ushort)type)
		{
		}

		internal static readonly Protocol InternalProtocol = new Protocol (0) { id = 1 }; // Error check bypass hack
		static TempestMessage()
		{
			InternalProtocol.Register (new []
			{
				new KeyValuePair<Type, Func<Message>> (typeof(ReliablePingMessage), () => new ReliablePingMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(ReliablePongMessage), () => new ReliablePongMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(UnreliablePingMessage), () => new UnreliablePingMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(UnreliablePongMessage), () => new UnreliablePongMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(DisconnectMessage), () => new DisconnectMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(ConnectMessage), () => new ConnectMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(AcknowledgeConnectMessage), () => new AcknowledgeConnectMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(FinalConnectMessage), () => new FinalConnectMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(ConnectedMessage), () => new ConnectedMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(AcknowledgeMessage), () => new AcknowledgeMessage()), 
			});
		}
	}
}
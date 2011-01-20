//
// TempestMessage.cs
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

namespace Tempest.InternalProtocol
{
	/// <summary>
	/// Internal Tempest protocol message type.
	/// </summary>
	public enum TempestMessageType
		: ushort
	{
		/// <summary>
		/// Ping.
		/// </summary>
		Ping = 1,

		/// <summary>
		/// Pong.
		/// </summary>
		Pong = 2,

		/// <summary>
		/// Disconnect with reason.
		/// </summary>
		Disconnect = 3,
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

		internal static readonly Protocol InternalProtocol = Protocols.Register (new Protocol (0) { id = 1 });
		static TempestMessage()
		{
			InternalProtocol.Register (new []
			{
				new KeyValuePair<Type, Func<Message>> (typeof(PingMessage), () => new PingMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(PongMessage), () => new PongMessage()), 
				new KeyValuePair<Type, Func<Message>> (typeof(DisconnectMessage), () => new DisconnectMessage()), 
			});
		}
	}
}
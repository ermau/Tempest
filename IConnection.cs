// The MIT License
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
using System.Linq;
using System.Net;

namespace Tempest
{
	[Flags]
	public enum MessagingModes
	{
		/// <summary>
		/// Event based messaging.
		/// </summary>
		Async = 1,

		/// <summary>
		/// Poll based messaging.
		/// </summary>
		Inline = 2,

		/// <summary>
		/// Both.
		/// </summary>
		Both = Async | Inline
	}

	/// <summary>
	/// Base connection contract.
	/// </summary>
	public interface IConnection
	{
		/// <summary>
		/// Gets whether the connection is alive or not.
		/// </summary>
		bool IsConnected { get; }

		/// <summary>
		/// The supported modes for the connection.
		/// </summary>
		MessagingModes Modes { get; }

		/// <summary>
		/// Gets the remote endpoint for this connection.
		/// </summary>
		EndPoint RemoteEndPoint { get; }

		/// <summary>
		/// A message was received on the transport.
		/// </summary>
		/// <exception cref="NotSupportedException"><see cref="Modes"/> is not <see cref="MessagingModes.Async"/>.</exception>
		event EventHandler<MessageReceivedEventArgs> MessageReceived;

		/// <summary>
		/// The connection was lost.
		/// </summary>
		event EventHandler<ConnectionEventArgs> Disconnected;

		/// <summary>
		/// Queues a message to send to this connection.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
		void Send (Message message);

		/// <summary>
		/// Sends and receives all pending messages.
		/// </summary>
		/// <returns>An enumerable of pending messages, <c>Enumerable.Empty&lt;Message&gt;()</c> if none.</returns>
		/// <exception cref="NotSupportedException"><see cref="Modes"/> is not <see cref="MessagingModes.Inline"/>.</exception>
		IEnumerable<Message> Tick();

		/// <summary>
		/// Closes the connection.
		/// </summary>
		void Disconnect();
	}

	/// <summary>
	/// Holds event base data for various connection-based events.
	/// </summary>
	public class ConnectionEventArgs
		: EventArgs
	{
		/// <summary>
		/// Creates a new instance of <see cref="ConnectionEventArgs"/>.
		/// </summary>
		/// <param name="connection">The connection of the event.</param>
		/// <exception cref="ArgumentNullException"><paramref name="connection"/> is <c>null</c>.</exception>
		public ConnectionEventArgs (IConnection connection)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");

			Connection = connection;
		}

		/// <summary>
		/// Gets the connection for the event.
		/// </summary>
		public IConnection Connection
		{
			get;
			private set;
		}
	}

	/// <summary>
	/// Holds event data for the <see cref="IConnection.MessageReceived"/> event.
	/// </summary>
	public class MessageReceivedEventArgs
		: ConnectionEventArgs
	{
		/// <summary>
		/// Creates a new instance of <see cref="MessageReceivedEventArgs"/>.
		/// </summary>
		/// <param name="connection">The connection of the event.</param>
		/// <param name="message">The message received.</param>
		/// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="message"/> is <c>null</c>.</exception>
		public MessageReceivedEventArgs(IConnection connection, Message message)
			: base (connection)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			Message = message;
		}

		/// <summary>
		/// Gets the message received.
		/// </summary>
		public Message Message
		{
			get;
			private set;
		}
	}
}
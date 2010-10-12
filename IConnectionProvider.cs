//
// IConnectionProvider.cs
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
using System.Linq;
using System.Net;

namespace Tempest
{
	[Flags]
	public enum MessageTypes
	{
		/// <summary>
		/// Reliable in-order delivery.
		/// </summary>
		Reliable = 1,

		/// <summary>
		/// Unreliable no-order-guarantee delivery.
		/// </summary>
		Unreliable = 2,
		
		/// <summary>
		/// All message delivery types.
		/// </summary>
		All = Reliable | Unreliable
	}

	public interface IConnectionProvider
		: IDisposable
	{
		/// <summary>
		/// A new connection was made.
		/// </summary>
		event EventHandler<ConnectionMadeEventArgs> ConnectionMade;

		/// <summary>
		/// A connectionless message was received.
		/// </summary>
		/// <exception cref="NotSupportedException"><see cref="SupportsConnectionless"/> is <c>false</c>.</exception>
		event EventHandler<ConnectionlessMessageReceivedEventArgs> ConnectionlessMessageReceived;

		/// <summary>
		/// Gets whether this connection provider supports connectionless messages.
		/// </summary>
		/// <seealso cref="ConnectionlessMessageReceived"/>
		/// <seealso cref="SendConnectionlessMessage"/>
		bool SupportsConnectionless { get; }

		/// <summary>
		/// Starts the connection provider.
		/// </summary>
		/// <param name="types">The message types to accept.</param>
		/// <seealso cref="Stop"/>
		void Start (MessageTypes types);

		/// <summary>
		/// Sends a connectionless <paramref name="message"/> to <paramref name="endPoint"/>.
		/// </summary>
		/// <param name="message"></param>
		/// <param name="endPoint"></param>
		/// <exception cref="NotSupportedException"><see cref="SupportsConnectionless"/> is <c>false</c>.</exception>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> or <paramref name="endPoint"/> is <c>null</c>.</exception>
		/// <seealso cref="SupportsConnectionless"/>
		void SendConnectionlessMessage (Message message, EndPoint endPoint);

		/// <summary>
		/// Stops the connection provider.
		/// </summary>
		/// <seealso cref="Start"/>
		void Stop();
	}

	/// <summary>
	/// Provides data for the <see cref="IConnectionProvider.ConnectionMade"/> event.
	/// </summary>
	public class ConnectionMadeEventArgs
		: EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectionMadeEventArgs"/> class.
		/// </summary>
		/// <param name="connection">The newly made connection.</param>
		/// <exception cref="ArgumentNullException"><paramref name="connection"/> is <c>null</c>.</exception>
		public ConnectionMadeEventArgs (IServerConnection connection)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");

			Connection = connection;
		}

		/// <summary>
		/// Gets the newly formed connection.
		/// </summary>
		public IServerConnection Connection
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets or sets whether to reject this connection.
		/// </summary>
		public bool Rejected
		{
			get;
			set;
		}
	}

	/// <summary>
	/// Provides data for the <see cref="IConnectionProvider.ConnectionlessMessageReceived"/> event.
	/// </summary>
	public class ConnectionlessMessageReceivedEventArgs
		: EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectionlessMessageReceivedEventArgs"/> class.
		/// </summary>
		/// <param name="message">The message received connectionlessly.</param>
		/// <param name="from">Where the message came from.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> or <paramref name="from"/> is <c>null</c>.</exception>
		public ConnectionlessMessageReceivedEventArgs (Message message, EndPoint from)
		{
			if (message == null)
				throw new ArgumentNullException ("message");
			if (from == null)
				throw new ArgumentNullException ("from");
			
			Message = message;
			From = from;
		}

		/// <summary>
		/// Gets the received message.
		/// </summary>
		public Message Message
		{
			get;
			private set;
		}

		/// <summary>
		/// Where the message came from.
		/// </summary>
		public EndPoint From
		{
			get;
			private set;
		}
	}
}
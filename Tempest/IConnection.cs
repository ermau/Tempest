//
// IConnection.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010-2011 Eric Maupin
// Copyright (c) 2012-2015 Xamarin Inc.
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
using System.Threading.Tasks;

namespace Tempest
{
	/// <summary>
	/// Base connection contract.
	/// </summary>
	public interface IConnection
		: IDisposable
	{
		/// <summary>
		/// Gets whether the connection is alive or not.
		/// </summary>
		bool IsConnected { get; }

		/// <summary>
		/// Gets the server-assigned connection id.
		/// </summary>
		int ConnectionId { get; }

		/// <summary>
		/// Gets the protocols the connection has enabled/negotiated.
		/// </summary>
		IEnumerable<Protocol> Protocols { get; }

		/// <summary>
		/// Gets the remote target for this connection.
		/// </summary>
		Target RemoteTarget { get; }

		/// <summary>
		/// Gets the remote authentication key for this connection.
		/// </summary>
		/// <remarks><c>null</c> if the transport did not handshake.</remarks>
		IAsymmetricKey RemoteKey { get; }

		/// <summary>
		/// Gets the local authentication key for this connection.
		/// </summary>
		/// <remarks><c>null</c> if the transport did not handshake.</remarks>
		IAsymmetricKey LocalKey { get; }

		/// <summary>
		/// Gets the response time in milliseconds for the connection. -1 if unsupported.
		/// </summary>
		int ResponseTime { get; }

		/// <summary>
		/// A message was received on the transport.
		/// </summary>
		/// <exception cref="NotSupportedException"><see cref="Modes"/> is not <see cref="MessagingModes.Async"/>.</exception>
		event EventHandler<MessageEventArgs> MessageReceived;

		/// <summary>
		/// The connection was lost.
		/// </summary>
		event EventHandler<DisconnectedEventArgs> Disconnected;

		/// <summary>
		/// Queues a message to send to this connection.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
		/// <returns>Whether or not the message was sent.</returns>
		/// <remarks>
		/// The returned future will complete when the underlying connection has completed sending the message,
		/// not when the other end has received it. In some cases, if you're sending while disconnecting, the
		/// message may fail to send in which case the future's result will be <c>false</c>.
		/// </remarks>
		Task<bool> SendAsync (Message message);

		/// <summary>
		/// Sends a <paramref name="message"/> on this connection and returns a <see cref="Task{TResponse}" /> for the direct response to this <paramref name="message"/>.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// <param name="responseTimeout">A timeout for the resposne in milliseconds.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
		/// <seealso cref="SendResponseAsync"/>
		/// <remarks>
		/// In some cases, such as sending a message while disconnecting, the message may fail to send
		/// in which case the future's result will be <c>null</c>.
		/// </remarks>
		Task<Message> SendFor (Message message, int responseTimeout = 0);

		/// <summary>
		/// Sends a <paramref name="message"/> on this connection and returns a <see cref="Task{TResponse}" /> for the direct response to this <paramref name="message"/>.
		/// </summary>
		/// <typeparam name="TResponse">The type of message being expected in response.</typeparam>
		/// <param name="message">The message to send.</param>
		/// <param name="responseTimeout">A timeout for the response in milliseconds.</param>
		/// <returns>A <see cref="Task{TResponse}" /> for the direct response to <paramref name="message"/>.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
		/// <seealso cref="SendResponseAsync"/>
		/// <remarks>
		/// In some cases, such as sending a message while disconnecting, the message may fail to send
		/// in which case the future's result will be <c>null</c>.
		/// </remarks>
		Task<TResponse> SendFor<TResponse> (Message message, int responseTimeout = 0) where TResponse : Message;

		/// <summary>
		/// Sends a <paramref name="response"/> to <paramref name="originalMessage"/>.
		/// </summary>
		/// <param name="originalMessage"></param>
		/// <param name="response"></param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="originalMessage"/> is <c>null</c>.</para>
		/// <para>-- or --</para>
		/// <para><paramref name="response"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="ArgumentException"><paramref name="originalMessage" />'s <see cref="Message.IsResponse"/> is <c>true</c>.</exception>
		/// <seealso cref="SendFor{TResponse}"/>
		/// <remarks>
		/// The returned future will complete when the underlying connection has completed sending the message,
		/// not when the other end has received it. In some cases, if you're sending while disconnecting, the
		/// message may fail to send in which case the future's result will be <c>false</c>.
		/// </remarks>
		Task<bool> SendResponseAsync (Message originalMessage, Message response);

		/// <summary>
		/// Asynchronously closes the connection.
		/// </summary>
		Task DisconnectAsync();

		/// <summary>
		/// Asynchronously closes the connection.
		/// </summary>
		/// <param name="reason">Reason for the disconnection.</param>
		/// <param name="customReason">A custom reason, if any.</param>
		/// <exception cref="ArgumentNullException">
		/// If <paramref name="reason"/> == <see cref="ConnectionResult.Custom"/> and <paramref name="customReason"/> is <c>null</c>.
		/// </exception>
		Task DisconnectAsync (ConnectionResult reason, string customReason = null);
	}

	/// <summary>
	/// Reasons for disconnection.
	/// </summary>
	public enum ConnectionResult
		: byte
	{
		/// <summary>
		/// Connection lost or killed for an unknown reason.
		/// </summary>
		FailedUnknown = 0,

		/// <summary>
		/// Connection succeeded.
		/// </summary>
		Success = 1,

		/// <summary>
		/// The connection failed to connect to begin with.
		/// </summary>
		ConnectionFailed = 2,

		/// <summary>
		/// The server does not support the client's version of the protocol.
		/// </summary>
		IncompatibleVersion = 3,
		
		/// <summary>
		/// The client failed during the handshake.
		/// </summary>
		FailedHandshake = 4,

		/// <summary>
		/// A signed message failed verification.
		/// </summary>
		MessageAuthenticationFailed = 5,

		/// <summary>
		/// An encrypted message failed decryption.
		/// </summary>
		EncryptionMismatch = 6,

		/// <summary>
		/// An application specified result.
		/// </summary>
		Custom = 7,

		/// <summary>
		/// The connection timed out.
		/// </summary>
		TimedOut = 8,
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
	[System.Diagnostics.DebuggerDisplay ("{Message}")]
	public class MessageEventArgs
		: ConnectionEventArgs
	{
		/// <summary>
		/// Creates a new instance of <see cref="MessageEventArgs"/>.
		/// </summary>
		/// <param name="connection">The connection of the event.</param>
		/// <param name="message">The message received.</param>
		/// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="message"/> is <c>null</c>.</exception>
		public MessageEventArgs (IConnection connection, Message message)
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
//
// DisconnectedEventArgs.cs
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

namespace Tempest
{
	/// <summary>
	/// Reasons for disconnection.
	/// </summary>
	public enum DisconnectedReason
	{
		/// <summary>
		/// Connection lost or killed for an unknown reason.
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// The connection failed to connect to begin with.
		/// </summary>
		ConnectionFailed = 1,

		/// <summary>
		/// The server does not support the client's version of the protocol.
		/// </summary>
		IncompatibleVersion = 2,
		
		/// <summary>
		/// The client failed during the handshake.
		/// </summary>
		FailedHandshake = 3,

		/// <summary>
		/// A signed message failed verification.
		/// </summary>
		MessageAuthenticationFailed = 4,

		/// <summary>
		/// An encrypted message failed decryption.
		/// </summary>
		EncryptionMismatch = 5,

		/// <summary>
		/// An application specified reason.
		/// </summary>
		Custom = 6,
	}

	/// <summary>
	/// Holds event data for the <see cref="IConnection.Disconnected"/> event.
	/// </summary>
	public class DisconnectedEventArgs
		: ConnectionEventArgs
	{
		/// <summary>
		/// Creates a new instance of <see cref="ConnectionEventArgs"/>.
		/// </summary>
		/// <param name="connection">The connection of the event.</param>
		/// <param name="reason">Reason for disconnection.</param>
		/// <exception cref="ArgumentNullException"><paramref name="connection"/> is <c>null</c>.</exception>
		public DisconnectedEventArgs (IConnection connection, DisconnectedReason reason)
			: base (connection)
		{
			Reason = reason;
		}

		public DisconnectedEventArgs (IConnection connection, DisconnectedReason reason, string customReason)
			: this (connection, reason)
		{
			CustomReason = customReason;
		}

		public DisconnectedReason Reason
		{
			get;
			private set;
		}

		public string CustomReason
		{
			get;
			private set;
		}
	}
}
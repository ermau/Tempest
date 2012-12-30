//
// IClientConnection.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010-2012 Eric Maupin
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
using System.Net;
using System.Threading.Tasks;

namespace Tempest
{
	public interface IClientConnection
		: IConnection
	{
		/// <summary>
		/// Raised when the connection has connected.
		/// </summary>
		event EventHandler<ClientConnectionEventArgs> Connected;

		/// <summary>
		/// Attempts to asynchronously connect to the <paramref name="endPoint"/> for <paramref name="messageTypes"/>.
		/// </summary>
		/// <param name="endPoint">The endpoint to connect to.</param>
		/// <param name="messageTypes">The type of messages to connect for.</param>
		/// <exception cref="ArgumentNullException"><paramref name="endPoint"/> is <c>null</c>.</exception>
		Task<ClientConnectionResult> ConnectAsync (EndPoint endPoint, MessageTypes messageTypes);
	}

	/// <summary>
	/// Holds data for <see cref="IClientConnection.ConnectAsync"/> results.
	/// </summary>
	public class ClientConnectionResult
	{
		/// <summary>
		/// Constructs and initializes a new instance of the <see cref="ClientConnectionResult"/> class.
		/// </summary>
		/// <param name="result">The result of the connection attempt.</param>
		/// <param name="publicKey">The server's public authentication key, if it has one.</param>
		public ClientConnectionResult (ConnectionResult result, IAsymmetricKey publicKey)
		{
			if (!Enum.IsDefined (typeof(ConnectionResult), result))
				throw new ArgumentException ("result is not a valid member of ConnectionResult", "result");

			Result = result;
			ServerPublicKey = publicKey;
		}

		/// <summary>
		/// Gets the connection result.
		/// </summary>
		public ConnectionResult Result
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the server's public authentication key, if encryption or authentication enabled.
		/// </summary>
		public IAsymmetricKey ServerPublicKey
		{
			get;
			private set;
		}
	}

	/// <summary>
	/// Holds data for the <see cref="IClientConnection.Connected"/> event.
	/// </summary>
	public class ClientConnectedEventArgs
		: ClientConnectionEventArgs
	{
		/// <summary>
		/// Creates a new instance of the <see cref="ClientConnectedEventArgs"/> class.
		/// </summary>
		/// <param name="connection">The connection for the event.</param>
		/// <param name="publicKey">The server's public authentication key, if it has one.</param>
		public ClientConnectedEventArgs (IClientConnection connection, IAsymmetricKey publicKey)
			: base (connection)
		{
			ServerPublicKey = publicKey;
		}

		/// <summary>
		/// Gets the server's public authentication key, if encryption or authentication enabled.
		/// </summary>
		public IAsymmetricKey ServerPublicKey
		{
			get;
			private set;
		}
	}

	/// <summary>
	/// Holds data for client-connection based events.
	/// </summary>
	public class ClientConnectionEventArgs
		: EventArgs
	{
		/// <summary>
		/// Creates a new instance of the <see cref="ClientConnectionEventArgs"/> class.
		/// </summary>
		/// <param name="connection">The connection for the event.</param>
		public ClientConnectionEventArgs (IClientConnection connection)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");

			Connection = connection;
		}

		/// <summary>
		/// Gets the connection for the event.
		/// </summary>
		public IClientConnection Connection
		{
			get;
			private set;
		}
	}
}
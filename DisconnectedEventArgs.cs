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

		public DisconnectedReason Reason
		{
			get;
			private set;
		}
	}
}
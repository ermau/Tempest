//
// IContext.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012 Eric Maupin
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
	public interface IContext
	{
		/// <summary>
		/// Permanently locks handler registration to improve read performance.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This locks handler registration so that no new handlers can registered, improving scalability by
		/// removing locks around message handler storage. This can not be undone.
		/// </para>
		/// </remarks>
		void LockHandlers();
		void RegisterConnectionlessMessageHandler (Protocol protocol, ushort messageType, Action<ConnectionlessMessageEventArgs> handler);

		/// <summary>
		/// Registers a message handler.
		/// </summary>
		/// <param name="protocol">The protocol of the <paramref name="messageType" />.</param>
		/// <param name="messageType">The message type to register a handler for.</param>
		/// <param name="handler">The handler to register for the message type.</param>
		/// <exception cref="ArgumentNullException"><paramref name="handler"/> is <c>null</c>.</exception>
		void RegisterMessageHandler (Protocol protocol, ushort messageType, Action<MessageEventArgs> handler);
	}

	public class MessageEventArgs<T>
		: ConnectionEventArgs
		where T : Message
	{
		public MessageEventArgs (IConnection connection, T message)
			: base (connection)
		{
			Message = message;
		}

		public T Message
		{
			get;
			private set;
		}
	}

	public static class ContextExtensions
	{
		/// <summary>
		/// Registers a message handler.
		/// </summary>
		/// <typeparam name="T">The message type.</typeparam>
		/// <param name="self">The context to register the message handler to.</param>
		/// <param name="handler">The message handler to register.</param>
		public static void RegisterMessageHandler<T> (this IContext self, Action<MessageEventArgs<T>> handler)
			where T : Message, new()
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			if (handler == null)
				throw new ArgumentNullException ("handler");

			T msg = new T();
			self.RegisterMessageHandler (msg.Protocol, msg.MessageType, e => handler (new MessageEventArgs<T> (e.Connection, (T)e.Message)));
		}
	}
}
using System;
using System.IO;

namespace Tempest.Samples.SimpleChat
{
	public class ChatClient
		: ClientBase
	{
		public ChatClient (IClientConnection connection, TextWriter writer)
			: base (connection, MessageTypes.Reliable, false)
		{
			if (writer == null)
				throw new ArgumentNullException ("writer");

			this.writer = writer;

			this.RegisterMessageHandler<ChatMessage> (OnChatMessage);
		}

		public void SetName (string name)
		{
			this.connection.Send (new SetNameMessage { Name = name });
		}

		public void SendMessage (string message)
		{
			this.connection.Send (new ChatMessage { Contents = message });
		}

		private readonly TextWriter writer;

		private void OnChatMessage (MessageEventArgs<ChatMessage> e)
		{
			this.writer.WriteLine (e.Message.Contents);
		}
	}
}
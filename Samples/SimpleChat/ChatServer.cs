using System.Collections.Concurrent;

namespace Tempest.Samples.SimpleChat
{
	public class ChatServer
		: ServerBase
	{
		public ChatServer (IConnectionProvider provider)
			: base (provider, MessageTypes.Reliable)
		{
			this.RegisterMessageHandler<SetNameMessage> (OnSetNameMessage);
			this.RegisterMessageHandler<ChatMessage> (OnChatMessage);
		}

		private readonly ConcurrentDictionary<IConnection, string> chatters = new ConcurrentDictionary<IConnection, string>();

		private void OnSetNameMessage (MessageEventArgs<SetNameMessage> e)
		{
			this.chatters[e.Connection] = e.Message.Name;
		}

		private void OnChatMessage (MessageEventArgs<ChatMessage> e)
		{
			string sender;
			if (!this.chatters.TryGetValue (e.Connection, out sender))
				return;

			e.Message.Contents = sender + ": " + e.Message.Contents;

			foreach (IConnection connection in this.chatters.Keys)
			{
				if (connection == e.Connection)
					continue;

				connection.Send (e.Message);
			}
		}

		protected override void OnConnectionDisconnected (object sender, DisconnectedEventArgs e)
		{
			string name;
			this.chatters.TryRemove (e.Connection, out name);
			
			base.OnConnectionDisconnected (sender, e);
		}
	}
}
using System.Collections.Concurrent;

namespace Tempest.Samples.SimpleChat
{
	public class ChatServer
		: ServerBase
	{
		public ChatServer (IConnectionProvider provider)
			: base (provider, MessageTypes.Reliable)
		{
			((IContext)this).RegisterMessageHandler (1, OnSetNameMessage);
			((IContext)this).RegisterMessageHandler (2, OnChatMessage);
		}

		private readonly ConcurrentDictionary<IConnection, string> chatters = new ConcurrentDictionary<IConnection, string>();

		private void OnSetNameMessage (MessageEventArgs e)
		{
			var msg = (SetNameMessage)e.Message;

			this.chatters[e.Connection] = msg.Name;
		}

		private void OnChatMessage (MessageEventArgs e)
		{
			var msg = (ChatMessage)e.Message;

			string sender;
			if (!this.chatters.TryGetValue (e.Connection, out sender))
				return;

			msg.Message = sender + ": " + msg.Message;

			foreach (IConnection connection in this.chatters.Keys)
			{
				if (connection == e.Connection)
					continue;

				connection.Send (msg);
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
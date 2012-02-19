using System;
using System.Collections.Generic;
using Tempest;

namespace SimpleChat.Client
{
	public class ChatClient
		: ClientBase
	{
		public ChatClient (IClientConnection connection)
			: base (connection, MessageTypes.Reliable)
		{
			this.RegisterMessageHandler<UserStateChangedMessage> (OnUserStateChangedMessage);
		}

		public event EventHandler<ChatEventArgs> Chat;
		public event EventHandler<UserEventArgs> UserStateChanged;

		private void OnUserStateChangedMessage (MessageEventArgs<UserStateChangedMessage> e)
		{
			var changed = UserStateChanged;
			if (changed != null)
				changed (this, new UserEventArgs (new User (e.Message.Nickname, e.Message.NewState)));
		}
	}

	public class UserEventArgs
		: EventArgs
	{
		public UserEventArgs (User user)
		{
			if (user == null)
				throw new ArgumentNullException ("user");

			User = user;
		}

		public User User
		{
			get;
			private set;
		}
	}
	
	public class ChatEventArgs
		: EventArgs
	{
		
	}
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

		public IEnumerable<User> Users
		{
			get { return this.users; }
		}

		public void SendMessage (string message)
		{
			Connection.Send (new SayMessage { Message = message });
		}

		private readonly ObservableCollection<User> users = new ObservableCollection<User>();

		private void OnUserStateChangedMessage (MessageEventArgs<UserStateChangedMessage> e)
		{
			
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
		: UserEventArgs
	{
		public ChatEventArgs (User user, string message)
			: base (user)
		{
			Message = message;
		}

		public string Message
		{
			get;
			private set;
		}
	}
}

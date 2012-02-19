using System;

namespace SimpleChat.Client
{
	public enum UserState
		: byte
	{
		Present = 1,
		Away = 2,
		Left = 3
	}

	public class User
	{
		public User (string nickname, UserState state)
		{
			if (nickname == null)
				throw new ArgumentNullException ("nickname");

			Nickname = nickname;
			State = state;
		}

		public string Nickname
		{
			get;
			private set;
		}

		public UserState State
		{
			get;
			private set;
		}
	}
}

using Tempest;

namespace SimpleChat.Client
{
	public enum SimpleChatMessageType
		: ushort
	{
		SetNickname = 1,
		Say = 2,
		Chat = 3,
		UserStateChanged = 4
	}

	public abstract class SimpleChatMessage
		: Message
	{
		protected SimpleChatMessage (SimpleChatMessageType type)
			: base (SimpleChatProtocol.Instance, (ushort)type)
		{
		}
	}

	public static class SimpleChatProtocol
	{
		public static readonly Protocol Instance = new Protocol (42, 1);

		static SimpleChatProtocol()
		{
			Instance.Discover();
		}
	}
}
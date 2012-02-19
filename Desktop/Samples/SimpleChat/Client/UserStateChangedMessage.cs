using Tempest;

namespace SimpleChat.Client
{
	public class UserStateChangedMessage
		: SimpleChatMessage
	{
		public UserStateChangedMessage()
			: base (SimpleChatMessageType.UserStateChanged)
		{
		}

		public string Nickname
		{
			get;
			set;
		}

		public UserState NewState
		{
			get;
			set;
		}

		public override void WritePayload(ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Nickname);
			writer.WriteByte ((byte)NewState);
		}
		// It's important to ensure that writes and reads occur in the same order.
		public override void ReadPayload(ISerializationContext context, IValueReader reader)
		{
			Nickname = reader.ReadString();
			NewState = (UserState)reader.ReadByte();
		}
	}
}
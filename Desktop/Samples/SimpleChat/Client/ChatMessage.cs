using Tempest;

namespace SimpleChat.Client
{
	public class ChatMessage
		: SimpleChatMessage
	{
		public ChatMessage()
			: base (SimpleChatMessageType.Chat)
		{
		}

		public string Nickname
		{
			get;
			set;
		}

		public string Message
		{
			get;
			set;
		}

		public override void WritePayload(ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Nickname);
			writer.WriteString (Message);
		}

		public override void ReadPayload(ISerializationContext context, IValueReader reader)
		{
			Nickname = reader.ReadString();
			Message = reader.ReadString();
		}
	}
}
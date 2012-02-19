using Tempest;

namespace SimpleChat.Client
{
	public class SetNicknameMessage
		: SimpleChatMessage
	{
		public SetNicknameMessage()
			: base (SimpleChatMessageType.SetNickname)
		{
		}

		public string Nickname
		{
			get;
			set;
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Nickname);
		}

		public override void ReadPayload(ISerializationContext context, IValueReader reader)
		{
			Nickname = reader.ReadString();
		}
	}
}

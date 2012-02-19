using Tempest;

namespace SimpleChat.Client
{
	public class SayMessage
		: SimpleChatMessage
	{
		public SayMessage()
			: base (SimpleChatMessageType.Say)
		{
		}

		public string Message
		{
			get;
			set;
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Message);
		}

		public override void ReadPayload(ISerializationContext context, IValueReader reader)
		{
			Message = reader.ReadString();
		}
	}
}

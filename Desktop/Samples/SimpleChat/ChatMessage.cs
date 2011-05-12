using System;

namespace Tempest.Samples.SimpleChat
{
	public class ChatMessage
		: Message
	{
		public ChatMessage()
			: base (Program.ChatProtocol, 2)
		{
			Date = DateTime.Now;
		}

		public DateTime Date
		{
			get;
			set;
		}

		public string Message
		{
			get;
			set;
		}

		public override void WritePayload (IValueWriter writer)
		{
			writer.WriteDate (Date);
			writer.WriteString (Message);
		}

		public override void ReadPayload (IValueReader reader)
		{
			Date = reader.ReadDate();
			Message = reader.ReadString();
		}
	}
}
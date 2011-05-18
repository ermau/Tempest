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

		public string Contents
		{
			get;
			set;
		}

		public override void WritePayload (IValueWriter writer)
		{
			writer.WriteDate (Date);
			writer.WriteString (this.Contents);
		}

		public override void ReadPayload (IValueReader reader)
		{
			Date = reader.ReadDate();
			this.Contents = reader.ReadString();
		}
	}
}
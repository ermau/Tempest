using System;

namespace Tempest.Samples.SimpleChat
{
	public class SetNameMessage
		: Message
	{
		public SetNameMessage()
			: base (Program.ChatProtocol, 1)
		{
		}

		public string Name
		{
			get;
			set;
		}

		public override void WritePayload (IValueWriter writer)
		{
			writer.WriteString (Name);
		}

		public override void ReadPayload (IValueReader reader)
		{
			Name = reader.ReadString();
		}
	}
}
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

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (Name);
		}

		public override void ReadPayload (ISerializationContext context, IValueReader reader)
		{
			Name = reader.ReadString();
		}
	}
}
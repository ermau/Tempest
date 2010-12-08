using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tempest.Tests
{
	public class MockProtocol
	{
		public static readonly Protocol Instance = ProtocolTests.GetTestProtocol();

		static MockProtocol()
		{
			Message.Factory.Register (Instance, new[] { typeof(MockMessage) });
		}
	}

	public class MockMessage
		: Message
	{
		public MockMessage ()
			: base (MockProtocol.Instance, 1)
		{
		}

		public string Content
		{
			get; set;
		}

		public override void WritePayload (IValueWriter writer)
		{
			writer.WriteString (Encoding.UTF8, Content);
		}

		public override void ReadPayload (IValueReader reader)
		{
			Content = reader.ReadString (Encoding.UTF8);
		}
	}
}
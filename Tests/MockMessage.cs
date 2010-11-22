using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tempest.Tests
{
	public class MockMessage
		: Message
	{
		public static readonly Protocol MockProtocol = ProtocolTests.GetTestProtocol();

		public MockMessage ()
			: base (MockProtocol, 1)
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
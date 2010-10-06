using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tempest.Tests
{
	public class MockMessage
		: Message
	{
		public MockMessage ()
			: base (1)
		{
		}

		public MockMessage (IValueReader reader)
			: base (1, reader)
		{
		}

		public override void Serialize (IValueWriter writer)
		{
		}

		public override void Deserialize (IValueReader reader)
		{
		}
	}
}
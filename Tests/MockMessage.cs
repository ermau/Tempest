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

		public override void Serialize (IValueWriter writer)
		{
		}

		public override void Deserialize (IValueReader reader)
		{
		}
	}
}
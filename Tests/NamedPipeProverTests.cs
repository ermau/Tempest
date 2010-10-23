using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class NamedPipeProverTests
		: ConnectionProviderTests
	{
		protected override EndPoint EndPoint
		{
			get { throw new NotImplementedException(); }
		}

		protected override MessageTypes MessageTypes
		{
			get { throw new NotImplementedException(); }
		}

		protected override IConnectionProvider SetUp()
		{
			throw new NotImplementedException();
		}

		protected override IClientConnection GetNewClientConnection()
		{
			throw new NotImplementedException();
		}
	}
}

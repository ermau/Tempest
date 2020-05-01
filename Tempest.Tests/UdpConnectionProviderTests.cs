using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using NUnit.Framework;
using Tempest.Providers.Network;

namespace Tempest.Tests
{
	[TestFixture]
	public class UdpConnectionProviderTests
		: ConnectionProviderTests
	{
		private static RSAAsymmetricKey serverKey;
		private static RSAAsymmetricKey localKey;

		static UdpConnectionProviderTests()
		{
			var crypto = new RSACrypto();
			serverKey = crypto.ExportKey (true);
			
			crypto = new RSACrypto();
			localKey = crypto.ExportKey (true);
		}

		protected override int MaxPayloadSize
		{
			get { return 64000; }
		}

		protected override Target Target
		{
			get { return new Target (Target.LoopbackIP, 42001); }
		}

		protected override MessageTypes MessageTypes
		{
			get { return MessageTypes.All; }
		}

		protected override IConnectionProvider SetUp()
		{
			return new UdpConnectionProvider (42001, MockProtocol.Instance, serverKey);
		}

		protected override IConnectionProvider SetUp (IEnumerable<Protocol> protocols)
		{
			return new UdpConnectionProvider (42001, protocols, serverKey);
		}

		protected override IClientConnection SetupClientConnection()
		{
			return new UdpClientConnection (MockProtocol.Instance, localKey);
		}

		protected override IClientConnection SetupClientConnection (IEnumerable<Protocol> protocols)
		{
			return new UdpClientConnection (protocols, localKey);
		}

		protected override IClientConnection SetupClientConnection (out RSAAsymmetricKey key)
		{
			key = localKey;
			return SetupClientConnection();
		}
	}
}
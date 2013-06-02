using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class RSAAsymmetricKeyTests
	{
		[Test]
		public void CtorNull()
		{
			Assert.Throws<ArgumentNullException> (() => new RSAAsymmetricKey (null));
			Assert.Throws<ArgumentNullException> (() => new RSAAsymmetricKey (SerializationContextTests.GetContext (MockProtocol.Instance), null));
		}

		[Test]
		public void ImportPrivateCspBlob()
		{
			var crypto = new RSACryptoServiceProvider();
			RSAParameters p = crypto.ExportParameters (true);
			byte[] csp = crypto.ExportCspBlob (true);

			var key = new RSAAsymmetricKey (csp);

			AssertArrayMatches (p.D, key.D);
			AssertArrayMatches (p.DP, key.DP);
			AssertArrayMatches (p.DQ, key.DQ);
			AssertArrayMatches (p.Exponent, key.Exponent);
			AssertArrayMatches (p.InverseQ, key.InverseQ);
			AssertArrayMatches (p.Modulus, key.Modulus);
			AssertArrayMatches (p.P, key.P);
			AssertArrayMatches (p.Q, key.Q);

			Assert.IsNotNull (key.PublicSignature);
		}

		[Test]
		public void Serialize()
		{
			var crypto = new RSACryptoServiceProvider();

			var p = new RSAAsymmetricKey (crypto.ExportCspBlob (true));

			byte[] buffer = new byte[20480];
			var writer = new BufferValueWriter (buffer);

			p.Serialize (null, writer);
			int len = writer.Length;
			writer.Flush();

			var reader = new BufferValueReader (buffer);
			var key = new RSAAsymmetricKey (null, reader);

			Assert.AreEqual (len, reader.Position);

			Assert.IsNull (key.D);
			Assert.IsNull (key.DP);
			Assert.IsNull (key.DQ);
			AssertArrayMatches (p.Exponent, key.Exponent);
			Assert.IsNull (key.InverseQ);
			AssertArrayMatches (p.Modulus, key.Modulus);
			Assert.IsNull (key.P);
			Assert.IsNull (key.Q);

			Assert.IsNotNull (key.PublicSignature);
		}

		private void AssertArrayMatches<T> (T[] expected, T[] actual)
		{
			Assert.AreEqual (expected.Length, actual.Length);

			for (int i = 0; i < expected.Length; ++i)
				Assert.AreEqual (expected[i], actual[i]);
		}
	}
}
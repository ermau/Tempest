//
// RSACrypto.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011 Eric Maupin
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Tempest
{
	#if !SILVERLIGHT
	public class RSACrypto
		: IPublicKeyCrypto
	{
		public RSACrypto()
		{
			List<string> algs = new List<string>();

			try
			{
				if (CryptoConfig.CreateFromName ("System.Security.Cryptography.SHA256CryptoServiceProvider") != null)
					algs.Add ("SHA256");
			}
			catch
			{
			}

			try
			{
				if (CryptoConfig.CreateFromName ("System.Security.Cryptography.SHA1CryptoServiceProvider") != null)
					algs.Add ("SHA1");
			}
			catch
			{
			}

			this.algs = algs;
		}

		public IEnumerable<String> SupportedHashAlgs
		{
			get { return this.algs; }
		}

		public int KeySize
		{
			get { return this.rsaCrypto.KeySize; }
		}

		public byte[] Encrypt (byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			return this.rsaCrypto.Encrypt (data, true);
		}

		public byte[] Decrypt (byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			return this.rsaCrypto.Decrypt (data, true);
		}

		public byte[] HashAndSign (string hashAlg, byte[] data, int offset, int count)
		{
			if (hashAlg == null)
				throw new ArgumentNullException ("hashAlg");
			if (data == null)
				throw new ArgumentNullException ("data");

			var hasher = CryptoConfig.CreateFromName (hashAlg) as HashAlgorithm;
			if (hasher == null)
				throw new ArgumentException ("Hash algorithm not found", "hashAlg");

			return this.rsaCrypto.SignData (data, offset, count, hashAlg);
		}

		public bool VerifySignedHash (string hashAlg, byte[] data, byte[] signature)
		{
			if (data == null)
				throw new ArgumentNullException ("data");
			if (signature == null)
				throw new ArgumentNullException ("signature");

			return this.rsaCrypto.VerifyData (data, hashAlg, signature);
		}

		public IAsymmetricKey ExportKey (bool includePrivate)
		{
			return new RSAAsymmetricKey (this.rsaCrypto.ExportParameters (includePrivate));
		}

		public void ImportKey (IAsymmetricKey key)
		{
			if (key == null)
				throw new ArgumentNullException ("key");

			var rsaKey = (key as RSAAsymmetricKey);
			if (rsaKey == null)
				throw new ArgumentException ("key must be RSAAsymmetricKey");

			this.rsaCrypto.ImportParameters (rsaKey);
		}

		private readonly RSACryptoServiceProvider rsaCrypto = new RSACryptoServiceProvider (4096);
		private IEnumerable<string> algs;
	}
	#endif
}
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
using System.Security.Cryptography;

namespace Tempest
{
	#if !SILVERLIGHT
	public class RSACrypto
		: IPublicKeyCrypto
	{
		public RSACrypto()
		{
			this.Sha256Name = CryptoConfig.MapNameToOID (this.sha.GetType().ToString());
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

		public byte[] HashAndSign (byte[] data, int offset, int count)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			byte[] hash = sha.ComputeHash (data, offset, count);

			return this.rsaCrypto.SignHash (hash, Sha256Name);
		}

		public bool VerifySignedHash (byte[] data, byte[] signature)
		{
			if (data == null)
				throw new ArgumentNullException ("data");
			if (signature == null)
				throw new ArgumentNullException ("signature");

			if (!this.rsaCrypto.VerifyData (data, Sha256Name, signature))
				return false;

			byte[] hash = this.sha.ComputeHash (data);
			return this.rsaCrypto.VerifyHash (hash, Sha256Name, signature);
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

		private readonly SHA256Managed sha = new SHA256Managed();
		private readonly RSACryptoServiceProvider rsaCrypto = new RSACryptoServiceProvider (4096);
		private readonly string Sha256Name;
	}
	#endif
}
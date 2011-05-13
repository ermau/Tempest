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

#if SILVERLIGHT
using RSA;
using RSA.SignatureProviders;
#endif

namespace Tempest
{
	public class RSACrypto
		: IPublicKeyCrypto
	{
		public int KeySize
		{
			get { return 4096; }
		}

		public byte[] Encrypt (byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			#if !SILVERLIGHT
			return this.rsaCrypto.Encrypt (data, true);
			#else
			return this.rsaCrypto.Encrypt (data);
			#endif
		}

		public byte[] Decrypt (byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			#if !SILVERLIGHT
			return this.rsaCrypto.Decrypt (data, true);
			#else
			return this.rsaCrypto.Decrypt (data);
			#endif
		}

		public byte[] HashAndSign (byte[] data, int offset, int count)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			#if !SILVERLIGHT
			return this.rsaCrypto.SignData (data, offset, count, Sha256Name);
			#else
			byte[] d = new byte[count];
			Buffer.BlockCopy (data, offset, d, 0, count);

			return this.rsaCrypto.SignData (d, this.shaSignature);
			#endif
		}

		public bool VerifySignedHash (byte[] data, byte[] signature)
		{
			if (data == null)
				throw new ArgumentNullException ("data");
			if (signature == null)
				throw new ArgumentNullException ("signature");

			#if !SILVERLIGHT
			return this.rsaCrypto.VerifyData (data, Sha256Name, signature);
			#else
			return this.rsaCrypto.VerifyData (data, signature, this.shaSignature);
			#endif
		}

		public IAsymmetricKey ExportKey (bool includePrivate)
		{
			#if !SILVERLIGHT
			return new RSAAsymmetricKey (this.rsaCrypto.ExportParameters (includePrivate));
			#else
			var parameters = this.rsaCrypto.ExportParameters();
			if (!includePrivate)
			{
				var p = parameters;
				parameters = new RSAParameters { N = p.N, E = p.E };
			}

			return new RSAAsymmetricKey (parameters);
			#endif
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
		
		#if !SILVERLIGHT
		private static readonly string Sha256Name = CryptoConfig.MapNameToOID ("SHA256");
		#else
		private readonly EMSAPKCS1v1_5_SHA256 shaSignature = new EMSAPKCS1v1_5_SHA256();
		#endif
		
		#if !SILVERLIGHT
		private readonly RSACryptoServiceProvider rsaCrypto = new RSACryptoServiceProvider (4096);
		#else
		private readonly RSA.RSACrypto rsaCrypto = new RSA.RSACrypto (4096);
		#endif
	}
}
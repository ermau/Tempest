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

#if SILVERLIGHT
using RSA;
using RSA.SignatureProviders;
#endif

namespace Tempest
{
	public class RSACrypto
		: IPublicKeyCrypto
	{
		public RSACrypto()
		{
			List<string> algs = new List<string>();

			#if !SILVERLIGHT
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
			#else
			algs.Add ("SHA256");
			algs.Add ("SHA1");
			#endif

			this.algs = algs;
		}

		public IEnumerable<String> SupportedHashAlgs
		{
			get { return this.algs; }
		}

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

		public byte[] HashAndSign (string hashAlg, byte[] data, int offset, int count)
		{
			if (hashAlg == null)
				throw new ArgumentNullException ("hashAlg");
			if (data == null)
				throw new ArgumentNullException ("data");

			#if !SILVERLIGHT
			var hasher = CryptoConfig.CreateFromName (hashAlg) as HashAlgorithm;
			if (hasher == null)
				throw new ArgumentException ("Hash algorithm not found", "hashAlg");
			
			return this.rsaCrypto.SignData (data, offset, count, hashAlg);
			#else
			byte[] d = new byte[count];
			Array.Copy (data, offset, d, 0, count);

			return this.rsaCrypto.SignData (d, GetSignatureProvider (hashAlg));
			#endif
		}

		#if SILVERLIGHT
		private static ISignatureProvider GetSignatureProvider (string hashAlg)
		{
			ISignatureProvider signatureProvider = null;
			switch (hashAlg)
			{
				case "SHA1":
					signatureProvider = new EMSAPKCS1v1_5_SHA1();
					break;

				case "SHA256":
					signatureProvider = new EMSAPKCS1v1_5_SHA256();
					break;
			}
			return signatureProvider;
		}
		#endif

		public bool VerifySignedHash (string hashAlg, byte[] data, byte[] signature)
		{
			if (data == null)
				throw new ArgumentNullException ("data");
			if (signature == null)
				throw new ArgumentNullException ("signature");

			#if !SILVERLIGHT
			return this.rsaCrypto.VerifyData (data, hashAlg, signature);
			#else
			return this.rsaCrypto.VerifyData (data, signature, GetSignatureProvider (hashAlg));
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
		private readonly RSACryptoServiceProvider rsaCrypto = new RSACryptoServiceProvider (4096);
		#else
		private readonly RSA.RSACrypto rsaCrypto = new RSA.RSACrypto (4096);
		#endif

		private readonly IEnumerable<string> algs;
	}
}
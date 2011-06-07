//
// RSAAsymmetricKey.cs
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
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;

#if SILVERLIGHT
using RSA;
#endif

namespace Tempest
{
	#if SILVERLIGHT
	public enum MemoryProtectionScope
	{
		SameProcess
	}

	public static class ProtectedMemory
	{
		[Conditional ("DEBUG")]
		public static void Protect (byte[] data, MemoryProtectionScope scope)
		{
		}

		[Conditional ("DEBUG")]
		public static void Unprotect (byte[] data, MemoryProtectionScope scope)
		{
		}
	}
	#endif

	public class RSAAsymmetricKey
		: IAsymmetricKey
	{
		public RSAAsymmetricKey()
		{
		}

		#if !SILVERLIGHT
		public RSAAsymmetricKey (byte[] cspBlob)
		{
			using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
			{
				rsa.ImportCspBlob (cspBlob);

				ImportRSAParameters (rsa.ExportParameters (true));
			}
		}
		#endif

		public RSAAsymmetricKey (RSAParameters parameters)
		{
			ImportRSAParameters (parameters);
		}

		public RSAAsymmetricKey (IValueReader reader)
		{
			Deserialize (reader);
		}

		public byte[] PublicSignature
		{
			get;
			private set;
		}

		public byte[] D
		{
			get;
			private set;
		}

		public byte[] DP
		{
			get;
			private set;
		}

		public byte[] DQ
		{
			get;
			private set;
		}

		public byte[] InverseQ
		{
			get;
			private set;
		}

		public byte[] P
		{
			get;
			private set;
		}

		public byte[] Q
		{
			get;
			private set;
		}

		public byte[] Modulus
		{
			get
			{
				byte[] copy = new byte[this.exponentOffset];
				Buffer.BlockCopy (this.publicKey, 0, copy, 0, this.exponentOffset);

				return copy;
			}
		}

		public byte[] Exponent
		{
			get
			{
				byte[] copy = new byte[this.publicKey.Length - this.exponentOffset];
				Buffer.BlockCopy (this.publicKey, this.exponentOffset, copy, 0, copy.Length);
				return copy;
			}
		}
		
		public void Serialize (IValueWriter writer)
		{
			if (writer.WriteBool (D != null))
			{
				writer.WriteBytes (D);
				writer.WriteBytes (DP);
				writer.WriteBytes (DQ);
				writer.WriteBytes (InverseQ);
				writer.WriteBytes (P);
				writer.WriteBytes (Q);
			}

			if (writer.WriteBool (this.publicKey != null))
			{
				writer.WriteBytes (this.publicKey);
				writer.WriteInt32 (this.exponentOffset);
			}
		}

		public void Serialize (IValueWriter writer, IPublicKeyCrypto crypto)
		{
			if (writer.WriteBool (D != null))
			{
				writer.WriteBytes (crypto.Encrypt (D));
				writer.WriteBytes (crypto.Encrypt (DP));
				writer.WriteBytes (crypto.Encrypt (DQ));
				writer.WriteBytes (crypto.Encrypt (InverseQ));
				writer.WriteBytes (crypto.Encrypt (P));
				writer.WriteBytes (crypto.Encrypt (Q));
			}

			if (writer.WriteBool (this.publicKey != null))
			{
				writer.WriteBytes (crypto.Encrypt (Exponent));

				int first = Modulus.Length / 2;
				writer.WriteBytes (crypto.Encrypt (Modulus.Copy (0, first)));
				writer.WriteBytes (crypto.Encrypt (Modulus.Copy (first, Modulus.Length - first)));
			}
		}

		public void Deserialize (IValueReader reader)
		{
			if (reader.ReadBool())
			{
				D = reader.ReadBytes();
				DP = reader.ReadBytes();
				DQ = reader.ReadBytes();
				InverseQ = reader.ReadBytes();
				P = reader.ReadBytes();
				Q = reader.ReadBytes();
			}

			if (reader.ReadBool())
			{
				this.publicKey = reader.ReadBytes();
				this.exponentOffset = reader.ReadInt32();
			}
		}

		public void Deserialize (IValueReader reader, IPublicKeyCrypto crypto)
		{
			if (reader.ReadBool())
			{
				D = crypto.Decrypt (reader.ReadBytes());
				DP = crypto.Decrypt (reader.ReadBytes());
				DQ = crypto.Decrypt (reader.ReadBytes());
				InverseQ = crypto.Decrypt (reader.ReadBytes());
				P = crypto.Decrypt (reader.ReadBytes());
				Q = crypto.Decrypt (reader.ReadBytes());
			}

			if (reader.ReadBool())
			{
				byte[] exponent = crypto.Decrypt (reader.ReadBytes());

				byte[] modulus1 = crypto.Decrypt (reader.ReadBytes());
				byte[] modulus2 = crypto.Decrypt (reader.ReadBytes());
				byte[] modulus = modulus1.Concat (modulus2).ToArray();

				this.exponentOffset = modulus.Length;
				this.publicKey = new byte[exponent.Length + modulus.Length];
				Buffer.BlockCopy (modulus, 0, this.publicKey, 0, modulus.Length);
				Buffer.BlockCopy (exponent, 0, this.publicKey, exponentOffset, exponent.Length);
			}

			SetupSignature();
		}

		public static implicit operator RSAParameters (RSAAsymmetricKey key)
		{
			return new RSAParameters
			{
				D = key.D,
				DP = key.DP,
				DQ = key.DQ,
				P = key.P,
				Q = key.Q,

				#if !SILVERLIGHT
				InverseQ = key.InverseQ,
				Exponent = key.Exponent,
				Modulus = key.Modulus,
				#else
				IQ = key.InverseQ,
				E = key.Exponent,
				N = key.Modulus
				#endif
			};
		}

		public static implicit operator RSAAsymmetricKey (RSAParameters rsaParameters)
		{
			return new RSAAsymmetricKey (rsaParameters);
		}

		private int exponentOffset;
		private byte[] publicKey;

		public override bool Equals (object obj)
		{
			RSAAsymmetricKey key = (obj as RSAAsymmetricKey);
			if (key != null)
			{
				if (this.publicKey.Length != key.publicKey.Length)
					return false;

				for (int i = 0; i < this.publicKey.Length; ++i)
				{
					if (this.publicKey[i] != key.publicKey[i])
						return false;
				}

				return true;
			}

			return base.Equals (obj);
		}

		private void SetupSignature()
		{
			using (SHA256Managed sha = new SHA256Managed())
				PublicSignature = sha.ComputeHash (this.publicKey);
		}

		private void ImportRSAParameters (RSAParameters parameters)
		{
			D = parameters.D;
			DP = parameters.DP;
			DQ = parameters.DQ;
			#if !SILVERLIGHT
			InverseQ = parameters.InverseQ;
			#else
			InverseQ = parameters.IQ;
			#endif
			P = parameters.P;
			Q = parameters.Q;

			#if !SILVERLIGHT
			this.publicKey = new byte[parameters.Modulus.Length + parameters.Exponent.Length];
			Buffer.BlockCopy (parameters.Modulus, 0, this.publicKey, 0, parameters.Modulus.Length);
			Buffer.BlockCopy (parameters.Exponent, 0, this.publicKey, parameters.Modulus.Length, parameters.Exponent.Length);
			this.exponentOffset = parameters.Modulus.Length;
			#else
			this.publicKey = new byte[parameters.N.Length + parameters.E.Length];
			Buffer.BlockCopy (parameters.N, 0, this.publicKey, 0, parameters.N.Length);
			Buffer.BlockCopy (parameters.E, 0, this.publicKey, parameters.N.Length, parameters.E.Length);
			this.exponentOffset = parameters.N.Length;
			#endif

			SetupSignature();
		}
	}
}
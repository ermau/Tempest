//
// RSAAsymmetricKey.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011-2013 Eric Maupin
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

		public RSAAsymmetricKey (byte[] cspBlob)
		{
			using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
			{
				rsa.ImportCspBlob (cspBlob);

				ImportRSAParameters (rsa.ExportParameters (true));
			}
		}

		public RSAAsymmetricKey (RSAParameters parameters)
		{
			ImportRSAParameters (parameters);
		}

		public RSAAsymmetricKey (ISerializationContext context, IValueReader reader)
		{
			Deserialize (context, reader);
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
		
		public void Serialize (ISerializationContext context, IValueWriter writer)
		{
			if (!writer.WriteBool (this.publicKey != null))
				return;

			writer.WriteBytes (this.publicKey);
			writer.WriteInt32 (this.exponentOffset);
		}

		public void Serialize (ISerializationContext context, IValueWriter writer, IAsymmetricCrypto crypto)
		{
			if (!writer.WriteBool (this.publicKey != null))
				return;

			writer.WriteBytes (crypto.Encrypt (Exponent));

			int first = this.Modulus.Length / 2;
			writer.WriteBytes (crypto.Encrypt (Modulus.Copy (0, first)));
			writer.WriteBytes (crypto.Encrypt (Modulus.Copy (first, Modulus.Length - first)));
		}

		public void Deserialize (ISerializationContext context, IValueReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");

			if (reader.ReadBool())
			{
				this.publicKey = reader.ReadBytes();
				this.exponentOffset = reader.ReadInt32();
			}

			SetupSignature();
		}

		public void Deserialize (IValueReader reader, RSACrypto crypto)
		{
			if (reader == null)
				throw new ArgumentNullException ("reader");
			if (crypto == null)
				throw new ArgumentNullException ("crypto");

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
				InverseQ = key.InverseQ,
				Exponent = key.Exponent,
				Modulus = key.Modulus,
			};
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
			using (System.Security.Cryptography.SHA256Managed sha = new System.Security.Cryptography.SHA256Managed())
				PublicSignature = sha.ComputeHash (this.publicKey);
		}

		private void ImportRSAParameters (RSAParameters parameters)
		{
			D = parameters.D;
			DP = parameters.DP;
			DQ = parameters.DQ;
			InverseQ = parameters.InverseQ;
			P = parameters.P;
			Q = parameters.Q;

			this.publicKey = new byte[parameters.Modulus.Length + parameters.Exponent.Length];
			Buffer.BlockCopy (parameters.Modulus, 0, this.publicKey, 0, parameters.Modulus.Length);
			Buffer.BlockCopy (parameters.Exponent, 0, this.publicKey, parameters.Modulus.Length, parameters.Exponent.Length);
			this.exponentOffset = parameters.Modulus.Length;

			SetupSignature();
		}
	}
}
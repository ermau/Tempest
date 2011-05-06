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
using System.Linq;
using System.Security.Cryptography;

namespace Tempest
{
	#if !SILVERLIGHT
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
			get
			{
				if (this.d.Array == null)
					return null;

				byte[] copyD = new byte[this.d.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Buffer.BlockCopy (this.privateKey, this.d.Offset, copyD, 0, this.d.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copyD;
			}
		}

		public byte[] DP
		{
			get
			{
				if (this.dp.Array == null)
					return null;

				byte[] copy = new byte[this.dp.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Buffer.BlockCopy (this.privateKey, this.dp.Offset, copy, 0, this.dp.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copy;
			}
		}

		public byte[] DQ
		{
			get
			{
				if (this.dq.Array == null)
					return null;

				byte[] copy = new byte[this.dq.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Buffer.BlockCopy (this.privateKey, this.dq.Offset, copy, 0, this.dq.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copy;
			}
		}

		public byte[] InverseQ
		{
			get
			{
				if (this.iq.Array == null)
					return null;

				byte[] copy = new byte[this.iq.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Buffer.BlockCopy (this.privateKey, this.iq.Offset, copy, 0, this.iq.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copy;
			}
		}

		public byte[] P
		{
			get
			{
				if (this.p.Array == null)
					return null;

				byte[] copy = new byte[this.p.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Buffer.BlockCopy (this.privateKey, this.p.Offset, copy, 0, this.p.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copy;
			}
		}

		public byte[] Q
		{
			get
			{
				if (this.q.Array == null)
					return null;

				byte[] copyQ = new byte[this.q.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Buffer.BlockCopy (this.privateKey, this.q.Offset, copyQ, 0, this.q.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copyQ;
			}
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
			if (writer.WriteBool (this.privateKey != null))
			{
				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				writer.WriteBytes (this.privateKey);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				writer.WriteInt32 (this.d.Offset);
				writer.WriteInt32 (this.d.Count);

				writer.WriteInt32 (this.dp.Offset);
				writer.WriteInt32 (this.dp.Count);

				writer.WriteInt32 (this.dq.Offset);
				writer.WriteInt32 (this.dq.Count);

				writer.WriteInt32 (this.iq.Offset);
				writer.WriteInt32 (this.iq.Count);

				writer.WriteInt32 (this.p.Offset);
				writer.WriteInt32 (this.p.Count);

				writer.WriteInt32 (this.q.Offset);
				writer.WriteInt32 (this.q.Count);
			}

			if (writer.WriteBool (this.publicKey != null))
			{
				writer.WriteBytes (this.publicKey);
				writer.WriteInt32 (this.exponentOffset);
			}
		}

		public void Serialize (IValueWriter writer, IPublicKeyCrypto crypto)
		{
			if (writer.WriteBool (this.privateKey != null))
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
				this.privateKey = reader.ReadBytes();
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				this.d = new ArraySegment<byte> (this.privateKey, reader.ReadInt32(), reader.ReadInt32());
				this.dp = new ArraySegment<byte> (this.privateKey, reader.ReadInt32(), reader.ReadInt32());
				this.dq = new ArraySegment<byte> (this.privateKey, reader.ReadInt32(), reader.ReadInt32());
				this.iq = new ArraySegment<byte> (this.privateKey, reader.ReadInt32(), reader.ReadInt32());
				this.p = new ArraySegment<byte> (this.privateKey, reader.ReadInt32(), reader.ReadInt32());
				this.q = new ArraySegment<byte> (this.privateKey, reader.ReadInt32(), reader.ReadInt32());
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
				byte[] ld = crypto.Decrypt (reader.ReadBytes());
				byte[] ldp = crypto.Decrypt (reader.ReadBytes());
				byte[] ldq = crypto.Decrypt (reader.ReadBytes());
				byte[] liq = crypto.Decrypt (reader.ReadBytes());
				byte[] lp = crypto.Decrypt (reader.ReadBytes());
				byte[] lq = crypto.Decrypt (reader.ReadBytes());

				this.privateKey = new byte[ld.Length + ldp.Length + ldq.Length + liq.Length + lp.Length + lq.Length];

				int offset = 0;
				
				Buffer.BlockCopy (ld, 0, this.privateKey, offset, ld.Length);
				this.d = new ArraySegment<byte> (this.privateKey, offset, ld.Length);
				offset += ld.Length;

				Buffer.BlockCopy (ldp, 0, this.privateKey, offset, ldp.Length);
				this.dp = new ArraySegment<byte> (this.privateKey, offset, ldp.Length);
				offset += ldp.Length;

				Buffer.BlockCopy (ldq, 0, this.privateKey, offset, ldq.Length);
				this.dq = new ArraySegment<byte> (this.privateKey, offset, ldq.Length);
				offset += ldq.Length;

				Buffer.BlockCopy (liq, 0, this.privateKey, offset, liq.Length);
				this.iq = new ArraySegment<byte> (this.privateKey, offset, liq.Length);
				offset += liq.Length;

				Buffer.BlockCopy (lp, 0, this.privateKey, offset, lp.Length);
				this.p = new ArraySegment<byte> (this.privateKey, offset, lp.Length);
				offset += lp.Length;

				Buffer.BlockCopy (lq, 0, this.privateKey, offset, lq.Length);
				this.q = new ArraySegment<byte> (this.privateKey, offset, lq.Length);

				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);
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
				InverseQ = key.InverseQ,
				P = key.P,
				Q = key.Q,
				Exponent = key.Exponent,
				Modulus = key.Modulus
			};
		}

		public static implicit operator RSAAsymmetricKey (RSAParameters rsaParameters)
		{
			return new RSAAsymmetricKey (rsaParameters);
		}

		private ArraySegment<byte> d;
		private ArraySegment<byte> dp;
		private ArraySegment<byte> dq;
		private ArraySegment<byte> iq;
		private ArraySegment<byte> p;
		private ArraySegment<byte> q;
		private byte[] privateKey;

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
			using (SHA1CryptoServiceProvider sha = new SHA1CryptoServiceProvider())
				PublicSignature = sha.ComputeHash (this.publicKey);
		}

		private void ImportRSAParameters (RSAParameters parameters)
		{
			int len = 0;
			len += (parameters.D != null) ? parameters.D.Length : 0;
			len += (parameters.DP != null) ? parameters.DP.Length : 0;
			len += (parameters.DQ != null) ? parameters.DQ.Length : 0;
			len += (parameters.InverseQ != null) ? parameters.InverseQ.Length : 0;
			len += (parameters.P != null) ? parameters.P.Length : 0;
			len += (parameters.Q != null) ? parameters.Q.Length : 0;

			while ((len % 16) != 0)
				len++;

			if (len != 0)
				this.privateKey = new byte[len];

			int index = 0;
			if (parameters.D != null)
			{
				Buffer.BlockCopy (parameters.D, 0, this.privateKey, index, parameters.D.Length);
				this.d = new ArraySegment<byte> (this.privateKey, index, parameters.D.Length);
				index += parameters.D.Length;
			}

			if (parameters.DP != null)
			{
				Buffer.BlockCopy (parameters.DP, 0, this.privateKey, index, parameters.DP.Length);
				this.dp = new ArraySegment<byte> (this.privateKey, index, parameters.DP.Length);
				index += parameters.DP.Length;
			}

			if (parameters.DQ != null)
			{
				Buffer.BlockCopy (parameters.DQ, 0, this.privateKey, index, parameters.DQ.Length);
				this.dq = new ArraySegment<byte> (this.privateKey, index, parameters.DQ.Length);
				index += parameters.DQ.Length;
			}

			if (parameters.InverseQ != null)
			{
				Buffer.BlockCopy (parameters.InverseQ, 0, this.privateKey, index, parameters.InverseQ.Length);
				this.iq = new ArraySegment<byte> (this.privateKey, index, parameters.InverseQ.Length);
				index += parameters.InverseQ.Length;
			}

			if (parameters.P != null)
			{
				Buffer.BlockCopy (parameters.P, 0, this.privateKey, index, parameters.P.Length);
				this.p = new ArraySegment<byte> (this.privateKey, index, parameters.P.Length);
				index += parameters.P.Length;
			}

			if (parameters.Q != null)
			{
				Buffer.BlockCopy (parameters.Q, 0, this.privateKey, index, parameters.Q.Length);
				this.q = new ArraySegment<byte> (this.privateKey, index, parameters.Q.Length);
				index += parameters.Q.Length;
			}

			if (this.privateKey != null)
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

			this.publicKey = new byte[parameters.Modulus.Length + parameters.Exponent.Length];
			Buffer.BlockCopy (parameters.Modulus, 0, this.publicKey, 0, parameters.Modulus.Length);
			Buffer.BlockCopy (parameters.Exponent, 0, this.publicKey, parameters.Modulus.Length, parameters.Exponent.Length);
			this.exponentOffset = parameters.Modulus.Length;

			SetupSignature();
		}
	}
	#endif
}
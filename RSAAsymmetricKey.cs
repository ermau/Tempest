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
	#if !SAFE
	public class RSAAsymmetricKey
		: IAsymmetricKey
	{
		public RSAAsymmetricKey (RSAParameters parameters)
		{
			int len = parameters.D.Length + parameters.DP.Length + parameters.DQ.Length + parameters.InverseQ.Length +
			          parameters.P.Length + parameters.Q.Length;

			while ((len % 16) != 0)
				len++;

			this.privateKey = new byte[len];

			int index = 0;
			Array.Copy (parameters.D, 0, this.privateKey, index, parameters.D.Length);
			d = new ArraySegment<byte> (this.privateKey, index, parameters.D.Length);

			Array.Copy (parameters.DP, 0, this.privateKey, index += parameters.D.Length, parameters.DP.Length);
			dp = new ArraySegment<byte> (this.privateKey, index, parameters.DP.Length);

			Array.Copy (parameters.DQ, 0, this.privateKey, index += parameters.DP.Length, parameters.DQ.Length);
			dq = new ArraySegment<byte> (this.privateKey, index, parameters.DQ.Length);

			Array.Copy (parameters.InverseQ, 0, this.privateKey, index += parameters.DQ.Length, parameters.InverseQ.Length);
			iq = new ArraySegment<byte> (this.privateKey, index, parameters.InverseQ.Length);

			Array.Copy (parameters.P, 0, this.privateKey, index += parameters.InverseQ.Length, parameters.P.Length);
			p = new ArraySegment<byte> (this.privateKey, index, parameters.P.Length);

			Array.Copy (parameters.Q, 0, this.privateKey, index += parameters.P.Length, parameters.Q.Length);
			q = new ArraySegment<byte> (this.privateKey, index, parameters.Q.Length);

			ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

			this.publicKey = new byte[parameters.Modulus.Length + parameters.Exponent.Length];
			Array.Copy (parameters.Modulus, this.publicKey, parameters.Modulus.Length);
			Array.Copy (parameters.Exponent, 0, this.publicKey, parameters.Modulus.Length, parameters.Exponent.Length);
			this.exponentOffset = parameters.Modulus.Length;
		}

		public RSAAsymmetricKey (IValueReader reader)
		{
			Deserialize (reader);
		}

		public byte[] Private
		{
			get
			{
				byte[] copy = new byte[this.privateKey.Length];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Array.Copy (this.privateKey, copy, this.privateKey.Length);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copy;
			}
		}

		public byte[] Public
		{
			get { return this.publicKey; }
		}

		public byte[] D
		{
			get
			{
				byte[] copyD = new byte[this.d.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Array.Copy (this.privateKey, this.d.Offset, copyD, 0, this.d.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copyD;
			}
		}

		public byte[] DP
		{
			get
			{
				byte[] copy = new byte[this.dp.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Array.Copy (this.privateKey, this.dp.Offset, copy, 0, this.dp.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copy;
			}
		}

		public byte[] DQ
		{
			get
			{
				byte[] copy = new byte[this.dq.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Array.Copy (this.privateKey, this.dq.Offset, copy, 0, this.dq.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copy;
			}
		}

		public byte[] InverseQ
		{
			get
			{
				byte[] copy = new byte[this.iq.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Array.Copy (this.privateKey, this.iq.Offset, copy, 0, this.iq.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copy;
			}
		}

		public byte[] P
		{
			get
			{
				byte[] copy = new byte[this.p.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Array.Copy (this.privateKey, this.p.Offset, copy, 0, this.p.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copy;
			}
		}

		public byte[] Q
		{
			get
			{
				byte[] copyQ = new byte[this.q.Count];

				ProtectedMemory.Unprotect (this.privateKey, MemoryProtectionScope.SameProcess);
				Array.Copy (this.privateKey, this.q.Offset, copyQ, 0, this.q.Count);
				ProtectedMemory.Protect (this.privateKey, MemoryProtectionScope.SameProcess);

				return copyQ;
			}
		}

		public byte[] Modulus
		{
			get
			{
				byte[] copy = new byte[this.exponentOffset];
				Array.Copy (this.publicKey, copy, this.exponentOffset);

				return copy;
			}
		}

		public byte[] Exponent
		{
			get
			{
				byte[] copy = new byte[this.publicKey.Length - this.exponentOffset];
				Array.Copy (this.publicKey, this.exponentOffset, copy, 0, copy.Length);
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

		public static implicit operator RSAParameters (RSAAsymmetricKey key)
		{
			return new RSAParameters
			{
				D = key.D,
				DP = key.DP,
				DQ = key.DQ,
				InverseQ = key.InverseQ,
				P = key.P,
				Q = key.Q
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
	}
	#endif
}
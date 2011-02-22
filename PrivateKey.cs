//
// PrivateKey.cs
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
	public class PrivateKey
	{
		public PrivateKey (RSAParameters parameters)
		{
			this.key =
				new byte[
					parameters.D.Length + parameters.DP.Length + parameters.DQ.Length + parameters.InverseQ.Length +
					parameters.P.Length + parameters.Q.Length];

			int index = 0;
			Array.Copy (parameters.D, 0, this.key, index, parameters.D.Length);
			d = new ArraySegment<byte> (this.key, index, parameters.D.Length);

			Array.Copy (parameters.DP, 0, this.key, index += parameters.D.Length, parameters.DP.Length);
			dp = new ArraySegment<byte> (this.key, index, parameters.DP.Length);
		}

		private ArraySegment<byte> d;
		private ArraySegment<byte> dp;
		private ArraySegment<byte> dq;
		private ArraySegment<byte> iq;
		private ArraySegment<byte> p;
		private ArraySegment<byte> q;
		private byte[] key;
	}
}
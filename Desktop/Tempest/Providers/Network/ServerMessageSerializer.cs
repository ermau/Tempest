//
// ServerMessageSerializer.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012 Eric Maupin
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
using System.Diagnostics;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	internal class ServerMessageSerializer
		: MessageSerializer
	{
		public ServerMessageSerializer (IAuthenticatedConnection connection, IEnumerable<Protocol> protocols)
			: base (connection, protocols)
		{
		}

		protected override void SignMessage (Message message, string hashAlg, BufferValueWriter writer)
		{
			if (HMAC == null)
				writer.WriteBytes (this.connection.LocalCrypto.HashAndSign (hashAlg, writer.Buffer, 0, writer.Length));
			else
				base.SignMessage (message, hashAlg, writer);
		}

		protected override bool VerifyMessage (string hashAlg, Message message, byte[] signature, byte[] data, int moffset, int length)
		{
			if (HMAC == null)
			{
				var msg = (FinalConnectMessage)message;

				byte[] resized = new byte[length];
				Buffer.BlockCopy (data, moffset, resized, 0, length);

				RSAAsymmetricKey key = (RSAAsymmetricKey)Activator.CreateInstance (msg.PublicAuthenticationKeyType);
				key.Deserialize (new BufferValueReader (msg.PublicAuthenticationKey), this.connection.Encryption);

				this.connection.RemoteKey = key;
				this.connection.RemoteCrypto.ImportKey (key);

				return this.connection.RemoteCrypto.VerifySignedHash (hashAlg, resized, signature);
			}
			else
				return base.VerifyMessage (hashAlg, message, signature, data, moffset, length);
		}
	}
}
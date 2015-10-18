//
// AcknowledgeConnectMessage.cs
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

using System.Collections.Generic;
using System.Linq;

namespace Tempest.InternalProtocol
{
	public sealed class AcknowledgeConnectMessage
		: TempestMessage
	{
		public AcknowledgeConnectMessage()
			: base (TempestMessageType.AcknowledgeConnect)
		{
		}

		public override bool Authenticated
		{
			get { return true; }
		}

		public string SignatureHashAlgorithm
		{
			get;
			set;
		}

		public IEnumerable<Protocol> EnabledProtocols
		{
			get;
			set;
		}

		public int ConnectionId
		{
			get;
			set;
		}

		public IAsymmetricKey PublicEncryptionKey
		{
			get;
			set;
		}

		public IAsymmetricKey PublicAuthenticationKey
		{
			get;
			set;
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteString (SignatureHashAlgorithm);

			Protocol[] protocols = EnabledProtocols.ToArray();
			writer.WriteInt32 (protocols.Length);
			for (int i = 0; i < protocols.Length; ++i)
				protocols[i].Serialize (context, writer);

			writer.WriteInt32 (ConnectionId);

			writer.WriteString (PublicEncryptionKey.GetType().GetSimplestName());
			PublicEncryptionKey.Serialize (context, writer);
			writer.WriteString (PublicAuthenticationKey.GetType().GetSimplestName());
			PublicAuthenticationKey.Serialize (context, writer);
		}

		public override void ReadPayload (ISerializationContext context, IValueReader reader)
		{
			SignatureHashAlgorithm = reader.ReadString();

			Protocol[] protocols = new Protocol[reader.ReadInt32()];
			for (int i = 0; i < protocols.Length; ++i)
				protocols[i] = new Protocol (context, reader);

			EnabledProtocols = protocols;

			ConnectionId = reader.ReadInt32();

			PublicEncryptionKey = ReadKey (context, reader);
			PublicAuthenticationKey = ReadKey (context, reader);
		}

		private IAsymmetricKey ReadKey (ISerializationContext context, IValueReader reader)
		{
			string keyType = reader.ReadString();
			Type encryptionKeyType = Type.GetType (keyType, true);
			if (!typeof (IAsymmetricKey).IsAssignableFrom (encryptionKeyType))
				throw new InvalidOperationException ("An invalid asymmetric key type was sent.");

			IAsymmetricKey encryptionKey = (IAsymmetricKey)Activator.CreateInstance (encryptionKeyType);
			encryptionKey.Deserialize (context, reader);
			return encryptionKey;
		}
	}
}
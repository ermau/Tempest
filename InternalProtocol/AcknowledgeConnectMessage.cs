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
	public class AcknowledgeConnectMessage
		: TempestMessage
	{
		public AcknowledgeConnectMessage()
			: base (TempestMessageType.AcknowledgeConnect)
		{
		}

		public IEnumerable<Protocol> EnabledProtocols
		{
			get;
			set;
		}

		public int NetworkId
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

		public override void WritePayload (IValueWriter writer)
		{
			Protocol[] protocols = EnabledProtocols.ToArray();
			writer.WriteInt32 (protocols.Length);
			for (int i = 0; i < protocols.Length; ++i)
				protocols[i].Serialize (writer);

			writer.WriteInt32 (NetworkId);

			writer.Write (PublicEncryptionKey);
			writer.Write (PublicAuthenticationKey);
		}

		public override void ReadPayload (IValueReader reader)
		{
			Protocol[] protocols = new Protocol[reader.ReadInt32()];
			for (int i = 0; i < protocols.Length; ++i)
				protocols[i] = new Protocol (reader);

			EnabledProtocols = protocols;

			NetworkId = reader.ReadInt32();

			PublicEncryptionKey = reader.Read<IAsymmetricKey>();
			PublicAuthenticationKey = reader.Read<IAsymmetricKey>();
		}
	}
}
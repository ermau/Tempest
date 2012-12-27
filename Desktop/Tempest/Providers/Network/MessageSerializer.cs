﻿//
// MessageSerializer.cs
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
using System.Linq;
using System.Security.Cryptography;
using Tempest.InternalProtocol;

namespace Tempest.Providers.Network
{
	// HACK
	internal interface IAuthenticatedConnection
		: IConnection
	{
		IPublicKeyCrypto LocalCrypto { get; }
		new IAsymmetricKey LocalKey { get; set; }

		IPublicKeyCrypto RemoteCrypto { get; }
		new IAsymmetricKey RemoteKey { get; set; }

		IPublicKeyCrypto Encryption { get; }
	}

	internal class MessageSerializer
	{
		public MessageSerializer (IEnumerable<Protocol> protocols)
		{
			if (protocols == null)
				throw new ArgumentNullException ("protocols");

			this.protocols = protocols.ToDictionary (p => p.id);
			this.protocols[1] = TempestMessage.InternalProtocol;
		}

		public MessageSerializer (IAuthenticatedConnection connection, IEnumerable<Protocol> protocols)
			: this (protocols)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");

			this.connection = connection;			
		}

		public MessageSerializer (MessageSerializer serializer)
		{
			if (serializer == null)
				throw new ArgumentNullException ("serializer");

			this.protocols = serializer.protocols;
			this.connection = serializer.connection;
			
			if (serializer.AES != null)
			{
				AES = new AesManaged();
				AES.KeySize = serializer.AES.KeySize;
				AES.Key = serializer.AES.Key;
			}

			if (serializer.HMAC != null)
				HMAC = new HMACSHA256 (serializer.HMAC.Key);

			this.signingHashAlgorithm = serializer.signingHashAlgorithm;
		}

		public IEnumerable<Protocol> Protocols
		{
			get { return this.protocols.Values; }
			set
			{
				if (value == null)
					throw new ArgumentNullException();

				this.protocols = value.ToDictionary (p => p.id);
			}
		}

		public AesManaged AES
		{
			get;
			set;
		}

		public HMACSHA256 HMAC
		{
			get;
			set;
		}

		public string SigningHashAlgorithm
		{
			get { return this.signingHashAlgorithm; }
			set { this.signingHashAlgorithm = value; }
		}

		#if SAFE
		public byte[] GetBytes (Message message, out int length, byte[] buffer)
		#else
		public unsafe byte[] GetBytes (Message message, out int length, byte[] buffer)
		#endif
		{
			int messageId = message.Header.MessageId;
			if (message.Header.IsResponse)
				messageId |= ResponseFlag;

			BufferValueWriter writer = new BufferValueWriter (buffer);
			writer.WriteByte (message.Protocol.id);

			int cid = (connection != null) ? connection.ConnectionId : 0;
			writer.WriteInt32 (cid); // TODO: Change to variable length later

			writer.WriteUInt16 (message.MessageType);
			writer.Length += sizeof (int); // length placeholder

			writer.WriteInt32 (messageId);

			var context = new SerializationContext (this.connection, this.protocols[message.Protocol.id], new TypeMap());

			message.WritePayload (context, writer);

			int headerLength = BaseHeaderLength;

			IList<KeyValuePair<Type, ushort>> types;
			bool hasTypes = context.TypeMap.TryGetNewTypes (out types);
			if (hasTypes)
			{
				if (types.Count > Int16.MaxValue)
					throw new ArgumentException ("Too many different types for serialization");

				int payloadLen = writer.Length;
				byte[] payload = writer.Buffer;
				writer = new BufferValueWriter (new byte[1024 + writer.Length]);
				writer.WriteByte (message.Protocol.id);
				writer.WriteInt32 (cid);
				writer.WriteUInt16 (message.MessageType);
				writer.Length += sizeof (int); // length placeholder
				writer.WriteInt32 (messageId);

				writer.Length += sizeof (ushort); // type header length placeholder
				writer.WriteUInt16 ((ushort)types.Count);
				for (int i = 0; i < types.Count; ++i)
					writer.WriteString (types[i].Key.GetSimplestName());

				#if SAFE
				Buffer.BlockCopy (BitConverter.GetBytes ((ushort)(writer.Length - BaseHeaderLength)), 0, writer.Buffer, BaseHeaderLength, sizeof (ushort));
				#else
				fixed (byte* mptr = writer.Buffer)
					*((ushort*) (mptr + BaseHeaderLength)) = (ushort)(writer.Length - BaseHeaderLength);
				#endif

				headerLength = writer.Length;
				writer.InsertBytes (headerLength, payload, BaseHeaderLength, payloadLen - BaseHeaderLength);
			}

			if (message.Encrypted)
			{
				EncryptMessage (writer, ref headerLength);
			}
			else if (message.Authenticated)
			{
				#if SAFE
				for (int i = LengthOffset; i < LengthOffset + sizeof(int); ++i)
					writer.Buffer[i] = 0;
				#else
				fixed (byte* mptr = writer.Buffer)
					*((int*)(mptr + LengthOffset)) = 0;
				#endif

				SignMessage (this.signingHashAlgorithm, writer);
			}

			byte[] rawMessage = writer.Buffer;
			length = writer.Length;
			int len = length << 1;
			if (hasTypes)
				len |= 1; // serialization header

			#if SAFE
			Buffer.BlockCopy (BitConverter.GetBytes (len), 0, rawMessage, LengthOffset, sizeof(int));
			#else
			fixed (byte* mptr = rawMessage)
				*((int*) (mptr + LengthOffset)) = len;
			#endif

			return rawMessage;
		}

		public bool TryGetHeader (BufferValueReader reader, int remaining, ref MessageHeader header)
		{
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{0}:TryGetHeader({1},{2})", c, reader.Position, remaining);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Entering {0}", (header == null) ? "without existing header" : "with existing header"), callCategory);

			int mlen; bool hasTypeHeader; Message msg = null; Protocol p;

			int headerLength = BaseHeaderLength;

			if (header == null)
				header = new MessageHeader();
			else if (header.State == HeaderState.Complete)
				return true;
			else if (header.HeaderLength > 0)
				headerLength = header.HeaderLength;

			try
			{
				if (header.State >= HeaderState.Protocol)
				{
					p = header.Protocol;
				}
				else
				{
					byte pid = reader.ReadByte();

					if (!this.protocols.TryGetValue (pid, out p))
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (Protocol " + pid + " not found)", callCategory);
						return true;
					}

					header.Protocol = p;
					header.State = HeaderState.Protocol;
				}

				if (header.State < HeaderState.CID)
				{
					header.ConnectionId = reader.ReadInt32();
					header.State = HeaderState.CID;
				}

				if (header.State >= HeaderState.Type)
				{
					msg = header.Message;
				}
				else
				{
					ushort type = reader.ReadUInt16();

					msg = header.Message = p.Create (type);
					header.State = HeaderState.Type;

					if (msg == null)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (Message " + type + " not found)", callCategory);
						return true;
					}

					msg.Header = header;
					
					if (msg.Encrypted)
						header.IsStillEncrypted = true;

					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Have " + msg.GetType().Name), callCategory);
				}

				if (header.State >= HeaderState.Length)
				{
					mlen = header.MessageLength;
					hasTypeHeader = header.HasTypeHeader;
				}
				else
				{
					mlen = reader.ReadInt32();
					hasTypeHeader = (mlen & 1) == 1;
					mlen >>= 1;

					if (mlen <= 0)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (length invalid)", callCategory);
						return true;
					}

					header.MessageLength = mlen;
					header.HasTypeHeader = hasTypeHeader;
					header.State = HeaderState.Length;

					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Have message of length: {0}, {1} type header", mlen, (hasTypeHeader) ? "with" : "without"), callCategory);
				}

				if (header.State == HeaderState.IV)
				{
					if (header.IsStillEncrypted)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (message not buffered)", callCategory);
						return !(remaining < mlen);
					}
					else if (header.Message.Encrypted)
						reader.Position = 0;
				}
				else if (msg.Encrypted && AES != null)
				{
					int ivLength = AES.IV.Length;
					headerLength += ivLength;

					if (remaining < headerLength)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not buffered (IV))", callCategory);
						return false;
					}

					byte[] iv = reader.ReadBytes (ivLength);

					header.HeaderLength = headerLength;
					header.State = HeaderState.IV;
					header.IV = iv;

					if (remaining < mlen)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (message not buffered)", callCategory);
						return false;
					}

					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (need to decrypt)", callCategory);
					return true;
				}

				if (header.State < HeaderState.MessageId)
				{
					int identV = reader.ReadInt32();
					header.MessageId = identV & ~ResponseFlag;
					header.IsResponse = (identV & ResponseFlag) == ResponseFlag;

					header.State = HeaderState.MessageId;

					Trace.WriteLineIf (NTrace.TraceVerbose, "Have message ID: " + header.MessageId, callCategory);
				}

				if (header.State < HeaderState.TypeMap)
				{
					TypeMap map;
					if (hasTypeHeader)
					{
						Trace.WriteLineIf (NTrace.TraceVerbose, "Has type header, reading types", callCategory);

						if (remaining < headerLength + (sizeof(ushort) * 2))
						{
							Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not buffered (types))", callCategory);
							return false;
						}

						if (header.TypeHeaderLength == 0)
						{
							ushort typeheaderLength = reader.ReadUInt16();
							headerLength += typeheaderLength;
							header.TypeHeaderLength = typeheaderLength;
						}

						if (remaining < headerLength)
						{
							Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not buffered (types))", callCategory);
							return false;
						}

						ushort numTypes = reader.ReadUInt16();
						var types = new Dictionary<Type, ushort> (numTypes);
						for (ushort i = 0; i < numTypes; ++i)
							types[Type.GetType (reader.ReadString())] = i;

						map = new TypeMap (types);
					}
					else
						map = new TypeMap();

					SerializationContext context;
					if (this.connection != null)
						context = new SerializationContext (this.connection, p, map);
					else
						context = new SerializationContext (p, map);

					header.SerializationContext = context;
					header.HeaderLength = headerLength;
					header.State = HeaderState.Complete;
				}

				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
				return true;
			}
			catch (Exception ex)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (error): " + ex, callCategory);
				header = null;
				return true;
			}
		}

		public unsafe List<Message> BufferMessages (ref byte[] buffer, ref int bufferOffset, ref int messageOffset, ref int remainingData, ref MessageHeader header, ref BufferValueReader reader, Func<MessageHeader, bool> messageIdCallback = null)
		{
			List<Message> messages = new List<Message>();

			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{0}:BufferMessages({1},{2},{3},{4},{5})", c, buffer.Length, bufferOffset, messageOffset, remainingData, reader.Position);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			BufferValueReader currentReader = reader;

			int length = 0;
			while (remainingData >= BaseHeaderLength)
			{
				if (!TryGetHeader (currentReader, remainingData, ref header))
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Message not ready", callCategory);
					break;
				}

				if (header == null || header.Message == null)
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (header not found)", callCategory);
					return null;
				}

				length = header.MessageLength;
				if (length > NetworkConnection.MaxMessageSize)
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (bad message size)", callCategory);
					return null;
				}

				if (header.State == HeaderState.IV)
				{
					DecryptMessage (header, ref currentReader);
					header.IsStillEncrypted = false;
					continue;
				}

				if (messageIdCallback != null && !messageIdCallback (header))
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (message id callback was false)", callCategory);
					return null;
				}

				if (remainingData < length)
				{
					bufferOffset += remainingData;
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Message not fully received (boffset={0})", bufferOffset), callCategory);
					break;
				}

				try
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Reading payload for message {0}", header.Message), callCategory);
					header.Message.ReadPayload (header.SerializationContext, currentReader);

					if (!header.Message.Encrypted && header.Message.Authenticated)
					{
						// Zero out length for message signing comparison
						#if SAFE
						for (int i = LengthOffset + messageOffset; i < LengthOffset + sizeof(int) + messageOffset; ++i)
							buffer[i] = 0;
						#else
						fixed (byte* bptr = buffer)
							*((int*)(bptr + (LengthOffset + messageOffset))) = 0;
						#endif

						int payloadLength = reader.Position;
						byte[] signature = reader.ReadBytes();
						if (!VerifyMessage (this.signingHashAlgorithm, header.Message, signature, buffer, messageOffset, payloadLength - messageOffset))
						{
							Disconnect (true, ConnectionResult.MessageAuthenticationFailed);
							Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (message auth failed)", callCategory);
							return null;
						}
					}
				}
				catch (Exception ex)
				{
					Disconnect (true);
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting for error: " + ex, callCategory);
					return null;
				}

				messages.Add (header.Message);

				currentReader = reader;
				header = null;

				if (length < buffer.Length)
				{
					messageOffset += length;
					bufferOffset = messageOffset;
					remainingData -= length;
				}
				else
				{
					messageOffset = 0;
					bufferOffset = 0;
					remainingData = 0;
					currentReader.Position = 0;
				}

				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("EOL: moffset={0},boffest={1},rdata={2},rpos={3}", messageOffset, bufferOffset, remainingData, reader.Position), callCategory);
			}

			if (remainingData > 0 || messageOffset + BaseHeaderLength >= buffer.Length)
			{
				Trace.WriteLineIf (NTrace.TraceVerbose, (remainingData > 0) ? String.Format ("Data remaining: {0:N0}", remainingData) : "Insufficient room for a header", callCategory);

				int knownRoomNeeded = (remainingData > BaseHeaderLength) ? remainingData : BaseHeaderLength;
				if (header != null && remainingData >= BaseHeaderLength)
					knownRoomNeeded = header.MessageLength;

				int pos = reader.Position - messageOffset;

				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format("Room needed: {0:N0} bytes", knownRoomNeeded), callCategory);
				if (messageOffset + knownRoomNeeded <= buffer.Length)
				{
					// bufferOffset is only moved on complete headers, so it's still == messageOffset.
					bufferOffset = messageOffset + remainingData;
					//reader.Position = pos;

					Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Exiting (sufficient room; boffest={0},rpos={1})", bufferOffset, pos), callCategory);
					return messages;
				}

				byte[] destinationBuffer = buffer;
				if (knownRoomNeeded > buffer.Length)
				{
					destinationBuffer = new byte[header.MessageLength];
					reader = new BufferValueReader (destinationBuffer);
				}

				Buffer.BlockCopy (buffer, messageOffset, destinationBuffer, 0, remainingData);
				reader.Position = pos;
				messageOffset = 0;
				bufferOffset = remainingData;
				buffer = destinationBuffer;

				Trace.WriteLineIf (NTrace.TraceVerbose, String.Format ("Exiting (moved message to front, moffset={1},boffset={2},rpos={0})", reader.Position, messageOffset, bufferOffset), callCategory);
			}
			else
				Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);

			return messages;
		}

		protected const int ResponseFlag = 16777216;
		protected const int MaxMessageId = 8388608;
		protected const int BaseHeaderLength = 15;
		protected const int LengthOffset = 1 + sizeof(int) + sizeof (ushort);
		protected readonly Func<int, IConnection> getConnection;
		protected readonly IAuthenticatedConnection connection;
		protected Dictionary<byte, Protocol> protocols;
		private string signingHashAlgorithm = "SHA256";

		private void Disconnect (bool now)
		{
			if (this.connection == null)
				return;

			if (now)
				this.connection.Disconnect();
			else
				this.connection.DisconnectAsync();
		}

		private void Disconnect (bool now, ConnectionResult result)
		{
			if (this.connection == null)
				return;

			if (now)
				this.connection.Disconnect (result);
			else
				this.connection.DisconnectAsync (result);
		}

		protected TraceSwitch NTrace
		{
			get { return NetworkConnection.NTrace; }
		}

		protected int GetNextCallId()
		{
			return NetworkConnection.GetNextCallId();
		}

		protected void EncryptMessage (BufferValueWriter writer, ref int headerLength)
		{
			AesManaged am = AES;
			if (am == null)
				return;

			ICryptoTransform encryptor = null;
			byte[] iv = null;
			lock (am)
			{
				am.GenerateIV();
				iv = am.IV;
				encryptor = am.CreateEncryptor();
			}

			const int workingHeaderLength = LengthOffset + sizeof(int); // right after length

			int r = ((writer.Length - workingHeaderLength) % encryptor.OutputBlockSize);
			if (r != 0)
			    writer.Pad (encryptor.OutputBlockSize - r);

			byte[] payload = encryptor.TransformFinalBlock (writer.Buffer, workingHeaderLength, writer.Length - workingHeaderLength);

			writer.Length = workingHeaderLength;
			writer.InsertBytes (workingHeaderLength, iv, 0, iv.Length);
			writer.WriteInt32 (payload.Length);
			writer.InsertBytes (writer.Length, payload, 0, payload.Length);

			headerLength += iv.Length;
		}

		protected void DecryptMessage (MessageHeader header, ref BufferValueReader r)
		{
			int c = 0;
			#if TRACE
			c = GetNextCallId();
			#endif

			//Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", String.Format ("{0}:{2} {1}:DecryptMessage({3},{4})", this.typeName, c, connectionId, header.IV.Length, r.Position));

			int payloadLength = r.ReadInt32();

			AesManaged am = AES;
			if (am == null)
				return;

			ICryptoTransform decryptor;
			lock (am)
			{
				am.IV = header.IV;
				decryptor = am.CreateDecryptor();
			}

			byte[] message = decryptor.TransformFinalBlock (r.Buffer, r.Position, payloadLength);
			r.Position += payloadLength; // Advance original reader position
			r = new BufferValueReader (message);

			//Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", String.Format ("{0}:{2} {1}:DecryptMessage({3},{4},{5})", this.typeName, c, connectionId, header.IV.Length, r.Position, message.Length));
		}

		protected virtual void SignMessage (string hashAlg, BufferValueWriter writer)
		{
			if (HMAC == null)
				throw new InvalidOperationException();
			
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{0}:SignMessage ({1},{2})", c, hashAlg, writer.Length);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			byte[] hash;
			lock (HMAC)
				 hash = HMAC.ComputeHash (writer.Buffer, 0, writer.Length);

			//Trace.WriteLineIf (NTrace.TraceVerbose, "Got hash:  " + GetHex (hash), callCategory);

			writer.WriteBytes (hash);

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting", callCategory);
		}

		protected virtual bool VerifyMessage (string hashAlg, Message message, byte[] signature, byte[] data, int moffset, int length)
		{
			string callCategory = null;
			#if TRACE
			int c = GetNextCallId();
			callCategory = String.Format ("{0}:VerifyMessage({1},{2},{3},{4},{5},{6})", c, hashAlg, message, signature.Length, data.Length, moffset, length);
			#endif
			Trace.WriteLineIf (NTrace.TraceVerbose, "Entering", callCategory);

			byte[] ourhash;
			lock (HMAC)
				ourhash = HMAC.ComputeHash (data, moffset, length);
			
			//Trace.WriteLineIf (NTrace.TraceVerbose, "Their hash: " + GetHex (signature), callCategory);
			//Trace.WriteLineIf (NTrace.TraceVerbose, "Our hash:   " + GetHex (ourhash), callCategory);

			if (signature.Length != ourhash.Length)
				return false;

			for (int i = 0; i < signature.Length; i++)
			{
				if (signature[i] != ourhash[i])
				{
					Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (false)", callCategory);
					return false;
				}
			}

			Trace.WriteLineIf (NTrace.TraceVerbose, "Exiting (true)", callCategory);
			return true;
		}

		protected string GetHex (byte[] ourhash)
		{
			return ourhash.Aggregate (String.Empty, (s, b) => s + b.ToString ("x2"));
		}
	}
}
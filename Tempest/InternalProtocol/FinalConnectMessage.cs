﻿//
// FinalConnectMessage.cs
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

namespace Tempest.InternalProtocol
{
	public sealed class FinalConnectMessage
		: TempestMessage
	{
		public FinalConnectMessage()
			: base (TempestMessageType.FinalConnect)
		{
		}

		public override bool Authenticated
		{
			get { return true; }
		}

		public override bool PreferReliable
		{
			get { return true; }
		}

		public byte[] AESKey
		{
			get;
			set;
		}

		public Type PublicAuthenticationKeyType
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the public authentication key.
		/// </summary>
		public byte[] PublicAuthenticationKey
		{
			get;
			set;
		}

		public override void WritePayload (ISerializationContext context, IValueWriter writer)
		{
			writer.WriteBytes (AESKey);
			writer.WriteString (PublicAuthenticationKeyType.GetSimplestName());
			writer.WriteBytes (PublicAuthenticationKey);
		}

		public override void ReadPayload (ISerializationContext context, IValueReader reader)
		{
			AESKey = reader.ReadBytes();
			PublicAuthenticationKeyType = Type.GetType (reader.ReadString());
			PublicAuthenticationKey = reader.ReadBytes();
		}
	}
}
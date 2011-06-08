//
// MessageExtensions.cs
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
using System.IO;
using NUnit.Framework;

namespace Tempest.Tests
{
	public static class MessageExtensions
	{
		public static T AssertLengthMatches<T> (this T self)
			where T : Message, new()
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			var stream = new MemoryStream (20480);
			var writer = new StreamValueWriter (stream);
			var reader = new StreamValueReader (stream);

			var msg = new T();
			var context = SerializationContextTests.GetContext (msg.Protocol);

			self.WritePayload (context, writer);
			long len = stream.Position;
			stream.Position = 0;

			msg.ReadPayload (context, reader);
			Assert.AreEqual (len, stream.Position);

			return msg;
		}
	}
}
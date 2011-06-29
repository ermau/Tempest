//
// AnonymousSerializer.cs
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

namespace Tempest
{
	public class AnonymousSerializer<T>
		: ISerializer<T>
	{
		public AnonymousSerializer (Action<ISerializationContext, IValueWriter, T> serializer, Func<ISerializationContext, IValueReader, T> deserialzier)
		{
			if (serializer == null)
				throw new ArgumentNullException ("serializer");
			if (deserialzier == null)
				throw new ArgumentNullException ("deserialzier");

			this.serializer = serializer;
			this.deserialzier = deserialzier;
		}

		public void Serialize (ISerializationContext context, IValueWriter writer, T element)
		{
			this.serializer (context, writer, element);
		}

		public T Deserialize (ISerializationContext context, IValueReader reader)
		{
			return this.deserialzier (context, reader);
		}

		private readonly Action<ISerializationContext, IValueWriter, T> serializer;
		private readonly Func<ISerializationContext, IValueReader, T> deserialzier;
	}
}
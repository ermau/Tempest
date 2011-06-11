//
// SerializationContext.cs
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
	public class SerializationContext
		: ISerializationContext
	{
		public SerializationContext (TypeMap map)
		{
			if (map == null)
				throw new ArgumentNullException ("map");

			this.map = map;
		}

		public SerializationContext (IConnection connection, Protocol protocol, TypeMap map)
			: this (map)
		{
			if (connection == null)
				throw new ArgumentNullException ("connection");
			if (protocol == null)
				throw new ArgumentNullException ("protocol");
			
			Connection = connection;
			Protocol = protocol;
		}

		public IConnection Connection
		{
			get;
			private set;
		}

		public Protocol Protocol
		{
			get;
			private set;
		}

		public TypeMap TypeMap
		{
			get { return this.map; }
		}

		private readonly TypeMap map;
	}
}
//
// ContextTests.cs
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
using NUnit.Framework;

namespace Tempest.Tests
{
	public abstract class ContextTests
	{
		protected abstract IContext Client
		{
			get;
		}

		[Test]
		public void RegisterHandler_Invalid()
		{
			Assert.Throws<ArgumentNullException> (() => Client.RegisterMessageHandler (null, 1, args => { }));
			Assert.Throws<ArgumentNullException> (() => Client.RegisterMessageHandler (MockProtocol.Instance, 1, null));
		}
		
		[Test]
		public void RegisterHandler_Locked()
		{
			Client.LockHandlers();

			Assert.Throws<InvalidOperationException> (() =>
			{
				Action<MessageEventArgs> handler = e => { };
				Client.RegisterMessageHandler (MockProtocol.Instance, 1, handler);
			});
		}

		[Test]
		public void RegisterConnectionlessHandler_Invalid()
		{
			Assert.Throws<ArgumentNullException> (() => Client.RegisterConnectionlessMessageHandler (null, 1, args => { }));
			Assert.Throws<ArgumentNullException> (() => Client.RegisterConnectionlessMessageHandler (MockProtocol.Instance, 1, null));
		}

		[Test]
		public void RegisterConnetionlessHandler_Locked()
		{
			Client.LockHandlers();

			Assert.Throws<InvalidOperationException> (() =>
			{
				Action<ConnectionlessMessageEventArgs> handler = e => { };
				Client.RegisterConnectionlessMessageHandler (MockProtocol.Instance, 1, handler);
			});
		}
	}
}
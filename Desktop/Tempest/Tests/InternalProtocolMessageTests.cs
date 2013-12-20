//
// InternalProtocolMessageTests.cs
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

using System.Linq;
using NUnit.Framework;
using Tempest.InternalProtocol;

namespace Tempest.Tests
{
	[TestFixture]
	public class InternalProtocolMessageTests
	{
		[Test]
		public void PingMessage()
		{
			var msg = new PingMessage();
			msg.Interval = 500;

			var result = msg.AssertLengthMatches();
			Assert.AreEqual (msg.Interval, result.Interval);
		}

		[Test]
		public void ConnectMessage()
		{
			var msg = new ConnectMessage();
			msg.Protocols = new[] { MockProtocol.Instance };
			msg.SignatureHashAlgorithms = new[] { "SHA1" };

			var result = msg.AssertLengthMatches();
			Assert.IsNotNull (result.Protocols);
			Assert.AreEqual (msg.Protocols.Single(), result.Protocols.Single());
			Assert.IsNotNull (result.SignatureHashAlgorithms);
			Assert.AreEqual ("SHA1", result.SignatureHashAlgorithms.Single());
		}

		[TestCase (ConnectionResult.Success, null)]
		[TestCase (ConnectionResult.Custom, "ohai")]
		public void DisconnectMessage (ConnectionResult result, string customReason)
		{
			var msg = new DisconnectMessage();
			msg.Reason = ConnectionResult.Success;
			msg.CustomReason = null;

			var mresult = msg.AssertLengthMatches();
			Assert.AreEqual (msg.Reason, mresult.Reason);
			Assert.AreEqual (msg.CustomReason, mresult.CustomReason);
		}
	}
}
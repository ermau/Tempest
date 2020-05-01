//
// TargetTests.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2012-2014 Eric Maupin
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
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class TargetTests
	{
		[Test]
		public void CtorNull()
		{
			Assert.Throws<ArgumentNullException> (() => new Target (null, 0));
		}

		[Test]
		public void Ctor()
		{
			var target = new Target ("host", 1234);
			Assert.That (target.Hostname, Is.EqualTo ("host"));
			Assert.That (target.Port, Is.EqualTo (1234));
		}

		// Extensions

		[Test]
		public void ToEndPointNull()
		{
			Assert.That (() => TargetExtensions.ToEndPoint (null), Throws.TypeOf<ArgumentNullException>());
		}

		[Test]
		public void ToEndPointIPv4()
		{
			var target = new Target ("127.0.0.1", 1234);
			EndPoint endPoint = target.ToEndPoint();

			Assert.That (endPoint, Is.InstanceOf<IPEndPoint>());

			IPEndPoint ip = (IPEndPoint) endPoint;
			Assert.That (ip.Address, Is.EqualTo (IPAddress.Parse ("127.0.0.1")));
			Assert.That (ip.AddressFamily, Is.EqualTo (AddressFamily.InterNetwork));
		}

		[Test]
		public void ToEndPointIPv6()
		{
			var target = new Target ("::1", 1234);
			EndPoint endPoint = target.ToEndPoint();

			Assert.That (endPoint, Is.InstanceOf<IPEndPoint>());

			IPEndPoint ip = (IPEndPoint) endPoint;
			Assert.That (ip.Address, Is.EqualTo (IPAddress.Parse ("::1")));
			Assert.That (ip.AddressFamily, Is.EqualTo (AddressFamily.InterNetworkV6));
			Assert.That (ip.Port, Is.EqualTo (target.Port));
		}

		[Test]
		public void ToEndPointHostname()
		{
			var target = new Target ("localhost", 1234);
			EndPoint endPoint = target.ToEndPoint();

			Assert.That (endPoint, Is.InstanceOf<DnsEndPoint>());

			DnsEndPoint dns = (DnsEndPoint) endPoint;
			Assert.That (dns.Host, Is.EqualTo (target.Hostname));
			Assert.That (dns.Port, Is.EqualTo (target.Port));
		}
	}
}
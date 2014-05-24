//
// TargetExtensions.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

#if WINDOWS_PHONE
using PhoneNetTools.Dns;
#endif

namespace Tempest
{
	public static class TargetExtensions
	{
		public static EndPoint ToEndPoint (this Target self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			IPAddress ip;
			if (IPAddress.TryParse (self.Hostname, out ip))
				return new IPEndPoint (ip, self.Port);

			return new DnsEndPoint (self.Hostname, self.Port);
		}

		public static async Task<IPEndPoint> ToIPEndPointAsync (this Target self)
		{
			EndPoint endPoint = self.ToEndPoint();
			IPEndPoint ipEndPoint = endPoint as IPEndPoint;
			if (ipEndPoint != null)
				return ipEndPoint;

			var dns = endPoint as DnsEndPoint;
			if (dns == null)
				throw new ArgumentException();


			#if !WINDOWS_PHONE
			try {
				IPHostEntry entry = await Dns.GetHostEntryAsync (dns.Host).ConfigureAwait (false);
				return new IPEndPoint (entry.AddressList.First(), dns.Port);
			} catch (SocketException) {
				return null;
			}
			#else
			var helper = new DnsHelper();
			IAsyncResult result = helper.BeginGetHostEntry (dns.Host, null, null);
			var addresses = await Task<IEnumerable<IPAddress>>.Factory.FromAsync (result, helper.EndGetHostEntry).ConfigureAwait (false);
			var address = addresses.FirstOrDefault();
			if (address == null)
				return null;

			return new IPEndPoint (address, dns.Port);
			#endif
		}

		public static Target ToTarget (this EndPoint endPoint)
		{
			if (endPoint == null)
				throw new ArgumentNullException ("endPoint");

			DnsEndPoint dns = endPoint as DnsEndPoint;
			if (dns != null)
				return new Target (dns.Host, dns.Port);

			IPEndPoint ip = endPoint as IPEndPoint;
			if (ip != null)
				return new Target (ip.Address.ToString(), ip.Port);

			throw new ArgumentException ("Unknown endpoint type", "self");
		}
	}
}
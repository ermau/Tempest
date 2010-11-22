//
// AsyncTest.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2010 Eric Maupin
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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using NAssert = NUnit.Framework.Assert;

namespace Tempest.Tests
{
	public class AsyncTest
	{
		private readonly bool multiple;
		private readonly Action<EventArgs> passTest;

		public AsyncTest()
		{
			this.passTest = e => NAssert.Pass();
		}

		public AsyncTest (bool multiple)
			: this (e => true, multiple)
		{
		}

		public AsyncTest (Action<EventArgs> passTest)
		{
			if (passTest == null)
				throw new ArgumentNullException ("passTest");
			
			this.passTest = passTest;
		}

		public AsyncTest (Func<EventArgs, bool> passPredicate)
			: this (passPredicate, false)
		{
		}

		public AsyncTest (Func<EventArgs, bool> passPredicate, bool multiple)
		{
			if (passPredicate == null)
				throw new ArgumentNullException ("passPredicate");

			this.multiple = multiple;

			this.passTest = e =>
			{
				if (passPredicate (e))
					this.passed = true;
				else if (!this.multiple)
					NAssert.Fail();
			};
		}

		public void PassHandler (object sender, EventArgs e)
		{
			try
			{
				passTest (e);
				if (!this.multiple)
					this.passed = true;
			}
			catch (Exception ex)
			{
				this.exception = ex;
			}
		}

		public void FailHandler (object sender, EventArgs e)
		{
			failed = true;
		}

		public void Assert (int timeout, bool failIfNotPassed = true)
		{
			DateTime start = DateTime.Now;
			while (DateTime.Now.Subtract (start).TotalMilliseconds < timeout)
			{
				if (failed)
					NAssert.Fail ();
				else if (passed || (exception as SuccessException) != null)
					return;
				else if (exception != null)
					throw new TargetInvocationException (exception);

				Thread.Sleep (1);
			}

			if (failIfNotPassed)
				NAssert.Fail ("Asynchronous operation timed out.");
		}

		private volatile bool passed = false;
		private volatile bool failed = false;

		private Exception exception;
	}
}
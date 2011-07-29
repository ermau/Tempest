//
// AsyncTest.cs
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
using NUnit.Framework;

namespace Tempest.Tests
{
	[TestFixture]
	public class AsyncTestTests
	{
		[Test]
		public void NonGeneric_Ctor_Invalid()
		{
			Assert.Throws<ArgumentOutOfRangeException> (() => new AsyncTest (-1));
			Assert.Throws<ArgumentNullException> (() => new AsyncTest ((Action<EventArgs>)null));
			Assert.Throws<ArgumentNullException> (() => new AsyncTest ((Action<EventArgs>)null, multiple: true));
			Assert.Throws<ArgumentNullException> (() => new AsyncTest ((Action<EventArgs>)null, times: 2));
			Assert.Throws<ArgumentOutOfRangeException> (() => new AsyncTest (e => Assert.Pass(), times: -1));
			Assert.Throws<ArgumentNullException> (() => new AsyncTest ((Func<EventArgs, bool>)null));
			Assert.Throws<ArgumentNullException> (() => new AsyncTest ((Func<EventArgs, bool>)null, multiple: true));
			Assert.Throws<ArgumentNullException> (() => new AsyncTest ((Func<EventArgs, bool>)null, times: 2));
			Assert.Throws<ArgumentOutOfRangeException> (() => new AsyncTest (e => true, times: -1));
		}

		[Test]
		public void FailIfNotPassed()
		{
			var test = new AsyncTest();

			Assert.Throws<AssertionException> (() => test.Assert (10, failIfNotPassed: true));
		}

		[Test]
		public void FailIfNotPassed_False()
		{
			var test = new AsyncTest();

			test.Assert (10, failIfNotPassed: false);
		}

		[Test]
		public void Multiple_Passes_NoFailures()
		{
			var test = new AsyncTest (multiple: true);
			test.PassHandler (this, EventArgs.Empty);
			test.PassHandler (this, EventArgs.Empty);

			test.Assert (10, failIfNotPassed: false);
		}

		[Test]
		public void Multiple_Passes_FailIfNotPassed()
		{
			var test = new AsyncTest (multiple: true);
			test.PassHandler (this, EventArgs.Empty);
			// failIfNotPassed refers to explicit Assert.Pass() calls only, not PassHandler calls.
			Assert.Throws<AssertionException> (() => test.Assert (10, failIfNotPassed: true));
		}

		[Test]
		public void Multiple_Passes_Failure()
		{
			var test = new AsyncTest (multiple: true);
			test.PassHandler (this, EventArgs.Empty);
			test.PassHandler (this, EventArgs.Empty);
			test.FailHandler (this, EventArgs.Empty);

			Assert.Throws<AssertionException> (() => test.Assert (10, failIfNotPassed: false));
		}
	}
}
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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using NAssert = NUnit.Framework.Assert;

namespace Tempest.Tests
{
	public class AsyncTest<T>
		: AsyncTest
		where T : EventArgs
	{
		public AsyncTest (Action<T> passTest)
			: base (e => passTest ((T)e))
		{
		}

		public AsyncTest (Action<T> passTest, bool multiple)
			: base (e => passTest ((T)e), multiple)
		{
		}

		public AsyncTest (Action<T> passTest, int times)
			: base (e => passTest ((T)e), times)
		{
		}

		public AsyncTest (Func<T, bool> passPredicate)
			: base (e => passPredicate ((T)e))
		{
		}

		public AsyncTest (Func<T, bool> passPredicate, bool multiple)
			: base (e => passPredicate ((T)e), multiple)
		{
		}

		public AsyncTest (Func<T, bool> passPredicate, int times)
			: base (e => passPredicate ((T)e), times)
		{
		}
	}

	public class AsyncTest
	{
		public AsyncTest()
		{
			this.passTest = e => NAssert.Pass();
		}

		public AsyncTest (int passTimes)
			: this (true)
		{
			if (passTimes < 0)
				throw new ArgumentOutOfRangeException ("passTimes");

			this.timesToPass = passTimes;
		}

		public AsyncTest (bool multiple)
			: this (e => true, multiple)
		{
		}

		public AsyncTest (Action<EventArgs> passTest)
		{
			if (passTest == null)
				throw new ArgumentNullException ("passTest");
			
			this.passTest = e =>
			{
				passTest (e);
				Interlocked.Increment (ref this.passCount);
			};
		}

		public AsyncTest (Action<EventArgs> passTest, bool multiple)
			: this (passTest)
		{
			this.multiple = multiple;
		}

		public AsyncTest (Action<EventArgs> passTest, int times)
			: this (passTest, true)
		{
			if (times < 0)
				throw new ArgumentOutOfRangeException ("times");

			this.timesToPass = times;
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
					Interlocked.Increment (ref this.passCount);
				else if (!this.multiple)
					NAssert.Fail();
			};
		}

		public AsyncTest (Func<EventArgs, bool> passPredicate, int times)
			: this (passPredicate, true)
		{
			if (times <= 0)
				throw new ArgumentOutOfRangeException ("times");

			this.timesToPass = times;
		}

		public void PassHandler (object sender, EventArgs e)
		{
			if (this.complete)
				return;

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
			if (this.complete)
				return;

			#if !TRACE
			Trace.WriteLine ("Fail handler called from " + Environment.StackTrace);
			#endif

			failed = true;
		}

		/// <summary>
		/// Explicitly fails the test with <paramref name="ex"/>.
		/// </summary>
		/// <param name="ex">The exception to fail with.</param>
		/// <remarks>
		/// <exception cref="ArgumentNullException"><paramref name="ex"/> is <c>null</c>.</exception>
		/// If <paramref name="ex"/> is <c>null</c>, it will raise the <see cref="ArgumentNullException"/> exception on assert.
		/// </remarks>
		public void FailWith (Exception ex)
		{
			if (this.complete)
				return;

			if (ex == null)
				ex = new ArgumentNullException ("ex");

			this.exception = ex;
		}

		/// <summary>
		/// Explicitly fails the test with <paramref name="message"/>.
		/// </summary>
		/// <param name="message">The message to fail with.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
		/// <remarks>
		/// If <paramref name="message"/> is <c>null</c>, it will raise the <see cref="ArgumentNullException"/> exception on assert.
		/// </remarks>
		public void FailWith (string message)
		{
			if (this.complete)
				return;

			if (message == null)
				this.exception = new ArgumentNullException ("message");
			else
				this.exception = new AssertionException (message);
		}

		/// <summary>
		/// Waits for a pass or failure and raises any async exceptions.
		/// </summary>
		/// <param name="timeout">How much time to wait, in milliseconds.</param>
		/// <param name="failIfNotPassed">Whether or not to fail if <see cref="NUnit.Framework.Assert.Pass()"/> is not called.</param>
		/// <remarks>
		/// <see cref="AsyncTest"/> will call <see cref="NUnit.Framework.Assert.Pass()" /> in non-multiple scenarios automatically.
		/// </remarks>
		public void Assert (int timeout, bool failIfNotPassed = true)
		{
			try
			{
				DateTime start = DateTime.Now;
				while (DateTime.Now.Subtract (start).TotalMilliseconds < timeout)
				{
					if (failed)
						NAssert.Fail ("Test was set as failed");
					else if (passed || (exception as SuccessException) != null)
						return;
					else if (this.timesToPass != 0 && this.passCount >= this.timesToPass)
						return;
					else if (exception != null)
						throw new TargetInvocationException (exception);

					Thread.Sleep (1);
				}

				if (this.passCount < this.timesToPass && !Debugger.IsAttached)
					NAssert.Fail ("Failed to pass required number of times in time allotted.");
				if (failIfNotPassed && !Debugger.IsAttached)
					NAssert.Fail ("Asynchronous operation timed out.");
			}
			finally
			{
				this.complete = true;
			}
		}

		private readonly bool multiple;
		private readonly Action<EventArgs> passTest;
		private int timesToPass = 0;

		private bool complete;
		private int passCount = 0;
		private volatile bool passed = false;
		private volatile bool failed = false;

		private Exception exception;
	}
}
//
// TraceSwitch.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2013 Xamarin Inc.
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

namespace Tempest
{
	class TraceSwitch
	{
		public TraceSwitch (string displayName, string description)
		{
			
		}

		public bool TraceVerbose
		{
			get { return false; }
		}

		public bool TraceWarning
		{
			get { return false; }
		}

		public bool TraceError
		{
			get { return false; }
		}
	}

	class Trace
	{
		[Conditional ("TRACE")]
		public static void WriteLineIf (bool condition, string message)
		{
			if (!condition)
				return;

			WriteLine (message);
		}

		[Conditional ("TRACE")]
		public static void WriteLineIf (bool condition, string message, string category)
		{
			if (!condition)
				return;

			WriteLine (category, message);
		}

		[Conditional ("TRACE")]
		public static void WriteLine (string message)
		{
			Console.WriteLine (message);
		}

		[Conditional ("TRACE")]
		public static void WriteLine (string message, string category)
		{
			Console.WriteLine ("[{0}] {1}", category, message);
		}
	}
}

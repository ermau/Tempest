//
// IListener.cs
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
using System.Collections.Generic;

namespace Tempest
{
	/// <summary>
	/// Contract for a listener.
	/// </summary>
	/// <see cref="IConnectionProvider"/>
	/// <see cref="IConnectionlessMessenger"/>
	public interface IListener
		: IDisposable
	{
		/// <summary>
		/// Gets whether this listener is currently running or not.
		/// </summary>
		/// <seealso cref="Start"/>
		/// <seealso cref="Stop"/>
		bool IsRunning { get; }

		/// <summary>
		/// Gets the local targets being listened to. Empty until started.
		/// </summary>
		IEnumerable<Target> LocalTargets { get; }

		/// <summary>
		/// Starts the listener.
		/// </summary>
		/// <param name="types">The message types to accept.</param>
		/// <seealso cref="Stop"/>
		void Start (MessageTypes types);

		/// <summary>
		/// Stops the listener.
		/// </summary>
		/// <seealso cref="Start"/>
		void Stop();
	}
}

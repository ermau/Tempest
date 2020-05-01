//
// Timer.cs
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
using System.Linq;
using System.Threading;

namespace Tempest
{
	public sealed class Timer
		: IDisposable
	{
		public Timer (int interval)
		{
			if (interval < 0)
				throw new ArgumentOutOfRangeException ("interval");

			this.interval = interval;
		}

		/// <summary>
		/// Raised when <see cref="Interval"/> has been reached by the timer.
		/// </summary>
		/// <seealso cref="Interval"/>
		public event EventHandler TimesUp;

		/// <summary>
		/// Gets or sets whether the timer should reset and start timing again automatically.
		/// </summary>
		/// <remarks>Defaults to <c>true</c>.</remarks>
		public bool AutoReset
		{
		    get { return this.autoReset; }
		    set { this.autoReset = value; }
		}

		/// <summary>
		/// Gets or sets the time interval in milliseconds.
		/// </summary>
		/// <seealso cref="TimesUp"/>
		public int Interval
		{
			get { return this.interval; }
			set { this.interval = value; }
		}

		/// <summary>
		/// Starts the timer.
		/// </summary>
		public void Start()
		{
			if (!this.alive)
				throw new ObjectDisposedException ("Timer");

			lock (this.sync)
			{
				if (this.timerThread == null)
				{
					(this.timerThread = new Thread (TimerProcess)
					{
						IsBackground = true,
						Name = "Timer"
					}).Start();
				}

				this.running = true;
			}
		}

		/// <summary>
		/// Stops the timer.
		/// </summary>
		public void Stop()
		{
			if (!this.alive)
				throw new ObjectDisposedException ("Timer");

			this.running = false;
		}

		public void Dispose()
		{
			if (!this.alive)
				return;

			Stop();

			this.alive = false;
			this.timerThread = null;
		}

		private int interval;
		//private readonly Action callback;

		private bool autoReset = true;
		private volatile bool alive = true;
		private volatile bool running;

		private readonly object sync = new object();
		private Thread timerThread;

		private void TimerProcess()
		{
			DateTime last = DateTime.Now;

			while (this.alive)
			{
				Thread.Sleep (this.interval / 4);
				if (!this.running)
					last = DateTime.Now;
				else if (DateTime.Now.Subtract (last).TotalMilliseconds > this.interval)
				{
					var timeup = TimesUp;
					if (timeup != null)
						timeup (this, EventArgs.Empty);

					last = DateTime.Now;

					if (!this.autoReset)
						this.running = false;
				}
			}
		}
	}
}
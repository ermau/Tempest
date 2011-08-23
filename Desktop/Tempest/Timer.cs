using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Tempest
{
	public class Timer
		: IDisposable
	{
		public Timer (int interval, Action callback)
		{
			if (callback == null)
				throw new ArgumentNullException ("callback");
			if (interval < 0)
				throw new ArgumentOutOfRangeException ("interval");

			this.interval = interval;
			this.callback = callback;
		}

		public bool AutoReset
		{
			get { return this.autoReset; }
			set { this.autoReset = value; }
		}

		public int Interval
		{
			get { return this.interval; }
			set { this.interval = value; }
		}

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

			Thread t = null;
			lock (this.sync)
			{
				if (this.timerThread != null)
					t = this.timerThread;
			}

			//if (t != null)
			//    t.Join();
		}

		private int interval;
		private readonly Action callback;

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
				Thread.Sleep (Interval / 4);
				if (!this.running)
					last = DateTime.Now;
				else if (DateTime.Now.Subtract (last).TotalMilliseconds > Interval)
				{
					callback();
					last = DateTime.Now;

					if (!this.autoReset)
						this.running = false;
				}
			}
		}
	}
}
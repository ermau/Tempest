using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Tempest.Providers.Network;

namespace Tempest.Tests
{
	[SetUpFixture]
	public class SetupFixture
	{
		[OneTimeSetUp]
		public void Setup()
		{
			Trace.Listeners.Add (new ImmediateOutput ());

			// .NET Core doesn't support .config
			NetworkConnection.NTrace.Level = TraceLevel.Verbose;
		}

		private class ImmediateOutput
			: TraceListener
		{
			public override void Write (string message)
			{
				TestContext.Write (message);
			}

			public override void WriteLine (string message)
			{
				TestContext.WriteLine (message);
			}
		}
	}
}

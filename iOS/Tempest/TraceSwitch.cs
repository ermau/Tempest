using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tempest
{
	class TraceSwitch
	{
		public TraceSwitch (string foo, string bar)
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
		public static void WriteLineIf (bool condition, string message)
		{
			
		}

		public static void WriteLineIf (bool condition, string message, string category)
		{
			
		}
	}
}

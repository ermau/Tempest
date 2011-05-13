using System.Diagnostics;

namespace Tempest
{
	public static class Trace
	{
		[Conditional ("TRACE")]
		public static void WriteLine (string message, string category)
		{
		}
	}
}
using System;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
	public static class LJ92
	{
		[DllImport("LJ92")]
		public static extern int TestMethod(int param);


	}
}
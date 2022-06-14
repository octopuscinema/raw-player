using System;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
	public static class Unpack
	{
		[DllImport("Unpack")]
		public static extern int Unpack12to16Bit([Out] byte[] out16Bit, byte[] in12Bit, IntPtr sizeBytes);

		[DllImport("Unpack")]
		public static extern int Unpack14to16Bit([Out] byte[] out16Bit, byte[] in14Bit, IntPtr sizeBytes);
	}
}


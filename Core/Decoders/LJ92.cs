using System;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
	public static class LJ92
	{
		[DllImport("LJ92")]
		public static extern Error Decode(IntPtr out16Bit, IntPtr inCompressed, uint compressedSizeBytes, uint width, uint height, uint bitDepth);
    }
}
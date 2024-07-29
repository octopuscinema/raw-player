using System;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
	public static class Jpeg
	{
		[DllImport("Jpeg")]
		public static extern Error DecodeLossless(IntPtr out16Bit, IntPtr inCompressed, uint compressedSizeBytes, uint width, uint height, uint bitDepth);
    }
}
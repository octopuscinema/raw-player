using System;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
	public static class LJ92
	{
		[DllImport("LJ92")]
		public static extern Error Decode(IntPtr out16Bit, uint outOffsetBytes, IntPtr inCompressed, uint inOffsetBytes, uint compressedSizeBytes,
			uint width, uint height, uint bitDepth);
    }
}
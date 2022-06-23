using System;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
	public static class LJ92
	{
		[DllImport("LJ92")]
		public static extern Error Decode([Out] byte[] out16Bit, uint outOffsetBytes, byte[] inCompressed, uint compressedSizeBytes, uint width, uint height, uint bitDepth);
	}
}
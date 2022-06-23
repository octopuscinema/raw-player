using System;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
	public static class Unpack
	{
		[DllImport("Unpack")]
		public static extern void Unpack12to16Bit([Out] byte[] out16Bit, UIntPtr outOffsetBytes, byte[] in12Bit, UIntPtr sizeBytes);

		[DllImport("Unpack")]
		public static extern void Unpack14to16Bit([Out] byte[] out16Bit, UIntPtr outOffsetBytes, byte[] in14Bit, UIntPtr sizeBytes);
	}
}


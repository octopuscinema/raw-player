using System;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
	public static class Unpack
	{
		[DllImport("Unpack")]
		public static extern void Unpack10to16Bit([Out] byte[] out16Bit, UIntPtr outOffsetBytes, byte[] in10Bit, UIntPtr sizeBytes);

		[DllImport("Unpack")]
		public static extern uint Unpack12InputOffsetBytes();

		[DllImport("Unpack")]
		public static extern void Unpack12to16Bit([Out] byte[] out16Bit, UIntPtr outOffsetBytes, byte[] in12Bit, uint inOffsetBytes, UIntPtr sizeBytes);

		[DllImport("Unpack")]
		public static extern void Unpack14to16Bit([Out] byte[] out16Bit, UIntPtr outOffsetBytes, byte[] in14Bit, UIntPtr sizeBytes);
	}
}


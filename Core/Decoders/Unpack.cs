using System;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
	public static class Unpack
	{
		[DllImport("Unpack")]
		public static extern void Unpack10to16Bit(IntPtr out16Bit, IntPtr in10Bit, uint sizeBytes);

		[DllImport("Unpack")]
		public static extern uint Unpack12InputOffsetBytes();

		[DllImport("Unpack")]
		public static extern void Unpack12to16Bit(IntPtr out16Bit, IntPtr in12Bit, uint sizeBytes);

		[DllImport("Unpack")]
		public static extern void Unpack14to16Bit(IntPtr out16Bit, IntPtr in14Bit, uint sizeBytes);
	}
}


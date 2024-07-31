using System;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.Decoders
{
	public static class Jpeg
	{
		[DllImport("Jpeg")]
		private static extern Error DecodeLossless(IntPtr out16Bit, IntPtr inCompressed, uint compressedSizeBytes, uint width, uint height, uint bitDepth);

        [DllImport("Jpeg")]
        private static extern bool IsLossy(IntPtr inCompressed, uint compressedSizeBytes);

        [DllImport("Jpeg")]
        private static extern Error DecodeLossy(IntPtr out16Bit, IntPtr inCompressed, uint compressedSizeBytes, uint width, uint height, uint bitDepth);

        public static Error DecodeLossless(byte[] compressedData, int compressedSizeBytes, int compressedDataOffset, byte[] dataOut, int dataOutOffset,
            in Vector2i dimensions, uint bitDepth)
        {
            unsafe
            {
                fixed (byte* pCompressedData = &compressedData[compressedDataOffset], pDataOut = &dataOut[dataOutOffset])
                {
                    return DecodeLossless(new IntPtr(pDataOut), new IntPtr(pCompressedData), (uint)compressedSizeBytes, (uint)dimensions.X, (uint)dimensions.Y, bitDepth);
                }
            }
        }
        
        public static bool IsLossy(byte[] compressedData, int compressedSizeBytes)
        {
            unsafe
            {
                fixed (byte* pCompressedData = &compressedData[0])
                {
                    return IsLossy(new IntPtr(pCompressedData), (uint)compressedSizeBytes);
                }
            }
        }

        public static Error DecodeLossy(byte[] compressedData, int compressedSizeBytes, int compressedDataOffset, byte[] dataOut, int dataOutOffset,
            in Vector2i dimensions, uint bitDepth)
        {
            unsafe
            {
                fixed (byte* pCompressedData = &compressedData[compressedDataOffset], pDataOut = &dataOut[dataOutOffset])
                {
                    return DecodeLossy(new IntPtr(pDataOut), new IntPtr(pCompressedData), (uint)compressedSizeBytes, (uint)dimensions.X, (uint)dimensions.Y, bitDepth);
                }
            }
        }
    }
}
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core.Decoders
{
    public static class Jpeg
    {
        [DllImport("turbojpeg")]
        private static extern IntPtr tj3Init(int initType);

        [DllImport("turbojpeg")]
        private static extern int tj3DecompressHeader(IntPtr handle, IntPtr jpegBuf, UIntPtr jpegSize);

        [DllImport("turbojpeg")]
        private static extern int tj3Get(IntPtr handle, int param);
        [DllImport("turbojpeg")]
        private static extern int tj3Set(IntPtr handle, int param, int value);

        [DllImport("turbojpeg")]
        private static extern int tj3Decompress12(IntPtr handle, IntPtr jpegBuf, UIntPtr jpegSize, IntPtr dstBuf, int pitch, int pixelFormat);

        [DllImport("turbojpeg")] 
        private static extern int tj3GetErrorCode(IntPtr handle);
        [DllImport("turbojpeg")]
        private static extern IntPtr tj3GetErrorStr(IntPtr handle);
        [DllImport("turbojpeg")]
        private static extern void tj3Destroy(IntPtr handle);


        private enum TJINIT
        {
            Compress,
            Decompress,
            Transform
        };

        private enum TJPARAM
        {
            TJPARAM_STOPONWARNING,
            TJPARAM_BOTTOMUP,
            TJPARAM_NOREALLOC,
            TJPARAM_QUALITY,
            TJPARAM_SUBSAMP,
            TJPARAM_JPEGWIDTH,
            TJPARAM_JPEGHEIGHT,
            TJPARAM_PRECISION,
            TJPARAM_COLORSPACE,
            TJPARAM_FASTUPSAMPLE,
            TJPARAM_FASTDCT,
            TJPARAM_OPTIMIZE,
            TJPARAM_PROGRESSIVE,
            TJPARAM_SCANLIMIT,
            TJPARAM_ARITHMETIC,
            TJPARAM_LOSSLESS,
            TJPARAM_LOSSLESSPSV,
            TJPARAM_LOSSLESSPT,
            TJPARAM_RESTARTBLOCKS,
            TJPARAM_RESTARTROWS,
            TJPARAM_XDENSITY,
            TJPARAM_YDENSITY,
            TJPARAM_DENSITYUNITS,
            TJPARAM_MAXMEMORY,
            TJPARAM_MAXPIXELS
        };

        private enum TJPF
        {
            TJPF_RGB,
            TJPF_BGR,
            TJPF_RGBX,
            TJPF_BGRX,
            TJPF_XBGR,
            TJPF_XRGB,
            TJPF_GRAY,
            TJPF_RGBA,
            TJPF_BGRA,
            TJPF_ABGR,
            TJPF_ARGB,
            TJPF_CMYK,
            TJPF_UNKNOWN = -1
        };

        public static Error Decode(IntPtr out16Bit, IntPtr inCompressed, uint compressedSizeBytes, uint width, uint height, uint bitDepth)
        {
            var handle = tj3Init((int)TJINIT.Decompress);
            if (handle == IntPtr.Zero)
                return Error.LibraryError;

            var ret = tj3DecompressHeader(handle, inCompressed, new UIntPtr(compressedSizeBytes));

            var realwidth = tj3Get(handle, (int)TJPARAM.TJPARAM_JPEGWIDTH);
            var realheight = tj3Get(handle, (int)TJPARAM.TJPARAM_JPEGHEIGHT);

            var subsamp = tj3Get(handle, (int)TJPARAM.TJPARAM_SUBSAMP);

            //ret = tj3Set(handle, (int)TJPARAM.TJPARAM_COLORSPACE, (int)TJPF.TJPF_GRAY);

            //var errorSet = Marshal.PtrToStringAnsi(tj3GetErrorStr(handle));

            var error = Error.None;
            switch(bitDepth)
            {
                case 12:
                    error = tj3Decompress12(handle, inCompressed, new UIntPtr(compressedSizeBytes), out16Bit, 0, (int)TJPF.TJPF_ARGB) == 0 ? Error.None : Error.LibraryError;
                    if ( error != Error.None)
                    {
                        var errorStr = Marshal.PtrToStringAnsi(tj3GetErrorStr(handle));
                        Debug.Assert(false);
                    }
                    break;
                default:
                    error = Error.BadMetadata;
                    break;
            }

            tj3Destroy(handle);
            return error;
        }

        public static bool IsLossy(IntPtr inCompressed, uint compressedSizeBytes)
        {
            var handle = tj3Init((int)TJINIT.Decompress);
            if (handle == IntPtr.Zero)
                return false;

            tj3DecompressHeader(handle, inCompressed, new UIntPtr(compressedSizeBytes));
            bool isLossy = tj3Get(handle, (int)TJPARAM.TJPARAM_LOSSLESS) == 0;
            tj3Destroy(handle);
            return isLossy;
        }
    }
}

using System;

namespace Octopus.Player.Core.IO
{
    public enum RawFormat
    {
        Unset = -1,
        Mono8,
        Bayer8,
        Mono16,
        Bayer16,
        Bayer16Planar, // bp16q for ProRes RAW
        DebayeredRGBA16, // bp64 for downsampled ProRes RAW
        Unknown
    };

    public static class RawFormatExtensions
    {
        public static int BytesPerPixel(this RawFormat format)
        {
            return format switch
            {
                RawFormat.Mono8 => 1,
                RawFormat.Bayer8 => 1,
                RawFormat.Mono16 => 2,
                RawFormat.Bayer16 => 2,
                _ => throw new NotImplementedException($"BytesPerPixel not implemented for {format}"),
            };
        }
    }
}
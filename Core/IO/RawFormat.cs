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
}
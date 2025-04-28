using System;

namespace Octopus.Player.Core
{
    public enum Codec
    {
        Invalid,
        Unknown,
        Unset,
        Dng,
        ProResRAW,
        ProResRAWHQ,
        TicoRAW
    }

    public enum Container
    {
        Dng,
        Mov
    }

    public static partial class Extensions
    {
        public static Essence Essence(this Container container)
        {
            switch (container)
            {
                case Container.Dng:
                    return Core.Essence.Sequence;
                case Container.Mov:
                    return Core.Essence.Video;
                default:
                    throw new Exception("Unknown container format: " + container.ToString());
            }
        }

        public static string Extension(this Container container, bool includeDot = true)
        {
            var prefix = includeDot ? new string(".") : new string("");

            switch (container)
            {
                case Container.Dng:
                    return prefix + "dng";
                case Container.Mov:
                    return prefix + "mov";
                default:
                    throw new ArgumentOutOfRangeException("Unknown container: " + container.ToString());
            }
        }

        public static string CodecDescription(this Codec codec)
        {
            switch (codec)
            {
                case Codec.Dng:
                    return "CinemaDNG";
                case Codec.ProResRAW:
                    return "ProRes RAW";
                case Codec.ProResRAWHQ:
                    return "ProRes RAW HQ";
                default:
                    throw new ArgumentOutOfRangeException("Unknown codec: " + codec.ToString());
            }
        }
    }
}

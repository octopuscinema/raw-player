namespace Octopus.Player.GPU
{
    public enum Format
    {
        BGRA8,
        RGBA8,
        RGBX8,
        RGB8,
        R8,
        RGBA16,
        RGBX16,
        RGB16,
        R16
    }

    public static partial class Extensions
    {
        public static int ComponentBitDepth(this Format format)
        {
            switch (format)
            {
                case Format.BGRA8:
                case Format.RGBA8:
                case Format.RGBX8:
                case Format.R8:
                case Format.RGB8:
                    return 8;
                case Format.RGBA16:
                case Format.RGBX16:
                case Format.R16:
                case Format.RGB16:
                    return 16;
                default:
                    throw new System.Exception("Unhandled GPU format");
            }
        }

        public static int SizeBits(this Format format)
        {
            return SizeBytes(format) * 8;
        }

        public static int SizeBytes(this Format format)
        {
            switch (format)
            {
                case Format.BGRA8:
                case Format.RGBA8:
                case Format.RGBX8:
                    return 4;
                case Format.RGB8:
                    return 3;
                case Format.RGBA16:
                case Format.RGBX16:
                    return 8;
                case Format.R8:
                    return 1;
                case Format.R16:
                    return 2;
                case Format.RGB16:
                    return 6;
                default:
                    throw new System.Exception("Unhandled GPU format");
            }
        }
    }
}
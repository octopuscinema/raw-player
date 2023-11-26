using System;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.Render
{
	public static class Extensions
	{
		public static int BytesPerPixel(this Format format)
		{
            switch (format)
            {
                case Format.RGBA8:
                    return 4;
                case Format.RGBX8:
                    return 4;
                case Format.RGB8:
                    return 3;
                case Format.R8:
                    return 1;
                case Format.RGBA16:
                case Format.RGBX16:
                    return 8;
                case Format.RGB16:
                    return 6;
                case Format.R16:
                    return 2;
                default:
                    throw new Exception("Unhandled texture format");
            }
        }
    }

    public enum TextureFilter
    {
        Nearest = 0,
        Linear = 1
    }

	public interface ITexture : IDisposable
	{
		string Name { get; }
		Vector2i Dimensions { get; }
		Format Format { get; }
        TextureFilter Filter { get; }
        bool Valid { get; }
        void Modify(IContext context, Vector2i origin, Vector2i size, byte[] imageData, uint imageDataOffset = 0);

    }
}


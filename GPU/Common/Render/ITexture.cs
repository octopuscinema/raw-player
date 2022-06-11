using System;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.Render
{
	public static class Extensions
	{
		public static uint BytesPerPixel(this TextureFormat format)
		{
            switch (format)
            {
                case TextureFormat.RGBA8:
                    return 4;
                case TextureFormat.RGBX8:
                    return 4;
                case TextureFormat.RGB8:
                    return 3;
                case TextureFormat.R8:
                    return 1;
                case TextureFormat.RGBA16:
                case TextureFormat.RGBX16:
                    return 8;
                case TextureFormat.RGB16:
                    return 6;
                case TextureFormat.R16:
                    return 2;
                default:
                    throw new Exception("Unhandled texture format");
            }
        }
    }

    public enum TextureFormat
    {
		RGBA8,
		RGBX8,
		RGB8,
        R8,
		RGBA16,
		RGBX16,
		RGB16,
		R16
    }

	public interface ITexture : IDisposable
	{
		string Name { get; }
		Vector2i Dimensions { get; }
		TextureFormat Format { get; }
		bool Valid { get; }
		void Modify(Vector2i dimensions, TextureFormat format, byte[] imageData, uint dataSizeBytes);
	}
}


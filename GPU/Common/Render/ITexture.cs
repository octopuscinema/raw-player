using System;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.Render
{
	public enum TextureFormat
    {
		RGBA8,
		RGBX8,
		RGB8,
		RGBA16,
		RGBX16,
		RGB16,
		R16
    }

	public interface ITexture : IDisposable
	{
		Vector2i Dimensions { get; }
		TextureFormat Format { get; }
		bool Valid { get; }
		void Modify(Vector2i dimensions, TextureFormat format, IntPtr imageData, uint dataSizeBytes);
	}
}


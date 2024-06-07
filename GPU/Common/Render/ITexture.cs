using System;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.Render
{
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
        IntPtr NativeHandle { get; }
        IntPtr NativeType { get; }
        bool Valid { get; }
        void Modify(IContext context, Vector2i origin, Vector2i size, byte[] imageData, uint imageDataOffset = 0);
    }
}


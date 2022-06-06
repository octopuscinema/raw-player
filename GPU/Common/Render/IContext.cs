using System;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.Render
{
    public interface IContext : IDisposable
    {
        object NativeContext { get; }

        ITexture CreateTexture(Vector2i dimensions, TextureFormat format);
        ITexture CreateTexture(Vector2i dimensions, TextureFormat format, IntPtr imageData);
        void DestroyTexture(ITexture texture);
        void EnqueueRenderAction(Action action);

        void OnRenderFrame(double timeInterval);
    }
}


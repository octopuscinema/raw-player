using System;
using System.IO;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.Render
{
    public enum Api
    {
        OpenGL,
        Metal,
        Direct3D
    }

    public delegate void ForceRender();

    public interface IContext : IDisposable
    {
        Api Api { get; }

        object NativeContext { get; }

        event ForceRender ForceRender;

        ITexture CreateTexture(Vector2i dimensions, TextureFormat format, string name = null);
        ITexture CreateTexture(Vector2i dimensions, TextureFormat format, byte[] imageData, string name = null);
        void DestroyTexture(ITexture texture);

        IShader CreateShader(System.Reflection.Assembly assembly, string shaderResourceName, string name = null);
        void DestroyShader(IShader shader);
        
        void EnqueueRenderAction(Action action);

        void OnRenderFrame(double timeInterval);
        void Draw2D(IShader shader, ITexture texture, Vector2i pos, Vector2i size);
        void RequestRender();
    }
}


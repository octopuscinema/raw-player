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
    public interface IContext : IDisposable
    {
        Api Api { get; }

        object NativeContext { get; }

        ITexture CreateTexture(Vector2i dimensions, TextureFormat format, string name = null);
        ITexture CreateTexture(Vector2i dimensions, TextureFormat format, IntPtr imageData, string name = null);
        void DestroyTexture(ITexture texture);

        IShader CreateShader(Stream vertexShaderSource, Stream fragmentShaderSource, string name = null);
        IShader CreateShader(System.Reflection.Assembly assembly, string shaderResourceName, string name = null);
        void DestroyShader(IShader shader);
        
        void EnqueueRenderAction(Action action);

        void OnRenderFrame(double timeInterval);
    }
}


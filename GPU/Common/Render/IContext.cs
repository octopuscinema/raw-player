using System;
using System.Collections.Generic;
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

    public enum RedrawBackground
    {
        Once,
        Off,
        On
    }

    public delegate void ForceRender();

    public interface IContext : IDisposable
    {
        Api Api { get; }
        string ApiVersion { get; }
        string ApiRenderer { get; }
        string ApiVendor { get; }
        string ApiShadingLanguageVersion { get; }

        Vector3 BackgroundColor {get; set;}
        RedrawBackground RedrawBackground {get; set;}

        object NativeContext { get; }
        Vector2i FramebufferSize { get; }

        event ForceRender ForceRender;

        ITexture CreateTexture(Vector2i dimensions, TextureFormat format, TextureFilter filter = TextureFilter.Nearest, string name = null);
        ITexture CreateTexture(Vector2i dimensions, TextureFormat format, byte[] imageData, TextureFilter filter = TextureFilter.Nearest, string name = null);
        ITexture CreateTexture(uint size, TextureFormat format, byte[] imageData, TextureFilter filter = TextureFilter.Linear, string name = null);
        void DestroyTexture(ITexture texture);

        IShader CreateShader(System.Reflection.Assembly assembly, string shaderResourceName, string name = null, IList<string> defines = null);
        void DestroyShader(IShader shader);
        
        void EnqueueRenderAction(Action action);

        void OnRenderFrame(double timeInterval);
        void Draw2D(IShader shader, IDictionary<string, ITexture> textures, Vector2i pos, Vector2i size);
        void RequestRender();
    }
}


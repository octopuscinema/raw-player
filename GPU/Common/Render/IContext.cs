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
        object NativeHandle { get; }
        IntPtr NativeContext { get; }
        IntPtr NativeDeviceContext { get; }
        Vector2i FramebufferSize { get; }

        event ForceRender ForceRender;

        ITexture CreateTexture(Vector2i dimensions, Format format, TextureFilter filter = TextureFilter.Nearest, string name = null);
        ITexture CreateTexture(Vector2i dimensions, Format format, byte[] imageData, TextureFilter filter = TextureFilter.Nearest, string name = null);
        ITexture CreateTexture(uint size, Format format, byte[] imageData, TextureFilter filter = TextureFilter.Linear, string name = null);
        void DestroyTexture(ITexture texture);

        IShader CreateShader(System.Reflection.Assembly assembly, string shaderResourceName, string name = null, IReadOnlyCollection<string> defines = null);
        void DestroyShader(IShader shader);
        
        void EnqueueRenderAction(Action action);
        void ClearRenderActions();

        void OnRenderFrame(double timeInterval);
        void Blit2D(ITexture texture, Vector2i pos, Vector2i size);
        void Blit2D(ITexture texture, Vector2i pos, Vector2i size, Orientation orientation);
        void Blit2D(ITexture texture, Vector2i pos, Vector2i size, in Vector4 uv);
        void Draw2D(IShader shader, IDictionary<string, ITexture> textures, Vector2i pos, Vector2i size);
        void Draw2D(IShader shader, IDictionary<string, ITexture> textures, Vector2i pos, Vector2i size, in Vector4 uv, bool transposeUv = false);
        void RequestRender();
        void Finish();
    }
}


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Octopus.Player.GPU.Render;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

// Xamarin.mac uses a baked in old version of OpenTK which doesn't expose GL3+ functions, expose them manually
#if __MACOS__
using System.Runtime.InteropServices;
namespace OpenTK.Graphics.OpenGL
{
    internal class GL3
    {
        internal const string Library = "/System/Library/Frameworks/OpenGL.framework/OpenGL";

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [DllImport(Library, EntryPoint = "glGenVertexArrays", ExactSpelling = true)]
        internal extern static void GenVertexArrays(Int32 n, [Out] UInt32[] arrays);

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [DllImport(Library, EntryPoint = "glBindVertexArray", ExactSpelling = true)]
        internal extern static void BindVertexArray(UInt32 array);

        [System.Security.SuppressUnmanagedCodeSecurity()]
        [DllImport(Library, EntryPoint = "glDeleteProgram", ExactSpelling = true)]
        internal extern static void DeleteProgram(UInt32 program);
    }
}
#endif

namespace Octopus.Player.GPU.OpenGL.Render
{
    public class Context : IContext
    {
        public Api Api { get { return Api.OpenGL; } }
        public string ApiVersion { get; private set; }
        public string ApiRenderer { get; private set; }
        public string ApiVendor { get; private set; }
        public string ApiShadingLanguageVersion { get; private set; }
        public Vector3 BackgroundColor { get; set; }
        public RedrawBackground RedrawBackground { get; set; }
        public object NativeHandle { get; private set; }
        public IntPtr NativeContext { get; private set; }
        public IntPtr NativeDeviceContext { get; private set; }
        public Vector2i FramebufferSize { get { return NativeWindow.FramebufferSize; } }
        public event ForceRender ForceRender;

        private List<ITexture> textures;
        private List<IShader> shaders;
        public IList<ITexture> Textures { get { return textures.AsReadOnly(); } }
        public IList<IShader> Shaders { get { return shaders.AsReadOnly(); } }

        object renderActionsLock;
        private List<Action> RenderActions { get; set; }
        public UI.INativeWindow NativeWindow { get; private set; }

        internal TextureUnit ActiveTextureUnit { get; private set; }

        private VertexBuffer Draw2DVertexBuffer { get; set; }
        private VertexBuffer activeVertexBuffer;
        private Shader activeShader;
        private IDictionary<TextureUnit,Texture> activeTexture;

        private int DefaultVertexArrayHandle { get; set; }

        public Context(UI.INativeWindow nativeWindow, object nativeHandle, IntPtr nativeContext, IntPtr nativeDeviceContext)
        {
            // Initialise GPU resource lists
            NativeWindow = nativeWindow;
            NativeHandle = nativeHandle;
            NativeDeviceContext = nativeDeviceContext;
            NativeContext = nativeContext;
            textures = new List<ITexture>();
            shaders = new List<IShader>();
            RenderActions = new List<Action>();
            renderActionsLock = new object();

            // Setup active texture tracking dictionary
            activeTexture = new Dictionary<TextureUnit, Texture>();
            ActiveTextureUnit = TextureUnit.Texture0;

            // Create default vertex array object
#if __MACOS__
            uint[] vertexArrays = new uint[1];
            GL3.GenVertexArrays(1, vertexArrays);
            DefaultVertexArrayHandle = (int)vertexArrays[0];
            GL3.BindVertexArray((uint)DefaultVertexArrayHandle);
#else
            DefaultVertexArrayHandle = GL.GenVertexArray();
            GL.BindVertexArray(DefaultVertexArrayHandle);
#endif
            CheckError();

            // Create vertex buffer for 2D drawing
            var vertexFormat = new VertexFormat();
            vertexFormat.AddParameter(VertexFormatParameter.Position2f, "VertexPosition");
            Vector2[] rectVerts = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            Draw2DVertexBuffer = new VertexBuffer(this, vertexFormat, GPU.Render.BufferUsageHint.Static, rectVerts, (uint)rectVerts.Length);

            // Apply default state
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Blend);

            // Print GL info
            Trace.WriteLine("Created OpenGL render context on thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            ApiVersion = GL.GetString(StringName.Version);
            ApiShadingLanguageVersion = GL.GetString(StringName.ShadingLanguageVersion);
            ApiRenderer = GL.GetString(StringName.Renderer);
            ApiVendor = GL.GetString(StringName.Vendor);
            Trace.WriteLine("OpenGL version: " + ApiVersion);
            Trace.WriteLine("GLSL version: " + ApiShadingLanguageVersion);
        }

        public ITexture CreateTexture(Vector2i dimensions, TextureFormat format, TextureFilter filter = TextureFilter.Nearest, string name = null)
        {
            return CreateTexture(dimensions, format, null, filter, name);
        }

        public ITexture CreateTexture(Vector2i dimensions, TextureFormat format, byte[] imageData, TextureFilter filter = TextureFilter.Nearest, string name = null)
        {
            var texture = new Texture(this, dimensions, format, imageData, filter);
            textures.Add(texture);
            return texture;
        }

        public ITexture CreateTexture(uint size, TextureFormat format, byte[] imageData, TextureFilter filter = TextureFilter.Linear, string name = null)
        {
            var texture = new Texture(this, size, format, imageData, filter);
            textures.Add(texture);
            return texture;
        }

        public void DestroyTexture(ITexture texture)
        {
            textures.Remove(texture);
            texture.Dispose();
        }

        public IShader CreateShader(System.Reflection.Assembly assembly, string shaderResourceName, string name = null, IList<string> defines = null)
        {
            if (!Path.HasExtension(shaderResourceName))
                shaderResourceName += ".glsl";

            var resources = assembly.GetManifestResourceNames();
            foreach (string resource in resources) 
            {
                if (resource.Contains(shaderResourceName))
                {
                    var shader = new Shader(this, assembly, resource, Draw2DVertexBuffer.VertexFormat, name, defines);
                    shaders.Add(shader);
                    return shader;
                }
            }

            throw new Exception("Error locating GLSL shader resource: " + shaderResourceName);
        }

        public void DestroyShader(IShader shader)
        {
            shaders.Remove(shader);
            shader.Dispose();
        }

        public static void CheckError()
        {
#if DEBUG
            var lastErrorCode = GL.GetError();
            if (lastErrorCode != ErrorCode.NoError)
                throw new Exception("OpenGL Error: " + lastErrorCode.ToString());
#endif
        }

        public void EnqueueRenderAction(Action action)
        {
            if (RenderActions == null)
                return;
            lock (renderActionsLock)
            {
                RenderActions.Add(action);
            }
            ForceRender?.Invoke();
        }

        public void ClearRenderActions()
        {
            lock (renderActionsLock)
            {
                RenderActions.Clear();
            }
        }

        public void OnRenderFrame(double timeInterval)
        {
            if (RenderActions != null && RenderActions.Any())
            {
                lock (renderActionsLock)
                {
                    foreach (var action in RenderActions)
                        action();
                    RenderActions.Clear();
                }
            }

            // Draw background
            if (RedrawBackground != RedrawBackground.Off)
            {
                GL.ClearColor(BackgroundColor.X, BackgroundColor.Y, BackgroundColor.Z, 1);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                if (RedrawBackground == RedrawBackground.Once)
                    RedrawBackground = RedrawBackground.Off;
            }
        }

        public void Draw2D(IShader shader, IDictionary<string, ITexture> textures, Vector2i pos, Vector2i size)
        {
            Draw2D(shader, textures, pos, size, new Vector4i(0, 0, 1, 1));
        }

        public void Draw2D(IShader shader, IDictionary<string, ITexture> textures, Vector2i pos, Vector2i size, in Vector4 uv)
        {
            SetVertexBuffer(Draw2DVertexBuffer);
            SetShader((Shader)shader);
            if (textures != null)
            {
                int textureUnit = 0;
                foreach (var texture in textures)
                {
                    shader.SetUniform(this, texture.Key, textureUnit);
                    SetTexture((Texture)texture.Value, TextureUnit.Texture0 + textureUnit);
                    textureUnit++;
                }
            }
            shader.SetUniform(this, "RectUV", new Vector4(uv));
            shader.SetUniform(this, "RectBounds", new Vector4(pos.X, pos.Y, size.X, size.Y));
            shader.SetUniform(this, "OrthographicBoundsInverse", new Vector2(1, 1) / FramebufferSize.ToVector2());

#if __MACOS__
            GL.DrawArrays(BeginMode.TriangleFan, 0, 4);
#else
			GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
#endif
        }

        internal void SetShader(Shader shader)
        {
            if (activeShader == shader)
                return;

            shader.Bind();
            activeShader = shader;
        }

        private void SetVertexBuffer(VertexBuffer vertexBuffer)
        {
            if (activeVertexBuffer == vertexBuffer)
                return;

            if (activeVertexBuffer != null)
                activeVertexBuffer.Unbind();
            if ( vertexBuffer != null)
                vertexBuffer.Bind();
            activeVertexBuffer = vertexBuffer;
        }

        internal void SetTexture(Texture texture, TextureUnit unit = TextureUnit.Texture0)
        {
            if (activeTexture.ContainsKey(unit) && activeTexture[unit] == texture)
                return;

            if (texture == null)
            {
                if (activeTexture.ContainsKey(unit))
                    activeTexture[unit]?.Unbind(unit);
            }
            else
            {
                texture.Bind(unit);
                ActiveTextureUnit = unit;
            }
            activeTexture[unit] = texture;
        }

        internal void SetActiveTextureUnit(TextureUnit unit)
        {
            if (ActiveTextureUnit == unit)
                return;

            GL.ActiveTexture(unit);
            ActiveTextureUnit = unit;
        }

        public void Dispose()
        {
            SetVertexBuffer(null);
            Draw2DVertexBuffer.Dispose();
#if __MACOS__
            var handle = DefaultVertexArrayHandle;
            GL.DeleteBuffers(1, ref handle);
#else
            GL.DeleteBuffer(DefaultVertexArrayHandle);
#endif
            foreach (var texture in textures)
                texture.Dispose();
            textures = null;

            lock (renderActionsLock)
            {
                RenderActions = null;
            }
            renderActionsLock = null;
        }

        public void RequestRender()
        {
            ForceRender?.Invoke();
#if __MACOS__
            RedrawBackground = GPU.Render.RedrawBackground.Once;
#endif
        }
    }
}


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Octopus.Player.GPU.Render;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.OpenGL.Render
{
    public class Context : IContext
    {
        public GPU.Render.Api Api { get { return Api.OpenGL; } }
        public Vector3 BackgroundColor { get; set; }
        public RedrawBackground RedrawBackground { get; set; }
        public object NativeContext { get; private set; }
        public Vector2i FramebufferSize { get { return NativeWindow.FramebufferSize; } }
        public event ForceRender ForceRender;

        private List<ITexture> textures;
        private List<IShader> shaders;
        public IList<ITexture> Textures { get { return textures.AsReadOnly(); } }
        public IList<IShader> Shaders { get { return shaders.AsReadOnly(); } }

        object renderActionsLock;
        private List<Action> RenderActions { get; set; }
        public UI.INativeWindow NativeWindow { get; private set; }

        private VertexBuffer Draw2DVertexBuffer { get; set; }
        private VertexBuffer activeVertexBuffer;
        private Shader activeShader;
        private IDictionary<TextureUnit,Texture> activeTexture;

        private int DefaultVertexArrayHandle { get; set; }

        public Context(UI.INativeWindow nativeWindow, object nativeContext)
        {
            // Initialise GPU resource lists
            NativeWindow = nativeWindow;
            NativeContext = nativeContext;
            textures = new List<ITexture>();
            shaders = new List<IShader>();
            RenderActions = new List<Action>();
            renderActionsLock = new object();

            // Setup active texture tracking dictionary
            activeTexture = new Dictionary<TextureUnit, Texture>();

            // Create default vertex array object
#if !__MACOS__
            DefaultVertexArrayHandle = GL.GenVertexArray();
            GL.BindVertexArray(DefaultVertexArrayHandle);
            CheckError();
#endif

            // Create vertex buffer for 2D drawing
            var vertexFormat = new VertexFormat();
            vertexFormat.AddParameter(VertexFormatParameter.Position2f, "VertexPosition");
            Vector2[] rectVerts = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            Draw2DVertexBuffer = new VertexBuffer(this, vertexFormat, GPU.Render.BufferUsageHint.Static, rectVerts, (uint)rectVerts.Length);

            Trace.WriteLine("Created OpenGL render context on thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId);

            Trace.WriteLine("OpenGL version: " + GL.GetString(StringName.Version));
            Trace.WriteLine("GLSL version: " + GL.GetString(StringName.ShadingLanguageVersion));
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
            shader.SetUniform(this, "RectBounds", new Vector4(pos.X, pos.Y, size.X, size.Y));
            shader.SetUniform(this, "OrthographicBoundsInverse", new Vector2(1, 1) / FramebufferSize.ToVector2());

#if __MACOS__
            GL.DrawArrays(BeginMode.TriangleFan, 0, 4);
#else
			GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
#endif
        }

        public void SetShader(Shader shader)
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

        public void SetTexture(Texture texture, TextureUnit unit = TextureUnit.Texture0)
        {
            if (activeTexture.ContainsKey(unit) && activeTexture[unit] == texture)
                return;

            if (texture == null)
                Texture.Unbind(unit);
            else
                texture.Bind(unit);
            activeTexture[unit] = texture;
        }

        public void Dispose()
        {
            SetVertexBuffer(null);
            Draw2DVertexBuffer.Dispose();
#if !__MACOS__
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
        }
    }
}


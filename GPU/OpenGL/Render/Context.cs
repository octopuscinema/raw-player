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
        public object NativeContext { get; private set; }
        public int RenderThreadId { get; private set; }
        public event ForceRender ForceRender;

        private List<ITexture> textures;
        private List<IShader> shaders;
        public IList<ITexture> Textures { get { return textures.AsReadOnly(); } }
        public IList<IShader> Shaders { get { return shaders.AsReadOnly(); } }

        object renderActionsLock;
        private List<Action> RenderActions { get; set; }

        public Context(object nativeContext)
        {
            NativeContext = nativeContext;
            textures = new List<ITexture>();
            shaders = new List<IShader>();
            RenderActions = new List<Action>();
            renderActionsLock = new object();
            RenderThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            Trace.WriteLine("Created OpenGL render context on thread: " + RenderThreadId);
        }

        public ITexture CreateTexture(Vector2i dimensions, TextureFormat format, string name = null)
        {
            return CreateTexture(dimensions, format, IntPtr.Zero, name);
        }

        public ITexture CreateTexture(Vector2i dimensions, TextureFormat format, IntPtr imageData, string name = null)
        {
            var texture = new Texture(this, dimensions, format, imageData);
            textures.Add(texture);
            return texture;
        }

        public void DestroyTexture(ITexture texture)
        {
            textures.Remove(texture);
            texture.Dispose();
        }

        public IShader CreateShader(System.Reflection.Assembly assembly, string shaderResourceName, string name = null)
        {
            if (!Path.HasExtension(shaderResourceName))
                shaderResourceName += ".glsl";

            string[] resources = assembly.GetManifestResourceNames();
            foreach (string resource in resources) {
                if (resource.Contains(shaderResourceName))
                {
                    var stream = assembly.GetManifestResourceStream(resource);
                    var shader = new Shader(this, stream, name);
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
            TestGLRender(timeInterval);
        }

        double previousTime;
        double rotation;
        float[,] cube_vertices = new float[8, 3] {
            {-1, -1, 1},  // 2    0
			{1, -1, 1},   // 1    1
			{1, 1, 1},    // 0    2
			{-1, 1, 1},   // 3    3
			{-1, 1, -1},  // 7    4
			{1, 1, -1},   // 4    5
			{-1, -1, -1}, // 6    6
			{1, -1, -1}   // 5    7
					
		};
        float[,] cube_face_colors = new float[6, 3] {
            {0.4f, 1.0f, 0.4f}, // flora
			{0.0f, 0.0f, 1.0f}, // blueberry
			{0.4f, 0.8f, 1.0f}, // sky
			{1.0f, 0.8f, 0.4f}, // cantelopue
			{1.0f, 1.0f, 0.4f}, // blubble gum
			{0.5f, 0.0f, 0.25f}  // marron
		};
        int num_faces = 6;
        short[,] cube_faces = new short[6, 4] {
            {3, 0, 1, 2}, // +Z
			{0, 3, 4, 6}, // -X
			{2, 1, 7, 5}, // +X
			{3, 2, 5, 4}, // +Y
			{1, 0, 6, 7}, // -Y
			{5, 7, 6, 4}  // -Z
		};
        private void TestGLRender(double timeInterval)
        {
            //GL.Viewport(new Rectangle(0, 0, 300, 300));
            //GL.ClearColor (NSColor.Clear.UsingColorSpace (NSColorSpace.CalibratedRGB));
            //OpenTK.Graphics.OpenGL.Color4.Red;

            Context.CheckError();

            GL.ClearColor(0, 0, 1, 1);// Color.Red);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            /*
            GL.Enable(EnableCap.DepthTest);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);
            Context.CheckError();
            if (previousTime == 0)
                previousTime = timeInterval;
            rotation += 15.0 * (timeInterval - previousTime);
            GL.LoadIdentity();
            Context.CheckError();
            double comp = 1 / Math.Sqrt(3.0);
            GL.Rotate(rotation, comp, comp, comp);
            Context.CheckError();
            {
                long f, i;
                double fSize = 0.5;
                GL.Begin(BeginMode.Quads);
                for (f = 0; f < num_faces; f++)
                {
                    GL.Color3(cube_face_colors[f, 0], cube_face_colors[f, 1], cube_face_colors[f, 2]);
                    for (i = 0; i < 4; i++)
                    {
                        GL.Vertex3(cube_vertices[cube_faces[f, i], 0] * fSize, cube_vertices[cube_faces[f, i], 1] * fSize, cube_vertices[cube_faces[f, i], 2] * fSize);
                    }
                }

                GL.End();
                GL.Color3(0, 0, 0);//, 1);// Color.Black);

                for (f = 0; f < num_faces; f++)
                {
                    GL.Begin(BeginMode.LineLoop);
                    for (i = 0; i < 4; i++)
                        GL.Vertex3(cube_vertices[cube_faces[f, i], 0] * fSize, cube_vertices[cube_faces[f, i], 1] * fSize, cube_vertices[cube_faces[f, i], 2] * fSize);
                    GL.End();
                }
            }
            Context.CheckError();
            GL.Flush();
            previousTime = timeInterval;
            GL.Disable(EnableCap.DepthTest);
            */
            Context.CheckError();

            //GL.Hint (HintTarget.LineSmoothHint, HintMode.DontCare);
            //GL.Hint (HintTarget.PolygonSmoothHint, HintMode.DontCare);
        }

        public void Dispose()
        {
            foreach (var texture in textures)
                texture.Dispose();
            textures = null;

            lock (renderActionsLock)
            {
                RenderActions = null;
            }
            renderActionsLock = null;
        }
    }
}


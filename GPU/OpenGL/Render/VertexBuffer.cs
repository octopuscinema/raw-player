using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Octopus.Player.GPU.Render;
using OpenTK.Graphics.OpenGL;

namespace Octopus.Player.GPU.OpenGL.Render
{
    public class VertexBuffer : IVertexBuffer
    {
        public GPU.Render.BufferUsageHint UsageHint { get; private set; }

        public string Name { get; private set; }
        public bool Valid { get { return valid; } }
        public VertexFormat VertexFormat { get; private set; }
        public uint SizeBytes { get; private set; }
        int Handle { get; set; }
        private volatile bool valid;

        public VertexBuffer(Context context, VertexFormat vertexFormat, GPU.Render.BufferUsageHint usageHint, object vertexData, uint vertexCount)
        {
            Debug.Assert(vertexCount > 0, "Vertex buffer must be allocated with a non zero vertex count");

            UsageHint = usageHint;
            VertexFormat = vertexFormat;
            SizeBytes = VertexFormat.VertexSizeBytes * vertexCount;
#if __MACOS__
            int handle;
            GL.GenBuffers(1, out handle);
            Handle = handle;
#else
            Handle = GL.GenBuffer();
#endif
            GL.BindBuffer(BufferTarget.ArrayBuffer, Handle);
            var usageGL = usageHint == GPU.Render.BufferUsageHint.Static ? OpenTK.Graphics.OpenGL.BufferUsageHint.StaticDraw : OpenTK.Graphics.OpenGL.BufferUsageHint.DynamicDraw;

            System.Runtime.InteropServices.GCHandle vertexDataHandle = System.Runtime.InteropServices.GCHandle.Alloc(vertexData, System.Runtime.InteropServices.GCHandleType.Pinned);
#if __MACOS__
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)SizeBytes, vertexDataHandle.AddrOfPinnedObject(), usageGL);
#else
            GL.BufferData(BufferTarget.ArrayBuffer, (int)SizeBytes, vertexDataHandle.AddrOfPinnedObject(), usageGL);
#endif
            vertexDataHandle.Free();
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            valid = true;
            Context.CheckError();
        }

        static VertexAttribPointerType OpenGLVertexParameterType(VertexFormatParameter parameter)
        {
            switch (parameter)
            {
                case VertexFormatParameter.Position2f:
                case VertexFormatParameter.UV2f:
                    return VertexAttribPointerType.Float;
                default:
                    throw new Exception("");
            }
        }

        public void Bind()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, Handle);
            Context.CheckError();

            // Apply vertex format
            for(uint i = 0; i < VertexFormat.Parameters.Count; i++)
            {
                var parameter = VertexFormat.Parameters[(int)i];
                GL.EnableVertexAttribArray(i);
                GL.VertexAttribPointer((int)i, (int)parameter.ComponentCount, OpenGLVertexParameterType(parameter.Parameter), parameter.IsNormalised, (int)VertexFormat.VertexSizeBytes, (IntPtr)parameter.ByteOffset);
                Context.CheckError();
            }
        }

        public void Unbind()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            for (uint i = 0; i < VertexFormat.Parameters.Count; i++)
                GL.DisableVertexAttribArray(i);
            Context.CheckError();
        }

        public void Dispose()
        {
            Debug.Assert(valid, "Attempting to dispose invalid vertex buffer");
#if __MACOS__
            var handle = Handle;
            GL.DeleteBuffers(1, ref handle);
#else
            GL.DeleteBuffer(Handle);
#endif
            VertexFormat = null;
            SizeBytes = 0;
            valid = false;
        }
    }
}

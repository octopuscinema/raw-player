using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Octopus.Player.GPU.Render;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.OpenGL.Render
{
    public class Shader : IShader
    {
        public string Name { get; private set; }
        public bool Valid {  get { return valid; } }
        public int Program { get; private set; }

        private volatile bool valid;

        public Shader(Context context, Stream shaderSourceStream, VertexFormat vertexFormat, string name = null, string shaderVersion = "120")
        {
            Name = name;

            Action buildShaderAction = () =>
            {
                // Read source code from streams
                StreamReader shaderReader = new StreamReader(shaderSourceStream);
                var shaderSource = shaderReader.ReadToEnd();
                shaderReader.Dispose();
                shaderSourceStream.Dispose();

                // Create and compile vertex and fragment shader
                var vertexShader = GL.CreateShader(ShaderType.VertexShader);
                var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
                GL.ShaderSource(vertexShader, "#version " + shaderVersion + "\n#define VERT\n" + shaderSource);
                GL.ShaderSource(fragmentShader, "#version " + shaderVersion + "\n#define FRAG\n" + shaderSource);
                GL.CompileShader(vertexShader);
                GL.CompileShader(fragmentShader);
                Context.CheckError();

                // Check compile status
                int vertexShaderCompileStatus, fragmentShaderCompileStatus;
                GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out vertexShaderCompileStatus);
                GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out fragmentShaderCompileStatus);
                Context.CheckError();
                if (vertexShaderCompileStatus == 1 && fragmentShaderCompileStatus == 1)
                    Trace.WriteLine(Name != null ? "GLSL shader '" + Name + "' compiled successfully" : "GLSL shader compiled successfully");

                // Get shader log
                var vertexShaderLog = GL.GetShaderInfoLog(vertexShader);
                var fragmentShaderLog = GL.GetShaderInfoLog(fragmentShader);
                Trace.WriteLine(vertexShaderLog.Length > 1 ? vertexShaderLog : "No vertex shader log generated");
                Trace.WriteLine(fragmentShaderLog.Length > 1 ? fragmentShaderLog : "No fragment shader log generated");
                if (vertexShaderCompileStatus != 1 || fragmentShaderCompileStatus != 1)
                    throw new Exception(Name != null ? "GLSL shader '" + Name + "' failed to compile" : "GLSL shader failed to compile");

                // Create and link program
                Program = GL.CreateProgram();
                GL.AttachShader(Program, vertexShader);
                GL.AttachShader(Program, fragmentShader);
                GL.LinkProgram(Program);
                Context.CheckError();

                // Check link status
                int programLinkStatus;
#if __MACOS__
                GL.GetProgram(Program, ProgramParameter.LinkStatus, out programLinkStatus);
#else
                GL.GetProgram(Program, GetProgramParameterName.LinkStatus, out programLinkStatus);
#endif
                if (programLinkStatus != 1)
                    throw new Exception(Name != null ? "GLSL shader '" + Name + "' failed to link" : "GLSL shader failed to link");

                // Bind attribute locations
                for(int i = 0; i < vertexFormat.Parameters.Count; i++)
                    GL.BindAttribLocation(Program, i, vertexFormat.Parameters[i].ParameterName);

                // Tidy up unneeded stuff
                GL.DetachShader(Program, vertexShader);
                GL.DetachShader(Program, fragmentShader);
                GL.DeleteShader(vertexShader);
                GL.DeleteShader(fragmentShader);
                Context.CheckError();
                valid = true;
            };

            context.EnqueueRenderAction(buildShaderAction);
        }

        private int UniformLocation(string uniformName)
        {
            var location = GL.GetUniformLocation(Program, uniformName);
            Context.CheckError();
            if (location == -1)
                throw new Exception("Could not find shader uniform '" + uniformName + ((Name == null) ? "' in GLSL shader" : "' in GLSL shader: " + Name));
            return location;
        }

        public void SetUniform(string uniformName, int value)
        {
            GL.Uniform1(UniformLocation(uniformName), value);
            Context.CheckError();
        }

        public void SetUniform(string uniformName, float value)
        {
            GL.Uniform1(UniformLocation(uniformName), value);
            Context.CheckError();
        }

        public void SetUniform(string uniformName, Vector2 value)
        {
#if __MACOS__
            GL.Uniform2(UniformLocation(uniformName), value.X, value.Y);
#else
            GL.Uniform2(UniformLocation(uniformName), value);
#endif
            Context.CheckError();
        }
        public void SetUniform(string uniformName, Vector4 value)
        {
#if __MACOS__
            GL.Uniform4(UniformLocation(uniformName), value.X, value.Y, value.Z, value.W);
#else
            GL.Uniform4(UniformLocation(uniformName), value);
#endif
            Context.CheckError();
        }

        public void AttributeLocation(string attributeName)
        {
            //GL.GetAttribLocation(Program,)
            //GL.BindAttribLocation()
        }

        public void Bind()
        {
            Debug.Assert(valid, "Attempting to bind an invalid shader");
            GL.UseProgram(Program);
            Context.CheckError();
        }

        public void Unbind()
        {
            GL.UseProgram(0);
            Context.CheckError();
        }

        public void Dispose()
        {
            Debug.Assert(valid, "Attempting to dispose invalid shader");
#if __MACOS__
            GL.DeleteProgram(Program, null);
#else
            GL.DeleteProgram(Program);
#endif
            Context.CheckError();
            valid = false;
        }
    }
}

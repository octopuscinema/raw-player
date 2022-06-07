using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Octopus.Player.GPU.Render;
using OpenTK.Graphics.OpenGL;

namespace Octopus.Player.GPU.OpenGL.Render
{
    public class Shader : IShader
    {
        public string Name { get; private set; }
        public bool Valid { get; private set; }
        public int Program { get; private set; }
/*
        public Shader(Context context, string vertexShaderPath, string fragmentShaderPath, string name = null)
        {
            Name = name;

            // Load source
            if (!File.Exists(vertexShaderPath))
                throw new Exception("Invalid vertex shader path: " + vertexShaderPath);
            if (!File.Exists(fragmentShaderPath))
                throw new Exception("Invalid fragment shader path: " + fragmentShaderPath);
            var vertexShaderSrc = File.ReadAllText(vertexShaderPath);
            var fragmentShaderSrc = File.ReadAllText(fragmentShaderPath);

            // Create and compile vertex and fragment shader
            var vertexShader = GL.CreateShader(ShaderType.VertexShader);
            var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(vertexShader, vertexShaderSrc);
            GL.ShaderSource(fragmentShader, fragmentShaderSrc);
            GL.CompileShader(vertexShader);
            GL.CompileShader(fragmentShader);

            // Create and link program
            Program = GL.CreateProgram();
            GL.AttachShader(Program, vertexShader);
            GL.AttachShader(Program, fragmentShader);
            GL.LinkProgram(Program);
        }
*/
        public Shader(Context context, Stream vertexShaderSource, Stream fragmentShaderSource, string name = null)
        { 
            Name = name;

            // Read source code from streams
            StreamReader vertexShaderReader = new StreamReader(vertexShaderSource);
            StreamReader fragmentShaderReader = new StreamReader(fragmentShaderSource);

            // Create and compile vertex and fragment shader
            var vertexShader = GL.CreateShader(ShaderType.VertexShader);
            var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(vertexShader, vertexShaderReader.ReadToEnd());
            GL.ShaderSource(fragmentShader, fragmentShaderReader.ReadToEnd());
            GL.CompileShader(vertexShader);
            GL.CompileShader(fragmentShader);
            Context.CheckError();

            // Create and link program
            Program = GL.CreateProgram();
            GL.AttachShader(Program, vertexShader);
            GL.AttachShader(Program, fragmentShader);
            GL.LinkProgram(Program);
            Context.CheckError();

            // Tidy up unneeded stuff
            GL.DetachShader(Program, vertexShader);
            GL.DetachShader(Program, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
            Context.CheckError();
            Valid = true;
        }

        public Shader(Context context, Stream shaderSourceStream, string name = null)
        {
            Name = name;

            // Read source code from streams
            StreamReader shaderReader = new StreamReader(shaderSourceStream);
            var shaderSource = shaderReader.ReadToEnd();
            shaderReader.Dispose();
            shaderSourceStream.Dispose();

            // Create and compile vertex and fragment shader
            var vertexShader = GL.CreateShader(ShaderType.VertexShader);
            var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(vertexShader, "#define VERT\n" + shaderSource);
            GL.ShaderSource(fragmentShader, "#define FRAG\n" + shaderSource);
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

            // Tidy up unneeded stuff
            GL.DetachShader(Program, vertexShader);
            GL.DetachShader(Program, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
            Context.CheckError();
            Valid = true;
        }

        public void Bind()
        {
            Debug.Assert(Valid, "Attempting to bind an invalid shader");
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
            Debug.Assert(Valid, "Attempting to dispose invalid shader");
#if __MACOS__
            GL.DeleteProgram(Program, null);
#else
            GL.DeleteProgram(Program);
#endif
            Context.CheckError();
            Valid = false;
        }
    }
}

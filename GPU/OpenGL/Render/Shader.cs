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
        public IReadOnlyList<string> Defines { get; private set; }

        private volatile bool valid;

        public Shader(Context context, System.Reflection.Assembly assembly, string resourceName, VertexFormat vertexFormat, string name = null, IList<string> defines = null, string shaderVersion = "330")
        {
            Name = name;
            Defines = new List<string>(defines);

            Action buildShaderAction = () =>
            {
                // Read source code from streams
                var shaderSourceStream = assembly.GetManifestResourceStream(resourceName);
                StreamReader shaderReader = new StreamReader(shaderSourceStream);
                var shaderSource = Preprocess(shaderReader.ReadToEnd(), assembly);
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

        string AddDefines(string source)
        {
            string defineBlock = "";
            foreach(var define in Defines)
                defineBlock += "#define " + define + "\n";
            return defineBlock + source;
        }

        string AddIncludes(string source, System.Reflection.Assembly assembly, string[] localResources, ref uint depth)
        {
            depth++;
            if (depth > 16)
                throw new Exception("Shader include depth maximum of 16 reached, check for cycling header dependancy");

            const string includeToken = "#include ";

            // Find lines which start '#include'
            var includeLines = new List<string>();
            using (StringReader reader = new StringReader(source))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if ( line.StartsWith(includeToken))
                        includeLines.Add(line.Replace("\n", "").Replace("\r", ""));
                }
            }

            // Apply the #includes
            foreach(var includeLine in includeLines)
            {
                // Extract the filename and check its in the resource list
                var filename = includeLine.Replace(includeToken, "").Replace("\"","");
                var includeResource = Array.Find(localResources, element => element.EndsWith(filename));
                if ( string.IsNullOrEmpty(includeResource) )
                    throw new Exception("Could not find shader include resource: '" + filename + "'");

                // Load the include resource and recursively add includes
                var includeSourceStream = assembly.GetManifestResourceStream(includeResource);
                StreamReader includeReader = new StreamReader(includeSourceStream);
                var includeSource = AddIncludes(includeReader.ReadToEnd(), assembly, localResources, ref depth);
                includeReader.Dispose();
                includeSourceStream.Dispose();

                // Finally replace the include line with the include source
                source = source.Replace(includeLine, includeSource);
            }

            return source;
        }

        string Preprocess(string source, System.Reflection.Assembly assembly)
        {
            uint includeDepth = 0;
            var localResources = assembly.GetManifestResourceNames();
            return AddIncludes(AddDefines(source), assembly, localResources, ref includeDepth);
        }
        
        private int UniformLocation(string uniformName)
        {
            var location = GL.GetUniformLocation(Program, uniformName);
            Context.CheckError();
            if (location == -1)
                throw new Exception("Could not find shader uniform '" + uniformName + ((Name == null) ? "' in GLSL shader" : "' in GLSL shader: " + Name));
            return location;
        }

        public void SetUniform(IContext context, string uniformName, int value)
        {
            ((Context)context).SetShader(this);
            GL.Uniform1(UniformLocation(uniformName), value);
            Context.CheckError();
        }

        public void SetUniform(IContext context, string uniformName, float value)
        {
            ((Context)context).SetShader(this);
            GL.Uniform1(UniformLocation(uniformName), value);
            Context.CheckError();
        }

        public void SetUniform(IContext context, string uniformName, Vector2 value)
        {
            ((Context)context).SetShader(this);
#if __MACOS__
            GL.Uniform2(UniformLocation(uniformName), value.X, value.Y);
#else
            GL.Uniform2(UniformLocation(uniformName), value);
#endif
            Context.CheckError();
        }
        public void SetUniform(IContext context, string uniformName, Vector4 value)
        {
            ((Context)context).SetShader(this);
#if __MACOS__
            GL.Uniform4(UniformLocation(uniformName), value.X, value.Y, value.Z, value.W);
#else
            GL.Uniform4(UniformLocation(uniformName), value);
#endif
            Context.CheckError();
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

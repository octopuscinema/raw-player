﻿using Octopus.Player.GPU.Compute;
using OpenTK.Mathematics;
using Silk.NET.Core.Native;
using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Octopus.Player.GPU.OpenCL.Compute
{
    internal class Program : IProgram
    {
        public string Name { get; private set; }

        public IReadOnlyList<string> Functions { get; private set; }

        public IReadOnlyCollection<string> Defines { get; private set; }

        internal nint NativeHandle { get; private set; }

        private IDictionary<string, nint> Kernels {get; set;}

        private Context Context { get; set; }

        internal Program(Context context, Assembly assembly, string resourceName, IReadOnlyList<string> functions, IReadOnlyCollection<string> defines = null, string name = null)
        {
            Name = name!=null ? name : resourceName;
            Functions = functions;
            Context = context;
            Defines = defines;

            // Load and preprocess source
            var sourceStream = assembly.GetManifestResourceStream(resourceName);
            StreamReader reader = new StreamReader(sourceStream);
            string[] source = new string[] { Preprocess(reader.ReadToEnd(), assembly, defines) };
            reader.Dispose();
            sourceStream.Dispose();

            // Create program
            int result;
            nuint stringLengths = (nuint)source[0].Length;
            NativeHandle = context.Handle.CreateProgramWithSource(context.NativeHandle, 1, source, stringLengths, out result);
            Debug.CheckError(result);

            // Build program
            nint[] devices = new nint[] { context.NativeDevice };
            string buildOptions = "";
            string buildLog = null;
            ErrorCodes buildResult;
            unsafe
            {
                buildResult = (ErrorCodes)context.Handle.BuildProgram(NativeHandle, (uint)devices.Length, devices.AsSpan(), buildOptions, null, null);

                // Get build log size
                nuint buildLogSize;
                Debug.CheckError(context.Handle.GetProgramBuildInfo(NativeHandle, context.NativeDevice, ProgramBuildInfo.BuildLog, 0, null, out buildLogSize));

                // Get build log
                if (buildLogSize > 2) {
                    byte[] buildLogData = new byte[buildLogSize];
                    fixed (byte* p = buildLogData)
                    {
                        Debug.CheckError(context.Handle.GetProgramBuildInfo(NativeHandle, context.NativeDevice, ProgramBuildInfo.BuildLog, buildLogSize, p, null));
                    }
                    buildLog = System.Text.Encoding.ASCII.GetString(buildLogData);
                }
            }

            // Build feedback
            if (buildLog != null && buildLog.Length > 0)
                Trace.WriteLine(buildResult == ErrorCodes.BuildProgramFailure ? "OpenCL program '" + Name + "' failed to build:\n" + buildLog
                   : "OpenCL program '" + Name + "' built:\n" + buildLog);
            else
                Trace.WriteLine(buildResult == ErrorCodes.BuildProgramFailure ? "OpenCL program '" + Name + "' failed to build"
                    : "OpenCL program '" + Name + "' built");
            Debug.CheckError((int)buildResult);

            // Create kernel for each function
            Kernels = new Dictionary<string, nint>();
            foreach (var function in Functions)
            {
                Kernels[function] = context.Handle.CreateKernel(NativeHandle, function, out result);
                Debug.CheckError(result);
            }
        }

        string Preprocess(string source, Assembly assembly, IReadOnlyCollection<string> defines)
        {
            uint includeDepth = 0;
            var localResources = assembly.GetManifestResourceNames();
            return AddIncludes(defines == null ? source : AddDefines(source, defines), assembly, localResources, ref includeDepth);
        }

        string AddDefines(string source, IReadOnlyCollection<string> defines)
        {
            string defineBlock = "";
            foreach (var define in defines)
                defineBlock += "#define " + define + "\n";
            return defineBlock + source;
        }

        string AddIncludes(string source, Assembly assembly, string[] localResources, ref uint depth)
        {
            depth++;
            if (depth > 16)
                throw new Exception("OpenCL include depth maximum of 16 reached, check for cycling header dependancy");

            const string includeToken = "#include ";

            // Find lines which start '#include'
            var includeLines = new List<string>();
            using (StringReader reader = new StringReader(source))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith(includeToken))
                        includeLines.Add(line.Replace("\n", "").Replace("\r", ""));
                }
            }

            // Apply the #includes
            foreach (var includeLine in includeLines)
            {
                // Extract the filename and check its in the resource list
                var filename = includeLine.Replace(includeToken, "").Replace("\"", "");
                var includeResource = Array.Find(localResources, element => element.EndsWith(filename));
                if (string.IsNullOrEmpty(includeResource))
                    throw new Exception("Could not find OpenCL include resource: '" + filename + "'");

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

        public void SetArgument(string function, uint index, float value)
        {
            Context.Handle.SetKernelArg(Kernels[function], index, sizeof(float), value);
        }

        public void SetArgument(string function, uint index, in Vector2 value)
        {
            Context.Handle.SetKernelArg(Kernels[function], index, (nuint)Vector2.SizeInBytes, value);
        }

        public void SetArgument(string function, uint index, in Matrix4 value)
        {
            Context.Handle.SetKernelArg(Kernels[function], index, (nuint)Vector4.SizeInBytes * 4, value);
        }

        public void SetArgument(string function, uint index, IBuffer buffer)
        {
            unsafe
            {
                switch (buffer)
                {
                    case Image image:
                        Context.Handle.SetKernelArg(Kernels[function], index, (nuint)sizeof(nint), image.NativeHandle);
                        break;
                    default:
                        break;
                }
            }
        }

        public void Dispose()
        {
            foreach (var kernel in Kernels)
                Debug.CheckError(Context.Handle.ReleaseKernel(kernel.Value));
            Kernels.Clear();

            Debug.CheckError(Context.Handle.ReleaseProgram(NativeHandle));
            NativeHandle = 0;
        }
    }
}

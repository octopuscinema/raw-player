using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenTK.Mathematics;
using Octopus.Player.GPU;

namespace Octopus.Player.Core.IO.LUT
{
    public sealed class LUT3D : ILUT3D
    {
        public string Title { get; private set; }
        public Vector3i Dimensions { get; private set; }
        public GPU.Compute.IImage ComputeImage { get; private set; }
        public GPU.Format Format { get { return ComputeImage.Format; } }

        public LUT3D(GPU.Compute.IContext computeContext, string filePath, GPU.Format format = GPU.Format.RGBA8)
        {
            var lines = File.ReadLines(filePath);
            Upload(computeContext, Parse(lines).ToArray(), format);
        }

        public LUT3D(GPU.Compute.IContext computeContext, Assembly assembly, string resourceName, GPU.Format format = GPU.Format.RGBA8)
        {
            var sourceStream = assembly.GetManifestResourceStream(resourceName);
            var reader = new StreamReader(sourceStream);

            IList<byte> data = new List<byte>();

            var line = reader.ReadLine();
            while (line != null)
            {
                Parse(line, data);
                line = reader.ReadLine();
            }

            reader.Dispose();
            sourceStream.Dispose();

            Upload(computeContext, data.ToArray(), format);
        }

        public void Dispose()
        {
            if (ComputeImage != null)
                ComputeImage.Dispose();
        }

        void Parse(string line, IList<byte> data)
        {
            // Clean the input
            var clean = line.Trim();
            if (clean.Length == 0 || clean[0] == '#')
                return;

            // Split into tokens
            var tokens = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return;

            switch(tokens[0].Trim())
            {
                case "LUT_3D_SIZE":
                case "3DLUTSIZE":
                    if (tokens.Length < 2 || !int.TryParse(tokens[1], out int value))
                        throw new Exception("Error parsing LUT size");
                    Dimensions = new Vector3i(value, value, value);
                    break;

                case "LUT_3D_INPUT_RANGE":
                    break;

                case "TITLE":
                case "BMD_TITLE":
                    Title = clean.Remove(0, tokens[0].Trim().Length);
                    break;

                case "LUT_IN_VIDEO_RANGE":
                case "LUT_OUT_VIDEO_RANGE":
                    break;

                default:
                    if ( tokens.Length == 3 )
                    {
                        // Attempt to parse 3 float values
                        float[] rgb = new float[3];
                        if (float.TryParse(tokens[0], out rgb[0]) &&
                            float.TryParse(tokens[1], out rgb[1]) &&
                            float.TryParse(tokens[2], out rgb[2]))
                        {
                            foreach (var channel in rgb)
                                data.Add((byte)Math.Clamp(channel * 255.0f, 0.0f, 255.0f));
                            data.Add(0);
                        }
                    }
                    break;
            }
        }

        IList<byte> Parse(IEnumerable<string> lines)
        {
            IList<byte> data = new List<byte>();

            foreach (var line in lines)
                Parse(line, data);

            return data;
        }

        void Upload(GPU.Compute.IContext computeContext, byte[] data, GPU.Format format)
        {
            // Check input size
            var expectedSizeBytes = format.SizeBytes() * Dimensions.X * Dimensions.Y *Dimensions.Z;
            if (expectedSizeBytes != data.Length)
                throw new Exception("Unexpected LUT size");
            
            if (ComputeImage != null)
                ComputeImage.Dispose();

            ComputeImage = computeContext.CreateImage(Dimensions, format, GPU.Compute.MemoryDeviceAccess.ReadOnly,
                GPU.Compute.MemoryHostAccess.NoAccess, data, GPU.Compute.MemoryLocation.Default, "lut");
        }
    }
}
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

        public LUT3D(GPU.Compute.IContext computeContext, string filePath, Format format = Format.RGBA8)
        {
            var lines = File.ReadLines(filePath);
            switch (format)
            {
                case GPU.Format.RGBA8:
                    Upload(computeContext, Parse<byte>(lines).ToArray(), format);
                    break;
                case GPU.Format.RGBA16:
                    Upload(computeContext, Parse<ushort>(lines).ToArray(), format);
                    break;
                default:
                    throw new Exception("Unsupported LUT format");
            }
        }

        public LUT3D(GPU.Compute.IContext computeContext, Assembly assembly, string resourceName, GPU.Format format = GPU.Format.RGBA8)
        {
            using (var sourceStream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(sourceStream))
            {
                switch (format)
                {
                    case GPU.Format.RGBA8:
                        var data = new List<byte>();
                        var line = reader.ReadLine();
                        while (line != null)
                        {
                            Parse(line, data);
                            line = reader.ReadLine();
                        }
                        Upload(computeContext, data.ToArray(), format);
                        break;
                    default:
                        throw new Exception("Unsupported LUT format");
                }
            }
        }

        public void Dispose()
        {
            if (ComputeImage != null)
            {
                ComputeImage.Dispose();
                ComputeImage = null;
            }
        }

        void Parse<T>(string line, IList<T> data)
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
                            switch(data)
                            {
                                case IList<byte> dataByte:
                                    foreach (var channel in rgb)
                                        dataByte.Add((byte)Math.Clamp(channel * 255.0f, 0.0f, 255.0f));
                                    dataByte.Add(0);
                                    break;
                                case IList<ushort> dataUshort:
                                    foreach (var channel in rgb)
                                        dataUshort.Add((ushort)Math.Clamp(channel * 65535.0f, 0.0f, 65535.0f));
                                    dataUshort.Add(0);
                                    break;
                                default:
                                    throw new Exception("Unsupported LUT format");
                            }
                        }
                    }
                    break;
            }
        }

        IList<T> Parse<T>(IEnumerable<string> lines)
        {
            IList<T> data = new List<T>();

            foreach (var line in lines)
                Parse(line, data);

            return data;
        }

        void Upload<T>(GPU.Compute.IContext computeContext, T[] data, GPU.Format format)
        {
            // Get input size
            var expectedSizeBytes = format.SizeBytes() * Dimensions.X * Dimensions.Y *Dimensions.Z;
            
            if (ComputeImage != null)
                ComputeImage.Dispose();

            switch (data)
            {
                case byte[] dataByte:
                    if (expectedSizeBytes != data.Length * sizeof(byte))
                        throw new Exception("Unexpected LUT size");
                    ComputeImage = computeContext.CreateImage(Dimensions, format, GPU.Compute.MemoryDeviceAccess.ReadOnly,
                        GPU.Compute.MemoryHostAccess.NoAccess, dataByte, GPU.Compute.MemoryLocation.Default, "lut");
                    break;

                case ushort[] dataUshort:
                    if (expectedSizeBytes != data.Length * sizeof(ushort))
                        throw new Exception("Unexpected LUT size");

                    byte[] dataRaw = new byte[dataUshort.Length * sizeof(ushort)];
                    Buffer.BlockCopy(dataUshort, 0, dataRaw, 0, dataRaw.Length);

                    ComputeImage = computeContext.CreateImage(Dimensions, format, GPU.Compute.MemoryDeviceAccess.ReadOnly,
                        GPU.Compute.MemoryHostAccess.NoAccess, dataRaw, GPU.Compute.MemoryLocation.Default, "lut");
                    break;

                default:
                    throw new Exception("Unsupported LUT format");
            }
        }
    }
}
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Octopus.Player.GPU.Compute
{
    public enum Api
    {
        OpenCL,
        Metal,
        CUDA
    }

    public interface IContext : IDisposable
    {
        Api Api { get; }

        string ApiVersion { get; }
        string ApiName { get; }
        string ApiVendor { get; }

        IProgram CreateProgram(System.Reflection.Assembly assembly, string resourceName, IReadOnlyList<string> functions, IReadOnlyCollection<string> defines = null, string name = null);
        IImage2D CreateImage(Vector2i dimensions, GPU.Format format, MemoryDeviceAccess memoryDeviceAccess, MemoryHostAccess memoryHostAccess,
            MemoryLocation memoryLocation = MemoryLocation.Default, string name = null);
        IQueue CreateQueue(string name = null);
    }
}


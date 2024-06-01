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

        bool ApiSupportsFp16 { get; }
        Vector3i ApiMaxImageDimensions3D { get; }

        IQueue DefaultQueue { get; }

        IProgram CreateProgram(System.Reflection.Assembly assembly, string resourceName, IReadOnlyList<string> functions, IReadOnlyCollection<string> defines = null, string name = null);

        IImage1D CreateImage(int dimensions, Format format, MemoryDeviceAccess memoryDeviceAccess, MemoryHostAccess memoryHostAccess, byte[] imageData,
            MemoryLocation memoryLocation = MemoryLocation.Default, string name = null);

        IImage2D CreateImage(in Vector2i dimensions, Format format, MemoryDeviceAccess memoryDeviceAccess, MemoryHostAccess memoryHostAccess,
            MemoryLocation memoryLocation = MemoryLocation.Default, string name = null);

        IImage3D CreateImage(in Vector3i dimensions, Format format, MemoryDeviceAccess memoryDeviceAccess, MemoryHostAccess memoryHostAccess, byte[] imageData,
            MemoryLocation memoryLocation = MemoryLocation.Default, string name = null);

        IImage2D CreateImage(Render.IContext renderContext, Render.ITexture texture, MemoryDeviceAccess memoryDeviceAccess);

        IQueue CreateQueue(string name = null);
    }
}


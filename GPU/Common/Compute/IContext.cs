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

        IProgram CreateProgram(System.Reflection.Assembly assembly, string resourceName, string name = null, IList<string> defines = null);
        IImage CreateImage(Vector2i dimensions, ImageFormat format, string name = null);
    }
}


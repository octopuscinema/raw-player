using System;

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
    }
}


using System;

namespace Octopus.Player.GPU.Compute
{
    public enum Api
    {
        OpenCL,
        Metal,
        CUDA
    }

    public interface IContext
    {
        Api Api { get; }
    }
}


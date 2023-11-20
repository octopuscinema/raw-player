using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Compute
{
    public enum ImageFormat
    {
        R16,
        RGBA8888
    }

    public interface IImage : IDisposable
    {
        ImageFormat Format { get; }
    }

    public interface IImage2D : IImage
    {
        Vector2i Dimensions { get; }
    }
}

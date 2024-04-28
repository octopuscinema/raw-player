using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Compute
{
    public interface IImage : IBuffer
    {
        Format Format { get; }
        int SizeBytes { get; }
    }

    public interface IImage1D : IImage
    {
        int Dimensions { get; }
    }

    public interface IImage2D : IImage
    {
        Vector2i Dimensions { get; }
    }

    public interface IImage3D : IImage
    {
        Vector3i Dimensions { get; }
    }
}

using Octopus.Player.GPU.Compute;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.OpenCL.Compute
{
    internal class Image2D : IImage2D
    {
        public ImageFormat Format { get; private set; }

        public Vector2i Dimensions { get; private set; }

        internal Image2D(Vector2i dimensions, ImageFormat format)
        {
            Dimensions = dimensions;
            Format = format;
        }

        public void Dispose()
        {
            
        }
    }
}

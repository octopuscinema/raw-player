using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Render
{
    public interface IVertexBuffer : IBuffer
    {
        VertexFormat VertexFormat { get; }
        uint SizeBytes { get; }
    }
}

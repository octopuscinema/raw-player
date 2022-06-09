using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Render
{
    public enum BufferUsageHint
    {
        Static,
        Dynamic
    }

    public interface IBuffer : IDisposable
    {
        BufferUsageHint UsageHint { get; }
        string Name { get; }
        bool Valid { get; }
    }
}

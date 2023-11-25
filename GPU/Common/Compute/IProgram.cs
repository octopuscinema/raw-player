using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Compute
{
    public interface IProgram : IDisposable
    {
        string Name { get; }
        IReadOnlyList<string> Defines { get; }
        IReadOnlyList<string> Functions { get; }
    }
}

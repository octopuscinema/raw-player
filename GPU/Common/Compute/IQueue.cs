using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Compute
{
    public interface IQueue : IDisposable
    {
        string Name { get; }
    }
}

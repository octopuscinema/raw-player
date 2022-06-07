using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Render
{
    public interface IShader : IDisposable
    {
        string Name { get; }
        bool Valid { get; }
    }
}

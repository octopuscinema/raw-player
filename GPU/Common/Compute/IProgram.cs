using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.Compute
{
    public interface IProgram : IDisposable
    {
        string Name { get; }
        IReadOnlyCollection<string> Defines { get; }
        IReadOnlyList<string> Functions { get; }

        void SetArgument(string function, uint index, float value);
        void SetArgument(string function, uint index, in Vector2 value);
        void SetArgument(string function, uint index, in Matrix4 value);
        void SetArgument(string function, uint index, IBuffer buffer);
    }
}

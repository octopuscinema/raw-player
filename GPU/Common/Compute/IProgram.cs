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

        void Run2D(IQueue queue, string function, in Vector2i dimensions, in Vector2i? offset = null);

        void SetArgument(string function, uint index, float value);
        void SetArgument(string function, uint index, int value);
        void SetArgument(string function, uint index, in Vector2 value);
        void SetArgument(string function, uint index, in Vector3 value);
        void SetArgument(string function, uint index, in Matrix3 value);
        void SetArgument(string function, uint index, in Matrix4 value);
        void SetArgument(string function, uint index, IBuffer buffer);
    }
}

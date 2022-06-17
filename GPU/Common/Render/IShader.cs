using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.Render
{
    public interface IShader : IDisposable
    {
        string Name { get; }
        bool Valid { get; }
        void SetUniform(string uniformName, int value);
        void SetUniform(string uniformName, float value);
        void SetUniform(string uniformName, Vector2 value);
        void SetUniform(string uniformName, Vector4 value);
    }
}

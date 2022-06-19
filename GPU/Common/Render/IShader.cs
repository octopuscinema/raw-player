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
        void SetUniform(IContext context, string uniformName, int value);
        void SetUniform(IContext context, string uniformName, float value);
        void SetUniform(IContext context, string uniformName, Vector2 value);
        void SetUniform(IContext context, string uniformName, Vector4 value);
    }
}

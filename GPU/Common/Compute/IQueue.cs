using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.Compute
{
    public interface IQueue : IDisposable
    {
        string Name { get; }

        void ModifyImage(IImage2D image, Vector2i origin, Vector2i size, byte[] imageData, uint imageDataOffset = 0);

        void AcquireTextureObject(GPU.Render.IContext renderContext, IImage image);
        void ReleaseTextureObject(IImage image);

        void WaitForComplete();
        void AsyncWaitForComplete();
        void Flush();
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using OpenTK.Mathematics;

namespace Octopus.Player.GPU.Compute
{
    public interface IQueue : IDisposable
    {
        string Name { get; }

        void ModifyImage(IImage2D image, Vector2i origin, Vector2i size, byte[] imageData, uint imageDataOffset = 0);

        void AcquireTextureObject(Render.ITexture texture);
        void ReleaseTextureObject(Render.ITexture texture);

        void WaitForComplete();
        void AsyncWaitForComplete();
        void Flush();
    }
}

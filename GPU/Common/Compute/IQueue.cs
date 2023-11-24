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

        unsafe byte* MapImage(IImage2D image, Vector2i regionOrigin, Vector2i regionSize);
        unsafe void UnmapImage(IImage2D image, byte* mappedRegion);

        void WaitForComplete();
        void AsyncWaitForComplete();
        void Flush();
    }
}

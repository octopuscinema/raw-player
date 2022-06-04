using System;

namespace Octopus.Player.GPU.Render
{
    public interface IContext
    {
        ITexture CreateTexture();
        void DestroyTexture(ITexture texture);
    }
}


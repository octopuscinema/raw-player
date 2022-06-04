using System;
using System.Collections.Generic;
using Octopus.Player.GPU.Render;
using OpenTK.Graphics.OpenGL;

namespace Octopus.Player.GPU.OpenGL
{
    public class Context : IContext
    {
        public List<ITexture> Textures { get; private set; }

        public Context()
        {
            Textures = new List<ITexture>();
        }

        public ITexture CreateTexture()
        {
            return null;
        }

        public void DestroyTexture(ITexture texture)
        {
            Textures.Remove(texture);
            texture.Dispose();
        }

        public static void CheckError()
        {
#if DEBUG
            var lastErrorCode = GL.GetError();
            if (lastErrorCode != ErrorCode.NoError)
                throw new Exception("OpenGL Error: " + lastErrorCode.ToString());
#endif
        }
    }
}


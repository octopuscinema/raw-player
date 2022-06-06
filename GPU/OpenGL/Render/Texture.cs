using System;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace Octopus.Player.GPU.OpenGL.Render
{
	public sealed class Texture : ITexture
	{
        public bool Valid { get; private set; }
        int Handle { get; set; }

        public Texture(Context context, Vector2i dimensions, TextureFormat format)
            : this(context, dimensions, format, IntPtr.Zero)
        {
        }

        public Texture(Context context, Vector2i dimensions, TextureFormat format, IntPtr imageData)
		{
            Dimensions = dimensions;
            Format = format;

            // Create the texture with blank data
            // TODO: set mipmap/filter and clamp options
            Action createTextureAction = () =>
            {
                Handle = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, Handle);
                GL.TexImage2D(TextureTarget.Texture2D, 0, GLPixelInternalFormat(format), Dimensions.X, Dimensions.Y, 0, GLPixelFormat(format), GLPixelType(format), imageData);
                Context.CheckError();
                Valid = true;
            };

            // Support creation of OpenGL texture outside of render thread but warn about it
            if (context.RenderThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId)
            {
                Console.WriteLine("Creating OpenGL texture asyncrhonosly from outside of render thread");
                context.EnqueueRenderAction(createTextureAction);
            }
            else
                createTextureAction();
        }

        public Vector2i Dimensions { get; private set; }

        public TextureFormat Format { get; private set; }

        public void Dispose()
        {
            Debug.Assert(Valid,"Attempting to dispose invalid texture");
            GL.DeleteTexture(Handle);
            Valid = false;
        }

        public void Modify(Vector2i dimensions, TextureFormat format, IntPtr imageData, uint dataSizeBytes)
        {
            Debug.Assert(dimensions == Dimensions && format == Format, "Modify does not support dimension or format changes");

            Vector2i offset = new Vector2i(0, 0);
            Vector2i size = dimensions;
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, offset.X, offset.Y, size.X, size.Y, GLPixelFormat(format), GLPixelType(format), imageData);
            Context.CheckError();
        }

        private static PixelFormat GLPixelFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGB8:
                case TextureFormat.RGB16:
                    return PixelFormat.Rgb;
                case TextureFormat.RGBA8:
                case TextureFormat.RGBX8:
                case TextureFormat.RGBA16:
                case TextureFormat.RGBX16:
                    return PixelFormat.Rgba;
                case TextureFormat.R16:
                    return PixelFormat.Red;
                default:
                    throw new Exception("Unhandled texture format: " + format.ToString());
            }
        }

        private static PixelType GLPixelType(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGBA8:
                case TextureFormat.RGBX8:
                case TextureFormat.RGB8:
                    return PixelType.UnsignedByte;
                case TextureFormat.RGBA16:
                case TextureFormat.RGBX16:
                case TextureFormat.RGB16:
                case TextureFormat.R16:
                    return PixelType.UnsignedShort;
                default:
                    throw new Exception("Unhandled texture format: " + format.ToString());
            }
        }

        private static PixelInternalFormat GLPixelInternalFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGBA8:
                case TextureFormat.RGBX8:
                    return PixelInternalFormat.Rgba8;
                case TextureFormat.RGB8:
                    return PixelInternalFormat.Rgb8;
                case TextureFormat.RGBA16:
                case TextureFormat.RGBX16:
                    return PixelInternalFormat.Rgba16;
                case TextureFormat.RGB16:
                    return PixelInternalFormat.Rgb16;
                case TextureFormat.R16:
                    return PixelInternalFormat.R16;
                default:
                    throw new Exception("Unhandled texture format: " + format.ToString());
            }
        }
    }
}


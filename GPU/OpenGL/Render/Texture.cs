﻿using System;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Octopus.Player.GPU.OpenGL.Render
{
	public sealed class Texture : ITexture
	{
        public string Name { get; private set; }
        public bool Valid { get { return valid; } }
        int Handle { get; set; }
        private volatile bool valid;
        private Context Context { get; set; }
        private TextureTarget TextureType { get; set; }

        public Texture(Context context, Vector2i dimensions, TextureFormat format, TextureFilter filter = TextureFilter.Nearest, string name = null)
            : this(context, dimensions, format, null, filter, name)
        {
        }

        public Texture(Context context, Vector2i dimensions, TextureFormat format, byte[] imageData, TextureFilter filter = TextureFilter.Nearest, string name = null)
		{
            Name = name;
            Dimensions = dimensions;
            Format = format;
            Context = context;
            Filter = filter;
            TextureType = TextureTarget.Texture2D;

            Action createTextureAction = () =>
            {
                Handle = GL.GenTexture();
                GL.BindTexture(TextureType, Handle);
                GL.TexImage2D(TextureType, 0, GLPixelInternalFormat(format), Dimensions.X, Dimensions.Y, 0, GLPixelFormat(format), GLPixelType(format), imageData);
                GL.TexParameter(TextureType, TextureParameterName.TextureMinFilter, (int)GLTextureMinFilter(Filter));
                GL.TexParameter(TextureType, TextureParameterName.TextureMagFilter, (int)GLTextureMagFilter(Filter));
                GL.TexParameter(TextureType, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureType, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                Context.CheckError();
                valid = true;
            };

            context.EnqueueRenderAction(createTextureAction);
        }

        public Texture(Context context, uint size, TextureFormat format, byte[] imageData, TextureFilter filter = TextureFilter.Linear, string name = null)
        {
            Name = name;
            Dimensions = new Vector2i((int)size,1);
            Format = format;
            Context = context;
            Filter = filter;
            TextureType = TextureTarget.Texture1D;

            Action createTextureAction = () =>
            {
                Handle = GL.GenTexture();
                GL.BindTexture(TextureType, Handle);
                GL.TexImage1D(TextureType, 0, GLPixelInternalFormat(format), Dimensions.X, 0, GLPixelFormat(format), GLPixelType(format), imageData);
                GL.TexParameter(TextureType, TextureParameterName.TextureMinFilter, (int)GLTextureMinFilter(Filter));
                GL.TexParameter(TextureType, TextureParameterName.TextureMagFilter, (int)GLTextureMagFilter(Filter));
                GL.TexParameter(TextureType, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                Context.CheckError();
                valid = true;
            };

            context.EnqueueRenderAction(createTextureAction);
        }

        public Vector2i Dimensions { get; private set; }

        public TextureFormat Format { get; private set; }

        public TextureFilter Filter { get; private set; }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            Debug.Assert(valid, "Attempting to bind an invalid texture");
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureType, Handle);
            Context.CheckError();
        }

        public void Unbind(TextureUnit unit = TextureUnit.Texture0)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureType, 0);
            Context.CheckError();
        }

        public void Dispose()
        {
            Action deleteTextureAction = () =>
            {
                Debug.Assert(Valid,"Attempting to dispose invalid texture");
                GL.DeleteTexture(Handle);
                Context.CheckError();
                valid = false;
            };
            Context.EnqueueRenderAction(deleteTextureAction);
        }

        public void Modify(IContext context, Vector2i origin, Vector2i size, byte[] imageData, uint imageDataOffset = 0)
        {
            Debug.Assert(valid, "Attempting to modify invalid texture");
            Debug.Assert(size.X <= Dimensions.X && size.Y <= Dimensions.Y, "Size cannot be larger than texture dimensions");
            Debug.Assert(origin.X <= Dimensions.X && origin.Y <= Dimensions.Y, "Origin cannot be larger than texture dimensions");

            var defaultTextureUnit = TextureUnit.Texture0;
            ((Context)context).SetTexture(this, defaultTextureUnit);
            if (((Context)Context).ActiveTextureUnit != defaultTextureUnit)
                ((Context)Context).SetActiveTextureUnit(defaultTextureUnit);

            if (imageDataOffset == 0)
            {
                if (TextureType == TextureTarget.Texture1D)
                    GL.TexSubImage1D(TextureType, 0, origin.X, size.X, GLPixelFormat(Format), GLPixelType(Format), imageData);
                else
                    GL.TexSubImage2D(TextureType, 0, origin.X, origin.Y, size.X, size.Y, GLPixelFormat(Format), GLPixelType(Format), imageData);
            }
            else
            {
                GCHandle pinnedImageData = GCHandle.Alloc(imageData, GCHandleType.Pinned);
                try
                {
                    if (TextureType == TextureTarget.Texture1D)
                        GL.TexSubImage1D(TextureType, 0, origin.X, size.X, GLPixelFormat(Format), GLPixelType(Format), IntPtr.Add(pinnedImageData.AddrOfPinnedObject(), (int)imageDataOffset));
                    else
                        GL.TexSubImage2D(TextureType, 0, origin.X, origin.Y, size.X, size.Y, GLPixelFormat(Format), GLPixelType(Format), IntPtr.Add(pinnedImageData.AddrOfPinnedObject(), (int)imageDataOffset));
                }
                finally
                {
                    pinnedImageData.Free();
                }
            }
            
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
                case TextureFormat.R8:
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
                case TextureFormat.R8:
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

        private static TextureMinFilter GLTextureMinFilter(TextureFilter filter)
        {
            switch(filter)
            {
                case TextureFilter.Linear:
                    return TextureMinFilter.Linear;
                case TextureFilter.Nearest:
                    return TextureMinFilter.Nearest;
                default:
                    throw new Exception("Unhandled texture filter");
            }
        }
        private static TextureMagFilter GLTextureMagFilter(TextureFilter filter)
        {
            switch (filter)
            {
                case TextureFilter.Linear:
                    return TextureMagFilter.Linear;
                case TextureFilter.Nearest:
                    return TextureMagFilter.Nearest;
                default:
                    throw new Exception("Unhandled texture filter");
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
                case TextureFormat.R8:
                    return PixelInternalFormat.R8;
                default:
                    throw new Exception("Unhandled texture format: " + format.ToString());
            }
        }
    }
}


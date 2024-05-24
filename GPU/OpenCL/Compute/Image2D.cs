using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Compute;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using Silk.NET.Core.Native;
using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Octopus.Player.GPU.OpenCL.Compute
{
    internal class Image2D : Image, IImage2D
    {
        public Vector2i Dimensions { get; private set; }

        Context Context { get; set; }

        public override int SizeBytes { get { return Dimensions.Area() * Format.BytesPerPixel(); } }

        internal Image2D(Context context, in Vector2i dimensions, Format format, MemoryDeviceAccess memoryDeviceAccess, MemoryHostAccess memoryHostAccess, 
            MemoryLocation memoryLocation = MemoryLocation.Default, string name = null)
        {
            Dimensions = dimensions;
            Format = format;
            MemoryDeviceAccess = memoryDeviceAccess;
            MemoryHostAccess = memoryHostAccess;
            MemoryLocation = memoryLocation;
            Name = name;
            Context = context;

            // Create CL image
            var memFlags = Buffer.MemFlag(memoryDeviceAccess) | Buffer.MemFlag(memoryHostAccess) | Buffer.MemFlag(memoryLocation);
            int result;
            var imageFormat = ImageFormat(Format);
            ImageDesc imageDesc = new ImageDesc();
            imageDesc.ImageType = MemObjectType.Image2D;
            imageDesc.ImageWidth = (nuint)dimensions.X;
            imageDesc.ImageHeight = (nuint)dimensions.Y;
            imageDesc.ImageDepth = 1;
            unsafe
            {
                NativeHandle = context.Handle.CreateImage(context.NativeHandle, memFlags, in imageFormat, in imageDesc, null, out result);
            }
            Debug.CheckError(result);
        }

        internal Image2D(Context context, Render.IContext renderContext, ITexture texture, MemoryDeviceAccess memoryDeviceAccess)
        {
            Format = texture.Format;
            Dimensions = texture.Dimensions;
            Name = texture.Name;
            MemoryDeviceAccess = memoryDeviceAccess;
            MemoryHostAccess = MemoryHostAccess.NoAccess;
            MemoryLocation = MemoryLocation.Default;
            Context = context;

            // Create CL image from GL texture
            Action createImageFromTextureAction = () =>
            {   
                var sharingExtension = new Silk.NET.OpenCL.Extensions.KHR.KhrGlSharing(Context.Handle.Context);
                int error;
                NativeHandle = sharingExtension.CreateFromGltexture2D(Context.NativeHandle, MemFlags.None, (uint)texture.NativeType, 0, (uint)texture.NativeHandle, out error);
                Debug.CheckError(error);
                valid = (NativeHandle != 0);
            };

            renderContext.EnqueueRenderAction(createImageFromTextureAction);
        }

        override public void Dispose()
        {
            Debug.CheckError(Context.Handle.ReleaseMemObject(NativeHandle));
            NativeHandle = 0;
        }
    }
}

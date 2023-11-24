using Octopus.Player.GPU.Compute;
using OpenTK.Mathematics;
using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.OpenCL.Compute
{
    internal class Image2D : Image, IImage2D
    {
        public Vector2i Dimensions { get; private set; }

        internal nint NativeHandle { get; private set; }

        Context Context { get; set; }

        internal Image2D(Context context, Vector2i dimensions, Format format, MemoryDeviceAccess memoryDeviceAccess, MemoryHostAccess memoryHostAccess, 
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
            unsafe
            {
                NativeHandle = context.Handle.CreateImage2D(context.NativeHandle, memFlags, in imageFormat, (nuint)dimensions.X, (nuint)dimensions.Y, 0, null, out result);
            }
            Debug.CheckError(result);
        }

        internal Image2D(GPU.Render.ITexture texture)
        {
            Format = texture.Format;
        }

        override public void Dispose()
        {
            Debug.CheckError(Context.Handle.ReleaseMemObject(NativeHandle));
            NativeHandle = 0;
        }
    }
}

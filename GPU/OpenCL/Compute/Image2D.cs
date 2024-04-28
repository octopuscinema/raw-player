using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Compute;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
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

        internal nint NativeHandle { get; private set; }

        public override int SizeBytes { get { return Dimensions.Area() * Format.BytesPerPixel(); } }

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

        internal Image2D(Context context, ITexture texture, MemoryDeviceAccess memoryDeviceAccess)
        {
            Format = texture.Format;
            Dimensions = texture.Dimensions;
            Name = texture.Name;
            MemoryDeviceAccess = memoryDeviceAccess;
            MemoryHostAccess = MemoryHostAccess.NoAccess;
            MemoryLocation = MemoryLocation.Default;
            Context = context;

            // Create CL image from GL texture
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                NativeHandle = Silk.NET.OpenCL.Extensions.APPLE.GCL.CreateImageFromTexture(texture.NativeType, (IntPtr)0, texture.NativeHandle);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //NativeHandle = 
            }
        }

        override public void Dispose()
        {
            Debug.CheckError(Context.Handle.ReleaseMemObject(NativeHandle));
            NativeHandle = 0;
        }
    }
}

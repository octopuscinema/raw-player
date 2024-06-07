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
    internal class Image1D : Image, IImage1D
    {
        public int Dimensions { get; private set; }

        Context Context { get; set; }

        public override int SizeBytes { get { return Dimensions * Format.SizeBytes(); } }

        internal Image1D(Context context, int dimensions, Format format, MemoryDeviceAccess memoryDeviceAccess, MemoryHostAccess memoryHostAccess, 
            MemoryLocation memoryLocation = MemoryLocation.Default, string name = null, byte[] imageData = null)
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
            imageDesc.ImageType = MemObjectType.Image1D;
            imageDesc.ImageWidth = (nuint)dimensions;
            imageDesc.ImageHeight = 1;
            imageDesc.ImageDepth = 1;
            unsafe
            {
                if (imageData == null)
                    NativeHandle = context.Handle.CreateImage(context.NativeHandle, memFlags, in imageFormat, in imageDesc, null, out result);
                else
                {
                    fixed (void* pImageData = imageData)
                    {
                        NativeHandle = context.Handle.CreateImage(context.NativeHandle, memFlags | MemFlags.CopyHostPtr, in imageFormat, in imageDesc, pImageData, out result);
                    }
                }
            }
            Debug.CheckError(result);
        }

        override public void Dispose()
        {
            Debug.CheckError(Context.Handle.ReleaseMemObject(NativeHandle));
            NativeHandle = 0;
        }
    }
}

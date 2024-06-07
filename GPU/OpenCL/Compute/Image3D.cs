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
    internal class Image3D : Image, IImage3D
    {
        public Vector3i Dimensions { get; private set; }

        Context Context { get; set; }

        public override int SizeBytes { get { return Dimensions.Volume() * Format.SizeBytes(); } }

        internal Image3D(Context context, in Vector3i dimensions, Format format, MemoryDeviceAccess memoryDeviceAccess, MemoryHostAccess memoryHostAccess, 
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
            imageDesc.ImageType = MemObjectType.Image3D;
            imageDesc.ImageWidth = (nuint)dimensions.X;
            imageDesc.ImageHeight = (nuint)dimensions.Y;
            imageDesc.ImageDepth = (nuint)dimensions.Z;
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

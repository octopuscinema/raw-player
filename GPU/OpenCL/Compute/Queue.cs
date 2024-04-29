using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Compute;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Octopus.Player.GPU.OpenCL.Compute
{
    internal class Queue : IQueue
    {
        public string Name { get; private set; }
        public nint NativeHandle { get; private set; }
        private Context Context { get; set; }

        internal Queue(Context context, string name = null)
        {
            Context = context;
            Name = name;

            int result;
            NativeHandle = Context.Handle.CreateCommandQueueWithProperties(Context.NativeHandle, Context.NativeDevice, (QueueProperties)0, out result);
            Debug.CheckError(result);
        }

        public void Dispose()
        {
            Debug.CheckError(Context.Handle.ReleaseCommandQueue(NativeHandle));
            NativeHandle = 0;
        }

        public void WaitForComplete()
        {
            Debug.CheckError(Context.Handle.Finish(NativeHandle));
        }

        public void AsyncWaitForComplete()
        {
            Debug.CheckError(Context.Handle.EnqueueBarrier(NativeHandle));
        }

        public void Flush()
        {
            Debug.CheckError(Context.Handle.Flush(NativeHandle));
        }

        public void ModifyImage(IImage2D image, Vector2i origin, Vector2i size, byte[] imageData, uint imageDataOffset = 0)
        {
            var imageCL = (Image2D)image;
            if (imageCL == null)
                throw new ArgumentException("Invalid image object");

            var originArray = new Vector3i(origin, 0).ToArray().Cast<nuint>().ToArray();
            var sizeArray = new Vector3i(size, 0).ToArray().Cast<nuint>().ToArray();

            nuint stride = (nuint)image.Dimensions.X * (nuint)image.Format.BytesPerPixel();
            unsafe
            {
                fixed (nuint* pOrigin = originArray)
                {
                    fixed (nuint* pSize = sizeArray)
                    {
                        fixed (byte* pImageData = imageData)
                        {
                            Debug.CheckError(Context.Handle.EnqueueWriteImage(NativeHandle, imageCL.NativeHandle, true, pOrigin, pSize, stride, 0, pImageData + imageDataOffset, 0, null, null));
                        }
                    }
                }
            }
        }

        public void AcquireTextureObject(Render.ITexture texture)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var sharingExtension = new Silk.NET.OpenCL.Extensions.KHR.KhrGlSharing(Context.Handle.Context);
                //sharingExtension. .EnqueueAcquireGlobjects()
            }
        }

        public void ReleaseTextureObject(Render.ITexture texture)
        {

        }
    }
}

using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Compute;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using Silk.NET.OpenCL;
using System;

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
            NativeHandle = Context.Handle.CreateCommandQueue(Context.NativeHandle, Context.NativeDevice, CommandQueueProperties.None, out result);

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

            var originArray = new nuint[] { (nuint)origin.X, (nuint)origin.Y, 0};
            var sizeArray = new nuint[] { (nuint)size.X, (nuint)size.Y, 1 };

            nuint stride = (nuint)image.Dimensions.X * (nuint)image.Format.BytesPerPixel();
            unsafe
            {
                fixed (nuint* pOrigin = originArray)
                {
                    fixed (nuint* pSize = sizeArray)
                    {
                        fixed (byte* pImageData = imageData)
                        {
                            Debug.CheckError(Context.Handle.EnqueueWriteImage(NativeHandle, imageCL.NativeHandle, true, pOrigin, pSize, 0, 0, pImageData + imageDataOffset, 0, null, null));
                        }
                    }
                }
            }
        }

        public void AcquireTextureObject(Render.IContext renderContext, IImage image)
        {
            var sharingExtension = new Silk.NET.OpenCL.Extensions.KHR.KhrGlSharing(Context.Handle.Context);
            switch (image)
            {
                case Image2D image2D:
                    unsafe
                    {
                        var imageHandle = image2D.NativeHandle;
                        renderContext.Finish();
                        Debug.CheckError(sharingExtension.EnqueueAcquireGlobjects(NativeHandle, 1, in imageHandle, 0, null, null));
                    }
                    break;
                default:
                    break;
            }
        }

        public void ReleaseTextureObject(IImage image)
        {
            var sharingExtension = new Silk.NET.OpenCL.Extensions.KHR.KhrGlSharing(Context.Handle.Context);
            switch (image)
            {
                case Image2D image2D:
                    unsafe
                    {
                        var imageHandle = image2D.NativeHandle;
                        Debug.CheckError(sharingExtension.EnqueueReleaseGlobjects(NativeHandle, 1, in imageHandle, 0, null, null));
                        Context.Handle.Finish(NativeHandle);
                    }
                    break;
                default:
                    break;
            }
        }
    }
}

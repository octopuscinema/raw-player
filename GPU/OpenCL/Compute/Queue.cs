using Octopus.Player.GPU.Compute;
using OpenTK.Mathematics;
using Silk.NET.OpenCL;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.GPU.OpenCL.Compute
{
    internal class Queue : IQueue
    {
        public string Name { get; private set; }
        private nint NativeHandle { get; set; }
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

        public unsafe byte* MapImage(IImage2D image, Vector2i regionOrigin, Vector2i regionSize)
        {
            return null;
        }

        public unsafe void UnmapImage(IImage2D image, byte* mappedRegion)
        {
            var imageCL = (Image2D)image;
            //Context.Handle.EnqueueUnmapMemObject(NativeHandle, image.)
        }
    }
}

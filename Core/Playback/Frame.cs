using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Render;
using System;
using System.Diagnostics;

namespace Octopus.Player.Core.Playback
{
    public abstract class Frame : IDisposable
    {
		public Error LastError { get; protected set; }
		public volatile uint frameNumber;
		public GPU.Compute.IImage2D decodedImageGpu;
		public Timecode? timeCode;

		protected GPU.Compute.IQueue ComputeQueue { get; private set; }

#if FRAME_DEBUG
		private volatile static int count = 0;
#endif
		public Frame(GPU.Compute.IContext computeContext, GPU.Compute.IQueue computeQueue, IClip clip, GPU.Format format)
		{
            ComputeQueue = computeQueue;
            decodedImageGpu = computeContext.CreateImage(clip.Metadata.PaddedDimensions, format, GPU.Compute.MemoryDeviceAccess.ReadOnly, GPU.Compute.MemoryHostAccess.WriteOnly);

#if FRAME_DEBUG
			count++;
			Trace.WriteLine("Frame created, count: " + count);
#endif
        }

        public virtual void Dispose()
        {
			if (decodedImageGpu != null)
			{
				decodedImageGpu.Dispose();
				decodedImageGpu = null;
            }

#if FRAME_DEBUG
			count--;
			Trace.WriteLine("Frame disposed, count: " + count);
#endif
        }
    }
}

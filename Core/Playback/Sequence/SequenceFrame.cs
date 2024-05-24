using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Render;
using System;
using System.Diagnostics;

namespace Octopus.Player.Core.Playback
{
    public abstract class SequenceFrame : IDisposable
    {
		public Error LastError { get; protected set; }
		public volatile uint frameNumber;
		public GPU.Compute.IImage2D decodedImageGpu;
		public TimeCode? timeCode;

		protected GPU.Compute.IQueue ComputeQueue { get; private set; }

#if SEQUENCE_FRAME_DEBUG
		private volatile static int count = 0;
#endif
		public SequenceFrame(GPU.Compute.IContext computeContext, GPU.Compute.IQueue computeQueue, IClip clip, GPU.Format format)
		{
            ComputeQueue = computeQueue;
            decodedImageGpu = computeContext.CreateImage(clip.Metadata.PaddedDimensions, format, GPU.Compute.MemoryDeviceAccess.ReadOnly, GPU.Compute.MemoryHostAccess.WriteOnly);

#if SEQUENCE_FRAME_DEBUG
			count++;
			Trace.WriteLine("Sequence frame created, count: " + count);
#endif
        }

        public void Dispose()
        {
			if (decodedImageGpu != null)
			{
				decodedImageGpu.Dispose();
				decodedImageGpu = null;
            }

#if SEQUENCE_FRAME_DEBUG
			count--;
			Trace.WriteLine("Sequence frame disposed, count: " + count);
#endif
        }

		public abstract Error Decode(IClip clip, byte[] workingBuffer = null);
    }
}

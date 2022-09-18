using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Render;
using System;
using System.Diagnostics;

namespace Octopus.Player.Core.Playback
{
    public abstract class SequenceFrame : IDisposable
    {
		public Error LastError { get; protected set; }
		public bool NeedsGPUCopy { get; protected set; }
		public volatile uint frameNumber;
		public byte[] decodedImage;
		public TimeCode? timeCode;

#if SEQUENCE_FRAME_DEBUG
		private volatile static int count = 0;
#endif

		public SequenceFrame(IContext gpuContext, IClip clip, GPU.Render.TextureFormat gpuFormat)
        {
			decodedImage = new byte[gpuFormat.BytesPerPixel() * clip.Metadata.Dimensions.Area()];

#if SEQUENCE_FRAME_DEBUG
			count++;
			Trace.WriteLine("Sequence frame created, count: " + count);
#endif
		}
		
        public void Dispose()
        {
			decodedImage = null;

#if SEQUENCE_FRAME_DEBUG
			count--;
			Trace.WriteLine("Sequence frame disposed, count: " + count);
#endif
		}

		public abstract Error Decode(IClip clip);

		public abstract Error CopyToGPU(IClip clip, IContext renderContext, ITexture gpuImage, byte[] stagingImage, bool immediate = false, Action postCopyAction = null);
    }
}

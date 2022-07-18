using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Render;
using System;
using System.Diagnostics;

namespace Octopus.Player.Core.Playback
{
    public abstract class SequenceFrame : IDisposable
    {
		public volatile uint frameNumber;
		//public GPU.Render.ITexture gpuImage;
		public byte[] decodedImage;

		//private IContext GPUContext { get; set; }

		public SequenceFrame(IContext gpuContext, IClip clip, GPU.Render.TextureFormat gpuFormat)
        {

			Debug.Assert(clip.Metadata != null, "Attempting to create sequence frame for clip without clip metadata");
			//GPUContext = gpuContext;
			//gpuImage = gpuContext.CreateTexture(clip.Metadata.Dimensions, gpuFormat);
			decodedImage = new byte[gpuFormat.BytesPerPixel() * clip.Metadata.Dimensions.Area()];
		}
		
        public void Dispose()
        {
			//GPUContext.DestroyTexture(gpuImage);
			//GPUContext = null;
			//gpuImage = null;
			decodedImage = null;
		}

		public abstract Error Decode(IClip clip);
    }
}

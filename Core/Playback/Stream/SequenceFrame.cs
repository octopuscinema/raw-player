using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Octopus.Player.Core.Playback.Stream
{
	public enum SequenceFrameState
    {
		Empty
    }

    public struct SequenceFrame : IDisposable
    {
		public volatile uint frameNumber;
		public volatile SequenceFrameState state;
		public GPU.Render.ITexture gpuImage;
		public byte[] decodedImage;

		private IContext GPUContext { get; set; }

		public SequenceFrame(IContext gpuContext, IClip clip, GPU.Render.TextureFormat gpuFormat)
        {
			GPUContext = gpuContext;
			Debug.Assert(clip.Metadata != null, "Attempting to create sequence frame for clip without clip metadata");
			gpuImage = gpuContext.CreateTexture(clip.Metadata.Dimensions, gpuFormat);
			state = SequenceFrameState.Empty;
			frameNumber = 0;
			decodedImage = new byte[gpuFormat.BytesPerPixel() * clip.Metadata.Dimensions.Area()];
		}

        public void Dispose()
        {
			GPUContext.DestroyTexture(gpuImage);
			gpuImage = null;
			decodedImage = null;
		}
    }
}

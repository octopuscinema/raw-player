using Octopus.Player.Core.Maths;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Octopus.Player.Core.Playback.Stream
{
	public enum SequenceFrameState
    {
		Empty
    }

    public class SequenceFrame// : IDisposable
    {
		public volatile uint frameNumber;
		public volatile SequenceFrameState state;
		//public GPU.Render.ITexture gpuImage;
		public byte[] decodedImage;

		//private IContext GPUContext { get; set; }


		public CancellationToken cancellationToken;

		public SequenceFrame(IContext gpuContext, IClip clip, GPU.Render.TextureFormat gpuFormat)
        {

			Debug.Assert(clip.Metadata != null, "Attempting to create sequence frame for clip without clip metadata");
			//GPUContext = gpuContext;
			//gpuImage = gpuContext.CreateTexture(clip.Metadata.Dimensions, gpuFormat);
			state = SequenceFrameState.Empty;
			decodedImage = new byte[gpuFormat.BytesPerPixel() * clip.Metadata.Dimensions.Area()];
		}
		
        /*public void Dispose()
        {
			//GPUContext.DestroyTexture(gpuImage);
			//GPUContext = null;
			//gpuImage = null;
			decodedImage = null;
		}*/
    }
}

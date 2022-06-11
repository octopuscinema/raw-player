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

    public struct SequenceFrame
    {
		public volatile uint frameNumber;
		public volatile SequenceFrameState state;
		public GPU.Render.ITexture gpuImage;
		public byte[] decodedImage;

		public SequenceFrame(GPU.Render.IContext gpuContext, IClip clip, GPU.Render.TextureFormat gpuFormat)
        {
			Debug.Assert(clip.Metadata != null, "Attempting to create sequence frame for clip without clip metadata");
			gpuImage = gpuContext.CreateTexture(clip.Metadata.Dimensions, gpuFormat);
			state = SequenceFrameState.Empty;
			frameNumber = 0;
			decodedImage = new byte[gpuFormat.BytesPerPixel() * clip.Metadata.Dimensions.Area()];
		}
		/*
		std::atomic_uint64_t FrameNumber;
		mutable std::vector<u8> pDecodedImage8Bit;
		mutable std::vector<u16> pDecodedImage16Bit;
		mutable std::vector<u16> WorkingCache;
		CJ3Image* pGPUImage;
		std::atomic<eSequenceFrameState> State;
		*/
	}
}

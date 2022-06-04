using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Playback.Stream
{
	public enum SequenceFrameState
    {

    }

    public struct SequenceFrame
    {
		public volatile uint frameNumber;
		public volatile GPU.Render.ITexture imageGPU;
		public volatile SequenceFrameState state;
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

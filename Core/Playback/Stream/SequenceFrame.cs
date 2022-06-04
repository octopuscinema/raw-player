using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Playback.Stream
{
    public struct SequenceFrame
    {
		uint FrameNumber { get; set; }
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

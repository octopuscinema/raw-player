﻿namespace Octopus.Player.Core.Playback
{
    public abstract class SequenceFrameRAW : SequenceFrame
    {

        public SequenceFrameRAW(IClip clip, GPU.Format format)
            : base(clip, format)
        {

		}

		public SequenceFrameRAW(GPU.Compute.IContext computeContext, GPU.Compute.IQueue computeQueue, IClip clip, GPU.Format format)
            : base(computeContext, computeQueue, clip, format)
		{

		}

        public abstract Error Process(IClip clip, GPU.Compute.IImage2D output, GPU.Compute.IImage1D linearizeTable, GPU.Compute.IProgram program, GPU.Compute.IQueue queue);
    }
}

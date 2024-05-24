using System;

namespace Octopus.Player.Core.Playback
{
    public abstract class SequenceFrameRAW : SequenceFrame
    {
        public bool Processed { get; protected set; }

        public SequenceFrameRAW(GPU.Compute.IContext computeContext, GPU.Compute.IQueue computeQueue, IClip clip, GPU.Format format)
            : base(computeContext, computeQueue, clip, format)
		{

		}

        public abstract Error Process(IClip clip, GPU.Render.IContext renderContext, GPU.Compute.IImage2D output, GPU.Compute.IImage1D linearizeTable, GPU.Compute.IProgram program,
            GPU.Compute.IQueue queue, bool immediate = false, Action postCopyAction = null);
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Playback
{
    public enum FrameRequestResult
    {
        Success,
        FrameAlreadyInProgress,
        ErrorFrameOutOfRange,
        ErrorFramePreviouslyFailed,
        ErrorBufferFull,
        ErrorUnknown
    }

    public interface ISequenceStream : IDisposable
    {
        FrameRequestResult RequestFrame(uint frameNumber);
    }
}

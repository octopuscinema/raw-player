using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Playback
{
    public enum FrameRequestResult
    {
        Success,
        NoRequests,
        FrameAlreadyComplete,
        FrameAlreadyInProgress,
        ErrorFrameOutOfRange,
        ErrorFramePreviouslyFailed,
        ErrorBufferFull,
        ErrorDecodingFrame,
        ErrorUnknown
    }

    public interface ISequenceStream : IDisposable
    {
        FrameRequestResult RequestFrame(uint frameNumber);
    }
}

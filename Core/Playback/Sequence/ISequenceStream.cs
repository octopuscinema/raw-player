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

    public enum FrameStorage
    {
        Cpu,
        GpuCompute
    }

    public interface ISequenceStream : IDisposable
    {
        GPU.Format Format { get; }

        FrameRequestResult RequestFrame(uint frameNumber);
        bool CancelRequest(uint frameNumber);
        void CancelRequestsFrom(uint fromFrame);
        void CancelRequestsUpTo(uint upToFrame);
        void CancelAllRequests();

        void OnFrameDisplayed(uint frameNumber);
        void ReclaimReadyFramesUpTo(uint upToFrame);
        void ReclaimReadyFramesFrom(uint fromFrame);
        void ReclaimReadyFrames();

        List<uint> ReadyFrames();
        bool FrameReady(uint frameNumber);
        SequenceFrame RetrieveFrame(uint frameNumber);
    }
}

using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Octopus.Player.Core.Playback
{
    public class SequenceStream<T> : ISequenceStream where T : SequenceFrame
    {
        public IClip Clip { get; protected set; }

        ConcurrentBag<SequenceFrame> Pool { get; set; }
        ConcurrentDictionary<uint,SequenceFrame> DisplayFrames { get; set; }
        List<uint> FrameRequests { get; set; }
        Mutex FrameRequestsMutex { get; set; }

        uint BufferDurationFrames { get; set; }

        List<Worker<FrameRequestResult>> Workers { get; set; }

        public SequenceStream(IClip clip, IContext gpuContext, GPU.Format gpuFormat, uint bufferDurationFrames, uint workerThreadBufferSize = 0, uint? workerThreadCount = null)
        {
            Debug.Assert(clip.Metadata != null, "Cannot create sequence stream for clip without clip metadata");
            Clip = clip;
            BufferDurationFrames = bufferDurationFrames;

            Pool = new ConcurrentBag<SequenceFrame>();
            FrameRequests = new List<uint>();
            DisplayFrames = new ConcurrentDictionary<uint, SequenceFrame>();
            FrameRequestsMutex = new Mutex();

            // Create work for workers
            Func<byte[],FrameRequestResult> processFrameRequests = (byte[] workingBuffer) =>
            {
                // Get the next frame requested
                uint? frameNumber = null;
                try
                {
                    FrameRequestsMutex.WaitOne();
                    if (FrameRequests.Count == 0)
                        return FrameRequestResult.NoRequests;
                    frameNumber = FrameRequests[0];
                    FrameRequests.RemoveAt(0);
                }
                finally
                {
                    FrameRequestsMutex.ReleaseMutex();
                }

                // Attempt to get a preallocated frame from the pool
                SequenceFrame frame;
                if (!Pool.TryTake(out frame))
                    return FrameRequestResult.ErrorBufferFull;

                // Decode the frame
                frame.frameNumber = frameNumber.Value;
                frame.timeCode = null;
                var decodeResult = frame.Decode(Clip, workingBuffer);

                // Frame ready to be displayed
                if (!DisplayFrames.TryAdd(frame.frameNumber, frame))
                {
                    Debug.Assert(false, "Frame already decoded and ready to display");
                    Pool.Add(frame);
                }

                return decodeResult == Error.None ? FrameRequestResult.Success : FrameRequestResult.ErrorDecodingFrame;
            };

            // Create worker threads
            if (workerThreadCount == null)
                workerThreadCount = Math.Min((uint)bufferDurationFrames, (uint)Environment.ProcessorCount);
            Workers = new List<Worker<FrameRequestResult>>((int)workerThreadCount.Value);
            for (uint i = 0; i < workerThreadCount.Value; i++)
                Workers.Add(new Worker<FrameRequestResult>(processFrameRequests));

            // Allocate frame pool
            for (int i = 0; i < bufferDurationFrames; i++)
                Pool.Add(Activator.CreateInstance(typeof(T), gpuContext, clip, gpuFormat) as T);
        }

        public virtual void Dispose()
        {
            CancelAllRequests();

            Workers.ForEach(i => i.Dispose());
            Workers.Clear();

            ReclaimReadyFrames();
            foreach (var frame in Pool)
                frame.Dispose();
            Pool.Clear();
        }

        public void CancelAllRequests()
        {
            try
            {
                FrameRequestsMutex.WaitOne();
                FrameRequests.Clear();
            }
            finally
            {
                FrameRequestsMutex.ReleaseMutex();
            }
        }

        public void ReclaimReadyFrames()
        {
            Workers.ForEach(i => i.Stop());
            var displayFramesCopy = new ConcurrentDictionary<uint, SequenceFrame>(DisplayFrames);
            foreach(var frame in displayFramesCopy)
                OnFrameDisplayed(frame.Key);
            Debug.Assert(DisplayFrames.IsEmpty);
            Workers.ForEach(i => i.Start());
        }

        public void ReclaimReadyFramesUpTo(uint upToFrame)
        {
            var displayFramesCopy = new ConcurrentDictionary<uint, SequenceFrame>(DisplayFrames);
            foreach (var frame in displayFramesCopy)
            {
                if ( frame.Key <= upToFrame)
                    OnFrameDisplayed(frame.Key);
            }
        }

        public void ReclaimReadyFramesFrom(uint fromFrame)
        {
            var displayFramesCopy = new ConcurrentDictionary<uint, SequenceFrame>(DisplayFrames);
            foreach (var frame in displayFramesCopy)
            {
                if (frame.Key >= fromFrame)
                    OnFrameDisplayed(frame.Key);
            }
        }

        public bool CancelRequest(uint frameNumber)
        {
            try
            {
                FrameRequestsMutex.WaitOne();
                return FrameRequests.Remove(frameNumber);
            }
            finally
            {
                FrameRequestsMutex.ReleaseMutex();
            }
        }

        public void CancelRequestsFrom(uint fromFrame)
        {
            try
            {
                FrameRequestsMutex.WaitOne();
                var requests = new List<uint>(FrameRequests);
                foreach (var request in requests)
                {
                    if ( request >= fromFrame)
                        FrameRequests.Remove(request);
                }
            }
            finally
            {
                FrameRequestsMutex.ReleaseMutex();
            }
        }

        public void CancelRequestsUpTo(uint upToFrame)
        {
            try
            {
                FrameRequestsMutex.WaitOne();
                var requests = new List<uint>(FrameRequests);
                foreach (var request in requests)
                {
                    if (request <= upToFrame)
                        FrameRequests.Remove(request);
                }
            }
            finally
            {
                FrameRequestsMutex.ReleaseMutex();
            }
        }

        public bool FrameReady(uint frameNumber)
        {
            return DisplayFrames.ContainsKey(frameNumber);
        }

        public List<uint> ReadyFrames()
        {
            List<uint> frames = new List<uint>();
            var displayFramesCopy = new ConcurrentDictionary<uint, SequenceFrame>(DisplayFrames);
            foreach (var frame in displayFramesCopy)
                frames.Add(frame.Key);
            return frames;
        }

        public virtual FrameRequestResult RequestFrame(uint frameNumber)
        {
            // Frame already ready to display
            if (DisplayFrames.ContainsKey(frameNumber))
                return FrameRequestResult.FrameAlreadyComplete;

            // Add to frame requests if not already there
            try
            {
                FrameRequestsMutex.WaitOne();
                if (FrameRequests.Contains(frameNumber))
                    return FrameRequestResult.FrameAlreadyInProgress;
                FrameRequests.Add(frameNumber);
            }
            finally
            {
                FrameRequestsMutex.ReleaseMutex();
            }

            // Wake workers
            Workers.ForEach(i => i.Resume());

            return FrameRequestResult.Success;
        }

        public SequenceFrame RetrieveFrame(uint frameNumber)
        {
            SequenceFrame frame;
            return DisplayFrames.TryGetValue(frameNumber, out frame) ? frame : null;
        }

        public void OnFrameDisplayed(uint frameNumber)
        {
            // Remove from ready frames and return to pool
            SequenceFrame frame;
            bool foundFrame = DisplayFrames.TryRemove(frameNumber, out frame);
            Debug.Assert(foundFrame);
            Pool.Add(frame);
        }
    }
}

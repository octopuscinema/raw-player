using Octopus.Player.Core.Playback.Stream;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Player.Core.Playback
{
    public abstract class SequenceStream : ISequenceStream
    {
        // Worker thread
        private class SequenceStreamWorker : IDisposable
        {
            public bool Paused { get; private set; }
            private Thread Thread { get; set; }
            private Func<FrameRequestResult> Work { get; set; }
            private ManualResetEvent ManualResetEvent { get; set; }
            private volatile bool terminate = false;

            public SequenceStreamWorker(Func<FrameRequestResult> work, bool paused = true)
            {
                Paused = paused;
                Work = work;
                ManualResetEvent = new ManualResetEvent(!Paused);
                Thread = new Thread(WorkLoop);
            }

            public void Dispose()
            {
                terminate = true;
                if (Paused)
                    Resume();
                Thread.Join();
                ManualResetEvent.Dispose();
                Thread = null;
                ManualResetEvent = null;
            }

            public void Pause()
            {
                Debug.Assert(!Paused);
                ManualResetEvent.Reset();
                Paused = true;
            }

            public void Resume()
            {
                Debug.Assert(Paused);
                ManualResetEvent.Set();
                Paused = false;
            }

            private void WorkLoop()
            {
                while(!terminate)
                {
                    var workResult = Work();
                    if (workResult == FrameRequestResult.NoRequests && !Paused)
                        Pause();
                    ManualResetEvent.WaitOne();
                }
            }
        }


        public IClip Clip { get; protected set; }
        public Vector2i Dimensions { get { Debug.Assert(Clip != null && Clip.Metadata != null); return Clip.Metadata.Dimensions; } }

        ConcurrentBag<SequenceFrame> Pool { get; set; }
        ConcurrentDictionary<uint,SequenceFrame> DisplayFrames { get; set; }
        List<uint> FrameRequests { get; set; }
        Mutex FrameRequestsMutex { get; set; }

        uint BufferDurationFrames { get; set; }

        List<SequenceStreamWorker> Workers { get; set; }

        protected SequenceStream(IClip clip, IContext gpuContext, TextureFormat gpuFormat, uint bufferDurationFrames, uint? workerThreadCount = null)
        {
            Debug.Assert(clip.Metadata != null, "Cannot create sequence stream for clip without clip metadata");
            Clip = clip;
            BufferDurationFrames = bufferDurationFrames;

            Pool = new ConcurrentBag<SequenceFrame>();
            FrameRequests = new List<uint>();
            DisplayFrames = new ConcurrentDictionary<uint, SequenceFrame>();
            FrameRequestsMutex = new Mutex();

            // Create work for workers
            Func<FrameRequestResult> processFrameRequests = () =>
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
                var decodeResult = DecodeFrame(frame);

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
                workerThreadCount = (uint)Environment.ProcessorCount;
            Workers = new List<SequenceStreamWorker>((int)workerThreadCount.Value);
            for (uint i = 0; i < workerThreadCount.Value; i++)
                Workers.Add(new SequenceStreamWorker(processFrameRequests));

            // Allocate pool storage
            for (int i = 0; i < bufferDurationFrames; i++)
                Pool.Add(new SequenceFrame(gpuContext, clip, gpuFormat));
        }

        public virtual void Dispose()
        {
            CancelAllRequests();

            Workers.ForEach(i => i.Dispose());
            Workers.Clear();
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

        public bool FrameInQueue(uint frameNumber)
        {
            try
            {
                FrameRequestsMutex.WaitOne();
                return FrameRequests.Contains(frameNumber);
            }
            finally
            {
                FrameRequestsMutex.ReleaseMutex();
            }
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

            // Wake a worker
            foreach(var worker in Workers)
            {
                if ( worker.Paused )
                {
                    worker.Resume();
                    break;
                }
            }

            return FrameRequestResult.Success;
        }

        public SequenceFrame RetreieveFrame(uint frameNumber)
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

        public abstract Error DecodeFrame(SequenceFrame frame);
    }
}

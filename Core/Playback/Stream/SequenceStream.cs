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
        public IClip Clip { get; protected set; }

        ConcurrentQueue<SequenceFrame> RequestedFrames { get; set; }
        ConcurrentBag<SequenceFrame> Pool { get; set; }
        ConcurrentDictionary<uint,SequenceFrame> DisplayFrames { get; set; }

        /*Concurrent*/Dictionary<uint, Tuple<Task,CancellationTokenSource>> DecodeTasks { get; set; }

        uint BufferDurationFrames { get; set; }

        public Vector2i Dimensions { get { Debug.Assert(Clip != null && Clip.Metadata != null); return Clip.Metadata.Dimensions; } }

        protected SequenceStream(IClip clip, IContext gpuContext, TextureFormat gpuFormat, uint bufferDurationFrames)
        {
            Debug.Assert(clip.Metadata != null, "Cannot create sequence stream for clip without clip metadata");
            Clip = clip;
            BufferDurationFrames = bufferDurationFrames;

            //cancelDecodeFrames = new CancellationTokenSource();

            RequestedFrames = new ConcurrentQueue<SequenceFrame>();
            DisplayFrames = new ConcurrentDictionary<uint, SequenceFrame>();
            


            // Allocate pool storage
            Pool = new ConcurrentBag<SequenceFrame>();
            for (int i = 0; i < bufferDurationFrames; i++)
                Pool.Add(new SequenceFrame(gpuContext, clip, gpuFormat));

            /*
            // Pre allocate queues
            RequestedFrames = new ConcurrentQueue<uint>();
            FramesInProgress = new List<uint>();

            // Allocate storage for free slots
            FreeFrames = new ConcurrentBag<SequenceFrame>();
            for (int i = 0; i < bufferDurationFrames; i++)
                FreeFrames.Add(new SequenceFrame(gpuContext, clip, gpuFormat));
            */
            // Example usage of thread pool
            /*
            const int threadCount = 10;
            var list = new List<int>(threadCount);
            for (var i = 0; i < threadCount; i++) list.Add(i);

            using (var countdownEvent = new CountdownEvent(threadCount))
            {
                for (var i = 0; i < threadCount; i++)
                    ThreadPool.QueueUserWorkItem(
                        x =>
                        {
                            Trace.WriteLine(x);
                            countdownEvent.Signal();
                        }, list[i]);

                countdownEvent.Wait();
            }
            Trace.WriteLine("done");
            */
        }

        public virtual void Dispose()
        {
            //Task.WaitAll
        }

        public virtual FrameRequestResult RequestFrame(uint frameNumber)
        {
            if (RequestedFrames.Count >= BufferDurationFrames)
                return FrameRequestResult.ErrorBufferFull;

            SequenceFrame frame;
            if (!Pool.TryTake(out frame))
                return FrameRequestResult.ErrorBufferFull;


            frame.frameNumber = frameNumber;
            //var cancellationTokenSource = new CancellationTokenSource();

            //frame.cancellationToken = new CancellationToken();
            RequestedFrames.Enqueue(frame);

            // Enqueue a frame decode task
            Action decodeFrame = () =>
            {
                SequenceFrame work;
                if (RequestedFrames.TryDequeue(out work))
                {
                    if (work.Cancellation.IsCancellationRequested)
                    {
                        Pool.Add(work);
                        return;
                    }
                    DecodeFrame(work);
                    DisplayFrames.TryAdd(work.frameNumber, work);
                }
            };

            //var tasks = new List<Task>();

            //tasks.Add()
            var task = Task.Run(decodeFrame);

            //ThreadPool.QueueUserWorkItem(new WaitCallback(decodeFrameAction));

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

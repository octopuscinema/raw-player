using Octopus.Player.Core.Playback.Stream;
using Octopus.Player.GPU.Render;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Octopus.Player.Core.Playback
{
    public abstract class SequenceStream : ISequenceStream
    {
        public IClip Clip { get; protected set; }

        Queue<uint> RequestedFrames { get; set; }
        List<uint> FramesInProgress { get; set; }

        List<uint> DecodedFrames { get; set; }
        List<SequenceFrame> FreeFrames { get; set; }

        public Vector2i Dimensions { get { Debug.Assert(Clip != null && Clip.Metadata != null); return Clip.Metadata.Dimensions; } }

        protected SequenceStream(IClip clip, IContext gpuContext, TextureFormat gpuFormat, TimeSpan bufferDuration)
        {
            Debug.Assert(clip.Metadata != null, "Cannot create sequence stream for clip without clip metadata");
            Clip = clip;

            // Pre allocate queues
            RequestedFrames = new Queue<uint>();// (int)clip.Metadata.Framerate.ToSingle());
            FramesInProgress = new List<uint>();

            // Allocate storage for free slots
            var bufferLengthFrames = (uint)(bufferDuration.Duration().TotalSeconds * clip.Metadata.Framerate.ToDouble());
            FreeFrames = new List<SequenceFrame>();
            for (int i = 0; i < bufferLengthFrames; i++)
                FreeFrames.Add(new SequenceFrame(gpuContext, clip, gpuFormat));

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

        public abstract void Dispose();

        public FrameRequestResult RequestFrame(uint frameNumber)
        {
            // Sanity check
            if (frameNumber >= Clip.Metadata.DurationFrames )
                return FrameRequestResult.ErrorFrameOutOfRange;

            return FrameRequestResult.Success;
        }

        public abstract Error DecodeFrame(SequenceFrame frame);
    }
}

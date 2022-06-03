using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Octopus.Player.Core.Playback
{
    public abstract class SequenceStream : ISequenceStream
    {
        public IClip Clip { get; protected set; }

        List<uint> RequestedFrames { get; set; }

        protected SequenceStream(IClip clip)
        {
            Debug.Assert(clip.Metadata != null, "Cannot create sequence stream for clip without clip metadata");
            Clip = clip;

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
                            Console.WriteLine(x);
                            countdownEvent.Signal();
                        }, list[i]);

                countdownEvent.Wait();
            }
            Console.WriteLine("done");
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
    }
}

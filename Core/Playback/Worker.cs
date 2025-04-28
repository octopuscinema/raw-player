using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Octopus.Player.Core.Playback
{
    public class Worker<T> : IDisposable
    {
        public bool IsSleeping { get { return !Sleep.WaitOne(0); } }
        public bool IsBusy { get { return !IsSleeping || !Busy.WaitOne(0); } }

        private IVideoDecompressionSession DecompressionSession { get; set; }

        private Thread Thread { get; set; }
        private Func<IVideoDecompressionSession,T> Work { get; set; }
        private AutoResetEvent Sleep { get; set; }
        private ManualResetEvent Busy { get; set; }
        private volatile bool terminate = false;
        private volatile bool terminateImmediate = false;

        public Worker(Func<IVideoDecompressionSession,T> work, bool paused = true, IVideoDecompressionSession decompressionSession = null)
        {
            DecompressionSession = decompressionSession;
            Work = work;
            Sleep = new AutoResetEvent(!paused);
            Busy = new ManualResetEvent(!paused);
            Thread = new Thread(WorkLoop);
            Thread.Start();
        }

        public void Dispose()
        {
            terminate = true;
            terminateImmediate = true;
            if (Thread != null)
            {
                Resume();
                Thread.Join();
            }
            Sleep.Dispose();
            Busy.Dispose();
            Busy = null;
            Thread = null;
            Sleep = null;
            Work = null;
            if (DecompressionSession != null)
            {
                DecompressionSession.Dispose();
                DecompressionSession = null;
            }
        }

        public void Stop(bool immediate = true)
        {
            terminate = true;
            terminateImmediate = immediate;
            Resume();
            Thread.Join();
            Thread = null;
        }

        public void Start()
        {
            terminate = false;
            terminateImmediate = false;
            Debug.Assert(Thread == null);
            Thread = new Thread(WorkLoop);
            Thread.Start();
        }

        public void WaitForWork()
        {
            Busy.WaitOne();
        }

        public void Resume()
        {
            Sleep.Set();
        }

        private void WorkLoop()
        {
            while (!terminate)
            {
                Busy.Reset();
                Work(DecompressionSession);
                Busy.Set();
                Sleep.WaitOne();

                if (!terminateImmediate)
                {
                    Busy.Reset();
                    Work(DecompressionSession);
                    Busy.Set();
                }
            }
        }
    }
}

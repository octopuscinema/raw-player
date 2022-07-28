using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Octopus.Player.Core.Playback
{
    internal class SequenceStreamWorker : IDisposable
    {
        public bool Paused { get { return !Sync.WaitOne(0); } }
        private Thread Thread { get; set; }
        private Func<FrameRequestResult> Work { get; set; }
        private AutoResetEvent Sync { get; set; }
        private volatile bool terminate = false;

        public SequenceStreamWorker(Func<FrameRequestResult> work, bool paused = true)
        {
            Work = work;
            Sync = new AutoResetEvent(!paused);
            Thread = new Thread(WorkLoop);
            Thread.Start();
        }

        public void Dispose()
        {
            terminate = true;
            Resume();
            Thread.Join();
            Sync.Dispose();
            Thread = null;
            Sync = null;
            Work = null;
        }

        public void Stop()
        {
            terminate = true;
            Resume();
            Thread.Join();
            Thread = null;
        }

        public void Start()
        {
            terminate = false;
            Debug.Assert(Thread == null);
            Thread = new Thread(WorkLoop);
            Thread.Start();
        }

        public void Resume()
        {
            Sync.Set();
        }

        private void WorkLoop()
        {
            while (!terminate)
            {
                Work();
                Sync.WaitOne();
            }
        }
    }
}

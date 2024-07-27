using System;
using System.Diagnostics;
using System.Threading;

namespace Octopus.Player.Core.Playback
{
	public class PlaybackTimer : IDisposable
	{
        public TimeSpan? Offset { get; private set; }
        public Maths.Rational Rate { get; private set; }

        private Action Action { get; set; }
        private Thread Thread { get; set; }


        private Stopwatch Stopwatch { get; set; }

        private AutoResetEvent Sleep { get; set; }

        private Audio.IMediaClock Clock { get; set; }
        private double? clockOffset;

        private volatile bool terminate = false;

        public PlaybackTimer(Action action, TimeSpan? offset, Maths.Rational rate, Audio.IMediaClock clock = null)
		{
            Clock = clock;
            Offset = offset;
            Rate = rate;
			Action = action;
            Sleep = new AutoResetEvent(false);
            Stopwatch = new Stopwatch();
            Stopwatch.Start();

            if ( Clock !=  null )
                Clock.ActiveChanged += OnClockActiveChanged;

            Thread = new Thread(Work);
            Thread.Start();
		}

        public void Dispose()
        {
            if (Clock != null)
                Clock.ActiveChanged -= OnClockActiveChanged;

            terminate = true;
            Sleep.Set();
            Thread.Join();
            Thread = null;
            Action = null;
            Sleep.Dispose();
            Sleep = null;
        }

        private double Time()
        {
            if (Clock != null && Clock.Active )
            {
                if (!clockOffset.HasValue)
                    clockOffset = Clock.Time - Stopwatch.Elapsed.TotalSeconds;
                return Clock.Time - clockOffset.Value;
            }

            return Stopwatch.Elapsed.TotalSeconds;
        }

        private void OnClockActiveChanged(object sender, EventArgs e)
        {
            if (Clock.Active)
                clockOffset = Clock.Time - Stopwatch.Elapsed.TotalSeconds;
            else
                clockOffset = null;
        }

        private void Work()
        {
            uint count = 0;
            while(!terminate)
            {
                var nextInvokeTime = (((double)count * (double)Rate.Denominator) / (double)Rate.Numerator);
                if (Offset.HasValue)
                    nextInvokeTime += Offset.Value.TotalSeconds;
                var sleepDuration = nextInvokeTime - Time();
                if (sleepDuration > 0.0)
                    Sleep.WaitOne(TimeSpan.FromSeconds(sleepDuration));

                if (terminate)
                    return;

                Action.Invoke();
                count++;
            }
        }
    }
}


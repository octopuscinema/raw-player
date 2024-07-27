using System;
using System.Threading;

namespace Octopus.Player.Core.Playback
{
    public interface IMediaClock
    {
        bool Active { get; }
        double Time { get; }
    }

	public class PlaybackTimer : IDisposable
	{
        public TimeSpan? Offset { get; private set; }
        public Maths.Rational Rate { get; private set; }

        private Action Action { get; set; }
        private Thread Thread { get; set; }

        private volatile bool terminate = false;

        private System.Diagnostics.Stopwatch Stopwatch { get; set; }

        private IMediaClock Clock { get; set; }
        private double? clockOffset;

        public PlaybackTimer(Action action, TimeSpan? offset, Maths.Rational rate, IMediaClock clock = null)
		{
            Offset = offset;
            Rate = rate;
			Action = action;
            Stopwatch.Start();
            Thread = new Thread(Work);
            Thread.Start();
		}

        public void Dispose()
        {
            terminate = true;
            Thread.Join();
            Thread = null;
            Action = null;
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

        private void Work()
        {
            uint count = 0;
            while(!terminate)
            {
                var nextInvokeTime = (((double)count * (double)Rate.Denominator) / (double)Rate.Numerator);
                if (Offset.HasValue)
                    nextInvokeTime += Offset.Value.TotalSeconds;
                var sleepDuration = nextInvokeTime - Time();
                if (sleepDuration > 0.0 )
                    Thread.Sleep(TimeSpan.FromSeconds(sleepDuration));

                Action.Invoke();
                count++;
            }
        }
    }
}


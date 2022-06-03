using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Octopus.Player.Core.Playback
{
	public abstract class Playback : IPlayback
	{
		public Playback()
		{
            State = State.Empty;
		}

        ~Playback()
        {
            Debug.Assert(!IsOpen());
            if (IsOpen())
                Close();
        }

        public virtual List<Essence> SupportedEssence { get; }
        public State State { get; protected set; }
        public IClip Clip { get; protected set; }
        public bool IsPlaying { get { return State == State.Playing; } }
        public bool IsPaused { get { return State == State.Paused; } }

        public abstract void Close();
        public abstract Error Open(IClip clip);
        public bool IsOpen()
        {
            return Clip != null;
        }
        public abstract bool SupportsClip(IClip clip);
        public abstract void Stop();
        public abstract void Play();
        public abstract void Pause();
    }
}


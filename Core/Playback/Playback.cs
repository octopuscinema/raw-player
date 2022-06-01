using System;
using System.Collections.Generic;

namespace Octopus.Player.Core.Playback
{
	public abstract class Playback : IPlayback
	{
		public Playback()
		{
            State = State.Empty;
		}

        public virtual List<Essence> SupportedEssence { get; }
        public State State { get; protected set; }
        public IClip Clip { get; protected set; }

        public abstract void Close();
        public abstract void Dispose();
        public abstract Error Open(IClip clip);
    }
}


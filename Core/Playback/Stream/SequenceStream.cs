using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Playback
{
    public abstract class SequenceStream : ISequenceStream
    {
        public IClip Clip { get; protected set; }

        protected SequenceStream(IClip clip)
        {
            Clip = clip;
        }

        public abstract void Dispose();
    }
}

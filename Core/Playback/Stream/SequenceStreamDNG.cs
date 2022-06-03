using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Playback
{
    public class SequenceStreamDNG : SequenceStream
    {
        private Core.IO.DNG.Reader DNGReader { get; set; }

        public SequenceStreamDNG(ClipCinemaDNG clip) : base(clip)
        {

        }

        public override void Dispose()
        {
            if (DNGReader != null)
            {
                DNGReader.Dispose();
                DNGReader = null;
            }
        }
    }
}

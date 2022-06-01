using System;
using System.Collections.Generic;

namespace Octopus.Player.Core.Playback
{
	public class PlaybackDNG : Playback
	{
		public PlaybackDNG()
		{
            
		}

        public override List<Essence> SupportedEssence { get { return new List<Essence>() { Essence.Sequence }; } }

        public override void Close()
        {
            
        }

        public override void Dispose()
        {
            Close();
        }

        public override Error Open(IClip clip)
        {
            // Enumerate

            return Error.NotImplmeneted;
        }
	}
}


using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Octopus.Player.Core.Playback
{
	public class PlaybackCinemaDNG : Playback
	{
        private SequenceStreamDNG SequenceStreamDNG { get; set; }

		public PlaybackCinemaDNG()
		{
            
		}

        public override List<Essence> SupportedEssence { get { return new List<Essence>() { Essence.Sequence }; } }

        public override void Close()
        {
            Debug.Assert(IsOpen() && SequenceStreamDNG != null);
            if (SequenceStreamDNG != null)
            {
                SequenceStreamDNG.Dispose();
                SequenceStreamDNG = null;
            }
            State = State.Empty;
            Clip = null;
        }

        public override Error Open(IClip clip)
        {
            Debug.Assert(!IsOpen());
            if (IsOpen())
                Close();

            // Load metadata, if that was unsuccesful, bail out
            var cinemaDNGClip = (ClipCinemaDNG)clip;
            Debug.Assert(cinemaDNGClip != null);
            if (clip.ReadMetadata() != Error.None)
                return Error.BadMetadata;
            Clip = clip;
/*
            try
            {
                var dngFiles = System.IO.Directory.EnumerateFiles(Path, "*.dng", System.IO.SearchOption.TopDirectoryOnly);
                return dngFiles.Any() ? Error.None : Error.NoVideoStream;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to validate CinemaDNG sequence path: " + Path + "\n" + e.Message);
                return Error.BadPath;
            }
*/

            // Create the sequence stream
            Debug.Assert(SequenceStreamDNG == null);
            SequenceStreamDNG = new SequenceStreamDNG((ClipCinemaDNG)clip);
               
            return Error.NotImplmeneted;
        }

        public override bool SupportsClip(IClip clip)
        {
            return (clip.GetType() == typeof(ClipCinemaDNG) && SupportedEssence.Contains(clip.Essence));
        }

        public override void Stop()
        {
            throw new NotImplementedException();
        }

        public override void Play()
        {
            throw new NotImplementedException();
        }

        public override void Pause()
        {
            throw new NotImplementedException();
        }
    }
}


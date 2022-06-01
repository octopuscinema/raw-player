using System;
using System.Linq;

namespace Octopus.Player.Core.Playback
{
	public class ClipCinemaDNG : Clip
	{
		public ClipCinemaDNG(string sequenceDir)
		{
            Path = sequenceDir;
		}

        public override Essence Essence { get { return Essence.Sequence; } }

        public override Error ReadMetadata()
        {
            throw new NotImplementedException();
        }

        public override Error Validate()
        {
            // Check path is a folder
            if (!System.IO.Directory.Exists(Path))
                return Error.BadPath;

            // Check path has DNGs
            try
            {
                var dngFiles = System.IO.Directory.EnumerateFiles(Path, "?.dng", System.IO.SearchOption.TopDirectoryOnly);
                return dngFiles.Any() ? Error.None : Error.NoVideoStream;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to validate CinemaDNG sequence path: " + Path + "\n" + e.Message);
                return Error.BadPath;
            }
        }
    }
}


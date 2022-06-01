using System;

namespace Octopus.Player.Core.Playback
{
	public class CinemaDNGClip : Clip
	{
		public CinemaDNGClip(string sequenceDir)
		{
            Path = sequenceDir;
		}

        public override Essence Essence { get { return Essence.Sequence; } }

        public override Error Validate()
        {
            // Check path is a folder
            if (!System.IO.Directory.Exists(Path))
                return Error.BadPath;

            // Check path has DNGs
            string[] files = System.IO.Directory.GetFiles(Path);
            foreach(var filePath in files)
            {
                if (string.Equals(System.IO.Path.GetExtension(filePath), ".dng",StringComparison.OrdinalIgnoreCase) )
                    return Error.None;
            }

            return Error.NotImplmeneted;
        }
    }
}


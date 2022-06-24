using System;
using System.Diagnostics;
using System.Linq;

namespace Octopus.Player.Core
{
	public class ClipCinemaDNG : Clip
	{
        public override Essence Essence { get { return Essence.Sequence; } }
		
        private uint SequencingFieldPosition { get; set; }
        private uint SequencingFieldLength { get; set; }
        private string CachedFramePath { get; set; }

        public ClipCinemaDNG(string sequenceDir)
		{
            Path = sequenceDir;
            Valid = false;
            RawParameters = new RawParameters();
        }

        public override Error ReadMetadata(uint? frame = null)
        {
            // Metadata already read
            if (Metadata != null)
                return Error.None;

            // Get path to DNG file
            string dngPath = null;
            if (frame.HasValue)
            {
                var framePathError = GetFramePath(frame.Value, out dngPath);
                if (framePathError != Error.None)
                    return framePathError;
            }
            else
                dngPath = CachedFramePath;
            if (!System.IO.File.Exists(dngPath))
                return Error.BadPath;

            try
            {
                using var reader = new IO.DNG.Reader(dngPath);
                Metadata = new IO.DNG.MetadataCinemaDNG(reader, this);
                return Error.None;
            }
            catch
            {
                return Error.BadFile;
            }
        }

        public Error GetFramePath(uint frame, out string path)
        {
            // Clip not valid
            if (!Valid)
            {
                path = null;
                return Error.ClipNotValidated;
            }

            // Create sequncing field from the frame number
            var sequencingField = frame.ToString("D" + SequencingFieldLength);
            
            // Use the cached frame path and swap the sequncing field characters            
            var pathCharArray = CachedFramePath.ToCharArray();
            var characterPosition = SequencingFieldPosition;
            foreach(var character in sequencingField)
                pathCharArray[(int)characterPosition++] = character;
            path = new string(pathCharArray);

            return Error.None;
        }

        public Error GetFrameNumber(string dngFramePath, out uint frameNumber)
        {
            // Extract the sequencing field from the path
            string sequencingField = dngFramePath.Substring((int)SequencingFieldPosition, (int)SequencingFieldLength);
            return uint.TryParse(sequencingField, out frameNumber) ? Error.None : Error.BadFrameIndex;
        }

        public override Error Validate()
        {
            // Check path is a folder
            if (!System.IO.Directory.Exists(Path))
                return Error.BadPath;

            // Check path has sequenceable DNGs
            try
            {
                var dngFiles = System.IO.Directory.EnumerateFiles(Path, "*.dng", System.IO.SearchOption.TopDirectoryOnly);

                // Determine the sequencing field
                // Travel backwards from where digits start to where digits end
                if (dngFiles.Any())
                {
                    var dngPath = dngFiles.First();
                    uint? sequenceEndPosition = null;
                    for (int i = dngPath.Length - 1; i >= 0; i--)
                    {
                        var character = dngPath[i];
                        bool isDigit = Char.IsDigit(character);
                        if (isDigit && sequenceEndPosition == null)
                            sequenceEndPosition = (uint)i + 1;
                        if (!isDigit && sequenceEndPosition != null)
                        {
                            SequencingFieldPosition = (uint)i + 1;
                            SequencingFieldLength = sequenceEndPosition.Value - SequencingFieldPosition;
                            CachedFramePath = dngPath;
                            Valid = true;
                            return Error.None;
                        }
                    }
                }

                Valid = false;
                return Error.NoVideoStream;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Failed to validate CinemaDNG sequence path: " + Path + "\n" + e.Message);
                return Error.BadPath;
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Octopus.Player.Core
{
	public class ClipCinemaDNG : Clip
	{
        public override Essence Essence { get { return Essence.Sequence; } }

        public override IClip NextClip
        {
            get
            {
                var clips = EnumerateAdditionalClips();
                if (clips != null)
                {
                    for (int i = 0; i < clips.Count; i++)
                    {
                        if (clips[i].Path == Path && (i+1) < clips.Count)
                            return clips[i+1];
                    }
                }
                return null;
            }
        }
        public override IClip PreviousClip
        {
            get
            {
                var clips = EnumerateAdditionalClips();
                if (clips != null)
                {
                    for (int i = 0; i < clips.Count; i++)
                    {
                        if (clips[i].Path == Path && (i - 1) >= 0)
                            return clips[i - 1];
                    }
                }
                return null;
            }
        }

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

        private List<IClip> EnumerateAdditionalClips()
        {
            var parentFolder = System.IO.Directory.GetParent(Path);
            if (parentFolder == null)
                return null;

            IEnumerable<string> folders = null;
            try
            {
                folders = System.IO.Directory.EnumerateDirectories(parentFolder.FullName, "*", System.IO.SearchOption.TopDirectoryOnly).OrderBy(f => f);
            }
            catch(Exception e)
            {
                Trace.WriteLine("Could not enumerate directories in: '" + parentFolder.FullName + "'\n" + e.Message);
                return null;
            }

            var clips = new List<IClip>();
            foreach(var folder in folders)
            {
                if (!System.IO.Directory.Exists(folder))
                    continue;
                try
                {
                    if (System.IO.Directory.EnumerateFiles(folder, "*.dng", System.IO.SearchOption.TopDirectoryOnly).Any())
                        clips.Add(new ClipCinemaDNG(folder));
                }
                catch(Exception e)
                {
                    Trace.WriteLine("Could not enumerate files in: '" + folder + "'\n" + e.Message);
                }
            }
            return clips;
        }

        public override Error Validate()
        {
            // Check path is a folder
            if (!System.IO.Directory.Exists(Path))
                return Error.BadPath;

            // Check path has sequenceable DNGs
            try
            {
                var dngFiles = System.IO.Directory.EnumerateFiles(Path, "*.dng", System.IO.SearchOption.TopDirectoryOnly).Where(f => !System.IO.Path.GetFileName(f).StartsWith("._")).OrderBy(f => f);

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


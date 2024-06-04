using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                if (nextClip == null)
                    nextClip = AdjacentClip(false);

                return nextClip;
            }
        }

        public override IClip PreviousClip
        {
            get
            {
                if (previousClip == null)
                    previousClip = AdjacentClip(true);

                return previousClip;
            }
        }

        public string FirstFrame { get; private set; }
        public string LastFrame { get; private set; }

        private IClip nextClip;
        private IClip previousClip;

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

        private bool FolderHasDNG(string folder)
        {
            try
            {
                return Directory.EnumerateFiles(folder, "*.dng", SearchOption.TopDirectoryOnly).Any();
            }
            catch (Exception e)
            {
                Trace.WriteLine("Could not enumerate files in: '" + folder + "'\n" + e.Message);
                return false;
            }
        }

        private IClip AdjacentClip(bool previous)
        {
            var parentFolder = Directory.GetParent(Path);
            if (parentFolder == null)
                return null;

            IEnumerable<string> folders = null;
            try
            {
                folders = Directory.EnumerateDirectories(parentFolder.FullName, "*", SearchOption.TopDirectoryOnly).OrderBy(f => f);
            }
            catch (Exception e)
            {
                Trace.WriteLine("Could not enumerate directories in: '" + parentFolder.FullName + "'\n" + e.Message);
                return null;
            }

            int? clipIndex = null;
            for(int i = 0; i < folders.Count(); i++)
            {
                if (folders.ElementAt(i) == Path)
                {
                    clipIndex = i;
                    break;
                }
            }

            if ( clipIndex.HasValue )
            {
                if (previous)
                {
                    for (int i = clipIndex.Value - 1; i >= 0; i++)
                    {
                        var folder = folders.ElementAt(i);
                        if (FolderHasDNG(folder))
                            return new ClipCinemaDNG(folder);
                    }
                }
                else
                {
                    for (int i = clipIndex.Value + 1; i < folders.Count(); i++)
                    {
                        var folder = folders.ElementAt(i);
                        if (FolderHasDNG(folder))
                            return new ClipCinemaDNG(folder);
                    }
                }
            }

            return null;
        }
        
        public override Error Validate()
        {
            // Check path is a folder
            if (!System.IO.Directory.Exists(Path))
                return Error.BadPath;

            // Check path has sequenceable DNGs
            try
            {
                var dngFiles = Directory.EnumerateFiles(Path, "*.dng", SearchOption.TopDirectoryOnly).Where(f => !System.IO.Path.GetFileName(f).StartsWith("._"));

                // Determine the sequencing field
                // Travel backwards from where digits start to where digits end
                var dngPath = dngFiles.Min();
                uint? sequenceEndPosition = null;
                for (int i = dngPath.Length - 1; i >= 0; i--)
                {
                    var character = dngPath[i];
                    bool isDigit = char.IsDigit(character);
                    if (isDigit && sequenceEndPosition == null)
                        sequenceEndPosition = (uint)i + 1;
                    if (!isDigit && sequenceEndPosition != null)
                    {
                        SequencingFieldPosition = (uint)i + 1;
                        SequencingFieldLength = sequenceEndPosition.Value - SequencingFieldPosition;
                        CachedFramePath = dngPath;
                        FirstFrame = dngPath;
                        LastFrame = dngFiles.Max();
                        Valid = true;
                        return Error.None;
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


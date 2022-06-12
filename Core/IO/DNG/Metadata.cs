﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Octopus.Player.Core.IO.DNG
{
    public class MetadataCinemaDNG : Metadata
    {
        public uint FirstFrame { get; private set; }
        public uint LastFrame { get; private set; }

        public MetadataCinemaDNG(Reader reader, Playback.ClipCinemaDNG clip)
        {
            // Assign from the reader
            Dimensions = reader.Dimensions;
            Framerate = reader.Framerate;
            BitDepth = reader.BitDepth;

            // Duration in frames is the sequencing field of the last frame subtracted by the first frame index
            var dngSortedFrames = System.IO.Directory.EnumerateFiles(clip.Path, "*.dng", System.IO.SearchOption.TopDirectoryOnly).OrderBy(f => f);
            uint firstFrameNumber;
            uint lastFrameNumber;
            if (clip.GetFrameNumber(dngSortedFrames.First(), out firstFrameNumber) == Error.None && clip.GetFrameNumber(dngSortedFrames.Last(), out lastFrameNumber) == Error.None)
            {
                FirstFrame = firstFrameNumber;
                LastFrame = lastFrameNumber;
                DurationFrames = (uint)((int)LastFrame - (int)FirstFrame) + 1;
            }
            else
                Trace.WriteLine("Warning, failed to determine CinemaDNG sequence duration for clip: " + clip.Path);
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.IO.DNG
{
    public enum Compression
    {
        Unknown = -1,
        None = 1,
        LJ92 = 7
    }

    public enum PhotometricInterpretation
    {
        Unknown = -1,
        ColorFilterArray = 32803,
        LinearRaw = 34892
    }

    public class MetadataCinemaDNG : Metadata
    {
        public uint FirstFrame { get; private set; }
        public uint LastFrame { get; private set; }
        public Vector2i CFARepeatPatternDimensions { get; private set; }
        public CFAPattern CFAPattern { get; private set; }
        public Compression Compression { get; private set; }
        public uint TileCount { get; private set; }
        public Vector2i TileDimensions { get; private set; }
        public ushort[] LinearizationTable { get; private set; }
        public ushort BlackLevel { get; private set; }
        public ushort WhiteLevel { get; private set; }

        public MetadataCinemaDNG(Reader reader, Playback.ClipCinemaDNG clip)
        {
            // Assign from the reader
            Dimensions = reader.Dimensions;
            Framerate = reader.Framerate;
            BitDepth = reader.BitDepth;
            DecodedBitDepth = reader.DecodedBitDepth;
            CFAPattern = reader.CFAPattern;
            CFARepeatPatternDimensions = reader.CFARepeatPatternDimensions;
            Compression = reader.Compression;
            TileCount = reader.IsTiled ? reader.TileCount : 0;
            TileDimensions = reader.IsTiled ? reader.TileDimensions : new Vector2i(0, 0);
            LinearizationTable = reader.LinearizationTable;

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

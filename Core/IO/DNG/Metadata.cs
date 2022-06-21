using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.IO.DNG
{
    public enum Compression
    {
        Unknown = -1,
        None = 1,
        LosslessJPEG = 7
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
        public Matrix3? ColorMatrix1 { get; private set; }
        public Matrix3? ColorMatrix2 { get; private set; }
        public Matrix3? ForwardMatrix1 { get; private set; }
        public Matrix3? ForwardMatrix2 { get; private set; }
        public bool IsDualIlluminant { get; private set; }
        public bool HasForwardMatrix { get; private set; }
        public bool Monochrome { get; private set; }

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
            BlackLevel = reader.BlackLevel;
            WhiteLevel = reader.WhiteLevel;
            IsDualIlluminant = reader.IsDualIlluminant;
            HasForwardMatrix = reader.HasForwardMatrix;
            Monochrome = reader.Monochrome;
            if (!reader.Monochrome)
            {
                ColorMatrix1 = reader.ColorMatrix1;
                if ( reader.IsDualIlluminant )
                    ColorMatrix2 = reader.ColorMatrix2;
                if (reader.HasForwardMatrix)
                {
                    ForwardMatrix1 = reader.ForwardMatrix1;
                    if (reader.IsDualIlluminant)
                        ForwardMatrix2 = reader.ForwardMatrix2;
                }
            }

            // Title is just the path without the parent folders
            Title = Path.GetFileName(clip.Path);

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

        public override string ToString()
        {
            string text = base.ToString() + "\nCinemaDNG\n-------------\n";

            var properties = GetType().GetProperties(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var property in properties)
            {
                switch (property.Name)
                {
                    case "LinearizationTable":
                        text += "Linearization Table: " + ((LinearizationTable == null || LinearizationTable.Length==0) ? "None\n" : LinearizationTable.Length + " entries\n");
                        break;
                    default:
                        text += Regex.Replace(property.Name, "(\\B[A-Z])", " $1") + ": " + property.GetValue(this, null) + "\n";
                        break;
                }
            }

            return text;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Octopus.Player.Core.Maths;
using Octopus.Player.GPU;
using OpenTK.Mathematics;
using TiffLibrary;

namespace Octopus.Player.Core.IO.DNG
{
    public enum Compression
    {
        Unknown = -1,
        None = 1,
        Jpeg = 7
    }

    public enum PhotometricInterpretation
    {
        Unknown = -1,
        ColorFilterArray = 32803,
        LinearRaw = 34892
    }

    public static partial class Extensions
    {
        public static Orientation Orientation(this TiffOrientation orientation)
        {
            switch (orientation)
            {
                case TiffOrientation.TopLeft:
                    return GPU.Orientation.TopLeft;
                case TiffOrientation.TopRight:
                    return GPU.Orientation.TopRight;
                case TiffOrientation.BottomRight:
                    return GPU.Orientation.BottomRight;
                case TiffOrientation.BottomLeft:
                    return GPU.Orientation.BottomLeft;
                case TiffOrientation.LeftTop:
                    return GPU.Orientation.LeftTop;
                case TiffOrientation.RightTop:
                    return GPU.Orientation.RightTop;
                case TiffOrientation.RightBottom:
                    return GPU.Orientation.RightBottom;
                case TiffOrientation.LeftBottom:
                    return GPU.Orientation.LeftBottom;
                default:
                    throw new ArgumentException("Unknown tiff orientation");
            }
        }
    }

    public class MetadataCinemaDNG : Metadata
    {
        public uint FirstFrame { get; private set; }
        public uint LastFrame { get; private set; }
        public Vector2i CFARepeatPatternDimensions { get; private set; }
        public CFAPattern CFAPattern { get; private set; }
        public Compression Compression { get; private set; }
        public bool IsLossy { get; private set; }
        public uint TileCount { get; private set; }
        public Vector2i TileDimensions { get; private set; }
        public ushort[] LinearizationTable { get; private set; }
        public ushort BlackLevel { get; private set; }
        public ushort WhiteLevel { get; private set; }
        public bool Monochrome { get; private set; }
        public string UniqueCameraModel { get; private set; }
        public Vector2 PixelAspectRatio { get; private set; }
        public Vector4i ActiveArea { get; private set; }

        public override Rational AspectRatio
        {
            get
            {
                var ratio = new Rational((int)((float)base.AspectRatio.Numerator * PixelAspectRatio.X),
                    (int)((float)base.AspectRatio.Denominator * PixelAspectRatio.Y));
                return Orientation.IsTransposed() ? ratio.Transpose() : ratio;
            }
        }

        public MetadataCinemaDNG(Reader reader, ClipCinemaDNG clip)
        {
            // Assign from the reader
            Dimensions = reader.Dimensions;
            PaddedDimensions = reader.PaddedDimensions;
            Framerate = reader.ContainsFramerate ? reader.Framerate : (Maths.Rational?)null;
            BitDepth = reader.BitDepth;
            DecodedBitDepth = reader.DecodedBitDepth;
            CFAPattern = reader.CFAPattern;
            CFARepeatPatternDimensions = reader.CFARepeatPatternDimensions;
            Compression = reader.Compression;
            IsLossy = reader.IsLossy;
            TileCount = reader.IsTiled ? reader.TileCount : 0;
            TileDimensions = reader.IsTiled ? reader.TileDimensions : new Vector2i(0, 0);
            LinearizationTable = reader.LinearizationTable;
            BlackLevel = reader.BlackLevel;
            WhiteLevel = reader.WhiteLevel;
            Monochrome = reader.Monochrome;
            if (!reader.Monochrome)
                ColorProfile = new Maths.Color.Profile(reader);
            ExposureValue = reader.BaselineExposure;
            UniqueCameraModel = reader.UniqueCameraModel;
            if (reader.ContainsTimeCode)
                StartTimeCode = reader.TimeCode;
            PixelAspectRatio = reader.ContainsDefaultScale ? reader.DefaultScale : new Vector2(1, 1);
            if (reader.ContainsDefaultCropOrigin && reader.ContainsDefaultCropSize)
                DefaultCrop = new Vector4i(reader.DefaultCropOrigin, reader.DefaultCropSize);
            ActiveArea = reader.ContainsActiveArea ? reader.ActiveArea : new Vector4i(0,0, reader.Dimensions.Y, reader.Dimensions.X);
            Orientation = reader.Orientation.Orientation();

            // Title is just the path without the parent folders
            Title = Path.GetFileName(clip.Path);

            // Duration in frames is the sequencing field of the last frame subtracted by the first frame index
            if (clip.GetFrameNumber(clip.FirstFrame, out uint firstFrameNumber) == Error.None &&
                clip.GetFrameNumber(clip.LastFrame, out uint lastFrameNumber) == Error.None)
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
            string text = base.ToString() + "\n-------------\nCinemaDNG\n-------------\n";

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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Octopus.Player.Core.Maths.Color;
using OpenTK.Mathematics;

namespace Octopus.Player.Core
{
    public enum HighlightRecovery
    {
        Off,
        On
    }

    public enum HighlightRollOff
    {
        Off,
        Low,
        Medium,
        High
    }

    public enum ToneMappingOperator
    {
        None,
        SDR
    }

    public enum GamutCompression
    {
        Off,
        Rec709
    }

    public enum GammaSpace
    {
        Rec709,
        sRGB,
        LogC3,
        Log3G10,
        FilmGen5
    }

    public static partial class Extensions
    {
        public static bool IsLog(this GammaSpace gamma)
        {
            switch (gamma)
            {
                case GammaSpace.LogC3:
                case GammaSpace.sRGB:
                case GammaSpace.FilmGen5:
                    return true;
                default:
                    return false;
            }
        }

        public static Matrix3 ColourSpaceTransformD50(this GammaSpace gamma)
        {
            switch (gamma)
            {
                case GammaSpace.Rec709:
                case GammaSpace.sRGB:
                    return Matrix.XYZToRec709D50();
                case GammaSpace.LogC3:
                    return Matrix.XYZtoAlexaWideGamutD50();
                default:
                    return Matrix.XYZToRec709D50();
            }
        }
    }

    public struct RawParameters
    {
        public float? exposure;
        public ToneMappingOperator? toneMappingOperator;
        public GammaSpace? gammaSpace;

        // Colour only
        public Tuple<float, float> whiteBalance;
        public HighlightRecovery? highlightRecovery;
        public HighlightRollOff? highlightRollOff;
        public GamutCompression? gamutCompression;
    }
}

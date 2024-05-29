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
        public static string DefaultLutName(this GammaSpace gamma)
        {
            switch (gamma)
            {
                case GammaSpace.LogC3:
                    return "ARRI LogC3 to Rec. 709";
                case GammaSpace.Log3G10:
                    return "RED Log3G10 to Rec. 709";
                case GammaSpace.FilmGen5:
                    return "Blackmagic Film Gen. 5 to Rec. 709";
                default:
                    return null;
            }
        }

        public static string DefaultLutResource(this GammaSpace gamma)
        {
            switch (gamma)
            {
                case GammaSpace.LogC3:
                    return "Arri Alexa LogC3 to Rec709.dat";
                case GammaSpace.Log3G10:
                    return "RWG_Log3G10_to_REC709_BT1886_with_LOW_CONTRAST_and_R_3_Soft_size_33.cube";
                case GammaSpace.FilmGen5:
                    return "Blackmagic Gen 5 Film to Video.cube";
                default:
                    return null;
            }
        }

        public static bool IsLog(this GammaSpace gamma)
        {
            switch (gamma)
            {
                case GammaSpace.LogC3:
                case GammaSpace.Log3G10:
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

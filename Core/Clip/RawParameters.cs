using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

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
        LogC
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

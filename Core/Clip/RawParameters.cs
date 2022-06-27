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
        Off = -1,
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

    public struct RawParameters
    {
        public float? exposure;
        public ToneMappingOperator? toneMappingOperator;

        // Colour only
        public Tuple<float, float> whiteBalance;
        public HighlightRecovery? highlightRecovery;
        public HighlightRollOff? highlightRollOff;
        public GamutCompression? gamutCompression;
    }
}

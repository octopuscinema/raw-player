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

    public enum ToneMappingOperator
    {
        None,
        SDR
    }

    public enum GamutCompression
    {
        None,
        Rec709
    }

    public struct RawParameters
    {
        public float? exposure;
        public ToneMappingOperator? toneMappingOperator;

        // Colour only
        public Tuple<float, float> whiteBalance;
        public HighlightRecovery? highlightRecovery;
        public GamutCompression? gamutCompression;
    }
}

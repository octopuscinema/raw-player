using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace Octopus.Player.Core.Maths.Color
{
    public static class WhiteBalance
    {
        public static Dictionary<string, Tuple<float, float>> Presets { get { return presets; } }
        public static Tuple<float, float> Default { get { return Presets["whiteBalanceDaylight"]; } }

        static readonly Dictionary<string, Tuple<float, float>> presets = new Dictionary<string, Tuple<float, float>>()
        {         
            { "whiteBalanceAsShot", null },
            { "whiteBalanceShade", new Tuple<float, float>(     7500.0f, 10.0f) },
            { "whiteBalanceCloud", new Tuple<float, float>(     6500.0f, 10.0f) },
            { "whiteBalanceDaylight", new Tuple<float, float>(  5500.0f, 10.0f) },
            { "whiteBalanceFluorescent", new Tuple<float,float>(3800.0f, 21.0f) },
            { "whiteBalanceTungsten", new Tuple<float, float>(  3200.0f, 0.0f) }
        };
    }
}
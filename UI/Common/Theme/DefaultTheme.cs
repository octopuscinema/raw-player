using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.UI
{
    public struct DefaultTheme : ITheme
    {
        public Vector3 ClipBackground { get { return new Vector3(0, 0, 0); } }
        public Vector3 EmptyBackground { get { return new Vector3(0.9f, 0.9f, 0.9f); } }

        public Vector3 LabelColour { get { return new Vector3(0.9f, 0.9f, 0.9f); } }

        public Vector3 MissingFrameColour { get { return new Vector3(1, 0, 0); } }
        public Vector3 SkippedFrameColour { get { return new Vector3(1, 0.5f, 0); } }

        public float DefaultOpacity { get { return 1.0f; } }
        public float DisabledOpacity { get { return 0.5f; } }

        public float PlaybackControlsMargin { get { return 20.0f; } }
        public TimeSpan PlaybackControlsAnimation { get { return TimeSpan.FromSeconds(0.25); } }
    }
}

using OpenTK.Mathematics;
using System;
using System.Windows;

namespace Octopus.Player.UI.Windows
{
    public struct DefaultWindowsTheme : ITheme
    {
        public Vector3 ClipBackground { get { return new Vector3(0, 0, 0); } }
        public Vector3 EmptyBackground { get { return new Vector3(SystemColors.MenuBarColor.R, SystemColors.MenuBarColor.G, SystemColors.MenuBarColor.B) / 255.0f; } }

        public Vector3 LabelColour { get { return new Vector3(System.Drawing.Color.LightGray.R, System.Drawing.Color.LightGray.G, System.Drawing.Color.LightGray.B) / 255.0f; } }

        public Vector3 MissingFrameColour { get { return new Vector3(1, 0, 0); } }
        public Vector3 SkippedFrameColour { get { return new Vector3(1, 0.5f, 0); } }

        public float DefaultOpacity { get { return 1.0f; } }
        public float DisabledOpacity { get { return 0.5f; } }

        public float PlaybackControlsMargin { get { return 20.0f; } }
        public TimeSpan ControlsAnimation { get { return TimeSpan.FromSeconds(0.25); } }
    }
}

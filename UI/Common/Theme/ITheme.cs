using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.UI
{
    public interface ITheme
    {
        Vector3 ClipBackground { get; }
        Vector3 EmptyBackground { get; }

        Vector3 LabelColour { get; }
        Vector3 MissingFrameColour { get; }
        Vector3 SkippedFrameColour { get; }

        float DefaultOpacity { get; }
        float DisabledOpacity { get; }

        float DropAreaOpacityMargin { get; }

        float PlaybackControlsMargin { get; }
        TimeSpan ControlsAnimation { get; }
        TimeSpan ControlsAnimationDelay { get; }
    }
}

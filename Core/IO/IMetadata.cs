using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.IO
{
    public interface IMetadata
    {
        uint DurationFrames { get; }

        Maths.Rational Framerate { get; }

        Vector2i Dimensions { get; }
        uint BitDepth { get; }
        uint DecodedBitDepth { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Player.Core.Maths;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.IO
{
    public interface IMetadata
    {
        string Title { get; }
        uint DurationFrames { get; }

        Maths.Rational? Framerate { get; }
        SMPTETimeCode? StartTimeCode { get; }
        Vector4i? ActiveSensorArea { get; }
        Vector2i Dimensions { get; }
        Vector2i PaddedDimensions { get; }
        Rational AspectRatio { get; }
        uint BitDepth { get; }
        uint DecodedBitDepth { get; }
        float ExposureValue { get; }
        Core.Maths.Color.Profile? ColorProfile { get; }
    }
}

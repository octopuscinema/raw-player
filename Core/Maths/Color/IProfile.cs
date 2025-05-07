using OpenTK.Mathematics;
using System;

namespace Octopus.Player.Core.Maths.Color
{
    public enum WhitePoint
    {
        D50,
        D65
    }

    public interface IProfile
    {
        Tuple<float, float> AsShotWhiteBalance { get; }
        bool HasAsShotMetadata { get; }
        WhitePoint WhitePoint { get; }
        Matrix3 CalculateCameraToXYZ(Tuple<float, float>? whiteBalance);
        Matrix3 CalculateCameraToXYZ(Vector2? whiteXY = null);
    }
}
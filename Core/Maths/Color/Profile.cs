using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Maths.Color
{
    public struct Profile
    {
        public Vector2 asShotWhiteXY;
        public Illuminant calibrationIlluminant1;
        public Illuminant calibrationIlluminant2;
        public Matrix3 colorMatrix1;
        public Matrix3 colorMatrix2;
        public Matrix3 forwardMatrix1;
        public Matrix3 forwardMatrix2;
        public bool isDualIlluminant;
        public bool hasForwardMatrix;
    }
}

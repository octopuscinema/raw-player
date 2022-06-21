using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Maths
{
    public static partial class Extensions
    {
        public static float MaxEntry(this Vector3 vector)
        {
            return Math.Max(Math.Max(vector.X, vector.Y), vector.Z);
        }

        public static Vector3 LinearInterpolate(this Vector3 vector, in Vector3 to, float lerp)
        {
            return vector + (to - vector) * lerp;
        }
    }
}
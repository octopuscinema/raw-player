using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Maths
{
    public static partial class Extensions
    {
        public static int[] ToArray(this Vector2i vector)
        {
            return new int[] { vector.X, vector.Y };
        }
        public static int[] ToArray(this Vector3i vector)
        {
            return new int[] { vector.X, vector.Y, vector.Z };
        }

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
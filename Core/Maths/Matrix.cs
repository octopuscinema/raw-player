using System.Diagnostics;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.Maths
{
    public static partial class Extensions
    {
        public static void Scale(this Matrix3 matrix, float factor)
        {
            matrix.Row0 *= factor;
            matrix.Row1 *= factor;
            matrix.Row2 *= factor;
        }

        public static Matrix3 LinearInterpolate(this Matrix3 matrix, in Matrix3 to, float lerp)
		{
            Matrix3 result = new Matrix3();
            result.Row0 = matrix.Row0.LinearInterpolate(to.Row0, lerp);
            result.Row1 = matrix.Row1.LinearInterpolate(to.Row1, lerp);
            result.Row2 = matrix.Row2.LinearInterpolate(to.Row2, lerp);
			return result;
        }

        public static float[] ToArray(this Matrix3 matrix)
        {
            return new float[] { matrix.Row0[0], matrix.Row0[1], matrix.Row0[2],
                matrix.Row1[0], matrix.Row1[1], matrix.Row1[2],
                matrix.Row2[0], matrix.Row2[1], matrix.Row2[2]
            };
        }
    }

    public static class CreateMatrix3
    {
        public static Matrix3 Diagonal(in Vector3 diagonal)
        {
            return new Matrix3(new Vector3(diagonal.X, 0, 0),
                new Vector3(0, diagonal.Y, 0),
                new Vector3(0, 0, diagonal.Z));
        }
    }
}
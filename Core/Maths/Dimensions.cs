using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Maths
{
    // Needs c# language version 10
    //global using Dimensions = Vector2i;

    public static class Extensions
    {
        public static int Area(this Vector2i vector)
        {
            return vector.X * vector.Y;
        }

        public static void FitAspectRatio(this Vector2i vector, Rational aspectRatio, out Vector2i position, out Vector2i size)
        {
            var aspectRatioFlt = aspectRatio.ToSingle();
            var containerSize = vector;
            var containerAspectRatio = (float)containerSize.X / (float)containerSize.Y;

            // If aspect ratio is wider than container aspect ratio, scale down the height
            // Otherwise scale down the width
            if (aspectRatioFlt > containerAspectRatio)
            {
                size.X = containerSize.X;
                size.Y = (int)((float)size.X / aspectRatioFlt);
            }
            else
            {
                size.Y = containerSize.Y;
                size.X = (int)((float)size.Y * aspectRatioFlt);
            }

            // Centre in the container
            var containerTopLeft = new Vector2i(0, 0);
            position = containerTopLeft + (Vector2i)(containerSize.ToVector2() * 0.5f - size.ToVector2() * 0.5f);
        }
    }
}

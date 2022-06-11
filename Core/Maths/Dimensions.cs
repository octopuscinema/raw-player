using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Maths
{
    // Needs c# language version 10
    //global using Dimensions = Vector2I;

    public static class Extensions
    {
        public static int Area(this Vector2i vector)
        {
            return vector.X * vector.Y;
        }
    }
}

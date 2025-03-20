using System;
using CoreGraphics;

namespace Octopus.Player.UI.macOS
{
    public static partial class Extensions
    {
        // Extension method to calculate the shortest distance between two CGRects
        public static double DistanceTo(this CGRect rect1, CGRect rect2)
        {
            // Calculate the horizontal distance between the edges of the rectangles
            double dx = 0;
            if (rect1.Right < rect2.Left)
                dx = rect2.Left - rect1.Right;
            else if (rect2.Right < rect1.Left)
                dx = rect1.Left - rect2.Right;

            // Calculate the vertical distance between the edges of the rectangles
            double dy = 0;
            if (rect1.Bottom < rect2.Top)
                dy = rect2.Top - rect1.Bottom;
            else if (rect2.Bottom < rect1.Top)
                dy = rect1.Top - rect2.Bottom;

            // If dx and dy are both 0, the rectangles overlap, so return 0
            if (dx == 0 && dy == 0)
                return 0;

            // If the rectangles are not overlapping, return the diagonal distance
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
namespace Octopus.Player.GPU
{
    public enum Orientation
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        LeftBottom,
        LeftTop,
        RightTop,
        RightBottom
    }

    public static partial class Extensions
    {
        public static bool IsTransposed(this Orientation orientation)
        {
            switch (orientation)
            {
                case Orientation.LeftTop:
                case Orientation.LeftBottom:
                case Orientation.RightTop:
                case Orientation.RightBottom:
                    return true;
                default:
                    return false;
            }
        }
    }
}
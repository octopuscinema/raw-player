using Octopus.Player.Core.Maths;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Octopus.Player.Core.IO
{
    public enum CFAPattern
    {
        None,
        RGGB,
        BGGR,
        GBRG,
        GRBG,
        Unknown
    }

    public abstract class Metadata : IMetadata
    {
        public string Title { get; protected set; }
        public uint DurationFrames { get; protected set; }
        public Vector2i Dimensions { get; protected set; }
        public Maths.Rational? Framerate { get; protected set; }
        public SMPTETimeCode? StartTimeCode { get; protected set; }
        public virtual Rational AspectRatio { get { return new Rational(Dimensions.X, Dimensions.Y); } }
        public uint BitDepth { get; protected set; }
        public uint DecodedBitDepth { get; protected set; }
        public float ExposureValue { get; protected set; }
        public Core.Maths.Color.Profile? ColorProfile { get; protected set; }

        public override string ToString()
        {
            string text = "";
            var properties = typeof(Metadata).GetProperties(System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var property in properties)
            {
                switch (property.Name)
                {
                    case "ColorProfile":
                        if (ColorProfile.HasValue)
                            text += "\n-------------\nColor Profile\n-------------" + ColorProfile;
                        break;
                    default:
                        text += Regex.Replace(property.Name, "(\\B[A-Z])", " $1") + ": " + property.GetValue(this, null) + "\n";
                        break;
                }
            }

            return text;
        }
    }
}

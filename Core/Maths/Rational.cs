using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Maths
{
    public readonly struct Rational : IEquatable<Rational>
    {
        public int Numerator { get; }

        public int Denominator { get; }

        public Rational(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }

        public double ToSingle()
        {
            return Numerator / (double)Denominator;
        }

        public double ToDouble()
        {
            return Numerator / (double)Denominator;
        }

        /// <inheritdoc />
        public bool Equals(Rational other)
        {
            return Numerator == other.Numerator && Denominator == other.Denominator;
        }

        public override bool Equals(object obj)
        {
            return (obj is Rational other && Equals(other));
        }

        public static bool operator ==(Rational left, Rational right) => left.Equals(right);

        public static bool operator !=(Rational left, Rational right) => !left.Equals(right);

        public override int GetHashCode()
        {
            return HashCode.Combine(Numerator, Denominator);
        }

        public override string ToString()
        {
            return Numerator.ToString() + "/" + Denominator.ToString();
        }
    }
}

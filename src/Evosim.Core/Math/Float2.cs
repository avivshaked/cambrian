using System;
using System.Globalization;

namespace Evosim.Core
{
    /// <summary>
    /// Two floats. Used for per-DOF joint limits (min, max) — DESIGN.md §4.1.
    /// </summary>
    /// <remarks>
    /// Evosim.Core carries no UnityEngine dependency (DESIGN.md §6.1), so the small
    /// amount of vector maths it needs lives here rather than coming from UnityEngine.
    /// </remarks>
    public readonly struct Float2 : IEquatable<Float2>
    {
        public readonly float X;
        public readonly float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Float2 Zero = new Float2(0f, 0f);

        /// <summary>Interprets the pair as (min, max) and returns whether it is ordered.</summary>
        public bool IsOrderedRange => X <= Y;

        /// <summary>Clamps <paramref name="value"/> into this pair read as (min, max).</summary>
        public float Clamp(float value) => value < X ? X : (value > Y ? Y : value);

        public bool Equals(Float2 other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object obj) => obj is Float2 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.####}, {1:0.####})", X, Y);
    }
}

using System;
using System.Globalization;

namespace Evosim.Core
{
    /// <summary>
    /// Three floats: box half-extents, anchors, scale factors, positions — DESIGN.md §4.1.
    /// </summary>
    public readonly struct Float3 : IEquatable<Float3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Float3(float uniform) : this(uniform, uniform, uniform) { }

        public static readonly Float3 Zero = new Float3(0f, 0f, 0f);
        public static readonly Float3 One = new Float3(1f, 1f, 1f);

        public float this[int axis]
        {
            get
            {
                switch (axis)
                {
                    case 0: return X;
                    case 1: return Y;
                    case 2: return Z;
                    default: throw new IndexOutOfRangeException("Axis must be 0, 1 or 2.");
                }
            }
        }

        public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 operator -(Float3 a) => new Float3(-a.X, -a.Y, -a.Z);
        public static Float3 operator *(Float3 a, float s) => new Float3(a.X * s, a.Y * s, a.Z * s);
        public static Float3 operator *(float s, Float3 a) => a * s;

        /// <summary>Componentwise product. Scale in DESIGN.md §4.1 is per-axis, not uniform.</summary>
        public static Float3 operator *(Float3 a, Float3 b) => new Float3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

        public static Float3 Abs(Float3 a) =>
            new Float3(System.Math.Abs(a.X), System.Math.Abs(a.Y), System.Math.Abs(a.Z));

        public static float Dot(Float3 a, Float3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Float3 Cross(Float3 a, Float3 b) => new Float3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        public float SqrMagnitude => X * X + Y * Y + Z * Z;

        public float Magnitude => (float)System.Math.Sqrt(SqrMagnitude);

        public Float3 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-12f ? this * (1f / m) : Zero;
            }
        }

        /// <summary>Volume of the box with these half-extents. Guard for DESIGN.md §4.2's minimum-volume rule.</summary>
        public float BoxVolume => 8f * System.Math.Abs(X) * System.Math.Abs(Y) * System.Math.Abs(Z);

        public bool IsFinite =>
            !(float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z) ||
              float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z));

        public bool Equals(Float3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is Float3 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = X.GetHashCode();
                h = (h * 397) ^ Y.GetHashCode();
                h = (h * 397) ^ Z.GetHashCode();
                return h;
            }
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.####}, {1:0.####}, {2:0.####})", X, Y, Z);
    }
}

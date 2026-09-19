using System;

namespace Evosim.Core
{
    /// <summary>
    /// A point in double precision — the one place in Core that is not float.
    /// </summary>
    /// <remarks>
    /// <b>Why it exists at all, when everything else here is <see cref="Float3"/>.</b> The convex
    /// hull in <see cref="ConvexHull"/> asks whether a point is on the far side of a plane, and
    /// the answer decides which faces die. In float, two corners a millimetre apart on a body a
    /// metre across sit close enough to the plane that the sign of that test is noise, and a hull
    /// that keeps a face it should have dropped is not a hull. The cloud is built from float
    /// geometry and widened once, so nothing is claimed about precision the parts do not have;
    /// the width buys the comparisons, not the input.
    /// </remarks>
    public readonly struct Double3
    {
        public readonly double X, Y, Z;

        public Double3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static Double3 operator -(Double3 a, Double3 b) =>
            new Double3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Double3 operator +(Double3 a, Double3 b) =>
            new Double3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Double3 operator *(Double3 a, double s) =>
            new Double3(a.X * s, a.Y * s, a.Z * s);

        public static double Dot(Double3 a, Double3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Double3 Cross(Double3 a, Double3 b) => new Double3(
            a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public override string ToString() => X + " " + Y + " " + Z;
    }
}

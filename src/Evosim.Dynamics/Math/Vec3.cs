using System;

namespace Evosim.Dynamics
{
    /// <summary>
    /// A double-precision 3-vector. The solver's whole arithmetic is <c>double</c>: the spike's
    /// question is whether a reduced-coordinate step is stable where PhysX's was not, and
    /// single precision is one of the things that would confound the answer.
    /// </summary>
    public readonly struct Vec3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);
        public static readonly Vec3 UnitX = new Vec3(1, 0, 0);
        public static readonly Vec3 UnitY = new Vec3(0, 1, 0);
        public static readonly Vec3 UnitZ = new Vec3(0, 0, 1);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(double s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        public double SqrMagnitude => X * X + Y * Y + Z * Z;
        public double Magnitude => System.Math.Sqrt(SqrMagnitude);

        public Vec3 Normalized
        {
            get
            {
                double m = Magnitude;
                return m > 1e-300 ? new Vec3(X / m, Y / m, Z / m) : Zero;
            }
        }

        public bool IsFinite =>
            !(double.IsNaN(X) || double.IsNaN(Y) || double.IsNaN(Z) ||
              double.IsInfinity(X) || double.IsInfinity(Y) || double.IsInfinity(Z));

        public static Vec3 Read(double[] a, int at) => new Vec3(a[at], a[at + 1], a[at + 2]);

        public static void Write(double[] a, int at, Vec3 v)
        {
            a[at] = v.X; a[at + 1] = v.Y; a[at + 2] = v.Z;
        }

        public static void Add(double[] a, int at, Vec3 v)
        {
            a[at] += v.X; a[at + 1] += v.Y; a[at + 2] += v.Z;
        }

        public override string ToString() =>
            System.FormattableString.Invariant($"({X:0.######}, {Y:0.######}, {Z:0.######})");
    }
}

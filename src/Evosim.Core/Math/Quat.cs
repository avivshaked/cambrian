using System;
using System.Globalization;

namespace Evosim.Core
{
    /// <summary>
    /// Unit quaternion. Edge orientation in DESIGN.md §4.1, and the rotation half of a
    /// developed part's world frame.
    /// </summary>
    public readonly struct Quat : IEquatable<Quat>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static readonly Quat Identity = new Quat(0f, 0f, 0f, 1f);

        public static Quat FromAxisAngle(Float3 axis, float radians)
        {
            Float3 n = axis.Normalized;
            if (n.SqrMagnitude < 1e-24f) return Identity;
            float half = radians * 0.5f;
            float s = (float)System.Math.Sin(half);
            return new Quat(n.X * s, n.Y * s, n.Z * s, (float)System.Math.Cos(half));
        }

        /// <summary>
        /// Euler angles in radians, composed as Rz * Ry * Rx — i.e. rotate about X first,
        /// then Y, then Z, all in the fixed parent frame.
        /// </summary>
        public static Quat FromEuler(float x, float y, float z)
        {
            float hx = x * 0.5f, hy = y * 0.5f, hz = z * 0.5f;
            float cx = (float)System.Math.Cos(hx), sx = (float)System.Math.Sin(hx);
            float cy = (float)System.Math.Cos(hy), sy = (float)System.Math.Sin(hy);
            float cz = (float)System.Math.Cos(hz), sz = (float)System.Math.Sin(hz);

            return new Quat(
                sx * cy * cz - cx * sy * sz,
                cx * sy * cz + sx * cy * sz,
                cx * cy * sz - sx * sy * cz,
                cx * cy * cz + sx * sy * sz);
        }

        public static Quat operator *(Quat a, Quat b) => new Quat(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        /// <summary>Rotates <paramref name="v"/> by this quaternion.</summary>
        public Float3 Rotate(Float3 v)
        {
            Float3 q = new Float3(X, Y, Z);
            Float3 t = Float3.Cross(q, v) * 2f;
            return v + t * W + Float3.Cross(q, t);
        }

        public Quat Conjugate => new Quat(-X, -Y, -Z, W);

        public float SqrMagnitude => X * X + Y * Y + Z * Z + W * W;

        public Quat Normalized
        {
            get
            {
                float m = (float)System.Math.Sqrt(SqrMagnitude);
                if (m < 1e-12f) return Identity;
                float inv = 1f / m;
                return new Quat(X * inv, Y * inv, Z * inv, W * inv);
            }
        }

        public bool IsFinite =>
            !(float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z) || float.IsNaN(W) ||
              float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z) || float.IsInfinity(W));

        /// <summary>
        /// Builds a quaternion from a proper rotation matrix (determinant +1). Callers are
        /// responsible for having removed scale and any reflection first — see
        /// <see cref="Mat4.Decompose"/>.
        /// </summary>
        public static Quat FromRotationMatrix(Mat4 m)
        {
            float trace = m.M00 + m.M11 + m.M22;
            if (trace > 0f)
            {
                float s = (float)System.Math.Sqrt(trace + 1f) * 2f;
                return new Quat(
                    (m.M21 - m.M12) / s,
                    (m.M02 - m.M20) / s,
                    (m.M10 - m.M01) / s,
                    0.25f * s).Normalized;
            }

            if (m.M00 > m.M11 && m.M00 > m.M22)
            {
                float s = (float)System.Math.Sqrt(1f + m.M00 - m.M11 - m.M22) * 2f;
                return new Quat(
                    0.25f * s,
                    (m.M01 + m.M10) / s,
                    (m.M02 + m.M20) / s,
                    (m.M21 - m.M12) / s).Normalized;
            }

            if (m.M11 > m.M22)
            {
                float s = (float)System.Math.Sqrt(1f + m.M11 - m.M00 - m.M22) * 2f;
                return new Quat(
                    (m.M01 + m.M10) / s,
                    0.25f * s,
                    (m.M12 + m.M21) / s,
                    (m.M02 - m.M20) / s).Normalized;
            }

            {
                float s = (float)System.Math.Sqrt(1f + m.M22 - m.M00 - m.M11) * 2f;
                return new Quat(
                    (m.M02 + m.M20) / s,
                    (m.M12 + m.M21) / s,
                    0.25f * s,
                    (m.M10 - m.M01) / s).Normalized;
            }
        }

        public bool Equals(Quat other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);

        public override bool Equals(object obj) => obj is Quat other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = X.GetHashCode();
                h = (h * 397) ^ Y.GetHashCode();
                h = (h * 397) ^ Z.GetHashCode();
                h = (h * 397) ^ W.GetHashCode();
                return h;
            }
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "({0:0.####}, {1:0.####}, {2:0.####}, {3:0.####})", X, Y, Z, W);
    }
}

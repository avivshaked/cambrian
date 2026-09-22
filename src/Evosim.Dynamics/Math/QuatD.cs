using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// A double-precision unit quaternion, in Core's convention (<see cref="Quat"/>): the same
    /// multiplication order and the same rotation formula, widened.
    /// </summary>
    public readonly struct QuatD
    {
        public readonly double X, Y, Z, W;

        public QuatD(double x, double y, double z, double w) { X = x; Y = y; Z = z; W = w; }

        public static readonly QuatD Identity = new QuatD(0, 0, 0, 1);

        public static QuatD From(Quat q) => new QuatD(q.X, q.Y, q.Z, q.W);

        public static QuatD FromAxisAngle(Vec3 axis, double radians)
        {
            Vec3 n = axis.Normalized;
            if (n.SqrMagnitude < 1e-24) return Identity;
            double half = radians * 0.5;
            double s = System.Math.Sin(half);
            return new QuatD(n.X * s, n.Y * s, n.Z * s, System.Math.Cos(half));
        }

        /// <summary>Euler angles in radians, composed as Rz * Ry * Rx — Core's convention.</summary>
        public static QuatD FromEuler(double x, double y, double z)
        {
            double hx = x * 0.5, hy = y * 0.5, hz = z * 0.5;
            double cx = System.Math.Cos(hx), sx = System.Math.Sin(hx);
            double cy = System.Math.Cos(hy), sy = System.Math.Sin(hy);
            double cz = System.Math.Cos(hz), sz = System.Math.Sin(hz);

            return new QuatD(
                sx * cy * cz - cx * sy * sz,
                cx * sy * cz + sx * cy * sz,
                cx * cy * sz - sx * sy * cz,
                cx * cy * cz + sx * sy * sz);
        }

        public static QuatD operator *(QuatD a, QuatD b) => new QuatD(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        public QuatD Conjugate => new QuatD(-X, -Y, -Z, W);

        public Vec3 Rotate(Vec3 v)
        {
            var q = new Vec3(X, Y, Z);
            Vec3 t = Vec3.Cross(q, v) * 2.0;
            return v + t * W + Vec3.Cross(q, t);
        }

        public double SqrMagnitude => X * X + Y * Y + Z * Z + W * W;

        public QuatD Normalized
        {
            get
            {
                double m = System.Math.Sqrt(SqrMagnitude);
                if (!(m > 1e-300)) return Identity;
                double inv = 1.0 / m;
                return new QuatD(X * inv, Y * inv, Z * inv, W * inv);
            }
        }

        public bool IsFinite =>
            !(double.IsNaN(X) || double.IsNaN(Y) || double.IsNaN(Z) || double.IsNaN(W) ||
              double.IsInfinity(X) || double.IsInfinity(Y) ||
              double.IsInfinity(Z) || double.IsInfinity(W));

        public Mat3 ToMatrix()
        {
            double xx = X * X, yy = Y * Y, zz = Z * Z;
            double xy = X * Y, xz = X * Z, yz = Y * Z;
            double wx = W * X, wy = W * Y, wz = W * Z;

            return new Mat3(
                1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy),
                2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx),
                2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy));
        }

        public static QuatD FromMatrix(Mat3 m)
        {
            double trace = m.M00 + m.M11 + m.M22;
            if (trace > 0)
            {
                double s = System.Math.Sqrt(trace + 1.0) * 2.0;
                return new QuatD((m.M21 - m.M12) / s, (m.M02 - m.M20) / s, (m.M10 - m.M01) / s,
                    0.25 * s).Normalized;
            }

            if (m.M00 > m.M11 && m.M00 > m.M22)
            {
                double s = System.Math.Sqrt(1.0 + m.M00 - m.M11 - m.M22) * 2.0;
                return new QuatD(0.25 * s, (m.M01 + m.M10) / s, (m.M02 + m.M20) / s,
                    (m.M21 - m.M12) / s).Normalized;
            }

            if (m.M11 > m.M22)
            {
                double s = System.Math.Sqrt(1.0 + m.M11 - m.M00 - m.M22) * 2.0;
                return new QuatD((m.M01 + m.M10) / s, 0.25 * s, (m.M12 + m.M21) / s,
                    (m.M02 - m.M20) / s).Normalized;
            }

            {
                double s = System.Math.Sqrt(1.0 + m.M22 - m.M00 - m.M11) * 2.0;
                return new QuatD((m.M02 + m.M20) / s, (m.M12 + m.M21) / s, 0.25 * s,
                    (m.M10 - m.M01) / s).Normalized;
            }
        }

        /// <summary>
        /// One semi-implicit Euler step of the orientation, driven by a world-frame angular
        /// velocity: <c>q += 0.5 * (0, w) * q * dt</c>, renormalised.
        /// </summary>
        /// <remarks>
        /// The world-frame form (the angular velocity quaternion on the <i>left</i>) rather than
        /// the body-frame one, because the solver carries every link's angular velocity in world
        /// axes — see <c>Aba</c> on why the recursion is world-oriented.
        /// </remarks>
        public QuatD IntegratedBy(Vec3 worldAngularVelocity, double dt)
        {
            var w = new QuatD(worldAngularVelocity.X, worldAngularVelocity.Y, worldAngularVelocity.Z, 0);
            QuatD d = w * this;
            double h = 0.5 * dt;
            return new QuatD(X + d.X * h, Y + d.Y * h, Z + d.Z * h, W + d.W * h).Normalized;
        }

        /// <summary>
        /// One semi-implicit Euler step driven by an angular velocity in this quaternion's own
        /// body frame: <c>q += 0.5 * q * (0, w) * dt</c>, renormalised.
        /// </summary>
        public QuatD IntegratedByBody(Vec3 bodyAngularVelocity, double dt)
        {
            var w = new QuatD(bodyAngularVelocity.X, bodyAngularVelocity.Y, bodyAngularVelocity.Z, 0);
            QuatD d = this * w;
            double h = 0.5 * dt;
            return new QuatD(X + d.X * h, Y + d.Y * h, Z + d.Z * h, W + d.W * h).Normalized;
        }

        /// <summary>
        /// The rotation vector — the axis times the angle, in (-pi, pi] — which is what a ball
        /// joint reports as its three angles.
        /// </summary>
        /// <remarks>
        /// <b>Not the Euler angles.</b> A rotation vector is single-valued and smooth everywhere
        /// short of half a turn, where an Euler triple is singular at a quarter turn about its
        /// middle axis — which is the failure this replaced. For a small rotation the two agree
        /// component for component, so a joint limit written for one reads very nearly the same
        /// on the other; at large angles they part, and the spike says so.
        /// </remarks>
        public Vec3 RotationVector()
        {
            double w = W;
            var v = new Vec3(X, Y, Z);
            if (w < 0) { w = -w; v = -v; }   // the shorter of the two ways round

            double length = v.Magnitude;
            if (length < 1e-12) return v * 2.0;   // the small-angle limit of the line below

            double angle = 2.0 * System.Math.Atan2(length, w);
            return v * (angle / length);
        }

        public static QuatD Read(double[] a, int at) =>
            new QuatD(a[at], a[at + 1], a[at + 2], a[at + 3]);

        public static void Write(double[] a, int at, QuatD q)
        {
            a[at] = q.X; a[at + 1] = q.Y; a[at + 2] = q.Z; a[at + 3] = q.W;
        }
    }
}

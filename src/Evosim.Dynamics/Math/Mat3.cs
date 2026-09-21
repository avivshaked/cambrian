namespace Evosim.Dynamics
{
    /// <summary>
    /// A row-major 3x3 matrix of doubles, stored in a flat array by the solver and passed by
    /// value here. Rotations live in <see cref="QuatD"/>; this is what a rotation becomes when
    /// an inertia tensor has to be carried through it.
    /// </summary>
    public readonly struct Mat3
    {
        public readonly double M00, M01, M02;
        public readonly double M10, M11, M12;
        public readonly double M20, M21, M22;

        public Mat3(
            double m00, double m01, double m02,
            double m10, double m11, double m12,
            double m20, double m21, double m22)
        {
            M00 = m00; M01 = m01; M02 = m02;
            M10 = m10; M11 = m11; M12 = m12;
            M20 = m20; M21 = m21; M22 = m22;
        }

        public static readonly Mat3 Identity = new Mat3(1, 0, 0, 0, 1, 0, 0, 0, 1);
        public static readonly Mat3 Zero = new Mat3(0, 0, 0, 0, 0, 0, 0, 0, 0);

        public static Mat3 Diagonal(double a, double b, double c) =>
            new Mat3(a, 0, 0, 0, b, 0, 0, 0, c);

        /// <summary>The skew-symmetric matrix with <c>Skew(v) * w == cross(v, w)</c>.</summary>
        public static Mat3 Skew(Vec3 v) => new Mat3(
            0, -v.Z, v.Y,
            v.Z, 0, -v.X,
            -v.Y, v.X, 0);

        public Mat3 Transposed => new Mat3(M00, M10, M20, M01, M11, M21, M02, M12, M22);

        public static Mat3 operator +(Mat3 a, Mat3 b) => new Mat3(
            a.M00 + b.M00, a.M01 + b.M01, a.M02 + b.M02,
            a.M10 + b.M10, a.M11 + b.M11, a.M12 + b.M12,
            a.M20 + b.M20, a.M21 + b.M21, a.M22 + b.M22);

        public static Mat3 operator -(Mat3 a, Mat3 b) => new Mat3(
            a.M00 - b.M00, a.M01 - b.M01, a.M02 - b.M02,
            a.M10 - b.M10, a.M11 - b.M11, a.M12 - b.M12,
            a.M20 - b.M20, a.M21 - b.M21, a.M22 - b.M22);

        public static Mat3 operator *(Mat3 a, Mat3 b) => new Mat3(
            a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20,
            a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21,
            a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22,

            a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20,
            a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21,
            a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22,

            a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20,
            a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21,
            a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22);

        public static Mat3 operator *(Mat3 a, double s) => new Mat3(
            a.M00 * s, a.M01 * s, a.M02 * s,
            a.M10 * s, a.M11 * s, a.M12 * s,
            a.M20 * s, a.M21 * s, a.M22 * s);

        public static Vec3 operator *(Mat3 m, Vec3 v) => new Vec3(
            m.M00 * v.X + m.M01 * v.Y + m.M02 * v.Z,
            m.M10 * v.X + m.M11 * v.Y + m.M12 * v.Z,
            m.M20 * v.X + m.M21 * v.Y + m.M22 * v.Z);

        /// <summary><c>this^T * v</c> — for a rotation, the inverse rotation.</summary>
        public Vec3 TransposedTimes(Vec3 v) => new Vec3(
            M00 * v.X + M10 * v.Y + M20 * v.Z,
            M01 * v.X + M11 * v.Y + M21 * v.Z,
            M02 * v.X + M12 * v.Y + M22 * v.Z);

        /// <summary>A diagonal tensor carried into another basis: <c>R * diag(d) * R^T</c>.</summary>
        public static Mat3 RotateDiagonal(Mat3 r, Vec3 d)
        {
            // R * diag(d) scales the columns of R, then times R^T.
            double a00 = r.M00 * d.X, a01 = r.M01 * d.Y, a02 = r.M02 * d.Z;
            double a10 = r.M10 * d.X, a11 = r.M11 * d.Y, a12 = r.M12 * d.Z;
            double a20 = r.M20 * d.X, a21 = r.M21 * d.Y, a22 = r.M22 * d.Z;

            return new Mat3(
                a00 * r.M00 + a01 * r.M01 + a02 * r.M02,
                a00 * r.M10 + a01 * r.M11 + a02 * r.M12,
                a00 * r.M20 + a01 * r.M21 + a02 * r.M22,

                a10 * r.M00 + a11 * r.M01 + a12 * r.M02,
                a10 * r.M10 + a11 * r.M11 + a12 * r.M12,
                a10 * r.M20 + a11 * r.M21 + a12 * r.M22,

                a20 * r.M00 + a21 * r.M01 + a22 * r.M02,
                a20 * r.M10 + a21 * r.M11 + a22 * r.M12,
                a20 * r.M20 + a21 * r.M21 + a22 * r.M22);
        }

        public static Mat3 Read(double[] a, int at) => new Mat3(
            a[at], a[at + 1], a[at + 2],
            a[at + 3], a[at + 4], a[at + 5],
            a[at + 6], a[at + 7], a[at + 8]);

        public static void Write(double[] a, int at, Mat3 m)
        {
            a[at] = m.M00; a[at + 1] = m.M01; a[at + 2] = m.M02;
            a[at + 3] = m.M10; a[at + 4] = m.M11; a[at + 5] = m.M12;
            a[at + 6] = m.M20; a[at + 7] = m.M21; a[at + 8] = m.M22;
        }
    }
}

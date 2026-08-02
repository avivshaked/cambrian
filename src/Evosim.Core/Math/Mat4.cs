using System;

namespace Evosim.Core
{
    /// <summary>
    /// A 4x4 affine transform, column-vector convention (<c>v' = M * v</c>). Row-major
    /// field naming: <c>M{row}{col}</c>.
    /// </summary>
    /// <remarks>
    /// Development accumulates scale, rotation and reflection down each subtree
    /// (DESIGN.md §4.2, [K12 §2.1, p.3]). Reflection makes the accumulated transform
    /// improper — determinant negative — which a position+quaternion pair cannot
    /// represent. Carrying a full matrix and decomposing once per part at the end is the
    /// simplest correct handling; see <see cref="Decompose"/>.
    /// </remarks>
    public readonly struct Mat4
    {
        public readonly float M00, M01, M02, M03;
        public readonly float M10, M11, M12, M13;
        public readonly float M20, M21, M22, M23;
        public readonly float M30, M31, M32, M33;

        public Mat4(
            float m00, float m01, float m02, float m03,
            float m10, float m11, float m12, float m13,
            float m20, float m21, float m22, float m23,
            float m30, float m31, float m32, float m33)
        {
            M00 = m00; M01 = m01; M02 = m02; M03 = m03;
            M10 = m10; M11 = m11; M12 = m12; M13 = m13;
            M20 = m20; M21 = m21; M22 = m22; M23 = m23;
            M30 = m30; M31 = m31; M32 = m32; M33 = m33;
        }

        public static readonly Mat4 Identity = new Mat4(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f);

        public static Mat4 Translate(Float3 t) => new Mat4(
            1f, 0f, 0f, t.X,
            0f, 1f, 0f, t.Y,
            0f, 0f, 1f, t.Z,
            0f, 0f, 0f, 1f);

        public static Mat4 Scale(Float3 s) => new Mat4(
            s.X, 0f, 0f, 0f,
            0f, s.Y, 0f, 0f,
            0f, 0f, s.Z, 0f,
            0f, 0f, 0f, 1f);

        public static Mat4 Rotate(Quat q)
        {
            Quat n = q.Normalized;
            float x = n.X, y = n.Y, z = n.Z, w = n.W;
            float xx = x * x, yy = y * y, zz = z * z;
            float xy = x * y, xz = x * z, yz = y * z;
            float wx = w * x, wy = w * y, wz = w * z;

            return new Mat4(
                1f - 2f * (yy + zz), 2f * (xy - wz), 2f * (xz + wy), 0f,
                2f * (xy + wz), 1f - 2f * (xx + zz), 2f * (yz - wx), 0f,
                2f * (xz - wy), 2f * (yz + wx), 1f - 2f * (xx + yy), 0f,
                0f, 0f, 0f, 1f);
        }

        /// <summary>
        /// Mirror about the parent-local planes selected by <paramref name="axes"/>.
        /// One enabled flag is a single mirror, two is a mirror pair, three inverts all
        /// axes — the machinery behind DESIGN.md §4.1's 2 / 4 / 8 mirrored copies.
        /// </summary>
        public static Mat4 Mirror(Bool3 axes) => Scale(new Float3(
            axes.X ? -1f : 1f,
            axes.Y ? -1f : 1f,
            axes.Z ? -1f : 1f));

        public static Mat4 Trs(Float3 t, Quat r, Float3 s) => Translate(t) * Rotate(r) * Scale(s);

        public static Mat4 operator *(Mat4 a, Mat4 b) => new Mat4(
            a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20 + a.M03 * b.M30,
            a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21 + a.M03 * b.M31,
            a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22 + a.M03 * b.M32,
            a.M00 * b.M03 + a.M01 * b.M13 + a.M02 * b.M23 + a.M03 * b.M33,

            a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20 + a.M13 * b.M30,
            a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
            a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
            a.M10 * b.M03 + a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,

            a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20 + a.M23 * b.M30,
            a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
            a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
            a.M20 * b.M03 + a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,

            a.M30 * b.M00 + a.M31 * b.M10 + a.M32 * b.M20 + a.M33 * b.M30,
            a.M30 * b.M01 + a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
            a.M30 * b.M02 + a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
            a.M30 * b.M03 + a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33);

        public Float3 MultiplyPoint(Float3 p) => new Float3(
            M00 * p.X + M01 * p.Y + M02 * p.Z + M03,
            M10 * p.X + M11 * p.Y + M12 * p.Z + M13,
            M20 * p.X + M21 * p.Y + M22 * p.Z + M23);

        public Float3 MultiplyVector(Float3 v) => new Float3(
            M00 * v.X + M01 * v.Y + M02 * v.Z,
            M10 * v.X + M11 * v.Y + M12 * v.Z,
            M20 * v.X + M21 * v.Y + M22 * v.Z);

        /// <summary>Translation column.</summary>
        public Float3 Position => new Float3(M03, M13, M23);

        /// <summary>Determinant of the upper-left 3x3. Negative means the frame is mirrored.</summary>
        public float Determinant3 =>
            M00 * (M11 * M22 - M12 * M21)
          - M01 * (M10 * M22 - M12 * M20)
          + M02 * (M10 * M21 - M11 * M20);

        public bool IsFinite
        {
            get
            {
                float[] all = { M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23, M30, M31, M32, M33 };
                for (int i = 0; i < all.Length; i++)
                {
                    if (float.IsNaN(all[i]) || float.IsInfinity(all[i])) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Splits this transform into position, a proper rotation, per-axis scale
        /// magnitudes, and whether the frame is mirrored.
        /// </summary>
        /// <remarks>
        /// When the determinant is negative the basis cannot be expressed as a rotation.
        /// The X axis is flipped to restore a proper rotation and <paramref name="mirrored"/>
        /// is set, so the caller keeps the information rather than losing it silently.
        /// A box is symmetric under that flip, so part geometry is unaffected; the flag
        /// exists because joint axis conventions in Evosim.Sim are not.
        /// </remarks>
        public void Decompose(out Float3 position, out Quat rotation, out Float3 scale, out bool mirrored)
        {
            position = Position;

            Float3 cx = new Float3(M00, M10, M20);
            Float3 cy = new Float3(M01, M11, M21);
            Float3 cz = new Float3(M02, M12, M22);

            float sx = cx.Magnitude;
            float sy = cy.Magnitude;
            float sz = cz.Magnitude;

            mirrored = Determinant3 < 0f;
            if (mirrored)
            {
                sx = -sx;
                cx = -cx;
            }

            scale = new Float3(sx, sy, sz);

            // Degenerate scale leaves no recoverable orientation; identity is the honest answer.
            if (System.Math.Abs(sx) < 1e-9f || sy < 1e-9f || sz < 1e-9f)
            {
                rotation = Quat.Identity;
                return;
            }

            Float3 nx = cx * (1f / System.Math.Abs(sx));
            Float3 ny = cy * (1f / sy);
            Float3 nz = cz * (1f / sz);

            Mat4 pure = new Mat4(
                nx.X, ny.X, nz.X, 0f,
                nx.Y, ny.Y, nz.Y, 0f,
                nx.Z, ny.Z, nz.Z, 0f,
                0f, 0f, 0f, 1f);

            rotation = Quat.FromRotationMatrix(pure);
        }
    }
}

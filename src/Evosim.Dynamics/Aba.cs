using System;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Featherstone's articulated-body algorithm over a tree of links with a floating base,
    /// O(links), in world-oriented spatial coordinates at each link's origin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The three passes.</b> The first is <see cref="Kinematics"/>: poses, velocities, the
    /// motion subspace <c>S</c> and the velocity-product term <c>c</c>. The second runs from the
    /// leaves inward, folding each link's articulated inertia and bias force into its parent's.
    /// The third runs outward, solving the free base from a six-by-six and then every joint
    /// acceleration from the <c>k x k</c> the second pass left behind.
    /// </para>
    /// <para>
    /// <b>Spatial vectors are written <c>[angular; linear]</c></b>, forces as
    /// <c>[torque about the link origin; force]</c> and accelerations as
    /// <c>[dw/dt; du/dt]</c>, where <c>u</c> is the velocity of the link's own centre of mass.
    /// These are true time derivatives rather than Featherstone's spatial acceleration, which is
    /// why the bias force has no linear term: the linear equation is simply <c>m du/dt = f</c>.
    /// </para>
    /// <para>
    /// <b>Nothing here allocates.</b> Every buffer is a field of the <see cref="Creature"/>, so
    /// a creature can be stepped on any thread with no shared state at all.
    /// </para>
    /// </remarks>
    public static class Aba
    {
        /// <summary>
        /// Solves for every acceleration, given the poses, the velocities, the external forces
        /// in <c>Fext</c> and the generalised joint torques in <c>Tau</c>.
        /// </summary>
        public static void Solve(Creature body)
        {
            Seed(body);
            Inward(body);
            Outward(body);
        }

        /// <summary>Each link's own inertia and its bias force, before anything is folded in.</summary>
        private static void Seed(Creature body)
        {
            for (int i = 0; i < body.Links; i++)
            {
                Mat3 rotation = Mat3.Read(body.RotationMatrix, 9 * i);
                Mat3 inertia = Mat3.RotateDiagonal(rotation, Vec3.Read(body.InertiaLocal, 3 * i));

                Mat3.Write(body.IaA, 9 * i, inertia);
                Mat3.Write(body.IaB, 9 * i, Mat3.Zero);
                Mat3.Write(body.IaC, 9 * i, Mat3.Diagonal(body.Mass[i], body.Mass[i], body.Mass[i]));

                Vec3 spin = Vec3.Read(body.Spin, 3 * i);
                Vec3 gyroscopic = Vec3.Cross(spin, inertia * spin);

                Vec3.Write(body.Pa, 6 * i, gyroscopic - Vec3.Read(body.Fext, 6 * i));
                Vec3.Write(body.Pa, 6 * i + 3, -Vec3.Read(body.Fext, 6 * i + 3));
            }
        }

        /// <summary>Leaves to root: the articulated inertia and bias force at every joint.</summary>
        private static void Inward(Creature body)
        {
            for (int i = body.Links - 1; i >= 1; i--)
            {
                int p = body.Parent[i];
                int n = body.DofCount[i];

                Mat3 a = Mat3.Read(body.IaA, 9 * i);
                Mat3 b = Mat3.Read(body.IaB, 9 * i);
                Mat3 c = Mat3.Read(body.IaC, 9 * i);

                Vec3 paN = Vec3.Read(body.Pa, 6 * i);
                Vec3 paF = Vec3.Read(body.Pa, 6 * i + 3);

                if (n > 0)
                {
                    int at = body.DofStart[i];

                    // U = I^A S, one column per degree of freedom.
                    for (int d = 0; d < n; d++)
                    {
                        Vec3 sa = Vec3.Read(body.Sang, 9 * i + 3 * d);
                        Vec3 sl = Vec3.Read(body.Slin, 9 * i + 3 * d);

                        Vec3.Write(body.Un, 9 * i + 3 * d, a * sa + b * sl);
                        Vec3.Write(body.Uf, 9 * i + 3 * d, b.TransposedTimes(sa) + c * sl);
                    }

                    // D = S^T U, and its inverse; ubar = tau - S^T p^A.
                    double d00 = 0, d01 = 0, d02 = 0, d11 = 0, d12 = 0, d22 = 0;
                    for (int j = 0; j < n; j++)
                    {
                        Vec3 sa = Vec3.Read(body.Sang, 9 * i + 3 * j);
                        Vec3 sl = Vec3.Read(body.Slin, 9 * i + 3 * j);

                        body.Ubar[3 * i + j] =
                            body.Tau[at + j] - (Vec3.Dot(sa, paN) + Vec3.Dot(sl, paF));

                        for (int k = j; k < n; k++)
                        {
                            double v = Vec3.Dot(sa, Vec3.Read(body.Un, 9 * i + 3 * k)) +
                                       Vec3.Dot(sl, Vec3.Read(body.Uf, 9 * i + 3 * k));

                            if (j == 0 && k == 0) d00 = v;
                            else if (j == 0 && k == 1) d01 = v;
                            else if (j == 0 && k == 2) d02 = v;
                            else if (j == 1 && k == 1) d11 = v;
                            else if (j == 1 && k == 2) d12 = v;
                            else d22 = v;
                        }
                    }

                    // The limit spring's implicit term, on the diagonal and nowhere else: a
                    // penalty stop resists the acceleration it is about to see, and saying so
                    // here is what lets the stiffness be chosen for the overshoot it allows
                    // rather than for what an explicit step will survive. Zero for every degree
                    // of freedom inside its stops, so the plain algorithm is what the oracle
                    // tests check. See Creature.LimitImplicit.
                    d00 += body.LimitImplicit[at];
                    if (n > 1) d11 += body.LimitImplicit[at + 1];
                    if (n > 2) d22 += body.LimitImplicit[at + 2];

                    InvertSmallSymmetric(body.Dinv, 9 * i, n, d00, d01, d02, d11, d12, d22);

                    // I^a = I^A - U D^-1 U^T
                    for (int j = 0; j < n; j++)
                    {
                        Vec3 unj = Vec3.Read(body.Un, 9 * i + 3 * j);
                        Vec3 ufj = Vec3.Read(body.Uf, 9 * i + 3 * j);

                        for (int k = 0; k < n; k++)
                        {
                            double w = body.Dinv[9 * i + 3 * j + k];
                            if (w == 0) continue;

                            Vec3 unk = Vec3.Read(body.Un, 9 * i + 3 * k);
                            Vec3 ufk = Vec3.Read(body.Uf, 9 * i + 3 * k);

                            a = a - Outer(unj, unk) * w;
                            b = b - Outer(unj, ufk) * w;
                            c = c - Outer(ufj, ufk) * w;
                        }
                    }

                    // p^a = p^A + I^a c + U D^-1 ubar
                    Vec3 cAng = Vec3.Read(body.Cbias, 6 * i);
                    Vec3 cLin = Vec3.Read(body.Cbias, 6 * i + 3);

                    paN += a * cAng + b * cLin;
                    paF += b.TransposedTimes(cAng) + c * cLin;

                    for (int j = 0; j < n; j++)
                    {
                        double y = 0;
                        for (int k = 0; k < n; k++) y += body.Dinv[9 * i + 3 * j + k] * body.Ubar[3 * i + k];

                        paN += Vec3.Read(body.Un, 9 * i + 3 * j) * y;
                        paF += Vec3.Read(body.Uf, 9 * i + 3 * j) * y;
                    }
                }
                else
                {
                    // A fixed joint transmits everything: I^a is I^A and the bias picks up the
                    // velocity-product term alone.
                    Vec3 cAng = Vec3.Read(body.Cbias, 6 * i);
                    Vec3 cLin = Vec3.Read(body.Cbias, 6 * i + 3);

                    paN += a * cAng + b * cLin;
                    paF += b.TransposedTimes(cAng) + c * cLin;
                }

                // Carry the result up to the parent. The transform is a pure translation,
                // because both frames are written in world axes.
                Vec3 offset = Vec3.Read(body.Position, 3 * i) - Vec3.Read(body.Position, 3 * p);
                Mat3 k2 = Mat3.Skew(offset);

                Mat3 product = b * k2;
                Mat3 aParent = a - product - product.Transposed - k2 * c * k2;
                Mat3 bParent = b + k2 * c;

                Mat3.Write(body.IaA, 9 * p, Mat3.Read(body.IaA, 9 * p) + aParent);
                Mat3.Write(body.IaB, 9 * p, Mat3.Read(body.IaB, 9 * p) + bParent);
                Mat3.Write(body.IaC, 9 * p, Mat3.Read(body.IaC, 9 * p) + c);

                Vec3.Add(body.Pa, 6 * p, paN + Vec3.Cross(offset, paF));
                Vec3.Add(body.Pa, 6 * p + 3, paF);
            }
        }

        /// <summary>Root to leaves: the free base, then every joint acceleration.</summary>
        private static void Outward(Creature body)
        {
            // The floating base carries no constraint force, so I^A a + p^A = 0.
            Mat3 a0 = Mat3.Read(body.IaA, 0);
            Mat3 b0 = Mat3.Read(body.IaB, 0);
            Mat3 c0 = Mat3.Read(body.IaC, 0);
            Vec3 pn = Vec3.Read(body.Pa, 0);
            Vec3 pf = Vec3.Read(body.Pa, 3);

            SolveSixBySix(a0, b0, c0, -pn, -pf, out Vec3 rootAngular, out Vec3 rootLinear);

            Vec3.Write(body.Acc, 0, rootAngular);
            Vec3.Write(body.Acc, 3, rootLinear);

            for (int i = 1; i < body.Links; i++)
            {
                int p = body.Parent[i];
                int n = body.DofCount[i];

                Vec3 parentAngular = Vec3.Read(body.Acc, 6 * p);
                Vec3 parentLinear = Vec3.Read(body.Acc, 6 * p + 3);
                Vec3 offset = Vec3.Read(body.Position, 3 * i) - Vec3.Read(body.Position, 3 * p);

                Vec3 angular = parentAngular + Vec3.Read(body.Cbias, 6 * i);
                Vec3 linear = parentLinear + Vec3.Cross(parentAngular, offset) +
                              Vec3.Read(body.Cbias, 6 * i + 3);

                if (n > 0)
                {
                    int at = body.DofStart[i];

                    Span<double> residual = stackalloc double[3];
                    for (int k = 0; k < n; k++)
                    {
                        residual[k] = body.Ubar[3 * i + k] -
                            (Vec3.Dot(Vec3.Read(body.Un, 9 * i + 3 * k), angular) +
                             Vec3.Dot(Vec3.Read(body.Uf, 9 * i + 3 * k), linear));
                    }

                    for (int j = 0; j < n; j++)
                    {
                        double acceleration = 0;
                        for (int k = 0; k < n; k++)
                        {
                            acceleration += body.Dinv[9 * i + 3 * j + k] * residual[k];
                        }

                        body.Tau[at + j] = acceleration;   // parked for the integrator
                        angular += Vec3.Read(body.Sang, 9 * i + 3 * j) * acceleration;
                        linear += Vec3.Read(body.Slin, 9 * i + 3 * j) * acceleration;
                    }
                }

                Vec3.Write(body.Acc, 6 * i, angular);
                Vec3.Write(body.Acc, 6 * i + 3, linear);
            }
        }

        /// <summary>
        /// Semi-implicit Euler: velocities first, then the positions they imply.
        /// </summary>
        /// <remarks>
        /// <c>Tau</c> holds the joint accelerations that <see cref="Outward"/> parked there; the
        /// caller refills it with torques before the next solve. The root's angular velocity is
        /// in world axes, which is why the quaternion is integrated from the left.
        /// </remarks>
        public static void Integrate(Creature body, double dt)
        {
            for (int i = 1; i < body.Links; i++)
            {
                int n = body.DofCount[i];
                if (n == 0) continue;

                int at = body.DofStart[i];
                for (int d = 0; d < n; d++) body.Qd[at + d] += body.Tau[at + d] * dt;

                if (n == 3)
                {
                    // A ball joint's velocity coordinates are the relative angular velocity in
                    // the child's joint frame, so what integrates is the quaternion, and the
                    // three angles are read back off it rather than accumulated.
                    var rate = new Vec3(body.Qd[at], body.Qd[at + 1], body.Qd[at + 2]);

                    QuatD turned = QuatD.Read(body.BallRotation, 4 * i)
                                        .IntegratedByBody(rate, dt);

                    QuatD.Write(body.BallRotation, 4 * i, turned);

                    Vec3 angles = turned.RotationVector();
                    body.Q[at] = angles.X;
                    body.Q[at + 1] = angles.Y;
                    body.Q[at + 2] = angles.Z;
                }
                else
                {
                    for (int d = 0; d < n; d++) body.Q[at + d] += body.Qd[at + d] * dt;
                }
            }

            Vec3 spin = Vec3.Read(body.Spin, 0) + Vec3.Read(body.Acc, 0) * dt;
            Vec3 velocity = Vec3.Read(body.Velocity, 0) + Vec3.Read(body.Acc, 3) * dt;

            Vec3.Write(body.Spin, 0, spin);
            Vec3.Write(body.Velocity, 0, velocity);

            body.BasePosition += velocity * dt;
            body.BaseRotation = body.BaseRotation.IntegratedBy(spin, dt);
        }

        private static Mat3 Outer(Vec3 a, Vec3 b) => new Mat3(
            a.X * b.X, a.X * b.Y, a.X * b.Z,
            a.Y * b.X, a.Y * b.Y, a.Y * b.Z,
            a.Z * b.X, a.Z * b.Y, a.Z * b.Z);

        /// <summary>Inverts the joint's own <c>k x k</c>, for k of 1, 2 or 3.</summary>
        private static void InvertSmallSymmetric(
            double[] into, int at, int n,
            double d00, double d01, double d02, double d11, double d12, double d22)
        {
            for (int i = 0; i < 9; i++) into[at + i] = 0;

            if (n == 1)
            {
                into[at] = d00 != 0 ? 1.0 / d00 : 0;
                return;
            }

            if (n == 2)
            {
                double det = d00 * d11 - d01 * d01;
                if (det == 0) return;
                double inv = 1.0 / det;
                into[at + 0] = d11 * inv;
                into[at + 1] = -d01 * inv;
                into[at + 3] = -d01 * inv;
                into[at + 4] = d00 * inv;
                return;
            }

            double c00 = d11 * d22 - d12 * d12;
            double c01 = d02 * d12 - d01 * d22;
            double c02 = d01 * d12 - d02 * d11;
            double determinant = d00 * c00 + d01 * c01 + d02 * c02;
            if (determinant == 0) return;

            double k = 1.0 / determinant;
            double c11 = d00 * d22 - d02 * d02;
            double c12 = d02 * d01 - d00 * d12;
            double c22 = d00 * d11 - d01 * d01;

            into[at + 0] = c00 * k; into[at + 1] = c01 * k; into[at + 2] = c02 * k;
            into[at + 3] = c01 * k; into[at + 4] = c11 * k; into[at + 5] = c12 * k;
            into[at + 6] = c02 * k; into[at + 7] = c12 * k; into[at + 8] = c22 * k;
        }

        /// <summary>
        /// Solves the free base's six-by-six by Gaussian elimination with partial pivoting.
        /// </summary>
        /// <remarks>
        /// Six unknowns once per creature per step, so the cost is not worth a specialised
        /// factorisation; pivoting is worth it, because an articulated inertia folded up a
        /// sixteen-link tree is not always well conditioned.
        /// </remarks>
        private static void SolveSixBySix(
            Mat3 a, Mat3 b, Mat3 c, Vec3 rhsAngular, Vec3 rhsLinear,
            out Vec3 angular, out Vec3 linear)
        {
            Span<double> m = stackalloc double[42];   // 6 rows of 7

            m[0] = a.M00; m[1] = a.M01; m[2] = a.M02;
            m[7] = a.M10; m[8] = a.M11; m[9] = a.M12;
            m[14] = a.M20; m[15] = a.M21; m[16] = a.M22;

            m[3] = b.M00; m[4] = b.M01; m[5] = b.M02;
            m[10] = b.M10; m[11] = b.M11; m[12] = b.M12;
            m[17] = b.M20; m[18] = b.M21; m[19] = b.M22;

            m[21] = b.M00; m[22] = b.M10; m[23] = b.M20;
            m[28] = b.M01; m[29] = b.M11; m[30] = b.M21;
            m[35] = b.M02; m[36] = b.M12; m[37] = b.M22;

            m[24] = c.M00; m[25] = c.M01; m[26] = c.M02;
            m[31] = c.M10; m[32] = c.M11; m[33] = c.M12;
            m[38] = c.M20; m[39] = c.M21; m[40] = c.M22;

            m[6] = rhsAngular.X; m[13] = rhsAngular.Y; m[20] = rhsAngular.Z;
            m[27] = rhsLinear.X; m[34] = rhsLinear.Y; m[41] = rhsLinear.Z;

            for (int col = 0; col < 6; col++)
            {
                int pivot = col;
                double best = System.Math.Abs(m[col * 7 + col]);
                for (int row = col + 1; row < 6; row++)
                {
                    double v = System.Math.Abs(m[row * 7 + col]);
                    if (v > best) { best = v; pivot = row; }
                }

                if (pivot != col)
                {
                    for (int k = col; k < 7; k++)
                    {
                        double swap = m[col * 7 + k];
                        m[col * 7 + k] = m[pivot * 7 + k];
                        m[pivot * 7 + k] = swap;
                    }
                }

                double diagonal = m[col * 7 + col];
                if (diagonal == 0) continue;

                for (int row = col + 1; row < 6; row++)
                {
                    double factor = m[row * 7 + col] / diagonal;
                    if (factor == 0) continue;
                    for (int k = col; k < 7; k++) m[row * 7 + k] -= factor * m[col * 7 + k];
                }
            }

            Span<double> x = stackalloc double[6];
            for (int row = 5; row >= 0; row--)
            {
                double sum = m[row * 7 + 6];
                for (int k = row + 1; k < 6; k++) sum -= m[row * 7 + k] * x[k];
                double diagonal = m[row * 7 + row];
                x[row] = diagonal != 0 ? sum / diagonal : 0;
            }

            angular = new Vec3(x[0], x[1], x[2]);
            linear = new Vec3(x[3], x[4], x[5]);
        }
    }
}

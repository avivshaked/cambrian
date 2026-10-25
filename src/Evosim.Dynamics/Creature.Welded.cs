using System;

namespace Evosim.Dynamics
{
    public sealed partial class Creature
    {
        // One row per link, used only for a link that anchors a welded body: its mass, the mass
        // moment of its links' positions, the drag torque about the body's centre, and the inertia
        // about that centre (xx, yy, zz, xy, xz, yz).
        private const int WeldStride = 13;

        private int[] _weldAnchor;
        private double[] _weldSums;
        private double[] _weldForceCut;

        /// <summary>
        /// What the per-link force cap took from link <paramref name="link"/>'s drag force this
        /// step, zero where it took nothing. Noted by <see cref="Fluid"/> under
        /// <c>DragLimitByWeldedBody</c>, so the welded body's torque is taken from the forces the
        /// links are actually given.
        /// </summary>
        internal void NoteWeldedForceCut(int link, Vec3 cut)
        {
            if (_weldForceCut == null) _weldForceCut = new double[3 * Links];
            Vec3.Write(_weldForceCut, 3 * link, cut);
        }

        /// <summary>
        /// D199's drag limiter, in the form the owner chose on 2026-10-18: each link's drag force
        /// is capped by that link's own mass and speed, as before, and the drag torque on each
        /// welded body (a link with a joint, or the root, and every link welded to it) is capped
        /// as a whole, by the body's inertia on each of its principal axes. Called by
        /// <see cref="Fluid.Apply(Creature, SolverConfig)"/> once every link's drag is noted and
        /// its force capped, where the limiter engages and <c>DragLimitByWeldedBody</c> is on.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why the welded body, for the torque.</b> The per-link law caps a part's drag torque
        /// by that part's own smallest inertia, along its spin only. A part welded into a long
        /// rigid blade turns with the blade, whose inertia is larger by six orders, so the law
        /// removed nearly all the drag along the spin while the torque across it passed untouched,
        /// and the blade ran away (logbook/specs/r62-blades.md).
        /// </para>
        /// <para>
        /// <b>Why each link, for the force.</b> Capped by the whole body's mass against its
        /// centre's velocity, the force let a fast roll through: the centre of a rolling blade
        /// barely moves while each part crosses the water at speed, and their drag summed to a net
        /// force that threw the body to 182 m/s in one 0.02 s step. Each part's own speed is what
        /// its drag acts against.
        /// </para>
        /// <para>
        /// <b>The law.</b> The links' drag torques, and the moments of their capped forces about
        /// the body's centre of mass, are summed into one torque. It is resolved on the body's
        /// principal axes, and on each axis where it opposes the body's spin about that axis it is
        /// capped at the axis's moment times that spin over the step. A component that does not
        /// oppose is left as computed. What the cap removes is applied to the anchoring link as a
        /// torque; the body is rigid, so where in it the correction lands moves nothing but the
        /// welds' internal forces.
        /// </para>
        /// <para>
        /// The bodies are found again on every call (a link is welded when it has a parent and no
        /// degree of freedom, and its parent is always earlier), so growth that adds a link needs
        /// nothing here. The rows are allocated at the first call and never again.
        /// </para>
        /// </remarks>
        internal void LimitWeldedDrag(double dt)
        {
            int n = Links;
            if (_weldAnchor == null)
            {
                _weldAnchor = new int[n];
                _weldSums = new double[WeldStride * n];
            }

            if (_weldForceCut == null) _weldForceCut = new double[3 * n];

            int[] anchor = _weldAnchor;
            double[] sums = _weldSums;
            Array.Clear(sums, 0, sums.Length);

            for (int i = 0; i < n; i++)
            {
                int parent = Parent[i];
                int a = parent >= 0 && DofCount[i] == 0 ? anchor[parent] : i;
                anchor[i] = a;

                int at = WeldStride * a;
                double m = Mass[i];
                sums[at] += m;
                sums[at + 1] += m * Position[3 * i];
                sums[at + 2] += m * Position[3 * i + 1];
                sums[at + 3] += m * Position[3 * i + 2];
            }

            for (int i = 0; i < n; i++)
            {
                int at = WeldStride * anchor[i];
                double total = sums[at];
                var centre = new Vec3(sums[at + 1] / total, sums[at + 2] / total, sums[at + 3] / total);

                Vec3 r = Vec3.Read(Position, 3 * i) - centre;
                Vec3 f = Vec3.Read(DragForce, 3 * i) + Vec3.Read(_weldForceCut, 3 * i);
                Vec3 t = Vec3.Read(DragTorque, 3 * i) + Vec3.Cross(r, f);
                sums[at + 4] += t.X;
                sums[at + 5] += t.Y;
                sums[at + 6] += t.Z;

                Mat3 own = Mat3.RotateDiagonal(Mat3.Read(RotationMatrix, 9 * i), Vec3.Read(InertiaLocal, 3 * i));
                double m = Mass[i];
                double rr = Vec3.Dot(r, r);
                sums[at + 7] += own.M00 + m * (rr - r.X * r.X);
                sums[at + 8] += own.M11 + m * (rr - r.Y * r.Y);
                sums[at + 9] += own.M22 + m * (rr - r.Z * r.Z);
                sums[at + 10] += own.M01 - m * r.X * r.Y;
                sums[at + 11] += own.M02 - m * r.X * r.Z;
                sums[at + 12] += own.M12 - m * r.Y * r.Z;
            }

            for (int a = 0; a < n; a++)
            {
                if (anchor[a] != a) continue;

                Vec3 spin = Vec3.Read(Spin, 3 * a);
                if (!(spin.SqrMagnitude > 0)) continue;

                int at = WeldStride * a;
                var torque = new Vec3(sums[at + 4], sums[at + 5], sums[at + 6]);

                SymmetricEigen(
                    sums[at + 7], sums[at + 8], sums[at + 9],
                    sums[at + 10], sums[at + 11], sums[at + 12],
                    out Vec3 moments, out Mat3 axes);

                Vec3 torqueCut = Vec3.Zero;
                for (int k = 0; k < 3; k++)
                {
                    var axis = new Vec3(
                        k == 0 ? axes.M00 : k == 1 ? axes.M01 : axes.M02,
                        k == 0 ? axes.M10 : k == 1 ? axes.M11 : axes.M12,
                        k == 0 ? axes.M20 : k == 1 ? axes.M21 : axes.M22);
                    double moment = k == 0 ? moments.X : k == 1 ? moments.Y : moments.Z;
                    if (!(moment > 0)) continue;

                    double w = Vec3.Dot(spin, axis);
                    double along = Vec3.Dot(torque, axis);
                    if (along * w >= 0) continue;

                    double allowed = moment * Math.Abs(w) / dt;
                    if (Math.Abs(along) > allowed)
                    {
                        torqueCut += axis * ((along > 0 ? allowed : -allowed) - along);
                        DragImpulsesLimited++;
                    }
                }

                if (torqueCut.SqrMagnitude > 0) Vec3.Add(Fext, 6 * a, torqueCut);
            }
        }

        /// <summary>
        /// The eigenvalues and unit eigenvectors (the columns of <paramref name="vectors"/>) of a
        /// symmetric 3×3 matrix, by cyclic Jacobi rotations: a fixed, branch-light recurrence, so
        /// the same input gives the same bits on any thread.
        /// </summary>
        internal static void SymmetricEigen(
            double a00, double a11, double a22, double a01, double a02, double a12,
            out Vec3 values, out Mat3 vectors)
        {
            Span<double> m = stackalloc double[9];
            Span<double> v = stackalloc double[9];
            m[0] = a00; m[1] = a01; m[2] = a02;
            m[3] = a01; m[4] = a11; m[5] = a12;
            m[6] = a02; m[7] = a12; m[8] = a22;
            v[0] = 1; v[4] = 1; v[8] = 1;

            double scale = a00 * a00 + a11 * a11 + a22 * a22;

            for (int sweep = 0; sweep < 32; sweep++)
            {
                double off = m[1] * m[1] + m[2] * m[2] + m[5] * m[5];
                if (!(off > 1e-30 * scale)) break;

                for (int pair = 0; pair < 3; pair++)
                {
                    int p = pair == 2 ? 1 : 0;
                    int q = pair == 0 ? 1 : 2;

                    double apq = m[3 * p + q];
                    if (apq == 0) continue;

                    double theta = (m[3 * q + q] - m[3 * p + p]) / (2.0 * apq);
                    double tan = (theta >= 0 ? 1.0 : -1.0) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                    double cos = 1.0 / Math.Sqrt(tan * tan + 1.0);
                    double sin = tan * cos;

                    for (int k = 0; k < 3; k++)
                    {
                        double kp = m[3 * k + p], kq = m[3 * k + q];
                        m[3 * k + p] = cos * kp - sin * kq;
                        m[3 * k + q] = sin * kp + cos * kq;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        double pk = m[3 * p + k], qk = m[3 * q + k];
                        m[3 * p + k] = cos * pk - sin * qk;
                        m[3 * q + k] = sin * pk + cos * qk;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        double kp = v[3 * k + p], kq = v[3 * k + q];
                        v[3 * k + p] = cos * kp - sin * kq;
                        v[3 * k + q] = sin * kp + cos * kq;
                    }
                }
            }

            values = new Vec3(m[0], m[4], m[8]);
            vectors = new Mat3(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8]);
        }
    }
}

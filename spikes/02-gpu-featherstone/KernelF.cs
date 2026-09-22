// ---------------------------------------------------------------------------------------------
// GENERATED from kernel.template by gen.py. Do not edit; edit the template and regenerate.
//
// One creature's Featherstone step, transcribed from Evosim.Dynamics for ILGPU: one GPU thread
// per creature, the whole body's state held in thread-local arrays for the length of a launch.
//
// Sources, term for term:
//   Kinematics.Poses / Kinematics.Velocities / JointRotation / SubspaceAxis  -> Kinematics.cs
//   Aba.Seed / Inward / Outward / Integrate / InvertSmallSymmetric / SolveSixBySix -> Aba.cs
//   Fluid.Apply (drag panels, buoyancy, D077's Restore)                     -> Fluid.cs
//   the joint damper and limit springs                                      -> DynamicsWorld.JointTorques
//   the drive torque pair on child and parent                               -> EffectorDrive.Drive tail
//   Vec3 / Mat3 / QuatD                                                     -> Dynamics/Math/*.cs
//
// What is NOT here, and why: see results.md. In short — brain, senses and the ten-sample drive
// average (the drive is frozen at the step's values, per the spike's brief); creature-creature
// contact, the bed and the glass; growth; the ledgers; the trace and the divergence guard; a
// moving current (the water is still, so D090's acceleration term is identically zero); and the
// two limiters, which at dt 0.01 do not engage in the library either (SolverConfig.LimitersEngage
// and DragLimiterEngages are both false at 0.01 with DriveLimitAtEveryStep off).
// ---------------------------------------------------------------------------------------------

using System;
using ILGPU;
using ILGPU.Runtime;

namespace Gpu.Sgl
{
    using Real = System.Single;

    /// <summary>Topology, one entry per creature or per (creature, link), strided by creature.</summary>
    public struct GTopo
    {
        public ArrayView<int> Links, Dof, Parent, DofCount, DofStart, PanelStart;
    }

    /// <summary>Everything about a body that does not change over a launch.</summary>
    public struct GBody
    {
        public ArrayView<Real> Mass, Inertia, ChildAnchor, ParentAnchor, JointFrame, RestFrame;
        public ArrayView<Real> LimitLo, LimitHi, LimitStiff, LimitDamp, DriveTorque, Lift, Excess;
    }

    public struct GPanels { public ArrayView<Real> Centre, Normal, Area; }

    /// <summary>The whole of a creature's state: everything else is derived by the kinematics.</summary>
    public struct GState
    {
        public ArrayView<Real> BasePos, BaseRot, Q, Qd, BallRot, RootSpin, RootVel, OutPos;
    }

    public struct GCfg
    {
        public Real Dt, DragK, Gravity, TissueDensity, RestoringDensity, WorldDepth, Damping;
        public int N, Steps, UseRestore, FloorRestores;
    }

    public struct V3
    {
        public Real X, Y, Z;
        public V3(Real x, Real y, Real z) { X = x; Y = y; Z = z; }

        public static V3 Zero => new V3(0, 0, 0);

        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator -(V3 a) => new V3(-a.X, -a.Y, -a.Z);
        public static V3 operator *(V3 a, Real s) => new V3(a.X * s, a.Y * s, a.Z * s);

        public static Real Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static V3 Cross(V3 a, V3 b) => new V3(
            a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public Real SqrMagnitude => X * X + Y * Y + Z * Z;
        public Real Magnitude => System.MathF.Sqrt(X * X + Y * Y + Z * Z);

        public V3 Normalized
        {
            get
            {
                Real m = Magnitude;
                return m > (Real)1e-300 ? new V3(X / m, Y / m, Z / m) : Zero;
            }
        }

        public static V3 Read(Real[] a, int at) => new V3(a[at], a[at + 1], a[at + 2]);

        public static void Write(Real[] a, int at, V3 v)
        { a[at] = v.X; a[at + 1] = v.Y; a[at + 2] = v.Z; }

        public static void Add(Real[] a, int at, V3 v)
        { a[at] += v.X; a[at + 1] += v.Y; a[at + 2] += v.Z; }

        public static V3 Axis(int d) =>
            d == 0 ? new V3(1, 0, 0) : d == 1 ? new V3(0, 1, 0) : new V3(0, 0, 1);
    }

    public struct M3
    {
        public Real M00, M01, M02, M10, M11, M12, M20, M21, M22;

        public M3(Real m00, Real m01, Real m02, Real m10, Real m11, Real m12,
                  Real m20, Real m21, Real m22)
        {
            M00 = m00; M01 = m01; M02 = m02;
            M10 = m10; M11 = m11; M12 = m12;
            M20 = m20; M21 = m21; M22 = m22;
        }

        public static M3 Zero => new M3(0, 0, 0, 0, 0, 0, 0, 0, 0);

        public static M3 Diagonal(Real a, Real b, Real c) => new M3(a, 0, 0, 0, b, 0, 0, 0, c);

        public static M3 Skew(V3 v) => new M3(0, -v.Z, v.Y, v.Z, 0, -v.X, -v.Y, v.X, 0);

        public M3 Transposed => new M3(M00, M10, M20, M01, M11, M21, M02, M12, M22);

        public static M3 operator +(M3 a, M3 b) => new M3(
            a.M00 + b.M00, a.M01 + b.M01, a.M02 + b.M02,
            a.M10 + b.M10, a.M11 + b.M11, a.M12 + b.M12,
            a.M20 + b.M20, a.M21 + b.M21, a.M22 + b.M22);

        public static M3 operator -(M3 a, M3 b) => new M3(
            a.M00 - b.M00, a.M01 - b.M01, a.M02 - b.M02,
            a.M10 - b.M10, a.M11 - b.M11, a.M12 - b.M12,
            a.M20 - b.M20, a.M21 - b.M21, a.M22 - b.M22);

        public static M3 operator *(M3 a, M3 b) => new M3(
            a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20,
            a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21,
            a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22,

            a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20,
            a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21,
            a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22,

            a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20,
            a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21,
            a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22);

        public static M3 operator *(M3 a, Real s) => new M3(
            a.M00 * s, a.M01 * s, a.M02 * s,
            a.M10 * s, a.M11 * s, a.M12 * s,
            a.M20 * s, a.M21 * s, a.M22 * s);

        public static V3 operator *(M3 m, V3 v) => new V3(
            m.M00 * v.X + m.M01 * v.Y + m.M02 * v.Z,
            m.M10 * v.X + m.M11 * v.Y + m.M12 * v.Z,
            m.M20 * v.X + m.M21 * v.Y + m.M22 * v.Z);

        public V3 TransposedTimes(V3 v) => new V3(
            M00 * v.X + M10 * v.Y + M20 * v.Z,
            M01 * v.X + M11 * v.Y + M21 * v.Z,
            M02 * v.X + M12 * v.Y + M22 * v.Z);

        public static M3 RotateDiagonal(M3 r, V3 d)
        {
            Real a00 = r.M00 * d.X, a01 = r.M01 * d.Y, a02 = r.M02 * d.Z;
            Real a10 = r.M10 * d.X, a11 = r.M11 * d.Y, a12 = r.M12 * d.Z;
            Real a20 = r.M20 * d.X, a21 = r.M21 * d.Y, a22 = r.M22 * d.Z;

            return new M3(
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

        public static M3 Read(Real[] a, int at) => new M3(
            a[at], a[at + 1], a[at + 2],
            a[at + 3], a[at + 4], a[at + 5],
            a[at + 6], a[at + 7], a[at + 8]);

        public static void Write(Real[] a, int at, M3 m)
        {
            a[at] = m.M00; a[at + 1] = m.M01; a[at + 2] = m.M02;
            a[at + 3] = m.M10; a[at + 4] = m.M11; a[at + 5] = m.M12;
            a[at + 6] = m.M20; a[at + 7] = m.M21; a[at + 8] = m.M22;
        }
    }

    public struct Q4
    {
        public Real X, Y, Z, W;
        public Q4(Real x, Real y, Real z, Real w) { X = x; Y = y; Z = z; W = w; }

        public static Q4 Identity => new Q4(0, 0, 0, 1);

        public static Q4 FromAxisAngle(V3 axis, Real radians)
        {
            V3 n = axis.Normalized;
            if (n.SqrMagnitude < (Real)1e-24) return Identity;
            Real half = radians * (Real)0.5;
            Real s = System.MathF.Sin(half);
            return new Q4(n.X * s, n.Y * s, n.Z * s, System.MathF.Cos(half));
        }

        public static Q4 operator *(Q4 a, Q4 b) => new Q4(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        public Q4 Conjugate => new Q4(-X, -Y, -Z, W);

        public V3 Rotate(V3 v)
        {
            var q = new V3(X, Y, Z);
            V3 t = V3.Cross(q, v) * (Real)2.0;
            return v + t * W + V3.Cross(q, t);
        }

        public Real SqrMagnitude => X * X + Y * Y + Z * Z + W * W;

        public Q4 Normalized
        {
            get
            {
                Real m = System.MathF.Sqrt(SqrMagnitude);
                if (!(m > (Real)1e-300)) return Identity;
                Real inv = (Real)1.0 / m;
                return new Q4(X * inv, Y * inv, Z * inv, W * inv);
            }
        }

        public M3 ToMatrix()
        {
            Real xx = X * X, yy = Y * Y, zz = Z * Z;
            Real xy = X * Y, xz = X * Z, yz = Y * Z;
            Real wx = W * X, wy = W * Y, wz = W * Z;

            return new M3(
                (Real)1 - (Real)2 * (yy + zz), (Real)2 * (xy - wz), (Real)2 * (xz + wy),
                (Real)2 * (xy + wz), (Real)1 - (Real)2 * (xx + zz), (Real)2 * (yz - wx),
                (Real)2 * (xz - wy), (Real)2 * (yz + wx), (Real)1 - (Real)2 * (xx + yy));
        }

        public Q4 IntegratedBy(V3 w, Real dt)
        {
            var q = new Q4(w.X, w.Y, w.Z, 0);
            Q4 d = q * this;
            Real h = (Real)0.5 * dt;
            return new Q4(X + d.X * h, Y + d.Y * h, Z + d.Z * h, W + d.W * h).Normalized;
        }

        public Q4 IntegratedByBody(V3 w, Real dt)
        {
            var q = new Q4(w.X, w.Y, w.Z, 0);
            Q4 d = this * q;
            Real h = (Real)0.5 * dt;
            return new Q4(X + d.X * h, Y + d.Y * h, Z + d.Z * h, W + d.W * h).Normalized;
        }

        public V3 RotationVector()
        {
            Real w = W;
            var v = new V3(X, Y, Z);
            if (w < 0) { w = -w; v = -v; }

            Real length = v.Magnitude;
            if (length < (Real)1e-12) return v * (Real)2.0;

            Real angle = (Real)2.0 * System.MathF.Atan2(length, w);
            return v * (angle / length);
        }

        public static Q4 Read(Real[] a, int at) => new Q4(a[at], a[at + 1], a[at + 2], a[at + 3]);

        public static void Write(Real[] a, int at, Q4 q)
        { a[at] = q.X; a[at + 1] = q.Y; a[at + 2] = q.Z; a[at + 3] = q.W; }
    }

    public static class Featherstone
    {
        public const int MaxLinks = 9;
        public const int MaxDof = 27;
        public const int MaxPanels = 288;

        public static void Kernel(Index1D index, GTopo t, GBody b, GPanels pan, GState s, GCfg cfg)
        {
            int c = index;
            int n = cfg.N;
            if (c >= n) return;

            int links = t.Links[c];
            int dof = t.Dof[c];

            // ---- per-thread state and scratch -------------------------------------------------
            var parent = new int[MaxLinks];
            var dofCount = new int[MaxLinks];
            var dofStart = new int[MaxLinks];
            var panelStart = new int[MaxLinks + 1];

            var mass = new Real[MaxLinks];
            var lift = new Real[MaxLinks];
            var inertiaLocal = new Real[3 * MaxLinks];
            var childAnchor = new Real[3 * MaxLinks];
            var parentAnchor = new Real[3 * MaxLinks];
            var jointFrame = new Real[4 * MaxLinks];
            var restFrame = new Real[4 * MaxLinks];
            var driveTorque = new Real[3 * MaxLinks];

            var limitLo = new Real[MaxDof];
            var limitHi = new Real[MaxDof];
            var limitStiff = new Real[MaxDof];
            var limitDamp = new Real[MaxDof];

            var q = new Real[MaxDof];
            var qd = new Real[MaxDof];
            var tau = new Real[MaxDof];
            var limitImplicit = new Real[MaxDof];

            var ballRot = new Real[4 * MaxLinks];
            var position = new Real[3 * MaxLinks];
            var rotation = new Real[4 * MaxLinks];
            var rotationMatrix = new Real[9 * MaxLinks];
            var spin = new Real[3 * MaxLinks];
            var velocity = new Real[3 * MaxLinks];

            var iaA = new Real[9 * MaxLinks];
            var iaB = new Real[9 * MaxLinks];
            var iaC = new Real[9 * MaxLinks];
            var pa = new Real[6 * MaxLinks];
            var sang = new Real[9 * MaxLinks];
            var slin = new Real[9 * MaxLinks];
            var cbias = new Real[6 * MaxLinks];
            var un = new Real[9 * MaxLinks];
            var uf = new Real[9 * MaxLinks];
            var dinv = new Real[9 * MaxLinks];
            var ubar = new Real[3 * MaxLinks];
            var acc = new Real[6 * MaxLinks];
            var fext = new Real[6 * MaxLinks];

            var six = new Real[42];
            var sol = new Real[6];
            var residual = new Real[3];

            // ---- load -------------------------------------------------------------------------
            for (int i = 0; i < links; i++)
            {
                parent[i] = t.Parent[i * n + c];
                dofCount[i] = t.DofCount[i * n + c];
                dofStart[i] = t.DofStart[i * n + c];
                panelStart[i] = t.PanelStart[i * n + c];

                mass[i] = b.Mass[i * n + c];
                lift[i] = b.Lift[i * n + c];

                for (int k = 0; k < 3; k++)
                {
                    inertiaLocal[3 * i + k] = b.Inertia[(3 * i + k) * n + c];
                    childAnchor[3 * i + k] = b.ChildAnchor[(3 * i + k) * n + c];
                    parentAnchor[3 * i + k] = b.ParentAnchor[(3 * i + k) * n + c];
                    driveTorque[3 * i + k] = b.DriveTorque[(3 * i + k) * n + c];
                }

                for (int k = 0; k < 4; k++)
                {
                    jointFrame[4 * i + k] = b.JointFrame[(4 * i + k) * n + c];
                    restFrame[4 * i + k] = b.RestFrame[(4 * i + k) * n + c];
                    ballRot[4 * i + k] = s.BallRot[(4 * i + k) * n + c];
                }
            }
            panelStart[links] = t.PanelStart[links * n + c];

            for (int d = 0; d < dof; d++)
            {
                limitLo[d] = b.LimitLo[d * n + c];
                limitHi[d] = b.LimitHi[d * n + c];
                limitStiff[d] = b.LimitStiff[d * n + c];
                limitDamp[d] = b.LimitDamp[d * n + c];
                q[d] = s.Q[d * n + c];
                qd[d] = s.Qd[d * n + c];
            }

            Real excess = b.Excess[c];

            var basePosition = new V3(s.BasePos[c], s.BasePos[n + c], s.BasePos[2 * n + c]);
            var baseRotation = new Q4(
                s.BaseRot[c], s.BaseRot[n + c], s.BaseRot[2 * n + c], s.BaseRot[3 * n + c]);

            V3.Write(spin, 0, new V3(s.RootSpin[c], s.RootSpin[n + c], s.RootSpin[2 * n + c]));
            V3.Write(velocity, 0, new V3(s.RootVel[c], s.RootVel[n + c], s.RootVel[2 * n + c]));

            // The state as loaded is a pose and a set of joint coordinates; everything else the
            // step reads is derived. Creature's constructor and PlaceAt call Kinematics.Refresh
            // for exactly this reason.
            Poses(links, parent, dofCount, dofStart, q, ballRot, rotation, rotationMatrix,
                  position, restFrame, jointFrame, parentAnchor, childAnchor,
                  basePosition, baseRotation);
            Velocities(links, parent, dofCount, dofStart, q, qd, rotation, jointFrame,
                       childAnchor, position, spin, velocity, sang, slin, cbias);

            // ---- the loop ---------------------------------------------------------------------
            for (int step = 0; step < cfg.Steps; step++)
            {
                for (int i = 0; i < 6 * links; i++) fext[i] = 0;
                for (int i = 0; i < dof; i++) tau[i] = 0;

                Drive(links, parent, dofCount, driveTorque, rotationMatrix, fext);

                FluidApply(links, cfg, excess, mass, lift, position, rotationMatrix, spin,
                           velocity, panelStart, pan, fext, c, n);

                JointTorques(links, dofCount, dofStart, q, qd, limitLo, limitHi,
                             limitStiff, limitDamp, limitImplicit, tau, cfg.Damping, cfg.Dt);

                Seed(links, rotationMatrix, inertiaLocal, mass, spin, fext, iaA, iaB, iaC, pa);
                Inward(links, parent, dofCount, dofStart, sang, slin, iaA, iaB, iaC, pa,
                       un, uf, dinv, ubar, tau, limitImplicit, cbias, position);
                Outward(links, parent, dofCount, dofStart, iaA, iaB, iaC, pa, acc, un, uf,
                        dinv, ubar, tau, sang, slin, cbias, position, six, sol, residual);

                Integrate(links, dofCount, dofStart, tau, q, qd, ballRot, acc, spin, velocity,
                          ref basePosition, ref baseRotation, cfg.Dt);

                Poses(links, parent, dofCount, dofStart, q, ballRot, rotation, rotationMatrix,
                      position, restFrame, jointFrame, parentAnchor, childAnchor,
                      basePosition, baseRotation);
                Velocities(links, parent, dofCount, dofStart, q, qd, rotation, jointFrame,
                           childAnchor, position, spin, velocity, sang, slin, cbias);
            }

            // ---- store ------------------------------------------------------------------------
            s.BasePos[c] = basePosition.X;
            s.BasePos[n + c] = basePosition.Y;
            s.BasePos[2 * n + c] = basePosition.Z;

            s.BaseRot[c] = baseRotation.X;
            s.BaseRot[n + c] = baseRotation.Y;
            s.BaseRot[2 * n + c] = baseRotation.Z;
            s.BaseRot[3 * n + c] = baseRotation.W;

            for (int k = 0; k < 3; k++)
            {
                s.RootSpin[k * n + c] = spin[k];
                s.RootVel[k * n + c] = velocity[k];
            }

            for (int d = 0; d < dof; d++)
            {
                s.Q[d * n + c] = q[d];
                s.Qd[d * n + c] = qd[d];
            }

            for (int i = 0; i < links; i++)
            {
                for (int k = 0; k < 4; k++) s.BallRot[(4 * i + k) * n + c] = ballRot[4 * i + k];
                for (int k = 0; k < 3; k++) s.OutPos[(3 * i + k) * n + c] = position[3 * i + k];
            }
        }

        // ------------------------------------------------------------------ EffectorDrive.Drive
        //
        // The tail of EffectorDrive.Drive with the ten-sample average frozen: DriveTorque holds
        // sum_d frame.Rotate(AxisOf(d)) * magnitude_d in the link's own frame, which is constant
        // once the signal is. The limiter does not engage at dt 0.01.

        private static void Drive(
            int links, int[] parent, int[] dofCount, Real[] driveTorque,
            Real[] rotationMatrix, Real[] fext)
        {
            for (int i = 1; i < links; i++)
            {
                if (dofCount[i] == 0) continue;

                M3 rotation = M3.Read(rotationMatrix, 9 * i);
                V3 world = rotation * V3.Read(driveTorque, 3 * i);

                V3.Add(fext, 6 * i, world);
                V3.Add(fext, 6 * parent[i], -world);
            }
        }

        // ------------------------------------------------------------------------- Fluid.Apply

        private static void FluidApply(
            int links, GCfg cfg, Real excess, Real[] mass, Real[] lift, Real[] position,
            Real[] rotationMatrix, Real[] spin, Real[] velocity, int[] panelStart,
            GPanels pan, Real[] fext, int c, int n)
        {
            for (int i = 0; i < links; i++)
            {
                M3 rotation = M3.Read(rotationMatrix, 9 * i);

                // Still water: the relative velocity is the link's own.
                V3 relative = V3.Read(velocity, 3 * i);
                V3 w = V3.Read(spin, 3 * i);

                V3 localVelocity = rotation.TransposedTimes(relative);
                V3 localSpin = rotation.TransposedTimes(w);

                V3 localForce = V3.Zero;
                V3 localTorque = V3.Zero;

                int from = panelStart[i];
                int to = panelStart[i + 1];

                for (int p = from; p < to; p++)
                {
                    var centre = new V3(
                        pan.Centre[(3 * p) * n + c],
                        pan.Centre[(3 * p + 1) * n + c],
                        pan.Centre[(3 * p + 2) * n + c]);

                    var normal = new V3(
                        pan.Normal[(3 * p) * n + c],
                        pan.Normal[(3 * p + 1) * n + c],
                        pan.Normal[(3 * p + 2) * n + c]);

                    V3 panelVelocity = localVelocity + V3.Cross(localSpin, centre);
                    Real normalSpeed = V3.Dot(panelVelocity, normal);
                    if (normalSpeed <= 0) continue;

                    V3 panelForce = normal *
                        (-cfg.DragK * pan.Area[p * n + c] * normalSpeed * normalSpeed);

                    localForce = localForce + panelForce;
                    localTorque = localTorque + V3.Cross(centre, panelForce);
                }

                V3 force = rotation * localForce;
                V3 torque = rotation * localTorque;

                Real height = position[3 * i + 1];

                Real netDensity = excess * ((Real)1.0 - lift[i]);

                if (cfg.UseRestore != 0)
                {
                    netDensity = Restore(
                        netDensity, height, cfg.RestoringDensity, cfg.WorldDepth,
                        cfg.FloorRestores != 0);
                }
                else if (netDensity < 0 && height >= 0)
                {
                    netDensity = 0;
                }

                if (netDensity != 0)
                {
                    force = force + new V3(
                        0,
                        -netDensity * mass[i] * cfg.Gravity / cfg.TissueDensity,
                        0);
                }

                V3.Add(fext, 6 * i, torque);
                V3.Add(fext, 6 * i + 3, force);
            }
        }

        private static Real Restore(
            Real netDensity, Real heightY, Real restoringDensity, Real worldDepthMetres,
            bool floorRestores)
        {
            if (!(restoringDensity > 0)) return netDensity < 0 && heightY >= 0 ? 0 : netDensity;

            if (heightY > 0) return netDensity > restoringDensity ? netDensity : restoringDensity;
            if (netDensity <= 0 && heightY == 0) return 0;

            if (floorRestores && heightY < -worldDepthMetres)
            {
                return netDensity < -restoringDensity ? netDensity : -restoringDensity;
            }

            return netDensity;
        }

        // ------------------------------------------------------- DynamicsWorld.JointTorques

        private static void JointTorques(
            int links, int[] dofCount, int[] dofStart, Real[] q, Real[] qd,
            Real[] limitLo, Real[] limitHi, Real[] limitStiff, Real[] limitDamp,
            Real[] limitImplicit, Real[] tau, Real damping, Real dt)
        {
            for (int i = 1; i < links; i++)
            {
                int n = dofCount[i];
                if (n == 0) continue;

                int at = dofStart[i];
                for (int d = 0; d < n; d++)
                {
                    int j = at + d;
                    Real value = q[j];
                    Real rate = qd[j];
                    Real torque = -damping * rate;
                    Real resist = damping * dt;

                    Real past = value < limitLo[j] ? value - limitLo[j]
                        : value > limitHi[j] ? value - limitHi[j]
                        : 0;

                    if (past != 0)
                    {
                        Real k = limitStiff[j];
                        Real cc = limitDamp[j];

                        torque += -k * past - cc * rate;
                        resist += (cc + k * dt) * dt;
                    }

                    limitImplicit[j] = resist;
                    tau[j] += torque;
                }
            }
        }

        // ------------------------------------------------------------------------- Kinematics

        private static Q4 JointRotation(
            int link, int[] dofCount, int[] dofStart, Real[] q, Real[] ballRot)
        {
            int n = dofCount[link];
            if (n == 0) return Q4.Identity;
            if (n == 3) return Q4.Read(ballRot, 4 * link);

            int at = dofStart[link];
            Q4 r = Q4.FromAxisAngle(V3.Axis(0), q[at]);
            for (int d = 1; d < n; d++)
            {
                r = r * Q4.FromAxisAngle(V3.Axis(d), q[at + d]);
            }
            return r;
        }

        private static V3 SubspaceAxis(
            int link, int d, int[] dofCount, int[] dofStart, Real[] q)
        {
            int n = dofCount[link];
            int at = dofStart[link];

            if (n == 3) return V3.Axis(d);

            V3 s = V3.Axis(d);
            for (int m = n - 1; m > d; m--)
            {
                s = Q4.FromAxisAngle(V3.Axis(m), -q[at + m]).Rotate(s);
            }
            return s;
        }

        private static void Poses(
            int links, int[] parent, int[] dofCount, int[] dofStart, Real[] q, Real[] ballRot,
            Real[] rotation, Real[] rotationMatrix, Real[] position, Real[] restFrame,
            Real[] jointFrame, Real[] parentAnchor, Real[] childAnchor,
            V3 basePosition, Q4 baseRotation)
        {
            Q4.Write(rotation, 0, baseRotation);
            M3.Write(rotationMatrix, 0, baseRotation.ToMatrix());
            V3.Write(position, 0, basePosition);

            for (int i = 1; i < links; i++)
            {
                int p = parent[i];

                Q4 parentRotation = Q4.Read(rotation, 4 * p);
                Q4 rest = Q4.Read(restFrame, 4 * i);
                Q4 frame = Q4.Read(jointFrame, 4 * i);
                Q4 joint = JointRotation(i, dofCount, dofStart, q, ballRot);

                Q4 turned = (parentRotation * rest * joint * frame.Conjugate).Normalized;

                Q4.Write(rotation, 4 * i, turned);
                M3.Write(rotationMatrix, 9 * i, turned.ToMatrix());

                V3 parentPosition = V3.Read(position, 3 * p);
                V3 pAnchor = parentRotation.Rotate(V3.Read(parentAnchor, 3 * i));
                V3 cAnchor = turned.Rotate(V3.Read(childAnchor, 3 * i));

                V3.Write(position, 3 * i, parentPosition + pAnchor - cAnchor);
            }
        }

        private static void Velocities(
            int links, int[] parent, int[] dofCount, int[] dofStart, Real[] q, Real[] qd,
            Real[] rotation, Real[] jointFrame, Real[] childAnchor, Real[] position,
            Real[] spin, Real[] velocity, Real[] sang, Real[] slin, Real[] cbias)
        {
            V3.Write(cbias, 0, V3.Zero);
            V3.Write(cbias, 3, V3.Zero);

            for (int i = 1; i < links; i++)
            {
                int p = parent[i];
                int n = dofCount[i];
                int at = dofStart[i];

                Q4 turned = Q4.Read(rotation, 4 * i);
                Q4 frame = Q4.Read(jointFrame, 4 * i);
                Q4 jointWorld = turned * frame;

                V3 cAnchor = turned.Rotate(V3.Read(childAnchor, 3 * i));

                V3 parentSpin = V3.Read(spin, 3 * p);
                V3 parentVelocity = V3.Read(velocity, 3 * p);

                V3 relativeSpin = V3.Zero;
                V3 sigma = V3.Zero;
                V3 frameSpin = parentSpin;

                for (int d = 0; d < n; d++)
                {
                    V3 axis = jointWorld.Rotate(SubspaceAxis(i, d, dofCount, dofStart, q));

                    V3.Write(sang, 9 * i + 3 * d, axis);
                    V3.Write(slin, 9 * i + 3 * d, -V3.Cross(axis, cAnchor));

                    Real rate = qd[at + d];
                    relativeSpin = relativeSpin + axis * rate;

                    if (n == 3) continue;

                    frameSpin = frameSpin + axis * rate;
                    sigma = sigma + V3.Cross(frameSpin, axis) * rate;
                }

                if (n == 3) sigma = V3.Cross(parentSpin, relativeSpin);

                V3 w = parentSpin + relativeSpin;

                V3 offset = V3.Read(position, 3 * i) - V3.Read(position, 3 * p);
                V3 v = parentVelocity +
                       V3.Cross(parentSpin, offset) -
                       V3.Cross(relativeSpin, cAnchor);

                V3.Write(spin, 3 * i, w);
                V3.Write(velocity, 3 * i, v);

                V3 linear =
                    V3.Cross(parentSpin, v - parentVelocity) -
                    V3.Cross(sigma, cAnchor) -
                    V3.Cross(relativeSpin, V3.Cross(w, cAnchor));

                V3.Write(cbias, 6 * i, sigma);
                V3.Write(cbias, 6 * i + 3, linear);
            }
        }

        // -------------------------------------------------------------------------------- Aba

        private static void Seed(
            int links, Real[] rotationMatrix, Real[] inertiaLocal, Real[] mass, Real[] spin,
            Real[] fext, Real[] iaA, Real[] iaB, Real[] iaC, Real[] pa)
        {
            for (int i = 0; i < links; i++)
            {
                M3 rotation = M3.Read(rotationMatrix, 9 * i);
                M3 inertia = M3.RotateDiagonal(rotation, V3.Read(inertiaLocal, 3 * i));

                M3.Write(iaA, 9 * i, inertia);
                M3.Write(iaB, 9 * i, M3.Zero);
                M3.Write(iaC, 9 * i, M3.Diagonal(mass[i], mass[i], mass[i]));

                V3 w = V3.Read(spin, 3 * i);
                V3 gyroscopic = V3.Cross(w, inertia * w);

                V3.Write(pa, 6 * i, gyroscopic - V3.Read(fext, 6 * i));
                V3.Write(pa, 6 * i + 3, -V3.Read(fext, 6 * i + 3));
            }
        }

        private static M3 Outer(V3 a, V3 b) => new M3(
            a.X * b.X, a.X * b.Y, a.X * b.Z,
            a.Y * b.X, a.Y * b.Y, a.Y * b.Z,
            a.Z * b.X, a.Z * b.Y, a.Z * b.Z);

        private static void Inward(
            int links, int[] parent, int[] dofCount, int[] dofStart, Real[] sang, Real[] slin,
            Real[] iaA, Real[] iaB, Real[] iaC, Real[] pa, Real[] un, Real[] uf, Real[] dinv,
            Real[] ubar, Real[] tau, Real[] limitImplicit, Real[] cbias, Real[] position)
        {
            for (int i = links - 1; i >= 1; i--)
            {
                int p = parent[i];
                int n = dofCount[i];

                M3 a = M3.Read(iaA, 9 * i);
                M3 bb = M3.Read(iaB, 9 * i);
                M3 cc = M3.Read(iaC, 9 * i);

                V3 paN = V3.Read(pa, 6 * i);
                V3 paF = V3.Read(pa, 6 * i + 3);

                if (n > 0)
                {
                    int at = dofStart[i];

                    for (int d = 0; d < n; d++)
                    {
                        V3 sa = V3.Read(sang, 9 * i + 3 * d);
                        V3 sl = V3.Read(slin, 9 * i + 3 * d);

                        V3.Write(un, 9 * i + 3 * d, a * sa + bb * sl);
                        V3.Write(uf, 9 * i + 3 * d, bb.TransposedTimes(sa) + cc * sl);
                    }

                    Real d00 = 0, d01 = 0, d02 = 0, d11 = 0, d12 = 0, d22 = 0;
                    for (int j = 0; j < n; j++)
                    {
                        V3 sa = V3.Read(sang, 9 * i + 3 * j);
                        V3 sl = V3.Read(slin, 9 * i + 3 * j);

                        ubar[3 * i + j] =
                            tau[at + j] - (V3.Dot(sa, paN) + V3.Dot(sl, paF));

                        for (int k = j; k < n; k++)
                        {
                            Real v = V3.Dot(sa, V3.Read(un, 9 * i + 3 * k)) +
                                     V3.Dot(sl, V3.Read(uf, 9 * i + 3 * k));

                            if (j == 0 && k == 0) d00 = v;
                            else if (j == 0 && k == 1) d01 = v;
                            else if (j == 0 && k == 2) d02 = v;
                            else if (j == 1 && k == 1) d11 = v;
                            else if (j == 1 && k == 2) d12 = v;
                            else d22 = v;
                        }
                    }

                    d00 += limitImplicit[at];
                    if (n > 1) d11 += limitImplicit[at + 1];
                    if (n > 2) d22 += limitImplicit[at + 2];

                    InvertSmallSymmetric(dinv, 9 * i, n, d00, d01, d02, d11, d12, d22);

                    for (int j = 0; j < n; j++)
                    {
                        V3 unj = V3.Read(un, 9 * i + 3 * j);
                        V3 ufj = V3.Read(uf, 9 * i + 3 * j);

                        for (int k = 0; k < n; k++)
                        {
                            Real w = dinv[9 * i + 3 * j + k];
                            if (w == 0) continue;

                            V3 unk = V3.Read(un, 9 * i + 3 * k);
                            V3 ufk = V3.Read(uf, 9 * i + 3 * k);

                            a = a - Outer(unj, unk) * w;
                            bb = bb - Outer(unj, ufk) * w;
                            cc = cc - Outer(ufj, ufk) * w;
                        }
                    }

                    V3 cAng = V3.Read(cbias, 6 * i);
                    V3 cLin = V3.Read(cbias, 6 * i + 3);

                    paN = paN + (a * cAng + bb * cLin);
                    paF = paF + (bb.TransposedTimes(cAng) + cc * cLin);

                    for (int j = 0; j < n; j++)
                    {
                        Real y = 0;
                        for (int k = 0; k < n; k++) y += dinv[9 * i + 3 * j + k] * ubar[3 * i + k];

                        paN = paN + V3.Read(un, 9 * i + 3 * j) * y;
                        paF = paF + V3.Read(uf, 9 * i + 3 * j) * y;
                    }
                }
                else
                {
                    V3 cAng = V3.Read(cbias, 6 * i);
                    V3 cLin = V3.Read(cbias, 6 * i + 3);

                    paN = paN + (a * cAng + bb * cLin);
                    paF = paF + (bb.TransposedTimes(cAng) + cc * cLin);
                }

                V3 offset = V3.Read(position, 3 * i) - V3.Read(position, 3 * p);
                M3 k2 = M3.Skew(offset);

                M3 product = bb * k2;
                M3 aParent = a - product - product.Transposed - k2 * cc * k2;
                M3 bParent = bb + k2 * cc;

                M3.Write(iaA, 9 * p, M3.Read(iaA, 9 * p) + aParent);
                M3.Write(iaB, 9 * p, M3.Read(iaB, 9 * p) + bParent);
                M3.Write(iaC, 9 * p, M3.Read(iaC, 9 * p) + cc);

                V3.Add(pa, 6 * p, paN + V3.Cross(offset, paF));
                V3.Add(pa, 6 * p + 3, paF);
            }
        }

        private static void Outward(
            int links, int[] parent, int[] dofCount, int[] dofStart, Real[] iaA, Real[] iaB,
            Real[] iaC, Real[] pa, Real[] acc, Real[] un, Real[] uf, Real[] dinv, Real[] ubar,
            Real[] tau, Real[] sang, Real[] slin, Real[] cbias, Real[] position,
            Real[] six, Real[] sol, Real[] residual)
        {
            M3 a0 = M3.Read(iaA, 0);
            M3 b0 = M3.Read(iaB, 0);
            M3 c0 = M3.Read(iaC, 0);
            V3 pn = V3.Read(pa, 0);
            V3 pf = V3.Read(pa, 3);

            SolveSixBySix(a0, b0, c0, -pn, -pf, six, sol);

            V3.Write(acc, 0, new V3(sol[0], sol[1], sol[2]));
            V3.Write(acc, 3, new V3(sol[3], sol[4], sol[5]));

            for (int i = 1; i < links; i++)
            {
                int p = parent[i];
                int n = dofCount[i];

                V3 parentAngular = V3.Read(acc, 6 * p);
                V3 parentLinear = V3.Read(acc, 6 * p + 3);
                V3 offset = V3.Read(position, 3 * i) - V3.Read(position, 3 * p);

                V3 angular = parentAngular + V3.Read(cbias, 6 * i);
                V3 linear = parentLinear + V3.Cross(parentAngular, offset) +
                            V3.Read(cbias, 6 * i + 3);

                if (n > 0)
                {
                    int at = dofStart[i];

                    for (int k = 0; k < n; k++)
                    {
                        residual[k] = ubar[3 * i + k] -
                            (V3.Dot(V3.Read(un, 9 * i + 3 * k), angular) +
                             V3.Dot(V3.Read(uf, 9 * i + 3 * k), linear));
                    }

                    for (int j = 0; j < n; j++)
                    {
                        Real acceleration = 0;
                        for (int k = 0; k < n; k++)
                        {
                            acceleration += dinv[9 * i + 3 * j + k] * residual[k];
                        }

                        tau[at + j] = acceleration;
                        angular = angular + V3.Read(sang, 9 * i + 3 * j) * acceleration;
                        linear = linear + V3.Read(slin, 9 * i + 3 * j) * acceleration;
                    }
                }

                V3.Write(acc, 6 * i, angular);
                V3.Write(acc, 6 * i + 3, linear);
            }
        }

        private static void Integrate(
            int links, int[] dofCount, int[] dofStart, Real[] tau, Real[] q, Real[] qd,
            Real[] ballRot, Real[] acc, Real[] spin, Real[] velocity,
            ref V3 basePosition, ref Q4 baseRotation, Real dt)
        {
            for (int i = 1; i < links; i++)
            {
                int n = dofCount[i];
                if (n == 0) continue;

                int at = dofStart[i];
                for (int d = 0; d < n; d++) qd[at + d] += tau[at + d] * dt;

                if (n == 3)
                {
                    var rate = new V3(qd[at], qd[at + 1], qd[at + 2]);

                    Q4 turned = Q4.Read(ballRot, 4 * i).IntegratedByBody(rate, dt);
                    Q4.Write(ballRot, 4 * i, turned);

                    V3 angles = turned.RotationVector();
                    q[at] = angles.X;
                    q[at + 1] = angles.Y;
                    q[at + 2] = angles.Z;
                }
                else
                {
                    for (int d = 0; d < n; d++) q[at + d] += qd[at + d] * dt;
                }
            }

            V3 w = V3.Read(spin, 0) + V3.Read(acc, 0) * dt;
            V3 v = V3.Read(velocity, 0) + V3.Read(acc, 3) * dt;

            V3.Write(spin, 0, w);
            V3.Write(velocity, 0, v);

            basePosition = basePosition + v * dt;
            baseRotation = baseRotation.IntegratedBy(w, dt);
        }

        private static void InvertSmallSymmetric(
            Real[] into, int at, int n,
            Real d00, Real d01, Real d02, Real d11, Real d12, Real d22)
        {
            for (int i = 0; i < 9; i++) into[at + i] = 0;

            if (n == 1)
            {
                into[at] = d00 != 0 ? (Real)1.0 / d00 : 0;
                return;
            }

            if (n == 2)
            {
                Real det = d00 * d11 - d01 * d01;
                if (det == 0) return;
                Real inv = (Real)1.0 / det;
                into[at + 0] = d11 * inv;
                into[at + 1] = -d01 * inv;
                into[at + 3] = -d01 * inv;
                into[at + 4] = d00 * inv;
                return;
            }

            Real c00 = d11 * d22 - d12 * d12;
            Real c01 = d02 * d12 - d01 * d22;
            Real c02 = d01 * d12 - d02 * d11;
            Real determinant = d00 * c00 + d01 * c01 + d02 * c02;
            if (determinant == 0) return;

            Real k = (Real)1.0 / determinant;
            Real c11 = d00 * d22 - d02 * d02;
            Real c12 = d02 * d01 - d00 * d12;
            Real c22 = d00 * d11 - d01 * d01;

            into[at + 0] = c00 * k; into[at + 1] = c01 * k; into[at + 2] = c02 * k;
            into[at + 3] = c01 * k; into[at + 4] = c11 * k; into[at + 5] = c12 * k;
            into[at + 6] = c02 * k; into[at + 7] = c12 * k; into[at + 8] = c22 * k;
        }

        private static void SolveSixBySix(
            M3 a, M3 b, M3 c, V3 rhsAngular, V3 rhsLinear, Real[] m, Real[] x)
        {
            for (int i = 0; i < 42; i++) m[i] = 0;

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
                Real best = System.MathF.Abs(m[col * 7 + col]);
                for (int row = col + 1; row < 6; row++)
                {
                    Real v = System.MathF.Abs(m[row * 7 + col]);
                    if (v > best) { best = v; pivot = row; }
                }

                if (pivot != col)
                {
                    for (int k = col; k < 7; k++)
                    {
                        Real swap = m[col * 7 + k];
                        m[col * 7 + k] = m[pivot * 7 + k];
                        m[pivot * 7 + k] = swap;
                    }
                }

                Real diagonal = m[col * 7 + col];
                if (diagonal == 0) continue;

                for (int row = col + 1; row < 6; row++)
                {
                    Real factor = m[row * 7 + col] / diagonal;
                    if (factor == 0) continue;
                    for (int k = col; k < 7; k++) m[row * 7 + k] -= factor * m[col * 7 + k];
                }
            }

            for (int row = 5; row >= 0; row--)
            {
                Real sum = m[row * 7 + 6];
                for (int k = row + 1; k < 6; k++) sum -= m[row * 7 + k] * x[k];
                Real diagonal = m[row * 7 + row];
                x[row] = diagonal != 0 ? sum / diagonal : 0;
            }
        }
    }
}

// ---------------------------------------------------------------------------------------------
// GENERATED from spike4/step.template by spike4/gen4.py. Do not edit; edit the template and
// regenerate.
//
// The whole body phase of DynamicsWorld.Step as ONE ILGPU kernel, a thread a body, every sub-phase
// in the CPU's order (DynamicsWorld.StepOne, src/Evosim.Dynamics/DynamicsWorld.cs:151-188), with the
// grid kernels of the third spike in front of it and the commit as a swap of two sphere buffers
// behind it. Sources, line for line (commit aa0fedd):
//
//   the water pass     DynamicsWorld.SampleWater / Water.Sample      DynamicsWorld.Forces.cs:36-62, Water.cs:48-118
//                      CurrentField.VelocityAt / StreamsAt / StreamsSlopedUnit / MapAt / StreamsUnit
//                      CurrentField.AccelerationAt / StreamsAccelerationAt / StreamsUnitWithGradient /
//                      StreamsSlopedUnitWithGradient                 CurrentField.cs:813-831, 1255-1270,
//                                                                    1899-1917, 2070-2090, 2176-2185,
//                                                                    2742-2808, 3833-3970, 4074-4362
//                      BedShape.HeightGradientAndHessian             BedShape.cs:750-800
//   senses             CreatureSenses.Sample / Read / Squash         (spike3/brain.template, as proved)
//   brain              Brain.Step / Evaluate / Apply / Read          (spike3/brain.template, as proved)
//   drive              EffectorDrive.Drive (the window, the cursor, the limiter branch, the pair)
//                                                                    EffectorDrive.cs:127-193
//   fluid              Fluid.Apply (panels, the ledger's NoteDrag, the drag limiter branch, D090's
//                      Morison term, buoyancy and Restore)          Fluid.cs:31-167, 187-202
//   contacts           Contacts.Apply, ContactGrid.Neighbours, ContactBed.Push, ContactLaw
//                                                                    (spike3/contact.template, as proved;
//                                                                    here the push is ADDED into Fext)
//   joint torques      DynamicsWorld.JointTorques + NotePassiveTorque DynamicsWorld.cs:204-256
//   solve / integrate  Aba.Seed / Inward / Outward / Integrate       (kernel.template, as proved; Outward
//                                                                    parks the joint accelerations in tau
//                                                                    and Settle reads them there)
//   poses, velocities  Kinematics.Poses / Velocities                 (kernel.template, as proved)
//   settle             Creature.Settle                               Creature.Ledger.cs:160-220
//   trace              Creature.RecordTraceFrame                     CreatureTrace.cs:119-195
//   finiteness         Creature.IsFinite + MarkLost                  Creature.cs:449-453, 575-587
//   sphere refresh     Creature.RefreshContactSphere                 Creature.cs:427-446
//
// What the kernel does NOT carry, and why:
//   - D100's water hold (WaterHoldSeconds > 0): round 45 runs at 0; the kernel refuses a hold.
//   - The rolls and the box's transport field: the port refuses a box (gpu-full-step-spec.md).
//   - EffectorDrive.AppliedTorque: a probe's readout that nothing in the step reads.
//   - The contact census (CloseContactStep) and the digest row: serial, cross-body, after the
//     step; the harness computes both from the kernel's overlap lists and state.
//   - A resize's flag on the trace frame: nothing resizes inside a physics step; carried as 0.
// ---------------------------------------------------------------------------------------------

using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Algorithms;

namespace Gpu.Spike4.Sgl
{
    using Real = System.Single;

    /// <summary>Topology and per-body integers: counts per body, then per link, strided by body.</summary>
    public struct STopo
    {
        public ArrayView<int> Links, Dof, SigLen, Mask, Parent, DofCount, DofStart, PanelStart;
        public ArrayView<int> NOff, NCnt, BParent, FirstChild, BDofStart, BDofCount;
    }

    /// <summary>Everything about a body that does not change inside a physics step.</summary>
    public struct SConst
    {
        public ArrayView<Real> Mass, Lift, Volume, SmallI, Reach, Inertia, ChildAnchor, ParentAnchor;
        public ArrayView<Real> JointFrame, RestFrame, LimitLo, LimitHi, LimitStiff, LimitDamp;
        public ArrayView<Real> Excess, TotalMass;
        public ArrayView<float> TorquePerUnit, PowerScale;
    }

    public struct SPanels { public ArrayView<Real> Centre, Normal, Area; }

    /// <summary>The body's state: what the next step reads, and the step's outputs beside it.</summary>
    public struct SState
    {
        public ArrayView<Real> BasePos, BaseRot, Q, Qd, Tau, LimitImplicit, BallRot;
        public ArrayView<Real> Pos, Rot, RotM, Spin, Vel, Sang, Slin, Cbias, RelVel, Water, WaterAcc, Fext;
        public ArrayView<int> Alive;
    }

    /// <summary>The drive's window and signal, the limiter counts and the ledger's four sums.</summary>
    public struct SDrive
    {
        public ArrayView<float> Signal, History, RunSum;
        public ArrayView<int> Cursor, Filled;
        public ArrayView<long> DriveLimited, DragLimited;
        public ArrayView<double> Work, Signed, Dissipated, Passive;
    }

    public struct SNeur
    {
        public ArrayView<int> Op, NIn, Kind, Index, Channel;
        public ArrayView<float> Freq, Phase, Amp, Bias, Const, Weight;
    }

    public struct SBrain
    {
        public ArrayView<float> S, Mem, Reserve, Damage;
        public ArrayView<int> Parity, Contact;
        public ArrayView<double> Clock;
    }

    public struct STrace
    {
        public ArrayView<Real> Ring;
        public ArrayView<long> Step, FirstBadStep;
        public ArrayView<double> Time;
        public ArrayView<int> Enabled, Held, Cursor, FirstBadLink, Resized;
    }

    /// <summary>One set of bounding spheres: the committed set or the pending one.</summary>
    public struct SSph
    {
        public ArrayView<Real> Cx, Cy, Cz, R, Vx, Vy, Vz;
        public ArrayView<int> Active;
    }

    /// <summary>The contact grid, and the contact pass's per-body outputs beside it.</summary>
    public struct SGrid
    {
        public ArrayView<int> Lo, Hi, Counts, Start, Cursor, Items, Flags;
        public ArrayView<Real> Cell;
        public ArrayView<int> Found, Unique, NOver, Over, BedGlass;
    }

    /// <summary>The world a body reads: the current's instants, the streams, the bed, the snow.</summary>
    public struct SWorld
    {
        public ArrayView<Real> Inst, StreamAmp, StreamRate, CellAmp;
        public ArrayView<Real> BedKx, BedKz, BedAmp, BedPhase;
        public ArrayView<Real> Stock;
        public ArrayView<int> LowestLive;
    }

    public struct SCfg
    {
        public int N, MaxN, Buckets, Mask, CandCap, OverCap, EntryCap;
        public int InstBase, HasCurrent, Accelerating, Sloped, HasBed, BedModes, LimitDrive, LimitDrag;
        public int UseRestore, FloorRestores, CreatureContact, HasNutrients, HasReserve;
        public long TraceStep;
        public double TraceTime, DtD;

        public Real Dt, Density, DragCoefficient, DragK, TissueExcess, NeutralVolume, RestoringFraction;
        public Real RestoringDensity, TissueDensity, FluidAccel, AddedMass, Gravity, WorldDepth, Damping;
        public Real SpinBudget;

        public Real Omega, Zeta, MaxSep, MaxDv, TankRadius, AxisX, AxisZ, CellOverride;
        public Real BedRadius, BedDepth, TiltX, TiltZ, Offset;

        public Real CurDepth, CurTankR, EddyWeight, Overturning, PerSecond, Squared;
        public float SigmaSloped, SigmaFlat, Speed;

        public float SWorldDepth, ChemHalf, EnergyScale, FlowScale, RateScale, ConstCE, DtF;
        public int Nx, Ny, Nz, LayerCount, RefugeLayers, LayerStride;
        public float CellMetres, FieldTankR, RefugeFraction, CellVolume;
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

    /// <summary>CurrentField.StreamsGradient: the velocity, its clock derivative and its Jacobian.</summary>
    public struct SG
    {
        public Real Vx, Vy, Vz, Tx, Ty, Tz, Xx, Xy, Xz, Yx, Yy, Yz, Zx, Zy, Zz;
    }

    public static class WholeStep
    {
        public const int MaxLinks = 16;
        public const int MaxDof = 48;
        public const int CandCap = 256;
        public const int Window = 10;              // EffectorDrive.SmoothWindow
        public const int TraceFrames = 3;          // Creature.TraceFrames
        public const int TraceValues = 21;         // Creature.TraceValuesPerLink

        // The instant's layout: the samplers' phase (A) then the analytic acceleration's (B).
        public const int Terms = 24, Cells = 3;
        public const int ACos = 0, ASin = 24, AEnv = 48, ACellEnv = 72, ACell = 75;
        public const int BCos = 78, BSin = 102, BEnv = 126, BEnvRate = 150, BCellEnv = 174,
            BCellEnvRate = 177, BCell = 180, BCellRate = 183;
        public const int InstStride = 186;

        private const int SDepth = 9, SUp = 3, SJointAngle = 0, SJointRate = 1, SChemical = 6, SEnergy = 7,
            SFlow = 8, SContact = 2, SDamage = 5;

        // ============================================================== the grid (spike 3)

        /// <summary>The mean radius in the CPU's order, on one thread (Contacts.cs:80-98).</summary>
        public static void MeanSerial(Index1D index, SSph s, SGrid g, SCfg cfg)
        {
            if (index.X != 0) return;

            Real sum = 0;
            int active = 0;
            for (int i = 0; i < cfg.N; i++)
            {
                if (s.Active[i] == 0) continue;
                sum += s.R[i];
                active++;
            }

            Real mean = active > 0 ? sum / active : 0;
            Real rule = mean > (Real)0.125 ? (Real)2.0 * mean : (Real)0.25;
            g.Cell[0] = cfg.CellOverride > 0 ? cfg.CellOverride : rule;
        }

        /// <summary>The mean radius by a fixed tree over one group. Deterministic; not the CPU's rounding.</summary>
        public static void MeanTree(SSph s, SGrid g, SCfg cfg)
        {
            var sum = SharedMemory.Allocate<Real>(1024);
            var cnt = SharedMemory.Allocate<int>(1024);
            int t = Group.IdxX;
            int G = Group.DimX;

            Real local = 0;
            int n = 0;
            for (int i = t; i < cfg.N; i += G)
            {
                if (s.Active[i] == 0) continue;
                local += s.R[i];
                n++;
            }
            sum[t] = local;
            cnt[t] = n;
            Group.Barrier();

            for (int stride = G / 2; stride > 0; stride >>= 1)
            {
                if (t < stride)
                {
                    sum[t] = sum[t] + sum[t + stride];
                    cnt[t] = cnt[t] + cnt[t + stride];
                }
                Group.Barrier();
            }

            if (t == 0)
            {
                Real mean = cnt[0] > 0 ? sum[0] / cnt[0] : 0;
                Real rule = mean > (Real)0.125 ? (Real)2.0 * mean : (Real)0.25;
                g.Cell[0] = cfg.CellOverride > 0 ? cfg.CellOverride : rule;
            }
        }

        /// <summary>The cells each committed sphere covers, and the histogram (Contacts.cs:110-160).</summary>
        public static void Ranges(Index1D index, SSph s, SGrid g, SCfg cfg)
        {
            int i = index.X;
            int N = cfg.N;
            if (i >= N) return;

            if (s.Active[i] == 0)
            {
                g.Lo[i] = 1; g.Hi[i] = 0;
                g.Lo[N + i] = 1; g.Hi[N + i] = 0;
                g.Lo[2 * N + i] = 1; g.Hi[2 * N + i] = 0;
                return;
            }

            Real cell = g.Cell[0];
            Real r = s.R[i];
            Real cx = s.Cx[i], cy = s.Cy[i], cz = s.Cz[i];

            int lx = Floor((cx - r) / cell), hx = Floor((cx + r) / cell);
            int ly = Floor((cy - r) / cell), hy = Floor((cy + r) / cell);
            int lz = Floor((cz - r) / cell), hz = Floor((cz + r) / cell);

            g.Lo[i] = lx; g.Hi[i] = hx;
            g.Lo[N + i] = ly; g.Hi[N + i] = hy;
            g.Lo[2 * N + i] = lz; g.Hi[2 * N + i] = hz;

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                Atomic.Add(ref g.Counts[Hash(x, y, z) & cfg.Mask], 1);
            }
        }

        /// <summary>The exclusive prefix sum of the bucket counts, on one group (Contacts.cs:162-169).</summary>
        public static void Scan(SGrid g, SCfg cfg)
        {
            var sh = SharedMemory.Allocate<int>(1024);
            int t = Group.IdxX;
            int G = Group.DimX;
            int B = cfg.Buckets;

            int chunk = (B + G - 1) / G;
            int begin = t * chunk;
            int end = begin + chunk;
            if (end > B) end = B;

            int local = 0;
            for (int b = begin; b < end; b++) local += g.Counts[b];
            sh[t] = local;
            Group.Barrier();

            for (int off = 1; off < G; off <<= 1)
            {
                int v = t >= off ? sh[t - off] : 0;
                Group.Barrier();
                sh[t] = sh[t] + v;
                Group.Barrier();
            }

            int running = sh[t] - local;
            for (int b = begin; b < end; b++)
            {
                int n = g.Counts[b];
                g.Start[b] = running;
                g.Cursor[b] = running;
                running += n;
            }

            if (t == G - 1) g.Start[B] = sh[G - 1];
        }

        /// <summary>Every sphere into every bucket it covers (Contacts.cs:171-179). Order within a bucket is a race.</summary>
        public static void Scatter(Index1D index, SGrid g, SCfg cfg)
        {
            int i = index.X;
            int N = cfg.N;
            if (i >= N) return;

            int lx = g.Lo[i], hx = g.Hi[i];
            int ly = g.Lo[N + i], hy = g.Hi[N + i];
            int lz = g.Lo[2 * N + i], hz = g.Hi[2 * N + i];

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                int slot = Atomic.Add(ref g.Cursor[Hash(x, y, z) & cfg.Mask], 1);
                if (slot < cfg.EntryCap) g.Items[slot] = i;
                else Atomic.Add(ref g.Flags[0], 1);
            }
        }

        public static void Empty(Index1D index, SCfg cfg) { }

        public static void Spin(Index1D index, ArrayView<int> sink, int iterations)
        {
            if (index.X != 0) return;
            uint x = (uint)sink[0];
            for (int k = 0; k < iterations; k++) x = x * 1664525u + 1013904223u;
            sink[1] = (int)x;
        }

        // ============================================================== the body phase

        public static void Step(
            Index1D index, STopo t, SConst b, SPanels pan, SState s, SDrive dr, SNeur nr, SBrain br,
            STrace tr, SSph cs, SSph ps, SGrid g, SWorld w, SCfg cfg)
        {
            int i = index.X;
            int N = cfg.N;
            if (i >= N) return;

            // DynamicsWorld.StepOne:154, `if (!body.Alive) return;`. The commit copies the pending
            // sphere, which a lost body stopped refreshing; with the commit a swap of two buffers,
            // the pending buffer has to be handed the committed values for the swap to be the copy.
            if (s.Alive[i] == 0)
            {
                ps.Cx[i] = cs.Cx[i]; ps.Cy[i] = cs.Cy[i]; ps.Cz[i] = cs.Cz[i]; ps.R[i] = cs.R[i];
                ps.Vx[i] = cs.Vx[i]; ps.Vy[i] = cs.Vy[i]; ps.Vz[i] = cs.Vz[i];
                ps.Active[i] = cs.Active[i];
                return;
            }

            int links = t.Links[i];
            int dof = t.Dof[i];

            // ---- the thread's own copy of the body -------------------------------------------
            var parent = new int[MaxLinks];
            var dofCount = new int[MaxLinks];
            var dofStart = new int[MaxLinks];
            var panelStart = new int[MaxLinks + 1];

            var mass = new Real[MaxLinks];
            var lift = new Real[MaxLinks];
            var volume = new Real[MaxLinks];
            var smallI = new Real[MaxLinks];
            var reach = new Real[MaxLinks];
            var inertiaLocal = new Real[3 * MaxLinks];
            var childAnchor = new Real[3 * MaxLinks];
            var parentAnchor = new Real[3 * MaxLinks];
            var jointFrame = new Real[4 * MaxLinks];
            var restFrame = new Real[4 * MaxLinks];

            var limitLo = new Real[MaxDof];
            var limitHi = new Real[MaxDof];
            var limitStiff = new Real[MaxDof];
            var limitDamp = new Real[MaxDof];
            var q = new Real[MaxDof];
            var qd = new Real[MaxDof];
            var tau = new Real[MaxDof];
            var limitImplicit = new Real[MaxDof];
            var passiveTq = new Real[MaxDof];
            var preRate = new Real[MaxDof];

            var ballRot = new Real[4 * MaxLinks];
            var position = new Real[3 * MaxLinks];
            var rotation = new Real[4 * MaxLinks];
            var rotationMatrix = new Real[9 * MaxLinks];
            var spin = new Real[3 * MaxLinks];
            var velocity = new Real[3 * MaxLinks];
            var relVel = new Real[3 * MaxLinks];
            var water = new Real[3 * MaxLinks];
            var wacc = new Real[3 * MaxLinks];

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

            var driveTq = new Real[3 * MaxLinks];
            var preRelSpin = new Real[3 * MaxLinks];
            var dragF = new Real[3 * MaxLinks];
            var dragT = new Real[3 * MaxLinks];
            var preVel = new Real[3 * MaxLinks];
            var preSpin = new Real[3 * MaxLinks];

            var sDepth = new float[MaxLinks];
            var sUp = new float[MaxLinks];
            var sChem = new float[MaxLinks];
            var sFlow = new float[3 * MaxLinks];
            var sAngle = new float[MaxDof];
            var sRate = new float[MaxDof];

            var six = new Real[42];
            var sol = new Real[6];
            var residual = new Real[3];
            var cand = new int[CandCap];

            // A card does not zero a thread's local memory. The senses the brain may read without
            // this step writing them (a channel whose mask bit is clear) read 0, never garbage.
            for (int k = 0; k < MaxLinks; k++)
            {
                sDepth[k] = 0f; sUp[k] = 0f; sChem[k] = 0f;
                sFlow[3 * k] = 0f; sFlow[3 * k + 1] = 0f; sFlow[3 * k + 2] = 0f;
            }
            for (int k = 0; k < MaxDof; k++) { sAngle[k] = 0f; sRate[k] = 0f; }

            // ---- load ------------------------------------------------------------------------
            for (int l = 0; l < links; l++)
            {
                int at = l * N + i;
                parent[l] = t.Parent[at];
                dofCount[l] = t.DofCount[at];
                dofStart[l] = t.DofStart[at];
                panelStart[l] = t.PanelStart[at];

                mass[l] = b.Mass[at];
                lift[l] = b.Lift[at];
                volume[l] = b.Volume[at];
                smallI[l] = b.SmallI[at];
                reach[l] = b.Reach[at];

                for (int k = 0; k < 3; k++)
                {
                    int a3 = (3 * l + k) * N + i;
                    inertiaLocal[3 * l + k] = b.Inertia[a3];
                    childAnchor[3 * l + k] = b.ChildAnchor[a3];
                    parentAnchor[3 * l + k] = b.ParentAnchor[a3];
                    position[3 * l + k] = s.Pos[a3];
                    spin[3 * l + k] = s.Spin[a3];
                    velocity[3 * l + k] = s.Vel[a3];
                    relVel[3 * l + k] = s.RelVel[a3];
                    driveTq[3 * l + k] = 0;
                }

                for (int k = 0; k < 4; k++)
                {
                    int a4 = (4 * l + k) * N + i;
                    jointFrame[4 * l + k] = b.JointFrame[a4];
                    restFrame[4 * l + k] = b.RestFrame[a4];
                    ballRot[4 * l + k] = s.BallRot[a4];
                    rotation[4 * l + k] = s.Rot[a4];
                }

                for (int k = 0; k < 9; k++)
                {
                    int a9 = (9 * l + k) * N + i;
                    rotationMatrix[9 * l + k] = s.RotM[a9];
                    sang[9 * l + k] = s.Sang[a9];
                    slin[9 * l + k] = s.Slin[a9];
                }

                for (int k = 0; k < 6; k++) cbias[6 * l + k] = s.Cbias[(6 * l + k) * N + i];
            }
            panelStart[links] = t.PanelStart[links * N + i];

            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                limitLo[d] = b.LimitLo[at];
                limitHi[d] = b.LimitHi[at];
                limitStiff[d] = b.LimitStiff[at];
                limitDamp[d] = b.LimitDamp[at];
                q[d] = s.Q[at];
                qd[d] = s.Qd[at];
            }

            var basePosition = new V3(s.BasePos[i], s.BasePos[N + i], s.BasePos[2 * N + i]);
            var baseRotation = new Q4(s.BaseRot[i], s.BaseRot[N + i], s.BaseRot[2 * N + i], s.BaseRot[3 * N + i]);

            Real excess = b.Excess[i];
            Real totalMass = b.TotalMass[i];

            // ---- 1. the water pass (Water.Sample, per link, no hold) --------------------------
            if (cfg.HasCurrent != 0)
            {
                int ib = cfg.InstBase;
                for (int l = 0; l < links; l++)
                {
                    float x = (float)position[3 * l];
                    float y = (float)position[3 * l + 1];
                    float z = (float)position[3 * l + 2];

                    float wx, wy, wz;
                    VelocityAt(x, y, z, w, ib, cfg, out wx, out wy, out wz);
                    water[3 * l] = wx;
                    water[3 * l + 1] = wy;
                    water[3 * l + 2] = wz;

                    if (cfg.Accelerating == 0) continue;

                    float ax, ay, az;
                    AccelerationAt(x, y, z, w, ib, cfg, out ax, out ay, out az);
                    wacc[3 * l] = ax;
                    wacc[3 * l + 1] = ay;
                    wacc[3 * l + 2] = az;
                }
            }
            else
            {
                for (int l = 0; l < links; l++)
                    for (int k = 0; k < 3; k++)
                    {
                        water[3 * l + k] = s.Water[(3 * l + k) * N + i];
                        wacc[3 * l + k] = s.WaterAcc[(3 * l + k) * N + i];
                    }
            }

            // ---- 2. senses, 3. brain ----------------------------------------------------------
            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << SDepth)) != 0;
            bool readsUp = (mask & (1 << SUp)) != 0;
            bool readsJoint = (mask & (1 << SJointAngle)) != 0 || (mask & (1 << SJointRate)) != 0;
            bool readsFlow = (mask & (1 << SFlow)) != 0;
            bool readsChemical = (mask & (1 << SChemical)) != 0;
            bool readsEnergy = (mask & (1 << SEnergy)) != 0;

            float energy = 0f;
            Sample(i, links, dofCount, dofStart, position, rotationMatrix, relVel, q, qd, limitHi,
                   br, w, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical, readsEnergy,
                   sDepth, sUp, sChem, sFlow, sAngle, sRate, ref energy);

            BrainStep(i, links, dof, t, nr, br, dr, cfg, dofCount, dofStart,
                      sDepth, sUp, sChem, sFlow, sAngle, sRate, energy);

            // ---- clear -----------------------------------------------------------------------
            for (int k = 0; k < 6 * links; k++) fext[k] = 0;
            for (int k = 0; k < dof; k++) tau[k] = 0;

            // ---- 4. drive (EffectorDrive.Drive) ----------------------------------------------
            bool drivePending = false;
            if (dof != 0)
            {
                int sigLen = t.SigLen[i];
                int cursor = dr.Cursor[i];
                int filled = dr.Filled[i];

                for (int d = 0; d < dof; d++)
                {
                    float v = d < sigLen ? dr.Signal[d * N + i] : 0f;
                    if (v < -1f) v = -1f;
                    else if (v > 1f) v = 1f;

                    int hAt = (d * Window + cursor) * N + i;
                    float sum = dr.RunSum[d * N + i];
                    sum -= dr.History[hAt];
                    dr.History[hAt] = v;
                    sum += v;
                    dr.RunSum[d * N + i] = sum;
                }

                cursor = (cursor + 1) % Window;
                if (filled < Window) filled++;
                dr.Cursor[i] = cursor;
                dr.Filled[i] = filled;
                float inv = 1f / filled;
                float powerScale = b.PowerScale[i];

                long limited = 0;

                for (int bl = 1; bl < links; bl++)
                {
                    int n = dofCount[bl];
                    if (n == 0) continue;

                    int offset = dofStart[bl];
                    Q4 frame = Q4.Read(jointFrame, 4 * bl);

                    Real allowed = cfg.LimitDrive != 0 ? smallI[bl] * cfg.SpinBudget : 0;

                    V3 torque = V3.Zero;

                    for (int d = 0; d < n; d++)
                    {
                        int idx = offset + d;
                        float smoothed = dr.RunSum[idx * N + i] * inv;
                        float product = smoothed * b.TorquePerUnit[idx * N + i] * powerScale;
                        Real magnitude = product;

                        if (cfg.LimitDrive != 0)
                        {
                            if (magnitude > allowed) { magnitude = allowed; limited++; }
                            else if (magnitude < -allowed) { magnitude = -allowed; limited++; }
                        }

                        torque = torque + frame.Rotate(V3.Axis(d)) * magnitude;
                    }

                    M3 rot = M3.Read(rotationMatrix, 9 * bl);
                    V3 world = rot * torque;

                    // NoteDriveTorque.
                    V3.Write(driveTq, 3 * bl, world);
                    V3.Write(preRelSpin, 3 * bl, V3.Read(spin, 3 * bl) - V3.Read(spin, 3 * parent[bl]));
                    drivePending = true;

                    V3.Add(fext, 6 * bl, world);
                    V3.Add(fext, 6 * parent[bl], -world);
                }

                if (limited != 0) dr.DriveLimited[i] = dr.DriveLimited[i] + limited;
            }

            // ---- 5. fluid (Fluid.Apply) --------------------------------------------------------
            long dragLimited = FluidApply(links, cfg, excess, mass, lift, volume, smallI, position,
                rotationMatrix, spin, velocity, water, wacc, relVel, panelStart, pan, fext,
                dragF, dragT, preVel, preSpin, i, N);
            if (dragLimited != 0) dr.DragLimited[i] = dr.DragLimited[i] + dragLimited;

            // ---- 6. contacts (Contacts.Apply) ------------------------------------------------
            ContactsApply(i, links, mass, totalMass, fext, cand, cs, g, w, cfg, b.TotalMass);

            // ---- 7. joint torques -------------------------------------------------------------
            JointTorques(links, dofCount, dofStart, q, qd, limitLo, limitHi, limitStiff, limitDamp,
                         limitImplicit, tau, passiveTq, preRate, cfg.Damping, cfg.Dt);

            // ---- 8. the articulated-body solve, 9. integration --------------------------------
            Seed(links, rotationMatrix, inertiaLocal, mass, spin, fext, iaA, iaB, iaC, pa);
            Inward(links, parent, dofCount, dofStart, sang, slin, iaA, iaB, iaC, pa,
                   un, uf, dinv, ubar, tau, limitImplicit, cbias, position);
            Outward(links, parent, dofCount, dofStart, iaA, iaB, iaC, pa, acc, un, uf,
                    dinv, ubar, tau, sang, slin, cbias, position, six, sol, residual);

            Integrate(links, dofCount, dofStart, tau, q, qd, ballRot, acc, spin, velocity,
                      ref basePosition, ref baseRotation, cfg.Dt);

            // ---- 10. poses, 11. velocities ----------------------------------------------------
            Poses(links, parent, dofCount, dofStart, q, ballRot, rotation, rotationMatrix,
                  position, restFrame, jointFrame, parentAnchor, childAnchor,
                  basePosition, baseRotation);
            Velocities(links, parent, dofCount, dofStart, q, qd, rotation, jointFrame,
                       childAnchor, position, spin, velocity, sang, slin, cbias);

            // ---- 12. settle (Creature.Settle), the four sums in double ------------------------
            Settle(i, links, dof, parent, drivePending, driveTq, preRelSpin, spin, dragF, dragT,
                   preVel, velocity, preSpin, passiveTq, preRate, limitImplicit, tau, qd, dr, cfg);

            // ---- 13. the throw trace (jointed bodies) -----------------------------------------
            if (tr.Enabled[i] != 0)
            {
                RecordTraceFrame(i, links, dofCount, dofStart, position, velocity, spin, qd, fext,
                                 water, tr, cfg);
            }

            // ---- 14. the finiteness check -----------------------------------------------------
            bool finite = true;
            for (int l = 0; l < links; l++)
            {
                Real sum =
                    position[3 * l] + position[3 * l + 1] + position[3 * l + 2] +
                    spin[3 * l] + spin[3 * l + 1] + spin[3 * l + 2] +
                    velocity[3 * l] + velocity[3 * l + 1] + velocity[3 * l + 2];

                if (sum != sum || sum == Real.PositiveInfinity || sum == Real.NegativeInfinity)
                {
                    finite = false;
                    break;
                }
            }

            if (!finite)
            {
                // Alive = false; MarkLost(): the pending sphere keeps the centre and velocity it
                // was committed with, and loses its radius and its place in every contact set.
                s.Alive[i] = 0;
                ps.Cx[i] = cs.Cx[i]; ps.Cy[i] = cs.Cy[i]; ps.Cz[i] = cs.Cz[i];
                ps.Vx[i] = cs.Vx[i]; ps.Vy[i] = cs.Vy[i]; ps.Vz[i] = cs.Vz[i];
                ps.R[i] = 0;
                ps.Active[i] = 0;
            }
            else
            {
                // ---- 15. the contact sphere, pending (RefreshContactSphere) -------------------
                V3 centre = V3.Read(position, 0);
                Real radius = 0;
                V3 momentum = V3.Zero;

                for (int l = 0; l < links; l++)
                {
                    V3 at = V3.Read(position, 3 * l);
                    Real r = (at - centre).Magnitude + reach[l];
                    if (r > radius) radius = r;

                    momentum = momentum + V3.Read(velocity, 3 * l) * mass[l];
                }

                V3 mean = totalMass > 0 ? momentum * ((Real)1.0 / totalMass) : V3.Zero;
                ps.Cx[i] = centre.X; ps.Cy[i] = centre.Y; ps.Cz[i] = centre.Z;
                ps.R[i] = radius;
                ps.Vx[i] = mean.X; ps.Vy[i] = mean.Y; ps.Vz[i] = mean.Z;
                ps.Active[i] = 1;
            }

            // ---- store -----------------------------------------------------------------------
            s.BasePos[i] = basePosition.X;
            s.BasePos[N + i] = basePosition.Y;
            s.BasePos[2 * N + i] = basePosition.Z;
            s.BaseRot[i] = baseRotation.X;
            s.BaseRot[N + i] = baseRotation.Y;
            s.BaseRot[2 * N + i] = baseRotation.Z;
            s.BaseRot[3 * N + i] = baseRotation.W;

            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                s.Q[at] = q[d];
                s.Qd[at] = qd[d];
                s.Tau[at] = tau[d];
                s.LimitImplicit[at] = limitImplicit[d];
            }

            for (int l = 0; l < links; l++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a3 = (3 * l + k) * N + i;
                    s.Pos[a3] = position[3 * l + k];
                    s.Spin[a3] = spin[3 * l + k];
                    s.Vel[a3] = velocity[3 * l + k];
                    s.RelVel[a3] = relVel[3 * l + k];
                    s.Water[a3] = water[3 * l + k];
                    s.WaterAcc[a3] = wacc[3 * l + k];
                }
                for (int k = 0; k < 4; k++)
                {
                    int a4 = (4 * l + k) * N + i;
                    s.BallRot[a4] = ballRot[4 * l + k];
                    s.Rot[a4] = rotation[4 * l + k];
                }
                for (int k = 0; k < 9; k++)
                {
                    int a9 = (9 * l + k) * N + i;
                    s.RotM[a9] = rotationMatrix[9 * l + k];
                    s.Sang[a9] = sang[9 * l + k];
                    s.Slin[a9] = slin[9 * l + k];
                }
                for (int k = 0; k < 6; k++)
                {
                    int a6 = (6 * l + k) * N + i;
                    s.Cbias[a6] = cbias[6 * l + k];
                    s.Fext[a6] = fext[6 * l + k];
                }
            }
        }

        // ============================================================== the water pass

        /// <summary>CurrentField.VelocityAt(x, y, z, t) in a tank with the vent off: StreamsAt.</summary>
        private static void VelocityAt(float x, float y, float z, SWorld w, int ib, SCfg cfg,
                                       out float ox, out float oy, out float oz)
        {
            if (cfg.Speed <= 0f) { ox = 0f; oy = 0f; oz = 0f; return; }

            float ux, uy, uz;
            if (cfg.Sloped == 0)
            {
                // StreamsUnit(...) * (_speed * _streamsScale): a float product.
                StreamsUnit(x, y, z, cfg.Overturning, w, ib, cfg, out ux, out uy, out uz);
                ox = ux * cfg.SigmaFlat; oy = uy * cfg.SigmaFlat; oz = uz * cfg.SigmaFlat;
                return;
            }

            // StreamsSlopedUnit(...) * (float)(_speed * _streamsScale * _bedScale).
            Real h, hx, hz, hxx, hxz, hzz;
            BedSample(x, z, w, cfg, out h, out hx, out hz, out hxx, out hxz, out hzz);

            // MapAt.
            Real depth = cfg.CurDepth;
            Real d = depth - h;
            Real floorY = -depth + h;
            Real yy = y;
            if (yy > 0) yy = 0;
            else if (yy < floorY) yy = floorY;
            Real c = depth / d;
            Real mappedY = yy * c;
            Real scale = yy * depth / (d * d);
            Real a = scale * hx;
            Real bb = scale * hz;

            StreamsUnit(x, mappedY, z, cfg.Overturning, w, ib, cfg, out ux, out uy, out uz);

            float sx = (float)(c * ux);
            float sy = (float)(uy - a * ux - bb * uz);
            float sz = (float)(c * uz);

            ox = sx * cfg.SigmaSloped; oy = sy * cfg.SigmaSloped; oz = sz * cfg.SigmaSloped;
        }

        /// <summary>CurrentField.StreamsUnit at the samplers' instant (A).</summary>
        private static void StreamsUnit(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg,
                                        out float ox, out float oy, out float oz)
        {
            Real depth = cfg.CurDepth;
            Real radius = cfg.CurTankR;

            if (y > 0) y = 0;
            else if (y < -depth) y = -depth;

            bool atFace = y >= 0 || y <= -depth;

            Real dx = x - radius;
            Real dz = z - radius;
            Real r = System.MathF.Sqrt(dx * dx + dz * dz);
            Real s = r / radius;
            if (s > 1) s = 1;

            Real cosTheta = r > 0 ? dx / r : 1;
            Real sinTheta = r > 0 ? dz / r : 0;

            Real radial = 0;
            Real azimuthal = 0;
            Real vy = 0;

            Real phi = (Real)Math.PI * y / depth;
            Real cosPhi = System.MathF.Cos(phi);
            Real sinPhi = System.MathF.Sin(phi);

            Real sin1 = atFace ? 0 : sinPhi;
            Real sin2 = atFace ? 0 : (Real)2 * sinPhi * cosPhi;
            Real sin3 = atFace ? 0 : sinPhi * ((Real)4 * cosPhi * cosPhi - (Real)1);

            Real cosM = cosTheta;
            Real sinM = sinTheta;

            int k = 0;

            for (int m = 1; m <= 4; m++)
            {
                Real sPow = 1;
                for (int e = 1; e < m; e++) sPow *= s;

                Real wall = (Real)1 - s * s;
                Real node = (Real)1 - (Real)2 * s * s;

                for (int j = 1; j <= 2; j++)
                {
                    Real overR;
                    Real slope;

                    if (j == 1)
                    {
                        overR = sPow * wall / radius;
                        slope = (m * sPow - (m + 2) * sPow * s * s) / radius;
                    }
                    else
                    {
                        overR = sPow * wall * node / radius;
                        slope = (m * sPow
                                 - (Real)3 * (m + 2) * sPow * s * s
                                 + (Real)2 * (m + 4) * sPow * s * s * s * s) / radius;
                    }

                    for (int qq = 1; qq <= 3; qq++, k++)
                    {
                        Real profile = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;
                        if (profile == 0) continue;

                        Real amplitude =
                            cfg.EddyWeight * w.StreamAmp[k] * w.Inst[ib + AEnv + k] * profile;

                        Real ic = w.Inst[ib + ACos + k];
                        Real isn = w.Inst[ib + ASin + k];
                        Real cosChi = cosM * ic - sinM * isn;
                        Real sinChi = sinM * ic + cosM * isn;

                        radial -= amplitude * m * overR * sinChi;
                        azimuthal -= amplitude * slope * cosChi;
                    }
                }

                Real nextCos = cosM * cosTheta - sinM * sinTheta;
                Real nextSin = sinM * cosTheta + cosM * sinTheta;
                cosM = nextCos;
                sinM = nextSin;
            }

            if (overturning != 0)
            {
                Real cos1 = cosPhi;
                Real cos2 = cosPhi * cosPhi - sinPhi * sinPhi;
                Real cos3 = cosPhi * ((Real)4 * cosPhi * cosPhi - (Real)3);

                Real wall = (Real)1 - s;

                for (int c = 0; c < Cells; c++)
                {
                    int qq = c + 1;
                    Real ky = qq * (Real)Math.PI / depth;
                    Real amplitude =
                        overturning * w.CellAmp[c] * w.Inst[ib + ACellEnv + c] * w.Inst[ib + ACell + c];

                    Real cosKy = qq == 1 ? cos1 : qq == 2 ? cos2 : cos3;
                    Real sinKy = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;

                    radial -= amplitude * r * wall * wall * ky * cosKy;

                    vy += (Real)2 * amplitude * wall * ((Real)1 - (Real)2 * s) * sinKy;
                }
            }

            ox = (float)(radial * cosTheta - azimuthal * sinTheta);
            oy = (float)vy;
            oz = (float)(radial * sinTheta + azimuthal * cosTheta);
        }

        /// <summary>CurrentField.AccelerationAt in a tank with the vent off: StreamsAccelerationAt.</summary>
        private static void AccelerationAt(float x, float y, float z, SWorld w, int ib, SCfg cfg,
                                           out float ox, out float oy, out float oz)
        {
            if (cfg.Speed <= 0f) { ox = 0f; oy = 0f; oz = 0f; return; }

            SG g = cfg.Sloped == 0
                ? StreamsUnitWithGradient(x, y, z, cfg.Overturning, w, ib, cfg)
                : StreamsSlopedUnitWithGradient(x, y, z, cfg.Overturning, w, ib, cfg);

            Real perSecond = cfg.PerSecond;
            Real squared = cfg.Squared;

            ox = (float)(perSecond * g.Tx +
                         squared * (g.Vx * g.Xx + g.Vy * g.Xy + g.Vz * g.Xz));
            oy = (float)(perSecond * g.Ty +
                         squared * (g.Vx * g.Yx + g.Vy * g.Yy + g.Vz * g.Yz));
            oz = (float)(perSecond * g.Tz +
                         squared * (g.Vx * g.Zx + g.Vy * g.Zy + g.Vz * g.Zz));
        }

        /// <summary>CurrentField.StreamsSlopedUnitWithGradient at the acceleration's instant (B).</summary>
        private static SG StreamsSlopedUnitWithGradient(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg)
        {
            Real depth = cfg.CurDepth;

            Real h, hx, hz, hxx, hxz, hzz;
            BedSample(x, z, w, cfg, out h, out hx, out hz, out hxx, out hxz, out hzz);

            Real d = depth - h;
            Real floorY = -depth + h;

            if (y > 0) y = 0;
            else if (y < floorY) y = floorY;

            Real inverseSquared = (Real)1 / (d * d);
            Real inverseCubed = inverseSquared / d;

            Real c = depth / d;
            Real a = y * depth * hx * inverseSquared;
            Real b = y * depth * hz * inverseSquared;

            Real cx = depth * hx * inverseSquared;
            Real cz = depth * hz * inverseSquared;

            Real ax = y * depth * (hxx * inverseSquared + (Real)2 * hx * hx * inverseCubed);
            Real az = y * depth * (hxz * inverseSquared + (Real)2 * hx * hz * inverseCubed);
            Real ay = cx;
            Real bx = az;
            Real bz = y * depth * (hzz * inverseSquared + (Real)2 * hz * hz * inverseCubed);
            Real by = cz;

            SG f = StreamsUnitWithGradient(x, y * c, z, overturning, w, ib, cfg);

            Real dxVx = f.Xx + a * f.Xy, dyVx = c * f.Xy, dzVx = f.Xz + b * f.Xy;
            Real dxVy = f.Yx + a * f.Yy, dyVy = c * f.Yy, dzVy = f.Yz + b * f.Yy;
            Real dxVz = f.Zx + a * f.Zy, dyVz = c * f.Zy, dzVz = f.Zz + b * f.Zy;

            var g = default(SG);

            g.Vx = c * f.Vx;
            g.Vz = c * f.Vz;
            g.Vy = f.Vy - a * f.Vx - b * f.Vz;

            g.Tx = c * f.Tx;
            g.Tz = c * f.Tz;
            g.Ty = f.Ty - a * f.Tx - b * f.Tz;

            g.Xx = cx * f.Vx + c * dxVx;
            g.Xy = c * dyVx;
            g.Xz = cz * f.Vx + c * dzVx;

            g.Zx = cx * f.Vz + c * dxVz;
            g.Zy = c * dyVz;
            g.Zz = cz * f.Vz + c * dzVz;

            g.Yx = -ax * f.Vx - bx * f.Vz + (dxVy - a * dxVx - b * dxVz);
            g.Yy = -ay * f.Vx - by * f.Vz + (dyVy - a * dyVx - b * dyVz);
            g.Yz = -az * f.Vx - bz * f.Vz + (dzVy - a * dzVx - b * dzVz);

            return g;
        }

        /// <summary>CurrentField.StreamsUnitWithGradient at the acceleration's instant (B).</summary>
        private static SG StreamsUnitWithGradient(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg)
        {
            Real depth = cfg.CurDepth;
            Real radius = cfg.CurTankR;

            if (y > 0) y = 0;
            else if (y < -depth) y = -depth;

            bool atFace = y >= 0 || y <= -depth;

            Real dx = x - radius;
            Real dz = z - radius;
            Real r = System.MathF.Sqrt(dx * dx + dz * dz);

            Real cosTheta = r > 0 ? dx / r : 1;
            Real sinTheta = r > 0 ? dz / r : 0;

            if (r > radius)
            {
                dx = radius * cosTheta;
                dz = radius * sinTheta;
                r = radius;
            }

            Real s = r / radius;
            Real u = s * s;

            Real phi = (Real)Math.PI * y / depth;
            Real cosPhi = System.MathF.Cos(phi);
            Real sinPhi = System.MathF.Sin(phi);

            Real sin1 = atFace ? 0 : sinPhi;
            Real sin2 = atFace ? 0 : (Real)2 * sinPhi * cosPhi;
            Real sin3 = atFace ? 0 : sinPhi * ((Real)4 * cosPhi * cosPhi - (Real)1);

            Real cos1 = cosPhi;
            Real cos2 = cosPhi * cosPhi - sinPhi * sinPhi;
            Real cos3 = cosPhi * ((Real)4 * cosPhi * cosPhi - (Real)3);

            Real invR = (Real)1 / radius;
            Real gx = (Real)2 * dx * invR * invR;
            Real gz = (Real)2 * dz * invR * invR;

            Real eVx = 0, eVz = 0;
            Real eTx = 0, eTz = 0;
            Real eXx = 0, eXy = 0, eXz = 0;
            Real eZx = 0, eZy = 0, eZz = 0;

            Real ca = 0, sa = 0;
            Real cb = 1, sb = 0;
            Real cc = s * cosTheta, sc = s * sinTheta;
            Real cd = s * (cc * cosTheta - sc * sinTheta);
            Real sd = s * (sc * cosTheta + cc * sinTheta);

            int k = 0;

            for (int m = 1; m <= 4; m++)
            {
                Real mp = (m + 1) * invR;
                Real mm = (m - 1) * invR;

                for (int j = 1; j <= 2; j++)
                {
                    Real alpha, alphaU, beta, betaU;

                    if (j == 1)
                    {
                        alpha = 1;
                        alphaU = 0;
                        beta = m - (m + 1) * u;
                        betaU = -(m + 1);
                    }
                    else
                    {
                        alpha = (Real)3 - (Real)4 * u;
                        alphaU = -4;
                        beta = m - (Real)3 * (m + 1) * u + (2 * m + 4) * u * u;
                        betaU = (Real)(-3) * (m + 1) + (Real)2 * (2 * m + 4) * u;
                    }

                    Real bc = 0, bs = 0;
                    Real yc = 0, ys = 0;
                    Real tc = 0, ts = 0;
                    Real wc = 0, ws = 0;

                    for (int qq = 1; qq <= 3; qq++, k++)
                    {
                        Real profile = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;
                        Real ky = qq * (Real)Math.PI / depth;
                        Real slope = ky * (qq == 1 ? cos1 : qq == 2 ? cos2 : cos3);

                        Real raw = cfg.EddyWeight * w.StreamAmp[k];
                        Real envelope = w.Inst[ib + BEnv + k];

                        Real b = raw * envelope * profile;
                        Real by = raw * envelope * slope;
                        Real bt = raw * w.Inst[ib + BEnvRate + k] * profile;
                        Real bw = b * w.StreamRate[k];

                        Real cosPsi = w.Inst[ib + BCos + k];
                        Real sinPsi = w.Inst[ib + BSin + k];

                        bc += b * cosPsi;
                        bs += b * sinPsi;
                        yc += by * cosPsi;
                        ys += by * sinPsi;
                        tc += bt * cosPsi;
                        ts += bt * sinPsi;
                        wc += bw * cosPsi;
                        ws += bw * sinPsi;
                    }

                    Real psD = sd * bc + cd * bs, pcD = cd * bc - sd * bs;
                    Real psC = sc * bc + cc * bs, pcC = cc * bc - sc * bs;
                    Real psB = sb * bc + cb * bs, pcB = cb * bc - sb * bs;
                    Real psA = sa * bc + ca * bs, pcA = ca * bc - sa * bs;

                    Real ysD = sd * yc + cd * ys, ycD = cd * yc - sd * ys;
                    Real ysB = sb * yc + cb * ys, ycB = cb * yc - sb * ys;

                    Real tsD = sd * tc + cd * ts, tcD = cd * tc - sd * ts;
                    Real tsB = sb * tc + cb * ts, tcB = cb * tc - sb * ts;

                    Real wsD = sd * wc + cd * ws, wcD = cd * wc - sd * ws;
                    Real wsB = sb * wc + cb * ws, wcB = cb * wc - sb * ws;

                    eVx += alpha * psD + beta * psB;
                    eVz += -alpha * pcD + beta * pcB;

                    eXx += alphaU * gx * psD + alpha * mp * psC +
                           betaU * gx * psB + beta * mm * psA;
                    eXz += alphaU * gz * psD + alpha * mp * pcC +
                           betaU * gz * psB + beta * mm * pcA;
                    eZx += -alphaU * gx * pcD - alpha * mp * pcC +
                           betaU * gx * pcB + beta * mm * pcA;
                    eZz += -alphaU * gz * pcD + alpha * mp * psC +
                           betaU * gz * pcB - beta * mm * psA;

                    eXy += alpha * ysD + beta * ysB;
                    eZy += -alpha * ycD + beta * ycB;

                    eTx += alpha * tsD + beta * tsB + alpha * wcD + beta * wcB;
                    eTz += -alpha * tcD + beta * tcB + alpha * wsD - beta * wsB;
                }

                ca = cb; sa = sb;
                cb = cc; sb = sc;
                cc = cd; sc = sd;

                Real nextC = s * (cc * cosTheta - sc * sinTheta);
                Real nextS = s * (sc * cosTheta + cc * sinTheta);
                cd = nextC; sd = nextS;
            }

            var g = default(SG);

            g.Vx = -eVx * invR;
            g.Vz = -eVz * invR;
            g.Tx = -eTx * invR;
            g.Tz = -eTz * invR;
            g.Xx = -eXx * invR;
            g.Xy = -eXy * invR;
            g.Xz = -eXz * invR;
            g.Zx = -eZx * invR;
            g.Zy = -eZy * invR;
            g.Zz = -eZz * invR;

            if (overturning == 0) return g;

            Real wall = (Real)1 - s;
            Real wallSquared = wall * wall;
            Real wallSlope = (Real)(-2) * wall;

            Real vertical = (Real)2 * wall * ((Real)1 - (Real)2 * s);
            Real verticalSlope = (Real)2 * ((Real)4 * s - (Real)3);

            Real sx = r > 0 ? dx / (r * radius) : 0;
            Real sz = r > 0 ? dz / (r * radius) : 0;

            for (int c = 0; c < Cells; c++)
            {
                int qq = c + 1;
                Real ky = qq * (Real)Math.PI / depth;

                Real raw = overturning * w.CellAmp[c];
                Real envelope = w.Inst[ib + BCellEnv + c];
                Real turn = w.Inst[ib + BCell + c];

                Real amplitude = raw * envelope * turn;
                Real rate = raw * (w.Inst[ib + BCellEnvRate + c] * turn +
                                   envelope * w.Inst[ib + BCellRate + c]);

                Real cosKy = qq == 1 ? cos1 : qq == 2 ? cos2 : cos3;
                Real sinKy = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;

                Real horizontal = amplitude * ky * cosKy;
                Real horizontalRate = rate * ky * cosKy;

                g.Vx -= horizontal * wallSquared * dx;
                g.Vz -= horizontal * wallSquared * dz;
                g.Vy += amplitude * vertical * sinKy;

                g.Xx -= horizontal * (wallSquared + wallSlope * sx * dx);
                g.Xz -= horizontal * wallSlope * sz * dx;
                g.Zx -= horizontal * wallSlope * sx * dz;
                g.Zz -= horizontal * (wallSquared + wallSlope * sz * dz);

                g.Xy += amplitude * ky * ky * sinKy * wallSquared * dx;
                g.Zy += amplitude * ky * ky * sinKy * wallSquared * dz;

                g.Yx += amplitude * verticalSlope * sx * sinKy;
                g.Yz += amplitude * verticalSlope * sz * sinKy;
                g.Yy += amplitude * vertical * ky * cosKy;

                g.Tx -= horizontalRate * wallSquared * dx;
                g.Tz -= horizontalRate * wallSquared * dz;
                g.Ty += rate * vertical * sinKy;
            }

            return g;
        }

        /// <summary>BedShape.HeightGradientAndHessian (the memo in CurrentField.BedSample is a pure cache).</summary>
        private static void BedSample(Real x, Real z, SWorld w, SCfg cfg,
            out Real height, out Real gradientX, out Real gradientZ,
            out Real hessianXX, out Real hessianXZ, out Real hessianZZ)
        {
            Real dx = x - cfg.BedRadius;
            Real dz = z - cfg.BedRadius;

            Real h = cfg.TiltX * dx + cfg.TiltZ * dz - cfg.Offset;
            Real gx = cfg.TiltX;
            Real gz = cfg.TiltZ;
            Real xx = 0, xz = 0, zz = 0;

            for (int m = 0; m < cfg.BedModes; m++)
            {
                Real kx = w.BedKx[m], kz = w.BedKz[m];
                Real chi = kx * dx + kz * dz + w.BedPhase[m];
                Real a = w.BedAmp[m];
                Real cos = System.MathF.Cos(chi);
                Real sin = System.MathF.Sin(chi);

                h += a * cos;
                gx -= a * kx * sin;
                gz -= a * kz * sin;

                xx -= a * kx * kx * cos;
                xz -= a * kx * kz * cos;
                zz -= a * kz * kz * cos;
            }

            height = h;
            gradientX = gx;
            gradientZ = gz;
            hessianXX = xx;
            hessianXZ = xz;
            hessianZZ = zz;
        }

        // ============================================================== senses (spike 3)

        private static void Sample(
            int i, int links, int[] dofCount, int[] dofStart, Real[] position, Real[] rotationMatrix,
            Real[] relVel, Real[] q, Real[] qd, Real[] limitHi, SBrain br, SWorld w, SCfg cfg,
            bool readsDepth, bool readsUp, bool readsJoint, bool readsFlow, bool readsChemical, bool readsEnergy,
            float[] sDepth, float[] sUp, float[] sChem, float[] sFlow, float[] sAngle, float[] sRate,
            ref float energy)
        {
            if (readsEnergy && cfg.HasReserve != 0) energy = Squash(br.Reserve[i], cfg.EnergyScale);

            bool smells = readsChemical && cfg.HasNutrients != 0;

            if (!readsDepth && !readsUp && !readsJoint && !readsFlow && !smells) return;

            float flowScale = cfg.FlowScale;
            float rateScale = cfg.RateScale;

            for (int bl = 0; bl < links; bl++)
            {
                Real px = position[3 * bl];
                Real py = position[3 * bl + 1];
                Real pz = position[3 * bl + 2];

                Real finite = px + py + pz;
                if (finite != finite || finite == Real.PositiveInfinity || finite == Real.NegativeInfinity)
                {
                    sDepth[bl] = 0f;
                    sUp[bl] = 0f;
                    sChem[bl] = 0f;

                    if (readsFlow)
                    {
                        sFlow[3 * bl] = 0f;
                        sFlow[3 * bl + 1] = 0f;
                        sFlow[3 * bl + 2] = 0f;
                    }

                    continue;
                }

                if (readsDepth)
                {
                    Real d = -py / (Real)cfg.SWorldDepth;
                    sDepth[bl] = (float)(d < 0 ? 0 : d > 1 ? 1 : d);
                }

                if (readsUp)
                {
                    sUp[bl] = (float)rotationMatrix[9 * bl + 4];
                }

                if (smells)
                {
                    float density = EdibleDensityAt((float)px, (float)py, (float)pz, w, cfg);
                    sChem[bl] = density > 0f ? density / (density + cfg.ChemHalf) : 0f;
                }

                if (readsFlow)
                {
                    Real m00 = rotationMatrix[9 * bl], m01 = rotationMatrix[9 * bl + 1], m02 = rotationMatrix[9 * bl + 2];
                    Real m10 = rotationMatrix[9 * bl + 3], m11 = rotationMatrix[9 * bl + 4], m12 = rotationMatrix[9 * bl + 5];
                    Real m20 = rotationMatrix[9 * bl + 6], m21 = rotationMatrix[9 * bl + 7], m22 = rotationMatrix[9 * bl + 8];
                    Real vx = relVel[3 * bl], vy = relVel[3 * bl + 1], vz = relVel[3 * bl + 2];

                    Real lx = m00 * vx + m10 * vy + m20 * vz;
                    Real ly = m01 * vx + m11 * vy + m21 * vz;
                    Real lz = m02 * vx + m12 * vy + m22 * vz;

                    sFlow[3 * bl] = Clamp((float)(lx / flowScale));
                    sFlow[3 * bl + 1] = Clamp((float)(ly / flowScale));
                    sFlow[3 * bl + 2] = Clamp((float)(lz / flowScale));
                }

                if (!readsJoint) continue;

                int n = dofCount[bl];
                int offset = dofStart[bl];
                if (n == 0 || offset < 0) continue;

                for (int d = 0; d < n; d++)
                {
                    int at = offset + d;
                    Real limit = AbsR(limitHi[at]);

                    sAngle[at] = limit > (Real)1e-6
                        ? Clamp((float)(q[at] / limit))
                        : 0f;

                    sRate[at] = Clamp((float)(qd[at] / rateScale));
                }
            }
        }

        private static float Squash(float seconds, float scale)
        {
            if (seconds != seconds) return 0f;
            if (seconds == float.PositiveInfinity) return 1f;
            if (seconds == float.NegativeInfinity) return -1f;

            return TanhF(seconds / scale);
        }

        private static float EdibleDensityAt(float x, float y, float z, SWorld w, SCfg cfg)
        {
            int cell = TankCellAt(x, y, z, w, cfg);
            Real stock = w.Stock[cell];
            int layer = cell / cfg.LayerStride;
            Real edible = layer >= cfg.LayerCount - cfg.RefugeLayers ? stock * cfg.RefugeFraction : stock;
            return (float)(edible / cfg.CellVolume);
        }

        private static int TankCellAt(float x, float y, float z, SWorld w, SCfg cfg)
        {
            int iy = y >= 0f ? 0 : (int)(-y / cfg.CellMetres);
            if (iy >= cfg.Ny) iy = cfg.Ny - 1;
            if (iy < 0) iy = 0;

            Real dx = x - cfg.FieldTankR;
            Real dz = z - cfg.FieldTankR;
            Real r = SqrtR(dx * dx + dz * dz);

            if (r > 0)
            {
                for (Real reach = r; reach > 0; reach -= (Real)0.5 * cfg.CellMetres)
                {
                    int cell = InColumn(
                        Column((float)(cfg.FieldTankR + dx * reach / r), cfg),
                        Column((float)(cfg.FieldTankR + dz * reach / r), cfg), iy, w, cfg);

                    if (cell >= 0) return cell;
                }
            }

            int axis = InColumn(Column(cfg.FieldTankR, cfg), Column(cfg.FieldTankR, cfg), iy, w, cfg);
            if (axis >= 0) return axis;

            int columns = cfg.Nx * cfg.Nz;
            for (int k = 0; k < columns; k++)
            {
                int cell = InColumn(k / cfg.Nz, k % cfg.Nz, iy, w, cfg);
                if (cell >= 0) return cell;
            }

            return (iy * cfg.Nx + Column(cfg.FieldTankR, cfg)) * cfg.Nz + Column(cfg.FieldTankR, cfg);
        }

        private static int InColumn(int ix, int iz, int iy, SWorld w, SCfg cfg)
        {
            int lowest = w.LowestLive[ix * cfg.Nz + iz];
            if (lowest < 0) return -1;

            return ((iy < lowest ? iy : lowest) * cfg.Nx + ix) * cfg.Nz + iz;
        }

        private static int Column(float v, SCfg cfg)
        {
            int i = (int)(v / cfg.CellMetres);
            if (i >= cfg.Nx) return cfg.Nx - 1;
            return i < 0 ? 0 : i;
        }

        // ============================================================== brain (spike 3)

        private static void BrainStep(
            int i, int links, int dof, STopo t, SNeur nr, SBrain br, SDrive dr, SCfg cfg,
            int[] dofCount, int[] dofStart,
            float[] sDepth, float[] sUp, float[] sChem, float[] sFlow, float[] sAngle, float[] sRate, float energy)
        {
            int N = cfg.N;

            double clock = br.Clock[i] + cfg.DtF;
            br.Clock[i] = clock;
            float time = (float)clock;

            int parity = br.Parity[i];
            int prev = parity * cfg.MaxN;
            int cur = (1 - parity) * cfg.MaxN;

            for (int group = 0; group < links; group++)
            {
                int at = t.NOff[group * N + i];
                int count = t.NCnt[group * N + i];

                for (int n = 0; n < count; n++)
                {
                    br.S[(cur + at + n) * N + i] = Evaluate(i, at + n, group, time, prev, links, dof, t, nr, br, cfg,
                        dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy);
                }
            }

            parity = 1 - parity;
            br.Parity[i] = parity;
            prev = parity * cfg.MaxN;

            for (int p = 0; p < links; p++)
            {
                int start = t.BDofStart[p * N + i];
                if (start < 0) continue;

                int own = t.NCnt[p * N + i];
                int at = t.NOff[p * N + i];
                int dofs = t.BDofCount[p * N + i];

                for (int d = 0; d < dofs; d++)
                {
                    dr.Signal[(start + d) * N + i] = d < own ? Clamp(br.S[(prev + at + d) * N + i]) : 0f;
                }
            }
        }

        private static float Evaluate(
            int i, int self, int part, float time, int prev, int links, int dof, STopo t, SNeur nr, SBrain br, SCfg cfg,
            int[] dofCount, int[] dofStart,
            float[] sDepth, float[] sUp, float[] sChem, float[] sFlow, float[] sAngle, float[] sRate, float energy)
        {
            int slot = self * cfg.N + i;
            int op = nr.Op[slot];
            float value;

            if (op == 22)
            {
                value = OscSin(2.0 * Math.PI * nr.Freq[slot] * time + nr.Phase[slot]);
            }
            else if (op == 23)
            {
                value = Saw(nr.Freq[slot] * time + nr.Phase[slot] / (2f * (float)Math.PI));
            }
            else
            {
                value = Apply(i, self, part, prev, links, dof, t, nr, br, cfg,
                              dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy);
            }

            value = value * nr.Amp[slot] + nr.Bias[slot];

            return value != value || value == float.PositiveInfinity || value == float.NegativeInfinity ? 0f : value;
        }

        private static float Apply(
            int i, int self, int part, int prev, int links, int dof, STopo t, SNeur nr, SBrain br, SCfg cfg,
            int[] dofCount, int[] dofStart,
            float[] sDepth, float[] sUp, float[] sChem, float[] sFlow, float[] sAngle, float[] sRate, float energy)
        {
            int slot = self * cfg.N + i;
            int nIn = nr.NIn[slot];
            float dt = cfg.DtF;

            float a = nIn > 0 ? Read(i, self, 0, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy) : 0f;
            float bb = nIn > 1 ? Read(i, self, 1, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy) : 0f;
            float c = nIn > 2 ? Read(i, self, 2, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy) : 0f;

            switch (nr.Op[slot])
            {
                case 0:     // Sum
                {
                    float acc = 0f;
                    for (int k = 0; k < nIn; k++) acc = acc + Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy);
                    return acc;
                }
                case 1:     // Product
                {
                    if (nIn == 0) return 0f;
                    float acc = 1f;
                    for (int k = 0; k < nIn; k++) acc = acc * Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy);
                    return acc;
                }
                case 4:     // Min
                {
                    if (nIn == 0) return 0f;
                    float acc = float.MaxValue;
                    for (int k = 0; k < nIn; k++) acc = MinF(acc, Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy));
                    return acc;
                }
                case 5:     // Max
                {
                    if (nIn == 0) return 0f;
                    float acc = float.MinValue;
                    for (int k = 0; k < nIn; k++) acc = MaxF(acc, Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy));
                    return acc;
                }

                case 2: return Math.Abs(bb) < 1e-6f ? 0f : a / bb;       // Divide
                case 3: return Math.Abs(a);                              // Abs

                case 10: return a > bb ? 1f : -1f;                       // GreaterThan
                case 11: return a > 0f ? 1f : a < 0f ? -1f : 0f;         // SignOf
                case 12: return a > 0f ? bb : c;                         // If
                case 13: return bb + (c - bb) * Clamp01(a);              // Interpolate

                case 20: return SinF(a);                                 // Sin
                case 21: return CosF(a);                                 // Cos

                case 30: return TanhF(a);                                // Sigmoid

                case 31:    // SumThreshold
                {
                    float acc = 0f;
                    for (int k = 0; k < nIn; k++) acc = acc + Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy);
                    return acc > 0f ? 1f : -1f;
                }

                case 40:    // Integrate
                {
                    float m = Clamp(br.Mem[slot] + a * dt, -100f, 100f);
                    br.Mem[slot] = m;
                    return m;
                }

                case 41:    // Differentiate
                {
                    float rate = dt > 0f ? (a - br.Mem[slot]) / dt : 0f;
                    br.Mem[slot] = a;
                    return rate;
                }

                case 42:    // Smooth
                {
                    float m = br.Mem[slot] + (a - br.Mem[slot]) * Clamp01(bb);
                    br.Mem[slot] = m;
                    return m;
                }

                case 43:    // Memory
                {
                    float held = br.Mem[slot];
                    br.Mem[slot] = a;
                    return held;
                }

                default: return a;
            }
        }

        private static float Read(
            int i, int self, int k, int part, int prev, int links, int dof, STopo t, SNeur nr, SBrain br, SCfg cfg,
            int[] dofCount, int[] dofStart,
            float[] sDepth, float[] sUp, float[] sChem, float[] sFlow, float[] sAngle, float[] sRate, float energy)
        {
            int N = cfg.N;
            int ks = (3 * self + k) * N + i;
            float wt = nr.Weight[ks];

            switch (nr.Kind[ks])
            {
                case 0: return nr.Const[ks] * wt;
                case 1: return SensorRead(i, part, nr.Channel[ks], nr.Index[ks], links, dof, br, cfg,
                                          dofCount, dofStart, sDepth, sUp, sChem, sFlow, sAngle, sRate, energy) * wt;
                case 2: return FromGroup(i, part, nr.Index[ks], prev, t, br, cfg) * wt;
                case 3:
                {
                    int owner = t.BParent[part * N + i];
                    return owner < 0 ? 0f : FromGroup(i, owner, nr.Index[ks], prev, t, br, cfg) * wt;
                }
                case 4:
                {
                    int owner = t.FirstChild[part * N + i];
                    return owner < 0 ? 0f : FromGroup(i, owner, nr.Index[ks], prev, t, br, cfg) * wt;
                }
                default: return 0f;
            }
        }

        private static float FromGroup(int i, int group, int index, int prev, STopo t, SBrain br, SCfg cfg)
        {
            int N = cfg.N;
            int count = t.NCnt[group * N + i];
            if (count == 0) return 0f;

            int at = index % count;
            if (at < 0) at += count;

            return br.S[(prev + t.NOff[group * N + i] + at) * N + i];
        }

        private static float SensorRead(
            int i, int part, int channel, int index, int links, int dof, SBrain br, SCfg cfg,
            int[] dofCount, int[] dofStart,
            float[] sDepth, float[] sUp, float[] sChem, float[] sFlow, float[] sAngle, float[] sRate, float energy)
        {
            int N = cfg.N;
            if (part < 0 || part >= links) return 0f;

            switch (channel)
            {
                case SDepth: return sDepth[part];
                case SUp: return sUp[part];
                case SJointAngle: return DofRead(part, index, 0, dof, dofCount, dofStart, sAngle, sRate);
                case SJointRate: return DofRead(part, index, 1, dof, dofCount, dofStart, sAngle, sRate);
                case SChemical: return cfg.HasNutrients != 0 ? sChem[part] : cfg.ConstCE;
                case SEnergy: return cfg.HasReserve != 0 ? energy : cfg.ConstCE;
                case SFlow: return index >= 0 && index < 3 ? sFlow[3 * part + index] : 0f;
                case SContact: return br.Contact[part * N + i] != 0 ? 1f : 0f;
                case SDamage: return Clamp(br.Damage[part * N + i]);
                default: return 0f;
            }
        }

        private static float DofRead(int part, int index, int which, int dof, int[] dofCount, int[] dofStart,
                                     float[] sAngle, float[] sRate)
        {
            int n = dofCount[part];
            int offset = dofStart[part];

            if (offset < 0 || index < 0 || index >= n) return 0f;

            int at = offset + index;
            int length = dof > 0 ? dof : 1;
            if (at >= length) return 0f;

            return which == 0 ? sAngle[at] : sRate[at];
        }

        private static float Saw(float turns)
        {
            float phase = turns - FloorF(turns);
            return 2f * phase - 1f;
        }

        private static float Clamp(float v) => Clamp(v, -1f, 1f);

        private static float Clamp(float v, float low, float high) =>
            v < low ? low : v > high ? high : v;

        private static float Clamp01(float v) => Clamp(v, 0f, 1f);

        private static bool IsNegative(float x) => (Interop.FloatAsInt(x) & 0x80000000u) != 0;

        private static float MinF(float x, float y)
        {
            if (x != y)
            {
                if (!(x != x)) return x < y ? x : y;
                return x;
            }
            return IsNegative(x) ? x : y;
        }

        private static float MaxF(float x, float y)
        {
            if (x != y)
            {
                if (!(x != x)) return y < x ? x : y;
                return x;
            }
            return IsNegative(y) ? x : y;
        }

        // Single on the card: the oscillator's argument is built in double as the CPU builds it,
        // reduced by whole turns in double, then narrowed; everything else is XMath in float.
        private static float OscSin(double arg)
        {
            const double Turn = 6.283185307179586;
            double reduced = arg - Turn * Math.Floor(arg / Turn);
            return XMath.Sin((float)reduced);
        }
        private static float SinF(float a) => XMath.Sin(a);
        private static float CosF(float a) => XMath.Cos(a);
        private static float TanhF(float a) => XMath.Tanh(a);
        private static float FloorF(float a) => XMath.Floor(a);
        private static Real SqrtR(Real a) => XMath.Sqrt(a);
        private static Real AbsR(Real a) => XMath.Abs(a);


        // ============================================================== fluid

        private static long FluidApply(
            int links, SCfg cfg, Real excess, Real[] mass, Real[] lift, Real[] volume, Real[] smallI,
            Real[] position, Real[] rotationMatrix, Real[] spin, Real[] velocity, Real[] water, Real[] wacc,
            Real[] relVel, int[] panelStart, SPanels pan, Real[] fext,
            Real[] dragF, Real[] dragT, Real[] preVel, Real[] preSpin, int c, int n)
        {
            Real k = cfg.DragK;
            Real dt = cfg.Dt;
            long limited = 0;
            bool accelerating = cfg.FluidAccel > 0;

            for (int i = 0; i < links; i++)
            {
                M3 rotation = M3.Read(rotationMatrix, 9 * i);

                V3 relative = V3.Read(velocity, 3 * i) - V3.Read(water, 3 * i);
                V3.Write(relVel, 3 * i, relative);

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

                    V3 panelForce = normal * (-k * pan.Area[p * n + c] * normalSpeed * normalSpeed);

                    localForce = localForce + panelForce;
                    localTorque = localTorque + V3.Cross(centre, panelForce);
                }

                V3 force = rotation * localForce;
                V3 torque = rotation * localTorque;

                // NoteDrag: before the limiter and the two terms below.
                V3.Write(dragF, 3 * i, force);
                V3.Write(dragT, 3 * i, torque);
                V3.Write(preVel, 3 * i, V3.Read(velocity, 3 * i));
                V3.Write(preSpin, 3 * i, V3.Read(spin, 3 * i));

                if (cfg.LimitDrag != 0)
                {
                    Real speed = relative.Magnitude;
                    if (speed > 0)
                    {
                        V3 direction = relative * ((Real)1.0 / speed);
                        Real opposing = -V3.Dot(force, direction);
                        Real allowed = mass[i] * speed / dt;
                        if (opposing > allowed)
                        {
                            force = force + direction * (opposing - allowed);
                            limited++;
                        }
                    }

                    Real spinRate = w.Magnitude;
                    if (spinRate > 0)
                    {
                        V3 axis = w * ((Real)1.0 / spinRate);
                        Real opposing = -V3.Dot(torque, axis);
                        Real allowed = smallI[i] * spinRate / dt;
                        if (opposing > allowed)
                        {
                            torque = torque + axis * (opposing - allowed);
                            limited++;
                        }
                    }
                }

                Real height = position[3 * i + 1];

                if (accelerating)
                {
                    V3 accelerationForce = V3.Read(wacc, 3 * i) *
                        (cfg.FluidAccel * cfg.Density * volume[i] *
                         ((Real)1.0 + cfg.AddedMass));

                    if (accelerationForce.Y > 0 && height >= 0)
                    {
                        accelerationForce = new V3(accelerationForce.X, 0, accelerationForce.Z);
                    }

                    force = force + accelerationForce;
                }

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

            return limited;
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

        // ============================================================== contacts (spike 3)

        private static void ContactsApply(
            int i, int links, Real[] mass, Real m, Real[] fext, int[] cand, SSph s, SGrid g, SWorld w, SCfg cfg,
            ArrayView<Real> totalMassOf)
        {
            int N = cfg.N;

            Real cx = s.Cx[i], cy = s.Cy[i], cz = s.Cz[i];
            Real r = s.R[i];
            Real vx = s.Vx[i], vy = s.Vy[i], vz = s.Vz[i];

            Real fx = 0, fy = 0, fz = 0;
            int found = 0;
            int unique = 0;
            int nOver = 0;
            int C = CandCap;

            if (cfg.CreatureContact != 0)
            {
                int lx = g.Lo[i], hx = g.Hi[i];
                int ly = g.Lo[N + i], hy = g.Hi[N + i];
                int lz = g.Lo[2 * N + i], hz = g.Hi[2 * N + i];

                for (int x = lx; x <= hx; x++)
                for (int y = ly; y <= hy; y++)
                for (int z = lz; z <= hz; z++)
                {
                    int h = Hash(x, y, z) & cfg.Mask;

                    for (int k = g.Start[h]; k < g.Start[h + 1] && k < cfg.EntryCap; k++)
                    {
                        int other = g.Items[k];
                        if (other == i) continue;

                        if (x < g.Lo[other] || x > g.Hi[other] ||
                            y < g.Lo[N + other] || y > g.Hi[N + other] ||
                            z < g.Lo[2 * N + other] || z > g.Hi[2 * N + other])
                        {
                            continue;
                        }

                        if (found < C) cand[found] = other;
                        found++;
                    }
                }

                int n = found < C ? found : C;

                if (n < 2)
                {
                    unique = n;
                }
                else
                {
                    for (int a = 1; a < n; a++)
                    {
                        int key = cand[a];
                        int bi = a - 1;
                        while (bi >= 0 && cand[bi] > key)
                        {
                            cand[bi + 1] = cand[bi];
                            bi--;
                        }
                        cand[bi + 1] = key;
                    }

                    unique = 1;
                    for (int a = 1; a < n; a++)
                    {
                        int v = cand[a];
                        if (v == cand[unique - 1]) continue;
                        cand[unique] = v;
                        unique++;
                    }
                }

                for (int k = 0; k < unique; k++)
                {
                    int other = cand[k];
                    if (s.Active[other] == 0) continue;

                    Real bx = cx - s.Cx[other];
                    Real by = cy - s.Cy[other];
                    Real bz = cz - s.Cz[other];
                    Real distance = System.MathF.Sqrt(bx * bx + by * by + bz * bz);
                    Real penetration = r + s.R[other] - distance;
                    if (penetration <= 0) continue;

                    if (nOver < cfg.OverCap) g.Over[nOver * N + i] = other;

                    Real nx, ny, nz;
                    if (distance > (Real)1e-9)
                    {
                        Real inv = (Real)1.0 / distance;
                        nx = bx * inv; ny = by * inv; nz = bz * inv;
                    }
                    else
                    {
                        nx = 0; ny = 1; nz = 0;
                    }

                    Real mo = totalMassOf[other];
                    Real reduced = m * mo / (m + mo);

                    Real stiffness = reduced * cfg.Omega * cfg.Omega;
                    Real damping = (Real)2.0 * cfg.Zeta * reduced * cfg.Omega;

                    Real dvx = vx - s.Vx[other];
                    Real dvy = vy - s.Vy[other];
                    Real dvz = vz - s.Vz[other];
                    Real approach = dvx * nx + dvy * ny + dvz * nz;

                    Real p = PairPush(stiffness * penetration - damping * approach, reduced, approach, cfg);

                    fx = fx + nx * p;
                    fy = fy + ny * p;
                    fz = fz + nz * p;

                    nOver++;
                }
            }

            Real bedStiffness = m * cfg.Omega * cfg.Omega;
            Real bedDamping = (Real)2.0 * cfg.Zeta * m * cfg.Omega;
            int bedGlass = 0;

            if (cfg.HasBed == 0)
            {
                Real below = -cfg.WorldDepth - (cy - r);
                if (below > 0)
                {
                    Real up = PairPush(bedStiffness * below - bedDamping * vy, m, vy, cfg);
                    fx = fx + 0;
                    fy = fy + up;
                    fz = fz + 0;
                    bedGlass = 1;
                }
            }
            else
            {
                // ContactBed.Push with BedShape.HeightAndGradient inlined.
                Real dx = cx - cfg.BedRadius;
                Real dz = cz - cfg.BedRadius;

                Real hgt = cfg.TiltX * dx + cfg.TiltZ * dz - cfg.Offset;
                Real gx = cfg.TiltX;
                Real gz = cfg.TiltZ;

                for (int md = 0; md < cfg.BedModes; md++)
                {
                    Real kx = w.BedKx[md], kz = w.BedKz[md];
                    Real chi = kx * dx + kz * dz + w.BedPhase[md];
                    Real a = w.BedAmp[md];
                    Real cos = System.MathF.Cos(chi);
                    Real sin = System.MathF.Sin(chi);

                    hgt += a * cos;
                    gx -= a * kx * sin;
                    gz -= a * kz * sin;
                }

                Real floorY = -cfg.BedDepth + hgt;
                Real slope = System.MathF.Sqrt((Real)1.0 + gx * gx + gz * gz);
                Real pen = r - (cy - floorY) / slope;

                Real rx = 0, ry = 0, rz = 0;
                if (pen > 0)
                {
                    Real inv = (Real)1.0 / slope;
                    Real nx = -gx * inv, ny = (Real)1.0 * inv, nz = -gz * inv;
                    Real approach = vx * nx + vy * ny + vz * nz;
                    Real p = PairPush(bedStiffness * pen - bedDamping * approach, m, approach, cfg);
                    rx = nx * p; ry = ny * p; rz = nz * p;
                }

                if (rx != 0 || ry != 0 || rz != 0)
                {
                    fx = fx + rx;
                    fy = fy + ry;
                    fz = fz + rz;
                    bedGlass = 1;
                }
            }

            if (cfg.TankRadius > 0)
            {
                Real x = cx - cfg.AxisX;
                Real z = cz - cfg.AxisZ;
                Real radial = System.MathF.Sqrt(x * x + z * z);
                Real outside = radial + r - cfg.TankRadius;

                if (outside > 0)
                {
                    Real ix, iy, iz;
                    if (radial > (Real)1e-9) { ix = -x / radial; iy = 0; iz = -z / radial; }
                    else { ix = 1; iy = 0; iz = 0; }

                    Real approach = vx * ix + vy * iy + vz * iz;
                    Real p = PairPush(bedStiffness * outside - bedDamping * approach, m, approach, cfg);

                    fx = fx + ix * p;
                    fy = fy + iy * p;
                    fz = fz + iz * p;
                    bedGlass = 1;
                }
            }

            Real cap = cfg.MaxDv;
            if (cap > 0)
            {
                Real most = m * cap / cfg.Dt;
                Real magnitude = System.MathF.Sqrt(fx * fx + fy * fy + fz * fz);
                if (magnitude > most)
                {
                    Real sc = most / magnitude;
                    fx = fx * sc; fy = fy * sc; fz = fz * sc;
                }
            }

            if (fx != 0 || fy != 0 || fz != 0)
            {
                Real scale = (Real)1.0 / m;
                for (int l = 0; l < links; l++)
                {
                    Real wl = mass[l] * scale;
                    fext[6 * l + 3] += fx * wl;
                    fext[6 * l + 4] += fy * wl;
                    fext[6 * l + 5] += fz * wl;
                }
            }

            g.Found[i] = found;
            g.Unique[i] = unique;
            g.NOver[i] = nOver;
            g.BedGlass[i] = bedGlass;
        }

        private static Real PairPush(Real raw, Real reducedMass, Real approach, SCfg cfg)
        {
            if (!(raw > 0)) return 0;

            Real cap = cfg.MaxSep;
            if (!(cap > 0)) return raw;

            Real allowed = cap - (approach < 0 ? approach : 0);
            Real most = reducedMass * allowed / cfg.Dt;

            return raw < most ? raw : most;
        }

        // ============================================================== joint torques

        private static void JointTorques(
            int links, int[] dofCount, int[] dofStart, Real[] q, Real[] qd,
            Real[] limitLo, Real[] limitHi, Real[] limitStiff, Real[] limitDamp,
            Real[] limitImplicit, Real[] tau, Real[] passiveTq, Real[] preRate, Real damping, Real dt)
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

                    // NotePassiveTorque.
                    passiveTq[j] = torque;
                    preRate[j] = rate;

                    tau[j] += torque;
                }
            }
        }

        // ============================================================== settle

        private static void Settle(
            int i, int links, int dof, int[] parent, bool drivePending, Real[] driveTq, Real[] preRelSpin,
            Real[] spin, Real[] dragF, Real[] dragT, Real[] preVel, Real[] velocity, Real[] preSpin,
            Real[] passiveTq, Real[] preRate, Real[] limitImplicit, Real[] tau, Real[] qd, SDrive dr, SCfg cfg)
        {
            double dt = cfg.DtD;
            if (dt <= 0) return;

            if (drivePending)
            {
                double work = dr.Work[i];
                double signed = dr.Signed[i];

                for (int b = 1; b < links; b++)
                {
                    V3 torque = V3.Read(driveTq, 3 * b);
                    if (torque.X == 0 && torque.Y == 0 && torque.Z == 0) continue;

                    V3 after = V3.Read(spin, 3 * b) - V3.Read(spin, 3 * parent[b]);
                    Real power = V3.Dot(
                        torque, (V3.Read(preRelSpin, 3 * b) + after) * (Real)0.5);

                    work += (double)AbsR(power) * dt;
                    signed += (double)power * dt;
                }

                dr.Work[i] = work;
                dr.Signed[i] = signed;
            }

            // The drag ledger: NoteDrag runs for every link, so it is always pending.
            {
                double dissipated = dr.Dissipated[i];

                for (int l = 0; l < links; l++)
                {
                    V3 v = (V3.Read(preVel, 3 * l) + V3.Read(velocity, 3 * l)) * (Real)0.5;
                    V3 w = (V3.Read(preSpin, 3 * l) + V3.Read(spin, 3 * l)) * (Real)0.5;

                    Real power = V3.Dot(V3.Read(dragF, 3 * l), v) +
                                 V3.Dot(V3.Read(dragT, 3 * l), w);

                    dissipated -= (double)power * dt;
                }

                dr.Dissipated[i] = dissipated;
            }

            if (dof == 0) return;

            double passive = dr.Passive[i];

            for (int j = 0; j < dof; j++)
            {
                Real torque = passiveTq[j] - limitImplicit[j] * tau[j];
                if (torque == 0) continue;

                Real part = torque * (preRate[j] + qd[j]) * (Real)0.5;
                passive += (double)part * dt;
            }

            dr.Passive[i] = passive;
        }

        // ============================================================== trace

        private static void RecordTraceFrame(
            int i, int links, int[] dofCount, int[] dofStart, Real[] position, Real[] velocity, Real[] spin,
            Real[] qd, Real[] fext, Real[] water, STrace tr, SCfg cfg)
        {
            int N = cfg.N;
            int bad = -1;

            for (int b = 0; b < links && bad < 0; b++)
            {
                for (int k = 0; k < TraceValues; k++)
                {
                    Real v = TraceValue(b, k, dofCount, dofStart, position, velocity, spin, qd, fext, water);
                    if (v != v || v == Real.PositiveInfinity || v == Real.NegativeInfinity) { bad = b; break; }
                }
            }

            if (bad >= 0)
            {
                if (tr.FirstBadStep[i] < 0)
                {
                    tr.FirstBadStep[i] = cfg.TraceStep;
                    tr.FirstBadLink[i] = bad;
                }
                return;
            }

            int slot = tr.Cursor[i];
            for (int b = 0; b < links; b++)
            {
                for (int k = 0; k < TraceValues; k++)
                {
                    tr.Ring[((slot * MaxLinks + b) * TraceValues + k) * N + i] =
                        TraceValue(b, k, dofCount, dofStart, position, velocity, spin, qd, fext, water);
                }
            }

            tr.Step[slot * N + i] = cfg.TraceStep;
            tr.Time[slot * N + i] = cfg.TraceTime;
            tr.Resized[slot * N + i] = 0;

            tr.Cursor[i] = slot + 1 == TraceFrames ? 0 : slot + 1;
            int held = tr.Held[i];
            if (held < TraceFrames) tr.Held[i] = held + 1;
        }

        private static Real TraceValue(int b, int k, int[] dofCount, int[] dofStart, Real[] position,
            Real[] velocity, Real[] spin, Real[] qd, Real[] fext, Real[] water)
        {
            if (k < 3) return position[3 * b + k];
            if (k < 6) return velocity[3 * b + k - 3];
            if (k < 9) return spin[3 * b + k - 6];
            if (k < 12)
            {
                int d = k - 9;
                return dofCount[b] > d ? qd[dofStart[b] + d] : 0;
            }
            if (k < 15) return fext[6 * b + 3 + k - 12];
            if (k < 18) return fext[6 * b + k - 15];
            return water[3 * b + k - 18];
        }

        // ============================================================== kinematics (0112)

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

        // ============================================================== the solve (0112)

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

                        // Parked for the integrator; Settle reads it here too.
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

        // ============================================================== small things

        private static int Floor(Real v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        private static int Hash(int x, int y, int z) =>
            (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);
    }
    /// <summary>Device buffers and the launches of the whole step, for one precision.</summary>
    internal sealed class StepRunner : Gpu.Spike4.IStepRunner
    {
        private readonly Accelerator _acc;
        private readonly Gpu.Spike4.StepHost _h;
        private readonly int _n, _buckets, _reduce;

        private readonly MemoryBuffer1D<int, Stride1D.Dense> _links, _dof, _sigLen, _mask, _parent, _dofCount, _dofStart,
            _panelStart, _noff, _ncnt, _bparent, _firstChild, _bdofStart, _bdofCount;
        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _mass, _lift, _volume, _smallI, _reach, _inertia,
            _childAnchor, _parentAnchor, _jointFrame, _restFrame, _limitLo, _limitHi, _limitStiff, _limitDamp,
            _excess, _totalMass;
        private readonly MemoryBuffer1D<float, Stride1D.Dense> _tpu, _powerScale;
        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _pCentre, _pNormal, _pArea;

        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _basePos, _baseRot, _q, _qd, _tau, _limitImplicit, _ballRot,
            _pos, _rot, _rotM, _spin, _vel, _sang, _slin, _cbias, _relVel, _water, _waterAcc, _fext;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _alive;

        private readonly MemoryBuffer1D<float, Stride1D.Dense> _signal, _history, _runSum;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _cursor, _filled;
        private readonly MemoryBuffer1D<long, Stride1D.Dense> _driveLimited, _dragLimited;
        private readonly MemoryBuffer1D<double, Stride1D.Dense> _work, _signed, _dissipated, _passive;

        private readonly MemoryBuffer1D<int, Stride1D.Dense> _op, _nin, _kind, _index, _channel;
        private readonly MemoryBuffer1D<float, Stride1D.Dense> _freq, _phase, _amp, _bias, _const, _weight;

        private readonly MemoryBuffer1D<float, Stride1D.Dense> _s, _mem, _reserve, _damage;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _parity, _contact;
        private readonly MemoryBuffer1D<double, Stride1D.Dense> _clock;

        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _ring;
        private readonly MemoryBuffer1D<long, Stride1D.Dense> _trStep, _trFirstBad;
        private readonly MemoryBuffer1D<double, Stride1D.Dense> _trTime;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _trEnabled, _trHeld, _trCursor, _trFirstBadLink, _trResized;

        // Two sphere sets: which one is committed flips with every step.
        private readonly MemoryBuffer1D<Real, Stride1D.Dense>[] _sphR = new MemoryBuffer1D<Real, Stride1D.Dense>[14];
        private readonly MemoryBuffer1D<int, Stride1D.Dense>[] _sphA = new MemoryBuffer1D<int, Stride1D.Dense>[2];
        private int _committed;

        private readonly MemoryBuffer1D<int, Stride1D.Dense> _lo, _hi, _counts, _start, _gcursor, _items, _flags,
            _found, _unique, _nOver, _over, _bedGlass;
        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _cell;

        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _inst, _streamAmp, _streamRate, _cellAmp,
            _bedKx, _bedKz, _bedAmp, _bedPhase, _stock;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _lowest, _sink;

        private readonly Action<Index1D, SSph, SGrid, SCfg> _meanSerial, _ranges;
        private readonly Action<KernelConfig, SSph, SGrid, SCfg> _meanTree;
        private readonly Action<KernelConfig, SGrid, SCfg> _scan;
        private readonly Action<Index1D, SGrid, SCfg> _scatter;
        private readonly Action<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain, STrace, SSph, SSph,
            SGrid, SWorld, SCfg> _step;
        private readonly Action<Index1D, SCfg> _empty;
        private readonly Action<Index1D, ArrayView<int>, int> _spinKernel;

        private SCfg _cfg;

        public double CompileMs { get; }
        public string Precision => "single";
        public int Buckets => _buckets;
        public int InstantBlock { get; }

        public StepRunner(Accelerator accelerator, Gpu.Spike4.StepHost h, int groupSize, int reduceGroup, int instantBlock)
        {
            _acc = accelerator;
            _h = h;
            _n = h.N;
            _reduce = reduceGroup;
            InstantBlock = instantBlock;

            int buckets = 1;
            while (buckets < _n * 2) buckets <<= 1;
            _buckets = buckets;

            _links = I(h.Links); _dof = I(h.Dof); _sigLen = I(h.SigLen); _mask = I(h.Mask);
            _parent = I(h.Parent); _dofCount = I(h.DofCount); _dofStart = I(h.DofStart); _panelStart = I(h.PanelStart);
            _noff = I(h.NOff); _ncnt = I(h.NCnt); _bparent = I(h.BParent); _firstChild = I(h.FirstChild);
            _bdofStart = I(h.BDofStart); _bdofCount = I(h.BDofCount);

            _mass = R(h.Mass); _lift = R(h.Lift); _volume = R(h.Volume); _smallI = R(h.SmallI); _reach = R(h.Reach);
            _inertia = R(h.Inertia); _childAnchor = R(h.ChildAnchor); _parentAnchor = R(h.ParentAnchor);
            _jointFrame = R(h.JointFrame); _restFrame = R(h.RestFrame);
            _limitLo = R(h.LimitLo); _limitHi = R(h.LimitHi); _limitStiff = R(h.LimitStiff); _limitDamp = R(h.LimitDamp);
            _excess = R(h.Excess); _totalMass = R(h.TotalMass);
            _tpu = F(h.TorquePerUnit); _powerScale = F(h.PowerScale);
            _pCentre = R(h.PanelCentre); _pNormal = R(h.PanelNormal); _pArea = R(h.PanelArea);

            _basePos = Z(h.BasePos.Length); _baseRot = Z(h.BaseRot.Length); _q = Z(h.Q.Length); _qd = Z(h.Qd.Length);
            _tau = Z(h.Tau.Length); _limitImplicit = Z(h.LimitImplicit.Length); _ballRot = Z(h.BallRot.Length);
            _pos = Z(h.Pos.Length); _rot = Z(h.Rot.Length); _rotM = Z(h.RotM.Length); _spin = Z(h.Spin.Length);
            _vel = Z(h.Vel.Length); _sang = Z(h.Sang.Length); _slin = Z(h.Slin.Length); _cbias = Z(h.Cbias.Length);
            _relVel = Z(h.RelVel.Length); _water = Z(h.Water.Length); _waterAcc = Z(h.WaterAcc.Length);
            _fext = Z(h.Fext.Length);
            _alive = _acc.Allocate1D<int>(_n);

            _signal = _acc.Allocate1D<float>(h.Signal.Length);
            _history = _acc.Allocate1D<float>(h.History.Length);
            _runSum = _acc.Allocate1D<float>(h.RunSum.Length);
            _cursor = _acc.Allocate1D<int>(_n); _filled = _acc.Allocate1D<int>(_n);
            _driveLimited = _acc.Allocate1D<long>(_n); _dragLimited = _acc.Allocate1D<long>(_n);
            _work = _acc.Allocate1D<double>(_n); _signed = _acc.Allocate1D<double>(_n);
            _dissipated = _acc.Allocate1D<double>(_n); _passive = _acc.Allocate1D<double>(_n);

            _op = I(h.Op); _nin = I(h.NIn); _kind = I(h.Kind); _index = I(h.Index); _channel = I(h.Channel);
            _freq = F(h.Freq); _phase = F(h.Phase); _amp = F(h.Amp); _bias = F(h.Bias);
            _const = F(h.Const); _weight = F(h.Weight);

            _s = _acc.Allocate1D<float>(h.S0.Length);
            _mem = _acc.Allocate1D<float>(h.Mem0.Length);
            _reserve = F(h.Reserve); _damage = F(h.Damage);
            _parity = _acc.Allocate1D<int>(_n);
            _contact = I(h.Contact);
            _clock = _acc.Allocate1D<double>(_n);

            _ring = Z(h.Ring.Length);
            _trStep = _acc.Allocate1D<long>(h.TraceStep.Length);
            _trFirstBad = _acc.Allocate1D<long>(_n);
            _trTime = _acc.Allocate1D<double>(h.TraceTime.Length);
            _trEnabled = I(h.TraceEnabled);
            _trHeld = _acc.Allocate1D<int>(_n); _trCursor = _acc.Allocate1D<int>(_n);
            _trFirstBadLink = _acc.Allocate1D<int>(_n);
            _trResized = _acc.Allocate1D<int>(h.TraceResized.Length);

            for (int k = 0; k < 14; k++) _sphR[k] = Z(_n);
            _sphA[0] = _acc.Allocate1D<int>(_n);
            _sphA[1] = _acc.Allocate1D<int>(_n);

            _lo = _acc.Allocate1D<int>(3 * _n); _hi = _acc.Allocate1D<int>(3 * _n);
            _counts = _acc.Allocate1D<int>(buckets + 1);
            _start = _acc.Allocate1D<int>(buckets + 1);
            _gcursor = _acc.Allocate1D<int>(buckets + 1);
            _items = _acc.Allocate1D<int>(h.EntryCap);
            _flags = _acc.Allocate1D<int>(4);
            _found = _acc.Allocate1D<int>(_n); _unique = _acc.Allocate1D<int>(_n);
            _nOver = _acc.Allocate1D<int>(_n);
            _over = _acc.Allocate1D<int>((long)h.OverCap * _n);
            _bedGlass = _acc.Allocate1D<int>(_n);
            _cell = Z(1);

            _inst = Z((long)WholeStep.InstStride * instantBlock);
            _streamAmp = R(h.StreamAmp); _streamRate = R(h.StreamRate); _cellAmp = R(h.CellAmp);
            _bedKx = R(Pad(h.BedKx)); _bedKz = R(Pad(h.BedKz)); _bedAmp = R(Pad(h.BedAmp)); _bedPhase = R(Pad(h.BedPhase));
            _stock = R(h.Stock);
            _lowest = I(h.LowestLive);
            _sink = _acc.Allocate1D<int>(2);
            _sink.View.MemSetToZero();

            _cfg = new SCfg
            {
                N = _n, MaxN = h.MaxNeurons, Buckets = buckets, Mask = buckets - 1,
                CandCap = WholeStep.CandCap, OverCap = h.OverCap, EntryCap = h.EntryCap,
                InstBase = 0, HasCurrent = h.HasCurrent ? 1 : 0, Accelerating = h.Accelerating ? 1 : 0,
                Sloped = h.Sloped ? 1 : 0, HasBed = h.HasBed ? 1 : 0, BedModes = h.BedAmp.Length,
                LimitDrive = h.LimitDrive ? 1 : 0, LimitDrag = h.LimitDrag ? 1 : 0,
                UseRestore = h.UseRestore ? 1 : 0, FloorRestores = h.FloorRestores ? 1 : 0,
                CreatureContact = h.CreatureContact ? 1 : 0, HasNutrients = h.HasNutrients ? 1 : 0,
                HasReserve = h.HasReserve ? 1 : 0,
                TraceStep = 0, TraceTime = 0, DtD = h.Dt,

                Dt = (Real)h.Dt, Density = (Real)h.Density, DragCoefficient = (Real)h.DragCoefficient,
                DragK = (Real)h.DragK, TissueExcess = (Real)h.TissueExcess, NeutralVolume = (Real)h.NeutralVolume,
                RestoringFraction = (Real)h.RestoringFraction, RestoringDensity = (Real)h.RestoringDensity,
                TissueDensity = (Real)h.TissueDensity, FluidAccel = (Real)h.FluidAccel, AddedMass = (Real)h.AddedMass,
                Gravity = (Real)h.Gravity, WorldDepth = (Real)h.WorldDepth, Damping = (Real)h.Damping,
                SpinBudget = (Real)h.SpinBudget,

                Omega = (Real)h.Omega, Zeta = (Real)h.Zeta, MaxSep = (Real)h.MaxSep, MaxDv = (Real)h.MaxDv,
                TankRadius = (Real)h.TankRadius, AxisX = (Real)h.AxisX, AxisZ = (Real)h.AxisZ, CellOverride = 0,
                BedRadius = (Real)h.BedRadius, BedDepth = (Real)h.BedDepth, TiltX = (Real)h.TiltX,
                TiltZ = (Real)h.TiltZ, Offset = (Real)h.Offset,

                CurDepth = (Real)h.CurDepth, CurTankR = (Real)h.CurTankR, EddyWeight = (Real)h.EddyWeight,
                Overturning = (Real)h.Overturning, PerSecond = (Real)h.PerSecond, Squared = (Real)h.Squared,
                SigmaSloped = h.SigmaSloped, SigmaFlat = h.SigmaFlat, Speed = h.Speed,

                SWorldDepth = h.SWorldDepth, ChemHalf = h.ChemHalf, EnergyScale = h.EnergyScale,
                FlowScale = h.FlowScale, RateScale = h.RateScale, ConstCE = h.ConstCE, DtF = h.DtF,
                Nx = h.Nx, Ny = h.Ny, Nz = h.Nz, LayerCount = h.LayerCount, RefugeLayers = h.RefugeLayers,
                LayerStride = h.Nx * h.Nz,
                CellMetres = h.CellMetres, FieldTankR = h.FieldTankR, RefugeFraction = h.RefugeFraction,
                CellVolume = h.CellVolume,
            };

            var watch = System.Diagnostics.Stopwatch.StartNew();
            _meanSerial = _acc.LoadAutoGroupedStreamKernel<Index1D, SSph, SGrid, SCfg>(WholeStep.MeanSerial);
            _meanTree = _acc.LoadStreamKernel<SSph, SGrid, SCfg>(WholeStep.MeanTree);
            _scan = _acc.LoadStreamKernel<SGrid, SCfg>(WholeStep.Scan);
            if (groupSize > 0)
            {
                _ranges = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, SSph, SGrid, SCfg>(WholeStep.Ranges, groupSize);
                _scatter = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, SGrid, SCfg>(WholeStep.Scatter, groupSize);
                _step = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur,
                    SBrain, STrace, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step, groupSize);
                _empty = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, SCfg>(WholeStep.Empty, groupSize);
            }
            else
            {
                _ranges = _acc.LoadAutoGroupedStreamKernel<Index1D, SSph, SGrid, SCfg>(WholeStep.Ranges);
                _scatter = _acc.LoadAutoGroupedStreamKernel<Index1D, SGrid, SCfg>(WholeStep.Scatter);
                _step = _acc.LoadAutoGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur,
                    SBrain, STrace, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step);
                _empty = _acc.LoadAutoGroupedStreamKernel<Index1D, SCfg>(WholeStep.Empty);
            }
            _spinKernel = _acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, int>(WholeStep.Spin);
            _acc.Synchronize();
            CompileMs = watch.Elapsed.TotalMilliseconds;

            ResetState();
        }

        private static double[] Pad(double[] a) => a.Length > 0 ? a : new double[1];

        private MemoryBuffer1D<int, Stride1D.Dense> I(int[] a) => _acc.Allocate1D(a.Length > 0 ? a : new int[1]);
        private MemoryBuffer1D<float, Stride1D.Dense> F(float[] a) => _acc.Allocate1D(a.Length > 0 ? a : new float[1]);
        private MemoryBuffer1D<Real, Stride1D.Dense> R(double[] a) => _acc.Allocate1D(Cv(a.Length > 0 ? a : new double[1]));
        private MemoryBuffer1D<Real, Stride1D.Dense> Z(long n) => _acc.Allocate1D<Real>(n > 0 ? n : 1);

        private static Real[] Cv(double[] a)
        {
            var v = new Real[a.Length];
            for (int i = 0; i < a.Length; i++) v[i] = (Real)a[i];
            return v;
        }

        private static double[] Wd(Real[] a)
        {
            var v = new double[a.Length];
            for (int i = 0; i < a.Length; i++) v[i] = a[i];
            return v;
        }

        private STopo Topo => new STopo
        {
            Links = _links.View, Dof = _dof.View, SigLen = _sigLen.View, Mask = _mask.View, Parent = _parent.View,
            DofCount = _dofCount.View, DofStart = _dofStart.View, PanelStart = _panelStart.View,
            NOff = _noff.View, NCnt = _ncnt.View, BParent = _bparent.View, FirstChild = _firstChild.View,
            BDofStart = _bdofStart.View, BDofCount = _bdofCount.View,
        };

        private SConst Const => new SConst
        {
            Mass = _mass.View, Lift = _lift.View, Volume = _volume.View, SmallI = _smallI.View, Reach = _reach.View,
            Inertia = _inertia.View, ChildAnchor = _childAnchor.View, ParentAnchor = _parentAnchor.View,
            JointFrame = _jointFrame.View, RestFrame = _restFrame.View, LimitLo = _limitLo.View,
            LimitHi = _limitHi.View, LimitStiff = _limitStiff.View, LimitDamp = _limitDamp.View,
            Excess = _excess.View, TotalMass = _totalMass.View, TorquePerUnit = _tpu.View, PowerScale = _powerScale.View,
        };

        private SPanels Panels => new SPanels { Centre = _pCentre.View, Normal = _pNormal.View, Area = _pArea.View };

        private SState State => new SState
        {
            BasePos = _basePos.View, BaseRot = _baseRot.View, Q = _q.View, Qd = _qd.View, Tau = _tau.View,
            LimitImplicit = _limitImplicit.View, BallRot = _ballRot.View, Pos = _pos.View, Rot = _rot.View,
            RotM = _rotM.View, Spin = _spin.View, Vel = _vel.View, Sang = _sang.View, Slin = _slin.View,
            Cbias = _cbias.View, RelVel = _relVel.View, Water = _water.View, WaterAcc = _waterAcc.View,
            Fext = _fext.View, Alive = _alive.View,
        };

        private SDrive Drive => new SDrive
        {
            Signal = _signal.View, History = _history.View, RunSum = _runSum.View, Cursor = _cursor.View,
            Filled = _filled.View, DriveLimited = _driveLimited.View, DragLimited = _dragLimited.View,
            Work = _work.View, Signed = _signed.View, Dissipated = _dissipated.View, Passive = _passive.View,
        };

        private SNeur Neur => new SNeur
        {
            Op = _op.View, NIn = _nin.View, Kind = _kind.View, Index = _index.View, Channel = _channel.View,
            Freq = _freq.View, Phase = _phase.View, Amp = _amp.View, Bias = _bias.View,
            Const = _const.View, Weight = _weight.View,
        };

        private SBrain Brain => new SBrain
        {
            S = _s.View, Mem = _mem.View, Reserve = _reserve.View, Damage = _damage.View,
            Parity = _parity.View, Contact = _contact.View, Clock = _clock.View,
        };

        private STrace Trace => new STrace
        {
            Ring = _ring.View, Step = _trStep.View, FirstBadStep = _trFirstBad.View, Time = _trTime.View,
            Enabled = _trEnabled.View, Held = _trHeld.View, Cursor = _trCursor.View,
            FirstBadLink = _trFirstBadLink.View, Resized = _trResized.View,
        };

        private SSph Sph(int set)
        {
            int o = 7 * set;
            return new SSph
            {
                Cx = _sphR[o].View, Cy = _sphR[o + 1].View, Cz = _sphR[o + 2].View, R = _sphR[o + 3].View,
                Vx = _sphR[o + 4].View, Vy = _sphR[o + 5].View, Vz = _sphR[o + 6].View, Active = _sphA[set].View,
            };
        }

        private SGrid Grid => new SGrid
        {
            Lo = _lo.View, Hi = _hi.View, Counts = _counts.View, Start = _start.View, Cursor = _gcursor.View,
            Items = _items.View, Flags = _flags.View, Cell = _cell.View, Found = _found.View, Unique = _unique.View,
            NOver = _nOver.View, Over = _over.View, BedGlass = _bedGlass.View,
        };

        private SWorld World => new SWorld
        {
            Inst = _inst.View, StreamAmp = _streamAmp.View, StreamRate = _streamRate.View, CellAmp = _cellAmp.View,
            BedKx = _bedKx.View, BedKz = _bedKz.View, BedAmp = _bedAmp.View, BedPhase = _bedPhase.View,
            Stock = _stock.View, LowestLive = _lowest.View,
        };

        /// <summary>Everything that changes put back as the host built it.</summary>
        public void ResetState()
        {
            Gpu.Spike4.StepHost h = _h;
            _basePos.View.CopyFromCPU(Cv(h.BasePos)); _baseRot.View.CopyFromCPU(Cv(h.BaseRot));
            _q.View.CopyFromCPU(Cv(h.Q)); _qd.View.CopyFromCPU(Cv(h.Qd)); _tau.View.CopyFromCPU(Cv(h.Tau));
            _limitImplicit.View.CopyFromCPU(Cv(h.LimitImplicit)); _ballRot.View.CopyFromCPU(Cv(h.BallRot));
            _pos.View.CopyFromCPU(Cv(h.Pos)); _rot.View.CopyFromCPU(Cv(h.Rot)); _rotM.View.CopyFromCPU(Cv(h.RotM));
            _spin.View.CopyFromCPU(Cv(h.Spin)); _vel.View.CopyFromCPU(Cv(h.Vel)); _sang.View.CopyFromCPU(Cv(h.Sang));
            _slin.View.CopyFromCPU(Cv(h.Slin)); _cbias.View.CopyFromCPU(Cv(h.Cbias));
            _relVel.View.CopyFromCPU(Cv(h.RelVel)); _water.View.CopyFromCPU(Cv(h.Water));
            _waterAcc.View.CopyFromCPU(Cv(h.WaterAcc)); _fext.View.CopyFromCPU(Cv(h.Fext));
            _alive.View.CopyFromCPU(h.Alive);

            _signal.View.CopyFromCPU(h.Signal); _history.View.CopyFromCPU(h.History); _runSum.View.CopyFromCPU(h.RunSum);
            _cursor.View.CopyFromCPU(h.Cursor); _filled.View.CopyFromCPU(h.Filled);
            _driveLimited.View.CopyFromCPU(h.DriveLimited); _dragLimited.View.CopyFromCPU(h.DragLimited);
            _work.View.CopyFromCPU(h.Work); _signed.View.CopyFromCPU(h.Signed);
            _dissipated.View.CopyFromCPU(h.Dissipated); _passive.View.CopyFromCPU(h.Passive);

            _s.View.CopyFromCPU(h.S0); _mem.View.CopyFromCPU(h.Mem0);
            _parity.View.CopyFromCPU(h.Parity0); _clock.View.CopyFromCPU(h.Clock0);

            _ring.View.CopyFromCPU(Cv(h.Ring)); _trStep.View.CopyFromCPU(h.TraceStep);
            _trFirstBad.View.CopyFromCPU(h.TraceFirstBadStep); _trTime.View.CopyFromCPU(h.TraceTime);
            _trHeld.View.CopyFromCPU(h.TraceHeld); _trCursor.View.CopyFromCPU(h.TraceCursor);
            _trFirstBadLink.View.CopyFromCPU(h.TraceFirstBadLink); _trResized.View.CopyFromCPU(h.TraceResized);

            // Set 0 committed, set 1 pending.
            _committed = 0;
            double[][] c = { h.CCx, h.CCy, h.CCz, h.CR, h.CVx, h.CVy, h.CVz };
            double[][] p = { h.PCx, h.PCy, h.PCz, h.PR, h.PVx, h.PVy, h.PVz };
            for (int k = 0; k < 7; k++)
            {
                _sphR[k].View.CopyFromCPU(Cv(c[k]));
                _sphR[7 + k].View.CopyFromCPU(Cv(p[k]));
            }
            _sphA[0].View.CopyFromCPU(h.CActive);
            _sphA[1].View.CopyFromCPU(h.PActive);

            _flags.View.MemSetToZero();
            _nOver.View.MemSetToZero();
            _bedGlass.View.MemSetToZero();
            _acc.Synchronize();
        }

        /// <summary>A block of instants going up: the per-metabolic-step upload the port pays.</summary>
        public void UploadInstants(double[] block, int count)
        {
            var v = new Real[WholeStep.InstStride * InstantBlock];
            for (int k = 0; k < WholeStep.InstStride * count; k++) v[k] = (Real)block[k];
            _inst.View.CopyFromCPU(v);
        }

        private Real[] _instScratch;

        /// <summary>The same, from a pre-narrowed array, for timing.</summary>
        public void UploadInstantsTimed(double[] block)
        {
            if (_instScratch == null)
            {
                _instScratch = new Real[WholeStep.InstStride * InstantBlock];
                for (int k = 0; k < _instScratch.Length && k < block.Length; k++) _instScratch[k] = (Real)block[k];
            }
            _inst.View.CopyFromCPU(_instScratch);
        }

        public long InstantBytes => (long)WholeStep.InstStride * InstantBlock * 4;

        /// <summary>The whole step: the grid over the committed spheres, the body phase, the swap.</summary>
        public void Step(bool serialMean, int instantIndex, long traceStep, double traceTime)
        {
            SCfg cfg = _cfg;
            cfg.InstBase = WholeStep.InstStride * instantIndex;
            cfg.TraceStep = traceStep;
            cfg.TraceTime = traceTime;

            SSph committed = Sph(_committed), pending = Sph(1 - _committed);
            SGrid grid = Grid;

            _counts.View.MemSetToZero();
            if (serialMean) _meanSerial(1, committed, grid, cfg);
            else _meanTree(new KernelConfig(1, _reduce), committed, grid, cfg);
            _ranges(_n, committed, grid, cfg);
            _scan(new KernelConfig(1, _reduce), grid, cfg);
            _scatter(_n, grid, cfg);

            _step(_n, Topo, Const, Panels, State, Drive, Neur, Brain, Trace, committed, pending, grid, World, cfg);

            _committed = 1 - _committed;
        }

        /// <summary>The body kernel alone, over the grid already built; the swap is not taken.</summary>
        public void BodyOnly(int instantIndex, long traceStep, double traceTime)
        {
            SCfg cfg = _cfg;
            cfg.InstBase = WholeStep.InstStride * instantIndex;
            cfg.TraceStep = traceStep;
            cfg.TraceTime = traceTime;
            _step(_n, Topo, Const, Panels, State, Drive, Neur, Brain, Trace, Sph(_committed), Sph(1 - _committed),
                  Grid, World, cfg);
        }

        public void GridOnly(bool serialMean)
        {
            SCfg cfg = _cfg;
            SSph committed = Sph(_committed);
            SGrid grid = Grid;
            _counts.View.MemSetToZero();
            if (serialMean) _meanSerial(1, committed, grid, cfg);
            else _meanTree(new KernelConfig(1, _reduce), committed, grid, cfg);
            _ranges(_n, committed, grid, cfg);
            _scan(new KernelConfig(1, _reduce), grid, cfg);
            _scatter(_n, grid, cfg);
        }

        public void EmptyLaunch() => _empty(_n, _cfg);

        public void Swap() => _committed = 1 - _committed;

        public void Spin(int iterations) => _spinKernel(1, _sink.View, iterations);

        public void Synchronize() => _acc.Synchronize();

        public void ClearFlags() => _flags.View.MemSetToZero();

        // ---- the per-metabolic-block readback the port pays: poses, the ledger, the limiter
        // counts, the lost flags, the overlap lists and counts, the committed spheres.
        private Real[] _rbPos, _rbRot, _rbSph;
        private double[] _rbWork, _rbSigned, _rbDiss, _rbPass;
        private long[] _rbDrive, _rbDrag;
        private int[] _rbAlive, _rbOver, _rbNOver, _rbBed, _rbActive;

        public long ReadMetabolic()
        {
            if (_rbPos == null)
            {
                _rbPos = new Real[_pos.Length]; _rbRot = new Real[_rot.Length]; _rbSph = new Real[_n];
                _rbWork = new double[_n]; _rbSigned = new double[_n]; _rbDiss = new double[_n]; _rbPass = new double[_n];
                _rbDrive = new long[_n]; _rbDrag = new long[_n];
                _rbAlive = new int[_n]; _rbOver = new int[_over.Length]; _rbNOver = new int[_n];
                _rbBed = new int[_n]; _rbActive = new int[_n];
            }
            _pos.View.CopyToCPU(_rbPos); _rot.View.CopyToCPU(_rbRot);
            _work.View.CopyToCPU(_rbWork); _signed.View.CopyToCPU(_rbSigned);
            _dissipated.View.CopyToCPU(_rbDiss); _passive.View.CopyToCPU(_rbPass);
            _driveLimited.View.CopyToCPU(_rbDrive); _dragLimited.View.CopyToCPU(_rbDrag);
            _alive.View.CopyToCPU(_rbAlive); _over.View.CopyToCPU(_rbOver); _nOver.View.CopyToCPU(_rbNOver);
            _bedGlass.View.CopyToCPU(_rbBed);
            int o = 7 * _committed;
            for (int k = 0; k < 7; k++) _sphR[o + k].View.CopyToCPU(_rbSph);
            _sphA[_committed].View.CopyToCPU(_rbActive);

            return (long)(_pos.Length + _rot.Length + 7L * _n) * 4 + 4L * 8 * _n + 2L * 8 * _n +
                   4L * _n * 4 + (long)_over.Length * 4;
        }

        public Gpu.Spike4.StepResult Read()
        {
            var r = new Gpu.Spike4.StepResult
            {
                BasePos = Wd(_basePos.GetAsArray1D()), BaseRot = Wd(_baseRot.GetAsArray1D()),
                Q = Wd(_q.GetAsArray1D()), Qd = Wd(_qd.GetAsArray1D()), Tau = Wd(_tau.GetAsArray1D()),
                LimitImplicit = Wd(_limitImplicit.GetAsArray1D()), BallRot = Wd(_ballRot.GetAsArray1D()),
                Pos = Wd(_pos.GetAsArray1D()), Rot = Wd(_rot.GetAsArray1D()), RotM = Wd(_rotM.GetAsArray1D()),
                Spin = Wd(_spin.GetAsArray1D()), Vel = Wd(_vel.GetAsArray1D()), Sang = Wd(_sang.GetAsArray1D()),
                Slin = Wd(_slin.GetAsArray1D()), Cbias = Wd(_cbias.GetAsArray1D()),
                RelVel = Wd(_relVel.GetAsArray1D()), Water = Wd(_water.GetAsArray1D()),
                WaterAcc = Wd(_waterAcc.GetAsArray1D()), Fext = Wd(_fext.GetAsArray1D()),
                Alive = _alive.GetAsArray1D(),
                Signal = _signal.GetAsArray1D(), History = _history.GetAsArray1D(), RunSum = _runSum.GetAsArray1D(),
                Cursor = _cursor.GetAsArray1D(), Filled = _filled.GetAsArray1D(),
                DriveLimited = _driveLimited.GetAsArray1D(), DragLimited = _dragLimited.GetAsArray1D(),
                Work = _work.GetAsArray1D(), Signed = _signed.GetAsArray1D(),
                Dissipated = _dissipated.GetAsArray1D(), Passive = _passive.GetAsArray1D(),
                S = _s.GetAsArray1D(), Mem = _mem.GetAsArray1D(), Parity = _parity.GetAsArray1D(),
                Clock = _clock.GetAsArray1D(),
                Ring = Wd(_ring.GetAsArray1D()), TraceStep = _trStep.GetAsArray1D(), TraceTime = _trTime.GetAsArray1D(),
                TraceHeld = _trHeld.GetAsArray1D(), TraceCursor = _trCursor.GetAsArray1D(),
                TraceFirstBadStep = _trFirstBad.GetAsArray1D(), TraceFirstBadLink = _trFirstBadLink.GetAsArray1D(),
                TraceResized = _trResized.GetAsArray1D(),
                Found = _found.GetAsArray1D(), Unique = _unique.GetAsArray1D(), NOver = _nOver.GetAsArray1D(),
                Over = _over.GetAsArray1D(), BedGlass = _bedGlass.GetAsArray1D(),
                Cell = _cell.GetAsArray1D()[0], Entries = _start.GetAsArray1D()[_buckets],
                EntryOverflow = _flags.GetAsArray1D()[0],
            };

            // After the swap the committed set is the one this step wrote as pending.
            int c = 7 * _committed, p = 7 * (1 - _committed);
            r.CCx = Wd(_sphR[c].GetAsArray1D()); r.CCy = Wd(_sphR[c + 1].GetAsArray1D()); r.CCz = Wd(_sphR[c + 2].GetAsArray1D());
            r.CR = Wd(_sphR[c + 3].GetAsArray1D());
            r.CVx = Wd(_sphR[c + 4].GetAsArray1D()); r.CVy = Wd(_sphR[c + 5].GetAsArray1D()); r.CVz = Wd(_sphR[c + 6].GetAsArray1D());
            r.CActive = _sphA[_committed].GetAsArray1D();
            r.PCx = Wd(_sphR[p].GetAsArray1D()); r.PCy = Wd(_sphR[p + 1].GetAsArray1D()); r.PCz = Wd(_sphR[p + 2].GetAsArray1D());
            r.PR = Wd(_sphR[p + 3].GetAsArray1D());
            r.PVx = Wd(_sphR[p + 4].GetAsArray1D()); r.PVy = Wd(_sphR[p + 5].GetAsArray1D()); r.PVz = Wd(_sphR[p + 6].GetAsArray1D());
            r.PActive = _sphA[1 - _committed].GetAsArray1D();
            return r;
        }

        /// <summary>
        /// FNV-1a over the raw bits of every state buffer the next step reads and every output the
        /// step writes: the card's identity with itself.
        /// </summary>
        public ulong Digest()
        {
            ulong hash = 14695981039346656037UL;
            foreach (var b in new[] { _basePos, _baseRot, _q, _qd, _tau, _limitImplicit, _ballRot, _pos, _rot, _rotM,
                _spin, _vel, _sang, _slin, _cbias, _relVel, _water, _waterAcc, _fext, _ring })
            {
                foreach (Real v in b.GetAsArray1D()) BitsR(ref hash, v);
            }
            for (int k = 0; k < 7; k++) foreach (Real v in _sphR[7 * _committed + k].GetAsArray1D()) BitsR(ref hash, v);
            foreach (var b in new[] { _signal, _history, _runSum, _s, _mem })
            {
                foreach (float v in b.GetAsArray1D()) Mix(ref hash, (uint)BitConverter.SingleToInt32Bits(v));
            }
            foreach (var b in new[] { _work, _signed, _dissipated, _passive, _clock, _trTime })
            {
                foreach (double v in b.GetAsArray1D()) Mix64(ref hash, (ulong)BitConverter.DoubleToInt64Bits(v));
            }
            foreach (var b in new[] { _alive, _cursor, _filled, _parity, _trHeld, _trCursor, _nOver, _bedGlass, _found })
            {
                foreach (int v in b.GetAsArray1D()) Mix(ref hash, (uint)v);
            }
            foreach (var b in new[] { _driveLimited, _dragLimited, _trStep })
            {
                foreach (long v in b.GetAsArray1D()) Mix64(ref hash, (ulong)v);
            }
            int[] nOver = _nOver.GetAsArray1D(), over = _over.GetAsArray1D();
            for (int i = 0; i < _n; i++)
                for (int k = 0; k < nOver[i] && k < _h.OverCap; k++) Mix(ref hash, (uint)over[k * _n + i]);
            return hash;
        }

        private static void BitsR(ref ulong hash, Real v)
        {
            byte[] raw = BitConverter.GetBytes(v);
            for (int b = 0; b < raw.Length; b++) { hash ^= raw[b]; hash *= 1099511628211UL; }
        }

        private static void Mix(ref ulong hash, uint v)
        {
            for (int b = 0; b < 4; b++) { hash ^= (v >> (8 * b)) & 0xFF; hash *= 1099511628211UL; }
        }

        private static void Mix64(ref ulong hash, ulong v)
        {
            for (int b = 0; b < 8; b++) { hash ^= (v >> (8 * b)) & 0xFF; hash *= 1099511628211UL; }
        }

        public void Dispose()
        {
            var all = new System.Collections.Generic.List<MemoryBuffer>
            {
                _links, _dof, _sigLen, _mask, _parent, _dofCount, _dofStart, _panelStart, _noff, _ncnt, _bparent,
                _firstChild, _bdofStart, _bdofCount, _mass, _lift, _volume, _smallI, _reach, _inertia, _childAnchor,
                _parentAnchor, _jointFrame, _restFrame, _limitLo, _limitHi, _limitStiff, _limitDamp, _excess,
                _totalMass, _tpu, _powerScale, _pCentre, _pNormal, _pArea, _basePos, _baseRot, _q, _qd, _tau,
                _limitImplicit, _ballRot, _pos, _rot, _rotM, _spin, _vel, _sang, _slin, _cbias, _relVel, _water,
                _waterAcc, _fext, _alive, _signal, _history, _runSum, _cursor, _filled, _driveLimited, _dragLimited,
                _work, _signed, _dissipated, _passive, _op, _nin, _kind, _index, _channel, _freq, _phase, _amp,
                _bias, _const, _weight, _s, _mem, _reserve, _damage, _parity, _contact, _clock, _ring, _trStep,
                _trFirstBad, _trTime, _trEnabled, _trHeld, _trCursor, _trFirstBadLink, _trResized, _lo, _hi,
                _counts, _start, _gcursor, _items, _flags, _found, _unique, _nOver, _over, _bedGlass, _cell, _inst,
                _streamAmp, _streamRate, _cellAmp, _bedKx, _bedKz, _bedAmp, _bedPhase, _stock, _lowest, _sink,
                _sphA[0], _sphA[1],
            };
            foreach (var b in _sphR) all.Add(b);
            foreach (var b in all) b.Dispose();
        }
    }
}

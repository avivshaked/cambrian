// ---------------------------------------------------------------------------------------------
// GENERATED from src/Evosim.Farm.Gpu/gpu-kernel.template by KernelGenerator. Do not edit: edit
// the template and regenerate (Evosim.Farm.Tests, GpuKernelSourceTests, EVOSIM_GPU_REGENERATE=1).
//
// The whole physics step of Evosim.Dynamics (DynamicsWorld.StepOnCpu, one body a thread) as
// ILGPU kernels, in double. Carried over from spike 4 (spikes/02-gpu-featherstone/spike4,
// logbook/0115), which held the body phase exact against the solver, and caught up with the
// solver at d995433 (scratch/gpu-port/inventory.txt): per-part contact (D114), the reefs' rock
// and their fade on the streams, the beach's clamp and its fade, D111's buoyancy torque, the
// drive's applied torque, the trace's resize flag and its poison, the senses' per-body gates,
// the reef-split columns of the chemical sense, candidates ordered by list rank, slots that are
// reused, and four size classes.
//
// Per step, in the CPU's order:
//   the grid       MeanSerial | MeanChunks + MeanCombine, Ranges, Scan, Scatter   (Contacts.cs)
//   the bodies     Step0 .. Step3, one launch per class                          (StepOne)
//   the census     Census                                               (CloseContactStep)
//   the commit     a swap of the two sphere sets, on the host
//
// What is not here, and why:
//   - D100's water hold, the rolls, the box's transport field and the vent: refused by the
//     backend at construction (gpu-port-spec.md section 3).
//   - The contact events list and the digest row: built on the host from what the census and
//     the readback leave, after the block.
// ---------------------------------------------------------------------------------------------

using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Algorithms;

namespace Evosim.Farm.Gpu.Dbl
{
    using Real = System.Double;

    /// <summary>A class's integers: per body, then per link, strided by the class's capacity.</summary>
    public struct STopo
    {
        public ArrayView<int> Links, Dof, SigLen, Mask, GSlot;
        public ArrayView<int> Parent, DofCount, DofStart, PanelStart, ThinAxis;
        public ArrayView<int> NOff, NCnt, BParent, FirstChild, BDofStart, BDofCount;
    }

    /// <summary>Everything about a class's body that does not change inside a physics step.</summary>
    public struct SConst
    {
        public ArrayView<Real> Mass, Lift, Volume, SmallI, Reach, Arm, Inertia, ChildAnchor, ParentAnchor;
        public ArrayView<Real> JointFrame, RestFrame, LimitLo, LimitHi, LimitStiff, LimitDamp;
        public ArrayView<Real> Excess, TotalMass;
        public ArrayView<float> TorquePerUnit, PowerScale;
    }

    public struct SPanels { public ArrayView<Real> Centre, Normal, Area; }

    /// <summary>A class's body state: what the next step reads, and the step's outputs beside it.</summary>
    public struct SState
    {
        public ArrayView<Real> BasePos, BaseRot, Q, Qd, Tau, LimitImplicit, BallRot;
        public ArrayView<Real> Pos, Rot, RotM, Spin, Vel, Sang, Slin, Cbias, RelVel, Water, WaterAcc, Fext, Applied;
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

    /// <summary>The brain's buffers and the senses' hand-back; Gates bit 0 nutrients, bit 1 reserve.</summary>
    public struct SBrain
    {
        public ArrayView<float> S, Mem, Reserve, Damage;
        public ArrayView<int> Parity, Contact, Gates;
        public ArrayView<double> Clock;
    }

    public struct STrace
    {
        public ArrayView<Real> Ring;
        public ArrayView<long> Step, FirstBadStep;
        public ArrayView<double> Time;
        public ArrayView<int> Enabled, Held, Cursor, FirstBadLink, Resized, Poison, Pending;
    }

    /// <summary>
    /// What every body reads of every other, by global slot: the roster, the masses the contact
    /// law divides, the overlap lists and the held lists, and the census's counters.
    /// </summary>
    public struct SGlob
    {
        public ArrayView<int> Links, Id, Rank, Alive, Jointed, RankToSlot;
        public ArrayView<Real> TotalMass, LinkMass;
        public ArrayView<int> NOver, OvSlot, OvId, OvPart, OvHeld, NHeld, HeldId, BedGlass;
        public ArrayView<long> Census, Overflow;
    }

    /// <summary>One set of bounding spheres, the body's and (D114) every link's: committed or pending.</summary>
    public struct SSph
    {
        public ArrayView<Real> Cx, Cy, Cz, R, Vx, Vy, Vz;
        public ArrayView<Real> LCx, LCy, LCz, LR, LVx, LVy, LVz;
        public ArrayView<int> Active;
    }

    /// <summary>The contact grid over rows: a body, or under per-part contact a (body, link).</summary>
    public struct SGrid
    {
        public ArrayView<int> Lo, Hi, Counts, Start, Cursor, Items, PartialN;
        public ArrayView<Real> Cell, Partial;
    }

    /// <summary>The world a body reads: the instants, the world's constants, the snow.</summary>
    public struct SWorld
    {
        public ArrayView<Real> Inst, WR, Stock;
        public ArrayView<int> WI;
    }

    public struct SCfg
    {
        // The launch: the class's capacity and neuron ceiling, the global roster, the grid.
        public int N, MaxN, GN, GUsed, Count, Rows, Buckets, Mask, OverCap, EntryCap;
        public int InstBase, StepIndex, PerPart, Instrument, Chunk, Chunks;

        public int HasCurrent, Accelerating, Sloped, HasBed, BedRelief, BedModes, ShoreOn, FadeOn;
        public int ReefCount, LimitDrive, LimitDrag, UseRestore, FloorRestores, CreatureContact;
        public int OffsetTorque, SplitColumns;

        public long TraceStep;
        public double TraceTime, DtD;

        public Real Dt, Density, DragK, AddedMass, FluidAccel, Gravity, WorldDepth, Damping, SpinBudget;
        public Real RestoringDensity, TissueDensity;

        public Real Omega, Zeta, MaxSep, MaxDv, TankRadius, AxisX, AxisZ, CellOverride;
        public Real BedRadius, BedDepth, TiltX, TiltZ, Offset, ShoalHeight;

        public Real CurDepth, CurTankR, EddyWeight, Overturning, PerSecond, Squared, ShoreDepth, ShoreFadeW;
        public Real HalfThickness, Fillet, ReefFade;
        public float SigmaSloped, SigmaFlat, Speed;

        public float SWorldDepth, ChemHalf, EnergyScale, FlowScale, RateScale, ConstCE, DtF;
        public int Nx, Ny, Nz, LayerCount, RefugeLayers, LayerStride;
        public float CellMetres, FieldTankR, RefugeFraction, CellVolume;

        // Offsets into SWorld.WR and SWorld.WI.
        public int StreamAmpAt, StreamRateAt, CellAmpAt, BedAt, ReefAt;
        public int LowestAt, IntervalFirstAt, IntervalTopAt, IntervalBottomAt;
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
        public Real Magnitude => System.Math.Sqrt(X * X + Y * Y + Z * Z);

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
            Real s = System.Math.Sin(half);
            return new Q4(n.X * s, n.Y * s, n.Z * s, System.Math.Cos(half));
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
                Real m = System.Math.Sqrt(SqrMagnitude);
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

            Real angle = (Real)2.0 * System.Math.Atan2(length, w);
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

    /// <summary>ReefGeometry.Distance: a signed distance or a fade, its gradient and its Hessian.</summary>
    public struct SD
    {
        public Real S, Gx, Gy, Gz, Hxx, Hxy, Hxz, Hyy, Hyz, Hzz;
    }

    /// <summary>CurrentField.BedMap: the floor-following map at one point.</summary>
    public struct SMap
    {
        public Real Y, MappedY, C, A, B, Height, SlopeX, SlopeZ, Depth, Fade;
    }

    public static partial class WholeStep
    {
        public const int LinkStride = 16;          // DevelopmentLimits.MaxParts: the global per-link stride
        public const int CandCap = 256;
        public const int Window = 10;              // EffectorDrive.SmoothWindow
        public const int TraceFrames = 3;          // Creature.TraceFrames
        public const int TraceValues = 21;         // Creature.TraceValuesPerLink
        public const int ReefStride = 16;          // values a reef takes in SWorld.WR
        public const int CensusFields = 5;         // pairs, jointed, held, bodies, bed or glass

        // Overflow counters: candidates past CandCap, overlaps past OverCap, held ids past OverCap,
        // cell entries past EntryCap.
        public const int OverflowCandidates = 0, OverflowOverlaps = 1, OverflowHeld = 2, OverflowEntries = 3;

        // The instant's layout: the samplers' phase (A) then the analytic acceleration's (B).
        public const int Terms = 24, Cells = 3;
        public const int ACos = 0, ASin = 24, AEnv = 48, ACellEnv = 72, ACell = 75;
        public const int BCos = 78, BSin = 102, BEnv = 126, BEnvRate = 150, BCellEnv = 174,
            BCellEnvRate = 177, BCell = 180, BCellRate = 183;
        public const int InstStride = 186;

        private const int SDepth = 9, SUp = 3, SJointAngle = 0, SJointRate = 1, SChemical = 6, SEnergy = 7,
            SFlow = 8, SContact = 2, SDamage = 5;

        // ============================================================== the grid

        /// <summary>The mean radius in the list's order, on one thread (Contacts.cs Build, BuildLinks).</summary>
        public static void MeanSerial(Index1D index, SSph s, SGlob gl, SGrid gr, SCfg cfg)
        {
            if (index.X != 0) return;

            Real sum = 0;
            int active = 0;

            for (int r = 0; r < cfg.Count; r++)
            {
                int g = gl.RankToSlot[r];
                if (g < 0 || s.Active[g] == 0) continue;   // -1: refused for its class, never on the card

                if (cfg.PerPart != 0)
                {
                    int n = gl.Links[g];
                    for (int l = 0; l < n; l++)
                    {
                        sum += s.LR[g * LinkStride + l];
                        active++;
                    }
                }
                else
                {
                    sum += s.R[g];
                    active++;
                }
            }

            gr.Cell[0] = CellRule(sum, active, cfg);
        }

        /// <summary>
        /// The same sum in fixed chunks of the list, one chunk a thread: deterministic at any group
        /// size, and not the CPU's rounding (a cell an ulp away changes a pair only when two
        /// spheres touch at a rounding edge).
        /// </summary>
        public static void MeanChunks(Index1D index, SSph s, SGlob gl, SGrid gr, SCfg cfg)
        {
            int c = index.X;
            if (c >= cfg.Chunks) return;

            int from = c * cfg.Chunk;
            int to = from + cfg.Chunk;
            if (to > cfg.Count) to = cfg.Count;

            Real sum = 0;
            int active = 0;

            for (int r = from; r < to; r++)
            {
                int g = gl.RankToSlot[r];
                if (g < 0 || s.Active[g] == 0) continue;   // -1: refused for its class, never on the card

                if (cfg.PerPart != 0)
                {
                    int n = gl.Links[g];
                    for (int l = 0; l < n; l++)
                    {
                        sum += s.LR[g * LinkStride + l];
                        active++;
                    }
                }
                else
                {
                    sum += s.R[g];
                    active++;
                }
            }

            gr.Partial[c] = sum;
            gr.PartialN[c] = active;
        }

        public static void MeanCombine(Index1D index, SGrid gr, SCfg cfg)
        {
            if (index.X != 0) return;

            Real sum = 0;
            int active = 0;
            for (int c = 0; c < cfg.Chunks; c++)
            {
                sum += gr.Partial[c];
                active += gr.PartialN[c];
            }

            gr.Cell[0] = CellRule(sum, active, cfg);
        }

        private static Real CellRule(Real sum, int active, SCfg cfg)
        {
            Real mean = active > 0 ? sum / active : 0;
            Real rule = mean > (Real)0.125 ? (Real)2.0 * mean : (Real)0.25;
            return cfg.CellOverride > 0 ? cfg.CellOverride : rule;
        }

        /// <summary>The cells each committed row covers, and the histogram (Contacts.cs Build, Fill).</summary>
        public static void Ranges(Index1D index, SSph s, SGlob gl, SGrid gr, SCfg cfg)
        {
            int row = index.X;
            int R = cfg.Rows;
            if (row >= R) return;

            int g = cfg.PerPart != 0 ? row / LinkStride : row;
            int l = cfg.PerPart != 0 ? row - g * LinkStride : 0;

            if (g >= cfg.GUsed || gl.Links[g] <= l || s.Active[g] == 0)
            {
                gr.Lo[row] = 1; gr.Hi[row] = 0;
                gr.Lo[R + row] = 1; gr.Hi[R + row] = 0;
                gr.Lo[2 * R + row] = 1; gr.Hi[2 * R + row] = 0;
                return;
            }

            Real cell = gr.Cell[0];
            Real r, cx, cy, cz;

            if (cfg.PerPart != 0)
            {
                r = s.LR[row];
                cx = s.LCx[row]; cy = s.LCy[row]; cz = s.LCz[row];
            }
            else
            {
                r = s.R[g];
                cx = s.Cx[g]; cy = s.Cy[g]; cz = s.Cz[g];
            }

            int lx = Floor((cx - r) / cell), hx = Floor((cx + r) / cell);
            int ly = Floor((cy - r) / cell), hy = Floor((cy + r) / cell);
            int lz = Floor((cz - r) / cell), hz = Floor((cz + r) / cell);

            gr.Lo[row] = lx; gr.Hi[row] = hx;
            gr.Lo[R + row] = ly; gr.Hi[R + row] = hy;
            gr.Lo[2 * R + row] = lz; gr.Hi[2 * R + row] = hz;

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                Atomic.Add(ref gr.Counts[Hash(x, y, z) & cfg.Mask], 1);
            }
        }

        /// <summary>The exclusive prefix sum of the bucket counts, on one group.</summary>
        public static void Scan(SGrid gr, SCfg cfg)
        {
            var sh = SharedMemory.Allocate<int>(1024);
            int t = Group.IdxX;
            int G = Group.DimX;
            int B = cfg.Buckets;

            int chunk = (B + G - 1) / G;
            int begin = t * chunk;
            int end = begin + chunk;
            if (end > B) end = B;
            if (begin > B) begin = B;

            int local = 0;
            for (int b = begin; b < end; b++) local += gr.Counts[b];
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
                int n = gr.Counts[b];
                gr.Start[b] = running;
                gr.Cursor[b] = running;
                running += n;
            }

            if (t == G - 1) gr.Start[B] = sh[G - 1];
        }

        /// <summary>Every row into every bucket it covers. Order within a bucket is a race; the query sorts.</summary>
        public static void Scatter(Index1D index, SGlob gl, SGrid gr, SCfg cfg)
        {
            int row = index.X;
            int R = cfg.Rows;
            if (row >= R) return;

            int lx = gr.Lo[row], hx = gr.Hi[row];
            int ly = gr.Lo[R + row], hy = gr.Hi[R + row];
            int lz = gr.Lo[2 * R + row], hz = gr.Hi[2 * R + row];

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                int slot = Atomic.Add(ref gr.Cursor[Hash(x, y, z) & cfg.Mask], 1);
                if (slot < cfg.EntryCap) gr.Items[slot] = row;
                else Atomic.Add(ref gl.Overflow[OverflowEntries], 1L);
            }
        }

        // ============================================================== the census

        /// <summary>
        /// DynamicsWorld.CloseContactStep, a body a thread: the five counts from the step's
        /// overlap lists, each pair from its lower id, the held flag of every entry, and then
        /// CommitOverlaps for every body in the roster. Integer sums, so the order of the atomics
        /// changes nothing.
        /// </summary>
        public static void Census(Index1D index, SGlob gl, SCfg cfg)
        {
            int g = index.X;
            if (g >= cfg.GUsed) return;
            if (gl.Links[g] == 0) return;

            int GN = cfg.GN;
            int n = gl.NOver[g];
            if (n > cfg.OverCap) n = cfg.OverCap;

            if (gl.Alive[g] != 0)
            {
                int at = cfg.StepIndex * CensusFields;
                if (gl.BedGlass[g] != 0) Atomic.Add(ref gl.Census[at + 4], 1L);

                int me = gl.Id[g];
                int nHeld = gl.NHeld[g];
                if (nHeld > cfg.OverCap) nHeld = cfg.OverCap;

                bool touching = false;
                long pairs = 0, jointed = 0, held = 0;

                for (int k = 0; k < n; k++)
                {
                    int other = gl.OvSlot[k * GN + g];
                    int id = gl.OvId[k * GN + g];

                    bool present = other >= 0 && gl.Links[other] != 0 && gl.Id[other] == id && gl.Alive[other] != 0;

                    bool wasHeld = false;
                    for (int h = 0; h < nHeld; h++)
                    {
                        if (gl.HeldId[h * GN + g] == id) { wasHeld = true; break; }
                    }

                    gl.OvHeld[k * GN + g] = wasHeld ? 1 : 0;

                    if (!present) continue;

                    touching = true;
                    if (id <= me) continue;

                    pairs++;
                    if (gl.Jointed[g] != 0 || gl.Jointed[other] != 0) jointed++;
                    if (wasHeld) held++;
                }

                if (pairs != 0) Atomic.Add(ref gl.Census[at], pairs);
                if (jointed != 0) Atomic.Add(ref gl.Census[at + 1], jointed);
                if (held != 0) Atomic.Add(ref gl.Census[at + 2], held);
                if (touching) Atomic.Add(ref gl.Census[at + 3], 1L);
            }

            // CommitOverlaps: every body in the list, living or lost.
            for (int k = 0; k < n; k++) gl.HeldId[k * GN + g] = gl.OvId[k * GN + g];
            gl.NHeld[g] = n;
        }

        public static void Empty(Index1D index, SCfg cfg) { }

        // ============================================================== the body phase, class 0

        public static void Step0(
            Index1D index, STopo t, SConst b, SPanels pan, SState s, SDrive dr, SNeur nr, SBrain br,
            STrace tr, SGlob gl, SSph cs, SSph ps, SGrid gr, SWorld w, SCfg cfg)
        {
            const int MaxLinks = 2;
            const int MaxDof = 6;

            int i = index.X;
            int N = cfg.N;
            if (i >= N) return;

            int g = t.GSlot[i];
            if (g < 0) return;

            int links = t.Links[i];

            // DynamicsWorld.StepOne, `if (!body.Alive) return;`. The commit copies the pending
            // spheres, which a lost body stopped refreshing; with the commit a swap of two sets,
            // the pending set is handed the committed values so that the swap is the copy.
            if (gl.Alive[g] == 0)
            {
                KeepSpheres(g, links, cs, ps, cfg);
                return;
            }

            int dof = t.Dof[i];

            // ---- the thread's own copy of the body -------------------------------------------
            var parent = new int[MaxLinks];
            var dofCount = new int[MaxLinks];
            var dofStart = new int[MaxLinks];
            var panelStart = new int[MaxLinks + 1];
            var thinAxis = new int[MaxLinks];

            var mass = new Real[MaxLinks];
            var lift = new Real[MaxLinks];
            var volume = new Real[MaxLinks];
            var smallI = new Real[MaxLinks];
            var reach = new Real[MaxLinks];
            var arm = new Real[MaxLinks];
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
                driveTq[3 * k] = 0; driveTq[3 * k + 1] = 0; driveTq[3 * k + 2] = 0;
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
                thinAxis[l] = t.ThinAxis[at];

                mass[l] = b.Mass[at];
                lift[l] = b.Lift[at];
                volume[l] = b.Volume[at];
                smallI[l] = b.SmallI[at];
                reach[l] = b.Reach[at];
                arm[l] = b.Arm[at];

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
                    water[3 * l + k] = s.Water[a3];
                    wacc[3 * l + k] = s.WaterAcc[a3];
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
            // Still water leaves both arrays as they stood, and a current with no acceleration
            // term leaves the acceleration as it stood: the CPU writes neither.
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

            // ---- 2. senses, 3. brain ----------------------------------------------------------
            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << SDepth)) != 0;
            bool readsUp = (mask & (1 << SUp)) != 0;
            bool readsJoint = (mask & (1 << SJointAngle)) != 0 || (mask & (1 << SJointRate)) != 0;
            bool readsFlow = (mask & (1 << SFlow)) != 0;
            bool readsChemical = (mask & (1 << SChemical)) != 0;
            bool readsEnergy = (mask & (1 << SEnergy)) != 0;

            int gates = br.Gates[i];
            bool hasNutrients = (gates & 1) != 0;
            bool hasReserve = (gates & 2) != 0;

            float energy = 0f;
            Sample(i, links, dofCount, dofStart, position, rotationMatrix, relVel, q, qd, limitHi,
                   br, w, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical && hasNutrients,
                   readsEnergy && hasReserve, sDepth, sUp, sChem, sFlow, sAngle, sRate, ref energy);

            // The brain reads the two gates itself (SensorRead): a body with no field or no
            // reserve source reads the standing-in constant on those channels.
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

                    // EffectorDrive.AppliedTorque, the checkpoint's and the dump's.
                    s.Applied[(3 * bl) * N + i] = world.X;
                    s.Applied[(3 * bl + 1) * N + i] = world.Y;
                    s.Applied[(3 * bl + 2) * N + i] = world.Z;

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
            long dragLimited = FluidApply(links, cfg, excess, mass, lift, volume, smallI, arm, thinAxis,
                position, rotationMatrix, spin, velocity, water, wacc, relVel, panelStart, pan, fext,
                dragF, dragT, preVel, preSpin, i, N);
            if (dragLimited != 0) dr.DragLimited[i] = dr.DragLimited[i] + dragLimited;

            // ---- 6. contacts (Contacts.Apply) ------------------------------------------------
            if (cfg.PerPart != 0)
            {
                ContactsPerPart(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }
            else
            {
                ContactsBody(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }

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
                RecordTraceFrame(i, links, MaxLinks, dofCount, dofStart, position, velocity, spin, qd,
                                 fext, water, tr, cfg);
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
                // Alive = false; MarkLost(): the pending spheres keep the centres and velocities
                // they were committed with, and lose their radii and their place in every set.
                gl.Alive[g] = 0;
                KeepSpheres(g, links, cs, ps, cfg);
                ps.R[g] = 0;
                ps.Active[g] = 0;

                if (cfg.PerPart != 0)
                {
                    for (int l = 0; l < links; l++) ps.LR[g * LinkStride + l] = 0;
                }
            }
            else
            {
                // ---- 15. the contact spheres, pending (RefreshContactSphere) ------------------
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
                ps.Cx[g] = centre.X; ps.Cy[g] = centre.Y; ps.Cz[g] = centre.Z;
                ps.R[g] = radius;
                ps.Vx[g] = mean.X; ps.Vy[g] = mean.Y; ps.Vz[g] = mean.Z;
                ps.Active[g] = 1;

                // RefreshLinkSpheres (D114): a one-link body's link sphere is its body sphere.
                if (cfg.PerPart != 0)
                {
                    int row = g * LinkStride;

                    if (links == 1)
                    {
                        ps.LCx[row] = centre.X; ps.LCy[row] = centre.Y; ps.LCz[row] = centre.Z;
                        ps.LR[row] = radius;
                        ps.LVx[row] = mean.X; ps.LVy[row] = mean.Y; ps.LVz[row] = mean.Z;
                    }
                    else
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LCx[row + l] = position[3 * l];
                            ps.LCy[row + l] = position[3 * l + 1];
                            ps.LCz[row + l] = position[3 * l + 2];
                            ps.LR[row + l] = reach[l];
                            ps.LVx[row + l] = velocity[3 * l];
                            ps.LVy[row + l] = velocity[3 * l + 1];
                            ps.LVz[row + l] = velocity[3 * l + 2];
                        }
                    }
                }
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
        // ============================================================== the body phase, class 1

        public static void Step1(
            Index1D index, STopo t, SConst b, SPanels pan, SState s, SDrive dr, SNeur nr, SBrain br,
            STrace tr, SGlob gl, SSph cs, SSph ps, SGrid gr, SWorld w, SCfg cfg)
        {
            const int MaxLinks = 4;
            const int MaxDof = 12;

            int i = index.X;
            int N = cfg.N;
            if (i >= N) return;

            int g = t.GSlot[i];
            if (g < 0) return;

            int links = t.Links[i];

            // DynamicsWorld.StepOne, `if (!body.Alive) return;`. The commit copies the pending
            // spheres, which a lost body stopped refreshing; with the commit a swap of two sets,
            // the pending set is handed the committed values so that the swap is the copy.
            if (gl.Alive[g] == 0)
            {
                KeepSpheres(g, links, cs, ps, cfg);
                return;
            }

            int dof = t.Dof[i];

            // ---- the thread's own copy of the body -------------------------------------------
            var parent = new int[MaxLinks];
            var dofCount = new int[MaxLinks];
            var dofStart = new int[MaxLinks];
            var panelStart = new int[MaxLinks + 1];
            var thinAxis = new int[MaxLinks];

            var mass = new Real[MaxLinks];
            var lift = new Real[MaxLinks];
            var volume = new Real[MaxLinks];
            var smallI = new Real[MaxLinks];
            var reach = new Real[MaxLinks];
            var arm = new Real[MaxLinks];
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
                driveTq[3 * k] = 0; driveTq[3 * k + 1] = 0; driveTq[3 * k + 2] = 0;
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
                thinAxis[l] = t.ThinAxis[at];

                mass[l] = b.Mass[at];
                lift[l] = b.Lift[at];
                volume[l] = b.Volume[at];
                smallI[l] = b.SmallI[at];
                reach[l] = b.Reach[at];
                arm[l] = b.Arm[at];

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
                    water[3 * l + k] = s.Water[a3];
                    wacc[3 * l + k] = s.WaterAcc[a3];
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
            // Still water leaves both arrays as they stood, and a current with no acceleration
            // term leaves the acceleration as it stood: the CPU writes neither.
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

            // ---- 2. senses, 3. brain ----------------------------------------------------------
            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << SDepth)) != 0;
            bool readsUp = (mask & (1 << SUp)) != 0;
            bool readsJoint = (mask & (1 << SJointAngle)) != 0 || (mask & (1 << SJointRate)) != 0;
            bool readsFlow = (mask & (1 << SFlow)) != 0;
            bool readsChemical = (mask & (1 << SChemical)) != 0;
            bool readsEnergy = (mask & (1 << SEnergy)) != 0;

            int gates = br.Gates[i];
            bool hasNutrients = (gates & 1) != 0;
            bool hasReserve = (gates & 2) != 0;

            float energy = 0f;
            Sample(i, links, dofCount, dofStart, position, rotationMatrix, relVel, q, qd, limitHi,
                   br, w, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical && hasNutrients,
                   readsEnergy && hasReserve, sDepth, sUp, sChem, sFlow, sAngle, sRate, ref energy);

            // The brain reads the two gates itself (SensorRead): a body with no field or no
            // reserve source reads the standing-in constant on those channels.
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

                    // EffectorDrive.AppliedTorque, the checkpoint's and the dump's.
                    s.Applied[(3 * bl) * N + i] = world.X;
                    s.Applied[(3 * bl + 1) * N + i] = world.Y;
                    s.Applied[(3 * bl + 2) * N + i] = world.Z;

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
            long dragLimited = FluidApply(links, cfg, excess, mass, lift, volume, smallI, arm, thinAxis,
                position, rotationMatrix, spin, velocity, water, wacc, relVel, panelStart, pan, fext,
                dragF, dragT, preVel, preSpin, i, N);
            if (dragLimited != 0) dr.DragLimited[i] = dr.DragLimited[i] + dragLimited;

            // ---- 6. contacts (Contacts.Apply) ------------------------------------------------
            if (cfg.PerPart != 0)
            {
                ContactsPerPart(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }
            else
            {
                ContactsBody(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }

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
                RecordTraceFrame(i, links, MaxLinks, dofCount, dofStart, position, velocity, spin, qd,
                                 fext, water, tr, cfg);
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
                // Alive = false; MarkLost(): the pending spheres keep the centres and velocities
                // they were committed with, and lose their radii and their place in every set.
                gl.Alive[g] = 0;
                KeepSpheres(g, links, cs, ps, cfg);
                ps.R[g] = 0;
                ps.Active[g] = 0;

                if (cfg.PerPart != 0)
                {
                    for (int l = 0; l < links; l++) ps.LR[g * LinkStride + l] = 0;
                }
            }
            else
            {
                // ---- 15. the contact spheres, pending (RefreshContactSphere) ------------------
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
                ps.Cx[g] = centre.X; ps.Cy[g] = centre.Y; ps.Cz[g] = centre.Z;
                ps.R[g] = radius;
                ps.Vx[g] = mean.X; ps.Vy[g] = mean.Y; ps.Vz[g] = mean.Z;
                ps.Active[g] = 1;

                // RefreshLinkSpheres (D114): a one-link body's link sphere is its body sphere.
                if (cfg.PerPart != 0)
                {
                    int row = g * LinkStride;

                    if (links == 1)
                    {
                        ps.LCx[row] = centre.X; ps.LCy[row] = centre.Y; ps.LCz[row] = centre.Z;
                        ps.LR[row] = radius;
                        ps.LVx[row] = mean.X; ps.LVy[row] = mean.Y; ps.LVz[row] = mean.Z;
                    }
                    else
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LCx[row + l] = position[3 * l];
                            ps.LCy[row + l] = position[3 * l + 1];
                            ps.LCz[row + l] = position[3 * l + 2];
                            ps.LR[row + l] = reach[l];
                            ps.LVx[row + l] = velocity[3 * l];
                            ps.LVy[row + l] = velocity[3 * l + 1];
                            ps.LVz[row + l] = velocity[3 * l + 2];
                        }
                    }
                }
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
        // ============================================================== the body phase, class 2

        public static void Step2(
            Index1D index, STopo t, SConst b, SPanels pan, SState s, SDrive dr, SNeur nr, SBrain br,
            STrace tr, SGlob gl, SSph cs, SSph ps, SGrid gr, SWorld w, SCfg cfg)
        {
            const int MaxLinks = 8;
            const int MaxDof = 24;

            int i = index.X;
            int N = cfg.N;
            if (i >= N) return;

            int g = t.GSlot[i];
            if (g < 0) return;

            int links = t.Links[i];

            // DynamicsWorld.StepOne, `if (!body.Alive) return;`. The commit copies the pending
            // spheres, which a lost body stopped refreshing; with the commit a swap of two sets,
            // the pending set is handed the committed values so that the swap is the copy.
            if (gl.Alive[g] == 0)
            {
                KeepSpheres(g, links, cs, ps, cfg);
                return;
            }

            int dof = t.Dof[i];

            // ---- the thread's own copy of the body -------------------------------------------
            var parent = new int[MaxLinks];
            var dofCount = new int[MaxLinks];
            var dofStart = new int[MaxLinks];
            var panelStart = new int[MaxLinks + 1];
            var thinAxis = new int[MaxLinks];

            var mass = new Real[MaxLinks];
            var lift = new Real[MaxLinks];
            var volume = new Real[MaxLinks];
            var smallI = new Real[MaxLinks];
            var reach = new Real[MaxLinks];
            var arm = new Real[MaxLinks];
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
                driveTq[3 * k] = 0; driveTq[3 * k + 1] = 0; driveTq[3 * k + 2] = 0;
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
                thinAxis[l] = t.ThinAxis[at];

                mass[l] = b.Mass[at];
                lift[l] = b.Lift[at];
                volume[l] = b.Volume[at];
                smallI[l] = b.SmallI[at];
                reach[l] = b.Reach[at];
                arm[l] = b.Arm[at];

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
                    water[3 * l + k] = s.Water[a3];
                    wacc[3 * l + k] = s.WaterAcc[a3];
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
            // Still water leaves both arrays as they stood, and a current with no acceleration
            // term leaves the acceleration as it stood: the CPU writes neither.
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

            // ---- 2. senses, 3. brain ----------------------------------------------------------
            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << SDepth)) != 0;
            bool readsUp = (mask & (1 << SUp)) != 0;
            bool readsJoint = (mask & (1 << SJointAngle)) != 0 || (mask & (1 << SJointRate)) != 0;
            bool readsFlow = (mask & (1 << SFlow)) != 0;
            bool readsChemical = (mask & (1 << SChemical)) != 0;
            bool readsEnergy = (mask & (1 << SEnergy)) != 0;

            int gates = br.Gates[i];
            bool hasNutrients = (gates & 1) != 0;
            bool hasReserve = (gates & 2) != 0;

            float energy = 0f;
            Sample(i, links, dofCount, dofStart, position, rotationMatrix, relVel, q, qd, limitHi,
                   br, w, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical && hasNutrients,
                   readsEnergy && hasReserve, sDepth, sUp, sChem, sFlow, sAngle, sRate, ref energy);

            // The brain reads the two gates itself (SensorRead): a body with no field or no
            // reserve source reads the standing-in constant on those channels.
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

                    // EffectorDrive.AppliedTorque, the checkpoint's and the dump's.
                    s.Applied[(3 * bl) * N + i] = world.X;
                    s.Applied[(3 * bl + 1) * N + i] = world.Y;
                    s.Applied[(3 * bl + 2) * N + i] = world.Z;

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
            long dragLimited = FluidApply(links, cfg, excess, mass, lift, volume, smallI, arm, thinAxis,
                position, rotationMatrix, spin, velocity, water, wacc, relVel, panelStart, pan, fext,
                dragF, dragT, preVel, preSpin, i, N);
            if (dragLimited != 0) dr.DragLimited[i] = dr.DragLimited[i] + dragLimited;

            // ---- 6. contacts (Contacts.Apply) ------------------------------------------------
            if (cfg.PerPart != 0)
            {
                ContactsPerPart(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }
            else
            {
                ContactsBody(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }

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
                RecordTraceFrame(i, links, MaxLinks, dofCount, dofStart, position, velocity, spin, qd,
                                 fext, water, tr, cfg);
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
                // Alive = false; MarkLost(): the pending spheres keep the centres and velocities
                // they were committed with, and lose their radii and their place in every set.
                gl.Alive[g] = 0;
                KeepSpheres(g, links, cs, ps, cfg);
                ps.R[g] = 0;
                ps.Active[g] = 0;

                if (cfg.PerPart != 0)
                {
                    for (int l = 0; l < links; l++) ps.LR[g * LinkStride + l] = 0;
                }
            }
            else
            {
                // ---- 15. the contact spheres, pending (RefreshContactSphere) ------------------
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
                ps.Cx[g] = centre.X; ps.Cy[g] = centre.Y; ps.Cz[g] = centre.Z;
                ps.R[g] = radius;
                ps.Vx[g] = mean.X; ps.Vy[g] = mean.Y; ps.Vz[g] = mean.Z;
                ps.Active[g] = 1;

                // RefreshLinkSpheres (D114): a one-link body's link sphere is its body sphere.
                if (cfg.PerPart != 0)
                {
                    int row = g * LinkStride;

                    if (links == 1)
                    {
                        ps.LCx[row] = centre.X; ps.LCy[row] = centre.Y; ps.LCz[row] = centre.Z;
                        ps.LR[row] = radius;
                        ps.LVx[row] = mean.X; ps.LVy[row] = mean.Y; ps.LVz[row] = mean.Z;
                    }
                    else
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LCx[row + l] = position[3 * l];
                            ps.LCy[row + l] = position[3 * l + 1];
                            ps.LCz[row + l] = position[3 * l + 2];
                            ps.LR[row + l] = reach[l];
                            ps.LVx[row + l] = velocity[3 * l];
                            ps.LVy[row + l] = velocity[3 * l + 1];
                            ps.LVz[row + l] = velocity[3 * l + 2];
                        }
                    }
                }
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
        // ============================================================== the body phase, class 3

        public static void Step3(
            Index1D index, STopo t, SConst b, SPanels pan, SState s, SDrive dr, SNeur nr, SBrain br,
            STrace tr, SGlob gl, SSph cs, SSph ps, SGrid gr, SWorld w, SCfg cfg)
        {
            const int MaxLinks = 16;
            const int MaxDof = 48;

            int i = index.X;
            int N = cfg.N;
            if (i >= N) return;

            int g = t.GSlot[i];
            if (g < 0) return;

            int links = t.Links[i];

            // DynamicsWorld.StepOne, `if (!body.Alive) return;`. The commit copies the pending
            // spheres, which a lost body stopped refreshing; with the commit a swap of two sets,
            // the pending set is handed the committed values so that the swap is the copy.
            if (gl.Alive[g] == 0)
            {
                KeepSpheres(g, links, cs, ps, cfg);
                return;
            }

            int dof = t.Dof[i];

            // ---- the thread's own copy of the body -------------------------------------------
            var parent = new int[MaxLinks];
            var dofCount = new int[MaxLinks];
            var dofStart = new int[MaxLinks];
            var panelStart = new int[MaxLinks + 1];
            var thinAxis = new int[MaxLinks];

            var mass = new Real[MaxLinks];
            var lift = new Real[MaxLinks];
            var volume = new Real[MaxLinks];
            var smallI = new Real[MaxLinks];
            var reach = new Real[MaxLinks];
            var arm = new Real[MaxLinks];
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
                driveTq[3 * k] = 0; driveTq[3 * k + 1] = 0; driveTq[3 * k + 2] = 0;
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
                thinAxis[l] = t.ThinAxis[at];

                mass[l] = b.Mass[at];
                lift[l] = b.Lift[at];
                volume[l] = b.Volume[at];
                smallI[l] = b.SmallI[at];
                reach[l] = b.Reach[at];
                arm[l] = b.Arm[at];

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
                    water[3 * l + k] = s.Water[a3];
                    wacc[3 * l + k] = s.WaterAcc[a3];
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
            // Still water leaves both arrays as they stood, and a current with no acceleration
            // term leaves the acceleration as it stood: the CPU writes neither.
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

            // ---- 2. senses, 3. brain ----------------------------------------------------------
            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << SDepth)) != 0;
            bool readsUp = (mask & (1 << SUp)) != 0;
            bool readsJoint = (mask & (1 << SJointAngle)) != 0 || (mask & (1 << SJointRate)) != 0;
            bool readsFlow = (mask & (1 << SFlow)) != 0;
            bool readsChemical = (mask & (1 << SChemical)) != 0;
            bool readsEnergy = (mask & (1 << SEnergy)) != 0;

            int gates = br.Gates[i];
            bool hasNutrients = (gates & 1) != 0;
            bool hasReserve = (gates & 2) != 0;

            float energy = 0f;
            Sample(i, links, dofCount, dofStart, position, rotationMatrix, relVel, q, qd, limitHi,
                   br, w, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical && hasNutrients,
                   readsEnergy && hasReserve, sDepth, sUp, sChem, sFlow, sAngle, sRate, ref energy);

            // The brain reads the two gates itself (SensorRead): a body with no field or no
            // reserve source reads the standing-in constant on those channels.
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

                    // EffectorDrive.AppliedTorque, the checkpoint's and the dump's.
                    s.Applied[(3 * bl) * N + i] = world.X;
                    s.Applied[(3 * bl + 1) * N + i] = world.Y;
                    s.Applied[(3 * bl + 2) * N + i] = world.Z;

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
            long dragLimited = FluidApply(links, cfg, excess, mass, lift, volume, smallI, arm, thinAxis,
                position, rotationMatrix, spin, velocity, water, wacc, relVel, panelStart, pan, fext,
                dragF, dragT, preVel, preSpin, i, N);
            if (dragLimited != 0) dr.DragLimited[i] = dr.DragLimited[i] + dragLimited;

            // ---- 6. contacts (Contacts.Apply) ------------------------------------------------
            if (cfg.PerPart != 0)
            {
                ContactsPerPart(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }
            else
            {
                ContactsBody(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }

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
                RecordTraceFrame(i, links, MaxLinks, dofCount, dofStart, position, velocity, spin, qd,
                                 fext, water, tr, cfg);
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
                // Alive = false; MarkLost(): the pending spheres keep the centres and velocities
                // they were committed with, and lose their radii and their place in every set.
                gl.Alive[g] = 0;
                KeepSpheres(g, links, cs, ps, cfg);
                ps.R[g] = 0;
                ps.Active[g] = 0;

                if (cfg.PerPart != 0)
                {
                    for (int l = 0; l < links; l++) ps.LR[g * LinkStride + l] = 0;
                }
            }
            else
            {
                // ---- 15. the contact spheres, pending (RefreshContactSphere) ------------------
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
                ps.Cx[g] = centre.X; ps.Cy[g] = centre.Y; ps.Cz[g] = centre.Z;
                ps.R[g] = radius;
                ps.Vx[g] = mean.X; ps.Vy[g] = mean.Y; ps.Vz[g] = mean.Z;
                ps.Active[g] = 1;

                // RefreshLinkSpheres (D114): a one-link body's link sphere is its body sphere.
                if (cfg.PerPart != 0)
                {
                    int row = g * LinkStride;

                    if (links == 1)
                    {
                        ps.LCx[row] = centre.X; ps.LCy[row] = centre.Y; ps.LCz[row] = centre.Z;
                        ps.LR[row] = radius;
                        ps.LVx[row] = mean.X; ps.LVy[row] = mean.Y; ps.LVz[row] = mean.Z;
                    }
                    else
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LCx[row + l] = position[3 * l];
                            ps.LCy[row + l] = position[3 * l + 1];
                            ps.LCz[row + l] = position[3 * l + 2];
                            ps.LR[row + l] = reach[l];
                            ps.LVx[row + l] = velocity[3 * l];
                            ps.LVy[row + l] = velocity[3 * l + 1];
                            ps.LVz[row + l] = velocity[3 * l + 2];
                        }
                    }
                }
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

        /// <summary>A lost or unstepped body's pending spheres handed its committed ones, so the swap is a copy.</summary>
        private static void KeepSpheres(int g, int links, SSph cs, SSph ps, SCfg cfg)
        {
            ps.Cx[g] = cs.Cx[g]; ps.Cy[g] = cs.Cy[g]; ps.Cz[g] = cs.Cz[g]; ps.R[g] = cs.R[g];
            ps.Vx[g] = cs.Vx[g]; ps.Vy[g] = cs.Vy[g]; ps.Vz[g] = cs.Vz[g];
            ps.Active[g] = cs.Active[g];

            if (cfg.PerPart == 0) return;

            int row = g * LinkStride;
            for (int l = 0; l < links; l++)
            {
                ps.LCx[row + l] = cs.LCx[row + l]; ps.LCy[row + l] = cs.LCy[row + l];
                ps.LCz[row + l] = cs.LCz[row + l]; ps.LR[row + l] = cs.LR[row + l];
                ps.LVx[row + l] = cs.LVx[row + l]; ps.LVy[row + l] = cs.LVy[row + l];
                ps.LVz[row + l] = cs.LVz[row + l];
            }
        }

        // ============================================================== the water pass

        /// <summary>
        /// CurrentField.VelocityAt(x, y, z, t) in a tank with the vent off: StreamsAt, or with the
        /// reefs ReefStreamsAt (the curl of g·A: g·u + ∇g × A, exact zeros inside the rock).
        /// </summary>
        private static void VelocityAt(float x, float y, float z, SWorld w, int ib, SCfg cfg,
                                       out float ox, out float oy, out float oz)
        {
            if (cfg.Speed <= 0f) { ox = 0f; oy = 0f; oz = 0f; return; }

            SD f;
            if (cfg.ReefCount == 0 || !ReefFade(x, y, z, w, cfg, out f))
            {
                StreamsAt(x, y, z, w, ib, cfg, out ox, out oy, out oz);
                return;
            }

            if (f.S == 0 && f.Gx == 0 && f.Gy == 0 && f.Gz == 0) { ox = 0f; oy = 0f; oz = 0f; return; }

            float ux, uy, uz, ax, ay, az;
            StreamsAt(x, y, z, w, ib, cfg, out ux, out uy, out uz);
            TankPotential(x, y, z, w, ib, cfg, out ax, out ay, out az);

            ox = (float)(f.S * ux + (f.Gy * az - f.Gz * ay));
            oy = (float)(f.S * uy + (f.Gz * ax - f.Gx * az));
            oz = (float)(f.S * uz + (f.Gx * ay - f.Gy * ax));
        }

        /// <summary>CurrentField.StreamsAt: the flat bed's own arithmetic, or the floor-following map's.</summary>
        private static void StreamsAt(float x, float y, float z, SWorld w, int ib, SCfg cfg,
                                      out float ox, out float oy, out float oz)
        {
            float ux, uy, uz;

            if (cfg.Sloped == 0)
            {
                // StreamsUnit(...) * (_speed * _streamsScale): a float product.
                StreamsUnit(x, y, z, cfg.Overturning, w, ib, cfg, out ux, out uy, out uz);
                ox = ux * cfg.SigmaFlat; oy = uy * cfg.SigmaFlat; oz = uz * cfg.SigmaFlat;
                return;
            }

            // StreamsSlopedUnit(...) * (float)(_speed * _streamsScale * _bedScale).
            StreamsSlopedUnit(x, y, z, w, ib, cfg, out ux, out uy, out uz);
            ox = ux * cfg.SigmaSloped; oy = uy * cfg.SigmaSloped; oz = uz * cfg.SigmaSloped;
        }

        /// <summary>CurrentField.TankPotential: the streams' potential before the reefs' fade, scaled.</summary>
        private static void TankPotential(float x, float y, float z, SWorld w, int ib, SCfg cfg,
                                          out float ox, out float oy, out float oz)
        {
            float ax, ay, az;

            if (cfg.Sloped == 0)
            {
                StreamsPotentialUnit(x, y, z, cfg.Overturning, w, ib, cfg, out ax, out ay, out az);
                ox = ax * cfg.SigmaFlat; oy = ay * cfg.SigmaFlat; oz = az * cfg.SigmaFlat;
                return;
            }

            StreamsSlopedPotentialUnit(x, y, z, w, ib, cfg, out ax, out ay, out az);
            ox = ax * cfg.SigmaSloped; oy = ay * cfg.SigmaSloped; oz = az * cfg.SigmaSloped;
        }

        /// <summary>CurrentField.MapAt: the floor-following map at a point, and the shore's fade there.</summary>
        private static SMap MapAt(Real x, Real y, Real z, SWorld w, SCfg cfg)
        {
            Real depth = cfg.CurDepth;

            Real h, hx, hz, hxx, hxz, hzz;
            BedSample(x, z, w, cfg, out h, out hx, out hz, out hxx, out hxz, out hzz);

            Real d = depth - h;
            Real floorY = -depth + h;

            if (y > 0) y = 0;
            else if (y < floorY) y = floorY;

            var map = default(SMap);

            map.Y = y;
            map.Height = h;
            map.SlopeX = hx;
            map.SlopeZ = hz;
            map.Depth = d;
            map.C = depth / d;
            map.MappedY = y * map.C;

            Real scale = y * depth / (d * d);
            map.A = scale * hx;
            map.B = scale * hz;

            map.Fade = 1;
            if (cfg.FadeOn != 0)
            {
                Real fade, fd, fdd;
                ShoreFade(d, cfg, out fade, out fd, out fdd);
                map.Fade = fade;
            }

            return map;
        }

        /// <summary>CurrentField.StreamsSlopedUnit.</summary>
        private static void StreamsSlopedUnit(Real x, Real y, Real z, SWorld w, int ib, SCfg cfg,
                                              out float ox, out float oy, out float oz)
        {
            SMap m = MapAt(x, y, z, w, cfg);

            if (cfg.FadeOn != 0)
            {
                StreamsFadedUnit(x, z, w, ib, cfg, m, out ox, out oy, out oz);
                return;
            }

            float ux, uy, uz;
            StreamsUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ux, out uy, out uz);

            ox = (float)(m.C * ux);
            oy = (float)(uy - m.A * ux - m.B * uz);
            oz = (float)(m.C * uz);
        }

        /// <summary>CurrentField.StreamsSlopedPotentialUnit.</summary>
        private static void StreamsSlopedPotentialUnit(Real x, Real y, Real z, SWorld w, int ib, SCfg cfg,
                                                       out float ox, out float oy, out float oz)
        {
            SMap m = MapAt(x, y, z, w, cfg);

            float ax, ay, az;

            if (cfg.FadeOn != 0)
            {
                // FadedPotential.
                if (m.Fade == 0) { ox = 0f; oy = 0f; oz = 0f; return; }

                StreamsPotentialUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ax, out ay, out az);

                Real f = m.Fade;
                ox = (float)(f * (ax + m.A * ay));
                oy = (float)(f * (m.C * ay));
                oz = (float)(f * (az + m.B * ay));
                return;
            }

            StreamsPotentialUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ax, out ay, out az);

            ox = (float)(ax + m.A * ay);
            oy = (float)(m.C * ay);
            oz = (float)(az + m.B * ay);
        }

        /// <summary>CurrentField.StreamsFadedUnit: f·u' + ∇f × A' (the beach, logbook/specs/beach-spec.md §3).</summary>
        private static void StreamsFadedUnit(Real x, Real z, SWorld w, int ib, SCfg cfg, SMap m,
                                             out float ox, out float oy, out float oz)
        {
            Real f, fd, fdd;
            ShoreFade(m.Depth, cfg, out f, out fd, out fdd);

            if (f == 0) { ox = 0f; oy = 0f; oz = 0f; return; }

            float ux, uy, uz;
            StreamsUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ux, out uy, out uz);

            Real vx = m.C * ux;
            Real vy = uy - m.A * ux - m.B * uz;
            Real vz = m.C * uz;

            if (fd == 0)
            {
                ox = (float)(f * vx);
                oy = (float)(f * vy);
                oz = (float)(f * vz);
                return;
            }

            float ax, ay, az;
            StreamsPotentialUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ax, out ay, out az);

            Real px = ax + m.A * ay;
            Real py = m.C * ay;
            Real pz = az + m.B * ay;

            Real gx = -fd * m.SlopeX;
            Real gz = -fd * m.SlopeZ;

            ox = (float)(f * vx - gz * py);
            oy = (float)(f * vy + gz * px - gx * pz);
            oz = (float)(f * vz + gx * py);
        }

        /// <summary>CurrentField.ShoreFade: the quintic in the distance to the shore times the depth factor.</summary>
        private static void ShoreFade(Real d, SCfg cfg, out Real f, out Real fd, out Real fdd)
        {
            Real depth = cfg.CurDepth;

            Real m, mx, mxx;
            DepthFactor(d / depth, out m, out mx, out mxx);
            Real md = mx / depth;
            Real mdd = mxx / (depth * depth);

            Real xi = (d - cfg.ShoreDepth) / cfg.ShoreFadeW;

            if (xi <= 0)
            {
                f = 0;
                fd = 0;
                fdd = 0;
                return;
            }

            if (xi >= 1)
            {
                f = m;
                fd = md;
                fdd = mdd;
                return;
            }

            Real xi2 = xi * xi;
            Real rest = (Real)1 - xi;

            Real q = xi2 * xi * ((Real)10 + xi * ((Real)(-15) + (Real)6 * xi));
            Real qd = (Real)30 * xi2 * rest * rest / cfg.ShoreFadeW;
            Real qdd = (Real)60 * xi * rest * ((Real)1 - (Real)2 * xi) / (cfg.ShoreFadeW * cfg.ShoreFadeW);

            f = q * m;
            fd = qd * m + q * md;
            fdd = qdd * m + (Real)2 * qd * md + q * mdd;
        }

        /// <summary>CurrentField.DepthFactor: d/D on the ramp, 1 deep, a C² turnover between.</summary>
        private static void DepthFactor(Real x, out Real m, out Real mx, out Real mxx)
        {
            Real W = (Real)0.1;

            if (x <= (Real)1 - W)
            {
                m = x;
                mx = 1;
                mxx = 0;
                return;
            }

            if (x >= (Real)1 + W)
            {
                m = 1;
                mx = 0;
                mxx = 0;
                return;
            }

            Real width = (Real)2 * W;
            Real s = (x - ((Real)1 - W)) / width;
            Real s2 = s * s;

            m = (Real)1 - W + width * (s - s2 * s + (Real)0.5 * s2 * s2);
            mx = (Real)1 - s2 * ((Real)3 - (Real)2 * s);
            mxx = (Real)(-6) * s * ((Real)1 - s) / width;
        }

        /// <summary>CurrentField.StreamsPotentialUnit at the samplers' instant (A).</summary>
        private static void StreamsPotentialUnit(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg,
                                                 out float ox, out float oy, out float oz)
        {
            Real depth = cfg.CurDepth;
            Real radius = cfg.CurTankR;

            if (y > 0) y = 0;
            else if (y < -depth) y = -depth;

            if (y >= 0 || y <= -depth) { ox = 0f; oy = 0f; oz = 0f; return; }

            Real dx = x - radius;
            Real dz = z - radius;
            Real r = System.Math.Sqrt(dx * dx + dz * dz);
            Real s = r / radius;
            if (s > 1) s = 1;

            Real cosTheta = r > 0 ? dx / r : 1;
            Real sinTheta = r > 0 ? dz / r : 0;

            Real phi = (Real)Math.PI * y / depth;
            Real cosPhi = System.Math.Cos(phi);
            Real sinPhi = System.Math.Sin(phi);

            Real sin1 = sinPhi;
            Real sin2 = (Real)2 * sinPhi * cosPhi;
            Real sin3 = sinPhi * ((Real)4 * cosPhi * cosPhi - (Real)1);

            Real ay = 0;

            Real cosM = cosTheta;
            Real sinM = sinTheta;

            int k = 0;

            for (int m = 1; m <= 4; m++)
            {
                Real sPow = 1;
                for (int e = 0; e < m; e++) sPow *= s;

                Real wall = (Real)1 - s * s;
                Real node = (Real)1 - (Real)2 * s * s;

                for (int j = 1; j <= 2; j++)
                {
                    Real f = j == 1 ? sPow * wall : sPow * wall * node;

                    for (int qq = 1; qq <= 3; qq++, k++)
                    {
                        Real profile = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;

                        Real amplitude =
                            cfg.EddyWeight * w.WR[cfg.StreamAmpAt + k] * w.Inst[ib + AEnv + k] * profile;

                        Real cosChi = cosM * w.Inst[ib + ACos + k] - sinM * w.Inst[ib + ASin + k];

                        ay -= amplitude * f * cosChi;
                    }
                }

                Real nextCos = cosM * cosTheta - sinM * sinTheta;
                Real nextSin = sinM * cosTheta + cosM * sinTheta;
                cosM = nextCos;
                sinM = nextSin;
            }

            Real wv = 0;

            if (overturning != 0)
            {
                Real wall = (Real)1 - s;

                for (int c = 0; c < Cells; c++)
                {
                    int qq = c + 1;
                    Real amplitude =
                        overturning * w.WR[cfg.CellAmpAt + c] * w.Inst[ib + ACellEnv + c] * w.Inst[ib + ACell + c];

                    Real sinKy = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;

                    wv += amplitude * wall * wall * sinKy;
                }
            }

            ox = (float)(wv * dz);
            oy = (float)ay;
            oz = (float)(-wv * dx);
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
            Real r = System.Math.Sqrt(dx * dx + dz * dz);
            Real s = r / radius;
            if (s > 1) s = 1;

            Real cosTheta = r > 0 ? dx / r : 1;
            Real sinTheta = r > 0 ? dz / r : 0;

            Real radial = 0;
            Real azimuthal = 0;
            Real vy = 0;

            Real phi = (Real)Math.PI * y / depth;
            Real cosPhi = System.Math.Cos(phi);
            Real sinPhi = System.Math.Sin(phi);

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
                            cfg.EddyWeight * w.WR[cfg.StreamAmpAt + k] * w.Inst[ib + AEnv + k] * profile;

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
                        overturning * w.WR[cfg.CellAmpAt + c] * w.Inst[ib + ACellEnv + c] * w.Inst[ib + ACell + c];

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

        /// <summary>
        /// CurrentField.AccelerationAt in a tank with the vent off: StreamsAccelerationAt, or with
        /// the reefs ReefStreamsAccelerationAt.
        /// </summary>
        private static void AccelerationAt(float x, float y, float z, SWorld w, int ib, SCfg cfg,
                                           out float ox, out float oy, out float oz)
        {
            if (cfg.Speed <= 0f) { ox = 0f; oy = 0f; oz = 0f; return; }

            SG g;
            SD f;

            if (cfg.ReefCount != 0 && ReefFade(x, y, z, w, cfg, out f))
            {
                if (f.S == 0 && f.Gx == 0 && f.Gy == 0 && f.Gz == 0) { ox = 0f; oy = 0f; oz = 0f; return; }

                g = ReefFadedGradient(x, y, z, w, ib, cfg, f);
            }
            else
            {
                g = StreamsGradientAt(x, y, z, w, ib, cfg);
            }

            Real perSecond = cfg.PerSecond;
            Real squared = cfg.Squared;

            ox = (float)(perSecond * g.Tx +
                         squared * (g.Vx * g.Xx + g.Vy * g.Xy + g.Vz * g.Xz));
            oy = (float)(perSecond * g.Ty +
                         squared * (g.Vx * g.Yx + g.Vy * g.Yy + g.Vz * g.Yz));
            oz = (float)(perSecond * g.Tz +
                         squared * (g.Vx * g.Zx + g.Vy * g.Zy + g.Vz * g.Zz));
        }

        /// <summary>The flat or the sloped streams' gradient at the acceleration's instant (B).</summary>
        private static SG StreamsGradientAt(Real x, Real y, Real z, SWorld w, int ib, SCfg cfg) =>
            cfg.Sloped == 0
                ? StreamsUnitWithGradient(x, y, z, cfg.Overturning, w, ib, cfg)
                : StreamsSlopedUnitWithGradient(x, y, z, cfg.Overturning, w, ib, cfg);

        /// <summary>CurrentField.ReefFadedGradient: u'' = g·u + G × A and its Jacobian.</summary>
        private static SG ReefFadedGradient(Real x, Real y, Real z, SWorld w, int ib, SCfg cfg, SD f)
        {
            SG u = StreamsGradientAt(x, y, z, w, ib, cfg);

            SG p = cfg.Sloped == 0
                ? StreamsPotentialUnitWithGradient(x, y, z, cfg.Overturning, w, ib, cfg)
                : SlopedPotentialWithGradient(x, y, z, cfg.Overturning, w, ib, cfg);

            Real gv = f.S;
            Real gx = f.Gx, gy = f.Gy, gz = f.Gz;

            var r = default(SG);

            r.Vx = gv * u.Vx + (gy * p.Vz - gz * p.Vy);
            r.Vy = gv * u.Vy + (gz * p.Vx - gx * p.Vz);
            r.Vz = gv * u.Vz + (gx * p.Vy - gy * p.Vx);

            r.Tx = gv * u.Tx + (gy * p.Tz - gz * p.Ty);
            r.Ty = gv * u.Ty + (gz * p.Tx - gx * p.Tz);
            r.Tz = gv * u.Tz + (gx * p.Ty - gy * p.Tx);

            Real jx, jy, jz;

            Column(
                gx, gv, gx, gy, gz, f.Hxx, f.Hxy, f.Hxz, u.Vx, u.Vy, u.Vz,
                u.Xx, u.Yx, u.Zx, p.Vx, p.Vy, p.Vz, p.Xx, p.Yx, p.Zx,
                out jx, out jy, out jz);
            r.Xx = jx; r.Yx = jy; r.Zx = jz;

            Column(
                gy, gv, gx, gy, gz, f.Hxy, f.Hyy, f.Hyz, u.Vx, u.Vy, u.Vz,
                u.Xy, u.Yy, u.Zy, p.Vx, p.Vy, p.Vz, p.Xy, p.Yy, p.Zy,
                out jx, out jy, out jz);
            r.Xy = jx; r.Yy = jy; r.Zy = jz;

            Column(
                gz, gv, gx, gy, gz, f.Hxz, f.Hyz, f.Hzz, u.Vx, u.Vy, u.Vz,
                u.Xz, u.Yz, u.Zz, p.Vx, p.Vy, p.Vz, p.Xz, p.Yz, p.Zz,
                out jx, out jy, out jz);
            r.Xz = jx; r.Yz = jy; r.Zz = jz;

            return r;
        }

        private static void Column(
            Real gk, Real g, Real gx, Real gy, Real gz,
            Real hxk, Real hyk, Real hzk,
            Real ux, Real uy, Real uz,
            Real dux, Real duy, Real duz,
            Real ax, Real ay, Real az,
            Real dax, Real day, Real daz,
            out Real jx, out Real jy, out Real jz)
        {
            jx = gk * ux + g * dux + (hyk * az - hzk * ay) + (gy * daz - gz * day);
            jy = gk * uy + g * duy + (hzk * ax - hxk * az) + (gz * dax - gx * daz);
            jz = gk * uz + g * duz + (hxk * ay - hyk * ax) + (gx * day - gy * dax);
        }

        /// <summary>CurrentField.StreamsSlopedUnitWithGradient at the acceleration's instant (B), with the shore's fade.</summary>
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

            if (cfg.FadeOn == 0) return g;

            Real fade, fd, fdd;
            ShoreFade(d, cfg, out fade, out fd, out fdd);

            if (fade == 0) return default(SG);
            if (fd == 0 && fdd == 0 && fade == 1) return g;

            return Faded(
                g, x, y * c, z, overturning, w, ib, cfg, fade, fd, fdd,
                hx, hz, hxx, hxz, hzz, a, b, c, ax, az, bz, cx, cz);
        }

        /// <summary>CurrentField.Faded: the shore's fade applied to the sloped gradient, u'' = f·u' + G × A'.</summary>
        private static SG Faded(
            SG u, Real x, Real mappedY, Real z, Real overturning, SWorld w, int ib, SCfg cfg,
            Real fade, Real fd, Real fdd,
            Real hx, Real hz, Real hxx, Real hxz, Real hzz,
            Real a, Real b, Real c,
            Real ax, Real az, Real bz, Real cx, Real cz)
        {
            SG p = StreamsPotentialUnitWithGradient(x, mappedY, z, overturning, w, ib, cfg);

            Real dxAx = p.Xx + a * p.Xy, dyAx = c * p.Xy, dzAx = p.Xz + b * p.Xy;
            Real dxAy = p.Yx + a * p.Yy, dyAy = c * p.Yy, dzAy = p.Yz + b * p.Yy;
            Real dxAz = p.Zx + a * p.Zy, dyAz = c * p.Zy, dzAz = p.Zz + b * p.Zy;

            Real px = p.Vx + a * p.Vy;
            Real py = c * p.Vy;
            Real pz = p.Vz + b * p.Vy;

            Real pxX = dxAx + ax * p.Vy + a * dxAy;
            Real pxY = dyAx + cx * p.Vy + a * dyAy;
            Real pxZ = dzAx + az * p.Vy + a * dzAy;

            Real pyX = cx * p.Vy + c * dxAy;
            Real pyY = c * dyAy;
            Real pyZ = cz * p.Vy + c * dzAy;

            Real pzX = dxAz + az * p.Vy + b * dxAy;
            Real pzY = dyAz + cz * p.Vy + b * dyAy;
            Real pzZ = dzAz + bz * p.Vy + b * dzAy;

            Real ptX = p.Tx + a * p.Ty;
            Real ptY = c * p.Ty;
            Real ptZ = p.Tz + b * p.Ty;

            Real gx = -fd * hx;
            Real gz = -fd * hz;

            Real gxX = fdd * hx * hx - fd * hxx;
            Real gxZ = fdd * hx * hz - fd * hxz;
            Real gzX = gxZ;
            Real gzZ = fdd * hz * hz - fd * hzz;

            var g = default(SG);

            g.Vx = fade * u.Vx - gz * py;
            g.Vy = fade * u.Vy + gz * px - gx * pz;
            g.Vz = fade * u.Vz + gx * py;

            g.Tx = fade * u.Tx - gz * ptY;
            g.Ty = fade * u.Ty + gz * ptX - gx * ptZ;
            g.Tz = fade * u.Tz + gx * ptY;

            g.Xx = gx * u.Vx + fade * u.Xx - gzX * py - gz * pyX;
            g.Xy = fade * u.Xy - gz * pyY;
            g.Xz = gz * u.Vx + fade * u.Xz - gzZ * py - gz * pyZ;

            g.Yx = gx * u.Vy + fade * u.Yx + gzX * px + gz * pxX - gxX * pz - gx * pzX;
            g.Yy = fade * u.Yy + gz * pxY - gx * pzY;
            g.Yz = gz * u.Vy + fade * u.Yz + gzZ * px + gz * pxZ - gxZ * pz - gx * pzZ;

            g.Zx = gx * u.Vz + fade * u.Zx + gxX * py + gx * pyX;
            g.Zy = fade * u.Zy + gx * pyY;
            g.Zz = gz * u.Vz + fade * u.Zz + gxZ * py + gx * pyZ;

            return g;
        }

        /// <summary>CurrentField.SlopedPotentialWithGradient: the mapped potential A' and its Jacobian, faded at the shore.</summary>
        private static SG SlopedPotentialWithGradient(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg)
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
            Real bz = y * depth * (hzz * inverseSquared + (Real)2 * hz * hz * inverseCubed);

            SG p = StreamsPotentialUnitWithGradient(x, y * c, z, overturning, w, ib, cfg);

            Real dxAx = p.Xx + a * p.Xy, dyAx = c * p.Xy, dzAx = p.Xz + b * p.Xy;
            Real dxAy = p.Yx + a * p.Yy, dyAy = c * p.Yy, dzAy = p.Yz + b * p.Yy;
            Real dxAz = p.Zx + a * p.Zy, dyAz = c * p.Zy, dzAz = p.Zz + b * p.Zy;

            var r = default(SG);

            r.Vx = p.Vx + a * p.Vy;
            r.Vy = c * p.Vy;
            r.Vz = p.Vz + b * p.Vy;

            r.Xx = dxAx + ax * p.Vy + a * dxAy;
            r.Xy = dyAx + cx * p.Vy + a * dyAy;
            r.Xz = dzAx + az * p.Vy + a * dzAy;

            r.Yx = cx * p.Vy + c * dxAy;
            r.Yy = c * dyAy;
            r.Yz = cz * p.Vy + c * dzAy;

            r.Zx = dxAz + az * p.Vy + b * dxAy;
            r.Zy = dyAz + cz * p.Vy + b * dyAy;
            r.Zz = dzAz + bz * p.Vy + b * dzAy;

            r.Tx = p.Tx + a * p.Ty;
            r.Ty = c * p.Ty;
            r.Tz = p.Tz + b * p.Ty;

            if (cfg.FadeOn == 0) return r;

            Real fade, fd, fdd;
            ShoreFade(d, cfg, out fade, out fd, out fdd);

            if (fade == 0) return default(SG);

            Real fx = -fd * hx;
            Real fz = -fd * hz;

            var s = default(SG);

            s.Vx = fade * r.Vx; s.Vy = fade * r.Vy; s.Vz = fade * r.Vz;
            s.Tx = fade * r.Tx; s.Ty = fade * r.Ty; s.Tz = fade * r.Tz;

            s.Xx = fx * r.Vx + fade * r.Xx; s.Xy = fade * r.Xy; s.Xz = fz * r.Vx + fade * r.Xz;
            s.Yx = fx * r.Vy + fade * r.Yx; s.Yy = fade * r.Yy; s.Yz = fz * r.Vy + fade * r.Yz;
            s.Zx = fx * r.Vz + fade * r.Zx; s.Zy = fade * r.Zy; s.Zz = fz * r.Vz + fade * r.Zz;

            return s;
        }

        /// <summary>CurrentField.StreamsPotentialUnitWithGradient at the acceleration's instant (B).</summary>
        private static SG StreamsPotentialUnitWithGradient(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg)
        {
            Real depth = cfg.CurDepth;
            Real radius = cfg.CurTankR;

            if (y > 0) y = 0;
            else if (y < -depth) y = -depth;

            bool atFace = y >= 0 || y <= -depth;

            Real dx = x - radius;
            Real dz = z - radius;
            Real r = System.Math.Sqrt(dx * dx + dz * dz);

            if (r > radius)
            {
                dx = radius * dx / r;
                dz = radius * dz / r;
                r = radius;
            }

            Real invR = (Real)1 / radius;
            Real s = r * invR;
            Real u = s * s;
            Real ux = (Real)2 * dx * invR * invR;
            Real uz = (Real)2 * dz * invR * invR;

            Real unitX = dx * invR;
            Real unitZ = dz * invR;

            Real phi = (Real)Math.PI * y / depth;
            Real cosPhi = System.Math.Cos(phi);
            Real sinPhi = System.Math.Sin(phi);

            Real sin1 = atFace ? 0 : sinPhi;
            Real sin2 = atFace ? 0 : (Real)2 * sinPhi * cosPhi;
            Real sin3 = atFace ? 0 : sinPhi * ((Real)4 * cosPhi * cosPhi - (Real)1);

            Real cos1 = cosPhi;
            Real cos2 = cosPhi * cosPhi - sinPhi * sinPhi;
            Real cos3 = cosPhi * ((Real)4 * cosPhi * cosPhi - (Real)3);

            var g = default(SG);

            Real cPrev = 1, sPrev = 0;
            Real cM = unitX, sM = unitZ;

            int k = 0;

            for (int m = 1; m <= 4; m++)
            {
                Real mR = m * invR;

                for (int j = 1; j <= 2; j++)
                {
                    Real poly = j == 1 ? (Real)1 - u : ((Real)1 - u) * ((Real)1 - (Real)2 * u);
                    Real polyU = j == 1 ? (Real)(-1) : (Real)(-3) + (Real)4 * u;

                    Real bc = 0, bs = 0, yc = 0, ys = 0;
                    Real tc = 0, ts = 0, wc = 0, ws = 0;

                    for (int qq = 1; qq <= 3; qq++, k++)
                    {
                        Real profile = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;
                        Real ky = qq * (Real)Math.PI / depth;
                        Real slope = ky * (qq == 1 ? cos1 : qq == 2 ? cos2 : cos3);

                        Real raw = cfg.EddyWeight * w.WR[cfg.StreamAmpAt + k];
                        Real envelope = w.Inst[ib + BEnv + k];

                        Real bb = raw * envelope * profile;
                        Real by = raw * envelope * slope;
                        Real bt = raw * w.Inst[ib + BEnvRate + k] * profile;
                        Real bw = bb * w.WR[cfg.StreamRateAt + k];

                        Real cosPsi = w.Inst[ib + BCos + k];
                        Real sinPsi = w.Inst[ib + BSin + k];

                        bc += bb * cosPsi; bs += bb * sinPsi;
                        yc += by * cosPsi; ys += by * sinPsi;
                        tc += bt * cosPsi; ts += bt * sinPsi;
                        wc += bw * cosPsi; ws += bw * sinPsi;
                    }

                    Real qv = cM * bc - sM * bs;
                    Real qx = mR * (cPrev * bc - sPrev * bs);
                    Real qz = mR * (-sPrev * bc - cPrev * bs);

                    g.Vy -= poly * qv;
                    g.Yx -= polyU * ux * qv + poly * qx;
                    g.Yz -= polyU * uz * qv + poly * qz;
                    g.Yy -= poly * (cM * yc - sM * ys);

                    g.Ty -= poly * (cM * tc - sM * ts);
                    g.Ty += poly * (cM * ws + sM * wc);
                }

                Real nextC = cM * unitX - sM * unitZ;
                Real nextS = sM * unitX + cM * unitZ;
                cPrev = cM; sPrev = sM;
                cM = nextC; sM = nextS;
            }

            if (overturning != 0)
            {
                Real wall = (Real)1 - s;
                Real wallSquared = wall * wall;

                Real sx = r > 0 ? dx / (r * radius) : 0;
                Real sz = r > 0 ? dz / (r * radius) : 0;

                Real wv = 0, wsum = 0, wy = 0, wt = 0;

                for (int cell = 0; cell < Cells; cell++)
                {
                    int qq = cell + 1;
                    Real ky = qq * (Real)Math.PI / depth;

                    Real raw = overturning * w.WR[cfg.CellAmpAt + cell];
                    Real envelope = w.Inst[ib + BCellEnv + cell];
                    Real turn = w.Inst[ib + BCell + cell];

                    Real amplitude = raw * envelope * turn;
                    Real rate = raw * (w.Inst[ib + BCellEnvRate + cell] * turn +
                                       envelope * w.Inst[ib + BCellRate + cell]);

                    Real sinKy = qq == 1 ? sin1 : qq == 2 ? sin2 : sin3;
                    Real cosKy = qq == 1 ? cos1 : qq == 2 ? cos2 : cos3;

                    wv += amplitude * wallSquared * sinKy;
                    wsum += amplitude * (Real)(-2) * wall * sinKy;
                    wy += amplitude * wallSquared * ky * cosKy;
                    wt += rate * wallSquared * sinKy;
                }

                Real wx = wsum * sx;
                Real wz = wsum * sz;

                g.Vx += wv * dz;
                g.Vz -= wv * dx;

                g.Xx += wx * dz;
                g.Xy += wy * dz;
                g.Xz += wz * dz + wv;

                g.Zx -= wx * dx + wv;
                g.Zy -= wy * dx;
                g.Zz -= wz * dx;

                g.Tx += wt * dz;
                g.Tz -= wt * dx;
            }

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
            Real r = System.Math.Sqrt(dx * dx + dz * dz);

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
            Real cosPhi = System.Math.Cos(phi);
            Real sinPhi = System.Math.Sin(phi);

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

                        Real raw = cfg.EddyWeight * w.WR[cfg.StreamAmpAt + k];
                        Real envelope = w.Inst[ib + BEnv + k];

                        Real b = raw * envelope * profile;
                        Real by = raw * envelope * slope;
                        Real bt = raw * w.Inst[ib + BEnvRate + k] * profile;
                        Real bw = b * w.WR[cfg.StreamRateAt + k];

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

                Real raw = overturning * w.WR[cfg.CellAmpAt + c];
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

        // ============================================================== the bed

        /// <summary>
        /// BedShape.HeightGradientAndHessian, the water's reading (CurrentField.BedSample's memo is
        /// a pure cache): zeros on a bed without relief, flat and uncurved on the beach's shoal.
        /// </summary>
        private static void BedSample(Real x, Real z, SWorld w, SCfg cfg,
            out Real height, out Real gradientX, out Real gradientZ,
            out Real hessianXX, out Real hessianXZ, out Real hessianZZ)
        {
            if (cfg.BedRelief == 0)
            {
                height = 0; gradientX = 0; gradientZ = 0;
                hessianXX = 0; hessianXZ = 0; hessianZZ = 0;
                return;
            }

            Real dx = x - cfg.BedRadius;
            Real dz = z - cfg.BedRadius;

            Real h = cfg.TiltX * dx + cfg.TiltZ * dz - cfg.Offset;
            Real gx = cfg.TiltX;
            Real gz = cfg.TiltZ;
            Real xx = 0, xz = 0, zz = 0;

            for (int m = 0; m < cfg.BedModes; m++)
            {
                int o = cfg.BedAt + 4 * m;
                Real kx = w.WR[o], kz = w.WR[o + 1];
                Real chi = kx * dx + kz * dz + w.WR[o + 2];
                Real a = w.WR[o + 3];
                Real cos = System.Math.Cos(chi);
                Real sin = System.Math.Sin(chi);

                h += a * cos;
                gx -= a * kx * sin;
                gz -= a * kz * sin;

                xx -= a * kx * kx * cos;
                xz -= a * kx * kz * cos;
                zz -= a * kz * kz * cos;
            }

            if (cfg.ShoreOn != 0 && h > cfg.ShoalHeight)
            {
                height = cfg.ShoalHeight;
                gradientX = 0; gradientZ = 0;
                hessianXX = 0; hessianXZ = 0; hessianZZ = 0;
                return;
            }

            height = h;
            gradientX = gx;
            gradientZ = gz;
            hessianXX = xx;
            hessianXZ = xz;
            hessianZZ = zz;
        }

        /// <summary>BedShape.HeightAndGradient, the contact bed's reading.</summary>
        private static void BedHeightAndGradient(Real x, Real z, SWorld w, SCfg cfg,
            out Real height, out Real gradientX, out Real gradientZ)
        {
            if (cfg.BedRelief == 0)
            {
                height = 0; gradientX = 0; gradientZ = 0;
                return;
            }

            Real dx = x - cfg.BedRadius;
            Real dz = z - cfg.BedRadius;

            Real h = cfg.TiltX * dx + cfg.TiltZ * dz - cfg.Offset;
            Real gx = cfg.TiltX;
            Real gz = cfg.TiltZ;

            for (int m = 0; m < cfg.BedModes; m++)
            {
                int o = cfg.BedAt + 4 * m;
                Real kx = w.WR[o], kz = w.WR[o + 1];
                Real chi = kx * dx + kz * dz + w.WR[o + 2];
                Real a = w.WR[o + 3];
                Real cos = System.Math.Cos(chi);
                Real sin = System.Math.Sin(chi);

                h += a * cos;
                gx -= a * kx * sin;
                gz -= a * kz * sin;
            }

            if (cfg.ShoreOn != 0 && h > cfg.ShoalHeight)
            {
                height = cfg.ShoalHeight;
                gradientX = 0;
                gradientZ = 0;
                return;
            }

            height = h;
            gradientX = gx;
            gradientZ = gz;
        }

        // ============================================================== the reefs (ReefGeometry)
        //
        // A reef's sixteen numbers in SWorld.WR from cfg.ReefAt: X, Z, CapRadius, CapDepth,
        // StemRadius, A2, A3, A4, P2, P3, P4, Round (1 or 0), capMidY, outerMax, cosMin, reach.

        /// <summary>ReefGeometry.LowerBound: a cheap bound under the reef's signed distance.</summary>
        private static Real ReefLowerBound(int reef, Real x, Real y, Real z, SWorld w, SCfg cfg)
        {
            int o = cfg.ReefAt + reef * ReefStride;

            Real dx = x - w.WR[o], dz = z - w.WR[o + 1];
            Real rho = System.Math.Sqrt(dx * dx + dz * dz);
            Real v = y - w.WR[o + 12];

            Real cap = AbsR(v) - cfg.HalfThickness;
            Real past = rho - w.WR[o + 13];
            if (past > 0) cap = MaxR(cap, w.WR[o + 14] * past);

            Real stemRadius = w.WR[o + 4];
            if (!(stemRadius > 0)) return cap - (Real)1e-9;

            Real stem = MaxR(rho - stemRadius, v);
            return MinR(cap, stem) - cfg.Fillet / (Real)6 - (Real)1e-9;
        }

        /// <summary>ReefGeometry.OfReef: one reef's signed distance with its gradient and Hessian.</summary>
        private static SD ReefOf(int reef, Real x, Real y, Real z, SWorld w, SCfg cfg)
        {
            int o = cfg.ReefAt + reef * ReefStride;

            Real dx = x - w.WR[o];
            Real dz = z - w.WR[o + 1];
            Real rho = System.Math.Sqrt(dx * dx + dz * dz);

            Real nx = rho > 0 ? dx / rho : 1;
            Real nz = rho > 0 ? dz / rho : 0;

            SD cap = w.WR[o + 11] != 0
                ? RoundCap(rho, nx, nz, y - w.WR[o + 12], w.WR[o + 2] - cfg.HalfThickness, cfg)
                : Cap(o, rho, nx, nz, dx, dz, y - w.WR[o + 12], w, cfg);

            Real stemRadius = w.WR[o + 4];
            if (!(stemRadius > 0)) return cap;

            SD stem = Stem(rho, nx, nz, y - w.WR[o + 12], stemRadius);
            return SmoothUnion(cap, stem, cfg.Fillet);
        }

        private static SD RoundCap(Real rho, Real nx, Real nz, Real v, Real discRadius, SCfg cfg)
        {
            var d = default(SD);
            Real q = rho - discRadius;

            if (q > 0)
            {
                Real r2 = System.Math.Sqrt(q * q + v * v);
                d.S = r2 - cfg.HalfThickness;

                if (!(r2 > 0))
                {
                    d.Gy = 1;
                    return d;
                }

                Real sr = q / r2;
                Real sv = v / r2;
                Real cube = r2 * r2 * r2;

                d.Gx = sr * nx;
                d.Gy = sv;
                d.Gz = sr * nz;

                return AddRevolution(d, v * v / cube, -q * v / cube, q * q / cube, sr / rho, nx, nz);
            }

            d.S = AbsR(v) - cfg.HalfThickness;
            d.Gy = v >= 0 ? (Real)1 : (Real)(-1);
            return d;
        }

        private static SD Cap(int o, Real rho, Real nx, Real nz, Real dx, Real dz, Real v, SWorld w, SCfg cfg)
        {
            var d = default(SD);

            if (!(rho > 0))
            {
                d.S = AbsR(v) - cfg.HalfThickness;
                d.Gy = v >= 0 ? (Real)1 : (Real)(-1);
                return d;
            }

            Real ro, r1, r2d, r3;
            Outline(o, System.Math.Atan2(dz, dx), w, out ro, out r1, out r2d, out r3);

            Real w2 = ro * ro + r1 * r1;
            Real wl = System.Math.Sqrt(w2);
            Real c = ro / wl;
            Real e = rho - ro;
            Real q = e * c + cfg.HalfThickness;

            if (!(q > 0))
            {
                d.S = AbsR(v) - cfg.HalfThickness;
                d.Gy = v >= 0 ? (Real)1 : (Real)(-1);
                return d;
            }

            Real w3 = w2 * wl;
            Real w5 = w3 * w2;
            Real p = r1 * (r1 * r1 - ro * r2d);
            Real c1 = p / w3;
            Real p1 = (Real)2 * r1 * r1 * r2d - ro * r2d * r2d - ro * r1 * r3;
            Real c2 = p1 / w3 - (Real)3 * p * r1 * (ro + r2d) / w5;

            Real qr = c;
            Real qt = -r1 * c + e * c1;
            Real qrt = c1;
            Real qtt = -r2d * c - (Real)2 * r1 * c1 + e * c2;

            Real tx = -nz, tz = nx;
            Real gqx = qr * nx + qt / rho * tx;
            Real gqz = qr * nz + qt / rho * tz;

            Real mixed = qrt / rho - qt / (rho * rho);
            Real ring = qtt / (rho * rho) + qr / rho;

            Real hqxx = (Real)2 * mixed * nx * tx + ring * tx * tx;
            Real hqxz = mixed * (nx * tz + tx * nz) + ring * tx * tz;
            Real hqzz = (Real)2 * mixed * nz * tz + ring * tz * tz;

            Real s = System.Math.Sqrt(q * q + v * v);
            d.S = s - cfg.HalfThickness;

            if (!(s > 0))
            {
                d.Gy = 1;
                return d;
            }

            Real gx = q * gqx / s, gy = v / s, gz = q * gqz / s;
            d.Gx = gx;
            d.Gy = gy;
            d.Gz = gz;

            d.Hxx = (gqx * gqx + q * hqxx - gx * gx) / s;
            d.Hxz = (gqx * gqz + q * hqxz - gx * gz) / s;
            d.Hzz = (gqz * gqz + q * hqzz - gz * gz) / s;
            d.Hxy = (-gx * gy) / s;
            d.Hyz = (-gz * gy) / s;
            d.Hyy = ((Real)1 - gy * gy) / s;
            return d;
        }

        /// <summary>ReefGeometry.Outline: the cap's radius at an angle, and its first three derivatives.</summary>
        private static void Outline(int o, Real theta, SWorld w, out Real ro, out Real o1, out Real o2, out Real o3)
        {
            Real capRadius = w.WR[o + 2];
            Real a2 = w.WR[o + 5], a3 = w.WR[o + 6], a4 = w.WR[o + 7];

            Real c2 = System.Math.Cos((Real)2 * theta + w.WR[o + 8]), s2 = System.Math.Sin((Real)2 * theta + w.WR[o + 8]);
            Real c3 = System.Math.Cos((Real)3 * theta + w.WR[o + 9]), s3 = System.Math.Sin((Real)3 * theta + w.WR[o + 9]);
            Real c4 = System.Math.Cos((Real)4 * theta + w.WR[o + 10]), s4 = System.Math.Sin((Real)4 * theta + w.WR[o + 10]);

            ro = capRadius * ((Real)1 + a2 * c2 + a3 * c3 + a4 * c4);
            o1 = -capRadius * ((Real)2 * a2 * s2 + (Real)3 * a3 * s3 + (Real)4 * a4 * s4);
            o2 = -capRadius * ((Real)4 * a2 * c2 + (Real)9 * a3 * c3 + (Real)16 * a4 * c4);
            o3 = capRadius * ((Real)8 * a2 * s2 + (Real)27 * a3 * s3 + (Real)64 * a4 * s4);
        }

        private static SD Stem(Real rho, Real nx, Real nz, Real ey, Real stemRadius)
        {
            var d = default(SD);
            Real er = rho - stemRadius;

            if (er > 0 && ey > 0)
            {
                Real r3 = System.Math.Sqrt(er * er + ey * ey);
                Real sr = er / r3;
                Real sv = ey / r3;
                Real cube = r3 * r3 * r3;

                d.S = r3;
                d.Gx = sr * nx;
                d.Gy = sv;
                d.Gz = sr * nz;

                return AddRevolution(d, ey * ey / cube, -er * ey / cube, er * er / cube, sr / rho, nx, nz);
            }

            if (er > 0 || (ey <= 0 && er > ey))
            {
                d.S = er;
                d.Gx = nx;
                d.Gz = nz;

                if (rho > 0) d = AddRevolution(d, 0, 0, 0, (Real)1 / rho, nx, nz);
                return d;
            }

            d.S = ey;
            d.Gy = 1;
            return d;
        }

        private static SD AddRevolution(SD d, Real hrr, Real hry, Real hyy, Real curvature, Real nx, Real nz)
        {
            d.Hxx += hrr * nx * nx + curvature * ((Real)1 - nx * nx);
            d.Hxz += hrr * nx * nz - curvature * nx * nz;
            d.Hzz += hrr * nz * nz + curvature * ((Real)1 - nz * nz);
            d.Hxy += hry * nx;
            d.Hyz += hry * nz;
            d.Hyy += hyy;
            return d;
        }

        private static SD SmoothUnion(SD a, SD b, Real k)
        {
            Real gap = a.S - b.S;
            Real spread = AbsR(gap);
            if (spread >= k) return gap <= 0 ? a : b;

            Real h = (k - spread) / k;
            Real wa = gap < 0 ? (Real)1 - (Real)0.5 * h * h : (Real)0.5 * h * h;
            Real wb = (Real)1 - wa;
            Real bend = h / k;

            Real ex = a.Gx - b.Gx, ey = a.Gy - b.Gy, ez = a.Gz - b.Gz;

            var d = default(SD);
            d.S = MinR(a.S, b.S) - k * h * h * h / (Real)6;
            d.Gx = wa * a.Gx + wb * b.Gx;
            d.Gy = wa * a.Gy + wb * b.Gy;
            d.Gz = wa * a.Gz + wb * b.Gz;

            d.Hxx = wa * a.Hxx + wb * b.Hxx - bend * ex * ex;
            d.Hxy = wa * a.Hxy + wb * b.Hxy - bend * ex * ey;
            d.Hxz = wa * a.Hxz + wb * b.Hxz - bend * ex * ez;
            d.Hyy = wa * a.Hyy + wb * b.Hyy - bend * ey * ey;
            d.Hyz = wa * a.Hyz + wb * b.Hyz - bend * ey * ez;
            d.Hzz = wa * a.Hzz + wb * b.Hzz - bend * ez * ez;

            return d;
        }

        /// <summary>
        /// ReefGeometry.SignedDistance with a limit: the nearest reef's distance, its gradient
        /// and Hessian in <paramref name="at"/>; the least bound, and nothing in
        /// <paramref name="at"/>, when every reef's bound is at or past the limit.
        /// </summary>
        private static Real ReefSignedDistance(Real x, Real y, Real z, Real limit, SWorld w, SCfg cfg, out SD at)
        {
            at = default(SD);

            int count = cfg.ReefCount;
            int nearest = -1;
            int first = -1;
            Real firstBound = Real.PositiveInfinity;

            for (int reef = 0; reef < count; reef++)
            {
                Real bound = ReefLowerBound(reef, x, y, z, w, cfg);
                if (bound < firstBound)
                {
                    firstBound = bound;
                    first = reef;
                }
            }

            if (first < 0 || firstBound >= limit) return firstBound;

            at = ReefOf(first, x, y, z, w, cfg);
            nearest = first;
            Real best = at.S;

            for (int reef = 0; reef < count; reef++)
            {
                if (reef == first) continue;

                Real bound = ReefLowerBound(reef, x, y, z, w, cfg);
                if (bound > best || (bound == best && reef > nearest)) continue;

                SD d = ReefOf(reef, x, y, z, w, cfg);
                if (d.S < best || (d.S == best && reef < nearest))
                {
                    best = d.S;
                    nearest = reef;
                    at = d;
                }
            }

            return best;
        }

        /// <summary>ReefGeometry.Reaches: whether a reef's fade can reach the point at all.</summary>
        private static bool ReefReaches(int reef, Real x, Real y, Real z, SWorld w, SCfg cfg)
        {
            int o = cfg.ReefAt + reef * ReefStride;
            Real dx = x - w.WR[o];
            Real dz = z - w.WR[o + 1];
            Real reach = w.WR[o + 15];
            if (dx * dx + dz * dz >= reach * reach) return false;
            return ReefLowerBound(reef, x, y, z, w, cfg) < cfg.ReefFade;
        }

        /// <summary>
        /// ReefGeometry.Fade: the product of every reaching reef's quintic fade, with its gradient
        /// and Hessian; false when no reef's fade touches the point.
        /// </summary>
        private static bool ReefFade(Real x, Real y, Real z, SWorld w, SCfg cfg, out SD fade)
        {
            fade = default(SD);
            fade.S = 1;
            bool touched = false;
            Real fadeMetres = cfg.ReefFade;

            for (int reef = 0; reef < cfg.ReefCount; reef++)
            {
                if (!ReefReaches(reef, x, y, z, w, cfg)) continue;

                SD s = ReefOf(reef, x, y, z, w, cfg);
                Real xi = s.S / fadeMetres;
                if (xi >= 1) continue;

                touched = true;

                if (xi <= 0)
                {
                    fade = default(SD);
                    return true;
                }

                Real rest = (Real)1 - xi;
                Real q = xi * xi * xi * ((Real)10 + xi * ((Real)(-15) + (Real)6 * xi));
                Real q1 = (Real)30 * xi * xi * rest * rest / fadeMetres;
                Real q2 = (Real)60 * xi * rest * ((Real)1 - (Real)2 * xi) / (fadeMetres * fadeMetres);

                var f = default(SD);
                f.S = q;
                f.Gx = q1 * s.Gx; f.Gy = q1 * s.Gy; f.Gz = q1 * s.Gz;
                f.Hxx = q2 * s.Gx * s.Gx + q1 * s.Hxx;
                f.Hxy = q2 * s.Gx * s.Gy + q1 * s.Hxy;
                f.Hxz = q2 * s.Gx * s.Gz + q1 * s.Hxz;
                f.Hyy = q2 * s.Gy * s.Gy + q1 * s.Hyy;
                f.Hyz = q2 * s.Gy * s.Gz + q1 * s.Hyz;
                f.Hzz = q2 * s.Gz * s.Gz + q1 * s.Hzz;

                SD p = fade;
                var n = default(SD);
                n.S = p.S * f.S;
                n.Gx = f.S * p.Gx + p.S * f.Gx;
                n.Gy = f.S * p.Gy + p.S * f.Gy;
                n.Gz = f.S * p.Gz + p.S * f.Gz;
                n.Hxx = f.S * p.Hxx + (Real)2 * p.Gx * f.Gx + p.S * f.Hxx;
                n.Hyy = f.S * p.Hyy + (Real)2 * p.Gy * f.Gy + p.S * f.Hyy;
                n.Hzz = f.S * p.Hzz + (Real)2 * p.Gz * f.Gz + p.S * f.Hzz;
                n.Hxy = f.S * p.Hxy + p.Gx * f.Gy + f.Gx * p.Gy + p.S * f.Hxy;
                n.Hxz = f.S * p.Hxz + p.Gx * f.Gz + f.Gx * p.Gz + p.S * f.Hxz;
                n.Hyz = f.S * p.Hyz + p.Gy * f.Gz + f.Gy * p.Gz + p.S * f.Hyz;
                fade = n;
            }

            return touched;
        }

        /// <summary>Math.Max's IEEE 754:2019 maximum: NaN propagates, +0 over -0.</summary>
        private static Real MaxR(Real x, Real y)
        {
            if (x != y)
            {
                if (!(x != x)) return y < x ? x : y;
                return x;
            }
            return IsNegativeR(y) ? x : y;
        }

        /// <summary>Math.Min's IEEE 754:2019 minimum: NaN propagates, -0 under +0.</summary>
        private static Real MinR(Real x, Real y)
        {
            if (x != y)
            {
                if (!(x != x)) return x < y ? x : y;
                return x;
            }
            return IsNegativeR(x) ? x : y;
        }

        /// <summary>The sign bit of a number that is not NaN: -0 is negative.</summary>
        private static bool IsNegativeR(Real x) => x < 0 || (x == 0 && (Real)1 / x < 0);

        // ============================================================== senses (spike 3)

        private static void Sample(
            int i, int links, int[] dofCount, int[] dofStart, Real[] position, Real[] rotationMatrix,
            Real[] relVel, Real[] q, Real[] qd, Real[] limitHi, SBrain br, SWorld w, SCfg cfg,
            bool readsDepth, bool readsUp, bool readsJoint, bool readsFlow, bool readsChemical, bool readsEnergy,
            float[] sDepth, float[] sUp, float[] sChem, float[] sFlow, float[] sAngle, float[] sRate,
            ref float energy)
        {
            if (readsEnergy) energy = Squash(br.Reserve[i], cfg.EnergyScale);

            bool smells = readsChemical;

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
            int column = ix * cfg.Nz + iz;
            int lowest = w.WI[cfg.LowestAt + column];
            if (lowest < 0) return -1;

            int layer = iy < lowest ? iy : lowest;
            if (cfg.SplitColumns == 0) return (layer * cfg.Nx + ix) * cfg.Nz + iz;

            // GridField.InColumn in a reef world: the nearest live interval of the column, the
            // shallower on a tie. The interval holding the layer, when there is one, is the only
            // one at a gap of 0, so the live test the CPU makes first is this loop's own answer.
            int best = lowest;
            int bestGap = int.MaxValue;
            int from = w.WI[cfg.IntervalFirstAt + column];
            int to = w.WI[cfg.IntervalFirstAt + column + 1];

            for (int k = from; k < to; k++)
            {
                int top = w.WI[cfg.IntervalTopAt + k];
                int bottom = w.WI[cfg.IntervalBottomAt + k];
                int nearest = layer < top ? top : layer > bottom ? bottom : layer;
                int gap = nearest > layer ? nearest - layer : layer - nearest;

                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = nearest;
                }
            }

            return (best * cfg.Nx + ix) * cfg.Nz + iz;
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
                case SChemical: return (br.Gates[i] & 1) != 0 ? sChem[part] : cfg.ConstCE;
                case SEnergy: return (br.Gates[i] & 2) != 0 ? energy : cfg.ConstCE;
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

        // The CPU's own arithmetic: float operands promoted to double, System.Math, narrowed.
        private static float OscSin(double arg) => (float)Math.Sin(arg);
        private static float SinF(float a) => (float)Math.Sin(a);
        private static float CosF(float a) => (float)Math.Cos(a);
        private static float TanhF(float a) => (float)Math.Tanh(a);
        private static float FloorF(float a) => (float)Math.Floor(a);
        private static Real SqrtR(Real a) => Math.Sqrt(a);
        private static Real AbsR(Real a) => Math.Abs(a);

        // ============================================================== fluid

        private static long FluidApply(
            int links, SCfg cfg, Real excess, Real[] mass, Real[] lift, Real[] volume, Real[] smallI,
            Real[] arm, int[] thinAxis,
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
                Real unclamped = netDensity;

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

                // D111: the part's whole displaced weight at its buoyancy centre, gated on the clamp.
                if (cfg.OffsetTorque != 0 && arm[i] != 0 && height <= 0 &&
                    !(netDensity == 0 && unclamped != 0))
                {
                    int axis = thinAxis[i];
                    V3 lever = rotation * new V3(
                        axis == 0 ? arm[i] : 0,
                        axis == 1 ? arm[i] : 0,
                        axis == 2 ? arm[i] : 0);

                    V3 displaced = new V3(0, cfg.Density * volume[i] * cfg.Gravity, 0);

                    torque = torque + V3.Cross(lever, displaced);
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

        // ============================================================== contacts

        /// <summary>
        /// ContactGrid.Neighbours and NeighbourLinks: every row sharing a cell with
        /// <paramref name="self"/> and not belonging to <paramref name="owner"/>, as sort keys (the
        /// list rank, or under per-part contact rank·16 + link, which is the CPU's row order)
        /// ascending and once each.
        /// </summary>
        private static int Candidates(int self, int owner, int[] cand, SGlob gl, SGrid gr, SCfg cfg)
        {
            int R = cfg.Rows;
            bool perPart = cfg.PerPart != 0;

            int lx = gr.Lo[self], hx = gr.Hi[self];
            int ly = gr.Lo[R + self], hy = gr.Hi[R + self];
            int lz = gr.Lo[2 * R + self], hz = gr.Hi[2 * R + self];

            int found = 0;

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                int h = Hash(x, y, z) & cfg.Mask;

                for (int k = gr.Start[h]; k < gr.Start[h + 1] && k < cfg.EntryCap; k++)
                {
                    int other = gr.Items[k];
                    int og = perPart ? other / LinkStride : other;
                    if (og == owner) continue;

                    if (x < gr.Lo[other] || x > gr.Hi[other] ||
                        y < gr.Lo[R + other] || y > gr.Hi[R + other] ||
                        z < gr.Lo[2 * R + other] || z > gr.Hi[2 * R + other])
                    {
                        continue;
                    }

                    if (found < CandCap)
                    {
                        cand[found] = perPart
                            ? gl.Rank[og] * LinkStride + (other - og * LinkStride)
                            : gl.Rank[og];
                    }

                    found++;
                }
            }

            if (found > CandCap)
            {
                Atomic.Add(ref gl.Overflow[OverflowCandidates], 1L);
                found = CandCap;
            }

            if (found < 2) return found;

            for (int a = 1; a < found; a++)
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

            int unique = 1;
            for (int a = 1; a < found; a++)
            {
                int v = cand[a];
                if (v == cand[unique - 1]) continue;
                cand[unique] = v;
                unique++;
            }

            return unique;
        }

        /// <summary>Creature.NoteOverlap: appended in candidate order.</summary>
        private static int NoteOverlap(int g, int count, int id, int slot, SGlob gl, SCfg cfg)
        {
            if (count >= cfg.OverCap)
            {
                Atomic.Add(ref gl.Overflow[OverflowOverlaps], 1L);
                return count;
            }

            int GN = cfg.GN;
            gl.OvId[count * GN + g] = id;
            gl.OvSlot[count * GN + g] = slot;
            return count + 1;
        }

        /// <summary>Creature.NoteOverlapPart: inserted in ascending id order, the first link pair kept.</summary>
        private static int NoteOverlapPart(int g, int count, int id, int slot, int part, int otherPart, SGlob gl, SCfg cfg)
        {
            int GN = cfg.GN;

            int at = count;
            while (at > 0 && gl.OvId[(at - 1) * GN + g] > id) at--;
            if (at > 0 && gl.OvId[(at - 1) * GN + g] == id) return count;

            if (count >= cfg.OverCap)
            {
                Atomic.Add(ref gl.Overflow[OverflowOverlaps], 1L);
                return count;
            }

            for (int k = count; k > at; k--)
            {
                gl.OvId[k * GN + g] = gl.OvId[(k - 1) * GN + g];
                gl.OvSlot[k * GN + g] = gl.OvSlot[(k - 1) * GN + g];
                gl.OvPart[(2 * k) * GN + g] = gl.OvPart[(2 * k - 2) * GN + g];
                gl.OvPart[(2 * k + 1) * GN + g] = gl.OvPart[(2 * k - 1) * GN + g];
            }

            gl.OvId[at * GN + g] = id;
            gl.OvSlot[at * GN + g] = slot;
            gl.OvPart[(2 * at) * GN + g] = part;
            gl.OvPart[(2 * at + 1) * GN + g] = otherPart;
            return count + 1;
        }

        /// <summary>Contacts.Apply's body sphere, the recorded path.</summary>
        private static void ContactsBody(
            int g, int links, Real[] mass, Real m, Real[] fext, int[] cand, SSph s, SGlob gl, SGrid gr,
            SWorld w, SCfg cfg)
        {
            bool instrument = cfg.Instrument != 0;

            Real cx = s.Cx[g], cy = s.Cy[g], cz = s.Cz[g];
            Real r = s.R[g];
            Real vx = s.Vx[g], vy = s.Vy[g], vz = s.Vz[g];

            Real fx = 0, fy = 0, fz = 0;
            int nOver = 0;
            int bedGlass = 0;

            if (cfg.CreatureContact != 0)
            {
                int unique = Candidates(g, g, cand, gl, gr, cfg);

                for (int k = 0; k < unique; k++)
                {
                    int other = gl.RankToSlot[cand[k]];
                    if (other < 0 || s.Active[other] == 0) continue;

                    Real bx = cx - s.Cx[other];
                    Real by = cy - s.Cy[other];
                    Real bz = cz - s.Cz[other];
                    Real distance = System.Math.Sqrt(bx * bx + by * by + bz * bz);
                    Real penetration = r + s.R[other] - distance;
                    if (penetration <= 0) continue;

                    if (instrument) nOver = NoteOverlap(g, nOver, gl.Id[other], other, gl, cfg);

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

                    Real mo = gl.TotalMass[other];
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
                }
            }

            Real bedStiffness = m * cfg.Omega * cfg.Omega;
            Real bedDamping = (Real)2.0 * cfg.Zeta * m * cfg.Omega;

            AgainstTheWorld(cx, cy, cz, r, vx, vy, vz, m, bedStiffness, bedDamping, w, cfg,
                            ref fx, ref fy, ref fz, ref bedGlass);

            BodyPush(ref fx, ref fy, ref fz, m, cfg);

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

            if (instrument)
            {
                gl.NOver[g] = nOver;
                gl.BedGlass[g] = bedGlass;
            }
        }

        /// <summary>Contacts.ApplyPerPart (D114): every link's own sphere against every other body's links.</summary>
        private static void ContactsPerPart(
            int g, int links, Real[] mass, Real totalMass, Real[] fext, int[] cand, SSph s, SGlob gl, SGrid gr,
            SWorld w, SCfg cfg)
        {
            bool instrument = cfg.Instrument != 0;
            int nOver = 0;
            int bedGlass = 0;

            for (int i = 0; i < links; i++)
            {
                int row = g * LinkStride + i;

                Real cx = s.LCx[row], cy = s.LCy[row], cz = s.LCz[row];
                Real radius = s.LR[row];
                Real vx = s.LVx[row], vy = s.LVy[row], vz = s.LVz[row];
                Real m = mass[i];

                Real fx = 0, fy = 0, fz = 0;

                if (cfg.CreatureContact != 0)
                {
                    int n = Candidates(row, g, cand, gl, gr, cfg);

                    for (int k = 0; k < n; k++)
                    {
                        int key = cand[k];
                        int rank = key / LinkStride;
                        int og = gl.RankToSlot[rank];
                        if (og < 0 || s.Active[og] == 0) continue;

                        int j = key - rank * LinkStride;
                        int orow = og * LinkStride + j;

                        Real bx = cx - s.LCx[orow];
                        Real by = cy - s.LCy[orow];
                        Real bz = cz - s.LCz[orow];
                        Real distance = System.Math.Sqrt(bx * bx + by * by + bz * bz);
                        Real penetration = radius + s.LR[orow] - distance;
                        if (penetration <= 0) continue;

                        if (instrument) nOver = NoteOverlapPart(g, nOver, gl.Id[og], og, i, j, gl, cfg);

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

                        Real otherMass = gl.LinkMass[orow];
                        Real reduced = m * otherMass / (m + otherMass);

                        Real stiffness = reduced * cfg.Omega * cfg.Omega;
                        Real damping = (Real)2.0 * cfg.Zeta * reduced * cfg.Omega;

                        Real dvx = vx - s.LVx[orow];
                        Real dvy = vy - s.LVy[orow];
                        Real dvz = vz - s.LVz[orow];
                        Real approach = dvx * nx + dvy * ny + dvz * nz;

                        Real p = PairPush(stiffness * penetration - damping * approach, reduced, approach, cfg);

                        fx = fx + nx * p;
                        fy = fy + ny * p;
                        fz = fz + nz * p;
                    }
                }

                Real bedStiffness = m * cfg.Omega * cfg.Omega;
                Real bedDamping = (Real)2.0 * cfg.Zeta * m * cfg.Omega;

                AgainstTheWorld(cx, cy, cz, radius, vx, vy, vz, m, bedStiffness, bedDamping, w, cfg,
                                ref fx, ref fy, ref fz, ref bedGlass);

                BodyPush(ref fx, ref fy, ref fz, m, cfg);

                if (fx != 0 || fy != 0 || fz != 0)
                {
                    if (links == 1)
                    {
                        Real share = mass[0] * ((Real)1.0 / totalMass);
                        fx = fx * share;
                        fy = fy * share;
                        fz = fz * share;
                    }

                    fext[6 * i + 3] += fx;
                    fext[6 * i + 4] += fy;
                    fext[6 * i + 5] += fz;
                }
            }

            if (instrument)
            {
                gl.NOver[g] = nOver;
                gl.BedGlass[g] = bedGlass;
            }
        }

        /// <summary>
        /// One sphere against the bed (flat, or ContactBed on the height map), the reefs
        /// (ContactReef) and the glass, in Contacts.Apply's order, each counted with the bed and
        /// the glass.
        /// </summary>
        private static void AgainstTheWorld(
            Real cx, Real cy, Real cz, Real r, Real vx, Real vy, Real vz, Real m,
            Real bedStiffness, Real bedDamping, SWorld w, SCfg cfg,
            ref Real fx, ref Real fy, ref Real fz, ref int bedGlass)
        {
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
                Real hgt, gx, gz;
                BedHeightAndGradient(cx, cz, w, cfg, out hgt, out gx, out gz);

                Real floorY = -cfg.BedDepth + hgt;
                Real slope = System.Math.Sqrt((Real)1.0 + gx * gx + gz * gz);
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

            if (cfg.ReefCount != 0)
            {
                SD at;
                Real distance = ReefSignedDistance(cx, cy, cz, r, w, cfg, out at);
                Real penetration = r - distance;

                Real rx = 0, ry = 0, rz = 0;
                if (penetration > 0)
                {
                    Real length = System.Math.Sqrt(at.Gx * at.Gx + at.Gy * at.Gy + at.Gz * at.Gz);
                    Real nx, ny, nz;
                    if (length > (Real)1e-12)
                    {
                        Real inv = (Real)1.0 / length;
                        nx = at.Gx * inv; ny = at.Gy * inv; nz = at.Gz * inv;
                    }
                    else
                    {
                        nx = 0; ny = 1; nz = 0;
                    }

                    Real approach = vx * nx + vy * ny + vz * nz;
                    Real p = PairPush(bedStiffness * penetration - bedDamping * approach, m, approach, cfg);
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
                Real radial = System.Math.Sqrt(x * x + z * z);
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

        /// <summary>ContactLaw.BodyPush: the whole push on one sphere capped at a speed change a step.</summary>
        private static void BodyPush(ref Real fx, ref Real fy, ref Real fz, Real m, SCfg cfg)
        {
            Real cap = cfg.MaxDv;
            if (!(cap > 0)) return;

            Real most = m * cap / cfg.Dt;
            Real magnitude = System.Math.Sqrt(fx * fx + fy * fy + fz * fz);
            if (magnitude > most)
            {
                Real sc = most / magnitude;
                fx = fx * sc; fy = fy * sc; fz = fz * sc;
            }
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

        /// <summary>
        /// Creature.RecordTraceFrame: the ring is strided by the class's link ceiling here and by
        /// the body's own link count on the CPU, which the host's unpack maps.
        /// </summary>
        private static void RecordTraceFrame(
            int i, int links, int maxLinks, int[] dofCount, int[] dofStart, Real[] position, Real[] velocity,
            Real[] spin, Real[] qd, Real[] fext, Real[] water, STrace tr, SCfg cfg)
        {
            int N = cfg.N;
            int bad = -1;
            int poison = tr.Poison[i];

            for (int b = 0; b < links && bad < 0; b++)
            {
                for (int k = 0; k < TraceValues; k++)
                {
                    Real v = k == 6 && b == poison
                        ? Real.NaN
                        : TraceValue(b, k, dofCount, dofStart, position, velocity, spin, qd, fext, water);
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
                    tr.Ring[((slot * maxLinks + b) * TraceValues + k) * N + i] =
                        TraceValue(b, k, dofCount, dofStart, position, velocity, spin, qd, fext, water);
                }
            }

            tr.Step[slot * N + i] = cfg.TraceStep;
            tr.Time[slot * N + i] = cfg.TraceTime;

            // Cleared as it is recorded, so exactly one frame per resize carries the flag.
            tr.Resized[slot * N + i] = tr.Pending[i];
            tr.Pending[i] = 0;

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
                Real best = System.Math.Abs(m[col * 7 + col]);
                for (int row = col + 1; row < 6; row++)
                {
                    Real v = System.Math.Abs(m[row * 7 + col]);
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
}

// ---------------------------------------------------------------------------------------------
// The host side of the double engine: the card's columns, the mirror's pack and unpack, and a
// block of physics steps in the CPU's order (DynamicsWorld.StepOnCpu, then AfterStep).
// ---------------------------------------------------------------------------------------------

namespace Evosim.Farm.Gpu.Dbl
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Evosim.Core;
    using Evosim.Dynamics;
    using Evosim.Farm.Gpu;
    using ILGPU;
    using ILGPU.Runtime;
    using Real = System.Double;

    /// <summary>One size class's columns, strided by the class's capacity.</summary>
    internal sealed class ClassSet : IDisposable
    {
        public const int Constant = 0, Down = 1, Block = 2;

        private readonly Accelerator _acc;
        private readonly List<IColumn> _all = new List<IColumn>();
        private readonly List<IColumn> _down = new List<IColumn>();
        private readonly List<IColumn> _block = new List<IColumn>();

        public readonly int Class, L, D, M;

        public int Cap { get; private set; }

        /// <summary>Every column goes up at the next block: a pack, a growth or the first block.</summary>
        public bool Dirty = true;

        public int Panels { get; private set; } = 1;

        public readonly Col<int> Links, Dof, SigLen, Mask, GSlot, Parent, DofCount, DofStart, PanelStart, ThinAxis,
            NOff, NCnt, BParent, FirstChild, BDofStart, BDofCount;

        public readonly Col<Real> Mass, Lift, Volume, SmallI, Reach, Arm, Inertia, ChildAnchor, ParentAnchor,
            JointFrame, RestFrame, LimitLo, LimitHi, LimitStiff, LimitDamp, Excess, TotalMass;

        public readonly Col<float> TorquePerUnit, PowerScale;

        public readonly Col<Real> PCentre, PNormal, PArea;

        public readonly Col<Real> BasePos, BaseRot, Q, Qd, Tau, LimitImplicit, BallRot, Pos, Rot, RotM, Spin, Vel,
            Sang, Slin, Cbias, RelVel, Water, WaterAcc, Fext, Applied;

        public readonly Col<float> Signal, History, RunSum;
        public readonly Col<int> Cursor, Filled;
        public readonly Col<long> DriveLimited, DragLimited;
        public readonly Col<double> Work, Signed, Dissipated, Passive;

        public readonly Col<int> Op, NIn, Kind, NIndex, Channel;
        public readonly Col<float> Freq, Phase, Amp, Bias, Const, Weight;

        public readonly Col<float> S, Mem, Reserve, Damage;
        public readonly Col<int> Parity, Contact, Gates;
        public readonly Col<double> Clock;

        public readonly Col<Real> Ring;
        public readonly Col<long> TStep, FirstBadStep;
        public readonly Col<double> TTime;
        public readonly Col<int> Enabled, Held, TCursor, FirstBadLink, Resized, Poison, Pending;

        public ClassSet(Accelerator acc, int index, int links, int neurons, int cap)
        {
            _acc = acc;
            Class = index;
            L = links;
            D = 3 * links;
            M = neurons;
            Cap = Math.Max(1, cap);

            Links = C<int>(1, Constant); Dof = C<int>(1, Constant); SigLen = C<int>(1, Constant);
            Mask = C<int>(1, Constant); GSlot = C<int>(1, Block, -1);
            Parent = C<int>(L, Constant); DofCount = C<int>(L, Constant); DofStart = C<int>(L, Constant);
            PanelStart = C<int>(L + 1, Constant); ThinAxis = C<int>(L, Constant);
            NOff = C<int>(L, Constant); NCnt = C<int>(L, Constant); BParent = C<int>(L, Constant);
            FirstChild = C<int>(L, Constant); BDofStart = C<int>(L, Constant); BDofCount = C<int>(L, Constant);

            Mass = C<Real>(L, Constant); Lift = C<Real>(L, Constant); Volume = C<Real>(L, Constant);
            SmallI = C<Real>(L, Constant); Reach = C<Real>(L, Constant); Arm = C<Real>(L, Constant);
            Inertia = C<Real>(3 * L, Constant); ChildAnchor = C<Real>(3 * L, Constant);
            ParentAnchor = C<Real>(3 * L, Constant);
            JointFrame = C<Real>(4 * L, Constant); RestFrame = C<Real>(4 * L, Constant);
            LimitLo = C<Real>(D, Constant); LimitHi = C<Real>(D, Constant);
            LimitStiff = C<Real>(D, Constant); LimitDamp = C<Real>(D, Constant);
            Excess = C<Real>(1, Constant); TotalMass = C<Real>(1, Constant);
            TorquePerUnit = C<float>(D, Constant); PowerScale = C<float>(1, Block);

            PCentre = C<Real>(3, Constant); PNormal = C<Real>(3, Constant); PArea = C<Real>(1, Constant);

            BasePos = C<Real>(3, Down); BaseRot = C<Real>(4, Down);
            Q = C<Real>(D, Down); Qd = C<Real>(D, Down); Tau = C<Real>(D, Down); LimitImplicit = C<Real>(D, Down);
            BallRot = C<Real>(4 * L, Down); Pos = C<Real>(3 * L, Down); Rot = C<Real>(4 * L, Down);
            RotM = C<Real>(9 * L, Down); Spin = C<Real>(3 * L, Down); Vel = C<Real>(3 * L, Down);
            Sang = C<Real>(9 * L, Down); Slin = C<Real>(9 * L, Down); Cbias = C<Real>(6 * L, Down);
            RelVel = C<Real>(3 * L, Down); Water = C<Real>(3 * L, Down); WaterAcc = C<Real>(3 * L, Down);
            Fext = C<Real>(6 * L, Down); Applied = C<Real>(3 * L, Down);

            Signal = C<float>(D, Down); History = C<float>(D * WholeStep.Window, Down); RunSum = C<float>(D, Down);
            Cursor = C<int>(1, Down); Filled = C<int>(1, Down);
            DriveLimited = C<long>(1, Down); DragLimited = C<long>(1, Down);
            Work = C<double>(1, Down); Signed = C<double>(1, Down);
            Dissipated = C<double>(1, Down); Passive = C<double>(1, Down);

            Op = C<int>(M, Constant); NIn = C<int>(M, Constant);
            Kind = C<int>(3 * M, Constant); NIndex = C<int>(3 * M, Constant); Channel = C<int>(3 * M, Constant);
            Freq = C<float>(M, Constant); Phase = C<float>(M, Constant); Amp = C<float>(M, Constant);
            Bias = C<float>(M, Constant); Const = C<float>(3 * M, Constant); Weight = C<float>(3 * M, Constant);

            S = C<float>(2 * M, Down); Mem = C<float>(M, Down);
            Reserve = C<float>(1, Block); Damage = C<float>(L, Block);
            Parity = C<int>(1, Down); Contact = C<int>(L, Block); Gates = C<int>(1, Block);
            Clock = C<double>(1, Down);

            int frames = WholeStep.TraceFrames;
            Ring = C<Real>(frames * L * WholeStep.TraceValues, Down);
            TStep = C<long>(frames, Down); FirstBadStep = C<long>(1, Down, -1L);
            TTime = C<double>(frames, Down);
            Enabled = C<int>(1, Block); Held = C<int>(1, Down); TCursor = C<int>(1, Down);
            FirstBadLink = C<int>(1, Down, -1); Resized = C<int>(frames, Down);
            Poison = C<int>(1, Block, -1); Pending = C<int>(1, Block | Down);
        }

        private Col<T> C<T>(int width, int kind, T fill = default) where T : unmanaged
        {
            var column = new Col<T>(_acc, width, Cap, fill);
            _all.Add(column);
            if ((kind & Down) != 0) _down.Add(column);
            if ((kind & Block) != 0) _block.Add(column);
            return column;
        }

        public long Bytes
        {
            get
            {
                long total = 0;
                foreach (IColumn c in _all) total += c.Bytes;
                return total;
            }
        }

        /// <summary>Room for <paramref name="used"/> slots: every column re-laid, and all of it goes up.</summary>
        public void Ensure(int used)
        {
            if (used <= Cap) return;

            int cap = Math.Max(used, 2 * Cap);
            foreach (IColumn c in _all) c.Resize(cap);
            Cap = cap;
            Dirty = true;
        }

        /// <summary>Room for a body of <paramref name="panels"/> drag panels.</summary>
        public void WidenPanels(int panels)
        {
            if (panels <= Panels) return;

            int width = Math.Max(panels, Panels + Panels / 2);
            PCentre.Widen(3 * width);
            PNormal.Widen(3 * width);
            PArea.Widen(width);
            Panels = width;
            Dirty = true;
        }

        public void Upload()
        {
            List<IColumn> columns = Dirty ? _all : _block;
            foreach (IColumn c in columns) c.Up();
            Dirty = false;
        }

        public void Download()
        {
            foreach (IColumn c in _down) c.Down();
        }

        public STopo Topo => new STopo
        {
            Links = Links.View, Dof = Dof.View, SigLen = SigLen.View, Mask = Mask.View, GSlot = GSlot.View,
            Parent = Parent.View, DofCount = DofCount.View, DofStart = DofStart.View, PanelStart = PanelStart.View,
            ThinAxis = ThinAxis.View, NOff = NOff.View, NCnt = NCnt.View, BParent = BParent.View,
            FirstChild = FirstChild.View, BDofStart = BDofStart.View, BDofCount = BDofCount.View,
        };

        public SConst ConstSet => new SConst
        {
            Mass = Mass.View, Lift = Lift.View, Volume = Volume.View, SmallI = SmallI.View, Reach = Reach.View,
            Arm = Arm.View, Inertia = Inertia.View, ChildAnchor = ChildAnchor.View, ParentAnchor = ParentAnchor.View,
            JointFrame = JointFrame.View, RestFrame = RestFrame.View, LimitLo = LimitLo.View, LimitHi = LimitHi.View,
            LimitStiff = LimitStiff.View, LimitDamp = LimitDamp.View, Excess = Excess.View,
            TotalMass = TotalMass.View, TorquePerUnit = TorquePerUnit.View, PowerScale = PowerScale.View,
        };

        public SPanels PanelSet => new SPanels { Centre = PCentre.View, Normal = PNormal.View, Area = PArea.View };

        public SState StateSet => new SState
        {
            BasePos = BasePos.View, BaseRot = BaseRot.View, Q = Q.View, Qd = Qd.View, Tau = Tau.View,
            LimitImplicit = LimitImplicit.View, BallRot = BallRot.View, Pos = Pos.View, Rot = Rot.View,
            RotM = RotM.View, Spin = Spin.View, Vel = Vel.View, Sang = Sang.View, Slin = Slin.View,
            Cbias = Cbias.View, RelVel = RelVel.View, Water = Water.View, WaterAcc = WaterAcc.View,
            Fext = Fext.View, Applied = Applied.View,
        };

        public SDrive DriveSet => new SDrive
        {
            Signal = Signal.View, History = History.View, RunSum = RunSum.View, Cursor = Cursor.View,
            Filled = Filled.View, DriveLimited = DriveLimited.View, DragLimited = DragLimited.View,
            Work = Work.View, Signed = Signed.View, Dissipated = Dissipated.View, Passive = Passive.View,
        };

        public SNeur NeurSet => new SNeur
        {
            Op = Op.View, NIn = NIn.View, Kind = Kind.View, Index = NIndex.View, Channel = Channel.View,
            Freq = Freq.View, Phase = Phase.View, Amp = Amp.View, Bias = Bias.View, Const = Const.View,
            Weight = Weight.View,
        };

        public SBrain BrainSet => new SBrain
        {
            S = S.View, Mem = Mem.View, Reserve = Reserve.View, Damage = Damage.View, Parity = Parity.View,
            Contact = Contact.View, Gates = Gates.View, Clock = Clock.View,
        };

        public STrace TraceSet => new STrace
        {
            Ring = Ring.View, Step = TStep.View, FirstBadStep = FirstBadStep.View, Time = TTime.View,
            Enabled = Enabled.View, Held = Held.View, Cursor = TCursor.View, FirstBadLink = FirstBadLink.View,
            Resized = Resized.View, Poison = Poison.View, Pending = Pending.View,
        };

        public void Dispose()
        {
            foreach (IColumn c in _all) c.Dispose();
        }
    }

    /// <summary>
    /// The double engine's host: the mirror's pack and unpack and one block of steps
    /// (logbook/specs/gpu-port-spec.md sections 2, 4 and 5).
    /// </summary>
    internal sealed class Runner : IGpuRunner
    {
        private readonly Accelerator _acc;
        private readonly GpuWorld _w;
        private readonly GpuOptions _o;
        private readonly GpuSlots _slots;
        private readonly ClassSet[] _classes;
        private readonly bool _onCpu;
        private readonly int _scanGroup;
        private readonly int _chunk = 256;

        public int GroupSize { get; }

        public double CompileMs { get; }

        public long[] Overflow { get; } = new long[4];

        public long RefusedForClass => _slots.RefusedForClass;

        public long NeuronsAtCeiling { get; private set; }

        public long NeuronsAtCeilingMost { get; private set; }

        // ---- the globals, by global slot
        private int _gcap = 16;
        private readonly List<IColumn> _gBody = new List<IColumn>();
        private readonly List<IColumn> _gLink = new List<IColumn>();
        private readonly List<IColumn> _gDown = new List<IColumn>();

        private readonly Col<int> _gLinks, _gId, _gRank, _gAlive, _gJointed;
        private readonly Col<int> _nOver, _ovSlot, _ovId, _ovPart, _ovHeld, _nHeld, _heldId, _bedGlass;
        private readonly Col<Real> _gTotalMass, _linkMass;
        private readonly Col<int> _rankToSlot;
        private readonly Col<long> _census, _overflow;

        // ---- two sphere sets: [set][Cx, Cy, Cz, R, Vx, Vy, Vz, LCx, LCy, LCz, LR, LVx, LVy, LVz]
        private readonly Col<Real>[][] _sph = new Col<Real>[2][];
        private readonly Col<int>[] _active = new Col<int>[2];
        private int _committed;

        // ---- the grid
        private readonly Scratch<int> _lo, _hi, _counts, _start, _gcursor, _items, _partialN;
        private readonly Scratch<Real> _cell, _partial;
        private int _entryCap;
        private readonly int[] _one = new int[1];

        // ---- the world
        private readonly Col<Real> _inst, _wr, _stock;
        private readonly Col<int> _wi;
        private int _fieldVersion = -1;
        private readonly double[] _instD = new double[GpuWorld.InstStride];
        private double[] _stockD = new double[1];

        // ---- the kernels
        private readonly Action<Index1D, SSph, SGlob, SGrid, SCfg> _meanSerial, _meanChunks, _ranges;
        private readonly Action<Index1D, SGrid, SCfg> _meanCombine;
        private readonly Action<KernelConfig, SGrid, SCfg> _scan;
        private readonly Action<Index1D, SGlob, SGrid, SCfg> _scatter;
        private readonly Action<Index1D, SGlob, SCfg> _censusKernel;
        private readonly Action<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain, STrace, SGlob,
            SSph, SSph, SGrid, SWorld, SCfg>[] _step;

        // EVOSIM_GPU_CONCURRENT: the same step kernels in their stream-taking form and a stream a
        // class. The grid is joined before the classes are launched and every class stream after,
        // so a class sees the grid it always saw and the census sees every class done.
        private readonly Action<AcceleratorStream, Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain, STrace,
            SGlob, SSph, SSph, SGrid, SWorld, SCfg>[] _stepOn;
        private readonly AcceleratorStream[] _streams;
        private readonly bool _concurrent;

        private SCfg _base;

        [ThreadStatic] private static long[] _idScratch;
        [ThreadStatic] private static int[] _partScratch;

        public Runner(Accelerator accelerator, GpuWorld world, GpuOptions options)
        {
            _acc = accelerator;
            _w = world;
            _o = options;
            _onCpu = accelerator.AcceleratorType == AcceleratorType.CPU;
            _slots = new GpuSlots(options.ClassLinks, options.ClassNeurons);

            _classes = new ClassSet[options.ClassLinks.Length];
            for (int k = 0; k < _classes.Length; k++)
            {
                _classes[k] = new ClassSet(accelerator, k, options.ClassLinks[k], options.ClassNeurons[k], 16);
            }

            int oc = options.OverlapCap;
            _gLinks = G<int>(_gBody, 1, true); _gId = G<int>(_gBody, 1, false); _gRank = G<int>(_gBody, 1, false);
            _gAlive = G<int>(_gBody, 1, true); _gJointed = G<int>(_gBody, 1, false);
            _gTotalMass = G<Real>(_gBody, 1, false);
            _linkMass = G<Real>(_gLink, 1, false);
            _nOver = G<int>(_gBody, 1, true); _ovSlot = G<int>(_gBody, oc, true, -1); _ovId = G<int>(_gBody, oc, true);
            _ovPart = G<int>(_gBody, 2 * oc, true, -1); _ovHeld = G<int>(_gBody, oc, true);
            _nHeld = G<int>(_gBody, 1, true); _heldId = G<int>(_gBody, oc, true); _bedGlass = G<int>(_gBody, 1, true);

            _rankToSlot = new Col<int>(_acc, 1, 16, -1);
            _census = new Col<long>(_acc, 1, WholeStep.CensusFields * GpuBackend.BlockCeiling);
            _overflow = new Col<long>(_acc, 1, 4);

            for (int set = 0; set < 2; set++)
            {
                var columns = new Col<Real>[14];
                for (int k = 0; k < 7; k++) columns[k] = new Col<Real>(_acc, 1, _gcap);
                for (int k = 7; k < 14; k++) columns[k] = new Col<Real>(_acc, 1, WholeStep.LinkStride * _gcap);
                _sph[set] = columns;
                _active[set] = new Col<int>(_acc, 1, _gcap);
            }

            _lo = new Scratch<int>(_acc, 3 * 16);
            _hi = new Scratch<int>(_acc, 3 * 16);
            _counts = new Scratch<int>(_acc, 17);
            _start = new Scratch<int>(_acc, 17);
            _gcursor = new Scratch<int>(_acc, 17);
            _entryCap = 1024;
            _items = new Scratch<int>(_acc, _entryCap);
            _partial = new Scratch<Real>(_acc, 1);
            _partialN = new Scratch<int>(_acc, 1);
            _cell = new Scratch<Real>(_acc, 1);

            _inst = new Col<Real>(_acc, 1, GpuWorld.InstStride * GpuBackend.BlockCeiling);
            _wr = new Col<Real>(_acc, 1, world.WR.Length);
            for (int k = 0; k < world.WR.Length; k++) _wr.Host[k] = (Real)world.WR[k];
            _wr.Up();
            _wi = new Col<int>(_acc, 1, 1);
            _stock = new Col<Real>(_acc, 1, 1);

            _scanGroup = Math.Max(1, Math.Min(1024, accelerator.MaxNumThreadsPerGroup));

            _base = BaseConfig(world, options);

            // ---- the kernels: every launch shape compiled before the first step is timed.
            var watch = Stopwatch.StartNew();
            bool grouped = !_onCpu && options.GroupSize > 0;
            int g = options.GroupSize;
            GroupSize = _onCpu ? accelerator.MaxNumThreadsPerGroup : g;

            _meanSerial = _acc.LoadAutoGroupedStreamKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.MeanSerial);
            _meanCombine = _acc.LoadAutoGroupedStreamKernel<Index1D, SGrid, SCfg>(WholeStep.MeanCombine);
            _scan = _acc.LoadStreamKernel<SGrid, SCfg>(WholeStep.Scan);

            if (grouped)
            {
                _meanChunks = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.MeanChunks, g);
                _ranges = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.Ranges, g);
                _scatter = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, SGlob, SGrid, SCfg>(WholeStep.Scatter, g);
                _censusKernel = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, SGlob, SCfg>(WholeStep.Census, g);
            }
            else
            {
                _meanChunks = _acc.LoadAutoGroupedStreamKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.MeanChunks);
                _ranges = _acc.LoadAutoGroupedStreamKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.Ranges);
                _scatter = _acc.LoadAutoGroupedStreamKernel<Index1D, SGlob, SGrid, SCfg>(WholeStep.Scatter);
                _censusKernel = _acc.LoadAutoGroupedStreamKernel<Index1D, SGlob, SCfg>(WholeStep.Census);
            }

            _step = new Action<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain, STrace, SGlob,
                SSph, SSph, SGrid, SWorld, SCfg>[KernelGenerator.ClassCount];
            _concurrent = options.Concurrent;
            _stepOn = new Action<AcceleratorStream, Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain, STrace,
                SGlob, SSph, SSph, SGrid, SWorld, SCfg>[KernelGenerator.ClassCount];
            _streams = new AcceleratorStream[KernelGenerator.ClassCount];

            _step[0] = grouped
                ? _acc.LoadImplicitlyGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step0, g)
                : _acc.LoadAutoGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step0);
            if (_concurrent)
            {
                _stepOn[0] = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step0, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step0);
                _streams[0] = _acc.CreateStream();
            }
            _step[1] = grouped
                ? _acc.LoadImplicitlyGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step1, g)
                : _acc.LoadAutoGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step1);
            if (_concurrent)
            {
                _stepOn[1] = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step1, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step1);
                _streams[1] = _acc.CreateStream();
            }
            _step[2] = grouped
                ? _acc.LoadImplicitlyGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step2, g)
                : _acc.LoadAutoGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step2);
            if (_concurrent)
            {
                _stepOn[2] = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step2, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step2);
                _streams[2] = _acc.CreateStream();
            }
            _step[3] = grouped
                ? _acc.LoadImplicitlyGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step3, g)
                : _acc.LoadAutoGroupedStreamKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step3);
            if (_concurrent)
            {
                _stepOn[3] = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step3, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step3);
                _streams[3] = _acc.CreateStream();
            }

            _acc.Synchronize();
            CompileMs = watch.Elapsed.TotalMilliseconds;
        }

        private Col<T> G<T>(List<IColumn> list, int width, bool down, T fill = default) where T : unmanaged
        {
            int cap = ReferenceEquals(list, _gLink) ? WholeStep.LinkStride * _gcap : _gcap;
            var column = new Col<T>(_acc, width, cap, fill);
            list.Add(column);
            if (down) _gDown.Add(column);
            return column;
        }

        private static SCfg BaseConfig(GpuWorld w, GpuOptions o)
        {
            return new SCfg
            {
                OverCap = o.OverlapCap,
                PerPart = w.PerPart ? 1 : 0,

                HasCurrent = w.HasCurrent ? 1 : 0, Accelerating = w.Accelerating ? 1 : 0, Sloped = w.Sloped ? 1 : 0,
                HasBed = w.HasBed ? 1 : 0, BedRelief = w.BedRelief ? 1 : 0, BedModes = w.BedModes,
                ShoreOn = w.ShoreOn ? 1 : 0, FadeOn = w.FadeOn ? 1 : 0, ReefCount = w.ReefCount,
                LimitDrive = w.LimitDrive ? 1 : 0, LimitDrag = w.LimitDrag ? 1 : 0,
                UseRestore = w.UseRestore ? 1 : 0, FloorRestores = w.FloorRestores ? 1 : 0,
                CreatureContact = w.CreatureContact ? 1 : 0, OffsetTorque = w.OffsetTorque ? 1 : 0,

                DtD = w.Dt,
                Dt = (Real)w.Dt, Density = (Real)w.Density, DragK = (Real)w.DragK, AddedMass = (Real)w.AddedMass,
                FluidAccel = (Real)w.FluidAccel, Gravity = (Real)w.Gravity, WorldDepth = (Real)w.WorldDepth,
                Damping = (Real)w.Damping, SpinBudget = (Real)w.SpinBudget,
                RestoringDensity = (Real)w.RestoringDensity, TissueDensity = (Real)w.TissueDensity,

                Omega = (Real)w.Omega, Zeta = (Real)w.Zeta, MaxSep = (Real)w.MaxSep, MaxDv = (Real)w.MaxDv,
                TankRadius = (Real)w.TankRadius, AxisX = (Real)w.AxisX, AxisZ = (Real)w.AxisZ, CellOverride = 0,
                BedRadius = (Real)w.BedRadius, BedDepth = (Real)w.BedDepth, TiltX = (Real)w.TiltX,
                TiltZ = (Real)w.TiltZ, Offset = (Real)w.Offset, ShoalHeight = (Real)w.ShoalHeight,

                CurDepth = (Real)w.CurDepth, CurTankR = (Real)w.CurTankR, EddyWeight = (Real)w.EddyWeight,
                Overturning = (Real)w.Overturning, PerSecond = (Real)w.PerSecond, Squared = (Real)w.Squared,
                ShoreDepth = (Real)w.ShoreDepth, ShoreFadeW = (Real)w.ShoreFadeW,
                HalfThickness = (Real)w.HalfThickness, Fillet = (Real)w.Fillet, ReefFade = (Real)w.ReefFade,
                SigmaSloped = w.SigmaSloped, SigmaFlat = w.SigmaFlat, Speed = w.Speed,

                SWorldDepth = w.SWorldDepth, ChemHalf = w.ChemHalf, EnergyScale = w.EnergyScale,
                FlowScale = w.FlowScale, RateScale = w.RateScale, ConstCE = w.ConstCE, DtF = w.DtF,

                StreamAmpAt = w.StreamAmpAt, StreamRateAt = w.StreamRateAt, CellAmpAt = w.CellAmpAt,
                BedAt = w.BedAt, ReefAt = w.ReefAt,
            };
        }

        // ============================================================== the block

        public void Block(DynamicsWorld world, int steps)
        {
            int done = 0;
            while (done < steps)
            {
                // A digest row is written from the mirror, so a block ends on every step that writes one.
                int n = steps - done;
                for (int k = 1; k <= n; k++)
                {
                    if (world.DigestDue(world.Steps + k))
                    {
                        n = k;
                        break;
                    }
                }

                Run(world, n);
                done += n;
            }
        }

        // ---- the probe (EVOSIM_GPU_PROBE=1): a diagnostic, never on in a scored run. It joins the
        // card after the grid kernels, after each class's launch and after the census, so each
        // part's time is its own, and every twenty blocks it prints those times a step beside the
        // contact grid's geometry read from the mirror: the cell, the largest link and the most
        // cells one link of each class covers. The joins cost a little; the step's numbers do not
        // move, because nothing it launches changes.
        private static readonly bool ProbeOn = Environment.GetEnvironmentVariable("EVOSIM_GPU_PROBE") == "1";
        private readonly long[] _probeClass = new long[4];
        private long _probeGrid, _probeTail, _probeSteps;
        private int _probeBlocks;

        private void PrintProbe(DynamicsWorld world, IReadOnlyList<Creature> list)
        {
            double sum = 0, largest = 0;
            int active = 0;
            for (int r = 0; r < list.Count; r++)
            {
                Creature c = list[r];
                if (!c.Alive || !c.ContactActive) continue;
                for (int i = 0; i < c.Links; i++)
                {
                    double rad = c.LinkContactRadius[i];
                    sum += rad;
                    active++;
                    if (rad > largest) largest = rad;
                }
            }

            double mean = active > 0 ? sum / active : 0;
            double cell = world.ContactCellOverrideMetres > 0 ? world.ContactCellOverrideMetres : (mean > 0.125 ? 2 * mean : 0.25);
            var used = new int[4];
            var most = new long[4];
            var entries = new long[4];
            for (int r = 0; r < list.Count; r++)
            {
                Creature c = list[r];
                int g = r < _slots.RankToSlot.Length ? _slots.RankToSlot[r] : -1;
                if (g < 0) continue;
                int k = _slots.ClassOf[g];
                used[k]++;
                if (!c.Alive || !c.ContactActive) continue;
                for (int i = 0; i < c.Links; i++)
                {
                    double rad = c.LinkContactRadius[i];
                    long n = 1;
                    for (int a = 0; a < 3; a++)
                    {
                        double x = c.LinkContactCentre[3 * i + a];
                        n *= (long)(Math.Floor((x + rad) / cell) - Math.Floor((x - rad) / cell) + 1);
                    }
                    entries[k] += n;
                    if (n > most[k]) most[k] = n;
                }
            }

            double f = 1000.0 / Stopwatch.Frequency / Math.Max(1, _probeSteps);
            Console.Error.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "gpu-probe" + (_concurrent ? " concurrent (the classes' time together in the first)" : "") +
                " t={0:0} s, ms a step: grid {1:0.000}, classes {2:0.000} / {3:0.000} / {4:0.000} / {5:0.000}, tail {6:0.000}; " +
                "bodies a class {7} / {8} / {9} / {10}; cell {11:0.000} m (mean link {12:0.000} m), largest link {13:0.000} m; " +
                "most cells one link covers, a class {14} / {15} / {16} / {17}; entries a class {18} / {19} / {20} / {21}",
                world.ElapsedSeconds, _probeGrid * f, _probeClass[0] * f, _probeClass[1] * f, _probeClass[2] * f, _probeClass[3] * f,
                _probeTail * f, used[0], used[1], used[2], used[3], cell, mean, largest,
                most[0], most[1], most[2], most[3], entries[0], entries[1], entries[2], entries[3]));

            _probeGrid = _probeTail = _probeSteps = 0;
            for (int k = 0; k < 4; k++) _probeClass[k] = 0;
        }

        private void Run(DynamicsWorld world, int steps)

        {
            long t0 = Stopwatch.GetTimestamp();
            IReadOnlyList<Creature> list = world.Creatures;
            _list = list;
            int threads = Math.Max(1, world.Threads);
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = threads };

            // ---- 1. slots, refusals and room
            _slots.Sync(list, _o.Resync);

            foreach (Creature refused in _slots.Refused)
            {
                // Not stepped, so not in anyone's contact set: the CPU's lost body. The farm kills
                // it at its next divergence check (Simulation.CheckFinite's gpu clause).
                refused.Alive = false;
                refused.MarkLost();
                refused.CommitContactSphere();
            }

            EnsureGlobal(_slots.Used);
            if (_rankToSlot.Cap < Math.Max(1, list.Count)) _rankToSlot.Resize(Math.Max(list.Count, 2 * _rankToSlot.Cap));
            for (int k = 0; k < _classes.Length; k++) _classes[k].Ensure(_slots.Classes[k].Used);

            if (_census.Cap < WholeStep.CensusFields * steps) _census.Resize(WholeStep.CensusFields * steps);
            if (_inst.Cap < GpuWorld.InstStride * steps) _inst.Resize(GpuWorld.InstStride * steps);

            // ---- 2. freed slots, then new and resized bodies
            foreach ((int g, int k, int local) in _slots.Freed)
            {
                _classes[k].GSlot.Host[local] = -1;
                _gLinks.Host[g] = 0;
                _gAlive.Host[g] = 0;
                _nOver.Host[g] = 0;
                _nHeld.Host[g] = 0;
                _active[_committed].Host[g] = 0;
                _sph[_committed][3].Host[g] = 0;
            }

            for (int r = 0; r < list.Count; r++)
            {
                Creature c = list[r];
                if (c.Senses.Nutrients != null) _w.AttachField(c.Senses.Nutrients);
            }

            foreach (int g in _slots.Packs)
            {
                ClassSet set = _classes[_slots.ClassOf[g]];
                set.Dirty = true;
                set.WidenPanels(_slots.Body[g].PanelStart[_slots.Body[g].Links]);
            }

            Parallel.For(0, _slots.Packs.Count, parallel, p => Pack(_slots.Packs[p]));

            // ---- 3. what every body carries into every block, and the roster
            Parallel.For(0, _slots.Used, parallel, g =>
            {
                if (_slots.Body[g] != null) PackBlock(g);
            });

            for (int r = 0; r < list.Count; r++) _rankToSlot.Host[r] = _slots.RankToSlot[r];

            // ---- 4. the world: the snow as it stands, the instants of this block
            if (_w.FieldVersion != _fieldVersion)
            {
                _wi.Resize(Math.Max(_w.WI.Length, 1));
                Array.Copy(_w.WI, _wi.Host, _w.WI.Length);
                _wi.Up();
                _fieldVersion = _w.FieldVersion;

                _base.Nx = _w.Nx; _base.Ny = _w.Ny; _base.Nz = _w.Nz; _base.LayerCount = _w.LayerCount;
                _base.RefugeLayers = _w.RefugeLayers; _base.LayerStride = _w.LayerStride;
                _base.CellMetres = _w.CellMetres; _base.FieldTankR = _w.FieldTankR;
                _base.RefugeFraction = _w.RefugeFraction; _base.CellVolume = _w.CellVolume;
                _base.SplitColumns = _w.SplitColumns ? 1 : 0;
                _base.LowestAt = _w.LowestAt; _base.IntervalFirstAt = _w.IntervalFirstAt;
                _base.IntervalTopAt = _w.IntervalTopAt; _base.IntervalBottomAt = _w.IntervalBottomAt;
            }

            int stockLength = _w.StockLength;
            if (_stockD.Length != stockLength) _stockD = new double[stockLength];
            if (_stock.Cap != stockLength) _stock.Resize(stockLength);
            _w.ReadStock(_stockD);
            for (int k = 0; k < stockLength; k++) _stock.Host[k] = (Real)_stockD[k];

            double dt = world.Config.StepSeconds;
            double seconds = world.ElapsedSeconds;
            for (int k = 0; k < steps; k++)
            {
                _w.Instant(seconds, _instD, 0);
                int at = k * GpuWorld.InstStride;
                for (int v = 0; v < GpuWorld.InstStride; v++) _inst.Host[at + v] = (Real)_instD[v];
                seconds += dt;
            }

            Array.Clear(_census.Host, 0, _census.Host.Length);

            // ---- 5. up
            foreach (IColumn c in _gBody) c.Up();
            foreach (IColumn c in _gLink) c.Up();
            _rankToSlot.Up();
            _census.Up();
            _overflow.Up();
            foreach (Col<Real> c in _sph[_committed]) c.Up();
            _active[_committed].Up();
            for (int k = 0; k < _classes.Length; k++) _classes[k].Upload();
            _stock.Up();
            _inst.Up();

            // ---- 6. the grid's room
            int gUsed = _slots.Used;
            int rows = _base.PerPart != 0 ? WholeStep.LinkStride * gUsed : gUsed;
            int buckets = 16;
            while (buckets < 2 * rows) buckets <<= 1;

            _lo.Ensure(3L * Math.Max(1, rows));
            _hi.Ensure(3L * Math.Max(1, rows));
            _counts.Ensure(buckets + 1);
            _start.Ensure(buckets + 1);
            _gcursor.Ensure(buckets + 1);
            if (_entryCap < 16 * rows) _entryCap = 16 * rows;
            _items.Ensure(_entryCap);

            int chunks = Math.Max(1, (list.Count + _chunk - 1) / _chunk);
            _partial.Ensure(chunks);
            _partialN.Ensure(chunks);

            SCfg cfg = _base;
            cfg.GN = _gcap;
            cfg.GUsed = gUsed;
            cfg.Count = list.Count;
            cfg.Rows = rows;
            cfg.Buckets = buckets;
            cfg.Mask = buckets - 1;
            cfg.EntryCap = _entryCap;
            cfg.Chunk = _chunk;
            cfg.Chunks = chunks;
            cfg.CellOverride = (Real)world.ContactCellOverrideMetres;

            // The farm switches the instrument on after it builds the solver's config, so it is
            // read from the world being stepped and not from the one the kernels were built for.
            cfg.Instrument = world.Config.ContactInstrument ? 1 : 0;

            SGlob gl = Glob;
            SGrid grid = Grid;
            SWorld sw = new SWorld { Inst = _inst.View, WR = _wr.View, Stock = _stock.View, WI = _wi.View };

            long t1 = Stopwatch.GetTimestamp();

            // ---- 7. the steps
            long steps0 = world.Steps;
            bool probe = ProbeOn;
            long pt = probe ? Stopwatch.GetTimestamp() : 0;
            bool serial = _o.SerialMean;
            bool instrument = world.Config.ContactInstrument;

            for (int k = 0; k < steps; k++)
            {
                cfg.InstBase = k * GpuWorld.InstStride;
                cfg.StepIndex = k;
                cfg.TraceStep = steps0 + k + 1;
                cfg.TraceTime = (steps0 + k + 1) * dt;

                SSph committed = Sph(_committed), pending = Sph(1 - _committed);

                _counts.View.MemSetToZero();
                if (rows > 0)
                {
                    if (serial)
                    {
                        _meanSerial(1, committed, gl, grid, cfg);
                    }
                    else
                    {
                        _meanChunks(chunks, committed, gl, grid, cfg);
                        _meanCombine(1, grid, cfg);
                    }

                    _ranges(rows, committed, gl, grid, cfg);
                    _scan(new KernelConfig(1, _scanGroup), grid, cfg);
                    _scatter(rows, gl, grid, cfg);
                }
                if (probe) { _acc.Synchronize(); long pn = Stopwatch.GetTimestamp(); _probeGrid += pn - pt; pt = pn; }

                if (_concurrent)
                {
                    // Every class reads the grid the default stream just built, so the grid is
                    // joined first; the census reads every class's pending spheres, so each class
                    // stream is joined after. In between the classes run at once.
                    _acc.DefaultStream.Synchronize();
                    for (int c = 0; c < _classes.Length; c++)
                    {
                        int used = _slots.Classes[c].Used;
                        if (used == 0) continue;

                        ClassSet set = _classes[c];
                        SCfg cc = cfg;
                        cc.N = set.Cap;
                        cc.MaxN = set.M;

                        _stepOn[c](_streams[c], used, set.Topo, set.ConstSet, set.PanelSet, set.StateSet, set.DriveSet,
                                   set.NeurSet, set.BrainSet, set.TraceSet, gl, committed, pending, grid, sw, cc);
                    }
                    for (int c = 0; c < _classes.Length; c++)
                        if (_slots.Classes[c].Used != 0) _streams[c].Synchronize();
                    if (probe) { long pn = Stopwatch.GetTimestamp(); _probeClass[0] += pn - pt; pt = pn; }
                }
                else
                {
                    for (int c = 0; c < _classes.Length; c++)
                    {
                        int used = _slots.Classes[c].Used;
                        if (used == 0) continue;

                        ClassSet set = _classes[c];
                        SCfg cc = cfg;
                        cc.N = set.Cap;
                        cc.MaxN = set.M;

                        _step[c](used, set.Topo, set.ConstSet, set.PanelSet, set.StateSet, set.DriveSet, set.NeurSet,
                                 set.BrainSet, set.TraceSet, gl, committed, pending, grid, sw, cc);
                        if (probe) { _acc.Synchronize(); long pn = Stopwatch.GetTimestamp(); _probeClass[c] += pn - pt; pt = pn; }
                    }
                }

                if (instrument && gUsed > 0) _censusKernel(gUsed, gl, cfg);
                if (probe) { _acc.Synchronize(); long pn = Stopwatch.GetTimestamp(); _probeTail += pn - pt; pt = pn; }

                _committed = 1 - _committed;
            }

            if (probe)
            {
                _probeSteps += steps;
                if (++_probeBlocks % 20 == 0) PrintProbe(world, list);
            }

            _acc.Synchronize();
            long t2 = Stopwatch.GetTimestamp();

            // ---- 8. down
            for (int k = 0; k < _classes.Length; k++)
            {
                if (_slots.Classes[k].Used > 0) _classes[k].Download();
            }

            foreach (IColumn c in _gDown) c.Down();
            foreach (Col<Real> c in _sph[_committed]) c.Down();
            _active[_committed].Down();
            _census.Down();
            _overflow.Down();

            if (rows > 0)
            {
                _start.View.SubView(buckets, 1).CopyToCPU(_one);
                if (_one[0] > _entryCap / 2) _entryCap = 2 * _one[0];
            }

            long t3 = Stopwatch.GetTimestamp();

            // ---- 9. the mirror, the census, the clock
            long ceiling = 0;
            Parallel.For(0, _slots.Used, parallel,
                () => 0L,
                (g, _, local) => _slots.Body[g] != null ? local + Unpack(g, instrument) : local,
                local => Interlocked.Add(ref ceiling, local));

            NeuronsAtCeiling = ceiling;
            if (ceiling > NeuronsAtCeilingMost) NeuronsAtCeilingMost = ceiling;

            for (int k = 0; k < 4; k++) Overflow[k] = _overflow.Host[k];

            if (instrument)
            {
                // CommitOverlaps for a body the card never held: its list is its last one.
                for (int r = 0; r < list.Count; r++)
                {
                    if (_slots.RankToSlot[r] >= 0) continue;
                    Creature c = list[r];
                    CommitOwnList(c);
                }
            }

            for (int k = 0; k < steps; k++)
            {
                world.BackendAdvanceClock();

                if (instrument)
                {
                    int at = k * WholeStep.CensusFields;
                    world.BackendCensus(_census.Host[at], _census.Host[at + 1], _census.Host[at + 2],
                                        _census.Host[at + 3], _census.Host[at + 4]);
                }
                else
                {
                    world.BackendCensus(0, 0, 0, 0, 0);
                }
            }

            BuildEvents(world, list, instrument);

            if (world.DigestDue(world.Steps)) world.BackendWriteDigestRow();

            long t4 = Stopwatch.GetTimestamp();

            // The five phases, as the CPU names them: the pack and the uploads stand for the grid,
            // the kernels for the bodies, the download for the commit, the rest for the tail.
            world.BackendPhaseTicks(0, t1 - t0);
            world.BackendPhaseTicks(2, t2 - t1);
            world.BackendPhaseTicks(3, t3 - t2);
            world.BackendPhaseTicks(4, t4 - t3);
        }

        private void EnsureGlobal(int used)
        {
            if (used <= _gcap) return;

            int cap = Math.Max(used, 2 * _gcap);
            foreach (IColumn c in _gBody) c.Resize(cap);
            foreach (IColumn c in _gLink) c.Resize(WholeStep.LinkStride * cap);

            for (int set = 0; set < 2; set++)
            {
                for (int k = 0; k < 7; k++) _sph[set][k].Resize(cap);
                for (int k = 7; k < 14; k++) _sph[set][k].Resize(WholeStep.LinkStride * cap);
                _active[set].Resize(cap);
            }

            _gcap = cap;
        }

        private SGlob Glob => new SGlob
        {
            Links = _gLinks.View, Id = _gId.View, Rank = _gRank.View, Alive = _gAlive.View, Jointed = _gJointed.View,
            RankToSlot = _rankToSlot.View, TotalMass = _gTotalMass.View, LinkMass = _linkMass.View,
            NOver = _nOver.View, OvSlot = _ovSlot.View, OvId = _ovId.View, OvPart = _ovPart.View,
            OvHeld = _ovHeld.View, NHeld = _nHeld.View, HeldId = _heldId.View, BedGlass = _bedGlass.View,
            Census = _census.View, Overflow = _overflow.View,
        };

        private SGrid Grid => new SGrid
        {
            Lo = _lo.View, Hi = _hi.View, Counts = _counts.View, Start = _start.View, Cursor = _gcursor.View,
            Items = _items.View, PartialN = _partialN.View, Cell = _cell.View, Partial = _partial.View,
        };

        private SSph Sph(int set)
        {
            Col<Real>[] s = _sph[set];
            return new SSph
            {
                Cx = s[0].View, Cy = s[1].View, Cz = s[2].View, R = s[3].View,
                Vx = s[4].View, Vy = s[5].View, Vz = s[6].View,
                LCx = s[7].View, LCy = s[8].View, LCz = s[9].View, LR = s[10].View,
                LVx = s[11].View, LVy = s[12].View, LVz = s[13].View,
                Active = _active[set].View,
            };
        }

        // ============================================================== the mirror

        /// <summary>A body onto the card whole: its plan, its brain, its state and its spheres.</summary>
        private void Pack(int g)
        {
            Creature c = _slots.Body[g];
            ClassSet s = _classes[_slots.ClassOf[g]];
            int i = _slots.LocalOf[g];
            int N = s.Cap;
            int links = c.Links;
            int dof = c.Dof;

            // ---- the plan
            s.Links.Host[i] = links;
            s.Dof.Host[i] = dof;
            s.SigLen.Host[i] = c.DriveSignal.Length;
            s.Mask.Host[i] = c.Brain.SensorMask;

            Brain brain = c.Brain;
            NeuronDef[][] neurons = BrainAccess.Neurons(brain);
            int[] offset = BrainAccess.Offset(brain);
            int[] bParent = BrainAccess.Parent(brain);
            int[] firstChild = BrainAccess.FirstChild(brain);
            int[] bDofStart = BrainAccess.DofStart(brain);
            int[] bDofCount = BrainAccess.DofCount(brain);

            for (int l = 0; l < links; l++)
            {
                int at = l * N + i;
                s.Parent.Host[at] = c.Parent[l];
                s.DofCount.Host[at] = c.DofCount[l];
                s.DofStart.Host[at] = c.DofStart[l];
                s.PanelStart.Host[at] = c.PanelStart[l];
                s.ThinAxis.Host[at] = c.ThinAxis[l];

                bool grouped = l < neurons.Length;
                s.NOff.Host[at] = grouped ? offset[l] : 0;
                s.NCnt.Host[at] = grouped ? neurons[l].Length : 0;
                s.BParent.Host[at] = grouped ? bParent[l] : -1;
                s.FirstChild.Host[at] = grouped ? firstChild[l] : -1;
                s.BDofStart.Host[at] = grouped ? bDofStart[l] : -1;
                s.BDofCount.Host[at] = grouped ? bDofCount[l] : 0;

                s.Mass.Host[at] = (Real)c.Mass[l];
                s.Lift.Host[at] = (Real)c.Lift[l];
                s.Volume.Host[at] = (Real)c.Volume[l];
                s.SmallI.Host[at] = (Real)c.SmallestInertia[l];
                s.Reach.Host[at] = (Real)c.LinkReach[l];
                s.Arm.Host[at] = (Real)c.BuoyancyArm[l];

                for (int k = 0; k < 3; k++)
                {
                    int a3 = (3 * l + k) * N + i;
                    s.Inertia.Host[a3] = (Real)c.InertiaLocal[3 * l + k];
                    s.ChildAnchor.Host[a3] = (Real)c.ChildAnchor[3 * l + k];
                    s.ParentAnchor.Host[a3] = (Real)c.ParentAnchor[3 * l + k];
                }

                for (int k = 0; k < 4; k++)
                {
                    int a4 = (4 * l + k) * N + i;
                    s.JointFrame.Host[a4] = (Real)c.JointFrame[4 * l + k];
                    s.RestFrame.Host[a4] = (Real)c.RestFrame[4 * l + k];
                }
            }

            s.PanelStart.Host[links * N + i] = c.PanelStart[links];

            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                s.LimitLo.Host[at] = (Real)c.LimitLo[d];
                s.LimitHi.Host[at] = (Real)c.LimitHi[d];
                s.LimitStiff.Host[at] = (Real)c.LimitStiffness[d];
                s.LimitDamp.Host[at] = (Real)c.LimitDamping[d];
            }

            // Fluid.Apply's excess, once per creature (D064).
            SolverConfig config = _w.Config;
            double excess = config.TissueExcessDensity;
            if (config.NeutralBodyVolume > 0)
            {
                excess *= BuoyancyModel.ExcessDensityFactor((float)c.TotalVolume, (float)config.NeutralBodyVolume);
            }

            s.Excess.Host[i] = (Real)excess;
            s.TotalMass.Host[i] = (Real)c.TotalMass;

            float[] perUnit = c.Drive.MirrorTorquePerUnit;
            for (int d = 0; d < s.D; d++) s.TorquePerUnit.Host[d * N + i] = d < perUnit.Length ? perUnit[d] : 0f;

            int panels = c.PanelStart[links];
            for (int p = 0; p < panels; p++)
            {
                for (int k = 0; k < 3; k++)
                {
                    s.PCentre.Host[(3 * p + k) * N + i] = (Real)c.PanelCentre[3 * p + k];
                    s.PNormal.Host[(3 * p + k) * N + i] = (Real)c.PanelNormal[3 * p + k];
                }

                s.PArea.Host[p * N + i] = (Real)c.PanelArea[p];
            }

            // ---- the neurons
            for (int p = 0; p < neurons.Length && p < links; p++)
            {
                NeuronDef[] set = neurons[p];
                for (int n = 0; n < set.Length; n++)
                {
                    NeuronDef def = set[n];
                    int self = offset[p] + n;
                    int slot = self * N + i;
                    NeuronInput[] inputs = def.Inputs ?? Array.Empty<NeuronInput>();

                    s.Op.Host[slot] = (int)def.Op;
                    s.NIn.Host[slot] = inputs.Length;
                    s.Freq.Host[slot] = def.Frequency;
                    s.Phase.Host[slot] = def.Phase;
                    s.Amp.Host[slot] = def.Amplitude;
                    s.Bias.Host[slot] = def.Bias;

                    for (int k = 0; k < 3; k++)
                    {
                        int ks = (3 * self + k) * N + i;
                        bool has = k < inputs.Length;
                        s.Kind.Host[ks] = has ? (int)inputs[k].Kind : 0;
                        s.NIndex.Host[ks] = has ? inputs[k].Index : 0;
                        s.Channel.Host[ks] = has ? (int)inputs[k].Channel : 0;
                        s.Const.Host[ks] = has ? inputs[k].Constant : 0f;
                        s.Weight.Host[ks] = has ? inputs[k].Weight : 0f;
                    }
                }
            }

            // ---- the state
            s.BasePos.Host[i] = (Real)c.BasePosition.X;
            s.BasePos.Host[N + i] = (Real)c.BasePosition.Y;
            s.BasePos.Host[2 * N + i] = (Real)c.BasePosition.Z;
            s.BaseRot.Host[i] = (Real)c.BaseRotation.X;
            s.BaseRot.Host[N + i] = (Real)c.BaseRotation.Y;
            s.BaseRot.Host[2 * N + i] = (Real)c.BaseRotation.Z;
            s.BaseRot.Host[3 * N + i] = (Real)c.BaseRotation.W;

            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                s.Q.Host[at] = (Real)c.Q[d];
                s.Qd.Host[at] = (Real)c.Qd[d];
                s.Tau.Host[at] = (Real)c.Tau[d];
                s.LimitImplicit.Host[at] = (Real)c.LimitImplicit[d];
            }

            double[] applied = c.Drive.AppliedTorque;

            for (int l = 0; l < links; l++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a3 = (3 * l + k) * N + i;
                    int h3 = 3 * l + k;
                    s.Pos.Host[a3] = (Real)c.Position[h3];
                    s.Spin.Host[a3] = (Real)c.Spin[h3];
                    s.Vel.Host[a3] = (Real)c.Velocity[h3];
                    s.RelVel.Host[a3] = (Real)c.RelativeVelocity[h3];
                    s.Water.Host[a3] = (Real)c.Water[h3];
                    s.WaterAcc.Host[a3] = (Real)c.WaterAcceleration[h3];
                    s.Applied.Host[a3] = (Real)applied[h3];
                }

                for (int k = 0; k < 4; k++)
                {
                    int a4 = (4 * l + k) * N + i;
                    s.BallRot.Host[a4] = (Real)c.BallRotation[4 * l + k];
                    s.Rot.Host[a4] = (Real)c.Rotation[4 * l + k];
                }

                for (int k = 0; k < 9; k++)
                {
                    int a9 = (9 * l + k) * N + i;
                    s.RotM.Host[a9] = (Real)c.RotationMatrix[9 * l + k];
                    s.Sang.Host[a9] = (Real)c.Sang[9 * l + k];
                    s.Slin.Host[a9] = (Real)c.Slin[9 * l + k];
                }

                for (int k = 0; k < 6; k++)
                {
                    int a6 = (6 * l + k) * N + i;
                    s.Cbias.Host[a6] = (Real)c.Cbias[6 * l + k];
                    s.Fext.Host[a6] = (Real)c.Fext[6 * l + k];
                }
            }

            // ---- the drive
            float[] signal = c.DriveSignal;
            for (int d = 0; d < s.D; d++) s.Signal.Host[d * N + i] = d < signal.Length ? signal[d] : 0f;

            float[] history = c.Drive.MirrorHistory;
            float[] runSum = c.Drive.MirrorRunningSum;
            int window = WholeStep.Window;
            for (int d = 0; d < s.D; d++)
            {
                s.RunSum.Host[d * N + i] = d < runSum.Length ? runSum[d] : 0f;
                for (int k = 0; k < window; k++)
                {
                    int h = d * window + k;
                    s.History.Host[h * N + i] = h < history.Length ? history[h] : 0f;
                }
            }

            s.Cursor.Host[i] = c.Drive.MirrorCursor;
            s.Filled.Host[i] = c.Drive.MirrorFilled;
            s.DriveLimited.Host[i] = c.DriveImpulsesLimited;
            s.DragLimited.Host[i] = c.DragImpulsesLimited;
            s.Work.Host[i] = c.MechanicalWorkJoules;
            s.Signed.Host[i] = c.SignedWorkJoules;
            s.Dissipated.Host[i] = c.DissipatedJoules;
            s.Passive.Host[i] = c.PassiveJointWorkJoules;

            // ---- the brain: parity 0 reads the previous outputs from the first half
            float[] previous = BrainAccess.Previous(brain);
            float[] current = BrainAccess.Current(brain);
            float[] memory = BrainAccess.Memory(brain);
            int M = s.M;
            for (int n = 0; n < M; n++)
            {
                bool has = n < previous.Length;
                s.S.Host[n * N + i] = has ? previous[n] : 0f;
                s.S.Host[(M + n) * N + i] = has ? current[n] : 0f;
                s.Mem.Host[n * N + i] = has ? memory[n] : 0f;
            }

            s.Parity.Host[i] = 0;
            s.Clock.Host[i] = brain.ElapsedSeconds;

            // ---- the trace
            int frames = WholeStep.TraceFrames;
            int values = WholeStep.TraceValues;
            if (c.HasTrace)
            {
                double[] trace = c.MirrorTrace;
                for (int f = 0; f < frames; f++)
                {
                    for (int b = 0; b < links; b++)
                    {
                        for (int k = 0; k < values; k++)
                        {
                            s.Ring.Host[((f * s.L + b) * values + k) * N + i] =
                                (Real)trace[(f * links + b) * values + k];
                        }
                    }

                    s.TStep.Host[f * N + i] = c.MirrorTraceSteps[f];
                    s.TTime.Host[f * N + i] = c.MirrorTraceTimes[f];
                    s.Resized.Host[f * N + i] = c.MirrorTraceResized[f] ? 1 : 0;
                }
            }

            s.Held.Host[i] = c.MirrorTraceHeld;
            s.TCursor.Host[i] = c.MirrorTraceCursor;
            s.FirstBadStep.Host[i] = c.MirrorFirstNonFiniteStep;
            s.FirstBadLink.Host[i] = c.MirrorFirstNonFiniteLink;

            // ---- the overlap and held lists, as the body carries them
            int oc = _o.OverlapCap;
            int GN = _gcap;
            int nOver = Math.Min(c.OverlapCount, oc);
            _nOver.Host[g] = nOver;
            for (int k = 0; k < nOver; k++)
            {
                _ovId.Host[k * GN + g] = (int)c.OverlapId(k);
                _ovPart.Host[(2 * k) * GN + g] = c.OverlapPart(k);
                _ovPart.Host[(2 * k + 1) * GN + g] = c.OverlapOtherPart(k);
                _ovHeld.Host[k * GN + g] = 0;
            }

            int nHeld = Math.Min(c.MirrorHeldCount, oc);
            _nHeld.Host[g] = nHeld;
            for (int k = 0; k < nHeld; k++) _heldId.Host[k * GN + g] = (int)c.MirrorHeldId(k);

            _bedGlass.Host[g] = c.TouchedBedOrGlass ? 1 : 0;

            // ---- the committed spheres, which this block's first step reads
            Col<Real>[] sph = _sph[_committed];
            sph[0].Host[g] = (Real)c.ContactCentre.X;
            sph[1].Host[g] = (Real)c.ContactCentre.Y;
            sph[2].Host[g] = (Real)c.ContactCentre.Z;
            sph[3].Host[g] = (Real)c.ContactRadius;
            sph[4].Host[g] = (Real)c.ContactVelocity.X;
            sph[5].Host[g] = (Real)c.ContactVelocity.Y;
            sph[6].Host[g] = (Real)c.ContactVelocity.Z;
            _active[_committed].Host[g] = c.ContactActive ? 1 : 0;

            int row = g * WholeStep.LinkStride;
            if (c.LinkContactActive)
            {
                for (int l = 0; l < links; l++)
                {
                    sph[7].Host[row + l] = (Real)c.LinkContactCentre[3 * l];
                    sph[8].Host[row + l] = (Real)c.LinkContactCentre[3 * l + 1];
                    sph[9].Host[row + l] = (Real)c.LinkContactCentre[3 * l + 2];
                    sph[10].Host[row + l] = (Real)c.LinkContactRadius[l];
                    sph[11].Host[row + l] = (Real)c.LinkContactVelocity[3 * l];
                    sph[12].Host[row + l] = (Real)c.LinkContactVelocity[3 * l + 1];
                    sph[13].Host[row + l] = (Real)c.LinkContactVelocity[3 * l + 2];
                }
            }
            else
            {
                for (int l = 0; l < WholeStep.LinkStride; l++) sph[10].Host[row + l] = 0;
            }
        }

        /// <summary>What the farm may change between two blocks without a resize, for every body.</summary>
        private void PackBlock(int g)
        {
            Creature c = _slots.Body[g];
            ClassSet s = _classes[_slots.ClassOf[g]];
            int i = _slots.LocalOf[g];
            int N = s.Cap;
            int links = c.Links;

            s.GSlot.Host[i] = g;
            s.PowerScale.Host[i] = c.Drive.PowerScale;

            CreatureSenses senses = c.Senses;
            IReserveSource reserve = senses.Reserve;
            s.Reserve.Host[i] = reserve != null ? reserve.SecondsOfReserve : 0f;
            s.Gates.Host[i] = (senses.Nutrients != null ? 1 : 0) | (reserve != null ? 2 : 0);

            bool[] contact = senses.Contact;
            float[] damage = senses.Damage;
            for (int l = 0; l < links; l++)
            {
                s.Contact.Host[l * N + i] = contact != null && l < contact.Length && contact[l] ? 1 : 0;
                s.Damage.Host[l * N + i] = damage != null && l < damage.Length ? damage[l] : 0f;
            }

            s.Enabled.Host[i] = c.HasTrace ? 1 : 0;
            s.Poison.Host[i] = c.MirrorPoisonedLink;
            s.Pending.Host[i] = c.MirrorResizedSinceLastFrame ? 1 : 0;

            _gLinks.Host[g] = links;
            _gId.Host[g] = c.Id;
            _gRank.Host[g] = _slots.Rank[g];
            _gAlive.Host[g] = c.Alive ? 1 : 0;
            _gJointed.Host[g] = c.Jointed ? 1 : 0;
            _gTotalMass.Host[g] = (Real)c.TotalMass;

            int row = g * WholeStep.LinkStride;
            for (int l = 0; l < links; l++) _linkMass.Host[row + l] = (Real)c.Mass[l];

            // An overlap entry's slot, found again: a rebuilt body keeps its id and not its slot.
            int GN = _gcap;
            int n = Math.Min(_nOver.Host[g], _o.OverlapCap);
            for (int k = 0; k < n; k++)
            {
                _ovSlot.Host[k * GN + g] = SlotOfId(_ovId.Host[k * GN + g]);
            }
        }

        /// <summary>The global slot of the body with this id in the block's list, or -1.</summary>
        private int SlotOfId(int id)
        {
            // The list is in ascending id order (DynamicsWorld's contract), so the rank is a search.
            IReadOnlyList<Creature> list = _list;
            int lo = 0, hi = list.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int at = list[mid].Id;
                if (at == id) return _slots.RankToSlot[mid];
                if (at < id) lo = mid + 1;
                else hi = mid - 1;
            }

            return -1;
        }

        private IReadOnlyList<Creature> _list = Array.Empty<Creature>();

        /// <summary>A body back from the card: everything the CPU step would have left in it.</summary>
        private long Unpack(int g, bool instrument)
        {
            Creature c = _slots.Body[g];
            ClassSet s = _classes[_slots.ClassOf[g]];
            int i = _slots.LocalOf[g];
            int N = s.Cap;
            int links = c.Links;
            int dof = c.Dof;

            c.BasePosition = new Vec3(s.BasePos.Host[i], s.BasePos.Host[N + i], s.BasePos.Host[2 * N + i]);
            c.BaseRotation = new QuatD(s.BaseRot.Host[i], s.BaseRot.Host[N + i], s.BaseRot.Host[2 * N + i],
                                       s.BaseRot.Host[3 * N + i]);

            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                c.Q[d] = s.Q.Host[at];
                c.Qd[d] = s.Qd.Host[at];
                c.Tau[d] = s.Tau.Host[at];
                c.LimitImplicit[d] = s.LimitImplicit.Host[at];
            }

            double[] applied = c.Drive.AppliedTorque;

            for (int l = 0; l < links; l++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a3 = (3 * l + k) * N + i;
                    int h3 = 3 * l + k;
                    c.Position[h3] = s.Pos.Host[a3];
                    c.Spin[h3] = s.Spin.Host[a3];
                    c.Velocity[h3] = s.Vel.Host[a3];
                    c.RelativeVelocity[h3] = s.RelVel.Host[a3];
                    c.Water[h3] = s.Water.Host[a3];
                    c.WaterAcceleration[h3] = s.WaterAcc.Host[a3];
                    applied[h3] = s.Applied.Host[a3];
                }

                for (int k = 0; k < 4; k++)
                {
                    int a4 = (4 * l + k) * N + i;
                    c.BallRotation[4 * l + k] = s.BallRot.Host[a4];
                    c.Rotation[4 * l + k] = s.Rot.Host[a4];
                }

                for (int k = 0; k < 9; k++)
                {
                    int a9 = (9 * l + k) * N + i;
                    c.RotationMatrix[9 * l + k] = s.RotM.Host[a9];
                    c.Sang[9 * l + k] = s.Sang.Host[a9];
                    c.Slin[9 * l + k] = s.Slin.Host[a9];
                }

                for (int k = 0; k < 6; k++)
                {
                    int a6 = (6 * l + k) * N + i;
                    c.Cbias[6 * l + k] = s.Cbias.Host[a6];
                    c.Fext[6 * l + k] = s.Fext.Host[a6];
                }
            }

            // ---- the drive
            float[] signal = c.DriveSignal;
            int signals = Math.Min(signal.Length, s.D);
            for (int d = 0; d < signals; d++) signal[d] = s.Signal.Host[d * N + i];

            float[] history = c.Drive.MirrorHistory;
            float[] runSum = c.Drive.MirrorRunningSum;
            int window = WholeStep.Window;
            int sums = Math.Min(runSum.Length, s.D);
            for (int d = 0; d < sums; d++)
            {
                runSum[d] = s.RunSum.Host[d * N + i];
                for (int k = 0; k < window; k++)
                {
                    int h = d * window + k;
                    if (h < history.Length) history[h] = s.History.Host[h * N + i];
                }
            }

            c.Drive.MirrorCursor = s.Cursor.Host[i];
            c.Drive.MirrorFilled = s.Filled.Host[i];
            c.DriveImpulsesLimited = s.DriveLimited.Host[i];
            c.DragImpulsesLimited = s.DragLimited.Host[i];
            c.MechanicalWorkJoules = s.Work.Host[i];
            c.SignedWorkJoules = s.Signed.Host[i];
            c.DissipatedJoules = s.Dissipated.Host[i];
            c.PassiveJointWorkJoules = s.Passive.Host[i];

            // ---- the brain
            Brain brain = c.Brain;
            float[] previous = BrainAccess.Previous(brain);
            float[] current = BrainAccess.Current(brain);
            float[] memory = BrainAccess.Memory(brain);
            int M = s.M;
            int parity = s.Parity.Host[i];
            int prev = parity * M, cur = (1 - parity) * M;
            long ceiling = 0;

            for (int n = 0; n < previous.Length && n < M; n++)
            {
                float p = s.S.Host[(prev + n) * N + i];
                previous[n] = p;
                current[n] = s.S.Host[(cur + n) * N + i];
                memory[n] = s.Mem.Host[n * N + i];

                // A bound on a neuron's value is a world rule and the owner's; this counts.
                if (!(Math.Abs(p) < 1e36f)) ceiling++;
            }

            BrainAccess.SetElapsedSeconds(brain, s.Clock.Host[i]);

            // ---- the trace
            if (c.HasTrace)
            {
                int frames = WholeStep.TraceFrames;
                int values = WholeStep.TraceValues;
                double[] trace = c.MirrorTrace;
                long[] traceSteps = c.MirrorTraceSteps;
                double[] traceTimes = c.MirrorTraceTimes;
                bool[] traceResized = c.MirrorTraceResized;

                for (int f = 0; f < frames; f++)
                {
                    for (int b = 0; b < links; b++)
                    {
                        for (int k = 0; k < values; k++)
                        {
                            trace[(f * links + b) * values + k] = s.Ring.Host[((f * s.L + b) * values + k) * N + i];
                        }
                    }

                    traceSteps[f] = s.TStep.Host[f * N + i];
                    traceTimes[f] = s.TTime.Host[f * N + i];
                    traceResized[f] = s.Resized.Host[f * N + i] != 0;
                }

                c.MirrorTraceHeld = s.Held.Host[i];
                c.MirrorTraceCursor = s.TCursor.Host[i];
            }

            c.MirrorFirstNonFiniteStep = s.FirstBadStep.Host[i];
            c.MirrorFirstNonFiniteLink = s.FirstBadLink.Host[i];
            c.MirrorResizedSinceLastFrame = s.Pending.Host[i] != 0;

            // ---- alive, and the contact instrument's lists
            c.Alive = _gAlive.Host[g] != 0;

            if (instrument)
            {
                int GN = _gcap;
                int oc = _o.OverlapCap;
                long[] ids = _idScratch ??= new long[oc];
                int[] parts = _partScratch ??= new int[2 * oc];
                if (ids.Length < oc) ids = _idScratch = new long[oc];
                if (parts.Length < 2 * oc) parts = _partScratch = new int[2 * oc];

                int nOver = Math.Min(_nOver.Host[g], oc);
                for (int k = 0; k < nOver; k++)
                {
                    ids[k] = _ovId.Host[k * GN + g];
                    parts[2 * k] = _ovPart.Host[(2 * k) * GN + g];
                    parts[2 * k + 1] = _ovPart.Host[(2 * k + 1) * GN + g];
                }

                c.MirrorSetOverlaps(nOver, ids, _base.PerPart != 0 ? parts : null);

                int nHeld = Math.Min(_nHeld.Host[g], oc);
                for (int k = 0; k < nHeld; k++) ids[k] = _heldId.Host[k * GN + g];
                c.MirrorSetHeld(nHeld, ids);

                c.TouchedBedOrGlass = _bedGlass.Host[g] != 0;
            }

            // ---- the spheres: after the commit the pending ones equal the committed ones
            Col<Real>[] sph = _sph[_committed];
            var centre = new Vec3(sph[0].Host[g], sph[1].Host[g], sph[2].Host[g]);
            double radius = sph[3].Host[g];
            var velocity = new Vec3(sph[4].Host[g], sph[5].Host[g], sph[6].Host[g]);
            bool active = _active[_committed].Host[g] != 0;

            c.ContactCentre = centre;
            c.ContactRadius = radius;
            c.ContactVelocity = velocity;
            c.ContactActive = active;
            c.MirrorPendingCentre = centre;
            c.MirrorPendingRadius = radius;
            c.MirrorPendingVelocity = velocity;
            c.MirrorPendingActive = active;

            if (c.LinkContactActive)
            {
                int row = g * WholeStep.LinkStride;
                double[] pc = c.MirrorPendingLinkCentre, pr = c.MirrorPendingLinkRadius, pv = c.MirrorPendingLinkVelocity;

                for (int l = 0; l < links; l++)
                {
                    double x = sph[7].Host[row + l], y = sph[8].Host[row + l], z = sph[9].Host[row + l];
                    double r = sph[10].Host[row + l];
                    double vx = sph[11].Host[row + l], vy = sph[12].Host[row + l], vz = sph[13].Host[row + l];

                    c.LinkContactCentre[3 * l] = x; c.LinkContactCentre[3 * l + 1] = y; c.LinkContactCentre[3 * l + 2] = z;
                    c.LinkContactRadius[l] = r;
                    c.LinkContactVelocity[3 * l] = vx; c.LinkContactVelocity[3 * l + 1] = vy; c.LinkContactVelocity[3 * l + 2] = vz;

                    pc[3 * l] = x; pc[3 * l + 1] = y; pc[3 * l + 2] = z;
                    pr[l] = r;
                    pv[3 * l] = vx; pv[3 * l + 1] = vy; pv[3 * l + 2] = vz;
                }
            }

            return ceiling;
        }

        /// <summary>Creature.CommitOverlaps for a body the card does not hold.</summary>
        private static void CommitOwnList(Creature c)
        {
            int n = c.OverlapCount;
            var ids = new long[Math.Max(1, n)];
            for (int k = 0; k < n; k++) ids[k] = c.OverlapId(k);
            c.MirrorSetHeld(n, ids);
        }

        /// <summary>
        /// DynamicsWorld.CloseContactStep's event list for the block's last step, which is the only
        /// one the farm reads: from the lists and the held flags the census left.
        /// </summary>
        private void BuildEvents(DynamicsWorld world, IReadOnlyList<Creature> list, bool instrument)
        {
            List<OverlapPair> events = world.BackendOverlapList;
            events.Clear();

            if (!instrument || !world.Config.ContactEvents) return;

            bool perPart = world.Config.ContactPerPart;
            int GN = _gcap;

            for (int r = 0; r < list.Count; r++)
            {
                int g = _slots.RankToSlot[r];
                if (g < 0) continue;

                Creature a = list[r];
                if (!a.Alive) continue;

                int n = Math.Min(_nOver.Host[g], _o.OverlapCap);
                for (int k = 0; k < n; k++)
                {
                    long id = _ovId.Host[k * GN + g];
                    Creature b = world.ById(id);
                    if (b == null || !b.Alive) continue;
                    if (id <= a.Id) continue;

                    bool jointed = a.Jointed || b.Jointed;
                    bool held = _ovHeld.Host[k * GN + g] != 0;

                    events.Add(perPart
                        ? new OverlapPair(a.Id, id, jointed, held,
                            _ovPart.Host[(2 * k) * GN + g], _ovPart.Host[(2 * k + 1) * GN + g])
                        : new OverlapPair(a.Id, id, jointed, held));
                }
            }
        }

        public void Dispose()
        {
            foreach (ClassSet s in _classes) s.Dispose();
            foreach (AcceleratorStream s in _streams) s?.Dispose();
            foreach (IColumn c in _gBody) c.Dispose();
            foreach (IColumn c in _gLink) c.Dispose();
            _rankToSlot.Dispose();
            _census.Dispose();
            _overflow.Dispose();
            for (int set = 0; set < 2; set++)
            {
                foreach (Col<Real> c in _sph[set]) c.Dispose();
                _active[set].Dispose();
            }

            _lo.Dispose(); _hi.Dispose(); _counts.Dispose(); _start.Dispose(); _gcursor.Dispose();
            _items.Dispose(); _partial.Dispose(); _partialN.Dispose(); _cell.Dispose();
            _inst.Dispose(); _wr.Dispose(); _wi.Dispose(); _stock.Dispose();
        }
    }
}

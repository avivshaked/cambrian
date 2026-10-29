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
//   the grid       MeanSerial | MeanChunks + MeanCombine, Ranges, ScanReduce + ScanTiles
//                  + ScanDown, Scatter                                         (Contacts.cs)
//   the reach guard  after Ranges: LoseStrays, Rearm, ClearCounts, the mean and Ranges again,
//                  LoseStrays again, each a no-op in a step with no stray  (DynamicsWorld.LoseStrays)
//   the water      WaterLinks, a link a thread, one launch per class (Water.Sample, StepOne's
//                  first part; beside the grid under concurrent streams)
//   the fluid      FluidLinks, a link a thread, one launch per class after its water
//                  (Fluid.Apply's per-link part)
//   the contacts   ContactLinks under per-part contact, a link a thread, one launch per
//                  class after the grid (Contacts.Apply's per-link part)
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
#if GPU_PHASE_PROBE
using ILGPU.Runtime.Cuda;
#endif

namespace Evosim.Farm.Gpu.Dbl
{
    using Real = System.Double;

    /// <summary>A class's integers: per body, then per link, strided by the class's capacity.</summary>
    public struct STopo
    {
        public ArrayView<int> Links, Dof, SigLen, Mask, GSlot;
        public ArrayView<int> Parent, DofCount, DofStart, PanelStart, ThinAxis;
        public ArrayView<int> NOff, NCnt, BParent, FirstChild, BDofStart, BDofCount;

        // The one brain (D146): its unit count (-1 for the per-part neurons), its degrees of
        // freedom, and where its links and its ports start in the shared pools (SNeur).
        public ArrayView<int> CUnits, CDof, CLinkAt, CPortAt;
    }

    /// <summary>Everything about a class's body that does not change inside a physics step.</summary>
    public struct SConst
    {
        public ArrayView<Real> Mass, Lift, Volume, SmallI, Reach, Arm, Inertia, ChildAnchor, ParentAnchor, SpinMoment;
        public ArrayView<Real> JointFrame, RestFrame, LimitLo, LimitHi, LimitStiff, LimitDamp;
        public ArrayView<Real> Excess, TotalMass;
        public ArrayView<float> TorquePerUnit, PowerScale;

        // The bend-and-power drive, a link each: the joint's four genes (DriveGenes) and the
        // part's Power, which caps its muscle.
        public ArrayView<float> Threshold, Reaction, HoldGene, Release;
        public ArrayView<Real> LinkPower;

        // D229's fin law, a link each: the part's shape (0 a sphere, 1 a box, 2 a capsule) and, beside
        // it, three numbers: a box's half-extents, or a capsule's radius and half-span.
        public ArrayView<int> ShapeCode;
        public ArrayView<Real> HalfExt;

        // D231's stroke that pushes its assembly. A link each: the parts in the assembly it anchors (0 for
        // a link that is not jointed); a degree of freedom each, indexed like DofStart: the cosine between
        // its axis and the limb's, the added mass of the assembly moving along its stroke, and that mass's
        // first moment about the joint. One row wide when the switch is off.
        public ArrayView<int> StrokeParts;
        public ArrayView<Real> StrokeAlong, StrokeMass, StrokeMoment;

        // D255's family limb under the fin law (shape code 3), a degree of freedom each, indexed like DofStart: the
        // added mass of the part moving along that degree's stroke and that mass's first moment about the joint,
        // as the host body holds them (Creature.FamilyAddedMass, FamilyFirstMoment). 0 for a degree of a link that
        // is not a family link. One row wide when the fin law is off.
        public ArrayView<Real> FamMass, FamMoment;
        // D234's mirrored twin, a link each: the bit mask of the degrees of freedom whose signed
        // quantities are read negated (Creature.MirrorMask). A link wide only in a world whose bodies
        // reflect their drive, a one-wide stub otherwise, and then never read (cfg.MirrorDrive is 0). Bit d is
        // degree of freedom d; bit 3 says the part reflects the vectors its senses read in its own frame, and
        // bits 4 and 5 are the axis of that reflection (Creature.FrameNormal).
        public ArrayView<int> MirrorMask;
        // D235's nose, a link each: 1 on a nose link. A link wide only in a world whose table lists the nose, a
        // one-wide stub otherwise, and then never read (cfg.Nose is 0).
        public ArrayView<int> NoseFlag;
    }

    public struct SPanels { public ArrayView<Real> Centre, Normal, Area; }

    /// <summary>A class's body state: what the next step reads, and the step's outputs beside it.</summary>
    public struct SState
    {
        public ArrayView<Real> BasePos, BaseCarry, BaseRot, Q, Qd, Tau, LimitImplicit, BallRot;
        public ArrayView<Real> Pos, Rot, RotM, Spin, Vel, Sang, Slin, Cbias, RelVel, Water, WaterAcc, Fext, Applied;
    }

    /// <summary>The drive's window and signal, the limiter counts and the ledger's four sums.</summary>
    public struct SDrive
    {
        public ArrayView<float> Signal, History, RunSum;
        public ArrayView<int> Cursor, Filled;

        // The bend-and-power drive: the brain's bend a degree of freedom, and the envelope's seven
        // numbers a degree of freedom (EffectorDrive's _power to _holdLeft), in double as on the CPU.
        public ArrayView<float> Bend;
        public ArrayView<double> EPower, EBend, EPowerTarget, EBendTarget, EPowerRate, EBendRate, EHoldLeft;
        public ArrayView<long> DriveLimited, DragLimited;
        public ArrayView<double> Work, Signed, Dissipated, Passive;
    }

    public struct SNeur
    {
        public ArrayView<int> Op, NIn, Kind, Index, Channel;
        public ArrayView<float> Freq, Phase, Amp, Bias, Const, Weight;

        // The one brain's pools, shared by every class: each link's source (a unit at 0 or more,
        // ~slot for a port) and weight, in the CPU's order; each port's part, channel and index.
        // A unit's bias is Bias and its number of incoming links NIn, in the unit's own row.
        public ArrayView<int> CSource, CPortPart, CPortChannel, CPortIndex;
        public ArrayView<float> CWeight;
    }

    /// <summary>The brain's buffers and the senses' hand-back; Gates bit 0 nutrients, bit 1 reserve.</summary>
    public struct SBrain
    {
        public ArrayView<float> S, Mem, Reserve, Damage;
        // D235: each nose link's held sniff, six floats a link (chemical, scent, leaf, direction x, y, z), rows
        // (6 * link + k) * N + slot, filled on the host from CreatureSenses at every block. A one-cell stub, never
        // filled or read, in a world without the nose (cfg.Nose is 0).
        public ArrayView<float> NoseHeld;
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

        // D228's cores, by link row: each link's core half-extents and margin (ConvexCore.Of), packed with the
        // body and uploaded only when a pack changes them. A stub of one cell unless cfg.TrueShape.
        public ArrayView<Real> CoreHx, CoreHy, CoreHz, CoreM;
        public ArrayView<int> NOver, OvSlot, OvId, OvPart, OvHeld, NHeld, HeldId, BedGlass;
        public ArrayView<long> Census, Overflow;
        // LinkPush: the contact force a link row hands its class kernel, three rows of Rows (x, y, z); under cfg.AtPoint
        // three more follow (the torque r x F about the link's centre, x, y, z).
        public ArrayView<Real> LinkPush, LinkFluid;
        public ArrayView<int> LinkFlags, StageN, StageId, StageSlot, StagePart, LinkLimited;

        // D220's remains, by global slot: 1 for a body whose organism is remains (Creature.Remains), uploaded
        // with every block. Read only under cfg.AnyRemains, so a world without remains never loads it.
        public ArrayView<int> Remains;

        // D162's touch of the rock, by link row: the record the last step left (RockTouched and its
        // point and normal), which the senses read and the farm keeps, and this step's (TouchNew,
        // and TouchAt's point then normal, a component per Rows), which ContactLinks writes and
        // the class kernel keeps after its contacts (KeepTouch).
        public ArrayView<int> RockTouched, TouchNew;
        public ArrayView<Real> RockPx, RockPy, RockPz, RockNx, RockNy, RockNz, TouchAt;

        // D162's grip (Anchor.Apply). By link row: whether the link carries the anchoring cell, its
        // full-strength breaking force and the price of a grip formed at it (the CPU's floats,
        // widened on the host), and the grip itself, the farm's to keep (GripAnchor and GripPoint 3
        // a row, GripAttitude 9). By global slot: the reserve rule's clock, the price owed since the
        // farm last drained it, and the body's tallies.
        public ArrayView<int> Anchors, GripHeld;
        public ArrayView<double> GripBreak, GripPrice, GripStrength, ReserveLow, GripOwed, GripHeldSeconds;
        public ArrayView<Real> GripAnchor, GripAttitude, GripPoint;
        public ArrayView<long> GripsFormed, GripsBroken;

        // The reach guard's records, by global slot, cleared at each block's start: which sphere
        // strayed (StrayCode, 0 for none), at which step, and where (x, y, z, radius).
        public ArrayView<int> StrayCode;
        public ArrayView<long> StrayStep;
        public ArrayView<Real> StrayAt;
#if GPU_PHASE_PROBE
        public ArrayView<long> Phase;
        public ArrayView<long> LinkPhase;
#endif
    }

    /// <summary>One set of bounding spheres, the body's and (D114) every link's: committed or pending.</summary>
    public struct SSph
    {
        public ArrayView<Real> Cx, Cy, Cz, R, Vx, Vy, Vz;
        public ArrayView<Real> LCx, LCy, LCz, LR, LVx, LVy, LVz, LEx, LEy, LEz;

        // D228: each link's turn, row-major, committed with its sphere. A stub of one cell unless cfg.TrueShape.
        public ArrayView<Real> LRot0, LRot1, LRot2, LRot3, LRot4, LRot5, LRot6, LRot7, LRot8;

        // D228 level (b): each link's angular velocity in the world, committed with its sphere (Creature.LinkContactSpin).
        // A stub of one cell unless cfg.AtPoint.
        public ArrayView<Real> LSp0, LSp1, LSp2;
        public ArrayView<int> Active;
    }

    /// <summary>The contact grid over rows: a body, or under per-part contact a (body, link).</summary>
    public struct SGrid
    {
        public ArrayView<int> Lo, Hi, Counts, Start, Cursor, Items, PartialN, TileSum, TileStart;
        public ArrayView<Real> Cell, Partial;

        // The giants: [0] how many claimed a place this build (may pass GiantCap), then their rows;
        // a row's place plus one, or 0; each giant's list length and its rows.
        public ArrayView<int> Giant, IsGiant, GiantN, GiantList;

        // The reach guard's counters: [StrayFound] strays the last Ranges found, [StrayRedo] the
        // flag the grid's second build reads, [StrayLeft] strays found by that second build.
        public ArrayView<int> Stray;
    }

    /// <summary>The world a body reads: the instants, the world's constants, the snow.</summary>
    public struct SWorld
    {
        public ArrayView<Real> Inst, WR, Stock;
        public ArrayView<int> WI;

        // The scent's grid (ScentField), uploaded every block: the host refills it only at a
        // metabolic step, which a block never spans.
        public ArrayView<float> Scent;

        // D167's leaf smell (World.LeafScent), uploaded every block as the scent's grid is.
        public ArrayView<float> Leaf;

        // One cumulative count: the bed's angles past AngleGuard (BedSample and
        // BedHeightAndGradient), for the tests.
        public ArrayView<long> Guard;
    }

    public struct SCfg
    {
        // The launch: the class's capacity and neuron ceiling, the global roster, the grid.
        public int N, MaxN, GN, GUsed, Count, Rows, Buckets, Mask, OverCap, EntryCap;
        public int GiantCells;   // cells a row's sphere covers from which it is a giant (WholeStep.Ranges)
        public int InstBase, StepIndex, PerPart, Instrument, Chunk, Chunks, Tiles, ClassUsed, ClassLinks, LinkRows;

        public int HasCurrent, Accelerating, Sloped, HasBed, BedRelief, BedModes, ShoreOn, FadeOn;
        public int Ocean;        // D204: the open ocean's current in a box (CurrentMode.Ocean)
        public int ReefCount, LimitDrive, LimitDrag, UseRestore, FloorRestores, CreatureContact;
        public int WeldedDrag;   // D199/D200: under LimitDrag, the torque capped by the welded body (WeldedDrag)
        public int OffsetTorque, SplitColumns, Redo, BendAndPower, TurningDrag, RockTouch, Anchoring;
        public int ShapedMass;   // D229: the limb pushes take the part's shaped added mass (Fluid.AddedMassOf)
        public int MirrorDrive;  // D234: a mirrored twin's drive and senses are read through its link's mirror mask
        public int Nose;         // D235: only nose links smell, and they read the held sniff the host left (SBrain.NoseHeld); no field is read for a smell
        public int AnyRemains;   // D220: 1 for a block in which some body is remains; 0 reads no remains flag at all
        public int TrueShape;    // D228: a part touches as its convex core with a margin (SolverConfig.ContactShape)
        public int StrokeAssembly;   // D231: the powered stroke pushes the assembly welded beyond a jointed link (SolverConfig.StrokePushesAssembly)
        public int AtPoint;      // D228 level (b): with TrueShape, the force acts at the contact point (ContactShapeForm.TrueShapeAtPoint)
        public int Paced;        // D239: 1 when the pace factor is not 1; 0 divides nothing and the water is stored as it was sampled

        public long TraceStep;
        public double TraceTime, DtD, PoseOmega, GripDecay, GripThreshold, GripLag;

        public Real Dt, Density, DragK, AddedMass, FluidAccel, Gravity, WorldDepth, Damping, SpinBudget;
        public Real RestoringDensity, TissueDensity;

        public Real Omega, Zeta, MaxSep, MaxDv, TankRadius, AxisX, AxisZ, CellOverride;

        // D204: the box's extent along x and z when bodies touch across its seams, 0 without them
        // (SolverConfig.SeamLengthMetres).
        public Real SeamL, SeamW;
        public Real PushOmega, LimbPush, NegSkin, NegSpin;
        public Real BedRadius, BedDepth, TiltX, TiltZ, Offset, ShoalHeight;

        public Real CurDepth, CurTankR, EddyWeight, Overturning, PerSecond, Squared, ShoreDepth, ShoreFadeW;
        public Real Pace, PaceSquared;   // D239: the pace factor and its square, which the stored water is divided by when Paced
        public Real HalfThickness, Fillet, ReefFade;

        // D194's coral (CoralRock): its colonies, 0 without; the fade, its floor, the fillet, the grid's cell and
        // cells, and the square's periods (0 in a tank). Its rows sit in SWorld.WR from the four offsets below.
        public int CoralCount, CoralCellsX, CoralCellsZ;
        public int CoralColonyAt, CoralPieceAt, CoralGridAt, CoralListAt, CoralFadeGridAt, CoralFadeListAt;
        public Real CoralFillet, CoralFade, CoralFloor, CoralCell, CoralPeriodX, CoralPeriodZ;
        public float SigmaSloped, SigmaFlat, Speed;

        // The current's own extents along x and z, as floats (CurrentField.LengthMetres and WidthMetres):
        // the ocean folds a place into the box with them.
        public float OceanL, OceanW;

        public float SWorldDepth, ChemHalf, EnergyScale, FlowScale, RateScale, ConstCE, DtF;
        public float ScentHalf;

        // D245's energy squash: 1 reads S / (|S| + EnergyHalfK) in float, 0 the tanh of S over EnergyScale.
        public int EnergyHalfMode;
        public float EnergyHalfK;
        public int ScentNx, ScentNy, ScentNz;

        // D204: 1 when the smell grids' x and z are rings (ScentField.Wraps), the seams joined.
        public int SmellWraps;
        public float LeafHalf, LeafCell;
        public int LeafNx, LeafNy, LeafNz;
        public int Nx, Ny, Nz, LayerCount, RefugeLayers, LayerStride;
        public float CellMetres, FieldTankR, RefugeFraction, CellVolume;

        // The snow grid's box, m: its seams in D204's square, where FieldTankR is 0.
        public float FieldL, FieldW;

        // Offsets into SWorld.WR and SWorld.WI.
        public int StreamAmpAt, StreamRateAt, CellAmpAt, BedAt, ReefAt;
        public int OceanKAt;     // the ocean's 25 k_x then its 25 k_z in SWorld.WR
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
            Real s = WholeStep.SinR(half);
            return new Q4(n.X * s, n.Y * s, n.Z * s, WholeStep.CosR(half));
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
        public const int CandCap = 512;            // contact candidates a query keeps; past it, counted and dropped
        public const int Window = 10;              // EffectorDrive.SmoothWindow
        public const int TraceFrames = 3;          // Creature.TraceFrames
        public const int TraceValues = 21;         // Creature.TraceValuesPerLink
        public const int ReefStride = 16;          // values a reef takes in SWorld.WR
        public const int CensusFields = 5;         // pairs, jointed, held, bodies, bed or glass
        public const int ScanPer = 8;              // buckets a thread in a scan tile
        public const int ScanGroupCap = 256;       // the scan tile's group, at most
        public const int StageCap = 4;             // overlaps a link hands its step; past it, found again

        // Overflow counters: candidates past CandCap, overlaps past OverCap, held ids past OverCap,
        // cell entries past EntryCap.
        public const int OverflowCandidates = 0, OverflowOverlaps = 1, OverflowHeld = 2, OverflowEntries = 3;

        // The contact watch's slots in the same column (DynamicsWorld.Watch), each a block's maximum that the
        // runner folds and clears: the most candidates a query kept, the most overlaps a body listed and the
        // row covering the most cells, the last two as count << 24 | (0xFFFFFF - the body's rank), so equal
        // counts go to the earliest body in the list.
        public const int WatchCandidates = 4, WatchOverlaps = 5, WatchCells = 6;

        // The giants (2026-10-17, round 61's r61-p6103): rows whose spheres cover GiantCells cells or more,
        // at most GiantCap a build; past it a row stays in the grid. A giant's list holds GiantListCap
        // rows; past it the giant walks its cells as any row does. GiantCap was 32 until r61-p6103x
        // (2026-10-18) held 48 to 75 such rows by 49,100 s, and the 15,000-cell ones that lost the race
        // for a place slowed the card fivefold from the grid; the work scales with the giants there are.
        // GiantListCap was 2,048 until r61-p6103x2 (2026-10-18): one kelp blade's 51 m link, 362,952 cells
        // with 2,702 rows in its box, walked its cells for 20 simulated seconds at about 50 times the step's
        // cost. At 32,768 a list holds more rows than the round's worlds hold at once (about 21,000 at
        // 55,100 s), and the lists take 128 MB of the card.
        public const int GiantCap = 1024, GiantListCap = 32768;

        // The reach guard's counters in SGrid.Stray, and Contacts.ReachCells.
        public const int StrayFound = 0, StrayRedo = 1, StrayLeft = 2;
        public const double ReachCells = 1e9;

        // The instant's layout: the samplers' phase (A) then the analytic acceleration's (B).
        public const int Terms = 24, Cells = 3;
        public const int ACos = 0, ASin = 24, AEnv = 48, ACellEnv = 72, ACell = 75;
        public const int BCos = 78, BSin = 102, BEnv = 126, BEnvRate = 150, BCellEnv = 174,
            BCellEnvRate = 177, BCell = 180, BCellRate = 183;
        public const int InstStride = 450;

        // The ocean's instant in the same rows, in place of the streams': every mode's W·cos φ and
        // W·sin φ at the samplers' phase (A), then the two and their clock derivatives at the
        // acceleration's (B). CurrentField.EnsureOceanInstant's slots, in its mode order.
        public const int OceanVectors = 25, OceanLayers = 3, OceanModes = 75;
        public const int OCos = 0, OSin = 75, OBCos = 150, OBSin = 225, OBCosRate = 300, OBSinRate = 375;

        private const int SDepth = 9, SUp = 3, SJointAngle = 0, SJointRate = 1, SChemical = 6, SEnergy = 7,
            SFlow = 8, SContact = 2, SDamage = 5, SScent = 10,
            SStroke = 11, SHold = 12, SRamp = 13, SRock = 14, SLeaf = 15, SLeafDir = 16;

        // ============================================================== the grid

        /// <summary>The mean radius in the list's order, on one thread (Contacts.cs Build, BuildLinks).</summary>
        public static void MeanSerial(Index1D index, SSph s, SGlob gl, SGrid gr, SCfg cfg)
        {
            if (index.X != 0) return;
            if (cfg.Redo != 0 && gr.Stray[StrayRedo] == 0) return;
            ResetGiants(gr);

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
                        int row = g * LinkStride + l;
                        if (!FiniteSphere(s.LCx[row], s.LCy[row], s.LCz[row], s.LR[row])) continue;
                        sum += s.LR[row];
                        active++;
                    }
                }
                else
                {
                    if (!FiniteSphere(s.Cx[g], s.Cy[g], s.Cz[g], s.R[g])) continue;
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
            if (cfg.Redo != 0 && gr.Stray[StrayRedo] == 0) return;

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
                        int row = g * LinkStride + l;
                        if (!FiniteSphere(s.LCx[row], s.LCy[row], s.LCz[row], s.LR[row])) continue;
                        sum += s.LR[row];
                        active++;
                    }
                }
                else
                {
                    if (!FiniteSphere(s.Cx[g], s.Cy[g], s.Cz[g], s.R[g])) continue;
                    sum += s.R[g];
                    active++;
                }
            }

            gr.Partial[c] = sum;
            gr.PartialN[c] = active;
        }

        /// <summary>The bodies one chunk of the mean holds: the runner's chunk, and MeanGroups' shared rows.</summary>
        public const int MeanGroupBodies = 256;

        /// <summary>
        /// <see cref="MeanChunks"/>' sums with the reads spread over a group, one group a chunk
        /// (card-next-r57.md, part 5). The chunk thread waited on every read in turn; here the group's
        /// threads read the chunk's bodies, a stride of the group apart, and put each link's radius in
        /// shared memory in the body's own order, NaN for a sphere the chunk thread skips (a finite
        /// sphere's radius never is). After the barrier the group's first thread adds them in rank
        /// order and link order, so each partial sum is the chunk thread's additions of the same
        /// values in the same order: the same bits at any group size.
        /// </summary>
        public static void MeanGroups(SSph s, SGlob gl, SGrid gr, SCfg cfg)
        {
            var radius = SharedMemory.Allocate<Real>(MeanGroupBodies * LinkStride);
            var count = SharedMemory.Allocate<int>(MeanGroupBodies);

            // Both tests hold for the whole group, so no thread leaves before a barrier another waits at.
            int c = Grid.IdxX;
            if (c >= cfg.Chunks) return;
            if (cfg.Redo != 0 && gr.Stray[StrayRedo] == 0) return;

            int from = c * cfg.Chunk;
            int bodies = cfg.Count - from;
            if (bodies > cfg.Chunk) bodies = cfg.Chunk;

            for (int b = Group.IdxX; b < bodies; b += Group.DimX)
            {
                int n = 0;
                int g = gl.RankToSlot[from + b];
                if (g >= 0 && s.Active[g] != 0)   // -1: refused for its class, never on the card
                {
                    if (cfg.PerPart != 0)
                    {
                        n = gl.Links[g];
                        for (int l = 0; l < n; l++)
                        {
                            int row = g * LinkStride + l;
                            radius[b * LinkStride + l] =
                                FiniteSphere(s.LCx[row], s.LCy[row], s.LCz[row], s.LR[row]) ? s.LR[row] : Real.NaN;
                        }
                    }
                    else
                    {
                        n = 1;
                        radius[b * LinkStride] = FiniteSphere(s.Cx[g], s.Cy[g], s.Cz[g], s.R[g]) ? s.R[g] : Real.NaN;
                    }
                }

                count[b] = n;
            }

            Group.Barrier();
            if (Group.IdxX != 0) return;

            Real sum = 0;
            int active = 0;
            for (int b = 0; b < bodies; b++)
            {
                int n = count[b];
                for (int l = 0; l < n; l++)
                {
                    Real r = radius[b * LinkStride + l];
                    if (r != r) continue;
                    sum += r;
                    active++;
                }
            }

            gr.Partial[c] = sum;
            gr.PartialN[c] = active;
        }

        /// <summary>A build's giants start empty: run by whichever mean kernel the build takes, before Ranges.</summary>
        private static void ResetGiants(SGrid gr)
        {
            gr.Giant[0] = 0;
            for (int k = 0; k < GiantCap; k++) gr.GiantN[k] = 0;
        }

        public static void MeanCombine(Index1D index, SGrid gr, SCfg cfg)
        {
            if (index.X != 0) return;
            if (cfg.Redo != 0 && gr.Stray[StrayRedo] == 0) return;
            ResetGiants(gr);

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
            if (cfg.Redo != 0 && gr.Stray[StrayRedo] == 0) return;
            gr.IsGiant[row] = 0;

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

            // Contacts.OutOfReach: a sphere past the grid's reach, or not a number, is entered
            // nowhere and marked for LoseStrays, the body's lowest straying link winning.
            if (OutOfReach(cx, cy, cz, r, cell))
            {
                gr.Lo[row] = 1; gr.Hi[row] = 0;
                gr.Lo[R + row] = 1; gr.Hi[R + row] = 0;
                gr.Lo[2 * R + row] = 1; gr.Hi[2 * R + row] = 0;
                Atomic.Max(ref gl.StrayCode[g], cfg.PerPart != 0 ? LinkStride - l : LinkStride + 1);
                Atomic.Add(ref gr.Stray[cfg.Redo != 0 ? StrayLeft : StrayFound], 1);
                return;
            }

            SeamCells(cell, cfg, out int wx, out int wz, out Real cellX, out Real cellZ);

            int lx = Floor((cx - r) / cellX), hx = Floor((cx + r) / cellX);
            int ly = Floor((cy - r) / cell), hy = Floor((cy + r) / cell);
            int lz = Floor((cz - r) / cellZ), hz = Floor((cz + r) / cellZ);

            // ContactGrid.ClampToSeams: a sphere wider than the box would enter some cells twice.
            if (wx > 0 && hx - lx >= wx) hx = lx + wx - 1;
            if (wz > 0 && hz - lz >= wz) hz = lz + wz - 1;

            gr.Lo[row] = lx; gr.Hi[row] = hx;
            gr.Lo[R + row] = ly; gr.Hi[R + row] = hy;
            gr.Lo[2 * R + row] = lz; gr.Hi[2 * R + row] = hz;

            // A giant enters no bucket: GiantLists hands it its rows, and every query tests it.
            long cells = (long)(hx - lx + 1) * (hy - ly + 1) * (hz - lz + 1);
            Atomic.Max(ref gl.Overflow[WatchCells], (cells << 24) | (long)(0xFFFFFF - gl.Rank[g]));
            if (cells >= cfg.GiantCells)
            {
                int place = Atomic.Add(ref gr.Giant[0], 1);
                if (place < GiantCap)
                {
                    gr.Giant[1 + place] = row;
                    gr.IsGiant[row] = place + 1;
                    return;
                }
            }

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                Atomic.Add(ref gr.Counts[Hash(Wrap(x, wx), y, Wrap(z, wz)) & cfg.Mask], 1);
            }
        }

        /// <summary>
        /// DynamicsWorld.LoseStrays, a global slot a thread: a body Ranges marked is recorded, by
        /// its sphere as Ranges read it and the step, for the host to hand the farm, and its
        /// committed spheres are emptied as Creature.MarkLost and CommitContactSphere empty them:
        /// the centres kept, the radii and the place in the grid lost. Nothing when the counter it
        /// reads is 0, which is every step in which no sphere strayed. Under <c>cfg.Redo</c> it
        /// takes what the grid's second build found.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It does not write <c>Alive</c>. Under concurrent streams the water and the fluid read
        /// <c>Alive</c> on the class streams while this runs on the default one, so a write here
        /// would decide by timing whether they sample the body. The class kernel marks the body
        /// dead at its top instead (StrayCode set), after the streams join.
        /// </para>
        /// <para>
        /// So the water and the fluid sample a stray once, in the step it strays, where the CPU
        /// samples nothing. That is safe. Neither reads memory at an index made from a position:
        /// the current, the bed and the reefs are sums over modes, terms and reefs with fixed
        /// counts (StreamsUnit, StreamsSlopedUnit, BedSample, ReefFade). Each writes only the
        /// body's own rows (s.Water, s.WaterAcc, gl.LinkFluid, gl.LinkLimited at the body's slot),
        /// which only the class kernel's own step reads, and a dead body's returns before it. They
        /// book nothing: the drive's work and the ledgers' sums are added in the class kernel's
        /// step, and the digest hashes poses, spins and velocities, which nothing here writes.
        /// ContactLinks skips a body with a stray record.
        /// </para>
        /// </remarks>
        public static void LoseStrays(Index1D index, SSph s, SGlob gl, SGrid gr, SCfg cfg)
        {
            int g = index.X;
            if (g >= cfg.GUsed) return;
            if (gr.Stray[cfg.Redo != 0 ? StrayLeft : StrayFound] == 0) return;

            // Recorded already (this step's first build, or an earlier step), or not a stray.
            int code = gl.StrayCode[g];
            if (code == 0 || gl.Alive[g] == 0 || gl.StrayStep[g] != 0) return;

            int l = code > LinkStride ? -1 : LinkStride - code;
            Real cx, cy, cz, r;
            if (l < 0)
            {
                cx = s.Cx[g]; cy = s.Cy[g]; cz = s.Cz[g]; r = s.R[g];
            }
            else
            {
                int row = g * LinkStride + l;
                cx = s.LCx[row]; cy = s.LCy[row]; cz = s.LCz[row]; r = s.LR[row];
            }

            gl.StrayAt[4 * g] = cx;
            gl.StrayAt[4 * g + 1] = cy;
            gl.StrayAt[4 * g + 2] = cz;
            gl.StrayAt[4 * g + 3] = r;
            gl.StrayStep[g] = cfg.TraceStep;

            s.R[g] = 0;
            s.Active[g] = 0;

            if (cfg.PerPart != 0)
            {
                int n = gl.Links[g];
                for (int k = 0; k < n; k++) s.LR[g * LinkStride + k] = 0;
            }
        }

        /// <summary>The grid is built again when the first build found a stray, and not otherwise.</summary>
        public static void Rearm(Index1D index, SGrid gr, SCfg cfg)
        {
            if (index.X != 0) return;
            gr.Stray[StrayRedo] = gr.Stray[StrayFound];
            gr.Stray[StrayFound] = 0;
        }

        /// <summary>The histogram emptied for the grid's second build, when there is one.</summary>
        public static void ClearCounts(Index1D index, SGrid gr, SCfg cfg)
        {
            int i = index.X;
            if (i > cfg.Buckets) return;
            if (gr.Stray[StrayRedo] == 0) return;
            gr.Counts[i] = 0;
        }

        // The exclusive prefix sum of the bucket counts, in three passes: each tile's total, the
        // tiles' starts on one group, and each tile's buckets from its start. The sums are
        // integers, so any tiling gives the same starts as the one group of the first cut did.

        /// <summary>The scan's first pass: a tile's total, a tile a group, ScanPer buckets a thread.</summary>
        public static void ScanReduce(SGrid gr, SCfg cfg)
        {
            var sh = SharedMemory.Allocate<int>(ScanGroupCap);
            int t = Group.IdxX;
            int G = Group.DimX;
            int tile = Grid.IdxX;
            int B = cfg.Buckets;
            int first = tile * G * ScanPer;

            int local = 0;
            for (int k = 0; k < ScanPer; k++)
            {
                int b = first + k * G + t;
                if (b < B) local += gr.Counts[b];
            }
            sh[t] = local;
            Group.Barrier();

            for (int half = G >> 1; half > 0; half >>= 1)
            {
                if (t < half) sh[t] = sh[t] + sh[t + half];
                Group.Barrier();
            }

            if (t == 0) gr.TileSum[tile] = sh[0];
        }

        /// <summary>The scan's second pass: the tiles' starts and the grand total, on one group.</summary>
        public static void ScanTiles(SGrid gr, SCfg cfg)
        {
            var sh = SharedMemory.Allocate<int>(1024);
            int t = Group.IdxX;
            int G = Group.DimX;
            int T = cfg.Tiles;

            int chunk = (T + G - 1) / G;
            int begin = t * chunk;
            int end = begin + chunk;
            if (end > T) end = T;
            if (begin > T) begin = T;

            int local = 0;
            for (int i = begin; i < end; i++) local += gr.TileSum[i];
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
            for (int i = begin; i < end; i++)
            {
                gr.TileStart[i] = running;
                running += gr.TileSum[i];
            }

            if (t == G - 1) gr.Start[cfg.Buckets] = sh[G - 1];
        }

        /// <summary>
        /// The scan's third pass: a tile's buckets from the tile's start. The counts are read and
        /// the starts written a stride of the group apart, and the sum runs over each thread's
        /// ScanPer neighbours in shared memory.
        /// </summary>
        public static void ScanDown(SGrid gr, SCfg cfg)
        {
            var vals = SharedMemory.Allocate<int>(ScanGroupCap * ScanPer);
            var sums = SharedMemory.Allocate<int>(ScanGroupCap);
            int t = Group.IdxX;
            int G = Group.DimX;
            int tile = Grid.IdxX;
            int B = cfg.Buckets;
            int first = tile * G * ScanPer;

            for (int k = 0; k < ScanPer; k++)
            {
                int b = first + k * G + t;
                vals[k * G + t] = b < B ? gr.Counts[b] : 0;
            }
            Group.Barrier();

            int local = 0;
            for (int k = 0; k < ScanPer; k++) local += vals[t * ScanPer + k];
            sums[t] = local;
            Group.Barrier();

            for (int off = 1; off < G; off <<= 1)
            {
                int v = t >= off ? sums[t - off] : 0;
                Group.Barrier();
                sums[t] = sums[t] + v;
                Group.Barrier();
            }

            int running = gr.TileStart[tile] + sums[t] - local;
            for (int k = 0; k < ScanPer; k++)
            {
                int n = vals[t * ScanPer + k];
                vals[t * ScanPer + k] = running;
                running += n;
            }
            Group.Barrier();

            for (int k = 0; k < ScanPer; k++)
            {
                int b = first + k * G + t;
                if (b < B)
                {
                    int s = vals[k * G + t];
                    gr.Start[b] = s;
                    gr.Cursor[b] = s;
                }
            }
        }

        /// <summary>Every row into every bucket it covers. Order within a bucket is a race; the query sorts.</summary>
        public static void Scatter(Index1D index, SGlob gl, SGrid gr, SCfg cfg)
        {
            int row = index.X;
            int R = cfg.Rows;
            if (row >= R) return;
            if (gr.IsGiant[row] != 0) return;

            int lx = gr.Lo[row], hx = gr.Hi[row];
            int ly = gr.Lo[R + row], hy = gr.Hi[R + row];
            int lz = gr.Lo[2 * R + row], hz = gr.Hi[2 * R + row];
            SeamCells(gr.Cell[0], cfg, out int wx, out int wz, out _, out _);

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                int slot = Atomic.Add(ref gr.Cursor[Hash(Wrap(x, wx), y, Wrap(z, wz)) & cfg.Mask], 1);
                if (slot < cfg.EntryCap) gr.Items[slot] = row;
                else Atomic.Add(ref gl.Overflow[OverflowEntries], 1L);
            }
        }

        /// <summary>
        /// After Scatter, a row a thread: each giant whose cell box meets this row's, the row into its
        /// list (the order is the atomics' race; the query sorts). Nothing when no row is a giant.
        /// </summary>
        public static void GiantLists(Index1D index, SGrid gr, SCfg cfg)
        {
            int row = index.X;
            int R = cfg.Rows;
            if (row >= R) return;

            int n = gr.Giant[0];
            if (n == 0) return;
            if (n > GiantCap) n = GiantCap;

            int lx = gr.Lo[row], hx = gr.Hi[row];
            if (lx > hx) return;
            int ly = gr.Lo[R + row], hy = gr.Hi[R + row];
            int lz = gr.Lo[2 * R + row], hz = gr.Hi[2 * R + row];
            SeamCells(gr.Cell[0], cfg, out int wx, out int wz, out _, out _);

            for (int k = 0; k < n; k++)
            {
                int giant = gr.Giant[1 + k];
                if (!BoxesMeet(lx, hx, ly, hy, lz, hz, giant, gr, R, wx, wz)) continue;
                int slot = Atomic.Add(ref gr.GiantN[k], 1);
                if (slot < GiantListCap) gr.GiantList[k * GiantListCap + slot] = row;
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
            // A body the reach guard recorded this step (LoseStrays) dies here, after the streams
            // join, rather than in LoseStrays, beside which the water and the fluid read Alive.
            if (gl.Alive[g] != 0 && gl.StrayCode[g] != 0) gl.Alive[g] = 0;

            if (gl.Alive[g] == 0)
            {
                KeepSpheres(g, links, cs, ps, cfg);
                return;
            }

            int dof = t.Dof[i];
#if GPU_PHASE_PROBE
            long clk = 0;
            if (CudaAsm.IsSupported) CudaAsm.Emit("mov.u64 %0, %%clock64;", out clk);
#endif

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
            var posErr = new Real[3 * MaxLinks];
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
            var sScent = new float[MaxLinks];
            var sLeaf = new float[4 * MaxLinks];   // a part's leaf strength, then its direction's three
            var sFlow = new float[3 * MaxLinks];
            var sAngle = new float[MaxDof];
            var sRate = new float[MaxDof];
            var sDrive = new float[3 * MaxDof];

            var six = new Real[42];
            var sol = new Real[6];
            var residual = new Real[3];
            var cand = new int[CandCap];
            var weldAnchor = new int[MaxLinks];
            var jacobiM = new Real[9];
            var jacobiV = new Real[9];

            // A card does not zero a thread's local memory. The senses the brain may read without
            // this step writing them (a channel whose mask bit is clear) read 0, never garbage.
            for (int k = 0; k < MaxLinks; k++)
            {
                sDepth[k] = 0f; sUp[k] = 0f; sChem[k] = 0f; sScent[k] = 0f;
                sLeaf[4 * k] = 0f; sLeaf[4 * k + 1] = 0f; sLeaf[4 * k + 2] = 0f; sLeaf[4 * k + 3] = 0f;
                sFlow[3 * k] = 0f; sFlow[3 * k + 1] = 0f; sFlow[3 * k + 2] = 0f;
                driveTq[3 * k] = 0; driveTq[3 * k + 1] = 0; driveTq[3 * k + 2] = 0;
            }
            for (int k = 0; k < MaxDof; k++)
            {
                sAngle[k] = 0f; sRate[k] = 0f;
                sDrive[3 * k] = 0f; sDrive[3 * k + 1] = 0f; sDrive[3 * k + 2] = 0f;
            }

            // ---- load ------------------------------------------------------------------------
            // Every read of a link before any of its writes (card-next-r57.md, part 6): the reads are
            // then independent of the stores to the thread's arrays in the PTX itself, so the card can
            // have them all in flight at once rather than one memory latency a value. The same values
            // land in the same places as the loop over k did.
            for (int l = 0; l < links; l++)
            {
                int at = l * N + i;
                int a3 = 3 * l * N + i, a4 = 4 * l * N + i, a9 = 9 * l * N + i, a6 = 6 * l * N + i;
                int parent_ = t.Parent[at];
                int dofCount_ = t.DofCount[at];
                int dofStart_ = t.DofStart[at];
                int panelStart_ = t.PanelStart[at];
                int thinAxis_ = t.ThinAxis[at];
                Real mass_ = b.Mass[at];
                Real lift_ = b.Lift[at];
                Real volume_ = b.Volume[at];
                Real smallI_ = b.SmallI[at];
                Real reach_ = b.Reach[at];
                Real arm_ = b.Arm[at];
                Real inertiaLocal0 = b.Inertia[a3];
                Real childAnchor0 = b.ChildAnchor[a3];
                Real parentAnchor0 = b.ParentAnchor[a3];
                Real position0 = s.Pos[a3];
                Real spin0 = s.Spin[a3];
                Real velocity0 = s.Vel[a3];
                Real relVel0 = s.RelVel[a3];
                Real water0 = s.Water[a3];
                Real wacc0 = s.WaterAcc[a3];
                Real inertiaLocal1 = b.Inertia[a3 + N];
                Real childAnchor1 = b.ChildAnchor[a3 + N];
                Real parentAnchor1 = b.ParentAnchor[a3 + N];
                Real position1 = s.Pos[a3 + N];
                Real spin1 = s.Spin[a3 + N];
                Real velocity1 = s.Vel[a3 + N];
                Real relVel1 = s.RelVel[a3 + N];
                Real water1 = s.Water[a3 + N];
                Real wacc1 = s.WaterAcc[a3 + N];
                Real inertiaLocal2 = b.Inertia[a3 + 2 * N];
                Real childAnchor2 = b.ChildAnchor[a3 + 2 * N];
                Real parentAnchor2 = b.ParentAnchor[a3 + 2 * N];
                Real position2 = s.Pos[a3 + 2 * N];
                Real spin2 = s.Spin[a3 + 2 * N];
                Real velocity2 = s.Vel[a3 + 2 * N];
                Real relVel2 = s.RelVel[a3 + 2 * N];
                Real water2 = s.Water[a3 + 2 * N];
                Real wacc2 = s.WaterAcc[a3 + 2 * N];
                Real jointFrame0 = b.JointFrame[a4];
                Real restFrame0 = b.RestFrame[a4];
                Real ballRot0 = s.BallRot[a4];
                Real rotation0 = s.Rot[a4];
                Real jointFrame1 = b.JointFrame[a4 + N];
                Real restFrame1 = b.RestFrame[a4 + N];
                Real ballRot1 = s.BallRot[a4 + N];
                Real rotation1 = s.Rot[a4 + N];
                Real jointFrame2 = b.JointFrame[a4 + 2 * N];
                Real restFrame2 = b.RestFrame[a4 + 2 * N];
                Real ballRot2 = s.BallRot[a4 + 2 * N];
                Real rotation2 = s.Rot[a4 + 2 * N];
                Real jointFrame3 = b.JointFrame[a4 + 3 * N];
                Real restFrame3 = b.RestFrame[a4 + 3 * N];
                Real ballRot3 = s.BallRot[a4 + 3 * N];
                Real rotation3 = s.Rot[a4 + 3 * N];
                Real rotationMatrix0 = s.RotM[a9];
                Real sang0 = s.Sang[a9];
                Real slin0 = s.Slin[a9];
                Real rotationMatrix1 = s.RotM[a9 + N];
                Real sang1 = s.Sang[a9 + N];
                Real slin1 = s.Slin[a9 + N];
                Real rotationMatrix2 = s.RotM[a9 + 2 * N];
                Real sang2 = s.Sang[a9 + 2 * N];
                Real slin2 = s.Slin[a9 + 2 * N];
                Real rotationMatrix3 = s.RotM[a9 + 3 * N];
                Real sang3 = s.Sang[a9 + 3 * N];
                Real slin3 = s.Slin[a9 + 3 * N];
                Real rotationMatrix4 = s.RotM[a9 + 4 * N];
                Real sang4 = s.Sang[a9 + 4 * N];
                Real slin4 = s.Slin[a9 + 4 * N];
                Real rotationMatrix5 = s.RotM[a9 + 5 * N];
                Real sang5 = s.Sang[a9 + 5 * N];
                Real slin5 = s.Slin[a9 + 5 * N];
                Real rotationMatrix6 = s.RotM[a9 + 6 * N];
                Real sang6 = s.Sang[a9 + 6 * N];
                Real slin6 = s.Slin[a9 + 6 * N];
                Real rotationMatrix7 = s.RotM[a9 + 7 * N];
                Real sang7 = s.Sang[a9 + 7 * N];
                Real slin7 = s.Slin[a9 + 7 * N];
                Real rotationMatrix8 = s.RotM[a9 + 8 * N];
                Real sang8 = s.Sang[a9 + 8 * N];
                Real slin8 = s.Slin[a9 + 8 * N];
                Real cbias0 = s.Cbias[a6];
                Real cbias1 = s.Cbias[a6 + N];
                Real cbias2 = s.Cbias[a6 + 2 * N];
                Real cbias3 = s.Cbias[a6 + 3 * N];
                Real cbias4 = s.Cbias[a6 + 4 * N];
                Real cbias5 = s.Cbias[a6 + 5 * N];

                parent[l] = parent_;
                dofCount[l] = dofCount_;
                dofStart[l] = dofStart_;
                panelStart[l] = panelStart_;
                thinAxis[l] = thinAxis_;
                mass[l] = mass_;
                lift[l] = lift_;
                volume[l] = volume_;
                smallI[l] = smallI_;
                reach[l] = reach_;
                arm[l] = arm_;
                inertiaLocal[3 * l] = inertiaLocal0;
                childAnchor[3 * l] = childAnchor0;
                parentAnchor[3 * l] = parentAnchor0;
                position[3 * l] = position0;
                spin[3 * l] = spin0;
                velocity[3 * l] = velocity0;
                relVel[3 * l] = relVel0;
                water[3 * l] = water0;
                wacc[3 * l] = wacc0;
                inertiaLocal[3 * l + 1] = inertiaLocal1;
                childAnchor[3 * l + 1] = childAnchor1;
                parentAnchor[3 * l + 1] = parentAnchor1;
                position[3 * l + 1] = position1;
                spin[3 * l + 1] = spin1;
                velocity[3 * l + 1] = velocity1;
                relVel[3 * l + 1] = relVel1;
                water[3 * l + 1] = water1;
                wacc[3 * l + 1] = wacc1;
                inertiaLocal[3 * l + 2] = inertiaLocal2;
                childAnchor[3 * l + 2] = childAnchor2;
                parentAnchor[3 * l + 2] = parentAnchor2;
                position[3 * l + 2] = position2;
                spin[3 * l + 2] = spin2;
                velocity[3 * l + 2] = velocity2;
                relVel[3 * l + 2] = relVel2;
                water[3 * l + 2] = water2;
                wacc[3 * l + 2] = wacc2;
                jointFrame[4 * l] = jointFrame0;
                restFrame[4 * l] = restFrame0;
                ballRot[4 * l] = ballRot0;
                rotation[4 * l] = rotation0;
                jointFrame[4 * l + 1] = jointFrame1;
                restFrame[4 * l + 1] = restFrame1;
                ballRot[4 * l + 1] = ballRot1;
                rotation[4 * l + 1] = rotation1;
                jointFrame[4 * l + 2] = jointFrame2;
                restFrame[4 * l + 2] = restFrame2;
                ballRot[4 * l + 2] = ballRot2;
                rotation[4 * l + 2] = rotation2;
                jointFrame[4 * l + 3] = jointFrame3;
                restFrame[4 * l + 3] = restFrame3;
                ballRot[4 * l + 3] = ballRot3;
                rotation[4 * l + 3] = rotation3;
                rotationMatrix[9 * l] = rotationMatrix0;
                sang[9 * l] = sang0;
                slin[9 * l] = slin0;
                rotationMatrix[9 * l + 1] = rotationMatrix1;
                sang[9 * l + 1] = sang1;
                slin[9 * l + 1] = slin1;
                rotationMatrix[9 * l + 2] = rotationMatrix2;
                sang[9 * l + 2] = sang2;
                slin[9 * l + 2] = slin2;
                rotationMatrix[9 * l + 3] = rotationMatrix3;
                sang[9 * l + 3] = sang3;
                slin[9 * l + 3] = slin3;
                rotationMatrix[9 * l + 4] = rotationMatrix4;
                sang[9 * l + 4] = sang4;
                slin[9 * l + 4] = slin4;
                rotationMatrix[9 * l + 5] = rotationMatrix5;
                sang[9 * l + 5] = sang5;
                slin[9 * l + 5] = slin5;
                rotationMatrix[9 * l + 6] = rotationMatrix6;
                sang[9 * l + 6] = sang6;
                slin[9 * l + 6] = slin6;
                rotationMatrix[9 * l + 7] = rotationMatrix7;
                sang[9 * l + 7] = sang7;
                slin[9 * l + 7] = slin7;
                rotationMatrix[9 * l + 8] = rotationMatrix8;
                sang[9 * l + 8] = sang8;
                slin[9 * l + 8] = slin8;
                cbias[6 * l] = cbias0;
                cbias[6 * l + 1] = cbias1;
                cbias[6 * l + 2] = cbias2;
                cbias[6 * l + 3] = cbias3;
                cbias[6 * l + 4] = cbias4;
                cbias[6 * l + 5] = cbias5;
            }
            panelStart[links] = t.PanelStart[links * N + i];

            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                Real lo = b.LimitLo[at], hi = b.LimitHi[at], stiff = b.LimitStiff[at], damp = b.LimitDamp[at];
                Real qv = s.Q[at], qdv = s.Qd[at];
                limitLo[d] = lo; limitHi[d] = hi; limitStiff[d] = stiff; limitDamp[d] = damp;
                q[d] = qv; qd[d] = qdv;
            }
            var basePosition = new V3(s.BasePos[i], s.BasePos[N + i], s.BasePos[2 * N + i]);
            var baseCarry = new V3(s.BaseCarry[i], s.BaseCarry[N + i], s.BaseCarry[2 * N + i]);
            var baseRotation = new Q4(s.BaseRot[i], s.BaseRot[N + i], s.BaseRot[2 * N + i], s.BaseRot[3 * N + i]);

            Real excess = b.Excess[i];
            Real totalMass = b.TotalMass[i];

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 0, ref clk);
#endif
            // ---- 1. the water pass (Water.Sample, per link, no hold) --------------------------
            // WaterLinks ran it before this kernel, a link a thread, from the positions the load
            // read, and wrote the two arrays the load read. Still water left both as they stood,
            // and a current with no acceleration term left the acceleration as it stood.

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 1, ref clk);
#endif
            // ---- 2. senses, 3. brain ----------------------------------------------------------
            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << SDepth)) != 0;
            bool readsUp = (mask & (1 << SUp)) != 0;
            bool readsJoint = (mask & (1 << SJointAngle)) != 0 || (mask & (1 << SJointRate)) != 0;
            bool readsFlow = (mask & (1 << SFlow)) != 0;
            bool readsChemical = (mask & (1 << SChemical)) != 0;
            bool readsEnergy = (mask & (1 << SEnergy)) != 0;
            bool readsScent = (mask & (1 << SScent)) != 0;
            bool readsLeafDir = (mask & (1 << SLeafDir)) != 0;
            bool readsLeaf = (mask & (1 << SLeaf)) != 0 || readsLeafDir;
            bool readsDrive = (mask & (1 << SStroke)) != 0 || (mask & (1 << SHold)) != 0 || (mask & (1 << SRamp)) != 0;

            int gates = br.Gates[i];
            bool hasNutrients = (gates & 1) != 0;
            bool hasReserve = (gates & 2) != 0;
            bool hasScent = (gates & 4) != 0;
            bool hasLeaf = (gates & 8) != 0;

            // D220: a body whose organism is remains runs no senses, no brain, no drive and no grip, as
            // DynamicsWorld.StepOne skips them. cfg.AnyRemains is the block's, so in a world without remains
            // every thread takes the old path on a test it cannot fail, and the flag is never loaded.
            bool remains = cfg.AnyRemains != 0 && gl.Remains[g] != 0;

            // CreatureSenses.SampleDrive: the envelope as the last step's drive left it, read before this
            // step's drive moves it. Under the torque drive it reads 0, as sDrive was cleared.
            if (!remains && readsDrive && cfg.BendAndPower != 0)
            {
                int dofLength = dof > 0 ? dof : 1;
                for (int bl = 0; bl < links; bl++)
                {
                    Real partSum = position[3 * bl] + position[3 * bl + 1] + position[3 * bl + 2];
                    if (partSum != partSum || partSum == Real.PositiveInfinity || partSum == Real.NegativeInfinity) continue;

                    int n = dofCount[bl];
                    int offset = dofStart[bl];
                    if (n == 0 || offset < 0) continue;

                    double hold = b.HoldGene[bl * N + i];
                    for (int d = 0; d < n; d++)
                    {
                        int at = offset + d;
                        if (at >= dofLength) break;

                        int e = at * N + i;
                        double stroke = dr.EPower[e];
                        sDrive[3 * at] = Clamp((float)stroke);
                        sDrive[3 * at + 1] = hold > 0 ? (float)MaxD(0.0, MinD(1.0, dr.EHoldLeft[e] / hold)) : 0f;
                        sDrive[3 * at + 2] = Clamp((float)(dr.EPowerTarget[e] - stroke));
                    }
                }
            }

            float energy = 0f;
            bool lost = false;
            if (!remains)
            {
                Sample(i, links, dofCount, dofStart, position, rotationMatrix, relVel, q, qd, limitHi, b,
                       br, w, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical && hasNutrients,
                       readsScent && hasScent, readsEnergy && hasReserve, readsLeaf && hasLeaf, readsLeafDir,
                       sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, sLeaf, ref energy, ref lost);
            }

            if (lost)
            {
                // The chemical sense's walk gave up on this body (TankCellAt): far beyond the glass,
                // or at a distance that is not finite. It is left unstepped from here, its state as
                // the step found it, and lost as the finiteness check below loses a body; the
                // farm's glass guard kills it at its next check.
                gl.Alive[g] = 0;
                KeepSpheres(g, links, cs, ps, cfg);
                ps.R[g] = 0;
                ps.Active[g] = 0;

                if (cfg.PerPart != 0)
                {
                    for (int l = 0; l < links; l++) ps.LR[g * LinkStride + l] = 0;
                }

                return;
            }

            // The touch of the rock as the last step's contact pass left it, a bit a link, read before
            // this step's touch is kept (KeepTouch), as CreatureSenses.Sample reads it.
            int rock = 0;
            if (!remains && cfg.RockTouch != 0)
            {
                for (int l = 0; l < links; l++)
                {
                    if (gl.RockTouched[g * LinkStride + l] != 0) rock |= 1 << l;
                }
            }

            // D235: in a nose world a part that is not a nose reads zero on the four smell channels, whatever the
            // gates (SensorChannels.IsSmell), the chemical sense's standing-in constant included. The nose links ride
            // in the same word as the rock's bits, a bit a link from bit 16 (a class holds at most 16 links), so
            // that SensorRead, which already takes the word, needs no new argument. SRock reads only bits 0 to 15.
            if (!remains && cfg.Nose != 0)
            {
                for (int l = 0; l < links; l++)
                {
                    if (b.NoseFlag[l * N + i] != 0) rock |= 1 << (16 + l);
                }
            }

            // The brain reads the two gates itself (SensorRead): a body with no field or no
            // reserve source reads the standing-in constant on those channels. Remains run none.
            if (!remains)
            {
                BrainStep(i, links, dof, t, nr, br, dr, cfg, dofCount, dofStart,
                          sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf);
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 2, ref clk);
#endif
            // ---- clear -----------------------------------------------------------------------
            for (int k = 0; k < 6 * links; k++) fext[k] = 0;
            for (int k = 0; k < dof; k++) tau[k] = 0;

            // ---- 4. drive (EffectorDrive.Drive) ----------------------------------------------
            bool drivePending = false;
            if (!remains && dof != 0 && cfg.BendAndPower != 0)
            {
                // EffectorDrive.DriveBendAndPower: each degree of freedom's power and bend through
                // the joint's threshold and envelope, in double as the CPU keeps them; the power
                // stays in EPower for the fluid's push below, and a critically damped muscle holds
                // the bend, capped by the part's power.
                int sigLen = t.SigLen[i];
                float powerScale = b.PowerScale[i];
                double stepD = cfg.DtD;
                double poseOmega = cfg.PoseOmega;

                long limited = 0;

                for (int bl = 1; bl < links; bl++)
                {
                    int n = dofCount[bl];
                    if (n == 0) continue;

                    int offset = dofStart[bl];
                    int gene = bl * N + i;
                    int mirror = cfg.MirrorDrive != 0 ? b.MirrorMask[gene] : 0;   // D234, Creature.MirrorMask
                    double threshold = b.Threshold[gene];
                    double reaction = b.Reaction[gene];
                    double holdGene = b.HoldGene[gene];
                    double release = b.Release[gene];
                    Q4 frame = Q4.Read(jointFrame, 4 * bl);

                    double allowed = cfg.LimitDrive != 0 ? (double)(smallI[bl] * cfg.SpinBudget) : double.MaxValue;
                    double cap = MinD(allowed, (double)b.LinkPower[gene] * powerScale);
                    double inertia = smallI[bl];

                    V3 torque = V3.Zero;

                    for (int d = 0; d < n; d++)
                    {
                        int idx = offset + d;
                        int e = idx * N + i;

                        float v = idx < sigLen ? dr.Signal[e] : 0f;
                        double level = MaxF(0f, MinF(1f, v));
                        double power = level <= threshold ? 0.0 : MinD(1.0, (level - threshold) / (1.0 - threshold));
                        float wv = idx < sigLen ? dr.Bend[e] : 0f;
                        double bend = (MaxF(-1f, MinF(1f, wv)) + 1.0) * 0.5;

                        double pw = dr.EPower[e], bd = dr.EBend[e];
                        double pt = dr.EPowerTarget[e], bt = dr.EBendTarget[e];
                        double pr = dr.EPowerRate[e], br8 = dr.EBendRate[e];
                        double holdLeft = dr.EHoldLeft[e];

                        if (holdLeft > 0)
                        {
                            holdLeft -= stepD;
                        }
                        else if (AbsD(power - pt) > 0.01 || AbsD(bend - bt) > 0.01)
                        {
                            bool fromRest = pw == 0 && pt == 0;
                            double ramp = fromRest ? reaction : release;
                            if (ramp < stepD) ramp = stepD;
                            pt = power;
                            bt = bend;
                            pr = AbsD(power - pw) / ramp;
                            br8 = AbsD(bend - bd) / ramp;
                            holdLeft = holdGene;
                        }

                        pw = Toward(pw, pt, pr * stepD);
                        bd = Toward(bd, bt, br8 * stepD);

                        dr.EPower[e] = pw; dr.EBend[e] = bd;
                        dr.EPowerTarget[e] = pt; dr.EBendTarget[e] = bt;
                        dr.EPowerRate[e] = pr; dr.EBendRate[e] = br8;
                        dr.EHoldLeft[e] = holdLeft;

                        if (poseOmega <= 0 || inertia <= 0) continue;

                        // D234, EffectorDrive.DriveBendAndPower: a reflected degree of freedom works in the signed
                        // reading, the genome's own stops (the stored ones swapped and negated back), the signed
                        // angle and rate, and the torque reversed back to the stored angle. The sign is exactly 1
                        // everywhere else, and in a world without the rule no mask is read at all.
                        bool flipped = mirror != 0 && ((mirror >> d) & 1) != 0;
                        double sign = flipped ? -1.0 : 1.0;
                        double lo = flipped ? -(double)limitHi[idx] : (double)limitLo[idx];
                        double hi = flipped ? -(double)limitLo[idx] : (double)limitHi[idx];
                        double target = lo + bd * (hi - lo);
                        double magnitude = inertia * poseOmega * (poseOmega * (target - sign * q[idx]) - 2.0 * (sign * qd[idx]));
                        if (magnitude > cap) { magnitude = cap; limited++; }
                        else if (magnitude < -cap) { magnitude = -cap; limited++; }

                        torque = torque + frame.Rotate(V3.Axis(d)) * (Real)(sign * magnitude);
                    }

                    M3 rot = M3.Read(rotationMatrix, 9 * bl);
                    V3 world = rot * torque;

                    s.Applied[(3 * bl) * N + i] = world.X;
                    s.Applied[(3 * bl + 1) * N + i] = world.Y;
                    s.Applied[(3 * bl + 2) * N + i] = world.Z;

                    V3.Write(driveTq, 3 * bl, world);
                    V3.Write(preRelSpin, 3 * bl, V3.Read(spin, 3 * bl) - V3.Read(spin, 3 * parent[bl]));
                    drivePending = true;

                    V3.Add(fext, 6 * bl, world);
                    V3.Add(fext, 6 * parent[bl], -world);
                }

                if (limited != 0) dr.DriveLimited[i] = dr.DriveLimited[i] + limited;
            }
            else if (!remains && dof != 0)
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
                    int mirror = cfg.MirrorDrive != 0 ? b.MirrorMask[bl * N + i] : 0;   // D234, Creature.MirrorMask
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

                        // D234: a reflected degree of freedom's torque is reversed, after the limiter.
                        if (((mirror >> d) & 1) != 0) magnitude = -magnitude;

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

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 3, ref clk);
#endif
            // ---- 5. fluid (Fluid.Apply) --------------------------------------------------------
            // FluidLinks ran it before this kernel, a link a thread; its results are taken here in
            // link order, at the place the CPU applies them.
            long dragLimited = 0;
            int fluidRows = cfg.LinkRows;
            bool powered = cfg.BendAndPower != 0 && cfg.LimbPush > 0 && cfg.PushOmega > 0;
            for (int fl = 0; fl < links; fl++)
            {
                int frow = g * LinkStride + fl;
                for (int fk = 0; fk < 3; fk++)
                {
                    relVel[3 * fl + fk] = gl.LinkFluid[fk * fluidRows + frow];
                    dragF[3 * fl + fk] = gl.LinkFluid[(3 + fk) * fluidRows + frow];
                    dragT[3 * fl + fk] = gl.LinkFluid[(6 + fk) * fluidRows + frow];
                    preVel[3 * fl + fk] = velocity[3 * fl + fk];
                    preSpin[3 * fl + fk] = spin[3 * fl + fk];
                }
                V3.Add(fext, 6 * fl, new V3(gl.LinkFluid[9 * fluidRows + frow], gl.LinkFluid[10 * fluidRows + frow],
                                            gl.LinkFluid[11 * fluidRows + frow]));
                V3 linkForce = new V3(gl.LinkFluid[12 * fluidRows + frow], gl.LinkFluid[13 * fluidRows + frow],
                                      gl.LinkFluid[14 * fluidRows + frow]);

                // Under the bend-and-power drive FluidLinks left the force before the power's push
                // and the buoyancy term: the push from this step's stroke, its work booked as
                // Fluid.Apply books it, then the buoyancy where the CPU added it.
                if (powered)
                {
                    if (!remains && parent[fl] >= 0 && position[3 * fl + 1] <= 0 && dofCount[fl] > 0)
                    {
                        M3 frot = M3.Read(rotationMatrix, 9 * fl);
                        V3 toOrigin = frot * (-V3.Read(childAnchor, 3 * fl));
                        Real linkReach = toOrigin.Magnitude;
                        if (linkReach > 0)
                        {
                            V3 axis = toOrigin * ((Real)1.0 / linkReach);
                            Q4 fframe = Q4.Read(jointFrame, 4 * fl);
                            int fstart = dofStart[fl];
                            Real omega = cfg.PushOmega;
                            Real swing2 = 0;

                            // D229, as Fluid.Apply: each degree of freedom moves the limb in the unit
                            // vector of dofAxis x axis, and the added mass is the part's for it. A sphere
                            // (shape 0) keeps the unshaped line.
                            int shapeCode = cfg.ShapedMass != 0 ? b.ShapeCode[fl * N + i] : 0;
                            Real hx0 = 0, hx1 = 0, hx2 = 0;

                            // D255, as Fluid.Apply: a family limb (shape code 3) takes the strip integral its host body
                            // carries for each degree of freedom, in place of D229's lines. It reads no half-extents.
                            // Not entered for any other part, whose lines below are unchanged.
                            bool family = shapeCode == 3;
                            Real familySum = 0;
                            Real familyMass = 0;
                            if (shapeCode != 0 && !family)
                            {
                                hx0 = b.HalfExt[(3 * fl) * N + i];
                                hx1 = b.HalfExt[(3 * fl + 1) * N + i];
                                hx2 = b.HalfExt[(3 * fl + 2) * N + i];
                            }
                            Real shapedSum = 0;

                            // D231, as Fluid.Apply: a link with parts welded beyond it takes its constants from
                            // the tables, and none of the lines below. Not entered with the switch off.
                            bool assembly = cfg.StrokeAssembly != 0 && b.StrokeParts[fl * N + i] > 1;
                            Real assemblySum = 0;
                            Real assemblyMass = 0;

                            for (int d = 0; d < dofCount[fl]; d++)
                            {
                                Real sv = (Real)dr.EPower[(fstart + d) * N + i];
                                if (sv <= 0) continue;

                                if (assembly)
                                {
                                    // q_d = (s_d Omega)^2 (1 - along_d^2), with the along of the link's own frame.
                                    Real alongStored = b.StrokeAlong[(fstart + d) * N + i];
                                    Real strokeQ = sv * omega * sv * omega * ((Real)1.0 - alongStored * alongStored);
                                    swing2 += strokeQ;
                                    assemblySum += strokeQ * b.StrokeMoment[(fstart + d) * N + i];
                                    assemblyMass += strokeQ * b.StrokeMass[(fstart + d) * N + i];
                                    continue;
                                }

                                V3 dofAxis = frot * fframe.Rotate(V3.Axis(d));
                                Real along = V3.Dot(dofAxis, axis);
                                swing2 += sv * omega * sv * omega * ((Real)1.0 - along * along);

                                if (family)
                                {
                                    // q_d as above, times the degree's I1 and M, which the host wrote at build and
                                    // at every resize (Fluid.Apply's familyQ).
                                    Real familyQ = sv * omega * sv * omega * ((Real)1.0 - along * along);
                                    familySum += familyQ * b.FamMoment[(fstart + d) * N + i];
                                    familyMass += familyQ * b.FamMass[(fstart + d) * N + i];
                                }

                                if (shapeCode != 0 && !family)
                                {
                                    V3 motion = V3.Cross(dofAxis, axis);
                                    Real motionLength = motion.Magnitude;
                                    if (motionLength > 0)
                                    {
                                        V3 strokeDirection = motion * ((Real)1.0 / motionLength);
                                        shapedSum += LeverOf(linkReach, axis, strokeDirection) *
                                            ShapedAddedMass(
                                                shapeCode, hx0, hx1, hx2, frot, axis,
                                                strokeDirection, cfg.Density) *
                                            (sv * omega * sv * omega * ((Real)1.0 - along * along));
                                    }
                                }
                            }

                            if (swing2 > 0)
                            {
                                Real addedMass = cfg.AddedMass * cfg.Density * volume[fl];
                                Real push = cfg.LimbPush * addedMass * linkReach * swing2;
                                if (shapeCode != 0 && !family) push = cfg.LimbPush * shapedSum;

                                // D231: C_T times the sum of q_d I1_d, and the push-weighted lever
                                // (sum q_d I1_d) / (sum q_d M_d). Another link's lever is the reach.
                                Real lever = linkReach;
                                if (assembly)
                                {
                                    push = cfg.LimbPush * assemblySum;
                                    lever = AssemblyLeverOf(assemblySum, assemblyMass, linkReach);
                                }
                                else if (family)
                                {
                                    // D255: the same law for a family limb, from the strip integral of its true slices.
                                    push = cfg.LimbPush * familySum;
                                    lever = AssemblyLeverOf(familySum, familyMass, linkReach);
                                }

                                linkForce = linkForce + axis * (-push);
                                // The work is the push times the lever it acts at: a lever other than the reach
                                // (LeverOf) must enter this line too, as on the CPU.
                                dr.Work[i] = dr.Work[i] + (double)(push * lever * System.Math.Sqrt(swing2)) * cfg.DtD;
                            }
                        }
                    }

                    if (gl.LinkFluid[16 * fluidRows + frow] != 0)
                    {
                        linkForce = linkForce + new V3(0, gl.LinkFluid[15 * fluidRows + frow], 0);
                    }
                }

                V3.Add(fext, 6 * fl + 3, linkForce);
                dragLimited += gl.LinkLimited[frow];
            }
            if (cfg.LimitDrag != 0 && cfg.WeldedDrag != 0)
            {
                dragLimited += WeldedDrag(g * LinkStride, fluidRows, links, parent, dofCount, mass, position,
                                          rotationMatrix, inertiaLocal, spin, dragF, dragT, fext, weldAnchor,
                                          jacobiM, jacobiV, gl, cfg);
            }
            if (dragLimited != 0) dr.DragLimited[i] = dr.DragLimited[i] + dragLimited;

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 4, ref clk);
#endif
            // ---- 6. contacts (Contacts.Apply) ------------------------------------------------
            if (cfg.PerPart != 0)
            {
                ContactsPerPartStaged(g, links, fext, cand, cs, gl, gr, cfg);
                if (cfg.RockTouch != 0) KeepTouch(g, links, gl, cfg);
                if (cfg.Anchoring != 0 && !remains)
                {
                    Grip(g, links, position, velocity, spin, rotationMatrix, mass, smallI, fext,
                         hasReserve, br.Reserve[i], cs, gl, cfg);
                }
            }
            else
            {
                ContactsBody(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 5, ref clk);
#endif
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
                      ref basePosition, ref baseCarry, ref baseRotation, cfg.Dt);

            // D077's third rule inside the card's block (D204): a root that has left the box comes
            // back in at the opposite face, before the poses are built from it, as Simulation's
            // WrapAtTheSeams puts it back before anything reads a position.
            if (cfg.SeamL > 0) WrapAtTheSeams(ref basePosition, ref baseCarry, cfg);

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 6, ref clk);
#endif
            // ---- 10. poses, 11. velocities ----------------------------------------------------
            Poses(links, parent, dofCount, dofStart, q, ballRot, rotation, rotationMatrix,
                  position, restFrame, jointFrame, parentAnchor, childAnchor,
                  basePosition, baseRotation, baseCarry, posErr);
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

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 7, ref clk);
#endif
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
                        ps.LEx[row] = CarryBase ? posErr[0] : 0;
                        ps.LEy[row] = CarryBase ? posErr[1] : 0;
                        ps.LEz[row] = CarryBase ? posErr[2] : 0;
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
                            ps.LEx[row + l] = CarryBase ? posErr[3 * l] : 0;
                            ps.LEy[row + l] = CarryBase ? posErr[3 * l + 1] : 0;
                            ps.LEz[row + l] = CarryBase ? posErr[3 * l + 2] : 0;
                            ps.LR[row + l] = reach[l];
                            ps.LVx[row + l] = velocity[3 * l];
                            ps.LVy[row + l] = velocity[3 * l + 1];
                            ps.LVz[row + l] = velocity[3 * l + 2];
                        }
                    }

                    // D228: each link's turn travels with its sphere (RefreshLinkSpheres copies RotationMatrix).
                    if (cfg.TrueShape != 0)
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LRot0[row + l] = rotationMatrix[9 * l];
                            ps.LRot1[row + l] = rotationMatrix[9 * l + 1];
                            ps.LRot2[row + l] = rotationMatrix[9 * l + 2];
                            ps.LRot3[row + l] = rotationMatrix[9 * l + 3];
                            ps.LRot4[row + l] = rotationMatrix[9 * l + 4];
                            ps.LRot5[row + l] = rotationMatrix[9 * l + 5];
                            ps.LRot6[row + l] = rotationMatrix[9 * l + 6];
                            ps.LRot7[row + l] = rotationMatrix[9 * l + 7];
                            ps.LRot8[row + l] = rotationMatrix[9 * l + 8];
                        }
                    }

                    // D228 level (b): each link's world-frame spin travels with its sphere (RefreshLinkSpheres copies Spin).
                    if (cfg.AtPoint != 0)
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LSp0[row + l] = spin[3 * l];
                            ps.LSp1[row + l] = spin[3 * l + 1];
                            ps.LSp2[row + l] = spin[3 * l + 2];
                        }
                    }
                }
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 8, ref clk);
#endif
            // ---- store -----------------------------------------------------------------------
            s.BasePos[i] = basePosition.X;
            s.BasePos[N + i] = basePosition.Y;
            s.BasePos[2 * N + i] = basePosition.Z;
            s.BaseCarry[i] = baseCarry.X;
            s.BaseCarry[N + i] = baseCarry.Y;
            s.BaseCarry[2 * N + i] = baseCarry.Z;
            s.BaseRot[i] = baseRotation.X;
            s.BaseRot[N + i] = baseRotation.Y;
            s.BaseRot[2 * N + i] = baseRotation.Z;
            s.BaseRot[3 * N + i] = baseRotation.W;

            // Every read of a link from the thread's arrays before any of its writes, as in the load.
            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                Real qv = q[d], qdv = qd[d], tv = tau[d], li = limitImplicit[d];
                s.Q[at] = qv; s.Qd[at] = qdv; s.Tau[at] = tv; s.LimitImplicit[at] = li;
            }

            for (int l = 0; l < links; l++)
            {
                int a3 = 3 * l * N + i, a4 = 4 * l * N + i, a9 = 9 * l * N + i, a6 = 6 * l * N + i;
                Real position0 = position[3 * l];
                Real spin0 = spin[3 * l];
                Real velocity0 = velocity[3 * l];
                Real relVel0 = relVel[3 * l];
                Real water0 = water[3 * l];
                Real wacc0 = wacc[3 * l];
                Real position1 = position[3 * l + 1];
                Real spin1 = spin[3 * l + 1];
                Real velocity1 = velocity[3 * l + 1];
                Real relVel1 = relVel[3 * l + 1];
                Real water1 = water[3 * l + 1];
                Real wacc1 = wacc[3 * l + 1];
                Real position2 = position[3 * l + 2];
                Real spin2 = spin[3 * l + 2];
                Real velocity2 = velocity[3 * l + 2];
                Real relVel2 = relVel[3 * l + 2];
                Real water2 = water[3 * l + 2];
                Real wacc2 = wacc[3 * l + 2];
                Real ballRot0 = ballRot[4 * l];
                Real rotation0 = rotation[4 * l];
                Real ballRot1 = ballRot[4 * l + 1];
                Real rotation1 = rotation[4 * l + 1];
                Real ballRot2 = ballRot[4 * l + 2];
                Real rotation2 = rotation[4 * l + 2];
                Real ballRot3 = ballRot[4 * l + 3];
                Real rotation3 = rotation[4 * l + 3];
                Real rotationMatrix0 = rotationMatrix[9 * l];
                Real sang0 = sang[9 * l];
                Real slin0 = slin[9 * l];
                Real rotationMatrix1 = rotationMatrix[9 * l + 1];
                Real sang1 = sang[9 * l + 1];
                Real slin1 = slin[9 * l + 1];
                Real rotationMatrix2 = rotationMatrix[9 * l + 2];
                Real sang2 = sang[9 * l + 2];
                Real slin2 = slin[9 * l + 2];
                Real rotationMatrix3 = rotationMatrix[9 * l + 3];
                Real sang3 = sang[9 * l + 3];
                Real slin3 = slin[9 * l + 3];
                Real rotationMatrix4 = rotationMatrix[9 * l + 4];
                Real sang4 = sang[9 * l + 4];
                Real slin4 = slin[9 * l + 4];
                Real rotationMatrix5 = rotationMatrix[9 * l + 5];
                Real sang5 = sang[9 * l + 5];
                Real slin5 = slin[9 * l + 5];
                Real rotationMatrix6 = rotationMatrix[9 * l + 6];
                Real sang6 = sang[9 * l + 6];
                Real slin6 = slin[9 * l + 6];
                Real rotationMatrix7 = rotationMatrix[9 * l + 7];
                Real sang7 = sang[9 * l + 7];
                Real slin7 = slin[9 * l + 7];
                Real rotationMatrix8 = rotationMatrix[9 * l + 8];
                Real sang8 = sang[9 * l + 8];
                Real slin8 = slin[9 * l + 8];
                Real cbias0 = cbias[6 * l];
                Real fext0 = fext[6 * l];
                Real cbias1 = cbias[6 * l + 1];
                Real fext1 = fext[6 * l + 1];
                Real cbias2 = cbias[6 * l + 2];
                Real fext2 = fext[6 * l + 2];
                Real cbias3 = cbias[6 * l + 3];
                Real fext3 = fext[6 * l + 3];
                Real cbias4 = cbias[6 * l + 4];
                Real fext4 = fext[6 * l + 4];
                Real cbias5 = cbias[6 * l + 5];
                Real fext5 = fext[6 * l + 5];

                s.Pos[a3] = position0;
                s.Spin[a3] = spin0;
                s.Vel[a3] = velocity0;
                s.RelVel[a3] = relVel0;
                s.Water[a3] = water0;
                s.WaterAcc[a3] = wacc0;
                s.Pos[a3 + N] = position1;
                s.Spin[a3 + N] = spin1;
                s.Vel[a3 + N] = velocity1;
                s.RelVel[a3 + N] = relVel1;
                s.Water[a3 + N] = water1;
                s.WaterAcc[a3 + N] = wacc1;
                s.Pos[a3 + 2 * N] = position2;
                s.Spin[a3 + 2 * N] = spin2;
                s.Vel[a3 + 2 * N] = velocity2;
                s.RelVel[a3 + 2 * N] = relVel2;
                s.Water[a3 + 2 * N] = water2;
                s.WaterAcc[a3 + 2 * N] = wacc2;
                s.BallRot[a4] = ballRot0;
                s.Rot[a4] = rotation0;
                s.BallRot[a4 + N] = ballRot1;
                s.Rot[a4 + N] = rotation1;
                s.BallRot[a4 + 2 * N] = ballRot2;
                s.Rot[a4 + 2 * N] = rotation2;
                s.BallRot[a4 + 3 * N] = ballRot3;
                s.Rot[a4 + 3 * N] = rotation3;
                s.RotM[a9] = rotationMatrix0;
                s.Sang[a9] = sang0;
                s.Slin[a9] = slin0;
                s.RotM[a9 + N] = rotationMatrix1;
                s.Sang[a9 + N] = sang1;
                s.Slin[a9 + N] = slin1;
                s.RotM[a9 + 2 * N] = rotationMatrix2;
                s.Sang[a9 + 2 * N] = sang2;
                s.Slin[a9 + 2 * N] = slin2;
                s.RotM[a9 + 3 * N] = rotationMatrix3;
                s.Sang[a9 + 3 * N] = sang3;
                s.Slin[a9 + 3 * N] = slin3;
                s.RotM[a9 + 4 * N] = rotationMatrix4;
                s.Sang[a9 + 4 * N] = sang4;
                s.Slin[a9 + 4 * N] = slin4;
                s.RotM[a9 + 5 * N] = rotationMatrix5;
                s.Sang[a9 + 5 * N] = sang5;
                s.Slin[a9 + 5 * N] = slin5;
                s.RotM[a9 + 6 * N] = rotationMatrix6;
                s.Sang[a9 + 6 * N] = sang6;
                s.Slin[a9 + 6 * N] = slin6;
                s.RotM[a9 + 7 * N] = rotationMatrix7;
                s.Sang[a9 + 7 * N] = sang7;
                s.Slin[a9 + 7 * N] = slin7;
                s.RotM[a9 + 8 * N] = rotationMatrix8;
                s.Sang[a9 + 8 * N] = sang8;
                s.Slin[a9 + 8 * N] = slin8;
                s.Cbias[a6] = cbias0;
                s.Fext[a6] = fext0;
                s.Cbias[a6 + N] = cbias1;
                s.Fext[a6 + N] = fext1;
                s.Cbias[a6 + 2 * N] = cbias2;
                s.Fext[a6 + 2 * N] = fext2;
                s.Cbias[a6 + 3 * N] = cbias3;
                s.Fext[a6 + 3 * N] = fext3;
                s.Cbias[a6 + 4 * N] = cbias4;
                s.Fext[a6 + 4 * N] = fext4;
                s.Cbias[a6 + 5 * N] = cbias5;
                s.Fext[a6 + 5 * N] = fext5;
            }
#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 9, ref clk);
#endif
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
            // A body the reach guard recorded this step (LoseStrays) dies here, after the streams
            // join, rather than in LoseStrays, beside which the water and the fluid read Alive.
            if (gl.Alive[g] != 0 && gl.StrayCode[g] != 0) gl.Alive[g] = 0;

            if (gl.Alive[g] == 0)
            {
                KeepSpheres(g, links, cs, ps, cfg);
                return;
            }

            int dof = t.Dof[i];
#if GPU_PHASE_PROBE
            long clk = 0;
            if (CudaAsm.IsSupported) CudaAsm.Emit("mov.u64 %0, %%clock64;", out clk);
#endif

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
            var posErr = new Real[3 * MaxLinks];
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
            var sScent = new float[MaxLinks];
            var sLeaf = new float[4 * MaxLinks];   // a part's leaf strength, then its direction's three
            var sFlow = new float[3 * MaxLinks];
            var sAngle = new float[MaxDof];
            var sRate = new float[MaxDof];
            var sDrive = new float[3 * MaxDof];

            var six = new Real[42];
            var sol = new Real[6];
            var residual = new Real[3];
            var cand = new int[CandCap];
            var weldAnchor = new int[MaxLinks];
            var jacobiM = new Real[9];
            var jacobiV = new Real[9];

            // A card does not zero a thread's local memory. The senses the brain may read without
            // this step writing them (a channel whose mask bit is clear) read 0, never garbage.
            for (int k = 0; k < MaxLinks; k++)
            {
                sDepth[k] = 0f; sUp[k] = 0f; sChem[k] = 0f; sScent[k] = 0f;
                sLeaf[4 * k] = 0f; sLeaf[4 * k + 1] = 0f; sLeaf[4 * k + 2] = 0f; sLeaf[4 * k + 3] = 0f;
                sFlow[3 * k] = 0f; sFlow[3 * k + 1] = 0f; sFlow[3 * k + 2] = 0f;
                driveTq[3 * k] = 0; driveTq[3 * k + 1] = 0; driveTq[3 * k + 2] = 0;
            }
            for (int k = 0; k < MaxDof; k++)
            {
                sAngle[k] = 0f; sRate[k] = 0f;
                sDrive[3 * k] = 0f; sDrive[3 * k + 1] = 0f; sDrive[3 * k + 2] = 0f;
            }

            // ---- load ------------------------------------------------------------------------
            // Every read of a link before any of its writes (card-next-r57.md, part 6): the reads are
            // then independent of the stores to the thread's arrays in the PTX itself, so the card can
            // have them all in flight at once rather than one memory latency a value. The same values
            // land in the same places as the loop over k did.
            for (int l = 0; l < links; l++)
            {
                int at = l * N + i;
                int a3 = 3 * l * N + i, a4 = 4 * l * N + i, a9 = 9 * l * N + i, a6 = 6 * l * N + i;
                int parent_ = t.Parent[at];
                int dofCount_ = t.DofCount[at];
                int dofStart_ = t.DofStart[at];
                int panelStart_ = t.PanelStart[at];
                int thinAxis_ = t.ThinAxis[at];
                Real mass_ = b.Mass[at];
                Real lift_ = b.Lift[at];
                Real volume_ = b.Volume[at];
                Real smallI_ = b.SmallI[at];
                Real reach_ = b.Reach[at];
                Real arm_ = b.Arm[at];
                Real inertiaLocal0 = b.Inertia[a3];
                Real childAnchor0 = b.ChildAnchor[a3];
                Real parentAnchor0 = b.ParentAnchor[a3];
                Real position0 = s.Pos[a3];
                Real spin0 = s.Spin[a3];
                Real velocity0 = s.Vel[a3];
                Real relVel0 = s.RelVel[a3];
                Real water0 = s.Water[a3];
                Real wacc0 = s.WaterAcc[a3];
                Real inertiaLocal1 = b.Inertia[a3 + N];
                Real childAnchor1 = b.ChildAnchor[a3 + N];
                Real parentAnchor1 = b.ParentAnchor[a3 + N];
                Real position1 = s.Pos[a3 + N];
                Real spin1 = s.Spin[a3 + N];
                Real velocity1 = s.Vel[a3 + N];
                Real relVel1 = s.RelVel[a3 + N];
                Real water1 = s.Water[a3 + N];
                Real wacc1 = s.WaterAcc[a3 + N];
                Real inertiaLocal2 = b.Inertia[a3 + 2 * N];
                Real childAnchor2 = b.ChildAnchor[a3 + 2 * N];
                Real parentAnchor2 = b.ParentAnchor[a3 + 2 * N];
                Real position2 = s.Pos[a3 + 2 * N];
                Real spin2 = s.Spin[a3 + 2 * N];
                Real velocity2 = s.Vel[a3 + 2 * N];
                Real relVel2 = s.RelVel[a3 + 2 * N];
                Real water2 = s.Water[a3 + 2 * N];
                Real wacc2 = s.WaterAcc[a3 + 2 * N];
                Real jointFrame0 = b.JointFrame[a4];
                Real restFrame0 = b.RestFrame[a4];
                Real ballRot0 = s.BallRot[a4];
                Real rotation0 = s.Rot[a4];
                Real jointFrame1 = b.JointFrame[a4 + N];
                Real restFrame1 = b.RestFrame[a4 + N];
                Real ballRot1 = s.BallRot[a4 + N];
                Real rotation1 = s.Rot[a4 + N];
                Real jointFrame2 = b.JointFrame[a4 + 2 * N];
                Real restFrame2 = b.RestFrame[a4 + 2 * N];
                Real ballRot2 = s.BallRot[a4 + 2 * N];
                Real rotation2 = s.Rot[a4 + 2 * N];
                Real jointFrame3 = b.JointFrame[a4 + 3 * N];
                Real restFrame3 = b.RestFrame[a4 + 3 * N];
                Real ballRot3 = s.BallRot[a4 + 3 * N];
                Real rotation3 = s.Rot[a4 + 3 * N];
                Real rotationMatrix0 = s.RotM[a9];
                Real sang0 = s.Sang[a9];
                Real slin0 = s.Slin[a9];
                Real rotationMatrix1 = s.RotM[a9 + N];
                Real sang1 = s.Sang[a9 + N];
                Real slin1 = s.Slin[a9 + N];
                Real rotationMatrix2 = s.RotM[a9 + 2 * N];
                Real sang2 = s.Sang[a9 + 2 * N];
                Real slin2 = s.Slin[a9 + 2 * N];
                Real rotationMatrix3 = s.RotM[a9 + 3 * N];
                Real sang3 = s.Sang[a9 + 3 * N];
                Real slin3 = s.Slin[a9 + 3 * N];
                Real rotationMatrix4 = s.RotM[a9 + 4 * N];
                Real sang4 = s.Sang[a9 + 4 * N];
                Real slin4 = s.Slin[a9 + 4 * N];
                Real rotationMatrix5 = s.RotM[a9 + 5 * N];
                Real sang5 = s.Sang[a9 + 5 * N];
                Real slin5 = s.Slin[a9 + 5 * N];
                Real rotationMatrix6 = s.RotM[a9 + 6 * N];
                Real sang6 = s.Sang[a9 + 6 * N];
                Real slin6 = s.Slin[a9 + 6 * N];
                Real rotationMatrix7 = s.RotM[a9 + 7 * N];
                Real sang7 = s.Sang[a9 + 7 * N];
                Real slin7 = s.Slin[a9 + 7 * N];
                Real rotationMatrix8 = s.RotM[a9 + 8 * N];
                Real sang8 = s.Sang[a9 + 8 * N];
                Real slin8 = s.Slin[a9 + 8 * N];
                Real cbias0 = s.Cbias[a6];
                Real cbias1 = s.Cbias[a6 + N];
                Real cbias2 = s.Cbias[a6 + 2 * N];
                Real cbias3 = s.Cbias[a6 + 3 * N];
                Real cbias4 = s.Cbias[a6 + 4 * N];
                Real cbias5 = s.Cbias[a6 + 5 * N];

                parent[l] = parent_;
                dofCount[l] = dofCount_;
                dofStart[l] = dofStart_;
                panelStart[l] = panelStart_;
                thinAxis[l] = thinAxis_;
                mass[l] = mass_;
                lift[l] = lift_;
                volume[l] = volume_;
                smallI[l] = smallI_;
                reach[l] = reach_;
                arm[l] = arm_;
                inertiaLocal[3 * l] = inertiaLocal0;
                childAnchor[3 * l] = childAnchor0;
                parentAnchor[3 * l] = parentAnchor0;
                position[3 * l] = position0;
                spin[3 * l] = spin0;
                velocity[3 * l] = velocity0;
                relVel[3 * l] = relVel0;
                water[3 * l] = water0;
                wacc[3 * l] = wacc0;
                inertiaLocal[3 * l + 1] = inertiaLocal1;
                childAnchor[3 * l + 1] = childAnchor1;
                parentAnchor[3 * l + 1] = parentAnchor1;
                position[3 * l + 1] = position1;
                spin[3 * l + 1] = spin1;
                velocity[3 * l + 1] = velocity1;
                relVel[3 * l + 1] = relVel1;
                water[3 * l + 1] = water1;
                wacc[3 * l + 1] = wacc1;
                inertiaLocal[3 * l + 2] = inertiaLocal2;
                childAnchor[3 * l + 2] = childAnchor2;
                parentAnchor[3 * l + 2] = parentAnchor2;
                position[3 * l + 2] = position2;
                spin[3 * l + 2] = spin2;
                velocity[3 * l + 2] = velocity2;
                relVel[3 * l + 2] = relVel2;
                water[3 * l + 2] = water2;
                wacc[3 * l + 2] = wacc2;
                jointFrame[4 * l] = jointFrame0;
                restFrame[4 * l] = restFrame0;
                ballRot[4 * l] = ballRot0;
                rotation[4 * l] = rotation0;
                jointFrame[4 * l + 1] = jointFrame1;
                restFrame[4 * l + 1] = restFrame1;
                ballRot[4 * l + 1] = ballRot1;
                rotation[4 * l + 1] = rotation1;
                jointFrame[4 * l + 2] = jointFrame2;
                restFrame[4 * l + 2] = restFrame2;
                ballRot[4 * l + 2] = ballRot2;
                rotation[4 * l + 2] = rotation2;
                jointFrame[4 * l + 3] = jointFrame3;
                restFrame[4 * l + 3] = restFrame3;
                ballRot[4 * l + 3] = ballRot3;
                rotation[4 * l + 3] = rotation3;
                rotationMatrix[9 * l] = rotationMatrix0;
                sang[9 * l] = sang0;
                slin[9 * l] = slin0;
                rotationMatrix[9 * l + 1] = rotationMatrix1;
                sang[9 * l + 1] = sang1;
                slin[9 * l + 1] = slin1;
                rotationMatrix[9 * l + 2] = rotationMatrix2;
                sang[9 * l + 2] = sang2;
                slin[9 * l + 2] = slin2;
                rotationMatrix[9 * l + 3] = rotationMatrix3;
                sang[9 * l + 3] = sang3;
                slin[9 * l + 3] = slin3;
                rotationMatrix[9 * l + 4] = rotationMatrix4;
                sang[9 * l + 4] = sang4;
                slin[9 * l + 4] = slin4;
                rotationMatrix[9 * l + 5] = rotationMatrix5;
                sang[9 * l + 5] = sang5;
                slin[9 * l + 5] = slin5;
                rotationMatrix[9 * l + 6] = rotationMatrix6;
                sang[9 * l + 6] = sang6;
                slin[9 * l + 6] = slin6;
                rotationMatrix[9 * l + 7] = rotationMatrix7;
                sang[9 * l + 7] = sang7;
                slin[9 * l + 7] = slin7;
                rotationMatrix[9 * l + 8] = rotationMatrix8;
                sang[9 * l + 8] = sang8;
                slin[9 * l + 8] = slin8;
                cbias[6 * l] = cbias0;
                cbias[6 * l + 1] = cbias1;
                cbias[6 * l + 2] = cbias2;
                cbias[6 * l + 3] = cbias3;
                cbias[6 * l + 4] = cbias4;
                cbias[6 * l + 5] = cbias5;
            }
            panelStart[links] = t.PanelStart[links * N + i];

            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                Real lo = b.LimitLo[at], hi = b.LimitHi[at], stiff = b.LimitStiff[at], damp = b.LimitDamp[at];
                Real qv = s.Q[at], qdv = s.Qd[at];
                limitLo[d] = lo; limitHi[d] = hi; limitStiff[d] = stiff; limitDamp[d] = damp;
                q[d] = qv; qd[d] = qdv;
            }
            var basePosition = new V3(s.BasePos[i], s.BasePos[N + i], s.BasePos[2 * N + i]);
            var baseCarry = new V3(s.BaseCarry[i], s.BaseCarry[N + i], s.BaseCarry[2 * N + i]);
            var baseRotation = new Q4(s.BaseRot[i], s.BaseRot[N + i], s.BaseRot[2 * N + i], s.BaseRot[3 * N + i]);

            Real excess = b.Excess[i];
            Real totalMass = b.TotalMass[i];

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 0, ref clk);
#endif
            // ---- 1. the water pass (Water.Sample, per link, no hold) --------------------------
            // WaterLinks ran it before this kernel, a link a thread, from the positions the load
            // read, and wrote the two arrays the load read. Still water left both as they stood,
            // and a current with no acceleration term left the acceleration as it stood.

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 1, ref clk);
#endif
            // ---- 2. senses, 3. brain ----------------------------------------------------------
            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << SDepth)) != 0;
            bool readsUp = (mask & (1 << SUp)) != 0;
            bool readsJoint = (mask & (1 << SJointAngle)) != 0 || (mask & (1 << SJointRate)) != 0;
            bool readsFlow = (mask & (1 << SFlow)) != 0;
            bool readsChemical = (mask & (1 << SChemical)) != 0;
            bool readsEnergy = (mask & (1 << SEnergy)) != 0;
            bool readsScent = (mask & (1 << SScent)) != 0;
            bool readsLeafDir = (mask & (1 << SLeafDir)) != 0;
            bool readsLeaf = (mask & (1 << SLeaf)) != 0 || readsLeafDir;
            bool readsDrive = (mask & (1 << SStroke)) != 0 || (mask & (1 << SHold)) != 0 || (mask & (1 << SRamp)) != 0;

            int gates = br.Gates[i];
            bool hasNutrients = (gates & 1) != 0;
            bool hasReserve = (gates & 2) != 0;
            bool hasScent = (gates & 4) != 0;
            bool hasLeaf = (gates & 8) != 0;

            // D220: a body whose organism is remains runs no senses, no brain, no drive and no grip, as
            // DynamicsWorld.StepOne skips them. cfg.AnyRemains is the block's, so in a world without remains
            // every thread takes the old path on a test it cannot fail, and the flag is never loaded.
            bool remains = cfg.AnyRemains != 0 && gl.Remains[g] != 0;

            // CreatureSenses.SampleDrive: the envelope as the last step's drive left it, read before this
            // step's drive moves it. Under the torque drive it reads 0, as sDrive was cleared.
            if (!remains && readsDrive && cfg.BendAndPower != 0)
            {
                int dofLength = dof > 0 ? dof : 1;
                for (int bl = 0; bl < links; bl++)
                {
                    Real partSum = position[3 * bl] + position[3 * bl + 1] + position[3 * bl + 2];
                    if (partSum != partSum || partSum == Real.PositiveInfinity || partSum == Real.NegativeInfinity) continue;

                    int n = dofCount[bl];
                    int offset = dofStart[bl];
                    if (n == 0 || offset < 0) continue;

                    double hold = b.HoldGene[bl * N + i];
                    for (int d = 0; d < n; d++)
                    {
                        int at = offset + d;
                        if (at >= dofLength) break;

                        int e = at * N + i;
                        double stroke = dr.EPower[e];
                        sDrive[3 * at] = Clamp((float)stroke);
                        sDrive[3 * at + 1] = hold > 0 ? (float)MaxD(0.0, MinD(1.0, dr.EHoldLeft[e] / hold)) : 0f;
                        sDrive[3 * at + 2] = Clamp((float)(dr.EPowerTarget[e] - stroke));
                    }
                }
            }

            float energy = 0f;
            bool lost = false;
            if (!remains)
            {
                Sample(i, links, dofCount, dofStart, position, rotationMatrix, relVel, q, qd, limitHi, b,
                       br, w, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical && hasNutrients,
                       readsScent && hasScent, readsEnergy && hasReserve, readsLeaf && hasLeaf, readsLeafDir,
                       sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, sLeaf, ref energy, ref lost);
            }

            if (lost)
            {
                // The chemical sense's walk gave up on this body (TankCellAt): far beyond the glass,
                // or at a distance that is not finite. It is left unstepped from here, its state as
                // the step found it, and lost as the finiteness check below loses a body; the
                // farm's glass guard kills it at its next check.
                gl.Alive[g] = 0;
                KeepSpheres(g, links, cs, ps, cfg);
                ps.R[g] = 0;
                ps.Active[g] = 0;

                if (cfg.PerPart != 0)
                {
                    for (int l = 0; l < links; l++) ps.LR[g * LinkStride + l] = 0;
                }

                return;
            }

            // The touch of the rock as the last step's contact pass left it, a bit a link, read before
            // this step's touch is kept (KeepTouch), as CreatureSenses.Sample reads it.
            int rock = 0;
            if (!remains && cfg.RockTouch != 0)
            {
                for (int l = 0; l < links; l++)
                {
                    if (gl.RockTouched[g * LinkStride + l] != 0) rock |= 1 << l;
                }
            }

            // D235: in a nose world a part that is not a nose reads zero on the four smell channels, whatever the
            // gates (SensorChannels.IsSmell), the chemical sense's standing-in constant included. The nose links ride
            // in the same word as the rock's bits, a bit a link from bit 16 (a class holds at most 16 links), so
            // that SensorRead, which already takes the word, needs no new argument. SRock reads only bits 0 to 15.
            if (!remains && cfg.Nose != 0)
            {
                for (int l = 0; l < links; l++)
                {
                    if (b.NoseFlag[l * N + i] != 0) rock |= 1 << (16 + l);
                }
            }

            // The brain reads the two gates itself (SensorRead): a body with no field or no
            // reserve source reads the standing-in constant on those channels. Remains run none.
            if (!remains)
            {
                BrainStep(i, links, dof, t, nr, br, dr, cfg, dofCount, dofStart,
                          sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf);
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 2, ref clk);
#endif
            // ---- clear -----------------------------------------------------------------------
            for (int k = 0; k < 6 * links; k++) fext[k] = 0;
            for (int k = 0; k < dof; k++) tau[k] = 0;

            // ---- 4. drive (EffectorDrive.Drive) ----------------------------------------------
            bool drivePending = false;
            if (!remains && dof != 0 && cfg.BendAndPower != 0)
            {
                // EffectorDrive.DriveBendAndPower: each degree of freedom's power and bend through
                // the joint's threshold and envelope, in double as the CPU keeps them; the power
                // stays in EPower for the fluid's push below, and a critically damped muscle holds
                // the bend, capped by the part's power.
                int sigLen = t.SigLen[i];
                float powerScale = b.PowerScale[i];
                double stepD = cfg.DtD;
                double poseOmega = cfg.PoseOmega;

                long limited = 0;

                for (int bl = 1; bl < links; bl++)
                {
                    int n = dofCount[bl];
                    if (n == 0) continue;

                    int offset = dofStart[bl];
                    int gene = bl * N + i;
                    int mirror = cfg.MirrorDrive != 0 ? b.MirrorMask[gene] : 0;   // D234, Creature.MirrorMask
                    double threshold = b.Threshold[gene];
                    double reaction = b.Reaction[gene];
                    double holdGene = b.HoldGene[gene];
                    double release = b.Release[gene];
                    Q4 frame = Q4.Read(jointFrame, 4 * bl);

                    double allowed = cfg.LimitDrive != 0 ? (double)(smallI[bl] * cfg.SpinBudget) : double.MaxValue;
                    double cap = MinD(allowed, (double)b.LinkPower[gene] * powerScale);
                    double inertia = smallI[bl];

                    V3 torque = V3.Zero;

                    for (int d = 0; d < n; d++)
                    {
                        int idx = offset + d;
                        int e = idx * N + i;

                        float v = idx < sigLen ? dr.Signal[e] : 0f;
                        double level = MaxF(0f, MinF(1f, v));
                        double power = level <= threshold ? 0.0 : MinD(1.0, (level - threshold) / (1.0 - threshold));
                        float wv = idx < sigLen ? dr.Bend[e] : 0f;
                        double bend = (MaxF(-1f, MinF(1f, wv)) + 1.0) * 0.5;

                        double pw = dr.EPower[e], bd = dr.EBend[e];
                        double pt = dr.EPowerTarget[e], bt = dr.EBendTarget[e];
                        double pr = dr.EPowerRate[e], br8 = dr.EBendRate[e];
                        double holdLeft = dr.EHoldLeft[e];

                        if (holdLeft > 0)
                        {
                            holdLeft -= stepD;
                        }
                        else if (AbsD(power - pt) > 0.01 || AbsD(bend - bt) > 0.01)
                        {
                            bool fromRest = pw == 0 && pt == 0;
                            double ramp = fromRest ? reaction : release;
                            if (ramp < stepD) ramp = stepD;
                            pt = power;
                            bt = bend;
                            pr = AbsD(power - pw) / ramp;
                            br8 = AbsD(bend - bd) / ramp;
                            holdLeft = holdGene;
                        }

                        pw = Toward(pw, pt, pr * stepD);
                        bd = Toward(bd, bt, br8 * stepD);

                        dr.EPower[e] = pw; dr.EBend[e] = bd;
                        dr.EPowerTarget[e] = pt; dr.EBendTarget[e] = bt;
                        dr.EPowerRate[e] = pr; dr.EBendRate[e] = br8;
                        dr.EHoldLeft[e] = holdLeft;

                        if (poseOmega <= 0 || inertia <= 0) continue;

                        // D234, EffectorDrive.DriveBendAndPower: a reflected degree of freedom works in the signed
                        // reading, the genome's own stops (the stored ones swapped and negated back), the signed
                        // angle and rate, and the torque reversed back to the stored angle. The sign is exactly 1
                        // everywhere else, and in a world without the rule no mask is read at all.
                        bool flipped = mirror != 0 && ((mirror >> d) & 1) != 0;
                        double sign = flipped ? -1.0 : 1.0;
                        double lo = flipped ? -(double)limitHi[idx] : (double)limitLo[idx];
                        double hi = flipped ? -(double)limitLo[idx] : (double)limitHi[idx];
                        double target = lo + bd * (hi - lo);
                        double magnitude = inertia * poseOmega * (poseOmega * (target - sign * q[idx]) - 2.0 * (sign * qd[idx]));
                        if (magnitude > cap) { magnitude = cap; limited++; }
                        else if (magnitude < -cap) { magnitude = -cap; limited++; }

                        torque = torque + frame.Rotate(V3.Axis(d)) * (Real)(sign * magnitude);
                    }

                    M3 rot = M3.Read(rotationMatrix, 9 * bl);
                    V3 world = rot * torque;

                    s.Applied[(3 * bl) * N + i] = world.X;
                    s.Applied[(3 * bl + 1) * N + i] = world.Y;
                    s.Applied[(3 * bl + 2) * N + i] = world.Z;

                    V3.Write(driveTq, 3 * bl, world);
                    V3.Write(preRelSpin, 3 * bl, V3.Read(spin, 3 * bl) - V3.Read(spin, 3 * parent[bl]));
                    drivePending = true;

                    V3.Add(fext, 6 * bl, world);
                    V3.Add(fext, 6 * parent[bl], -world);
                }

                if (limited != 0) dr.DriveLimited[i] = dr.DriveLimited[i] + limited;
            }
            else if (!remains && dof != 0)
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
                    int mirror = cfg.MirrorDrive != 0 ? b.MirrorMask[bl * N + i] : 0;   // D234, Creature.MirrorMask
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

                        // D234: a reflected degree of freedom's torque is reversed, after the limiter.
                        if (((mirror >> d) & 1) != 0) magnitude = -magnitude;

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

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 3, ref clk);
#endif
            // ---- 5. fluid (Fluid.Apply) --------------------------------------------------------
            // FluidLinks ran it before this kernel, a link a thread; its results are taken here in
            // link order, at the place the CPU applies them.
            long dragLimited = 0;
            int fluidRows = cfg.LinkRows;
            bool powered = cfg.BendAndPower != 0 && cfg.LimbPush > 0 && cfg.PushOmega > 0;
            for (int fl = 0; fl < links; fl++)
            {
                int frow = g * LinkStride + fl;
                for (int fk = 0; fk < 3; fk++)
                {
                    relVel[3 * fl + fk] = gl.LinkFluid[fk * fluidRows + frow];
                    dragF[3 * fl + fk] = gl.LinkFluid[(3 + fk) * fluidRows + frow];
                    dragT[3 * fl + fk] = gl.LinkFluid[(6 + fk) * fluidRows + frow];
                    preVel[3 * fl + fk] = velocity[3 * fl + fk];
                    preSpin[3 * fl + fk] = spin[3 * fl + fk];
                }
                V3.Add(fext, 6 * fl, new V3(gl.LinkFluid[9 * fluidRows + frow], gl.LinkFluid[10 * fluidRows + frow],
                                            gl.LinkFluid[11 * fluidRows + frow]));
                V3 linkForce = new V3(gl.LinkFluid[12 * fluidRows + frow], gl.LinkFluid[13 * fluidRows + frow],
                                      gl.LinkFluid[14 * fluidRows + frow]);

                // Under the bend-and-power drive FluidLinks left the force before the power's push
                // and the buoyancy term: the push from this step's stroke, its work booked as
                // Fluid.Apply books it, then the buoyancy where the CPU added it.
                if (powered)
                {
                    if (!remains && parent[fl] >= 0 && position[3 * fl + 1] <= 0 && dofCount[fl] > 0)
                    {
                        M3 frot = M3.Read(rotationMatrix, 9 * fl);
                        V3 toOrigin = frot * (-V3.Read(childAnchor, 3 * fl));
                        Real linkReach = toOrigin.Magnitude;
                        if (linkReach > 0)
                        {
                            V3 axis = toOrigin * ((Real)1.0 / linkReach);
                            Q4 fframe = Q4.Read(jointFrame, 4 * fl);
                            int fstart = dofStart[fl];
                            Real omega = cfg.PushOmega;
                            Real swing2 = 0;

                            // D229, as Fluid.Apply: each degree of freedom moves the limb in the unit
                            // vector of dofAxis x axis, and the added mass is the part's for it. A sphere
                            // (shape 0) keeps the unshaped line.
                            int shapeCode = cfg.ShapedMass != 0 ? b.ShapeCode[fl * N + i] : 0;
                            Real hx0 = 0, hx1 = 0, hx2 = 0;

                            // D255, as Fluid.Apply: a family limb (shape code 3) takes the strip integral its host body
                            // carries for each degree of freedom, in place of D229's lines. It reads no half-extents.
                            // Not entered for any other part, whose lines below are unchanged.
                            bool family = shapeCode == 3;
                            Real familySum = 0;
                            Real familyMass = 0;
                            if (shapeCode != 0 && !family)
                            {
                                hx0 = b.HalfExt[(3 * fl) * N + i];
                                hx1 = b.HalfExt[(3 * fl + 1) * N + i];
                                hx2 = b.HalfExt[(3 * fl + 2) * N + i];
                            }
                            Real shapedSum = 0;

                            // D231, as Fluid.Apply: a link with parts welded beyond it takes its constants from
                            // the tables, and none of the lines below. Not entered with the switch off.
                            bool assembly = cfg.StrokeAssembly != 0 && b.StrokeParts[fl * N + i] > 1;
                            Real assemblySum = 0;
                            Real assemblyMass = 0;

                            for (int d = 0; d < dofCount[fl]; d++)
                            {
                                Real sv = (Real)dr.EPower[(fstart + d) * N + i];
                                if (sv <= 0) continue;

                                if (assembly)
                                {
                                    // q_d = (s_d Omega)^2 (1 - along_d^2), with the along of the link's own frame.
                                    Real alongStored = b.StrokeAlong[(fstart + d) * N + i];
                                    Real strokeQ = sv * omega * sv * omega * ((Real)1.0 - alongStored * alongStored);
                                    swing2 += strokeQ;
                                    assemblySum += strokeQ * b.StrokeMoment[(fstart + d) * N + i];
                                    assemblyMass += strokeQ * b.StrokeMass[(fstart + d) * N + i];
                                    continue;
                                }

                                V3 dofAxis = frot * fframe.Rotate(V3.Axis(d));
                                Real along = V3.Dot(dofAxis, axis);
                                swing2 += sv * omega * sv * omega * ((Real)1.0 - along * along);

                                if (family)
                                {
                                    // q_d as above, times the degree's I1 and M, which the host wrote at build and
                                    // at every resize (Fluid.Apply's familyQ).
                                    Real familyQ = sv * omega * sv * omega * ((Real)1.0 - along * along);
                                    familySum += familyQ * b.FamMoment[(fstart + d) * N + i];
                                    familyMass += familyQ * b.FamMass[(fstart + d) * N + i];
                                }

                                if (shapeCode != 0 && !family)
                                {
                                    V3 motion = V3.Cross(dofAxis, axis);
                                    Real motionLength = motion.Magnitude;
                                    if (motionLength > 0)
                                    {
                                        V3 strokeDirection = motion * ((Real)1.0 / motionLength);
                                        shapedSum += LeverOf(linkReach, axis, strokeDirection) *
                                            ShapedAddedMass(
                                                shapeCode, hx0, hx1, hx2, frot, axis,
                                                strokeDirection, cfg.Density) *
                                            (sv * omega * sv * omega * ((Real)1.0 - along * along));
                                    }
                                }
                            }

                            if (swing2 > 0)
                            {
                                Real addedMass = cfg.AddedMass * cfg.Density * volume[fl];
                                Real push = cfg.LimbPush * addedMass * linkReach * swing2;
                                if (shapeCode != 0 && !family) push = cfg.LimbPush * shapedSum;

                                // D231: C_T times the sum of q_d I1_d, and the push-weighted lever
                                // (sum q_d I1_d) / (sum q_d M_d). Another link's lever is the reach.
                                Real lever = linkReach;
                                if (assembly)
                                {
                                    push = cfg.LimbPush * assemblySum;
                                    lever = AssemblyLeverOf(assemblySum, assemblyMass, linkReach);
                                }
                                else if (family)
                                {
                                    // D255: the same law for a family limb, from the strip integral of its true slices.
                                    push = cfg.LimbPush * familySum;
                                    lever = AssemblyLeverOf(familySum, familyMass, linkReach);
                                }

                                linkForce = linkForce + axis * (-push);
                                // The work is the push times the lever it acts at: a lever other than the reach
                                // (LeverOf) must enter this line too, as on the CPU.
                                dr.Work[i] = dr.Work[i] + (double)(push * lever * System.Math.Sqrt(swing2)) * cfg.DtD;
                            }
                        }
                    }

                    if (gl.LinkFluid[16 * fluidRows + frow] != 0)
                    {
                        linkForce = linkForce + new V3(0, gl.LinkFluid[15 * fluidRows + frow], 0);
                    }
                }

                V3.Add(fext, 6 * fl + 3, linkForce);
                dragLimited += gl.LinkLimited[frow];
            }
            if (cfg.LimitDrag != 0 && cfg.WeldedDrag != 0)
            {
                dragLimited += WeldedDrag(g * LinkStride, fluidRows, links, parent, dofCount, mass, position,
                                          rotationMatrix, inertiaLocal, spin, dragF, dragT, fext, weldAnchor,
                                          jacobiM, jacobiV, gl, cfg);
            }
            if (dragLimited != 0) dr.DragLimited[i] = dr.DragLimited[i] + dragLimited;

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 4, ref clk);
#endif
            // ---- 6. contacts (Contacts.Apply) ------------------------------------------------
            if (cfg.PerPart != 0)
            {
                ContactsPerPartStaged(g, links, fext, cand, cs, gl, gr, cfg);
                if (cfg.RockTouch != 0) KeepTouch(g, links, gl, cfg);
                if (cfg.Anchoring != 0 && !remains)
                {
                    Grip(g, links, position, velocity, spin, rotationMatrix, mass, smallI, fext,
                         hasReserve, br.Reserve[i], cs, gl, cfg);
                }
            }
            else
            {
                ContactsBody(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 5, ref clk);
#endif
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
                      ref basePosition, ref baseCarry, ref baseRotation, cfg.Dt);

            // D077's third rule inside the card's block (D204): a root that has left the box comes
            // back in at the opposite face, before the poses are built from it, as Simulation's
            // WrapAtTheSeams puts it back before anything reads a position.
            if (cfg.SeamL > 0) WrapAtTheSeams(ref basePosition, ref baseCarry, cfg);

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 6, ref clk);
#endif
            // ---- 10. poses, 11. velocities ----------------------------------------------------
            Poses(links, parent, dofCount, dofStart, q, ballRot, rotation, rotationMatrix,
                  position, restFrame, jointFrame, parentAnchor, childAnchor,
                  basePosition, baseRotation, baseCarry, posErr);
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

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 7, ref clk);
#endif
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
                        ps.LEx[row] = CarryBase ? posErr[0] : 0;
                        ps.LEy[row] = CarryBase ? posErr[1] : 0;
                        ps.LEz[row] = CarryBase ? posErr[2] : 0;
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
                            ps.LEx[row + l] = CarryBase ? posErr[3 * l] : 0;
                            ps.LEy[row + l] = CarryBase ? posErr[3 * l + 1] : 0;
                            ps.LEz[row + l] = CarryBase ? posErr[3 * l + 2] : 0;
                            ps.LR[row + l] = reach[l];
                            ps.LVx[row + l] = velocity[3 * l];
                            ps.LVy[row + l] = velocity[3 * l + 1];
                            ps.LVz[row + l] = velocity[3 * l + 2];
                        }
                    }

                    // D228: each link's turn travels with its sphere (RefreshLinkSpheres copies RotationMatrix).
                    if (cfg.TrueShape != 0)
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LRot0[row + l] = rotationMatrix[9 * l];
                            ps.LRot1[row + l] = rotationMatrix[9 * l + 1];
                            ps.LRot2[row + l] = rotationMatrix[9 * l + 2];
                            ps.LRot3[row + l] = rotationMatrix[9 * l + 3];
                            ps.LRot4[row + l] = rotationMatrix[9 * l + 4];
                            ps.LRot5[row + l] = rotationMatrix[9 * l + 5];
                            ps.LRot6[row + l] = rotationMatrix[9 * l + 6];
                            ps.LRot7[row + l] = rotationMatrix[9 * l + 7];
                            ps.LRot8[row + l] = rotationMatrix[9 * l + 8];
                        }
                    }

                    // D228 level (b): each link's world-frame spin travels with its sphere (RefreshLinkSpheres copies Spin).
                    if (cfg.AtPoint != 0)
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LSp0[row + l] = spin[3 * l];
                            ps.LSp1[row + l] = spin[3 * l + 1];
                            ps.LSp2[row + l] = spin[3 * l + 2];
                        }
                    }
                }
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 8, ref clk);
#endif
            // ---- store -----------------------------------------------------------------------
            s.BasePos[i] = basePosition.X;
            s.BasePos[N + i] = basePosition.Y;
            s.BasePos[2 * N + i] = basePosition.Z;
            s.BaseCarry[i] = baseCarry.X;
            s.BaseCarry[N + i] = baseCarry.Y;
            s.BaseCarry[2 * N + i] = baseCarry.Z;
            s.BaseRot[i] = baseRotation.X;
            s.BaseRot[N + i] = baseRotation.Y;
            s.BaseRot[2 * N + i] = baseRotation.Z;
            s.BaseRot[3 * N + i] = baseRotation.W;

            // Every read of a link from the thread's arrays before any of its writes, as in the load.
            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                Real qv = q[d], qdv = qd[d], tv = tau[d], li = limitImplicit[d];
                s.Q[at] = qv; s.Qd[at] = qdv; s.Tau[at] = tv; s.LimitImplicit[at] = li;
            }

            for (int l = 0; l < links; l++)
            {
                int a3 = 3 * l * N + i, a4 = 4 * l * N + i, a9 = 9 * l * N + i, a6 = 6 * l * N + i;
                Real position0 = position[3 * l];
                Real spin0 = spin[3 * l];
                Real velocity0 = velocity[3 * l];
                Real relVel0 = relVel[3 * l];
                Real water0 = water[3 * l];
                Real wacc0 = wacc[3 * l];
                Real position1 = position[3 * l + 1];
                Real spin1 = spin[3 * l + 1];
                Real velocity1 = velocity[3 * l + 1];
                Real relVel1 = relVel[3 * l + 1];
                Real water1 = water[3 * l + 1];
                Real wacc1 = wacc[3 * l + 1];
                Real position2 = position[3 * l + 2];
                Real spin2 = spin[3 * l + 2];
                Real velocity2 = velocity[3 * l + 2];
                Real relVel2 = relVel[3 * l + 2];
                Real water2 = water[3 * l + 2];
                Real wacc2 = wacc[3 * l + 2];
                Real ballRot0 = ballRot[4 * l];
                Real rotation0 = rotation[4 * l];
                Real ballRot1 = ballRot[4 * l + 1];
                Real rotation1 = rotation[4 * l + 1];
                Real ballRot2 = ballRot[4 * l + 2];
                Real rotation2 = rotation[4 * l + 2];
                Real ballRot3 = ballRot[4 * l + 3];
                Real rotation3 = rotation[4 * l + 3];
                Real rotationMatrix0 = rotationMatrix[9 * l];
                Real sang0 = sang[9 * l];
                Real slin0 = slin[9 * l];
                Real rotationMatrix1 = rotationMatrix[9 * l + 1];
                Real sang1 = sang[9 * l + 1];
                Real slin1 = slin[9 * l + 1];
                Real rotationMatrix2 = rotationMatrix[9 * l + 2];
                Real sang2 = sang[9 * l + 2];
                Real slin2 = slin[9 * l + 2];
                Real rotationMatrix3 = rotationMatrix[9 * l + 3];
                Real sang3 = sang[9 * l + 3];
                Real slin3 = slin[9 * l + 3];
                Real rotationMatrix4 = rotationMatrix[9 * l + 4];
                Real sang4 = sang[9 * l + 4];
                Real slin4 = slin[9 * l + 4];
                Real rotationMatrix5 = rotationMatrix[9 * l + 5];
                Real sang5 = sang[9 * l + 5];
                Real slin5 = slin[9 * l + 5];
                Real rotationMatrix6 = rotationMatrix[9 * l + 6];
                Real sang6 = sang[9 * l + 6];
                Real slin6 = slin[9 * l + 6];
                Real rotationMatrix7 = rotationMatrix[9 * l + 7];
                Real sang7 = sang[9 * l + 7];
                Real slin7 = slin[9 * l + 7];
                Real rotationMatrix8 = rotationMatrix[9 * l + 8];
                Real sang8 = sang[9 * l + 8];
                Real slin8 = slin[9 * l + 8];
                Real cbias0 = cbias[6 * l];
                Real fext0 = fext[6 * l];
                Real cbias1 = cbias[6 * l + 1];
                Real fext1 = fext[6 * l + 1];
                Real cbias2 = cbias[6 * l + 2];
                Real fext2 = fext[6 * l + 2];
                Real cbias3 = cbias[6 * l + 3];
                Real fext3 = fext[6 * l + 3];
                Real cbias4 = cbias[6 * l + 4];
                Real fext4 = fext[6 * l + 4];
                Real cbias5 = cbias[6 * l + 5];
                Real fext5 = fext[6 * l + 5];

                s.Pos[a3] = position0;
                s.Spin[a3] = spin0;
                s.Vel[a3] = velocity0;
                s.RelVel[a3] = relVel0;
                s.Water[a3] = water0;
                s.WaterAcc[a3] = wacc0;
                s.Pos[a3 + N] = position1;
                s.Spin[a3 + N] = spin1;
                s.Vel[a3 + N] = velocity1;
                s.RelVel[a3 + N] = relVel1;
                s.Water[a3 + N] = water1;
                s.WaterAcc[a3 + N] = wacc1;
                s.Pos[a3 + 2 * N] = position2;
                s.Spin[a3 + 2 * N] = spin2;
                s.Vel[a3 + 2 * N] = velocity2;
                s.RelVel[a3 + 2 * N] = relVel2;
                s.Water[a3 + 2 * N] = water2;
                s.WaterAcc[a3 + 2 * N] = wacc2;
                s.BallRot[a4] = ballRot0;
                s.Rot[a4] = rotation0;
                s.BallRot[a4 + N] = ballRot1;
                s.Rot[a4 + N] = rotation1;
                s.BallRot[a4 + 2 * N] = ballRot2;
                s.Rot[a4 + 2 * N] = rotation2;
                s.BallRot[a4 + 3 * N] = ballRot3;
                s.Rot[a4 + 3 * N] = rotation3;
                s.RotM[a9] = rotationMatrix0;
                s.Sang[a9] = sang0;
                s.Slin[a9] = slin0;
                s.RotM[a9 + N] = rotationMatrix1;
                s.Sang[a9 + N] = sang1;
                s.Slin[a9 + N] = slin1;
                s.RotM[a9 + 2 * N] = rotationMatrix2;
                s.Sang[a9 + 2 * N] = sang2;
                s.Slin[a9 + 2 * N] = slin2;
                s.RotM[a9 + 3 * N] = rotationMatrix3;
                s.Sang[a9 + 3 * N] = sang3;
                s.Slin[a9 + 3 * N] = slin3;
                s.RotM[a9 + 4 * N] = rotationMatrix4;
                s.Sang[a9 + 4 * N] = sang4;
                s.Slin[a9 + 4 * N] = slin4;
                s.RotM[a9 + 5 * N] = rotationMatrix5;
                s.Sang[a9 + 5 * N] = sang5;
                s.Slin[a9 + 5 * N] = slin5;
                s.RotM[a9 + 6 * N] = rotationMatrix6;
                s.Sang[a9 + 6 * N] = sang6;
                s.Slin[a9 + 6 * N] = slin6;
                s.RotM[a9 + 7 * N] = rotationMatrix7;
                s.Sang[a9 + 7 * N] = sang7;
                s.Slin[a9 + 7 * N] = slin7;
                s.RotM[a9 + 8 * N] = rotationMatrix8;
                s.Sang[a9 + 8 * N] = sang8;
                s.Slin[a9 + 8 * N] = slin8;
                s.Cbias[a6] = cbias0;
                s.Fext[a6] = fext0;
                s.Cbias[a6 + N] = cbias1;
                s.Fext[a6 + N] = fext1;
                s.Cbias[a6 + 2 * N] = cbias2;
                s.Fext[a6 + 2 * N] = fext2;
                s.Cbias[a6 + 3 * N] = cbias3;
                s.Fext[a6 + 3 * N] = fext3;
                s.Cbias[a6 + 4 * N] = cbias4;
                s.Fext[a6 + 4 * N] = fext4;
                s.Cbias[a6 + 5 * N] = cbias5;
                s.Fext[a6 + 5 * N] = fext5;
            }
#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 9, ref clk);
#endif
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
            // A body the reach guard recorded this step (LoseStrays) dies here, after the streams
            // join, rather than in LoseStrays, beside which the water and the fluid read Alive.
            if (gl.Alive[g] != 0 && gl.StrayCode[g] != 0) gl.Alive[g] = 0;

            if (gl.Alive[g] == 0)
            {
                KeepSpheres(g, links, cs, ps, cfg);
                return;
            }

            int dof = t.Dof[i];
#if GPU_PHASE_PROBE
            long clk = 0;
            if (CudaAsm.IsSupported) CudaAsm.Emit("mov.u64 %0, %%clock64;", out clk);
#endif

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
            var posErr = new Real[3 * MaxLinks];
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
            var sScent = new float[MaxLinks];
            var sLeaf = new float[4 * MaxLinks];   // a part's leaf strength, then its direction's three
            var sFlow = new float[3 * MaxLinks];
            var sAngle = new float[MaxDof];
            var sRate = new float[MaxDof];
            var sDrive = new float[3 * MaxDof];

            var six = new Real[42];
            var sol = new Real[6];
            var residual = new Real[3];
            var cand = new int[CandCap];
            var weldAnchor = new int[MaxLinks];
            var jacobiM = new Real[9];
            var jacobiV = new Real[9];

            // A card does not zero a thread's local memory. The senses the brain may read without
            // this step writing them (a channel whose mask bit is clear) read 0, never garbage.
            for (int k = 0; k < MaxLinks; k++)
            {
                sDepth[k] = 0f; sUp[k] = 0f; sChem[k] = 0f; sScent[k] = 0f;
                sLeaf[4 * k] = 0f; sLeaf[4 * k + 1] = 0f; sLeaf[4 * k + 2] = 0f; sLeaf[4 * k + 3] = 0f;
                sFlow[3 * k] = 0f; sFlow[3 * k + 1] = 0f; sFlow[3 * k + 2] = 0f;
                driveTq[3 * k] = 0; driveTq[3 * k + 1] = 0; driveTq[3 * k + 2] = 0;
            }
            for (int k = 0; k < MaxDof; k++)
            {
                sAngle[k] = 0f; sRate[k] = 0f;
                sDrive[3 * k] = 0f; sDrive[3 * k + 1] = 0f; sDrive[3 * k + 2] = 0f;
            }

            // ---- load ------------------------------------------------------------------------
            // Every read of a link before any of its writes (card-next-r57.md, part 6): the reads are
            // then independent of the stores to the thread's arrays in the PTX itself, so the card can
            // have them all in flight at once rather than one memory latency a value. The same values
            // land in the same places as the loop over k did.
            for (int l = 0; l < links; l++)
            {
                int at = l * N + i;
                int a3 = 3 * l * N + i, a4 = 4 * l * N + i, a9 = 9 * l * N + i, a6 = 6 * l * N + i;
                int parent_ = t.Parent[at];
                int dofCount_ = t.DofCount[at];
                int dofStart_ = t.DofStart[at];
                int panelStart_ = t.PanelStart[at];
                int thinAxis_ = t.ThinAxis[at];
                Real mass_ = b.Mass[at];
                Real lift_ = b.Lift[at];
                Real volume_ = b.Volume[at];
                Real smallI_ = b.SmallI[at];
                Real reach_ = b.Reach[at];
                Real arm_ = b.Arm[at];
                Real inertiaLocal0 = b.Inertia[a3];
                Real childAnchor0 = b.ChildAnchor[a3];
                Real parentAnchor0 = b.ParentAnchor[a3];
                Real position0 = s.Pos[a3];
                Real spin0 = s.Spin[a3];
                Real velocity0 = s.Vel[a3];
                Real relVel0 = s.RelVel[a3];
                Real water0 = s.Water[a3];
                Real wacc0 = s.WaterAcc[a3];
                Real inertiaLocal1 = b.Inertia[a3 + N];
                Real childAnchor1 = b.ChildAnchor[a3 + N];
                Real parentAnchor1 = b.ParentAnchor[a3 + N];
                Real position1 = s.Pos[a3 + N];
                Real spin1 = s.Spin[a3 + N];
                Real velocity1 = s.Vel[a3 + N];
                Real relVel1 = s.RelVel[a3 + N];
                Real water1 = s.Water[a3 + N];
                Real wacc1 = s.WaterAcc[a3 + N];
                Real inertiaLocal2 = b.Inertia[a3 + 2 * N];
                Real childAnchor2 = b.ChildAnchor[a3 + 2 * N];
                Real parentAnchor2 = b.ParentAnchor[a3 + 2 * N];
                Real position2 = s.Pos[a3 + 2 * N];
                Real spin2 = s.Spin[a3 + 2 * N];
                Real velocity2 = s.Vel[a3 + 2 * N];
                Real relVel2 = s.RelVel[a3 + 2 * N];
                Real water2 = s.Water[a3 + 2 * N];
                Real wacc2 = s.WaterAcc[a3 + 2 * N];
                Real jointFrame0 = b.JointFrame[a4];
                Real restFrame0 = b.RestFrame[a4];
                Real ballRot0 = s.BallRot[a4];
                Real rotation0 = s.Rot[a4];
                Real jointFrame1 = b.JointFrame[a4 + N];
                Real restFrame1 = b.RestFrame[a4 + N];
                Real ballRot1 = s.BallRot[a4 + N];
                Real rotation1 = s.Rot[a4 + N];
                Real jointFrame2 = b.JointFrame[a4 + 2 * N];
                Real restFrame2 = b.RestFrame[a4 + 2 * N];
                Real ballRot2 = s.BallRot[a4 + 2 * N];
                Real rotation2 = s.Rot[a4 + 2 * N];
                Real jointFrame3 = b.JointFrame[a4 + 3 * N];
                Real restFrame3 = b.RestFrame[a4 + 3 * N];
                Real ballRot3 = s.BallRot[a4 + 3 * N];
                Real rotation3 = s.Rot[a4 + 3 * N];
                Real rotationMatrix0 = s.RotM[a9];
                Real sang0 = s.Sang[a9];
                Real slin0 = s.Slin[a9];
                Real rotationMatrix1 = s.RotM[a9 + N];
                Real sang1 = s.Sang[a9 + N];
                Real slin1 = s.Slin[a9 + N];
                Real rotationMatrix2 = s.RotM[a9 + 2 * N];
                Real sang2 = s.Sang[a9 + 2 * N];
                Real slin2 = s.Slin[a9 + 2 * N];
                Real rotationMatrix3 = s.RotM[a9 + 3 * N];
                Real sang3 = s.Sang[a9 + 3 * N];
                Real slin3 = s.Slin[a9 + 3 * N];
                Real rotationMatrix4 = s.RotM[a9 + 4 * N];
                Real sang4 = s.Sang[a9 + 4 * N];
                Real slin4 = s.Slin[a9 + 4 * N];
                Real rotationMatrix5 = s.RotM[a9 + 5 * N];
                Real sang5 = s.Sang[a9 + 5 * N];
                Real slin5 = s.Slin[a9 + 5 * N];
                Real rotationMatrix6 = s.RotM[a9 + 6 * N];
                Real sang6 = s.Sang[a9 + 6 * N];
                Real slin6 = s.Slin[a9 + 6 * N];
                Real rotationMatrix7 = s.RotM[a9 + 7 * N];
                Real sang7 = s.Sang[a9 + 7 * N];
                Real slin7 = s.Slin[a9 + 7 * N];
                Real rotationMatrix8 = s.RotM[a9 + 8 * N];
                Real sang8 = s.Sang[a9 + 8 * N];
                Real slin8 = s.Slin[a9 + 8 * N];
                Real cbias0 = s.Cbias[a6];
                Real cbias1 = s.Cbias[a6 + N];
                Real cbias2 = s.Cbias[a6 + 2 * N];
                Real cbias3 = s.Cbias[a6 + 3 * N];
                Real cbias4 = s.Cbias[a6 + 4 * N];
                Real cbias5 = s.Cbias[a6 + 5 * N];

                parent[l] = parent_;
                dofCount[l] = dofCount_;
                dofStart[l] = dofStart_;
                panelStart[l] = panelStart_;
                thinAxis[l] = thinAxis_;
                mass[l] = mass_;
                lift[l] = lift_;
                volume[l] = volume_;
                smallI[l] = smallI_;
                reach[l] = reach_;
                arm[l] = arm_;
                inertiaLocal[3 * l] = inertiaLocal0;
                childAnchor[3 * l] = childAnchor0;
                parentAnchor[3 * l] = parentAnchor0;
                position[3 * l] = position0;
                spin[3 * l] = spin0;
                velocity[3 * l] = velocity0;
                relVel[3 * l] = relVel0;
                water[3 * l] = water0;
                wacc[3 * l] = wacc0;
                inertiaLocal[3 * l + 1] = inertiaLocal1;
                childAnchor[3 * l + 1] = childAnchor1;
                parentAnchor[3 * l + 1] = parentAnchor1;
                position[3 * l + 1] = position1;
                spin[3 * l + 1] = spin1;
                velocity[3 * l + 1] = velocity1;
                relVel[3 * l + 1] = relVel1;
                water[3 * l + 1] = water1;
                wacc[3 * l + 1] = wacc1;
                inertiaLocal[3 * l + 2] = inertiaLocal2;
                childAnchor[3 * l + 2] = childAnchor2;
                parentAnchor[3 * l + 2] = parentAnchor2;
                position[3 * l + 2] = position2;
                spin[3 * l + 2] = spin2;
                velocity[3 * l + 2] = velocity2;
                relVel[3 * l + 2] = relVel2;
                water[3 * l + 2] = water2;
                wacc[3 * l + 2] = wacc2;
                jointFrame[4 * l] = jointFrame0;
                restFrame[4 * l] = restFrame0;
                ballRot[4 * l] = ballRot0;
                rotation[4 * l] = rotation0;
                jointFrame[4 * l + 1] = jointFrame1;
                restFrame[4 * l + 1] = restFrame1;
                ballRot[4 * l + 1] = ballRot1;
                rotation[4 * l + 1] = rotation1;
                jointFrame[4 * l + 2] = jointFrame2;
                restFrame[4 * l + 2] = restFrame2;
                ballRot[4 * l + 2] = ballRot2;
                rotation[4 * l + 2] = rotation2;
                jointFrame[4 * l + 3] = jointFrame3;
                restFrame[4 * l + 3] = restFrame3;
                ballRot[4 * l + 3] = ballRot3;
                rotation[4 * l + 3] = rotation3;
                rotationMatrix[9 * l] = rotationMatrix0;
                sang[9 * l] = sang0;
                slin[9 * l] = slin0;
                rotationMatrix[9 * l + 1] = rotationMatrix1;
                sang[9 * l + 1] = sang1;
                slin[9 * l + 1] = slin1;
                rotationMatrix[9 * l + 2] = rotationMatrix2;
                sang[9 * l + 2] = sang2;
                slin[9 * l + 2] = slin2;
                rotationMatrix[9 * l + 3] = rotationMatrix3;
                sang[9 * l + 3] = sang3;
                slin[9 * l + 3] = slin3;
                rotationMatrix[9 * l + 4] = rotationMatrix4;
                sang[9 * l + 4] = sang4;
                slin[9 * l + 4] = slin4;
                rotationMatrix[9 * l + 5] = rotationMatrix5;
                sang[9 * l + 5] = sang5;
                slin[9 * l + 5] = slin5;
                rotationMatrix[9 * l + 6] = rotationMatrix6;
                sang[9 * l + 6] = sang6;
                slin[9 * l + 6] = slin6;
                rotationMatrix[9 * l + 7] = rotationMatrix7;
                sang[9 * l + 7] = sang7;
                slin[9 * l + 7] = slin7;
                rotationMatrix[9 * l + 8] = rotationMatrix8;
                sang[9 * l + 8] = sang8;
                slin[9 * l + 8] = slin8;
                cbias[6 * l] = cbias0;
                cbias[6 * l + 1] = cbias1;
                cbias[6 * l + 2] = cbias2;
                cbias[6 * l + 3] = cbias3;
                cbias[6 * l + 4] = cbias4;
                cbias[6 * l + 5] = cbias5;
            }
            panelStart[links] = t.PanelStart[links * N + i];

            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                Real lo = b.LimitLo[at], hi = b.LimitHi[at], stiff = b.LimitStiff[at], damp = b.LimitDamp[at];
                Real qv = s.Q[at], qdv = s.Qd[at];
                limitLo[d] = lo; limitHi[d] = hi; limitStiff[d] = stiff; limitDamp[d] = damp;
                q[d] = qv; qd[d] = qdv;
            }
            var basePosition = new V3(s.BasePos[i], s.BasePos[N + i], s.BasePos[2 * N + i]);
            var baseCarry = new V3(s.BaseCarry[i], s.BaseCarry[N + i], s.BaseCarry[2 * N + i]);
            var baseRotation = new Q4(s.BaseRot[i], s.BaseRot[N + i], s.BaseRot[2 * N + i], s.BaseRot[3 * N + i]);

            Real excess = b.Excess[i];
            Real totalMass = b.TotalMass[i];

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 0, ref clk);
#endif
            // ---- 1. the water pass (Water.Sample, per link, no hold) --------------------------
            // WaterLinks ran it before this kernel, a link a thread, from the positions the load
            // read, and wrote the two arrays the load read. Still water left both as they stood,
            // and a current with no acceleration term left the acceleration as it stood.

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 1, ref clk);
#endif
            // ---- 2. senses, 3. brain ----------------------------------------------------------
            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << SDepth)) != 0;
            bool readsUp = (mask & (1 << SUp)) != 0;
            bool readsJoint = (mask & (1 << SJointAngle)) != 0 || (mask & (1 << SJointRate)) != 0;
            bool readsFlow = (mask & (1 << SFlow)) != 0;
            bool readsChemical = (mask & (1 << SChemical)) != 0;
            bool readsEnergy = (mask & (1 << SEnergy)) != 0;
            bool readsScent = (mask & (1 << SScent)) != 0;
            bool readsLeafDir = (mask & (1 << SLeafDir)) != 0;
            bool readsLeaf = (mask & (1 << SLeaf)) != 0 || readsLeafDir;
            bool readsDrive = (mask & (1 << SStroke)) != 0 || (mask & (1 << SHold)) != 0 || (mask & (1 << SRamp)) != 0;

            int gates = br.Gates[i];
            bool hasNutrients = (gates & 1) != 0;
            bool hasReserve = (gates & 2) != 0;
            bool hasScent = (gates & 4) != 0;
            bool hasLeaf = (gates & 8) != 0;

            // D220: a body whose organism is remains runs no senses, no brain, no drive and no grip, as
            // DynamicsWorld.StepOne skips them. cfg.AnyRemains is the block's, so in a world without remains
            // every thread takes the old path on a test it cannot fail, and the flag is never loaded.
            bool remains = cfg.AnyRemains != 0 && gl.Remains[g] != 0;

            // CreatureSenses.SampleDrive: the envelope as the last step's drive left it, read before this
            // step's drive moves it. Under the torque drive it reads 0, as sDrive was cleared.
            if (!remains && readsDrive && cfg.BendAndPower != 0)
            {
                int dofLength = dof > 0 ? dof : 1;
                for (int bl = 0; bl < links; bl++)
                {
                    Real partSum = position[3 * bl] + position[3 * bl + 1] + position[3 * bl + 2];
                    if (partSum != partSum || partSum == Real.PositiveInfinity || partSum == Real.NegativeInfinity) continue;

                    int n = dofCount[bl];
                    int offset = dofStart[bl];
                    if (n == 0 || offset < 0) continue;

                    double hold = b.HoldGene[bl * N + i];
                    for (int d = 0; d < n; d++)
                    {
                        int at = offset + d;
                        if (at >= dofLength) break;

                        int e = at * N + i;
                        double stroke = dr.EPower[e];
                        sDrive[3 * at] = Clamp((float)stroke);
                        sDrive[3 * at + 1] = hold > 0 ? (float)MaxD(0.0, MinD(1.0, dr.EHoldLeft[e] / hold)) : 0f;
                        sDrive[3 * at + 2] = Clamp((float)(dr.EPowerTarget[e] - stroke));
                    }
                }
            }

            float energy = 0f;
            bool lost = false;
            if (!remains)
            {
                Sample(i, links, dofCount, dofStart, position, rotationMatrix, relVel, q, qd, limitHi, b,
                       br, w, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical && hasNutrients,
                       readsScent && hasScent, readsEnergy && hasReserve, readsLeaf && hasLeaf, readsLeafDir,
                       sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, sLeaf, ref energy, ref lost);
            }

            if (lost)
            {
                // The chemical sense's walk gave up on this body (TankCellAt): far beyond the glass,
                // or at a distance that is not finite. It is left unstepped from here, its state as
                // the step found it, and lost as the finiteness check below loses a body; the
                // farm's glass guard kills it at its next check.
                gl.Alive[g] = 0;
                KeepSpheres(g, links, cs, ps, cfg);
                ps.R[g] = 0;
                ps.Active[g] = 0;

                if (cfg.PerPart != 0)
                {
                    for (int l = 0; l < links; l++) ps.LR[g * LinkStride + l] = 0;
                }

                return;
            }

            // The touch of the rock as the last step's contact pass left it, a bit a link, read before
            // this step's touch is kept (KeepTouch), as CreatureSenses.Sample reads it.
            int rock = 0;
            if (!remains && cfg.RockTouch != 0)
            {
                for (int l = 0; l < links; l++)
                {
                    if (gl.RockTouched[g * LinkStride + l] != 0) rock |= 1 << l;
                }
            }

            // D235: in a nose world a part that is not a nose reads zero on the four smell channels, whatever the
            // gates (SensorChannels.IsSmell), the chemical sense's standing-in constant included. The nose links ride
            // in the same word as the rock's bits, a bit a link from bit 16 (a class holds at most 16 links), so
            // that SensorRead, which already takes the word, needs no new argument. SRock reads only bits 0 to 15.
            if (!remains && cfg.Nose != 0)
            {
                for (int l = 0; l < links; l++)
                {
                    if (b.NoseFlag[l * N + i] != 0) rock |= 1 << (16 + l);
                }
            }

            // The brain reads the two gates itself (SensorRead): a body with no field or no
            // reserve source reads the standing-in constant on those channels. Remains run none.
            if (!remains)
            {
                BrainStep(i, links, dof, t, nr, br, dr, cfg, dofCount, dofStart,
                          sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf);
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 2, ref clk);
#endif
            // ---- clear -----------------------------------------------------------------------
            for (int k = 0; k < 6 * links; k++) fext[k] = 0;
            for (int k = 0; k < dof; k++) tau[k] = 0;

            // ---- 4. drive (EffectorDrive.Drive) ----------------------------------------------
            bool drivePending = false;
            if (!remains && dof != 0 && cfg.BendAndPower != 0)
            {
                // EffectorDrive.DriveBendAndPower: each degree of freedom's power and bend through
                // the joint's threshold and envelope, in double as the CPU keeps them; the power
                // stays in EPower for the fluid's push below, and a critically damped muscle holds
                // the bend, capped by the part's power.
                int sigLen = t.SigLen[i];
                float powerScale = b.PowerScale[i];
                double stepD = cfg.DtD;
                double poseOmega = cfg.PoseOmega;

                long limited = 0;

                for (int bl = 1; bl < links; bl++)
                {
                    int n = dofCount[bl];
                    if (n == 0) continue;

                    int offset = dofStart[bl];
                    int gene = bl * N + i;
                    int mirror = cfg.MirrorDrive != 0 ? b.MirrorMask[gene] : 0;   // D234, Creature.MirrorMask
                    double threshold = b.Threshold[gene];
                    double reaction = b.Reaction[gene];
                    double holdGene = b.HoldGene[gene];
                    double release = b.Release[gene];
                    Q4 frame = Q4.Read(jointFrame, 4 * bl);

                    double allowed = cfg.LimitDrive != 0 ? (double)(smallI[bl] * cfg.SpinBudget) : double.MaxValue;
                    double cap = MinD(allowed, (double)b.LinkPower[gene] * powerScale);
                    double inertia = smallI[bl];

                    V3 torque = V3.Zero;

                    for (int d = 0; d < n; d++)
                    {
                        int idx = offset + d;
                        int e = idx * N + i;

                        float v = idx < sigLen ? dr.Signal[e] : 0f;
                        double level = MaxF(0f, MinF(1f, v));
                        double power = level <= threshold ? 0.0 : MinD(1.0, (level - threshold) / (1.0 - threshold));
                        float wv = idx < sigLen ? dr.Bend[e] : 0f;
                        double bend = (MaxF(-1f, MinF(1f, wv)) + 1.0) * 0.5;

                        double pw = dr.EPower[e], bd = dr.EBend[e];
                        double pt = dr.EPowerTarget[e], bt = dr.EBendTarget[e];
                        double pr = dr.EPowerRate[e], br8 = dr.EBendRate[e];
                        double holdLeft = dr.EHoldLeft[e];

                        if (holdLeft > 0)
                        {
                            holdLeft -= stepD;
                        }
                        else if (AbsD(power - pt) > 0.01 || AbsD(bend - bt) > 0.01)
                        {
                            bool fromRest = pw == 0 && pt == 0;
                            double ramp = fromRest ? reaction : release;
                            if (ramp < stepD) ramp = stepD;
                            pt = power;
                            bt = bend;
                            pr = AbsD(power - pw) / ramp;
                            br8 = AbsD(bend - bd) / ramp;
                            holdLeft = holdGene;
                        }

                        pw = Toward(pw, pt, pr * stepD);
                        bd = Toward(bd, bt, br8 * stepD);

                        dr.EPower[e] = pw; dr.EBend[e] = bd;
                        dr.EPowerTarget[e] = pt; dr.EBendTarget[e] = bt;
                        dr.EPowerRate[e] = pr; dr.EBendRate[e] = br8;
                        dr.EHoldLeft[e] = holdLeft;

                        if (poseOmega <= 0 || inertia <= 0) continue;

                        // D234, EffectorDrive.DriveBendAndPower: a reflected degree of freedom works in the signed
                        // reading, the genome's own stops (the stored ones swapped and negated back), the signed
                        // angle and rate, and the torque reversed back to the stored angle. The sign is exactly 1
                        // everywhere else, and in a world without the rule no mask is read at all.
                        bool flipped = mirror != 0 && ((mirror >> d) & 1) != 0;
                        double sign = flipped ? -1.0 : 1.0;
                        double lo = flipped ? -(double)limitHi[idx] : (double)limitLo[idx];
                        double hi = flipped ? -(double)limitLo[idx] : (double)limitHi[idx];
                        double target = lo + bd * (hi - lo);
                        double magnitude = inertia * poseOmega * (poseOmega * (target - sign * q[idx]) - 2.0 * (sign * qd[idx]));
                        if (magnitude > cap) { magnitude = cap; limited++; }
                        else if (magnitude < -cap) { magnitude = -cap; limited++; }

                        torque = torque + frame.Rotate(V3.Axis(d)) * (Real)(sign * magnitude);
                    }

                    M3 rot = M3.Read(rotationMatrix, 9 * bl);
                    V3 world = rot * torque;

                    s.Applied[(3 * bl) * N + i] = world.X;
                    s.Applied[(3 * bl + 1) * N + i] = world.Y;
                    s.Applied[(3 * bl + 2) * N + i] = world.Z;

                    V3.Write(driveTq, 3 * bl, world);
                    V3.Write(preRelSpin, 3 * bl, V3.Read(spin, 3 * bl) - V3.Read(spin, 3 * parent[bl]));
                    drivePending = true;

                    V3.Add(fext, 6 * bl, world);
                    V3.Add(fext, 6 * parent[bl], -world);
                }

                if (limited != 0) dr.DriveLimited[i] = dr.DriveLimited[i] + limited;
            }
            else if (!remains && dof != 0)
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
                    int mirror = cfg.MirrorDrive != 0 ? b.MirrorMask[bl * N + i] : 0;   // D234, Creature.MirrorMask
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

                        // D234: a reflected degree of freedom's torque is reversed, after the limiter.
                        if (((mirror >> d) & 1) != 0) magnitude = -magnitude;

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

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 3, ref clk);
#endif
            // ---- 5. fluid (Fluid.Apply) --------------------------------------------------------
            // FluidLinks ran it before this kernel, a link a thread; its results are taken here in
            // link order, at the place the CPU applies them.
            long dragLimited = 0;
            int fluidRows = cfg.LinkRows;
            bool powered = cfg.BendAndPower != 0 && cfg.LimbPush > 0 && cfg.PushOmega > 0;
            for (int fl = 0; fl < links; fl++)
            {
                int frow = g * LinkStride + fl;
                for (int fk = 0; fk < 3; fk++)
                {
                    relVel[3 * fl + fk] = gl.LinkFluid[fk * fluidRows + frow];
                    dragF[3 * fl + fk] = gl.LinkFluid[(3 + fk) * fluidRows + frow];
                    dragT[3 * fl + fk] = gl.LinkFluid[(6 + fk) * fluidRows + frow];
                    preVel[3 * fl + fk] = velocity[3 * fl + fk];
                    preSpin[3 * fl + fk] = spin[3 * fl + fk];
                }
                V3.Add(fext, 6 * fl, new V3(gl.LinkFluid[9 * fluidRows + frow], gl.LinkFluid[10 * fluidRows + frow],
                                            gl.LinkFluid[11 * fluidRows + frow]));
                V3 linkForce = new V3(gl.LinkFluid[12 * fluidRows + frow], gl.LinkFluid[13 * fluidRows + frow],
                                      gl.LinkFluid[14 * fluidRows + frow]);

                // Under the bend-and-power drive FluidLinks left the force before the power's push
                // and the buoyancy term: the push from this step's stroke, its work booked as
                // Fluid.Apply books it, then the buoyancy where the CPU added it.
                if (powered)
                {
                    if (!remains && parent[fl] >= 0 && position[3 * fl + 1] <= 0 && dofCount[fl] > 0)
                    {
                        M3 frot = M3.Read(rotationMatrix, 9 * fl);
                        V3 toOrigin = frot * (-V3.Read(childAnchor, 3 * fl));
                        Real linkReach = toOrigin.Magnitude;
                        if (linkReach > 0)
                        {
                            V3 axis = toOrigin * ((Real)1.0 / linkReach);
                            Q4 fframe = Q4.Read(jointFrame, 4 * fl);
                            int fstart = dofStart[fl];
                            Real omega = cfg.PushOmega;
                            Real swing2 = 0;

                            // D229, as Fluid.Apply: each degree of freedom moves the limb in the unit
                            // vector of dofAxis x axis, and the added mass is the part's for it. A sphere
                            // (shape 0) keeps the unshaped line.
                            int shapeCode = cfg.ShapedMass != 0 ? b.ShapeCode[fl * N + i] : 0;
                            Real hx0 = 0, hx1 = 0, hx2 = 0;

                            // D255, as Fluid.Apply: a family limb (shape code 3) takes the strip integral its host body
                            // carries for each degree of freedom, in place of D229's lines. It reads no half-extents.
                            // Not entered for any other part, whose lines below are unchanged.
                            bool family = shapeCode == 3;
                            Real familySum = 0;
                            Real familyMass = 0;
                            if (shapeCode != 0 && !family)
                            {
                                hx0 = b.HalfExt[(3 * fl) * N + i];
                                hx1 = b.HalfExt[(3 * fl + 1) * N + i];
                                hx2 = b.HalfExt[(3 * fl + 2) * N + i];
                            }
                            Real shapedSum = 0;

                            // D231, as Fluid.Apply: a link with parts welded beyond it takes its constants from
                            // the tables, and none of the lines below. Not entered with the switch off.
                            bool assembly = cfg.StrokeAssembly != 0 && b.StrokeParts[fl * N + i] > 1;
                            Real assemblySum = 0;
                            Real assemblyMass = 0;

                            for (int d = 0; d < dofCount[fl]; d++)
                            {
                                Real sv = (Real)dr.EPower[(fstart + d) * N + i];
                                if (sv <= 0) continue;

                                if (assembly)
                                {
                                    // q_d = (s_d Omega)^2 (1 - along_d^2), with the along of the link's own frame.
                                    Real alongStored = b.StrokeAlong[(fstart + d) * N + i];
                                    Real strokeQ = sv * omega * sv * omega * ((Real)1.0 - alongStored * alongStored);
                                    swing2 += strokeQ;
                                    assemblySum += strokeQ * b.StrokeMoment[(fstart + d) * N + i];
                                    assemblyMass += strokeQ * b.StrokeMass[(fstart + d) * N + i];
                                    continue;
                                }

                                V3 dofAxis = frot * fframe.Rotate(V3.Axis(d));
                                Real along = V3.Dot(dofAxis, axis);
                                swing2 += sv * omega * sv * omega * ((Real)1.0 - along * along);

                                if (family)
                                {
                                    // q_d as above, times the degree's I1 and M, which the host wrote at build and
                                    // at every resize (Fluid.Apply's familyQ).
                                    Real familyQ = sv * omega * sv * omega * ((Real)1.0 - along * along);
                                    familySum += familyQ * b.FamMoment[(fstart + d) * N + i];
                                    familyMass += familyQ * b.FamMass[(fstart + d) * N + i];
                                }

                                if (shapeCode != 0 && !family)
                                {
                                    V3 motion = V3.Cross(dofAxis, axis);
                                    Real motionLength = motion.Magnitude;
                                    if (motionLength > 0)
                                    {
                                        V3 strokeDirection = motion * ((Real)1.0 / motionLength);
                                        shapedSum += LeverOf(linkReach, axis, strokeDirection) *
                                            ShapedAddedMass(
                                                shapeCode, hx0, hx1, hx2, frot, axis,
                                                strokeDirection, cfg.Density) *
                                            (sv * omega * sv * omega * ((Real)1.0 - along * along));
                                    }
                                }
                            }

                            if (swing2 > 0)
                            {
                                Real addedMass = cfg.AddedMass * cfg.Density * volume[fl];
                                Real push = cfg.LimbPush * addedMass * linkReach * swing2;
                                if (shapeCode != 0 && !family) push = cfg.LimbPush * shapedSum;

                                // D231: C_T times the sum of q_d I1_d, and the push-weighted lever
                                // (sum q_d I1_d) / (sum q_d M_d). Another link's lever is the reach.
                                Real lever = linkReach;
                                if (assembly)
                                {
                                    push = cfg.LimbPush * assemblySum;
                                    lever = AssemblyLeverOf(assemblySum, assemblyMass, linkReach);
                                }
                                else if (family)
                                {
                                    // D255: the same law for a family limb, from the strip integral of its true slices.
                                    push = cfg.LimbPush * familySum;
                                    lever = AssemblyLeverOf(familySum, familyMass, linkReach);
                                }

                                linkForce = linkForce + axis * (-push);
                                // The work is the push times the lever it acts at: a lever other than the reach
                                // (LeverOf) must enter this line too, as on the CPU.
                                dr.Work[i] = dr.Work[i] + (double)(push * lever * System.Math.Sqrt(swing2)) * cfg.DtD;
                            }
                        }
                    }

                    if (gl.LinkFluid[16 * fluidRows + frow] != 0)
                    {
                        linkForce = linkForce + new V3(0, gl.LinkFluid[15 * fluidRows + frow], 0);
                    }
                }

                V3.Add(fext, 6 * fl + 3, linkForce);
                dragLimited += gl.LinkLimited[frow];
            }
            if (cfg.LimitDrag != 0 && cfg.WeldedDrag != 0)
            {
                dragLimited += WeldedDrag(g * LinkStride, fluidRows, links, parent, dofCount, mass, position,
                                          rotationMatrix, inertiaLocal, spin, dragF, dragT, fext, weldAnchor,
                                          jacobiM, jacobiV, gl, cfg);
            }
            if (dragLimited != 0) dr.DragLimited[i] = dr.DragLimited[i] + dragLimited;

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 4, ref clk);
#endif
            // ---- 6. contacts (Contacts.Apply) ------------------------------------------------
            if (cfg.PerPart != 0)
            {
                ContactsPerPartStaged(g, links, fext, cand, cs, gl, gr, cfg);
                if (cfg.RockTouch != 0) KeepTouch(g, links, gl, cfg);
                if (cfg.Anchoring != 0 && !remains)
                {
                    Grip(g, links, position, velocity, spin, rotationMatrix, mass, smallI, fext,
                         hasReserve, br.Reserve[i], cs, gl, cfg);
                }
            }
            else
            {
                ContactsBody(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 5, ref clk);
#endif
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
                      ref basePosition, ref baseCarry, ref baseRotation, cfg.Dt);

            // D077's third rule inside the card's block (D204): a root that has left the box comes
            // back in at the opposite face, before the poses are built from it, as Simulation's
            // WrapAtTheSeams puts it back before anything reads a position.
            if (cfg.SeamL > 0) WrapAtTheSeams(ref basePosition, ref baseCarry, cfg);

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 6, ref clk);
#endif
            // ---- 10. poses, 11. velocities ----------------------------------------------------
            Poses(links, parent, dofCount, dofStart, q, ballRot, rotation, rotationMatrix,
                  position, restFrame, jointFrame, parentAnchor, childAnchor,
                  basePosition, baseRotation, baseCarry, posErr);
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

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 7, ref clk);
#endif
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
                        ps.LEx[row] = CarryBase ? posErr[0] : 0;
                        ps.LEy[row] = CarryBase ? posErr[1] : 0;
                        ps.LEz[row] = CarryBase ? posErr[2] : 0;
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
                            ps.LEx[row + l] = CarryBase ? posErr[3 * l] : 0;
                            ps.LEy[row + l] = CarryBase ? posErr[3 * l + 1] : 0;
                            ps.LEz[row + l] = CarryBase ? posErr[3 * l + 2] : 0;
                            ps.LR[row + l] = reach[l];
                            ps.LVx[row + l] = velocity[3 * l];
                            ps.LVy[row + l] = velocity[3 * l + 1];
                            ps.LVz[row + l] = velocity[3 * l + 2];
                        }
                    }

                    // D228: each link's turn travels with its sphere (RefreshLinkSpheres copies RotationMatrix).
                    if (cfg.TrueShape != 0)
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LRot0[row + l] = rotationMatrix[9 * l];
                            ps.LRot1[row + l] = rotationMatrix[9 * l + 1];
                            ps.LRot2[row + l] = rotationMatrix[9 * l + 2];
                            ps.LRot3[row + l] = rotationMatrix[9 * l + 3];
                            ps.LRot4[row + l] = rotationMatrix[9 * l + 4];
                            ps.LRot5[row + l] = rotationMatrix[9 * l + 5];
                            ps.LRot6[row + l] = rotationMatrix[9 * l + 6];
                            ps.LRot7[row + l] = rotationMatrix[9 * l + 7];
                            ps.LRot8[row + l] = rotationMatrix[9 * l + 8];
                        }
                    }

                    // D228 level (b): each link's world-frame spin travels with its sphere (RefreshLinkSpheres copies Spin).
                    if (cfg.AtPoint != 0)
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LSp0[row + l] = spin[3 * l];
                            ps.LSp1[row + l] = spin[3 * l + 1];
                            ps.LSp2[row + l] = spin[3 * l + 2];
                        }
                    }
                }
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 8, ref clk);
#endif
            // ---- store -----------------------------------------------------------------------
            s.BasePos[i] = basePosition.X;
            s.BasePos[N + i] = basePosition.Y;
            s.BasePos[2 * N + i] = basePosition.Z;
            s.BaseCarry[i] = baseCarry.X;
            s.BaseCarry[N + i] = baseCarry.Y;
            s.BaseCarry[2 * N + i] = baseCarry.Z;
            s.BaseRot[i] = baseRotation.X;
            s.BaseRot[N + i] = baseRotation.Y;
            s.BaseRot[2 * N + i] = baseRotation.Z;
            s.BaseRot[3 * N + i] = baseRotation.W;

            // Every read of a link from the thread's arrays before any of its writes, as in the load.
            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                Real qv = q[d], qdv = qd[d], tv = tau[d], li = limitImplicit[d];
                s.Q[at] = qv; s.Qd[at] = qdv; s.Tau[at] = tv; s.LimitImplicit[at] = li;
            }

            for (int l = 0; l < links; l++)
            {
                int a3 = 3 * l * N + i, a4 = 4 * l * N + i, a9 = 9 * l * N + i, a6 = 6 * l * N + i;
                Real position0 = position[3 * l];
                Real spin0 = spin[3 * l];
                Real velocity0 = velocity[3 * l];
                Real relVel0 = relVel[3 * l];
                Real water0 = water[3 * l];
                Real wacc0 = wacc[3 * l];
                Real position1 = position[3 * l + 1];
                Real spin1 = spin[3 * l + 1];
                Real velocity1 = velocity[3 * l + 1];
                Real relVel1 = relVel[3 * l + 1];
                Real water1 = water[3 * l + 1];
                Real wacc1 = wacc[3 * l + 1];
                Real position2 = position[3 * l + 2];
                Real spin2 = spin[3 * l + 2];
                Real velocity2 = velocity[3 * l + 2];
                Real relVel2 = relVel[3 * l + 2];
                Real water2 = water[3 * l + 2];
                Real wacc2 = wacc[3 * l + 2];
                Real ballRot0 = ballRot[4 * l];
                Real rotation0 = rotation[4 * l];
                Real ballRot1 = ballRot[4 * l + 1];
                Real rotation1 = rotation[4 * l + 1];
                Real ballRot2 = ballRot[4 * l + 2];
                Real rotation2 = rotation[4 * l + 2];
                Real ballRot3 = ballRot[4 * l + 3];
                Real rotation3 = rotation[4 * l + 3];
                Real rotationMatrix0 = rotationMatrix[9 * l];
                Real sang0 = sang[9 * l];
                Real slin0 = slin[9 * l];
                Real rotationMatrix1 = rotationMatrix[9 * l + 1];
                Real sang1 = sang[9 * l + 1];
                Real slin1 = slin[9 * l + 1];
                Real rotationMatrix2 = rotationMatrix[9 * l + 2];
                Real sang2 = sang[9 * l + 2];
                Real slin2 = slin[9 * l + 2];
                Real rotationMatrix3 = rotationMatrix[9 * l + 3];
                Real sang3 = sang[9 * l + 3];
                Real slin3 = slin[9 * l + 3];
                Real rotationMatrix4 = rotationMatrix[9 * l + 4];
                Real sang4 = sang[9 * l + 4];
                Real slin4 = slin[9 * l + 4];
                Real rotationMatrix5 = rotationMatrix[9 * l + 5];
                Real sang5 = sang[9 * l + 5];
                Real slin5 = slin[9 * l + 5];
                Real rotationMatrix6 = rotationMatrix[9 * l + 6];
                Real sang6 = sang[9 * l + 6];
                Real slin6 = slin[9 * l + 6];
                Real rotationMatrix7 = rotationMatrix[9 * l + 7];
                Real sang7 = sang[9 * l + 7];
                Real slin7 = slin[9 * l + 7];
                Real rotationMatrix8 = rotationMatrix[9 * l + 8];
                Real sang8 = sang[9 * l + 8];
                Real slin8 = slin[9 * l + 8];
                Real cbias0 = cbias[6 * l];
                Real fext0 = fext[6 * l];
                Real cbias1 = cbias[6 * l + 1];
                Real fext1 = fext[6 * l + 1];
                Real cbias2 = cbias[6 * l + 2];
                Real fext2 = fext[6 * l + 2];
                Real cbias3 = cbias[6 * l + 3];
                Real fext3 = fext[6 * l + 3];
                Real cbias4 = cbias[6 * l + 4];
                Real fext4 = fext[6 * l + 4];
                Real cbias5 = cbias[6 * l + 5];
                Real fext5 = fext[6 * l + 5];

                s.Pos[a3] = position0;
                s.Spin[a3] = spin0;
                s.Vel[a3] = velocity0;
                s.RelVel[a3] = relVel0;
                s.Water[a3] = water0;
                s.WaterAcc[a3] = wacc0;
                s.Pos[a3 + N] = position1;
                s.Spin[a3 + N] = spin1;
                s.Vel[a3 + N] = velocity1;
                s.RelVel[a3 + N] = relVel1;
                s.Water[a3 + N] = water1;
                s.WaterAcc[a3 + N] = wacc1;
                s.Pos[a3 + 2 * N] = position2;
                s.Spin[a3 + 2 * N] = spin2;
                s.Vel[a3 + 2 * N] = velocity2;
                s.RelVel[a3 + 2 * N] = relVel2;
                s.Water[a3 + 2 * N] = water2;
                s.WaterAcc[a3 + 2 * N] = wacc2;
                s.BallRot[a4] = ballRot0;
                s.Rot[a4] = rotation0;
                s.BallRot[a4 + N] = ballRot1;
                s.Rot[a4 + N] = rotation1;
                s.BallRot[a4 + 2 * N] = ballRot2;
                s.Rot[a4 + 2 * N] = rotation2;
                s.BallRot[a4 + 3 * N] = ballRot3;
                s.Rot[a4 + 3 * N] = rotation3;
                s.RotM[a9] = rotationMatrix0;
                s.Sang[a9] = sang0;
                s.Slin[a9] = slin0;
                s.RotM[a9 + N] = rotationMatrix1;
                s.Sang[a9 + N] = sang1;
                s.Slin[a9 + N] = slin1;
                s.RotM[a9 + 2 * N] = rotationMatrix2;
                s.Sang[a9 + 2 * N] = sang2;
                s.Slin[a9 + 2 * N] = slin2;
                s.RotM[a9 + 3 * N] = rotationMatrix3;
                s.Sang[a9 + 3 * N] = sang3;
                s.Slin[a9 + 3 * N] = slin3;
                s.RotM[a9 + 4 * N] = rotationMatrix4;
                s.Sang[a9 + 4 * N] = sang4;
                s.Slin[a9 + 4 * N] = slin4;
                s.RotM[a9 + 5 * N] = rotationMatrix5;
                s.Sang[a9 + 5 * N] = sang5;
                s.Slin[a9 + 5 * N] = slin5;
                s.RotM[a9 + 6 * N] = rotationMatrix6;
                s.Sang[a9 + 6 * N] = sang6;
                s.Slin[a9 + 6 * N] = slin6;
                s.RotM[a9 + 7 * N] = rotationMatrix7;
                s.Sang[a9 + 7 * N] = sang7;
                s.Slin[a9 + 7 * N] = slin7;
                s.RotM[a9 + 8 * N] = rotationMatrix8;
                s.Sang[a9 + 8 * N] = sang8;
                s.Slin[a9 + 8 * N] = slin8;
                s.Cbias[a6] = cbias0;
                s.Fext[a6] = fext0;
                s.Cbias[a6 + N] = cbias1;
                s.Fext[a6 + N] = fext1;
                s.Cbias[a6 + 2 * N] = cbias2;
                s.Fext[a6 + 2 * N] = fext2;
                s.Cbias[a6 + 3 * N] = cbias3;
                s.Fext[a6 + 3 * N] = fext3;
                s.Cbias[a6 + 4 * N] = cbias4;
                s.Fext[a6 + 4 * N] = fext4;
                s.Cbias[a6 + 5 * N] = cbias5;
                s.Fext[a6 + 5 * N] = fext5;
            }
#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 9, ref clk);
#endif
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
            // A body the reach guard recorded this step (LoseStrays) dies here, after the streams
            // join, rather than in LoseStrays, beside which the water and the fluid read Alive.
            if (gl.Alive[g] != 0 && gl.StrayCode[g] != 0) gl.Alive[g] = 0;

            if (gl.Alive[g] == 0)
            {
                KeepSpheres(g, links, cs, ps, cfg);
                return;
            }

            int dof = t.Dof[i];
#if GPU_PHASE_PROBE
            long clk = 0;
            if (CudaAsm.IsSupported) CudaAsm.Emit("mov.u64 %0, %%clock64;", out clk);
#endif

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
            var posErr = new Real[3 * MaxLinks];
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
            var sScent = new float[MaxLinks];
            var sLeaf = new float[4 * MaxLinks];   // a part's leaf strength, then its direction's three
            var sFlow = new float[3 * MaxLinks];
            var sAngle = new float[MaxDof];
            var sRate = new float[MaxDof];
            var sDrive = new float[3 * MaxDof];

            var six = new Real[42];
            var sol = new Real[6];
            var residual = new Real[3];
            var cand = new int[CandCap];
            var weldAnchor = new int[MaxLinks];
            var jacobiM = new Real[9];
            var jacobiV = new Real[9];

            // A card does not zero a thread's local memory. The senses the brain may read without
            // this step writing them (a channel whose mask bit is clear) read 0, never garbage.
            for (int k = 0; k < MaxLinks; k++)
            {
                sDepth[k] = 0f; sUp[k] = 0f; sChem[k] = 0f; sScent[k] = 0f;
                sLeaf[4 * k] = 0f; sLeaf[4 * k + 1] = 0f; sLeaf[4 * k + 2] = 0f; sLeaf[4 * k + 3] = 0f;
                sFlow[3 * k] = 0f; sFlow[3 * k + 1] = 0f; sFlow[3 * k + 2] = 0f;
                driveTq[3 * k] = 0; driveTq[3 * k + 1] = 0; driveTq[3 * k + 2] = 0;
            }
            for (int k = 0; k < MaxDof; k++)
            {
                sAngle[k] = 0f; sRate[k] = 0f;
                sDrive[3 * k] = 0f; sDrive[3 * k + 1] = 0f; sDrive[3 * k + 2] = 0f;
            }

            // ---- load ------------------------------------------------------------------------
            // Every read of a link before any of its writes (card-next-r57.md, part 6): the reads are
            // then independent of the stores to the thread's arrays in the PTX itself, so the card can
            // have them all in flight at once rather than one memory latency a value. The same values
            // land in the same places as the loop over k did.
            for (int l = 0; l < links; l++)
            {
                int at = l * N + i;
                int a3 = 3 * l * N + i, a4 = 4 * l * N + i, a9 = 9 * l * N + i, a6 = 6 * l * N + i;
                int parent_ = t.Parent[at];
                int dofCount_ = t.DofCount[at];
                int dofStart_ = t.DofStart[at];
                int panelStart_ = t.PanelStart[at];
                int thinAxis_ = t.ThinAxis[at];
                Real mass_ = b.Mass[at];
                Real lift_ = b.Lift[at];
                Real volume_ = b.Volume[at];
                Real smallI_ = b.SmallI[at];
                Real reach_ = b.Reach[at];
                Real arm_ = b.Arm[at];
                Real inertiaLocal0 = b.Inertia[a3];
                Real childAnchor0 = b.ChildAnchor[a3];
                Real parentAnchor0 = b.ParentAnchor[a3];
                Real position0 = s.Pos[a3];
                Real spin0 = s.Spin[a3];
                Real velocity0 = s.Vel[a3];
                Real relVel0 = s.RelVel[a3];
                Real water0 = s.Water[a3];
                Real wacc0 = s.WaterAcc[a3];
                Real inertiaLocal1 = b.Inertia[a3 + N];
                Real childAnchor1 = b.ChildAnchor[a3 + N];
                Real parentAnchor1 = b.ParentAnchor[a3 + N];
                Real position1 = s.Pos[a3 + N];
                Real spin1 = s.Spin[a3 + N];
                Real velocity1 = s.Vel[a3 + N];
                Real relVel1 = s.RelVel[a3 + N];
                Real water1 = s.Water[a3 + N];
                Real wacc1 = s.WaterAcc[a3 + N];
                Real inertiaLocal2 = b.Inertia[a3 + 2 * N];
                Real childAnchor2 = b.ChildAnchor[a3 + 2 * N];
                Real parentAnchor2 = b.ParentAnchor[a3 + 2 * N];
                Real position2 = s.Pos[a3 + 2 * N];
                Real spin2 = s.Spin[a3 + 2 * N];
                Real velocity2 = s.Vel[a3 + 2 * N];
                Real relVel2 = s.RelVel[a3 + 2 * N];
                Real water2 = s.Water[a3 + 2 * N];
                Real wacc2 = s.WaterAcc[a3 + 2 * N];
                Real jointFrame0 = b.JointFrame[a4];
                Real restFrame0 = b.RestFrame[a4];
                Real ballRot0 = s.BallRot[a4];
                Real rotation0 = s.Rot[a4];
                Real jointFrame1 = b.JointFrame[a4 + N];
                Real restFrame1 = b.RestFrame[a4 + N];
                Real ballRot1 = s.BallRot[a4 + N];
                Real rotation1 = s.Rot[a4 + N];
                Real jointFrame2 = b.JointFrame[a4 + 2 * N];
                Real restFrame2 = b.RestFrame[a4 + 2 * N];
                Real ballRot2 = s.BallRot[a4 + 2 * N];
                Real rotation2 = s.Rot[a4 + 2 * N];
                Real jointFrame3 = b.JointFrame[a4 + 3 * N];
                Real restFrame3 = b.RestFrame[a4 + 3 * N];
                Real ballRot3 = s.BallRot[a4 + 3 * N];
                Real rotation3 = s.Rot[a4 + 3 * N];
                Real rotationMatrix0 = s.RotM[a9];
                Real sang0 = s.Sang[a9];
                Real slin0 = s.Slin[a9];
                Real rotationMatrix1 = s.RotM[a9 + N];
                Real sang1 = s.Sang[a9 + N];
                Real slin1 = s.Slin[a9 + N];
                Real rotationMatrix2 = s.RotM[a9 + 2 * N];
                Real sang2 = s.Sang[a9 + 2 * N];
                Real slin2 = s.Slin[a9 + 2 * N];
                Real rotationMatrix3 = s.RotM[a9 + 3 * N];
                Real sang3 = s.Sang[a9 + 3 * N];
                Real slin3 = s.Slin[a9 + 3 * N];
                Real rotationMatrix4 = s.RotM[a9 + 4 * N];
                Real sang4 = s.Sang[a9 + 4 * N];
                Real slin4 = s.Slin[a9 + 4 * N];
                Real rotationMatrix5 = s.RotM[a9 + 5 * N];
                Real sang5 = s.Sang[a9 + 5 * N];
                Real slin5 = s.Slin[a9 + 5 * N];
                Real rotationMatrix6 = s.RotM[a9 + 6 * N];
                Real sang6 = s.Sang[a9 + 6 * N];
                Real slin6 = s.Slin[a9 + 6 * N];
                Real rotationMatrix7 = s.RotM[a9 + 7 * N];
                Real sang7 = s.Sang[a9 + 7 * N];
                Real slin7 = s.Slin[a9 + 7 * N];
                Real rotationMatrix8 = s.RotM[a9 + 8 * N];
                Real sang8 = s.Sang[a9 + 8 * N];
                Real slin8 = s.Slin[a9 + 8 * N];
                Real cbias0 = s.Cbias[a6];
                Real cbias1 = s.Cbias[a6 + N];
                Real cbias2 = s.Cbias[a6 + 2 * N];
                Real cbias3 = s.Cbias[a6 + 3 * N];
                Real cbias4 = s.Cbias[a6 + 4 * N];
                Real cbias5 = s.Cbias[a6 + 5 * N];

                parent[l] = parent_;
                dofCount[l] = dofCount_;
                dofStart[l] = dofStart_;
                panelStart[l] = panelStart_;
                thinAxis[l] = thinAxis_;
                mass[l] = mass_;
                lift[l] = lift_;
                volume[l] = volume_;
                smallI[l] = smallI_;
                reach[l] = reach_;
                arm[l] = arm_;
                inertiaLocal[3 * l] = inertiaLocal0;
                childAnchor[3 * l] = childAnchor0;
                parentAnchor[3 * l] = parentAnchor0;
                position[3 * l] = position0;
                spin[3 * l] = spin0;
                velocity[3 * l] = velocity0;
                relVel[3 * l] = relVel0;
                water[3 * l] = water0;
                wacc[3 * l] = wacc0;
                inertiaLocal[3 * l + 1] = inertiaLocal1;
                childAnchor[3 * l + 1] = childAnchor1;
                parentAnchor[3 * l + 1] = parentAnchor1;
                position[3 * l + 1] = position1;
                spin[3 * l + 1] = spin1;
                velocity[3 * l + 1] = velocity1;
                relVel[3 * l + 1] = relVel1;
                water[3 * l + 1] = water1;
                wacc[3 * l + 1] = wacc1;
                inertiaLocal[3 * l + 2] = inertiaLocal2;
                childAnchor[3 * l + 2] = childAnchor2;
                parentAnchor[3 * l + 2] = parentAnchor2;
                position[3 * l + 2] = position2;
                spin[3 * l + 2] = spin2;
                velocity[3 * l + 2] = velocity2;
                relVel[3 * l + 2] = relVel2;
                water[3 * l + 2] = water2;
                wacc[3 * l + 2] = wacc2;
                jointFrame[4 * l] = jointFrame0;
                restFrame[4 * l] = restFrame0;
                ballRot[4 * l] = ballRot0;
                rotation[4 * l] = rotation0;
                jointFrame[4 * l + 1] = jointFrame1;
                restFrame[4 * l + 1] = restFrame1;
                ballRot[4 * l + 1] = ballRot1;
                rotation[4 * l + 1] = rotation1;
                jointFrame[4 * l + 2] = jointFrame2;
                restFrame[4 * l + 2] = restFrame2;
                ballRot[4 * l + 2] = ballRot2;
                rotation[4 * l + 2] = rotation2;
                jointFrame[4 * l + 3] = jointFrame3;
                restFrame[4 * l + 3] = restFrame3;
                ballRot[4 * l + 3] = ballRot3;
                rotation[4 * l + 3] = rotation3;
                rotationMatrix[9 * l] = rotationMatrix0;
                sang[9 * l] = sang0;
                slin[9 * l] = slin0;
                rotationMatrix[9 * l + 1] = rotationMatrix1;
                sang[9 * l + 1] = sang1;
                slin[9 * l + 1] = slin1;
                rotationMatrix[9 * l + 2] = rotationMatrix2;
                sang[9 * l + 2] = sang2;
                slin[9 * l + 2] = slin2;
                rotationMatrix[9 * l + 3] = rotationMatrix3;
                sang[9 * l + 3] = sang3;
                slin[9 * l + 3] = slin3;
                rotationMatrix[9 * l + 4] = rotationMatrix4;
                sang[9 * l + 4] = sang4;
                slin[9 * l + 4] = slin4;
                rotationMatrix[9 * l + 5] = rotationMatrix5;
                sang[9 * l + 5] = sang5;
                slin[9 * l + 5] = slin5;
                rotationMatrix[9 * l + 6] = rotationMatrix6;
                sang[9 * l + 6] = sang6;
                slin[9 * l + 6] = slin6;
                rotationMatrix[9 * l + 7] = rotationMatrix7;
                sang[9 * l + 7] = sang7;
                slin[9 * l + 7] = slin7;
                rotationMatrix[9 * l + 8] = rotationMatrix8;
                sang[9 * l + 8] = sang8;
                slin[9 * l + 8] = slin8;
                cbias[6 * l] = cbias0;
                cbias[6 * l + 1] = cbias1;
                cbias[6 * l + 2] = cbias2;
                cbias[6 * l + 3] = cbias3;
                cbias[6 * l + 4] = cbias4;
                cbias[6 * l + 5] = cbias5;
            }
            panelStart[links] = t.PanelStart[links * N + i];

            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                Real lo = b.LimitLo[at], hi = b.LimitHi[at], stiff = b.LimitStiff[at], damp = b.LimitDamp[at];
                Real qv = s.Q[at], qdv = s.Qd[at];
                limitLo[d] = lo; limitHi[d] = hi; limitStiff[d] = stiff; limitDamp[d] = damp;
                q[d] = qv; qd[d] = qdv;
            }
            var basePosition = new V3(s.BasePos[i], s.BasePos[N + i], s.BasePos[2 * N + i]);
            var baseCarry = new V3(s.BaseCarry[i], s.BaseCarry[N + i], s.BaseCarry[2 * N + i]);
            var baseRotation = new Q4(s.BaseRot[i], s.BaseRot[N + i], s.BaseRot[2 * N + i], s.BaseRot[3 * N + i]);

            Real excess = b.Excess[i];
            Real totalMass = b.TotalMass[i];

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 0, ref clk);
#endif
            // ---- 1. the water pass (Water.Sample, per link, no hold) --------------------------
            // WaterLinks ran it before this kernel, a link a thread, from the positions the load
            // read, and wrote the two arrays the load read. Still water left both as they stood,
            // and a current with no acceleration term left the acceleration as it stood.

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 1, ref clk);
#endif
            // ---- 2. senses, 3. brain ----------------------------------------------------------
            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << SDepth)) != 0;
            bool readsUp = (mask & (1 << SUp)) != 0;
            bool readsJoint = (mask & (1 << SJointAngle)) != 0 || (mask & (1 << SJointRate)) != 0;
            bool readsFlow = (mask & (1 << SFlow)) != 0;
            bool readsChemical = (mask & (1 << SChemical)) != 0;
            bool readsEnergy = (mask & (1 << SEnergy)) != 0;
            bool readsScent = (mask & (1 << SScent)) != 0;
            bool readsLeafDir = (mask & (1 << SLeafDir)) != 0;
            bool readsLeaf = (mask & (1 << SLeaf)) != 0 || readsLeafDir;
            bool readsDrive = (mask & (1 << SStroke)) != 0 || (mask & (1 << SHold)) != 0 || (mask & (1 << SRamp)) != 0;

            int gates = br.Gates[i];
            bool hasNutrients = (gates & 1) != 0;
            bool hasReserve = (gates & 2) != 0;
            bool hasScent = (gates & 4) != 0;
            bool hasLeaf = (gates & 8) != 0;

            // D220: a body whose organism is remains runs no senses, no brain, no drive and no grip, as
            // DynamicsWorld.StepOne skips them. cfg.AnyRemains is the block's, so in a world without remains
            // every thread takes the old path on a test it cannot fail, and the flag is never loaded.
            bool remains = cfg.AnyRemains != 0 && gl.Remains[g] != 0;

            // CreatureSenses.SampleDrive: the envelope as the last step's drive left it, read before this
            // step's drive moves it. Under the torque drive it reads 0, as sDrive was cleared.
            if (!remains && readsDrive && cfg.BendAndPower != 0)
            {
                int dofLength = dof > 0 ? dof : 1;
                for (int bl = 0; bl < links; bl++)
                {
                    Real partSum = position[3 * bl] + position[3 * bl + 1] + position[3 * bl + 2];
                    if (partSum != partSum || partSum == Real.PositiveInfinity || partSum == Real.NegativeInfinity) continue;

                    int n = dofCount[bl];
                    int offset = dofStart[bl];
                    if (n == 0 || offset < 0) continue;

                    double hold = b.HoldGene[bl * N + i];
                    for (int d = 0; d < n; d++)
                    {
                        int at = offset + d;
                        if (at >= dofLength) break;

                        int e = at * N + i;
                        double stroke = dr.EPower[e];
                        sDrive[3 * at] = Clamp((float)stroke);
                        sDrive[3 * at + 1] = hold > 0 ? (float)MaxD(0.0, MinD(1.0, dr.EHoldLeft[e] / hold)) : 0f;
                        sDrive[3 * at + 2] = Clamp((float)(dr.EPowerTarget[e] - stroke));
                    }
                }
            }

            float energy = 0f;
            bool lost = false;
            if (!remains)
            {
                Sample(i, links, dofCount, dofStart, position, rotationMatrix, relVel, q, qd, limitHi, b,
                       br, w, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical && hasNutrients,
                       readsScent && hasScent, readsEnergy && hasReserve, readsLeaf && hasLeaf, readsLeafDir,
                       sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, sLeaf, ref energy, ref lost);
            }

            if (lost)
            {
                // The chemical sense's walk gave up on this body (TankCellAt): far beyond the glass,
                // or at a distance that is not finite. It is left unstepped from here, its state as
                // the step found it, and lost as the finiteness check below loses a body; the
                // farm's glass guard kills it at its next check.
                gl.Alive[g] = 0;
                KeepSpheres(g, links, cs, ps, cfg);
                ps.R[g] = 0;
                ps.Active[g] = 0;

                if (cfg.PerPart != 0)
                {
                    for (int l = 0; l < links; l++) ps.LR[g * LinkStride + l] = 0;
                }

                return;
            }

            // The touch of the rock as the last step's contact pass left it, a bit a link, read before
            // this step's touch is kept (KeepTouch), as CreatureSenses.Sample reads it.
            int rock = 0;
            if (!remains && cfg.RockTouch != 0)
            {
                for (int l = 0; l < links; l++)
                {
                    if (gl.RockTouched[g * LinkStride + l] != 0) rock |= 1 << l;
                }
            }

            // D235: in a nose world a part that is not a nose reads zero on the four smell channels, whatever the
            // gates (SensorChannels.IsSmell), the chemical sense's standing-in constant included. The nose links ride
            // in the same word as the rock's bits, a bit a link from bit 16 (a class holds at most 16 links), so
            // that SensorRead, which already takes the word, needs no new argument. SRock reads only bits 0 to 15.
            if (!remains && cfg.Nose != 0)
            {
                for (int l = 0; l < links; l++)
                {
                    if (b.NoseFlag[l * N + i] != 0) rock |= 1 << (16 + l);
                }
            }

            // The brain reads the two gates itself (SensorRead): a body with no field or no
            // reserve source reads the standing-in constant on those channels. Remains run none.
            if (!remains)
            {
                BrainStep(i, links, dof, t, nr, br, dr, cfg, dofCount, dofStart,
                          sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf);
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 2, ref clk);
#endif
            // ---- clear -----------------------------------------------------------------------
            for (int k = 0; k < 6 * links; k++) fext[k] = 0;
            for (int k = 0; k < dof; k++) tau[k] = 0;

            // ---- 4. drive (EffectorDrive.Drive) ----------------------------------------------
            bool drivePending = false;
            if (!remains && dof != 0 && cfg.BendAndPower != 0)
            {
                // EffectorDrive.DriveBendAndPower: each degree of freedom's power and bend through
                // the joint's threshold and envelope, in double as the CPU keeps them; the power
                // stays in EPower for the fluid's push below, and a critically damped muscle holds
                // the bend, capped by the part's power.
                int sigLen = t.SigLen[i];
                float powerScale = b.PowerScale[i];
                double stepD = cfg.DtD;
                double poseOmega = cfg.PoseOmega;

                long limited = 0;

                for (int bl = 1; bl < links; bl++)
                {
                    int n = dofCount[bl];
                    if (n == 0) continue;

                    int offset = dofStart[bl];
                    int gene = bl * N + i;
                    int mirror = cfg.MirrorDrive != 0 ? b.MirrorMask[gene] : 0;   // D234, Creature.MirrorMask
                    double threshold = b.Threshold[gene];
                    double reaction = b.Reaction[gene];
                    double holdGene = b.HoldGene[gene];
                    double release = b.Release[gene];
                    Q4 frame = Q4.Read(jointFrame, 4 * bl);

                    double allowed = cfg.LimitDrive != 0 ? (double)(smallI[bl] * cfg.SpinBudget) : double.MaxValue;
                    double cap = MinD(allowed, (double)b.LinkPower[gene] * powerScale);
                    double inertia = smallI[bl];

                    V3 torque = V3.Zero;

                    for (int d = 0; d < n; d++)
                    {
                        int idx = offset + d;
                        int e = idx * N + i;

                        float v = idx < sigLen ? dr.Signal[e] : 0f;
                        double level = MaxF(0f, MinF(1f, v));
                        double power = level <= threshold ? 0.0 : MinD(1.0, (level - threshold) / (1.0 - threshold));
                        float wv = idx < sigLen ? dr.Bend[e] : 0f;
                        double bend = (MaxF(-1f, MinF(1f, wv)) + 1.0) * 0.5;

                        double pw = dr.EPower[e], bd = dr.EBend[e];
                        double pt = dr.EPowerTarget[e], bt = dr.EBendTarget[e];
                        double pr = dr.EPowerRate[e], br8 = dr.EBendRate[e];
                        double holdLeft = dr.EHoldLeft[e];

                        if (holdLeft > 0)
                        {
                            holdLeft -= stepD;
                        }
                        else if (AbsD(power - pt) > 0.01 || AbsD(bend - bt) > 0.01)
                        {
                            bool fromRest = pw == 0 && pt == 0;
                            double ramp = fromRest ? reaction : release;
                            if (ramp < stepD) ramp = stepD;
                            pt = power;
                            bt = bend;
                            pr = AbsD(power - pw) / ramp;
                            br8 = AbsD(bend - bd) / ramp;
                            holdLeft = holdGene;
                        }

                        pw = Toward(pw, pt, pr * stepD);
                        bd = Toward(bd, bt, br8 * stepD);

                        dr.EPower[e] = pw; dr.EBend[e] = bd;
                        dr.EPowerTarget[e] = pt; dr.EBendTarget[e] = bt;
                        dr.EPowerRate[e] = pr; dr.EBendRate[e] = br8;
                        dr.EHoldLeft[e] = holdLeft;

                        if (poseOmega <= 0 || inertia <= 0) continue;

                        // D234, EffectorDrive.DriveBendAndPower: a reflected degree of freedom works in the signed
                        // reading, the genome's own stops (the stored ones swapped and negated back), the signed
                        // angle and rate, and the torque reversed back to the stored angle. The sign is exactly 1
                        // everywhere else, and in a world without the rule no mask is read at all.
                        bool flipped = mirror != 0 && ((mirror >> d) & 1) != 0;
                        double sign = flipped ? -1.0 : 1.0;
                        double lo = flipped ? -(double)limitHi[idx] : (double)limitLo[idx];
                        double hi = flipped ? -(double)limitLo[idx] : (double)limitHi[idx];
                        double target = lo + bd * (hi - lo);
                        double magnitude = inertia * poseOmega * (poseOmega * (target - sign * q[idx]) - 2.0 * (sign * qd[idx]));
                        if (magnitude > cap) { magnitude = cap; limited++; }
                        else if (magnitude < -cap) { magnitude = -cap; limited++; }

                        torque = torque + frame.Rotate(V3.Axis(d)) * (Real)(sign * magnitude);
                    }

                    M3 rot = M3.Read(rotationMatrix, 9 * bl);
                    V3 world = rot * torque;

                    s.Applied[(3 * bl) * N + i] = world.X;
                    s.Applied[(3 * bl + 1) * N + i] = world.Y;
                    s.Applied[(3 * bl + 2) * N + i] = world.Z;

                    V3.Write(driveTq, 3 * bl, world);
                    V3.Write(preRelSpin, 3 * bl, V3.Read(spin, 3 * bl) - V3.Read(spin, 3 * parent[bl]));
                    drivePending = true;

                    V3.Add(fext, 6 * bl, world);
                    V3.Add(fext, 6 * parent[bl], -world);
                }

                if (limited != 0) dr.DriveLimited[i] = dr.DriveLimited[i] + limited;
            }
            else if (!remains && dof != 0)
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
                    int mirror = cfg.MirrorDrive != 0 ? b.MirrorMask[bl * N + i] : 0;   // D234, Creature.MirrorMask
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

                        // D234: a reflected degree of freedom's torque is reversed, after the limiter.
                        if (((mirror >> d) & 1) != 0) magnitude = -magnitude;

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

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 3, ref clk);
#endif
            // ---- 5. fluid (Fluid.Apply) --------------------------------------------------------
            // FluidLinks ran it before this kernel, a link a thread; its results are taken here in
            // link order, at the place the CPU applies them.
            long dragLimited = 0;
            int fluidRows = cfg.LinkRows;
            bool powered = cfg.BendAndPower != 0 && cfg.LimbPush > 0 && cfg.PushOmega > 0;
            for (int fl = 0; fl < links; fl++)
            {
                int frow = g * LinkStride + fl;
                for (int fk = 0; fk < 3; fk++)
                {
                    relVel[3 * fl + fk] = gl.LinkFluid[fk * fluidRows + frow];
                    dragF[3 * fl + fk] = gl.LinkFluid[(3 + fk) * fluidRows + frow];
                    dragT[3 * fl + fk] = gl.LinkFluid[(6 + fk) * fluidRows + frow];
                    preVel[3 * fl + fk] = velocity[3 * fl + fk];
                    preSpin[3 * fl + fk] = spin[3 * fl + fk];
                }
                V3.Add(fext, 6 * fl, new V3(gl.LinkFluid[9 * fluidRows + frow], gl.LinkFluid[10 * fluidRows + frow],
                                            gl.LinkFluid[11 * fluidRows + frow]));
                V3 linkForce = new V3(gl.LinkFluid[12 * fluidRows + frow], gl.LinkFluid[13 * fluidRows + frow],
                                      gl.LinkFluid[14 * fluidRows + frow]);

                // Under the bend-and-power drive FluidLinks left the force before the power's push
                // and the buoyancy term: the push from this step's stroke, its work booked as
                // Fluid.Apply books it, then the buoyancy where the CPU added it.
                if (powered)
                {
                    if (!remains && parent[fl] >= 0 && position[3 * fl + 1] <= 0 && dofCount[fl] > 0)
                    {
                        M3 frot = M3.Read(rotationMatrix, 9 * fl);
                        V3 toOrigin = frot * (-V3.Read(childAnchor, 3 * fl));
                        Real linkReach = toOrigin.Magnitude;
                        if (linkReach > 0)
                        {
                            V3 axis = toOrigin * ((Real)1.0 / linkReach);
                            Q4 fframe = Q4.Read(jointFrame, 4 * fl);
                            int fstart = dofStart[fl];
                            Real omega = cfg.PushOmega;
                            Real swing2 = 0;

                            // D229, as Fluid.Apply: each degree of freedom moves the limb in the unit
                            // vector of dofAxis x axis, and the added mass is the part's for it. A sphere
                            // (shape 0) keeps the unshaped line.
                            int shapeCode = cfg.ShapedMass != 0 ? b.ShapeCode[fl * N + i] : 0;
                            Real hx0 = 0, hx1 = 0, hx2 = 0;

                            // D255, as Fluid.Apply: a family limb (shape code 3) takes the strip integral its host body
                            // carries for each degree of freedom, in place of D229's lines. It reads no half-extents.
                            // Not entered for any other part, whose lines below are unchanged.
                            bool family = shapeCode == 3;
                            Real familySum = 0;
                            Real familyMass = 0;
                            if (shapeCode != 0 && !family)
                            {
                                hx0 = b.HalfExt[(3 * fl) * N + i];
                                hx1 = b.HalfExt[(3 * fl + 1) * N + i];
                                hx2 = b.HalfExt[(3 * fl + 2) * N + i];
                            }
                            Real shapedSum = 0;

                            // D231, as Fluid.Apply: a link with parts welded beyond it takes its constants from
                            // the tables, and none of the lines below. Not entered with the switch off.
                            bool assembly = cfg.StrokeAssembly != 0 && b.StrokeParts[fl * N + i] > 1;
                            Real assemblySum = 0;
                            Real assemblyMass = 0;

                            for (int d = 0; d < dofCount[fl]; d++)
                            {
                                Real sv = (Real)dr.EPower[(fstart + d) * N + i];
                                if (sv <= 0) continue;

                                if (assembly)
                                {
                                    // q_d = (s_d Omega)^2 (1 - along_d^2), with the along of the link's own frame.
                                    Real alongStored = b.StrokeAlong[(fstart + d) * N + i];
                                    Real strokeQ = sv * omega * sv * omega * ((Real)1.0 - alongStored * alongStored);
                                    swing2 += strokeQ;
                                    assemblySum += strokeQ * b.StrokeMoment[(fstart + d) * N + i];
                                    assemblyMass += strokeQ * b.StrokeMass[(fstart + d) * N + i];
                                    continue;
                                }

                                V3 dofAxis = frot * fframe.Rotate(V3.Axis(d));
                                Real along = V3.Dot(dofAxis, axis);
                                swing2 += sv * omega * sv * omega * ((Real)1.0 - along * along);

                                if (family)
                                {
                                    // q_d as above, times the degree's I1 and M, which the host wrote at build and
                                    // at every resize (Fluid.Apply's familyQ).
                                    Real familyQ = sv * omega * sv * omega * ((Real)1.0 - along * along);
                                    familySum += familyQ * b.FamMoment[(fstart + d) * N + i];
                                    familyMass += familyQ * b.FamMass[(fstart + d) * N + i];
                                }

                                if (shapeCode != 0 && !family)
                                {
                                    V3 motion = V3.Cross(dofAxis, axis);
                                    Real motionLength = motion.Magnitude;
                                    if (motionLength > 0)
                                    {
                                        V3 strokeDirection = motion * ((Real)1.0 / motionLength);
                                        shapedSum += LeverOf(linkReach, axis, strokeDirection) *
                                            ShapedAddedMass(
                                                shapeCode, hx0, hx1, hx2, frot, axis,
                                                strokeDirection, cfg.Density) *
                                            (sv * omega * sv * omega * ((Real)1.0 - along * along));
                                    }
                                }
                            }

                            if (swing2 > 0)
                            {
                                Real addedMass = cfg.AddedMass * cfg.Density * volume[fl];
                                Real push = cfg.LimbPush * addedMass * linkReach * swing2;
                                if (shapeCode != 0 && !family) push = cfg.LimbPush * shapedSum;

                                // D231: C_T times the sum of q_d I1_d, and the push-weighted lever
                                // (sum q_d I1_d) / (sum q_d M_d). Another link's lever is the reach.
                                Real lever = linkReach;
                                if (assembly)
                                {
                                    push = cfg.LimbPush * assemblySum;
                                    lever = AssemblyLeverOf(assemblySum, assemblyMass, linkReach);
                                }
                                else if (family)
                                {
                                    // D255: the same law for a family limb, from the strip integral of its true slices.
                                    push = cfg.LimbPush * familySum;
                                    lever = AssemblyLeverOf(familySum, familyMass, linkReach);
                                }

                                linkForce = linkForce + axis * (-push);
                                // The work is the push times the lever it acts at: a lever other than the reach
                                // (LeverOf) must enter this line too, as on the CPU.
                                dr.Work[i] = dr.Work[i] + (double)(push * lever * System.Math.Sqrt(swing2)) * cfg.DtD;
                            }
                        }
                    }

                    if (gl.LinkFluid[16 * fluidRows + frow] != 0)
                    {
                        linkForce = linkForce + new V3(0, gl.LinkFluid[15 * fluidRows + frow], 0);
                    }
                }

                V3.Add(fext, 6 * fl + 3, linkForce);
                dragLimited += gl.LinkLimited[frow];
            }
            if (cfg.LimitDrag != 0 && cfg.WeldedDrag != 0)
            {
                dragLimited += WeldedDrag(g * LinkStride, fluidRows, links, parent, dofCount, mass, position,
                                          rotationMatrix, inertiaLocal, spin, dragF, dragT, fext, weldAnchor,
                                          jacobiM, jacobiV, gl, cfg);
            }
            if (dragLimited != 0) dr.DragLimited[i] = dr.DragLimited[i] + dragLimited;

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 4, ref clk);
#endif
            // ---- 6. contacts (Contacts.Apply) ------------------------------------------------
            if (cfg.PerPart != 0)
            {
                ContactsPerPartStaged(g, links, fext, cand, cs, gl, gr, cfg);
                if (cfg.RockTouch != 0) KeepTouch(g, links, gl, cfg);
                if (cfg.Anchoring != 0 && !remains)
                {
                    Grip(g, links, position, velocity, spin, rotationMatrix, mass, smallI, fext,
                         hasReserve, br.Reserve[i], cs, gl, cfg);
                }
            }
            else
            {
                ContactsBody(g, links, mass, totalMass, fext, cand, cs, gl, gr, w, cfg);
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 5, ref clk);
#endif
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
                      ref basePosition, ref baseCarry, ref baseRotation, cfg.Dt);

            // D077's third rule inside the card's block (D204): a root that has left the box comes
            // back in at the opposite face, before the poses are built from it, as Simulation's
            // WrapAtTheSeams puts it back before anything reads a position.
            if (cfg.SeamL > 0) WrapAtTheSeams(ref basePosition, ref baseCarry, cfg);

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 6, ref clk);
#endif
            // ---- 10. poses, 11. velocities ----------------------------------------------------
            Poses(links, parent, dofCount, dofStart, q, ballRot, rotation, rotationMatrix,
                  position, restFrame, jointFrame, parentAnchor, childAnchor,
                  basePosition, baseRotation, baseCarry, posErr);
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

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 7, ref clk);
#endif
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
                        ps.LEx[row] = CarryBase ? posErr[0] : 0;
                        ps.LEy[row] = CarryBase ? posErr[1] : 0;
                        ps.LEz[row] = CarryBase ? posErr[2] : 0;
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
                            ps.LEx[row + l] = CarryBase ? posErr[3 * l] : 0;
                            ps.LEy[row + l] = CarryBase ? posErr[3 * l + 1] : 0;
                            ps.LEz[row + l] = CarryBase ? posErr[3 * l + 2] : 0;
                            ps.LR[row + l] = reach[l];
                            ps.LVx[row + l] = velocity[3 * l];
                            ps.LVy[row + l] = velocity[3 * l + 1];
                            ps.LVz[row + l] = velocity[3 * l + 2];
                        }
                    }

                    // D228: each link's turn travels with its sphere (RefreshLinkSpheres copies RotationMatrix).
                    if (cfg.TrueShape != 0)
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LRot0[row + l] = rotationMatrix[9 * l];
                            ps.LRot1[row + l] = rotationMatrix[9 * l + 1];
                            ps.LRot2[row + l] = rotationMatrix[9 * l + 2];
                            ps.LRot3[row + l] = rotationMatrix[9 * l + 3];
                            ps.LRot4[row + l] = rotationMatrix[9 * l + 4];
                            ps.LRot5[row + l] = rotationMatrix[9 * l + 5];
                            ps.LRot6[row + l] = rotationMatrix[9 * l + 6];
                            ps.LRot7[row + l] = rotationMatrix[9 * l + 7];
                            ps.LRot8[row + l] = rotationMatrix[9 * l + 8];
                        }
                    }

                    // D228 level (b): each link's world-frame spin travels with its sphere (RefreshLinkSpheres copies Spin).
                    if (cfg.AtPoint != 0)
                    {
                        for (int l = 0; l < links; l++)
                        {
                            ps.LSp0[row + l] = spin[3 * l];
                            ps.LSp1[row + l] = spin[3 * l + 1];
                            ps.LSp2[row + l] = spin[3 * l + 2];
                        }
                    }
                }
            }

#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 8, ref clk);
#endif
            // ---- store -----------------------------------------------------------------------
            s.BasePos[i] = basePosition.X;
            s.BasePos[N + i] = basePosition.Y;
            s.BasePos[2 * N + i] = basePosition.Z;
            s.BaseCarry[i] = baseCarry.X;
            s.BaseCarry[N + i] = baseCarry.Y;
            s.BaseCarry[2 * N + i] = baseCarry.Z;
            s.BaseRot[i] = baseRotation.X;
            s.BaseRot[N + i] = baseRotation.Y;
            s.BaseRot[2 * N + i] = baseRotation.Z;
            s.BaseRot[3 * N + i] = baseRotation.W;

            // Every read of a link from the thread's arrays before any of its writes, as in the load.
            for (int d = 0; d < dof; d++)
            {
                int at = d * N + i;
                Real qv = q[d], qdv = qd[d], tv = tau[d], li = limitImplicit[d];
                s.Q[at] = qv; s.Qd[at] = qdv; s.Tau[at] = tv; s.LimitImplicit[at] = li;
            }

            for (int l = 0; l < links; l++)
            {
                int a3 = 3 * l * N + i, a4 = 4 * l * N + i, a9 = 9 * l * N + i, a6 = 6 * l * N + i;
                Real position0 = position[3 * l];
                Real spin0 = spin[3 * l];
                Real velocity0 = velocity[3 * l];
                Real relVel0 = relVel[3 * l];
                Real water0 = water[3 * l];
                Real wacc0 = wacc[3 * l];
                Real position1 = position[3 * l + 1];
                Real spin1 = spin[3 * l + 1];
                Real velocity1 = velocity[3 * l + 1];
                Real relVel1 = relVel[3 * l + 1];
                Real water1 = water[3 * l + 1];
                Real wacc1 = wacc[3 * l + 1];
                Real position2 = position[3 * l + 2];
                Real spin2 = spin[3 * l + 2];
                Real velocity2 = velocity[3 * l + 2];
                Real relVel2 = relVel[3 * l + 2];
                Real water2 = water[3 * l + 2];
                Real wacc2 = wacc[3 * l + 2];
                Real ballRot0 = ballRot[4 * l];
                Real rotation0 = rotation[4 * l];
                Real ballRot1 = ballRot[4 * l + 1];
                Real rotation1 = rotation[4 * l + 1];
                Real ballRot2 = ballRot[4 * l + 2];
                Real rotation2 = rotation[4 * l + 2];
                Real ballRot3 = ballRot[4 * l + 3];
                Real rotation3 = rotation[4 * l + 3];
                Real rotationMatrix0 = rotationMatrix[9 * l];
                Real sang0 = sang[9 * l];
                Real slin0 = slin[9 * l];
                Real rotationMatrix1 = rotationMatrix[9 * l + 1];
                Real sang1 = sang[9 * l + 1];
                Real slin1 = slin[9 * l + 1];
                Real rotationMatrix2 = rotationMatrix[9 * l + 2];
                Real sang2 = sang[9 * l + 2];
                Real slin2 = slin[9 * l + 2];
                Real rotationMatrix3 = rotationMatrix[9 * l + 3];
                Real sang3 = sang[9 * l + 3];
                Real slin3 = slin[9 * l + 3];
                Real rotationMatrix4 = rotationMatrix[9 * l + 4];
                Real sang4 = sang[9 * l + 4];
                Real slin4 = slin[9 * l + 4];
                Real rotationMatrix5 = rotationMatrix[9 * l + 5];
                Real sang5 = sang[9 * l + 5];
                Real slin5 = slin[9 * l + 5];
                Real rotationMatrix6 = rotationMatrix[9 * l + 6];
                Real sang6 = sang[9 * l + 6];
                Real slin6 = slin[9 * l + 6];
                Real rotationMatrix7 = rotationMatrix[9 * l + 7];
                Real sang7 = sang[9 * l + 7];
                Real slin7 = slin[9 * l + 7];
                Real rotationMatrix8 = rotationMatrix[9 * l + 8];
                Real sang8 = sang[9 * l + 8];
                Real slin8 = slin[9 * l + 8];
                Real cbias0 = cbias[6 * l];
                Real fext0 = fext[6 * l];
                Real cbias1 = cbias[6 * l + 1];
                Real fext1 = fext[6 * l + 1];
                Real cbias2 = cbias[6 * l + 2];
                Real fext2 = fext[6 * l + 2];
                Real cbias3 = cbias[6 * l + 3];
                Real fext3 = fext[6 * l + 3];
                Real cbias4 = cbias[6 * l + 4];
                Real fext4 = fext[6 * l + 4];
                Real cbias5 = cbias[6 * l + 5];
                Real fext5 = fext[6 * l + 5];

                s.Pos[a3] = position0;
                s.Spin[a3] = spin0;
                s.Vel[a3] = velocity0;
                s.RelVel[a3] = relVel0;
                s.Water[a3] = water0;
                s.WaterAcc[a3] = wacc0;
                s.Pos[a3 + N] = position1;
                s.Spin[a3 + N] = spin1;
                s.Vel[a3 + N] = velocity1;
                s.RelVel[a3 + N] = relVel1;
                s.Water[a3 + N] = water1;
                s.WaterAcc[a3 + N] = wacc1;
                s.Pos[a3 + 2 * N] = position2;
                s.Spin[a3 + 2 * N] = spin2;
                s.Vel[a3 + 2 * N] = velocity2;
                s.RelVel[a3 + 2 * N] = relVel2;
                s.Water[a3 + 2 * N] = water2;
                s.WaterAcc[a3 + 2 * N] = wacc2;
                s.BallRot[a4] = ballRot0;
                s.Rot[a4] = rotation0;
                s.BallRot[a4 + N] = ballRot1;
                s.Rot[a4 + N] = rotation1;
                s.BallRot[a4 + 2 * N] = ballRot2;
                s.Rot[a4 + 2 * N] = rotation2;
                s.BallRot[a4 + 3 * N] = ballRot3;
                s.Rot[a4 + 3 * N] = rotation3;
                s.RotM[a9] = rotationMatrix0;
                s.Sang[a9] = sang0;
                s.Slin[a9] = slin0;
                s.RotM[a9 + N] = rotationMatrix1;
                s.Sang[a9 + N] = sang1;
                s.Slin[a9 + N] = slin1;
                s.RotM[a9 + 2 * N] = rotationMatrix2;
                s.Sang[a9 + 2 * N] = sang2;
                s.Slin[a9 + 2 * N] = slin2;
                s.RotM[a9 + 3 * N] = rotationMatrix3;
                s.Sang[a9 + 3 * N] = sang3;
                s.Slin[a9 + 3 * N] = slin3;
                s.RotM[a9 + 4 * N] = rotationMatrix4;
                s.Sang[a9 + 4 * N] = sang4;
                s.Slin[a9 + 4 * N] = slin4;
                s.RotM[a9 + 5 * N] = rotationMatrix5;
                s.Sang[a9 + 5 * N] = sang5;
                s.Slin[a9 + 5 * N] = slin5;
                s.RotM[a9 + 6 * N] = rotationMatrix6;
                s.Sang[a9 + 6 * N] = sang6;
                s.Slin[a9 + 6 * N] = slin6;
                s.RotM[a9 + 7 * N] = rotationMatrix7;
                s.Sang[a9 + 7 * N] = sang7;
                s.Slin[a9 + 7 * N] = slin7;
                s.RotM[a9 + 8 * N] = rotationMatrix8;
                s.Sang[a9 + 8 * N] = sang8;
                s.Slin[a9 + 8 * N] = slin8;
                s.Cbias[a6] = cbias0;
                s.Fext[a6] = fext0;
                s.Cbias[a6 + N] = cbias1;
                s.Fext[a6 + N] = fext1;
                s.Cbias[a6 + 2 * N] = cbias2;
                s.Fext[a6 + 2 * N] = fext2;
                s.Cbias[a6 + 3 * N] = cbias3;
                s.Fext[a6 + 3 * N] = fext3;
                s.Cbias[a6 + 4 * N] = cbias4;
                s.Fext[a6 + 4 * N] = fext4;
                s.Cbias[a6 + 5 * N] = cbias5;
                s.Fext[a6 + 5 * N] = fext5;
            }
#if GPU_PHASE_PROBE
            PhaseMark(gl, cfg, g, 9, ref clk);
#endif
        }

#if GPU_PHASE_PROBE
        // The phase probe, compiled only into a build with GPU_PHASE_PROBE: the SM's cycle counter
        // read between the body phase's parts, each part's cycles written to the body's slot. The
        // parts: load, water, senses and brain, drive, fluid, contacts, the solve with the joint
        // torques and the integration, poses to the throw trace, the finiteness check and the
        // spheres, store. A lane in a diverged warp waits for the others, so a part's cycles are
        // its warp's.
        public const int Phases = 10;

        private static void PhaseMark(SGlob gl, SCfg cfg, int g, int k, ref long prev)
        {
            long now = 0;
            if (CudaAsm.IsSupported) CudaAsm.Emit("mov.u64 %0, %%clock64;", out now);
            gl.Phase[k * cfg.GN + g] = now - prev;
            prev = now;
        }

        // The same counter in the water and fluid passes, a link a thread, each part's cycles
        // written to the link's row (LinkStride a slot). The water's parts: the read of the
        // link's position, the velocity, the acceleration. The fluid's: the read with the link's
        // local velocity and spin, the panels, the limiter and the other terms, the store.
        public const int WaterParts = 3;
        public const int LinkParts = 7;

        private static void LinkMark(SGlob gl, SCfg cfg, int row, int k, ref long prev)
        {
            long now = 0;
            if (CudaAsm.IsSupported) CudaAsm.Emit("mov.u64 %0, %%clock64;", out now);
            gl.LinkPhase[k * cfg.LinkRows + row] = now - prev;
            prev = now;
        }
#endif

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
                ps.LEx[row + l] = cs.LEx[row + l]; ps.LEy[row + l] = cs.LEy[row + l];
                ps.LEz[row + l] = cs.LEz[row + l];

                if (cfg.TrueShape != 0)
                {
                    ps.LRot0[row + l] = cs.LRot0[row + l]; ps.LRot1[row + l] = cs.LRot1[row + l];
                    ps.LRot2[row + l] = cs.LRot2[row + l]; ps.LRot3[row + l] = cs.LRot3[row + l];
                    ps.LRot4[row + l] = cs.LRot4[row + l]; ps.LRot5[row + l] = cs.LRot5[row + l];
                    ps.LRot6[row + l] = cs.LRot6[row + l]; ps.LRot7[row + l] = cs.LRot7[row + l];
                    ps.LRot8[row + l] = cs.LRot8[row + l];
                }

                if (cfg.AtPoint != 0)
                {
                    ps.LSp0[row + l] = cs.LSp0[row + l]; ps.LSp1[row + l] = cs.LSp1[row + l];
                    ps.LSp2[row + l] = cs.LSp2[row + l];
                }
            }
        }

        // ============================================================== the water pass

        /// <summary>
        /// Water.Sample for every link of one class's bodies, before the class's step: a thread a
        /// link, the threads of one link index side by side so that neighbours read and write
        /// neighbouring words. It reads the positions the last step stored and writes the arrays
        /// the step's load reads, with the calls the step made itself, so the numbers do not move.
        /// Launched only when the water moves.
        /// </summary>
        public static void WaterLinks(Index1D index, STopo t, SState s, SGlob gl, SWorld w, SCfg cfg)
        {
            int used = cfg.ClassUsed;
            int l = index.X / used;
            int i = index.X - l * used;
            if (l >= cfg.ClassLinks) return;

            int g = t.GSlot[i];
            if (g < 0 || gl.Alive[g] == 0 || l >= t.Links[i]) return;

            int N = cfg.N;
            int a = 3 * l * N + i;
            int ib = cfg.InstBase;
#if GPU_PHASE_PROBE
            int row = g * LinkStride + l;
            long clk = 0;
            if (CudaAsm.IsSupported) CudaAsm.Emit("mov.u64 %0, %%clock64;", out clk);
#endif

            float x = (float)s.Pos[a];
            float y = (float)s.Pos[a + N];
            float z = (float)s.Pos[a + 2 * N];
#if GPU_PHASE_PROBE
            LinkMark(gl, cfg, row, 0, ref clk);
#endif

            float wx, wy, wz;
            VelocityAt(x, y, z, w, ib, cfg, out wx, out wy, out wz);

            // D239, as Water.Store on the host: the world's metres per world second over k, in the
            // arithmetic of the stored column. Unpaced, the sample is stored as it was.
            if (cfg.Paced == 0)
            {
                s.Water[a] = wx;
                s.Water[a + N] = wy;
                s.Water[a + 2 * N] = wz;
            }
            else
            {
                Real pace = cfg.Pace;
                s.Water[a] = (Real)wx / pace;
                s.Water[a + N] = (Real)wy / pace;
                s.Water[a + 2 * N] = (Real)wz / pace;
            }
#if GPU_PHASE_PROBE
            LinkMark(gl, cfg, row, 1, ref clk);
#endif

            if (cfg.Accelerating == 0)
            {
#if GPU_PHASE_PROBE
                LinkMark(gl, cfg, row, 2, ref clk);
#endif
                return;
            }

            float ax, ay, az;
            AccelerationAt(x, y, z, w, ib, cfg, out ax, out ay, out az);

            // The acceleration over k squared, the same way.
            if (cfg.Paced == 0)
            {
                s.WaterAcc[a] = ax;
                s.WaterAcc[a + N] = ay;
                s.WaterAcc[a + 2 * N] = az;
            }
            else
            {
                Real paceSquared = cfg.PaceSquared;
                s.WaterAcc[a] = (Real)ax / paceSquared;
                s.WaterAcc[a + N] = (Real)ay / paceSquared;
                s.WaterAcc[a + 2 * N] = (Real)az / paceSquared;
            }
#if GPU_PHASE_PROBE
            LinkMark(gl, cfg, row, 2, ref clk);
#endif
        }

        /// <summary>
        /// CurrentField.VelocityAt(x, y, z, t) in a tank with the vent off: StreamsAt, or with the
        /// reefs ReefStreamsAt (the curl of g·A: g·u + ∇g × A, exact zeros inside the rock).
        /// </summary>
        private static void VelocityAt(float x, float y, float z, SWorld w, int ib, SCfg cfg,
                                       out float ox, out float oy, out float oz)
        {
            if (cfg.Speed <= 0f) { ox = 0f; oy = 0f; oz = 0f; return; }

            // CurrentField.OceanAt: a place folded into the box first.
            if (cfg.Ocean != 0) { x = OceanFold(x, cfg.OceanL); z = OceanFold(z, cfg.OceanW); }

            // The rock's fade: the coral's hulls (CurrentField.CoralOceanAt and ReefStreamsAt over CoralRock.Fade), or the reefs'.
            SD f = default(SD);
            bool near = false;
            if (cfg.CoralCount != 0) near = CoralFade(x, y, z, w, cfg, out f);
            else if (cfg.ReefCount != 0) near = ReefFade(x, y, z, w, cfg, out f);

            if (!near)
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
                FlatUnit(x, y, z, cfg.Overturning, w, ib, cfg, out ux, out uy, out uz);
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
                FlatPotentialUnit(x, y, z, cfg.Overturning, w, ib, cfg, out ax, out ay, out az);
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
            FlatUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ux, out uy, out uz);

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

                FlatPotentialUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ax, out ay, out az);

                Real f = m.Fade;
                ox = (float)(f * (ax + m.A * ay));
                oy = (float)(f * (m.C * ay));
                oz = (float)(f * (az + m.B * ay));
                return;
            }

            FlatPotentialUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ax, out ay, out az);

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
            FlatUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ux, out uy, out uz);

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
            FlatPotentialUnit(x, m.MappedY, z, cfg.Overturning, w, ib, cfg, out ax, out ay, out az);

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
        // ------------------------------------------------------------------ D204's open ocean

        /// <summary>CurrentField.WrapAxis: a coordinate folded into [0, extent), in single as the processor's.</summary>
        private static float OceanFold(float v, float extent)
        {
            if (v >= 0f && v < extent) return v;
            float folded = v - extent * System.MathF.Floor(v / extent);
            if (folded >= extent || folded < 0f) folded = 0f;
            return folded;
        }

        /// <summary>CurrentField.FlatUnit: the ocean's flat field in a box, the streams' in a tank.</summary>
        private static void FlatUnit(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg,
                                     out float ox, out float oy, out float oz)
        {
            if (cfg.Ocean != 0) { OceanUnit(x, y, z, w, ib, cfg, out ox, out oy, out oz); return; }
            StreamsUnit(x, y, z, overturning, w, ib, cfg, out ox, out oy, out oz);
        }

        /// <summary>CurrentField.FlatPotentialUnit.</summary>
        private static void FlatPotentialUnit(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg,
                                              out float ox, out float oy, out float oz)
        {
            if (cfg.Ocean != 0) { OceanPotentialUnit(x, y, z, w, ib, cfg, out ox, out oy, out oz); return; }
            StreamsPotentialUnit(x, y, z, overturning, w, ib, cfg, out ox, out oy, out oz);
        }

        /// <summary>CurrentField.FlatUnitWithGradient.</summary>
        private static SG FlatUnitWithGradient(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg) =>
            cfg.Ocean != 0
                ? OceanUnitWithGradient(x, y, z, w, ib, cfg)
                : StreamsUnitWithGradient(x, y, z, overturning, w, ib, cfg);

        /// <summary>CurrentField.FlatPotentialUnitWithGradient.</summary>
        private static SG FlatPotentialUnitWithGradient(Real x, Real y, Real z, Real overturning, SWorld w, int ib, SCfg cfg) =>
            cfg.Ocean != 0
                ? OceanPotentialUnitWithGradient(x, y, z, w, ib, cfg)
                : StreamsPotentialUnitWithGradient(x, y, z, overturning, w, ib, cfg);

        /// <summary>
        /// CurrentField.OceanLayersAt: the three layers' shapes sin((q - 1/2)πη) at a flat height and
        /// their slopes, by the recurrence over the odd multiples of θ = πη/2.
        /// </summary>
        private static void OceanLayersAt(Real y, SCfg cfg,
                                          out Real s1, out Real s2, out Real s3, out Real d1, out Real d2, out Real d3)
        {
            Real depth = cfg.CurDepth;

            if (y > 0) y = 0;
            else if (y < -depth) y = -depth;

            Real theta = (Real)0.5 * (Real)Math.PI * ((Real)1 + y / depth);
            Real sin = SinR(theta);
            Real cos = CosR(theta);
            Real twice = (Real)2 * ((Real)1 - (Real)2 * sin * sin);

            s1 = sin;
            s2 = twice * s1 + sin;
            s3 = twice * s2 - s1;

            Real c1 = cos;
            Real c2 = twice * c1 - cos;
            Real c3 = twice * c2 - c1;

            Real unit = (Real)0.5 * (Real)Math.PI / depth;

            d1 = unit * c1;
            d2 = (Real)3 * unit * c2;
            d3 = (Real)5 * unit * c3;
        }

        /// <summary>CurrentField.OceanUnit: the flat ocean at unit speed and scale, at a folded place (A).</summary>
        private static void OceanUnit(Real x, Real y, Real z, SWorld w, int ib, SCfg cfg,
                                      out float ox, out float oy, out float oz)
        {
            Real s1, s2, s3, d1, d2, d3;
            OceanLayersAt(y, cfg, out s1, out s2, out s3, out d1, out d2, out d3);

            Real ux = 0, uz = 0;

            for (int v = 0, k = 0; v < OceanVectors; v++, k += OceanLayers)
            {
                Real alpha = w.Inst[ib + OCos + k] * s1 + w.Inst[ib + OCos + k + 1] * s2 + w.Inst[ib + OCos + k + 2] * s3;
                Real beta = w.Inst[ib + OSin + k] * s1 + w.Inst[ib + OSin + k + 1] * s2 + w.Inst[ib + OSin + k + 2] * s3;

                Real kx = w.WR[cfg.OceanKAt + v], kz = w.WR[cfg.OceanKAt + OceanVectors + v];
                Real chi = kx * x + kz * z;
                Real sinChi = SinR(chi), cosChi = CosR(chi);

                Real q = sinChi * alpha + cosChi * beta;

                ux += kz * q;
                uz -= kx * q;
            }

            ox = (float)ux; oy = 0f; oz = (float)uz;
        }

        /// <summary>CurrentField.OceanPotentialUnit: (0, A_y, 0) at unit speed and scale (A).</summary>
        private static void OceanPotentialUnit(Real x, Real y, Real z, SWorld w, int ib, SCfg cfg,
                                               out float ox, out float oy, out float oz)
        {
            Real s1, s2, s3, d1, d2, d3;
            OceanLayersAt(y, cfg, out s1, out s2, out s3, out d1, out d2, out d3);

            Real ay = 0;

            for (int v = 0, k = 0; v < OceanVectors; v++, k += OceanLayers)
            {
                Real alpha = w.Inst[ib + OCos + k] * s1 + w.Inst[ib + OCos + k + 1] * s2 + w.Inst[ib + OCos + k + 2] * s3;
                Real beta = w.Inst[ib + OSin + k] * s1 + w.Inst[ib + OSin + k + 1] * s2 + w.Inst[ib + OSin + k + 2] * s3;

                Real chi = w.WR[cfg.OceanKAt + v] * x + w.WR[cfg.OceanKAt + OceanVectors + v] * z;

                ay += CosR(chi) * alpha - SinR(chi) * beta;
            }

            ox = 0f; oy = (float)ay; oz = 0f;
        }

        /// <summary>CurrentField.OceanUnitWithGradient: the field, its clock derivative and its Jacobian (B).</summary>
        private static SG OceanUnitWithGradient(Real x, Real y, Real z, SWorld w, int ib, SCfg cfg)
        {
            Real s1, s2, s3, d1, d2, d3;
            OceanLayersAt(y, cfg, out s1, out s2, out s3, out d1, out d2, out d3);

            var g = default(SG);

            for (int v = 0, k = 0; v < OceanVectors; v++, k += OceanLayers)
            {
                Real c0 = w.Inst[ib + OBCos + k], c1 = w.Inst[ib + OBCos + k + 1], c2 = w.Inst[ib + OBCos + k + 2];
                Real n0 = w.Inst[ib + OBSin + k], n1 = w.Inst[ib + OBSin + k + 1], n2 = w.Inst[ib + OBSin + k + 2];
                Real r0 = w.Inst[ib + OBCosRate + k], r1 = w.Inst[ib + OBCosRate + k + 1], r2 = w.Inst[ib + OBCosRate + k + 2];
                Real m0 = w.Inst[ib + OBSinRate + k], m1 = w.Inst[ib + OBSinRate + k + 1], m2 = w.Inst[ib + OBSinRate + k + 2];

                Real alpha = c0 * s1 + c1 * s2 + c2 * s3;
                Real beta = n0 * s1 + n1 * s2 + n2 * s3;
                Real alphaY = c0 * d1 + c1 * d2 + c2 * d3;
                Real betaY = n0 * d1 + n1 * d2 + n2 * d3;
                Real alphaT = r0 * s1 + r1 * s2 + r2 * s3;
                Real betaT = m0 * s1 + m1 * s2 + m2 * s3;

                Real kx = w.WR[cfg.OceanKAt + v], kz = w.WR[cfg.OceanKAt + OceanVectors + v];
                Real chi = kx * x + kz * z;
                Real sinChi = SinR(chi), cosChi = CosR(chi);

                Real p = cosChi * alpha - sinChi * beta;
                Real q = sinChi * alpha + cosChi * beta;
                Real qy = sinChi * alphaY + cosChi * betaY;
                Real qt = sinChi * alphaT + cosChi * betaT;

                g.Vx += kz * q;
                g.Vz -= kx * q;

                g.Tx += kz * qt;
                g.Tz -= kx * qt;

                g.Xx += kz * kx * p;
                g.Xy += kz * qy;
                g.Xz += kz * kz * p;

                g.Zx -= kx * kx * p;
                g.Zy -= kx * qy;
                g.Zz -= kx * kz * p;
            }

            return g;
        }

        /// <summary>CurrentField.OceanPotentialUnitWithGradient: the potential, its clock derivative and its Jacobian (B).</summary>
        private static SG OceanPotentialUnitWithGradient(Real x, Real y, Real z, SWorld w, int ib, SCfg cfg)
        {
            Real s1, s2, s3, d1, d2, d3;
            OceanLayersAt(y, cfg, out s1, out s2, out s3, out d1, out d2, out d3);

            var g = default(SG);

            for (int v = 0, k = 0; v < OceanVectors; v++, k += OceanLayers)
            {
                Real c0 = w.Inst[ib + OBCos + k], c1 = w.Inst[ib + OBCos + k + 1], c2 = w.Inst[ib + OBCos + k + 2];
                Real n0 = w.Inst[ib + OBSin + k], n1 = w.Inst[ib + OBSin + k + 1], n2 = w.Inst[ib + OBSin + k + 2];
                Real r0 = w.Inst[ib + OBCosRate + k], r1 = w.Inst[ib + OBCosRate + k + 1], r2 = w.Inst[ib + OBCosRate + k + 2];
                Real m0 = w.Inst[ib + OBSinRate + k], m1 = w.Inst[ib + OBSinRate + k + 1], m2 = w.Inst[ib + OBSinRate + k + 2];

                Real alpha = c0 * s1 + c1 * s2 + c2 * s3;
                Real beta = n0 * s1 + n1 * s2 + n2 * s3;
                Real alphaY = c0 * d1 + c1 * d2 + c2 * d3;
                Real betaY = n0 * d1 + n1 * d2 + n2 * d3;
                Real alphaT = r0 * s1 + r1 * s2 + r2 * s3;
                Real betaT = m0 * s1 + m1 * s2 + m2 * s3;

                Real kx = w.WR[cfg.OceanKAt + v], kz = w.WR[cfg.OceanKAt + OceanVectors + v];
                Real chi = kx * x + kz * z;
                Real sinChi = SinR(chi), cosChi = CosR(chi);

                Real p = cosChi * alpha - sinChi * beta;
                Real q = sinChi * alpha + cosChi * beta;

                g.Vy += p;
                g.Yx -= kx * q;
                g.Yy += cosChi * alphaY - sinChi * betaY;
                g.Yz -= kz * q;
                g.Ty += cosChi * alphaT - sinChi * betaT;
            }

            return g;
        }

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
            Real cosPhi = CosR(phi);
            Real sinPhi = SinR(phi);

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
            Real cosPhi = CosR(phi);
            Real sinPhi = SinR(phi);

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

            // CurrentField.OceanAccelerationAt: a place folded into the box first.
            if (cfg.Ocean != 0) { x = OceanFold(x, cfg.OceanL); z = OceanFold(z, cfg.OceanW); }

            SG g;
            SD f = default(SD);
            bool near = false;
            if (cfg.CoralCount != 0) near = CoralFade(x, y, z, w, cfg, out f);
            else if (cfg.ReefCount != 0) near = ReefFade(x, y, z, w, cfg, out f);

            if (near)
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
                ? FlatUnitWithGradient(x, y, z, cfg.Overturning, w, ib, cfg)
                : StreamsSlopedUnitWithGradient(x, y, z, cfg.Overturning, w, ib, cfg);

        /// <summary>CurrentField.ReefFadedGradient: u'' = g·u + G × A and its Jacobian.</summary>
        private static SG ReefFadedGradient(Real x, Real y, Real z, SWorld w, int ib, SCfg cfg, SD f)
        {
            SG u = StreamsGradientAt(x, y, z, w, ib, cfg);

            SG p = cfg.Sloped == 0
                ? FlatPotentialUnitWithGradient(x, y, z, cfg.Overturning, w, ib, cfg)
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

            SG f = FlatUnitWithGradient(x, y * c, z, overturning, w, ib, cfg);

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
            SG p = FlatPotentialUnitWithGradient(x, mappedY, z, overturning, w, ib, cfg);

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

            SG p = FlatPotentialUnitWithGradient(x, y * c, z, overturning, w, ib, cfg);

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
            Real cosPhi = CosR(phi);
            Real sinPhi = SinR(phi);

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
            Real cosPhi = CosR(phi);
            Real sinPhi = SinR(phi);

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
                if (chi > (Real)AngleGuard || chi < -(Real)AngleGuard) Atomic.Add(ref w.Guard[0], 1L);
                Real a = w.WR[o + 3];
                Real cos = CosR(chi);
                Real sin = SinR(chi);

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
                if (chi > (Real)AngleGuard || chi < -(Real)AngleGuard) Atomic.Add(ref w.Guard[0], 1L);
                Real a = w.WR[o + 3];
                Real cos = CosR(chi);
                Real sin = SinR(chi);

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

        /// <summary>The contact lattice's spacing (r59-pos64): a power of two, so a float holds every point.</summary>
        private const float LatticeMetres = 0.25f;

        /// <summary>
        /// BedHeightAndGradient for the contact in single (r59-pos64): the height at the lattice point nearest
        /// (x, z), and the rise from there to (x + ex, z + ez) in small arguments, with the gradient at the true
        /// point. The height is the same number for every point of the square, so a body creeping inside it
        /// meets a floor that moves smoothly with it.
        /// </summary>
        private static void BedHeightNear(Real x, Real z, Real ex, Real ez, SWorld w, SCfg cfg,
            out Real height, out Real rise, out Real gradientX, out Real gradientZ)
        {
            if (cfg.BedRelief == 0)
            {
                height = 0; rise = 0; gradientX = 0; gradientZ = 0;
                return;
            }

            Real x0 = System.Math.Floor(x * ((Real)1.0 / LatticeMetres) + (Real)0.5) * LatticeMetres;
            Real z0 = System.Math.Floor(z * ((Real)1.0 / LatticeMetres) + (Real)0.5) * LatticeMetres;
            Real ux = (x - x0) + ex;
            Real uz = (z - z0) + ez;

            Real dx = x0 - cfg.BedRadius;
            Real dz = z0 - cfg.BedRadius;

            Real h = cfg.TiltX * dx + cfg.TiltZ * dz - cfg.Offset;
            Real d = cfg.TiltX * ux + cfg.TiltZ * uz;
            Real gx = cfg.TiltX;
            Real gz = cfg.TiltZ;

            for (int m = 0; m < cfg.BedModes; m++)
            {
                int o = cfg.BedAt + 4 * m;
                Real kx = w.WR[o], kz = w.WR[o + 1];
                Real chi = kx * dx + kz * dz + w.WR[o + 2];
                if (chi > (Real)AngleGuard || chi < -(Real)AngleGuard) Atomic.Add(ref w.Guard[0], 1L);
                Real a = w.WR[o + 3];
                Real cos = CosR(chi);
                Real sin = SinR(chi);

                // sin and cos less one of the small step, by their series: the step is under a radian's tenth
                // or so on this lattice, and the terms kept leave less than a nanometre.
                Real t = kx * ux + kz * uz;
                Real t2 = t * t;
                Real sinT = t * ((Real)1.0 - t2 * ((Real)(1.0 / 6.0) - t2 * (Real)(1.0 / 120.0)));
                Real cosT1 = -t2 * ((Real)0.5 - t2 * ((Real)(1.0 / 24.0) - t2 * (Real)(1.0 / 720.0)));

                h += a * cos;
                d += a * (cos * cosT1 - sin * sinT);
                Real sinAt = sin + (sin * cosT1 + cos * sinT);
                gx -= a * kx * sinAt;
                gz -= a * kz * sinAt;
            }

            if (cfg.ShoreOn != 0 && h + d > cfg.ShoalHeight)
            {
                height = cfg.ShoalHeight;
                rise = 0;
                gradientX = 0;
                gradientZ = 0;
                return;
            }

            height = h;
            rise = d;
            gradientX = gx;
            gradientZ = gz;
        }

        /// <summary>
        /// The glass's overlap for the contact in single (r59-pos64): the radius at the lattice point nearest
        /// (x, z), rounded once for its square, its difference from the tank's taken first, and the change
        /// from there to (x + ex, z + ez) to second order.
        /// </summary>
        private static Real GlassOutsideNear(Real x, Real z, Real ex, Real ez, Real r, SCfg cfg)
        {
            Real x0 = System.Math.Floor(x * ((Real)1.0 / LatticeMetres) + (Real)0.5) * LatticeMetres;
            Real z0 = System.Math.Floor(z * ((Real)1.0 / LatticeMetres) + (Real)0.5) * LatticeMetres;
            Real ux = (x - x0) + ex;
            Real uz = (z - z0) + ez;

            Real px = x0 - cfg.AxisX;
            Real pz = z0 - cfg.AxisZ;
            Real rho = System.Math.Sqrt(px * px + pz * pz);
            if (!(rho > (Real)1e-3)) return System.Math.Sqrt((px + ux) * (px + ux) + (pz + uz) * (pz + uz)) + r - cfg.TankRadius;

            Real along = (px * ux + pz * uz) / rho;
            Real across2 = ux * ux + uz * uz - along * along;
            return (rho - cfg.TankRadius) + (along + across2 / ((Real)2.0 * rho)) + r;
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

            Real c2 = CosR((Real)2 * theta + w.WR[o + 8]), s2 = SinR((Real)2 * theta + w.WR[o + 8]);
            Real c3 = CosR((Real)3 * theta + w.WR[o + 9]), s3 = SinR((Real)3 * theta + w.WR[o + 9]);
            Real c4 = CosR((Real)4 * theta + w.WR[o + 10]), s4 = SinR((Real)4 * theta + w.WR[o + 10]);

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

        // ============================================================== the coral (CoralRock)
        //
        // A colony's sixteen numbers in SWorld.WR from cfg.CoralColonyAt: its box (min x, y, z, max x, y, z), the
        // allowance under its box's distance, its hull's centre (x, y, z), semi-axes (x, y, z) and least semi-axis,
        // its first piece and its piece count. A piece's nine from cfg.CoralPieceAt: its kind (0 a capsule, 1 a
        // plate), its ends A and B, its radius and its half thickness. The grid's cell starts (cells + 1) from
        // cfg.CoralGridAt and its colony lists from cfg.CoralListAt, and the fade's (CoralRock._fadeStart and
        // _fadeColonies) from cfg.CoralFadeGridAt and CoralFadeListAt, all as reals: every index is under 2^24.

        public const int CoralColonyStride = 16, CoralPieceStride = 9;

        /// <summary>CoralRock.Fold: a coordinate folded into [0, period).</summary>
        private static Real CoralFold(Real v, Real period)
        {
            if (v >= 0 && v < period) return v;
            Real folded = v - period * System.Math.Floor(v / period);
            return folded >= period || folded < 0 ? (Real)0 : folded;
        }

        /// <summary>CoralRock.ShortWay: the short way along a periodic side, d itself in a tank.</summary>
        private static Real CoralShortWay(Real d, Real period)
        {
            if (!(period > 0)) return d;
            Real half = (Real)0.5 * period;
            if (d > half) return d - period;
            if (d < -half) return d + period;
            return d;
        }

        /// <summary>CoralRock.FadeCell: the cell whose fade list a point reads, its folded place's in the square.</summary>
        private static int CoralFadeCell(Real x, Real z, SCfg cfg)
        {
            Real fx = cfg.CoralPeriodX > 0 ? CoralFold(x, cfg.CoralPeriodX) : x;
            Real fz = cfg.CoralPeriodZ > 0 ? CoralFold(z, cfg.CoralPeriodZ) : z;
            int i = (int)System.Math.Floor(fx / cfg.CoralCell), j = (int)System.Math.Floor(fz / cfg.CoralCell);
            if (i > cfg.CoralCellsX - 1) i = cfg.CoralCellsX - 1;
            if (i < 0) i = 0;
            if (j > cfg.CoralCellsZ - 1) j = cfg.CoralCellsZ - 1;
            if (j < 0) j = 0;
            return i * cfg.CoralCellsZ + j;
        }

        /// <summary>CoralRock.CellRange: the grid's cells under a span, clamped.</summary>
        private static void CoralCellRange(Real lo, Real hi, int cells, SCfg cfg, out int first, out int last)
        {
            int a = (int)System.Math.Floor(lo / cfg.CoralCell), b = (int)System.Math.Floor(hi / cfg.CoralCell);
            first = a < cells - 1 ? a : cells - 1;
            if (first < 0) first = 0;
            last = b < cells - 1 ? b : cells - 1;
            if (last < 0) last = 0;
        }

        /// <summary>CoralRock.ListedCells: the cells a colony is listed in, its box widened by its allowance.</summary>
        private static void CoralListed(int c, SWorld w, SCfg cfg, out int i0, out int i1, out int j0, out int j1)
        {
            int o = cfg.CoralColonyAt + c * CoralColonyStride;
            Real allowance = w.WR[o + 6];
            CoralCellRange(w.WR[o] - allowance, w.WR[o + 3] + allowance, cfg.CoralCellsX, cfg, out i0, out i1);
            CoralCellRange(w.WR[o + 2] - allowance, w.WR[o + 5] + allowance, cfg.CoralCellsZ, cfg, out j0, out j1);
        }

        /// <summary>CoralRock.FirstCell: whether (i, j) is the query window's first cell listing colony c.</summary>
        private static bool CoralFirstCell(int c, int i, int j, int i0, int j0, SWorld w, SCfg cfg)
        {
            int ci0, ci1, cj0, cj1;
            CoralListed(c, w, cfg, out ci0, out ci1, out cj0, out cj1);
            return i == (i0 > ci0 ? i0 : ci0) && j == (j0 > cj0 ? j0 : cj0);
        }

        /// <summary>CoralRock.LowerBound: a colony's box distance less its allowance.</summary>
        private static Real CoralLowerBound(int c, Real x, Real y, Real z, SWorld w, SCfg cfg)
        {
            int o = cfg.CoralColonyAt + c * CoralColonyStride;
            Real dx = MaxR(0, MaxR(w.WR[o] - x, x - w.WR[o + 3]));
            Real dy = MaxR(0, MaxR(w.WR[o + 1] - y, y - w.WR[o + 4]));
            Real dz = MaxR(0, MaxR(w.WR[o + 2] - z, z - w.WR[o + 5]));
            return System.Math.Sqrt(dx * dx + dy * dy + dz * dz) - w.WR[o + 6];
        }

        /// <summary>CoralRock.OfPiece: a capsule's or a plate's distance and gradient.</summary>
        private static SD CoralOfPiece(int piece, Real x, Real y, Real z, SWorld w, SCfg cfg)
        {
            int o = cfg.CoralPieceAt + piece * CoralPieceStride;
            Real ax = w.WR[o + 1], ay = w.WR[o + 2], az = w.WR[o + 3];
            var d = default(SD);

            if (w.WR[o] == 0)
            {
                Real bx = w.WR[o + 4] - ax, by = w.WR[o + 5] - ay, bz = w.WR[o + 6] - az;
                Real px = x - ax, py = y - ay, pz = z - az;
                Real bb = bx * bx + by * by + bz * bz;
                Real t = bb > 0 ? (px * bx + py * by + pz * bz) / bb : (Real)0;
                if (t < 0) t = 0;
                else if (t > 1) t = 1;

                Real qx = px - t * bx, qy = py - t * by, qz = pz - t * bz;
                Real length = System.Math.Sqrt(qx * qx + qy * qy + qz * qz);
                d.S = length - w.WR[o + 7];
                if (length > 0)
                {
                    d.Gx = qx / length;
                    d.Gy = qy / length;
                    d.Gz = qz / length;
                }
                else
                {
                    d.Gy = 1;
                }

                return d;
            }

            Real ex = x - ax, ey = y - ay, ez = z - az;
            Real rho = System.Math.Sqrt(ex * ex + ez * ez);
            Real past = rho - w.WR[o + 7];
            if (past > 0)
            {
                Real span = System.Math.Sqrt(past * past + ey * ey);
                d.S = span - w.WR[o + 8];
                d.Gx = past * ex / rho / span;
                d.Gy = ey / span;
                d.Gz = past * ez / rho / span;
                return d;
            }

            d.S = AbsR(ey) - w.WR[o + 8];
            d.Gy = ey >= 0 ? (Real)1 : (Real)(-1);
            return d;
        }

        /// <summary>CoralRock.SmoothUnion: the cubic smooth minimum's value and gradient.</summary>
        private static SD CoralUnion(SD a, SD b, Real k)
        {
            Real gap = a.S - b.S;
            Real spread = AbsR(gap);
            if (spread >= k) return gap <= 0 ? a : b;

            Real h = (k - spread) / k;
            Real wa = gap < 0 ? (Real)1 - (Real)0.5 * h * h : (Real)0.5 * h * h;
            Real wb = (Real)1 - wa;

            var d = default(SD);
            d.S = MinR(a.S, b.S) - k * h * h * h / (Real)6;
            d.Gx = wa * a.Gx + wb * b.Gx;
            d.Gy = wa * a.Gy + wb * b.Gy;
            d.Gz = wa * a.Gz + wb * b.Gz;
            return d;
        }

        /// <summary>CoralRock.OfColony: a colony's pieces by the smooth minimum, in order.</summary>
        private static SD CoralOfColony(int c, Real x, Real y, Real z, SWorld w, SCfg cfg)
        {
            int o = cfg.CoralColonyAt + c * CoralColonyStride;
            int first = (int)w.WR[o + 14], count = (int)w.WR[o + 15];
            SD d = CoralOfPiece(first, x, y, z, w, cfg);
            for (int k = 1; k < count; k++) d = CoralUnion(d, CoralOfPiece(first + k, x, y, z, w, cfg), cfg.CoralFillet);
            return d;
        }

        /// <summary>CoralRock.Query: the grid's query at a point in the footprint's frame, unfolded.</summary>
        private static Real CoralQuery(Real x, Real y, Real z, Real limit, SWorld w, SCfg cfg, out int nearest, out SD at)
        {
            Real reach = MaxR(limit, 0);
            int i0, i1, j0, j1;
            CoralCellRange(x - reach, x + reach, cfg.CoralCellsX, cfg, out i0, out i1);
            CoralCellRange(z - reach, z + reach, cfg.CoralCellsZ, cfg, out j0, out j1);

            nearest = -1;
            at = default(SD);

            int first = -1;
            Real firstBound = Real.PositiveInfinity;

            for (int i = i0; i <= i1; i++)
            {
                for (int j = j0; j <= j1; j++)
                {
                    int cell = i * cfg.CoralCellsZ + j;
                    int end = (int)w.WR[cfg.CoralGridAt + cell + 1];
                    for (int e = (int)w.WR[cfg.CoralGridAt + cell]; e < end; e++)
                    {
                        int c = (int)w.WR[cfg.CoralListAt + e];
                        if (!CoralFirstCell(c, i, j, i0, j0, w, cfg)) continue;

                        Real bound = CoralLowerBound(c, x, y, z, w, cfg);
                        if (bound < firstBound || (bound == firstBound && c < first))
                        {
                            firstBound = bound;
                            first = c;
                        }
                    }
                }
            }

            if (first < 0 || firstBound >= limit) return first < 0 ? limit : firstBound;

            at = CoralOfColony(first, x, y, z, w, cfg);
            nearest = first;
            Real best = at.S;

            for (int i = i0; i <= i1; i++)
            {
                for (int j = j0; j <= j1; j++)
                {
                    int cell = i * cfg.CoralCellsZ + j;
                    int end = (int)w.WR[cfg.CoralGridAt + cell + 1];
                    for (int e = (int)w.WR[cfg.CoralGridAt + cell]; e < end; e++)
                    {
                        int c = (int)w.WR[cfg.CoralListAt + e];
                        if (c == first || !CoralFirstCell(c, i, j, i0, j0, w, cfg)) continue;

                        Real bound = CoralLowerBound(c, x, y, z, w, cfg);
                        if (bound > best || (bound == best && c > nearest)) continue;

                        SD d = CoralOfColony(c, x, y, z, w, cfg);
                        if (d.S < best || (d.S == best && c < nearest))
                        {
                            best = d.S;
                            nearest = c;
                            at = d;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// CoralRock.SignedDistance with a finite limit: the grid's query, and in the square the folded point and
        /// each image across a face within the limit (CoralRock.Periodic), the least kept, a tie to the earlier.
        /// </summary>
        private static Real CoralSignedDistance(Real x, Real y, Real z, Real limit, SWorld w, SCfg cfg, out SD at)
        {
            int nearest;
            if (!(cfg.CoralPeriodX > 0)) return CoralQuery(x, y, z, limit, w, cfg, out nearest, out at);

            Real px = cfg.CoralPeriodX, pz = cfg.CoralPeriodZ;
            Real fx = CoralFold(x, px), fz = CoralFold(z, pz);
            Real best = CoralQuery(fx, y, fz, limit, w, cfg, out nearest, out at);

            Real reach = MaxR(limit, 0);
            Real ox0 = fx < reach ? px : (Real)0, ox1 = fx > px - reach ? -px : (Real)0;
            Real oz0 = fz < reach ? pz : (Real)0, oz1 = fz > pz - reach ? -pz : (Real)0;

            for (int a = 0; a < 3; a++)
            {
                Real ox = a == 0 ? (Real)0 : a == 1 ? ox0 : ox1;
                if (a > 0 && ox == 0) continue;

                for (int b = 0; b < 3; b++)
                {
                    Real oz = b == 0 ? (Real)0 : b == 1 ? oz0 : oz1;
                    if (b > 0 && oz == 0) continue;
                    if (a == 0 && b == 0) continue;

                    int n;
                    SD g;
                    Real d = CoralQuery(fx + ox, y, fz + oz, limit, w, cfg, out n, out g);
                    if (n >= 0 && (nearest < 0 || d < best))
                    {
                        best = d;
                        nearest = n;
                        at = g;
                    }
                    else if (nearest < 0 && d < best)
                    {
                        best = d;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// CoralRock.HullDistance: a hull's near-Euclidean distance k0(k0 − 1)/k1, +∞ where the cull rules its
        /// fade out, negative inside; with k0 and p/a, the short way round in the square.
        /// </summary>
        private static Real CoralHullDistance(int c, Real x, Real y, Real z, SWorld w, SCfg cfg,
                                              out Real k0, out Real mx, out Real my, out Real mz)
        {
            int o = cfg.CoralColonyAt + c * CoralColonyStride;
            Real edge = (Real)1 + cfg.CoralFade / w.WR[o + 13];
            mx = CoralShortWay(x - w.WR[o + 7], cfg.CoralPeriodX) / w.WR[o + 10];
            my = (y - w.WR[o + 8]) / w.WR[o + 11];
            mz = CoralShortWay(z - w.WR[o + 9], cfg.CoralPeriodZ) / w.WR[o + 12];
            Real q2 = mx * mx + my * my + mz * mz;
            k0 = System.Math.Sqrt(q2);
            if (q2 >= edge * edge) return Real.PositiveInfinity;
            if (k0 <= 1) return k0 - (Real)1;

            Real wx = mx / w.WR[o + 10], wy = my / w.WR[o + 11], wz = mz / w.WR[o + 12];
            Real k1 = System.Math.Sqrt(wx * wx + wy * wy + wz * wz);
            return k0 * (k0 - (Real)1) / k1;
        }

        /// <summary>One entry of the hull distance's Hessian (CoralRock.Fade's Entry).</summary>
        private static Real CoralHessianEntry(
            Real diag0, Real diag1, Real g0i, Real g0j, Real g1i, Real g1j, Real dai, Real daj,
            Real k0, Real k1, Real a, Real b, Real k1Squared, Real k1Cubed)
        {
            Real h0 = diag0 / k0 - g0i * g0j / k0;
            Real h1 = diag1 / k1 - g1i * g1j / k1;
            Real ha = (Real)2 * g0i * g0j + b * h0;
            return ha / k1 - (dai * g1j + g1i * daj) / k1Squared - a * h1 / k1Squared + (Real)2 * a * g1i * g1j / k1Cubed;
        }

        /// <summary>
        /// CoralRock.Fade: the product over the colonies of each hull's quintic lifted onto the floor, with its
        /// gradient and Hessian; false, the fade 1, where no hull's fade reaches.
        /// </summary>
        private static bool CoralFade(Real x, Real y, Real z, SWorld w, SCfg cfg, out SD fade)
        {
            fade = default(SD);
            fade.S = 1;
            bool touched = false;
            Real floor = cfg.CoralFloor, lift = (Real)1 - cfg.CoralFloor, fadeMetres = cfg.CoralFade;

            int cell = CoralFadeCell(x, z, cfg);
            int end = (int)w.WR[cfg.CoralFadeGridAt + cell + 1];
            for (int e = (int)w.WR[cfg.CoralFadeGridAt + cell]; e < end; e++)
            {
                int c = (int)w.WR[cfg.CoralFadeListAt + e];
                Real k0, mx, my, mz;
                Real s = CoralHullDistance(c, x, y, z, w, cfg, out k0, out mx, out my, out mz);
                Real xi = s / fadeMetres;
                if (xi >= 1) continue;

                touched = true;

                var f = default(SD);
                if (xi <= 0)
                {
                    f.S = floor;
                }
                else
                {
                    int o = cfg.CoralColonyAt + c * CoralColonyStride;
                    Real ax = w.WR[o + 10], ay = w.WR[o + 11], az = w.WR[o + 12];
                    Real ax2 = ax * ax, ay2 = ay * ay, az2 = az * az;
                    Real wx = mx / ax, wy = my / ay, wz = mz / az;
                    Real k1 = System.Math.Sqrt(wx * wx + wy * wy + wz * wz);
                    Real a = k0 * (k0 - (Real)1);
                    Real b = (Real)2 * k0 - (Real)1;

                    Real g0x = wx / k0, g0y = wy / k0, g0z = wz / k0;
                    Real g1x = wx / ax2 / k1, g1y = wy / ay2 / k1, g1z = wz / az2 / k1;
                    Real dax = b * g0x, day = b * g0y, daz = b * g0z;

                    Real k1Squared = k1 * k1, k1Cubed = k1Squared * k1;
                    Real sx = dax / k1 - a * g1x / k1Squared;
                    Real sy = day / k1 - a * g1y / k1Squared;
                    Real sz = daz / k1 - a * g1z / k1Squared;

                    Real hxx = CoralHessianEntry((Real)1 / ax2, (Real)1 / (ax2 * ax2), g0x, g0x, g1x, g1x, dax, dax, k0, k1, a, b, k1Squared, k1Cubed);
                    Real hyy = CoralHessianEntry((Real)1 / ay2, (Real)1 / (ay2 * ay2), g0y, g0y, g1y, g1y, day, day, k0, k1, a, b, k1Squared, k1Cubed);
                    Real hzz = CoralHessianEntry((Real)1 / az2, (Real)1 / (az2 * az2), g0z, g0z, g1z, g1z, daz, daz, k0, k1, a, b, k1Squared, k1Cubed);
                    Real hxy = CoralHessianEntry(0, 0, g0x, g0y, g1x, g1y, dax, day, k0, k1, a, b, k1Squared, k1Cubed);
                    Real hxz = CoralHessianEntry(0, 0, g0x, g0z, g1x, g1z, dax, daz, k0, k1, a, b, k1Squared, k1Cubed);
                    Real hyz = CoralHessianEntry(0, 0, g0y, g0z, g1y, g1z, day, daz, k0, k1, a, b, k1Squared, k1Cubed);

                    Real rest = (Real)1 - xi;
                    Real q = xi * xi * xi * ((Real)10 + xi * ((Real)(-15) + (Real)6 * xi));
                    Real q1 = lift * (Real)30 * xi * xi * rest * rest / fadeMetres;
                    Real qq = lift * (Real)60 * xi * rest * ((Real)1 - (Real)2 * xi) / (fadeMetres * fadeMetres);

                    f.S = floor + lift * q;
                    f.Gx = q1 * sx; f.Gy = q1 * sy; f.Gz = q1 * sz;
                    f.Hxx = qq * sx * sx + q1 * hxx;
                    f.Hxy = qq * sx * sy + q1 * hxy;
                    f.Hxz = qq * sx * sz + q1 * hxz;
                    f.Hyy = qq * sy * sy + q1 * hyy;
                    f.Hyz = qq * sy * sz + q1 * hyz;
                    f.Hzz = qq * sz * sz + q1 * hzz;
                }

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
            Real[] relVel, Real[] q, Real[] qd, Real[] limitHi, SConst b, SBrain br, SWorld w, SCfg cfg,
            bool readsDepth, bool readsUp, bool readsJoint, bool readsFlow, bool readsChemical, bool readsScent,
            bool readsEnergy, bool leaves, bool readsLeafDir,
            float[] sDepth, float[] sUp, float[] sChem, float[] sScent, float[] sFlow, float[] sAngle, float[] sRate, float[] sDrive,
            float[] sLeaf, ref float energy, ref bool lost)
        {
            if (readsEnergy) energy = Squash(br.Reserve[i], cfg.EnergyScale, cfg.EnergyHalfMode, cfg.EnergyHalfK);

            bool smells = readsChemical;

            if (!readsDepth && !readsUp && !readsJoint && !readsFlow && !smells && !readsScent && !leaves) return;

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
                    sScent[bl] = 0f;
                    sLeaf[4 * bl] = 0f;
                    sLeaf[4 * bl + 1] = 0f;
                    sLeaf[4 * bl + 2] = 0f;
                    sLeaf[4 * bl + 3] = 0f;

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

                if (cfg.Nose != 0)
                {
                    // D235, CreatureSenses.Sample in a nose world: no part smells at its centre. A nose link holds
                    // what the host's last Sniff left, copied as it stands (CreatureSenses.HeldSmell); every other
                    // link holds zero. No field is read, so the chemical sense's walk cannot lose the body here.
                    if (b.NoseFlag[bl * cfg.N + i] != 0)
                    {
                        int held = 6 * bl;
                        sChem[bl] = br.NoseHeld[held * cfg.N + i];
                        sScent[bl] = br.NoseHeld[(held + 1) * cfg.N + i];
                        sLeaf[4 * bl] = br.NoseHeld[(held + 2) * cfg.N + i];
                        sLeaf[4 * bl + 1] = br.NoseHeld[(held + 3) * cfg.N + i];
                        sLeaf[4 * bl + 2] = br.NoseHeld[(held + 4) * cfg.N + i];
                        sLeaf[4 * bl + 3] = br.NoseHeld[(held + 5) * cfg.N + i];
                    }
                    else
                    {
                        sChem[bl] = 0f;
                        sScent[bl] = 0f;
                        sLeaf[4 * bl] = 0f;
                        sLeaf[4 * bl + 1] = 0f;
                        sLeaf[4 * bl + 2] = 0f;
                        sLeaf[4 * bl + 3] = 0f;
                    }
                }
                else
                {
                    if (smells)
                    {
                        float density = EdibleDensityAt((float)px, (float)py, (float)pz, w, cfg, ref lost);
                        sChem[bl] = density > 0f ? density / (density + cfg.ChemHalf) : 0f;
                    }

                    if (readsScent)
                    {
                        float sc = ScentAt((float)px, (float)py, (float)pz, w, cfg);
                        sScent[bl] = sc > 0f ? sc / (sc + cfg.ScentHalf) : 0f;
                    }
                }

                // D234: a part that reflects reads the vectors in its own frame with the component along the
                // reflection's axis negated (CreatureSenses, Creature.ReflectFrameVector). 0 reflects nothing.
                int frameMirror = cfg.MirrorDrive != 0 ? b.MirrorMask[bl * cfg.N + i] : 0;

                if (leaves && cfg.Nose == 0) SampleLeaf(bl, (float)px, (float)py, (float)pz, rotationMatrix, readsLeafDir, frameMirror, sLeaf, w, cfg);

                if (readsFlow)
                {
                    Real m00 = rotationMatrix[9 * bl], m01 = rotationMatrix[9 * bl + 1], m02 = rotationMatrix[9 * bl + 2];
                    Real m10 = rotationMatrix[9 * bl + 3], m11 = rotationMatrix[9 * bl + 4], m12 = rotationMatrix[9 * bl + 5];
                    Real m20 = rotationMatrix[9 * bl + 6], m21 = rotationMatrix[9 * bl + 7], m22 = rotationMatrix[9 * bl + 8];
                    Real vx = relVel[3 * bl], vy = relVel[3 * bl + 1], vz = relVel[3 * bl + 2];

                    Real lx = m00 * vx + m10 * vy + m20 * vz;
                    Real ly = m01 * vx + m11 * vy + m21 * vz;
                    Real lz = m02 * vx + m12 * vy + m22 * vz;

                    if ((frameMirror & 8) != 0)
                    {
                        int axis = (frameMirror >> 4) & 3;
                        if (axis == 0) lx = -lx;
                        else if (axis == 1) ly = -ly;
                        else lz = -lz;
                    }

                    sFlow[3 * bl] = Clamp((float)(lx / flowScale));
                    sFlow[3 * bl + 1] = Clamp((float)(ly / flowScale));
                    sFlow[3 * bl + 2] = Clamp((float)(lz / flowScale));
                }

                if (!readsJoint) continue;

                int n = dofCount[bl];
                int offset = dofStart[bl];
                if (n == 0 || offset < 0) continue;

                // D234: CreatureSenses reads a reflected degree of freedom's angle and rate with the sign
                // reversed, against the reversed lower stop as its upper one. The mask is read only in a world
                // whose bodies reflect their drive; elsewhere the sign is 1 and the line is the old one.
                int mirror = cfg.MirrorDrive != 0 ? b.MirrorMask[bl * cfg.N + i] : 0;

                for (int d = 0; d < n; d++)
                {
                    int at = offset + d;
                    bool flipped = ((mirror >> d) & 1) != 0;
                    Real sign = flipped ? (Real)(-1) : (Real)1;
                    Real limit = AbsR(flipped ? -b.LimitLo[at * cfg.N + i] : limitHi[at]);

                    sAngle[at] = limit > (Real)1e-6
                        ? Clamp((float)(sign * q[at] / limit))
                        : 0f;

                    sRate[at] = Clamp((float)(sign * qd[at] / rateScale));
                }
            }
        }

        private static float Squash(float seconds, float scale, int halfMode, float halfK)
        {
            if (seconds != seconds) return 0f;
            if (seconds == float.PositiveInfinity) return 1f;
            if (seconds == float.NegativeInfinity) return -1f;

            // D245: S / (|S| + k), the host's CreatureSenses.Squash transcribed; the tanh path is untouched.
            if (halfMode != 0) return seconds / ((seconds < 0f ? -seconds : seconds) + halfK);

            return TanhF(seconds / scale);
        }

        private static float EdibleDensityAt(float x, float y, float z, SWorld w, SCfg cfg, ref bool lost)
        {
            int cell = cfg.FieldTankR > 0 ? TankCellAt(x, y, z, w, cfg) : BoxCellAt(x, y, z, w, cfg);
            if (cell < 0)
            {
                lost = true;
                return 0f;
            }

            Real stock = w.Stock[cell];
            int layer = cell / cfg.LayerStride;
            Real edible = layer >= cfg.LayerCount - cfg.RefugeLayers ? stock * cfg.RefugeFraction : stock;
            return (float)(edible / cfg.CellVolume);
        }

        /// <summary>
        /// The far bound of the chemical sense's walk, in cells past the field's outermost corner.
        /// Out to it the walk is GridField.TankCellAt's; beyond it the walk starts at it.
        /// </summary>
        public const int WalkMarginCells = 4096;

        /// <summary>
        /// GridField.TankCellAt, or −1 for a body the walk gives up on: the caller leaves that body
        /// unstepped, as the solver leaves a non-finite one, and the farm's glass guard kills it at
        /// its next check (card-audit-r57.md, "The chemical sense's walk can hang the card").
        /// </summary>
        /// <remarks>
        /// The CPU walks in from the body's distance to the axis, half a cell a step, which on the
        /// card would hang a thread: in single, <c>reach -= h</c> stops moving past about 1.7e7 m on
        /// a 2 m cell, and an infinite distance never moves. So the walk is the CPU's out to a far
        /// bound, the field's outermost corner plus <see cref="WalkMarginCells"/> cells, and starts
        /// at that bound for a body beyond it, which only a body far outside the glass can be. A
        /// distance that is not finite, and a walk longer than the bound allows, give −1.
        /// </remarks>
        internal static int TankCellAt(float x, float y, float z, SWorld w, SCfg cfg)
        {
            int iy = y >= 0f ? 0 : (int)(-y / cfg.CellMetres);
            if (iy >= cfg.Ny) iy = cfg.Ny - 1;
            if (iy < 0) iy = 0;

            Real dx = x - cfg.FieldTankR;
            Real dz = z - cfg.FieldTankR;
            Real r = SqrtR(dx * dx + dz * dz);

            // NaN, or an infinity from a cast or a square past the type's range. A square root is
            // never −∞.
            if (r != r || r == Real.PositiveInfinity) return -1;

            if (r > 0)
            {
                Real half = (Real)0.5 * cfg.CellMetres;
                Real ex = MaxR((Real)cfg.FieldTankR, (Real)cfg.Nx * cfg.CellMetres - cfg.FieldTankR);
                Real ez = MaxR((Real)cfg.FieldTankR, (Real)cfg.Nz * cfg.CellMetres - cfg.FieldTankR);
                Real far = SqrtR(ex * ex + ez * ez) + (Real)WalkMarginCells * cfg.CellMetres;
                int cap = (int)(far / half) + 64;

                int walked = 0;
                for (Real reach = r > far ? far : r; reach > 0; reach -= half)
                {
                    if (++walked > cap) return -1;

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

        /// <summary>
        /// GridField.CellAt in a box (D204): the column the point falls in, x and z taken round the
        /// seams, clamped up to its floor; a column with no water gives way to the nearest one that
        /// has some, ring by ring round the seams (GridField.BoxCellAt). −1 for a point that is not
        /// a number, which the caller leaves for the farm's guard, as the tank's walk does.
        /// </summary>
        internal static int BoxCellAt(float x, float y, float z, SWorld w, SCfg cfg)
        {
            float u = BoxFold(x, cfg.FieldL), v = BoxFold(z, cfg.FieldW);
            if (u != u || v != v) return -1;

            int iy = y >= 0f ? 0 : (int)(-y / cfg.CellMetres);
            if (iy >= cfg.Ny) iy = cfg.Ny - 1;
            if (iy < 0) iy = 0;

            int ix = (int)(u / cfg.CellMetres);
            if (ix >= cfg.Nx) ix = cfg.Nx - 1;
            if (ix < 0) ix = 0;

            int iz = (int)(v / cfg.CellMetres);
            if (iz >= cfg.Nz) iz = cfg.Nz - 1;
            if (iz < 0) iz = 0;

            int cell = InColumn(ix, iz, iy, w, cfg);
            if (cell >= 0) return cell;

            int reach = cfg.Nx > cfg.Nz ? cfg.Nx : cfg.Nz;
            for (int ring = 1; ring <= reach; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (dx != -ring && dx != ring && dz != -ring && dz != ring) continue;

                        int cx = ((ix + dx) % cfg.Nx + cfg.Nx) % cfg.Nx;
                        int cz = ((iz + dz) % cfg.Nz + cfg.Nz) % cfg.Nz;
                        cell = InColumn(cx, cz, iy, w, cfg);
                        if (cell >= 0) return cell;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// GridField.WrapAxis in float: a coordinate folded into [0, extent). NaN for a coordinate
        /// that is not a number or one so far out that the fold means nothing.
        /// </summary>
        private static float BoxFold(float v, float extent)
        {
            if (v >= 0f && v < extent) return v;
            float turns = v / extent;
            if (!(turns > -1e7f && turns < 1e7f)) return float.NaN;

            float folded = v - extent * (float)Floor(turns);
            if (folded >= extent || folded < 0f) folded = 0f;
            return folded;
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
            float[] sDepth, float[] sUp, float[] sChem, float[] sScent, float[] sFlow, float[] sAngle, float[] sRate, float[] sDrive, float energy, int rock, float[] sLeaf)
        {
            int N = cfg.N;

            double clock = br.Clock[i] + cfg.DtF;
            br.Clock[i] = clock;
            float time = (float)clock;

            int parity = br.Parity[i];
            int prev = parity * cfg.MaxN;
            int cur = (1 - parity) * cfg.MaxN;

            int units = t.CUnits[i];
            if (units >= 0)
            {
                // Brain.StepCentral. Every unit sums its bias and its links in the CPU's order,
                // ports from now and units from the step before, through tanh. A port is read
                // where a link names it: the senses do not move within the step, so it reads what
                // the CPU's one read gave, NaN and infinity as 0.
                int k = t.CLinkAt[i];
                int portAt = t.CPortAt[i];
                for (int u = 0; u < units; u++)
                {
                    float sum = nr.Bias[u * N + i];
                    int count = nr.NIn[u * N + i];
                    for (int e = 0; e < count; e++, k++)
                    {
                        int source = nr.CSource[k];
                        float v;
                        if (source >= 0)
                        {
                            v = br.S[(prev + source) * N + i];
                        }
                        else
                        {
                            int slot = portAt + ~source;
                            v = SensorRead(i, nr.CPortPart[slot], nr.CPortChannel[slot], nr.CPortIndex[slot], links, dof,
                                           br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate,
                                           sDrive, energy, rock, sLeaf);
                            if (v != v || v == float.PositiveInfinity || v == float.NegativeInfinity) v = 0f;
                        }

                        sum += nr.CWeight[k] * v;
                    }

                    float value = TanhF(sum);
                    br.S[(cur + u) * N + i] = value != value ? 0f : value;
                }

                parity = 1 - parity;
                br.Parity[i] = parity;
                prev = parity * cfg.MaxN;

                int totalDof = t.CDof[i];
                for (int d = 0; d < totalDof; d++)
                {
                    dr.Signal[d * N + i] = br.S[(prev + 2 * d) * N + i];
                    if (cfg.BendAndPower != 0) dr.Bend[d * N + i] = br.S[(prev + 2 * d + 1) * N + i];
                }

                return;
            }

            for (int group = 0; group < links; group++)
            {
                int at = t.NOff[group * N + i];
                int count = t.NCnt[group * N + i];

                for (int n = 0; n < count; n++)
                {
                    br.S[(cur + at + n) * N + i] = Evaluate(i, at + n, group, time, prev, links, dof, t, nr, br, cfg,
                        dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf);
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

                    // Brain.Step's bend, under the bend-and-power drive only: the part's neuron after
                    // its drive neurons, or still when the part has too few.
                    if (cfg.BendAndPower != 0)
                    {
                        int k = dofs + d;
                        dr.Bend[(start + d) * N + i] = k < own ? Clamp(br.S[(prev + at + k) * N + i]) : 0f;
                    }
                }
            }
        }

        private static float Evaluate(
            int i, int self, int part, float time, int prev, int links, int dof, STopo t, SNeur nr, SBrain br, SCfg cfg,
            int[] dofCount, int[] dofStart,
            float[] sDepth, float[] sUp, float[] sChem, float[] sScent, float[] sFlow, float[] sAngle, float[] sRate, float[] sDrive, float energy, int rock, float[] sLeaf)
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
                              dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf);
            }

            value = value * nr.Amp[slot] + nr.Bias[slot];

            return value != value || value == float.PositiveInfinity || value == float.NegativeInfinity ? 0f : value;
        }

        private static float Apply(
            int i, int self, int part, int prev, int links, int dof, STopo t, SNeur nr, SBrain br, SCfg cfg,
            int[] dofCount, int[] dofStart,
            float[] sDepth, float[] sUp, float[] sChem, float[] sScent, float[] sFlow, float[] sAngle, float[] sRate, float[] sDrive, float energy, int rock, float[] sLeaf)
        {
            int slot = self * cfg.N + i;
            int nIn = nr.NIn[slot];
            float dt = cfg.DtF;

            float a = nIn > 0 ? Read(i, self, 0, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf) : 0f;
            float bb = nIn > 1 ? Read(i, self, 1, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf) : 0f;
            float c = nIn > 2 ? Read(i, self, 2, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf) : 0f;

            switch (nr.Op[slot])
            {
                case 0:     // Sum
                {
                    float acc = 0f;
                    for (int k = 0; k < nIn; k++) acc = acc + Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf);
                    return acc;
                }
                case 1:     // Product
                {
                    if (nIn == 0) return 0f;
                    float acc = 1f;
                    for (int k = 0; k < nIn; k++) acc = acc * Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf);
                    return acc;
                }
                case 4:     // Min
                {
                    if (nIn == 0) return 0f;
                    float acc = float.MaxValue;
                    for (int k = 0; k < nIn; k++) acc = MinF(acc, Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf));
                    return acc;
                }
                case 5:     // Max
                {
                    if (nIn == 0) return 0f;
                    float acc = float.MinValue;
                    for (int k = 0; k < nIn; k++) acc = MaxF(acc, Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf));
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
                    for (int k = 0; k < nIn; k++) acc = acc + Read(i, self, k, part, prev, links, dof, t, nr, br, cfg, dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf);
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
            float[] sDepth, float[] sUp, float[] sChem, float[] sScent, float[] sFlow, float[] sAngle, float[] sRate, float[] sDrive, float energy, int rock, float[] sLeaf)
        {
            int N = cfg.N;
            int ks = (3 * self + k) * N + i;
            float wt = nr.Weight[ks];

            switch (nr.Kind[ks])
            {
                case 0: return nr.Const[ks] * wt;
                case 1: return SensorRead(i, part, nr.Channel[ks], nr.Index[ks], links, dof, br, cfg,
                                          dofCount, dofStart, sDepth, sUp, sChem, sScent, sFlow, sAngle, sRate, sDrive, energy, rock, sLeaf) * wt;
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
            float[] sDepth, float[] sUp, float[] sChem, float[] sScent, float[] sFlow, float[] sAngle, float[] sRate, float[] sDrive, float energy, int rock, float[] sLeaf)
        {
            int N = cfg.N;
            if (part < 0 || part >= links) return 0f;

            // D235: in a nose world a part that is not a nose reads zero on the four smell channels, under either
            // brain (SensorChannels.IsSmell): the nose links are the word's bits 16 and up.
            if (cfg.Nose != 0 && ((rock >> (16 + part)) & 1) == 0 &&
                (channel == SChemical || channel == SScent || channel == SLeaf || channel == SLeafDir))
            {
                return 0f;
            }

            switch (channel)
            {
                case SDepth: return sDepth[part];
                case SUp: return sUp[part];
                case SJointAngle: return DofRead(part, index, 0, dof, dofCount, dofStart, sAngle, sRate, sDrive);
                case SJointRate: return DofRead(part, index, 1, dof, dofCount, dofStart, sAngle, sRate, sDrive);
                case SStroke: return DofRead(part, index, 2, dof, dofCount, dofStart, sAngle, sRate, sDrive);
                case SHold: return DofRead(part, index, 3, dof, dofCount, dofStart, sAngle, sRate, sDrive);
                case SRamp: return DofRead(part, index, 4, dof, dofCount, dofStart, sAngle, sRate, sDrive);
                case SChemical: return (br.Gates[i] & 1) != 0 ? sChem[part] : cfg.ConstCE;
                case SEnergy: return (br.Gates[i] & 2) != 0 ? energy : cfg.ConstCE;
                case SFlow: return index >= 0 && index < 3 ? sFlow[3 * part + index] : 0f;
                case SContact: return br.Contact[part * N + i] != 0 ? 1f : 0f;
                case SDamage: return Clamp(br.Damage[part * N + i]);
                case SScent: return (br.Gates[i] & 4) != 0 ? sScent[part] : 0f;
                case SRock: return ((rock >> part) & 1) != 0 ? 1f : 0f;
                case SLeaf: return sLeaf[4 * part];
                case SLeafDir: return index >= 0 && index < 3 ? sLeaf[4 * part + 1 + index] : 0f;
                default: return 0f;
            }
        }

        private static float DofRead(int part, int index, int which, int dof, int[] dofCount, int[] dofStart,
                                     float[] sAngle, float[] sRate, float[] sDrive)
        {
            int n = dofCount[part];
            int offset = dofStart[part];

            if (offset < 0 || index < 0 || index >= n) return 0f;

            int at = offset + index;
            int length = dof > 0 ? dof : 1;
            if (at >= length) return 0f;

            return which == 0 ? sAngle[at] : which == 1 ? sRate[at] : sDrive[3 * at + which - 2];
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

        /// <summary>Math.Min on doubles, as MinR: NaN propagates, -0 under +0.</summary>
        private static double MinD(double x, double y)
        {
            if (x != y)
            {
                if (!(x != x)) return x < y ? x : y;
                return x;
            }
            return x < 0 || (x == 0 && 1.0 / x < 0) ? x : y;
        }

        /// <summary>Math.Max on doubles, as MaxR: NaN propagates, +0 over -0.</summary>
        private static double MaxD(double x, double y)
        {
            if (x != y)
            {
                if (!(x != x)) return y < x ? x : y;
                return x;
            }
            return y < 0 || (y == 0 && 1.0 / y < 0) ? x : y;
        }

        private static double AbsD(double x) => x < 0 ? -x : x;

        /// <summary>EffectorDrive.Toward: one step of a ramp, landing on its end.</summary>
        private static double Toward(double from, double to, double step)
        {
            if (from < to) return from + step >= to ? to : from + step;
            if (from > to) return from - step <= to ? to : from - step;
            return from;
        }

        // The CPU's own arithmetic: float operands promoted to double, System.Math, narrowed.
        private static float OscSin(double arg) => (float)SinR(arg);
        private static float SinF(float a) => (float)SinR(a);
        private static float CosF(float a) => (float)CosR(a);
        private static float FloorF(float a) => (float)Math.Floor(a);
        private static Real SqrtR(Real a) => Math.Sqrt(a);
        private static Real AbsR(Real a) => Math.Abs(a);

        // Guards against ILGPU 1.5.3's double maths on the card, not world rules. Its Sin and Cos
        // bring the angle into range by subtracting 2 pi in a loop (XMath/Cordic.tt, RangeLimit), which
        // past about 7.2e16 never ends; its Exp, under Tanh, loops up to int.MaxValue times
        // (Cordic.Pow.tt). On the accelerator these are .NET's own, and the guards are bit-neutral
        // inside 1e9 rad: an angle past AngleGuard is reduced, so a body that samples one parts from
        // the CPU's arithmetic there too (the bed counts such angles, BedAnglesPastGuard), while tanh
        // past TanhGuard is 1 in double already, so 1f once narrowed, on either side (card-audit-r57.md,
        // the card in double hangs on a far body). A pass of
        // the reduction leaves at most half the angle's spacing, so a huge angle takes a few passes,
        // about 20 from 1e308; NaN and an infinity leave as NaN.
        private const double AngleGuard = 1e9;
        private const float TanhGuard = 20f;
        internal static double Reduced(double a)
        {
            const double Turn = 6.283185307179586;
            if (!(a > AngleGuard || a < -AngleGuard)) return a;
            do { a -= Turn * Math.Floor(a / Turn); } while (a > 64.0 || a < -64.0);
            return a;
        }
        internal static double SinR(double a) => Math.Sin(Reduced(a));
        internal static double CosR(double a) => Math.Cos(Reduced(a));
        internal static float TanhF(float a) => a > TanhGuard ? 1f : a < -TanhGuard ? -1f : (float)Math.Tanh(a);

        // ============================================================== fluid

        /// <summary>
        /// Fluid.Apply for every link of one class's bodies, before the class's step and after its
        /// water pass: a thread a link. It reads the state the step loads and the water WaterLinks
        /// wrote, computes the step's own terms, and leaves the link's relative velocity, its drag
        /// before the limiter, the torque and force it adds and its limiter count, which the step
        /// takes at the fluid's place in its order. The senses still read the relative velocity
        /// the last step stored, so nothing here is written where the step loads it.
        /// </summary>
        public static void FluidLinks(Index1D index, STopo t, SConst b, SState s, SPanels pan, SGlob gl, SCfg cfg)
        {
            int used = cfg.ClassUsed;
            int i = index.X / used;
            int c = index.X - i * used;
            if (i >= cfg.ClassLinks) return;

            int g = t.GSlot[c];
            if (g < 0 || gl.Alive[g] == 0 || i >= t.Links[c]) return;

            int n = cfg.N;
            int rowsAll = cfg.LinkRows;
            int row = g * LinkStride + i;

            Real k = cfg.DragK;
            Real dt = cfg.Dt;
            long limited = 0;
            bool accelerating = cfg.FluidAccel > 0;
#if GPU_PHASE_PROBE
            long clk = 0;
            if (CudaAsm.IsSupported) CudaAsm.Emit("mov.u64 %0, %%clock64;", out clk);
#endif

            var rotationMatrix = new Real[9];
            var velocity = new Real[3];
            var water = new Real[3];
            var spin = new Real[3];
            var wacc = new Real[3];
            for (int e = 0; e < 9; e++) rotationMatrix[e] = s.RotM[(9 * i + e) * n + c];
            for (int e = 0; e < 3; e++)
            {
                int a3 = (3 * i + e) * n + c;
                velocity[e] = s.Vel[a3];
                water[e] = s.Water[a3];
                spin[e] = s.Spin[a3];
                wacc[e] = s.WaterAcc[a3];
            }

            Real excess = b.Excess[c];
            Real mass = b.Mass[i * n + c];
            Real lift = b.Lift[i * n + c];

            // D220: remains take no float lift, as Fluid.Apply's `body.Remains ? 0.0 : body.Lift[i]`. The
            // flag is read only in a block with remains; a lift of 0 gives (1 - 0), the CPU's own 1.0.
            if (cfg.AnyRemains != 0 && gl.Remains[g] != 0) lift = 0;

            Real volume = b.Volume[i * n + c];
            Real smallI = b.SmallI[i * n + c];
            Real arm = b.Arm[i * n + c];
            int thinAxis = t.ThinAxis[i * n + c];

            M3 rotation = M3.Read(rotationMatrix, 0);

            V3 relative = V3.Read(velocity, 0) - V3.Read(water, 0);

            V3 w = V3.Read(spin, 0);

            V3 localVelocity = rotation.TransposedTimes(relative);
            V3 localSpin = rotation.TransposedTimes(w);

            V3 localForce = V3.Zero;
            V3 localTorque = V3.Zero;

            int from = t.PanelStart[i * n + c];
            int to = t.PanelStart[(i + 1) * n + c];
#if GPU_PHASE_PROBE
            LinkMark(gl, cfg, row, 3, ref clk);
#endif

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
#if GPU_PHASE_PROBE
            LinkMark(gl, cfg, row, 4, ref clk);
#endif

            Real height = s.Pos[(3 * i + 1) * n + c];

            // D161, only in the water, summed after the pressure drag as Fluid.Apply sums it, so
            // the ledger counts it and the limiter caps it. Not entered at none, the recorded bits.
            if (cfg.TurningDrag == 1 && height <= 0)
            {
                // The shear on each panel's velocity along its face, leading and trailing alike.
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

                    V3 along = panelVelocity - normal * normalSpeed;
                    Real slide = along.Magnitude;
                    if (slide == 0) continue;

                    V3 shear = along * (cfg.NegSkin * pan.Area[p * n + c] * slide);

                    localForce = localForce + shear;
                    localTorque = localTorque + V3.Cross(centre, shear);
                }
            }
            else if (cfg.TurningDrag == 2 && height <= 0)
            {
                // A torque about each of the link's own axes against its spin, and no force.
                Real brake = cfg.NegSpin * localSpin.Magnitude;
                localTorque = localTorque + new V3(
                    brake * b.SpinMoment[(3 * i) * n + c] * localSpin.X,
                    brake * b.SpinMoment[(3 * i + 1) * n + c] * localSpin.Y,
                    brake * b.SpinMoment[(3 * i + 2) * n + c] * localSpin.Z);
            }

            V3 force = rotation * localForce;
            V3 torque = rotation * localTorque;

            // NoteDrag: before the limiter and the two terms below.
            V3 drag = force;
            V3 dragTorque = torque;

            if (cfg.LimitDrag != 0)
            {
                Real speed = relative.Magnitude;
                if (speed > 0)
                {
                    V3 direction = relative * ((Real)1.0 / speed);
                    Real opposing = -V3.Dot(force, direction);
                    Real allowed = mass * speed / dt;
                    if (opposing > allowed)
                    {
                        force = force + direction * (opposing - allowed);
                        limited++;
                    }
                }

                // D199/D200: under the welded law, what the cap took from the force (rows 17 to 19,
                // Creature.NoteWeldedForceCut), and no torque cap here; WeldedDrag caps the torque.
                if (cfg.WeldedDrag != 0)
                {
                    V3 cut = force - drag;
                    gl.LinkFluid[17 * rowsAll + row] = cut.X;
                    gl.LinkFluid[18 * rowsAll + row] = cut.Y;
                    gl.LinkFluid[19 * rowsAll + row] = cut.Z;
                }
                else
                {
                    Real spinRate = w.Magnitude;
                    if (spinRate > 0)
                    {
                        V3 axis = w * ((Real)1.0 / spinRate);
                        Real opposing = -V3.Dot(torque, axis);
                        Real allowed = smallI * spinRate / dt;
                        if (opposing > allowed)
                        {
                            torque = torque + axis * (opposing - allowed);
                            limited++;
                        }
                    }
                }
            }

            if (accelerating)
            {
                V3 accelerationForce = V3.Read(wacc, 0) *
                    (cfg.FluidAccel * cfg.Density * volume *
                     ((Real)1.0 + cfg.AddedMass));

                if (accelerationForce.Y > 0 && height >= 0)
                {
                    accelerationForce = new V3(accelerationForce.X, 0, accelerationForce.Z);
                }

                force = force + accelerationForce;
            }

            // Fluid.Apply's two pushes. The swing's is whole here: it reads the spins the step
            // starts from, which the drive does not change. The power's reads this step's stroke,
            // which the class kernel's drive writes after this kernel has run, so under it the
            // force is left before the push and the buoyancy term apart (rows 15 and 16), and the
            // class kernel adds the push and then the buoyancy, the CPU's order.
            bool powered = cfg.BendAndPower != 0 && cfg.LimbPush > 0 && cfg.PushOmega > 0;
            int parentLink = t.Parent[i * n + c];
            if (cfg.LimbPush > 0 && !powered && parentLink >= 0 && height <= 0)
            {
                V3 anchor = new V3(b.ChildAnchor[(3 * i) * n + c], b.ChildAnchor[(3 * i + 1) * n + c],
                                   b.ChildAnchor[(3 * i + 2) * n + c]);
                V3 toOrigin = rotation * (-anchor);
                Real reach = toOrigin.Magnitude;
                if (reach > 0)
                {
                    V3 axis = toOrigin * ((Real)1.0 / reach);
                    V3 parentSpin = new V3(s.Spin[(3 * parentLink) * n + c], s.Spin[(3 * parentLink + 1) * n + c],
                                           s.Spin[(3 * parentLink + 2) * n + c]);
                    V3 swing = w - parentSpin;
                    V3 across = swing - axis * V3.Dot(swing, axis);
                    Real addedMass = cfg.AddedMass * cfg.Density * volume;

                    // D229: the part's added mass for the direction of its swing. A sphere (shape 0)
                    // keeps the line above.
                    bool shapedSwing = false;
                    Real shapedLever = reach;
                    if (cfg.ShapedMass != 0)
                    {
                        int shapeCode = b.ShapeCode[i * n + c];
                        V3 swingMotion = V3.Cross(across, axis);
                        Real acrossLength = swingMotion.Magnitude;
                        // D255: shape code 3 (a family part) is never read as a box here. The host refuses a family limb
                        // under the swing push when the body is built (Creature.RefuseFamilyStroke), so this guard is never
                        // reached; it stops a future path reading the code's unwritten half-extents as one.
                        if (acrossLength > 0 && shapeCode != 0 && shapeCode != 3)
                        {
                            V3 swingDirection = swingMotion * ((Real)1.0 / acrossLength);
                            addedMass = ShapedAddedMass(
                                shapeCode, b.HalfExt[(3 * i) * n + c], b.HalfExt[(3 * i + 1) * n + c],
                                b.HalfExt[(3 * i + 2) * n + c], rotation, axis,
                                swingDirection, cfg.Density);
                            shapedLever = LeverOf(reach, axis, swingDirection);
                            shapedSwing = true;
                        }
                    }

                    Real push = cfg.LimbPush * addedMass * reach * V3.Dot(across, across);
                    if (shapedSwing) push = cfg.LimbPush * addedMass * shapedLever * V3.Dot(across, across);
                    force = force + axis * (-push);
                }
            }

            Real netDensity = excess * ((Real)1.0 - lift);
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

            Real buoyancy = 0;
            if (netDensity != 0)
            {
                buoyancy = -netDensity * mass * cfg.Gravity / cfg.TissueDensity;
                if (!powered) force = force + new V3(0, buoyancy, 0);
            }

            // D111: the part's whole displaced weight at its buoyancy centre, gated on the clamp.
            if (cfg.OffsetTorque != 0 && arm != 0 && height <= 0 &&
                !(netDensity == 0 && unclamped != 0))
            {
                int axis = thinAxis;
                V3 lever = rotation * new V3(
                    axis == 0 ? arm : 0,
                    axis == 1 ? arm : 0,
                    axis == 2 ? arm : 0);

                V3 displaced = new V3(0, cfg.Density * volume * cfg.Gravity, 0);

                torque = torque + V3.Cross(lever, displaced);
            }
#if GPU_PHASE_PROBE
            LinkMark(gl, cfg, row, 5, ref clk);
#endif

            gl.LinkFluid[row] = relative.X;
            gl.LinkFluid[rowsAll + row] = relative.Y;
            gl.LinkFluid[2 * rowsAll + row] = relative.Z;
            gl.LinkFluid[3 * rowsAll + row] = drag.X;
            gl.LinkFluid[4 * rowsAll + row] = drag.Y;
            gl.LinkFluid[5 * rowsAll + row] = drag.Z;
            gl.LinkFluid[6 * rowsAll + row] = dragTorque.X;
            gl.LinkFluid[7 * rowsAll + row] = dragTorque.Y;
            gl.LinkFluid[8 * rowsAll + row] = dragTorque.Z;
            gl.LinkFluid[9 * rowsAll + row] = torque.X;
            gl.LinkFluid[10 * rowsAll + row] = torque.Y;
            gl.LinkFluid[11 * rowsAll + row] = torque.Z;
            gl.LinkFluid[12 * rowsAll + row] = force.X;
            gl.LinkFluid[13 * rowsAll + row] = force.Y;
            gl.LinkFluid[14 * rowsAll + row] = force.Z;
            if (powered)
            {
                gl.LinkFluid[15 * rowsAll + row] = buoyancy;
                gl.LinkFluid[16 * rowsAll + row] = netDensity != 0 ? 1 : 0;
            }
            gl.LinkLimited[row] = (int)limited;
#if GPU_PHASE_PROBE
            LinkMark(gl, cfg, row, 6, ref clk);
#endif
        }

        /// <summary>
        /// Fluid.LeverOf (D229): the lever a limb's push acts at for one direction of motion. Today the
        /// reach, exactly. The one place a later rule would move it; the work line must follow.
        /// </summary>
        private static Real LeverOf(Real reach, V3 axis, V3 direction) => reach;

        /// <summary>
        /// Fluid.AssemblyLeverOf (D231): the lever a stroke's push acts at when it moves a whole assembly,
        /// the first moment of the added mass over the added mass, <c>I1 / M</c>. Where nothing is moved
        /// (the mass not above 0) the reach, so that the work line stays finite.
        /// </summary>
        private static Real AssemblyLeverOf(Real firstMoment, Real addedMass, Real reach) =>
            addedMass > 0 ? firstMoment / addedMass : reach;

        /// <summary>
        /// Fluid.AddedMassOf (D229) for a box (shape 1) or a capsule (shape 2), in the same operations in
        /// the same order, so the double kernel is the solver's to the bit. A box's h0..h2 are its
        /// half-extents; a capsule's h0 is its radius and h1 its half-span, which the host took from
        /// CapsuleShape. A sphere never comes here, for its value is the unshaped line's.
        /// </summary>
        private static Real ShapedAddedMass(
            int shape, Real h0, Real h1, Real h2, M3 rotation, V3 axis, V3 direction, Real density)
        {
            if (shape == 2)
            {
                return density * (Real)Math.PI * h0 * h0 * ((Real)2.0 * h1) +
                       (Real)0.5 * density * (Real)(4.0 / 3.0) * (Real)Math.PI * h0 * h0 * h0;
            }

            V3 localAxis = rotation.TransposedTimes(axis);
            V3 localDirection = rotation.TransposedTimes(direction);
            V3 transverse = localDirection - localAxis * V3.Dot(localDirection, localAxis);
            if (!(transverse.SqrMagnitude > 0)) return 0;
            V3 across = V3.Cross(localAxis, transverse);
            Real acrossLength = across.Magnitude;
            if (!(acrossLength > 0)) return 0;
            across = across * ((Real)1.0 / acrossLength);

            Real length = (Real)2.0 * (System.Math.Abs(localAxis.X) * h0 + System.Math.Abs(localAxis.Y) * h1 +
                                       System.Math.Abs(localAxis.Z) * h2);
            Real width = (Real)2.0 * (System.Math.Abs(across.X) * h0 + System.Math.Abs(across.Y) * h1 +
                                      System.Math.Abs(across.Z) * h2);

            return density * (Real)(Math.PI / 4.0) * width * width * length;
        }

        /// <summary>
        /// Creature.LimitWeldedDrag (D199/D200), for one body: each welded body's drag torque about
        /// its centre of mass, the moments of its links' capped forces included, capped on its
        /// principal axes. Each sum is taken over the body's links in link order, the order the CPU
        /// adds them in, so the double kernel is the solver's to the bit. Returns the caps that bound.
        /// </summary>
        private static long WeldedDrag(
            int rowBase, int fluidRows, int links, int[] parent, int[] dofCount, Real[] mass, Real[] position,
            Real[] rotationMatrix, Real[] inertiaLocal, Real[] spin, Real[] dragF, Real[] dragT, Real[] fext,
            int[] anchor, Real[] jm, Real[] jv, SGlob gl, SCfg cfg)
        {
            long limited = 0;
            Real dt = cfg.Dt;

            for (int l = 0; l < links; l++)
            {
                anchor[l] = parent[l] >= 0 && dofCount[l] == 0 ? anchor[parent[l]] : l;
            }

            for (int a = 0; a < links; a++)
            {
                if (anchor[a] != a) continue;

                Real total = 0, mx = 0, my = 0, mz = 0;
                for (int l = a; l < links; l++)
                {
                    if (anchor[l] != a) continue;
                    Real m = mass[l];
                    total += m;
                    mx += m * position[3 * l];
                    my += m * position[3 * l + 1];
                    mz += m * position[3 * l + 2];
                }

                var centre = new V3(mx / total, my / total, mz / total);

                Real tx = 0, ty = 0, tz = 0;
                Real ixx = 0, iyy = 0, izz = 0, ixy = 0, ixz = 0, iyz = 0;
                for (int l = a; l < links; l++)
                {
                    if (anchor[l] != a) continue;

                    int frow = rowBase + l;
                    V3 r = V3.Read(position, 3 * l) - centre;
                    V3 cut = new V3(gl.LinkFluid[17 * fluidRows + frow], gl.LinkFluid[18 * fluidRows + frow],
                                    gl.LinkFluid[19 * fluidRows + frow]);
                    V3 f = V3.Read(dragF, 3 * l) + cut;
                    V3 t = V3.Read(dragT, 3 * l) + V3.Cross(r, f);
                    tx += t.X;
                    ty += t.Y;
                    tz += t.Z;

                    M3 own = M3.RotateDiagonal(M3.Read(rotationMatrix, 9 * l), V3.Read(inertiaLocal, 3 * l));
                    Real m = mass[l];
                    Real rr = V3.Dot(r, r);
                    ixx += own.M00 + m * (rr - r.X * r.X);
                    iyy += own.M11 + m * (rr - r.Y * r.Y);
                    izz += own.M22 + m * (rr - r.Z * r.Z);
                    ixy += own.M01 - m * r.X * r.Y;
                    ixz += own.M02 - m * r.X * r.Z;
                    iyz += own.M12 - m * r.Y * r.Z;
                }

                V3 w = V3.Read(spin, 3 * a);
                if (!(w.SqrMagnitude > 0)) continue;

                var torque = new V3(tx, ty, tz);
                SymmetricEigen(ixx, iyy, izz, ixy, ixz, iyz, jm, jv);

                V3 torqueCut = V3.Zero;
                for (int k = 0; k < 3; k++)
                {
                    var axis = new V3(jv[k], jv[3 + k], jv[6 + k]);
                    Real moment = jm[4 * k];
                    if (!(moment > 0)) continue;

                    Real wk = V3.Dot(w, axis);
                    Real along = V3.Dot(torque, axis);
                    if (along * wk >= 0) continue;

                    Real allowed = moment * (wk < 0 ? -wk : wk) / dt;
                    if ((along < 0 ? -along : along) > allowed)
                    {
                        torqueCut = torqueCut + axis * ((along > 0 ? allowed : -allowed) - along);
                        limited++;
                    }
                }

                if (torqueCut.SqrMagnitude > 0) V3.Add(fext, 6 * a, torqueCut);
            }

            return limited;
        }

        /// <summary>
        /// Creature.SymmetricEigen: cyclic Jacobi rotations on a symmetric 3x3, left in
        /// <paramref name="m"/> (the eigenvalues on its diagonal) and <paramref name="v"/> (the unit
        /// eigenvectors as its columns), row-major, in the CPU's order of operations.
        /// </summary>
        private static void SymmetricEigen(Real a00, Real a11, Real a22, Real a01, Real a02, Real a12, Real[] m, Real[] v)
        {
            m[0] = a00; m[1] = a01; m[2] = a02;
            m[3] = a01; m[4] = a11; m[5] = a12;
            m[6] = a02; m[7] = a12; m[8] = a22;
            for (int e = 0; e < 9; e++) v[e] = 0;
            v[0] = 1; v[4] = 1; v[8] = 1;

            Real scale = a00 * a00 + a11 * a11 + a22 * a22;

            for (int sweep = 0; sweep < 32; sweep++)
            {
                Real off = m[1] * m[1] + m[2] * m[2] + m[5] * m[5];
                if (!(off > (Real)1e-30 * scale)) break;

                for (int pair = 0; pair < 3; pair++)
                {
                    int p = pair == 2 ? 1 : 0;
                    int q = pair == 0 ? 1 : 2;

                    Real apq = m[3 * p + q];
                    if (apq == 0) continue;

                    Real theta = (m[3 * q + q] - m[3 * p + p]) / ((Real)2.0 * apq);
                    Real tan = (theta >= 0 ? (Real)1.0 : (Real)(-1.0)) /
                               ((theta < 0 ? -theta : theta) + System.Math.Sqrt(theta * theta + (Real)1.0));
                    Real cos = (Real)1.0 / System.Math.Sqrt(tan * tan + (Real)1.0);
                    Real sin = tan * cos;

                    for (int k = 0; k < 3; k++)
                    {
                        Real kp = m[3 * k + p], kq = m[3 * k + q];
                        m[3 * k + p] = cos * kp - sin * kq;
                        m[3 * k + q] = sin * kp + cos * kq;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        Real pk = m[3 * p + k], qk = m[3 * q + k];
                        m[3 * p + k] = cos * pk - sin * qk;
                        m[3 * q + k] = sin * pk + cos * qk;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        Real kp = v[3 * k + p], kq = v[3 * k + q];
                        v[3 * k + p] = cos * kp - sin * kq;
                        v[3 * k + q] = sin * kp + cos * kq;
                    }
                }
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

        // ============================================================== contacts

        /// <summary>
        /// ContactGrid.Neighbours and NeighbourLinks: every row sharing a cell with
        /// <paramref name="self"/> and not belonging to <paramref name="owner"/>, as sort keys (the
        /// list rank, or under per-part contact rank·16 + link, which is the CPU's row order)
        /// ascending and once each.
        /// </summary>
        private static int Candidates(int self, int owner, int[] cand, SGlob gl, SGrid gr, SCfg cfg, bool countOverflow)
        {
            int R = cfg.Rows;
            bool perPart = cfg.PerPart != 0;

            int lx = gr.Lo[self], hx = gr.Hi[self];
            int ly = gr.Lo[R + self], hy = gr.Hi[R + self];
            int lz = gr.Lo[2 * R + self], hz = gr.Hi[2 * R + self];

            int found = 0;
            int dropped = 0;
            SeamCells(gr.Cell[0], cfg, out int wx, out int wz, out _, out _);

            // A giant with its list whole reads the list: every row whose cell box meets its own, which
            // is every row the cell walk would have met in some cell it covers.
            int gk = gr.IsGiant[self] - 1;
            if (gk >= 0 && gr.GiantN[gk] <= GiantListCap)
            {
                int n = gr.GiantN[gk];
                for (int i = 0; i < n; i++)
                {
                    int other = gr.GiantList[gk * GiantListCap + i];
                    int og = perPart ? other / LinkStride : other;
                    if (og == owner) continue;
                    found = Keep(KeyOf(other, og, perPart, gl), cand, found, ref dropped);
                }
            }
            else
            {
                for (int x = lx; x <= hx; x++)
                for (int y = ly; y <= hy; y++)
                for (int z = lz; z <= hz; z++)
                {
                    int h = Hash(Wrap(x, wx), y, Wrap(z, wz)) & cfg.Mask;

                    for (int k = gr.Start[h]; k < gr.Start[h + 1] && k < cfg.EntryCap; k++)
                    {
                        int other = gr.Items[k];
                        int og = perPart ? other / LinkStride : other;
                        if (og == owner) continue;

                        if (!Spans(x, gr.Lo[other], gr.Hi[other], wx) ||
                            y < gr.Lo[R + other] || y > gr.Hi[R + other] ||
                            !Spans(z, gr.Lo[2 * R + other], gr.Hi[2 * R + other], wz))
                        {
                            continue;
                        }

                        found = Keep(KeyOf(other, og, perPart, gl), cand, found, ref dropped);
                    }
                }

                // The giants are in no bucket: each is met where the cell walk would have met it, in a
                // cell both boxes cover, which is wherever the boxes meet. An empty box meets nothing.
                int ng = gr.Giant[0];
                if (ng > GiantCap) ng = GiantCap;
                for (int i = 0; i < ng && lx <= hx; i++)
                {
                    int other = gr.Giant[1 + i];
                    int og = perPart ? other / LinkStride : other;
                    if (og == owner) continue;
                    if (!BoxesMeet(lx, hx, ly, hy, lz, hz, other, gr, R, wx, wz)) continue;
                    found = Keep(KeyOf(other, og, perPart, gl), cand, found, ref dropped);
                }
            }

            // Counted once a query, as before, but only when a distinct candidate fell out.
            if (dropped != 0 && countOverflow) Atomic.Add(ref gl.Overflow[OverflowCandidates], 1L);
            if (countOverflow) Atomic.Max(ref gl.Overflow[WatchCandidates], (long)found);

            return found;
        }

        /// <summary>A candidate's sort key: the list rank, or under per-part contact rank·16 + link.</summary>
        private static int KeyOf(int other, int og, bool perPart, SGlob gl) =>
            perPart ? gl.Rank[og] * LinkStride + (other - og * LinkStride) : gl.Rank[og];

        /// <summary>
        /// Whether a row's cell box, not empty, meets the box lx..hz: on x and z round the seam when
        /// there is one (<paramref name="wx"/>, <paramref name="wz"/> above 0).
        /// </summary>
        private static bool BoxesMeet(int lx, int hx, int ly, int hy, int lz, int hz, int other, SGrid gr, int R, int wx, int wz)
        {
            int olx = gr.Lo[other], ohx = gr.Hi[other];
            if (olx > ohx) return false;
            return Meet(lx, hx, olx, ohx, wx) &&
                   gr.Lo[R + other] <= hy && ly <= gr.Hi[R + other] &&
                   Meet(lz, hz, gr.Lo[2 * R + other], gr.Hi[2 * R + other], wz);
        }

        /// <summary>
        /// ContactGrid.JoinSeams: the cells round each seam (0 without one) and the cell along x and
        /// z, a whole fraction of the box, from the grid's cell. Every kernel that reads the grid
        /// derives them from the same cell by the same arithmetic.
        /// </summary>
        private static void SeamCells(Real cell, SCfg cfg, out int wx, out int wz, out Real cellX, out Real cellZ)
        {
            if (cfg.SeamL > 0 && cfg.SeamW > 0)
            {
                wx = (int)(cfg.SeamL / cell);
                wz = (int)(cfg.SeamW / cell);
                if (wx < 1) wx = 1;
                if (wz < 1) wz = 1;
                cellX = cfg.SeamL / wx;
                cellZ = cfg.SeamW / wz;
            }
            else
            {
                wx = 0;
                wz = 0;
                cellX = cell;
                cellZ = cell;
            }
        }

        /// <summary>A cell index round a seam of <paramref name="n"/> cells; itself when n is 0.</summary>
        private static int Wrap(int v, int n)
        {
            if (n == 0) return v;
            int m = v % n;
            return m < 0 ? m + n : m;
        }

        /// <summary>ContactGrid.Covers on one axis: whether the span lo..hi covers cell x, round the seam.</summary>
        private static bool Spans(int x, int lo, int hi, int n) =>
            n == 0 ? x >= lo && x <= hi : Wrap(x - lo, n) <= hi - lo;

        /// <summary>Whether two spans meet on one axis: on a ring, the start of one lies in the other.</summary>
        private static bool Meet(int a0, int a1, int b0, int b1, int n) =>
            n == 0 ? b0 <= a1 && a0 <= b1 : Wrap(b0 - a0, n) <= a1 - a0 || Wrap(a0 - b0, n) <= b1 - b0;

        /// <summary>
        /// Contacts.ShortWay on one axis, in the processor's order: the plain difference, moved by
        /// the box's whole extent once when it is further than half, so the accelerator in double is
        /// the solver to the bit. In single a pair across a seam loses about an ulp of the box's
        /// length in its separation, some 1e-5 m at 300 m, far under any contact's scale.
        /// </summary>
        private static Real ShortWay(Real a, Real b, Real length)
        {
            Real d = a - b;
            if (!(length > 0)) return d;
            Real half = (Real)0.5 * length;
            if (d > half) return d - length;
            if (d < -half) return d + length;
            return d;
        }

        /// <summary>
        /// One key into the kept keys, which stay ascending and distinct. The order rows arrive in is
        /// Scatter's race (and the giants' lists' race), so past CandCap the highest key falls out: what
        /// remains is the CandCap lowest of the distinct keys whatever the race delivered, and an
        /// overflow is as deterministic as a query under the cap (2026-10-16; the first that arrived
        /// were kept before, and a resume over overflows parted at once).
        /// </summary>
        private static int Keep(int key, int[] cand, int found, ref int dropped)
        {
            int lo = 0, hi = found;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (cand[mid] < key) lo = mid + 1;
                else hi = mid;
            }

            if (lo < found && cand[lo] == key) return found;

            if (found == CandCap)
            {
                dropped = 1;
                if (lo == CandCap) return found;
                for (int m = CandCap - 1; m > lo; m--) cand[m] = cand[m - 1];
            }
            else
            {
                for (int m = found; m > lo; m--) cand[m] = cand[m - 1];
                found++;
            }

            cand[lo] = key;
            return found;
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
            Atomic.Max(ref gl.Overflow[WatchOverlaps], ((long)(count + 1) << 24) | (long)(0xFFFFFF - gl.Rank[g]));
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

            Atomic.Max(ref gl.Overflow[WatchOverlaps], ((long)(count + 1) << 24) | (long)(0xFFFFFF - gl.Rank[g]));
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
                int unique = Candidates(g, g, cand, gl, gr, cfg, true);

                for (int k = 0; k < unique; k++)
                {
                    int other = gl.RankToSlot[cand[k]];
                    if (other < 0 || s.Active[other] == 0) continue;

                    Real bx = ShortWay(cx, s.Cx[other], cfg.SeamL);
                    Real by = cy - s.Cy[other];
                    Real bz = ShortWay(cz, s.Cz[other], cfg.SeamW);
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

            Real noKept = 0, noX = 0, noY = 0, noZ = 0;
            AgainstTheWorld(cx, cy, cz, r, 0, 0, 0, vx, vy, vz, m, bedStiffness, bedDamping, w, cfg,
                            ref fx, ref fy, ref fz, ref bedGlass, 0, ref noKept, ref noX, ref noY, ref noZ);

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

        /// <summary>
        /// Contacts.ApplyPerPart (D114), every link's own sphere against every other body's links:
        /// the step's half, which takes what ContactLinks left for each link.
        /// </summary>
        private static void ContactsPerPartStaged(
            int g, int links, Real[] fext, int[] cand, SSph s, SGlob gl, SGrid gr, SCfg cfg)
        {
            // ContactLinks ran each link's part before this kernel. Here the links are taken in
            // order, as the CPU takes them: each one's overlaps noted as it met them, its bed or
            // glass flag, and its push added to its force where the CPU adds it.
            bool instrument = cfg.Instrument != 0;
            int R = cfg.Rows;
            int nOver = 0;
            int bedGlass = 0;

            for (int i = 0; i < links; i++)
            {
                int row = g * LinkStride + i;

                if (instrument)
                {
                    int staged = gl.StageN[row];
                    if (staged <= StageCap)
                    {
                        for (int c = 0; c < staged; c++)
                        {
                            nOver = NoteOverlapPart(g, nOver, gl.StageId[c * R + row], gl.StageSlot[c * R + row],
                                                    i, gl.StagePart[c * R + row], gl, cfg);
                        }
                    }
                    else
                    {
                        nOver = OverlapsAgain(g, i, nOver, cand, s, gl, gr, cfg);
                    }
                }

                int flags = gl.LinkFlags[row];
                if ((flags & 1) != 0) bedGlass = 1;
                if ((flags & 2) != 0)
                {
                    fext[6 * i + 3] += gl.LinkPush[row];
                    fext[6 * i + 4] += gl.LinkPush[R + row];
                    fext[6 * i + 5] += gl.LinkPush[2 * R + row];

                    // D228 level (b): the torque of the touches about the link's centre, where the grip puts its own
                    // (Grip: V3.Add(fext, 6 * l, torque)). Added where the host adds it, under the same gate as the force.
                    if (cfg.AtPoint != 0)
                    {
                        fext[6 * i] += gl.LinkPush[3 * R + row];
                        fext[6 * i + 1] += gl.LinkPush[4 * R + row];
                        fext[6 * i + 2] += gl.LinkPush[5 * R + row];
                    }
                }
            }

            if (instrument)
            {
                gl.NOver[g] = nOver;
                gl.BedGlass[g] = bedGlass;
            }
        }

        /// <summary>
        /// A link that met more overlaps than StageCap has them found again here, in the order
        /// ContactLinks met them, with the same test; the candidates' overflow was counted there.
        /// </summary>
        private static int OverlapsAgain(int g, int i, int nOver, int[] cand, SSph s, SGlob gl, SGrid gr, SCfg cfg)
        {
            int row = g * LinkStride + i;
            Real cx = s.LCx[row], cy = s.LCy[row], cz = s.LCz[row];
            Real radius = s.LR[row];
            Real ex = s.LEx[row], ey = s.LEy[row], ez = s.LEz[row];

            int n = Candidates(row, g, cand, gl, gr, cfg, false);

            // D228: the true shape's narrowphase needs GJK's simplex.
            var simplex = new Real[GjkSimplex];

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
                if (CarryBase)
                {
                    bx = bx + (ex - s.LEx[orow]);
                    by = by + (ey - s.LEy[orow]);
                    bz = bz + (ez - s.LEz[orow]);
                }
                Real distance = System.Math.Sqrt(bx * bx + by * by + bz * bz);
                Real penetration = radius + s.LR[orow] - distance;
                if (penetration <= 0) continue;

                if (cfg.TrueShape != 0)
                {
                    Real onx, ony, onz, open;
                    if (!TrueShapePair(s, gl, simplex, g, i, og, j, bx, by, bz, radius, s.LR[orow],
                                       out onx, out ony, out onz, out open))
                    {
                        continue;
                    }
                }

                nOver = NoteOverlapPart(g, nOver, gl.Id[og], og, i, j, gl, cfg);
            }

            return nOver;
        }

        /// <summary>
        /// Contacts.Apply under per-part contact, a link a thread, before the class's step: the
        /// link's push from the committed spheres, the bed, the reefs and the glass, computed as
        /// the step computed it, with the overlaps it met in the order it met them (the first
        /// StageCap of them) and two flags, bed or glass and a push to add. The step adds the push
        /// and notes the overlaps in link order, so the force and the list are the step's own.
        /// </summary>
        public static void ContactLinks(Index1D index, STopo t, SConst b, SGlob gl, SSph s, SGrid gr, SWorld w, SCfg cfg)
        {
            int used = cfg.ClassUsed;
            int i = index.X / used;
            int bi = index.X - i * used;
            if (i >= cfg.ClassLinks) return;

            int g = t.GSlot[bi];
            if (g < 0 || gl.Alive[g] == 0) return;
            int links = t.Links[bi];
            if (i >= links) return;

            // A stray of this step (LoseStrays), still alive until its class kernel: its rows are in
            // no cell, and its bed and glass would be read at its stray position. Read after the
            // grid, on the stream that waited for it, so the read races nothing.
            if (gl.StrayCode[g] != 0) return;

            int N = cfg.N;
            int R = cfg.Rows;
            int row = g * LinkStride + i;
            bool instrument = cfg.Instrument != 0;
            int staged = 0;
            int bedGlass = 0;
            var cand = new int[CandCap];

            Real totalMass = b.TotalMass[bi];
            Real cx = s.LCx[row], cy = s.LCy[row], cz = s.LCz[row];
            Real radius = s.LR[row];
            Real ex = s.LEx[row], ey = s.LEy[row], ez = s.LEz[row];
            Real vx = s.LVx[row], vy = s.LVy[row], vz = s.LVz[row];
            Real m = b.Mass[i * N + bi];

            // D228: the true shape's narrowphase needs GJK's simplex.
            var simplex = new Real[GjkSimplex];

            Real fx = 0, fy = 0, fz = 0;

            // D228 level (b): this link's committed spin and smallest inertia, and the torque the touches sum, read
            // and kept only at that level (ContactTrueShape.Apply: spin and inertia are zero and the torque unused at the others).
            V3 pSpin = V3.Zero;
            Real pInertia = 0;
            Real qx = 0, qy = 0, qz = 0;
            if (cfg.AtPoint != 0)
            {
                pSpin = new V3(s.LSp0[row], s.LSp1[row], s.LSp2[row]);
                pInertia = b.SmallI[i * N + bi];
            }

            if (cfg.CreatureContact != 0)
            {
                int n = Candidates(row, g, cand, gl, gr, cfg, true);

                for (int k = 0; k < n; k++)
                {
                    int key = cand[k];
                    int rank = key / LinkStride;
                    int og = gl.RankToSlot[rank];
                    if (og < 0 || s.Active[og] == 0) continue;

                    int j = key - rank * LinkStride;
                    int orow = og * LinkStride + j;

                    Real bx = ShortWay(cx, s.LCx[orow], cfg.SeamL);
                    Real by = cy - s.LCy[orow];
                    Real bz = ShortWay(cz, s.LCz[orow], cfg.SeamW);
                    if (CarryBase)
                    {
                        // The float centres' difference, near each other so exact, then the rests'.
                        bx = bx + (ex - s.LEx[orow]);
                        by = by + (ey - s.LEy[orow]);
                        bz = bz + (ez - s.LEz[orow]);
                    }
                    Real distance = System.Math.Sqrt(bx * bx + by * by + bz * bz);
                    Real penetration = radius + s.LR[orow] - distance;
                    if (penetration <= 0) continue;

                    if (cfg.AtPoint != 0)
                    {
                        // D228 level (b): ContactTrueShape.Apply's pair at the contact point. The pair is found as at
                        // the level below (the same cores in the same frame) and hands back its contact point; the
                        // lever to it from this centre and from the other's, and each side's velocity at it, are read
                        // in that frame. The push is today's law along the pair's normal; its torque r x F is capped
                        // by the grip's cap and summed.
                        V3 aNormal, aLever, aOtherLever;
                        Real aPenetration;
                        if (!TrueShapePairAt(s, gl, simplex, g, i, og, j, bx, by, bz, radius, s.LR[orow],
                                             out aNormal, out aPenetration, out aLever, out aOtherLever))
                        {
                            continue;
                        }

                        if (instrument)
                        {
                            if (staged < StageCap)
                            {
                                gl.StageId[staged * R + row] = gl.Id[og];
                                gl.StageSlot[staged * R + row] = og;
                                gl.StagePart[staged * R + row] = j;
                            }
                            staged++;
                        }

                        Real aOtherMass = gl.LinkMass[orow];
                        Real aReduced = m * aOtherMass / (m + aOtherMass);

                        Real aStiffness = aReduced * cfg.Omega * cfg.Omega;
                        Real aDamping = (Real)2.0 * cfg.Zeta * aReduced * cfg.Omega;

                        V3 aThisVelocity = new V3(vx, vy, vz) + V3.Cross(pSpin, aLever);
                        V3 aOtherSpin = new V3(s.LSp0[orow], s.LSp1[orow], s.LSp2[orow]);
                        V3 aOtherVelocity = new V3(s.LVx[orow], s.LVy[orow], s.LVz[orow]) + V3.Cross(aOtherSpin, aOtherLever);
                        Real aApproach = V3.Dot(aThisVelocity - aOtherVelocity, aNormal);

                        Real aPush = PairPush(aStiffness * aPenetration - aDamping * aApproach, aReduced, aApproach, cfg);
                        V3 aForce = aNormal * aPush;

                        fx = fx + aForce.X;
                        fy = fy + aForce.Y;
                        fz = fz + aForce.Z;

                        V3 aTorque = CapTorque(V3.Cross(aLever, aForce), pInertia, radius, cfg);
                        qx = qx + aTorque.X;
                        qy = qy + aTorque.Y;
                        qz = qz + aTorque.Z;
                        continue;
                    }

                    if (cfg.TrueShape != 0)
                    {
                        // D228: the sphere test above is the broadphase. The pair touches when its cores,
                        // swept by their margins, overlap (ContactTrueShape.Apply's pair loop); the force law
                        // is today's, on that penetration and along that normal.
                        Real tnx, tny, tnz, tpen;
                        if (!TrueShapePair(s, gl, simplex, g, i, og, j, bx, by, bz, radius, s.LR[orow],
                                           out tnx, out tny, out tnz, out tpen))
                        {
                            continue;
                        }

                        if (instrument)
                        {
                            if (staged < StageCap)
                            {
                                gl.StageId[staged * R + row] = gl.Id[og];
                                gl.StageSlot[staged * R + row] = og;
                                gl.StagePart[staged * R + row] = j;
                            }
                            staged++;
                        }

                        Real tOtherMass = gl.LinkMass[orow];
                        Real tReduced = m * tOtherMass / (m + tOtherMass);

                        Real tStiffness = tReduced * cfg.Omega * cfg.Omega;
                        Real tDamping = (Real)2.0 * cfg.Zeta * tReduced * cfg.Omega;

                        Real tdvx = vx - s.LVx[orow];
                        Real tdvy = vy - s.LVy[orow];
                        Real tdvz = vz - s.LVz[orow];
                        Real tApproach = tdvx * tnx + tdvy * tny + tdvz * tnz;

                        Real tp = PairPush(tStiffness * tpen - tDamping * tApproach, tReduced, tApproach, cfg);

                        fx = fx + tnx * tp;
                        fy = fy + tny * tp;
                        fz = fz + tnz * tp;
                        continue;
                    }

                    if (instrument)
                    {
                        if (staged < StageCap)
                        {
                            gl.StageId[staged * R + row] = gl.Id[og];
                            gl.StageSlot[staged * R + row] = og;
                            gl.StagePart[staged * R + row] = j;
                        }
                        staged++;
                    }

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

            if (cfg.AtPoint != 0)
            {
                // D228 level (b): the world's surfaces at every vertex of the core (ContactTrueShape.Apply with atPoint),
                // the touches' torque summed beside the force, then the tail below with the torque kept as well.
                V3 aHalf = new V3(gl.CoreHx[row], gl.CoreHy[row], gl.CoreHz[row]);
                M3 aRotation = new M3(
                    s.LRot0[row], s.LRot1[row], s.LRot2[row],
                    s.LRot3[row], s.LRot4[row], s.LRot5[row],
                    s.LRot6[row], s.LRot7[row], s.LRot8[row]);
                Real aMargin = gl.CoreM[row];

                // The link's principal inertias in its own axes (Creature.InertiaLocal), which the vertices' effective mass reads.
                V3 aInertia = new V3(b.Inertia[3 * i * N + bi], b.Inertia[(3 * i + 1) * N + bi], b.Inertia[(3 * i + 2) * N + bi]);

                Real aBedStiffness = m * cfg.Omega * cfg.Omega;
                Real aBedDamping = (Real)2.0 * cfg.Zeta * m * cfg.Omega;

                Real aKept = 0, aNormalX = 0, aNormalY = 0, aNormalZ = 0, aTouchX = 0, aTouchY = 0, aTouchZ = 0;
                AgainstTheWorldPoint(cx, cy, cz, radius, ex, ey, ez, vx, vy, vz, m, aBedStiffness, aBedDamping,
                                     aHalf, aRotation, aMargin, pSpin, aInertia, pInertia, w, cfg,
                                     ref fx, ref fy, ref fz, ref qx, ref qy, ref qz, ref bedGlass, cfg.RockTouch,
                                     ref aKept, ref aNormalX, ref aNormalY, ref aNormalZ,
                                     ref aTouchX, ref aTouchY, ref aTouchZ);

                // D162: the touch this step, and where: the deepest vertex's sphere's deepest point along the normal.
                if (cfg.RockTouch != 0)
                {
                    int aTouched = aKept > 0 ? 1 : 0;
                    gl.TouchNew[row] = aTouched;
                    if (aTouched != 0)
                    {
                        gl.TouchAt[row] = aTouchX;
                        gl.TouchAt[R + row] = aTouchY;
                        gl.TouchAt[2 * R + row] = aTouchZ;
                        gl.TouchAt[3 * R + row] = aNormalX;
                        gl.TouchAt[4 * R + row] = aNormalY;
                        gl.TouchAt[5 * R + row] = aNormalZ;
                    }
                }

                BodyPush(ref fx, ref fy, ref fz, m, cfg);

                int aFlags = bedGlass != 0 ? 1 : 0;
                if (fx != 0 || fy != 0 || fz != 0)
                {
                    aFlags |= 2;
                    if (links == 1)
                    {
                        // A one-link body's force is spread by m (1/M), its torque likewise; BodyPush bounds the force
                        // and the cap bounded each torque already, and the one does not scale the other.
                        Real aShare = m * ((Real)1.0 / totalMass);
                        fx = fx * aShare;
                        fy = fy * aShare;
                        fz = fz * aShare;
                        qx = qx * aShare;
                        qy = qy * aShare;
                        qz = qz * aShare;
                    }
                }

                gl.LinkPush[row] = fx;
                gl.LinkPush[R + row] = fy;
                gl.LinkPush[2 * R + row] = fz;
                gl.LinkPush[3 * R + row] = qx;
                gl.LinkPush[4 * R + row] = qy;
                gl.LinkPush[5 * R + row] = qz;
                gl.LinkFlags[row] = aFlags;
                if (instrument) gl.StageN[row] = staged;
                return;
            }

            if (cfg.TrueShape != 0)
            {
                // D228: the world's surfaces on the core's support point (ContactTrueShape.Apply), then the
                // tail below, written out for it so that the recorded path's lines are left as they are.
                V3 tHalf = new V3(gl.CoreHx[row], gl.CoreHy[row], gl.CoreHz[row]);
                M3 tRotation = new M3(
                    s.LRot0[row], s.LRot1[row], s.LRot2[row],
                    s.LRot3[row], s.LRot4[row], s.LRot5[row],
                    s.LRot6[row], s.LRot7[row], s.LRot8[row]);
                Real tMargin = gl.CoreM[row];

                Real tBedStiffness = m * cfg.Omega * cfg.Omega;
                Real tBedDamping = (Real)2.0 * cfg.Zeta * m * cfg.Omega;

                Real tKept = 0, tnx = 0, tny = 0, tnz = 0, tpx = 0, tpy = 0, tpz = 0;
                AgainstTheWorldTrue(cx, cy, cz, radius, ex, ey, ez, vx, vy, vz, m, tBedStiffness, tBedDamping,
                                    tHalf, tRotation, tMargin, w, cfg,
                                    ref fx, ref fy, ref fz, ref bedGlass, cfg.RockTouch,
                                    ref tKept, ref tnx, ref tny, ref tnz, ref tpx, ref tpy, ref tpz);

                // D162: the touch this step, and where: the support sphere's deepest point along the normal.
                if (cfg.RockTouch != 0)
                {
                    int tTouched = tKept > 0 ? 1 : 0;
                    gl.TouchNew[row] = tTouched;
                    if (tTouched != 0)
                    {
                        gl.TouchAt[row] = tpx;
                        gl.TouchAt[R + row] = tpy;
                        gl.TouchAt[2 * R + row] = tpz;
                        gl.TouchAt[3 * R + row] = tnx;
                        gl.TouchAt[4 * R + row] = tny;
                        gl.TouchAt[5 * R + row] = tnz;
                    }
                }

                BodyPush(ref fx, ref fy, ref fz, m, cfg);

                int tFlags = bedGlass != 0 ? 1 : 0;
                if (fx != 0 || fy != 0 || fz != 0)
                {
                    tFlags |= 2;
                    if (links == 1)
                    {
                        Real tShare = m * ((Real)1.0 / totalMass);
                        fx = fx * tShare;
                        fy = fy * tShare;
                        fz = fz * tShare;
                    }
                }

                gl.LinkPush[row] = fx;
                gl.LinkPush[R + row] = fy;
                gl.LinkPush[2 * R + row] = fz;
                gl.LinkFlags[row] = tFlags;
                if (instrument) gl.StageN[row] = staged;
                return;
            }

            Real bedStiffness = m * cfg.Omega * cfg.Omega;
            Real bedDamping = (Real)2.0 * cfg.Zeta * m * cfg.Omega;

            Real rockKept = 0, rnx = 0, rny = 0, rnz = 0;
            AgainstTheWorld(cx, cy, cz, radius, ex, ey, ez, vx, vy, vz, m, bedStiffness, bedDamping, w, cfg,
                            ref fx, ref fy, ref fz, ref bedGlass, cfg.RockTouch, ref rockKept, ref rnx, ref rny, ref rnz);

            // D162: the touch this step, and where: the sphere's deepest point along the normal.
            if (cfg.RockTouch != 0)
            {
                int touched = rockKept > 0 ? 1 : 0;
                gl.TouchNew[row] = touched;
                if (touched != 0)
                {
                    gl.TouchAt[row] = cx - rnx * radius;
                    gl.TouchAt[R + row] = cy - rny * radius;
                    gl.TouchAt[2 * R + row] = cz - rnz * radius;
                    gl.TouchAt[3 * R + row] = rnx;
                    gl.TouchAt[4 * R + row] = rny;
                    gl.TouchAt[5 * R + row] = rnz;
                }
            }

            BodyPush(ref fx, ref fy, ref fz, m, cfg);

            int flags = bedGlass != 0 ? 1 : 0;
            if (fx != 0 || fy != 0 || fz != 0)
            {
                flags |= 2;
                if (links == 1)
                {
                    Real share = m * ((Real)1.0 / totalMass);
                    fx = fx * share;
                    fy = fy * share;
                    fz = fz * share;
                }
            }

            gl.LinkPush[row] = fx;
            gl.LinkPush[R + row] = fy;
            gl.LinkPush[2 * R + row] = fz;
            gl.LinkFlags[row] = flags;
            if (instrument) gl.StageN[row] = staged;
        }

        /// <summary>
        /// One sphere against the bed (flat, or ContactBed on the height map), the reefs
        /// (ContactReef) and the glass, in Contacts.Apply's order, each counted with the bed and
        /// the glass.
        /// </summary>
        private static void AgainstTheWorld(
            Real cx, Real cy, Real cz, Real r, Real ex, Real ey, Real ez, Real vx, Real vy, Real vz, Real m,
            Real bedStiffness, Real bedDamping, SWorld w, SCfg cfg,
            ref Real fx, ref Real fy, ref Real fz, ref int bedGlass,
            int noteRock, ref Real rockKept, ref Real rnx, ref Real rny, ref Real rnz)
        {
            if (cfg.HasBed == 0)
            {
                Real below = CarryBase ? ((-cfg.WorldDepth - cy) - ey) + r : -cfg.WorldDepth - (cy - r);
                if (below > 0)
                {
                    Real up = PairPush(bedStiffness * below - bedDamping * vy, m, vy, cfg);
                    fx = fx + 0;
                    fy = fy + up;
                    fz = fz + 0;
                    bedGlass = 1;
                    if (noteRock != 0 && up > 0) NoteRock(0, up, 0, ref rockKept, ref rnx, ref rny, ref rnz);
                }
            }
            else
            {
                Real hgt, gx, gz, pen, slope;
                if (CarryBase)
                {
                    // The floor at a lattice point, rounded once for its square, and the small rise from there
                    // to the centre's true x and z, rest included.
                    Real rise;
                    BedHeightNear(cx, cz, ex, ez, w, cfg, out hgt, out rise, out gx, out gz);
                    Real floorY = -cfg.BedDepth + hgt;
                    slope = System.Math.Sqrt((Real)1.0 + gx * gx + gz * gz);
                    pen = r - ((cy - floorY) + (ey - rise)) / slope;
                }
                else
                {
                    BedHeightAndGradient(cx, cz, w, cfg, out hgt, out gx, out gz);
                    Real floorY = -cfg.BedDepth + hgt;
                    slope = System.Math.Sqrt((Real)1.0 + gx * gx + gz * gz);
                    pen = r - (cy - floorY) / slope;
                }

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
                    if (noteRock != 0) NoteRock(rx, ry, rz, ref rockKept, ref rnx, ref rny, ref rnz);
                }
            }

            if (cfg.ReefCount != 0 || cfg.CoralCount != 0)
            {
                // ContactReef: the coral's distance (CoralRock.SignedDistance), or the reefs'.
                SD at;
                Real distance = cfg.CoralCount != 0
                    ? CoralSignedDistance(cx, cy, cz, r, w, cfg, out at)
                    : ReefSignedDistance(cx, cy, cz, r, w, cfg, out at);
                Real penetration = r - distance;
                if (CarryBase)
                {
                    // The rest moves the centre along the reef's gradient: first order is enough at microns.
                    Real glen = System.Math.Sqrt(at.Gx * at.Gx + at.Gy * at.Gy + at.Gz * at.Gz);
                    if (glen > (Real)1e-12) penetration = penetration - (at.Gx * ex + at.Gy * ey + at.Gz * ez) / glen;
                }

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
                    if (noteRock != 0) NoteRock(rx, ry, rz, ref rockKept, ref rnx, ref rny, ref rnz);
                }
            }

            if (cfg.TankRadius > 0)
            {
                Real x = cx - cfg.AxisX;
                Real z = cz - cfg.AxisZ;
                Real radial = System.Math.Sqrt(x * x + z * z);
                Real outside = radial + r - cfg.TankRadius;

                // In single: the radius at a lattice point, rounded once for its square, and the small change
                // from there to the centre's true x and z, to second order.
                if (CarryBase) outside = GlassOutsideNear(cx, cz, ex, ez, r, cfg);

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

        // ============================================================== D228: contact on the true shape
        //
        // ContactTrueShape on the card, behind cfg.TrueShape. A part is a convex core, a box with any
        // half-extent 0, swept by a ball of its margin (ConvexCore.Of); a pair is tested by GJK on the two
        // cores, and a surface by the core's support point toward it. The sphere test stays the broadphase
        // and the early-out. ConvexCore.Distance and Support are transcribed here operation for operation,
        // with a local array for the simplex in place of stackalloc, so that in double the accelerator is
        // the solver to the bit; in single it is the same code in single precision. A change to ConvexCore
        // is therefore made here in the same change.

        public const int GjkSimplex = 40;
        private const int GjkMaxIterations = 32;
        private const Real GjkTolerance = (Real)1e-10;
        private const Real GjkTouching = (Real)1e-9;

        // The simplex buffer: the difference's points, then A's, then B's, three reals a vertex, four
        // vertices; then the four weights of the nearest point.
        private const int GjkW = 0, GjkA = 12, GjkB = 24, GjkL = 36;

        /// <summary>ConvexCore.Support: the core's farthest point along a direction. A local component that is 0 or NaN reads as +.</summary>
        private static V3 GjkSupport(V3 half, M3 rotation, V3 centre, V3 direction)
        {
            V3 local = rotation.TransposedTimes(direction);

            V3 corner = new V3(
                local.X < 0 ? -half.X : half.X,
                local.Y < 0 ? -half.Y : half.Y,
                local.Z < 0 ? -half.Z : half.Z);

            return centre + rotation * corner;
        }

        private static V3 GjkVertex(Real[] s, int block, int k) =>
            new V3(s[block + 3 * k], s[block + 3 * k + 1], s[block + 3 * k + 2]);

        private static void GjkPut(Real[] s, int block, int k, V3 p)
        {
            s[block + 3 * k] = p.X;
            s[block + 3 * k + 1] = p.Y;
            s[block + 3 * k + 2] = p.Z;
        }

        private static void GjkAddVertex(Real[] s, ref int n, V3 pa, V3 pb)
        {
            GjkPut(s, GjkA, n, pa);
            GjkPut(s, GjkB, n, pb);
            GjkPut(s, GjkW, n, pa - pb);
            n++;
        }

        private static bool GjkHeld(Real[] s, int n, V3 w)
        {
            for (int k = 0; k < n; k++)
            {
                if (s[GjkW + 3 * k] == w.X && s[GjkW + 3 * k + 1] == w.Y && s[GjkW + 3 * k + 2] == w.Z) return true;
            }
            return false;
        }

        /// <summary>
        /// ConvexCore.Distance: the distance between two cores and the nearest point of each, or false when
        /// they intersect or touch.
        /// </summary>
        private static bool GjkDistance(
            V3 halfA, M3 rotationA, V3 centreA,
            V3 halfB, M3 rotationB, V3 centreB,
            Real[] s, out Real distance, out V3 pointA, out V3 pointB)
        {
            // Each core contains its own centre, so coincident centres are an intersection.
            V3 first = centreB - centreA;
            if (first.SqrMagnitude <= GjkTouching * GjkTouching)
            {
                distance = 0;
                pointA = centreA;
                pointB = centreB;
                return false;
            }

            int n = 0;
            GjkAddVertex(s, ref n,
                GjkSupport(halfA, rotationA, centreA, first),
                GjkSupport(halfB, rotationB, centreB, -first));

            s[GjkL] = (Real)1.0;
            V3 v = GjkVertex(s, GjkW, 0);
            bool inside = false;

            int pass = 0;
            for (; pass < GjkMaxIterations; pass++)
            {
                if (!GjkReduce(s, ref n, out v)) { inside = true; break; }

                Real vv = v.SqrMagnitude;
                if (vv <= GjkTouching * GjkTouching) break;

                // The difference's support point along -v: A's along -v, B's along +v.
                V3 wa = GjkSupport(halfA, rotationA, centreA, -v);
                V3 wb = GjkSupport(halfB, rotationB, centreB, v);
                V3 w = wa - wb;

                // No progress possible: the support point is a vertex already held, or the gap is closed.
                if (vv - V3.Dot(v, w) <= GjkTolerance * System.Math.Sqrt(vv)) break;
                if (GjkHeld(s, n, w)) break;

                GjkAddVertex(s, ref n, wa, wb);
            }

            // The cap was reached with a vertex added and not yet reduced: reduce it, so the weights are current.
            if (pass == GjkMaxIterations && !GjkReduce(s, ref n, out v)) inside = true;

            if (inside)
            {
                distance = 0;
                pointA = centreA;
                pointB = centreB;
                return false;
            }

            // The nearest points are the weights' combination of A's and B's.
            V3 pa = V3.Zero, pb = V3.Zero;
            for (int k = 0; k < n; k++)
            {
                Real lam = s[GjkL + k];
                pa = pa + GjkVertex(s, GjkA, k) * lam;
                pb = pb + GjkVertex(s, GjkB, k) * lam;
            }

            pointA = pa;
            pointB = pb;
            distance = (pa - pb).Magnitude;
            return distance > GjkTouching;
        }

        /// <summary>
        /// ConvexCore.Reduce: the point of the simplex's hull nearest the origin, its weights written, every
        /// vertex it does not use dropped. False when the simplex is a tetrahedron holding the origin.
        /// </summary>
        private static bool GjkReduce(Real[] s, ref int n, out V3 v)
        {
            if (n == 1)
            {
                s[GjkL] = (Real)1.0;
            }
            else if (n == 2)
            {
                Real a, b;
                GjkSegment(GjkVertex(s, GjkW, 0), GjkVertex(s, GjkW, 1), out a, out b);
                s[GjkL] = a;
                s[GjkL + 1] = b;
            }
            else if (n == 3)
            {
                Real a, b, c;
                GjkTriangle(GjkVertex(s, GjkW, 0), GjkVertex(s, GjkW, 1), GjkVertex(s, GjkW, 2),
                    out a, out b, out c);
                s[GjkL] = a;
                s[GjkL + 1] = b;
                s[GjkL + 2] = c;
            }
            else
            {
                if (!GjkTetrahedron(s)) { v = V3.Zero; return false; }
            }

            // Keep the vertices the nearest point uses, in order.
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                Real lam = s[GjkL + k];
                if (!(lam > 0)) continue;

                if (m != k)
                {
                    GjkPut(s, GjkW, m, GjkVertex(s, GjkW, k));
                    GjkPut(s, GjkA, m, GjkVertex(s, GjkA, k));
                    GjkPut(s, GjkB, m, GjkVertex(s, GjkB, k));
                    s[GjkL + m] = lam;
                }
                m++;
            }
            n = m;

            V3 sum = V3.Zero;
            for (int k = 0; k < n; k++) sum = sum + GjkVertex(s, GjkW, k) * s[GjkL + k];
            v = sum;
            return true;
        }

        /// <summary>ConvexCore.Segment: the weights of the point of segment a-b nearest the origin.</summary>
        private static void GjkSegment(V3 a, V3 b, out Real la, out Real lb)
        {
            V3 ab = b - a;
            Real len2 = V3.Dot(ab, ab);
            Real t = len2 > 0 ? -V3.Dot(a, ab) / len2 : (Real)0.0;

            if (!(t > 0)) { la = (Real)1.0; lb = (Real)0.0; }
            else if (t >= (Real)1.0) { la = (Real)0.0; lb = (Real)1.0; }
            else { la = (Real)1.0 - t; lb = t; }
        }

        /// <summary>ConvexCore.Triangle: the weights of the point of triangle a-b-c nearest the origin (Ericson).</summary>
        private static void GjkTriangle(V3 a, V3 b, V3 c, out Real la, out Real lb, out Real lc)
        {
            V3 ab = b - a, ac = c - a;
            V3 normal = V3.Cross(ab, ac);

            if (normal.SqrMagnitude <= (Real)1e-20 * V3.Dot(ab, ab) * V3.Dot(ac, ac) || !(V3.Dot(ab, ab) > 0))
            {
                GjkEdges(a, b, c, out la, out lb, out lc);
                return;
            }

            // The origin's offsets from a, b and c.
            Real d1 = -V3.Dot(ab, a), d2 = -V3.Dot(ac, a);
            if (d1 <= 0 && d2 <= 0) { la = 1; lb = 0; lc = 0; return; }

            Real d3 = -V3.Dot(ab, b), d4 = -V3.Dot(ac, b);
            if (d3 >= 0 && d4 <= d3) { la = 0; lb = 1; lc = 0; return; }

            Real vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0)
            {
                Real t = d1 / (d1 - d3);
                la = 1 - t; lb = t; lc = 0;
                return;
            }

            Real d5 = -V3.Dot(ab, c), d6 = -V3.Dot(ac, c);
            if (d6 >= 0 && d5 <= d6) { la = 0; lb = 0; lc = 1; return; }

            Real vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0)
            {
                Real t = d2 / (d2 - d6);
                la = 1 - t; lb = 0; lc = t;
                return;
            }

            Real va = d3 * d6 - d5 * d4;
            if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
            {
                Real t = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                la = 0; lb = 1 - t; lc = t;
                return;
            }

            Real denom = (Real)1.0 / (va + vb + vc);
            Real bv = vb * denom, cv = vc * denom;
            la = (Real)1.0 - bv - cv; lb = bv; lc = cv;
        }

        /// <summary>ConvexCore.Edges: the nearest point of a triangle taken on its three edges, in the order ab, bc, ca.</summary>
        private static void GjkEdges(V3 a, V3 b, V3 c, out Real la, out Real lb, out Real lc)
        {
            Real p, q;
            GjkSegment(a, b, out p, out q);
            Real best = (a * p + b * q).SqrMagnitude;
            la = p; lb = q; lc = 0;

            GjkSegment(b, c, out p, out q);
            Real d = (b * p + c * q).SqrMagnitude;
            if (d < best) { best = d; la = 0; lb = p; lc = q; }

            GjkSegment(c, a, out p, out q);
            d = (c * p + a * q).SqrMagnitude;
            if (d < best) { la = q; lb = 0; lc = p; }
        }

        /// <summary>
        /// ConvexCore.Tetrahedron: the weights of the nearest point of the tetrahedron in the buffer's four
        /// vertices; false when the origin is inside it.
        /// </summary>
        private static bool GjkTetrahedron(Real[] s)
        {
            V3 p0 = GjkVertex(s, GjkW, 0), p1 = GjkVertex(s, GjkW, 1), p2 = GjkVertex(s, GjkW, 2), p3 = GjkVertex(s, GjkW, 3);

            Real best = Real.PositiveInfinity;
            Real w0 = 0, w1 = 0, w2 = 0, w3 = 0;
            bool any = false;

            // Faces (0,1,2) | 3, (0,2,3) | 1, (0,3,1) | 2, (1,3,2) | 0.
            for (int face = 0; face < 4; face++)
            {
                V3 a, b, c, d;
                if (face == 0) { a = p0; b = p1; c = p2; d = p3; }
                else if (face == 1) { a = p0; b = p2; c = p3; d = p1; }
                else if (face == 2) { a = p0; b = p3; c = p1; d = p2; }
                else { a = p1; b = p3; c = p2; d = p0; }

                V3 normal = V3.Cross(b - a, c - a);
                Real signOrigin = -V3.Dot(a, normal);
                Real signFourth = V3.Dot(d - a, normal);
                if (signOrigin * signFourth > 0) continue;

                Real la, lb, lc;
                GjkTriangle(a, b, c, out la, out lb, out lc);
                Real dist = (a * la + b * lb + c * lc).SqrMagnitude;
                if (!(dist < best))
                {
                    any = true;
                    continue;
                }

                best = dist;
                any = true;
                if (face == 0) { w0 = la; w1 = lb; w2 = lc; w3 = 0; }
                else if (face == 1) { w0 = la; w1 = 0; w2 = lb; w3 = lc; }
                else if (face == 2) { w0 = la; w1 = lc; w2 = 0; w3 = lb; }
                else { w0 = 0; w1 = la; w2 = lc; w3 = lb; }
            }

            if (!any) return false;

            s[GjkL] = w0;
            s[GjkL + 1] = w1;
            s[GjkL + 2] = w2;
            s[GjkL + 3] = w3;
            return true;
        }

        /// <summary>
        /// ContactTrueShape.Pair on the link pair (this link g/i, the other og/j), computed with the one of
        /// the lower (body id, link) as the first core, as the solver does: the first core at the origin and
        /// the second at minus the sphere test's difference, so no absolute coordinate enters GJK. (bx, by, bz)
        /// is that difference as ContactLinks computed it, this link's centre less the other's. The normal
        /// returned is this link's; False when the penetration is not positive.
        /// </summary>
        private static bool TrueShapePair(
            SSph s, SGlob gl, Real[] simplex, int g, int i, int og, int j,
            Real bx, Real by, Real bz, Real radius, Real otherRadius,
            out Real nx, out Real ny, out Real nz, out Real penetration)
        {
            int row = g * LinkStride + i;
            int orow = og * LinkStride + j;

            bool firstIsThis = gl.Id[g] < gl.Id[og] || (gl.Id[g] == gl.Id[og] && i < j);
            int rowA = firstIsThis ? row : orow;
            int rowB = firstIsThis ? orow : row;
            Real radiusA = firstIsThis ? radius : otherRadius;
            Real radiusB = firstIsThis ? otherRadius : radius;

            // A less B, as the host reads the sphere test in the canonical order: the negation of this less other.
            V3 between = firstIsThis ? new V3(bx, by, bz) : new V3(-bx, -by, -bz);

            V3 centreA = V3.Zero;
            V3 centreB = V3.Zero - between;

            V3 halfA = new V3(gl.CoreHx[rowA], gl.CoreHy[rowA], gl.CoreHz[rowA]);
            V3 halfB = new V3(gl.CoreHx[rowB], gl.CoreHy[rowB], gl.CoreHz[rowB]);
            Real marginA = gl.CoreM[rowA];
            Real marginB = gl.CoreM[rowB];
            M3 rotationA = new M3(
                s.LRot0[rowA], s.LRot1[rowA], s.LRot2[rowA],
                s.LRot3[rowA], s.LRot4[rowA], s.LRot5[rowA],
                s.LRot6[rowA], s.LRot7[rowA], s.LRot8[rowA]);
            M3 rotationB = new M3(
                s.LRot0[rowB], s.LRot1[rowB], s.LRot2[rowB],
                s.LRot3[rowB], s.LRot4[rowB], s.LRot5[rowB],
                s.LRot6[rowB], s.LRot7[rowB], s.LRot8[rowB]);

            V3 normalAB;
            V3 pairPoint;   // kept as the host keeps it, unapplied

            Real d;
            V3 pa, pb;
            if (GjkDistance(halfA, rotationA, centreA, halfB, rotationB, centreB, simplex, out d, out pa, out pb))
            {
                // Apart: along the closest points, from B toward A.
                V3 gap = pa - pb;
                normalAB = gap * ((Real)1.0 / d);
                penetration = marginA + marginB - d;

                // Each surface point is the closest core point moved out by its margin toward the other.
                pairPoint = ((pa - normalAB * marginA) + (pb + normalAB * marginB)) * (Real)0.5;
            }
            else
            {
                // The cores meet: the centres' difference, as today, and a push that never weakens as the pair
                // goes deeper than the margins' sum.
                V3 deepBetween = centreA - centreB;
                Real deepDistance = deepBetween.Magnitude;
                normalAB = deepDistance > (Real)1e-9 ? deepBetween * ((Real)1.0 / deepDistance) : new V3(0, 1, 0);

                Real sum = marginA + marginB;
                Real deep = radiusA + radiusB - deepDistance;
                penetration = deep > sum ? deep : sum;

                // The point between the centres at the ratio of the margins.
                Real along = sum > 0 ? marginA / sum : (Real)0.5;
                pairPoint = centreA - deepBetween * along;
            }

            V3 normal = firstIsThis ? normalAB : -normalAB;
            nx = normal.X; ny = normal.Y; nz = normal.Z;
            return penetration > 0;
        }

        /// <summary>
        /// <see cref="TrueShapePair"/> for D228's level (b): the same pair, in the same frame, with the same
        /// arithmetic, and the contact point it keeps handed back as two levers, this link's and the other's. Each
        /// is the point less that side's centre in the pair's frame (the first core at the origin, the second at
        /// minus the sphere test's difference), so the same four numbers whichever of the two does the work
        /// (ContactTrueShape.Apply: <c>lever</c> and <c>otherLever</c>). A copy rather than a change to
        /// <see cref="TrueShapePair"/>, so that the kernels of the other two values of the switch keep their lines.
        /// </summary>
        private static bool TrueShapePairAt(
            SSph s, SGlob gl, Real[] simplex, int g, int i, int og, int j,
            Real bx, Real by, Real bz, Real radius, Real otherRadius,
            out V3 normal, out Real penetration, out V3 lever, out V3 otherLever)
        {
            int row = g * LinkStride + i;
            int orow = og * LinkStride + j;

            bool firstIsThis = gl.Id[g] < gl.Id[og] || (gl.Id[g] == gl.Id[og] && i < j);
            int rowA = firstIsThis ? row : orow;
            int rowB = firstIsThis ? orow : row;
            Real radiusA = firstIsThis ? radius : otherRadius;
            Real radiusB = firstIsThis ? otherRadius : radius;

            // A less B, as the host reads the sphere test in the canonical order: the negation of this less other.
            V3 between = firstIsThis ? new V3(bx, by, bz) : new V3(-bx, -by, -bz);

            V3 centreA = V3.Zero;
            V3 centreB = V3.Zero - between;

            V3 halfA = new V3(gl.CoreHx[rowA], gl.CoreHy[rowA], gl.CoreHz[rowA]);
            V3 halfB = new V3(gl.CoreHx[rowB], gl.CoreHy[rowB], gl.CoreHz[rowB]);
            Real marginA = gl.CoreM[rowA];
            Real marginB = gl.CoreM[rowB];
            M3 rotationA = new M3(
                s.LRot0[rowA], s.LRot1[rowA], s.LRot2[rowA],
                s.LRot3[rowA], s.LRot4[rowA], s.LRot5[rowA],
                s.LRot6[rowA], s.LRot7[rowA], s.LRot8[rowA]);
            M3 rotationB = new M3(
                s.LRot0[rowB], s.LRot1[rowB], s.LRot2[rowB],
                s.LRot3[rowB], s.LRot4[rowB], s.LRot5[rowB],
                s.LRot6[rowB], s.LRot7[rowB], s.LRot8[rowB]);

            V3 normalAB;
            V3 pairPoint;

            Real d;
            V3 pa, pb;
            if (GjkDistance(halfA, rotationA, centreA, halfB, rotationB, centreB, simplex, out d, out pa, out pb))
            {
                // Apart: along the closest points, from B toward A.
                V3 gap = pa - pb;
                normalAB = gap * ((Real)1.0 / d);
                penetration = marginA + marginB - d;

                // Each surface point is the closest core point moved out by its margin toward the other.
                pairPoint = ((pa - normalAB * marginA) + (pb + normalAB * marginB)) * (Real)0.5;
            }
            else
            {
                // The cores meet: the centres' difference, as today, and a push that never weakens as the pair
                // goes deeper than the margins' sum.
                V3 deepBetween = centreA - centreB;
                Real deepDistance = deepBetween.Magnitude;
                normalAB = deepDistance > (Real)1e-9 ? deepBetween * ((Real)1.0 / deepDistance) : new V3(0, 1, 0);

                Real sum = marginA + marginB;
                Real deep = radiusA + radiusB - deepDistance;
                penetration = deep > sum ? deep : sum;

                // The point between the centres at the ratio of the margins.
                Real along = sum > 0 ? marginA / sum : (Real)0.5;
                pairPoint = centreA - deepBetween * along;
            }

            normal = firstIsThis ? normalAB : -normalAB;

            // Each side's centre in the pair's frame: A at the origin, B at minus the difference.
            V3 minusBetween = V3.Zero - between;
            lever = pairPoint - (firstIsThis ? V3.Zero : minusBetween);
            otherLever = pairPoint - (firstIsThis ? minusBetween : V3.Zero);
            return penetration > 0;
        }

        /// <summary>
        /// A core's support point along a direction as a sphere's centre, a float and its rest: in double the
        /// host's centre plus the turned corner; in single the float centre moved by the corner with the
        /// rounding of the sum moved into the rest, so the rest stays at the size the world's samplers
        /// (BedHeightNear, GlassOutsideNear) are written for.
        /// </summary>
        private static void SupportSphere(
            V3 half, M3 rotation, Real cx, Real cy, Real cz, Real ex, Real ey, Real ez, V3 direction,
            out Real sx, out Real sy, out Real sz, out Real sex, out Real sey, out Real sez)
        {
            V3 at = GjkSupport(half, rotation, CarryBase ? V3.Zero : new V3(cx, cy, cz), direction);

            if (CarryBase)
            {
                // Knuth's two-sum, per axis: s + err is cx + at.X exactly.
                sx = cx + at.X; Real bbx = sx - cx; sex = ex + ((cx - (sx - bbx)) + (at.X - bbx));
                sy = cy + at.Y; Real bby = sy - cy; sey = ey + ((cy - (sy - bby)) + (at.Y - bby));
                sz = cz + at.Z; Real bbz = sz - cz; sez = ez + ((cz - (sz - bbz)) + (at.Z - bbz));
            }
            else
            {
                sx = at.X; sy = at.Y; sz = at.Z;
                sex = ex; sey = ey; sez = ez;
            }
        }

        /// <summary>
        /// ContactTrueShape's NoteRock: one push from the rock, kept where it is larger than any this link has
        /// had this step, with its direction as the rock's normal and the support sphere's deepest point along
        /// it as the touch.
        /// </summary>
        private static void NoteRockAt(
            Real x, Real y, Real z, Real sx, Real sy, Real sz, Real margin,
            ref Real kept, ref Real nx, ref Real ny, ref Real nz, ref Real px, ref Real py, ref Real pz)
        {
            Real size = System.Math.Sqrt(x * x + y * y + z * z);
            if (!(size > kept)) return;

            kept = size;
            Real inv = (Real)1.0 / size;
            nx = x * inv; ny = y * inv; nz = z * inv;
            px = sx - nx * margin; py = sy - ny * margin; pz = sz - nz * margin;
        }

        /// <summary>
        /// ContactTrueShape's four surfaces on one link, in Contacts.ApplyPerPart's order: the bed (flat, or
        /// ContactBed on the height map), the reefs and the coral, and the glass. Each takes the link's bounding
        /// sphere (centre, rest and radius r) as the early-out, then the direction to the surface, then the core's
        /// support point along it, and runs the recorded routine for that surface on a sphere of the margin's radius
        /// there. The contact point of the host's routines is not kept: nothing applies a force there.
        /// </summary>
        private static void AgainstTheWorldTrue(
            Real cx, Real cy, Real cz, Real r, Real ex, Real ey, Real ez, Real vx, Real vy, Real vz, Real m,
            Real bedStiffness, Real bedDamping, V3 half, M3 rotation, Real margin, SWorld w, SCfg cfg,
            ref Real fx, ref Real fy, ref Real fz, ref int bedGlass,
            int noteRock, ref Real rockKept, ref Real rnx, ref Real rny, ref Real rnz,
            ref Real rpx, ref Real rpy, ref Real rpz)
        {
            Real sx, sy, sz, sex, sey, sez;

            if (cfg.HasBed == 0)
            {
                // FlatBed: straight down.
                Real bounded = CarryBase ? ((-cfg.WorldDepth - cy) - ey) + r : -cfg.WorldDepth - (cy - r);
                if (bounded > 0)
                {
                    SupportSphere(half, rotation, cx, cy, cz, ex, ey, ez, new V3(0, -1, 0),
                                  out sx, out sy, out sz, out sex, out sey, out sez);

                    Real below = CarryBase ? ((-cfg.WorldDepth - sy) - sey) + margin : -cfg.WorldDepth - (sy - margin);
                    if (below > 0)
                    {
                        Real up = PairPush(bedStiffness * below - bedDamping * vy, m, vy, cfg);
                        if (up > 0)
                        {
                            fx = fx + 0;
                            fy = fy + up;
                            fz = fz + 0;
                            bedGlass = 1;
                            if (noteRock != 0)
                                NoteRockAt(0, up, 0, sx, sy, sz, margin, ref rockKept, ref rnx, ref rny, ref rnz, ref rpx, ref rpy, ref rpz);
                        }
                    }
                }
            }
            else
            {
                // HeightBed: the bounding sphere's push, then minus the bed's normal under the centre.
                Real hgt, gx, gz, pen, slope;
                if (CarryBase)
                {
                    Real rise;
                    BedHeightNear(cx, cz, ex, ez, w, cfg, out hgt, out rise, out gx, out gz);
                    Real floorY = -cfg.BedDepth + hgt;
                    slope = System.Math.Sqrt((Real)1.0 + gx * gx + gz * gz);
                    pen = r - ((cy - floorY) + (ey - rise)) / slope;
                }
                else
                {
                    BedHeightAndGradient(cx, cz, w, cfg, out hgt, out gx, out gz);
                    Real floorY = -cfg.BedDepth + hgt;
                    slope = System.Math.Sqrt((Real)1.0 + gx * gx + gz * gz);
                    pen = r - (cy - floorY) / slope;
                }

                Real rx = 0, ry = 0, rz = 0;
                Real nx = 0, ny = 0, nz = 0;
                if (pen > 0)
                {
                    Real inv = (Real)1.0 / slope;
                    nx = -gx * inv; ny = (Real)1.0 * inv; nz = -gz * inv;
                    Real approach = vx * nx + vy * ny + vz * nz;
                    Real p = PairPush(bedStiffness * pen - bedDamping * approach, m, approach, cfg);
                    rx = nx * p; ry = ny * p; rz = nz * p;
                }

                if (rx != 0 || ry != 0 || rz != 0)
                {
                    SupportSphere(half, rotation, cx, cy, cz, ex, ey, ez, new V3(-nx, -ny, -nz),
                                  out sx, out sy, out sz, out sex, out sey, out sez);

                    Real shgt, sgx, sgz, spen, sslope;
                    if (CarryBase)
                    {
                        Real srise;
                        BedHeightNear(sx, sz, sex, sez, w, cfg, out shgt, out srise, out sgx, out sgz);
                        Real sfloorY = -cfg.BedDepth + shgt;
                        sslope = System.Math.Sqrt((Real)1.0 + sgx * sgx + sgz * sgz);
                        spen = margin - ((sy - sfloorY) + (sey - srise)) / sslope;
                    }
                    else
                    {
                        BedHeightAndGradient(sx, sz, w, cfg, out shgt, out sgx, out sgz);
                        Real sfloorY = -cfg.BedDepth + shgt;
                        sslope = System.Math.Sqrt((Real)1.0 + sgx * sgx + sgz * sgz);
                        spen = margin - (sy - sfloorY) / sslope;
                    }

                    Real tx = 0, ty = 0, tz = 0;
                    if (spen > 0)
                    {
                        Real sinv = (Real)1.0 / sslope;
                        Real snx = -sgx * sinv, sny = (Real)1.0 * sinv, snz = -sgz * sinv;
                        Real sapproach = vx * snx + vy * sny + vz * snz;
                        Real sp = PairPush(bedStiffness * spen - bedDamping * sapproach, m, sapproach, cfg);
                        tx = snx * sp; ty = sny * sp; tz = snz * sp;
                    }

                    if (tx != 0 || ty != 0 || tz != 0)
                    {
                        fx = fx + tx;
                        fy = fy + ty;
                        fz = fz + tz;
                        bedGlass = 1;
                        if (noteRock != 0)
                            NoteRockAt(tx, ty, tz, sx, sy, sz, margin, ref rockKept, ref rnx, ref rny, ref rnz, ref rpx, ref rpy, ref rpz);
                    }
                }
            }

            if (cfg.ReefCount != 0 || cfg.CoralCount != 0)
            {
                // Reef: the bounding sphere's push, then minus its direction.
                SD at;
                Real distance = cfg.CoralCount != 0
                    ? CoralSignedDistance(cx, cy, cz, r, w, cfg, out at)
                    : ReefSignedDistance(cx, cy, cz, r, w, cfg, out at);
                Real penetration = r - distance;
                if (CarryBase)
                {
                    Real glen = System.Math.Sqrt(at.Gx * at.Gx + at.Gy * at.Gy + at.Gz * at.Gz);
                    if (glen > (Real)1e-12) penetration = penetration - (at.Gx * ex + at.Gy * ey + at.Gz * ez) / glen;
                }

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
                    Real magnitude = System.Math.Sqrt(rx * rx + ry * ry + rz * rz);
                    Real unit = (Real)1.0 / magnitude;
                    SupportSphere(half, rotation, cx, cy, cz, ex, ey, ez, new V3(-(rx * unit), -(ry * unit), -(rz * unit)),
                                  out sx, out sy, out sz, out sex, out sey, out sez);

                    SD sat;
                    Real sdistance = cfg.CoralCount != 0
                        ? CoralSignedDistance(sx, sy, sz, margin, w, cfg, out sat)
                        : ReefSignedDistance(sx, sy, sz, margin, w, cfg, out sat);
                    Real spenetration = margin - sdistance;
                    if (CarryBase)
                    {
                        Real sglen = System.Math.Sqrt(sat.Gx * sat.Gx + sat.Gy * sat.Gy + sat.Gz * sat.Gz);
                        if (sglen > (Real)1e-12)
                            spenetration = spenetration - (sat.Gx * sex + sat.Gy * sey + sat.Gz * sez) / sglen;
                    }

                    Real tx = 0, ty = 0, tz = 0;
                    if (spenetration > 0)
                    {
                        Real slength = System.Math.Sqrt(sat.Gx * sat.Gx + sat.Gy * sat.Gy + sat.Gz * sat.Gz);
                        Real snx, sny, snz;
                        if (slength > (Real)1e-12)
                        {
                            Real sinv = (Real)1.0 / slength;
                            snx = sat.Gx * sinv; sny = sat.Gy * sinv; snz = sat.Gz * sinv;
                        }
                        else
                        {
                            snx = 0; sny = 1; snz = 0;
                        }

                        Real sapproach = vx * snx + vy * sny + vz * snz;
                        Real sp = PairPush(bedStiffness * spenetration - bedDamping * sapproach, m, sapproach, cfg);
                        tx = snx * sp; ty = sny * sp; tz = snz * sp;
                    }

                    if (tx != 0 || ty != 0 || tz != 0)
                    {
                        fx = fx + tx;
                        fy = fy + ty;
                        fz = fz + tz;
                        bedGlass = 1;
                        if (noteRock != 0)
                            NoteRockAt(tx, ty, tz, sx, sy, sz, margin, ref rockKept, ref rnx, ref rny, ref rnz, ref rpx, ref rpy, ref rpz);
                    }
                }
            }

            if (cfg.TankRadius > 0)
            {
                // Glass: the outward radial direction. The push runs inward. It sets no touch.
                Real x = cx - cfg.AxisX;
                Real z = cz - cfg.AxisZ;
                Real radial = System.Math.Sqrt(x * x + z * z);
                Real outside = radial + r - cfg.TankRadius;
                if (CarryBase) outside = GlassOutsideNear(cx, cz, ex, ez, r, cfg);

                if (outside > 0)
                {
                    Real ox, oz;
                    if (radial > (Real)1e-9) { ox = x / radial; oz = z / radial; }
                    else { ox = -1; oz = 0; }

                    SupportSphere(half, rotation, cx, cy, cz, ex, ey, ez, new V3(ox, 0, oz),
                                  out sx, out sy, out sz, out sex, out sey, out sez);

                    Real gsx = sx - cfg.AxisX;
                    Real gsz = sz - cfg.AxisZ;
                    Real supportRadial = System.Math.Sqrt(gsx * gsx + gsz * gsz);
                    Real soutside = supportRadial + margin - cfg.TankRadius;
                    if (CarryBase) soutside = GlassOutsideNear(sx, sz, sex, sez, margin, cfg);

                    if (soutside > 0)
                    {
                        Real ix, iz;
                        if (supportRadial > (Real)1e-9) { ix = -gsx / supportRadial; iz = -gsz / supportRadial; }
                        else { ix = 1; iz = 0; }

                        Real approach = vx * ix + vy * 0 + vz * iz;
                        Real p = PairPush(bedStiffness * soutside - bedDamping * approach, m, approach, cfg);

                        Real tx = ix * p, ty = 0 * p, tz = iz * p;
                        if (tx != 0 || ty != 0 || tz != 0)
                        {
                            fx = fx + tx;
                            fy = fy + ty;
                            fz = fz + tz;
                            bedGlass = 1;
                        }
                    }
                }
            }
        }

        // ---- D228 level (b): the world's surfaces at every vertex of the core, behind cfg.AtPoint
        //
        // ContactTrueShape.Apply with atPoint, for the bed, the reefs and the coral, and the glass, in that order.
        // Each surface takes the bounding sphere as the early-out with a still link (the host reads no velocity there
        // at this level), then the direction to the surface at the link's centre, then every distinct vertex of the
        // core in a fixed order (ContactTrueShape.Vertices): each vertex's sphere test at the vertex's own velocity
        // v + spin x r, on a spring and damper scaled to the mass the vertex meets (m_eff), summed. The sum is the
        // surface's push; the touch's torque is the vertices' moment plus the friction's lever (the card has no
        // friction: FarmGpu.Open refuses a bed with any), capped once by the grip's cap (Rock). The three surfaces
        // share the one vertex routine below, so that the kernel carries it once.

        private const int PointFlat = 0, PointHeight = 1, PointReef = 2, PointGlass = 3;

        /// <summary>
        /// ContactTrueShape.EffectiveMass: the mass a push at <paramref name="lever"/> from the link's centre, along
        /// <paramref name="normal"/>, meets: <c>m / (1 + m q)</c> with <c>q = (r x n) . I^-1 (r x n)</c>, the inverse
        /// inertia taken in the link's own axes (the diagonal).
        /// </summary>
        private static Real EffectiveMassAt(V3 lever, V3 normal, V3 inertia, M3 rotation, Real mass)
        {
            V3 local = rotation.TransposedTimes(V3.Cross(lever, normal));

            Real turning =
                (inertia.X > 0 ? local.X * local.X / inertia.X : (Real)0.0) +
                (inertia.Y > 0 ? local.Y * local.Y / inertia.Y : (Real)0.0) +
                (inertia.Z > 0 ? local.Z * local.Z / inertia.Z : (Real)0.0);

            return mass / ((Real)1.0 + mass * turning);
        }

        /// <summary>
        /// ContactTrueShape.Vertices for one surface: the touch of a link on every distinct vertex of its core, in the
        /// order of the sign patterns 0 to 7 (bit 0 a minus on x, bit 1 on y, bit 2 on z; a pattern with a minus on an
        /// axis of no half-extent repeats another's vertex and is skipped). Each vertex is a sphere of the margin's
        /// radius at the vertex, tested by the surface's recorded routine with the vertex's velocity, the mass it meets
        /// and the spring and damper scaled to that mass. Returns the pushes' sum.
        /// </summary>
        /// <param name="supX">The deepest vertex (the largest push, the first in the order on a tie) as a sphere's centre; the link's centre with none.</param>
        /// <param name="point">The push-weighted contact point less the link's centre, which the host forms as <c>point - centre</c>.</param>
        /// <param name="moment">The pushes' moment about the centre, <c>sum (p_k - centre) x F_k</c>, not capped.</param>
        private static V3 VerticesPoint(
            int kind, V3 half, M3 rotation, Real cx, Real cy, Real cz, Real ex, Real ey, Real ez,
            V3 direction, Real margin, V3 velocity, V3 spin, Real m, Real stiffness, Real damping, V3 inertia,
            SWorld w, SCfg cfg,
            out Real supX, out Real supY, out Real supZ, out V3 point, out V3 moment)
        {
            // The touch's push direction, read once for every vertex.
            V3 pushDirection = -direction;

            // The origin the vertex is built from: in double the link's centre, as the host builds it; in single zero,
            // so that the lever stays a small number (as SupportSphere builds its corner).
            V3 origin = CarryBase ? V3.Zero : new V3(cx, cy, cz);

            V3 total = V3.Zero;
            V3 weighted = V3.Zero;
            Real weight = 0;
            Real deepest = 0;
            int pushing = 0;
            V3 only = origin;

            supX = cx; supY = cy; supZ = cz;
            moment = V3.Zero;

            for (int pattern = 0; pattern < 8; pattern++)
            {
                bool minusX = (pattern & 1) != 0, minusY = (pattern & 2) != 0, minusZ = (pattern & 4) != 0;
                if ((minusX && !(half.X > 0)) || (minusY && !(half.Y > 0)) || (minusZ && !(half.Z > 0))) continue;

                // The corner is built as GjkSupport builds it, so that a vertex that is the support point is the same bits.
                V3 corner = new V3(
                    minusX ? -half.X : half.X,
                    minusY ? -half.Y : half.Y,
                    minusZ ? -half.Z : half.Z);

                V3 vertex = origin + rotation * corner;
                V3 vertexPoint = vertex + direction * margin;
                V3 lever = vertexPoint - origin;

                // The mass this vertex pushes against, and the spring and damper of that mass.
                Real effective = EffectiveMassAt(lever, pushDirection, inertia, rotation, m);
                Real scale = effective / m;
                Real vStiffness = stiffness * scale;
                Real vDamping = damping * scale;

                V3 pointVelocity = velocity + V3.Cross(spin, lever);

                // The vertex as a sphere's centre: a float and its rest in single (Knuth's two-sum per axis, as
                // SupportSphere), the vertex itself in double.
                Real sx, sy, sz, sex, sey, sez;
                if (CarryBase)
                {
                    sx = cx + vertex.X; Real bbx = sx - cx; sex = ex + ((cx - (sx - bbx)) + (vertex.X - bbx));
                    sy = cy + vertex.Y; Real bby = sy - cy; sey = ey + ((cy - (sy - bby)) + (vertex.Y - bby));
                    sz = cz + vertex.Z; Real bbz = sz - cz; sez = ez + ((cz - (sz - bbz)) + (vertex.Z - bbz));
                }
                else
                {
                    sx = vertex.X; sy = vertex.Y; sz = vertex.Z;
                    sex = ex; sey = ey; sez = ez;
                }

                V3 push = V3.Zero;

                if (kind == PointFlat)
                {
                    // FlatSphere: straight up.
                    Real below = CarryBase ? ((-cfg.WorldDepth - sy) - sey) + margin : -cfg.WorldDepth - (sy - margin);
                    if (below > 0)
                    {
                        Real up = PairPush(vStiffness * below - vDamping * pointVelocity.Y, effective, pointVelocity.Y, cfg);
                        push = new V3(0, up, 0);
                    }
                }
                else if (kind == PointHeight)
                {
                    // HeightSphere: ContactBed.Push on the sphere at the vertex.
                    Real shgt, sgx, sgz, spen, sslope;
                    if (CarryBase)
                    {
                        Real srise;
                        BedHeightNear(sx, sz, sex, sez, w, cfg, out shgt, out srise, out sgx, out sgz);
                        Real sfloorY = -cfg.BedDepth + shgt;
                        sslope = System.Math.Sqrt((Real)1.0 + sgx * sgx + sgz * sgz);
                        spen = margin - ((sy - sfloorY) + (sey - srise)) / sslope;
                    }
                    else
                    {
                        BedHeightAndGradient(sx, sz, w, cfg, out shgt, out sgx, out sgz);
                        Real sfloorY = -cfg.BedDepth + shgt;
                        sslope = System.Math.Sqrt((Real)1.0 + sgx * sgx + sgz * sgz);
                        spen = margin - (sy - sfloorY) / sslope;
                    }

                    if (spen > 0)
                    {
                        Real sinv = (Real)1.0 / sslope;
                        Real snx = -sgx * sinv, sny = (Real)1.0 * sinv, snz = -sgz * sinv;
                        Real sapproach = pointVelocity.X * snx + pointVelocity.Y * sny + pointVelocity.Z * snz;
                        Real sp = PairPush(vStiffness * spen - vDamping * sapproach, effective, sapproach, cfg);
                        push = new V3(snx * sp, sny * sp, snz * sp);
                    }
                }
                else if (kind == PointReef)
                {
                    // ReefSphere: ContactReef.Push on the sphere at the vertex.
                    SD sat;
                    Real sdistance = cfg.CoralCount != 0
                        ? CoralSignedDistance(sx, sy, sz, margin, w, cfg, out sat)
                        : ReefSignedDistance(sx, sy, sz, margin, w, cfg, out sat);
                    Real spenetration = margin - sdistance;
                    if (CarryBase)
                    {
                        Real sglen = System.Math.Sqrt(sat.Gx * sat.Gx + sat.Gy * sat.Gy + sat.Gz * sat.Gz);
                        if (sglen > (Real)1e-12)
                            spenetration = spenetration - (sat.Gx * sex + sat.Gy * sey + sat.Gz * sez) / sglen;
                    }

                    if (spenetration > 0)
                    {
                        Real slength = System.Math.Sqrt(sat.Gx * sat.Gx + sat.Gy * sat.Gy + sat.Gz * sat.Gz);
                        Real snx, sny, snz;
                        if (slength > (Real)1e-12)
                        {
                            Real sinv = (Real)1.0 / slength;
                            snx = sat.Gx * sinv; sny = sat.Gy * sinv; snz = sat.Gz * sinv;
                        }
                        else
                        {
                            snx = 0; sny = 1; snz = 0;
                        }

                        Real sapproach = pointVelocity.X * snx + pointVelocity.Y * sny + pointVelocity.Z * snz;
                        Real sp = PairPush(vStiffness * spenetration - vDamping * sapproach, effective, sapproach, cfg);
                        push = new V3(snx * sp, sny * sp, snz * sp);
                    }
                }
                else
                {
                    // GlassSphere: the cylinder about the vertical axis; the push runs inward.
                    Real gsx = sx - cfg.AxisX;
                    Real gsz = sz - cfg.AxisZ;
                    Real supportRadial = System.Math.Sqrt(gsx * gsx + gsz * gsz);
                    Real soutside = supportRadial + margin - cfg.TankRadius;
                    if (CarryBase) soutside = GlassOutsideNear(sx, sz, sex, sez, margin, cfg);

                    if (soutside > 0)
                    {
                        Real ix, iz;
                        if (supportRadial > (Real)1e-9) { ix = -gsx / supportRadial; iz = -gsz / supportRadial; }
                        else { ix = 1; iz = 0; }

                        Real approach = pointVelocity.X * ix + pointVelocity.Y * 0 + pointVelocity.Z * iz;
                        Real p = PairPush(vStiffness * soutside - vDamping * approach, effective, approach, cfg);
                        push = new V3(ix * p, 0 * p, iz * p);
                    }
                }

                Real size = push.Magnitude;
                if (!(size > 0)) continue;

                pushing++;
                total = total + push;
                weighted = weighted + vertexPoint * size;
                weight = weight + size;
                moment = moment + V3.Cross(lever, push);
                only = vertexPoint;

                if (size > deepest)
                {
                    deepest = size;
                    supX = sx; supY = sy; supZ = sz;
                }
            }

            V3 contact = pushing > 1 ? weighted * ((Real)1.0 / weight) : (pushing == 1 ? only : origin);
            point = contact - origin;
            return total;
        }

        /// <summary>
        /// ContactTrueShape.Apply's four surfaces on one link at D228's level (b), in Contacts.ApplyPerPart's order:
        /// the bed (flat, or ContactBed on the height map), the reefs and the coral, and the glass, each through
        /// <see cref="VerticesPoint"/>. The pushes go into the force, and each touch's capped torque into the
        /// torque (<c>Rock</c>). The rock touch is the surface's summed push, kept where it is larger than any this
        /// link has had this step, with the deepest vertex as its support (<c>NoteRock</c>).
        /// </summary>
        private static void AgainstTheWorldPoint(
            Real cx, Real cy, Real cz, Real r, Real ex, Real ey, Real ez, Real vx, Real vy, Real vz, Real m,
            Real bedStiffness, Real bedDamping, V3 half, M3 rotation, Real margin, V3 spin, V3 inertia, Real smallInertia,
            SWorld w, SCfg cfg,
            ref Real fx, ref Real fy, ref Real fz, ref Real qx, ref Real qy, ref Real qz, ref int bedGlass,
            int noteRock, ref Real rockKept, ref Real rnx, ref Real rny, ref Real rnz,
            ref Real rpx, ref Real rpy, ref Real rpz)
        {
            for (int surface = 0; surface < 3; surface++)
            {
                int kind = -1;
                V3 direction = V3.Zero;

                if (surface == 0)
                {
                    if (cfg.HasBed == 0)
                    {
                        // FlatBed: straight down. The early-out is the geometry's.
                        Real bounded = CarryBase ? ((-cfg.WorldDepth - cy) - ey) + r : -cfg.WorldDepth - (cy - r);
                        if (bounded > 0)
                        {
                            kind = PointFlat;
                            direction = new V3(0, -1, 0);
                        }
                    }
                    else
                    {
                        // HeightBed: the bounding sphere's push at rest, then minus the bed's normal under the centre.
                        Real hgt, gx, gz, pen, slope;
                        if (CarryBase)
                        {
                            Real rise;
                            BedHeightNear(cx, cz, ex, ez, w, cfg, out hgt, out rise, out gx, out gz);
                            Real floorY = -cfg.BedDepth + hgt;
                            slope = System.Math.Sqrt((Real)1.0 + gx * gx + gz * gz);
                            pen = r - ((cy - floorY) + (ey - rise)) / slope;
                        }
                        else
                        {
                            BedHeightAndGradient(cx, cz, w, cfg, out hgt, out gx, out gz);
                            Real floorY = -cfg.BedDepth + hgt;
                            slope = System.Math.Sqrt((Real)1.0 + gx * gx + gz * gz);
                            pen = r - (cy - floorY) / slope;
                        }

                        Real rx = 0, ry = 0, rz = 0;
                        Real nx = 0, ny = 0, nz = 0;
                        if (pen > 0)
                        {
                            Real inv = (Real)1.0 / slope;
                            nx = -gx * inv; ny = (Real)1.0 * inv; nz = -gz * inv;

                            // ContactBed.Push with a zero velocity: the approach is the zero vector's dot.
                            Real approach = (Real)0.0 * nx + (Real)0.0 * ny + (Real)0.0 * nz;
                            Real p = PairPush(bedStiffness * pen - bedDamping * approach, m, approach, cfg);
                            rx = nx * p; ry = ny * p; rz = nz * p;
                        }

                        if (rx != 0 || ry != 0 || rz != 0)
                        {
                            kind = PointHeight;
                            direction = new V3(-nx, -ny, -nz);
                        }
                    }
                }
                else if (surface == 1)
                {
                    if (cfg.ReefCount != 0 || cfg.CoralCount != 0)
                    {
                        // Reef: the bounding sphere's push at rest, then minus its direction.
                        SD at;
                        Real distance = cfg.CoralCount != 0
                            ? CoralSignedDistance(cx, cy, cz, r, w, cfg, out at)
                            : ReefSignedDistance(cx, cy, cz, r, w, cfg, out at);
                        Real penetration = r - distance;
                        if (CarryBase)
                        {
                            Real glen = System.Math.Sqrt(at.Gx * at.Gx + at.Gy * at.Gy + at.Gz * at.Gz);
                            if (glen > (Real)1e-12) penetration = penetration - (at.Gx * ex + at.Gy * ey + at.Gz * ez) / glen;
                        }

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

                            Real approach = (Real)0.0 * nx + (Real)0.0 * ny + (Real)0.0 * nz;
                            Real p = PairPush(bedStiffness * penetration - bedDamping * approach, m, approach, cfg);
                            rx = nx * p; ry = ny * p; rz = nz * p;
                        }

                        if (rx != 0 || ry != 0 || rz != 0)
                        {
                            Real magnitude = System.Math.Sqrt(rx * rx + ry * ry + rz * rz);
                            Real unit = (Real)1.0 / magnitude;
                            kind = PointReef;
                            direction = new V3(-(rx * unit), -(ry * unit), -(rz * unit));
                        }
                    }
                }
                else
                {
                    if (cfg.TankRadius > 0)
                    {
                        // Glass: the outward radial direction. The push runs inward. It sets no touch.
                        Real x = cx - cfg.AxisX;
                        Real z = cz - cfg.AxisZ;
                        Real radial = System.Math.Sqrt(x * x + z * z);
                        Real outside = radial + r - cfg.TankRadius;
                        if (CarryBase) outside = GlassOutsideNear(cx, cz, ex, ez, r, cfg);

                        if (outside > 0)
                        {
                            Real ox, oz;
                            if (radial > (Real)1e-9) { ox = x / radial; oz = z / radial; }
                            else { ox = -1; oz = 0; }

                            kind = PointGlass;
                            direction = new V3(ox, 0, oz);
                        }
                    }
                }

                if (kind < 0) continue;

                Real dsx, dsy, dsz;
                V3 pointAt, moment;
                V3 total = VerticesPoint(
                    kind, half, rotation, cx, cy, cz, ex, ey, ez, direction, margin, new V3(vx, vy, vz), spin, m,
                    bedStiffness, bedDamping, inertia, w, cfg,
                    out dsx, out dsy, out dsz, out pointAt, out moment);

                // The flat bed counts a push upward; the others any push.
                bool pushed = kind == PointFlat ? total.Y > 0 : (total.X != 0 || total.Y != 0 || total.Z != 0);
                if (!pushed) continue;

                // Rock: the push into the force; no friction on the card (FarmGpu.Open refuses BedFriction > 0), so the
                // slide is the zero vector the host holds with the friction off, and the torque is the vertices'
                // moment, capped once.
                fx = fx + total.X;
                fy = fy + total.Y;
                fz = fz + total.Z;

                V3 slide = V3.Zero;
                V3 capped = CapTorque(moment + V3.Cross(pointAt, slide), smallInertia, r, cfg);
                qx = qx + capped.X;
                qy = qy + capped.Y;
                qz = qz + capped.Z;

                bedGlass = 1;
                if (noteRock != 0 && kind != PointGlass)
                    NoteRockAt(total.X, total.Y, total.Z, dsx, dsy, dsz, margin, ref rockKept, ref rnx, ref rny, ref rnz, ref rpx, ref rpy, ref rpz);
            }
        }

        /// <summary>
        /// Contacts.NoteRock: one push from the rock, kept where it is larger than any this link has
        /// had this step, with its direction as the rock's normal.
        /// </summary>
        private static void NoteRock(Real x, Real y, Real z, ref Real kept, ref Real nx, ref Real ny, ref Real nz)
        {
            Real size = System.Math.Sqrt(x * x + y * y + z * z);
            if (!(size > kept)) return;

            kept = size;
            Real inv = (Real)1.0 / size;
            nx = x * inv; ny = y * inv; nz = z * inv;
        }

        /// <summary>
        /// The touch of the rock ContactLinks found this step, kept as the body's record after the
        /// step's contacts, where the CPU's contact pass leaves it: a link not touching keeps the
        /// point and normal it last had, as the CPU's does.
        /// </summary>
        private static void KeepTouch(int g, int links, SGlob gl, SCfg cfg)
        {
            int R = cfg.Rows;
            for (int l = 0; l < links; l++)
            {
                int row = g * LinkStride + l;
                int touched = gl.TouchNew[row];
                gl.RockTouched[row] = touched;
                if (touched == 0) continue;

                gl.RockPx[row] = gl.TouchAt[row];
                gl.RockPy[row] = gl.TouchAt[R + row];
                gl.RockPz[row] = gl.TouchAt[2 * R + row];
                gl.RockNx[row] = gl.TouchAt[3 * R + row];
                gl.RockNy[row] = gl.TouchAt[4 * R + row];
                gl.RockNz[row] = gl.TouchAt[5 * R + row];
            }
        }

        /// <summary>
        /// Anchor.Apply, D162's grip, on the touch KeepTouch just kept: an anchoring link grips the
        /// rock it touches, a spring and a damper hold its origin and its turn, and the grip breaks
        /// when the pull exceeds its breaking force. The CPU's arithmetic in the CPU's order.
        /// </summary>
        private static void Grip(
            int g, int links, Real[] position, Real[] velocity, Real[] spin, Real[] rotationMatrix,
            Real[] mass, Real[] smallI, Real[] fext, bool hasReserve, float secondsOfReserve,
            SSph cs, SGlob gl, SCfg cfg)
        {
            int first = g * LinkStride;
            bool any = false;
            for (int l = 0; l < links && !any; l++) any = gl.Anchors[first + l] != 0;
            if (!any) return;

            // ReserveFactor, the lag clock advanced a step; a body with no reserve source holds as if fed.
            double reserve = 1.0;
            if (hasReserve)
            {
                double seconds = secondsOfReserve;
                double threshold = cfg.GripThreshold;
                if (!(seconds > 0))
                {
                    reserve = 0.0;
                }
                else if (seconds >= threshold)
                {
                    gl.ReserveLow[g] = 0;
                }
                else
                {
                    double low = gl.ReserveLow[g] + cfg.DtD;
                    gl.ReserveLow[g] = low;
                    if (!(low <= cfg.GripLag)) reserve = threshold > 0 ? MinD(1.0, seconds / threshold) : 1.0;
                }
            }

            Real omega = cfg.Omega;
            Real zeta = cfg.Zeta;

            for (int l = 0; l < links; l++)
            {
                int row = first + l;
                if (gl.Anchors[row] == 0) continue;

                if (gl.GripHeld[row] == 0)
                {
                    if (gl.RockTouched[row] == 0 || !(reserve > 0)) continue;

                    gl.GripHeld[row] = 1;
                    gl.GripStrength[row] = 1.0;
                    for (int k = 0; k < 3; k++) gl.GripAnchor[3 * row + k] = position[3 * l + k];
                    for (int k = 0; k < 9; k++) gl.GripAttitude[9 * row + k] = rotationMatrix[9 * l + k];
                    gl.GripPoint[3 * row] = gl.RockPx[row];
                    gl.GripPoint[3 * row + 1] = gl.RockPy[row];
                    gl.GripPoint[3 * row + 2] = gl.RockPz[row];

                    gl.GripOwed[g] = gl.GripOwed[g] + gl.GripPrice[row];
                    gl.GripsFormed[g] = gl.GripsFormed[g] + 1;
                }

                if (!(reserve > 0))
                {
                    GripRelease(g, row, gl);
                    continue;
                }

                Real m = mass[l];
                V3 away = V3.Read(position, 3 * l) -
                          new V3(gl.GripAnchor[3 * row], gl.GripAnchor[3 * row + 1], gl.GripAnchor[3 * row + 2]);
                V3 linkVelocity = V3.Read(velocity, 3 * l);

                Real stiffness = m * omega * omega;
                Real damping = (Real)2.0 * zeta * m * omega;
                V3 pull = (away * stiffness + linkVelocity * damping) * (Real)(-1.0);

                double breaking = gl.GripBreak[row] * gl.GripStrength[row] * reserve;
                if ((double)pull.Magnitude > breaking)
                {
                    GripRelease(g, row, gl);
                    continue;
                }

                // The turn: the rotation from the grip's attitude to the link's, as a small vector.
                int at = 9 * row;
                M3 now = M3.Read(rotationMatrix, 9 * l);
                M3 then = new M3(
                    gl.GripAttitude[at], gl.GripAttitude[at + 1], gl.GripAttitude[at + 2],
                    gl.GripAttitude[at + 3], gl.GripAttitude[at + 4], gl.GripAttitude[at + 5],
                    gl.GripAttitude[at + 6], gl.GripAttitude[at + 7], gl.GripAttitude[at + 8]);
                V3 turned = RotationVector(now * then.Transposed);
                V3 linkSpin = V3.Read(spin, 3 * l);

                Real inertia = smallI[l];
                V3 twist = (turned * (inertia * omega * omega) + linkSpin * ((Real)2.0 * zeta * inertia * omega)) * (Real)(-1.0);

                Real fx = pull.X, fy = pull.Y, fz = pull.Z;
                BodyPush(ref fx, ref fy, ref fz, m, cfg);
                V3 torque = CapTorque(twist, inertia, cs.LR[row], cfg);

                V3.Add(fext, 6 * l, torque);
                V3.Add(fext, 6 * l + 3, new V3(fx, fy, fz));

                gl.GripStrength[row] = gl.GripStrength[row] * cfg.GripDecay;
                gl.GripHeldSeconds[g] = gl.GripHeldSeconds[g] + cfg.DtD;
            }
        }

        private static void GripRelease(int g, int row, SGlob gl)
        {
            if (gl.GripHeld[row] == 0) return;
            gl.GripHeld[row] = 0;
            gl.GripStrength[row] = 0;
            gl.GripsBroken[g] = gl.GripsBroken[g] + 1;
        }

        /// <summary>Anchor.RotationVector: the rotation vector of a rotation matrix, axis times angle.</summary>
        private static V3 RotationVector(M3 r)
        {
            V3 axis = new V3(r.M21 - r.M12, r.M02 - r.M20, r.M10 - r.M01);
            Real sine = (Real)0.5 * axis.Magnitude;
            Real cosine = (Real)0.5 * (r.M00 + r.M11 + r.M22 - (Real)1.0);
            if (!(sine > (Real)1e-12)) return axis * (Real)0.5;

            Real angle = System.Math.Atan2(sine, cosine);
            return axis * ((Real)0.5 * angle / sine);
        }

        /// <summary>Anchor.CapTorque: the grip's turn capped at the contact's speed change at the sphere's rim.</summary>
        private static V3 CapTorque(V3 torque, Real inertia, Real radius, SCfg cfg)
        {
            Real cap = cfg.MaxDv;
            if (!(cap > 0) || !(radius > 0)) return torque;

            Real most = inertia * (cap / radius) / cfg.Dt;
            Real magnitude = torque.Magnitude;
            return magnitude > most ? torque * (most / magnitude) : torque;
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
            V3 basePosition, Q4 baseRotation, V3 baseCarry, Real[] posErr)
        {
            Q4.Write(rotation, 0, baseRotation);
            M3.Write(rotationMatrix, 0, baseRotation.ToMatrix());
            V3.Write(position, 0, basePosition);
            if (CarryBase) V3.Write(posErr, 0, -baseCarry);

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

                if (CarryBase)
                {
                    // The float position and what its two roundings left out, kept exactly (TwoSum) on
                    // top of the parent's own rest: the link's centre is position plus posErr.
                    V3 half = parentPosition + pAnchor;
                    V3 at = half - cAnchor;
                    V3 left = V3.Read(posErr, 3 * p) + SumError(parentPosition, pAnchor, half)
                              + SumError(half, -cAnchor, at);
                    V3.Write(position, 3 * i, at);
                    V3.Write(posErr, 3 * i, left);
                }
                else
                {
                    V3.Write(position, 3 * i, parentPosition + pAnchor - cAnchor);
                }
            }
        }

        /// <summary>What rounding left out of <paramref name="s"/> = a + b (Knuth's TwoSum, exact).</summary>
        private static V3 SumError(V3 a, V3 b, V3 s)
        {
            V3 bb = s - a;
            return (a - (s - bb)) + (b - bb);
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

        // r59-pos64: in single the base position is a compensated sum (Integrate). In double it is
        // the solver's plain sum, to the bit.
        private const bool CarryBase = 8 == 4;

        /// <summary>
        /// SharedVolume.TryWrap on the card: the root moved by the box's length on each axis it has
        /// left, the velocities untouched. The position is the base less its carry. Off the far face
        /// the length comes off the base, which is exact there; off the near face it goes onto the
        /// base, and what that sum rounds away goes into the carry, so the place moves by the
        /// length and by nothing else. A coordinate that is not a number is left for the farm's
        /// guard, as the processor leaves it.
        /// </summary>
        private static void WrapAtTheSeams(ref V3 basePosition, ref V3 baseCarry, SCfg cfg)
        {
            Real x = basePosition.X, cx = baseCarry.X;
            Real z = basePosition.Z, cz = baseCarry.Z;
            WrapAxis(ref x, ref cx, cfg.SeamL);
            WrapAxis(ref z, ref cz, cfg.SeamW);
            basePosition = new V3(x, basePosition.Y, z);
            baseCarry = new V3(cx, baseCarry.Y, cz);
        }

        private static void WrapAxis(ref Real v, ref Real carry, Real length)
        {
            if (v >= length)
            {
                v = v - length;
            }
            else if (v < 0)
            {
                Real moved = v + length;
                Real lost = (moved - length) - v;
                v = moved;
                carry = carry + lost;
            }
        }

        private static void Integrate(
            int links, int[] dofCount, int[] dofStart, Real[] tau, Real[] q, Real[] qd,
            Real[] ballRot, Real[] acc, Real[] spin, Real[] velocity,
            ref V3 basePosition, ref V3 baseCarry, ref Q4 baseRotation, Real dt)
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

            if (CarryBase)
            {
                // A compensated sum: the part of the step the position's float could not hold is
                // carried and added back, so a slow body still moves. The position is basePosition
                // less baseCarry.
                V3 add = v * dt - baseCarry;
                V3 sum = basePosition + add;
                baseCarry = (sum - basePosition) - add;
                basePosition = sum;
            }
            else
            {
                basePosition = basePosition + v * dt;
            }
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

        private static bool IsFiniteR(Real v) =>
            !(v != v || v == Real.PositiveInfinity || v == Real.NegativeInfinity);

        /// <summary>Contacts.FiniteSphere: a sphere the grid's mean may count.</summary>
        private static bool FiniteSphere(Real cx, Real cy, Real cz, Real r) =>
            IsFiniteR(cx) && IsFiniteR(cy) && IsFiniteR(cz) && IsFiniteR(r);

        /// <summary>Contacts.OutOfReach: <c>!(|c| + r &lt; 1e9·cell)</c> on every axis, which NaN fails.</summary>
        private static bool OutOfReach(Real cx, Real cy, Real cz, Real r, Real cell)
        {
            Real reach = (Real)ReachCells * cell;
            return !(AbsR(cx) + r < reach && AbsR(cy) + r < reach && AbsR(cz) + r < reach);
        }

        /// <summary>
        /// ScentField.At, line for line: the scent at a point by trilinear interpolation between the
        /// centres of the eight cells round it, a point outside the grid reading its nearest edge,
        /// and a point that is not finite reading 0.
        /// </summary>
        private static float ScentAt(float x, float y, float z, SWorld w, SCfg cfg)
        {
            int nx = cfg.ScentNx, ny = cfg.ScentNy, nz = cfg.ScentNz;
            Real u = (Real)(x / ScentCell) - (Real)0.5;
            Real v = (Real)(-y / ScentCell) - (Real)0.5;
            Real s = (Real)(z / ScentCell) - (Real)0.5;
            Real sum = u + v + s;
            if (sum != sum || sum == Real.PositiveInfinity || sum == Real.NegativeInfinity) return 0f;

            int i0 = ScentClamp(ScentFloor(u), nx), j0 = ScentClamp(ScentFloor(v), ny), k0 = ScentClamp(ScentFloor(s), nz);
            int i1 = ScentClamp(i0 + 1, nx), j1 = ScentClamp(j0 + 1, ny), k1 = ScentClamp(k0 + 1, nz);
            Real fu = ScentFrac(u, i0, nx), fw = ScentFrac(v, j0, ny), fs = ScentFrac(s, k0, nz);
            if (cfg.SmellWraps != 0)
            {
                SmellRound(u, nx, out i0, out i1, out fu);
                SmellRound(s, nz, out k0, out k1, out fs);
            }

            Real c00 = (Real)w.Scent[(i0 * ny + j0) * nz + k0] * ((Real)1 - fs) + (Real)w.Scent[(i0 * ny + j0) * nz + k1] * fs;
            Real c01 = (Real)w.Scent[(i0 * ny + j1) * nz + k0] * ((Real)1 - fs) + (Real)w.Scent[(i0 * ny + j1) * nz + k1] * fs;
            Real c10 = (Real)w.Scent[(i1 * ny + j0) * nz + k0] * ((Real)1 - fs) + (Real)w.Scent[(i1 * ny + j0) * nz + k1] * fs;
            Real c11 = (Real)w.Scent[(i1 * ny + j1) * nz + k0] * ((Real)1 - fs) + (Real)w.Scent[(i1 * ny + j1) * nz + k1] * fs;
            Real c0 = c00 * ((Real)1 - fw) + c01 * fw, c1 = c10 * ((Real)1 - fw) + c11 * fw;
            return (float)(c0 * ((Real)1 - fu) + c1 * fu);
        }

        private const float ScentCell = 2f;   // ScentField.CellMetres

        /// <summary>
        /// CreatureSenses.SampleLeaf: the leaf field squashed, and in the direction form its gradient in
        /// the part's own frame, unit length, times that value; zero where the gradient is.
        /// </summary>
        private static void SampleLeaf(int bl, float px, float py, float pz, Real[] rotationMatrix, bool direction,
                                       int frameMirror, float[] sLeaf, SWorld w, SCfg cfg)
        {
            float s = LeafAt(px, py, pz, w, cfg);
            float strength = s > 0f ? s / (s + cfg.LeafHalf) : 0f;
            sLeaf[4 * bl] = strength;

            if (!direction) return;

            float gx, gy, gz;
            LeafGradientAt(px, py, pz, w, cfg, out gx, out gy, out gz);

            int r = 9 * bl;
            Real lx = rotationMatrix[r] * gx + rotationMatrix[r + 3] * gy + rotationMatrix[r + 6] * gz;
            Real ly = rotationMatrix[r + 1] * gx + rotationMatrix[r + 4] * gy + rotationMatrix[r + 7] * gz;
            Real lz = rotationMatrix[r + 2] * gx + rotationMatrix[r + 5] * gy + rotationMatrix[r + 8] * gz;

            // D234, as the flow in Sample: the component along the reflection's axis negated.
            if ((frameMirror & 8) != 0)
            {
                int axis = (frameMirror >> 4) & 3;
                if (axis == 0) lx = -lx;
                else if (axis == 1) ly = -ly;
                else lz = -lz;
            }

            Real length = SqrtR(lx * lx + ly * ly + lz * lz);

            if (length > 0 && strength > 0f)
            {
                Real scale = strength / length;
                sLeaf[4 * bl + 1] = (float)(lx * scale);
                sLeaf[4 * bl + 2] = (float)(ly * scale);
                sLeaf[4 * bl + 3] = (float)(lz * scale);
            }
            else
            {
                sLeaf[4 * bl + 1] = 0f;
                sLeaf[4 * bl + 2] = 0f;
                sLeaf[4 * bl + 3] = 0f;
            }
        }

        /// <summary>ScentField.At on the leaf field's own grid and cell, line for line.</summary>
        private static float LeafAt(float x, float y, float z, SWorld w, SCfg cfg)
        {
            int nx = cfg.LeafNx, ny = cfg.LeafNy, nz = cfg.LeafNz;
            float cell = cfg.LeafCell;
            Real u = (Real)(x / cell) - (Real)0.5;
            Real v = (Real)(-y / cell) - (Real)0.5;
            Real s = (Real)(z / cell) - (Real)0.5;
            Real sum = u + v + s;
            if (sum != sum || sum == Real.PositiveInfinity || sum == Real.NegativeInfinity) return 0f;

            int i0 = ScentClamp(ScentFloor(u), nx), j0 = ScentClamp(ScentFloor(v), ny), k0 = ScentClamp(ScentFloor(s), nz);
            int i1 = ScentClamp(i0 + 1, nx), j1 = ScentClamp(j0 + 1, ny), k1 = ScentClamp(k0 + 1, nz);
            Real fu = ScentFrac(u, i0, nx), fw = ScentFrac(v, j0, ny), fs = ScentFrac(s, k0, nz);
            if (cfg.SmellWraps != 0)
            {
                SmellRound(u, nx, out i0, out i1, out fu);
                SmellRound(s, nz, out k0, out k1, out fs);
            }

            Real c00 = (Real)w.Leaf[(i0 * ny + j0) * nz + k0] * ((Real)1 - fs) + (Real)w.Leaf[(i0 * ny + j0) * nz + k1] * fs;
            Real c01 = (Real)w.Leaf[(i0 * ny + j1) * nz + k0] * ((Real)1 - fs) + (Real)w.Leaf[(i0 * ny + j1) * nz + k1] * fs;
            Real c10 = (Real)w.Leaf[(i1 * ny + j0) * nz + k0] * ((Real)1 - fs) + (Real)w.Leaf[(i1 * ny + j0) * nz + k1] * fs;
            Real c11 = (Real)w.Leaf[(i1 * ny + j1) * nz + k0] * ((Real)1 - fs) + (Real)w.Leaf[(i1 * ny + j1) * nz + k1] * fs;
            Real c0 = c00 * ((Real)1 - fw) + c01 * fw, c1 = c10 * ((Real)1 - fw) + c11 * fw;
            return (float)(c0 * ((Real)1 - fu) + c1 * fu);
        }

        /// <summary>
        /// ScentField.GradientAt on the leaf field, line for line: the interpolation's derivative per
        /// metre, zero along an axis where the point lies outside the cell centres, y's sign turned.
        /// </summary>
        private static void LeafGradientAt(float x, float y, float z, SWorld w, SCfg cfg,
                                           out float gx, out float gy, out float gz)
        {
            gx = 0f; gy = 0f; gz = 0f;
            int nx = cfg.LeafNx, ny = cfg.LeafNy, nz = cfg.LeafNz;
            float cell = cfg.LeafCell;
            Real u = (Real)(x / cell) - (Real)0.5;
            Real v = (Real)(-y / cell) - (Real)0.5;
            Real s = (Real)(z / cell) - (Real)0.5;
            Real sum = u + v + s;
            if (sum != sum || sum == Real.PositiveInfinity || sum == Real.NegativeInfinity) return;

            int i0 = ScentClamp(ScentFloor(u), nx), j0 = ScentClamp(ScentFloor(v), ny), k0 = ScentClamp(ScentFloor(s), nz);
            int i1 = ScentClamp(i0 + 1, nx), j1 = ScentClamp(j0 + 1, ny), k1 = ScentClamp(k0 + 1, nz);
            Real fu = ScentFrac(u, i0, nx), fw = ScentFrac(v, j0, ny), fs = ScentFrac(s, k0, nz);
            if (cfg.SmellWraps != 0)
            {
                SmellRound(u, nx, out i0, out i1, out fu);
                SmellRound(s, nz, out k0, out k1, out fs);
            }
            bool inU = LeafInside(u, nx), inW = LeafInside(v, ny), inS = LeafInside(s, nz);
            if (cfg.SmellWraps != 0)
            {
                inU = nx > 1;
                inS = nz > 1;
            }

            Real v000 = w.Leaf[(i0 * ny + j0) * nz + k0], v001 = w.Leaf[(i0 * ny + j0) * nz + k1];
            Real v010 = w.Leaf[(i0 * ny + j1) * nz + k0], v011 = w.Leaf[(i0 * ny + j1) * nz + k1];
            Real v100 = w.Leaf[(i1 * ny + j0) * nz + k0], v101 = w.Leaf[(i1 * ny + j0) * nz + k1];
            Real v110 = w.Leaf[(i1 * ny + j1) * nz + k0], v111 = w.Leaf[(i1 * ny + j1) * nz + k1];

            Real c00 = v000 * ((Real)1 - fs) + v001 * fs, c01 = v010 * ((Real)1 - fs) + v011 * fs;
            Real c10 = v100 * ((Real)1 - fs) + v101 * fs, c11 = v110 * ((Real)1 - fs) + v111 * fs;
            Real c0 = c00 * ((Real)1 - fw) + c01 * fw, c1 = c10 * ((Real)1 - fw) + c11 * fw;

            Real du = inU ? c1 - c0 : 0;
            Real dw = inW ? (c01 - c00) * ((Real)1 - fu) + (c11 - c10) * fu : 0;
            Real ds = inS
                ? ((v001 - v000) * ((Real)1 - fw) + (v011 - v010) * fw) * ((Real)1 - fu) +
                  ((v101 - v100) * ((Real)1 - fw) + (v111 - v110) * fw) * fu
                : 0;

            gx = (float)(du / cell);
            gy = (float)(-dw / cell);
            gz = (float)(ds / cell);
        }

        private static bool LeafInside(Real u, int n) => n > 1 && u > 0 && u < n - 1;

        /// <summary>
        /// (int)Math.Floor(u) as the farm's .NET 8 on x64 is believed to give it: a value outside int's
        /// range is int.MinValue (cvttsd2si's integer indefinite), where a card's conversion would
        /// saturate. Only a body millions of kilometres out could tell, and the reach guard has lost
        /// such a body before any sense reads it (D157 lets the two engines part there besides).
        /// </summary>
        private static int ScentFloor(Real u)
        {
            int i = Floor(u);
            return u >= (Real)(-2147483648.0) && u < (Real)2147483648.0 ? i : int.MinValue;
        }

        private static int ScentClamp(int i, int n) => i < 0 ? 0 : i >= n ? n - 1 : i;

        /// <summary>
        /// ScentField.RoundAxis: the two cells either side of a point along a ring and its fraction
        /// between their centres, for a smell grid whose seams are joined.
        /// </summary>
        private static void SmellRound(Real u, int n, out int i0, out int i1, out Real f)
        {
            int floor = ScentFloor(u);
            f = u - floor;
            int m = floor % n;
            i0 = m < 0 ? m + n : m;
            i1 = i0 + 1 == n ? 0 : i0 + 1;
        }

        private static Real ScentFrac(Real u, int i0, int n)
        {
            if (u <= 0 || n == 1) return 0;
            if (u >= n - 1) return 0;
            return u - i0;
        }

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
        private readonly AcceleratorStream _stream;
        private readonly List<IColumn> _all = new List<IColumn>();
        private readonly List<IColumn> _down = new List<IColumn>();
        private readonly List<IColumn> _block = new List<IColumn>();

        // Every column a block does not send up whole anyway (not _block): what a pack changes and
        // nothing else does between blocks, so a pack sends only its own slots of these (Upload).
        private readonly List<IColumn> _packed = new List<IColumn>();

        // The local slots packed since the last upload, and how many.
        private readonly Col<int> _packSlots;
        private int _packCount;

        // The arenas still being laid out, in the order their first column asked for them, keyed
        // by element type and copy kind (C); Seal builds each one's buffers once every column is in.
        private readonly List<IArenaPlan> _plans = new List<IArenaPlan>();

        public readonly int Class, L, D, M;

        public int Cap { get; private set; }

        /// <summary>Every column goes up at the next block: a pack, a growth or the first block.</summary>
        public bool Dirty = true;

        public int Panels { get; private set; } = 1;

        public readonly Seg<int> Links, Dof, SigLen, Mask, GSlot, Parent, DofCount, DofStart, PanelStart, ThinAxis,
            NOff, NCnt, BParent, FirstChild, BDofStart, BDofCount, CUnits, CDof, CLinkAt, CPortAt;

        public readonly Seg<Real> Mass, Lift, Volume, SmallI, Reach, Arm, Inertia, ChildAnchor, ParentAnchor, SpinMoment,
            JointFrame, RestFrame, LimitLo, LimitHi, LimitStiff, LimitDamp, Excess, TotalMass;

        public readonly Seg<float> TorquePerUnit, PowerScale;

        // The bend-and-power drive: the joint's four genes and the part's power, a link each, and
        // the brain's bend and the envelope's seven numbers, a degree of freedom each. A body's
        // width only in a world that runs the drive, Col's one-wide stub otherwise.
        public readonly Seg<float> Threshold, Reaction, HoldGene, Release;
        public readonly Seg<Real> LinkPower;

        // D229's fin law: each link's shape code and the three numbers beside it. A link wide only in a
        // world that runs the law, one-wide otherwise.
        public readonly Seg<int> ShapeCode;
        public readonly Seg<Real> HalfExt;

        // D231's stroke that pushes its assembly: a link's parts in its assembly, and a degree of freedom's
        // along, added mass and first moment. A link or a degree of freedom wide only in a world that runs it,
        // one-wide otherwise.
        public readonly Seg<int> StrokeParts;
        public readonly Seg<Real> StrokeAlong, StrokeMass, StrokeMoment;
        // D255's family limb under the fin law: a degree of freedom each, indexed like DofStart: the added mass of
        // the family part moving along that degree's stroke, and that mass's first moment about the joint
        // (Creature.FamilyAddedMass, FamilyFirstMoment). 0 for a degree of a link that is not a family link. A degree of
        // freedom wide only in a world that runs the fin law, one-wide otherwise.
        public readonly Seg<Real> FamMass, FamMoment;
        // D234's mirrored twin: each link's mask of the degrees of freedom read negated. A link wide only in
        // a world whose bodies reflect their drive, one-wide otherwise.
        public readonly Seg<int> MirrorMask;
        // D235's nose: 1 on a nose link. A link wide only in a world whose table lists the nose, one-wide otherwise.
        public readonly Seg<int> NoseFlag;
        // D235's held sniff, six floats a link, filled from each live slot's CreatureSenses at every block and
        // uploaded with the block's columns. A column of its own, not an arena's row: in a world without the nose it
        // is a one-cell stub that belongs to no copy list, so no block ever moves its byte (HeldBytesUp counts them).
        public readonly Col<float> NoseHeld;
        public readonly bool Nose;
        public long HeldBytesUp;
        public readonly Seg<float> Bend;
        public readonly Seg<double> EPower, EBend, EPowerTarget, EBendTarget, EPowerRate, EBendRate, EHoldLeft;

        public readonly Col<Real> PCentre, PNormal, PArea;

        public readonly Seg<Real> BasePos, BaseCarry, BaseRot, Q, Qd, Tau, LimitImplicit, BallRot, Pos, Rot, RotM, Spin, Vel,
            Sang, Slin, Cbias, RelVel, Water, WaterAcc, Fext, Applied;

        public readonly Seg<float> Signal, History, RunSum;
        public readonly Seg<int> Cursor, Filled;
        public readonly Seg<long> DriveLimited, DragLimited;
        public readonly Seg<double> Work, Signed, Dissipated, Passive;

        public readonly Seg<int> Op, NIn, Kind, NIndex, Channel;
        public readonly Seg<float> Freq, Phase, Amp, Bias, Const, Weight;

        public readonly Seg<float> S, Mem, Reserve, Damage;
        public readonly Seg<int> Parity, Contact, Gates;
        public readonly Seg<double> Clock;

        public readonly Seg<Real> Ring;
        public readonly Seg<long> TStep, FirstBadStep;
        public readonly Seg<double> TTime;
        public readonly Seg<int> Enabled, Held, TCursor, FirstBadLink, Resized, Poison, Pending;

        public ClassSet(Accelerator acc, AcceleratorStream stream, int index, int links, int neurons, int cap, bool bendAndPower, bool shapedMass, bool strokeAssembly, bool mirroredDrive, bool nose)
        {
            _acc = acc;
            _stream = stream;
            Class = index;
            L = links;
            D = 3 * links;
            M = neurons;
            Cap = Math.Max(1, cap);
            _packSlots = new Col<int>(acc, stream, 1, 16);

            Links = C<int>(1, Constant); Dof = C<int>(1, Constant); SigLen = C<int>(1, Constant);
            Mask = C<int>(1, Constant); GSlot = C<int>(1, Block, -1);
            Parent = C<int>(L, Constant); DofCount = C<int>(L, Constant); DofStart = C<int>(L, Constant);
            PanelStart = C<int>(L + 1, Constant); ThinAxis = C<int>(L, Constant);
            NOff = C<int>(L, Constant); NCnt = C<int>(L, Constant); BParent = C<int>(L, Constant);
            FirstChild = C<int>(L, Constant); BDofStart = C<int>(L, Constant); BDofCount = C<int>(L, Constant);
            CUnits = C<int>(1, Constant, -1); CDof = C<int>(1, Constant);
            CLinkAt = C<int>(1, Constant); CPortAt = C<int>(1, Constant);

            Mass = C<Real>(L, Constant); Lift = C<Real>(L, Constant); Volume = C<Real>(L, Constant);
            SmallI = C<Real>(L, Constant); Reach = C<Real>(L, Constant); Arm = C<Real>(L, Constant);
            Inertia = C<Real>(3 * L, Constant); ChildAnchor = C<Real>(3 * L, Constant);
            ParentAnchor = C<Real>(3 * L, Constant); SpinMoment = C<Real>(3 * L, Constant);
            JointFrame = C<Real>(4 * L, Constant); RestFrame = C<Real>(4 * L, Constant);
            LimitLo = C<Real>(D, Constant); LimitHi = C<Real>(D, Constant);
            LimitStiff = C<Real>(D, Constant); LimitDamp = C<Real>(D, Constant);
            Excess = C<Real>(1, Constant); TotalMass = C<Real>(1, Constant);
            TorquePerUnit = C<float>(D, Constant); PowerScale = C<float>(1, Block);

            int gl = bendAndPower ? L : 0, gd = bendAndPower ? D : 0;
            Threshold = C<float>(gl, Constant); Reaction = C<float>(gl, Constant);
            HoldGene = C<float>(gl, Constant); Release = C<float>(gl, Constant);
            LinkPower = C<Real>(gl, Constant);

            int gs = shapedMass ? L : 0;
            ShapeCode = C<int>(gs, Constant); HalfExt = C<Real>(3 * gs, Constant);
            int fd = shapedMass ? D : 0;
            FamMass = C<Real>(fd, Constant); FamMoment = C<Real>(fd, Constant);

            int sa = strokeAssembly ? L : 0, sd = strokeAssembly ? D : 0;
            StrokeParts = C<int>(sa, Constant);
            StrokeAlong = C<Real>(sd, Constant); StrokeMass = C<Real>(sd, Constant); StrokeMoment = C<Real>(sd, Constant);
            MirrorMask = C<int>(mirroredDrive ? L : 0, Constant);
            NoseFlag = C<int>(nose ? L : 0, Constant);
            Bend = C<float>(gd, Down);
            EPower = C<double>(gd, Down); EBend = C<double>(gd, Down);
            EPowerTarget = C<double>(gd, Down); EBendTarget = C<double>(gd, Down);
            EPowerRate = C<double>(gd, Down); EBendRate = C<double>(gd, Down);
            EHoldLeft = C<double>(gd, Down);

            PCentre = Widening<Real>(3); PNormal = Widening<Real>(3); PArea = Widening<Real>(1);

            Nose = nose;
            if (nose)
            {
                NoseHeld = new Col<float>(acc, stream, 6 * L, Cap);
                _all.Add(NoseHeld);
                _block.Add(NoseHeld);
            }
            else
            {
                NoseHeld = new Col<float>(acc, stream, 1, 1);
            }

            BasePos = C<Real>(3, Down); BaseCarry = C<Real>(3, Down); BaseRot = C<Real>(4, Down);
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

            Seal();
        }

        // A column in its type and kind's arena, one row a fill (Seg). A width of nothing still
        // takes the one row Col's stub would.
        private Seg<T> C<T>(int width, int kind, T fill = default) where T : unmanaged
        {
            ArenaPlan<T> plan = null;
            foreach (IArenaPlan p in _plans)
            {
                if (p.Kind == kind && p is ArenaPlan<T> typed) { plan = typed; break; }
            }

            if (plan == null) _plans.Add(plan = new ArenaPlan<T>(kind));
            return plan.Add(Math.Max(1, width), fill);
        }

        // The drag panels' columns widen with the widest body (WidenPanels), which an arena's rows
        // cannot, so each is a column of its own, constant.
        private Col<T> Widening<T>(int width) where T : unmanaged
        {
            var column = new Col<T>(_acc, _stream, width, Cap);
            _all.Add(column);
            _packed.Add(column);
            return column;
        }

        private void Seal()
        {
            foreach (IArenaPlan plan in _plans)
            {
                IColumn arena = plan.Build(_acc, _stream, Cap);
                _all.Add(arena);
                if ((plan.Kind & Down) != 0) _down.Add(arena);
                if ((plan.Kind & Block) != 0) _block.Add(arena);
                else _packed.Add(arena);
            }

            _plans.Clear();
        }

        private interface IArenaPlan
        {
            int Kind { get; }

            IColumn Build(Accelerator acc, AcceleratorStream stream, int cap);
        }

        private sealed class ArenaPlan<T> : IArenaPlan where T : unmanaged
        {
            private readonly List<Seg<T>> _segs = new List<Seg<T>>();
            private readonly List<T> _fills = new List<T>();

            public ArenaPlan(int kind) => Kind = kind;

            public int Kind { get; }

            public Seg<T> Add(int width, T fill)
            {
                var seg = new Seg<T>(_fills.Count, width);
                for (int k = 0; k < width; k++) _fills.Add(fill);
                _segs.Add(seg);
                return seg;
            }

            public IColumn Build(Accelerator acc, AcceleratorStream stream, int cap)
            {
                var arena = new Col<T>(acc, stream, cap, _fills.ToArray());
                foreach (Seg<T> seg in _segs) seg.Arena = arena;
                return arena;
            }
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

        // Both queue their copies without waiting (IColumn.UpQueued); the runner joins the stream
        // after its block's uploads and after its downloads (Run, sections 5 and 8). Each copy stops
        // at the class's used slots (IColumn.UpUsedQueued): no kernel reads a slot past them. A copy
        // moves an arena, every column of one element type and kind, so a class makes a handful.
        //
        // A class dirty with packs sends its other columns by the packed slots alone
        // (IColumn.UpSlotsQueued, 1b). That is sound because between blocks the host writes a class's
        // columns only in Pack (the packed slots), PackBlock (block columns, which go up whole) and a
        // freed slot's GSlot (a block column), and the download before brought every used slot of the
        // down columns back, so in every other slot the card already holds what the host does. A
        // column re-laid since its last copy (Ensure, WidenPanels) goes up over its used slots.
        public void Upload(int used)
        {
            if (Dirty && _packCount > 0)
            {
                _packSlots.UpUsedQueued(_packCount);
                foreach (IColumn c in _block) c.UpUsedQueued(used);
                foreach (IColumn c in _packed) c.UpSlotsQueued(_packSlots, _packCount, used);
            }
            else
            {
                foreach (IColumn c in Dirty ? _all : _block) c.UpUsedQueued(used);
            }

            if (Nose) HeldBytesUp += NoseHeld.BytesFor(used);

            Dirty = false;
            _packCount = 0;
        }

        /// <summary>A body packed into local slot <paramref name="local"/>: the class goes up at the next block, that slot of it in full.</summary>
        public void NotePack(int local)
        {
            if (_packCount == _packSlots.Cap) _packSlots.Resize(2 * _packSlots.Cap);
            _packSlots.Host[_packCount++] = local;
            Dirty = true;
        }

        public void Download(int used)
        {
            foreach (IColumn c in _down) c.DownUsedQueued(used);
        }

        /// <summary>The probe's count: what Upload would send now, and what Download brings back.</summary>
        public long UploadBytes
        {
            get { long b = 0; foreach (IColumn c in Dirty ? _all : _block) b += c.Bytes; return b; }
        }

        public long DownloadBytes
        {
            get { long b = 0; foreach (IColumn c in _down) b += c.Bytes; return b; }
        }

        /// <summary>The probe's count of what the same copies would move if they stopped at the class's <paramref name="used"/> slots.</summary>
        public long UploadBytesFor(int used)
        {
            long b = 0;
            foreach (IColumn c in Dirty ? _all : _block) b += c.BytesFor(used);
            return b;
        }

        public long DownloadBytesFor(int used)
        {
            long b = 0;
            foreach (IColumn c in _down) b += c.BytesFor(used);
            return b;
        }

        /// <summary>Every column of the class, for the probe's count of page-locked host arrays.</summary>
        public IReadOnlyList<IColumn> Columns => _all;

        public STopo Topo => new STopo
        {
            Links = Links.View, Dof = Dof.View, SigLen = SigLen.View, Mask = Mask.View, GSlot = GSlot.View,
            Parent = Parent.View, DofCount = DofCount.View, DofStart = DofStart.View, PanelStart = PanelStart.View,
            ThinAxis = ThinAxis.View, NOff = NOff.View, NCnt = NCnt.View, BParent = BParent.View,
            FirstChild = FirstChild.View, BDofStart = BDofStart.View, BDofCount = BDofCount.View,
            CUnits = CUnits.View, CDof = CDof.View, CLinkAt = CLinkAt.View, CPortAt = CPortAt.View,
        };

        public SConst ConstSet => new SConst
        {
            Mass = Mass.View, Lift = Lift.View, Volume = Volume.View, SmallI = SmallI.View, Reach = Reach.View,
            Arm = Arm.View, Inertia = Inertia.View, ChildAnchor = ChildAnchor.View, ParentAnchor = ParentAnchor.View,
            SpinMoment = SpinMoment.View,
            JointFrame = JointFrame.View, RestFrame = RestFrame.View, LimitLo = LimitLo.View, LimitHi = LimitHi.View,
            LimitStiff = LimitStiff.View, LimitDamp = LimitDamp.View, Excess = Excess.View,
            TotalMass = TotalMass.View, TorquePerUnit = TorquePerUnit.View, PowerScale = PowerScale.View,
            Threshold = Threshold.View, Reaction = Reaction.View, HoldGene = HoldGene.View, Release = Release.View,
            LinkPower = LinkPower.View, ShapeCode = ShapeCode.View, HalfExt = HalfExt.View,
            StrokeParts = StrokeParts.View, StrokeAlong = StrokeAlong.View,
            StrokeMass = StrokeMass.View, StrokeMoment = StrokeMoment.View,
            FamMass = FamMass.View, FamMoment = FamMoment.View,
            MirrorMask = MirrorMask.View, NoseFlag = NoseFlag.View,
        };

        public SPanels PanelSet => new SPanels { Centre = PCentre.View, Normal = PNormal.View, Area = PArea.View };

        public SState StateSet => new SState
        {
            BasePos = BasePos.View, BaseCarry = BaseCarry.View, BaseRot = BaseRot.View, Q = Q.View, Qd = Qd.View, Tau = Tau.View,
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
            Bend = Bend.View, EPower = EPower.View, EBend = EBend.View, EPowerTarget = EPowerTarget.View,
            EBendTarget = EBendTarget.View, EPowerRate = EPowerRate.View, EBendRate = EBendRate.View,
            EHoldLeft = EHoldLeft.View,
        };

        public SNeur NeurSet => new SNeur
        {
            Op = Op.View, NIn = NIn.View, Kind = Kind.View, Index = NIndex.View, Channel = Channel.View,
            Freq = Freq.View, Phase = Phase.View, Amp = Amp.View, Bias = Bias.View, Const = Const.View,
            Weight = Weight.View,
        };

        public SBrain BrainSet => new SBrain
        {
            S = S.View, Mem = Mem.View, Reserve = Reserve.View, Damage = Damage.View, NoseHeld = NoseHeld.View,
            Parity = Parity.View, Contact = Contact.View, Gates = Gates.View, Clock = Clock.View,
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
            if (!Nose) NoseHeld.Dispose();
            _packSlots.Dispose();
        }
    }

    /// <summary>
    /// The double engine's host: the mirror's pack and unpack and one block of steps
    /// (logbook/specs/gpu-port-spec.md sections 2, 4 and 5).
    /// </summary>
    internal sealed class Runner : IGpuRunner
    {
        private readonly Accelerator _acc;

        // Every launch, copy and wait of this engine's: the accelerator's default stream when the engine
        // is alone on its device, every call as it always was; a stream of its own on a shared device
        // (GpuDevice), where a wait on the device would wait on another world's kernels too.
        private readonly AcceleratorStream _stream;
        private readonly bool _shared;
        private readonly GpuWorld _w;
        private readonly GpuOptions _o;
        private readonly GpuSlots _slots;
        private readonly ClassSet[] _classes;
        private readonly bool _onCpu;
        private readonly int _scanGroup, _tileGroup, _meanGroup;
        private readonly int _chunk = WholeStep.MeanGroupBodies;

        public int GroupSize { get; }

        public double CompileMs { get; }

        public long[] Overflow { get; } = new long[4];

        // The overflow counts the contact watch has already taken, so each block adds only its own.
        private long _watchQueries, _watchPast;

        /// <summary>
        /// The block's contact extremes into the stats row's window (DynamicsWorld.Watch), and the card's
        /// slots cleared for the next block: the column goes up again at the next block's start.
        /// </summary>
        private void FoldWatch(DynamicsWorld world, IReadOnlyList<Creature> list)
        {
            ContactWatch watch = world.Watch;
            long[] h = _overflow.Host;
            watch.NoteCandidates(h[WholeStep.WatchCandidates]);
            long over = h[WholeStep.WatchOverlaps];
            if (over > 0)
            {
                int rank = 0xFFFFFF - (int)(over & 0xFFFFFF);
                if (rank >= 0 && rank < list.Count) watch.NoteOverlaps(over >> 24, rank, list[rank].Id, list[rank].Links);
            }
            long packed = h[WholeStep.WatchCells];
            if (packed > 0)
            {
                long cells = packed >> 24;
                int rank = 0xFFFFFF - (int)(packed & 0xFFFFFF);
                if (rank >= 0 && rank < list.Count) watch.NoteCells(cells, rank, list[rank].Id, list[rank].Links);
            }
            watch.QueriesAtCap += Overflow[0] - _watchQueries;
            watch.OverlapsPastCap += Overflow[1] - _watchPast;
            _watchQueries = Overflow[0];
            _watchPast = Overflow[1];
            h[WholeStep.WatchCandidates] = 0;
            h[WholeStep.WatchOverlaps] = 0;
            h[WholeStep.WatchCells] = 0;
        }

        public long RefusedForClass => _slots.RefusedForClass;

        public long NeuronsAtCeiling { get; private set; }

        public long NeuronsAtCeilingMost { get; private set; }

        public long BedAnglesPastGuard { get; private set; }

        public long HeldCells
        {
            get { long n = 0; foreach (ClassSet set in _classes) n += set.NoseHeld.Host.LongLength; return n; }
        }

        public long HeldBytesUp
        {
            get { long n = 0; foreach (ClassSet set in _classes) n += set.HeldBytesUp; return n; }
        }

        public long NoseFlagRows
        {
            get { long n = 0; foreach (ClassSet set in _classes) n += set.NoseFlag.Width; return n; }
        }

        // ---- the globals, by global slot
        private int _gcap = 16;
        private readonly List<IColumn> _gBody = new List<IColumn>();
        private readonly List<IColumn> _gLink = new List<IColumn>();
        private readonly List<IColumn> _gDown = new List<IColumn>();

        // The reach guard's records by global slot (WholeStep.LoseStrays), zeroed on the card at
        // each block's start and never uploaded, and its three counters.
        private readonly List<IColumn> _gStray = new List<IColumn>();
        private readonly Col<int> _strayCode;
        private readonly Col<long> _strayStep;
        private readonly Col<Real> _strayAt;
        private readonly Col<int> _strayCount;

        private readonly Col<int> _gLinks, _gId, _gRank, _gAlive, _gJointed;

        // D220's remains flag, by global slot: the host Creature.Remains, set every block (PackBlock).
        private readonly Col<int> _gRemains;
        private readonly Col<int> _nOver, _ovSlot, _ovId, _ovPart, _ovHeld, _nHeld, _heldId, _bedGlass;
        private readonly bool _rock;
        private readonly Col<int> _rockTouched;
        private readonly Col<Real>[] _rockP;
        private readonly Scratch<int> _touchNew;
        private readonly bool _grip;
        private readonly Col<int> _anchors, _gripHeld;
        private readonly Col<double> _gripBreak, _gripPrice, _gripStrength, _reserveLow, _gripOwed, _gripHeldSeconds;
        private readonly Col<Real> _gripAnchor, _gripAttitude, _gripPoint;
        private readonly Col<long> _gripsFormed, _gripsBroken;
        private readonly List<IColumn> _gripStubs = new List<IColumn>();
        private readonly Scratch<Real> _touchAt;

        // The overlap lists move by their first rows only. Every read of them, on the card
        // (NoteOverlapPart, the census, CommitOverlaps) and on the host (PackBlock, the mirror,
        // the events), stops at a body's count, so the rows past the largest NOver or NHeld
        // are never read: 64 rows of five lists were 25 MB each way a block at 8,400 bodies.
        private readonly HashSet<IColumn> _lists;

        private int ListRows()
        {
            int most = 0;
            for (int g = 0; g < _slots.Used; g++)
            {
                int n = Math.Max(_nOver.Host[g], _nHeld.Host[g]);
                if (n > most) most = n;
            }
            return Math.Min(most, _o.OverlapCap);
        }

        // Queued, as the block's other copies, and joined with them (Run, sections 5 and 8).
        private void ListsUp(int rows)
        {
            _ovSlot.UpRowsQueued(rows); _ovId.UpRowsQueued(rows); _ovPart.UpRowsQueued(2 * rows);
            _ovHeld.UpRowsQueued(rows); _heldId.UpRowsQueued(rows);
        }

        private void ListsDown(int rows)
        {
            _ovSlot.DownRowsQueued(rows); _ovId.DownRowsQueued(rows); _ovPart.DownRowsQueued(2 * rows);
            _ovHeld.DownRowsQueued(rows); _heldId.DownRowsQueued(rows);
        }
        private readonly Col<Real> _gTotalMass, _linkMass;
        private readonly Col<int> _rankToSlot;
        private readonly Col<long> _census, _overflow, _guard;

        // ---- two sphere sets: [set][Cx, Cy, Cz, R, Vx, Vy, Vz, LCx, LCy, LCz, LR, LVx, LVy, LVz, LEx, LEy, LEz,
        // LRot0 .. LRot8, LSp0 .. LSp2], the nine LRot columns a link row wide under D228's true shape and one cell
        // otherwise, and the three LSp columns (each link's spin, level (b)) a link row wide at TrueShapeAtPoint only
        private readonly Col<Real>[][] _sph = new Col<Real>[2][];

        // How many of a sphere set's columns a block copies: all 29 at TrueShapeAtPoint, the first 26 otherwise.
        private readonly int _sphCopied;

        // D228's cores by link row: the half-extents (3) and the margin, which a pack writes and a block
        // uploads only after one (_coreUp). A cell each unless the world is on the true shape.
        private readonly Col<Real>[] _core = new Col<Real>[4];
        private bool _coreUp;
        private readonly Col<int>[] _active = new Col<int>[2];
        private int _committed;

        // ---- the grid
        private readonly Scratch<int> _lo, _hi, _counts, _start, _gcursor, _items, _partialN, _tileSum, _tileStart;
        private readonly Scratch<int> _giant, _isGiant, _giantN, _giantList;

        // The giants' threshold (WholeStep.Ranges): 1,024 cells, or EVOSIM_GPU_GIANT_CELLS, where 0 makes no
        // row a giant. Result-neutral at any value, which is what the variable is for: a check runs a span at
        // 0, at the default and at 1, and the three must be the same bits.
        private static readonly int GiantCells = ReadGiantCells();

        private static int ReadGiantCells()
        {
            string v = Environment.GetEnvironmentVariable("EVOSIM_GPU_GIANT_CELLS");
            if (string.IsNullOrEmpty(v)) return 1024;
            int n = int.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture);
            if (n < 0) throw new ArgumentException("EVOSIM_GPU_GIANT_CELLS is " + n + "; it is 0 (no giants) or a count of cells.");
            return n == 0 ? int.MaxValue : n;
        }
        private readonly Scratch<int> _linkFlags, _stageN, _stageId, _stageSlot, _stagePart, _linkLimited;
        private readonly Scratch<Real> _linkPush, _linkFluid;
        private readonly Scratch<Real> _cell, _partial;
        private int _entryCap;
        private readonly int[] _one = new int[1];

        // ---- the world
        private readonly Col<Real> _inst, _wr, _stock;
        private readonly Col<int> _wi;
        private readonly Col<float> _scent;
        private readonly Col<float> _leaf;

        // The one brain's pools (card-audit-r57.md, the central brain), shared by every class: a
        // body's links and ports in a run of their own, placed at its pack. A slot keeps its runs
        // for the next body it holds; the pools are laid again from nothing when the runs left
        // behind outweigh the live ones.
        private readonly Col<int> _cSource, _cPortPart, _cPortChannel, _cPortIndex;
        private readonly Col<float> _cWeight;
        private int[] _cLinkAt = Array.Empty<int>(), _cLinkRoom = Array.Empty<int>();
        private int[] _cPortAt = Array.Empty<int>(), _cPortRoom = Array.Empty<int>();
        private int _cLinksTop, _cPortsTop;
        private bool _cDirty = true;

        public int PoolSlack { get; set; } = 4096;

        public long PoolRelays { get; private set; }

        public int LinkPoolCapacity => _cSource.Cap;

        // The scent's fill on the card (C3, GpuScent), when the farm hands it the world.
        private GpuScent _gpuScent;

        /// <summary>
        /// Takes the scent's fill from <paramref name="world"/> (EVOSIM_GPU_SCENT): the world hands
        /// its corpses to a GpuScent at each fill, the card fills the grid its kernels read, and the
        /// host's field is brought home only when a host reader asks (World.BringScentHome).
        /// </summary>
        public void FillScentOnCard(World world)
        {
            if (world?.Scent == null || _gpuScent != null) return;

            // The field is the engine's from now on, and not only once a body smells it: the block
            // sizes the column the fill writes from the engine's field, and a founded world steps
            // blocks before its first body is attached (2026-10-10, r58t1). A restored world had
            // attached it before its first block, so this changes nothing there.
            _w.AttachScent(world.Scent);
            _gpuScent = new GpuScent(_acc, _stream, world.Scent, _scent);
            world.FillScentElsewhere(_gpuScent.Snapshot, _gpuScent.BringHome);
        }
        private int _fieldVersion = -1;
        private readonly double[] _instD = new double[GpuWorld.InstStride];
        private double[] _stockD = new double[1];

        // ---- the kernels
        private readonly Action<Index1D, SSph, SGlob, SGrid, SCfg> _meanSerial, _ranges;

        // The chunked mean, one group a chunk (MeanGroups): a group of up to MeanGroupBodies threads.
        private readonly Action<KernelConfig, SSph, SGlob, SGrid, SCfg> _meanGroups;
        private readonly Action<Index1D, SGrid, SCfg> _meanCombine, _rearm, _clearCounts;
        private readonly Action<Index1D, SSph, SGlob, SGrid, SCfg> _loseStrays;
        private readonly Action<KernelConfig, SGrid, SCfg> _scanReduce, _scanTiles, _scanDown;
        private readonly Action<Index1D, SGlob, SGrid, SCfg> _scatter;
        private readonly Action<Index1D, SGrid, SCfg> _giantLists;
        private readonly Action<Index1D, SGlob, SCfg> _censusKernel;
        private readonly Action<Index1D, STopo, SState, SGlob, SWorld, SCfg> _water;
        private readonly Action<AcceleratorStream, Index1D, STopo, SState, SGlob, SWorld, SCfg> _waterOn;
        private readonly Action<Index1D, STopo, SConst, SGlob, SSph, SGrid, SWorld, SCfg> _contact;
        private readonly Action<Index1D, STopo, SConst, SState, SPanels, SGlob, SCfg> _fluid;
        private readonly Action<AcceleratorStream, Index1D, STopo, SConst, SState, SPanels, SGlob, SCfg> _fluidOn;
        private readonly Action<AcceleratorStream, Index1D, STopo, SConst, SGlob, SSph, SGrid, SWorld, SCfg> _contactOn;
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

        public Runner(Accelerator accelerator, AcceleratorStream stream, GpuWorld world, GpuOptions options)
        {
            _acc = accelerator;
            _stream = stream ?? accelerator.DefaultStream;
            _shared = !ReferenceEquals(_stream, accelerator.DefaultStream);
            _w = world;
            _o = options;
            _onCpu = accelerator.AcceleratorType == AcceleratorType.CPU;
            _slots = new GpuSlots(options.ClassLinks, options.ClassNeurons);

            _classes = new ClassSet[options.ClassLinks.Length];
            for (int k = 0; k < _classes.Length; k++)
            {
                _classes[k] = new ClassSet(accelerator, _stream, k, options.ClassLinks[k], options.ClassNeurons[k], 16, world.BendAndPower, world.ShapedMass, world.StrokeAssembly, world.MirroredDrive, world.Nose);
            }

            int oc = options.OverlapCap;
            _gLinks = G<int>(_gBody, 1, true); _gId = G<int>(_gBody, 1, false); _gRank = G<int>(_gBody, 1, false);
            _gAlive = G<int>(_gBody, 1, true); _gJointed = G<int>(_gBody, 1, false);
            _gRemains = G<int>(_gBody, 1, false);
            _gTotalMass = G<Real>(_gBody, 1, false);
            _linkMass = G<Real>(_gLink, 1, false);
            _nOver = G<int>(_gBody, 1, true); _ovSlot = G<int>(_gBody, oc, true, -1); _ovId = G<int>(_gBody, oc, true);
            _ovPart = G<int>(_gBody, 2 * oc, true, -1); _ovHeld = G<int>(_gBody, oc, true);
            _nHeld = G<int>(_gBody, 1, true); _heldId = G<int>(_gBody, oc, true); _bedGlass = G<int>(_gBody, 1, true);
            _lists = new HashSet<IColumn> { _ovSlot, _ovId, _ovPart, _ovHeld, _heldId };
            _strayCode = G<int>(_gStray, 1, true); _strayStep = G<long>(_gStray, 1, true);

            // D162's touch of the rock, kept by link row and brought home with the body, only in a
            // world that records it; elsewhere a cell each, outside the lists, which no copy moves.
            _rock = world.RockTouch;
            _rockTouched = _rock ? G<int>(_gLink, 1, true) : new Col<int>(_acc, _stream, 1, 1);
            _rockP = new Col<Real>[6];
            for (int k = 0; k < 6; k++) _rockP[k] = _rock ? G<Real>(_gLink, 1, true) : new Col<Real>(_acc, _stream, 1, 1);
            // D162's grip, by link row and by global slot, kept on the card and brought home with the
            // body, only in a world whose table holds the anchoring cell; elsewhere a cell each.
            _grip = world.AnchorCell != null;
            _anchors = GripCol<int>(_gLink, 1, false);
            _gripBreak = GripCol<double>(_gLink, 1, false);
            _gripPrice = GripCol<double>(_gLink, 1, false);
            _gripHeld = GripCol<int>(_gLink, 1, true);
            _gripStrength = GripCol<double>(_gLink, 1, true);
            _gripAnchor = GripCol<Real>(_gLink, 3, true);
            _gripAttitude = GripCol<Real>(_gLink, 9, true);
            _gripPoint = GripCol<Real>(_gLink, 3, true);
            _reserveLow = GripCol<double>(_gBody, 1, true);
            _gripOwed = GripCol<double>(_gBody, 1, true);
            _gripHeldSeconds = GripCol<double>(_gBody, 1, true);
            _gripsFormed = GripCol<long>(_gBody, 1, true);
            _gripsBroken = GripCol<long>(_gBody, 1, true);
            _strayAt = G<Real>(_gStray, 4, true);
            _strayCount = new Col<int>(_acc, _stream, 1, 3);

            _rankToSlot = new Col<int>(_acc, _stream, 1, 16, -1);
            _census = new Col<long>(_acc, _stream, 1, WholeStep.CensusFields * GpuBackend.BlockCeiling);
            _overflow = new Col<long>(_acc, _stream, 1, 8);
            _guard = new Col<long>(_acc, _stream, 1, 1);

            for (int k = 0; k < 4; k++) _core[k] = new Col<Real>(_acc, _stream, 1, world.TrueShape ? WholeStep.LinkStride * _gcap : 1);

            _sphCopied = world.AtPoint ? 29 : 26;

            for (int set = 0; set < 2; set++)
            {
                var columns = new Col<Real>[29];
                for (int k = 0; k < 7; k++) columns[k] = new Col<Real>(_acc, _stream, 1, _gcap);
                for (int k = 7; k < 17; k++) columns[k] = new Col<Real>(_acc, _stream, 1, WholeStep.LinkStride * _gcap);
                for (int k = 17; k < 26; k++) columns[k] = new Col<Real>(_acc, _stream, 1, world.TrueShape ? WholeStep.LinkStride * _gcap : 1);
                for (int k = 26; k < 29; k++) columns[k] = new Col<Real>(_acc, _stream, 1, world.AtPoint ? WholeStep.LinkStride * _gcap : 1);
                _sph[set] = columns;
                _active[set] = new Col<int>(_acc, _stream, 1, _gcap);
            }

            _lo = new Scratch<int>(_acc, 3 * 16);
            _giant = new Scratch<int>(_acc, 1 + WholeStep.GiantCap);
            _isGiant = new Scratch<int>(_acc, 16);
            _giantN = new Scratch<int>(_acc, WholeStep.GiantCap);
            _giantList = new Scratch<int>(_acc, WholeStep.GiantCap * WholeStep.GiantListCap);
            _hi = new Scratch<int>(_acc, 3 * 16);
            _counts = new Scratch<int>(_acc, 17);
            _start = new Scratch<int>(_acc, 17);
            _gcursor = new Scratch<int>(_acc, 17);
            _tileSum = new Scratch<int>(_acc, 1);
            _tileStart = new Scratch<int>(_acc, 1);
            _linkPush = new Scratch<Real>(_acc, world.AtPoint ? 6 : 3);
            _linkFluid = new Scratch<Real>(_acc, 20);   // rows 17-19: D199/D200's force cut
            _linkLimited = new Scratch<int>(_acc, 1);
            _linkFlags = new Scratch<int>(_acc, 1);
            _touchNew = new Scratch<int>(_acc, 1);
            _touchAt = new Scratch<Real>(_acc, 6);
            _stageN = new Scratch<int>(_acc, 1);
            _stageId = new Scratch<int>(_acc, WholeStep.StageCap);
            _stageSlot = new Scratch<int>(_acc, WholeStep.StageCap);
            _stagePart = new Scratch<int>(_acc, WholeStep.StageCap);
#if GPU_PHASE_PROBE
            _phase = new Scratch<long>(_acc, 1);
            _linkPhase = new Scratch<long>(_acc, 1);
#endif
            _entryCap = 1024;
            _items = new Scratch<int>(_acc, _entryCap);
            _partial = new Scratch<Real>(_acc, 1);
            _partialN = new Scratch<int>(_acc, 1);
            _cell = new Scratch<Real>(_acc, 1);

            _inst = new Col<Real>(_acc, _stream, 1, GpuWorld.InstStride * GpuBackend.BlockCeiling);
            _wr = new Col<Real>(_acc, _stream, 1, world.WR.Length);
            for (int k = 0; k < world.WR.Length; k++) _wr.Host[k] = (Real)world.WR[k];
            _wr.Up();
            _wi = new Col<int>(_acc, _stream, 1, 1);
            _stock = new Col<Real>(_acc, _stream, 1, 1);
            _scent = new Col<float>(_acc, _stream, 1, 1);
            _leaf = new Col<float>(_acc, _stream, 1, 1);
            _cSource = new Col<int>(_acc, _stream, 1, 1); _cPortPart = new Col<int>(_acc, _stream, 1, 1);
            _cPortChannel = new Col<int>(_acc, _stream, 1, 1); _cPortIndex = new Col<int>(_acc, _stream, 1, 1);
            _cWeight = new Col<float>(_acc, _stream, 1, 1);

            _scanGroup = Math.Max(1, Math.Min(1024, accelerator.MaxNumThreadsPerGroup));

            // The tile's group is a power of two, for the first pass's tree.
            _tileGroup = 1;
            while (_tileGroup * 2 <= Math.Min(WholeStep.ScanGroupCap, accelerator.MaxNumThreadsPerGroup)) _tileGroup <<= 1;

            _base = BaseConfig(world, options);

            // ---- the kernels: every launch shape compiled before the first step is timed.
            var watch = Stopwatch.StartNew();
            bool grouped = !_onCpu && options.GroupSize > 0;
            int g = options.GroupSize;
            GroupSize = _onCpu ? accelerator.MaxNumThreadsPerGroup : g;

            _meanSerial = On(_acc.LoadAutoGroupedKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.MeanSerial));
            _meanCombine = On(_acc.LoadAutoGroupedKernel<Index1D, SGrid, SCfg>(WholeStep.MeanCombine));
            _rearm = On(_acc.LoadAutoGroupedKernel<Index1D, SGrid, SCfg>(WholeStep.Rearm));
            _clearCounts = On(_acc.LoadAutoGroupedKernel<Index1D, SGrid, SCfg>(WholeStep.ClearCounts));
            _giantLists = On(_acc.LoadAutoGroupedKernel<Index1D, SGrid, SCfg>(WholeStep.GiantLists));
            _loseStrays = On(_acc.LoadAutoGroupedKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.LoseStrays));
            _scanReduce = On(_acc.LoadKernel<SGrid, SCfg>(WholeStep.ScanReduce));
            _scanTiles = On(_acc.LoadKernel<SGrid, SCfg>(WholeStep.ScanTiles));
            _scanDown = On(_acc.LoadKernel<SGrid, SCfg>(WholeStep.ScanDown));
            _meanGroups = On(_acc.LoadKernel<SSph, SGlob, SGrid, SCfg>(WholeStep.MeanGroups));
            _meanGroup = Math.Max(1, Math.Min(WholeStep.MeanGroupBodies, accelerator.MaxNumThreadsPerGroup));

            if (grouped)
            {
                _ranges = On(_acc.LoadImplicitlyGroupedKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.Ranges, g));
                _scatter = On(_acc.LoadImplicitlyGroupedKernel<Index1D, SGlob, SGrid, SCfg>(WholeStep.Scatter, g));
                _censusKernel = On(_acc.LoadImplicitlyGroupedKernel<Index1D, SGlob, SCfg>(WholeStep.Census, g));
                _water = On(_acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SState, SGlob, SWorld, SCfg>(WholeStep.WaterLinks, g));
                _contact = On(_acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SGlob, SSph, SGrid, SWorld, SCfg>(WholeStep.ContactLinks, g));
                _fluid = On(_acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SState, SPanels, SGlob, SCfg>(WholeStep.FluidLinks, g));
            }
            else
            {
                _ranges = On(_acc.LoadAutoGroupedKernel<Index1D, SSph, SGlob, SGrid, SCfg>(WholeStep.Ranges));
                _scatter = On(_acc.LoadAutoGroupedKernel<Index1D, SGlob, SGrid, SCfg>(WholeStep.Scatter));
                _censusKernel = On(_acc.LoadAutoGroupedKernel<Index1D, SGlob, SCfg>(WholeStep.Census));
                _water = On(_acc.LoadAutoGroupedKernel<Index1D, STopo, SState, SGlob, SWorld, SCfg>(WholeStep.WaterLinks));
                _contact = On(_acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SGlob, SSph, SGrid, SWorld, SCfg>(WholeStep.ContactLinks));
                _fluid = On(_acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SState, SPanels, SGlob, SCfg>(WholeStep.FluidLinks));
            }

            _step = new Action<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain, STrace, SGlob,
                SSph, SSph, SGrid, SWorld, SCfg>[KernelGenerator.ClassCount];
            _concurrent = options.Concurrent;
            _stepOn = new Action<AcceleratorStream, Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain, STrace,
                SGlob, SSph, SSph, SGrid, SWorld, SCfg>[KernelGenerator.ClassCount];
            _streams = new AcceleratorStream[KernelGenerator.ClassCount];
            if (_concurrent)
            {
                _waterOn = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SState, SGlob, SWorld, SCfg>(WholeStep.WaterLinks, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SState, SGlob, SWorld, SCfg>(WholeStep.WaterLinks);
                _contactOn = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SGlob, SSph, SGrid, SWorld, SCfg>(WholeStep.ContactLinks, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SGlob, SSph, SGrid, SWorld, SCfg>(WholeStep.ContactLinks);
                _fluidOn = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SState, SPanels, SGlob, SCfg>(WholeStep.FluidLinks, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SState, SPanels, SGlob, SCfg>(WholeStep.FluidLinks);
            }

            _step[0] = On(grouped
                ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step0, g)
                : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step0));
            if (_concurrent)
            {
                _stepOn[0] = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step0, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step0);
                _streams[0] = _acc.CreateStream();
            }
            _step[1] = On(grouped
                ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step1, g)
                : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step1));
            if (_concurrent)
            {
                _stepOn[1] = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step1, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step1);
                _streams[1] = _acc.CreateStream();
            }
            _step[2] = On(grouped
                ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step2, g)
                : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step2));
            if (_concurrent)
            {
                _stepOn[2] = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step2, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step2);
                _streams[2] = _acc.CreateStream();
            }
            _step[3] = On(grouped
                ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step3, g)
                : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                    STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step3));
            if (_concurrent)
            {
                _stepOn[3] = grouped
                    ? _acc.LoadImplicitlyGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step3, g)
                    : _acc.LoadAutoGroupedKernel<Index1D, STopo, SConst, SPanels, SState, SDrive, SNeur, SBrain,
                        STrace, SGlob, SSph, SSph, SGrid, SWorld, SCfg>(WholeStep.Step3);
                _streams[3] = _acc.CreateStream();
            }

            Sync();
            CompileMs = watch.Elapsed.TotalMilliseconds;
        }

        // A kernel in its stream-taking form, launched on this engine's stream: on the default stream
        // the very launch the stream-kernel loaders make (Runner's _stream). Each wrapper's kernel is kept
        // for the probe's shape lines, which read the kernel object behind a launcher.
        private readonly Dictionary<Delegate, Delegate> _raw = new Dictionary<Delegate, Delegate>();

        private Delegate Raw(Delegate launcher) => _raw.TryGetValue(launcher, out Delegate k) ? k : launcher;

        private Action<T1, T2, T3> On<T1, T2, T3>(Action<AcceleratorStream, T1, T2, T3> k)
        {
            Action<T1, T2, T3> f = (a1, a2, a3) => k(_stream, a1, a2, a3);
            _raw[f] = k;
            return f;
        }
        private Action<T1, T2, T3, T4> On<T1, T2, T3, T4>(Action<AcceleratorStream, T1, T2, T3, T4> k)
        {
            Action<T1, T2, T3, T4> f = (a1, a2, a3, a4) => k(_stream, a1, a2, a3, a4);
            _raw[f] = k;
            return f;
        }
        private Action<T1, T2, T3, T4, T5> On<T1, T2, T3, T4, T5>(Action<AcceleratorStream, T1, T2, T3, T4, T5> k)
        {
            Action<T1, T2, T3, T4, T5> f = (a1, a2, a3, a4, a5) => k(_stream, a1, a2, a3, a4, a5);
            _raw[f] = k;
            return f;
        }
        private Action<T1, T2, T3, T4, T5, T6> On<T1, T2, T3, T4, T5, T6>(Action<AcceleratorStream, T1, T2, T3, T4, T5, T6> k)
        {
            Action<T1, T2, T3, T4, T5, T6> f = (a1, a2, a3, a4, a5, a6) => k(_stream, a1, a2, a3, a4, a5, a6);
            _raw[f] = k;
            return f;
        }
        private Action<T1, T2, T3, T4, T5, T6, T7> On<T1, T2, T3, T4, T5, T6, T7>(Action<AcceleratorStream, T1, T2, T3, T4, T5, T6, T7> k)
        {
            Action<T1, T2, T3, T4, T5, T6, T7> f = (a1, a2, a3, a4, a5, a6, a7) => k(_stream, a1, a2, a3, a4, a5, a6, a7);
            _raw[f] = k;
            return f;
        }
        private Action<T1, T2, T3, T4, T5, T6, T7, T8> On<T1, T2, T3, T4, T5, T6, T7, T8>(Action<AcceleratorStream, T1, T2, T3, T4, T5, T6, T7, T8> k)
        {
            Action<T1, T2, T3, T4, T5, T6, T7, T8> f = (a1, a2, a3, a4, a5, a6, a7, a8) => k(_stream, a1, a2, a3, a4, a5, a6, a7, a8);
            _raw[f] = k;
            return f;
        }
        private Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> On<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Action<AcceleratorStream, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> k)
        {
            Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15> f = (a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15) => k(_stream, a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15);
            _raw[f] = k;
            return f;
        }

        /// <summary>
        /// Waits for this engine's work: the device, as it always did, when the engine is alone on it; on
        /// a shared device, or with the scent filled on the card, its own stream and its class streams,
        /// and never another world's kernels. The scent's fill stream is left out: the block joins it
        /// in section 5, so under the probe the wait on the fill is timed there and not in whichever
        /// section happened to sync first.
        /// </summary>
        private void Sync()
        {
            if (!_shared && _gpuScent == null)
            {
                _acc.Synchronize();
                return;
            }

            _stream.Synchronize();
            foreach (AcceleratorStream s in _streams)
            {
                s?.Synchronize();
            }
        }

        private Col<T> G<T>(List<IColumn> list, int width, bool down, T fill = default) where T : unmanaged
        {
            int cap = ReferenceEquals(list, _gLink) ? WholeStep.LinkStride * _gcap : _gcap;
            var column = new Col<T>(_acc, _stream, width, cap, fill);
            list.Add(column);
            if (down) _gDown.Add(column);
            return column;
        }

        private Col<T> GripCol<T>(List<IColumn> list, int width, bool down) where T : unmanaged
        {
            if (_grip) return G<T>(list, width, down);

            var stub = new Col<T>(_acc, _stream, width, 1);
            _gripStubs.Add(stub);
            return stub;
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
                LimitDrive = w.LimitDrive ? 1 : 0, LimitDrag = w.LimitDrag ? 1 : 0, WeldedDrag = w.WeldedDrag ? 1 : 0,
                UseRestore = w.UseRestore ? 1 : 0, FloorRestores = w.FloorRestores ? 1 : 0,
                CreatureContact = w.CreatureContact ? 1 : 0, OffsetTorque = w.OffsetTorque ? 1 : 0,
                ShapedMass = w.ShapedMass ? 1 : 0,
                MirrorDrive = w.MirroredDrive ? 1 : 0, Nose = w.Nose ? 1 : 0,
                TrueShape = w.TrueShape ? 1 : 0, AtPoint = w.AtPoint ? 1 : 0,
                StrokeAssembly = w.StrokeAssembly ? 1 : 0,
                BendAndPower = w.BendAndPower ? 1 : 0, TurningDrag = w.TurningDrag, RockTouch = w.RockTouch ? 1 : 0,
                Anchoring = w.AnchorCell != null ? 1 : 0,
                GripDecay = w.GripDecay, GripThreshold = w.GripThreshold, GripLag = w.GripLag,

                Paced = w.Paced ? 1 : 0, Pace = (Real)w.Pace, PaceSquared = (Real)w.PaceSquared,
                DtD = w.Dt, PoseOmega = w.PoseOmega,
                PushOmega = (Real)w.PushOmega, LimbPush = (Real)w.LimbPush,
                NegSkin = (Real)w.NegSkin, NegSpin = (Real)w.NegSpin,
                Dt = (Real)w.Dt, Density = (Real)w.Density, DragK = (Real)w.DragK, AddedMass = (Real)w.AddedMass,
                FluidAccel = (Real)w.FluidAccel, Gravity = (Real)w.Gravity, WorldDepth = (Real)w.WorldDepth,
                Damping = (Real)w.Damping, SpinBudget = (Real)w.SpinBudget,
                RestoringDensity = (Real)w.RestoringDensity, TissueDensity = (Real)w.TissueDensity,

                Omega = (Real)w.Omega, Zeta = (Real)w.Zeta, MaxSep = (Real)w.MaxSep, MaxDv = (Real)w.MaxDv,
                TankRadius = (Real)w.TankRadius, AxisX = (Real)w.AxisX, AxisZ = (Real)w.AxisZ, CellOverride = 0,
                SeamL = (Real)w.SeamLength, SeamW = (Real)w.SeamWidth,
                BedRadius = (Real)w.BedRadius, BedDepth = (Real)w.BedDepth, TiltX = (Real)w.TiltX,
                TiltZ = (Real)w.TiltZ, Offset = (Real)w.Offset, ShoalHeight = (Real)w.ShoalHeight,

                CurDepth = (Real)w.CurDepth, CurTankR = (Real)w.CurTankR, EddyWeight = (Real)w.EddyWeight,
                Overturning = (Real)w.Overturning, PerSecond = (Real)w.PerSecond, Squared = (Real)w.Squared,
                ShoreDepth = (Real)w.ShoreDepth, ShoreFadeW = (Real)w.ShoreFadeW,
                HalfThickness = (Real)w.HalfThickness, Fillet = (Real)w.Fillet, ReefFade = (Real)w.ReefFade,
                CoralCount = w.CoralCount, CoralCellsX = w.CoralCellsX, CoralCellsZ = w.CoralCellsZ,
                CoralColonyAt = w.CoralColonyAt, CoralPieceAt = w.CoralPieceAt, CoralGridAt = w.CoralGridAt,
                CoralListAt = w.CoralListAt, CoralFadeGridAt = w.CoralFadeGridAt, CoralFadeListAt = w.CoralFadeListAt,
                CoralFillet = (Real)w.CoralFillet, CoralFade = (Real)w.CoralFade,
                CoralFloor = (Real)w.CoralFloor, CoralCell = (Real)w.CoralCell,
                CoralPeriodX = (Real)w.CoralPeriodX, CoralPeriodZ = (Real)w.CoralPeriodZ,
                SigmaSloped = w.SigmaSloped, SigmaFlat = w.SigmaFlat, Speed = w.Speed,
                Ocean = w.Ocean ? 1 : 0, OceanL = w.OceanL, OceanW = w.OceanW, OceanKAt = w.OceanKAt,

                SWorldDepth = w.SWorldDepth, ChemHalf = w.ChemHalf, ScentHalf = w.ScentHalf, EnergyScale = w.EnergyScale,
                LeafHalf = w.LeafHalf,
                EnergyHalfMode = w.EnergyHalfMode, EnergyHalfK = w.EnergyHalfK,
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
        //
        // From the start under EVOSIM_GPU_PROBE=1, or switched on while the world runs by the farm's pace guard
        // (Probe), which reads the last print back as lines (ProbeLines). It is switched between blocks, never
        // inside one, so a block's joins and marks are all on or all off.
        private bool ProbeOn = Environment.GetEnvironmentVariable("EVOSIM_GPU_PROBE") == "1";
        private readonly List<string> _probeLines = new List<string>();
        private readonly List<string> _probeLast = new List<string>();

        public bool Probe
        {
            get => ProbeOn;
            set
            {
                if (value && !ProbeOn) _probeBlocks = 0;
                ProbeOn = value;
            }
        }

        public IReadOnlyList<string> ProbeLines => _probeLast;

        // A print's line, written as before and kept for ProbeLines; the print's first line starts a new set.
        private void ProbeLine(string line)
        {
            GpuConsole.Error.WriteLine(line);
            _probeLines.Add(line);
        }

        private void ProbeLinesDone()
        {
            _probeLast.Clear();
            _probeLast.AddRange(_probeLines);
            _probeLines.Clear();
        }
        private readonly long[] _probeClass = new long[4];
        private readonly long[] _probeGridPart = new long[5];
        private int _probeRows, _probeBuckets;
#if GPU_PHASE_PROBE
        private readonly Scratch<long> _phase;
        private long[] _phaseHost = new long[1];
        private readonly double[] _phaseMean = new double[4 * WholeStep.Phases];
        private readonly double[] _phaseSlow = new double[4 * WholeStep.Phases];
        private readonly long[] _phaseBodies = new long[4];
        private readonly int[] _phaseBlocks = new int[4];
        private readonly int[] _phaseSlowLinks = new int[4];
        private static readonly string[] PhaseNames =
            { "load", "water", "brain", "drive", "fluid", "contacts", "solve", "poses", "spheres", "store" };

        // After a block's last step: each class's bodies' cycles a part, summed, and its slowest
        // body's (the most cycles in all), summed over blocks.
        private void PhaseRead()
        {
            Sync();
            int P = WholeStep.Phases;
            int n = P * _gcap;
            if (_phaseHost.Length != n) _phaseHost = new long[n];
            _phase.View.SubView(0, n).CopyToCPU(_stream, _phaseHost);

            var slowTotal = new long[4];
            var slowG = new int[] { -1, -1, -1, -1 };
            for (int g = 0; g < _slots.Used; g++)
            {
                Creature body = _slots.Body[g];
                if (body == null || !body.Alive) continue;
                int c = _slots.ClassOf[g];
                long total = 0;
                for (int p = 0; p < P; p++)
                {
                    long v = _phaseHost[p * _gcap + g];
                    _phaseMean[c * P + p] += v;
                    total += v;
                }
                _phaseBodies[c]++;
                if (total > slowTotal[c]) { slowTotal[c] = total; slowG[c] = g; }
            }

            for (int c = 0; c < 4; c++)
            {
                if (slowG[c] < 0) continue;
                for (int p = 0; p < P; p++) _phaseSlow[c * P + p] += _phaseHost[p * _gcap + slowG[c]];
                _phaseBlocks[c]++;
                _phaseSlowLinks[c] = Math.Max(_phaseSlowLinks[c], _slots.Body[slowG[c]].Links);
            }

            LinkPhaseRead();
        }

        private readonly Scratch<long> _linkPhase;
        private long _linkPhaseRows;
        private long[] _linkPhaseHost = new long[1];
        private readonly double[] _linkMean = new double[4 * WholeStep.LinkParts];
        private readonly double[] _linkSlow = new double[4 * WholeStep.LinkParts];
        private readonly long[] _linkCount = new long[4];
        private readonly int[] _linkBlocks = new int[4];
        private static readonly string[] LinkPartNames =
            { "read", "velocity", "acceleration", "read", "panels", "terms", "store" };

        // After a block's last step: each class's links' cycles a part in the water and fluid
        // passes, summed, and its slowest link's in each pass (the most cycles in that pass),
        // summed over blocks. A tank with no current leaves the water's parts as they were.
        private void LinkPhaseRead()
        {
            int P = WholeStep.LinkParts, W = WholeStep.WaterParts;
            long rows = _linkPhaseRows;
            if (rows == 0) return;
            long n = P * rows;
            if (_linkPhaseHost.Length != n) _linkPhaseHost = new long[n];
            _linkPhase.View.SubView(0, n).CopyToCPU(_stream, _linkPhaseHost);

            var slowWater = new long[4];
            var slowFluid = new long[4];
            var slowWaterRow = new long[] { -1, -1, -1, -1 };
            var slowFluidRow = new long[] { -1, -1, -1, -1 };
            for (int g = 0; g < _slots.Used; g++)
            {
                Creature body = _slots.Body[g];
                if (body == null || !body.Alive) continue;
                int c = _slots.ClassOf[g];
                for (int l = 0; l < body.Links; l++)
                {
                    long row = (long)g * WholeStep.LinkStride + l;
                    if (row >= rows) break;
                    long water = 0, fluid = 0;
                    for (int p = 0; p < P; p++)
                    {
                        long v = _linkPhaseHost[p * rows + row];
                        _linkMean[c * P + p] += v;
                        if (p < W) water += v; else fluid += v;
                    }
                    _linkCount[c]++;
                    if (water > slowWater[c]) { slowWater[c] = water; slowWaterRow[c] = row; }
                    if (fluid > slowFluid[c]) { slowFluid[c] = fluid; slowFluidRow[c] = row; }
                }
            }

            for (int c = 0; c < 4; c++)
            {
                if (slowFluidRow[c] < 0) continue;
                for (int p = 0; p < P; p++)
                {
                    long row = p < W ? slowWaterRow[c] : slowFluidRow[c];
                    if (row >= 0) _linkSlow[c * P + p] += _linkPhaseHost[p * rows + row];
                }
                _linkBlocks[c]++;
            }
        }

        private void PrintLinkPhases()
        {
            int P = WholeStep.LinkParts, W = WholeStep.WaterParts;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int c = 0; c < 4; c++)
            {
                if (_linkBlocks[c] == 0) continue;
                var text = new System.Text.StringBuilder();
                foreach (bool slowest in new[] { false, true })
                {
                    for (int pass = 0; pass < 2; pass++)
                    {
                        int from = pass == 0 ? 0 : W, to = pass == 0 ? W : P;
                        var parts = new System.Text.StringBuilder();
                        double sum = 0;
                        for (int p = from; p < to; p++)
                        {
                            double v = slowest
                                ? _linkSlow[c * P + p] / _linkBlocks[c] / 1000.0
                                : _linkMean[c * P + p] / Math.Max(1, _linkCount[c]) / 1000.0;
                            sum += v;
                            parts.Append(string.Format(inv, "{0}{1} {2:0.00}", p == from ? "" : ", ", LinkPartNames[p], v));
                        }
                        text.Append(string.Format(inv, "{0}{1} {2:0.00} ({3})",
                            text.Length == 0 ? "" : "; ", (slowest ? "the slowest link's " : "the mean link's ") +
                            (pass == 0 ? "water" : "fluid"), sum, parts));
                    }
                }
                ProbeLine(string.Format(inv,
                    "gpu-probe link phases, class {0}, kilocycles a step over {1:0} links a block: {2}",
                    c, _linkCount[c] / (double)_linkBlocks[c], text));
            }
            System.Array.Clear(_linkMean, 0, _linkMean.Length);
            System.Array.Clear(_linkSlow, 0, _linkSlow.Length);
            System.Array.Clear(_linkCount, 0, 4);
            System.Array.Clear(_linkBlocks, 0, 4);
        }

        private void PrintPhases()
        {
            int P = WholeStep.Phases;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int c = 0; c < 4; c++)
            {
                if (_phaseBlocks[c] == 0) continue;
                var mean = new System.Text.StringBuilder();
                var slow = new System.Text.StringBuilder();
                double meanSum = 0, slowSum = 0;
                for (int p = 0; p < P; p++)
                {
                    double m = _phaseMean[c * P + p] / Math.Max(1, _phaseBodies[c]) / 1000.0;
                    double s = _phaseSlow[c * P + p] / _phaseBlocks[c] / 1000.0;
                    meanSum += m; slowSum += s;
                    mean.Append(string.Format(inv, "{0}{1} {2:0.0}", p == 0 ? "" : ", ", PhaseNames[p], m));
                    slow.Append(string.Format(inv, "{0}{1} {2:0.0}", p == 0 ? "" : ", ", PhaseNames[p], s));
                }
                ProbeLine(string.Format(inv,
                    "gpu-probe phases, class {0}, kilocycles a step: the mean body {1:0.0} ({2}); the slowest body, up to {3} links, {4:0.0} ({5})",
                    c, meanSum, mean, _phaseSlowLinks[c], slowSum, slow));
            }
            System.Array.Clear(_phaseMean, 0, _phaseMean.Length);
            System.Array.Clear(_phaseSlow, 0, _phaseSlow.Length);
            System.Array.Clear(_phaseBodies, 0, 4);
            System.Array.Clear(_phaseBlocks, 0, 4);
            System.Array.Clear(_phaseSlowLinks, 0, 4);
            PrintLinkPhases();
        }
#endif

        // The grid's own parts, joined one by one: the zeroing, the mean, the ranges, the scan and
        // the scatter. Their sum is the grid's time, so the grid term is added here as well.
        private void GridPart(int k, ref long pt)
        {
            Sync();
            long pn = Stopwatch.GetTimestamp();
            _probeGridPart[k] += pn - pt;
            _probeGrid += pn - pt;
            pt = pn;
        }
        private long _probeGrid, _probeTail, _probeSteps, _probeWater, _probeContact;

        // The water and fluid passes apart, a class at a time, each launch joined when the classes
        // share a stream. Their sum is the passes' time, so the passes' term is added here as well.
        private readonly long[] _probeWaterClass = new long[4];
        private readonly long[] _probeFluidClass = new long[4];
        private void PassPart(long[] part, int c, ref long pt)
        {
            Sync();
            long pn = Stopwatch.GetTimestamp();
            part[c] += pn - pt;
            _probeWater += pn - pt;
            pt = pn;
        }

        // The host's side of a block: the slots, the packs of new and resized bodies, every
        // body's block pack, the world's snow and instants, the uploads; then the downloads and
        // the mirror, which land one block behind the print.
        private readonly long[] _probeHost = new long[7];
        private readonly long[] _probeBytes = new long[7];

        // The scent grid's share of those: its copy from the host's field (in snow and instants)
        // and its upload (in up), with the upload's bytes.
        private readonly long[] _probeScent = new long[2];
        private long _probeScentBytes;

        // The copies themselves (W0, card-next-r57.md Part 8), from the thread's CopyTally: the up
        // section's, the down section's and the rest of the block's, each a count and the bytes
        // moved; the bytes the up and down copies would move if each stopped at the used slots;
        // and each class's capacity and used slots at the last block.
        private readonly long[] _probeCopies = new long[3], _probeCopyBytes = new long[3], _probeWaits = new long[3];
        private long _waitMark;
        private readonly long[] _probeUsedBytes = new long[2];
        private readonly int[] _probeCap = new int[4], _probeClassUsed = new int[4];

        private void CopyMark(int k, ref long copies, ref long bytes)
        {
            _probeCopies[k] += CopyTally.Copies - copies;
            _probeCopyBytes[k] += CopyTally.Bytes - bytes;
            _probeWaits[k] += CopyTally.Waits - _waitMark;
            _waitMark = CopyTally.Waits;
            copies = CopyTally.Copies;
            bytes = CopyTally.Bytes;
        }

        // A global column's used slots in its own units: a body column holds a slot a row, a link
        // column LinkStride of them, so the used part is the used slots times the column's
        // capacity over the bodies' capacity.
        private int UsedUnits(IColumn c, int gUsed) => gUsed * Math.Max(1, c.Cap / Math.Max(1, _gcap));

        // Whether each host array is page-locked, counted once at the first print: the globals by
        // their lists, the sphere sets, the classes, and the named columns the up section sends.
        private string LockedCount()
        {
            var columns = new List<IColumn>();
            columns.AddRange(_gBody);
            columns.AddRange(_gLink);
            columns.AddRange(_gStray);
            columns.AddRange(_core);
            for (int set = 0; set < 2; set++)
            {
                columns.AddRange(_sph[set]);
                columns.Add(_active[set]);
            }
            foreach (ClassSet cs in _classes) columns.AddRange(cs.Columns);
            columns.AddRange(new IColumn[]
            {
                _rankToSlot, _census, _overflow, _guard, _stock, _scent, _inst, _wr, _wi,
                _cSource, _cWeight, _cPortPart, _cPortChannel, _cPortIndex, _strayCount,
            });
            int locked = 0;
            foreach (IColumn c in columns) if (c.Locked) locked++;
            return locked + " of " + columns.Count;
        }

        private void HostMark(int k, ref long th)
        {
            long now = Stopwatch.GetTimestamp();
            _probeHost[k] += now - th;
            th = now;
        }
        private int _probeBlocks;

        // The last block's launch shapes, for the kernels' own lines (KernelShape), printed once
        // at the first print: each class's bodies and link threads, the slots, rows, chunks and
        // tiles, and the launches a step.
        private readonly int[] _probeUsed = new int[4], _probeLinkThreads = new int[4];
        private int _probeGUsed, _probeChunks, _probeTiles;
        private double _probeLaunches;
        private bool _probeShaped;

        private void PrintShape()
        {
            int g = !_onCpu && _o.GroupSize > 0 ? _o.GroupSize : 0;
            var lines = new List<string>
            {
                string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "gpu-probe card: {0}, {1} SMs, {2} threads a SM at most, group {3}",
                    _acc.Name, _acc.NumMultiprocessors, _acc.MaxNumThreadsPerMultiprocessor, g > 0 ? g.ToString() : "auto"),
            };
            for (int c = 0; c < _classes.Length; c++)
            {
                if (_probeUsed[c] == 0) continue;
                if (_base.HasCurrent != 0) lines.Add(KernelShape.Describe(_acc, "water " + c, Raw(_water), g, _probeLinkThreads[c]));
                lines.Add(KernelShape.Describe(_acc, "fluid " + c, Raw(_fluid), g, _probeLinkThreads[c]));
                if (_base.PerPart != 0) lines.Add(KernelShape.Describe(_acc, "contact " + c, Raw(_contact), g, _probeLinkThreads[c]));
                lines.Add(KernelShape.Describe(_acc, "step " + c, Raw(_step[c]), g, _probeUsed[c]));
            }
            lines.Add(KernelShape.Describe(_acc, "mean groups", Raw(_meanGroups), _meanGroup, (long)_probeChunks * _meanGroup));
            lines.Add(KernelShape.Describe(_acc, "ranges", Raw(_ranges), g, _probeRows));
            lines.Add(KernelShape.Describe(_acc, "lose strays", Raw(_loseStrays), g, _probeGUsed));
            lines.Add(KernelShape.Describe(_acc, "scan reduce", Raw(_scanReduce), _tileGroup, (long)_probeTiles * _tileGroup));
            lines.Add(KernelShape.Describe(_acc, "scan tiles", Raw(_scanTiles), _scanGroup, _scanGroup));
            lines.Add(KernelShape.Describe(_acc, "scatter", Raw(_scatter), g, _probeRows));
            lines.Add(KernelShape.Describe(_acc, "census", Raw(_censusKernel), g, _probeGUsed));
            foreach (string line in lines) ProbeLine(line.StartsWith("gpu-probe") ? line : "gpu-probe kernel " + line);
            ProbeLine(KernelShape.LaunchCost(_acc, g > 0 ? g : 32, _probeLaunches));
            ProbeLine("gpu-probe host arrays page-locked: " + LockedCount() + " columns");
        }

        private void PrintProbe(DynamicsWorld world, IReadOnlyList<Creature> list)
        {
            if (!_probeShaped)
            {
                _probeShaped = true;
                PrintShape();
            }

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
            ProbeLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "gpu-probe" + (_concurrent ? " concurrent (the classes' time together in the first)" : "") +
                " t={0:0} s, ms a step: grid {1:0.000}, classes {2:0.000} / {3:0.000} / {4:0.000} / {5:0.000}, tail {6:0.000}; " +
                "bodies a class {7} / {8} / {9} / {10}; cell {11:0.000} m (mean link {12:0.000} m), largest link {13:0.000} m; " +
                "most cells one link covers, a class {14} / {15} / {16} / {17}; entries a class {18} / {19} / {20} / {21}",
                world.ElapsedSeconds, _probeGrid * f, _probeClass[0] * f, _probeClass[1] * f, _probeClass[2] * f, _probeClass[3] * f,
                _probeTail * f, used[0], used[1], used[2], used[3], cell, mean, largest,
                most[0], most[1], most[2], most[3], entries[0], entries[1], entries[2], entries[3]));

            ProbeLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "gpu-probe grid parts, ms a step: zero {0:0.000}, mean {1:0.000}, ranges {2:0.000}, scan {3:0.000}, scatter {4:0.000}; rows {5}, buckets {6}; " +
                "the water and fluid passes before them {7:0.000}; the links' contacts before each class (in its time when concurrent) {8:0.000}",
                _probeGridPart[0] * f, _probeGridPart[1] * f, _probeGridPart[2] * f, _probeGridPart[3] * f, _probeGridPart[4] * f,
                _probeRows, _probeBuckets, _probeWater * f, _probeContact * f));
            if (!_concurrent)
            {
                ProbeLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "gpu-probe passes, ms a step, each launch joined: water a class {0:0.000} / {1:0.000} / {2:0.000} / {3:0.000}; " +
                    "fluid a class {4:0.000} / {5:0.000} / {6:0.000} / {7:0.000}",
                    _probeWaterClass[0] * f, _probeWaterClass[1] * f, _probeWaterClass[2] * f, _probeWaterClass[3] * f,
                    _probeFluidClass[0] * f, _probeFluidClass[1] * f, _probeFluidClass[2] * f, _probeFluidClass[3] * f));
            }
            for (int k = 0; k < 4; k++) _probeWaterClass[k] = _probeFluidClass[k] = 0;
#if GPU_PHASE_PROBE
            PrintPhases();
#endif

            double fb = 1000.0 / Stopwatch.Frequency / 20;
            ProbeLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "gpu-probe host, ms a block: slots {0:0.00}, packs {1:0.00}, block packs {2:0.00}, snow and instants {3:0.00}, " +
                "up {4:0.00}; down {5:0.00}, mirror {6:0.00}; the scent's copy {7:0.00} (in snow and instants) and upload " +
                "{8:0.00} (in up), {9:0.0} MB",
                _probeHost[0] * fb, _probeHost[1] * fb, _probeHost[2] * fb, _probeHost[3] * fb, _probeHost[4] * fb,
                _probeHost[5] * fb, _probeHost[6] * fb, _probeScent[0] * fb, _probeScent[1] * fb,
                _probeScentBytes / (double)(1 << 20) / 20));
            for (int k = 0; k < _probeHost.Length; k++) _probeHost[k] = 0;
            _probeScent[0] = _probeScent[1] = 0;
            _probeScentBytes = 0;
            double mb = 1.0 / (1 << 20) / 20;
            ProbeLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "gpu-probe bytes, MB a block: up body columns {0:0.0}, link columns {1:0.0}, spheres {2:0.0}, classes {3:0.0}, " +
                "snow and instants {4:0.0}; down body and link columns {5:0.0}, classes {6:0.0}; slots {7}",
                _probeBytes[0] * mb, _probeBytes[1] * mb, _probeBytes[2] * mb, _probeBytes[3] * mb, _probeBytes[4] * mb,
                _probeBytes[5] * mb, _probeBytes[6] * mb, _gcap));
            for (int k = 0; k < _probeBytes.Length; k++) _probeBytes[k] = 0;
            ProbeLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "gpu-probe copies, a block: up {0:0.0} copies ({18:0.0} waiting), {1:0.0} MB moved, {2:0.0} MB in used slots; " +
                "down {3:0.0} copies ({19:0.0} waiting), {4:0.0} MB moved, {5:0.0} MB in used slots; the rest of the block {6:0.0} copies " +
                "({20:0.0} waiting), {7:0.0} MB; capacity against used a class {8}/{9} / {10}/{11} / {12}/{13} / {14}/{15}; slots {16}/{17}",
                _probeCopies[0] / 20.0, _probeCopyBytes[0] * mb, _probeUsedBytes[0] * mb,
                _probeCopies[1] / 20.0, _probeCopyBytes[1] * mb, _probeUsedBytes[1] * mb,
                _probeCopies[2] / 20.0, _probeCopyBytes[2] * mb,
                _probeCap[0], _probeClassUsed[0], _probeCap[1], _probeClassUsed[1],
                _probeCap[2], _probeClassUsed[2], _probeCap[3], _probeClassUsed[3], _gcap, _slots.Used,
                _probeWaits[0] / 20.0, _probeWaits[1] / 20.0, _probeWaits[2] / 20.0));
            for (int k = 0; k < 3; k++) _probeCopies[k] = _probeCopyBytes[k] = _probeWaits[k] = 0;
            _probeUsedBytes[0] = _probeUsedBytes[1] = 0;

            _probeGrid = _probeTail = _probeSteps = _probeWater = _probeContact = 0;
            for (int k = 0; k < 4; k++) _probeClass[k] = 0;
            for (int k = 0; k < 5; k++) _probeGridPart[k] = 0;
        }

        private void Run(DynamicsWorld world, int steps)

        {
            long t0 = Stopwatch.GetTimestamp();
            long th = t0;
            long tc = CopyTally.Copies, tb = CopyTally.Bytes;
            _waitMark = CopyTally.Waits;
            IReadOnlyList<Creature> list = world.Creatures;
            _list = list;
            int threads = Math.Max(1, world.Threads);
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = threads };

            // ---- 1. slots, refusals and room
            _slots.Sync(list, _o.Resync);

            // A body no class holds stops the run before anything steps. Losing it, or stepping it
            // with less brain than it grew, would be a cap on what can evolve: a world rule made by
            // the engine (card-audit-r57.md, the central brain). The farm writes this as the
            // run's ending and exits non-zero; the world carries on, on the cpu engine, from its
            // last checkpoint.
            if (_slots.Refused.Count > 0)
            {
                Creature refused = _slots.Refused[0];
                throw new NotSupportedException(FormattableString.Invariant(
                    $"The gpu engine cannot hold body {refused.Id} at step {world.Steps}: ") +
                    _slots.WhyNoClass(refused) + ". It will not change a body to fit, so this world steps on " +
                    "the cpu engine from its last checkpoint.");
            }

            EnsureGlobal(_slots.Used);
            if (_rankToSlot.Cap < Math.Max(1, list.Count)) _rankToSlot.Resize(Math.Max(list.Count, 2 * _rankToSlot.Cap));
            for (int k = 0; k < _classes.Length; k++) _classes[k].Ensure(_slots.Classes[k].Used);

            if (_census.Cap < WholeStep.CensusFields * steps) _census.Resize(WholeStep.CensusFields * steps);
            if (_inst.Cap < GpuWorld.InstStride * steps) _inst.Resize(GpuWorld.InstStride * steps);

            if (ProbeOn) HostMark(0, ref th);

            // ---- 2. freed slots, then new and resized bodies
            foreach ((int g, int k, int local) in _slots.Freed)
            {
                _classes[k].GSlot.Host[local] = -1;
                _gLinks.Host[g] = 0;
                _gAlive.Host[g] = 0;
                _gRemains.Host[g] = 0;
                _nOver.Host[g] = 0;
                _nHeld.Host[g] = 0;
                _active[_committed].Host[g] = 0;
                _sph[_committed][3].Host[g] = 0;
            }

            for (int r = 0; r < list.Count; r++)
            {
                Creature c = list[r];
                if (c.Senses.Nutrients != null) _w.AttachField(c.Senses.Nutrients);
                if (c.Senses.Scent != null) _w.AttachScent(c.Senses.Scent);
                if (c.Senses.LeafScent != null) _w.AttachLeafScent(c.Senses.LeafScent);
            }

            PlaceBrains();

            foreach (int g in _slots.Packs)
            {
                ClassSet set = _classes[_slots.ClassOf[g]];
                set.NotePack(_slots.LocalOf[g]);
                set.WidenPanels(_slots.Body[g].PanelStart[_slots.Body[g].Links]);
            }

            Parallel.For(0, _slots.Packs.Count, parallel, p => Pack(_slots.Packs[p]));
            if (_w.TrueShape && _slots.Packs.Count > 0) _coreUp = true;

            if (ProbeOn) HostMark(1, ref th);

            // ---- 3. what every body carries into every block, and the roster
            Parallel.For(0, _slots.Used, parallel, g =>
            {
                if (_slots.Body[g] != null) PackBlock(g);
            });

            for (int r = 0; r < list.Count; r++) _rankToSlot.Host[r] = _slots.RankToSlot[r];

            if (ProbeOn) HostMark(2, ref th);

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
                _base.FieldL = _w.FieldL; _base.FieldW = _w.FieldW;
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

            // The scent's grid as the host last filled it, every block: the host refills it at a
            // metabolic step, which a block never spans, so the card reads within a block what the
            // CPU reads, stale or not. Under EVOSIM_GPU_SCENT the card fills it (GpuScent, section 5)
            // and the host's copy is not read.
            ScentField scent = _w.Scent;
            int scentLength = scent == null ? 1 : scent.CellsX * scent.CellsY * scent.CellsZ;

            // Under the card's fill the column is the fill's output, sized to its grid when the fill
            // was set up. Resized under it, the fill would write past the end of the card's buffer,
            // which nothing on the card checks.
            if (_gpuScent != null && _scent.Cap != scentLength)
            {
                throw new InvalidOperationException(
                    "The scent's column holds " + _scent.Cap.ToString(System.Globalization.CultureInfo.InvariantCulture) + " cells and the " +
                    "block wants " + scentLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", under the card's fill: " +
                    "the fill writes the whole grid into it.");
            }

            if (_scent.Cap != scentLength) _scent.Resize(scentLength);
            if (scent != null && _gpuScent == null)
            {
                long sc = ProbeOn ? Stopwatch.GetTimestamp() : 0;
                scent.CopyCellsTo(_scent.Host);
                if (ProbeOn) _probeScent[0] += Stopwatch.GetTimestamp() - sc;
            }
            _base.ScentNx = scent == null ? 1 : scent.CellsX;
            _base.SmellWraps = (scent != null && scent.Wraps) || (_w.LeafScent != null && _w.LeafScent.Wraps) ? 1 : 0;
            _base.ScentNy = scent == null ? 1 : scent.CellsY;
            _base.ScentNz = scent == null ? 1 : scent.CellsZ;

            // D167's leaf smell, as the scent's grid: the host refills it at a metabolic step only.
            ScentField leaf = _w.LeafScent;
            int leafLength = leaf == null ? 1 : leaf.CellsX * leaf.CellsY * leaf.CellsZ;
            if (_leaf.Cap != leafLength) _leaf.Resize(leafLength);
            if (leaf != null) leaf.CopyCellsTo(_leaf.Host);
            _base.LeafNx = leaf == null ? 1 : leaf.CellsX;
            _base.LeafNy = leaf == null ? 1 : leaf.CellsY;
            _base.LeafNz = leaf == null ? 1 : leaf.CellsZ;
            _base.LeafCell = leaf == null ? 1f : leaf.CellSizeMetres;

            // D239: the instants are the world's clock, which steps by dt over the pace factor (dt itself
            // at a factor of 1), exactly as DynamicsWorld.BackendAdvanceClock advances it.
            double dt = world.Config.ClockStepSeconds;
            double seconds = world.ElapsedSeconds;
            for (int k = 0; k < steps; k++)
            {
                _w.Instant(seconds, _instD, 0);
                int at = k * GpuWorld.InstStride;
                for (int v = 0; v < GpuWorld.InstStride; v++) _inst.Host[at + v] = (Real)_instD[v];
                seconds += dt;
            }

            Array.Clear(_census.Host, 0, _census.Host.Length);

            if (ProbeOn) HostMark(3, ref th);

            if (ProbeOn)
            {
                int probeRows = ListRows();
                foreach (IColumn c in _gBody) _probeBytes[0] += _lists.Contains(c) ? 0 : c.Bytes;
                _probeBytes[0] += 6L * probeRows * _gcap * sizeof(int);
                foreach (IColumn c in _gLink) _probeBytes[1] += c.Bytes;
                foreach (Col<Real> c in _sph[_committed]) _probeBytes[2] += c.Bytes;
                for (int k = 0; k < _classes.Length; k++) _probeBytes[3] += _classes[k].UploadBytes;
                _probeBytes[4] += _stock.Bytes + _inst.Bytes;
                foreach (IColumn c in _gDown) _probeBytes[5] += _lists.Contains(c) ? 0 : c.Bytes;
                _probeBytes[5] += 6L * probeRows * _gcap * sizeof(int);
                for (int k = 0; k < _classes.Length; k++)
                    if (_slots.Classes[k].Used > 0) _probeBytes[6] += _classes[k].DownloadBytes;

                // The same copies stopped at the used slots, beside the bytes at capacity above.
                int probeUsed = _slots.Used;
                foreach (IColumn c in _gBody) _probeUsedBytes[0] += _lists.Contains(c) ? 0 : c.BytesFor(UsedUnits(c, probeUsed));
                _probeUsedBytes[0] += 6L * probeRows * probeUsed * sizeof(int);
                foreach (IColumn c in _gLink) _probeUsedBytes[0] += c.BytesFor(UsedUnits(c, probeUsed));
                foreach (Col<Real> c in _sph[_committed]) _probeUsedBytes[0] += c.BytesFor(UsedUnits(c, probeUsed));
                _probeUsedBytes[0] += _stock.Bytes + _inst.Bytes;
                foreach (IColumn c in _gDown) _probeUsedBytes[1] += _lists.Contains(c) ? 0 : c.BytesFor(UsedUnits(c, probeUsed));
                _probeUsedBytes[1] += 6L * probeRows * probeUsed * sizeof(int);
                for (int k = 0; k < _classes.Length; k++)
                {
                    int used = _slots.Classes[k].Used;
                    _probeUsedBytes[0] += _classes[k].UploadBytesFor(used);
                    if (used > 0) _probeUsedBytes[1] += _classes[k].DownloadBytesFor(used);
                    _probeCap[k] = _classes[k].Cap;
                    _probeClassUsed[k] = used;
                }
                CopyMark(2, ref tc, ref tb);
            }

            // ---- 5. up
            // Every copy here is queued without a wait on a card (IColumn.UpQueued) and joined once
            // at the section's end, which card-next-r57.md Part 8's table makes sound: the host
            // writes none of these arrays again before the download, and the card reads them only
            // after the join.
            int listRows = ListRows();
            int upUsed = _slots.Used;
            foreach (IColumn c in _gBody) if (!_lists.Contains(c)) c.UpUsedQueued(UsedUnits(c, upUsed));
            ListsUp(listRows);
            foreach (IColumn c in _gLink) c.UpUsedQueued(UsedUnits(c, upUsed));
            _rankToSlot.UpQueued();
            _census.UpQueued();
            _overflow.UpQueued();
            _guard.UpQueued();
            _strayCode.View.MemSetToZero(_stream);
            _strayStep.View.MemSetToZero(_stream);
            _strayCount.View.MemSetToZero(_stream);
            // The spin columns (26 to 28) travel only at D228's level (b); a one-cell stub is not copied at the other values.
            for (int k = 0; k < _sphCopied; k++) _sph[_committed][k].UpUsedQueued(UsedUnits(_sph[_committed][k], upUsed));
            _active[_committed].UpUsedQueued(UsedUnits(_active[_committed], upUsed));
            if (_coreUp)
            {
                // D228's cores change only in a pack (a birth, a growth resize), so they go up after one.
                foreach (Col<Real> c in _core) c.UpUsedQueued(UsedUnits(c, upUsed));
                _coreUp = false;
            }
            for (int k = 0; k < _classes.Length; k++) _classes[k].Upload(_slots.Classes[k].Used);
            _stock.UpQueued();
            if (_gpuScent != null)
            {
                // The card's own fill (C3) was queued on its own stream at the world's snapshot
                // (GpuScent). The block joins that stream here, before any kernel reads the grid.
                // Under the probe the engine's own streams are joined first (Sync leaves the fill's
                // stream out), so the time is the step's wait on the fill, in the scent's upload's place.
                long su = 0;
                if (ProbeOn) { Sync(); su = Stopwatch.GetTimestamp(); }
                bool filled = _gpuScent.Join();
                if (ProbeOn)
                {
                    _probeScent[1] += Stopwatch.GetTimestamp() - su;
                    if (filled) _probeScentBytes += _gpuScent.Bytes;
                }
                _gpuScent.Audit();
            }
            else if (ProbeOn)
            {
                // The scent's upload joined on both sides, so its time is its own (C0,
                // card-next-r57.md): what a fill on the card would no longer pay.
                Sync();
                long su = Stopwatch.GetTimestamp();
                _scent.Up();
                Sync();
                _probeScent[1] += Stopwatch.GetTimestamp() - su;
                _probeScentBytes += _scent.Bytes;
            }
            else
            {
                _scent.UpQueued();
            }
            if (_w.LeafScent != null) _leaf.UpQueued();
            if (_cDirty)
            {
                _cSource.UpQueued(); _cWeight.UpQueued(); _cPortPart.UpQueued(); _cPortChannel.UpQueued(); _cPortIndex.UpQueued();
                _cDirty = false;
            }
            _inst.UpQueued();

            // The join after the uploads (Part 8). The classes' streams, when concurrent, do not
            // follow this stream's order, and the water and fluid passes of the first step start
            // on them; without this wait they could read a column before its copy lands.
            _stream.Synchronize();

            if (ProbeOn) { Sync(); HostMark(4, ref th); CopyMark(0, ref tc, ref tb); }

            // ---- 6. the grid's room
            int gUsed = _slots.Used;
            int rows = _base.PerPart != 0 ? WholeStep.LinkStride * gUsed : gUsed;
            int buckets = 16;
            while (buckets < 2 * rows) buckets <<= 1;

            _lo.Ensure(3L * Math.Max(1, rows));
            _hi.Ensure(3L * Math.Max(1, rows));
            _isGiant.Ensure(Math.Max(1, rows));
            _counts.Ensure(buckets + 1);
            _start.Ensure(buckets + 1);
            _gcursor.Ensure(buckets + 1);
            int tileBuckets = _tileGroup * WholeStep.ScanPer;
            int tiles = (buckets + tileBuckets - 1) / tileBuckets;
            _tileSum.Ensure(tiles);
            _tileStart.Ensure(tiles);
            long linkRows = Math.Max(1, rows);
            // Three rows of the push; under D228's level (b) three more, the torque about the link's centre.
            _linkPush.Ensure((_w.AtPoint ? 6 : 3) * linkRows);
            // The fluid's rows are links whatever the contact's are: LinkStride a slot.
            long fluidRows = Math.Max(1, (long)WholeStep.LinkStride * gUsed);
            _linkFluid.Ensure(20 * fluidRows);
            _linkLimited.Ensure(fluidRows);
            _linkFlags.Ensure(linkRows);
            if (_rock)
            {
                _touchNew.Ensure(linkRows);
                _touchAt.Ensure(6 * linkRows);
            }
            _stageN.Ensure(linkRows);
            _stageId.Ensure(WholeStep.StageCap * linkRows);
            _stageSlot.Ensure(WholeStep.StageCap * linkRows);
            _stagePart.Ensure(WholeStep.StageCap * linkRows);
#if GPU_PHASE_PROBE
            _phase.Ensure((long)WholeStep.Phases * _gcap);
            _linkPhase.Ensure(WholeStep.LinkParts * fluidRows);
            _linkPhaseRows = WholeStep.LinkStride * gUsed;
#endif
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
            cfg.LinkRows = WholeStep.LinkStride * gUsed;
            cfg.Buckets = buckets;
            cfg.Mask = buckets - 1;
            cfg.EntryCap = _entryCap;
            cfg.GiantCells = GiantCells;
            cfg.Chunk = _chunk;
            cfg.Chunks = chunks;
            cfg.Tiles = tiles;
            cfg.CellOverride = (Real)world.ContactCellOverrideMetres;

            // D220: whether any body of this block is remains. A block without any (every block of a world with
            // the dial off) reads no flag on the card and takes every old path on a test it cannot fail.
            cfg.AnyRemains = 0;
            for (int rg = 0; rg < _slots.Used; rg++)
            {
                if (_slots.Body[rg] != null && _slots.Body[rg].Remains) { cfg.AnyRemains = 1; break; }
            }

            // The farm switches the instrument on after it builds the solver's config, so it is
            // read from the world being stepped and not from the one the kernels were built for.
            cfg.Instrument = world.Config.ContactInstrument ? 1 : 0;

            SGlob gl = Glob;
            SGrid grid = Grid;
            SWorld sw = new SWorld { Inst = _inst.View, WR = _wr.View, Stock = _stock.View, WI = _wi.View, Scent = _scent.View, Leaf = _leaf.View, Guard = _guard.View };

            long t1 = Stopwatch.GetTimestamp();

            // ---- 7. the steps
            long steps0 = world.Steps;
            bool probe = ProbeOn;
            long pt = probe ? Stopwatch.GetTimestamp() : 0;
            bool serial = _o.SerialMean;
            bool instrument = world.Config.ContactInstrument;
            bool perPart = _base.PerPart != 0;

            if (probe)
            {
                // The launches a step, counted as the loop below makes them (the memset is the
                // driver's, and counted with them); the probe's own joins are not launches.
                int launches = 1;
                for (int c = 0; c < _classes.Length; c++)
                {
                    int used = _slots.Classes[c].Used;
                    _probeUsed[c] = used;
                    _probeLinkThreads[c] = used * _slots.Classes[c].Links;
                    if (used > 0) launches += (cfg.HasCurrent != 0 ? 1 : 0) + 1 + (perPart ? 1 : 0) + 1;
                }
                if (rows > 0) launches += 2 * (serial ? 1 : 2) + 10;
                if (instrument && gUsed > 0) launches++;
                _probeLaunches = launches;
                _probeGUsed = gUsed;
                _probeChunks = chunks;
                _probeTiles = tiles;
            }

            for (int k = 0; k < steps; k++)
            {
                cfg.InstBase = k * GpuWorld.InstStride;
                cfg.StepIndex = k;
                cfg.TraceStep = steps0 + k + 1;
                cfg.TraceTime = (steps0 + k + 1) * dt;

                SSph committed = Sph(_committed), pending = Sph(1 - _committed);

                // The water and the fluid, a link a thread, ahead of each class's step on the
                // class's own stream, so that under concurrent streams they run beside the grid.
                for (int c = 0; c < _classes.Length; c++)
                {
                    int used = _slots.Classes[c].Used;
                    if (used == 0) continue;

                    ClassSet set = _classes[c];
                    SCfg cc = cfg;
                    cc.N = set.Cap;
                    cc.MaxN = set.M;
                    cc.ClassUsed = used;
                    cc.ClassLinks = _slots.Classes[c].Links;

                    int linkThreads = used * cc.ClassLinks;
                    if (_concurrent)
                    {
                        if (cfg.HasCurrent != 0) _waterOn(_streams[c], linkThreads, set.Topo, set.StateSet, gl, sw, cc);
                        _fluidOn(_streams[c], linkThreads, set.Topo, set.ConstSet, set.StateSet, set.PanelSet, gl, cc);
                    }
                    else
                    {
                        if (cfg.HasCurrent != 0) _water(linkThreads, set.Topo, set.StateSet, gl, sw, cc);
                        if (probe) PassPart(_probeWaterClass, c, ref pt);
                        _fluid(linkThreads, set.Topo, set.ConstSet, set.StateSet, set.PanelSet, gl, cc);
                        if (probe) PassPart(_probeFluidClass, c, ref pt);
                    }
                }
                if (probe) { Sync(); long pn = Stopwatch.GetTimestamp(); _probeWater += pn - pt; pt = pn; }

                _counts.View.MemSetToZero(_stream);
                if (probe) GridPart(0, ref pt);
                if (rows > 0)
                {
                    if (serial)
                    {
                        _meanSerial(1, committed, gl, grid, cfg);
                    }
                    else
                    {
                        _meanGroups(new KernelConfig(chunks, _meanGroup), committed, gl, grid, cfg);
                        _meanCombine(1, grid, cfg);
                    }
                    if (probe) GridPart(1, ref pt);

                    _ranges(rows, committed, gl, grid, cfg);

                    // The reach guard (DynamicsWorld.LoseStrays): what strayed is recorded and
                    // emptied, and the grid built once more without it. Every launch returns at
                    // once when nothing strayed, so a step without a stray is the step it was. A
                    // stray the second build finds is lost too, and refused at the block's end.
                    SCfg redo = cfg;
                    redo.Redo = 1;
                    _loseStrays(gUsed, committed, gl, grid, cfg);
                    _rearm(1, grid, cfg);
                    _clearCounts(buckets + 1, grid, cfg);
                    if (serial)
                    {
                        _meanSerial(1, committed, gl, grid, redo);
                    }
                    else
                    {
                        _meanGroups(new KernelConfig(chunks, _meanGroup), committed, gl, grid, redo);
                        _meanCombine(1, grid, redo);
                    }
                    _ranges(rows, committed, gl, grid, redo);
                    _loseStrays(gUsed, committed, gl, grid, redo);
                    if (probe) GridPart(2, ref pt);
                    _scanReduce(new KernelConfig(tiles, _tileGroup), grid, cfg);
                    _scanTiles(new KernelConfig(1, _scanGroup), grid, cfg);
                    _scanDown(new KernelConfig(tiles, _tileGroup), grid, cfg);
                    if (probe) GridPart(3, ref pt);
                    _scatter(rows, gl, grid, cfg);
                    _giantLists(rows, grid, cfg);
                    if (probe) GridPart(4, ref pt);
                }
                if (probe) { Sync(); long pn = Stopwatch.GetTimestamp(); _probeGrid += pn - pt; pt = pn; _probeRows = rows; _probeBuckets = cfg.Buckets; }

                if (_concurrent)
                {
                    // Every class reads the grid the default stream just built, so the grid is
                    // joined first; the census reads every class's pending spheres, so each class
                    // stream is joined after. In between the classes run at once.
                    _stream.Synchronize();
                    for (int c = 0; c < _classes.Length; c++)
                    {
                        int used = _slots.Classes[c].Used;
                        if (used == 0) continue;

                        ClassSet set = _classes[c];
                        SCfg cc = cfg;
                        cc.N = set.Cap;
                        cc.MaxN = set.M;
                        cc.ClassUsed = used;
                        cc.ClassLinks = _slots.Classes[c].Links;

                        if (perPart) _contactOn(_streams[c], used * cc.ClassLinks, set.Topo, set.ConstSet, gl, committed, grid, sw, cc);
                        _stepOn[c](_streams[c], used, set.Topo, set.ConstSet, set.PanelSet, set.StateSet, set.DriveSet,
                                   Neur(set), set.BrainSet, set.TraceSet, gl, committed, pending, grid, sw, cc);
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
                        cc.ClassUsed = used;
                        cc.ClassLinks = _slots.Classes[c].Links;

                        if (perPart)
                        {
                            _contact(used * cc.ClassLinks, set.Topo, set.ConstSet, gl, committed, grid, sw, cc);
                            if (probe) { Sync(); long pn = Stopwatch.GetTimestamp(); _probeContact += pn - pt; pt = pn; }
                        }
                        _step[c](used, set.Topo, set.ConstSet, set.PanelSet, set.StateSet, set.DriveSet, Neur(set),
                                 set.BrainSet, set.TraceSet, gl, committed, pending, grid, sw, cc);
                        if (probe) { Sync(); long pn = Stopwatch.GetTimestamp(); _probeClass[c] += pn - pt; pt = pn; }
                    }
                }

                if (instrument && gUsed > 0) _censusKernel(gUsed, gl, cfg);
                if (probe) { Sync(); long pn = Stopwatch.GetTimestamp(); _probeTail += pn - pt; pt = pn; }

                _committed = 1 - _committed;
            }

#if GPU_PHASE_PROBE
            if (probe) PhaseRead();
#endif
            if (probe)
            {
                _probeSteps += steps;
                if (++_probeBlocks % 20 == 0)
                {
                    PrintProbe(world, list);
                    ProbeLinesDone();
                }
            }

            Sync();
            long t2 = Stopwatch.GetTimestamp();
            if (ProbeOn) CopyMark(2, ref tc, ref tb);

            // ---- 8. down
            // Queued like the uploads, with two joins (card-next-r57.md Part 8). The overlap lists'
            // rows are counted from two of the body columns (ListRows reads NOver and NHeld), so
            // those land before the count is taken; the lists land before the mirror reads them.
            for (int k = 0; k < _classes.Length; k++)
            {
                if (_slots.Classes[k].Used > 0) _classes[k].Download(_slots.Classes[k].Used);
            }

            int downUsed = _slots.Used;
            foreach (IColumn c in _gDown) if (!_lists.Contains(c)) c.DownUsedQueued(UsedUnits(c, downUsed));
            for (int k = 0; k < _sphCopied; k++) _sph[_committed][k].DownUsedQueued(UsedUnits(_sph[_committed][k], downUsed));
            _active[_committed].DownUsedQueued(UsedUnits(_active[_committed], downUsed));
            _census.DownQueued();
            _overflow.DownQueued();
            _guard.DownQueued();
            _stream.Synchronize();

            ListsDown(ListRows());
            _stream.Synchronize();

            if (rows > 0)
            {
                _start.View.SubView(buckets, 1).CopyToCPU(_stream, _one);
                if (_one[0] > _entryCap / 2) _entryCap = 2 * _one[0];
            }

            long t3 = Stopwatch.GetTimestamp();
            if (ProbeOn) CopyMark(1, ref tc, ref tb);

            // ---- 9. the mirror, the census, the clock
            long ceiling = 0;
            Parallel.For(0, _slots.Used, parallel,
                () => 0L,
                (g, _, local) => _slots.Body[g] != null ? local + Unpack(g, instrument) : local,
                local => Interlocked.Add(ref ceiling, local));

            NeuronsAtCeiling = ceiling;
            if (ceiling > NeuronsAtCeilingMost) NeuronsAtCeilingMost = ceiling;

            HandStraysOver(world, list);

            for (int k = 0; k < 4; k++) Overflow[k] = _overflow.Host[k];
            FoldWatch(world, list);
            BedAnglesPastGuard = _guard.Host[0];

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
            if (ProbeOn) { _probeHost[5] += t3 - t2; _probeHost[6] += t4 - t3; CopyMark(2, ref tc, ref tb); }

            world.BackendPhaseTicks(0, t1 - t0);
            world.BackendPhaseTicks(2, t2 - t1);
            world.BackendPhaseTicks(3, t3 - t2);
            world.BackendPhaseTicks(4, t4 - t3);
        }

        /// <summary>
        /// The block's strays, handed to <see cref="DynamicsWorld.StrayHandler"/> in the list's
        /// order, one a body, with the sphere LoseStrays recorded: the CPU's call, made at the block's
        /// end rather than inside the step. The body was left unstepped from its stray step, as
        /// the CPU leaves it, so the mirror holds it as it was when it strayed.
        /// </summary>
        private void HandStraysOver(DynamicsWorld world, IReadOnlyList<Creature> list)
        {
            _strayCount.Down();
            int left = _strayCount.Host[WholeStep.StrayLeft];
            if (left > 0)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"The gpu engine's contact grid had {left} sphere(s) out of its reach ") +
                    "after it was built again without the block's first strays. The CPU builds it again until " +
                    "none strays; the card builds it twice, so this world is not the card's to step.");
            }

            for (int r = 0; r < list.Count; r++)
            {
                int g = _slots.RankToSlot[r];
                if (g < 0 || _strayStep.Host[g] == 0) continue;

                int code = _strayCode.Host[g];
                int link = code > WholeStep.LinkStride ? -1 : WholeStep.LinkStride - code;
                var stray = new ContactStray(r, link,
                    (double)_strayAt.Host[4 * g], (double)_strayAt.Host[4 * g + 1],
                    (double)_strayAt.Host[4 * g + 2], (double)_strayAt.Host[4 * g + 3]);

                world.StrayHandler?.Invoke(list[r], stray);
            }
        }

        private void EnsureGlobal(int used)
        {
            if (used <= _gcap) return;

            int cap = Math.Max(used, 2 * _gcap);
            foreach (IColumn c in _gBody) c.Resize(cap);
            foreach (IColumn c in _gLink) c.Resize(WholeStep.LinkStride * cap);
            foreach (IColumn c in _gStray) c.Resize(cap);

            for (int set = 0; set < 2; set++)
            {
                for (int k = 0; k < 7; k++) _sph[set][k].Resize(cap);
                for (int k = 7; k < 17; k++) _sph[set][k].Resize(WholeStep.LinkStride * cap);
                if (_w.TrueShape) for (int k = 17; k < 26; k++) _sph[set][k].Resize(WholeStep.LinkStride * cap);
                if (_w.AtPoint) for (int k = 26; k < 29; k++) _sph[set][k].Resize(WholeStep.LinkStride * cap);
                _active[set].Resize(cap);
            }

            if (_w.TrueShape)
            {
                foreach (Col<Real> c in _core) c.Resize(WholeStep.LinkStride * cap);
                _coreUp = true;
            }

            _gcap = cap;
        }

        private SGlob Glob => new SGlob
        {
            Links = _gLinks.View, Id = _gId.View, Rank = _gRank.View, Alive = _gAlive.View, Jointed = _gJointed.View,
            Remains = _gRemains.View,
            RankToSlot = _rankToSlot.View, TotalMass = _gTotalMass.View, LinkMass = _linkMass.View,
            CoreHx = _core[0].View, CoreHy = _core[1].View, CoreHz = _core[2].View, CoreM = _core[3].View,
            NOver = _nOver.View, OvSlot = _ovSlot.View, OvId = _ovId.View, OvPart = _ovPart.View,
            OvHeld = _ovHeld.View, NHeld = _nHeld.View, HeldId = _heldId.View, BedGlass = _bedGlass.View,
            Census = _census.View, Overflow = _overflow.View,
            LinkPush = _linkPush.View, LinkFlags = _linkFlags.View, StageN = _stageN.View,
            LinkFluid = _linkFluid.View, LinkLimited = _linkLimited.View,
            StageId = _stageId.View, StageSlot = _stageSlot.View, StagePart = _stagePart.View,
            StrayCode = _strayCode.View, StrayStep = _strayStep.View, StrayAt = _strayAt.View,
            RockTouched = _rockTouched.View, TouchNew = _touchNew.View, TouchAt = _touchAt.View,
            RockPx = _rockP[0].View, RockPy = _rockP[1].View, RockPz = _rockP[2].View,
            RockNx = _rockP[3].View, RockNy = _rockP[4].View, RockNz = _rockP[5].View,
            Anchors = _anchors.View, GripHeld = _gripHeld.View, GripBreak = _gripBreak.View,
            GripPrice = _gripPrice.View, GripStrength = _gripStrength.View, ReserveLow = _reserveLow.View,
            GripOwed = _gripOwed.View, GripHeldSeconds = _gripHeldSeconds.View, GripAnchor = _gripAnchor.View,
            GripAttitude = _gripAttitude.View, GripPoint = _gripPoint.View,
            GripsFormed = _gripsFormed.View, GripsBroken = _gripsBroken.View,
#if GPU_PHASE_PROBE
            Phase = _phase.View,
            LinkPhase = _linkPhase.View,
#endif
        };

        private SGrid Grid => new SGrid
        {
            Lo = _lo.View, Hi = _hi.View, Counts = _counts.View, Start = _start.View, Cursor = _gcursor.View,
            Items = _items.View, PartialN = _partialN.View, Cell = _cell.View, Partial = _partial.View,
            TileSum = _tileSum.View, TileStart = _tileStart.View, Stray = _strayCount.View,
            Giant = _giant.View, IsGiant = _isGiant.View, GiantN = _giantN.View, GiantList = _giantList.View,
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
                LEx = s[14].View, LEy = s[15].View, LEz = s[16].View,
                LRot0 = s[17].View, LRot1 = s[18].View, LRot2 = s[19].View,
                LRot3 = s[20].View, LRot4 = s[21].View, LRot5 = s[22].View,
                LRot6 = s[23].View, LRot7 = s[24].View, LRot8 = s[25].View,
                LSp0 = s[26].View, LSp1 = s[27].View, LSp2 = s[28].View,
                Active = _active[set].View,
            };
        }

        // ============================================================== the mirror

        /// <summary>A class's neuron columns with the one brain's pools, for a launch.</summary>
        private SNeur Neur(ClassSet set)
        {
            SNeur nr = set.NeurSet;
            nr.CSource = _cSource.View;
            nr.CWeight = _cWeight.View;
            nr.CPortPart = _cPortPart.View;
            nr.CPortChannel = _cPortChannel.View;
            nr.CPortIndex = _cPortIndex.View;
            return nr;
        }

        private static int CentralLinks(Creature c) => c.Brain.IsCentral ? BrainAccess.CentralInSource(c.Brain).Length : 0;

        private static int CentralPorts(Creature c) => c.Brain.IsCentral ? BrainAccess.CentralPortPart(c.Brain).Length : 0;

        /// <summary>
        /// Gives every body about to be packed that carries the one brain a run of links and a run of
        /// ports in the pools, before the packs run in parallel and fill them. A slot whose runs
        /// still hold its new body keeps them; otherwise the body takes new runs at the pools' top.
        /// When the runs above the live ones are more than the live ones and PoolSlack (4,096) links,
        /// the pools are laid again from nothing and every body with the one brain is packed again.
        /// </summary>
        private void PlaceBrains()
        {
            int used = _slots.Used;
            if (_cLinkAt.Length < used)
            {
                int size = Math.Max(used, 2 * _cLinkAt.Length);
                Array.Resize(ref _cLinkAt, size); Array.Resize(ref _cLinkRoom, size);
                Array.Resize(ref _cPortAt, size); Array.Resize(ref _cPortRoom, size);
            }

            long liveLinks = 0, livePorts = 0;
            for (int g = 0; g < used; g++)
            {
                Creature c = _slots.Body[g];
                if (c == null) continue;
                liveLinks += CentralLinks(c);
                livePorts += CentralPorts(c);
            }

            if (_cLinksTop > 2 * liveLinks + PoolSlack || _cPortsTop > 2 * livePorts + PoolSlack)
            {
                PoolRelays++;
                var packs = new HashSet<int>(_slots.Packs);
                _cLinksTop = 0;
                _cPortsTop = 0;
                for (int g = 0; g < used; g++)
                {
                    _cLinkRoom[g] = 0;
                    _cPortRoom[g] = 0;
                    Creature c = _slots.Body[g];
                    if (c != null && c.Brain.IsCentral && packs.Add(g)) _slots.Packs.Add(g);
                }
            }

            foreach (int g in _slots.Packs)
            {
                Creature c = _slots.Body[g];
                if (!c.Brain.IsCentral) continue;

                int links = CentralLinks(c), ports = CentralPorts(c);
                if (links > _cLinkRoom[g]) { _cLinkAt[g] = _cLinksTop; _cLinkRoom[g] = links; _cLinksTop += links; }
                if (ports > _cPortRoom[g]) { _cPortAt[g] = _cPortsTop; _cPortRoom[g] = ports; _cPortsTop += ports; }
                _cDirty = true;
            }

            Grow(_cSource, _cLinksTop); Grow(_cWeight, _cLinksTop);
            Grow(_cPortPart, _cPortsTop); Grow(_cPortChannel, _cPortsTop); Grow(_cPortIndex, _cPortsTop);
        }

        private void Grow<T>(Col<T> pool, int length) where T : unmanaged
        {
            if (pool.Cap >= Math.Max(1, length)) return;
            pool.Resize(Math.Max(length, 2 * pool.Cap));
            _cDirty = true;
        }

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
                    s.SpinMoment.Host[a3] = (Real)c.SpinMoment[3 * l + k];
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

            // D229's fin law: each link's shape and the three numbers its added mass reads. A box's
            // are its half-extents; a capsule's, the radius and half-span CapsuleShape derives from
            // them, in the floats the CPU derives them in. A shape the law has no formula for is refused.
            if (_w.ShapedMass)
            {
                for (int l = 0; l < links; l++)
                {
                    PhenotypePart part = c.Phenotype.Parts[l];
                    Float3 h = part.HalfExtents;
                    int at = l * N + i;
                    int a3 = (3 * l) * N + i;
                    if (part.Family != null)
                    {
                        // D255: a family part (D233) takes the strip integral of its true slices, which the host body
                        // carries by degree of freedom (below). No half-extents are written, and the kernel reads none.
                        s.ShapeCode.Host[at] = 3;
                    }
                    else if (part.ShapeId == ShapeIds.Sphere)
                    {
                        s.ShapeCode.Host[at] = 0;
                    }
                    else if (part.ShapeId == ShapeIds.Box)
                    {
                        s.ShapeCode.Host[at] = 1;
                        s.HalfExt.Host[a3] = (Real)Math.Abs((double)h.X);
                        s.HalfExt.Host[a3 + N] = (Real)Math.Abs((double)h.Y);
                        s.HalfExt.Host[a3 + 2 * N] = (Real)Math.Abs((double)h.Z);
                    }
                    else if (part.ShapeId == ShapeIds.Capsule)
                    {
                        s.ShapeCode.Host[at] = 2;
                        s.HalfExt.Host[a3] = (Real)(double)CapsuleShape.Radius(h);
                        s.HalfExt.Host[a3 + N] = (Real)(double)CapsuleShape.HalfSpan(h);
                        s.HalfExt.Host[a3 + 2 * N] = 0;
                    }
                    else
                    {
                        throw new ArgumentException(
                            "The fin law has no added mass for the shape '" + part.ShapeId +
                            "'; it knows box, capsule and sphere.");
                    }
                }

                // D255: a family link's added mass and first moment, the host body's own per-degree tables
                // (Creature.FamilyAddedMass, FamilyFirstMoment, indexed like DofStart) as WriteFamilyStroke left them
                // at its build and at its last resize. A resize re-packs the body whole, so the card never holds the
                // numbers of an old size. Every degree is written, 0 for any other link or when the tables are null
                // (a body with no family joint), so a slot's earlier body leaves nothing behind.
                for (int d = 0; d < dof; d++)
                {
                    int at = d * N + i;
                    s.FamMass.Host[at] = 0;
                    s.FamMoment.Host[at] = 0;
                }

                if (c.FamilyAddedMass != null && c.FamilyFirstMoment != null)
                {
                    for (int l = 0; l < links; l++)
                    {
                        if (c.Phenotype.Parts[l].Family == null) continue;
                        for (int k = 0; k < c.DofCount[l]; k++)
                        {
                            int d = c.DofStart[l] + k;
                            int at = d * N + i;
                            s.FamMass.Host[at] = (Real)c.FamilyAddedMass[d];
                            s.FamMoment.Host[at] = (Real)c.FamilyFirstMoment[d];
                        }
                    }
                }
            }

            // D231's stroke that pushes its assembly: the host body's own tables, as Creature.WriteStrokeAssembly
            // left them at its build and at its last resize (a resize re-packs the body whole, so the card never
            // holds the old sizes' constants). A link's parts in its assembly, then a degree of freedom's
            // along, added mass and first moment, indexed like DofStart.
            if (_w.StrokeAssembly)
            {
                if (c.StrokeAssemblyParts == null)
                {
                    throw new InvalidOperationException(
                        "The gpu engine runs the stroke that pushes its assembly and this body was built " +
                        "without it, so it has no assembly table. A body is built under the config it is stepped with.");
                }

                for (int l = 0; l < links; l++) s.StrokeParts.Host[l * N + i] = c.StrokeAssemblyParts[l];

                for (int d = 0; d < dof; d++)
                {
                    int at = d * N + i;
                    s.StrokeAlong.Host[at] = (Real)c.StrokeAlong[d];
                    s.StrokeMass.Host[at] = (Real)c.StrokeAddedMass[d];
                    s.StrokeMoment.Host[at] = (Real)c.StrokeFirstMoment[d];
                }
            }

            // D234's mirrored twin: each link's mask of the degrees of freedom read negated, a mask for every
            // link so that a slot's earlier body leaves nothing behind. A world that did not ask for the rule
            // sizes no column for it, so a body that reflects its drive there would be stepped as a seesaw
            // by the card and as a mirror image by the solver; it is refused naming the setting instead.
            // The column holds the degrees of freedom's bits (0 to 2), and beside them the frame-vector reflection of
            // the senses (Creature.FrameNormal): bit 3 set when the part reflects, and its axis in bits 4 and 5.
            if (_w.MirroredDrive)
            {
                for (int l = 0; l < links; l++)
                {
                    int frame = c.FrameNormal[l];
                    s.MirrorMask.Host[l * N + i] = c.MirrorMask[l] | (frame >= 0 ? 8 | (frame << 4) : 0);
                }
            }
            else
            {
                for (int l = 0; l < links; l++)
                {
                    if (c.MirrorMask[l] != 0 || c.FrameNormal[l] >= 0)
                    {
                        throw new ArgumentException(
                            "Body " + c.Id + " reflects a mirrored part's joint drive, and the gpu engine was built for " +
                            "a world that does not (EVOSIM_REFLECT_MIRRORED_ANCHORS).");
                    }
                }
            }

            // D235's nose: 1 on each nose link, a flag for every link so that a slot's earlier body leaves nothing
            // behind. A world that does not list the nose sizes no column for it, and no body there has a nose link.
            if (_w.Nose)
            {
                CreatureSenses senses = c.Senses;
                bool nosed = false;
                for (int l = 0; l < links; l++)
                {
                    bool nose = senses.IsNose(l);
                    s.NoseFlag.Host[l * N + i] = nose ? 1 : 0;
                    nosed |= nose;
                }

                // The card reads only what Sniff held (CreatureSenses.HeldSniff, as the farm sets it). A bench's body
                // reads its noses' tips on every step instead, which the card does not, and would part from the solver.
                if (nosed && !senses.HeldSniff)
                {
                    throw new InvalidOperationException(
                        "Body " + c.Id + " has a nose whose senses read its tip on every step (HeldSniff is off); " +
                        "the gpu engine reads only the held sniff. Set HeldSniff and call Sniff, as the farm does.");
                }
            }

            if (c.Drive.BendAndPower)
            {
                for (int l = 0; l < links; l++)
                {
                    DriveGenes genes = c.JointGenes[l];
                    s.Threshold.Host[l * N + i] = genes.Threshold;
                    s.Reaction.Host[l * N + i] = genes.Reaction;
                    s.HoldGene.Host[l * N + i] = genes.Hold;
                    s.Release.Host[l * N + i] = genes.Release;
                    s.LinkPower.Host[l * N + i] = (Real)c.Power[l];
                }
            }

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

            // ---- the one brain (Brain.ForCentral): its units in the neurons' rows, its links and
            // ports in the runs PlaceBrains gave this slot
            if (brain.IsCentral)
            {
                float[] bias = BrainAccess.CentralBias(brain);
                int[] inStart = BrainAccess.CentralInStart(brain);
                int[] inSource = BrainAccess.CentralInSource(brain);
                float[] inWeight = BrainAccess.CentralInWeight(brain);
                int[] portPart = BrainAccess.CentralPortPart(brain);
                SensorChannel[] portChannel = BrainAccess.CentralPortChannel(brain);
                int[] portIndex = BrainAccess.CentralPortIndex(brain);

                s.CUnits.Host[i] = bias.Length;
                s.CDof.Host[i] = brain.TotalDof;
                s.CLinkAt.Host[i] = _cLinkAt[g];
                s.CPortAt.Host[i] = _cPortAt[g];

                for (int u = 0; u < bias.Length; u++)
                {
                    s.Bias.Host[u * N + i] = bias[u];
                    s.NIn.Host[u * N + i] = inStart[u + 1] - inStart[u];
                }

                Array.Copy(inSource, 0, _cSource.Host, _cLinkAt[g], inSource.Length);
                Array.Copy(inWeight, 0, _cWeight.Host, _cLinkAt[g], inWeight.Length);
                for (int p = 0; p < portPart.Length; p++)
                {
                    int at = _cPortAt[g] + p;
                    _cPortPart.Host[at] = portPart[p];
                    _cPortChannel.Host[at] = (int)portChannel[p];
                    _cPortIndex.Host[at] = portIndex[p];
                }
            }
            else
            {
                s.CUnits.Host[i] = -1;
            }

            // ---- the state
            if (c.CardBasePairHolds())
            {
                // The float and carry the card last held, as a restore brings them back (round 60): the
                // split below would agree but at a tie.
                for (int a = 0; a < 3; a++)
                {
                    s.BasePos.Host[a * N + i] = (Real)c.CardBaseHi[a];
                    s.BaseCarry.Host[a * N + i] = (Real)c.CardBaseLo[a];
                }
            }
            else
            {
                s.BasePos.Host[i] = (Real)c.BasePosition.X;
                s.BasePos.Host[N + i] = (Real)c.BasePosition.Y;
                s.BasePos.Host[2 * N + i] = (Real)c.BasePosition.Z;
                // The rest the float could not hold, as the step's compensated sum carries it: the position
                // is BasePos less BaseCarry. Zero in double.
                s.BaseCarry.Host[i] = (Real)(s.BasePos.Host[i] - c.BasePosition.X);
                s.BaseCarry.Host[N + i] = (Real)(s.BasePos.Host[N + i] - c.BasePosition.Y);
                s.BaseCarry.Host[2 * N + i] = (Real)(s.BasePos.Host[2 * N + i] - c.BasePosition.Z);
            }
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

            if (c.Drive.BendAndPower)
            {
                float[] bend = c.BendSignal;
                EffectorDrive e = c.Drive;
                for (int d = 0; d < s.D; d++)
                {
                    int at = d * N + i;
                    s.Bend.Host[at] = d < bend.Length ? bend[d] : 0f;
                    bool has = d < e.MirrorPower.Length;
                    s.EPower.Host[at] = has ? e.MirrorPower[d] : 0.0;
                    s.EBend.Host[at] = has ? e.MirrorBend[d] : 0.5;
                    s.EPowerTarget.Host[at] = has ? e.MirrorPowerTarget[d] : 0.0;
                    s.EBendTarget.Host[at] = has ? e.MirrorBendTarget[d] : 0.5;
                    s.EPowerRate.Host[at] = has ? e.MirrorPowerRate[d] : 0.0;
                    s.EBendRate.Host[at] = has ? e.MirrorBendRate[d] : 0.0;
                    s.EHoldLeft.Host[at] = has ? e.MirrorHoldLeft[d] : 0.0;
                }
            }
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
                s.Mem.Host[n * N + i] = n < memory.Length ? memory[n] : 0f;
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
                // The float and rest the card last held, where they still sum to the host's centres (a
                // restore brings them back, round 60): the rest can pass half a float step, so the split
                // below is not always the card's.
                bool pair = c.CardLinkPairHolds();
                for (int l = 0; l < links; l++)
                {
                    if (pair)
                    {
                        for (int a = 0; a < 3; a++)
                        {
                            sph[7 + a].Host[row + l] = (Real)c.CardLinkCentreHi[3 * l + a];
                            sph[14 + a].Host[row + l] = (Real)c.CardLinkCentreLo[3 * l + a];
                        }
                    }
                    else
                    {
                        sph[7].Host[row + l] = (Real)c.LinkContactCentre[3 * l];
                        sph[8].Host[row + l] = (Real)c.LinkContactCentre[3 * l + 1];
                        sph[9].Host[row + l] = (Real)c.LinkContactCentre[3 * l + 2];
                        // What the float centre leaves out of the creature's own; zero in double.
                        sph[14].Host[row + l] = (Real)(c.LinkContactCentre[3 * l] - sph[7].Host[row + l]);
                        sph[15].Host[row + l] = (Real)(c.LinkContactCentre[3 * l + 1] - sph[8].Host[row + l]);
                        sph[16].Host[row + l] = (Real)(c.LinkContactCentre[3 * l + 2] - sph[9].Host[row + l]);
                    }

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

            // D228: each link's committed turn with its sphere, and its core and margin (rewritten at a growth
            // resize, which packs the body again)
            if (_w.TrueShape && c.LinkContactActive)
            {
                for (int l = 0; l < links; l++)
                {
                    for (int k = 0; k < 9; k++) sph[17 + k].Host[row + l] = (Real)c.LinkContactRotation[9 * l + k];
                    for (int k = 0; k < 3; k++) _core[k].Host[row + l] = (Real)c.LinkCoreHalf[3 * l + k];
                    _core[3].Host[row + l] = (Real)c.LinkMargin[l];
                }
            }

            // D228 level (b): each link's committed spin with its sphere. Only a world at TrueShapeAtPoint has the array.
            if (_w.AtPoint && c.LinkContactActive && c.LinkContactSpin != null)
            {
                for (int l = 0; l < links; l++)
                {
                    for (int k = 0; k < 3; k++) sph[26 + k].Host[row + l] = (Real)c.LinkContactSpin[3 * l + k];
                }
            }

            // D162's touch of the rock, the record the next step's senses read
            if (_rock && c.RockTouched != null)
            {
                for (int l = 0; l < links; l++)
                {
                    _rockTouched.Host[row + l] = c.RockTouched[l] ? 1 : 0;
                    for (int k = 0; k < 3; k++)
                    {
                        _rockP[k].Host[row + l] = (Real)c.RockPoint[3 * l + k];
                        _rockP[3 + k].Host[row + l] = (Real)c.RockNormal[3 * l + k];
                    }
                }
            }

            // D162's grip: which links carry the cell, each one's breaking force and price as the CPU
            // takes them (Anchor.Form), and the grips the body holds. The price owed goes every block.
            if (_grip)
            {
                AnchoringCell cell = _w.AnchorCell;
                for (int l = 0; l < links; l++)
                {
                    int at = row + l;
                    bool anchors = c.Anchors != null && c.Anchors[l];
                    float breaking = anchors ? cell.BreakingForce((float)c.Volume[l]) : 0f;

                    _anchors.Host[at] = anchors ? 1 : 0;
                    _gripBreak.Host[at] = breaking;
                    _gripPrice.Host[at] = anchors ? cell.BuildPrice(breaking) : 0f;
                    _gripHeld.Host[at] = anchors && c.GripHeld[l] ? 1 : 0;
                    _gripStrength.Host[at] = anchors ? c.GripStrength[l] : 0.0;
                    for (int k = 0; k < 3; k++)
                    {
                        _gripAnchor.Host[3 * at + k] = anchors ? (Real)c.GripAnchor[3 * l + k] : 0;
                        _gripPoint.Host[3 * at + k] = anchors ? (Real)c.GripPoint[3 * l + k] : 0;
                    }
                    for (int k = 0; k < 9; k++) _gripAttitude.Host[9 * at + k] = anchors ? (Real)c.GripAttitude[9 * l + k] : 0;
                }

                _reserveLow.Host[g] = c.ReserveLowSeconds;
                _gripHeldSeconds.Host[g] = c.GripHeldSeconds;
                _gripsFormed.Host[g] = c.GripsFormed;
                _gripsBroken.Host[g] = c.GripsBroken;
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

            // The farm drains the grips' price between blocks (Creature.DrainGripOwed).
            if (_grip) _gripOwed.Host[g] = c.GripOwedJoules;

            CreatureSenses senses = c.Senses;
            IReserveSource reserve = senses.Reserve;
            s.Reserve.Host[i] = reserve != null ? reserve.SecondsOfReserve : 0f;
            s.Gates.Host[i] = (senses.Nutrients != null ? 1 : 0) | (reserve != null ? 2 : 0) |
                              (senses.Scent != null ? 4 : 0) | (senses.LeafScent != null ? 8 : 0);

            bool[] contact = senses.Contact;
            float[] damage = senses.Damage;
            for (int l = 0; l < links; l++)
            {
                s.Contact.Host[l * N + i] = contact != null && l < contact.Length && contact[l] ? 1 : 0;
                s.Damage.Host[l * N + i] = damage != null && l < damage.Length ? damage[l] : 0f;
            }

            // D235: each nose link's held sniff, as Simulation's Sniff left it in the body's CreatureSenses before
            // this block (a block never crosses a metabolic step). The six floats are copied as they stand; the
            // kernel reads them for a nose link only, so the rows of any other link are never written here.
            if (_w.Nose)
            {
                for (int l = 0; l < links; l++)
                {
                    if (!senses.IsNose(l)) continue;

                    senses.HeldSmell(l, out float chemical, out float scent, out float leaf,
                                     out float directionX, out float directionY, out float directionZ);
                    int held = 6 * l;
                    s.NoseHeld.Host[held * N + i] = chemical;
                    s.NoseHeld.Host[(held + 1) * N + i] = scent;
                    s.NoseHeld.Host[(held + 2) * N + i] = leaf;
                    s.NoseHeld.Host[(held + 3) * N + i] = directionX;
                    s.NoseHeld.Host[(held + 4) * N + i] = directionY;
                    s.NoseHeld.Host[(held + 5) * N + i] = directionZ;
                }
            }

            s.Enabled.Host[i] = c.HasTrace ? 1 : 0;
            s.Poison.Host[i] = c.MirrorPoisonedLink;
            s.Pending.Host[i] = c.MirrorResizedSinceLastFrame ? 1 : 0;

            _gLinks.Host[g] = links;
            _gId.Host[g] = c.Id;
            _gRank.Host[g] = _slots.Rank[g];
            _gAlive.Host[g] = c.Alive ? 1 : 0;
            _gJointed.Host[g] = c.Jointed ? 1 : 0;
            _gRemains.Host[g] = c.Remains ? 1 : 0;
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

            c.BasePosition = new Vec3((double)s.BasePos.Host[i] - s.BaseCarry.Host[i],
                                      (double)s.BasePos.Host[N + i] - s.BaseCarry.Host[N + i],
                                      (double)s.BasePos.Host[2 * N + i] - s.BaseCarry.Host[2 * N + i]);

            // The pair itself, which a checkpoint carries and the next pack takes back (round 60).
            double[] baseHi = c.CardBaseHi ?? (c.CardBaseHi = new double[3]);
            double[] baseLo = c.CardBaseLo ?? (c.CardBaseLo = new double[3]);
            for (int a = 0; a < 3; a++)
            {
                baseHi[a] = s.BasePos.Host[a * N + i];
                baseLo[a] = s.BaseCarry.Host[a * N + i];
            }
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

            if (c.Drive.BendAndPower)
            {
                float[] bend = c.BendSignal;
                int bends = Math.Min(bend.Length, s.D);
                for (int d = 0; d < bends; d++) bend[d] = s.Bend.Host[d * N + i];

                EffectorDrive e = c.Drive;
                int envelope = Math.Min(e.MirrorPower.Length, s.D);
                for (int d = 0; d < envelope; d++)
                {
                    int at = d * N + i;
                    e.MirrorPower[d] = s.EPower.Host[at];
                    e.MirrorBend[d] = s.EBend.Host[at];
                    e.MirrorPowerTarget[d] = s.EPowerTarget.Host[at];
                    e.MirrorBendTarget[d] = s.EBendTarget.Host[at];
                    e.MirrorPowerRate[d] = s.EPowerRate.Host[at];
                    e.MirrorBendRate[d] = s.EBendRate.Host[at];
                    e.MirrorHoldLeft[d] = s.EHoldLeft.Host[at];
                }
                e.MirrorStrokeFromPower();
            }
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
                if (n < memory.Length) memory[n] = s.Mem.Host[n * N + i];

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
                if (c.CardLinkCentreHi == null || c.CardLinkCentreHi.Length != 3 * links)
                {
                    c.CardLinkCentreHi = new double[3 * links];
                    c.CardLinkCentreLo = new double[3 * links];
                }

                double[] hi = c.CardLinkCentreHi, lo = c.CardLinkCentreLo;

                for (int l = 0; l < links; l++)
                {
                    for (int a = 0; a < 3; a++)
                    {
                        hi[3 * l + a] = sph[7 + a].Host[row + l];
                        lo[3 * l + a] = sph[14 + a].Host[row + l];
                    }

                    double x = (double)sph[7].Host[row + l] + sph[14].Host[row + l];
                    double y = (double)sph[8].Host[row + l] + sph[15].Host[row + l];
                    double z = (double)sph[9].Host[row + l] + sph[16].Host[row + l];
                    double r = sph[10].Host[row + l];
                    double vx = sph[11].Host[row + l], vy = sph[12].Host[row + l], vz = sph[13].Host[row + l];

                    c.LinkContactCentre[3 * l] = x; c.LinkContactCentre[3 * l + 1] = y; c.LinkContactCentre[3 * l + 2] = z;
                    c.LinkContactRadius[l] = r;
                    c.LinkContactVelocity[3 * l] = vx; c.LinkContactVelocity[3 * l + 1] = vy; c.LinkContactVelocity[3 * l + 2] = vz;

                    pc[3 * l] = x; pc[3 * l + 1] = y; pc[3 * l + 2] = z;
                    pr[l] = r;
                    pv[3 * l] = vx; pv[3 * l + 1] = vy; pv[3 * l + 2] = vz;
                }

                // D228: the turns, committed and pending alike (the next pack and a checkpoint read them).
                if (_w.TrueShape)
                {
                    double[] pq = c.MirrorPendingLinkRotation;
                    for (int l = 0; l < links; l++)
                    {
                        for (int k = 0; k < 9; k++)
                        {
                            double q = sph[17 + k].Host[row + l];
                            c.LinkContactRotation[9 * l + k] = q;
                            pq[9 * l + k] = q;
                        }
                    }
                }

                // D228 level (b): the spins, committed and pending alike, as the turns are.
                if (_w.AtPoint && c.LinkContactSpin != null)
                {
                    double[] ps = c.MirrorPendingLinkSpin;
                    for (int l = 0; l < links; l++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            double q = sph[26 + k].Host[row + l];
                            c.LinkContactSpin[3 * l + k] = q;
                            ps[3 * l + k] = q;
                        }
                    }
                }
            }

            if (_rock && c.RockTouched != null)
            {
                int row = g * WholeStep.LinkStride;
                for (int l = 0; l < links; l++)
                {
                    c.RockTouched[l] = _rockTouched.Host[row + l] != 0;
                    for (int k = 0; k < 3; k++)
                    {
                        c.RockPoint[3 * l + k] = _rockP[k].Host[row + l];
                        c.RockNormal[3 * l + k] = _rockP[3 + k].Host[row + l];
                    }
                }
            }

            if (_grip)
            {
                if (c.Anchors != null)
                {
                    int row = g * WholeStep.LinkStride;
                    for (int l = 0; l < links; l++)
                    {
                        int at = row + l;
                        c.GripHeld[l] = _gripHeld.Host[at] != 0;
                        c.GripStrength[l] = _gripStrength.Host[at];
                        for (int k = 0; k < 3; k++)
                        {
                            c.GripAnchor[3 * l + k] = _gripAnchor.Host[3 * at + k];
                            c.GripPoint[3 * l + k] = _gripPoint.Host[3 * at + k];
                        }
                        for (int k = 0; k < 9; k++) c.GripAttitude[9 * l + k] = _gripAttitude.Host[9 * at + k];
                    }
                }

                c.ReserveLowSeconds = _reserveLow.Host[g];
                c.GripOwedJoules = _gripOwed.Host[g];
                c.GripHeldSeconds = _gripHeldSeconds.Host[g];
                c.GripsFormed = _gripsFormed.Host[g];
                c.GripsBroken = _gripsBroken.Host[g];
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
            foreach (Col<Real> c in _core) c.Dispose();
            _census.Dispose();
            _overflow.Dispose();
            _guard.Dispose();
            for (int set = 0; set < 2; set++)
            {
                foreach (Col<Real> c in _sph[set]) c.Dispose();
                _active[set].Dispose();
            }

            _lo.Dispose(); _hi.Dispose(); _counts.Dispose(); _start.Dispose(); _gcursor.Dispose();
            _items.Dispose(); _partial.Dispose(); _partialN.Dispose(); _cell.Dispose();
            _tileSum.Dispose(); _tileStart.Dispose();
            _giant.Dispose(); _isGiant.Dispose(); _giantN.Dispose(); _giantList.Dispose();
            _linkPush.Dispose(); _linkFlags.Dispose(); _stageN.Dispose();
            _touchNew.Dispose(); _touchAt.Dispose();
            if (!_rock)
            {
                _rockTouched.Dispose();
                foreach (Col<Real> c in _rockP) c.Dispose();
            }
            foreach (IColumn c in _gripStubs) c.Dispose();
            _linkFluid.Dispose(); _linkLimited.Dispose();
            _stageId.Dispose(); _stageSlot.Dispose(); _stagePart.Dispose();
            _inst.Dispose(); _wr.Dispose(); _wi.Dispose(); _stock.Dispose(); _scent.Dispose(); _leaf.Dispose();
            _gpuScent?.Dispose();
            _cSource.Dispose(); _cWeight.Dispose(); _cPortPart.Dispose(); _cPortChannel.Dispose(); _cPortIndex.Dispose();
        }
    }
}

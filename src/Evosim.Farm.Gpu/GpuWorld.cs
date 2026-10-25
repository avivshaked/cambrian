using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm.Gpu
{
    /// <summary>
    /// The world a GPU step reads, taken once from the solver's config and the objects it holds —
    /// the current, the bed, the reefs, the snow field — as plain numbers, with the refusals of
    /// logbook/specs/gpu-port-spec.md section 3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Read by reflection, not by a change to Core.</b> The current's tables, the bed's modes
    /// and the reefs' derived bounds are private, and a public accessor for each would be a
    /// change to Core for the benefit of one reader; the spike's host read them the same way.
    /// Every field is read by name and a missing one throws naming it, so a rename in Core
    /// fails here loudly rather than packing a zero.
    /// </para>
    /// <para>
    /// <b>The instants are the CPU's arithmetic transcribed</b> (CurrentField.EnsureInstant):
    /// <see cref="Instant"/> writes what the field's pinned slot would hold at the samplers'
    /// phase and at the analytic acceleration's, and <see cref="VerifyInstant"/> holds the
    /// transcription against the field's own slot to the bit.
    /// </para>
    /// </remarks>
    public sealed class GpuWorld
    {
        public const int InstStride = 450;
        public const int ReefStride = 16;

        // ---- flags
        public bool HasCurrent, Accelerating, Sloped, HasBed, BedRelief, ShoreOn, FadeOn;
        public bool LimitDrive, LimitDrag, UseRestore, FloorRestores, CreatureContact, OffsetTorque, PerPart;

        /// <summary>D199/D200: under <see cref="LimitDrag"/>, the torque capped by the welded body.</summary>
        public bool WeldedDrag;

        // ---- the bend-and-power drive and the limb push (EffectorDrive, Fluid.Apply)
        public bool BendAndPower;
        public double PoseOmega, PushOmega, LimbPush;

        /// <summary>D229's fin law: the limb pushes take the part's shaped added mass (<c>Fluid.AddedMassOf</c>).</summary>
        public bool ShapedMass;

        /// <summary>D231: the powered stroke pushes the assembly welded beyond a jointed link (<c>SolverConfig.StrokePushesAssembly</c>).</summary>
        public bool StrokeAssembly;

        /// <summary>D228: a part touches as its convex core with a margin (<c>SolverConfig.ContactShape</c>), not as the ball of its half-diagonal.</summary>
        public bool TrueShape;

        // ---- D161's drag on turning (Fluid.Apply): 0 none, 1 skin, 2 spin, and each form's
        // negated factor as the CPU hoists it
        public int TurningDrag;
        public double NegSkin, NegSpin;

        // ---- D162's touch of the rock (Contacts.ApplyPerPart's record), for the sense and the grip
        public bool RockTouch;

        // ---- D162's grip (Anchor.Apply): the cell, whose breaking force and price the runner takes
        // per link as the CPU does, the strength's fall a step, and the reserve rule's threshold and lag
        public AnchoringCell AnchorCell;
        public double GripDecay, GripThreshold, GripLag;

        // ---- the solver's scalars
        public double Dt, Density, DragK, AddedMass, FluidAccel, Gravity, WorldDepth, Damping, SpinBudget;
        public double RestoringDensity, TissueDensity, TissueExcess, NeutralVolume;
        public double Omega, Zeta, MaxSep, MaxDv, TankRadius, AxisX, AxisZ;

        /// <summary>D204: the box's seams the contact reads across, m; 0 without them.</summary>
        public double SeamLength, SeamWidth;

        // ---- the bed
        public double BedRadius, BedDepth, TiltX, TiltZ, Offset, ShoalHeight;
        public int BedModes;

        // ---- the current
        public double CurDepth, CurTankR, EddyWeight, Overturning, PerSecond, Squared, ShoreDepth, ShoreFadeW;
        public float SigmaSloped, SigmaFlat, Speed;

        /// <summary>
        /// D204: the open ocean's current in a box (CurrentMode.Ocean), its own extents along x and z
        /// (CurrentField.LengthMetres and WidthMetres, the floats it folds with), and where its wave
        /// numbers sit in <see cref="WR"/>.
        /// </summary>
        public bool Ocean;
        public float OceanL, OceanW;
        public int OceanKAt;

        // ---- the reefs
        public double HalfThickness, Fillet, ReefFade;
        public int ReefCount;

        // ---- D194's coral (CoralRock), its rows in WR: see the kernel's coral section for their layout
        public int CoralCount, CoralCellsX, CoralCellsZ, CoralColonyAt, CoralPieceAt, CoralGridAt, CoralListAt;
        public int CoralFadeGridAt, CoralFadeListAt;
        public double CoralFillet, CoralFade, CoralFloor, CoralCell, CoralPeriodX, CoralPeriodZ;

        // ---- the senses
        public float SWorldDepth, ChemHalf, EnergyScale, FlowScale, RateScale, ConstCE, DtF;
        public float ScentHalf;

        // ---- D167's leaf smell: the strength's half scale, m², as CreatureSenses takes it
        public float LeafHalf;

        /// <summary>The scent the bodies smell (World.Scent), once a body is given one.</summary>
        public ScentField Scent { get; private set; }

        /// <summary>The leaves' smell the bodies read (World.LeafScent, D167), once a body is given one.</summary>
        public ScentField LeafScent { get; private set; }

        // ---- the snow field the chemical sense reads, once a body is given one
        public GridField Field { get; private set; }
        public int Nx, Ny, Nz, LayerCount, RefugeLayers, LayerStride;
        public float CellMetres, FieldTankR, RefugeFraction, CellVolume;

        /// <summary>The snow grid's box, m (GridField.LengthMetres and WidthMetres): its seams in D204's square.</summary>
        public float FieldL, FieldW;
        public bool SplitColumns;

        /// <summary>The world's reals: the stream tables, the bed's modes, the reefs.</summary>
        public double[] WR;

        public int StreamAmpAt, StreamRateAt, CellAmpAt, BedAt, ReefAt;

        /// <summary>The field's integers: each column's lowest live layer and its live intervals.</summary>
        public int[] WI = new int[1];

        public int LowestAt, IntervalFirstAt, IntervalTopAt, IntervalBottomAt;

        /// <summary>Bumped when <see cref="WI"/> changes, so the runner uploads it once.</summary>
        public int FieldVersion { get; private set; }

        private readonly CurrentField _current;
        private readonly float _period;
        private readonly double[] _sPhase, _sRate, _sBreathPhase, _sBreathRate, _cPhase, _cRate, _cBreathPhase, _cBreathRate;
        private readonly double[] _oAmp, _oPhase, _oRate, _oBreathPhase, _oBreathRate;
        private FieldInfo _stockField;

        public SolverConfig Config { get; }

        /// <summary>Reads the world and refuses what the kernel does not carry, naming the setting.</summary>
        public GpuWorld(SolverConfig s)
        {
            Config = s ?? throw new ArgumentNullException(nameof(s));

            Refuse(s);

            Dt = s.StepSeconds;
            DtF = (float)s.StepSeconds;
            Density = s.Density;
            DragK = 0.5 * s.Density * s.DragCoefficient;
            TissueExcess = s.TissueExcessDensity;
            NeutralVolume = s.NeutralBodyVolume;
            double restoringFraction = s.SurfaceRestoringFraction;
            RestoringDensity = restoringFraction * s.TissueDensity;
            UseRestore = restoringFraction > 0;
            FloorRestores = !s.FloorIsSolid;
            TissueDensity = s.TissueDensity;
            FluidAccel = s.FluidAccelerationCoefficient;
            AddedMass = s.AddedMassCoefficient;
            Gravity = s.GravityMetresPerSecondSquared;
            WorldDepth = s.WorldDepthMetres;
            Damping = s.JointDriveDamping;
            LimitDrive = s.LimitersEngage;
            LimitDrag = s.DragLimiterEngages;
            WeldedDrag = s.DragLimitByWeldedBody;
            SpinBudget = s.StepSeconds > 0 ? s.MaxJointAngularVelocity / s.StepSeconds : 0;
            OffsetTorque = s.BuoyancyOffsetTorque;
            PerPart = s.ContactPerPart;

            BendAndPower = s.PowerPushRadiansPerSecond > 0;
            PoseOmega = 2.0 * Math.PI * s.PoseHertz;
            PushOmega = s.PowerPushRadiansPerSecond;
            LimbPush = s.LimbPushCoefficient;
            ShapedMass = s.ShapedAddedMass;
            StrokeAssembly = s.StrokePushesAssembly;
            TrueShape = s.ContactShape == ContactShapeForm.TrueShape;

            TurningDrag = s.TurningDrag == TurningDragForm.Skin ? 1 : s.TurningDrag == TurningDragForm.Spin ? 2 : 0;
            NegSkin = -(0.5 * s.Density * s.SkinFrictionCoefficient);
            NegSpin = -(0.5 * s.Density * s.SpinDragCoefficient);

            RockTouch = s.RockTouch || s.Anchoring != null;

            AnchorCell = s.Anchoring;
            if (s.Anchoring != null)
            {
                GripDecay = Math.Exp(-s.StepSeconds / s.Anchoring.DecaySeconds);
                GripThreshold = s.Anchoring.ReserveThresholdSeconds;
                GripLag = s.Anchoring.ReserveLagSeconds;
            }

            CreatureContact = s.CreatureContact;
            Omega = s.ContactOmega;
            Zeta = s.ContactDampingRatio;
            MaxSep = s.MaxSeparationSpeed;
            MaxDv = s.MaxContactSpeedChange;
            TankRadius = s.TankRadiusMetres;
            AxisX = s.TankAxisX;
            AxisZ = s.TankAxisZ;
            SeamLength = s.SeamLengthMetres;
            SeamWidth = s.SeamWidthMetres;

            SWorldDepth = (float)Math.Max(0.001, s.WorldDepthMetres);
            ChemHalf = (float)Math.Max(1e-6, s.ChemicalHalfScaleJoulesPerCubicMetre);
            ScentHalf = (float)Math.Max(1e-6, s.ScentHalfScaleJoules);
            LeafHalf = (float)Math.Max(1e-6, s.LeafScentHalfScaleSquareMetres);
            EnergyScale = (float)Math.Max(1e-6, s.EnergyFullScaleSeconds);
            FlowScale = (float)Math.Max(1e-6, s.FlowFullScaleMetresPerSecond);
            RateScale = (float)Math.Max(1e-6, s.JointRateFullScale);
            ConstCE = s.ConstantChemicalAndEnergy;

            var reals = new List<double>();

            // ---- the current
            CurrentField current = s.Current;
            _current = current;
            HasCurrent = current != null;
            Accelerating = s.FluidAccelerationCoefficient > 0 && current != null;

            StreamAmpAt = reals.Count;
            if (HasCurrent)
            {
                // The two lazy builds, as the world's own pin takes them.
                current.PinInstant(0.0);
                current.UnpinInstant();

                Speed = (float)Reflect.Field(current, "_speed");
                Ocean = current.Mode == CurrentMode.Ocean && current.Shape != WorldShape.Tank;

                // A still current builds no streams (CurrentField.PinInstant returns before its lazy
                // build at speed 0), and every table below would be null. Named here rather than met
                // as a NullReferenceException (2026-10-10).
                if (!(Speed > 0f) || Reflect.Field(current, Ocean ? "_oceanAmplitude" : "_streamAmplitude") == null)
                {
                    throw new NotSupportedException(
                        "The gpu engine carries a moving current, and this world's is still (EVOSIM_CURRENT " +
                        "0 m/s), so its streams were never built. Give the current a speed, or run the cpu engine.");
                }

                float scale = (float)Reflect.Field(current, "_streamsScale");
                double bedScale = (double)Reflect.Field(current, "_bedScale");
                _period = Convert.ToSingle(Reflect.Field(current, "_periodSeconds"), CultureInfo.InvariantCulture);
                CurDepth = (float)Reflect.Field(current, "_depthMetres");
                CurTankR = (float)Reflect.Field(current, "_tankRadiusMetres");
                EddyWeight = (double)Reflect.Field(current, "_streamsEddyWeight");
                Overturning = (double)Reflect.Field(current, "_streamsOverturning");
                FadeOn = (bool)Reflect.Field(current, "_fadeOn");
                ShoreDepth = (double)Reflect.Field(current, "_shoreDepth");
                ShoreFadeW = (double)Reflect.Field(current, "_shoreFade");

                if ((double)Reflect.Field(current, "_envelopeOverride") != 0)
                {
                    throw new NotSupportedException(
                        "The current holds its envelope at a constant (CurrentField's envelope override, a " +
                        "test knob); the gpu engine carries the breathing envelope only.");
                }

                if (Ocean)
                {
                    // CurrentField.Ocean.cs: 25 vectors of 3 layers, its wave numbers into the reals and
                    // the rest for the instants, which are transcribed on the host.
                    var kx = (double[])Reflect.Field(current, "_oceanKx");
                    var kz = (double[])Reflect.Field(current, "_oceanKz");
                    _oAmp = (double[])Reflect.Field(current, "_oceanAmplitude");
                    _oPhase = (double[])Reflect.Field(current, "_oceanPhase");
                    _oRate = (double[])Reflect.Field(current, "_oceanRate");
                    _oBreathPhase = (double[])Reflect.Field(current, "_oceanBreathPhase");
                    _oBreathRate = (double[])Reflect.Field(current, "_oceanBreathRate");
                    if (kx.Length != 25 || kz.Length != 25 || _oAmp.Length != 75 || _oRate.Length != 75)
                    {
                        throw new NotSupportedException(
                            "The ocean's tables are not the 25 vectors of 3 layers the kernel is written for.");
                    }

                    StreamRateAt = StreamAmpAt;
                    CellAmpAt = StreamAmpAt;
                    OceanKAt = reals.Count;
                    reals.AddRange(kx);
                    reals.AddRange(kz);
                    OceanL = current.LengthMetres;
                    OceanW = current.WidthMetres;
                }
                else
                {
                    var streamAmp = (double[])Reflect.Field(current, "_streamAmplitude");
                    var streamRate = (double[])Reflect.Field(current, "_streamRate");
                    var cellAmp = (double[])Reflect.Field(current, "_cellAmplitude");
                    if (streamAmp.Length != 24 || streamRate.Length != 24 || cellAmp.Length != 3)
                    {
                        throw new NotSupportedException(
                            "The current's tables are not the 24 terms and 3 cells the kernel is written for.");
                    }

                    reals.AddRange(streamAmp);
                    StreamRateAt = reals.Count;
                    reals.AddRange(streamRate);
                    CellAmpAt = reals.Count;
                    reals.AddRange(cellAmp);

                    _sPhase = (double[])Reflect.Field(current, "_streamPhase");
                    _sRate = streamRate;
                    _sBreathPhase = (double[])Reflect.Field(current, "_streamBreathPhase");
                    _sBreathRate = (double[])Reflect.Field(current, "_streamBreathRate");
                    _cPhase = (double[])Reflect.Field(current, "_cellPhase");
                    _cRate = (double[])Reflect.Field(current, "_cellRate");
                    _cBreathPhase = (double[])Reflect.Field(current, "_cellBreathPhase");
                    _cBreathRate = (double[])Reflect.Field(current, "_cellBreathRate");
                }

                BedShape curBed = current.Bed;
                Sloped = curBed != null;
                if (Sloped && !ReferenceEquals(curBed, s.Bed))
                {
                    throw new NotSupportedException(
                        "The current follows a different bed from the one the bodies land on; the gpu " +
                        "engine carries one bed (SolverConfig.Bed and CurrentField.Bed must be one object).");
                }

                if (!ReferenceEquals(current.Coral, s.Coral))
                {
                    throw new NotSupportedException(
                        "The current fades around different coral from the coral the bodies touch; the gpu " +
                        "engine carries one (SolverConfig.Coral and CurrentField.Coral must be one object).");
                }

                // The coral's ocean takes its chain rule with no overturning (CurrentField.CoralOceanAccelerationAt
                // passes 0); the kernel reads cfg.Overturning, which is 0 in the ocean.
                if (Ocean && current.Coral != null && Overturning != 0)
                {
                    throw new NotSupportedException(
                        "The ocean's coral fade reads an overturning of 0, and this current carries " +
                        Overturning.ToString("R", CultureInfo.InvariantCulture) + ".");
                }

                if (!ReferenceEquals(current.Reefs, s.Reefs))
                {
                    throw new NotSupportedException(
                        "The current fades around different reefs from the ones the bodies land on; the gpu " +
                        "engine carries one set (SolverConfig.Reefs and CurrentField.Reefs must be one object).");
                }

                if (Ocean)
                {
                    // OceanAt: `u * (float)(_speed * _oceanScale)`, flat or sloped alike, and
                    // OceanAccelerationAt's `(double)_speed * _oceanScale`.
                    double oceanScale = (double)Reflect.Field(current, "_oceanScale");
                    SigmaFlat = (float)(Speed * oceanScale);
                    SigmaSloped = SigmaFlat;

                    double clock = 2.0 * Math.PI / _period;
                    double sigma = (double)Speed * oceanScale;
                    PerSecond = sigma * clock;
                    Squared = sigma * sigma;
                }
                else
                {
                    // StreamsAt: `_speed * _streamsScale` is a float product; the sloped multiplier
                    // widens it, multiplies by the double _bedScale and narrows.
                    float flat = Speed * scale;
                    SigmaFlat = flat;
                    SigmaSloped = (float)(flat * bedScale);

                    // StreamsAccelerationAt and ReefStreamsAccelerationAt.
                    double clock = 2.0 * Math.PI / _period;
                    double sigma = (double)Speed * scale * bedScale;
                    PerSecond = sigma * clock;
                    Squared = sigma * sigma;
                }
            }
            else
            {
                StreamRateAt = StreamAmpAt;
                CellAmpAt = StreamAmpAt;
            }

            // ---- the bed
            BedAt = reals.Count;
            HasBed = s.Bed != null;
            if (HasBed)
            {
                BedShape bed = s.Bed;
                var kx = (double[])Reflect.Field(bed, "_kx");
                var kz = (double[])Reflect.Field(bed, "_kz");
                var amp = (double[])Reflect.Field(bed, "_amplitude");
                var phase = (double[])Reflect.Field(bed, "_phase");

                BedModes = amp.Length;
                for (int m = 0; m < BedModes; m++)
                {
                    reals.Add(kx[m]);
                    reals.Add(kz[m]);
                    reals.Add(phase[m]);
                    reals.Add(amp[m]);
                }

                TiltX = (double)Reflect.Field(bed, "_tiltX");
                TiltZ = (double)Reflect.Field(bed, "_tiltZ");
                Offset = (double)Reflect.Field(bed, "_offset");
                BedRadius = (double)Reflect.Field(bed, "_radius");
                ShoreOn = (bool)Reflect.Field(bed, "_shoreOn");
                ShoalHeight = (double)Reflect.Field(bed, "_shoalHeight");
                BedDepth = (double)bed.DepthMetres;
                BedRelief = bed.HasRelief;
            }

            // ---- the reefs
            ReefAt = reals.Count;
            ReefGeometry reefs = s.Reefs;
            if (reefs != null)
            {
                ReefCount = reefs.Count;
                HalfThickness = (double)Reflect.Field(reefs, "_halfThickness");
                Fillet = (double)Reflect.Field(reefs, "_fillet");
                ReefFade = (double)reefs.FadeMetres;

                var capMidY = (double[])Reflect.Field(reefs, "_capMidY");
                var outerMax = (double[])Reflect.Field(reefs, "_outerMax");
                var cosMin = (double[])Reflect.Field(reefs, "_cosMin");
                var reach = (double[])Reflect.Field(reefs, "_reach");

                for (int r = 0; r < ReefCount; r++)
                {
                    ReefGeometry.Reef reef = reefs.ReefAt(r);
                    reals.Add(reef.X);
                    reals.Add(reef.Z);
                    reals.Add(reef.CapRadius);
                    reals.Add(reef.CapDepth);
                    reals.Add(reef.StemRadius);
                    reals.Add(reef.A2);
                    reals.Add(reef.A3);
                    reals.Add(reef.A4);
                    reals.Add(reef.P2);
                    reals.Add(reef.P3);
                    reals.Add(reef.P4);
                    reals.Add(reef.Round ? 1.0 : 0.0);
                    reals.Add(capMidY[r]);
                    reals.Add(outerMax[r]);
                    reals.Add(cosMin[r]);
                    reals.Add(reach[r]);
                }
            }

            // ---- D194's coral: CoralRock's own arrays, read as the reefs' are (a renamed field fails the build's
            // tests, not the compiler; GpuCoralTests checks the packing against the rock's public readers).
            CoralRock coral = s.Coral;
            if (coral != null)
            {
                CoralCount = coral.Count;
                CoralFillet = (double)Reflect.Field(coral, "_fillet");
                CoralFade = coral.FadeMetres;
                CoralFloor = coral.FadeFloor;
                CoralCell = (double)Reflect.Field(coral, "_cell");
                CoralCellsX = (int)Reflect.Field(coral, "_cellsX");
                CoralCellsZ = (int)Reflect.Field(coral, "_cellsZ");
                CoralPeriodX = (double)Reflect.Field(coral, "_periodX");
                CoralPeriodZ = (double)Reflect.Field(coral, "_periodZ");

                string[] rows = { "_minX", "_minY", "_minZ", "_maxX", "_maxY", "_maxZ", "_allowance",
                                  "_cx", "_cy", "_cz", "_ax", "_ay", "_az", "_amin" };
                var columns = new double[rows.Length][];
                for (int k = 0; k < rows.Length; k++) columns[k] = (double[])Reflect.Field(coral, rows[k]);

                CoralGeometry geometry = coral.Coral;
                CoralColonyAt = reals.Count;
                for (int c = 0; c < CoralCount; c++)
                {
                    for (int k = 0; k < rows.Length; k++) reals.Add(columns[k][c]);
                    CoralGeometry.Colony colony = geometry.ColonyAt(c);
                    reals.Add(colony.FirstPiece);
                    reals.Add(colony.PieceCount);
                }

                CoralPieceAt = reals.Count;
                for (int p = 0; p < geometry.PieceCount; p++)
                {
                    CoralGeometry.Piece piece = geometry.PieceAt(p);
                    reals.Add(piece.Kind == CoralGeometry.PieceKind.Plate ? 1.0 : 0.0);
                    reals.Add(piece.AX); reals.Add(piece.AY); reals.Add(piece.AZ);
                    reals.Add(piece.BX); reals.Add(piece.BY); reals.Add(piece.BZ);
                    reals.Add(piece.Radius);
                    reals.Add(piece.HalfThickness);
                }

                var starts = (int[])Reflect.Field(coral, "_cellStart");
                var lists = (int[])Reflect.Field(coral, "_cellColonies");
                if (starts.Length != CoralCellsX * CoralCellsZ + 1 || lists.Length >= (1 << 24))
                {
                    throw new NotSupportedException(
                        "The coral's grid is not the cells and lists the kernel reads (CoralRock's _cellStart, _cellColonies).");
                }

                CoralGridAt = reals.Count;
                foreach (int v in starts) reals.Add(v);
                CoralListAt = reals.Count;
                foreach (int v in lists) reals.Add(v);

                var fadeStarts = (int[])Reflect.Field(coral, "_fadeStart");
                var fadeLists = (int[])Reflect.Field(coral, "_fadeColonies");
                if (fadeStarts.Length != CoralCellsX * CoralCellsZ + 1 || fadeLists.Length >= (1 << 24))
                {
                    throw new NotSupportedException(
                        "The coral's fade lists are not the cells and lists the kernel reads (CoralRock's _fadeStart, _fadeColonies).");
                }

                CoralFadeGridAt = reals.Count;
                foreach (int v in fadeStarts) reals.Add(v);
                CoralFadeListAt = reals.Count;
                foreach (int v in fadeLists) reals.Add(v);
            }

            if (reals.Count == 0) reals.Add(0);
            WR = reals.ToArray();
        }

        /// <summary>
        /// logbook/specs/gpu-port-spec.md section 3, and the kernel's own ceilings: each refusal
        /// names the setting that would have to change.
        /// </summary>
        public static void Refuse(SolverConfig s)
        {
            bool box = !(s.TankRadiusMetres > 0);
            if (box && !(s.SeamLengthMetres > 0 && s.SeamWidthMetres > 0))
            {
                throw new NotSupportedException(
                    "The gpu engine steps a box only with its seams read across (EVOSIM_READ_ACROSS_SEAMS 1, " +
                    "D204); this box's seams the farm wraps between steps.");
            }

            if (s.ContactShape == ContactShapeForm.TrueShape && !s.ContactPerPart)
            {
                throw new NotSupportedException(
                    "The gpu engine takes the true shape (EVOSIM_CONTACT_SHAPE trueshape) as the narrowphase of " +
                    "per-part contact, and EVOSIM_CONTACT_PER_PART is off.");
            }

            if (s.WaterHoldSeconds > 0)
            {
                throw new NotSupportedException(
                    "The gpu engine samples the water at every link on every step; EVOSIM_WATER_HOLD is " +
                    s.WaterHoldSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " s (D100's hold is not carried).");
            }

            // The farm's step is a float widened (0.02f is 0.019999999552965164), so a step is
            // one of the two when it is within a float's rounding of it.
            double dt = s.StepSeconds;
            if (Math.Abs(dt - 0.01) > 1e-8 && Math.Abs(dt - 0.02) > 1e-8)
            {
                throw new NotSupportedException(
                    "The gpu engine runs at EVOSIM_DT 0.01 or 0.02; this world steps at " +
                    dt.ToString("R", CultureInfo.InvariantCulture) + " s.");
            }

            // A body over the largest class (16 links, DevelopmentLimits.MaxParts' default) is not a
            // world refusal: it is refused at its first block and counted (GpuSlots).

            CurrentField current = s.Current;
            if (current == null) return;

            if (box || current.Shape != WorldShape.Tank)
            {
                if (current.Shape == WorldShape.Tank || current.Mode != CurrentMode.Ocean)
                {
                    throw new NotSupportedException(
                        "The gpu engine carries the tank's streams in a tank and the open ocean's current in a " +
                        "box (EVOSIM_CURRENT_MODE ocean); this current is " + current.Mode + " built for a " +
                        current.Shape + " (EVOSIM_SHAPE).");
                }

                if ((bool)Reflect.Field(current, "_fadeOn"))
                {
                    throw new NotSupportedException(
                        "The gpu engine carries the open ocean without the shore's fade (EVOSIM_BED_SHORE and " +
                        "EVOSIM_BED_SHORE_FADE 0); its faded field is not yet shown to match on the card.");
                }

                if (s.Reefs != null && s.Reefs.Count > 0)
                {
                    throw new NotSupportedException(
                        "The gpu engine carries the open ocean without reefs (EVOSIM_REEF_COVER 0); the reefs' " +
                        "fade around it is not yet shown to match on the card.");
                }
            }
            else if (current.Mode != CurrentMode.Transport)
            {
                throw new NotSupportedException(
                    "The gpu engine samples the water by place (EVOSIM_CURRENT_MODE Transport); the current's " +
                    "mode is " + current.Mode + ", which samples it by patch.");
            }

            if (current.VentActive(s.PatchCount))
            {
                throw new NotSupportedException(
                    "The gpu engine carries no vent; EVOSIM_VENT is on in a world of " +
                    s.PatchCount.ToString(CultureInfo.InvariantCulture) + " patches.");
            }
        }

        /// <summary>
        /// The scent field the scent sense reads: kept the first time a body is handed one, and
        /// refused when a second one appears. Its grid goes up every block.
        /// </summary>
        public void AttachScent(ScentField scent)
        {
            if (scent == null || ReferenceEquals(scent, Scent)) return;

            if (Scent != null)
            {
                throw new NotSupportedException(
                    "Two bodies smell two different scents; the gpu engine carries one scent field.");
            }

            Scent = scent;
        }

        /// <summary>
        /// The leaves' smell the two leaf senses read: kept the first time a body is handed one, and
        /// refused when a second one appears. Its grid goes up every block, as the scent's does.
        /// </summary>
        public void AttachLeafScent(ScentField leaf)
        {
            if (leaf == null || ReferenceEquals(leaf, LeafScent)) return;

            if (LeafScent != null)
            {
                throw new NotSupportedException(
                    "Two bodies smell two different leaf fields; the gpu engine carries one leaf smell.");
            }

            LeafScent = leaf;
        }

        /// <summary>
        /// The snow field the chemical sense reads: packed the first time a body is handed one, and
        /// refused when it is not the tank's grid or when a second one appears.
        /// </summary>
        public void AttachField(IMatterField field)
        {
            if (field == null || ReferenceEquals(field, Field)) return;

            if (Field != null)
            {
                throw new NotSupportedException(
                    "Two bodies smell two different fields; the gpu engine carries one snow field.");
            }

            // A tank's grid, or D204's box's: the kernel finds the cell by the tank's walk or by the
            // box's wrap (BoxCellAt).
            if (!(field is GridField grid))
            {
                throw new NotSupportedException(
                    "The gpu engine's chemical sense reads a grid field (EVOSIM_FIELD grid); a body was " +
                    "handed a " + field.GetType().Name + ".");
            }

            Field = grid;
            Nx = grid.CellsX;
            Ny = grid.CellsY;
            Nz = grid.CellsZ;
            LayerCount = grid.LayerCount;
            RefugeLayers = grid.RefugeLayerCount;
            LayerStride = Nx * Nz;
            CellMetres = grid.CellMetres;
            FieldTankR = grid.TankRadiusMetres;
            FieldL = grid.LengthMetres;
            FieldW = grid.WidthMetres;
            RefugeFraction = grid.RefugeEdibleFraction;
            CellVolume = grid.CellVolume;

            int columns = Nx * Nz;
            var lowest = new int[columns];
            var first = new int[columns + 1];
            var tops = new List<int>();
            var bottoms = new List<int>();
            SplitColumns = false;

            for (int ix = 0; ix < Nx; ix++)
            for (int iz = 0; iz < Nz; iz++)
            {
                int column = ix * Nz + iz;
                lowest[column] = grid.LowestLiveLayer(ix, iz);
            }

            for (int column = 0; column < columns; column++)
            {
                first[column] = tops.Count;
                int ix = column / Nz, iz = column % Nz;
                int count = grid.LiveIntervalCount(ix, iz);
                if (count > 1) SplitColumns = true;

                for (int k = 0; k < count; k++)
                {
                    (int top, int bottom) = grid.LiveInterval(ix, iz, k);
                    tops.Add(top);
                    bottoms.Add(bottom);
                }
            }
            first[columns] = tops.Count;

            LowestAt = 0;
            IntervalFirstAt = columns;
            IntervalTopAt = IntervalFirstAt + columns + 1;
            IntervalBottomAt = IntervalTopAt + tops.Count;

            var wi = new int[IntervalBottomAt + bottoms.Count + 1];
            Array.Copy(lowest, 0, wi, LowestAt, columns);
            Array.Copy(first, 0, wi, IntervalFirstAt, columns + 1);
            tops.CopyTo(wi, IntervalTopAt);
            bottoms.CopyTo(wi, IntervalBottomAt);
            WI = wi;
            FieldVersion++;

            _stockField = typeof(GridField).GetField("_stock", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        /// <summary>The number of cells in the field's stock, 1 when there is none.</summary>
        public int StockLength => Field == null ? 1 : Nx * Ny * Nz;

        /// <summary>The field's stock as it stands, in its own index order.</summary>
        public void ReadStock(double[] into)
        {
            if (Field == null) { into[0] = 0; return; }

            if (_stockField?.GetValue(Field) is double[] stock && stock.Length == Nx * Ny * Nz)
            {
                Array.Copy(stock, into, stock.Length);
                return;
            }

            for (int iy = 0; iy < Ny; iy++)
            for (int ix = 0; ix < Nx; ix++)
            for (int iz = 0; iz < Nz; iz++)
            {
                into[(iy * Nx + ix) * Nz + iz] = Field.JoulesAt(ix, iy, iz);
            }
        }

        /// <summary>
        /// The instant the step at <paramref name="seconds"/> reads: CurrentField.EnsureInstant's
        /// arithmetic at the samplers' phase <c>2π·s/P</c> into slots 0-77 and at the analytic
        /// acceleration's <c>(2π/P)·s</c> into slots 78-185.
        /// </summary>
        public void Instant(double seconds, double[] into, int at)
        {
            if (!HasCurrent)
            {
                Array.Clear(into, at, InstStride);
                return;
            }

            double a = 2.0 * Math.PI * seconds / _period;
            double b = 2.0 * Math.PI / _period * seconds;

            if (Ocean)
            {
                FillOcean(a, into, at, false);
                FillOcean(b, into, at + 150, true);
                return;
            }

            Fill(a, into, at, false);
            Fill(b, into, at + 78, true);
        }

        /// <summary>
        /// CurrentField.EnsureOceanInstant's arithmetic: every mode's W·cos φ and W·sin φ, and with
        /// <paramref name="withRates"/> their clock derivatives after them, 75 apiece.
        /// </summary>
        private void FillOcean(double t, double[] into, int at, bool withRates)
        {
            for (int k = 0; k < 75; k++)
            {
                double rate = _oRate[k];
                double phase = _oPhase[k] + rate * t;
                double cos = Math.Cos(phase);
                double sin = Math.Sin(phase);

                double breath = _oBreathRate[k] * t + _oBreathPhase[k];
                double envelope = 0.75d + 0.25d * Math.Sin(breath);
                double envelopeRate = 0.25d * _oBreathRate[k] * Math.Cos(breath);

                double amp = _oAmp[k];

                into[at + k] = amp * envelope * cos;
                into[at + 75 + k] = amp * envelope * sin;
                if (!withRates) continue;
                into[at + 150 + k] = amp * (envelopeRate * cos - envelope * rate * sin);
                into[at + 225 + k] = amp * (envelopeRate * sin + envelope * rate * cos);
            }
        }

        private void Fill(double t, double[] into, int at, bool withRates)
        {
            for (int k = 0; k < 24; k++)
            {
                double psi = _sPhase[k] + _sRate[k] * t;
                double breath = _sBreathRate[k] * t + _sBreathPhase[k];
                into[at + k] = Math.Cos(psi);
                into[at + 24 + k] = Math.Sin(psi);
                into[at + 48 + k] = 0.75d + 0.25d * Math.Sin(breath);
                if (withRates) into[at + 72 + k] = 0.25d * _sBreathRate[k] * Math.Cos(breath);
            }

            for (int c = 0; c < 3; c++)
            {
                double breath = _cBreathRate[c] * t + _cBreathPhase[c];
                double turn = _cRate[c] * t + _cPhase[c];
                double env = 0.75d + 0.25d * Math.Sin(breath);
                double envRate = 0.25d * _cBreathRate[c] * Math.Cos(breath);
                double cell = Math.Cos(turn);
                double cellRate = -_cRate[c] * Math.Sin(turn);

                if (!withRates)
                {
                    into[at + 72 + c] = env;
                    into[at + 75 + c] = cell;
                }
                else
                {
                    into[at + 96 + c] = env;
                    into[at + 99 + c] = envRate;
                    into[at + 102 + c] = cell;
                    into[at + 105 + c] = cellRate;
                }
            }
        }

        /// <summary>
        /// The transcription held against the field's own slots: pin the field at a clock, read the
        /// slot for each phase by reflection, and compare every number to the bit. Returns the
        /// values compared and how many differed; (0, 0) in still water.
        /// </summary>
        public (int values, int mismatches) VerifyInstant(double seconds)
        {
            if (!HasCurrent) return (0, 0);

            var mine = new double[InstStride];
            Instant(seconds, mine, 0);

            _current.PinInstant(seconds);
            if (Ocean)
            {
                try { return VerifyOcean(seconds, mine); }
                finally { _current.UnpinInstant(); }
            }

            try
            {
                var slots = (Array)Reflect.Field(_current, "_slots");
                double a = 2.0 * Math.PI * seconds / _period;
                double b = 2.0 * Math.PI / _period * seconds;
                int values = 0, bad = 0;

                foreach (object slot in slots)
                {
                    if (slot == null) continue;

                    double held = (double)Reflect.Field(slot, "At");
                    bool isA = held == a, isB = held == b;
                    if (!isA && !isB) continue;

                    var cos = (double[])Reflect.Field(slot, "Cos");
                    var sin = (double[])Reflect.Field(slot, "Sin");
                    var env = (double[])Reflect.Field(slot, "Envelope");
                    var envRate = (double[])Reflect.Field(slot, "EnvelopeRate");
                    var cenv = (double[])Reflect.Field(slot, "CellEnvelope");
                    var cenvRate = (double[])Reflect.Field(slot, "CellEnvelopeRate");
                    var cell = (double[])Reflect.Field(slot, "Cell");
                    var cellRate = (double[])Reflect.Field(slot, "CellRate");

                    void Cmp(double x, double y)
                    {
                        values++;
                        if (BitConverter.DoubleToInt64Bits(x) != BitConverter.DoubleToInt64Bits(y)) bad++;
                    }

                    if (isA)
                    {
                        for (int k = 0; k < 24; k++) { Cmp(cos[k], mine[k]); Cmp(sin[k], mine[24 + k]); Cmp(env[k], mine[48 + k]); }
                        for (int c = 0; c < 3; c++) { Cmp(cenv[c], mine[72 + c]); Cmp(cell[c], mine[75 + c]); }
                    }

                    if (isB)
                    {
                        for (int k = 0; k < 24; k++)
                        {
                            Cmp(cos[k], mine[78 + k]); Cmp(sin[k], mine[102 + k]);
                            Cmp(env[k], mine[126 + k]); Cmp(envRate[k], mine[150 + k]);
                        }

                        for (int c = 0; c < 3; c++)
                        {
                            Cmp(cenv[c], mine[174 + c]); Cmp(cenvRate[c], mine[177 + c]);
                            Cmp(cell[c], mine[180 + c]); Cmp(cellRate[c], mine[183 + c]);
                        }
                    }
                }

                return (values, bad);
            }
            finally
            {
                _current.UnpinInstant();
            }
        }

        /// <summary><see cref="VerifyInstant"/> for the ocean: its pinned slots against <see cref="FillOcean"/>.</summary>
        private (int values, int mismatches) VerifyOcean(double seconds, double[] mine)
        {
            var slots = (Array)Reflect.Field(_current, "_oceanSlots");
            double a = 2.0 * Math.PI * seconds / _period;
            double b = 2.0 * Math.PI / _period * seconds;
            int values = 0, bad = 0;

            foreach (object slot in slots)
            {
                if (slot == null) continue;

                double held = (double)Reflect.Field(slot, "At");
                bool isA = held == a, isB = held == b;
                if (!isA && !isB) continue;

                var cos = (double[])Reflect.Field(slot, "Cos");
                var sin = (double[])Reflect.Field(slot, "Sin");
                var cosRate = (double[])Reflect.Field(slot, "CosRate");
                var sinRate = (double[])Reflect.Field(slot, "SinRate");

                void Cmp(double x, double y)
                {
                    values++;
                    if (BitConverter.DoubleToInt64Bits(x) != BitConverter.DoubleToInt64Bits(y)) bad++;
                }

                for (int k = 0; k < 75; k++)
                {
                    if (isA) { Cmp(cos[k], mine[k]); Cmp(sin[k], mine[75 + k]); }
                    if (isB)
                    {
                        Cmp(cos[k], mine[150 + k]); Cmp(sin[k], mine[225 + k]);
                        Cmp(cosRate[k], mine[300 + k]); Cmp(sinRate[k], mine[375 + k]);
                    }
                }
            }

            return (values, bad);
        }
    }

    /// <summary>Private fields read by name, walking the base types; a missing one throws naming it.</summary>
    internal static class Reflect
    {
        public static object Field(object o, string name)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return f.GetValue(o);
            }

            throw new MissingFieldException(o.GetType().Name, name);
        }
    }
}

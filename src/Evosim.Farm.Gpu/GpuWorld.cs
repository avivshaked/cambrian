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
        public const int InstStride = 186;
        public const int ReefStride = 16;

        // ---- flags
        public bool HasCurrent, Accelerating, Sloped, HasBed, BedRelief, ShoreOn, FadeOn;
        public bool LimitDrive, LimitDrag, UseRestore, FloorRestores, CreatureContact, OffsetTorque, PerPart;

        // ---- the solver's scalars
        public double Dt, Density, DragK, AddedMass, FluidAccel, Gravity, WorldDepth, Damping, SpinBudget;
        public double RestoringDensity, TissueDensity, TissueExcess, NeutralVolume;
        public double Omega, Zeta, MaxSep, MaxDv, TankRadius, AxisX, AxisZ;

        // ---- the bed
        public double BedRadius, BedDepth, TiltX, TiltZ, Offset, ShoalHeight;
        public int BedModes;

        // ---- the current
        public double CurDepth, CurTankR, EddyWeight, Overturning, PerSecond, Squared, ShoreDepth, ShoreFadeW;
        public float SigmaSloped, SigmaFlat, Speed;

        // ---- the reefs
        public double HalfThickness, Fillet, ReefFade;
        public int ReefCount;

        // ---- the senses
        public float SWorldDepth, ChemHalf, EnergyScale, FlowScale, RateScale, ConstCE, DtF;

        // ---- the snow field the chemical sense reads, once a body is given one
        public GridField Field { get; private set; }
        public int Nx, Ny, Nz, LayerCount, RefugeLayers, LayerStride;
        public float CellMetres, FieldTankR, RefugeFraction, CellVolume;
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
            SpinBudget = s.StepSeconds > 0 ? s.MaxJointAngularVelocity / s.StepSeconds : 0;
            OffsetTorque = s.BuoyancyOffsetTorque;
            PerPart = s.ContactPerPart;

            CreatureContact = s.CreatureContact;
            Omega = s.ContactOmega;
            Zeta = s.ContactDampingRatio;
            MaxSep = s.MaxSeparationSpeed;
            MaxDv = s.MaxContactSpeedChange;
            TankRadius = s.TankRadiusMetres;
            AxisX = s.TankAxisX;
            AxisZ = s.TankAxisZ;

            SWorldDepth = (float)Math.Max(0.001, s.WorldDepthMetres);
            ChemHalf = (float)Math.Max(1e-6, s.ChemicalHalfScaleJoulesPerCubicMetre);
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

                BedShape curBed = current.Bed;
                Sloped = curBed != null;
                if (Sloped && !ReferenceEquals(curBed, s.Bed))
                {
                    throw new NotSupportedException(
                        "The current follows a different bed from the one the bodies land on; the gpu " +
                        "engine carries one bed (SolverConfig.Bed and CurrentField.Bed must be one object).");
                }

                if (!ReferenceEquals(current.Reefs, s.Reefs))
                {
                    throw new NotSupportedException(
                        "The current fades around different reefs from the ones the bodies land on; the gpu " +
                        "engine carries one set (SolverConfig.Reefs and CurrentField.Reefs must be one object).");
                }

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

            if (reals.Count == 0) reals.Add(0);
            WR = reals.ToArray();
        }

        /// <summary>
        /// logbook/specs/gpu-port-spec.md section 3, and the kernel's own ceilings: each refusal
        /// names the setting that would have to change.
        /// </summary>
        public static void Refuse(SolverConfig s)
        {
            if (!(s.TankRadiusMetres > 0))
            {
                throw new NotSupportedException(
                    "The gpu engine steps a tank only (EVOSIM_SHAPE tank); this world is a box, whose " +
                    "seams the farm wraps between steps.");
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

            if (current.Shape != WorldShape.Tank)
            {
                throw new NotSupportedException(
                    "The gpu engine carries the tank's streams only; the current is built for a " +
                    current.Shape + " (EVOSIM_SHAPE).");
            }

            if (current.Mode != CurrentMode.Transport)
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

            if (!(field is GridField grid) || grid.Shape != WorldShape.Tank)
            {
                throw new NotSupportedException(
                    "The gpu engine's chemical sense reads a tank's grid field (EVOSIM_FIELD grid, EVOSIM_SHAPE " +
                    "tank); a body was handed a " + field.GetType().Name + ".");
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

            Fill(a, into, at, false);
            Fill(b, into, at + 78, true);
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

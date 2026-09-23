using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Evosim.Core;
using Evosim.Dynamics;

namespace Gpu.Spike4
{
    internal interface IStepRunner : IDisposable
    {
        double CompileMs { get; }
        string Precision { get; }
        int Buckets { get; }
        int InstantBlock { get; }
        long InstantBytes { get; }
        void ResetState();
        void UploadInstants(double[] block, int count);
        void UploadInstantsTimed(double[] block);
        void Step(bool serialMean, int instantIndex, long traceStep, double traceTime);
        void BodyOnly(int instantIndex, long traceStep, double traceTime);
        void GridOnly(bool serialMean);
        void EmptyLaunch();
        void Swap();
        void Spin(int iterations);
        void Synchronize();
        void ClearFlags();
        long ReadMetabolic();
        StepResult Read();
        ulong Digest();
    }

    /// <summary>What the whole-step kernel holds after a step, widened to double, strided as on the card.</summary>
    internal sealed class StepResult
    {
        public double[] BasePos, BaseRot, Q, Qd, Tau, LimitImplicit, BallRot, Pos, Rot, RotM, Spin, Vel, Sang, Slin,
            Cbias, RelVel, Water, WaterAcc, Fext, Ring;
        public int[] Alive, Cursor, Filled, Parity, TraceHeld, TraceCursor, TraceFirstBadLink, TraceResized;
        public float[] Signal, History, RunSum, S, Mem;
        public long[] DriveLimited, DragLimited, TraceStep, TraceFirstBadStep;
        public double[] Work, Signed, Dissipated, Passive, Clock, TraceTime;
        public int[] Found, Unique, NOver, Over, BedGlass;
        public double Cell;
        public int Entries, EntryOverflow;
        public double[] CCx, CCy, CCz, CR, CVx, CVy, CVz, PCx, PCy, PCz, PR, PVx, PVy, PVz;
        public int[] CActive, PActive;
    }

    /// <summary>
    /// Everything the whole step of N bodies reads and writes, struct-of-arrays and strided by body
    /// (<c>slot * N + body</c>), read off the real <see cref="Creature"/> objects the CPU reference
    /// steps and off the world's own <see cref="CurrentField"/>, so the kernel and the reference start
    /// from one set of numbers.
    /// </summary>
    /// <remarks>
    /// Private state is read, never written, by reflection: the drive's window
    /// (<c>EffectorDrive._history</c>, <c>_runningSum</c>, <c>_torquePerUnit</c>, <c>_cursor</c>,
    /// <c>_filled</c>), the pending contact sphere (<c>Creature._pending*</c>), the trace ring
    /// (<c>Creature._trace*</c>), the streams' constants (<c>CurrentField._stream*</c>,
    /// <c>_cell*</c>, <c>_speed</c>, <c>_streamsScale</c>, <c>_bedScale</c>, <c>_periodSeconds</c>,
    /// <c>_depthMetres</c>, <c>_tankRadiusMetres</c>, <c>_bed</c>) and the bed's modes, as the
    /// third spike's <c>ContactHost</c> reads them.
    /// </remarks>
    internal sealed class StepHost
    {
        public const int MaxLinks = 16, MaxDof = 48, Window = 10, TraceFrames = 3, TraceValues = 21, Inst = 186;

        public int N, MaxNeurons, MaxPanels, OverCap, EntryCap;
        public int[] Links, Dof, SigLen, Mask, Alive, NeuronCount, TraceEnabled;
        public int[] Parent, DofCount, DofStart, PanelStart, NOff, NCnt, BParent, FirstChild, BDofStart, BDofCount;
        public double[] Mass, Lift, Volume, SmallI, Reach, Inertia, ChildAnchor, ParentAnchor, JointFrame, RestFrame;
        public double[] LimitLo, LimitHi, LimitStiff, LimitDamp, Excess, TotalMass;
        public float[] TorquePerUnit, PowerScale;
        public double[] PanelCentre, PanelNormal, PanelArea;

        public double[] BasePos, BaseRot, Q, Qd, Tau, LimitImplicit, BallRot, Pos, Rot, RotM, Spin, Vel, Sang, Slin,
            Cbias, RelVel, Water, WaterAcc, Fext;

        public float[] Signal, History, RunSum;
        public int[] Cursor, Filled;
        public long[] DriveLimited, DragLimited;
        public double[] Work, Signed, Dissipated, Passive;

        public int[] Op, NIn, Kind, Index, Channel;
        public float[] Freq, Phase, Amp, Bias, Const, Weight;
        public float[] S0, Mem0, Reserve, Damage;
        public int[] Parity0, Contact;
        public double[] Clock0;

        public double[] Ring, TraceTime;
        public long[] TraceStep, TraceFirstBadStep;
        public int[] TraceHeld, TraceCursor, TraceFirstBadLink, TraceResized;

        public double[] CCx, CCy, CCz, CR, CVx, CVy, CVz, PCx, PCy, PCz, PR, PVx, PVy, PVz;
        public int[] CActive, PActive;

        // The world.
        public bool HasCurrent, Accelerating, Sloped, HasBed, LimitDrive, LimitDrag, UseRestore, FloorRestores,
            CreatureContact, HasNutrients, HasReserve;
        public double Dt, Density, DragCoefficient, DragK, TissueExcess, NeutralVolume, RestoringFraction,
            RestoringDensity, TissueDensity, FluidAccel, AddedMass, Gravity, WorldDepth, Damping, SpinBudget;
        public double Omega, Zeta, MaxSep, MaxDv, TankRadius, AxisX, AxisZ;
        public double BedRadius, BedDepth, TiltX, TiltZ, Offset;
        public double[] BedAmp = Array.Empty<double>(), BedKx = Array.Empty<double>(),
            BedKz = Array.Empty<double>(), BedPhase = Array.Empty<double>();

        public double CurDepth, CurTankR, EddyWeight, Overturning, PerSecond, Squared;
        public float SigmaSloped, SigmaFlat, Speed;
        public double[] StreamAmp = new double[24], StreamRate = new double[24], CellAmp = new double[3];

        public double[] Stock;
        public int[] LowestLive;
        public float SWorldDepth, ChemHalf, EnergyScale, FlowScale, RateScale, ConstCE, DtF;
        public int Nx, Ny, Nz, LayerCount, RefugeLayers;
        public float CellMetres, FieldTankR, RefugeFraction, CellVolume;

        // The current's own instant tables, transcribed from CurrentField.EnsureInstant.
        private readonly CurrentField _current;
        private double[] _sPhase, _sRate, _sBreathPhase, _sBreathRate, _cPhase, _cRate, _cBreathPhase, _cBreathRate;
        private float _period;

        public StepHost(IReadOnlyList<Creature> bodies, SolverConfig s, GridField field,
                        float[] reserveSeconds, bool[][] contact, float[][] damage, int overCap, int entryCap)
        {
            N = bodies.Count;
            OverCap = overCap;
            EntryCap = entryCap;

            MaxNeurons = 1;
            MaxPanels = 1;
            foreach (Creature c in bodies)
            {
                if (c.Links > MaxLinks) throw new InvalidDataException($"body {c.Id} has {c.Links} links; the ceiling is {MaxLinks}");
                if (c.Dof > MaxDof) throw new InvalidDataException($"body {c.Id} has {c.Dof} dof; the ceiling is {MaxDof}");
                if (c.DriveSignal.Length > MaxDof) throw new InvalidDataException($"body {c.Id} drive signal {c.DriveSignal.Length}");
                if (c.Phenotype.PartCount != c.Links) throw new InvalidDataException($"body {c.Id}: {c.Phenotype.PartCount} parts, {c.Links} links");
                MaxNeurons = Math.Max(MaxNeurons, c.Brain.NeuronCount);
                MaxPanels = Math.Max(MaxPanels, c.PanelStart[c.Links]);
            }

            int L = MaxLinks * N, D = MaxDof * N, NN = MaxNeurons * N;

            Links = new int[N]; Dof = new int[N]; SigLen = new int[N]; Mask = new int[N]; Alive = new int[N];
            NeuronCount = new int[N]; TraceEnabled = new int[N];
            Parent = new int[L]; DofCount = new int[L]; DofStart = new int[L]; PanelStart = new int[L + N];
            NOff = new int[L]; NCnt = new int[L]; BParent = new int[L]; FirstChild = new int[L];
            BDofStart = new int[L]; BDofCount = new int[L];
            Mass = new double[L]; Lift = new double[L]; Volume = new double[L]; SmallI = new double[L]; Reach = new double[L];
            Inertia = new double[3 * L]; ChildAnchor = new double[3 * L]; ParentAnchor = new double[3 * L];
            JointFrame = new double[4 * L]; RestFrame = new double[4 * L];
            LimitLo = new double[D]; LimitHi = new double[D]; LimitStiff = new double[D]; LimitDamp = new double[D];
            Excess = new double[N]; TotalMass = new double[N];
            TorquePerUnit = new float[D]; PowerScale = new float[N];
            PanelCentre = new double[3 * MaxPanels * N]; PanelNormal = new double[3 * MaxPanels * N];
            PanelArea = new double[MaxPanels * N];

            BasePos = new double[3 * N]; BaseRot = new double[4 * N];
            Q = new double[D]; Qd = new double[D]; Tau = new double[D]; LimitImplicit = new double[D];
            BallRot = new double[4 * L]; Pos = new double[3 * L]; Rot = new double[4 * L]; RotM = new double[9 * L];
            Spin = new double[3 * L]; Vel = new double[3 * L]; Sang = new double[9 * L]; Slin = new double[9 * L];
            Cbias = new double[6 * L]; RelVel = new double[3 * L]; Water = new double[3 * L];
            WaterAcc = new double[3 * L]; Fext = new double[6 * L];

            Signal = new float[D]; History = new float[Window * D]; RunSum = new float[D];
            Cursor = new int[N]; Filled = new int[N];
            DriveLimited = new long[N]; DragLimited = new long[N];
            Work = new double[N]; Signed = new double[N]; Dissipated = new double[N]; Passive = new double[N];

            Op = new int[NN]; NIn = new int[NN]; Freq = new float[NN]; Phase = new float[NN];
            Amp = new float[NN]; Bias = new float[NN];
            Kind = new int[3 * NN]; Index = new int[3 * NN]; Channel = new int[3 * NN];
            Const = new float[3 * NN]; Weight = new float[3 * NN];
            S0 = new float[2 * NN]; Mem0 = new float[NN]; Clock0 = new double[N]; Parity0 = new int[N];
            Reserve = new float[N]; Damage = new float[L]; Contact = new int[L];

            Ring = new double[TraceFrames * L * TraceValues];
            TraceStep = new long[TraceFrames * N]; TraceTime = new double[TraceFrames * N];
            TraceResized = new int[TraceFrames * N];
            TraceFirstBadStep = new long[N]; TraceHeld = new int[N]; TraceCursor = new int[N];
            TraceFirstBadLink = new int[N];

            CCx = new double[N]; CCy = new double[N]; CCz = new double[N]; CR = new double[N];
            CVx = new double[N]; CVy = new double[N]; CVz = new double[N];
            PCx = new double[N]; PCy = new double[N]; PCz = new double[N]; PR = new double[N];
            PVx = new double[N]; PVy = new double[N]; PVz = new double[N];
            CActive = new int[N]; PActive = new int[N];

            double excessBase = s.TissueExcessDensity;

            for (int i = 0; i < N; i++)
            {
                Creature c = bodies[i];
                Links[i] = c.Links;
                Dof[i] = c.Dof;
                SigLen[i] = c.DriveSignal.Length;
                Mask[i] = c.Brain.SensorMask;
                Alive[i] = c.Alive ? 1 : 0;
                NeuronCount[i] = c.Brain.NeuronCount;
                TraceEnabled[i] = c.HasTrace ? 1 : 0;
                TotalMass[i] = c.TotalMass;
                PowerScale[i] = c.Drive.PowerScale;
                Reserve[i] = reserveSeconds[i];

                // Fluid.Apply's excess, per creature (Fluid.cs:33-38), as Flat.cs takes it.
                double excess = excessBase;
                if (s.NeutralBodyVolume > 0)
                {
                    excess *= BuoyancyModel.ExcessDensityFactor((float)c.TotalVolume, (float)s.NeutralBodyVolume);
                }
                Excess[i] = excess;

                BasePos[i] = c.BasePosition.X; BasePos[N + i] = c.BasePosition.Y; BasePos[2 * N + i] = c.BasePosition.Z;
                BaseRot[i] = c.BaseRotation.X; BaseRot[N + i] = c.BaseRotation.Y;
                BaseRot[2 * N + i] = c.BaseRotation.Z; BaseRot[3 * N + i] = c.BaseRotation.W;

                double[] reach = c.LinkReach;
                for (int l = 0; l < c.Links; l++)
                {
                    int at = l * N + i;
                    Parent[at] = c.Parent[l];
                    DofCount[at] = c.DofCount[l];
                    DofStart[at] = c.DofStart[l];
                    PanelStart[at] = c.PanelStart[l];
                    Mass[at] = c.Mass[l];
                    Lift[at] = c.Lift[l];
                    Volume[at] = c.Volume[l];
                    SmallI[at] = c.SmallestInertia[l];
                    Reach[at] = reach[l];
                    Contact[at] = contact[i][l] ? 1 : 0;
                    Damage[at] = damage[i][l];

                    Put(Inertia, c.InertiaLocal, 3, l, i);
                    Put(ChildAnchor, c.ChildAnchor, 3, l, i);
                    Put(ParentAnchor, c.ParentAnchor, 3, l, i);
                    Put(JointFrame, c.JointFrame, 4, l, i);
                    Put(RestFrame, c.RestFrame, 4, l, i);
                }
                PanelStart[c.Links * N + i] = c.PanelStart[c.Links];

                int panels = c.PanelStart[c.Links];
                for (int p = 0; p < panels; p++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        PanelCentre[(3 * p + k) * N + i] = c.PanelCentre[3 * p + k];
                        PanelNormal[(3 * p + k) * N + i] = c.PanelNormal[3 * p + k];
                    }
                    PanelArea[p * N + i] = c.PanelArea[p];
                }

                for (int d = 0; d < c.Dof; d++)
                {
                    int at = d * N + i;
                    LimitLo[at] = c.LimitLo[d];
                    LimitHi[at] = c.LimitHi[d];
                    LimitStiff[at] = c.LimitStiffness[d];
                    LimitDamp[at] = c.LimitDamping[d];
                }

                float[] tpu = (float[])Field(c.Drive, "_torquePerUnit");
                for (int d = 0; d < c.Dof; d++) TorquePerUnit[d * N + i] = tpu[d];

                // The brain's walk, as the third spike's BrainHost flattens it (Brain.cs:157-227).
                Phenotype plan = c.Phenotype;
                int cursor = 0, dofCursor = 0;
                for (int p = 0; p < plan.PartCount; p++) FirstChild[p * N + i] = -1;
                for (int p = 0; p < plan.PartCount; p++)
                {
                    PhenotypePart part = plan.Parts[p];
                    NeuronDef[] set = part.Neurons ?? Array.Empty<NeuronDef>();
                    NOff[p * N + i] = cursor;
                    NCnt[p * N + i] = set.Length;
                    BParent[p * N + i] = part.ParentIndex;
                    if (part.ParentIndex >= 0 && FirstChild[part.ParentIndex * N + i] < 0)
                    {
                        FirstChild[part.ParentIndex * N + i] = p;
                    }
                    int count = part.JointType.DofCount();
                    BDofCount[p * N + i] = count;
                    BDofStart[p * N + i] = count > 0 ? dofCursor : -1;
                    dofCursor += count;

                    for (int n = 0; n < set.Length; n++)
                    {
                        NeuronDef def = set[n];
                        int slot = (cursor + n) * N + i;
                        Op[slot] = (int)def.Op;
                        NeuronInput[] inputs = def.Inputs ?? Array.Empty<NeuronInput>();
                        if (inputs.Length > 3)
                        {
                            throw new InvalidDataException(
                                $"body {c.Id} part {p} neuron {n} has {inputs.Length} inputs; the kernel carries three");
                        }
                        NIn[slot] = inputs.Length;
                        Freq[slot] = def.Frequency; Phase[slot] = def.Phase;
                        Amp[slot] = def.Amplitude; Bias[slot] = def.Bias;
                        for (int k = 0; k < inputs.Length; k++)
                        {
                            int ks = (3 * (cursor + n) + k) * N + i;
                            Kind[ks] = (int)inputs[k].Kind;
                            Index[ks] = inputs[k].Index;
                            Channel[ks] = (int)inputs[k].Channel;
                            Const[ks] = inputs[k].Constant;
                            Weight[ks] = inputs[k].Weight;
                        }
                    }
                    cursor += set.Length;
                }
            }

            ReadState(bodies);

            // ---- the world's scalars
            Dt = s.StepSeconds;
            DtF = (float)s.StepSeconds;
            Density = s.Density;
            DragCoefficient = s.DragCoefficient;
            DragK = 0.5 * s.Density * s.DragCoefficient;
            TissueExcess = s.TissueExcessDensity;
            NeutralVolume = s.NeutralBodyVolume;
            RestoringFraction = s.SurfaceRestoringFraction;
            RestoringDensity = RestoringFraction * s.TissueDensity;
            UseRestore = RestoringFraction > 0;
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

            if (s.WaterHoldSeconds > 0)
            {
                throw new NotSupportedException("the kernel carries no water hold (D100); WaterHoldSeconds is " + s.WaterHoldSeconds);
            }

            CreatureContact = s.CreatureContact;
            Omega = s.ContactOmega;
            Zeta = s.ContactDampingRatio;
            MaxSep = s.MaxSeparationSpeed;
            MaxDv = s.MaxContactSpeedChange;
            TankRadius = s.TankRadiusMetres;
            AxisX = s.TankAxisX;
            AxisZ = s.TankAxisZ;

            HasBed = s.Bed != null;
            if (HasBed)
            {
                BedShape bed = s.Bed;
                BedKx = (double[])Field(bed, "_kx");
                BedKz = (double[])Field(bed, "_kz");
                BedAmp = (double[])Field(bed, "_amplitude");
                BedPhase = (double[])Field(bed, "_phase");
                TiltX = (double)Field(bed, "_tiltX");
                TiltZ = (double)Field(bed, "_tiltZ");
                Offset = (double)Field(bed, "_offset");
                BedRadius = (double)Field(bed, "_radius");
                BedDepth = (double)bed.DepthMetres;
            }

            // ---- the current
            CurrentField current = s.Current;
            _current = current;
            HasCurrent = current != null;
            Accelerating = s.FluidAccelerationCoefficient > 0 && current != null;
            if (HasCurrent)
            {
                if (current.Shape != WorldShape.Tank || current.Mode != CurrentMode.Transport ||
                    current.VentActive(s.PatchCount))
                {
                    throw new NotSupportedException(
                        $"the kernel carries the tank's streams only: shape {current.Shape}, mode {current.Mode}, " +
                        $"vent {current.VentActive(s.PatchCount)}");
                }

                // The two lazy builds, as the world's own pin takes them.
                current.PinInstant(0.0);
                current.UnpinInstant();

                Speed = (float)Field(current, "_speed");
                float scale = (float)Field(current, "_streamsScale");
                double bedScale = (double)Field(current, "_bedScale");
                _period = (float)Field(current, "_periodSeconds");
                CurDepth = (float)Field(current, "_depthMetres");
                CurTankR = (float)Field(current, "_tankRadiusMetres");
                EddyWeight = (double)Field(current, "_streamsEddyWeight");
                Overturning = (double)Field(current, "_streamsOverturning");
                if ((double)Field(current, "_envelopeOverride") != 0) throw new InvalidOperationException("envelope override set");

                StreamAmp = (double[])Field(current, "_streamAmplitude");
                StreamRate = (double[])Field(current, "_streamRate");
                CellAmp = (double[])Field(current, "_cellAmplitude");
                _sPhase = (double[])Field(current, "_streamPhase");
                _sRate = StreamRate;
                _sBreathPhase = (double[])Field(current, "_streamBreathPhase");
                _sBreathRate = (double[])Field(current, "_streamBreathRate");
                _cPhase = (double[])Field(current, "_cellPhase");
                _cRate = (double[])Field(current, "_cellRate");
                _cBreathPhase = (double[])Field(current, "_cellBreathPhase");
                _cBreathRate = (double[])Field(current, "_cellBreathRate");

                var curBed = (BedShape)Field(current, "_bed");
                Sloped = curBed != null;
                if (Sloped && !SameBed(curBed, s.Bed))
                {
                    throw new NotSupportedException("the current's bed and the contact's bed differ");
                }

                // StreamsAt: `_speed * _streamsScale` is a float product; the sloped multiplier
                // widens it, multiplies by the double _bedScale and narrows (CurrentField.cs:1899-1917).
                float flat = Speed * scale;
                SigmaFlat = flat;
                SigmaSloped = (float)(flat * bedScale);

                // StreamsAccelerationAt (CurrentField.cs:4342-4365).
                double clock = 2.0 * Math.PI / _period;
                double sigma = (double)Speed * scale * bedScale;
                PerSecond = sigma * clock;
                Squared = sigma * sigma;
            }

            // ---- the senses' world (BrainHost's reading)
            HasNutrients = field != null;
            HasReserve = true;
            SWorldDepth = (float)Math.Max(0.001, s.WorldDepthMetres);
            ChemHalf = (float)Math.Max(1e-6, s.ChemicalHalfScaleJoulesPerCubicMetre);
            EnergyScale = (float)Math.Max(1e-6, s.EnergyFullScaleSeconds);
            FlowScale = (float)Math.Max(1e-6, s.FlowFullScaleMetresPerSecond);
            RateScale = (float)Math.Max(1e-6, s.JointRateFullScale);
            ConstCE = s.ConstantChemicalAndEnergy;

            Nx = field.CellsX; Ny = field.CellsY; Nz = field.CellsZ;
            LayerCount = field.LayerCount;
            RefugeLayers = field.RefugeLayerCount;
            CellMetres = field.CellMetres;
            FieldTankR = field.TankRadiusMetres;
            RefugeFraction = field.RefugeEdibleFraction;
            CellVolume = field.CellVolume;

            Stock = new double[Nx * Ny * Nz];
            for (int iy = 0; iy < Ny; iy++)
            for (int ix = 0; ix < Nx; ix++)
            for (int iz = 0; iz < Nz; iz++)
            {
                Stock[(iy * Nx + ix) * Nz + iz] = field.JoulesAt(ix, iy, iz);
            }

            LowestLive = new int[Nx * Nz];
            for (int ix = 0; ix < Nx; ix++)
            for (int iz = 0; iz < Nz; iz++)
            {
                LowestLive[ix * Nz + iz] = field.LowestLiveLayer(ix, iz);
            }
        }

        private static bool SameBed(BedShape a, BedShape b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            foreach (string name in new[] { "_kx", "_kz", "_amplitude", "_phase" })
            {
                var x = (double[])Field(a, name);
                var y = (double[])Field(b, name);
                if (x.Length != y.Length) return false;
                for (int k = 0; k < x.Length; k++) if (BitConverter.DoubleToInt64Bits(x[k]) != BitConverter.DoubleToInt64Bits(y[k])) return false;
            }
            foreach (string name in new[] { "_tiltX", "_tiltZ", "_offset", "_radius" })
            {
                if ((double)Field(a, name) != (double)Field(b, name)) return false;
            }
            return a.DepthMetres == b.DepthMetres;
        }

        /// <summary>Every piece of state the kernel carries, read off the creatures as they stand.</summary>
        public void ReadState(IReadOnlyList<Creature> bodies)
        {
            for (int i = 0; i < N; i++)
            {
                Creature c = bodies[i];
                for (int l = 0; l < c.Links; l++)
                {
                    Put(BallRot, c.BallRotation, 4, l, i);
                    Put(Pos, c.Position, 3, l, i);
                    Put(Rot, c.Rotation, 4, l, i);
                    Put(RotM, c.RotationMatrix, 9, l, i);
                    Put(Spin, c.Spin, 3, l, i);
                    Put(Vel, c.Velocity, 3, l, i);
                    Put(Sang, c.Sang, 9, l, i);
                    Put(Slin, c.Slin, 9, l, i);
                    Put(Cbias, c.Cbias, 6, l, i);
                    Put(RelVel, c.RelativeVelocity, 3, l, i);
                    Put(Water, c.Water, 3, l, i);
                    Put(WaterAcc, c.WaterAcceleration, 3, l, i);
                    Put(Fext, c.Fext, 6, l, i);
                }
                for (int d = 0; d < c.Dof; d++)
                {
                    int at = d * N + i;
                    Q[at] = c.Q[d];
                    Qd[at] = c.Qd[d];
                    Tau[at] = c.Tau[d];
                    LimitImplicit[at] = c.LimitImplicit[d];
                }

                for (int d = 0; d < c.DriveSignal.Length; d++) Signal[d * N + i] = c.DriveSignal[d];
                var history = (float[])Field(c.Drive, "_history");
                var runSum = (float[])Field(c.Drive, "_runningSum");
                for (int d = 0; d < c.Dof; d++)
                {
                    RunSum[d * N + i] = runSum[d];
                    for (int w = 0; w < Window; w++) History[(d * Window + w) * N + i] = history[d * Window + w];
                }
                Cursor[i] = (int)Field(c.Drive, "_cursor");
                Filled[i] = (int)Field(c.Drive, "_filled");
                DriveLimited[i] = c.DriveImpulsesLimited;
                DragLimited[i] = c.DragImpulsesLimited;
                Work[i] = c.MechanicalWorkJoules;
                Signed[i] = c.SignedWorkJoules;
                Dissipated[i] = c.DissipatedJoules;
                Passive[i] = c.PassiveJointWorkJoules;

                var (clock, prev, cur, mem) = Gpu.Spike3.BrainHost.BrainState(c.Brain);
                Clock0[i] = clock;
                Parity0[i] = 0;
                for (int n = 0; n < prev.Length; n++)
                {
                    S0[n * N + i] = prev[n];
                    S0[(MaxNeurons + n) * N + i] = cur[n];
                    Mem0[n * N + i] = mem[n];
                }

                TraceFirstBadStep[i] = c.FirstNonFiniteStep;
                TraceFirstBadLink[i] = c.FirstNonFiniteLink;
                if (c.HasTrace)
                {
                    TraceHeld[i] = c.TraceHeld;
                    TraceCursor[i] = c.TraceCursor;
                    for (int f = 0; f < TraceFrames; f++)
                    {
                        TraceStep[f * N + i] = c.TraceStepAt(f);
                        TraceTime[f * N + i] = c.TraceTimeAt(f);
                        TraceResized[f * N + i] = c.TraceResizedAt(f) ? 1 : 0;
                        for (int l = 0; l < c.Links; l++)
                            for (int k = 0; k < TraceValues; k++)
                                Ring[((f * MaxLinks + l) * TraceValues + k) * N + i] = c.TraceValue(f, l, k);
                    }
                }

                CCx[i] = c.ContactCentre.X; CCy[i] = c.ContactCentre.Y; CCz[i] = c.ContactCentre.Z;
                CR[i] = c.ContactRadius;
                CVx[i] = c.ContactVelocity.X; CVy[i] = c.ContactVelocity.Y; CVz[i] = c.ContactVelocity.Z;
                CActive[i] = c.ContactActive ? 1 : 0;
                var (pc, pr, pv, pa) = Pending(c);
                PCx[i] = pc.X; PCy[i] = pc.Y; PCz[i] = pc.Z; PR[i] = pr;
                PVx[i] = pv.X; PVy[i] = pv.Y; PVz[i] = pv.Z; PActive[i] = pa ? 1 : 0;
            }
        }

        private void Put(double[] into, double[] from, int width, int l, int i)
        {
            for (int k = 0; k < width; k++) into[(width * l + k) * N + i] = from[width * l + k];
        }

        /// <summary>The pending contact sphere, which the creature keeps private (Creature.cs:245-248).</summary>
        public static (Vec3 centre, double radius, Vec3 velocity, bool active) Pending(Creature c) =>
            ((Vec3)Field(c, "_pendingCentre"), (double)Field(c, "_pendingRadius"),
             (Vec3)Field(c, "_pendingVelocity"), (bool)Field(c, "_pendingActive"));

        /// <summary>
        /// The instant the step at <paramref name="seconds"/> reads: CurrentField.EnsureInstant's
        /// arithmetic (CurrentField.cs:3732-3791) at the samplers' phase <c>2π·s/P</c> into slots
        /// 0-77 and at the analytic acceleration's <c>(2π/P)·s</c> into slots 78-185.
        /// </summary>
        public void Instant(double seconds, double[] into, int at)
        {
            if (!HasCurrent) { Array.Clear(into, at, Inst); return; }

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
        /// The transcription held against the field's own slots: pin the field at a clock, read
        /// the slot for each phase by reflection, and compare every number to the bit.
        /// </summary>
        public (int values, int mismatches) VerifyInstant(double seconds)
        {
            if (!HasCurrent) return (0, 0);
            var mine = new double[Inst];
            Instant(seconds, mine, 0);

            _current.PinInstant(seconds);
            try
            {
                var slots = (Array)Field(_current, "_slots");
                double a = 2.0 * Math.PI * seconds / _period;
                double b = 2.0 * Math.PI / _period * seconds;
                int values = 0, bad = 0;
                foreach (object slot in slots)
                {
                    double held = (double)FieldOf(slot, "At");
                    bool isA = held == a, isB = held == b;
                    if (!isA && !isB) continue;
                    var cos = (double[])FieldOf(slot, "Cos");
                    var sin = (double[])FieldOf(slot, "Sin");
                    var env = (double[])FieldOf(slot, "Envelope");
                    var envRate = (double[])FieldOf(slot, "EnvelopeRate");
                    var cenv = (double[])FieldOf(slot, "CellEnvelope");
                    var cenvRate = (double[])FieldOf(slot, "CellEnvelopeRate");
                    var cell = (double[])FieldOf(slot, "Cell");
                    var cellRate = (double[])FieldOf(slot, "CellRate");

                    void Cmp(double x, double y) { values++; if (BitConverter.DoubleToInt64Bits(x) != BitConverter.DoubleToInt64Bits(y)) bad++; }

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

        public static object Field(object o, string name)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return f.GetValue(o);
            }
            throw new MissingFieldException(o.GetType().Name, name);
        }

        private static object FieldOf(object o, string name) => Field(o, name);
    }
}

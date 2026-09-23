using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Evosim.Dynamics;

namespace Gpu.Spike3
{
    internal interface IBrainRunner : IDisposable
    {
        double CompileMs { get; }
        string Precision { get; }
        long MetabolicBytes { get; }
        void ResetState();
        void Launch(int steps);
        void Spin(int iterations);
        void Synchronize();
        void UploadMetabolic();
        BrainResult Read();
        ulong Digest();
    }

    internal sealed class BrainResult
    {
        public float[] S, Mem, Drive, Depth, Up, Chem, Flow, Angle, Rate, Energy;
        public int[] Parity;
        public double[] Clock;
    }

    /// <summary>
    /// Everything the senses and the brain of N bodies read, struct-of-arrays and strided by body
    /// (<c>slot * N + body</c>) as <c>Flat.cs</c> strides, read off the real <see cref="Creature"/>
    /// objects the CPU reference steps, so the kernel and the reference start from one set of
    /// numbers.
    /// </summary>
    internal sealed class BrainHost
    {
        public int N, MaxLinks, MaxDof, MaxNeurons;
        public int[] Links, Dof, Mask, NeuronCount;
        public int[] LDofCount, LDofStart, NOff, NCnt, Parent, FirstChild, BDofStart, BDofCount;
        public double[] Pos, Rot, RelVel, Q, Qd, LimitHi;
        public float[] Reserve, Damage;
        public int[] Contact;
        public int[] Op, NIn, Kind, Index, Channel;
        public float[] Freq, Phase, Amp, Bias, Const, Weight;
        public float[] S0, Mem0;
        public double[] Clock0;

        public double[] Stock;
        public int[] LowestLive;
        public bool HasNutrients, HasReserve;
        public float WorldDepth, ChemHalf, EnergyScale, FlowScale, RateScale, ConstCE, Dt;
        public int Nx, Ny, Nz, LayerCount, RefugeLayers;
        public float CellMetres, TankR, RefugeFraction, CellVolume;

        public BrainHost(IReadOnlyList<Creature> bodies, SolverConfig s, GridField field,
                         float[] reserveSeconds, bool[][] contact, float[][] damage)
        {
            N = bodies.Count;

            MaxLinks = 1; MaxDof = 1; MaxNeurons = 1;
            foreach (Creature c in bodies)
            {
                MaxLinks = Math.Max(MaxLinks, c.Links);
                MaxDof = Math.Max(MaxDof, c.Dof);
                MaxNeurons = Math.Max(MaxNeurons, c.Brain.NeuronCount);
            }

            Links = new int[N]; Dof = new int[N]; Mask = new int[N]; NeuronCount = new int[N];
            int L = MaxLinks * N, D = MaxDof * N, NN = MaxNeurons * N;
            LDofCount = new int[L]; LDofStart = new int[L];
            NOff = new int[L]; NCnt = new int[L]; Parent = new int[L]; FirstChild = new int[L];
            BDofStart = new int[L]; BDofCount = new int[L];
            Pos = new double[3 * L]; Rot = new double[9 * L]; RelVel = new double[3 * L];
            Q = new double[D]; Qd = new double[D]; LimitHi = new double[D];
            Reserve = new float[N]; Damage = new float[L]; Contact = new int[L];
            Op = new int[NN]; NIn = new int[NN]; Freq = new float[NN]; Phase = new float[NN];
            Amp = new float[NN]; Bias = new float[NN];
            Kind = new int[3 * NN]; Index = new int[3 * NN]; Channel = new int[3 * NN];
            Const = new float[3 * NN]; Weight = new float[3 * NN];
            S0 = new float[2 * NN]; Mem0 = new float[NN]; Clock0 = new double[N];

            for (int i = 0; i < N; i++)
            {
                Creature c = bodies[i];
                Phenotype plan = c.Phenotype;
                Links[i] = c.Links;
                Dof[i] = c.Dof;
                Mask[i] = c.Brain.SensorMask;
                NeuronCount[i] = c.Brain.NeuronCount;
                Reserve[i] = reserveSeconds[i];

                for (int l = 0; l < c.Links; l++)
                {
                    int at = l * N + i;
                    LDofCount[at] = c.DofCount[l];
                    LDofStart[at] = c.DofStart[l];
                    for (int k = 0; k < 3; k++)
                    {
                        Pos[(3 * l + k) * N + i] = c.Position[3 * l + k];
                        RelVel[(3 * l + k) * N + i] = c.RelativeVelocity[3 * l + k];
                    }
                    for (int k = 0; k < 9; k++) Rot[(9 * l + k) * N + i] = c.RotationMatrix[9 * l + k];
                    Contact[at] = contact[i][l] ? 1 : 0;
                    Damage[at] = damage[i][l];
                }

                for (int d = 0; d < c.Dof; d++)
                {
                    Q[d * N + i] = c.Q[d];
                    Qd[d * N + i] = c.Qd[d];
                    LimitHi[d * N + i] = c.LimitHi[d];
                }

                // Brain.For's own walk (Brain.cs:157-227): offsets, parents, first children, the
                // brain's DoF numbering, and the neurons of each part in order.
                int cursor = 0, dofCursor = 0;
                for (int p = 0; p < plan.PartCount; p++) FirstChild[p * N + i] = -1;
                for (int p = 0; p < plan.PartCount; p++)
                {
                    PhenotypePart part = plan.Parts[p];
                    NeuronDef[] set = part.Neurons ?? Array.Empty<NeuronDef>();
                    NOff[p * N + i] = cursor;
                    NCnt[p * N + i] = set.Length;
                    Parent[p * N + i] = part.ParentIndex;
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

                // The recurrent state as the brain holds it: the clock, both buffers, the memory.
                var (clock, prev, cur, mem) = BrainState(c.Brain);
                Clock0[i] = clock;
                for (int n = 0; n < prev.Length; n++)
                {
                    S0[n * N + i] = prev[n];
                    S0[(MaxNeurons + n) * N + i] = cur[n];
                    Mem0[n * N + i] = mem[n];
                }
            }

            // The world: the snow field's stock, its mask, the scales.
            HasNutrients = field != null;
            HasReserve = true;
            WorldDepth = (float)Math.Max(0.001, s.WorldDepthMetres);
            ChemHalf = (float)Math.Max(1e-6, s.ChemicalHalfScaleJoulesPerCubicMetre);
            EnergyScale = (float)Math.Max(1e-6, s.EnergyFullScaleSeconds);
            FlowScale = (float)Math.Max(1e-6, s.FlowFullScaleMetresPerSecond);
            RateScale = (float)Math.Max(1e-6, s.JointRateFullScale);
            ConstCE = s.ConstantChemicalAndEnergy;
            Dt = (float)s.StepSeconds;

            Nx = field.CellsX; Ny = field.CellsY; Nz = field.CellsZ;
            LayerCount = field.LayerCount;
            RefugeLayers = field.RefugeLayerCount;
            CellMetres = field.CellMetres;
            TankR = field.TankRadiusMetres;
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

        /// <summary>A brain's clock and buffers, read through its own <c>WriteState</c>.</summary>
        public static (double clock, float[] prev, float[] cur, float[] mem) BrainState(Brain brain)
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) brain.WriteState(w);
            ms.Position = 0;
            using var r = new BinaryReader(ms);
            r.ReadBytes(4);                         // "BRAN"
            double clock = r.ReadDouble();
            float[] prev = StateIo.ReadFloats(r);
            float[] cur = StateIo.ReadFloats(r);
            float[] mem = StateIo.ReadFloats(r);
            return (clock, prev, cur, mem);
        }

        /// <summary>Sets a brain's clock through its own <c>ReadState</c>, the buffers untouched.</summary>
        public static void SetClock(Brain brain, double seconds)
        {
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) brain.WriteState(w);
            byte[] bytes = ms.ToArray();
            BitConverter.GetBytes(seconds).CopyTo(bytes, 4);
            using var r = new BinaryReader(new MemoryStream(bytes));
            brain.ReadState(r);
        }
    }

    /// <summary>A body's reserve, fixed: what the energy sense reads between metabolic steps.</summary>
    internal sealed class FixedReserve : IReserveSource
    {
        public FixedReserve(float seconds) { SecondsOfReserve = seconds; }
        public float SecondsOfReserve { get; }
    }
}

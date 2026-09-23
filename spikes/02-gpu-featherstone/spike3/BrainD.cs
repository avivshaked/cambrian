// ---------------------------------------------------------------------------------------------
// GENERATED from spike3/brain.template by spike3/gen3.py. Do not edit; edit the template and
// regenerate.
//
// The senses sub-phase and the brain evaluation, transcribed for ILGPU: one thread a body,
// struct-of-arrays strided by body (slot * N + body) as Flat.cs strides. Sources, line for line
// (commit d153224):
//   CreatureSenses.Sample / Read / Dof / Squash -> src/Evosim.Dynamics/CreatureSenses.cs:155-336
//   GridField.EdibleDensityAt, CellAt (tank), TankCellAt, InColumn, Column, Edible
//                                               -> src/Evosim.Core/Environment/GridField.cs:
//                                                  801-910, 1025-1026, 1500
//   Brain.Step / Evaluate / Apply / Fold / Read / FromGroup / Saw / Clamp
//                                               -> src/Evosim.Core/Brain/Brain.cs:238-574
//   Math.Min / Math.Max (float)                  -> .NET 8's own definitions, written out so the
//                                                  card cannot substitute its min/max instructions
//
// The neuron state is three floats a neuron: two buffers and a memory. The two buffers are
// one array of 2 x MaxNeurons slots a body, and which half is "previous" is one int a body
// (Parity): the swap is a flip of that int, not a copy. The clock is a double a body.
//
// The double build (Gpu.Spike3.Dbl) is the CPU's arithmetic exactly: the body's state in
// double, the senses and neurons in float, System.Math through double where the CPU calls it.
// The single build (Gpu.Spike3.Sgl) holds the body's state in float and calls XMath in float;
// the clock stays in double and the oscillator's argument is built in double, as the spec asks.
// ---------------------------------------------------------------------------------------------

using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Algorithms;

namespace Gpu.Spike3.Dbl
{
    using Real = System.Double;

    /// <summary>Per body and per link: the counts, the masks and the two topologies.</summary>
    public struct BTopo
    {
        public ArrayView<int> Links, Dof, Mask;
        public ArrayView<int> LDofCount, LDofStart;                          // the body's, per link
        public ArrayView<int> NOff, NCnt, Parent, FirstChild, BDofStart, BDofCount;   // the brain's, per part
    }

    /// <summary>The body's own state the senses read, and the two hand-overs.</summary>
    public struct BBody
    {
        public ArrayView<Real> Pos, Rot, RelVel, Q, Qd, LimitHi;
        public ArrayView<float> Reserve, Damage;
        public ArrayView<int> Contact;
    }

    /// <summary>Neuron definitions: per neuron slot, and three input slots a neuron.</summary>
    public struct BNeur
    {
        public ArrayView<int> Op, NIn, Kind, Index, Channel;
        public ArrayView<float> Freq, Phase, Amp, Bias, Const, Weight;
    }

    /// <summary>What persists from step to step: the neurons, the clock, the sensed values, the drive.</summary>
    public struct BState
    {
        public ArrayView<float> S, Mem, Drive, Depth, Up, Chem, Flow, Angle, Rate, Energy;
        public ArrayView<int> Parity;
        public ArrayView<double> Clock;
    }

    public struct BField
    {
        public ArrayView<Real> Stock;
        public ArrayView<int> LowestLive;
    }

    public struct BCfg
    {
        public int N, Steps, MaxN, HasNutrients, HasReserve;
        public float WorldDepth, ChemHalf, EnergyScale, FlowScale, RateScale, ConstCE, Dt;
        public int Nx, Ny, Nz, LayerCount, RefugeLayers, LayerStride;
        public float CellMetres, TankR, RefugeFraction, CellVolume;
    }

    public static class BrainKernels
    {
        private const int Depth = 9, Up = 3, JointAngle = 0, JointRate = 1, Chemical = 6, Energy = 7,
            Flow = 8, Contact = 2, Damage = 5;

        public static void Step(Index1D index, BTopo t, BBody b, BNeur nr, BState st, BField f, BCfg cfg)
        {
            int i = index.X;
            if (i >= cfg.N) return;

            int mask = t.Mask[i];
            bool readsDepth = (mask & (1 << Depth)) != 0;
            bool readsUp = (mask & (1 << Up)) != 0;
            bool readsJoint = (mask & (1 << JointAngle)) != 0 || (mask & (1 << JointRate)) != 0;
            bool readsFlow = (mask & (1 << Flow)) != 0;
            bool readsChemical = (mask & (1 << Chemical)) != 0;
            bool readsEnergy = (mask & (1 << Energy)) != 0;

            for (int step = 0; step < cfg.Steps; step++)
            {
                Sample(i, t, b, st, f, cfg, readsDepth, readsUp, readsJoint, readsFlow, readsChemical, readsEnergy);
                BrainStep(i, t, b, nr, st, cfg);
            }
        }

        // ------------------------------------------------------------------ CreatureSenses.Sample

        private static void Sample(
            int i, BTopo t, BBody b, BState st, BField f, BCfg cfg,
            bool readsDepth, bool readsUp, bool readsJoint, bool readsFlow, bool readsChemical, bool readsEnergy)
        {
            int N = cfg.N;

            if (readsEnergy && cfg.HasReserve != 0) st.Energy[i] = Squash(b.Reserve[i], cfg.EnergyScale);

            bool smells = readsChemical && cfg.HasNutrients != 0;

            if (!readsDepth && !readsUp && !readsJoint && !readsFlow && !smells) return;

            float flowScale = cfg.FlowScale;
            float rateScale = cfg.RateScale;
            int links = t.Links[i];

            for (int bl = 0; bl < links; bl++)
            {
                Real px = b.Pos[(3 * bl) * N + i];
                Real py = b.Pos[(3 * bl + 1) * N + i];
                Real pz = b.Pos[(3 * bl + 2) * N + i];

                Real finite = px + py + pz;
                if (finite != finite || finite == Real.PositiveInfinity || finite == Real.NegativeInfinity)
                {
                    st.Depth[bl * N + i] = 0f;
                    st.Up[bl * N + i] = 0f;
                    st.Chem[bl * N + i] = 0f;

                    if (readsFlow)
                    {
                        st.Flow[(3 * bl) * N + i] = 0f;
                        st.Flow[(3 * bl + 1) * N + i] = 0f;
                        st.Flow[(3 * bl + 2) * N + i] = 0f;
                    }

                    continue;
                }

                if (readsDepth)
                {
                    Real d = -py / (Real)cfg.WorldDepth;
                    st.Depth[bl * N + i] = (float)(d < 0 ? 0 : d > 1 ? 1 : d);
                }

                if (readsUp)
                {
                    st.Up[bl * N + i] = (float)b.Rot[(9 * bl + 4) * N + i];
                }

                if (smells)
                {
                    float density = EdibleDensityAt((float)px, (float)py, (float)pz, f, cfg);
                    st.Chem[bl * N + i] = density > 0f ? density / (density + cfg.ChemHalf) : 0f;
                }

                if (readsFlow)
                {
                    // Mat3.Read then TransposedTimes: column k of the row-major 3x3 against v.
                    Real m00 = b.Rot[(9 * bl) * N + i], m01 = b.Rot[(9 * bl + 1) * N + i], m02 = b.Rot[(9 * bl + 2) * N + i];
                    Real m10 = b.Rot[(9 * bl + 3) * N + i], m11 = b.Rot[(9 * bl + 4) * N + i], m12 = b.Rot[(9 * bl + 5) * N + i];
                    Real m20 = b.Rot[(9 * bl + 6) * N + i], m21 = b.Rot[(9 * bl + 7) * N + i], m22 = b.Rot[(9 * bl + 8) * N + i];
                    Real vx = b.RelVel[(3 * bl) * N + i], vy = b.RelVel[(3 * bl + 1) * N + i], vz = b.RelVel[(3 * bl + 2) * N + i];

                    Real lx = m00 * vx + m10 * vy + m20 * vz;
                    Real ly = m01 * vx + m11 * vy + m21 * vz;
                    Real lz = m02 * vx + m12 * vy + m22 * vz;

                    st.Flow[(3 * bl) * N + i] = Clamp((float)(lx / flowScale));
                    st.Flow[(3 * bl + 1) * N + i] = Clamp((float)(ly / flowScale));
                    st.Flow[(3 * bl + 2) * N + i] = Clamp((float)(lz / flowScale));
                }

                if (!readsJoint) continue;

                int n = t.LDofCount[bl * N + i];
                int offset = t.LDofStart[bl * N + i];
                if (n == 0 || offset < 0) continue;

                for (int d = 0; d < n; d++)
                {
                    int at = (offset + d) * N + i;
                    Real limit = AbsR(b.LimitHi[at]);

                    st.Angle[at] = limit > (Real)1e-6
                        ? Clamp((float)(b.Q[at] / limit))
                        : 0f;

                    st.Rate[at] = Clamp((float)(b.Qd[at] / rateScale));
                }
            }
        }

        /// <summary>CreatureSenses.Squash: seconds of reserve onto (-1, 1].</summary>
        private static float Squash(float seconds, float scale)
        {
            if (seconds != seconds) return 0f;
            if (seconds == float.PositiveInfinity) return 1f;
            if (seconds == float.NegativeInfinity) return -1f;

            return TanhF(seconds / scale);
        }

        // ------------------------------------------------------------------ GridField, the tank's lookup

        private static float EdibleDensityAt(float x, float y, float z, BField f, BCfg cfg)
        {
            int cell = TankCellAt(x, y, z, f, cfg);
            Real stock = f.Stock[cell];
            int layer = cell / cfg.LayerStride;
            Real edible = layer >= cfg.LayerCount - cfg.RefugeLayers ? stock * cfg.RefugeFraction : stock;
            return (float)(edible / cfg.CellVolume);
        }

        private static int TankCellAt(float x, float y, float z, BField f, BCfg cfg)
        {
            int iy = y >= 0f ? 0 : (int)(-y / cfg.CellMetres);
            if (iy >= cfg.Ny) iy = cfg.Ny - 1;
            if (iy < 0) iy = 0;

            Real dx = x - cfg.TankR;
            Real dz = z - cfg.TankR;
            Real r = SqrtR(dx * dx + dz * dz);

            if (r > 0)
            {
                for (Real reach = r; reach > 0; reach -= (Real)0.5 * cfg.CellMetres)
                {
                    int cell = InColumn(
                        Column((float)(cfg.TankR + dx * reach / r), cfg),
                        Column((float)(cfg.TankR + dz * reach / r), cfg), iy, f, cfg);

                    if (cell >= 0) return cell;
                }
            }

            int axis = InColumn(Column(cfg.TankR, cfg), Column(cfg.TankR, cfg), iy, f, cfg);
            if (axis >= 0) return axis;

            int columns = cfg.Nx * cfg.Nz;
            for (int k = 0; k < columns; k++)
            {
                int cell = InColumn(k / cfg.Nz, k % cfg.Nz, iy, f, cfg);
                if (cell >= 0) return cell;
            }

            return (iy * cfg.Nx + Column(cfg.TankR, cfg)) * cfg.Nz + Column(cfg.TankR, cfg);
        }

        private static int InColumn(int ix, int iz, int iy, BField f, BCfg cfg)
        {
            int lowest = f.LowestLive[ix * cfg.Nz + iz];
            if (lowest < 0) return -1;

            return ((iy < lowest ? iy : lowest) * cfg.Nx + ix) * cfg.Nz + iz;
        }

        private static int Column(float v, BCfg cfg)
        {
            int i = (int)(v / cfg.CellMetres);
            if (i >= cfg.Nx) return cfg.Nx - 1;
            return i < 0 ? 0 : i;
        }

        // ------------------------------------------------------------------ Brain.Step

        private static void BrainStep(int i, BTopo t, BBody b, BNeur nr, BState st, BCfg cfg)
        {
            int N = cfg.N;
            int links = t.Links[i];

            double clock = st.Clock[i] + cfg.Dt;
            st.Clock[i] = clock;
            float time = (float)clock;

            int parity = st.Parity[i];
            int prev = parity * cfg.MaxN;
            int cur = (1 - parity) * cfg.MaxN;

            for (int group = 0; group < links; group++)
            {
                int at = t.NOff[group * N + i];
                int count = t.NCnt[group * N + i];

                for (int n = 0; n < count; n++)
                {
                    st.S[(cur + at + n) * N + i] = Evaluate(i, at + n, group, time, prev, t, b, nr, st, cfg);
                }
            }

            // The swap.
            parity = 1 - parity;
            st.Parity[i] = parity;
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
                    st.Drive[(start + d) * N + i] = d < own ? Clamp(st.S[(prev + at + d) * N + i]) : 0f;
                }
            }
        }

        private static float Evaluate(
            int i, int self, int part, float time, int prev, BTopo t, BBody b, BNeur nr, BState st, BCfg cfg)
        {
            int slot = self * cfg.N + i;
            int op = nr.Op[slot];
            float value;

            if (op == 22)
            {
                // OscillateWave: (float)Math.Sin(2.0 * Math.PI * Frequency * time + Phase).
                value = OscSin(2.0 * Math.PI * nr.Freq[slot] * time + nr.Phase[slot]);
            }
            else if (op == 23)
            {
                // OscillateSaw: Saw(Frequency * time + Phase / (2f * (float)Math.PI)).
                value = Saw(nr.Freq[slot] * time + nr.Phase[slot] / (2f * (float)Math.PI));
            }
            else
            {
                value = Apply(i, self, part, prev, t, b, nr, st, cfg);
            }

            value = value * nr.Amp[slot] + nr.Bias[slot];

            return value != value || value == float.PositiveInfinity || value == float.NegativeInfinity ? 0f : value;
        }

        private static float Apply(int i, int self, int part, int prev, BTopo t, BBody b, BNeur nr, BState st, BCfg cfg)
        {
            int slot = self * cfg.N + i;
            int nIn = nr.NIn[slot];
            float dt = cfg.Dt;

            float a = nIn > 0 ? Read(i, self, 0, part, prev, t, b, nr, st, cfg) : 0f;
            float bb = nIn > 1 ? Read(i, self, 1, part, prev, t, b, nr, st, cfg) : 0f;
            float c = nIn > 2 ? Read(i, self, 2, part, prev, t, b, nr, st, cfg) : 0f;

            switch (nr.Op[slot])
            {
                case 0:     // Sum
                {
                    float acc = 0f;
                    for (int k = 0; k < nIn; k++) acc = acc + Read(i, self, k, part, prev, t, b, nr, st, cfg);
                    return acc;
                }
                case 1:     // Product
                {
                    if (nIn == 0) return 0f;
                    float acc = 1f;
                    for (int k = 0; k < nIn; k++) acc = acc * Read(i, self, k, part, prev, t, b, nr, st, cfg);
                    return acc;
                }
                case 4:     // Min
                {
                    if (nIn == 0) return 0f;
                    float acc = float.MaxValue;
                    for (int k = 0; k < nIn; k++) acc = MinF(acc, Read(i, self, k, part, prev, t, b, nr, st, cfg));
                    return acc;
                }
                case 5:     // Max
                {
                    if (nIn == 0) return 0f;
                    float acc = float.MinValue;
                    for (int k = 0; k < nIn; k++) acc = MaxF(acc, Read(i, self, k, part, prev, t, b, nr, st, cfg));
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
                    for (int k = 0; k < nIn; k++) acc = acc + Read(i, self, k, part, prev, t, b, nr, st, cfg);
                    return acc > 0f ? 1f : -1f;
                }

                case 40:    // Integrate
                {
                    float m = Clamp(st.Mem[slot] + a * dt, -100f, 100f);
                    st.Mem[slot] = m;
                    return m;
                }

                case 41:    // Differentiate
                {
                    float rate = dt > 0f ? (a - st.Mem[slot]) / dt : 0f;
                    st.Mem[slot] = a;
                    return rate;
                }

                case 42:    // Smooth
                {
                    float m = st.Mem[slot] + (a - st.Mem[slot]) * Clamp01(bb);
                    st.Mem[slot] = m;
                    return m;
                }

                case 43:    // Memory
                {
                    float held = st.Mem[slot];
                    st.Mem[slot] = a;
                    return held;
                }

                default: return a;
            }
        }

        /// <summary>Brain.Read: one input against the previous step, times its weight.</summary>
        private static float Read(int i, int self, int k, int part, int prev, BTopo t, BBody b, BNeur nr, BState st, BCfg cfg)
        {
            int N = cfg.N;
            int ks = (3 * self + k) * N + i;
            float w = nr.Weight[ks];

            switch (nr.Kind[ks])
            {
                case 0: return nr.Const[ks] * w;
                case 1: return SensorRead(i, part, nr.Channel[ks], nr.Index[ks], t, b, st, cfg) * w;
                case 2: return FromGroup(i, part, nr.Index[ks], prev, t, st, cfg) * w;
                case 3:
                {
                    int owner = t.Parent[part * N + i];
                    return owner < 0 ? 0f : FromGroup(i, owner, nr.Index[ks], prev, t, st, cfg) * w;
                }
                case 4:
                {
                    int owner = t.FirstChild[part * N + i];
                    return owner < 0 ? 0f : FromGroup(i, owner, nr.Index[ks], prev, t, st, cfg) * w;
                }
                default: return 0f;
            }
        }

        private static float FromGroup(int i, int group, int index, int prev, BTopo t, BState st, BCfg cfg)
        {
            int N = cfg.N;
            int count = t.NCnt[group * N + i];
            if (count == 0) return 0f;

            int at = index % count;
            if (at < 0) at += count;

            return st.S[(prev + t.NOff[group * N + i] + at) * N + i];
        }

        /// <summary>CreatureSenses.Read, with the body's hand-overs present and its world given.</summary>
        private static float SensorRead(int i, int part, int channel, int index, BTopo t, BBody b, BState st, BCfg cfg)
        {
            int N = cfg.N;
            if (part < 0 || part >= t.Links[i]) return 0f;

            switch (channel)
            {
                case Depth: return st.Depth[part * N + i];
                case Up: return st.Up[part * N + i];
                case JointAngle: return DofRead(i, part, index, 0, t, st, cfg);
                case JointRate: return DofRead(i, part, index, 1, t, st, cfg);
                case Chemical: return cfg.HasNutrients != 0 ? st.Chem[part * N + i] : cfg.ConstCE;
                case Energy: return cfg.HasReserve != 0 ? st.Energy[i] : cfg.ConstCE;
                case Flow: return index >= 0 && index < 3 ? st.Flow[(3 * part + index) * N + i] : 0f;
                case Contact: return b.Contact[part * N + i] != 0 ? 1f : 0f;
                case Damage: return Clamp(b.Damage[part * N + i]);
                default: return 0f;
            }
        }

        private static float DofRead(int i, int part, int index, int which, BTopo t, BState st, BCfg cfg)
        {
            int N = cfg.N;
            int n = t.LDofCount[part * N + i];
            int offset = t.LDofStart[part * N + i];

            if (offset < 0 || index < 0 || index >= n) return 0f;

            int at = offset + index;
            int length = t.Dof[i] > 0 ? t.Dof[i] : 1;
            if (at >= length) return 0f;

            return which == 0 ? st.Angle[at * N + i] : st.Rate[at * N + i];
        }

        // ------------------------------------------------------------------ small things

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

        /// <summary>.NET 8's Math.Min(float, float): NaN propagates, -0 is below +0.</summary>
        private static float MinF(float x, float y)
        {
            if (x != y)
            {
                if (!(x != x)) return x < y ? x : y;
                return x;
            }
            return IsNegative(x) ? x : y;
        }

        /// <summary>.NET 8's Math.Max(float, float): NaN propagates, +0 is above -0.</summary>
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


        /// <summary>One thread busy for a while; see the contact kernel's Spin.</summary>
        public static void Spin(Index1D index, ArrayView<int> sink, int iterations)
        {
            if (index.X != 0) return;
            uint x = (uint)sink[0];
            for (int k = 0; k < iterations; k++) x = x * 1664525u + 1013904223u;
            sink[1] = (int)x;
        }
    }

    /// <summary>Device buffers and the launch, for one precision.</summary>
    internal sealed class BrainRunner : Gpu.Spike3.IBrainRunner
    {
        private readonly Accelerator _acc;
        private readonly Gpu.Spike3.BrainHost _h;
        private readonly int _n;

        private readonly MemoryBuffer1D<int, Stride1D.Dense> _links, _dof, _mask, _ldofCount, _ldofStart,
            _noff, _ncnt, _parent, _firstChild, _bdofStart, _bdofCount;
        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _pos, _rot, _relVel, _q, _qd, _limitHi;
        private readonly MemoryBuffer1D<float, Stride1D.Dense> _reserve, _damage;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _contact;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _op, _nin, _kind, _index, _channel;
        private readonly MemoryBuffer1D<float, Stride1D.Dense> _freq, _phase, _amp, _bias, _const, _weight;
        private readonly MemoryBuffer1D<float, Stride1D.Dense> _s, _mem, _drive, _depth, _up, _chem, _flow,
            _angle, _rate, _energy;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _parity;
        private readonly MemoryBuffer1D<double, Stride1D.Dense> _clock;
        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _stock;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _lowest, _sink;

        private readonly Action<Index1D, BTopo, BBody, BNeur, BState, BField, BCfg> _kernel;
        private readonly Action<Index1D, ArrayView<int>, int> _spin;

        private readonly Real[] _stockHost;
        private BCfg _cfg;

        public double CompileMs { get; }
        public string Precision => "double";

        public BrainRunner(Accelerator accelerator, Gpu.Spike3.BrainHost h, int groupSize)
        {
            _acc = accelerator;
            _h = h;
            _n = h.N;

            _links = I(h.Links); _dof = I(h.Dof); _mask = I(h.Mask);
            _ldofCount = I(h.LDofCount); _ldofStart = I(h.LDofStart);
            _noff = I(h.NOff); _ncnt = I(h.NCnt); _parent = I(h.Parent); _firstChild = I(h.FirstChild);
            _bdofStart = I(h.BDofStart); _bdofCount = I(h.BDofCount);

            _pos = R(h.Pos); _rot = R(h.Rot); _relVel = R(h.RelVel); _q = R(h.Q); _qd = R(h.Qd);
            _limitHi = R(h.LimitHi);
            _reserve = F(h.Reserve); _damage = F(h.Damage); _contact = I(h.Contact);

            _op = I(h.Op); _nin = I(h.NIn); _kind = I(h.Kind); _index = I(h.Index); _channel = I(h.Channel);
            _freq = F(h.Freq); _phase = F(h.Phase); _amp = F(h.Amp); _bias = F(h.Bias);
            _const = F(h.Const); _weight = F(h.Weight);

            _s = _acc.Allocate1D<float>(h.S0.Length);
            _mem = _acc.Allocate1D<float>(h.Mem0.Length);
            _drive = _acc.Allocate1D<float>((long)h.MaxDof * _n);
            _depth = _acc.Allocate1D<float>((long)h.MaxLinks * _n);
            _up = _acc.Allocate1D<float>((long)h.MaxLinks * _n);
            _chem = _acc.Allocate1D<float>((long)h.MaxLinks * _n);
            _flow = _acc.Allocate1D<float>((long)3 * h.MaxLinks * _n);
            _angle = _acc.Allocate1D<float>((long)h.MaxDof * _n);
            _rate = _acc.Allocate1D<float>((long)h.MaxDof * _n);
            _energy = _acc.Allocate1D<float>(_n);
            _parity = _acc.Allocate1D<int>(_n);
            _clock = _acc.Allocate1D<double>(_n);

            _stockHost = Cv(h.Stock);
            _stock = _acc.Allocate1D(_stockHost);
            _lowest = I(h.LowestLive);
            _sink = _acc.Allocate1D<int>(2);
            _sink.View.MemSetToZero();

            _cfg = new BCfg
            {
                N = _n, Steps = 1, MaxN = h.MaxNeurons,
                HasNutrients = h.HasNutrients ? 1 : 0, HasReserve = h.HasReserve ? 1 : 0,
                WorldDepth = h.WorldDepth, ChemHalf = h.ChemHalf, EnergyScale = h.EnergyScale,
                FlowScale = h.FlowScale, RateScale = h.RateScale, ConstCE = h.ConstCE, Dt = h.Dt,
                Nx = h.Nx, Ny = h.Ny, Nz = h.Nz, LayerCount = h.LayerCount, RefugeLayers = h.RefugeLayers,
                LayerStride = h.Nx * h.Nz,
                CellMetres = h.CellMetres, TankR = h.TankR, RefugeFraction = h.RefugeFraction,
                CellVolume = h.CellVolume,
            };

            var watch = System.Diagnostics.Stopwatch.StartNew();
            _kernel = groupSize > 0
                ? _acc.LoadImplicitlyGroupedStreamKernel<Index1D, BTopo, BBody, BNeur, BState, BField, BCfg>(BrainKernels.Step, groupSize)
                : _acc.LoadAutoGroupedStreamKernel<Index1D, BTopo, BBody, BNeur, BState, BField, BCfg>(BrainKernels.Step);
            _spin = _acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, int>(BrainKernels.Spin);
            _acc.Synchronize();
            CompileMs = watch.Elapsed.TotalMilliseconds;

            ResetState();
        }

        private MemoryBuffer1D<int, Stride1D.Dense> I(int[] a) => _acc.Allocate1D(a);
        private MemoryBuffer1D<float, Stride1D.Dense> F(float[] a) => _acc.Allocate1D(a);
        private MemoryBuffer1D<Real, Stride1D.Dense> R(double[] a) => _acc.Allocate1D(Cv(a));

        private static Real[] Cv(double[] a)
        {
            var v = new Real[a.Length];
            for (int i = 0; i < a.Length; i++) v[i] = (Real)a[i];
            return v;
        }

        private BTopo Topo => new BTopo
        {
            Links = _links.View, Dof = _dof.View, Mask = _mask.View, LDofCount = _ldofCount.View,
            LDofStart = _ldofStart.View, NOff = _noff.View, NCnt = _ncnt.View, Parent = _parent.View,
            FirstChild = _firstChild.View, BDofStart = _bdofStart.View, BDofCount = _bdofCount.View,
        };

        private BBody Body => new BBody
        {
            Pos = _pos.View, Rot = _rot.View, RelVel = _relVel.View, Q = _q.View, Qd = _qd.View,
            LimitHi = _limitHi.View, Reserve = _reserve.View, Damage = _damage.View, Contact = _contact.View,
        };

        private BNeur Neur => new BNeur
        {
            Op = _op.View, NIn = _nin.View, Kind = _kind.View, Index = _index.View, Channel = _channel.View,
            Freq = _freq.View, Phase = _phase.View, Amp = _amp.View, Bias = _bias.View,
            Const = _const.View, Weight = _weight.View,
        };

        private BState State => new BState
        {
            S = _s.View, Mem = _mem.View, Drive = _drive.View, Depth = _depth.View, Up = _up.View,
            Chem = _chem.View, Flow = _flow.View, Angle = _angle.View, Rate = _rate.View,
            Energy = _energy.View, Parity = _parity.View, Clock = _clock.View,
        };

        private BField Field => new BField { Stock = _stock.View, LowestLive = _lowest.View };

        /// <summary>Puts the recurrent state back as the host built it.</summary>
        public void ResetState()
        {
            _s.View.CopyFromCPU(_h.S0);
            _mem.View.CopyFromCPU(_h.Mem0);
            _clock.View.CopyFromCPU(_h.Clock0);
            _parity.View.MemSetToZero();
            _drive.View.MemSetToZero();
            _depth.View.MemSetToZero(); _up.View.MemSetToZero(); _chem.View.MemSetToZero();
            _flow.View.MemSetToZero(); _angle.View.MemSetToZero(); _rate.View.MemSetToZero();
            _energy.View.MemSetToZero();
            _acc.Synchronize();
        }

        public void Launch(int steps)
        {
            BCfg cfg = _cfg;
            cfg.Steps = steps;
            _kernel(_n, Topo, Body, Neur, State, Field, cfg);
        }

        public void Spin(int iterations) => _spin(1, _sink.View, iterations);

        public void Synchronize() => _acc.Synchronize();

        /// <summary>What goes up once a metabolic step: the field's stock and every body's reserve.</summary>
        public void UploadMetabolic()
        {
            _stock.View.CopyFromCPU(_stockHost);
            _reserve.View.CopyFromCPU(_h.Reserve);
        }

        public long MetabolicBytes => (long)_stockHost.Length * 8 + (long)_h.Reserve.Length * 4;

        public Gpu.Spike3.BrainResult Read() => new Gpu.Spike3.BrainResult
        {
            S = _s.GetAsArray1D(), Mem = _mem.GetAsArray1D(), Drive = _drive.GetAsArray1D(),
            Depth = _depth.GetAsArray1D(), Up = _up.GetAsArray1D(), Chem = _chem.GetAsArray1D(),
            Flow = _flow.GetAsArray1D(), Angle = _angle.GetAsArray1D(), Rate = _rate.GetAsArray1D(),
            Energy = _energy.GetAsArray1D(), Parity = _parity.GetAsArray1D(), Clock = _clock.GetAsArray1D(),
        };

        /// <summary>FNV-1a over every neuron slot, memory, drive and clock a body owns.</summary>
        public ulong Digest()
        {
            Gpu.Spike3.BrainResult r = Read();
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < _n; i++)
            {
                int prev = r.Parity[i] * _h.MaxNeurons, cur = (1 - r.Parity[i]) * _h.MaxNeurons;
                for (int k = 0; k < _h.NeuronCount[i]; k++)
                {
                    Mix(ref hash, r.S[(prev + k) * _n + i]);
                    Mix(ref hash, r.S[(cur + k) * _n + i]);
                    Mix(ref hash, r.Mem[k * _n + i]);
                }
                for (int d = 0; d < _h.Dof[i]; d++) Mix(ref hash, r.Drive[d * _n + i]);
                ulong c = (ulong)BitConverter.DoubleToInt64Bits(r.Clock[i]);
                for (int b = 0; b < 8; b++) { hash ^= (c >> (8 * b)) & 0xFF; hash *= 1099511628211UL; }
            }
            return hash;
        }

        private static void Mix(ref ulong hash, float v)
        {
            uint u = (uint)BitConverter.SingleToInt32Bits(v);
            for (int b = 0; b < 4; b++) { hash ^= (u >> (8 * b)) & 0xFF; hash *= 1099511628211UL; }
        }

        public void Dispose()
        {
            foreach (var b in new MemoryBuffer[] { _links, _dof, _mask, _ldofCount, _ldofStart, _noff, _ncnt,
                _parent, _firstChild, _bdofStart, _bdofCount, _pos, _rot, _relVel, _q, _qd, _limitHi,
                _reserve, _damage, _contact, _op, _nin, _kind, _index, _channel, _freq, _phase, _amp, _bias,
                _const, _weight, _s, _mem, _drive, _depth, _up, _chem, _flow, _angle, _rate, _energy,
                _parity, _clock, _stock, _lowest, _sink })
            {
                b.Dispose();
            }
        }
    }
}

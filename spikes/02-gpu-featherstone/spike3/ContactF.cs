// ---------------------------------------------------------------------------------------------
// GENERATED from spike3/contact.template by spike3/gen3.py. Do not edit; edit the template and
// regenerate.
//
// The contact grid and the contact pass, transcribed from Evosim.Dynamics for ILGPU. Sources,
// line for line (commit d153224):
//   ContactGrid.Build        -> src/Evosim.Dynamics/Contacts.cs:73-179   (MeanSerial/MeanTree,
//                               Ranges, Scan, Scatter: the grid built on the card)
//   ContactGrid.Neighbours   -> src/Evosim.Dynamics/Contacts.cs:186-244  (Query, first half)
//   Contacts.Apply           -> src/Evosim.Dynamics/Contacts.cs:280-398  (Query, second half)
//   ContactBed.Push          -> src/Evosim.Dynamics/ContactBed.cs:46-70
//   BedShape.HeightAndGradient -> src/Evosim.Core/Environment/BedShape.cs:700-733
//   ContactLaw.PairPush / BodyPush -> src/Evosim.Dynamics/ContactLaw.cs:66-96
//
// What differs from the CPU, and why it cannot change a result:
//   - The grid's buckets are filled by atomics, so the order of the entries inside a bucket is a
//     race. The query sorts and deduplicates its candidates, as the CPU does, so the list it
//     reads is the same list in the same order whatever the race did.
//   - The candidate and overlap lists live in fixed-capacity scratch strided by body; the CPU
//     grows its arrays by doubling. A body whose list would pass the capacity is counted and its
//     result is wrong; the harness reports the count.
//   - MeanTree sums the radii in a fixed tree rather than in index order. Deterministic on one
//     device and group size, but not the CPU's rounding of the mean: the cell size can differ in
//     the last bit, which can move a candidate list and cannot move an overlap or a force (the
//     query is exact at any cell). MeanSerial is the CPU's own order, on one thread.
// ---------------------------------------------------------------------------------------------

using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Algorithms;

namespace Gpu.Spike3.Sgl
{
    using Real = System.Single;

    /// <summary>The committed spheres and the masses: what every body reads of every other.</summary>
    public struct CSph
    {
        public ArrayView<Real> Cx, Cy, Cz, R, Vx, Vy, Vz, M, LinkMass;
        public ArrayView<int> Active, Links;
    }

    public struct CGrid
    {
        public ArrayView<int> Lo, Hi, Counts, Start, Cursor, Items, Flags;
        public ArrayView<Real> Cell;
    }

    public struct COut
    {
        public ArrayView<int> Found, Unique, Cand, NOver, Over, BedGlass;
        public ArrayView<Real> Push, PairSum, Force, Fext;
    }

    public struct CBed { public ArrayView<Real> Amp, Kx, Kz, Phase; }

    /// <summary>Where a body's candidate list lives while it is gathered, sorted and read.</summary>
    public interface ICand
    {
        int Get(int k);
        void Set(int k, int v);
    }

    /// <summary>A global array strided by body, <c>k * N + i</c>, as every other per-body array.</summary>
    public struct GlobalCand : ICand
    {
        public ArrayView<int> V;
        public int N, I;
        public int Get(int k) => V[k * N + I];
        public void Set(int k, int v) { V[k * N + I] = v; }
    }

    /// <summary>The thread's own local memory, of a capacity fixed when the kernel is compiled.</summary>
    public struct LocalCand : ICand
    {
        public ArrayView<int> V;
        public int Get(int k) => V[k];
        public void Set(int k, int v) { V[k] = v; }
    }

    public struct CCfg
    {
        public int N, Buckets, Mask, CandCap, OverCap, EntryCap, MaxLinks, Contact, HasBed, BedModes, Record;
        public Real Omega, Zeta, MaxSep, MaxDv, Dt, WorldDepth, TankRadius, AxisX, AxisZ;
        public Real BedRadius, BedDepth, TiltX, TiltZ, Offset, CellOverride;
    }

    public static class ContactKernels
    {
        // ------------------------------------------------------------------ the grid

        /// <summary>The mean radius in the CPU's order, on one thread (Contacts.cs:80-98).</summary>
        public static void MeanSerial(Index1D index, CSph s, CGrid g, CCfg cfg)
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
        public static void MeanTree(CSph s, CGrid g, CCfg cfg)
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

        /// <summary>The cells each sphere covers, and the histogram (Contacts.cs:110-160).</summary>
        public static void Ranges(Index1D index, CSph s, CGrid g, CCfg cfg)
        {
            int i = index.X;
            int N = cfg.N;
            if (i >= N) return;

            if (s.Active[i] == 0)
            {
                // Not entered and never a candidate: lo above hi on every axis.
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

        /// <summary>
        /// The exclusive prefix sum of the bucket counts, on one group: each thread sums a chunk,
        /// a Hillis-Steele scan over the chunk sums, then each chunk is written (Contacts.cs:162-169).
        /// </summary>
        public static void Scan(CGrid g, CCfg cfg)
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
        public static void Scatter(Index1D index, CGrid g, CCfg cfg)
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

        // ------------------------------------------------------------------ the query and the push

        public static void Query(Index1D index, CSph s, CGrid g, COut o, CBed bed, CCfg cfg)
        {
            int i = index.X;
            if (i >= cfg.N) return;
            QueryBody(i, new GlobalCand { V = o.Cand, N = cfg.N, I = i }, s, g, o, bed, cfg);
        }

        /// <summary>
        /// The same query with the candidate scratch in the thread's local memory rather than in
        /// a global array strided by body. The same algorithm on the same numbers: only where the
        /// list lives differs. The unique list is copied out for the checks when recording.
        /// </summary>
        public static void QueryLocal(Index1D index, CSph s, CGrid g, COut o, CBed bed, CCfg cfg)
        {
            int i = index.X;
            if (i >= cfg.N) return;
            var buffer = LocalMemory.Allocate<int>(256);
            QueryBody(i, new LocalCand { V = buffer }, s, g, o, bed, cfg);

            if (cfg.Record != 0)
            {
                int unique = o.Unique[i];
                for (int k = 0; k < unique; k++) o.Cand[k * cfg.N + i] = buffer[k];
            }
        }

        private static void QueryBody<TC>(int i, TC cand, CSph s, CGrid g, COut o, CBed bed, CCfg cfg)
            where TC : struct, ICand
        {
            int N = cfg.N;
            int links = s.Links[i];

            if (s.Active[i] == 0)
            {
                o.Found[i] = 0;
                o.Unique[i] = 0;
                o.NOver[i] = 0;
                o.BedGlass[i] = 0;
                o.PairSum[i] = 0; o.PairSum[N + i] = 0; o.PairSum[2 * N + i] = 0;
                o.Force[i] = 0; o.Force[N + i] = 0; o.Force[2 * N + i] = 0;
                for (int l = 0; l < links; l++)
                {
                    o.Fext[(3 * l) * N + i] = 0;
                    o.Fext[(3 * l + 1) * N + i] = 0;
                    o.Fext[(3 * l + 2) * N + i] = 0;
                }
                return;
            }

            Real cx = s.Cx[i], cy = s.Cy[i], cz = s.Cz[i];
            Real r = s.R[i];
            Real vx = s.Vx[i], vy = s.Vy[i], vz = s.Vz[i];
            Real m = s.M[i];

            Real fx = 0, fy = 0, fz = 0;
            int found = 0;
            int unique = 0;
            int nOver = 0;
            int C = cfg.CandCap;

            if (cfg.Contact != 0)
            {
                // ---- ContactGrid.Neighbours
                int lx = g.Lo[i], hx = g.Hi[i];
                int ly = g.Lo[N + i], hy = g.Hi[N + i];
                int lz = g.Lo[2 * N + i], hz = g.Hi[2 * N + i];

                for (int x = lx; x <= hx; x++)
                for (int y = ly; y <= hy; y++)
                for (int z = lz; z <= hz; z++)
                {
                    int h = Hash(x, y, z) & cfg.Mask;

                    // The second bound is the scratch's, never reached when the entry count fits.
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

                        if (found < C) cand.Set(found, other);
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
                    // Ascending (insertion sort; any correct sort of integers gives the one order),
                    // then each once.
                    for (int a = 1; a < n; a++)
                    {
                        int key = cand.Get(a);
                        int b = a - 1;
                        while (b >= 0 && cand.Get(b) > key)
                        {
                            cand.Set(b + 1, cand.Get(b));
                            b--;
                        }
                        cand.Set(b + 1, key);
                    }

                    unique = 1;
                    for (int a = 1; a < n; a++)
                    {
                        int v = cand.Get(a);
                        if (v == cand.Get(unique - 1)) continue;
                        cand.Set(unique, v);
                        unique++;
                    }
                }

                // ---- Contacts.Apply, the pair loop
                for (int k = 0; k < unique; k++)
                {
                    int other = cand.Get(k);
                    if (s.Active[other] == 0) continue;

                    Real bx = cx - s.Cx[other];
                    Real by = cy - s.Cy[other];
                    Real bz = cz - s.Cz[other];
                    Real distance = System.MathF.Sqrt(bx * bx + by * by + bz * bz);
                    Real penetration = r + s.R[other] - distance;
                    if (penetration <= 0) continue;

                    if (nOver < cfg.OverCap) o.Over[nOver * N + i] = other;

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

                    Real mo = s.M[other];
                    Real reduced = m * mo / (m + mo);

                    Real stiffness = reduced * cfg.Omega * cfg.Omega;
                    Real damping = (Real)2.0 * cfg.Zeta * reduced * cfg.Omega;

                    Real dvx = vx - s.Vx[other];
                    Real dvy = vy - s.Vy[other];
                    Real dvz = vz - s.Vz[other];
                    Real approach = dvx * nx + dvy * ny + dvz * nz;

                    Real p = PairPush(stiffness * penetration - damping * approach, reduced, approach, cfg);
                    Real px = nx * p, py = ny * p, pz = nz * p;

                    if (cfg.Record != 0 && nOver < cfg.OverCap)
                    {
                        o.Push[(3 * nOver) * N + i] = px;
                        o.Push[(3 * nOver + 1) * N + i] = py;
                        o.Push[(3 * nOver + 2) * N + i] = pz;
                    }

                    fx = fx + px;
                    fy = fy + py;
                    fz = fz + pz;

                    nOver++;
                }
            }

            o.PairSum[i] = fx; o.PairSum[N + i] = fy; o.PairSum[2 * N + i] = fz;

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
                // ContactBed.Push, with BedShape.HeightAndGradient inlined.
                Real dx = cx - cfg.BedRadius;
                Real dz = cz - cfg.BedRadius;

                Real hgt = cfg.TiltX * dx + cfg.TiltZ * dz - cfg.Offset;
                Real gx = cfg.TiltX;
                Real gz = cfg.TiltZ;

                for (int md = 0; md < cfg.BedModes; md++)
                {
                    Real kx = bed.Kx[md], kz = bed.Kz[md];
                    Real chi = kx * dx + kz * dz + bed.Phase[md];
                    Real a = bed.Amp[md];
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

            // ContactLaw.BodyPush
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

            o.Force[i] = fx; o.Force[N + i] = fy; o.Force[2 * N + i] = fz;

            if (fx != 0 || fy != 0 || fz != 0)
            {
                Real scale = (Real)1.0 / m;
                for (int l = 0; l < links; l++)
                {
                    Real w = s.LinkMass[l * N + i] * scale;
                    o.Fext[(3 * l) * N + i] = (Real)0 + fx * w;
                    o.Fext[(3 * l + 1) * N + i] = (Real)0 + fy * w;
                    o.Fext[(3 * l + 2) * N + i] = (Real)0 + fz * w;
                }
            }
            else
            {
                for (int l = 0; l < links; l++)
                {
                    o.Fext[(3 * l) * N + i] = 0;
                    o.Fext[(3 * l + 1) * N + i] = 0;
                    o.Fext[(3 * l + 2) * N + i] = 0;
                }
            }

            o.Found[i] = found;
            o.Unique[i] = unique;
            o.NOver[i] = nOver;
            o.BedGlass[i] = bedGlass;
        }

        /// <summary>For the launch-overhead measurement: a kernel that does nothing.</summary>
        public static void Empty(Index1D index, CCfg cfg) { }

        /// <summary>
        /// One thread busy for a while, so that the launches queued behind it run back to back
        /// and a marker pair around each measures the kernel and not the host's submission gap.
        /// </summary>
        public static void Spin(Index1D index, ArrayView<int> sink, int iterations)
        {
            if (index.X != 0) return;
            uint x = (uint)sink[0];
            for (int k = 0; k < iterations; k++) x = x * 1664525u + 1013904223u;
            sink[1] = (int)x;
        }

        // ------------------------------------------------------------------ helpers

        private static Real PairPush(Real raw, Real reducedMass, Real approach, CCfg cfg)
        {
            if (!(raw > 0)) return 0;

            Real cap = cfg.MaxSep;
            if (!(cap > 0)) return raw;

            Real allowed = cap - (approach < 0 ? approach : 0);
            Real most = reducedMass * allowed / cfg.Dt;

            return raw < most ? raw : most;
        }

        private static int Floor(Real v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        private static int Hash(int x, int y, int z) =>
            (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);
    }

    /// <summary>Device buffers and the launches, for one precision.</summary>
    internal sealed class ContactRunner : Gpu.Spike3.IContactRunner
    {
        private readonly Accelerator _acc;
        private readonly int _n, _maxLinks, _buckets, _group;
        private readonly Gpu.Spike3.ContactHost _h;

        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _cx, _cy, _cz, _r, _vx, _vy, _vz, _m, _linkMass;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _active, _links;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _lo, _hi, _counts, _start, _cursor, _items, _flags;
        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _cell;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _found, _unique, _cand, _nOver, _over, _bedGlass;
        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _push, _pairSum, _force, _fext;
        private readonly MemoryBuffer1D<Real, Stride1D.Dense> _amp, _kx, _kz, _phase;

        private readonly Action<Index1D, CSph, CGrid, CCfg> _meanSerial;
        private readonly Action<KernelConfig, CSph, CGrid, CCfg> _meanTree;
        private readonly Action<Index1D, CSph, CGrid, CCfg> _ranges;
        private readonly Action<KernelConfig, CGrid, CCfg> _scan;
        private readonly Action<Index1D, CGrid, CCfg> _scatter;
        private readonly Action<Index1D, CSph, CGrid, COut, CBed, CCfg> _query, _queryLocal;
        private bool _local;
        private readonly Action<Index1D, CCfg> _empty;
        private readonly Action<Index1D, ArrayView<int>, int> _spin;
        private readonly MemoryBuffer1D<int, Stride1D.Dense> _sink;

        private CCfg _cfg;

        public double CompileMs { get; }
        public string Precision => "single";
        public int Buckets => _buckets;

        public ContactRunner(Accelerator accelerator, Gpu.Spike3.ContactHost h, int groupSize, int reduceGroup)
        {
            _acc = accelerator;
            _h = h;
            _n = h.N;
            _maxLinks = h.MaxLinks;
            _group = reduceGroup;

            int buckets = 1;
            while (buckets < _n * 2) buckets <<= 1;
            _buckets = buckets;

            _cx = A(_n); _cy = A(_n); _cz = A(_n); _r = A(_n); _vx = A(_n); _vy = A(_n); _vz = A(_n);
            _m = A(_n);
            _linkMass = _acc.Allocate1D(Cv(h.LinkMass));
            _active = _acc.Allocate1D<int>(_n);
            _links = _acc.Allocate1D(h.Links);

            _lo = _acc.Allocate1D<int>(3 * _n); _hi = _acc.Allocate1D<int>(3 * _n);
            _counts = _acc.Allocate1D<int>(buckets + 1);
            _start = _acc.Allocate1D<int>(buckets + 1);
            _cursor = _acc.Allocate1D<int>(buckets + 1);
            _items = _acc.Allocate1D<int>(h.EntryCap);
            _flags = _acc.Allocate1D<int>(4);
            _cell = A(1);

            _found = _acc.Allocate1D<int>(_n); _unique = _acc.Allocate1D<int>(_n);
            _cand = _acc.Allocate1D<int>((long)h.CandCap * _n);
            _nOver = _acc.Allocate1D<int>(_n);
            _over = _acc.Allocate1D<int>((long)h.OverCap * _n);
            _bedGlass = _acc.Allocate1D<int>(_n);
            _push = A((long)3 * h.OverCap * _n);
            _pairSum = A(3 * _n); _force = A(3 * _n);
            _fext = A((long)3 * _maxLinks * _n);

            int modes = h.BedAmp.Length > 0 ? h.BedAmp.Length : 1;
            _amp = A(modes); _kx = A(modes); _kz = A(modes); _phase = A(modes);
            if (h.BedAmp.Length > 0)
            {
                _amp.View.CopyFromCPU(Cv(h.BedAmp));
                _kx.View.CopyFromCPU(Cv(h.BedKx));
                _kz.View.CopyFromCPU(Cv(h.BedKz));
                _phase.View.CopyFromCPU(Cv(h.BedPhase));
            }

            _cfg = new CCfg
            {
                N = _n, Buckets = buckets, Mask = buckets - 1,
                CandCap = h.CandCap, OverCap = h.OverCap, EntryCap = h.EntryCap,
                MaxLinks = _maxLinks, Contact = h.CreatureContact ? 1 : 0,
                HasBed = h.HasBed ? 1 : 0, BedModes = h.BedAmp.Length, Record = 1,
                Omega = (Real)h.Omega, Zeta = (Real)h.Zeta, MaxSep = (Real)h.MaxSep,
                MaxDv = (Real)h.MaxDv, Dt = (Real)h.Dt, WorldDepth = (Real)h.WorldDepth,
                TankRadius = (Real)h.TankRadius, AxisX = (Real)h.AxisX, AxisZ = (Real)h.AxisZ,
                BedRadius = (Real)h.BedRadius, BedDepth = (Real)h.BedDepth,
                TiltX = (Real)h.TiltX, TiltZ = (Real)h.TiltZ, Offset = (Real)h.Offset,
                CellOverride = 0,
            };

            var watch = System.Diagnostics.Stopwatch.StartNew();
            _meanSerial = _acc.LoadAutoGroupedStreamKernel<Index1D, CSph, CGrid, CCfg>(ContactKernels.MeanSerial);
            _meanTree = _acc.LoadStreamKernel<CSph, CGrid, CCfg>(ContactKernels.MeanTree);
            _scan = _acc.LoadStreamKernel<CGrid, CCfg>(ContactKernels.Scan);
            if (groupSize > 0)
            {
                _ranges = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, CSph, CGrid, CCfg>(ContactKernels.Ranges, groupSize);
                _scatter = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, CGrid, CCfg>(ContactKernels.Scatter, groupSize);
                _query = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, CSph, CGrid, COut, CBed, CCfg>(ContactKernels.Query, groupSize);
                _queryLocal = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, CSph, CGrid, COut, CBed, CCfg>(ContactKernels.QueryLocal, groupSize);
                _empty = _acc.LoadImplicitlyGroupedStreamKernel<Index1D, CCfg>(ContactKernels.Empty, groupSize);
            }
            else
            {
                _ranges = _acc.LoadAutoGroupedStreamKernel<Index1D, CSph, CGrid, CCfg>(ContactKernels.Ranges);
                _scatter = _acc.LoadAutoGroupedStreamKernel<Index1D, CGrid, CCfg>(ContactKernels.Scatter);
                _query = _acc.LoadAutoGroupedStreamKernel<Index1D, CSph, CGrid, COut, CBed, CCfg>(ContactKernels.Query);
                _queryLocal = _acc.LoadAutoGroupedStreamKernel<Index1D, CSph, CGrid, COut, CBed, CCfg>(ContactKernels.QueryLocal);
                _empty = _acc.LoadAutoGroupedStreamKernel<Index1D, CCfg>(ContactKernels.Empty);
            }
            _spin = _acc.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, int>(ContactKernels.Spin);
            _sink = _acc.Allocate1D<int>(2);
            _sink.View.MemSetToZero();
            _acc.Synchronize();
            CompileMs = watch.Elapsed.TotalMilliseconds;
        }

        public void Spin(int iterations) => _spin(1, _sink.View, iterations);

        private MemoryBuffer1D<Real, Stride1D.Dense> A(long n) => _acc.Allocate1D<Real>(n);

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

        private CSph Sph => new CSph
        {
            Cx = _cx.View, Cy = _cy.View, Cz = _cz.View, R = _r.View, Vx = _vx.View, Vy = _vy.View,
            Vz = _vz.View, M = _m.View, LinkMass = _linkMass.View, Active = _active.View, Links = _links.View,
        };

        private CGrid Grid => new CGrid
        {
            Lo = _lo.View, Hi = _hi.View, Counts = _counts.View, Start = _start.View,
            Cursor = _cursor.View, Items = _items.View, Flags = _flags.View, Cell = _cell.View,
        };

        private COut Out => new COut
        {
            Found = _found.View, Unique = _unique.View, Cand = _cand.View, NOver = _nOver.View,
            Over = _over.View, BedGlass = _bedGlass.View, Push = _push.View, PairSum = _pairSum.View,
            Force = _force.View, Fext = _fext.View,
        };

        private CBed Bed => new CBed { Amp = _amp.View, Kx = _kx.View, Kz = _kz.View, Phase = _phase.View };

        /// <summary>The committed spheres going up: what the check does every step, and what the port would not.</summary>
        public void UploadSpheres()
        {
            _cx.View.CopyFromCPU(Cv(_h.Cx)); _cy.View.CopyFromCPU(Cv(_h.Cy)); _cz.View.CopyFromCPU(Cv(_h.Cz));
            _r.View.CopyFromCPU(Cv(_h.R));
            _vx.View.CopyFromCPU(Cv(_h.Vx)); _vy.View.CopyFromCPU(Cv(_h.Vy)); _vz.View.CopyFromCPU(Cv(_h.Vz));
            _m.View.CopyFromCPU(Cv(_h.M));
            _active.View.CopyFromCPU(_h.Active);
        }

        private Real[][] _narrowed;

        /// <summary>The per-step upload a host-fed port would pay: eight reals and a flag a body, pre-narrowed.</summary>
        public void UploadSpheresTimed()
        {
            if (_narrowed == null)
            {
                _narrowed = new[] { Cv(_h.Cx), Cv(_h.Cy), Cv(_h.Cz), Cv(_h.R), Cv(_h.Vx), Cv(_h.Vy), Cv(_h.Vz), Cv(_h.M) };
            }
            _cx.View.CopyFromCPU(_narrowed[0]); _cy.View.CopyFromCPU(_narrowed[1]); _cz.View.CopyFromCPU(_narrowed[2]);
            _r.View.CopyFromCPU(_narrowed[3]); _vx.View.CopyFromCPU(_narrowed[4]); _vy.View.CopyFromCPU(_narrowed[5]);
            _vz.View.CopyFromCPU(_narrowed[6]); _m.View.CopyFromCPU(_narrowed[7]);
            _active.View.CopyFromCPU(_h.Active);
        }

        public void SetRecord(bool record) => _cfg.Record = record ? 1 : 0;

        public void SetCellOverride(double cell) => _cfg.CellOverride = (Real)cell;

        public void Build(bool serialMean)
        {
            _counts.View.MemSetToZero();
            if (serialMean) _meanSerial(1, Sph, Grid, _cfg);
            else _meanTree(new KernelConfig(1, _group), Sph, Grid, _cfg);
            _ranges(_n, Sph, Grid, _cfg);
            _scan(new KernelConfig(1, _group), Grid, _cfg);
            _scatter(_n, Grid, _cfg);
        }

        public void Query() => (_local ? _queryLocal : _query)(_n, Sph, Grid, Out, Bed, _cfg);

        /// <summary>Which query the step launches: the candidate scratch in local memory, or strided in global.</summary>
        public void SetLocal(bool local)
        {
            if (local && _h.CandCap > 256) throw new ArgumentException("the local scratch holds 256 candidates");
            _local = local;
        }

        public void Step(bool serialMean) { Build(serialMean); Query(); }

        // The pieces, for timing one at a time.
        public void Memset() => _counts.View.MemSetToZero();
        public void Mean(bool serial)
        {
            if (serial) _meanSerial(1, Sph, Grid, _cfg);
            else _meanTree(new KernelConfig(1, _group), Sph, Grid, _cfg);
        }
        public void RangesOnly() => _ranges(_n, Sph, Grid, _cfg);
        public void ScanOnly() => _scan(new KernelConfig(1, _group), Grid, _cfg);
        public void ScatterOnly() => _scatter(_n, Grid, _cfg);
        public void EmptyLaunch() => _empty(_n, _cfg);

        public void Synchronize() => _acc.Synchronize();

        public void ClearFlags() => _flags.View.MemSetToZero();

        public Gpu.Spike3.ContactResult Read()
        {
            var res = new Gpu.Spike3.ContactResult
            {
                N = _n, CandCap = _h.CandCap, OverCap = _h.OverCap, MaxLinks = _maxLinks,
                Found = _found.GetAsArray1D(), Unique = _unique.GetAsArray1D(),
                Cand = _cand.GetAsArray1D(), NOver = _nOver.GetAsArray1D(), Over = _over.GetAsArray1D(),
                BedGlass = _bedGlass.GetAsArray1D(),
                Push = Wd(_push.GetAsArray1D()), PairSum = Wd(_pairSum.GetAsArray1D()),
                Force = Wd(_force.GetAsArray1D()), Fext = Wd(_fext.GetAsArray1D()),
                Cell = _cell.GetAsArray1D()[0],
                Entries = _start.GetAsArray1D()[_buckets],
                EntryOverflow = _flags.GetAsArray1D()[0],
            };
            return res;
        }

        /// <summary>FNV-1a over the raw bits of every output: the card's identity with itself.</summary>
        public ulong Digest()
        {
            ulong hash = 14695981039346656037UL;
            BitsI(ref hash, _found.GetAsArray1D()); BitsI(ref hash, _unique.GetAsArray1D());
            BitsI(ref hash, _nOver.GetAsArray1D()); BitsI(ref hash, _bedGlass.GetAsArray1D());
            // Only the live part of each list: slots past a body's count hold stale values.
            int[] unique = _unique.GetAsArray1D(), nOver = _nOver.GetAsArray1D();
            int[] cand = _cand.GetAsArray1D(), over = _over.GetAsArray1D();
            Real[] push = _push.GetAsArray1D();
            for (int i = 0; i < _n; i++)
            {
                for (int k = 0; k < unique[i] && k < _h.CandCap; k++) Mix(ref hash, (uint)cand[k * _n + i]);
                for (int k = 0; k < nOver[i] && k < _h.OverCap; k++)
                {
                    Mix(ref hash, (uint)over[k * _n + i]);
                    for (int c = 0; c < 3; c++) BitsR(ref hash, push[(3 * k + c) * _n + i]);
                }
            }
            foreach (Real v in _pairSum.GetAsArray1D()) BitsR(ref hash, v);
            foreach (Real v in _force.GetAsArray1D()) BitsR(ref hash, v);
            int[] links = _h.Links;
            Real[] fext = _fext.GetAsArray1D();
            for (int i = 0; i < _n; i++)
                for (int l = 0; l < links[i]; l++)
                    for (int c = 0; c < 3; c++) BitsR(ref hash, fext[(3 * l + c) * _n + i]);
            BitsR(ref hash, _cell.GetAsArray1D()[0]);
            return hash;
        }

        private static void BitsI(ref ulong hash, int[] a) { foreach (int v in a) Mix(ref hash, (uint)v); }

        private static void BitsR(ref ulong hash, Real v)
        {
            byte[] raw = BitConverter.GetBytes(v);
            for (int b = 0; b < raw.Length; b++) { hash ^= raw[b]; hash *= 1099511628211UL; }
        }

        private static void Mix(ref ulong hash, uint v)
        {
            for (int b = 0; b < 4; b++) { hash ^= (v >> (8 * b)) & 0xFF; hash *= 1099511628211UL; }
        }

        public void Dispose()
        {
            foreach (var b in new MemoryBuffer[] { _cx, _cy, _cz, _r, _vx, _vy, _vz, _m, _linkMass, _active,
                _links, _lo, _hi, _counts, _start, _cursor, _items, _flags, _cell, _found, _unique, _cand,
                _nOver, _over, _bedGlass, _push, _pairSum, _force, _fext, _amp, _kx, _kz, _phase, _sink })
            {
                b.Dispose();
            }
        }
    }
}

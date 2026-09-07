using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Dead matter, or free matter, as a set of vertices that each hold joules at a place —
    /// D083's water. The density anywhere is the kernel-weighted sum of the vertices within
    /// reach; feeding draws from those vertices; the water carries them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why vertices and not cells.</b> A cell field (<see cref="NutrientField"/>) prices every
    /// body in a cell 1 m by 5 m at one number, so a body that stays does not deplete its own
    /// water and a body that moves does not refresh it — undirected movement earns nothing by
    /// construction, and every round through 29 deleted the muscles for that reason (DESIGN §0r,
    /// research Q11). Here a body eats from the vertices around it, a still body eats a hole, a
    /// moving body leaves the hole behind, and the density has a gradient at the kernel's scale
    /// everywhere, which is the first time the chemical sense has had one to read. A corpse is
    /// one vertex where the body died, so a feast reads as a feast.
    /// </para>
    /// <para>
    /// <b>Amounts on vertices, densities between them.</b> Each vertex holds joules, never a
    /// density, so that every operation is a transfer: a deposit adds to one vertex, a take
    /// subtracts from several, a merge sums two into one, a burial removes one whole. §5A.2's
    /// audit therefore holds by construction and never has to trust the interpolation. The
    /// density read at a point is <c>Σ m_j·W(r_j)</c> with a Wendland kernel normalised to unit
    /// integral over its support <see cref="KernelMetres"/> — a mass density, in J/m³, that
    /// integrates back to the mass it was read from.
    /// </para>
    /// <para>
    /// <b>Feeding is exact under the frozen-availability rule.</b> A body's demand is spread
    /// over its neighbours in proportion to edible mass times kernel weight; each vertex freezes
    /// what it has to give; a body's share is the weighted mean of its neighbours' shares; and its
    /// take is spread in proportion to weight times share. Summed over every body demanding at a
    /// vertex, the takes cannot exceed what the vertex froze — the same guarantee the cell field
    /// gives (the Astra review's R1), with the same "food deposited mid-pass is next step's"
    /// consequence.
    /// </para>
    /// <para>
    /// <b>The water moves the vertices.</b> Sinking lowers them; the current
    /// (<see cref="CurrentField.VelocityAt(float, double, int, int)"/>, the same velocity the
    /// bodies feel as drag) displaces them; eddy mixing is a seeded random walk, which is the
    /// standard Lagrangian form of diffusion; the floor stops them and buries them whole at a
    /// rate; the vent and the surface influx emit new ones of a fixed quantum
    /// (<see cref="VertexJoules"/>) at random positions in their plume. Every draw comes from the
    /// field's own stream (<see cref="World.DetritusFieldIndex"/>, <see cref="World.MatterFieldIndex"/>),
    /// so a field knob perturbs no other draw in the world.
    /// </para>
    /// <para>
    /// <b>A deposit joins the nearest vertex within the kernel, and founds one only where there
    /// is none.</b> A vertex is a sample of the water, and a corpse or an exudate lands in the
    /// water that is there; putting it on the nearest sample moves it by less than the kernel can
    /// resolve, and the sample keeps its position, so nothing leaves its grid cell mid-pass. This
    /// is what bounds the count: a field founds vertices only where the water has no sample yet.
    /// The first build merged on drift instead — any two vertices within a quarter metre became
    /// one, every step — and the mixing walk fed that rule until the seeded lattice of 48,000 had
    /// collapsed to 744 fat vertices in 200 s of the smoke and the matter gate was refusing
    /// conceptions in water that was full on average (logbook/0074).
    /// </para>
    /// <para>
    /// <b>Culling is a budget, not physics.</b> <see cref="Cull"/> drops vertices that have been
    /// eaten to nothing and, only while the count is over <see cref="VertexCap"/>, merges nearest
    /// pairs starting at <see cref="MergeMetres"/> and widening toward the kernel. Resolution is
    /// bought with count; the bill is a few million distance checks per metabolic step at a
    /// hundred thousand vertices, against physics that runs fifty times as often. The limit is
    /// noise — a kernel read fluctuates as <c>1/√n</c> in the vertices it covers — not compute,
    /// and the defaults put about thirty vertices in a kernel of seeded water.
    /// </para>
    /// <para>
    /// <b>Geometry is D077's box</b>: x on a ring of <see cref="PatchCount"/> patches
    /// <see cref="PatchWidthMetres"/> wide, z on a ring one patch wide, y from the floor at
    /// −<see cref="DepthMetres"/> to the waterline at 0. Distances take the shorter way round both
    /// rings, which needs each ring at least two kernels long, and the constructor refuses a box
    /// that is not.
    /// </para>
    /// </remarks>
    public sealed class VertexField : IMatterField
    {
        private readonly List<float> _x = new List<float>();
        private readonly List<float> _y = new List<float>();
        private readonly List<float> _z = new List<float>();
        private readonly List<double> _m = new List<double>();
        private readonly List<bool> _alive = new List<bool>();
        private readonly List<double> _demand = new List<double>();
        private readonly List<double> _available = new List<double>();
        private bool _frozen;

        private readonly Rng _rng;
        private double _bank;

        // ---- the hash grid: cells of at least one kernel, so a neighbourhood is 27 cells
        private int _nx, _ny, _nz;
        private float _cx, _cy, _cz;
        private int[] _head = Array.Empty<int>();
        private readonly List<int> _next = new List<int>();
        private bool _gridBuilt;

        // ---- reusable neighbourhood buffers
        private readonly List<int> _nb = new List<int>();
        private readonly List<double> _nbW = new List<double>();
        private readonly int[] _ix = new int[3];
        private readonly int[] _iz = new int[3];

        public float WorldArea { get; }
        public float LayerMetres { get; }
        public float SinkMetresPerSecond { get; }
        public int LayerCount { get; }
        public int PatchCount { get; }
        public float PatchWidthMetres { get; }
        public float DepthMetres { get; }
        public float RefugeMetres { get; }
        public float RefugeEdibleFraction { get; }

        /// <summary>Support radius of the kernel, m — how far a body's mouth reaches.</summary>
        public float KernelMetres { get; }

        /// <summary>Where <see cref="Cull"/> starts merging when the count is over the cap, m.</summary>
        public float MergeMetres { get; }

        /// <summary>The count <see cref="Cull"/> merges down toward.</summary>
        public int VertexCap { get; }

        /// <summary>Joules in one emitted vertex, and the mass the seeded lattice is spaced for.</summary>
        public float VertexJoules { get; }

        /// <summary>The ring's length along x, m — every patch side by side.</summary>
        public float LengthMetres => PatchWidthMetres * PatchCount;

        /// <summary>The box's extent along z, m — one patch.</summary>
        public float WidthMetres => PatchWidthMetres;

        public float LayerVolume => (WorldArea / PatchCount) * LayerMetres;

        /// <summary>Living vertices.</summary>
        public int Count { get; private set; }

        /// <summary>Vertices merged away by <see cref="Cull"/> over the cap, running total.</summary>
        public long Merged { get; private set; }

        /// <summary>Vertices dropped by <see cref="Cull"/> for holding nothing, running total.</summary>
        public long Emptied { get; private set; }

        /// <summary>Steps at which <see cref="Cull"/> could not get under <see cref="VertexCap"/>.</summary>
        public long OverCapSteps { get; private set; }

        /// <summary>Influx received and not yet emitted — less than one quantum, J.</summary>
        public double Bank => _bank;

        private readonly double _sigma;

        public VertexField(
            float worldArea, float layerMetres, float sinkMetresPerSecond, float worldDepth,
            float refugeMetres, float refugeEdibleFraction, int patchCount,
            float kernelMetres, float mergeMetres, int vertexCap, float vertexJoules, ulong seed)
        {
            if (!(worldArea > 0f) || float.IsInfinity(worldArea))
                throw new ArgumentOutOfRangeException(nameof(worldArea), worldArea, "Must be positive and finite.");
            if (!(layerMetres > 0f) || float.IsInfinity(layerMetres))
                throw new ArgumentOutOfRangeException(nameof(layerMetres), layerMetres, "Must be positive and finite.");
            if (!(sinkMetresPerSecond >= 0f) || float.IsInfinity(sinkMetresPerSecond))
                throw new ArgumentOutOfRangeException(nameof(sinkMetresPerSecond), sinkMetresPerSecond, "Must be finite and not negative.");
            if (!(worldDepth > 0f) || float.IsInfinity(worldDepth))
                throw new ArgumentOutOfRangeException(nameof(worldDepth), worldDepth, "Must be positive and finite.");
            if (!(refugeMetres >= 0f) || float.IsInfinity(refugeMetres))
                throw new ArgumentOutOfRangeException(nameof(refugeMetres), refugeMetres, "Must be finite and not negative.");
            if (!(refugeEdibleFraction >= 0f) || refugeEdibleFraction > 1f)
                throw new ArgumentOutOfRangeException(nameof(refugeEdibleFraction), refugeEdibleFraction, "Must be in [0, 1].");
            if (patchCount < 1)
                throw new ArgumentOutOfRangeException(nameof(patchCount), patchCount, "A field needs at least one patch.");
            if (!(kernelMetres > 0f) || float.IsInfinity(kernelMetres))
                throw new ArgumentOutOfRangeException(nameof(kernelMetres), kernelMetres, "Must be positive and finite.");
            if (!(mergeMetres >= 0f) || mergeMetres > kernelMetres)
                throw new ArgumentOutOfRangeException(
                    nameof(mergeMetres), mergeMetres,
                    "Must be in [0, kernel]: the merge search walks the kernel's own grid cells.");
            if (vertexCap < 1)
                throw new ArgumentOutOfRangeException(nameof(vertexCap), vertexCap, "Must be at least 1.");
            if (!(vertexJoules > 0f) || float.IsInfinity(vertexJoules))
                throw new ArgumentOutOfRangeException(nameof(vertexJoules), vertexJoules, "Must be positive and finite.");

            WorldArea = worldArea;
            LayerMetres = layerMetres;
            SinkMetresPerSecond = sinkMetresPerSecond;
            DepthMetres = worldDepth;
            LayerCount = Math.Max(1, (int)Math.Ceiling(worldDepth / layerMetres));
            PatchCount = patchCount;
            PatchWidthMetres = (float)Math.Sqrt(worldArea / patchCount);
            RefugeMetres = refugeMetres;
            RefugeEdibleFraction = refugeEdibleFraction;
            KernelMetres = kernelMetres;
            MergeMetres = mergeMetres;
            VertexCap = vertexCap;
            VertexJoules = vertexJoules;

            if (LengthMetres < 2f * kernelMetres || WidthMetres < 2f * kernelMetres)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kernelMetres), kernelMetres,
                    $"The box is {LengthMetres} x {WidthMetres} m across and a ring shorter than " +
                    "two kernels would reach a vertex both ways round.");
            }

            _sigma = 21.0 / (2.0 * Math.PI * (double)kernelMetres * kernelMetres * kernelMetres);
            _rng = new Rng(seed);

            _nx = Math.Max(1, (int)Math.Floor(LengthMetres / kernelMetres));
            _nz = Math.Max(1, (int)Math.Floor(WidthMetres / kernelMetres));
            _ny = (int)Math.Ceiling(worldDepth / kernelMetres) + 1;
            _cx = LengthMetres / _nx;
            _cz = WidthMetres / _nz;
            _cy = kernelMetres;
            _head = new int[_nx * _ny * _nz];
        }

        // ------------------------------------------------------------------ geometry

        public int LayerOf(float heightY)
        {
            int layer = heightY >= 0f ? 0 : (int)(-heightY / LayerMetres);
            if (layer < 0) return 0;
            return layer >= LayerCount ? LayerCount - 1 : layer;
        }

        /// <summary>The patch an x falls in on the ring — <c>floor(x / W) mod K</c>, D077's rule.</summary>
        public int PatchOf(float x)
        {
            int patch = (int)Math.Floor(WrapAxis(x, LengthMetres) / PatchWidthMetres);
            patch %= PatchCount;
            if (patch < 0) patch += PatchCount;
            return patch;
        }

        private static float WrapAxis(float v, float extent)
        {
            if (v >= 0f && v < extent) return v;
            float folded = v - extent * (float)Math.Floor(v / extent);
            if (folded >= extent || folded < 0f) folded = 0f;
            return folded;
        }

        private float ClampY(float y)
        {
            if (y > 0f) return 0f;
            if (y < -DepthMetres) return -DepthMetres;
            return y;
        }

        /// <summary>The shorter way round the ring, signed.</summary>
        private static float RingDelta(float from, float to, float extent)
        {
            float d = to - from;
            if (d > 0.5f * extent) d -= extent;
            else if (d < -0.5f * extent) d += extent;
            return d;
        }

        private double Edible(int j) =>
            RefugeMetres > 0f && _y[j] <= -DepthMetres + RefugeMetres ? RefugeEdibleFraction : 1.0;

        private bool OnFloor(int j) => _y[j] <= -DepthMetres + 1e-4f;

        private static Float3 Validated(FieldPoint at)
        {
            if (!at.HasHorizontal)
            {
                throw new InvalidOperationException(
                    "A vertex field needs a position, not a depth and a patch: the point was made " +
                    "with FieldPoint.At, which carries no x or z.");
            }

            Float3 p = at.Position;
            if (!p.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(at), p, "A non-finite position.");
            }

            return p;
        }

        // ------------------------------------------------------------------ the grid

        private int CellOf(float x, float y, float z)
        {
            int ix = (int)(WrapAxis(x, LengthMetres) / _cx);
            if (ix >= _nx) ix = _nx - 1;
            int iz = (int)(WrapAxis(z, WidthMetres) / _cz);
            if (iz >= _nz) iz = _nz - 1;
            int iy = (int)(-ClampY(y) / _cy);
            if (iy >= _ny) iy = _ny - 1;
            if (iy < 0) iy = 0;
            return (iy * _nx + ix) * _nz + iz;
        }

        private void BuildGrid()
        {
            for (int i = 0; i < _head.Length; i++) _head[i] = -1;
            while (_next.Count < _x.Count) _next.Add(-1);

            for (int i = 0; i < _x.Count; i++)
            {
                if (!_alive[i]) { _next[i] = -1; continue; }
                int cell = CellOf(_x[i], _y[i], _z[i]);
                _next[i] = _head[cell];
                _head[cell] = i;
            }

            _gridBuilt = true;
        }

        private void EnsureGrid()
        {
            if (!_gridBuilt) BuildGrid();
        }

        private int Distinct(int centre, int n, int[] into)
        {
            int count = 0;
            for (int d = -1; d <= 1; d++)
            {
                int i = ((centre + d) % n + n) % n;
                bool seen = false;
                for (int k = 0; k < count; k++) if (into[k] == i) { seen = true; break; }
                if (!seen) into[count++] = i;
            }

            return count;
        }

        /// <summary>
        /// Fills <see cref="_nb"/> with every living vertex within the kernel of <paramref name="p"/>
        /// and <see cref="_nbW"/> with its kernel weight; or, with <paramref name="radius"/> given,
        /// every vertex within that smaller radius and its distance.
        /// </summary>
        private void Gather(Float3 p, float radius = -1f)
        {
            EnsureGrid();
            _nb.Clear();
            _nbW.Clear();

            float px = WrapAxis(p.X, LengthMetres);
            float pz = WrapAxis(p.Z, WidthMetres);
            float py = ClampY(p.Y);

            float reach = radius < 0f ? KernelMetres : radius;
            float reach2 = reach * reach;

            int cx = Math.Min(_nx - 1, (int)(px / _cx));
            int cz = Math.Min(_nz - 1, (int)(pz / _cz));
            int cy = Math.Min(_ny - 1, (int)(-py / _cy));

            int nxs = Distinct(cx, _nx, _ix);
            int nzs = Distinct(cz, _nz, _iz);

            for (int dy = -1; dy <= 1; dy++)
            {
                int iy = cy + dy;
                if (iy < 0 || iy >= _ny) continue;

                for (int a = 0; a < nxs; a++)
                {
                    int ix = _ix[a];
                    for (int b = 0; b < nzs; b++)
                    {
                        int iz = _iz[b];
                        int j = _head[(iy * _nx + ix) * _nz + iz];

                        while (j >= 0)
                        {
                            if (_alive[j])
                            {
                                float dx = RingDelta(px, _x[j], LengthMetres);
                                float dz = RingDelta(pz, _z[j], WidthMetres);
                                float dyy = _y[j] - py;
                                float r2 = dx * dx + dyy * dyy + dz * dz;

                                if (r2 < reach2)
                                {
                                    double r = Math.Sqrt(r2);
                                    _nb.Add(j);
                                    _nbW.Add(radius < 0f ? Kernel(r) : r);
                                }
                            }

                            j = _next[j];
                        }
                    }
                }
            }
        }

        /// <summary>Wendland's C2 kernel on a support of <see cref="KernelMetres"/>, unit integral.</summary>
        private double Kernel(double r)
        {
            double q = r / KernelMetres;
            if (q >= 1.0) return 0.0;
            double t = 1.0 - q;
            double t2 = t * t;
            return _sigma * t2 * t2 * (1.0 + 4.0 * q);
        }

        private int Add(float x, float y, float z, double joules)
        {
            int i = _x.Count;
            _x.Add(WrapAxis(x, LengthMetres));
            _y.Add(ClampY(y));
            _z.Add(WrapAxis(z, WidthMetres));
            _m.Add(joules);
            _alive.Add(true);
            _demand.Add(0.0);
            _available.Add(0.0);
            _next.Add(-1);
            Count++;

            if (_gridBuilt)
            {
                int cell = CellOf(_x[i], _y[i], _z[i]);
                _next[i] = _head[cell];
                _head[cell] = i;
            }

            return i;
        }

        // ------------------------------------------------------------------ seeding

        /// <summary>
        /// Fills the box with a lattice of vertices holding <paramref name="densityJoulesPerCubicMetre"/>
        /// in total. Spaced for <see cref="VertexJoules"/> per vertex, or wider when that would
        /// exceed <see cref="VertexCap"/>.
        /// </summary>
        public void SeedUniform(float densityJoulesPerCubicMetre)
        {
            if (!(densityJoulesPerCubicMetre > 0f)) return;

            double volume = (double)LengthMetres * WidthMetres * DepthMetres;
            double spacing = Math.Pow(VertexJoules / densityJoulesPerCubicMetre, 1.0 / 3.0);
            double capSpacing = Math.Pow(volume / VertexCap, 1.0 / 3.0);
            if (capSpacing > spacing) spacing = capSpacing;

            int nx = Math.Max(1, (int)Math.Round(LengthMetres / spacing));
            int ny = Math.Max(1, (int)Math.Round(DepthMetres / spacing));
            int nz = Math.Max(1, (int)Math.Round(WidthMetres / spacing));

            double each = densityJoulesPerCubicMetre * volume / ((double)nx * ny * nz);

            for (int j = 0; j < ny; j++)
            {
                float y = -((j + 0.5f) * DepthMetres / ny);
                for (int i = 0; i < nx; i++)
                {
                    float x = (i + 0.5f) * LengthMetres / nx;
                    for (int k = 0; k < nz; k++)
                    {
                        float z = (k + 0.5f) * WidthMetres / nz;
                        Add(x, y, z, each);
                    }
                }
            }

            _gridBuilt = false;
        }

        // ------------------------------------------------------------------ totals

        public double TotalJoules
        {
            get
            {
                double sum = 0.0;
                for (int i = 0; i < _m.Count; i++) if (_alive[i]) sum += _m[i];
                return sum;
            }
        }

        // ------------------------------------------------------------------ bodies and the water

        /// <summary>
        /// Adds joules at a point: into the nearest vertex within the kernel if there is one,
        /// else as a new vertex there. See the class remarks for why the kernel and not a
        /// smaller radius.
        /// </summary>
        public void Deposit(FieldPoint at, float joules)
        {
            if (!(joules > 0f)) return;
            Float3 p = Validated(at);

            Gather(p, KernelMetres);
            int best = -1;
            double bestR = double.MaxValue;
            for (int k = 0; k < _nb.Count; k++)
            {
                if (_nbW[k] < bestR) { bestR = _nbW[k]; best = _nb[k]; }
            }

            if (best >= 0)
            {
                _m[best] += joules;
                return;
            }

            Add(p.X, p.Y, p.Z, joules);
        }

        /// <summary>A deposit by depth and patch, at the patch's centre — for tests and tools.</summary>
        public void Deposit(float heightY, float joules, int patch)
        {
            if (patch < 0 || patch >= PatchCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(patch), patch, $"This field has {PatchCount} patch(es), indexed 0..{PatchCount - 1}.");
            }

            Deposit(new FieldPoint(
                new Float3((patch + 0.5f) * PatchWidthMetres, heightY, 0.5f * WidthMetres), patch), joules);
        }

        /// <summary>The pre-D061 signature: patch 0 of a one-patch field, a refusal otherwise.</summary>
        public void Deposit(float heightY, float joules)
        {
            if (PatchCount > 1)
            {
                throw new InvalidOperationException(
                    $"This field has {PatchCount} patches, so a caller must say which one.");
            }

            Deposit(heightY, joules, 0);
        }

        public float DensityAt(FieldPoint at)
        {
            Gather(Validated(at));
            double sum = 0.0;
            for (int k = 0; k < _nb.Count; k++) sum += _m[_nb[k]] * _nbW[k];
            return (float)sum;
        }

        public float EdibleDensityAt(FieldPoint at)
        {
            Gather(Validated(at));
            double sum = 0.0;
            for (int k = 0; k < _nb.Count; k++)
            {
                int j = _nb[k];
                sum += Edible(j) * _m[j] * _nbW[k];
            }

            return (float)sum;
        }

        public double ReachableStock(FieldPoint at)
        {
            Gather(Validated(at));
            double sum = 0.0;
            for (int k = 0; k < _nb.Count; k++)
            {
                int j = _nb[k];
                if (_nbW[k] > 0.0) sum += Edible(j) * _m[j];
            }

            return sum;
        }

        public void ClearDemand()
        {
            for (int i = 0; i < _demand.Count; i++) _demand[i] = 0.0;
            _frozen = false;
        }

        public void Demand(FieldPoint at, float joules)
        {
            if (!(joules > 0f)) return;
            Gather(Validated(at));

            double sum = 0.0;
            for (int k = 0; k < _nb.Count; k++)
            {
                int j = _nb[k];
                sum += Edible(j) * _m[j] * _nbW[k];
            }

            if (sum <= 0.0) return;

            for (int k = 0; k < _nb.Count; k++)
            {
                int j = _nb[k];
                _demand[j] += joules * (Edible(j) * _m[j] * _nbW[k] / sum);
            }
        }

        public void FreezeAvailability()
        {
            for (int i = 0; i < _m.Count; i++) _available[i] = _alive[i] ? Edible(i) * _m[i] : 0.0;
            _frozen = true;
        }

        /// <summary>What vertex <paramref name="j"/> has to give this pass, and the share it can honour.</summary>
        private void Offer(int j, out double available, out double share)
        {
            available = _frozen ? _available[j] : Edible(j) * _m[j];
            double wanted = _frozen ? _demand[j] : 0.0;
            share = wanted > available && wanted > 0.0 ? available / wanted : 1.0;
        }

        public float FrozenEdibleDensityAt(FieldPoint at)
        {
            if (!_frozen) throw new InvalidOperationException("FreezeAvailability has not been called this step.");
            Gather(Validated(at));
            double sum = 0.0;
            for (int k = 0; k < _nb.Count; k++) sum += _available[_nb[k]] * _nbW[k];
            return (float)sum;
        }

        public float ShareAt(FieldPoint at)
        {
            Gather(Validated(at));
            double weighted = 0.0;
            double total = 0.0;

            for (int k = 0; k < _nb.Count; k++)
            {
                Offer(_nb[k], out double available, out double share);
                double w = available * _nbW[k];
                weighted += w * share;
                total += w;
            }

            return total > 0.0 ? (float)(weighted / total) : 1f;
        }

        public float Take(FieldPoint at, float joules)
        {
            if (!(joules > 0f)) return 0f;
            Gather(Validated(at));

            double total = 0.0;
            for (int k = 0; k < _nb.Count; k++)
            {
                Offer(_nb[k], out double available, out double share);
                total += available * _nbW[k] * share;
            }

            if (total <= 0.0) return 0f;

            double taken = 0.0;
            for (int k = 0; k < _nb.Count; k++)
            {
                int j = _nb[k];
                Offer(j, out double available, out double share);
                double want = joules * (available * _nbW[k] * share / total);
                if (want <= 0.0) continue;

                // Never past what the vertex physically holds for a mouth right now: a deposit
                // mid-pass raised the live mass, an earlier take lowered it, and the frozen
                // figure is a promise about the pass, not about this instant.
                double cap = Edible(j) * _m[j];
                double t = want < cap ? want : cap;
                if (t <= 0.0) continue;

                _m[j] -= t;
                taken += t;
            }

            return (float)taken;
        }

        // ------------------------------------------------------------------ the water on its own

        public void Settle(float seconds)
        {
            if (SinkMetresPerSecond <= 0f || !(seconds > 0f)) return;
            float drop = SinkMetresPerSecond * seconds;

            for (int i = 0; i < _y.Count; i++)
            {
                if (!_alive[i]) continue;
                float y = _y[i] - drop;
                _y[i] = y < -DepthMetres ? -DepthMetres : y;
            }

            _gridBuilt = false;
        }

        /// <summary>
        /// Lifts each vertex resting on the floor back into the layer above it with probability
        /// <c>1 − exp(−rate·seconds)</c> — D051's return leg, one whole vertex at a time.
        /// </summary>
        public void Remineralise(double seconds, float ratePerSecond)
        {
            if (!(ratePerSecond > 0f) || !(seconds > 0d)) return;
            float probability = (float)(1.0 - Math.Exp(-ratePerSecond * seconds));
            float lifted = ClampY(-DepthMetres + LayerMetres);

            for (int i = 0; i < _y.Count; i++)
            {
                if (!_alive[i] || !OnFloor(i)) continue;
                if (_rng.NextFloat() < probability) _y[i] = lifted;
            }

            _gridBuilt = false;
        }

        /// <summary>
        /// Eddy mixing as a random walk: every vertex steps by a Gaussian of variance
        /// <c>2·D·dt</c> per axis, reflected at the waterline and the floor and wrapped round the
        /// rings. Draws nothing when both diffusivities are 0.
        /// </summary>
        public void Mix(float seconds, float diffusivity, float horizontalDiffusivity = 0f)
        {
            if (!(seconds > 0f)) return;
            float sv = diffusivity > 0f ? (float)Math.Sqrt(2.0 * diffusivity * seconds) : 0f;
            float sh = horizontalDiffusivity > 0f ? (float)Math.Sqrt(2.0 * horizontalDiffusivity * seconds) : 0f;
            if (sv <= 0f && sh <= 0f) return;

            for (int i = 0; i < _y.Count; i++)
            {
                if (!_alive[i]) continue;

                if (sv > 0f)
                {
                    float y = _y[i] + _rng.Gaussian() * sv;
                    for (int bounce = 0; bounce < 4 && (y > 0f || y < -DepthMetres); bounce++)
                    {
                        if (y > 0f) y = -y;
                        if (y < -DepthMetres) y = -2f * DepthMetres - y;
                    }

                    _y[i] = ClampY(y);
                }

                if (sh > 0f)
                {
                    _x[i] = WrapAxis(_x[i] + _rng.Gaussian() * sh, LengthMetres);
                    _z[i] = WrapAxis(_z[i] + _rng.Gaussian() * sh, WidthMetres);
                }
            }

            _gridBuilt = false;
        }

        /// <summary>
        /// Carries every vertex with the water — the velocity the bodies feel, at the vertex's
        /// own depth and patch, for one step. Nothing unless <see cref="CurrentField.AdvectFields"/>.
        /// </summary>
        public void Advect(CurrentField current, double seconds, float dt, float patchWidthMetres)
        {
            if (current == null || !current.AdvectFields || !(dt > 0f)) return;

            for (int i = 0; i < _y.Count; i++)
            {
                if (!_alive[i]) continue;
                Float3 v = current.VelocityAt(_y[i], seconds, PatchOf(_x[i]), PatchCount);
                if (v.X == 0f && v.Y == 0f && v.Z == 0f) continue;

                _x[i] = WrapAxis(_x[i] + v.X * dt, LengthMetres);
                _y[i] = ClampY(_y[i] + v.Y * dt);
                _z[i] = WrapAxis(_z[i] + v.Z * dt, WidthMetres);
            }

            _gridBuilt = false;
        }

        /// <summary>
        /// Emits vertices of <see cref="VertexJoules"/> for an influx: banks the joules and
        /// founds one vertex per whole quantum, uniformly inside the box
        /// <paramref name="centre"/> ± <paramref name="halfExtent"/>. Returns what was emitted.
        /// </summary>
        public float Emit(double joules, Float3 centre, Float3 halfExtent)
        {
            if (!(joules > 0d)) return 0f;
            _bank += joules;

            double emitted = 0.0;
            while (_bank >= VertexJoules)
            {
                float x = centre.X + (2f * _rng.NextFloat() - 1f) * halfExtent.X;
                float y = centre.Y + (2f * _rng.NextFloat() - 1f) * halfExtent.Y;
                float z = centre.Z + (2f * _rng.NextFloat() - 1f) * halfExtent.Z;
                Add(x, y, z, VertexJoules);
                _bank -= VertexJoules;
                emitted += VertexJoules;
            }

            return (float)emitted;
        }

        /// <summary>
        /// Removes each vertex resting on the floor with the given probability — D074's burial,
        /// one whole vertex at a time. Returns the joules that left the world.
        /// </summary>
        public double BuryFloor(double probability)
        {
            if (!(probability > 0d)) return 0.0;
            float p = probability >= 1d ? 1f : (float)probability;
            double buried = 0.0;

            for (int i = 0; i < _y.Count; i++)
            {
                if (!_alive[i] || !OnFloor(i)) continue;
                if (_rng.NextFloat() < p)
                {
                    buried += _m[i];
                    Kill(i);
                }
            }

            return buried;
        }

        private void Kill(int i)
        {
            _alive[i] = false;
            _m[i] = 0.0;
            Count--;
        }

        /// <summary>
        /// Drops vertices eaten to nothing; while the count is over <see cref="VertexCap"/>,
        /// merges nearest pairs from <see cref="MergeMetres"/> outward to the kernel; then
        /// compacts the store. Conserves mass to the last bit of the addition.
        /// </summary>
        public void Cull()
        {
            for (int i = 0; i < _m.Count; i++)
            {
                if (_alive[i] && !(_m[i] > 0.0)) { Kill(i); Emptied++; }
            }

            float radius = MergeMetres;
            bool merged = Count > VertexCap;

            while (merged && radius > 0f)
            {
                merged = false;
                BuildGrid();

                for (int i = 0; i < _x.Count; i++)
                {
                    if (!_alive[i]) continue;

                    Gather(new Float3(_x[i], _y[i], _z[i]), radius);

                    int best = -1;
                    double bestR = double.MaxValue;
                    for (int k = 0; k < _nb.Count; k++)
                    {
                        int j = _nb[k];
                        if (j == i || !_alive[j]) continue;
                        if (_nbW[k] < bestR) { bestR = _nbW[k]; best = j; }
                    }

                    if (best < 0) continue;

                    double total = _m[i] + _m[best];
                    if (total > 0.0)
                    {
                        double f = _m[best] / total;
                        _x[i] = WrapAxis(_x[i] + RingDelta(_x[i], _x[best], LengthMetres) * (float)f, LengthMetres);
                        _y[i] = ClampY(_y[i] + (_y[best] - _y[i]) * (float)f);
                        _z[i] = WrapAxis(_z[i] + RingDelta(_z[i], _z[best], WidthMetres) * (float)f, WidthMetres);
                    }

                    _m[i] = total;
                    Kill(best);
                    Merged++;
                    merged = true;
                }

                if (Count <= VertexCap) break;
                if (radius >= KernelMetres) { OverCapSteps++; break; }
                radius = Math.Min(KernelMetres, radius * 2f);
                merged = true;
            }

            Compact();
        }

        private void Compact()
        {
            int write = 0;
            for (int read = 0; read < _x.Count; read++)
            {
                if (!_alive[read]) continue;
                if (write != read)
                {
                    _x[write] = _x[read];
                    _y[write] = _y[read];
                    _z[write] = _z[read];
                    _m[write] = _m[read];
                    _alive[write] = true;
                    _demand[write] = _demand[read];
                    _available[write] = _available[read];
                }

                write++;
            }

            int remove = _x.Count - write;
            if (remove > 0)
            {
                _x.RemoveRange(write, remove);
                _y.RemoveRange(write, remove);
                _z.RemoveRange(write, remove);
                _m.RemoveRange(write, remove);
                _alive.RemoveRange(write, remove);
                _demand.RemoveRange(write, remove);
                _available.RemoveRange(write, remove);
                _next.RemoveRange(write, remove);
            }

            Count = write;
            _gridBuilt = false;
        }

        // ------------------------------------------------------------------ reads by layer and patch

        public double StockInLayer(int layer, int patch)
        {
            if (layer < 0 || layer >= LayerCount) return 0.0;
            if (patch < 0 || patch >= PatchCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(patch), patch, $"This field has {PatchCount} patch(es), indexed 0..{PatchCount - 1}.");
            }

            double sum = 0.0;
            for (int i = 0; i < _m.Count; i++)
            {
                if (_alive[i] && LayerOf(_y[i]) == layer && PatchOf(_x[i]) == patch) sum += _m[i];
            }

            return sum;
        }

        /// <summary>The pre-D061 signature: patch 0 of a one-patch field, a refusal otherwise.</summary>
        public double StockInLayer(int layer)
        {
            if (PatchCount > 1)
            {
                throw new InvalidOperationException(
                    $"This field has {PatchCount} patches, so a caller must say which one.");
            }

            return StockInLayer(layer, 0);
        }

        public float DensityAt(float heightY, int patch) =>
            (float)(StockInLayer(LayerOf(heightY), patch) / LayerVolume);

        public float EdibleDensityAt(float heightY, int patch)
        {
            int layer = LayerOf(heightY);
            double sum = 0.0;
            for (int i = 0; i < _m.Count; i++)
            {
                if (_alive[i] && LayerOf(_y[i]) == layer && PatchOf(_x[i]) == patch) sum += Edible(i) * _m[i];
            }

            return (float)(sum / LayerVolume);
        }

        /// <summary>The position of a living vertex, for tests and the theatre.</summary>
        public Float3 PositionOf(int index) => new Float3(_x[index], _y[index], _z[index]);

        /// <summary>The joules a living vertex holds, for tests and the theatre.</summary>
        public double JoulesOf(int index) => _m[index];

        /// <summary>Whether an index is a living vertex — false past <see cref="Count"/> or once culled.</summary>
        public bool IsAlive(int index) => index >= 0 && index < _alive.Count && _alive[index];

        /// <summary>Indices in the store, living and culled-but-not-yet-compacted alike.</summary>
        public int StoreLength => _x.Count;

        public override string ToString() =>
            FormattableString.Invariant(
                $"{TotalJoules:0} J on {Count} vertices, kernel {KernelMetres:0.##} m, sinking {SinkMetresPerSecond:0.###} m/s");
    }
}

using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Dead matter, or free matter, held as a 3D grid of cubic cells: the water of
    /// <c>fable-propose-grid.md</c>. Every cell is one number of joules, and a body feeds from
    /// the one cell it stands in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a grid and not the old cells.</b> <see cref="NutrientField"/> prices a body's water
    /// at one number per (layer, patch), and a patch is the whole 5 m by 5 m by 1 m slab. A body
    /// that stays there does not deplete its own water and a body that moves does not refresh it,
    /// so undirected movement earns nothing by construction and selection deletes the muscles
    /// (DESIGN §0r, research Q11). Cutting the same slab into metre cubes restores the thing that
    /// was missing: a still mouth eats a hole the size of its own cell, a moving mouth leaves the
    /// hole behind, and the density has an edge for the chemical sense to read.
    /// </para>
    /// <para>
    /// <b>Why a grid and not the vertices.</b> <see cref="VertexField"/> (D083) buys the same
    /// property with a set of moving samples, and pays for it with a hash grid, a kernel sum per
    /// read, a merge budget and a count that has to be watched. A fixed grid is an array index.
    /// It cannot collapse the way the vertices did on their first night (logbook/0074), it has no
    /// cap to run into, and its reads are exact rather than a quadrature that fluctuates as
    /// <c>1/√n</c> in the samples in reach. What it gives up is the sub-cell gradient: inside one
    /// cell the water is still one number, so the resolution is bought once, in
    /// <see cref="CellMetres"/>, and never adapts.
    /// </para>
    /// <para>
    /// <b>Amounts, not densities.</b> Each cell holds joules. Every operation is a transfer, so
    /// §5A.2's audit holds by construction and never has to trust an interpolation. The density
    /// at a point is the cell's stock over <see cref="CellVolume"/>.
    /// </para>
    /// <para>
    /// <b>Feeding is the cell field's own rule, one scale down.</b> Demand, frozen availability,
    /// share and take are per cell and word for word what <see cref="NutrientField"/> does (the
    /// Astra review's R1). A meal taken earlier in the walk cannot change the price of a meal
    /// taken later, and two feeders in one cell get what the demand pass promised them.
    /// </para>
    /// <para>
    /// <b>Transport is isotropic, and that is a ruling and not a convenience.</b> D084 settled
    /// that sideways mixing is the same as vertical mixing because the walk is the same in every
    /// direction. A cubic cell has no axis to prefer, so <see cref="Mix"/> takes one diffusivity
    /// and applies it to all six faces, and refuses a horizontal diffusivity that disagrees with
    /// it rather than quietly running an anisotropic world under a knob that says otherwise.
    /// </para>
    /// <para>
    /// <b>Geometry is D077's box</b>: x on a ring <see cref="PatchCount"/> patches long, z on a
    /// ring one patch wide, y from the floor at −<see cref="DepthMetres"/> to the waterline at 0.
    /// A patch is a range of x columns. The cell size must divide the box on all three axes, and
    /// the constructor refuses one that does not: a half cell at a seam would be a cell of a
    /// different volume, priced at the same volume as every other, which is the shape of an
    /// invisible subsidy.
    /// </para>
    /// </remarks>
    public sealed class GridField : IMatterField
    {
        private readonly double[] _stock;
        private readonly double[] _demand;
        private readonly double[] _available;

        // One flux buffer per axis, so a pass computes every face from one snapshot and applies
        // them afterwards. A single buffer would make the result depend on which end the loop
        // started at, which is the fault Settle's remarks in NutrientField describe.
        private readonly double[] _fluxX;
        private readonly double[] _fluxY;
        private readonly double[] _fluxZ;

        // Velocity is a function of depth and patch alone, never of the cell within a column, so
        // Advect samples the current once per (layer, patch) instead of once per face. At a
        // 24 by 24 by 6 box that is 96 samples a step rather than ten thousand.
        private readonly double[] _velocityX;
        private readonly double[] _velocityY;
        private readonly double[] _velocityZ;

        private readonly int[] _patchOfColumn;

        // Reused by DepositBox so a per-step influx allocates nothing.
        private readonly List<int> _boxCells = new List<int>();

        private readonly int _nx;
        private readonly int _ny;
        private readonly int _nz;
        private readonly int _layerStride;

        private bool _frozen;
        private double _total;

        /// <summary>Horizontal area of the world, m².</summary>
        public float WorldArea { get; }

        /// <summary>The side of one cubic cell, m.</summary>
        public float CellMetres { get; }

        /// <summary>
        /// Thickness of one layer, m. The cell's own side: a grid layer is one row of cells, and a
        /// second thickness would be a number nothing in here measures anything with.
        /// </summary>
        public float LayerMetres { get; }

        /// <summary>How fast detritus falls, m/s.</summary>
        public float SinkMetresPerSecond { get; }

        /// <summary>Layers in the world, floor last. The cells down one column.</summary>
        public int LayerCount { get; }

        /// <summary>Horizontal cells of D061, each a range of x columns.</summary>
        public int PatchCount { get; }

        /// <summary>One patch's side, m: <c>sqrt(WorldArea / PatchCount)</c>, and the box's z extent.</summary>
        public float PatchWidthMetres { get; }

        /// <summary>The box's depth, m.</summary>
        public float DepthMetres { get; }

        /// <summary>Layers, counted up from the floor, that no mouth can reach (D055).</summary>
        public int RefugeLayerCount { get; }

        /// <summary>Fraction of a refuge layer feeding can see and take, in [0, 1]. D055's arm C.</summary>
        public float RefugeEdibleFraction { get; }

        /// <summary>The ring's length along x, m: every patch side by side.</summary>
        public float LengthMetres => PatchWidthMetres * PatchCount;

        /// <summary>The box's extent along z, m: one patch.</summary>
        public float WidthMetres => PatchWidthMetres;

        /// <summary>Cells along x.</summary>
        public int CellsX => _nx;

        /// <summary>Cells along y, which is <see cref="LayerCount"/>.</summary>
        public int CellsY => _ny;

        /// <summary>Cells along z.</summary>
        public int CellsZ => _nz;

        /// <summary>Cells in the whole box.</summary>
        public int CellCount => _stock.Length;

        /// <summary>Volume of one cell, m³. The divisor of every density this field reports.</summary>
        public float CellVolume => CellMetres * CellMetres * CellMetres;

        /// <summary>
        /// Volume of one layer <i>in one patch</i>, m³: <c>WorldArea / PatchCount</c> times the
        /// cell. The same quantity <see cref="NutrientField.LayerVolume"/> and
        /// <see cref="VertexField.LayerVolume"/> report.
        /// </summary>
        /// <remarks>
        /// It has to mean the same thing on all three fields, because a caller holding an
        /// <see cref="IMatterField"/> divides <see cref="StockInLayer(int, int)"/> by it and gets a
        /// density; a whole-world slab here would make that read low by
        /// <see cref="PatchCount"/> on a grid and nowhere else. Nothing inside this class divides
        /// by it: a grid's own unit of water is the cell, so every density this field returns is a
        /// stock over <see cref="CellVolume"/>.
        /// </remarks>
        public float LayerVolume { get; }

        public GridField(
            float worldArea, float sinkMetresPerSecond, float worldDepth,
            float refugeMetres, float refugeEdibleFraction, int patchCount, float cellMetres)
        {
            if (!(worldArea > 0f) || float.IsInfinity(worldArea))
                throw new ArgumentOutOfRangeException(nameof(worldArea), worldArea, "Must be positive and finite.");
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
            if (!(cellMetres > 0f) || float.IsInfinity(cellMetres))
                throw new ArgumentOutOfRangeException(nameof(cellMetres), cellMetres, "Must be positive and finite.");

            WorldArea = worldArea;
            CellMetres = cellMetres;
            LayerMetres = cellMetres;
            SinkMetresPerSecond = sinkMetresPerSecond;
            DepthMetres = worldDepth;
            PatchCount = patchCount;
            PatchWidthMetres = (float)Math.Sqrt(worldArea / patchCount);

            _nx = (int)Math.Round(LengthMetres / cellMetres);
            _ny = (int)Math.Round(worldDepth / cellMetres);
            _nz = (int)Math.Round(WidthMetres / cellMetres);

            if (_nx < 1 || _ny < 1 || _nz < 1 ||
                Math.Abs(_nx * (double)cellMetres - LengthMetres) > 1e-4 ||
                Math.Abs(_ny * (double)cellMetres - worldDepth) > 1e-4 ||
                Math.Abs(_nz * (double)cellMetres - WidthMetres) > 1e-4)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A cell of {cellMetres} m does not divide the box, which is {LengthMetres} m ") +
                    FormattableString.Invariant(
                        $"long, {worldDepth} m deep and {WidthMetres} m wide. Pick a cell that divides ") +
                    "all three: a part cell at a seam holds less than a whole one and would be " +
                    "priced as a whole one.",
                    nameof(cellMetres));
            }

            LayerCount = _ny;
            LayerVolume = (worldArea / patchCount) * cellMetres;
            RefugeLayerCount = Math.Min(LayerCount, (int)Math.Ceiling(refugeMetres / cellMetres));
            RefugeEdibleFraction = refugeEdibleFraction;

            _layerStride = _nx * _nz;
            int cells = _layerStride * _ny;

            _stock = new double[cells];
            _demand = new double[cells];
            _available = new double[cells];
            _fluxX = new double[cells];
            _fluxY = new double[cells];
            _fluxZ = new double[cells];

            _velocityX = new double[_ny * PatchCount];
            _velocityY = new double[_ny * PatchCount];
            _velocityZ = new double[_ny * PatchCount];

            // A patch is a range of x columns, and the width constraint above guarantees the
            // ranges are whole: the box's z extent is one patch width, so a cell that divides the
            // width divides the patch. The map is built once from each column's own centre, which
            // is the only reading of "which patch is this column in" that cannot straddle a seam.
            _patchOfColumn = new int[_nx];
            for (int ix = 0; ix < _nx; ix++) _patchOfColumn[ix] = PatchOf((ix + 0.5f) * cellMetres);
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>The layer a world height falls in, clamped to the world.</summary>
        /// <remarks>
        /// Clamped rather than extended, for <see cref="NutrientField.LayerOf"/>'s reason: the
        /// pool has to be conserved, and a deposit at a depth with no layer would vanish, which
        /// is exactly what the audit exists to notice.
        /// </remarks>
        public int LayerOf(float heightY)
        {
            int layer = heightY >= 0f ? 0 : (int)(-heightY / CellMetres);
            if (layer < 0) return 0;
            return layer >= _ny ? _ny - 1 : layer;
        }

        /// <summary>The patch an x falls in on the ring, <c>floor(x / W) mod K</c>. D077's rule.</summary>
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

        private int Index(int ix, int iy, int iz) => (iy * _nx + ix) * _nz + iz;

        private int LayerOfCell(int cell) => cell / _layerStride;

        /// <summary>The cell a position falls in, wrapped on x and z and clamped on y.</summary>
        private int CellAt(Float3 p)
        {
            int ix = (int)(WrapAxis(p.X, LengthMetres) / CellMetres);
            if (ix >= _nx) ix = _nx - 1;
            if (ix < 0) ix = 0;

            int iz = (int)(WrapAxis(p.Z, WidthMetres) / CellMetres);
            if (iz >= _nz) iz = _nz - 1;
            if (iz < 0) iz = 0;

            int iy = p.Y >= 0f ? 0 : (int)(-p.Y / CellMetres);
            if (iy >= _ny) iy = _ny - 1;
            if (iy < 0) iy = 0;

            return Index(ix, iy, iz);
        }

        /// <summary>
        /// The position a point describes, or a refusal. A grid cell is a place, so a point that
        /// carries no place cannot be answered.
        /// </summary>
        /// <remarks>
        /// The same refusal <see cref="VertexField"/> makes and for the same reason: defaulting
        /// the missing coordinates would put every body at its patch's centre and call that a
        /// position, which is the cell field's coarseness wearing a grid's name.
        /// </remarks>
        private static Float3 Validated(FieldPoint at)
        {
            if (!at.HasHorizontal)
            {
                throw new InvalidOperationException(
                    "A grid field needs a position, not a depth and a patch: the point was made " +
                    "with FieldPoint.At, which carries no x or z.");
            }

            Float3 p = at.Position;
            if (!p.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(at), p, "A non-finite position.");
            }

            return p;
        }

        private void ValidatePatch(int patch)
        {
            if (patch < 0 || patch >= PatchCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(patch), patch,
                    $"This field has {PatchCount} patch(es), indexed 0..{PatchCount - 1}.");
            }
        }

        /// <summary>
        /// Patch 0 when there is one patch, a refusal otherwise. <see cref="NutrientField"/>'s
        /// pre-D061 rule, kept so the two fields answer the old signatures alike.
        /// </summary>
        private int SinglePatchOrThrow()
        {
            if (PatchCount > 1)
            {
                throw new InvalidOperationException(
                    $"This field has {PatchCount} patches (D061), so a caller must say which one.");
            }

            return 0;
        }

        /// <summary>
        /// The cell at a depth in a patch's centre column, which is what the depth-and-patch
        /// overloads address.
        /// </summary>
        /// <remarks>
        /// A patch is many columns now, so "deposit at this depth in this patch" has no single
        /// answer any more. The centre column is the choice <see cref="VertexField"/> already
        /// makes for the same overloads, so a tool that writes through the old address writes to
        /// the same place in both representations.
        /// </remarks>
        private int CentreCell(float heightY, int patch)
        {
            ValidatePatch(patch);
            return CellAt(new Float3((patch + 0.5f) * PatchWidthMetres, heightY, 0.5f * WidthMetres));
        }

        /// <summary>Whether a layer is buried beyond any mouth's reach (D055).</summary>
        public bool IsRefuge(int layer) => layer >= LayerCount - RefugeLayerCount;

        /// <summary>What a cell's stock feeding may currently see and take, J.</summary>
        private double Edible(int cell) =>
            IsRefuge(LayerOfCell(cell)) ? _stock[cell] * RefugeEdibleFraction : _stock[cell];

        // ------------------------------------------------------------------ totals

        /// <summary>Everything the field holds, J. Part of §5A.2's audit.</summary>
        /// <remarks>
        /// A running total rather than a sum over the cells, because a grid can be tens of
        /// thousands of cells and the world reads this several times a step. Every transfer pass
        /// in here is conservative face by face and so leaves it alone by construction; only a
        /// deposit or a take moves it. <see cref="Recount"/> re-sums the array and is what a test
        /// asserts the running figure against.
        /// </remarks>
        public double TotalJoules => _total;

        /// <summary>Re-sums the cells into the running total and returns it.</summary>
        public double Recount()
        {
            double sum = 0.0;
            for (int i = 0; i < _stock.Length; i++) sum += _stock[i];
            _total = sum;
            return sum;
        }

        // ------------------------------------------------------------------ seeding

        /// <summary>
        /// Fills the box uniformly at a density, in J/m³. The same signature and the same meaning
        /// as <see cref="VertexField.SeedUniform"/>, so <c>World</c> seeds both the same way.
        /// </summary>
        public void SeedUniform(float densityJoulesPerCubicMetre)
        {
            if (!(densityJoulesPerCubicMetre > 0f)) return;

            double each = (double)densityJoulesPerCubicMetre * CellVolume;
            for (int i = 0; i < _stock.Length; i++) _stock[i] += each;
            _total += each * _stock.Length;
        }

        /// <summary>
        /// Spreads an amount equally over the cells whose centres fall inside a box, and returns
        /// what was deposited. The grid's answer to <see cref="VertexField.Emit"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The vertex field founds whole quanta at random points in the plume and banks the
        /// remainder. A grid has nothing to found, so the plume simply lands on the cells it
        /// covers, and every joule handed in is in the world on the step it arrives. That is the
        /// simpler identity for <c>World.MatterInfluxedTotal</c> to hold: what is counted is
        /// exactly what was added.
        /// </para>
        /// <para>
        /// A box that covers no cell centre falls back to the one cell at its own centre, so a
        /// plume thinner than a cell still delivers rather than silently losing its influx. That
        /// case is not hypothetical: the surface influx box has zero half extent in y and no cell
        /// centre sits at the waterline.
        /// </para>
        /// </remarks>
        public double DepositBox(double joules, Float3 centre, Float3 halfExtent)
        {
            if (!(joules > 0d)) return 0d;

            _boxCells.Clear();
            for (int i = 0; i < _stock.Length; i++)
            {
                if (InsideBox(i, centre, halfExtent)) _boxCells.Add(i);
            }

            if (_boxCells.Count == 0)
            {
                int one = CellAt(centre);
                _stock[one] += joules;
                _total += joules;
                return joules;
            }

            double each = joules / _boxCells.Count;
            double deposited = 0.0;
            for (int i = 0; i < _boxCells.Count; i++)
            {
                _stock[_boxCells[i]] += each;
                deposited += each;
            }

            _total += deposited;
            return deposited;
        }

        private bool InsideBox(int cell, Float3 centre, Float3 halfExtent)
        {
            int iy = cell / _layerStride;
            int rest = cell - iy * _layerStride;
            int ix = rest / _nz;
            int iz = rest - ix * _nz;

            float cx = (ix + 0.5f) * CellMetres;
            float cy = -((iy + 0.5f) * CellMetres);
            float cz = (iz + 0.5f) * CellMetres;

            // x and z are rings, so "inside" is measured the shorter way round; y is not.
            return Math.Abs(RingDelta(WrapAxis(centre.X, LengthMetres), cx, LengthMetres)) <= halfExtent.X + 1e-4f
                && Math.Abs(cy - centre.Y) <= halfExtent.Y + 1e-4f
                && Math.Abs(RingDelta(WrapAxis(centre.Z, WidthMetres), cz, WidthMetres)) <= halfExtent.Z + 1e-4f;
        }

        private static float RingDelta(float from, float to, float extent)
        {
            float d = to - from;
            if (d > 0.5f * extent) d -= extent;
            else if (d < -0.5f * extent) d += extent;
            return d;
        }

        // ------------------------------------------------------------------ bodies and the water

        public void Deposit(FieldPoint at, float joules)
        {
            if (!(joules > 0f)) return;
            int cell = CellAt(Validated(at));
            _stock[cell] += joules;
            _total += joules;
        }

        /// <summary>A deposit by depth and patch, into the patch's centre column.</summary>
        public void Deposit(float heightY, float joules, int patch)
        {
            if (!(joules > 0f)) return;
            int cell = CentreCell(heightY, patch);
            _stock[cell] += joules;
            _total += joules;
        }

        /// <summary>The pre-D061 signature: patch 0 of a one-patch field, a refusal otherwise.</summary>
        public void Deposit(float heightY, float joules) => Deposit(heightY, joules, SinglePatchOrThrow());

        public float DensityAt(FieldPoint at) => (float)(_stock[CellAt(Validated(at))] / CellVolume);

        public float EdibleDensityAt(FieldPoint at) => (float)(Edible(CellAt(Validated(at))) / CellVolume);

        /// <summary>The cell's edible stock, J: what conception's matter gate compares a price against.</summary>
        public double ReachableStock(FieldPoint at) => Edible(CellAt(Validated(at)));

        public void ClearDemand()
        {
            Array.Clear(_demand, 0, _demand.Length);
            _frozen = false;
        }

        /// <summary>Registers what one creature would take in this cell if nothing competed.</summary>
        /// <remarks>
        /// Refuses a refuge layer outright at <see cref="RefugeEdibleFraction"/> zero, which is
        /// the invariant <see cref="NutrientField.Demand(float, float, int)"/> enforces so that no
        /// caller can forget the refuge by pricing the plain density.
        /// </remarks>
        public void Demand(FieldPoint at, float joules)
        {
            if (!(joules > 0f)) return;
            int cell = CellAt(Validated(at));
            if (IsRefuge(LayerOfCell(cell)) && RefugeEdibleFraction <= 0f) return;
            _demand[cell] += joules;
        }

        /// <summary>
        /// Fixes what every cell has to give for the consumption pass that follows.
        /// </summary>
        /// <remarks>
        /// The Astra review's R1, unchanged by the change of scale. Read from the live stock, two
        /// identical feeders wanting 10 J of a 10 J cell got 5 J and 1.25 J in admission order and
        /// left 3.75 J in the water with both still hungry. Frozen, each gets what the demand pass
        /// promised. Anything deposited during the pass raises the cap on a take and not what is
        /// shared out; it is food next step.
        /// </remarks>
        public void FreezeAvailability()
        {
            for (int i = 0; i < _stock.Length; i++) _available[i] = Edible(i);
            _frozen = true;
        }

        public float FrozenEdibleDensityAt(FieldPoint at)
        {
            if (!_frozen) throw new InvalidOperationException("FreezeAvailability has not been called this step.");
            return (float)(_available[CellAt(Validated(at))] / CellVolume);
        }

        public float ShareAt(FieldPoint at)
        {
            int cell = CellAt(Validated(at));
            double wanted = _demand[cell];
            if (wanted <= 0.0) return 1f;

            // Frozen for the pass when the world has frozen it, the live stock otherwise, which is
            // what a caller outside the world's step sees.
            double available = _frozen ? _available[cell] : Edible(cell);
            return available >= wanted ? 1f : (float)(available / wanted);
        }

        /// <summary>Removes energy from one cell and returns what was actually there to take.</summary>
        public float Take(FieldPoint at, float joules)
        {
            if (!(joules > 0f)) return 0f;

            int cell = CellAt(Validated(at));
            double cap = Edible(cell);
            double taken = Math.Min(joules, cap);
            if (taken <= 0.0) return 0f;

            _stock[cell] -= taken;
            _total -= taken;
            return (float)taken;
        }

        /// <summary>
        /// Removes up to <paramref name="wanted"/> from one layer of one patch and returns what
        /// was taken. What <c>World.BuryMatter</c> asks of a field.
        /// </summary>
        /// <remarks>
        /// A patch's layer is many cells here, so the draw is spread over them in proportion to
        /// what each holds. Taking it from a centre column instead would bury the same fraction of
        /// a much smaller pile and make burial depend on the cell size, which is a resolution knob
        /// and not a world rule.
        /// </remarks>
        public double TakeFromLayer(int layer, int patch, double wanted)
        {
            ValidatePatch(patch);
            if (layer < 0 || layer >= _ny) return 0d;
            if (!(wanted > 0d)) return 0d;

            double pool = 0.0;
            for (int ix = 0; ix < _nx; ix++)
            {
                if (_patchOfColumn[ix] != patch) continue;
                for (int iz = 0; iz < _nz; iz++) pool += Edible(Index(ix, layer, iz));
            }

            if (!(pool > 0d)) return 0d;

            double take = wanted < pool ? wanted : pool;
            double fraction = take / pool;
            double taken = 0.0;

            for (int ix = 0; ix < _nx; ix++)
            {
                if (_patchOfColumn[ix] != patch) continue;
                for (int iz = 0; iz < _nz; iz++)
                {
                    int cell = Index(ix, layer, iz);
                    double t = Edible(cell) * fraction;
                    if (t <= 0.0) continue;
                    _stock[cell] -= t;
                    taken += t;
                }
            }

            _total -= taken;
            return taken;
        }

        // ------------------------------------------------------------------ the water on its own

        /// <summary>
        /// Moves detritus down one cell's worth of sinking, independently in every column.
        /// </summary>
        /// <remarks>
        /// A fraction of each cell moves down rather than the whole cell moving a distance, which
        /// is <see cref="NutrientField.Settle"/>'s reasoning: with cells of fixed size a sink
        /// speed slower than one cell per step has nowhere else to go. The fraction is capped at a
        /// half rather than at 1, because a cell here has a face above and a face below and the
        /// same clamp that keeps <see cref="Advect"/> from emptying a cell twice over keeps this
        /// one honest at any step length. The floor keeps what reaches it.
        /// </remarks>
        public void Settle(float seconds)
        {
            if (SinkMetresPerSecond <= 0f || _ny < 2 || !(seconds > 0f)) return;

            double fraction = SinkMetresPerSecond * seconds / CellMetres;
            if (fraction <= 0.0) return;
            if (fraction > 0.5) fraction = 0.5;

            Array.Clear(_fluxY, 0, _fluxY.Length);

            for (int iy = 0; iy < _ny - 1; iy++)
            {
                for (int ix = 0; ix < _nx; ix++)
                {
                    for (int iz = 0; iz < _nz; iz++)
                    {
                        int cell = Index(ix, iy, iz);
                        _fluxY[cell] = _stock[cell] * fraction;
                    }
                }
            }

            ApplyDown(_fluxY);
        }

        /// <summary>
        /// Leaks a fraction of the floor's stock into the layer above it, D051's return leg.
        /// </summary>
        /// <remarks>
        /// Exact rather than a capped Euler step, as <see cref="NutrientField.Remineralise"/> is:
        /// the moved fraction is <c>1 - exp(-rate·seconds)</c>, so one call over 10 s and ten
        /// calls over 1 s move the same fraction. Measured redundant wherever mixing is on
        /// (logbook/0036), and on a grid that is more true than before: mixing here crosses the
        /// floor interface on every column rather than on every patch.
        /// </remarks>
        public void Remineralise(double seconds, float ratePerSecond)
        {
            if (!(ratePerSecond > 0f) || _ny < 2 || !(seconds > 0d)) return;

            double fraction = 1.0 - Math.Exp(-ratePerSecond * seconds);

            for (int ix = 0; ix < _nx; ix++)
            {
                for (int iz = 0; iz < _nz; iz++)
                {
                    int floor = Index(ix, _ny - 1, iz);
                    int above = Index(ix, _ny - 2, iz);
                    double moved = _stock[floor] * fraction;
                    _stock[floor] -= moved;
                    _stock[above] += moved;
                }
            }
        }

        /// <summary>
        /// Stirs stock between face neighbours on all six faces at one diffusivity: explicit
        /// Fick, D036 and D084.
        /// </summary>
        /// <param name="seconds">Interval to mix over.</param>
        /// <param name="diffusivity">Eddy diffusivity, m²/s, on every axis alike.</param>
        /// <param name="horizontalDiffusivity">
        /// D061's separate sideways rate. Accepted only at 0 or at
        /// <paramref name="diffusivity"/>, because a cubic cell has no axis to prefer.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>One rate, and a refusal rather than a quiet anisotropy.</b> D084 ruled that sideways
        /// mixing equals vertical mixing because the walk is the same in every direction. The cell
        /// field had two rates because its cells were 5 m across and 1 m tall and the two
        /// directions were genuinely different lengths. Here they are the same length, so a caller
        /// asking for two different rates is describing a world this field cannot build, and being
        /// told so is better than running it at one of the two.
        /// </para>
        /// <para>
        /// <b>Refused above the stability limit rather than clamped.</b> The cell field clamps its
        /// mixed fraction at a half, which is the right bound for a pass with two faces. Six faces
        /// need a sixth, and a clamp there would silently run a slower stir than the config asks
        /// for at exactly the settings a run is most likely to choose (0.2 m²/s, 1 m, 0.5 s is
        /// 0.1, and 1 s is 0.2, which is over). A clamp turns a config error into an unrecorded
        /// change of physics. The refusal names the three numbers so the fix is arithmetic.
        /// </para>
        /// <para>
        /// <b>Conservative by construction.</b> Every face is a flux subtracted from one cell and
        /// added to its neighbour, computed from one snapshot of the whole field and applied
        /// afterwards, so the result does not depend on the order of the walk and the total cannot
        /// move. The surface and the floor carry no flux; x and z wrap round D077's rings.
        /// </para>
        /// </remarks>
        public void Mix(float seconds, float diffusivity, float horizontalDiffusivity = 0f)
        {
            if (horizontalDiffusivity != 0f && horizontalDiffusivity != diffusivity)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A grid field mixes at one rate on every axis (D084), but was asked for ") +
                    FormattableString.Invariant(
                        $"{diffusivity} m²/s vertically and {horizontalDiffusivity} m²/s sideways. ") +
                    "Its cells are cubes, so there is no shorter axis for a slower rate to belong " +
                    "to. Set the horizontal diffusivity equal to the vertical one, or to 0.",
                    nameof(horizontalDiffusivity));
            }

            if (!(diffusivity > 0f) || !(seconds > 0f)) return;

            double fraction = (double)diffusivity * seconds / ((double)CellMetres * CellMetres);
            // At exactly a sixth a cell hands its whole stock to its six neighbours and keeps
            // nothing, which is the boundary of the scheme rather than a point inside it, so the
            // comparison is not strict.
            if (fraction >= 1.0 / 6.0)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"Explicit diffusion on a grid is stable below D·dt/cell² = 1/6, and ") +
                    FormattableString.Invariant(
                        $"{diffusivity} m²/s over {seconds} s in cells of {CellMetres} m is ") +
                    FormattableString.Invariant($"{fraction:0.####}. ") +
                    "A cell has six faces and could be asked for more than it holds. Shorten the " +
                    "step, widen the cell, or lower the diffusivity.",
                    nameof(diffusivity));
            }

            Array.Clear(_fluxX, 0, _fluxX.Length);
            Array.Clear(_fluxY, 0, _fluxY.Length);
            Array.Clear(_fluxZ, 0, _fluxZ.Length);

            for (int iy = 0; iy < _ny; iy++)
            {
                for (int ix = 0; ix < _nx; ix++)
                {
                    for (int iz = 0; iz < _nz; iz++)
                    {
                        int cell = Index(ix, iy, iz);

                        if (_nx >= 2)
                        {
                            int east = Index(ix + 1 == _nx ? 0 : ix + 1, iy, iz);
                            _fluxX[cell] = (_stock[cell] - _stock[east]) * fraction;
                        }

                        if (_nz >= 2)
                        {
                            int front = Index(ix, iy, iz + 1 == _nz ? 0 : iz + 1);
                            _fluxZ[cell] = (_stock[cell] - _stock[front]) * fraction;
                        }

                        if (iy < _ny - 1)
                        {
                            int below = Index(ix, iy + 1, iz);
                            _fluxY[cell] = (_stock[cell] - _stock[below]) * fraction;
                        }
                    }
                }
            }

            if (_nx >= 2) ApplyEast(_fluxX);
            if (_nz >= 2) ApplyFront(_fluxZ);
            ApplyDown(_fluxY);
        }

        /// <summary>
        /// Carries the stock with the water. D066's transport, upwind on each of the three axes.
        /// </summary>
        /// <param name="current">The flow, or null for none.</param>
        /// <param name="seconds">The world's clock, s.</param>
        /// <param name="dt">Step length, s.</param>
        /// <param name="patchWidthMetres">Unused here; the grid reads its own geometry.</param>
        /// <remarks>
        /// <para>
        /// <b>Upwind, and conservative by construction.</b> Each face takes a fraction
        /// <c>min(½, |v|·dt/cell)</c> of the upstream cell and gives all of it to the downstream
        /// one. The three axes run as three passes, each computed from a snapshot and applied
        /// afterwards, which is what keeps the half safe: a cell has two faces in a pass, so the
        /// most it can be asked for is its whole stock and nothing goes negative. Running all six
        /// faces from one snapshot would need a clamp at a sixth instead, and would slow the
        /// transport for a tidiness nothing is asking for.
        /// </para>
        /// <para>
        /// <b>The velocity is sampled once per layer and patch.</b> <see cref="CurrentField"/>
        /// is a function of depth, time and patch, so the x face at every column of a patch feels
        /// the same water. The face's own depth is used: cell centres for the horizontal passes,
        /// the interface depth for the vertical one, which is the staggered arrangement
        /// <see cref="NutrientField.Advect"/> already upwinds across.
        /// </para>
        /// <para>
        /// <b>Nothing leaves the world.</b> The vertical pass walks only interfaces between two
        /// real layers; x and z wrap. So the only way to lose stock here would be an arithmetic
        /// one.
        /// </para>
        /// </remarks>
        public void Advect(CurrentField current, double seconds, float dt, float patchWidthMetres)
        {
            if (current == null || !current.AdvectFields) return;
            if (!(dt > 0f)) return;

            int k = PatchCount;

            for (int iy = 0; iy < _ny; iy++)
            {
                float centreY = -((iy + 0.5f) * CellMetres);
                float interfaceY = -((iy + 1) * CellMetres);

                for (int patch = 0; patch < k; patch++)
                {
                    Float3 atCentre = current.VelocityAt(centreY, seconds, patch, k);
                    _velocityX[iy * k + patch] = atCentre.X;
                    _velocityZ[iy * k + patch] = atCentre.Z;
                    _velocityY[iy * k + patch] = iy < _ny - 1
                        ? current.VelocityAt(interfaceY, seconds, patch, k).Y
                        : 0d;
                }
            }

            if (_nx >= 2)
            {
                Array.Clear(_fluxX, 0, _fluxX.Length);

                for (int iy = 0; iy < _ny; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        double u = _velocityX[iy * k + _patchOfColumn[ix]];
                        if (u == 0d) continue;

                        double fraction = Math.Abs(u) * dt / CellMetres;
                        if (fraction > 0.5) fraction = 0.5;

                        int nextX = ix + 1 == _nx ? 0 : ix + 1;
                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = Index(ix, iy, iz);
                            int east = Index(nextX, iy, iz);
                            _fluxX[cell] = u > 0d ? _stock[cell] * fraction : -_stock[east] * fraction;
                        }
                    }
                }

                ApplyEast(_fluxX);
            }

            if (_ny >= 2)
            {
                Array.Clear(_fluxY, 0, _fluxY.Length);

                for (int iy = 0; iy < _ny - 1; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        double w = _velocityY[iy * k + _patchOfColumn[ix]];
                        if (w == 0d) continue;

                        double fraction = Math.Abs(w) * dt / CellMetres;
                        if (fraction > 0.5) fraction = 0.5;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int upper = Index(ix, iy, iz);
                            int lower = Index(ix, iy + 1, iz);

                            // Rising water carries what is below it up, sinking water carries what
                            // is above it down.
                            _fluxY[upper] = w > 0d ? -_stock[lower] * fraction : _stock[upper] * fraction;
                        }
                    }
                }

                ApplyDown(_fluxY);
            }

            if (_nz >= 2)
            {
                Array.Clear(_fluxZ, 0, _fluxZ.Length);

                for (int iy = 0; iy < _ny; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        double v = _velocityZ[iy * k + _patchOfColumn[ix]];
                        if (v == 0d) continue;

                        double fraction = Math.Abs(v) * dt / CellMetres;
                        if (fraction > 0.5) fraction = 0.5;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = Index(ix, iy, iz);
                            int front = Index(ix, iy, iz + 1 == _nz ? 0 : iz + 1);
                            _fluxZ[cell] = v > 0d ? _stock[cell] * fraction : -_stock[front] * fraction;
                        }
                    }
                }

                ApplyFront(_fluxZ);
            }
        }

        /// <summary>Nothing to merge and nothing to drop: a cell is a cell.</summary>
        public void Cull() { }

        private void ApplyEast(double[] flux)
        {
            for (int iy = 0; iy < _ny; iy++)
            {
                for (int ix = 0; ix < _nx; ix++)
                {
                    int nextX = ix + 1 == _nx ? 0 : ix + 1;
                    for (int iz = 0; iz < _nz; iz++)
                    {
                        int cell = Index(ix, iy, iz);
                        double f = flux[cell];
                        if (f == 0d) continue;
                        _stock[cell] -= f;
                        _stock[Index(nextX, iy, iz)] += f;
                    }
                }
            }
        }

        private void ApplyFront(double[] flux)
        {
            for (int iy = 0; iy < _ny; iy++)
            {
                for (int ix = 0; ix < _nx; ix++)
                {
                    for (int iz = 0; iz < _nz; iz++)
                    {
                        int cell = Index(ix, iy, iz);
                        double f = flux[cell];
                        if (f == 0d) continue;
                        _stock[cell] -= f;
                        _stock[Index(ix, iy, iz + 1 == _nz ? 0 : iz + 1)] += f;
                    }
                }
            }
        }

        private void ApplyDown(double[] flux)
        {
            for (int iy = 0; iy < _ny - 1; iy++)
            {
                for (int ix = 0; ix < _nx; ix++)
                {
                    for (int iz = 0; iz < _nz; iz++)
                    {
                        int cell = Index(ix, iy, iz);
                        double f = flux[cell];
                        if (f == 0d) continue;
                        _stock[cell] -= f;
                        _stock[Index(ix, iy + 1, iz)] += f;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ reads by layer and patch

        /// <summary>What one layer holds across a whole patch, J: every column of it.</summary>
        public double StockInLayer(int layer, int patch)
        {
            if (layer < 0 || layer >= _ny) return 0.0;
            ValidatePatch(patch);

            double sum = 0.0;
            for (int ix = 0; ix < _nx; ix++)
            {
                if (_patchOfColumn[ix] != patch) continue;
                for (int iz = 0; iz < _nz; iz++) sum += _stock[Index(ix, layer, iz)];
            }

            return sum;
        }

        /// <summary>The pre-D061 signature: patch 0 of a one-patch field, a refusal otherwise.</summary>
        public double StockInLayer(int layer) => StockInLayer(layer, SinglePatchOrThrow());

        /// <summary>The density in a patch's centre column at this depth, J/m³.</summary>
        public float DensityAt(float heightY, int patch) => (float)(_stock[CentreCell(heightY, patch)] / CellVolume);

        /// <summary>The pre-D061 signature: patch 0 of a one-patch field, a refusal otherwise.</summary>
        public float DensityAt(float heightY) => DensityAt(heightY, SinglePatchOrThrow());

        /// <summary>The edible density in a patch's centre column at this depth, J/m³.</summary>
        public float EdibleDensityAt(float heightY, int patch) => (float)(Edible(CentreCell(heightY, patch)) / CellVolume);

        /// <summary>The pre-D061 signature: patch 0 of a one-patch field, a refusal otherwise.</summary>
        public float EdibleDensityAt(float heightY) => EdibleDensityAt(heightY, SinglePatchOrThrow());

        /// <summary>What one cell holds, J, by index on the three axes. For tests and the theatre.</summary>
        public double JoulesAt(int ix, int iy, int iz) => _stock[Index(ix, iy, iz)];

        /// <summary>The cell a position falls in, as its three indices. For tests and the theatre.</summary>
        public (int X, int Y, int Z) CellIndicesAt(Float3 position)
        {
            int cell = CellAt(position);
            int iy = cell / _layerStride;
            int rest = cell - iy * _layerStride;
            return (rest / _nz, iy, rest % _nz);
        }

        public override string ToString() =>
            FormattableString.Invariant(
                $"{TotalJoules:0} J on {_nx}x{_ny}x{_nz} cells of {CellMetres:0.##} m, sinking {SinkMetresPerSecond:0.###} m/s");
    }
}

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
    /// <b>Geometry is D077's box</b>, laid out as fable-propose-box.md says: x on a ring
    /// <see cref="PatchesAlong"/> patches long, z on a ring <see cref="PatchesAcross"/> patches
    /// wide, y from the floor at −<see cref="DepthMetres"/> to the waterline at 0. At A = 1 that
    /// is the row of patches this field has always held.
    /// A patch is a block of columns. The cell size must divide the box on all three axes, and
    /// the constructor refuses one that does not: a half cell at a seam would be a cell of a
    /// different volume, priced at the same volume as every other, which is the shape of an
    /// invisible subsidy.
    /// </para>
    /// <para>
    /// <b>In a <see cref="WorldShape.Tank"/> the array is the same and a mask decides what is
    /// water</b> — <c>logbook/specs/tank-spec.md</c>. The cells span the bounding square
    /// <c>[0, 2R)²</c> and a cell is live when its own centre is inside the circle; every face
    /// between a live cell and a dead one is glass, and so is the array's edge, so
    /// <see cref="Mix"/> and <see cref="Advect"/> move nothing across either and neither ever
    /// wraps. Seeding, the totals, the refuge sums and the per-patch sums run over live cells
    /// alone, and a patch is a ring of equal area rather than a block of columns
    /// (<see cref="TankGeometry"/>). The tank's diameter need not be a whole number of cells,
    /// because everything past it is dead: what the box's divisibility rule protects — a part cell
    /// priced as a whole one — is protected by the mask instead, and the depth is still refused
    /// unless it divides.
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

        // CurrentMode.Transport's field is a function of a place, so it is sampled per cell rather
        // than per layer and patch. Allocated on the first transport step - see
        // EnsureCellVelocities.
        private double[] _cellVelocityX;
        private double[] _cellVelocityY;
        private double[] _cellVelocityZ;

        // The conservative route's two sets of buffers - logbook/specs/transport-conserves-spec.md.
        // The edges carry A.(edge direction).h for each of the three edge families of the lattice,
        // and the faces carry the volumetric flux through each cell's east, lower and front face,
        // m^3/s, assembled as a circulation of the edges round that face. Allocated on the first
        // step that takes the route, so a rolls world carries exactly the memory it did before.
        private double[] _edgeX;
        private double[] _edgeY;
        private double[] _edgeZ;
        private double[] _faceX;
        private double[] _faceY;
        private double[] _faceZ;

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

        /// <summary>Horizontal cells of D061, each a block of columns.</summary>
        public int PatchCount { get; }

        /// <summary>One patch's side, m: <c>sqrt(WorldArea / PatchCount)</c>, on both horizontal axes.</summary>
        public float PatchWidthMetres { get; }

        /// <summary>The box's depth, m.</summary>
        public float DepthMetres { get; }

        /// <summary>Layers, counted up from the floor, that no mouth can reach (D055).</summary>
        public int RefugeLayerCount { get; }

        /// <summary>Fraction of a refuge layer feeding can see and take, in [0, 1]. D055's arm C.</summary>
        public float RefugeEdibleFraction { get; }

        /// <summary>
        /// How many patches lie across z — <see cref="RunConfig.PatchesAcross"/>, A. 1 is the
        /// row of patches every run on file was measured in.
        /// </summary>
        public int PatchesAcross { get; }

        /// <summary>Patches along x, <c>K / A</c>. The constructor refuses an A that leaves a remainder.</summary>
        public int PatchesAlong { get; }

        /// <summary>
        /// The box's length along x, m: <c>W · K / A</c>, and the tank's diameter <c>2R</c>.
        /// </summary>
        public float LengthMetres =>
            Shape == WorldShape.Tank ? 2f * TankRadiusMetres : PatchWidthMetres * PatchesAlong;

        /// <summary>
        /// The box's extent along z, m: <c>W · A</c>, and the tank's diameter <c>2R</c>.
        /// </summary>
        public float WidthMetres =>
            Shape == WorldShape.Tank ? 2f * TankRadiusMetres : PatchWidthMetres * PatchesAcross;

        /// <summary>
        /// The container — <see cref="RunConfig.WorldShape"/>. <see cref="WorldShape.Box"/> is
        /// every run on file, at which there is no mask and every face is the face it always was.
        /// </summary>
        public WorldShape Shape { get; }

        /// <summary>The tank's radius, m — 0 in a box. <see cref="TankGeometry"/>.</summary>
        public float TankRadiusMetres { get; }

        /// <summary>
        /// Cells the mask calls live: the whole array in a box, and the cells whose centres lie
        /// inside the circle in a tank.
        /// </summary>
        public int LiveCellCount { get; }

        /// <summary>The water this field actually holds, m³ — <see cref="LiveCellCount"/> cells.</summary>
        /// <remarks>
        /// What <see cref="RunConfig.MatterBudgetUnits"/> divides a total by, so that a budget is
        /// the number asked for whatever the circle cuts off the corners of the array. In a box it
        /// is the box's own volume to the last bit, because the cell size is refused unless it
        /// divides all three axes.
        /// </remarks>
        public double LiveVolumeCubicMetres => (double)LiveCellCount * CellVolume;

        /// <summary>Whether the cell at these indices is water. Always true in a box.</summary>
        public bool IsLive(int ix, int iy, int iz) => _live == null || _live[Index(ix, iy, iz)];

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

        /// <summary>
        /// Builds the water. <c>shape</c> is <see cref="RunConfig.WorldShape"/> — a tank is this
        /// same array over the bounding square with a mask on it, see <see cref="IsLive"/> and the
        /// class remarks — and <c>tankRadiusMetres</c> is <c>sqrt(area/π)</c> when the shape is a
        /// tank and unread otherwise.
        /// </summary>
        /// <remarks>
        /// The radius is handed in rather than derived here, so that the world, the water, the
        /// current and the placer all read one radius (<see cref="TankGeometry.RadiusFor"/>).
        /// Written as prose rather than as two <c>param</c> tags because the other eight arguments
        /// carry none, and a half-documented signature is a compiler warning per undocumented
        /// argument.
        /// </remarks>
        public GridField(
            float worldArea, float sinkMetresPerSecond, float worldDepth,
            float refugeMetres, float refugeEdibleFraction, int patchCount, float cellMetres,
            int patchesAcross = 1, WorldShape shape = WorldShape.Box, float tankRadiusMetres = 0f)
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
            if (patchesAcross < 1)
                throw new ArgumentOutOfRangeException(nameof(patchesAcross), patchesAcross, "A box is at least one patch wide.");
            if (patchCount % patchesAcross != 0)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"{patchesAcross} patches across do not divide {patchCount} patches, so ") +
                    "the layout leaves a part row. Pick a divisor: four patches lie one by four " +
                    "or two by two, and nine lie one by nine or three by three.",
                    nameof(patchesAcross));
            }

            if (shape == WorldShape.Tank && (!(tankRadiusMetres > 0f) || float.IsInfinity(tankRadiusMetres)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tankRadiusMetres), tankRadiusMetres,
                    "A tank's water is a disc, so its radius is positive and finite. " +
                    "logbook/specs/tank-spec.md.");
            }

            WorldArea = worldArea;
            CellMetres = cellMetres;
            LayerMetres = cellMetres;
            SinkMetresPerSecond = sinkMetresPerSecond;
            DepthMetres = worldDepth;
            PatchCount = patchCount;
            PatchesAcross = patchesAcross;
            PatchesAlong = patchCount / patchesAcross;
            PatchWidthMetres = (float)Math.Sqrt(worldArea / patchCount);
            Shape = shape;
            TankRadiusMetres = shape == WorldShape.Tank ? tankRadiusMetres : 0f;

            _ny = (int)Math.Round(worldDepth / cellMetres);

            if (shape == WorldShape.Tank)
            {
                // The array spans the bounding square, which a cell need not divide: the tank's
                // diameter is 2*sqrt(area/pi) and nothing makes that a whole number of cells. The
                // part of the array past the diameter is entirely outside the circle and therefore
                // entirely dead, so no live cell is ever a part cell and the divisibility rule the
                // box needs — a part cell holds less than a whole one and would be priced as a
                // whole one — is kept where it matters. The depth is still refused, because a
                // layer is a row of cells and a part layer would be a part-priced layer.
                _nx = (int)Math.Ceiling(LengthMetres / cellMetres - 1e-6);
                _nz = _nx;

                if (_nx < 1 || _ny < 1 || Math.Abs(_ny * (double)cellMetres - worldDepth) > 1e-4)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"A cell of {cellMetres} m does not fit a tank {LengthMetres} m across ") +
                        FormattableString.Invariant($"and {worldDepth} m deep. ") +
                        "The depth must be a whole number of cells, and the tank must be at least " +
                        "one cell across.",
                        nameof(cellMetres));
                }
            }
            else
            {
                _nx = (int)Math.Round(LengthMetres / cellMetres);
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

            // A patch is a block of columns, and the divisibility above guarantees the blocks are
            // whole: the box is a whole number of patches on both horizontal axes and a cell
            // divides both, so a cell that divides the box divides the patch. The map is built
            // once from each column's own centre, which is the only reading of "which patch is
            // this column in" that cannot straddle a seam. It is indexed by the column rather
            // than by x alone because at A > 1 the patch is a function of z as well
            // (fable-propose-box.md clause 3); at A = 1 every z in a column answers the same and
            // the map is the one this field has always held, repeated down the z axis.
            _patchOfColumn = new int[_nx * _nz];
            for (int ix = 0; ix < _nx; ix++)
            {
                for (int iz = 0; iz < _nz; iz++)
                {
                    _patchOfColumn[ix * _nz + iz] =
                        PatchOf((ix + 0.5f) * cellMetres, (iz + 0.5f) * cellMetres);
                }
            }

            LiveCellCount = cells;

            if (shape != WorldShape.Tank) return;

            // The mask, built once — logbook/specs/tank-spec.md. A cell is live when its own
            // centre is inside the circle, which is the only test that cannot make a cell half
            // alive: a cell straddling the glass is either in the water or it is not, and a
            // fractional one would hold less than a whole cell's worth while being priced as a
            // whole cell, which is exactly what the box's divisibility rule exists to prevent.
            // What it costs is a rim of about a cell's width where the water's edge and the
            // glass's are not the same line; the live-cell count is therefore pi*R^2/cell^2 to
            // within one ring of cells, and the tests say so rather than the geometry claiming it.
            _live = new bool[cells];
            int live = 0;

            for (int ix = 0; ix < _nx; ix++)
            {
                float cx = (ix + 0.5f) * cellMetres;

                for (int iz = 0; iz < _nz; iz++)
                {
                    float cz = (iz + 0.5f) * cellMetres;
                    bool inside = TankGeometry.Inside(cx, cz, tankRadiusMetres);

                    // A dead column belongs to no patch. -1 rather than a ring index, so that
                    // every per-patch sum — StockInLayer, TakeFromLayer, the roll's velocity
                    // lookup — passes over it without needing to know about the mask.
                    if (!inside) _patchOfColumn[ix * _nz + iz] = -1;

                    for (int iy = 0; iy < _ny; iy++)
                    {
                        if (!inside) continue;
                        _live[Index(ix, iy, iz)] = true;
                        live++;
                    }
                }
            }

            if (live == 0)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"No cell of {cellMetres} m has its centre inside a tank of radius ") +
                    FormattableString.Invariant($"{tankRadiusMetres} m, so the water has no cells ") +
                    "in it at all. Use a cell smaller than the radius.",
                    nameof(cellMetres));
            }

            LiveCellCount = live;
        }

        /// <summary>
        /// Which cells are water: null in a box, where every cell is — see the class remarks.
        /// </summary>
        private readonly bool[] _live;

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

        /// <summary>
        /// The patch a horizontal position falls in: <c>iz · (K / A) + ix</c>, numbered along x
        /// first. D077's rule as fable-propose-box.md's clause 3 generalises it.
        /// </summary>
        /// <remarks>
        /// At A = 1 the row index is 0 and this is <c>floor(x / W) mod K</c> term for term, which
        /// is the arithmetic every recorded run's per-patch bins were filled by.
        /// </remarks>
        /// <remarks>
        /// <b>In a tank a patch is a ring of equal area</b> about the axis at <c>(R, R)</c> —
        /// <see cref="TankGeometry.RingOf"/> — so the per-patch bins read centre to rim and stay
        /// comparable in area, which is the whole reason a bin is worth having.
        /// </remarks>
        public int PatchOf(float x, float z)
        {
            if (Shape == WorldShape.Tank)
            {
                return TankGeometry.RingOf(x, z, TankRadiusMetres, PatchCount);
            }

            int along = PatchesAlong;

            int ix = (int)Math.Floor(WrapAxis(x, LengthMetres) / PatchWidthMetres);
            ix %= along;
            if (ix < 0) ix += along;

            if (PatchesAcross == 1) return ix;

            int iz = (int)Math.Floor(WrapAxis(z, WidthMetres) / PatchWidthMetres);
            iz %= PatchesAcross;
            if (iz < 0) iz += PatchesAcross;

            return iz * along + ix;
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

        /// <summary>
        /// The cell across this one's east face, or −1 when that face is a wall.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A box has no walls and a tank is nothing but.</b> In a box the x axis is a ring, so
        /// the face past the last column is the first column's and this is the wrap every
        /// recorded run stirred and advected across. In a tank the array's edge is a wall, and so
        /// is every face between a live cell and a dead one: the mask is the glass, and water that
        /// crossed it would be water leaving the tank.
        /// </para>
        /// <para>
        /// <b>The flux at a closed face is left at zero rather than skipped downstream</b>, which
        /// is why <see cref="ApplyEast"/> and <see cref="ApplyFront"/> need no mask of their own:
        /// every pass clears its buffer first and only an open face ever writes to it, so the
        /// apply walks the same loop it always did and moves nothing across a wall.
        /// </para>
        /// </remarks>
        private int EastOf(int ix, int iy, int iz)
        {
            int next = ix + 1;

            if (next == _nx)
            {
                if (Shape == WorldShape.Tank) return -1;
                next = 0;
            }

            int east = Index(next, iy, iz);
            if (_live != null && !_live[east]) return -1;

            return east;
        }

        /// <summary>The cell across this one's front face, or −1 when that face is a wall.</summary>
        private int FrontOf(int ix, int iy, int iz)
        {
            int next = iz + 1;

            if (next == _nz)
            {
                if (Shape == WorldShape.Tank) return -1;
                next = 0;
            }

            int front = Index(ix, iy, next);
            if (_live != null && !_live[front]) return -1;

            return front;
        }

        /// <summary>The cell a position falls in, wrapped on x and z and clamped on y.</summary>
        private int CellAt(Float3 p)
        {
            if (Shape == WorldShape.Tank) return TankCellAt(p);

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
        /// The cell a position falls in inside a tank, walking toward the axis until it finds
        /// water.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Nothing in the world is outside the glass, and float error at the wall is.</b> A
        /// body is stopped by a static collider and a corpse drifts on a field with no radial flow
        /// at the rim, so a point past the circle is arithmetic rather than an event — a
        /// depenetration of a centimetre, a deposit at the exact radius, a cell centre a
        /// millimetre outside its own mask. Refusing it would take a run down over a rounding;
        /// clamping it to the array's edge would put that stock in a dead cell, where it would sit
        /// for the rest of the run out of every sum and quietly break the audit. So the point is
        /// walked in along its own radius, half a cell at a time, until it lands in water — the
        /// nearest water in the direction that exists, and the axis at worst.
        /// </para>
        /// <para>
        /// <b>No wrap, on either axis.</b> A tank has a wall where the box has a seam, so folding
        /// a coordinate would put a body at the far rim of the same tank.
        /// </para>
        /// </remarks>
        private int TankCellAt(Float3 p)
        {
            int iy = p.Y >= 0f ? 0 : (int)(-p.Y / CellMetres);
            if (iy >= _ny) iy = _ny - 1;
            if (iy < 0) iy = 0;

            double dx = p.X - TankRadiusMetres;
            double dz = p.Z - TankRadiusMetres;
            double r = Math.Sqrt(dx * dx + dz * dz);

            if (r > 0d)
            {
                for (double reach = r; reach > 0d; reach -= 0.5d * CellMetres)
                {
                    int cell = Index(
                        Column((float)(TankRadiusMetres + dx * reach / r)), iy,
                        Column((float)(TankRadiusMetres + dz * reach / r)));

                    if (_live[cell]) return cell;
                }
            }

            int axis = Index(Column(TankRadiusMetres), iy, Column(TankRadiusMetres));
            if (_live[axis]) return axis;

            // The cell holding the axis is live in any tank wider than a cell and a half, which is
            // every tank a round would run; the scan is here so that a small one answers rather
            // than putting stock in a dead cell, where it would sit outside every sum for the rest
            // of the run and break the audit quietly.
            for (int i = iy * _layerStride; i < (iy + 1) * _layerStride; i++)
            {
                if (_live[i]) return i;
            }

            return axis;
        }

        /// <summary>
        /// One horizontal index, clamped to the array. The tank's, which never wraps — and where
        /// the two horizontal counts are equal by construction, so one clamp serves both axes.
        /// </summary>
        private int Column(float v)
        {
            int i = (int)(v / CellMetres);
            if (i >= _nx) return _nx - 1;
            return i < 0 ? 0 : i;
        }

        /// <summary>
        /// The position a point describes, or a refusal. A grid cell is a place, so a point that
        /// carries no place cannot be answered.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The same refusal <see cref="VertexField"/> makes and for the same reason: defaulting
        /// the missing coordinates would put every body at its patch's centre and call that a
        /// position, which is the cell field's coarseness wearing a grid's name.
        /// </para>
        /// <para>
        /// <b>Two refusals, and the difference is the whole diagnosis.</b> Until 2026-09-10 this
        /// asked <c>HasHorizontal</c> first, which is false for NaN, so a caller that handed over
        /// a diverged part's transform was told the point "was made with FieldPoint.At" and the
        /// next reader went looking for a call nobody had written. That is how
        /// <c>r35old-s3</c> was read for half an hour: the real fault was a part the solver had
        /// lost reaching <c>CreatureSensors.Sample</c> before the harness noticed. The point now
        /// says which constructor made it (<see cref="FieldPoint.DepthAndPatchOnly"/>), so a NaN
        /// position is named as one.
        /// </para>
        /// </remarks>
        private static Float3 Validated(FieldPoint at)
        {
            if (at.DepthAndPatchOnly)
            {
                throw new InvalidOperationException(
                    "A grid field needs a position, not a depth and a patch: the point was made " +
                    "with FieldPoint.At, which carries no x or z.");
            }

            Float3 p = at.Position;
            if (!p.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(at), p,
                    "A non-finite position: this point was built from a real coordinate that has " +
                    "stopped being finite, not with FieldPoint.At. The body it came from has " +
                    "diverged and should have been killed before it was asked where it was.");
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
        /// <remarks>
        /// <b>In a tank it is the cell at the ring's mid-radius on the <c>θ = 0</c> ray from the
        /// axis</b> — <see cref="TankGeometry.MidRadiusOf"/>. A ring has no centre column and no
        /// preferred direction, so a ray has to be picked; <c>θ = 0</c> is the one every other
        /// reader of this geometry picks (<see cref="CurrentField"/>'s patch-centre sampler), and
        /// picking the same one is what keeps the density a run reports and the water it samples
        /// describing the same place.
        /// </remarks>
        private int CentreCell(float heightY, int patch)
        {
            ValidatePatch(patch);

            if (Shape == WorldShape.Tank)
            {
                return CellAt(new Float3(
                    TankRadiusMetres + TankGeometry.MidRadiusOf(patch, TankRadiusMetres, PatchCount),
                    heightY,
                    TankRadiusMetres));
            }

            // The patch's own centre on both axes. At A = 1 the row is 0 and the second
            // coordinate is half the box's width, which is what this line always read.
            return CellAt(new Float3(
                (patch % PatchesAlong + 0.5f) * PatchWidthMetres,
                heightY,
                (patch / PatchesAlong + 0.5f) * PatchWidthMetres));
        }

        /// <summary>Which patch a column of cells stands in — the map built in the constructor.</summary>
        private int PatchOfColumn(int ix, int iz) => _patchOfColumn[ix * _nz + iz];

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
        /// <remarks>
        /// Over the live cells only, so a tank is seeded at the density asked for and holds
        /// <c>density × <see cref="LiveVolumeCubicMetres"/></c> rather than the bounding square's
        /// worth. Stock put in a dead cell could never be reached, mixed or advected, and would
        /// sit outside every sum for the life of the run.
        /// </remarks>
        public void SeedUniform(float densityJoulesPerCubicMetre)
        {
            if (!(densityJoulesPerCubicMetre > 0f)) return;

            double each = (double)densityJoulesPerCubicMetre * CellVolume;

            if (_live == null)
            {
                for (int i = 0; i < _stock.Length; i++) _stock[i] += each;
                _total += each * _stock.Length;
                return;
            }

            for (int i = 0; i < _stock.Length; i++)
            {
                if (_live[i]) _stock[i] += each;
            }

            _total += each * LiveCellCount;
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
                // Live cells only: an influx that landed in a dead cell would be counted as
                // arriving and could never be reached again.
                if (_live != null && !_live[i]) continue;
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

            // A tank has a wall where the box has a seam, so nothing is measured the way round;
            // in a box x and z are rings and "inside" is the shorter arc, while y never is.
            if (Shape == WorldShape.Tank)
            {
                return Math.Abs(cx - centre.X) <= halfExtent.X + 1e-4f
                    && Math.Abs(cy - centre.Y) <= halfExtent.Y + 1e-4f
                    && Math.Abs(cz - centre.Z) <= halfExtent.Z + 1e-4f;
            }

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
                for (int iz = 0; iz < _nz; iz++)
                {
                    if (PatchOfColumn(ix, iz) != patch) continue;
                    pool += Edible(Index(ix, layer, iz));
                }
            }

            if (!(pool > 0d)) return 0d;

            double take = wanted < pool ? wanted : pool;
            double fraction = take / pool;
            double taken = 0.0;

            for (int ix = 0; ix < _nx; ix++)
            {
                for (int iz = 0; iz < _nz; iz++)
                {
                    if (PatchOfColumn(ix, iz) != patch) continue;

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
                        if (_live != null && !_live[cell]) continue;

                        if (_nx >= 2)
                        {
                            int east = EastOf(ix, iy, iz);
                            if (east >= 0) _fluxX[cell] = (_stock[cell] - _stock[east]) * fraction;
                        }

                        if (_nz >= 2)
                        {
                            int front = FrontOf(ix, iy, iz);
                            if (front >= 0) _fluxZ[cell] = (_stock[cell] - _stock[front]) * fraction;
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
        /// <b>The velocity is sampled once per layer and patch under
        /// <see cref="CurrentMode.Rolls"/>.</b> That field is a function of depth, time and patch,
        /// so the x face at every column of a patch feels the same water. The face's own depth is
        /// used: cell centres for the horizontal passes, the interface depth for the vertical one,
        /// which is the staggered arrangement <see cref="NutrientField.Advect"/> already upwinds
        /// across.
        /// </para>
        /// <para>
        /// <b>Under <see cref="CurrentMode.Transport"/> it is sampled once per cell.</b> That field
        /// is a function of a place, and a per-layer sample would throw away exactly the structure
        /// it exists to have: two cells in one patch at one depth would be handed the same water,
        /// which is the coarseness that made a clade drain the same cells for a whole run
        /// (logbook/0083). The same two depths are used, so the pass shape is unchanged.
        /// </para>
        /// <para>
        /// <b>The cost, stated.</b> Two samples per cell per advection step, and the advection
        /// step is the metabolic step and not the physics step, so a 20 by 60 by 5 m box on metre
        /// cells is 12,000 samples every half second of world time for the detritus grid, and a
        /// twenty-fifth of that for the matter grid on 5 m cells. A sample is five Fourier modes,
        /// four trigonometric calls each. It is not cached across the step because there is nothing
        /// to cache: the clock has moved by the time it is called again, and the field is a
        /// function of the clock.
        /// </para>
        /// <para>
        /// <b>The Courant check is against the field's maximum, not its RMS.</b>
        /// <see cref="CurrentField.Speed"/> is an average over the whole box under
        /// <see cref="CurrentMode.Transport"/>, and an average says nothing about the fastest
        /// water, which is where a scheme comes apart. The per-face fraction is still clamped at a
        /// half so that conservation and non-negativity hold whatever anyone configures, but a
        /// world whose fastest water would cross more than half a cell in a step is refused rather
        /// than quietly run at a transport slower than the config asks for, which is
        /// <see cref="Mix"/>'s ruling applied to the same arithmetic.
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

            // A field of a place: the transport field in a box, and the streams in a tank
            // whatever the mode says (CurrentField.Shape). D037's rolls are a field of depth,
            // time and patch and take the route below, unchanged.
            bool ofAPlace = current.Shape == WorldShape.Tank || current.Mode == CurrentMode.Transport;

            if (ofAPlace && !current.HasPotential)
            {
                // The only way to get here is an active vent, which is not a curl and never has
                // been run with either of these fields — see CurrentField.HasPotential. Refused
                // rather than carried on the superseded scheme, because a world whose water this
                // grid cannot carry conservatively is a world whose books would drift in a way no
                // audit reads: the energy audit sees transfers, and a transfer is conservative
                // whatever velocity it is handed. logbook/specs/transport-conserves-spec.md.
                throw new ArgumentException(
                    "This water is a field of a place and has no vector potential, so its face " +
                    "fluxes cannot be made divergence-free and a uniform concentration would not " +
                    FormattableString.Invariant($"stay uniform. Mode {current.Mode}, shape ") +
                    FormattableString.Invariant($"{current.Shape}, vent ") +
                    FormattableString.Invariant($"{current.VentSpeed:0.###} m/s in patch {current.VentPatch}. ") +
                    "Turn the vent off, or use CurrentMode.Rolls, whose scheme is unchanged.",
                    nameof(current));
            }

            if (ofAPlace)
            {
                AdvectFromThePotential(current, seconds, dt);
                return;
            }

            Sweep(current, seconds, dt, transport: false);
        }

        /// <summary>
        /// The superseded centre-sampled transport of rounds 32 to 36, kept so that the repair can
        /// be measured against it and for no other reason.
        /// </summary>
        /// <param name="current">The flow.</param>
        /// <param name="seconds">The world's clock, s.</param>
        /// <param name="dt">Step length, s.</param>
        /// <remarks>
        /// <b>Not a route any world takes.</b> <see cref="Advect"/> never reaches this; a run
        /// cannot select it; it exists because "the repair works" is a claim about a difference,
        /// and a difference needs both numbers. <c>ConservativeTransportTests</c> prints the old
        /// and the new constant-field spreads side by side from this method and from
        /// <see cref="Advect"/>. See <c>logbook/specs/transport-conserves-spec.md</c> for what it
        /// gets wrong: the face velocities it builds are not divergence-free on the grid, and the
        /// three axis passes each run on the stock the last one left.
        /// </remarks>
        public void AdvectCentreSampled(CurrentField current, double seconds, float dt)
        {
            if (current == null || !current.AdvectFields) return;
            if (!(dt > 0f)) return;

            bool transport = current.Shape == WorldShape.Tank || current.Mode == CurrentMode.Transport;
            int substeps = transport ? CourantSubsteps(current, dt) : 1;
            float step = dt / substeps;

            if (transport) EnsureCellVelocities();

            for (int i = 0; i < substeps; i++)
            {
                Sweep(current, seconds + i * (double)step, step, transport);
            }
        }

        /// <summary>
        /// How many substeps the a-priori Courant bound asks for, refusing past
        /// <see cref="MaximumSubsteps"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="CurrentField.MaximumTransportSpeed"/> is a ceiling no argument reaches, so
        /// this is an over-estimate — typically by about twice — and over-estimating is the
        /// direction a stability check errs in. That is what makes it the right shape for a
        /// refusal and the wrong shape for a count.
        /// </para>
        /// <para>
        /// <b>Under the conservative route it is the refusal and nothing else.</b> What decides
        /// the count there is the fluxes' own largest per-cell outflow sum, which is the number
        /// positivity actually depends on and is measured on the water rather than derived from
        /// its ceiling. The two disagree, and by a lot: the campaign's box at 0.3 m/s on 1 m cells
        /// bounds at a Courant number that asks for two substeps while its largest outflow sum is
        /// 0.50 of a cell, which needs one. Keeping the bound as a floor would have doubled the
        /// transport's cost for an over-estimate. It still decides which worlds are refused, so a
        /// config the grid turned away before this repair is turned away after it, in the same
        /// words. See <see cref="AdvectFromThePotential"/>.
        /// </para>
        /// </remarks>
        private int CourantSubsteps(CurrentField current, float dt)
        {
            double courant = current.MaximumTransportSpeed * (double)dt / CellMetres;
            if (courant <= 0.5) return 1;

            int substeps = (int)Math.Ceiling(2.0 * courant);

            if (substeps > MaximumSubsteps)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"Upwind advection on a grid is stable to a Courant number of a half, ") +
                    FormattableString.Invariant(
                        $"and the transport field's fastest water, at most ") +
                    FormattableString.Invariant(
                        $"{current.MaximumTransportSpeed:0.####} m/s, crosses {courant:0.####} ") +
                    FormattableString.Invariant($"of a {CellMetres} m cell in {dt} s. ") +
                    FormattableString.Invariant(
                        $"That needs {substeps} substeps and the ceiling is {MaximumSubsteps}. ") +
                    "Shorten the step, widen the cell, or slow the current. The knob is an RMS " +
                    "over the box and this is the ceiling, which is the number stability " +
                    "depends on.",
                    nameof(current));
            }

            return substeps;
        }

        /// <summary>
        /// How many substeps <see cref="Advect"/> will split one step into rather than run past a
        /// Courant number of a half, and the ceiling past which it refuses instead.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Substeps rather than a refusal, ruled 2026-09-10.</b> The first build refused, on
        /// <see cref="Mix"/>'s argument that a clamp turns a config error into an unrecorded change
        /// of physics. That argument is sound about a clamp and wrong about a substep: a clamp runs
        /// a slower transport than the config asks for and says nothing, while a substep runs the
        /// transport the config asks for and costs time. The refusal was also biting a world nobody
        /// would call unreasonable, round 35's own 0.3 m/s over 1 m cells at a half-second
        /// metabolic step, where the honest answer is two substeps and not a stopped launch.
        /// </para>
        /// <para>
        /// <b>Each substep samples the field at its own clock</b>, <c>seconds + i·dt/n</c>, so a
        /// substepped step is a shorter step run n times and not one velocity applied n times. The
        /// three passes are conservative per substep exactly as they are per step, so the total is
        /// unmoved by however many there are.
        /// </para>
        /// <para>
        /// <b>The ceiling stays, because the cost is real.</b> Eight substeps is a Courant number
        /// of four, sixteen field samples per cell per metabolic step; past that the config is
        /// asking for water faster than the grid is a description of, and the same refusal names
        /// the numbers so the fix is arithmetic.
        /// </para>
        /// </remarks>
        public const int MaximumSubsteps = 8;

        /// <summary>
        /// The largest share of its own stock a cell may be asked to hand out in one substep.
        /// </summary>
        /// <remarks>
        /// <b>Positivity needs at most 1; the margin is what is left over.</b> The six faces of a
        /// cell are computed from one snapshot and applied together, so a cell can lose at most
        /// the sum of its outflow fractions and nothing goes negative while that sum is at or
        /// below 1. The substep count is chosen from the first substep's own fluxes, and the later
        /// substeps sample the field at their own clock, so a quarter is kept back for the field
        /// having moved between them — which over half a second of a current whose phases turn in
        /// thousands is a great deal more than it needs.
        /// </remarks>
        private const double OutflowMargin = 0.75;

        /// <summary>
        /// Carries the stock on face fluxes assembled from the current's vector potential — the
        /// scheme of <c>logbook/specs/transport-conserves-spec.md</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why edges and not centres.</b> A uniform dissolved concentration carried by
        /// incompressible water in a closed container stays uniform. Through round 36 this grid
        /// did not keep it so: it sampled the velocity at a cell's centre, used that for the
        /// cell's east and front faces and a second sample at the lower interface for the
        /// vertical, and ran the three axis passes one after another on the stock the last one
        /// left. Those face velocities are not divergence-free on the grid, and a sequential sweep
        /// compresses along one axis before the next expands. Conservation and positivity held;
        /// the constant field did not — the campaign's own water turned 1 unit/m³ into 0.357 to
        /// 2.454 in 600 s.
        /// </para>
        /// <para>
        /// <b>Stokes, on the lattice.</b> Every current the campaign runs is the curl of a vector
        /// potential (<see cref="CurrentField.PotentialAt(float, float, float, double)"/>), and the flux of a curl through a
        /// face is the circulation of the potential round that face's edges. So each edge of the
        /// lattice carries one number, <c>A</c> at its midpoint dotted with its direction times
        /// the cell size, and each face's flux is the signed sum of its four. Every edge belongs
        /// to two faces of any one cell with opposite sign, so the cell's net flux is
        /// <i>identically</i> zero — not to a tolerance, and for any edge values whatever, which
        /// is why a badly resolved mode or a rounded sample cannot break it. What it costs is
        /// about three potential samples per live cell per substep against two velocity samples
        /// before.
        /// </para>
        /// <para>
        /// <b>The boundaries.</b> The surface plane and the floor plane carry no edge values at
        /// all: both fields' potentials have <c>sin(qπy/D)</c> on every horizontal component, so
        /// the analytic value there is zero, and leaving the planes empty makes "no flux through
        /// the surface and none through the floor" a property of the loop bounds rather than of a
        /// sampler agreeing to return zero. In a tank every edge that touches a dead cell — or a
        /// cell off the array, which is the glass — is left at zero too, so every face between
        /// live water and dead carries nothing while every live cell still telescopes to zero. The
        /// consequence, and it is a real one: the discrete field is tangential to the mask's
        /// stair-step rather than to the circle, so the rim's face speeds are lower than the
        /// analytic field's. <c>ConservativeTransportTests</c> measures how much lower on 1 m and
        /// 5 m cells and reports it rather than asserting a number nobody has derived.
        /// </para>
        /// <para>
        /// <b>All three axes from one stock.</b> The three flux arrays are computed from the same
        /// snapshot and only then applied, which is the other half of the fault: calling the three
        /// existing applies in sequence on precomputed transfers is the same arithmetic as one
        /// combined apply, because a transfer is an amount and not a fraction.
        /// </para>
        /// <para>
        /// <b>No clamp, and the substeps decide instead.</b> A clamped face fraction is a
        /// divergence: it moves less than the water does across that one face and nothing else
        /// changes, which is exactly the thing being repaired. So the transfer across a face is
        /// the upwind cell's stock times <c>Q·dt/cell³</c> whatever that comes to, and the step is
        /// split until the largest per-cell outflow sum is at or below
        /// <see cref="OutflowMargin"/>. The fluxes decide that count; the a-priori Courant bound
        /// is kept as the refusal past <see cref="MaximumSubsteps"/> and nothing else, so a config
        /// that was refused before is refused now — see <see cref="CourantSubsteps"/> for why the
        /// two must not be the same number.
        /// </para>
        /// </remarks>
        private void AdvectFromThePotential(CurrentField current, double seconds, float dt)
        {
            // Still water carries nothing, and a field at Speed 0 must not be built to find that
            // out: the streams' construction is a lattice of measurements.
            if (!(current.Speed > 0f)) return;

            // The a-priori bound, first, and as a refusal only: a config the grid refused before
            // this repair is refused after it, and for the same reason in the same words. It does
            // not set the count. CourantSubsteps' own remarks say why.
            CourantSubsteps(current, dt);

            EnsureFaceBuffers();
            SampleEdges(current, seconds);
            AssembleFaces();

            double outflow = LargestOutflowFraction(dt);
            int substeps = outflow <= OutflowMargin
                ? 1
                : (int)Math.Ceiling(outflow / OutflowMargin);

            if (substeps > MaximumSubsteps)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A cell would be asked for {outflow:0.###} of its own stock in {dt} s, and ") +
                    "upwind transport can hand out at most its whole stock, so the step is split " +
                    FormattableString.Invariant(
                        $"until the largest outflow sum is under {OutflowMargin}. That needs ") +
                    FormattableString.Invariant(
                        $"{substeps} substeps and the ceiling is {MaximumSubsteps}. ") +
                    "Shorten the step, widen the cell, or slow the current. This is the Courant " +
                    "condition read off the face fluxes rather than off the field's ceiling.",
                    nameof(current));
            }

            float step = dt / substeps;

            // The first substep reuses the fluxes already assembled, scaled by the shorter step;
            // every later one samples the field at its own clock, so a split step is a shorter
            // step run n times and not one snapshot applied n times.
            ApplyFaces(step);

            for (int i = 1; i < substeps; i++)
            {
                SampleEdges(current, seconds + i * (double)step);
                AssembleFaces();
                ApplyFaces(step);
            }
        }

        /// <summary>
        /// What one step's face fluxes come to, without moving anything — for the build report.
        /// </summary>
        /// <param name="current">The flow.</param>
        /// <param name="seconds">The world's clock, s.</param>
        /// <param name="dt">Step length, s.</param>
        /// <remarks>
        /// <para>
        /// Reports the substep count <see cref="Advect"/> would take, the largest per-cell outflow
        /// sum it was chosen from, and the two readings the spec asks for at each cell size: the
        /// RMS of the face-normal speeds the scheme actually carries (<c>Q/cell²</c>) and the RMS
        /// of the analytic field's own normal component at the same face centres. The two part
        /// company at a tank's rim, where the discrete field is tangential to the mask's
        /// stair-step and the analytic one to the circle.
        /// </para>
        /// <para>
        /// <b>And the worst net face flux over the live cells</b>, which is the scheme's own
        /// invariant and the one number here that is asserted rather than reported: a cell's six
        /// signed fluxes are built from twelve edge values, each of which appears twice with
        /// opposite sign, so the sum is exactly zero for any edge values at all. It is returned
        /// from here rather than from a test-only accessor so that the invariant is checked on the
        /// same arithmetic a step runs.
        /// </para>
        /// <para>
        /// A measurement rather than an assertion: it exists so that the difference between a
        /// grid and the water it stands for is a number in the record instead of a shrug.
        /// </para>
        /// </remarks>
        public (int Substeps, double LargestOutflow, double DiscreteRms, double AnalyticRms,
                int OpenFaces, double WorstNetFlux)
            MeasureFaceFluxes(CurrentField current, double seconds, float dt)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (!current.HasPotential)
            {
                throw new ArgumentException(
                    "This water has no vector potential, so it has no face fluxes to measure. " +
                    "logbook/specs/transport-conserves-spec.md.",
                    nameof(current));
            }

            EnsureFaceBuffers();
            SampleEdges(current, seconds);
            AssembleFaces();

            CourantSubsteps(current, dt);

            double outflow = LargestOutflowFraction(dt);
            int substeps = outflow <= OutflowMargin ? 1 : (int)Math.Ceiling(outflow / OutflowMargin);

            double h = CellMetres;
            double area = h * h;
            double discrete = 0d, analytic = 0d;
            double worstNet = 0d;
            int faces = 0;

            for (int iy = 0; iy < _ny; iy++)
            {
                for (int ix = 0; ix < _nx; ix++)
                {
                    for (int iz = 0; iz < _nz; iz++)
                    {
                        int cell = Index(ix, iy, iz);
                        if (_live != null && !_live[cell]) continue;

                        float centreY = -(float)((iy + 0.5) * h);

                        // The invariant: outward through all six faces, which cancels term for
                        // term. A neighbour that is off the array contributes nothing, which is
                        // the same statement as its face being closed.
                        double net = _faceX[cell] + _faceZ[cell] + _faceY[cell];

                        int west = ix == 0 ? (Shape == WorldShape.Tank ? -1 : _nx - 1) : ix - 1;
                        if (west >= 0) net -= _faceX[Index(west, iy, iz)];

                        int back = iz == 0 ? (Shape == WorldShape.Tank ? -1 : _nz - 1) : iz - 1;
                        if (back >= 0) net -= _faceZ[Index(ix, iy, back)];

                        if (iy > 0) net -= _faceY[Index(ix, iy - 1, iz)];

                        if (Math.Abs(net) > worstNet) worstNet = Math.Abs(net);

                        if (_nx >= 2 && EastOf(ix, iy, iz) >= 0)
                        {
                            discrete += Square(_faceX[cell] / area);
                            analytic += Square(
                                current.VelocityAt(
                                    (float)((ix + 1) * h), centreY, (float)((iz + 0.5) * h), seconds).X);
                            faces++;
                        }

                        if (_nz >= 2 && FrontOf(ix, iy, iz) >= 0)
                        {
                            discrete += Square(_faceZ[cell] / area);
                            analytic += Square(
                                current.VelocityAt(
                                    (float)((ix + 0.5) * h), centreY, (float)((iz + 1) * h), seconds).Z);
                            faces++;
                        }

                        if (_ny >= 2 && iy < _ny - 1)
                        {
                            discrete += Square(_faceY[cell] / area);
                            analytic += Square(
                                current.VelocityAt(
                                    (float)((ix + 0.5) * h), -(float)((iy + 1) * h),
                                    (float)((iz + 0.5) * h), seconds).Y);
                            faces++;
                        }
                    }
                }
            }

            return (
                substeps, outflow,
                faces == 0 ? 0d : Math.Sqrt(discrete / faces),
                faces == 0 ? 0d : Math.Sqrt(analytic / faces),
                faces, worstNet);
        }

        private static double Square(double v) => v * v;

        // ---------------------------------------------------------- the lattice's edges and faces

        // An edge is named by the lattice node it starts at and the axis it runs along. Nodes run
        // 0..n on each axis, so the x edges are indexed by the cell's own ix (the edge from node
        // ix to node ix+1) and by nodes on the other two, and so on round. In a box the far planes
        // are copies of the near ones rather than fresh samples: the field is periodic on both
        // rings, but 2*pi*n computed in double is not exactly 2*pi*n, and a copy is what makes the
        // telescoping at the seam exact rather than nearly so.
        private int EdgeXIndex(int i, int j, int k) => (j * _nx + i) * (_nz + 1) + k;

        private int EdgeYIndex(int i, int j, int k) => (j * (_nx + 1) + i) * (_nz + 1) + k;

        private int EdgeZIndex(int i, int j, int k) => (j * (_nx + 1) + i) * _nz + k;

        /// <summary>
        /// Allocates the edge and face buffers, on the first step that takes the conservative
        /// route and never in a world that does not.
        /// </summary>
        /// <remarks>
        /// Three edge arrays of about one double per cell each and three face arrays of exactly
        /// one, which on the campaign's 6,000-cell detritus grid is under 400 kB in total. Lazy
        /// for <see cref="EnsureCellVelocities"/>'s reason: a rolls world carries the memory it
        /// always did.
        /// </remarks>
        private void EnsureFaceBuffers()
        {
            if (_edgeX != null) return;

            _edgeX = new double[_nx * (_ny + 1) * (_nz + 1)];
            _edgeY = new double[(_nx + 1) * _ny * (_nz + 1)];
            _edgeZ = new double[(_nx + 1) * (_ny + 1) * _nz];

            _faceX = new double[_stock.Length];
            _faceY = new double[_stock.Length];
            _faceZ = new double[_stock.Length];
        }

        /// <summary>
        /// Whether the cell at these indices is water, for indices that may be off the array:
        /// wrapped on x and z in a box, and dead off the array in a tank.
        /// </summary>
        /// <remarks>
        /// What makes the glass a wall for the edges as well as for the faces. A cell off the
        /// array's y range is never water, which is what keeps the surface and the floor closed.
        /// </remarks>
        private bool CellIsWater(int ix, int iy, int iz)
        {
            if (iy < 0 || iy >= _ny) return false;

            if (Shape == WorldShape.Tank)
            {
                if (ix < 0 || ix >= _nx || iz < 0 || iz >= _nz) return false;
            }
            else
            {
                if (ix < 0) ix += _nx;
                else if (ix >= _nx) ix -= _nx;

                if (iz < 0) iz += _nz;
                else if (iz >= _nz) iz -= _nz;
            }

            return _live == null || _live[Index(ix, iy, iz)];
        }

        /// <summary>Fills the three edge families from the current's potential at one clock.</summary>
        private void SampleEdges(CurrentField current, double seconds)
        {
            Array.Clear(_edgeX, 0, _edgeX.Length);
            Array.Clear(_edgeY, 0, _edgeY.Length);
            Array.Clear(_edgeZ, 0, _edgeZ.Length);

            double h = CellMetres;
            bool tank = Shape == WorldShape.Tank;

            // The x and z edges, on the interior node planes only: at y = 0 and y = -D the
            // potential's horizontal components are analytically zero, and an empty plane says so
            // without depending on Math.Sin(-Math.PI) being 0 rather than -1.2e-16.
            for (int j = 1; j < _ny; j++)
            {
                float y = -(float)(j * h);

                for (int i = 0; i < _nx; i++)
                {
                    float x = (float)((i + 0.5) * h);

                    for (int k = 0; k <= _nz; k++)
                    {
                        if (!tank && k == _nz)
                        {
                            _edgeX[EdgeXIndex(i, j, k)] = _edgeX[EdgeXIndex(i, j, 0)];
                            continue;
                        }

                        // The four cells this edge belongs to. One dead one and the edge is zero,
                        // which is what makes every face onto the glass carry nothing.
                        if (!CellIsWater(i, j - 1, k - 1) || !CellIsWater(i, j - 1, k) ||
                            !CellIsWater(i, j, k - 1) || !CellIsWater(i, j, k))
                        {
                            continue;
                        }

                        _edgeX[EdgeXIndex(i, j, k)] =
                            current.PotentialAt(x, y, (float)(k * h), seconds).X * h;
                    }
                }

                for (int i = 0; i <= _nx; i++)
                {
                    for (int k = 0; k < _nz; k++)
                    {
                        if (!tank && i == _nx)
                        {
                            _edgeZ[EdgeZIndex(i, j, k)] = _edgeZ[EdgeZIndex(0, j, k)];
                            continue;
                        }

                        if (!CellIsWater(i - 1, j - 1, k) || !CellIsWater(i - 1, j, k) ||
                            !CellIsWater(i, j - 1, k) || !CellIsWater(i, j, k))
                        {
                            continue;
                        }

                        _edgeZ[EdgeZIndex(i, j, k)] =
                            current.PotentialAt((float)(i * h), y, (float)((k + 0.5) * h), seconds).Z * h;
                    }
                }
            }

            // The y edges, which run inside a layer rather than across an interface, so every one
            // of them has a midpoint at an interior depth and none is on a boundary plane.
            for (int j = 0; j < _ny; j++)
            {
                float y = -(float)((j + 0.5) * h);

                for (int i = 0; i <= _nx; i++)
                {
                    for (int k = 0; k <= _nz; k++)
                    {
                        if (!tank && (i == _nx || k == _nz))
                        {
                            _edgeY[EdgeYIndex(i, j, k)] =
                                _edgeY[EdgeYIndex(i == _nx ? 0 : i, j, k == _nz ? 0 : k)];
                            continue;
                        }

                        if (!CellIsWater(i - 1, j, k - 1) || !CellIsWater(i - 1, j, k) ||
                            !CellIsWater(i, j, k - 1) || !CellIsWater(i, j, k))
                        {
                            continue;
                        }

                        _edgeY[EdgeYIndex(i, j, k)] =
                            current.PotentialAt((float)(i * h), y, (float)(k * h), seconds).Y * h;
                    }
                }
            }
        }

        /// <summary>
        /// Assembles each open face's volumetric flux, m³/s, as the circulation of the edges round
        /// it in right-hand order about its outward normal.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>East face</b> (normal +x), at node plane <c>i = ix+1</c>: up the near z edge, along
        /// the shallow x-plane, down the far one, back along the deep one. <b>Front face</b>
        /// (normal +z) and <b>lower face</b> (stored as the flux <i>downward</i>, which is the
        /// sign <see cref="ApplyDown"/> wants) the same way about their own normals.
        /// </para>
        /// <para>
        /// <b>Closed faces are left at zero</b> rather than computed. The two agree — all four
        /// edges of a face onto the glass are zero, so its circulation is zero — and skipping is
        /// what keeps the cost down in a tank, whose array is about a fifth dry. That they agree
        /// is the thing <c>ConservativeTransportTests</c> checks by summing every live cell's six
        /// faces and finding exactly zero.
        /// </para>
        /// </remarks>
        private void AssembleFaces()
        {
            Array.Clear(_faceX, 0, _faceX.Length);
            Array.Clear(_faceY, 0, _faceY.Length);
            Array.Clear(_faceZ, 0, _faceZ.Length);

            for (int iy = 0; iy < _ny; iy++)
            {
                for (int ix = 0; ix < _nx; ix++)
                {
                    for (int iz = 0; iz < _nz; iz++)
                    {
                        int cell = Index(ix, iy, iz);
                        if (_live != null && !_live[cell]) continue;

                        if (_nx >= 2 && EastOf(ix, iy, iz) >= 0)
                        {
                            _faceX[cell] =
                                _edgeY[EdgeYIndex(ix + 1, iy, iz)] +
                                _edgeZ[EdgeZIndex(ix + 1, iy, iz)] -
                                _edgeY[EdgeYIndex(ix + 1, iy, iz + 1)] -
                                _edgeZ[EdgeZIndex(ix + 1, iy + 1, iz)];
                        }

                        if (_nz >= 2 && FrontOf(ix, iy, iz) >= 0)
                        {
                            _faceZ[cell] =
                                _edgeX[EdgeXIndex(ix, iy + 1, iz + 1)] +
                                _edgeY[EdgeYIndex(ix + 1, iy, iz + 1)] -
                                _edgeX[EdgeXIndex(ix, iy, iz + 1)] -
                                _edgeY[EdgeYIndex(ix, iy, iz + 1)];
                        }

                        // A dead column is dead all the way down, so a live cell's lower face is
                        // open whenever there is a layer below it.
                        if (_ny >= 2 && iy < _ny - 1)
                        {
                            _faceY[cell] =
                                _edgeZ[EdgeZIndex(ix + 1, iy + 1, iz)] +
                                _edgeX[EdgeXIndex(ix, iy + 1, iz)] -
                                _edgeZ[EdgeZIndex(ix, iy + 1, iz)] -
                                _edgeX[EdgeXIndex(ix, iy + 1, iz + 1)];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The largest share of its own stock any live cell would be asked to hand out over
        /// <paramref name="dt"/>.
        /// </summary>
        /// <remarks>
        /// A cell's signed face fluxes sum to zero, so its outflow and its inflow are equal and
        /// each is half the sum of the six magnitudes. That identity is why this needs no sign
        /// bookkeeping, and it is the same identity the scheme rests on.
        /// </remarks>
        private double LargestOutflowFraction(float dt)
        {
            double scale = 0.5 * dt / CellVolume;
            double largest = 0d;

            for (int iy = 0; iy < _ny; iy++)
            {
                for (int ix = 0; ix < _nx; ix++)
                {
                    for (int iz = 0; iz < _nz; iz++)
                    {
                        int cell = Index(ix, iy, iz);
                        if (_live != null && !_live[cell]) continue;

                        double sum = Math.Abs(_faceX[cell]) + Math.Abs(_faceZ[cell]) + Math.Abs(_faceY[cell]);

                        int west = ix == 0 ? (Shape == WorldShape.Tank ? -1 : _nx - 1) : ix - 1;
                        if (west >= 0) sum += Math.Abs(_faceX[Index(west, iy, iz)]);

                        int back = iz == 0 ? (Shape == WorldShape.Tank ? -1 : _nz - 1) : iz - 1;
                        if (back >= 0) sum += Math.Abs(_faceZ[Index(ix, iy, back)]);

                        if (iy > 0) sum += Math.Abs(_faceY[Index(ix, iy - 1, iz)]);

                        double outflow = sum * scale;
                        if (outflow > largest) largest = outflow;
                    }
                }
            }

            return largest;
        }

        /// <summary>
        /// Turns the face fluxes into transfers from one snapshot of the stock and applies all
        /// three axes.
        /// </summary>
        /// <remarks>
        /// Upwind: each face moves <c>Q·dt/cell³</c> of the cell the water is coming <i>from</i>,
        /// never clamped — a clamped fraction would move less than the water does across that one
        /// face and nothing else, which is a divergence, and a divergence is the fault this scheme
        /// exists to remove. The three transfer arrays are built before any of them is applied, so
        /// the result does not depend on the order the axes run in.
        /// </remarks>
        private void ApplyFaces(float dt)
        {
            double scale = dt / CellVolume;

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
                        if (_live != null && !_live[cell]) continue;

                        double q = _faceX[cell];
                        if (q != 0d)
                        {
                            // q > 0 takes from this cell, q < 0 from the one east of it, and
                            // writing it as one product keeps the two branches the same arithmetic.
                            int east = EastOf(ix, iy, iz);
                            _fluxX[cell] = (q > 0d ? _stock[cell] : _stock[east]) * q * scale;
                        }

                        q = _faceZ[cell];
                        if (q != 0d)
                        {
                            int front = FrontOf(ix, iy, iz);
                            _fluxZ[cell] = (q > 0d ? _stock[cell] : _stock[front]) * q * scale;
                        }

                        // Positive is downward, which is what ApplyDown means by a flux: sinking
                        // water carries what is above it down, rising water what is below it up.
                        q = _faceY[cell];
                        if (q != 0d)
                        {
                            _fluxY[cell] = (q > 0d ? _stock[cell] : _stock[Index(ix, iy + 1, iz)]) * q * scale;
                        }
                    }
                }
            }

            if (_nx >= 2) ApplyEast(_fluxX);
            if (_ny >= 2) ApplyDown(_fluxY);
            if (_nz >= 2) ApplyFront(_fluxZ);
        }

        /// <summary>One pass of the three upwind axes at one clock. <see cref="Advect"/>'s body.</summary>
        private void Sweep(CurrentField current, double seconds, float dt, bool transport)
        {
            int k = PatchCount;

            if (transport)
            {
                for (int iy = 0; iy < _ny; iy++)
                {
                    float centreY = -((iy + 0.5f) * CellMetres);
                    float interfaceY = -((iy + 1) * CellMetres);
                    bool hasBelow = iy < _ny - 1;

                    for (int ix = 0; ix < _nx; ix++)
                    {
                        float x = (ix + 0.5f) * CellMetres;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            float z = (iz + 0.5f) * CellMetres;
                            int cell = Index(ix, iy, iz);

                            // Dead cells are never a source or a destination, so the field is not
                            // asked about them: a sample is five Fourier modes or twenty-seven
                            // terms of the tank's streams, and a tank's array is about a fifth dry.
                            if (_live != null && !_live[cell]) continue;

                            Float3 atCentre = current.VelocityAt(x, centreY, z, seconds);
                            _cellVelocityX[cell] = atCentre.X;
                            _cellVelocityZ[cell] = atCentre.Z;
                            _cellVelocityY[cell] = hasBelow
                                ? current.VelocityAt(x, interfaceY, z, seconds).Y
                                : 0d;
                        }
                    }
                }
            }
            else
            {
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
            }

            if (_nx >= 2)
            {
                Array.Clear(_fluxX, 0, _fluxX.Length);

                for (int iy = 0; iy < _ny; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = Index(ix, iy, iz);
                            if (_live != null && !_live[cell]) continue;

                            // The mask is the glass — see EastOf. In a box this is the wrap the
                            // record was advected across and the branch never fires.
                            int east = EastOf(ix, iy, iz);
                            if (east < 0) continue;

                            // The roll's velocity is per layer and patch, and at A > 1 a column's
                            // patch is a function of z as well as of x, so the lookup sits inside
                            // this loop rather than outside it. Every value and every assignment
                            // is the one the hoisted form produced; what is gone is a skip of a
                            // whole column, and the flux it would have skipped is still the zero
                            // the clear above left.
                            double u = transport
                                ? _cellVelocityX[cell]
                                : _velocityX[iy * k + PatchOfColumn(ix, iz)];

                            if (u == 0d) continue;

                            double fraction = Math.Abs(u) * dt / CellMetres;
                            if (fraction > 0.5) fraction = 0.5;

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
                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int upper = Index(ix, iy, iz);
                            int lower = Index(ix, iy + 1, iz);

                            // A dead column is dead all the way down, so the vertical face needs
                            // no mask of its own beyond this one.
                            if (_live != null && !_live[upper]) continue;

                            // Per cell rather than per column, for the x pass's reason.
                            double w = transport
                                ? _cellVelocityY[upper]
                                : _velocityY[iy * k + PatchOfColumn(ix, iz)];

                            if (w == 0d) continue;

                            double fraction = Math.Abs(w) * dt / CellMetres;
                            if (fraction > 0.5) fraction = 0.5;

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
                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = Index(ix, iy, iz);
                            if (_live != null && !_live[cell]) continue;

                            int front = FrontOf(ix, iy, iz);
                            if (front < 0) continue;

                            // Per cell rather than per column, for the x pass's reason.
                            double v = transport
                                ? _cellVelocityZ[cell]
                                : _velocityZ[iy * k + PatchOfColumn(ix, iz)];

                            if (v == 0d) continue;

                            double fraction = Math.Abs(v) * dt / CellMetres;
                            if (fraction > 0.5) fraction = 0.5;

                            _fluxZ[cell] = v > 0d ? _stock[cell] * fraction : -_stock[front] * fraction;
                        }
                    }
                }

                ApplyFront(_fluxZ);
            }
        }

        /// <summary>
        /// The per-cell velocity buffers, allocated on the first transport step and never in a
        /// world that does not take one.
        /// </summary>
        /// <remarks>
        /// Three arrays of one double per cell, which is 144 kB on the campaign's 6,000 detritus
        /// cells. Lazy rather than allocated in the constructor so that a rolls world, which is
        /// every run in the record, carries exactly the memory it did before.
        /// </remarks>
        private void EnsureCellVelocities()
        {
            if (_cellVelocityX != null) return;

            _cellVelocityX = new double[_stock.Length];
            _cellVelocityY = new double[_stock.Length];
            _cellVelocityZ = new double[_stock.Length];
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
                for (int iz = 0; iz < _nz; iz++)
                {
                    if (PatchOfColumn(ix, iz) != patch) continue;
                    sum += _stock[Index(ix, layer, iz)];
                }
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

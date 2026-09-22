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
    /// <para>
    /// <b>With a <see cref="BedShape"/> the mask gains a second condition and nothing else
    /// changes</b> — D092, <c>logbook/specs/bed-spec.md</c> item 9. A cell is live when its centre
    /// is inside the circle <i>and</i> above the floor at its own column, so a column is water from
    /// the surface down to its own <see cref="FloorYAtColumn"/> and rock below that. Everything
    /// that runs over live cells follows: no new cells, no flux into a dead one, seeding and the
    /// totals over the water that exists, and settling stopping at the lowest live cell of a column
    /// exactly as it stopped at the flat floor. The floor's low spots are therefore where the
    /// columns are deepest, which is what makes a hollow a place detritus gathers in;
    /// <see cref="ColumnFloorAndFloorStock"/> is the reading that says so.
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

        // The shaped floor's numbers at each of the three edge families' own (x, z) lattices,
        // built once per current and read at every depth, every substep and every step - D092 and
        // CurrentField.BedColumn's remarks for why. Null on a flat bed, which is every recorded
        // world, and null until the first conservative step on a shaped one. _bedColumnsFor is the
        // current they were built against: a grid handed a second field rebuilds rather than
        // reusing a floor that is not that water's.
        private CurrentField _bedColumnsFor;
        private CurrentField.BedColumn[] _bedColumnX;
        private CurrentField.BedColumn[] _bedColumnY;
        private CurrentField.BedColumn[] _bedColumnZ;

        // D105's hoist: everything the streams' potential derives from a lattice point's (x, z)
        // and from the height it reads the flat field at, which is neither the clock's nor the
        // sample's. Built once per current, in the same three edge lattices the bed columns use,
        // and then read at every depth, every substep and every step. Null on a box, on still
        // water and until the first conservative step in a tank. See EnsureStreamsTerms for what
        // a shaped floor costs here: the depth terms are per column and depth rather than per
        // depth, because the floor-following map reads each column at its own heights.
        private CurrentField _streamsTermsFor;
        private CurrentField.StreamsColumn[] _streamsColumnX;
        private CurrentField.StreamsColumn[] _streamsColumnY;
        private CurrentField.StreamsColumn[] _streamsColumnZ;
        private CurrentField.StreamsDepth[] _streamsDepthX;
        private CurrentField.StreamsDepth[] _streamsDepthY;
        private CurrentField.StreamsDepth[] _streamsDepthZ;

        // How a lattice's depth terms are indexed: depth * stride + column * step. On a shaped
        // floor the table is depth-major (stride = the column count, step = 1) because that is
        // the order the edge loops walk it — one node plane at a time, every column of it. The
        // column-major layout reads 24 bytes out of every 64-byte line and walks the whole table
        // once per plane; depth-major streams it once per pass, which on round 43's tank is the
        // difference between 240 MB of traffic and 4. On a flat floor one row serves every
        // column (stride = 1, step = 0). The horizontal lattices are read at the ny − 1 interior
        // node planes and the vertical one at the ny layer midpoints, so they differ.
        private int _streamsStrideX;
        private int _streamsStrideY;
        private int _streamsStrideZ;
        private int _streamsDepthStep;

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

        /// <summary>The box's depth, m — the configured one, which with a bed is the mean.</summary>
        public float DepthMetres { get; }

        /// <summary>
        /// How deep the array itself reaches, m: <see cref="LayerCount"/> cells. The same as
        /// <see cref="DepthMetres"/> everywhere except a tank with a bed, where it reaches the
        /// deepest column's floor so that no hollow is cut off at the mean depth (D092).
        /// </summary>
        public float ArrayDepthMetres => LayerCount * CellMetres;

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
        /// The floor's shape, or null for the flat bed — D092. A flat bed is every recorded world.
        /// </summary>
        public BedShape Bed { get; }

        /// <summary>
        /// Cells the mask calls live: the whole array in a box, the cells whose centres lie inside
        /// the circle in a tank, and of those the ones above the floor when there is a bed.
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
            int patchesAcross = 1, WorldShape shape = WorldShape.Box, float tankRadiusMetres = 0f,
            BedShape bed = null)
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

            if (bed != null && bed.HasRelief && shape != WorldShape.Tank)
            {
                throw new ArgumentException(
                    "A bed with relief was handed to a box. The height map is fitted over the " +
                    "tank's disc and the box is periodic on both horizontal axes, where a floor " +
                    "would have to meet itself at two seams. logbook/specs/bed-spec.md.",
                    nameof(bed));
            }

            if (bed != null && bed.HasRelief &&
                (Math.Abs(bed.DepthMetres - worldDepth) > 1e-4f ||
                 Math.Abs(bed.RadiusMetres - tankRadiusMetres) > 1e-4f))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"The bed is a map over a tank {bed.RadiusMetres} m across and ") +
                    FormattableString.Invariant($"{bed.DepthMetres} m deep, and this field is ") +
                    FormattableString.Invariant($"{tankRadiusMetres} m and {worldDepth} m. One ") +
                    "geometry reaches the fields, the water and the placer, or the mask and the " +
                    "collider are two different floors.",
                    nameof(bed));
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

            // THE ARRAY REACHES THE DEEPEST FLOOR, not the mean one — D092. The height map is
            // mean-zero over the disc (spec item 5), so half of it is below the configured depth,
            // and an array that stopped at −depth would floor every hollow off at exactly the
            // place the round is about: the deepest columns would be flat-bottomed, the water
            // volume would come out under the floor's own integral, and the loss would be
            // concentrated in the pockets rather than spread. So a tank with a bed gets as many
            // extra layers as its lowest column needs, counted over the cell-centre columns
            // themselves rather than over the map's own lattice, which makes it exact for the
            // columns that exist rather than nearly right for all of them.
            //
            // What it costs a reader: LayerCount is no longer DepthMetres over the cell in such a
            // world, and a layer index no longer maps to a depth band shared with a flat run. The
            // report's per-depth bins are read against ArrayDepthMetres, and the refuge band
            // (RefugeLayerCount, counted up from the array's floor) sits under the deepest column
            // rather than under the mean one.
            if (shape == WorldShape.Tank && bed != null && bed.HasRelief)
            {
                double lowest = 0d;

                for (int ix = 0; ix < _nx; ix++)
                {
                    double cx = (ix + 0.5d) * cellMetres;

                    for (int iz = 0; iz < _nz; iz++)
                    {
                        double cz = (iz + 0.5d) * cellMetres;
                        if (!TankGeometry.Inside(cx, cz, tankRadiusMetres)) continue;

                        double below = -bed.FloorY(cx, cz) - worldDepth;
                        if (below > lowest) lowest = below;
                    }
                }

                if (lowest > 0d) _ny += (int)Math.Ceiling(lowest / cellMetres - 1e-9);
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

            // The floor of every column, as a layer index and as a height. In a box and in a flat
            // tank it is the array's own last layer and −depth, which is what every line that
            // reads it computed for itself before the bed existed; with a bed it is the lowest
            // cell whose centre is above the floor, and −1 for a column that holds no water at
            // all. Held rather than recomputed because Settle, Mix and Remineralise each ask it
            // once per column per call and a height map is trigonometry.
            _lowestLive = new int[_nx * _nz];
            _floorYOfColumn = new float[_nx * _nz];

            for (int i = 0; i < _lowestLive.Length; i++)
            {
                _lowestLive[i] = _ny - 1;
                _floorYOfColumn[i] = -worldDepth;
            }

            Bed = bed != null && bed.HasRelief ? bed : null;

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

                    // The bed's second condition — D092, logbook/specs/bed-spec.md item 9. A cell
                    // is water when its own centre is above the floor under its own column, which
                    // is the same test the glass gets and for the same reason: a cell the floor
                    // cuts in half is either water or rock, and a fractional one would hold less
                    // than a whole cell's worth while being priced as a whole cell.
                    float floorY = Bed != null ? (float)Bed.FloorY(cx, cz) : -worldDepth;
                    _floorYOfColumn[ix * _nz + iz] = floorY;

                    int lowest = -1;

                    for (int iy = 0; iy < _ny; iy++)
                    {
                        if (!inside) continue;
                        if (-((iy + 0.5f) * cellMetres) <= floorY) continue;

                        _live[Index(ix, iy, iz)] = true;
                        live++;
                        lowest = iy;
                    }

                    _lowestLive[ix * _nz + iz] = lowest;

                    // A dead column belongs to no patch. -1 rather than a ring index, so that
                    // every per-patch sum — StockInLayer, TakeFromLayer, the roll's velocity
                    // lookup — passes over it without needing to know about the mask. A column the
                    // floor has filled to the surface is as dead as one outside the glass, and is
                    // named the same way.
                    if (lowest < 0) _patchOfColumn[ix * _nz + iz] = -1;
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

        // The lowest live layer of each column, −1 for a column with no water in it, and the
        // floor's height under each column, m. Both are the array's last layer and −depth
        // everywhere until a bed says otherwise (D092).
        private readonly int[] _lowestLive;
        private readonly float[] _floorYOfColumn;

        /// <summary>
        /// The floor's height under a column, m — <c>−depth</c> on a flat bed and the bed's own
        /// height there otherwise. D092, <c>logbook/specs/bed-spec.md</c> item 9.
        /// </summary>
        /// <remarks>
        /// Read at the column's centre and held from construction, so the mask, this reading and
        /// the collider Unity builds are all the same floor rather than three samples of one
        /// function. A column outside the glass answers with the flat bed's height, which is what
        /// it was masked against; ask <see cref="LowestLiveLayer"/> whether there is any water in
        /// it.
        /// </remarks>
        public float FloorYAtColumn(int ix, int iz)
        {
            if (ix < 0 || ix >= _nx || iz < 0 || iz >= _nz)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ix), (ix, iz),
                    FormattableString.Invariant($"This field is {_nx} by {_nz} columns."));
            }

            return _floorYOfColumn[ix * _nz + iz];
        }

        /// <summary>
        /// The lowest layer of a column that is water — the cell that sits on the floor — or −1
        /// when the column holds none.
        /// </summary>
        public int LowestLiveLayer(int ix, int iz)
        {
            if (ix < 0 || ix >= _nx || iz < 0 || iz >= _nz)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ix), (ix, iz),
                    FormattableString.Invariant($"This field is {_nx} by {_nz} columns."));
            }

            return _lowestLive[ix * _nz + iz];
        }

        /// <summary>
        /// Every live column's floor height and what its floor cell holds, for the read that asks
        /// whether the hollows hold the most — D092, <c>logbook/specs/bed-spec.md</c>'s "what the
        /// round reads".
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The floor cell rather than the column</b>, because that is where the answer is:
        /// settling stops at the lowest live cell (<see cref="Settle"/>), so a hollow holds what
        /// it does by being the deepest place the stock can reach. A column total would mix the
        /// floor's stock with the water above it and read the same in a hollow and over a ridge.
        /// </para>
        /// <para>
        /// <b>Allocates its own arrays and is not a per-step call.</b> It is a sampler's reading,
        /// taken at a sample or at the end of a run, in the shape a correlation wants: two arrays
        /// of the same length, one floor height and one stock per live column.
        /// </para>
        /// </remarks>
        public (float[] FloorY, double[] FloorStock) ColumnFloorAndFloorStock()
        {
            int columns = 0;
            for (int i = 0; i < _lowestLive.Length; i++)
            {
                if (_lowestLive[i] >= 0) columns++;
            }

            var floors = new float[columns];
            var stock = new double[columns];
            int n = 0;

            for (int ix = 0; ix < _nx; ix++)
            {
                for (int iz = 0; iz < _nz; iz++)
                {
                    int column = ix * _nz + iz;
                    int lowest = _lowestLive[column];
                    if (lowest < 0) continue;

                    floors[n] = _floorYOfColumn[column];
                    stock[n] = _stock[Index(ix, lowest, iz)];
                    n++;
                }
            }

            return (floors, stock);
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
                    int cell = InColumn(
                        Column((float)(TankRadiusMetres + dx * reach / r)),
                        Column((float)(TankRadiusMetres + dz * reach / r)), iy);

                    if (cell >= 0) return cell;
                }
            }

            int axis = InColumn(Column(TankRadiusMetres), Column(TankRadiusMetres), iy);
            if (axis >= 0) return axis;

            // The cell holding the axis is live in any tank wider than a cell and a half, which is
            // every tank a round would run; the scan is here so that a small one answers rather
            // than putting stock in a dead cell, where it would sit outside every sum for the rest
            // of the run and break the audit quietly.
            for (int i = 0; i < _lowestLive.Length; i++)
            {
                int cell = InColumn(i / _nz, i % _nz, iy);
                if (cell >= 0) return cell;
            }

            return Index(Column(TankRadiusMetres), iy, Column(TankRadiusMetres));
        }

        /// <summary>
        /// The cell this column holds at this layer or at its floor, or −1 when the column holds
        /// no water at all.
        /// </summary>
        /// <remarks>
        /// <b>Clamped up to the floor rather than refused</b> — D092. Without a bed the two are
        /// the same thing, because a live column is water to the last layer and the layer index is
        /// already clamped to the array. With one, a point below the floor is a deposit at a body
        /// that has settled into the sand or a corpse at the bed, and the honest cell for it is
        /// the water immediately above the floor: refusing would take a run down over a
        /// depenetration, and answering with the dead cell would put that stock outside every sum
        /// for the rest of the run, which is exactly what <see cref="TankCellAt"/>'s own remarks
        /// refuse to do at the glass.
        /// </remarks>
        private int InColumn(int ix, int iz, int iy)
        {
            int lowest = _lowestLive[ix * _nz + iz];
            if (lowest < 0) return -1;

            return Index(ix, iy < lowest ? iy : lowest, iz);
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

        /// <summary>
        /// Writes the whole of what this field is: the cells' stock and the running total.
        /// </summary>
        /// <remarks>
        /// <b>Everything else here is geometry, a per-step scratch buffer or a cache built once
        /// from the current.</b> The masks, the patch map, the flux buffers, the velocity tables
        /// and the hoisted bed and streams terms are all rebuilt by the constructor from the
        /// config and the seed, which is what a restore reconstructs the world with. The demand
        /// and availability buffers live inside one metabolic step and are cleared by
        /// <c>ClearDemand</c> before the next reads them, so a checkpoint taken between steps
        /// carries nothing of them; the frozen flag is put back to false for the same reason.
        /// The running total is written rather than re-summed, because a sum over a hundred and
        /// seventy thousand doubles taken in the same order is the same number but a sum taken
        /// by a different route is not, and this one is compared against an audit.
        /// </remarks>
        internal void WriteState(System.IO.BinaryWriter w)
        {
            StateIo.Tag(w, "GRID");
            w.Write(_stock.Length);
            for (int i = 0; i < _stock.Length; i++) w.Write(_stock[i]);
            w.Write(_total);
        }

        /// <summary>Puts the cells back. See <see cref="WriteState"/> for what is not here.</summary>
        internal void ReadState(System.IO.BinaryReader r)
        {
            StateIo.Tag(r, "GRID");

            int count = r.ReadInt32();
            if (count != _stock.Length)
            {
                throw new System.IO.InvalidDataException(
                    "The checkpoint holds " + count + " grid cells and this world has " +
                    _stock.Length + ". The cell size, the box or the mask has moved, which is a " +
                    "different world and not a different moment in this one.");
            }

            for (int i = 0; i < count; i++) _stock[i] = r.ReadDouble();
            _total = r.ReadDouble();

            System.Array.Clear(_demand, 0, _demand.Length);
            _frozen = false;
        }

        /// <summary>Re-sums the cells into the running total and returns it.</summary>
        public double Recount()
        {
            double sum = 0.0;
            for (int i = 0; i < _stock.Length; i++) sum += _stock[i];
            _total = sum;
            return sum;
        }

        /// <summary>
        /// How far the field is from well mixed: the population standard deviation of the live
        /// cells' densities over their mean, 0 when the mean is 0.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It reads <see cref="_stock"/> and nothing else, in one pass, allocating nothing and
        /// writing nothing, so the sampler can ask it at every sample without touching a
        /// trajectory. Stocks give the same figure densities would, because every cell holds the
        /// same volume and the volume cancels between the deviation and the mean.
        /// </para>
        /// <para>
        /// Dead cells are skipped exactly as <see cref="SeedUniform"/> and <see cref="Mix"/> skip
        /// them. A tank's array is the bounding square and holds zeros outside the disc, so a
        /// reading over the whole array would report a perfectly uniform tank as badly patchy.
        /// </para>
        /// </remarks>
        public double DensityCoefficientOfVariation()
        {
            // Welford's running mean and sum of squared deviations rather than the mean square
            // less the square of the mean. The second form loses every digit the two terms share,
            // and on a uniform field they share all of them: it returns a deviation of about 1e-8
            // of the mean where the honest answer is 0, which is the one reading this instrument
            // most needs to get exactly right.
            int n = 0;
            double mean = 0.0;
            double sumSquaredDeviations = 0.0;

            for (int i = 0; i < _stock.Length; i++)
            {
                if (_live != null && !_live[i]) continue;

                double s = _stock[i];
                n++;
                double before = s - mean;
                mean += before / n;
                sumSquaredDeviations += before * (s - mean);
            }

            if (n == 0 || !(mean > 0.0)) return 0.0;
            if (!(sumSquaredDeviations > 0.0)) return 0.0;

            return Math.Sqrt(sumSquaredDeviations / n) / mean;
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
        /// Seeds a total as islands and deserts — D109. The columns whose map value is in the top
        /// <paramref name="cover"/> of the live columns hold matter, uniform down the column to
        /// <paramref name="depthMetres"/> (0: to the bed) and level across the island but for a
        /// short ramp at the shore; the rest hold none; the amounts are scaled so that the live
        /// cells hold exactly <paramref name="totalJoules"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The threshold is a quantile of the live columns, not a number on the map.</b> Noise
        /// has no fixed distribution over a finite disc, so "above 0.3" covers a different share
        /// of every seed's tank; a quantile covers the share asked for on every seed, to within
        /// a column. The islands' shape is then the map's, and only their number of cells is the
        /// tunable's.
        /// </para>
        /// <para>
        /// <b>A plateau, not a peak.</b> A column's weight is its excess over the threshold
        /// divided by a quarter of the range above it, clipped at 1: the outer quarter of an
        /// island ramps up from its shore and the rest is level. The first profile weighted a
        /// column by its excess alone, and put the budget on the peaks: the fullest cell held
        /// twice the cover's density and the median founder's cell half of it, and no founder
        /// had round 44's water to found in (scratch/r45-build/runs/bigF, the first island
        /// smoke at the ruled stirring). On a plateau every island column holds the budget over
        /// the cover's volume, which at cover 0.1 and round 44's budget in a ten-times tank is
        /// round 44's density.
        /// </para>
        /// <para>
        /// <b>Exactly the total.</b> The weights are summed over the live cells in double and each
        /// cell takes <c>total × weight / sum</c>, so the field holds the budget to the rounding
        /// of one division per cell, as <see cref="SeedUniform"/> holds it. Returns the
        /// threshold, for the header and the tests.
        /// </para>
        /// </remarks>
        public float SeedIslands(double totalJoules, Func<float, float, float> map, float cover, float depthMetres = 0f)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!(cover > 0f) || cover > 1f)
                throw new ArgumentOutOfRangeException(nameof(cover), cover, "A cover is a share in (0, 1].");
            if (!(depthMetres >= 0f))
                throw new ArgumentOutOfRangeException(nameof(depthMetres), depthMetres, "An island depth is not negative; 0 is the whole column.");
            if (!(totalJoules > 0d)) return 0f;

            // The layers an island fills: every one to the bed, or those whose top lies above the
            // depth asked (at least the first, so a depth under one cell still seeds something).
            int layers = depthMetres > 0f ? Math.Max(1, (int)Math.Ceiling(depthMetres / CellMetres - 1e-4f)) : _ny;
            if (layers > _ny) layers = _ny;

            // The map at every live column's centre, once.
            var value = new float[_layerStride];
            var liveColumns = new List<int>();

            for (int ix = 0; ix < _nx; ix++)
            {
                float cx = (ix + 0.5f) * CellMetres;

                for (int iz = 0; iz < _nz; iz++)
                {
                    int column = ix * _nz + iz;
                    if (_live != null && _lowestLive[column] < 0) continue;

                    float cz = (iz + 0.5f) * CellMetres;
                    value[column] = map(cx, cz);
                    liveColumns.Add(column);
                }
            }

            if (liveColumns.Count == 0) return 0f;

            // The threshold: the value below which (1 − cover) of the live columns fall. Sorted
            // ascending, so the index is the first island column.
            var sorted = new float[liveColumns.Count];
            for (int i = 0; i < sorted.Length; i++) sorted[i] = value[liveColumns[i]];
            Array.Sort(sorted);

            int first = (int)Math.Floor((1d - cover) * sorted.Length);
            if (first >= sorted.Length) first = sorted.Length - 1;
            if (first < 0) first = 0;
            float threshold = sorted[first];

            // The plateau's ramp: a quarter of the range above the threshold. A column at or
            // past it weighs 1; a column at the shore weighs its excess over the ramp.
            float ramp = 0.25f * (sorted[sorted.Length - 1] - threshold);
            float Weight(int column)
            {
                float excess = value[column] - threshold;
                if (!(excess > 0f)) return 0f;
                return ramp > 0f && excess < ramp ? excess / ramp : 1f;
            }

            // Weights over the live cells, summed in double.
            double sum = 0d;
            for (int c = 0; c < liveColumns.Count; c++)
            {
                int column = liveColumns[c];
                float excess = Weight(column);
                if (!(excess > 0f)) continue;

                int ix = column / _nz;
                int iz = column % _nz;
                for (int iy = 0; iy < layers; iy++)
                {
                    int cell = Index(ix, iy, iz);
                    if (_live != null && !_live[cell]) continue;
                    sum += excess;
                }
            }

            if (!(sum > 0d))
            {
                // A flat map (every column equal): nothing is above the threshold, so the seed
                // falls back to uniform rather than placing nothing.
                SeedUniform((float)(totalJoules / LiveVolumeCubicMetres));
                return threshold;
            }

            double placed = 0d;
            for (int c = 0; c < liveColumns.Count; c++)
            {
                int column = liveColumns[c];
                float excess = Weight(column);
                if (!(excess > 0f)) continue;

                int ix = column / _nz;
                int iz = column % _nz;
                double each = totalJoules * excess / sum;

                for (int iy = 0; iy < layers; iy++)
                {
                    int cell = Index(ix, iy, iz);
                    if (_live != null && !_live[cell]) continue;
                    _stock[cell] += each;
                    placed += each;
                }
            }

            _total += placed;
            return threshold;
        }

        /// <summary>Whether a column holds any water: true everywhere in a box, inside the circle in a tank.</summary>
        public bool ColumnIsLive(int ix, int iz) => _live == null || _lowestLive[ix * _nz + iz] >= 0;

        /// <summary>The column a position stands in, as an index into the <c>_nx × _nz</c> plane.</summary>
        private int ColumnAt(float x, float z) =>
            CellAt(new Float3(x, -0.5f * CellMetres, z)) % _layerStride;

        /// <summary>The stock in the column under a position, J, summed over its live cells.</summary>
        public double ColumnStockAt(float x, float z)
        {
            int column = ColumnAt(x, z);
            int ix = column / _nz;
            int iz = column % _nz;

            double total = 0d;
            for (int iy = 0; iy < _ny; iy++)
            {
                int cell = Index(ix, iy, iz);
                if (_live != null && !_live[cell]) continue;
                total += _stock[cell];
            }

            return total;
        }

        /// <summary>The fullest column's stock, J — what <see cref="ColumnStockAt"/> is a share of.</summary>
        public double MaxColumnStock()
        {
            double max = 0d;

            for (int column = 0; column < _layerStride; column++)
            {
                if (_live != null && _lowestLive[column] < 0) continue;

                int ix = column / _nz;
                int iz = column % _nz;

                double total = 0d;
                for (int iy = 0; iy < _ny; iy++)
                {
                    int cell = Index(ix, iy, iz);
                    if (_live != null && !_live[cell]) continue;
                    total += _stock[cell];
                }

                if (total > max) max = total;
            }

            return max;
        }

        /// <summary>
        /// Every cell's stock as floats in index order (<c>(iy × nx + ix) × nz + iz</c>), for a
        /// recording; a dead cell reads 0.
        /// </summary>
        public void CopyStockTo(float[] into)
        {
            if (into == null || into.Length < _stock.Length)
                throw new ArgumentException("The array is shorter than the field.", nameof(into));

            for (int i = 0; i < _stock.Length; i++) into[i] = (float)_stock[i];
        }

        /// <summary>Every column's stock as floats in column order (<c>ix × nz + iz</c>), for a recording.</summary>
        public void CopyColumnStockTo(float[] into)
        {
            if (into == null || into.Length < _layerStride)
                throw new ArgumentException("The array is shorter than the plane.", nameof(into));

            Array.Clear(into, 0, _layerStride);

            for (int iy = 0; iy < _ny; iy++)
            {
                int layer = iy * _layerStride;
                for (int column = 0; column < _layerStride; column++)
                {
                    into[column] += (float)_stock[layer + column];
                }
            }
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

            // Each layer writes its own slice of the flux buffer, value or zero, so there is no
            // clearing pass and the layers are independent of one another. Nothing here reads a
            // cell another layer writes: the flux out of a cell is a function of that cell alone.
            Parallelism.ForRanges(_ny, (from, to) =>
            {
                double[] stock = _stock, fluxY = _fluxY;
                int[] lowest = _lowestLive;

                for (int iy = from; iy < to; iy++)
                {
                    bool hasBelow = iy < _ny - 1;

                    for (int ix = 0; ix < _nx; ix++)
                    {
                        int row = (iy * _nx + ix) * _nz;
                        int lowRow = ix * _nz;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = row + iz;

                            // Nothing sinks into rock. On a flat bed the test is free — a column
                            // is water all the way down to the last layer, so every cell above the
                            // last passes it and the arithmetic is the one this line always ran —
                            // and with a bed it is what keeps spec item 9's rule: what reaches the
                            // lowest live cell of a column stays there, whichever layer that is.
                            fluxY[cell] = hasBelow && iy < lowest[lowRow + iz]
                                ? stock[cell] * fraction
                                : 0d;
                        }
                    }
                }
            });

            ApplyDown(_fluxY);
        }

        /// <summary>
        /// Decays every live cell's charged stock into <paramref name="spent"/> — D098's leg 8.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Every cell, not the floor's.</b> D051's leg moved a field's floor stock into the
        /// layer above it and this one moves charged matter into spent matter wherever it is, so
        /// the loop that used to touch one cell per column touches them all. On round 40's tank —
        /// 2,200 m² over 45 m at a metre, about 99,000 cells, twice a second — that is the
        /// world's largest per-step loop after the transport passes, which is why the deposit
        /// below is aggregated per spent cell rather than made one fine cell at a time.
        /// </para>
        /// <para>
        /// <b>The units land where the joules left, at the spent field's own resolution.</b> A
        /// charged cell is a metre across and a spent cell five, so sixty-odd charged cells share
        /// one spent cell and the aggregation is exact: the sum of what they lost, deposited once
        /// at a point inside the spent cell they all sit in.
        /// </para>
        /// </remarks>
        public double Remineralise(IMatterField spent, double seconds, float ratePerSecond, float joulesPerUnit)
        {
            if (spent == null) throw new ArgumentNullException(nameof(spent));
            if (ReferenceEquals(spent, this))
            {
                throw new ArgumentException(
                    "A field cannot remineralise into itself: the joules would leave the world " +
                    "and the units would arrive in the same stock they left.", nameof(spent));
            }
            if (!(joulesPerUnit > 0f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(joulesPerUnit), joulesPerUnit,
                    "A charged unit carries a positive number of joules, or the units returned " +
                    "to the water are an infinity of them.");
            }
            if (!(ratePerSecond > 0f) || !(seconds > 0d)) return 0d;

            double fraction = 1.0 - Math.Exp(-ratePerSecond * seconds);

            // The bucket a fine cell's loss is added to: the spent field's own cell when it is a
            // grid, and one bucket per column of this field otherwise. Either way the deposit is
            // made once per bucket, with a point the bucket's own field resolves for itself.
            float spentCell = spent is GridField spentGrid ? spentGrid.CellMetres : CellMetres;
            EnsureRemineralisationBuckets(spentCell);

            double total = 0d;

            // Two passes, and the split is exactly the rule: what one cell loses is a function of
            // that cell alone, so it is computed and taken in parallel; the running total and the
            // per-bucket sums are float additions whose answer depends on the order they are made
            // in, so they are made afterwards in the order the single-threaded walk made them.
            if (_moved == null || _moved.Length != _stock.Length) _moved = new double[_stock.Length];
            double[] lostPerCell = _moved;

            Parallelism.ForRanges(_ny, (from, to) =>
            {
                bool[] live = _live;
                double[] stockOf = _stock;

                for (int iy = from; iy < to; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        // A layer's cells run contiguously in iz — Index is (iy*nx+ix)*nz+iz — so
                        // the row's first cell is the whole of the index arithmetic.
                        int row = (iy * _nx + ix) * _nz;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = row + iz;

                            if (live != null && !live[cell]) { lostPerCell[cell] = 0d; continue; }

                            double stock = stockOf[cell];
                            if (!(stock > 0d)) { lostPerCell[cell] = 0d; continue; }

                            double lost = stock * fraction;
                            stockOf[cell] = stock - lost;
                            lostPerCell[cell] = lost;
                        }
                    }
                }
            });

            // The serial sum, in the order it has always been made in — which is also the order the
            // cells lie in, so the walk is one index and the bucket's three are read from tables
            // built with the buckets. Every value is the integer BucketOf gave.
            double[] perBucket = _remineralisedPerBucket;
            int[] bucketOfX = _bucketOfX, bucketOfY = _bucketOfY, bucketOfZ = _bucketOfZ;

            for (int iy = 0; iy < _ny; iy++)
            {
                int by = bucketOfY[iy] * _bx;

                for (int ix = 0; ix < _nx; ix++)
                {
                    int row = (iy * _nx + ix) * _nz;
                    int bucketRow = (by + bucketOfX[ix]) * _bz;

                    for (int iz = 0; iz < _nz; iz++)
                    {
                        double lost = lostPerCell[row + iz];
                        if (lost == 0d) continue;

                        total += lost;
                        perBucket[bucketRow + bucketOfZ[iz]] += lost;
                    }
                }
            }

            _total -= total;

            for (int i = 0; i < _remineralisedPerBucket.Length; i++)
            {
                double moved = _remineralisedPerBucket[i];
                if (!(moved > 0d)) continue;
                _remineralisedPerBucket[i] = 0d;
                spent.Deposit(BucketCentre(i, spentCell), (float)(moved / joulesPerUnit));
            }

            return total;
        }

        /// <summary>
        /// The scratch the remineralisation pass sums into, one slot per spent cell over this
        /// field's own box. Allocated once and reused, because the pass runs twice a second.
        /// </summary>
        private double[] _remineralisedPerBucket;

        /// <summary>
        /// What each cell lost this pass, held between the parallel take and the serial sum —
        /// see <see cref="Remineralise"/>. Allocated on the first remineralising step and reused.
        /// </summary>
        private double[] _moved;

        private float _bucketMetres;
        private int _bx, _by, _bz;

        /// <summary><see cref="BucketOf"/>'s three axes, tabulated — see EnsureRemineralisationBuckets.</summary>
        private int[] _bucketOfX, _bucketOfY, _bucketOfZ;

        private void EnsureRemineralisationBuckets(float spentCellMetres)
        {
            if (_remineralisedPerBucket != null && _bucketMetres == spentCellMetres) return;

            _bucketMetres = spentCellMetres;
            _bx = Math.Max(1, (int)Math.Ceiling(_nx * (double)CellMetres / spentCellMetres));
            _by = Math.Max(1, (int)Math.Ceiling(_ny * (double)CellMetres / spentCellMetres));
            _bz = Math.Max(1, (int)Math.Ceiling(_nz * (double)CellMetres / spentCellMetres));
            _remineralisedPerBucket = new double[_bx * _by * _bz];

            // The three axes of BucketOf, which are independent of one another and of everything
            // the step does, tabulated once. The sum's walk then costs three loads where it cost
            // three float divides a cell.
            _bucketOfX = new int[_nx];
            _bucketOfY = new int[_ny];
            _bucketOfZ = new int[_nz];

            for (int ix = 0; ix < _nx; ix++)
            {
                _bucketOfX[ix] = Math.Min(_bx - 1, (int)((ix + 0.5f) * CellMetres / spentCellMetres));
            }

            for (int iy = 0; iy < _ny; iy++)
            {
                _bucketOfY[iy] = Math.Min(_by - 1, (int)((iy + 0.5f) * CellMetres / spentCellMetres));
            }

            for (int iz = 0; iz < _nz; iz++)
            {
                _bucketOfZ[iz] = Math.Min(_bz - 1, (int)((iz + 0.5f) * CellMetres / spentCellMetres));
            }
        }

        /// <summary>
        /// The bucket a fine cell falls in: <c>(by·bx + bx)·bz + bz</c> off the three tables
        /// <see cref="EnsureRemineralisationBuckets"/> built. The sum's own walk opens this out,
        /// because two of the three are constants of its outer loops.
        /// </summary>
        private int BucketOf(int ix, int iy, int iz) =>
            (_bucketOfY[iy] * _bx + _bucketOfX[ix]) * _bz + _bucketOfZ[iz];

        private FieldPoint BucketCentre(int bucket, float spentCellMetres)
        {
            int by = bucket / (_bx * _bz);
            int rest = bucket - by * _bx * _bz;
            int bx = rest / _bz;
            int bz = rest - bx * _bz;

            float x = (bx + 0.5f) * spentCellMetres;
            float y = -((by + 0.5f) * spentCellMetres);
            float z = (bz + 0.5f) * spentCellMetres;

            // A bucket whose centre falls on a dead column — outside the glass, or filled by the
            // bed — belongs to no patch, and a cell field would refuse the index. The stock is
            // real and came from live cells inside the bucket, so it is named patch 0 and the
            // receiving field puts it in the nearest water its own arithmetic finds. On a grid
            // the patch is not read at all.
            int patch = PatchOf(x, z);
            return new FieldPoint(new Float3(x, y, z), patch >= 0 ? patch : 0);
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

            // One layer per unit of work, each writing its own slice of the three flux buffers —
            // value where the face is open and zero where it is not, which is what the three
            // clearing passes used to say. Every read is of the stock as the step found it, and
            // the applies below are the only thing that moves any of it, so the layers are
            // independent and the fluxes do not depend on which order they were computed in.
            bool wideX = _nx >= 2, wideZ = _nz >= 2;

            Parallelism.ForRanges(_ny, (from, to) =>
            {
                bool[] live = _live;
                double[] stock = _stock;
                double[] fluxX = _fluxX, fluxY = _fluxY, fluxZ = _fluxZ;
                int[] lowest = _lowestLive;
                bool wraps = Shape != WorldShape.Tank;

                for (int iy = from; iy < to; iy++)
                {
                    bool hasBelow = iy < _ny - 1;

                    for (int ix = 0; ix < _nx; ix++)
                    {
                        // The row, the row below and the row east of it — the same three bases
                        // AssembleFaces takes, and for the same reason.
                        int row = (iy * _nx + ix) * _nz;
                        int below = row + _nx * _nz;
                        int lowRow = ix * _nz;

                        int nextX = ix + 1;
                        if (nextX == _nx) nextX = wraps ? 0 : -1;
                        int eastRow = !wideX || nextX < 0 ? -1 : (iy * _nx + nextX) * _nz;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = row + iz;

                            if (live != null && !live[cell])
                            {
                                fluxX[cell] = 0d;
                                fluxY[cell] = 0d;
                                fluxZ[cell] = 0d;
                                continue;
                            }

                            int east = eastRow < 0 ? -1 : eastRow + iz;
                            if (east >= 0 && live != null && !live[east]) east = -1;

                            fluxX[cell] = east >= 0
                                ? (stock[cell] - stock[east]) * fraction
                                : 0d;

                            int front = -1;
                            if (wideZ)
                            {
                                int nextZ = iz + 1;
                                if (nextZ == _nz) nextZ = wraps ? 0 : -1;
                                if (nextZ >= 0)
                                {
                                    front = row + nextZ;
                                    if (live != null && !live[front]) front = -1;
                                }
                            }

                            fluxZ[cell] = front >= 0
                                ? (stock[cell] - stock[front]) * fraction
                                : 0d;

                            // The floor is a face no stock crosses, and with a bed the floor is
                            // the column's own (D092). On a flat bed the second test is the
                            // first one's restatement, since a live column is water to the last
                            // layer.
                            fluxY[cell] = hasBelow && iy < lowest[lowRow + iz]
                                ? (stock[cell] - stock[below + iz]) * fraction
                                : 0d;
                        }
                    }
                }
            });

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

        /// <summary>
        /// Times the four legs of the conservative transport separately — a profiler's reading,
        /// in milliseconds per iteration.
        /// </summary>
        /// <param name="current">The flow.</param>
        /// <param name="seconds">The world's clock, s.</param>
        /// <param name="dt">Step length, s.</param>
        /// <param name="iterations">How many times to run each leg.</param>
        /// <remarks>
        /// <b>A measurement and not a step.</b> It runs the same four calls
        /// <see cref="AdvectFromThePotential"/> makes and therefore moves the stock as one substep
        /// would, <paramref name="iterations"/> times over; nothing reads the result but a bench.
        /// It exists because the four legs are private and the interesting question — which of
        /// them the world's step is actually spending its half-second in — cannot be asked from
        /// outside otherwise.
        /// </remarks>
        public (double SampleMs, double AssembleMs, double OutflowMs, double ApplyMs)
            ProfileTransport(CurrentField current, double seconds, float dt, int iterations)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));
            if (!current.HasPotential || !(current.Speed > 0f)) return (0d, 0d, 0d, 0d);

            EnsureFaceBuffers();

            long sample = 0L, assemble = 0L, outflow = 0L, apply = 0L;

            for (int i = 0; i < iterations; i++)
            {
                long at = System.Diagnostics.Stopwatch.GetTimestamp();
                SampleEdges(current, seconds);
                long now = System.Diagnostics.Stopwatch.GetTimestamp();
                sample += now - at;

                at = now;
                AssembleFaces();
                now = System.Diagnostics.Stopwatch.GetTimestamp();
                assemble += now - at;

                at = now;
                LargestOutflowFraction(dt);
                now = System.Diagnostics.Stopwatch.GetTimestamp();
                outflow += now - at;

                at = now;
                ApplyFaces(dt);
                now = System.Diagnostics.Stopwatch.GetTimestamp();
                apply += now - at;
            }

            double perTick = 1000d / System.Diagnostics.Stopwatch.Frequency / iterations;

            return (sample * perTick, assemble * perTick, outflow * perTick, apply * perTick);
        }

        /// <summary>
        /// A copy of the east, lower and front face fluxes the last
        /// <see cref="MeasureFaceFluxes"/> or conservative step left standing, m³/s, one entry
        /// per cell. Empty until one has run.
        /// </summary>
        /// <remarks>
        /// A reading and not a handle: the arrays are copied, so nothing a caller does with them
        /// reaches the scheme. It exists because the tuple <see cref="MeasureFaceFluxes"/> returns
        /// is two sums and a worst case, and the identity D092's precomputed bed has to meet is
        /// per face rather than in aggregate.
        /// </remarks>
        public (double[] East, double[] Lower, double[] Front) FaceFluxesForReading()
        {
            if (_faceX == null) return (new double[0], new double[0], new double[0]);

            return ((double[])_faceX.Clone(), (double[])_faceY.Clone(), (double[])_faceZ.Clone());
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

            EnsureEdgeOpenness();
        }

        // Which edges of each lattice have water on all four sides — D106. One bool per entry of
        // the three edge arrays, in the same indexing, so an edge loop reads its own index.
        private bool[] _edgeOpenX;
        private bool[] _edgeOpenY;
        private bool[] _edgeOpenZ;

        /// <summary>
        /// Builds the three edge families' openness masks, once, beside the buffers they parallel.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Geometry, not state.</b> An edge is open when all four cells it belongs to are water,
        /// and which cells are water is <see cref="_live"/>, which the constructor fills and
        /// nothing afterwards writes. So the answer is a property of the box and the mask cannot go
        /// stale; it is built with the buffers and never again. What it replaces is four
        /// <see cref="CellIsWater"/> calls at every edge of every pass — two index multiplications,
        /// a wrap and a load each — with one sequential byte.
        /// </para>
        /// <para>
        /// <b>Every entry the edge loops test, and false everywhere else.</b> The planes those
        /// loops skip (the surface and the floor for the horizontal families) and the far planes a
        /// box copies rather than samples are never read through this, so a false there costs
        /// nothing and asserts nothing. The predicate itself is
        /// <see cref="HorizontalEdgePlane"/>'s and <see cref="VerticalEdgePlane"/>'s, term for
        /// term and in the same order, which is what makes the mask a hoist rather than a second
        /// opinion.
        /// </para>
        /// <para>
        /// <b>What it costs</b> is a byte per edge — about a ninth of what the edge itself takes,
        /// and 5 MB on the largest grid the campaign has run.
        /// </para>
        /// </remarks>
        private void EnsureEdgeOpenness()
        {
            if (_edgeOpenX != null) return;

            var openX = new bool[_edgeX.Length];
            var openY = new bool[_edgeY.Length];
            var openZ = new bool[_edgeZ.Length];

            for (int j = 1; j < _ny; j++)
            {
                for (int i = 0; i < _nx; i++)
                {
                    int row = (j * _nx + i) * (_nz + 1);

                    for (int k = 0; k <= _nz; k++)
                    {
                        openX[row + k] =
                            CellIsWater(i, j - 1, k - 1) && CellIsWater(i, j - 1, k) &&
                            CellIsWater(i, j, k - 1) && CellIsWater(i, j, k);
                    }
                }

                for (int i = 0; i <= _nx; i++)
                {
                    int row = (j * (_nx + 1) + i) * _nz;

                    for (int k = 0; k < _nz; k++)
                    {
                        openZ[row + k] =
                            CellIsWater(i - 1, j - 1, k) && CellIsWater(i - 1, j, k) &&
                            CellIsWater(i, j - 1, k) && CellIsWater(i, j, k);
                    }
                }
            }

            for (int j = 0; j < _ny; j++)
            {
                for (int i = 0; i <= _nx; i++)
                {
                    int row = (j * (_nx + 1) + i) * (_nz + 1);

                    for (int k = 0; k <= _nz; k++)
                    {
                        openY[row + k] =
                            CellIsWater(i - 1, j, k - 1) && CellIsWater(i - 1, j, k) &&
                            CellIsWater(i, j, k - 1) && CellIsWater(i, j, k);
                    }
                }
            }

            _edgeOpenX = openX;
            _edgeOpenY = openY;
            _edgeOpenZ = openZ;
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

        /// <summary>
        /// Whether the shaped floor's per-column numbers are precomputed for the edge lattices
        /// rather than sampled at every edge — D092. True, and the two paths are the same
        /// arithmetic in the same order, so this decides speed and nothing else.
        /// </summary>
        /// <remarks>
        /// It exists so that <c>BedGridTests</c> can measure one grid both ways and assert the
        /// fluxes agree bit for bit. A flat bed takes neither path: it has no column to precompute
        /// and this flag reaches nothing.
        /// </remarks>
        public bool PrecomputeBedColumns { get; set; } = true;

        /// <summary>
        /// Whether the streams' per-column and per-depth terms are precomputed for the edge
        /// lattices rather than derived at every edge — D105. True, and the two paths are the
        /// same arithmetic in the same order and grouping, so this decides speed and memory and
        /// nothing else.
        /// </summary>
        /// <remarks>
        /// It exists for the reason <see cref="PrecomputeBedColumns"/> does: so that one grid can
        /// be measured both ways and the fluxes held against each other bit for bit
        /// (<c>StreamsHoistTests</c>). A box takes neither path — the transport field's potential
        /// is a plain term loop with no polar geometry in it.
        /// </remarks>
        public bool PrecomputeStreamsTerms { get; set; } = true;

        /// <summary>
        /// Builds the three edge families' streams terms for this current, or clears them for
        /// water that has none to give.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What it costs, and why the shaped floor costs so much more.</b> A column's terms are
        /// nineteen doubles and there is one per <c>(x, z)</c> of each lattice — a few megabytes
        /// at any size the campaign runs. The depth terms are the ones to watch. On a flat floor
        /// the depth phase is a function of the height alone, so one row of <c>ny</c> of them
        /// serves the whole tank. On a shaped floor the map reads each column at <c>ŷ = y·D/d</c>
        /// and <c>d</c> is that column's own water depth, so the table is columns × depths: at
        /// 22,000 m² and 1 m cells that is about 5 million entries over the three lattices, some
        /// 120 MB, against the 5 million transcendentals a step it takes out. The grid's own edge
        /// and face buffers are the same order at that size, so this roughly doubles a big grid's
        /// memory. <see cref="PrecomputeStreamsTerms"/> is the way out of it.
        /// </para>
        /// <para>
        /// <b>Built on the bed columns</b>, so a shaped floor's terms want those too; the builder
        /// takes them itself rather than depending on the order <see cref="SampleEdges(CurrentField, double)"/> asks.
        /// </para>
        /// </remarks>
        private void EnsureStreamsTerms(CurrentField current)
        {
            if (ReferenceEquals(_streamsTermsFor, current)) return;

            _streamsTermsFor = null;
            _streamsColumnX = _streamsColumnY = _streamsColumnZ = null;
            _streamsDepthX = _streamsDepthY = _streamsDepthZ = null;
            _streamsStrideX = _streamsStrideY = _streamsStrideZ = 1;
            _streamsDepthStep = 0;

            if (Shape != WorldShape.Tank || !current.HasPotential || !(current.Speed > 0f))
            {
                _streamsTermsFor = current;
                return;
            }

            EnsureBedColumns(current);

            double h = CellMetres;
            bool shaped = current.Bed != null;

            // The x edges sit at ((i+½)h, kh), the z edges at (ih, (k+½)h), the y edges at
            // (ih, kh) — the three lattices EnsureBedColumns walks, in the same order, so a
            // column and its bed column share an index.
            _streamsColumnX = new CurrentField.StreamsColumn[_nx * (_nz + 1)];
            for (int i = 0; i < _nx; i++)
            {
                float x = (float)((i + 0.5) * h);
                for (int k = 0; k <= _nz; k++)
                {
                    _streamsColumnX[i * (_nz + 1) + k] = current.ColumnOf(x, (float)(k * h));
                }
            }

            _streamsColumnZ = new CurrentField.StreamsColumn[(_nx + 1) * _nz];
            for (int i = 0; i <= _nx; i++)
            {
                float x = (float)(i * h);
                for (int k = 0; k < _nz; k++)
                {
                    _streamsColumnZ[i * _nz + k] = current.ColumnOf(x, (float)((k + 0.5) * h));
                }
            }

            _streamsColumnY = new CurrentField.StreamsColumn[(_nx + 1) * (_nz + 1)];
            for (int i = 0; i <= _nx; i++)
            {
                float x = (float)(i * h);
                for (int k = 0; k <= _nz; k++)
                {
                    _streamsColumnY[i * (_nz + 1) + k] = current.ColumnOf(x, (float)(k * h));
                }
            }

            // The horizontal lattices are read at the node planes j = 1 … ny−1 and indexed by
            // j − 1; the vertical one at the layer midpoints j = 0 … ny−1 and indexed by j. Both
            // are the heights the edge loops pass, taken through the same float.
            int horizontalDepths = Math.Max(0, _ny - 1);
            int verticalDepths = _ny;

            if (!shaped)
            {
                _streamsDepthX = new CurrentField.StreamsDepth[horizontalDepths];
                for (int j = 1; j < _ny; j++)
                {
                    _streamsDepthX[j - 1] = current.DepthOf(-(float)(j * h));
                }

                _streamsDepthZ = _streamsDepthX;

                _streamsDepthY = new CurrentField.StreamsDepth[verticalDepths];
                for (int j = 0; j < _ny; j++)
                {
                    _streamsDepthY[j] = current.DepthOf(-(float)((j + 0.5) * h));
                }

                _streamsTermsFor = current;
                return;
            }

            _streamsStrideX = _bedColumnX.Length;
            _streamsStrideY = _bedColumnY.Length;
            _streamsStrideZ = _bedColumnZ.Length;
            _streamsDepthStep = 1;

            _streamsDepthX = BuildShapedDepths(current, _bedColumnX, horizontalDepths, false, h);
            _streamsDepthZ = BuildShapedDepths(current, _bedColumnZ, horizontalDepths, false, h);
            _streamsDepthY = BuildShapedDepths(current, _bedColumnY, verticalDepths, true, h);

            _streamsTermsFor = current;
        }

        /// <summary>
        /// One lattice's depth terms on a shaped floor — every column at every one of its own
        /// mapped heights. Split by column, which is how the array is laid out.
        /// </summary>
        private static CurrentField.StreamsDepth[] BuildShapedDepths(
            CurrentField current, CurrentField.BedColumn[] bed, int depths, bool midpoints, double h)
        {
            int columns = bed.Length;
            var terms = new CurrentField.StreamsDepth[columns * depths];

            // Depth-major, which is the order the edge loops read it in.
            Parallelism.ForRanges(depths, (from, to) =>
            {
                for (int d = from; d < to; d++)
                {
                    float y = midpoints
                        ? -(float)((d + 0.5) * h)
                        : -(float)((d + 1) * h);

                    int at = d * columns;

                    for (int c = 0; c < columns; c++) terms[at + c] = current.DepthOf(bed[c], y);
                }
            });

            return terms;
        }

        /// <summary>
        /// Builds the three edge families' bed columns for this current, or clears them for water
        /// with no shaped floor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The three lattices <see cref="SampleEdges(CurrentField, double)"/> visits.</b> The x edges sit at
        /// <c>((i+½)h, kh)</c> for <c>i &lt; nx</c> and <c>k ≤ nz</c>, the z edges at
        /// <c>(ih, (k+½)h)</c> for <c>i ≤ nx</c> and <c>k &lt; nz</c>, and the y edges at
        /// <c>(ih, kh)</c> for both ≤ n. Three arrays rather than one half-cell lattice because
        /// the loops index them as they stand and a shared lattice would cost an index map at
        /// every edge to save nothing.
        /// </para>
        /// <para>
        /// <b>Sampled at the float the edge loop passes</b>, not at the double the arithmetic would
        /// give: <see cref="CurrentField.ColumnAt"/> takes floats for that reason, so the column is
        /// the floor at the point the direct call would have read.
        /// </para>
        /// </remarks>
        private void EnsureBedColumns(CurrentField current)
        {
            if (ReferenceEquals(_bedColumnsFor, current)) return;

            _bedColumnsFor = null;
            _bedColumnX = null;
            _bedColumnY = null;
            _bedColumnZ = null;

            if (current.Bed != null)
            {
                double h = CellMetres;

                _bedColumnX = new CurrentField.BedColumn[_nx * (_nz + 1)];
                for (int i = 0; i < _nx; i++)
                {
                    float x = (float)((i + 0.5) * h);
                    for (int k = 0; k <= _nz; k++)
                    {
                        _bedColumnX[i * (_nz + 1) + k] = current.ColumnAt(x, (float)(k * h));
                    }
                }

                _bedColumnZ = new CurrentField.BedColumn[(_nx + 1) * _nz];
                for (int i = 0; i <= _nx; i++)
                {
                    float x = (float)(i * h);
                    for (int k = 0; k < _nz; k++)
                    {
                        _bedColumnZ[i * _nz + k] = current.ColumnAt(x, (float)((k + 0.5) * h));
                    }
                }

                _bedColumnY = new CurrentField.BedColumn[(_nx + 1) * (_nz + 1)];
                for (int i = 0; i <= _nx; i++)
                {
                    float x = (float)(i * h);
                    for (int k = 0; k <= _nz; k++)
                    {
                        _bedColumnY[i * (_nz + 1) + k] = current.ColumnAt(x, (float)(k * h));
                    }
                }
            }

            _bedColumnsFor = current;
        }

        /// <summary>Fills the three edge families from the current's potential at one clock.</summary>
        /// <remarks>
        /// <para>
        /// <b>One node plane is one unit of work.</b> Every edge this writes carries the plane's
        /// own <c>j</c> in its index, all three index functions are <c>j</c>-major, and the two
        /// wrap copies a box needs read an edge of the same plane. So the planes are independent:
        /// a slab of them writes a contiguous range of each array and reads nothing another slab
        /// writes. That is what <see cref="Parallelism.ForRanges"/> splits, and why splitting it
        /// cannot move a number — each edge is assigned the value one expression gives it, and
        /// which thread evaluated that expression is not part of the expression.
        /// </para>
        /// <para>
        /// <b>The clock is pinned for the whole pass.</b> <see cref="CurrentField"/> memoises a
        /// clock's trigonometry in a slot it selects by writing to itself, which is not something
        /// several threads may do at once; every sample here is at one clock, so the field is
        /// pinned to it first and is then a pure read. The pin selects the slot an unpinned call
        /// would have selected, so the samples are the same bits.
        /// </para>
        /// <para>
        /// <b>Each slab clears its own range</b> rather than the whole array being cleared first.
        /// The old shape walked about 4 MB of edge buffers with <c>Array.Clear</c> and then walked
        /// them again; this walks them once. A cleared edge is a zero either way.
        /// </para>
        /// </remarks>
        private void SampleEdges(CurrentField current, double seconds)
        {
            CurrentField.BedColumn[] bedX = null, bedY = null, bedZ = null;

            if (PrecomputeBedColumns)
            {
                EnsureBedColumns(current);

                bedX = _bedColumnX;
                bedY = _bedColumnY;
                bedZ = _bedColumnZ;
            }

            if (PrecomputeStreamsTerms)
            {
                EnsureStreamsTerms(current);

                // The streams' hoist reads the bed columns, so it only stands where they do.
                if (_streamsColumnX != null && (current.Bed == null || bedX != null))
                {
                    int step = _streamsDepthStep;

                    SampleEdges(
                        current, seconds,
                        new EdgeTerms(
                            bedX, _streamsColumnX, _streamsDepthX, _streamsStrideX, step),
                        new EdgeTerms(
                            bedY, _streamsColumnY, _streamsDepthY, _streamsStrideY, step),
                        new EdgeTerms(
                            bedZ, _streamsColumnZ, _streamsDepthZ, _streamsStrideZ, step));
                    return;
                }
            }

            SampleEdges(
                current, seconds,
                new EdgeTerms(bedX, null, null, 0, 0),
                new EdgeTerms(bedY, null, null, 0, 0),
                new EdgeTerms(bedZ, null, null, 0, 0));
        }

        /// <summary>
        /// One edge lattice's precomputed terms: the floor's numbers at each <c>(x, z)</c> (D092),
        /// the streams' polar terms there and the depth phases (D105). A null
        /// <see cref="Columns"/> is the unhoisted path.
        /// </summary>
        private readonly struct EdgeTerms
        {
            public EdgeTerms(
                CurrentField.BedColumn[] bed, CurrentField.StreamsColumn[] columns,
                CurrentField.StreamsDepth[] depths, int stride, int step)
            {
                Bed = bed;
                Columns = columns;
                Depths = depths;
                Stride = stride;
                Step = step;
            }

            public CurrentField.BedColumn[] Bed { get; }

            public CurrentField.StreamsColumn[] Columns { get; }

            public CurrentField.StreamsDepth[] Depths { get; }

            /// <summary>What one depth advances the index by: the column count, or 1 when flat.</summary>
            public int Stride { get; }

            /// <summary>What one column advances it by: 1, or 0 when one row serves them all.</summary>
            public int Step { get; }

            // Where the terms for one column at one depth sit is depth * Stride + column * Step.
            // The edge loops open that out themselves — the depth is a constant of the plane, so
            // its half of the product is lifted out of both loops and only the column's is paid.
        }

        private void SampleEdges(
            CurrentField current, double seconds,
            EdgeTerms termsX, EdgeTerms termsY, EdgeTerms termsZ)
        {
            // The y = 0 node plane carries no x or z edge: both fields' potentials have
            // sin(q*pi*y/D) on every horizontal component, so the value there is analytically
            // zero, and an empty plane says so without depending on Math.Sin(-Math.PI) being 0.
            Array.Clear(_edgeX, 0, _nx * (_nz + 1));
            Array.Clear(_edgeZ, 0, (_nx + 1) * _nz);

            // The last node plane, y = -D, is the floor's and carries none either.
            Array.Clear(_edgeX, _nx * _ny * (_nz + 1), _nx * (_nz + 1));
            Array.Clear(_edgeZ, (_nx + 1) * _nz * _ny, (_nx + 1) * _nz);

            current.PinInstant(seconds);

            try
            {
                Parallelism.ForRanges(_ny - 1, (from, to) =>
                {
                    for (int j = from + 1; j <= to; j++)
                    {
                        HorizontalEdgePlane(current, seconds, j, termsX, termsZ);
                    }
                });

                Parallelism.ForRanges(_ny, (from, to) =>
                {
                    for (int j = from; j < to; j++) VerticalEdgePlane(current, seconds, j, termsY);
                });
            }
            finally
            {
                current.UnpinInstant();
            }
        }

        /// <summary>The x and z edges of one interior node plane. <see cref="SampleEdges(CurrentField, double)"/>' body.</summary>
        private void HorizontalEdgePlane(
            CurrentField current, double seconds, int j, EdgeTerms termsX, EdgeTerms termsZ)
        {
            double h = CellMetres;
            bool tank = Shape == WorldShape.Tank;
            float y = -(float)(j * h);

            // The node planes this loop visits are j = 1 … ny − 1; the depth tables are indexed
            // from the first of them.
            int d = j - 1;

            CurrentField.BedColumn[] bedX = termsX.Bed, bedZ = termsZ.Bed;

            // The lattice's own arrays and the terms' index arithmetic, lifted out of the inner
            // loops: every one of them is a constant of the plane, and a constant read once is the
            // same bits as a constant read at every edge.
            double[] edgeX = _edgeX, edgeZ = _edgeZ;
            bool[] openX = _edgeOpenX, openZ = _edgeOpenZ;
            int depthBaseX = d * termsX.Stride, stepX = termsX.Step;
            int depthBaseZ = d * termsZ.Stride, stepZ = termsZ.Step;

            for (int i = 0; i < _nx; i++)
            {
                float x = (float)((i + 0.5) * h);
                int row = (j * _nx + i) * (_nz + 1);
                int columnBase = i * (_nz + 1);

                for (int k = 0; k <= _nz; k++)
                {
                    if (!tank && k == _nz)
                    {
                        edgeX[row + k] = edgeX[row];
                        continue;
                    }

                    // The four cells this edge belongs to. One dead one and the edge is zero,
                    // which is what makes every face onto the glass carry nothing. The four tests
                    // are EnsureEdgeOpenness' one byte, since the mask is the geometry.
                    if (!openX[row + k])
                    {
                        edgeX[row + k] = 0d;
                        continue;
                    }

                    int c = columnBase + k;

                    double value;

                    if (termsX.Columns == null)
                    {
                        value = bedX == null
                            ? current.PotentialAt(x, y, (float)(k * h), seconds).X
                            : current.PotentialAt(
                                  x, y, (float)(k * h), seconds, bedX[c]).X;
                    }
                    else if (bedX == null)
                    {
                        value = current.PotentialAt(
                            termsX.Columns[c], termsX.Depths[depthBaseX + c * stepX], seconds).X;
                    }
                    else
                    {
                        value = current.PotentialAt(
                            termsX.Columns[c], bedX[c],
                            termsX.Depths[depthBaseX + c * stepX], y, seconds).X;
                    }

                    edgeX[row + k] = value * h;
                }
            }

            int zWrapRow = (j * (_nx + 1)) * _nz;

            for (int i = 0; i <= _nx; i++)
            {
                int row = (j * (_nx + 1) + i) * _nz;
                int columnBase = i * _nz;

                for (int k = 0; k < _nz; k++)
                {
                    if (!tank && i == _nx)
                    {
                        edgeZ[row + k] = edgeZ[zWrapRow + k];
                        continue;
                    }

                    if (!openZ[row + k])
                    {
                        edgeZ[row + k] = 0d;
                        continue;
                    }

                    int c = columnBase + k;

                    double value;

                    if (termsZ.Columns == null)
                    {
                        value = bedZ == null
                            ? current.PotentialAt(
                                  (float)(i * h), y, (float)((k + 0.5) * h), seconds).Z
                            : current.PotentialAt(
                                  (float)(i * h), y, (float)((k + 0.5) * h), seconds, bedZ[c]).Z;
                    }
                    else if (bedZ == null)
                    {
                        value = current.PotentialAt(
                            termsZ.Columns[c], termsZ.Depths[depthBaseZ + c * stepZ], seconds).Z;
                    }
                    else
                    {
                        value = current.PotentialAt(
                            termsZ.Columns[c], bedZ[c],
                            termsZ.Depths[depthBaseZ + c * stepZ], y, seconds).Z;
                    }

                    edgeZ[row + k] = value * h;
                }
            }
        }

        /// <summary>
        /// The y edges of one layer, which run inside it rather than across an interface, so every
        /// one of them has a midpoint at an interior depth and none is on a boundary plane.
        /// </summary>
        private void VerticalEdgePlane(
            CurrentField current, double seconds, int j, EdgeTerms termsY)
        {
            double h = CellMetres;
            bool tank = Shape == WorldShape.Tank;
            float y = -(float)((j + 0.5) * h);

            CurrentField.BedColumn[] bedY = termsY.Bed;

            // The plane's constants, lifted for HorizontalEdgePlane's reason.
            double[] edgeY = _edgeY;
            bool[] openY = _edgeOpenY;
            int depthBaseY = j * termsY.Stride, stepY = termsY.Step;

            for (int i = 0; i <= _nx; i++)
            {
                int row = (j * (_nx + 1) + i) * (_nz + 1);
                int columnBase = i * (_nz + 1);

                for (int k = 0; k <= _nz; k++)
                {
                    if (!tank && (i == _nx || k == _nz))
                    {
                        edgeY[row + k] =
                            edgeY[EdgeYIndex(i == _nx ? 0 : i, j, k == _nz ? 0 : k)];
                        continue;
                    }

                    if (!openY[row + k])
                    {
                        edgeY[row + k] = 0d;
                        continue;
                    }

                    int c = columnBase + k;

                    double value;

                    if (termsY.Columns == null)
                    {
                        value = bedY == null
                            ? current.PotentialAt((float)(i * h), y, (float)(k * h), seconds).Y
                            : current.PotentialAt(
                                  (float)(i * h), y, (float)(k * h), seconds, bedY[c]).Y;
                    }
                    else if (bedY == null)
                    {
                        value = current.PotentialAt(
                            termsY.Columns[c], termsY.Depths[depthBaseY + c * stepY], seconds).Y;
                    }
                    else
                    {
                        value = current.PotentialAt(
                            termsY.Columns[c], bedY[c],
                            termsY.Depths[depthBaseY + c * stepY], y, seconds).Y;
                    }

                    edgeY[row + k] = value * h;
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
            // A layer at a time, each writing only its own cells and reading only edges — which
            // nothing here writes. The three clearing passes are folded into the walk: a closed
            // face is written zero where it used to be left zero.
            // Every index below is j-major and then i-major, so a whole row's worth of each is one
            // addition off a base the (ix, iy) loop computes. Twelve index products a cell become
            // eight per row; the doubles they pick out are the ones they always picked out.
            bool wideX = _nx >= 2, wideY = _ny >= 2, wideZ = _nz >= 2;

            Parallelism.ForRanges(_ny, (from, to) =>
            {
                bool[] live = _live;
                double[] edgeX = _edgeX, edgeY = _edgeY, edgeZ = _edgeZ;
                double[] faceX = _faceX, faceY = _faceY, faceZ = _faceZ;
                int[] lowest = _lowestLive;

                for (int iy = from; iy < to; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        int row = (iy * _nx + ix) * _nz;
                        int lowRow = ix * _nz;

                        // EdgeXIndex(ix, ·, 0) on this plane and the one below it.
                        int exHere = (iy * _nx + ix) * (_nz + 1);
                        int exBelow = ((iy + 1) * _nx + ix) * (_nz + 1);

                        // EdgeYIndex(ix, iy, 0) and EdgeYIndex(ix + 1, iy, 0).
                        int eyWest = (iy * (_nx + 1) + ix) * (_nz + 1);
                        int eyEast = eyWest + (_nz + 1);

                        // EdgeZIndex(·, iy + 1, 0) on the plane below, west and east.
                        int ezBelowWest = ((iy + 1) * (_nx + 1) + ix) * _nz;
                        int ezBelowEast = ezBelowWest + _nz;

                        // EdgeZIndex(ix + 1, iy, 0).
                        int ezEast = (iy * (_nx + 1) + ix) * _nz + _nz;

                        // EastOf's wrap and mask, with the row it lands on lifted out of iz.
                        int eastRow = -1;
                        if (wideX)
                        {
                            int next = ix + 1;
                            if (next == _nx) next = Shape == WorldShape.Tank ? -1 : 0;
                            if (next >= 0) eastRow = (iy * _nx + next) * _nz;
                        }

                        bool wraps = Shape != WorldShape.Tank;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = row + iz;

                            if (live != null && !live[cell])
                            {
                                faceX[cell] = 0d;
                                faceY[cell] = 0d;
                                faceZ[cell] = 0d;
                                continue;
                            }

                            int east = eastRow < 0 ? -1 : eastRow + iz;
                            if (east >= 0 && live != null && !live[east]) east = -1;

                            faceX[cell] = east >= 0
                                ? edgeY[eyEast + iz] +
                                  edgeZ[ezEast + iz] -
                                  edgeY[eyEast + iz + 1] -
                                  edgeZ[ezBelowEast + iz]
                                : 0d;

                            int front = -1;
                            if (wideZ)
                            {
                                int next = iz + 1;
                                if (next == _nz) next = wraps ? 0 : -1;
                                if (next >= 0)
                                {
                                    front = row + next;
                                    if (live != null && !live[front]) front = -1;
                                }
                            }

                            faceZ[cell] = front >= 0
                                ? edgeX[exBelow + iz + 1] +
                                  edgeY[eyEast + iz + 1] -
                                  edgeX[exHere + iz + 1] -
                                  edgeY[eyWest + iz + 1]
                                : 0d;

                            // A live cell's lower face is open whenever the cell below it is
                            // water: in a box and a flat tank that is any layer below the last,
                            // and with a bed it stops at the column's own floor. The test is a
                            // saving rather than a correction — all four edges of a face onto rock
                            // touch the dead cell and were left at zero by SampleEdges, so the
                            // circulation would come out zero anyway, which is what keeps the
                            // telescoping exact either way.
                            faceY[cell] = wideY && iy < lowest[lowRow + iz]
                                ? edgeZ[ezBelowEast + iz] +
                                  edgeX[exBelow + iz] -
                                  edgeZ[ezBelowWest + iz] -
                                  edgeX[exBelow + iz + 1]
                                : 0d;
                        }
                    }
                }
            });
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

            // A maximum, not a sum. Each cell's own figure is computed by the expression it always
            // was, and taking the largest of them is a comparison: it has no rounding of its own
            // and does not care what order the candidates arrive in. So a slab may keep its own
            // best and the bests be compared afterwards, and the answer is the same double —
            // which is not true of the totals elsewhere in this file, and is why they are summed
            // serially and this is not.
            // One slot per layer, keyed by the slab's first layer, so no slab has to know its own
            // index and nothing is allocated per step.
            if (_outflowBest == null || _outflowBest.Length != _ny) _outflowBest = new double[_ny];
            double[] best = _outflowBest;
            Array.Clear(best, 0, best.Length);

            Parallelism.ForRanges(_ny, (from, to) =>
            {
                double largest = 0d;
                bool[] live = _live;
                double[] faceX = _faceX, faceY = _faceY, faceZ = _faceZ;
                bool tank = Shape == WorldShape.Tank;

                for (int iy = from; iy < to; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        int row = (iy * _nx + ix) * _nz;
                        int above = row - _nx * _nz;

                        int west = ix == 0 ? (tank ? -1 : _nx - 1) : ix - 1;
                        int westRow = west < 0 ? -1 : (iy * _nx + west) * _nz;

                        int backOfFirst = tank ? -1 : _nz - 1;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = row + iz;
                            if (live != null && !live[cell]) continue;

                            double sum = Math.Abs(faceX[cell]) + Math.Abs(faceZ[cell]) + Math.Abs(faceY[cell]);

                            if (westRow >= 0) sum += Math.Abs(faceX[westRow + iz]);

                            int back = iz == 0 ? backOfFirst : iz - 1;
                            if (back >= 0) sum += Math.Abs(faceZ[row + back]);

                            if (iy > 0) sum += Math.Abs(faceY[above + iz]);

                            double outflow = sum * scale;
                            if (outflow > largest) largest = outflow;
                        }
                    }
                }

                if (from < best.Length) best[from] = largest;
            });

            double answer = 0d;
            for (int s = 0; s < best.Length; s++) if (best[s] > answer) answer = best[s];

            return answer;
        }

        /// <summary>One slot per layer for the outflow maximum — see <see cref="LargestOutflowFraction"/>.</summary>
        private double[] _outflowBest;

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

            // The transfers are built from one snapshot of the stock, so a layer reads only what
            // no layer is writing and writes only its own slice of the three buffers — the
            // clearing passes folded in as the zeros they wrote. The three applies below move the
            // stock and stay serial, because a cell is touched by two of their iterations and
            // which one goes first decides the last bit of the sum.
            Parallelism.ForRanges(_ny, (from, to) =>
            {
                bool[] live = _live;
                double[] stock = _stock;
                double[] faceX = _faceX, faceY = _faceY, faceZ = _faceZ;
                double[] fluxX = _fluxX, fluxY = _fluxY, fluxZ = _fluxZ;
                bool wraps = Shape != WorldShape.Tank;

                for (int iy = from; iy < to; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        // The row's own cells, the row below it, and the row east of it — every
                        // index this loop takes, off three bases instead of nine products.
                        int row = (iy * _nx + ix) * _nz;
                        int below = row + _nx * _nz;

                        int nextX = ix + 1;
                        if (nextX == _nx) nextX = wraps ? 0 : -1;
                        int eastRow = nextX < 0 ? -1 : (iy * _nx + nextX) * _nz;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = row + iz;

                            if (live != null && !live[cell])
                            {
                                fluxX[cell] = 0d;
                                fluxY[cell] = 0d;
                                fluxZ[cell] = 0d;
                                continue;
                            }

                            double q = faceX[cell];
                            if (q != 0d)
                            {
                                // q > 0 takes from this cell, q < 0 from the one east of it, and
                                // writing it as one product keeps the two branches the same
                                // arithmetic. A wall's index is kept at −1 rather than folded
                                // away — and so is a dead neighbour's — so a face that carried a
                                // flux across one still throws here, as it did when this line
                                // called EastOf. What is gone is the two index products, not the
                                // test they guarded.
                                int east = eastRow < 0 ? -1 : eastRow + iz;
                                if (east >= 0 && live != null && !live[east]) east = -1;
                                fluxX[cell] = (q > 0d ? stock[cell] : stock[east]) * q * scale;
                            }
                            else
                            {
                                fluxX[cell] = 0d;
                            }

                            q = faceZ[cell];
                            if (q != 0d)
                            {
                                int nextZ = iz + 1;
                                if (nextZ == _nz) nextZ = wraps ? 0 : -1;
                                int front = nextZ < 0 ? -1 : row + nextZ;
                                if (front >= 0 && live != null && !live[front]) front = -1;
                                fluxZ[cell] = (q > 0d ? stock[cell] : stock[front]) * q * scale;
                            }
                            else
                            {
                                fluxZ[cell] = 0d;
                            }

                            // Positive is downward, which is what ApplyDown means by a flux:
                            // sinking water carries what is above it down, rising water what is
                            // below it up.
                            q = faceY[cell];
                            fluxY[cell] = q != 0d
                                ? (q > 0d ? stock[cell] : stock[below + iz]) * q * scale
                                : 0d;
                        }
                    }
                }
            });

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

        // The three applies are the only passes that move the stock, and each of them touches a
        // cell twice: once to take a flux out of it and once to put its upstream neighbour's in.
        // Which of the two happens first decides the last bit of the sum, so the order a cell's
        // two touches arrive in is part of the arithmetic and is preserved exactly.
        //
        // What is *not* part of the arithmetic is the order two different cells are visited in.
        // A horizontal pass moves stock only inside one layer, so layers are independent and the
        // split is over iy. The vertical pass moves stock only inside one column, so columns are
        // independent and the split is over ix — and within a column the layers still run from the
        // surface down, which is what keeps each cell's pair in its original order.

        private void ApplyEast(double[] flux)
        {
            Parallelism.ForRanges(_ny, (from, to) =>
            {
                double[] stock = _stock;

                for (int iy = from; iy < to; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        int nextX = ix + 1 == _nx ? 0 : ix + 1;
                        int row = (iy * _nx + ix) * _nz;
                        int eastRow = (iy * _nx + nextX) * _nz;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = row + iz;
                            double f = flux[cell];
                            if (f == 0d) continue;
                            stock[cell] -= f;
                            stock[eastRow + iz] += f;
                        }
                    }
                }
            });
        }

        private void ApplyFront(double[] flux)
        {
            Parallelism.ForRanges(_ny, (from, to) =>
            {
                double[] stock = _stock;

                for (int iy = from; iy < to; iy++)
                {
                    for (int ix = 0; ix < _nx; ix++)
                    {
                        int row = (iy * _nx + ix) * _nz;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = row + iz;
                            double f = flux[cell];
                            if (f == 0d) continue;
                            stock[cell] -= f;
                            stock[row + (iz + 1 == _nz ? 0 : iz + 1)] += f;
                        }
                    }
                }
            });
        }

        private void ApplyDown(double[] flux)
        {
            Parallelism.ForRanges(_nx, (fromX, toX) =>
            {
                double[] stock = _stock;
                int layer = _nx * _nz;

                for (int ix = fromX; ix < toX; ix++)
                {
                    for (int iy = 0; iy < _ny - 1; iy++)
                    {
                        int row = (iy * _nx + ix) * _nz;

                        for (int iz = 0; iz < _nz; iz++)
                        {
                            int cell = row + iz;
                            double f = flux[cell];
                            if (f == 0d) continue;
                            stock[cell] -= f;
                            stock[cell + layer] += f;
                        }
                    }
                }
            });
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

        /// <summary>
        /// What is standing on the floor across a patch, J: the lowest live cells of every column
        /// in it, as many deep as the refuge is. D092, <c>logbook/specs/bed-spec.md</c> item 5's
        /// report clause.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Per column rather than per layer, because with a bed those stopped being the same
        /// thing.</b> The report has always read this as <c>StockInLayer(LayerCount − 1)</c>, and
        /// on a flat grid that is exactly what this returns, cell for cell and in the same order:
        /// every column's lowest live cell <i>is</i> the last layer. On a shaped grid the last
        /// layer lies under all but the deepest hollow (<see cref="ArrayDepthMetres"/>), so the old
        /// expression would read a floor of dead cells and report almost nothing while the
        /// sediment sat in the columns above it.
        /// </para>
        /// <para>
        /// <b>As deep as the refuge, and at least one cell.</b> <see cref="RefugeLayerCount"/> is 0
        /// in every run on file (<c>RunConfig.FloorRefugeMetres</c> defaults to 0 and no launcher
        /// has ever set it), at which this is one cell per column and the column named
        /// <c>refuge J</c> keeps the number it has always printed. With a refuge set it is the band
        /// feeding cannot price, measured up from each column's own floor rather than from the
        /// array's — which on a flat grid is the same band and on a shaped one is where the
        /// sediment actually is. ⚠ That is a widening of the old reading for a world with
        /// <c>FloorRefugeMetres</c> above 0, and no such world has been run.
        /// </para>
        /// <para>
        /// A dead column — one the glass or the floor leaves no water in — contributes nothing and
        /// is not an error: <see cref="LowestLiveLayer"/> answers −1 for it.
        /// </para>
        /// </remarks>
        public double RefugeStock(int patch)
        {
            ValidatePatch(patch);

            int band = RefugeLayerCount > 1 ? RefugeLayerCount : 1;
            double sum = 0.0;

            for (int ix = 0; ix < _nx; ix++)
            {
                for (int iz = 0; iz < _nz; iz++)
                {
                    if (PatchOfColumn(ix, iz) != patch) continue;

                    int lowest = _lowestLive[ix * _nz + iz];
                    if (lowest < 0) continue;

                    // Up from the floor, stopping at the top of the column: a band thicker than
                    // the water in a shallow column is the whole of it and not a read past the
                    // surface.
                    int top = lowest - band + 1;
                    if (top < 0) top = 0;

                    for (int iy = lowest; iy >= top; iy--)
                    {
                        if (!IsLive(ix, iy, iz)) break;
                        sum += _stock[Index(ix, iy, iz)];
                    }
                }
            }

            return sum;
        }

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

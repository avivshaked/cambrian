using System;

namespace Evosim.Core
{
    /// <summary>
    /// What the world reads of D194's coral (<c>logbook/specs/coral-build.md</c>, step 2): the
    /// rock's distance for contact, touch, grip, the divergence guard, placement and the grids'
    /// masks; one ellipsoid hull per colony for the current's fade; and a canopy map for the light
    /// and for where corpses come to rest. Built once from the placed <see cref="CoralGeometry"/>
    /// and read only afterwards, so any thread may ask.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The distance.</b> A capsule's is one projection, a clamp and a square root; a plate's is
    /// <c>length((max(|xz| − R, 0), y)) − t/2</c> in its frame. A colony's pieces join by the
    /// mushrooms' cubic smooth minimum, folded in ascending order; colonies join by a plain
    /// minimum, ties to the lower index. Only the value and the gradient are computed: contact
    /// needs no Hessian, and the current's fade reads the hulls instead.
    /// </para>
    /// <para>
    /// <b>The grid lists colonies, not pieces.</b> A smooth minimum folded in order does not give
    /// the same bits when a far piece is left out: the piece can move the running value, which a
    /// later near piece then meets inside the fillet. So a query reads every piece of each colony
    /// it cannot rule out, and rules a colony out by a lower bound: its box's distance, less its
    /// thickest piece and the most the folds can lower the minimum, a sixth of the fillet a fold.
    /// A colony listed in no cell under the query is past the limit by that bound, so the grid
    /// gives a scan's bits whenever the distance is under the limit.
    /// </para>
    /// <para>
    /// <b>The hull's fade.</b> Each colony's hull is the ellipsoid inscribed in its box. Its
    /// distance is the ellipsoid's near-Euclidean estimate <c>k0(k0 − 1)/k1</c>, with
    /// <c>k0 = |p/a|</c> and <c>k1 = |p/a²|</c>: exact along every axis, linear along every ray
    /// from the centre, and smooth away from it, with a closed-form gradient and Hessian. The
    /// scaled sphere's <c>(|p/a| − 1)·min a</c>, which the build spec first named, stretches the
    /// fade by the hull's aspect: a colony ten times wider than tall faded the water 90 m from its
    /// centre at an 8 m fade. The factor is the mushrooms' quintic of <c>s/fade</c> lifted onto the
    /// floor, <c>φ + (1 − φ)·q</c>, so water inside a thicket keeps <c>φ</c> of its speed (D194,
    /// D171). No hull is flatter than <see cref="HullAspect"/>, where the estimate strays. The
    /// product over colonies is taken in index order.
    /// </para>
    /// <para>
    /// <b>In D204's square every reader is periodic.</b> The placement keeps every piece a margin
    /// from the faces, but a body's parts are not folded with its root and a hull's fade can reach
    /// past a face. So a query folds its point into the box, a distance query within its limit of
    /// a face also reads the image across it, the hulls are read by the short way round, and the
    /// canopy's column is the folded point's.
    /// </para>
    /// </remarks>
    public sealed class CoralRock
    {
        private readonly CoralGeometry _coral;
        private readonly double _fillet;

        // The box's sides, or 0 in a tank, where nothing is folded.
        private readonly double _periodX, _periodZ;

        // Each colony's box, its lower bound's allowance below the box, and its hull.
        private readonly double[] _minX, _minY, _minZ, _maxX, _maxY, _maxZ;
        private readonly double[] _allowance;
        private readonly double[] _cx, _cy, _cz, _ax, _ay, _az, _amin;

        // The fade's own lists on the same cells: each cell lists in ascending order every colony whose cull box
        // (its hull's semi-axes times 1 + fade/min a, in which the cull keeps every point it reads) meets the cell,
        // across the square's seams. The edge cells of a tank's grid run on to infinity.
        private readonly int[] _fadeStart;
        private readonly int[] _fadeColonies;

        // How far each hull's fade reaches from its centre along x and along z, m: sampled over the
        // directions and widened by a tenth, for the box's refusal and the grid's column cull.
        private readonly double[] _reachX, _reachZ;

        // The colonies' grid: cells over the footprint's square, each listing in ascending order the
        // colonies whose box, widened by the allowance, meets it (compressed rows).
        private readonly double _cell;
        private readonly int _cellsX, _cellsZ;
        private readonly int[] _cellStart;
        private readonly int[] _cellColonies;

        // The canopy map, on CoralGeometry.CanopyColumnMetres columns over the footprint's square.
        private readonly int _columnsX, _columnsZ;
        private readonly float[] _canopyTop, _transmissionOf, _plateTop;

        /// <summary>
        /// The most a hull's longest axis may be of its shortest: shorter axes are raised to the
        /// longest over this. The distance's estimate <c>k0(k0 − 1)/k1</c> is exact along the axes and
        /// strays off them with the aspect: at an 8 m fade it ends the fade within 10 m of a hull of
        /// aspect 2 in true distance, 13 m at 3 and 42 m at 11, which the test tank's flattest colony
        /// reached. A flat colony's hull is therefore thicker than the colony.
        /// </summary>
        public const double HullAspect = 2d;

        /// <summary>The coral this reads.</summary>
        public CoralGeometry Coral => _coral;

        /// <summary>How many colonies.</summary>
        public int Count => _coral.Count;

        /// <summary>The fade's reach outside a hull, m (<see cref="RunConfig.CoralFadeMetres"/>).</summary>
        public double FadeMetres { get; }

        /// <summary>The fade's floor inside a hull (<see cref="RunConfig.CoralFadeFloor"/>).</summary>
        public double FadeFloor { get; }

        /// <summary>The light a fully covered column passes (<see cref="RunConfig.CoralTransmission"/>).</summary>
        public float Transmission { get; }

        /// <summary>
        /// The readers of a placed coral over a footprint, at the config's fade, floor,
        /// transmission and grid cell.
        /// </summary>
        public CoralRock(CoralGeometry coral, CoralFootprint footprint, RunConfig config)
            : this(coral, footprint, config.CoralFadeMetres, config.CoralFadeFloor, config.CoralTransmission,
                   config.CoralGridCellMetres)
        {
        }

        /// <summary>The readers of a placed coral at explicit dials, for the tests.</summary>
        public CoralRock(CoralGeometry coral, CoralFootprint footprint, float fadeMetres, float fadeFloor,
                         float transmission, float gridCellMetres)
        {
            _coral = coral ?? throw new ArgumentNullException(nameof(coral));
            if (!(fadeMetres > 0f) || !(fadeFloor >= 0f && fadeFloor <= 1f) || !(transmission >= 0f && transmission <= 1f) ||
                !(gridCellMetres > 0f))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"The coral's readers need a fade above 0 m (have {fadeMetres}), a floor and a transmission in ") +
                    FormattableString.Invariant(
                        $"[0, 1] (have {fadeFloor} and {transmission}) and a grid cell above 0 m (have {gridCellMetres})."));
            }

            _fillet = coral.FilletMetres;
            FadeMetres = fadeMetres;
            FadeFloor = fadeFloor;
            Transmission = transmission;

            int n = coral.Count;
            _minX = new double[n]; _minY = new double[n]; _minZ = new double[n];
            _maxX = new double[n]; _maxY = new double[n]; _maxZ = new double[n];
            _allowance = new double[n];
            _cx = new double[n]; _cy = new double[n]; _cz = new double[n];
            _ax = new double[n]; _ay = new double[n]; _az = new double[n]; _amin = new double[n];

            for (int c = 0; c < n; c++)
            {
                coral.Bounds(c, out _minX[c], out _minY[c], out _minZ[c], out _maxX[c], out _maxY[c], out _maxZ[c]);

                CoralGeometry.Colony colony = coral.ColonyAt(c);
                double thickest = 0d;
                for (int k = 0; k < colony.PieceCount; k++)
                {
                    CoralGeometry.Piece p = coral.PieceAt(colony.FirstPiece + k);
                    thickest = Math.Max(thickest, p.Kind == CoralGeometry.PieceKind.Plate ? p.HalfThickness : p.Radius);
                }

                // Inside its box a colony's distance is at least minus its thickest piece; each fold of
                // the smooth minimum lowers the plain minimum by at most a sixth of the fillet.
                _allowance[c] = thickest + (colony.PieceCount - 1) * _fillet / 6d + 1e-9;

                _cx[c] = 0.5d * (_minX[c] + _maxX[c]);
                _cy[c] = 0.5d * (_minY[c] + _maxY[c]);
                _cz[c] = 0.5d * (_minZ[c] + _maxZ[c]);
                _ax[c] = Math.Max(0.25d, 0.5d * (_maxX[c] - _minX[c]));
                _ay[c] = Math.Max(0.25d, 0.5d * (_maxY[c] - _minY[c]));
                _az[c] = Math.Max(0.25d, 0.5d * (_maxZ[c] - _minZ[c]));

                // No axis under 1/HullAspect of the longest, so the distance's estimate holds.
                double longest = Math.Max(_ax[c], Math.Max(_ay[c], _az[c])) / HullAspect;
                _ax[c] = Math.Max(_ax[c], longest);
                _ay[c] = Math.Max(_ay[c], longest);
                _az[c] = Math.Max(_az[c], longest);
                _amin[c] = Math.Min(_ax[c], Math.Min(_ay[c], _az[c]));
            }

            double extentX = footprint.IsBox ? footprint.LengthMetres : 2d * footprint.TankRadiusMetres;
            double extentZ = footprint.IsBox ? footprint.WidthMetres : 2d * footprint.TankRadiusMetres;
            _periodX = footprint.IsBox ? footprint.LengthMetres : 0d;
            _periodZ = footprint.IsBox ? footprint.WidthMetres : 0d;

            // Each fade's reach. Along the ray p = t·n the distance is c0(t·c0 − 1)/c1, with
            // c0 = |n/a| and c1 = |n/a²|, so it meets the fade at t = (1 + fade·c1/c0)/c0.
            _reachX = new double[n];
            _reachZ = new double[n];
            for (int k = 0; k < n; k++)
            {
                for (int i = 0; i <= 64; i++)
                {
                    double polar = Math.PI * i / 64d, ny = Math.Cos(polar), level = Math.Sin(polar);
                    for (int j = 0; j < 128; j++)
                    {
                        double azimuth = 2d * Math.PI * j / 128d;
                        double nx = level * Math.Cos(azimuth), nz = level * Math.Sin(azimuth);
                        double ux = nx / _ax[k], uy = ny / _ay[k], uz = nz / _az[k];
                        double c0 = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                        double c1 = Math.Sqrt(ux * ux / (_ax[k] * _ax[k]) + uy * uy / (_ay[k] * _ay[k]) + uz * uz / (_az[k] * _az[k]));
                        double reach = (1d + fadeMetres * c1 / c0) / c0;
                        _reachX[k] = Math.Max(_reachX[k], reach * Math.Abs(nx));
                        _reachZ[k] = Math.Max(_reachZ[k], reach * Math.Abs(nz));
                    }
                }

                _reachX[k] *= 1.1d;
                _reachZ[k] *= 1.1d;
            }

            // The short way round is the only way in a box, so no hull's fade may reach half way.
            for (int k = 0; k < n && footprint.IsBox; k++)
            {
                if (2d * _reachX[k] >= _periodX || 2d * _reachZ[k] >= _periodZ)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"Colony {k}'s fade reaches {_reachX[k]:0.##} by {_reachZ[k]:0.##} m from its centre, ") +
                        FormattableString.Invariant(
                            $"half the box ({_periodX:0.##} by {_periodZ:0.##} m) or more, so it would meet itself across the seams."));
                }
            }

            // ---- the grid
            _cell = gridCellMetres;
            _cellsX = Math.Max(1, (int)Math.Ceiling(extentX / _cell - 1e-9));
            _cellsZ = Math.Max(1, (int)Math.Ceiling(extentZ / _cell - 1e-9));
            var counts = new int[_cellsX * _cellsZ];
            for (int c = 0; c < n; c++)
            {
                ListedCells(c, out int i0, out int i1, out int j0, out int j1);
                for (int i = i0; i <= i1; i++)
                {
                    for (int j = j0; j <= j1; j++) counts[i * _cellsZ + j]++;
                }
            }

            _cellStart = new int[counts.Length + 1];
            for (int k = 0; k < counts.Length; k++) _cellStart[k + 1] = _cellStart[k] + counts[k];
            _cellColonies = new int[_cellStart[counts.Length]];

            // Colonies in ascending order, so every cell's list ascends.
            var fill = (int[])_cellStart.Clone();
            for (int c = 0; c < n; c++)
            {
                ListedCells(c, out int i0, out int i1, out int j0, out int j1);
                for (int i = i0; i <= i1; i++)
                {
                    for (int j = j0; j <= j1; j++) _cellColonies[fill[i * _cellsZ + j]++] = c;
                }
            }

            // ---- the fade's lists
            var fadeCount = new int[_cellsX * _cellsZ];
            var listX = new bool[n * _cellsX];
            var listZ = new bool[n * _cellsZ];
            for (int k = 0; k < n; k++)
            {
                double edge = 1d + fadeMetres / _amin[k];
                FadeSpan(_cx[k], _ax[k] * edge, _cellsX, _periodX, listX, k * _cellsX);
                FadeSpan(_cz[k], _az[k] * edge, _cellsZ, _periodZ, listZ, k * _cellsZ);
                for (int i = 0; i < _cellsX; i++)
                {
                    if (!listX[k * _cellsX + i]) continue;
                    for (int j = 0; j < _cellsZ; j++)
                    {
                        if (listZ[k * _cellsZ + j]) fadeCount[i * _cellsZ + j]++;
                    }
                }
            }

            _fadeStart = new int[fadeCount.Length + 1];
            for (int m = 0; m < fadeCount.Length; m++) _fadeStart[m + 1] = _fadeStart[m] + fadeCount[m];
            _fadeColonies = new int[_fadeStart[fadeCount.Length]];
            var fadeFill = (int[])_fadeStart.Clone();
            for (int k = 0; k < n; k++)
            {
                for (int i = 0; i < _cellsX; i++)
                {
                    if (!listX[k * _cellsX + i]) continue;
                    for (int j = 0; j < _cellsZ; j++)
                    {
                        if (listZ[k * _cellsZ + j]) _fadeColonies[fadeFill[i * _cellsZ + j]++] = k;
                    }
                }
            }

            // ---- the canopy map
            double column = CoralGeometry.CanopyColumnMetres;
            int across = CoralGeometry.CanopySamplesAcross;
            double sampleMetres = column / across;
            _columnsX = Math.Max(1, (int)Math.Ceiling(extentX / column - 1e-6));
            _columnsZ = Math.Max(1, (int)Math.Ceiling(extentZ / column - 1e-6));
            int samplesX = _columnsX * across, samplesZ = _columnsZ * across;
            var covered = new bool[samplesX * samplesZ];
            _canopyTop = new float[_columnsX * _columnsZ];
            _plateTop = new float[_columnsX * _columnsZ];
            for (int k = 0; k < _canopyTop.Length; k++)
            {
                _canopyTop[k] = float.NegativeInfinity;
                _plateTop[k] = float.NegativeInfinity;
            }

            for (int index = 0; index < coral.PieceCount; index++)
            {
                CoralGeometry.Piece p = coral.PieceAt(index);
                bool plate = p.Kind == CoralGeometry.PieceKind.Plate;
                double reach = plate ? p.Radius + p.HalfThickness : p.Radius;
                double top = plate ? p.AY + p.HalfThickness : Math.Max(p.AY, p.BY) + p.Radius;

                int p0 = Math.Max(0, (int)Math.Floor((Math.Min(p.AX, p.BX) - reach) / sampleMetres - 0.5d));
                int p1 = Math.Min(samplesX - 1, (int)Math.Ceiling((Math.Max(p.AX, p.BX) + reach) / sampleMetres - 0.5d));
                int q0 = Math.Max(0, (int)Math.Floor((Math.Min(p.AZ, p.BZ) - reach) / sampleMetres - 0.5d));
                int q1 = Math.Min(samplesZ - 1, (int)Math.Ceiling((Math.Max(p.AZ, p.BZ) + reach) / sampleMetres - 0.5d));

                double sx = p.BX - p.AX, sz = p.BZ - p.AZ;
                double run2 = sx * sx + sz * sz;
                double reach2 = reach * reach;

                for (int pi = p0; pi <= p1; pi++)
                {
                    double x = (pi + 0.5d) * sampleMetres;
                    for (int qi = q0; qi <= q1; qi++)
                    {
                        double z = (qi + 0.5d) * sampleMetres;
                        double ex = x - p.AX, ez = z - p.AZ;
                        if (run2 > 0d)
                        {
                            double t = (ex * sx + ez * sz) / run2;
                            if (t < 0d) t = 0d;
                            else if (t > 1d) t = 1d;
                            ex -= t * sx;
                            ez -= t * sz;
                        }

                        if (ex * ex + ez * ez > reach2) continue;

                        covered[pi * samplesZ + qi] = true;
                        int col = (pi / across) * _columnsZ + qi / across;
                        if (top > _canopyTop[col]) _canopyTop[col] = (float)top;
                        if (plate && top > _plateTop[col]) _plateTop[col] = (float)top;
                    }
                }
            }

            _transmissionOf = new float[_columnsX * _columnsZ];
            for (int ci = 0; ci < _columnsX; ci++)
            {
                for (int cj = 0; cj < _columnsZ; cj++)
                {
                    int hits = 0;
                    for (int a = 0; a < across; a++)
                    {
                        for (int b = 0; b < across; b++)
                        {
                            if (covered[(ci * across + a) * samplesZ + cj * across + b]) hits++;
                        }
                    }

                    double share = hits / (double)(across * across);
                    _transmissionOf[ci * _columnsZ + cj] = (float)(1d - share * (1d - transmission));
                }
            }
        }

        // Marks the cells along one axis whose span meets [centre − half, centre + half], and in a box its images a
        // period either way. A tank's first and last cells run on to infinity, as FadeCell clamps.
        // The span is widened by a millimetre and a millionth, so that rounding in the cull or in the point's cell (in
        // floats on the card) never reads a list that lacks a hull it keeps; a hull listed and culled adds nothing.
        private void FadeSpan(double centre, double half, int cells, double period, bool[] mark, int at)
        {
            half = half * (1d + 1e-6d) + 1e-3d;
            for (int i = 0; i < cells; i++)
            {
                double lo = i == 0 && !(period > 0d) ? double.NegativeInfinity : i * _cell;
                double hi = i == cells - 1 ? (period > 0d ? period : double.PositiveInfinity) : (i + 1) * _cell;
                for (int shift = -1; shift <= 1; shift++)
                {
                    if (shift != 0 && !(period > 0d)) continue;
                    double c0 = centre + shift * period;
                    if (c0 + half >= lo && c0 - half <= hi) mark[at + i] = true;
                }
            }
        }

        // The cell whose fade list a point reads: its folded place's in a box, its own clamped in a tank.
        private int FadeCell(double x, double z)
        {
            double fx = _periodX > 0d ? Fold(x, _periodX) : x, fz = _periodZ > 0d ? Fold(z, _periodZ) : z;
            int i = Math.Max(0, Math.Min(_cellsX - 1, (int)Math.Floor(fx / _cell)));
            int j = Math.Max(0, Math.Min(_cellsZ - 1, (int)Math.Floor(fz / _cell)));
            return i * _cellsZ + j;
        }

        // The cells a colony is listed in: its box widened by its allowance.
        private void ListedCells(int c, out int i0, out int i1, out int j0, out int j1)
        {
            CellRange(_minX[c] - _allowance[c], _maxX[c] + _allowance[c], _cellsX, out i0, out i1);
            CellRange(_minZ[c] - _allowance[c], _maxZ[c] + _allowance[c], _cellsZ, out j0, out j1);
        }

        private void CellRange(double lo, double hi, int cells, out int first, out int last)
        {
            first = Math.Max(0, Math.Min(cells - 1, (int)Math.Floor(lo / _cell)));
            last = Math.Max(0, Math.Min(cells - 1, (int)Math.Floor(hi / _cell)));
        }

        // ------------------------------------------------------------------ the distance

        /// <summary>The signed distance to the coral, m: negative inside, the minimum over the colonies.</summary>
        public double SignedDistance(double x, double y, double z) =>
            SignedDistance(x, y, z, double.PositiveInfinity, out _, out _);

        /// <summary>
        /// The signed distance to the coral when it is under <paramref name="limit"/>, with the
        /// colony it is nearest and that colony's value and gradient; otherwise a lower bound at or
        /// above the limit, with <paramref name="nearest"/> −1 — <see cref="ReefGeometry"/>'s
        /// contract, the Hessian left 0.
        /// </summary>
        public double SignedDistance(double x, double y, double z, double limit, out int nearest,
                                     out ReefGeometry.Distance at)
        {
            if (double.IsInfinity(limit) || double.IsNaN(limit)) return Scan(x, y, z, limit, out nearest, out at);
            return _periodX > 0d
                ? Periodic(x, y, z, limit, true, out nearest, out at)
                : Query(x, y, z, limit, out nearest, out at);
        }

        // The grid's query at a point in the footprint's frame, unfolded.
        private double Query(double x, double y, double z, double limit, out int nearest, out ReefGeometry.Distance at)
        {

            // The cells under the query's square, widened by nothing: each colony's listing already
            // carries its allowance. A colony is read at the first of its cells inside the window.
            double reach = Math.Max(limit, 0d);
            CellRange(x - reach, x + reach, _cellsX, out int i0, out int i1);
            CellRange(z - reach, z + reach, _cellsZ, out int j0, out int j1);

            nearest = -1;
            at = default(ReefGeometry.Distance);

            int first = -1;
            double firstBound = double.PositiveInfinity;

            for (int i = i0; i <= i1; i++)
            {
                for (int j = j0; j <= j1; j++)
                {
                    int cell = i * _cellsZ + j;
                    for (int e = _cellStart[cell]; e < _cellStart[cell + 1]; e++)
                    {
                        int c = _cellColonies[e];
                        if (!FirstCell(c, i, j, i0, j0)) continue;

                        double bound = LowerBound(c, x, y, z);
                        if (bound < firstBound || (bound == firstBound && c < first))
                        {
                            firstBound = bound;
                            first = c;
                        }
                    }
                }
            }

            if (first < 0 || firstBound >= limit) return first < 0 ? limit : firstBound;

            at = OfColony(first, x, y, z);
            nearest = first;
            double best = at.S;

            for (int i = i0; i <= i1; i++)
            {
                for (int j = j0; j <= j1; j++)
                {
                    int cell = i * _cellsZ + j;
                    for (int e = _cellStart[cell]; e < _cellStart[cell + 1]; e++)
                    {
                        int c = _cellColonies[e];
                        if (c == first || !FirstCell(c, i, j, i0, j0)) continue;

                        double bound = LowerBound(c, x, y, z);
                        if (bound > best || (bound == best && c > nearest)) continue;

                        ReefGeometry.Distance d = OfColony(c, x, y, z);
                        if (d.S < best || (d.S == best && c < nearest))
                        {
                            best = d.S;
                            nearest = c;
                            at = d;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// <see cref="SignedDistance(double, double, double, double, out int, out ReefGeometry.Distance)"/>
        /// over every colony, with no grid: the reference the grid is held to.
        /// </summary>
        public double Scan(double x, double y, double z, double limit, out int nearest, out ReefGeometry.Distance at) =>
            _periodX > 0d
                ? Periodic(x, y, z, limit, false, out nearest, out at)
                : ScanAt(x, y, z, limit, out nearest, out at);

        // In a box: the folded point, then each image across a face within the limit, the least
        // distance kept and a tie kept by the earlier read, so the grid and the scan read alike.
        private double Periodic(double x, double y, double z, double limit, bool grid, out int nearest,
                                out ReefGeometry.Distance at)
        {
            double fx = Fold(x, _periodX), fz = Fold(z, _periodZ);
            double best = grid ? Query(fx, y, fz, limit, out nearest, out at) : ScanAt(fx, y, fz, limit, out nearest, out at);

            double reach = Math.Max(limit, 0d);
            double ox0 = fx < reach ? _periodX : 0d, ox1 = fx > _periodX - reach ? -_periodX : 0d;
            double oz0 = fz < reach ? _periodZ : 0d, oz1 = fz > _periodZ - reach ? -_periodZ : 0d;

            for (int a = 0; a < 3; a++)
            {
                double ox = a == 0 ? 0d : a == 1 ? ox0 : ox1;
                if (a > 0 && ox == 0d) continue;

                for (int b = 0; b < 3; b++)
                {
                    double oz = b == 0 ? 0d : b == 1 ? oz0 : oz1;
                    if (b > 0 && oz == 0d) continue;
                    if (a == 0 && b == 0) continue;

                    double d = grid
                        ? Query(fx + ox, y, fz + oz, limit, out int n, out ReefGeometry.Distance g)
                        : ScanAt(fx + ox, y, fz + oz, limit, out n, out g);
                    if (n >= 0 && (nearest < 0 || d < best))
                    {
                        best = d;
                        nearest = n;
                        at = g;
                    }
                    else if (nearest < 0 && d < best)
                    {
                        best = d;
                    }
                }
            }

            return best;
        }

        // A coordinate folded into [0, period), as the current folds it.
        private static double Fold(double v, double period)
        {
            if (v >= 0d && v < period) return v;
            double folded = v - period * Math.Floor(v / period);
            return folded >= period || folded < 0d ? 0d : folded;
        }

        // The short way from b to a along a periodic side, in the processor's order (d = a − b, then one side).
        private static double ShortWay(double d, double period)
        {
            if (!(period > 0d)) return d;
            double half = 0.5d * period;
            if (d > half) return d - period;
            if (d < -half) return d + period;
            return d;
        }

        // The scan at a point in the footprint's frame, unfolded.
        private double ScanAt(double x, double y, double z, double limit, out int nearest, out ReefGeometry.Distance at)
        {
            nearest = -1;
            at = default(ReefGeometry.Distance);

            int count = Count;
            int first = -1;
            double firstBound = double.PositiveInfinity;

            for (int c = 0; c < count; c++)
            {
                double bound = LowerBound(c, x, y, z);
                if (bound < firstBound)
                {
                    firstBound = bound;
                    first = c;
                }
            }

            if (first < 0 || firstBound >= limit) return firstBound;

            at = OfColony(first, x, y, z);
            nearest = first;
            double best = at.S;

            for (int c = 0; c < count; c++)
            {
                if (c == first) continue;

                double bound = LowerBound(c, x, y, z);
                if (bound > best || (bound == best && c > nearest)) continue;

                ReefGeometry.Distance d = OfColony(c, x, y, z);
                if (d.S < best || (d.S == best && c < nearest))
                {
                    best = d.S;
                    nearest = c;
                    at = d;
                }
            }

            return best;
        }

        /// <summary>Whether a point is inside the coral.</summary>
        public bool Inside(double x, double y, double z) =>
            SignedDistance(x, y, z, 0d, out _, out _) < 0d;

        // Whether (i, j) is the first cell of the query window that lists colony c, so a colony
        // listed in several cells is read once.
        private bool FirstCell(int c, int i, int j, int i0, int j0)
        {
            ListedCells(c, out int ci0, out _, out int cj0, out _);
            return i == Math.Max(i0, ci0) && j == Math.Max(j0, cj0);
        }

        /// <summary>A lower bound on one colony's distance: its box's, less its allowance.</summary>
        private double LowerBound(int c, double x, double y, double z)
        {
            double dx = Math.Max(0d, Math.Max(_minX[c] - x, x - _maxX[c]));
            double dy = Math.Max(0d, Math.Max(_minY[c] - y, y - _maxY[c]));
            double dz = Math.Max(0d, Math.Max(_minZ[c] - z, z - _maxZ[c]));
            return Math.Sqrt(dx * dx + dy * dy + dz * dz) - _allowance[c];
        }

        /// <summary>One colony's distance and gradient: its pieces by the smooth minimum, in order.</summary>
        public ReefGeometry.Distance OfColony(int c, double x, double y, double z)
        {
            CoralGeometry.Colony colony = _coral.ColonyAt(c);
            ReefGeometry.Distance d = OfPiece(_coral.PieceAt(colony.FirstPiece), x, y, z);
            for (int k = 1; k < colony.PieceCount; k++)
            {
                d = SmoothUnion(d, OfPiece(_coral.PieceAt(colony.FirstPiece + k), x, y, z), _fillet);
            }

            return d;
        }

        /// <summary>A piece's distance and gradient: a capsule's or a plate's.</summary>
        public static ReefGeometry.Distance OfPiece(in CoralGeometry.Piece p, double x, double y, double z)
        {
            var d = default(ReefGeometry.Distance);

            if (p.Kind == CoralGeometry.PieceKind.Capsule)
            {
                double bx = p.BX - p.AX, by = p.BY - p.AY, bz = p.BZ - p.AZ;
                double px = x - p.AX, py = y - p.AY, pz = z - p.AZ;
                double bb = bx * bx + by * by + bz * bz;
                double t = bb > 0d ? (px * bx + py * by + pz * bz) / bb : 0d;
                if (t < 0d) t = 0d;
                else if (t > 1d) t = 1d;

                double qx = px - t * bx, qy = py - t * by, qz = pz - t * bz;
                double length = Math.Sqrt(qx * qx + qy * qy + qz * qz);
                d.S = length - p.Radius;
                if (length > 0d)
                {
                    d.Gx = qx / length;
                    d.Gy = qy / length;
                    d.Gz = qz / length;
                }
                else
                {
                    d.Gy = 1d;
                }

                return d;
            }

            double dx = x - p.AX, dy = y - p.AY, dz = z - p.AZ;
            double rho = Math.Sqrt(dx * dx + dz * dz);
            double past = rho - p.Radius;
            if (past > 0d)
            {
                double length = Math.Sqrt(past * past + dy * dy);
                d.S = length - p.HalfThickness;
                d.Gx = past * dx / rho / length;
                d.Gy = dy / length;
                d.Gz = past * dz / rho / length;
                return d;
            }

            d.S = Math.Abs(dy) - p.HalfThickness;
            d.Gy = dy >= 0d ? 1d : -1d;
            return d;
        }

        /// <summary>
        /// The mushrooms' cubic smooth minimum (<see cref="ReefGeometry"/>), value and gradient:
        /// <c>min(a, b) − k·h³/6</c> with <c>h = max(k − |a − b|, 0)/k</c>.
        /// </summary>
        private static ReefGeometry.Distance SmoothUnion(in ReefGeometry.Distance a, in ReefGeometry.Distance b, double k)
        {
            double gap = a.S - b.S;
            double spread = Math.Abs(gap);
            if (spread >= k) return gap <= 0d ? a : b;

            double h = (k - spread) / k;
            double wa = gap < 0d ? 1d - 0.5d * h * h : 0.5d * h * h;
            double wb = 1d - wa;

            var d = default(ReefGeometry.Distance);
            d.S = Math.Min(a.S, b.S) - k * h * h * h / 6d;
            d.Gx = wa * a.Gx + wb * b.Gx;
            d.Gy = wa * a.Gy + wb * b.Gy;
            d.Gz = wa * a.Gz + wb * b.Gz;
            return d;
        }

        // ------------------------------------------------------------------ the hull's fade

        // A hull's p/a at a point, the short way round in a box, and |p/a|². The cull below is
        // exact: k1 ≤ k0/min a, so the distance is at least (k0 − 1)·min a.
        private double HullScaled(int c, double x, double y, double z, out double mx, out double my, out double mz)
        {
            mx = ShortWay(x - _cx[c], _periodX) / _ax[c];
            my = (y - _cy[c]) / _ay[c];
            mz = ShortWay(z - _cz[c], _periodZ) / _az[c];
            return mx * mx + my * my + mz * mz;
        }

        // A hull's distance at a point, m, k0(k0 − 1)/k1, or +∞ where the cull rules the fade out;
        // negative inside. Also k0 and p/a.
        private double HullDistance(int c, double x, double y, double z, out double k0,
                                    out double mx, out double my, out double mz)
        {
            double edge = 1d + FadeMetres / _amin[c];
            double q2 = HullScaled(c, x, y, z, out mx, out my, out mz);
            k0 = Math.Sqrt(q2);
            if (q2 >= edge * edge) return double.PositiveInfinity;
            if (k0 <= 1d) return k0 - 1d;

            double wx = mx / _ax[c], wy = my / _ay[c], wz = mz / _az[c];
            double k1 = Math.Sqrt(wx * wx + wy * wy + wz * wz);
            return k0 * (k0 - 1d) / k1;
        }

        /// <summary>
        /// The current's fade at a point: the product over the colonies of each hull's factor, so
        /// <see cref="FadeFloor"/> inside a hull and 1 beyond the fade from every hull.
        /// </summary>
        public double FadeAt(double x, double y, double z)
        {
            double g = 1d;

            int cell = FadeCell(x, z);
            for (int e = _fadeStart[cell]; e < _fadeStart[cell + 1]; e++)
            {
                int c = _fadeColonies[e];
                double s = HullDistance(c, x, y, z, out _, out _, out _, out _);
                double xi = s / FadeMetres;
                if (xi >= 1d) continue;
                if (xi <= 0d)
                {
                    g *= FadeFloor;
                    continue;
                }

                g *= FadeFloor + (1d - FadeFloor) * (xi * xi * xi * (10d + xi * (-15d + 6d * xi)));
            }

            return g;
        }

        /// <summary>
        /// The fade, its gradient and its Hessian at a point, for the current's <c>g·u + ∇g × A</c>
        /// and its closed-form acceleration — <see cref="ReefGeometry.Fade"/>'s contract. Returns
        /// false, with the fade 1 and every derivative 0, where no hull's fade reaches.
        /// </summary>
        public bool Fade(double x, double y, double z, out ReefGeometry.Distance fade)
        {
            return Fade(x, y, z, out fade, true);
        }

        /// <summary>
        /// <see cref="Fade(double, double, double, out ReefGeometry.Distance)"/> over every colony, with no list:
        /// the reference the fade's lists are held to.
        /// </summary>
        public bool FadeByScan(double x, double y, double z, out ReefGeometry.Distance fade) => Fade(x, y, z, out fade, false);

        private bool Fade(double x, double y, double z, out ReefGeometry.Distance fade, bool listed)
        {
            fade = default(ReefGeometry.Distance);
            fade.S = 1d;
            bool touched = false;
            double floor = FadeFloor, lift = 1d - FadeFloor;

            int cell = FadeCell(x, z);
            int first = listed ? _fadeStart[cell] : 0, end = listed ? _fadeStart[cell + 1] : Count;
            for (int e = first; e < end; e++)
            {
                int c = listed ? _fadeColonies[e] : e;
                double s = HullDistance(c, x, y, z, out double k0, out double mx, out double my, out double mz);
                double xi = s / FadeMetres;
                if (xi >= 1d) continue;

                touched = true;

                var f = default(ReefGeometry.Distance);
                if (xi <= 0d)
                {
                    f.S = floor;
                }
                else
                {
                    // s = A/k1 with A = k0² − k0, k0 = |p/a|, k1 = |p/a²|. With w = p/a² and
                    // v = p/a⁴: ∇k0 = w/k0, ∇k1 = v/k1, ∇∇k0 = diag(1/a²)/k0 − ∇k0∇k0ᵀ/k0,
                    // ∇∇k1 = diag(1/a⁴)/k1 − ∇k1∇k1ᵀ/k1, ∇A = (2k0 − 1)∇k0,
                    // ∇∇A = 2∇k0∇k0ᵀ + (2k0 − 1)∇∇k0, and
                    // ∇∇s = ∇∇A/k1 − (∇A∇k1ᵀ + ∇k1∇Aᵀ)/k1² − A∇∇k1/k1² + 2A∇k1∇k1ᵀ/k1³.
                    double ax2 = _ax[c] * _ax[c], ay2 = _ay[c] * _ay[c], az2 = _az[c] * _az[c];
                    double wx = mx / _ax[c], wy = my / _ay[c], wz = mz / _az[c];
                    double k1 = Math.Sqrt(wx * wx + wy * wy + wz * wz);
                    double a = k0 * (k0 - 1d);
                    double b = 2d * k0 - 1d;

                    double g0x = wx / k0, g0y = wy / k0, g0z = wz / k0;
                    double g1x = wx / ax2 / k1, g1y = wy / ay2 / k1, g1z = wz / az2 / k1;
                    double dax = b * g0x, day = b * g0y, daz = b * g0z;

                    double k1Squared = k1 * k1, k1Cubed = k1Squared * k1;
                    double sx = dax / k1 - a * g1x / k1Squared;
                    double sy = day / k1 - a * g1y / k1Squared;
                    double sz = daz / k1 - a * g1z / k1Squared;

                    double Entry(double diag0, double diag1, double g0i, double g0j, double g1i, double g1j,
                                 double dai, double daj)
                    {
                        double h0 = diag0 / k0 - g0i * g0j / k0;
                        double h1 = diag1 / k1 - g1i * g1j / k1;
                        double ha = 2d * g0i * g0j + b * h0;
                        return ha / k1 - (dai * g1j + g1i * daj) / k1Squared - a * h1 / k1Squared + 2d * a * g1i * g1j / k1Cubed;
                    }

                    double hxx = Entry(1d / ax2, 1d / (ax2 * ax2), g0x, g0x, g1x, g1x, dax, dax);
                    double hyy = Entry(1d / ay2, 1d / (ay2 * ay2), g0y, g0y, g1y, g1y, day, day);
                    double hzz = Entry(1d / az2, 1d / (az2 * az2), g0z, g0z, g1z, g1z, daz, daz);
                    double hxy = Entry(0d, 0d, g0x, g0y, g1x, g1y, dax, day);
                    double hxz = Entry(0d, 0d, g0x, g0z, g1x, g1z, dax, daz);
                    double hyz = Entry(0d, 0d, g0y, g0z, g1y, g1z, day, daz);

                    double rest = 1d - xi;
                    double q = xi * xi * xi * (10d + xi * (-15d + 6d * xi));
                    double q1 = lift * 30d * xi * xi * rest * rest / FadeMetres;
                    double qq = lift * 60d * xi * rest * (1d - 2d * xi) / (FadeMetres * FadeMetres);

                    f.S = floor + lift * q;
                    f.Gx = q1 * sx; f.Gy = q1 * sy; f.Gz = q1 * sz;
                    f.Hxx = qq * sx * sx + q1 * hxx;
                    f.Hxy = qq * sx * sy + q1 * hxy;
                    f.Hxz = qq * sx * sz + q1 * hxz;
                    f.Hyy = qq * sy * sy + q1 * hyy;
                    f.Hyz = qq * sy * sz + q1 * hyz;
                    f.Hzz = qq * sz * sz + q1 * hzz;
                }

                ReefGeometry.Distance p = fade;
                var n = default(ReefGeometry.Distance);
                n.S = p.S * f.S;
                n.Gx = f.S * p.Gx + p.S * f.Gx;
                n.Gy = f.S * p.Gy + p.S * f.Gy;
                n.Gz = f.S * p.Gz + p.S * f.Gz;
                n.Hxx = f.S * p.Hxx + 2d * p.Gx * f.Gx + p.S * f.Hxx;
                n.Hyy = f.S * p.Hyy + 2d * p.Gy * f.Gy + p.S * f.Hyy;
                n.Hzz = f.S * p.Hzz + 2d * p.Gz * f.Gz + p.S * f.Hzz;
                n.Hxy = f.S * p.Hxy + p.Gx * f.Gy + f.Gx * p.Gy + p.S * f.Hxy;
                n.Hxz = f.S * p.Hxz + p.Gx * f.Gz + f.Gx * p.Gz + p.S * f.Hxz;
                n.Hyz = f.S * p.Hyz + p.Gy * f.Gz + f.Gy * p.Gz + p.S * f.Hyz;
                fade = n;
            }

            return touched;
        }

        /// <summary>Whether any hull's fade reaches a point: <see cref="Fade"/>'s answer, without its derivatives.</summary>
        public bool FadeReaches(double x, double y, double z)
        {
            int cell = FadeCell(x, z);
            for (int e = _fadeStart[cell]; e < _fadeStart[cell + 1]; e++)
            {
                int c = _fadeColonies[e];
                if (HullDistance(c, x, y, z, out _, out _, out _, out _) / FadeMetres < 1d) return true;
            }

            return false;
        }

        /// <summary>
        /// Whether any hull's fade may reach the column at <c>(x, z)</c> widened by
        /// <paramref name="widen"/> metres: inside the box of the fade's reach about a hull's centre,
        /// widened. The grid's Courant sampling skips by it.
        /// </summary>
        public bool FadeReachesColumn(double x, double z, double widen)
        {
            for (int c = 0; c < Count; c++)
            {
                if (Math.Abs(ShortWay(x - _cx[c], _periodX)) < _reachX[c] + widen &&
                    Math.Abs(ShortWay(z - _cz[c], _periodZ)) < _reachZ[c] + widen)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ the canopy

        private int ColumnOf(float x, float z)
        {
            double column = CoralGeometry.CanopyColumnMetres;
            double fx = _periodX > 0d ? Fold(x, _periodX) : x, fz = _periodZ > 0d ? Fold(z, _periodZ) : z;
            int i = (int)Math.Floor(fx / column), j = (int)Math.Floor(fz / column);
            if (i < 0 || j < 0 || i >= _columnsX || j >= _columnsZ) return -1;
            return i * _columnsZ + j;
        }

        /// <summary>
        /// The share of the light a point receives past the canopy: its column's transmission below
        /// the canopy's top there, 1 above it and outside the map.
        /// </summary>
        public float LightTransmission(float x, float heightY, float z)
        {
            int col = ColumnOf(x, z);
            if (col < 0 || !(heightY < _canopyTop[col])) return 1f;
            return _transmissionOf[col];
        }

        /// <summary>The highest plate top over a point's column, m, or −∞ where no plate covers it.</summary>
        public float PlateTop(float x, float z)
        {
            int col = ColumnOf(x, z);
            return col < 0 ? float.NegativeInfinity : _plateTop[col];
        }

        /// <summary>The canopy's top over a point's column, m, or −∞ where nothing covers it.</summary>
        public float CanopyTop(float x, float z)
        {
            int col = ColumnOf(x, z);
            return col < 0 ? float.NegativeInfinity : _canopyTop[col];
        }
    }
}

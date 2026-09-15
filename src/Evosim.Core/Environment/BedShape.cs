using System;

namespace Evosim.Core
{
    /// <summary>
    /// The sea floor's own shape: a height map over the tank's disc, drawn from the run's seed and
    /// read in closed form. D092, <c>logbook/specs/bed-spec.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it is for.</b> A flat floor under uniform water gives food no place to gather, so a
    /// stroke or a sense has nothing to buy: round 38's fields read near uniform at a coefficient
    /// of variation of 0.11 to 0.39 once the disc had filled. A shaped bed makes places — hollows
    /// where sinking detritus collects and stays, ridges that shed it, water that slows in the
    /// hollows and quickens over the rises — and every one of those follows from this one function
    /// rather than from a mechanism of its own: the grid masks its cells against it
    /// (<see cref="GridField"/>), the water is pulled through a map built from it
    /// (<see cref="CurrentField"/>), and Unity's collider is a mesh of it.
    /// </para>
    /// <para>
    /// <b>A height map and therefore no caves.</b> One height per column means no overhang, no
    /// vertical wall and no ledge, which is the limit this round accepts (spec item 2) and the
    /// reason the grid needs no cell under anything: a column is water from the surface down to its
    /// own floor and rock below that.
    /// </para>
    /// <para>
    /// <b>Three bands, red.</b> The map is a sum of cosines in three scale bands — the largest
    /// <see cref="ScaleMetres"/> across (about a third of the tank), then that over
    /// <see cref="BandRatio"/>, then over its square — with four modes of random direction, phase
    /// and a jittered wavenumber in each. Amplitude falls as <c>k^-1.5</c>, so there is more relief
    /// at the large scales than at the small, as a sea floor's spectrum has (spec item 1).
    /// <b>Why -1.5 and not the -2 the brief offered:</b> at -2 with bands a factor of three apart
    /// the third band carries 1.2% of the relief, which at a 4 m dial is 5 cm — under the grid's
    /// own cell and under the half-metre column this map is measured on, so the band would exist
    /// only in the arithmetic. At -1 every band contributes the same slope, so the smallest band
    /// decides the 30° bound and eats the large-scale relief the hollows are made of. -1.5 leaves
    /// the three bands at 1 : 0.19 : 0.037 of the amplitude, which is visibly red and still leaves
    /// the ripples a decimetre to stand on.
    /// </para>
    /// <para>
    /// <b>Mean-zero over the disc, by measurement rather than by algebra.</b> A cosine is mean-zero
    /// over a period and not over a circle, so the disc mean is summed on a half-metre lattice at
    /// construction and subtracted once (spec item 5). That is what keeps the water volume and the
    /// seeded matter density at round 38's: the mean depth is still <see cref="DepthMetres"/>.
    /// </para>
    /// <para>
    /// <b>Two dials and a bound.</b> <see cref="RunConfig.BedReliefMetres"/> is the total range of
    /// the cosine bands over the disc and <see cref="RunConfig.BedTiltMetres"/> is the depth
    /// difference along one diameter (spec item 5a), and the relief is scaled down if the two
    /// together would put a slope past <see cref="SteepestSlope"/> — 30 degrees, so bodies settle
    /// and detritus slides rather than sticking to a cliff (spec item 3). When the bound binds,
    /// <see cref="RangeMetres"/> comes out below the dial and <see cref="SlopeBoundBinds"/> says so
    /// rather than the world quietly running a relief nobody asked for.
    /// </para>
    /// <para>
    /// <b>Relief 0 and tilt 0 is the flat world to the bit.</b> <see cref="HasRelief"/> is false,
    /// the mode tables are empty, and <see cref="Height"/>, <see cref="Gradient"/> and
    /// <see cref="Hessian"/> return exact zeros from their first line — not a sum of nothing, which
    /// would be a signed zero and a rounding away from nothing. That is spec item 12: every
    /// recorded world replays.
    /// </para>
    /// <para>
    /// <b>A pure function of its arguments.</b> No memo, no mutable state after construction, so
    /// the grid, the water and a body's placer may read it in any order and in any number and all
    /// get the same floor — the property <see cref="CurrentField"/>'s instant memo deliberately
    /// does not have and has to document.
    /// </para>
    /// </remarks>
    public sealed class BedShape
    {
        /// <summary>Scale bands the map is summed over — spec item 1's three.</summary>
        public const int Bands = 3;

        /// <summary>Modes drawn in each band: enough directions that the sum is not a corrugation.</summary>
        public const int ModesPerBand = 4;

        /// <summary>Wavelength ratio between one band and the next.</summary>
        /// <remarks>
        /// Three, which puts the default bands at about a third of the tank, a couple of metres and
        /// under a metre — spec item 1's three scales, from one dial.
        /// </remarks>
        public const double BandRatio = 3.0;

        /// <summary>Amplitude falls as <c>k</c> to the minus this — the class remarks say why 1.5.</summary>
        public const double SpectrumExponent = 1.5;

        /// <summary>The steepest slope the map is allowed, as a gradient: <c>tan 30°</c>.</summary>
        public const double SteepestSlope = 0.57735026918962576;

        /// <summary>
        /// The share of <see cref="SteepestSlope"/> the fit actually aims at, leaving the rest as
        /// headroom for the slope between two columns of the lattice it is measured on.
        /// </summary>
        /// <remarks>
        /// <b>A bound has to be a bound.</b> The fit reads the slope on the half-metre columns of
        /// <see cref="MeasureStepMetres"/>, and the smallest band's wavelength is under a metre, so
        /// a finer reading finds a little more slope than the fit saw — up to 0.7° in the first
        /// build, which put the steepest column at 30.7° under a rule that says 30. Five per cent
        /// of headroom costs a twentieth of the relief and makes the number in the doc comment the
        /// number a body stands on.
        /// </remarks>
        public const double SlopeFitMargin = 0.95;

        /// <summary>The lattice the disc measurements are taken on, m — spec item 1's column.</summary>
        public const double MeasureStepMetres = 0.5;

        private readonly double[] _kx;
        private readonly double[] _kz;
        private readonly double[] _amplitude;
        private readonly double[] _phase;

        private readonly double _tiltX;
        private readonly double _tiltZ;
        private readonly double _offset;
        private readonly double _radius;

        /// <summary>The tank's radius, m — the disc this map is fitted over.</summary>
        public float RadiusMetres { get; }

        /// <summary>The world's depth, m. The flat bed is at <c>−this</c> and the map is its offset.</summary>
        public float DepthMetres { get; }

        /// <summary>The largest band's wavelength, m — <see cref="RunConfig.BedScaleMetres"/>.</summary>
        public float ScaleMetres { get; }

        /// <summary>The relief asked for, m — <see cref="RunConfig.BedReliefMetres"/>.</summary>
        public float ReliefMetres { get; }

        /// <summary>The tilt asked for, m — <see cref="RunConfig.BedTiltMetres"/>.</summary>
        public float TiltMetres { get; }

        /// <summary>
        /// Whether this bed has any shape at all. False is the flat world, which every reader
        /// short-circuits on so that a flat tank is the arithmetic it always was.
        /// </summary>
        public bool HasRelief { get; }

        /// <summary>The direction the tilt falls along, radians from <c>+x</c> toward <c>+z</c>.</summary>
        public float TiltDirectionRadians { get; }

        /// <summary>The range of the cosine bands over the disc, m — the dial, or less if the slope bound bound.</summary>
        public double RangeMetres { get; }

        /// <summary>The whole map's range over the disc, m: the bands and the tilt together.</summary>
        public double TotalRangeMetres { get; }

        /// <summary>The highest and the lowest floor over the disc, m, as heights above the flat bed.</summary>
        public double HighestMetres { get; }

        /// <summary>The lowest floor over the disc, m, as a height above the flat bed (negative).</summary>
        public double LowestMetres { get; }

        /// <summary>The steepest slope on the disc, radians — under <see cref="SteepestSlope"/> unless the tilt alone is over.</summary>
        public double SteepestSlopeRadians { get; }

        /// <summary>Whether the slope bound cut the relief below <see cref="ReliefMetres"/>.</summary>
        public bool SlopeBoundBinds { get; }

        /// <summary>Hollows on the disc — spec item 4's count, the smoke prints it.</summary>
        public int Hollows { get; }

        /// <summary>Ridges on the disc, counted by the same rule on maxima.</summary>
        public int Ridges { get; }

        /// <summary>
        /// The radius a hollow has to be the lowest point within, m — a quarter of the largest
        /// wavelength.
        /// </summary>
        /// <remarks>
        /// <b>The rule, stated once.</b> A column is a hollow when it is strictly lower than every
        /// lattice column within this radius and the mean height on the ring at that radius stands
        /// at least a quarter of <see cref="RangeMetres"/> above it (spec item 4). A quarter of the
        /// largest wavelength is the separator that distinguishes two basins of that band rather
        /// than two dimples of the smallest: minima of a wave are a wavelength apart, and at a
        /// quarter of one from a trough a cosine has risen by half its peak-to-trough range, which
        /// is twice the threshold — so a real basin is counted comfortably and a ripple in the
        /// third band is not counted at all.
        /// </remarks>
        public double HollowRadiusMetres { get; }

        /// <summary>
        /// Draws the floor.
        /// </summary>
        /// <param name="tankRadiusMetres">The disc's radius, m — <see cref="TankGeometry.RadiusFor"/>.</param>
        /// <param name="depthMetres">The world's depth, m. The mean floor stays here.</param>
        /// <param name="reliefMetres">
        /// <see cref="RunConfig.BedReliefMetres"/>, <c>EVOSIM_BED_RELIEF</c>. The cosine bands'
        /// total range over the disc, before the slope bound.
        /// </param>
        /// <param name="tiltMetres">
        /// <see cref="RunConfig.BedTiltMetres"/>, <c>EVOSIM_BED_TILT</c>. The depth difference
        /// between the deep side and the shallow arc.
        /// </param>
        /// <param name="scaleMetres">
        /// <see cref="RunConfig.BedScaleMetres"/>, <c>EVOSIM_BED_SCALE</c>. The largest band's
        /// wavelength; 0 takes a third of the tank's diameter.
        /// </param>
        /// <param name="seed">
        /// The run's seed, mixed for this field — every seed gets its own floor and a replay gets
        /// the same one (spec item 1).
        /// </param>
        public BedShape(
            float tankRadiusMetres, float depthMetres, float reliefMetres, float tiltMetres,
            float scaleMetres, ulong seed)
        {
            if (!(tankRadiusMetres > 0f) || float.IsInfinity(tankRadiusMetres))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tankRadiusMetres), tankRadiusMetres,
                    "A bed is the floor of a tank, so its radius is positive and finite. " +
                    "logbook/specs/bed-spec.md.");
            }

            if (!(depthMetres > 0f) || float.IsInfinity(depthMetres))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(depthMetres), depthMetres, "A depth is positive and finite.");
            }

            if (!(reliefMetres >= 0f) || float.IsInfinity(reliefMetres))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reliefMetres), reliefMetres,
                    "A relief is finite and not negative; 0 is the flat bed.");
            }

            if (!(tiltMetres >= 0f) || float.IsInfinity(tiltMetres))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tiltMetres), tiltMetres,
                    "A tilt is finite and not negative; 0 is a floor at one depth.");
            }

            if (!(scaleMetres >= 0f) || float.IsInfinity(scaleMetres))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scaleMetres), scaleMetres,
                    "A feature scale is finite and not negative; 0 is a third of the diameter.");
            }

            // Spec item 5a's last clause and item 3's: the floor stays below the surface with a
            // metre to spare, because the beach is a round of its own and needs a dry mask in the
            // grid, a minimum depth for the floor-following current, and rules for a body on sand.
            // The bound is conservative on purpose — the bands' own maximum is under their range
            // and the tilt reaches half its dial at the rim — so a config that passes here cannot
            // break the surface however the seed draws.
            if (reliefMetres + 0.5f * tiltMetres >= depthMetres - 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reliefMetres), reliefMetres,
                    FormattableString.Invariant(
                        $"A relief of {reliefMetres} m with a tilt of {tiltMetres} m could raise ") +
                    FormattableString.Invariant(
                        $"the floor to within {depthMetres - reliefMetres - 0.5f * tiltMetres} m ") +
                    FormattableString.Invariant($"of the surface of a {depthMetres} m world. ") +
                    "The floor must stay below −1 m everywhere: a floor that reaches the lit band " +
                    "is the shelf and a floor that breaks the surface is the beach, and both are " +
                    "rounds of their own (logbook/specs/bed-spec.md).");
            }

            // The tilt's own slope, which nothing below can reduce. ⚠ NOT ON THE BRIEF'S LIST OF
            // REFUSALS, and added because the alternative is worse: the fit scales the cosine
            // bands down until the whole map is under the bound, so a tilt that is over it on its
            // own drives the relief to zero and hands back a bare ramp under the name of a shaped
            // bed. A dial that silently deletes another dial is the fault every refusal in this
            // file exists to prevent. The arithmetic is exact — a tilt of T over a diameter of 2R
            // is a plane of slope T/2R everywhere — so the message can say what would fit.
            if (tiltMetres > 0f &&
                tiltMetres / (2d * tankRadiusMetres) > SteepestSlope)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tiltMetres), tiltMetres,
                    FormattableString.Invariant(
                        $"A tilt of {tiltMetres} m across a tank {2d * tankRadiusMetres:0.##} m ") +
                    FormattableString.Invariant(
                        $"wide is a ramp at {Math.Atan(tiltMetres / (2d * tankRadiusMetres)) * 180d / Math.PI:0.#}") +
                    "°, and the bed's slopes stay under 30° so that bodies settle and detritus " +
                    FormattableString.Invariant(
                        $"slides (logbook/specs/bed-spec.md item 3). This tank admits {2d * tankRadiusMetres * SteepestSlope:0.##} m ") +
                    "of tilt, and that much leaves no slope at all for the relief.");
            }

            RadiusMetres = tankRadiusMetres;
            DepthMetres = depthMetres;
            ReliefMetres = reliefMetres;
            TiltMetres = tiltMetres;

            _radius = tankRadiusMetres;

            ScaleMetres = scaleMetres > 0f
                ? scaleMetres
                : (float)(2d * tankRadiusMetres / 3d);

            HasRelief = reliefMetres > 0f || tiltMetres > 0f;

            if (!HasRelief)
            {
                // The flat world, and every reader below returns before it touches these. Empty
                // rather than zero-filled so that a sum over them cannot even be written.
                _kx = Array.Empty<double>();
                _kz = Array.Empty<double>();
                _amplitude = Array.Empty<double>();
                _phase = Array.Empty<double>();
                HollowRadiusMetres = 0.25 * ScaleMetres;
                return;
            }

            int modes = Bands * ModesPerBand;
            _kx = new double[modes];
            _kz = new double[modes];
            _amplitude = new double[modes];
            _phase = new double[modes];

            var rng = new Rng(seed);

            // The tilt first, so that its direction is the same draw whatever the relief is: two
            // arms of one round that differ only in BedReliefMetres then fall the same way, and
            // the comparison is of the relief rather than of two unrelated floors.
            TiltDirectionRadians = (float)(2d * Math.PI * rng.NextFloat());
            _tiltX = tiltMetres * Math.Cos(TiltDirectionRadians) / (2d * tankRadiusMetres);
            _tiltZ = tiltMetres * Math.Sin(TiltDirectionRadians) / (2d * tankRadiusMetres);

            for (int band = 0; band < Bands; band++)
            {
                double wavelength = ScaleMetres / Math.Pow(BandRatio, band);
                double bandK = 2d * Math.PI / wavelength;

                for (int i = 0; i < ModesPerBand; i++)
                {
                    int m = band * ModesPerBand + i;

                    double theta = 2d * Math.PI * rng.NextFloat();

                    // A fifth either way on the wavenumber, so a band is a band rather than a
                    // single wave with four directions: four modes on one circle of |k| beat into
                    // a pattern that repeats across the disc, and a jittered magnitude does not.
                    double k = bandK * (0.8d + 0.4d * rng.NextFloat());

                    _kx[m] = k * Math.Cos(theta);
                    _kz[m] = k * Math.Sin(theta);
                    _phase[m] = 2d * Math.PI * rng.NextFloat();

                    // Red: the amplitude of the drawn wavenumber, not of the band's nominal one,
                    // so the jitter carries the spectrum with it.
                    _amplitude[m] = Math.Pow(k / (2d * Math.PI / ScaleMetres), -SpectrumExponent);
                }
            }

            // ------------------------------------------------------------------ the fit

            // One pass over the half-metre columns of the disc, holding each column's raw band
            // height and its raw gradient, so that the three fits below — the range, the slope
            // bound and the mean — are arithmetic on stored numbers rather than three more
            // lattices. Spec item 1's column is the half metre, and the map is measured on the
            // resolution it will be built at.
            int side = (int)Math.Ceiling(2d * tankRadiusMetres / MeasureStepMetres);
            if (side < 2) side = 2;

            var rawHeight = new double[side * side];
            var rawGradientX = new double[side * side];
            var rawGradientZ = new double[side * side];
            var columnX = new double[side * side];
            var columnZ = new double[side * side];
            int columns = 0;

            for (int ix = 0; ix < side; ix++)
            {
                double x = (ix + 0.5d) * MeasureStepMetres;

                for (int iz = 0; iz < side; iz++)
                {
                    double z = (iz + 0.5d) * MeasureStepMetres;
                    if (!TankGeometry.Inside(x, z, tankRadiusMetres)) continue;

                    double dx = x - _radius;
                    double dz = z - _radius;
                    double h = 0d, gx = 0d, gz = 0d;

                    for (int m = 0; m < modes; m++)
                    {
                        double chi = _kx[m] * dx + _kz[m] * dz + _phase[m];
                        double cos = Math.Cos(chi);
                        double sin = Math.Sin(chi);

                        h += _amplitude[m] * cos;
                        gx -= _amplitude[m] * _kx[m] * sin;
                        gz -= _amplitude[m] * _kz[m] * sin;
                    }

                    rawHeight[columns] = h;
                    rawGradientX[columns] = gx;
                    rawGradientZ[columns] = gz;
                    columnX[columns] = dx;
                    columnZ[columns] = dz;
                    columns++;
                }
            }

            if (columns == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tankRadiusMetres), tankRadiusMetres,
                    FormattableString.Invariant(
                        $"No half-metre column has its centre inside a tank of radius ") +
                    FormattableString.Invariant($"{tankRadiusMetres} m, so the bed has nothing to ") +
                    "be a map over.");
            }

            // 1. The relief dial: scale the bands so their range over the disc is the number asked
            //    for. The tilt is not in this range — it has its own dial and its own shape, and a
            //    range that mixed the two would make either dial a readout of the other.
            double rawLow = double.MaxValue, rawHigh = double.MinValue;
            for (int i = 0; i < columns; i++)
            {
                if (rawHeight[i] < rawLow) rawLow = rawHeight[i];
                if (rawHeight[i] > rawHigh) rawHigh = rawHeight[i];
            }

            double rawRange = rawHigh - rawLow;
            double alpha = rawRange > 0d && reliefMetres > 0f ? reliefMetres / rawRange : 0d;

            // 2. The slope bound, on the whole map including the tilt: a body settles and detritus
            //    slides on 30 degrees and sticks to a cliff. Bisected rather than solved, because
            //    the steepest column is not the same column at every alpha — the tilt's constant
            //    gradient adds to the bands' at one place and subtracts at another.
            double SteepestAt(double scale)
            {
                double worst = 0d;

                for (int i = 0; i < columns; i++)
                {
                    double gx = scale * rawGradientX[i] + _tiltX;
                    double gz = scale * rawGradientZ[i] + _tiltZ;
                    double slope = Math.Sqrt(gx * gx + gz * gz);
                    if (slope > worst) worst = slope;
                }

                return worst;
            }

            bool binds = false;

            double target = SlopeFitMargin * SteepestSlope;

            if (alpha > 0d && SteepestAt(alpha) > target)
            {
                binds = true;

                double low = 0d, high = alpha;

                // Forty halvings of the interval, which lands the relief to a part in 10^12 of the
                // dial: far finer than the number matters, and cheap because each step is a pass
                // over stored gradients rather than over the trigonometry.
                for (int i = 0; i < 40; i++)
                {
                    double mid = 0.5d * (low + high);
                    if (SteepestAt(mid) > target) high = mid;
                    else low = mid;
                }

                alpha = low;
            }

            SlopeBoundBinds = binds;

            for (int m = 0; m < modes; m++) _amplitude[m] *= alpha;

            // 3. Mean zero over the disc (spec item 5), on the whole map: the cosines are not
            //    mean-zero over a circle and the tilt is only to the lattice's own rounding. This
            //    is what keeps the water volume and the seeded matter density at the flat world's.
            double sum = 0d;
            for (int i = 0; i < columns; i++)
            {
                sum += alpha * rawHeight[i] + _tiltX * columnX[i] + _tiltZ * columnZ[i];
            }

            _offset = sum / columns;

            // ------------------------------------------------------------------ what it came to

            double low2 = double.MaxValue, high2 = double.MinValue;
            double bandLow = double.MaxValue, bandHigh = double.MinValue;

            var height = new double[columns];

            for (int i = 0; i < columns; i++)
            {
                double band = alpha * rawHeight[i];
                double h = band + _tiltX * columnX[i] + _tiltZ * columnZ[i] - _offset;

                height[i] = h;

                if (h < low2) low2 = h;
                if (h > high2) high2 = h;
                if (band < bandLow) bandLow = band;
                if (band > bandHigh) bandHigh = band;
            }

            RangeMetres = bandHigh - bandLow;
            TotalRangeMetres = high2 - low2;
            HighestMetres = high2;
            LowestMetres = low2;

            // At the scale the bands were actually given, not at the one they were drawn with:
            // this is the floor a body walks on.
            SteepestSlopeRadians = Math.Atan(SteepestAt(alpha));

            HollowRadiusMetres = 0.25d * ScaleMetres;

            (int hollows, int ridges) = CountPlaces(height, columnX, columnZ, columns);
            Hollows = hollows;
            Ridges = ridges;
        }

        /// <summary>
        /// Counts the hollows and the ridges by the rule <see cref="HollowRadiusMetres"/> states.
        /// </summary>
        /// <remarks>
        /// One pass per column over its own neighbourhood, which at the campaign's scale is about
        /// forty lattice columns each — a few hundred thousand comparisons once per world, against
        /// the streams' two and a half million samples. A column whose neighbourhood reaches
        /// outside the disc is skipped rather than judged on a partial ring: the glass is not a
        /// rise, and a rim column called a ridge because half its ring is missing would put the
        /// spec's count in the geometry rather than in the floor.
        /// </remarks>
        private (int Hollows, int Ridges) CountPlaces(
            double[] height, double[] columnX, double[] columnZ, int columns)
        {
            double radius = HollowRadiusMetres;
            double radiusSquared = radius * radius;
            double innerSquared = 0.64d * radiusSquared;   // the ring is the outer fifth: 0.8r to r
            double threshold = 0.25d * RangeMetres;

            int hollows = 0, ridges = 0;

            for (int i = 0; i < columns; i++)
            {
                // Inside the disc by the whole neighbourhood, or the ring is a half ring.
                double fromAxis = Math.Sqrt(columnX[i] * columnX[i] + columnZ[i] * columnZ[i]);
                if (fromAxis + radius > _radius) continue;

                bool lowest = true, highest = true;
                double ringSum = 0d;
                int ringCount = 0;

                for (int j = 0; j < columns; j++)
                {
                    if (j == i) continue;

                    double dx = columnX[j] - columnX[i];
                    double dz = columnZ[j] - columnZ[i];
                    double distanceSquared = dx * dx + dz * dz;
                    if (distanceSquared > radiusSquared) continue;

                    if (height[j] <= height[i]) lowest = false;
                    if (height[j] >= height[i]) highest = false;
                    if (!lowest && !highest) break;

                    if (distanceSquared >= innerSquared)
                    {
                        ringSum += height[j];
                        ringCount++;
                    }
                }

                if (ringCount == 0) continue;

                double ring = ringSum / ringCount;

                if (lowest && ring - height[i] >= threshold) hollows++;
                if (highest && height[i] - ring >= threshold) ridges++;
            }

            return (hollows, ridges);
        }

        /// <summary>
        /// The floor's offset from the flat bed at a place, m, positive up. Exactly 0 on a flat
        /// bed.
        /// </summary>
        /// <param name="x">World x, m. The disc's axis is at <c>(R, R)</c>.</param>
        /// <param name="z">World z, m.</param>
        /// <remarks>
        /// <b>Defined outside the disc as well</b>, and deliberately not clamped there. The map is
        /// a sum of cosines and a plane, so it simply carries on past the glass; clamping it would
        /// put a crease at the rim, and a crease in <c>h</c> is a discontinuity in the water's
        /// Jacobian at exactly the place a body is most likely to be pressed against. Nothing in
        /// the world lives out there — the grid's cells are dead and the glass is a collider — so
        /// what the map says past the rim is only ever read by a float error's worth of overshoot.
        /// </remarks>
        public double Height(double x, double z)
        {
            if (!HasRelief) return 0d;

            double dx = x - _radius;
            double dz = z - _radius;
            double h = _tiltX * dx + _tiltZ * dz - _offset;

            for (int m = 0; m < _amplitude.Length; m++)
            {
                h += _amplitude[m] * Math.Cos(_kx[m] * dx + _kz[m] * dz + _phase[m]);
            }

            return h;
        }

        /// <summary>The floor's height in the world at a place, m: <c>−depth + <see cref="Height"/></c>.</summary>
        public double FloorY(double x, double z) => -(double)DepthMetres + Height(x, z);

        /// <summary>The map's slope at a place: <c>∂h/∂x</c> and <c>∂h/∂z</c>, dimensionless.</summary>
        public (double X, double Z) Gradient(double x, double z)
        {
            HeightAndGradient(x, z, out _, out double gx, out double gz);
            return (gx, gz);
        }

        /// <summary>
        /// The height and both first derivatives in one pass — what the water's floor-following map
        /// needs at every sample.
        /// </summary>
        /// <remarks>
        /// One pass rather than two calls, because the sine and the cosine of a mode's phase are
        /// bought together and <see cref="CurrentField"/> asks for this once per part per physics
        /// step. The flat bed returns three exact zeros without touching a table.
        /// </remarks>
        public void HeightAndGradient(
            double x, double z, out double height, out double gradientX, out double gradientZ)
        {
            if (!HasRelief)
            {
                height = 0d;
                gradientX = 0d;
                gradientZ = 0d;
                return;
            }

            double dx = x - _radius;
            double dz = z - _radius;

            double h = _tiltX * dx + _tiltZ * dz - _offset;
            double gx = _tiltX;
            double gz = _tiltZ;

            for (int m = 0; m < _amplitude.Length; m++)
            {
                double chi = _kx[m] * dx + _kz[m] * dz + _phase[m];
                double a = _amplitude[m];
                double cos = Math.Cos(chi);
                double sin = Math.Sin(chi);

                h += a * cos;
                gx -= a * _kx[m] * sin;
                gz -= a * _kz[m] * sin;
            }

            height = h;
            gradientX = gx;
            gradientZ = gz;
        }

        /// <summary>The map's three second derivatives at a place: <c>h_xx</c>, <c>h_xz</c>, <c>h_zz</c>.</summary>
        public (double XX, double XZ, double ZZ) Hessian(double x, double z)
        {
            HeightGradientAndHessian(
                x, z, out _, out _, out _, out double xx, out double xz, out double zz);

            return (xx, xz, zz);
        }

        /// <summary>
        /// Everything about the map at one place, in one pass — what the water's closed-form
        /// acceleration needs.
        /// </summary>
        /// <remarks>
        /// The Hessian is what carries the floor's curvature into <c>(u·∇)u</c>: the map's Jacobian
        /// depends on <c>∇h</c>, so the velocity's Jacobian depends on <c>∇∇h</c>, and a material
        /// derivative built without it would be the flat field's answer wearing the sloped field's
        /// name (<c>logbook/specs/bed-spec.md</c> item 6).
        /// </remarks>
        public void HeightGradientAndHessian(
            double x, double z,
            out double height, out double gradientX, out double gradientZ,
            out double hessianXX, out double hessianXZ, out double hessianZZ)
        {
            if (!HasRelief)
            {
                height = 0d;
                gradientX = 0d;
                gradientZ = 0d;
                hessianXX = 0d;
                hessianXZ = 0d;
                hessianZZ = 0d;
                return;
            }

            double dx = x - _radius;
            double dz = z - _radius;

            double h = _tiltX * dx + _tiltZ * dz - _offset;
            double gx = _tiltX;
            double gz = _tiltZ;
            double xx = 0d, xz = 0d, zz = 0d;

            for (int m = 0; m < _amplitude.Length; m++)
            {
                double chi = _kx[m] * dx + _kz[m] * dz + _phase[m];
                double a = _amplitude[m];
                double cos = Math.Cos(chi);
                double sin = Math.Sin(chi);

                h += a * cos;
                gx -= a * _kx[m] * sin;
                gz -= a * _kz[m] * sin;

                // The tilt is linear and contributes nothing here, which is the whole reason it is
                // a separate term rather than a mode at wavenumber zero.
                xx -= a * _kx[m] * _kx[m] * cos;
                xz -= a * _kx[m] * _kz[m] * cos;
                zz -= a * _kz[m] * _kz[m] * cos;
            }

            height = h;
            gradientX = gx;
            gradientZ = gz;
            hessianXX = xx;
            hessianXZ = xz;
            hessianZZ = zz;
        }

        public override string ToString() =>
            HasRelief
                ? FormattableString.Invariant(
                      $"bed relief {RangeMetres:0.##} m (dial {ReliefMetres:0.##}), ") +
                  FormattableString.Invariant(
                      $"tilt {TiltMetres:0.##} m at {TiltDirectionRadians:0.##} rad, ") +
                  FormattableString.Invariant(
                      $"scale {ScaleMetres:0.##} m, {Hollows} hollows, {Ridges} ridges, ") +
                  FormattableString.Invariant(
                      $"steepest {SteepestSlopeRadians * 180d / Math.PI:0.#}°")
                : "bed flat";
    }
}

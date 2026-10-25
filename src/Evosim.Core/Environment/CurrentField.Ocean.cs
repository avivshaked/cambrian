using System;

namespace Evosim.Core
{
    /// <summary>
    /// D204's open ocean current: <see cref="CurrentMode.Ocean"/>, the water of a box that joins at
    /// both seams. <c>logbook/specs/open-ocean-current.md</c> is its specification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A flat field, carried over the floor by the tank's own machinery.</b> What is written
    /// here is the field over a flat bed: a potential <c>A = (0, A_y, 0)</c> in the flat
    /// coordinates, its curl, and their derivatives. Everything the floor does to it — the
    /// floor-following map, the Piola transform, the shore's fade, the analytic acceleration's
    /// chain rule — is the code the streams already use (<see cref="StreamsSlopedUnit"/> and its
    /// kin), which call <see cref="FlatUnit"/> and its three siblings for the flat field and so
    /// reach this one in a box and the streams in a tank. One map and one fade for both waters
    /// means a property proved of the map holds for both: divergence-free for any floor, no flow
    /// through the bed, and a potential the grid can carry conservatively.
    /// </para>
    /// <para>
    /// <b>The potential.</b> <c>A_y = Σ_v Σ_q W_vq(t)·S_q(η)·cos(k_v·x + φ_vq(t))</c> over 25 wave
    /// vectors <c>k_v</c> and three vertical layers <c>q</c>, 75 modes. <c>η = 1 + ỹ/D</c> is the
    /// mapped height, 0 at the bed and 1 at the surface, and <c>S_q = sin((q − ½)πη)</c> is still at
    /// the bed and moving most at the surface (the coordinator's choice of 2026-10-19, in the spec).
    /// The curl of <c>A_y ŷ</c> is <c>(−∂A_y/∂z, 0, ∂A_y/∂x)</c>: horizontal swirls, layered in
    /// depth, with no vertical motion over a flat bed and the vertical the floor's slope asks for
    /// over a shaped one. Every component of <c>A</c> is zero at the bed, which is what keeps the
    /// grid's floor faces dry; at the surface <c>A_y</c> is not zero and need not be, since a
    /// horizontal face reads only <c>A_x</c> and <c>A_z</c>, and the map's <c>a</c> and <c>b</c>
    /// carry a factor <c>y</c> and vanish there.
    /// </para>
    /// <para>
    /// <b>One sine and one cosine per vector, not per mode.</b> <c>cos(χ + φ) = cos χ·cos φ −
    /// sin χ·sin φ</c>, and <c>φ</c> is a function of the clock alone, so the instant table holds
    /// <c>W·cos φ</c> and <c>W·sin φ</c> for every mode and a sample sums the three layers of a
    /// vector against them before it touches <c>χ</c> at all. That is the card probe's finding
    /// (<c>logbook/specs/open-ocean-mode-probe/</c>), kept on the CPU so the two are one
    /// arithmetic.
    /// </para>
    /// <para>
    /// <b>Folded before it is read.</b> Every entry folds <c>x</c> and <c>z</c> into the box with
    /// <see cref="WrapAxis"/>, so the two sides of a seam read the same water to rounding and the
    /// phases never grow with a body's distance from the origin. The wave vectors are whole
    /// numbers of waves along each side, so the field is periodic exactly and the fold changes
    /// nothing but the rounding.
    /// </para>
    /// </remarks>
    public sealed partial class CurrentField
    {
        // ------------------------------------------------------------------ the mode set

        /// <summary>The number of wave vectors.</summary>
        private const int OceanVectors = 25;

        /// <summary>The number of vertical layers per vector.</summary>
        private const int OceanLayers = 3;

        /// <summary>The number of modes: a mode is a (vector, layer) pair.</summary>
        private const int OceanModes = OceanVectors * OceanLayers;

        // Whole waves along the box's length (m) and across its width (n), one of each ± pair,
        // in order of |(m, n)|. Chosen by scripts/reads/open-ocean-vectors.py, whose rule is
        // logbook/specs/open-ocean-current-build.md's: radii geometric from 2 to 10 over the 25,
        // directions by the golden angle over the half-plane, each target taking the nearest
        // unused integer vector, and no vector on an axis below |(m, n)| = 5, where a mode on an
        // axis is a straight jet that wraps the square in one length. The largest gap between
        // two directions is 18°; one vector, (0, 6), lies on an axis, at a wavelength of 50 m in
        // the 300 m square. OceanVectorsTests holds the table to the rule.
        private static readonly int[] OceanWavesX =
            { 1, 2, 2, 2, 3, 3, 1, 3, 3, 2, 1, 4, 1, 5, 2, 5, 4, 0, 6, 5, 3, 8, 3, 7, 8 };

        private static readonly int[] OceanWavesZ =
            { -2, -1, -2, 2, -1, 1, 3, -2, 2, 3, -4, -1, 4, 1, -5, -2, 4, 6, 3, -5, 7, -1, -8, 6, -5 };

        /// <summary>The mean square of a breathing envelope, <c>0.75 + 0.25·sin</c>: 9/16 + 1/32.</summary>
        private const double OceanEnvelopeMeanSquare = 0.59375d;

        private bool _oceanBuilt;

        // Per vector: the wavenumbers, rad/m.
        private double[] _oceanKx;
        private double[] _oceanKz;

        // Per mode, index 3v + (q − 1): the potential's amplitude at unit scale, m, and the
        // phases and rates of the travelling phase and the breathing envelope.
        private double[] _oceanAmplitude;
        private double[] _oceanPhase;
        private double[] _oceanRate;
        private double[] _oceanBreathPhase;
        private double[] _oceanBreathRate;

        // 1 / the RMS speed over the water at unit Speed, and the ceiling at unit Speed and unit
        // scale times that scale: see BuildOcean.
        private double _oceanScale;
        private double _oceanBound;

        /// <summary>
        /// Whether the flat field the pullback reads is the ocean's: a box whose mode is
        /// <see cref="CurrentMode.Ocean"/>. A tank's is the streams whatever the mode says.
        /// </summary>
        private bool OceanWater => _shape != WorldShape.Tank && Mode == CurrentMode.Ocean;

        // ------------------------------------------------------------------ the flat field's dispatch

        /// <summary>The flat field the pullback carries: the ocean's in a box, the streams' in a tank.</summary>
        private Float3 FlatUnit(double x, double y, double z, double t, double overturning) =>
            OceanWater ? OceanUnit(x, y, z, t) : StreamsUnit(x, y, z, t, overturning);

        /// <summary>The flat potential the pullback carries — <see cref="FlatUnit"/>'s.</summary>
        private Float3 FlatPotentialUnit(double x, double y, double z, double t, double overturning) =>
            OceanWater ? OceanPotentialUnit(x, y, z, t) : StreamsPotentialUnit(x, y, z, t, overturning);

        /// <summary><see cref="FlatUnit"/>'s field, clock derivative and Jacobian.</summary>
        private StreamsGradient FlatUnitWithGradient(double x, double y, double z, double t, double overturning) =>
            OceanWater ? OceanUnitWithGradient(x, y, z, t) : StreamsUnitWithGradient(x, y, z, t, overturning);

        /// <summary><see cref="FlatPotentialUnit"/>'s potential, clock derivative and Jacobian.</summary>
        private StreamsGradient FlatPotentialUnitWithGradient(
            double x, double y, double z, double t, double overturning) =>
            OceanWater
                ? OceanPotentialUnitWithGradient(x, y, z, t)
                : StreamsPotentialUnitWithGradient(x, y, z, t, overturning);

        // ------------------------------------------------------------------ the entries

        /// <summary>The ocean's velocity at a place and a time, m/s.</summary>
        private Float3 OceanAt(float x, float y, float z, double seconds)
        {
            if (_speed <= 0f) return Float3.Zero;

            EnsureOcean();

            double t = 2.0 * Math.PI * seconds / _periodSeconds;
            double fx = WrapAxis(x, LengthMetres);
            double fz = WrapAxis(z, WidthMetres);

            Float3 u = _bed == null ? OceanUnit(fx, y, fz, t) : StreamsSlopedUnit(fx, y, fz, t, 0d);

            return u * (float)(_speed * _oceanScale);
        }

        /// <summary>The ocean's potential at a place and a time, m²/s. The caller has checked the speed.</summary>
        private Float3 OceanPotentialAt(float x, float y, float z, double seconds)
        {
            EnsureOcean();

            double t = 2.0 * Math.PI * seconds / _periodSeconds;
            double fx = WrapAxis(x, LengthMetres);
            double fz = WrapAxis(z, WidthMetres);

            Float3 a = _bed == null
                ? OceanPotentialUnit(fx, y, fz, t)
                : StreamsSlopedPotentialUnit(fx, y, fz, t, 0d);

            return a * (float)(_speed * _oceanScale);
        }

        /// <summary>
        /// <see cref="OceanPotentialAt(float, float, float, double)"/> with the floor's column
        /// supplied — the same bits, as the tank's column overload is. The column was taken at the
        /// folded point (<see cref="ColumnAt"/> folds in a box), so the two read one floor.
        /// </summary>
        private Float3 OceanPotentialAt(float x, float y, float z, double seconds, in BedColumn column)
        {
            EnsureOcean();

            double t = 2.0 * Math.PI * seconds / _periodSeconds;
            double fx = WrapAxis(x, LengthMetres);
            double fz = WrapAxis(z, WidthMetres);

            return StreamsSlopedPotentialUnit(fx, y, fz, t, 0d, column) * (float)(_speed * _oceanScale);
        }

        /// <summary>
        /// The ocean's acceleration along its own path, m/s², in closed form: the tank's
        /// assembly (<see cref="StreamsAccelerationAt"/>) over this field's gradient. The caller
        /// has checked the speed and the vent.
        /// </summary>
        /// <remarks>
        /// Analytic rather than the box's nine-sample stencil, because round 61's worlds ran with
        /// <see cref="FluidConfig.FluidAccelerationCoefficient"/> at 1, so this is asked at every
        /// link on every step, and the stencil would be nine of the 25-vector sums per call.
        /// </remarks>
        private Float3 OceanAccelerationAt(float x, float y, float z, double seconds)
        {
            EnsureOcean();

            double clock = 2.0 * Math.PI / _periodSeconds;
            double fx = WrapAxis(x, LengthMetres);
            double fz = WrapAxis(z, WidthMetres);

            StreamsGradient g = _bed == null
                ? OceanUnitWithGradient(fx, y, fz, clock * seconds)
                : StreamsSlopedUnitWithGradient(fx, y, fz, clock * seconds, 0d);

            double sigma = (double)_speed * _oceanScale;
            double perSecond = sigma * clock;
            double squared = sigma * sigma;

            return new Float3(
                (float)(perSecond * g.Tx +
                        squared * (g.Vx * g.Xx + g.Vy * g.Xy + g.Vz * g.Xz)),
                (float)(perSecond * g.Ty +
                        squared * (g.Vx * g.Yx + g.Vy * g.Yy + g.Vz * g.Yz)),
                (float)(perSecond * g.Tz +
                        squared * (g.Vx * g.Zx + g.Vy * g.Zy + g.Vz * g.Zz)));
        }

        // ------------------------------------------------------------------ the coral's fade

        /// <summary>The coral's fade at a place folded into the box, as the float the potential takes.</summary>
        private float CoralFoldedFade(float x, float y, float z) =>
            (float)_coral.FadeAt(WrapAxis(x, LengthMetres), y, WrapAxis(z, WidthMetres));

        /// <summary>
        /// The ocean faded around the coral, <c>g·u + ∇g × A</c> at the place folded into the box —
        /// the tank's <see cref="ReefStreamsAt(float, float, float, double)"/> with the ocean's field.
        /// Beyond every hull's fade it is <see cref="OceanAt"/> itself.
        /// </summary>
        private Float3 CoralOceanAt(float x, float y, float z, double seconds)
        {
            if (_speed <= 0f) return Float3.Zero;

            float fx = WrapAxis(x, LengthMetres), fz = WrapAxis(z, WidthMetres);
            if (!_coral.Fade(fx, y, fz, out ReefGeometry.Distance f)) return OceanAt(fx, y, fz, seconds);

            Float3 u = OceanAt(fx, y, fz, seconds);
            Float3 a = OceanPotentialAt(fx, y, fz, seconds);

            return new Float3(
                (float)(f.S * u.X + (f.Gy * a.Z - f.Gz * a.Y)),
                (float)(f.S * u.Y + (f.Gz * a.X - f.Gx * a.Z)),
                (float)(f.S * u.Z + (f.Gx * a.Y - f.Gy * a.X)));
        }

        /// <summary>The faded ocean's potential, <c>g·A</c>, as one float multiply after the ocean's.</summary>
        private Float3 CoralOceanPotentialAt(float x, float y, float z, double seconds)
        {
            float fx = WrapAxis(x, LengthMetres), fz = WrapAxis(z, WidthMetres);
            return OceanPotentialAt(fx, y, fz, seconds) * (float)_coral.FadeAt(fx, y, fz);
        }

        /// <summary>
        /// The faded ocean's acceleration in closed form: <see cref="ReefFadedGradient"/>'s chain
        /// rule over the ocean's field and potential, assembled as <see cref="OceanAccelerationAt"/>.
        /// </summary>
        private Float3 CoralOceanAccelerationAt(float x, float y, float z, double seconds)
        {
            float fx = WrapAxis(x, LengthMetres), fz = WrapAxis(z, WidthMetres);
            if (!_coral.Fade(fx, y, fz, out ReefGeometry.Distance f)) return OceanAccelerationAt(fx, y, fz, seconds);

            EnsureOcean();

            double clock = 2.0 * Math.PI / _periodSeconds;
            // The ocean's own field carries no overturning (OceanAt's sloped call passes 0).
            StreamsGradient g = ReefFadedGradient(fx, y, fz, clock * seconds, f, 0d);

            double sigma = (double)_speed * _oceanScale;
            double perSecond = sigma * clock;
            double squared = sigma * sigma;

            return new Float3(
                (float)(perSecond * g.Tx +
                        squared * (g.Vx * g.Xx + g.Vy * g.Xy + g.Vz * g.Xz)),
                (float)(perSecond * g.Ty +
                        squared * (g.Vx * g.Yx + g.Vy * g.Yy + g.Vz * g.Yz)),
                (float)(perSecond * g.Tz +
                        squared * (g.Vx * g.Zx + g.Vy * g.Zy + g.Vz * g.Zz)));
        }

        // ------------------------------------------------------------------ the flat field

        /// <summary>
        /// The three layers' shapes at a flat height, <c>S_q = sin((q − ½)πη)</c> with
        /// <c>η = 1 + y/D</c>, and their slopes in <c>y</c>.
        /// </summary>
        /// <remarks>
        /// One sine and one cosine of <c>θ = πη/2</c>, and the rest by the recurrence
        /// <c>S_{n+1} = 2·cos 2θ·S_n − S_{n−1}</c> over the odd multiples of <c>θ</c>. At the bed
        /// <c>η</c> is exactly 0, since <c>−D/D</c> is exactly −1, so every shape is exactly 0 there
        /// and nothing special-cases it. Outside the water the height is clamped first, as the
        /// streams clamp theirs, and the slope is the face's own.
        /// </remarks>
        private void OceanLayersAt(
            double y,
            out double s1, out double s2, out double s3,
            out double d1, out double d2, out double d3)
        {
            double depth = _depthMetres;

            if (y > 0d) y = 0d;
            else if (y < -depth) y = -depth;

            double theta = 0.5d * Math.PI * (1d + y / depth);
            double sin = Math.Sin(theta);
            double cos = Math.Cos(theta);
            double twice = 2d * (1d - 2d * sin * sin);

            s1 = sin;
            s2 = twice * s1 + sin;
            s3 = twice * s2 - s1;

            double c1 = cos;
            double c2 = twice * c1 - cos;
            double c3 = twice * c2 - c1;

            double unit = 0.5d * Math.PI / depth;

            d1 = unit * c1;
            d2 = 3d * unit * c2;
            d3 = 5d * unit * c3;
        }

        /// <summary>The flat ocean at unit speed and unit scale, at an already-folded place and a scaled phase.</summary>
        private Float3 OceanUnit(double x, double y, double z, double t)
        {
            OceanInstant instant = EnsureOceanInstant(t);
            OceanLayersAt(y, out double s1, out double s2, out double s3, out _, out _, out _);

            double[] cos = instant.Cos, sin = instant.Sin;
            double ux = 0d, uz = 0d;

            for (int v = 0, k = 0; v < OceanVectors; v++, k += OceanLayers)
            {
                double alpha = cos[k] * s1 + cos[k + 1] * s2 + cos[k + 2] * s3;
                double beta = sin[k] * s1 + sin[k + 1] * s2 + sin[k + 2] * s3;

                double kx = _oceanKx[v], kz = _oceanKz[v];
                double chi = kx * x + kz * z;
                double sinChi = Math.Sin(chi), cosChi = Math.Cos(chi);

                // Σ_q W·S_q·sin(χ + φ), and the curl of its cosine partner: u = (−∂A_y/∂z, 0, ∂A_y/∂x).
                double q = sinChi * alpha + cosChi * beta;

                ux += kz * q;
                uz -= kx * q;
            }

            return new Float3((float)ux, 0f, (float)uz);
        }

        /// <summary>The flat ocean's potential at unit speed and unit scale: <c>(0, A_y, 0)</c>.</summary>
        private Float3 OceanPotentialUnit(double x, double y, double z, double t)
        {
            OceanInstant instant = EnsureOceanInstant(t);
            OceanLayersAt(y, out double s1, out double s2, out double s3, out _, out _, out _);

            double[] cos = instant.Cos, sin = instant.Sin;
            double ay = 0d;

            for (int v = 0, k = 0; v < OceanVectors; v++, k += OceanLayers)
            {
                double alpha = cos[k] * s1 + cos[k + 1] * s2 + cos[k + 2] * s3;
                double beta = sin[k] * s1 + sin[k + 1] * s2 + sin[k + 2] * s3;

                double chi = _oceanKx[v] * x + _oceanKz[v] * z;

                ay += Math.Cos(chi) * alpha - Math.Sin(chi) * beta;
            }

            return new Float3(0f, (float)ay, 0f);
        }

        /// <summary>
        /// <see cref="OceanPotentialUnit(double, double, double, double)"/> with each vector's
        /// <c>cos χ</c> and <c>sin χ</c> read from <see cref="OceanPhasesAt"/>'s table rather than
        /// computed: the same products summed in the same order, so the same bits.
        /// </summary>
        private Float3 OceanPotentialUnit(double y, double t, double[] phases, int at)
        {
            OceanInstant instant = EnsureOceanInstant(t);
            OceanLayersAt(y, out double s1, out double s2, out double s3, out _, out _, out _);

            double[] cos = instant.Cos, sin = instant.Sin;
            double ay = 0d;

            for (int v = 0, k = 0, p = at; v < OceanVectors; v++, k += OceanLayers, p += 2)
            {
                double alpha = cos[k] * s1 + cos[k + 1] * s2 + cos[k + 2] * s3;
                double beta = sin[k] * s1 + sin[k + 1] * s2 + sin[k + 2] * s3;

                ay += phases[p] * alpha - phases[p + 1] * beta;
            }

            return new Float3(0f, (float)ay, 0f);
        }

        // ------------------------------------------------------------------ the ocean on a device

        /// <summary>
        /// The box ocean's instant for a transport device (<see cref="PotentialTransportPlan.Ocean"/>):
        /// pinned at <paramref name="seconds"/> as the grid's sampling pins it, the 75 modes'
        /// cosine coefficients and then their 75 sine coefficients copied, and released.
        /// </summary>
        internal void CopyOceanInstant(double seconds, double[] into)
        {
            PinInstant(seconds);

            try
            {
                OceanInstant instant = EnsureOceanInstant(2.0 * Math.PI * seconds / _periodSeconds);
                Array.Copy(instant.Cos, 0, into, 0, OceanModes);
                Array.Copy(instant.Sin, 0, into, OceanModes, OceanModes);
            }
            finally
            {
                UnpinInstant();
            }
        }

        /// <summary>The float the box ocean's potential is multiplied by, as <see cref="OceanPotentialAt(float, float, float, double, in BedColumn)"/> takes it.</summary>
        internal float OceanScale => (float)(_speed * _oceanScale);

        /// <summary>
        /// <c>sin θ</c> of <see cref="OceanLayersAt"/> at the height <see cref="MapFrom"/> maps
        /// <paramref name="y"/> to in <paramref name="column"/>: the same clamps and the same
        /// expression, so a device that takes the layers' shapes from it by the recurrence gets
        /// the CPU's shapes.
        /// </summary>
        internal double OceanMappedSine(in BedColumn column, float y)
        {
            double mapped = MapFrom(column, y).MappedY;
            double depth = _depthMetres;

            if (mapped > 0d) mapped = 0d;
            else if (mapped < -depth) mapped = -depth;

            return Math.Sin(0.5d * Math.PI * (1d + mapped / depth));
        }

        // ------------------------------------------------------------------ the ocean's column hoist

        /// <summary>The doubles one place takes in <see cref="OceanPhasesAt"/>'s table: <c>cos χ</c> and <c>sin χ</c> for each vector.</summary>
        public const int OceanPhaseStride = 2 * OceanVectors;

        /// <summary>
        /// Whether a table of <see cref="OceanPhasesAt"/> can stand in for the ocean's
        /// trigonometry at a lattice of fixed places: the ocean in a box, on a shaped floor, with a
        /// potential to give. The grid asks at every pass, so a mode changed between passes sends it
        /// back to the direct path.
        /// </summary>
        public bool OceanPhasesApply => OceanWater && _bed != null && HasPotential;

        /// <summary>
        /// Builds the ocean if it is not built and says which build it is, so a table made from
        /// one build is not read against the vectors of another.
        /// </summary>
        public int OceanPhasesBuild()
        {
            EnsureOcean();
            return _oceanBuilds;
        }

        /// <summary>
        /// Writes <c>cos χ</c> and <c>sin χ</c> for each of the ocean's vectors at a place, from
        /// <paramref name="at"/> on: <c>χ = k_x·x + k_z·z</c> at the place folded into the box,
        /// computed as <see cref="OceanPotentialAt(float, float, float, double, in BedColumn)"/>
        /// computes it. Neither the height nor the clock enters <c>χ</c>, so a lattice whose
        /// places never move pays for its trigonometry once.
        /// </summary>
        public void OceanPhasesAt(float x, float z, double[] into, int at)
        {
            EnsureOcean();

            double fx = WrapAxis(x, LengthMetres);
            double fz = WrapAxis(z, WidthMetres);

            for (int v = 0, p = at; v < OceanVectors; v++, p += 2)
            {
                double chi = _oceanKx[v] * fx + _oceanKz[v] * fz;
                into[p] = Math.Cos(chi);
                into[p + 1] = Math.Sin(chi);
            }
        }

        /// <summary>
        /// <see cref="PotentialAt(float, float, float, double, in BedColumn)"/> in the box's ocean
        /// with the place's phases read from a table <see cref="OceanPhasesAt"/> filled at the same
        /// <c>(x, z)</c>: the map, the shore's fade, the scale and the coral's fade applied as the
        /// direct path applies them, so the same bits.
        /// </summary>
        /// <remarks>
        /// <b>Refuses water the table cannot stand for</b> (<see cref="OceanPhasesApply"/>) and a
        /// column never sampled. The caller answers for the table: it must be this build's
        /// (<see cref="OceanPhasesBuild"/>) and filled at this <c>(x, z)</c>.
        /// </remarks>
        public Float3 PotentialAt(
            float x, float y, float z, double seconds, in BedColumn column, double[] phases, int at)
        {
            if (!OceanPhasesApply || !column.Sampled)
            {
                throw new InvalidOperationException(
                    "The ocean's phase table stands for the ocean in a box on a shaped floor, sampled " +
                    FormattableString.Invariant($"at a sampled column; have mode {Mode}, shape {_shape}, ") +
                    FormattableString.Invariant($"bed {(_bed == null ? "absent" : "present")}, ") +
                    FormattableString.Invariant($"column {(column.Sampled ? "sampled" : "default")}."));
            }

            if (_speed <= 0f) return Float3.Zero;

            EnsureOcean();

            double t = 2.0 * Math.PI * seconds / _periodSeconds;

            // StreamsSlopedPotentialUnit's column overload with the flat potential's phases read.
            BedMap m = MapFrom(column, y);
            Float3 a;

            if (_fadeOn)
            {
                a = m.Fade == 0d
                    ? Float3.Zero
                    : FadedPotentialOf(OceanPotentialUnit(m.MappedY, t, phases, at), m);
            }
            else
            {
                Float3 flat = OceanPotentialUnit(m.MappedY, t, phases, at);

                a = new Float3(
                    (float)(flat.X + m.A * flat.Y),
                    (float)(m.C * flat.Y),
                    (float)(flat.Z + m.B * flat.Y));
            }

            Float3 potential = a * (float)(_speed * _oceanScale);

            return _coral == null ? potential : potential * CoralFoldedFade(x, y, z);
        }

        /// <summary>
        /// The flat ocean's velocity, clock derivative and Jacobian, in closed form from one pass
        /// over the vectors.
        /// </summary>
        /// <remarks>
        /// With <c>P_v = Σ_q W·S_q·cos(χ + φ)</c> and <c>Q_v = Σ_q W·S_q·sin(χ + φ)</c>,
        /// <c>u_x = Σ k_z·Q_v</c> and <c>u_z = −Σ k_x·Q_v</c>; <c>∂Q_v/∂x = k_x·P_v</c>,
        /// <c>∂Q_v/∂z = k_z·P_v</c>, and the layers' slopes give <c>∂/∂y</c>. The trace is
        /// <c>Σ (k_z·k_x − k_x·k_z)·P_v</c>, zero term by term.
        /// </remarks>
        private StreamsGradient OceanUnitWithGradient(double x, double y, double z, double t)
        {
            OceanInstant instant = EnsureOceanInstant(t);
            OceanLayersAt(y, out double s1, out double s2, out double s3, out double d1, out double d2, out double d3);

            double[] cos = instant.Cos, sin = instant.Sin, cosRate = instant.CosRate, sinRate = instant.SinRate;
            var g = default(StreamsGradient);

            for (int v = 0, k = 0; v < OceanVectors; v++, k += OceanLayers)
            {
                double alpha = cos[k] * s1 + cos[k + 1] * s2 + cos[k + 2] * s3;
                double beta = sin[k] * s1 + sin[k + 1] * s2 + sin[k + 2] * s3;
                double alphaY = cos[k] * d1 + cos[k + 1] * d2 + cos[k + 2] * d3;
                double betaY = sin[k] * d1 + sin[k + 1] * d2 + sin[k + 2] * d3;
                double alphaT = cosRate[k] * s1 + cosRate[k + 1] * s2 + cosRate[k + 2] * s3;
                double betaT = sinRate[k] * s1 + sinRate[k + 1] * s2 + sinRate[k + 2] * s3;

                double kx = _oceanKx[v], kz = _oceanKz[v];
                double chi = kx * x + kz * z;
                double sinChi = Math.Sin(chi), cosChi = Math.Cos(chi);

                double p = cosChi * alpha - sinChi * beta;
                double q = sinChi * alpha + cosChi * beta;
                double qy = sinChi * alphaY + cosChi * betaY;
                double qt = sinChi * alphaT + cosChi * betaT;

                g.Vx += kz * q;
                g.Vz -= kx * q;

                g.Tx += kz * qt;
                g.Tz -= kx * qt;

                g.Xx += kz * kx * p;
                g.Xy += kz * qy;
                g.Xz += kz * kz * p;

                g.Zx -= kx * kx * p;
                g.Zy -= kx * qy;
                g.Zz -= kx * kz * p;
            }

            return g;
        }

        /// <summary>
        /// The flat ocean's potential, its clock derivative and its Jacobian — what the shore's
        /// fade needs for <c>∇f × A'</c>, as the streams' is.
        /// </summary>
        /// <remarks>
        /// <c>A = (0, Σ P_v, 0)</c>; <c>∂P_v/∂x = −k_x·Q_v</c>, <c>∂P_v/∂z = −k_z·Q_v</c>.
        /// </remarks>
        private StreamsGradient OceanPotentialUnitWithGradient(double x, double y, double z, double t)
        {
            OceanInstant instant = EnsureOceanInstant(t);
            OceanLayersAt(y, out double s1, out double s2, out double s3, out double d1, out double d2, out double d3);

            double[] cos = instant.Cos, sin = instant.Sin, cosRate = instant.CosRate, sinRate = instant.SinRate;
            var g = default(StreamsGradient);

            for (int v = 0, k = 0; v < OceanVectors; v++, k += OceanLayers)
            {
                double alpha = cos[k] * s1 + cos[k + 1] * s2 + cos[k + 2] * s3;
                double beta = sin[k] * s1 + sin[k + 1] * s2 + sin[k + 2] * s3;
                double alphaY = cos[k] * d1 + cos[k + 1] * d2 + cos[k + 2] * d3;
                double betaY = sin[k] * d1 + sin[k + 1] * d2 + sin[k + 2] * d3;
                double alphaT = cosRate[k] * s1 + cosRate[k + 1] * s2 + cosRate[k + 2] * s3;
                double betaT = sinRate[k] * s1 + sinRate[k + 1] * s2 + sinRate[k + 2] * s3;

                double kx = _oceanKx[v], kz = _oceanKz[v];
                double chi = kx * x + kz * z;
                double sinChi = Math.Sin(chi), cosChi = Math.Cos(chi);

                double p = cosChi * alpha - sinChi * beta;
                double q = sinChi * alpha + cosChi * beta;

                g.Vy += p;
                g.Yx -= kx * q;
                g.Yy += cosChi * alphaY - sinChi * betaY;
                g.Yz -= kz * q;
                g.Ty += cosChi * alphaT - sinChi * betaT;
            }

            return g;
        }

        // ------------------------------------------------------------------ the clock

        // The ocean's own memo of instants, for the streams' reasons (see the remarks above
        // InstantSlots): a step asks for the samplers' phase and the analytic acceleration's, and
        // the grid's advection asks for the metabolic clock's. The same keying, the same eviction
        // and the same pin: a pinned field fills its slots at the pin and refuses any other phase.
        private OceanInstant[] _oceanSlots;
        private int _oceanNext;

        /// <summary>
        /// One memoised instant of the ocean: every mode's <c>W·cos φ</c> and <c>W·sin φ</c>, with
        /// <c>W</c> its amplitude times its breathing envelope, and their clock derivatives.
        /// </summary>
        private sealed class OceanInstant
        {
            /// <summary>The scaled phase this slot holds, NaN while unfilled or half written.</summary>
            public double At;

            public double[] Cos;
            public double[] Sin;
            public double[] CosRate;
            public double[] SinRate;
        }

        private OceanInstant EnsureOceanInstant(double t)
        {
            for (int slot = 0; slot < InstantSlots; slot++)
            {
                OceanInstant held = _oceanSlots[slot];
                if (held.At == t) return held;
            }

            if (_instantPinned)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"This current is pinned at phase {_pinnedInstant:R} and was sampled at ") +
                    FormattableString.Invariant($"{t:R}. A pinned field answers at one clock."));
            }

            int fill = _oceanNext;
            _oceanNext = fill + 1 == InstantSlots ? 0 : fill + 1;

            OceanInstant instant = _oceanSlots[fill];
            instant.At = double.NaN;

            for (int k = 0; k < OceanModes; k++)
            {
                double rate = _oceanRate[k];
                double phase = _oceanPhase[k] + rate * t;
                double cos = Math.Cos(phase);
                double sin = Math.Sin(phase);

                // Between half and full, never off — the streams' 0.75 + 0.25 sin.
                double breath = _oceanBreathRate[k] * t + _oceanBreathPhase[k];
                double envelope = 0.75d + 0.25d * Math.Sin(breath);
                double envelopeRate = 0.25d * _oceanBreathRate[k] * Math.Cos(breath);

                double a = _oceanAmplitude[k];

                instant.Cos[k] = a * envelope * cos;
                instant.Sin[k] = a * envelope * sin;
                instant.CosRate[k] = a * (envelopeRate * cos - envelope * rate * sin);
                instant.SinRate[k] = a * (envelopeRate * sin + envelope * rate * cos);
            }

            instant.At = t;

            return instant;
        }

        /// <summary>Fills the slots a pin at <paramref name="seconds"/> needs: both groupings of the clock.</summary>
        private void PinOcean(double seconds)
        {
            EnsureOceanInstant(2.0 * Math.PI * seconds / _periodSeconds);
            EnsureOceanInstant(2.0 * Math.PI / _periodSeconds * seconds);
        }

        // ------------------------------------------------------------------ the build

        private void EnsureOcean()
        {
            if (_oceanBuilt) return;

            if (_shape == WorldShape.Tank)
            {
                throw new InvalidOperationException(
                    "The open ocean's current is the water of a box that joins at both seams, and " +
                    "this field is in a tank, whose water is the streams. D204.");
            }

            if (!(_patchWidthMetres > 0f) || _patchCount < 1 || !(_depthMetres > 0f))
            {
                throw new InvalidOperationException(
                    "CurrentMode.Ocean is a field over a box, and this field has not been told what " +
                    "box it is in. Call SetBox(patchWidth, patchCount, depth, seed) first; World's " +
                    "constructor does it for every world. " +
                    FormattableString.Invariant(
                        $"Have patch width {_patchWidthMetres} m, {_patchCount} patches, depth {_depthMetres} m."));
            }

            BuildOcean();
            _oceanBuilt = true;
            _oceanBuilds++;
        }

        // How many times BuildOcean has run, so a table of the vectors' phases knows when the
        // vectors it was built from have been replaced (OceanPhasesBuild).
        private int _oceanBuilds;

        /// <summary>
        /// Draws the modes' phases from the run's seed, sets their amplitudes from the spectrum,
        /// and measures the scale that makes <see cref="Speed"/> the RMS over the water.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The spectrum.</b> The spec asks for energy roughly as <c>|k|^(−5/3)</c> over the band.
        /// The vectors' radii are spaced geometrically, so each stands for a band of width
        /// proportional to <c>|k|</c> and carries energy proportional to <c>|k|^(−2/3)</c>: a mode's
        /// RMS speed goes as <c>|k|^(−1/3)</c>, and its potential, whose curl multiplies by
        /// <c>|k|</c>, as <c>|k|^(−4/3)</c>. The three layers are weighted <c>1/q</c>, so the
        /// shallowest-varying layer carries most of the speed (my choice, to be read in the theatre).
        /// </para>
        /// <para>
        /// <b>The rates.</b> The streams' rule, <c>±(1 + j·φ)</c> in units of <c>2π/Period</c> with
        /// the signs alternating, over the 75 modes in the table's order, so <c>j</c> rises with
        /// <c>|k|</c> and the largest swirls change slowest. Every pair is incommensurate, so the
        /// sum has no period. The envelopes breathe at <c>−φ</c> times their mode's rate.
        /// </para>
        /// <para>
        /// <b>The scale is the time-averaged mean square over the water, worked per point rather
        /// than sampled.</b> Every mode's phase drifts at its own incommensurate rate, so over time
        /// each one's phase is uniform and independent of every other's, and the cross terms
        /// between modes average to zero exactly. What is left at a point is each mode's own mean
        /// square: <c>⟨E²⟩·½·W²·S_q²·(f²·|U_v|² + |∇f × p|²)</c>, with <c>U_v</c> the mode's flat
        /// curl carried through the map (<c>(c·k_z, b·k_x − a·k_z, −c·k_x)</c>), <c>p = (a, c, b)</c>
        /// the map's row that carries <c>A_y</c>, and <c>f</c> the shore's fade. That is a sum over
        /// 75 modes of numbers that depend only on the column and the height, so the whole water is
        /// a walk over columns and depths with no clock in it, and the answer is exact in time
        /// rather than a sample of a few instants. The volume is weighted by each column's own
        /// depth.
        /// </para>
        /// <para>
        /// <b>The bound is a ceiling, not a sample, in the vertical and across the modes</b>: every
        /// envelope at 1, every layer at its largest, <c>|U_v| ≤ |k_v|·√(c² + a² + b²)</c>. Across
        /// the columns it is the largest over the walk's lattice with a tenth more for the water
        /// between, as the streams' is. <see cref="GridField.Advect"/>'s Courant refusal reads it.
        /// </para>
        /// </remarks>
        private void BuildOcean()
        {
            double length = LengthMetres;
            double width = WidthMetres;
            double depth = _depthMetres;

            _oceanKx = new double[OceanVectors];
            _oceanKz = new double[OceanVectors];

            _oceanAmplitude = new double[OceanModes];
            _oceanPhase = new double[OceanModes];
            _oceanRate = new double[OceanModes];
            _oceanBreathPhase = new double[OceanModes];
            _oceanBreathRate = new double[OceanModes];

            _oceanSlots = new OceanInstant[InstantSlots];
            _oceanNext = 0;

            for (int slot = 0; slot < InstantSlots; slot++)
            {
                _oceanSlots[slot] = new OceanInstant
                {
                    At = double.NaN,
                    Cos = new double[OceanModes],
                    Sin = new double[OceanModes],
                    CosRate = new double[OceanModes],
                    SinRate = new double[OceanModes],
                };
            }

            var rng = new Rng(_seed);

            // The sums the walk needs, over the vectors at unit layer weight: Σ w², and the three
            // second moments of (w/|k|)·k, and Σ (w/|k|)² and the bound's two first moments.
            double sumW2 = 0d, sumXx = 0d, sumXz = 0d, sumZz = 0d, sumA2 = 0d, sumW = 0d, sumA = 0d;

            for (int v = 0; v < OceanVectors; v++)
            {
                double kx = 2.0 * Math.PI * OceanWavesX[v] / length;
                double kz = 2.0 * Math.PI * OceanWavesZ[v] / width;
                double kk = Math.Sqrt(kx * kx + kz * kz);

                _oceanKx[v] = kx;
                _oceanKz[v] = kz;

                // A mode's RMS speed as |k|^(−1/3), its potential as that over |k| — see the remarks.
                double w = Math.Pow(kk, -1d / 3d);
                double a = w / kk;

                sumW2 += w * w;
                sumXx += a * a * kx * kx;
                sumXz += a * a * kx * kz;
                sumZz += a * a * kz * kz;
                sumA2 += a * a;
                sumW += w;
                sumA += a;

                for (int q = 1; q <= OceanLayers; q++)
                {
                    int k = v * OceanLayers + (q - 1);

                    _oceanAmplitude[k] = a / q;
                    _oceanPhase[k] = 2.0 * Math.PI * rng.NextFloat();
                    _oceanBreathPhase[k] = 2.0 * Math.PI * rng.NextFloat();
                    _oceanRate[k] = Rate(k);
                    _oceanBreathRate[k] = -Incommensurate * Rate(k);
                }
            }

            // The walk: columns at the centres of a lattice no coarser than 2.5 m, and 24 heights
            // per column at the midpoints of equal slices of its water. Midpoints make the layers'
            // own mean squares exactly ½ on a flat bed, so the walk and the closed form agree there.
            int columnsAlong = Math.Max(8, (int)Math.Ceiling(length / 2.5d));
            int columnsAcross = Math.Max(8, (int)Math.Ceiling(width / 2.5d));
            const int Heights = 24;

            double weighted = 0d, volume = 0d, ceiling = 0d;

            for (int i = 0; i < columnsAlong; i++)
            {
                double x = (i + 0.5d) * length / columnsAlong;

                for (int j = 0; j < columnsAcross; j++)
                {
                    double z = (j + 0.5d) * width / columnsAcross;

                    double h = 0d, hx = 0d, hz = 0d;
                    if (_bed != null) _bed.HeightAndGradient(x, z, out h, out hx, out hz);

                    double d = depth - h;

                    double fade = 1d, fd = 0d;
                    if (_fadeOn) ShoreFade(d, out fade, out fd, out _);

                    double gx = -fd * hx;
                    double gz = -fd * hz;
                    double c = depth / d;

                    double column = 0d;

                    for (int n = 0; n < Heights; n++)
                    {
                        double eta = (n + 0.5d) / Heights;
                        double y = -d * (1d - eta);

                        double scale = y * depth / (d * d);
                        double a = scale * hx;
                        double b = scale * hz;

                        // ∇f × p with ∇f = (gx, 0, gz) and p = (a, c, b).
                        double crossX = -gz * c;
                        double crossY = gz * a - gx * b;
                        double crossZ = gx * c;
                        double cross2 = crossX * crossX + crossY * crossY + crossZ * crossZ;

                        OceanLayersAt(-depth * (1d - eta), out double s1, out double s2, out double s3, out _, out _, out _);
                        double layers = s1 * s1 + s2 * s2 / 4d + s3 * s3 / 9d;

                        double flow = c * c * sumW2 + b * b * sumXx - 2d * a * b * sumXz + a * a * sumZz;

                        column += layers * (fade * fade * flow + sumA2 * cross2);

                        double reach =
                            fade * Math.Sqrt(c * c + a * a + b * b) * sumW + Math.Sqrt(cross2) * sumA;
                        if (reach > ceiling) ceiling = reach;
                    }

                    weighted += d * column / Heights;
                    volume += d;
                }
            }

            double meanSquare = OceanEnvelopeMeanSquare * 0.5d * weighted / volume;

            if (!(meanSquare > 0d))
            {
                throw new InvalidOperationException(
                    "The open ocean measured no motion over its own box, which means every column " +
                    FormattableString.Invariant(
                        $"was faded still (box {length} m by {width} m, depth {depth} m, ") +
                    FormattableString.Invariant($"fade {(_fadeOn ? "on" : "off")})."));
            }

            _oceanScale = 1d / Math.Sqrt(meanSquare);

            // Σ_q 1/q over the three layers, each at its largest, and a tenth for the lattice.
            _oceanBound = 1.1d * (1d + 1d / 2d + 1d / 3d) * ceiling * _oceanScale;

            double Rate(int j) => ((j & 1) == 0 ? 1.0 : -1.0) * (1.0 + j * Incommensurate);
        }

        /// <summary>
        /// The ocean's ceiling at unit <see cref="Speed"/>, m/s — what
        /// <see cref="MaximumTransportSpeed"/> multiplies by the speed.
        /// </summary>
        private float OceanBound
        {
            get
            {
                EnsureOcean();
                return (float)_oceanBound;
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// The mushroom reefs of round 47: rock columns standing on the tank's floor, each topped by a
    /// cap that overhangs and shades — <c>logbook/specs/reef-spec.md</c>, redesigned under the
    /// owner's ruling of 2026-09-23 night ("the idea is to have areas in the tank that do not get
    /// light"): many reefs to a cover, each its own size, depth and outline, allowed to overlap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shape of one reef.</b> A vertical axis at <c>(x, z)</c> in the tank's frame (the
    /// tank's own axis at <c>(R, R)</c>). The cap's outline is a radial function
    /// <c>r(θ) = r0·(1 + Σ_{k=2..4} a_k cos(kθ + φ_k))</c>, a circle when every <c>a_k</c> is 0,
    /// and the cap is a slab of <see cref="CapThicknessMetres"/> whose top lies at the reef's own
    /// depth, rounded at the rim by half the thickness, whose widest extent, at its mid-plane, is
    /// the outline <c>r(θ)</c> exactly. The stem is a vertical
    /// cylinder of radius <see cref="StemRadius"/> about the same axis, from below the deepest
    /// floor to the cap's mid-plane, joined to the cap by a smooth minimum with a fillet of half
    /// the thickness. A stem radius of 0 with the cap at the surface is the floating island.
    /// </para>
    /// <para>
    /// <b>The distance, and where it is approximate.</b> With <c>v</c> the height over the cap's
    /// mid-plane and <c>q = (ρ − r(θ))·c(θ) + t/2</c>, where <c>c = r/√(r² + r′²)</c> is the cosine
    /// of the outline's slope against the radial, the cap's distance is <c>√(q² + v²) − t/2</c>
    /// where <c>q &gt; 0</c> and <c>|v| − t/2</c> over the flat blob <c>q ≤ 0</c>. <c>q − t/2</c>
    /// is the first-order distance to the outline: exactly zero on it, with a gradient of exactly
    /// unit length there, drifting from the true distance by about the square of the distance over
    /// the outline's radius of curvature away from it. So the rim at the mid-plane is the outline
    /// to the bit, the flat blob is the outline moved in by <c>t/2</c> to first order, and the
    /// rim's rounding is <c>t/2</c> to first order. For a circle it is the exact distance and the
    /// arithmetic is the round reef's. The rock is <i>defined</i> as the set where this function is
    /// negative, so the mask, the contact, the placer, the guard, the light (under the outline) and
    /// the theatre's lathe (stations offset along the radial by <c>q/c</c>) all read one shape.
    /// </para>
    /// <para>
    /// <b>Its derivatives are exact, of that function.</b> The gradient and Hessian are the
    /// function's own, carried through <c>c′</c> and <c>c″</c> (which need <c>R‴</c>), so the
    /// streams' analytic acceleration is the true acceleration of the faded water that the
    /// sampler samples; what is approximate is how far from the rock the fade's quintic reaches
    /// along a steep lobe, not the water. The function is C¹ everywhere outside the rock and C²
    /// except on the surface <c>q = 0</c> above and below the cap (where the flat top meets the
    /// rim, as for the round reef) and across the plane of the stem's top, which the cap hides;
    /// the Jacobian of the water steps there and nowhere else.
    /// </para>
    /// <para>
    /// <b>The union.</b> The rock is the union of the reefs: inside is inside any, the distance is
    /// the minimum over the reefs, the light under any cap is <see cref="CapTransmission"/>, and
    /// the streams' fade is the product of every reef's fade — still 0 in every rock and 1 beyond
    /// every fade, so the faded velocity <c>g·u + ∇g × A</c> stays divergence-free however the
    /// reefs overlap.
    /// </para>
    /// <para>
    /// <b>Placed by the seed and its own stream</b> (<see cref="World.ReefPlacementIndex"/>), with
    /// no draw at all in a world with no reef. See <see cref="Place"/> for the rule.
    /// </para>
    /// </remarks>
    public sealed class ReefGeometry
    {
        /// <summary>How far inside the glass a cap's outline must stay everywhere, m.</summary>
        public const float GlassClearanceMetres = 2f;

        /// <summary>
        /// The water a cap must leave under itself, m: the floor under the cap's centre and eight
        /// points of its outline lies at least <c>depth + t + 2 m</c> down, so the beach's shelf
        /// carries no reef.
        /// </summary>
        public const float RoomUnderCapMetres = 2f;

        /// <summary>The shallowest a stemmed cap's top may lie, m.</summary>
        public const float ShallowestCapMetres = 0.5f;

        /// <summary>The outline never comes nearer its axis than this share of <c>r0</c>, nor farther than its complement past 1.</summary>
        public const double OutlineSwing = 0.4d;

        /// <summary>
        /// One reef's parameters as drawn: its axis, its cap's mean radius, the depth of its
        /// cap's top, its stem, its outline's three harmonics and the seed of its rock's look.
        /// </summary>
        public struct Reef
        {
            /// <summary>The axis, x and z, m, in the tank's frame.</summary>
            public double X, Z;

            /// <summary>The cap's mean radius <c>r0</c>, m.</summary>
            public double CapRadius;

            /// <summary>The depth of the cap's top below the surface, m.</summary>
            public double CapDepth;

            /// <summary>The stem's radius, m; 0 is the floating island.</summary>
            public double StemRadius;

            /// <summary>The outline's amplitudes for <c>k</c> = 2, 3, 4, as shares of <c>r0</c>.</summary>
            public double A2, A3, A4;

            /// <summary>The outline's phases for <c>k</c> = 2, 3, 4, radians.</summary>
            public double P2, P3, P4;

            /// <summary>A seed for the theatre's look of this rock; nothing in the simulation reads it.</summary>
            public uint NoiseSeed;

            /// <summary>Whether the outline is a circle.</summary>
            public bool Round => A2 == 0d && A3 == 0d && A4 == 0d;
        }

        private readonly Reef[] _reefs;

        // Derived once per reef: the cap's mid-plane, the outline's largest radius and least
        // slope cosine (for the lower bound that culls reefs a point cannot be near), and the
        // horizontal reach of the fade.
        private readonly double[] _capMidY;
        private readonly double[] _outerMax;
        private readonly double[] _cosMin;
        private readonly double[] _reach;

        private readonly double _halfThickness;
        private readonly double _fillet;

        /// <summary>
        /// Reefs as drawn, with one thickness and one fade. The world builds this through
        /// <see cref="Place"/>; tests build it directly.
        /// </summary>
        /// <param name="capThicknessMetres">Every cap's thickness, m.</param>
        /// <param name="fadeMetres">The streams' fade distance, m.</param>
        /// <param name="reefs">The reefs.</param>
        /// <param name="coverTarget">The cover the placer was asked for, 0 when built by hand.</param>
        /// <param name="coverGot">The cover the placer reached on the grid's columns, 0 when built by hand.</param>
        public ReefGeometry(float capThicknessMetres, float fadeMetres, Reef[] reefs, double coverTarget = 0d, double coverGot = 0d)
        {
            if (reefs == null) throw new ArgumentNullException(nameof(reefs));

            if (!(capThicknessMetres > 0f) || float.IsInfinity(capThicknessMetres) ||
                !(fadeMetres > 0f) || float.IsInfinity(fadeMetres))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A reef needs a cap thickness and a fade above 0 and finite; have t={capThicknessMetres} m, ") +
                    FormattableString.Invariant($"fade {fadeMetres} m. logbook/specs/reef-spec.md §1."));
            }

            CapThicknessMetres = capThicknessMetres;
            FadeMetres = fadeMetres;
            CoverTarget = coverTarget;
            CoverGot = coverGot;

            _halfThickness = 0.5d * capThicknessMetres;
            _fillet = _halfThickness;

            _reefs = (Reef[])reefs.Clone();
            int n = _reefs.Length;
            _capMidY = new double[n];
            _outerMax = new double[n];
            _cosMin = new double[n];
            _reach = new double[n];

            for (int i = 0; i < n; i++)
            {
                Reef r = _reefs[i];
                RefuseReef(i, r, capThicknessMetres);

                _capMidY[i] = -(r.CapDepth + _halfThickness);

                // Sampled: the outline is a trigonometric polynomial of degree 4, so 2,048 samples
                // find its extremes to about 1e-5 of r0; the margins below cover that. A circle
                // is exact: r0, a cosine of 1, and a flat blob of r0 − t/2.
                double outerMax = r.CapRadius, cosMin = 1d, flatMin = r.CapRadius - _halfThickness;

                if (!r.Round)
                {
                    outerMax = 0d;
                    flatMin = double.MaxValue;

                    for (int s = 0; s < 2048; s++)
                    {
                        double theta = 2d * Math.PI * s / 2048d;
                        Outline(r, theta, out double o, out double o1, out _, out _);
                        double c = o / Math.Sqrt(o * o + o1 * o1);
                        outerMax = Math.Max(outerMax, o);
                        cosMin = Math.Min(cosMin, c);
                        flatMin = Math.Min(flatMin, o - _halfThickness / c);
                    }

                    outerMax += 1e-3d * r.CapRadius;
                    cosMin *= 0.95d;
                }

                RefuseFlat(i, r, flatMin, capThicknessMetres);

                _outerMax[i] = outerMax;
                _cosMin[i] = cosMin;

                // Past this horizontal distance the reef's distance is at least the fade: the
                // cap's is at least c_min·(ρ − r_max) there and the stem's at least ρ − r_s, and
                // the blend lowers the smaller by at most a sixth of the fillet.
                double capReach = outerMax + (fadeMetres + _fillet / 6d) / cosMin;
                double stemReach = r.StemRadius + fadeMetres + _fillet / 6d;
                _reach[i] = Math.Max(capReach, stemReach) + 1e-9;
            }
        }

        /// <summary>
        /// Round reefs at the given centres with one set of dimensions — the first build's shape,
        /// kept for the tests that read a known rock.
        /// </summary>
        public ReefGeometry(
            float capRadiusMetres, float capDepthMetres, float capThicknessMetres,
            float stemRadiusMetres, float fadeMetres, double[] centresX, double[] centresZ)
            : this(capThicknessMetres, fadeMetres, RoundReefs(capRadiusMetres, capDepthMetres, stemRadiusMetres, centresX, centresZ))
        {
        }

        private static Reef[] RoundReefs(float capRadius, float capDepth, float stem, double[] xs, double[] zs)
        {
            if (xs == null) throw new ArgumentNullException(nameof(xs));
            if (zs == null) throw new ArgumentNullException(nameof(zs));
            if (xs.Length != zs.Length) throw new ArgumentException("One z for every x.", nameof(zs));

            var reefs = new Reef[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                reefs[i] = new Reef { X = xs[i], Z = zs[i], CapRadius = capRadius, CapDepth = capDepth, StemRadius = stem };
            }

            return reefs;
        }

        private static void RefuseReef(int index, Reef r, float thickness)
        {
            bool finite =
                !double.IsNaN(r.X) && !double.IsInfinity(r.X) && !double.IsNaN(r.Z) && !double.IsInfinity(r.Z) &&
                r.CapRadius > 0d && !double.IsInfinity(r.CapRadius) &&
                r.CapDepth >= 0d && !double.IsInfinity(r.CapDepth) &&
                r.StemRadius >= 0d && !double.IsInfinity(r.StemRadius) &&
                !double.IsNaN(r.A2) && !double.IsNaN(r.A3) && !double.IsNaN(r.A4) &&
                !double.IsNaN(r.P2) && !double.IsNaN(r.P3) && !double.IsNaN(r.P4);

            if (!finite)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"Reef {index} needs a cap radius above 0 and a cap depth and a stem radius at or above 0, all finite; ") +
                    FormattableString.Invariant(
                        $"have r0={r.CapRadius} m, depth {r.CapDepth} m, stem r={r.StemRadius} m. logbook/specs/reef-spec.md §1."));
            }

            double swing = Math.Abs(r.A2) + Math.Abs(r.A3) + Math.Abs(r.A4);
            if (swing > OutlineSwing + 1e-12)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"Reef {index}'s outline amplitudes sum to {swing:0.####} of its radius, past {OutlineSwing}: ") +
                    "the outline would leave [0.6, 1.4] of r0. logbook/specs/reef-spec.md §1.");
            }

        }

        /// <summary>
        /// The flat underside's refusals, once the outline has been sampled: the blob the rim is
        /// rounded about is the outline moved in by <c>t/(2c)</c>, and it has to be there and wider
        /// than the stem, or the cap is a ball and not a table, or the stem stands out past it.
        /// </summary>
        private static void RefuseFlat(int index, Reef r, double flatMin, float thickness)
        {
            if (!(flatMin > 0d))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"Reef {index}'s cap (r0 {r.CapRadius:0.###} m) leaves no flat underside at its narrowest once its ") +
                    FormattableString.Invariant(
                        $"rim is rounded by half of {thickness} m: the cap is a ball and not a table. ") +
                    "logbook/specs/reef-spec.md §1.");
            }

            if (r.StemRadius > flatMin)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"Reef {index}'s stem of radius {r.StemRadius:0.###} m is wider than its cap's flat underside at its ") +
                    FormattableString.Invariant(
                        $"narrowest ({flatMin:0.###} m, the outline less the rim's rounding), so the stem would stand out ") +
                    "past the cap. logbook/specs/reef-spec.md §1.");
            }
        }

        /// <summary>How many reefs.</summary>
        public int Count => _reefs.Length;

        /// <summary>Every cap's thickness, m.</summary>
        public float CapThicknessMetres { get; }

        /// <summary>The streams' fade distance from the rock, m.</summary>
        public float FadeMetres { get; }

        /// <summary>The cover the placer was asked for; 0 for reefs built by hand.</summary>
        public double CoverTarget { get; }

        /// <summary>The cover the placer reached, counted on the grid's columns; 0 for reefs built by hand.</summary>
        public double CoverGot { get; }

        /// <summary>A reef's parameters as drawn.</summary>
        public Reef ReefAt(int reef) => _reefs[reef];

        /// <summary>A reef's axis, x, m, in the tank's frame.</summary>
        public double CentreX(int reef) => _reefs[reef].X;

        /// <summary>A reef's axis, z, m, in the tank's frame.</summary>
        public double CentreZ(int reef) => _reefs[reef].Z;

        /// <summary>A reef's cap's mean radius <c>r0</c>, m.</summary>
        public double CapRadius(int reef) => _reefs[reef].CapRadius;

        /// <summary>The height of a reef's cap's top, m (negative down).</summary>
        public double CapTopY(int reef) => -_reefs[reef].CapDepth;

        /// <summary>The height of a reef's cap's underside, m.</summary>
        public double CapUndersideY(int reef) => -(_reefs[reef].CapDepth + CapThicknessMetres);

        /// <summary>A reef's stem radius, m; 0 is the floating island.</summary>
        public double StemRadius(int reef) => _reefs[reef].StemRadius;

        /// <summary>A seed for the theatre's look of a reef's rock. Nothing in the simulation reads it.</summary>
        public uint NoiseSeed(int reef) => _reefs[reef].NoiseSeed;

        /// <summary>Whether a reef is the floating island: no stem.</summary>
        public bool IsIsland(int reef) => !(_reefs[reef].StemRadius > 0d);

        /// <summary>
        /// A reef's cap outline at an angle about its axis, m: <c>r(θ)</c>, the cap's outer
        /// radius at its mid-plane, with <c>θ</c> measured from +x towards +z.
        /// </summary>
        public double OutlineRadius(int reef, double theta)
        {
            Outline(_reefs[reef], theta, out double o, out _, out _, out _);
            return o;
        }

        /// <summary>Whether a horizontal point lies inside a reef's cap outline.</summary>
        public bool InsideOutline(int reef, double x, double z)
        {
            Reef r = _reefs[reef];
            double dx = x - r.X, dz = z - r.Z;
            double rho2 = dx * dx + dz * dz;

            if (r.Round) return rho2 <= r.CapRadius * r.CapRadius;

            double outer = _outerMax[reef];
            if (rho2 > outer * outer) return false;
            if (rho2 <= 0d) return true;

            Outline(r, Math.Atan2(dz, dx), out double o, out _, out _, out _);
            return rho2 <= o * o;
        }

        /// <summary>The outline and its first three derivatives in the angle.</summary>
        private static void Outline(in Reef r, double theta, out double o, out double o1, out double o2, out double o3)
        {
            double c2 = Math.Cos(2d * theta + r.P2), s2 = Math.Sin(2d * theta + r.P2);
            double c3 = Math.Cos(3d * theta + r.P3), s3 = Math.Sin(3d * theta + r.P3);
            double c4 = Math.Cos(4d * theta + r.P4), s4 = Math.Sin(4d * theta + r.P4);

            o = r.CapRadius * (1d + r.A2 * c2 + r.A3 * c3 + r.A4 * c4);
            o1 = -r.CapRadius * (2d * r.A2 * s2 + 3d * r.A3 * s3 + 4d * r.A4 * s4);
            o2 = -r.CapRadius * (4d * r.A2 * c2 + 9d * r.A3 * c3 + 16d * r.A4 * c4);
            o3 = r.CapRadius * (8d * r.A2 * s2 + 27d * r.A3 * s3 + 64d * r.A4 * s4);
        }

        // ------------------------------------------------------------------ the refusals

        /// <summary>
        /// Every refusal of the reef group that the config and the tank's radius can decide. At
        /// <see cref="RunConfig.ReefCover"/> 0 the only one is that every other reef dial stands at
        /// its default, which every recorded config does not have to know about.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="tankRadiusMetres">The tank's radius, m.</param>
        /// <param name="worldRules">
        /// False skips the two refusals about the rest of the world (a box, a field that is not a
        /// grid), for the theatre's picture reader, which reads a config the farm already ran.
        /// </param>
        public static void Refuse(RunConfig config, float tankRadiusMetres, bool worldRules = true)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            float cover = config.ReefCover;

            if (!(cover > 0f))
            {
                var defaults = new RunConfig();
                var off = new List<string>();

                if (config.ReefMaxCount != defaults.ReefMaxCount) off.Add(Name("ReefMaxCount", config.ReefMaxCount));
                if (config.ReefCapRadiusMinMetres != defaults.ReefCapRadiusMinMetres) off.Add(Name("ReefCapRadiusMinMetres", config.ReefCapRadiusMinMetres));
                if (config.ReefCapRadiusMaxMetres != defaults.ReefCapRadiusMaxMetres) off.Add(Name("ReefCapRadiusMaxMetres", config.ReefCapRadiusMaxMetres));
                if (config.ReefOutlineRoughness != defaults.ReefOutlineRoughness) off.Add(Name("ReefOutlineRoughness", config.ReefOutlineRoughness));
                if (config.ReefCapDepthMetres != defaults.ReefCapDepthMetres) off.Add(Name("ReefCapDepthMetres", config.ReefCapDepthMetres));
                if (config.ReefCapDepthJitterMetres != defaults.ReefCapDepthJitterMetres) off.Add(Name("ReefCapDepthJitterMetres", config.ReefCapDepthJitterMetres));
                if (config.ReefCapThicknessMetres != defaults.ReefCapThicknessMetres) off.Add(Name("ReefCapThicknessMetres", config.ReefCapThicknessMetres));
                if (config.ReefStemRadiusFraction != defaults.ReefStemRadiusFraction) off.Add(Name("ReefStemRadiusFraction", config.ReefStemRadiusFraction));
                if (config.ReefFadeMetres != defaults.ReefFadeMetres) off.Add(Name("ReefFadeMetres", config.ReefFadeMetres));

                if (off.Count == 0) return;

                // A reef group that names a reef with no reef would put numbers in the config's
                // hash that the world does not have.
                throw new ArgumentException(
                    "ReefCover is 0 and the reef group sets " + string.Join(", ", off) + ". With no reef every " +
                    "reef dial stands at its default, or the config's hash would carry a reef the world does not " +
                    "have. logbook/specs/reef-spec.md §1.",
                    nameof(config));
            }

            // A reef in a box: the streams and the mask are the tank's.
            if (worldRules && config.WorldShape != WorldShape.Tank)
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"WorldShape is {config.WorldShape} and the config asks for a reef cover of {cover}. ") +
                    "A reef is the tank's: its fade is on the streams' potential and its rock is a mask on " +
                    "the tank's grid, and a box has neither. logbook/specs/reef-spec.md §3.",
                    nameof(config));
            }

            if (worldRules && config.FieldModel != MatterField.Grid)
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"FieldModel is {config.FieldModel} and the config asks for a reef cover of {cover}. ") +
                    "The rock is dead cells in the grid, and only a grid has cells to kill. " +
                    "logbook/specs/reef-spec.md §2.",
                    nameof(config));
            }

            float rMin = config.ReefCapRadiusMinMetres;
            float rMax = config.ReefCapRadiusMaxMetres;
            float depth = config.ReefCapDepthMetres;
            float jitter = config.ReefCapDepthJitterMetres;
            float thickness = config.ReefCapThicknessMetres;
            float stem = config.ReefStemRadiusFraction;
            float fade = config.ReefFadeMetres;

            if (!(rMin > 0f) || !(rMax >= rMin) || !(thickness > 0f) || !(fade > 0f))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A reef needs a least cap radius above 0 and a largest at or above it, a cap thickness above 0 and ") +
                    FormattableString.Invariant(
                        $"a fade above 0; have r={rMin} to {rMax} m, t={thickness} m, fade {fade} m. logbook/specs/reef-spec.md §1."),
                    nameof(config));
            }

            // The narrowest outline any draw can make, and the flat underside left inside it.
            double narrowest = (1d - OutlineSwing) * rMin;

            if (!(narrowest > 0.5d * thickness))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A cap drawn at {rMin} m can be {narrowest:0.###} m in radius at its narrowest ({1d - OutlineSwing:0.#} ") +
                    FormattableString.Invariant(
                        $"of r0), and cannot be {thickness} m thick: its rim is rounded by half the thickness, so the ") +
                    "radius has to exceed that or the cap is a ball and not a table. logbook/specs/reef-spec.md §1.",
                    nameof(config));
            }

            if (stem * rMin > narrowest - 0.5d * thickness)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A stem of {stem} of its cap is {stem * rMin:0.###} m in radius under a cap drawn at {rMin} m, whose ") +
                    FormattableString.Invariant(
                        $"flat underside can be {narrowest - 0.5d * thickness:0.###} m at its narrowest (the outline at ") +
                    FormattableString.Invariant(
                        $"{1d - OutlineSwing:0.#} of r0 less the rim's rounding), so the stem could stand out past the cap. ") +
                    "logbook/specs/reef-spec.md §1.",
                    nameof(config));
            }

            // A cap that breaks the surface, unless it is the island.
            if (stem > 0f && depth - jitter < ShallowestCapMetres)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A cap's top at {depth} m ± {jitter} m under the surface on a stem could rise to {depth - jitter} m: ") +
                    FormattableString.Invariant(
                        $"a cap on a stem sits at least {ShallowestCapMetres} m down, refused rather than clamped. The floating ") +
                    "island is the one reef at the surface, and it has no stem (ReefStemRadiusFraction 0). " +
                    "logbook/specs/reef-spec.md §3.",
                    nameof(config));
            }

            if (!(stem > 0f) && (depth > 0f || jitter > 0f))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A reef with no stem has its cap at {depth} m ± {jitter} m under the surface, where nothing holds it: ") +
                    "the floating island's cap is at the surface with no jitter (ReefCapDepthMetres and " +
                    "ReefCapDepthJitterMetres 0), and a cap below it needs a stem. logbook/specs/reef-spec.md §1.",
                    nameof(config));
            }

            if (depth + jitter + thickness + RoomUnderCapMetres > config.WorldDepthMetres)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A cap {thickness} m thick with its top as deep as {depth + jitter} m needs {RoomUnderCapMetres} m of ") +
                    FormattableString.Invariant($"water under it, and the tank is {config.WorldDepthMetres} m deep. ") +
                    "logbook/specs/reef-spec.md §1.",
                    nameof(config));
            }

            if (!(tankRadiusMetres - (1d + OutlineSwing) * rMin - GlassClearanceMetres > 0d))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A cap drawn at {rMin} m can reach {(1d + OutlineSwing) * rMin:0.##} m from its axis, and has to stay ") +
                    FormattableString.Invariant(
                        $"{GlassClearanceMetres} m inside a glass {tankRadiusMetres:0.##} m in radius: no reef fits. ") +
                    "logbook/specs/reef-spec.md §1.",
                    nameof(config));
            }
        }

        private static string Name(string dial, float value) =>
            FormattableString.Invariant($"{dial} {value}");

        private static string Name(string dial, int value) =>
            FormattableString.Invariant($"{dial} {value}");

        // ------------------------------------------------------------------ the placement

        /// <summary>How many centres one reef is offered before the seed is refused.</summary>
        public const int PlacementAttempts = 4000;

        /// <summary>
        /// The world's reefs, drawn by the seed until their caps cover <see cref="RunConfig.ReefCover"/>
        /// of the tank's surface.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Its own stream, drawn only here.</b> A world with no reef never calls this, so no
        /// stream any recorded world draws from moves. Each reef draws, in order, its mean radius
        /// (uniform in the range), its depth (uniform in the jitter), its three amplitudes (uniform
        /// up to the roughness, the three scaled together when their sum passes
        /// <see cref="OutlineSwing"/>), its three phases, its rock's noise seed, and then centres,
        /// uniform over the disc its whole outline stays <see cref="GlassClearanceMetres"/> inside,
        /// until one stands over water deep enough at its axis and at eight points of its outline
        /// (<see cref="PlacementAttempts"/> tries, then refused). Caps may overlap.
        /// </para>
        /// <para>
        /// <b>The cover is counted on the grid's own columns</b> (<see cref="RunConfig.FieldCellMetres"/>),
        /// the cells whose centres lie inside the glass and inside any cap's outline, against every
        /// column inside the glass, so the rock the mask cuts and the cover the header prints are
        /// one count. The last reef drawn is kept only when keeping it lands nearer the target
        /// than leaving it out, so the cover lands within half a reef's share of the target. More
        /// than <see cref="RunConfig.ReefMaxCount"/> reefs needed is a refusal, not a truncation.
        /// </para>
        /// </remarks>
        public static ReefGeometry Place(
            RunConfig config, float tankRadiusMetres, BedShape bed, ulong streamSeed, bool worldRules = true)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!(config.ReefCover > 0f)) return null;

            Refuse(config, tankRadiusMetres, worldRules);

            double thickness = config.ReefCapThicknessMetres;
            double half = 0.5d * thickness;
            BedShape floor = bed != null && bed.HasRelief ? bed : null;

            // The grid's own columns: centres at (i + ½)·h across the tank's 2R, alive when
            // inside the glass — GridField's test in its own float arithmetic.
            float cell = config.FieldCellMetres > 0f ? config.FieldCellMetres : 1f;
            int n = (int)Math.Ceiling(2d * tankRadiusMetres / cell - 1e-6);
            var inTank = new bool[n * n];
            var covered = new bool[n * n];
            int tankColumns = 0;

            for (int ix = 0; ix < n; ix++)
            {
                float cx = (ix + 0.5f) * cell;
                for (int iz = 0; iz < n; iz++)
                {
                    float cz = (iz + 0.5f) * cell;
                    if (!TankGeometry.Inside(cx, cz, tankRadiusMetres)) continue;
                    inTank[ix * n + iz] = true;
                    tankColumns++;
                }
            }

            double target = config.ReefCover * (double)tankColumns;
            int coveredColumns = 0;

            var rng = new Rng(streamSeed);
            var reefs = new List<Reef>();
            var fresh = new List<int>();

            while (coveredColumns < target)
            {
                if (reefs.Count >= config.ReefMaxCount)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"The seed's reef stream reached the ceiling of {config.ReefMaxCount} reefs with the caps covering ") +
                        FormattableString.Invariant(
                            $"{coveredColumns / (double)tankColumns:0.###} of the tank against a cover of {config.ReefCover}. ") +
                        "Raise ReefMaxCount, widen the caps or lower the cover. logbook/specs/reef-spec.md §1.",
                        nameof(config));
                }

                Reef reef = Draw(config, rng);

                // The outline's reach, for the glass: sampled with a margin, as the class does.
                double outer = 0d;
                for (int s = 0; s < 512; s++)
                {
                    Outline(reef, 2d * Math.PI * s / 512d, out double o, out _, out _, out _);
                    outer = Math.Max(outer, o);
                }

                outer += 1e-3d * reef.CapRadius;

                double allowed = tankRadiusMetres - outer - GlassClearanceMetres;
                double needBelow = reef.CapDepth + thickness + RoomUnderCapMetres;
                bool found = false;

                for (int attempt = 0; attempt < PlacementAttempts && allowed > 0d; attempt++)
                {
                    // Uniform over the disc: r = a·sqrt(u).
                    double r = allowed * Math.Sqrt(rng.NextFloat());
                    double theta = 2d * Math.PI * rng.NextFloat();
                    reef.X = tankRadiusMetres + r * Math.Cos(theta);
                    reef.Z = tankRadiusMetres + r * Math.Sin(theta);

                    if (DeepEnough(floor, config.WorldDepthMetres, reef, needBelow))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant(
                            $"The seed's reef stream found no spot for reef {reefs.Count} (r0 {reef.CapRadius:0.##} m, reaching ") +
                        FormattableString.Invariant(
                            $"{outer:0.##} m) {GlassClearanceMetres} m inside the glass over water at least {needBelow:0.##} m ") +
                        FormattableString.Invariant(
                            $"deep under its centre and eight points of its outline in {PlacementAttempts} tries. The tank's ") +
                        "deep water is too small for caps this wide on this seed's floor. logbook/specs/reef-spec.md §1.",
                        nameof(config));
                }

                // The columns this cap would add.
                fresh.Clear();
                int lo = Math.Max(0, (int)Math.Floor((reef.X - outer) / cell));
                int hi = Math.Min(n - 1, (int)Math.Ceiling((reef.X + outer) / cell));
                int loZ = Math.Max(0, (int)Math.Floor((reef.Z - outer) / cell));
                int hiZ = Math.Min(n - 1, (int)Math.Ceiling((reef.Z + outer) / cell));

                for (int ix = lo; ix <= hi; ix++)
                {
                    double cx = (ix + 0.5f) * cell;
                    for (int iz = loZ; iz <= hiZ; iz++)
                    {
                        int column = ix * n + iz;
                        if (!inTank[column] || covered[column]) continue;
                        double cz = (iz + 0.5f) * cell;
                        if (InsideOutlineOf(reef, cx - reef.X, cz - reef.Z)) fresh.Add(column);
                    }
                }

                double with = coveredColumns + fresh.Count;
                if (reefs.Count > 0 && with > target && with - target > target - coveredColumns) break;

                reefs.Add(reef);
                foreach (int column in fresh) covered[column] = true;
                coveredColumns += fresh.Count;
            }

            return new ReefGeometry(
                config.ReefCapThicknessMetres, config.ReefFadeMetres, reefs.ToArray(),
                config.ReefCover, coveredColumns / (double)tankColumns);
        }

        /// <summary>One reef's shape from the stream, before its centre.</summary>
        private static Reef Draw(RunConfig config, Rng rng)
        {
            var reef = new Reef();

            reef.CapRadius = config.ReefCapRadiusMinMetres +
                             (config.ReefCapRadiusMaxMetres - (double)config.ReefCapRadiusMinMetres) * rng.NextFloat();
            reef.CapDepth = config.ReefCapDepthMetres +
                            config.ReefCapDepthJitterMetres * (2d * rng.NextFloat() - 1d);

            // The island's cap is at the surface exactly, whatever the draw rounds to.
            if (!(config.ReefStemRadiusFraction > 0f)) reef.CapDepth = 0d;

            double rough = config.ReefOutlineRoughness;
            double a2 = rough * rng.NextFloat();
            double a3 = rough * rng.NextFloat();
            double a4 = rough * rng.NextFloat();
            double swing = a2 + a3 + a4;

            if (swing > OutlineSwing)
            {
                double scale = OutlineSwing / swing;
                a2 *= scale;
                a3 *= scale;
                a4 *= scale;
            }

            reef.A2 = a2;
            reef.A3 = a3;
            reef.A4 = a4;
            reef.P2 = 2d * Math.PI * rng.NextFloat();
            reef.P3 = 2d * Math.PI * rng.NextFloat();
            reef.P4 = 2d * Math.PI * rng.NextFloat();
            reef.NoiseSeed = rng.NextUInt();
            reef.StemRadius = config.ReefStemRadiusFraction * reef.CapRadius;
            return reef;
        }

        private static bool InsideOutlineOf(in Reef reef, double dx, double dz)
        {
            double rho2 = dx * dx + dz * dz;
            if (reef.Round) return rho2 <= reef.CapRadius * reef.CapRadius;
            if (rho2 <= 0d) return true;

            Outline(reef, Math.Atan2(dz, dx), out double o, out _, out _, out _);
            return rho2 <= o * o;
        }

        private static bool DeepEnough(BedShape bed, float worldDepth, in Reef reef, double needBelow)
        {
            if (bed == null) return worldDepth >= needBelow;

            if (-bed.FloorY(reef.X, reef.Z) < needBelow) return false;

            for (int k = 0; k < 8; k++)
            {
                double a = k * (Math.PI / 4d);
                Outline(reef, a, out double o, out _, out _, out _);
                if (-bed.FloorY(reef.X + o * Math.Cos(a), reef.Z + o * Math.Sin(a)) < needBelow) return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ the distance

        /// <summary>
        /// One reef's signed distance and its first and second derivatives. The Hessian is
        /// symmetric and held as its six upper entries.
        /// </summary>
        public struct Distance
        {
            /// <summary>The signed distance, m: negative inside the rock.</summary>
            public double S;

            /// <summary>The gradient: the outward normal on the rock's surface.</summary>
            public double Gx, Gy, Gz;

            /// <summary>The Hessian's six entries, per metre.</summary>
            public double Hxx, Hxy, Hxz, Hyy, Hyz, Hzz;
        }

        /// <summary>
        /// The signed distance to the rock, m: negative inside, the minimum over the reefs.
        /// </summary>
        public double SignedDistance(double x, double y, double z) =>
            SignedDistance(x, y, z, out _, out _);

        /// <summary>
        /// The signed distance to the rock, the reef it is nearest, and the full derivative
        /// record of that reef's distance at the point.
        /// </summary>
        public double SignedDistance(double x, double y, double z, out int nearest, out Distance at) =>
            SignedDistance(x, y, z, double.PositiveInfinity, out nearest, out at);

        /// <summary>
        /// The signed distance to the rock when it is under <paramref name="limit"/>; otherwise a
        /// lower bound on it at or above the limit, with <paramref name="nearest"/> −1 and
        /// <paramref name="at"/> empty. The contact and the placer ask only whether a sphere
        /// reaches the rock, and a reef whose cheap lower bound is past the limit is never
        /// evaluated.
        /// </summary>
        /// <remarks>
        /// The minimum is exact whatever the order: every reef is bounded first, the one with the
        /// least bound is evaluated first, and a reef is evaluated only when its bound is under
        /// the best so far. A tie keeps the lower index.
        /// </remarks>
        public double SignedDistance(double x, double y, double z, double limit, out int nearest, out Distance at)
        {
            nearest = -1;
            at = default(Distance);

            int count = _reefs.Length;
            int first = -1;
            double firstBound = double.PositiveInfinity;

            for (int reef = 0; reef < count; reef++)
            {
                double bound = LowerBound(reef, x, y, z);
                if (bound < firstBound)
                {
                    firstBound = bound;
                    first = reef;
                }
            }

            if (first < 0 || firstBound >= limit) return firstBound;

            at = OfReef(first, x, y, z);
            nearest = first;
            double best = at.S;

            for (int reef = 0; reef < count; reef++)
            {
                if (reef == first) continue;

                double bound = LowerBound(reef, x, y, z);
                if (bound > best || (bound == best && reef > nearest)) continue;

                Distance d = OfReef(reef, x, y, z);
                if (d.S < best || (d.S == best && reef < nearest))
                {
                    best = d.S;
                    nearest = reef;
                    at = d;
                }
            }

            return best;
        }

        /// <summary>
        /// A lower bound on one reef's distance that needs one square root: the cap's distance is
        /// at least <c>|y − y_mid| − t/2</c> everywhere and at least <c>c_min·(ρ − r_max)</c> past
        /// the outline's widest, the stem's at least <c>max(ρ − r_s, y − y_mid)</c>, and the blend
        /// lowers the smaller by at most a sixth of the fillet.
        /// </summary>
        private double LowerBound(int reef, double x, double y, double z)
        {
            Reef r = _reefs[reef];
            double dx = x - r.X, dz = z - r.Z;
            double rho = Math.Sqrt(dx * dx + dz * dz);
            double v = y - _capMidY[reef];

            double cap = Math.Abs(v) - _halfThickness;
            double past = rho - _outerMax[reef];
            if (past > 0d) cap = Math.Max(cap, _cosMin[reef] * past);
            if (!(r.StemRadius > 0d)) return cap - 1e-9;

            double stem = Math.Max(rho - r.StemRadius, v);
            return Math.Min(cap, stem) - _fillet / 6d - 1e-9;
        }

        /// <summary>Whether a point is inside the rock of any reef.</summary>
        public bool Inside(double x, double y, double z) =>
            SignedDistance(x, y, z, 0d, out _, out _) < 0d;

        /// <summary>
        /// One reef's distance: the cap and the stem joined by the smooth minimum — see the class
        /// remarks.
        /// </summary>
        public Distance OfReef(int reef, double x, double y, double z)
        {
            Reef r = _reefs[reef];
            double dx = x - r.X;
            double dz = z - r.Z;
            double rho = Math.Sqrt(dx * dx + dz * dz);

            double nx = rho > 0d ? dx / rho : 1d;
            double nz = rho > 0d ? dz / rho : 0d;

            Distance cap = r.Round
                ? RoundCap(rho, nx, nz, y - _capMidY[reef], r.CapRadius - _halfThickness)
                : Cap(r, rho, nx, nz, dx, dz, y - _capMidY[reef]);

            if (!(r.StemRadius > 0d)) return cap;

            Distance stem = Stem(rho, nx, nz, y - _capMidY[reef], r.StemRadius);
            return SmoothUnion(cap, stem, _fillet);
        }

        /// <summary>The round cap: the exact distance to a rounded disc, the first build's arithmetic.</summary>
        private Distance RoundCap(double rho, double nx, double nz, double v, double discRadius)
        {
            var d = default(Distance);
            double q = rho - discRadius;

            if (q > 0d)
            {
                double r2 = Math.Sqrt(q * q + v * v);
                d.S = r2 - _halfThickness;

                if (!(r2 > 0d))
                {
                    d.Gy = 1d;
                    return d;
                }

                double sr = q / r2;
                double sv = v / r2;
                double cube = r2 * r2 * r2;

                d.Gx = sr * nx;
                d.Gy = sv;
                d.Gz = sr * nz;

                AddRevolution(ref d, v * v / cube, -q * v / cube, q * q / cube, sr / rho, nx, nz);
                return d;
            }

            d.S = Math.Abs(v) - _halfThickness;
            d.Gy = v >= 0d ? 1d : -1d;
            return d;
        }

        /// <summary>
        /// The cap with an outline: <c>√(q² + v²) − t/2</c> where <c>q = (ρ − r(θ))·c(θ) + t/2</c>
        /// is positive, and <c>|v| − t/2</c> over the flat blob — see the class remarks.
        /// </summary>
        private Distance Cap(in Reef r, double rho, double nx, double nz, double dx, double dz, double v)
        {
            var d = default(Distance);

            if (!(rho > 0d))
            {
                // On the axis: inside every flat blob the refusals allow.
                d.S = Math.Abs(v) - _halfThickness;
                d.Gy = v >= 0d ? 1d : -1d;
                return d;
            }

            Outline(r, Math.Atan2(dz, dx), out double o, out double r1, out double r2d, out double r3);

            // The slope's cosine and its two derivatives in θ: c = r/W, W = √(r² + r′²).
            double w2 = o * o + r1 * r1;
            double w = Math.Sqrt(w2);
            double c = o / w;
            double e = rho - o;
            double q = e * c + _halfThickness;

            if (!(q > 0d))
            {
                d.S = Math.Abs(v) - _halfThickness;
                d.Gy = v >= 0d ? 1d : -1d;
                return d;
            }

            double w3 = w2 * w;
            double w5 = w3 * w2;
            double p = r1 * (r1 * r1 - o * r2d);
            double c1 = p / w3;
            double p1 = 2d * r1 * r1 * r2d - o * r2d * r2d - o * r1 * r3;
            double c2 = p1 / w3 - 3d * p * r1 * (o + r2d) / w5;

            // q's polar derivatives (q_ρρ = 0).
            double qr = c;
            double qt = -r1 * c + e * c1;
            double qrt = c1;
            double qtt = -r2d * c - 2d * r1 * c1 + e * c2;

            // To Cartesian: e_ρ = (nx, nz), e_θ = (−nz, nx).
            double tx = -nz, tz = nx;
            double gqx = qr * nx + qt / rho * tx;
            double gqz = qr * nz + qt / rho * tz;

            double mixed = qrt / rho - qt / (rho * rho);
            double ring = qtt / (rho * rho) + qr / rho;

            double hqxx = 2d * mixed * nx * tx + ring * tx * tx;
            double hqxz = mixed * (nx * tz + tx * nz) + ring * tx * tz;
            double hqzz = 2d * mixed * nz * tz + ring * tz * tz;

            double s = Math.Sqrt(q * q + v * v);
            d.S = s - _halfThickness;

            if (!(s > 0d))
            {
                d.Gy = 1d;
                return d;
            }

            double gx = q * gqx / s, gy = v / s, gz = q * gqz / s;
            d.Gx = gx;
            d.Gy = gy;
            d.Gz = gz;

            // s = √(q² + v²): ∇∇s = (∇q∇qᵀ + q∇∇q + ŷŷᵀ − ∇s∇sᵀ)/s, and q has no y in it.
            d.Hxx = (gqx * gqx + q * hqxx - gx * gx) / s;
            d.Hxz = (gqx * gqz + q * hqxz - gx * gz) / s;
            d.Hzz = (gqz * gqz + q * hqzz - gz * gz) / s;
            d.Hxy = (-gx * gy) / s;
            d.Hyz = (-gz * gy) / s;
            d.Hyy = (1d - gy * gy) / s;
            return d;
        }

        private static Distance Stem(double rho, double nx, double nz, double ey, double stemRadius)
        {
            var d = default(Distance);
            double er = rho - stemRadius;

            if (er > 0d && ey > 0d)
            {
                double r3 = Math.Sqrt(er * er + ey * ey);
                double sr = er / r3;
                double sv = ey / r3;
                double cube = r3 * r3 * r3;

                d.S = r3;
                d.Gx = sr * nx;
                d.Gy = sv;
                d.Gz = sr * nz;

                AddRevolution(ref d, ey * ey / cube, -er * ey / cube, er * er / cube, sr / rho, nx, nz);
                return d;
            }

            if (er > 0d || (ey <= 0d && er > ey))
            {
                // The side of the stem, or inside it nearer the side than the top.
                d.S = er;
                d.Gx = nx;
                d.Gz = nz;

                if (rho > 0d) AddRevolution(ref d, 0d, 0d, 0d, 1d / rho, nx, nz);
                return d;
            }

            // Above the top and inside the radius, or inside nearer the top.
            d.S = ey;
            d.Gy = 1d;
            return d;
        }

        /// <summary>
        /// Adds the Hessian of a function of <c>(ρ, y)</c>: its second derivatives in the
        /// meridian plane along <c>n</c> and <c>ŷ</c>, and the ring's curvature <c>(∂s/∂ρ)/ρ</c>
        /// across it.
        /// </summary>
        private static void AddRevolution(
            ref Distance d, double hrr, double hry, double hyy, double curvature, double nx, double nz)
        {
            d.Hxx += hrr * nx * nx + curvature * (1d - nx * nx);
            d.Hxz += hrr * nx * nz - curvature * nx * nz;
            d.Hzz += hrr * nz * nz + curvature * (1d - nz * nz);
            d.Hxy += hry * nx;
            d.Hyz += hry * nz;
            d.Hyy += hyy;
        }

        /// <summary>
        /// The cubic smooth minimum of two distances and its derivatives:
        /// <c>min(a, b) − k·h³/6</c> with <c>h = max(k − |a − b|, 0)/k</c>.
        /// </summary>
        private static Distance SmoothUnion(in Distance a, in Distance b, double k)
        {
            double gap = a.S - b.S;
            double spread = Math.Abs(gap);
            if (spread >= k) return gap <= 0d ? a : b;

            double h = (k - spread) / k;
            double wa = gap < 0d ? 1d - 0.5d * h * h : 0.5d * h * h;
            double wb = 1d - wa;
            double bend = h / k;

            double ex = a.Gx - b.Gx, ey = a.Gy - b.Gy, ez = a.Gz - b.Gz;

            var d = default(Distance);
            d.S = Math.Min(a.S, b.S) - k * h * h * h / 6d;
            d.Gx = wa * a.Gx + wb * b.Gx;
            d.Gy = wa * a.Gy + wb * b.Gy;
            d.Gz = wa * a.Gz + wb * b.Gz;

            d.Hxx = wa * a.Hxx + wb * b.Hxx - bend * ex * ex;
            d.Hxy = wa * a.Hxy + wb * b.Hxy - bend * ex * ey;
            d.Hxz = wa * a.Hxz + wb * b.Hxz - bend * ex * ez;
            d.Hyy = wa * a.Hyy + wb * b.Hyy - bend * ey * ey;
            d.Hyz = wa * a.Hyz + wb * b.Hyz - bend * ey * ez;
            d.Hzz = wa * a.Hzz + wb * b.Hzz - bend * ez * ez;

            return d;
        }

        // ------------------------------------------------------------------ the fade

        /// <summary>
        /// The streams' fade at a point, <c>g</c>: the product over the reefs of each reef's
        /// quintic <c>6ξ⁵ − 15ξ⁴ + 10ξ³</c> of <c>ξ = s/fade</c>, so 0 in any rock and 1 beyond the
        /// fade from every reef.
        /// </summary>
        public double FadeAt(double x, double y, double z)
        {
            double g = 1d;

            for (int reef = 0; reef < _reefs.Length; reef++)
            {
                if (!Reaches(reef, x, y, z)) continue;

                double xi = OfReef(reef, x, y, z).S / FadeMetres;
                if (xi >= 1d) continue;
                if (xi <= 0d) return 0d;

                g *= xi * xi * xi * (10d + xi * (-15d + 6d * xi));
            }

            return g;
        }

        /// <summary>Whether a reef's fade can reach a point: its horizontal reach, then its lower bound.</summary>
        private bool Reaches(int reef, double x, double y, double z)
        {
            double dx = x - _reefs[reef].X;
            double dz = z - _reefs[reef].Z;
            if (dx * dx + dz * dz >= _reach[reef] * _reach[reef]) return false;
            return LowerBound(reef, x, y, z) < FadeMetres;
        }

        /// <summary>
        /// The horizontal distance from a reef's axis past which its fade is 1 at every height, m.
        /// </summary>
        public double FadeReach(int reef) => _reach[reef];

        /// <summary>
        /// The fade, its gradient and its Hessian at a point — what the streams' velocity
        /// (<c>g·u + ∇g × A</c>) and the closed-form acceleration need. Returns false, with
        /// <c>g</c> = 1 and every derivative 0, where no reef's fade reaches.
        /// </summary>
        public bool Fade(double x, double y, double z, out Distance fade)
        {
            // The running product and its derivatives, S carrying the value.
            fade = default(Distance);
            fade.S = 1d;
            bool touched = false;

            for (int reef = 0; reef < _reefs.Length; reef++)
            {
                if (!Reaches(reef, x, y, z)) continue;

                Distance s = OfReef(reef, x, y, z);
                double xi = s.S / FadeMetres;
                if (xi >= 1d) continue;

                touched = true;

                if (xi <= 0d)
                {
                    fade = default(Distance);
                    return true;
                }

                double rest = 1d - xi;
                double q = xi * xi * xi * (10d + xi * (-15d + 6d * xi));
                double q1 = 30d * xi * xi * rest * rest / FadeMetres;
                double q2 = 60d * xi * rest * (1d - 2d * xi) / (FadeMetres * FadeMetres);

                // This reef's factor f = q(s): ∇f = q'∇s, ∇∇f = q''∇s∇sᵀ + q'∇∇s.
                var f = default(Distance);
                f.S = q;
                f.Gx = q1 * s.Gx; f.Gy = q1 * s.Gy; f.Gz = q1 * s.Gz;
                f.Hxx = q2 * s.Gx * s.Gx + q1 * s.Hxx;
                f.Hxy = q2 * s.Gx * s.Gy + q1 * s.Hxy;
                f.Hxz = q2 * s.Gx * s.Gz + q1 * s.Hxz;
                f.Hyy = q2 * s.Gy * s.Gy + q1 * s.Hyy;
                f.Hyz = q2 * s.Gy * s.Gz + q1 * s.Hyz;
                f.Hzz = q2 * s.Gz * s.Gz + q1 * s.Hzz;

                // The product rule: P·f, ∇(Pf) = f∇P + P∇f, ∇∇(Pf) = f∇∇P + ∇P∇fᵀ + ∇f∇Pᵀ + P∇∇f.
                Distance p = fade;
                var n = default(Distance);
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

        // ------------------------------------------------------------------ the light

        /// <summary>
        /// The share of the light a point receives past the caps, 0 to 1: <see cref="CapTransmission"/>
        /// under any cap (inside its outline and below its top), 1 everywhere else.
        /// </summary>
        /// <remarks>
        /// <see cref="LightField"/> multiplies a body's irradiance by this. The cap's top is lit as
        /// any water is; the stem casts nothing, being vertical.
        /// </remarks>
        public float LightTransmission(float x, float heightY, float z)
        {
            for (int reef = 0; reef < _reefs.Length; reef++)
            {
                if (!(heightY < -_reefs[reef].CapDepth)) continue;
                if (InsideOutline(reef, x, z)) return CapTransmission;
            }

            return 1f;
        }

        /// <summary>
        /// What a cap lets through to the column under it: nothing. The cap is rock, and the
        /// owner's requirement for the reef was that it block the sun under it (2026-09-23:
        /// "make sure they block the sun under them"; that night: "the idea is to have areas in
        /// the tank that do not get light"). One constant to change.
        /// </summary>
        public static readonly float CapTransmission = 0f;

        /// <summary>
        /// The header's token: <c>reefs 23 cover 0.25 (0.247 got) cap r=6-16 m rough 0.15 at 3 m ±1
        /// t=2 m stem 0.25 fade 8 m</c>, or <c>no reef</c>.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="reefs">The world's reefs, for the count and the cover got; null prints them as unknown.</param>
        public static string HeaderToken(RunConfig config, ReefGeometry reefs)
        {
            if (config == null || !(config.ReefCover > 0f)) return "no reef";

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return
                "reefs " + (reefs != null ? reefs.Count.ToString(inv) : "?") +
                " cover " + config.ReefCover.ToString("0.###", inv) +
                " (" + (reefs != null ? reefs.CoverGot.ToString("0.000", inv) : "?") + " got)" +
                " cap r=" + config.ReefCapRadiusMinMetres.ToString("0.##", inv) +
                "-" + config.ReefCapRadiusMaxMetres.ToString("0.##", inv) +
                " m rough " + config.ReefOutlineRoughness.ToString("0.###", inv) +
                " at " + config.ReefCapDepthMetres.ToString("0.##", inv) +
                " m ±" + config.ReefCapDepthJitterMetres.ToString("0.##", inv) +
                " t=" + config.ReefCapThicknessMetres.ToString("0.##", inv) +
                " m stem " + config.ReefStemRadiusFraction.ToString("0.###", inv) +
                " fade " + config.ReefFadeMetres.ToString("0.##", inv) + " m";
        }
    }
}

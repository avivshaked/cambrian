using System;

namespace Evosim.Core
{
    /// <summary>
    /// The mushroom reefs of round 47: rock columns standing on the tank's floor, each topped by a
    /// cap that overhangs and shades — <c>logbook/specs/reef-spec.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The shape.</b> A reef is a solid of revolution about a vertical axis at a place
    /// <c>(x, z)</c> in the tank's own frame (the axis of the tank at <c>(R, R)</c>, as
    /// <see cref="TankGeometry"/> and <see cref="BedShape"/> have it). Its cap is a disc of rock of
    /// radius <see cref="CapRadiusMetres"/> and thickness <see cref="CapThicknessMetres"/> whose top
    /// lies <see cref="CapDepthMetres"/> under the surface, with its rim rounded by half the
    /// thickness: it is the set of points within <c>t/2</c> of a flat disc of radius
    /// <c>r_c − t/2</c> at the cap's mid-plane, which is what makes a body slide off the rim rather
    /// than catch on a corner. Its stem is a vertical cylinder of radius
    /// <see cref="StemRadiusMetres"/> from below the deepest floor up to the cap's mid-plane, so the
    /// stem's top is buried in the cap and the floor it stands on is whatever the bed is there. A
    /// stem radius of 0 with the cap at the surface is the floating island: a disc of rock at the
    /// waterline with nothing under it.
    /// </para>
    /// <para>
    /// <b>One signed distance, read by everything.</b> <see cref="SignedDistance(double, double, double)"/>
    /// is negative inside the rock, zero on its surface and the distance to it outside. The two
    /// grids mask a cell whose centre it calls rock, the placer and the contacts read it against a
    /// body's radius, the divergence guard reads it against the root, and the streams fade on it. The
    /// stem and the cap are joined by a smooth minimum with a fillet of half the cap's thickness
    /// (a cubic blend, twice continuously differentiable in the two distances), because the plain
    /// minimum has a crease along the bisector of the concave corner under the cap and the water's
    /// velocity is a derivative of the fade: a crease in the distance would be a shear sheet in the
    /// water. The blend lowers the distance by at most a sixth of the fillet, only inside that
    /// corner, and cannot reach above the cap's mid-plane (the stem's top is a smaller disc in the
    /// same plane, so the cap is at least half a thickness nearer there).
    /// </para>
    /// <para>
    /// ⚠ <b>The distance to a disc is C¹ and not C²</b> across the vertical cylinder through the
    /// flat disc's edge (<c>ρ = r_c − t/2</c>), above and below the cap: over the disc the
    /// distance is <c>|y − y_c| − t/2</c>, whose curvature is zero, and beyond it the curvature in
    /// the radial direction starts at <c>1/|y − y_c|</c>. The stem's distance has the same seam
    /// across the plane of its top, which the cap hides. So the water's velocity is continuous
    /// everywhere and its Jacobian, and the acceleration a body feels, jumps across those two
    /// surfaces within the fade. It is stated rather than smoothed: every closed-form rock with a
    /// flat face has it, and the beach's C² ruling was about a seam in the depth factor that had no
    /// geometry behind it.
    /// </para>
    /// <para>
    /// <b>Placed by the seed and its own stream</b> (<see cref="World.ReefPlacementIndex"/>), with
    /// no draw at all in a world with no reef: every stream a recorded world draws from is the one
    /// it drew from before. See <see cref="Place"/> for the rule.
    /// </para>
    /// </remarks>
    public sealed class ReefGeometry
    {
        /// <summary>How far inside the glass a cap's rim must stay, m — the spec's <c>r_c + 5 m</c>.</summary>
        public const float GlassClearanceMetres = 5f;

        /// <summary>
        /// The water a cap must leave under itself, m: the floor under the whole cap lies at least
        /// <c>d_cap + t + 2 m</c> down, so the beach's shelf carries no reef.
        /// </summary>
        public const float RoomUnderCapMetres = 2f;

        private readonly double[] _x;
        private readonly double[] _z;

        // Derived once: the cap's mid-plane, the flat disc the cap is rounded about, the fillet.
        private readonly double _capMidY;
        private readonly double _halfThickness;
        private readonly double _discRadius;
        private readonly double _fillet;
        private readonly double _reach;

        /// <summary>
        /// Reefs at the given centres, with one set of dimensions. The world builds this through
        /// <see cref="Place"/>; tests build it directly.
        /// </summary>
        public ReefGeometry(
            float capRadiusMetres, float capDepthMetres, float capThicknessMetres,
            float stemRadiusMetres, float fadeMetres, double[] centresX, double[] centresZ)
        {
            if (centresX == null) throw new ArgumentNullException(nameof(centresX));
            if (centresZ == null) throw new ArgumentNullException(nameof(centresZ));
            if (centresX.Length != centresZ.Length)
                throw new ArgumentException("One z for every x.", nameof(centresZ));

            RefuseDimensions(capRadiusMetres, capDepthMetres, capThicknessMetres, stemRadiusMetres, fadeMetres);

            CapRadiusMetres = capRadiusMetres;
            CapDepthMetres = capDepthMetres;
            CapThicknessMetres = capThicknessMetres;
            StemRadiusMetres = stemRadiusMetres;
            FadeMetres = fadeMetres;

            _x = (double[])centresX.Clone();
            _z = (double[])centresZ.Clone();

            _halfThickness = 0.5d * capThicknessMetres;
            _capMidY = -((double)capDepthMetres + _halfThickness);
            _discRadius = capRadiusMetres - _halfThickness;
            _fillet = _halfThickness;

            // The horizontal distance from an axis past which a reef's distance is at least its
            // fade: the cap's and the stem's distances are both at least ρ − r_c, and the blend
            // lowers the smaller by at most a sixth of the fillet.
            _reach = capRadiusMetres + fadeMetres + _fillet / 6d + 1e-9;
        }

        /// <summary>How many reefs.</summary>
        public int Count => _x.Length;

        /// <summary>The caps' radius, m.</summary>
        public float CapRadiusMetres { get; }

        /// <summary>The depth of a cap's top, m.</summary>
        public float CapDepthMetres { get; }

        /// <summary>A cap's thickness, m.</summary>
        public float CapThicknessMetres { get; }

        /// <summary>The stems' radius, m; 0 is the floating island.</summary>
        public float StemRadiusMetres { get; }

        /// <summary>The streams' fade distance from the rock, m.</summary>
        public float FadeMetres { get; }

        /// <summary>A reef's axis, x, m, in the tank's frame.</summary>
        public double CentreX(int reef) => _x[reef];

        /// <summary>A reef's axis, z, m, in the tank's frame.</summary>
        public double CentreZ(int reef) => _z[reef];

        /// <summary>The height of a cap's top, m (negative down).</summary>
        public double CapTopY => -(double)CapDepthMetres;

        /// <summary>The height of a cap's underside, m.</summary>
        public double CapUndersideY => -((double)CapDepthMetres + CapThicknessMetres);

        /// <summary>Whether this is the floating island: no stem.</summary>
        public bool IsIsland => !(StemRadiusMetres > 0f);

        /// <summary>
        /// The dimensions' own refusals, the ones that need nothing but the six numbers — the
        /// world's are in <see cref="Refuse"/>.
        /// </summary>
        private static void RefuseDimensions(float capRadius, float capDepth, float thickness, float stem, float fade)
        {
            if (!(capRadius > 0f) || !(thickness > 0f) || !(fade > 0f) || !(capDepth >= 0f) || !(stem >= 0f) ||
                float.IsInfinity(capRadius) || float.IsInfinity(thickness) || float.IsInfinity(fade) ||
                float.IsInfinity(capDepth) || float.IsInfinity(stem))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A reef needs a cap radius, a cap thickness and a fade above 0 and a cap depth and a stem radius at or above 0; ") +
                    FormattableString.Invariant(
                        $"have cap r={capRadius} m, t={thickness} m, depth {capDepth} m, stem r={stem} m, fade {fade} m. ") +
                    "logbook/specs/reef-spec.md §1.");
            }

            if (!(capRadius > 0.5f * thickness))
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A cap of radius {capRadius} m cannot be {thickness} m thick: its rim is rounded by half the ") +
                    "thickness, so the radius has to exceed that or the cap is a ball and not a table. " +
                    "logbook/specs/reef-spec.md §1.");
            }

            if (stem > capRadius - 0.5f * thickness)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A stem of radius {stem} m is wider than the cap's flat underside ({capRadius - 0.5f * thickness} m, ") +
                    "the cap radius less the rim's rounding), so the stem would stand out past the cap and there " +
                    "would be no overhang to shade anything. logbook/specs/reef-spec.md §1.");
            }
        }

        // ------------------------------------------------------------------ the refusals

        /// <summary>
        /// Every refusal of <c>logbook/specs/reef-spec.md</c> §3 that the config alone can decide,
        /// and the ones that need the tank. Skipped entirely at <see cref="RunConfig.ReefCount"/> 0
        /// with every other dial at 0, which is every recorded config.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <param name="tankRadiusMetres">The tank's radius, m.</param>
        /// <param name="worldRules">
        /// False skips the two refusals about the rest of the world (a box, a field that is not a
        /// grid), for the theatre's picture reader, which reads a config the farm already ran and
        /// does not read its field model.
        /// </param>
        public static void Refuse(RunConfig config, float tankRadiusMetres, bool worldRules = true)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            int count = config.ReefCount;
            float capRadius = config.ReefCapRadiusMetres;
            float capDepth = config.ReefCapDepthMetres;
            float thickness = config.ReefCapThicknessMetres;
            float stem = config.ReefStemRadiusMetres;
            float fade = config.ReefFadeMetres;

            bool anyDial = capRadius > 0f || capDepth > 0f || thickness > 0f || stem > 0f || fade > 0f;

            if (count == 0)
            {
                if (!anyDial) return;

                // A reef group that names dimensions with no reef would put a world's numbers in its
                // hash that the world does not have.
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"ReefCount is 0 and the reef group names cap r={capRadius} m, depth {capDepth} m, ") +
                    FormattableString.Invariant($"t={thickness} m, stem r={stem} m, fade {fade} m. ") +
                    "With no reef every dimension is 0, or the config's hash would carry a reef the " +
                    "world does not have. logbook/specs/reef-spec.md §1.",
                    nameof(config));
            }

            // A reef in a box: the streams and the mask are the tank's.
            if (worldRules && config.WorldShape != WorldShape.Tank)
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"WorldShape is {config.WorldShape} and the config asks for {count} reef(s). ") +
                    "A reef is the tank's: its fade is on the streams' potential and its rock is a mask on " +
                    "the tank's grid, and a box has neither. logbook/specs/reef-spec.md §3.",
                    nameof(config));
            }

            if (worldRules && config.FieldModel != MatterField.Grid)
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"FieldModel is {config.FieldModel} and the config asks for {count} reef(s). ") +
                    "The rock is dead cells in the grid, and only a grid has cells to kill. " +
                    "logbook/specs/reef-spec.md §2.",
                    nameof(config));
            }

            RefuseDimensions(capRadius, capDepth, thickness, stem, fade);

            // A cap that breaks the surface, unless it is the island.
            if (capDepth < 0.5f && stem > 0f)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A cap's top at {capDepth} m under the surface on a stem of radius {stem} m would break the ") +
                    "surface: a cap on a stem sits at least 0.5 m down. The floating island is the one reef at " +
                    "the surface, and it has no stem (ReefStemRadiusMetres 0). logbook/specs/reef-spec.md §3.",
                    nameof(config));
            }

            if (!(stem > 0f) && capDepth > 0f)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A reef with no stem has its cap at {capDepth} m under the surface, where nothing holds it: ") +
                    "the floating island's cap is at the surface (ReefCapDepthMetres 0), and a cap below it needs " +
                    "a stem. logbook/specs/reef-spec.md §1.",
                    nameof(config));
            }

            if (capDepth + thickness + RoomUnderCapMetres > config.WorldDepthMetres)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"A cap {thickness} m thick at {capDepth} m under the surface needs {RoomUnderCapMetres} m of ") +
                    FormattableString.Invariant($"water under it, and the tank is {config.WorldDepthMetres} m deep. ") +
                    "logbook/specs/reef-spec.md §1.",
                    nameof(config));
            }

            // The spacing: centres at least two caps and two fades apart, so no two reefs' fades
            // overlap and the fade is under half the spacing by construction. What can refuse is
            // the tank: the centres live inside R − r_c − 5 m, and a ring of that many at the
            // spacing has to fit there.
            double allowed = tankRadiusMetres - capRadius - GlassClearanceMetres;
            double spacing = Spacing(capRadius, fade);
            double ring = count < 2 ? 0d : spacing / (2d * Math.Sin(Math.PI / count));

            if (!(allowed > 0d) || ring > allowed)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"{count} reef(s) with caps {capRadius} m in radius and a fade of {fade} m need their ") +
                    FormattableString.Invariant(
                        $"centres {spacing:0.##} m apart (two caps and two fades, so no two fades meet and the ") +
                    FormattableString.Invariant(
                        $"fade is under half the spacing) and {capRadius + GlassClearanceMetres} m inside the glass. ") +
                    FormattableString.Invariant(
                        $"A tank {tankRadiusMetres:0.##} m in radius leaves {Math.Max(0d, allowed):0.##} m for the centres, ") +
                    FormattableString.Invariant(
                        $"and a ring of {count} at that spacing needs {ring:0.##} m. A fade longer than half the ") +
                    "spacing makes the water between the reefs still. Shorten the fade, narrow the caps or " +
                    "place fewer. logbook/specs/reef-spec.md §2 and §3.",
                    nameof(config));
            }
        }

        /// <summary>The least distance between two reefs' axes, m: <c>2·(r_c + fade)</c>.</summary>
        public static double Spacing(float capRadiusMetres, float fadeMetres) =>
            2d * ((double)capRadiusMetres + fadeMetres);

        // ------------------------------------------------------------------ the placement

        /// <summary>How many candidate layouts the placer tries before it refuses the seed.</summary>
        public const int PlacementRestarts = 2000;

        /// <summary>How many candidates one reef of one layout is offered.</summary>
        public const int PlacementAttempts = 2000;

        /// <summary>
        /// The world's reefs, placed by the seed: uniform over the disc the centres may occupy,
        /// at least <see cref="Spacing"/> apart, and only where the floor under the whole cap
        /// leaves <see cref="RoomUnderCapMetres"/> of water under it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Its own stream, drawn only here.</b> A world with no reef never calls this, so no
        /// stream any recorded world draws from moves. The draws are sequential: a layout is
        /// built one reef at a time, each offered <see cref="PlacementAttempts"/> candidates, and
        /// a layout that strands a reef is thrown away and a fresh one begun from where the
        /// stream stands, up to <see cref="PlacementRestarts"/> times. The seed decides the
        /// layout and nothing else does.
        /// </para>
        /// <para>
        /// <b>The floor is read under the whole cap</b>, at the axis and at eight points on the
        /// rim, against the cap's underside plus two metres: the spec's "a reef is refused where
        /// the floor under it is shallower than <c>d_cap + t + 2 m</c>", read so that the shaded
        /// room is never a slot, and so that the beach's shelf carries none.
        /// </para>
        /// </remarks>
        public static ReefGeometry Place(
            RunConfig config, float tankRadiusMetres, BedShape bed, ulong streamSeed, bool worldRules = true)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            int count = config.ReefCount;
            if (count <= 0) return null;

            Refuse(config, tankRadiusMetres, worldRules);

            float capRadius = config.ReefCapRadiusMetres;
            double allowed = tankRadiusMetres - capRadius - GlassClearanceMetres;
            double spacing = Spacing(capRadius, config.ReefFadeMetres);
            double needFloorBelow = config.ReefCapDepthMetres + config.ReefCapThicknessMetres + RoomUnderCapMetres;
            BedShape floor = bed != null && bed.HasRelief ? bed : null;

            var rng = new Rng(streamSeed);
            var xs = new double[count];
            var zs = new double[count];

            for (int restart = 0; restart < PlacementRestarts; restart++)
            {
                int placed = 0;

                for (int reef = 0; reef < count; reef++)
                {
                    bool found = false;

                    for (int attempt = 0; attempt < PlacementAttempts && !found; attempt++)
                    {
                        // Uniform over the disc: r = a·sqrt(u).
                        double r = allowed * Math.Sqrt(rng.NextFloat());
                        double theta = 2d * Math.PI * rng.NextFloat();
                        double x = tankRadiusMetres + r * Math.Cos(theta);
                        double z = tankRadiusMetres + r * Math.Sin(theta);

                        bool apart = true;
                        for (int other = 0; other < placed && apart; other++)
                        {
                            double dx = x - xs[other];
                            double dz = z - zs[other];
                            if (dx * dx + dz * dz < spacing * spacing) apart = false;
                        }

                        if (!apart) continue;
                        if (!DeepEnough(floor, config.WorldDepthMetres, x, z, capRadius, needFloorBelow)) continue;

                        xs[placed] = x;
                        zs[placed] = z;
                        placed++;
                        found = true;
                    }

                    if (!found) break;
                }

                if (placed == count)
                {
                    return new ReefGeometry(
                        capRadius, config.ReefCapDepthMetres, config.ReefCapThicknessMetres,
                        config.ReefStemRadiusMetres, config.ReefFadeMetres, xs, zs);
                }
            }

            throw new ArgumentException(
                FormattableString.Invariant(
                    $"The seed's reef stream found no layout of {count} reef(s) {spacing:0.##} m apart over water at least ") +
                FormattableString.Invariant(
                    $"{needFloorBelow:0.##} m deep under the whole cap in {PlacementRestarts} layouts of {PlacementAttempts} ") +
                "candidates a reef. The tank's deep water is too small for this count at this spacing on this " +
                "seed's floor. logbook/specs/reef-spec.md §1.",
                nameof(config));
        }

        private static bool DeepEnough(BedShape bed, float worldDepth, double x, double z, double capRadius, double needBelow)
        {
            if (bed == null) return worldDepth >= needBelow;

            if (-bed.FloorY(x, z) < needBelow) return false;

            for (int k = 0; k < 8; k++)
            {
                double a = k * (Math.PI / 4d);
                if (-bed.FloorY(x + capRadius * Math.Cos(a), z + capRadius * Math.Sin(a)) < needBelow) return false;
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

            /// <summary>The gradient: the outward normal where the distance is exact.</summary>
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
        public double SignedDistance(double x, double y, double z, out int nearest, out Distance at)
        {
            nearest = -1;
            at = default(Distance);
            double best = double.PositiveInfinity;

            for (int reef = 0; reef < _x.Length; reef++)
            {
                Distance d = OfReef(reef, x, y, z);
                if (d.S < best)
                {
                    best = d.S;
                    nearest = reef;
                    at = d;
                }
            }

            return best;
        }

        /// <summary>Whether a point is inside the rock.</summary>
        public bool Inside(double x, double y, double z) => SignedDistance(x, y, z) < 0d;

        /// <summary>
        /// One reef's distance: the cap and the stem joined by the smooth minimum — see the class
        /// remarks.
        /// </summary>
        public Distance OfReef(int reef, double x, double y, double z)
        {
            double dx = x - _x[reef];
            double dz = z - _z[reef];
            double rho = Math.Sqrt(dx * dx + dz * dz);

            double nx = rho > 0d ? dx / rho : 1d;
            double nz = rho > 0d ? dz / rho : 0d;

            Distance cap = Cap(rho, nx, nz, y);
            if (IsIsland) return cap;

            Distance stem = Stem(rho, nx, nz, y);
            return SmoothUnion(cap, stem, _fillet);
        }

        private Distance Cap(double rho, double nx, double nz, double y)
        {
            var d = default(Distance);
            double q = rho - _discRadius;
            double v = y - _capMidY;

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

        private Distance Stem(double rho, double nx, double nz, double y)
        {
            var d = default(Distance);
            double er = rho - StemRadiusMetres;
            double ey = y - _capMidY;

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
        /// <remarks>
        /// With <c>w_a = ∂s/∂a</c> (<c>1 − h²/2</c> when <c>a</c> is the smaller, <c>h²/2</c>
        /// otherwise) the gradient is <c>w_a∇a + (1 − w_a)∇b</c>, and the Hessian adds
        /// <c>−(h/k)(∇a − ∇b)(∇a − ∇b)ᵀ</c> to the weighted pair; both weights and that term are
        /// continuous at <c>a = b</c> and vanish into the plain minimum at <c>|a − b| = k</c>.
        /// </remarks>
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
        /// The streams' fade at a point, <c>g</c>: 0 inside the rock, 1 beyond
        /// <see cref="FadeMetres"/> from it, the quintic <c>6ξ⁵ − 15ξ⁴ + 10ξ³</c> of
        /// <c>ξ = s/fade</c> between, and the product over the reefs.
        /// </summary>
        public double FadeAt(double x, double y, double z)
        {
            double g = 1d;

            for (int reef = 0; reef < _x.Length; reef++)
            {
                double dx = x - _x[reef];
                double dz = z - _z[reef];
                if (dx * dx + dz * dz >= _reach * _reach) continue;

                double xi = OfReef(reef, x, y, z).S / FadeMetres;
                if (xi >= 1d) continue;
                if (xi <= 0d) return 0d;

                g *= xi * xi * xi * (10d + xi * (-15d + 6d * xi));
            }

            return g;
        }

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

            for (int reef = 0; reef < _x.Length; reef++)
            {
                double dx = x - _x[reef];
                double dz = z - _z[reef];
                if (dx * dx + dz * dz >= _reach * _reach) continue;

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
        /// under a cap (inside its disc and below its top), 1 everywhere else.
        /// </summary>
        /// <remarks>
        /// <see cref="LightField"/> multiplies a body's irradiance by this. The cap's top is lit as
        /// any water is; the stem casts nothing, being vertical.
        /// </remarks>
        public float LightTransmission(float x, float heightY, float z)
        {
            if (!(heightY < -(double)CapDepthMetres)) return 1f;

            double r2 = (double)CapRadiusMetres * CapRadiusMetres;

            for (int reef = 0; reef < _x.Length; reef++)
            {
                double dx = x - _x[reef];
                double dz = z - _z[reef];
                if (dx * dx + dz * dz <= r2) return CapTransmission;
            }

            return 1f;
        }

        /// <summary>
        /// What a cap lets through to the column under it: nothing. The cap is rock, and the
        /// owner's requirement for the reef was that it block the sun under it (2026-09-23:
        /// "make sure they block the sun under them"). The first cut read the canopy's own
        /// arithmetic at a cover of 1 (<see cref="LightField.Transmitted"/>, <c>e⁻¹</c>), which
        /// is what a leaf's silhouette lets past and not what a slab of rock does; that value
        /// is kept in the tests' message as the alternative, and is one constant to change.
        /// </summary>
        public static readonly float CapTransmission = 0f;

        /// <summary>The header's token: <c>reefs 3 cap r=4 m at 3 m t=2 m stem r=1 m fade 15 m</c>.</summary>
        public static string HeaderToken(RunConfig config)
        {
            if (config == null || config.ReefCount <= 0) return "no reef";

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return
                "reefs " + config.ReefCount.ToString(inv) +
                " cap r=" + config.ReefCapRadiusMetres.ToString("0.##", inv) +
                " m at " + config.ReefCapDepthMetres.ToString("0.##", inv) +
                " m t=" + config.ReefCapThicknessMetres.ToString("0.##", inv) +
                " m stem r=" + config.ReefStemRadiusMetres.ToString("0.##", inv) +
                " m fade " + config.ReefFadeMetres.ToString("0.##", inv) + " m";
        }
    }
}

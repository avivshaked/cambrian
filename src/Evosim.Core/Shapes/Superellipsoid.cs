using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// A tapered superellipsoid's geometry for one set of genes — D233's fourth shape, the
    /// first piece of its build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The solid.</b> On half-extents a, b, c with Y the long axis, as for the capsule, the
    /// untapered solid is where
    /// <c>F = (|x/a|^(2/ε2) + |z/c|^(2/ε2))^(ε2/ε1) + |y/b|^(2/ε1) ≤ 1</c>. The taper scales each
    /// slice across Y by <c>f(t) = 1 + k·t</c>, with <c>t = y/b</c> and <c>k = (τ − 1)/(τ + 1)</c>,
    /// so the middle slice keeps the half-extents and the ends stand in the ratio τ.
    /// </para>
    /// <para>
    /// <b>The frame is shifted to the centre of mass.</b> The taper moves the centre of mass
    /// toward the wide end by <see cref="CentreOffset"/>, and the solver takes a link's centre of
    /// mass to be its frame's origin, which is what lets it carry three numbers of inertia a link
    /// (<c>src/Evosim.Dynamics/Creature.cs</c>). So everything this class returns or reads — the
    /// surface point, the inside test, the panels — is in the frame whose origin is the centre
    /// of mass, the untapered solid's y less <see cref="CentreOffset"/>.
    /// </para>
    /// <para>
    /// <b>Every quantity is a scale-free factor times powers of the half-extents.</b> The factors
    /// depend on the three genes only and are computed once, here, from beta functions
    /// (<see cref="SpecialFunctions.Beta"/>). Growth rescales a body at every growth step, and a
    /// step then multiplies and calls no gamma function. With
    /// <c>S(ε) = ε·B(ε/2, ε/2)</c>, the area of a unit superellipse, <c>J(ε) = 2ε·B(3ε/2, ε/2 + 1)</c>,
    /// its <c>∫x² dA</c>, and <c>Wm</c>, <c>Um</c> the integrals of <c>t^m·w²</c> and
    /// <c>t^m·w⁴</c> along the profile <c>w(t) = (1 − |t|^(2/ε1))^(ε1/2)</c>:
    /// <c>V = S·abc·(W0 + k²W2)</c>, <c>y_c = 2k·b·W2/(W0 + k²W2)</c>,
    /// <c>∫x² dV = J·a³bc·(U0 + 6k²U2 + k⁴U4)</c> and <c>∫y² dV = S·ab³c·(W2 + k²W4)</c>.
    /// The derivation is in <c>git show 68c55ca5:proposals/propose-shape-family.md</c>; the shape
    /// tests check every one against a numerical integral and against the box and ball.
    /// </para>
    /// <para>
    /// Not a <see cref="PartShape"/> itself: that interface hands a shape its half-extents and a
    /// <see cref="PartForm"/>, and <see cref="SuperellipsoidShape"/> is the shape that builds one of
    /// these from the form and calls it.
    /// </para>
    /// </remarks>
    public sealed class Superellipsoid
    {
        /// <summary>The fewest sub-cells a side within a drag panel, for its area: D233's 4 by 4.</summary>
        public const int PanelSubdivisions = 4;

        /// <summary>The fewest sub-cells along a whole face edge, whatever the resolution.</summary>
        public const int MinimumSubdivisionsPerFace = 32;

        private readonly double _e1, _e2, _k;
        private readonly double _profilePower;    // 2/ε1
        private readonly double _sectionPower;    // 2/ε2
        private readonly double _sectionToProfile; // ε2/ε1

        private readonly double _volumeFactor;    // V / abc
        private readonly double _centreFactor;    // y_c / b
        private readonly double _acrossFactor;    // ∫x² dV / (V·a²), the same for z over c²
        private readonly double _alongFactor;     // ∫y² dV / (V·b²) about the centre of mass

        public PartForm Form { get; }

        /// <param name="form">
        /// The genes. Both exponents must lie in (0, 2], where the surface has no cusp and its
        /// normal is defined, and the taper must be positive. D233's ranges sit inside that.
        /// </param>
        public Superellipsoid(PartForm form)
        {
            if (!(form.Profile > 0f && form.Profile <= 2f) || !(form.Section > 0f && form.Section <= 2f))
            {
                throw new ArgumentOutOfRangeException(nameof(form),
                    $"A superellipsoid's exponents must lie in (0, 2]; got {form}.");
            }

            if (!(form.Taper > 0f) || float.IsInfinity(form.Taper))
            {
                throw new ArgumentOutOfRangeException(nameof(form),
                    $"A superellipsoid's taper must be positive and finite; got {form}.");
            }

            Form = form;
            _e1 = form.Profile;
            _e2 = form.Section;
            _k = (form.Taper - 1.0) / (form.Taper + 1.0);

            _profilePower = 2.0 / _e1;
            _sectionPower = 2.0 / _e2;
            _sectionToProfile = _e2 / _e1;

            double s = SectionArea(_e2);
            double j = SectionSecondMoment(_e2);
            double w0 = ProfileMoment(0, 1), w2 = ProfileMoment(2, 1), w4 = ProfileMoment(4, 1);
            double u0 = ProfileMoment(0, 2), u2 = ProfileMoment(2, 2), u4 = ProfileMoment(4, 2);

            double kk = _k * _k;
            double along = w0 + kk * w2;

            _volumeFactor = s * along;
            _centreFactor = 2.0 * _k * w2 / along;
            _acrossFactor = j * (u0 + 6.0 * kk * u2 + kk * kk * u4) / _volumeFactor;
            _alongFactor = s * (w2 + kk * w4) / _volumeFactor - _centreFactor * _centreFactor;
        }

        /// <summary>k = (τ − 1)/(τ + 1), the taper's slope along t.</summary>
        public double TaperSlope => _k;

        // ---------------------------------------------------------------- mass properties

        /// <summary>Volume in m³.</summary>
        public float Volume(Float3 h) => (float)(_volumeFactor * AbsA(h) * AbsB(h) * AbsC(h));

        /// <summary>
        /// How far the centre of mass sits from the untapered solid's centre along +Y, metres:
        /// positive toward the wide end. The shifted frame's origin.
        /// </summary>
        public float CentreOffset(Float3 h) => (float)(_centreFactor * AbsB(h));

        /// <summary>
        /// The inertia tensor's diagonal about the centre of mass, in the part's own axes, per
        /// kilogram (m²): multiply by the mass for kg·m². The shape is symmetric across its X and Z
        /// planes, so the tensor is diagonal in these axes, as the solver requires.
        /// </summary>
        public Double3 InertiaPerMass(Float3 h)
        {
            double a = AbsA(h), b = AbsB(h), c = AbsC(h);
            double xx = _acrossFactor * a * a;
            double yy = _alongFactor * b * b;
            double zz = _acrossFactor * c * c;
            return new Double3(yy + zz, xx + zz, xx + yy);
        }

        // ---------------------------------------------------------------- the surface

        /// <summary>
        /// The inside function in the shifted frame: at most 1 inside the solid, 1 on its surface.
        /// Infinite beyond the ends, where no slice exists.
        /// </summary>
        public double InsideFunction(Float3 p, Float3 h)
        {
            double a = AbsA(h), b = AbsB(h), c = AbsC(h);
            if (!(a > 0.0 && b > 0.0 && c > 0.0)) return double.PositiveInfinity;

            double t = (p.Y + _centreFactor * b) / b;
            if (Math.Abs(t) > 1.0) return double.PositiveInfinity;

            double f = 1.0 + _k * t;
            return UnitF(p.X / (a * f), t, p.Z / (c * f));
        }

        /// <summary>Whether a point in the shifted frame is inside the solid.</summary>
        public bool ContainsPoint(Float3 p, Float3 h) => InsideFunction(p, h) <= 1.0;

        /// <summary>
        /// Where a child attaches, given an anchor with components in [-1, 1], in the shifted frame.
        /// </summary>
        /// <remarks>
        /// The box's rule first: the anchor is clamped onto the cube, as <see cref="BoxShape.SurfacePoint"/>
        /// clamps it onto the box. That point is then pulled in along its ray onto the untapered
        /// surface, which is a closed formula because F scales along a ray as the ray's length to the
        /// power 2/ε1; then the taper and the shift apply. As both exponents go to 0 the pull goes to
        /// nothing, so a box-like part attaches children on its faces where a box would. An anchor
        /// inside the cube goes out along its ray to the surface, as a sphere's and a capsule's do;
        /// the box alone attaches such a child inside itself. A zero anchor has no ray, and attaches
        /// at the centre of the untapered solid, as a box's attaches at its centre.
        /// </remarks>
        public Float3 SurfacePoint(Float3 anchor, Float3 h)
        {
            var cube = new Double3(Clamp(anchor.X), Clamp(anchor.Y), Clamp(anchor.Z));
            return ToFloat(MapCube(cube, AbsA(h), AbsB(h), AbsC(h), out _));
        }

        /// <summary>
        /// The surface as outward panels in the shifted frame: <c>6·n²</c> of them, the box's and the
        /// sphere's count, so the drag sum costs what a box's does.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The box's grid of six faces, n by n, each grid point pushed onto the surface along its ray
        /// as <see cref="SurfacePoint"/> pushes an anchor. A panel's normal is the inside function's
        /// gradient at its centre, carried through the taper.
        /// </para>
        /// <para>
        /// <b>Its area is measured on a sub-grid and extrapolated</b>, since there is no closed
        /// formula for a superellipsoid's area. Flat sub-quads fall short of a curved surface by the
        /// square of their size, so the sum is taken on a sub-grid and on every other line of it, and
        /// <c>(4·fine − coarse)/3</c> cancels the leading error at no extra cost. The proposal's plain
        /// 4 by 4 sub-grid fell 1 to 2.5% short at the world's resolution of 2, where the box's, the
        /// sphere's and the capsule's panels are exact; this lands within about 0.03% of a fine
        /// integral of the surface at any resolution (the shape tests).
        /// </para>
        /// <para>
        /// <b>Measured once per part, not per step.</b> At resolution 2 this is about 7,000 points on
        /// the surface, under a millisecond. Growth scales a body uniformly, which scales every panel's
        /// centre by s and its area by s², so the genes' build should keep a part's panels at one size
        /// and scale them rather than call this at every growth step.
        /// </para>
        /// </remarks>
        public void AddPanels(Float3 h, int resolution, List<DragPanel> into)
        {
            double a = AbsA(h), b = AbsB(h), c = AbsC(h);
            if (!(a > 0.0 && b > 0.0 && c > 0.0)) return;

            int n = resolution < 1 ? 1 : resolution;
            int m = SubdivisionsFor(n);
            var corners = new Double3[(m + 1) * (m + 1)];

            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

                for (int side = 0; side < 2; side++)
                {
                    double sign = side == 0 ? 1.0 : -1.0;

                    for (int i = 0; i < n; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            double u0 = -1.0 + 2.0 * i / n;
                            double v0 = -1.0 + 2.0 * j / n;
                            double step = 2.0 / (n * m);

                            for (int p = 0; p <= m; p++)
                            {
                                for (int q = 0; q <= m; q++)
                                {
                                    corners[p * (m + 1) + q] = MapCube(
                                        CubePoint(axis, sign, u, u0 + p * step, v, v0 + q * step), a, b, c, out _);
                                }
                            }

                            double fine = QuadArea(corners, m, 1);
                            double coarse = QuadArea(corners, m, 2);
                            double area = (4.0 * fine - coarse) / 3.0;

                            Double3 centre = MapCube(
                                CubePoint(axis, sign, u, u0 + 1.0 / n, v, v0 + 1.0 / n), a, b, c, out Double3 unit);

                            into.Add(new DragPanel(ToFloat(centre), ToFloat(Normal(unit, a, b, c)), (float)area));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A part's exposure factor from its panels: its shadow on the horizontal over its
        /// orientation average, the quarter of its surface that <see cref="PhenotypePart.LitArea"/> is.
        /// </summary>
        /// <remarks>
        /// The shadow of a closed convex body is half the sum of each panel's area times the size of
        /// its normal's part along up, since every shadow point is covered once from above and once
        /// from below. The mean of that size over all poses is a half, so the factor's mean over all
        /// poses is exactly 1, for any panel set, as Cauchy's formula makes it for the other shapes.
        /// On a box's panels it is the box formula of <see cref="PhenotypePart.ExposureFactor(double, double, double)"/>.
        /// </remarks>
        public static float ExposureFactor(IReadOnlyList<DragPanel> panels, double upX, double upY, double upZ)
        {
            double total = 0.0, shadow = 0.0;
            for (int i = 0; i < panels.Count; i++)
            {
                DragPanel panel = panels[i];
                total += panel.Area;
                shadow += panel.Area * Math.Abs(panel.Normal.X * upX + panel.Normal.Y * upY + panel.Normal.Z * upZ);
            }

            return total > 0.0 ? (float)(2.0 * shadow / total) : 1f;
        }

        // ---------------------------------------------------------------- convexity

        /// <summary>
        /// The largest second difference of the tapered profile <c>h(t) = w(t)·(1 + k·t)</c> over
        /// <paramref name="samples"/> + 1 even points of [-1, 1]. At most 0, up to rounding, exactly
        /// when every solid with this profile and taper is convex.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why convexity reduces to this one function.</b> A slice across Y is the unit ball of
        /// <c>g(x, z) = (|x/a|^(2/ε2) + |z/c|^(2/ε2))^(ε2/2)</c>, scaled by <c>h(t)</c>, so the solid
        /// is the set where <c>g(x, z) − h(t) ≤ 0</c>. For ε2 ≤ 2, g is a norm and so convex; if h is
        /// concave, <c>g − h</c> is a convex function and its sublevel set is convex. Conversely the
        /// slice z = 0 is the plane region <c>|x| ≤ a·h(t)</c>, which is convex only if h is concave.
        /// So the solid is convex exactly when ε2 ≤ 2 and h is concave on [-1, 1], whatever the
        /// section exponent, and this is the check D233 asked for over the gene ranges.
        /// </para>
        /// <para>
        /// For ε1 = 1 the condition is <c>1 − |k|·√2 ≥ 0</c>, so |k| below about 0.71; D233's
        /// taper range keeps |k| at most 0.6. Below ε1 = 1 the shape tests confirm it numerically.
        /// </para>
        /// </remarks>
        public static double ProfileConcavity(double profile, double taper, int samples)
        {
            if (samples < 2) throw new ArgumentOutOfRangeException(nameof(samples), samples, "At least two intervals.");

            double k = (taper - 1.0) / (taper + 1.0);
            double power = 2.0 / profile;

            double Profile(double t)
            {
                double s = 1.0 - Math.Pow(Math.Abs(t), power);
                return (s > 0.0 ? Math.Pow(s, profile / 2.0) : 0.0) * (1.0 + k * t);
            }

            double worst = double.NegativeInfinity;
            double previous = Profile(-1.0), current = Profile(-1.0 + 2.0 / samples);

            for (int i = 2; i <= samples; i++)
            {
                double next = Profile(-1.0 + 2.0 * i / samples);
                worst = Math.Max(worst, previous - 2.0 * current + next);
                previous = current;
                current = next;
            }

            return worst;
        }

        // ---------------------------------------------------------------- the contact core

        /// <summary>
        /// The points of a contact core: the lattice of the cube's surface at step one half, every
        /// <c>(i, j, k)/2</c> with each of i, j, k in -2..2 and the largest of their absolute values 2.
        /// 5³ less the 3³ inside: 98.
        /// </summary>
        public const int ContactCorePoints = 98;

        /// <summary>How many directions the core's bulge and shortfall are measured over: a Fibonacci lattice on the sphere.</summary>
        public const int ContactBulgeDirections = 256;

        /// <summary>The bulge, as a fraction of the margin, above which the margin shrinks.</summary>
        public const double ContactBulgeLimit = 0.25;

        /// <summary>
        /// The share of the smaller of the smallest half-extent and the inradius that the margin takes (the
        /// coordinator's ruling of 2026-10-25). At the full value the inner body has no thickness along the
        /// smallest axis, a flat rectangle for a box-like form and a point for a ball, and the cyclic clip crawls
        /// toward a set of no volume. At 0.98 it keeps a thickness of 2% of the margin and the clip ends in a few
        /// passes; the corners are rounded by 0.98 of the box's margin, and the faces stand where they were.
        /// </summary>
        public const double ContactMarginBackoff = 0.98;

        /// <summary>
        /// Two corner points of a core closer than this share of the margin are one corner, and touch a surface
        /// once (the coordinator's ruling of 2026-10-25), as a box's core skips a vertex that repeats another's
        /// along an axis of no half-extent.
        /// </summary>
        /// <remarks>
        /// <b>It must exceed <c>2(1 − β)/β</c></b>, with β <see cref="ContactMarginBackoff"/>. A flat part's eroded
        /// core keeps a thickness of twice its smallest half-extent less the margin, which is that share of the
        /// margin, about 0.0408 at β = 0.98. A tolerance under it leaves a plate's top and bottom corners apart, and
        /// the plate touches on eight springs where a box touches on four. Change β and this together; a test
        /// fails if they part.
        /// </remarks>
        public const double ContactCornerMerge = 0.05;

        /// <summary>
        /// How much deeper than every pushing corner, as a share of the margin, a core's support point must stand to
        /// take the deepest corner's place in a surface touch (the coordinator's ruling of 2026-10-25).
        /// </summary>
        /// <remarks>
        /// <b>It must stay under <see cref="ContactCornerMerge"/></b>: a support point that close to a corner is that
        /// corner to the merge's reading, and one deeper by more is a point of its own that the touch must not miss.
        /// Above zero, so that a flat plate's support point, as deep as its lower corners, never switches. A test
        /// fails if the two part.
        /// </remarks>
        public const double ContactSupportSwitch = 0.005;

        /// <summary>What the margin is multiplied by each time the bulge is over its limit.</summary>
        public const double ContactMarginShrink = 0.8;

        /// <summary>
        /// The most times the margin shrinks. A form with a truly sharp corner has a bulge that is a fixed
        /// fraction of the margin however small it gets, so the rule is capped and the caller reads the
        /// bulge it ends with.
        /// </summary>
        public const int ContactMarginRebuilds = 40;

        /// <summary>
        /// Sub-cells along a face edge of the cube when the solid's support is sampled: the panel
        /// construction's sub-grid at resolution 4 (<see cref="PanelSubdivisions"/> panels of
        /// <see cref="SubdivisionsFor"/> cells), 33 by 33 points on each of six faces.
        /// </summary>
        public const int ContactSolidSteps = 32;

        // The lattice in a fixed order (i outermost, then j, then k), the index of each of the eight
        // corners in it, and the directions the support is measured along. All immutable once built.
        private static readonly Double3[] CoreLattice = BuildCoreLattice();
        private static readonly int[] CoreCorners = BuildCoreCorners();
        private static readonly Double3[] BulgeDirections = BuildBulgeDirections();

        // The directions the core is clipped against: the measuring directions first, in their order, so a
        // support array built over these serves the measurement too, then the 26 of the cube's lattice, so a
        // box-like form's faces, edges and corners are planes of their own.
        private static readonly Double3[] ClipDirections = BuildClipDirections();

        /// <summary>
        /// The passes of Dykstra's projection in the reference build (<see cref="ContactCore(Float3, bool, out double, out double, out double)"/>
        /// with its flag set), and the most in the fallback a point takes if the exact clip ever gives up.
        /// </summary>
        public const int ContactClipPasses = 200;

        /// <summary>The passes of plain cyclic projection that finish Dykstra's, in the same two places.</summary>
        public const int ContactPolishPasses = 200;

        /// <summary>
        /// The share of the form's size, its largest half-extent, within which a plane counts as met: by the exact
        /// clip's answer, by the fallback's polish, and as the still pass of the fallback's Dykstra.
        /// </summary>
        public const double ContactClipTolerance = 1e-9;

        /// <summary>
        /// The most steps of the exact clip for one point: each adds the plane the point stands farthest outside and
        /// solves for the nearest point of the planes held, so at most three are held at once. A point that needs
        /// more falls back to Dykstra's projection, and the build counts it.
        /// </summary>
        public const int ContactExactSteps = 64;

        /// <summary>
        /// The index, in the 98 points of a contact core, of one of the cube's eight corners: bit 0 of
        /// <paramref name="pattern"/> is a minus on x, bit 1 on y, bit 2 on z, as the solver's core vertices
        /// are numbered.
        /// </summary>
        public static int ContactCoreCorner(int pattern)
        {
            if ((uint)pattern > 7u)
            {
                throw new ArgumentOutOfRangeException(nameof(pattern), pattern, "A corner pattern is 0 to 7.");
            }

            return CoreCorners[pattern];
        }

        /// <summary>The cube point whose surface point is core point <paramref name="index"/>.</summary>
        public static Double3 ContactCoreCube(int index) => CoreLattice[index];

        /// <summary>
        /// A cube point's point on the surface, in the shifted frame, and the unit outward normal there.
        /// </summary>
        /// <remarks>
        /// The surface point is <see cref="SurfacePoint"/>'s map without its clamp and in double
        /// precision (<c>MapCube</c>), and the normal is the gradient of the tapered inside function
        /// (<c>Normal</c>); neither is changed. A cube point off the cube's surface is pushed out along its
        /// ray first, as the map always does; the origin has no ray and gets the centre and the +X axis.
        /// </remarks>
        public Double3 SurfacePointAndNormal(Double3 cube, Float3 h, out Double3 normal)
        {
            double a = AbsA(h), b = AbsB(h), c = AbsC(h);
            Double3 point = MapCube(cube, a, b, c, out Double3 unit);
            normal = Normal(unit, a, b, c);
            return point;
        }

        /// <summary>
        /// A radius about the shifted frame's origin that holds every point of the solid: the half-diagonal
        /// of the box that holds it, which is the slice's widest reach (the half-extent times
        /// <c>1 + |k|</c>) across, and the pole's farthest reach (<c>b·(1 + |y_c/b|)</c>) along Y.
        /// </summary>
        /// <remarks>
        /// Rigorous and not tight: the widest slice and the farthest pole are not at the same place. For
        /// taper 1 it is the half-diagonal of the half-extents, which is what the named shapes use. A
        /// taper of 4 has <c>k = 0.6</c> and so reaches 1.6 half-extents at its wide end, which the
        /// half-diagonal does not hold.
        /// </remarks>
        public double Reach(Float3 h)
        {
            double wide = 1.0 + Math.Abs(_k);
            double along = 1.0 + Math.Abs(_centreFactor);
            double x = AbsA(h) * wide, y = AbsB(h) * along, z = AbsC(h) * wide;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>
        /// The contact core of D233's solid: the convex hull of 98 points, which the solver takes through
        /// its support function and sweeps by <paramref name="margin"/>. Points are in the shifted frame, in
        /// the fixed order of the lattice (<see cref="ContactCoreCube"/>).
        /// </summary>
        /// <param name="halfExtents">The half-extents the core is built at.</param>
        /// <param name="margin">The ball swept round the core, metres; at most the smallest half-extent.</param>
        /// <param name="bulge">
        /// Over <see cref="ContactBulgeDirections"/> directions, the largest of (the core's support + margin)
        /// less the solid's support: how far the swept core stands outside the solid. Negative when it stands
        /// inside in every direction. Taken against <see cref="MeasureContactCore"/>'s sampling.
        /// </param>
        /// <param name="shortfall">
        /// The largest of the solid's support less (the core's support + margin): how far the solid stands
        /// outside the swept core, which the faceting between the 98 points causes.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>The core is the solid eroded by the margin</b>, the points whose ball of that radius stays inside
        /// it, as a box's core is the box shrunk by its margin on every side (the coordinator's ruling of
        /// 2026-10-25, which replaced moving each point inward along its normal: at a sharp corner the normal
        /// is the diagonal, and that ball stands outside the adjacent faces by a fixed fraction of the margin,
        /// about 0.42 at a cube's corner, however small the margin). Each lattice point is pushed onto the
        /// surface as <see cref="SurfacePoint"/> pushes an anchor, moved inward along its normal by the
        /// margin as a start, then clipped into the half-spaces <c>x·u ≤ h(u) − margin</c> over the
        /// measuring directions and the cube's 26 (<c>Clip</c>). The core swept by the margin is then the
        /// solid's opening: the solid with its sharp corners rounded by the margin, as a box's are, and no
        /// more than the sampling's error outside it.
        /// </para>
        /// <para>
        /// <b>The margin</b> is the smallest half-extent, as a box's is, unless the solid's inscribed ball is
        /// smaller, as a diamond's or a strong taper's is; past the inradius the eroded body is empty. It is
        /// <see cref="ContactMarginBackoff"/> of that, so the eroded body keeps a thickness. The
        /// rule that shrinks it by <see cref="ContactMarginShrink"/> while the bulge exceeds a quarter of it
        /// (<see cref="ContactBulgeLimit"/>) stays as a guard, and on the eroded core it should never act.
        /// </para>
        /// <para>
        /// A half-extent of 0 has no surface to move along: the core is 98 points at the origin and the
        /// margin 0.
        /// </para>
        /// </remarks>
        public Double3[] ContactCore(Float3 halfExtents, out double margin, out double bulge, out double shortfall) =>
            ContactCore(halfExtents, false, out margin, out bulge, out shortfall);

        /// <summary>
        /// <see cref="ContactCore(Float3, out double, out double, out double)"/>, with <paramref name="reference"/> true
        /// building each point by Dykstra's projection run to its 200 passes and the polish to its 200, with no exit:
        /// the build the exact clip is held against.
        /// </summary>
        public Double3[] ContactCore(
            Float3 halfExtents, bool reference, out double margin, out double bulge, out double shortfall) =>
            BuildContactCore(halfExtents, reference, null, out margin, out bulge, out shortfall);

        /// <summary>
        /// What the exact clip did for each point of the core at <paramref name="halfExtents"/>, for a reader: the steps
        /// it took, the planes it held at the end, the smallest angle between two held planes' normals and the passes
        /// Dykstra's projection would need at that angle, and how many points fell back.
        /// </summary>
        /// <remarks>
        /// Alternating projection between two planes closes on their meeting line by about cos² of the angle between
        /// their normals each pass, so planes whose normals are nearly parallel take many passes: about
        /// ln(tolerance) / ln(cos² θ). The 282 clipping directions stand about 0.21 rad apart, which at a tolerance of
        /// a billionth of the size is some 470 passes.
        /// </remarks>
        public string ContactClipReport(Float3 halfExtents)
        {
            var work = new ClipWork(CoreLattice.Length);
            BuildContactCore(halfExtents, false, work, out double margin, out _, out _);

            int[] held = new int[4];
            int most = 0, total = 0, predictedOver = 0;
            double smallest = double.PositiveInfinity;
            for (int i = 0; i < work.Steps.Length; i++)
            {
                held[Math.Min(3, work.Held[i])]++;
                most = Math.Max(most, work.Steps[i]);
                total += work.Steps[i];
                smallest = Math.Min(smallest, work.Angle[i]);
                if (work.Held[i] >= 2 && PredictedPasses(work.Angle[i]) > ContactClipPasses) predictedOver++;
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "margin {0:0.######} m; points holding 0/1/2/3 planes {1}/{2}/{3}/{4}; steps mean {5:0.00}, most {6}; " +
                "smallest angle between held normals {7:0.####} rad, where Dykstra would need about {8:0} passes; " +
                "points past Dykstra's 200 {9}; fallbacks {10}",
                margin, held[0], held[1], held[2], held[3], (double)total / work.Steps.Length, most,
                smallest, PredictedPasses(smallest), predictedOver, work.Fallbacks);
        }

        private static double PredictedPasses(double angle)
        {
            if (double.IsPositiveInfinity(angle)) return 0.0;
            double rate = Math.Cos(angle) * Math.Cos(angle);
            return rate >= 1.0 ? double.PositiveInfinity : Math.Log(ContactClipTolerance) / Math.Log(rate);
        }

        // A build's record of the exact clip's work, point by point, for ContactClipReport.
        private sealed class ClipWork
        {
            public readonly int[] Steps, Held;
            public readonly double[] Angle;
            public int Fallbacks;

            public ClipWork(int points)
            {
                Steps = new int[points];
                Held = new int[points];
                Angle = new double[points];
            }
        }

        private Double3[] BuildContactCore(
            Float3 halfExtents, bool reference, ClipWork work, out double margin, out double bulge, out double shortfall)
        {
            double a = AbsA(halfExtents), b = AbsB(halfExtents), c = AbsC(halfExtents);
            var points = new Double3[CoreLattice.Length];

            if (!(a > 0.0 && b > 0.0 && c > 0.0))
            {
                margin = 0.0;
                bulge = 0.0;
                shortfall = 0.0;
                return points;
            }

            var surface = new Double3[CoreLattice.Length];
            var normals = new Double3[CoreLattice.Length];
            for (int i = 0; i < CoreLattice.Length; i++)
            {
                surface[i] = MapCube(CoreLattice[i], a, b, c, out Double3 unit);
                normals[i] = Normal(unit, a, b, c);
            }

            double[] solid = SolidSupports(SolidSamples(a, b, c));

            // The margin is the box's, the smallest half-extent, unless the solid's inscribed ball is smaller,
            // as a diamond's or a strongly tapered form's is: past the inradius the inner body is empty. Backed
            // off by ContactMarginBackoff, so that the inner body keeps a thickness for the clip to end in.
            margin = ContactMarginBackoff * Math.Max(0.0, Math.Min(Math.Min(a, Math.Min(b, c)), Inradius(solid, b * (1.0 + Math.Abs(_centreFactor)))));
            double tolerance = ContactClipTolerance * Math.Max(a, Math.Max(b, c));
            var corrections = new Double3[ClipDirections.Length];
            for (int attempt = 0; ; attempt++)
            {
                for (int i = 0; i < points.Length; i++)
                {
                    Double3 start = surface[i] - normals[i] * margin;
                    if (reference)
                    {
                        points[i] = Clip(start, solid, margin, corrections, tolerance, true);
                        continue;
                    }

                    bool exact = Nearest(start, solid, margin, tolerance, out points[i], out int steps, out int held, out double angle);
                    if (!exact) points[i] = Clip(start, solid, margin, corrections, tolerance, false);

                    if (work != null)
                    {
                        work.Steps[i] = steps;
                        work.Held[i] = held;
                        work.Angle[i] = angle;
                        if (!exact) work.Fallbacks++;
                    }
                }

                MeasureAgainst(points, margin, solid, out bulge, out shortfall);
                if (bulge <= ContactBulgeLimit * margin || attempt >= ContactMarginRebuilds) return points;

                margin *= ContactMarginShrink;
            }
        }

        /// <summary>
        /// The bulge and the shortfall of any point set swept by any margin against this solid at
        /// <paramref name="halfExtents"/>, as <see cref="ContactCore(Float3, out double, out double, out double)"/> defines them.
        /// </summary>
        /// <remarks>
        /// <b>The sampling.</b> The solid's support along a direction is the largest dot product over its
        /// surface sampled at 33 by 33 points of each cube face (<see cref="ContactSolidSteps"/> cells an
        /// edge), the cube's lattice among them, pushed onto the surface by the same map as the core's
        /// points. A sample set under-reaches a curved surface by about the square of its spacing, so a
        /// smooth body's measured bulge is its sampling error, of the order of a thousandth of its size.
        /// The directions are a Fibonacci lattice of <see cref="ContactBulgeDirections"/> points.
        /// </remarks>
        public void MeasureContactCore(
            Float3 halfExtents, Double3[] points, double margin, out double bulge, out double shortfall)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Length == 0) throw new ArgumentException("A core needs at least one point.", nameof(points));

            double a = AbsA(halfExtents), b = AbsB(halfExtents), c = AbsC(halfExtents);
            if (!(a > 0.0 && b > 0.0 && c > 0.0))
            {
                bulge = 0.0;
                shortfall = 0.0;
                return;
            }

            MeasureAgainst(points, margin, SolidSupports(SolidSamples(a, b, c)), out bulge, out shortfall);
        }

        private static void MeasureAgainst(
            Double3[] points, double margin, double[] solid, out double bulge, out double shortfall)
        {
            bulge = double.NegativeInfinity;
            shortfall = double.NegativeInfinity;

            for (int d = 0; d < BulgeDirections.Length; d++)
            {
                Double3 direction = BulgeDirections[d];

                double core = double.NegativeInfinity;
                for (int i = 0; i < points.Length; i++)
                {
                    double along = Double3.Dot(points[i], direction);
                    if (along > core) core = along;
                }

                double over = core + margin - solid[d];
                if (over > bulge) bulge = over;
                if (-over > shortfall) shortfall = -over;
            }
        }

        /// <summary>The solid's surface at 33 by 33 points of each cube face, in the shifted frame.</summary>
        private Double3[] SolidSamples(double a, double b, double c)
        {
            int steps = ContactSolidSteps;
            var samples = new Double3[6 * (steps + 1) * (steps + 1)];
            int at = 0;

            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

                for (int side = 0; side < 2; side++)
                {
                    double sign = side == 0 ? 1.0 : -1.0;

                    for (int p = 0; p <= steps; p++)
                    {
                        for (int q = 0; q <= steps; q++)
                        {
                            double uValue = -1.0 + 2.0 * p / steps;
                            double vValue = -1.0 + 2.0 * q / steps;
                            samples[at++] = MapCube(CubePoint(axis, sign, u, uValue, v, vValue), a, b, c, out _);
                        }
                    }
                }
            }

            return samples;
        }

        /// <summary>
        /// The point of the inner body nearest <paramref name="point"/>, solved rather than approached: the half-spaces
        /// <c>x·u ≤ h(u) − margin</c> over <see cref="ClipDirections"/>, by an active set of at most three planes.
        /// False, with the last point reached, if <see cref="ContactExactSteps"/> run out or no set of the held planes
        /// solves, which the caller answers with Dykstra's projection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each step finds the plane the current point stands farthest outside, by more than
        /// <paramref name="tolerance"/>, and adds it to the planes held. The nearest point of the start in the
        /// intersection of those (at most four) is then found exactly: the smallest subset, in a fixed order, whose
        /// projection of the start onto its planes' meeting keeps every multiplier at least zero and meets every held
        /// plane. That subset is the new held set. The distance from the start grows at every step, so no held set
        /// comes back, and in three dimensions the answer is a face, an edge or a corner of the polytope.
        /// </para>
        /// <para>
        /// The coordinator's ruling of 2026-10-25 asked for a cold build of at most 50 ms. Dykstra's projection
        /// closes on a meeting of nearly parallel planes by cos² of their angle each pass, and the clipping planes of
        /// a smooth form meet at about 0.21 rad, so its points took hundreds of passes (<see cref="ContactClipReport"/>).
        /// The answer here is the same point, the limit Dykstra's approaches, found in a few steps.
        /// </para>
        /// </remarks>
        private static bool Nearest(
            Double3 start, double[] solid, double margin, double tolerance,
            out Double3 nearest, out int steps, out int heldCount, out double smallestAngle)
        {
            Span<int> held = stackalloc int[3];
            Span<int> candidates = stackalloc int[4];
            Span<int> subset = stackalloc int[3];
            Span<int> chosen = stackalloc int[3];
            int count = 0;
            Double3 x = start;

            for (steps = 0; steps < ContactExactSteps; steps++)
            {
                int worst = -1;
                double worstExcess = tolerance;
                for (int d = 0; d < ClipDirections.Length; d++)
                {
                    double excess = Double3.Dot(x, ClipDirections[d]) - (solid[d] - margin);
                    if (excess > worstExcess)
                    {
                        worstExcess = excess;
                        worst = d;
                    }
                }

                if (worst < 0)
                {
                    nearest = x;
                    heldCount = count;
                    smallestAngle = SmallestAngle(held, count);
                    return true;
                }

                int n = 0;
                for (int i = 0; i < count; i++) candidates[n++] = held[i];
                candidates[n++] = worst;

                bool solved = false;
                int chosenCount = 0;
                Double3 next = x;
                for (int size = 1; size <= Math.Min(3, n) && !solved; size++)
                {
                    for (int mask = 1; mask < (1 << n) && !solved; mask++)
                    {
                        if (Bits(mask) != size) continue;

                        int k = 0;
                        for (int i = 0; i < n; i++)
                        {
                            if ((mask & (1 << i)) != 0) subset[k++] = candidates[i];
                        }

                        if (!OntoPlanes(start, subset, k, solid, margin, tolerance, out Double3 y)) continue;

                        bool meets = true;
                        for (int i = 0; i < n && meets; i++)
                        {
                            int d = candidates[i];
                            if (Double3.Dot(y, ClipDirections[d]) - (solid[d] - margin) > tolerance) meets = false;
                        }

                        if (!meets) continue;

                        solved = true;
                        next = y;
                        chosenCount = k;
                        for (int i = 0; i < k; i++) chosen[i] = subset[i];
                    }
                }

                if (!solved) break;

                x = next;
                count = chosenCount;
                for (int i = 0; i < count; i++) held[i] = chosen[i];
            }

            nearest = x;
            heldCount = count;
            smallestAngle = SmallestAngle(held, count);
            return false;
        }

        /// <summary>
        /// The projection of <paramref name="start"/> onto the meeting of the <paramref name="count"/> planes
        /// <c>x·u = h(u) − margin</c> named by <paramref name="planes"/>: <c>x = start − Σ λ u</c> with the
        /// multipliers λ from their Gram system. False when the system is near singular or a multiplier is below
        /// zero by more than <paramref name="tolerance"/>, either of which says the set is not the answer.
        /// </summary>
        private static bool OntoPlanes(
            Double3 start, Span<int> planes, int count, double[] solid, double margin, double tolerance, out Double3 point)
        {
            point = start;
            Double3 u0 = ClipDirections[planes[0]];
            double r0 = Double3.Dot(start, u0) - (solid[planes[0]] - margin);

            if (count == 1)
            {
                if (r0 < -tolerance) return false;
                point = start - u0 * r0;
                return true;
            }

            Double3 u1 = ClipDirections[planes[1]];
            double r1 = Double3.Dot(start, u1) - (solid[planes[1]] - margin);
            double g01 = Double3.Dot(u0, u1);

            if (count == 2)
            {
                double det2 = 1.0 - g01 * g01;
                if (!(det2 > 1e-10)) return false;

                double l0 = (r0 - g01 * r1) / det2;
                double l1 = (r1 - g01 * r0) / det2;
                if (l0 < -tolerance || l1 < -tolerance) return false;

                point = start - u0 * l0 - u1 * l1;
                return true;
            }

            Double3 u2 = ClipDirections[planes[2]];
            double r2 = Double3.Dot(start, u2) - (solid[planes[2]] - margin);
            double g02 = Double3.Dot(u0, u2), g12 = Double3.Dot(u1, u2);

            // The Gram matrix [[1, g01, g02], [g01, 1, g12], [g02, g12, 1]], solved by Cramer's rule.
            double det = 1.0 + 2.0 * g01 * g02 * g12 - g01 * g01 - g02 * g02 - g12 * g12;
            if (!(Math.Abs(det) > 1e-10)) return false;

            double a0 = (r0 * (1.0 - g12 * g12) - g01 * (r1 - g12 * r2) + g02 * (r1 * g12 - r2)) / det;
            double a1 = ((r1 - g12 * r2) - r0 * (g01 - g12 * g02) + g02 * (g01 * r2 - r1 * g02)) / det;
            double a2 = ((r2 - r1 * g12) - g01 * (g01 * r2 - r1 * g02) + r0 * (g01 * g12 - g02)) / det;
            if (a0 < -tolerance || a1 < -tolerance || a2 < -tolerance) return false;

            point = start - u0 * a0 - u1 * a1 - u2 * a2;
            return true;
        }

        private static int Bits(int mask)
        {
            int bits = 0;
            for (; mask != 0; mask &= mask - 1) bits++;
            return bits;
        }

        /// <summary>The smallest angle between the normals of two held planes, radians; infinity with fewer than two.</summary>
        private static double SmallestAngle(Span<int> held, int count)
        {
            double smallest = double.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    double cosine = Math.Max(-1.0, Math.Min(1.0, Double3.Dot(ClipDirections[held[i]], ClipDirections[held[j]])));
                    smallest = Math.Min(smallest, Math.Acos(cosine));
                }
            }

            return smallest;
        }

        /// <summary>
        /// The point of the inner body nearest <paramref name="point"/> by Dykstra's projection: the reference build's
        /// clip, and the fallback of a point the exact clip gives up on. The half-spaces <c>x·u ≤ h(u) − margin</c>
        /// over <see cref="ClipDirections"/>, for at most <see cref="ContactClipPasses"/> passes, stopping after a pass
        /// that moves it by no more than <paramref name="tolerance"/> unless <paramref name="fullPasses"/>, then
        /// <see cref="Feasible"/>.
        /// </summary>
        /// <remarks>
        /// Plain cyclic projection ends somewhere in the intersection, not at its nearest point: a box's corner
        /// projected first onto an oblique plane came to rest on the shrunk box's face, 18 mm from its corner.
        /// Dykstra's carries each half-space's last correction into its next projection, which makes the limit
        /// the nearest point. A point moved along a smooth surface's normal by the margin is already on the inner
        /// body, and stays where it is; a sharp corner's goes to the inner body's corner, as a box's core's does.
        /// </remarks>
        private static Double3 Clip(
            Double3 point, double[] solid, double margin, Double3[] corrections, double tolerance, bool fullPasses)
        {
            Array.Clear(corrections, 0, corrections.Length);
            double still = tolerance * tolerance;

            for (int pass = 0; pass < ContactClipPasses; pass++)
            {
                double moved = 0.0;
                for (int d = 0; d < ClipDirections.Length; d++)
                {
                    Double3 direction = ClipDirections[d];
                    Double3 carried = point + corrections[d];
                    double excess = Double3.Dot(carried, direction) - (solid[d] - margin);
                    Double3 next = excess > 0.0 ? carried - direction * excess : carried;

                    corrections[d] = carried - next;
                    Double3 step = next - point;
                    double squared = Double3.Dot(step, step);
                    if (squared > moved) moved = squared;
                    point = next;
                }

                if (!fullPasses && moved <= still) break;
            }

            return Feasible(point, solid, margin, tolerance, fullPasses);
        }

        /// <summary>
        /// Plain cyclic projection onto the same half-spaces for at most <see cref="ContactPolishPasses"/> passes.
        /// Dykstra's iterates reach the nearest point only in the limit and may stand outside a half-space before it,
        /// so a clip on a thin inner body is finished here: the point moves only by Dykstra's remaining error, and
        /// ends inside. Unless <paramref name="fullPasses"/>, it stops once the point stands inside every plane to
        /// within <paramref name="tolerance"/>, read before each pass; how far a pass moved it is never the test.
        /// </summary>
        private static Double3 Feasible(Double3 point, double[] solid, double margin, double tolerance, bool fullPasses)
        {
            for (int pass = 0; pass < ContactPolishPasses; pass++)
            {
                if (!fullPasses && WorstExcess(point, solid, margin) <= tolerance) break;

                for (int d = 0; d < ClipDirections.Length; d++)
                {
                    Double3 direction = ClipDirections[d];
                    double excess = Double3.Dot(point, direction) - (solid[d] - margin);
                    if (excess > 0.0) point = point - direction * excess;
                }
            }

            return point;
        }

        /// <summary>The most a point stands outside any of the clip's half-spaces; at most 0 when it is inside all.</summary>
        private static double WorstExcess(Double3 point, double[] solid, double margin)
        {
            double worst = double.NegativeInfinity;
            for (int d = 0; d < ClipDirections.Length; d++)
            {
                double excess = Double3.Dot(point, ClipDirections[d]) - (solid[d] - margin);
                if (excess > worst) worst = excess;
            }

            return worst;
        }

        /// <summary>
        /// The radius of the largest ball inside the sampled solid. The solid is symmetric across its X and
        /// Z planes, so the ball's centre is on the Y axis, and its clearance there, the least of
        /// <c>h(u) − y·u_y</c>, is concave in y: a ternary search finds its top.
        /// </summary>
        private static double Inradius(double[] solid, double reachY)
        {
            double low = -reachY, high = reachY;
            for (int n = 0; n < 100; n++)
            {
                double left = low + (high - low) / 3.0, right = high - (high - low) / 3.0;
                if (Clearance(solid, left) < Clearance(solid, right)) low = left;
                else high = right;
            }

            return Clearance(solid, 0.5 * (low + high));
        }

        private static double Clearance(double[] solid, double y)
        {
            double clearance = double.PositiveInfinity;
            for (int d = 0; d < ClipDirections.Length; d++)
            {
                double room = solid[d] - y * ClipDirections[d].Y;
                if (room < clearance) clearance = room;
            }

            return clearance;
        }

        private static Double3[] BuildClipDirections()
        {
            var directions = new List<Double3>(BulgeDirections);
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        if (i == 0 && j == 0 && k == 0) continue;
                        var v = new Double3(i, j, k);
                        directions.Add(v * (1.0 / v.Length));
                    }
                }
            }

            return directions.ToArray();
        }

        /// <summary>
        /// The largest dot product of each clipping direction with a set of samples. The first
        /// <see cref="ContactBulgeDirections"/> are the measuring directions, which the measurement reads.
        /// </summary>
        private static double[] SolidSupports(Double3[] samples)
        {
            var support = new double[ClipDirections.Length];

            for (int d = 0; d < ClipDirections.Length; d++)
            {
                Double3 direction = ClipDirections[d];
                double best = double.NegativeInfinity;
                for (int i = 0; i < samples.Length; i++)
                {
                    double along = Double3.Dot(samples[i], direction);
                    if (along > best) best = along;
                }

                support[d] = best;
            }

            return support;
        }

        private static Double3[] BuildCoreLattice()
        {
            var lattice = new List<Double3>(ContactCorePoints);

            for (int i = -2; i <= 2; i++)
            {
                for (int j = -2; j <= 2; j++)
                {
                    for (int k = -2; k <= 2; k++)
                    {
                        int largest = Math.Max(Math.Abs(i), Math.Max(Math.Abs(j), Math.Abs(k)));
                        if (largest != 2) continue;

                        lattice.Add(new Double3(i * 0.5, j * 0.5, k * 0.5));
                    }
                }
            }

            return lattice.ToArray();
        }

        private static int[] BuildCoreCorners()
        {
            var corners = new int[8];

            for (int index = 0; index < CoreLattice.Length; index++)
            {
                Double3 p = CoreLattice[index];
                if (Math.Abs(p.X) != 1.0 || Math.Abs(p.Y) != 1.0 || Math.Abs(p.Z) != 1.0) continue;

                int pattern = (p.X < 0.0 ? 1 : 0) | (p.Y < 0.0 ? 2 : 0) | (p.Z < 0.0 ? 4 : 0);
                corners[pattern] = index;
            }

            return corners;
        }

        private static Double3[] BuildBulgeDirections()
        {
            var directions = new Double3[ContactBulgeDirections];
            double golden = Math.PI * (3.0 - Math.Sqrt(5.0));

            for (int i = 0; i < directions.Length; i++)
            {
                double y = 1.0 - 2.0 * (i + 0.5) / directions.Length;
                double ring = Math.Sqrt(Math.Max(0.0, 1.0 - y * y));
                double angle = golden * i;
                directions[i] = new Double3(ring * Math.Cos(angle), y, ring * Math.Sin(angle));
            }

            return directions;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>F of the untapered solid at a point already divided by its half-extents.</summary>
        private double UnitF(double x, double t, double z)
        {
            double r = Math.Pow(Math.Abs(x), _sectionPower) + Math.Pow(Math.Abs(z), _sectionPower);
            return Math.Pow(r, _sectionToProfile) + Math.Pow(Math.Abs(t), _profilePower);
        }

        /// <summary>
        /// A point of the cube [-1, 1]³ pushed along its ray onto the unit untapered surface, then
        /// scaled, tapered and shifted into the part's frame. <paramref name="unit"/> returns the
        /// point on the unit untapered surface, which the normal needs.
        /// </summary>
        private Double3 MapCube(Double3 cube, double a, double b, double c, out Double3 unit)
        {
            // Out to the cube's surface first, along the same ray, so that F is at least 1 and no
            // power of a small coordinate underflows to a zero F: at ε1 = 0.1, 0.5^20 is fine, but
            // a tiny anchor's 1e-20^20 is not.
            double extent = Math.Max(Math.Abs(cube.X), Math.Max(Math.Abs(cube.Y), Math.Abs(cube.Z)));
            if (!(extent > 0.0))
            {
                unit = new Double3(0.0, 0.0, 0.0);
                return new Double3(0.0, -_centreFactor * b, 0.0);
            }

            Double3 onCube = cube * (1.0 / extent);
            unit = onCube * Math.Pow(UnitF(onCube.X, onCube.Y, onCube.Z), -_e1 / 2.0);

            double taper = 1.0 + _k * unit.Y;
            return new Double3(a * unit.X * taper, b * (unit.Y - _centreFactor), c * unit.Z * taper);
        }

        /// <summary>
        /// The outward unit normal at the surface point whose untapered unit point is <paramref name="unit"/>:
        /// the gradient of the tapered inside function, less its common factor 2/ε1.
        /// </summary>
        /// <remarks>
        /// With <c>G(x, y, z) = F(x/f, y, z/f)</c> and <c>f = 1 + k·y/b</c>, the chain rule gives
        /// <c>G_x = F_X/f</c>, <c>G_z = F_Z/f</c> and <c>G_y = F_Y − (k/(b·f))·(X·F_X + Z·F_Z)</c>,
        /// with X and Z the untapered coordinates.
        /// </remarks>
        private Double3 Normal(Double3 unit, double a, double b, double c)
        {
            double ax = Math.Abs(unit.X), az = Math.Abs(unit.Z), ay = Math.Abs(unit.Y);
            double r = Math.Pow(ax, _sectionPower) + Math.Pow(az, _sectionPower);

            double gx = 0.0, gz = 0.0;
            if (r > 0.0)
            {
                double scale = Math.Pow(r, _sectionToProfile - 1.0);
                gx = scale * Math.Pow(ax, _sectionPower - 1.0) * Math.Sign(unit.X);
                gz = scale * Math.Pow(az, _sectionPower - 1.0) * Math.Sign(unit.Z);
            }

            double gy = Math.Pow(ay, _profilePower - 1.0) * Math.Sign(unit.Y);

            double f = 1.0 + _k * unit.Y;
            var gradient = new Double3(
                gx / (a * f),
                gy / b - _k / (b * f) * (unit.X * gx + unit.Z * gz),
                gz / (c * f));

            double length = gradient.Length;
            return length > 0.0 ? gradient * (1.0 / length) : new Double3(1.0, 0.0, 0.0);
        }

        /// <summary>
        /// Sub-cells a side within a panel at resolution n: even, so the coarse sum can take every
        /// other line, and at least 32 a face edge, where the extrapolated area has settled.
        /// </summary>
        private static int SubdivisionsFor(int n)
        {
            int half = (MinimumSubdivisionsPerFace / 2 + n - 1) / n;
            return 2 * Math.Max(half, PanelSubdivisions / 2);
        }

        /// <summary>The summed area of the sub-quads of a corner grid, taking every <paramref name="stride"/>th line.</summary>
        private static double QuadArea(Double3[] corners, int m, int stride)
        {
            int row = m + 1;
            double area = 0.0;

            for (int p = 0; p < m; p += stride)
            {
                for (int q = 0; q < m; q += stride)
                {
                    Double3 c00 = corners[p * row + q];
                    Double3 c01 = corners[p * row + q + stride];
                    Double3 c10 = corners[(p + stride) * row + q];
                    Double3 c11 = corners[(p + stride) * row + q + stride];
                    area += 0.5 * Double3.Cross(c11 - c00, c10 - c01).Length;
                }
            }

            return area;
        }

        private static Double3 CubePoint(int axis, double sign, int u, double uValue, int v, double vValue)
        {
            double x = 0.0, y = 0.0, z = 0.0;
            Set(ref x, ref y, ref z, axis, sign);
            Set(ref x, ref y, ref z, u, uValue);
            Set(ref x, ref y, ref z, v, vValue);
            return new Double3(x, y, z);
        }

        private static void Set(ref double x, ref double y, ref double z, int axis, double value)
        {
            if (axis == 0) x = value;
            else if (axis == 1) y = value;
            else z = value;
        }

        /// <summary>Area of the unit superellipse of exponent ε: π at 1, 4 as ε goes to 0, 2 at 2.</summary>
        private static double SectionArea(double e) => e * SpecialFunctions.Beta(e / 2.0, e / 2.0);

        /// <summary>∫x² dA over the unit superellipse: π/4 at 1, 4/3 as ε goes to 0.</summary>
        private static double SectionSecondMoment(double e) =>
            2.0 * e * SpecialFunctions.Beta(1.5 * e, e / 2.0 + 1.0);

        /// <summary>∫ t^m·w(t)^(2·power) dt over [-1, 1], for even m: Wm at power 1, Um at power 2.</summary>
        private double ProfileMoment(int m, int power) =>
            _e1 * SpecialFunctions.Beta((m + 1) * _e1 / 2.0, power * _e1 + 1.0);

        private static double AbsA(Float3 h) => Math.Abs((double)h.X);
        private static double AbsB(Float3 h) => Math.Abs((double)h.Y);
        private static double AbsC(Float3 h) => Math.Abs((double)h.Z);

        private static double Clamp(float v) => v < -1f ? -1.0 : v > 1f ? 1.0 : v;

        private static Float3 ToFloat(Double3 d) => new Float3((float)d.X, (float)d.Y, (float)d.Z);
    }
}

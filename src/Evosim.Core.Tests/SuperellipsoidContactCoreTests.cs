using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D233's contact core: the 98 moved points of the tapered superellipsoid, the margin that keeps the
    /// swept core from standing outside the solid, the bulge and shortfall that say how well it does, and
    /// the scaled copy that reads the reference core without building another.
    /// </summary>
    public class SuperellipsoidContactCoreTests
    {
        private readonly ITestOutputHelper _output;

        public SuperellipsoidContactCoreTests(ITestOutputHelper output) => _output = output;

        private static readonly Float3 Halves = new Float3(0.3f, 0.7f, 0.2f);

        private static readonly PartShapeRegistry FamilyRegistry = new PartShapeRegistry(
            new BoxShape(), new SphereShape(), new CapsuleShape(), new SuperellipsoidShape());

        private static readonly PartForm Egg = new PartForm(0.6f, 1f, 1.5f);

        /// <summary>The box limit: both exponents at 0.001, outside the shape's ranges, so built on the geometry directly.</summary>
        private static readonly PartForm BoxLimit = new PartForm(0.001f, 0.001f, 1f);

        private static double Smallest(Float3 h) =>
            Math.Min(Math.Abs((double)h.X), Math.Min(Math.Abs((double)h.Y), Math.Abs((double)h.Z)));

        private static Float3 ToFloat(Double3 d) => new Float3((float)d.X, (float)d.Y, (float)d.Z);

        /// <summary>The forms of <see cref="SuperellipsoidTests.Forms"/> and then the box limit.</summary>
        private static IEnumerable<PartForm> FormsAndTheBoxLimit()
        {
            foreach (object[] row in SuperellipsoidTests.Forms())
            {
                yield return new PartForm((float)row[0], (float)row[1], (float)row[2]);
            }

            yield return BoxLimit;
        }

        // ---------------------------------------------------------------- the lattice

        [Fact]
        public void TheLatticeIsNinetyEightPointsOnTheCubesSurfaceAndEachCornerHasItsOwnIndex()
        {
            Assert.Equal(98, Superellipsoid.ContactCorePoints);

            var seen = new HashSet<(double, double, double)>();
            for (int i = 0; i < Superellipsoid.ContactCorePoints; i++)
            {
                Double3 p = Superellipsoid.ContactCoreCube(i);
                double largest = Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z)));
                Assert.Equal(1.0, largest);
                Assert.True(seen.Add((p.X, p.Y, p.Z)), $"point {i} repeats another");

                foreach (double v in new[] { p.X, p.Y, p.Z })
                {
                    Assert.True(v == -1.0 || v == -0.5 || v == 0.0 || v == 0.5 || v == 1.0, $"point {i} has a component {v}");
                }
            }

            // The first point is (-1, -1, -1) and the order runs k fastest.
            Assert.Equal(new Double3(-1, -1, -1).ToString(), Superellipsoid.ContactCoreCube(0).ToString());
            Assert.Equal(new Double3(-1, -1, -0.5).ToString(), Superellipsoid.ContactCoreCube(1).ToString());

            var corners = new HashSet<int>();
            for (int pattern = 0; pattern < 8; pattern++)
            {
                int at = Superellipsoid.ContactCoreCorner(pattern);
                Assert.True(corners.Add(at), $"pattern {pattern} repeats a corner");

                Double3 p = Superellipsoid.ContactCoreCube(at);
                Assert.Equal((pattern & 1) != 0 ? -1.0 : 1.0, p.X);
                Assert.Equal((pattern & 2) != 0 ? -1.0 : 1.0, p.Y);
                Assert.Equal((pattern & 4) != 0 ? -1.0 : 1.0, p.Z);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => Superellipsoid.ContactCoreCorner(8));
        }

        [Fact]
        public void TheSurfacePointAndNormalAreTheMapsOwnAndTheNormalIsAUnitVectorPointingOut()
        {
            var shape = new Superellipsoid(Egg);

            for (int i = 0; i < Superellipsoid.ContactCorePoints; i++)
            {
                Double3 cube = Superellipsoid.ContactCoreCube(i);
                Double3 point = shape.SurfacePointAndNormal(cube, Halves, out Double3 normal);

                // The same point SurfacePoint gives for the same anchor, to float rounding.
                Float3 viaAnchor = shape.SurfacePoint(new Float3((float)cube.X, (float)cube.Y, (float)cube.Z), Halves);
                Fixtures.AssertClose(viaAnchor, ToFloat(point), 1e-6f);

                Assert.InRange(normal.Length, 1.0 - 1e-12, 1.0 + 1e-12);

                // On the surface, and stepping out along the normal leaves the solid. A pole, where the inside
                // function is infinite beyond the end, is left out of the first: rounding to float can put it there.
                bool pole = cube.X == 0.0 && cube.Z == 0.0;
                if (!pole) Assert.InRange(shape.InsideFunction(ToFloat(point), Halves), 1.0 - 1e-4, 1.0 + 1e-4);
                Double3 out1 = point + normal * (0.01 * Smallest(Halves));
                Assert.True(shape.InsideFunction(ToFloat(out1), Halves) > 1.0, $"point {i}: the normal points in");
            }
        }

        // ---------------------------------------------------------------- the margin, the bulge and the shortfall

        /// <summary>Whether the margin ran all the way down the rule's cap: the bulge never fell under a quarter of it.</summary>
        private static bool Capped(double margin, double smallest) =>
            margin <= smallest * Superellipsoid.ContactMarginBackoff *
                      Math.Pow(Superellipsoid.ContactMarginShrink, Superellipsoid.ContactMarginRebuilds) * (1.0 + 1e-9);

        [Fact]
        public void EveryFormsMarginIsUnderTheSmallestHalfExtentItsBulgeUnderAQuarterOfItWhereTheRuleEndsAndItsPointsInsideTheSolid()
        {
            double smallest = Smallest(Halves);
            string worstBulgeForm = null, worstShortfallForm = null;
            double worstBulge = double.NegativeInfinity, worstShortfall = double.NegativeInfinity;

            foreach (PartForm form in FormsAndTheBoxLimit())
            {
                var shape = new Superellipsoid(form);
                Double3[] core = shape.ContactCore(Halves, out double margin, out double bulge, out double shortfall);
                bool capped = Capped(margin, smallest);

                _output.WriteLine(
                    $"{form}: margin {margin:0.######} m ({margin / smallest:0.####} of the smallest half-extent){(capped ? " CAPPED" : "")}, " +
                    $"bulge {bulge:E3} m ({bulge / margin:0.####} of the margin), shortfall {shortfall:E3} m ({shortfall / smallest:0.####} of the smallest half-extent)");

                Assert.Equal(Superellipsoid.ContactCorePoints, core.Length);
                Assert.True(margin > 0.0, $"{form}: the margin ran down to {margin}");
                Assert.True(margin <= smallest + 1e-12, $"{form}: the margin {margin} is over the smallest half-extent {smallest}");

                // Where the rule ended by its own test, the bulge is under a quarter of the margin by construction.
                if (!capped)
                {
                    Assert.True(
                        bulge <= Superellipsoid.ContactBulgeLimit * margin,
                        $"{form}: the bulge {bulge} is over a quarter of the margin {margin}");
                }

                for (int i = 0; i < core.Length; i++)
                {
                    double inside = shape.InsideFunction(ToFloat(core[i]), Halves);
                    Assert.True(inside <= 1.0 + 1e-6, $"{form}: core point {i} is outside the solid (F = {inside})");
                }

                if (bulge > worstBulge) { worstBulge = bulge; worstBulgeForm = form.ToString(); }
                if (shortfall > worstShortfall) { worstShortfall = shortfall; worstShortfallForm = form.ToString(); }
            }

            _output.WriteLine($"worst bulge {worstBulge:E3} m at {worstBulgeForm}");
            _output.WriteLine($"worst shortfall {worstShortfall:E3} m at {worstShortfallForm}");
        }

        [Fact]
        public void TheBulgeGuardNeverActsOnTheErodedCore()
        {
            // The core is the solid eroded by the margin (the coordinator's ruling, which replaced moving each point
            // along its normal), so its swept form stands outside the solid only by the sampling's error, and the
            // guard that shrinks the margin while the bulge is over a quarter of it never acts: every margin is 0.98 of
            // the smaller of the smallest half-extent and the inradius, as built, for every form, sharp ridges included.
            double smallest = Smallest(Halves);
            var acted = new List<string>();
            double worst = double.NegativeInfinity;

            foreach (PartForm form in FormsAndTheBoxLimit())
            {
                new Superellipsoid(form).ContactCore(Halves, out double margin, out double bulge, out _);
                worst = Math.Max(worst, bulge / Math.Max(Halves.X, Math.Max(Halves.Y, Halves.Z)));
                if (Capped(margin, smallest) || bulge > Superellipsoid.ContactBulgeLimit * margin) acted.Add($"{form} (margin {margin}, bulge {bulge})");
            }

            _output.WriteLine($"worst bulge over all forms: {worst:E3} of the largest half-extent");
            Assert.True(acted.Count == 0, "the guard acted for: " + string.Join("; ", acted));
            Assert.True(worst <= 2e-3, $"the eroded core stands {worst:E3} of the size outside the solid, past the sampling's error");
        }

        [Fact]
        public void AnEvenBallsMarginIsTheBackedOffRadiusAndItsCoreTheSmallBallLeftAtItsCentre()
        {
            // The margin is 0.98 of the radius (ContactMarginBackoff), so the ball eroded by it is a ball of 0.02 of
            // the radius at the centre, and every core point is inside that. To the sampling's error: the inradius is
            // read off the solid's sampled supports, which reach a little short of a curved surface between the samples.
            var ball = new Superellipsoid(new PartForm(1f, 1f, 1f));
            var h = new Float3(0.4f, 0.4f, 0.4f);
            double r = 0.4f;

            Double3[] core = ball.ContactCore(h, out double margin, out double bulge, out double shortfall);

            _output.WriteLine($"ball: margin {margin:0.000000} m against {r}, bulge {bulge:E3} m, shortfall {shortfall:E3} m");
            double expected = Superellipsoid.ContactMarginBackoff * r;
            Assert.True(margin <= expected + 1e-12 && margin >= expected * (1.0 - 1e-3), $"the margin {margin} is not 0.98 of the radius {r}");
            double left = (1.0 - Superellipsoid.ContactMarginBackoff) * r;
            foreach (Double3 p in core) Assert.True(p.Length <= left + 1e-3 * r, $"a core point {p} is outside the ball of {left} left at the centre");
            Assert.True(bulge <= Superellipsoid.ContactBulgeLimit * margin);
        }

        [Fact]
        public void ABoxsOwnCoreMeasuresNoBulgeAndTheMeasureTakesAnyPointSetAndMargin()
        {
            // The box limit's solid, and the core a box has: the box less its smallest half-extent on every side, which
            // is the lattice scaled onto the shrunk box. Its swept core is the box with rounded edges, which lies inside
            // the box in every direction, so the measure must find no bulge worth the name.
            var shape = new Superellipsoid(BoxLimit);
            double m = Smallest(Halves);

            var points = new Double3[Superellipsoid.ContactCorePoints];
            for (int i = 0; i < points.Length; i++)
            {
                Double3 cube = Superellipsoid.ContactCoreCube(i);
                points[i] = new Double3(
                    cube.X * (Halves.X - m), cube.Y * (Halves.Y - m), cube.Z * (Halves.Z - m));
            }

            shape.MeasureContactCore(Halves, points, m, out double bulge, out double shortfall);
            _output.WriteLine($"a box's own core at margin {m}: bulge {bulge:E3} m, shortfall {shortfall:E3} m");

            Assert.True(bulge <= Superellipsoid.ContactBulgeLimit * m, $"a box's own core bulges by {bulge}");
            Assert.True(shortfall > 0.0, "the rounded corners of a swept box fall short of the box's corners");

            // A bigger margin on the same points reaches farther in every direction: the bulge and the shortfall move
            // by the change of margin, one up and one down.
            shape.MeasureContactCore(Halves, points, m + 0.01, out double more, out double less);
            Assert.Equal(bulge + 0.01, more, 9);
            Assert.Equal(shortfall - 0.01, less, 9);

            // A single point at the origin with the radius of the solid's largest half-extent: a ball, which stands
            // well outside a box of these half-extents (0.3 by 0.7 by 0.2) toward its shortest axis.
            shape.MeasureContactCore(Halves, new[] { new Double3(0, 0, 0) }, 0.7, out double ballBulge, out _);
            Assert.True(ballBulge > 0.4, $"a ball of 0.7 in a box of 0.3 by 0.7 by 0.2 should bulge by 0.4 or more; got {ballBulge}");
        }

        [Fact]
        public void TheBoxLimitsMarginIsTheBoxsBackedOffAndItsCoreIsTheBoxShrunkByIt()
        {
            // The coordinator's test of the ruling: a box-like form's eroded core is a box's own core, the box shrunk
            // by the margin on every side, to the sampling's error. The margin is 0.98 of the box's, its smallest
            // half-extent (ContactMarginBackoff), so the shrunk box keeps a thickness along its smallest axis. The face
            // directions are among the clipping planes, so each corner lands on the shrunk box's corner and every other
            // point inside it.
            var shape = new Superellipsoid(BoxLimit);
            Double3[] core = shape.ContactCore(Halves, out double margin, out double bulge, out double shortfall);
            double smallest = Smallest(Halves);
            double expected = Superellipsoid.ContactMarginBackoff * smallest;
            double tolerance = 1e-3 * smallest;

            double worstCorner = 0.0;
            for (int pattern = 0; pattern < 8; pattern++)
            {
                Double3 corner = core[Superellipsoid.ContactCoreCorner(pattern)];
                var shrunk = new Double3(
                    ((pattern & 1) != 0 ? -1.0 : 1.0) * (Halves.X - expected),
                    ((pattern & 2) != 0 ? -1.0 : 1.0) * (Halves.Y - expected),
                    ((pattern & 4) != 0 ? -1.0 : 1.0) * (Halves.Z - expected));
                worstCorner = Math.Max(worstCorner, (corner - shrunk).Length);
            }

            _output.WriteLine(
                $"box limit: margin {margin:0.######} m against {expected}, bulge {bulge:E3} m, shortfall {shortfall:E3} m; " +
                $"the corners stand up to {worstCorner:E3} m from the shrunk box's");

            Assert.True(Math.Abs(margin - expected) <= tolerance, $"the margin {margin} is not 0.98 of the smallest half-extent, {expected}");
            Assert.True(worstCorner <= 2e-3 * smallest, $"a corner is {worstCorner} from the shrunk box's");

            foreach (Double3 p in core)
            {
                Assert.True(
                    Math.Abs(p.X) <= Halves.X - expected + tolerance &&
                    Math.Abs(p.Y) <= Halves.Y - expected + tolerance &&
                    Math.Abs(p.Z) <= Halves.Z - expected + tolerance,
                    $"core point {p} is outside the shrunk box");
            }
        }

        [Fact]
        public void TheCornerMergeExceedsAFlatCoresThicknessAtTheBackoffSoAPlatesPairsMerge()
        {
            // The coordinator's ruling of 2026-10-25: the merge tolerance must exceed 2(1 - b)/b of the margin, b the
            // backoff, which is the thickness a flat part's eroded core keeps. Changing one constant without the other
            // fails here, by the formula and by a plate's own core.
            double beta = Superellipsoid.ContactMarginBackoff;
            double thickness = 2.0 * (1.0 - beta) / beta;
            Assert.True(
                Superellipsoid.ContactCornerMerge > thickness,
                $"the corner merge {Superellipsoid.ContactCornerMerge} of the margin is not above a flat core's thickness {thickness:0.####}");

            // A box-limit plate: each corner with a plus on y and its partner with a minus stand the core's thickness
            // apart, inside the merge; corners that differ on x or z stand far outside it.
            var plate = new Float3(0.2f, 0.02f, 0.15f);
            Double3[] core = new Superellipsoid(BoxLimit).ContactCore(plate, out double margin, out _, out _);
            double merge = Superellipsoid.ContactCornerMerge * margin;

            double widestPair = 0.0, nearestOther = double.PositiveInfinity;
            for (int pattern = 0; pattern < 8; pattern++)
            {
                Double3 corner = core[Superellipsoid.ContactCoreCorner(pattern)];
                for (int other = pattern + 1; other < 8; other++)
                {
                    double apart = (corner - core[Superellipsoid.ContactCoreCorner(other)]).Length;
                    if ((pattern ^ other) == 2) widestPair = Math.Max(widestPair, apart);
                    else nearestOther = Math.Min(nearestOther, apart);
                }
            }

            _output.WriteLine(
                $"plate: margin {margin:0.######} m, merge {merge:E3} m; top and bottom corners up to {widestPair:E3} m apart, " +
                $"other corners at least {nearestOther:E3} m apart");
            Assert.True(widestPair <= merge, $"a plate's top and bottom corners stand {widestPair} apart, past the merge {merge}");
            Assert.True(nearestOther > merge, $"two distinct corners stand {nearestOther} apart, inside the merge {merge}");
        }

        [Fact]
        public void TheSupportSwitchIsAboveZeroAndUnderTheCornerMerge()
        {
            // The coordinator's ruling of 2026-10-25: the depth by which a support point must stand below every
            // pushing corner to take the deepest one's place is under the merge, so that a point the switch passes over
            // is that corner to the merge's reading, and above zero, so that a flat plate's support point, as deep as
            // its lower corners, never switches. Changing one constant without the other fails here.
            Assert.True(Superellipsoid.ContactSupportSwitch > 0.0, "the switch must be above zero");
            Assert.True(
                Superellipsoid.ContactSupportSwitch < Superellipsoid.ContactCornerMerge,
                $"the switch {Superellipsoid.ContactSupportSwitch} is not under the corner merge {Superellipsoid.ContactCornerMerge}");
        }

        // ---------------------------------------------------------------- the clip's exit and the cache

        [Fact]
        public void EveryExactClipPointCarriesTheKktCertificateOfTheNearestPointAndNoFormFallsBack()
        {
            // The coordinator's ruling of 2026-10-26: proof that each point is the nearest point of the inner body to its
            // start, needing no reference build. Inside every clipping plane to the tolerance; every held plane through
            // the point to the tolerance; and the start less the point a combination of the held normals, by least
            // squares, with no weight below -1e-12 and a misfit of at most 1e-9 of the size. Those are KKT's conditions,
            // and for a convex set they make the point the nearest.
            double size = Math.Max(Halves.X, Math.Max(Halves.Y, Halves.Z));

            foreach (PartForm form in FormsAndTheBoxLimit())
            {
                Superellipsoid.ContactClipCertificate certificate = new Superellipsoid(form).ContactClipDetail(Halves);
                Assert.Equal(0, certificate.Fallbacks);

                double worstExcess = double.NegativeInfinity, worstMisfit = 0.0, leastWeight = double.PositiveInfinity, worstOff = 0.0;
                for (int i = 0; i < certificate.Points.Length; i++)
                {
                    Double3 point = certificate.Points[i];
                    Double3[] held = certificate.HeldNormals[i];

                    Assert.True(
                        certificate.WorstExcess[i] <= certificate.Tolerance,
                        $"{form}: point {i} stands {certificate.WorstExcess[i]} outside a clipping plane");
                    worstExcess = Math.Max(worstExcess, certificate.WorstExcess[i]);

                    for (int j = 0; j < held.Length; j++)
                    {
                        double off = Math.Abs(Double3.Dot(point, held[j]) - certificate.HeldOffsets[i][j]);
                        Assert.True(off <= certificate.Tolerance, $"{form}: point {i} is {off} off its held plane {j}");
                        worstOff = Math.Max(worstOff, off);
                    }

                    Double3 residual = certificate.Starts[i] - point;
                    double[] weights = LeastSquares(held, residual);
                    Double3 fit = residual;
                    for (int j = 0; j < held.Length; j++)
                    {
                        Assert.True(weights[j] >= -1e-12, $"{form}: point {i} has the weight {weights[j]} on held plane {j}");
                        leastWeight = Math.Min(leastWeight, weights[j]);
                        fit = fit - held[j] * weights[j];
                    }

                    Assert.True(fit.Length <= 1e-9 * size, $"{form}: point {i} misses the cone of its held normals by {fit.Length}");
                    worstMisfit = Math.Max(worstMisfit, fit.Length);
                }

                _output.WriteLine(
                    $"{form}: worst excess {worstExcess:E3} m, worst held-plane offset {worstOff:E3} m, " +
                    $"least weight {(double.IsPositiveInfinity(leastWeight) ? 0.0 : leastWeight):E3} m, worst misfit {worstMisfit:E3} m " +
                    $"(tolerance {certificate.Tolerance:E3} m)");
            }
        }

        /// <summary>The weights of <paramref name="normals"/> (none to three) that fit <paramref name="target"/> best, by the normal equations.</summary>
        private static double[] LeastSquares(Double3[] normals, Double3 target)
        {
            int k = normals.Length;
            var weights = new double[k];
            if (k == 0) return weights;

            var g = new double[k, k];
            var r = new double[k];
            for (int i = 0; i < k; i++)
            {
                r[i] = Double3.Dot(normals[i], target);
                for (int j = 0; j < k; j++) g[i, j] = Double3.Dot(normals[i], normals[j]);
            }

            if (k == 1)
            {
                weights[0] = r[0] / g[0, 0];
                return weights;
            }

            if (k == 2)
            {
                double det2 = g[0, 0] * g[1, 1] - g[0, 1] * g[1, 0];
                weights[0] = (r[0] * g[1, 1] - g[0, 1] * r[1]) / det2;
                weights[1] = (g[0, 0] * r[1] - r[0] * g[1, 0]) / det2;
                return weights;
            }

            double det = Det3(g, -1, null);
            for (int c = 0; c < 3; c++) weights[c] = Det3(g, c, r) / det;
            return weights;
        }

        // The determinant of a 3 by 3 matrix, with column `column` replaced by `values` when it is 0 to 2.
        private static double Det3(double[,] m, int column, double[] values)
        {
            double At(int i, int j) => j == column ? values[i] : m[i, j];
            return At(0, 0) * (At(1, 1) * At(2, 2) - At(1, 2) * At(2, 1))
                 - At(0, 1) * (At(1, 0) * At(2, 2) - At(1, 2) * At(2, 0))
                 + At(0, 2) * (At(1, 0) * At(2, 1) - At(1, 1) * At(2, 0));
        }

        [Fact]
        [Trait("Category", "Slow")]
        public void TheExactClipIsWithinATenMillionthOfTheSizeOfADykstraBuildOfFiveThousandPasses()
        {
            // The coordinator's rulings of 2026-10-26: the reference is Dykstra's projection run to 5,000 passes and the
            // polish to 5,000, since at 200 it stood 17 mm from the certified point on the ellipsoid and at 2,000 still
            // 57 micrometres; the bound of 1e-7 of the size is kept. Slow, about 90 s; the certificate is the proof in the
            // default suite.
            // Every form's gap is written before the bound is asserted, so a failure names them all; the margin is the
            // same; the bulge stays under the limit.
            double size = Math.Max(Halves.X, Math.Max(Halves.Y, Halves.Z));
            var over = new List<string>();

            foreach (PartForm form in FormsAndTheBoxLimit())
            {
                var shape = new Superellipsoid(form);
                Double3[] exact = shape.ContactCore(Halves, 0, out double exactMargin, out double exactBulge, out _);
                Double3[] reference = shape.ContactCore(Halves, 5000, out double referenceMargin, out double referenceBulge, out _);

                Assert.Equal(referenceMargin, exactMargin);

                double apart = 0.0;
                for (int i = 0; i < exact.Length; i++) apart = Math.Max(apart, (exact[i] - reference[i]).Length);

                _output.WriteLine($"{form}: exact against 5,000 passes {apart:E3} m; bulge {exactBulge:E3} m against {referenceBulge:E3} m");
                if (apart > 1e-7 * size) over.Add($"{form} by {apart:E3} m");

                Assert.True(exactBulge <= Superellipsoid.ContactBulgeLimit * exactMargin, $"{form}: the exact build's bulge {exactBulge}");
                Assert.True(exactBulge <= 2e-3 * size, $"{form}: the exact build's bulge {exactBulge} is past the sampling's error");
            }

            Assert.True(over.Count == 0, "the exact build stands past 1e-7 of the size from the reference for: " + string.Join("; ", over));
        }

        private static void SameBuild(ContactCoreBuild expected, ContactCoreBuild actual, string what)
        {
            Assert.Equal(expected.Points.Length, actual.Points.Length);
            for (int i = 0; i < expected.Points.Length; i++)
            {
                Assert.True(
                    BitConverter.DoubleToInt64Bits(expected.Points[i]) == BitConverter.DoubleToInt64Bits(actual.Points[i]),
                    $"{what}: point value {i}");
            }

            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Margin), BitConverter.DoubleToInt64Bits(actual.Margin));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Bulge), BitConverter.DoubleToInt64Bits(actual.Bulge));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Shortfall), BitConverter.DoubleToInt64Bits(actual.Shortfall));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Reach), BitConverter.DoubleToInt64Bits(actual.Reach));
        }

        [Fact]
        public void ACacheHitReturnsTheColdBuildsBits()
        {
            var cache = new ContactCoreCache(8);
            var shape = new Superellipsoid(Egg);

            ContactCoreBuild first = cache.For(shape, Halves);
            ContactCoreBuild second = cache.For(new Superellipsoid(Egg), Halves);

            Assert.Equal(1L, cache.Builds);
            Assert.Equal(1L, cache.Hits);
            Assert.Same(first, second);
            SameBuild(new ContactCoreBuild(new Superellipsoid(Egg), Halves), second, "the hit");
        }

        [Fact]
        public void EvictionNeverChangesABitAndTheKeyIsTheFloatsBitPatterns()
        {
            // A cache of two: the third form evicts the first, and the first asked again is built again, to the bit.
            var cache = new ContactCoreCache(2);
            var forms = new[] { Egg, new PartForm(0.5f, 1f, 1.6f), new PartForm(0.3f, 1.7f, 3f) };

            ContactCoreBuild before = cache.For(new Superellipsoid(forms[0]), Halves);
            cache.For(new Superellipsoid(forms[1]), Halves);
            cache.For(new Superellipsoid(forms[2]), Halves);
            Assert.Equal(2, cache.Count);

            ContactCoreBuild after = cache.For(new Superellipsoid(forms[0]), Halves);
            Assert.Equal(4L, cache.Builds);
            Assert.Equal(0L, cache.Hits);
            Assert.NotSame(before, after);
            SameBuild(before, after, "the rebuild after eviction");

            // The least recently used goes first: forms[0] was asked again after forms[2], so a fourth form evicts
            // forms[2] and forms[0] is still held.
            cache.For(new Superellipsoid(new PartForm(0.7f, 0.4f, 0.25f)), Halves);
            long built = cache.Builds;
            cache.For(new Superellipsoid(forms[0]), Halves);
            Assert.Equal(built, cache.Builds);

            // 0 and -0 are two keys: equal values, different bits.
            var keys = new ContactCoreCache(8);
            var ball = new Superellipsoid(new PartForm(1f, 1f, 1f));
            keys.For(ball, new Float3(0.2f, 0f, 0.2f));
            keys.For(ball, new Float3(0.2f, -0f, 0.2f));
            Assert.Equal(2L, keys.Builds);
        }

        [Fact]
        [Trait("Category", "Slow")]
        public void OneColdBuildsTimeWithTheExactClipAndWithEveryPass()
        {
            // A reading, not a check: one isolated build of a form not built before in this process, timed alone on a
            // quiet lane, after a different form has warmed the code. The least of three tries of each is reported.
            new Superellipsoid(new PartForm(0.9f, 1.1f, 1.2f)).ContactCore(Halves, 0, out _, out _, out _);
            new Superellipsoid(new PartForm(0.9f, 1.1f, 1.2f)).ContactCore(Halves, 200, out _, out _, out _);

            foreach (PartForm form in new[] { Egg, new PartForm(0.1f, 0.1f, 1f), new PartForm(1f, 2f, 4f) })
            {
                double exit = double.PositiveInfinity, full = double.PositiveInfinity;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    new Superellipsoid(form).ContactCore(Halves, 0, out _, out _, out _);
                    exit = Math.Min(exit, clock.Elapsed.TotalMilliseconds);

                    clock.Restart();
                    new Superellipsoid(form).ContactCore(Halves, 200, out _, out _, out _);
                    full = Math.Min(full, clock.Elapsed.TotalMilliseconds);
                }

                _output.WriteLine($"{form}: one cold build {exit:0.0} ms with the exact clip, {full:0.0} ms with every pass; {new Superellipsoid(form).ContactClipReport(Halves)}");
            }
        }

        [Fact]
        public void ASolidWithAHalfExtentOfZeroHasNoCoreToMove()
        {
            var shape = new Superellipsoid(Egg);
            Double3[] core = shape.ContactCore(new Float3(0.2f, 0f, 0.2f), out double margin, out double bulge, out double shortfall);

            Assert.Equal(Superellipsoid.ContactCorePoints, core.Length);
            Assert.Equal(0.0, margin);
            Assert.Equal(0.0, bulge);
            Assert.Equal(0.0, shortfall);
            foreach (Double3 p in core) Assert.Equal(0.0, p.Length);
        }

        [Fact]
        public void TheReachHoldsEveryPointOfTheSolidAndIsTheHalfDiagonalAtTaperOne()
        {
            foreach (PartForm form in FormsAndTheBoxLimit())
            {
                var shape = new Superellipsoid(form);
                double reach = shape.Reach(Halves);

                for (int i = 0; i < Superellipsoid.ContactCorePoints; i++)
                {
                    Double3 p = shape.SurfacePointAndNormal(Superellipsoid.ContactCoreCube(i), Halves, out _);
                    Assert.True(p.Length <= reach + 1e-12, $"{form}: surface point {i} is {p.Length} from the origin, over the reach {reach}");
                }
            }

            double hx = Halves.X, hy = Halves.Y, hz = Halves.Z;
            double diagonal = Math.Sqrt(hx * hx + hy * hy + hz * hz);
            Assert.Equal(diagonal, new Superellipsoid(new PartForm(0.5f, 1f, 1f)).Reach(Halves), 6);

            // A taper of 4 reaches past the half-diagonal.
            Assert.True(new Superellipsoid(new PartForm(1f, 1f, 4f)).Reach(Halves) > diagonal * 1.2);
        }

        // ---------------------------------------------------------------- a scaled copy reads the reference core

        private static Genome FamilyGenome(PartForm form)
        {
            var genome = new Genome { RootIndex = 0 };

            MorphNode root = Fixtures.Box(0.3f);
            root.ShapeId = ShapeIds.Superellipsoid;
            root.Form = form;
            root.Dimensions = new Float3(0.2f, 0.45f, 0.15f);
            genome.Nodes.Add(root);

            return genome;
        }

        [Fact]
        public void AScaledFamilyPartsCoreAndMarginAreTheReferencesTimesTheScaleAndNothingIsRebuilt()
        {
            Phenotype adult = Developer.Develop(FamilyGenome(Egg), shapes: FamilyRegistry);
            FamilyGeometry shared = adult.Parts[0].Family;

            Assert.Equal(0, shared.ContactCoreBuilds);

            double[] reference = shared.ReferenceCorePoints;
            double referenceMargin = shared.ReferenceCoreMargin;
            double referenceBulge = shared.ReferenceCoreBulge;
            double referenceShortfall = shared.ReferenceCoreShortfall;
            double referenceReach = shared.ReferenceReach;

            Assert.Equal(1, shared.ContactCoreBuilds);
            Assert.Equal(3 * Superellipsoid.ContactCorePoints, reference.Length);
            Assert.Same(reference, shared.ReferenceCorePoints);

            // The reference is the geometry's core at the reference half-extents.
            var geometry = new Superellipsoid(Egg);
            Double3[] fresh = geometry.ContactCore(adult.Parts[0].HalfExtents, out double freshMargin, out double freshBulge, out double freshShortfall);
            Assert.Equal(freshMargin, referenceMargin, 12);
            Assert.Equal(freshBulge, referenceBulge, 12);
            Assert.Equal(freshShortfall, referenceShortfall, 12);
            for (int i = 0; i < fresh.Length; i++)
            {
                Assert.Equal(fresh[i].X, reference[3 * i], 12);
                Assert.Equal(fresh[i].Y, reference[3 * i + 1], 12);
                Assert.Equal(fresh[i].Z, reference[3 * i + 2], 12);
            }

            foreach (float s in new[] { 0.2f, 0.37f, 0.5f, 0.81f, 1.3f })
            {
                Phenotype grown = adult.Scaled(s, FamilyRegistry);
                PhenotypePart part = grown.Parts[0];
                Assert.Same(shared, part.Family);

                // The reference times the scale is the core a fresh build at the new size gives, to the float
                // rounding of the new half-extents.
                double scale = part.FamilyScale;
                Double3[] rebuilt = geometry.ContactCore(part.HalfExtents, out double rebuiltMargin, out double rebuiltBulge, out double rebuiltShortfall);
                double size = Math.Max(part.HalfExtents.X, Math.Max(part.HalfExtents.Y, part.HalfExtents.Z));

                Assert.True(Math.Abs(rebuiltMargin - referenceMargin * scale) <= 1e-6 * size, $"scale {s}: margin {rebuiltMargin} against {referenceMargin * scale}");
                Assert.True(Math.Abs(rebuiltBulge - referenceBulge * scale) <= 1e-6 * size, $"scale {s}: bulge");
                Assert.True(Math.Abs(rebuiltShortfall - referenceShortfall * scale) <= 1e-6 * size, $"scale {s}: shortfall");
                for (int i = 0; i < rebuilt.Length; i++)
                {
                    Assert.True(Math.Abs(rebuilt[i].X - reference[3 * i] * scale) <= 1e-6 * size, $"scale {s}: point {i} x");
                    Assert.True(Math.Abs(rebuilt[i].Y - reference[3 * i + 1] * scale) <= 1e-6 * size, $"scale {s}: point {i} y");
                    Assert.True(Math.Abs(rebuilt[i].Z - reference[3 * i + 2] * scale) <= 1e-6 * size, $"scale {s}: point {i} z");
                }

                // The reach scales the same way, and holds the scaled solid.
                Assert.True(
                    referenceReach * scale >= geometry.Reach(part.HalfExtents) - 1e-6 * size,
                    $"scale {s}: the reference reach times the scale, {referenceReach * scale}, does not hold the solid's {geometry.Reach(part.HalfExtents)}");

                // Asking the shared geometry again at any scale builds nothing.
                _ = part.Family.ReferenceCorePoints;
                _ = part.Family.ReferenceCoreMargin;
                Assert.Equal(1, shared.ContactCoreBuilds);
            }

            // The panels' own count is not touched by the core.
            Assert.Equal(1, shared.PanelBuilds);
        }
    }
}

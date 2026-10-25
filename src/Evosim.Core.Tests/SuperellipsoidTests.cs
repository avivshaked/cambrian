using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D233's tapered superellipsoid, the first piece: its geometry checked against numerical
    /// integrals that share none of its formulas, and against the ball and the box it runs to.
    /// </summary>
    public class SuperellipsoidTests
    {
        private readonly ITestOutputHelper _output;

        public SuperellipsoidTests(ITestOutputHelper output) => _output = output;

        private static readonly Float3 Halves = new Float3(0.3f, 0.7f, 0.2f);

        /// <summary>Forms across D233's ranges, ends and corners included.</summary>
        public static IEnumerable<object[]> Forms()
        {
            yield return new object[] { 1f, 1f, 1f };
            yield return new object[] { 0.1f, 0.1f, 1f };
            yield return new object[] { 0.3f, 1.7f, 3f };
            yield return new object[] { 0.7f, 0.4f, 0.25f };
            yield return new object[] { 1f, 2f, 4f };
            yield return new object[] { 0.1f, 2f, 0.25f };
            yield return new object[] { 0.5f, 1f, 1.6f };
        }

        // ---------------------------------------------------------------- gamma and beta

        [Fact]
        public void GammaAndBetaMatchKnownValues()
        {
            AssertRelative(Math.Sqrt(Math.PI), SpecialFunctions.Gamma(0.5), 1e-13);
            AssertRelative(1.0, SpecialFunctions.Gamma(1.0), 1e-13);
            AssertRelative(24.0, SpecialFunctions.Gamma(5.0), 1e-13);
            AssertRelative(1.329340388179137, SpecialFunctions.Gamma(2.5), 1e-13);
            AssertRelative(9.513507698668732, SpecialFunctions.Gamma(0.1), 1e-13);
            AssertRelative(2.678938534707748, SpecialFunctions.Gamma(1.0 / 3.0), 1e-13);
            AssertRelative(999.4237724845955, SpecialFunctions.Gamma(0.001), 1e-12);

            AssertRelative(Math.Log(3628800.0), SpecialFunctions.LogGamma(11.0), 1e-13);
            AssertRelative(Math.Log(9.513507698668732), SpecialFunctions.LogGamma(0.1), 1e-13);

            AssertRelative(1.0 / 12.0, SpecialFunctions.Beta(2.0, 3.0), 1e-13);
            AssertRelative(Math.PI, SpecialFunctions.Beta(0.5, 0.5), 1e-13);
        }

        // ---------------------------------------------------------------- mass properties

        [Theory]
        [MemberData(nameof(Forms))]
        public void MassPropertiesMatchANumericalIntegral(float profile, float section, float taper)
        {
            var form = new PartForm(profile, section, taper);
            var shape = new Superellipsoid(form);
            Moments m = Integrate(form, Halves.X, Halves.Y, Halves.Z, 1200);

            double centre = m.Y / m.Volume;
            double along = m.YY / m.Volume - centre * centre;
            double acrossX = m.XX / m.Volume, acrossZ = m.ZZ / m.Volume;
            Double3 inertia = shape.InertiaPerMass(Halves);

            _output.WriteLine($"{form}: V {shape.Volume(Halves):0.000000} vs {m.Volume:0.000000}, " +
                              $"y_c {shape.CentreOffset(Halves):0.000000} vs {centre:0.000000}, " +
                              $"I {inertia} vs {along + acrossZ} {acrossX + acrossZ} {acrossX + along}");

            AssertRelative(m.Volume, shape.Volume(Halves), 1e-4);
            Assert.True(Math.Abs(centre - shape.CentreOffset(Halves)) <= 1e-5 * Halves.Y,
                $"centre offset {shape.CentreOffset(Halves)} against {centre}");
            AssertRelative(along + acrossZ, inertia.X, 1e-4);
            AssertRelative(acrossX + acrossZ, inertia.Y, 1e-4);
            AssertRelative(acrossX + along, inertia.Z, 1e-4);
        }

        [Fact]
        public void TheEllipsoidIsBothExponentsAtOne()
        {
            var shape = new Superellipsoid(new PartForm(1f, 1f, 1f));
            double a = Halves.X, b = Halves.Y, c = Halves.Z;

            AssertRelative(4.0 / 3.0 * Math.PI * a * b * c, shape.Volume(Halves), 1e-6);
            Assert.Equal(0f, shape.CentreOffset(Halves));

            Double3 i = shape.InertiaPerMass(Halves);
            AssertRelative((b * b + c * c) / 5.0, i.X, 1e-6);
            AssertRelative((a * a + c * c) / 5.0, i.Y, 1e-6);
            AssertRelative((a * a + b * b) / 5.0, i.Z, 1e-6);
        }

        [Fact]
        public void TheBallMatchesTheSphereShape()
        {
            var h = new Float3(0.4f, 0.4f, 0.4f);
            var shape = new Superellipsoid(new PartForm(1f, 1f, 1f));
            var sphere = new SphereShape();
            double r = 0.4;

            AssertRelative(sphere.Volume(h), shape.Volume(h), 1e-6);
            Double3 i = shape.InertiaPerMass(h);
            AssertRelative(0.4 * r * r, i.X, 1e-6);
            AssertRelative(0.4 * r * r, i.Y, 1e-6);
            AssertRelative(0.4 * r * r, i.Z, 1e-6);

            var panels = new List<DragPanel>();
            shape.AddPanels(h, 2, panels);
            double total = Sum(panels);
            _output.WriteLine($"ball at resolution 2: {panels.Count} panels, {total:0.00000} m² vs {4 * Math.PI * r * r:0.00000}");
            Assert.Equal(24, panels.Count);
            AssertRelative(4.0 * Math.PI * r * r, total, 0.001);

            var rng = new Rng(233);
            for (int n = 0; n < 200; n++)
            {
                var anchor = new Float3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f));
                Fixtures.AssertClose(sphere.SurfacePoint(anchor, h), shape.SurfacePoint(anchor, h), 1e-5f);

                var point = new Float3(rng.Range(-0.5f, 0.5f), rng.Range(-0.5f, 0.5f), rng.Range(-0.5f, 0.5f));
                if (Math.Abs(point.Magnitude - 0.4f) < 1e-4f) continue;
                Assert.Equal(sphere.ContainsPoint(point, h), shape.ContainsPoint(point, h));
            }
        }

        [Fact]
        public void BothExponentsNearZeroGiveTheBox()
        {
            var shape = new Superellipsoid(new PartForm(0.001f, 0.001f, 1f));
            var box = new BoxShape();
            double a = Halves.X, b = Halves.Y, c = Halves.Z;

            AssertRelative(box.Volume(Halves), shape.Volume(Halves), 0.01);
            Double3 i = shape.InertiaPerMass(Halves);
            AssertRelative((b * b + c * c) / 3.0, i.X, 0.01);
            AssertRelative((a * a + c * c) / 3.0, i.Y, 0.01);
            AssertRelative((a * a + b * b) / 3.0, i.Z, 0.01);

            // The panels are the box's, panel for panel and in the same order.
            var mine = new List<DragPanel>();
            var boxes = new List<DragPanel>();
            shape.AddPanels(Halves, 3, mine);
            box.AddPanels(Halves, 3, boxes);
            Assert.Equal(boxes.Count, mine.Count);

            for (int n = 0; n < mine.Count; n++)
            {
                Fixtures.AssertClose(boxes[n].Centre, mine[n].Centre, 0.01f);
                Assert.True(Float3.Dot(boxes[n].Normal, mine[n].Normal) > 0.999f, $"panel {n} normal {mine[n].Normal}");
                AssertRelative(boxes[n].Area, mine[n].Area, 0.01);
            }

            // A child anchored on a face attaches where a box would put it.
            var rng = new Rng(2331);
            for (int n = 0; n < 100; n++)
            {
                int face = n % 3;
                float sign = n % 2 == 0 ? 1f : -1f;
                var anchor = new Float3(
                    face == 0 ? sign : rng.Range(-1f, 1f),
                    face == 1 ? sign : rng.Range(-1f, 1f),
                    face == 2 ? sign : rng.Range(-1f, 1f));
                Fixtures.AssertClose(box.SurfacePoint(anchor, Halves), shape.SurfacePoint(anchor, Halves), 0.01f);
            }
        }

        // ---------------------------------------------------------------- the surface

        [Theory]
        [MemberData(nameof(Forms))]
        public void PanelAreasSumToTheSurfaceArea(float profile, float section, float taper)
        {
            var form = new PartForm(profile, section, taper);
            var shape = new Superellipsoid(form);
            double expected = SurfaceAreaByAngles(form, Halves.X, Halves.Y, Halves.Z, 1600);

            foreach (int resolution in new[] { 1, 2, 3, 4, 8 })
            {
                var panels = new List<DragPanel>();
                shape.AddPanels(Halves, resolution, panels);
                double total = Sum(panels);

                _output.WriteLine($"{form} at {resolution}: {panels.Count} panels, {total:0.000000} m² vs {expected:0.000000} " +
                                  $"({(total / expected - 1.0) * 100.0:+0.000;-0.000}%)");

                Assert.Equal(6 * resolution * resolution, panels.Count);
                AssertRelative(expected, total, 0.002);
            }
        }

        [Theory]
        [MemberData(nameof(Forms))]
        public void PanelsLieOnTheSurfaceAndFaceOut(float profile, float section, float taper)
        {
            var shape = new Superellipsoid(new PartForm(profile, section, taper));
            var panels = new List<DragPanel>();
            shape.AddPanels(Halves, 4, panels);

            foreach (DragPanel panel in panels)
            {
                Assert.InRange(shape.InsideFunction(panel.Centre, Halves), 1.0 - 1e-4, 1.0 + 1e-4);
                Assert.InRange(panel.Normal.Magnitude, 0.9999f, 1.0001f);

                // The centre of mass is inside a convex body, so every outward normal faces away from it.
                Assert.True(Float3.Dot(panel.Normal, panel.Centre) > 0f, $"panel at {panel.Centre} faces {panel.Normal}");

                // A step out along the normal leaves the solid and a step in stays inside.
                Assert.False(shape.ContainsPoint(panel.Centre + panel.Normal * 1e-3f, Halves));
                Assert.True(shape.ContainsPoint(panel.Centre - panel.Normal * 1e-3f, Halves));
            }
        }

        [Theory]
        [MemberData(nameof(Forms))]
        public void AChildAttachesOnTheSurface(float profile, float section, float taper)
        {
            var shape = new Superellipsoid(new PartForm(profile, section, taper));
            var centre = new Float3(0f, -shape.CentreOffset(Halves), 0f);
            var rng = new Rng(2332);

            for (int n = 0; n < 300; n++)
            {
                var anchor = new Float3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f));
                Float3 point = shape.SurfacePoint(anchor, Halves);

                Assert.InRange(shape.InsideFunction(point, Halves), 1.0 - 1e-4, 1.0 + 1e-4);
                Assert.True(shape.ContainsPoint(centre + (point - centre) * 0.999f, Halves));
                Assert.False(shape.ContainsPoint(centre + (point - centre) * 1.001f, Halves));
            }

            // A tiny anchor still has a ray, and a zero one attaches at the untapered centre.
            Assert.InRange(shape.InsideFunction(shape.SurfacePoint(new Float3(1e-20f, 0f, 0f), Halves), Halves), 0.9999, 1.0001);
            Assert.Equal(centre, shape.SurfacePoint(Float3.Zero, Halves));
        }

        [Theory]
        [MemberData(nameof(Forms))]
        public void TheInsideTestHoldsTheVolumeAboutTheCentreOfMass(float profile, float section, float taper)
        {
            // A count of low-discrepancy points (the R3 sequence) in a box round the solid, tying
            // the inside test to the solid whose formulas the other tests check, and its frame's
            // origin to the centre of mass. A regular grid lines up with a box-like part's faces
            // and misses or keeps whole layers of cells; this does not.
            var shape = new Superellipsoid(new PartForm(profile, section, taper));
            double ex = 1.6 * Halves.X, ey = 2.0 * Halves.Y, ez = 1.6 * Halves.Z;   // past the widest taper and shift
            const int count = 400_000;
            const double g = 1.2207440846057596;   // the R3 sequence's constant, x³ = x + 1

            double inside = 0.0, sx = 0.0, sy = 0.0, sz = 0.0;
            for (int i = 0; i < count; i++)
            {
                double ux = Frac(0.5 + i / g), uy = Frac(0.5 + i / (g * g)), uz = Frac(0.5 + i / (g * g * g));
                var p = new Float3((float)(ex * (2 * ux - 1)), (float)(ey * (2 * uy - 1)), (float)(ez * (2 * uz - 1)));
                if (!shape.ContainsPoint(p, Halves)) continue;

                inside += 1.0;
                sx += p.X; sy += p.Y; sz += p.Z;
            }

            double volume = inside / count * 8.0 * ex * ey * ez;
            _output.WriteLine($"counted volume {volume:0.00000} vs {shape.Volume(Halves):0.00000}; " +
                              $"centroid ({sx / inside:0.00000}, {sy / inside:0.00000}, {sz / inside:0.00000})");

            AssertRelative(shape.Volume(Halves), volume, 0.005);
            Assert.InRange(sx / inside, -1e-3, 1e-3);
            Assert.InRange(sy / inside, -1e-3, 1e-3);
            Assert.InRange(sz / inside, -1e-3, 1e-3);

            static double Frac(double v) => v - Math.Floor(v);
        }

        // ---------------------------------------------------------------- light

        [Theory]
        [MemberData(nameof(Forms))]
        public void ExposureAveragesOneOverEveryPose(float profile, float section, float taper)
        {
            var shape = new Superellipsoid(new PartForm(profile, section, taper));
            var panels = new List<DragPanel>();
            shape.AddPanels(Halves, 2, panels);

            const int poses = 4000;
            double sum = 0.0;
            for (int i = 0; i < poses; i++)
            {
                // A Fibonacci lattice of up directions in the part's frame, as SphereDirection draws.
                double z = 1.0 - 2.0 * (i + 0.5) / poses;
                double r = Math.Sqrt(Math.Max(0.0, 1.0 - z * z));
                double theta = 2.39996323 * i;
                sum += Superellipsoid.ExposureFactor(panels, r * Math.Cos(theta), r * Math.Sin(theta), z);
            }

            _output.WriteLine($"mean exposure {sum / poses:0.00000}");
            AssertRelative(1.0, sum / poses, 0.005);
        }

        [Fact]
        public void ExposureOnABoxLikePartIsTheBoxFormula()
        {
            var shape = new Superellipsoid(new PartForm(0.001f, 0.001f, 1f));
            var h = new Float3(0.4f, 0.05f, 0.3f);
            var panels = new List<DragPanel>();
            shape.AddPanels(h, 2, panels);

            // PhenotypePart.ExposureFactor's box formula, written out.
            double faces = h.X * h.Y + h.Y * h.Z + h.X * h.Z;
            var rng = new Rng(2333);
            for (int n = 0; n < 100; n++)
            {
                var up = new Float3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f)).Normalized;
                double shadow = h.Y * h.Z * Math.Abs(up.X) + h.X * h.Z * Math.Abs(up.Y) + h.X * h.Y * Math.Abs(up.Z);
                Fixtures.AssertClose((float)(2.0 * shadow / faces),
                    Superellipsoid.ExposureFactor(panels, up.X, up.Y, up.Z), 0.01f);
            }

            // A thin lens lying flat catches far more than its average, and on edge far less.
            var lens = new Superellipsoid(new PartForm(1f, 1f, 1f));
            panels.Clear();
            lens.AddPanels(h, 2, panels);
            Assert.True(Superellipsoid.ExposureFactor(panels, 0, 1, 0) > 1.5f);
            Assert.True(Superellipsoid.ExposureFactor(panels, 1, 0, 0) < 0.3f);
        }

        // ---------------------------------------------------------------- convexity

        [Fact]
        public void EveryMemberOfTheRangesIsConvex()
        {
            double worst = double.NegativeInfinity;
            string where = "";

            for (int i = 0; i <= 45; i++)
            {
                double profile = PartForm.ProfileMin +
                                 (PartForm.ProfileMax - PartForm.ProfileMin) * i / 45.0;

                for (int j = 0; j <= 40; j++)
                {
                    double taper = PartForm.TaperMin *
                                   Math.Pow(PartForm.TaperMax / PartForm.TaperMin, j / 40.0);

                    double margin = Superellipsoid.ProfileConcavity(profile, taper, 4000);
                    if (margin > worst) { worst = margin; where = $"profile {profile:0.000} taper {taper:0.000}"; }
                }
            }

            _output.WriteLine($"largest second difference {worst:E3} at {where}");
            Assert.True(worst <= 1e-12, $"a non-convex member at {where}: {worst:E3}");
        }

        [Fact]
        public void TheConvexityCheckFindsTheTaperLimitAtProfileOne()
        {
            // At ε1 = 1 the profile stays concave while |k|·√2 ≤ 1.
            Assert.True(Superellipsoid.ProfileConcavity(1.0, Taper(0.70), 4000) <= 1e-12);
            Assert.True(Superellipsoid.ProfileConcavity(1.0, Taper(-0.70), 4000) <= 1e-12);
            Assert.True(Superellipsoid.ProfileConcavity(1.0, Taper(0.72), 4000) > 1e-12);
            Assert.True(Superellipsoid.ProfileConcavity(1.0, Taper(-0.72), 4000) > 1e-12);

            static double Taper(double k) => (1.0 + k) / (1.0 - k);
        }

        [Fact]
        public void NoPanelSeesTheSolidOnItsFarSide()
        {
            // A check that does not lean on ProfileConcavity's argument: a closed body is convex
            // when each surface point's tangent plane has the whole surface behind it. Here across
            // the corners and middle of all three ranges, and on a taper beyond them that must fail.
            float[] profiles = { 0.1f, 0.4f, 1f };
            float[] sections = { 0.1f, 1f, 2f };
            float[] tapers = { 0.25f, 1f, 4f };

            foreach (float profile in profiles)
            foreach (float section in sections)
            foreach (float taper in tapers)
            {
                double worst = WorstSupportingPlane(new PartForm(profile, section, taper));
                Assert.True(worst <= 2e-6, $"profile {profile} section {section} taper {taper}: {worst:E3}");
            }

            double beyond = WorstSupportingPlane(new PartForm(1f, 1f, 12f));
            _output.WriteLine($"taper 12 at profile 1: worst {beyond:E3}");
            Assert.True(beyond > 1e-4);
        }

        private static double WorstSupportingPlane(PartForm form)
        {
            var shape = new Superellipsoid(form);
            var h = new Float3(0.5f, 0.5f, 0.5f);
            var panels = new List<DragPanel>();
            shape.AddPanels(h, 12, panels);

            double worst = double.NegativeInfinity;
            foreach (DragPanel p in panels)
            foreach (DragPanel q in panels)
            {
                worst = Math.Max(worst, Float3.Dot(p.Normal, q.Centre - p.Centre));
            }

            return worst;
        }

        // ---------------------------------------------------------------- independent integrals

        private struct Moments
        {
            public double Volume, Y, XX, YY, ZZ;
        }

        /// <summary>
        /// The mass integrals straight from the solid's definition: over a grid of t and z, the
        /// chord along x is closed form, and the sine substitution in both packs the samples where
        /// the profile and the slice fall steeply to their edges. No beta function is used.
        /// </summary>
        private static Moments Integrate(PartForm form, double a, double b, double c, int n)
        {
            double e1 = form.Profile, e2 = form.Section;
            double k = (form.Taper - 1.0) / (form.Taper + 1.0);
            double p = 2.0 / e2;

            var m = new Moments();
            for (int i = 0; i < n; i++)
            {
                double s = -1.0 + (i + 0.5) * 2.0 / n;
                double t = Math.Sin(0.5 * Math.PI * s);
                double dt = 0.5 * Math.PI * Math.Cos(0.5 * Math.PI * s) * 2.0 / n;

                double w = Math.Pow(Math.Max(0.0, 1.0 - Math.Pow(Math.Abs(t), 2.0 / e1)), e1 / 2.0);
                double edge = w * (1.0 + k * t);
                if (!(edge > 0.0)) continue;

                double y = b * t, dy = b * dt;
                double zMax = c * edge;

                for (int j = 0; j < n; j++)
                {
                    double sz = -1.0 + (j + 0.5) * 2.0 / n;
                    double z = zMax * Math.Sin(0.5 * Math.PI * sz);
                    double dz = zMax * 0.5 * Math.PI * Math.Cos(0.5 * Math.PI * sz) * 2.0 / n;

                    double rest = Math.Pow(edge, p) - Math.Pow(Math.Abs(z / c), p);
                    if (!(rest > 0.0)) continue;

                    double x = a * Math.Pow(rest, 1.0 / p);
                    double cell = dy * dz;

                    m.Volume += 2.0 * x * cell;
                    m.Y += y * 2.0 * x * cell;
                    m.YY += y * y * 2.0 * x * cell;
                    m.ZZ += z * z * 2.0 * x * cell;
                    m.XX += 2.0 * x * x * x / 3.0 * cell;
                }
            }

            return m;
        }

        /// <summary>
        /// The surface area by the superellipsoid's own angular parametrisation, a different map
        /// from the panels' cube rays, tessellated finely into flat triangles.
        /// </summary>
        private static double SurfaceAreaByAngles(PartForm form, double a, double b, double c, int n)
        {
            double e1 = form.Profile, e2 = form.Section;
            double k = (form.Taper - 1.0) / (form.Taper + 1.0);
            int around = 2 * n;

            Double3 Point(double eta, double omega)
            {
                double ring = SignedPower(Math.Cos(eta), e1);
                double y = b * SignedPower(Math.Sin(eta), e1);
                double f = 1.0 + k * y / b;
                return new Double3(
                    a * ring * SignedPower(Math.Cos(omega), e2) * f,
                    y,
                    c * ring * SignedPower(Math.Sin(omega), e2) * f);
            }

            var previous = new Double3[around + 1];
            var current = new Double3[around + 1];
            for (int j = 0; j <= around; j++) previous[j] = Point(-0.5 * Math.PI, 2.0 * Math.PI * j / around);

            double area = 0.0;
            for (int i = 1; i <= n; i++)
            {
                double eta = -0.5 * Math.PI + Math.PI * i / n;
                for (int j = 0; j <= around; j++) current[j] = Point(eta, 2.0 * Math.PI * j / around);

                for (int j = 0; j < around; j++)
                {
                    area += 0.5 * Double3.Cross(current[j + 1] - previous[j], current[j] - previous[j + 1]).Length;
                }

                var swap = previous; previous = current; current = swap;
            }

            return area;
        }

        /// <remarks>
        /// Snaps a rounding residue to zero first: cos(π/2) is 6e-17 in double, and its power 0.1
        /// is 0.023, which left the parametrisation's seam open by 2% of a half-extent and the
        /// reference area 0.3% short on the box-like forms while it agreed with itself at every grid.
        /// </remarks>
        private static double SignedPower(double v, double e) =>
            Math.Abs(v) < 1e-12 ? 0.0 : Math.Sign(v) * Math.Pow(Math.Abs(v), e);

        private static double Sum(List<DragPanel> panels)
        {
            double total = 0.0;
            foreach (DragPanel p in panels) total += p.Area;
            return total;
        }

        private static void AssertRelative(double expected, double actual, double relative) =>
            Assert.True(Math.Abs(actual - expected) <= relative * Math.Abs(expected),
                $"expected {expected}, got {actual} (relative tolerance {relative})");
    }
}

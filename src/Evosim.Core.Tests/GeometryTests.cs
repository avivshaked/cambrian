using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The convex hull D099 caps a body's claim on the light with — <see cref="ConvexHull"/>.
    /// </summary>
    /// <remarks>
    /// A hull is the kind of code that is wrong quietly: it returns a number for any cloud, and
    /// a number a few percent out looks exactly like a number that is right. So every case here
    /// is one whose answer is known in closed form before the hull is asked.
    /// </remarks>
    public class GeometryTests
    {
        private readonly ITestOutputHelper _output;

        public GeometryTests(ITestOutputHelper output) => _output = output;

        /// <summary>The eight corners of an axis-aligned box, appended in a fixed order.</summary>
        private static void AppendBox(List<Double3> into, Double3 centre, double hx, double hy, double hz)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        into.Add(new Double3(centre.X + hx * sx, centre.Y + hy * sy, centre.Z + hz * sz));
                    }
                }
            }
        }

        private static double BoxSurface(double hx, double hy, double hz) =>
            8.0 * (hx * hy + hy * hz + hz * hx);

        [Fact]
        public void OneBoxesCornersHullToThatBox()
        {
            // The case the whole cap rests on: an honest body, laid out in the open, must read
            // hull equal to its own surface, so that the cap never bites it.
            var pts = new List<Double3>();
            AppendBox(pts, new Double3(0.0, 0.0, 0.0), 0.5, 0.3, 0.2);

            double area = ConvexHull.SurfaceArea(pts, out bool fellBack, out double box);

            Assert.False(fellBack);
            Assert.Equal(BoxSurface(0.5, 0.3, 0.2), area, 6);
            Assert.Equal(BoxSurface(0.5, 0.3, 0.2), box, 6);
            Assert.Equal(area / 4.0, BoxSurface(0.5, 0.3, 0.2) / 4.0, 6);
        }

        [Fact]
        public void ABoxInsideAnotherAddsNothing()
        {
            // What a knot is, in miniature: a part entirely behind another contributes no
            // silhouette at all, however much surface of its own it has.
            var pts = new List<Double3>();
            AppendBox(pts, new Double3(0.0, 0.0, 0.0), 0.5, 0.5, 0.5);
            AppendBox(pts, new Double3(0.05, -0.02, 0.01), 0.2, 0.2, 0.2);

            double area = ConvexHull.SurfaceArea(pts, out bool fellBack, out _);

            Assert.False(fellBack);
            Assert.Equal(BoxSurface(0.5, 0.5, 0.5), area, 6);

            // And the arithmetic the cap actually does: two parts, one shadow.
            double summedQuarterSurfaces = BoxSurface(0.5, 0.5, 0.5) / 4.0 + BoxSurface(0.2, 0.2, 0.2) / 4.0;
            _output.WriteLine(
                $"parts summed {summedQuarterSurfaces:0.######} m2, silhouette {area / 4.0:0.######} m2");
            Assert.True(area / 4.0 < summedQuarterSurfaces);
        }

        [Fact]
        public void TwoBoxesInALineHullToLessThanTheirSumAndMoreThanEither()
        {
            // Two unit cubes face to face: the hull is the 2x1x1 box round both, surface 10,
            // against 12 for the two cubes counted separately and 6 for either alone. Nothing
            // here is hidden, and the cap still takes something off — two touching boxes share
            // the faces between them.
            var pts = new List<Double3>();
            AppendBox(pts, new Double3(-0.5, 0.0, 0.0), 0.5, 0.5, 0.5);
            AppendBox(pts, new Double3(0.5, 0.0, 0.0), 0.5, 0.5, 0.5);

            double area = ConvexHull.SurfaceArea(pts, out bool fellBack, out _);
            double one = BoxSurface(0.5, 0.5, 0.5);

            Assert.False(fellBack);
            Assert.Equal(10.0, area, 6);
            Assert.True(area < 2.0 * one, $"hull {area} is not below the summed {2.0 * one}");
            Assert.True(area > one, $"hull {area} is not above one box's {one}");
        }

        [Fact]
        public void ACoplanarCloudFallsBackToTheBoxAndSaysSo()
        {
            // No hull with an interior, so no orientation average to take. The bounding box is
            // the honest stand-in and the caller is told, rather than handed a number that
            // silently means something else.
            var pts = new List<Double3>
            {
                new Double3(0.0, 0.0, 0.0),
                new Double3(1.0, 0.0, 0.0),
                new Double3(1.0, 0.0, 1.0),
                new Double3(0.0, 0.0, 1.0),
                new Double3(0.5, 0.0, 0.5),
            };

            Assert.False(ConvexHull.TrySurfaceArea(pts, out _));

            double area = ConvexHull.SurfaceArea(pts, out bool fellBack, out double box);

            Assert.True(fellBack);
            Assert.Equal(2.0, area, 6);   // 2 * (1*0 + 0*1 + 1*1)
            Assert.Equal(box, area, 12);
        }

        [Fact]
        public void ACollinearCloudFallsBackToo()
        {
            var pts = new List<Double3>
            {
                new Double3(0.0, 0.0, 0.0),
                new Double3(1.0, 0.0, 0.0),
                new Double3(2.0, 0.0, 0.0),
                new Double3(3.0, 0.0, 0.0),
            };

            double area = ConvexHull.SurfaceArea(pts, out bool fellBack, out _);

            Assert.True(fellBack);
            Assert.Equal(0.0, area, 12);
        }

        [Fact]
        public void TheSameCloudInAnotherOrderGivesTheSameArea()
        {
            // §7's promise applies to geometry too: the answer is a property of the cloud and
            // not of the order the parts happened to be developed in. To nine places rather than
            // bit for bit: the order decides which faces are summed in which order, and IEEE
            // addition is not associative, so the last two bits of a double are the order's and
            // nothing else's.
            var forward = new List<Double3>();
            AppendBox(forward, new Double3(0.0, 0.0, 0.0), 0.5, 0.3, 0.2);
            AppendBox(forward, new Double3(0.4, 0.1, -0.3), 0.25, 0.4, 0.15);
            AppendBox(forward, new Double3(-0.2, -0.35, 0.22), 0.3, 0.1, 0.45);

            var reversed = new List<Double3>(forward);
            reversed.Reverse();

            // A third order that is neither: every third point, wrapping.
            var strided = new List<Double3>();
            for (int start = 0; start < 3; start++)
            {
                for (int i = start; i < forward.Count; i += 3) strided.Add(forward[i]);
            }

            double a = ConvexHull.SurfaceArea(forward, out bool fa, out _);
            double b = ConvexHull.SurfaceArea(reversed, out bool fb, out _);
            double c = ConvexHull.SurfaceArea(strided, out bool fc, out _);

            _output.WriteLine($"forward {a:R}, reversed {b:R}, strided {c:R}");

            Assert.False(fa);
            Assert.False(fb);
            Assert.False(fc);
            Assert.Equal(a, b, 9);
            Assert.Equal(a, c, 9);
        }

        [Fact]
        public void AHullNeverHasMoreSurfaceThanItsBox()
        {
            // The sanity check inside SurfaceArea, asserted as a property rather than trusted:
            // a convex hull is contained in the box, and a contained convex body has no more
            // surface than the one containing it.
            var pts = new List<Double3>();
            AppendBox(pts, new Double3(0.0, 0.0, 0.0), 0.5, 0.2, 0.3);
            AppendBox(pts, new Double3(0.3, 0.4, 0.1), 0.2, 0.2, 0.2);
            AppendBox(pts, new Double3(-0.4, -0.1, -0.35), 0.15, 0.3, 0.1);

            double area = ConvexHull.SurfaceArea(pts, out bool fellBack, out double box);

            Assert.False(fellBack);
            Assert.True(area <= box, $"hull {area} exceeds its box {box}");
        }

        [Fact]
        public void SixtyRandomBodiesAreMeasuredInBoundedTime()
        {
            // The guard for the one real fault in the port (2026-09-19). An incremental hull
            // assumes the faces a new point can see form one connected patch. A body from
            // GenomeFactory.Random does not always oblige — parts land exactly on top of each
            // other, so corners coincide and the horizon stops being one cycle — and the build
            // then added more faces than it removed, every round, until the O(edges²) horizon
            // pairing took minutes on one body. BrainTests found it, because it develops sixty
            // random genomes and every development now measures a silhouette.
            //
            // A wall bound rather than a face count, because the failure is unbounded work and
            // that is what has to stay bounded. Sixty bodies take milliseconds; thirty seconds
            // is three orders of magnitude of headroom on a loaded machine.
            var limits = DevelopmentLimits.Default;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int fellBack = 0, parts = 0;

            for (ulong seed = 1; seed <= 60; seed++)
            {
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 2);
                Phenotype body = Developer.Develop(genome, limits);
                if (body.PartCount == 0) continue;

                parts += body.PartCount;
                fellBack += body.SilhouetteFellBackToBox;

                Assert.True(
                    body.SilhouetteArea >= 0f && !float.IsNaN(body.SilhouetteArea) &&
                    !float.IsInfinity(body.SilhouetteArea),
                    $"seed {seed}: silhouette {body.SilhouetteArea}");
                Assert.InRange(body.LitAreaFactor(capOn: true), 0f, 1f);
            }

            clock.Stop();
            _output.WriteLine(
                $"60 random genomes, {parts} parts, {fellBack} degenerate hulls, " +
                $"{clock.Elapsed.TotalMilliseconds:0} ms");

            Assert.True(
                clock.Elapsed.TotalSeconds < 30.0,
                $"measuring sixty random bodies took {clock.Elapsed.TotalSeconds:0.#} s — the " +
                "hull is running away on one of them again");
        }

        [Fact]
        public void APartsCornersHullToItsOwnSurface()
        {
            // AppendPartCorners is the one place the hull meets a developed body, and it takes
            // every shape as its half-extent box. Eight corners, and for a box they hull back to
            // the surface the part itself reports.
            Phenotype body = Developer.Develop(Fixtures.SingleBox());

            var corners = new List<Double3>();
            ConvexHull.AppendPartCorners(body.Parts[0], corners);

            double area = ConvexHull.SurfaceArea(corners, out bool fellBack, out _);

            Assert.Equal(8, corners.Count);
            Assert.False(fellBack);
            Assert.Equal(body.Parts[0].SurfaceArea, area, 6);
        }
    }
}

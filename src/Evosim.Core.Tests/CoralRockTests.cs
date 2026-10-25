using System;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Round 62's coral, step 2 of <c>logbook/specs/coral-build.md</c>: what the world reads of it.
    /// The colony grid against a scan of every colony, the pieces' distances, the hulls' fade and its
    /// derivatives, the current faded around it in the tank and in D204's open square, the square's
    /// seams, the canopy, and the world handing one rock to every reader.
    /// </summary>
    public class CoralRockTests
    {
        private readonly ITestOutputHelper _output;

        public CoralRockTests(ITestOutputHelper output) => _output = output;

        private const float Area = 12000f;
        private const float Depth = 20f;
        private const float Side = 120f;
        private const float Period = 6000f;
        private const float Speed = 0.1f;

        private static float Radius => TankGeometry.RadiusFor(Area);

        /// <summary>CoralGeometryTests' tank: about 62 m in radius, islands on 2 m matter cells, the coral at its start values.</summary>
        private static RunConfig TankConfig() => new RunConfig
        {
            Light = new LightModel(100f, 12f),
            SharedSpace = true,
            WorldShape = WorldShape.Tank,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = Area,
            HorizontalPatches = 4,
            WorldDepthMetres = Depth,
            FieldCellMetres = 1f,
            FieldMatterCellMetres = 2f,
            BedReliefMetres = 1.5f,
            BedTiltMetres = 6f,
            MatterBudgetUnits = 1000f,
            MatterIslandWavelengthMetres = 25f,
            MatterIslandCover = 0.15f,
            MatterIslandDepthMetres = 6f,
            MatterIslandGlassMarginMetres = 8f,
            MatterInfluxPerSecond = 1f,
            MatterInfluxAt = MatterInflux.Islands,
            ReefShape = ReefShape.Coral,
        };

        /// <summary>The same islands and coral over a 120 m square (D204), on the box's own floor.</summary>
        private static RunConfig BoxConfig()
        {
            RunConfig c = TankConfig();
            c.WorldShape = WorldShape.Box;
            c.WorldAreaSquareMetres = Side * Side;
            c.BedTiltMetres = 0f;
            return c;
        }

        private static BedShape TankBed(RunConfig c, ulong landscape) =>
            new BedShape(Radius, c.WorldDepthMetres, c.BedReliefMetres, c.BedTiltMetres, c.BedScaleMetres,
                Rng.SeedFor(landscape, World.BedShapeIndex));

        private static BedShape BoxBed(RunConfig c, ulong landscape) =>
            BedShape.ForBox(Side, Side, c.WorldDepthMetres, c.BedReliefMetres, 0f,
                Rng.SeedFor(landscape, World.BedShapeIndex), 0f, 0f);

        private static readonly Lazy<(CoralRock Rock, BedShape Bed)> TankRock = new Lazy<(CoralRock, BedShape)>(() =>
        {
            RunConfig c = TankConfig();
            BedShape bed = TankBed(c, 3UL);
            CoralGeometry coral = CoralGeometry.PlaceFromLandscape(c, CoralFootprint.Tank(Radius), bed, 3UL);
            return (new CoralRock(coral, CoralFootprint.Tank(Radius), c), bed);
        });

        private static readonly Lazy<(CoralRock Rock, BedShape Bed)> BoxRock = new Lazy<(CoralRock, BedShape)>(() =>
        {
            RunConfig c = BoxConfig();
            BedShape bed = BoxBed(c, 5UL);
            CoralGeometry coral = CoralGeometry.PlaceFromLandscape(c, CoralFootprint.Box(Side, Side), bed, 5UL);
            return (new CoralRock(coral, CoralFootprint.Box(Side, Side), c), bed);
        });

        private static (CoralRock Rock, BedShape Bed, bool Box, string Name)[] Rocks() => new[]
        {
            (TankRock.Value.Rock, TankRock.Value.Bed, false, "tank"),
            (BoxRock.Value.Rock, BoxRock.Value.Bed, true, "square"),
        };

        // A colony's hull as the rock builds it: the centre of its box and half its extents, at least 0.25 m.
        private static void Hull(CoralRock rock, int c, out double cx, out double cy, out double cz,
                                 out double ax, out double ay, out double az)
        {
            rock.Coral.Bounds(c, out double x0, out double y0, out double z0, out double x1, out double y1, out double z1);
            cx = 0.5d * (x0 + x1); cy = 0.5d * (y0 + y1); cz = 0.5d * (z0 + z1);
            ax = Math.Max(0.25d, 0.5d * (x1 - x0));
            ay = Math.Max(0.25d, 0.5d * (y1 - y0));
            az = Math.Max(0.25d, 0.5d * (z1 - z0));
            double longest = Math.Max(ax, Math.Max(ay, az)) / CoralRock.HullAspect;
            ax = Math.Max(ax, longest);
            ay = Math.Max(ay, longest);
            az = Math.Max(az, longest);
        }

        /// <summary>A point near a random piece: within <paramref name="reach"/> of its first end.</summary>
        private static (double X, double Y, double Z) NearPiece(Rng rng, CoralRock rock, double reach)
        {
            int index = (int)(rng.NextFloat() * rock.Coral.PieceCount) % rock.Coral.PieceCount;
            CoralGeometry.Piece p = rock.Coral.PieceAt(index);
            double u = rng.NextFloat(), v = rng.NextFloat();
            double along = p.Kind == CoralGeometry.PieceKind.Capsule ? rng.NextFloat() : 0d;
            return (p.AX + along * (p.BX - p.AX) + reach * (2d * u - 1d),
                    p.AY + along * (p.BY - p.AY) + reach * (2d * rng.NextFloat() - 1d),
                    p.AZ + along * (p.BZ - p.AZ) + reach * (2d * v - 1d));
        }

        /// <summary>A point in a random colony's hull box widened by <paramref name="widen"/> times the fade.</summary>
        private static (double X, double Y, double Z) NearHull(Rng rng, CoralRock rock, double widen)
        {
            int c = (int)(rng.NextFloat() * rock.Count) % rock.Count;
            Hull(rock, c, out double cx, out double cy, out double cz, out double ax, out double ay, out double az);
            double f = widen * rock.FadeMetres;
            return (cx + (ax + f) * (2d * rng.NextFloat() - 1d),
                    cy + (ay + f) * (2d * rng.NextFloat() - 1d),
                    cz + (az + f) * (2d * rng.NextFloat() - 1d));
        }

        // The fade's reach from a hull's centre along a direction: the distance k0(k0 − 1)/k1 is
        // c0(t·c0 − 1)/c1 along the ray, so it meets the fade at t = (1 + fade·c1/c0)/c0.
        private static double Reach(double ax, double ay, double az, double fade, double nx, double ny, double nz)
        {
            double ux = nx / ax, uy = ny / ay, uz = nz / az;
            double c0 = Math.Sqrt(ux * ux + uy * uy + uz * uz);
            double c1 = Math.Sqrt(ux * ux / (ax * ax) + uy * uy / (ay * ay) + uz * uz / (az * az));
            return (1d + fade * c1 / c0) / c0;
        }

        // ------------------------------------------------------------------ what was placed

        [Fact]
        public void TheHullsAndTheirFadesAreReported()
        {
            foreach (var w in Rocks())
            {
                double worstStretch = 0d, worstReach = 0d, meanReach = 0d, worstBeyond = 0d;
                var rng = new Rng(3UL);
                for (int c = 0; c < w.Rock.Count; c++)
                {
                    Hull(w.Rock, c, out _, out _, out _, out double ax, out double ay, out double az);
                    double amin = Math.Min(ax, Math.Min(ay, az)), amax = Math.Max(ax, Math.Max(ay, az));
                    worstStretch = Math.Max(worstStretch, amax / amin);

                    // The longest reach over 2,000 directions, and how far past the hull's own extent
                    // along that direction it lies.
                    double longest = 0d;
                    for (int k = 0; k < 2000; k++)
                    {
                        double nx = 2d * rng.NextFloat() - 1d, ny = 2d * rng.NextFloat() - 1d, nz = 2d * rng.NextFloat() - 1d;
                        double norm = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        if (!(norm > 0.1d && norm <= 1d)) continue;
                        nx /= norm; ny /= norm; nz /= norm;
                        double reach = Reach(ax, ay, az, w.Rock.FadeMetres, nx, ny, nz);
                        double hull = Reach(ax, ay, az, 0d, nx, ny, nz);
                        longest = Math.Max(longest, reach);
                        worstBeyond = Math.Max(worstBeyond, reach - hull);
                    }

                    worstReach = Math.Max(worstReach, longest);
                    meanReach += longest / w.Rock.Count;
                }

                _output.WriteLine(
                    $"{w.Name}: {w.Rock.Count} colonies of {w.Rock.Coral.PieceCount} pieces; a hull's longest axis is at most " +
                    $"{worstStretch:0.0} times its shortest; the fade reaches {meanReach:0.0} m from a centre on the mean " +
                    $"and {worstReach:0.0} m at most, and at most {worstBeyond:0.0} m past the hull along a ray");

                // Along an axis the distance is exact, so a hull's own fade ends the fade's width past it
                // there; a neighbour's may still reach the point, so a third of the colonies at least read 1 just past.
                int clear = 0;
                for (int c = 0; c < w.Rock.Count; c++)
                {
                    Hull(w.Rock, c, out double cx0, out double cy0, out double cz0, out double ax0, out _, out _);
                    if (w.Rock.FadeAt(cx0 + ax0 + w.Rock.FadeMetres + 1e-6, cy0, cz0) == 1d) clear++;
                    Assert.True(w.Rock.FadeAt(cx0 + ax0 + w.Rock.FadeMetres - 0.5d, cy0, cz0) < 1d);
                }

                _output.WriteLine($"{w.Name}: {clear} of {w.Rock.Count} colonies read open water just past the fade along x");
                Assert.True(worstBeyond < 1.4d * w.Rock.FadeMetres, $"{w.Name}: a fade reaches {worstBeyond:0.0} m past its hull along a ray");
                Assert.True(3 * clear >= w.Rock.Count, $"{w.Name}: only {clear} of {w.Rock.Count} colonies clear just past the fade");

                Assert.InRange(w.Rock.Count, 1, 200);
            }
        }

        // ------------------------------------------------------------------ the distance

        [Fact]
        public void TheGridIsTheScanToTheBitUnderEveryLimit()
        {
            double[] limits = { -0.5d, 0d, 0.3d, 1d, 3d, 8d, 20d };

            foreach (var w in Rocks())
            {
                var rng = new Rng(11UL);
                int under = 0, over = 0;
                double extent = w.Box ? Side : 2d * Radius;

                for (int i = 0; i < 6000; i++)
                {
                    (double x, double y, double z) = i % 3 == 0
                        ? (-6d + rng.NextFloat() * (extent + 12d), -Depth * rng.NextFloat(), -6d + rng.NextFloat() * (extent + 12d))
                        : NearPiece(rng, w.Rock, i % 3 == 1 ? 1d : 6d);
                    double limit = limits[i % limits.Length];

                    double grid = w.Rock.SignedDistance(x, y, z, limit, out int gn, out ReefGeometry.Distance ga);
                    double scan = w.Rock.Scan(x, y, z, limit, out int sn, out ReefGeometry.Distance sa);

                    if (scan < limit)
                    {
                        under++;
                        Assert.Equal(scan, grid);
                        Assert.Equal(sn, gn);
                        Assert.Equal(sa.Gx, ga.Gx);
                        Assert.Equal(sa.Gy, ga.Gy);
                        Assert.Equal(sa.Gz, ga.Gz);
                    }
                    else
                    {
                        over++;
                        Assert.True(grid >= limit, $"{w.Name}: the grid read {grid} under the limit {limit} where the scan read {scan}");
                    }
                }

                _output.WriteLine($"{w.Name}: {under} queries under their limit read the scan's bits, {over} over it read at or above it");
                Assert.True(under > 2000);
            }
        }

        [Fact]
        public void EachPieceIsItsShapeAndItsGradientItsSlope()
        {
            const double H = 1e-5;

            foreach (var w in Rocks())
            {
                var rng = new Rng(17UL);
                double worstShape = 0d, worstSlope = 0d, worstUnit = 0d;

                for (int i = 0; i < 3000; i++)
                {
                    int index = (int)(rng.NextFloat() * w.Rock.Coral.PieceCount) % w.Rock.Coral.PieceCount;
                    CoralGeometry.Piece p = w.Rock.Coral.PieceAt(index);
                    double x = p.AX + 4d * (2d * rng.NextFloat() - 1d);
                    double y = p.AY + 4d * (2d * rng.NextFloat() - 1d);
                    double z = p.AZ + 4d * (2d * rng.NextFloat() - 1d);

                    ReefGeometry.Distance d = CoralRock.OfPiece(p, x, y, z);
                    if (!(d.S > 0.02d)) continue;

                    // The shape, sampled: the nearest of 2,001 points along the axis (or over the plate's disc).
                    double sampled = double.PositiveInfinity;
                    if (p.Kind == CoralGeometry.PieceKind.Capsule)
                    {
                        for (int k = 0; k <= 2000; k++)
                        {
                            double t = k / 2000d;
                            double ex = x - (p.AX + t * (p.BX - p.AX)), ey = y - (p.AY + t * (p.BY - p.AY)), ez = z - (p.AZ + t * (p.BZ - p.AZ));
                            sampled = Math.Min(sampled, Math.Sqrt(ex * ex + ey * ey + ez * ez) - p.Radius);
                        }
                    }
                    else
                    {
                        double dx = x - p.AX, dz = z - p.AZ, rho = Math.Sqrt(dx * dx + dz * dz);
                        for (int k = 0; k <= 2000; k++)
                        {
                            double r = Math.Min(rho, p.Radius) * k / 2000d;
                            double ex = rho - r, ey = y - p.AY;
                            sampled = Math.Min(sampled, Math.Sqrt(ex * ex + ey * ey) - p.HalfThickness);
                        }
                    }

                    worstShape = Math.Max(worstShape, Math.Abs(sampled - d.S));

                    double gx = (CoralRock.OfPiece(p, x + H, y, z).S - CoralRock.OfPiece(p, x - H, y, z).S) / (2d * H);
                    double gy = (CoralRock.OfPiece(p, x, y + H, z).S - CoralRock.OfPiece(p, x, y - H, z).S) / (2d * H);
                    double gz = (CoralRock.OfPiece(p, x, y, z + H).S - CoralRock.OfPiece(p, x, y, z - H).S) / (2d * H);
                    worstSlope = Math.Max(worstSlope, Math.Abs(gx - d.Gx) + Math.Abs(gy - d.Gy) + Math.Abs(gz - d.Gz));
                    worstUnit = Math.Max(worstUnit, Math.Abs(Math.Sqrt(d.Gx * d.Gx + d.Gy * d.Gy + d.Gz * d.Gz) - 1d));
                }

                _output.WriteLine(
                    $"{w.Name}: a piece's distance off its sampled shape by {worstShape:0.0e+0} m at worst, its gradient off a " +
                    $"10 µm stencil by {worstSlope:0.0e+0}, off unit length by {worstUnit:0.0e+0}");

                Assert.True(worstShape < 5e-4, $"shape {worstShape}");
                Assert.True(worstSlope < 1e-5, $"slope {worstSlope}");
                Assert.True(worstUnit < 1e-12, $"unit {worstUnit}");
            }
        }

        [Fact]
        public void AColonysUnionLiesWithinItsFilletsOfThePlainMinimum()
        {
            foreach (var w in Rocks())
            {
                var rng = new Rng(23UL);
                double fillet = w.Rock.Coral.FilletMetres;
                int smoothed = 0;

                for (int i = 0; i < 4000; i++)
                {
                    (double x, double y, double z) = NearPiece(rng, w.Rock, 1.5d);
                    double d = w.Rock.SignedDistance(x, y, z, double.PositiveInfinity, out int c, out _);
                    Assert.True(c >= 0);

                    CoralGeometry.Colony colony = w.Rock.Coral.ColonyAt(c);
                    double plain = double.PositiveInfinity;
                    for (int k = 0; k < colony.PieceCount; k++)
                        plain = Math.Min(plain, CoralRock.OfPiece(w.Rock.Coral.PieceAt(colony.FirstPiece + k), x, y, z).S);

                    Assert.True(d <= plain + 1e-12, $"{w.Name}: the union {d} above the plain minimum {plain}");
                    Assert.True(d >= plain - (colony.PieceCount - 1) * fillet / 6d - 1e-12, $"{w.Name}: the union {d} below its allowance");
                    if (d < plain) smoothed++;
                }

                _output.WriteLine($"{w.Name}: {smoothed} of 4,000 points near the coral read a fillet");
                Assert.True(smoothed > 0);
            }
        }

        [Fact]
        public void TheSquaresRockIsTheSameAcrossItsSeams()
        {
            CoralRock rock = BoxRock.Value.Rock;
            var rng = new Rng(29UL);
            double worst = 0d;

            for (int i = 0; i < 2000; i++)
            {
                (double x, double y, double z) = NearPiece(rng, rock, 3d);
                double limit = 2d;
                double d = rock.SignedDistance(x, y, z, limit, out _, out _);

                foreach ((double dx, double dz) in new[] { (Side, 0d), (-Side, 0d), (0d, Side), (0d, -Side), ((double)Side, (double)-Side) })
                {
                    double e = rock.SignedDistance(x + dx, y, z + dz, limit, out _, out _);
                    if (d < limit || e < limit) worst = Math.Max(worst, Math.Abs(d - e));
                }

                worst = Math.Max(worst, Math.Abs(rock.FadeAt(x, y, z) - rock.FadeAt(x + Side, y, z - Side)));
            }

            // A part past a face is near the coral by the far side's image: every piece keeps a margin from
            // every face, so the image is what a sphere wider than the margin meets.
            double margin = Math.Max(BoxConfig().MatterIslandGlassMarginMetres, ReefGeometry.GlassClearanceMetres);
            for (int c = 0; c < rock.Count; c++)
            {
                rock.Coral.Bounds(c, out double x0, out _, out _, out double x1, out _, out _);
                Assert.True(x0 >= margin - 1e-9 && x1 <= Side - margin + 1e-9, $"colony {c} spans {x0:0.##} to {x1:0.##} m");
            }

            _output.WriteLine($"the square's distance and fade across a seam part by {worst:0.0e+0} at worst");
            Assert.True(worst < 1e-9, $"the seam parts by {worst}");
        }

        // ------------------------------------------------------------------ the hulls' fade

        [Fact]
        public void TheFadeIsItsFloorInsideAHullOneBeyondAndItsDerivativesAreItsOwn()
        {
            const double H = 1e-4;

            foreach (var w in Rocks())
            {
                var rng = new Rng(31UL);
                double worstGradient = 0d, worstHessian = 0d, largestGradient = 0d, largestHessian = 0d;
                int touched = 0;

                for (int i = 0; i < 3000; i++)
                {
                    (double x, double y, double z) = NearHull(rng, w.Rock, 1.2d);
                    bool near = w.Rock.Fade(x, y, z, out ReefGeometry.Distance f);
                    Assert.Equal(w.Rock.FadeAt(x, y, z), f.S);
                    Assert.Equal(w.Rock.FadeReaches(x, y, z), near);
                    if (!near) { Assert.Equal(1d, f.S); continue; }
                    touched++;

                    double gx = (w.Rock.FadeAt(x + H, y, z) - w.Rock.FadeAt(x - H, y, z)) / (2d * H);
                    double gy = (w.Rock.FadeAt(x, y + H, z) - w.Rock.FadeAt(x, y - H, z)) / (2d * H);
                    double gz = (w.Rock.FadeAt(x, y, z + H) - w.Rock.FadeAt(x, y, z - H)) / (2d * H);
                    worstGradient = Math.Max(worstGradient, Math.Abs(gx - f.Gx) + Math.Abs(gy - f.Gy) + Math.Abs(gz - f.Gz));
                    largestGradient = Math.Max(largestGradient, Math.Abs(f.Gx) + Math.Abs(f.Gy) + Math.Abs(f.Gz));

                    w.Rock.Fade(x + H, y, z, out ReefGeometry.Distance px); w.Rock.Fade(x - H, y, z, out ReefGeometry.Distance mx);
                    w.Rock.Fade(x, y + H, z, out ReefGeometry.Distance py); w.Rock.Fade(x, y - H, z, out ReefGeometry.Distance my);
                    w.Rock.Fade(x, y, z + H, out ReefGeometry.Distance pz); w.Rock.Fade(x, y, z - H, out ReefGeometry.Distance mz);
                    double off =
                        Math.Abs((px.Gx - mx.Gx) / (2d * H) - f.Hxx) + Math.Abs((py.Gy - my.Gy) / (2d * H) - f.Hyy) +
                        Math.Abs((pz.Gz - mz.Gz) / (2d * H) - f.Hzz) + Math.Abs((px.Gy - mx.Gy) / (2d * H) - f.Hxy) +
                        Math.Abs((px.Gz - mx.Gz) / (2d * H) - f.Hxz) + Math.Abs((py.Gz - my.Gz) / (2d * H) - f.Hyz);
                    worstHessian = Math.Max(worstHessian, off);
                    largestHessian = Math.Max(largestHessian,
                        Math.Abs(f.Hxx) + Math.Abs(f.Hyy) + Math.Abs(f.Hzz) + Math.Abs(f.Hxy) + Math.Abs(f.Hxz) + Math.Abs(f.Hyz));
                }

                // At a hull's centre the water keeps at most the floor; far from every hull, all of it.
                for (int c = 0; c < w.Rock.Count; c++)
                {
                    Hull(w.Rock, c, out double cx, out double cy, out double cz, out _, out _, out _);
                    Assert.True(w.Rock.FadeAt(cx + 1e-3, cy, cz) <= w.Rock.FadeFloor + 1e-15);
                }

                if (!w.Box) Assert.Equal(1d, w.Rock.FadeAt(-500d, -5d, -500d));

                _output.WriteLine(
                    $"{w.Name}: {touched} points in a fade; the gradient off a 0.1 mm stencil by {worstGradient:0.0e+0} " +
                    $"(largest {largestGradient:0.0e+0} /m), the Hessian by {worstHessian:0.0e+0} (largest {largestHessian:0.0e+0} /m²)");

                Assert.True(touched > 1000);
                Assert.True(worstGradient < 1e-6 * Math.Max(largestGradient, 1d), $"gradient {worstGradient}");
                Assert.True(worstHessian < 1e-4 * Math.Max(largestHessian, 1d), $"Hessian {worstHessian}");
            }
        }

        [Fact]
        public void TheFadesListsGiveTheScansBits()
        {
            foreach (var w in Rocks())
            {
                var rng = new Rng(53UL);
                double extent = w.Box ? Side : 2d * Radius;
                int touched = 0;

                for (int i = 0; i < 20000; i++)
                {
                    (double x, double y, double z) = i % 2 == 0
                        ? NearHull(rng, w.Rock, 1.3d)
                        : (-10d + rng.NextFloat() * (extent + 20d), -Depth * rng.NextFloat(), -10d + rng.NextFloat() * (extent + 20d));

                    bool a = w.Rock.Fade(x, y, z, out ReefGeometry.Distance listed);
                    bool b = w.Rock.FadeByScan(x, y, z, out ReefGeometry.Distance scanned);
                    Assert.Equal(b, a);
                    Assert.Equal(scanned.S, listed.S);
                    Assert.Equal(scanned.Gx, listed.Gx);
                    Assert.Equal(scanned.Gy, listed.Gy);
                    Assert.Equal(scanned.Gz, listed.Gz);
                    Assert.Equal(scanned.Hxx, listed.Hxx);
                    Assert.Equal(scanned.Hxy, listed.Hxy);
                    Assert.Equal(scanned.Hxz, listed.Hxz);
                    Assert.Equal(scanned.Hyy, listed.Hyy);
                    Assert.Equal(scanned.Hyz, listed.Hyz);
                    Assert.Equal(scanned.Hzz, listed.Hzz);
                    Assert.Equal(scanned.S, w.Rock.FadeAt(x, y, z));
                    if (a) touched++;
                }

                _output.WriteLine($"{w.Name}: 20,000 points, {touched} in a fade, every one the scan's bits");
                Assert.True(touched > 3000);
            }
        }

        // ------------------------------------------------------------------ the water

        private static CurrentField SquareOcean(BedShape bed)
        {
            var field = new CurrentField { Mode = CurrentMode.Ocean, Speed = Speed, PeriodSeconds = Period, AdvectFields = true };
            field.SetBox(Side / 2f, 4, Depth, Rng.SeedFor(5UL, World.CurrentFieldIndex), patchesAcross: 2, shape: WorldShape.Box, bed: bed);
            return field;
        }

        private static CurrentField TankStreams(BedShape bed)
        {
            var field = new CurrentField { Mode = CurrentMode.Transport, Speed = Speed, PeriodSeconds = Period, AdvectFields = true };
            field.SetBox(
                (float)Math.Sqrt(Area / 4f), 4, Depth, Rng.SeedFor(3UL, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: Radius, bed: bed);
            return field;
        }

        private static (CurrentField Field, CoralRock Rock, BedShape Bed, bool Box, string Name)[] Waters()
        {
            CurrentField ocean = SquareOcean(BoxRock.Value.Bed);
            ocean.SetCoral(BoxRock.Value.Rock);
            CurrentField flatOcean = SquareOcean(null);
            flatOcean.SetCoral(BoxRock.Value.Rock);
            CurrentField tank = TankStreams(TankRock.Value.Bed);
            tank.SetCoral(TankRock.Value.Rock);

            return new[]
            {
                (ocean, BoxRock.Value.Rock, BoxRock.Value.Bed, true, "the square's ocean on its floor"),
                (flatOcean, BoxRock.Value.Rock, (BedShape)null, true, "the square's ocean on a flat floor"),
                (tank, TankRock.Value.Rock, TankRock.Value.Bed, false, "the tank's streams on its floor"),
            };
        }

        /// <summary>A point in the water near a hull, clear of the floor, the surface and (in the tank) the glass.</summary>
        private static (double X, double Y, double Z) InFade(Rng rng, CoralRock rock, BedShape bed, bool box, double inset)
        {
            while (true)
            {
                (double x, double y, double z) = NearHull(rng, rock, 1.1d);
                if (!box && !TankGeometry.Inside(x, z, Radius - 1d)) continue;
                double floor = bed != null ? bed.FloorY(box ? Wrap(x) : x, box ? Wrap(z) : z) : -Depth;
                if (!(y > floor + inset && y < -inset)) continue;
                if (rock.SignedDistance(x, y, z, inset, out _, out _) < inset) continue;
                if (!(rock.FadeAt(x, y, z) < 1d)) continue;
                return (x, y, z);
            }
        }

        private static double Wrap(double v) => v - Side * Math.Floor(v / Side);

        private static Float3 V(CurrentField field, double x, double y, double z, double t) =>
            field.VelocityAt((float)x, (float)y, (float)z, t);

        [Fact]
        public void TheWaterFadedAroundTheCoralIsDivergenceFree()
        {
            const double H = 0.0125;

            foreach (var w in Waters())
            {
                var rng = new Rng(37UL);
                double worst = 0d, speed = 0d, fullest = 0d;

                for (int i = 0; i < 1000; i++)
                {
                    (double x, double y, double z) = InFade(rng, w.Rock, w.Bed, w.Box, 0.2d);
                    double t = rng.NextFloat() * 4d * Period;

                    double dudx = (V(w.Field, x + H, y, z, t).X - V(w.Field, x - H, y, z, t).X) / (2d * H);
                    double dvdy = (V(w.Field, x, y + H, z, t).Y - V(w.Field, x, y - H, z, t).Y) / (2d * H);
                    double dwdz = (V(w.Field, x, y, z + H, t).Z - V(w.Field, x, y, z - H, t).Z) / (2d * H);
                    worst = Math.Max(worst, Math.Abs(dudx + dvdy + dwdz));
                    speed = Math.Max(speed, V(w.Field, x, y, z, t).Magnitude);
                    fullest = Math.Max(fullest, 1d - w.Rock.FadeAt(x, y, z));
                }

                _output.WriteLine(
                    $"{w.Name}: worst |div| by a 12.5 mm stencil {worst:0.0e+0} /s; fastest water in the fade {speed:0.0000} m/s; " +
                    $"the deepest fade sampled {1d - fullest:0.000}");

                Assert.True(worst < 1e-3 * speed, $"{w.Name}: divergence {worst}");
            }
        }

        [Fact]
        public void TheAccelerationAgreesWithAStencilAroundTheCoral()
        {
            foreach (var w in Waters())
            {
                var rng = new Rng(41UL);
                double sumFine = 0d, sumReference = 0d, worst = 0d, worstMagnitude = 0d;

                for (int i = 0; i < 400; i++)
                {
                    (double x, double y, double z) = InFade(rng, w.Rock, w.Bed, w.Box, 0.5d);
                    double t = rng.NextFloat() * 4d * Period;

                    Float3 analytic = w.Field.AccelerationAt((float)x, (float)y, (float)z, t);
                    Float3 finer = Stencil(w.Field, (float)x, (float)y, (float)z, t, 0.0125f, 0.0025d);
                    Float3 difference = analytic - finer;

                    sumFine += (double)difference.Magnitude * difference.Magnitude;
                    sumReference += (double)finer.Magnitude * finer.Magnitude;
                    worst = Math.Max(worst, difference.Magnitude);
                    worstMagnitude = Math.Max(worstMagnitude, finer.Magnitude);
                }

                double ratio = Math.Sqrt(sumFine / sumReference);
                _output.WriteLine(
                    $"{w.Name}: the analytic acceleration against the 12.5 mm stencil, {ratio:0.000%} RMS, worst " +
                    $"{worst / worstMagnitude:0.00%} of the largest");

                Assert.True(ratio < 0.001, $"{w.Name}: RMS {ratio:0.000%}");
                Assert.True(worst < 0.02 * worstMagnitude, $"{w.Name}: worst {worst / worstMagnitude:0.00%}");
            }
        }

        private static Float3 Stencil(CurrentField field, float x, float y, float z, double t, float h, double dt)
        {
            Float3 u = field.VelocityAt(x, y, z, t);
            float perMetre = 1f / (2f * h);

            Float3 total =
                (field.VelocityAt(x + h, y, z, t) - field.VelocityAt(x - h, y, z, t)) * (perMetre * u.X) +
                (field.VelocityAt(x, y + h, z, t) - field.VelocityAt(x, y - h, z, t)) * (perMetre * u.Y) +
                (field.VelocityAt(x, y, z + h, t) - field.VelocityAt(x, y, z - h, t)) * (perMetre * u.Z);

            return total +
                   (field.VelocityAt(x, y, z, t + dt) - field.VelocityAt(x, y, z, t - dt)) * (float)(1d / (2d * dt));
        }

        [Fact]
        public void TheOpenWaterIsTheUnfadedWaterToTheBitAndTheFadedWaterIsSlower()
        {
            foreach (var w in Waters())
            {
                CurrentField open = w.Box ? SquareOcean(w.Bed) : TankStreams(w.Bed);
                var rng = new Rng(43UL);
                int beyond = 0;
                double faded = 0d, unfaded = 0d;

                for (int i = 0; i < 3000; i++)
                {
                    double extent = w.Box ? Side : 2d * Radius;
                    double x = rng.NextFloat() * extent, z = rng.NextFloat() * extent;
                    if (!w.Box && !TankGeometry.Inside(x, z, Radius - 1d)) continue;
                    double floor = w.Bed != null ? w.Bed.FloorY(x, z) : -Depth;
                    double y = floor + 0.1d + rng.NextFloat() * (-floor - 0.2d);
                    double t = rng.NextFloat() * 4d * Period;

                    Float3 a = V(w.Field, x, y, z, t), b = V(open, x, y, z, t);
                    if (!w.Rock.FadeReaches(x, y, z))
                    {
                        beyond++;
                        Assert.Equal(b.X, a.X);
                        Assert.Equal(b.Y, a.Y);
                        Assert.Equal(b.Z, a.Z);
                        Assert.Equal(open.AccelerationAt((float)x, (float)y, (float)z, t).X, w.Field.AccelerationAt((float)x, (float)y, (float)z, t).X);
                    }
                }

                for (int i = 0; i < 2000; i++)
                {
                    (double x, double y, double z) = InFade(rng, w.Rock, w.Bed, w.Box, 0.2d);
                    if (w.Rock.FadeAt(x, y, z) > 0.5d) { i--; continue; }
                    double t = rng.NextFloat() * 4d * Period;
                    faded += V(w.Field, x, y, z, t).Magnitude;
                    unfaded += V(open, x, y, z, t).Magnitude;
                }

                _output.WriteLine(
                    $"{w.Name}: {beyond} points beyond every fade read the open water's bits; where the fade is under a " +
                    $"half the water runs at {faded / unfaded:0.00} of the open water's mean speed");

                Assert.True(beyond > 300);
                Assert.True(faded < 0.8d * unfaded);
            }
        }

        [Fact]
        public void TheSquaresFadedWaterJoinsAcrossItsSeams()
        {
            (CurrentField field, CoralRock rock, BedShape bed, _, _) = Waters()[0];
            var rng = new Rng(47UL);
            double worst = 0d;

            for (int i = 0; i < 400; i++)
            {
                (double x, double y, double z) = InFade(rng, rock, bed, true, 0.2d);
                double t = rng.NextFloat() * 4d * Period;
                Float3 v = V(field, x, y, z, t);
                foreach ((double dx, double dz) in new[] { (Side, 0d), (0d, -Side), ((double)-Side, (double)Side) })
                    worst = Math.Max(worst, (V(field, x + dx, y, z + dz, t) - v).Magnitude);
            }

            _output.WriteLine($"the faded ocean across a seam parts by {worst:0.0e+0} m/s at worst");
            Assert.True(worst < 1e-4 * Speed, $"the seam parts by {worst}");
        }

        [Fact]
        public void TheCurrentRefusesCoralBesideReefsAndInABoxWithoutTheOcean()
        {
            var streams = new CurrentField { Mode = CurrentMode.Transport, Speed = Speed, PeriodSeconds = Period };
            streams.SetBox(Side / 2f, 4, Depth, 1UL, patchesAcross: 2, shape: WorldShape.Box);
            Assert.Contains("ocean", Assert.Throws<ArgumentException>(() => streams.SetCoral(BoxRock.Value.Rock)).Message);

            CurrentField tank = TankStreams(TankRock.Value.Bed);
            tank.SetCoral(TankRock.Value.Rock);
            Assert.Contains("one or the other", Assert.Throws<ArgumentException>(() => tank.SetReefs(ReefTank.Reefs())).Message);
        }

        // ------------------------------------------------------------------ the light

        [Fact]
        public void TheCanopyShadesItsColumnsByTheirCover()
        {
            foreach (var w in Rocks())
            {
                int shaded = 0, plates = 0;
                double extent = w.Box ? Side : 2d * Radius;

                for (double x = 0.5d; x < extent; x += 1d)
                {
                    for (double z = 0.5d; z < extent; z += 1d)
                    {
                        float top = w.Rock.CanopyTop((float)x, (float)z);
                        if (float.IsNegativeInfinity(top))
                        {
                            Assert.Equal(1f, w.Rock.LightTransmission((float)x, -Depth, (float)z));
                            continue;
                        }

                        float under = w.Rock.LightTransmission((float)x, top - 0.01f, (float)z);
                        Assert.InRange(under, w.Rock.Transmission, 1f);
                        Assert.Equal(1f, w.Rock.LightTransmission((float)x, top + 0.01f, (float)z));
                        if (under < 1f) shaded++;

                        float plate = w.Rock.PlateTop((float)x, (float)z);
                        if (!float.IsNegativeInfinity(plate)) { plates++; Assert.True(plate <= top); }
                    }
                }

                _output.WriteLine($"{w.Name}: {shaded} m² of columns shaded under the canopy, {plates} m² under a plate");
                Assert.True(shaded > 0 && plates > 0);
            }
        }

        // ------------------------------------------------------------------ the world

        [Fact]
        public void TheWorldHandsOneRockToEveryReader()
        {
            RunConfig c = TankConfig();
            c.Current = new CurrentField { Mode = CurrentMode.Transport, Speed = Speed, PeriodSeconds = Period, AdvectFields = true };
            var world = new World(c, seed: 3);

            Assert.NotNull(world.Coral);
            Assert.Null(world.Reefs);
            Assert.Same(world.Coral, world.Field.Coral);
            Assert.Same(world.Coral, ((GridField)world.Nutrients).Coral);
            Assert.Same(world.Coral, ((GridField)world.Matter).Coral);
            Assert.Same(world.Coral, c.Current.Coral);

            // The mask kills every cell whose centre is in the rock, and no other.
            var grid = (GridField)world.Nutrients;
            int dead = 0;
            float h = grid.CellMetres;
            for (int c0 = 0; c0 < world.Coral.Count; c0++)
            {
                world.Coral.Coral.Bounds(c0, out double x0, out double y0, out double z0, out double x1, out double y1, out double z1);
                for (int ix = (int)(x0 / h); ix <= (int)(x1 / h); ix++)
                for (int iy = (int)(-y1 / h); iy <= (int)(-y0 / h); iy++)
                for (int iz = (int)(z0 / h); iz <= (int)(z1 / h); iz++)
                {
                    double cx = (ix + 0.5d) * h, cy = -((iy + 0.5d) * h), cz = (iz + 0.5d) * h;
                    if (iy < 0 || !TankGeometry.Inside((float)cx, (float)cz, Radius)) continue;
                    if (world.Bed != null && cy <= world.Bed.FloorY(cx, cz)) continue;
                    bool rock = world.Coral.Inside(cx, cy, cz);
                    Assert.Equal(!rock, grid.IsLive(ix, iy, iz));
                    if (rock) dead++;
                }
            }

            _output.WriteLine($"{world.Coral.Count} colonies; {dead} nutrient cells in the rock are dead");
            Assert.True(dead > 0);
        }

        [Fact]
        public void AMushroomWorldHasNoCoral()
        {
            RunConfig c = TankConfig();
            c.ReefShape = ReefShape.Mushroom;
            var world = new World(c, seed: 3);
            Assert.Null(world.Coral);
            Assert.Null(world.Field.Coral);
            Assert.Null(((GridField)world.Nutrients).Coral);
        }
    }
}

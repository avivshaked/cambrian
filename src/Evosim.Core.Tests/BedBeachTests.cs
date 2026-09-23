using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The beach's floor, grid and settling — <c>logbook/specs/beach-spec.md</c> §5, tests 1, 3 and
    /// 4, and the refusals of §2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The floor is read at round 45's tank and the round's dials</b>: 22,000 m² and 45 m, the
    /// bands at 1.5 m on a 17.641891 m scale, the tilt raised to 96 m and the shoal at 1 m. That is
    /// the floor the round will run on, and the numbers printed here are the ones its report will
    /// be read against.
    /// </para>
    /// <para>
    /// <b>The grid is read on a small beach of the same shape.</b> A 1 m grid over round 45's tank
    /// is a million live cells in an array twice as deep, and 1,200 transporter steps of it is not
    /// a test. A 400 m² tank 6 m deep with 12 m of tilt is the same geometry in miniature — a ramp
    /// under 30°, the plane three metres past the shoal at the rim, a strip of shoal about 4% of
    /// the disc — and every mechanism the spec asks about (a one-cell column over the shoal, the
    /// fade over the shelf, the mask's stair-step up the ramp) is in it.
    /// </para>
    /// </remarks>
    public class BedBeachTests
    {
        private readonly ITestOutputHelper _output;

        public BedBeachTests(ITestOutputHelper output) => _output = output;

        // Round 45's tank and the beach's dials.
        internal const float Area = 22000f;
        internal const float Depth = 45f;
        internal const float Relief = 1.5f;
        internal const float Tilt = 96f;
        internal const float Scale = 17.641891f;
        internal const float Shore = 1f;
        internal const float Fade = 15f;

        internal static float Radius => (float)Math.Sqrt(Area / Math.PI);

        internal static BedShape Beach(ulong seed, float shore = Shore, float fade = Fade, float tilt = Tilt) =>
            new BedShape(
                Radius, Depth, Relief, tilt, Scale, Rng.SeedFor(seed, World.BedShapeIndex), shore, fade);

        /// <summary>
        /// The plane's own arithmetic: the share of a disc of radius R cut off by a chord at
        /// <c>a·R</c> from the axis, where the plane <c>(T/2)·u</c> reaches the shoal's height.
        /// </summary>
        private static double PlaneShoalFraction(double depth, double shore, double tilt)
        {
            double a = (depth - shore) / (0.5d * tilt);
            return (Math.Acos(a) - a * Math.Sqrt(1d - a * a)) / Math.PI;
        }

        [Fact]
        public void TheShoalIsAMetreUnderTheSurface()
        {
            // Spec test 1. The highest floor is the shoal to the float; the shoal's share of the
            // disc is the plane's chord to within the bands' half-point; and the mean floor sits
            // under −45 m by the sliver the clamp cut off and by nothing else.
            double plane = PlaneShoalFraction(Depth, Shore, Tilt);

            _output.WriteLine(
                $"the plane's arithmetic: shoal {plane:0.0000} of the disc at tilt {Tilt} m, " +
                $"shore {Shore} m, radius {Radius:0.00} m");

            foreach (ulong seed in new[] { 1UL, 2UL, 3UL })
            {
                BedShape bed = Beach(seed);

                _output.WriteLine($"seed {seed}: {bed}");
                _output.WriteLine(
                    $"  range {bed.RangeMetres:0.000} m, total {bed.TotalRangeMetres:0.000} m, " +
                    $"floor {bed.LowestMetres - Depth:0.000} to {bed.HighestMetres - Depth:0.000000} m, " +
                    $"sliver {bed.ShoalSliverMetres:0.00000} m, slopes {bed.SteepestSlopeRadians * 180d / Math.PI:0.0}° " +
                    $"bands and {bed.SteepestTotalSlopeRadians * 180d / Math.PI:0.0}° total");

                Assert.True(bed.HasShore);
                Assert.Equal(-1d, bed.HighestMetres - Depth, 9);
                Assert.True(
                    Math.Abs(bed.ShoalAreaFraction - plane) < 0.005d,
                    $"seed {seed}: shoal {bed.ShoalAreaFraction:0.0000} against the plane's {plane:0.0000}");

                // The mean on the bed's own half-metre lattice, where the plane was fitted to zero:
                // what is left is the sliver to the rounding, which says the clamp is the only
                // thing that moved the mean.
                (double ownMean, int ownColumns) = MeanFloor(bed, 0.5d, 0.25d);
                Assert.Equal(-(double)Depth - bed.ShoalSliverMetres, ownMean, 6);

                // And a second opinion on a finer lattice offset from it, which carries its own
                // discretisation of the disc's edge.
                (double fineMean, _) = MeanFloor(bed, 0.37d, 0.31d);
                Assert.True(
                    Math.Abs(fineMean - ownMean) < 0.01d,
                    $"seed {seed}: the fine lattice's mean {fineMean:0.0000} m against {ownMean:0.0000} m");

                Assert.True(bed.ShoalSliverMetres > 0d && bed.ShoalSliverMetres < 0.1d);

                _output.WriteLine(
                    $"  mean floor {ownMean:0.00000} m over {ownColumns} columns " +
                    $"({fineMean:0.00000} m on a 0.37 m lattice)");

                // The shoal is flat: a point at the rim on the shallow side has a floor at the
                // shoal's y, no slope and no curvature.
                double sx = Radius + 0.98d * Radius * Math.Cos(bed.TiltDirectionRadians + Math.PI);
                double sz = Radius + 0.98d * Radius * Math.Sin(bed.TiltDirectionRadians + Math.PI);
                double deepX = Radius + 0.98d * Radius * Math.Cos(bed.TiltDirectionRadians);
                double deepZ = Radius + 0.98d * Radius * Math.Sin(bed.TiltDirectionRadians);

                // Whichever end of the diameter is the shallow one, it is the one at the shoal.
                if (bed.FloorY(deepX, deepZ) > bed.FloorY(sx, sz)) (sx, sz, deepX, deepZ) = (deepX, deepZ, sx, sz);

                Assert.Equal(-(double)Shore, bed.FloorY(sx, sz), 9);
                bed.HeightGradientAndHessian(
                    sx, sz, out double h, out double gx, out double gz,
                    out double xx, out double xz, out double zz);
                Assert.Equal(0d, gx);
                Assert.Equal(0d, gz);
                Assert.Equal(0d, xx);
                Assert.Equal(0d, xz);
                Assert.Equal(0d, zz);
                Assert.Equal(bed.Height(sx, sz), h);

                // The same seed with the shore off is refused, and the refusal names the beach.
                ArgumentOutOfRangeException thrown = Assert.Throws<ArgumentOutOfRangeException>(
                    () => Beach(seed, shore: 0f, fade: 0f));
                Assert.Contains("beach", thrown.Message);
            }
        }

        private static (double Mean, int Columns) MeanFloor(BedShape bed, double step, double offset)
        {
            double radius = bed.RadiusMetres;
            int side = (int)Math.Ceiling(2d * radius / step);
            double sum = 0d;
            int n = 0;

            for (int ix = 0; ix < side; ix++)
            {
                double x = (ix + offset / step) * step;

                for (int iz = 0; iz < side; iz++)
                {
                    double z = (iz + offset / step) * step;
                    if (!TankGeometry.Inside(x, z, radius)) continue;

                    sum += bed.FloorY(x, z);
                    n++;
                }
            }

            return (sum / n, n);
        }

        [Fact]
        public void AShoreThePlaneNeverReachesIsTheRecordedFloorToTheBit()
        {
            // Round 45's own floor (tilt 30 m, the shallowest column near 28 m) with the shore
            // switched on: the clamp's branch is never taken, so every reading and every height
            // is the shore-off bed's to the bit. That is the proof that the clamp changes the
            // floor where it clamps and nowhere else.
            BedShape off = new BedShape(
                Radius, Depth, Relief, 30f, Scale, Rng.SeedFor(1UL, World.BedShapeIndex));
            BedShape on = Beach(1UL, tilt: 30f);

            Assert.True(on.HasShore);
            Assert.Equal(off.RangeMetres, on.RangeMetres);
            Assert.Equal(off.TotalRangeMetres, on.TotalRangeMetres);
            Assert.Equal(off.HighestMetres, on.HighestMetres);
            Assert.Equal(off.LowestMetres, on.LowestMetres);
            Assert.Equal(off.SteepestSlopeRadians, on.SteepestSlopeRadians);
            Assert.Equal(off.SteepestTotalSlopeRadians, on.SteepestTotalSlopeRadians);
            Assert.Equal(off.Hollows, on.Hollows);
            Assert.Equal(off.Ridges, on.Ridges);
            Assert.Equal(0d, on.ShoalAreaFraction);
            Assert.Equal(0d, on.ShoalSliverMetres);
            Assert.Equal(off.ShelfWithin24Fraction, on.ShelfWithin24Fraction);

            var rng = new Rng(29UL);

            for (int i = 0; i < 2000; i++)
            {
                double x = rng.NextFloat() * 2d * Radius;
                double z = rng.NextFloat() * 2d * Radius;

                Assert.Equal(off.Height(x, z), on.Height(x, z));

                off.HeightGradientAndHessian(x, z, out double h0, out double gx0, out double gz0,
                    out double xx0, out double xz0, out double zz0);
                on.HeightGradientAndHessian(x, z, out double h1, out double gx1, out double gz1,
                    out double xx1, out double xz1, out double zz1);

                Assert.Equal(h0, h1);
                Assert.Equal(gx0, gx1);
                Assert.Equal(gz0, gz1);
                Assert.Equal(xx0, xx1);
                Assert.Equal(xz0, xz1);
                Assert.Equal(zz0, zz1);
            }

            _output.WriteLine($"round 45's floor with the shore off: {off}");
            _output.WriteLine($"and with it on, clamping nothing: {on}");
        }

        [Fact]
        public void TheBeachsRefusals()
        {
            // BedShape's own three at its own numbers.
            Assert.Throws<ArgumentOutOfRangeException>(() => Beach(1UL, shore: 1f, fade: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Beach(1UL, shore: 0f, fade: 15f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Beach(1UL, shore: 45f, fade: 15f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Beach(1UL, shore: -1f, fade: 15f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Beach(1UL, shore: float.NaN, fade: 15f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Beach(1UL, shore: 1f, fade: float.PositiveInfinity));

            // The config's setters refuse what no world could be.
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { BedShoreDepthMetres = -1f });
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig { BedShoreFadeMetres = float.NaN });

            // And the world refuses the three only it can see: a shore in a box, a shore on a
            // flat floor, and a shoal thinner than the detritus cell.
            ArgumentException box = Assert.Throws<ArgumentException>(
                () => new World(new RunConfig
                {
                    WorldShape = WorldShape.Box,
                    BedShoreDepthMetres = 1f,
                    BedShoreFadeMetres = 15f,
                }));
            Assert.Contains("box has no bed", box.Message);

            ArgumentException flat = Assert.Throws<ArgumentException>(
                () => new World(new RunConfig
                {
                    WorldShape = WorldShape.Tank,
                    FieldModel = MatterField.Grid,
                    BedShoreDepthMetres = 1f,
                    BedShoreFadeMetres = 15f,
                }));
            Assert.Contains("flat floor", flat.Message);

            ArgumentException thin = Assert.Throws<ArgumentException>(
                () => new World(new RunConfig
                {
                    WorldShape = WorldShape.Tank,
                    FieldModel = MatterField.Grid,
                    FieldCellMetres = 2f,
                    BedTiltMetres = 10f,
                    BedShoreDepthMetres = 1f,
                    BedShoreFadeMetres = 15f,
                }));
            Assert.Contains("thinner than a cell", thin.Message);

            _output.WriteLine(box.Message);
            _output.WriteLine(flat.Message);
            _output.WriteLine(thin.Message);
        }

        // ------------------------------------------------------------ the small beach's grid

        private const float SmallArea = 400f;
        private const float SmallDepth = 6f;
        private const float SmallTilt = 12f;
        private const float SmallRelief = 0.5f;
        private const float SmallFade = 3f;
        private const int Patches = 4;
        private const float Period = 6000f;
        private const ulong RunSeed = 20260923UL;

        private static float SmallRadius => TankGeometry.RadiusFor(SmallArea);

        private static BedShape SmallBeach(float shore = Shore, float fade = SmallFade) =>
            new BedShape(
                SmallRadius, SmallDepth, SmallRelief, SmallTilt, 0f,
                Rng.SeedFor(RunSeed, World.BedShapeIndex), shore, fade);

        private static GridField SmallGrid(BedShape bed, float sink = 0f) =>
            new GridField(
                SmallArea, sink, SmallDepth, 0f, 0f, Patches, 1f,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: SmallRadius, bed: bed);

        private static CurrentField SmallStreams(BedShape bed, float speed = 0.1f)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Transport,
                Speed = speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                (float)Math.Sqrt(SmallArea / Patches), Patches, SmallDepth,
                Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: SmallRadius, bed: bed);

            return field;
        }

        /// <summary>The columns whose floor is the shoal: one live cell each at 1 m cells.</summary>
        private static int ShoalColumns(GridField grid, Action<int, int> visit = null)
        {
            int n = 0;

            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
            {
                if (grid.LowestLiveLayer(ix, iz) < 0) continue;
                if (grid.FloorYAtColumn(ix, iz) != -Shore) continue;

                visit?.Invoke(ix, iz);
                n++;
            }

            return n;
        }

        [Fact]
        public void AUniformConcentrationStaysUniformOnTheBeach()
        {
            // Spec test 3, BedGridTests' method on the beach: uniform water carried 600 s by the
            // faded current at the campaign's mixing stays uniform to the rounding, and a shoal
            // column — one live cell under a still column of water — holds its total.
            const int Steps = 1200;
            const float Dt = 0.5f;
            const float Mixing = 0.02f;

            BedShape bed = SmallBeach();
            GridField field = SmallGrid(bed);
            CurrentField current = SmallStreams(bed);

            _output.WriteLine($"small beach: {bed}");

            field.SeedUniform(1f);
            double initial = field.Recount();

            var shoalStart = new System.Collections.Generic.Dictionary<(int, int), double>();
            int shoal = ShoalColumns(field, (ix, iz) =>
            {
                Assert.Equal(0, field.LowestLiveLayer(ix, iz));
                shoalStart[(ix, iz)] = field.JoulesAt(ix, 0, iz);
            });

            Assert.True(shoal > 0, "the small beach has no shoal column on its 1 m grid");

            (int substeps, _, double discrete, double analytic, _, double worstNet) =
                field.MeasureFaceFluxes(current, 0d, Dt);

            for (int step = 1; step <= Steps; step++)
            {
                field.Mix(Dt, Mixing);
                field.Advect(current, step * (double)Dt, Dt, field.PatchWidthMetres);
            }

            double min = double.MaxValue, max = double.MinValue;
            double volume = field.CellVolume;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iy = 0; iy < field.CellsY; iy++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                if (!field.IsLive(ix, iy, iz)) continue;
                double density = field.JoulesAt(ix, iy, iz) / volume;
                min = Math.Min(min, density);
                max = Math.Max(max, density);
            }

            double worstShoal = 0d;
            foreach (var entry in shoalStart)
            {
                (int ix, int iz) = entry.Key;
                worstShoal = Math.Max(worstShoal, Math.Abs(field.JoulesAt(ix, 0, iz) - entry.Value));
            }

            double drift = (field.Recount() - initial) / initial;
            double worst = Math.Max(Math.Abs(max - 1d), Math.Abs(min - 1d));

            _output.WriteLine(
                $"{field.LiveCellCount} live cells, {shoal} shoal columns: density {min:R} to {max:R} " +
                $"after {Steps} steps (worst |c−1| {worst:0.0e+0}), total moved {drift:0.0e+0}, " +
                $"worst shoal column moved {worstShoal:0.0e+0} J; {substeps} substeps, " +
                $"face speeds {discrete:0.0000} / {analytic:0.0000} m/s, worst net flux {worstNet:0.0e+0}");

            Assert.True(worst < 1e-9, $"the density ran {min:R} to {max:R}");
            Assert.True(Math.Abs(drift) < 1e-12, $"the total moved by {drift:0.0e+0}");
            Assert.True(worstShoal < 1e-9, $"a shoal column's total moved by {worstShoal:0.0e+0} J");
            Assert.Equal(0d, worstNet);
        }

        [Fact]
        public void SnowSettlesIntoTheShoalsTopCell()
        {
            // Spec test 4: a uniform snow settles into the top cell of every shoal column — the
            // one live cell there, in full light — and into the lowest live cell of every other,
            // with the total held and nothing in the rock.
            BedShape bed = SmallBeach();
            GridField field = SmallGrid(bed, sink: 0.5f);

            field.SeedUniform(1f);
            double initial = field.Recount();

            for (int step = 0; step < 1200; step++) field.Settle(0.5f);

            double onFloor = 0d, above = 0d, rock = 0d, onShoal = 0d;

            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                int lowest = field.LowestLiveLayer(ix, iz);

                for (int iy = 0; iy < field.CellsY; iy++)
                {
                    double stock = field.JoulesAt(ix, iy, iz);
                    if (lowest < 0 || !field.IsLive(ix, iy, iz)) rock += stock;
                    else if (iy == lowest) onFloor += stock;
                    else above += stock;
                }
            }

            int shoal = ShoalColumns(field, (ix, iz) =>
            {
                Assert.Equal(0, field.LowestLiveLayer(ix, iz));
                Assert.True(field.JoulesAt(ix, 0, iz) > 0d);
                onShoal += field.JoulesAt(ix, 0, iz);
            });

            _output.WriteLine(
                $"after 600 s: {onFloor / initial:0.0000%} on the floor cells, {above / initial:0.0000%} " +
                $"above them, {rock} J in the rock; {shoal} shoal columns hold {onShoal:0.000} J in " +
                $"their top cell; total moved {(field.Recount() - initial) / initial:0.0e+0}");

            Assert.Equal(0d, rock);
            Assert.True(onFloor > 0.99d * initial, $"only {onFloor / initial:0.0%} reached the floor");
            Assert.True(shoal > 0);
            Assert.Equal(initial, field.Recount(), 6);
        }

        [Fact]
        public void ThePrecomputedBeachIsTheSameWaterToTheBit()
        {
            // The fade is applied on both of the potential's paths — the direct one and the one
            // the grid's precomputed columns take — and the two must agree face for face in every
            // bit, as they do on the unfaded bed (BedGridTests).
            BedShape bed = SmallBeach();
            CurrentField current = SmallStreams(bed);

            GridField precomputed = SmallGrid(bed);
            GridField direct = SmallGrid(bed);
            direct.PrecomputeBedColumns = false;

            int carried = 0;

            foreach (double seconds in new[] { 0d, 137.5d, 4321.25d })
            {
                var fast = precomputed.MeasureFaceFluxes(current, seconds, 0.5f);
                var slow = direct.MeasureFaceFluxes(current, seconds, 0.5f);

                Assert.Equal(slow.Substeps, fast.Substeps);
                Assert.Equal(BitConverter.DoubleToInt64Bits(slow.DiscreteRms), BitConverter.DoubleToInt64Bits(fast.DiscreteRms));
                Assert.Equal(BitConverter.DoubleToInt64Bits(slow.LargestOutflow), BitConverter.DoubleToInt64Bits(fast.LargestOutflow));

                (double[] fe, double[] fl, double[] ff) = precomputed.FaceFluxesForReading();
                (double[] se, double[] sl, double[] sf) = direct.FaceFluxesForReading();

                carried += Same(se, fe) + Same(sl, fl) + Same(sf, ff);
            }

            Assert.True(carried > 1000, $"only {carried} faces carried anything");
            _output.WriteLine($"{carried} carrying faces identical over three clocks");
        }

        [Fact]
        public void TheMatterGridOverTheShallowsReadsTheNearestWaterInward()
        {
            // The coordinator's follow-up: a 2 m matter cell (5 m does not divide the small beach's
            // 6 m) is dead wherever the floor is shallower than a cell's centre, 1 m, so the whole
            // shoal has no live matter cell. What happens to a body there: GridField.TankCellAt
            // walks the point in along its own radius, half a cell at a time, to the first live
            // column, and reads, takes and deposits there — no throw, no zero, a neighbour inward.
            // This measures how far inward, and steps a world with a body on the shoal once.
            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = SmallArea,
                HorizontalPatches = 1,
                WorldDepthMetres = SmallDepth,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                MatterBudgetUnits = 1000f,
                BedReliefMetres = SmallRelief,
                BedTiltMetres = SmallTilt,
                BedShoreDepthMetres = Shore,
                BedShoreFadeMetres = SmallFade,
                Current = new CurrentField
                {
                    Mode = CurrentMode.Transport, Speed = 0.1f, PeriodSeconds = Period, AdvectFields = true,
                },
            };

            var world = new World(config, seed: 5);
            for (int i = 0; i < 10 && world.Living.Count == 0; i++) world.Step(1f);
            Assert.True(world.Living.Count > 0, "the small beach founded nobody");

            var matter = (GridField)world.Matter;
            float radius = world.TankRadiusMetres;

            // A standalone copy of the matter grid, so a probe deposit can be found by a scan.
            var probe = new GridField(
                SmallArea, 0f, SmallDepth, 0f, 0f, 1, 2f,
                patchesAcross: 1, shape: WorldShape.Tank, tankRadiusMetres: radius, bed: world.Bed);

            int dead = 0, inside = 0, shoalDead = 0;
            double worstDistance = 0d, sumDistance = 0d;
            (float X, float Z)? onShoal = null;

            for (int ix = 0; ix < probe.CellsX; ix++)
            for (int iz = 0; iz < probe.CellsZ; iz++)
            {
                float cx = (ix + 0.5f) * probe.CellMetres;
                float cz = (iz + 0.5f) * probe.CellMetres;
                if (!TankGeometry.Inside(cx, cz, radius)) continue;
                inside++;

                if (probe.ColumnIsLive(ix, iz)) continue;
                dead++;

                bool shoal = world.Bed.FloorY(cx, cz) == -Shore;
                if (shoal) { shoalDead++; onShoal ??= (cx, cz); }

                var at = new FieldPoint(new Float3(cx, -0.5f, cz), 0);
                probe.Deposit(at, 1f);

                int fx = -1, fz = -1, fy = -1;
                for (int jx = 0; jx < probe.CellsX; jx++)
                for (int jy = 0; jy < probe.CellsY; jy++)
                for (int jz = 0; jz < probe.CellsZ; jz++)
                {
                    if (probe.JoulesAt(jx, jy, jz) > 0d) { fx = jx; fy = jy; fz = jz; }
                }

                Assert.True(fx >= 0, "the deposit went nowhere");
                Assert.True(probe.IsLive(fx, fy, fz), "the deposit landed in a dead cell");
                Assert.Equal(1d / probe.CellVolume, probe.DensityAt(at), 6);
                Assert.Equal(1f, probe.Take(at, 1f), 6);

                double dx = (fx + 0.5d) * probe.CellMetres - cx;
                double dz = (fz + 0.5d) * probe.CellMetres - cz;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                worstDistance = Math.Max(worstDistance, distance);
                sumDistance += distance;
            }

            Assert.True(dead > 0 && onShoal.HasValue, "no dead matter column over the shoal to test");
            Assert.Equal(0d, probe.Recount(), 9);

            // And the world, with a living body's centre on the shoal for one step.
            Organism body = world.Living[0];
            (float sx, float sz) = onShoal.Value;
            double residualBefore = world.MatterResidual;
            world.Observe(body, new Float3(sx, -0.5f, sz), 0f);
            world.Step(1f);

            _output.WriteLine(
                $"2 m matter cells: {dead} of {inside} columns in the disc have no live cell " +
                $"({shoalDead} of them over the shoal); a point in one reads the live column " +
                $"{sumDistance / dead:0.00} m inward on average and {worstDistance:0.00} m at worst. " +
                $"One step with a body at ({sx:0.0}, −0.5, {sz:0.0}): matter residual " +
                $"{residualBefore:0.0e+0} → {world.MatterResidual:0.0e+0}, audit {world.AuditResidual:0.0e+0} J");

            Assert.True(Math.Abs(world.MatterResidual) < 1e-6 * 1000d, $"matter residual {world.MatterResidual}");
        }

        private static int Same(double[] expected, double[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            int carried = 0;

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(actual[i]));
                if (expected[i] != 0d) carried++;
            }

            return carried;
        }
    }
}

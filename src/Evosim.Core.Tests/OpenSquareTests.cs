using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D204's open square on the processor: the floor whose waves join at both seams
    /// (<see cref="BedShape.ForBox"/>), the grid's mask over it, the islands' margin from every
    /// face, and the coral placed inside that margin.
    /// </summary>
    public class OpenSquareTests
    {
        private readonly ITestOutputHelper _output;

        public OpenSquareTests(ITestOutputHelper output) => _output = output;

        private const float Depth = 20f;

        // A square of 30 m a side: one patch, a 1 m cell divides it.
        private const float SmallArea = 900f;
        private const float Side = 30f;

        private static BedShape Floor(float relief = 4f, ulong seed = 62UL) =>
            BedShape.ForBox(Side, Side, Depth, relief, 0f, seed);

        // ------------------------------------------------------------------ the floor

        [Fact]
        public void TheSquaresFloorJoinsAtBothSeams()
        {
            BedShape bed = Floor();
            var rng = new Random(7);
            double worst = 0d;

            for (int i = 0; i < 200; i++)
            {
                double x = Side * rng.NextDouble();
                double z = Side * rng.NextDouble();

                bed.HeightAndGradient(x, z, out double h, out double gx, out double gz);
                bed.HeightAndGradient(x + Side, z, out double hx, out double gxx, out double gzx);
                bed.HeightAndGradient(x, z - Side, out double hz, out double gxz, out double gzz);

                worst = Math.Max(worst, Math.Abs(h - hx));
                worst = Math.Max(worst, Math.Abs(h - hz));
                worst = Math.Max(worst, Math.Abs(gx - gxx) + Math.Abs(gz - gzx));
                worst = Math.Max(worst, Math.Abs(gx - gxz) + Math.Abs(gz - gzz));
            }

            _output.WriteLine(bed.ToString());
            _output.WriteLine(FormattableString.Invariant($"the worst mismatch across a seam: {worst:0.0e+0}"));

            Assert.True(bed.IsBox);
            Assert.True(bed.HasRelief);
            Assert.True(worst < 1e-9, FormattableString.Invariant($"the floor parts at a seam by {worst:R}"));
        }

        [Fact]
        public void TheSquaresFloorIsLevelOnAverageAndKeepsItsRelief()
        {
            BedShape bed = Floor();

            // A finer lattice than the fit's half metre: the mean over a whole period of every wave
            // is the fit's mean, which was taken off.
            const int n = 240;
            double sum = 0d;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    sum += bed.Height((i + 0.5d) * Side / n, (j + 0.5d) * Side / n);
                }
            }

            double mean = sum / (n * (double)n);
            _output.WriteLine(FormattableString.Invariant($"mean {mean:0.0e+0} m, range {bed.RangeMetres:0.###} m, tilt {bed.TiltMetres}"));

            Assert.True(Math.Abs(mean) < 1e-6, FormattableString.Invariant($"the mean floor is {mean:R} m off"));
            Assert.Equal(0f, bed.TiltMetres);
            Assert.True(bed.RangeMetres > 0.5d * bed.ReliefMetres || bed.SlopeBoundBinds);
            Assert.True(bed.SteepestSlopeRadians <= Math.Atan(BedShape.SteepestSlope) + 1e-9);
        }

        [Fact]
        public void TheTankFloorIsTheRecordedOne()
        {
            // The tank's constructor now runs through the shared one; its numbers are its own.
            float radius = TankGeometry.RadiusFor(400f);
            var bed = new BedShape(radius, Depth, 3f, 2f, 0f, 45UL);

            Assert.False(bed.IsBox);
            Assert.Equal(2f * radius, bed.LengthMetres);
            Assert.Equal(radius, bed.RadiusMetres);
            Assert.True(bed.HasRelief);
        }

        // ------------------------------------------------------------------ the grid

        [Fact]
        public void TheSquaresGridIsMaskedByItsFloor()
        {
            BedShape bed = Floor(relief: 8f);
            var grid = new GridField(SmallArea, 0f, Depth, 0f, 0f, 1, 1f, 1, WorldShape.Box, 0f, bed);
            var flat = new GridField(SmallArea, 0f, Depth, 0f, 0f, 1, 1f, 1, WorldShape.Box);

            int total = grid.CellsX * grid.LayerCount * grid.CellsX;
            _output.WriteLine(FormattableString.Invariant(
                $"{grid.LiveCellCount} of {total} cells live under the floor, {grid.LayerCount} layers; flat {flat.LiveCellCount}"));

            Assert.Equal(flat.CellsX * flat.LayerCount * flat.CellsX, flat.LiveCellCount);
            Assert.True(grid.LiveCellCount < total);

            // Every column: water to the cell whose centre is the last above the floor, rock below.
            for (int ix = 0; ix < grid.CellsX; ix++)
            {
                for (int iz = 0; iz < grid.CellsX; iz++)
                {
                    float floorY = (float)bed.FloorY(ix + 0.5d, iz + 0.5d);
                    for (int iy = 0; iy < grid.LayerCount; iy++)
                    {
                        bool water = -(iy + 0.5f) > floorY;
                        Assert.Equal(water, grid.IsLive(ix, iy, iz));
                    }
                }
            }
        }

        [Fact]
        public void TheSquaresIslandsKeepTheirMarginFromEveryFace()
        {
            const float margin = 5f;
            CoralIslandColumns columns = CoralIslandColumns.Build(
                CoralFootprint.Box(Side, Side), 1f, Depth, null, (x, z) => (float)Math.Sin(0.3 * x + 0.2 * z), 0.3f, margin);

            Assert.Equal(30, columns.CellsAlong);
            Assert.Equal(30, columns.CellsAcross);

            int eligible = 0;
            for (int ix = 0; ix < 30; ix++)
            {
                for (int iz = 0; iz < 30; iz++)
                {
                    bool near = ix + 0.5f < margin || ix + 0.5f > Side - margin || iz + 0.5f < margin || iz + 0.5f > Side - margin;
                    int label = columns.IslandOf(ix, iz);
                    if (near) Assert.Equal(CoralIslandColumns.Ineligible, label);
                    else
                    {
                        Assert.NotEqual(CoralIslandColumns.Ineligible, label);
                        eligible++;
                    }
                }
            }

            Assert.Equal(20 * 20, eligible);
            Assert.True(columns.IslandCount > 0);
        }

        // ------------------------------------------------------------------ the coral

        [Fact]
        public void TheSquaresCoralStandsInsideTheMarginFromEveryFace()
        {
            // 120 m a side: four patches, two across, 60 m each, which 1 m and 2 m cells divide.
            const float side = 120f;
            const float margin = 8f;
            var c = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Box,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = side * side,
                HorizontalPatches = 4,
                PatchesAcross = 2,
                WorldDepthMetres = Depth,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                BedReliefMetres = 1.5f,
                MatterBudgetUnits = 1000f,
                MatterIslandWavelengthMetres = 25f,
                MatterIslandCover = 0.15f,
                MatterIslandDepthMetres = 6f,
                MatterIslandGlassMarginMetres = margin,
                MatterInfluxPerSecond = 1f,
                MatterInfluxAt = MatterInflux.Islands,
                ReefShape = ReefShape.Coral,
            };

            const ulong landscape = 6201UL;
            BedShape bed = BedShape.ForBox(side, side, Depth, c.BedReliefMetres, c.BedScaleMetres,
                Rng.SeedFor(landscape, World.BedShapeIndex));
            CoralGeometry coral = CoralGeometry.PlaceFromLandscape(c, CoralFootprint.Box(side, side), bed, landscape);

            Assert.True(coral.Count > 0);

            double nearest = double.MaxValue;
            for (int k = 0; k < coral.Count; k++)
            {
                CoralGeometry.Colony colony = coral.ColonyAt(k);
                for (int i = 0; i < colony.PieceCount; i++)
                {
                    CoralGeometry.Piece p = coral.PieceAt(colony.FirstPiece + i);
                    double reach = p.Kind == CoralGeometry.PieceKind.Plate ? p.Radius + p.HalfThickness : p.Radius;
                    double edge = Math.Min(
                        Math.Min(Math.Min(p.AX, p.BX) - reach, side - (Math.Max(p.AX, p.BX) + reach)),
                        Math.Min(Math.Min(p.AZ, p.BZ) - reach, side - (Math.Max(p.AZ, p.BZ) + reach)));
                    nearest = Math.Min(nearest, edge);
                }
            }

            _output.WriteLine(FormattableString.Invariant($"{coral.Count} colonies, the nearest piece {nearest:0.###} m from a face"));
            Assert.True(nearest >= margin - 1e-9, FormattableString.Invariant($"a piece stands {nearest:R} m from a face"));

            // The same config refused over the tank's footprint, and a tank's config over the box's.
            Assert.Throws<ArgumentException>(() => CoralGeometry.Refuse(c, 40f));
        }
    }
}

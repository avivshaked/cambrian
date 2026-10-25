using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The box ocean's phase tables (<see cref="GridField.PrecomputeOceanPhases"/>): each edge
    /// column's <c>cos χ</c> and <c>sin χ</c> for the ocean's vectors, bought once instead of at
    /// every edge of every pass.
    /// </summary>
    /// <remarks>
    /// Compared bit for bit, for <c>StreamsHoistTests</c>' reason: a hoist that moved one product
    /// would be right to fifteen figures and a different realisation of every seed. Three floors,
    /// because they are three branches: the bed's map alone, the map with the shore's fade, and
    /// the map with the coral's fade multiplied last.
    /// </remarks>
    public class OceanPhaseHoistTests
    {
        private readonly ITestOutputHelper _output;

        public OceanPhaseHoistTests(ITestOutputHelper output) => _output = output;

        private const float Period = 6000f;
        private const float Speed = 0.1f;
        private const ulong RunSeed = 20261019UL;

        private sealed class Water
        {
            public string Name;
            public float Side;
            public float Depth;
            public BedShape Bed;
            public CoralRock Coral;
        }

        private static CurrentField Ocean(Water w)
        {
            var field = new CurrentField
            {
                Mode = CurrentMode.Ocean,
                Speed = Speed,
                PeriodSeconds = Period,
                AdvectFields = true,
            };

            field.SetBox(
                w.Side / 2f, 4, w.Depth, Rng.SeedFor(RunSeed, World.CurrentFieldIndex),
                patchesAcross: 2, shape: WorldShape.Box, bed: w.Bed);

            if (w.Coral != null) field.SetCoral(w.Coral);

            return field;
        }

        private static GridField Grid(Water w) =>
            new GridField(
                w.Side * w.Side, 0f, w.Depth, 0f, 0f, 4, 1f, 2, WorldShape.Box, 0f, w.Bed,
                coral: w.Coral);

        /// <summary>CoralRockTests' square: 120 m on a side, 20 m deep, islands and coral at their start values.</summary>
        private static Water CoralSquare()
        {
            const float Side = 120f;

            var c = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                WorldShape = WorldShape.Box,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Side * Side,
                HorizontalPatches = 4,
                WorldDepthMetres = 20f,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                BedReliefMetres = 1.5f,
                BedTiltMetres = 0f,
                MatterBudgetUnits = 1000f,
                MatterIslandWavelengthMetres = 25f,
                MatterIslandCover = 0.15f,
                MatterIslandDepthMetres = 6f,
                MatterIslandGlassMarginMetres = 8f,
                MatterInfluxPerSecond = 1f,
                MatterInfluxAt = MatterInflux.Islands,
                ReefShape = ReefShape.Coral,
            };

            BedShape bed = BedShape.ForBox(
                Side, Side, c.WorldDepthMetres, c.BedReliefMetres, 0f,
                Rng.SeedFor(5UL, World.BedShapeIndex), 0f, 0f);
            CoralGeometry coral = CoralGeometry.PlaceFromLandscape(c, CoralFootprint.Box(Side, Side), bed, 5UL);

            return new Water
            {
                Name = "coral",
                Side = Side,
                Depth = c.WorldDepthMetres,
                Bed = bed,
                Coral = new CoralRock(coral, CoralFootprint.Box(Side, Side), c),
            };
        }

        private static Water[] Waters() => new[]
        {
            new Water
            {
                Name = "floor",
                Side = 60f,
                Depth = 10f,
                Bed = BedShape.ForBox(60f, 60f, 10f, 4f, 0f, Rng.SeedFor(RunSeed, World.BedShapeIndex)),
            },
            new Water
            {
                Name = "shore",
                Side = 60f,
                Depth = 10f,
                Bed = BedShape.ForBox(
                    60f, 60f, 10f, 8f, 0f, Rng.SeedFor(RunSeed, World.BedShapeIndex), 3f, 2f),
            },
            CoralSquare(),
        };

        private static void SameBits(double expected, double actual, string what)
        {
            if (BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual)) return;

            Assert.Fail(
                $"{what}: the direct path gave {expected:R} and the tabled one {actual:R} — " +
                "same to the eye and not to the bit.");
        }

        private static int SameFaces(double[] slow, double[] fast, string what)
        {
            Assert.Equal(slow.Length, fast.Length);

            int carried = 0;

            for (int i = 0; i < slow.Length; i++)
            {
                SameBits(slow[i], fast[i], $"{what} face {i}");
                if (slow[i] != 0d) carried++;
            }

            return carried;
        }

        [Fact]
        public void TheTabledOceanIsTheSameWaterToTheBit()
        {
            foreach (Water w in Waters())
            {
                CurrentField current = Ocean(w);
                Assert.True(current.OceanPhasesApply, $"{w.Name}: the table should stand for this water");

                GridField tabled = Grid(w);
                GridField direct = Grid(w);

                Assert.True(tabled.PrecomputeOceanPhases);
                direct.PrecomputeOceanPhases = false;

                foreach (double seconds in new[] { 0d, 137.5d, 4321.25d })
                {
                    var fast = tabled.MeasureFaceFluxes(current, seconds, 0.5f);
                    var slow = direct.MeasureFaceFluxes(current, seconds, 0.5f);

                    Assert.Equal(slow.Substeps, fast.Substeps);
                    Assert.Equal(slow.OpenFaces, fast.OpenFaces);
                    SameBits(slow.LargestOutflow, fast.LargestOutflow, "largest outflow");
                    SameBits(slow.DiscreteRms, fast.DiscreteRms, "discrete rms");
                    SameBits(slow.WorstNetFlux, fast.WorstNetFlux, "worst net flux");

                    (double[] fastEast, double[] fastLower, double[] fastFront) = tabled.FaceFluxesForReading();
                    (double[] slowEast, double[] slowLower, double[] slowFront) = direct.FaceFluxesForReading();

                    int carried = 0;
                    carried += SameFaces(slowEast, fastEast, $"{w.Name} east");
                    carried += SameFaces(slowLower, fastLower, $"{w.Name} lower");
                    carried += SameFaces(slowFront, fastFront, $"{w.Name} front");

                    Assert.True(carried > 2000, $"{w.Name}: only {carried} faces carried anything at {seconds} s");

                    _output.WriteLine($"{w.Name,-6} t {seconds,9:0.00} s: {carried} carrying faces identical");
                }

                // The proof the tables were read, and only on the side that should read them.
                Assert.True(tabled.OceanPhasePasses >= 3, $"{w.Name}: the tables were read {tabled.OceanPhasePasses} times");
                Assert.Equal(0L, direct.OceanPhasePasses);

                // And the step itself, substep loop and all.
                GridField movedFast = Grid(w);
                GridField movedSlow = Grid(w);
                movedSlow.PrecomputeOceanPhases = false;

                movedFast.SeedUniform(1f);
                movedSlow.SeedUniform(1f);

                // A lump as well, so the step moves something a uniform field would hide.
                movedFast.Deposit(new FieldPoint(new Float3(w.Side * 0.3f, -2f, w.Side * 0.6f), 0), 50f);
                movedSlow.Deposit(new FieldPoint(new Float3(w.Side * 0.3f, -2f, w.Side * 0.6f), 0), 50f);

                for (int step = 0; step < 4; step++)
                {
                    movedFast.Advect(current, 0.5d * step, 0.5f, movedFast.PatchWidthMetres);
                    movedSlow.Advect(current, 0.5d * step, 0.5f, movedSlow.PatchWidthMetres);
                }

                int cells = 0;

                for (int ix = 0; ix < movedFast.CellsX; ix++)
                for (int iy = 0; iy < movedFast.CellsY; iy++)
                for (int iz = 0; iz < movedFast.CellsZ; iz++)
                {
                    if (!movedFast.IsLive(ix, iy, iz)) continue;

                    SameBits(
                        movedSlow.JoulesAt(ix, iy, iz), movedFast.JoulesAt(ix, iy, iz),
                        $"{w.Name} cell {ix},{iy},{iz}");

                    cells++;
                }

                Assert.True(movedFast.OceanPhasePasses >= 4);
                Assert.Equal(0L, movedSlow.OceanPhasePasses);

                _output.WriteLine($"{w.Name,-6}: {cells} live cells identical after 4 steps");
            }
        }

        [Fact]
        public void TheTableRefusesWaterItCannotStandFor()
        {
            var flat = new CurrentField { Mode = CurrentMode.Ocean, Speed = Speed, PeriodSeconds = Period, AdvectFields = true };
            flat.SetBox(30f, 4, 10f, Rng.SeedFor(RunSeed, World.CurrentFieldIndex), patchesAcross: 2, shape: WorldShape.Box);

            Assert.False(flat.OceanPhasesApply);

            var phases = new double[CurrentField.OceanPhaseStride];
            Assert.Throws<InvalidOperationException>(() => flat.PotentialAt(1f, -1f, 1f, 0d, default, phases, 0));

            Water w = Waters()[0];
            CurrentField floored = Ocean(w);
            Assert.True(floored.OceanPhasesApply);
            Assert.Throws<InvalidOperationException>(() => floored.PotentialAt(1f, -1f, 1f, 0d, default, phases, 0));

            // Another mode on the same box sends the grid back to the direct path.
            floored.Mode = CurrentMode.Transport;
            Assert.False(floored.OceanPhasesApply);
        }
    }
}

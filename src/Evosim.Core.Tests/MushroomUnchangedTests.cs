using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Round 62's coral (<c>logbook/specs/coral-build.md</c>) leaves the mushrooms as they were:
    /// with <see cref="RunConfig.ReefShape"/> at <see cref="ReefShape.Mushroom"/>, a bedded tank
    /// with reefs and islands draws the same reefs, masks the same cells, picks the same islands
    /// and seeds the same matter, bit for bit, as the build before the coral's tunables.
    /// </summary>
    /// <remarks>
    /// The digests were recorded on the build before the coral (5747365b), with this file and
    /// nothing else added. The config's own hash cannot be pinned the same way: every new
    /// tunable moves it, which is the refusal of older configs that the spec asks for.
    /// </remarks>
    public class MushroomUnchangedTests
    {
        private readonly ITestOutputHelper _output;

        public MushroomUnchangedTests(ITestOutputHelper output) => _output = output;

        /// <summary>LandscapeSeedTests' tank: a bed, reefs at 5% cover and fed islands on 2 m matter cells.</summary>
        private static RunConfig Landscape() => new RunConfig
        {
            Light = new LightModel(100f, 12f),
            SharedSpace = true,
            WorldShape = WorldShape.Tank,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = 2000f,
            HorizontalPatches = 4,
            WorldDepthMetres = 20f,
            FieldCellMetres = 1f,
            FieldMatterCellMetres = 2f,
            BedReliefMetres = 1.5f,
            BedTiltMetres = 6f,
            ReefCover = 0.05f,
            MatterBudgetUnits = 1000f,
            MatterIslandWavelengthMetres = 8f,
            MatterIslandCover = 0.25f,
            MatterIslandDepthMetres = 6f,
            MatterInfluxPerSecond = 1f,
            MatterInfluxAt = MatterInflux.Islands,
            NutrientMixingDiffusivity = 0.02f,
            HorizontalMixingDiffusivity = 0.02f,
            MatterMixingDiffusivity = 0.02f,
        };

        private static ulong Mix(ulong h, ulong v)
        {
            for (int b = 0; b < 8; b++)
            {
                h ^= (v >> (8 * b)) & 0xFFUL;
                h *= 1099511628211UL;
            }

            return h;
        }

        private static ulong Bits(double d) => (ulong)BitConverter.DoubleToInt64Bits(d);

        private static ulong ReefDigest(ReefGeometry reefs)
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, (ulong)reefs.Count);
            h = Mix(h, Bits(reefs.CoverGot));

            for (int i = 0; i < reefs.Count; i++)
            {
                ReefGeometry.Reef r = reefs.ReefAt(i);
                foreach (double d in new[] { r.X, r.Z, r.CapRadius, r.CapDepth, r.StemRadius, r.A2, r.A3, r.A4, r.P2, r.P3, r.P4 })
                {
                    h = Mix(h, Bits(d));
                }

                h = Mix(h, r.NoiseSeed);
                h = Mix(h, Bits(reefs.FadeReach(i)));
            }

            return h;
        }

        private static ulong GroundDigest(World world)
        {
            var grid = (GridField)world.Matter;
            ulong h = 14695981039346656037UL;
            h = Mix(h, (ulong)BitConverter.SingleToInt32Bits(world.IslandThreshold));

            for (int ix = 0; ix < grid.CellsX; ix++)
            for (int iz = 0; iz < grid.CellsZ; iz++)
            {
                h = Mix(h, (ulong)(long)grid.LowestLiveLayer(ix, iz));
                h = Mix(h, (ulong)grid.LiveIntervalCount(ix, iz));
                h = Mix(h, world.IslandInflux.IsIslandColumn(ix, iz) ? 1UL : 0UL);
                for (int iy = 0; iy < grid.LayerCount; iy++) h = Mix(h, Bits(grid.JoulesAt(ix, iy, iz)));
            }

            return h;
        }

        [Fact]
        public void TheMushroomWorldIsTheOneRecordedBeforeTheCoral()
        {
            var world = new World(Landscape(), seed: 6101);
            ulong reefs = ReefDigest(world.Reefs);
            ulong ground = GroundDigest(world);

            _output.WriteLine($"{world.Reefs.Count} reefs, reef digest {reefs:x16}, ground digest {ground:x16}");
            Assert.Equal(0xb4d6206acc89745eUL, reefs);
            Assert.Equal(0xf097111235182593UL, ground);
        }

        [Fact]
        public void TheMushroomPlacerDrawsTheRecordedReefs()
        {
            RunConfig c = ReefTank.Round46();
            ReefGeometry reefs = ReefGeometry.Place(
                c, BedBeachTests.Radius, ReefTank.Beach(1UL), Rng.SeedFor(1UL, World.ReefPlacementIndex));
            ulong digest = ReefDigest(reefs);

            _output.WriteLine($"{reefs.Count} reefs, digest {digest:x16}");
            Assert.Equal(0xb408660f6fd096f8UL, digest);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The gripping grazer of <c>logbook/specs/grazer-grip-control-plan.md</c>:
    /// <c>inocula/grazer-grip/grazer-grip.json</c> is round 58's plate-late grazer with a 0.2 m cube of the
    /// anchoring cell under its plate. It is valid only where the world's table holds the cell, it is planted
    /// whole with the cube in place, and it grows its mouth at its first growth step as plate-late did.
    /// </summary>
    public sealed class GrazerGripGenomeTests
    {
        private readonly ITestOutputHelper _out;

        public GrazerGripGenomeTests(ITestOutputHelper output) => _out = output;

        private static CellTypeRegistry WithAnchoring() =>
            CellTypeRegistry.Standard.Appending(new AnchoringCell());

        /// <summary>The genome from <c>inocula/</c>, or null where the repo is not laid out as expected.</summary>
        private Genome Load()
        {
            string path = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "inocula", "grazer-grip", "grazer-grip.json"));

            if (!File.Exists(path))
            {
                _out.WriteLine($"no genome at {path}; skipped");
                return null;
            }

            return GenomeJson.Read(File.ReadAllText(path));
        }

        // r57-s2's config in round 59's format, with the trickle, its pool and the floor off, so a Core world
        // can be founded without the pool's files; and the anchoring cell appended, as EVOSIM_ANCHORING does.
        private static RunConfig Round57WithTheCell()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "r57-s2-config.json");
            RunConfig config = RunConfigJson.Read(File.ReadAllText(path), out string mismatch);
            Assert.Null(mismatch);
            config.FoundingTricklePoolShare = 0f;
            config.FoundingFloorPoolShare = 0f;
            config.FoundingTricklePoolCount = 0;
            config.FoundingTricklePoolHash = "";
            config.FoundingTricklePerSecond = 0f;
            config.MinimumPopulation = 0;
            config.CellTypes = config.CellTypes.Appending(new AnchoringCell());
            return config;
        }

        [Fact]
        public void ItIsValidOnlyWhereTheTableHoldsTheCellAndCarriesTheCubeUnderItsPlate()
        {
            Genome genome = Load();
            if (genome == null) return;

            Assert.Empty(genome.Validate(cellTypes: WithAnchoring()));
            Assert.Contains(genome.Validate(), issue => issue.Contains("unknown cell type 'anchoring'"));

            Phenotype body = Developer.Develop(genome, DevelopmentLimits.Default, cellTypes: WithAnchoring());
            PhenotypePart plate = body.Parts.Single(p => p.CellTypeId == CellTypeIds.Consumer);
            PhenotypePart cube = body.Parts.Single(p => p.CellTypeId == CellTypeIds.Anchoring);

            // Before its first growth step the body is the plate and the cube: the mouth's cube is a module.
            Assert.Equal(2, body.Parts.Count);
            Assert.Equal(1f, plate.Attack);
            Assert.Equal(0f, plate.Intake);
            Assert.Equal(JointType.Fixed, cube.JointType);
            Assert.Equal(0.008f, cube.Volume, 5);

            // The cube hangs under the plate: its centre is below the plate's by the two half-heights.
            Assert.Equal(plate.Position.Y - 0.03f - 0.1f, cube.Position.Y, 4);
        }

        [Fact]
        public void InRound57sWorldWithTheCellItIsPlantedWholeAndGrowsItsMouth()
        {
            Genome genome = Load();
            if (genome == null) return;

            RunConfig config = Round57WithTheCell();
            var world = new World(config, seed: 1);
            world.Inoculate(genome, count: 1, heightY: -3f);

            Organism grazer = Assert.Single(world.Living);
            Assert.Contains(grazer.Phenotype.Parts, p => p.CellTypeId == CellTypeIds.Anchoring);
            Assert.DoesNotContain(grazer.Phenotype.Parts, p => p.Intake > 0f);

            var cell = (AnchoringCell)config.CellTypes.Resolve(CellTypeIds.Anchoring);
            float force = cell.BreakingForce(0.008f);
            _out.WriteLine(FormattableString.Invariant(
                $"planted with {grazer.Energy:0.0} J; the cube breaks free at {force:0} N, at a price of {cell.BuildPrice(force):0.###} J, and costs {cell.Upkeep(0.008f, 1f):0.###} W"));
            Assert.InRange(force, 2300f, 2340f);

            // The farm runs the module rule at each growth step (Metabolise); a Core world does not, so the
            // test runs it once. With the 200 J purse over 300 s of standing cost, the mouth's cube is added
            // at the first, as plate-late's was in r58g-smoke2, and the anchoring cube stays.
            Assert.Equal(1, world.ApplyModuleRule(config.GrowthStepSeconds));

            grazer = Assert.Single(world.Living);
            Assert.Contains(grazer.Phenotype.Parts, p => p.Intake == 1f && p.Attack == 1f);
            Assert.Contains(grazer.Phenotype.Parts, p => p.CellTypeId == CellTypeIds.Anchoring);
            Assert.Equal(3, grazer.Phenotype.Parts.Count);
            _out.WriteLine(FormattableString.Invariant(
                $"after the rule: {grazer.Phenotype.Parts.Count} parts, {grazer.Energy:0.0} J, modules {string.Join(",", grazer.ModuleCounts ?? Array.Empty<int>())}"));
        }
    }
}

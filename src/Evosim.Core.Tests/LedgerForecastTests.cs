using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="LedgerForecast"/> against the same closed-form arithmetic
    /// <see cref="Metabolism"/>'s own tests use — a single-node body, so the ledger's terms are
    /// checkable by hand rather than only by re-running the code that computes them.
    /// </summary>
    public class LedgerForecastTests
    {
        private readonly ITestOutputHelper _output;

        public LedgerForecastTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void APhotosyntheticBodyAtTheSurfaceReproducesAndEventuallySuccumbsToSenescence()
        {
            Genome genome = SingleCellGenome(CellTypeIds.Photosynthetic, broodSize: 1, endowment: 100f);
            var config = new RunConfig { SenescenceDoublingSeconds = 3000f };

            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);

            LedgerForecastResult result = LedgerForecast.Forecast(
                body, config,
                irradianceWattsPerSquareMetre: 200f,
                nutrientDensityJoulesPerCubicMetre: 0f,
                shadeFraction: 0f,
                reproduction: genome.Reproduction);

            _output.WriteLine(
                $"photosynthetic @ 200 W/m2: {result}, first child at " +
                $"{result.TimeToFirstChildSeconds?.ToString("0.#") ?? "never"} s");

            Assert.True(result.NetWattsAtBirth > 0f, "a plant at 200 W/m2 should earn more than it spends");
            Assert.True(result.ChildrenProduced > 1, $"expected R0 > 1, got {result.ChildrenProduced}");

            // Senescence (doubling every 3000 s) eventually outpaces a fixed income: costs rise
            // with wear and conversion falls by the same factor, so net crosses zero and the body
            // starves rather than living out the full forecast window.
            Assert.True(
                result.LifetimeSeconds < LedgerForecast.MaxLifetimeSeconds,
                "senescence never caught up with this body inside the forecast cap");
            Assert.True(result.DiedOfStarvation, "expected senescence to end this lineage, not the cap");
        }

        [Fact]
        public void AnAbsorptiveBodyAtClearanceOneInThinWaterNeverBreedsAndDies()
        {
            Genome genome = SingleCellGenome(CellTypeIds.Absorptive, broodSize: 1, endowment: 100f);
            var config = new RunConfig { CellTypes = AbsorptiveRegistry(clearanceRate: 1f) };

            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);

            LedgerForecastResult result = LedgerForecast.Forecast(
                body, config,
                irradianceWattsPerSquareMetre: 0f,
                nutrientDensityJoulesPerCubicMetre: 1f,
                shadeFraction: 0f,
                reproduction: genome.Reproduction);

            _output.WriteLine($"absorptive, clearance 1, density 1 J/m3: {result}");

            Assert.True(result.NetWattsAtBirth < 0f, "1 J/m3 at clearance 1 should not cover upkeep");
            Assert.Equal(0, result.ChildrenProduced);
            Assert.True(result.DiedOfStarvation);
        }

        [Fact]
        public void AnAbsorptiveBodyAtClearanceTenInRichWaterReproduces()
        {
            Genome genome = SingleCellGenome(CellTypeIds.Absorptive, broodSize: 1, endowment: 100f);
            var config = new RunConfig { CellTypes = AbsorptiveRegistry(clearanceRate: 10f) };

            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);

            LedgerForecastResult result = LedgerForecast.Forecast(
                body, config,
                irradianceWattsPerSquareMetre: 0f,
                nutrientDensityJoulesPerCubicMetre: 7f,
                shadeFraction: 0f,
                reproduction: genome.Reproduction);

            _output.WriteLine($"absorptive, clearance 10, density 7 J/m3: {result}");

            Assert.True(result.NetWattsAtBirth > 0f, "7 J/m3 at clearance 10 should clear upkeep easily");
            Assert.True(result.ChildrenProduced > 1, $"expected R0 > 1, got {result.ChildrenProduced}");
        }

        [Fact]
        public void BreakEvenDensityForAnAbsorptiveBodyIsUpkeepOverClearance()
        {
            // Default AbsorptiveCell: clearance 1, upkeep 4 W/m3, yield 1 — so income is
            // density x clearance x volume and upkeep is 4 x volume; the volume cancels and the
            // break-even density is upkeep / clearance = 4, independent of body size.
            Genome genome = SingleCellGenome(CellTypeIds.Absorptive, broodSize: 1, endowment: 100f);
            var config = new RunConfig();

            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);

            LedgerForecastResult result = LedgerForecast.Forecast(
                body, config,
                irradianceWattsPerSquareMetre: 0f,
                nutrientDensityJoulesPerCubicMetre: 0f,
                shadeFraction: 0f,
                reproduction: genome.Reproduction);

            _output.WriteLine($"break-even density: {result.BreakEvenNutrientDensity}");

            Assert.True(result.BreakEvenNutrientDensity.HasValue, "an absorptive body must have a break-even density");
            Fixtures.AssertClose(4f, result.BreakEvenNutrientDensity.Value, tol: 1e-3f);
        }

        [Fact]
        public void ABodyWithNoAbsorptiveTissueHasNoBreakEvenDensity()
        {
            Genome genome = SingleCellGenome(CellTypeIds.Photosynthetic, broodSize: 1, endowment: 100f);
            var config = new RunConfig();

            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);

            LedgerForecastResult result = LedgerForecast.Forecast(
                body, config,
                irradianceWattsPerSquareMetre: 200f,
                nutrientDensityJoulesPerCubicMetre: 3f,
                shadeFraction: 0f,
                reproduction: genome.Reproduction);

            Assert.False(result.BreakEvenNutrientDensity.HasValue);
        }

        [Fact]
        public void RejectsAStillbornPhenotype()
        {
            // A phenotype with no parts has nothing to price — the same guard World.Admit applies
            // before a stillbirth ever reaches the economy.
            var empty = new Phenotype();
            var config = new RunConfig();

            Assert.Throws<ArgumentException>(() => LedgerForecast.Forecast(
                empty, config, 100f, 1f, 0f,
                new ReproductionTraits { BroodSize = 1, OffspringEndowment = 10f }));
        }

        /// <summary>One unjointed box of a single cell type — the simplest body that can earn or spend.</summary>
        private static Genome SingleCellGenome(string cellTypeId, int broodSize, float endowment)
        {
            var genome = new Genome
            {
                RootIndex = 0,
                Reproduction = new ReproductionTraits { BroodSize = broodSize, OffspringEndowment = endowment },
            };

            genome.Nodes.Add(new MorphNode
            {
                CellTypeId = cellTypeId,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });

            return genome;
        }

        /// <summary>The standard registry with the absorptive cell's clearance rate replaced.</summary>
        private static CellTypeRegistry AbsorptiveRegistry(float clearanceRate) => new CellTypeRegistry(
            new StructuralCell(),
            new LinkCell(),
            new NeuralCell(),
            new PhotosyntheticCell(),
            new AbsorptiveCell(clearanceRate),
            new ConsumerCell(),
            new BuoyancyCell());
    }
}

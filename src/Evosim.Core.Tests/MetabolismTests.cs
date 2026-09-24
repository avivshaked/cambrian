using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The round 48 ruling on senescence (owner, 2026-09-24): <see cref="RunConfig.SenescenceWearsIntake"/>
    /// off wears upkeep alone, and on is D038's arithmetic.
    /// </summary>
    public class MetabolismTests
    {
        private readonly ITestOutputHelper _output;

        public MetabolismTests(ITestOutputHelper output) => _output = output;

        private static Phenotype Body(RunConfig config, string cellType)
        {
            var genome = Fixtures.SingleBox();
            genome.Nodes[0].CellTypeId = cellType;
            return Developer.Develop(genome, config.Development, shapes: config.Shapes);
        }

        private static EnergyLedger At(RunConfig config, string cellType, float age) =>
            Metabolism.StepAt(
                Body(config, cellType), config, irradiance: 200f, nutrientDensity: 40f,
                spentDensity: 1f, workJoules: 0f, seconds: 1f, ageSeconds: age);

        [Fact]
        public void TheSwitchDefaultsToD038sWorld()
        {
            Assert.True(new RunConfig().SenescenceWearsIntake);
        }

        [Theory]
        [InlineData(CellTypeIds.Photosynthetic)]
        [InlineData(CellTypeIds.Absorptive)]
        public void WithTheSwitchOffIntakeIsUnchangedByAgeAndUpkeepStillWears(string cellType)
        {
            var config = new RunConfig { SenescenceDoublingSeconds = 500f, SenescenceWearsIntake = false };

            EnergyLedger young = At(config, cellType, 0f);
            Assert.True(young.Upkeep > 0f, "the fixture has no upkeep to wear");
            Assert.True(young.Income > 0d, "the fixture earns nothing");

            foreach (float age in new[] { 1f, 250f, 500f, 3_000f })
            {
                EnergyLedger old = At(config, cellType, age);
                float wear = 1f + age / 500f;

                _output.WriteLine($"{cellType} at {age} s: {old}");

                // Every intake term to the bit: nothing divides them.
                Assert.Equal(young.LightIncome, old.LightIncome);
                Assert.Equal(young.FoodIncome, old.FoodIncome);
                Assert.Equal(young.PoolDrawn, old.PoolDrawn);
                Assert.Equal(young.LightCapacity, old.LightCapacity);
                Assert.Equal(young.Exuded, old.Exuded);

                // The costs wear as they always did.
                Assert.Equal(young.Upkeep * wear, old.Upkeep, 4);
                Assert.Equal(young.Neural * wear, old.Neural, 4);
                Assert.True(old.Upkeep > young.Upkeep || age == 0f);
            }
        }

        [Theory]
        [InlineData(CellTypeIds.Photosynthetic)]
        [InlineData(CellTypeIds.Absorptive)]
        public void WithTheSwitchOnTheArithmeticIsTheRecordedOne(string cellType)
        {
            // The recorded expression written out: upkeep and neural times the wear, the light,
            // the food and the capacity over it, the draw untouched.
            var on = new RunConfig { SenescenceDoublingSeconds = 500f, SenescenceWearsIntake = true };
            var ageless = new RunConfig();

            Phenotype body = Body(on, cellType);

            foreach (float age in new[] { 0f, 1f, 500f, 3_000f })
            {
                float wear = age > 0f ? 1f + age / 500f : 1f;

                EnergyLedger old = Metabolism.StepAt(
                    body, on, 200f, 40f, 1f, workJoules: 0f, seconds: 1f, ageSeconds: age);
                EnergyLedger raw = Metabolism.StepAt(
                    body, ageless, 200f, 40f, 1f, workJoules: 0f, seconds: 1f, ageSeconds: age);

                Assert.Equal(raw.LightIncome / wear, old.LightIncome);
                Assert.Equal(raw.FoodIncome / wear, old.FoodIncome);
                Assert.Equal(raw.LightCapacity / wear, old.LightCapacity);
                Assert.Equal(raw.PoolDrawn, old.PoolDrawn);
                Assert.Equal(raw.Upkeep * wear, old.Upkeep);
                Assert.Equal(raw.Neural * wear, old.Neural);
            }
        }

        [Fact]
        public void OffTheBreakEvenRisesAsTheWearAndNotItsSquare()
        {
            // The ruling's reason, read on a stomach: its net at the density that breaks it even at
            // birth. With both sides worn the old body is a long way under; with upkeep alone it
            // is under by the upkeep's growth only.
            var both = new RunConfig { SenescenceDoublingSeconds = 3_000f };
            var upkeepOnly = new RunConfig { SenescenceDoublingSeconds = 3_000f, SenescenceWearsIntake = false };

            Phenotype stomach = Body(both, CellTypeIds.Absorptive);

            EnergyLedger Net(RunConfig c, float density, float age) => Metabolism.StepAt(
                stomach, c, 0f, density, 0f, workJoules: 0f, seconds: 1f, ageSeconds: age);

            double youngNet = Net(both, 40f, 0f).Net;
            double oldBoth = Net(both, 40f, 3_000f).Net;
            double oldUpkeep = Net(upkeepOnly, 40f, 3_000f).Net;

            _output.WriteLine($"net at 40 J/m3: young {youngNet:0.####} W, at 3,000 s both {oldBoth:0.####} W, upkeep only {oldUpkeep:0.####} W");

            Assert.True(oldUpkeep > oldBoth, "wearing upkeep alone should leave an old stomach better off");
            Assert.True(oldUpkeep < youngNet, "an old stomach still pays more than a young one");
        }
    }
}

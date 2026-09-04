using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D070's exudation at the level <see cref="Metabolism.StepAt"/> prices it — one body, one
    /// step, arithmetic checkable by hand. What the *world* does with the released joules is
    /// <see cref="WorldTests"/>'s question.
    /// </summary>
    /// <remarks>
    /// The knob exists because dead tissue alone feeds the second trophic level at about 1% of
    /// primary production (D070, logbook/0050). These tests do not judge the dose — they check
    /// that the fraction named is the fraction released, that it comes out of the body's own net
    /// rather than out of nowhere, and that a stomach is left alone.
    /// </remarks>
    public class ExudationTests
    {
        private readonly ITestOutputHelper _output;

        public ExudationTests(ITestOutputHelper output) => _output = output;

        [Theory]
        [InlineData(0.05f)]
        [InlineData(0.15f)]
        [InlineData(0.37f)]
        public void AProducerExudesExactlyTheNamedFractionOfItsLightIncome(float fraction)
        {
            var off = new RunConfig();
            var on = new RunConfig { ExudationFraction = fraction };

            Phenotype body = Body(CellTypeIds.Photosynthetic, off);

            EnergyLedger baseline = Metabolism.StepAt(
                body, off, irradiance: 200f, nutrientDensity: 0f, workJoules: 0f, seconds: 1f);
            EnergyLedger exuding = Metabolism.StepAt(
                body, on, irradiance: 200f, nutrientDensity: 0f, workJoules: 0f, seconds: 1f);

            _output.WriteLine(
                $"fraction {fraction}: light {baseline.LightIncome:0.####} J, " +
                $"exuded {exuding.Exuded:0.####} J, net {baseline.Net:0.####} -> {exuding.Net:0.####}");

            Assert.True(baseline.LightIncome > 0f, "no light income — the test measures nothing");
            Assert.Equal(0f, baseline.Exuded);

            // Income is untouched: the world still counts the gross photosynthesis as new energy,
            // and the release is a transfer out of the body rather than a smaller harvest.
            Fixtures.AssertClose(baseline.LightIncome, exuding.LightIncome, 0f);
            Fixtures.AssertClose(fraction * exuding.LightIncome, exuding.Exuded, 1e-6f);

            // Net falls by exactly the joules released and by nothing else — no second charge
            // hidden in upkeep, and no rounding of the deduction.
            Fixtures.AssertClose(baseline.Expenditure, exuding.Expenditure, 0f);
            Fixtures.AssertClose(baseline.Net - exuding.Exuded, exuding.Net, 1e-6f);
        }

        [Fact]
        public void AStomachDoesNotExudeWhatItAte()
        {
            // Exudation is dissolved organic matter leaking from a photosynthesising cell
            // [LS13 p.1]. Applying it to FoodIncome would put a second transfer loss on top of
            // CellIntake.PoolDrawn's, and would make a consumer feed the field it grazes.
            var config = new RunConfig { ExudationFraction = 0.15f };
            Phenotype body = Body(CellTypeIds.Absorptive, config);

            EnergyLedger ledger = Metabolism.StepAt(
                body, config, irradiance: 0f, nutrientDensity: 500f, workJoules: 0f, seconds: 1f);

            _output.WriteLine(
                $"absorptive in the dark: food {ledger.FoodIncome:0.####} J, " +
                $"light {ledger.LightIncome:0.####} J, exuded {ledger.Exuded:0.####} J");

            Assert.True(ledger.FoodIncome > 0f, "nothing was eaten — the test measures nothing");
            Assert.Equal(0f, ledger.LightIncome);
            Assert.Equal(0f, ledger.Exuded);

            // And with light off, Net is what it always was.
            EnergyLedger noKnob = Metabolism.StepAt(
                Body(CellTypeIds.Absorptive, new RunConfig()), new RunConfig(),
                irradiance: 0f, nutrientDensity: 500f, workJoules: 0f, seconds: 1f);

            Fixtures.AssertClose(noKnob.Net, ledger.Net, 0f);
        }

        [Fact]
        public void ReleaseIsTakenFromTheLightTheBodyKeptRatherThanTheLightItCaught()
        {
            // Under senescence, conversion falls by the same factor costs rise by, and exudation
            // is a fraction of *intake* — so an old producer fixes less and therefore releases
            // less. Charging the pre-wear figure would have an ageing cell exude a rising share of
            // what it actually kept, which is a mechanism nobody asked for.
            var config = new RunConfig { ExudationFraction = 0.2f, SenescenceDoublingSeconds = 1000f };
            Phenotype body = Body(CellTypeIds.Photosynthetic, config);

            EnergyLedger young = Metabolism.StepAt(
                body, config, 200f, 0f, workJoules: 0f, seconds: 1f, ageSeconds: 0f);
            EnergyLedger old = Metabolism.StepAt(
                body, config, 200f, 0f, workJoules: 0f, seconds: 1f, ageSeconds: 3000f);

            _output.WriteLine(
                $"young: light {young.LightIncome:0.####} exuded {young.Exuded:0.####}; " +
                $"old (wear 4): light {old.LightIncome:0.####} exuded {old.Exuded:0.####}");

            Fixtures.AssertClose(0.2f * young.LightIncome, young.Exuded, 1e-6f);
            Fixtures.AssertClose(0.2f * old.LightIncome, old.Exuded, 1e-6f);
            Assert.True(old.Exuded < young.Exuded, "wear did not reach the release");
        }

        [Fact]
        public void TheLedgerSumsItsReleaseOverALifetime()
        {
            // Organism.Lifetime accumulates ledgers with operator+; a term that does not survive
            // the addition would read zero for every creature in a lineage dissection.
            var config = new RunConfig { ExudationFraction = 0.1f };
            Phenotype body = Body(CellTypeIds.Photosynthetic, config);

            EnergyLedger one = Metabolism.StepAt(body, config, 200f, 0f, 0f, 1f);
            EnergyLedger three = one + one + one;

            Fixtures.AssertClose(3f * one.Exuded, three.Exuded, 1e-6f);
            Fixtures.AssertClose(3f * one.Net, three.Net, 1e-5f);
            Assert.Contains("~", three.ToString());
            _output.WriteLine(three.ToString());
        }

        [Theory]
        [InlineData(-0.01f)]
        [InlineData(1.01f)]
        [InlineData(7.5f)]
        public void AFractionOutsideZeroToOneIsRefusedRatherThanClamped(float bad)
        {
            // §9's rule, applied to the knob rather than to the file: a producer releasing more
            // than it earns from light, or drawing energy out of the field for nothing, is not a
            // world anybody meant to ask for. Refused at the setter, so it holds for a
            // hand-edited config.json and for EVOSIM_EXUDATION alike.
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new RunConfig { ExudationFraction = bad });

            _output.WriteLine(e.Message);
        }

        [Fact]
        public void AFractionOutsideZeroToOneIsRefusedWhenAFileCarriesIt()
        {
            // The same guard reached through the surface a person actually edits.
            string text = RunConfigJson.Write(new RunConfig { ExudationFraction = 0.15f })
                .Replace("\"exudationFraction\": 0.15", "\"exudationFraction\": 1.5");

            Exception e = Assert.ThrowsAny<Exception>(() => RunConfigJson.Read(text));
            _output.WriteLine(e.ToString());

            Assert.True(
                e is ArgumentOutOfRangeException || e.InnerException is ArgumentOutOfRangeException,
                $"expected the range to be refused, got {e.GetType().Name}");
        }

        [Fact]
        public void TheFractionReachesTheHashAndSurvivesAFile()
        {
            // Belt and braces beside the two reflection guards: a run at 0.15 and a run at 0 must
            // be distinguishable after the fact, and the file must carry the difference.
            var off = new RunConfig();
            var on = new RunConfig { ExudationFraction = 0.15f };

            Assert.NotEqual(off.Hash(), on.Hash());

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(on), out string mismatch);

            Assert.Null(mismatch);
            Assert.Equal(on.Hash(), back.Hash());
            Fixtures.AssertClose(0.15f, back.ExudationFraction, 0f);
        }

        private static Phenotype Body(string cellTypeId, RunConfig config)
        {
            var genome = new Genome
            {
                RootIndex = 0,
                Reproduction = new ReproductionTraits { BroodSize = 1, OffspringEndowment = 100f },
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

            return Developer.Develop(genome, config.Development, null, config.Shapes);
        }
    }
}

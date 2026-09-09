using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Brood size and the birth investment as evolved traits — DESIGN.md §5A.6 and
    /// fable-propose-growth.md rule 2.
    /// </summary>
    public class ReproductionTraitsTests
    {
        private readonly ITestOutputHelper _output;

        public ReproductionTraitsTests(ITestOutputHelper output) => _output = output;

        private const float Overhead = 20f;
        private const float Tissue = 800f;

        [Fact]
        public void BroodSizeIsNotNeutralOnceThereIsAnOverhead()
        {
            // The reason two numbers are evolved rather than one. The investment is spent whole
            // however many ways it is divided, so without a per-head term a brood of four and a
            // brood of one would cost the same and brood size would select for nothing. The
            // overhead is what separates them, so it is worth a test that would fail if someone
            // simplified it away.
            var manyFeeble = new ReproductionTraits { BroodSize = 4, BirthInvestment = 0.5f };
            var oneRich = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f };

            float many = manyFeeble.CostJoules(Tissue, Overhead);
            float one = oneRich.CostJoules(Tissue, Overhead);

            _output.WriteLine($"4 x 0.125 of a {Tissue} J body -> {many} J, 1 x 0.5 -> {one} J " +
                              $"(overhead {Overhead})");

            Assert.True(many > one,
                "same energy into offspring, but four of them should cost more to make");
        }

        [Fact]
        public void WithNoOverheadTheTwoStrategiesAreIndistinguishable()
        {
            // Stated as a test rather than a comment: this is the degenerate case the overhead
            // exists to avoid, and it should be visible that it is degenerate.
            var manyFeeble = new ReproductionTraits { BroodSize = 8, BirthInvestment = 0.5f };
            var oneRich = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f };

            Fixtures.AssertClose(
                manyFeeble.CostJoules(Tissue, 0f), oneRich.CostJoules(Tissue, 0f), 1e-4f);
        }

        [Fact]
        public void CostRisesWithBothTraitsAndWithTheBodySpendingIt()
        {
            var baseline = new ReproductionTraits { BroodSize = 2, BirthInvestment = 0.5f };
            var biggerBrood = new ReproductionTraits { BroodSize = 3, BirthInvestment = 0.5f };
            var richerOffspring = new ReproductionTraits { BroodSize = 2, BirthInvestment = 0.75f };

            Assert.True(biggerBrood.CostJoules(Tissue, Overhead) > baseline.CostJoules(Tissue, Overhead));
            Assert.True(richerOffspring.CostJoules(Tissue, Overhead) > baseline.CostJoules(Tissue, Overhead));

            // And the third input, which is what makes the investment a fraction rather than a
            // number of joules: the same strategy costs a big body more than a small one, so a
            // lineage that grows does not have to re-evolve the decision it already had.
            Assert.True(
                baseline.CostJoules(2f * Tissue, Overhead) > baseline.CostJoules(Tissue, Overhead));
        }

        [Fact]
        public void ABroodOfNoneFailsValidation()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Fixtures.Box());
            g.Reproduction = new ReproductionTraits { BroodSize = 0, BirthInvestment = 0.5f };

            Assert.Contains(g.Validate(), i => i.Contains("Brood size"));
        }

        [Fact]
        public void AnOffspringThatPaysItsParentFailsValidation()
        {
            // A negative investment would make reproduction a net energy gain, which is the free
            // lunch §11.2 exists to catch — and one a search would find within a few generations.
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Fixtures.Box());
            g.Reproduction = new ReproductionTraits { BroodSize = 2, BirthInvestment = -0.1f };

            Assert.Contains(g.Validate(), i => i.Contains("investment"));
        }

        [Fact]
        public void ABodyPlanWithNoSizeFailsValidation()
        {
            // The adult scale multiplies every node's dimensions, so zero is not a small creature
            // and a negative one reflects the whole body through the origin without saying so.
            var g = new Genome { RootIndex = 0, AdultScale = 0f };
            g.Nodes.Add(Fixtures.Box());

            Assert.Contains(g.Validate(), i => i.Contains("Adult scale"));
        }

        [Fact]
        public void RandomGenomesCarryUsableReproductionTraits()
        {
            int totalBrood = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                Genome g = GenomeFactory.Random(new Rng(seed));

                Assert.Empty(g.Validate());
                Assert.True(g.Reproduction.BroodSize >= 1);
                Assert.True(g.Reproduction.BirthInvestment > 0f);
                Assert.True(g.AdultScale > 0f);

                totalBrood += g.Reproduction.BroodSize;
            }

            _output.WriteLine($"mean brood over 200 seeds: {totalBrood / 200f:0.##}");
        }

        [Fact]
        public void ReproductionTraitsSurviveCloning()
        {
            // Clone is what mutation copies through, so a trait it drops would silently reset to
            // the default on every reproduction and never evolve at all.
            var g = new Genome { RootIndex = 0, AdultScale = 1.75f };
            g.Nodes.Add(Fixtures.Box());
            g.Reproduction = new ReproductionTraits { BroodSize = 7, BirthInvestment = 0.33f };

            Genome clone = g.Clone();

            Assert.Equal(7, clone.Reproduction.BroodSize);
            Fixtures.AssertClose(0.33f, clone.Reproduction.BirthInvestment, 1e-6f);
            Fixtures.AssertClose(1.75f, clone.AdultScale, 1e-6f);
        }
    }
}

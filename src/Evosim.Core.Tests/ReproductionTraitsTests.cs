using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Brood size and offspring endowment as evolved traits — DESIGN.md §5A.6.
    /// </summary>
    public class ReproductionTraitsTests
    {
        private readonly ITestOutputHelper _output;

        public ReproductionTraitsTests(ITestOutputHelper output) => _output = output;

        private const float Overhead = 20f;

        [Fact]
        public void BroodSizeIsNotNeutralOnceThereIsAnOverhead()
        {
            // The reason two numbers are evolved rather than one. With cost strictly proportional
            // to total energy invested, one brood of four and four broods of one cost the same
            // and brood size selects for nothing. The per-offspring overhead is what separates
            // them, so it is worth a test that would fail if someone simplified it away.
            var manyFeeble = new ReproductionTraits { BroodSize = 4, OffspringEndowment = 100f };
            var oneRich = new ReproductionTraits { BroodSize = 1, OffspringEndowment = 400f };

            float many = manyFeeble.CostJoules(Overhead);
            float one = oneRich.CostJoules(Overhead);

            _output.WriteLine($"4 x 100 J -> {many} J, 1 x 400 J -> {one} J (overhead {Overhead})");

            Assert.True(many > one,
                "same energy into offspring, but four of them should cost more to make");
        }

        [Fact]
        public void WithNoOverheadTheTwoStrategiesAreIndistinguishable()
        {
            // Stated as a test rather than a comment: this is the degenerate case the overhead
            // exists to avoid, and it should be visible that it is degenerate.
            var manyFeeble = new ReproductionTraits { BroodSize = 8, OffspringEndowment = 50f };
            var oneRich = new ReproductionTraits { BroodSize = 1, OffspringEndowment = 400f };

            Fixtures.AssertClose(manyFeeble.CostJoules(0f), oneRich.CostJoules(0f), 1e-4f);
        }

        [Fact]
        public void CostRisesWithBothTraits()
        {
            var baseline = new ReproductionTraits { BroodSize = 2, OffspringEndowment = 100f };
            var biggerBrood = new ReproductionTraits { BroodSize = 3, OffspringEndowment = 100f };
            var richerOffspring = new ReproductionTraits { BroodSize = 2, OffspringEndowment = 150f };

            Assert.True(biggerBrood.CostJoules(Overhead) > baseline.CostJoules(Overhead));
            Assert.True(richerOffspring.CostJoules(Overhead) > baseline.CostJoules(Overhead));
        }

        [Fact]
        public void ABroodOfNoneFailsValidation()
        {
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Fixtures.Box());
            g.Reproduction = new ReproductionTraits { BroodSize = 0, OffspringEndowment = 100f };

            Assert.Contains(g.Validate(), i => i.Contains("Brood size"));
        }

        [Fact]
        public void AnOffspringThatPaysItsParentFailsValidation()
        {
            // A negative endowment would make reproduction a net energy gain, which is the free
            // lunch §11.2 exists to catch — and one a search would find within a few generations.
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Fixtures.Box());
            g.Reproduction = new ReproductionTraits { BroodSize = 2, OffspringEndowment = -10f };

            Assert.Contains(g.Validate(), i => i.Contains("endowment"));
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
                Assert.True(g.Reproduction.OffspringEndowment > 0f);

                totalBrood += g.Reproduction.BroodSize;
            }

            _output.WriteLine($"mean brood over 200 seeds: {totalBrood / 200f:0.##}");
        }

        [Fact]
        public void ReproductionTraitsSurviveCloning()
        {
            // Clone is what mutation copies through, so a trait it drops would silently reset to
            // the default on every reproduction and never evolve at all.
            var g = new Genome { RootIndex = 0 };
            g.Nodes.Add(Fixtures.Box());
            g.Reproduction = new ReproductionTraits { BroodSize = 7, OffspringEndowment = 33f };

            Genome clone = g.Clone();

            Assert.Equal(7, clone.Reproduction.BroodSize);
            Fixtures.AssertClose(33f, clone.Reproduction.OffspringEndowment, 1e-6f);
        }
    }
}

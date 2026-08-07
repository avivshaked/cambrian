using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>Neural tissue — DESIGN.md §5A.1, and DECISIONS.md D019.</summary>
    public class NeuralCellTests
    {
        private readonly ITestOutputHelper _output;

        public NeuralCellTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void EveryOtherCellHostsNeuronsAtFullPrice()
        {
            // The baseline nerve net. If neural tissue were required before a part could carry
            // any neuron, a flagellate would need two mutations to gain a working tail — a link
            // and the tissue to drive it — each useless without the other. Populations do not
            // cross valleys that wide, so the baseline is what makes a tail reachable at all.
            foreach (string id in CellTypeRegistry.Standard.Ids())
            {
                if (id == CellTypeIds.Neural) continue;

                CellType type = CellTypeRegistry.Standard.Resolve(id);
                Assert.Equal(1f, type.NeuronCostMultiplier(10, 0.05f));
            }
        }

        [Fact]
        public void NeuralTissueDiscountsTheNeuronsItSupports()
        {
            var cell = new NeuralCell(neuronsSupportedPerCubicMetre: 400f, discountedCostFraction: 0.2f);

            // 0.05 m3 supports 20 neurons, so 10 are comfortably covered.
            Assert.Equal(0.2f, cell.NeuronCostMultiplier(10, 0.05f), 4);
        }

        [Fact]
        public void MoreTissueThanNeededBuysNothingFurther()
        {
            // There has to be an optimum size rather than a ceiling to press against. Tissue past
            // what the neurons need discounts nothing more and still pays upkeep, so the pressure
            // runs from both directions — which is the shape a cost should have.
            var cell = new NeuralCell();

            float justEnough = cell.NeuronCostMultiplier(10, 10f / 400f);
            float tenTimesMore = cell.NeuronCostMultiplier(10, 100f / 400f);

            Fixtures.AssertClose(justEnough, tenTimesMore, 1e-6f);
        }

        [Fact]
        public void TheDiscountIsAGradientAndNotAThreshold()
        {
            // §2's central worry is that this search is bad at thresholds. A step function would
            // make brain size something to find; a slope makes it something to climb.
            var cell = new NeuralCell(neuronsSupportedPerCubicMetre: 400f, discountedCostFraction: 0.2f);

            float previous = float.MaxValue;
            _output.WriteLine("| volume m3 | supported | cost x |");
            _output.WriteLine("|---|---|---|");

            for (int step = 0; step <= 10; step++)
            {
                float volume = step * 0.005f;
                float multiplier = cell.NeuronCostMultiplier(10, volume);

                _output.WriteLine($"| {volume:0.###} | {volume * 400f:0.#} | {multiplier:0.###} |");

                Assert.True(multiplier <= previous + 1e-6f, "cost must never rise with more tissue");
                previous = multiplier;
            }

            // No tissue at all is full price; enough tissue is the full discount.
            Assert.Equal(1f, cell.NeuronCostMultiplier(10, 0f), 4);
            Assert.Equal(0.2f, cell.NeuronCostMultiplier(10, 1f), 4);
        }

        [Fact]
        public void ABrainNeverCostsMoreThanNoBrain()
        {
            // The one thing the blend must never do. A multiplier above 1 would mean growing
            // neural tissue made thinking more expensive, and the trait could never establish.
            var cell = new NeuralCell();

            for (int neurons = 1; neurons <= 200; neurons += 7)
            {
                for (int v = 0; v <= 50; v++)
                {
                    float multiplier = cell.NeuronCostMultiplier(neurons, v * 0.002f);
                    Assert.InRange(multiplier, cell.DiscountedCostFraction, 1f);
                }
            }
        }

        [Fact]
        public void NeuralTissueEarnsNothingAndStillCosts()
        {
            var cell = new NeuralCell();

            Assert.Equal(0f, cell.Acquire(new CellContext(1f, 0.05f)));
            Assert.True(cell.Upkeep(0.05f, 1f) > 0f);

            // Nervous tissue is among the most expensive an animal carries, and a brain that
            // cost little would grow without limit for the same reason a free part would.
            CellType structural = CellTypeRegistry.Standard.Resolve(CellTypeIds.Structural);
            Assert.True(cell.UpkeepWattsPerCubicMetre > structural.UpkeepWattsPerCubicMetre);
        }

        [Fact]
        public void TissueThatSupportsNothingOrDiscountsNothingIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeuralCell(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeuralCell(400f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NeuralCell(400f, 1.5f));
        }

        [Fact]
        public void ItIsInTheRegistryTheHashAndTheSerializer()
        {
            Assert.True(CellTypeRegistry.Standard.Contains(CellTypeIds.Neural));

            var a = new RunConfig { CellTypes = new CellTypeRegistry(new NeuralCell(400f)) };
            var b = new RunConfig { CellTypes = new CellTypeRegistry(new NeuralCell(800f)) };
            Assert.NotEqual(a.Hash(), b.Hash());

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(a), out string mismatch);
            Assert.Null(mismatch);

            var restored = (NeuralCell)back.CellTypes.Resolve(CellTypeIds.Neural);
            Assert.Equal(400f, restored.NeuronsSupportedPerCubicMetre, 3);
        }

        [Fact]
        public void MutationCanReachNeuralTissueFromAFounder()
        {
            // Founders draw from the earning types only, so no founder is born with a brain.
            // It has to be discoverable, or the trait exists on paper and never in a world.
            bool everSeen = false;

            for (ulong lineage = 1; lineage <= 40 && !everSeen; lineage++)
            {
                Genome g = GenomeFactory.Founder(new Rng(lineage));

                for (ulong birth = 1; birth <= 400; birth++)
                {
                    g = Mutator.Mutate(g, new Rng(lineage * 10000 + birth));

                    foreach (MorphNode node in g.Nodes)
                    {
                        if (node.CellTypeId == CellTypeIds.Neural) everSeen = true;
                    }

                    if (everSeen) { _output.WriteLine($"neural tissue at lineage {lineage}, birth {birth}"); break; }
                }
            }

            Assert.True(everSeen, "no lineage ever mutated into neural tissue");
        }
    }
}

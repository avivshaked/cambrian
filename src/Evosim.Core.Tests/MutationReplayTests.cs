using System.Diagnostics;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// How fast is it to reconstruct a creature by replaying its ancestry? — DESIGN.md §9.
    /// </summary>
    /// <remarks>
    /// Storing a birth as <c>(parent, seed)</c> costs about two dozen bytes against ~5 KB for a
    /// whole genome, but it moves the cost from disk to reconstruction: getting a creature back
    /// means replaying every mutation since the last keyframe. These measure that price, because
    /// it is what decides how far apart keyframes can sit.
    /// </remarks>
    public class MutationReplayTests
    {
        private readonly ITestOutputHelper _output;

        public MutationReplayTests(ITestOutputHelper output) => _output = output;

        private static Genome Start() => GenomeFactory.RandomViable(
            new Rng(1), RandomGenomeOptions.Default, DevelopmentLimits.Default, minParts: 3);

        [Fact]
        public void ReplayCostAndGenomeGrowthAlongALineage()
        {
            const int chain = 100_000;
            var rates = MutationRates.Default;

            Genome g = Start();
            var clock = Stopwatch.StartNew();

            _output.WriteLine("| births | elapsed ms | nodes | neurons |");
            _output.WriteLine("|---|---|---|---|");

            for (ulong step = 1; step <= chain; step++)
            {
                g = Mutator.Mutate(g, new Rng(step), rates);

                if (step == 1_000 || step == 10_000 || step == 50_000 || step == chain)
                {
                    int neurons = g.GlobalBrain.Length;
                    foreach (MorphNode n in g.Nodes) neurons += n.Neurons.Length;

                    _output.WriteLine(
                        $"| {step} | {clock.ElapsedMilliseconds} | {g.Nodes.Count} | {neurons} |");
                }
            }

            clock.Stop();

            _output.WriteLine("");
            _output.WriteLine($"{chain} mutations in {clock.ElapsedMilliseconds} ms " +
                              $"({clock.Elapsed.TotalMilliseconds / chain * 1000:0.#} µs each)");

            // The regression guard, and the reason this test exists rather than a stopwatch run
            // once by hand. With add-node and remove-node both rolled per birth at 0.04 against
            // 0.03, this lineage grew to 847 nodes and the chain took 32 seconds — the cost is
            // quadratic, because every mutation walks every node.
            //
            // Removal is now extinction by shrinking (§4.5), so what bounds genome size is the
            // balance between duplication and drift across the extinction threshold. Nothing
            // declares a size; unexpressed nodes feel no selection, drift, and fall out.
            //
            // Asserted loosely, and it has to be. The number is an equilibrium of a random walk,
            // so it fluctuates; pinning it would make this fail on any rate change rather than on
            // the thing it watches for. It is deliberately far above the ~40 fixed point, because
            // a tight bar here would be this test quietly deciding how large a genome may be —
            // which is the exact mistake the rate itself was set to avoid. What must not happen
            // is unbounded drift, and 847 was unbounded drift.
            Assert.True(g.Nodes.Count < 200,
                $"genome grew to {g.Nodes.Count} nodes over {chain} births — size is drifting " +
                "rather than sitting at an equilibrium, and replay cost goes quadratic with it");
        }

        [Fact]
        public void ReplayingTheSameChainGivesTheSameCreature()
        {
            // The property the whole storage scheme depends on. If a chain replayed twice in the
            // same build could differ, a stored lineage would not describe anything.
            const int chain = 5_000;

            Genome a = Start(), b = Start();
            for (ulong step = 1; step <= chain; step++)
            {
                a = Mutator.Mutate(a, new Rng(step));
                b = Mutator.Mutate(b, new Rng(step));
            }

            Assert.Equal(GenomeJson.Write(a), GenomeJson.Write(b));
        }
    }
}

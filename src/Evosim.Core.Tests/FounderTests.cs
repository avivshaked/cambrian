using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>Generation zero — DESIGN.md §5A.0b.</summary>
    public class FounderTests
    {
        private readonly ITestOutputHelper _output;

        public FounderTests(ITestOutputHelper output) => _output = output;

        private const int Seeds = 500;

        [Fact]
        public void AFounderIsOneOrTwoPartsAndNothingElse()
        {
            var sizes = new Dictionary<int, int>();

            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                Genome g = GenomeFactory.Founder(new Rng(seed));
                Assert.Empty(g.Validate());

                Phenotype p = Developer.Develop(g);
                Assert.InRange(p.PartCount, 1, 2);
                Assert.False(p.WasTruncated);

                sizes.TryGetValue(p.PartCount, out int n);
                sizes[p.PartCount] = n + 1;
            }

            foreach (var kv in sizes) _output.WriteLine($"{kv.Key} part(s): {kv.Value}");

            // Both openings have to actually occur, or FounderTailChance is not doing anything
            // and the world starts as one strategy rather than two.
            Assert.True(sizes.ContainsKey(1) && sizes[1] > Seeds / 4, "blobs are too rare");
            Assert.True(sizes.ContainsKey(2) && sizes[2] > Seeds / 4, "flagellates are too rare");
        }

        [Fact]
        public void EveryFounderCanTryToEat()
        {
            // The one guarantee generation zero gets. Structural and Link acquire nothing, so a
            // founder built from them alone starves with certainty in every world — compute
            // spent to produce a corpse. Whether it *does* eat is the world's business; that it
            // has some way to is ours.
            var registry = CellTypeRegistry.Standard;

            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                Genome g = GenomeFactory.Founder(new Rng(seed));

                bool earns = false;
                foreach (MorphNode node in g.Nodes)
                {
                    CellType type = registry.Resolve(node.CellTypeId);
                    if (type.Id != CellTypeIds.Structural && type.Id != CellTypeIds.Link) earns = true;
                }

                Assert.True(earns, $"seed {seed}: founder has no way to acquire energy at all");
            }
        }

        [Fact]
        public void TheRootIsAlwaysTheEarningCellAndTheTailIsAlwaysTheLink()
        {
            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                Genome g = GenomeFactory.Founder(new Rng(seed));

                Assert.NotEqual(CellTypeIds.Link, g.Nodes[g.RootIndex].CellTypeId);
                Assert.NotEqual(CellTypeIds.Structural, g.Nodes[g.RootIndex].CellTypeId);

                if (g.Nodes.Count > 1) Assert.Equal(CellTypeIds.Link, g.Nodes[1].CellTypeId);
            }
        }

        [Fact]
        public void OnlyTheTwoPartFounderCanMove()
        {
            // A blob has no joint, so it cannot swim — and that is not a defect to filter out.
            // Under §5A stillness is a way of living, not a failure, and a photosynthetic blob
            // that pays its bills sitting in the light is a plant.
            int still = 0, mobile = 0;

            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                Phenotype p = Developer.Develop(GenomeFactory.Founder(new Rng(seed)));

                if (p.PartCount == 1) { Assert.Equal(0, p.TotalDof); still++; }
                else { Assert.True(p.TotalDof > 0, "a founder with a link should have a joint"); mobile++; }
            }

            _output.WriteLine($"{still} immobile, {mobile} able to swim");
        }

        [Fact]
        public void EveryEarningStrategyIsRepresented()
        {
            var counts = new Dictionary<string, int>();

            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                string id = GenomeFactory.Founder(new Rng(seed)).Nodes[0].CellTypeId;
                counts.TryGetValue(id, out int n);
                counts[id] = n + 1;
            }

            foreach (var kv in counts) _output.WriteLine($"{kv.Key,-16} {kv.Value} ({(float)kv.Value / Seeds:P1})");

            // Photosynthesis is weighted double because at t=0 it is the only strategy with
            // anything to eat. The other two are expected to starve and become the nutrient
            // pool that makes them viable a generation later, so they must be present — a world
            // seeded with plants alone has no primordial soup and no reason for anything else
            // to ever appear.
            Assert.True(counts.ContainsKey(CellTypeIds.Photosynthetic));
            Assert.True(counts.ContainsKey(CellTypeIds.Absorptive));
            Assert.True(counts.ContainsKey(CellTypeIds.Consumer));
            Assert.False(counts.ContainsKey(CellTypeIds.Structural));

            Assert.InRange((float)counts[CellTypeIds.Photosynthetic] / Seeds, 0.4f, 0.6f);
        }

        [Fact]
        public void AFounderCarriesNoMorphologyItDidNotEarn()
        {
            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                Genome g = GenomeFactory.Founder(new Rng(seed));

                foreach (MorphNode node in g.Nodes)
                {
                    Assert.Equal(1, node.RecursiveLimit);

                    foreach (MorphEdge edge in node.Edges)
                    {
                        // Reflection is a bilateral pair, tilt is a pose, recursion is a limb.
                        // All three are morphology, and morphology is the thing generation zero
                        // is deliberately not given — every one of them has to be discovered.
                        Assert.Equal(Bool3.None, edge.Reflect);
                        Assert.False(edge.TerminalOnly);
                        Fixtures.AssertClose(1f, edge.Orientation.W, 1e-5f);
                    }
                }
            }
        }

        [Fact]
        public void FoundersDoNotOverlapThemselves()
        {
            // Two parts meeting face to face should not intersect. If they do, the solver pushes
            // them apart on the first step and that is momentum from nowhere (§11.2) — a free
            // launch handed to every creature in the world at t=0.
            float worst = 0f;

            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                Phenotype p = Developer.Develop(GenomeFactory.Founder(new Rng(seed)));
                if (p.PartCount < 2) continue;

                Assert.Equal(0, PhenotypeGeometry.BuriedPartPairs(p));
                worst = System.Math.Max(
                    worst, PhenotypeGeometry.MeasureOverlap(p, samplesPerAxis: 4).UnjointedFraction);
            }

            _output.WriteLine($"worst unjointed overlap: {worst:P2}");
            Assert.Equal(0f, worst);
        }

        [Fact]
        public void MutationCanGrowAFounderIntoSomethingBigger()
        {
            // The claim the whole design rests on: if founders are this small, complexity has to
            // be reachable by mutation alone or the world never becomes interesting.
            Genome g = GenomeFactory.Founder(new Rng(1));
            int startNodes = g.Nodes.Count;

            int mostParts = Developer.Develop(g).PartCount;
            for (ulong birth = 1; birth <= 2000; birth++)
            {
                g = Mutator.Mutate(g, new Rng(birth));
                mostParts = System.Math.Max(mostParts, Developer.Develop(g).PartCount);
            }

            _output.WriteLine(
                $"{startNodes} nodes -> {g.Nodes.Count} after 2000 births; largest body seen {mostParts} parts");

            Assert.True(g.Nodes.Count > startNodes, "the genome never grew at all");
            Assert.True(mostParts >= 4, $"nothing bigger than {mostParts} parts was ever reachable");
        }
    }
}

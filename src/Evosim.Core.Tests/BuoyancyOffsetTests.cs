using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D111, the buoyancy offset, on Core's side: the gene, its file, its refusal where nothing
    /// prices it, its mutation, its distance, its part and its bill. The torque is the solver's
    /// and is tested there (<c>Evosim.Dynamics.Tests.BuoyancyOffsetTorqueTests</c>).
    /// </summary>
    public class BuoyancyOffsetTests
    {
        private readonly ITestOutputHelper _output;

        public BuoyancyOffsetTests(ITestOutputHelper output) => _output = output;

        private static Genome Leaf(float offset, float hx = 0.5f, float hy = 0.045f, float hz = 0.5f)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f };
            MorphNode node = Fixtures.Box();
            node.CellTypeId = CellTypeIds.Photosynthetic;
            node.Dimensions = new Float3(hx, hy, hz);
            node.BuoyancyOffset = offset;
            genome.Nodes.Add(node);
            return genome;
        }

        // ------------------------------------------------------------------ the file

        [Fact]
        public void TheOffsetIsWrittenReadAndAFormatSevenGenomeIsRefusedByName()
        {
            Genome g = Leaf(-0.375f);
            string text = GenomeJson.Write(g);

            Assert.Contains("\"buoyancyOffset\":-0.375", text);
            // D111 took the format to 8; D120's gestation gene took it to 9 the next day. What
            // this test pins is that a format-7 file is refused by name, not the number itself.
            Assert.True(GenomeJson.FormatVersion >= 8);
            Assert.Equal(-0.375f, GenomeJson.Read(text).Nodes[0].BuoyancyOffset);

            // §9: the missing field refused, never defaulted, and the message says what it was.
            string old = text.Replace($"\"format\":{GenomeJson.FormatVersion}", "\"format\":7");
            FormatException e = Assert.Throws<FormatException>(() => GenomeJson.Read(old));
            _output.WriteLine(e.Message);
            Assert.Contains("BuoyancyOffset", e.Message);

            string missing = text.Replace(",\"buoyancyOffset\":-0.375", "");
            Assert.NotEqual(text, missing);
            Assert.ThrowsAny<Exception>(() => GenomeJson.Read(missing));
        }

        [Fact]
        public void AFounderFloatsFromItsCentre()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                foreach (MorphNode node in GenomeFactory.Random(new Rng(seed)).Nodes)
                {
                    Assert.Equal(0f, node.BuoyancyOffset);
                }
            }
        }

        // ------------------------------------------------------------------ the refusal

        [Fact]
        public void ValidationAsksTheRangeAlwaysAndThePriceWhereTheWorldIsKnown()
        {
            Assert.Empty(Leaf(0.5f).Validate());
            Assert.Empty(Leaf(0.5f).Validate(null, buoyancyOffsetPriced: true));
            Assert.Empty(Leaf(0f).Validate(null, buoyancyOffsetPriced: false));

            Assert.Contains(
                Leaf(0.5f).Validate(null, buoyancyOffsetPriced: false),
                issue => issue.Contains("does not price it"));

            Assert.NotEmpty(Leaf(1.01f).Validate());
            Assert.NotEmpty(Leaf(-1.01f).Validate());
            Assert.NotEmpty(Leaf(float.NaN).Validate());
        }

        [Fact]
        public void AWorldThatDoesNotPriceTheOffsetRefusesAnInoculantCarryingOne()
        {
            var config = new RunConfig { MinimumPopulation = 0 };
            var world = new World(config, seed: 3);

            ArgumentException e = Assert.Throws<ArgumentException>(
                () => world.Inoculate(Leaf(0.5f), count: 1, heightY: -1f));
            _output.WriteLine(e.Message);
            Assert.Contains("D111", e.Message);
            Assert.Empty(world.Living);

            var priced = new RunConfig { MinimumPopulation = 0, BuoyancyOffsetWattsPerCubicMetre = 0.02f };
            var open = new World(priced, seed: 3);
            open.Inoculate(Leaf(0.5f), count: 1, heightY: -1f);
            Assert.Single(open.Living);
        }

        [Fact]
        public void ANegativePriceIsRefused()
        {
            var config = new RunConfig { BuoyancyOffsetWattsPerCubicMetre = -0.01f };
            Assert.Throws<ArgumentException>(() => new World(config, seed: 3));
        }

        // ------------------------------------------------------------------ mutation

        [Fact]
        public void UnpricedTheMutatorDrawsNothingForItAndPricedItWalksTheRange()
        {
            // Unpriced, the stream a birth takes is the one it took before the gene: the same
            // parent and seed give byte-identical children whether or not the argument is named.
            for (ulong seed = 1; seed <= 30; seed++)
            {
                Genome parent = GenomeFactory.Random(new Rng(seed));
                string plain = GenomeJson.Write(Mutator.Mutate(parent, new Rng(seed + 1000)));
                string named = GenomeJson.Write(
                    Mutator.Mutate(parent, new Rng(seed + 1000), buoyancyOffsetPriced: false));
                Assert.Equal(plain, named);
                Assert.DoesNotContain("\"buoyancyOffset\":0.", plain);
            }

            // Priced, a lineage leaves 0, stays in the range and reaches both signs.
            Genome line = GenomeFactory.Random(new Rng(77));
            float lowest = 0f, highest = 0f;
            for (int birth = 0; birth < 400; birth++)
            {
                line = Mutator.Mutate(line, new Rng(9000UL + (ulong)birth), buoyancyOffsetPriced: true);
                foreach (MorphNode node in line.Nodes)
                {
                    Assert.InRange(node.BuoyancyOffset, -1f, 1f);
                    lowest = Math.Min(lowest, node.BuoyancyOffset);
                    highest = Math.Max(highest, node.BuoyancyOffset);
                }
            }

            _output.WriteLine($"over 400 births: offset {lowest:0.###} to {highest:0.###}");
            Assert.True(lowest < -0.05f && highest > 0.05f, $"the offset stayed in {lowest}..{highest}");
        }

        // ------------------------------------------------------------------ distance

        [Fact]
        public void SpeciesDistanceCountsTheOffsetAndReadsTwoCentresAsNothing()
        {
            Genome a = Leaf(0f), b = Leaf(0f), c = Leaf(0.5f), d = Leaf(-0.5f);

            // The parameter term alone.
            float Between(Genome x, Genome y) => SpeciesDistance.Between(x, y, 0f, 0f, 1f, 0f);

            Assert.Equal(0f, Between(a, b));
            Assert.Equal(0.25f, Between(a, c));
            Assert.Equal(0.5f, Between(c, d));
        }

        // ------------------------------------------------------------------ the part

        [Fact]
        public void ThePartCarriesTheOffsetThroughGrowthAndNamesItsThinAxis()
        {
            PhenotypePart leaf = Developer.Develop(Leaf(0.5f)).Parts[0];
            Assert.Equal(0.5f, leaf.BuoyancyOffset);
            Assert.Equal(1, leaf.ThinAxis);

            PhenotypePart grown = Developer.Develop(Leaf(0.5f)).Scaled(0.3f).Parts[0];
            Assert.Equal(0.5f, grown.BuoyancyOffset);
            Assert.Equal(1, grown.ThinAxis);

            Assert.Equal(2, Developer.Develop(Leaf(0.5f, 0.4f, 0.3f, 0.1f)).Parts[0].ThinAxis);
            Assert.Equal(0, Developer.Develop(Leaf(0.5f, 0.2f, 0.2f, 0.2f)).Parts[0].ThinAxis);

            Genome ball = Leaf(0.5f);
            ball.Nodes[0].ShapeId = ShapeIds.Sphere;
            Assert.Equal(-1, Developer.Develop(ball).Parts[0].ThinAxis);
        }

        // ------------------------------------------------------------------ the bill

        [Fact]
        public void ThePriceIsChargedOnVolumeAndOffsetAndWornWithTheRest()
        {
            Phenotype body = Developer.Develop(Leaf(-0.5f));
            float volume = body.Parts[0].Volume;

            // A price far above the proposal's 0.02, so the term stands well clear of the float
            // rounding of the rest of the bill it is added to.
            const float price = 20f;
            var free = new RunConfig();
            var priced = new RunConfig { BuoyancyOffsetWattsPerCubicMetre = price };

            EnergyLedger a = Metabolism.StepAt(body, free, 0f, 0f, 0f, 0f, seconds: 2f);
            EnergyLedger b = Metabolism.StepAt(body, priced, 0f, 0f, 0f, 0f, seconds: 2f);

            double expected = price * 0.5 * volume * 2.0;
            _output.WriteLine($"upkeep {a.Upkeep} J free, {b.Upkeep} J priced, term {expected:0.######} J");
            Assert.InRange((double)b.Upkeep - a.Upkeep, expected * 0.999, expected * 1.001);
            Assert.Equal(price * 0.5f * volume, Metabolism.BuoyancyOffsetWatts(body.Parts[0], priced));

            // Worn: senescence multiplies the new term with the rest, twice at one doubling time.
            var old = new RunConfig { BuoyancyOffsetWattsPerCubicMetre = price, SenescenceDoublingSeconds = 1000f };
            var oldFree = new RunConfig { SenescenceDoublingSeconds = 1000f };
            EnergyLedger c = Metabolism.StepAt(body, old, 0f, 0f, 0f, 0f, seconds: 2f, ageSeconds: 1000f);
            EnergyLedger e = Metabolism.StepAt(body, oldFree, 0f, 0f, 0f, 0f, seconds: 2f, ageSeconds: 1000f);
            Assert.InRange((double)c.Upkeep - e.Upkeep, 2.0 * expected * 0.999, 2.0 * expected * 1.001);
        }

        [Fact]
        public void AtPriceZeroTheBillIsTheRecordedOneToTheBit()
        {
            // A body at offset 0 under a price and any body without one pay what they always did:
            // the term is not computed, so not even a zero is added.
            Genome g = GenomeFactory.Random(new Rng(5));
            Phenotype body = Developer.Develop(g);
            var config = new RunConfig();

            EnergyLedger a = Metabolism.StepAt(body, config, 30f, 1f, 1f, 0.5f, seconds: 0.5f, ageSeconds: 200f);
            var pricedAtZeroOffset = new RunConfig { BuoyancyOffsetWattsPerCubicMetre = 0.02f };
            EnergyLedger b = Metabolism.StepAt(
                body, pricedAtZeroOffset, 30f, 1f, 1f, 0.5f, seconds: 0.5f, ageSeconds: 200f);

            Assert.Equal(BitConverter.SingleToInt32Bits(a.Upkeep), BitConverter.SingleToInt32Bits(b.Upkeep));
            Assert.Equal(a.Net, b.Net);
        }
    }
}

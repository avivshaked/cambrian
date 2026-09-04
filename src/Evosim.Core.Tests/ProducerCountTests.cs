using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="World.CountPhotosynthetic"/> — the producer count behind the report's
    /// <c>photo</c> column (the Sol/GPT review of 2026-09-03, finding 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The report had <c>absorpt</c> and <c>inherit</c> for the stomachs and nothing at all for
    /// the leaves, so every trophic ratio it invited a reader to take had an unwritten
    /// denominator. These tests pin the denominator, at the level the report reads it — the
    /// world's own count, not a table a run had to be launched to produce.
    /// </para>
    /// <para>
    /// <b>Every world here has <c>MinimumPopulation = 0</c></b>, for the reason
    /// <see cref="AbsorptiveLogTests"/> records: the population floor draws founders of its own,
    /// and a world with the floor open would contain producers nobody put there — which is
    /// exactly the contamination the count exists to be read against.
    /// </para>
    /// </remarks>
    public class ProducerCountTests
    {
        private readonly ITestOutputHelper _output;

        public ProducerCountTests(ITestOutputHelper output) => _output = output;

        /// <summary>A pure stomach: one absorptive box, no joint, no neurons.</summary>
        private static Genome Stomach()
        {
            var g = new Genome();
            g.Nodes.Add(new MorphNode
            {
                CellTypeId = CellTypeIds.Absorptive,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });
            g.RootIndex = 0;
            return g;
        }

        /// <summary>The same body, photosynthetic — a leaf.</summary>
        private static Genome Plant()
        {
            Genome g = Stomach();
            g.Nodes[0].CellTypeId = CellTypeIds.Photosynthetic;
            return g;
        }

        private static RunConfig EmptyWorld() => new RunConfig
        {
            MinimumPopulation = 0,
            MaximumPopulation = 100_000,
            Light = new LightModel(20f, 12f),
        };

        [Fact]
        public void AWorldOfLeavesCountsEveryLivingCreatureAsAProducer()
        {
            var world = new World(EmptyWorld(), seed: 11);

            world.Inoculate(Plant(), count: 6, heightY: -10f);
            Assert.Equal(6, world.Living.Count);

            world.Step(1f);

            _output.WriteLine($"alive {world.Living.Count}, photo {world.CountPhotosynthetic()}");
            Assert.Equal(world.Living.Count, world.CountPhotosynthetic());
        }

        [Fact]
        public void OneInoculatedStomachIsTheOneCreatureNotCounted()
        {
            RunConfig config = EmptyWorld();

            // Too poor to breed. At the default endowment every one of these bodies conceives on
            // the first step and the world doubles, so "alive − 1" becomes "alive − 2" — the
            // stomach's child is a stomach — and the assertion would be measuring reproduction
            // rather than the count. Same device AbsorptiveLogTests uses, for the same reason.
            config.FounderEnergyJoules = 10f;

            var world = new World(config, seed: 11);

            // Something for the stomach to eat, so it is alive at the sample rather than absent
            // from it — a count that happens to be right because the odd creature died would
            // assert nothing.
            world.Nutrients.Deposit(-10f, 20_000f, 0);

            world.Inoculate(Plant(), count: 6, heightY: -10f);
            world.Inoculate(Stomach(), count: 1, heightY: -10f);
            Assert.Equal(7, world.Living.Count);

            world.Step(1f);

            int alive = world.Living.Count;
            int photo = world.CountPhotosynthetic();

            _output.WriteLine($"alive {alive}, photo {photo}");
            Assert.Equal(alive - 1, photo);

            // And the one that is not counted is the one that was meant not to be: the count is
            // over developed phenotypes, so a body's own flag is the ground truth it must agree
            // with, creature by creature.
            int flagged = 0;
            foreach (Organism creature in world.Living)
            {
                if (creature.HasPhotosyntheticTissue) flagged++;
                else Assert.True(creature.HasAbsorptiveTissue, "the odd creature is the stomach");
            }

            Assert.Equal(flagged, photo);
        }

        [Fact]
        public void AnEmptyWorldHoldsNoProducers()
        {
            var world = new World(EmptyWorld(), seed: 11);

            // The floor is closed, so this stays true rather than being a statement about t=0.
            Assert.Empty(world.Living);
            Assert.Equal(0, world.CountPhotosynthetic());
        }
    }
}

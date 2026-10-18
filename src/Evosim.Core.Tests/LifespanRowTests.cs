using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Round 52's instrument for D128's heritable lifespan: a birth row carries the body's gene as
    /// <c>ls</c> in a world where the gene can differ between bodies, and in no other world, and a
    /// queued row carries it through a checkpoint.
    /// </summary>
    public class LifespanRowTests
    {
        private static RunConfig Stage(bool heritable)
        {
            var config = new RunConfig
            {
                MinimumPopulation = 12,
                MaximumPopulation = 2_000,
                WorldAreaSquareMetres = 100f,
                WorldDepthMetres = 20f,
                Light = new LightModel(64f, 12f),
                AgeHazardBaseSeconds = 3000f,
                AgeRepairExponent = heritable ? 1f : 0f,
            };

            if (heritable)
            {
                config.Mutation.LifespanChance = 0.08f;
                config.Genome.MinLifespan = 0.5f;
                config.Genome.MaxLifespan = 2f;
            }

            return config;
        }

        private static Genome Leaf(float lifespan)
        {
            Genome g = Fixtures.SingleLeaf();
            g.LifespanScale = lifespan;
            return g;
        }

        [Fact]
        public void ABirthRowCarriesTheGeneWhereItCanDiffer()
        {
            var world = new World(Stage(heritable: true), seed: 11);
            world.Inoculate(Leaf(1.5f), count: 3, heightY: -5f);

            var births = world.DrainLineageEvents().Where(e => e.Kind == LineageEventKind.Birth).ToList();
            Assert.Equal(3, births.Count);
            foreach (LineageEvent e in births)
            {
                Assert.Equal(1.5f, e.LifespanScale);
                Assert.Contains("\"ls\":1.5", e.ToJson());
            }

            // The floor's founders draw theirs from the range, and every row says what was drawn.
            for (int step = 0; step < 4; step++) world.Step(0.5f);
            var founders = world.DrainLineageEvents()
                .Where(e => e.Kind == LineageEventKind.Birth && e.Source == FounderSource.Floor).ToList();
            Assert.NotEmpty(founders);
            foreach (LineageEvent e in founders)
            {
                Assert.InRange(e.LifespanScale, 0.5f, 2f);
                Organism body = world.Living.FirstOrDefault(o => o.Id == e.Id);
                if (body != null) Assert.Equal(body.Genome.LifespanScale, e.LifespanScale);
            }
        }

        [Fact]
        public void NoRowCarriesItWhereEveryBodyIsTheWorldsBase()
        {
            var world = new World(Stage(heritable: false), seed: 11);
            world.Inoculate(Leaf(1f), count: 3, heightY: -5f);
            for (int step = 0; step < 4; step++) world.Step(0.5f);

            foreach (LineageEvent e in world.DrainLineageEvents())
            {
                Assert.True(float.IsNaN(e.LifespanScale));
                Assert.DoesNotContain("\"ls\":", e.ToJson());
            }
        }

        [Fact]
        public void AQueuedRowCarriesTheGeneThroughACheckpoint()
        {
            RunConfig config = Stage(heritable: true);
            var world = new World(config, seed: 13);
            world.Inoculate(Leaf(1.25f), count: 2, heightY: -5f);
            world.Step(0.5f);

            byte[] state;
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    w.Flush();
                }

                state = buffer.ToArray();
            }

            var restored = new World(config, seed: 13);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                restored.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            var before = world.DrainLineageEvents().Select(e => e.ToJson()).ToList();
            var after = restored.DrainLineageEvents().Select(e => e.ToJson()).ToList();
            Assert.Equal(before, after);
            Assert.Contains(after, row => row.Contains("\"ls\":1.25"));
        }
    }
}
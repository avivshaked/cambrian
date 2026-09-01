using System.Text;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The lineage-events instrument — pre-round-8, LITERATURE-REVIEW.md §9 item 9. Per-creature
    /// birth/death rows, queued by <see cref="World"/> and drained by a harness into
    /// <c>lineage.jsonl</c>.
    /// </summary>
    public class LineageEventTests
    {
        [Fact]
        public void BirthEventsCarryParentKindGenerationAndTraits()
        {
            var config = new RunConfig { MinimumPopulation = 20, MaximumPopulation = 400 };
            config.Light = new LightModel(4000f, 40f);
            var world = new World(config, seed: 1);

            // The founding step: every event here is a floor spawn with no parent.
            world.Step(1f);
            var firstStep = world.DrainLineageEvents();

            Assert.NotEmpty(firstStep);
            foreach (LineageEvent evt in firstStep)
            {
                Assert.Equal(LineageEventKind.Birth, evt.Kind);
                Assert.Equal(BirthKind.Floor, evt.BirthKind);
                Assert.Equal(-1, evt.ParentId);
                Assert.Equal(0, evt.GenerationDepth);

                string json = evt.ToJson();
                Assert.DoesNotContain('\n', json);
                Assert.NotNull(Json.Parse(json));
            }

            // Step until a real reproduction happens, and check that birth's own row against the
            // creature it describes — the same cross-check EvolutionRun.cs's report applies.
            bool foundReproduction = false;

            for (int i = 0; i < 2000 && !foundReproduction; i++)
            {
                world.Step(1f);

                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind != LineageEventKind.Birth || evt.BirthKind != BirthKind.Reproduction)
                    {
                        continue;
                    }

                    foundReproduction = true;
                    Assert.True(evt.ParentId >= 0, "a reproduction birth must name its parent");
                    Assert.True(evt.GenerationDepth >= 1, "a reproduction birth must be past generation 0");

                    // Cross-checked only while the child is still findable among the living — it
                    // may have starved before this loop reads it, and that is not this test's
                    // question.
                    Organism match = null;
                    foreach (Organism c in world.Living)
                    {
                        if (c.Id != evt.Id) continue;
                        match = c;
                        break;
                    }

                    if (match != null)
                    {
                        bool hasAbsorptive = false;
                        foreach (PhenotypePart part in match.Phenotype.Parts)
                        {
                            if (part.CellTypeId != CellTypeIds.Absorptive) continue;
                            hasAbsorptive = true;
                            break;
                        }

                        Assert.Equal(hasAbsorptive, evt.HasAbsorptive);
                        Assert.Equal(match.Phenotype.TotalDof > 0, evt.HasJoint);
                    }

                    break;
                }
            }

            Assert.True(foundReproduction, "no reproduction birth happened in this window to check");
        }

        [Fact]
        public void DeathEventsCarryTheStarvedCause()
        {
            // A dark world: nothing can earn, so everything that is not a fresh floor spawn runs
            // out of energy and dies — Organism.cs's DeathCause remark: "the only cause the
            // design has".
            var config = new RunConfig { MinimumPopulation = 20 };
            config.Light = new LightModel(1e-6f, 1f);
            var world = new World(config, seed: 1);

            bool foundDeath = false;

            for (int i = 0; i < 400 && !foundDeath; i++)
            {
                world.Step(1f);

                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind != LineageEventKind.Death) continue;

                    foundDeath = true;
                    Assert.Equal(DeathCause.Starved, evt.Cause);

                    string json = evt.ToJson();
                    Assert.DoesNotContain('\n', json);
                    Assert.NotNull(Json.Parse(json));
                    break;
                }
            }

            Assert.True(foundDeath, "nothing died in a world with no light");
        }

        [Fact]
        public void DrainLineageEventsReturnsEachEventExactlyOnce()
        {
            var world = new World(new RunConfig { MinimumPopulation = 20 });
            world.Step(1f);

            var first = world.DrainLineageEvents();
            Assert.NotEmpty(first);

            // A second drain with nothing new queued must come back empty — draining clears
            // rather than replaying.
            var second = world.DrainLineageEvents();
            Assert.Empty(second);
        }

        [Fact]
        public void DrainingLineageEventsNeverChangesTheWorldsTrajectory()
        {
            // Pure instrumentation, D057-style: the queue is written to and read from, never
            // consulted by anything World.Step does. A world whose events are drained every step
            // and one whose events are never drained at all must produce the identical sequence
            // of samples — bit-identical trajectories are what makes this safe to wire into the
            // report loop with no config knob at all.
            RunConfig Config() => new RunConfig
            {
                MinimumPopulation = 20,
                MaximumPopulation = 400,
                Light = new LightModel(4000f, 40f),
            };

            string Trajectory(bool drain)
            {
                var world = new World(Config(), seed: 9);
                var samples = new StringBuilder();

                try
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        world.Step(1f);
                        if (drain) world.DrainLineageEvents();
                        samples.AppendLine(WorldStats.Sample(world).ToJson());
                    }
                }
                catch (PopulationRunawayException e)
                {
                    samples.AppendLine($"runaway:{e.Population}@{e.ElapsedSeconds:0.#}");
                }

                return samples.ToString();
            }

            Assert.Equal(Trajectory(drain: false), Trajectory(drain: true));
        }
    }
}

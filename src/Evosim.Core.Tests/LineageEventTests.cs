using System;
using System.Collections.Generic;
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

                // Field order, not just presence: readers index lineage.jsonl by name, but a
                // diff of a new row against an old one should be one inserted field, so "pho"
                // sits between "jnt" and "pt" and stays there.
                int jnt = json.IndexOf("\"jnt\":", StringComparison.Ordinal);
                int pho = json.IndexOf("\"pho\":", StringComparison.Ordinal);
                int pt = json.IndexOf("\"pt\":", StringComparison.Ordinal);
                Assert.True(jnt >= 0 && pho > jnt, "a birth row carries \"pho\" after \"jnt\"");
                Assert.True(pt > pho, "a birth row carries \"pho\" before \"pt\"");
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

                        bool hasPhotosynthetic = false;
                        foreach (PhenotypePart part in match.Phenotype.Parts)
                        {
                            if (part.CellTypeId != CellTypeIds.Photosynthetic) continue;
                            hasPhotosynthetic = true;
                            break;
                        }

                        Assert.Equal(hasAbsorptive, evt.HasAbsorptive);
                        Assert.Equal(match.Phenotype.TotalDof > 0, evt.HasJoint);
                        Assert.Equal(hasPhotosynthetic, evt.HasPhotosynthetic);

                        // And against the creature's own flag, which is what the report's photo
                        // column counts — row and organism read one local in World.Admit, and
                        // this is the assertion that keeps it that way.
                        Assert.Equal(match.HasPhotosyntheticTissue, evt.HasPhotosynthetic);
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

                    // A death row says who left and why, and nothing about the body. The
                    // birth-only fields are absent rather than written as zeros, which is what
                    // lets a reader tell the two kinds of row apart by their keys alone.
                    Assert.DoesNotContain("\"pho\"", json);
                    Assert.DoesNotContain("\"abs\"", json);
                    break;
                }
            }

            Assert.True(foundDeath, "nothing died in a world with no light");
        }

        /// <summary>
        /// Both values of the flag, from two bodies that can develop only one way each. How many
        /// leaves a random world founds is the founding lottery's business, so a test asserting
        /// "some founder is photosynthetic" would be asserting the draw; two inoculants assert
        /// the flag.
        /// </summary>
        [Fact]
        public void InoculatedLeavesAndStomachsCarryTheirOwnPhotosyntheticFlag()
        {
            // MinimumPopulation 0, for the reason ProducerCountTests records: an open floor puts
            // founders nobody inoculated into the same drain, and the counts below would then be
            // reading the lottery rather than the two genomes.
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(20f, 12f),
            };

            var world = new World(config, seed: 11);
            world.Inoculate(Leaf(), count: 3, heightY: -10f);
            world.Inoculate(Stomach(), count: 2, heightY: -10f);
            Assert.Equal(5, world.Living.Count);

            var living = new Dictionary<long, Organism>();
            foreach (Organism c in world.Living) living[c.Id] = c;

            int flagged = 0;
            int unflagged = 0;

            foreach (LineageEvent evt in world.DrainLineageEvents())
            {
                Assert.Equal(LineageEventKind.Birth, evt.Kind);
                Assert.Equal(BirthKind.Inoculation, evt.BirthKind);
                Assert.Equal(living[evt.Id].HasPhotosyntheticTissue, evt.HasPhotosynthetic);

                string json = evt.ToJson();
                Assert.DoesNotContain('\n', json);
                Assert.NotNull(Json.Parse(json));
                Assert.Contains(evt.HasPhotosynthetic ? "\"pho\":1" : "\"pho\":0", json);

                if (evt.HasPhotosynthetic) flagged++;
                else unflagged++;
            }

            Assert.Equal(3, flagged);
            Assert.Equal(2, unflagged);
        }

        /// <summary>A pure stomach: one absorptive box, no joint, no neurons — ProducerCountTests'.</summary>
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

        /// <summary>The same body, photosynthetic.</summary>
        private static Genome Leaf()
        {
            Genome g = Stomach();
            g.Nodes[0].CellTypeId = CellTypeIds.Photosynthetic;
            return g;
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

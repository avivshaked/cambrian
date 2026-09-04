using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The per-creature absorptive ledger log — <c>absorptive.jsonl</c>, and the
    /// <see cref="AbsorptiveSample"/> rows behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Motivated by logbook/0050's closing dissection: <c>r14c10-s4</c>'s stomachs were forecast
    /// to breed at R0 2–6 and did not, and no file a run wrote could say where they were, what
    /// density they saw, or why two children died 74–253 s after birth on a 101 J endowment.
    /// <c>lineage.jsonl</c> has parentage and no physiology; <c>snapshots/</c> have genomes and no
    /// ids. These tests pin the join.
    /// </para>
    /// <para>
    /// <b>Every world here has <c>MinimumPopulation = 0</c>.</b> The floor draws absorptive
    /// founders about one time in four (<c>RandomGenomeOptions.FounderCellTypes</c>), so a world
    /// with the floor open would log creatures nobody asked for and "no absorptive creatures
    /// writes no rows" would be untestable.
    /// </para>
    /// </remarks>
    public class AbsorptiveLogTests
    {
        private readonly ITestOutputHelper _output;

        public AbsorptiveLogTests(ITestOutputHelper output) => _output = output;

        /// <summary>A pure stomach: one absorptive box, no joint, no neurons.</summary>
        /// <remarks>Same body <c>WorldTests.AbsorptiveBlob</c> uses, so the two agree.</remarks>
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

        /// <summary>The same body, photosynthetic — a creature the log must not record.</summary>
        private static Genome Plant()
        {
            Genome g = Stomach();
            g.Nodes[0].CellTypeId = CellTypeIds.Photosynthetic;
            return g;
        }

        /// <summary>
        /// An empty stage: no floor spawns, so the only creatures in it are the ones a test puts
        /// there.
        /// </summary>
        private static RunConfig EmptyWorld() => new RunConfig
        {
            MinimumPopulation = 0,
            MaximumPopulation = 100_000,
            Light = new LightModel(20f, 12f),
        };

        [Fact]
        public void ARowCarriesTheDensityTheWorldActuallyFedTheCreatureAtAndTheNetItKept()
        {
            // The two numbers the dissection could not produce. Both are asserted against ground
            // truth taken outside AbsorptiveSample: the field's own density immediately before the
            // step, and the creature's own change in reserve across it.
            RunConfig config = EmptyWorld();

            // Too poor to breed. Reproduction takes energy out of the reserve as well, and the
            // reserve's change across the step is the ground truth netW is checked against — a
            // conception in the same step would be a second withdrawal the ledger never saw.
            config.FounderEnergyJoules = 10f;

            var world = new World(config, seed: 7);

            const float Depth = -20f;
            const float Dt = 1f;

            // Something to eat. The field starts empty — nothing has died yet — so a stomach in a
            // fresh world would be measured against a density of zero, which asserts nothing.
            world.Nutrients.Deposit(Depth, 40_000f, 0);
            world.Inoculate(Stomach(), count: 1, heightY: Depth);
            Assert.Single(world.Living);

            Organism stomach = world.Living[0];
            Assert.True(stomach.HasAbsorptiveTissue);
            Assert.False(stomach.HasPhotosyntheticTissue);

            // Read here, not after the step. The appetite pass in World.Metabolise prices every
            // creature before anything is taken, settled, mixed or advected — so the field as it
            // stands at this instant is exactly what the coming step will charge against, and
            // reading it afterwards would be reading a field three transports later.
            float densityBefore = world.Nutrients.DensityAt(Depth, 0);
            Assert.True(densityBefore > 0f, "the deposit did not land where the stomach is");

            float energyBefore = stomach.Energy;

            world.Step(Dt);

            var rows = new List<AbsorptiveSample>();
            int truncated = world.CollectAbsorptiveLog(rows);

            Assert.Equal(0, truncated);

            // Exactly one row even if it bred this step: a child born after Metabolise has no
            // ledger yet and is left out until its first step.
            AbsorptiveSample row = Assert.Single(rows);

            _output.WriteLine(row.ToJson());

            Assert.Equal(stomach.Id, row.Id);
            Assert.False(row.Dead);
            Assert.Equal(0, row.Patch);
            Assert.Equal(Depth, row.HeightY);
            Assert.Equal(1, row.PartCount);
            Assert.False(row.Mixotroph);
            Assert.Equal(stomach.TissueJoules, row.TissueJoules);
            Assert.Equal(stomach.Phenotype.TotalVolume, row.AbsorptiveVolume);
            Assert.Equal(stomach.Genome.Reproduction.OffspringEndowment, row.Endowment);
            Assert.Equal(0, stomach.Children);
            Assert.Equal(0, row.Children);
            Assert.True(double.IsNaN(row.LastChildSeconds), "a childless creature reported a last child");

            // One eater and a full larder: nothing is rationed, so the density it was fed at is
            // the field's own reading — which is the equality the spec states, taken at the
            // instant the pricing happened.
            Assert.Equal(1f, row.Share);
            Fixtures.AssertClose(densityBefore, row.DensityHere, densityBefore * 1e-5f);

            // Net, from the only witness that cannot be argued with: World.Metabolise moves the
            // reserve by exactly ledger.Net, so the reserve's change over the step divided by the
            // step is ledger.Net / step by construction.
            float expectedNetWatts = (stomach.Energy - energyBefore) / Dt;
            Fixtures.AssertClose(expectedNetWatts, row.NetWatts, Math.Abs(expectedNetWatts) * 1e-4f + 1e-6f);

            // And the five terms close, which is what makes the row a budget rather than five
            // numbers: upkeepW is the whole expenditure (see AbsorptiveSample.UpkeepWatts).
            float closure = row.LightWatts + row.FoodWatts - row.UpkeepWatts - row.ExudedWatts;
            Fixtures.AssertClose(row.NetWatts, closure, Math.Abs(row.NetWatts) * 1e-4f + 1e-6f);

            Assert.True(row.FoodWatts > 0f, "a stomach in a full larder earned nothing");
        }

        [Fact]
        public void ADeathWritesExactlyOneFinalRowAndNothingAfterIt()
        {
            // The whole reason the file has a `dead` flag: every death in this design reads
            // `starved` (Organism.DeathCause is one value), so cause of death discriminates
            // nothing and the terminal budget is the only thing that can.
            RunConfig config = EmptyWorld();

            // Dark and empty: no light to earn on, no detritus to eat, so the stomach spends its
            // endowment and stops. A death that takes thousands of steps would be a slow test
            // measuring the same thing.
            config.Light = new LightModel(0.0001f, 1f);
            config.FounderEnergyJoules = 5f;

            var world = new World(config, seed: 11);
            world.Inoculate(Stomach(), count: 1, heightY: -30f);

            long id = world.Living[0].Id;

            var rows = new List<AbsorptiveSample>();
            int steps = 0;

            while (world.Living.Count > 0 && steps < 5_000)
            {
                world.Step(1f);
                world.CollectAbsorptiveLog(rows);
                steps++;
            }

            Assert.True(world.Living.Count == 0, $"the stomach was still alive after {steps} steps");

            int mine = 0, dead = 0;
            double deathTime = double.NaN;
            foreach (AbsorptiveSample row in rows)
            {
                if (row.Id != id) continue;
                mine++;
                if (!row.Dead) continue;
                dead++;
                deathTime = row.ElapsedSeconds;
            }

            _output.WriteLine($"{mine} rows over {steps} steps, died at t={deathTime}");

            Assert.Equal(1, dead);

            // The living row and the death row are both written on the step it died — the living
            // pass runs over a population it has already left — so the last row for this id is
            // the dead one, and nothing follows it.
            Assert.Equal(steps, mine);
            Assert.True(rows[rows.Count - 1].Dead, "the last row for a dead creature was not its death row");

            // Continuing to step must not produce a second death row, or a lifetime becomes
            // uncountable from the file.
            for (int i = 0; i < 10; i++) world.Step(1f);
            var after = new List<AbsorptiveSample>();
            Assert.Equal(0, world.CollectAbsorptiveLog(after));
            Assert.Empty(after);
        }

        [Fact]
        public void AWorldWithNothingAbsorptiveInItWritesNoRows()
        {
            // Not "writes zeros" — writes nothing. A file that grows in a world with no eaters is
            // a file whose row count means something other than what it says.
            RunConfig config = EmptyWorld();
            var world = new World(config, seed: 3);

            world.Nutrients.Deposit(-10f, 5_000f, 0);
            world.Inoculate(Plant(), count: 4, heightY: -10f);
            Assert.Equal(4, world.Living.Count);

            var rows = new List<AbsorptiveSample>();

            for (int i = 0; i < 50; i++)
            {
                world.Step(1f);
                Assert.Equal(0, world.CollectAbsorptiveLog(rows));
            }

            Assert.Empty(rows);
            foreach (Organism creature in world.Living)
            {
                Assert.False(creature.HasAbsorptiveTissue);
                Assert.True(creature.HasPhotosyntheticTissue);
            }
        }

        [Fact]
        public void ARowIsOneLineAndCarriesNoGenome()
        {
            // JsonlWriter refuses an embedded line break outright, because one bad record makes
            // every row after it in the file unreadable (RunDirectory's own remark). A row that
            // ever carried a genome — GenomeJson is written indented — would trip that on its
            // first write, thousands of rows into a run.
            RunConfig config = EmptyWorld();
            var world = new World(config, seed: 5);

            world.Nutrients.Deposit(-15f, 10_000f, 0);
            world.Inoculate(Stomach(), count: 2, heightY: -15f);
            world.Step(1f);

            var rows = new List<AbsorptiveSample>();
            world.CollectAbsorptiveLog(rows);
            Assert.Equal(2, rows.Count);

            foreach (AbsorptiveSample row in rows)
            {
                string json = row.ToJson();

                Assert.DoesNotContain("\n", json);
                Assert.DoesNotContain("\r", json);
                Assert.DoesNotContain("\"nodes\"", json);

                // Parses, and to the object it claims to be — a row that is one line and not
                // valid JSON is a different kind of unreadable.
                JsonNode parsed = Json.Parse(json);
                Assert.Equal(row.Id, (long)parsed["id"].AsDouble());
                Assert.False(parsed["dead"].AsBool());
            }

            // And the truncated marker, which is written on the same file and by the same rules.
            string marker = AbsorptiveSample.TruncatedRowJson(123.5, 17);
            Assert.DoesNotContain("\n", marker);
            Assert.Equal(17, (int)Json.Parse(marker)["truncated"].AsDouble());
        }

        [Fact]
        public void PastTheCapTheRowsStopAndTheOverflowIsCounted()
        {
            // A stomach bloom must not make this file the largest thing a run writes. The cap is
            // by id — _living is appended to and removed from in place and ids come from a
            // monotonic counter — so what survives the cap is the oldest eaters, which are the
            // ones with a history worth reading.
            RunConfig config = EmptyWorld();

            // Too poor to breed: with 5 J each nobody can afford a child, so the population is
            // exactly what was inoculated and the ids the cap keeps are unambiguous.
            config.FounderEnergyJoules = 5f;

            var world = new World(config, seed: 13);

            const int Extra = 120;
            world.Inoculate(Stomach(), count: World.AbsorptiveLogRowCap + Extra, heightY: -5f);
            Assert.Equal(World.AbsorptiveLogRowCap + Extra, world.Living.Count);

            // One step, because a creature that has never been through Metabolise has no ledger
            // and is deliberately left out of the log.
            world.Step(1f);
            Assert.Equal(World.AbsorptiveLogRowCap + Extra, world.Living.Count);

            var rows = new List<AbsorptiveSample>();
            int truncated = world.CollectAbsorptiveLog(rows);

            Assert.Equal(World.AbsorptiveLogRowCap, rows.Count);
            Assert.Equal(Extra, truncated);

            long first = world.Living[0].Id;
            Assert.Equal(first, rows[0].Id);
            Assert.Equal(first + World.AbsorptiveLogRowCap - 1, rows[rows.Count - 1].Id);
        }

        [Fact]
        public void ChildrenAreCountedOnTheParentAndTheLastBirthIsTimestamped()
        {
            // `children` and `lastChildT` are the columns that separate "this lineage bred and
            // its children died" from "this lineage never bred at all" — the two the R0 forecast
            // cannot tell apart from lineage.jsonl alone until after the fact.
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(400f, 12f),
            };

            var world = new World(config, seed: 17);
            world.Nutrients.Deposit(-4f, 200_000f, 0);
            world.Inoculate(Stomach(), count: 1, heightY: -4f);

            Organism founder = world.Living[0];

            for (int i = 0; i < 400 && founder.Children == 0; i++)
            {
                world.Nutrients.Deposit(-4f, 2_000f, 0);
                world.Step(1f);
            }

            Assert.True(founder.Children > 0, "the stomach never bred, so the counter is untested");

            var rows = new List<AbsorptiveSample>();
            world.CollectAbsorptiveLog(rows);

            AbsorptiveSample row = rows.Find(r => r.Id == founder.Id);
            Assert.Equal(founder.Children, row.Children);
            Assert.False(double.IsNaN(row.LastChildSeconds));
            Assert.True(
                row.LastChildSeconds <= world.ElapsedSeconds,
                "a child was born after the world got there");

            _output.WriteLine(row.ToJson());
        }
    }
}

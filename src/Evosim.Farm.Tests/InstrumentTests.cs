using System;
using System.IO;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// Round 46's recording instruments on the farm's side: the farthest part (K6b), the two inner
    /// timers (K10) and the snow's floor cells (K11a). The kill row's part index (K9b) is Core's
    /// and is asserted in <c>MouthTests</c>.
    /// </summary>
    /// <remarks>
    /// <b>Recordings, so the tests ask only that the number is there and means what it says.</b>
    /// None of the three feeds a sum, a force or a draw; the crowd fixture's regress is what says
    /// no trajectory moved, and it is a farm run, not a test.
    /// </remarks>
    public class InstrumentTests
    {
        /// <summary>Two boxes of half-extent 0.25 m joined face to face along x.</summary>
        private static Genome TwoBoxes()
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(0.25f, 0.25f, 0.25f),
                JointType = JointType.Fixed,
                RecursiveLimit = 2,
                CellTypeId = CellTypeIds.Structural,
                Power = 0f,
            };
            node.ResampleJointLimits(new Float2(-1f, 1f));
            node.Edges.Add(new MorphEdge
            {
                Child = 0,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Nodes.Add(node);
            return genome;
        }

        [Fact]
        public void TheFarthestPartOfATwoPartBodyIsItsSecondPartsDistanceAndScalesWithTheBody()
        {
            Phenotype body = Developer.Develop(TwoBoxes());
            Assert.Equal(2, body.PartCount);

            // The second box's centre stands two half-extents from the root's.
            Assert.InRange(Sampler.FarthestPartFromRoot(body), 0.5d - 1e-6, 0.5d + 1e-6);

            // Scaled as the living body is: a body at twice the length reads twice the reach.
            Phenotype grown = body.Scaled(2f);
            Assert.InRange(Sampler.FarthestPartFromRoot(grown), 1d - 2e-6, 1d + 2e-6);

            // And a one-part body stands nowhere from its own root.
            Genome single = TwoBoxes();
            single.Nodes[0].Edges.Clear();
            Phenotype one = Developer.Develop(single);
            Assert.Equal(1, one.PartCount);
            Assert.Equal(0d, Sampler.FarthestPartFromRoot(one));
        }

        [Fact]
        public void TheStatsRowCarriesMaxReachAndBothInnerTimers()
        {
            string scratch = SimulationTests.Scratch("instruments-row");
            string statsPath;
            double maxReachSeen = double.NaN;

            using (Simulation sim = SimulationTests.Build(out RunDirectory dir, scratch))
            using (dir)
            {
                for (int i = 0; i < 200; i++) sim.Step();

                Assert.True(sim.World.Living.Count > 0, "nothing alive to read");

                // The ledger's clock ran: every metabolic step bills every body.
                Assert.True(sim.World.LedgerTicks > 0L, "the ledger timer never ran");

                foreach (Organism creature in sim.World.Living)
                {
                    double far = Sampler.FarthestPartFromRoot(creature.Phenotype);
                    if (!(far <= maxReachSeen)) maxReachSeen = far;
                }

                var report = new Report(Path.Combine(scratch, "report.md"), sim.Config);
                var sampler = new Sampler();
                string row = sampler.Write(sim, dir, report.Columns);

                // The table's last base column is the same number, rounded to three places.
                string[] cells = row.Split('|');
                int at = 1 + Array.IndexOf(Report.BaseColumns, "max reach");
                Assert.Equal(maxReachSeen.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), cells[at].Trim());

                statsPath = Path.Combine(dir.Path, "stats.jsonl");
            }

            string[] rows = JsonlWriter.ReadRows(statsPath);
            JsonNode last = Json.Parse(rows[rows.Length - 1]);

            Assert.True(last.Has("maxReach"));
            Assert.Equal(maxReachSeen, last["maxReach"].AsDouble(), 6);

            Assert.True(last.Has("wallExposureMs"), "wallExposureMs is not on the row");
            Assert.True(last.Has("wallLedgerMs"), "wallLedgerMs is not on the row");
            Assert.True(last["wallExposureMs"].AsDouble() >= 0d);
            Assert.True(last["wallLedgerMs"].AsDouble() >= 0d);

            // Neither is a phase: the harness split's fields are the six they were.
            Assert.Equal(6, Simulation.HarnessPhaseFields.Length);
        }

        /// <summary>
        /// D224: the stats row carries <c>biteStore</c> only in a world whose bites take the store, beside the other
        /// bite columns, and a world with contact eating alone writes the columns it wrote before.
        /// </summary>
        [Fact]
        public void TheStatsRowCarriesTheBiteStoreColumnOnlyWithTheDialOn()
        {
            foreach (bool takesStore in new[] { false, true })
            {
                string scratch = SimulationTests.Scratch(takesStore ? "bite-store-on" : "bite-store-off");
                RunConfig config = RemainsFixtures.Config(contactEating: true);
                config.BiteTakesStore = takesStore;

                var world = new World(config, 1UL);
                RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow);
                string statsPath;

                using (dir)
                using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path))
                {
                    for (int i = 0; i < 20; i++) sim.Step();

                    var report = new Report(Path.Combine(scratch, "report.md"), sim.Config);
                    new Sampler().Write(sim, dir, report.Columns);
                    statsPath = Path.Combine(dir.Path, "stats.jsonl");
                }

                string[] rows = JsonlWriter.ReadRows(statsPath);
                JsonNode last = Json.Parse(rows[rows.Length - 1]);

                Assert.True(last.Has("biteKeptLeaves"), "the contact-eating columns are on the row");
                Assert.Equal(takesStore, last.Has("biteStore"));
                if (takesStore) Assert.Equal(world.BiteStoreTakenJoules, last["biteStore"].AsDouble(), 6);
            }
        }

        /// <summary>
        /// D227: the stats row carries <c>mouthBlows</c> and <c>mouthBlowsFinishing</c> only with the bite damage dial
        /// off 1, and a world with the dial at 1 writes the columns it wrote before.
        /// </summary>
        [Fact]
        public void TheStatsRowCarriesTheMouthBlowColumnsOnlyWithTheDamageDialOffOne()
        {
            foreach (float scale in new[] { 1f, 4f })
            {
                string scratch = SimulationTests.Scratch(scale == 1f ? "mouth-blows-off" : "mouth-blows-on");
                RunConfig config = RemainsFixtures.Config(contactEating: true);
                config.BiteDamageScale = scale;

                var world = new World(config, 1UL);
                RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow);
                string statsPath;

                using (dir)
                using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path))
                {
                    for (int i = 0; i < 20; i++) sim.Step();

                    var report = new Report(Path.Combine(scratch, "report.md"), sim.Config);
                    new Sampler().Write(sim, dir, report.Columns);
                    statsPath = Path.Combine(dir.Path, "stats.jsonl");
                }

                string[] rows = JsonlWriter.ReadRows(statsPath);
                JsonNode last = Json.Parse(rows[rows.Length - 1]);

                Assert.True(last.Has("biteKeptLeaves"), "the contact-eating columns are on the row");
                Assert.Equal(scale != 1f, last.Has("mouthBlows"));
                Assert.Equal(scale != 1f, last.Has("mouthBlowsFinishing"));
                if (scale != 1f)
                {
                    Assert.Equal((double)world.MouthBlows, last["mouthBlows"].AsDouble());
                    Assert.Equal((double)world.MouthBlowsFinishing, last["mouthBlowsFinishing"].AsDouble());
                }
            }
        }

        /// <summary>
        /// D232: the stats row carries the gut's columns only with the kill swallowed into a gut, beside the other bite
        /// columns, and a world with contact eating alone writes the columns it wrote before.
        /// </summary>
        [Fact]
        public void TheStatsRowCarriesTheGutColumnsOnlyWithTheKillSwallowed()
        {
            string[] columns =
            {
                "gutJoules", "killsSwallowed", "gutSwallowedJoules", "gutDigested", "gutWaste", "biteCapCut", "gutCapCut", "gutCapBound",
            };

            foreach (bool gut in new[] { false, true })
            {
                string scratch = SimulationTests.Scratch(gut ? "gut-on" : "gut-off");
                RunConfig config = RemainsFixtures.Config(contactEating: true);
                config.KillSwallowsBody = gut;

                var world = new World(config, 1UL);
                RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow);
                string statsPath;

                using (dir)
                using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path))
                {
                    for (int i = 0; i < 20; i++) sim.Step();

                    var report = new Report(Path.Combine(scratch, "report.md"), sim.Config);
                    new Sampler().Write(sim, dir, report.Columns);
                    statsPath = Path.Combine(dir.Path, "stats.jsonl");
                }

                string[] rows = JsonlWriter.ReadRows(statsPath);
                JsonNode last = Json.Parse(rows[rows.Length - 1]);

                Assert.True(last.Has("biteKeptLeaves"), "the contact-eating columns are on the row");
                foreach (string column in columns) Assert.Equal(gut, last.Has(column));
                if (gut)
                {
                    Assert.Equal(world.GutJoulesHeld, last["gutJoules"].AsDouble(), 6);
                    Assert.Equal((double)world.KillsSwallowed, last["killsSwallowed"].AsDouble());
                    Assert.Equal(world.GutDigestedJoules, last["gutDigested"].AsDouble(), 6);
                }
            }
        }

        [Fact]
        public void TheSnowFloorDumpIsOneFloatAColumnAndNeverMoreThanTheColumn()
        {
            string scratch = SimulationTests.Scratch("instruments-floor");
            string fields;
            double seconds;
            int columns;

            using (Simulation sim = SimulationTests.Build(out RunDirectory dir, scratch))
            using (dir)
            {
                for (int i = 0; i < 100; i++) sim.Step();

                var snow = Assert.IsType<GridField>(sim.World.Nutrients);
                columns = snow.CellsX * snow.CellsZ;

                // A young small world has shed little or no snow, so some is laid down by hand and
                // settled a while, which gives the floor cells something unlike the column above.
                snow.SeedUniform(1f);
                for (int s = 0; s < 20; s++) snow.Settle(0.5f);

                new Sampler().Snapshot(dir, sim.World);

                fields = dir.FieldsPath;
                seconds = sim.World.ElapsedSeconds;
            }

            string stem = Path.Combine(fields, ((long)seconds).ToString("000000000"));
            float[] sums = ReadFloats(stem + ".snow-columns.f32");
            float[] floor = ReadFloats(stem + ".snow-floor.f32");

            Assert.Equal(columns, floor.Length);
            Assert.Equal(sums.Length, floor.Length);

            bool anySnow = false;
            for (int c = 0; c < columns; c++)
            {
                Assert.True(floor[c] >= 0f, $"column {c}: a negative floor cell");
                Assert.True(
                    floor[c] <= sums[c] * (1f + 1e-6f) + 1e-9f,
                    $"column {c}: floor cell {floor[c]} J over the column's {sums[c]} J");
                if (floor[c] > 0f) anySnow = true;
            }

            Assert.True(anySnow, "no floor cell held any snow, so the bound was not tested");

            string layout = File.ReadAllText(Path.Combine(fields, "layout.json"));
            JsonNode named = Json.Parse(layout)["snowFloor"];
            Assert.Equal("NNNNNNNNN.snow-floor.f32", named["file"].AsString());
            Assert.Equal("ix * cellsZ + iz", named["order"].AsString());
        }

        private static float[] ReadFloats(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(0, bytes.Length % 4);

            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// Record format 2 as the farm writes it (<c>logbook/specs/record-and-film-spec.md</c> Part A):
    /// the same world recorded in both formats reads the same through <see cref="RecordFiles"/>,
    /// every living body in a slim snapshot finds its genome, <c>poses.jsonl</c> is not written,
    /// and the switch and its one default are bound where the other recording settings are.
    /// </summary>
    public class RecordFormatFarmTests
    {
        private static Simulation Build(int format, string scratch, out RunDirectory dir)
        {
            RunConfig config = SimulationTests.SmallWorld();

            // What Program.Loop does: the queue is on exactly when the directory has a genome file.
            var world = new World(config, 1UL);
            dir = RunDirectory.Create(scratch, config, DateTime.UtcNow, format);
            world.QueueAdmittedGenomes = dir.Genomes != null;

            return new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path);
        }

        private sealed class Recorded
        {
            public string Path;
            public double Second;
            public readonly Dictionary<long, string> FullRows = new Dictionary<long, string>();
            public readonly Dictionary<long, float> Fractions = new Dictionary<long, float>();
            public int Samples;
        }

        /// <summary>Steps a small world, samples it five times and snapshots it once.</summary>
        private static Recorded Record(int format, string name)
        {
            string scratch = SimulationTests.Scratch(name);
            var recorded = new Recorded();

            using (Simulation sim = Build(format, scratch, out RunDirectory dir))
            using (dir)
            {
                var sampler = new Sampler();
                IReadOnlyList<string> columns = Sampler.Columns(sim.Config);
                int metabolic = 0;

                while (metabolic < 25)
                {
                    if (!sim.Step()) continue;
                    metabolic++;

                    if (metabolic % 5 == 0)
                    {
                        sampler.Write(sim, dir, columns);
                        recorded.Samples++;
                    }
                }

                Assert.True(sim.World.Living.Count > 0, "nothing alive to snapshot");

                foreach (Organism creature in sim.World.Living)
                {
                    recorded.FullRows[creature.Id] = GenomeJson.Write(
                        creature.Genome, indent: false, id: creature.Id,
                        moduleCounts: creature.ModuleCounts, lostPartPaths: creature.LostPartPaths);
                    recorded.Fractions[creature.Id] = creature.BodyFraction;
                }

                sampler.Snapshot(dir, sim.World);
                sampler.Close();

                recorded.Path = dir.Path;
                recorded.Second = sim.World.ElapsedSeconds;
            }

            return recorded;
        }

        [Fact]
        public void ASlimSnapshotJoinsEveryLivingBodyToItsGenomeAndCarriesItsFraction()
        {
            Recorded run = Record(RunRecordFormat.Compact, "record-compact");

            SnapshotRead read = RecordFiles.ReadSnapshot(run.Path, run.Second, RunRecordFormat.Compact);

            Assert.NotNull(read);
            Assert.Equal(0, read.WithoutAGenome);
            Assert.Empty(read.Notes);
            Assert.Equal(run.FullRows.Count, read.Rows.Length);

            for (int i = 0; i < read.Rows.Length; i++)
            {
                Assert.Equal(run.FullRows[read.Ids[i]], read.Rows[i]);
                Assert.Equal(run.Fractions[read.Ids[i]], read.BodyFractions[i]);
            }

            // Format 2 writes no poses.jsonl and no positions.jsonl; its positions are a member a
            // sample, and its snapshot is the gzipped slim file alone.
            Assert.False(File.Exists(Path.Combine(run.Path, RecordFiles.PosesName)));
            Assert.False(File.Exists(Path.Combine(run.Path, RecordFiles.PositionsName)));
            Assert.Null(RecordFiles.SnapshotFile(run.Path, run.Second, RunRecordFormat.Jsonl));
            Assert.Equal(run.Samples, new List<string>(RecordFiles.PositionLines(run.Path, RunRecordFormat.Compact)).Count);
        }

        [Fact]
        public void EveryBirthRowHasItsGenomeRowAndNoOtherIsWritten()
        {
            Recorded run = Record(RunRecordFormat.Compact, "record-births");

            var births = new List<long>();
            foreach (string row in JsonlWriter.ReadRows(Path.Combine(run.Path, "lineage.jsonl")))
            {
                JsonNode node = Json.Parse(row);
                if (node["e"].AsString() == "b") births.Add((long)node["id"].AsDouble());
            }

            var genomes = new List<long>();
            using (GzipMemberReader reader = GzipMemberReader.Open(Path.Combine(run.Path, RecordFiles.GenomesName)))
            {
                foreach (string line in reader.Lines())
                {
                    genomes.Add(RecordFiles.IdOf(line));

                    // The genome row is the genome and its id, nothing of the life.
                    Assert.DoesNotContain("\"moduleCounts\"", line);
                    Assert.DoesNotContain("\"lostPaths\"", line);
                }

                Assert.False(reader.Torn);
            }

            Assert.NotEmpty(births);
            Assert.Equal(births, genomes);
        }

        [Fact]
        public void TheSameWorldReadsTheSameFromBothRecords()
        {
            Recorded compact = Record(RunRecordFormat.Compact, "record-both-2");
            Recorded jsonl = Record(RunRecordFormat.Jsonl, "record-both-1");

            // The recording moved nothing: the same bodies, the same seconds.
            Assert.Equal(jsonl.Second, compact.Second);

            SnapshotRead a = RecordFiles.ReadSnapshot(jsonl.Path, jsonl.Second, RunRecordFormat.Jsonl);
            SnapshotRead b = RecordFiles.ReadSnapshot(compact.Path, compact.Second, RunRecordFormat.Compact);

            Assert.Equal(a.Rows, b.Rows);
            Assert.Equal(a.Ids, b.Ids);

            Assert.Equal(
                new List<string>(RecordFiles.PositionLines(jsonl.Path, RunRecordFormat.Jsonl)),
                new List<string>(RecordFiles.PositionLines(compact.Path, RunRecordFormat.Compact)));

            Assert.Equal(
                File.ReadAllText(Path.Combine(jsonl.Path, "lineage.jsonl")),
                File.ReadAllText(Path.Combine(compact.Path, "lineage.jsonl")));

            // Format 1 is what it always was: poses.jsonl beside positions.jsonl.
            Assert.True(File.Exists(Path.Combine(jsonl.Path, RecordFiles.PosesName)));
            Assert.False(File.Exists(Path.Combine(jsonl.Path, RecordFiles.GenomesName)));
        }

        // ------------------------------------------------------------------ the binding

        [Fact]
        public void TheRecordFormatIsBoundDefaultsToTwoAndReachesNoConfigHash()
        {
            Dictionary<string, string> plain = EnvBindingTests.Round42Seed1();

            EnvSettings unset = EnvBinding.Read(EnvBinding.Of(plain));
            Assert.Equal(RunRecordFormat.Compact, unset.RecordFormat);

            var old = new Dictionary<string, string>(plain) { { "EVOSIM_RECORD_FORMAT", "1" } };
            EnvSettings one = EnvBinding.Read(EnvBinding.Of(old));
            Assert.Equal(RunRecordFormat.Jsonl, one.RecordFormat);

            Assert.Equal(EnvBinding.BuildConfig(unset).Hash(), EnvBinding.BuildConfig(one).Hash());
            Assert.Empty(EnvBinding.UnknownNames(new List<string> { "EVOSIM_RECORD_FORMAT" }));

            var bad = new Dictionary<string, string>(plain) { { "EVOSIM_RECORD_FORMAT", "3" } };
            Assert.Throws<ArgumentException>(() => EnvBinding.Read(EnvBinding.Of(bad)));
        }

        [Fact]
        public void FormatTwoTurnsTheStreamOnAtTheReportIntervalUnlessTheLauncherNamedACadence()
        {
            var block = new Dictionary<string, string> { { "EVOSIM_REPORT_EVERY", "20" } };

            EnvSettings two = EnvBinding.Read(EnvBinding.Of(block));
            Assert.True(two.ApplyRecordDefaults(sharedSpace: true));
            Assert.Equal(10f, two.PoseEvery);
            Assert.Equal(10f, two.ResolvePoseEvery());

            // A tiled world wrote no poses.jsonl, so it gets no stream by default either.
            EnvSettings tiled = EnvBinding.Read(EnvBinding.Of(block));
            Assert.False(tiled.ApplyRecordDefaults(sharedSpace: false));
            Assert.Equal(0f, tiled.PoseEvery);

            // Format 1 writes poses.jsonl and leaves the stream as the launcher had it.
            var oldBlock = new Dictionary<string, string>(block) { { "EVOSIM_RECORD_FORMAT", "1" } };
            EnvSettings one = EnvBinding.Read(EnvBinding.Of(oldBlock));
            Assert.False(one.ApplyRecordDefaults(sharedSpace: true));
            Assert.Equal(0f, one.PoseEvery);

            // A launcher that names 0 wants no stream, and gets none.
            var off = new Dictionary<string, string>(block) { { "EVOSIM_POSE_EVERY", "0" } };
            EnvSettings named = EnvBinding.Read(EnvBinding.Of(off));
            Assert.False(named.ApplyRecordDefaults(sharedSpace: true));
            Assert.Equal(0f, named.PoseEvery);

            // And one that names a cadence keeps it.
            var half = new Dictionary<string, string>(block) { { "EVOSIM_POSE_EVERY", "0.5" } };
            EnvSettings fine = EnvBinding.Read(EnvBinding.Of(half));
            Assert.False(fine.ApplyRecordDefaults(sharedSpace: true));
            Assert.Equal(0.5f, fine.PoseEvery);
        }

        [Fact]
        public void TheManifestSaysWhichRecordTheDirectoryHolds()
        {
            string two = Manifest.Render(new RunManifest { ArmName = "r", RecordFormat = RunRecordFormat.Compact }, null);
            string one = Manifest.Render(new RunManifest { ArmName = "r", RecordFormat = RunRecordFormat.Jsonl }, null);

            Assert.Contains("\"recordFormat\": 2", two, StringComparison.Ordinal);
            Assert.Contains("\"recordFormat\": 1", one, StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------ a resume's record

        /// <summary>
        /// The source's record is its manifest's <c>recordFormat</c>, format 1 when the manifest
        /// has none, the checkpoint's version when there is no manifest, and a refusal when the
        /// manifest names a record this build does not write.
        /// </summary>
        [Fact]
        public void AResumeReadsItsSourcesRecordFromTheManifestFirst()
        {
            string run = SimulationTests.Scratch("record-source");
            Directory.CreateDirectory(run);
            string manifest = Path.Combine(run, "run.json");

            var five = new CheckpointHeader { Version = Checkpoint.Version };
            var four = new CheckpointHeader { Version = Checkpoint.UncompressedVersion };

            File.WriteAllText(manifest, "{\"arm\": \"a\", \"recordFormat\": 1}");
            Assert.Equal(RunRecordFormat.Jsonl, Program.SourceRecordFormat(run, five));

            File.WriteAllText(manifest, "{\"arm\": \"a\", \"recordFormat\": 2}");
            Assert.Equal(RunRecordFormat.Compact, Program.SourceRecordFormat(run, four));

            // Every manifest before the record existed.
            File.WriteAllText(manifest, "{\"arm\": \"a\"}");
            Assert.Equal(RunRecordFormat.Jsonl, Program.SourceRecordFormat(run, five));

            File.WriteAllText(manifest, "{\"arm\": \"a\", \"recordFormat\": 7}");
            Assert.Throws<InvalidDataException>(() => Program.SourceRecordFormat(run, five));

            File.Delete(manifest);
            Assert.Equal(RunRecordFormat.Compact, Program.SourceRecordFormat(run, five));
            Assert.Equal(RunRecordFormat.Jsonl, Program.SourceRecordFormat(run, four));
        }

        /// <summary>
        /// A format 1 run resumed with no <c>EVOSIM_RECORD_FORMAT</c> is continued in format 1,
        /// its checkpoints still version 4; a launcher that names 2 gets 2.
        /// </summary>
        [Fact]
        public void AResumeKeepsItsSourcesRecordUnlessTheLauncherNamesOne()
        {
            string root = SimulationTests.Scratch("record-resume-" + Guid.NewGuid().ToString("N"));
            string runsRoot = Path.Combine(root, "runs");

            var source = new List<string>(FilmWindowTests.SmallRun(runsRoot, "old", seconds: 40, checkpointEvery: 20))
            {
                "EVOSIM_RECORD_FORMAT=1",
            };
            Assert.Equal(0, Program.Main(source.ToArray()));

            string sourceRun = Directory.GetDirectories(Path.Combine(runsRoot, "old"))[0];
            Assert.True(File.Exists(Path.Combine(sourceRun, "positions.jsonl")));

            string Resumed(string arm, params string[] extra)
            {
                var settings = new List<string>
                {
                    "EVOSIM_RUNS_ROOT=" + runsRoot,
                    "EVOSIM_OUT=" + arm + ".md",
                    "EVOSIM_SECONDS=30",
                    "EVOSIM_WALL_MINUTES=10",
                    "EVOSIM_THREADS=2",
                    "EVOSIM_RESUME=" + sourceRun,
                    "EVOSIM_RESUME_AT=20",
                };
                settings.AddRange(extra);

                Assert.Equal(0, Program.Main(settings.ToArray()));
                return Directory.GetDirectories(Path.Combine(runsRoot, arm))[0];
            }

            string kept = Resumed("kept");
            JsonNode keptManifest = Json.Parse(File.ReadAllText(Path.Combine(kept, "run.json")));
            Assert.Equal(RunRecordFormat.Jsonl, keptManifest["recordFormat"].AsInt());
            Assert.True(File.Exists(Path.Combine(kept, "positions.jsonl")));
            Assert.False(File.Exists(Path.Combine(kept, RecordFiles.GenomesName)));

            string keptCheckpoints = Checkpoint.DirectoryIn(kept);
            Assert.True(Directory.Exists(keptCheckpoints), "the resumed run wrote no checkpoint");

            foreach (string checkpoint in Directory.GetFiles(keptCheckpoints, "*.ckpt"))
            {
                Assert.Equal(Checkpoint.UncompressedVersion, CheckpointReader.ReadHeader(checkpoint).Version);
            }

            string named = Resumed("named", "EVOSIM_RECORD_FORMAT=2");
            JsonNode namedManifest = Json.Parse(File.ReadAllText(Path.Combine(named, "run.json")));
            Assert.Equal(RunRecordFormat.Compact, namedManifest["recordFormat"].AsInt());
            Assert.True(File.Exists(Path.Combine(named, RecordFiles.GenomesName)));
        }
    }
}

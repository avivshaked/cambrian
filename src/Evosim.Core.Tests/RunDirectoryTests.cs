using System;
using System.IO;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>The on-disk run layout — DESIGN.md §9.</summary>
    public class RunDirectoryTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _root;

        public RunDirectoryTests(ITestOutputHelper output)
        {
            _output = output;
            _root = Path.Combine(Path.GetTempPath(), "evosim-tests-" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
            catch (IOException) { /* a leaked temp directory is not worth failing a test over */ }
        }

        private static readonly DateTime Started = new DateTime(2026, 8, 5, 14, 30, 22, DateTimeKind.Utc);

        [Fact]
        public void CreatesTheExpectedLayout()
        {
            using (var run = RunDirectory.Create(_root, new RunConfig(), Started))
            {
                run.Lineage.WriteGenome(GenomeFactory.Random(new Rng(1)));
                run.Stats.WriteRow(w => w.Field("t", 0f).Field("population", 128));

                _output.WriteLine(Path.GetFileName(run.Path));

                Assert.True(File.Exists(Path.Combine(run.Path, "config.json")));
                Assert.True(Directory.Exists(run.SnapshotsPath));
            }

            string dir = Directory.GetDirectories(_root)[0];
            Assert.True(File.Exists(Path.Combine(dir, "lineage.jsonl")));
            Assert.True(File.Exists(Path.Combine(dir, "stats.jsonl")));
        }

        [Fact]
        public void TheDirectoryNameCarriesTheTimeAndTheSettingsHash()
        {
            // So that two runs differing only in settings are distinguishable in a listing —
            // the case a timestamp alone cannot tell you anything about.
            var a = new RunConfig();
            var b = new RunConfig { WorkCostMultiplier = 3f };

            using (var runA = RunDirectory.Create(_root, a, Started))
            using (var runB = RunDirectory.Create(_root, b, Started))
            {
                Assert.NotEqual(Path.GetFileName(runA.Path), Path.GetFileName(runB.Path));
                Assert.StartsWith("2026-08-05-143022-", Path.GetFileName(runA.Path));
            }
        }

        [Fact]
        public void EveryRowIsOneLineAndReloads()
        {
            const int births = 50;

            using (var run = RunDirectory.Create(_root, new RunConfig(), Started))
            {
                for (ulong seed = 1; seed <= births; seed++)
                {
                    run.Lineage.WriteGenome(GenomeFactory.Random(new Rng(seed)));
                }
                Assert.Equal(births, run.Lineage.RowCount);
            }

            string[] lines = File.ReadAllLines(
                Path.Combine(Directory.GetDirectories(_root)[0], "lineage.jsonl"));

            Assert.Equal(births, lines.Length);

            for (int i = 0; i < lines.Length; i++)
            {
                Genome g = GenomeJson.Read(lines[i]);
                Assert.Empty(g.Validate());
            }

            _output.WriteLine($"{births} creatures, {string.Join("", lines).Length / births} bytes each");
        }

        [Fact]
        public void AKilledRunLeavesEveryCompletedRowIntact()
        {
            // The reason the format is line-oriented and appended. A single rewritten document
            // would leave a truncated file that parses as nothing at all; here everything
            // written before the interruption is still valid.
            var run = RunDirectory.Create(_root, new RunConfig(), Started);

            for (int i = 0; i < 20; i++) run.Stats.WriteRow(w => w.Field("t", 1f));

            // No Dispose: stand in for a process killed mid-run. Stats flushes each row, so
            // everything written should already be on disk — and readable *now*, while the
            // writer still holds the file, which is what watching a live run requires.
            string[] lines = JsonlWriter.ReadRows(run.Stats.Path);

            Assert.Equal(20, lines.Length);
            foreach (string line in lines) Json.Parse(line);

            run.Dispose();
        }

        [Fact]
        public void ARowContainingALineBreakIsRefused()
        {
            // One embedded newline splits a record across two lines and makes every row after it
            // unreadable — a whole-file loss caused by one bad record.
            using (var writer = new JsonlWriter(Path.Combine(_root, "x.jsonl"), flushEachRow: true))
            {
                Assert.Throws<ArgumentException>(() => writer.Write("{\"a\":\n1}"));
                Assert.Throws<ArgumentException>(
                    () => writer.Write(GenomeJson.Write(GenomeFactory.Random(new Rng(1)), indent: true)));
            }
        }

        [Fact]
        public void AppendingToAnExistingRunDoesNotTruncateIt()
        {
            // Resuming a run must add to the record rather than replace it.
            string path = Path.Combine(_root, "append.jsonl");

            using (var w = new JsonlWriter(path, flushEachRow: true)) w.Write("{\"n\":1}");
            using (var w = new JsonlWriter(path, flushEachRow: true)) w.Write("{\"n\":2}");

            Assert.Equal(2, File.ReadAllLines(path).Length);
        }

        [Fact]
        public void SettingsSurviveAWriteAndReload()
        {
            var config = new RunConfig { WorkCostMultiplier = 2.5f, PerOffspringOverheadJoules = 40f };

            using (RunDirectory.Create(_root, config, Started)) { }

            RunConfig back = RunDirectory.ReadConfig(
                Directory.GetDirectories(_root)[0], out string mismatch);

            Assert.Null(mismatch);
            Assert.Equal(config.Hash(), back.Hash());
        }

        [Fact]
        public void SnapshotPathsSortChronologically()
        {
            using (var run = RunDirectory.Create(_root, new RunConfig(), Started))
            {
                string early = Path.GetFileName(run.SnapshotPath(60));
                string late = Path.GetFileName(run.SnapshotPath(3600));

                _output.WriteLine($"{early}  {late}");
                Assert.True(string.CompareOrdinal(early, late) < 0,
                    "zero-padding is what makes a directory listing chronological");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D201's cousin resume: a checkpoint carried on under an edited copy of its source's config,
    /// with only the economy and mouth dials and the cell types free to differ, and the trickle
    /// off on its own switch.
    /// </summary>
    [Collection(FarmProcessCollection.Name)]
    public sealed class CousinResumeTests
    {
        private readonly ITestOutputHelper _out;

        public CousinResumeTests(ITestOutputHelper output) => _out = output;

        private static string Fresh(string name)
        {
            string path = Path.Combine(SimulationTests.Scratch("cousin-resume"), name);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            Directory.CreateDirectory(path);
            return path;
        }

        private static RunConfig Source()
        {
            var config = new RunConfig();
            config.FoundingTricklePerSecond = 0.05f;
            return config;
        }

        private static string Saved(RunConfig config, string name)
        {
            string path = Path.Combine(Fresh("configs-" + name), "config.json");
            File.WriteAllText(path, RunConfigJson.Write(config));
            return path;
        }

        // ------------------------------------------------------------------ the config, field by field

        [Fact]
        public void AnUnchangedConfigChangesNothing()
        {
            RunConfig source = Source();
            RunConfig cousin = CousinConfig.Build(source, Saved(source, "unchanged"), false, out IReadOnlyList<string> changes);

            Assert.Empty(changes);
            Assert.Equal(source.Hash(), cousin.Hash());
        }

        [Fact]
        public void AnEconomyAndAMouthDialAreAllowedAndListed()
        {
            RunConfig source = Source();
            RunConfig edited = RunConfigJson.Read(RunConfigJson.Write(source));
            edited.WorkCostMultiplier = 2f;
            edited.HealingPerSecond = source.HealingPerSecond + 1f;

            RunConfig cousin = CousinConfig.Build(source, Saved(edited, "dials"), false, out IReadOnlyList<string> changes);

            Assert.Equal(2, changes.Count);
            Assert.Contains(changes, c => c.StartsWith("economy.", StringComparison.Ordinal) && c.Contains("-> 2"));
            Assert.Contains(changes, c => c.StartsWith("mouth.", StringComparison.Ordinal));
            Assert.NotEqual(source.Hash(), cousin.Hash());
            Assert.Equal(2f, cousin.WorkCostMultiplier);
        }

        [Fact]
        public void ACellTypesFieldIsAllowedAndNamed()
        {
            RunConfig source = Source();
            string text = System.Text.RegularExpressions.Regex.Replace(
                RunConfigJson.Write(source),
                "(\"id\":\\s*\"consumer\",\\s*\"upkeepWattsPerCubicMetre\":\\s*)[-0-9.eE+]+", "${1}3.25");
            string path = Path.Combine(Fresh("configs-cell"), "config.json");
            File.WriteAllText(path, text);

            RunConfig cousin = CousinConfig.Build(source, path, false, out IReadOnlyList<string> changes);

            Assert.Single(changes);
            Assert.StartsWith("cellTypes.consumer.upkeepWattsPerCubicMetre: ", changes[0]);
            Assert.EndsWith(" -> 3.25", changes[0]);
            Assert.Equal(3.25f, cousin.CellTypes.Resolve("consumer").UpkeepWattsPerCubicMetre);
        }

        [Fact]
        public void TheWorldsSizeIsRefusedByName()
        {
            RunConfig source = Source();
            RunConfig edited = RunConfigJson.Read(RunConfigJson.Write(source));
            edited.WorkCostMultiplier = 2f;
            edited.WorldAreaSquareMetres = source.WorldAreaSquareMetres * 2f;

            var e = Assert.Throws<InvalidOperationException>(() =>
                CousinConfig.Build(source, Saved(edited, "area"), false, out _));

            Assert.Contains("world.", e.Message);
            Assert.DoesNotContain("economy.", e.Message.Substring(e.Message.IndexOf("also changes", StringComparison.Ordinal)));
        }

        [Fact]
        public void ThePaceFactorIsRefusedByName()
        {
            // D239: a world is resumed at the pace it was written at, so a cousin's edited config
            // cannot turn the factor either.
            RunConfig source = Source();
            RunConfig edited = RunConfigJson.Read(RunConfigJson.Write(source));
            edited.WorkCostMultiplier = 2f;
            edited.PaceFactor = 3f;

            var e = Assert.Throws<InvalidOperationException>(() =>
                CousinConfig.Build(source, Saved(edited, "pace"), false, out _));

            string refused = e.Message.Substring(e.Message.IndexOf("also changes", StringComparison.Ordinal));
            Assert.Contains("physics.", refused);
            Assert.Contains("pacefactor", refused.ToLowerInvariant());
        }

        [Fact]
        public void TheTrickleFallsToZeroOnlyUnderItsSwitch()
        {
            RunConfig source = Source();

            RunConfig off = CousinConfig.Build(source, null, true, out IReadOnlyList<string> changes);
            Assert.Equal(0f, off.FoundingTricklePerSecond);
            Assert.Single(changes);
            Assert.StartsWith("population.", changes[0]);
            Assert.Equal(source.MinimumPopulation, off.MinimumPopulation);

            // The same edit made in the file, without the switch, is a population field changed.
            RunConfig edited = RunConfigJson.Read(RunConfigJson.Write(source));
            edited.FoundingTricklePerSecond = 0f;
            Assert.Throws<InvalidOperationException>(() => CousinConfig.Build(source, Saved(edited, "trickle"), false, out _));
        }

        [Fact]
        public void ThePoolShareGoesWithTheTrickle()
        {
            RunConfig source = Source();
            source.FoundingTricklePoolShare = 0.6f;

            RunConfig off = CousinConfig.Build(source, null, true, out IReadOnlyList<string> changes);
            Assert.Equal(0f, off.FoundingTricklePerSecond);
            Assert.Equal(0f, off.FoundingTricklePoolShare);
            Assert.Equal(source.FoundingTricklePoolCount, off.FoundingTricklePoolCount);
            Assert.Equal(source.FoundingTricklePoolHash, off.FoundingTricklePoolHash);
            Assert.Equal(2, changes.Count);

            // The share alone, without the switch, is refused.
            RunConfig edited = RunConfigJson.Read(RunConfigJson.Write(source));
            edited.FoundingTricklePoolShare = 0f;
            Assert.Throws<InvalidOperationException>(() => CousinConfig.Build(source, Saved(edited, "share"), false, out _));
        }

        // ------------------------------------------------------------------ through the farm

        private static string RunDirectoryIn(string runsRoot, string arm) =>
            Directory.GetDirectories(Path.Combine(runsRoot, arm)).OrderBy(d => d, StringComparer.Ordinal).Last();

        private static List<string> DigestRows(string runDirectory) =>
            File.ReadAllLines(Path.Combine(runDirectory, "digest.jsonl")).Where(l => l.Length > 0).ToList();

        private static string Resumed(string name, string source, params string[] extra)
        {
            string root = Fresh(name);
            var args = new List<string>(FilmWindowTests.SmallRun(root, name, seconds: 20, checkpointEvery: 5))
            {
                "EVOSIM_RESUME=" + source,
                "EVOSIM_RESUME_AT=10",
                "EVOSIM_DIGEST_EVERY=25",
            };
            args.AddRange(extra);
            Assert.Equal(0, Program.Main(args.ToArray()));
            return RunDirectoryIn(root, name);
        }

        [Fact]
        public void ACousinIsMarkedAndAnUnchangedConfigResumesBitIdentical()
        {
            string sourceRoot = Fresh("source");
            var founding = new List<string>(FilmWindowTests.SmallRun(sourceRoot, "source", seconds: 15, checkpointEvery: 5))
            {
                "EVOSIM_FLOOR_CLOSES=2",
                "EVOSIM_TRICKLE=1/4",
                "EVOSIM_DIGEST_EVERY=25",
            };
            Assert.Equal(0, Program.Main(founding.ToArray()));
            string source = RunDirectoryIn(sourceRoot, "source");
            RunConfig sourceConfig = RunDirectory.ReadConfig(source, out _);
            Assert.True(sourceConfig.FoundingTricklePerSecond > 0f);

            // An unchanged copy through EVOSIM_RESUME_CONFIG is the plain resume, row for row.
            string plain = Resumed("plain", source);
            string unchanged = Resumed("unchanged", source, "EVOSIM_RESUME_CONFIG=" + Saved(sourceConfig, "farm-unchanged"));

            List<string> a = DigestRows(plain), b = DigestRows(unchanged);
            _out.WriteLine($"plain {a.Count} digest rows, unchanged copy {b.Count}");
            Assert.True(a.Count >= 10, "too few digest rows to compare");
            Assert.Equal(a, b);

            JsonNode unchangedRun = Json.Parse(File.ReadAllText(Path.Combine(unchanged, "run.json")));
            Assert.False(unchangedRun["resumedFrom"].Has("cousin"));
            Assert.Equal(sourceConfig.Hash(), unchangedRun["configHash"].AsString());

            // A dial and the trickle off: a cousin, both hashes kept, both changes named.
            RunConfig edited = RunConfigJson.Read(RunConfigJson.Write(sourceConfig));
            edited.WorkCostMultiplier = 3f;
            string cousin = Resumed("cousin", source,
                "EVOSIM_RESUME_CONFIG=" + Saved(edited, "farm-dial"), "EVOSIM_RESUME_TRICKLE_OFF=1");

            JsonNode cousinRun = Json.Parse(File.ReadAllText(Path.Combine(cousin, "run.json")));
            JsonNode from = cousinRun["resumedFrom"];
            Assert.True(from["cousin"].AsBool());
            Assert.Equal(sourceConfig.Hash(), from["sourceConfigHash"].AsString());
            Assert.NotEqual(sourceConfig.Hash(), cousinRun["configHash"].AsString());
            Assert.False(from["sourceMismatch"].AsBool());

            string[] changes = from["changes"].Items().Select(c => c.AsString()).ToArray();
            _out.WriteLine("cousin changes: " + string.Join("; ", changes));
            Assert.Equal(2, changes.Length);

            RunConfig written = RunDirectory.ReadConfig(cousin, out _);
            Assert.Equal(3f, written.WorkCostMultiplier);
            Assert.Equal(0f, written.FoundingTricklePerSecond);
            Assert.Equal(written.Hash(), cousinRun["configHash"].AsString());
            Assert.NotEqual(a, DigestRows(cousin));

            // The world's size: refused before a directory is made.
            RunConfig wider = RunConfigJson.Read(RunConfigJson.Write(sourceConfig));
            wider.WorldAreaSquareMetres = sourceConfig.WorldAreaSquareMetres * 2f;
            string refusedRoot = Fresh("refused");
            var refused = new List<string>(FilmWindowTests.SmallRun(refusedRoot, "refused", seconds: 20, checkpointEvery: 5))
            {
                "EVOSIM_RESUME=" + source,
                "EVOSIM_RESUME_AT=10",
                "EVOSIM_RESUME_CONFIG=" + Saved(wider, "farm-wider"),
            };
            Assert.NotEqual(0, Program.Main(refused.ToArray()));
            Assert.False(Directory.Exists(Path.Combine(refusedRoot, "refused")));
        }

        [Fact]
        public void TheCousinSwitchesNeedAResume()
        {
            string root = Fresh("founded");
            var args = new List<string>(FilmWindowTests.SmallRun(root, "founded", seconds: 2, checkpointEvery: 0))
            {
                "EVOSIM_RESUME_TRICKLE_OFF=1",
            };
            Assert.NotEqual(0, Program.Main(args.ToArray()));
            Assert.False(Directory.Exists(Path.Combine(root, "founded")));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The registry (nursery-general-build.md, section 5): which files are results, an entry's line
    /// reads back to itself, entries are numbered in the order appended, a rerun's digests are
    /// compared down to the first differing line, and a build's tree hashes are checked.
    /// </summary>
    public class RegistryTests : IDisposable
    {
        private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "registry-" + Guid.NewGuid().ToString("N"));

        public RegistryTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, true);

        private string Folder(string name, params (string file, string text)[] files)
        {
            string path = Path.Combine(_dir, name);
            Directory.CreateDirectory(path);
            foreach ((string file, string text) in files) File.WriteAllText(Path.Combine(path, file), text);
            return path;
        }

        private static RegistryEntry Entry(string outFolder) => new RegistryEntry
        {
            Recipe = "brain2", RecipeHash = "ab12", RecipePath = "rounds/nursery/brain2.json", ConfigHash = "7319d6b528cc77f1",
            GitCommit = "dc5dd6f", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n",
            Out = outFolder, Status = "completed", StartedAtUtc = "2026-10-02T00:51:00Z", EndedAtUtc = "2026-10-02T14:00:00Z",
            Digests = RegistryEntry.DigestsOf(outFolder),
        };

        [Theory]
        [InlineData("tank-generations.jsonl", true)]
        [InlineData("tank-exam-genomes.tsv", true)]
        [InlineData("tank-scores.jsonl", true)]
        [InlineData("run.json", false)]
        [InlineData("recipe.json", false)]
        [InlineData("timing.jsonl", false)]
        [InlineData("nursery.log", false)]
        [InlineData("pilot.err", false)]
        [InlineData("stdout.txt", false)]
        [InlineData("STOP", false)]     // the stop file asks a run to end; it is not something the run wrote
        [InlineData("stop", false)]
        [InlineData("STOP.txt", false)]
        [InlineData("STOPPED-genomes.tsv", true)]
        public void OnlyResultsAreDigested(string name, bool result) => Assert.Equal(result, RegistryEntry.IsResult(name));

        [Fact]
        public void AnEntryReadsBackToItself()
        {
            string run = Folder("run", ("tank-generations.jsonl", "{\"gen\":0}\n"), ("run.json", "{}"));
            RegistryEntry e = Entry(run);
            Assert.Single(e.Digests);
            e.RerunOf = 3;
            e.Identical = false;
            string line = e.ToJson();
            Assert.DoesNotContain("\n", line);
            Assert.Equal(line, RegistryEntry.FromJson(line).ToJson());
        }

        [Fact]
        public void AReferenceEntryReadsBackToItself()
        {
            RegistryEntry e = Entry(Folder("old", ("tank-best.jsonl", "g\n")));
            e.Reference = new RegistryEntry.ReferenceNote
            {
                Build = "commit dc5dd6f, nursery-v10",
                Verdict = "generations 0 to 2 identical",
                KnownDifferences = new[] { "seconds in the generation rows", "no tank-scores.jsonl" },
            };
            string line = e.ToJson();
            RegistryEntry back = RegistryEntry.FromJson(line);
            Assert.Equal(line, back.ToJson());
            Assert.Equal(2, back.Reference.KnownDifferences.Length);
            Assert.Null(RegistryEntry.FromJson(Entry(Folder("new")).ToJson()).Reference);
        }

        [Fact]
        public void EntriesAreNumberedInTheOrderAppended()
        {
            string file = Path.Combine(_dir, "rounds", "nursery", "registry.jsonl");
            Assert.Empty(RegistryEntry.ReadAll(file));
            string run = Folder("run", ("tank-best.jsonl", "x\n"));
            Assert.Equal(1, RegistryEntry.Append(file, Entry(run)));
            Assert.Equal(2, RegistryEntry.Append(file, Entry(run)));
            Assert.Equal(2, RegistryEntry.ReadAll(file).Count);
        }

        [Fact]
        public void ALastLineWithoutItsNewlineGetsOne()
        {
            string file = Path.Combine(_dir, "registry.jsonl");
            string run = Folder("run", ("tank-best.jsonl", "x\n"));
            File.WriteAllText(file, Entry(run).ToJson());   // no newline at the end
            Assert.Equal(2, RegistryEntry.Append(file, Entry(run)));
            Assert.Equal(2, RegistryEntry.ReadAll(file).Count);
        }

        [Fact]
        public void ALineThatDoesNotReadIsRefusedWithItsNumber()
        {
            string file = Path.Combine(_dir, "registry.jsonl");
            File.WriteAllText(file, Entry(Folder("run")).ToJson() + "\n{\"recipe\": \"x\"}\n");
            Assert.Contains("entry 2 does not read", Assert.Throws<InvalidDataException>(() => RegistryEntry.ReadAll(file)).Message);
        }

        [Fact]
        public void ADirtyRunReadsBackAsNotRerunnable()
        {
            RegistryEntry e = Entry(Folder("run"));
            e.Rerunnable = false;
            e.NotRerunnableBecause = "the tree it ran from was dirty";
            RegistryEntry back = RegistryEntry.FromJson(e.ToJson());
            Assert.False(back.Rerunnable);
            Assert.Equal("the tree it ran from was dirty", back.NotRerunnableBecause);
        }

        [Fact]
        public void ARerunIsComparedDownToItsFirstDifferentLine()
        {
            string old = Folder("old", ("tank-generations.jsonl", "a\nb\nc\n"), ("tank-best.jsonl", "g\n"), ("timing.jsonl", "1\n"));
            RegistryEntry e = Entry(old);

            string same = Folder("same", ("tank-generations.jsonl", "a\nb\nc\n"), ("tank-best.jsonl", "g\n"), ("timing.jsonl", "2\n"));
            Assert.Null(e.FirstDifference(same, RegistryEntry.DigestsOf(same)));   // wall time is not a result

            string moved = Folder("moved", ("tank-generations.jsonl", "a\nB\nc\n"), ("tank-best.jsonl", "g\n"));
            string first = e.FirstDifference(moved, RegistryEntry.DigestsOf(moved));
            Assert.StartsWith("tank-generations.jsonl: line 2 differs", first);

            string fewer = Folder("fewer", ("tank-generations.jsonl", "a\nb\nc\n"));
            Assert.Equal("tank-best.jsonl: written by the entry, not by the rerun", e.FirstDifference(fewer, RegistryEntry.DigestsOf(fewer)));
        }

        [Fact]
        public void AnEntryWrittenBeforeThePopulationFileIsNotDifferentForIt()
        {
            RegistryEntry e = Entry(Folder("old", ("tank-best.jsonl", "g\n")));
            string rerun = Folder("rerun", ("tank-best.jsonl", "g\n"), ("tank-population.jsonl", "p\n"));
            Assert.Null(e.FirstDifference(rerun, RegistryEntry.DigestsOf(rerun)));

            // Any other file the entry lacks still counts, and an entry that holds the file is compared on it.
            string extra = Folder("extra", ("tank-best.jsonl", "g\n"), ("tank-other.jsonl", "p\n"));
            Assert.Equal("tank-other.jsonl: written by the rerun, not by the entry", e.FirstDifference(extra, RegistryEntry.DigestsOf(extra)));
            RegistryEntry n = Entry(Folder("new", ("tank-best.jsonl", "g\n"), ("tank-population.jsonl", "q\n")));
            Assert.StartsWith("tank-population.jsonl: ", n.FirstDifference(rerun, RegistryEntry.DigestsOf(rerun)));
        }

        [Fact]
        public void TheEntrysBuildIsChecked()
        {
            RegistryEntry e = Entry(Folder("run"));
            var same = new RunRecord { CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n" };
            Assert.Empty(e.BuildDifferences(same));
            var other = new RunRecord { CoreHash = "c", DynamicsHash = "d", FarmHash = "x", NurseryHash = "y" };
            Assert.Equal(new[] { "farm", "nursery" }, e.BuildDifferences(other));

            // A tree that could not be hashed matches nothing.
            e.CoreHash = "unknown";
            Assert.Contains("core", e.BuildDifferences(new RunRecord { CoreHash = "unknown", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n" }));
        }
    }
}

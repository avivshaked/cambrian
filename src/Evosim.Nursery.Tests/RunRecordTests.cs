using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The run record (nursery-general-build.md, section 1): the document parses, every option
    /// reaches it, values a JSON reader cannot take are written as null, the second write replaces
    /// the first, and the build's half names a commit, four tree hashes and the process.
    /// </summary>
    public class RunRecordTests
    {
        private sealed class Sample
        {
            public string Name = "a \"quoted\" path\\x";
            public double Missing = double.NaN, Half = 0.5, Endless = double.PositiveInfinity;
            public int Count = 3;
            public ulong Seed = 0x67656EUL;
            public bool On = true;
            public float Small = 0.1f;
            public string[] Groups = { "Scent", "Stroke,Hold,Ramp" };
            public SensorChannel[] Channels = { SensorChannel.Scent, SensorChannel.Chemical };
            public string Unset;
            private int _hidden = 7;
            public int Hidden => _hidden;
        }

        private static RunRecord Filled()
        {
            return new RunRecord
            {
                Args = new[] { "--tank", "--out", "x" },
                Options = RunRecord.FieldsOf(new Sample()),
                ConfigPath = "c.json", ConfigFileSha256 = "ab", ConfigHash = "cd", EpisodeConfigHash = "ef",
                EpisodeConfigJson = "{\"a\": 1}",
                Seed = 1, Threads = 2,
                Salts = new List<KeyValuePair<string, ulong>> { new KeyValuePair<string, ulong>("generations", 0x67656EUL) },
                GitCommit = "abc", CoreHash = "1", DynamicsHash = "2", FarmHash = "3", NurseryHash = "4",
            };
        }

        [Fact]
        public void EveryPublicFieldIsWrittenInOrder()
        {
            List<KeyValuePair<string, string>> fields = RunRecord.FieldsOf(new Sample());
            Assert.Equal(
                new[] { "Name", "Missing", "Half", "Endless", "Count", "Seed", "On", "Small", "Groups", "Channels", "Unset" },
                fields.Select(kv => kv.Key).ToArray());
        }

        [Fact]
        public void TheDocumentParsesAndKeepsTheValues()
        {
            JsonNode doc = Json.Parse(Filled().Render());
            Assert.Equal("running", doc["status"].AsString());
            Assert.Equal(JsonNode.NodeKind.Null, doc["exitCode"].Kind);
            Assert.Equal("--out", doc["args"][1].AsString());

            JsonNode o = doc["options"];
            Assert.Equal("a \"quoted\" path\\x", o["Name"].AsString());
            Assert.Equal(JsonNode.NodeKind.Null, o["Missing"].Kind);
            Assert.Equal(JsonNode.NodeKind.Null, o["Endless"].Kind);
            Assert.Equal(0.5, o["Half"].AsDouble());
            Assert.Equal(0x67656EUL, o["Seed"].AsULong());
            Assert.True(o["On"].AsBool());
            Assert.Equal(0.1f, o["Small"].AsFloat());
            Assert.Equal("Stroke,Hold,Ramp", o["Groups"][1].AsString());
            Assert.Equal("Chemical", o["Channels"][1].AsString());
            Assert.Equal(JsonNode.NodeKind.Null, o["Unset"].Kind);

            Assert.Equal(0x67656EUL, doc["salts"]["generations"].AsULong());
            Assert.Equal(1, doc["config"]["episode"]["a"].AsInt());
            Assert.Equal("4", doc["build"]["nurseryHash"].AsString());
        }

        [Fact]
        public void TheEndingReplacesTheOpening()
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "run-record-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                RunRecord r = Filled();
                r.Write(dir);
                Assert.Equal("running", Json.Parse(File.ReadAllText(Path.Combine(dir, "run.json")))["status"].AsString());

                r.End(0);
                r.Write(dir);
                JsonNode doc = Json.Parse(File.ReadAllText(Path.Combine(dir, "run.json")));
                Assert.Equal("completed", doc["status"].AsString());
                Assert.Equal(0, doc["exitCode"].AsInt());
                Assert.False(File.Exists(Path.Combine(dir, "run.json.tmp")));

                r.Fail(new InvalidOperationException("boom"));
                doc = Json.Parse(r.Render());
                Assert.Equal("failed", doc["status"].AsString());
                Assert.Equal(JsonNode.NodeKind.Null, doc["exitCode"].Kind);
                Assert.Equal("InvalidOperationException: boom", doc["error"].AsString());

                r.End(1);
                doc = Json.Parse(r.Render());
                Assert.Equal("exited", doc["status"].AsString());
                Assert.Equal(1, doc["exitCode"].AsInt());

                Assert.Null(r.TryWrite(dir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static string Fresh(string name)
        {
            string dir = Path.Combine(AppContext.BaseDirectory, name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void TheStopFileIsSeenByItsNameInTheRunsFolderAndNowhereElse()
        {
            string dir = Fresh("stop-file");
            try
            {
                Assert.False(RunRecord.StopRequested(dir));
                Assert.False(RunRecord.StopRequested(null));
                Assert.False(RunRecord.StopRequested(Path.Combine(dir, "no-such-folder")));
                File.WriteAllText(Path.Combine(dir, "STOPPED.txt"), "");
                File.WriteAllText(Path.Combine(dir, "run.json"), "{}");
                Assert.False(RunRecord.StopRequested(dir));
                File.WriteAllText(Path.Combine(dir, RunRecord.StopFileName), "");
                Assert.True(RunRecord.StopRequested(dir));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void AStoppedRunIsRecordedAsStoppedWithExitZeroAndAnEndTime()
        {
            string dir = Fresh("stopped-record");
            try
            {
                RunRecord r = Filled();
                r.Write(dir);
                r.Stop();
                Assert.Null(r.TryWrite(dir));
                JsonNode doc = Json.Parse(File.ReadAllText(Path.Combine(dir, "run.json")));
                Assert.Equal("stopped", doc["status"].AsString());
                Assert.Equal(0, doc["exitCode"].AsInt());
                Assert.NotEqual(JsonNode.NodeKind.Null, doc["endedAtUtc"].Kind);
                Assert.Equal(JsonNode.NodeKind.Null, doc["error"].Kind);
                // A record carries the stop file's name, so that a reader can tell a build that reads it from one that does not.
                Assert.Equal("STOP", doc["stopFile"].AsString());
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void TheListOfAppendedFilesIsReadWithItsCommentsAndNamesTheFileWhenItCannotBe()
        {
            string root = Fresh("appended");
            try
            {
                string dir = Path.Combine(root, "rounds", "nursery");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "appended.txt");

                var missing = Assert.Throws<InvalidDataException>(() => RunRecord.AppendedPaths(root));
                Assert.Contains("appended.txt", missing.Message);

                File.WriteAllText(file, "# a comment\n\nrounds/nursery/registry.jsonl\n  rounds/nursery/activity.jsonl  \n# another\n");
                Assert.Equal(new[] { "rounds/nursery/registry.jsonl", "rounds/nursery/activity.jsonl" }, RunRecord.AppendedPaths(root).ToArray());

                File.WriteAllText(file, "# nothing but a comment\n");
                Assert.Contains("lists no file", Assert.Throws<InvalidDataException>(() => RunRecord.AppendedPaths(root)).Message);

                foreach (string bad in new[] { "/rounds/x", "../x", "a/../../x", "C:/x", "a b", "rounds\\x", "a*" })
                {
                    File.WriteAllText(file, bad + "\n");
                    var e = Assert.Throws<InvalidDataException>(() => RunRecord.AppendedPaths(root));
                    Assert.Contains("appended.txt", e.Message);
                }

                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Assert.Contains("appended.txt", Assert.Throws<InvalidDataException>(() => RunRecord.AppendedPaths(root)).Message);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void TheCommittedListNamesTheRegistryAndTheTreeBuildReadsIt()
        {
            string root = Recipe.RepoRootAbove(AppContext.BaseDirectory);
            Assert.Contains(RegistryEntry.RelativePath, RunRecord.AppendedPaths(root));
            Assert.Contains("rounds/nursery/activity.jsonl", RunRecord.AppendedPaths(root));
        }

        [Fact]
        public void AStopFileInARunsFolderIsNotDigestedAsAResult()
        {
            string dir = Fresh("stop-digest");
            try
            {
                File.WriteAllText(Path.Combine(dir, "tank-generations.jsonl"), "{}\n");
                File.WriteAllText(Path.Combine(dir, "run.json"), "{}");
                var before = RegistryEntry.DigestsOf(dir);
                File.WriteAllText(Path.Combine(dir, RunRecord.StopFileName), "stop requested");
                Assert.Equal(before.Keys.ToArray(), RegistryEntry.DigestsOf(dir).Keys.ToArray());
                Assert.Equal(new[] { "tank-generations.jsonl" }, before.Keys.ToArray());
            }
            finally { Directory.Delete(dir, true); }
        }

        private static string Git(string dir, params string[] args)
        {
            var info = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (string a in args) info.ArgumentList.Add(a);
            using (var p = System.Diagnostics.Process.Start(info))
            {
                var output = p.StandardOutput.ReadToEndAsync();
                var error = p.StandardError.ReadToEndAsync();
                Assert.True(p.WaitForExit(30000), "git " + string.Join(" ", args) + " did not finish");
                Assert.True(p.ExitCode == 0, "git " + string.Join(" ", args) + ": " + error.Result);
                return output.Result;
            }
        }

        /// <summary>A folder holding a git repository: its object files are read-only on Windows, so the attributes go first.</summary>
        private static void DeleteRepository(string dir)
        {
            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }

        [Fact]
        public void TheDirtyCheckLeavesOutTheFilesTheListNamesAndSeesAnyOtherNewFile()
        {
            // a repository of its own under the build output: the pathspec the nursery builds from rounds/nursery/appended.txt, run for real
            string root = Fresh("dirty-check");
            try
            {
                string nursery = Path.Combine(root, "rounds", "nursery");
                Directory.CreateDirectory(nursery);
                File.WriteAllText(Path.Combine(nursery, "appended.txt"), "# the files a run appends to\nrounds/nursery/registry.jsonl\nrounds/nursery/activity.jsonl\n");
                File.WriteAllText(Path.Combine(nursery, "kept.json"), "{}\n");
                Git(root, "init", "-q");
                Git(root, "add", "-A");
                Git(root, "-c", "user.name=test", "-c", "user.email=nursery-test", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "first");

                var clean = new RunRecord();
                clean.ReadBuild(root);
                Assert.True(clean.GitDirty == false);

                // the two appended files, untracked: still clean
                File.WriteAllText(Path.Combine(nursery, "activity.jsonl"), "{\"row\":1}\n");
                File.WriteAllText(Path.Combine(nursery, "registry.jsonl"), "{\"entry\":1}\n");
                var appended = new RunRecord();
                appended.ReadBuild(root);
                Assert.True(appended.GitDirty == false);

                // any other new file under rounds/ is an edit
                File.WriteAllText(Path.Combine(nursery, "other.json"), "{}\n");
                var dirty = new RunRecord();
                dirty.ReadBuild(root);
                Assert.True(dirty.GitDirty == true);

                // and a tracked file that changed is one too, whatever the list holds
                File.Delete(Path.Combine(nursery, "other.json"));
                File.WriteAllText(Path.Combine(nursery, "kept.json"), "{\"changed\":true}\n");
                var edited = new RunRecord();
                edited.ReadBuild(root);
                Assert.True(edited.GitDirty == true);
            }
            finally { DeleteRepository(root); }
        }

        [Fact]
        public void AFoundRepositoryRootWhoseListIsMissingStillRefusesTheBuildReadingByNamingTheFile()
        {
            string root = Fresh("no-list");
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "rounds", "nursery"));
                var e = Assert.Throws<InvalidDataException>(() => new RunRecord().ReadBuild(root));
                Assert.Contains("appended.txt", e.Message);
            }
            finally { Directory.Delete(root, true); }
        }

        [Fact]
        public void TheBuildNamesACommitAndFourTrees()
        {
            // The tests run from artifacts/ inside the checkout, so the root is found by walking up
            // from the working directory or, failing that, from the test's own folder.
            var r = new RunRecord();
            r.ReadBuild(null);
            Assert.Matches("^[0-9a-f]{40}$", r.GitCommit);
            Assert.NotNull(r.GitDirty);
            Assert.True(Directory.Exists(Path.Combine(r.RepoRoot, "src", "Evosim.Nursery")));
            foreach (string h in new[] { r.CoreHash, r.DynamicsHash, r.FarmHash, r.NurseryHash })
                Assert.Matches("^[0-9a-f]{64}$", h);
            Assert.NotEqual(r.CoreHash, r.NurseryHash);

            // The process, for telling a killed run from a live one.
            using (var self = System.Diagnostics.Process.GetCurrentProcess())
            {
                Assert.Equal(self.Id, r.ProcessId);
                Assert.Equal(self.StartTime.ToUniversalTime(), DateTime.Parse(r.ProcessStartedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind));
            }
            JsonNode doc = Json.Parse(r.Render());
            Assert.Equal(r.ProcessId, doc["processId"].AsInt());
        }
    }
}

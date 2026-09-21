using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// <c>run.json</c>: the two writes, the fields the scripts read, and the tree digest.
    /// </summary>
    public class ManifestTests : IDisposable
    {
        /// <summary>
        /// Everything this test class writes, under the build output inside the worktree.
        /// </summary>
        /// <remarks>
        /// Not the system temp directory: nothing of the project's is written outside the
        /// repository, TEMP included (CLAUDE.md's conventions). <c>artifacts/</c> is gitignored
        /// and is where <c>src/Directory.Build.props</c> already sends every binary.
        /// </remarks>
        private readonly string _root = Path.Combine(
            AppContext.BaseDirectory, "test-out", Guid.NewGuid().ToString("N"));

        public ManifestTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { }
        }

        private static RunManifest Sample() => new RunManifest
        {
            ArmName = "r43-s1",
            Seed = 7,
            RequestedSeconds = 30000f,
            RequestedWallMinutes = 1800f,
            ConfigHash = "ff557bce2685293a",
            PhysicsStepSeconds = 0.01f,
            MetabolicStepSeconds = 0.5f,
            EngineVersion = "1.2.3.4",
            Threads = 24,
            GitCommit = "83ea184181e865659b18c8c71b3d6382919c87cd",
            GitDirty = false,
            CoreHash = new string('a', 64),
            DynamicsHash = new string('b', 64),
            FarmHash = new string('c', 64),
            ProgramPath = @"D:\Projects\experiments\evolution-simulator\artifacts\Evosim.Farm\bin",
            RepoRoot = @"D:\Projects\experiments\evolution-simulator",
        };

        private static RunEnding SampleEnding() => new RunEnding
        {
            Status = "ended",
            Reason = "budget",
            Prose = "budget reached",
            SimulatedSeconds = 30000,
            PhysicsSteps = 3000000,
            Births = 8823,
            Alive = 934,
            WallClockMinutes = 825.85511611166669,
            TimesRealTime = 0.6054330720309945,
            DragImpulsesLimited = 0,
            DriveImpulsesLimited = 0,
            DivergedTotal = 0,
            MatterInfluxedTotal = 183.37856225729007,
            MatterBuriedTotal = 0,
            BestSpeed = 1.242867112159729,
            BestSpeedAtSeconds = 14.5,
            SharedSpace = true,
            Wraps = 0,
            Crowded = 189,
            ContactPairsPerStep = 463.653692,
            ContactPairsJointed = 707006207,
            ContactPairsPersistent = 1390220158,
            ContactBodies = 958548352,
            MaxJointMassRatio = 54.565940856933594,
            BodiesOverMassRatio10 = 617,
            WallPhysicsMs = 18222500,
            WallWorldMs = 4684679,
            WallHarnessMs = 26638293,
            WallWritersMs = 3208,
            WallTotalMs = 49551306,
            HarnessPhases = new[] { "read", "control", "fluid", "other" },
            WallHarnessPhaseMs = new long[] { 7320536, 5961472, 8557059, 1401 },
            HarnessBodySteps = 2309857800,
            WallFluidGatherMs = 1075764,
            WallFluidWaterMs = 1242089,
            WallFluidComputeMs = 3622617,
            WallFluidApplyMs = 2615636,
            FluidLinkSteps = 5960466800,
        };

        /// <summary>The creation half, and the fields a script reaches for by name.</summary>
        [Fact]
        public void TheRunningManifestCarriesTheRunsIdentity()
        {
            JsonNode m = Json.Parse(Manifest.Render(Sample(), ending: null));

            Assert.Equal("r43-s1", m["arm"].AsString());
            Assert.Equal(7, m["seed"].AsDouble());
            Assert.Equal("dynamics", m["engine"].AsString());
            Assert.Equal("1.2.3.4", m["engineVersion"].AsString());
            Assert.Equal(0.01, m["physicsDtSeconds"].AsDouble(), 6);
            Assert.Equal(0.5, m["metabolicStepSeconds"].AsDouble(), 6);
            Assert.Equal(24, m["threads"].AsDouble());
            Assert.Equal("ff557bce2685293a", m["configHash"].AsString());
            Assert.Equal("running", m["status"].AsString());

            // stop-arm.ps1 merges into this document and keeps every creation field, so the
            // source block has to be there from the first write.
            JsonNode source = m["source"];
            Assert.Equal(new string('a', 64), source["coreHash"].AsString());
            Assert.Equal(new string('b', 64), source["dynamicsHash"].AsString());
            Assert.Equal(new string('c', 64), source["farmHash"].AsString());
            Assert.False(source["gitDirty"].AsBool());

            // The Unity build's two names are gone, and neither may be quietly present.
            Assert.False(m.Has("unityVersion"));
            Assert.False(m.Has("simHash"));
            Assert.False(source.Has("simHash"));

            // A run that has not ended says nothing about how it ended.
            Assert.False(m.Has("reason"));
            Assert.False(m.Has("endedAt"));
            Assert.False(m.Has("simulatedSeconds"));
        }

        /// <summary>The ending block, field for field the Unity build's.</summary>
        [Fact]
        public void TheEndedManifestCarriesTheFootersFactsAsData()
        {
            JsonNode m = Json.Parse(Manifest.Render(Sample(), SampleEnding()));

            Assert.Equal("ended", m["status"].AsString());
            Assert.Equal("budget", m["reason"].AsString());
            Assert.Equal("budget reached", m["ending"].AsString());
            Assert.Equal(30000, m["simulatedSeconds"].AsDouble());
            Assert.Equal(3000000, m["physicsSteps"].AsDouble());
            Assert.Equal(8823, m["births"].AsDouble());
            Assert.Equal(934, m["aliveAtEnd"].AsDouble());
            Assert.Equal(0.6054330720309945, m["timesRealTime"].AsDouble(), 12);
            Assert.Equal(463.653692, m["contactPairsPerStep"].AsDouble(), 6);
            Assert.Equal(707006207, m["contactPairsJointed"].AsDouble());
            Assert.Equal(54.565940856933594, m["maxJointMassRatio"].AsDouble(), 9);
            Assert.Equal(49551306, m["wallTotalMs"].AsDouble());

            // The harness profile is written under the same names the statistics rows carry.
            Assert.Equal(7320536, m["wallHarnessReadMs"].AsDouble());
            Assert.Equal(5961472, m["wallHarnessControlMs"].AsDouble());
            Assert.Equal(8557059, m["wallHarnessFluidMs"].AsDouble());
            Assert.Equal(1401, m["wallHarnessOtherMs"].AsDouble());
            Assert.Equal(2309857800, m["harnessBodySteps"].AsDouble());
            Assert.Equal(5960466800, m["fluidLinkSteps"].AsDouble());
        }

        /// <summary>
        /// The error path writes the last facts the loop recorded, never zeros.
        /// </summary>
        [Fact]
        public void TheErrorEndingReportsWhatWasLastTrue()
        {
            RunManifest m = Sample();
            m.LastSimulatedSeconds = 15345;
            m.LastPhysicsSteps = 1534500;
            m.LastBirths = 4000;
            m.LastAlive = 1707;
            m.LastWallClockMinutes = 120;
            m.LastContactPairsTotal = 3069000;

            RunEnding ending = Manifest.ErrorEnding(m, new InvalidOperationException("the grid refused a point"));
            JsonNode json = Json.Parse(Manifest.Render(m, ending));

            Assert.Equal("error", json["status"].AsString());
            Assert.Equal("error", json["reason"].AsString());
            Assert.Equal(
                "InvalidOperationException: the grid refused a point", json["ending"].AsString());
            Assert.Equal(15345, json["simulatedSeconds"].AsDouble());
            Assert.Equal(1707, json["aliveAtEnd"].AsDouble());
            Assert.Equal(2.0, json["contactPairsPerStep"].AsDouble(), 6);

            // The harness phases are absent rather than ten zeros: the manifest's cache never
            // carried them, and a zero that means "not measured" is worse than no field at all.
            Assert.False(json.Has("wallHarnessReadMs"));
            Assert.False(json.Has("harnessBodySteps"));
        }

        /// <summary>
        /// Two writes, one document: the creation half is byte-identical across them and no
        /// temporary file is left behind.
        /// </summary>
        [Fact]
        public void TheSecondWriteRewritesAndKeepsEveryCreationField()
        {
            var config = new RunConfig();
            using (RunDirectory dir = RunDirectory.Create(
                Path.Combine(_root, "r43-s1"), config, new DateTime(2026, 9, 21, 8, 0, 0, DateTimeKind.Utc)))
            {
                RunManifest m = Sample();
                Manifest.Write(dir, m, ending: null);

                string path = Path.Combine(dir.Path, "run.json");
                string running = File.ReadAllText(path);

                Assert.False(File.Exists(path + ".tmp"), "a temporary manifest was left behind");
                Assert.Contains("\"status\": \"running\"", running);

                Manifest.Write(dir, m, SampleEnding());
                string ended = File.ReadAllText(path);

                Assert.False(File.Exists(path + ".tmp"), "a temporary manifest was left behind");

                // Everything up to `status` is the creation half and must not have moved a byte.
                int cut = running.IndexOf("\"status\"", StringComparison.Ordinal);
                Assert.True(cut > 0);
                Assert.Equal(running.Substring(0, cut), ended.Substring(0, cut));

                JsonNode json = Json.Parse(ended);
                Assert.Equal("ended", json["status"].AsString());
                Assert.Equal(m.StartedAtUtc, json["startedAt"].AsString());

                // No BOM: the scripts read these with ConvertFrom-Json and Python's json, and one
                // of the two chokes on a byte-order mark.
                byte[] bytes = File.ReadAllBytes(path);
                Assert.False(bytes.Length > 2 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            }
        }

        /// <summary>
        /// The tree digest is over bytes, so a line ending changes it. Deliberately.
        /// </summary>
        /// <remarks>
        /// <c>simHash</c> is a property of a checkout and not of a commit, and two checkouts of one
        /// commit that differ by carriage returns are two different trees to this function — which
        /// is exactly what caught round 41c's worktree (CLAUDE.md). Normalising the line endings
        /// would make the digest agree across checkouts and stop it identifying the bytes that
        /// ran, so the port keeps it byte-exact and this test is the statement of that choice.
        /// </remarks>
        [Fact]
        public void TheTreeDigestIsOverBytesAndNotOverLines()
        {
            string lf = Path.Combine(_root, "lf");
            string crlf = Path.Combine(_root, "crlf");
            Directory.CreateDirectory(lf);
            Directory.CreateDirectory(crlf);

            File.WriteAllText(Path.Combine(lf, "A.cs"), "class A\n{\n}\n");
            File.WriteAllText(Path.Combine(crlf, "A.cs"), "class A\r\n{\r\n}\r\n");

            string a = Manifest.HashSourceTree(lf);
            string b = Manifest.HashSourceTree(crlf);

            Assert.NotNull(a);
            Assert.NotEqual(a, b);
            Assert.Equal(a, Manifest.HashSourceTree(lf));
            Assert.Equal(64, a.Length);
        }

        /// <summary>
        /// The digest covers paths as well as contents, and only <c>.cs</c>.
        /// </summary>
        /// <remarks>
        /// Paths are in so that moving a file changes the digest; per-file digests are in so the
        /// boundary between two files cannot be forged by concatenation; and the extension is
        /// matched rather than globbed, because <c>"*.cs"</c> also matches <c>.csproj</c> through
        /// 8.3 short names on Windows — a difference that would refuse every launch for a reason
        /// nobody could see.
        /// </remarks>
        [Fact]
        public void TheTreeDigestReadsPathsAndOnlyCsFiles()
        {
            string one = Path.Combine(_root, "one");
            string two = Path.Combine(_root, "two");
            Directory.CreateDirectory(Path.Combine(one, "sub"));
            Directory.CreateDirectory(Path.Combine(two, "sub"));

            File.WriteAllText(Path.Combine(one, "A.cs"), "x");
            File.WriteAllText(Path.Combine(two, "sub", "A.cs"), "x");
            Assert.NotEqual(Manifest.HashSourceTree(one), Manifest.HashSourceTree(two));

            string before = Manifest.HashSourceTree(one);
            File.WriteAllText(Path.Combine(one, "A.csproj"), "<Project/>");
            Assert.Equal(before, Manifest.HashSourceTree(one));

            Assert.Null(Manifest.HashSourceTree(Path.Combine(_root, "nothing-here")));
        }

        /// <summary>
        /// A manifest built against this worktree carries real hashes and a real commit.
        /// </summary>
        /// <remarks>
        /// The one test here that runs git and hashes the tree it is built from — the rest are
        /// over a hand-made manifest, because a provenance failure must never be able to fail a
        /// round-trip test and hide behind it.
        /// </remarks>
        [Fact]
        public void BuildReadsTheSourceItIsAboutToRun()
        {
            string repoRoot = RepoRoot();
            var settings = EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
            {
                { "EVOSIM_OUT", Path.Combine(_root, "r43-s1.md") },
                { "EVOSIM_REPO_ROOT", repoRoot },
                { "EVOSIM_SEED", "3" },
            }));

            RunManifest m = Manifest.Build(settings, "deadbeefdeadbeef", null, 0.01f, 50, 8);

            Assert.Equal("r43-s1", m.ArmName);
            Assert.Equal(3UL, m.Seed);
            Assert.Equal(8, m.Threads);
            Assert.Equal(repoRoot, m.RepoRoot);
            Assert.Equal(64, m.CoreHash.Length);
            Assert.Equal(64, m.DynamicsHash.Length);
            Assert.Equal(64, m.FarmHash.Length);
            Assert.NotEqual(m.CoreHash, m.DynamicsHash);
            Assert.True(m.ProcessId > 0);

            // git may not be on PATH on some machine; that is a note, never a failure.
            Assert.True(m.GitCommit == "unknown" || m.GitCommit.Length == 40);
        }

        /// <summary>The worktree this test binary was built in.</summary>
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "src", "Evosim.Farm")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("no src/Evosim.Farm above " + AppContext.BaseDirectory);
        }
    }
}

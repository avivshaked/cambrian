using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>The machine's processors are read once, from the system, and every check of an affinity mask uses that one value.</summary>
    public class SystemMaskTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private static string Launch(string name, int threads = 2) =>
            Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name, threads, reason = "a test" });

        [Fact]
        public void TheSystemMaskIsTheMachinesAndHoldsEveryProcessorThisProcessMayUse()
        {
            if (!OperatingSystem.IsWindows()) return;
            ulong system = SystemProcessors.Read();
            Assert.NotEqual(0UL, system);
            ulong own;
            using (var self = System.Diagnostics.Process.GetCurrentProcess()) own = (ulong)(long)self.ProcessorAffinity;
            // a process pinned to fewer processors (a pinned shell) still sees the machine's: its own mask is inside the system's, never the other way
            Assert.Equal(0UL, own & ~system);
            Assert.True(CoreMask.Count(system) >= CoreMask.Count(own));
        }

        [Fact]
        public void ARoomWithNoCountGivenReadsItsLogicalCountFromTheSystemMask()
        {
            if (!OperatingSystem.IsWindows()) return;
            var room = new RoomContext(_fx.Root, _fx.Room.Settings, _fx.Main, _fx.Registry, Path.Combine(_fx.Dir, "other-activity.jsonl"), _fx.Launcher, _fx.Queue, _fx.Heat);
            Assert.Equal(SystemProcessors.Read(), room.SystemMask);
            Assert.Equal(CoreMask.Count(SystemProcessors.Read()), room.Logical);
        }

        [Fact]
        public void AFakeSystemMaskGivesTheLogicalCountItsPopulationEvenWithGapsInIt()
        {
            using (Fx room = Fx.WithSystemMask(0xFFFF0000UL))
            {
                Assert.Equal(0xFFFF0000UL, room.Room.SystemMask);
                Assert.Equal(16, room.Room.Logical);
            }
            Assert.Equal(32, _fx.Room.Logical);
            Assert.Equal(0xFFFFFFFFUL, _fx.Room.SystemMask);
        }

        [Fact]
        public void ACountThatDisagreesWithTheMaskIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new RoomContext(_fx.Root, _fx.Room.Settings, _fx.Main, _fx.Registry, null, _fx.Launcher, _fx.Queue, _fx.Heat, 8, null, 0xFFFFUL));
        }

        [Fact]
        public void AMaskThePretendSystemLacksIsRefusedNamingTheProcessorsAndNothingStarts()
        {
            // a machine of processors 0 to 15 only: the room.json mask 0xFFFF0000 is all processors it lacks
            using (Fx room = Fx.WithSystemMask(0xFFFFUL))
            {
                OperationResult r = room.Call("run.launch", Launch("on-a-small-machine"));
                Assert.False(r.Ok);
                Assert.Equal(400, r.Status);
                Assert.Contains("processor(s) 16, 17", r.Error);
                Assert.Contains("0xFFFF", r.Error);
                Assert.Empty(room.Launcher.Started);
                Assert.False(Directory.Exists(Path.Combine(room.RunsDir, "on-a-small-machine")));
                Assert.False(File.Exists(Path.Combine(room.RunsDir, "on-a-small-machine.launch.log")));
            }
        }

        [Fact]
        public void AMachineWhoseProcessorsAreTheHighOnesAcceptsAMaskOnThemAndRefusesOneOnTheLowOnes()
        {
            // the room's own count (the old Environment.ProcessorCount) would have said 16 here and refused bit 31 even though the machine has it
            using (Fx room = Fx.WithSystemMask(0xFFFF0000UL))
                Fx.Result(room.Call("run.launch", Launch("high-processors")));
            using (Fx room = Fx.WithSystemMask(0xFFFF0000UL, "0x3"))
            {
                OperationResult r = room.Call("run.launch", Launch("low-processors"));
                Assert.False(r.Ok);
                Assert.Contains("processor(s) 0, 1", r.Error);
            }
        }

        [Fact]
        public void TheRealLauncherChecksTheSameSourceTheRoomGaveIt()
        {
            if (!OperatingSystem.IsWindows()) return;
            string dir = Where.Fresh("one-source");
            try
            {
                var spec = new LaunchSpec
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "hostname.exe"), Arguments = new string[0],
                    WorkingDirectory = dir, Folder = Path.Combine(dir, "out"), LogFile = Path.Combine(dir, "out.launch.log"), Affinity = 0x4UL,
                };
                // a launcher told the machine has processors 0 and 1 refuses processor 2 whatever the real machine has
                var e = Assert.Throws<ArgumentException>(() => new ProcessRunLauncher(() => 0x3UL).Start(spec));
                Assert.Contains("processor(s) 2", e.Message);
                Assert.False(File.Exists(spec.LogFile));
                // and the launcher a room makes for itself is given the room's mask, not a second reading
                var room = new RoomContext(_fx.Root, _fx.Room.Settings, _fx.Main, _fx.Registry, Path.Combine(dir, "a.jsonl"), null, _fx.Queue, _fx.Heat, null, null, 0x3UL);
                var own = Assert.IsType<ProcessRunLauncher>(room.Launcher);
                e = Assert.Throws<ArgumentException>(() => own.Start(spec));
                Assert.Contains("processor(s) 2", e.Message);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
        }
    }

    public class ThreadsBeyondTheMaskTests : IDisposable
    {
        private readonly Fx _fx = Fx.WithAffinity("0x3");   // two processors
        public void Dispose() => _fx.Dispose();

        // the room's slot is the whole machine, so that the bound of two processors is what holds the threads
        public ThreadsBeyondTheMaskTests() { _fx.Queue.Text = "## Running\n\n- the room (a test) · cores: 0-31 · room\n"; }

        private static string Launch(string name, int threads) =>
            Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name, threads, reason = "a test" });

        [Fact]
        public void ALaunchOfMoreThreadsThanTheProcessorsItsMaskAllowsIsRefusedWithBothNumbers()
        {
            OperationResult r = _fx.Call("run.launch", Launch("too-many", 3));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("3 threads", r.Error);
            Assert.Contains("allows 2 processors", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "too-many")));
            Assert.False(File.Exists(Path.Combine(_fx.RunsDir, "too-many.launch.log")));
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
            // as many as the processors it allows is fine
            Fx.Result(_fx.Call("run.launch", Launch("just-enough", 2)));
            Assert.Single(_fx.Launcher.Started);
        }

        [Fact]
        public void ARerunIsHeldToTheSameNumbers()
        {
            RegistryEntry.Append(_fx.Registry, new RegistryEntry
            {
                Recipe = "brain2", RecipeHash = Recipe.Load(_fx.Full("rounds/nursery/brain2-w5.json")).Hash(), RecipePath = "rounds/nursery/brain2-w5.json",
                Out = Path.Combine(_fx.Dir, "entry-out"), Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z",
                GitCommit = "abc1234", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Rerunnable = true,
            });
            OperationResult r = _fx.Call("run.rerun", Js.Serialize(new { entry = "1", name = "again-many", threads = 3, reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Contains("3 threads", r.Error);
            Assert.Contains("allows 2 processors", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Fx.Result(_fx.Call("run.rerun", Js.Serialize(new { entry = "1", name = "again-two", threads = 2, reason = "a test" })));
        }
    }

    public class AppliedAffinityTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private static string Launch(string name) =>
            Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name, threads = 2, reason = "a test" });

        [Fact]
        public void TheResultAndTheRowRecordTheMaskTheLauncherAppliedNotTheOneAsked()
        {
            _fx.Launcher.Applied = 0x3UL;
            JsonElement r = Fx.Result(_fx.Call("run.launch", Launch("other-mask")));
            Assert.Equal("0x3", r.GetProperty("affinity").GetString());
            Assert.Contains("applied the affinity mask 0x3, not the 0xFFFF0000", r.GetProperty("launcherNote").GetString());
            JsonElement result = _fx.LastRow().GetProperty("result");
            Assert.Equal("0x3", result.GetProperty("affinity").GetString());
            Assert.Contains("applied the affinity mask 0x3", result.GetProperty("launcherNote").GetString());
        }

        [Fact]
        public void ALauncherThatPinnedNothingIsRecordedAsHavingPinnedNothing()
        {
            _fx.Launcher.Applied = 0UL;
            JsonElement r = Fx.Result(_fx.Call("run.launch", Launch("no-mask")));
            Assert.Equal(JsonValueKind.Null, r.GetProperty("affinity").ValueKind);
            Assert.Contains("applied no affinity mask", r.GetProperty("launcherNote").GetString());
        }

        [Fact]
        public void ALauncherThatAppliedWhatWasAskedAddsNoNote()
        {
            JsonElement r = Fx.Result(_fx.Call("run.launch", Launch("as-asked")));
            Assert.Equal("0xFFFF0000", r.GetProperty("affinity").GetString());
            Assert.Equal(JsonValueKind.Null, r.GetProperty("launcherNote").ValueKind);
        }
    }

    public class TrialsFolderTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private const string Trials = "scratch/nursery-trials";

        [Fact]
        public void ARecipeIsWrittenIntoTheTrialsFolderAndLaunchedFromThereAsATrial()
        {
            string path = Trials + "/zz-first.json";
            Assert.False(Directory.Exists(_fx.Full(Trials)));
            JsonElement w = Fx.Result(_fx.Call("recipe.write", _fx.Json(path)));
            Assert.True(w.GetProperty("created").GetBoolean());
            Assert.True(File.Exists(_fx.Full(path)));
            Assert.Empty(Directory.GetFiles(_fx.Full(Trials), "*.writing"));

            JsonElement r = Fx.Result(_fx.Call("run.launch", Js.Serialize(new { recipe = path, name = "from-trials", threads = 2, reason = "a test" })));
            Assert.True(r.GetProperty("trial").GetBoolean());
            Assert.Equal(path, r.GetProperty("recipe").GetString());
            Assert.True(_fx.LastRow().GetProperty("result").GetProperty("trial").GetBoolean());

            // the run uses the file, so it is not replaced while the run has not ended (the same guard as in rounds/nursery/)
            OperationResult again = _fx.Call("recipe.write", _fx.Json(path, expect: RunRecord.FileSha256(_fx.Full(path))));
            Assert.False(again.Ok);
            Assert.Contains("has not ended", again.Error);
        }

        [Theory]
        [InlineData("scratch/other/zz-no.json")]
        [InlineData("scratch/zz-no.json")]
        [InlineData("scratch/nursery-trials/sub/zz-no.json")]
        [InlineData("rounds/nursery/sub/zz-no.json")]
        [InlineData("rounds/zz-no.json")]
        public void NoOtherFolderIsWrittenInto(string path)
        {
            OperationResult r = _fx.Call("recipe.write", _fx.Json(path));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("is not a recipe the room takes", r.Error);
            Assert.Contains(Trials + "/", r.Error);
            Assert.False(File.Exists(_fx.Full(path)));
        }

        [Theory]
        [InlineData("scratch/nursery-trials/con.json", "Windows device")]
        [InlineData("scratch/nursery-trials/nul.x.json", "Windows device")]
        [InlineData("scratch/nursery-trials/a b.json", "outside A-Z")]
        [InlineData("scratch/nursery-trials/zz.json.", "ends in a dot")]
        [InlineData("scratch/nursery-trials/zz.txt", "is not a recipe the room takes")]
        [InlineData("scratch/nursery-trials/room.json", "the room's own settings")]
        public void EveryOtherRuleOfTheNamesAppliesThere(string path, string said)
        {
            OperationResult r = _fx.Call("recipe.write", _fx.Json(path));
            Assert.False(r.Ok);
            Assert.Contains(said, r.Error);
        }

        [Fact]
        public void ReplacingATrialRecipeNeedsItsHashAsAnyOther()
        {
            string path = Trials + "/zz-second.json";
            Fx.Result(_fx.Call("recipe.write", _fx.Json(path)));
            string first = RunRecord.FileSha256(_fx.Full(path));
            string variant = _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-variant\"");
            OperationResult noHash = _fx.Call("recipe.write", _fx.Json(path, text: variant));
            Assert.False(noHash.Ok);
            Assert.Contains("expectHash is required", noHash.Error);
            Assert.Contains(first, noHash.Error);
            OperationResult stale = _fx.Call("recipe.write", _fx.Json(path, text: variant, expect: new string('0', 64)));
            Assert.False(stale.Ok);
            Fx.Result(_fx.Call("recipe.write", _fx.Json(path, text: variant, expect: first)));
            Assert.Contains("zz-variant", File.ReadAllText(_fx.Full(path)));
        }

        [Fact]
        public void ATrialRecipeIsListedAndItsViewSaysTrialWhereARegisteredOneDoesNot()
        {
            string path = Trials + "/zz-listed.json";
            Fx.Result(_fx.Call("recipe.write", _fx.Json(path)));
            var resolver = new ResourceResolver(_fx.Room);
            Assert.Contains(resolver.List(), x => x.Uri == "nursery://recipe/" + path);
            Assert.True(Js.Parse(Js.Serialize(resolver.RecipeView(path))).GetProperty("trial").GetBoolean());
            Assert.False(Js.Parse(Js.Serialize(resolver.RecipeView("rounds/nursery/brain2-w5.json"))).GetProperty("trial").GetBoolean());
        }

        [Fact]
        public void TheShippedRoomJsonNamesAFolderTheRepositoryIgnores()
        {
            RoomSettings s = RoomSettings.Load(Path.Combine(Where.Root, "rounds", "nursery", "room.json"));
            Assert.Equal(Trials, s.TrialsDir);
            // scratch/ is ignored whole, so a trial recipe never dirties the tree
            string[] lines = File.ReadAllLines(Path.Combine(Where.Root, ".gitignore")).Select(l => l.Trim()).ToArray();
            Assert.Contains("scratch/", lines);
            Assert.StartsWith("scratch/", s.TrialsDir);
        }
    }
}

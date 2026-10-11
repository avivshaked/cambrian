using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>The recipe resource says what a launch of the recipe would do, as the launch decides it.</summary>
    public class LaunchStateTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private JsonElement View(string rel) => Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).RecipeView(rel)));

        private void Put(string rel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_fx.Full(rel)));
            File.WriteAllText(_fx.Full(rel), _fx.Brain2);
        }

        [Fact]
        public void ARegisteredRecipeIsRegisteredAndOneInTheTrialsFolderIsATrial()
        {
            JsonElement registered = View("rounds/nursery/brain2-w5.json");
            Assert.Equal("registered", registered.GetProperty("launch").GetProperty("state").GetString());
            Assert.False(registered.GetProperty("trial").GetBoolean());
            Assert.Equal(JsonValueKind.Null, registered.GetProperty("launch").GetProperty("reason").ValueKind);

            Put("scratch/nursery-trials/zz-trial.json");
            JsonElement trial = View("scratch/nursery-trials/zz-trial.json");
            Assert.Equal("trial", trial.GetProperty("launch").GetProperty("state").GetString());
            Assert.True(trial.GetProperty("trial").GetBoolean());
        }

        [Theory]
        [InlineData("rounds/nursery/sub/x.json", "the room launches only plain files directly under rounds/nursery/")]
        [InlineData("rounds/nursery/room.json", "the room's own settings")]
        public void ARecipeALaunchRefusesIsRefusedWithTheReasonAndNoTrial(string rel, string reason)
        {
            Put(rel);
            JsonElement v = View(rel);
            Assert.Equal("refused", v.GetProperty("launch").GetProperty("state").GetString());
            Assert.Contains(reason, v.GetProperty("launch").GetProperty("reason").GetString());
            Assert.False(v.GetProperty("trial").GetBoolean());

            // and the launch itself refuses it for the same reason, so the screen and the launch agree
            OperationResult r = _fx.Call("run.launch", Js.Serialize(new { recipe = rel, name = "no-launch", threads = 2, reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Contains(reason, r.Error);
            Assert.Empty(_fx.Launcher.Started);
        }

        [Fact]
        public void ARecipeWhoseNameALaunchRefusesIsRefusedOnTheScreenToo()
        {
            Put("scratch/nursery-trials/a b.json");
            JsonElement v = View("scratch/nursery-trials/a b.json");
            Assert.Equal("refused", v.GetProperty("launch").GetProperty("state").GetString());
            Assert.Contains("outside A-Z", v.GetProperty("launch").GetProperty("reason").GetString());
        }

        [Fact]
        public void TheRecipeScreenEnablesTheLaunchThatFitsAndNoneForARefusedRecipe()
        {
            string js = File.ReadAllText(Path.Combine(Where.WwwRoot, "js", "recipe.js"));
            Assert.Contains("d.launch.state", js);
            Assert.Contains("launchState === 'refused'", js);
            Assert.Contains("launchState === 'trial'", js);
            Assert.Contains("title=\"${esc(launchReason)}\"", js);   // the reason is the refused buttons' title
        }
    }

    /// <summary>A link on the way to what the room writes is refused, in the one check recipe.write, run.launch and the run folder's creation all use.</summary>
    public class LinkTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        private readonly ITestOutputHelper _out;
        private readonly List<string> _junctions = new List<string>();
        private readonly string _elsewhere;

        public LinkTests(ITestOutputHelper output)
        {
            _out = output;
            _elsewhere = Path.Combine(_fx.Dir, "elsewhere");
            Directory.CreateDirectory(_elsewhere);
        }

        public void Dispose()
        {
            // a junction is removed as a link and never followed: a non-recursive delete takes the link and leaves what it pointed at
            foreach (string j in _junctions)
                try { if (Directory.Exists(j)) Directory.Delete(j, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            _fx.Dispose();
        }

        /// <summary>Makes a directory junction (no privilege needed). False, with the reason in the test's output, when it cannot be made.</summary>
        private bool Junction(string link, string target)
        {
            if (!OperatingSystem.IsWindows()) { _out.WriteLine("SKIPPED: junctions are made with cmd /c mklink /J, on Windows."); return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(link));
            var psi = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            using (Process p = Process.Start(psi))
            {
                string said = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (Directory.Exists(link)) _junctions.Add(link);
                bool made = p.ExitCode == 0 && Directory.Exists(link) && (File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0;
                if (!made) _out.WriteLine("SKIPPED: a junction could not be made here (" + said.Trim() + ").");
                return made;
            }
        }

        private string[] InElsewhere() => Directory.GetFileSystemEntries(_elsewhere, "*", SearchOption.AllDirectories);

        [Fact]
        public void APathWithNoLinkOnItIsFineWhetherOrNotItExistsYet()
        {
            Assert.Null(_fx.Room.LinkProblem(_fx.Full("rounds/nursery/brain2-w5.json")));
            Assert.Null(_fx.Room.LinkProblem(_fx.Full("scratch/not/made/yet.json")));
            Assert.Null(_fx.Room.LinkProblem(_fx.Root));
            Assert.Contains("outside the repository", _fx.Room.LinkProblem(Path.Combine(_fx.Dir, "elsewhere", "x")));
        }

        [Fact]
        public void ARecipeIsNotWrittenThroughALinkedTrialsFolder()
        {
            if (!Junction(_fx.Full("scratch/nursery-trials"), _elsewhere)) return;
            Assert.Contains("scratch/nursery-trials is a link", _fx.Room.LinkProblem(_fx.Full("scratch/nursery-trials/zz.json")));

            OperationResult r = _fx.Call("recipe.write", _fx.Json("scratch/nursery-trials/zz-linked.json"));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("scratch/nursery-trials is a link", r.Error);
            Assert.Contains("reparse point", r.Error);
            Assert.Contains("Nothing was written", r.Error);
            Assert.Empty(InElsewhere());
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
        }

        [Fact]
        public void ARecipeThatAlreadyLiesBehindALinkIsNotReplaced()
        {
            string there = Path.Combine(_elsewhere, "zz-file.json");
            File.WriteAllText(there, _fx.Brain2);
            if (!Junction(_fx.Full("scratch/nursery-trials"), _elsewhere)) return;
            string before = File.ReadAllText(there);
            OperationResult r = _fx.Call("recipe.write", _fx.Json("scratch/nursery-trials/zz-file.json", text: _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-over\""), expect: RunRecord.FileSha256(there)));
            Assert.False(r.Ok);
            Assert.Contains("is a link", r.Error);
            Assert.Equal(before, File.ReadAllText(there));
        }

        [Fact]
        public void ALaunchIsRefusedWhenTheRunsFolderIsALinkAndNothingIsMadeThroughIt()
        {
            if (!Junction(_fx.RunsDir, _elsewhere)) return;
            OperationResult r = _fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "through-link", threads = 2, reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("runs is a link", r.Error);
            Assert.Contains("No run was started", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.Empty(InElsewhere());     // neither the claim (the launch log) nor the folder was made through the link
        }

        [Fact]
        public void ALaunchIsRefusedWhenTheRunFolderItselfIsALinkBeforeItIsToldItExists()
        {
            Directory.CreateDirectory(_fx.RunsDir);
            if (!Junction(Path.Combine(_fx.RunsDir, "linked-run"), _elsewhere)) return;
            OperationResult r = _fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "linked-run", threads = 2, reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Contains("is a link", r.Error);
            Assert.DoesNotContain(" exists;", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.Empty(InElsewhere());
            Assert.False(File.Exists(Path.Combine(_fx.RunsDir, "linked-run.launch.log")));
        }

        [Fact]
        public void ALaunchIsRefusedWhenItsRecipeLiesBehindALink()
        {
            string real = Path.Combine(_elsewhere, "zz-behind.json");
            File.WriteAllText(real, _fx.Brain2);
            if (!Junction(_fx.Full("scratch/nursery-trials"), _elsewhere)) return;
            OperationResult r = _fx.Call("run.launch", Js.Serialize(new { recipe = "scratch/nursery-trials/zz-behind.json", name = "behind", threads = 2, reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Contains("is a link", r.Error);
            Assert.Contains("No run was started", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "behind")));
            // and the screen shows it refused, as the launch does
            JsonElement v = Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).RecipeView("scratch/nursery-trials/zz-behind.json")));
            Assert.Equal("refused", v.GetProperty("launch").GetProperty("state").GetString());
        }

        [Fact]
        public void TheOneFolderMakerRefusesALinkAndMakesNothing()
        {
            if (!Junction(_fx.Full("scratch/linked"), _elsewhere)) return;
            var e = Assert.Throws<ArgumentException>(() => _fx.Room.EnsureDirectory(_fx.Full("scratch/linked/deeper/still")));
            Assert.Contains("scratch/linked is a link", e.Message);
            Assert.Empty(InElsewhere());
            _fx.Room.EnsureDirectory(_fx.Full("scratch/plain/deeper"));
            Assert.True(Directory.Exists(_fx.Full("scratch/plain/deeper")));
        }
    }

    /// <summary>Launch checks the mask and the threads before anything that reads git.</summary>
    public class LaunchOrderTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private int _builds;

        private RoomContext RoomCountingBuildReads() =>
            new RoomContext(_fx.Root, _fx.Room.Settings, _fx.Main, _fx.Registry, _fx.Room.Activity.Path, _fx.Launcher, _fx.Queue, _fx.Heat, systemMask: _fx.Room.SystemMask, readBuild: _ => { _builds++; return _fx.Build; });

        private void Enter()
        {
            RegistryEntry.Append(_fx.Registry, new RegistryEntry
            {
                Recipe = "brain2", RecipeHash = Recipe.Load(_fx.Full("rounds/nursery/brain2-w5.json")).Hash(), RecipePath = "rounds/nursery/brain2-w5.json",
                Out = Path.Combine(_fx.Dir, "entry-out"), Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z",
                GitCommit = "abc1234", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Rerunnable = true,
            });
        }

        [Fact]
        public void ARerunWithTooManyThreadsForTheMaskIsRefusedBeforeTheBuildIsRead()
        {
            Enter();
            var ops = new OperationList(RoomCountingBuildReads());
            // room.json's mask 0xFFFF0000 is sixteen processors
            OperationResult r = ops.Invoke("run.rerun", Js.Parse(Js.Serialize(new { entry = "1", name = "too-many", threads = 17, reason = "a test" })), OperationList.Screen);
            Assert.False(r.Ok);
            Assert.Contains("17", r.Error);
            Assert.Equal(0, _builds);
            Assert.Empty(_fx.Launcher.Started);

            // the control: with the threads the mask allows, the same rerun reads the build (once) and starts
            OperationResult ok = ops.Invoke("run.rerun", Js.Parse(Js.Serialize(new { entry = "1", name = "enough", threads = 2, reason = "a test" })), OperationList.Screen);
            Assert.True(ok.Ok, ok.Error);
            Assert.Equal(1, _builds);
        }

        [Fact]
        public void ARerunWhoseMaskNamesAProcessorTheMachineLacksIsRefusedBeforeTheBuildIsRead()
        {
            Enter();
            // a machine of 16 processors, with room.json's mask on processors 16 to 31
            var lacking = new RoomContext(_fx.Root, _fx.Room.Settings, _fx.Main, _fx.Registry, _fx.Room.Activity.Path, _fx.Launcher, _fx.Queue, _fx.Heat, systemMask: 0xFFFFUL, readBuild: _ => { _builds++; return _fx.Build; });
            OperationResult r = new OperationList(lacking).Invoke("run.rerun", Js.Parse(Js.Serialize(new { entry = "1", name = "no-core", threads = 2, reason = "a test" })), OperationList.Screen);
            Assert.False(r.Ok);
            Assert.Contains("lacks", r.Error);
            Assert.Equal(0, _builds);
            Assert.Empty(_fx.Launcher.Started);
        }
    }

    /// <summary>recipe.write makes the trials folder only after the nursery has accepted the text.</summary>
    public class TrialsFolderOrderTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        [Fact]
        public void ARefusedTextLeavesNoTrialsFolderAndNoCheckCopyBehind()
        {
            string trials = _fx.Full("scratch/nursery-trials");
            OperationResult r = _fx.Call("recipe.write", _fx.Json("scratch/nursery-trials/zz-bad.json", text: "{ this is not a recipe"));
            Assert.False(r.Ok);
            Assert.Contains("the nursery refuses this recipe, so nothing was written", r.Error);
            Assert.Contains("scratch/nursery-trials/zz-bad.json", r.Error.Replace('\\', '/'));   // the message names the file, not the check copy
            Assert.DoesNotContain(".writing", r.Error);
            Assert.False(Directory.Exists(trials));
            Assert.False(Directory.Exists(_fx.Full("scratch")));
            Assert.Empty(Directory.GetFiles(_fx.Root, "*.writing", SearchOption.AllDirectories));
        }

        [Fact]
        public void AnAcceptedTextMakesTheFolderAndLeavesNoCheckCopy()
        {
            string path = "scratch/nursery-trials/zz-good.json";
            Fx.Result(_fx.Call("recipe.write", _fx.Json(path)));
            Assert.True(File.Exists(_fx.Full(path)));
            Assert.Empty(Directory.GetFiles(_fx.Root, "*.writing", SearchOption.AllDirectories));
        }

        [Fact]
        public void ARecipeThatNamesItselfAsItsBaseIsRefusedAfterTheCheckAndLeavesNoFileOrFolder()
        {
            string path = "scratch/nursery-trials/zz-loop.json";
            OperationResult r = _fx.Call("recipe.write", _fx.Json(path, text: "{\"base\":\"" + path + "\",\"name\":\"zz-loop\"}"));
            Assert.False(r.Ok);
            Assert.False(File.Exists(_fx.Full(path)));
            Assert.Empty(Directory.GetFiles(_fx.Root, "*.writing", SearchOption.AllDirectories));
        }
    }

    public class TrialsFolderSettingsTests
    {
        [Theory]
        [InlineData("scratch", "scratch/nursery", "not under scratch/")]
        [InlineData("elsewhere/trials", "scratch/nursery", "not under scratch/")]
        [InlineData("scratchy/trials", "scratch/nursery", "not under scratch/")]
        [InlineData("scratch/wt-other/trials", "scratch/nursery", "scratch/wt-")]
        [InlineData("scratch/WT-Other", "scratch/nursery", "scratch/wt-")]
        [InlineData("scratch/nursery", "scratch/nursery", "is the runs folder")]
        [InlineData("Scratch/Nursery", "scratch/nursery", "is the runs folder")]
        [InlineData("scratch/nursery/trials", "scratch/nursery", "inside the runs folder")]
        [InlineData("scratch/nursery", "scratch/nursery/runs", "holds the runs folder")]
        public void ATrialsFolderThatIsNotUnderScratchOrIsAnotherTreesOrTheRunsFoldersIsRefused(string trials, string runs, string said)
        {
            var e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(Where.SettingsJson(trialsDir: trials, runsDir: runs), "the/settings.json"));
            Assert.Contains("trialsDir", e.Message);
            Assert.Contains("the/settings.json", e.Message);
            Assert.Contains(said, e.Message);
        }

        [Theory]
        [InlineData("scratch/nursery-trials", "scratch/nursery")]      // a name that only begins like the runs folder's is another folder
        [InlineData("scratch/nursery", "scratch/nursery-runs")]
        [InlineData("scratch/trials/a", "scratch/nursery")]
        [InlineData("scratch/wtx/trials", "scratch/nursery")]
        public void AFolderUnderScratchThatIsNeitherAnotherTreesNorTheRunsFoldersIsTaken(string trials, string runs)
        {
            Assert.Equal(trials, RoomSettings.Parse(Where.SettingsJson(trialsDir: trials, runsDir: runs)).TrialsDir);
        }
    }

    public class TrialFlagOfAListedRunTests : IDisposable
    {
        private readonly Fx _fx = new Fx("runs/trial-one", "runs/reg-one");
        public void Dispose() => _fx.Dispose();

        [Fact]
        public void ATrialWhoseFolderRoomJsonAlsoListsKeepsItsTrialFlagFromTheActivityLog()
        {
            string trial = "scratch/nursery-trials/zz-t.json";
            Fx.Result(_fx.Call("recipe.write", _fx.Json(trial)));
            Fx.Result(_fx.Call("run.launch", Js.Serialize(new { recipe = trial, name = "trial-one", threads = 2, reason = "a test" })));
            Fx.Result(_fx.Call("run.launch", Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name = "reg-one", threads = 2, reason = "a test" })));

            var resolver = new ResourceResolver(_fx.Room);
            List<ResourceResolver.RunRef> runs = resolver.Catalog(out _);
            ResourceResolver.RunRef t = runs.Single(r => r.Id == "trial-one"), reg = runs.Single(r => r.Id == "reg-one");
            Assert.Equal(2, runs.Count);              // listed once each, not twice
            Assert.True(t.Trial);
            Assert.False(reg.Trial);

            JsonElement items = Js.Parse(Js.Serialize(resolver.Registry())).GetProperty("items");
            Assert.True(items.EnumerateArray().Single(i => i.GetProperty("id").GetString() == "trial-one").GetProperty("trial").GetBoolean());
            Assert.False(items.EnumerateArray().Single(i => i.GetProperty("id").GetString() == "reg-one").GetProperty("trial").GetBoolean());
            Assert.True(Js.Parse(Js.Serialize(resolver.Run("trial-one"))).GetProperty("trial").GetBoolean());

            // a room started afterwards reads it from the log
            var later = new RoomContext(_fx.Root, _fx.Room.Settings, _fx.Main, _fx.Registry, _fx.Room.Activity.Path, new FakeLauncher(), _fx.Queue, _fx.Heat, 32, _ => _fx.Build);
            Assert.True(new ResourceResolver(later).Catalog(out _).Single(r => r.Id == "trial-one").Trial);
        }
    }

    public class SettingsRefusalNamesTheFileTests
    {
        [Theory]
        [InlineData("\"port\":5179", "\"port\":0", "port")]
        [InlineData("\"pollSeconds\":30", "\"pollSeconds\":0", "pollSeconds")]
        [InlineData("\"queueFile\":\"scratch/machine/queue.md\"", "\"queueFile\":\"../queue.md\"", "queueFile")]
        [InlineData("\"heatScript\":\"scripts/no-heat-check-in-the-room-tests.ps1\"", "\"heatScript\":\"C:/x.ps1\"", "heatScript")]
        [InlineData("\"bind\":\"127.0.0.1\"", "\"bind\":\"10.0.0.1\"", "bind")]
        [InlineData("\"dotnet\":\"dotnet\"", "\"dotnet\":\" \"", "dotnet")]
        [InlineData("\"runs\":[]", "\"runs\":{}", "runs")]
        [InlineData("\"runs\":[]", "\"runs\":[\" \"]", "runs")]
        [InlineData("\"runs\":[]", "\"runs\":[\"D:/x\"]", "runs")]
        [InlineData("\"runsDir\":\"scratch/nursery\"", "\"runsDir\":\"../x\"", "runsDir")]
        [InlineData("\"activityFile\":\"rounds/nursery/activity.jsonl\"", "\"activityFile\":\"/x\"", "activityFile")]
        public void EveryRefusalOfAKeyNamesTheSettingsFile(string good, string bad, string key)
        {
            string text = Where.SettingsJson();
            Assert.Contains(good, text);
            var e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(text.Replace(good, bad), "my/room-settings.json"));
            Assert.Contains(key, e.Message);
            Assert.Contains("my/room-settings.json", e.Message);
        }

        [Fact]
        public void TheNurseryDllRefusalNamesTheSettingsFileToo()
        {
            string text = Where.SettingsJson().Replace("\"nurseryDll\":\"artifacts/Evosim.Nursery/bin/Debug/net8.0/Evosim.Nursery.dll\"", "\"nurseryDll\":\"\"");
            var e = Assert.Throws<InvalidDataException>(() => RoomSettings.Parse(text, "my/room-settings.json"));
            Assert.Contains("nurseryDll", e.Message);
            Assert.Contains("my/room-settings.json", e.Message);
        }
    }
}

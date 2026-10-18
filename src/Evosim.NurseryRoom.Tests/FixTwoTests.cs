using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>Launched runs are keyed by folder, not by name: two rooms in two trees launching one name are two runs.</summary>
    public class SameNameTwoTreesTests : IDisposable
    {
        private readonly Fx _fx = Fx.InWorktree();
        private readonly RoomContext _other;
        private readonly OperationList _otherOps;
        private readonly FakeLauncher _otherLauncher = new FakeLauncher();

        public SameNameTwoTreesTests()
        {
            // a second worktree of the same main checkout: its own root, bin and recipes; the registry and the activity log are the main checkout's
            string wt2 = Path.Combine(_fx.Main, "scratch", "wt2");
            CopyDir(_fx.Root, wt2);
            _other = new RoomContext(wt2, _fx.Room.Settings, _fx.Main, _fx.Registry, _fx.Room.Activity.Path, _otherLauncher, _fx.Load, 32, _ => _fx.Build);
            _otherOps = new OperationList(_other);
        }

        public void Dispose() => _fx.Dispose();

        private static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
            foreach (string d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
        }

        private static string Launch(string name, int threads) =>
            Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name, threads, reason = "a test" });

        [Fact]
        public void TwoRoomsLaunchingOneNameAreTwoRunsAndBothCountAgainstTheCapAndInTheCatalogue()
        {
            Fx.Result(_fx.Call("run.launch", Launch("same-name", 3)));
            OperationResult second = _otherOps.Invoke("run.launch", Js.Parse(Launch("same-name", 4)), OperationList.Screen);
            Assert.True(second.Ok, second.Error);
            Assert.Single(_fx.Launcher.Started);
            Assert.Single(_otherLauncher.Started);

            DateTime now = DateTime.UtcNow;
            Assert.Equal(7, _fx.Room.GrantedThreads(now));      // 3 and 4: neither is dropped for sharing a name
            Assert.Equal(7, _other.GrantedThreads(now));
            Assert.Equal(2, _fx.Room.LaunchedRuns().Count);

            List<ResourceResolver.RunRef> catalogue = new ResourceResolver(_fx.Room).Catalog(out _).Where(c => c.Launched).ToList();
            Assert.Equal(2, catalogue.Count);
            Assert.All(catalogue, c => Assert.Equal("same-name", c.Name));
            Assert.Equal(2, catalogue.Select(c => c.Folder).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(2, catalogue.Select(c => c.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            // each is found by its own id
            foreach (var c in catalogue) Assert.Equal(c.Folder, new ResourceResolver(_fx.Room).FindRun(c.Id).Folder);

            string a = Path.Combine(_fx.Root, "runs", "same-name"), b = Path.Combine(_other.Root, "runs", "same-name");
            foreach (RoomContext room in new[] { _fx.Room, _other })
            {
                Assert.True(room.IsLaunchedFolder(a));
                Assert.True(room.IsLaunchedFolder(b));
            }
        }

        [Fact]
        public void TheRunsANewlyStartedRoomHasNotYetLoggedAreKeyedByFolderToo()
        {
            var fresh = new RoomContext(_fx.Root, _fx.Room.Settings, _fx.Main, _fx.Registry, Path.Combine(_fx.Dir, "no-log.jsonl"), new FakeLauncher(), _fx.Load, 32, _ => _fx.Build);
            DateTime now = DateTime.UtcNow;
            fresh.NoteLaunched("same", "scratch/wt/runs/same", 3, now);
            fresh.NoteLaunched("same", "scratch/wt2/runs/same", 4, now);
            fresh.NoteLaunched("same", "scratch/wt2/runs/same", 4, now);     // the same folder again is the same run
            Assert.Equal(2, fresh.LaunchedRuns().Count);
            Assert.Equal(7, fresh.GrantedThreads(now));
        }
    }

    /// <summary>recipe.write never replaces a recipe that a running run uses.</summary>
    public class ActiveRunGuardTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public void Dispose() => _fx.Dispose();

        private string Variant(string name) => _fx.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"" + name + "\"");

        private string NewRecipe(string text = null)
        {
            string path = _fx.RecipePath();
            Fx.Result(_fx.Call("recipe.write", _fx.Json(path, text: text)));
            return path;
        }

        private OperationResult Replace(string path) =>
            _fx.Call("recipe.write", _fx.Json(path, text: Variant("zz-replaced"), expect: RunRecord.FileSha256(_fx.Full(path))));

        /// <summary>
        /// A run folder under the runs folder whose run.json names a recipe, as the nursery writes it: the path absolute
        /// (<c>Path.GetFullPath</c> of what the nursery was given), where these tests once wrote it relative.
        /// </summary>
        private string RunJson(string folderName, string recipePath, string status, bool alive = true) =>
            RunJsonNaming(folderName, Js.Serialize(_fx.Full(recipePath)), status, alive);

        /// <summary>A run.json whose recipe path is this JSON text, or which has no recipe member when it is null.</summary>
        private string RunJsonNaming(string folderName, string recipePathJson, string status, bool alive = true)
        {
            string folder = Path.Combine(_fx.RunsDir, folderName);
            Directory.CreateDirectory(folder);
            using (Process self = Process.GetCurrentProcess())
            {
                string started = alive ? self.StartTime.ToUniversalTime().ToString("o") : DateTime.UtcNow.AddDays(-9).ToString("o");
                File.WriteAllText(Path.Combine(folder, "run.json"),
                    "{\"status\":\"" + status + "\",\"processId\":" + (alive ? self.Id : 999999) + ",\"processStartedAtUtc\":\"" + started + "\"" +
                    (recipePathJson == null ? "" : ",\"recipe\":{\"path\":" + recipePathJson + ",\"name\":\"n\"}") + "}");
            }
            return folder;
        }

        [Fact]
        public void ARecipeARunTheRoomLaunchedUsesIsNotReplacedUntilTheRunEnds()
        {
            string path = NewRecipe();
            string before = File.ReadAllText(_fx.Full(path));
            Fx.Result(_fx.Call("run.launch", Js.Serialize(new { recipe = path, name = "uses-x", threads = 2, reason = "a test" })));

            // just launched, with no run.json yet: it has not ended
            OperationResult r = Replace(path);
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("uses-x", r.Error);
            Assert.Contains("has not ended", r.Error);
            Assert.Contains("cannot be replaced", r.Error);
            Assert.Equal(before, File.ReadAllText(_fx.Full(path)));
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
            Assert.Empty(_fx.WritingFiles());

            // the launch row carries the recipe, so a room started later knows it too
            Assert.Equal(path, _fx.Rows().First(row => row.GetProperty("operation").GetString() == "run.launch").GetProperty("result").GetProperty("recipe").GetString());
            var later = new RoomContext(_fx.Root, _fx.Room.Settings, _fx.Main, _fx.Registry, _fx.Room.Activity.Path, new FakeLauncher(), _fx.Load, 32, _ => _fx.Build);
            Assert.Equal(path, later.LaunchedRuns().Single().Recipe);
            Assert.False(new OperationList(later).Invoke("recipe.write", Js.Parse(_fx.Json(path, text: Variant("zz-later"), expect: RunRecord.FileSha256(_fx.Full(path)))), "screen").Ok);

            // running, by its record
            RunJson("uses-x", path, "running");
            Assert.Contains("uses-x", Replace(path).Error);

            // ended: the file is the owner's again
            File.WriteAllText(Path.Combine(_fx.RunsDir, "uses-x", "run.json"), "{\"status\":\"completed\",\"recipe\":{\"path\":" + Js.Serialize(_fx.Full(path)) + "}}");
            JsonElement ok = Fx.Result(Replace(path));
            Assert.False(ok.GetProperty("created").GetBoolean());
        }

        [Fact]
        public void ARunningRunJsonNamingTheRecipeOrOneWithItInItsBaseChainBlocksAReplace()
        {
            string a = NewRecipe();
            string child = NewRecipe("{\"base\":\"" + a + "\",\"name\":\"zz-child\"}");
            string unrelated = NewRecipe();
            RunJson("elsewhere", child, "running");

            OperationResult r = Replace(a);
            Assert.False(r.Ok);
            Assert.Contains("elsewhere", r.Error);
            Assert.Contains("base chain", r.Error);
            Assert.Contains(child, r.Error);
            Assert.Contains("elsewhere", _fx.LastRow().GetProperty("detail").GetString());

            RunJson("direct", unrelated, "running");
            Assert.Contains("as its recipe", Replace(unrelated).Error);

            // a different recipe in the same folder does not block, and neither does a run that is not running, or whose process is gone
            string free = NewRecipe();
            Fx.Result(Replace(free));
            RunJson("elsewhere", child, "completed");
            RunJson("direct", unrelated, "running", alive: false);
            Fx.Result(Replace(a));
            Fx.Result(Replace(unrelated));
        }

        [Fact]
        public void ARunJsonThatCannotBeReadRefusesAReplaceNamingTheRunButNotANewFile()
        {
            string path = NewRecipe();
            string folder = Path.Combine(_fx.RunsDir, "broken");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "run.json"), "{\"status\":\"run");
            OperationResult r = Replace(path);
            Assert.False(r.Ok);
            Assert.Contains("broken", r.Error);
            Assert.Contains("cannot be read", r.Error);
            Fx.Result(_fx.Call("recipe.write", _fx.Json(_fx.RecipePath())));
        }

        [Theory]
        [InlineData("rounds/nursery", false)]
        [InlineData("scratch/nursery-trials", false)]
        [InlineData("rounds/nursery", true)]
        [InlineData("scratch/nursery-trials", true)]
        public void ARunningRunWhoseRunJsonHoldsAnAbsoluteRecipePathBlocksAWriteOfThatRecipeAndOfOneInItsBaseChain(string folder, bool upperCased)
        {
            string name = Guid.NewGuid().ToString("N").Substring(0, 8);
            string baseRel = folder + "/zz-base-" + name + ".json", childRel = folder + "/zz-child-" + name + ".json", freeRel = folder + "/zz-free-" + name + ".json";
            Fx.Result(_fx.Call("recipe.write", _fx.Json(baseRel)));
            Fx.Result(_fx.Call("recipe.write", _fx.Json(childRel, text: "{\"base\":\"" + baseRel + "\",\"name\":\"zz-child\"}")));
            Fx.Result(_fx.Call("recipe.write", _fx.Json(freeRel)));

            // the nursery's own spelling of the path: Path.GetFullPath of what it was given (the upper-cased form is the same file on Windows)
            string absolute = _fx.Full(childRel);
            Assert.True(Path.IsPathRooted(absolute));
            RunJsonNaming("absolute-run", Js.Serialize(upperCased ? absolute.ToUpperInvariant() : absolute), "running");

            OperationResult viaBase = Replace(baseRel);
            Assert.False(viaBase.Ok);
            Assert.Contains("absolute-run", viaBase.Error);
            Assert.Contains("base chain", viaBase.Error);
            Assert.Contains(baseRel, viaBase.Error);
            OperationResult direct = Replace(childRel);
            Assert.False(direct.Ok);
            Assert.Contains("as its recipe", direct.Error);
            Assert.Contains("absolute-run", direct.Error);

            // a recipe the run does not use is still replaced, and the run's own ending frees the others
            Fx.Result(Replace(freeRel));
            RunJsonNaming("absolute-run", Js.Serialize(absolute), "completed");
            Fx.Result(Replace(baseRel));
            Fx.Result(Replace(childRel));
        }

        [Fact]
        public void AnActiveRunWhoseRecipeCannotBePlacedInThisTreeRefusesAWriteNamingTheRunAndWhy()
        {
            string path = NewRecipe();
            string before = File.ReadAllText(_fx.Full(path));

            // an absolute path outside every tree this room knows
            RunJsonNaming("far-away", Js.Serialize(Path.Combine(Path.GetPathRoot(_fx.Dir), "nowhere-" + Guid.NewGuid().ToString("N"), "x.json")), "running");
            OperationResult far = Replace(path);
            Assert.False(far.Ok);
            Assert.Contains("far-away", far.Error);
            Assert.Contains("cannot be placed inside this tree", far.Error);
            Assert.Contains(path, far.Error);
            Assert.DoesNotContain(Path.GetPathRoot(_fx.Dir) + "nowhere", far.Error);   // no path outside the repository is shown
            Assert.Equal(before, File.ReadAllText(_fx.Full(path)));
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
            Assert.Empty(_fx.WritingFiles());

            // a relative path that climbs out, and a run.json that names no recipe at all, cannot be placed either
            RunJsonNaming("far-away", Js.Serialize("../../outside.json"), "running");
            Assert.Contains("cannot be placed", Replace(path).Error);
            RunJsonNaming("far-away", null, "running");
            OperationResult none = Replace(path);
            Assert.False(none.Ok);
            Assert.Contains("no recipe is recorded", none.Error);
            Assert.Contains("far-away", none.Error);

            // ended, the run is no concern; a new file was never blocked
            RunJsonNaming("far-away", null, "completed");
            Fx.Result(Replace(path));
        }

        [Fact]
        public void AnActiveRunOfAnotherTreeUsesAnotherTreesFileAndDoesNotBlockThisTreesWrites()
        {
            using (var wt = Fx.InWorktree())
            {
                string path = wt.RecipePath();
                Fx.Result(wt.Call("recipe.write", wt.Json(path)));
                // a run in the main checkout, which has the same relative file: not this tree's file
                string mainFile = Path.Combine(wt.Main, path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(mainFile));
                File.WriteAllText(mainFile, wt.Brain2);
                string folder = Path.Combine(wt.RunsDir, "other-tree");
                Directory.CreateDirectory(folder);
                using (Process self = Process.GetCurrentProcess())
                    File.WriteAllText(Path.Combine(folder, "run.json"),
                        "{\"status\":\"running\",\"processId\":" + self.Id + ",\"processStartedAtUtc\":\"" + self.StartTime.ToUniversalTime().ToString("o") + "\",\"recipe\":{\"path\":" + Js.Serialize(mainFile) + ",\"name\":\"n\"}}");
                string current = RunRecord.FileSha256(wt.Full(path));
                Fx.Result(wt.Call("recipe.write", wt.Json(path, text: wt.Brain2.Replace("\"name\": \"brain2-w5\"", "\"name\": \"zz-other\""), expect: current)));
            }
        }

        [Fact]
        public void TheTemporaryFileOfAWriteHasAUniqueNameSoAnotherWritersIsNeverTouched()
        {
            string path = NewRecipe();
            string full = _fx.Full(path);
            string theirs = full + ".writing";                     // the name an earlier build used, standing for another writer's file
            File.WriteAllText(theirs, "another writer's text");
            Fx.Result(Replace(path));
            Assert.Equal("another writer's text", File.ReadAllText(theirs));
            Assert.Equal(new[] { theirs }, _fx.WritingFiles());
        }
    }

    public class SettingsAndIgnoreTests
    {
        [Fact]
        public void RoomJsonNamesTheReleaseBuildOfTheNursery()
        {
            RoomSettings s = RoomSettings.Load(Path.Combine(Where.Root, "rounds", "nursery", "room.json"));
            Assert.Equal("artifacts/Evosim.Nursery/bin/Release/net8.0/Evosim.Nursery.dll", s.NurseryDll);
        }

        [Fact]
        public void TheActivityLogIsExemptFromTheJsonlIgnoreBesideTheRegistry()
        {
            string[] lines = File.ReadAllLines(Path.Combine(Where.Root, ".gitignore")).Select(l => l.Trim()).ToArray();
            int ignore = Array.IndexOf(lines, "*.jsonl");
            Assert.True(ignore >= 0);
            Assert.Contains("!rounds/nursery/registry.jsonl", lines);
            // a later line wins in .gitignore, so the exceptions come after the blanket ignore
            Assert.True(Array.IndexOf(lines, "!rounds/nursery/activity.jsonl") > ignore);
            Assert.Equal(Array.IndexOf(lines, "!rounds/nursery/registry.jsonl") + 1, Array.IndexOf(lines, "!rounds/nursery/activity.jsonl"));
        }
    }

    public class SmallFixTests : IDisposable
    {
        private readonly Fx _fx = new Fx("scratch/live");
        public void Dispose() => _fx.Dispose();

        private static string Launch(string name, string recipe = "rounds/nursery/brain2-w5.json", int threads = 2) =>
            Js.Serialize(new { recipe, name, threads, reason = "a test" });

        [Theory]
        [InlineData("a\\")]
        [InlineData("runs\\a-b\\")]
        [InlineData("C:\\x\\")]
        public void AnArgumentEndingInABackslashIsRefused(string bad)
        {
            var spec = new LaunchSpec
            {
                FileName = "C:/Program Files/dotnet/dotnet.exe", Arguments = new[] { "D:/x/n.dll", "--out", bad },
                WorkingDirectory = "D:/x", Folder = "D:/x/runs/a-b", LogFile = "D:/x/runs/a-b.launch.log",
            };
            Assert.Contains("argument 3", CommandLine.Problem(spec));
            Assert.DoesNotMatch(CommandLine.Argument, bad);
            Assert.DoesNotMatch(CommandLine.PathText, bad);
            Assert.Matches(CommandLine.PathText, bad.TrimEnd('\\'));
            spec.Arguments = new[] { bad, "--threads", "2" };
            Assert.Contains("argument 1", CommandLine.Problem(spec));
            spec.Arguments = new[] { "D:/x/n.dll" };
            spec.FileName = bad;
            Assert.Contains("the program", CommandLine.Problem(spec));
            spec.FileName = "dotnet";
            spec.LogFile = bad;
            Assert.Contains("the log file", CommandLine.Problem(spec));
        }

        [Theory]
        [InlineData("rounds/nursery/sub/x.json")]
        [InlineData("rounds/nursery/room.json")]
        public void ASubfolderOrTheSettingsAreNotLaunchedAndTheSubfolderIsToldWhatIs(string rel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_fx.Full(rel)));
            if (!File.Exists(_fx.Full(rel))) File.WriteAllText(_fx.Full(rel), _fx.Brain2);
            OperationResult r = _fx.Call("run.launch", Launch("no-sub", rel));
            Assert.False(r.Ok);
            if (rel.Contains("/sub/")) Assert.Contains("the room launches only plain files directly under rounds/nursery/", r.Error);
            else Assert.Contains("the room's own settings", r.Error);
            Assert.DoesNotContain("unregistered trial", r.Error);
            Assert.Empty(_fx.Launcher.Started);
        }

        [Theory]
        [InlineData("rounds/nursery/brain2-w5.json.")]
        [InlineData("rounds/nursery/brain2-w5.json..")]
        public void ARawRecipePathEndingInADotIsRefusedByLaunch(string raw)
        {
            OperationResult r = _fx.Call("run.launch", Launch("dotted", raw));
            Assert.False(r.Ok);
            Assert.Contains("ends in a dot", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "dotted")));
            Assert.False(_fx.Call("recipe.write", _fx.Json(raw)).Ok);
        }

        [Theory]
        [InlineData("rounds/nursery/sub/x.json", "the room launches only plain files directly under rounds/nursery/")]
        [InlineData("rounds/other/x.json", "unregistered trial")]
        [InlineData("rounds/nursery/brain2-w5.json.", "ends in a dot")]
        public void ARerunAppliesTheRegisteredRecipeCheckToTheEntrysRecipe(string recipePath, string said)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_fx.Full(recipePath)));
            if (!File.Exists(_fx.Full(recipePath))) File.WriteAllText(_fx.Full(recipePath), _fx.Brain2);
            RegistryEntry.Append(_fx.Registry, new RegistryEntry
            {
                Recipe = "x", RecipeHash = Recipe.Load(_fx.Full("rounds/nursery/brain2-w5.json")).Hash(), RecipePath = recipePath,
                Out = Path.Combine(_fx.Dir, "entry-out"), Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z",
                GitCommit = "abc1234", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Rerunnable = true,
            });
            OperationResult r = _fx.Call("run.rerun", Js.Serialize(new { entry = "1", name = "again", threads = 2, reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Contains(said, r.Error);
            Assert.Contains("entry 1's recipe", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "again")));
        }

        [Fact]
        public void TheRerunOfARegisteredRecipeCarriesItsRecipeInTheResult()
        {
            RegistryEntry.Append(_fx.Registry, new RegistryEntry
            {
                Recipe = "brain2", RecipeHash = Recipe.Load(_fx.Full("rounds/nursery/brain2-w5.json")).Hash(), RecipePath = "rounds/nursery/brain2-w5.json",
                Out = Path.Combine(_fx.Dir, "entry-out"), Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z",
                GitCommit = "abc1234", CoreHash = "c", DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Rerunnable = true,
            });
            JsonElement r = Fx.Result(_fx.Call("run.rerun", Js.Serialize(new { entry = "1", name = "again", threads = 2, reason = "a test" })));
            Assert.Equal("rounds/nursery/brain2-w5.json", r.GetProperty("recipe").GetString());
            Assert.Equal("rounds/nursery/brain2-w5.json", _fx.LastRow().GetProperty("result").GetProperty("recipe").GetString());
        }

        [Fact]
        public void TheStopNoteSaysTheRunIsNotEnteredUnlessItIsOnItsLastExamBrain()
        {
            string folder = Path.Combine(_fx.Root, "scratch", "live");
            Directory.CreateDirectory(folder);
            using (Process self = Process.GetCurrentProcess())
                File.WriteAllText(Path.Combine(folder, "run.json"),
                    "{\"status\":\"running\",\"processId\":" + self.Id + ",\"processStartedAtUtc\":\"" + self.StartTime.ToUniversalTime().ToString("o") + "\",\"stopFile\":\"STOP\"}");
            JsonElement r = Fx.Result(_fx.Call("run.stop", Js.Serialize(new { id = "live", reason = "a test" })));
            Assert.Contains("is not entered in the registry unless it is already swimming its last exam brain", r.GetProperty("note").GetString());
        }

        [Fact]
        public void AStopOnARunWhoseRunJsonDoesNotParseIsRefusedAndNothingIsWritten()
        {
            string folder = Path.Combine(_fx.Root, "scratch", "live");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "run.json"), "{\"status\":\"runn");
            OperationResult r = _fx.Call("run.stop", Js.Serialize(new { id = "live", reason = "a test" }));
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("cannot be read", r.Error);
            Assert.Contains("Nothing was written", r.Error);
            Assert.False(File.Exists(Path.Combine(folder, "STOP")));
            Assert.Equal("refused", _fx.LastRow().GetProperty("outcome").GetString());
        }

        [Theory]
        [InlineData(3200.5)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        public void TheRoomStatusShowsAReadingTheCapWouldRefuseAsUnreadable(double percent)
        {
            JsonElement load = Js.Parse(Js.Serialize(Program.RoomStatus(_fx.Room, LoadReading.Of(percent), DateTime.UtcNow))).GetProperty("load");
            Assert.Equal(JsonValueKind.Null, load.GetProperty("percent").ValueKind);
            Assert.Equal(JsonValueKind.Null, load.GetProperty("inUse").ValueKind);
            Assert.Equal(JsonValueKind.Null, load.GetProperty("read").ValueKind);
            Assert.Contains("not a possible reading", load.GetProperty("problem").GetString());
            Assert.Contains("not a possible reading", Cap.Refusal(1, 16, 32, LoadReading.Of(percent)));   // the launch is refused for the same reading
        }

        [Fact]
        public void TheRoomStatusShowsAUsableReadingAndAFailedOne()
        {
            JsonElement ok = Js.Parse(Js.Serialize(Program.RoomStatus(_fx.Room, LoadReading.Of(50), DateTime.UtcNow))).GetProperty("load");
            Assert.Equal(50, ok.GetProperty("percent").GetDouble());
            Assert.Equal(16, ok.GetProperty("inUse").GetInt32());
            Assert.Equal(JsonValueKind.Null, ok.GetProperty("problem").ValueKind);
            JsonElement over = Js.Parse(Js.Serialize(Program.RoomStatus(_fx.Room, LoadReading.Of(105), DateTime.UtcNow))).GetProperty("load");
            Assert.Equal(105, over.GetProperty("percent").GetDouble());                    // above 100 is a reading in turbo, as the cap takes it
            JsonElement failed = Js.Parse(Js.Serialize(Program.RoomStatus(_fx.Room, LoadReading.Failed("typeperf could not be run"), DateTime.UtcNow))).GetProperty("load");
            Assert.Equal(JsonValueKind.Null, failed.GetProperty("percent").ValueKind);
            Assert.Equal("typeperf could not be run", failed.GetProperty("problem").GetString());
        }

        [Fact]
        public void AnExistingLaunchLogRefusesTheNameAndIsNeverRemovedByTheRefusal()
        {
            Directory.CreateDirectory(_fx.RunsDir);
            string log = Path.Combine(_fx.RunsDir, "claimed.launch.log");
            File.WriteAllText(log, "another room's claim");
            OperationResult r = _fx.Call("run.launch", Launch("claimed"));
            Assert.False(r.Ok);
            Assert.Contains("launch.log exists", r.Error);
            Assert.Empty(_fx.Launcher.Started);
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "claimed")));
            Assert.Equal("another room's claim", File.ReadAllText(log));
        }

        [Fact]
        public void ALaunchClaimsItsNameByCreatingTheLogBesideAnEmptyFolderAndAFailedStartGivesTheClaimBack()
        {
            Fx.Result(_fx.Call("run.launch", Launch("held")));
            string log = Path.Combine(_fx.RunsDir, "held.launch.log"), folder = Path.Combine(_fx.RunsDir, "held");
            Assert.True(File.Exists(log));
            Assert.Empty(Directory.GetFileSystemEntries(folder));       // nothing is put inside the run folder, which the nursery requires empty
            Assert.Contains("exists", _fx.Call("run.launch", Launch("held")).Error);      // the folder is there
            Directory.Delete(folder);
            Assert.Contains("launch.log exists", _fx.Call("run.launch", Launch("held")).Error);   // the claim alone holds the name
            Assert.Single(_fx.Launcher.Started);

            _fx.Launcher.Throw = new InvalidOperationException("no such program");
            Assert.Equal(500, _fx.Call("run.launch", Launch("not-started")).Status);
            Assert.False(File.Exists(Path.Combine(_fx.RunsDir, "not-started.launch.log")));
            Assert.False(Directory.Exists(Path.Combine(_fx.RunsDir, "not-started")));
            _fx.Launcher.Throw = null;
            Fx.Result(_fx.Call("run.launch", Launch("not-started")));
        }

        [Fact]
        public void TwoRoomsAskingForOneNameAtOnceStartOneRunBetweenThem()
        {
            // two rooms on one tree each have their own lock, so only the claim can keep the second out
            for (int i = 0; i < 12; i++)
            {
                string name = "race-" + i;
                var launcherB = new FakeLauncher();
                var roomB = new RoomContext(_fx.Root, _fx.Room.Settings, _fx.Main, _fx.Registry, Path.Combine(_fx.Dir, "activity-b.jsonl"), launcherB, _fx.Load, 32, _ => _fx.Build);
                var opsB = new OperationList(roomB);
                var results = new OperationResult[2];
                Parallel.Invoke(
                    () => results[0] = _fx.Ops.Invoke("run.launch", Js.Parse(Launch(name, threads: 1)), OperationList.Screen),
                    () => results[1] = opsB.Invoke("run.launch", Js.Parse(Launch(name, threads: 1)), OperationList.Screen));
                Assert.Equal(1, results.Count(r => r.Ok));
                Assert.Equal(1, _fx.Launcher.Started.Count(s => s.Folder.EndsWith(name)) + launcherB.Started.Count);
                Assert.True(File.Exists(Path.Combine(_fx.RunsDir, name + ".launch.log")));
            }
        }

        [Fact]
        public void TheActivityRowRecordsWhichVariablesTheRunDidNotInherit()
        {
            _fx.Launcher.Outcome = () => new LaunchOutcome { Pid = 9, DroppedEnvironment = new[] { "EVOSIM_GPU_DEVICE", "EVOSIM_REPO_ROOT" } };
            JsonElement r = Fx.Result(_fx.Call("run.launch", Launch("env-run")));
            Assert.Equal(new[] { "EVOSIM_GPU_DEVICE", "EVOSIM_REPO_ROOT" }, r.GetProperty("droppedEnvironment").EnumerateArray().Select(e => e.GetString()).ToArray());
            JsonElement row = _fx.LastRow().GetProperty("result");
            Assert.Equal(new[] { "EVOSIM_GPU_DEVICE", "EVOSIM_REPO_ROOT" }, row.GetProperty("droppedEnvironment").EnumerateArray().Select(e => e.GetString()).ToArray());
            Assert.Equal("rounds/nursery/brain2-w5.json", row.GetProperty("recipe").GetString());
        }
    }

    public class LaunchEnvironmentTests
    {
        [Fact]
        public void TheRepoRootAndEveryGpuVariableAreDroppedAndTheRestKept()
        {
            var vars = new Hashtable
            {
                ["EVOSIM_REPO_ROOT"] = "D:/x", ["evosim_gpu_device"] = "1", ["EVOSIM_GPU_TRANSPORT"] = "a", ["EVOSIM_OTHER"] = "keep", ["PATH"] = "C:/bin", ["EVOSIM_GPUS"] = "kept: not EVOSIM_GPU_",
            };
            LaunchEnvironment.Split(vars, out SortedDictionary<string, string> kept, out List<string> dropped);
            Assert.Equal(new[] { "EVOSIM_GPU_TRANSPORT", "EVOSIM_REPO_ROOT", "evosim_gpu_device" }.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray(), dropped.ToArray());
            Assert.Equal(new[] { "EVOSIM_GPUS", "EVOSIM_OTHER", "PATH" }, kept.Keys.ToArray());
            string block = LaunchEnvironment.Block(kept);
            Assert.EndsWith("\0\0", block);
            Assert.DoesNotContain("REPO_ROOT", block);
            Assert.DoesNotContain("D:/x", block);
            Assert.Contains("EVOSIM_OTHER=keep\0", block);
        }
    }

    /// <summary>The real launcher with the real shape of a room.json launch: a program in a folder whose path holds a space, by a forward-slash path, with quoted arguments.</summary>
    public class RealShapeLauncherTests
    {
        private static string EchoOutput()
        {
            // Built with one shared output folder (-p:OutDir=...), as a build beside a running room's locked files must be, the echo program lies beside the tests.
            string beside = AppContext.BaseDirectory.TrimEnd('\\', '/');
            // (the program is its .exe and its .dll together: an .exe left beside the tests by an earlier build without its .dll starts nothing)
            if (File.Exists(Path.Combine(beside, "Evosim.NurseryRoom.TestEcho.exe")) && File.Exists(Path.Combine(beside, "Evosim.NurseryRoom.TestEcho.dll"))) return beside;
            // AppContext.BaseDirectory is <artifacts>/Evosim.NurseryRoom.Tests/bin/<Config>/net8.0/
            var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd('\\', '/'));
            string config = tfm.Parent.Name;
            string artifacts = tfm.Parent.Parent.Parent.Parent.FullName;
            string dir = Path.Combine(artifacts, "Evosim.NurseryRoom.TestEcho", "bin", config, tfm.Name);
            Assert.True(File.Exists(Path.Combine(dir, "Evosim.NurseryRoom.TestEcho.exe")), "the echo program is built beside the tests: " + dir);
            return dir;
        }

        private static string Read(string log)
        {
            using (var s = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var r = new StreamReader(s))
                return r.ReadToEnd();
        }

        [Fact]
        public void AProgramInAFolderWithASpaceStartsByAForwardSlashPathWithItsQuotedArgumentsAndWithoutTheEvosimVariables()
        {
            if (!OperatingSystem.IsWindows()) return;
            string echo = EchoOutput();
            string dir = Where.Fresh("real shape");
            string saved1 = Environment.GetEnvironmentVariable("EVOSIM_REPO_ROOT"), saved2 = Environment.GetEnvironmentVariable("EVOSIM_GPU_DEVICE"), saved3 = Environment.GetEnvironmentVariable("EVOSIM_KEEP_ME");
            try
            {
                string target = Path.Combine(dir, "a space dir");
                Directory.CreateDirectory(target);
                foreach (string f in Directory.GetFiles(echo, "Evosim.NurseryRoom.TestEcho.*").Where(f => !f.EndsWith(".pdb")))
                    File.Copy(f, Path.Combine(target, Path.GetFileName(f)));
                string exe = Path.Combine(target, "Evosim.NurseryRoom.TestEcho.exe").Replace('\\', '/');
                string dll = Path.Combine(target, "nursery.dll").Replace('\\', '/');
                Assert.Contains(' ', exe);
                string log = Path.Combine(dir, "run-1.launch.log").Replace('\\', '/');
                var spec = new LaunchSpec
                {
                    FileName = exe,
                    Arguments = new[] { dll, "--recipe", "rounds/nursery/brain2-w5.json", "--out", "runs/run-1", "--threads", "2" },
                    WorkingDirectory = dir, Folder = Path.Combine(dir, "run-1"), LogFile = log,
                };
                Environment.SetEnvironmentVariable("EVOSIM_REPO_ROOT", "D:/not/for/the/run");
                Environment.SetEnvironmentVariable("EVOSIM_GPU_DEVICE", "1");
                Environment.SetEnvironmentVariable("EVOSIM_KEEP_ME", "1");

                LaunchOutcome o = new ProcessRunLauncher().Start(spec);
                Assert.True(o.Pid > 0);
                Assert.Contains("EVOSIM_REPO_ROOT", o.DroppedEnvironment);
                Assert.Contains("EVOSIM_GPU_DEVICE", o.DroppedEnvironment, StringComparer.OrdinalIgnoreCase);
                Assert.DoesNotContain("EVOSIM_KEEP_ME", o.DroppedEnvironment);

                string text = "";
                // a machine with a swimming nursery on it can take over ten seconds to start a program: wait up to a minute for the line it ends on
                for (int i = 0; i < 600 && !text.Contains("cwd:"); i++)
                {
                    System.Threading.Thread.Sleep(100);
                    if (File.Exists(log)) text = Read(log);
                }
                Assert.True(text.Contains("cwd:"), "the program ran and wrote its console output to the log; the log reads: " + text);
                string[] lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
                Assert.Contains("arg 1: " + dll, lines);
                Assert.Contains("arg 2: --recipe", lines);
                Assert.Contains("arg 3: rounds/nursery/brain2-w5.json", lines);
                Assert.Contains("arg 4: --out", lines);
                Assert.Contains("arg 5: runs/run-1", lines);
                Assert.Contains("arg 6: --threads", lines);
                Assert.Contains("arg 7: 2", lines);
                Assert.DoesNotContain(lines, l => l.StartsWith("arg 8"));
                Assert.Contains("env EVOSIM_KEEP_ME", lines);
                Assert.DoesNotContain(lines, l => l.Contains("EVOSIM_REPO_ROOT") || l.ToUpperInvariant().Contains("EVOSIM_GPU_"));
                Assert.Contains("cwd: " + dir, lines);
            }
            finally
            {
                Environment.SetEnvironmentVariable("EVOSIM_REPO_ROOT", saved1);
                Environment.SetEnvironmentVariable("EVOSIM_GPU_DEVICE", saved2);
                Environment.SetEnvironmentVariable("EVOSIM_KEEP_ME", saved3);
                // best effort: the echo may still be exiting and hold its own files (Windows says access denied, not in use)
                try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
        }

        /// <summary>Starts the echo program under the real launcher with this mask (0: not pinned) and returns the affinity it printed for itself.</summary>
        private static string AffinityPrintedUnder(string echo, string dir, string name, ulong mask)
        {
            string exe = Path.Combine(echo, "Evosim.NurseryRoom.TestEcho.exe").Replace('\\', '/');
            string log = Path.Combine(dir, name + ".launch.log").Replace('\\', '/');
            var spec = new LaunchSpec
            {
                FileName = exe, Arguments = new[] { Path.Combine(echo, "nursery.dll").Replace('\\', '/') },
                WorkingDirectory = dir, Folder = Path.Combine(dir, name), LogFile = log, Affinity = mask,
            };
            LaunchOutcome o = new ProcessRunLauncher().Start(spec);
            Assert.True(o.Pid > 0);
            Assert.Equal(mask, o.Affinity);
            string text = "";
            for (int i = 0; i < 600 && !text.Contains("cwd:"); i++)
            {
                System.Threading.Thread.Sleep(100);
                if (File.Exists(log)) text = Read(log);
            }
            Assert.True(text.Contains("cwd:"), "the program ran and wrote its console output to the log; the log reads: " + text);
            return text.Split('\n').Select(l => l.TrimEnd('\r')).Single(l => l.StartsWith("affinity: "));
        }

        [Fact]
        public void ALaunchIsPinnedToItsMaskBeforeItsFirstInstructionAndTheProgramThatCmdStartsInheritsIt()
        {
            if (!OperatingSystem.IsWindows()) return;
            string echo = EchoOutput();
            string dir = Where.Fresh("pinned");
            try
            {
                // the lowest two processors this test's own process may use: a mask that exists on any machine the suite runs on
                ulong own;
                using (var self = System.Diagnostics.Process.GetCurrentProcess()) own = (ulong)(long)self.ProcessorAffinity;
                ulong low = own & (~own + 1UL), rest = own & ~low, second = rest & (~rest + 1UL);
                ulong mask = low | second;
                Assert.NotEqual(0UL, mask);

                Assert.Equal("affinity: " + CoreMask.Format(mask), AffinityPrintedUnder(echo, dir, "pinned", mask));
                // with no mask the program keeps its parent's, so the pin above is what the launcher did
                if (mask != own) Assert.Equal("affinity: " + CoreMask.Format(own), AffinityPrintedUnder(echo, dir, "unpinned", 0UL));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
        }

        [Fact]
        public void TheRealLauncherRefusesAMaskNamingAProcessorTheMachineLacksBeforeAnyProcessExists()
        {
            if (!OperatingSystem.IsWindows()) return;
            string dir = Where.Fresh("lacking");
            try
            {
                var spec = new LaunchSpec
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "hostname.exe"), Arguments = new string[0],
                    WorkingDirectory = dir, Folder = Path.Combine(dir, "out"), LogFile = Path.Combine(dir, "out.launch.log"),
                    // From the system's mask, not Environment.ProcessorCount, which counts the processors this process may use: a suite pinned
                    // to 8 of 32 read 8 there, and processor 8 exists.
                    Affinity = SystemProcessors.Read() is ulong m && (m >> 63) == 0 ? 1UL << (64 - System.Numerics.BitOperations.LeadingZeroCount(m)) : 0UL,
                };
                if (spec.Affinity == 0UL) return;   // a machine with 64 logical processors has no bit left to lack
                var e = Assert.Throws<ArgumentException>(() => new ProcessRunLauncher().Start(spec));
                Assert.Contains("lacks", e.Message);
                Assert.False(File.Exists(spec.LogFile));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
        }
    }
}

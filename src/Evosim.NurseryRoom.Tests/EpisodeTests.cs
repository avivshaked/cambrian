using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>Stands in for the nursery's process: records what it was asked to start, and says its processes live until the test ends them. Nothing is started.</summary>
    internal sealed class StepLauncher : IRunLauncher
    {
        public readonly List<LaunchSpec> Started = new List<LaunchSpec>();
        public readonly FakeProcesses Procs;
        public int NextPid = 7000;
        public IReadOnlyList<string> Dropped = new[] { "DOTNET_TieredPGO", "EVOSIM_TANK_TRACE" };

        public StepLauncher(FakeProcesses procs) { Procs = procs; }

        public LaunchOutcome Start(LaunchSpec spec)
        {
            Started.Add(spec);
            int pid = NextPid++;
            Procs.Alive.Add(pid);
            return new LaunchOutcome { Pid = pid, Affinity = spec.Affinity, DroppedEnvironment = Dropped };
        }
    }

    internal sealed class FakeProcesses : IProcessControl
    {
        public readonly HashSet<int> Alive = new HashSet<int>();
        public readonly List<int> Stopped = new List<int>();

        public bool IsAlive(int pid, DateTime launchedUtc) => Alive.Contains(pid);

        public void Stop(int pid)
        {
            Stopped.Add(pid);
            Alive.Remove(pid);
        }
    }

    /// <summary>Copies by content, or damages the copy by a byte when asked, as a failing disk would.</summary>
    internal sealed class DamagingCopier : IEpisodeFileCopier
    {
        public bool Damage;

        public void Copy(string source, string target)
        {
            File.Copy(source, target, overwrite: false);
            if (Damage) File.AppendAllText(target, "x");
        }
    }

    /// <summary>
    /// A room on synthetic files that holds something shaped like the pilot: a registry entry 1 (the brain2 recipe, hash right), a run folder in the
    /// main checkout (outside the tree's root, as the real pilot's is) with six genomes and the exam's rows of 18 brains, a fake launcher and fake
    /// processes. The exam's numbers are made up and known: <see cref="E"/>.
    /// </summary>
    internal sealed class EpisodeFixture : IDisposable
    {
        public static readonly int[] Bodies = { 0, 1, 3, 4, 8, 10 };
        public const string RunName = "pilot-fix";

        public readonly Fx Fx;
        public readonly FakeProcesses Procs = new FakeProcesses();
        public readonly StepLauncher Launcher;
        public readonly DamagingCopier Copier = new DamagingCopier();
        public readonly RoomContext Room;
        public readonly OperationList Ops;
        public readonly string RunFolder;
        public readonly string RecipeHash;
        public readonly Recipe Recipe;

        /// <summary>The commit the entry of <see cref="Unloadable"/> says its run was made on.</summary>
        public const string OldCommit = "0ld0c0m";

        /// <summary>The recipe the entry of <see cref="Unloadable"/> names, and the key its world lacks.</summary>
        public const string UnloadableRecipe = "rounds/nursery/old-world-recipe.json";
        public const string MissingKey = "seasonAmplitude";

        public EpisodeFixture(params string[] roomRuns) : this(false, roomRuns) { }

        /// <summary>The same room, whose registry entry 1 names a recipe whose world this build refuses (a copy of the real config without one tunable's key).</summary>
        public static EpisodeFixture Unloadable(params string[] roomRuns) => new EpisodeFixture(true, roomRuns);

        /// <summary>Writes the config without <see cref="MissingKey"/> and a recipe naming it, with the hash of the config the key was taken from, under the room's rounds/nursery.</summary>
        public static void WriteUnloadableWorld(string root)
        {
            string nursery = Path.Combine(root, "rounds", "nursery");
            string config = File.ReadAllText(Path.Combine(nursery, "configs", "a8cbe76c9532bc71.json")).Replace("\r\n", "\n");
            string line = "    \"" + MissingKey + "\": 0,\n";
            Assert.Contains(line, config);
            File.WriteAllText(Path.Combine(nursery, "configs", "old-world.json"), config.Replace(line, ""));
            string recipe = File.ReadAllText(Path.Combine(nursery, "brain2-w5.json")).Replace("\r\n", "\n");
            string from = "\"config\": \"rounds/nursery/configs/a8cbe76c9532bc71.json\"";
            Assert.Contains(from, recipe);
            File.WriteAllText(Path.Combine(nursery, "old-world-recipe.json"),
                recipe.Replace(from, "\"config\": \"rounds/nursery/configs/old-world.json\"").Replace("\"name\": \"brain2-w5\"", "\"name\": \"old-world\""));
        }

        private EpisodeFixture(bool unloadable, string[] roomRuns)
        {
            Fx = Fx.InWorktree(roomRuns);
            Launcher = new StepLauncher(Procs);
            string recipeRel = "rounds/nursery/brain2-w5.json";
            if (unloadable) { WriteUnloadableWorld(Fx.Root); recipeRel = UnloadableRecipe; }
            string recipePath = Path.Combine(Fx.Root, recipeRel.Replace('/', Path.DirectorySeparatorChar));
            Recipe = unloadable ? Recipe.Read(recipePath) : Recipe.Load(recipePath);
            RecipeHash = Recipe.Hash();
            RunFolder = Path.Combine(Fx.Main, "scratch", "nursery", RunName);
            Directory.CreateDirectory(RunFolder);
            File.WriteAllLines(Path.Combine(RunFolder, "tank-exam-genomes.tsv"), Bodies.Select(b => b + "\t{}"), new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(RunFolder, "tank-exam.jsonl"), ExamRows(), new UTF8Encoding(false));
            var entry = new RegistryEntry
            {
                Recipe = "brain2", RecipeHash = RecipeHash, RecipePath = recipeRel, ConfigHash = "x", GitCommit = unloadable ? OldCommit : "x", CoreHash = "x", DynamicsHash = "x",
                FarmHash = "x", NurseryHash = "x", Out = RunFolder, Status = "completed", StartedAtUtc = "x", EndedAtUtc = "x", NotRerunnableBecause = "x",
            };
            File.WriteAllText(Fx.Registry, entry.ToJson() + "\n", new UTF8Encoding(false));
            Room = new RoomContext(Fx.Root, Fx.Room.Settings, Fx.Main, Fx.Registry, Path.Combine(Fx.Dir, "activity.jsonl"), Launcher, Fx.Load, 32,
                _ => Fx.Build, null, Procs, Copier);
            Ops = new OperationList(Room);
        }

        public void Dispose() => Fx.Dispose();

        /// <summary>The exam's made-up number for brain b, genome g, draw d, as the nursery writes a net ("R"); null where <see cref="NullAt"/> says the body did not land.</summary>
        public static string E(int b, int g, int d) => NullAt(b, g, d) ? null : ((b * 100 + g * 10 + d) / 7.0).ToString("R", CultureInfo.InvariantCulture);

        public static bool NullAt(int b, int g, int d) => b == 1 && g == 3 && d == 0;

        private IEnumerable<string> ExamRows()
        {
            for (int b = 0; b < Recipe.Controls.Count; b++)
                for (int g = 0; g < Bodies.Length; g++)
                {
                    string per = string.Join(",", Enumerable.Range(0, 32).Select(d => E(b, g, d) ?? "null"));
                    yield return $"{{\"brain\":\"{Recipe.Controls[b].Label}\",\"genome\":{g},\"body\":{Bodies[g]},\"net\":0,\"per\":[{per}],\"work\":0,\"path\":0,\"fresh\":0,\"score\":0}}";
                }
        }

        public string Cache(int b, int d, int t, string run = RunName) => Path.Combine(Fx.Root, "runs", "episodes", run, $"b{b}-d{d}-t{t}");

        public string TimingFile => Path.Combine(Fx.Main, "scratch", "machine", "timing-read.json");

        public OperationResult Step(int b, int d, int t = 0, string run = "1", bool? fresh = null, string reason = "a test of the re-step", string who = OperationList.Screen)
        {
            var args = new Dictionary<string, object> { ["run"] = run, ["brain"] = b, ["draw"] = d, ["tank"] = t, ["reason"] = reason };
            if (reason == null) args.Remove("reason");
            if (fresh.HasValue) args["fresh"] = fresh.Value;
            return Ops.Invoke("episode.step", Js.Parse(Js.Serialize(args)), who);
        }

        public JsonElement Ok(OperationResult r)
        {
            Assert.True(r.Ok, r.Error);
            return Js.Parse(Js.Serialize(r.Result));
        }

        public JsonElement Read(int b, int d, int t = 0, string run = "1") =>
            Js.Parse(Js.Serialize(new ResourceResolver(Room).Read($"nursery://episode/{run}/{b}/{d}/{t}")));

        public ExamPlanView Plan(string run = "1") => (ExamPlanView)new ResourceResolver(Room).Read("nursery://exam-plan/" + run);

        public List<JsonElement> ActivityRows() => Room.Activity.ReadAll();

        /// <summary>Writes what the nursery writes when an episode ends, for the tank draw d of brain b dealt: <paramref name="net"/> gives each slot's net (null: did not land).</summary>
        public void Finish(int b, int d, Func<int, int, string> net, int t = 0, string bitsOverride = null, string coreHash = "x", string nurseryHash = "x", bool endProcess = true)
        {
            string folder = Cache(b, d, t);
            Directory.CreateDirectory(folder);
            List<int> tank = Plan().Draws[d].Tanks[t];
            var members = new List<string>();
            for (int slot = 0; slot < tank.Count; slot++)
            {
                string n = net(slot, tank[slot]);
                string bits = n == null ? "null" : "\"" + (bitsOverride ?? BitConverter.DoubleToInt64Bits(double.Parse(n, CultureInfo.InvariantCulture)).ToString("x16")) + "\"";
                members.Add($"{{\"slot\":{slot},\"pool\":{tank[slot]},\"landed\":{(n == null ? "false" : "true")},\"net\":{(n == null ? "null" : "\"" + n + "\"")},\"bits\":{bits},\"survived\":true}}");
            }
            File.WriteAllText(Path.Combine(folder, "episode.json"),
                "{\"format\":1,\"program\":\"Evosim.Nursery --restep\",\"build\":{\"gitCommit\":\"x\",\"gitDirty\":false,\"coreHash\":\"" + coreHash + "\",\"dynamicsHash\":\"x\",\"farmHash\":\"x\",\"nurseryHash\":\"" + nurseryHash + "\"," +
                "\"runtime\":\".NET 8\",\"tieredCompilation\":\"False\",\"note\":null},\"frames\":3,\"frameFloats\":10}\n");
            File.WriteAllBytes(Path.Combine(folder, "frames.bin"), new byte[120]);
            File.WriteAllText(Path.Combine(folder, "result.json"),
                "{\"complete\":true,\"episodeSeed\":\"" + Plan().Draws[d].Seed + "\",\"control\":\"own\",\"members\":[" + string.Join(",", members) + "]}\n");
            if (endProcess) Procs.Alive.Clear();
        }

        public void WriteTiming(string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TimingFile));
            File.WriteAllText(TimingFile, text);
        }

        public static string TimingJson(string who, string why, DateTimeOffset until) => $"{{\"who\":\"{who}\",\"why\":\"{why}\",\"until\":\"{until.ToString("o", CultureInfo.InvariantCulture)}\"}}";
    }

    public class EpisodeRequestTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        [Fact]
        public void TheRequestIsBuiltFromTheExamPlanForDrawZeroAndTheNurserysOwnParserTakesIt()
        {
            JsonElement res = _f.Ok(_f.Step(0, 0));
            Assert.Single(_f.Launcher.Started);
            Assert.Equal("running", res.GetProperty("state").GetString());
            Assert.False(res.GetProperty("cached").GetBoolean());
            Assert.Equal("nursery://episode/1/0/0/0", res.GetProperty("uri").GetString());

            string folder = _f.Cache(0, 0, 0);
            string text = File.ReadAllText(Path.Combine(folder, "request.json"));
            JsonElement req = Js.Parse(text);
            Assert.Equal(new[] { "brain", "episode", "generation", "members", "pool", "recipe", "recipeHash", "salt", "seed", "tank" },
                req.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
            Assert.Equal("rounds/nursery/brain2-w5.json", req.GetProperty("recipe").GetString());
            Assert.Equal(_f.RecipeHash, req.GetProperty("recipeHash").GetString());
            Assert.Equal("runs/episodes/pilot-fix/b0-d0-t0/pool.tsv", req.GetProperty("pool").GetString());
            Assert.Equal(JsonValueKind.String, req.GetProperty("seed").ValueKind);
            Assert.Equal("1", req.GetProperty("seed").GetString());
            Assert.Equal("422608528232", req.GetProperty("salt").GetString());
            Assert.Equal(0, req.GetProperty("generation").GetInt32());
            Assert.Equal(0, req.GetProperty("episode").GetInt32());
            Assert.Equal(0, req.GetProperty("brain").GetInt32());
            Assert.Equal(0, req.GetProperty("tank").GetInt32());
            // the draw's tank, in the order the nursery's own deal gives it (the pilot's draw 0, pinned by the plan's own test)
            Assert.Equal(new[] { 1, 2, 0, 5, 3, 4 }, req.GetProperty("members").EnumerateArray().Select(x => x.GetInt32()).ToArray());
            Assert.Equal(_f.Plan().Draws[0].Tanks[0], req.GetProperty("members").EnumerateArray().Select(x => x.GetInt32()).ToList());

            // the nursery's own reader takes it whole: every key it requires, none it does not know
            RestepRequest parsed = RestepRequest.Parse(text);
            Assert.Equal(1UL, parsed.Seed);
            Assert.Equal(422608528232UL, parsed.Salt);
            Assert.Equal(new[] { 1, 2, 0, 5, 3, 4 }, parsed.Members);
        }

        [Fact]
        public void TheLaunchIsPinnedOnOneThreadWithTheEnvironmentCleanedAndTheNurserysArgumentsInOrder()
        {
            _f.Ok(_f.Step(0, 0));
            LaunchSpec spec = _f.Launcher.Started.Single();
            Assert.Equal("dotnet-host", spec.FileName);
            Assert.Equal(new[] { "--restep", "runs/episodes/pilot-fix/b0-d0-t0/request.json", "--out", "runs/episodes/pilot-fix/b0-d0-t0", "--threads", "1" }, spec.Arguments.Skip(1).ToArray());
            Assert.EndsWith("nursery.dll", spec.Arguments[0]);
            Assert.Equal(_f.Fx.Root, spec.WorkingDirectory);
            Assert.Equal(_f.Fx.Room.Settings.Affinity, spec.Affinity);
            Assert.NotEqual(0UL, spec.Affinity);
            Assert.True(spec.CleanForRestep);
            Assert.Equal(_f.Cache(0, 0, 0) + ".step.log", spec.LogFile);
            Assert.Null(CommandLine.Problem(spec));
        }

        [Fact]
        public void APoolOutsideTheTreeIsCopiedByContentAndTheCopysHashIsTheSources()
        {
            JsonElement res = _f.Ok(_f.Step(0, 0));
            string source = Path.Combine(_f.RunFolder, "tank-exam-genomes.tsv");
            string copy = Path.Combine(_f.Cache(0, 0, 0), "pool.tsv");
            Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(copy));
            JsonElement copied = res.GetProperty("copied").EnumerateArray().Single();
            Assert.Equal("pool", copied.GetProperty("what").GetString());
            Assert.Equal(RunRecord.FileSha256(source), copied.GetProperty("sha256").GetString());
            Assert.Equal(RunRecord.FileSha256(source), RunRecord.FileSha256(copy));
            Assert.Equal("runs/episodes/pilot-fix/b0-d0-t0/pool.tsv", copied.GetProperty("to").GetString());
            // the recipe lies inside the tree, so it is named where it is and not copied
            Assert.False(File.Exists(Path.Combine(_f.Cache(0, 0, 0), "recipe.json")));
        }

        [Fact]
        public void ACopyWhoseHashIsNotTheSourcesIsRefusedAndNothingIsStarted()
        {
            _f.Copier.Damage = true;
            OperationResult r = _f.Step(0, 0);
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("the copy of the pool", r.Error);
            Assert.Contains("hashes to", r.Error);
            Assert.Empty(_f.Launcher.Started);
            Assert.False(File.Exists(Path.Combine(_f.Cache(0, 0, 0), "step.json")));
            Assert.Equal("refused", _f.ActivityRows().Last().GetProperty("outcome").GetString());
        }

        [Fact]
        public void APoolInsideTheTreeIsNamedWhereItIsAndNotCopied()
        {
            // a second run whose folder is under the tree's own runs folder
            string inside = Path.Combine(_f.Fx.Root, "runs", "inside-run");
            Directory.CreateDirectory(inside);
            File.Copy(Path.Combine(_f.RunFolder, "tank-exam-genomes.tsv"), Path.Combine(inside, "tank-exam-genomes.tsv"));
            File.Copy(Path.Combine(_f.RunFolder, "tank-exam.jsonl"), Path.Combine(inside, "tank-exam.jsonl"));
            RegistryEntry e = RegistryEntry.ReadAll(_f.Fx.Registry).Single();
            e.Out = inside;
            File.WriteAllText(_f.Fx.Registry, e.ToJson() + "\n");
            JsonElement res = _f.Ok(_f.Step(0, 0));
            Assert.Empty(res.GetProperty("copied").EnumerateArray());
            string pool = Js.Parse(File.ReadAllText(Path.Combine(_f.Cache(0, 0, 0).Replace("pilot-fix", "inside-run"), "request.json"))).GetProperty("pool").GetString();
            Assert.Equal("runs/inside-run/tank-exam-genomes.tsv", pool);
        }

        [Fact]
        public void ARecipeOutsideTheTreeIsCopiedToo()
        {
            // the recipe lives only in the main checkout: under its trials folder, with the configs its base needs beside the tree's own
            string mainRounds = Path.Combine(_f.Fx.Main, "rounds", "nursery");
            Directory.CreateDirectory(Path.Combine(mainRounds, "configs"));
            foreach (string c in Directory.GetFiles(Path.Combine(_f.Fx.Root, "rounds", "nursery", "configs"), "*.json"))
                File.Copy(c, Path.Combine(mainRounds, "configs", Path.GetFileName(c)));
            string only = Path.Combine(_f.Fx.Main, "scratch", "nursery-trials", "main-only.json");
            Directory.CreateDirectory(Path.GetDirectoryName(only));
            File.Copy(Path.Combine(_f.Fx.Root, "rounds", "nursery", "brain2-w5.json"), only);
            RegistryEntry e = RegistryEntry.ReadAll(_f.Fx.Registry).Single();
            e.RecipePath = "scratch/nursery-trials/main-only.json";
            File.WriteAllText(_f.Fx.Registry, e.ToJson() + "\n");

            JsonElement res = _f.Ok(_f.Step(0, 0));
            Assert.Equal(new[] { "pool", "recipe" }, res.GetProperty("copied").EnumerateArray().Select(x => x.GetProperty("what").GetString()).ToArray());
            JsonElement req = Js.Parse(File.ReadAllText(Path.Combine(_f.Cache(0, 0, 0), "request.json")));
            Assert.Equal("runs/episodes/pilot-fix/b0-d0-t0/recipe.json", req.GetProperty("recipe").GetString());
            Assert.Equal(RunRecord.FileSha256(only), RunRecord.FileSha256(Path.Combine(_f.Cache(0, 0, 0), "recipe.json")));
            Assert.Equal(_f.RecipeHash, req.GetProperty("recipeHash").GetString());
        }

        [Fact]
        public void ARecipeThatHashesOtherwiseThanTheRunRecordedIsRefusedBeforeAnythingStarts()
        {
            string path = Path.Combine(_f.Fx.Root, "rounds", "nursery", "brain2-w5.json");
            string edited = File.ReadAllText(path).Replace("\"seed\": \"0x1\",", "\"seed\": \"0x2\",");
            Assert.NotEqual(File.ReadAllText(path), edited);
            File.WriteAllText(path, edited);
            OperationResult r = _f.Step(0, 0);
            Assert.False(r.Ok);
            Assert.Empty(_f.Launcher.Started);
        }

        [Fact]
        public void TheRunsNumberAndItsFolderNameShareOneCache()
        {
            // the run is known twice here, as the pilot is in the real room: as registry entry 1 and by the folder name room.json lists
            using var f = new EpisodeFixture("scratch/nursery/" + EpisodeFixture.RunName);
            f.Ok(f.Step(0, 0, run: "1"));
            f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            JsonElement again = f.Ok(f.Step(0, 0, run: EpisodeFixture.RunName));
            Assert.True(again.GetProperty("cached").GetBoolean());
            Assert.Single(f.Launcher.Started);
        }

        [Fact]
        public void TheBrainDrawAndTankAreChecked_AndEveryRefusalStartsNothing()
        {
            Assert.Contains("brain 18", _f.Step(18, 0).Error);
            Assert.Contains("draw 32", _f.Step(0, 32).Error);
            Assert.Contains("tank 1", _f.Step(0, 0, 1).Error);
            Assert.Contains("brain, draw and tank are 0 or more", _f.Step(-1, 0).Error);
            Assert.Contains("no run \"nope\"", _f.Step(0, 0, run: "nope").Error);
            Assert.Contains("a reason is required", _f.Step(0, 0, reason: null).Error);
            Assert.Empty(_f.Launcher.Started);
            // every call has its row, a refusal of the arguments included
            Assert.Equal(6, _f.ActivityRows().Count);
            Assert.All(_f.ActivityRows(), row => Assert.Equal("refused", row.GetProperty("outcome").GetString()));
        }
    }

    public class EpisodeCacheTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        [Fact]
        public void AFinishedEpisodeIsServedFromTheCacheAndNotSteppedAgain()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            JsonElement again = _f.Ok(_f.Step(0, 0));
            Assert.True(again.GetProperty("cached").GetBoolean());
            Assert.Equal("done", again.GetProperty("state").GetString());
            Assert.Equal("EXACT", again.GetProperty("verdict").GetString());
            Assert.Single(_f.Launcher.Started);
            Assert.Equal(0, _f.Room.GrantedThreads(DateTime.UtcNow));
            // and it is a row of the log like any other
            Assert.Equal(2, _f.ActivityRows().Count);
            Assert.True(_f.ActivityRows().Last().GetProperty("result").GetProperty("cached").GetBoolean());
        }

        [Fact]
        public void FreshStepsItAgainAndTheOldFolderIsKeptBesideIt()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            JsonElement fresh = _f.Ok(_f.Step(0, 0, fresh: true));
            Assert.False(fresh.GetProperty("cached").GetBoolean());
            Assert.Equal("running", fresh.GetProperty("state").GetString());
            Assert.Equal("b0-d0-t0.prev-1", fresh.GetProperty("movedAside").GetString());
            Assert.Equal(2, _f.Launcher.Started.Count);
            string old = _f.Cache(0, 0, 0) + ".prev-1";
            Assert.True(File.Exists(Path.Combine(old, "result.json")), "the finished episode is kept, never written over");
            Assert.False(File.Exists(Path.Combine(_f.Cache(0, 0, 0), "result.json")));
            Assert.True(File.Exists(Path.Combine(_f.Cache(0, 0, 0), "step.json")));
            // a third time: the next free name
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            Assert.Equal("b0-d0-t0.prev-2", _f.Ok(_f.Step(0, 0, fresh: true)).GetProperty("movedAside").GetString());
        }

        [Fact]
        public void ACachedEpisodeOfAnotherRequestIsNotServedAsThisOne()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            string req = Path.Combine(_f.Cache(0, 0, 0), "request.json");
            File.WriteAllText(req, File.ReadAllText(req).Replace("\"episode\":0", "\"episode\":5"));
            OperationResult r = _f.Step(0, 0);
            Assert.False(r.Ok);
            Assert.Contains("another request", r.Error);
            Assert.Contains("fresh: true", r.Error);
            Assert.Single(_f.Launcher.Started);
        }

        [Fact]
        public void AStepThatLivesIsNotStartedTwice()
        {
            _f.Ok(_f.Step(0, 0));
            OperationResult r = _f.Step(0, 0);
            Assert.False(r.Ok);
            Assert.Contains("being stepped now", r.Error);
            Assert.Contains("nursery://episode/1/0/0/0", r.Error);
            Assert.Single(_f.Launcher.Started);
            // even fresh: a live step is never moved from under its process
            Assert.False(_f.Step(0, 0, fresh: true).Ok);
        }

        [Fact]
        public void AStepThatFailedOrWasStoppedIsSteppedAgainWithoutFreshAndItsFilesAreKept()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Procs.Alive.Clear();   // the process ended without a result
            JsonElement again = _f.Ok(_f.Step(0, 0));
            Assert.Equal("b0-d0-t0.prev-1", again.GetProperty("movedAside").GetString());
            Assert.Equal(2, _f.Launcher.Started.Count);
        }
    }

    public class EpisodeEnvironmentTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        [Fact]
        public void AReStepDropsEveryDotnetVariableAndTheTraceAndNamesWhatItDroppedButARunDoesNot()
        {
            var vars = new System.Collections.Hashtable
            {
                ["DOTNET_TieredCompilation"] = "1", ["DOTNET_gcServer"] = "1", ["dotnet_lower"] = "x", ["DOTNET_"] = "bare", ["EVOSIM_TANK_TRACE"] = "1",
                ["EVOSIM_REPO_ROOT"] = "D:/x", ["EVOSIM_GPU_DEVICE"] = "1", ["EVOSIM_KEEP_ME"] = "1", ["PATH"] = "p", ["MYDOTNET_X"] = "keep",
            };
            LaunchEnvironment.Split(vars, out var kept, out List<string> dropped, restep: true);
            Assert.Equal(new[] { "DOTNET_", "dotnet_lower", "DOTNET_gcServer", "DOTNET_TieredCompilation", "EVOSIM_GPU_DEVICE", "EVOSIM_REPO_ROOT", "EVOSIM_TANK_TRACE" }.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), dropped.ToArray());
            Assert.Equal(new[] { "EVOSIM_KEEP_ME", "MYDOTNET_X", "PATH" }, kept.Keys.ToArray());
            string block = LaunchEnvironment.Block(kept);
            Assert.DoesNotContain("TANK_TRACE", block);
            Assert.DoesNotContain("gcServer", block);
            Assert.DoesNotContain("TieredCompilation", block);
            Assert.Contains("MYDOTNET_X=keep\0", block);

            // a run's launch keeps them: only the repository root and the graphics card's variables are dropped there
            LaunchEnvironment.Split(vars, out var keptRun, out List<string> droppedRun);
            Assert.Equal(new[] { "EVOSIM_GPU_DEVICE", "EVOSIM_REPO_ROOT" }, droppedRun.ToArray());
            Assert.Contains("DOTNET_gcServer", keptRun.Keys);
            Assert.Contains("EVOSIM_TANK_TRACE", keptRun.Keys);
        }

        [Fact]
        public void TheStepAsksTheLauncherForTheCleanedEnvironmentAndReportsAndRecordsTheNamesDropped()
        {
            _f.Launcher.Dropped = new[] { "DOTNET_TieredPGO", "DOTNET_TC_QuickJitForLoops", "EVOSIM_TANK_TRACE" };
            JsonElement res = _f.Ok(_f.Step(0, 0));
            Assert.True(_f.Launcher.Started.Single().CleanForRestep);
            Assert.Equal(new[] { "DOTNET_TieredPGO", "DOTNET_TC_QuickJitForLoops", "EVOSIM_TANK_TRACE" }, res.GetProperty("droppedEnvironment").EnumerateArray().Select(x => x.GetString()).ToArray());
            JsonElement row = _f.ActivityRows().Last();
            Assert.Equal(3, row.GetProperty("result").GetProperty("droppedEnvironment").GetArrayLength());
            Assert.Equal(1, row.GetProperty("result").GetProperty("threads").GetInt32());
            Assert.Equal("0xFFFF0000", row.GetProperty("result").GetProperty("affinity").GetString());
        }

        [Fact]
        public void TheRealLauncherStartedForAReStepLeavesTheTraceVariableOutAndKeepsTheOthers()
        {
            if (!OperatingSystem.IsWindows()) return;
            string echo = EchoOutput();
            string dir = Where.Fresh("restep-env");
            string saved1 = Environment.GetEnvironmentVariable("EVOSIM_TANK_TRACE"), saved2 = Environment.GetEnvironmentVariable("EVOSIM_KEEP_ME"), saved3 = Environment.GetEnvironmentVariable("DOTNET_EVOSIM_TEST_JIT");
            try
            {
                string exe = Path.Combine(echo, "Evosim.NurseryRoom.TestEcho.exe").Replace('\\', '/');
                string log = Path.Combine(dir, "step.launch.log").Replace('\\', '/');
                Environment.SetEnvironmentVariable("EVOSIM_TANK_TRACE", "1");
                Environment.SetEnvironmentVariable("EVOSIM_KEEP_ME", "1");
                Environment.SetEnvironmentVariable("DOTNET_EVOSIM_TEST_JIT", "1");
                var spec = new LaunchSpec
                {
                    FileName = exe, Arguments = new[] { Path.Combine(echo, "nursery.dll").Replace('\\', '/'), "--restep", "x/request.json" },
                    WorkingDirectory = dir, Folder = Path.Combine(dir, "out"), LogFile = log, CleanForRestep = true,
                };
                LaunchOutcome o = new ProcessRunLauncher().Start(spec);
                Assert.Contains("EVOSIM_TANK_TRACE", o.DroppedEnvironment);
                Assert.Contains("DOTNET_EVOSIM_TEST_JIT", o.DroppedEnvironment);
                Assert.DoesNotContain("EVOSIM_KEEP_ME", o.DroppedEnvironment);
                string text = "";
                for (int i = 0; i < 600 && !text.Contains("cwd:"); i++)
                {
                    System.Threading.Thread.Sleep(100);
                    if (File.Exists(log))
                        using (var s = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var r = new StreamReader(s)) text = r.ReadToEnd();
                }
                Assert.Contains("cwd:", text);
                string[] lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
                Assert.Contains("env EVOSIM_KEEP_ME", lines);
                Assert.DoesNotContain(lines, l => l.Contains("EVOSIM_TANK_TRACE"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("EVOSIM_TANK_TRACE", saved1);
                Environment.SetEnvironmentVariable("EVOSIM_KEEP_ME", saved2);
                Environment.SetEnvironmentVariable("DOTNET_EVOSIM_TEST_JIT", saved3);
                try { Directory.Delete(dir, true); } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
        }

        private static string EchoOutput()
        {
            string beside = AppContext.BaseDirectory.TrimEnd('\\', '/');
            // (the program is its .exe and its .dll together: an .exe left beside the tests by an earlier build without its .dll starts nothing)
            if (File.Exists(Path.Combine(beside, "Evosim.NurseryRoom.TestEcho.exe")) && File.Exists(Path.Combine(beside, "Evosim.NurseryRoom.TestEcho.dll"))) return beside;
            var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd('\\', '/'));
            string dir = Path.Combine(tfm.Parent.Parent.Parent.Parent.FullName, "Evosim.NurseryRoom.TestEcho", "bin", tfm.Parent.Name, tfm.Name);
            Assert.True(File.Exists(Path.Combine(dir, "Evosim.NurseryRoom.TestEcho.exe")), "the echo program is built beside the tests: " + dir);
            return dir;
        }
    }

    public class EpisodeCapTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        private OperationResult Launch(string name, int threads) =>
            _f.Ops.Invoke("run.launch", Js.Parse(Js.Serialize(new { recipe = "rounds/nursery/brain2-w5.json", name, threads, reason = "holds threads" })), OperationList.Screen);

        [Fact]
        public void ALiveStepCountsOneThreadAsLongAsItsProcessLivesNotForThreeMinutes()
        {
            Assert.Equal(0, _f.Room.GrantedThreads(DateTime.UtcNow));
            _f.Ok(_f.Step(0, 0));
            Assert.Equal(1, _f.Room.GrantedThreads(DateTime.UtcNow));

            // made to have begun ten minutes ago: past the three minutes a launch counts for, and still one thread, since its process lives
            string step = Path.Combine(_f.Cache(0, 0, 0), "step.json");
            File.WriteAllText(step, File.ReadAllText(step).Replace(Js.Str(Js.Parse(File.ReadAllText(step)), "startedUtc"), DateTime.UtcNow.AddMinutes(-10).ToString("o", CultureInfo.InvariantCulture)));
            Assert.Equal(1, _f.Room.GrantedThreads(DateTime.UtcNow));
            Assert.Equal(1, _f.Room.GrantedThreads(DateTime.UtcNow.AddMinutes(30)));

            // its process ends: the thread is free at once, though the step began a moment ago, which a launch's three minutes would still count
            _f.Procs.Alive.Clear();
            Assert.Equal(0, _f.Room.GrantedThreads(DateTime.UtcNow));
        }

        [Fact]
        public void AFinishedEpisodeWhoseProcessHasNotYetGoneCountsNothing()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0), endProcess: false);
            Assert.True(_f.Procs.Alive.Count == 1);
            Assert.Equal(0, _f.Room.GrantedThreads(DateTime.UtcNow));
        }

        [Fact]
        public void AStepThatWouldPassTheCapIsRefusedLikeALaunchAndFitsOnceAStepEnds()
        {
            _f.Fx.Load.Value = LoadReading.Of(0);   // the counter reads nothing, so what the room itself granted is what counts
            OperationResult held = Launch("holder", 14);   // 14 granted for three minutes
            Assert.True(held.Ok, held.Error);
            _f.Ok(_f.Step(0, 0));   // 1 + 14 = 15 of 16
            _f.Ok(_f.Step(0, 1));   // 1 + 15 = 16 of 16
            Assert.Equal(16, _f.Room.GrantedThreads(DateTime.UtcNow));

            OperationResult over = _f.Step(0, 2);
            Assert.False(over.Ok);
            Assert.Equal(400, over.Status);
            Assert.Contains("1 thread asked", over.Error);
            Assert.Contains("past the cap of 16 of 32", over.Error);
            Assert.Contains("episode re-steps", over.Error);
            Assert.Equal(2, _f.Launcher.Started.Count - 1);   // the launch and two steps, and not the refused one
            Assert.False(File.Exists(Path.Combine(_f.Cache(0, 2, 0), "step.json")));
            Assert.Equal("refused", _f.ActivityRows().Last().GetProperty("outcome").GetString());

            // the first step ends: its thread is released, and the refused one fits
            _f.Procs.Alive.Remove(_f.Launcher.NextPid - 2);
            Assert.Equal(15, _f.Room.GrantedThreads(DateTime.UtcNow));
            _f.Ok(_f.Step(0, 2));
            Assert.Equal(16, _f.Room.GrantedThreads(DateTime.UtcNow));
        }

        [Fact]
        public void TheCapIsCheckedWithTheLoadNowAndAnUnreadableLoadRefuses()
        {
            _f.Fx.Load.Value = LoadReading.Of(100);
            OperationResult r = _f.Step(0, 0);
            Assert.False(r.Ok);
            Assert.Contains("past the cap", r.Error);
            _f.Fx.Load.Value = LoadReading.Failed("no counter");
            Assert.Contains("cannot be read", _f.Step(0, 0).Error);
            Assert.Empty(_f.Launcher.Started);
        }

        [Fact]
        public void AStepAsksForAMaskTheMachineHasOrIsRefused()
        {
            // a machine of eight processors cannot be asked to pin to the efficiency cores of a 32-processor one
            var room = new RoomContext(_f.Fx.Root, _f.Fx.Room.Settings, _f.Fx.Main, _f.Fx.Registry, Path.Combine(_f.Fx.Dir, "a2.jsonl"), _f.Launcher, _f.Fx.Load, 8, _ => _f.Fx.Build, null, _f.Procs, _f.Copier);
            var ops = new OperationList(room);
            OperationResult r = ops.Invoke("episode.step", Js.Parse("{\"run\":\"1\",\"brain\":0,\"draw\":0,\"tank\":0,\"reason\":\"a test\"}"));
            Assert.False(r.Ok);
            Assert.Contains("lacks", r.Error);
            Assert.Empty(_f.Launcher.Started);
        }
    }

    public class EpisodeTimingReadTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose()
        {
            try { if (File.Exists(_f.TimingFile)) File.SetAttributes(_f.TimingFile, FileAttributes.Normal); } catch (IOException) { }
            _f.Dispose();
        }

        private static DateTimeOffset Later => DateTimeOffset.UtcNow.AddHours(1);

        private JsonElement Status() => Js.Parse(Js.Serialize(Program.RoomStatus(_f.Room, LoadReading.Of(10), DateTime.UtcNow)));

        [Fact]
        public void WhileTheFileIsLiveAStepIsRefusedQuotingWhoAndWhy()
        {
            _f.WriteTiming(EpisodeFixture.TimingJson("evolution-simulator-6e", "taking a pace read of the tank bench", Later));
            OperationResult r = _f.Step(0, 0);
            Assert.False(r.Ok);
            Assert.Equal(400, r.Status);
            Assert.Contains("evolution-simulator-6e", r.Error);
            Assert.Contains("taking a pace read of the tank bench", r.Error);
            Assert.Contains("timing read", r.Error);
            Assert.Empty(_f.Launcher.Started);
            JsonElement row = _f.ActivityRows().Last();
            Assert.Equal("refused", row.GetProperty("outcome").GetString());
            Assert.Contains("evolution-simulator-6e", row.GetProperty("detail").GetString());
            // refused even for an episode the cache holds: a timing read is declared
            File.Delete(_f.TimingFile);
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (s, p) => EpisodeFixture.E(0, p, 0));
            _f.WriteTiming(EpisodeFixture.TimingJson("a", "b", Later));
            Assert.False(_f.Step(0, 0).Ok);
        }

        [Fact]
        public void AStaleFileCountsAsAbsentAndTheMachinePanelSaysSo()
        {
            _f.WriteTiming(EpisodeFixture.TimingJson("a crashed timer", "its read", DateTimeOffset.UtcNow.AddMinutes(-5)));
            JsonElement s = Status().GetProperty("timingRead");
            Assert.Equal("stale", s.GetProperty("state").GetString());
            Assert.False(s.GetProperty("blocks").GetBoolean());
            Assert.Contains("counts as absent", s.GetProperty("note").GetString());
            _f.Ok(_f.Step(0, 0));
            Assert.True(File.Exists(_f.TimingFile), "the room never deletes it, stale or not");
        }

        [Theory]
        [InlineData("this is not json")]
        [InlineData("{\"who\":\"x\",\"why\":\"y\"}")]
        [InlineData("{\"who\":\"x\",\"why\":\"y\",\"until\":\"tomorrow-ish\"}")]
        [InlineData("[1,2]")]
        [InlineData("")]
        public void AFileThatDoesNotParseCountsAsAbsentAndTheMachinePanelSaysSo(string text)
        {
            _f.WriteTiming(text);
            JsonElement s = Status().GetProperty("timingRead");
            Assert.Equal("unparsable", s.GetProperty("state").GetString());
            Assert.Contains("counts as absent", s.GetProperty("note").GetString());
            Assert.False(s.GetProperty("blocks").GetBoolean());
            _f.Ok(_f.Step(0, 0));
        }

        [Fact]
        public void NoFileIsNone()
        {
            Assert.Equal("none", Status().GetProperty("timingRead").GetProperty("state").GetString());
        }

        [Fact]
        public void AFileWithAnOffsetFreeTimeIsReadAsLocalTimeAndAFutureOneBlocks()
        {
            _f.WriteTiming("{\"who\":\"w\",\"why\":\"y\",\"until\":\"" + DateTime.Now.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "\"}");
            Assert.Equal("declared", Status().GetProperty("timingRead").GetProperty("state").GetString());
            _f.WriteTiming("{\"who\":\"w\",\"why\":\"y\",\"until\":\"" + DateTime.Now.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "\"}");
            Assert.Equal("stale", Status().GetProperty("timingRead").GetProperty("state").GetString());
        }

        [Fact]
        public void AStepThatLivesWhenTheFileAppearsIsStoppedAndMarkedWhenTheEpisodeIsRead()
        {
            JsonElement started = _f.Ok(_f.Step(0, 0));
            int pid = started.GetProperty("pid").GetInt32();
            Assert.Contains(pid, _f.Procs.Alive);
            Assert.Equal("running", _f.Read(0, 0).GetProperty("state").GetString());

            _f.WriteTiming(EpisodeFixture.TimingJson("evolution-simulator-6e", "a pace read", Later));
            JsonElement now = _f.Read(0, 0);
            Assert.Equal("stopped", now.GetProperty("state").GetString());
            Assert.Equal("stopped for a timing read", now.GetProperty("reason").GetString());
            Assert.Equal("evolution-simulator-6e", now.GetProperty("stoppedBy").GetProperty("who").GetString());
            Assert.Equal("a pace read", now.GetProperty("stoppedBy").GetProperty("why").GetString());
            Assert.Equal(new[] { pid }, _f.Procs.Stopped.ToArray());
            Assert.DoesNotContain(pid, _f.Procs.Alive);
            Assert.True(File.Exists(Path.Combine(_f.Cache(0, 0, 0), "stopped.json")));
            Assert.Equal(0, _f.Room.GrantedThreads(DateTime.UtcNow));
            // the room's reading it again does not stop it twice
            _f.Read(0, 0);
            Assert.Single(_f.Procs.Stopped);

            // once the timer has gone, the stopped step is stepped again (its folder kept), and the file was never the room's to delete
            File.Delete(_f.TimingFile);
            Assert.Equal("b0-d0-t0.prev-1", _f.Ok(_f.Step(0, 0)).GetProperty("movedAside").GetString());
        }

        [Fact]
        public void TheMachinePanelsPollStopsAStepToo()
        {
            int pid = _f.Ok(_f.Step(0, 0)).GetProperty("pid").GetInt32();
            Assert.Equal(1, Status().GetProperty("resteps").GetProperty("running").GetInt32());
            _f.WriteTiming(EpisodeFixture.TimingJson("w", "y", Later));
            JsonElement s = Status();
            Assert.Equal("declared", s.GetProperty("timingRead").GetProperty("state").GetString());
            Assert.Equal("w", s.GetProperty("timingRead").GetProperty("who").GetString());
            Assert.Equal(0, s.GetProperty("resteps").GetProperty("running").GetInt32());
            Assert.Equal(new[] { pid }, _f.Procs.Stopped.ToArray());
        }

        [Fact]
        public void AFileThatCannotBeReadRefusesTheStepAndStopsNothingUntilItIsKnown()
        {
            _f.WriteTiming(EpisodeFixture.TimingJson("w", "y", Later));
            using (new FileStream(_f.TimingFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                JsonElement s = Status().GetProperty("timingRead");
                Assert.Equal("unreadable", s.GetProperty("state").GetString());
                Assert.True(s.GetProperty("blocks").GetBoolean());
                OperationResult r = _f.Step(0, 0);
                Assert.False(r.Ok);
                Assert.Contains("could not be read", r.Error);
            }
            Assert.Empty(_f.Launcher.Started);
        }

        [Fact]
        public void TheRoomNeverOpensTheFileForWritingAndNeverDeletesItWhateverItsState()
        {
            string file = _f.TimingFile;
            _f.WriteTiming(EpisodeFixture.TimingJson("w", "y", Later));
            byte[] before = File.ReadAllBytes(file);
            DateTime stamp = File.GetLastWriteTimeUtc(file);
            // a read-only file, and a reader holding it open for reading and refusing every writer: a room that opened it for writing would be refused,
            // and would then read the file as unreadable instead of declared
            File.SetAttributes(file, FileAttributes.ReadOnly);
            using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                OperationResult refused = _f.Step(0, 0);
                Assert.False(refused.Ok);
                Assert.Contains("timing read is declared", refused.Error);
                Assert.Equal("declared", _f.Read(0, 0).GetProperty("timingRead").GetProperty("state").GetString());
                Assert.Equal("declared", Status().GetProperty("timingRead").GetProperty("state").GetString());
            }
            Assert.True(File.Exists(file));
            Assert.Equal(before, File.ReadAllBytes(file));
            Assert.Equal(stamp, File.GetLastWriteTimeUtc(file));

            // the other states: a stale file and a garbled one are left where they are, byte for byte
            File.SetAttributes(file, FileAttributes.Normal);
            foreach (string text in new[] { EpisodeFixture.TimingJson("w", "y", DateTimeOffset.UtcNow.AddHours(-1)), "garbled" })
            {
                _f.WriteTiming(text);
                byte[] kept = File.ReadAllBytes(file);
                DateTime when = File.GetLastWriteTimeUtc(file);
                Status();
                _f.Read(0, 0);
                _f.Step(0, 0, fresh: null);
                Assert.True(File.Exists(file));
                Assert.Equal(kept, File.ReadAllBytes(file));
                Assert.Equal(when, File.GetLastWriteTimeUtc(file));
                _f.Procs.Alive.Clear();
            }
        }
    }

    public class EpisodeResourceTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        private static int[] Slots(JsonElement a) => a.EnumerateArray().Select(x => x.GetInt32()).ToArray();

        [Fact]
        public void BeforeAnyStepTheEpisodeIsNone()
        {
            JsonElement v = _f.Read(0, 0);
            Assert.Equal("none", v.GetProperty("state").GetString());
            Assert.Equal("nursery://episode/1/0/0/0", v.GetProperty("uri").GetString());
        }

        [Fact]
        public void ARunningStepShowsItsStreamsSizeAndTheFramesOnceEpisodeJsonGivesTheFrameSize()
        {
            _f.Ok(_f.Step(0, 0));
            string folder = _f.Cache(0, 0, 0);
            Assert.Equal("running", _f.Read(0, 0).GetProperty("state").GetString());
            File.WriteAllBytes(Path.Combine(folder, "frames.bin"), new byte[400]);
            JsonElement p = _f.Read(0, 0).GetProperty("progress");
            Assert.Equal(400, p.GetProperty("framesBytes").GetInt64());
            Assert.Equal(JsonValueKind.Null, p.GetProperty("frames").ValueKind);   // no episode.json yet, so the frame size is not known
            File.WriteAllText(Path.Combine(folder, "episode.json"), "{\"frames\":10,\"frameFloats\":10}");
            p = _f.Read(0, 0).GetProperty("progress");
            Assert.Equal(10, p.GetProperty("frames").GetInt64());
            Assert.Equal(10, p.GetProperty("frameFloats").GetDouble());
        }

        [Fact]
        public void AStepWhoseProcessEndedWithoutAResultFailedAndShowsItsLogsTail()
        {
            _f.Ok(_f.Step(0, 0));
            File.WriteAllLines(_f.Cache(0, 0, 0) + ".step.log", Enumerable.Range(1, 30).Select(i => "line " + i));
            _f.Procs.Alive.Clear();
            JsonElement v = _f.Read(0, 0);
            Assert.Equal("failed", v.GetProperty("state").GetString());
            string[] tail = v.GetProperty("logTail").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Equal(20, tail.Length);
            Assert.Equal("line 30", tail.Last());
            Assert.Equal("line 11", tail.First());
        }

        [Fact]
        public void EveryMemberEqualIsExactAndTheBitsAreCompared()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            JsonElement v = _f.Read(0, 0);
            Assert.Equal("done", v.GetProperty("state").GetString());
            Assert.Equal("EXACT", v.GetProperty("verdict").GetString());
            JsonElement c = v.GetProperty("comparison");
            Assert.Equal(6, c.GetProperty("equal").GetInt32());
            Assert.Equal(6, c.GetProperty("slots").GetArrayLength());
            Assert.Empty(c.GetProperty("differs").EnumerateArray());
            JsonElement[] slots = c.GetProperty("slots").EnumerateArray().ToArray();
            Assert.Equal(new[] { 1, 2, 0, 5, 3, 4 }, slots.Select(s => s.GetProperty("pool").GetInt32()).ToArray());
            Assert.Equal(new[] { "exam.1", "exam.2", "exam.0", "exam.5", "exam.3", "exam.4" }, slots.Select(s => s.GetProperty("genome").GetString()).ToArray());
            Assert.Equal(new[] { 1, 3, 0, 10, 4, 8 }, slots.Select(s => s.GetProperty("body").GetInt32()).ToArray());
            Assert.All(slots, s =>
            {
                Assert.True(s.GetProperty("equal").GetBoolean());
                Assert.Equal(s.GetProperty("expected").GetProperty("bits").GetString(), s.GetProperty("actual").GetProperty("bits").GetString());
                Assert.Equal(16, s.GetProperty("expected").GetProperty("bits").GetString().Length);
            });
            Assert.Equal(EpisodeFixture.E(0, 1, 0), slots[0].GetProperty("expected").GetProperty("net").GetString());
        }

        [Fact]
        public void OneBitOffIsDiffersAndNamesTheSlot()
        {
            _f.Ok(_f.Step(0, 0));
            // slot 2 (pool 0): the last bit of the number flipped, the smallest difference a double has
            _f.Finish(0, 0, (slot, pool) =>
            {
                string n = EpisodeFixture.E(0, pool, 0);
                if (slot != 2) return n;
                long bits = BitConverter.DoubleToInt64Bits(double.Parse(n, CultureInfo.InvariantCulture)) ^ 1L;
                return BitConverter.Int64BitsToDouble(bits).ToString("R", CultureInfo.InvariantCulture);
            });
            JsonElement v = _f.Read(0, 0);
            Assert.Equal("DIFFERS", v.GetProperty("verdict").GetString());
            JsonElement c = v.GetProperty("comparison");
            Assert.Equal(new[] { 2 }, Slots(c.GetProperty("differs")));
            Assert.Equal(5, c.GetProperty("equal").GetInt32());
            Assert.Contains("slot 2", c.GetProperty("reason").GetString());
            JsonElement slot2 = c.GetProperty("slots")[2];
            Assert.False(slot2.GetProperty("equal").GetBoolean());
            Assert.NotEqual(slot2.GetProperty("expected").GetProperty("bits").GetString(), slot2.GetProperty("actual").GetProperty("bits").GetString());
            // and the two numbers are one unit in the last place apart, which no tolerance would have called a difference
            Assert.Equal(1L, Math.Abs(Convert.ToInt64(slot2.GetProperty("expected").GetProperty("bits").GetString(), 16) - Convert.ToInt64(slot2.GetProperty("actual").GetProperty("bits").GetString(), 16)));
        }

        [Fact]
        public void NullAgainstNullIsEqual()
        {
            // brain 1's genome 3 did not land on draw 0, in the run's rows: a result that also says it did not land is the same
            _f.Ok(_f.Step(1, 0));
            _f.Finish(1, 0, (slot, pool) => EpisodeFixture.E(1, pool, 0));
            Assert.Null(EpisodeFixture.E(1, 3, 0));
            JsonElement v = _f.Read(1, 0);
            Assert.Equal("EXACT", v.GetProperty("verdict").GetString());
            JsonElement nullSlot = v.GetProperty("comparison").GetProperty("slots").EnumerateArray().Single(s => s.GetProperty("pool").GetInt32() == 3);
            Assert.True(nullSlot.GetProperty("expected").GetProperty("known").GetBoolean());
            Assert.Equal(JsonValueKind.Null, nullSlot.GetProperty("expected").GetProperty("net").ValueKind);
            Assert.Equal(JsonValueKind.Null, nullSlot.GetProperty("actual").GetProperty("net").ValueKind);
            Assert.True(nullSlot.GetProperty("equal").GetBoolean());
        }

        [Fact]
        public void NullAgainstANumberDiffersBothWays()
        {
            // the run's row says null and the re-step landed: differs
            _f.Ok(_f.Step(1, 0));
            _f.Finish(1, 0, (slot, pool) => pool == 3 ? "1.5" : EpisodeFixture.E(1, pool, 0));
            JsonElement v = _f.Read(1, 0);
            Assert.Equal("DIFFERS", v.GetProperty("verdict").GetString());
            JsonElement c = v.GetProperty("comparison");
            Assert.Equal(new[] { 4 }, Slots(c.GetProperty("differs")));   // pool 3 sits in slot 4 of the pinned deal
            Assert.Equal(3, c.GetProperty("slots")[4].GetProperty("pool").GetInt32());

            // the run's row has a number and the re-step did not land: differs
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => pool == 5 ? null : EpisodeFixture.E(0, pool, 0));
            v = _f.Read(0, 0);
            Assert.Equal("DIFFERS", v.GetProperty("verdict").GetString());
            Assert.Equal(new[] { 3 }, Slots(v.GetProperty("comparison").GetProperty("differs")));
        }

        [Fact]
        public void ABitsFieldThatIsNotTheBitsOfItsNetIsADifference()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0), bitsOverride: "0000000000000001");
            JsonElement v = _f.Read(0, 0);
            Assert.Equal("DIFFERS", v.GetProperty("verdict").GetString());
            Assert.Equal(6, v.GetProperty("comparison").GetProperty("differs").GetArrayLength());
            Assert.Contains("are not the bits of its net", v.GetProperty("comparison").GetProperty("slots")[0].GetProperty("note").GetString());
        }

        [Fact]
        public void WithoutTheRunsRowsNothingIsExactAndTheVerdictSaysItWasNotCompared()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            File.Delete(Path.Combine(_f.RunFolder, "tank-exam.jsonl"));
            JsonElement v = _f.Read(0, 0);
            Assert.Equal("UNCOMPARED", v.GetProperty("verdict").GetString());
            Assert.Contains("tank-exam.jsonl is not in the run", v.GetProperty("comparison").GetProperty("reason").GetString());
        }

        [Fact]
        public void ARowNotYetWrittenIsUncomparedAndNeverExact()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            // only brain 0's first three genomes' rows are in the file so far
            string rows = Path.Combine(_f.RunFolder, "tank-exam.jsonl");
            File.WriteAllLines(rows, File.ReadAllLines(rows).Take(3));
            JsonElement v = _f.Read(0, 0);
            Assert.Equal("UNCOMPARED", v.GetProperty("verdict").GetString());
            JsonElement[] slots = v.GetProperty("comparison").GetProperty("slots").EnumerateArray().ToArray();
            Assert.Equal(JsonValueKind.Null, slots.Single(s => s.GetProperty("pool").GetInt32() == 5).GetProperty("equal").ValueKind);
            Assert.True(slots.Single(s => s.GetProperty("pool").GetInt32() == 1).GetProperty("equal").GetBoolean());
        }

        [Fact]
        public void TheProvenanceSetsTheEpisodesBuildNextToTheRunsAndNamesTheSourceOfThePlan()
        {
            _f.Ok(_f.Step(0, 0));
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0));
            JsonElement p = _f.Read(0, 0).GetProperty("provenance");
            Assert.Equal("rebuilt", p.GetProperty("planSource").GetString());
            Assert.Equal(1, p.GetProperty("entry").GetInt32());
            Assert.Equal("x", p.GetProperty("episodeBuild").GetProperty("coreHash").GetString());
            Assert.Equal(".NET 8", p.GetProperty("episodeBuild").GetProperty("runtime").GetString());
            Assert.Equal("registry entry 1", p.GetProperty("runBuildFrom").GetString());
            Assert.Equal("x", p.GetProperty("runBuild").GetProperty("nurseryHash").GetString());
            Assert.True(p.GetProperty("sameBuild").GetBoolean());
            Assert.Empty(p.GetProperty("buildsDiffer").EnumerateArray());

            // another nursery tree: the builds differ, and the provenance says which
            _f.Finish(0, 0, (slot, pool) => EpisodeFixture.E(0, pool, 0), nurseryHash: "other");
            p = _f.Read(0, 0).GetProperty("provenance");
            Assert.False(p.GetProperty("sameBuild").GetBoolean());
            Assert.Equal(new[] { "nurseryHash" }, p.GetProperty("buildsDiffer").EnumerateArray().Select(x => x.GetString()).ToArray());
            // the verdict is the numbers' alone
            Assert.Equal("EXACT", _f.Read(0, 0).GetProperty("verdict").GetString());
        }

        [Fact]
        public void TheResourceIsATemplateAndAListedResourceAndServedThroughTheMcpAndAMalformedAddressIsRefused()
        {
            var r = new ResourceResolver(_f.Room);
            Assert.Contains(r.Templates.Select(t => t.UriTemplate), t => t == "nursery://episode/{run}/{b}/{d}/{t}");
            Assert.DoesNotContain(r.List().Select(x => x.Uri), u => u.StartsWith("nursery://episode/"));
            _f.Ok(_f.Step(0, 0));
            Assert.Contains(r.List().Select(x => x.Uri), u => u == "nursery://episode/pilot-fix/0/0/0");
            Assert.Equal(r.Templates.Select(t => t.UriTemplate).ToArray(), McpSurface.Templates(r).Select(t => t.UriTemplate).ToArray());
            var read = McpSurface.Read(r, "nursery://episode/1/0/0/0");
            JsonElement j = Js.Parse(((ModelContextProtocol.Protocol.TextResourceContents)read.Contents[0]).Text);
            Assert.Equal("running", j.GetProperty("state").GetString());
            Assert.Throws<ResourceRefused>(() => r.Read("nursery://episode/1/0/0"));
            Assert.Throws<ResourceRefused>(() => r.Read("nursery://episode/1/x/0/0"));
            Assert.Throws<ResourceRefused>(() => r.Read("nursery://episode/"));
            Assert.Throws<ResourceNotFound>(() => r.Read("nursery://episode/nope/0/0/0"));
        }
    }

    public class EpisodeOperationTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture();
        public void Dispose() => _f.Dispose();

        [Fact]
        public void EveryStepHasARowWithItsReasonWhetherItRanWasServedFromTheCacheOrWasRefused()
        {
            _f.Ok(_f.Step(0, 0, reason: "the first look"));
            JsonElement ran = _f.ActivityRows().Last();
            Assert.Equal("episode.step", ran.GetProperty("operation").GetString());
            Assert.Equal("ok", ran.GetProperty("outcome").GetString());
            Assert.Equal("the first look", ran.GetProperty("reason").GetString());
            Assert.Equal("screen", ran.GetProperty("who").GetString());
            Assert.Equal("1", ran.GetProperty("args").GetProperty("run").GetString());
            Assert.Equal(0, ran.GetProperty("args").GetProperty("brain").GetInt32());
            Assert.Equal("running", ran.GetProperty("result").GetProperty("state").GetString());
            // a folder in a row is relative to the main checkout, as every row's is (the room here runs in a worktree of it)
            Assert.Equal("scratch/wt/runs/episodes/pilot-fix/b0-d0-t0", ran.GetProperty("result").GetProperty("folder").GetString());
            Assert.DoesNotContain(Path.GetPathRoot(_f.Fx.Root).Replace("\\", "\\\\"), File.ReadAllText(_f.Room.Activity.Path), StringComparison.OrdinalIgnoreCase);

            _f.Finish(0, 0, (s, p) => EpisodeFixture.E(0, p, 0));
            _f.Ok(_f.Step(0, 0, reason: "the same look again"));
            JsonElement cached = _f.ActivityRows().Last();
            Assert.Equal("the same look again", cached.GetProperty("reason").GetString());
            Assert.True(cached.GetProperty("result").GetProperty("cached").GetBoolean());
            Assert.Equal("EXACT", cached.GetProperty("result").GetProperty("verdict").GetString());

            Assert.False(_f.Step(99, 0, reason: "a wrong brain").Ok);
            JsonElement refused = _f.ActivityRows().Last();
            Assert.Equal("refused", refused.GetProperty("outcome").GetString());
            Assert.Equal("a wrong brain", refused.GetProperty("reason").GetString());
            Assert.Contains("brain 99", refused.GetProperty("detail").GetString());
            Assert.Equal(3, _f.ActivityRows().Count);
        }

        [Fact]
        public void AnAgentsStepThroughTheMcpIsRecordedAsMcp()
        {
            var args = new Dictionary<string, JsonElement>
            {
                ["action"] = Js.Parse("\"step\""), ["run"] = Js.Parse("\"1\""), ["brain"] = Js.Parse("0"), ["draw"] = Js.Parse("0"), ["tank"] = Js.Parse("0"),
                ["reason"] = Js.Parse("\"an agent's first re-step\""),
            };
            var result = McpSurface.Call(_f.Ops, "episode", args);
            Assert.NotEqual(true, result.IsError);
            JsonElement row = _f.ActivityRows().Last();
            Assert.Equal("mcp", row.GetProperty("who").GetString());
            Assert.Equal("an agent's first re-step", row.GetProperty("reason").GetString());
            Assert.Single(_f.Launcher.Started);
        }

        [Fact]
        public void TheOperationIsInItsOwnFamilyWithItsSchemaAndAScreenControlWouldFindItByItsId()
        {
            Operation op = _f.Ops.Find("episode.step");
            Assert.NotNull(op);
            Assert.True(op.Mutates);
            Assert.Equal("episode", op.Family);
            Assert.Equal(new[] { "run", "brain", "draw", "tank" }, op.SchemaElement.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.Contains("fresh", op.SchemaElement.GetProperty("properties").EnumerateObject().Select(p => p.Name));
            var tools = McpSurface.Tools(_f.Ops);
            Assert.Equal(new[] { "recipe", "run", "episode", "compare", "describe" }, tools.Select(t => t.Name).ToArray());
            JsonElement d = Fx.Result(_f.Fx.Call("describe", "{\"family\":\"episode\"}"));
            Assert.Equal("episode.step", d.GetProperty("operations").EnumerateArray().Single().GetProperty("id").GetString());
            // parity: a control of any screen that names the operation names it literally, and it is built (none does yet: no screen control is added in this step)
            foreach (string file in Directory.GetFiles(Where.WwwRoot, "*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".html") || f.EndsWith(".js")))
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), @"episode\.[a-z]+"))
                    Assert.NotNull(_f.Ops.Find(m.Value));
        }
    }

    /// <summary>
    /// A run that recorded its exam plan (3b): the request is built from the record, the plan's source is said to be recorded, and a recipe path
    /// that is run.json's own (absolute, as the nursery writes it) is found.
    /// </summary>
    public class EpisodeRecordedPlanTests : IDisposable
    {
        private readonly EpisodeFixture _f = new EpisodeFixture("scratch/nursery/rec1");
        public void Dispose() => _f.Dispose();

        private string Recorded()
        {
            string folder = Path.Combine(_f.Fx.Main, "scratch", "nursery", "rec1");
            Directory.CreateDirectory(folder);
            ulong[] seeds = ExamPlans.SeedsFor(1UL, 9UL, 0, 2);
            string ids = string.Join(",", Enumerable.Range(0, 10).Select(i => "{\"id\":\"g5." + i + "\",\"body\":" + (i % 3) + "}"));
            string recipe = Path.Combine(_f.Fx.Root, "rounds", "nursery", "brain2-w5.json").Replace("\\", "\\\\");
            File.WriteAllText(Path.Combine(folder, "run.json"),
                "{\"status\":\"completed\",\"seed\":1,\"recipe\":{\"path\":\"" + recipe + "\",\"name\":\"brain2-w5\",\"hash\":\"" + _f.RecipeHash + "\",\"resolved\":{\"setup\":{\"tankSize\":4}," +
                "\"exam\":{\"episodes\":2,\"salt\":\"0x9\",\"controls\":[{\"kind\":\"own\"},{\"kind\":\"constant\",\"power\":0.5}]}}}," +
                "\"build\":{\"gitCommit\":\"abc\",\"coreHash\":\"c\",\"dynamicsHash\":\"d\",\"farmHash\":\"f\",\"nurseryHash\":\"n\"}," +
                "\"exam\":{\"name\":\"exam\",\"brains\":[{\"label\":\"own\",\"kind\":\"own\"},{\"label\":\"constant 0.5\",\"kind\":\"constant\"}],\"genomes\":[" + ids + "],\"episodes\":2,\"salt\":\"9\",\"seeds\":[\"" + seeds[0] + "\",\"" + seeds[1] + "\"]}}");
            File.WriteAllLines(Path.Combine(folder, "tank-exam-genomes.tsv"), Enumerable.Range(0, 10).Select(i => i + "\t{}"));
            return folder;
        }

        [Fact]
        public void TheRequestIsBuiltFromTheRunsOwnRecordAndTheProvenanceSaysRecorded()
        {
            Recorded();
            ExamPlanView plan = _f.Plan("rec1");
            Assert.Equal("record", plan.Source);
            Assert.Equal(_f.RecipeHash, plan.RecipeHash);
            Assert.Equal("tank-exam-genomes.tsv", plan.PoolFile);
            Assert.Equal("rounds/nursery/brain2-w5.json", plan.RecipePath);
            Assert.Equal(3, plan.Draws[1].Tanks.Count);
            Assert.Equal(2, plan.Draws[1].Tanks[2].Count);

            JsonElement res = _f.Ok(_f.Step(1, 1, 2, run: "rec1"));
            JsonElement req = Js.Parse(File.ReadAllText(Path.Combine(_f.Cache(1, 1, 2, "rec1"), "request.json")));
            Assert.Equal(plan.Draws[1].Tanks[2], req.GetProperty("members").EnumerateArray().Select(x => x.GetInt32()).ToList());
            Assert.Equal("9", req.GetProperty("salt").GetString());
            Assert.Equal(1, req.GetProperty("episode").GetInt32());
            Assert.Equal(1, req.GetProperty("brain").GetInt32());
            Assert.Equal(2, req.GetProperty("tank").GetInt32());
            Assert.Equal("rounds/nursery/brain2-w5.json", req.GetProperty("recipe").GetString());   // inside the tree: named where it is
            Assert.Equal(new[] { "pool" }, res.GetProperty("copied").EnumerateArray().Select(x => x.GetProperty("what").GetString()).ToArray());
            RestepRequest.Parse(File.ReadAllText(Path.Combine(_f.Cache(1, 1, 2, "rec1"), "request.json")));

            // done: no exam rows here, so nothing is compared; the provenance says the plan was recorded, and the run's own build is run.json's
            string folder = _f.Cache(1, 1, 2, "rec1");
            File.WriteAllText(Path.Combine(folder, "result.json"), "{\"complete\":true,\"episodeSeed\":\"" + plan.Draws[1].Seed + "\",\"control\":\"constant 0.5\",\"members\":[" +
                string.Join(",", plan.Draws[1].Tanks[2].Select((p, slot) => "{\"slot\":" + slot + ",\"pool\":" + p + ",\"landed\":false,\"net\":null,\"bits\":null,\"survived\":false}")) + "]}\n");
            _f.Procs.Alive.Clear();
            JsonElement v = _f.Read(1, 1, 2, "rec1");
            Assert.Equal("recorded", v.GetProperty("provenance").GetProperty("planSource").GetString());
            Assert.Equal("run.json", v.GetProperty("provenance").GetProperty("runBuildFrom").GetString());
            Assert.Equal("abc", v.GetProperty("provenance").GetProperty("runBuild").GetProperty("gitCommit").GetString());
            Assert.Equal("UNCOMPARED", v.GetProperty("verdict").GetString());
        }

        [Fact]
        public void ARecordWhoseSeedsAreNotWhatItsSeedAndSaltGiveIsRefused()
        {
            string folder = Recorded();
            string run = Path.Combine(folder, "run.json");
            ulong[] seeds = ExamPlans.SeedsFor(1UL, 9UL, 0, 2);
            File.WriteAllText(run, File.ReadAllText(run).Replace("\"" + seeds[1] + "\"", "\"12345\""));
            OperationResult r = _f.Step(0, 1, 0, run: "rec1");
            Assert.False(r.Ok);
            Assert.Contains("would swim another episode", r.Error);
            Assert.Empty(_f.Launcher.Started);
        }
    }

    /// <summary>The real pilot's own files, when this clone or one above it holds them: the request for its draw 0 and the comparison against its own rows. Nothing swims.</summary>
    public class EpisodeRealPilotTests
    {
        private const string Genomes = "pilot-deep-brain2/tank-exam-genomes.tsv", Rows = "pilot-deep-brain2/tank-exam.jsonl", Log = "pilot-deep-brain2/nursery.log";

        [RealRunFact(Genomes, Rows, Log, Skip = "refused by design on this build: it re-steps the pilot's draw 0, whose world (configs/7319d6b528cc77f1.json) this build refuses (it lacks the season and floor-pool-share tunables), so the room refuses the episode before it starts it (logbook/specs/nursery-room-unloadable-recipes.md); it runs at nursery-room e824ed4")]
        public void ThePilotsDrawZeroRequestIsBuiltFromItsRealPlanAndItsRealRowsAreTheComparisonsExpectation()
        {
            string main = RealRunFactAttribute.Find(Log);
            using var fx = Fx.InWorktree();
            var procs = new FakeProcesses();
            var launcher = new StepLauncher(procs);
            // the real registry and the real run folder of the main checkout; the tree is the synthetic one, so the cache lands in a folder this test deletes
            var room = new RoomContext(fx.Root, fx.Room.Settings, main, null, Path.Combine(fx.Dir, "activity.jsonl"), launcher, fx.Load, 32, _ => fx.Build, null, procs, new DamagingCopier());
            var ops = new OperationList(room);
            OperationResult r = ops.Invoke("episode.step", Js.Parse("{\"run\":\"1\",\"brain\":0,\"draw\":0,\"tank\":0,\"reason\":\"the pilot's draw 0\"}"));
            Assert.True(r.Ok, r.Error);
            string folder = Path.Combine(fx.Root, "runs", "episodes", "pilot-deep-brain2", "b0-d0-t0");
            string text = File.ReadAllText(Path.Combine(folder, "request.json"));
            RestepRequest q = RestepRequest.Parse(text);
            Assert.Equal(1UL, q.Seed);
            Assert.Equal(422608528232UL, q.Salt);
            Assert.Equal(new[] { 1, 2, 0, 5, 3, 4 }, q.Members);
            Assert.Equal(0, q.Brain);
            Assert.Equal(0, q.Episode);
            Assert.Equal(0, q.Tank);
            Assert.Equal("rounds/nursery/brain2.json", q.Recipe);
            string source = Path.Combine(main, "scratch", "nursery", "pilot-deep-brain2", "tank-exam-genomes.tsv");
            Assert.Equal(RunRecord.FileSha256(source), RunRecord.FileSha256(Path.Combine(folder, "pool.tsv")));

            // the run's own per[0] of brain 0 for those six genomes, as the file writes them, is what a faithful re-step would give back
            string[] lines = File.ReadAllLines(Path.Combine(main, "scratch", "nursery", Rows));
            string Per0(int pool) => Js.Parse(lines[pool]).GetProperty("per")[0].ValueKind == JsonValueKind.Null ? null : Js.Parse(lines[pool]).GetProperty("per")[0].GetRawText();
            void Finish(Func<int, string> net)
            {
                var members = new List<string>();
                for (int slot = 0; slot < q.Members.Length; slot++)
                {
                    string n = net(q.Members[slot]);
                    members.Add($"{{\"slot\":{slot},\"pool\":{q.Members[slot]},\"landed\":{(n == null ? "false" : "true")},\"net\":{(n == null ? "null" : "\"" + n + "\"")},\"bits\":{(n == null ? "null" : "\"" + BitConverter.DoubleToInt64Bits(double.Parse(n, CultureInfo.InvariantCulture)).ToString("x16") + "\"")},\"survived\":true}}");
                }
                File.WriteAllText(Path.Combine(folder, "result.json"), "{\"complete\":true,\"episodeSeed\":\"4691847893231451394\",\"control\":\"own\",\"members\":[" + string.Join(",", members) + "]}\n");
                procs.Alive.Clear();
            }
            Finish(Per0);
            JsonElement v = Js.Parse(Js.Serialize(new ResourceResolver(room).Read("nursery://episode/1/0/0/0")));
            Assert.Equal("EXACT", v.GetProperty("verdict").GetString());
            Assert.Equal(6, v.GetProperty("comparison").GetProperty("equal").GetInt32());

            // one bit of one number flipped: DIFFERS, naming its slot
            File.Delete(Path.Combine(folder, "result.json"));
            Finish(pool =>
            {
                string n = Per0(pool);
                if (pool != 0 || n == null) return n;
                return BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(double.Parse(n, CultureInfo.InvariantCulture)) ^ 1L).ToString("R", CultureInfo.InvariantCulture);
            });
            v = Js.Parse(Js.Serialize(new ResourceResolver(room).Read("nursery://episode/1/0/0/0")));
            if (Per0(0) != null)
            {
                Assert.Equal("DIFFERS", v.GetProperty("verdict").GetString());
                Assert.Equal(2, v.GetProperty("comparison").GetProperty("differs")[0].GetInt32());   // pool 0 sits in slot 2
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Evosim.Nursery;
using Evosim.NurseryRoom;
using Xunit;

namespace Evosim.NurseryRoom.Tests
{
    /// <summary>
    /// A recipe whose world this build refuses (logbook/specs/nursery-room-unloadable-recipes.md). The fixture is a copy of the real config without one tunable's
    /// key and a recipe naming it, written by the test: the room reads and shows such a recipe, and refuses to step or rerun it, naming the build it needs.
    /// </summary>
    public class UnloadableWorldRecipeViewTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public UnloadableWorldRecipeViewTests() => EpisodeFixture.WriteUnloadableWorld(_fx.Root);
        public void Dispose() => _fx.Dispose();

        private JsonElement View(string rel) => Js.Parse(Js.Serialize(new ResourceResolver(_fx.Room).RecipeView(rel)));

        [Fact]
        public void TheViewGivesTheResolvedFieldsAndSaysTheWorldDidNotLoad()
        {
            string full = _fx.Full(EpisodeFixture.UnloadableRecipe);
            JsonElement v = View(EpisodeFixture.UnloadableRecipe);

            // the fields and the identity come from Read
            Assert.Equal("old-world", v.GetProperty("name").GetString());
            Assert.Equal(Recipe.Read(full).Hash(), v.GetProperty("hash").GetString());
            Assert.Equal(Recipe.Read(full).Canonical(), v.GetProperty("canonical").GetString());
            JsonElement resolved = v.GetProperty("resolved");
            Assert.Equal(18, resolved.GetProperty("exam").GetProperty("controls").GetArrayLength());
            Assert.Equal("old-world", resolved.GetProperty("name").GetString());

            // the check is the world's refusal
            Assert.False(v.GetProperty("ok").GetBoolean());
            Assert.False(v.GetProperty("check").GetProperty("ok").GetBoolean());
            Assert.Contains(EpisodeFixture.MissingKey, v.GetProperty("check").GetProperty("message").GetString());
            JsonElement world = v.GetProperty("world");
            Assert.False(world.GetProperty("loaded").GetBoolean());
            Assert.Contains(EpisodeFixture.MissingKey, world.GetProperty("problem").GetString());
            Assert.Equal("rounds/nursery/configs/old-world.json", world.GetProperty("config").GetString());

            // a launch is refused, with the reason
            Assert.Equal("refused", v.GetProperty("launch").GetProperty("state").GetString());
            Assert.Contains("WORLD NOT LOADED", v.GetProperty("launch").GetProperty("reason").GetString());
            Assert.True(v.TryGetProperty("labels", out _));
            Assert.True(v.TryGetProperty("gitState", out _));
        }

        [Fact]
        public void ABaseWhoseWorldDidNotLoadIsShownToo()
        {
            string child = "rounds/nursery/zz-old-child.json";
            File.WriteAllText(_fx.Full(child), "{\"base\": \"" + EpisodeFixture.UnloadableRecipe + "\", \"name\": \"old-child\"}");
            JsonElement v = View(child);
            Assert.Equal("old-child", v.GetProperty("name").GetString());
            Assert.Equal("old-world", v.GetProperty("base").GetProperty("name").GetString());
            Assert.False(v.GetProperty("world").GetProperty("loaded").GetBoolean());
        }

        [Fact]
        public void TheRecipeDiffComparesRecipesWhoseWorldDidNotLoad()
        {
            string child = "rounds/nursery/zz-old-child.json";
            File.WriteAllText(_fx.Full(child), "{\"base\": \"" + EpisodeFixture.UnloadableRecipe + "\", \"name\": \"old-child\"}");
            JsonElement d = Fx.Result(_fx.Call("recipe.diff", new { path = child }));
            Assert.False(d.GetProperty("identical").GetBoolean());
            Assert.Contains(d.GetProperty("differs").EnumerateArray(), x => x.GetProperty("path").GetString() == "name");
        }

        [Fact]
        public void ARecipeThatLoadsStillSaysItsWorldLoaded()
        {
            JsonElement v = View("rounds/nursery/brain2-w11.json");
            Assert.True(v.GetProperty("ok").GetBoolean());
            Assert.True(v.GetProperty("check").GetProperty("ok").GetBoolean());
            Assert.True(v.GetProperty("world").GetProperty("loaded").GetBoolean());
            Assert.Equal(JsonValueKind.Null, v.GetProperty("world").GetProperty("problem").ValueKind);
            Assert.NotEqual("refused", v.GetProperty("launch").GetProperty("state").GetString());
        }

        [Fact]
        public void ARecipeTheNurseryRefusesOutrightIsShownAsItWas()
        {
            File.WriteAllText(_fx.Full("rounds/nursery/zz-bad.json"), "{ not json");
            JsonElement v = View("rounds/nursery/zz-bad.json");
            Assert.False(v.GetProperty("ok").GetBoolean());
            Assert.Equal(JsonValueKind.Null, v.GetProperty("name").ValueKind);
            Assert.Equal(JsonValueKind.Null, v.GetProperty("world").ValueKind);
        }

        [Fact]
        public void RecipeCheckStillRefusesTheWorldAndNamesTheKey()
        {
            OperationResult r = _fx.Call("recipe.check", new { path = EpisodeFixture.UnloadableRecipe });
            Assert.True(r.Ok, r.Error);
            JsonElement j = Js.Parse(Js.Serialize(r.Result));
            Assert.False(j.GetProperty("ok").GetBoolean());
            Assert.Contains(EpisodeFixture.MissingKey, j.GetProperty("message").GetString());
        }
    }

    public class UnloadableWorldRunTests : IDisposable
    {
        private readonly EpisodeFixture _f = EpisodeFixture.Unloadable();
        public void Dispose() => _f.Dispose();

        [Fact]
        public void TheExamPlanIsRebuiltFromTheRecipeThatCannotBeLoaded()
        {
            ExamPlanView p = _f.Plan();
            Assert.Equal("reconstruction", p.Source);
            Assert.Equal(1, p.Entry);
            Assert.Equal(18, p.Brains.Count);
            Assert.Equal(32, p.Episodes);
            Assert.Equal(_f.RecipeHash, p.RecipeHash);
            Assert.Equal(EpisodeFixture.UnloadableRecipe, p.RecipePath);
            Assert.All(p.Checks, c => Assert.True(c.Ok, c.Name + ": " + c.Detail));
        }

        [Fact]
        public void TheRunsColumnsTakeTheirControlsFromTheEntrysRecipeThatCannotBeLoaded()
        {
            JsonElement p = Js.Parse(Js.Serialize(new ResourceResolver(_f.Room).Run("1"))).GetProperty("progress");
            Assert.Equal(18, p.GetProperty("examExpected").GetInt32());
            Assert.Equal(18, p.GetProperty("examDone").GetInt32());
        }

        [Fact]
        public void ATankEpisodeIsRefusedBeforeAnythingIsMadeOrMoved()
        {
            // an old cache folder is there: it must not be moved aside
            string cache = _f.Cache(0, 0, 0);
            Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "marker.txt"), "stays");
            string runCache = Path.GetDirectoryName(cache);

            OperationResult r = _f.Step(0, 0);
            Assert.False(r.Ok);
            Assert.Contains("this run's world is from an older build", r.Error);
            Assert.Contains("needs a build of commit " + EpisodeFixture.OldCommit, r.Error);
            Assert.Contains("worktree under scratch/", r.Error);
            Assert.Contains(EpisodeFixture.MissingKey, r.Error);
            Assert.Contains("Nothing was started", r.Error);

            Assert.Empty(_f.Launcher.Started);
            Assert.Equal("stays", File.ReadAllText(Path.Combine(cache, "marker.txt")));
            Assert.Equal(new[] { cache }, Directory.GetDirectories(runCache));   // nothing moved aside, nothing new
            Assert.Empty(Directory.GetFiles(runCache));                          // no .step.log
            Assert.Equal(new[] { "marker.txt" }, Directory.GetFiles(cache).Select(Path.GetFileName).ToArray());
        }

        [Fact]
        public void ATankEpisodeWithNoCacheMakesNoFolderWhenTheWorldDidNotLoad()
        {
            Assert.False(_f.Step(0, 0).Ok);
            Assert.False(Directory.Exists(_f.Cache(0, 0, 0)));
            Assert.False(Directory.Exists(Path.Combine(_f.Fx.Root, "runs", "episodes")));
            Assert.Empty(_f.Launcher.Started);
        }
    }

    public class UnloadableWorldRerunTests : IDisposable
    {
        private readonly Fx _fx = new Fx();
        public UnloadableWorldRerunTests() => EpisodeFixture.WriteUnloadableWorld(_fx.Root);
        public void Dispose() => _fx.Dispose();

        private RegistryEntry Entry(string commit, string core) => new RegistryEntry
        {
            Recipe = "old-world", RecipeHash = Recipe.Read(_fx.Full(EpisodeFixture.UnloadableRecipe)).Hash(), RecipePath = EpisodeFixture.UnloadableRecipe,
            Out = Path.Combine(_fx.Dir, "entry-out"), Status = "completed", StartedAtUtc = "2026-10-02T00:00:00Z", EndedAtUtc = "2026-10-02T01:00:00Z",
            GitCommit = commit, CoreHash = core, DynamicsHash = "d", FarmHash = "f", NurseryHash = "n", Rerunnable = true,
        };

        private static string Rerun(string entry) => Js.Serialize(new { entry, name = "again", threads = 4, reason = "a test" });

        [Fact]
        public void ARerunOnAnotherBuildIsRefusedByTheBuildCheckWhichNamesTheCommit()
        {
            RegistryEntry.Append(_fx.Registry, Entry(EpisodeFixture.OldCommit, "an-older-core"));
            OperationResult r = _fx.Call("run.rerun", Rerun("1"));
            Assert.False(r.Ok);
            Assert.Contains("ran on commit " + EpisodeFixture.OldCommit, r.Error);
            Assert.Contains("worktree under scratch/", r.Error);
            Assert.DoesNotContain("edited after its run", r.Error);
            Assert.Empty(_fx.Launcher.Started);
        }

        [Fact]
        public void ARerunWhoseWorldThisBuildRefusesIsRefusedEvenOnTheEntrysBuild()
        {
            RegistryEntry.Append(_fx.Registry, Entry("abc1234", "c"));
            OperationResult r = _fx.Call("run.rerun", Rerun("1"));
            Assert.False(r.Ok);
            Assert.Contains("refused now", r.Error);
            Assert.Contains(EpisodeFixture.MissingKey, r.Error);
            Assert.Empty(_fx.Launcher.Started);
        }
    }
}

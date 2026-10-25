using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// <see cref="Recipe.Read"/> (logbook/specs/nursery-room-unloadable-recipes.md): a recipe is read, merged and checked field by field without opening
    /// its world, so a recipe whose world this build refuses still reads, with the identity it has under <see cref="Recipe.Load"/>.
    /// </summary>
    public class RecipeReadTests : IDisposable
    {
        private static readonly string Root = Recipe.RepoRootAbove(AppContext.BaseDirectory);
        private static readonly string Real = Path.Combine(Root, "rounds", "nursery");
        private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "recipes-read-" + Guid.NewGuid().ToString("N"));

        public RecipeReadTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, true);

        /// <summary>The key the fixture world lacks, as the config file spells it.</summary>
        private const string MissingKey = "seasonAmplitude";

        /// <summary>
        /// A recipe naming a copy of the real config with one tunable's key removed, and the hash the real file had: the shape of a world written before
        /// a tunable was added. Written by the test, so no file under rounds/ is pinned to an old build.
        /// </summary>
        private string UnloadableRecipe()
        {
            string config = File.ReadAllText(Path.Combine(Real, "configs", "b2885199f9213276.json")).Replace("\r\n", "\n");
            string line = "    \"" + MissingKey + "\": 0,\n";
            Assert.Contains(line, config);
            string configPath = Path.Combine(_dir, "old-world.json");
            File.WriteAllText(configPath, config.Replace(line, ""));
            string rel = Path.GetRelativePath(Root, configPath).Replace("\\", "/");
            string recipe = File.ReadAllText(Path.Combine(Real, "brain2-w7.json")).Replace("\r\n", "\n");
            string from = "\"config\": \"rounds/nursery/configs/b2885199f9213276.json\"";
            Assert.Contains(from, recipe);
            string path = Path.Combine(_dir, "old-world-recipe.json");
            File.WriteAllText(path, recipe.Replace(from, "\"config\": \"" + rel + "\"").Replace("\"name\": \"brain2-w7\"", "\"name\": \"old-world\""));
            return path;
        }

        private static string[] Recipes() => Directory.GetFiles(Real, "*.json").Where(f =>
        {
            using (JsonDocument d = JsonDocument.Parse(File.ReadAllText(f)))
                return d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("format", out _) && d.RootElement.TryGetProperty("world", out _);
        }).ToArray();

        [Fact]
        public void ReadGivesTheHashAndTheCanonicalTextLoadGivesForEveryRecipeThatLoads()
        {
            string[] all = Recipes();
            Assert.NotEmpty(all);
            int loaded = 0;
            foreach (string file in all)
            {
                Recipe read = Recipe.Read(file);   // never refused for its world
                Recipe load;
                try { load = Recipe.Load(file); }
                catch (Exception e) when (e is InvalidDataException || e is FormatException)
                {
                    Assert.NotNull(read.WorldProblem());   // a recipe Load refuses here is refused for its world, and the world's problem says so
                    continue;
                }
                loaded++;
                Assert.Equal(load.Hash(), read.Hash());
                Assert.Equal(load.Canonical(), read.Canonical());
                Assert.Null(read.WorldProblem());
            }
            Assert.True(loaded > 0, "no recipe under rounds/nursery/ loads, so nothing was compared");
        }

        [Fact]
        public void AWorldThisBuildRefusesIsReadButNotLoaded()
        {
            string path = UnloadableRecipe();

            Recipe r = Recipe.Read(path);
            Assert.Equal("old-world", r.Name);
            Assert.Equal(18, r.Controls.Count);
            Assert.Equal("b2885199f9213276", r.ConfigHash);
            Assert.False(string.IsNullOrEmpty(r.Hash()));
            Assert.Contains("old-world.json", r.Canonical());   // the canonical text names the config's path, never its contents

            // Core refuses a config that lacks a tunable with a FormatException, not the InvalidDataException a field of the recipe gets.
            string load = Assert.ThrowsAny<Exception>(() => Recipe.Load(path)).Message;
            Assert.Contains(MissingKey, load);
            string problem = r.WorldProblem();
            Assert.NotNull(problem);
            Assert.Contains(MissingKey, problem);
        }

        [Fact]
        public void ARecipeThatReadRefusesIsRefusedByReadToo()
        {
            string text = File.ReadAllText(Path.Combine(Real, "brain2-w7.json")).Replace("\"seed\": \"0x1\"", "\"sed\": \"0x1\"");
            Assert.Contains("sed", text);
            string path = Path.Combine(_dir, "bad.json");
            File.WriteAllText(path, text);
            Assert.Throws<InvalidDataException>(() => Recipe.Read(path));
        }
    }
}

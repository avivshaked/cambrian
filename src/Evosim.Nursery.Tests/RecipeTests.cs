using System;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Evosim.TestSupport;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The recipe (nursery-general-build.md, sections 2 and 3): the baseline reads, its canonical text
    /// reads back to the same text and hash, a base merges under its child, and every field that is
    /// missing, unknown or out of range is refused before anything swims.
    /// </summary>
    public class RecipeTests : IDisposable
    {
        private static readonly string Root = Recipe.RepoRootAbove(AppContext.BaseDirectory);
        private static readonly string Baseline = Path.Combine(Root, "rounds", "nursery", "brain2-w10.json");
        private readonly string _dir = Path.Combine(AppContext.BaseDirectory, "recipes-" + Guid.NewGuid().ToString("N"));

        public RecipeTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, true);

        private string Write(string name, string text)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllText(path, text);
            return path;
        }

        /// <summary>The baseline's text with one replacement, written beside the test.</summary>
        private string Variant(string from, string to)
        {
            string text = File.ReadAllText(Baseline).Replace("\r\n", "\n");   // a checkout may turn the line ends to CRLF
            Assert.Contains(from, text);
            return Write("variant.json", text.Replace(from, to));
        }

        private static string Refusal(string path) => Assert.Throws<InvalidDataException>(() => Recipe.Load(path)).Message;

        /// <summary>A path as a recipe names it: relative to the repository root, with forward slashes.</summary>
        private static string FromRoot(string path) => Path.GetRelativePath(Root, path).Replace("\\", "/");

        /// <summary>A child of the baseline with these members, as JSON text without its braces.</summary>
        private string Child(string members) =>
            Write("child.json", "{\"base\": \"rounds/nursery/brain2-w10.json\", " + members + "}");

        [Fact]
        public void TheBaselineReads()
        {
            Recipe r = Recipe.Load(Baseline);
            Assert.Equal("brain2-w10", r.Name);
            Assert.Equal(1UL, r.Seed);
            Assert.Equal(0x62656E6368UL, r.ExamSalt);
            Assert.Equal(new[] { 0, 1, 3, 4, 8, 10 }, r.Keep);
            Assert.Equal(96, r.Search.Population);
            Assert.Equal(0.785, r.Search.Crossover.MutationByDistance);
            Assert.Equal(20f, r.Snow.IslandWavelength);
            Assert.Equal(0, r.Snow.Layers);
            Assert.True(double.IsNaN(r.PurseSeconds));
            Assert.Equal(18, r.Controls.Count);
            Assert.Equal("knockout Stroke,Hold,Ramp", r.Controls[9].Label);
            Assert.Equal("constant 0.3", r.Controls[11].Label);
            Assert.Equal("kinesis rate -0.5 10", r.Controls[16].Label);

            // The work cost reaches the episode's config, and the floor is closed.
            RunConfig c = r.EpisodeConfig(File.ReadAllText(r.ConfigFullPath));
            Assert.Equal(0.5f, c.WorkCostMultiplier);
            Assert.Equal(0, c.MinimumPopulation);
            Assert.Equal("2950156a342c9bc5", c.Hash());   // the r63b build's config (brain2-w10); brain2-w10's was 78091b3deb236e74, main's config after integration branch 2; brain2-w8's was 1cea30702d4a20c3, the pace build's config; brain2-w7's was be80a0d1eadbfa1d, round 62's migrated config; brain2-w5's was a6d278da00ff70f2, brain2-w4's was 092790eff1a559d3, brain2-w3's 3adad91928939beb, brain2-w2's de49694b78ed5db7 and pilot-deep-brain2's 87d66e98fed542c2 is reference entry 1's, on brain2.json's build
            Assert.Equal(0f, c.Fluid.WaterHoldSeconds);
        }

        /// <summary>
        /// D150 and D152's base: sixteen a tank, and the water held 0.5 s. The hold is a world override, so it is proved where it acts, in
        /// the solver the episode's world is stepped with, and not only in the recipe's text.
        /// </summary>
        [Fact]
        public void TheBaseFromD152HoldsTheWaterAndTakesSixteenATank()
        {
            // The recipe on file, brain2-w5-t16-hold, is on the world config round 62's build refuses, so its members are read from the file and put under brain2-w10.
            string onFile = File.ReadAllText(Path.Combine(Root, "rounds", "nursery", "brain2-w5-t16-hold.json")).Replace("\r\n", "\n");
            Assert.Contains("\"base\": \"rounds/nursery/brain2-w5.json\"", onFile);
            Recipe r = Recipe.Load(Write("brain2-w10-t16-hold.json", onFile.Replace("\"base\": \"rounds/nursery/brain2-w5.json\"", "\"base\": \"rounds/nursery/brain2-w10.json\"")));
            Assert.Equal("brain2-w5-t16-hold", r.Name);
            Assert.Equal(16, r.TankSize);
            Assert.Equal("6bbbf7131b54bde6", r.ConfigHash);   // brain2-w10's world config, the r63b build's move of 220192064e01e1c1, which was main's move of 613ca725c30288c0, which was the pace build's move of b2885199f9213276

            RunConfig c = r.EpisodeConfig(File.ReadAllText(r.ConfigFullPath));
            Assert.Equal(0.5f, c.Fluid.WaterHoldSeconds);
            Assert.Equal(0.5f, c.WorkCostMultiplier);
            Assert.Equal(0.5, Evosim.Dynamics.SolverConfig.From(c, 0.01).WaterHoldSeconds);
            Assert.Equal("b5214c1a61949c76", c.Hash());   // the r63b build's config (brain2-w5-t16-hold on brain2-w10); on brain2-w10 it was 0807cf064abdfffd, main's config after integration branch 2; on brain2-w8 it was f9b472daeb18a422, the pace build's config; on brain2-w7 it was 4c150ba07e111462, round 62's migrated config; brain2-w5-t16-hold's was 04d6be15c0799fdf, brain2-w4-t16-hold's was 341514ba3a32a0d2, brain2-w3-t16-hold's 30cd14a1abe5122a
        }

        [Fact]
        public void TheCanonicalTextReadsBackToItself()
        {
            Recipe r = Recipe.Load(Baseline);
            string text = r.Canonical();
            Recipe again = Recipe.Load(Write("canonical.json", text));
            Assert.Equal(text, again.Canonical());
            Assert.Equal(r.Hash(), again.Hash());
            Assert.Matches("^[0-9a-f]{64}$", r.Hash());
            Assert.DoesNotContain("\r", text);
        }

        [Fact]
        public void AnyChangeMovesTheHash()
        {
            string a = Recipe.Load(Baseline).Hash();
            string b = Recipe.Load(Variant("\"generations\": 100", "\"generations\": 99")).Hash();
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void ABaseMergesUnderItsChild()
        {
            string child = Child("\"name\": \"brain2-short\", \"search\": {\"generations\": 3}, \"world\": {\"overrides\": {\"WorkCostMultiplier\": 0.25}}");
            Recipe r = Recipe.Load(child);
            Assert.Equal("brain2-short", r.Name);
            Assert.Equal(3, r.Search.Generations);
            Assert.Equal(96, r.Search.Population);          // kept from the base
            Assert.Equal("6bbbf7131b54bde6", r.ConfigHash);  // kept from the base's world
            Assert.Equal(0.25f, r.EpisodeConfig(File.ReadAllText(r.ConfigFullPath)).WorkCostMultiplier);
            Assert.DoesNotContain("\"base\"", r.Canonical());
        }

        [Fact]
        public void AWorldAtAPaceFactorAboveOneIsReadAndItsFactorReachesTheEpisodesConfig()
        {
            // D240: the tank carries the world's pace factor. At dt 0.01 and k = 3 a metabolic step holds 150 physics steps, which the recipe may make.
            Recipe r = Recipe.Load(Child("\"name\": \"paced\", \"world\": {\"overrides\": {\"PaceFactor\": 3}}"));
            RunConfig c = r.EpisodeConfig(File.ReadAllText(r.ConfigFullPath));
            Assert.Equal(3f, c.PaceFactor);
            Assert.Equal(0.01f, r.Dt);

            // The factor is the world's: the same recipe at 1 is another hash, and an episode config at another hash.
            Recipe plain = Recipe.Load(Child("\"name\": \"paced\", \"world\": {\"overrides\": {}}"));
            Assert.Equal(1f, plain.EpisodeConfig(File.ReadAllText(plain.ConfigFullPath)).PaceFactor);
            Assert.NotEqual(plain.Hash(), r.Hash());
            Assert.NotEqual(plain.EpisodeConfig(File.ReadAllText(plain.ConfigFullPath)).Hash(), c.Hash());
        }

        [Theory]
        [InlineData(3, 0.04, "setup.dt")]
        [InlineData(3, 0.4, "setup.dt")]
        [InlineData(1, 0.03, "setup.dt")]
        public void ADtThatCannotMakeAMetabolicStepAtTheFactorIsRefusedAtReadNamingTheSettingAndItsValue(int k, double dt, string setting)
        {
            // D240: the same check the farm makes (EnvBinding.ResolvePhysicsStep), made when the recipe is read, so --check refuses it and not the first episode.
            string dtText = dt.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            string refused = Refusal(Child("\"name\": \"bad-dt\", \"setup\": {\"dt\": " + dtText + "}, \"world\": {\"overrides\": {\"PaceFactor\": " + k + "}}"));
            Assert.Contains(setting, refused);
            Assert.Contains(dtText, refused);
            Assert.Contains("PaceFactor of " + k, refused);
            Assert.Contains("Must", refused);   // says what would be accepted: the farm's own words
        }

        [Fact]
        public void ARecipeThatIsItsOwnBaseIsRefused()
        {
            string path = Path.Combine(_dir, "loop.json");
            File.WriteAllText(path, "{\"base\": \"" + FromRoot(path) + "\"}");
            Assert.Contains("its own base", Refusal(path));

            // Two files, each the other's base.
            string a = Path.Combine(_dir, "a.json"), b = Path.Combine(_dir, "b.json");
            File.WriteAllText(a, "{\"base\": \"" + FromRoot(b) + "\"}");
            File.WriteAllText(b, "{\"base\": \"" + FromRoot(a) + "\"}");
            Assert.Contains("its own base", Refusal(a));
        }

        [Fact]
        public void AChainOfBasesMergesInOrder()
        {
            string middle = Child("\"name\": \"middle\", \"search\": {\"generations\": 3, \"draws\": 4}");
            string top = Write("top.json", "{\"base\": \"" + FromRoot(middle) + "\", \"name\": \"top\", \"search\": {\"draws\": 2, \"crossover\": null}}");
            Recipe r = Recipe.Load(top);
            Assert.Equal("top", r.Name);
            Assert.Equal(3, r.Search.Generations);   // from the middle
            Assert.Equal(2, r.Search.Draws);         // from the top
            Assert.Null(r.Search.Crossover);         // null replaces an object
            Assert.Equal(96, r.Search.Population);   // from the baseline
        }

        [Fact]
        public void AChildReplacesItsBasesOverridesWhole()
        {
            // An empty map drops what the base set, rather than merging to the base's.
            Recipe r = Recipe.Load(Child("\"name\": \"full-cost\", \"world\": {\"overrides\": {}}"));
            Assert.Empty(r.Overrides);
            Assert.Equal(0f, r.EpisodeConfig(File.ReadAllText(r.ConfigFullPath)).WorkCostMultiplier);   // the config's own
        }

        [Theory]
        [InlineData("\"base\": null", "recipe.base")]
        [InlineData("\"base\": \"\"", "recipe.base")]
        [InlineData("\"base\": 3", "recipe.base")]
        [InlineData("\"base\": \"D:/Projects/rounds/nursery/brain2.json\"", "relative to the repository root")]
        [InlineData("\"base\": \"../evolution-simulator/rounds/nursery/brain2.json\"", "leaves the repository")]
        [InlineData("\"base\": \"rounds/../../x.json\"", "leaves the repository")]
        public void ABadBaseIsRefused(string member, string said)
        {
            Assert.Contains(said, Refusal(Write("bad-base.json", "{" + member + "}")));
        }

        [Theory]
        [InlineData("0xffffffffffffffff", ulong.MaxValue)]
        [InlineData("18446744073709551615", ulong.MaxValue)]
        [InlineData("9007199254740993", 9007199254740993UL)]   // 2^53 + 1, which a double rounds
        public void ASeedReadsExactly(string seed, ulong value)
        {
            Recipe r = Recipe.Load(Variant("\"seed\": \"0x1\"", "\"seed\": \"" + seed + "\""));
            Assert.Equal(value, r.Seed);
            Assert.Equal(value, Recipe.Load(Write("again.json", r.Canonical())).Seed);
        }

        [Theory]
        [InlineData("\"tankSize\": 8,", "", "missing tankSize")]
        [InlineData("\"tankSize\": 8,", "\"tankSize\": 8, \"bodiesPerTank\": 8,", "unknown bodiesPerTank")]
        [InlineData("\"draws\": 16", "\"draws\": 1000", "search.draws")]
        [InlineData("\"elites\": 8", "\"elites\": 96", "search.elites")]
        [InlineData("\"perBody\": true", "\"perBody\": false", "search.crossover")]
        [InlineData("\"configHash\": \"6bbbf7131b54bde6\"", "\"configHash\": \"0000000000000000\"", "hashes to 6bbbf7131b54bde6")]
        [InlineData("\"WorkCostMultiplier\": 0.5", "\"WorkCostMultiplier\": 0.5, \"NoSuchKnob\": 1", "NoSuchKnob")]
        [InlineData("\"WorkCostMultiplier\": 0.5", "\"WorkCostMultiplier\": true", "WorkCostMultiplier")]
        [InlineData("\"WorkCostMultiplier\": 0.5", "\"MinimumPopulation\": 3", "MinimumPopulation")]
        [InlineData("\"seed\": \"0x1\"", "\"seed\": 1", "recipe.seed")]
        [InlineData("\"floorClosed\": true", "\"floorClosed\": false", "setup.floorClosed")]
        [InlineData("\"birthsHeld\": true", "\"birthsHeld\": false", "setup.birthsHeld")]
        [InlineData("\"layers\": null", "\"layers\": 5", "layers or in islands")]
        [InlineData("\"draw\": \"halfToDouble\"", "\"draw\": \"drawn\"", "setup.snow.draw")]
        [InlineData("\"rule\": \"grown\"", "\"rule\": \"rich\"", "setup.purse.rule")]
        [InlineData("\"rule\": \"grown\"", "\"rule\": \"world\"", "was retired")]
        [InlineData("\"config\": \"rounds/", "\"config\": \"../rounds/", "leaves the repository")]
        [InlineData("\"class\": \"Net\"", "\"class\": \"Speed\"", "Speed")]
        [InlineData("\"parameters\": {}", "\"parameters\": {\"x\": 1}", "x")]
        [InlineData("\"kind\": \"own\"", "\"kind\": \"frozen\"", "starts with")]
        [InlineData("\"Chemical\"", "\"Smell\"", "Smell")]
        [InlineData("\"source\": \"founders\"", "\"source\": \"snapshot\"", "bodies.source")]
        [InlineData("\"keep\": [", "\"keep\": [0, ", "bodies.keep")]
        [InlineData("10\n    ]", "15\n    ]", "the draw has bodies 0 to 11")]
        [InlineData("\"keep\": [\n      0,\n      1,", "\"keep\": [\n      1,\n      0,", "ascending")]
        [InlineData("\"seed\": \"0x1\"", "\"seed\": \"0X1\"", "recipe.seed")]
        [InlineData("\"seed\": \"0x1\"", "\"seed\": \"\"", "recipe.seed")]
        [InlineData("\"seed\": \"0x1\"", "\"seed\": \"+1\"", "recipe.seed")]
        [InlineData("\"format\": 1,", "\"format\": 1, \"extra\": 1,", "unknown extra")]
        [InlineData("\"cover\": 0.1", "\"cover\": 0.1, \"depth\": 1", "unknown depth")]
        [InlineData("\"rate\": 0.5", "\"rate\": 0.5, \"points\": 2", "unknown points")]
        [InlineData("\"rule\": \"grown\"", "\"rule\": \"grown\", \"cap\": 1", "unknown cap")]
        [InlineData("\"kind\": \"own\"", "\"kind\": \"own\", \"power\": 1", "unknown power")]
        [InlineData("\"seconds\": 600", "\"seconds\": 1e400", "setup.seconds")]
        [InlineData("\"WorkCostMultiplier\": 0.5", "\"WorkCostMultiplier\": 1e400", "finite")]
        [InlineData("\"WorkCostMultiplier\": 0.5", "\"WorkCostMultiplier\": 0.5, \"MaximumPopulation\": 2.5", "MaximumPopulation")]
        [InlineData("\"config\": \"rounds/", "\"config\": \"D:/rounds/", "relative to the repository root")]
        [InlineData("\"format\": 1", "\"format\": 2", "recipe.format")]
        [InlineData("\"name\": \"brain2-w10\"", "\"name\": \"brain2 w10\"", "recipe.name")]
        public void AFaultIsRefused(string from, string to, string said)
        {
            Assert.Contains(said, Refusal(Variant(from, to)));
        }

        [Fact]
        public void AnEnumOverrideIsReadByName()
        {
            Recipe r = Recipe.Load(Variant("\"WorkCostMultiplier\": 0.5", "\"WorkCostMultiplier\": 0.5, \"Brain.BrainModel\": \"PerNode\""));
            Assert.Equal(BrainModel.PerNode, r.EpisodeConfig(File.ReadAllText(r.ConfigFullPath)).Brain.BrainModel);
            Assert.Contains("Brain.BrainModel", Refusal(Variant("\"WorkCostMultiplier\": 0.5", "\"WorkCostMultiplier\": 0.5, \"Brain.BrainModel\": \"Central2\"")));
        }

        [Fact]
        public void ABenchHasNoSearch()
        {
            string text = File.ReadAllText(Baseline);
            int at = text.IndexOf("\"search\": {", StringComparison.Ordinal);
            int end = text.IndexOf("\"reward\"", at, StringComparison.Ordinal);
            Recipe r = Recipe.Load(Write("bench.json", text.Substring(0, at) + "\"search\": null,\n  " + text.Substring(end)));
            Assert.Null(r.Search);
            Assert.Contains("\"search\": null", r.Canonical());
        }

        // ---------------------------------------------------------------- D172

        private const string Crossover = "\"mutationByDistance\": 0.785\n    }\n  },";
        private const string Stop = "\"mutationByDistance\": 0.785\n    },\n    \"stop\": {\"measure\": \"eaten\", \"window\": 5, \"gain\": 0.05, \"over\": 10, \"floor\": 20}\n  },";
        private const string Salt = "\"salt\": \"0x62656e6368\",";

        [Fact]
        public void WithoutTheStopOrTheExamPoolARecipeWritesNeither()
        {
            Recipe r = Recipe.Load(Baseline);
            Assert.Null(r.Search.Stop);
            Assert.Null(r.ExamPool);
            Assert.DoesNotContain("\"stop\"", r.Canonical());
            Assert.DoesNotContain("\"pool\": \"tops\"", r.Canonical());
        }

        [Fact]
        public void TheStopAndTheExamPoolReadAndReadBackToThemselves()
        {
            string text = File.ReadAllText(Baseline).Replace("\r\n", "\n").Replace(Crossover, Stop).Replace(Salt, Salt + " \"pool\": \"tops\",");
            Assert.Contains("\"stop\"", text);
            Assert.Contains("\"pool\": \"tops\"", text);
            Recipe r = Recipe.Load(Write("d172.json", text));
            Assert.Equal("eaten", r.Search.Stop.Measure);
            Assert.Equal(5, r.Search.Stop.Window);
            Assert.Equal(0.05, r.Search.Stop.Gain);
            Assert.Equal(10, r.Search.Stop.Over);
            Assert.Equal(20, r.Search.Stop.Floor);
            Assert.Equal(100, r.Search.Generations);
            Assert.Equal("tops", r.ExamPool);
            Recipe again = Recipe.Load(Write("d172-again.json", r.Canonical()));
            Assert.Equal(r.Hash(), again.Hash());
            Assert.NotEqual(Recipe.Load(Baseline).Hash(), r.Hash());
        }

        [Fact]
        public void TheStopIsRefusedOutsideItsRange()
        {
            string Stopped(string stop) => File.ReadAllText(Baseline).Replace("\r\n", "\n").Replace(Crossover,
                "\"mutationByDistance\": 0.785\n    },\n    \"stop\": " + stop + "\n  },");
            Assert.Contains("search.stop.floor", Refusal(Write("a.json", Stopped("{\"measure\": \"eaten\", \"window\": 5, \"gain\": 0.05, \"over\": 10, \"floor\": 101}"))));
            Assert.Contains("search.stop.measure", Refusal(Write("b.json", Stopped("{\"measure\": \"net\", \"window\": 5, \"gain\": 0.05, \"over\": 10, \"floor\": 20}"))));
            Assert.Contains("search.stop.window", Refusal(Write("c.json", Stopped("{\"measure\": \"eaten\", \"window\": 0, \"gain\": 0.05, \"over\": 10, \"floor\": 20}"))));
            Assert.Contains("search.stop.gain", Refusal(Write("d.json", Stopped("{\"measure\": \"eaten\", \"window\": 5, \"gain\": -0.1, \"over\": 10, \"floor\": 20}"))));
            Assert.Contains("search.stop", Refusal(Write("e.json", Stopped("{\"measure\": \"eaten\", \"window\": 5, \"gain\": 0.05, \"over\": 10}"))));
        }

        [Fact]
        public void TheExamPoolIsTopsAndActsOnlyAfterASearch()
        {
            Assert.Contains("exam.pool", Refusal(Variant(Salt, Salt + " \"pool\": \"best\",")));
            string text = File.ReadAllText(Baseline).Replace("\r\n", "\n").Replace(Salt, Salt + " \"pool\": \"tops\",");
            int at = text.IndexOf("\"search\": {", StringComparison.Ordinal);
            int end = text.IndexOf("\"reward\"", at, StringComparison.Ordinal);
            Assert.Contains("exam.pool", Refusal(Write("bench-tops.json", text.Substring(0, at) + "\"search\": null,\n  " + text.Substring(end))));
        }

        // ---------------------------------------------------------------- D178

        private static readonly string Leaf = Path.Combine(Root, "rounds", "nursery", "leaf-direction-sparse-w10.json");
        private const string Keep = "\"keep\": null\n  },";
        private const string Kept = "\"keep\": null,\n    \"brains\": \"kept\"\n  },";

        private string LeafWith(string name, string from, string to)
        {
            string text = File.ReadAllText(Leaf).Replace("\r\n", "\n");
            Assert.Contains(from, text);
            return Write(name, text.Replace(from, to));
        }

        [LocalDataTreeFact("rounds/nursery/pools/6a27d8acada28e03.tsv", "rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void WithoutBrainsAPoolRecipeWritesNoneAndKeepsItsHash()
        {
            Recipe r = Recipe.Load(Leaf);
            Assert.Null(r.Brains);
            Assert.DoesNotContain("\"brains\"", r.Canonical());
            Assert.Equal(r.Hash(), Recipe.Load(Write("leaf-again.json", r.Canonical())).Hash());
        }

        [LocalDataTreeFact("rounds/nursery/pools/6a27d8acada28e03.tsv", "rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void KeptBrainsReadAndReadBackToThemselves()
        {
            Recipe r = Recipe.Load(LeafWith("kept.json", Keep, Kept));
            Assert.Equal("kept", r.Brains);
            Assert.Contains("\"brains\": \"kept\"", r.Canonical());
            Assert.Equal(r.Hash(), Recipe.Load(Write("kept-again.json", r.Canonical())).Hash());
            Assert.NotEqual(Recipe.Load(Leaf).Hash(), r.Hash());
        }

        [LocalDataTreeFact("rounds/nursery/pools/6a27d8acada28e03.tsv", "rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void BrainsAreKeptOnlyFromAPoolIntoASearch()
        {
            Assert.Contains("bodies.brains", Refusal(LeafWith("fresh.json", Keep, Kept.Replace("kept", "fresh"))));
            string founders = File.ReadAllText(Baseline).Replace("\r\n", "\n");
            int keep = founders.IndexOf("\"keep\": [", StringComparison.Ordinal);
            int close = founders.IndexOf("]", keep, StringComparison.Ordinal);
            Assert.Contains("bodies.brains", Refusal(Write("founders-kept.json", founders.Substring(0, close + 1) + ",\n    \"brains\": \"kept\"" + founders.Substring(close + 1))));
            string kept = File.ReadAllText(LeafWith("kept.json", Keep, Kept));
            int at = kept.IndexOf("\"search\": {", StringComparison.Ordinal);
            int end = kept.IndexOf("\"reward\"", at, StringComparison.Ordinal);
            string bench = kept.Substring(0, at) + "\"search\": null,\n  " + kept.Substring(end);
            bench = bench.Replace(",\n    \"pool\": \"tops\"", "");
            Assert.Contains("bodies.brains", Refusal(Write("bench-kept.json", bench)));
        }

        [LocalDataTreeFact("rounds/nursery/pools/6a27d8acada28e03.tsv", "rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void TheStopReadsTheReward()
        {
            Recipe r = Recipe.Load(LeafWith("reward-stop.json", "\"measure\": \"eaten\"", "\"measure\": \"reward\""));
            Assert.Equal("reward", r.Search.Stop.Measure);
            Assert.NotEqual(Recipe.Load(Leaf).Hash(), r.Hash());
        }

        // ---------------------------------------------------------------- held-out exam seeds

        [Fact]
        public void TheExamsSeedsMeetTheSearchsOnlyWhenTheSaltsDo()
        {
            Assert.Null(EpisodeSeeds.ExamOverlap(0x1, 60, 8, 0x62656e6368, 32));
            // The search's own salt as the exam's: the exam's first episode is the search's generation 0, draw 0.
            Assert.Equal((0, 0, 0), EpisodeSeeds.ExamOverlap(0x1, 60, 8, EpisodeSeeds.GenerationSalt, 32));
            // Generation g's draws are the salt's seeds g * 1,000 on, which an exam reaches only with 1,000 draws or more.
            Assert.Equal(EpisodeSeeds.For(0x1, 3, 8, EpisodeSeeds.GenerationSalt)[5], EpisodeSeeds.For(0x1, 0, 3006, EpisodeSeeds.GenerationSalt)[3005]);
        }

        [LocalDataTreeFact("rounds/nursery/pools/6a27d8acada28e03.tsv", "rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void ARecipeWhoseExamWouldSwimTheSearchsEpisodesIsRefused()
        {
            string said = Refusal(LeafWith("same-salt.json", "\"salt\": \"0x62656e6368\"", "\"salt\": \"0x67656e\""));
            Assert.Contains("exam.salt", said);
            Assert.Contains("generation 0, draw 0", said);
        }

        // Recipe.Read refuses a recipe whose pool is absent, so every pool a recipe on file names is declared.
        [LocalDataTreeFact(
            "rounds/nursery/pools/206827daf1cce715.tsv",
            "rounds/nursery/pools/320aa1fd335d62b4.tsv",
            "rounds/nursery/pools/39d06aac2d0d0f60.tsv",
            "rounds/nursery/pools/410a70db44a4b20a.tsv",
            "rounds/nursery/pools/6a27d8acada28e03.tsv",
            "rounds/nursery/pools/6fe2fc2a1bd2061f.tsv",
            "rounds/nursery/pools/97d4a4fc12a91017.tsv",
            "rounds/nursery/pools/9cee8ad6fca64ecd.tsv",
            "rounds/nursery/pools/a326093a77658bdc.tsv",
            "rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void EveryRecipeOnFileExaminesOnHeldOutEpisodes()
        {
            int searched = 0;
            foreach (string file in Directory.GetFiles(Path.Combine(Root, "rounds", "nursery"), "*.json"))
            {
                string text = File.ReadAllText(file);
                if (!text.Contains("\"format\"") || !text.Contains("\"world\"")) continue;
                Recipe r = Recipe.Read(file);
                if (r.Search == null) continue;
                searched++;
                Assert.True(EpisodeSeeds.ExamOverlap(r.Seed, r.Search.Generations, r.Search.Draws, r.ExamSalt, r.ExamEpisodes) == null,
                    Path.GetFileName(file) + " examines on an episode its search swims");
            }
            Assert.True(searched > 10, "too few recipes with a search were read: " + searched);
        }
    }
}

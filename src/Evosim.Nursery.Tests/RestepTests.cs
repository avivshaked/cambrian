using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// <c>--restep</c> and the watcher (TankWatcher.cs, FrameRecorder.cs, Restep.cs): the request is read and refused as it should be, a dry plan
    /// names the seed and the members, the probe offers only reads, and the episode's own source calls the watcher only behind a null test.
    /// <b>Nothing here swims an episode</b>: every test refuses or plans (<c>--check</c>) or reads source. The two that swim are in RestepSwimTests.cs,
    /// behind a trait and an environment variable.
    /// </summary>
    [Collection("restep")]
    public class RestepTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("restep");

        public RestepTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        private string Pool() => RestepFixtures.WritePool(_dir);

        private (int code, string stdout, string stderr) Check(string requestJson, params string[] more)
        {
            string path = RestepFixtures.WriteRequest(_dir, requestJson);
            return RestepFixtures.Run(new[] { "--restep", path, "--check" }.Concat(more).ToArray());
        }

        // ------------------------------------------------------------------ the dry plan

        [Fact]
        public void ThePilotsDrawZeroOwnPlansItsSeedAndItsSixMembersInSlotOrder()
        {
            // The tank is dealt by the nursery's own shuffle: the plan the room pins is [1, 2, 0, 5, 3, 4] on the seed below.
            ulong[] seeds = { Rng.SeedFor(1UL ^ 0x62656E6368UL, 0UL) };
            Assert.Equal(RestepFixtures.PilotSeed0, seeds[0].ToString());
            Assert.Equal(RestepFixtures.PilotTank0, TankLog.PlanTanks(6, 8, seeds)[0][0]);

            string pool = Pool();
            (int code, string stdout, string stderr) = Check(RestepFixtures.PilotRequest(pool));
            _out.WriteLine(stdout);
            Assert.Equal(0, code);
            Assert.Equal("", stderr);
            Assert.Contains("episode seed " + RestepFixtures.PilotSeed0 + "\n", stdout);
            Assert.Contains("episode config hash 1cea30702d4a20c3\n", stdout);   // brain2-w8's (brain2-w7's was be80a0d1eadbfa1d)
            Assert.Contains("control 0: own (own)\n", stdout);
            Assert.Contains("generation 0, episode 0, run seed 1, salt 422608528232\n", stdout);
            Assert.Contains("seconds 600, dt 0.01, sample rate 10 per second (every 10 steps)\n", stdout);

            // Six members, one a line, in the order the tank was dealt: pool index, then the body its line carries.
            string[] members = stdout.Split('\n').Where(l => l.StartsWith("member ")).ToArray();
            Assert.Equal(new[]
            {
                "member 0: pool 1, body 1", "member 1: pool 2, body 3", "member 2: pool 0, body 0",
                "member 3: pool 5, body 10", "member 4: pool 3, body 4", "member 5: pool 4, body 8",
            }, members);
        }

        [Fact]
        public void ADryPlanWritesNothingAndTheKnockoutControlLoadsOnTheGenomes()
        {
            string pool = Pool();
            string[] before = Directory.GetFileSystemEntries(_dir);
            // Brain 3 of the pilot's recipe is the knockout of Chemical: the control is applied to each member's genome in the plan, so a control that cannot be applied refuses here.
            (int code, string stdout, string stderr) = Check(RestepFixtures.PilotRequest(pool, brain: 3));
            Assert.True(code == 0, stderr);
            Assert.Contains("control 3: knockout Chemical (knockout)\n", stdout);
            // Only the request this test wrote itself is new.
            Assert.Equal(before.Length + 1, Directory.GetFileSystemEntries(_dir).Length);
        }

        [Fact]
        public void ASampleRateOfAWholeNumberOfStepsIsKeptAndAnotherIsRefused()
        {
            string pool = Pool();
            (int code, string stdout, _) = Check(RestepFixtures.PilotRequest(pool, extra: ", \"sampleRate\": 20"));
            Assert.Equal(0, code);
            Assert.Contains("sample rate 20 per second (every 5 steps)", stdout);

            (int bad, _, string err) = Check(RestepFixtures.PilotRequest(pool, extra: ", \"sampleRate\": 7"));
            Assert.Equal(2, bad);
            Assert.Contains("not a whole number", err);
        }

        // ------------------------------------------------------------------ the refusals

        [Fact]
        public void AWrongRecipeHashIsRefusedNamingBothHashes()
        {
            string pool = Pool();
            string json = RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", new string('0', 64), pool, RestepFixtures.PilotTank0);
            (int code, string stdout, string stderr) = Check(json);
            Assert.Equal(2, code);
            Assert.Equal("", stdout);
            Assert.Contains(RestepFixtures.RecipeHashOfBrain2(), stderr);
            Assert.Contains(new string('0', 64), stderr);
        }

        [Fact]
        public void MembersOutOfRangeAreRefused()
        {
            string pool = Pool();
            (int code, _, string stderr) = Check(RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", RestepFixtures.RecipeHashOfBrain2(), pool, new[] { 1, 6 }));
            Assert.Equal(2, code);
            Assert.Contains("pool index 6", stderr);
            Assert.Contains("holds 6 genomes", stderr);

            (int negative, _, string e2) = Check(RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", RestepFixtures.RecipeHashOfBrain2(), pool, new[] { 1, -1 }));
            Assert.Equal(2, negative);
            Assert.Contains("0 or more", e2);

            (int twice, _, string e3) = Check(RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", RestepFixtures.RecipeHashOfBrain2(), pool, new[] { 1, 1 }));
            Assert.Equal(2, twice);
            Assert.Contains("twice", e3);

            // A tank holds at most the recipe's tank size (8).
            (int tooMany, _, string e4) = Check(RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", RestepFixtures.RecipeHashOfBrain2(), pool, Enumerable.Range(0, 9).ToArray()));
            Assert.Equal(2, tooMany);
            Assert.True(e4.Contains("tanks hold 8") || e4.Contains("pool index"), e4);
        }

        [Fact]
        public void ABrainBeyondTheRecipesControlsIsRefused()
        {
            string pool = Pool();
            (int code, _, string stderr) = Check(RestepFixtures.PilotRequest(pool, brain: 18));
            Assert.Equal(2, code);
            Assert.Contains("exam has 18 controls", stderr);
        }

        [Fact]
        public void ThreadsOtherThanOneAreRefusedBeforeAnythingIsRead()
        {
            string pool = Pool();
            (int code, string stdout, string stderr) = Check(RestepFixtures.PilotRequest(pool), "--threads", "2");
            Assert.Equal(2, code);
            Assert.Equal("", stdout);
            Assert.Contains("--threads must be 1", stderr);

            // One thread is fine.
            (int ok, _, string e2) = Check(RestepFixtures.PilotRequest(pool), "--threads", "1");
            Assert.True(ok == 0, e2);
        }

        [Fact]
        public void AnOutFolderThatHoldsAResultIsRefusedAndNothingIsWrittenOverIt()
        {
            string pool = Pool();
            string request = RestepFixtures.WriteRequest(_dir, RestepFixtures.PilotRequest(pool));
            string outDir = Path.Combine(_dir, "done");
            Directory.CreateDirectory(outDir);
            string result = Path.Combine(outDir, "result.json");
            File.WriteAllText(result, "{\"complete\": true}");

            // No --check: this is the run's own path, and it refuses before it loads the recipe or steps anything.
            (int code, string stdout, string stderr) = RestepFixtures.Run("--restep", request, "--out", outDir);
            Assert.Equal(2, code);
            Assert.Equal("", stdout);
            Assert.Contains("already holds a result.json", stderr);
            Assert.Equal("{\"complete\": true}", File.ReadAllText(result));
            Assert.Equal(new[] { result }, Directory.GetFileSystemEntries(outDir));
        }

        [Fact]
        public void ARunWithNeitherOutNorCheckIsRefused()
        {
            string request = RestepFixtures.WriteRequest(_dir, RestepFixtures.PilotRequest(Pool()));
            (int code, _, string stderr) = RestepFixtures.Run("--restep", request);
            Assert.Equal(2, code);
            Assert.Contains("--out", stderr);
        }

        [Theory]
        [InlineData("\"recipe\": \"rounds/nursery/brain2-w8.json\"", "\"recipe\": \"../brain2.json\"", "leaves the repository")]
        [InlineData("\"recipe\": \"rounds/nursery/brain2-w8.json\"", "\"recipe\": \"C:/brain2.json\"", "relative to the repository root")]
        [InlineData("\"salt\": \"0x62656e6368\"", "\"salt\": 422608528232", "salt is a string")]
        [InlineData("\"episode\": 0", "\"episode\": 1000", "episode must be 0 to 999")]
        [InlineData("\"seed\": \"0x1\"", "\"seed\": \"one\"", "seed is a string holding a whole number")]
        [InlineData("\"brain\": 0", "\"brain\": 0, \"extra\": 1", "unknown extra")]
        [InlineData("\"brain\": 0,", "", "missing brain")]
        [InlineData("\"tank\": 0,", "", "missing tank")]
        [InlineData("\"tank\": 0", "\"tank\": -1", "tank must be 0 or more")]
        public void AMalformedRequestIsRefusedBeforeAnythingIsLoaded(string from, string to, string expected)
        {
            string json = RestepFixtures.PilotRequest(Pool());
            Assert.Contains(from, json);
            (int code, _, string stderr) = Check(json.Replace(from, to));
            Assert.Equal(2, code);
            Assert.Contains(expected, stderr);
        }

        [Fact]
        public void APoolOutsideTheRepositoryIsRefused()
        {
            string json = RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", RestepFixtures.RecipeHashOfBrain2(), "../../elsewhere.tsv", RestepFixtures.PilotTank0);
            (int code, _, string stderr) = Check(json);
            Assert.Equal(2, code);
            Assert.Contains("leaves the repository", stderr);
        }

        [Fact]
        public void ASeedAboveTwoToTheFiftyThirdSurvivesAsAString()
        {
            RestepRequest q = RestepRequest.Parse(RestepFixtures.RequestJson("a.json", "h", "p.tsv", new[] { 0 }, seed: "18446744073709551601", salt: "0xFFFFFFFFFFFFFFF1"));
            Assert.Equal(18446744073709551601UL, q.Seed);
            Assert.Equal(0xFFFFFFFFFFFFFFF1UL, q.Salt);
            RestepRequest again = RestepRequest.Parse(q.ToJson());
            Assert.Equal(q.Seed, again.Seed);
            Assert.Equal(q.Members, again.Members);
            Assert.Equal(10d, again.SampleRate);
        }

        // ------------------------------------------------------------------ the run's numbers, checked against the recipe the hash ties to the run

        private static readonly ulong Seed0 = Rng.SeedFor(1UL ^ 0x62656E6368UL, 0UL);

        private string WriteRecipe(string name, string setupJson)
        {
            string path = Path.Combine(_dir, name + ".json");
            File.WriteAllText(path, "{\"base\": \"rounds/nursery/brain2-w8.json\", \"name\": \"" + name + "\", \"setup\": " + setupJson + "}");
            return path;
        }

        [Fact]
        public void EachOfTheRunsNumbersThatIsNotTheRecipesIsRefusedNamingItsField()
        {
            string pool = Pool(), hash = RestepFixtures.RecipeHashOfBrain2();
            string R(string seed = "0x1", string salt = "0x62656e6368", int generation = 0, int episode = 0, int[] members = null, int tank = 0, int brain = 0) =>
                RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", hash, pool, members ?? RestepFixtures.PilotTank0, brain, "", seed, salt, generation, episode, tank);

            // The control: the pilot's own request, and the deal of its second episode, which is another tank of six in another order.
            int[] second = RestepFixtures.Deal(6, 8, 1, 0);
            Assert.NotEqual(RestepFixtures.PilotTank0, second);
            Assert.Equal(0, Check(R()).code);
            Assert.Equal(0, Check(R(episode: 1, members: second)).code);

            var cases = new (string json, string[] expected)[]
            {
                (R(seed: "0x2"), new[] { "request's seed is 0x2", "recipe's seed is 1" }),
                (R(salt: "0x1"), new[] { "request's salt is 0x1", "exam salt is 422608528232" }),
                (R(generation: 1), new[] { "request's generation is 1", "an exam's is 0" }),
                (R(episode: 32), new[] { "request's episode is 32", "32 episodes" }),
                (R(brain: 18), new[] { "request's brain is 18" }),
                (R(tank: 1), new[] { "request's tank is 1", "1 tank(s)" }),
                (R(members: RestepFixtures.PilotTank0.Reverse().ToArray()), new[] { "request's members [4,3,5,0,2,1]", "tank 0 of episode 0's deal" }),
                (R(members: RestepFixtures.PilotTank0.Take(5).ToArray()), new[] { "request's members [1,2,0,5,3]", "[1,2,0,5,3,4]" }),
                (R(episode: 1), new[] { "request's members [1,2,0,5,3,4]", "tank 0 of episode 1's deal" }),
            };
            foreach ((string json, string[] expected) in cases)
            {
                (int code, string stdout, string stderr) = Check(json);
                Assert.True(code == 2, stderr);
                Assert.Equal("", stdout);
                foreach (string part in expected) Assert.Contains(part, stderr);
            }
        }

        [Fact]
        public void ATankOtherThanTheFirstIsNamedByItsIndexAndItsMembersMustBeThatTank()
        {
            // Six genomes in tanks of four are dealt as a tank of four and a tank of two.
            string pool = Pool();
            string recipe = WriteRecipe("tanks-of-4", "{\"tankSize\": 4}");
            string relative = RestepFixtures.Relative(recipe), hash = Recipe.Load(recipe).Hash();
            int[][] tanks = TankLog.PlanTanks(6, 4, new[] { Seed0 })[0];
            Assert.Equal(new[] { 4, 2 }, tanks.Select(t => t.Length).ToArray());

            for (int t = 0; t < tanks.Length; t++)
            {
                (int code, string stdout, string stderr) = Check(RestepFixtures.RequestJson(relative, hash, pool, tanks[t], tank: t));
                Assert.True(code == 0, stderr);
                Assert.Contains("member 0: pool " + tanks[t][0] + ",", stdout);
            }
            (int swapped, _, string e1) = Check(RestepFixtures.RequestJson(relative, hash, pool, tanks[1], tank: 0));
            Assert.Equal(2, swapped);
            Assert.Contains("request's members [" + string.Join(",", tanks[1]) + "] are not tank 0", e1);
            (int beyond, _, string e2) = Check(RestepFixtures.RequestJson(relative, hash, pool, tanks[1], tank: 2));
            Assert.Equal(2, beyond);
            Assert.Contains("request's tank is 2", e2);
            Assert.Contains("2 tank(s)", e2);
        }

        // ------------------------------------------------------------------ a failed load leaves nothing half-set

        [Fact]
        public void AFailedLoadClearsThePlanThatWasLoadedBeforeAndASwimOfItRefuses()
        {
            // Nothing is stepped: a swim of a plan that is not the one loaded last refuses before it reaches the episode.
            string pool = Pool();
            RestepRequest good = RestepRequest.Parse(RestepFixtures.PilotRequest(pool));
            RestepPlan plan = Restep.Load(good, RestepFixtures.Root, _dir);
            Assert.Equal(RestepFixtures.PilotSeed0, plan.EpisodeSeed.ToString());

            RestepRequest bad = RestepRequest.Parse(RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", new string('0', 64), pool, RestepFixtures.PilotTank0));
            Assert.Throws<InvalidDataException>(() => Restep.Load(bad, RestepFixtures.Root, _dir));
            var refused = Assert.Throws<InvalidOperationException>(() => Restep.Swim(plan, null));
            Assert.Contains("not the one loaded last", refused.Message);

            // A load that fails on the run's numbers, after the recipe and the pool have been read, clears it as well.
            RestepPlan again = Restep.Load(good, RestepFixtures.Root, _dir);
            RestepRequest wrongTank = RestepRequest.Parse(RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", RestepFixtures.RecipeHashOfBrain2(), pool, RestepFixtures.PilotTank0.Reverse().ToArray()));
            Assert.Throws<InvalidDataException>(() => Restep.Load(wrongTank, RestepFixtures.Root, _dir));
            Assert.Throws<InvalidOperationException>(() => Restep.Swim(again, null));
        }

        [Fact]
        public void ADryPlanLeavesNothingToSwimAndClearsThePlanThatWasLoadedBefore()
        {
            string pool = Pool();
            RestepRequest good = RestepRequest.Parse(RestepFixtures.PilotRequest(pool));
            RestepPlan loaded = Restep.Load(good, RestepFixtures.Root, _dir);
            RestepPlan dry = Restep.Plan(good, RestepFixtures.Root);
            Assert.Equal(loaded.EpisodeSeed, dry.EpisodeSeed);
            Assert.Throws<InvalidOperationException>(() => Restep.Swim(loaded, null));
            Assert.Throws<InvalidOperationException>(() => Restep.Swim(dry, null));
        }

        [Fact]
        public void ALoadWithoutAnOutFolderIsRefused()
        {
            RestepRequest good = RestepRequest.Parse(RestepFixtures.PilotRequest(Pool()));
            foreach (string none in new[] { null, "", "  " })
            {
                var e = Assert.Throws<InvalidDataException>(() => Restep.Load(good, RestepFixtures.Root, none));
                Assert.Contains("out folder", e.Message);
            }
        }

        // ------------------------------------------------------------------ a path through a link

        [Fact]
        public void APathThatPassesThroughAReparsePointIsRefused()
        {
            string pool = Pool(), hash = RestepFixtures.RecipeHashOfBrain2();
            string real = Path.Combine(_dir, "real");
            Directory.CreateDirectory(real);
            File.Copy(Path.Combine(_dir, "pool.tsv"), Path.Combine(real, "pool.tsv"));
            string via = Path.Combine(_dir, "via"), viaRecipes = Path.Combine(_dir, "via-recipes");
            RestepFixtures.MakeFolderLink(via, real);
            try
            {
                RestepFixtures.MakeFolderLink(viaRecipes, Path.Combine(RestepFixtures.Root, "rounds", "nursery"));
                try
                {
                    // The same file by its real path is taken, so that the refusal below is the link's and not the file's.
                    (int ok, _, string e0) = Check(RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", hash, RestepFixtures.Relative(Path.Combine(real, "pool.tsv")), RestepFixtures.PilotTank0));
                    Assert.True(ok == 0, e0);

                    (int viaPool, _, string e1) = Check(RestepFixtures.RequestJson("rounds/nursery/brain2-w8.json", hash, RestepFixtures.Relative(Path.Combine(via, "pool.tsv")), RestepFixtures.PilotTank0));
                    Assert.Equal(2, viaPool);
                    Assert.Contains("pool", e1);
                    Assert.Contains("passes through via, which is a reparse point", e1);

                    (int viaRecipe, _, string e2) = Check(RestepFixtures.RequestJson(RestepFixtures.Relative(Path.Combine(viaRecipes, "brain2-w8.json")), hash, pool, RestepFixtures.PilotTank0));
                    Assert.Equal(2, viaRecipe);
                    Assert.Contains("recipe", e2);
                    Assert.Contains("passes through via-recipes, which is a reparse point", e2);
                }
                finally { RestepFixtures.RemoveFolderLink(viaRecipes); }
            }
            finally { RestepFixtures.RemoveFolderLink(via); }
            Assert.True(File.Exists(RestepFixtures.Brain2), "removing the links left the folder they pointed to");
        }

        // ------------------------------------------------------------------ result.json is flushed and moved into place

        [Fact]
        public void AResultIsPublishedWholeAndLeavesNoTemporaryFileBehind()
        {
            string dir = Path.Combine(_dir, "publish");
            Directory.CreateDirectory(dir);
            RestepFiles.PublishResult(dir, "{\"complete\": true}\n");
            Assert.Equal(new[] { Path.Combine(dir, "result.json") }, Directory.GetFileSystemEntries(dir));
            Assert.Equal("{\"complete\": true}\n", File.ReadAllText(Path.Combine(dir, "result.json")));
        }

        [Fact]
        public void AResultThatAppearedWhileThisOneWasBeingMadeIsNeverWrittenOverAndTheReportSaysSo()
        {
            string dir = Path.Combine(_dir, "raced");
            Directory.CreateDirectory(dir);
            string other = Path.Combine(dir, "result.json");
            File.WriteAllText(other, "{\"complete\": true, \"from\": \"the other run\"}");

            var e = Assert.Throws<ResultAlreadyThereException>(() => RestepFiles.PublishResult(dir, "{\"complete\": true, \"from\": \"this run\"}"));
            Assert.Contains("result.json appeared in", e.Message);
            Assert.Contains("while this episode was being stepped", e.Message);
            Assert.Contains("it is left as it is", e.Message);

            // The other run's file is as it was, and this run's own result is kept in the temporary file the message names.
            Assert.Equal("{\"complete\": true, \"from\": \"the other run\"}", File.ReadAllText(other));
            string[] temps = Directory.GetFiles(dir, "result.json.*.tmp");
            Assert.Single(temps);
            Assert.Contains(Path.GetFileName(temps[0]), e.Message);
            Assert.Equal("{\"complete\": true, \"from\": \"this run\"}", File.ReadAllText(temps[0]));
        }

        // ------------------------------------------------------------------ the island scan

        [Fact]
        public void TheIslandScanIsReadOnAWorldOfSeveralPatchesAndFindsTheIslandsOfThePilotsSnow()
        {
            // The world as TankEpisode builds it up to the carrion's placement, from the pilot's recipe and the pilot's draw-0 seed, and never stepped:
            // the snow is laid (the spread's draw first, then the islands), and the scan reads it.
            Recipe recipe = Recipe.Load(RestepFixtures.Brain2);
            RunConfig config = recipe.EpisodeConfig(File.ReadAllText(recipe.ConfigFullPath));
            Assert.True(config.HorizontalPatches > 1f, "the pilot's world has several patches, which is why an earlier scan skipped it");
            Assert.NotNull(recipe.Snow);
            Assert.Equal(0, recipe.Snow.Layers);            // islands, not layers: the snow runs the whole column to the bed
            Assert.True(recipe.Snow.IslandWavelength > 0f);

            ulong seed = ulong.Parse(RestepFixtures.PilotSeed0);
            var world = new World(config, seed);
            Assert.True(world.TankRadiusMetres > 0f);
            var rng = new Rng(Rng.SeedFor(seed, 0x74616E6BUL));
            double density = recipe.Snow.Density;
            float spread = rng.NextFloat();
            if (!recipe.Snow.Fixed) density *= Math.Pow(2d, 2d * spread - 1d);
            world.LaySnowIslands((float)density, Rng.SeedFor(seed, 0x736E6F77UL), recipe.Snow.IslandWavelength, recipe.Snow.IslandCover);

            IslandScan Scan(int patch) => IslandScan.Run(world.TankRadiusMetres, config.WorldDepthMetres, (int)config.HorizontalPatches,
                (x, y, z) => world.Nutrients.EdibleDensityAt(new FieldPoint(new Float3((float)x, (float)y, (float)z), patch)));
            IslandScan scan = Scan(0);
            _out.WriteLine(scan.Rule);
            Assert.True(scan.Read, scan.Rule);
            Assert.True(scan.Richest > 0d);
            Assert.Equal(0.25 * scan.Richest, scan.EnterAt, 12);
            Assert.Equal(0.10 * scan.Richest, scan.ExitAt, 12);
            Assert.Equal((int)config.HorizontalPatches, scan.Patches);
            Assert.Contains(((int)config.HorizontalPatches).ToString() + " patch(es)", scan.Rule);
            // Some islands, and not all of the bed: the cover is 0.1 of the columns, so a share of the scan's points above the quarter, far from 0 and from 1.
            Assert.InRange(scan.PointsInIsland, 1, scan.Points - 1);
            double share = scan.PointsInIsland / (double)scan.Points;
            Assert.InRange(share, 0.02, 0.4);

            // The grid reads by position and ignores the patch: one scan serves every patch.
            IslandScan other = Scan((int)config.HorizontalPatches - 1);
            Assert.Equal(scan.Richest, other.Richest);
            Assert.Equal(scan.PointsInIsland, other.PointsInIsland);
        }

        [Fact]
        public void TheIslandScanReadsAWorldOfSeveralPatchesAndSaysWhyItReadsNothingWhereItCannot()
        {
            // A snow of one side of the glass: read, with its thresholds, at any number of patches.
            IslandScan half = IslandScan.Run(20d, 25d, 4, (x, y, z) => x < 20d ? 8d : 0d);
            Assert.True(half.Read);
            Assert.Equal(8d, half.Richest);
            Assert.Equal(2d, half.EnterAt);
            Assert.Equal(0.8, half.ExitAt, 12);
            Assert.InRange(half.PointsInIsland, 1, half.Points - 1);
            Assert.Contains("4 patch(es)", half.Rule);

            // The scan is at the lowest layer, a half metre above the bed, wherever the depth is.
            double seen = double.NaN;
            IslandScan.Run(20d, 25d, 1, (x, y, z) => { seen = y; return 1d; });
            Assert.Equal(-24.5, seen);

            IslandScan none = IslandScan.Run(20d, 25d, 1, (x, y, z) => 0d);
            Assert.False(none.Read);
            Assert.Contains("found no snow", none.Rule);
            IslandScan flat = IslandScan.Run(0d, 25d, 1, (x, y, z) => 1d);
            Assert.False(flat.Read);
            Assert.Contains("no round glass", flat.Rule);
        }

        // ------------------------------------------------------------------ what the recorder writes where there is nothing to read

        [Fact]
        public void TheRecorderWritesNaNForADeadBodyAndForASenseOnALostLinkAsItsRemarksSay()
        {
            // The recorder is driven by a probe only an episode can make, so this reads the source: each channel that its remarks name as NaN is written behind a test.
            string code = CodeOf("FrameRecorder.cs");
            foreach (string line in new[]
            {
                "Put(ref written, alive ? (float)probe.Reserve(i) : float.NaN);",
                "Put(ref written, alive ? (float)probe.Tissue(i) : float.NaN);",
                "Put(ref written, alive ? (float)probe.Gestation(i) : float.NaN);",
                "if (alive && probe.TryLinkPose(i, j, out LinkPose p))",
                "Put(ref written, alive ? probe.Drive(i, d) : float.NaN);",
                "Put(ref written, alive ? probe.Bend(i, d) : float.NaN);",
                "if (alive && probe.TryBrainUnits(",
                "int linksNow = alive ? probe.LinkCount(i) : 0;",
                "m.Senses[s].Part < linksNow ? probe.Sense(i, m.Senses[s]) : float.NaN",
            })
                Assert.Contains(line, code);
        }

        // ------------------------------------------------------------------ the S1 trial recipe: retired on main; it runs at the old commit

        // Two tests checked that S1's trial recipe (scratch/nursery-trials/s1-pilot-exam.json, a bench of the pilot's pool on rounds/nursery/brain2.json)
        // loads on the pilot's config, whose episode hash is the 87d66e98fed542c2 pilot-deep-brain2's log printed. S1 restates the pilot's draws bit for
        // bit, which only a build that loads the pilot's world can make: this build refuses configs/7319d6b528cc77f1.json, which lacks the season and
        // floor-pool-share tunables. Both tests run at nursery-room e824ed4, or on any later build that still loads that config.

        // ------------------------------------------------------------------ the probe offers only reads

        /// <summary>Everything the probe may offer. A new member must be added here on purpose, after asking whether it reads.</summary>
        private static readonly string[] ProbeMembers =
        {
            "AdultPartCount", "Alive", "Bend", "BreedingFunds", "BreedingGate", "CorpseCount", "CorpsesEmptied", "Deaths", "Drive", "EconomySeconds",
            "Gestation", "HorizontalPatches", "JoulesPerUnit", "Landed", "LinkCount", "Ledger", "MemberCount", "OutputCount", "PhysicsDt", "PhysicsSeconds",
            "PlacedCorpse", "PlacedCorpseCount", "Reserve", "Sense", "SenseSpecs", "SnowAt", "SnowAtBody", "SolverDigest", "Steps", "StepsPerMetabolicStep",
            "TankRadiusMetres", "Tissue", "TryBrainUnits", "TryLeafScent", "TryLinkPose", "UnitsEaten", "WorldDepthMetres",
            // The rest of the water, for the leaf tank: counts and copies.
            "LivingCount", "Others", "SnowJoules", "SpentUnits",
            // The leaf tank's two measures, read as the episode reads them: the mouth's count and the root's spin, as numbers.
            "MouthJoules", "TryRootSpin",
            // D236's switching tank: its schedule of openings, read-only, and the count of remains under contact eating.
            "RemainsCount", "Schedule",
            // D236 piece 4: what the tank calls eaten (the leaves' joules under contact eating, else the mouth's), as a number. It reads one
            // of two cumulative counters of a body; it hands out no organism and writes nothing.
            "EatenJoules",
            // D241: the adult's tissue, the body fraction and the adult's standing watts, as numbers, to read a body placed grown under the climb.
            "AdultTissue", "BodyFraction", "AdultStandingWatts",
        };

        [Fact]
        public void TheProbeExposesNoMemberThatWritesOrHandsOutTheWorld()
        {
            Type t = typeof(EpisodeProbe);
            BindingFlags all = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            Assert.Empty(t.GetFields(all));                                              // no public field, which could be written
            Assert.Empty(t.GetProperties(all).Where(p => p.CanWrite));                   // no setter
            Assert.Empty(t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)); // only the episode builds one

            string[] names = t.GetMembers(all)
                .Where(m => !(m is MethodInfo mi && mi.IsSpecialName))                   // the accessors of the properties
                .Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(ProbeMembers.OrderBy(n => n, StringComparer.Ordinal).ToArray(), names);

            // Nothing it returns or takes is a handle on the episode's own objects: the world, the solver, a body, a corpse, the brain, or a random stream.
            string[] forbidden = { "Rng", "World", "Simulation", "DynamicsWorld", "Organism", "Creature", "Corpse", "Brain", "CreatureSenses", "IMatterField", "RunConfig" };
            var shown = new List<string>();
            foreach (MethodBase m in t.GetMethods(all).Cast<MethodBase>())
            {
                var types = m.GetParameters().Select(p => p.ParameterType).ToList();
                if (m is MethodInfo info) types.Add(info.ReturnType);
                foreach (Type x in types)
                {
                    Type bare = x.IsByRef ? x.GetElementType() : x;
                    if (bare.IsArray) bare = bare.GetElementType();
                    if (bare.IsGenericType) bare = bare.GetGenericArguments()[0];
                    shown.Add(m.Name + " " + bare.Name);
                    Assert.DoesNotContain(bare.Name, forbidden);
                }
            }
            Assert.NotEmpty(shown);
        }

        private static string CodeOf(string file)
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, "src", "Evosim.Nursery", file);
                if (!File.Exists(path)) continue;
                // The code of the file: each line without its comment, so that a comment which names a forbidden call is not a call.
                return string.Join("\n", File.ReadAllLines(path).Select(l => { int c = l.IndexOf("//", StringComparison.Ordinal); return c < 0 ? l : l.Substring(0, c); }));
            }
            throw new FileNotFoundException("src/Evosim.Nursery/" + file + " was not found above the test program");
        }

        [Theory]
        [InlineData("TankWatcher.cs")]
        [InlineData("FrameRecorder.cs")]
        public void TheProbeAndTheRecorderNeverCallWhatWritesOrDraws(string file)
        {
            string code = CodeOf(file);
            foreach (string never in new[] { ".Sample(", "Refresh", "Commit", ".Step(", "StepBlock", "LinkReach", "Rng", "Random", "PlaceAt", "Resize", "Deposit", "Take(" })
                Assert.DoesNotContain(never, code);
        }

        // ------------------------------------------------------------------ the episode's own statements

        [Fact]
        public void TankEpisodeCallsTheWatcherOnlyBehindANullTestAndSwimTanksGivesItNone()
        {
            string code = CodeOf("Tank.cs");
            string[] lines = code.Split('\n').Select(l => l.Trim()).Where(l => l.Contains("watcher")).ToArray();
            Assert.Equal(new[]
            {
                "private static Resident[] TankEpisode(Genome[] genomes, ulong seed, Arena arena, ITankWatcher watcher = null)",
                // The leaf tank smoke's read of the smell, a watcher like any other, set only when EVOSIM_LEAF_SENSE names its file.
                "if (watcher == null && LeafSenseProbe.Path != null) watcher = new LeafSenseProbe(seed);",
                "if (watcher != null)",
                "watcher.Begin(probe);",
                "if (watcher != null) watcher.Physics(probe);",
                "if (watcher != null) watcher.Metabolic(probe);",
                "if (watcher != null) watcher.End(probe);",
            }, lines);

            // The one swim the nursery makes passes no watcher: its call is the line it always was.
            Assert.Contains("Resident[] r = TankEpisode(members.Select(i => genomes[i]).ToArray(), seeds[d], arena);", code);
            // The probe is built only inside the first null test, from the world, the solver and the bodies: never from the random stream.
            Assert.Contains("probe = new EpisodeProbe(world, sim, focal);", code);
        }
    }
}

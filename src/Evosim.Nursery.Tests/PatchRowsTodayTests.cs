using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// A tank whose patches do not switch writes the <c>patches.jsonl</c> rows it wrote before D236's travel measures, byte for byte.
    /// <para>
    /// <b>This file uses only what the build before piece 4 had</b> (commit 48c24edd), so that it can be copied alone into a checkout of that
    /// build to record the fixture it compares against. To record: set <c>EVOSIM_P4_RECORD</c> to a file path and run this class's one test at
    /// that build; it writes the rows it compares to that path and passes. Commit that file as <c>fixtures/p4-patch-rows-today.txt</c>.
    /// Without the variable the test compares this build's rows to the committed file.
    /// </para>
    /// </summary>
    [Collection("restep")]
    public class PatchRowsTodayTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("patch-rows-today");

        public PatchRowsTodayTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        [Fact]
        public void ANonSwitchingPatchEpisodesRowsAreTheBytesTheyWere()
        {
            // The tank of PatchWatcherTests' patchy episode: 1,600 m2, three patches of four leaves, the smell short-ranged, three members.
            RunConfig config = LeafTankFixtures.Config();
            config.WorldAreaSquareMetres = 1600f;
            config.LeafScent = LeafScentForm.Direction;
            config.LeafScentRangeMetres = 2f;
            (string cfg, string hash) = LeafTankFixtures.WriteConfig(_dir, config);
            string pool = LeafTankFixtures.WriteBredPool(_dir, 3);
            string leaves = "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 12, \"spentUnitsPerCubicMetre\": 0, " +
                            "\"patches\": {\"count\": 3, \"radius\": 4, \"ring\": 14}}";
            string recipePath = LeafTankFixtures.Write(_dir, "patched.json", LeafTankFixtures.RecipeJson(cfg, hash, pool, 3, leaves, seconds: 8));
            Recipe recipe = Recipe.Load(recipePath);

            var got = new List<string>();
            for (int e = 0; e < 2; e++)
            {
                string request = RestepFixtures.WriteRequest(_dir,
                    RestepFixtures.RequestJson(RestepFixtures.Relative(recipePath), recipe.Hash(), RestepFixtures.Relative(recipe.PoolFullPath),
                        RestepFixtures.Deal(3, 3, e, 0), episode: e), "request-" + e + ".json");
                string outDir = Path.Combine(_dir, "out-" + e);
                Directory.CreateDirectory(outDir);
                (int code, string stdout, string stderr) = RestepFixtures.Run("--restep", request, "--out", outDir, "--patches");
                Assert.True(code == 0, stdout + stderr);
                got.Add("episode " + e);
                got.AddRange(File.ReadAllLines(Path.Combine(outDir, "patches.jsonl")).Where(l => l.Length > 0));
            }
            foreach (string line in got) _out.WriteLine(line);
            Assert.True(got.Count > 2, "no row was written");

            string record = Environment.GetEnvironmentVariable("EVOSIM_P4_RECORD");
            if (!string.IsNullOrEmpty(record))
            {
                File.WriteAllLines(record, got);
                _out.WriteLine("recorded " + got.Count + " lines to " + record);
                return;
            }

            string fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "p4-patch-rows-today.txt");
            Assert.True(File.Exists(fixture),
                "fixtures/p4-patch-rows-today.txt is not recorded: run this test at the build before piece 4 (48c24edd) with EVOSIM_P4_RECORD set to a file path, and commit that file there");
            Assert.Equal(File.ReadAllLines(fixture), got);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Evosim.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// D182's patch measures (PatchWatcher.cs): the counting rules of <see cref="PatchTrack"/> on tracks written by hand, the leaves' grouping
    /// into patches, and the watcher on a re-stepped leaf tank, which must leave every net as it was.
    /// </summary>
    [Collection("restep")]
    public class PatchWatcherTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("patch-watch");

        public PatchWatcherTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        private static PatchTrack Track(double window, params (double t, int patch)[] samples)
        {
            var k = new PatchTrack(window);
            foreach ((double t, int patch) in samples) k.Observe(t, true, patch);
            return k;
        }

        [Fact]
        public void ATrackCountsTurnBacksTransitsLateReturnsAndTheUnended()
        {
            PatchTrack k = Track(30,
                (0, -1), (10, 0), (20, -1), (30, -1), (40, 0), (50, -1), (100, 1), (110, -1), (150, -1), (200, 1), (210, -1));

            Assert.False(k.StartedInScent);
            Assert.Equal(10, k.FirstScentSeconds);
            Assert.Equal(4, k.Exits);
            Assert.Equal(1, k.TurnBacks);      // out of patch 0 at 20, back in at 40
            Assert.Equal(1, k.Transits);       // out of 0 at 50, into 1 at 100
            Assert.Equal(1, k.LateReturns);    // out of 1 at 110, back at 200
            Assert.Equal(1, k.Unended);        // out of 1 at 210, and the track ends
            Assert.Equal(new[] { (100d, 0, 1) }, k.TransitLog);
            Assert.Equal(2, k.Entries[0]);
            Assert.Equal(2, k.Entries[1]);
            Assert.Equal(210, k.AliveSeconds);
            Assert.Equal(40, k.ScentSeconds);
            Assert.Equal(20, k.PatchSeconds[0]);
            Assert.Equal(20, k.PatchSeconds[1]);
            Assert.False(k.Died);
        }

        [Fact]
        public void TheWindowHoldsItsOwnEdge()
        {
            Assert.Equal(1, Track(30, (0, 0), (10, -1), (40, 0)).TurnBacks);
            Assert.Equal(1, Track(30, (0, 0), (10, -1), (40.5, 0)).LateReturns);
        }

        [Fact]
        public void AStepFromOnePatchStraightIntoAnotherIsAnExitAndATransit()
        {
            PatchTrack k = Track(30, (0, 0), (1, 1));
            Assert.True(k.StartedInScent);
            Assert.True(double.IsNaN(k.FirstScentSeconds));
            Assert.Equal(1, k.Exits);
            Assert.Equal(1, k.Transits);
            Assert.Equal(0, k.Unended);
            Assert.Equal(1, k.Entries[0]);
            Assert.Equal(1, k.Entries[1]);
        }

        [Fact]
        public void ADeathEndsTheCountAndLeavesItsExitUnended()
        {
            var k = new PatchTrack(30);
            k.Observe(0, true, 0);
            k.Observe(5, true, -1);
            k.Observe(10, false, -1);
            k.Observe(15, false, -1);
            Assert.True(k.Died);
            Assert.Equal(10, k.AliveSeconds);
            Assert.Equal(5, k.ScentSeconds);
            Assert.Equal(1, k.Unended);
            Assert.Throws<ArgumentException>(() => k.Observe(20, true, 0));
        }

        [Fact]
        public void ABodyThatNeverSmellsALeafHasNoFirstSmellAndAllItsTimeBare()
        {
            PatchTrack k = Track(30, (0, -1), (100, -1), (200, -1));
            Assert.False(k.StartedInScent);
            Assert.True(double.IsNaN(k.FirstScentSeconds));
            Assert.Equal(200, k.AliveSeconds);
            Assert.Equal(0, k.ScentSeconds);
            Assert.Equal(0, k.Exits);
        }

        [Fact]
        public void SamplesGoForwardAndTheWindowIsPositive()
        {
            var k = new PatchTrack(30);
            k.Observe(10, true, -1);
            Assert.Throws<ArgumentException>(() => k.Observe(5, true, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PatchTrack(0));
        }

        [Fact]
        public void LeavesCloserThanTheLinkShareAPatch()
        {
            var groups = PatchGroups.Of(new[] { (1L, 0d, 0d), (2L, 30d, 0d), (3L, 5d, 0d), (4L, 34d, 0d), (5L, 100d, 100d), (6L, 41d, 0d) }, 12);
            Assert.Equal(new[] { 0, 1, 0, 1, 2, 1 }, new long[] { 1, 2, 3, 4, 5, 6 }.Select(id => groups[id]));
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void TheWatcherReadsAPatchyTankAndChangesNoNet()
        {
            // A tank of 1,600 m² (radius 22.6 m) whose patches stand apart as the arm's do: centres 24.2 m apart, so at least 16 m
            // between leaves of different patches, more than the watcher's 12 m link, and at most 8 m across a patch.
            RunConfig config = LeafTankFixtures.Config();
            config.WorldAreaSquareMetres = 1600f;
            config.LeafScent = LeafScentForm.Direction;
            config.LeafScentRangeMetres = 2f;
            (string cfg, string hash) = LeafTankFixtures.WriteConfig(_dir, config);
            string pool = LeafTankFixtures.WriteBredPool(_dir, 3);
            string leaves = "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 12, \"spentUnitsPerCubicMetre\": 0, " +
                            "\"patches\": {\"count\": 3, \"radius\": 4, \"ring\": 14}}";
            string recipe = LeafTankFixtures.Write(_dir, "patched.json", LeafTankFixtures.RecipeJson(cfg, hash, pool, 3, leaves, seconds: 10));

            var watcher = new PatchWatcher();
            RestepOutcome watched = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, watcher);
            RestepOutcome bare = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, null);

            Assert.Equal(bare.Members.Select(m => m.Net), watched.Members.Select(m => m.Net));
            Assert.True(watcher.HasScent);
            Assert.Equal(12, watcher.LeafCount);
            Assert.Equal(3, watcher.PatchCount);
            for (int m = 0; m < watcher.Tracks.Length; m++)
            {
                PatchTrack k = watcher.Tracks[m];
                _out.WriteLine($"member {m}: started in scent {k.StartedInScent}, alive {k.AliveSeconds} s, scent {k.ScentSeconds} s, exits {k.Exits}");
                if (!watched.Members[m].Landed) continue;
                Assert.NotNull(k.StartedInScent);
                // A survivor is counted from its first sample to the end; a body that died, up to its death.
                Assert.Equal(!watched.Members[m].Survived, k.Died);
                if (watched.Members[m].Survived) Assert.InRange(k.AliveSeconds, 9, 10.01);
                else Assert.InRange(k.AliveSeconds, 0, 10);
                Assert.InRange(k.ScentSeconds, 0, k.AliveSeconds);
            }
        }
    }
}

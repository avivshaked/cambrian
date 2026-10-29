using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Evosim.Core;
using Evosim.Nursery;
using Evosim.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// D236's travel measures (PatchTravel.cs, and the watcher's switching path in PatchWatcher.cs): the counting rules of <see cref="TravelTrack"/>
    /// on samples written by hand, then a short switching episode re-stepped under the watcher, which must leave every net as it was and write
    /// the new columns. That a tank whose patches do not switch writes the rows it always did is <c>PatchRowsTodayTests</c>.
    /// </summary>
    [Collection("restep")]
    public class PatchTravelTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("patch-travel");

        public PatchTravelTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        /// <summary>Each opening's time by id: 0 and 1 at the start, then one at 600 s and one at 1,200 s.</summary>
        private static readonly double[] Opened = { 0d, 0d, 600d, 1200d };

        // ------------------------------------------------------------------ the time from a switch to arrival

        [Fact]
        public void ASwitchGivesTheSecondsToTheFirstSampleInTheNewOpeningsSmell()
        {
            var k = new TravelTrack(0d);
            k.Observe(0, true, 0, 0);
            k.Observe(10, true, 1, 0);
            k.Switched(1, 20d, 2, true);
            k.Observe(20, true, 1, 0);
            k.Observe(30, true, -1, 0);
            k.Observe(35, true, 1, 0);      // the old opening again, not the new one
            k.Observe(45, true, 2, 0);      // the new opening, 25 s after the switch
            k.Observe(50, true, 2, 0);      // staying in it moves nothing

            TravelTrack.Switch s = Assert.Single(k.Switches);
            Assert.Equal(1, s.Index);
            Assert.Equal(2, s.Opening);
            Assert.Equal(20d, s.SwitchSeconds);
            Assert.True(s.AliveAtSwitch);
            Assert.True(s.Arrived);
            Assert.Equal(25d, s.ArrivalSeconds);
            Assert.Equal(25d, s.FollowSeconds);
            Assert.Equal(1, k.AliveAtSwitches);
        }

        [Fact]
        public void ABodyThatNeverArrivesHasNoArrivalAndKeepsItsFollowUpToItsLastAliveSample()
        {
            var k = new TravelTrack(0d);
            k.Observe(0, true, 0, 0);
            k.Switched(1, 20d, 2, true);
            k.Observe(20, true, 0, 0);
            k.Observe(30, true, -1, 0);
            k.Observe(40, true, 0, 0);
            k.Observe(50, false, -1, 0);     // dies before it arrives
            k.Observe(60, false, -1, 0);

            TravelTrack.Switch s = Assert.Single(k.Switches);
            Assert.True(s.AliveAtSwitch);
            Assert.False(s.Arrived);
            Assert.True(double.IsNaN(s.ArrivalSeconds));
            Assert.Equal(20d, s.FollowSeconds);      // 40 s, its last alive sample, less the switch at 20 s
            Assert.Equal(1, k.AliveAtSwitches);
        }

        [Fact]
        public void ABodyDeadAtTheSwitchHasNothingForIt()
        {
            var k = new TravelTrack(0d);
            k.Observe(0, true, 0, 0);
            k.Observe(5, false, -1, 0);
            k.Switched(1, 10d, 2, false);
            k.Observe(10, false, -1, 0);
            k.Observe(20, false, 2, 0);       // a dead body is nowhere

            TravelTrack.Switch s = Assert.Single(k.Switches);
            Assert.False(s.AliveAtSwitch);
            Assert.True(double.IsNaN(s.ArrivalSeconds));
            Assert.True(double.IsNaN(s.FollowSeconds));
            Assert.Equal(0, k.AliveAtSwitches);
        }

        [Fact]
        public void ABodyAlreadyInTheNewOpeningsSmellAtTheSampleThatSeesTheSwitchArrivesAtZero()
        {
            var k = new TravelTrack(0d);
            k.Observe(0, true, 0, 0);
            k.Switched(1, 20d, 2, true);
            k.Observe(20, true, 2, 0);
            Assert.Equal(0d, k.Switches[0].ArrivalSeconds);
        }

        [Fact]
        public void EachSwitchIsKeptApartAndALaterOneDoesNotHideAnEarlierWait()
        {
            var k = new TravelTrack(0d);
            k.Observe(0, true, 0, 0);
            k.Switched(1, 20d, 2, true);
            k.Observe(20, true, 0, 0);
            k.Switched(2, 40d, 3, true);
            k.Observe(40, true, 0, 0);
            k.Observe(45, true, 3, 0);        // arrives at the second switch's opening, 5 s on; the first is still waited for
            k.Observe(50, true, -1, 0);
            k.Observe(60, true, 2, 0);        // and at the first's, 40 s on

            Assert.Equal(new[] { 40d, 5d }, k.Switches.Select(s => s.ArrivalSeconds));
            Assert.Equal(new[] { 40d, 5d }, k.Switches.Select(s => s.FollowSeconds));
            Assert.Equal(new[] { 1, 2 }, k.Switches.Select(s => s.Index));
            Assert.Equal(2, k.AliveAtSwitches);
        }

        [Fact]
        public void AnOpeningIsAPatchOfItsOwnSoSmellingTheOldOneIsNotArriving()
        {
            var k = new TravelTrack(0d);
            k.Switched(1, 10d, 2, true);
            k.Observe(10, true, 0, 0);
            k.Observe(20, true, 1, 0);
            Assert.False(k.Switches[0].Arrived);
            Assert.Equal(10d, k.Switches[0].FollowSeconds);
        }

        [Fact]
        public void TheFirstPatchIsTheFirstOpeningSmeltByAnAliveBody()
        {
            var k = new TravelTrack(0d);
            k.Observe(0, true, -1, 0);
            Assert.Equal(-1, k.FirstPatch);
            k.Observe(1, true, 1, 0);
            k.Observe(2, true, 0, 0);
            k.Observe(3, false, 2, 0);
            Assert.Equal(1, k.FirstPatch);
        }

        // ------------------------------------------------------------------ eating by opening

        [Fact]
        public void EachStepsGainIsCreditedToTheOpeningTheBodyIsInOrToNone()
        {
            var k = new TravelTrack(5d);
            k.Observe(0, true, 0, 5);          // nothing eaten
            k.Observe(1, true, 0, 8);          // +3 in opening 0
            k.Observe(2, true, -1, 9);         // +1 in no smell
            k.Observe(3, true, 2, 9);          // nothing
            k.Observe(4, true, 2, 12);         // +3 in opening 2
            k.Observe(5, true, 2, 10);         // a fall is not credited, and the count carries on from the lower value
            k.Observe(6, true, 2, 11);         // +1 in opening 2
            k.Observe(7, true, 2, double.NaN); // no reading, no credit
            k.Observe(8, false, -1, 13);       // +2 at the sample it is found dead, to the opening of its last alive sample

            Assert.Equal(new[] { 0, 2 }, k.EatenByOpening.Keys.OrderBy(x => x));
            Assert.Equal(3d, k.EatenByOpening[0]);
            Assert.Equal(6d, k.EatenByOpening[2]);
            Assert.Equal(1d, k.EatenNone);
            Assert.Equal(10d, k.EatenTotal);
        }

        [Fact]
        public void TheFirstSamplesGainCountsFromTheBaselineGivenAtTheStart()
        {
            var counted = new TravelTrack(5d);
            counted.Observe(0, true, 0, 7);
            Assert.Equal(2d, counted.EatenByOpening[0]);

            var unknown = new TravelTrack();   // no baseline: the first reading is it
            unknown.Observe(0, true, 0, 100);
            Assert.Empty(unknown.EatenByOpening);
            unknown.Observe(1, true, 0, 104);
            Assert.Equal(4d, unknown.EatenByOpening[0]);
        }

        // ------------------------------------------------------------------ the share eaten in later openings

        [Fact]
        public void TheLaterShareIsWhatWasEatenInOpeningsOpenedAfterTheFirstPatchOverAllItAte()
        {
            var k = new TravelTrack(0d);
            k.Observe(0, true, 1, 0);          // the first patch is opening 1, opened at 0 s
            k.Observe(1, true, 1, 2);          // 2 J in opening 1
            k.Observe(2, true, 0, 3);          // 1 J in opening 0, which opened at the same time: not later
            k.Observe(3, true, -1, 5);         // 2 J in no smell: in the total
            k.Observe(4, true, 2, 8);          // 3 J in opening 2, opened at 600 s
            k.Observe(5, true, 3, 12);         // 4 J in opening 3, opened at 1,200 s
            Assert.Equal(7d / 12d, k.LaterShare(Opened), 12);

            // The same eating, but the body's first patch was opening 2: only opening 3 opened after it.
            var j = new TravelTrack(0d);
            j.Observe(0, true, 2, 0);
            j.Observe(1, true, 2, 1);
            j.Observe(2, true, 3, 4);
            Assert.Equal(3d / 4d, j.LaterShare(Opened), 12);
        }

        [Fact]
        public void TheLaterShareIsNothingForABodyThatNeverSmeltAPatchOrAteNothing()
        {
            var bare = new TravelTrack(0d);
            bare.Observe(0, true, -1, 0);
            bare.Observe(1, true, -1, 3);       // ate, but never in a smell: no first patch
            Assert.True(double.IsNaN(bare.LaterShare(Opened)));

            var fasting = new TravelTrack(0d);
            fasting.Observe(0, true, 1, 0);
            fasting.Observe(1, true, 2, 0);
            Assert.True(double.IsNaN(fasting.LaterShare(Opened)));
        }

        [Fact]
        public void TheLaterShareRefusesOpeningTimesThatDoNotCoverTheOpeningsCredited()
        {
            var k = new TravelTrack(0d);
            k.Observe(0, true, 1, 0);
            k.Observe(1, true, 3, 2);
            Assert.Throws<ArgumentException>(() => k.LaterShare(new[] { 0d }));
            Assert.Throws<ArgumentException>(() => k.LaterShare(new[] { 0d, 0d, 600d }));
        }

        // ------------------------------------------------------------------ a short switching episode, re-stepped

        private string SwitchingRecipe(double seconds)
        {
            RunConfig config = LeafTankFixtures.Config();
            config.LeafScent = LeafScentForm.Direction;
            (string cfg, string hash) = LeafTankFixtures.WriteConfig(_dir, config);
            string pool = LeafTankFixtures.WriteBredPool(_dir, 3);
            string leaves = "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 6, \"spentUnitsPerCubicMetre\": 0, " +
                            "\"patches\": {\"count\": 2, \"radius\": 4.5, \"ring\": 6.5, " +
                            "\"switching\": {\"period\": 3, \"regrowAfter\": 1, \"witherOver\": 1.5, \"minimumTurn\": 120}}}";
            return LeafTankFixtures.Write(_dir, "switching.json", LeafTankFixtures.RecipeJson(cfg, hash, pool, 3, leaves, seconds));
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void TheWatcherReadsASwitchingEpisodeAndChangesNoNetToTheBit()
        {
            string recipe = SwitchingRecipe(8);   // two switches, at 3 s and 6 s: four openings

            var watcher = new PatchWatcher();
            RestepOutcome watched = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, watcher);
            RestepOutcome bare = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, null);

            Assert.Equal(bare.Members.Select(m => LeafTankFixtures.Line(m)), watched.Members.Select(m => LeafTankFixtures.Line(m)));
            Assert.Equal(bare.Members.Select(m => BitConverter.DoubleToInt64Bits(m.Eaten)), watched.Members.Select(m => BitConverter.DoubleToInt64Bits(m.Eaten)));

            Assert.True(watcher.HasScent);
            Assert.Equal(4, watcher.PatchCount);
            Assert.Equal(new[] { 0d, 0d, 3d, 6d }, watcher.OpenedAt);
            Assert.NotNull(watcher.Travel);
            Assert.Equal(watcher.Tracks.Length, watcher.Travel.Length);

            int landed = 0;
            for (int m = 0; m < watcher.Travel.Length; m++)
            {
                if (!watched.Members[m].Landed) continue;
                landed++;
                TravelTrack k = watcher.Travel[m];
                Assert.Equal(new[] { 1, 2 }, k.Switches.Select(s => s.Index));
                Assert.Equal(new[] { 2, 3 }, k.Switches.Select(s => s.Opening));
                Assert.Equal(new[] { 3d, 6d }, k.Switches.Select(s => s.SwitchSeconds));
                foreach (TravelTrack.Switch s in k.Switches)
                {
                    if (!s.AliveAtSwitch)
                    {
                        Assert.True(double.IsNaN(s.ArrivalSeconds));
                        Assert.True(double.IsNaN(s.FollowSeconds));
                        continue;
                    }
                    Assert.InRange(s.FollowSeconds, 0d, 8d);
                    if (s.Arrived) Assert.Equal(s.ArrivalSeconds, s.FollowSeconds);
                }
                Assert.Equal(k.Switches.Count(s => s.AliveAtSwitch), k.AliveAtSwitches);
                double later = k.LaterShare(watcher.OpenedAt);
                Assert.True(double.IsNaN(later) || (later >= 0d && later <= 1d), "a share outside 0 to 1: " + later);
                // What the tank calls eaten is what the openings and the smell-less water were credited, to rounding.
                Assert.Equal(watched.Members[m].Eaten, k.EatenTotal, 6);
                _out.WriteLine(string.Format(CultureInfo.InvariantCulture, "member {0}: arrivals {1}, follow {2}, first patch {3}, later share {4}, eaten {5} J",
                    m, string.Join(" ", k.Switches.Select(s => s.ArrivalSeconds)), string.Join(" ", k.Switches.Select(s => s.FollowSeconds)),
                    k.FirstPatch, later, k.EatenTotal));
            }
            Assert.True(landed > 0, "no member landed");
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void APatchesRunOfASwitchingEpisodeWritesTheTravelColumnsAfterTodaysAndEverySwitchOnce()
        {
            string recipe = SwitchingRecipe(8);
            Recipe r = Recipe.Load(recipe);
            int[] members = RestepFixtures.Deal(3, 3, 0, 0);
            string request = RestepFixtures.WriteRequest(_dir,
                RestepFixtures.RequestJson(RestepFixtures.Relative(recipe), r.Hash(), RestepFixtures.Relative(r.PoolFullPath), members, episode: 0));
            string outDir = Path.Combine(_dir, "patches-out");
            Directory.CreateDirectory(outDir);

            (int code, string stdout, string stderr) = RestepFixtures.Run("--restep", request, "--out", outDir, "--patches");
            Assert.True(code == 0, stdout + stderr);

            string[] lines = File.ReadAllLines(Path.Combine(outDir, "patches.jsonl")).Where(l => l.Length > 0).ToArray();
            Assert.NotEmpty(lines);
            foreach (string line in lines)
            {
                _out.WriteLine(line.Length > 400 ? line.Substring(line.IndexOf("\"switches\"", StringComparison.Ordinal)) : line);
                // After today's last column, in this order.
                int at = line.IndexOf("\"transitLog\"", StringComparison.Ordinal);
                Assert.True(at > 0);
                string[] order = { "\"switches\"", "\"aliveAtSwitch\"", "\"arrivals\"", "\"follow\"", "\"firstPatch\"", "\"laterShare\"", "\"eaten\"", "\"eatenNone\"", "\"eatenByOpening\"" };
                foreach (string name in order)
                {
                    int here = line.IndexOf(name, StringComparison.Ordinal);
                    Assert.True(here > at, name + " is not after transitLog");
                    at = here;
                }

                using (JsonDocument doc = JsonDocument.Parse(line))
                {
                    JsonElement row = doc.RootElement;
                    Assert.Equal(4, row.GetProperty("patches").GetInt32());
                    Assert.Equal(2, row.GetProperty("switches").GetInt32());
                    JsonElement arrivals = row.GetProperty("arrivals"), follow = row.GetProperty("follow");
                    Assert.Equal(2, arrivals.GetArrayLength());
                    Assert.Equal(2, follow.GetArrayLength());
                    int alive = 0;
                    for (int s = 0; s < 2; s++)
                    {
                        bool has = follow[s].ValueKind == JsonValueKind.Number;
                        if (has) alive++;
                        if (arrivals[s].ValueKind == JsonValueKind.Number)
                        {
                            Assert.True(has, "an arrival at a switch the body was dead at");
                            Assert.Equal(follow[s].GetDouble(), arrivals[s].GetDouble());
                        }
                    }
                    Assert.Equal(alive, row.GetProperty("aliveAtSwitch").GetInt32());
                    Assert.Equal(4, row.GetProperty("eatenByOpening").GetArrayLength());
                    double byOpening = row.GetProperty("eatenByOpening").EnumerateArray().Sum(x => x.GetDouble());
                    Assert.Equal(row.GetProperty("eaten").GetDouble(), byOpening + row.GetProperty("eatenNone").GetDouble(), 9);
                    JsonElement later = row.GetProperty("laterShare");
                    if (later.ValueKind == JsonValueKind.Number) Assert.InRange(later.GetDouble(), 0d, 1d);
                }
            }
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void ATankWhosePatchesDoNotSwitchHasNoTravelTracks()
        {
            RunConfig config = LeafTankFixtures.Config();
            config.LeafScent = LeafScentForm.Direction;
            (string cfg, string hash) = LeafTankFixtures.WriteConfig(_dir, config);
            string pool = LeafTankFixtures.WriteBredPool(_dir, 3);
            string leaves = "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 6, \"spentUnitsPerCubicMetre\": 0, " +
                            "\"patches\": {\"count\": 2, \"radius\": 4.5, \"ring\": 6.5}}";
            string recipe = LeafTankFixtures.Write(_dir, "fixed.json", LeafTankFixtures.RecipeJson(cfg, hash, pool, 3, leaves, seconds: 3));

            var watcher = new PatchWatcher();
            LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, watcher);
            Assert.Null(watcher.Travel);
            Assert.Empty(watcher.OpenedAt);
            Assert.True(watcher.PatchCount >= 1);   // the 12 m link may join two discs 13 m apart centre to centre
        }

        // ------------------------------------------------------------------ the probe's EatenJoules

        private static Organism[] Focal(EpisodeProbe probe) =>
            (Organism[])typeof(EpisodeProbe).GetField("_focal", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(probe);

        /// <summary>Reads each member's cumulative eaten through the probe and its two counters off the bodies themselves, which the probe offers no member for.</summary>
        private sealed class EatenReads : ITankWatcher
        {
            public double[] ProbeFirst, ProbeLast, Mouth, Food;
            public bool Feed, Fed;
            public double LeafGain = double.NaN;
            private bool _fed;

            public void Begin(EpisodeProbe probe)
            {
                ProbeFirst = Enumerable.Range(0, probe.MemberCount).Select(probe.EatenJoules).ToArray();
            }

            public void Physics(EpisodeProbe probe) { }

            /// <summary>When <see cref="Feed"/> is set, the first economy step ends with a bite handed to the world by hand: member 0's mouth on a leaf.</summary>
            public void Metabolic(EpisodeProbe probe)
            {
                if (!Feed || _fed) return;
                _fed = true;
                Organism[] f = Focal(probe);
                var world = (World)typeof(EpisodeProbe).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(probe);
                if (!world.Living.Contains(f[0])) return;
                Fed = true;
                Organism leaf = world.Living.First(b => b.HasPhotosyntheticTissue && Array.IndexOf(f, b) < 0);
                double kept = f[0].BiteKeptJoules;
                world.SetContacts(new List<CreatureContact> { new CreatureContact(f[0].Id, 0, leaf.Id, 0) });
                world.ApplyMouth(1f);
                LeafGain = f[0].BiteKeptJoules - kept;
            }

            public void End(EpisodeProbe probe)
            {
                Organism[] f = Focal(probe);
                ProbeLast = Enumerable.Range(0, probe.MemberCount).Select(probe.EatenJoules).ToArray();
                Mouth = f.Select(o => o?.MouthJoules ?? double.NaN).ToArray();
                Food = f.Select(o => o?.FoodFromLeavesJoules ?? double.NaN).ToArray();
            }
        }

        private string EatenRecipe(bool contact)
        {
            RunConfig c = LeafTankFixtures.Config();
            c.ContactEating = contact;
            c.WorldAreaSquareMetres = 100f;
            (string path, string hash) = LeafTankFixtures.WriteConfig(_dir, c);
            Genome swimmer = GenomeJson.Read(File.ReadAllText(Path.Combine(RestepFixtures.Root, LeafTankSwimmerTests.Plate.Replace('/', Path.DirectorySeparatorChar))));
            string pool = LeafTankFixtures.WriteBredPool(_dir, 2, _ => swimmer);
            string leaves = "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": 6, \"spentUnitsPerCubicMetre\": 0}";
            return LeafTankFixtures.Write(_dir, "eaten.json", LeafTankFixtures.RecipeJson(path, hash, pool, 2, leaves, seconds: 6, purseSeconds: 600));
        }

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        // ------------------------------------------------------------------ the read script, on a batch written by hand

        [Fact]
        public void TheReadScriptReadsAHandWrittenBatchByBrainAndByFounderLineage()
        {
            // fixtures/travel-batch is a batch of three brains (own, brainless, frozen) and four pool lines in two lineages, two switches each,
            // written by hand; one tank of it does not switch and is left out. The numbers below are worked by hand from its rows.
            string script = Path.Combine(RestepFixtures.Root, "scripts", "reads", "nursery-travel-read.py");
            string batch = Path.Combine(AppContext.BaseDirectory, "fixtures", "travel-batch");
            Assert.True(File.Exists(script), script);
            Assert.True(Directory.Exists(batch), batch);

            string stdout, stderr;
            int code;
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                info.ArgumentList.Add("-I");
                info.ArgumentList.Add(script);
                info.ArgumentList.Add(batch);
                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(info))
                {
                    stdout = p.StandardOutput.ReadToEnd();
                    stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    code = p.ExitCode;
                }
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                _out.WriteLine("python is not on this machine's path, so the script was not run: " + e.Message);
                return;
            }
            if (code == 9009 || stderr.Contains("was not found"))
            {
                _out.WriteLine("python is not installed here (only its store stub), so the script was not run: " + stderr);
                return;
            }

            _out.WriteLine(stdout);
            Assert.True(code == 0, stderr);
            Assert.Contains("12 switching rows read from 3 episodes, 1 rows of tanks that do not switch left out", stdout);
            Assert.Contains("own: 4 bodies, 2 switches an episode, alive at 7 (body, switch) pairs, 4 arrived (57%)", stdout);
            Assert.Contains("time from a switch to arrival: median 40 s  quartiles 20 s .. -", stdout);
            Assert.Contains("own vs no brain, time to arrival (s, a never-arrived switch at its follow-up: a lower bound): 38.75 vs 113.75, difference -75.00 = -2.85 standard errors", stdout);
            Assert.Contains("own vs frozen, time to arrival (s, a never-arrived switch at its follow-up: a lower bound): 38.75 vs 100.00, difference -61.25 = -5.44 standard errors", stdout);
        }

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void EatenJoulesIsTheLeafFoodUnderContactEatingAndTheMouthsCountWithout()
        {
            // Under contact eating: the joules kept from leaves, which a bite handed to the world by hand makes certain of.
            var on = new EatenReads { Feed = true };
            RestepOutcome o = LeafTankFixtures.Swim(_dir, EatenRecipe(contact: true), 2, 2, 0, on);
            Assert.True(on.Fed, "member 0 was not living at the first economy step, so no bite was made");
            Assert.True(on.LeafGain > 0d, "the hand-made bite on a leaf kept nothing");
            for (int k = 0; k < 2; k++)
            {
                if (!o.Members[k].Landed) continue;
                Assert.Equal(Bits(on.Food[k]), Bits(on.ProbeLast[k]));
                Assert.Equal(Bits(on.ProbeLast[k] - on.ProbeFirst[k]), Bits(o.Members[k].Eaten));
            }
            Assert.True(on.ProbeLast[0] - on.ProbeFirst[0] > 0d);

            // Without it: the mouth's count, as the tank has always read it.
            var off = new EatenReads();
            RestepOutcome p = LeafTankFixtures.Swim(_dir, EatenRecipe(contact: false), 2, 2, 0, off);
            for (int k = 0; k < 2; k++)
            {
                if (!p.Members[k].Landed) continue;
                Assert.Equal(Bits(off.Mouth[k]), Bits(off.ProbeLast[k]));
                Assert.Equal(Bits(off.ProbeLast[k] - off.ProbeFirst[k]), Bits(p.Members[k].Eaten));
            }
        }
    }
}

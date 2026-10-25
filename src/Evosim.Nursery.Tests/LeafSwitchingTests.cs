using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;
using Evosim.Dynamics.Placement;
using Evosim.Nursery;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// D236's switching patches (<c>leaves.patches.switching</c>, LeafSwitching.cs): the recipe field, the plan, the four rules on a small world
    /// driven by hand (a death is a <see cref="World.KillDiverged"/>, standing for whatever kills a leaf), and whole episodes swum through
    /// <see cref="Restep"/>. That a recipe without the field draws as before is <c>LeafPatchesTodayTests</c>.
    /// </summary>
    [Collection("restep")]
    public class LeafSwitchingTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("leaf-switching");

        public LeafSwitchingTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ the recipe

        private static string SwitchingJson(double period = 600, double regrow = 120, double wither = 300, double minTurn = 120) =>
            "{\"period\": " + period.ToString("R", Inv) + ", \"regrowAfter\": " + regrow.ToString("R", Inv) +
            ", \"witherOver\": " + wither.ToString("R", Inv) + ", \"minimumTurn\": " + minTurn.ToString("R", Inv) + "}";

        private static string PatchesJson(string switching, int count = 2) =>
            "{\"count\": " + count + ", \"radius\": 4.5, \"ring\": 6.5" + (switching == null ? "" : ", \"switching\": " + switching) + "}";

        private static string LeavesJson(string patches, int leaves = 6) =>
            "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": " + leaves + ", \"spentUnitsPerCubicMetre\": 0, \"patches\": " + patches + "}";

        private string WriteRecipe(string leaves, double seconds = 2, string name = "recipe.json")
        {
            (string config, string hash) = LeafTankFixtures.WriteConfig(_dir, LeafTankFixtures.Config());
            string pool = LeafTankFixtures.WriteBredPool(_dir, 3);
            return LeafTankFixtures.Write(_dir, name, LeafTankFixtures.RecipeJson(config, hash, pool, 3, leaves, seconds));
        }

        [Fact]
        public void TheFieldReadsBackToItselfAndMovesTheHash()
        {
            Recipe plain = Recipe.Read(WriteRecipe(LeavesJson(PatchesJson(null)), name: "plain.json"));
            Recipe sw = Recipe.Read(WriteRecipe(LeavesJson(PatchesJson(SwitchingJson())), name: "sw.json"));

            Assert.Null(plain.Leaves.Patches.Switching);
            Recipe.SwitchingSetting s = sw.Leaves.Patches.Switching;
            Assert.Equal((600d, 120d, 300d, 120d), (s.PeriodSeconds, s.RegrowAfterSeconds, s.WitherOverSeconds, s.MinimumTurnDegrees));
            Assert.DoesNotContain("\"switching\"", plain.Canonical());
            Assert.Contains("\"switching\"", sw.Canonical());
            _out.WriteLine(sw.Canonical());
            Assert.NotEqual(plain.Hash(), sw.Hash());

            // Written back, it reads to the same text and hash.
            string text = sw.Canonical();
            Recipe again = Recipe.Read(LeafTankFixtures.Write(_dir, "again.json", text));
            Assert.Equal(text, again.Canonical());
            Assert.Equal(sw.Hash(), again.Hash());

            // Each of its numbers moves the hash; null is the field left out.
            string[] variants =
            {
                SwitchingJson(period: 500), SwitchingJson(regrow: 60), SwitchingJson(wither: 200), SwitchingJson(minTurn: 90),
            };
            for (int i = 0; i < variants.Length; i++)
                Assert.NotEqual(sw.Hash(), Recipe.Read(WriteRecipe(LeavesJson(PatchesJson(variants[i])), name: "v" + i + ".json")).Hash());
            Recipe nulled = Recipe.Read(WriteRecipe(LeavesJson(PatchesJson("null")), name: "null.json"));
            Assert.Null(nulled.Leaves.Patches.Switching);
            Assert.Equal(plain.Canonical(), nulled.Canonical());
        }

        [Theory]
        [InlineData("{\"period\": 600, \"regrowAfter\": 120, \"witherOver\": 300}", "leaves.patches.switching is missing minimumTurn")]
        [InlineData("{\"period\": 600, \"regrowAfter\": 120, \"witherOver\": 300, \"minimumTurn\": 120, \"x\": 1}", "leaves.patches.switching has unknown x")]
        [InlineData("{\"period\": 0, \"regrowAfter\": 120, \"witherOver\": 300, \"minimumTurn\": 120}", "leaves.patches.switching.period must be more than 0")]
        [InlineData("{\"period\": -5, \"regrowAfter\": 120, \"witherOver\": 300, \"minimumTurn\": 120}", "leaves.patches.switching.period must be more than 0")]
        [InlineData("{\"period\": 600, \"regrowAfter\": -1, \"witherOver\": 300, \"minimumTurn\": 120}", "leaves.patches.switching.regrowAfter must be 0 s or more")]
        [InlineData("{\"period\": 600, \"regrowAfter\": 120, \"witherOver\": 0, \"minimumTurn\": 120}", "leaves.patches.switching.witherOver must be more than 0")]
        [InlineData("{\"period\": 600, \"regrowAfter\": 120, \"witherOver\": 601, \"minimumTurn\": 120}", "leaves.patches.switching.witherOver is 601 s, more than the period of 600 s")]
        [InlineData("{\"period\": 600, \"regrowAfter\": 120, \"witherOver\": 300, \"minimumTurn\": -1}", "leaves.patches.switching.minimumTurn must be from 0 to 180")]
        [InlineData("{\"period\": 600, \"regrowAfter\": 120, \"witherOver\": 300, \"minimumTurn\": 181}", "leaves.patches.switching.minimumTurn must be from 0 to 180")]
        [InlineData("{\"period\": \"600\", \"regrowAfter\": 120, \"witherOver\": 300, \"minimumTurn\": 120}", "leaves.patches.switching.period is a number")]
        [InlineData("3", "leaves.patches.switching is an object")]
        public void AFaultInTheFieldIsRefused(string switching, string said)
        {
            string path = WriteRecipe(LeavesJson(PatchesJson(switching)));
            var e = Assert.Throws<InvalidDataException>(() => Recipe.Read(path));
            Assert.Contains(said, e.Message);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void TheFieldIsRefusedUnlessThePatchesAreTwo(int count)
        {
            string path = WriteRecipe(LeavesJson(PatchesJson(SwitchingJson(), count: count)));
            var e = Assert.Throws<InvalidDataException>(() => Recipe.Read(path));
            Assert.Contains("is for two patches, and leaves.patches.count is " + count, e.Message);
        }

        [Fact]
        public void AnEdgeValueIsAccepted()
        {
            foreach (string s in new[] { SwitchingJson(regrow: 0), SwitchingJson(wither: 600), SwitchingJson(minTurn: 0), SwitchingJson(minTurn: 180) })
                Assert.NotNull(Recipe.Read(WriteRecipe(LeavesJson(PatchesJson(s)))).Leaves.Patches.Switching);
        }

        // ------------------------------------------------------------------ the plan

        private static Recipe.SwitchingSetting Setting(double period, double minTurn) =>
            new Recipe.SwitchingSetting { PeriodSeconds = period, RegrowAfterSeconds = 1, WitherOverSeconds = period / 2, MinimumTurnDegrees = minTurn };

        private static double Shorter(double from, double to)
        {
            double d = Math.Abs((to - from) % (2 * Math.PI));
            if (d > Math.PI) d = 2 * Math.PI - d;
            return d * 180d / Math.PI;
        }

        [Theory]
        [InlineData(0d)]
        [InlineData(90d)]
        [InlineData(120d)]
        [InlineData(180d)]
        public void EveryNewAngleIsAtLeastTheMinimumTurnFromTheOutgoingDiscsAndTheDiscsAlternate(double minimum)
        {
            const double ring = 6.5, tank = 11.28;
            var turns = new List<double>();
            int firstOne = 0, over = 0, under = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                double turn = 0.3 + 0.01 * seed;
                LeafSwitchPlan plan = LeafSwitchPlan.Draw(seed, Setting(10, minimum), ring, tank, turn, 100d);
                Assert.Equal(9, plan.Switches.Count);   // 10, 20, ... 90: strictly before the episode's end
                if (plan.FirstFedDisc == 1) firstOne++;
                double prev = turn + Math.PI * plan.FirstFedDisc;
                int disc = plan.FirstFedDisc;
                int k = 0;
                foreach (LeafSwitch s in plan.Switches)
                {
                    k++;
                    Assert.Equal(k, s.Index);
                    Assert.Equal(10d * k, s.Time);
                    Assert.Equal(1 - disc, s.Disc);
                    double turned = Shorter(prev, s.Angle);
                    Assert.InRange(turned, minimum - 1e-6, 180d + 1e-6);
                    Assert.Equal(turned, s.TurnDegrees, 6);
                    Assert.Equal(2 * ring * Math.Sin(turned * Math.PI / 360d), s.CentreDistance, 6);
                    Assert.Equal(tank + ring * Math.Cos(s.Angle), s.CentreX, 9);
                    Assert.Equal(tank + ring * Math.Sin(s.Angle), s.CentreZ, 9);
                    Assert.InRange(s.Angle, 0d, 2 * Math.PI);
                    double signed = (s.Angle - prev) % (2 * Math.PI);
                    if (signed < 0) signed += 2 * Math.PI;
                    if (signed < Math.PI) over++; else under++;
                    turns.Add(turned);
                    prev = s.Angle;
                    disc = s.Disc;
                }
            }
            _out.WriteLine($"minimum {minimum}: turned {turns.Min():0.0} to {turns.Max():0.0}; first fed disc 1 in {firstOne} of 300; around one way {over}, the other {under}");
            Assert.InRange(firstOne, 100, 200);                      // drawn per episode: neither disc is always first
            if (minimum < 180d)
            {
                Assert.True(over > 0 && under > 0, "the arc is on both sides of the outgoing disc");
                Assert.InRange(turns.Max(), 175d, 180d);             // the whole arc is used
            }
            else
            {
                Assert.All(turns, t => Assert.Equal(180d, t, 6));    // a minimum of 180 is the opposite side
            }
        }

        [Fact]
        public void ThePlanFollowsTheSeedAndTheEpisodesLengthAloneAndAnEpisodeShorterThanAPeriodHasNoSwitch()
        {
            LeafSwitchPlan a = LeafSwitchPlan.Draw(7, Setting(10, 120), 6.5, 11.28, 0.5, 100.5);
            LeafSwitchPlan b = LeafSwitchPlan.Draw(7, Setting(10, 120), 6.5, 11.28, 0.5, 100.5);
            Assert.Equal(10, a.Switches.Count);
            Assert.Equal(a.FirstFedDisc, b.FirstFedDisc);
            Assert.Equal(a.Switches.Select(s => s.Angle), b.Switches.Select(s => s.Angle));
            Assert.NotEqual(a.Switches.Select(s => s.Angle), LeafSwitchPlan.Draw(8, Setting(10, 120), 6.5, 11.28, 0.5, 100.5).Switches.Select(s => s.Angle));
            Assert.Empty(LeafSwitchPlan.Draw(7, Setting(10, 120), 6.5, 11.28, 0.5, 10d).Switches);   // a switch at the episode's end does not happen
            Assert.Empty(LeafSwitchPlan.Draw(7, Setting(10, 120), 6.5, 11.28, 0.5, 9d).Switches);

            // The plan's stream is salted apart from the leaves' and the tank's.
            Assert.NotEqual(0x6C656166UL, LeafSwitchPlan.Salt);
            Assert.NotEqual(0x74616E6BUL, LeafSwitchPlan.Salt);
        }

        // ------------------------------------------------------------------ the rules, on a small world driven by hand

        private static readonly Lazy<LeafPool> CommittedPool = new Lazy<LeafPool>(() =>
            LeafPool.Read(Path.Combine(RestepFixtures.Root, LeafTankFixtures.LeafPool.Replace('/', Path.DirectorySeparatorChar))));

        /// <summary>A world with no physics and the switcher over six leaves, three a disc; the clock moves by <see cref="Advance"/>.</summary>
        private sealed class Rig
        {
            public World World;
            public LeafSwitcher Switcher;
            public LeafSchedule Schedule => Switcher.Schedule;
            public Recipe.PatchSetting Patches;
            public double Turn;
        }

        private static Rig MakeRig(ulong seed, double period = 20, double regrow = 5, double wither = 8, double minTurn = 120, int perDisc = 3, double episode = 200)
        {
            RunConfig config = LeafTankFixtures.Config();
            var world = new World(config, seed) { BirthsHeld = true };
            var patches = new Recipe.PatchSetting
            {
                Count = 2, RadiusMetres = 4.5, RingMetres = 6.5,
                Switching = new Recipe.SwitchingSetting { PeriodSeconds = period, RegrowAfterSeconds = regrow, WitherOverSeconds = wither, MinimumTurnDegrees = minTurn },
            };
            float tank = world.TankRadiusMetres;
            double turn = 0.7;
            var leaves = new List<Organism>();
            var placed = new List<(float x, float z, float radius, int patch)>();
            int row = 0;
            for (int disc = 0; disc < 2; disc++)
            {
                (double cx, double cz) = LeafPatches.Centre(tank, patches.RingMetres, 2, turn, disc);
                for (int i = 0; i < perDisc; i++)
                {
                    LeafPool.Leaf leaf;
                    Phenotype adult;
                    float radius;
                    do
                    {
                        leaf = CommittedPool.Value.Leaves[row++];
                        adult = Developer.Develop(leaf.Genome, config.Development, null, config.Shapes, cellTypes: config.CellTypes);
                        radius = SharedVolume.BoundingRadius(adult);
                    } while (radius > patches.RadiusMetres - 2f);
                    float x = (float)(cx + 1.2 * Math.Cos(2.1 * i)), z = (float)(cz + 1.2 * Math.Sin(2.1 * i));
                    float y = Math.Min(-radius, Math.Max(-config.WorldDepthMetres + radius, leaf.HeightY));
                    Organism o = world.InoculateAt(leaf.Genome, new Float3(x, y, z));
                    double owed = Math.Max(0d, Metabolism.TissueJoules(o.AdultPhenotype, config) - o.TissueJoules);
                    if (owed > 0d) world.GiveReserve(o, owed);
                    leaves.Add(o);
                    placed.Add((x, z, radius, disc));
                }
            }
            var switcher = new LeafSwitcher(patches, CommittedPool.Value, config, seed, tank, episode, turn, LeafSwitcher.DealtOf(placed), null);
            switcher.Adopt(leaves, placed);
            return new Rig { World = world, Switcher = switcher, Patches = patches, Turn = turn };
        }

        /// <summary>One economy step of the clock: the world's, then the switcher's turn.</summary>
        private static void Step(Rig rig)
        {
            rig.World.Step(0.5f);
            rig.Switcher.AfterEconomy(rig.World);
        }

        private static void Advance(Rig rig, double untilSeconds)
        {
            while (rig.World.ElapsedSeconds < untilSeconds - 1e-9) Step(rig);
        }

        private static int Alive(Rig rig, int opening) =>
            rig.World.Living.Count(o => rig.Schedule.OpeningOf(o.Id) == opening);

        private static List<Organism> LivingOf(Rig rig, int opening) =>
            rig.World.Living.Where(o => rig.Schedule.OpeningOf(o.Id) == opening).ToList();

        [Fact]
        public void TheFedDiscsDeadLeafIsReplacedAfterRegrowAfterAndOnlyThen()
        {
            Rig rig = MakeRig(11);
            int fed = rig.Schedule.Plan.FirstFedDisc;
            Advance(rig, 2.0);
            Assert.Equal(3, Alive(rig, fed));
            Assert.Equal(3, Alive(rig, 1 - fed));
            Assert.Equal(0d, rig.Schedule.Openings[fed].FedFrom);
            Assert.True(double.IsNaN(rig.Schedule.Openings[1 - fed].FedFrom));

            Organism victim = LivingOf(rig, fed)[0];
            rig.World.KillDiverged(victim);
            Step(rig);
            double seen = rig.World.ElapsedSeconds;          // the step the leaf's leaving is seen in
            Assert.Equal(2, Alive(rig, fed));

            Advance(rig, seen + 4.5);                          // a half second short of regrowAfter
            Assert.Equal(2, Alive(rig, fed));
            Assert.Equal(0, rig.Schedule.Regrown);
            Step(rig);
            Assert.Equal(seen + 5d, rig.World.ElapsedSeconds, 6);
            Assert.Equal(3, Alive(rig, fed));
            Assert.Equal(1, rig.Schedule.Regrown);

            // The replacement is a pool leaf of the same opening, in the disc, grown.
            Organism fresh = LivingOf(rig, fed).OrderBy(o => o.Id).Last();
            LeafOpening opening = rig.Schedule.Openings[fed];
            double dx = fresh.X - opening.CentreX, dz = fresh.Z - opening.CentreZ;
            Assert.True(Math.Sqrt(dx * dx + dz * dz) <= rig.Patches.RadiusMetres, "the replacement lies outside its disc");
            Assert.True(fresh.TissueJoules + fresh.Energy >= fresh.AdultTissueJoules, "the replacement was not paid its growth");

            // Still nothing happens in the other disc.
            Assert.Equal(3, Alive(rig, 1 - fed));
        }

        [Fact]
        public void TheUnfedDiscNeitherRegrowsNorWithersInTheFirstPeriod()
        {
            Rig rig = MakeRig(12);
            int fed = rig.Schedule.Plan.FirstFedDisc, unfed = 1 - fed;
            Advance(rig, 2.0);
            rig.World.KillDiverged(LivingOf(rig, unfed)[0]);
            Advance(rig, 19.5);                                // the last step before the first switch
            Assert.Equal(2, Alive(rig, unfed));
            Assert.Equal(3, Alive(rig, fed));
            Assert.Equal(0, rig.Schedule.Regrown);
            Assert.Equal(1, rig.World.Deaths);                 // the one we killed, and no wither
            Assert.Equal(2, rig.Schedule.Openings.Count);
        }

        [Fact]
        public void AtASwitchEveryLeafOfBothOldOpeningsIsGoneWithinWitherOverAndLeftNothing()
        {
            Rig rig = MakeRig(13);
            int fed = rig.Schedule.Plan.FirstFedDisc;
            Advance(rig, 19.5);
            Assert.Equal(6, Alive(rig, 0) + Alive(rig, 1));
            double detritus = rig.World.DetritusDepositedTotal;
            double out0 = rig.World.EnergyOut;
            rig.World.DrainLineageEvents();

            var goneAt = new Dictionary<long, double>();
            long[] oldIds = rig.World.Living.Select(o => o.Id).ToArray();
            while (rig.World.ElapsedSeconds < 28.0 - 1e-9)
            {
                Step(rig);
                foreach (long id in oldIds)
                    if (!goneAt.ContainsKey(id) && !rig.World.Living.Any(o => o.Id == id)) goneAt[id] = rig.World.ElapsedSeconds;
            }

            // The switch happened at 20 s and opened a third opening for the disc that was not fed.
            Assert.Equal(3, rig.Schedule.Openings.Count);
            LeafOpening nw = rig.Schedule.Openings[2];
            Assert.Equal(1 - fed, nw.Disc);
            Assert.Equal(20d, nw.OpenedAt);
            Assert.Equal(20d, nw.FedFrom);
            Assert.Equal(20d, rig.Schedule.Openings[fed].FedUntil);
            Assert.Equal(20d, rig.Schedule.Openings[fed].WitheringFrom);
            Assert.Equal(20d, rig.Schedule.Openings[1 - fed].WitheringFrom);
            Assert.Same(nw, rig.Schedule.FedAt(25d));
            Assert.Same(rig.Schedule.Openings[fed], rig.Schedule.FedAt(19.9));

            // Every old leaf is gone by 20 s + witherOver, none before 20 s, at their own seconds.
            Assert.Equal(6, goneAt.Count);
            _out.WriteLine("gone at " + string.Join(", ", goneAt.Values.OrderBy(t => t).Select(t => t.ToString("0.0", Inv))));
            Assert.All(goneAt.Values, t => Assert.InRange(t, 20d, 28d));
            Assert.True(goneAt.Values.Distinct().Count() >= 2, "the leaves withered at one moment, not each at its own second");
            Assert.Equal(0, Alive(rig, 0) + Alive(rig, 1));

            // They left nothing: no corpse, no remains, no detritus, and six death rows saying withered.
            Assert.Empty(rig.World.Corpses);
            Assert.Empty(rig.World.Remains);
            Assert.Equal(detritus, rig.World.DetritusDepositedTotal);
            Assert.True(rig.World.EnergyOut > out0);
            LineageEvent[] deaths = rig.World.DrainLineageEvents().Where(e => e.Kind == LineageEventKind.Death).ToArray();
            Assert.Equal(6, deaths.Length);
            Assert.All(deaths, e => Assert.Equal(DeathCause.Withered, e.Cause));

            // The disc now fed holds a full fresh complement of three, in its new disc.
            Assert.Equal(3, Alive(rig, 2));
            Assert.All(LivingOf(rig, 2), l =>
            {
                double dx = l.X - nw.CentreX, dz = l.Z - nw.CentreZ;
                Assert.True(Math.Sqrt(dx * dx + dz * dz) <= rig.Patches.RadiusMetres);
            });
            Assert.True(Math.Abs(rig.World.AuditResidual) <= 1e-6 * Math.Max(1d, rig.World.StandingJoules),
                $"the energy audit does not close: {rig.World.AuditResidual}");
        }

        [Fact]
        public void TheNewOpeningsAngleIsAtLeastTheMinimumTurnFromTheOutgoingDiscsInTheWorldToo()
        {
            Rig rig = MakeRig(14, period: 10, wither: 5, regrow: 3, minTurn: 150, episode: 62);
            Advance(rig, 62);
            IReadOnlyList<LeafOpening> o = rig.Schedule.Openings;
            Assert.Equal(2 + 6, o.Count);                      // switches at 10, 20 ... 60
            int fed = rig.Schedule.Plan.FirstFedDisc;
            LeafOpening outgoing = o[fed];
            for (int k = 2; k < o.Count; k++)
            {
                double turned = Shorter(outgoing.Angle, o[k].Angle);
                Assert.InRange(turned, 150d - 1e-6, 180d + 1e-6);
                Assert.Equal(outgoing.Disc == 0 ? 1 : 0, o[k].Disc);
                Assert.Equal(10d * (k - 1), o[k].OpenedAt);
                outgoing = o[k];
            }
            // At each switch the one that was fed is not fed any longer and the new one is, so exactly one opening is fed at any time.
            for (double t = 0.25; t < 62; t += 1.0) Assert.NotNull(rig.Schedule.FedAt(t));
            foreach (LeafOpening opening in o.Skip(2)) Assert.Equal(opening.OpenedAt, opening.FedFrom);
        }

        [Fact]
        public void TheSameEpisodeStepsToTheSameBitsAndLeafIdsMapToTheirOpenings()
        {
            string Run(ulong seed)
            {
                Rig rig = MakeRig(seed, period: 10, wither: 4, regrow: 2, episode: 40);
                var lines = new List<string>();
                for (int s = 0; s < 70; s++)
                {
                    Step(rig);
                    if (s == 7) rig.World.KillDiverged(rig.World.Living.First(o => rig.Schedule.OpeningOf(o.Id) == rig.Schedule.Plan.FirstFedDisc));
                    if (s == 33) rig.World.KillDiverged(rig.World.Living.First());
                    lines.Add(BitConverter.DoubleToInt64Bits(rig.World.ElapsedSeconds).ToString("x") + " " +
                              string.Join(",", rig.World.Living.Select(o => o.Id + ":" + rig.Schedule.OpeningOf(o.Id) + ":" +
                                  BitConverter.DoubleToInt64Bits(o.X).ToString("x") + ":" + BitConverter.DoubleToInt64Bits(o.Energy).ToString("x"))));
                }
                Assert.All(rig.World.Living, o => Assert.True(rig.Schedule.OpeningOf(o.Id) >= 0));
                Assert.Equal(-1, rig.Schedule.OpeningOf(-12345L));
                return string.Join("\n", lines);
            }
            Assert.Equal(Run(21), Run(21));
            Assert.NotEqual(Run(21), Run(22));
        }

        // ------------------------------------------------------------------ whole episodes, swum in the solver

        private string SwitchingEpisodeRecipe(double seconds) =>
            WriteRecipe(LeavesJson(PatchesJson(SwitchingJson(period: 3, regrow: 1, wither: 1.5, minTurn: 120))), seconds, "episode.json");

        /// <summary>Everything a watcher can read of a switching episode, as text, frame by frame, and what the schedule said at the end.</summary>
        private sealed class Frames : ITankWatcher
        {
            public readonly List<string> Lines = new List<string>();
            public LeafSchedule Schedule;
            public int Corpses, Remains, MaxOpenings;
            public int[] LeavesAtEnd;
            public int PlacementDelays;
            public int LeavesAtBegin, MappedAtBegin;

            private static string Bits(double v) => BitConverter.DoubleToInt64Bits(v).ToString("x16", Inv);

            private string Frame(string what, EpisodeProbe p)
            {
                IReadOnlyList<BodyView> others = p.Others();
                Corpses = Math.Max(Corpses, p.CorpseCount);
                Remains = Math.Max(Remains, p.RemainsCount);
                MaxOpenings = Math.Max(MaxOpenings, p.Schedule.Openings.Count);
                Assert.All(others, l => Assert.True(p.Schedule.OpeningOf(l.Id) >= 0, "a leaf the schedule does not know"));
                return what + " " + Bits(p.EconomySeconds) + " " + p.Deaths + " " + p.LivingCount + " " + p.SolverDigest().ToString("x16", Inv) + " " +
                       string.Join(",", others.Select(l => l.Id + ":" + p.Schedule.OpeningOf(l.Id) + ":" + Bits(l.X) + ":" + Bits(l.Z) + ":" + Bits(l.Reserve))) +
                       " | openings " + string.Join(",", p.Schedule.Openings.Select(o => string.Join(" ", new[] { o.Id.ToString(Inv), o.Disc.ToString(Inv), Bits(o.Angle), Bits(o.OpenedAt), Bits(o.FedFrom), Bits(o.FedUntil), Bits(o.WitheringFrom) })));
            }

            public void Begin(EpisodeProbe probe)
            {
                Schedule = probe.Schedule;
                Assert.NotNull(Schedule);
                LeavesAtBegin = probe.Others().Count;
                MappedAtBegin = probe.Others().Count(l => Schedule.OpeningOf(l.Id) >= 0);
                Lines.Add(Frame("begin", probe));
            }

            public void Physics(EpisodeProbe probe) { }

            public void Metabolic(EpisodeProbe probe) => Lines.Add(Frame("economy", probe));

            public void End(EpisodeProbe probe)
            {
                Lines.Add(Frame("end", probe));
                LeavesAtEnd = probe.Others().GroupBy(l => Schedule.OpeningOf(l.Id)).OrderBy(g => g.Key).SelectMany(g => new[] { g.Key, g.Count() }).ToArray();
                PlacementDelays = Schedule.PlacementDelays;
            }
        }

        [Fact]
        public void ASwitchingEpisodeSwimsTwiceToTheBitAndWithAWatcherAsWithout()
        {
            string recipe = SwitchingEpisodeRecipe(8);

            var first = new Frames();
            RestepOutcome a = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, first);
            var second = new Frames();
            RestepOutcome b = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, second);
            RestepOutcome bare = LeafTankFixtures.Swim(_dir, recipe, 3, 3, 0, null);
            foreach (string line in first.Lines.Take(3)) _out.WriteLine(line.Length > 300 ? line.Substring(0, 300) + "..." : line);

            Assert.Equal(first.Lines, second.Lines);
            Assert.Equal(a.Members.Select(m => LeafTankFixtures.Line(m)), b.Members.Select(m => LeafTankFixtures.Line(m)));
            Assert.Equal(a.Members.Select(m => LeafTankFixtures.Line(m)), bare.Members.Select(m => LeafTankFixtures.Line(m)));

            // And it was a switching episode: 8 s at a period of 3 is two switches, so four openings.
            Assert.Equal(4, first.MaxOpenings);
            Assert.Equal(6, first.LeavesAtBegin);
            Assert.Equal(6, first.MappedAtBegin);
        }

        [Fact]
        public void ASwitchingEpisodeWithersTheOldOpeningsLeavesNothingAndLaysTheFreshComplementInTheSolver()
        {
            var frames = new Frames();
            LeafTankFixtures.Swim(_dir, SwitchingEpisodeRecipe(8), 3, 3, 0, frames);

            Assert.Equal(0, frames.Corpses);
            Assert.Equal(0, frames.Remains);
            Assert.Equal(4, frames.MaxOpenings);

            // At the end only the last opening (3) has leaves: openings 0 and 1 withered by 3 s + 1.5 s, opening 2 by 6 s + 1.5 s.
            // LeavesAtEnd is pairs of (opening, count). Three leaves were dealt to each disc, so a full complement is three.
            _out.WriteLine("at the end: " + string.Join(" ", frames.LeavesAtEnd) + $"; placement delays {frames.PlacementDelays}");
            Assert.Equal(new[] { 3 }, frames.LeavesAtEnd.Where((v, i) => i % 2 == 0));
            int laid = frames.LeavesAtEnd[1];
            Assert.True(laid == 3 || frames.PlacementDelays > 0, $"{laid} of 3 leaves laid and no delay counted");

            // The openings, as the schedule ended: the fed disc is the last, the others stopped being fed at their switches.
            LeafSchedule s = frames.Schedule;
            Assert.Equal(new[] { 0, 1, 2, 3 }, s.Openings.Select(o => o.Id));
            Assert.Equal(new[] { 3d, 6d }, s.Plan.Switches.Select(w => w.Time));
            Assert.Equal(3d, s.Openings[2].OpenedAt);
            Assert.Equal(6d, s.Openings[3].OpenedAt);
            Assert.Equal(3d, s.Openings[s.Plan.FirstFedDisc].FedUntil);
            Assert.Equal(6d, s.Openings[2].FedUntil);
            Assert.True(double.IsNaN(s.Openings[3].FedUntil));
        }
    }
}

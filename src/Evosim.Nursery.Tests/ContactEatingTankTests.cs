using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Evosim.Core;
using Evosim.Nursery;
using Evosim.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// The tank's "eaten" under the world's contact eating (D219 to D221): a bite's meal goes to <c>Organism.BiteKeptJoules</c>, which does not say
    /// what was bitten, so the episode reads <c>Organism.FoodFromLeavesJoules</c> instead. Swum in-process through <see cref="Restep"/> as
    /// <see cref="LeafTankSwimmerTests"/> does, on a small tank so a swimmer meets a leaf within seconds.
    /// </summary>
    [Collection("restep")]
    public class ContactEatingTankTests : IDisposable
    {
        private readonly ITestOutputHelper _out;
        private readonly string _dir = RestepFixtures.NewDir("contact-tank");

        public ContactEatingTankTests(ITestOutputHelper output) => _out = output;

        public void Dispose() => Directory.Delete(_dir, true);

        private static Genome Swimmer() =>
            GenomeJson.Read(File.ReadAllText(Path.Combine(RestepFixtures.Root, LeafTankSwimmerTests.Plate.Replace('/', Path.DirectorySeparatorChar))));

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        /// <summary>A small tank under contact eating.</summary>
        private static RunConfig Contact(bool leaves, bool on = true)
        {
            RunConfig c = LeafTankFixtures.Config(leaves);
            c.ContactEating = on;
            c.WorldAreaSquareMetres = 100f;
            return c;
        }

        /// <summary>What a watcher read of each member's counters, off the episode's own bodies (the probe offers no such member and gains none).</summary>
        private sealed class Counters : ITankWatcher
        {
            public double[] FoodFirst, FoodLast, KeptFirst, KeptLast;

            private static Organism[] Focal(EpisodeProbe probe) =>
                (Organism[])typeof(EpisodeProbe).GetField("_focal", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(probe);

            public void Begin(EpisodeProbe probe)
            {
                Organism[] f = Focal(probe);
                FoodFirst = f.Select(o => o?.FoodFromLeavesJoules ?? double.NaN).ToArray();
                KeptFirst = f.Select(o => o?.BiteKeptJoules ?? double.NaN).ToArray();
            }

            public void Physics(EpisodeProbe probe) { }

            /// <summary>
            /// When set, the first economy step ends with two bites handed to the world by hand, as <c>ContactEatingTests</c> does (Core's
            /// <c>SetContacts</c> and <c>ApplyMouth</c>): member 0's mouth on a leaf, then on member 1. The tank then reads them on its next economy step.
            /// </summary>
            public bool Feed;
            public double LeafGain = double.NaN, MemberGain = double.NaN;
            public bool Fed, BothAlive;
            private bool _fed;

            public void Metabolic(EpisodeProbe probe)
            {
                if (!Feed || _fed) return;
                _fed = true;
                Organism[] f = Focal(probe);
                var world = (World)typeof(EpisodeProbe).GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(probe);
                BothAlive = world.Living.Contains(f[0]) && world.Living.Contains(f[1]);
                if (!BothAlive) return;
                Fed = true;
                Organism leaf = world.Living.First(b => b.HasPhotosyntheticTissue && Array.IndexOf(f, b) < 0);

                double kept = f[0].BiteKeptJoules;
                world.SetContacts(new List<CreatureContact> { new CreatureContact(f[0].Id, 0, leaf.Id, 0) });
                world.ApplyMouth(1f);
                LeafGain = f[0].BiteKeptJoules - kept;

                kept = f[0].BiteKeptJoules;
                // On member 1's limb (part 1), which has no mouth: a contact bites both ways, and mouth to mouth the two trunks bit each
                // other to death (54 J each, the first run of this test).
                world.SetContacts(new List<CreatureContact> { new CreatureContact(f[0].Id, 0, f[1].Id, 1) });
                world.ApplyMouth(1f);
                MemberGain = f[0].BiteKeptJoules - kept;
            }

            public void End(EpisodeProbe probe)
            {
                Organism[] f = Focal(probe);
                FoodLast = f.Select(o => o?.FoodFromLeavesJoules ?? double.NaN).ToArray();
                KeptLast = f.Select(o => o?.BiteKeptJoules ?? double.NaN).ToArray();
            }
        }

        private string Recipe(RunConfig config, int count, Func<int, Genome> bred, string leaves, double seconds, double? purseSeconds = null)
        {
            (string path, string hash) = LeafTankFixtures.WriteConfig(_dir, config);
            string pool = LeafTankFixtures.WriteBredPool(_dir, count, bred);
            return LeafTankFixtures.Write(_dir, "recipe.json", LeafTankFixtures.RecipeJson(path, hash, pool, count, leaves, seconds, purseSeconds: purseSeconds));
        }

        private static string Leaves(int count) =>
            "{\"pool\": \"" + LeafTankFixtures.LeafPool + "\", \"count\": " + count + ", \"spentUnitsPerCubicMetre\": 0}";

        [LocalDataTreeFact("rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz")]
        public void UnderContactEatingASwimmersEatenIsWhatItKeptFromLeavesAndAMealOnAMemberIsNotEaten()
        {
            string recipe = Recipe(Contact(leaves: true), 2, _ => Swimmer(), Leaves(6), seconds: 6, purseSeconds: 600);
            var seen = new Counters { Feed = true };
            RestepOutcome o = LeafTankFixtures.Swim(_dir, recipe, 2, 2, 0, seen);

            // The bites are certain, so the test does not depend on where the swimmers swim; the purse keeps both members alive to the first economy step.
            Assert.True(seen.BothAlive, "a member was not living at the first economy step, so the feeding was not made");
            Assert.True(seen.Fed);
            _out.WriteLine(FormattableString.Invariant($"fed: {seen.LeafGain:R} J from a leaf, {seen.MemberGain:R} J from a member"));
            Assert.True(seen.LeafGain > 0d, "the hand-made bite on a leaf kept nothing");
            Assert.True(seen.MemberGain > 0d, "the hand-made bite on a member kept nothing");

            // Slot 0 is member 0 of the tank, as the watcher reads it (the probe's focal order).
            for (int k = 0; k < 2; k++)
            {
                RestepMemberOutcome m = o.Members[k];
                _out.WriteLine(FormattableString.Invariant(
                    $"slot {k}: landed {m.Landed}, survived {m.Survived}, eaten {m.Eaten:R} J, meals {m.Meals}, leaf food {seen.FoodFirst[k]:R} to {seen.FoodLast[k]:R}, bite kept {seen.KeptFirst[k]:R} to {seen.KeptLast[k]:R}"));
                Assert.True(m.Landed);
                Assert.Equal(Bits(seen.FoodLast[k] - seen.FoodFirst[k]), Bits(m.Eaten));
            }

            // The feeder: eaten holds the leaf's share and not the member's, though both were kept.
            RestepMemberOutcome feeder = o.Members[0];
            double kept = seen.KeptLast[0] - seen.KeptFirst[0];
            Assert.True(feeder.Eaten >= seen.LeafGain * (1d - 1e-9), "eaten lacks the bite on the leaf");
            Assert.True(kept >= feeder.Eaten + seen.MemberGain * (1d - 1e-9), "the bite on the member is not in what the bites kept");
            Assert.True(feeder.Eaten <= kept - seen.MemberGain * (1d - 1e-9), "the bite on the member was counted as eaten");
            Assert.True(feeder.Survived, "the feeder did not live out the episode");
            Assert.True(feeder.Meals > 0d);

            // The member that was bitten ate nothing from it.
            Assert.True(o.Members[1].Eaten <= seen.KeptLast[1] - seen.KeptFirst[1] + 1e-9);
        }

        [Fact]
        public void UnderContactEatingMembersWithNoLeavesInTheTankEatNothingWhateverTheyBiteOfEachOther()
        {
            string recipe = Recipe(Contact(leaves: false), 2, _ => Swimmer(), null, seconds: 15);
            var seen = new Counters();
            RestepOutcome o = LeafTankFixtures.Swim(_dir, recipe, 2, 2, 0, seen);

            for (int k = 0; k < 2; k++)
            {
                _out.WriteLine(FormattableString.Invariant(
                    $"slot {k}: eaten {o.Members[k].Eaten:R} J, meals {o.Members[k].Meals}, bite kept {seen.KeptFirst[k]:R} to {seen.KeptLast[k]:R}"));
                Assert.Equal(0d, o.Members[k].Eaten);
                Assert.Equal(0d, o.Members[k].Meals);
            }
        }
    }
}

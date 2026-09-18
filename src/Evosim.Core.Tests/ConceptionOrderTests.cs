using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="RunConfig.ConceptionOrder"/> — the order <see cref="World"/> offers the living
    /// their turn at conception (D072, logbook/0056).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things have to be true for the knob to be worth having, and each is one test here.
    /// The default has to be the world every earlier arm ran in, step for step, or the historical
    /// record stops replaying. The new order has to be reproducible from <c>(seed, config)</c> like
    /// everything else (§7), or an arm run under it is not an experiment. And it has to actually
    /// change who breeds when a layer's matter covers fewer children than there are parents who
    /// want one — which is the fault it exists for, and which CLAUDE.md's "identical numbers"
    /// rule says to prove rather than assume.
    /// </para>
    /// <para>
    /// The contest below is the fault in miniature: two identical solvent leaves at one depth, one
    /// child's worth of matter per step, and nothing else able to breed. Under
    /// <see cref="ConceptionOrder.Age"/> the elder takes it every single step and the younger never
    /// breeds at all; under <see cref="ConceptionOrder.Shuffled"/> they split it.
    /// </para>
    /// <para>
    /// <see cref="ConceptionOrder.Reserve"/> — D073, logbook/0057 — is asked the same question by
    /// the same world, with the two contestants no longer equal: one earns more light than the
    /// other. It has to give the matter to the richer of them and to keep doing it whichever of
    /// them is older, because the whole point of the rule is that the winner is chosen by the
    /// energy books rather than by anything the walk knows about age.
    /// </para>
    /// </remarks>
    public class ConceptionOrderTests
    {
        private readonly ITestOutputHelper _output;

        public ConceptionOrderTests(ITestOutputHelper output) => _output = output;

        // ---------------------------------------------------------------------------------
        // 1. The default is the world that already existed.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void TheDefaultIsAgeAndSettingItExplicitlyChangesNothing()
        {
            // The D052/D055 shape, tested rather than asserted in a comment: a run that never
            // heard of this knob must be bit-identical to one that names its default. A world is
            // compared by everything it can be compared by — who is alive and in what order, how
            // many were born and died, and both conserved totals — because the walk order decides
            // *which* creature breeds, and a difference in it need not show up in a population
            // count at all.
            Assert.Equal(ConceptionOrder.Age, new RunConfig().ConceptionOrder);

            var untouched = new World(Reference(), seed: 9);
            var named = new World(Reference(ConceptionOrder.Age), seed: 9);

            Assert.Equal(untouched.Config.Hash(), named.Config.Hash());

            for (int i = 0; i < 200; i++)
            {
                untouched.Step(1f);
                named.Step(1f);
            }

            _output.WriteLine(Describe("untouched", untouched));
            _output.WriteLine(Describe("named Age ", named));

            AssertSameWorld(untouched, named);
        }

        // ---------------------------------------------------------------------------------
        // 2. The new order replays.
        // ---------------------------------------------------------------------------------

        [Fact]
        public void ShuffledReplaysFromTheSameSeedAndConfig()
        {
            // §7's promise applied to the permutation. The stream behind it is the world's own,
            // seeded from Rng.SeedFor(Seed, a reserved index) at construction, so two worlds built
            // the same way draw the same permutations in the same order — and a shuffled arm is an
            // experiment somebody can repeat rather than a one-off.
            var first = new World(Reference(ConceptionOrder.Shuffled), seed: 9);
            var second = new World(Reference(ConceptionOrder.Shuffled), seed: 9);

            for (int i = 0; i < 200; i++)
            {
                first.Step(1f);
                second.Step(1f);
            }

            _output.WriteLine(Describe("first ", first));
            _output.WriteLine(Describe("second", second));

            AssertSameWorld(first, second);
        }

        [Fact]
        public void ShuffledIsADifferentWorldFromAge()
        {
            // The other half of the same guard, and the one this project has learned to insist on:
            // identical numbers across a configuration change mean the change never reached the
            // thing it configures (logbook/0007, logbook/0008). Two worlds differing only in the
            // knob must not agree on everything.
            var age = new World(Reference(ConceptionOrder.Age), seed: 9);
            var shuffled = new World(Reference(ConceptionOrder.Shuffled), seed: 9);

            for (int i = 0; i < 200; i++)
            {
                age.Step(1f);
                shuffled.Step(1f);
            }

            _output.WriteLine(Describe("age     ", age));
            _output.WriteLine(Describe("shuffled", shuffled));

            Assert.NotEqual(age.Config.Hash(), shuffled.Config.Hash());
            Assert.NotEqual(LivingIds(age), LivingIds(shuffled));
        }

        [Fact]
        public void ReserveReplaysFromTheSameSeedAndConfig()
        {
            // §7 again, and easier to keep here than for Shuffled: Reserve takes no draw from any
            // stream at all — the ranking is a function of the living and their energies — so the
            // only way it could fail to replay is a tie broken by whatever Array.Sort felt like.
            // That is the ordering's index fallback, tested rather than argued.
            var first = new World(Reference(ConceptionOrder.Reserve), seed: 9);
            var second = new World(Reference(ConceptionOrder.Reserve), seed: 9);

            for (int i = 0; i < 200; i++)
            {
                first.Step(1f);
                second.Step(1f);
            }

            _output.WriteLine(Describe("first ", first));
            _output.WriteLine(Describe("second", second));

            AssertSameWorld(first, second);
        }

        [Fact]
        public void ReserveIsADifferentWorldFromAge()
        {
            // logbook/0007 and logbook/0008's rule, for the third member: a knob that reached
            // nothing would produce exactly the reassuring agreement this asserts against.
            var age = new World(Reference(ConceptionOrder.Age), seed: 9);
            var reserve = new World(Reference(ConceptionOrder.Reserve), seed: 9);

            for (int i = 0; i < 200; i++)
            {
                age.Step(1f);
                reserve.Step(1f);
            }

            _output.WriteLine(Describe("age    ", age));
            _output.WriteLine(Describe("reserve", reserve));

            Assert.NotEqual(age.Config.Hash(), reserve.Config.Hash());
            Assert.NotEqual(LivingIds(age), LivingIds(reserve));
        }

        // ---------------------------------------------------------------------------------
        // 3. The contest, retired by D098.
        // ---------------------------------------------------------------------------------
        //
        // Two theories lived here: TwoEqualParentsAndOneChildsWorthOfMatter and
        // OneRichParentAndOnePoorOne. Both put two solvent parents at one depth with exactly one
        // child's worth of matter in their layer at the start of every step, and read which of
        // them the walk let take it. That contest was for the conception matter price, and D098
        // deleted the price: a child is charged matter given by its parent and draws on no field
        // at all, so there is no longer a shared stock that reproduction can exhaust and nothing
        // for the walk order to award. Rewriting the contest around a different scarcity would be
        // a different test with the same name, which is worse than not having it.
        //
        // What still guards the knob is everything above and below: the default, the two
        // replay-determinism pairs, the two different-world pairs, and the hash-and-file
        // round-trip. What is no longer guarded is the behaviour under a contested resource,
        // and the honest place for that test is wherever D098's successor scarcity is ruled —
        // the placer's room is the one shared thing a conception still competes for.

        // ---------------------------------------------------------------------------------
        // 4. The knob is a tunable like every other.
        // ---------------------------------------------------------------------------------

        [Theory]
        [InlineData(ConceptionOrder.Shuffled)]
        [InlineData(ConceptionOrder.Reserve)]
        public void TheOrderReachesTheHashAndTheFileByName(ConceptionOrder order)
        {
            // The two reflection guards cover this generically; these three assertions say what
            // the generic ones mean for an enum, which is the first scalar enum on RunConfig and
            // therefore the first knob whose file value is a word rather than a number. Every
            // member but the default is walked, because a member that reached neither the hash nor
            // the file would be a run filed under a world it did not have.
            var age = new RunConfig();
            var named = new RunConfig { ConceptionOrder = order };

            Assert.NotEqual(age.Hash(), named.Hash());

            string text = RunConfigJson.Write(named);
            _output.WriteLine(Line(text, "conceptionOrder"));

            Assert.Contains("\"conceptionOrder\": \"" + order + "\"", text);

            RunConfig back = RunConfigJson.Read(text, out string mismatch);
            Assert.Null(mismatch);
            Assert.Equal(order, back.ConceptionOrder);
            Assert.Equal(named.Hash(), back.Hash());
        }

        [Fact]
        public void AnUnknownOrderIsRefusedOnLoad()
        {
            // §9's rule, which matters more here than for a number: an order the file names and
            // the run did not walk is a run filed under settings it never had, and nothing
            // downstream could notice.
            string text = RunConfigJson.Write(new RunConfig())
                .Replace("\"conceptionOrder\": \"Age\"", "\"conceptionOrder\": \"Oldest\"");

            FormatException e = Assert.Throws<FormatException>(() => RunConfigJson.Read(text));

            _output.WriteLine(e.Message);
            Assert.Contains("Oldest", e.Message);
            Assert.Contains("Shuffled", e.Message);
            Assert.Contains("Reserve", e.Message);
        }

        // ---------------------------------------------------------------------------------
        // Comparing two worlds.
        // ---------------------------------------------------------------------------------

        /// <summary>The reference world for the replay tests: the defaults, with the knob set.</summary>
        private static RunConfig Reference(ConceptionOrder? order = null)
        {
            var config = new RunConfig();
            if (order.HasValue) config.ConceptionOrder = order.Value;
            return config;
        }

        private static string LivingIds(World world)
        {
            var ids = new List<string>(world.Living.Count);
            foreach (Organism creature in world.Living) ids.Add(creature.Id.ToString());
            return string.Join(",", ids);
        }

        private static string Describe(string label, World world) =>
            FormattableString.Invariant(
                $"{label}: alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}, ") +
            FormattableString.Invariant(
                $"floor {world.FloorSpawns}, standing {world.StandingJoules:0.######} J, ") +
            FormattableString.Invariant(
                $"in {world.EnergyIn:0.######} J, out {world.EnergyOut:0.######} J, ") +
            FormattableString.Invariant(
                $"matter {world.StandingMatterUnits:0.######}");

        private static void AssertSameWorld(World a, World b)
        {
            Assert.Equal(a.Births, b.Births);
            Assert.Equal(a.Deaths, b.Deaths);
            Assert.Equal(a.FloorSpawns, b.FloorSpawns);
            Assert.Equal(a.Stillbirths, b.Stillbirths);

            Assert.Equal(a.EnergyIn, b.EnergyIn);
            Assert.Equal(a.EnergyOut, b.EnergyOut);
            Assert.Equal(a.StandingJoules, b.StandingJoules);
            Assert.Equal(a.StandingMatterUnits, b.StandingMatterUnits);

            // Last and most specific: the same creatures, in the same places in the list. Two
            // walks that breed the same number of times can still breed different bodies, and this
            // is the assertion that can tell.
            Assert.Equal(LivingIds(a), LivingIds(b));
        }

        private static string Line(string text, string key)
        {
            foreach (string line in text.Split('\n'))
            {
                if (line.Contains(key)) return line.Trim();
            }

            return $"({key} not in the file)";
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="World.WitherForTank"/> and <see cref="DeathCause.Withered"/>: the nursery's switching leaf tank (D236) ends a leaf that nothing
    /// killed, and it must leave no food and no hole in the books, with contact eating on or off. Driven by hand, as
    /// <see cref="ContactEatingTests"/> is: Core has no physics.
    /// </summary>
    public class WitherForTankTests
    {
        private readonly ITestOutputHelper _output;

        public WitherForTankTests(ITestOutputHelper output) => _output = output;

        /// <summary>A bright world with a corpse decay that would found a corpse at an ordinary death, and the dials a case names.</summary>
        private static RunConfig Stage(bool contactEating, bool unbittenLeavesNothing, bool plantsKeep = false)
        {
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(400f, 12f),
                InitialMatterPerCubicMetre = 50f,
                PerOffspringOverheadJoules = 1e9f,
                HealthPerCubicMetre = 100f,
                CorpseDecayPerSecond = 0.0001f,
                ContactEating = contactEating,
                UnbittenDeathsLeaveNoCorpse = unbittenLeavesNothing,
                PlantsKeepMatter = plantsKeep,
            };
            foreach (var property in typeof(MutationRates).GetProperties())
                if (property.PropertyType == typeof(float) && property.Name.EndsWith("Chance")) property.SetValue(config.Mutation, 0f);
            return config;
        }

        private static void AssertBooksClose(World world, string where)
        {
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"{where}: audit residual {world.AuditResidual:R} against {world.StandingJoules:R} J standing");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"{where}: matter residual {world.MatterResidual:R} against {world.MatterInitialTotal:R} units");
        }

        private static World Populated(RunConfig config, int leaves, int steps)
        {
            var world = new World(config, seed: 5);
            for (int i = 0; i < leaves; i++) world.Inoculate(Fixtures.MouthSpine(2 + i, 0.15f + 0.03f * i), 1, -1f);
            for (int s = 0; s < steps; s++) world.Step(0.5f);
            return world;
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void AWitheredBodyLeavesNoCorpseAndNoRemainsAndTheBooksClose(bool contactEating, bool unbittenLeavesNothing)
        {
            World world = Populated(Stage(contactEating, unbittenLeavesNothing), leaves: 3, steps: 10);
            Assert.Equal(3, world.Living.Count);
            Organism victim = world.Living[1];
            double joules = victim.TissueJoules + victim.Energy + victim.GestationJoules;
            Assert.True(joules > 0d, "the fixture's leaf holds nothing to burn");

            double energyOut = world.EnergyOut, burnt = world.BurntTotal, matter = world.Matter.TotalJoules;
            long deaths = world.Deaths, diverged = world.Diverged;
            world.DrainLineageEvents();

            world.WitherForTank(victim);

            _output.WriteLine($"contact {contactEating}, unbitten {unbittenLeavesNothing}: burnt {joules:R} J of #{victim.Id}");
            Assert.DoesNotContain(victim, world.Living);
            Assert.Equal(2, world.Living.Count);
            Assert.Empty(world.Corpses);
            Assert.Empty(world.Remains);
            Assert.Empty(world.DrainRemainsFoundings());
            Assert.Equal(deaths + 1, world.Deaths);
            Assert.Equal(diverged, world.Diverged);
            Assert.Equal(0d, victim.TissueJoules);
            Assert.Equal(0d, victim.Energy);
            Assert.Equal(joules, world.EnergyOut - energyOut, 6);
            Assert.Equal(joules / world.Config.JoulesPerUnit, world.BurntTotal - burnt, 6);
            Assert.Equal(joules / world.Config.JoulesPerUnit, world.Matter.TotalJoules - matter, 6);

            LineageEvent death = world.DrainLineageEvents().Single(e => e.Kind == LineageEventKind.Death);
            Assert.Equal(victim.Id, death.Id);
            Assert.Equal(DeathCause.Withered, death.Cause);

            AssertBooksClose(world, "just after the wither");
            for (int s = 0; s < 40; s++) world.Step(0.5f);
            Assert.Empty(world.Corpses);
            Assert.Empty(world.Remains);
            AssertBooksClose(world, "forty steps after the wither");
        }

        [Fact]
        public void AnOrdinaryDeathOfTheSameBodyDoesLeaveFoodSoTheTestAboveCanFail()
        {
            // The control: with contact eating off and the unbitten rule off, a diverged leaf founds a corpse, so "no corpse" above is the wither's doing.
            World world = Populated(Stage(contactEating: false, unbittenLeavesNothing: false), leaves: 3, steps: 10);
            world.KillDiverged(world.Living[1]);
            Assert.Single(world.Corpses);
        }

        [Fact]
        public void APlantsHeldUnitsGoToTheSpentFieldAsADeathWithoutACorpseSendsThem()
        {
            World world = Populated(Stage(contactEating: false, unbittenLeavesNothing: false, plantsKeep: true), leaves: 2, steps: 40);
            Organism victim = world.Living[0];
            double held = victim.SpentUnits;
            Assert.True(held > 0d, "the fixture's leaf holds no spent units, so the rule is not exercised");

            double released = world.HeldReleasedTotal;
            world.WitherForTank(victim);

            Assert.Equal(held, world.HeldReleasedTotal - released, 9);
            Assert.Equal(0d, victim.SpentUnits);
            Assert.Empty(world.Corpses);
            AssertBooksClose(world, "after a plant that held units withered");
            for (int s = 0; s < 40; s++) world.Step(0.5f);
            AssertBooksClose(world, "forty steps on");
        }

        [Fact]
        public void ABodyThatIsNotLivingOrIsNullIsRefused()
        {
            World world = Populated(Stage(false, false), leaves: 2, steps: 2);
            Organism victim = world.Living[0];
            world.WitherForTank(victim);
            Assert.Throws<ArgumentException>(() => world.WitherForTank(victim));
            Assert.Throws<ArgumentNullException>(() => world.WitherForTank(null));
        }

        [Fact]
        public void TheCauseIsWrittenByNameAndSurvivesACheckpoint()
        {
            string row = LineageEvent.Death(12.5d, id: 7, DeathCause.Withered).ToJson();
            _output.WriteLine(row);
            Assert.Contains("\"c\":\"withered\"", row);
            Assert.DoesNotContain("\"c\":4", row);

            // A death row still waiting to be drained is part of a checkpoint: it comes back with its cause.
            World world = Populated(Stage(true, false), leaves: 2, steps: 4);
            world.DrainLineageEvents();
            Organism victim = world.Living[0];
            world.WitherForTank(victim);

            byte[] state;
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    w.Flush();
                }
                state = buffer.ToArray();
            }
            var restored = new World(Stage(true, false), seed: 5);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                restored.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            LineageEvent death = restored.DrainLineageEvents().Single(e => e.Kind == LineageEventKind.Death);
            Assert.Equal(victim.Id, death.Id);
            Assert.Equal(DeathCause.Withered, death.Cause);
        }

        [Fact]
        public void EveryCauseHasALineageCodeAndAnUnknownOneIsRefusedNotLeftUnlabelled()
        {
            foreach (DeathCause cause in Enum.GetValues(typeof(DeathCause)))
                Assert.Contains("\"c\":\"", LineageEvent.Death(1d, id: 1, cause).ToJson());
            Assert.Throws<ArgumentOutOfRangeException>(() => LineageEvent.Death(1d, id: 1, (DeathCause)99).ToJson());
        }
    }
}

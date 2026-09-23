using System.Collections.Generic;
using Evosim.Core;
using Evosim.Farm;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D115 and D116 in the farm: the <c>extinct</c> ending under the trickle (the farm's half of
    /// <c>logbook/specs/founding-trickle-spec.md</c> §3 test 3), the two variables and the
    /// header's tokens. The ending is tested as the decision <see cref="Program.EndsExtinct"/>
    /// makes, on a Core world emptied by hand, rather than by a farm run.
    /// </summary>
    public class FoundingTrickleEndingTests
    {
        private static World Emptied(float rate)
        {
            var config = new RunConfig
            {
                Light = new LightModel(120f, 12f),
                MinimumPopulation = 20,
                FloorClosesAfterSeconds = 20f,
                FoundingTricklePerSecond = rate,
            };

            var world = new World(config, seed: 8);
            for (int i = 0; i < 60; i++) world.Step(0.5f);

            foreach (Organism c in new List<Organism>(world.Living)) world.KillDiverged(c);
            Assert.Empty(world.Living);
            return world;
        }

        [Fact]
        public void AnEmptyWorldUnderTheTrickleDoesNotEndExtinct()
        {
            Assert.False(Program.EndsExtinct(Emptied(1f / 30f)));
        }

        [Fact]
        public void AnEmptyWorldWithTheTrickleOffEndsExtinct()
        {
            Assert.True(Program.EndsExtinct(Emptied(0f)));
        }

        [Fact]
        public void ALivingWorldNeverEndsExtinct()
        {
            var world = new World(new RunConfig { Light = new LightModel(120f, 12f) }, seed: 2);
            world.Step(0.5f);

            Assert.NotEmpty(world.Living);
            Assert.False(Program.EndsExtinct(world));
        }

        /// <summary>
        /// Before the floor closes the trickle is not running, so an empty world there ends as it
        /// always did, whatever the rate.
        /// </summary>
        [Fact]
        public void BeforeTheFloorClosesTheOldRuleStands()
        {
            var config = new RunConfig
            {
                Light = new LightModel(120f, 12f),
                MinimumPopulation = 20,
                FloorClosesAfterSeconds = 1_000f,
                FoundingTricklePerSecond = 1f / 30f,
            };

            var world = new World(config, seed: 8);
            world.Step(0.5f);
            foreach (Organism c in new List<Organism>(world.Living)) world.KillDiverged(c);

            Assert.False(world.FoundersStillArriving);
            Assert.True(Program.EndsExtinct(world));
        }

        [Fact]
        public void TheTrickleIsReadAsANumberOrAsOneOverN()
        {
            var block = EnvBindingTests.Round42Seed1();

            block["EVOSIM_TRICKLE"] = "1/30";
            EnvSettings reciprocal = EnvBinding.Read(EnvBinding.Of(block));
            Assert.Equal(1f / 30f, reciprocal.Trickle);
            Assert.Equal(1f / 30f, EnvBinding.BuildConfig(reciprocal).FoundingTricklePerSecond);

            block["EVOSIM_TRICKLE"] = "0.05";
            Assert.Equal(0.05f, EnvBinding.Read(EnvBinding.Of(block)).Trickle);

            block["EVOSIM_TRICKLE"] = "one per thirty";
            Assert.Throws<System.ArgumentException>(() => EnvBinding.Read(EnvBinding.Of(block)));

            block.Remove("EVOSIM_TRICKLE");
            Assert.Equal(0f, EnvBinding.Read(EnvBinding.Of(block)).Trickle);
        }

        [Fact]
        public void TheFollowFoodFlagReachesTheConfig()
        {
            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_FOUNDERS_FOLLOW_FOOD"] = "1";

            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            Assert.True(settings.FoundersFollowFood);
            Assert.True(EnvBinding.BuildConfig(settings).FoundersFollowFood);
        }

        [Fact]
        public void TheTrickleTokenReadsAsTheRate()
        {
            Assert.Equal("off", Report.TrickleToken(0f));
            Assert.Equal("1/30 s", Report.TrickleToken(1f / 30f));
            Assert.Equal("1/5 s", Report.TrickleToken(0.2f));
            Assert.Equal("1/1 s", Report.TrickleToken(1f));
            Assert.Equal("0.3/s", Report.TrickleToken(0.3f));
        }

        [Fact]
        public void TheHeaderNamesBothRules()
        {
            var block = EnvBindingTests.Round42Seed1();
            block["EVOSIM_TRICKLE"] = "1/30";
            block["EVOSIM_FOUNDERS_FOLLOW_FOOD"] = "1";

            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(block));
            RunConfig config = EnvBinding.BuildConfig(settings);
            var world = new World(config, settings.Seed);

            SpaceFacts space = SpaceFacts.Of(
                world,
                hasWall: config.SharedSpace && config.WorldShape == WorldShape.Tank,
                hasFloor: config.SharedSpace,
                threads: 4,
                inoculumHashShort: null);

            string line = Report.HeaderLine(settings, config, space, "9.9.9.9");

            Assert.Contains(" · founders in their food · ", line);
            Assert.Contains(" · floor closes 3000 s · trickle 1/30 s · ceiling ", line);
        }

        [Fact]
        public void TheTableCarriesTheTrickleColumnBeforeMaxReach()
        {
            // The trickle column was the last until round 46's `max reach` was appended after it.
            Assert.Equal("**trickle**", Report.BaseColumns[Report.BaseColumns.Length - 2]);
            Assert.Equal("max reach", Report.BaseColumns[Report.BaseColumns.Length - 1]);
        }
    }
}

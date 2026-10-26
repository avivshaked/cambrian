using System;
using System.Collections.Generic;
using System.Globalization;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D241's five launcher names reach the config and the header, unset they are the recorded world and the header
    /// says nothing new, and the climb is a function of world seconds alone: the same world at pace factor 1 and 3
    /// has a body the same size at the same world second, while the allowance is what binds.
    /// </summary>
    /// <remarks>Written without being run; the caller runs it in a test window.</remarks>
    public sealed class GrowthBindingTests
    {
        private readonly ITestOutputHelper _out;

        public GrowthBindingTests(ITestOutputHelper output) => _out = output;

        private static RunConfig Built(Dictionary<string, string> env) =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(env)));

        private static string Header(Dictionary<string, string> env)
        {
            EnvSettings settings = EnvBinding.Read(EnvBinding.Of(env));
            RunConfig config = EnvBinding.BuildConfig(settings);
            var world = new World(config, settings.Seed);
            SpaceFacts space = SpaceFacts.Of(world, hasWall: true, hasFloor: true, threads: 1, inoculumHashShort: null);
            return Report.HeaderLine(settings, config, space, "9.9.9.9");
        }

        [Fact]
        public void UnsetTheWorldHasNoClimbAndTheHeaderSaysNothingNew()
        {
            var env = new Dictionary<string, string>();
            RunConfig config = Built(env);
            var defaults = new RunConfig();

            Assert.Equal(0f, config.GrowthAdultSeconds);
            Assert.Equal(defaults.GrowthReserveStartSeconds, config.GrowthReserveStartSeconds);
            Assert.Equal(defaults.GrowthReserveMinSeconds, config.GrowthReserveMinSeconds);
            Assert.Equal(defaults.GrowthReserveMaxSeconds, config.GrowthReserveMaxSeconds);
            Assert.Equal(0f, config.Mutation.GrowthReserveChance);

            Assert.DoesNotContain(" · climb ", Header(env));
        }

        [Fact]
        public void TheFiveReachTheConfigAndTheHeaderToken()
        {
            var env = new Dictionary<string, string>
            {
                { "EVOSIM_GROWTH_ADULT", "120" },
                { "EVOSIM_GROWTH_RESERVE_START", "45" },
                { "EVOSIM_GROWTH_RESERVE_MIN", "2" },
                { "EVOSIM_GROWTH_RESERVE_MAX", "200" },
                { "EVOSIM_GROWTH_RESERVE_MUT", "0.05" },
            };
            RunConfig config = Built(env);

            Assert.Equal(120f, config.GrowthAdultSeconds);
            Assert.Equal(45f, config.GrowthReserveStartSeconds);
            Assert.Equal(2f, config.GrowthReserveMinSeconds);
            Assert.Equal(200f, config.GrowthReserveMaxSeconds);
            Assert.Equal(0.05f, config.Mutation.GrowthReserveChance);
            Assert.NotEqual(Built(new Dictionary<string, string>()).Hash(), config.Hash());

            CellType consumer = config.CellTypes.Resolve(CellTypeIds.Consumer);
            string clocks = (120d / CellType.MetabolicClockSeconds(consumer)).ToString("0.00", CultureInfo.InvariantCulture);

            string header = Header(env);
            _out.WriteLine(header);

            Assert.Contains(" · climb T 120 s (" + clocks + " consumer clocks) reserve 45 s [2-200] mut 0.05", header);

            // Before the hash, as every token of the settings line goes.
            Assert.True(header.IndexOf(" · climb ", StringComparison.Ordinal) < header.IndexOf(" · configHash ", StringComparison.Ordinal));
        }

        [Fact]
        public void TheMutationChanceAloneNamesTheClimbInTheHeader()
        {
            var env = new Dictionary<string, string> { { "EVOSIM_GROWTH_RESERVE_MUT", "0.01" } };

            Assert.Contains(" · climb T 0 s (0.00 consumer clocks) reserve 30 s [1-300] mut 0.01", Header(env));
        }

        [Fact]
        public void AConfigTheClimbCannotRunIsRefusedWhenTheWorldIsBuilt()
        {
            RunConfig negative = Built(new Dictionary<string, string> { { "EVOSIM_GROWTH_ADULT", "-5" } });
            Assert.Throws<ArgumentException>(() => new World(negative, 1UL));

            RunConfig badRange = Built(new Dictionary<string, string>
            {
                { "EVOSIM_GROWTH_ADULT", "60" },
                { "EVOSIM_GROWTH_RESERVE_START", "400" },
            });
            Assert.Throws<ArgumentException>(() => new World(badRange, 1UL));
        }

        // ---------------------------------------------------------------- the pace test

        private const float Dt = 0.02f;

        private static (World world, Simulation sim) Build(float k, float t)
        {
            RunConfig config = RemainsFixtures.Config(contactEating: false);
            config.PaceFactor = k;
            config.GrowthAdultSeconds = t;

            // A reserve no climb can spend, so the allowance is what binds at every step.
            config.FounderEnergyJoules = 1e6f;

            var world = new World(config, 1UL);
            int steps = (int)Math.Round(0.5f * k / Dt);
            var sim = new Simulation(world, 1UL, Dt, steps, threads: 2, runDirectory: null);

            // A juvenile: born at 0.65 of its adult (0.8125 x the 0.8 the newborn keeps as body).
            Genome juvenile = RemainsFixtures.Claw();
            juvenile.Reproduction.BirthInvestment = 0.8125f;

            world.Inoculate(juvenile, count: 1, heightY: -2.5f);
            sim.SettleBeforeCheckpoint();
            return (world, sim);
        }

        private float BodyFractionAfterTwentyWorldSeconds(float k, float t, out double clock)
        {
            (World world, Simulation sim) = Build(k, t);
            using (sim)
            {
                Organism body = world.Living[0];
                Assert.InRange(body.BodyFraction, 0.64f, 0.66f);

                double started = world.ElapsedSeconds;
                int guard = 0;

                while (world.ElapsedSeconds - started < 20.0 - 1e-9)
                {
                    sim.Step();
                    Assert.True(++guard < 20_000, "twenty world seconds never passed");
                }

                clock = world.ElapsedSeconds - started;
                return body.BodyFraction;
            }
        }

        [Fact]
        public void TheClimbIsTheSameAtPaceFactorOneAndThreeWhileTheAllowanceBinds()
        {
            float one = BodyFractionAfterTwentyWorldSeconds(1f, 120f, out double clock1);
            float three = BodyFractionAfterTwentyWorldSeconds(3f, 120f, out double clock3);

            _out.WriteLine($"k=1: fraction {one:0.#######} at {clock1:0.###} world s; k=3: fraction {three:0.#######} at {clock3:0.###} world s");

            Assert.Equal(20.0, clock1, 6);
            Assert.Equal(20.0, clock3, 6);

            // The allowance binds: not yet adult, and well past the 0.65 it was born at.
            Assert.InRange(one, 0.70f, 0.80f);
            Assert.Equal((double)one, (double)three, 6);
        }
    }
}

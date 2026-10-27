using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D239's pace factor k on the farm side: the economy runs on the world's clock, so ten world
    /// seconds cost the same at any k, and a harness is refused a step count that does not hold
    /// 0.5·k physics seconds.
    /// </summary>
    public sealed class PaceFactorTests
    {
        private readonly ITestOutputHelper _out;

        public PaceFactorTests(ITestOutputHelper output) => _out = output;

        private const float Dt = 0.02f;

        private static (World world, Simulation sim) Build(float k)
        {
            RunConfig config = RemainsFixtures.Config(contactEating: false);
            config.PaceFactor = k;

            var world = new World(config, 1UL);
            int steps = (int)Math.Round(0.5f * k / Dt);
            var sim = new Simulation(world, 1UL, Dt, steps, threads: 2, runDirectory: null);

            world.Inoculate(RemainsFixtures.Claw(), count: 1, heightY: -2.5f);
            sim.SettleBeforeCheckpoint();
            return (world, sim);
        }

        private static double EnergyOutAfterTenWorldSeconds(float k, out double clock)
        {
            (World world, Simulation sim) = Build(k);
            using (sim)
            {
                double before = world.EnergyOut;
                double started = world.ElapsedSeconds;
                int guard = 0;

                while (world.ElapsedSeconds - started < 10.0 - 1e-9)
                {
                    sim.Step();
                    Assert.True(++guard < 10_000, "ten world seconds never passed");
                }

                clock = world.ElapsedSeconds - started;
                return world.EnergyOut - before;
            }
        }

        [Fact]
        public void TheStandingCostOfTenWorldSecondsIsTheSameAtOneAndAtThree()
        {
            double paid1 = EnergyOutAfterTenWorldSeconds(1f, out double clock1);
            double paid3 = EnergyOutAfterTenWorldSeconds(3f, out double clock3);

            _out.WriteLine($"k=1: {paid1:0.######} J over {clock1:0.###} world s; k=3: {paid3:0.######} J over {clock3:0.###} world s");

            Assert.Equal(10.0, clock1, 6);
            Assert.Equal(10.0, clock3, 6);
            Assert.True(paid1 > 0, "nothing was paid at k=1, so the comparison reads nothing");
            Assert.Equal(paid1, paid3, 6);
        }

        [Fact]
        public void AMetabolicStepThatDoesNotHoldHalfAWorldSecondOfPaceIsRefused()
        {
            RunConfig config = RemainsFixtures.Config(contactEating: false);
            config.PaceFactor = 3f;
            var world = new World(config, 1UL);

            // 25 steps of 0.02 s are 0.5 physics seconds, a sixth of what k = 3 needs.
            var e = Assert.Throws<ArgumentException>(() =>
                new Simulation(world, 1UL, Dt, 25, threads: 2, runDirectory: null));

            Assert.Contains("pace factor", e.Message);
        }

        [Fact]
        public void TheFactorIsPartOfTheConfigHash()
        {
            RunConfig one = RemainsFixtures.Config(contactEating: false);
            RunConfig three = RemainsFixtures.Config(contactEating: false);
            three.PaceFactor = 3f;

            Assert.NotEqual(one.Hash(), three.Hash());
        }
    }
}

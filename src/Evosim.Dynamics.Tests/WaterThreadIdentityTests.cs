using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The spike's identity check again, with round 42's water switched on — the one thing
    /// package C adds that could have broken it.
    /// </summary>
    /// <remarks>
    /// <b>The hazard is real and is the reason the sampling is pinned.</b>
    /// <see cref="CurrentField"/> memoises the instants a call touches, and a miss fills a slot,
    /// so two threads asking it for water at new clocks would race on one slot table.
    /// <c>DynamicsWorld.SampleWater</c> therefore pins the field at the step's clock, which
    /// fills the slots and makes every lookup a read, and then samples across the world's
    /// threads (it ran on one until 2026-09-23); this test is what says the arrangement holds.
    /// The two cases are D100's half-second hold, under which the root samples the water once
    /// a hold, and the per-link sampling every round from 43 runs, under which every link asks
    /// the field for its velocity and its analytic acceleration on every step — the case in
    /// which the acceleration's own phase, grouped differently from the sampler's, has to be
    /// answered from a slot of its own.
    /// </remarks>
    public sealed class WaterThreadIdentityTests
    {
        private readonly ITestOutputHelper _out;

        public WaterThreadIdentityTests(ITestOutputHelper output) => _out = output;

        [Theory]
        [InlineData(0.5)]
        [InlineData(0.0)]
        public void TheThreadCountDoesNotChangeATrajectoryInMovingWater(double waterHoldSeconds)
        {
            ulong one = Run(1, waterHoldSeconds, out DynamicsWorld world);
            ulong four = Run(4, waterHoldSeconds, out _);
            ulong sixteen = Run(16, waterHoldSeconds, out _);

            _out.WriteLine($"hold {waterHoldSeconds} s");
            _out.WriteLine($"digest  1 thread  {one:x16}");
            _out.WriteLine($"digest  4 threads {four:x16}");
            _out.WriteLine($"digest 16 threads {sixteen:x16}");

            Assert.Equal(one, four);
            Assert.Equal(one, sixteen);

            // A digest is equal for a dead world too, so say what was alive and whether the
            // water reached it — without this the test passes on a population the solver lost.
            int lost = world.NonFiniteBodies();
            double fastestWater = 0;
            double work = 0;

            for (int i = 0; i < world.Creatures.Count; i++)
            {
                Creature body = world.Creatures[i];
                work += body.MechanicalWorkJoules;
                double speed = Vec3.Read(body.Water, 0).Magnitude;
                if (speed > fastestWater) fastestWater = speed;
            }

            _out.WriteLine($"{world.Creatures.Count - lost} of {world.Creatures.Count} alive at 20 s; " +
                           $"fastest water at a root {fastestWater:0.#####} m/s; " +
                           $"mechanical work {work:0.###} J");

            Assert.Equal(0, lost);
            Assert.True(fastestWater > 0, "no current reached any body");
            Assert.True(work > 0, "nothing was driven");
        }

        /// <summary>
        /// 200 bodies in round 42's tank, stepped 2,000 times with the streams, D090's
        /// acceleration force and D100's half-second hold on.
        /// </summary>
        /// <remarks>
        /// <b>Creature-creature contact is off here and the omission is measured, not tidy.</b>
        /// At this packing the spike's contact spring throws the population: 196 of these 200
        /// bodies go non-finite inside half a second, with the current on or off and with the
        /// spike's own <see cref="ThreadIdentityTests"/> settings as well as the tank's. A digest
        /// is equal for a dead world too, so the identity claim would have been made about four
        /// survivors. The contact spring is package E's; what this test is for is the water, and
        /// it is made on a population that is entirely alive at the end.
        /// </remarks>
        private static ulong Run(int threads, double waterHoldSeconds, out DynamicsWorld built)
        {
            SolverConfig config = R42Tank.Config(0.01, waterHoldSeconds);
            config.CreatureContact = false;

            var world = new DynamicsWorld(config) { Threads = threads };
            var rng = new Rng(20260921);

            for (int i = 0; i < 200; i++)
            {
                int links = 2 + (i % 3);
                JointType joint = (i % 3) == 0 ? JointType.Hinge
                    : (i % 3) == 1 ? JointType.Universal
                    : JointType.Spherical;

                Genome genome = TestBodies.Chain(
                    links, joint, power: 8f + i % 5, hz: 0.5f + 0.1f * (i % 7));

                Creature body = TestBodies.Build(genome, config, i);

                // Packed into a few metres about the tank's AXIS — (R, R), not the origin. Round
                // the origin they are outside the disc, where VelocityAt reads the nearest water
                // it has and StreamsAccelerationAt does not clamp the same way: 196 of these 200
                // bodies were thrown non-finite inside twenty seconds when this said 0.
                body.PlaceAt(
                    R42Tank.At(
                        (rng.NextFloat() - 0.5) * 4.0,
                        -20.0 - rng.NextFloat() * 2.0,
                        (rng.NextFloat() - 0.5) * 4.0),
                    QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5),
                        rng.NextFloat() * 6.28));

                body.Patch = i % config.PatchCount;

                world.Add(body);
            }

            for (int step = 0; step < 2000; step++) world.Step();

            built = world;
            return world.Digest();
        }
    }
}

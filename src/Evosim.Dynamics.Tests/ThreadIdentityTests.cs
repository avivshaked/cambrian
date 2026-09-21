using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The spike spec's item 4: state digests equal at every thread count. A trajectory that
    /// depends on the partition is a world that cannot be replayed, which is what D078 costs
    /// the farm today.
    /// </summary>
    public sealed class ThreadIdentityTests
    {
        private readonly ITestOutputHelper _out;

        public ThreadIdentityTests(ITestOutputHelper output) => _out = output;

        [Fact]
        public void TheThreadCountDoesNotChangeTheTrajectory()
        {
            ulong one = Run(1);
            ulong four = Run(4);
            ulong sixteen = Run(16);

            _out.WriteLine($"digest  1 thread  {one:x16}");
            _out.WriteLine($"digest  4 threads {four:x16}");
            _out.WriteLine($"digest 16 threads {sixteen:x16}");

            Assert.Equal(one, four);
            Assert.Equal(one, sixteen);
        }

        /// <summary>
        /// 200 bodies packed tightly enough that the contact grid has work to do, stepped 2,000
        /// times with drag, weight, the bed and creature-creature pushes all on.
        /// </summary>
        private static ulong Run(int threads)
        {
            var config = new SolverConfig
            {
                StepSeconds = 0.01,
                Density = 1000,
                DragCoefficient = 1.5,
                PanelsPerAxis = 2,
                AddedMassCoefficient = 0.5,
                FluidAccelerationCoefficient = 1,
                TissueExcessDensity = 0.02,
                NeutralBodyVolume = 0.25,
                SurfaceRestoringFraction = 1,
                WorldDepthMetres = 15,
                TankRadiusMetres = 0,
                CreatureContact = true,
                FloorIsSolid = true,
            };

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

                // Packed into a few metres so the spheres overlap and contact fires.
                body.PlaceAt(
                    new Vec3(
                        (rng.NextFloat() - 0.5) * 4.0,
                        -3.0 - rng.NextFloat() * 2.0,
                        (rng.NextFloat() - 0.5) * 4.0),
                    QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5),
                        rng.NextFloat() * 6.28));

                world.Add(body);
            }

            for (int step = 0; step < 2000; step++) world.Step();

            return world.Digest();
        }
    }
}

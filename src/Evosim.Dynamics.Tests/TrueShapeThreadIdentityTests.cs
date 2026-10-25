using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D228: a step on the true shape gives the same bits at 1, 8 and 24 threads, as
    /// <see cref="ThreadIdentityTests"/> shows of the spheres. The bodies carry all three shapes, so the
    /// narrowphase runs on points, segments and rectangles and boxes, against one another, the bed and the glass.
    /// </summary>
    public sealed class TrueShapeThreadIdentityTests
    {
        private readonly ITestOutputHelper _out;

        public TrueShapeThreadIdentityTests(ITestOutputHelper output) => _out = output;

        [Fact]
        public void TheThreadCountDoesNotChangeTheTrajectoryOnTheTrueShape()
        {
            ulong one = Run(1, ContactShapeForm.TrueShape, out int alive, out int total, out long touches);
            ulong eight = Run(8, ContactShapeForm.TrueShape);
            ulong twentyFour = Run(24, ContactShapeForm.TrueShape);

            // The same world on the spheres' rule is another trajectory, so the rule did something.
            ulong spheres = Run(1, ContactShapeForm.Spheres);

            _out.WriteLine($"true shape  1 thread   {one:x16}");
            _out.WriteLine($"true shape  8 threads  {eight:x16}");
            _out.WriteLine($"true shape 24 threads  {twentyFour:x16}");
            _out.WriteLine($"spheres     1 thread   {spheres:x16}");
            _out.WriteLine($"alive at the end {alive} of {total}; link touches summed over the run's samples {touches}");

            Assert.Equal(one, eight);
            Assert.Equal(one, twentyFour);
            Assert.NotEqual(one, spheres);

            Assert.True(alive >= (total * 95) / 100, $"only {alive} of {total} bodies were still finite");
            Assert.True(touches > 0, "no pair of parts touched in the sampled steps: the test measures nothing");
        }

        private static ulong Run(int threads, ContactShapeForm form) => Run(threads, form, out _, out _, out _);

        private static ulong Run(int threads, ContactShapeForm form, out int alive, out int total, out long touches)
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
                ContactPerPart = true,
                ContactShape = form,
                ContactInstrument = true,
            };

            var world = new DynamicsWorld(config) { Threads = threads };
            var rng = new Rng(20261025);
            string[] shapes = { ShapeIds.Box, ShapeIds.Capsule, ShapeIds.Sphere };

            for (int i = 0; i < 120; i++)
            {
                int links = 2 + (i % 3);
                JointType joint = (i % 3) == 0 ? JointType.Hinge
                    : (i % 3) == 1 ? JointType.Universal
                    : JointType.Spherical;

                Genome genome = TestBodies.Chain(links, joint, power: 8f + i % 5, hz: 0.5f + 0.1f * (i % 7));
                for (int n = 0; n < genome.Nodes.Count; n++) genome.Nodes[n].ShapeId = shapes[(i + n) % 3];

                Creature body = TestBodies.Build(genome, config, i);

                // Packed into a few metres so the parts overlap and contact fires; low enough to reach the bed.
                body.PlaceAt(
                    new Vec3(
                        (rng.NextFloat() - 0.5) * 3.0,
                        -13.0 - rng.NextFloat() * 1.8,
                        (rng.NextFloat() - 0.5) * 3.0),
                    QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5),
                        rng.NextFloat() * 6.28));

                world.Add(body);
            }

            touches = 0;
            for (int step = 0; step < 600; step++)
            {
                world.Step();

                if (step % 50 == 49)
                {
                    foreach (Creature c in world.Creatures) touches += c.OverlapCount;
                }
            }

            total = world.Creatures.Count;
            alive = total - world.NonFiniteBodies();

            return world.Digest();
        }
    }
}

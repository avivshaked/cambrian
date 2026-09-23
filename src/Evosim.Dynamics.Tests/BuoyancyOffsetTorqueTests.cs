using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D111's torque, <c>logbook/specs/buoyancy-offset-spec.md</c> §4 tests 1, 2, 3 and 5: a leaf
    /// with gas on one face turns that face up, the net force is the offset-free body's to the
    /// bit, a sphere feels nothing, and the trajectory does not depend on the thread count.
    /// Test 4, the recorded solver at offset 0 over the crowd fixture's 3,000 s, and test 6, the
    /// screen, are runs and not unit tests.
    /// </summary>
    public sealed class BuoyancyOffsetTorqueTests
    {
        private readonly ITestOutputHelper _out;

        public BuoyancyOffsetTorqueTests(ITestOutputHelper output) => _out = output;

        /// <summary>Still water with drag and the offset's torque, and no net weight to sink by.</summary>
        private static SolverConfig Still(bool torque)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.BuoyancyOffsetTorque = torque;
            return config;
        }

        /// <summary>A half-metre leaf, 4.5 cm thick on y, floating from <paramref name="offset"/>.</summary>
        private static Genome Leaf(float offset, string shape = ShapeIds.Box)
        {
            Genome genome = TestBodies.Box(new Float3(0.5f, 0.045f, 0.5f));
            genome.Nodes[0].ShapeId = shape;
            genome.Nodes[0].BuoyancyOffset = offset;
            return genome;
        }

        /// <summary>The world's up read along the link's own y axis: 1 when +y points up.</summary>
        private static double UpAlongY(Creature body) => body.RotationMatrix[4];

        private static void ApplyOnly(Creature body, SolverConfig config)
        {
            Array.Clear(body.Fext, 0, body.Fext.Length);
            Fluid.Apply(body, config);
        }

        // ------------------------------------------------------------------ tests 1 and 2

        [Theory]
        [InlineData(0.5f, 1.0)]
        [InlineData(-0.5f, -1.0)]
        public void ALeafOnEdgeTurnsItsOffsetFaceUpAndTheNetForceDoesNotMove(float offset, double face)
        {
            SolverConfig on = Still(torque: true);
            SolverConfig off = Still(torque: false);

            Creature body = TestBodies.Build(Leaf(offset), on);

            // A quarter turn about x, and a little about z so the leaf is not balanced on the
            // unstable side: its thin axis is horizontal, along the world's z.
            QuatD start = QuatD.FromAxisAngle(new Vec3(1, 0, 0), Math.PI / 2) *
                          QuatD.FromAxisAngle(new Vec3(0, 0, 1), 0.01);
            body.PlaceAt(new Vec3(0, -20, 0), start);

            var world = new DynamicsWorld(on);
            world.Add(body);

            _out.WriteLine($"start: up along y {UpAlongY(body):0.####}, arm {body.BuoyancyArm[0]:0.#####} m, " +
                           $"axis {body.ThinAxis[0]}");
            Assert.Equal(1, body.ThinAxis[0]);
            Assert.True(Math.Abs(UpAlongY(body)) < 0.05, "the leaf did not start on edge");

            double within = Math.Cos(5.0 * Math.PI / 180.0);
            double firstWithin = -1, early = 0, late = 0;
            int steps = 1000; // 10 s at dt 0.01

            for (int step = 0; step < steps; step++)
            {
                // The net force with the torque on and off, at the pose this step starts from:
                // equal to the bit, since the weight still acts at the origin. The world re-applies
                // the fluid itself inside Step, and NoteDrag overwrites, so this changes nothing.
                ApplyOnly(body, off);
                double[] without = (double[])body.Fext.Clone();
                ApplyOnly(body, on);

                for (int k = 3; k < 6; k++)
                {
                    Assert.Equal(
                        BitConverter.DoubleToInt64Bits(without[k]),
                        BitConverter.DoubleToInt64Bits(body.Fext[k]));
                }

                world.Step();

                if (firstWithin < 0 && face * UpAlongY(body) >= within) firstWithin = (step + 1) * 0.01;

                double tilt = Math.Acos(Math.Max(-1.0, Math.Min(1.0, face * UpAlongY(body)))) * 180.0 / Math.PI;
                if (step >= 200 && step < 400) early = Math.Max(early, tilt);
                if (step >= 800) late = Math.Max(late, tilt);

                if ((step + 1) % 100 == 0)
                {
                    _out.WriteLine($"  t {(step + 1) * 0.01:0} s: offset face {tilt:0.###} degrees off vertical");
                }
            }

            _out.WriteLine($"offset {offset}: within 5 degrees first at {firstWithin:0.##} s; " +
                           $"largest tilt {early:0.##} degrees over 2-4 s and {late:0.##} over 8-10 s");

            // The spec's test 1 asks for the turn within 10 s, and it comes in under two. What it
            // does not get is rest: drag alone is a quadratic damper, weak at small angles, so the
            // leaf rocks about flat with a period near 4 s and a slowly shrinking swing. Asserted
            // as what it is, a swing that shrinks, rather than as a pose at one instant.
            Assert.True(firstWithin > 0 && firstWithin <= 10.0, "the leaf never came within 5 degrees of flat");
            Assert.True(late < early, $"the swing did not shrink: {early} then {late} degrees");
            Assert.True(late < 10.0, $"the leaf still rocked {late:0.##} degrees at 8-10 s");
        }

        [Fact]
        public void WithoutThePriceTheSameLeafStaysOnEdge()
        {
            // The switch reaches the solver: the same body, the torque off, turns nowhere.
            SolverConfig off = Still(torque: false);
            Creature body = TestBodies.Build(Leaf(0.5f), off);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.FromAxisAngle(new Vec3(1, 0, 0), Math.PI / 2));

            var world = new DynamicsWorld(off);
            world.Add(body);
            for (int step = 0; step < 300; step++) world.Step();

            Assert.True(Math.Abs(UpAlongY(body)) < 1e-6, $"up along y read {UpAlongY(body)}");
        }

        // ------------------------------------------------------------------ test 3

        [Fact]
        public void ASphereWithAnyOffsetReadsNoTorque()
        {
            SolverConfig on = Still(torque: true);
            SolverConfig off = Still(torque: false);

            foreach (float offset in new[] { -1f, -0.3f, 0.5f, 1f })
            {
                Creature body = TestBodies.Build(Leaf(offset, ShapeIds.Sphere), on);
                body.PlaceAt(new Vec3(0, -20, 0), QuatD.FromAxisAngle(new Vec3(1, 1, 0), 0.7));

                Assert.Equal(-1, body.ThinAxis[0]);
                Assert.Equal(0.0, body.BuoyancyArm[0]);

                ApplyOnly(body, off);
                double[] without = (double[])body.Fext.Clone();
                ApplyOnly(body, on);

                for (int k = 0; k < 6; k++)
                {
                    Assert.Equal(
                        BitConverter.DoubleToInt64Bits(without[k]),
                        BitConverter.DoubleToInt64Bits(body.Fext[k]));
                }
            }
        }

        [Fact]
        public void AboveTheWaterlineTheOffsetTurnsNothing()
        {
            // The clamp's share: a leaf held in the surface film is not spun by water it is not in.
            SolverConfig on = Still(torque: true);
            on.SurfaceRestoringFraction = 1;
            on.TissueExcessDensity = 0.02;
            SolverConfig off = Still(torque: false);
            off.SurfaceRestoringFraction = 1;
            off.TissueExcessDensity = 0.02;

            Creature body = TestBodies.Build(Leaf(0.5f), on);
            body.PlaceAt(new Vec3(0, 0.2, 0), QuatD.FromAxisAngle(new Vec3(1, 0, 0), Math.PI / 2));

            ApplyOnly(body, off);
            double[] without = (double[])body.Fext.Clone();
            ApplyOnly(body, on);

            for (int k = 0; k < 6; k++) Assert.Equal(without[k], body.Fext[k]);

            // And below it, the same leaf is turned.
            body.PlaceAt(new Vec3(0, -5, 0), QuatD.FromAxisAngle(new Vec3(1, 0, 0), Math.PI / 2));
            ApplyOnly(body, off);
            double torqueOff = Vec3.Read(body.Fext, 0).Magnitude;
            ApplyOnly(body, on);
            double torqueOn = Vec3.Read(body.Fext, 0).Magnitude;

            double expected = 1000.0 * body.Volume[0] * 9.81 * 0.5 * 0.045;
            _out.WriteLine($"righting moment {torqueOn:0.###} N·m against {expected:0.###} expected");
            Assert.Equal(0.0, torqueOff);
            Assert.InRange(torqueOn, expected * 0.999, expected * 1.001);
        }

        // ------------------------------------------------------------------ test 5

        /// <summary>
        /// The digest at 1 and 16 threads with the torque on and a third of the crowd floating off
        /// its centre. A synthetic crowd, <see cref="ThreadIdentityTests"/>' own, and not the
        /// recorded one (<see cref="RunFixture"/>): the fixture's genomes are format 7 and carry no
        /// offset, and its config predates the price, so this build refuses both.
        /// </summary>
        [Fact]
        public void TheThreadCountDoesNotChangeTheTrajectoryWithTheTorqueOn()
        {
            ulong one = Crowd(1, torque: true, out int alive, out int total);
            ulong sixteen = Crowd(16, torque: true, out _, out _);
            ulong without = Crowd(1, torque: false, out _, out _);

            _out.WriteLine($"torque on:  1 thread {one:x16}, 16 threads {sixteen:x16}");
            _out.WriteLine($"torque off: 1 thread {without:x16}");
            _out.WriteLine($"alive at the end {alive} of {total}");

            Assert.Equal(one, sixteen);
            Assert.NotEqual(one, without);
            Assert.True(alive >= (total * 95) / 100, $"only {alive} of {total} bodies were finite");
        }

        private static ulong Crowd(int threads, bool torque, out int alive, out int total)
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
                BuoyancyOffsetTorque = torque,
            };

            var world = new DynamicsWorld(config) { Threads = threads };
            var rng = new Rng(20260923);

            for (int i = 0; i < 120; i++)
            {
                int links = 2 + (i % 3);
                JointType joint = (i % 3) == 0 ? JointType.Hinge
                    : (i % 3) == 1 ? JointType.Universal
                    : JointType.Spherical;

                Genome genome = TestBodies.Chain(
                    links, joint, power: 8f + i % 5, hz: 0.5f + 0.1f * (i % 7));

                // A third of the crowd floats off its centre, on every node, both signs.
                if (i % 3 == 1)
                {
                    foreach (MorphNode node in genome.Nodes)
                    {
                        node.BuoyancyOffset = (i % 2 == 0 ? 0.6f : -0.4f);
                    }
                }

                Creature body = TestBodies.Build(genome, config, i);
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

            for (int step = 0; step < 1000; step++) world.Step();

            total = world.Creatures.Count;
            alive = total - world.NonFiniteBodies();
            return world.Digest();
        }
    }
}

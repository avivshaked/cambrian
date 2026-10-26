using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D239's pace factor k: the solver steps physics seconds, k of them to each world second.
    /// The clock the world keeps is the world's (dt / k a step), the current is pinned at it and
    /// reaches the solver divided by k (velocity) and k squared (acceleration), and at k = 1
    /// none of that arithmetic is entered.
    /// </summary>
    public sealed class PaceFactorTests
    {
        private readonly ITestOutputHelper _out;

        public PaceFactorTests(ITestOutputHelper output) => _out = output;

        [Fact]
        public void TheClockAtOneIsTheSumOfTheStepToTheBit()
        {
            SolverConfig config = TestBodies.Water(0.01);
            Assert.Equal(1.0, config.PaceFactor);
            Assert.Equal(config.StepSeconds, config.ClockStepSeconds);

            var world = new DynamicsWorld(config);
            AddBox(world, config);

            double sum = 0;
            for (int step = 0; step < 100; step++)
            {
                world.Step();
                sum += 0.01;
            }

            Assert.Equal(BitConverter.DoubleToInt64Bits(sum), BitConverter.DoubleToInt64Bits(world.ElapsedSeconds));
        }

        [Fact]
        public void TheClockAtThreeRunsInWorldSeconds()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.PaceFactor = 3.0;

            var world = new DynamicsWorld(config);
            AddBox(world, config);

            for (int step = 0; step < 300; step++) world.Step();

            // 300 steps of 0.01 physics seconds are 3 physics seconds, and one world second at k = 3.
            Assert.Equal(1.0, world.ElapsedSeconds, 9);
        }

        [Fact]
        public void AJointedBodyInStillWaterTravelsAboutKTimesAsFarPerWorldSecond()
        {
            double one = PathPerWorldSecond(1.0);
            double three = PathPerWorldSecond(3.0);
            double ratio = three / one;

            _out.WriteLine($"root path per world second: k=1 {one:0.####} m, k=3 {three:0.####} m, ratio {ratio:0.###}");

            Assert.True(one > 0.05, $"the chain barely moved at k=1 ({one} m per world second)");
            Assert.InRange(ratio, 2.4, 3.6);
        }

        /// <summary>The root's path length over the second half of 30 world seconds, per world second.</summary>
        private static double PathPerWorldSecond(double k)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = 0.3;
            config.PaceFactor = k;

            Creature body = TestBodies.Build(TestBodies.Chain(links: 2, power: 10f, hz: 1f), config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);

            var world = new DynamicsWorld(config);
            world.Add(body);

            const double Total = 30.0, Half = 15.0;
            double path = 0;
            double x = body.Position[0], y = body.Position[1], z = body.Position[2];

            while (world.ElapsedSeconds < Total - 1e-9)
            {
                world.Step();

                double nx = body.Position[0], ny = body.Position[1], nz = body.Position[2];
                if (world.ElapsedSeconds > Half)
                {
                    path += Math.Sqrt((nx - x) * (nx - x) + (ny - y) * (ny - y) + (nz - z) * (nz - z));
                }

                x = nx; y = ny; z = nz;
            }

            return path / (Total - Half);
        }

        /// <summary>
        /// A neutrally buoyant body released moving with the water goes where the water goes in
        /// world time, whatever k: the same tracer, integrated in world seconds, and the same end.
        /// </summary>
        [Fact]
        public void ARigidBodyInTheStreamsMovesTheSameDistancePerWorldSecond()
        {
            double errorOne = Follow(1.0, out Vec3 endOne, out double path);
            double errorThree = Follow(3.0, out Vec3 endThree, out _);
            double apart = (endOne - endThree).Magnitude;

            _out.WriteLine($"path {path:0.###} m in 200 world seconds; from the tracer: k=1 {errorOne:0.####} m, k=3 {errorThree:0.####} m; the two ends {apart:0.####} m apart");

            Assert.True(path > 5, $"the water barely moved the tracer ({path} m of path)");
            Assert.True(errorOne < 0.15, $"at k=1 the body parted from its parcel by {errorOne} m");
            Assert.True(errorThree < 0.15, $"at k=3 the body parted from its parcel by {errorThree} m");
            Assert.True(apart < 0.2, $"the two paces ended {apart} m apart");
        }

        private static double Follow(double k, out Vec3 end, out double pathLength)
        {
            SolverConfig config = R42Tank.Config(0.01, 0);
            config.PaceFactor = k;
            CurrentField current = config.Current;

            Creature body = TestBodies.Build(TestBodies.Box(new Float3(0.1f, 0.1f, 0.1f)), config);
            Vec3 start = R42Tank.At(3.0, -20.0, 2.0);
            body.PlaceAt(start, QuatD.Identity);

            // Released moving with the water: the solver's velocity is the world's over k.
            Float3 u0 = current.VelocityAt((float)start.X, (float)start.Y, (float)start.Z, 0d);
            Vec3.Write(body.Velocity, 0, new Vec3(u0.X / k, u0.Y / k, u0.Z / k));

            var world = new DynamicsWorld(config);
            world.Add(body);

            Vec3 tracer = start;
            double clockStep = 0.01 / k;
            double clock = 0;
            pathLength = 0;

            while (clock < 200.0 - 1e-9)
            {
                Float3 u = current.VelocityAt((float)tracer.X, (float)tracer.Y, (float)tracer.Z, clock);
                var move = new Vec3(u.X, u.Y, u.Z) * clockStep;
                tracer += move;
                pathLength += move.Magnitude;

                world.Step();
                clock += clockStep;
            }

            end = Vec3.Read(body.Position, 0);
            return (end - tracer).Magnitude;
        }

        private static void AddBox(DynamicsWorld world, SolverConfig config)
        {
            Creature body = TestBodies.Build(TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f)), config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            world.Add(body);
        }
    }
}

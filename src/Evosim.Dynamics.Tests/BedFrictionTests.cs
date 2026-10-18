using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D137, the owner's ruling of 2026-09-30: sliding friction where a part touches the rock or
    /// the glass, against the sliding velocity, at most μ times the push, never reversing a slide.
    /// </summary>
    public sealed class BedFrictionTests
    {
        private readonly ITestOutputHelper _out;

        public BedFrictionTests(ITestOutputHelper output) => _out = output;

        private const double Depth = 5.0;

        /// <summary>
        /// Still water over a flat bed at -5 m. <paramref name="excess"/> is the tissue's excess
        /// density in kg/m3: the rounds run 0.02 (a resting body weighs 2e-5 of its mass), and the
        /// heavy box here is 200, a fifth of its mass, so that the law is read on a body the bed
        /// actually holds up.
        /// </summary>
        private static SolverConfig Bed(double friction, double excess, bool perPart)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.WorldDepthMetres = Depth;
            config.FloorIsSolid = true;
            config.TissueExcessDensity = excess;
            config.NeutralBodyVolume = 0;
            config.ContactPerPart = perPart;
            config.BedFriction = friction;
            return config;
        }

        private static (DynamicsWorld, Creature) Box(SolverConfig config, double height)
        {
            var world = new DynamicsWorld(config);
            Creature body = TestBodies.Build(TestBodies.Box(new Float3(0.1f, 0.1f, 0.1f)), config);
            body.PlaceAt(new Vec3(0, height, 0), QuatD.Identity);
            world.Add(body);
            return (world, body);
        }

        private static Vec3 VelocityOf(Creature body) => Vec3.Read(body.Velocity, 0);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AHeavyBoxSlidingOnTheBedStopsWithFrictionAndCoastsWithout(bool perPart)
        {
            (double speed, double travel) Slide(double mu)
            {
                SolverConfig config = Bed(mu, 200, perPart);
                (DynamicsWorld world, Creature body) = Box(config, -Depth + 0.17);
                for (int i = 0; i < 200; i++) world.Step();      // settle on the bed
                double x0 = body.Position[0];
                Vec3.Write(body.Velocity, 0, new Vec3(0.3, 0, 0));
                for (int i = 0; i < 300; i++) world.Step();
                return (VelocityOf(body).Magnitude, body.Position[0] - x0);
            }

            (double still, double short_) = Slide(0.5);
            (double coasting, double far) = Slide(0);

            // Deceleration μ g (excess / tissue) = 0.5 x 9.81 x 0.2 = 0.98 m/s2, so 0.3 m/s stops in
            // about 0.3 s over about 5 cm, less what the drag takes.
            _out.WriteLine($"per part {perPart}: μ 0.5 -> {still * 100:0.###} cm/s after 3 s, slid {short_ * 100:0.#} cm; μ 0 -> {coasting * 100:0.#} cm/s, slid {far * 100:0.#} cm");
            Assert.True(still < 0.002, $"with friction the box still moves at {still} m/s");
            Assert.InRange(short_, 0.01, 0.06);
            Assert.True(coasting > 0.05, $"without friction the box slowed to {coasting} m/s");
            Assert.True(far > 0.3, $"without friction the box slid only {far} m");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AWeightlessBoxStrikingTheBedAtAnAngleLosesSomeOfItsSlideAndNeverReversesIt(bool perPart)
        {
            double After(double mu)
            {
                SolverConfig config = Bed(mu, 0, perPart);
                (DynamicsWorld world, Creature body) = Box(config, -Depth + 0.3);
                Vec3.Write(body.Velocity, 0, new Vec3(0.3, -0.3, 0));
                for (int i = 0; i < 100; i++) world.Step();
                return VelocityOf(body).X;
            }

            double rough = After(0.5), smooth = After(0);
            _out.WriteLine($"per part {perPart}: along the bed after the strike, μ 0.5 {rough * 100:0.##} cm/s, μ 0 {smooth * 100:0.##} cm/s");

            // The strike's push stops 0.3 m/s of approach; Coulomb's bound on what it takes from the
            // slide is μ times that, 0.15 m/s, and a little more if the spring throws the box back.
            Assert.True(rough < smooth - 0.02, $"friction took only {smooth - rough} m/s from the slide");
            Assert.True(rough > smooth - 0.5 * 0.3 * 2.0 - 1e-9, $"friction took {smooth - rough} m/s, past Coulomb's bound");
            Assert.True(rough >= 0, $"the slide reversed: {rough} m/s");
        }

        /// <summary>
        /// The owner's point that creatures must not swim into the ground: a two-link chain at full power under
        /// D135's drive, pointed straight down at the bed for ten seconds. The bed's push is a stiff
        /// spring (400 times the part's mass per metre, critically damped): the strike, arriving at
        /// the chain's swimming speed, overlaps the rock by a few millimetres, and the steady press
        /// after it by far less.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        public void ABodySwimmingStraightAtTheBedAtFullPowerDoesNotSinkIntoIt(double mu)
        {
            SolverConfig config = Bed(mu, 0, perPart: true);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = 0.3;
            config.PowerPushRadiansPerSecond = 6.0;
            config.PoseHertz = 2.0;

            Genome g = TestBodies.Chain(links: 2, power: 10f);
            g.Nodes[1].Neurons = new[]
            {
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 1f, Inputs = Array.Empty<NeuronInput>() },
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f, Inputs = Array.Empty<NeuronInput>() },
            };
            g.Nodes[1].Drive = new DriveGenes(0f, 0.05f, 0.05f, 0.05f);

            var world = new DynamicsWorld(config);
            Creature body = TestBodies.Build(g, config);

            // The chain swims toward its head, -x; turned a quarter about z, its head points down.
            body.PlaceAt(new Vec3(0, -Depth + 0.6, 0), QuatD.FromAxisAngle(new Vec3(0, 0, 1), Math.PI / 2));
            world.Add(body);

            double deepest = double.NegativeInfinity, pressing = double.NegativeInfinity;
            for (int step = 0; step < 1000; step++)
            {
                world.Step();
                for (int i = 0; i < body.Links; i++)
                {
                    double into = -Depth - (body.Position[3 * i + 1] - body.LinkContactRadius[i]);
                    if (into > deepest) deepest = into;
                    if (step >= 500 && into > pressing) pressing = into;
                }
            }

            _out.WriteLine($"μ {mu}: deepest past the bed {deepest * 1000:0.###} mm at any time, {pressing * 1000:0.###} mm in the last 5 s of pressing");
            Assert.True(pressing > 0, $"the chain was not on the bed at the end ({pressing} m)");
            Assert.True(deepest < 0.01, $"the strike took the chain {deepest * 1000} mm into the bed");
            Assert.True(pressing < 0.001, $"the chain pressed {pressing * 1000} mm into the bed");
        }

        [Fact]
        public void FrictionIsZeroWithoutAPushOrWithoutASlide()
        {
            SolverConfig config = Bed(0.5, 0, false);
            Assert.Equal(0.0, ContactLaw.Friction(Vec3.Zero, new Vec3(1, 0, 0), 1, config).Magnitude);
            Assert.Equal(0.0, ContactLaw.Friction(new Vec3(0, 3, 0), new Vec3(0, -2, 0), 1, config).Magnitude, 12);

            Vec3 f = ContactLaw.Friction(new Vec3(0, 2, 0), new Vec3(1, 0, 0), 100, config);
            Assert.Equal(-1.0, f.X, 12);   // μ |push| = 0.5 x 2 N, against the slide
            Assert.Equal(0.0, f.Y, 12);

            // A light part: the cap stops the slide in one step and no more.
            Vec3 capped = ContactLaw.Friction(new Vec3(0, 1000, 0), new Vec3(0.01, 0, 0), 1, config);
            Assert.Equal(-1.0 * 0.01 / config.StepSeconds, capped.X, 9);
        }
    }
}
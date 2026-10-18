using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// propose-joint-drive.md: each degree of freedom takes a power and a bend; the power passes the
    /// joint's threshold and envelope and pushes the link as a stroke at s·Ω would; the bend is held
    /// by a muscle capped by the part's Power.
    /// </summary>
    public sealed class BendAndPowerTests
    {
        private readonly ITestOutputHelper _out;

        public BendAndPowerTests(ITestOutputHelper output) => _out = output;

        private const double Omega = 6.0;

        private static SolverConfig Water(double push = 0.3, double omega = Omega, double poseHz = 2.0)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = push;
            config.PowerPushRadiansPerSecond = omega;
            config.PoseHertz = poseHz;
            return config;
        }

        /// <summary>A two-link chain whose link's brain holds a constant power and bend.</summary>
        private static Creature Chain(SolverConfig config, float power, float bend, DriveGenes? genes = null)
        {
            Genome g = TestBodies.Chain(links: 2, power: 10f);
            MorphNode link = g.Nodes[1];
            link.Neurons = new[]
            {
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = power, Inputs = Array.Empty<NeuronInput>() },
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = bend, Inputs = Array.Empty<NeuronInput>() },
            };
            if (genes.HasValue) link.Drive = genes.Value;
            Creature body = TestBodies.Build(g, config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            return body;
        }

        private static Vec3 ForceOn(Creature body, SolverConfig config, int link)
        {
            Array.Clear(body.Fext, 0, body.Fext.Length);
            Fluid.Apply(body, config);
            return Vec3.Read(body.Fext, 6 * link + 3);
        }

        [Fact]
        public void APoweredLinkIsPushedTowardItsJointAsAStrokeAtSOmegaWouldBe()
        {
            SolverConfig config = Water();
            Creature body = Chain(config, 0f, 0f);
            Assert.True(body.Drive.BendAndPower);

            Vec3 still = ForceOn(body, config, 1);
            body.Drive.Stroke[0] = 0.5;
            Vec3 push = ForceOn(body, config, 1) - still;

            // The hinge turns about an axis across the link, so the whole of s·Ω is a swing.
            double addedMass = 0.5 * 1000 * body.Volume[1];
            double expected = 0.3 * addedMass * 0.2 * (0.5 * Omega) * (0.5 * Omega);
            _out.WriteLine($"push ({push.X:0.#####}, {push.Y:0.#####}, {push.Z:0.#####}) N, expected {-expected:0.#####} along x");

            Assert.InRange(push.X, -expected * (1 + 1e-6), -expected * (1 - 1e-6));
            Assert.InRange(push.Y, -1e-9, 1e-9);
            Assert.InRange(push.Z, -1e-9, 1e-9);
            Assert.Equal(0.0, (ForceOn(body, config, 0) - ForceOn(body, Water(0.3, 0), 0)).Magnitude, 12);
        }

        [Fact]
        public void NoPowerAndNoPushCoefficientPushNothing()
        {
            SolverConfig config = Water();
            Creature body = Chain(config, 0f, 0f);
            Vec3 still = ForceOn(body, config, 1);
            body.Drive.Stroke[0] = 0;
            Assert.Equal(0.0, (ForceOn(body, config, 1) - still).Magnitude, 12);

            SolverConfig noPush = Water(push: 0);
            Creature other = Chain(noPush, 0f, 0f);
            Vec3 before = ForceOn(other, noPush, 1);
            other.Drive.Stroke[0] = 1;
            Assert.Equal(0.0, (ForceOn(other, noPush, 1) - before).Magnitude, 12);
        }

        [Fact]
        public void UnderTheBendAndPowerDriveARealSwingPushesNothing()
        {
            SolverConfig config = Water();
            Creature body = Chain(config, 0f, 0f);
            Vec3 still = ForceOn(body, config, 1);
            Vec3.Write(body.Spin, 3, new Vec3(0, 2, 0));
            Vec3 swinging = ForceOn(body, config, 1);

            // Drag on the swinging link changes the force; the push must not. So compare with the
            // same swing in a world with no push coefficient at all.
            SolverConfig dragOnly = Water(push: 0);
            Creature other = Chain(dragOnly, 0f, 0f);
            Vec3.Write(other.Spin, 3, new Vec3(0, 2, 0));
            Vec3 drag = ForceOn(other, dragOnly, 1);
            _out.WriteLine($"swinging, powered world ({swinging.X:0.####}, {swinging.Y:0.####}, {swinging.Z:0.####}) N; drag alone ({drag.X:0.####}, {drag.Y:0.####}, {drag.Z:0.####}) N; still ({still.X:0.####}) N");
            Assert.Equal(0.0, (swinging - drag).Magnitude, 9);
        }

        [Fact]
        public void ASilentOrNegativeNeuronIsNoPower()
        {
            // The owner's design (D135, "0 or threshold"): the power is the neuron's positive half.
            SolverConfig config = Water();
            Creature body = Chain(config, 0f, 0f, new DriveGenes(0f, 0.02f, 0.02f, 0.02f));
            for (int i = 0; i < 5; i++) body.Drive.Drive(new[] { 0f }, new[] { 0f });
            Assert.Equal(0.0, body.Drive.Stroke[0]);
            for (int i = 0; i < 5; i++) body.Drive.Drive(new[] { -0.7f }, new[] { 0f });
            Assert.Equal(0.0, body.Drive.Stroke[0]);
            for (int i = 0; i < 5; i++) body.Drive.Drive(new[] { 0.5f }, new[] { 0f });
            Assert.Equal(0.5, body.Drive.Stroke[0], 6);
        }

        [Fact]
        public void TheTorqueDriveIsWhatItWasWhenOmegaIsZero()
        {
            Creature body = Chain(Water(omega: 0), 1f, 1f);
            Assert.False(body.Drive.BendAndPower);
        }

        [Fact]
        public void BelowItsThresholdAJointGetsNoPower()
        {
            SolverConfig config = Water();
            var genes = new DriveGenes(0.2f, 0.02f, 0.02f, 0.02f);
            Creature body = Chain(config, 0f, 0f, genes);

            // A raw 0.2 is at the threshold; a raw 0.6 is half the way from the threshold to 1.
            body.Drive.Drive(new[] { 0.2f }, new[] { 0f });
            Assert.Equal(0.0, body.Drive.Stroke[0]);

            for (int i = 0; i < 10; i++) body.Drive.Drive(new[] { 0.6f }, new[] { 0f });
            Assert.Equal(0.5, body.Drive.Stroke[0], 6);
        }

        [Fact]
        public void TheEnvelopeRampsHoldsAndReleases()
        {
            SolverConfig config = Water();
            var genes = new DriveGenes(0f, 0.2f, 0.5f, 0.1f);
            Creature body = Chain(config, 0f, 0f, genes);
            var on = new[] { 1f };
            var off = new[] { -1f };
            var bend = new[] { 0f };

            // From rest the ramp is the reaction: halfway after 0.1 s, there after 0.2 s.
            for (int i = 0; i < 10; i++) body.Drive.Drive(on, bend);
            _out.WriteLine($"after 0.1 s: {body.Drive.Stroke[0]:0.###}");
            Assert.Equal(0.5, body.Drive.Stroke[0], 6);
            for (int i = 0; i < 10; i++) body.Drive.Drive(on, bend);
            Assert.Equal(1.0, body.Drive.Stroke[0], 6);

            // A new command inside the hold is not taken.
            for (int i = 0; i < 25; i++) body.Drive.Drive(off, bend);
            Assert.Equal(1.0, body.Drive.Stroke[0], 6);

            // After it, the ramp is the release.
            for (int i = 0; i < 20; i++) body.Drive.Drive(off, bend);
            _out.WriteLine($"0.25 s after the hold ended: {body.Drive.Stroke[0]:0.###}");
            Assert.Equal(0.0, body.Drive.Stroke[0], 6);
        }

        [Fact]
        public void ACommandFlickeringEveryStepMovesTheJointOnlyOnceAHold()
        {
            SolverConfig config = Water();
            var genes = new DriveGenes(0f, 0.02f, 0.5f, 0.02f);
            Creature body = Chain(config, 0f, 0f, genes);
            var bend = new[] { 0f };
            int changes = 0;
            double last = body.Drive.Stroke[0];
            bool rising = false;
            for (int i = 0; i < 200; i++)
            {
                body.Drive.Drive(new[] { i % 2 == 0 ? 1f : -1f }, bend);
                double now = body.Drive.Stroke[0];
                bool up = now > last;
                if (now != last && up != rising) { changes++; rising = up; }
                last = now;
            }
            _out.WriteLine($"{changes} reversals of the power in 2 s of a command flipping every step");
            Assert.InRange(changes, 1, 5);
        }

        [Fact]
        public void TheMuscleHoldsTheJointAtItsBend()
        {
            SolverConfig config = Water();
            var world = new DynamicsWorld(config);
            Creature body = Chain(config, -1f, 1f, new DriveGenes(0f, 0.05f, 0.05f, 0.05f));
            world.Add(body);
            for (int i = 0; i < 300; i++) world.Step();
            double hi = body.LimitHi[0];
            _out.WriteLine($"after 3 s at bend 1: q {body.Q[0]:0.###} rad against the upper limit {hi:0.###}");
            Assert.InRange(body.Q[0], 0.85 * hi, 1.05 * hi);

            var world2 = new DynamicsWorld(config);
            Creature centred = Chain(config, -1f, 0f, new DriveGenes(0f, 0.05f, 0.05f, 0.05f));
            world2.Add(centred);
            for (int i = 0; i < 300; i++) world2.Step();
            Assert.InRange(centred.Q[0], -0.05 * hi, 0.05 * hi);
        }

        [Fact]
        public void APoweredTailSwimsAndAnIdleOneDoesNot()
        {
            double Travel(float power)
            {
                SolverConfig config = Water();
                var world = new DynamicsWorld(config);
                Creature body = Chain(config, power, 0f, new DriveGenes(0f, 0.05f, 0.05f, 0.05f));
                world.Add(body);
                for (int i = 0; i < 2000; i++) world.Step();
                return -body.Position[0];
            }

            double on = Travel(1f), off = Travel(-1f);
            _out.WriteLine($"20 s at full power: {on:0.###} m toward the head; idle {off:0.####} m");
            Assert.True(on > 0.2, $"full power moved the body only {on} m in 20 s");
            Assert.InRange(off, -0.01, 0.01);
        }

        /// <summary>
        /// The chain's hinge bends about a horizontal axis, so a bent limb here pitches its body.
        /// A steer about the vertical is TurningDragTests.ABentFinOnAVerticalHingeSteersItsBody.
        /// </summary>
        [Fact]
        public void ABentPoweredLimbPitchesItsBody()
        {
            double Turn(float bend)
            {
                SolverConfig config = Water();
                var world = new DynamicsWorld(config);
                Creature body = Chain(config, 1f, bend, new DriveGenes(0f, 0.05f, 0.05f, 0.05f));
                world.Add(body);
                for (int i = 0; i < 1000; i++) world.Step();
                Vec3 heading = Mat3.Read(body.RotationMatrix, 0) * new Vec3(-1, 0, 0);
                return Math.Acos(Math.Max(-1, Math.Min(1, -heading.X)));
            }

            double straight = Turn(0f), bent = Turn(0.8f);
            _out.WriteLine($"10 s at full power: the body's axis pitched {straight * 180 / Math.PI:0.#} deg straight, {bent * 180 / Math.PI:0.#} deg bent");
            Assert.True(straight < 0.05, $"a centred limb pitched its body {straight} rad");
            Assert.True(bent > 0.5, $"a bent limb pitched its body only {bent} rad");
        }
    }
}
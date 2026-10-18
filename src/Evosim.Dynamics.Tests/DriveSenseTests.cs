using System;
using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The drive sense (RunConfig.SenseDrive, the owner's ruling of 2026-10-01): a joint's
    /// <see cref="SensorChannel.Stroke"/>, <see cref="SensorChannel.Hold"/> and
    /// <see cref="SensorChannel.Ramp"/> read its envelope as the last drive step left it.
    /// </summary>
    public sealed class DriveSenseTests
    {
        /// <summary>
        /// A two-link chain under the bend-and-power drive; the link's power and bend neurons are
        /// constants and a third neuron reads the three channels, so the body's mask asks for them.
        /// </summary>
        private static Creature Chain(SolverConfig config, DriveGenes genes)
        {
            Genome g = TestBodies.Chain(links: 2, power: 10f);
            MorphNode link = g.Nodes[1];
            link.Neurons = new[]
            {
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f, Inputs = Array.Empty<NeuronInput>() },
                new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f, Inputs = Array.Empty<NeuronInput>() },
                new NeuronDef
                {
                    Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f,
                    Inputs = new[]
                    {
                        NeuronInput.FromSensor(SensorChannel.Stroke, 0),
                        NeuronInput.FromSensor(SensorChannel.Hold, 0),
                        NeuronInput.FromSensor(SensorChannel.Ramp, 0),
                    },
                },
            };
            link.Drive = genes;
            Creature body = TestBodies.Build(g, config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            return body;
        }

        private static SolverConfig Water()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = 0.3;
            config.PowerPushRadiansPerSecond = 6.0;
            config.PoseHertz = 2.0;
            return config;
        }

        [Fact]
        public void TheThreeChannelsReadTheStrokeTheHoldLeftAndTheRampStillToGo()
        {
            SolverConfig config = Water();
            Creature body = Chain(config, new DriveGenes(0f, 0.2f, 0.1f, 0.2f));
            var senses = new CreatureSenses(body, config);
            Assert.True(body.Drive.BendAndPower);

            // At rest: nothing delivered, no hold, nothing to ramp.
            senses.Sample();
            Assert.Equal(0f, senses.Read(1, SensorChannel.Stroke, 0));
            Assert.Equal(0f, senses.Read(1, SensorChannel.Hold, 0));
            Assert.Equal(0f, senses.Read(1, SensorChannel.Ramp, 0));

            // Full power from rest: the command is taken, the hold starts at its whole 0.1 s, and the
            // reaction's 0.2 s ramp has gone one step of 0.01 s, a twentieth of the way.
            var on = new[] { 1f };
            var bend = new[] { 0f };
            body.Drive.Drive(on, bend);
            senses.Sample();
            Assert.Equal(0.05f, senses.Read(1, SensorChannel.Stroke, 0), 5);
            Assert.Equal(1f, senses.Read(1, SensorChannel.Hold, 0), 5);
            Assert.Equal(0.95f, senses.Read(1, SensorChannel.Ramp, 0), 5);

            // Five steps on: half the hold is left and the stroke is at 0.3.
            for (int i = 0; i < 5; i++) body.Drive.Drive(on, bend);
            senses.Sample();
            Assert.Equal(0.3f, senses.Read(1, SensorChannel.Stroke, 0), 5);
            Assert.Equal(0.5f, senses.Read(1, SensorChannel.Hold, 0), 5);
            Assert.Equal(0.7f, senses.Read(1, SensorChannel.Ramp, 0), 5);

            // Long after: arrived, and the joint will take the next command.
            for (int i = 0; i < 30; i++) body.Drive.Drive(on, bend);
            senses.Sample();
            Assert.Equal(1f, senses.Read(1, SensorChannel.Stroke, 0), 5);
            Assert.Equal(0f, senses.Read(1, SensorChannel.Hold, 0));
            Assert.Equal(0f, senses.Read(1, SensorChannel.Ramp, 0), 5);

            // Easing off: the ramp reads negative while the stroke falls toward the new command.
            var off = new[] { 0f };
            body.Drive.Drive(off, bend);
            senses.Sample();
            Assert.True(senses.Read(1, SensorChannel.Ramp, 0) < 0f);
            Assert.Equal(1f, senses.Read(1, SensorChannel.Hold, 0), 5);
        }

        [Fact]
        public void ACommandUnderTheThresholdIsSensedAsNoStroke()
        {
            SolverConfig config = Water();
            Creature body = Chain(config, new DriveGenes(0.4f, 0.02f, 0.02f, 0.02f));
            var senses = new CreatureSenses(body, config);
            for (int i = 0; i < 5; i++) body.Drive.Drive(new[] { 0.3f }, new[] { 0f });
            senses.Sample();
            Assert.Equal(0f, senses.Read(1, SensorChannel.Stroke, 0));
            Assert.Equal(0f, senses.Read(1, SensorChannel.Ramp, 0));
        }

        [Fact]
        public void TheRootAndAMissingDegreeOfFreedomReadNothing()
        {
            SolverConfig config = Water();
            Creature body = Chain(config, new DriveGenes(0f, 0.02f, 0.5f, 0.02f));
            var senses = new CreatureSenses(body, config);
            for (int i = 0; i < 5; i++) body.Drive.Drive(new[] { 1f }, new[] { 0f });
            senses.Sample();
            Assert.True(senses.Read(1, SensorChannel.Stroke, 0) > 0f);
            foreach (SensorChannel c in new[] { SensorChannel.Stroke, SensorChannel.Hold, SensorChannel.Ramp })
            {
                Assert.Equal(0f, senses.Read(0, c, 0));
                Assert.Equal(0f, senses.Read(1, c, 1));
            }
        }

        [Fact]
        public void ABodyThatDoesNotReadTheSenseIsNotSampledForIt()
        {
            SolverConfig config = Water();
            Genome g = TestBodies.Chain(links: 2, power: 10f);
            Creature body = TestBodies.Build(g, config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            var senses = new CreatureSenses(body, config);
            for (int i = 0; i < 5; i++) body.Drive.Drive(new[] { 1f }, new[] { 0f });
            senses.Sample();
            Assert.Equal(0f, senses.Read(1, SensorChannel.Stroke, 0));
        }
    }
}

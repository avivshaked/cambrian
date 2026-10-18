using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D167's two forms of the leaf smell as a body reads them: the squashed strength, and the
    /// direction in the part's own frame scaled by it.
    /// </summary>
    public sealed class LeafScentSenseTests
    {
        private readonly ITestOutputHelper _out;

        public LeafScentSenseTests(ITestOutputHelper output) => _out = output;

        private const double Half = 0.5;

        /// <summary>One leaf of 0.4 m² at (20, -10, 20), on 1 m cells with an 8 m range.</summary>
        private static ScentField OneLeaf()
        {
            var field = new ScentField(40f, 40f, 20f, range: 8f, cell: 1f);
            field.FillSources(new[] { 20f, -10f, 20f, 0.4f }, 1);
            return field;
        }

        /// <summary>A box whose neurons read the channel at each of its indices.</summary>
        private static Creature Reader(SensorChannel channel, int indices, QuatD attitude, Vec3 at)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.LeafScentHalfScaleSquareMetres = Half;

            Genome g = TestBodies.Box(new Float3(0.2f, 0.1f, 0.1f));
            var neurons = new NeuronDef[indices];
            for (int i = 0; i < indices; i++)
            {
                neurons[i] = new NeuronDef
                {
                    Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f,
                    Inputs = new[] { NeuronInput.FromSensor(channel, i) },
                };
            }
            g.Nodes[0].Neurons = neurons;

            Creature body = TestBodies.Build(g, config);
            body.PlaceAt(at, attitude);
            return body;
        }

        [Fact]
        public void TheStrengthIsTheSquashedField()
        {
            ScentField field = OneLeaf();
            Creature body = Reader(SensorChannel.LeafScent, 1, QuatD.Identity, new Vec3(23, -10, 20));
            body.Senses.LeafScent = field;
            body.Senses.Sample();

            float x = field.At((float)body.Position[0], (float)body.Position[1], (float)body.Position[2]);
            float expected = x / (x + (float)Half);
            float read = body.Senses.Read(0, SensorChannel.LeafScent, 0);
            _out.WriteLine($"field {x:0.####}, read {read:0.####}");

            Assert.True(x > 0f);
            Assert.Equal(expected, read, 6);

            // No field is no smell.
            Creature blind = Reader(SensorChannel.LeafScent, 1, QuatD.Identity, new Vec3(23, -10, 20));
            blind.Senses.Sample();
            Assert.Equal(0f, blind.Senses.Read(0, SensorChannel.LeafScent, 0));
        }

        [Fact]
        public void TheDirectionPointsAtTheLeafInThePartsFrameAndItsLengthIsTheStrength()
        {
            ScentField field = OneLeaf();

            // The leaf is 3 m along -x from the body.
            Creature level = Reader(SensorChannel.LeafScentDirection, 3, QuatD.Identity, new Vec3(23, -10, 20));
            level.Senses.LeafScent = field;
            level.Senses.Sample();
            Vec3 d = Read(level);

            float x = field.At(23f, -10f, 20f);
            float strength = x / (x + (float)Half);
            _out.WriteLine($"level: ({d.X:0.####}, {d.Y:0.####}, {d.Z:0.####}), strength {strength:0.####}");

            Assert.True(d.X < 0, "the direction does not point at the leaf");
            Assert.Equal(strength, d.Magnitude, 5);
            Assert.True(Math.Abs(d.Y) < 1e-3 * strength && Math.Abs(d.Z) < 1e-3 * strength);

            // Turned a quarter about y, the body's own -z faces where its -x did, as Flow turns.
            QuatD turned = QuatD.FromAxisAngle(new Vec3(0, 1, 0), Math.PI / 2);
            Creature yawed = Reader(SensorChannel.LeafScentDirection, 3, turned, new Vec3(23, -10, 20));
            yawed.Senses.LeafScent = field;
            yawed.Senses.Sample();
            Vec3 e = Read(yawed);

            Mat3 rotation = Mat3.Read(yawed.RotationMatrix, 0);
            Vec3 world = rotation * e;
            _out.WriteLine($"yawed: in the part ({e.X:0.####}, {e.Y:0.####}, {e.Z:0.####}), in the world ({world.X:0.####}, {world.Y:0.####}, {world.Z:0.####})");

            Assert.Equal(d.X, world.X, 4);
            Assert.Equal(d.Magnitude, e.Magnitude, 5);
            Assert.True(Math.Abs(e.X) < 1e-3 * strength, "the part's own frame did not turn the reading");
        }

        private static Vec3 Read(Creature body) => new Vec3(
            body.Senses.Read(0, SensorChannel.LeafScentDirection, 0),
            body.Senses.Read(0, SensorChannel.LeafScentDirection, 1),
            body.Senses.Read(0, SensorChannel.LeafScentDirection, 2));
    }
}

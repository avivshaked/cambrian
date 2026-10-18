using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// propose-foraging.md's third rule on the solver's side: a part reads the scent at its own
    /// place, squashed as x / (x + k).
    /// </summary>
    public sealed class ScentSenseTests
    {
        /// <summary>A two-part chain whose link's one neuron reads the scent.</summary>
        private static Creature Nose(SolverConfig config)
        {
            Genome genome = TestBodies.Chain(links: 2);
            NeuronDef neuron = genome.Nodes[1].Neurons[0];
            neuron.Inputs = new[] { NeuronInput.FromSensor(SensorChannel.Scent, 0) };
            Creature body = TestBodies.Build(genome, config);
            body.PlaceAt(new Vec3(20, -20, 20), QuatD.Identity);
            return body;
        }

        [Fact]
        public void APartReadsTheSquashedScentAtItsOwnPlace()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.ScentHalfScaleJoules = 20.0;
            Creature body = Nose(config);

            var senses = new CreatureSenses(body, config);
            senses.Sample();
            Assert.Equal(0f, senses.Read(1, SensorChannel.Scent, 0));

            var field = new ScentField(40f, 40f, 40f, 10f);
            field.Add(new Float3(22f, -20f, 20f), 100.0);
            senses.Scent = field;
            senses.Sample();

            for (int part = 0; part < 2; part++)
            {
                float s = field.At(
                    (float)body.Position[3 * part], (float)body.Position[3 * part + 1],
                    (float)body.Position[3 * part + 2]);
                Assert.True(s > 0f);
                Assert.Equal(s / (s + 20f), senses.Read(part, SensorChannel.Scent, 0), 6);
            }

            // The link lies nearer the carcass than the root, and reads more of it.
            Assert.True(senses.Read(1, SensorChannel.Scent, 0) > senses.Read(0, SensorChannel.Scent, 0));

            // And out of range, nothing.
            body.PlaceAt(new Vec3(5, -5, 5), QuatD.Identity);
            senses.Sample();
            Assert.Equal(0f, senses.Read(0, SensorChannel.Scent, 0));
        }
    }
}
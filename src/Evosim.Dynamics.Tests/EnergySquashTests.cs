using System;
using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D245: the energy sense reads the reserve as S / (|S| + k) under the half-scale squash, and as tanh(S / full scale)
    /// under the other, which is every world before it.
    /// </summary>
    public sealed class EnergySquashTests
    {
        private sealed class Reserve : IReserveSource
        {
            public float SecondsOfReserve { get; set; }
        }

        /// <summary>A two-link chain whose link carries a neuron that reads the energy channel, so the body's mask asks for it.</summary>
        private static Creature Reading(SolverConfig config)
        {
            Genome g = TestBodies.Chain(links: 2, power: 10f);
            g.Nodes[1].Neurons = new[]
            {
                new NeuronDef
                {
                    Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f,
                    Inputs = new[] { NeuronInput.FromSensor(SensorChannel.Energy, 0) },
                },
            };
            return TestBodies.Build(g, config);
        }

        private static float Sense(SolverConfig config, float seconds)
        {
            Creature body = Reading(config);
            var senses = new CreatureSenses(body, config);
            senses.Reserve = new Reserve { SecondsOfReserve = seconds };
            senses.Sample();
            return senses.Read(1, SensorChannel.Energy, 0);
        }

        [Fact]
        public void HalfScaleReadsAThirdAtFifteenSecondsAndThreeFifthsAtFortyFiveWithKOfThirty()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.EnergySenseSquash = EnergySenseSquash.HalfScale;
            config.EnergyHalfScaleSeconds = 30.0;

            Assert.Equal(0.333333f, Sense(config, 15f), 5);
            Assert.Equal(0.6f, Sense(config, 45f), 5);
            Assert.Equal(0.5f, Sense(config, 30f), 6);
            Assert.Equal(0f, Sense(config, 0f));

            // 0.9 at 300 s and a little under 1 at 3,000 s: the decades the tanh at 3,000 s crushed into 0.005.
            Assert.Equal(0.909091f, Sense(config, 300f), 5);
            Assert.InRange(Sense(config, 3000f), 0.98f, 1f);
            Assert.True(Sense(config, 3000f) < 1f);
        }

        [Fact]
        public void TanhIsUnchangedAndStillReadsTheFullScaleAtTheSameNumbers()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.EnergyFullScaleSeconds = 3000.0;

            // The default squash, with a half-scale set that must not be read.
            config.EnergyHalfScaleSeconds = 1.0;
            Assert.Equal(EnergySenseSquash.Tanh, config.EnergySenseSquash);

            foreach (float seconds in new[] { 0f, 15f, 45f, 300f, 3000f, 30000f, -45f })
            {
                float expected = (float)Math.Tanh(seconds / 3000f);
                Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(Sense(config, seconds)));
            }

            // 15 s against 3,000 s: the reading D245 calls wrong for a newborn.
            Assert.InRange(Sense(config, 15f), 0.004f, 0.006f);
        }

        [Fact]
        public void HalfScaleIsOddBelowZeroAndEveryNonFiniteAnswerIsTheSameUnderBothSquashes()
        {
            foreach (EnergySenseSquash squash in new[] { EnergySenseSquash.Tanh, EnergySenseSquash.HalfScale })
            {
                SolverConfig config = TestBodies.Water(0.01);
                config.EnergySenseSquash = squash;

                Assert.Equal(0f, Sense(config, float.NaN));
                Assert.Equal(1f, Sense(config, float.PositiveInfinity));
                Assert.Equal(-1f, Sense(config, float.NegativeInfinity));
            }

            SolverConfig half = TestBodies.Water(0.01);
            half.EnergySenseSquash = EnergySenseSquash.HalfScale;
            half.EnergyHalfScaleSeconds = 30.0;
            foreach (float seconds in new[] { 1f, 15f, 45f, 300f, 12345f })
            {
                Assert.Equal(-Sense(half, seconds), Sense(half, -seconds));
            }
        }

        [Fact]
        public void TheSquashIsWrittenOnceAndTheStaticReadsWhatTheSenseReads()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.EnergySenseSquash = EnergySenseSquash.HalfScale;
            config.EnergyHalfScaleSeconds = 30.0;

            foreach (float seconds in new[] { 0f, 0.001f, 15f, 45f, 300f, 3000f, 1e7f, -15f })
            {
                float fromSense = Sense(config, seconds);
                float fromStatic = CreatureSenses.SquashSeconds(seconds, EnergySenseSquash.HalfScale, 3000f, 30f);
                Assert.Equal(BitConverter.SingleToInt32Bits(fromStatic), BitConverter.SingleToInt32Bits(fromSense));
            }
        }

        [Fact]
        public void TheSolverConfigTakesBothTunablesFromTheRunConfig()
        {
            var run = new RunConfig { EnergySenseSquash = EnergySenseSquash.HalfScale, EnergyHalfScaleSeconds = 12.5f };
            SolverConfig solver = SolverConfig.From(run, stepSeconds: 0.01);

            Assert.Equal(EnergySenseSquash.HalfScale, solver.EnergySenseSquash);
            Assert.Equal(12.5, solver.EnergyHalfScaleSeconds);

            SolverConfig plain = SolverConfig.From(new RunConfig(), stepSeconds: 0.01);
            Assert.Equal(EnergySenseSquash.Tanh, plain.EnergySenseSquash);
            Assert.Equal(30.0, plain.EnergyHalfScaleSeconds);
        }
    }
}

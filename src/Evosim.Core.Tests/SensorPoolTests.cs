using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// What a run lets a genome perceive, and the identity that depends on it — DESIGN.md §4.4,
    /// D075 item 1.
    /// </summary>
    /// <remarks>
    /// <b>The property under test is a negative one.</b> Wiring <c>Chemical</c>, <c>Energy</c> and
    /// <c>Flow</c> is only safe because turning them on is a decision a run makes: a sensor draw is
    /// one <see cref="Rng.Pick{T}"/> into an array, so a pool one element longer returns a
    /// different channel for the same draw and every run in the historical record stops replaying.
    /// These tests hold the default pool to the four channels, in the order they were always in,
    /// and hold mutation to drawing from the pool rather than from
    /// <see cref="SensorChannels.Implemented"/>.
    /// </remarks>
    public class SensorPoolTests
    {
        private readonly ITestOutputHelper _output;

        public SensorPoolTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void TheDefaultPoolIsTodaysFourChannelsInTodaysOrder()
        {
            Assert.Equal(SensorChannels.DefaultPool, new RunConfig().SensorPool());

            Assert.Equal(
                new[]
                {
                    SensorChannel.JointAngle,
                    SensorChannel.JointAngularVelocity,
                    SensorChannel.OrientationUp,
                    SensorChannel.Depth,
                },
                SensorChannels.DefaultPool);
        }

        [Fact]
        public void ImplementedIsEverythingTheSimulatorAnswers()
        {
            // Seven, not four: the simulator answers the three later channels whether or not a
            // run lets anything draw them. The Milestone 1 smoke test is what holds this honest —
            // it drives a creature and fails any channel named here that reads a constant.
            Assert.Equal(
                new[]
                {
                    SensorChannel.JointAngle,
                    SensorChannel.JointAngularVelocity,
                    SensorChannel.OrientationUp,
                    SensorChannel.Depth,
                    SensorChannel.Chemical,
                    SensorChannel.Energy,
                    SensorChannel.Flow,
                },
                SensorChannels.Implemented);

            Assert.All(SensorChannels.DefaultPool, c => Assert.True(c.IsImplemented()));
        }

        [Fact]
        public void EnabledChannelsAreAppendedAfterDepth()
        {
            var all = new RunConfig { SenseChemical = true, SenseEnergy = true, SenseFlow = true };

            Assert.Equal(
                new[]
                {
                    SensorChannel.JointAngle,
                    SensorChannel.JointAngularVelocity,
                    SensorChannel.OrientationUp,
                    SensorChannel.Depth,
                    SensorChannel.Chemical,
                    SensorChannel.Energy,
                    SensorChannel.Flow,
                },
                all.SensorPool());

            // One at a time, each landing at index 4 — nothing is ever inserted before Depth, and
            // the first four entries are the same array positions in every configuration.
            Assert.Equal(
                new[] { SensorChannel.Flow },
                Tail(new RunConfig { SenseFlow = true }.SensorPool()));

            Assert.Equal(
                new[] { SensorChannel.Chemical, SensorChannel.Flow },
                Tail(new RunConfig { SenseChemical = true, SenseFlow = true }.SensorPool()));

            Assert.Equal(
                new[] { SensorChannel.Energy },
                Tail(new RunConfig { SenseEnergy = true }.SensorPool()));
        }

        [Fact]
        public void ThePoolCacheFollowsTheKnobs()
        {
            // The pool is cached because it is read once per sensor draw. A cache that outlived a
            // setter would make an arm's genomes disagree with its own header — the shape of
            // fault this project has twice paid for, where a setting never reached the thing it
            // configured (logbook/0007, logbook/0008).
            var config = new RunConfig();
            Assert.Equal(4, config.SensorPool().Length);

            config.SenseChemical = true;
            Assert.Equal(5, config.SensorPool().Length);

            config.SenseChemical = false;
            Assert.Equal(4, config.SensorPool().Length);
        }

        [Fact]
        public void TheDefaultPoolConsumesTheDrawItAlwaysConsumed()
        {
            // The identity requirement, at the one place it can be checked without a physics run:
            // a draw against the default pool must return the same channel *and* leave the stream
            // in the same state as a draw against SensorChannels.DefaultPool. If it did not, every
            // founder and every mutant after the first sensor reference would diverge.
            SensorChannel[] pool = new RunConfig().SensorPool();

            var a = new Rng(4242);
            var b = new Rng(4242);

            for (int i = 0; i < 500; i++)
            {
                NeuronInput fromDefault = SensorChannels.RandomSensor(a, 1f);
                NeuronInput fromPool = SensorChannels.RandomSensor(b, 1f, pool);

                Assert.Equal(fromDefault.Channel, fromPool.Channel);
                Assert.Equal(fromDefault.Index, fromPool.Index);
            }

            // Both streams must have consumed the same number of draws, not merely produced the
            // same answers — a pool that consumed one extra draw would agree here and disagree
            // about everything afterwards.
            Assert.Equal(a.Range(1_000_000), b.Range(1_000_000));
        }

        [Fact]
        public void FoundersAreUnchangedByTheKnobsBeingOff()
        {
            // The same property one level up, over whole genomes: with every sense off, the
            // founder a seed produces is the founder that seed always produced.
            var options = RandomGenomeOptions.Default;

            for (ulong seed = 1; seed <= 60; seed++)
            {
                Genome withoutPool = GenomeFactory.Founder(new Rng(seed), options);
                Genome withDefaultPool = GenomeFactory.Founder(
                    new Rng(seed), options, new RunConfig().SensorPool());

                Assert.Equal(Wiring(withoutPool), Wiring(withDefaultPool));
            }
        }

        [Fact]
        public void MutationDrawsFromThePoolAndOnlyFromThePool()
        {
            // With the knobs off, three hundred mutation steps must never produce a Chemical,
            // Energy or Flow reference; with them on, they must reach all three — otherwise the
            // knob is decorative and a round turning it on would measure nothing.
            var rates = new MutationRates { RewireInputChance = 0.9f };

            HashSet<SensorChannel> closed = ChannelsReachedByMutation(rates, new RunConfig());
            HashSet<SensorChannel> open = ChannelsReachedByMutation(
                rates,
                new RunConfig { SenseChemical = true, SenseEnergy = true, SenseFlow = true });

            _output.WriteLine("closed: " + string.Join(", ", closed));
            _output.WriteLine("open:   " + string.Join(", ", open));

            Assert.All(closed, c => Assert.Contains(c, SensorChannels.DefaultPool));
            Assert.DoesNotContain(SensorChannel.Chemical, closed);
            Assert.DoesNotContain(SensorChannel.Energy, closed);
            Assert.DoesNotContain(SensorChannel.Flow, closed);

            Assert.All(SensorChannels.Implemented, c => Assert.Contains(c, open));
        }

        [Fact]
        public void ABrainsSensorMaskIsExactlyWhatItsNeuronsRead()
        {
            // §4.4's requirement mask. It has to be exact in both directions: a bit missing means
            // the simulator skips computing a channel some neuron reads, and reports a silent
            // zero; a bit spare means the mask saves nothing and the cost stays where it was.
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, DevelopmentLimits.Default,
                    minParts: 3);

                Phenotype phenotype = Developer.Develop(genome, DevelopmentLimits.Default);
                Brain brain = Brain.For(phenotype, genome.GlobalBrain);

                var expected = new HashSet<SensorChannel>();
                foreach (PhenotypePart part in phenotype.Parts)
                {
                    Collect(part.Neurons, expected);
                }
                Collect(genome.GlobalBrain, expected);

                foreach (SensorChannel channel in Enum.GetValues(typeof(SensorChannel)))
                {
                    Assert.Equal(
                        expected.Contains(channel),
                        Brain.MaskReads(brain.SensorMask, channel));
                }
            }
        }

        [Fact]
        public void TheAllChannelsMaskReadsEverything()
        {
            foreach (SensorChannel channel in Enum.GetValues(typeof(SensorChannel)))
            {
                Assert.True(Brain.MaskReads(Brain.AllSensorChannels, channel));
                Assert.False(Brain.MaskReads(0, channel));
            }
        }

        private static void Collect(NeuronDef[] neurons, HashSet<SensorChannel> into)
        {
            if (neurons == null) return;

            foreach (NeuronDef neuron in neurons)
            {
                if (neuron.Inputs == null) continue;

                foreach (NeuronInput input in neuron.Inputs)
                {
                    if (input.Kind == NeuronInputKind.Sensor) into.Add(input.Channel);
                }
            }
        }

        private static HashSet<SensorChannel> ChannelsReachedByMutation(
            MutationRates rates, RunConfig config)
        {
            var seen = new HashSet<SensorChannel>();

            Genome g = GenomeFactory.RandomViable(
                new Rng(3), RandomGenomeOptions.Default, DevelopmentLimits.Default, minParts: 3,
                sensorPool: config.SensorPool());

            for (ulong step = 1; step <= 300; step++)
            {
                g = Mutator.Mutate(
                    g, new Rng(step), rates, CellTypeRegistry.Standard, RandomGenomeOptions.Default,
                    config.SensorPool());

                foreach (MorphNode node in g.Nodes)
                {
                    foreach (NeuronDef neuron in node.Neurons)
                    {
                        foreach (NeuronInput input in neuron.Inputs)
                        {
                            if (input.Kind == NeuronInputKind.Sensor) seen.Add(input.Channel);
                        }
                    }
                }
            }

            return seen;
        }

        private static SensorChannel[] Tail(SensorChannel[] pool)
        {
            var tail = new SensorChannel[pool.Length - SensorChannels.DefaultPool.Length];
            Array.Copy(pool, SensorChannels.DefaultPool.Length, tail, 0, tail.Length);
            return tail;
        }

        /// <summary>Every neuron input in a genome, as text, so two genomes can be compared.</summary>
        private static string Wiring(Genome g)
        {
            var sb = new System.Text.StringBuilder();

            foreach (MorphNode node in g.Nodes)
            {
                foreach (NeuronDef neuron in node.Neurons)
                {
                    foreach (NeuronInput input in neuron.Inputs)
                    {
                        sb.Append(input.Kind).Append(':')
                          .Append(input.Channel).Append(':')
                          .Append(input.Index).Append(';');
                    }
                }
            }

            return sb.ToString();
        }
    }
}

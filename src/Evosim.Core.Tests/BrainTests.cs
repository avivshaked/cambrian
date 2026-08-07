using System;
using System.Collections.Generic;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The controller the genome always described and nothing evaluated — DESIGN.md §4.3, §4.4.
    /// </summary>
    public class BrainTests
    {
        private readonly ITestOutputHelper _output;

        public BrainTests(ITestOutputHelper output) => _output = output;

        private const float Dt = 0.01f;

        [Fact]
        public void AnOscillatorProducesItsOwnFrequencyAndPhase()
        {
            // The MVP operator set is a pure pattern generator (§4.3), so this one operator is
            // what has to work for anything to swim at all.
            Brain brain = Brain.For(OneJoint(
                new NeuronDef { Op = NeuronOp.OscillateWave, Frequency = 2f, Amplitude = 1f }));

            var drive = new float[1];

            // A quarter period of a 2 Hz wave is 0.125 s, where sin is at its peak.
            for (int i = 0; i < 13; i++) brain.Step(Dt, drive);
            _output.WriteLine($"2 Hz at t=0.13 s reads {drive[0]:0.###}");
            Assert.True(drive[0] > 0.9f, $"expected near the peak, got {drive[0]}");

            // Half a period later, the trough.
            for (int i = 0; i < 25; i++) brain.Step(Dt, drive);
            _output.WriteLine($"a half period later it reads {drive[0]:0.###}");
            Assert.True(drive[0] < -0.9f, $"expected near the trough, got {drive[0]}");
        }

        [Fact]
        public void ASawtoothIsAsymmetricAndASineIsNot()
        {
            // §4.3 keeps oscillate-saw in the MVP set on the strength of [C18 §4, p.30]: harmonic
            // actuation alone is a real limitation in unsteady aquatic locomotion, and a sawtooth
            // has the asymmetric duty cycle a sine cannot express. Worth measuring rather than
            // trusting the operator's name.
            float RisingShare(NeuronOp op)
            {
                Brain brain = Brain.For(OneJoint(
                    new NeuronDef { Op = op, Frequency = 1f, Amplitude = 1f }));

                var drive = new float[1];
                float last = 0f;
                int rising = 0;

                for (int i = 0; i < 400; i++)
                {
                    brain.Step(Dt, drive);
                    if (drive[0] > last) rising++;
                    last = drive[0];
                }

                return rising / 400f;
            }

            float sine = RisingShare(NeuronOp.OscillateWave);
            float saw = RisingShare(NeuronOp.OscillateSaw);

            _output.WriteLine($"share of the cycle spent rising — sine {sine:P0}, saw {saw:P0}");

            Assert.True(Math.Abs(sine - 0.5f) < 0.06f, $"a sine should rise half the time, got {sine:P0}");
            Assert.True(saw > 0.9f, $"a saw should rise almost all the time, got {saw:P0}");
        }

        [Fact]
        public void TwoGenomesProduceDifferentDriveAndThatIsTheWholePoint()
        {
            // The fault this class exists to fix. Every creature used to run one shared sine
            // regardless of its genome, so the controller was a constant across the population,
            // morphology was the only thing that could vary, and once mechanical work was billed
            // the world deleted every joint it had in sixty seconds (logbook/0015).
            var limits = DevelopmentLimits.Default;
            var options = RandomGenomeOptions.Default;
            var signatures = new List<string>();

            for (ulong seed = 1; seed <= 10; seed++)
            {
                Genome genome = GenomeFactory.RandomViable(new Rng(seed), options, limits, minParts: 3);
                Phenotype phenotype = Developer.Develop(genome, limits);

                Brain brain = Brain.For(phenotype, genome.GlobalBrain);
                if (brain.TotalDof == 0) continue;

                var drive = new float[brain.TotalDof];
                var trace = new StringBuilder();

                for (int i = 0; i < 60; i++)
                {
                    brain.Step(Dt, drive);
                    if (i % 20 == 0) trace.Append(drive[0].ToString("0.###")).Append(' ');
                }

                signatures.Add(trace.ToString());
                _output.WriteLine($"seed {seed}: {brain.TotalDof} dof, drive[0] over time: {trace}");
            }

            Assert.True(signatures.Count >= 4, $"only {signatures.Count} genomes had a joint");

            var distinct = new HashSet<string>(signatures);
            _output.WriteLine($"{distinct.Count} distinct signals from {signatures.Count} genomes");

            Assert.True(distinct.Count > 1,
                "every genome produced the identical drive signal — the brain is not being read");
        }

        [Fact]
        public void ASignalCrossesExactlyOneNodePerStep()
        {
            // §4.4 leans on this three times: damage propagates outward with no relay mechanism,
            // latency is bounded by body length, and "a long body senses direction better but
            // thinks about it slower". None of it is true if neurons update in place in part
            // order, which would also make the result depend on iteration order.
            NeuronDef Holds() => new NeuronDef
            {
                Op = NeuronOp.Sum,
                Inputs = new[] { NeuronInput.FromConstant(1f) },
            };

            NeuronDef ReadsParent() => new NeuronDef
            {
                Op = NeuronOp.Sum,
                Inputs = new[] { NeuronInput.FromNeuron(NeuronInputKind.ParentNode, 0) },
            };

            Phenotype phenotype = Linear(
                new[] { Holds() }, new[] { ReadsParent() }, new[] { ReadsParent() });

            Assert.Equal(3, phenotype.PartCount);

            Brain brain = Brain.For(phenotype);
            var drive = new float[Math.Max(1, brain.TotalDof)];

            var arrived = new int[phenotype.PartCount];
            for (int i = 0; i < arrived.Length; i++) arrived[i] = -1;

            for (int step = 1; step <= 8; step++)
            {
                brain.Step(Dt, drive);

                for (int p = 0; p < phenotype.PartCount; p++)
                {
                    if (arrived[p] < 0 && Math.Abs(brain.Output(p, 0)) > 0.5f) arrived[p] = step;
                }
            }

            _output.WriteLine($"the signal reached parts 0,1,2 on steps {string.Join(", ", arrived)}");

            Assert.Equal(1, arrived[0]);
            Assert.Equal(2, arrived[1]);
            Assert.Equal(3, arrived[2]);
        }

        [Fact]
        public void RecursionCopiesTheControllerWithTheSegment()
        {
            // §4.3's central claim: neurons live inside morph nodes, so a recursive body is a
            // chain of identical local controllers — structurally a central pattern generator.
            // If the copies were not identical, recursion would not be producing one.
            var node = new MorphNode
            {
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Hinge,
                JointLimits = new[] { new Float2(-0.8f, 0.8f) },
                Power = 40f,
                RecursiveLimit = 3,
                CellTypeId = CellTypeIds.Link,
                ShapeId = ShapeIds.Box,
                Neurons = new[] { new NeuronDef { Op = NeuronOp.OscillateWave, Frequency = 1.5f } },
            };

            node.Edges.Add(new MorphEdge
            {
                Child = 0,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Scale = new Float3(0.9f, 0.9f, 0.9f),
            });

            var genome = new Genome { RootIndex = 0 };
            genome.Nodes.Add(node);

            Phenotype phenotype = Developer.Develop(genome, DevelopmentLimits.Default);
            _output.WriteLine($"{phenotype.PartCount} parts, {phenotype.TotalDof} dof");

            Assert.True(phenotype.TotalDof >= 2, "the recursive chain did not develop");

            Brain brain = Brain.For(phenotype);
            var drive = new float[brain.TotalDof];

            for (int i = 0; i < 37; i++) brain.Step(Dt, drive);

            for (int d = 1; d < drive.Length; d++)
            {
                Assert.True(Math.Abs(drive[d] - drive[0]) < 1e-5f,
                    $"dof {d} reads {drive[d]} against dof 0's {drive[0]} — the copies differ");
            }

            _output.WriteLine($"all {drive.Length} joints in phase at {drive[0]:0.####}");
        }

        [Fact]
        public void TheSameGenomeGivesTheSameDriveEveryTime()
        {
            // §7. A controller that varied between runs would make every stored result
            // unreproducible, and would do it silently.
            Genome genome = GenomeFactory.RandomViable(
                new Rng(99), RandomGenomeOptions.Default, DevelopmentLimits.Default, minParts: 3);

            string Trace()
            {
                Phenotype phenotype = Developer.Develop(genome, DevelopmentLimits.Default);
                Brain brain = Brain.For(phenotype, genome.GlobalBrain);

                var drive = new float[Math.Max(1, brain.TotalDof)];
                var text = new StringBuilder();

                for (int i = 0; i < 100; i++)
                {
                    brain.Step(Dt, drive);
                    for (int d = 0; d < brain.TotalDof; d++) text.Append(drive[d].ToString("R")).Append(',');
                }

                return text.ToString();
            }

            Assert.Equal(Trace(), Trace());
        }

        [Fact]
        public void DriveIsAlwaysFiniteAndInRange()
        {
            // A NaN torque diverges the solver, and a diverged solver is a creature that has
            // found infinite energy (§11.2). Divide, Integrate and Product are each one mutation
            // away from producing one, so the guard is at the output rather than per operator.
            var rng = new Rng(4242);

            for (int trial = 0; trial < 300; trial++)
            {
                var neurons = new NeuronDef[3];

                for (int n = 0; n < neurons.Length; n++)
                {
                    var inputs = new NeuronInput[rng.Range(0, 4)];
                    for (int i = 0; i < inputs.Length; i++)
                    {
                        inputs[i] = rng.Range(0, 2) == 0
                            ? NeuronInput.FromConstant(rng.Range(-1e6f, 1e6f), rng.Range(-1e3f, 1e3f))
                            : NeuronInput.FromNeuron(
                                NeuronInputKind.SameNode, rng.Range(0, 3), rng.Range(-1e3f, 1e3f));
                    }

                    neurons[n] = new NeuronDef
                    {
                        Op = rng.Pick(NeuronOps.All),
                        Inputs = inputs,
                        Frequency = rng.Range(-100f, 100f),
                        Phase = rng.Range(-10f, 10f),
                        Amplitude = rng.Range(-1e4f, 1e4f),
                        Bias = rng.Range(-1e4f, 1e4f),
                    };
                }

                Brain brain = Brain.For(OneJoint(neurons));
                var drive = new float[brain.TotalDof];

                for (int i = 0; i < 40; i++)
                {
                    brain.Step(Dt, drive);

                    for (int d = 0; d < drive.Length; d++)
                    {
                        Assert.False(float.IsNaN(drive[d]), $"trial {trial} produced NaN");
                        Assert.False(float.IsInfinity(drive[d]), $"trial {trial} produced infinity");
                        Assert.InRange(drive[d], -1f, 1f);
                    }
                }
            }
        }

        [Fact]
        public void AnUnwiredJointStaysStillRatherThanReadingSomeoneElse()
        {
            // A part with fewer neurons than degrees of freedom is a real morphology — a joint
            // nothing innervates. It must read zero, not whatever was already in the buffer,
            // which for a buffer reused across a population is another creature's last stroke.
            Phenotype phenotype = Linear(
                spherical: true,
                neuronsPerNode: new[]
                {
                    Array.Empty<NeuronDef>(),
                    new[] { new NeuronDef { Op = NeuronOp.OscillateWave, Frequency = 1f, Amplitude = 1f } },
                });

            Brain brain = Brain.For(phenotype);
            Assert.Equal(3, brain.TotalDof);

            var drive = new[] { 9f, 9f, 9f };
            for (int i = 0; i < 13; i++) brain.Step(Dt, drive);

            _output.WriteLine(
                $"one neuron on a 3-dof joint drives [{drive[0]:0.##}, {drive[1]:0.##}, {drive[2]:0.##}]");

            Assert.True(Math.Abs(drive[0]) > 0.5f, "the innervated dof is not moving");
            Assert.Equal(0f, drive[1]);
            Assert.Equal(0f, drive[2]);
        }

        [Fact]
        public void TheRootNeverHasAJointSoTheTwoDofOrderingsAgree()
        {
            // The one real risk in wiring this up. Evosim.Sim indexes drive through
            // CreatureInstance.DofOffset, which skips the root outright; Brain walks
            // Phenotype.Parts in order and counts every part's DofCount(). Those agree only
            // because Developer forces the root's joint to Fixed. If that ever changed, every
            // creature would drive the wrong joints and nothing would throw — the plausible
            // number rather than the error (logbook/0007, logbook/0008).
            var limits = DevelopmentLimits.Default;
            int checkedGenomes = 0;

            for (ulong seed = 1; seed <= 60; seed++)
            {
                Genome genome = GenomeFactory.RandomViable(
                    new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 2);

                // Force the root node to something actuated, legally, so the genome still
                // validates. Development must ignore it and build a fixed root anyway.
                MorphNode root = genome.Nodes[genome.RootIndex];
                root.CellTypeId = CellTypeIds.Link;
                root.JointType = JointType.Spherical;
                root.JointLimits = new[]
                {
                    new Float2(-0.8f, 0.8f), new Float2(-0.8f, 0.8f), new Float2(-0.8f, 0.8f),
                };
                root.Power = 40f;

                Phenotype phenotype = Developer.Develop(genome, limits);
                if (phenotype.PartCount == 0) continue;

                checkedGenomes++;

                Assert.Equal(JointType.Fixed, phenotype.Parts[0].JointType);
                Assert.Equal(0, phenotype.Parts[0].JointType.DofCount());

                // And the total the brain sizes itself by is the total the builder would assign.
                int fromParts = 0;
                for (int i = 1; i < phenotype.PartCount; i++)
                {
                    fromParts += phenotype.Parts[i].JointType.DofCount();
                }

                Assert.Equal(fromParts, Brain.For(phenotype).TotalDof);
            }

            _output.WriteLine($"{checkedGenomes} genomes, root always fixed, orderings agree");
            Assert.True(checkedGenomes > 40, $"only {checkedGenomes} genomes developed");
        }

        [Fact]
        public void AShortDriveBufferIsRefusedRatherThanPartlyFilled()
        {
            Brain brain = Brain.For(OneJoint(new NeuronDef { Op = NeuronOp.OscillateWave }));
            Assert.Throws<ArgumentException>(() => brain.Step(Dt, Array.Empty<float>()));
        }

        // ------------------------------------------------------------------ fixtures
        //
        // Everything here goes through Genome + Developer rather than assembling a Phenotype by
        // hand, because PhenotypePart's setters are internal by design — a developed body is an
        // output, not something to edit. It also means these exercise the real path a creature
        // takes from genome to controller.

        /// <summary>A root and one hinged child carrying the given neurons.</summary>
        private static Phenotype OneJoint(params NeuronDef[] neurons) =>
            Linear(Array.Empty<NeuronDef>(), neurons);

        /// <summary>A straight chain of distinct nodes, one part each, joined by hinges.</summary>
        private static Phenotype Linear(params NeuronDef[][] neuronsPerNode) =>
            Linear(false, neuronsPerNode);

        private static Phenotype Linear(bool spherical, NeuronDef[][] neuronsPerNode)
        {
            var genome = new Genome { RootIndex = 0 };

            for (int i = 0; i < neuronsPerNode.Length; i++)
            {
                var node = new MorphNode
                {
                    Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                    JointType = spherical ? JointType.Spherical : JointType.Hinge,
                    JointLimits = spherical
                        ? new[]
                        {
                            new Float2(-0.8f, 0.8f), new Float2(-0.8f, 0.8f), new Float2(-0.8f, 0.8f),
                        }
                        : new[] { new Float2(-0.8f, 0.8f) },
                    Power = 40f,
                    RecursiveLimit = 1,
                    CellTypeId = CellTypeIds.Link,
                    ShapeId = ShapeIds.Box,
                    Neurons = neuronsPerNode[i],
                };

                if (i + 1 < neuronsPerNode.Length)
                {
                    node.Edges.Add(new MorphEdge
                    {
                        Child = i + 1,
                        ParentAnchor = new Float3(1f, 0f, 0f),
                        ChildAnchor = new Float3(-1f, 0f, 0f),
                        Scale = Float3.One,
                    });
                }

                genome.Nodes.Add(node);
            }

            Phenotype phenotype = Developer.Develop(genome, DevelopmentLimits.Default);

            if (phenotype.PartCount != neuronsPerNode.Length)
            {
                throw new InvalidOperationException(
                    $"fixture wanted {neuronsPerNode.Length} parts and developed {phenotype.PartCount}");
            }

            return phenotype;
        }
    }
}

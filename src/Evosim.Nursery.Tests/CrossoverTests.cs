using System;
using System.Linq;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// propose-nursery-crossover.md's route and copy, on two-part bodies built by hand: a fixed
    /// root (node 0) with one edge to a hinge (node 1). On the hinge, neuron 0 is the power and
    /// neuron 1 the bend.
    /// </summary>
    public class CrossoverTests
    {
        // ------------------------------------------------------------ builders

        private static NeuronDef N(NeuronOp op, float bias, params NeuronInput[] inputs) =>
            new NeuronDef { Op = op, Inputs = inputs, Frequency = 1f + bias, Phase = bias, Amplitude = 1f, Bias = bias };

        private static NeuronDef Sum(float bias, params NeuronInput[] inputs) => N(NeuronOp.Sum, bias, inputs);

        private static NeuronInput Same(int i, float w) => NeuronInput.FromNeuron(NeuronInputKind.SameNode, i, w);
        private static NeuronInput Parent(int i, float w) => NeuronInput.FromNeuron(NeuronInputKind.ParentNode, i, w);
        private static NeuronInput Sense(SensorChannel c, float w) => NeuronInput.FromSensor(c, 0, w);
        private static NeuronInput Const(float v, float w) => NeuronInput.FromConstant(v, w);

        private static Genome Body(NeuronDef[] root, NeuronDef[] hinge)
        {
            var g = new Genome { RootIndex = 0 };
            var r = new MorphNode { JointType = JointType.Fixed, Neurons = root };
            r.Edges.Add(new MorphEdge { Child = 1 });
            var h = new MorphNode { JointType = JointType.Hinge, JointLimits = new[] { new Float2(-1f, 1f) }, Neurons = hinge };
            g.Nodes.Add(r);
            g.Nodes.Add(h);
            return g;
        }

        private static NeuronDef[] BaseHinge(int count) =>
            Enumerable.Range(0, count).Select(i => Sum(-1f - i, Sense(SensorChannel.Depth, 0.5f))).ToArray();

        /// <summary>The crossover on the first seed whose output is the hinge's neuron <paramref name="output"/>.</summary>
        private static Genome CrossAt(Genome b, Genome d, int output, int limit, out CrossoverRecord record)
        {
            for (ulong s = 1; s < 500; s++)
            {
                Genome g = Crossover.Cross(b, d, new Rng(s), limit, out record);
                if (record.OutputNode == 1 && record.OutputIndex == output) return g;
            }
            throw new InvalidOperationException("no seed chose output " + output);
        }

        private static void SameNeuron(NeuronDef a, NeuronDef b)
        {
            Assert.Equal(a.Op, b.Op);
            Assert.Equal(a.Frequency, b.Frequency);
            Assert.Equal(a.Phase, b.Phase);
            Assert.Equal(a.Amplitude, b.Amplitude);
            Assert.Equal(a.Bias, b.Bias);
            Assert.Equal(a.Inputs.Length, b.Inputs.Length);
            for (int k = 0; k < a.Inputs.Length; k++) SameInput(a.Inputs[k], b.Inputs[k]);
        }

        private static void SameInput(NeuronInput a, NeuronInput b)
        {
            Assert.Equal(a.Kind, b.Kind);
            Assert.Equal(a.Index, b.Index);
            Assert.Equal(a.Channel, b.Channel);
            Assert.Equal(a.Constant, b.Constant);
            Assert.Equal(a.Weight, b.Weight);
        }

        private static (int, int)[] Route(CrossoverRecord r) => r.Route.Select(x => (x.Node, x.Index)).ToArray();

        // ------------------------------------------------------------ the route

        [Fact]
        public void RouteFollowsTheLargestAbsoluteWeightAndSkipsConstants()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Const(1f, 10f), Same(2, -3f), Same(3, 2f)),
                Sum(1f, Sense(SensorChannel.Depth, 0.1f)),
                Sum(2f, Sense(SensorChannel.Chemical, 1f)),
                Sum(3f, Sense(SensorChannel.Flow, 5f)),
            });
            CrossAt(Body(new NeuronDef[0], BaseHinge(4)), donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0), (1, 2) }, Route(r));
            Assert.Equal(RouteEnd.Sense, r.End);
            Assert.True(r.Transferred);
        }

        [Fact]
        public void RouteReadsOnlyTheInputsTheOperatorReads()
        {
            // A Sigmoid reads its first input only, so the stronger sense in second place is not followed.
            Genome donor = Body(new NeuronDef[0], new[]
            {
                N(NeuronOp.Sigmoid, 0f, Same(2, 0.1f), Sense(SensorChannel.Chemical, 5f)),
                Sum(1f),
                Sum(2f, Sense(SensorChannel.Flow, 1f)),
            });
            CrossAt(Body(new NeuronDef[0], BaseHinge(3)), donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0), (1, 2) }, Route(r));
            Assert.Equal(RouteEnd.Sense, r.End);
        }

        [Fact]
        public void RouteEndsAtASense()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Same(1, 1f)),
                Sum(1f, Sense(SensorChannel.Scent, -2f), Same(0, 1f)),
            });
            CrossAt(Body(new NeuronDef[0], BaseHinge(2)), donor, 1, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 1) }, Route(r));
            Assert.Equal(RouteEnd.Sense, r.End);
            Assert.True(r.Transferred);
        }

        [Fact]
        public void RouteEndsAtAnOscillatorWhoseInputsAreNotRead()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Same(2, 1f)),
                Sum(1f),
                N(NeuronOp.OscillateWave, 2f, Sense(SensorChannel.Chemical, 9f)),
            });
            CrossAt(Body(new NeuronDef[0], BaseHinge(3)), donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0), (1, 2) }, Route(r));
            Assert.Equal(RouteEnd.NoInput, r.End);
            Assert.True(r.Transferred);
        }

        [Fact]
        public void RouteEndsAtANeuronWithNoInputs()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Same(2, 1f)),
                Sum(1f),
                Sum(2f, Const(1f, 4f)),
            });
            CrossAt(Body(new NeuronDef[0], BaseHinge(3)), donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0), (1, 2) }, Route(r));
            Assert.Equal(RouteEnd.NoInput, r.End);
        }

        [Fact]
        public void RouteStopsOnALoop()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Same(2, 1f)),
                Sum(1f),
                Sum(2f, Same(3, 1f)),
                Sum(3f, Same(2, 4f), Same(0, 3f)),
            });
            CrossAt(Body(new NeuronDef[0], BaseHinge(4)), donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0), (1, 2), (1, 3) }, Route(r));
            Assert.Equal(RouteEnd.Loop, r.End);
            Assert.True(r.Transferred);
        }

        [Fact]
        public void AnOutputWithNothingToFollowPassesNothing()
        {
            Genome donor = Body(new NeuronDef[0], new[] { Sum(0f, Same(0, 2f)), Sum(1f, Same(0, 1f)) });
            Genome b = Body(new NeuronDef[0], BaseHinge(2));
            Genome child = CrossAt(b, donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(RouteEnd.Loop, r.End);
            Assert.False(r.Transferred);
            Assert.Equal("no-route", r.Refused);
            Assert.Equal(GenomeJson.Write(b), GenomeJson.Write(child));
        }

        // ------------------------------------------------------------ across parts

        [Fact]
        public void ParentNodeIsFollowedWhenTheGenomeNamesOneParent()
        {
            Genome donor = Body(
                new[] { Sum(10f, Sense(SensorChannel.Depth, 1f)), Sum(11f, Sense(SensorChannel.Chemical, 1f)) },
                new[] { Sum(0f, Parent(1, 2f)), Sum(1f) });
            Genome b = Body(new[] { Sum(-10f) }, BaseHinge(2));
            Genome child = CrossAt(b, donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0), (0, 1) }, Route(r));
            Assert.Equal(RouteEnd.Sense, r.End);
            Assert.Equal(1, r.Added);
            SameNeuron(donor.Nodes[0].Neurons[1], child.Nodes[0].Neurons[1]);
            SameInput(Parent(1, 2f), child.Nodes[1].Neurons[0].Inputs.Last());
        }

        [Fact]
        public void ParentNodeWithTwoParentsEndsTheRoute()
        {
            Genome donor = Body(new[] { Sum(10f, Sense(SensorChannel.Depth, 1f)) }, new[] { Sum(0f, Parent(0, 2f)), Sum(1f) });
            Genome b = Body(new[] { Sum(-10f) }, BaseHinge(2));
            foreach (Genome g in new[] { donor, b })
            {
                var other = new MorphNode { JointType = JointType.Fixed };
                other.Edges.Add(new MorphEdge { Child = 1 });
                g.Nodes.Add(other);
            }
            CrossAt(b, donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0) }, Route(r));
            Assert.Equal(RouteEnd.Unresolved, r.End);
            Assert.Equal("no-route", r.Refused);
        }

        [Fact]
        public void ParentNodeAtTheRootEndsTheRouteThere()
        {
            Genome donor = Body(new[] { Sum(10f, Parent(0, 5f), Sense(SensorChannel.Depth, 1f)) }, new[] { Sum(0f, Parent(0, 2f)), Sum(1f) });
            Genome b = Body(new[] { Sum(-10f) }, BaseHinge(2));
            Genome child = CrossAt(b, donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0), (0, 0) }, Route(r));
            Assert.Equal(RouteEnd.Unresolved, r.End);
            Assert.True(r.Transferred);
            SameNeuron(donor.Nodes[0].Neurons[0], child.Nodes[0].Neurons[0]);
        }

        [Fact]
        public void GlobalBrainInputEndsTheRoute()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, NeuronInput.FromNeuron(NeuronInputKind.GlobalBrain, 0, 5f), Sense(SensorChannel.Depth, 1f)),
                Sum(1f),
            });
            CrossAt(Body(new NeuronDef[0], BaseHinge(2)), donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(RouteEnd.Unresolved, r.End);
            Assert.False(r.Transferred);
        }

        // ------------------------------------------------------------ the copy

        [Fact]
        public void RouteNeuronsAreCopiedWholeAtTheSameIndex()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Same(2, 3f)),
                Sum(1f),
                N(NeuronOp.Smooth, 2.5f, Same(3, 1f), Sense(SensorChannel.Chemical, 0.25f), Const(0.3f, 1f)),
                Sum(3f, Sense(SensorChannel.Flow, 1f)),
            });
            Genome b = Body(new NeuronDef[0], BaseHinge(4));
            Genome child = CrossAt(b, donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0), (1, 2), (1, 3) }, Route(r));
            Assert.Equal(0, r.Added);
            Assert.Equal(4, child.Nodes[1].Neurons.Length);
            SameNeuron(donor.Nodes[1].Neurons[2], child.Nodes[1].Neurons[2]);
            SameNeuron(donor.Nodes[1].Neurons[3], child.Nodes[1].Neurons[3]);
            SameNeuron(b.Nodes[1].Neurons[1], child.Nodes[1].Neurons[1]);
        }

        [Fact]
        public void AMissingNeuronIsAddedAndTheRoutesInputsRepointed()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Same(4, 2f)),
                Sum(1f),
                Sum(2f),
                Sum(3f),
                Sum(4f, Same(5, 3f), Same(1, 0.5f), Same(3, 0.1f)),
                Sum(5f, Sense(SensorChannel.Chemical, 1f)),
            });
            Genome b = Body(new NeuronDef[0], BaseHinge(2));
            Genome child = CrossAt(b, donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0), (1, 4), (1, 5) }, Route(r));
            Assert.Equal(2, r.Added);
            NeuronDef[] hinge = child.Nodes[1].Neurons;
            Assert.Equal(4, hinge.Length);

            // Donor 4 lands at 2 and donor 5 at 3; the output now reads 2, and 4's copy reads 3.
            Assert.Equal(4f, hinge[2].Bias);
            Assert.Equal(5f, hinge[3].Bias);
            SameInput(Same(2, 2f), hinge[0].Inputs.Last());
            SameInput(Same(3, 3f), hinge[2].Inputs[0]);

            // Every SameNode input points inside the part.
            foreach (NeuronDef n in hinge)
                foreach (NeuronInput i in n.Inputs)
                    if (i.Kind == NeuronInputKind.SameNode) Assert.InRange(i.Index, 0, hinge.Length - 1);
        }

        [Fact]
        public void AnInputToADonorNeuronNotCopiedIsKeptWhereTheBaseHasOneAndDroppedWhereNot()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Same(4, 2f)),
                Sum(1f),
                Sum(2f),
                Sum(3f),
                Sum(4f, Same(5, 3f), Same(1, 0.5f), Same(3, 0.1f)),
                Sum(5f, Sense(SensorChannel.Chemical, 1f)),
            });
            Genome child = CrossAt(Body(new NeuronDef[0], BaseHinge(2)), donor, 0, 0, out CrossoverRecord r);
            NeuronInput[] copied = child.Nodes[1].Neurons[2].Inputs;
            Assert.Equal(2, copied.Length);         // the input from donor 3 is dropped: the base has no neuron 3
            SameInput(Same(3, 3f), copied[0]);       // donor 5, re-pointed to its copy
            SameInput(Same(1, 0.5f), copied[1]);     // donor 1, not copied, kept: the base has a neuron 1
        }

        [Fact]
        public void TheOutputNeuronTakesOnlyItsOneInputFromTheRoute()
        {
            Genome b = Body(new NeuronDef[0], new[]
            {
                N(NeuronOp.Sum, -7f, Sense(SensorChannel.Depth, 0.5f), Same(1, 0.2f)),
                Sum(-1f),
            });
            Genome donor = Body(new NeuronDef[0], new[]
            {
                N(NeuronOp.Product, 7f, Sense(SensorChannel.Chemical, 2f), Sense(SensorChannel.Flow, 1f), Const(1f, 1f)),
                Sum(1f),
            });
            Genome child = CrossAt(b, donor, 0, 0, out CrossoverRecord r);
            Assert.Equal(new[] { (1, 0) }, Route(r));
            NeuronDef output = child.Nodes[1].Neurons[0];
            Assert.Equal(NeuronOp.Sum, output.Op);
            Assert.Equal(-7f, output.Bias);
            Assert.Equal(3, output.Inputs.Length);
            SameInput(Sense(SensorChannel.Depth, 0.5f), output.Inputs[0]);
            SameInput(Same(1, 0.2f), output.Inputs[1]);
            SameInput(Sense(SensorChannel.Chemical, 2f), output.Inputs[2]);
            Assert.True(r.OutputInputRead);
            SameNeuron(b.Nodes[1].Neurons[1], child.Nodes[1].Neurons[1]);
        }

        [Fact]
        public void TheOutputsInputReplacesOneFromTheSameSource()
        {
            Genome b = Body(new NeuronDef[0], new[]
            {
                N(NeuronOp.Sum, -7f, Sense(SensorChannel.Depth, 0.5f), Same(1, 0.2f)),
                Sum(-1f),
            });
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(7f, Sense(SensorChannel.Depth, 3f), Sense(SensorChannel.Flow, 1f)),
                Sum(1f),
            });
            Genome child = CrossAt(b, donor, 0, 0, out _);
            NeuronDef output = child.Nodes[1].Neurons[0];
            Assert.Equal(2, output.Inputs.Length);
            SameInput(Sense(SensorChannel.Depth, 3f), output.Inputs[0]);
            SameInput(Same(1, 0.2f), output.Inputs[1]);
        }

        [Fact]
        public void AnInputAddedPastAFixedArityIsRecordedAsUnread()
        {
            Genome b = Body(new NeuronDef[0], new[] { N(NeuronOp.Sigmoid, -7f, Sense(SensorChannel.Depth, 0.5f)), Sum(-1f) });
            Genome donor = Body(new NeuronDef[0], new[] { Sum(7f, Sense(SensorChannel.Chemical, 2f)), Sum(1f) });
            CrossAt(b, donor, 0, 0, out CrossoverRecord r);
            Assert.True(r.Transferred);
            Assert.False(r.OutputInputRead);
        }

        [Fact]
        public void ABaseWithoutTheOutputNeuronTakesNothing()
        {
            Genome b = Body(new NeuronDef[0], BaseHinge(1));
            Genome donor = Body(new NeuronDef[0], new[] { Sum(0f, Sense(SensorChannel.Depth, 1f)), Sum(1f, Sense(SensorChannel.Chemical, 1f)) });
            Genome child = CrossAt(b, donor, 1, 0, out CrossoverRecord r);
            Assert.Equal("base-output-missing", r.Refused);
            Assert.Equal(GenomeJson.Write(b), GenomeJson.Write(child));
        }

        [Fact]
        public void WithNoLimitMissingRouteNeuronsAreAddedPastTheFoundersBound()
        {
            // The tank passes no limit: a hinge of 2 neurons takes 2 more, past the founders' 3.
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Same(4, 2f)),
                Sum(1f),
                Sum(2f),
                Sum(3f),
                Sum(4f, Same(5, 3f)),
                Sum(5f, Sense(SensorChannel.Chemical, 1f)),
            });
            Genome b = Body(new NeuronDef[0], BaseHinge(2));
            Genome child = CrossAt(b, donor, 0, 0, out CrossoverRecord r);
            Assert.True(r.Transferred);
            Assert.Null(r.Refused);
            Assert.Equal(2, r.Added);
            Assert.Equal(4, child.Nodes[1].Neurons.Length);
            Assert.True(Crossover.BrainDistance(child, b) >= 2f);
        }

        [Fact]
        public void AnExplicitNeuronLimitStillRefusesTheCrossover()
        {
            Genome donor = Body(new NeuronDef[0], new[]
            {
                Sum(0f, Same(4, 2f)),
                Sum(1f),
                Sum(2f),
                Sum(3f),
                Sum(4f, Same(5, 3f)),
                Sum(5f, Sense(SensorChannel.Chemical, 1f)),
            });
            Genome b = Body(new NeuronDef[0], BaseHinge(2));

            Genome refused = CrossAt(b, donor, 0, 3, out CrossoverRecord r);
            Assert.Equal("limit", r.Refused);
            Assert.False(r.Transferred);
            Assert.Equal(GenomeJson.Write(b), GenomeJson.Write(refused));
            Assert.Equal(0f, Crossover.BrainDistance(refused, b));

            CrossAt(b, donor, 0, 4, out r);
            Assert.True(r.Transferred);
            Assert.Equal(2, r.Added);
        }

        // ------------------------------------------------------------ distance and draws

        [Fact]
        public void DistanceIsZeroForAnUntouchedCopyAndAboveZeroAfterACrossover()
        {
            Genome b = Body(new[] { Sum(-10f) }, BaseHinge(3));
            Assert.Equal(0f, Crossover.BrainDistance(b.Clone(), b));

            Genome donor = Body(new[] { Sum(10f) }, new[]
            {
                Sum(0f, Same(2, 3f)),
                Sum(1f),
                Sum(2f, Sense(SensorChannel.Chemical, 1f)),
            });
            Genome child = CrossAt(b, donor, 0, 0, out _);
            Assert.True(Crossover.BrainDistance(child, b) > 0f);
        }

        [Fact]
        public void DistanceCountsAnAddedNeuronAsAUnit()
        {
            Genome b = Body(new[] { Sum(-10f) }, BaseHinge(2));
            Genome g = b.Clone();
            g.Nodes[1].Neurons = g.Nodes[1].Neurons.Concat(new[] { Sum(9f, Sense(SensorChannel.Chemical, 1f)) }).ToArray();
            Assert.Equal(0f, SpeciesDistance.Between(g, b, 0f, 0f, 0f, 1f));   // Core's term alone does not see it
            Assert.True(Crossover.BrainDistance(g, b) >= 1f);
            Assert.Equal(Crossover.BrainDistance(g, b), Crossover.BrainDistance(b, g));
        }

        [Fact]
        public void DistanceCountsAnAddedInputAsAUnit()
        {
            Genome b = Body(new[] { Sum(-10f) }, BaseHinge(2));
            Genome g = b.Clone();
            NeuronDef n = g.Nodes[1].Neurons[0];
            n.Inputs = n.Inputs.Concat(new[] { Sense(SensorChannel.Chemical, 2f) }).ToArray();
            Assert.Equal(0f, SpeciesDistance.Between(g, b, 0f, 0f, 0f, 1f));
            Assert.True(Crossover.BrainDistance(g, b) >= 1f);
        }

        [Fact]
        public void DistanceIsZeroForAnUntouchedCopy()
        {
            Genome b = Body(new[] { Sum(-10f, Same(0, 1f)) }, BaseHinge(3));
            Assert.Equal(0f, Crossover.BrainDistance(b.Clone(), b));
        }

        [Fact]
        public void TheCrossoverDrawsOnlyFromTheRngItIsGiven()
        {
            Genome b = Body(new[] { Sum(-10f) }, BaseHinge(3));
            Genome donor = Body(new[] { Sum(10f, Sense(SensorChannel.Depth, 1f)) }, new[]
            {
                Sum(0f, Same(2, 3f)),
                Sum(1f, Parent(0, 1f)),
                Sum(2f, Sense(SensorChannel.Chemical, 1f)),
            });
            for (ulong s = 1; s <= 20; s++)
            {
                var r1 = new Rng(s);
                var r2 = new Rng(s);
                Genome a = Crossover.Cross(b, donor, r1, 0, out CrossoverRecord ra);
                Genome c = Crossover.Cross(b, donor, r2, 0, out CrossoverRecord rc);
                Assert.Equal(GenomeJson.Write(a), GenomeJson.Write(c));
                Assert.Equal(Route(ra), Route(rc));

                // Exactly one draw: the output, among the donor's two (power and bend).
                var reference = new Rng(s);
                reference.Range(2);
                Assert.Equal(reference.State, r1.State);
            }
        }

        [Fact]
        public void TheParentsAreNotChanged()
        {
            Genome b = Body(new[] { Sum(-10f) }, BaseHinge(2));
            Genome donor = Body(new[] { Sum(10f) }, new[]
            {
                Sum(0f, Same(4, 2f)), Sum(1f), Sum(2f), Sum(3f),
                Sum(4f, Same(5, 3f)), Sum(5f, Sense(SensorChannel.Chemical, 1f)),
            });
            string before = GenomeJson.Write(b), donorBefore = GenomeJson.Write(donor);
            CrossAt(b, donor, 0, 0, out _);
            Assert.Equal(before, GenomeJson.Write(b));
            Assert.Equal(donorBefore, GenomeJson.Write(donor));
        }
    }
}

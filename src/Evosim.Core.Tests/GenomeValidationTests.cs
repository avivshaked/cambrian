using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    public class GenomeValidationTests
    {
        [Fact]
        public void AWellFormedGenomeHasNoIssues()
        {
            Assert.Empty(Fixtures.SelfLoopSpine(3).Validate());
        }

        [Fact]
        public void AnEmptyGenomeIsRejected()
        {
            Assert.NotEmpty(new Genome().Validate());
        }

        [Fact]
        public void RootIndexMustBeInRange()
        {
            Genome g = Fixtures.SingleBox();
            g.RootIndex = 4;

            Assert.Contains(g.Validate(), i => i.Contains("RootIndex"));
        }

        [Fact]
        public void EdgesMustPointAtRealNodes()
        {
            Genome g = Fixtures.SingleBox();
            g.Nodes[0].Edges.Add(Fixtures.FaceToFace(9));

            Assert.Contains(g.Validate(), i => i.Contains("child 9"));
        }

        [Fact]
        public void JointLimitCountMustMatchTheJointsDof()
        {
            Genome g = Fixtures.SingleBox();
            g.Nodes[0].JointType = JointType.Spherical;
            g.Nodes[0].JointLimits = new[] { new Float2(-1f, 1f) };

            Assert.Contains(g.Validate(), i => i.Contains("3 DOF but 1 joint limits"));
        }

        [Fact]
        public void ResampleJointLimitsKeepsExistingEntriesAndFillsTheRest()
        {
            var node = Fixtures.Box(joint: JointType.Hinge);
            node.JointLimits = new[] { new Float2(-0.25f, 0.75f) };

            node.JointType = JointType.Spherical;
            node.ResampleJointLimits(new Float2(-1f, 1f));

            Assert.Equal(3, node.JointLimits.Length);
            Fixtures.AssertClose(-0.25f, node.JointLimits[0].X);
            Fixtures.AssertClose(0.75f, node.JointLimits[0].Y);
            Fixtures.AssertClose(-1f, node.JointLimits[1].X);
        }

        [Fact]
        public void DimensionsMustBePositive()
        {
            Genome g = Fixtures.SingleBox();
            g.Nodes[0].Dimensions = new Float3(0.5f, 0f, 0.5f);

            Assert.Contains(g.Validate(), i => i.Contains("positive half-extents"));
        }

        [Fact]
        public void DegenerateEdgeScaleIsRejected()
        {
            Genome g = Fixtures.SingleBox();
            MorphEdge edge = Fixtures.FaceToFace(0);
            edge.Scale = new Float3(1f, 0f, 1f);
            g.Nodes[0].Edges.Add(edge);

            Assert.Contains(g.Validate(), i => i.Contains("degenerate"));
        }

        [Fact]
        public void InvertedJointLimitsAreRejected()
        {
            Genome g = Fixtures.SingleBox();
            g.Nodes[0].JointType = JointType.Hinge;
            g.Nodes[0].JointLimits = new[] { new Float2(1f, -1f) };

            Assert.Contains(g.Validate(), i => i.Contains("inverted"));
        }

        [Fact]
        public void NeuronReferencesToMissingNeuronsAreRejected()
        {
            Genome g = Fixtures.SingleBox();
            g.Nodes[0].Neurons = new[]
            {
                new NeuronDef
                {
                    Op = NeuronOp.Sum,
                    Inputs = new[] { NeuronInput.FromNeuron(NeuronInputKind.SameNode, 3) },
                },
            };

            Assert.Contains(g.Validate(), i => i.Contains("SameNode input 3"));
        }

        [Fact]
        public void GlobalNeuronsCannotReadSensorsBecauseTheyOwnNoPart()
        {
            Genome g = Fixtures.SingleBox();
            g.GlobalBrain = new[]
            {
                new NeuronDef
                {
                    Op = NeuronOp.Sigmoid,
                    Inputs = new[] { NeuronInput.FromSensor(SensorChannel.JointAngle, 0) },
                },
            };

            Assert.Contains(g.Validate(), i => i.Contains("own no part"));
        }

        [Fact]
        public void CloneIsDeepAndIndependent()
        {
            Genome original = Fixtures.SelfLoopSpine(3);
            original.Nodes[0].Neurons = new[] { new NeuronDef { Op = NeuronOp.Sin, Frequency = 2f } };

            Genome copy = original.Clone();
            copy.Nodes[0].Dimensions = new Float3(9f, 9f, 9f);
            copy.Nodes[0].Neurons[0].Frequency = 99f;
            copy.Nodes[0].Edges[0].Scale = new Float3(0.1f, 0.1f, 0.1f);

            Fixtures.AssertClose(0.5f, original.Nodes[0].Dimensions.X);
            Fixtures.AssertClose(2f, original.Nodes[0].Neurons[0].Frequency);
            Fixtures.AssertClose(1f, original.Nodes[0].Edges[0].Scale.X);
        }

        [Fact]
        public void TheMvpOperatorSetIsTheCpgSubset()
        {
            // DESIGN.md §4.3 — a population constraint, not a separate system.
            Assert.Equal(5, NeuronOps.MvpSet.Length);
            Assert.Contains(NeuronOp.OscillateSaw, NeuronOps.MvpSet);
            Assert.True(NeuronOp.Sigmoid.IsInMvpSet());
            Assert.False(NeuronOp.Integrate.IsInMvpSet());
        }

        [Fact]
        public void OscillatorsTakeNoInputs()
        {
            Assert.Equal(0, NeuronOp.OscillateWave.Arity());
            Assert.Equal(0, NeuronOp.OscillateSaw.Arity());
            Assert.Equal(1, NeuronOp.Sin.Arity());
            Assert.Equal(3, NeuronOp.If.Arity());
        }
    }
}

using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Nursery;
using Xunit;

namespace Evosim.Nursery.Tests
{
    /// <summary>
    /// --evolve-envelope's copy (Envelope.cs) on a fixed root (node 0) with one edge to a hinge
    /// (node 1): the hinge's drive genes are taken, the root's never are.
    /// </summary>
    public class EnvelopeTests
    {
        private static Genome Body(DriveGenes root, DriveGenes hinge, JointType second = JointType.Hinge)
        {
            var g = new Genome { RootIndex = 0 };
            var r = new MorphNode { JointType = JointType.Fixed, Neurons = Array.Empty<NeuronDef>(), Drive = root };
            r.Edges.Add(new MorphEdge { Child = 1 });
            var h = new MorphNode
            {
                JointType = second,
                JointLimits = second == JointType.Fixed ? Array.Empty<Float2>() : new[] { new Float2(-1f, 1f) },
                Neurons = Array.Empty<NeuronDef>(),
                Drive = hinge,
            };
            g.Nodes.Add(r);
            g.Nodes.Add(h);
            return g;
        }

        private static readonly DriveGenes A = new DriveGenes(0.1f, 0.2f, 0.3f, 0.4f);
        private static readonly DriveGenes B = new DriveGenes(0.4f, 0.9f, 0.05f, 0.6f);

        private static void Same(DriveGenes x, DriveGenes y)
        {
            Assert.Equal(x.Threshold, y.Threshold);
            Assert.Equal(x.Reaction, y.Reaction);
            Assert.Equal(x.Hold, y.Hold);
            Assert.Equal(x.Release, y.Release);
        }

        [Fact]
        public void TakesTheJointsGenesAndLeavesTheRoots()
        {
            Genome into = Body(DriveGenes.Default, A);
            Genome from = Body(B, B);
            Envelope.Take(into, from);
            Same(DriveGenes.Default, into.Nodes[0].Drive);
            Same(B, into.Nodes[1].Drive);
            Same(B, from.Nodes[1].Drive);
        }

        [Fact]
        public void LeavesANodeThatIsAJointInOnlyOneOfTheTwo()
        {
            Genome into = Body(DriveGenes.Default, A);
            Envelope.Take(into, Body(DriveGenes.Default, B, JointType.Fixed));
            Same(A, into.Nodes[1].Drive);

            Genome fixedInto = Body(DriveGenes.Default, DriveGenes.Default, JointType.Fixed);
            Envelope.Take(fixedInto, Body(DriveGenes.Default, B));
            Same(DriveGenes.Default, fixedInto.Nodes[1].Drive);
        }

        [Fact]
        public void RefusesDifferentNodeCounts()
        {
            Genome one = Body(DriveGenes.Default, A);
            one.Nodes.RemoveAt(1);
            one.Nodes[0].Edges.Clear();
            Assert.Throws<ArgumentException>(() => Envelope.Take(Body(DriveGenes.Default, A), one));
        }

        [Fact]
        public void MeanCountsJointsOnly()
        {
            var m = Envelope.Mean(new List<Genome> { Body(B, A), Body(B, B) });
            Assert.Equal(2, m.joints);
            Assert.Equal((0.1 + 0.4) / 2, m.threshold, 6);
            Assert.Equal((0.2 + 0.9) / 2, m.reaction, 6);
            Assert.Equal((0.3 + 0.05) / 2, m.hold, 6);
            Assert.Equal((0.4 + 0.6) / 2, m.release, 6);

            var none = Envelope.Mean(new List<Genome> { Body(B, B, JointType.Fixed) });
            Assert.Equal(0, none.joints);
            Assert.True(double.IsNaN(none.threshold));
        }
    }
}

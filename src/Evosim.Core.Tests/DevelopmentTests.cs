using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Development is where recursive encodings go wrong in ways that are near-impossible
    /// to debug through a physics view — a limb in the wrong place looks much like a limb
    /// with a bad joint. These run headless, before anything touches Unity.
    /// </summary>
    public class DevelopmentTests
    {
        [Fact]
        public void ASingleNodeDevelopsToOnePartAtTheOrigin()
        {
            Phenotype p = Developer.Develop(Fixtures.SingleBox());

            Assert.Equal(1, p.PartCount);
            Assert.True(p.Parts[0].IsRoot);
            Assert.Equal(-1, p.Parts[0].ParentIndex);
            Assert.Equal(0, p.Parts[0].Depth);
            Fixtures.AssertClose(Float3.Zero, p.Parts[0].Position);
            Assert.False(p.WasTruncated);
        }

        [Fact]
        public void ASelfLoopWithLimitFiveYieldsAFiveSegmentSpine()
        {
            // DESIGN.md §4.1: "a self-loop with recursiveLimit = 5 yields a five-segment spine".
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(5));

            Assert.Equal(5, p.PartCount);
            Assert.Equal(4, p.MaxDepthReached);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(8, 8)]
        public void RecursiveLimitControlsSegmentCount(int limit, int expectedParts)
        {
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(limit));
            Assert.Equal(expectedParts, p.PartCount);
        }

        [Fact]
        public void SegmentsAreLaidOutFaceToFaceAlongTheAxis()
        {
            // Unit boxes, +X face to -X face: centres one metre apart.
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(4));

            for (int i = 0; i < p.PartCount; i++)
            {
                Fixtures.AssertClose(new Float3(i * 1f, 0f, 0f), p.Parts[i].Position);
            }
        }

        [Fact]
        public void ParentsAlwaysPrecedeChildren()
        {
            // Evosim.Sim builds ArticulationBody chains parent-first and depends on this.
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(6));

            foreach (PhenotypePart part in p.Parts)
            {
                Assert.True(part.ParentIndex < part.Index,
                    $"part {part.Index} has parent {part.ParentIndex}");
            }
        }

        [Fact]
        public void ScaleAccumulatesDownTheSubtree()
        {
            // [K12 §2.1, p.3]: transforms "are applied to the entire subtree of the
            // phenotype graph during its construction" — so scale compounds, it does not reset.
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(4, segmentScale: 0.5f));

            Assert.Equal(4, p.PartCount);
            Fixtures.AssertClose(0.5f, p.Parts[0].HalfExtents.X);
            Fixtures.AssertClose(0.25f, p.Parts[1].HalfExtents.X);
            Fixtures.AssertClose(0.125f, p.Parts[2].HalfExtents.X);
            Fixtures.AssertClose(0.0625f, p.Parts[3].HalfExtents.X);
        }

        [Fact]
        public void OneReflectionFlagProducesABilateralPair()
        {
            var g = new Genome();
            MorphNode root = Fixtures.Box();
            MorphNode limb = Fixtures.Box(0.25f, JointType.Hinge);

            MorphEdge edge = Fixtures.FaceToFace(1);
            edge.Reflect = new Bool3(true, false, false);
            root.Edges.Add(edge);

            g.Nodes.Add(root);
            g.Nodes.Add(limb);
            g.RootIndex = 0;

            Phenotype p = Developer.Develop(g);

            Assert.Equal(3, p.PartCount);

            PhenotypePart[] limbs = p.Parts.Where(x => x.SourceNode == 1).ToArray();
            Assert.Equal(2, limbs.Length);

            // Mirrored about the parent's YZ plane: same magnitude, opposite sign.
            Fixtures.AssertClose(-limbs[0].Position.X, limbs[1].Position.X);
            Assert.False(limbs[0].Mirrored);
            Assert.True(limbs[1].Mirrored);
        }

        [Theory]
        [InlineData(true, false, false, 2)]
        [InlineData(true, true, false, 4)]
        [InlineData(true, true, true, 8)]
        public void ReflectionFlagsMultiplyChildCopies(bool x, bool y, bool z, int copies)
        {
            var g = new Genome();
            MorphNode root = Fixtures.Box();
            MorphNode limb = Fixtures.Box(0.25f, JointType.Hinge);

            MorphEdge edge = Fixtures.FaceToFace(1);
            edge.Reflect = new Bool3(x, y, z);
            root.Edges.Add(edge);

            g.Nodes.Add(root);
            g.Nodes.Add(limb);
            g.RootIndex = 0;

            Phenotype p = Developer.Develop(g);

            Assert.Equal(copies, p.Parts.Count(part => part.SourceNode == 1));
        }

        [Fact]
        public void TerminalEdgesFireOnlyOnceRecursionIsSpent()
        {
            // [K12 §2.1, p.3]: terminalOnly "can be used to represent structures appearing
            // at the end of chains or repeating units" — one fin at the tip, not one per segment.
            var g = new Genome();
            MorphNode segment = Fixtures.Box(joint: JointType.Hinge, recursiveLimit: 4);
            MorphNode fin = Fixtures.Box(0.2f, JointType.Hinge);

            segment.Edges.Add(Fixtures.FaceToFace(0));

            MorphEdge terminal = Fixtures.FaceToFace(1);
            terminal.TerminalOnly = true;
            segment.Edges.Add(terminal);

            g.Nodes.Add(segment);
            g.Nodes.Add(fin);
            g.RootIndex = 0;

            Phenotype p = Developer.Develop(g);

            Assert.Equal(4, p.Parts.Count(x => x.SourceNode == 0));
            Assert.Equal(1, p.Parts.Count(x => x.SourceNode == 1));

            // And it is attached to the last segment, not the first.
            PhenotypePart finPart = p.Parts.Single(x => x.SourceNode == 1);
            Assert.Equal(3, p.Parts[finPart.ParentIndex].Depth);
        }

        [Fact]
        public void PartCapTruncatesAndSaysSo()
        {
            var limits = new DevelopmentLimits { MaxParts = 4, MaxDepth = 64 };
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(20), limits);

            Assert.Equal(4, p.PartCount);
            Assert.True(p.WasTruncated);
            Assert.True(p.PrunedForParts > 0);
        }

        [Fact]
        public void DepthCapTruncatesAndSaysSo()
        {
            var limits = new DevelopmentLimits { MaxParts = 64, MaxDepth = 3 };
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(20), limits);

            Assert.Equal(4, p.PartCount); // depths 0..3
            Assert.Equal(3, p.MaxDepthReached);
            Assert.True(p.PrunedForDepth > 0);
        }

        [Fact]
        public void PartsBelowMinimumVolumeArePruned()
        {
            // [K12 §2.3, p.7]: "extremely small body parts cause instability in the physical
            // engine." Cumulative scale reaches sub-millimetre boxes quickly.
            var limits = new DevelopmentLimits { MaxParts = 64, MaxDepth = 64, MinPartVolume = 0.05f };
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(20, segmentScale: 0.5f), limits);

            Assert.True(p.PrunedForVolume > 0);
            Assert.All(p.Parts, part => Assert.True(part.Volume >= limits.MinPartVolume));
        }

        [Fact]
        public void DevelopmentIsDeterministic()
        {
            Genome g = Fixtures.SelfLoopSpine(5, segmentScale: 0.8f);

            Phenotype a = Developer.Develop(g);
            Phenotype b = Developer.Develop(g);

            Assert.Equal(a.PartCount, b.PartCount);
            for (int i = 0; i < a.PartCount; i++)
            {
                Assert.Equal(a.Parts[i].SourceNode, b.Parts[i].SourceNode);
                Assert.Equal(a.Parts[i].ParentIndex, b.Parts[i].ParentIndex);
                Assert.Equal(a.Parts[i].Position, b.Parts[i].Position);
                Assert.Equal(a.Parts[i].HalfExtents, b.Parts[i].HalfExtents);
            }
        }

        [Fact]
        public void DevelopingDoesNotMutateTheGenome()
        {
            Genome g = Fixtures.SelfLoopSpine(5);
            int nodesBefore = g.Nodes.Count;
            int edgesBefore = g.Nodes[0].Edges.Count;

            Developer.Develop(g);

            Assert.Equal(nodesBefore, g.Nodes.Count);
            Assert.Equal(edgesBefore, g.Nodes[0].Edges.Count);
            Assert.Empty(g.Validate());
        }

        [Fact]
        public void JointLimitsTravelFromNodeToPart()
        {
            var g = new Genome();
            MorphNode root = Fixtures.Box();
            MorphNode child = Fixtures.Box(0.4f, JointType.Spherical);
            child.JointLimits = new[] { new Float2(-0.5f, 0.5f), new Float2(-1f, 1f), new Float2(-2f, 2f) };

            root.Edges.Add(Fixtures.FaceToFace(1));
            g.Nodes.Add(root);
            g.Nodes.Add(child);
            g.RootIndex = 0;

            Phenotype p = Developer.Develop(g);

            PhenotypePart part = p.Parts.Single(x => x.SourceNode == 1);
            Assert.Equal(JointType.Spherical, part.JointType);
            Assert.Equal(3, part.JointLimits.Length);
            Fixtures.AssertClose(2f, part.JointLimits[2].Y);
            Assert.Equal(3, p.TotalDof);
        }

        [Fact]
        public void PartsShareTheirSourceNodesNeurons()
        {
            // The point of neurons living inside morph nodes: recursion duplicates the
            // segment's controller with the segment, which is what makes a repeated chain a
            // central pattern generator (DESIGN.md §4.3).
            Genome g = Fixtures.SelfLoopSpine(4);
            g.Nodes[0].Neurons = new[]
            {
                new NeuronDef { Op = NeuronOp.OscillateWave, Frequency = 1.5f },
            };

            Phenotype p = Developer.Develop(g);

            Assert.Equal(4, p.PartCount);
            Assert.All(p.Parts, part =>
            {
                Assert.Single(part.Neurons);
                Assert.Equal(NeuronOp.OscillateWave, part.Neurons[0].Op);
            });
        }

        [Fact]
        public void AnInvalidGenomeIsRejectedRatherThanDevelopedBadly()
        {
            var g = new Genome();
            MorphNode root = Fixtures.Box();
            root.Edges.Add(Fixtures.FaceToFace(7)); // no such node
            g.Nodes.Add(root);
            g.RootIndex = 0;

            Assert.Throws<System.ArgumentException>(() => Developer.Develop(g));
        }
    }
}

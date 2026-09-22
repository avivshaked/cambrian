using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Development is where recursive encodings go wrong in ways that are near-impossible
    /// to debug through a physics view — a limb in the wrong place looks much like a limb
    /// with a bad joint. These run headless, before anything touches Unity.
    /// </summary>
    public class DevelopmentTests
    {
        private readonly ITestOutputHelper _output;

        public DevelopmentTests(ITestOutputHelper output) => _output = output;

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

        // ---------------------------------------------------------------- the module gene (D106)

        [Fact]
        public void ADeterminateGenomeDevelopsIdenticallyWithAndWithoutTheCounts()
        {
            // Rule 2's acceptance, and the reason the regress against the recorded world can be
            // expected to hold at all: a determinate node ignores the array, so every genome in
            // the record develops part for part into the body it always did whether the counts
            // are supplied or not. Asked of forty random founders rather than of one fixture,
            // because what has to be true is a property of the developer and not of a spine.
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Genome g = GenomeFactory.Random(new Rng(seed));

                var counts = new int[g.Nodes.Count];
                for (int n = 0; n < counts.Length; n++) counts[n] = g.Nodes[n].RecursiveLimit + 3;

                Phenotype without = Developer.Develop(g);
                Phenotype with = Developer.Develop(g, null, null, null, counts);

                Assert.Equal(without.PartCount, with.PartCount);

                for (int i = 0; i < without.PartCount; i++)
                {
                    PhenotypePart a = without.Parts[i];
                    PhenotypePart b = with.Parts[i];

                    Assert.Equal(a.SourceNode, b.SourceNode);
                    Assert.Equal(a.ParentIndex, b.ParentIndex);
                    Assert.Equal(a.Depth, b.Depth);
                    Assert.Equal(a.CellTypeId, b.CellTypeId);
                    Assert.Equal(a.JointType, b.JointType);
                    Fixtures.AssertClose(a.HalfExtents, b.HalfExtents, 0f);
                    Fixtures.AssertClose(a.Position, b.Position, 0f);
                }
            }
        }

        [Theory]
        [InlineData(2, 2)]
        [InlineData(4, 4)]
        [InlineData(7, 7)]
        public void ACountAboveTheRecursiveLimitAddsModulesToAnIndeterminateNode(
            int count, int expectedParts)
        {
            // Rule 2's other half. The same spine, with the node made indeterminate and the
            // body's own count standing in for the limit: one more segment per module, up to
            // MaxParts, which is what a module *is* — the node's own expansion, placed where the
            // genome already says to place it.
            Genome g = Fixtures.SelfLoopSpine(recursiveLimit: 1);
            g.Nodes[0].Growth = ModuleGrowth.Indeterminate;
            g.Nodes[0].MaxModules = 16;

            Phenotype born = Developer.Develop(g);
            Assert.Equal(1, born.PartCount);

            Phenotype grown = Developer.Develop(g, null, null, null, new[] { count });

            _output.WriteLine($"count {count}: {born.PartCount} -> {grown.PartCount} parts");
            Assert.Equal(expectedParts, grown.PartCount);
        }

        [Fact]
        public void ACountBelowTheGenomesOwnLimitIsReadAsTheLimit()
        {
            // Developer.CountFor's second rule. RecursiveLimit and MaxModules both mutate, so a
            // stored count can find itself under a limit that has moved up; taking the larger
            // means development always builds at least the body the genome describes.
            Genome g = Fixtures.SelfLoopSpine(recursiveLimit: 4);
            g.Nodes[0].Growth = ModuleGrowth.Indeterminate;

            Phenotype p = Developer.Develop(g, null, null, null, new[] { 1 });
            Assert.Equal(4, p.PartCount);
        }

        [Fact]
        public void ThePartPathsIdentifyTheSamePartAcrossACountChange()
        {
            // Developer.MatchParts, on the genome shape that breaks the obvious reading. The root
            // has an indeterminate child A and a second child B, so a module of A is inserted in
            // the middle of the depth-first order and B's index moves — which is exactly what an
            // index-based map would get wrong, and why the map is on paths.
            var g = new Genome { RootIndex = 0 };

            MorphNode root = Fixtures.Box();
            MorphNode a = Fixtures.Box(half: 0.3f, recursiveLimit: 2);
            MorphNode b = Fixtures.Box(half: 0.2f);

            a.Growth = ModuleGrowth.Indeterminate;
            a.MaxModules = 6;

            root.Edges.Add(Fixtures.FaceToFace(1));
            root.Edges.Add(Fixtures.FaceToFace(2));
            a.Edges.Add(Fixtures.FaceToFace(1));

            g.Nodes.Add(root);
            g.Nodes.Add(a);
            g.Nodes.Add(b);

            var before = new System.Collections.Generic.List<int[]>();
            var after = new System.Collections.Generic.List<int[]>();

            Phenotype was = Developer.Develop(g, null, null, null, new[] { 1, 2, 1 }, before);
            Phenotype now = Developer.Develop(g, null, null, null, new[] { 1, 3, 1 }, after);

            _output.WriteLine($"{was.PartCount} parts -> {now.PartCount} parts");
            Assert.Equal(was.PartCount + 1, now.PartCount);

            int[] map = Developer.MatchParts(before, after);

            // The added module is the only part with no ancestor in the old body, and everything
            // else maps to the part with its own source node — which for B is an index that has
            // moved.
            int added = 0;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] < 0) { added++; continue; }

                Assert.Equal(was.Parts[map[i]].SourceNode, now.Parts[i].SourceNode);
                Assert.Equal(was.Parts[map[i]].Depth, now.Parts[i].Depth);
            }

            Assert.Equal(1, added);

            // The part that proves it: B is at a different index in the two bodies and is still
            // matched, which an index-for-index map could not do.
            int bBefore = IndexOfSourceNode(was, 2);
            int bAfter = IndexOfSourceNode(now, 2);

            _output.WriteLine($"B moved from index {bBefore} to index {bAfter}");
            Assert.NotEqual(bBefore, bAfter);
            Assert.Equal(bBefore, map[bAfter]);
        }

        private static int IndexOfSourceNode(Phenotype p, int node)
        {
            for (int i = 0; i < p.PartCount; i++)
            {
                if (p.Parts[i].SourceNode == node) return i;
            }

            return -1;
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

        // ------------------------------------------------------------------ D099, 2026-09-19

        [Fact]
        public void ATerminalOnlySelfEdgeStopsAtItsOwnRecursiveLimit()
        {
            // 2b. Until D099 a terminal edge skipped CanEnter entirely, so a self-edge marked
            // terminal unfolded to MaxDepth whatever its node's limit said. One node, limit 1,
            // one terminal self-edge: the node itself and nothing else.
            var g = new Genome();
            MorphNode node = Fixtures.Box(recursiveLimit: 1);
            MorphEdge terminal = Fixtures.FaceToFace(0);
            terminal.TerminalOnly = true;
            node.Edges.Add(terminal);
            g.Nodes.Add(node);
            g.RootIndex = 0;

            Phenotype p = Developer.Develop(g);

            Assert.Equal(1, p.PartCount);
            Assert.Equal(0, p.MaxDepthReached);
        }

        [Fact]
        public void ATerminalOnlySelfEdgeStillGrowsToItsLimitWhenItHasOne()
        {
            // The rule is the limit, not a ban: a terminal self-edge on a node that may be
            // entered three times still lays three segments down, and stops there rather than
            // at MaxDepth.
            var g = new Genome();
            MorphNode node = Fixtures.Box(recursiveLimit: 3);
            MorphEdge terminal = Fixtures.FaceToFace(0);
            terminal.TerminalOnly = true;
            node.Edges.Add(terminal);
            g.Nodes.Add(node);
            g.RootIndex = 0;

            Phenotype p = Developer.Develop(g, new DevelopmentLimits { MaxParts = 32, MaxDepth = 16 });

            Assert.Equal(3, p.PartCount);
            Assert.Equal(2, p.MaxDepthReached);
        }

        [Fact]
        public void ANonTerminalSelfEdgeIsUntouchedByTheNewRule()
        {
            // The other half of 2b: nothing changes for an ordinary recursive spine, which was
            // already asking CanEnter. Stated again here beside its terminal twin, because the
            // two now differ only in when they fire.
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(3));

            Assert.Equal(3, p.PartCount);
            Assert.Equal(2, p.MaxDepthReached);
        }

        [Theory]
        [InlineData("knot-16-r41c-s3-1341.json", 3)]
        [InlineData("knot-9-r41c-s3-937.json", 2)]
        [InlineData("knot-16-jointed-r41b-s1-1632.json", 3)]
        public void TheRecordedKnotsCollapseUnderTheNewRule(string file, int expectedParts)
        {
            // The three bodies round 41c and 41b were stopped on (logbook/0107), taken from
            // their own snapshots and kept under inocula/. Each grew a ball of nine or sixteen
            // links off a terminal-only self-edge on a node with a recursive limit of 1. Under
            // 2b each is what its limits always said it was: a root and the one or two children
            // its edges legitimately reach.
            //
            // Two, not one, where a node carries two edges to the same child or one edge with a
            // reflection: both are ordinary edges entering a child that may be entered once.
            Genome genome = LoadInoculum(file);
            if (genome == null) return;

            // The runs' own development limits, which are DevelopmentLimits.Default exactly:
            // maxParts 16, maxDepth 8, minPartVolume 1e-4, maxPartVolume 1e6, minHalfExtent 0.01.
            Phenotype p = Developer.Develop(genome, DevelopmentLimits.Default);

            _output.WriteLine(
                $"{file}: {p.PartCount} parts, lit {p.TotalLitArea:0.######} m2, " +
                $"silhouette {p.SilhouetteArea:0.######} m2, factor {p.LitAreaFactor(true):0.####}");

            Assert.Equal(expectedParts, p.PartCount);
            Assert.False(p.WasTruncated);
        }

        [Fact]
        public void AOnePartBoxIsAllSilhouetteAndTheCapNeverTouchesIt()
        {
            // 2a's own safety rail: an honest body, laid out in the open, has a hull equal to
            // itself, so capping it changes nothing. If this ever fails the cap is a tax rather
            // than a ceiling.
            Phenotype p = Developer.Develop(Fixtures.SingleBox());

            Assert.Equal(0, p.SilhouetteFellBackToBox);
            Fixtures.AssertClose(p.TotalLitArea, p.SilhouetteArea, 1e-6f);
            Assert.Equal(1f, p.LitAreaFactor(capOn: true));
            Assert.Equal(1f, p.LitAreaFactor(capOn: false));
            Assert.Equal(p.TotalLitArea, p.EffectiveLitArea(capOn: true));
        }

        [Fact]
        public void ASixteenPartKnotEarnsAFractionOfWhatItsPartsAddUpTo()
        {
            // Sixteen boxes exactly on top of each other: sixteen parts' lit area, one part's
            // shadow, so the factor is exactly one sixteenth. The spec asks for under 0.35 and
            // the real round 41c knot read 0.275 under the old development rule; this states the
            // arithmetic where it can be checked by hand.
            // Explicit limits: the default caps depth at 8, and a knot grown down a self-edge
            // is a chain, so sixteen segments need sixteen levels to exist at all.
            Phenotype p = Developer.Develop(
                Fixtures.CoincidentKnot(16), new DevelopmentLimits { MaxParts = 16, MaxDepth = 16 });

            Assert.Equal(16, p.PartCount);
            Assert.Equal(0, p.SilhouetteFellBackToBox);

            float onePart = p.Parts[0].LitArea;
            Fixtures.AssertClose(16f * onePart, p.TotalLitArea, 1e-5f);
            Fixtures.AssertClose(onePart, p.SilhouetteArea, 1e-5f);

            _output.WriteLine(
                $"lit {p.TotalLitArea:0.######} m2, silhouette {p.SilhouetteArea:0.######} m2, " +
                $"factor {p.LitAreaFactor(true):0.######}");

            Assert.True(p.LitAreaFactor(capOn: true) < 0.35f);
            Fixtures.AssertClose(1f / 16f, p.LitAreaFactor(capOn: true), 1e-5f);
            Assert.Equal(1f, p.LitAreaFactor(capOn: false));
        }

        /// <summary>
        /// A scale of exactly 1 is the body itself, not a copy that reads as one: every caller
        /// takes the cube root of a volume fraction, and a fraction within a float's rounding of
        /// 1 gives a linear scale of exactly 1f. A copy at 1f has the adult's every number and none
        /// of its identity — World.Grow's ReferenceEquals reads it as not adult and the checkpoint
        /// writer refused it (round 45's first launch, 2026-09-22). Pinned here so the identity
        /// stays load-bearing.
        /// </summary>
        [Fact]
        public void ScaledByExactlyOneIsTheSameBody()
        {
            Phenotype adult = Developer.Develop(Fixtures.SingleLeaf());

            Assert.Same(adult, adult.Scaled(1f));
            Assert.NotSame(adult, adult.Scaled(0.99999f));

            // The fraction a body one growth step from adult reaches: its cube root rounds to 1f
            // in single precision, and the body must be the adult and not a copy.
            float linear = (float)System.Math.Pow(1d - 1e-9d, 1d / 3d);
            Assert.Equal(1f, linear);
            Assert.Same(adult, adult.Scaled(linear));
        }

        [Fact]
        public void AScaledBodysSilhouetteScalesWithTheSquareOfItsLength()
        {
            // An area, so it goes as the square — and it is carried rather than rebuilt, which
            // is what keeps growth from paying for a hull on every step.
            Phenotype adult = Developer.Develop(Fixtures.CoincidentKnot(4));
            Phenotype young = adult.Scaled(0.5f);

            Fixtures.AssertClose(0.25f * adult.SilhouetteArea, young.SilhouetteArea, 1e-6f);

            // And the factor is scale-free, which is why a growing body is not quietly taxed
            // more or less than its adult.
            Fixtures.AssertClose(
                adult.LitAreaFactor(capOn: true), young.LitAreaFactor(capOn: true), 1e-5f);
        }

        /// <summary>
        /// A genome from <c>inocula/</c>, or null when the test is running somewhere the repo is
        /// not laid out as expected — the same graceful skip AbsorptiveLogTests uses for its
        /// reader script.
        /// </summary>
        private Genome LoadInoculum(string file)
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..",
                "inocula", file));

            if (!System.IO.File.Exists(path))
            {
                _output.WriteLine($"no genome at {path} — skipped");
                return null;
            }

            return GenomeJson.Read(System.IO.File.ReadAllText(path));
        }
    }
}

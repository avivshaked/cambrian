using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    public class OverlapTests
    {
        private readonly ITestOutputHelper _output;

        public OverlapTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void FaceToFaceSegmentsDoNotOverlap()
        {
            var report = PhenotypeGeometry.MeasureOverlap(Developer.Develop(Fixtures.SelfLoopSpine(5)));

            Fixtures.AssertClose(0f, report.JointedVolume, 1e-3f);
            Fixtures.AssertClose(0f, report.UnjointedVolume, 1e-3f);
        }

        [Fact]
        public void ACoincidentChildIsMeasuredAsFullyOverlapping()
        {
            // Anchors at the parent's centre put the child exactly on top of it.
            var g = new Genome();
            MorphNode root = Fixtures.Box();
            MorphNode child = Fixtures.Box(0.5f, JointType.Hinge);

            var edge = new MorphEdge { Child = 1, ParentAnchor = Float3.Zero, ChildAnchor = Float3.Zero };
            root.Edges.Add(edge);

            g.Nodes.Add(root);
            g.Nodes.Add(child);
            g.RootIndex = 0;

            PhenotypeGeometry.OverlapReport report =
                PhenotypeGeometry.MeasureOverlap(Developer.Develop(g), samplesPerAxis: 10);

            // Identical boxes in the same place: one whole part's volume, and it is jointed.
            Fixtures.AssertClose(1f, report.JointedVolume, 0.05f);
            Fixtures.AssertClose(0f, report.UnjointedVolume, 1e-3f);
        }

        [Fact]
        public void OverlapBetweenUnjointedPartsIsCountedSeparately()
        {
            // Two children on the same parent face land on each other. They are siblings, so
            // nothing connects them — this is the case that reads as physically impossible.
            var g = new Genome();
            MorphNode root = Fixtures.Box();
            MorphNode limb = Fixtures.Box(0.5f, JointType.Hinge);

            root.Edges.Add(Fixtures.FaceToFace(1));
            root.Edges.Add(Fixtures.FaceToFace(1));

            g.Nodes.Add(root);
            g.Nodes.Add(limb);
            g.RootIndex = 0;

            PhenotypeGeometry.OverlapReport report =
                PhenotypeGeometry.MeasureOverlap(Developer.Develop(g), samplesPerAxis: 10);

            _output.WriteLine(report.ToString());
            Assert.True(report.UnjointedVolume > 0.5f,
                $"two siblings in the same place should overlap substantially, got {report}");
        }

        [Fact]
        public void RandomPopulationOverlapIsMeasured()
        {
            // No assertion on the value yet — this exists to report the number honestly.
            // BuriedPartPairs reported 0/400 while boxes were still visibly slicing through
            // each other, because a centre-in-box test cannot see a deep partial overlap.
            const int samples = 200;
            int withUnjointed = 0;
            float worst = 0f;
            double totalFraction = 0;

            int withBigJointed = 0;
            float worstJointed = 0f;
            double totalJointed = 0;

            for (ulong seed = 1; seed <= samples; seed++)
            {
                Genome genome = GenomeFactory.RandomViable(new Rng(seed), minParts: 3);
                Phenotype p = Developer.Develop(genome);

                PhenotypeGeometry.OverlapReport report = PhenotypeGeometry.MeasureOverlap(p, samplesPerAxis: 6);

                if (report.UnjointedFraction > 0.01f) withUnjointed++;
                if (report.UnjointedFraction > worst) worst = report.UnjointedFraction;
                totalFraction += report.UnjointedFraction;

                float jointedFraction = report.TotalVolume > 1e-9f
                    ? report.JointedVolume / report.TotalVolume : 0f;
                totalJointed += jointedFraction;
                if (jointedFraction > worstJointed) worstJointed = jointedFraction;
                if (jointedFraction > 0.05f) withBigJointed++;
            }

            _output.WriteLine(
                $"UNJOINTED (two solids passing through each other): " +
                $"{withUnjointed}/{samples} over 1%; mean {totalFraction / samples:P2}, worst {worst:P1}.");
            _output.WriteLine(
                $"JOINTED (parent/child at a joint, permitted by §4.2): " +
                $"{withBigJointed}/{samples} over 5%; mean {totalJointed / samples:P2}, worst {worstJointed:P1}.");
        }
    }
}

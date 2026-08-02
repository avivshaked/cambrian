using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// A genome can be perfectly valid and still develop into something that reads as wrong.
    /// These measure that, because "looks right" is otherwise only checkable by a human
    /// pressing Play — which is how the last two real bugs were actually found.
    /// </summary>
    public class GeometryQualityTests
    {
        private readonly ITestOutputHelper _output;

        public GeometryQualityTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void APointInsideABoxIsDetected()
        {
            Phenotype p = Developer.Develop(Fixtures.SingleBox());
            PhenotypePart box = p.Parts[0];   // half-extent 0.5 at the origin

            Assert.True(box.ContainsPoint(Float3.Zero));
            Assert.True(box.ContainsPoint(new Float3(0.49f, -0.49f, 0.49f)));
            Assert.False(box.ContainsPoint(new Float3(0.51f, 0f, 0f)));
        }

        [Fact]
        public void ContainmentRespectsPartRotation()
        {
            // A long thin box rotated 90 degrees about Z contains points along Y, not X.
            var g = new Genome();
            MorphNode node = Fixtures.Box();
            node.Dimensions = new Float3(1f, 0.1f, 0.1f);
            g.Nodes.Add(node);
            g.RootIndex = 0;

            Mat4 rotated = Mat4.Rotate(Quat.FromAxisAngle(new Float3(0f, 0f, 1f), (float)System.Math.PI / 2f));
            PhenotypePart part = Developer.Develop(g, null, rotated).Parts[0];

            Assert.True(part.ContainsPoint(new Float3(0f, 0.9f, 0f)));
            Assert.False(part.ContainsPoint(new Float3(0.9f, 0f, 0f)));
        }

        [Fact]
        public void FaceToFaceSegmentsAreNotBuriedInEachOther()
        {
            // Unit boxes meeting surface to surface: touching, never containing.
            Phenotype p = Developer.Develop(Fixtures.SelfLoopSpine(6));
            Assert.Equal(0, PhenotypeGeometry.BuriedPartPairs(p));
        }

        [Fact]
        public void AChildRotatedBackOntoItsParentIsDetectedAsBuried()
        {
            // Half a turn about the contact point puts the child straight through the parent.
            // This is what an unbounded random edge orientation produces, and what the tilt
            // limit in RandomGenomeOptions exists to avoid.
            var g = new Genome();
            MorphNode root = Fixtures.Box();
            MorphNode child = Fixtures.Box(0.5f, JointType.Hinge);

            MorphEdge edge = Fixtures.FaceToFace(1);
            edge.Orientation = Quat.FromAxisAngle(new Float3(0f, 1f, 0f), (float)System.Math.PI);
            root.Edges.Add(edge);

            g.Nodes.Add(root);
            g.Nodes.Add(child);
            g.RootIndex = 0;

            Assert.True(PhenotypeGeometry.BuriedPartPairs(Developer.Develop(g)) > 0);
        }

        [Fact]
        public void RandomGenomesRarelyBuryPartsInsideOtherParts()
        {
            // The population-level guard. Boxes visibly inside boxes were the first thing a
            // human noticed about the sandbox, and at Milestone 2 they stop being cosmetic:
            // per-part fluid forces would let a stack of coincident parts collect thrust
            // several times over for one body's worth of volume.
            const int samples = 400;
            int withBuried = 0;
            int totalPairs = 0;

            for (ulong seed = 1; seed <= samples; seed++)
            {
                Genome genome = GenomeFactory.RandomViable(new Rng(seed), minParts: 3);
                Phenotype p = Developer.Develop(genome);

                int buried = PhenotypeGeometry.BuriedPartPairs(p);
                totalPairs += buried;
                if (buried > 0) withBuried++;
            }

            float fraction = withBuried / (float)samples;
            _output.WriteLine($"{withBuried}/{samples} creatures have a buried part ({fraction:P1}); {totalPairs} pairs total.");

            // Measured 0/400 once RandomViable filtered on the developed creature. The bar is
            // 5% rather than 0 because the filter gives up after a fixed number of attempts
            // and returns its least-bad candidate rather than failing — a genome that cannot
            // avoid burial is still a genome.
            //
            // For reference, this was 69.7% when the generator drew edge orientations from all
            // of SO(3) and chose reflection axes independently of the attachment axis.
            Assert.True(fraction < 0.05f,
                $"{fraction:P1} of random creatures bury a part inside another; expected under 5%.");
        }

        [Fact]
        public void ReflectionActuallyReachesThePopulation()
        {
            // primer/01 argues reflection is what makes creatures read as organisms rather
            // than debris. That argument is worthless if the generator almost never uses it.
            const int samples = 200;
            int withMirroredParts = 0;

            for (ulong seed = 1; seed <= samples; seed++)
            {
                Genome genome = GenomeFactory.RandomViable(new Rng(seed), minParts: 3);
                Phenotype p = Developer.Develop(genome);
                if (PhenotypeGeometry.MirroredPartCount(p) > 0) withMirroredParts++;
            }

            float fraction = withMirroredParts / (float)samples;
            _output.WriteLine($"{withMirroredParts}/{samples} creatures contain mirrored parts ({fraction:P1}).");

            Assert.True(fraction > 0.2f,
                $"only {fraction:P1} of random creatures show any reflection; symmetry is not reaching the population.");
        }
    }
}

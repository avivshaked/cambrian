using System;
using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The mouth point: one function the solver, the world's reach test and the theatre all call,
    /// so these pin each shape, the root, the single part, a mirrored part, a grown body and the
    /// pose, once.
    /// </summary>
    public class MouthPointTests
    {
        private const float Root = 0.5f;

        // A box root of half-extent 0.5 and one child of the given shape hung from its +X face.
        // The child's anchor is whatever the test asks for.
        private static Genome RootAndChild(
            string shapeId, Float3 dimensions, Float3 childAnchor, Bool3? reflect = null)
        {
            var g = new Genome();
            MorphNode root = Fixtures.Box(Root);
            MorphNode limb = Fixtures.Box(0.25f, JointType.Hinge);
            limb.ShapeId = shapeId;
            limb.Dimensions = dimensions;

            MorphEdge edge = Fixtures.FaceToFace(1);
            edge.ChildAnchor = childAnchor;
            if (reflect.HasValue) edge.Reflect = reflect.Value;
            root.Edges.Add(edge);

            g.Nodes.Add(root);
            g.Nodes.Add(limb);
            g.RootIndex = 0;
            return g;
        }

        private static Genome BoxChild(Float3 dimensions, Float3 childAnchor, Bool3? reflect = null) =>
            RootAndChild(ShapeIds.Box, dimensions, childAnchor, reflect);

        // ---------------------------------------------------------------- which parts eat

        [Fact]
        public void APartWithIntakeAndNoAttackEats()
        {
            Phenotype p = Developer.Develop(
                Fixtures.ArmedBox(attack: 0f, intake: 1f, cellTypeId: CellTypeIds.Consumer));

            Assert.True(MouthPoint.IsEatingPart(p.Parts[0]));
        }

        [Fact]
        public void APartWithAttackAndNoIntakeDoesNotEat()
        {
            Phenotype p = Developer.Develop(
                Fixtures.ArmedBox(attack: 1f, intake: 0f, cellTypeId: CellTypeIds.Consumer));

            Assert.False(MouthPoint.IsEatingPart(p.Parts[0]));
        }

        [Fact]
        public void APartWithNeitherDoesNotEat()
        {
            Phenotype p = Developer.Develop(Fixtures.SingleBox());

            Assert.False(MouthPoint.IsEatingPart(p.Parts[0]));
        }

        // ---------------------------------------------------------------- the shapes

        [Fact]
        public void ABoxHungByItsMinusXFaceHasItsMouthAtTheCentreOfItsPlusXFace()
        {
            Phenotype p = Developer.Develop(BoxChild(new Float3(0.25f, 0.25f, 0.25f), new Float3(-1f, 0f, 0f)));

            // The anchor is on -x, so the direction from joint to centre is +x.
            Fixtures.AssertClose(new Float3(-0.25f, 0f, 0f), p.Parts[1].ChildAnchorLocal);
            Fixtures.AssertClose(new Float3(0.25f, 0f, 0f), MouthPoint.Local(p, 1));
        }

        [Fact]
        public void ABoxTakesTheFaceOfItsLargestComponentWithTheSignOfThatComponent()
        {
            // Anchored on the +Y face and a little towards +X: the mouth is out of the -Y face,
            // and the half-extent of that axis is what is read.
            Phenotype p = Developer.Develop(BoxChild(new Float3(0.2f, 0.3f, 0.25f), new Float3(0.2f, 1f, 0f)));

            Fixtures.AssertClose(new Float3(0f, -0.3f, 0f), MouthPoint.Local(p, 1));
        }

        [Fact]
        public void ABoxBreaksATieBetweenAxesTowardsTheLowestIndex()
        {
            // d = (0.25, 0.25, 0): x and y tie, so x.
            Phenotype p = Developer.Develop(BoxChild(new Float3(0.25f, 0.25f, 0.25f), new Float3(-1f, -1f, 0f)));

            Fixtures.AssertClose(new Float3(0.25f, 0f, 0f), MouthPoint.Local(p, 1));
        }

        [Fact]
        public void ASphereHasItsMouthOnItsSurfaceAlongTheDirectionAtItsRadius()
        {
            const float radius = 0.3f;
            Phenotype p = Developer.Develop(RootAndChild(
                ShapeIds.Sphere, new Float3(radius, radius, radius), new Float3(-1f, -1f, 0f)));

            float s = 1f / (float)Math.Sqrt(2.0);
            Float3 expected = new Float3(s * radius, s * radius, 0f);

            Float3 mouth = MouthPoint.Local(p, 1);
            Fixtures.AssertClose(expected, mouth);
            Fixtures.AssertClose(radius, mouth.Magnitude);
        }

        [Fact]
        public void ACapsuleHasItsMouthAtTheTipOfTheFarCap()
        {
            // Radius 0.2, half-length 0.5 including the cap, long axis y.
            var dims = new Float3(0.2f, 0.5f, 0.2f);

            // Hung by its -Y tip: the far tip is +Y.
            Phenotype lower = Developer.Develop(RootAndChild(ShapeIds.Capsule, dims, new Float3(0f, -1f, 0f)));
            Fixtures.AssertClose(new Float3(0f, 0.5f, 0f), MouthPoint.Local(lower, 1));

            // Hung by its +Y tip: the far tip is -Y.
            Phenotype upper = Developer.Develop(RootAndChild(ShapeIds.Capsule, dims, new Float3(0f, 1f, 0f)));
            Fixtures.AssertClose(new Float3(0f, -0.5f, 0f), MouthPoint.Local(upper, 1));
        }

        [Fact]
        public void ACapsuleShorterThanItsRadiusIsASphereAndItsTipIsItsRadius()
        {
            var dims = new Float3(0.3f, 0.1f, 0.3f);
            Phenotype p = Developer.Develop(RootAndChild(ShapeIds.Capsule, dims, new Float3(0f, -1f, 0f)));

            Fixtures.AssertClose(new Float3(0f, 0.3f, 0f), MouthPoint.Local(p, 1));
        }

        // ---------------------------------------------------------------- the root and the single part

        [Fact]
        public void TheRootTakesTheFaceOppositeItsFirstChild()
        {
            // The first child hangs from the +X face, so the root's mouth is the -X face.
            Phenotype p = Developer.Develop(BoxChild(new Float3(0.25f, 0.25f, 0.25f), new Float3(-1f, 0f, 0f)));

            Fixtures.AssertClose(new Float3(Root, 0f, 0f), p.Parts[1].ParentAnchorLocal);
            Fixtures.AssertClose(new Float3(-Root, 0f, 0f), MouthPoint.Local(p, 0));
        }

        [Fact]
        public void TheRootReadsItsFirstChildInDevelopmentOrderAndNoOther()
        {
            var g = new Genome();
            MorphNode root = Fixtures.Box(Root);
            MorphNode limb = Fixtures.Box(0.25f, JointType.Hinge);

            // The +X edge comes first, the +Y edge second.
            root.Edges.Add(Fixtures.FaceToFace(1));
            root.Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(0f, 1f, 0f),
                ChildAnchor = new Float3(0f, -1f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            g.Nodes.Add(root);
            g.Nodes.Add(limb);
            g.RootIndex = 0;

            Phenotype p = Developer.Develop(g);

            Assert.Equal(3, p.PartCount);
            Fixtures.AssertClose(new Float3(-Root, 0f, 0f), MouthPoint.Local(p, 0));
        }

        [Fact]
        public void ASinglePartBodyTakesItsLongestAxisOnThePositiveSide()
        {
            Genome g = Fixtures.SingleBox();
            g.Nodes[0].Dimensions = new Float3(0.2f, 0.5f, 0.3f);

            Phenotype p = Developer.Develop(g);

            Fixtures.AssertClose(new Float3(0f, 0.5f, 0f), MouthPoint.Local(p, 0));
        }

        [Fact]
        public void ASinglePartCubeBreaksTheTieTowardsX()
        {
            Phenotype p = Developer.Develop(Fixtures.SingleBox());

            Fixtures.AssertClose(new Float3(0.5f, 0f, 0f), MouthPoint.Local(p, 0));
        }

        [Fact]
        public void APartAnchoredAtItsCentreFallsBackToTheSinglePartRule()
        {
            Phenotype p = Developer.Develop(BoxChild(new Float3(0.2f, 0.4f, 0.3f), Float3.Zero));

            Fixtures.AssertClose(Float3.Zero, p.Parts[1].ChildAnchorLocal);
            Fixtures.AssertClose(new Float3(0f, 0.4f, 0f), MouthPoint.Local(p, 1));
        }

        // ---------------------------------------------------------------- a mirrored part

        [Fact]
        public void AMirroredPartHasTheSamePointInItsOwnFrameAsItsTwin()
        {
            // The anchors are stored in the unreflected frame and the reflection is folded into the
            // rotation, so the two copies' own-frame points are the same and posing them with their
            // own rotations does the mirroring.
            Phenotype p = Developer.Develop(BoxChild(
                new Float3(0.25f, 0.25f, 0.25f), new Float3(-1f, 0f, 0f), new Bool3(true, false, false)));

            PhenotypePart[] limbs = p.Parts.Where(x => x.SourceNode == 1).ToArray();
            Assert.Equal(2, limbs.Length);
            Assert.False(limbs[0].Mirrored);
            Assert.True(limbs[1].Mirrored);

            Float3 twin = MouthPoint.Local(p, limbs[0].Index);
            Float3 mirrored = MouthPoint.Local(p, limbs[1].Index);

            Fixtures.AssertClose(new Float3(0.25f, 0f, 0f), twin);
            Fixtures.AssertClose(twin, mirrored);
        }

        [Fact]
        public void AMirroredRootTakesTheSameFaceAsItsUnmirroredFirstChildImplies()
        {
            // The root's first child is the unmirrored copy, whatever follows it.
            Phenotype p = Developer.Develop(BoxChild(
                new Float3(0.25f, 0.25f, 0.25f), new Float3(-1f, 0f, 0f), new Bool3(true, false, false)));

            Assert.False(p.Parts[1].Mirrored);
            Fixtures.AssertClose(new Float3(-Root, 0f, 0f), MouthPoint.Local(p, 0));
        }

        // ---------------------------------------------------------------- a body at its current size

        [Fact]
        public void ABodyScaledByHalfHasEveryMouthAtHalfTheDistance()
        {
            Genome g = BoxChild(new Float3(0.3f, 0.4f, 0.25f), new Float3(-1f, 0.2f, 0f));
            Phenotype adult = Developer.Develop(g);
            Phenotype grown = adult.Scaled(0.5f);

            Assert.NotSame(adult, grown);

            for (int i = 0; i < adult.PartCount; i++)
            {
                Float3 full = MouthPoint.Local(adult, i);
                Float3 half = MouthPoint.Local(grown, i);

                Fixtures.AssertClose(full * 0.5f, half);
                Fixtures.AssertClose(full.Magnitude * 0.5f, half.Magnitude);
            }
        }

        [Fact]
        public void ASphereAndACapsuleScaleTheSameWay()
        {
            Phenotype sphere = Developer.Develop(RootAndChild(
                ShapeIds.Sphere, new Float3(0.3f, 0.3f, 0.3f), new Float3(-1f, -1f, 0f)));
            Phenotype capsule = Developer.Develop(RootAndChild(
                ShapeIds.Capsule, new Float3(0.2f, 0.5f, 0.2f), new Float3(0f, -1f, 0f)));

            Fixtures.AssertClose(MouthPoint.Local(sphere, 1) * 0.5f, MouthPoint.Local(sphere.Scaled(0.5f), 1));
            Fixtures.AssertClose(MouthPoint.Local(capsule, 1) * 0.5f, MouthPoint.Local(capsule.Scaled(0.5f), 1));
        }

        // ---------------------------------------------------------------- the pose

        [Fact]
        public void PosedTurnsThePointByTheRotationAndMovesItByThePosition()
        {
            Quat quarterTurn = Quat.FromAxisAngle(new Float3(0f, 0f, 1f), (float)(Math.PI / 2.0));

            // +x turned a quarter turn about z is +y, then moved by (1, 2, 3).
            Float3 world = MouthPoint.Posed(new Float3(1f, 0f, 0f), new Float3(1f, 2f, 3f), quarterTurn);

            Fixtures.AssertClose(new Float3(1f, 3f, 3f), world);
        }

        [Fact]
        public void PosedWithTheIdentityRotationIsThePositionPlusThePoint()
        {
            Float3 world = MouthPoint.Posed(new Float3(0.1f, -0.2f, 0.3f), new Float3(1f, 2f, 3f), Quat.Identity);

            Fixtures.AssertClose(new Float3(1.1f, 1.8f, 3.3f), world);
        }

        // ---------------------------------------------------------------- refusals

        [Fact]
        public void APartIndexOutsideTheBodyIsRefused()
        {
            Phenotype p = Developer.Develop(Fixtures.SingleBox());

            Assert.Throws<ArgumentOutOfRangeException>(() => MouthPoint.Local(p, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MouthPoint.Local(p, -1));
        }
    }
}

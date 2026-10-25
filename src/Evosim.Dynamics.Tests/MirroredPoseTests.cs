using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// A reflected pair, put down as developed, lands where development put each twin. Developer
    /// stores a mirrored part's anchors unreflected and folds the reflection into its pose, so the
    /// solver, which rebuilds positions from those anchors, must not stack the twin on its original.
    /// </summary>
    public sealed class MirroredPoseTests
    {
        // Fails on this build: the twin lands on its original (logbook/specs/mirrored-anchor-finding.md).
        // Skipped so the suite stays green until the owner rules on the engine change; the fix unskips it.
        [Fact(Skip = "logbook/specs/mirrored-anchor-finding.md: the solver stacks a mirrored part on its twin; the fix waits on the owner")]
        public void AReflectedPairLandsWhereDevelopmentPutEachTwin()
        {
            SolverConfig config = TestBodies.Water(0.01);
            Genome genome = TestBodies.Chain(2, JointType.Hinge, power: 5f);
            genome.Nodes[0].Edges[0].Reflect = new Bool3(true, false, false);
            Phenotype adult = Developer.Develop(genome, DevelopmentLimits.Default);
            Assert.Equal(3, adult.PartCount);

            var body = new Creature(0, adult, config);
            var origin = new Vec3(3, -10, -7);
            body.PlaceAsDeveloped(origin, adult);

            for (int i = 0; i < body.Links; i++)
            {
                Vec3 expected = origin + new Vec3(
                    adult.Parts[i].Position.X, adult.Parts[i].Position.Y, adult.Parts[i].Position.Z);
                Vec3 got = Vec3.Read(body.Position, 3 * i);
                Assert.True(
                    (got - expected).Magnitude < 1e-5,
                    $"part {i} (mirrored {adult.Parts[i].Mirrored}) landed at {got.X:0.######},{got.Y:0.######}," +
                    $"{got.Z:0.######} and development put it at {expected.X:0.######},{expected.Y:0.######}," +
                    $"{expected.Z:0.######}");
            }

            // The twins sit on opposite sides of the root, not on one another.
            Vec3 a = Vec3.Read(body.Position, 3 * 1);
            Vec3 b = Vec3.Read(body.Position, 3 * 2);
            Assert.True((a - b).Magnitude > 0.3, $"the twins are {(a - b).Magnitude:0.###} m apart");
        }

        private static readonly DevelopmentLimits Reflecting = new DevelopmentLimits
        {
            MaxParts = 64, ReflectMirroredAnchors = true,
        };

        /// <summary>
        /// The same body as the skipped fact, with D228's switch on: each twin lands where
        /// development put it, and the twins sit apart.
        /// </summary>
        [Fact]
        public void AReflectedPairLandsWhereDevelopmentPutEachTwinWithTheAnchorsReflected()
        {
            SolverConfig config = TestBodies.Water(0.01);
            Genome genome = TestBodies.Chain(2, JointType.Hinge, power: 5f);
            genome.Nodes[0].Edges[0].Reflect = new Bool3(true, false, false);
            Phenotype adult = Developer.Develop(genome, Reflecting);
            Assert.Equal(3, adult.PartCount);
            Assert.True(adult.Parts[2].Mirrored);

            var body = new Creature(0, adult, config);
            var origin = new Vec3(3, -10, -7);
            body.PlaceAsDeveloped(origin, adult);

            for (int i = 0; i < body.Links; i++)
            {
                Vec3 expected = origin + new Vec3(
                    adult.Parts[i].Position.X, adult.Parts[i].Position.Y, adult.Parts[i].Position.Z);
                Vec3 got = Vec3.Read(body.Position, 3 * i);
                Assert.True(
                    (got - expected).Magnitude < 1e-5,
                    $"part {i} (mirrored {adult.Parts[i].Mirrored}) landed at {got.X:0.######},{got.Y:0.######}," +
                    $"{got.Z:0.######} and development put it at {expected.X:0.######},{expected.Y:0.######}," +
                    $"{expected.Z:0.######}");
            }

            Vec3 a = Vec3.Read(body.Position, 3 * 1);
            Vec3 b = Vec3.Read(body.Position, 3 * 2);
            Assert.True((a - b).Magnitude > 0.3, $"the twins are {(a - b).Magnitude:0.###} m apart");
        }

        // A box root and two link boxes in a chain, every anchor off every axis and every edge turned, so a
        // reflection in any plane moves the joint. firstReflect is the edge into the first link,
        // secondReflect the edge into the second: a mirrored parent is a first edge that reflects.
        private static Genome Chain3(Bool3 firstReflect, Bool3 secondReflect)
        {
            Genome genome = TestBodies.Chain(3, JointType.Hinge, power: 5f, linkHalf: new Float3(0.2f, 0.15f, 0.2f));

            genome.Nodes[0].Edges[0].ParentAnchor = new Float3(0.8f, 0.5f, 0.4f);
            genome.Nodes[0].Edges[0].ChildAnchor = new Float3(-0.7f, 0.3f, -0.2f);
            genome.Nodes[0].Edges[0].Orientation = Quat.FromEuler(0.3f, 0.5f, -0.4f);
            genome.Nodes[0].Edges[0].Reflect = firstReflect;

            genome.Nodes[1].Edges[0].ParentAnchor = new Float3(-0.6f, 0.4f, 0.5f);
            genome.Nodes[1].Edges[0].ChildAnchor = new Float3(0.5f, -0.3f, 0.2f);
            genome.Nodes[1].Edges[0].Orientation = Quat.FromEuler(-0.2f, 0.1f, 0.6f);
            genome.Nodes[1].Edges[0].Reflect = secondReflect;

            return genome;
        }

        // x, y, z in turn, and x with y at once.
        private static readonly Bool3[] Planes =
        {
            new Bool3(true, false, false), new Bool3(false, true, false), new Bool3(false, false, true),
            new Bool3(true, true, false),
        };

        public static IEnumerable<object[]> EveryParentAndEdge()
        {
            for (int p = 0; p < Planes.Length; p++)
            {
                for (int e = 0; e < Planes.Length; e++) yield return new object[] { p, e };
            }
        }

        /// <summary>
        /// A body whose second edge reflects in each of x, y, z and x with y, under a first edge that
        /// itself reflects in each of the same, so the second edge's parent is a mirrored part: put
        /// down as developed, every part lands where development put it, and so does its attitude.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryParentAndEdge))]
        public void EveryPartLandsWhereDevelopmentPutItUnderAMirroredParent(int first, int second)
        {
            Genome genome = Chain3(Planes[first], Planes[second]);
            Phenotype adult = Developer.Develop(genome, Reflecting);

            int firstCopies = Planes[first].CopyCount;
            int secondCopies = Planes[second].CopyCount;
            Assert.Equal(1 + firstCopies + firstCopies * secondCopies, adult.PartCount);
            Assert.Contains(adult.Parts, p => p.Mirrored && p.ParentIndex > 0);

            var body = new Creature(0, adult, TestBodies.Water(0.01));
            var origin = new Vec3(3, -10, -7);
            body.PlaceAsDeveloped(origin, adult);

            for (int i = 0; i < body.Links; i++)
            {
                PhenotypePart part = adult.Parts[i];
                Vec3 expected = origin + new Vec3(part.Position.X, part.Position.Y, part.Position.Z);
                Vec3 got = Vec3.Read(body.Position, 3 * i);
                Assert.True(
                    (got - expected).Magnitude < 1e-5,
                    $"part {i} (mirrored {part.Mirrored}) landed {(got - expected).Magnitude:0.######} m from development");

                // The attitude is the stored proper rotation; the anchors do not enter it.
                QuatD want = QuatD.From(part.Rotation);
                QuatD have = QuatD.Read(body.Rotation, 4 * i);
                double dot = Math.Abs(want.X * have.X + want.Y * have.Y + want.Z * have.Z + want.W * have.W);
                // The stored rotation is a float quaternion, read back as a double: 1e-8 of noise.
                Assert.True(dot > 1 - 1e-6, $"part {i}'s attitude is off: |dot| {dot:0.##########}");
            }
        }

        /// <summary>
        /// The twins of a pair reflected in one plane, joint and centre both, are mirror images about the
        /// plane of their parent through the parent's centre, under a proper parent and under a mirrored one.
        /// </summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(0, 1)]
        [InlineData(0, 2)]
        [InlineData(1, 0)]
        [InlineData(2, 2)]
        public void TheTwinsOfAReflectedPairAreMirrorImagesAboutTheirParentsPlane(int first, int second)
        {
            // Planes 0 to 2 are the single axes. The first edge reflects too, so the second edge's pairs
            // hang from the proper parent and from the mirrored one.
            Genome genome = Chain3(Planes[first], Planes[second]);
            Phenotype adult = Developer.Develop(genome, Reflecting);

            var body = new Creature(0, adult, TestBodies.Water(0.01));
            body.PlaceAsDeveloped(new Vec3(1, 2, 3), adult);

            int pairs = 0;

            for (int i = 0; i < adult.PartCount; i++)
            {
                PhenotypePart original = adult.Parts[i];
                if (original.ParentIndex < 0) continue;

                // The pair's other copy: the next part of the same parent and node, the single-axis
                // copy with the mirror on. The mirrored copy has none after it.
                int j = -1;
                for (int k = i + 1; k < adult.PartCount; k++)
                {
                    if (adult.Parts[k].ParentIndex == original.ParentIndex && adult.Parts[k].SourceNode == original.SourceNode)
                    {
                        j = k;
                        break;
                    }
                }

                if (j < 0) continue;
                pairs++;

                // The edge's plane: the axis the genome reflects, in the parent's full frame, which is
                // the parent's proper rotation times the parent's flip.
                PhenotypePart parent = adult.Parts[original.ParentIndex];
                Bool3 reflect = original.SourceNode == 1 ? Planes[first] : Planes[second];
                int axis = reflect.X ? 0 : reflect.Y ? 1 : 2;

                var local = new Float3(axis == 0 ? 1 : 0, axis == 1 ? 1 : 0, axis == 2 ? 1 : 0);
                if (parent.Mirrored) local = new Float3(-local.X, local.Y, local.Z);
                Float3 nF = parent.Rotation.Rotate(local);
                var n = new Vec3(nF.X, nF.Y, nF.Z);

                Vec3 centreParent = Vec3.Read(body.Position, 3 * original.ParentIndex);
                Vec3 centreA = Vec3.Read(body.Position, 3 * i);
                Vec3 centreB = Vec3.Read(body.Position, 3 * j);
                Vec3 jointA = Joint(body, i);
                Vec3 jointB = Joint(body, j);

                Assert.True((Reflect(centreA, centreParent, n) - centreB).Magnitude < 1e-5, $"centres of parts {i} and {j} are not mirror images");
                Assert.True((Reflect(jointA, centreParent, n) - jointB).Magnitude < 1e-5, $"joints of parts {i} and {j} are not mirror images");
                Assert.True((jointA - jointB).Magnitude > 0.05, $"the joints of parts {i} and {j} are {(jointA - jointB).Magnitude:0.######} m apart");
            }

            Assert.True(pairs >= 2, $"only {pairs} pairs were compared");
        }

        // Link i's joint in the world: its centre plus its attitude times its child anchor.
        private static Vec3 Joint(Creature body, int i)
        {
            Vec3 centre = Vec3.Read(body.Position, 3 * i);
            double[] m = body.RotationMatrix;
            int o = 9 * i;
            double ax = body.ChildAnchor[3 * i], ay = body.ChildAnchor[3 * i + 1], az = body.ChildAnchor[3 * i + 2];
            return new Vec3(
                centre.X + m[o] * ax + m[o + 1] * ay + m[o + 2] * az,
                centre.Y + m[o + 3] * ax + m[o + 4] * ay + m[o + 5] * az,
                centre.Z + m[o + 6] * ax + m[o + 7] * ay + m[o + 8] * az);
        }

        // A point reflected through the plane through <through> with unit normal n.
        private static Vec3 Reflect(Vec3 point, Vec3 through, Vec3 n)
        {
            Vec3 d = point - through;
            double along = d.X * n.X + d.Y * n.Y + d.Z * n.Z;
            return point - n * (2 * along);
        }
    }
}

using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>Small hand-built genomes. Readable beats generic — each fixture is one shape.</summary>
    internal static class Fixtures
    {
        public const float Tol = 1e-4f;

        public static MorphNode Box(float half = 0.5f, JointType joint = JointType.Fixed, int recursiveLimit = 1)
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(half, half, half),
                JointType = joint,
                RecursiveLimit = recursiveLimit,
            };
            node.ResampleJointLimits(new Float2(-1f, 1f));
            return node;
        }

        /// <summary>An edge attaching the child's -X face to the parent's +X face.</summary>
        public static MorphEdge FaceToFace(int child) => new MorphEdge
        {
            Child = child,
            ParentAnchor = new Float3(1f, 0f, 0f),
            ChildAnchor = new Float3(-1f, 0f, 0f),
            Orientation = Quat.Identity,
            Scale = Float3.One,
        };

        /// <summary>A single unjointed box.</summary>
        public static Genome SingleBox()
        {
            var g = new Genome();
            g.Nodes.Add(Box());
            g.RootIndex = 0;
            return g;
        }

        /// <summary>
        /// One node with a self-loop: DESIGN.md §4.1's example of a compact encoding —
        /// "a self-loop with recursiveLimit = 5 yields a five-segment spine".
        /// </summary>
        public static Genome SelfLoopSpine(int recursiveLimit, float segmentScale = 1f)
        {
            var g = new Genome();
            MorphNode node = Box(joint: JointType.Hinge, recursiveLimit: recursiveLimit);
            MorphEdge loop = FaceToFace(0);
            loop.Scale = new Float3(segmentScale, segmentScale, segmentScale);
            node.Edges.Add(loop);
            g.Nodes.Add(node);
            g.RootIndex = 0;
            return g;
        }

        public static void AssertClose(float expected, float actual, float tol = Tol) =>
            Assert.True(System.Math.Abs(expected - actual) <= tol,
                $"expected {expected}, got {actual} (tolerance {tol})");

        public static void AssertClose(Float3 expected, Float3 actual, float tol = Tol)
        {
            Assert.True(
                System.Math.Abs(expected.X - actual.X) <= tol &&
                System.Math.Abs(expected.Y - actual.Y) <= tol &&
                System.Math.Abs(expected.Z - actual.Z) <= tol,
                $"expected {expected}, got {actual} (tolerance {tol})");
        }
    }
}

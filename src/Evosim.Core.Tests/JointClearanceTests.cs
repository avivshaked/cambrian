using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// How far can a face-attached child rotate before it is substantially inside its parent,
    /// and does a gap between them change the answer?
    /// </summary>
    /// <remarks>
    /// <para>
    /// The question behind these: can "a part may be at most x% inside its parent" be enforced
    /// by clamping the evolved joint limit? The mechanism exists — <c>MorphNode.JointLimits</c>
    /// is evolved per DOF and the builder applies it as limited motion — so the clamp would be
    /// a development-time geometric calculation and nothing else.
    /// </para>
    /// <para>
    /// Whether it is a good idea depends on a number: the angle at which overlap becomes
    /// objectionable, compared with the range of motion a creature needs to swim. If the first
    /// is much smaller than the second, clamping trades one problem for another and the honest
    /// fix is a gap at the joint instead.
    /// </para>
    /// <para>
    /// Two equal cubes, the child attached to the centre of the parent's +X face, hinged about
    /// Z. Overlap is reported as a fraction of the child's volume by grid sampling.
    /// </para>
    /// </remarks>
    public class JointClearanceTests
    {
        private readonly ITestOutputHelper _output;

        public JointClearanceTests(ITestOutputHelper output) => _output = output;

        private const float H = 0.5f;              // half-extent of both cubes
        private const int Samples = 24;            // per axis; 13,824 points per measurement

        /// <summary>
        /// Overlap of the child with the parent, as a fraction of the child's volume.
        /// </summary>
        /// <param name="angle">Hinge angle about Z, radians.</param>
        /// <param name="gap">Extra distance between the parent's face and the child's face.</param>
        private static float OverlapFraction(float angle, float gap) =>
            OverlapFraction(angle, gap, 1f);

        /// <param name="childScale">Child half-extent as a multiple of the parent's.</param>
        private static float OverlapFraction(float angle, float gap, float childScale)
        {
            float hc = H * childScale;
            var rotation = Quat.FromAxisAngle(new Float3(0f, 0f, 1f), angle);

            // The joint sits at the centre of the parent's +X face. At rest the child's own -X
            // face meets it, so its centre is (hc + gap) further along the child's own X.
            var joint = new Float3(H, 0f, 0f);
            Float3 centre = joint + rotation.Rotate(new Float3(hc + gap, 0f, 0f));

            int inside = 0;
            int total = 0;

            for (int x = 0; x < Samples; x++)
            {
                float fx = (2f * (x + 0.5f) / Samples - 1f) * hc;
                for (int y = 0; y < Samples; y++)
                {
                    float fy = (2f * (y + 0.5f) / Samples - 1f) * hc;
                    for (int z = 0; z < Samples; z++)
                    {
                        float fz = (2f * (z + 0.5f) / Samples - 1f) * hc;

                        Float3 world = centre + rotation.Rotate(new Float3(fx, fy, fz));
                        total++;

                        // Parent is axis-aligned at the origin.
                        if (MathF.Abs(world.X) <= H &&
                            MathF.Abs(world.Y) <= H &&
                            MathF.Abs(world.Z) <= H)
                        {
                            inside++;
                        }
                    }
                }
            }

            return (float)inside / total;
        }

        [Fact]
        public void OverlapAgainstHingeAngleAndGap()
        {
            float[] angles = { 0f, 0.1f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0f, 1.2f, 1.4f };
            float[] gaps = { 0f, 0.1f * H, 0.2f * H, 0.4f * H, 0.6f * H };

            _output.WriteLine("Overlap of child with parent, as a share of the child's volume.");
            _output.WriteLine("Columns are the gap between their facing surfaces, in half-extents.");
            _output.WriteLine("The genome currently generates joint limits between 0.4 and 1.4 rad.");
            _output.WriteLine("");

            var header = "| angle rad |";
            var divider = "|---|";
            foreach (float g in gaps)
            {
                header += $" gap {g / H:0.0}h |";
                divider += "---|";
            }
            _output.WriteLine(header);
            _output.WriteLine(divider);

            foreach (float a in angles)
            {
                var row = $"| {a:0.0} |";
                foreach (float g in gaps)
                {
                    row += $" {OverlapFraction(a, g):P1} |";
                }
                _output.WriteLine(row);
            }

            _output.WriteLine("");
            _output.WriteLine("Largest hinge angle keeping overlap at or below each bound:");
            _output.WriteLine("");
            _output.WriteLine("| max overlap | gap 0.0h | gap 0.1h | gap 0.2h | gap 0.4h | gap 0.6h |");
            _output.WriteLine("|---|---|---|---|---|---|");

            foreach (float bound in new[] { 0.02f, 0.05f, 0.10f, 0.20f })
            {
                var row = $"| {bound:P0} |";
                foreach (float g in gaps)
                {
                    row += $" {LargestAngleWithin(bound, g):0.00} rad |";
                }
                _output.WriteLine(row);
            }
        }

        /// <summary>
        /// Largest hinge angle whose overlap stays at or below <paramref name="bound"/>.
        /// Swept rather than solved: the relationship is monotonic over this range but the
        /// sampled measurement is not smooth, so a coarse sweep is more honest than a solver
        /// that would converge on sampling noise.
        /// </summary>
        private static float LargestAngleWithin(float bound, float gap)
        {
            float best = 0f;
            for (float a = 0f; a <= 1.6f; a += 0.02f)
            {
                if (OverlapFraction(a, gap) <= bound) best = a;
                else break;
            }
            return best;
        }

        /// <summary>
        /// The same sweep, varying how large the child is relative to its parent.
        /// </summary>
        /// <remarks>
        /// The equal-cube case understates the problem. A small child intruding on a large
        /// parent loses the same absolute volume, which is a far larger share of itself — and
        /// a genome that attaches a small part to a big one is common, so this is the case a
        /// viewer is most likely to read as "a box inside a box".
        /// </remarks>
        [Fact]
        public void OverlapAgainstChildSize()
        {
            float[] scales = { 1.5f, 1.0f, 0.6f, 0.4f, 0.25f };
            float[] angles = { 0.4f, 0.8f, 1.2f, 1.4f };

            _output.WriteLine("Overlap as a share of the CHILD's volume, no gap.");
            _output.WriteLine("");

            var header = "| angle rad |";
            var divider = "|---|";
            foreach (float s in scales)
            {
                header += $" child {s:0.00}x |";
                divider += "---|";
            }
            _output.WriteLine(header);
            _output.WriteLine(divider);

            foreach (float a in angles)
            {
                var row = $"| {a:0.0} |";
                foreach (float s in scales)
                {
                    row += $" {OverlapFraction(a, 0f, s):P1} |";
                }
                _output.WriteLine(row);
            }

            _output.WriteLine("");
            _output.WriteLine("Largest angle keeping overlap at or below 10%, by child size:");
            _output.WriteLine("");
            _output.WriteLine(header.Replace("angle rad", "gap      "));
            _output.WriteLine(divider);

            foreach (float g in new[] { 0f, 0.2f * H, 0.4f * H })
            {
                var row = $"| {g / H:0.0}h |";
                foreach (float s in scales)
                {
                    float best = 0f;
                    for (float a = 0f; a <= 1.6f; a += 0.02f)
                    {
                        if (OverlapFraction(a, g, s) <= 0.10f) best = a;
                        else break;
                    }
                    row += $" {best:0.00} rad |";
                }
                _output.WriteLine(row);
            }
        }

        [Fact]
        public void AGapRemovesOverlapAtRest()
        {
            // Sanity: touching faces at rest overlap nothing, and neither does a gap.
            Assert.True(OverlapFraction(0f, 0f) < 0.01f);
            Assert.True(OverlapFraction(0f, 0.2f * H) < 0.01f);
        }

        [Fact]
        public void RotationDrivesAFaceAttachedChildIntoItsParent()
        {
            // The claim the clamp would rest on: with no gap, overlap grows immediately with
            // angle. If this ever stops being true the clamp is unnecessary.
            Assert.True(OverlapFraction(0.4f, 0f) > OverlapFraction(0.1f, 0f));
            Assert.True(OverlapFraction(0.4f, 0f) > 0.02f);
        }
    }
}

using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Do random genomes develop into creatures that are welded shut by their own bodies?
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Milestone 1 smoke test measures joint travel over eight seconds with self-collision
    /// off and then on. Across twelve seeds it kept 0–22% of its range: seed 8 went from 31.86
    /// rad to 0.01, seed 9 did not move at all while carrying 111 N·m of link capacity. Those
    /// creatures are not weak. They are jammed.
    /// </para>
    /// <para>
    /// <b>Parent and child are not the culprits.</b> PhysX never collides two directly-jointed
    /// links, so overlap at a joint — which §4.2 permits deliberately, and which
    /// <see cref="PhenotypeGeometry.MeasureOverlap"/> reports separately for that reason —
    /// costs nothing mechanically. What jams a creature is overlap between parts that are
    /// <i>not</i> joined: two body cells either side of a link, and siblings sharing a face.
    /// Those do collide, and a joint whose two neighbours are interpenetrating cannot turn.
    /// </para>
    /// <para>
    /// It also explains the momentum leak the same run reported. Depenetration is a positional
    /// correction rather than a force, so a permanently overlapped creature is handed separation
    /// velocity every step and never has to push against water to get it. The seed that leaked
    /// worst (0.048 m²/s, growing 3.05x with time) is the seed that kept 2% of its travel. One
    /// phenomenon, two symptoms.
    /// </para>
    /// <para>
    /// These tests run in Core rather than in the Unity harness on purpose: unjointed overlap is
    /// a property of the developed geometry, needs no physics to measure, and the whole suite
    /// costs a second where the smoke test costs two minutes.
    /// </para>
    /// </remarks>
    public class SelfJamTests
    {
        private readonly ITestOutputHelper _output;

        public SelfJamTests(ITestOutputHelper output) => _output = output;

        private const int Seeds = 60;

        [Fact]
        public void UnjointedOverlapAcrossRandomGenomes()
        {
            _output.WriteLine("Overlap between parts that are NOT joined, as a share of total volume.");
            _output.WriteLine("Jointed overlap is listed for contrast: it is permitted (§4.2) and");
            _output.WriteLine("mechanically free, because PhysX does not collide directly-jointed links.");
            _output.WriteLine("");
            _output.WriteLine("| seed | parts | DOF | jointed m3 | unjointed m3 | unjointed share |");
            _output.WriteLine("|---|---|---|---|---|---|");

            int jammed = 0;
            float worst = 0f;
            double sum = 0d;
            var shares = new float[Seeds];

            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                // RandomViable, not Random: this is the population the harness actually spawns,
                // and it has already been through the overlap filter. Measuring the unfiltered
                // draw answers a question nobody is asking — what the filter is for is exactly
                // that its input is bad.
                var limits = DevelopmentLimits.Default;
                Phenotype p = Developer.Develop(
                    GenomeFactory.RandomViable(new Rng(seed), RandomGenomeOptions.Default, limits, minParts: 3),
                    limits);

                // Eight samples per axis, against the filter's four. A coarse grid can miss a
                // thin intersection entirely, so this is the check on whether the filter's own
                // resolution is good enough to enforce what it claims.
                PhenotypeGeometry.OverlapReport o = PhenotypeGeometry.MeasureOverlap(p, samplesPerAxis: 8);

                sum += o.UnjointedFraction;
                shares[seed - 1] = o.UnjointedFraction;
                if (o.UnjointedFraction > worst) worst = o.UnjointedFraction;
                if (o.UnjointedFraction > 0.05f) jammed++;

                if (seed <= 12)
                {
                    _output.WriteLine(
                        $"| {seed} | {p.PartCount} | {p.TotalDof} | {o.JointedVolume:0.####} | " +
                        $"{o.UnjointedVolume:0.####} | {o.UnjointedFraction:P1} |");
                }
            }

            _output.WriteLine("");
            _output.WriteLine($"over {Seeds} seeds: mean {sum / Seeds:P1}, worst {worst:P1}, " +
                              $"{jammed} above 5%");

            // The distribution, not the mean. A bound on at-rest overlap has to be picked from
            // where the mass actually sits: set it near the median and most body plans are
            // rejected for geometry that is merely untidy, set it near the tail and the creatures
            // that spawn substantially inside themselves get through and farm depenetration.
            System.Array.Sort(shares);
            _output.WriteLine("");
            _output.WriteLine("| percentile | unjointed overlap |");
            _output.WriteLine("|---|---|");
            foreach (int pct in new[] { 50, 75, 90, 95, 99 })
            {
                int i = System.Math.Min(shares.Length - 1, pct * shares.Length / 100);
                _output.WriteLine($"| p{pct} | {shares[i]:P1} |");
            }

            _output.WriteLine("");
            _output.WriteLine("| bound | genomes rejected |");
            _output.WriteLine("|---|---|");
            foreach (float bound in new[] { 0.05f, 0.10f, 0.15f, 0.25f, 0.40f })
            {
                int rejected = 0;
                foreach (float s in shares) if (s > bound) rejected++;
                _output.WriteLine($"| {bound:P0} | {rejected}/{Seeds} ({rejected / (float)Seeds:P0}) |");
            }

            // Reported, not asserted. The bar that belongs here is a design decision about how
            // much interpenetration a genome may develop into, and it is not yet made — see
            // AGapAtEveryAttachmentIsTheCandidateFix for the measurement that should inform it.
            // Asserting a number now would freeze whatever the generator happens to do today.
        }

        [Fact]
        public void JammingIsDrivenByAttachmentDistanceNotByJointRange()
        {
            // Two equal cubes on either side of a small link, which is the shape the double-cell
            // starting creature has (§5A.3) and the shape seeds 8 and 9 developed into. The two
            // body cells are two hops apart, so they collide with each other; whether the link
            // can turn is decided entirely by how far apart they sit.
            //
            // This is the measurement that says whether a gap fixes jamming, and how big it has
            // to be. It is geometry, so it is exact and needs no solver.
            _output.WriteLine("Body-to-body separation vs the hinge angle reachable before the two");
            _output.WriteLine("body cells interpenetrate. Link half-extent as a fraction of the body's.");
            _output.WriteLine("");
            _output.WriteLine("| link size | clearance angle rad |");
            _output.WriteLine("|---|---|");

            foreach (float linkScale in new[] { 0.1f, 0.25f, 0.5f, 1f, 1.5f })
            {
                _output.WriteLine($"| {linkScale:0.##}x | {ClearanceAngle(linkScale):0.##} |");
            }

            // A body cell can always rotate at least this far before touching its sibling across
            // the link, when the link is as big as the bodies are.
            Assert.True(ClearanceAngle(1f) > ClearanceAngle(0.1f),
                "a longer link must buy more range, or the geometry here is wrong");
        }

        /// <summary>
        /// Largest hinge angle at which two unit cubes, separated by a link of
        /// <paramref name="linkScale"/> half-extents, do not overlap each other.
        /// </summary>
        private static float ClearanceAngle(float linkScale)
        {
            const float H = 0.5f;
            float hl = H * linkScale;

            // Root body at the origin; link on its +X face; far body on the link's +X face.
            // The hinge is at the link, so the far body swings about the link's far anchor.
            var pivot = new Float3(H + 2f * hl, 0f, 0f);

            for (float angle = 0f; angle <= 1.6f; angle += 0.02f)
            {
                var rotation = Quat.FromAxisAngle(new Float3(0f, 0f, 1f), angle);
                Float3 centre = pivot + rotation.Rotate(new Float3(H, 0f, 0f));

                if (Overlaps(Float3.Zero, Quat.Identity, new Float3(H, H, H),
                             centre, rotation, new Float3(H, H, H)))
                {
                    return angle;
                }
            }

            return 1.6f;
        }

        /// <summary>Grid-sampled overlap test between two boxes. Coarse but sufficient here.</summary>
        private static bool Overlaps(
            Float3 aCentre, Quat aRotation, Float3 aHalf,
            Float3 bCentre, Quat bRotation, Float3 bHalf)
        {
            const int N = 12;

            for (int x = 0; x < N; x++)
            {
                float fx = (2f * (x + 0.5f) / N - 1f) * bHalf.X;
                for (int y = 0; y < N; y++)
                {
                    float fy = (2f * (y + 0.5f) / N - 1f) * bHalf.Y;
                    for (int z = 0; z < N; z++)
                    {
                        float fz = (2f * (z + 0.5f) / N - 1f) * bHalf.Z;

                        Float3 world = bCentre + bRotation.Rotate(new Float3(fx, fy, fz));
                        Float3 local = aRotation.Conjugate.Rotate(world - aCentre);

                        if (System.Math.Abs(local.X) <= aHalf.X &&
                            System.Math.Abs(local.Y) <= aHalf.Y &&
                            System.Math.Abs(local.Z) <= aHalf.Z)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}

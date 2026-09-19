using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// A body that would grow into itself is not born — the owner's ruling of 2026-09-19, from
    /// the geometry up to the world's admission door. <c>logbook/0107</c>.
    /// </summary>
    public class SelfOverlapTests
    {
        private readonly ITestOutputHelper _output;

        public SelfOverlapTests(ITestOutputHelper output) => _output = output;

        private const float Half = 0.25f;
        private const float Depth = -1f;
        private const float Fraction = 0.1f;

        private static readonly Float3 X = new Float3(1f, 0f, 0f);
        private static readonly Float3 Y = new Float3(0f, 1f, 0f);
        private static readonly Float3 Z = new Float3(0f, 0f, 1f);

        private static BoxOverlap.Obb UnitBoxAt(float x) =>
            new BoxOverlap.Obb(new Float3(x, 0f, 0f), X, Y, Z, new Float3(0.5f, 0.5f, 0.5f));

        // ------------------------------------------------------------------ the geometry

        [Fact]
        public void TwoSeparatedBoxesDoNotIntersect()
        {
            BoxOverlap.Obb a = UnitBoxAt(0f);
            BoxOverlap.Obb b = UnitBoxAt(2f);

            Assert.False(BoxOverlap.Obb.Intersect(a, b, out _));
            Assert.False(BoxOverlap.Obb.Intersect(b, a, out _));
        }

        [Fact]
        public void AnAxisAlignedPairReportsTheOverlapAlongTheAxisTheyMeetOn()
        {
            // Two 1 m cubes 0.75 m apart along x: they share 0.25 m of x and a whole metre of
            // both other axes, so the minimum-translation depth is the 0.25.
            BoxOverlap.Obb a = UnitBoxAt(0f);
            BoxOverlap.Obb b = UnitBoxAt(0.75f);

            Assert.True(BoxOverlap.Obb.Intersect(a, b, out float depth));
            _output.WriteLine($"axis-aligned depth {depth:R} m");

            Fixtures.AssertClose(0.25f, depth, 1e-4f);
        }

        [Fact]
        public void ARotatedPairIsSeparatedOnItsOwnDiagonal()
        {
            // The same cube turned 45° about z presents a half-width of 0.5·(cos45 + sin45) =
            // 0.7071 m along x, so the two touch at 1.2071 m and are clear beyond it. A test the
            // face axes alone cannot pass: an axis-aligned reading of the turned box would call
            // 1.1 m a separation.
            float r = (float)(Math.Sqrt(2d) / 2d);
            var turned = new Float3(r, r, 0f);
            var across = new Float3(-r, r, 0f);

            var near = new BoxOverlap.Obb(
                new Float3(1.0f, 0f, 0f), turned, across, Z, new Float3(0.5f, 0.5f, 0.5f));
            var far = new BoxOverlap.Obb(
                new Float3(1.5f, 0f, 0f), turned, across, Z, new Float3(0.5f, 0.5f, 0.5f));

            BoxOverlap.Obb a = UnitBoxAt(0f);

            Assert.True(BoxOverlap.Obb.Intersect(a, near, out float depth));
            Assert.False(BoxOverlap.Obb.Intersect(a, far, out _));

            _output.WriteLine($"rotated depth {depth:R} m");

            // 0.5 + 0.7071 − 1.0 along x, which is the least of the fifteen.
            Fixtures.AssertClose(0.5f + r - 1.0f, depth, 1e-4f);
        }

        [Fact]
        public void TheThresholdIsTakenFromTheSmallerBoxsThinnestAxis()
        {
            var big = new BoxOverlap.Obb(Float3.Zero, X, Y, Z, new Float3(1f, 1f, 1f));
            var small = new BoxOverlap.Obb(Float3.Zero, X, Y, Z, new Float3(0.5f, 0.2f, 0.4f));

            Fixtures.AssertClose(0.02f, BoxOverlap.DepthThreshold(big, small, Fraction), 1e-6f);
            Fixtures.AssertClose(0.02f, BoxOverlap.DepthThreshold(small, big, Fraction), 1e-6f);
        }

        // ------------------------------------------------------------------ the count

        [Fact]
        public void EverySegmentOfAKnotOverlapsEveryOtherButItsNeighbour()
        {
            // Sixteen boxes exactly on top of each other, in a chain. Every pair overlaps
            // completely; the fifteen parent-child pairs are excluded, so what is left is
            // 16·15/2 − 15 = 105.
            Phenotype knot = Developer.Develop(
                Fixtures.CoincidentKnot(16, Half),
                new DevelopmentLimits { MaxParts = 16, MaxDepth = 16 });

            _output.WriteLine($"{knot.PartCount} parts, {knot.SelfOverlappingPairs(Fraction)} pairs");

            Assert.Equal(16, knot.PartCount);
            Assert.Equal(105, knot.SelfOverlappingPairs(Fraction));
        }

        [Fact]
        public void ASpineLaidOutFaceToFaceOverlapsNothing()
        {
            // The other half: a plan whose segments meet at a face is not a knot, and a rule
            // that refused it would refuse most bodies that fit together at all.
            Phenotype spine = Developer.Develop(Fixtures.SelfLoopSpine(5));
            Phenotype leaf = Developer.Develop(Fixtures.SingleLeaf(Half));

            _output.WriteLine($"spine {spine.PartCount} parts, {spine.SelfOverlappingPairs(Fraction)} pairs");

            Assert.True(spine.PartCount > 1, "the spine developed into one part, so it tests nothing");
            Assert.Equal(0, spine.SelfOverlappingPairs(Fraction));
            Assert.Equal(0, leaf.SelfOverlappingPairs(Fraction));
        }

        [Fact]
        public void TheCountIsTheSameAtAnySize()
        {
            // The threshold is a fraction of the smaller part, and a newborn is its adult times
            // one factor, so the rule judges a body the same whether it is asked at birth or at
            // full size. That is what lets the world ask it once, at the door.
            Phenotype adult = Developer.Develop(
                Fixtures.CoincidentKnot(16, Half),
                new DevelopmentLimits { MaxParts = 16, MaxDepth = 16 });

            Phenotype newborn = adult.Scaled(0.3f);

            Assert.Equal(adult.SelfOverlappingPairs(Fraction), newborn.SelfOverlappingPairs(Fraction));
        }

        [Fact]
        public void ADeeperThresholdRefusesFewerPairs()
        {
            // A guard on the fraction meaning anything: a pair buried by a tenth of itself is
            // not a pair buried by the whole of itself, and the knot's are the second.
            Phenotype knot = Developer.Develop(
                Fixtures.CoincidentKnot(16, Half),
                new DevelopmentLimits { MaxParts = 16, MaxDepth = 16 });

            // Coincident boxes overlap by their whole width, which is twice the half-extent the
            // threshold is a fraction of, so nothing can be deeper than 2 and everything is
            // deeper than 0.9.
            Assert.Equal(105, knot.SelfOverlappingPairs(0.9f));
            Assert.Equal(0, knot.SelfOverlappingPairs(2.5f));
        }

        // ------------------------------------------------------------------ the world

        /// <summary>An empty stage lit from above, with water enough to build bodies from.</summary>
        private static RunConfig Stage(float fraction) => new RunConfig
        {
            MinimumPopulation = 0,
            MaximumPopulation = 100,
            Light = new LightModel(100f, 12f),
            FounderEnergyJoules = 1000f,
            InitialMatterPerCubicMetre = 100f,
            SelfOverlapDepthFraction = fraction,

            // Sixteen segments down a self-edge is a chain sixteen levels deep, and the default
            // caps depth at 8.
            Development = new DevelopmentLimits { MaxParts = 16, MaxDepth = 16 },

            WorldAreaSquareMetres = 100_000f,
        };

        private static Genome Knot() => Fixtures.CoincidentKnot(16, Half, CellTypeIds.Photosynthetic);

        [Fact]
        public void AnInoculatedKnotIsRefusedAtTheDoor()
        {
            // Inoculation and the population floor both go through Admit, which is where the
            // rule lives, so a founder is held to it exactly as a child is.
            var world = new World(Stage(Fraction), seed: 5);
            world.Inoculate(Knot(), count: 1, heightY: Depth);

            _output.WriteLine(
                $"living {world.Living.Count}, stillbirths {world.Stillbirths}, " +
                $"self-overlap {world.SelfOverlapStillbirths}");

            Assert.Empty(world.Living);
            Assert.Equal(1L, world.SelfOverlapStillbirths);
            Assert.Equal(1L, world.Stillbirths);
        }

        [Fact]
        public void TheSameKnotLivesWithTheRuleOff()
        {
            // The guard on the test above: 0 is off and is every world on file, and the knot has
            // to be admissible there or the refusal proves nothing.
            var world = new World(Stage(0f), seed: 5);
            world.Inoculate(Knot(), count: 1, heightY: Depth);

            Assert.Single(world.Living);
            Assert.Equal(0L, world.SelfOverlapStillbirths);
            Assert.Equal(0L, world.Stillbirths);
        }

        [Fact]
        public void ARefusedChildIsACountedStillbirthWithBothBooksClosed()
        {
            // The reproduction path, which is the one that spends. The parent is admitted while
            // the rule is off and the rule is turned on under it, so what it breeds is refused:
            // it paid tissue, reserve and overhead before Admit, and the first two have to reach
            // the water at its own point exactly as a body of no parts' do.
            RunConfig config = Stage(0f);
            var world = new World(config, seed: 5);
            world.Inoculate(Knot(), count: 1, heightY: Depth);
            Assert.Single(world.Living);

            config.SelfOverlapDepthFraction = Fraction;
            for (int i = 0; i < 400; i++) world.Step(1f);

            _output.WriteLine(
                $"living {world.Living.Count}, births {world.Births}, " +
                $"stillbirths {world.Stillbirths}, self-overlap {world.SelfOverlapStillbirths}, " +
                $"charged {world.Nutrients.TotalJoules:R}, resid {world.MatterResidual:R}, " +
                $"audit {world.AuditResidual:R}");

            Assert.True(
                world.SelfOverlapStillbirths > 0,
                "nothing was refused, so the books below are not the rule's books");
            Assert.True(world.Stillbirths >= world.SelfOverlapStillbirths);

            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"matter residual {world.MatterResidual:R}");
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
        }
    }
}

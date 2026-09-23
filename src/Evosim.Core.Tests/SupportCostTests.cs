using System;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D113, the support cost: each part pays <c>price · LitArea · d²</c> in its upkeep, <c>d</c>
    /// its centre's distance from the root's. <c>logbook/specs/support-cost-spec.md</c> §5,
    /// tests 1 to 3, and the distance's own bookkeeping beside them. Tests 4 and 5 (the regress
    /// and the screen) are farm runs and live with the round.
    /// </summary>
    public class SupportCostTests
    {
        private readonly ITestOutputHelper _output;

        public SupportCostTests(ITestOutputHelper output) => _output = output;

        /// <summary>
        /// A photosynthetic box with a face-to-face self-edge, <paramref name="parts"/> of them in
        /// a straight line along x, so the n-th part's centre stands <c>2·half·n</c> from the root.
        /// </summary>
        private static Genome LeafChain(int parts, float half = 0.25f)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f };

            MorphNode node = Fixtures.Box(half, recursiveLimit: parts);
            node.CellTypeId = CellTypeIds.Photosynthetic;
            node.Edges.Add(Fixtures.FaceToFace(0));
            genome.Nodes.Add(node);
            return genome;
        }

        // ------------------------------------------------------------------ test 1

        [Theory]
        [InlineData(0f)]
        [InlineData(0.1f)]
        [InlineData(1000f)]
        public void AOnePartBodyPaysNothingAtAnyPrice(float price)
        {
            Phenotype body = Developer.Develop(LeafChain(1));
            Assert.Equal(1, body.PartCount);
            Assert.Equal(0f, body.Parts[0].DistanceFromRoot);

            var free = new RunConfig();
            var priced = new RunConfig { SupportWattsPerSquareMetrePerSquareMetre = price };

            Assert.Equal(0f, Metabolism.SupportWatts(body.Parts[0], priced));
            Assert.Equal(0d, Metabolism.SupportWatts(body, priced));

            // The root adds a zero, and a float plus zero is the float: the bill is the unpriced
            // one to the bit.
            Assert.Equal(
                BitConverter.SingleToInt32Bits(Metabolism.StandingWatts(body, free)),
                BitConverter.SingleToInt32Bits(Metabolism.StandingWatts(body, priced)));
        }

        // ------------------------------------------------------------------ test 2

        [Fact]
        public void ASecondPartHalfAMetreOutPaysPriceTimesAreaTimesAQuarterAndSixteenTimesThatGrownTwice()
        {
            const float price = 0.1f;
            Phenotype body = Developer.Develop(LeafChain(2));
            Assert.Equal(2, body.PartCount);

            PhenotypePart second = body.Parts[1];
            _output.WriteLine($"second part at {second.Position}, d = {second.DistanceFromRoot:0.#######} m, lit {second.LitArea} m2");
            Assert.InRange(second.DistanceFromRoot, 0.5f - 1e-6f, 0.5f + 1e-6f);

            var free = new RunConfig();
            var priced = new RunConfig { SupportWattsPerSquareMetrePerSquareMetre = price };

            double expected = price * (double)second.LitArea * 0.25;
            Assert.InRange(Metabolism.SupportWatts(second, priced), expected - 1e-6, expected + 1e-6);
            Assert.InRange(Metabolism.SupportWatts(body, priced), expected - 1e-6, expected + 1e-6);

            // And it reaches the bill: the standing cost is the unpriced one plus exactly this.
            double charged = (double)Metabolism.StandingWatts(body, priced) - Metabolism.StandingWatts(body, free);
            _output.WriteLine($"expected {expected:0.#########} W, charged {charged:0.#########} W");
            Assert.InRange(charged, expected - 1e-6, expected + 1e-6);

            // Grown to twice the length: the distance doubles and the area quadruples, so the term
            // is sixteen times. The distance is carried by Scaled, not re-measured.
            Phenotype grown = body.Scaled(2f);
            Assert.InRange(grown.Parts[1].DistanceFromRoot, 1f - 2e-6f, 1f + 2e-6f);

            double grownExpected = 16d * expected;
            double grownCharged = Metabolism.SupportWatts(grown, priced);
            _output.WriteLine($"grown x2: {grownCharged:0.#########} W against {grownExpected:0.#########} W");
            Assert.InRange(grownCharged, grownExpected * (1 - 1e-5), grownExpected * (1 + 1e-5));

            // A newborn at half the length pays a sixteenth, by the same arithmetic.
            double newborn = Metabolism.SupportWatts(body.Scaled(0.5f), priced);
            Assert.InRange(newborn, expected / 16d * (1 - 1e-5), expected / 16d * (1 + 1e-5));
        }

        [Fact]
        public void ThePriceIsWornWithTheRestOfTheUpkeep()
        {
            const float price = 0.1f;
            Phenotype body = Developer.Develop(LeafChain(3));
            double support = Metabolism.SupportWatts(body, new RunConfig { SupportWattsPerSquareMetrePerSquareMetre = price });

            var oldFree = new RunConfig { SenescenceDoublingSeconds = 1000f };
            var oldPriced = new RunConfig { SenescenceDoublingSeconds = 1000f, SupportWattsPerSquareMetrePerSquareMetre = price };

            double charged =
                (double)Metabolism.StandingWatts(body, oldPriced, ageSeconds: 1000f) -
                Metabolism.StandingWatts(body, oldFree, ageSeconds: 1000f);

            // Twice the term at one doubling time, as every other part of the upkeep is.
            Assert.InRange(charged, 2d * support * (1 - 1e-5), 2d * support * (1 + 1e-5));
        }

        // ------------------------------------------------------------------ the distance

        [Fact]
        public void TheDistanceIsFromTheRootPartAndARootTransformMovesNothing()
        {
            Genome chain = LeafChain(4);
            Phenotype atOrigin = Developer.Develop(chain);
            Phenotype moved = Developer.Develop(
                chain, rootTransform: Mat4.Trs(
                    new Float3(7f, -3f, 11f), Quat.FromAxisAngle(new Float3(0f, 1f, 0f), 0.7f), Float3.One));

            for (int i = 0; i < atOrigin.PartCount; i++)
            {
                Assert.InRange(atOrigin.Parts[i].DistanceFromRoot, 0.5f * i - 1e-5f, 0.5f * i + 1e-5f);
                Assert.InRange(
                    moved.Parts[i].DistanceFromRoot,
                    atOrigin.Parts[i].DistanceFromRoot - 1e-5f, atOrigin.Parts[i].DistanceFromRoot + 1e-5f);
            }
        }

        [Fact]
        public void ACutKeepsEverySurvivorsDistance()
        {
            Phenotype body = Developer.Develop(LeafChain(4));
            Phenotype cut = body.WithoutSubtrees(new[] { false, false, false, true }, out int[] map);

            Assert.Equal(3, cut.PartCount);
            for (int i = 0; i < cut.PartCount; i++)
            {
                Assert.Equal(body.Parts[map[i]].DistanceFromRoot, cut.Parts[i].DistanceFromRoot);
            }
        }

        [Fact]
        public void ANegativePriceIsRefused()
        {
            var config = new RunConfig { SupportWattsPerSquareMetrePerSquareMetre = -0.01f };
            Assert.Throws<ArgumentException>(() => new World(config, seed: 3));
        }

        // ------------------------------------------------------------------ test 3

        // The screen's two per-unit numbers (logbook/specs/r45-read/support-cost-screen.py), from
        // round 45's reference leaf: its surface fixation over its lit area, and its standing cost
        // over its volume.
        private const double IncomePerSquareMetre = 3.5583 / 0.3558;
        private const double StandingPerCubicMetre = 0.479 / 0.1597;

        // Round 44's giant 7597 at its module ceiling, from scratch/logs/giant-7597.txt by way of
        // the screen: seven photosynthetic boxes, half-extents and centres in the body's frame.
        private static readonly (Float3 Half, Float3 At)[] Giant =
        {
            (new Float3(0.24f, 0.248f, 0.045f), new Float3(0f, 0f, 0f)),
            (new Float3(0.314f, 0.436f, 0.034f), new Float3(-0.417f, 0.244f, 0.126f)),
            (new Float3(0.411f, 0.765f, 0.025f), new Float3(-0.61f, 0.047f, 0.959f)),
            (new Float3(0.536f, 1.345f, 0.019f), new Float3(0.017f, 1.121f, 1.87f)),
            (new Float3(0.701f, 2.364f, 0.014f), new Float3(-1.837f, 3.044f, 2.322f)),
            (new Float3(0.916f, 4.155f, 0.011f), new Float3(-4.136f, 1.991f, 6.357f)),
            (new Float3(1.197f, 7.303f, 0.010f), new Float3(-0.326f, 5.948f, 12.67f)),
        };

        /// <summary>
        /// The giant as a phenotype, built part by part because its genome is format 7 and this
        /// build reads format 8 only; the distances are measured by <c>Developer</c>'s own pass.
        /// </summary>
        /// <param name="screenArea">
        /// True gives each part the screen's lit area, its largest face (<c>4·h0·h1</c>), by
        /// setting its surface to four times that; false gives it the box's own surface, whose
        /// quarter is what <see cref="PhenotypePart.LitArea"/> is in every run.
        /// </param>
        private static Phenotype GiantBody(bool screenArea)
        {
            var body = new Phenotype();
            MethodInfo add = typeof(Phenotype).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(add);

            PartShape box = PartShapeRegistry.Standard.Resolve(ShapeIds.Box);

            for (int i = 0; i < Giant.Length; i++)
            {
                Float3 h = Giant[i].Half;
                float[] sorted = { h.X, h.Y, h.Z };
                Array.Sort(sorted);

                var part = new PhenotypePart();
                Set(part, nameof(PhenotypePart.ParentIndex), i - 1);
                Set(part, nameof(PhenotypePart.Depth), i);
                Set(part, nameof(PhenotypePart.HalfExtents), h);
                Set(part, nameof(PhenotypePart.Position), Giant[i].At);
                Set(part, nameof(PhenotypePart.Rotation), Quat.Identity);
                Set(part, nameof(PhenotypePart.CellTypeId), CellTypeIds.Photosynthetic);
                Set(part, nameof(PhenotypePart.ShapeId), ShapeIds.Box);
                Set(part, nameof(PhenotypePart.Volume), box.Volume(h));
                Set(part, nameof(PhenotypePart.SurfaceArea),
                    screenArea ? 4f * (4f * sorted[2] * sorted[1]) : box.SurfaceArea(h));

                add.Invoke(body, new object[] { part });
            }

            MethodInfo measure = typeof(Developer).GetMethod(
                "MeasureDistancesFromRoot", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(measure);
            measure.Invoke(null, new object[] { body });

            return body;
        }

        private static void Set(PhenotypePart part, string property, object value) =>
            typeof(PhenotypePart).GetProperty(property).GetSetMethod(true).Invoke(part, new[] { value });

        [Theory]
        [InlineData(true, -179.33)]
        [InlineData(false, -90.13)]
        public void TheGiantLosesMoneyAtTheRuledPrice(bool screenArea, double expectedNet)
        {
            Phenotype giant = GiantBody(screenArea);

            var config = new RunConfig
            {
                SupportWattsPerSquareMetrePerSquareMetre = 0.1f,
                CellTypes = new CellTypeRegistry(
                    new StructuralCell(),
                    new PhotosyntheticCell(upkeepWattsPerCubicMetre: (float)StandingPerCubicMetre)),
            };

            double income = IncomePerSquareMetre * giant.TotalLitArea;
            double standing = Metabolism.StandingWatts(giant, config);
            double support = Metabolism.SupportWatts(giant, config);
            double net = income - standing;

            double meanReach = 0d;
            foreach (PhenotypePart part in giant.Parts) meanReach += (double)part.LitArea * part.DistanceFromRoot;
            meanReach /= giant.TotalLitArea;

            _output.WriteLine(
                $"{(screenArea ? "screen's lit area (largest face)" : "the code's lit area (quarter surface)")}: " +
                $"lit {giant.TotalLitArea:0.###} m2, reach {meanReach:0.##} m, income {income:0.##} W, " +
                $"standing {standing:0.##} W of which support {support:0.##} W, net {net:0.##} W");

            // Within a watt of the arithmetic, through StandingWatts, which is the term's whole
            // route into the bill.
            Assert.InRange(net, expectedNet - 1d, expectedNet + 1d);
        }
    }
}

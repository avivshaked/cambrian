using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D099's cap where it is spent — in the world, on both sides of the light at once.
    /// DESIGN.md §5A.2b; <c>logbook/specs/silhouette-spec.md</c>.
    /// </summary>
    public class SilhouetteTests
    {
        private readonly ITestOutputHelper _output;

        public SilhouetteTests(ITestOutputHelper output) => _output = output;

        private const float Half = 0.25f;
        private const float Depth = -1f;

        /// <summary>An empty stage lit from above: only what a test puts in it lives there.</summary>
        private static RunConfig Stage(bool capOn) => new RunConfig
        {
            MinimumPopulation = 0,
            MaximumPopulation = 100,
            Light = new LightModel(100f, 12f),
            LightSilhouetteCap = capOn,
            FounderEnergyJoules = 1000f,

            // D098's uptake leg off. With it on, fixation is min(light, what the water can
            // give), and a knot drawing on sixteen surfaces strips its own cell — which is a
            // real effect and not the one this test is about. At 0 the ceiling is unbounded and
            // what is left is the light alone.
            UptakeRatePerSquareMetre = 0f,

            // Sixteen segments down a self-edge is a chain sixteen levels deep, and the default
            // caps depth at 8.
            Development = new DevelopmentLimits { MaxParts = 16, MaxDepth = 16 },

            // A wide sky. The layer's sharing term is interceptedFraction x area / demand, which
            // is a shade under 1 and a different shade under 1 for two different demands; at 400
            // m2 that alone moved the knot's capped-to-uncapped ratio by 0.4%, which is a real
            // effect of the shading model and not of the cap.
            WorldAreaSquareMetres = 100_000f,
        };

        /// <summary>
        /// Light earned by one inoculated body in its first step, J — and the fraction of its
        /// adult body it was born at, which is what makes a second step a different comparison.
        /// </summary>
        private float FirstStepLightIncome(Genome genome, bool capOn, out float bodyFraction)
        {
            var world = new World(Stage(capOn), seed: 5);
            world.Inoculate(genome, count: 1, heightY: Depth);
            world.Step(1f);

            Organism creature = world.Living[0];
            bodyFraction = creature.BodyFraction;
            return creature.Lifetime.LightIncome;
        }

        [Fact]
        public void AKnotEarnsOneSixteenthOfWhatItsPartsBillFor()
        {
            // Sixteen photosynthetic boxes exactly on top of each other. Uncapped the knot bills
            // the sun sixteen times for one shadow, which is the exploit round 41c found
            // (logbook/0107); capped it earns on its silhouette, and its silhouette is one box.
            //
            // One step, not several: a capped knot earns less, so it grows less, so by the
            // second step the two runs are bodies of different sizes and the ratio is no longer
            // the cap's alone.
            Genome knot = Fixtures.CoincidentKnot(16, Half, CellTypeIds.Photosynthetic);

            float uncapped = FirstStepLightIncome(knot, capOn: false, out float fractionOff);
            float capped = FirstStepLightIncome(knot, capOn: true, out float fractionOn);

            _output.WriteLine(
                $"knot {uncapped:0.######} J uncapped, {capped:0.######} J capped, " +
                $"ratio {(uncapped > 0f ? capped / uncapped : 0f):0.######}; " +
                $"born at {fractionOff:0.####} and {fractionOn:0.####} of adult");

            Assert.True(uncapped > 0f, "the knot earned nothing — the test measures nothing");
            Assert.Equal(fractionOff, fractionOn);
            Fixtures.AssertClose(uncapped / 16f, capped, uncapped * 1e-5f);
        }

        [Fact]
        public void TheCapIsANoOpOnALeaf()
        {
            // The other half, and the one that matters most: an honest one-part body is lit
            // identically with the cap on, bit for bit. A cap that shaved a leaf would be a tax
            // on being simple.
            Genome leaf = Fixtures.SingleLeaf(Half, CellTypeIds.Photosynthetic);

            float uncapped = FirstStepLightIncome(leaf, capOn: false, out _);
            float capped = FirstStepLightIncome(leaf, capOn: true, out _);

            _output.WriteLine($"leaf {uncapped:R} J uncapped, {capped:R} J capped");

            Assert.True(uncapped > 0f);
            Assert.Equal(uncapped, capped);
        }

        [Fact]
        public void ACappedKnotEarnsNoMoreThanALeafOfItsOwnSilhouette()
        {
            // The spec's reading, stated where a world's births and growth cannot confound it:
            // one metabolic step on each adult body, same water, same light. The knot's
            // silhouette is exactly one of its boxes, so a leaf that size is what it may earn.
            Phenotype knot = Developer.Develop(Fixtures.CoincidentKnot(16, Half, CellTypeIds.Photosynthetic));
            Phenotype leaf = Developer.Develop(Fixtures.SingleLeaf(Half, CellTypeIds.Photosynthetic));

            RunConfig capped = Stage(capOn: true);
            RunConfig plain = Stage(capOn: false);

            float knotCapped = Metabolism.StepAt(knot, capped, 100f, 0f, 0f, 0f, 1f).LightIncome;
            float knotPlain = Metabolism.StepAt(knot, plain, 100f, 0f, 0f, 0f, 1f).LightIncome;
            float leafCapped = Metabolism.StepAt(leaf, capped, 100f, 0f, 0f, 0f, 1f).LightIncome;

            _output.WriteLine(
                $"knot {knotPlain:0.######} J uncapped, {knotCapped:0.######} J capped; " +
                $"leaf {leafCapped:0.######} J");

            Assert.True(knotCapped < knotPlain);
            Assert.True(knotCapped <= leafCapped * 1.000001f,
                $"the capped knot still out-earns its own silhouette: {knotCapped} against {leafCapped}");
            Fixtures.AssertClose(leafCapped, knotCapped, leafCapped * 1e-4f);
        }

        [Fact]
        public void TheCappedKnotShadesExactlyWhatItCollects()
        {
            // The property §5A.2b's shading rests on, and the reason the cap goes on both sides
            // or neither: a body's demand on the light field is the same number it earns on. If
            // they ever part, the world is creating or destroying light and the audit will say
            // so.
            //
            // Read against the same world with the cap off rather than against zero. Twenty
            // steps of float arithmetic leave a residual of their own either way, and the claim
            // is that the cap does not add to it, not that the books are exact in single
            // precision.
            double capped = ResidualAfterTwentySteps(capOn: true);
            double plain = ResidualAfterTwentySteps(capOn: false);

            _output.WriteLine($"audit residual: {capped:R} capped, {plain:R} uncapped");

            Assert.True(
                System.Math.Abs(capped) <= System.Math.Abs(plain) + 1e-6,
                $"the cap opened the books: {capped} capped against {plain} uncapped");
        }

        private static double ResidualAfterTwentySteps(bool capOn)
        {
            var world = new World(Stage(capOn), seed: 5);
            world.Inoculate(Fixtures.CoincidentKnot(16, Half, CellTypeIds.Photosynthetic), 1, Depth);

            for (int i = 0; i < 20; i++) world.Step(1f);
            return world.AuditResidual;
        }

        /// <summary>
        /// Light earned by a fixed world in a fixed number of steps, in joules — the number the
        /// cap-off arithmetic has to leave alone.
        /// </summary>
        /// <remarks>
        /// Measured on this tree with D099's three edits reverted by hand, 2026-09-19. A pinned
        /// literal rather than a comparison, because the claim is about the record and not about
        /// two runs of one build: every config on disk has the cap off, and if this number moves
        /// then every one of them describes a world that no longer replays.
        /// </remarks>
        private const float RecordedIncome = 431.77087f;

        [Fact]
        public void ACapOffWorldEarnsExactlyWhatItAlwaysDid()
        {
            var config = RecordWorld();
            Assert.False(config.LightSilhouetteCap);

            float income = IncomeOfRecordWorld(config);
            _output.WriteLine($"cap off: {income:R} J");

            Assert.Equal(RecordedIncome, income);
        }

        [Fact]
        public void TheSameWorldWithTheCapOnIsADifferentWorld()
        {
            // The other half of the guard above: a pinned number is only worth having if the
            // thing it pins could have moved it. With the cap on this world earns less, so the
            // test above is asserting something.
            RunConfig config = RecordWorld();
            config.LightSilhouetteCap = true;

            float capped = IncomeOfRecordWorld(config);
            _output.WriteLine($"cap on: {capped:R} J against {RecordedIncome:R} J off");

            Assert.True(
                capped < RecordedIncome,
                $"the cap changed nothing in a world of evolved bodies ({capped} against {RecordedIncome})");
        }

        private static RunConfig RecordWorld() => new RunConfig
        {
            MinimumPopulation = 40,
            MaximumPopulation = 500,
            Light = new LightModel(100f, 12f),
        };

        private static float IncomeOfRecordWorld(RunConfig config)
        {
            var world = new World(config, seed: 7);
            for (int i = 0; i < 40; i++) world.Step(1f);

            float income = 0f;
            for (int i = 0; i < world.Living.Count; i++) income += world.Living[i].Lifetime.LightIncome;
            return income;
        }
    }
}

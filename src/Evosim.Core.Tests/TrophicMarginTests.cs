using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// What a body earns above its costs, by trade — DESIGN.md §5A.2, §5A.3, D039.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Break-even is the wrong target and that is what these measure.</b> A creature exactly at
    /// break-even survives forever and never breeds: reproduction in §5A.6 is paid out of surplus,
    /// so a trade that merely covers its costs founds no lineage. The 15,000 s run in
    /// logbook/0024 showed this directly — absorptive creatures arrived, survived a thousand
    /// seconds apiece on a world producing 10–12 J/m³ against a break-even of 8, ate (the first
    /// non-zero food income on record), and the count never once went above one.
    /// </para>
    /// <para>
    /// So the quantity that decides whether a trade is viable is the <i>margin</i>: net watts per
    /// cubic metre of tissue, which divided into the tissue cost of a body gives the seconds it
    /// takes to earn its own replacement. That is the closest thing to a generation time the
    /// economy has, and it is comparable across trades that acquire energy in completely
    /// different ways.
    /// </para>
    /// </remarks>
    public class TrophicMarginTests
    {
        private readonly ITestOutputHelper _output;

        public TrophicMarginTests(ITestOutputHelper output) => _output = output;

        /// <summary>The world the long runs were measured in — 64 W/m² surface, 12 m attenuation.</summary>
        private static RunConfig World() => new RunConfig { Light = new LightModel(64f, 12f) };

        private static Phenotype Body(RunConfig config, string cellType)
        {
            var genome = Fixtures.SingleBox();
            genome.Nodes[0].CellTypeId = cellType;
            return Developer.Develop(genome, config.Development, shapes: config.Shapes);
        }

        /// <summary>Net watts per m³ of tissue, and seconds to earn a body's own tissue cost.</summary>
        private (double watts, double perCubicMetre, double doublingSeconds) Margin(
            RunConfig config, string cellType, float depthY, float nutrientDensity)
        {
            Phenotype body = Body(config, cellType);

            EnergyLedger ledger = Metabolism.StepAt(
                body, config, config.Light.IrradianceAt(depthY),
                nutrientDensity, workJoules: 0f, seconds: 1f);

            double volume = 0d;
            foreach (PhenotypePart part in body.Parts) volume += part.Volume;

            double tissue = Metabolism.TissueJoules(body, config);
            double perCubic = ledger.Net / volume;

            // Seconds to earn its own tissue back. Not the reproduction threshold — §5A.6 adds
            // endowment and overhead on top — but proportional to it and independent of them,
            // which is what makes it comparable between trades.
            double doubling = ledger.Net > 0f ? tissue / ledger.Net : double.PositiveInfinity;

            _output.WriteLine(
                $"{cellType,-15} at {depthY,6:0.#} m, {nutrientDensity,5:0.#} J/m³: " +
                $"income {ledger.Income,7:0.####} − costs {ledger.Expenditure,7:0.####} = " +
                $"{ledger.Net,8:0.####} W  |  {perCubic,8:0.####} W/m³  |  " +
                $"earns its own tissue in {doubling,9:0.#} s");

            return (ledger.Net, perCubic, doubling);
        }

        [Fact]
        public void PhotosynthesisAndAbsorptionAreComparedOnMarginNotOnBreakEven()
        {
            // The measurement D039 turns on, printed rather than asserted at first: what each
            // trade earns above its costs in the conditions the world actually delivers.
            //
            //   Photosynthesis in the lit layer, which is where the whole population lives —
            //   mean depth ran −1 to −2.8 m across every seed.
            //
            //   Absorption at 10 J/m³, which is what fifteen thousand seconds of accumulated
            //   corpses produced (logbook/0024). Not a hoped-for number: a measured one.
            RunConfig config = World();

            var photo = Margin(config, CellTypeIds.Photosynthetic, depthY: -2f, nutrientDensity: 0f);
            var absorb = Margin(config, CellTypeIds.Absorptive, depthY: -2f, nutrientDensity: 10f);

            _output.WriteLine("");
            _output.WriteLine(
                $"photosynthesis earns its body back {absorb.doublingSeconds / photo.doublingSeconds:0.#}x " +
                "faster than absorption at the density this world produces");

            // Both must be solvent, or the comparison is meaningless.
            Assert.True(photo.watts > 0f, "photosynthesis does not pay in the lit layer");
            Assert.True(
                absorb.watts > 0f,
                $"absorption nets {absorb.watts:R} W at 10 J/m³, so the trade is not merely " +
                "slow to breed, it is insolvent — which contradicts logbook/0024's observation " +
                "of absorptive creatures surviving a thousand seconds");
        }

        [Fact]
        public void AbsorptionIsViableAtTheDensityTheWorldActuallyProduces()
        {
            // The acceptance test for D039's calibration. "Viable" is deliberately not
            // "solvent" — break-even was the target that produced a world where exactly one
            // absorptive creature lived at a time for fifteen thousand seconds.
            //
            // The bar: at the density this world reaches, an absorptive body earns its own
            // tissue back within the same order of magnitude as a photosynthetic one does in
            // the light. A trade an order slower cannot hold a niche against one that fast,
            // because §5A.6 pays for offspring out of surplus and surplus is what this measures.
            RunConfig config = World();

            var photo = Margin(config, CellTypeIds.Photosynthetic, depthY: -2f, nutrientDensity: 0f);
            var absorb = Margin(config, CellTypeIds.Absorptive, depthY: -2f, nutrientDensity: 10f);

            double ratio = absorb.doublingSeconds / photo.doublingSeconds;

            Assert.True(
                ratio < 10d,
                $"absorption takes {ratio:0.#}x as long as photosynthesis to earn its own body " +
                $"({absorb.doublingSeconds:0.#} s against {photo.doublingSeconds:0.#} s). A trade " +
                "that slow survives and does not breed, which is exactly what the 15,000 s run " +
                "recorded: one absorptive creature alive at a time, never two.");
        }

        /// <summary>A body of a given half-extent shape, so area-to-volume can be varied.</summary>
        private Phenotype Shaped(RunConfig config, string cellType, Float3 half)
        {
            var genome = Fixtures.SingleBox();
            genome.Nodes[0].CellTypeId = cellType;
            genome.Nodes[0].Dimensions = half;
            return Developer.Develop(genome, config.Development, shapes: config.Shapes);
        }

        [Fact]
        public void TheTwoTradesWantOppositeBodiesAndAConvertInheritsTheWrongOne()
        {
            // The hole in PhotosynthesisAndAbsorptionAreComparedOnMarginNotOnBreakEven: it priced
            // both trades on the same cube, and a cube is neither shape. Photosynthesis scales
            // with lit AREA and absorption with VOLUME, so they want opposite bodies — and an
            // absorptive creature in this world is always a mutant of a photosynthesiser, wearing
            // a body evolved to spread itself out for light. If the penalty for that is large,
            // then a fresh convert is not a creature in a new niche; it is a creature in the wrong
            // shape for the niche it just entered, and raising the clearance rate treats the
            // symptom.
            RunConfig config = World();

            // Same volume, three ways: a flat plate (high area-to-volume, what light wants), a
            // cube, and a long rod. Volume is held equal so the only thing varying is shape.
            var shapes = new (string name, Float3 half)[]
            {
                ("plate", new Float3(1f, 0.0625f, 1f)),
                ("cube",  new Float3(0.397f, 0.397f, 0.397f)),
                ("brick", new Float3(0.7f, 0.128f, 0.7f)),
            };

            foreach (var (name, half) in shapes)
            {
                Phenotype photo = Shaped(config, CellTypeIds.Photosynthetic, half);
                Phenotype absorb = Shaped(config, CellTypeIds.Absorptive, half);

                float irradiance = config.Light.IrradianceAt(-2f);

                EnergyLedger p = Metabolism.StepAt(photo, config, irradiance, 0f, 0f, 1f);
                EnergyLedger a = Metabolism.StepAt(absorb, config, irradiance, 10f, 0f, 1f);

                double volume = 0d, area = 0d;
                foreach (PhenotypePart part in photo.Parts) { volume += part.Volume; area += part.LitArea; }

                _output.WriteLine(
                    $"{name,-16} vol {volume,6:0.###} m³  lit {area,6:0.###} m²  " +
                    $"(A/V {area / volume,5:0.##})   " +
                    $"photo {p.Net,8:0.####} W   absorptive {a.Net,8:0.####} W");
            }

            // Two assertions, and the second is the finding.
            float lit = config.Light.IrradianceAt(-2f);

            var plate = new Float3(1f, 0.0625f, 1f);
            var cube = new Float3(0.397f, 0.397f, 0.397f);

            float platePhoto = Metabolism.StepAt(
                Shaped(config, CellTypeIds.Photosynthetic, plate), config, lit, 0f, 0f, 1f).Net;
            float cubePhoto = Metabolism.StepAt(
                Shaped(config, CellTypeIds.Photosynthetic, cube), config, lit, 0f, 0f, 1f).Net;

            float plateAbsorb = Metabolism.StepAt(
                Shaped(config, CellTypeIds.Absorptive, plate), config, lit, 10f, 0f, 1f).Net;
            float cubeAbsorb = Metabolism.StepAt(
                Shaped(config, CellTypeIds.Absorptive, cube), config, lit, 10f, 0f, 1f).Net;

            // Shape is worth a great deal to light and nothing at all to filtering, which is the
            // asymmetry the whole argument rests on.
            Assert.True(
                platePhoto > cubePhoto * 2f,
                $"spreading out is worth only {platePhoto / cubePhoto:0.##}x to photosynthesis " +
                $"({platePhoto:R} vs {cubePhoto:R} W), so lit area is not doing what §5A.2 says");

            Assert.True(
                Math.Abs(plateAbsorb - cubeAbsorb) < 0.01f * Math.Abs(cubeAbsorb) + 1e-4f,
                $"absorption changed with shape ({plateAbsorb:R} vs {cubeAbsorb:R} W) — it is " +
                "supposed to scale with volume alone");

            // And therefore the two trades have opposite optima, which is the finding. Before
            // D041 raised clearance this read as a pure handicap — converting a spread-out body
            // cost it 9.2× its income, which is why a lone absorptive arrival survived a thousand
            // seconds on a rich larder and never bred (logbook/0025). It now reads as a trade-off,
            // and that is the point: light rewards spreading out and filtering does not care, so
            // the best body for one is a poor body for the other and a creature has to commit.
            _output.WriteLine("");
            _output.WriteLine(
                $"spread out: photo {platePhoto:0.##} W vs filter {plateAbsorb:0.##} W  |  " +
                $"compact: photo {cubePhoto:0.##} W vs filter {cubeAbsorb:0.##} W");

            Assert.True(
                platePhoto > plateAbsorb,
                $"a spread-out body filters better than it photosynthesises " +
                $"({plateAbsorb:0.###} vs {platePhoto:0.###} W), so nothing has a reason to keep a " +
                "light-catching shape and the lit layer changes hands");

            Assert.True(
                cubeAbsorb > cubePhoto,
                $"a compact body still photosynthesises better than it filters " +
                $"({cubePhoto:0.###} vs {cubeAbsorb:0.###} W), so shape does not select a trade and " +
                "there is no morphological reason to become anything");
        }

        [Fact]
        public void TheLitLayerStaysPhotosynthesisAndTheDeepWaterOpensToFiltering()
        {
            // The guard on D041, and it exists because raising clearance is the mirror image of
            // the failure that took two days to find. Absorption is depth-independent;
            // photosynthesis dies below about twenty metres. A clearance rate that merely *ties*
            // at the surface therefore wins everywhere below it, and the world trades a
            // photosynthetic monoculture for a detritivore one — which would look like success
            // for a long time, because food income would finally be large.
            //
            // So both directions are asserted, on the spread-out body a real creature has rather
            // than on a cube:
            //
            //   In the light, photosynthesis must still win, or the surface changes hands.
            //   In the dark, filtering must win, or nothing has been bought at all.
            RunConfig config = World();
            var plate = new Float3(1f, 0.0625f, 1f);

            float density = 10f;   // what 15,000 s of corpses produced (logbook/0025)

            float PhotoAt(float depth) => Metabolism.StepAt(
                Shaped(config, CellTypeIds.Photosynthetic, plate), config,
                config.Light.IrradianceAt(depth), density, 0f, 1f).Net;

            float AbsorbAt(float depth) => Metabolism.StepAt(
                Shaped(config, CellTypeIds.Absorptive, plate), config,
                config.Light.IrradianceAt(depth), density, 0f, 1f).Net;

            float litPhoto = PhotoAt(-2f), litAbsorb = AbsorbAt(-2f);
            float deepPhoto = PhotoAt(-45f), deepAbsorb = AbsorbAt(-45f);

            _output.WriteLine($"  −2 m: photo {litPhoto,7:0.###} W   absorptive {litAbsorb,7:0.###} W");
            _output.WriteLine($" −45 m: photo {deepPhoto,7:0.###} W   absorptive {deepAbsorb,7:0.###} W");

            Assert.True(
                litPhoto > litAbsorb,
                $"filtering earns {litAbsorb:0.###} W in the lit layer against photosynthesis's " +
                $"{litPhoto:0.###} W. Absorption does not care how deep it is, so a trade that " +
                "wins at the surface wins everywhere, and the world becomes a detritivore " +
                "monoculture. Clearance is too high.");

            Assert.True(
                deepAbsorb > 0f && deepAbsorb > deepPhoto,
                $"filtering nets {deepAbsorb:0.###} W at 45 m against photosynthesis's " +
                $"{deepPhoto:0.###} W, so the deep water is still uninhabitable and raising " +
                "clearance bought nothing. Clearance is too low.");
        }

        [Fact]
        public void TheDeepWaterIsStillUnprofitableForPhotosynthesis()
        {
            // The guard on the other side of D039. The point of feeding on detritus is that it
            // works where light does not — if absorption were made generous enough to also be
            // the best trade in the lit layer, the world would swap one monoculture for another
            // and §5A.4's depth gradient would stop meaning anything.
            RunConfig config = World();

            var shallow = Margin(config, CellTypeIds.Photosynthetic, depthY: -2f, nutrientDensity: 0f);
            var deep = Margin(config, CellTypeIds.Photosynthetic, depthY: -45f, nutrientDensity: 0f);

            Assert.True(shallow.watts > 0f, "photosynthesis does not pay even at the surface");
            Assert.True(
                deep.watts < 0f,
                $"photosynthesis nets {deep.watts:R} W at 45 m down, so there is no depth at " +
                "which another trade is needed and no gradient for anything to descend");
        }
    }
}

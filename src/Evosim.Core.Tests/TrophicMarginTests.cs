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

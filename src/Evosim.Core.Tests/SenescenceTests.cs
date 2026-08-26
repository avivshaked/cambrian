using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>Ageing as an energy phenomenon — DESIGN.md §5A.2, D038.</summary>
    public class SenescenceTests
    {
        private readonly ITestOutputHelper _output;

        public SenescenceTests(ITestOutputHelper output) => _output = output;

        private static Phenotype Body(RunConfig config, string cellType = CellTypeIds.Photosynthetic)
        {
            var genome = Fixtures.SingleBox();
            genome.Nodes[0].CellTypeId = cellType;
            return Developer.Develop(genome, config.Development, shapes: config.Shapes);
        }

        private static EnergyLedger At(RunConfig config, float age) =>
            Metabolism.StepAt(
                Body(config), config, irradiance: 200f, nutrientDensity: 40f,
                workJoules: 0f, seconds: 1f, ageSeconds: age);

        [Fact]
        public void AnImmortalWorldIsExactlyTheWorldEveryEarlierRunMeasured()
        {
            // Default zero, and zero has to mean bit-identical rather than nearly. Every result
            // on file — the §5A.2b sweep, D031's calibration, every logbook number — was measured
            // without this, and a default that perturbed the ledger at all would quietly mean
            // none of them describes a world that still exists. That has happened here once
            // already (D031) and is not a thing to do twice deliberately.
            var config = new RunConfig();
            Assert.Equal(0f, config.SenescenceDoublingSeconds);

            EnergyLedger young = At(config, 0f);

            foreach (float age in new[] { 1f, 60f, 3_600f, 1e6f })
            {
                EnergyLedger old = At(config, age);

                Assert.Equal(young.LightIncome, old.LightIncome);
                Assert.Equal(young.FoodIncome, old.FoodIncome);
                Assert.Equal(young.PoolDrawn, old.PoolDrawn);
                Assert.Equal(young.Upkeep, old.Upkeep);
                Assert.Equal(young.Neural, old.Neural);
                Assert.Equal(young.Work, old.Work);
            }
        }

        [Fact]
        public void AtTheDoublingTimeCostsDoubleAndYieldHalves()
        {
            // The knob's name is a promise about a number, so it is worth checking that the
            // number is the one the name says. Both sides move from the one factor: at age = T
            // the wear is 1 + T/T = 2, so upkeep doubles and what the creature keeps halves.
            //
            // Costs alone would have been the cheaper implementation and the wrong biology.
            // Senescence is loss of function first and expense second; a creature that
            // photosynthesised at full efficiency right up to the day it starved would be an
            // odd thing to call old.
            var config = new RunConfig { SenescenceDoublingSeconds = 500f };

            EnergyLedger young = At(config, 0f);
            EnergyLedger old = At(config, 500f);

            _output.WriteLine($"young: {young}");
            _output.WriteLine($"old:   {old}");

            Assert.True(young.Upkeep > 0f, "the fixture has no upkeep to double");
            Assert.True(young.Income > 0f, "the fixture earns nothing to halve");

            Fixtures.AssertClose(young.Upkeep * 2f, old.Upkeep, young.Upkeep * 1e-3f);
            Fixtures.AssertClose(young.Neural * 2f, old.Neural, Math.Max(1e-6f, young.Neural * 1e-3f));
            Fixtures.AssertClose(young.Income * 0.5f, old.Income, young.Income * 1e-3f);
        }

        [Fact]
        public void AgeingDoesNotDiscountTheWorldsGroceries()
        {
            // What falls with age is what a creature *keeps*, not what it takes. An old body
            // still strips the larder at the same rate and feeds itself worse on it, and the
            // shortfall leaves the world through the transfer loss §5A.3 already accounts for.
            //
            // The alternative — scaling the draw — would be a strictly gentler world for
            // everybody else, so a population of the old would deplete less than a population of
            // the young. Ageing would have become a form of restraint.
            var config = new RunConfig { SenescenceDoublingSeconds = 500f };

            // Absorptive, because a photosynthesiser draws nothing from the pool and the whole
            // question here is what happens to the draw.
            var body = Body(config, CellTypeIds.Absorptive);

            EnergyLedger young = Metabolism.StepAt(
                body, config, 0f, nutrientDensity: 400f, workJoules: 0f, seconds: 1f, ageSeconds: 0f);
            EnergyLedger old = Metabolism.StepAt(
                body, config, 0f, nutrientDensity: 400f, workJoules: 0f, seconds: 1f, ageSeconds: 2_000f);

            _output.WriteLine($"drawn {young.PoolDrawn:0.####} vs {old.PoolDrawn:0.####} J");
            _output.WriteLine($"kept  {young.FoodIncome:0.####} vs {old.FoodIncome:0.####} J");

            Assert.Equal(young.PoolDrawn, old.PoolDrawn, 5);
            Assert.True(
                old.FoodIncome < young.FoodIncome,
                $"an old creature kept {old.FoodIncome:R} J of the same {old.PoolDrawn:R} drawn");
            Assert.True(old.Wasted > young.Wasted, "the shortfall went nowhere");
        }

        [Fact]
        public void SenescenceStillClosesTheEnergyAudit()
        {
            // Ageing moves joules between accounts and must not create or destroy any. The
            // income side is the one that could: a term subtracted from what a creature keeps
            // without being added to an outflow is a leak, and §5A.2's audit is the only thing
            // that would ever notice.
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 600,
                SenescenceDoublingSeconds = 400f,
                Light = new LightModel(400f, 12f),
            };

            var world = new World(config, seed: 3);

            try { for (int i = 0; i < 400; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            double residual = world.AuditResidual;
            double scale = Math.Max(1.0, world.EnergyIn);

            _output.WriteLine($"residual {residual:0.######} ({residual / scale:P4})");

            Assert.True(
                Math.Abs(residual) / scale < 1e-4,
                $"energy is not conserved under senescence: {residual:0.###} J unaccounted for");
        }

        [Fact]
        public void SenescenceTurnsThePopulationOverInsteadOfLettingItAccumulate()
        {
            // The behaviour the knob exists for, and the failure it was written against: 98
            // deaths against 1,164 births with the literal t=0 founders still alive at t=3,500
            // (logbook/0023). A world where almost nothing dies is a world where almost nothing
            // is selected — a successful lineage is never replaced, only added to.
            //
            // Not a comparison at a common elapsed time, and the reason is the result: the
            // immortal world cannot be run long enough to compare. It reaches §5A.7's ceiling in
            // 467 s and is stopped there, at which point every creature in both worlds averages
            // about 127 s — well under the doubling time, so senescence has not yet touched
            // anything. Stopping the ageing world at the same instant measures the interval
            // before the mechanism starts, which is the shape of the mistake in logbook/0017.
            //
            // So the honest statement is the one below: without ageing this world explodes, and
            // with it the same world runs to the end of the same 1500 s. That is the effect.
            var lit = new LightModel(400f, 12f);

            RunConfig Config(float doubling) => new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 5000,
                SenescenceDoublingSeconds = doubling,
                Light = lit,
            };

            (World world, bool exploded, double stoppedAt) Run(float doubling)
            {
                var world = new World(Config(doubling), seed: 7);
                for (int i = 0; i < 1500; i++)
                {
                    try { world.Step(1f); }
                    catch (PopulationRunawayException e) { return (world, true, e.ElapsedSeconds); }
                }
                return (world, false, world.ElapsedSeconds);
            }

            var immortal = Run(0f);
            var ageing = Run(300f);

            double Turnover((World world, bool exploded, double stoppedAt) run) =>
                run.world.Deaths / Math.Max(1d, run.world.Births + run.world.FloorSpawns);

            foreach (var run in new[] { immortal, ageing })
            {
                _output.WriteLine(
                    $"stopped at t={run.stoppedAt:0} s ({(run.exploded ? "ran away" : "survived")}): " +
                    $"{run.world.Living.Count} alive, {run.world.Deaths} deaths / " +
                    $"{run.world.Births + run.world.FloorSpawns} born — {Turnover(run):P1}");
            }

            Assert.True(
                immortal.exploded,
                "the immortal world did not run away, so there is nothing here for senescence " +
                "to have changed and this test is measuring nothing");

            Assert.False(
                ageing.exploded,
                $"the ageing world also hit the ceiling, at t={ageing.stoppedAt:0} s");

            // And it is mortality doing it rather than a slower birth rate — the ageing world
            // out-breeds the immortal one over its much longer run and is still smaller.
            Assert.True(
                Turnover(ageing) > Turnover(immortal) * 5d,
                $"{Turnover(ageing):P1} of the ageing world's creatures died against " +
                $"{Turnover(immortal):P1} of the immortal one's");
        }
    }
}

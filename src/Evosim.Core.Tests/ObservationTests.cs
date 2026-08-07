using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The seam between the physics and the economy — DESIGN.md §10 M4, §5A.2.
    /// </summary>
    /// <remarks>
    /// <see cref="World.Observe"/> is the only route by which a number crosses from the simulator
    /// into the ledger, so it is the only place a physics fault can become an accounting fault.
    /// Everything here is about that one direction of travel.
    /// </remarks>
    public class ObservationTests
    {
        private readonly ITestOutputHelper _output;

        public ObservationTests(ITestOutputHelper output) => _output = output;

        private static RunConfig Config() => new RunConfig
        {
            Light = new LightModel(48f, 12f),
            MinimumPopulation = 8,
            FloorSpawnsPerStep = 2,
        };

        [Fact]
        public void WorkIsSpentExactlyOnceAndTheAuditStillCloses()
        {
            // The audit is a hard equality, and mechanical work is the newest term in it. A term
            // that is charged twice destroys energy and one never drained charges a creature
            // forever for a single stroke — and both would show up here rather than as anything
            // recognisable a week into a run.
            var world = new World(Config(), seed: 11);

            world.Step(1f);   // the floor makes the first creatures

            double worst = 0d;
            for (int i = 0; i < 200; i++)
            {
                for (int c = 0; c < world.Living.Count; c++)
                {
                    Organism creature = world.Living[c];
                    world.Observe(creature, creature.HeightY, 0.5f);
                }

                world.Step(1f);

                double residual = world.EnergyIn > 0d
                    ? Math.Abs(world.AuditResidual / world.EnergyIn) : 0d;

                worst = Math.Max(worst, residual);
            }

            _output.WriteLine(
                $"{world.Living.Count} alive, {world.Births} born, {world.Deaths} died, " +
                $"worst audit residual {worst:P4}");

            Assert.True(worst < 1e-4, $"audit drifted to {worst:P6} with the work term live");
        }

        [Fact]
        public void WorkIsDrainedSoAStrokeIsNotBilledForever()
        {
            var world = new World(Config(), seed: 3);
            world.Step(1f);

            Organism creature = world.Living[0];
            world.Observe(creature, creature.HeightY, 12f);

            Assert.Equal(12f, creature.PendingWorkJoules, 3);

            world.Step(1f);

            Assert.Equal(0f, creature.PendingWorkJoules);
        }

        [Fact]
        public void WorkAccumulatesBetweenMetabolicSteps()
        {
            // Physics steps 50 times per metabolic step, so one call per stroke has to add up
            // rather than overwrite. Assigning instead would bill a creature for its last
            // hundredth of a second and let the rest go free.
            var world = new World(Config(), seed: 5);
            world.Step(1f);

            Organism creature = world.Living[0];

            for (int i = 0; i < 50; i++) world.Observe(creature, -3f, 0.1f);

            Assert.Equal(5f, creature.PendingWorkJoules, 3);
            Assert.Equal(-3f, creature.HeightY);
        }

        [Fact]
        public void SwimmingCostsAndTheCostIsTheConfiguredOne()
        {
            // §5A.10's rule applied to the newest knob: WorkCostMultiplier must reach the
            // arithmetic, or "swimming is expensive" is a claim no run can vary.
            var cheap = new RunConfig { Light = new LightModel(48f, 12f), WorkCostMultiplier = 1f };
            var dear = new RunConfig { Light = new LightModel(48f, 12f), WorkCostMultiplier = 4f };

            Assert.NotEqual(cheap.Hash(), dear.Hash());

            float Spend(RunConfig config)
            {
                var world = new World(config, seed: 7);
                world.Step(1f);

                Organism creature = world.Living[0];
                float before = creature.Energy;

                world.Observe(creature, creature.HeightY, 10f);
                world.Step(0.001f);   // short, so upkeep is negligible beside the stroke

                return before - creature.Energy;
            }

            float cheapSpend = Spend(cheap);
            float dearSpend = Spend(dear);

            _output.WriteLine($"10 J of work costs {cheapSpend:0.###} J at 1x, {dearSpend:0.###} J at 4x");

            Assert.True(dearSpend > cheapSpend * 3f, $"{dearSpend} is not ~4x {cheapSpend}");
        }

        [Fact]
        public void NegativeWorkIsRefusedBecauseItWouldBeAnIncome()
        {
            // §11.2. A joint driven by the water does negative work at the actuator, and a
            // creature paid for that would evolve to be pushed around — the free-energy failure
            // arriving through the ledger rather than through the solver.
            var world = new World(Config(), seed: 2);
            world.Step(1f);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => world.Observe(world.Living[0], -1f, -5f));
        }

        [Fact]
        public void ADivergedSolverIsRefusedRatherThanAveragedIn()
        {
            var world = new World(Config(), seed: 2);
            world.Step(1f);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => world.Observe(world.Living[0], float.NaN, 1f));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => world.Observe(world.Living[0], float.NegativeInfinity, 1f));
        }

        [Fact]
        public void DepthDecidesIncomeSoThereIsSomethingToSwimFor()
        {
            // The reason any of this is worth doing. Two identical worlds, one held at the
            // surface and one held deep: if their incomes do not differ, then moving is a pure
            // cost and the only strategy is stillness.
            float IncomeAt(float height)
            {
                var world = new World(Config(), seed: 21);
                world.Step(1f);

                for (int i = 0; i < 20; i++)
                {
                    for (int c = 0; c < world.Living.Count; c++)
                    {
                        world.Observe(world.Living[c], height, 0f);
                    }
                    world.Step(1f);
                }

                float income = 0f;
                for (int c = 0; c < world.Living.Count; c++) income += world.Living[c].Lifetime.LightIncome;
                return income;
            }

            float surface = IncomeAt(-0.5f);
            float deep = IncomeAt(-40f);

            _output.WriteLine($"light income at 0.5 m: {surface:0.#} J, at 40 m: {deep:0.#} J");

            Assert.True(surface > deep * 2f,
                $"depth barely changed income ({surface:0.#} vs {deep:0.#}) — nothing to swim for");
        }
    }
}

using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The module gene: a node whose count is a bounded rule on the body's reserve rather than a
    /// number fixed at development — D106 item 2, <c>logbook/specs/module-gene-spec.md</c> rules
    /// 2 to 5.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both books are the subject, as they were for growth.</b> An addition moves reserve into
    /// tissue, and a drop moves tissue and a share of the reserve into the water; both are
    /// transfers between accounts §5A.2's audit and D074's matter identity already sum. So the way
    /// this mechanism fails is not that a plant grows the wrong number of leaves — it is that a
    /// joule or a unit appears from nowhere while every other column reads normally, which is why
    /// <see cref="AssertBooksClose"/> runs after every step of every test here.
    /// </para>
    /// <para>
    /// <b>How a drought is made.</b> Rule 3's condition is "the reserve stands below
    /// <see cref="RunConfig.ModuleDropReserveSeconds"/> of this body's upkeep", and these tests
    /// make it true by moving the line rather than by impoverishing the body. The two are the same
    /// comparison. Moving the line is the one that happens at a step the test names: starving a
    /// body in a Core-only world means waiting for a reserve to burn down a senescence curve,
    /// which measures the curve.
    /// </para>
    /// </remarks>
    public class ModuleGeneTests
    {
        private readonly ITestOutputHelper _output;

        public ModuleGeneTests(ITestOutputHelper output) => _output = output;

        /// <summary>
        /// A spine of photosynthetic boxes whose one node is indeterminate: the smallest genome on
        /// which a module is a whole part and nothing else about the body changes. Born adult, so
        /// a reading of the module price is not also a reading of growth.
        /// </summary>
        private static Genome Leaf(int maxModules, float half = 0.2f) =>
            Fixtures.IndeterminateLeaf(maxModules, half);

        /// <summary>
        /// A bright, well-fed, childless world. The overhead is what makes it childless: no reserve
        /// a body here can reach clears a gate of a billion joules, so the population stays the one
        /// body the test inoculated and every reading is that body's.
        /// </summary>
        private static RunConfig Stage(float addSeconds, float dropSeconds, float dropAfter)
        {
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(400f, 12f),
                InitialMatterPerCubicMetre = 50f,
                PerOffspringOverheadJoules = 1e9f,
                ModuleAddReserveSeconds = addSeconds,
                ModuleDropReserveSeconds = dropSeconds,
                ModuleDropAfterSeconds = dropAfter,
            };

            foreach (var property in typeof(MutationRates).GetProperties())
            {
                if (property.PropertyType == typeof(float) && property.Name.EndsWith("Chance"))
                {
                    property.SetValue(config.Mutation, 0f);
                }
            }

            return config;
        }

        /// <summary>
        /// Both identities, at the scale each is kept in. A residual is a difference of sums over
        /// every cell and every body, so it is read against the size of what was summed and never
        /// against zero.
        /// </summary>
        private static void AssertBooksClose(World world, string where)
        {
            double energyScale = Math.Max(1d, world.EnergyIn);
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-9 * energyScale,
                $"{where}: audit residual {world.AuditResidual:R} against {energyScale:R} J in");

            double matterScale = Math.Max(1d, world.StandingMatterUnits);
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-9 * matterScale,
                $"{where}: matter residual {world.MatterResidual:R} against {matterScale:R} units");
        }

        /// <summary>Steps the world a second at a time, checking both books at every one.</summary>
        private static void Run(World world, int seconds)
        {
            for (int s = 0; s < seconds; s++)
            {
                world.Step(1f);
                AssertBooksClose(world, "after a step");
            }
        }

        // ---------------------------------------------------------------- rule 2, the addition

        [Fact]
        public void AnIndeterminateLeafAddsModulesToItsCeilingAndNoFurther()
        {
            RunConfig config = Stage(addSeconds: 20f, dropSeconds: 0f, dropAfter: 0f);

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(maxModules: 4), 1, -1f);

            Organism leaf = world.Living[0];
            Assert.Equal(1, leaf.Phenotype.PartCount);

            int adds = 0;
            for (int round = 0; round < 60; round++)
            {
                Run(world, 30);

                long before = world.ModuleAdds;
                world.ApplyModuleRule(30f);
                AssertBooksClose(world, "after the module rule");

                if (world.ModuleAdds > before) adds++;
            }

            _output.WriteLine(
                $"{adds} additions and {world.ModuleAddsRefused} refusals; " +
                $"{leaf.Phenotype.PartCount} parts, {world.ModulesStanding} modules standing; " +
                $"tissue {leaf.TissueJoules:0.####} J, reserve {leaf.Energy:0.####} J");

            // The ceiling is four and the node's own limit is one, so three modules and then
            // nothing more, however rich the body gets. The world's counters and the body agree,
            // which is the only way the instrument can be read at all.
            Assert.Equal(3, adds);
            Assert.Equal(3L, world.ModuleAdds);
            Assert.Equal(4, leaf.Phenotype.PartCount);
            Assert.Equal(3L, world.ModulesStanding);
            Assert.Equal(1f, world.IndeterminateShare);

            // Nothing was refused: a body at its ceiling is not a body that asked and was told no.
            // The refusal counter is for the bounds of rule 5, and reads 0 in a world that has not
            // met one.
            Assert.Equal(0L, world.ModuleAddsRefused);
        }

        [Fact]
        public void AModuleIsPaidForAtTheTissuePriceOutOfTheReserve()
        {
            // Rule 2's price, isolated: no step runs between the two readings, so what the reserve
            // lost is what the body gained, and nothing else in the world moved at all.
            RunConfig config = Stage(addSeconds: 20f, dropSeconds: 0f, dropAfter: 0f);

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(maxModules: 3), 1, -1f);

            Organism leaf = world.Living[0];
            Run(world, 600);

            double energyBefore = leaf.Energy;
            double tissueBefore = leaf.TissueJoules;
            double standingBefore = world.StandingJoules;
            double fieldBefore = world.Nutrients.TotalJoules;

            Assert.Equal(1, world.ApplyModuleRule(10f));

            double spent = energyBefore - leaf.Energy;
            double built = leaf.TissueJoules - tissueBefore;

            _output.WriteLine(
                $"paid {spent:0.######} J for {built:0.######} J of tissue; " +
                $"{leaf.Phenotype.PartCount} parts at fraction {leaf.BodyFraction:0.####}");

            Assert.True(built > 0d, "nothing was built");
            Fixtures.AssertClose(built, spent, Math.Max(1e-9, 1e-9 * built));

            // A transfer and nothing else: the world holds what it held, the water was not touched,
            // and the body's tissue is still measured from its own phenotype rather than tracked
            // alongside it.
            Fixtures.AssertClose(standingBefore, world.StandingJoules, 1e-9 * standingBefore);
            Assert.Equal(fieldBefore, world.Nutrients.TotalJoules);
            Fixtures.AssertClose(
                Metabolism.TissueJoules(leaf.Phenotype, config), leaf.TissueJoules, 1e-9);

            AssertBooksClose(world, "after the price");
        }

        [Fact]
        public void APoorBodyIsNotChargedAndTheRuleSimplyDoesNotFire()
        {
            // The threshold is a threshold. A body whose reserve does not stand above the line is
            // left holding it — no attempt, no refusal, no charge — which is what makes a module
            // something a lineage saves for rather than something it is billed for.
            RunConfig config = Stage(addSeconds: 1e9f, dropSeconds: 0f, dropAfter: 0f);

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(maxModules: 4), 1, -1f);

            Organism leaf = world.Living[0];
            Run(world, 600);

            double energy = leaf.Energy;
            int parts = leaf.Phenotype.PartCount;

            Assert.Equal(0, world.ApplyModuleRule(10f));

            Assert.Equal(energy, leaf.Energy);
            Assert.Equal(parts, leaf.Phenotype.PartCount);
            Assert.Equal(0L, world.ModuleAdds);
            Assert.Equal(0L, world.ModuleAddsRefused);
        }

        // ---------------------------------------------------------- rules 3 and 4, the shedding

        [Fact]
        public void AStarvedBodyDropsOneModuleAtATimeAndTheWaterGetsTheMatter()
        {
            RunConfig config = Stage(addSeconds: 20f, dropSeconds: 0f, dropAfter: 0f);

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(maxModules: 4), 1, -1f);

            Organism leaf = world.Living[0];

            for (int round = 0; round < 60 && leaf.Phenotype.PartCount < 4; round++)
            {
                Run(world, 30);
                world.ApplyModuleRule(30f);
            }

            Assert.Equal(4, leaf.Phenotype.PartCount);

            // The drought: the addition is switched off and the drop's line is put above anything
            // this body holds, which makes rule 3's own comparison true at a step named here.
            config.ModuleAddReserveSeconds = 0f;
            config.ModuleDropReserveSeconds = 1e9f;
            config.ModuleDropAfterSeconds = 5f;

            // Not yet: the clock has to run first, which is the half of rule 3 that stops a plant
            // shedding a leaf on every dip it takes in the step before it feeds.
            Assert.Equal(0, world.ApplyModuleRule(1f));
            Assert.Equal(0L, world.ModuleDrops);

            var shed = new List<double>();

            for (int round = 0; round < 5; round++)
            {
                double standingBefore = world.StandingJoules;
                double fieldBefore = world.Nutrients.TotalJoules;
                double bodyBefore = leaf.Energy + leaf.TissueJoules;
                int partsBefore = leaf.Phenotype.PartCount;

                world.ApplyModuleRule(10f);
                AssertBooksClose(world, "after a drop");

                double moved = world.Nutrients.TotalJoules - fieldBefore;
                double lost = bodyBefore - (leaf.Energy + leaf.TissueJoules);

                _output.WriteLine(
                    $"{partsBefore} -> {leaf.Phenotype.PartCount} parts, " +
                    $"{moved:0.######} J into the water against {lost:0.######} J off the body");

                if (leaf.Phenotype.PartCount == partsBefore) continue;

                shed.Add(moved);

                // One per body per growth step; what leaves the body is what the water gains; and
                // the world as a whole is no poorer for it, which is the whole of why the books
                // stay shut through a shedding.
                Assert.Equal(partsBefore - 1, leaf.Phenotype.PartCount);
                Assert.True(moved > 0d, "the drop deposited nothing");
                Fixtures.AssertClose(lost, moved, Math.Max(1e-9, 1e-9 * moved));
                Fixtures.AssertClose(standingBefore, world.StandingJoules, 1e-9 * standingBefore);
            }

            // Three modules were added and three come off; the fourth call finds a body standing at
            // its genome's own plan and leaves it there, because rule 3 drops modules and never the
            // body the genome describes.
            Assert.Equal(3L, world.ModuleDrops);
            Assert.Equal(3, shed.Count);
            Assert.Equal(1, leaf.Phenotype.PartCount);
            Assert.Equal(0L, world.ModulesStanding);
            Assert.True(leaf.Energy > 0d, "the body was bankrupted by its own shedding");
        }

        [Fact]
        public void ADroppedModuleGrowsBackWhenTheBodyCanAffordItAgain()
        {
            // Rule 4, which is not a mechanism of its own: a shed module is a count standing below
            // what rule 2 would keep, and rule 2 restores it exactly the way it added it.
            RunConfig config = Stage(addSeconds: 20f, dropSeconds: 0f, dropAfter: 0f);

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(maxModules: 3), 1, -1f);

            Organism leaf = world.Living[0];

            for (int round = 0; round < 60 && leaf.Phenotype.PartCount < 3; round++)
            {
                Run(world, 30);
                world.ApplyModuleRule(30f);
            }

            Assert.Equal(3, leaf.Phenotype.PartCount);

            config.ModuleAddReserveSeconds = 0f;
            config.ModuleDropReserveSeconds = 1e9f;
            world.ApplyModuleRule(10f);

            Assert.Equal(2, leaf.Phenotype.PartCount);
            Assert.Equal(1L, world.ModuleDrops);

            config.ModuleAddReserveSeconds = 20f;
            config.ModuleDropReserveSeconds = 0f;

            int regrown = 0;
            for (int round = 0; round < 60 && leaf.Phenotype.PartCount < 3; round++)
            {
                Run(world, 30);
                if (world.ApplyModuleRule(30f) > 0) regrown++;
            }

            _output.WriteLine(
                $"regrown in {regrown} addition(s) to {leaf.Phenotype.PartCount} parts; " +
                $"{world.ModuleAdds} adds and {world.ModuleDrops} drops on the run");

            Assert.Equal(3, leaf.Phenotype.PartCount);
            Assert.Equal(3L, world.ModuleAdds);
            AssertBooksClose(world, "after the regrowth");
        }

        // ------------------------------------------------------------- rule 5, the two bounds

        [Fact]
        public void AnAdditionThatWouldStandInsideItselfIsRefusedAndCounted()
        {
            // D101, asked of the candidate before it is paid for. The knot's segments land exactly
            // on top of each other, so its third module is a body standing inside itself — which
            // is a stillbirth at Admit, and has to be a refusal here, or the module rule would be
            // the one door into the world that D101 does not watch.
            //
            // The third and not the second, because D101 asks the question of non-adjacent parts
            // only: a child sitting on its own parent is a joint, and every jointed body in the
            // world has one. The knot is born with two parts, which is legal, and the part that
            // would make three lands on the root it is not attached to.
            RunConfig config = Stage(addSeconds: 20f, dropSeconds: 0f, dropAfter: 0f);
            config.SelfOverlapDepthFraction = 0.1f;

            Genome knot = Fixtures.CoincidentKnot(
                recursiveLimit: 2, half: 0.2f, cellTypeId: CellTypeIds.Photosynthetic);

            knot.Nodes[0].Growth = ModuleGrowth.Indeterminate;
            knot.Nodes[0].MaxModules = 4;

            var world = new World(config, seed: 3);
            world.Inoculate(knot, 1, -1f);

            Organism body = world.Living[0];
            Assert.Equal(2, body.Phenotype.PartCount);

            Run(world, 600);

            double energy = body.Energy;
            Assert.Equal(0, world.ApplyModuleRule(10f));

            _output.WriteLine(
                $"{world.ModuleAddsRefused} refusal(s) and {world.ModuleAdds} add(s); " +
                $"reserve {energy:0.####} -> {body.Energy:0.####} J");

            Assert.Equal(1L, world.ModuleAddsRefused);
            Assert.Equal(0L, world.ModuleAdds);
            Assert.Equal(2, body.Phenotype.PartCount);

            // Refused and not charged: the body keeps its reserve and will ask again next step.
            Assert.Equal(energy, body.Energy);
            AssertBooksClose(world, "after the refusal");
        }

        [Fact]
        public void ABodyAtThePartLimitAddsNothingAndCountsTheRefusal()
        {
            // The other bound of rule 5. Development would happily build what fits and prune the
            // rest, which would charge a body for a module and hand it a fraction of one; refusing
            // rather than pruning is what keeps the price and the part in step.
            RunConfig config = Stage(addSeconds: 20f, dropSeconds: 0f, dropAfter: 0f);
            config.Development.MaxParts = 3;

            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(maxModules: 8), 1, -1f);

            Organism leaf = world.Living[0];

            for (int round = 0; round < 60; round++)
            {
                Run(world, 30);
                world.ApplyModuleRule(30f);
                AssertBooksClose(world, "after the module rule");
            }

            _output.WriteLine(
                $"{leaf.Phenotype.PartCount} parts at MaxParts {config.Development.MaxParts}; " +
                $"{world.ModuleAdds} adds and {world.ModuleAddsRefused} refusals");

            Assert.Equal(3, leaf.Phenotype.PartCount);
            Assert.Equal(2L, world.ModuleAdds);
            Assert.True(world.ModuleAddsRefused > 0L, "the part limit never refused anything");
        }

        // ------------------------------------------------------- rule 6, the gene and its cost

        [Fact]
        public void TheGeneCostsNoDrawAtAChanceOfZero()
        {
            // The property the whole regress rests on, stated as something a test can see: at a
            // chance of zero the mutator's consumption of the stream does not depend on the gene at
            // all. Two parents identical but for the gene, one determinate and one indeterminate
            // with room above its limit, mutated off the same seed — if the gene took a draw, or
            // took a different number of draws in the two cases, the generators would part and the
            // rest of the two children with them.
            var rates = new MutationRates { ModuleGeneMutationChance = 0f };

            Genome determinate = GenomeFactory.Random(new Rng(7));
            Genome indeterminate = determinate.Clone();

            foreach (MorphNode node in indeterminate.Nodes)
            {
                node.Growth = ModuleGrowth.Indeterminate;
                node.MaxModules = node.RecursiveLimit + 6;
            }

            var a = new Rng(99);
            var b = new Rng(99);

            for (int i = 0; i < 100; i++)
            {
                determinate = Mutator.Mutate(determinate, a, rates);
                indeterminate = Mutator.Mutate(indeterminate, b, rates);
            }

            _output.WriteLine(
                $"{determinate.Nodes.Count} nodes after 100 births, " +
                $"generator at {a.State} against {b.State}");

            Assert.Equal(a.State, b.State);

            // And the gene itself did not move under either parent, which is the other half of
            // "does nothing at zero" — the recorded world's genomes are determinate and stay so.
            foreach (MorphNode node in determinate.Nodes)
            {
                Assert.Equal(ModuleGrowth.Determinate, node.Growth);
            }
        }

        [Fact]
        public void AboveZeroTheGeneAppearsAndItsCeilingMoves()
        {
            // The other side of it: the dial does something when it is set. Not a calibration — the
            // value is the owner's, and unmeasured — but a check that the operator is wired to the
            // dial at all, which is the fault CLAUDE.md's "identical numbers across a configuration
            // change" rule exists to catch.
            var rates = new MutationRates { ModuleGeneMutationChance = 0.05f };
            var rng = new Rng(11);

            Genome g = GenomeFactory.Random(new Rng(7));
            int indeterminateNodes = 0;
            int ceilingAbove = 0;

            for (int i = 0; i < 300; i++)
            {
                g = Mutator.Mutate(g, rng, rates);

                foreach (MorphNode node in g.Nodes)
                {
                    if (node.Growth != ModuleGrowth.Indeterminate) continue;

                    indeterminateNodes++;
                    if (node.MaxModules > node.RecursiveLimit) ceilingAbove++;
                }
            }

            _output.WriteLine(
                $"{indeterminateNodes} indeterminate node-readings over 300 births, " +
                $"{ceilingAbove} of them with room above the node's own limit");

            Assert.True(indeterminateNodes > 0, "the gene never flipped");
            Assert.True(ceilingAbove > 0, "the ceiling never moved off the recursive limit");
        }
    }
}

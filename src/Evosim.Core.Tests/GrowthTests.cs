using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Bodies that grow — fable-propose-growth.md (2026-09-08). Three genome dials, a child born
    /// at the investment over the litter, and a body that builds the rest of itself out of its own
    /// reserve.
    /// </summary>
    /// <remarks>
    /// <b>Both books are the subject.</b> Growth moves energy between two accounts §5A.2's audit
    /// already sums and matter between two D074 already sums, so the way this mechanism fails is
    /// not that a creature grows wrongly but that a joule or a unit of matter appears from nowhere
    /// while every other column reads normally. The closure test at the bottom is the one that
    /// would catch that; the ones above it say what the arithmetic is supposed to be.
    /// </remarks>
    public class GrowthTests
    {
        private readonly ITestOutputHelper _output;

        public GrowthTests(ITestOutputHelper output) => _output = output;

        /// <summary>One unjointed box of one cell type, at a stated size and strategy.</summary>
        private static Genome Box(
            string cellTypeId, float halfExtent, float investment, int brood = 1, float adultScale = 1f)
        {
            var genome = new Genome
            {
                RootIndex = 0,
                AdultScale = adultScale,
                Reproduction = new ReproductionTraits { BroodSize = brood, BirthInvestment = investment },
            };

            genome.Nodes.Add(new MorphNode
            {
                CellTypeId = cellTypeId,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(halfExtent, halfExtent, halfExtent),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });

            return genome;
        }

        /// <summary>
        /// An empty stage with no mutation: nothing is founded, and a child is its parent's twin,
        /// so a child's adult body is a number the test knows before the birth happens.
        /// </summary>
        private static RunConfig Stage(float surfaceIrradiance = 200f)
        {
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(surfaceIrradiance, 12f),
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

        // ---------------------------------------------------------------- birth

        [Fact]
        public void AChildIsBornAtTheInvestmentOverTheLitterAndTheParentPaysExactlyThat()
        {
            // Rule 2, whole. The parent banks half its own body value, spends all of it on a
            // litter of two, and each child's share is its body plus its first reserve split by
            // NewbornReserveFraction. What the parent loses is the investment plus the litter's
            // overhead and nothing else, which is checked against the one witness that cannot be
            // argued with: its reserve before the step, plus what the ledger says the step earned
            // it, less what it holds after.
            RunConfig config = Stage();
            Genome genome = Box(CellTypeIds.Photosynthetic, 0.3f, investment: 0.5f, brood: 2);

            var world = new World(config, seed: 3);
            world.Inoculate(genome, count: 1, heightY: -1f);

            Organism parent = world.Living[0];

            float energyBefore = 0f, tissueBefore = 0f, ageBefore = 0f;
            long births = 0;

            for (int step = 0; step < 4000; step++)
            {
                energyBefore = parent.Energy;
                tissueBefore = parent.TissueJoules;
                ageBefore = parent.Age;

                world.Step(1f);

                if (world.Births > births) { births = world.Births; break; }

                // Only measured once the parent is finished growing: while it is still building
                // itself, its reserve is moving for a second reason and its tissue is moving too.
                Assert.True(step < 3999, "the parent never bred");
            }

            Assert.Equal(2, world.Living.Count - 1);
            Assert.Equal(1f, parent.BodyFraction);

            // What the step earned the parent, priced the way World.Metabolise priced it: the
            // field still holds this step's solve, since it is rebuilt at the top of the next one.
            float net = Metabolism.StepAt(
                parent.Phenotype, config, world.Field.IrradianceAt(parent.HeightY, parent.Patch),
                nutrientDensity: 0f, workJoules: 0f, seconds: 1f, ageSeconds: ageBefore).Net;

            float spent = energyBefore + net - parent.Energy;
            float expected = 0.5f * tissueBefore + 2 * config.PerOffspringOverheadJoules;

            _output.WriteLine(
                $"parent tissue {tissueBefore:0.###} J: spent {spent:0.###} J against " +
                $"{expected:0.###} J = 0.5 x tissue + 2 x {config.PerOffspringOverheadJoules:0.#} J overhead");

            Fixtures.AssertClose(expected, spent, expected * 1e-3f);

            // Each child: a fifth of its adult body, and a reserve of a fifth of its share.
            float share = 0.5f * tissueBefore / 2f;

            for (int i = 1; i < world.Living.Count; i++)
            {
                Organism child = world.Living[i];

                _output.WriteLine(
                    $"child {child.Id}: fraction {child.BodyFraction:0.####}, " +
                    $"tissue {child.TissueJoules:0.###} J, reserve {child.Energy:0.###} J");

                Fixtures.AssertClose(
                    share * (1f - config.NewbornReserveFraction), child.TissueJoules, share * 1e-3f);
                Fixtures.AssertClose(
                    share * config.NewbornReserveFraction, child.Energy, share * 1e-3f);
                Fixtures.AssertClose(0.2f, child.BodyFraction, 1e-3f);
                Fixtures.AssertClose(tissueBefore, child.AdultTissueJoules, tissueBefore * 1e-3f);
            }
        }

        [Fact]
        public void AShareLargerThanTheAdultBodyIsCappedAndTheSurplusStaysWithTheParent()
        {
            // The other half of rule 3. A parent may not buy a creature larger than its genome
            // describes, so the child arrives finished and the parent keeps the difference —
            // which is what stops a runaway investment from being a way to make giants.
            RunConfig config = Stage();
            Genome genome = Box(CellTypeIds.Photosynthetic, 0.3f, investment: 3f);

            var world = new World(config, seed: 3);
            world.Inoculate(genome, count: 1, heightY: -1f);

            Organism parent = world.Living[0];
            Assert.Equal(1f, parent.BodyFraction);   // 3 x 0.8 of an adult body, capped at one

            float energyBefore = 0f, tissueBefore = 0f, ageBefore = 0f;

            for (int step = 0; step < 4000; step++)
            {
                energyBefore = parent.Energy;
                tissueBefore = parent.TissueJoules;
                ageBefore = parent.Age;

                world.Step(1f);
                if (world.Births > 0) break;

                Assert.True(step < 3999, "the parent never bred");
            }

            Organism child = world.Living[1];

            float net = Metabolism.StepAt(
                parent.Phenotype, config, world.Field.IrradianceAt(parent.HeightY, parent.Patch),
                nutrientDensity: 0f, workJoules: 0f, seconds: 1f, ageSeconds: ageBefore).Net;

            float spent = energyBefore + net - parent.Energy;

            // The whole share is capped, not only the body it buys: the ceiling is the share
            // that exactly fills the adult body once the reserve has come out of it, so the
            // reserve a finished child arrives with is that body's fraction and not the parent's.
            float share = 3f * tissueBefore;
            float reserveFraction = config.NewbornReserveFraction;
            float cappedShare = tissueBefore / (1f - reserveFraction);
            float capped = tissueBefore + cappedShare * reserveFraction +
                           config.PerOffspringOverheadJoules;

            _output.WriteLine(
                $"a share of {share:0.#} J bought a {child.TissueJoules:0.#} J body and a " +
                $"{child.Energy:0.#} J reserve for {spent:0.#} J, against an uncapped {share:0.#} J");

            Assert.Equal(1f, child.BodyFraction);
            Fixtures.AssertClose(tissueBefore, child.TissueJoules, tissueBefore * 1e-3f);
            Fixtures.AssertClose(capped, spent, capped * 1e-3f);
            Assert.True(spent < share, "the surplus above the adult body did not stay with the parent");

            // The point of capping the share rather than the body. A child born finished holds
            // the reserve its own body is worth, not the reserve its parent's investment could
            // have bought, so a mutation downward in adult scale is not also a windfall.
            Fixtures.AssertClose(
                cappedShare * reserveFraction, child.Energy, cappedShare * reserveFraction * 1e-3f);
            Assert.True(
                child.Energy < share * reserveFraction,
                "a child born finished still took the uncapped share's reserve");
        }

        [Fact]
        public void ANewbornPartUnderTheMassFloorIsNotConceivedAndNothingMoves()
        {
            // Rule 3's refusal, against its own control. Two worlds identical but for the floor:
            // the one that can carry a 5.4 kg newborn breeds, and the one that cannot does not —
            // and the parent that was refused is left holding more than its own breeding gate,
            // which is what says it was refused for the body and not for want of energy.
            Genome genome = Box(CellTypeIds.Photosynthetic, 0.15f, investment: 0.5f, brood: 2);

            World Run(float floorKilograms)
            {
                // Short, and darker than the rest of this file: the control world breeds freely
                // and there is no reason to grow it into thousands of creatures to learn that it
                // breeds at all.
                RunConfig config = Stage(surfaceIrradiance: 100f);
                config.MinNewbornPartKilograms = 1f;
                config.MaximumPopulation = 2000;

                var world = new World(config, seed: 3);
                world.Inoculate(genome, count: 1, heightY: -1f);

                // Raised after the hand has placed the founder, because since the review of
                // 2026-09-08 the floor refuses an inoculant too and a 10 kg floor would throw
                // rather than seed. The two worlds therefore differ in exactly one thing: what
                // the floor does to the children.
                config.MinNewbornPartKilograms = floorKilograms;

                for (int step = 0; step < 300; step++) world.Step(1f);
                return world;
            }

            World allowed = Run(1f);
            World refused = Run(10f);

            Organism parent = refused.Living[0];

            _output.WriteLine(
                $"floor 1 kg: {allowed.Births} births, {allowed.ConceptionsUnderMassFloor} refused; " +
                $"floor 10 kg: {refused.Births} births, {refused.ConceptionsUnderMassFloor} refused, " +
                $"parent holds {parent.Energy:0.#} J against a gate of " +
                $"{parent.ReproductionThreshold(refused.Config.PerOffspringOverheadJoules):0.#} J");

            Assert.True(allowed.Births > 0, "nothing bred even under a floor the newborn clears");
            Assert.Equal(0L, allowed.ConceptionsUnderMassFloor);

            Assert.Equal(0L, refused.Births);
            Assert.True(refused.ConceptionsUnderMassFloor > 0, "the floor never refused anything");
            Assert.Single(refused.Living);

            // Nothing moved: the parent is solvent past its own gate, so the refusal is the floor
            // and not poverty, and no matter or energy left the world on its account.
            Assert.True(
                parent.Energy > parent.ReproductionThreshold(refused.Config.PerOffspringOverheadJoules),
                "the parent could not afford to breed anyway, so the floor was never the reason");
            // The house tolerance for a world audit, not the 1e-6 the closure test uses: the
            // three accounts are floats and a lone creature's EnergyIn is small, so the residual
            // here is float rounding rather than anything the refusal did.
            Assert.True(
                Math.Abs(refused.AuditResidual) <= 1e-4 * Math.Max(1d, refused.EnergyIn),
                $"a refused conception moved a joule: {refused.AuditResidual:R}");
        }

        [Fact]
        public void AMassFloorRefusalSkipsTheSiblingAndNotTheLitter()
        {
            // The refusal is a fact about the body that was drawn, not about the parent, so the
            // next sibling still gets its draw. The first build broke the brood on it, which
            // quietly made one unlucky mutation cost a parent its whole litter. Found in the
            // review of 2026-09-08.
            //
            // Adult scale is the only dial mutating, at every birth, so the three siblings of one
            // litter differ in size and nothing else. The floor sits where a scale below about
            // 0.885 of the parent's is refused, which is roughly a third of the draws.
            Genome genome = Box(CellTypeIds.Photosynthetic, 0.3f, investment: 3f, brood: 3);

            (long Births, long Refused) FirstBrood(ulong seed)
            {
                RunConfig config = Stage();
                config.MinNewbornPartKilograms = 150f;
                config.Mutation.AdultScaleChance = 1f;
                config.Mutation.ScalarStdDev = 0.35f;

                var world = new World(config, seed);
                world.Inoculate(genome, count: 1, heightY: -1f);

                for (int step = 0; step < 4000; step++)
                {
                    world.Step(1f);

                    // The whole litter is attempted inside one step, so the first step that
                    // records anything records the whole of the first brood.
                    if (world.Births + world.ConceptionsUnderMassFloor > 0)
                    {
                        return (world.Births, world.ConceptionsUnderMassFloor);
                    }
                }

                return (-1, -1);
            }

            int twoChildren = -1, oneChild = -1;

            for (ulong seed = 1; seed <= 200 && (twoChildren < 0 || oneChild < 0); seed++)
            {
                (long births, long refused) = FirstBrood(seed);

                if (births == 2 && refused == 1 && twoChildren < 0) twoChildren = (int)seed;
                if (births == 1 && refused == 2 && oneChild < 0) oneChild = (int)seed;
            }

            _output.WriteLine(
                $"seed {twoChildren}: a brood of three refused one sibling and bore two; " +
                $"seed {oneChild}: refused two and bore one");

            // The review's case, stated as it was stated: three siblings, one under the floor,
            // two children and one count.
            Assert.True(twoChildren > 0, "no seed produced two children and one floor refusal");

            // And the case that could not happen at all if a refusal ended the brood: two
            // refusals in one litter means the loop went past the first of them.
            Assert.True(
                oneChild > 0,
                "no litter recorded two refusals, so nothing proves the loop continued");
        }

        [Fact]
        public void TheDefaultMassFloorAdmitsABodyJustOverItAndRefusesOneJustUnder()
        {
            // The half-kilogram default, exercised at its own edge rather than at some obviously
            // impossible value. A child of this genome is born at 0.4 of its adult volume, so its
            // one part weighs 3,200 times halfExtent cubed kilograms: 0.45 kg at 0.052 m and
            // 0.56 kg at 0.056 m, which straddles the floor by about a tenth of it either way.
            (long Births, long Refused) Run(float halfExtent)
            {
                RunConfig config = Stage(surfaceIrradiance: 1000f);

                // The parent is placed under a floor of nothing, then the default is restored.
                // Otherwise the "just under" world would refuse its own inoculant and never get
                // as far as the conception this is about.
                config.MinNewbornPartKilograms = 0f;

                var world = new World(config, seed: 3);
                world.Inoculate(
                    Box(CellTypeIds.Photosynthetic, halfExtent, investment: 0.5f),
                    count: 1, heightY: -1f);

                config.MinNewbornPartKilograms = new RunConfig().MinNewbornPartKilograms;

                for (int step = 0; step < 20_000; step++)
                {
                    world.Step(1f);
                    if (world.Births > 0) break;
                }

                return (world.Births, world.ConceptionsUnderMassFloor);
            }

            (long overBirths, long overRefused) = Run(0.056f);
            (long underBirths, long underRefused) = Run(0.052f);

            _output.WriteLine(
                $"0.056 m: {overBirths} births, {overRefused} refused; " +
                $"0.052 m: {underBirths} births, {underRefused} refused; " +
                $"floor {new RunConfig().MinNewbornPartKilograms:0.##} kg");

            Assert.True(overBirths > 0, "a 0.56 kg newborn was not born under a 0.5 kg floor");
            Assert.Equal(0L, overRefused);

            Assert.Equal(0L, underBirths);
            Assert.True(underRefused > 0, "a 0.45 kg newborn was born under a 0.5 kg floor");
        }

        [Fact]
        public void AFounderUnderTheMassFloorIsRefusedAndTheFloorDrawsAgain()
        {
            // Rule 7 says a founder is born as a child is, and rule 3 is a fact about what the
            // solver can carry rather than about who the parent was, so the floor holds for the
            // founding lottery too. The first build applied it only to conceptions, which left
            // the one path that has produced every divergence on record exempt from the rule
            // written because of them. Found in the review of 2026-09-08.
            World Run(float floorKilograms)
            {
                RunConfig config = Stage(surfaceIrradiance: 100f);
                config.MinimumPopulation = 20;
                config.MinNewbornPartKilograms = floorKilograms;

                var world = new World(config, seed: 7);
                for (int step = 0; step < 40; step++) world.Step(1f);
                return world;
            }

            World admitted = Run(0.001f);
            World refused = Run(500f);

            // And the number the owner actually has to live with: what the shipped floor does to
            // the founder draw at the shipped genome options. Printed rather than asserted on,
            // because the value is a world rule and this test is not the place to fix one.
            World shipped = Run(new RunConfig().MinNewbornPartKilograms);

            _output.WriteLine(
                $"floor 0.001 kg: {admitted.Living.Count} alive of {admitted.FloorSpawns} draws, " +
                $"{admitted.FoundersUnderMassFloor} refused; " +
                $"floor 500 kg: {refused.Living.Count} alive of {refused.FloorSpawns} draws, " +
                $"{refused.FoundersUnderMassFloor} refused; " +
                $"floor {new RunConfig().MinNewbornPartKilograms:0.##} kg (the default): " +
                $"{shipped.Living.Count} alive of {shipped.FloorSpawns} draws, " +
                $"{shipped.FoundersUnderMassFloor} refused");

            Assert.NotEmpty(admitted.Living);
            Assert.Equal(0L, admitted.FoundersUnderMassFloor);

            // The draw is refused and the floor simply draws again next step: the attempts are
            // still counted, so the trickle stays a trickle rather than becoming a retry loop
            // that packs the world with whatever happens to fit.
            Assert.Empty(refused.Living);
            Assert.True(refused.FoundersUnderMassFloor > 0, "the floor never refused a founder");
            Assert.Equal(refused.FloorSpawns, refused.FoundersUnderMassFloor);

            // Nothing was admitted, so nothing was credited: FounderEnergyJoules is income in
            // the audit and a refusal that leaked it would open the books.
            Assert.Equal(0d, refused.EnergyIn);
            Assert.Equal(0d, refused.AuditResidual);

            // The floor that ships must not close the founding lottery. Rule 3 is meant to keep
            // a body the solver cannot carry out of the world, not to stop the world starting.
            Assert.NotEmpty(shipped.Living);
        }

        [Fact]
        public void AnInoculantUnderTheMassFloorIsRefusedByName()
        {
            // Loud where the floor is quiet, because an assay names one genome on purpose. A
            // silent refusal would leave an experiment reporting an inoculation that never
            // happened, which is the D060 assay measuring a hand that never moved.
            RunConfig config = Stage();
            var world = new World(config, seed: 3);

            ArgumentException e = Assert.Throws<ArgumentException>(
                () => world.Inoculate(
                    Box(CellTypeIds.Photosynthetic, 0.052f, investment: 0.5f),
                    count: 1, heightY: -1f));

            _output.WriteLine(e.Message);

            Assert.Empty(world.Living);
            Assert.Contains("mass floor", e.Message);
            Assert.Contains("0.44", e.Message);   // 0.4499 kg, four places
            Assert.Contains("0.5 kg", e.Message);
        }

        // ---------------------------------------------------------------- growth

        [Fact]
        public void GrowthMovesReserveIntoTissueAndMatterFromTheCellIntoTheBody()
        {
            // Rule 5's transfer, both halves, over one step. The reserve falls by what the body
            // gained, and the water gives up exactly MatterPerTissueJoule of that.
            RunConfig config = Stage();
            config.MatterPerTissueJoule = 0.5f;
            config.InitialMatterPerCubicMetre = 50f;

            var world = new World(config, seed: 3);
            world.Inoculate(Box(CellTypeIds.Photosynthetic, 0.3f, investment: 0.5f), 1, -1f);

            Organism creature = world.Living[0];
            Assert.True(creature.BodyFraction < 1f, "the inoculant was born finished");

            float energyBefore = creature.Energy;
            float tissueBefore = creature.TissueJoules;
            float lockedBefore = creature.LockedMatter;
            float fractionBefore = creature.BodyFraction;
            double fieldBefore = world.Matter.TotalJoules;

            float net = Metabolism.StepAt(
                creature.Phenotype, config, world.Light.IrradianceAt(-1f),
                nutrientDensity: 0f, workJoules: 0f, seconds: 1f, ageSeconds: 0f).Net;

            world.Step(1f);

            float grown = creature.TissueJoules - tissueBefore;
            float paid = creature.LockedMatter - lockedBefore;

            _output.WriteLine(
                $"grew {grown:0.####} J of tissue for {paid:0.####} matter; fraction " +
                $"{fractionBefore:0.####} -> {creature.BodyFraction:0.####}");

            Assert.True(grown > 0f, "nothing was built");
            Assert.True(creature.BodyFraction > fractionBefore);

            // The reserve paid for it: what came in from the ledger, less what the body took.
            Fixtures.AssertClose(energyBefore + net - grown, creature.Energy, Math.Abs(grown) * 1e-4f);

            // And the matter came out of the water rather than out of the air.
            Fixtures.AssertClose(config.MatterPerTissueJoule * grown, paid, Math.Abs(paid) * 1e-3f);
            Assert.Equal(paid, fieldBefore - world.Matter.TotalJoules, 4);
            Assert.Equal(creature.LockedMatter, (float)world.MatterInLivingBodies, 4);

            // The body and the ledger agree to the last float, which is the invariant the whole
            // mechanism rests on: the tissue figure is measured from the body, never accumulated.
            Assert.Equal(Metabolism.TissueJoules(creature.Phenotype, config), creature.TissueJoules);
        }

        [Fact]
        public void ABodyReachesItsAdultSizeAndStops()
        {
            RunConfig config = Stage();

            var world = new World(config, seed: 3);
            world.Inoculate(Box(CellTypeIds.Photosynthetic, 0.3f, investment: 0.5f), 1, -1f);

            Organism creature = world.Living[0];
            float adult = creature.AdultTissueJoules;

            int stepsToAdult = -1;
            for (int step = 0; step < 3000 && stepsToAdult < 0; step++)
            {
                world.Step(1f);
                if (creature.BodyFraction >= 1f) stepsToAdult = step + 1;
            }

            _output.WriteLine(
                $"finished at t={stepsToAdult} s: tissue {creature.TissueJoules:0.####} J of an " +
                $"adult {adult:0.####} J, volume {creature.Phenotype.TotalVolume:0.#####} m3");

            Assert.True(stepsToAdult > 0, "the body never finished growing");
            Assert.Equal(1f, creature.BodyFraction);
            Assert.Equal(adult, creature.TissueJoules);
            Assert.Same(creature.AdultPhenotype, creature.Phenotype);

            // And it stops: the reserve is banked from here on rather than spent on a body that
            // is already built, which is what makes an adult a breeder.
            float tissue = creature.TissueJoules;
            for (int step = 0; step < 50; step++) world.Step(1f);

            Assert.Equal(tissue, creature.TissueJoules);
            Assert.Equal(1f, creature.BodyFraction);
        }

        [Fact]
        public void ABodyShortOfMatterGrowsOnlyWhatItPaidFor()
        {
            // Rule 5's last sentence. The water holds a sliver of matter, so the body may build
            // exactly what that sliver buys and not the joule more its reserve could afford.
            RunConfig config = Stage();
            config.MatterPerTissueJoule = 0.5f;
            config.InitialMatterPerCubicMetre = 0.002f;

            var world = new World(config, seed: 3);
            world.Inoculate(Box(CellTypeIds.Photosynthetic, 0.3f, investment: 0.5f), 1, -1f);

            Organism creature = world.Living[0];

            float tissueBefore = creature.TissueJoules;
            double fieldBefore = world.Matter.TotalJoules;
            double standingBefore = world.StandingMatter;

            world.Step(1f);

            float grown = creature.TissueJoules - tissueBefore;
            double taken = fieldBefore - world.Matter.TotalJoules;

            _output.WriteLine(
                $"the water held {fieldBefore:0.####} within reach; the body grew {grown:0.#####} J " +
                $"for {taken:0.#####} matter, {world.GrowthShortOfMatter} short step(s)");

            Assert.True(world.GrowthShortOfMatter > 0, "matter never bound, so nothing was measured");
            Assert.True(grown > 0f, "a short take grew nothing at all rather than what it paid for");
            Fixtures.AssertClose(
                config.MatterPerTissueJoule * grown, (float)taken, (float)Math.Abs(taken) * 1e-3f);

            // And the identity is untouched: what left the water is in the body.
            Assert.Equal(standingBefore, world.StandingMatter, 6);
            Assert.True(creature.BodyFraction < 1f, "a body this starved of matter finished anyway");
        }

        // ---------------------------------------------------------------- the scaled body

        [Fact]
        public void TheScaledBodyIsTheAdultTimesTheFractionInVolumeAndTheCubeRootInLength()
        {
            // Phenotype.Scaled on its own, away from any world. Lengths scale by the linear
            // factor, volumes and areas are re-measured from the scaled extents, and the tissue
            // value follows the volume — which is what makes the body fraction a readout of the
            // energy ledger rather than a second account beside it.
            var config = new RunConfig();
            Genome genome = Fixtures.SelfLoopSpine(recursiveLimit: 3, segmentScale: 0.8f);
            Phenotype adult = Developer.Develop(genome, config.Development, null, config.Shapes);

            Assert.True(adult.PartCount > 1, "a one-part body would not exercise the anchors");

            const float Fraction = 0.125f;
            float linear = (float)Math.Pow(Fraction, 1d / 3d);
            Phenotype young = adult.Scaled(linear, config.Shapes);

            Assert.Equal(adult.PartCount, young.PartCount);

            for (int i = 0; i < adult.PartCount; i++)
            {
                PhenotypePart a = adult.Parts[i], y = young.Parts[i];

                Fixtures.AssertClose(a.HalfExtents * linear, y.HalfExtents, 1e-6f);
                Fixtures.AssertClose(a.Position * linear, y.Position, 1e-6f);
                Fixtures.AssertClose(a.ParentAnchorLocal * linear, y.ParentAnchorLocal, 1e-6f);
                Fixtures.AssertClose(a.ChildAnchorLocal * linear, y.ChildAnchorLocal, 1e-6f);

                Fixtures.AssertClose(a.Volume * Fraction, y.Volume, a.Volume * 1e-5f);
                Fixtures.AssertClose(
                    a.SurfaceArea * linear * linear, y.SurfaceArea, a.SurfaceArea * 1e-5f);

                Assert.Equal(a.CellTypeId, y.CellTypeId);
                Assert.Equal(a.ShapeId, y.ShapeId);
                Assert.Equal(a.JointType, y.JointType);
                Assert.Equal(a.ParentIndex, y.ParentIndex);
            }

            Fixtures.AssertClose(
                adult.TotalVolume * Fraction, young.TotalVolume, adult.TotalVolume * 1e-5f);
            Fixtures.AssertClose(
                Metabolism.TissueJoules(adult, config) * Fraction,
                Metabolism.TissueJoules(young, config),
                Metabolism.TissueJoules(adult, config) * 1e-5f);

            _output.WriteLine(
                $"{adult.PartCount} parts: {adult.TotalVolume:0.#####} m3 -> {young.TotalVolume:0.#####} m3 " +
                $"at a fraction of {Fraction}, lit area {adult.TotalLitArea:0.####} -> {young.TotalLitArea:0.####} m2");
        }

        [Fact]
        public void ALivingBodysTissueIsAlwaysItsOwnPhenotypesToTheLastFloat()
        {
            // The invariant Organism.TissueJoules states: the ledger's figure is measured from the
            // body on every step it changes, so a body worth one number and a ledger holding
            // another cannot happen — which is how a birth-and-death cycle would create energy.
            RunConfig config = Stage();
            config.MatterPerTissueJoule = 0.2f;
            config.InitialMatterPerCubicMetre = 20f;

            var world = new World(config, seed: 5);
            world.Inoculate(Box(CellTypeIds.Photosynthetic, 0.3f, investment: 0.5f, brood: 2), 4, -3f);

            for (int step = 0; step < 600; step++)
            {
                world.Step(1f);

                foreach (Organism creature in world.Living)
                {
                    Assert.Equal(
                        Metabolism.TissueJoules(creature.Phenotype, config), creature.TissueJoules);

                    float fraction = creature.TissueJoules / creature.AdultTissueJoules;
                    Fixtures.AssertClose(fraction, creature.BodyFraction, 1e-6f);
                    Assert.InRange(creature.BodyFraction, 0f, 1f);
                }
            }

            _output.WriteLine(
                $"{world.Living.Count} alive after {world.Births} births, mean body fraction " +
                $"{world.MeanBodyFraction:0.###}");

            Assert.True(world.Births > 0, "nothing was born, so no small body was ever checked");
        }

        // ---------------------------------------------------------------- mutation and the file

        [Fact]
        public void MutationMovesAllThreeDialsAndBothWays()
        {
            // The reason these are dials and not switches: each moves by a graded step, and a
            // walk that only went one way would be a ratchet rather than a strategy.
            var rates = new MutationRates
            {
                BroodSizeChance = 1f, InvestmentChance = 1f, AdultScaleChance = 1f,
                ScalarChance = 1f, ScalarStdDev = 0.2f, MaxBroodSize = 16,
            };

            int broodUp = 0, broodDown = 0, investUp = 0, investDown = 0, scaleUp = 0, scaleDown = 0;

            for (ulong seed = 1; seed <= 400; seed++)
            {
                var parent = new Genome { RootIndex = 0, AdultScale = 1f };
                parent.Nodes.Add(Fixtures.Box());
                parent.Reproduction = new ReproductionTraits { BroodSize = 8, BirthInvestment = 0.5f };

                Genome child = Mutator.Mutate(parent, new Rng(seed), rates);

                if (child.Reproduction.BroodSize > 8) broodUp++;
                else if (child.Reproduction.BroodSize < 8) broodDown++;

                if (child.Reproduction.BirthInvestment > 0.5f) investUp++;
                else if (child.Reproduction.BirthInvestment < 0.5f) investDown++;

                if (child.AdultScale > 1f) scaleUp++;
                else if (child.AdultScale < 1f) scaleDown++;

                Assert.Empty(child.Validate());
            }

            _output.WriteLine(
                $"brood {broodUp} up / {broodDown} down, investment {investUp} / {investDown}, " +
                $"adult scale {scaleUp} / {scaleDown} over 400 births");

            Assert.True(broodUp > 50 && broodDown > 50, "brood size is not drifting symmetrically");
            Assert.True(investUp > 50 && investDown > 50, "the birth investment is not drifting symmetrically");
            Assert.True(scaleUp > 50 && scaleDown > 50, "the adult scale is not drifting symmetrically");
        }

        [Fact]
        public void TheAdultScaleReachesEveryPartAndEveryAnchor()
        {
            // Rule 1: one scalar on the plan, multiplied into the developer's accumulated scale,
            // so it reaches the edge scales' compounding and a scaled body is the same shape.
            var config = new RunConfig();
            Genome one = Fixtures.SelfLoopSpine(recursiveLimit: 3, segmentScale: 0.8f);
            Genome twice = Fixtures.SelfLoopSpine(recursiveLimit: 3, segmentScale: 0.8f);
            twice.AdultScale = 2f;

            Phenotype small = Developer.Develop(one, config.Development, null, config.Shapes);
            Phenotype large = Developer.Develop(twice, config.Development, null, config.Shapes);

            Assert.Equal(small.PartCount, large.PartCount);

            for (int i = 0; i < small.PartCount; i++)
            {
                Fixtures.AssertClose(small.Parts[i].HalfExtents * 2f, large.Parts[i].HalfExtents, 1e-6f);
                Fixtures.AssertClose(small.Parts[i].Position * 2f, large.Parts[i].Position, 1e-6f);
                Fixtures.AssertClose(
                    small.Parts[i].ParentAnchorLocal * 2f, large.Parts[i].ParentAnchorLocal, 1e-6f);
            }

            Fixtures.AssertClose(
                small.TotalVolume * 8f, large.TotalVolume, small.TotalVolume * 1e-4f);

            _output.WriteLine(
                $"adult scale 1 -> {small.TotalVolume:0.#####} m3, scale 2 -> {large.TotalVolume:0.#####} m3");
        }

        [Fact]
        public void AGenomeRoundTripsItsThreeDialsAndAFormatFourGenomeIsRefused()
        {
            Genome genome = GenomeFactory.Random(new Rng(21));
            genome.AdultScale = 0.625f;
            genome.Reproduction = new ReproductionTraits { BroodSize = 3, BirthInvestment = 0.875f };

            string text = GenomeJson.Write(genome);
            Genome back = GenomeJson.Read(text);

            Assert.Equal(3, back.Reproduction.BroodSize);
            Fixtures.AssertClose(0.875f, back.Reproduction.BirthInvestment, 0f);
            Fixtures.AssertClose(0.625f, back.AdultScale, 0f);
            Assert.Equal(text, GenomeJson.Write(back));

            // The bump is not cosmetic: a format-4 genome's endowment is joules where the field
            // that replaced it is a fraction of a body, so the same number in the same place would
            // mean something else and the file carries no size at all.
            string old = text.Replace("\"format\":5", "\"format\":4");
            FormatException e = Assert.Throws<FormatException>(() => GenomeJson.Read(old));

            _output.WriteLine(e.Message);
            Assert.Contains("format 4", e.Message);
        }

        // ---------------------------------------------------------------- both books

        [Fact]
        public void AGrowingGridWorldClosesItsAuditAndItsMatterIdentity()
        {
            // The closure test, ported from GridFieldTests with growth in it. Everything that
            // moves a joule or a unit of matter runs at once — founders, feeding, growth, births,
            // deaths, corpses, an influx, burial, sinking, mixing and a rolling current — and both
            // identities are hard equalities rather than plausibility checks.
            //
            // The floor closes at 1,000 s of the 1,500, so the last third of the run is bodies
            // that were born and grown rather than founded. Half-second steps because explicit
            // diffusion on a 1 m cell at 0.2 m2/s is stable only to 0.83 s and GridField refuses a
            // longer one rather than clamping.
            const float Area = 144f;
            const int Patches = 4;
            const float Depth = 24f;

            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Patches,
                WorldDepthMetres = Depth,
                FloorClosesAfterSeconds = 1000f,
                MatterPerTissueJoule = 0.05f,
                MatterInfluxPerSecond = 0.05f,
                MatterBurialPerSecond = 0.001f,
                CorpseDecayPerSecond = 0.01f,
                ExcretionPerJoule = 0.0005f,
                NutrientMixingDiffusivity = 0.2f,
                HorizontalMixingDiffusivity = 0.2f,
                MatterMixingDiffusivity = 0.2f,
                Current = new CurrentField
                {
                    Speed = 0.1f, CellMetres = 10f, PeriodSeconds = 600f, Rolls = true,
                    AdvectFields = true, RollBlinkSeconds = 300f, VentDepthMetres = Depth,
                },
            };

            var world = new World(config, seed: 3);

            // What each creature was actually born at, taken from the world's own lineage rows
            // rather than reconstructed. A child's birth fraction comes from its *parent's*
            // investment and its parent's tissue, so a guard computed from the child's own genome
            // is only right for founders, and it was wrong here until the review of 2026-09-08.
            var bornAt = new Dictionary<long, float>();

            for (int step = 0; step < 3_000; step++)
            {
                world.Step(0.5f);

                foreach (LineageEvent e in world.DrainLineageEvents())
                {
                    if (e.Kind == LineageEventKind.Birth) bornAt[e.Id] = e.BirthFraction;
                }
            }

            double identity = world.MatterInitialTotal + world.MatterInfluxedTotal -
                              world.MatterBuriedTotal - world.StandingMatter;

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}, " +
                $"corpses {world.Corpses.Count}; " +
                $"mean adult scale {world.MeanAdultScale:0.####}, " +
                $"mean investment {world.MeanBirthInvestment:0.####}, " +
                $"mean brood {world.MeanBroodSize:0.###}, " +
                $"mean body fraction {world.MeanBodyFraction:0.####}; " +
                $"growth short of matter {world.GrowthShortOfMatter}, " +
                $"under the mass floor {world.ConceptionsUnderMassFloor}; " +
                $"audit residual {world.AuditResidual:R} of {world.EnergyIn:0} J in; " +
                $"matter identity {identity:R} of {world.MatterInitialTotal:0}");

            Assert.True(world.Births > 0, "nothing was born, so the economy was not exercised");
            Assert.NotEmpty(world.Living);
            Assert.Equal(0L, world.ConceptionsShortOfMatter);

            // Somebody grew, or the pass under test never ran.
            Assert.True(world.MeanBodyFraction > 0f);
            Assert.True(
                world.Living.Count > 0 && HasGrown(world, bornAt),
                "every living body was born finished, so growth never moved a joule here");

            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(identity) <= 1e-6 * world.MatterInitialTotal,
                $"matter identity {identity:R}");
        }

        /// <summary>Whether any body alive is larger than the world says it was born.</summary>
        private static bool HasGrown(World world, Dictionary<long, float> bornAt)
        {
            foreach (Organism creature in world.Living)
            {
                if (bornAt.TryGetValue(creature.Id, out float fraction) &&
                    creature.BodyFraction > fraction + 1e-3f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

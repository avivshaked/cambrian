using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D229, D247: the fat cell and the body-and-store reserve cap, behind switches that are off by default
    /// (<c>logbook/specs/fat-build-spec.md</c>).
    /// </summary>
    public class FatTests
    {
        private readonly ITestOutputHelper _output;

        public FatTests(ITestOutputHelper output) => _output = output;

        // ------------------------------------------------------------------------ the fixtures

        private const float OverheadFloor = 5f;

        /// <summary>A bright one-patch world, with the cap and the table set.</summary>
        private static RunConfig Stage(
            ReserveCapForm form, float seconds, bool fat, float room = 50000f)
        {
            var config = new RunConfig
            {
                Light = new LightModel(60f, 12f),
                MinimumPopulation = 0,
                PerOffspringOverheadJoules = OverheadFloor,
                InitialMatterPerCubicMetre = 50f,
                ReserveCapSeconds = seconds,
                ReserveCapForm = form,
            };

            if (fat) config.CellTypes = CellTypeRegistry.Standard.Appending(new FatCell(room));
            return config;
        }

        private static Genome Reproducing(Genome genome, float margin)
        {
            genome.AdultScale = 1f;
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = margin };
            return genome;
        }

        /// <summary>One photosynthetic box.</summary>
        private static Genome Leaf(float margin = 0f, float half = 0.2f) =>
            Reproducing(Fixtures.SingleLeaf(half, CellTypeIds.Photosynthetic), margin);

        /// <summary>A photosynthetic root with a child of another cell type, joined face to face.</summary>
        private static Genome RootAndChild(string childType, float margin = 0f, float half = 0.2f)
        {
            var genome = new Genome();
            MorphNode root = Fixtures.Box(half);
            root.CellTypeId = CellTypeIds.Photosynthetic;
            MorphNode child = Fixtures.Box(half);
            child.CellTypeId = childType;
            root.Edges.Add(Fixtures.FaceToFace(1));
            genome.Nodes.Add(root);
            genome.Nodes.Add(child);
            genome.RootIndex = 0;
            return Reproducing(genome, margin);
        }

        private static double FatVolume(Organism body) =>
            body.Phenotype.Parts.Where(p => p.CellTypeId == CellTypeIds.Fat).Sum(p => (double)Math.Max(0f, p.Volume));

        /// <summary>The cap of the specification, written out from the body and the config, not from the world's code.</summary>
        private static double HandCap(Organism body, RunConfig config, float room)
        {
            double price = 0.5d * body.TissueJoules + 1d * OverheadFloor;
            double growth = Math.Max(0d, body.AdultTissueJoules - body.TissueJoules);
            return price + growth + (double)config.ReserveCapSeconds * body.StandingWatts + room * FatVolume(body);
        }

        private static void AssertBooksClose(World world)
        {
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-5 * Math.Max(1d, world.EnergyIn),
                $"audit residual {world.AuditResidual:R} of {world.EnergyIn:R} J in");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.StandingMatterUnits),
                $"matter residual {world.MatterResidual:R} of {world.StandingMatterUnits:R} units");
        }

        /// <summary>
        /// The audit closes but for the joules a fixture put into a purse by hand, which no book recorded: the residual is
        /// minus what was injected, to the float's rounding.
        /// </summary>
        private static void AssertAuditClosesButFor(World world, double injected)
        {
            Assert.True(
                Math.Abs(world.AuditResidual + injected) <= 1e-5 * Math.Max(1d, Math.Abs(injected)),
                $"audit residual {world.AuditResidual:R} against {injected:R} J put in by hand");
        }

        // ------------------------------------------------------------------------ the switches

        [Fact]
        public void TheFormAndTheSquashAreOffByDefaultAndTheBodyAndStoreCapAtZeroSecondsIsRefused()
        {
            var config = new RunConfig();
            Assert.Equal(ReserveCapForm.Seconds, config.ReserveCapForm);
            Assert.Equal(EnergySenseSquash.Tanh, config.EnergySenseSquash);
            Assert.Equal(30f, config.EnergyHalfScaleSeconds);

            // Zero means no cap under either form, and the form that reads a cap is refused with nothing to read.
            RunConfig refused = Stage(ReserveCapForm.BodyAndStore, 0f, fat: false);
            ArgumentException error = Assert.Throws<ArgumentException>(() => new World(refused, seed: 3));
            Assert.Contains("ReserveCapForm", error.Message);
            Assert.Contains("ReserveCapSeconds", error.Message);

            new World(Stage(ReserveCapForm.Seconds, 0f, fat: false), seed: 3);
            new World(Stage(ReserveCapForm.BodyAndStore, 100f, fat: false), seed: 3);

            // The half-scale is a time above zero.
            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new RunConfig().EnergyHalfScaleSeconds = bad);
            }

            Assert.NotEqual(new RunConfig().Hash(), new RunConfig { EnergySenseSquash = EnergySenseSquash.HalfScale }.Hash());
            Assert.NotEqual(new RunConfig().Hash(), new RunConfig { ReserveCapForm = ReserveCapForm.BodyAndStore }.Hash());
        }

        // ------------------------------------------------------------------------ the cell

        [Fact]
        public void FatIsNotInTheStandardTableAndItsParametersLiveOnTheType()
        {
            Assert.DoesNotContain(CellTypeIds.Fat, CellTypeRegistry.Standard.Ids());
            Assert.DoesNotContain("room=", CellTypeRegistry.Standard.HashContribution());

            var fat = new FatCell();
            Assert.Equal(CellTypeIds.Fat, fat.Id);
            Assert.Equal(0.5f, fat.UpkeepWattsPerCubicMetre);
            Assert.Equal(2000f, fat.TissueEnergyPerCubicMetre);
            Assert.Equal(50000f, fat.StoreRoomJoulesPerCubicMetre);
            Assert.Equal(0f, fat.AttackMax);
            Assert.Equal(0f, fat.IntakeMax);
            Assert.Equal(0f, fat.ProtectionMax);
            Assert.Equal(1f, fat.ToughnessMax);
            Assert.Equal(1f, fat.BudWeight);
            Assert.False(fat.AllowsJoint);
            Assert.False(fat.FixesLight);
            Assert.Equal(CellIntake.None, fat.Acquire(new CellContext(1f, 1f)));

            // Every other type has no room, so the base property changes nothing for it.
            foreach (string id in CellTypeRegistry.Standard.Ids())
            {
                Assert.Equal(0f, CellTypeRegistry.Standard.Resolve(id).StoreRoomJoulesPerCubicMetre);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => new FatCell(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FatCell(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FatCell(1f, 0f));

            // A change to the room is a different world.
            Assert.NotEqual(Stage(ReserveCapForm.Seconds, 0f, true, 50000f).Hash(), Stage(ReserveCapForm.Seconds, 0f, true, 40000f).Hash());
        }

        [Fact]
        public void FatSurvivesASaveAndReloadOfTheCellTableAndNoOtherTypeWritesARoom()
        {
            RunConfig config = Stage(ReserveCapForm.BodyAndStore, 100f, fat: true, room: 12345f);
            config.CellTypes.Resolve(CellTypeIds.Fat).TissueEnergyPerCubicMetre = 1900f;

            string text = RunConfigJson.Write(config);
            RunConfig back = RunConfigJson.Read(text);

            Assert.Equal(config.Hash(), back.Hash());
            Assert.Equal(ReserveCapForm.BodyAndStore, back.ReserveCapForm);
            Assert.Equal(string.Join(",", config.CellTypes.Ids()), string.Join(",", back.CellTypes.Ids()));
            Assert.Equal(CellTypeIds.Fat, back.CellTypes.At(back.CellTypes.Count - 1).Id);

            CellType fat = back.CellTypes.Resolve(CellTypeIds.Fat);
            Assert.Equal(12345f, fat.StoreRoomJoulesPerCubicMetre);
            Assert.Equal(1900f, fat.TissueEnergyPerCubicMetre);
            Assert.Equal(0.5f, fat.UpkeepWattsPerCubicMetre);

            // The field is fat's alone: a table without fat writes none, so no other type's file moved.
            Assert.Contains("storeRoomJoulesPerCubicMetre", text);
            Assert.DoesNotContain("storeRoomJoulesPerCubicMetre", RunConfigJson.Write(new RunConfig()));

            // A fat entry with the room missing is refused, as every field is.
            Assert.ThrowsAny<Exception>(() => RunConfigJson.Read(text.Replace("storeRoomJoulesPerCubicMetre", "somethingElse")));
        }

        [Fact]
        public void AMutationDrawsFatOnlyFromATableThatCarriesIt()
        {
            var budOnly = new MutationRates
            {
                CellTypeChance = 1f, AddNodeChance = 0f, ScalarChance = 0f, JointTypeChance = 0f,
                AddEdgeChance = 0f, RemoveEdgeChance = 0f, RecursiveLimitChance = 0f,
                FlagChance = 0f, ShapeChance = 0f,
            };

            int Drawn(CellTypeRegistry table)
            {
                int fat = 0;
                for (ulong seed = 1; seed <= 300; seed++)
                {
                    Genome parent = Fixtures.SingleBox();
                    parent.Nodes[0].CellTypeId = CellTypeIds.Photosynthetic;
                    Genome child = Mutator.Mutate(parent, new Rng(seed), budOnly, table);
                    fat += child.Nodes.Count(n => n.CellTypeId == CellTypeIds.Fat);
                }

                return fat;
            }

            Assert.Equal(0, Drawn(CellTypeRegistry.Standard));
            int withFat = Drawn(CellTypeRegistry.Standard.Appending(new FatCell()));
            _output.WriteLine($"fat in {withFat} of 300 buds, table with fat");
            Assert.True(withFat > 0, "fat was never drawn from a table that carries it");
        }

        // ------------------------------------------------------------------------ the cap

        [Fact]
        public void TheBodyAndStoreCapIsTheLitterPlusTheGrowthPlusSTimesWattsAndTheTrimHoldsAtIt()
        {
            RunConfig config = Stage(ReserveCapForm.BodyAndStore, 100f, fat: false);
            var world = new World(config, seed: 3);
            world.BirthsHeld = true;
            world.Inoculate(Leaf(), count: 1, heightY: -0.5f);
            Organism body = world.Living[0];

            // A newborn has growth still to come, and the cap holds room for it.
            double juvenile = world.ReserveCapJoules(body);
            double toGrow = body.AdultTissueJoules - body.TissueJoules;
            _output.WriteLine($"juvenile: cap {juvenile:0.####} J with {toGrow:0.####} J of growth to come");
            Assert.True(toGrow > 0d, "the fixture is born adult, so the growth term is not exercised");
            Assert.Equal(HandCap(body, config, 0f), juvenile, 9);

            // Grown up, the term is gone: the cap is the litter plus s seconds of the standing cost.
            for (int step = 0; step < 400 && body.BodyFraction < 1f; step++) world.Step(0.5f);
            Assert.Equal(1f, body.BodyFraction);
            double cap = world.ReserveCapJoules(body);
            double adult = body.AdultTissueJoules;
            Assert.Equal(HandCap(body, config, 0f), cap, 9);
            Assert.Equal(0.5d * body.TissueJoules + OverheadFloor + 100d * body.StandingWatts + Math.Max(0d, adult - body.TissueJoules), cap, 9);

            // The pure function, with the growth term moved by hand: 7 J of adult tissue still to come.
            double grown = Math.Max(0d, adult - body.TissueJoules);
            ContactEatingTests.Poke(body, "AdultTissueJoules", body.TissueJoules + 7d);
            Assert.Equal(HandCap(body, config, 0f), world.ReserveCapJoules(body), 9);
            Assert.Equal(cap - grown + 7d, world.ReserveCapJoules(body), 9);
            ContactEatingTests.Poke(body, "AdultTissueJoules", adult);

            // The trim: a hoard far over the cap is released to the water down to the cap and no lower.
            double injected = 1e5 - body.Energy;
            ContactEatingTests.Poke(body, "Energy", 1e5);
            double water = world.Nutrients.TotalJoules;
            world.Step(0.5f);

            double after = world.ReserveCapJoules(body);
            _output.WriteLine($"cap {after:0.####} J, reserve {body.Energy:0.####} J, trimmed {world.ReserveTrimmedTotal:0.####} J");
            Assert.InRange(body.Energy, after - 1e-2, after + 1e-2);
            Assert.True(world.ReserveTrimmedTotal > 9e4, "the hoard was not trimmed");
            Assert.True(world.DetritusExudedTotal >= world.ReserveTrimmedTotal, "the trim is counted with exudation");
            Assert.True(world.Nutrients.TotalJoules - water > 9e4, "the excess is in the water");
            AssertAuditClosesButFor(world, injected);

            // Seconds form of the same body, for contrast: D098's s × W and nothing else.
            RunConfig plain = Stage(ReserveCapForm.Seconds, 100f, fat: false);
            var other = new World(plain, seed: 3);
            other.BirthsHeld = true;
            other.Inoculate(Leaf(), count: 1, heightY: -0.5f);
            Organism same = other.Living[0];
            Assert.Equal(100d * same.StandingWatts, other.ReserveCapJoules(same), 9);
            Assert.True(other.ReserveCapJoules(same) < cap, "the body-and-store cap is the larger");
        }

        [Fact]
        public void NoBirthIsRefusedByTheCapWhileTheMarginGeneIsUnderSAndAMarginAboveItIsSterile()
        {
            // Under s: the cap clears the gate for every body, at every step, as a structural fact.
            RunConfig config = Stage(ReserveCapForm.BodyAndStore, 100f, fat: false);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(margin: 60f), count: 1, heightY: -0.5f);
            Organism parent = world.Living[0];

            for (int step = 0; step < 300; step++)
            {
                world.Step(0.5f);
                foreach (Organism b in world.Living)
                {
                    Assert.True(
                        world.ReserveCapJoules(b) >= b.BreedingGate(config) - 1e-9,
                        $"step {step}: the cap {world.ReserveCapJoules(b):R} sits under the gate {b.BreedingGate(config):R}");
                }
            }

            // A hoard over the cap still breeds: the trim runs first and leaves a reserve that clears the gate. Grown
            // first, because a juvenile's reserve goes into its growth before the births of the step.
            RunConfig fresh = Stage(ReserveCapForm.BodyAndStore, 100f, fat: false);
            var breeder = new World(fresh, seed: 3);
            breeder.BirthsHeld = true;
            breeder.Inoculate(Leaf(margin: 60f), count: 1, heightY: -0.5f);
            Organism adult = breeder.Living[0];
            for (int step = 0; step < 400 && adult.BodyFraction < 1f; step++) breeder.Step(0.5f);
            Assert.Equal(1f, adult.BodyFraction);
            Assert.Equal(0L, breeder.Births);

            double hoard = 1e5 - adult.Energy;
            ContactEatingTests.Poke(adult, "Energy", 1e5);
            breeder.BirthsHeld = false;
            breeder.Step(0.5f);
            Assert.True(breeder.Births > 0, "a body at its cap with its margin under s should breed");
            Assert.True(breeder.ReserveTrimmedTotal > 0d);
            AssertAuditClosesButFor(breeder, hoard);

            // Above s: the cap sits under the gate, nothing breeds however much is held, and the census says so.
            var sterile = new World(Stage(ReserveCapForm.BodyAndStore, 100f, fat: false), seed: 3);
            sterile.Inoculate(Leaf(margin: 150f), count: 1, heightY: -0.5f);
            Organism barren = sterile.Living[0];
            sterile.BirthsHeld = true;
            for (int step = 0; step < 400 && barren.BodyFraction < 1f; step++) sterile.Step(0.5f);
            Assert.Equal(1f, barren.BodyFraction);
            Assert.True(sterile.ReserveCapJoules(barren) < barren.BreedingGate(sterile.Config), "the cap should sit under the gate");
            sterile.BirthsHeld = false;
            ContactEatingTests.Poke(barren, "Energy", 1e5);
            for (int step = 0; step < 20; step++) sterile.Step(0.5f);
            Assert.Equal(0L, sterile.Births);
            Assert.Equal(1, sterile.CensusReserveCap().MarginAboveCap);
            Assert.Equal(0, world.CensusReserveCap().MarginAboveCap);
        }

        [Fact]
        public void FatRaisesTheCapByRoomTimesVolumeAndNothingElseDoes()
        {
            RunConfig low = Stage(ReserveCapForm.BodyAndStore, 100f, fat: true, room: 1000f);
            RunConfig high = Stage(ReserveCapForm.BodyAndStore, 100f, fat: true, room: 51000f);
            var worldLow = new World(low, seed: 3);
            var worldHigh = new World(high, seed: 3);
            worldLow.Inoculate(RootAndChild(CellTypeIds.Fat), 1, -0.5f);
            worldHigh.Inoculate(RootAndChild(CellTypeIds.Fat), 1, -0.5f);
            Organism a = worldLow.Living[0], b = worldHigh.Living[0];

            double volume = FatVolume(a);
            Assert.True(volume > 0d, "the fixture has no fat");
            Assert.Equal(volume, FatVolume(b), 12);

            // Same body, same standing cost, same price: the caps differ by the room's difference times the volume.
            Assert.Equal(a.StandingWatts, b.StandingWatts);
            Assert.Equal(50000d * volume, worldHigh.ReserveCapJoules(b) - worldLow.ReserveCapJoules(a), 6);
            Assert.Equal(HandCap(a, low, 1000f), worldLow.ReserveCapJoules(a), 9);
            Assert.Equal(HandCap(b, high, 51000f), worldHigh.ReserveCapJoules(b), 9);

            // A body with the same shape and no fat gets none of it.
            var plain = new World(Stage(ReserveCapForm.BodyAndStore, 100f, fat: true, room: 51000f), seed: 3);
            plain.Inoculate(RootAndChild(CellTypeIds.Structural), 1, -0.5f);
            Organism lean = plain.Living[0];
            Assert.Equal(0d, FatVolume(lean));
            Assert.Equal(HandCap(lean, plain.Config, 0f), plain.ReserveCapJoules(lean), 9);

            // Under the Seconds form fat's room is not read: the cap is s × W whatever the table holds.
            var seconds = new World(Stage(ReserveCapForm.Seconds, 100f, fat: true, room: 51000f), seed: 3);
            seconds.Inoculate(RootAndChild(CellTypeIds.Fat), 1, -0.5f);
            Organism fatBody = seconds.Living[0];
            Assert.Equal(100d * fatBody.StandingWatts, seconds.ReserveCapJoules(fatBody), 9);
        }

        [Fact]
        public void TheCensusCountsTheBodiesNearTheirCapTheFatCarriersAndFatsShareOfTheVolume()
        {
            RunConfig config = Stage(ReserveCapForm.BodyAndStore, 100f, fat: true);
            var world = new World(config, seed: 3);
            world.BirthsHeld = true;
            world.Inoculate(RootAndChild(CellTypeIds.Fat), 2, -0.5f);
            world.Inoculate(Leaf(), 1, -0.5f);
            Organism first = world.Living[0];
            Organism second = world.Living[1];
            Organism third = world.Living[2];

            // Put one body at 99.5% of its cap and the others at half (a founder's purse is over its cap until the trim runs).
            ContactEatingTests.Poke(first, "Energy", 0.995d * world.ReserveCapJoules(first));
            ContactEatingTests.Poke(second, "Energy", 0.5d * world.ReserveCapJoules(second));
            ContactEatingTests.Poke(third, "Energy", 0.5d * world.ReserveCapJoules(third));

            ReserveCapCensus census = world.CensusReserveCap();
            double fatVolume = world.Living.Sum(FatVolume);
            double volume = world.Living.Sum(o => o.Phenotype.Parts.Sum(p => (double)Math.Max(0f, p.Volume)));

            Assert.Equal(1, census.NearCap);
            Assert.Equal(2, census.FatCarriers);
            Assert.Equal(fatVolume, census.FatVolume, 12);
            Assert.Equal(volume, census.Volume, 12);
            Assert.Equal(fatVolume / volume, census.FatVolumeShare, 12);

            // No cap and no fat reads zeros.
            var off = new World(Stage(ReserveCapForm.Seconds, 0f, fat: false), seed: 3);
            off.Inoculate(Leaf(), 2, -0.5f);
            ReserveCapCensus none = off.CensusReserveCap();
            Assert.Equal(0, none.NearCap);
            Assert.Equal(0, none.FatCarriers);
            Assert.Equal(0d, none.FatVolumeShare);
        }

        [Fact]
        public void TheBooksCloseWithTheSwitchOnOverBirthsDeathsAndTrimmedHoards()
        {
            RunConfig config = Stage(ReserveCapForm.BodyAndStore, 100f, fat: true, room: 20000f);
            config.SenescenceDoublingSeconds = 3000f;
            var world = new World(config, seed: 5);
            world.Inoculate(RootAndChild(CellTypeIds.Fat), 3, -0.5f);
            world.Inoculate(Leaf(margin: 40f), 3, -0.5f);

            // Births held so the hoards reach the cap and the trim runs, then released so the cap meets the gate.
            world.BirthsHeld = true;
            for (int step = 0; step < 400; step++) world.Step(0.5f);
            double trimmedHeld = world.ReserveTrimmedTotal;
            world.BirthsHeld = false;
            for (int step = 0; step < 800; step++) world.Step(0.5f);

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}, trimmed {world.ReserveTrimmedTotal:0.###} J " +
                $"({trimmedHeld:0.###} while births were held); audit {world.AuditResidual:R}, matter {world.MatterResidual:R}");

            Assert.True(trimmedHeld > 0d, "the trim never ran while births were held");
            Assert.True(world.Births > 0L, "nothing was born once births were released");
            Assert.True(world.DetritusExudedTotal >= world.ReserveTrimmedTotal);
            AssertBooksClose(world);
        }

        // ------------------------------------------------------------------------ the bite on fat

        [Fact]
        public void ABiteOnAFatPartTakesTheLargerStoreShareUnderTheStoreRule()
        {
            double Taken(string childType, out double fraction, out double tissueShare)
            {
                RunConfig config = ContactEatingTests.Stage(on: true);
                config.BiteTakesStore = true;
                config.CellTypes = CellTypeRegistry.Standard.Appending(new FatCell());

                var world = new World(config, seed: 3);
                world.Inoculate(RootAndChild(childType), 1, -1f);
                world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake), 1, -1f);
                Organism body = world.Living[0];
                Organism hunter = world.Living[1];

                ContactEatingTests.Poke(body, "Energy", 200d);
                ContactEatingTests.Poke(body, "GestationJoules", 80d);

                double account = body.TissueJoules;
                double part = ContactEatingTests.TissueOf(body, 1, config);
                tissueShare = part / account;

                world.SetContacts(ContactEatingTests.Touching(hunter, 0, body, 1));
                world.ApplyMouth(1f);

                double healthTaken = 1d - body.PartHealth[1];
                Assert.True(healthTaken > 0d && healthTaken < 1d, $"the fixture should wound and not kill: {healthTaken}");

                fraction = healthTaken * part / account;
                ContactEatingTests.AssertRelative(fraction * 280d, world.BiteStoreTakenJoules, 1e-9, "the store taken is the tissue's share of the account");
                return world.BiteStoreTakenJoules;
            }

            double fat = Taken(CellTypeIds.Fat, out double fatFraction, out double fatShare);
            double lean = Taken(CellTypeIds.Photosynthetic, out double leanFraction, out double leanShare);

            _output.WriteLine($"fat part: {fatShare:0.###} of the tissue, {fat:0.###} J of store; lean part: {leanShare:0.###}, {lean:0.###} J");
            Assert.True(fatShare > leanShare, "a fat part is worth more of its body's tissue");
            Assert.True(fat > lean, "the bite on fat took no larger share of the store");
            ContactEatingTests.AssertRelative(fatShare / leanShare, fat / lean, 1e-6, "the store taken goes with the part's tissue value");
        }

        // ------------------------------------------------------------------------ the gut waits

        private static RunConfig GutStage(ReserveCapForm form, float seconds)
        {
            RunConfig config = ContactEatingTests.Stage(on: true);
            config.KillSwallowsBody = true;
            config.PerOffspringOverheadJoules = 1f;
            config.ReserveCapSeconds = seconds;
            config.ReserveCapForm = form;
            return config;
        }

        /// <summary>
        /// A kill into a small mouth's gut, then the hunter's reserve set <paramref name="room"/> under its cap and one more
        /// mouth pass with no bites, in which the gut digests as far as the mouth and the cap let it.
        /// </summary>
        private static (World World, Organism Hunter, double Cap, double Swallowed, double Injected) Kill(
            ReserveCapForm form, float seconds, double room)
        {
            RunConfig config = GutStage(form, seconds);
            var world = new World(config, seed: 3);
            world.Inoculate(ContactEatingTests.Branching(), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake * 0.001f), 1, -1f);
            Organism tree = world.Living[0];
            Organism hunter = world.Living[1];

            // The kill pass: the bites use the mouth's whole cap for the pass, so nothing digests in it.
            world.SetContacts(ContactEatingTests.Touching(hunter, 0, tree, 0));
            world.ApplyMouth(100f);
            double swallowed = world.SwallowedJoules;

            // Under the Seconds form the cap is s × W (100 s where the dial is off, for a like distance).
            double cap = form == ReserveCapForm.BodyAndStore
                ? world.ReserveCapJoules(hunter)
                : (seconds > 0f ? seconds : 100d) * (double)hunter.StandingWatts;
            double injected = (cap - room) - hunter.Energy;
            ContactEatingTests.Poke(hunter, "Energy", cap - room);

            world.SetContacts(new List<CreatureContact>());
            world.ApplyMouth(5f);
            return (world, hunter, cap, swallowed, injected);
        }

        [Fact]
        public void DigestionWaitsAtTheCapAndTheGutKeepsTheRest()
        {
            var waiting = Kill(ReserveCapForm.BodyAndStore, 100f, room: 0.2d);
            World world = waiting.World;
            Organism hunter = waiting.Hunter;

            _output.WriteLine(
                $"cap {waiting.Cap:0.####} J, reserve {hunter.Energy:0.####} J, gut {hunter.GutJoules:0.####} J of {waiting.Swallowed:0.####} J swallowed, " +
                $"waited on {world.GutWaitedAtReserveCap}, largest gut {world.LargestGutJoules:0.####} J");

            Assert.Equal(1L, world.KillsSwallowed);
            Assert.True(waiting.Swallowed > 10d, "the fixture swallowed too little to show the wait");

            // The reserve rose to the cap and no higher, and the gut kept what it could not digest.
            Assert.InRange(hunter.Energy, waiting.Cap - 1e-4, waiting.Cap + 1e-4);
            Assert.Equal(1L, world.GutWaitedAtReserveCap);
            double digested = world.GutDigestedJoules;
            Assert.InRange(digested, 0.2d / 0.75d - 1e-6, 0.2d / 0.75d + 1e-6);
            ContactEatingTests.AssertRelative(waiting.Swallowed, hunter.GutJoules + digested, 1e-12, "swallowed = held + digested");
            ContactEatingTests.AssertRelative(waiting.Swallowed, world.LargestGutJoules, 1e-12, "the largest gut held");

            // The books hold but for the joules the fixture put into the hunter's purse by hand.
            ContactEatingTests.AssertRelative(Math.Abs(waiting.Injected), Math.Abs(world.AuditResidual), 1e-6, "the audit residual is the hand-set purse");
            ContactEatingTests.AssertRelative(
                0.75d * world.GutDigestedJoules, hunter.Energy - (waiting.Cap - 0.2d), 1e-5,
                "the reserve took the digest less the intake waste");

            // The same kill with a reserve far under the cap digests what the mouth lets out and never waits.
            var free = Kill(ReserveCapForm.BodyAndStore, 100f, room: 1e6d);
            Assert.Equal(0L, free.World.GutWaitedAtReserveCap);
            Assert.True(free.World.GutDigestedJoules > digested, "room under the cap should digest more");
            Assert.Equal(waiting.Swallowed, free.Swallowed, 9);
        }

        [Fact]
        public void TheSecondsFormAndTheCapAtZeroDigestExactlyAsTheGutAlwaysDid()
        {
            var off = Kill(ReserveCapForm.Seconds, 0f, room: 0.2d);
            var seconds = Kill(ReserveCapForm.Seconds, 100f, room: 0.2d);

            // The cap, whatever it is, does not touch digestion under the Seconds form: the same bits.
            Assert.Equal(off.Swallowed, seconds.Swallowed);
            Assert.Equal(off.World.GutDigestedJoules, seconds.World.GutDigestedJoules);
            Assert.Equal(off.World.GutWasteJoules, seconds.World.GutWasteJoules);
            Assert.Equal(0L, off.World.GutWaitedAtReserveCap);
            Assert.Equal(0L, seconds.World.GutWaitedAtReserveCap);
            Assert.Equal(0d, seconds.World.LargestGutJoules);
            Assert.Equal(off.Hunter.GutJoules, seconds.Hunter.GutJoules);
        }

        [Fact]
        public void TheGutCountersAndTheFatFlagRoundTripThroughACheckpointAndAddNothingWhenOff()
        {
            // On: the two gut counters are written and read, and the pending birth row keeps its fat flag.
            RunConfig on = GutStage(ReserveCapForm.BodyAndStore, 100f);
            on.CellTypes = CellTypeRegistry.Standard.Appending(new FatCell());
            var world = new World(on, seed: 3);
            world.Inoculate(RootAndChild(CellTypeIds.Fat), 1, -1f);
            world.Inoculate(ContactEatingTests.Branching(), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake * 0.001f), 1, -1f);
            Organism hunter = world.Living[2];
            Organism tree = world.Living[1];
            world.SetContacts(ContactEatingTests.Touching(hunter, 0, tree, 0));
            world.ApplyMouth(100f);
            ContactEatingTests.Poke(hunter, "Energy", world.ReserveCapJoules(hunter) - 0.2d);
            world.SetContacts(new List<CreatureContact>());
            world.ApplyMouth(5f);
            Assert.True(world.GutWaitedAtReserveCap > 0L && world.LargestGutJoules > 0d, "the fixture never waited");

            byte[] state = ContactEatingTests.StateOf(world);
            World back = ContactEatingTests.Restored(on, 3, state);
            Assert.Equal(world.GutWaitedAtReserveCap, back.GutWaitedAtReserveCap);
            Assert.Equal(world.LargestGutJoules, back.LargestGutJoules);
            Assert.Equal(state, ContactEatingTests.StateOf(back));

            IReadOnlyList<LineageEvent> rows = back.DrainLineageEvents();
            Assert.Contains(rows, e => e.Kind == LineageEventKind.Birth && e.HasFat);
            Assert.Contains(rows, e => e.Kind == LineageEventKind.Birth && !e.HasFat);

            // Off: the same kill under the Seconds form and a table without fat writes the bytes it always wrote.
            RunConfig offConfig = GutStage(ReserveCapForm.Seconds, 100f);
            var offWorld = new World(offConfig, seed: 3);
            offWorld.Inoculate(ContactEatingTests.Branching(), 1, -1f);
            offWorld.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake * 0.001f), 1, -1f);
            byte[] offState = ContactEatingTests.StateOf(offWorld);

            RunConfig capped = GutStage(ReserveCapForm.BodyAndStore, 100f);
            var cappedWorld = new World(capped, seed: 3);
            cappedWorld.Inoculate(ContactEatingTests.Branching(), 1, -1f);
            cappedWorld.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake * 0.001f), 1, -1f);
            Assert.Equal(offState.Length + sizeof(long) + sizeof(double), ContactEatingTests.StateOf(cappedWorld).Length);

            // And a restore of the off state reads zeros.
            World offBack = ContactEatingTests.Restored(offConfig, 3, offState);
            Assert.Equal(0L, offBack.GutWaitedAtReserveCap);
            Assert.Equal(0d, offBack.LargestGutJoules);
        }

        [Fact]
        public void ABirthRowCarriesTheFatFlagOnlyWhereTheBodyHasFat()
        {
            var world = new World(Stage(ReserveCapForm.Seconds, 0f, fat: true), seed: 3);
            world.Inoculate(RootAndChild(CellTypeIds.Fat), 1, -0.5f);
            world.Inoculate(Leaf(), 1, -0.5f);
            IReadOnlyList<LineageEvent> rows = world.DrainLineageEvents();
            LineageEvent withFat = rows.First(e => e.Kind == LineageEventKind.Birth && e.HasFat);
            LineageEvent without = rows.First(e => e.Kind == LineageEventKind.Birth && !e.HasFat);

            // Beside the three flags, and absent where it is 0, so a world with no fat writes the rows it always wrote.
            string json = withFat.ToJson();
            Assert.Contains("\"pho\":1,\"fat\":1,\"pt\":", json.Replace(" ", ""));
            Assert.DoesNotContain("\"fat\"", without.ToJson());

            var plain = new World(Stage(ReserveCapForm.Seconds, 0f, fat: false), seed: 3);
            plain.Inoculate(Leaf(), 1, -0.5f);
            Assert.All(plain.DrainLineageEvents(), e => Assert.DoesNotContain("\"fat\"", e.ToJson()));
        }
    }
}

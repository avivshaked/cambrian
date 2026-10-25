using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D219, D220: a mouth eats only what it touches, and a bite feeds as it wounds. Each test names the rule of
    /// <c>logbook/specs/contact-eating-spec.md</c> it guards. Driven by hand as <see cref="MouthTests"/> is:
    /// Core has no physics, so the tests hand the world the contact list a bite would make.
    /// </summary>
    public class ContactEatingTests
    {
        private readonly ITestOutputHelper _output;

        public ContactEatingTests(ITestOutputHelper output) => _output = output;

        internal static readonly float ConsumerIntake = CellTypeRegistry.Standard.Resolve(CellTypeIds.Consumer).IntakeMax;

        // ------------------------------------------------------------------------ the fixtures

        /// <summary>A childless, bright world with health worth measuring.</summary>
        internal static RunConfig Stage(bool on, float waste = 0.25f, float healing = 0f)
        {
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(400f, 12f),
                InitialMatterPerCubicMetre = 50f,
                PerOffspringOverheadJoules = 1e9f,
                HealthPerCubicMetre = 100f,
                CorpseDecayPerSecond = 0.0001f,
                IntakeReachMetres = 5f,
                IntakeWasteFraction = waste,
                HealingPerSecond = healing,
                ContactEating = on,
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

        internal static Genome Leaf(int parts, float half = 0.2f) => Fixtures.MouthSpine(parts, half);

        /// <summary>A body with a claw and a mouth at its cell type's cap.</summary>
        internal static Genome Hunter(float intake) =>
            Fixtures.ArmedBox(attack: 1f, intake: intake, cellTypeId: CellTypeIds.Consumer);

        /// <summary>A claw and no mouth.</summary>
        internal static Genome Claw() => Fixtures.ArmedBox(attack: 1f);

        internal static List<CreatureContact> Touching(Organism a, int partA, Organism b, int partB) =>
            new List<CreatureContact> { new CreatureContact(a.Id, partA, b.Id, partB) };

        internal static void AssertRelative(double expected, double actual, double relative, string what)
        {
            double tolerance = Math.Max(1e-12, relative * Math.Abs(expected));
            Assert.True(
                Math.Abs(expected - actual) <= tolerance,
                $"{what}: expected {expected:R}, got {actual:R} (tolerance {tolerance:R})");
        }

        internal static void AssertBooksClose(World world, string where)
        {
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"{where}: audit residual {world.AuditResidual:R} against {world.StandingJoules:R} J standing");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"{where}: matter residual {world.MatterResidual:R} against {world.MatterInitialTotal:R} units");
        }

        internal static double TissueOf(Organism body, int part, RunConfig config) =>
            Metabolism.PartTissueJoules(body.Phenotype, body.Phenotype.Parts[part], config);

        /// <summary>The share of a part's health one hand-driven step took, read off the health array.</summary>
        private static double ShareTaken(Organism body, int part) => 1d - body.PartHealth[part];

        internal static byte[] StateOf(World world)
        {
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    world.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        internal static World Restored(RunConfig config, ulong seed, byte[] state)
        {
            var world = new World(config, seed);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                world.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            return world;
        }

        // ------------------------------------------------------------------------ the dials

        [Fact]
        public void TheDialsAreOffAndSixtySecondsByDefaultAndTheWindowIsRefusedNegativeOrNotFinite()
        {
            var config = new RunConfig();
            Assert.False(config.ContactEating);
            Assert.Equal(60f, config.RemainsWindowSeconds);

            Assert.Throws<ArgumentOutOfRangeException>(() => config.RemainsWindowSeconds = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.RemainsWindowSeconds = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => config.RemainsWindowSeconds = float.PositiveInfinity);

            config.RemainsWindowSeconds = 0f;
            Assert.Equal(0f, config.RemainsWindowSeconds);
        }

        [Fact]
        public void APartsTissueSharesSumToTheBodysTissueToTheBit()
        {
            // The per-part function is the share Metabolism.TissueJoules sums, the free first link's zero
            // included: the account is a sum over parts and the growth and plan-change code relies on it.
            var config = new RunConfig { FreeFirstLinkCubicMetres = 0.001f };

            int checkedBodies = 0;
            for (ulong seed = 1; seed <= 25; seed++)
            {
                Genome genome = GenomeFactory.Founder(new Rng(seed), cellTypes: config.CellTypes);
                Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes, cellTypes: config.CellTypes);
                if (body.PartCount == 0) continue;

                double sum = 0d;
                foreach (PhenotypePart part in body.Parts) sum += Metabolism.PartTissueJoules(body, part, config);

                Assert.Equal(Metabolism.TissueJoules(body, config), sum);
                checkedBodies++;
            }

            Assert.True(checkedBodies > 5, "too few bodies were developed to say anything");
        }

        // ------------------------------------------------------------------------ section 1: a bite feeds as it wounds

        [Fact]
        public void AWoundMovesItsShareOfThePartsTissueOutOfTheBittenBody()
        {
            RunConfig config = Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            double before = leaf.TissueJoules;
            double tissue = TissueOf(leaf, 1, config);

            world.SetContacts(Touching(hunter, 0, leaf, 1));
            world.ApplyMouth(1f);

            double s = ShareTaken(leaf, 1);
            _output.WriteLine($"share {s:0.####} of a part holding {tissue:0.####} J; account {before:0.####} -> {leaf.TissueJoules:0.####}");

            Assert.True(s > 0d && s < 1d, "the fixture should wound and not kill");
            AssertRelative(before - s * tissue, leaf.TissueJoules, 1e-9, "the bitten body's account");
            AssertBooksClose(world, "after the bite");
        }

        [Fact]
        public void ABiterWhosePartCarriesNoIntakeKeepsNothingAndEverythingGoesToNutrients()
        {
            RunConfig config = Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            world.Inoculate(Claw(), 1, -1f);
            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];
            Assert.False(claw.HasIntake);

            double reserve = claw.Energy;
            double water = world.Nutrients.TotalJoules;
            double detritus = world.DetritusDepositedTotal;
            double tissue = TissueOf(leaf, 1, config);

            world.SetContacts(Touching(claw, 0, leaf, 1));
            world.ApplyMouth(1f);

            double offered = ShareTaken(leaf, 1) * tissue;
            Assert.True(offered > 0d);

            Assert.Equal(reserve, claw.Energy);
            Assert.Equal(0d, world.BiteKeptFromLeavesJoules);
            Assert.Equal(0d, world.BiteKeptFromOthersJoules);
            Assert.Equal(0d, claw.BiteKeptJoules);
            AssertRelative(offered, world.Nutrients.TotalJoules - water, 1e-6, "the water's gain");
            AssertRelative(offered, world.DetritusDepositedTotal - detritus, 1e-6, "the detritus book");
            AssertRelative(offered, world.BiteWasteJoules, 1e-6, "the waste total");
            AssertBooksClose(world, "after the bite");
        }

        [Fact]
        public void ABitingPartKeepsNoMoreThanItsIntakeTimesAreaTimesScaleTimesTheStepAtAHundredJoulesAUnit()
        {
            RunConfig config = Stage(on: true);
            config.IntakeScale = 2f;
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake * 0.001f), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            PhenotypePart mouth = hunter.Phenotype.Parts[0];
            const float seconds = 0.5f;
            double cap = (double)mouth.Intake * mouth.SurfaceArea * 2d * seconds * config.JoulesPerUnit;
            double tissue = TissueOf(leaf, 1, config);
            double reserve = hunter.Energy;
            double water = world.Nutrients.TotalJoules;

            world.SetContacts(Touching(hunter, 0, leaf, 1));
            world.ApplyMouth(seconds);

            double offered = ShareTaken(leaf, 1) * tissue;
            _output.WriteLine($"offered {offered:0.####} J against a cap of {cap:0.####} J");
            Assert.True(offered > 4d * cap, "the fixture should offer far more than the mouth can carry");

            // The reserve takes the difference to the water's float, so its gain is exact to the float's rounding of the
            // whole offer and not of the kept share.
            AssertRelative(cap * (1d - config.IntakeWasteFraction), hunter.Energy - reserve, 1e-5, "the reserve's gain");
            AssertRelative(offered - cap * (1d - config.IntakeWasteFraction), world.Nutrients.TotalJoules - water, 1e-6, "the water's gain");
            AssertBooksClose(world, "after the bite");
        }

        [Fact]
        public void SeveralWoundsByOnePartShareOneCapInProportionToWhatEachOffered()
        {
            RunConfig config = Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2, 0.2f), 1, -1f);
            world.Inoculate(Fixtures.ArmedBox(half: 0.3f), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake * 0.001f), 1, -1f);
            Organism leaf = world.Living[0];
            Organism box = world.Living[1];
            Organism hunter = world.Living[2];
            Assert.False(box.HasPhotosyntheticTissue);

            PhenotypePart mouth = hunter.Phenotype.Parts[0];
            double cap = (double)mouth.Intake * mouth.SurfaceArea * 1d * config.JoulesPerUnit;
            double tissueLeaf = TissueOf(leaf, 1, config);
            double tissueBox = TissueOf(box, 0, config);
            double reserve = hunter.Energy;

            world.SetContacts(new List<CreatureContact>
            {
                new CreatureContact(hunter.Id, 0, leaf.Id, 1),
                new CreatureContact(hunter.Id, 0, box.Id, 0),
            });
            world.ApplyMouth(1f);

            double offerLeaf = ShareTaken(leaf, 1) * tissueLeaf;
            double offerBox = ShareTaken(box, 0) * tissueBox;
            double total = offerLeaf + offerBox;
            _output.WriteLine($"offers {offerLeaf:0.####} and {offerBox:0.####} J against one cap of {cap:0.####} J");
            Assert.True(total > 4d * cap, "the fixture should offer far more than the mouth can carry");

            double keptTotal = cap * (1d - config.IntakeWasteFraction);
            AssertRelative(keptTotal, hunter.Energy - reserve, 1e-5, "one cap for the part");
            AssertRelative(keptTotal * offerLeaf / total, world.BiteKeptFromLeavesJoules, 1e-4, "the leaf's share");
            AssertRelative(keptTotal * offerBox / total, world.BiteKeptFromOthersJoules, 1e-4, "the box's share");
            Assert.Equal(hunter.Energy - reserve, hunter.BiteKeptJoules);
            AssertBooksClose(world, "after the bites");
        }

        [Fact]
        public void TheKeptShareFeedsTheReserveAndTheWasteGoesToTheWaterAndTheDetritusBookAsOneNumber()
        {
            RunConfig config = Stage(on: true, waste: 0.25f);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            double tissue = TissueOf(leaf, 1, config);
            double reserve = hunter.Energy;
            double water = world.Nutrients.TotalJoules;
            double detritus = world.DetritusDepositedTotal;

            world.SetContacts(Touching(hunter, 0, leaf, 1));
            world.ApplyMouth(1f);

            double offered = ShareTaken(leaf, 1) * tissue;
            double gain = hunter.Energy - reserve;
            double waste = world.BiteWasteJoules;
            _output.WriteLine($"offered {offered:0.####} J: reserve +{gain:0.####}, water +{waste:0.####}");

            AssertRelative(0.75d * offered, gain, 1e-6, "the reserve takes 1 - waste of the offer");
            AssertRelative(offered, gain + waste, 1e-12, "what leaves the tissue is what the two gain");
            Assert.Equal(waste, world.DetritusDepositedTotal - detritus, 12);
            AssertRelative(waste, world.Nutrients.TotalJoules - water, 1e-6, "the water's gain");
            Assert.Equal(gain, world.BiteKeptFromLeavesJoules, 12);
            Assert.Equal(0d, world.BiteKeptFromOthersJoules);
            AssertBooksClose(world, "after the bite");
        }

        // ------------------------------------------------------------------------ section 2: healing re-buys the tissue

        [Fact]
        public void HealingReBuysTheTissueItRestoresAndBurnsOnlyTheHealthsPrice()
        {
            RunConfig config = Stage(on: true, healing: 0.05f);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            world.SetContacts(Touching(hunter, 0, leaf, 1));
            world.ApplyMouth(0.1f);

            double health = leaf.PartHealth[1];
            Assert.True(health > 0d && health < 1d);

            double tissueBefore = leaf.TissueJoules;
            double reserve = leaf.Energy;
            double burnt = world.EnergyOut;
            double rebought = world.HealTissueJoules;
            double pool = Metabolism.HealthPool(leaf.Phenotype.Parts[1], config);
            double t = TissueOf(leaf, 1, config);

            world.ApplyMouth(1f);

            double healed = leaf.PartHealth[1] - health;
            double expectedShare = Math.Min(1d - health, 0.05d * 1d);
            _output.WriteLine($"healed {healed:0.######} of an expected {expectedShare:0.######}");
            AssertRelative(expectedShare, healed, 1e-5, "the share healed");

            double burn = world.EnergyOut - burnt;
            double bought = leaf.TissueJoules - tissueBefore;
            AssertRelative(expectedShare * pool * config.HealingJoulesPerHealth, burn, 1e-5, "the health's price, burnt");
            AssertRelative(expectedShare * t, bought, 1e-5, "the tissue re-bought");
            AssertRelative(bought, world.HealTissueJoules - rebought, 1e-12, "the instrument");
            AssertRelative(burn + bought, reserve - leaf.Energy, 1e-9, "the reserve paid both");
            AssertBooksClose(world, "after healing");
        }

        [Fact]
        public void ABodyThatCannotPayTheBurnAndTheTissueTogetherHealsInProportionToWhatItCanPay()
        {
            // Healing is off while the wound is made and on for the step under test, so the repair is one pass.
            RunConfig config = Stage(on: true, healing: 0f);
            config.FounderEnergyJoules = 0.5f;
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            world.SetContacts(Touching(hunter, 0, leaf, 1));
            world.ApplyMouth(0.5f);

            config.HealingPerSecond = 0.5f;
            double health = leaf.PartHealth[1];
            double pool = Metabolism.HealthPool(leaf.Phenotype.Parts[1], config);
            double t = TissueOf(leaf, 1, config);
            double share = Math.Min(1d - health, 0.5d);
            double wanted = share * (pool * config.HealingJoulesPerHealth + t);
            double reserve = leaf.Energy;
            Assert.True(reserve > 0d && reserve < 0.5d * wanted, $"the fixture should leave a reserve under the price: {reserve} against {wanted}");

            world.ApplyMouth(1f);

            double healed = leaf.PartHealth[1] - health;
            _output.WriteLine($"wanted {wanted:0.####} J, could pay {reserve:0.######} J: healed {healed:0.######} of {share:0.######}");
            AssertRelative(share * reserve / wanted, healed, 1e-4, "the share healed in proportion");
            Assert.True(leaf.Energy >= 0d && leaf.Energy < 1e-4 * reserve + 1e-12, $"the reserve should be spent, not {leaf.Energy:R}");
        }

        // ------------------------------------------------------------------------ section 3: growth reads the intact body

        [Fact]
        public void ABittenAdultGrowsBackToItsAdultSizeAndNeverShrinks()
        {
            RunConfig config = Stage(on: true, healing: 0.2f);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(3), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            Phenotype adult = leaf.AdultPhenotype;
            Assert.Same(adult, leaf.Phenotype);
            double adultTissue = leaf.AdultTissueJoules;

            world.SetContacts(Touching(hunter, 0, leaf, 2));
            world.Step(0.5f);

            Assert.NotNull(leaf.PartHealth);
            Assert.True(leaf.PartHealth[2] < 1f, "the bite should have landed");
            Assert.True(leaf.TissueJoules < adultTissue - 1e-6, "the account should be short by the eaten share");

            for (int step = 0; step < 60; step++)
            {
                world.Step(0.5f);

                // Never re-scaled: the body is the adult phenotype itself, and its fraction is whole.
                Assert.Same(adult, leaf.Phenotype);
                Assert.Equal(1f, leaf.BodyFraction);
                Assert.True(leaf.TissueJoules <= adultTissue * (1d + 1e-9), $"step {step}: {leaf.TissueJoules:R}");
                AssertBooksClose(world, $"step {step}");
            }

            _output.WriteLine($"health {string.Join(", ", leaf.PartHealth)}; account {leaf.TissueJoules:R} of {adultTissue:R} J");
            Assert.All(leaf.PartHealth, h => Assert.Equal(1f, h));
            AssertRelative(adultTissue, leaf.TissueJoules, 1e-9, "the account is the adult's again");
        }

        [Fact]
        public void ABittenJuvenileKeepsGrowingToItsAdultSizeAndTheBooksClose()
        {
            RunConfig config = Stage(on: true, healing: 0.1f);
            config.FounderEnergyJoules = 2000f;
            Genome genome = Leaf(3);
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.4f };

            var world = new World(config, seed: 3);
            world.Inoculate(genome, 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            Assert.True(leaf.BodyFraction < 1f, "the fixture should be born small");
            float fraction = leaf.BodyFraction;

            world.SetContacts(Touching(hunter, 0, leaf, 2));
            world.Step(0.5f);
            Assert.True(leaf.PartHealth != null && leaf.PartHealth[2] < 1f, "the bite should have landed");

            for (int step = 0; step < 120; step++)
            {
                world.Step(0.5f);
                Assert.True(leaf.BodyFraction >= fraction - 1e-6f, $"step {step}: {leaf.BodyFraction} after {fraction}");
                fraction = leaf.BodyFraction;
                AssertBooksClose(world, $"step {step}");
            }

            _output.WriteLine($"fraction {leaf.BodyFraction}; health {string.Join(", ", leaf.PartHealth)}");
            Assert.Equal(1f, leaf.BodyFraction);
            AssertRelative(leaf.AdultTissueJoules, leaf.TissueJoules, 1e-6, "the account is the adult's again");
        }

        // ------------------------------------------------------------------------ section 4: the leaf

        [Fact]
        public void AGrazedLeafsLitAreaAndLightIncomeFallInProportionToItsHealth()
        {
            RunConfig config = Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(1), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            float whole = World.PhotosyntheticLitArea(leaf, config.CellTypes, false, config.LightSilhouetteCap);
            Assert.True(whole > 0f);

            world.SetContacts(Touching(hunter, 0, leaf, 0));
            world.ApplyMouth(0.5f);
            double h = leaf.PartHealth[0];
            Assert.True(h > 0d && h < 1d, $"the fixture should graze and not kill: {h}");

            float grazed = World.PhotosyntheticLitArea(leaf, config.CellTypes, false, config.LightSilhouetteCap, byHealth: true);
            float ignoring = World.PhotosyntheticLitArea(leaf, config.CellTypes, false, config.LightSilhouetteCap);
            Assert.Equal(whole, ignoring);
            AssertRelative(whole * h, grazed, 1e-6, "the leaf's lit area");

            EnergyLedger full = Metabolism.StepAtHealth(
                leaf.Phenotype, config, 100f, 0f, 1e6f, 0f, 1f, 0f, null, default, null);
            EnergyLedger hurt = Metabolism.StepAtHealth(
                leaf.Phenotype, config, 100f, 0f, 1e6f, 0f, 1f, 0f, null, default, leaf.PartHealth);
            EnergyLedger recorded = Metabolism.StepAt(leaf.Phenotype, config, 100f, 0f, 1e6f, 0f, 1f);

            _output.WriteLine($"light income {full.LightIncome:R} whole, {hurt.LightIncome:R} at health {h:R}");
            Assert.Equal(recorded.LightIncome, full.LightIncome);
            Assert.True(full.LightIncome > 0f);
            AssertRelative(full.LightIncome * h, hurt.LightIncome, 1e-5, "the light fixed");
        }

        // ------------------------------------------------------------------------ section 5: no corpse, and the remains

        [Fact]
        public void ACutOffLimbFoundsRemainsAndNoCorpseAndNothingIsBurntAtTheCut()
        {
            RunConfig config = Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(3), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            double burnt = world.EnergyOut;

            // Bite the middle part until it comes off. Its tissue went to the biter as it was bitten; the part that
            // hung from it, and its share of the reserve, are what the remains hold.
            for (int step = 0; step < 100 && world.PartsKilled == 0L; step++)
            {
                world.SetContacts(Touching(hunter, 0, leaf, 1));
                world.ApplyMouth(1f);
                AssertBooksClose(world, $"bite {step}");
            }

            Assert.Equal(1L, world.PartsKilled);
            Assert.Empty(world.Corpses);
            Assert.Equal(0L, world.CorpsesFromKills);
            Assert.Single(world.Remains);
            Assert.Equal(1, world.Remains[0].Phenotype.PartCount);
            Assert.True(world.Remains[0].TissueJoules > 0d, "the limb's tissue should stand in the remains");
            Assert.True(world.Remains[0].Energy > 0d, "the limb's share of the reserve should stand in the remains");
            Assert.Equal(burnt, world.EnergyOut);
            AssertBooksClose(world, "after the limb");
        }

        [Fact]
        public void ADeathFoundsRemainsWithTheStoreAndTheBodyLeavesTheLiving()
        {
            // Dying of age with a reserve in hand: the whole body is the remains, its reserve the store.
            RunConfig config = RemainsStage();
            config.AgeHazardBaseSeconds = 2f;
            var world = new World(config, seed: 3);
            world.Inoculate(Branching(), 1, -1f);
            Organism body = world.Living[0];
            double tissue = body.TissueJoules;

            double burnt = world.EnergyOut;
            for (int step = 0; step < 400 && world.Remains.Count == 0; step++) world.Step(0.5f);

            Assert.Single(world.Remains);
            Assert.Empty(world.Living);
            Assert.Equal(1L, world.Deaths);
            Assert.Equal(1L, world.DeathsByAge);
            Assert.Empty(world.Corpses);
            Assert.Equal(0L, world.CorpsesFromKills);

            // The body itself is the remains: its id, its phenotype, its tissue, and its reserve as the store.
            Organism remains = world.Remains[0];
            Assert.Same(body, remains);
            Assert.True(remains.IsRemains);
            Assert.Equal(world.ElapsedSeconds, remains.RemainsSince);
            Assert.Equal(tissue, remains.TissueJoules);
            Assert.Equal(5, remains.Phenotype.PartCount);
            Assert.Equal(0d, remains.GestationJoules);

            LineageEvent death = world.DrainLineageEvents().Single(e => e.Kind == LineageEventKind.Death);
            Assert.Equal(DeathCause.Aged, death.Cause);
            Assert.True(death.ReserveJoulesAtDeath > 0d, "the body should have died holding a reserve");
            Assert.Equal(death.ReserveJoulesAtDeath, remains.Energy);
            Assert.Equal(death.ReserveJoulesAtDeath, remains.RemainsStoreJoules);

            Assert.Equal(1, world.RemainsStanding);
            AssertRelative(tissue + remains.Energy, world.RemainsStandingJoules, 1e-12, "the remains standing");
            Assert.Empty(world.DrainRemainsFoundings());
            AssertBooksClose(world, "after the death");
            _output.WriteLine($"died of age with {remains.Energy:0.###} J in store and {tissue:0.###} J of tissue; burnt {world.EnergyOut - burnt:0.###} J since");
        }

        [Fact]
        public void ABodyWhoseRootIsEatenWholeSplitsIntoItsChildSubtreesAndAOnePartBodyLeavesNothing()
        {
            // The root is the body's: eaten whole, the body dies, and what hung from it is no longer one body.
            RunConfig config = RemainsStage();
            var world = new World(config, seed: 3);
            world.Inoculate(Branching(), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism tree = world.Living[0];
            Organism hunter = world.Living[1];

            double tissueOfTheRest = 0d;
            for (int p = 1; p < 5; p++) tissueOfTheRest += TissueOf(tree, p, config);
            double reserve = tree.Energy;

            world.SetContacts(Touching(hunter, 0, tree, 0));
            world.ApplyMouth(100f);

            Assert.Equal(1L, world.BodiesEaten);
            Assert.DoesNotContain(world.Living, c => c.Id == tree.Id);

            // One child of the root, so one remains, a new body of its four parts with the whole store.
            Assert.Single(world.Remains);
            Organism rest = world.Remains[0];
            Assert.NotEqual(tree.Id, rest.Id);
            Assert.Equal(tree.Id, rest.ParentId);
            Assert.Equal(4, rest.Phenotype.PartCount);
            Assert.Equal(new[] { 1f, 1f, 1f, 1f }, rest.PartHealth);
            AssertRelative(tissueOfTheRest, rest.TissueJoules, 1e-9, "the rest of the body's tissue");
            AssertRelative(reserve, rest.Energy, 1e-9, "the store");
            Assert.Equal(1L, world.RemainsClosed);

            IReadOnlyList<RemainsFounding> foundings = world.DrainRemainsFoundings();
            Assert.Single(foundings);
            Assert.Equal(rest.Id, foundings[0].RemainsId);
            Assert.Equal(tree.Id, foundings[0].FromBodyId);
            Assert.Equal(new[] { 1, 2, 3, 4 }, foundings[0].Map);
            AssertBooksClose(world, "after the root was eaten");

            // A body of one part has nothing hanging from the part: nothing can be bitten, and its store is burnt.
            var small = new World(config, seed: 3);
            small.Inoculate(Leaf(1), 1, -1f);
            small.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = small.Living[0];
            double burnt = small.EnergyOut;
            double held = leaf.Energy;

            small.SetContacts(Touching(small.Living[1], 0, leaf, 0));
            small.ApplyMouth(100f);

            Assert.Equal(1L, small.BodiesEaten);
            Assert.Empty(small.Remains);
            Assert.DoesNotContain(small.Living, c => c.Id == leaf.Id);
            AssertRelative(held, small.EnergyOut - burnt, 1e-9, "the store burnt");
            AssertBooksClose(small, "after the one-part body was eaten");
        }

        [Fact]
        public void AStarvedBodyKeepsItsWholeTissueInItsRemainsAndTheDeathRowIsStillWritten()
        {
            RunConfig config = Stage(on: true);
            config.FounderEnergyJoules = 0.001f;
            var world = new World(config, seed: 3);
            world.Inoculate(Fixtures.ArmedBox(), 1, -1f);
            Organism body = world.Living[0];
            double tissue = body.TissueJoules;
            Assert.True(tissue > 0d);

            for (int step = 0; step < 400 && world.Remains.Count == 0; step++) world.Step(0.5f);

            Assert.Single(world.Remains);
            Assert.Same(body, world.Remains[0]);
            Assert.Empty(world.Living);
            Assert.Equal(tissue, body.TissueJoules);
            Assert.Equal(world.ElapsedSeconds, body.RemainsSince);
            Assert.Contains(world.DrainLineageEvents(), e => e.Kind == LineageEventKind.Death && e.Id == body.Id);
            Assert.Empty(world.DrainRemainsFoundings());
            AssertBooksClose(world, "after the starvation");
        }

        [Fact]
        public void ADivergedBodyTakesTheSameRouteAndLeavesNoCorpse()
        {
            RunConfig config = Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            Organism leaf = world.Living[0];
            double joules = leaf.TissueJoules + leaf.Energy;
            double burnt = world.EnergyOut;

            world.KillDiverged(leaf);

            Assert.Empty(world.Corpses);
            AssertRelative(joules, world.EnergyOut - burnt, 1e-12, "the whole body burnt");
            AssertBooksClose(world, "after the divergence");
        }

        [Fact]
        public void ACorpseMealNeverRunsWithTheDialOn()
        {
            RunConfig config = Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism hunter = world.Living[0];
            Float3 at = hunter.Point.Position;
            world.PlaceCorpse(new Float3(at.X, at.Y, at.Z), 100d);

            double reserve = hunter.Energy;
            for (int i = 0; i < 5; i++) world.ApplyMouth(0.5f);

            Assert.Equal(reserve, hunter.Energy);
            Assert.Equal(0d, hunter.MouthJoules);
            Assert.Equal(0d, world.UnitsEaten);
        }

        // ------------------------------------------------------------------------ the books

        /// <summary>One body of every kind, a hunter at the leaves, healing on: the world the books are read over.</summary>
        private static World BitingWorld(bool on)
        {
            RunConfig config = Stage(on, healing: 0.04f);
            config.SenescenceDoublingSeconds = 0f;

            var world = new World(config, seed: 11);
            for (int i = 0; i < 3; i++) world.Inoculate(Leaf(3, 0.15f + 0.03f * i), 1, -1f);
            world.Inoculate(Fixtures.ArmedBox(half: 0.3f), 2, -1f);
            world.Inoculate(Hunter(ConsumerIntake * 0.3f), 2, -1f);
            world.Inoculate(Claw(), 1, -1f);
            return world;
        }

        /// <summary>The contacts of one step: every armed body bites the prey the step number picks, part by part.</summary>
        private static List<CreatureContact> Bites(World world, int step)
        {
            var biters = world.Living.Where(c => c.HasAttack).ToList();
            var prey = world.Living.Where(c => !c.HasAttack).ToList();
            var contacts = new List<CreatureContact>();
            if (prey.Count == 0) return contacts;

            for (int i = 0; i < biters.Count; i++)
            {
                Organism victim = prey[(step + i) % prey.Count];
                int part = (step / 3 + i) % victim.Phenotype.PartCount;
                contacts.Add(new CreatureContact(biters[i].Id, 0, victim.Id, part));
            }

            return contacts;
        }

        private static void RunBites(World world, int steps, Action<int> after = null)
        {
            for (int step = 0; step < steps; step++)
            {
                world.SetContacts(Bites(world, step));
                world.Step(0.5f);
                after?.Invoke(step);
            }
        }

        /// <summary>
        /// The digest of <see cref="BitingWorld"/>'s state after each of 400 steps, dial on and dial off, recorded at 91795444, the build
        /// before D236's leaf-food counter (Organism.FoodFromLeavesJoules) and before the switching tank's WitherForTank. With the variable
        /// EVOSIM_CE_DIGEST_RECORD naming a file, the test writes the digests there instead of comparing.
        /// </summary>
        private const string BitingWorldDigests =
            "on 03ba92ab45bc204a7c899b977c904edfe3b63b49df84328ed9206e7ea7322ce0 off f3c0cde918f4e572461dea7dadf026496c5855132b29c39973a2e3ff0daf75b0";

        [Fact]
        public void ABitingWorldStepsToTheStatesItSteppedToBeforeTheLeafFoodCounter()
        {
            string Digest(bool on)
            {
                World world = BitingWorld(on);
                using (var sha = SHA256.Create())
                {
                    RunBites(world, 400, step =>
                    {
                        byte[] state = StateOf(world);
                        sha.TransformBlock(state, 0, state.Length, null, 0);
                    });
                    sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                }
            }

            string digests = $"on {Digest(true)} off {Digest(false)}";
            _output.WriteLine(digests);
            string record = Environment.GetEnvironmentVariable("EVOSIM_CE_DIGEST_RECORD");
            if (!string.IsNullOrEmpty(record)) { File.WriteAllText(record, digests); return; }
            Assert.Equal(BitingWorldDigests, digests);
        }

        [Fact]
        public void TheMatterAndEnergyBooksCloseOverAFewHundredStepsOfADialOnWorldWithBites()
        {
            World world = BitingWorld(on: true);
            RunBites(world, 400, step => AssertBooksClose(world, $"step {step}"));

            _output.WriteLine(
                $"alive {world.Living.Count}, deaths {world.Deaths}, parts killed {world.PartsKilled}, bodies eaten {world.BodiesEaten}; " +
                $"kept {world.BiteKeptFromLeavesJoules:0.###} + {world.BiteKeptFromOthersJoules:0.###} J, waste {world.BiteWasteJoules:0.###} J, " +
                $"healed {world.HealTissueJoules:0.###} J; audit {world.AuditResidual:R}, matter {world.MatterResidual:R}");

            Assert.True(world.BiteWasteJoules > 0d, "nothing was bitten");
            Assert.True(world.BiteKeptFromLeavesJoules + world.BiteKeptFromOthersJoules > 0d, "nothing was kept");
            Assert.True(world.HealTissueJoules > 0d, "nothing was healed");
            Assert.True(world.PartsKilled + world.BodiesEaten > 0L, "no part or body was eaten whole, so LeaveRemains was not read");
            Assert.Empty(world.Corpses);
        }

        // ------------------------------------------------------------------------ the checkpoint

        [Fact]
        public void ADialOnCheckpointRoundTripRestoresEveryNewTotalAndTheResumeIsTheSameWorld()
        {
            World world = BitingWorld(on: true);
            RunBites(world, 150);
            Assert.True(world.BiteWasteJoules > 0d && world.HealTissueJoules > 0d);

            byte[] state = StateOf(world);
            World restored = Restored(world.Config, 11, state);

            Assert.Equal(world.BiteKeptFromLeavesJoules, restored.BiteKeptFromLeavesJoules);
            Assert.Equal(world.BiteKeptFromOthersJoules, restored.BiteKeptFromOthersJoules);
            Assert.Equal(world.BiteWasteJoules, restored.BiteWasteJoules);
            Assert.Equal(world.HealTissueJoules, restored.HealTissueJoules);
            Assert.Equal(
                world.Living.Select(c => c.BiteKeptJoules).ToList(),
                restored.Living.Select(c => c.BiteKeptJoules).ToList());
            Assert.Equal(StateOf(world), StateOf(restored));

            RunBites(world, 60);
            RunBites(restored, 60);

            Assert.Equal(world.BiteWasteJoules, restored.BiteWasteJoules);
            Assert.Equal(world.HealTissueJoules, restored.HealTissueJoules);
            Assert.Equal(world.BiteKeptFromLeavesJoules, restored.BiteKeptFromLeavesJoules);
            Assert.Equal(StateOf(world), StateOf(restored));
        }

        [Fact]
        public void ADeathRowCarriesTheBodysOwnKeptJoulesAndACheckpointKeepsThemInTheQueue()
        {
            World world = BitingWorld(on: true);
            double kept = 0d;
            for (int step = 0; step < 600 && world.Deaths == 0L; step++)
            {
                world.SetContacts(Bites(world, step));
                world.Step(0.5f);
            }

            LineageEvent[] events = world.DrainLineageEvents().ToArray();
            Assert.NotEmpty(events);
            foreach (LineageEvent e in events.Where(e => e.Kind == LineageEventKind.Death)) kept += e.BiteKeptJoules;
            _output.WriteLine($"deaths {world.Deaths}, kept by the dead {kept:0.####} J");

            // A hunter or claw that starved or was eaten would carry its kept joules; at least the row's field
            // exists and is never negative.
            Assert.All(events.Where(e => e.Kind == LineageEventKind.Death), e => Assert.True(e.BiteKeptJoules >= 0d));
        }

        [Fact]
        public void ADialOffWorldWritesNothingForTheDialAndADialOnWorldWritesItsTotalsAndSectionsAndEightPerBody()
        {
            World off = BitingWorld(on: false);
            World on = BitingWorld(on: true);

            int bodies = on.Living.Count;
            Assert.Equal(bodies, off.Living.Count);
            // The brief-1 totals (four doubles) and a double a body; D220's four totals (two doubles and two longs) and its two
            // sections (a marker and a count each: the remains standing, the foundings undrained), both empty here.
            int remainsTotals = 2 * sizeof(double) + 2 * sizeof(long);
            int remainsSections = 2 * (4 + sizeof(int));
            Assert.Equal(
                StateOf(off).Length + 4 * sizeof(double) + bodies * sizeof(double) + remainsTotals + remainsSections,
                StateOf(on).Length);

            World back = Restored(off.Config, 11, StateOf(off));
            Assert.Equal(0d, back.BiteWasteJoules);
            Assert.Equal(StateOf(off), StateOf(back));
        }

        // ------------------------------------------------------------------------ D220: the remains

        /// <summary>
        /// A tree of five photosynthetic parts: 0 the root, 1 a part of its own, 2 and 3 a chain that hangs from 1, and 4
        /// a second branch on 1. Eating 1 whole cuts off two limbs, {2, 3} and {4}.
        /// </summary>
        internal static Genome Branching()
        {
            var genome = new Genome();
            MorphNode root = Fixtures.Box(0.2f);
            MorphNode a = Fixtures.Box(0.25f);
            MorphNode b = Fixtures.Box(0.2f);
            MorphNode d = Fixtures.Box(0.15f);
            MorphNode c = Fixtures.Box(0.3f);

            foreach (MorphNode node in new[] { root, a, b, d, c }) node.CellTypeId = CellTypeIds.Photosynthetic;

            root.Edges.Add(Fixtures.FaceToFace(1));
            a.Edges.Add(Fixtures.FaceToFace(2));
            a.Edges.Add(new MorphEdge
            {
                Child = 4,
                ParentAnchor = new Float3(0f, 1f, 0f),
                ChildAnchor = new Float3(0f, -1f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });
            b.Edges.Add(Fixtures.FaceToFace(3));

            genome.Nodes.Add(root);
            genome.Nodes.Add(a);
            genome.Nodes.Add(b);
            genome.Nodes.Add(d);
            genome.Nodes.Add(c);
            genome.RootIndex = 0;
            genome.AdultScale = 1f;
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            return genome;
        }

        internal static RunConfig RemainsStage(float window = 60f, float healing = 0f)
        {
            RunConfig config = Stage(on: true, healing: healing);
            config.RemainsWindowSeconds = window;
            return config;
        }

        /// <summary>The bodies a limb test needs, and the remains the cut leaves.</summary>
        private sealed class Cut
        {
            public World World;
            public Organism Tree, Hunter, Claw, Small;
            public Organism First, Second;
            public double[] Tissue, Volume;
            public double ReserveBefore;
            public double TissueBefore;
        }

        /// <summary>
        /// A branching tree, a hunter whose mouth carries what it eats, a claw with no mouth and a hunter with a
        /// mouth too small for a mouthful; then the first hunter eats the tree's part 1 whole in one pass.
        /// </summary>
        private static Cut CutOffLimbs(float window = 60f, float healing = 0f)
        {
            RunConfig config = RemainsStage(window, healing);
            var world = new World(config, seed: 3);
            world.Inoculate(Branching(), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            world.Inoculate(Claw(), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake * 0.001f), 1, -1f);

            var cut = new Cut
            {
                World = world,
                Tree = world.Living[0],
                Hunter = world.Living[1],
                Claw = world.Living[2],
                Small = world.Living[3],
            };

            Assert.Equal(5, cut.Tree.Phenotype.PartCount);
            cut.Tissue = new double[5];
            cut.Volume = new double[5];
            for (int p = 0; p < 5; p++)
            {
                cut.Tissue[p] = TissueOf(cut.Tree, p, config);
                cut.Volume[p] = cut.Tree.Phenotype.Parts[p].Volume;
            }
            cut.ReserveBefore = cut.Tree.Energy;
            cut.TissueBefore = cut.Tree.TissueJoules;

            world.SetContacts(Touching(cut.Hunter, 0, cut.Tree, 1));
            world.ApplyMouth(100f);

            Assert.Equal(1L, world.PartsKilled);
            Assert.Equal(2, world.Remains.Count);
            cut.First = world.Remains[0];
            cut.Second = world.Remains[1];
            return cut;
        }

        [Fact]
        public void ACutOffLimbFoundsRemainsForEachChildSubtreeWithTheRightPartsHealthTissueAndReserveShare()
        {
            Cut cut = CutOffLimbs();
            World world = cut.World;
            Organism tree = cut.Tree;

            // The two limbs, in ascending id order, new bodies with ids of their own.
            Assert.True(cut.First.Id > cut.Hunter.Id && cut.First.Id > cut.Small.Id, "a new id from the world's counter");
            Assert.True(cut.Second.Id > cut.First.Id);
            Assert.All(world.Remains, r => Assert.True(r.IsRemains));
            Assert.DoesNotContain(world.Living, c => c.IsRemains);
            Assert.Equal(tree.Id, cut.First.ParentId);

            // Parts: {2, 3} and {4}, re-rooted.
            Assert.Equal(2, cut.First.Phenotype.PartCount);
            Assert.Equal(1, cut.Second.Phenotype.PartCount);
            Assert.Equal(-1, cut.First.Phenotype.Parts[0].ParentIndex);
            Assert.Equal(0, cut.First.Phenotype.Parts[1].ParentIndex);
            Assert.Equal(-1, cut.Second.Phenotype.Parts[0].ParentIndex);

            // Health: the old body's, part by part. Part 1 died, and these are the others whole.
            Assert.Equal(new[] { 1f, 1f }, cut.First.PartHealth);
            Assert.Equal(new[] { 1f }, cut.Second.PartHealth);

            // Tissue: each limb's parts at their health.
            AssertRelative(cut.Tissue[2] + cut.Tissue[3], cut.First.TissueJoules, 1e-9, "the first limb's tissue");
            AssertRelative(cut.Tissue[4], cut.Second.TissueJoules, 1e-9, "the second limb's tissue");

            // Reserve: what the living body released, by volume between the two.
            double released = cut.ReserveBefore - tree.Energy;
            Assert.True(released > 0d);
            AssertRelative(released, cut.First.Energy + cut.Second.Energy, 1e-12, "the reserve that left the tree");
            double volumeFirst = cut.Volume[2] + cut.Volume[3];
            double volumeSecond = cut.Volume[4];
            AssertRelative(volumeFirst / volumeSecond, cut.First.Energy / cut.Second.Energy, 1e-9, "the reserve split by volume");

            // And what the living body holds is the root alone.
            AssertRelative(cut.Tissue[0], tree.TissueJoules, 1e-9, "the tree's account");
            Assert.Equal(1, tree.Phenotype.PartCount);

            // The death second of the cut.
            Assert.Equal(world.ElapsedSeconds, cut.First.RemainsSince);
            Assert.Equal(world.ElapsedSeconds, cut.Second.RemainsSince);

            // Nothing was burnt at the cut, no corpse was founded, and the books close.
            Assert.Empty(world.Corpses);
            AssertBooksClose(world, "after the cut");
        }

        [Fact]
        public void TheFoundingListCarriesTheRightIdsAndMapsAndIsDrainedOnce()
        {
            Cut cut = CutOffLimbs();
            World world = cut.World;

            IReadOnlyList<RemainsFounding> foundings = world.DrainRemainsFoundings();
            Assert.Equal(2, foundings.Count);

            Assert.Equal(cut.First.Id, foundings[0].RemainsId);
            Assert.Equal(cut.Tree.Id, foundings[0].FromBodyId);
            Assert.Equal(new[] { 2, 3 }, foundings[0].Map);

            Assert.Equal(cut.Second.Id, foundings[1].RemainsId);
            Assert.Equal(cut.Tree.Id, foundings[1].FromBodyId);
            Assert.Equal(new[] { 4 }, foundings[1].Map);

            // Drained once, as the lineage queue is.
            Assert.Empty(world.DrainRemainsFoundings());

            // A body with nothing hanging from its root, eaten, founds nothing: it burns and no body is left to pose.
            world.SetContacts(Touching(cut.Hunter, 0, cut.Tree, 0));
            world.ApplyMouth(100f);
            Assert.Equal(2, world.Remains.Count);
            Assert.Empty(world.DrainRemainsFoundings());
        }

        [Fact]
        public void ADivergedBodyFoundsNoRemainsAndIsBurntWholeWhateverItHeld()
        {
            RunConfig config = RemainsStage();
            var world = new World(config, seed: 3);
            world.Inoculate(Branching(), 1, -1f);
            Organism tree = world.Living[0];
            double joules = tree.TissueJoules + tree.Energy;
            double burnt = world.EnergyOut;
            double units = world.BurntTotal;

            world.KillDiverged(tree);

            Assert.Empty(world.Remains);
            Assert.Empty(world.Living);
            Assert.False(tree.IsRemains);
            Assert.Empty(world.DrainRemainsFoundings());
            AssertRelative(joules, world.EnergyOut - burnt, 1e-12, "the whole body burnt");
            AssertRelative(joules / config.JoulesPerUnit, world.BurntTotal - units, 1e-12, "the units burnt");
            AssertBooksClose(world, "after the divergence");
        }

        [Fact]
        public void SubtreeAtCutsThePartAndEverythingThatHangsFromItAndRoundTrips()
        {
            var config = new RunConfig();
            Phenotype body = Developer.Develop(Branching(), config.Development, null, config.Shapes, cellTypes: config.CellTypes);
            Assert.Equal(5, body.PartCount);

            // The descendants of every part, worked out from the parent links alone.
            for (int p = 0; p < body.PartCount; p++)
            {
                var expected = new List<int> { p };
                for (int i = p + 1; i < body.PartCount; i++)
                {
                    if (expected.Contains(body.Parts[i].ParentIndex)) expected.Add(i);
                }

                Phenotype sub = body.SubtreeAt(p, out int[] map);
                Assert.Equal(expected, map.ToList());
                Assert.Equal(map.Length, sub.PartCount);

                // Geometry is absolute and moves for nobody; the new root has no parent and no joint to one.
                for (int k = 0; k < map.Length; k++)
                {
                    PhenotypePart was = body.Parts[map[k]];
                    PhenotypePart now = sub.Parts[k];
                    Assert.Equal(was.Position, now.Position);
                    Assert.Equal(was.HalfExtents, now.HalfExtents);
                    Assert.Equal(was.Rotation, now.Rotation);
                    Assert.Equal(was.Volume, now.Volume);
                    Assert.Equal(was.CellTypeId, now.CellTypeId);
                    Assert.Equal(k, now.Index);
                    Assert.Equal(was.Depth - body.Parts[p].Depth, now.Depth);

                    if (k == 0 && p > 0)
                    {
                        Assert.Equal(-1, now.ParentIndex);
                        Assert.Equal(JointType.Fixed, now.JointType);
                        Assert.Equal(0f, now.Power);
                    }
                    else
                    {
                        Assert.Equal(Array.IndexOf(map, was.ParentIndex), now.ParentIndex);
                        Assert.Equal(was.JointType, now.JointType);
                    }
                }

                // The cut at the root is the body.
                if (p == 0)
                {
                    Assert.Equal(body.PartCount, sub.PartCount);
                    Assert.Equal(body.TotalVolume, sub.TotalVolume);
                    Assert.Equal(body.SilhouetteArea, sub.SilhouetteArea);
                }

                // The limb and the body without it partition the body.
                if (p > 0)
                {
                    var drop = new bool[body.PartCount];
                    drop[p] = true;
                    body.WithoutSubtrees(drop, out int[] rest);
                    Assert.Equal(
                        Enumerable.Range(0, body.PartCount).ToList(),
                        rest.Concat(map).OrderBy(i => i).ToList());
                    Assert.Empty(rest.Intersect(map));
                }

                // Selecting the map from the body is the cut, to the bit.
                Phenotype again = body.Select(map);
                Assert.Equal(sub.PartCount, again.PartCount);
                for (int k = 0; k < sub.PartCount; k++)
                {
                    Assert.Equal(sub.Parts[k].ParentIndex, again.Parts[k].ParentIndex);
                    Assert.Equal(sub.Parts[k].Position, again.Parts[k].Position);
                    Assert.Equal(sub.Parts[k].Depth, again.Parts[k].Depth);
                }
                Assert.Equal(sub.SilhouetteArea, again.SilhouetteArea);
                Assert.Equal(sub.TotalLitArea, again.TotalLitArea);
            }

            // A cut of a cut is one cut with the maps composed: what a checkpoint relies on.
            Phenotype limb = body.SubtreeAt(1, out int[] outer);
            for (int q = 0; q < limb.PartCount; q++)
            {
                Phenotype inner = limb.SubtreeAt(q, out int[] innerMap);
                int[] composed = innerMap.Select(k => outer[k]).ToArray();
                Phenotype direct = body.Select(composed);

                Assert.Equal(inner.PartCount, direct.PartCount);
                for (int k = 0; k < inner.PartCount; k++)
                {
                    Assert.Equal(inner.Parts[k].ParentIndex, direct.Parts[k].ParentIndex);
                    Assert.Equal(inner.Parts[k].Position, direct.Parts[k].Position);
                    Assert.Equal(inner.Parts[k].Depth, direct.Parts[k].Depth);
                    Assert.Equal(inner.Parts[k].JointType, direct.Parts[k].JointType);
                    Assert.Equal(inner.Parts[k].Volume, direct.Parts[k].Volume);
                }
                Assert.Equal(inner.SilhouetteArea, direct.SilhouetteArea);
            }

            // What it refuses.
            Assert.Throws<ArgumentOutOfRangeException>(() => body.SubtreeAt(-1, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => body.SubtreeAt(body.PartCount, out _));
            Assert.Throws<ArgumentException>(() => body.Select(new[] { 2, 1 }));
            Assert.Throws<ArgumentException>(() => body.Select(new[] { 1, 3 }));
            Assert.Throws<ArgumentException>(() => body.Select(new int[0]));
        }

        [Fact]
        public void ABiteOnRemainsTakesTissueAndItsShareOfTheStoreAndKeepsOrWastesThemByTheGateAndTheCap()
        {
            // The capacity is generous: the whole offer is within the mouth's cap, so the reserve takes 1 - waste.
            Cut cut = CutOffLimbs();
            World world = cut.World;
            Organism limb = cut.First;
            RunConfig config = world.Config;

            double account = limb.TissueJoules;
            double store = limb.Energy;
            double tissue = TissueOf(limb, 0, config);
            double reserve = cut.Hunter.Energy;
            double keptLeaves = world.BiteKeptFromLeavesJoules, keptOthers = world.BiteKeptFromOthersJoules;
            double keptRemains = world.BiteKeptFromRemainsJoules;

            world.SetContacts(Touching(cut.Hunter, 0, limb, 0));
            world.ApplyMouth(0.3f);

            double s = ShareTaken(limb, 0);
            Assert.True(s > 0d && s < 1d, $"the fixture should wound and not kill: {s}");

            double fromTissue = s * tissue;
            double f = fromTissue / account;
            AssertRelative(account - fromTissue, limb.TissueJoules, 1e-9, "the remains' tissue");
            AssertRelative(store * (1d - f), limb.Energy, 1e-9, "the remains' store, by the same share");

            double offered = fromTissue + store * f;
            double gain = cut.Hunter.Energy - reserve;
            _output.WriteLine($"offered {offered:0.####} J ({fromTissue:0.####} tissue + {store * f:0.####} store): reserve +{gain:0.####}");
            AssertRelative((1d - config.IntakeWasteFraction) * offered, gain, 1e-5, "the reserve takes 1 - waste of tissue and store");
            AssertRelative(gain, world.BiteKeptFromRemainsJoules - keptRemains, 1e-9, "the remains total");
            Assert.Equal(keptLeaves, world.BiteKeptFromLeavesJoules);
            Assert.Equal(keptOthers, world.BiteKeptFromOthersJoules);
            AssertBooksClose(world, "after the bite on remains");

            // The gate: a biter with no mouth keeps nothing, and all of what the wound takes goes to the water.
            double claw = cut.Claw.Energy;
            double water = world.Nutrients.TotalJoules;
            double wasteBefore = world.BiteWasteJoules;
            double accountNow = limb.TissueJoules, storeNow = limb.Energy;
            double tissueNow = TissueOf(limb, 1, config);

            world.SetContacts(Touching(cut.Claw, 0, limb, 1));
            world.ApplyMouth(0.3f);

            double s1 = ShareTaken(limb, 1);
            Assert.True(s1 > 0d);
            double f1 = s1 * tissueNow / accountNow;
            double offered1 = s1 * tissueNow + storeNow * f1;
            Assert.Equal(claw, cut.Claw.Energy);
            AssertRelative(offered1, world.BiteWasteJoules - wasteBefore, 1e-6, "all of it is waste");
            AssertRelative(offered1, world.Nutrients.TotalJoules - water, 1e-6, "and goes to the water");
            AssertRelative(gain, world.BiteKeptFromRemainsJoules - keptRemains, 1e-9, "the remains total did not move");
            AssertBooksClose(world, "after the clawing");

            // The cap: a mouth that cannot carry the offer keeps what it can, and the rest is waste.
            Organism other = cut.Second;
            double smallReserve = cut.Small.Energy;
            PhenotypePart mouth = cut.Small.Phenotype.Parts[0];
            double cap = (double)mouth.Intake * mouth.SurfaceArea * 1d * config.JoulesPerUnit;
            double otherAccount = other.TissueJoules, otherStore = other.Energy;
            double otherTissue = TissueOf(other, 0, config);
            double waste2 = world.BiteWasteJoules;

            world.SetContacts(Touching(cut.Small, 0, other, 0));
            world.ApplyMouth(1f);

            double s2 = ShareTaken(other, 0);
            double offered2 = s2 * otherTissue + otherStore * (s2 * otherTissue / otherAccount);
            Assert.True(offered2 > 4d * cap, $"the fixture should offer more than the mouth can carry: {offered2} against {cap}");
            AssertRelative(cap * (1d - config.IntakeWasteFraction), cut.Small.Energy - smallReserve, 1e-5, "the capped reserve gain");
            AssertRelative(offered2 - cap * (1d - config.IntakeWasteFraction), world.BiteWasteJoules - waste2, 1e-5, "the rest is waste");
            AssertBooksClose(world, "after the capped bite");
        }

        [Fact]
        public void RemainsAreNotMetabolisedGrownHealedOrCountedAlive()
        {
            Cut cut = CutOffLimbs(window: 600f, healing: 0.5f);
            World world = cut.World;
            Organism limb = cut.First;

            // A bite that leaves a part wounded; healing is on and would mend a living body in a step.
            world.SetContacts(Touching(cut.Hunter, 0, limb, 0));
            world.ApplyMouth(0.3f);
            float wounded = limb.PartHealth[0];
            Assert.True(wounded > 0f && wounded < 1f);

            Phenotype body = limb.Phenotype;
            double tissue = limb.TissueJoules, store = limb.Energy, age = limb.Age;
            int alive = world.Living.Count;
            float[] health = (float[])limb.PartHealth.Clone();

            for (int step = 0; step < 40; step++)
            {
                world.Step(0.5f);
                Assert.Same(body, limb.Phenotype);
                Assert.Equal(tissue, limb.TissueJoules);
                Assert.Equal(store, limb.Energy);
                Assert.Equal(age, limb.Age);
                Assert.Equal(health, limb.PartHealth);
                AssertBooksClose(world, $"step {step}");
            }

            Assert.Equal(alive, world.Living.Count);
            Assert.DoesNotContain(world.Living, c => c.IsRemains);
            Assert.Contains(limb, world.Remains);
            Assert.Equal(2, world.RemainsStanding);

            Assert.Equal(0d, limb.PendingWorkJoules);
            Assert.Equal(0f, limb.StandingWatts);
        }

        [Fact]
        public void TheWindowClosesAtRemainsWindowSecondsAndBurnsWhatIsLeft()
        {
            RunConfig config = RemainsStage(window: 10f);
            config.FounderEnergyJoules = 0.001f;
            var world = new World(config, seed: 3);
            world.Inoculate(Fixtures.ArmedBox(), 1, -1f);
            Organism body = world.Living[0];

            for (int step = 0; step < 400 && world.Remains.Count == 0; step++) world.Step(0.5f);
            Assert.Single(world.Remains);

            double since = body.RemainsSince;
            double joules = body.TissueJoules + body.Energy;
            Assert.True(joules > 0d);

            double burnt = world.EnergyOut;
            double units = world.BurntTotal;

            // Whole until the window passes: nothing decays before.
            int guard = 0;
            while (world.RemainsClosed == 0L && guard++ < 100)
            {
                world.Step(0.5f);

                if (world.RemainsClosed == 0L)
                {
                    Assert.True(world.ElapsedSeconds - since < 10d, "closed late");
                    Assert.Single(world.Remains);
                    Assert.Equal(joules, body.TissueJoules + body.Energy);
                }
            }

            Assert.Equal(1L, world.RemainsClosed);
            Assert.Empty(world.Remains);
            Assert.Equal(since + 10d, world.ElapsedSeconds, 9);
            Assert.Equal(0d, body.TissueJoules);
            AssertRelative(joules, world.RemainsClosedJoules, 1e-12, "the joules burnt at the close");
            Assert.True(world.EnergyOut - burnt >= joules - 1e-9, "the joules should be out as heat");
            AssertRelative(joules / config.JoulesPerUnit, world.BurntTotal - units, 1e-9, "the units into the spent field");
            AssertBooksClose(world, "after the close");
        }

        [Fact]
        public void AWindowOfZeroClosesTheStepItOpensAndABodyWithNoPartsLeftClosesAtOnce()
        {
            // Window 0: a death closes in the mouth pass of the step that founded it.
            RunConfig config = RemainsStage(window: 0f);
            config.FounderEnergyJoules = 0.001f;
            var world = new World(config, seed: 3);
            world.Inoculate(Fixtures.ArmedBox(), 1, -1f);
            double tissue = world.Living[0].TissueJoules;
            double burnt = world.EnergyOut;

            for (int step = 0; step < 400 && world.RemainsClosed == 0L; step++) world.Step(0.5f);

            Assert.Equal(1L, world.RemainsClosed);
            Assert.Empty(world.Remains);
            AssertRelative(tissue, world.RemainsClosedJoules, 1e-9, "the tissue burnt at the close");
            Assert.True(world.EnergyOut - burnt >= tissue - 1e-9);
            AssertBooksClose(world, "after the close");

            // A remains whose last part is eaten holds no parts, and closes with what it held.
            Cut cut = CutOffLimbs();
            World second = cut.World;
            Organism lone = cut.Second;
            Assert.Equal(1, lone.Phenotype.PartCount);
            long closed = second.RemainsClosed;

            second.SetContacts(Touching(cut.Hunter, 0, lone, 0));
            second.ApplyMouth(100f);

            Assert.DoesNotContain(second.Remains, r => r.Id == lone.Id);
            Assert.Equal(closed + 1L, second.RemainsClosed);
            AssertBooksClose(second, "after the last part was eaten");
        }

        [Fact]
        public void RemainsOfRemainsKeepTheOriginalDeathSecondAndNoBiteRenewsTheWindow()
        {
            Cut cut = CutOffLimbs(window: 20f);
            World world = cut.World;
            double born = cut.First.RemainsSince;
            Assert.Equal(0d, born);

            // Time passes; then the limb {2, 3} loses its root part (2) to the hunter, and 3 hangs from it.
            for (int step = 0; step < 10; step++) world.Step(0.5f);
            Assert.True(world.ElapsedSeconds > born);

            Organism limb = cut.First;
            long limbId = limb.Id;
            double tissueOfThree = TissueOf(limb, 1, world.Config);
            double storeBefore = limb.Energy;
            world.DrainRemainsFoundings();

            world.SetContacts(Touching(cut.Hunter, 0, limb, 0));
            world.ApplyMouth(100f);

            // The root went; its one child subtree became remains of its own, and the limb it was cut from closed.
            Assert.DoesNotContain(world.Remains, r => r.Id == limbId);
            Assert.Equal(2, world.Remains.Count);
            Organism child = world.Remains.Single(r => r.Id != cut.Second.Id);

            Assert.Equal(born, child.RemainsSince);
            Assert.Equal(1, child.Phenotype.PartCount);
            Assert.True(child.Id > cut.Second.Id, "a new id");
            Assert.Equal(limbId, child.ParentId);
            AssertRelative(tissueOfThree, child.TissueJoules, 1e-9, "the child's tissue");
            Assert.True(child.Energy > 0d && child.Energy <= storeBefore, "the child holds the store that was left");

            IReadOnlyList<RemainsFounding> foundings = world.DrainRemainsFoundings();
            Assert.Single(foundings);
            Assert.Equal(child.Id, foundings[0].RemainsId);
            Assert.Equal(limbId, foundings[0].FromBodyId);
            Assert.Equal(new[] { 1 }, foundings[0].Map);
            Assert.Equal(1L, world.RemainsClosed);
            AssertBooksClose(world, "after the shed");

            // The window runs on from the original second: the child closes with the rest, not 20 s after the bite.
            int guard = 0;
            while (world.Remains.Any(r => r.Id == child.Id) && guard++ < 200) world.Step(0.5f);
            Assert.False(world.Remains.Any(r => r.Id == child.Id));
            Assert.Equal(born + 20d, world.ElapsedSeconds, 9);
            AssertBooksClose(world, "after the child closed");
        }

        [Fact]
        public void APartOfRemainsEatenWholeIsShedAndTheRestKeepsItsPlanAndItsWindow()
        {
            Cut cut = CutOffLimbs();
            World world = cut.World;
            Organism limb = cut.First;
            long limbId = limb.Id;

            // Eat the limb's second part (3, the leaf of the chain): the root stays, nothing hangs from the shed part.
            double store = limb.Energy;
            world.DrainRemainsFoundings();
            world.SetContacts(Touching(cut.Hunter, 0, limb, 1));
            world.ApplyMouth(100f);

            Assert.Contains(world.Remains, r => r.Id == limbId);
            Assert.Equal(1, limb.Phenotype.PartCount);
            Assert.Single(limb.PartHealth);
            Assert.Equal(1f, limb.PartHealth[0]);
            Assert.Equal(0d, limb.RemainsSince);
            Assert.Empty(world.DrainRemainsFoundings());
            Assert.NotNull(limb.PartMapFromPreviousPlan);
            Assert.Equal(new[] { 0 }, limb.PartMapFromPreviousPlan);

            // The shed part had no children: its share of the store by volume was burnt with it.
            Assert.True(limb.Energy < store && limb.Energy > 0d);
            AssertBooksClose(world, "after the shed");
        }

        [Fact]
        public void AShadowFallsWithHealthAndRemainsShadeTheLivingUntilTheyClose()
        {
            Cut cut = CutOffLimbs(window: 600f);
            World world = cut.World;
            Organism limb = cut.First;
            RunConfig config = world.Config;

            // The part of the sum: a whole body casts its effective lit area, a bitten one each part's at its health.
            float whole = World.ShadowAtHealth(limb, false, false);
            float expectedWhole = 0f;
            foreach (PhenotypePart part in limb.Phenotype.Parts) expectedWhole += part.LitArea;
            AssertRelative(expectedWhole, whole, 1e-6, "a whole remains casts its lit area");

            world.SetContacts(Touching(cut.Hunter, 0, limb, 0));
            world.ApplyMouth(0.3f);
            float h = limb.PartHealth[0];
            Assert.True(h > 0f && h < 1f);

            float bitten = World.ShadowAtHealth(limb, false, false);
            double expected = 0d;
            foreach (PhenotypePart part in limb.Phenotype.Parts) expected += part.LitArea * (double)limb.PartHealth[part.Index];
            Assert.True(bitten < whole, "a bitten remains casts less");
            AssertRelative(expected, bitten, 1e-6, "each part's lit area at its health");

            // In the water: the factor the light reaches the depth below with falls as the remains' shadow does.
            float depth = -6f;

            // Two worlds in the same state but for the remains' health, read after one more step each.
            Cut other = CutOffLimbs(window: 600f);
            other.World.Step(0.5f);
            float shaded = Factor0(other.World, depth);

            world.Step(0.5f);
            float lighter = Factor0(world, depth);
            _output.WriteLine($"light reaching {depth} m: {shaded:0.######} with whole remains, {lighter:0.######} with a bitten one");
            Assert.True(lighter > shaded, "the bitten remains' shadow should be lighter");

            // A closed body casts none: the same world with the remains gone lets more through.
            world.Config.RemainsWindowSeconds = 0f;
            world.Step(0.5f);
            world.Step(0.5f);
            Assert.Empty(world.Remains);
            Assert.True(Factor0(world, depth) > lighter, "with the remains closed the light should reach further");
        }

        private static float Factor0(World world, float depth) =>
            world.Field.IrradianceAt(depth) / (world.Field.Model.IrradianceAt(depth) * world.Field.DayFactor);

        [Fact]
        public void ADroppedModuleBecomesRemainsOfItsOwnWithItsShareOfTheReserveAndItsPartsMap()
        {
            RunConfig config = RemainsStage();
            config.ModuleAddReserveSeconds = 20f;
            config.ModuleDropReserveSeconds = 0f;
            config.ModuleDropAfterSeconds = 0f;

            var world = new World(config, seed: 3);
            world.Inoculate(Fixtures.IndeterminateLeaf(3, 0.2f), 1, -1f);
            Organism leaf = world.Living[0];

            for (int round = 0; round < 60 && leaf.Phenotype.PartCount < 3; round++)
            {
                for (int s = 0; s < 30; s++) world.Step(1f);
                world.ApplyModuleRule(30f);
            }

            Assert.Equal(3, leaf.Phenotype.PartCount);
            world.DrainRemainsFoundings();

            double tissueOfTheModule = TissueOf(leaf, 2, config);
            double reserve = leaf.Energy;
            double burnt = world.EnergyOut;

            // The drought: addition off, and the drop's line above anything the body holds.
            config.ModuleAddReserveSeconds = 0f;
            config.ModuleDropReserveSeconds = 1e9f;
            world.ApplyModuleRule(10f);

            Assert.Equal(1L, world.ModuleDrops);
            Assert.Equal(2, leaf.Phenotype.PartCount);
            Assert.Empty(world.Corpses);
            Assert.Equal(burnt, world.EnergyOut);

            Assert.Single(world.Remains);
            Organism module = world.Remains[0];
            Assert.Equal(leaf.Id, module.ParentId);
            Assert.Equal(1, module.Phenotype.PartCount);
            AssertRelative(tissueOfTheModule, module.TissueJoules, 1e-9, "the module's tissue");
            AssertRelative(reserve - leaf.Energy, module.Energy, 1e-9, "the module's share of the reserve");
            Assert.True(module.Energy > 0d);

            IReadOnlyList<RemainsFounding> foundings = world.DrainRemainsFoundings();
            Assert.Single(foundings);
            Assert.Equal(module.Id, foundings[0].RemainsId);
            Assert.Equal(leaf.Id, foundings[0].FromBodyId);
            Assert.Equal(new[] { 2 }, foundings[0].Map);
            AssertBooksClose(world, "after the drop");
        }

        // ------------------------------------------------------------------------ D220: the books and the checkpoint with remains

        /// <summary>
        /// A world of trees and hunters in which things die, are cut, and are bitten after they are dead: every
        /// contact picks its prey among the living and the remains together.
        /// </summary>
        private static World RemainsWorld(int seedOffset = 0)
        {
            RunConfig config = RemainsStage(window: 25f, healing: 0.04f);
            config.SenescenceDoublingSeconds = 0f;

            var world = new World(config, seed: (ulong)(21 + seedOffset));
            world.Inoculate(Branching(), 2, -1f);
            world.Inoculate(Leaf(3, 0.18f), 2, -1f);
            world.Inoculate(Fixtures.ArmedBox(half: 0.3f), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake * 0.3f), 2, -1f);
            world.Inoculate(Claw(), 1, -1f);
            return world;
        }

        private static List<CreatureContact> RemainsBites(World world, int step)
        {
            var biters = world.Living.Where(c => c.HasAttack).ToList();
            var prey = new List<Organism>();
            prey.AddRange(world.Living.Where(c => !c.HasAttack));
            prey.AddRange(world.Remains);

            var contacts = new List<CreatureContact>();
            if (prey.Count == 0) return contacts;

            for (int i = 0; i < biters.Count; i++)
            {
                Organism victim = prey[(step / 25 + i) % prey.Count];
                int part = (step / 25 + i) % victim.Phenotype.PartCount;
                contacts.Add(new CreatureContact(biters[i].Id, 0, victim.Id, part));
            }

            // And the dead bite back, as a claw would: a remains part that carries attack wounds the biter it touches.
            foreach (Organism dead in world.Remains)
            {
                if (!dead.HasAttack || biters.Count == 0) continue;
                contacts.Add(new CreatureContact(dead.Id, 0, biters[step % biters.Count].Id, 0));
            }

            return contacts;
        }

        private static void RunRemainsBites(World world, int steps, Action<int> after = null)
        {
            for (int step = 0; step < steps; step++)
            {
                world.SetContacts(RemainsBites(world, step));
                world.Step(0.5f);
                after?.Invoke(step);
            }
        }

        [Fact]
        public void BothResidualsStayWithinRoundingOverHundredsOfStepsWithDeathsCutsBitesOnRemainsAndCloses()
        {
            World world = RemainsWorld();
            int foundings = 0, mostStanding = 0;

            RunRemainsBites(world, 700, step =>
            {
                AssertBooksClose(world, $"step {step}");
                foundings += world.DrainRemainsFoundings().Count;
                mostStanding = Math.Max(mostStanding, world.RemainsStanding);
            });

            _output.WriteLine(
                $"alive {world.Living.Count}, remains {world.RemainsStanding} (most {mostStanding}), closed {world.RemainsClosed}, " +
                $"foundings {foundings}, deaths {world.Deaths}, parts killed {world.PartsKilled}, bodies eaten {world.BodiesEaten}; " +
                $"kept {world.BiteKeptFromLeavesJoules:0.###} + {world.BiteKeptFromOthersJoules:0.###} + {world.BiteKeptFromRemainsJoules:0.###} J " +
                $"(remains), waste {world.BiteWasteJoules:0.###} J; closed {world.RemainsClosedJoules:0.###} J; " +
                $"audit {world.AuditResidual:R}, matter {world.MatterResidual:R}");

            Assert.True(world.PartsKilled + world.BodiesEaten > 0L, "nothing was eaten whole");
            Assert.True(foundings > 0, "no limb was cut off");
            Assert.True(mostStanding > 0, "no remains stood");
            Assert.True(world.BiteKeptFromRemainsJoules > 0d, "no bite on remains was kept");
            Assert.True(world.RemainsClosed > 0L, "no remains closed");
            Assert.Empty(world.Corpses);
        }

        [Fact]
        public void ADialOnCheckpointWithRemainsInItRoundTripsAndTheResumeEqualsTheUnbrokenRun()
        {
            World world = RemainsWorld();

            // Until there are limbs of limbs, and a founding nobody has drained.
            int step = 0;
            for (; step < 700; step++)
            {
                world.SetContacts(RemainsBites(world, step));
                world.Step(0.5f);
                if (world.Remains.Any(r => r.ParentId >= 0) && world.RemainsStanding >= 2) break;
            }

            Assert.True(world.RemainsStanding >= 2, "the fixture never stood two remains");
            Assert.Contains(world.Remains, r => r.PartHealth != null);
            Assert.Contains(world.Remains, r => r.Phenotype.PartCount > 0);

            byte[] state = StateOf(world);
            World restored = Restored(world.Config, 21, state);

            Assert.Equal(world.RemainsStanding, restored.RemainsStanding);
            for (int i = 0; i < world.Remains.Count; i++)
            {
                Organism a = world.Remains[i], b = restored.Remains[i];
                Assert.Equal(a.Id, b.Id);
                Assert.True(b.IsRemains);
                Assert.Equal(a.RemainsSince, b.RemainsSince);
                Assert.Equal(a.TissueJoules, b.TissueJoules);
                Assert.Equal(a.Energy, b.Energy);
                Assert.Equal(a.PartHealth, b.PartHealth);
                Assert.Equal(a.Phenotype.PartCount, b.Phenotype.PartCount);
                for (int k = 0; k < a.Phenotype.PartCount; k++)
                {
                    Assert.Equal(a.Phenotype.Parts[k].Position, b.Phenotype.Parts[k].Position);
                    Assert.Equal(a.Phenotype.Parts[k].ParentIndex, b.Phenotype.Parts[k].ParentIndex);
                    Assert.Equal(a.Phenotype.Parts[k].Volume, b.Phenotype.Parts[k].Volume);
                }
            }

            Assert.Equal(world.BiteKeptFromRemainsJoules, restored.BiteKeptFromRemainsJoules);
            Assert.Equal(world.RemainsClosed, restored.RemainsClosed);
            Assert.Equal(world.RemainsClosedJoules, restored.RemainsClosedJoules);
            Assert.Equal(world.RemainsStandingJoules, restored.RemainsStandingJoules);
            Assert.Equal(StateOf(world), StateOf(restored));

            // The undrained foundings travel in the checkpoint.
            IReadOnlyList<RemainsFounding> pending = restored.DrainRemainsFoundings();
            IReadOnlyList<RemainsFounding> original = world.DrainRemainsFoundings();
            Assert.Equal(original.Count, pending.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Assert.Equal(original[i].RemainsId, pending[i].RemainsId);
                Assert.Equal(original[i].FromBodyId, pending[i].FromBodyId);
                Assert.Equal(original[i].Map, pending[i].Map);
            }

            // And the resume goes where the unbroken run goes, through more cuts, bites on remains and closes.
            for (int more = 0; more < 200; more++)
            {
                world.SetContacts(RemainsBites(world, step + more));
                restored.SetContacts(RemainsBites(restored, step + more));
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(world.RemainsClosed, restored.RemainsClosed);
            Assert.Equal(world.BiteKeptFromRemainsJoules, restored.BiteKeptFromRemainsJoules);
            Assert.Equal(StateOf(world), StateOf(restored));
            _output.WriteLine($"resumed at step {step}: remains {world.RemainsStanding}, closed {world.RemainsClosed}");
        }

        // ------------------------------------------------------------------------ D224: a living bite takes its share of the store

        private static RunConfig StoreStage(float waste = 0.25f)
        {
            RunConfig config = Stage(on: true, waste: waste);
            config.BiteTakesStore = true;
            return config;
        }

        /// <summary>Sets a body's reserve or gestation account, which only the world sets in play: a fixture's hand.</summary>
        internal static void Poke(Organism body, string property, double value) =>
            typeof(Organism).GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(body, value);

        [Fact]
        public void TheStoreDialIsOffByDefaultAndIsRefusedOnWithContactEatingOff()
        {
            Assert.False(new RunConfig().BiteTakesStore);

            RunConfig refused = Stage(on: false);
            refused.BiteTakesStore = true;
            Assert.Throws<ArgumentException>(() => new World(refused, seed: 3));

            RunConfig allowed = StoreStage();
            Assert.True(new World(allowed, seed: 3).Config.BiteTakesStore);
        }

        [Fact]
        public void ABiteOnALivingBodyTakesTheTissuesShareOfTheReserveAndTheGestationAccountAndTheBiterKeepsThemByTheGateAndTheCap()
        {
            RunConfig config = StoreStage();
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            world.Inoculate(Claw(), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];
            Organism claw = world.Living[2];

            Poke(leaf, "Energy", 200d);
            Poke(leaf, "GestationJoules", 80d);

            double account = leaf.TissueJoules;
            double tissue = TissueOf(leaf, 1, config);
            double reserve = hunter.Energy;
            double water = world.Nutrients.TotalJoules;

            world.SetContacts(Touching(hunter, 0, leaf, 1));
            world.ApplyMouth(1f);

            double s = ShareTaken(leaf, 1);
            Assert.True(s > 0d && s < 1d, $"the fixture should wound and not kill: {s}");

            double fromTissue = s * tissue;
            double f = fromTissue / account;
            AssertRelative(account - fromTissue, leaf.TissueJoules, 1e-9, "the tissue account");
            AssertRelative(200d * (1d - f), leaf.Energy, 1e-9, "the reserve, by the tissue's share");
            AssertRelative(80d * (1d - f), leaf.GestationJoules, 1e-9, "the gestation account, by the tissue's share");
            AssertRelative(f * 280d, world.BiteStoreTakenJoules, 1e-9, "the store total");

            double offered = fromTissue + f * 280d;
            double mouthCap = (double)hunter.Phenotype.Parts[0].Intake * hunter.Phenotype.Parts[0].SurfaceArea * config.JoulesPerUnit;
            Assert.True(offered < mouthCap, $"the fixture should offer less than the mouth can carry: {offered} against {mouthCap}");

            double gain = hunter.Energy - reserve;
            _output.WriteLine($"offered {offered:0.####} J ({fromTissue:0.####} tissue + {f * 280d:0.####} store): reserve +{gain:0.####}");
            AssertRelative((1d - config.IntakeWasteFraction) * offered, gain, 1e-5, "the reserve takes 1 - waste of tissue and store");
            AssertRelative(offered - gain, world.Nutrients.TotalJoules - water, 1e-5, "the rest goes to the water");
            AssertRelative(gain, world.BiteKeptFromLeavesJoules, 1e-9, "the kept total");

            // The gate: a claw with no mouth keeps nothing, and all of what its wound takes, store included, is waste.
            double clawReserve = claw.Energy;
            double waste = world.BiteWasteJoules;
            double accountNow = leaf.TissueJoules, reserveNow = leaf.Energy, gestationNow = leaf.GestationJoules;
            double tissueNow = TissueOf(leaf, 0, config);
            double takenBefore = world.BiteStoreTakenJoules;

            world.SetContacts(Touching(claw, 0, leaf, 0));
            world.ApplyMouth(1f);

            double s1 = ShareTaken(leaf, 0);
            Assert.True(s1 > 0d);
            double f1 = s1 * tissueNow / accountNow;
            Assert.Equal(clawReserve, claw.Energy);
            AssertRelative(f1 * (reserveNow + gestationNow), world.BiteStoreTakenJoules - takenBefore, 1e-9, "the store the claw took");
            AssertRelative(
                s1 * tissueNow + f1 * (reserveNow + gestationNow), world.BiteWasteJoules - waste, 1e-6, "all of it is waste");
        }

        [Fact]
        public void ABiteOnALivingBodyWhoseReserveIsZeroOrBelowTakesNoneOfItAndTheGestationAccountIsClampedAlone()
        {
            RunConfig config = StoreStage();
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(3), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            foreach (double reserve in new[] { 0d, -50d })
            {
                Poke(leaf, "Energy", reserve);
                Poke(leaf, "GestationJoules", 0d);
                double before = world.BiteStoreTakenJoules;

                world.SetContacts(Touching(hunter, 0, leaf, 1));
                world.ApplyMouth(0.5f);

                Assert.Equal(reserve, leaf.Energy);
                Assert.Equal(0d, leaf.GestationJoules);
                Assert.Equal(before, world.BiteStoreTakenJoules);
            }

            // A gestation account is not held back by a reserve in debt: each is clamped by itself.
            Poke(leaf, "Energy", -50d);
            Poke(leaf, "GestationJoules", 120d);
            double account = leaf.TissueJoules;
            double tissue = TissueOf(leaf, 2, config);

            world.SetContacts(Touching(hunter, 0, leaf, 2));
            world.ApplyMouth(0.5f);

            double f = ShareTaken(leaf, 2) * tissue / account;
            Assert.True(f > 0d);
            Assert.Equal(-50d, leaf.Energy);
            AssertRelative(120d * (1d - f), leaf.GestationJoules, 1e-9, "the gestation account");
            AssertRelative(120d * f, world.BiteStoreTakenJoules, 1e-9, "the total");

            // And a bite on a whole body that holds nothing takes tissue alone, as the dial off does.
            Poke(leaf, "Energy", 0d);
            Poke(leaf, "GestationJoules", 0d);
            double taken = world.BiteStoreTakenJoules;
            world.SetContacts(Touching(hunter, 0, leaf, 0));
            world.ApplyMouth(0.5f);
            Assert.Equal(taken, world.BiteStoreTakenJoules);
        }

        [Fact]
        public void ACapThatBindsCutsTheStoreWithTheTissueInProportion()
        {
            RunConfig config = StoreStage();
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2, 0.2f), 1, -1f);
            world.Inoculate(Fixtures.ArmedBox(half: 0.3f), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake * 0.001f), 1, -1f);
            Organism leaf = world.Living[0];
            Organism box = world.Living[1];
            Organism hunter = world.Living[2];

            // The leaf holds a store and the box none, so what the leaf's offer carries beyond its tissue is the store.
            Poke(leaf, "Energy", 300d);
            Poke(leaf, "GestationJoules", 100d);
            Poke(box, "Energy", 0d);

            PhenotypePart mouth = hunter.Phenotype.Parts[0];
            double cap = (double)mouth.Intake * mouth.SurfaceArea * 1d * config.JoulesPerUnit;
            double leafAccount = leaf.TissueJoules;
            double tissueLeaf = TissueOf(leaf, 1, config);
            double tissueBox = TissueOf(box, 0, config);
            double reserve = hunter.Energy;
            double water = world.Nutrients.TotalJoules;

            world.SetContacts(new List<CreatureContact>
            {
                new CreatureContact(hunter.Id, 0, leaf.Id, 1),
                new CreatureContact(hunter.Id, 0, box.Id, 0),
            });
            world.ApplyMouth(1f);

            double leafTissueOffer = ShareTaken(leaf, 1) * tissueLeaf;
            double leafStoreOffer = leafTissueOffer / leafAccount * 400d;
            double offerLeaf = leafTissueOffer + leafStoreOffer;
            double offerBox = ShareTaken(box, 0) * tissueBox;
            double total = offerLeaf + offerBox;
            _output.WriteLine($"offers {offerLeaf:0.####} (of which store {leafStoreOffer:0.####}) and {offerBox:0.####} J against one cap of {cap:0.####} J");
            Assert.True(leafStoreOffer > 0d);
            Assert.True(total > 4d * cap, "the fixture should offer far more than the mouth can carry");

            AssertRelative(leafStoreOffer, world.BiteStoreTakenJoules, 1e-9, "the store taken");

            double keptTotal = cap * (1d - config.IntakeWasteFraction);
            // The reserve takes the difference to the water's float, so it is exact to that float's rounding of the whole offer.
            AssertRelative(keptTotal, hunter.Energy - reserve, 1e-4, "one cap for the part");

            // The cap cut the leaf's offer, tissue and store together, to the same fraction of both.
            AssertRelative(keptTotal * offerLeaf / total, world.BiteKeptFromLeavesJoules, 1e-4, "the leaf's share, store and tissue");
            AssertRelative(keptTotal * offerBox / total, world.BiteKeptFromOthersJoules, 1e-4, "the box's share");
            AssertRelative(total - keptTotal, world.Nutrients.TotalJoules - water, 1e-5, "the rest, store included, went to the water");
        }

        /// <summary>The remains world with the dial on, and a gestating tree among the bodies so an account fills.</summary>
        private static World StoreWorld(float biteScale = 1f)
        {
            RunConfig config = RemainsStage(window: 25f, healing: 0.04f);
            config.SenescenceDoublingSeconds = 0f;
            config.BiteTakesStore = true;
            config.BiteDamageScale = biteScale;

            Genome gestating = Leaf(3, 0.18f);
            gestating.Reproduction = new ReproductionTraits
            {
                BroodSize = 1,
                BirthInvestment = 2f,
                Mode = ReproductionMode.Gestation,
                GestationShare = 0.5f,
            };

            var world = new World(config, seed: 23);
            world.Inoculate(Branching(), 2, -1f);
            world.Inoculate(Leaf(3, 0.18f), 1, -1f);
            world.Inoculate(gestating, 2, -1f);
            world.Inoculate(Fixtures.ArmedBox(half: 0.3f), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake * 0.3f), 2, -1f);
            world.Inoculate(Claw(), 1, -1f);
            return world;
        }

        [Fact]
        public void BothResidualsStayWithinRoundingWithTheStoreTakenOverHundredsOfStepsOfBitesDeathsRemainsAndCloses()
        {
            World world = StoreWorld();
            double mostGestation = 0d;

            RunRemainsBites(world, 700, step =>
            {
                AssertBooksClose(world, $"step {step}");
                world.DrainRemainsFoundings();
                mostGestation = Math.Max(mostGestation, world.GestationJoulesInBodies);
            });

            _output.WriteLine(
                $"alive {world.Living.Count}, remains {world.RemainsStanding}, closed {world.RemainsClosed}, deaths {world.Deaths}; " +
                $"store taken {world.BiteStoreTakenJoules:0.###} J, kept {world.BiteKeptFromLeavesJoules:0.###} + {world.BiteKeptFromOthersJoules:0.###} + " +
                $"{world.BiteKeptFromRemainsJoules:0.###} J, waste {world.BiteWasteJoules:0.###} J, most gestation {mostGestation:0.###} J; " +
                $"audit {world.AuditResidual:R}, matter {world.MatterResidual:R}");

            Assert.True(world.BiteStoreTakenJoules > 0d, "no bite took a store");
            Assert.True(mostGestation > 0d, "no gestation account ever held anything");
            Assert.True(world.BiteKeptFromRemainsJoules > 0d, "no bite on remains was kept");
            Assert.True(world.RemainsClosed > 0L, "no remains closed");
            Assert.True(world.PartsKilled + world.BodiesEaten > 0L, "nothing was eaten whole");
        }

        [Fact]
        public void ADialOnCheckpointWithTheStoreTakenRoundTripsAndTheResumeEqualsTheUnbrokenRun()
        {
            World world = StoreWorld();
            RunRemainsBites(world, 150);
            Assert.True(world.BiteStoreTakenJoules > 0d, "the fixture took no store before the checkpoint");

            byte[] state = StateOf(world);
            World restored = Restored(world.Config, 23, state);

            Assert.Equal(world.BiteStoreTakenJoules, restored.BiteStoreTakenJoules);
            Assert.Equal(world.GestationJoulesInBodies, restored.GestationJoulesInBodies);
            Assert.Equal(StateOf(world), StateOf(restored));

            for (int more = 0; more < 200; more++)
            {
                world.SetContacts(RemainsBites(world, 150 + more));
                restored.SetContacts(RemainsBites(restored, 150 + more));
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(world.BiteStoreTakenJoules, restored.BiteStoreTakenJoules);
            Assert.Equal(world.BiteWasteJoules, restored.BiteWasteJoules);
            Assert.Equal(StateOf(world), StateOf(restored));
        }

        [Fact]
        public void TheStoreDialAddsOneDoubleToTheCheckpointOnlyWhenOnAndTheDialOffLayoutIsUnchanged()
        {
            World contact = BitingWorld(on: true);

            RunConfig config = Stage(on: true, healing: 0.04f);
            config.SenescenceDoublingSeconds = 0f;
            config.BiteTakesStore = true;
            var store = new World(config, seed: 11);
            for (int i = 0; i < 3; i++) store.Inoculate(Leaf(3, 0.15f + 0.03f * i), 1, -1f);
            store.Inoculate(Fixtures.ArmedBox(half: 0.3f), 2, -1f);
            store.Inoculate(Hunter(ConsumerIntake * 0.3f), 2, -1f);
            store.Inoculate(Claw(), 1, -1f);

            Assert.Equal(StateOf(contact).Length + sizeof(double), StateOf(store).Length);

            World back = Restored(contact.Config, 11, StateOf(contact));
            Assert.Equal(0d, back.BiteStoreTakenJoules);
            Assert.Equal(StateOf(contact), StateOf(back));
        }

        // ------------------------------------------------------------------------ D226: a bite's wound, scaled

        /// <summary>A bite stage with the dial of D226 set.</summary>
        private static RunConfig ScaledStage(float scale, bool store = false)
        {
            RunConfig config = Stage(on: true);
            config.BiteDamageScale = scale;
            config.BiteTakesStore = store;
            return config;
        }

        [Fact]
        public void TheBiteDamageDialIsOneByDefaultAndIsRefusedAtZeroBelowZeroNotFiniteAndNotOneWithContactEatingOff()
        {
            Assert.Equal(1f, new RunConfig().BiteDamageScale);
            Assert.Equal(4f, new World(ScaledStage(4f), seed: 3).Config.BiteDamageScale);

            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                RunConfig refused = ScaledStage(bad);
                Assert.Throws<ArgumentException>(() => new World(refused, seed: 3));
            }

            RunConfig off = Stage(on: false);
            off.BiteDamageScale = 4f;
            Assert.Throws<ArgumentException>(() => new World(off, seed: 3));

            RunConfig offAtOne = Stage(on: false);
            Assert.Equal(1f, new World(offAtOne, seed: 3).Config.BiteDamageScale);
        }

        /// <summary>A hunter bites a leaf's part 1 once for the given seconds under a given dial.</summary>
        private static (World World, Organism Leaf, Organism Hunter, double TissueBefore, double AccountBefore) OneBite(
            float scale, bool store = false, float seconds = 0.1f)
        {
            RunConfig config = ScaledStage(scale, store);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];

            if (store)
            {
                Poke(leaf, "Energy", 200d);
                Poke(leaf, "GestationJoules", 80d);
            }

            double tissue = TissueOf(leaf, 1, config);
            double account = leaf.TissueJoules;
            world.SetContacts(Touching(hunter, 0, leaf, 1));
            world.ApplyMouth(seconds);
            return (world, leaf, hunter, tissue, account);
        }

        [Fact]
        public void ADialAtFourMakesAMouthsBlowFourTimesTheShareAndTheOfferFourTimesTheTissue()
        {
            var one = OneBite(1f);
            var four = OneBite(4f);

            double s1 = ShareTaken(one.Leaf, 1), s4 = ShareTaken(four.Leaf, 1);
            Assert.True(s1 > 0d && s4 < 1d, $"the fixture should wound at 1 and not kill at 4: {s1}, {s4}");
            AssertRelative(4d * s1, s4, 1e-5, "the share of the part's health");

            // The offer is the tissue the wound takes out of the account, and what the biter kept and wasted adds back to it.
            double offer1 = one.AccountBefore - one.Leaf.TissueJoules;
            double offer4 = four.AccountBefore - four.Leaf.TissueJoules;
            AssertRelative(s4 * four.TissueBefore, offer4, 1e-9, "the tissue the wound took at 4");
            AssertRelative(4d * offer1, offer4, 1e-5, "the tissue offered, at 4 against at 1");

            double settled1 = one.World.BiteKeptFromLeavesJoules + one.World.BiteWasteJoules;
            double settled4 = four.World.BiteKeptFromLeavesJoules + four.World.BiteWasteJoules;
            AssertRelative(offer4, settled4, 1e-5, "kept and wasted add up to the offer at 4");
            AssertRelative(4d * settled1, settled4, 1e-5, "the offer settled, at 4 against at 1");
            AssertBooksClose(four.World, "after the bite at 4");

            _output.WriteLine($"share {s1:0.#####} -> {s4:0.#####}; offered {offer1:0.####} -> {offer4:0.####} J");
        }

        [Fact]
        public void ADialAtFourCutsTheOfferAtWhatThePartHeldWhenTheBlowIsDeeperThanThePart()
        {
            // A long enough blow at 4 kills a part that the same blow at 1 leaves standing: the offer is cut at what the part held.
            var one = OneBite(1f, seconds: 1.5f);
            var four = OneBite(4f, seconds: 1.5f);

            double s1 = ShareTaken(one.Leaf, 1);
            _output.WriteLine($"share at 1 over 1.5 s: {s1:0.#####}");
            Assert.True(s1 > 0.25d && s1 < 1d, $"the fixture should leave the part standing at 1: {s1}");
            Assert.Equal(0L, one.World.PartsKilled);
            Assert.Equal(1L, four.World.PartsKilled);

            // Eaten whole, the part gives its tissue and no more; what the biter kept and the water got is that tissue.
            double settled4 = four.World.BiteKeptFromLeavesJoules + four.World.BiteWasteJoules;
            AssertRelative(four.TissueBefore, settled4, 1e-5, "the whole part and no more");
            AssertBooksClose(four.World, "after the deep bite");
        }

        [Fact]
        public void ADialAtFourLeavesAClawsBlowExactlyAsItWasAtOne()
        {
            (World World, Organism Leaf, Organism Claw) Bite(float scale)
            {
                var world = new World(ScaledStage(scale), seed: 3);
                world.Inoculate(Leaf(2), 1, -1f);
                world.Inoculate(Claw(), 1, -1f);
                world.SetContacts(Touching(world.Living[1], 0, world.Living[0], 1));
                world.ApplyMouth(0.1f);
                return (world, world.Living[0], world.Living[1]);
            }

            var one = Bite(1f);
            var four = Bite(4f);

            Assert.True(ShareTaken(one.Leaf, 1) > 0d, "the claw should wound");
            Assert.Equal(one.Leaf.PartHealth[1], four.Leaf.PartHealth[1]);
            Assert.Equal(one.Leaf.TissueJoules, four.Leaf.TissueJoules);
            Assert.Equal(one.World.BiteWasteJoules, four.World.BiteWasteJoules);
            Assert.Equal(one.Leaf.PartDamage[1], four.Leaf.PartDamage[1]);
        }

        /// <summary>
        /// A hunter that has died of age and so lies as remains with its mouth and claw still on the part, and a leaf
        /// still living. Null when no leaf outlived it under this seed.
        /// </summary>
        private static (World World, Organism Remains, Organism Leaf)? DeadHunterAndLeaf(float scale, ulong seed)
        {
            RunConfig config = ScaledStage(scale);
            config.AgeHazardBaseSeconds = 2f;
            config.SenescenceDoublingSeconds = 0f;
            config.SenseContact = true;

            var world = new World(config, seed);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            world.Inoculate(Leaf(2), 8, -1f);
            Organism hunter = world.Living[0];

            for (int step = 0; step < 400 && !hunter.IsRemains; step++) world.Step(0.5f);
            if (!hunter.IsRemains || world.Living.Count == 0) return null;

            return (world, hunter, world.Living[0]);
        }

        [Fact]
        public void ADialAtFourLeavesABlowFromRemainsExactlyAsItWasAtOne()
        {
            ulong seed = 0;
            for (ulong candidate = 3; candidate < 80 && seed == 0; candidate++)
            {
                if (DeadHunterAndLeaf(1f, candidate) != null) seed = candidate;
            }

            Assert.True(seed != 0, "no seed left a leaf living beside the dead hunter");

            (World World, Organism Remains, Organism Leaf) Bite(float scale)
            {
                var found = DeadHunterAndLeaf(scale, seed).Value;
                Assert.True(found.Remains.Phenotype.Parts[0].Intake > 0f && found.Remains.Phenotype.Parts[0].Attack > 0f);
                found.World.SetContacts(Touching(found.Remains, 0, found.Leaf, 1));
                found.World.ApplyMouth(0.1f);
                return found;
            }

            var one = Bite(1f);
            var four = Bite(4f);

            Assert.True(ShareTaken(one.Leaf, 1) > 0d, "the remains' part should wound");
            Assert.Equal(one.Leaf.PartHealth[1], four.Leaf.PartHealth[1]);
            Assert.Equal(one.Leaf.TissueJoules, four.Leaf.TissueJoules);
            Assert.Equal(one.World.BiteWasteJoules, four.World.BiteWasteJoules);
        }

        [Fact]
        public void ADialAtFourWithTheStoreShareOnTakesTheStoreByTheDeeperTissueShare()
        {
            var one = OneBite(1f, store: true);
            var four = OneBite(4f, store: true);

            double s4 = ShareTaken(four.Leaf, 1);
            Assert.True(s4 > 0d && s4 < 1d, $"the fixture should wound and not kill: {s4}");

            double fromTissue = s4 * four.TissueBefore;
            double f = fromTissue / four.AccountBefore;
            AssertRelative(f * 280d, four.World.BiteStoreTakenJoules, 1e-9, "the store taken at 4, by the deeper share");
            AssertRelative(200d * (1d - f), four.Leaf.Energy, 1e-9, "the reserve left");
            AssertRelative(80d * (1d - f), four.Leaf.GestationJoules, 1e-9, "the gestation account left");
            AssertRelative(4d * one.World.BiteStoreTakenJoules, four.World.BiteStoreTakenJoules, 1e-5, "four times the store taken at 1");

            double settled = four.World.BiteKeptFromLeavesJoules + four.World.BiteWasteJoules;
            AssertRelative(fromTissue + f * 280d, settled, 1e-5, "tissue and store are what was offered");
            // The books are not read here: the fixture's hand-set reserve and gestation account are outside them, as in D224's own test.
        }

        [Fact]
        public void BothResidualsStayWithinRoundingWithTheDialAtSixteenOverHundredsOfStepsOfBitesDeathsRemainsAndCloses()
        {
            World world = StoreWorld(16f);

            RunRemainsBites(world, 700, step =>
            {
                AssertBooksClose(world, $"step {step}");
                world.DrainRemainsFoundings();
            });

            _output.WriteLine(
                $"alive {world.Living.Count}, remains {world.RemainsStanding}, closed {world.RemainsClosed}, deaths {world.Deaths}, " +
                $"parts killed {world.PartsKilled}, bodies eaten {world.BodiesEaten}; store taken {world.BiteStoreTakenJoules:0.###} J, " +
                $"kept {world.BiteKeptFromLeavesJoules:0.###} + {world.BiteKeptFromOthersJoules:0.###} + {world.BiteKeptFromRemainsJoules:0.###} J, " +
                $"waste {world.BiteWasteJoules:0.###} J; audit {world.AuditResidual:R}, matter {world.MatterResidual:R}");

            Assert.True(world.BiteStoreTakenJoules > 0d, "no bite took a store");
            Assert.True(world.BiteKeptFromRemainsJoules > 0d, "no bite on remains was kept");
            Assert.True(world.RemainsClosed > 0L, "no remains closed");
            Assert.True(world.PartsKilled + world.BodiesEaten > 0L, "nothing was eaten whole");
        }

        [Fact]
        public void ADialAtFourCheckpointRoundTripResumesEqualToTheUnbrokenRun()
        {
            World world = StoreWorld(4f);
            RunRemainsBites(world, 150);
            Assert.True(world.BiteStoreTakenJoules > 0d, "the fixture took no store before the checkpoint");

            byte[] state = StateOf(world);
            World restored = Restored(world.Config, 23, state);
            Assert.Equal(4f, restored.Config.BiteDamageScale);
            Assert.Equal(StateOf(world), StateOf(restored));

            for (int more = 0; more < 200; more++)
            {
                world.SetContacts(RemainsBites(world, 150 + more));
                restored.SetContacts(RemainsBites(restored, 150 + more));
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(world.BiteStoreTakenJoules, restored.BiteStoreTakenJoules);
            Assert.Equal(world.BiteWasteJoules, restored.BiteWasteJoules);
            Assert.Equal(StateOf(world), StateOf(restored));
        }

        [Fact]
        public void TheDialAddsTwoLongsToTheCheckpointOnlyWhenItIsNotOne()
        {
            // D227: the dial itself is configuration, but a world off 1 also writes its two mouth-blow totals.
            Assert.Equal(StateOf(StoreWorld(1f)).Length + 2 * sizeof(long), StateOf(StoreWorld(4f)).Length);
        }

        // ------------------------------------------------------------------------ D227: the mouth's blows that finish a part

        [Fact]
        public void AMouthsBlowThatFinishesAPartCountsOnceInBothTotalsAndOneThatLeavesHealthCountsInTheFirstOnly()
        {
            var shallow = OneBite(4f, seconds: 0.1f);
            Assert.True(shallow.Leaf.PartHealth[1] > 0f, "the fixture should leave the part standing");
            Assert.Equal(1L, shallow.World.MouthBlows);
            Assert.Equal(0L, shallow.World.MouthBlowsFinishing);

            var deep = OneBite(4f, seconds: 1.5f);
            // The killed part is shed within the pass, so the world's own count of it is the witness.
            Assert.Equal(1L, deep.World.PartsKilled);
            Assert.Equal(1L, deep.World.MouthBlows);
            Assert.Equal(1L, deep.World.MouthBlowsFinishing);
        }

        [Fact]
        public void TheCountsAreKeptWhateverTheDialAndAClawsBlowAndABlowFromRemainsCountInNeither()
        {
            // At 1 the mouth's blow is counted too, since the count does not wait for the dial.
            var atOne = OneBite(1f, seconds: 0.1f);
            Assert.Equal(1L, atOne.World.MouthBlows);
            Assert.Equal(0L, atOne.World.MouthBlowsFinishing);

            foreach (float scale in new[] { 1f, 4f })
            {
                var world = new World(ScaledStage(scale), seed: 3);
                world.Inoculate(Leaf(2), 1, -1f);
                world.Inoculate(Claw(), 1, -1f);
                world.SetContacts(Touching(world.Living[1], 0, world.Living[0], 1));
                world.ApplyMouth(1.5f);
                Assert.True(ShareTaken(world.Living[0], 1) > 0d, "the claw should wound");
                Assert.Equal(0L, world.MouthBlows);
                Assert.Equal(0L, world.MouthBlowsFinishing);
            }

            ulong seed = 0;
            for (ulong candidate = 3; candidate < 80 && seed == 0; candidate++)
            {
                if (DeadHunterAndLeaf(4f, candidate) != null) seed = candidate;
            }

            Assert.True(seed != 0, "no seed left a leaf living beside the dead hunter");

            var found = DeadHunterAndLeaf(4f, seed).Value;
            long before = found.World.MouthBlows;
            long beforeFinishing = found.World.MouthBlowsFinishing;
            found.World.SetContacts(Touching(found.Remains, 0, found.Leaf, 1));
            found.World.ApplyMouth(1.5f);
            Assert.True(ShareTaken(found.Leaf, 1) > 0d, "the remains' part should wound");
            Assert.Equal(before, found.World.MouthBlows);
            Assert.Equal(beforeFinishing, found.World.MouthBlowsFinishing);
        }

        [Fact]
        public void ADialAtFourCheckpointRoundTripRestoresTheMouthBlowTotalsAndTheResumeEqualsTheUnbrokenRun()
        {
            World world = StoreWorld(4f);
            RunRemainsBites(world, 150);
            Assert.True(world.MouthBlows > 0L, "the fixture counted no mouth blow before the checkpoint");
            Assert.True(world.MouthBlowsFinishing <= world.MouthBlows);
            _output.WriteLine($"mouth blows {world.MouthBlows}, finishing {world.MouthBlowsFinishing}");

            byte[] state = StateOf(world);
            World restored = Restored(world.Config, 23, state);
            Assert.Equal(world.MouthBlows, restored.MouthBlows);
            Assert.Equal(world.MouthBlowsFinishing, restored.MouthBlowsFinishing);
            Assert.Equal(StateOf(world), StateOf(restored));

            for (int more = 0; more < 200; more++)
            {
                world.SetContacts(RemainsBites(world, 150 + more));
                restored.SetContacts(RemainsBites(restored, 150 + more));
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(world.MouthBlows, restored.MouthBlows);
            Assert.Equal(world.MouthBlowsFinishing, restored.MouthBlowsFinishing);
            Assert.Equal(StateOf(world), StateOf(restored));
        }

        [Fact]
        public void ADialAtOneCountsTheBlowsButWritesNoTotalAndAResumeAtOneStartsThemAtZero()
        {
            World world = StoreWorld(1f);
            RunRemainsBites(world, 150);
            Assert.True(world.MouthBlows > 0L, "the fixture counted no mouth blow at 1");

            World back = Restored(world.Config, 23, StateOf(world));
            Assert.Equal(0L, back.MouthBlows);
            Assert.Equal(0L, back.MouthBlowsFinishing);
            Assert.Equal(StateOf(world), StateOf(back));
        }

        // ------------------------------------------------------------------------ the dial off

        /// <summary>The digest of a dial-off world stepped over bites, heals, kills and growth: its state stream.</summary>
        private static string DialOffDigest(out World world)
        {
            world = BitingWorld(on: false);
            var sha = SHA256.Create();
            var all = new StringBuilder();

            for (int step = 0; step < 300; step++)
            {
                world.SetContacts(Bites(world, step));
                world.Step(0.5f);
                if (step % 50 == 49) all.Append(Convert.ToBase64String(sha.ComputeHash(StateOf(world)))).Append('|');
            }

            all.Append(world.Deaths).Append('|').Append(world.PartsKilled).Append('|').Append(world.BodiesEaten);
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(all.ToString())));
        }

        [Fact]
        public void TheDialOffLeavesASteppedWorldsStateStreamByteEqualToAWorldBuiltWithoutTheNewCode()
        {
            string digest = DialOffDigest(out World world);
            _output.WriteLine(
                "dial-off digest " + digest + $"; parts killed {world.PartsKilled}, bodies eaten {world.BodiesEaten}, " +
                $"healing {world.HealingJoules:0.###} J, deaths {world.Deaths}");

            // The scenario has to touch the paths the dial guards, or equal bytes say nothing.
            Assert.True(world.HealingJoules > 0d && world.PartsKilled + world.BodiesEaten > 0L, "the scenario never healed or killed");

            // Pinned from this same scenario run on the commit before the contact-eating build (a40ac972), in a copy
            // of that commit's Evosim.Core: the dial off, the stream is its bytes.
            Assert.Equal(PinnedDialOffDigest, digest);
        }

        private const string PinnedDialOffDigest = "EOxfRGP4KCEMErOdqsb5lYm3EWC4ujTP2Xr8y8L2Qu4=";

        // ------------------------------------------------------------------------ Organism.FoodFromLeavesJoules (the nursery's "eaten" under contact eating)

        [Fact]
        public void ABiteOnALeafAddsToTheBitersLeafFoodWhatItAddsToTheWorldsLeafTotal()
        {
            RunConfig config = Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(Leaf(2), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];
            Assert.True(leaf.HasPhotosyntheticTissue);
            Assert.Equal(0d, hunter.FoodFromLeavesJoules);

            world.SetContacts(Touching(hunter, 0, leaf, 1));
            world.ApplyMouth(1f);

            _output.WriteLine($"world total {world.BiteKeptFromLeavesJoules:R}, hunter {hunter.FoodFromLeavesJoules:R}");
            Assert.True(world.BiteKeptFromLeavesJoules > 0d, "the fixture should feed");
            Assert.Equal(world.BiteKeptFromLeavesJoules, hunter.FoodFromLeavesJoules);
            Assert.Equal(hunter.BiteKeptJoules, hunter.FoodFromLeavesJoules);
            Assert.Equal(0d, leaf.FoodFromLeavesJoules);

            // And it is cumulative: a second bite adds to both by the same amount.
            double world1 = world.BiteKeptFromLeavesJoules, hunter1 = hunter.FoodFromLeavesJoules;
            world.SetContacts(Touching(hunter, 0, leaf, 1));
            world.ApplyMouth(1f);
            Assert.True(world.BiteKeptFromLeavesJoules > world1);
            Assert.Equal(world.BiteKeptFromLeavesJoules - world1, hunter.FoodFromLeavesJoules - hunter1, 12);
        }

        [Fact]
        public void ABiteOnABodyWithoutPhotosyntheticTissueAndABiteOnRemainsAddNothingToLeafFood()
        {
            // A body with no photosynthetic tissue.
            RunConfig config = Stage(on: true);
            var world = new World(config, seed: 3);
            world.Inoculate(Fixtures.ArmedBox(half: 0.3f), 1, -1f);
            world.Inoculate(Hunter(ConsumerIntake), 1, -1f);
            Organism box = world.Living[0];
            Organism hunter = world.Living[1];
            Assert.False(box.HasPhotosyntheticTissue);

            world.SetContacts(Touching(hunter, 0, box, 0));
            world.ApplyMouth(1f);

            Assert.True(world.BiteKeptFromOthersJoules > 0d, "the fixture should feed");
            Assert.True(hunter.BiteKeptJoules > 0d);
            Assert.Equal(0d, hunter.FoodFromLeavesJoules);
            Assert.Equal(0d, world.BiteKeptFromLeavesJoules);

            // Remains.
            Cut cut = CutOffLimbs();
            double before = cut.Hunter.FoodFromLeavesJoules;
            double keptBefore = cut.Hunter.BiteKeptJoules;
            double remainsBefore = cut.World.BiteKeptFromRemainsJoules;
            cut.World.SetContacts(Touching(cut.Hunter, 0, cut.First, 0));
            cut.World.ApplyMouth(0.3f);

            Assert.True(cut.World.BiteKeptFromRemainsJoules > remainsBefore, "the fixture should feed on the remains");
            Assert.True(cut.Hunter.BiteKeptJoules > keptBefore);
            Assert.Equal(before, cut.Hunter.FoodFromLeavesJoules);
        }

        [Fact]
        public void LeafFoodSummedOverEveryBodyEverEqualsTheWorldsLeafTotalAndStaysZeroWithTheDialOff()
        {
            World world = BitingWorld(on: true);
            RunBites(world, 400);

            double sum = world.Living.Sum(c => c.FoodFromLeavesJoules) + world.Dead.Sum(c => c.FoodFromLeavesJoules);
            _output.WriteLine($"{world.Living.Count} living, {world.Dead.Count} dead: summed {sum:R} against the world's {world.BiteKeptFromLeavesJoules:R}");
            Assert.True(world.BiteKeptFromLeavesJoules > 0d, "nothing was kept from leaves");
            AssertRelative(world.BiteKeptFromLeavesJoules, sum, 1e-9, "the per-body counters against the world's total");

            World off = BitingWorld(on: false);
            RunBites(off, 400);
            Assert.Equal(0d, off.BiteKeptFromLeavesJoules);
            Assert.All(off.Living.Concat(off.Dead), c => Assert.Equal(0d, c.FoodFromLeavesJoules));
        }

        [Fact]
        public void LeafFoodIsInNeitherTheCheckpointNorTheConfigHash()
        {
            string hashBefore = new RunConfig().Hash();
            World world = BitingWorld(on: true);
            string configHash = world.Config.Hash();
            RunBites(world, 150);
            Assert.Equal(configHash, world.Config.Hash());
            Assert.Equal(hashBefore, new RunConfig().Hash());
            Assert.DoesNotContain(typeof(RunConfig).GetProperties(), p => p.Name.Contains("FoodFromLeaves"));

            Assert.Contains(world.Living, c => c.FoodFromLeavesJoules > 0d);
            Assert.Contains(world.Living, c => c.BiteKeptJoules > 0d);

            // The checkpoint's bytes are the same with the counters at zero, which is what a world that never counted them would write.
            byte[] counted = StateOf(world);
            double[] saved = world.Living.Select(c => c.FoodFromLeavesJoules).ToArray();
            foreach (Organism c in world.Living) Poke(c, "FoodFromLeavesJoules", 0d);
            byte[] uncounted = StateOf(world);
            Assert.Equal(counted, uncounted);
            for (int i = 0; i < saved.Length; i++) Poke(world.Living[i], "FoodFromLeavesJoules", saved[i]);

            // A restored body counts from its restore.
            World restored = Restored(world.Config, 11, counted);
            Assert.All(restored.Living, c => Assert.Equal(0d, c.FoodFromLeavesJoules));
            Assert.Equal(world.Living.Select(c => c.BiteKeptJoules).ToList(), restored.Living.Select(c => c.BiteKeptJoules).ToList());
            Assert.Equal(counted, StateOf(restored));
        }

        // ------------------------------------------------------------------------ the dial on, pinned

        /// <summary>
        /// The digest of a world's state stream over <paramref name="steps"/> half-second steps, its state hashed every
        /// <paramref name="every"/> steps and the stream closed with its counts of deaths, parts killed and bodies eaten.
        /// <paramref name="contacts"/> hands the world each step's contacts.
        /// </summary>
        internal static string StreamDigest(World world, int steps, int every, Func<World, int, List<CreatureContact>> contacts)
        {
            var sha = SHA256.Create();
            var all = new StringBuilder();

            for (int step = 0; step < steps; step++)
            {
                world.SetContacts(contacts(world, step));
                world.Step(0.5f);
                if (step % every == every - 1) all.Append(Convert.ToBase64String(sha.ComputeHash(StateOf(world)))).Append('|');
            }

            all.Append(world.Deaths).Append('|').Append(world.PartsKilled).Append('|').Append(world.BodiesEaten);
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(all.ToString())));
        }

        /// <summary>
        /// Contact eating's own path against recorded bits. Before 2026-10-25 nothing pinned a world with the dial on, so
        /// a change to <c>WorldContact</c> or <c>WorldMouth</c> was checked by its books and never by its bytes.
        /// </summary>
        /// <remarks>
        /// Pinned from this same scenario run on <c>main</c> at <c>1e9b2aca</c>, before D232's kill gut, in a worktree of
        /// that commit. The gut off writes nothing to the state, so the bytes must be the same on every later build. A
        /// format bump of the world's state re-pins it, as it would any recording.
        /// </remarks>
        [Fact]
        public void TheDialOnBitingWorldsStateStreamIsTheBytesPinnedBeforeTheKillGut()
        {
            World world = BitingWorld(on: true);
            string digest = StreamDigest(world, 300, 50, Bites);
            _output.WriteLine(
                "dial-on digest " + digest + $"; parts killed {world.PartsKilled}, bodies eaten {world.BodiesEaten}, " +
                $"healing {world.HealingJoules:0.###} J, deaths {world.Deaths}, kept {world.BiteKeptFromLeavesJoules:0.###} + " +
                $"{world.BiteKeptFromOthersJoules:0.###} J");

            Assert.True(world.HealingJoules > 0d && world.PartsKilled + world.BodiesEaten > 0L, "the scenario never healed or killed");
            Assert.True(world.BiteKeptFromLeavesJoules + world.BiteKeptFromOthersJoules > 0d, "no bite was kept");
            Assert.Equal(PinnedDialOnDigest, digest);
        }

        /// <summary>The same for the remains window and the bite on the store, over the 700 steps that close remains.</summary>
        /// <remarks>Pinned as <see cref="TheDialOnBitingWorldsStateStreamIsTheBytesPinnedBeforeTheKillGut"/> is.</remarks>
        [Fact]
        public void TheStoreWorldsStateStreamIsTheBytesPinnedBeforeTheKillGut()
        {
            World world = StoreWorld();
            string digest = StreamDigest(world, 700, 100, RemainsBites);
            _output.WriteLine(
                "store digest " + digest + $"; store taken {world.BiteStoreTakenJoules:0.###} J, from remains " +
                $"{world.BiteKeptFromRemainsJoules:0.###} J, remains closed {world.RemainsClosed}, deaths {world.Deaths}");

            Assert.True(world.BiteStoreTakenJoules > 0d, "no bite took a store");
            Assert.True(world.BiteKeptFromRemainsJoules > 0d, "no bite on remains was kept");
            Assert.True(world.RemainsClosed > 0L, "no remains closed");
            Assert.Equal(PinnedStoreDigest, digest);
        }

        private const string PinnedDialOnDigest = "jfWBue/6sF+VBgPFECfn13fxaxrAkb3i6MwvfE2ZnwI=";
        private const string PinnedStoreDigest = "LWDBiKrM/c9n8VAMfuOXMA1ICHEJiXm/T4/kzL6Npfk=";
    }
}

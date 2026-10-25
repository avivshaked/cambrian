using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D232: a kill swallows the whole body into the killer's gut. Driven by hand as <see cref="ContactEatingTests"/> is,
    /// and built on its fixtures: Core has no physics, so each test hands the world the contact list a bite would make.
    /// </summary>
    public class KillGutTests
    {
        private readonly ITestOutputHelper _output;

        public KillGutTests(ITestOutputHelper output) => _output = output;

        private static float ConsumerIntake => ContactEatingTests.ConsumerIntake;

        /// <summary>A childless, bright world with contact eating and the switch on.</summary>
        private static RunConfig GutStage(float waste = 0.25f)
        {
            RunConfig config = ContactEatingTests.Stage(on: true, waste: waste);
            config.KillSwallowsBody = true;
            return config;
        }

        /// <summary>A body's mouth cap for a step, in offered joules: intake times area, summed, times the scale, the step and a unit's price.</summary>
        private static double CapOf(Organism body, RunConfig config, double seconds)
        {
            double rate = 0d;
            foreach (PhenotypePart part in body.Phenotype.Parts)
            {
                if (part.Intake > 0f) rate += (double)part.Intake * Math.Max(0f, part.SurfaceArea);
            }

            return rate * config.IntakeScale * seconds * config.JoulesPerUnit;
        }

        // ------------------------------------------------------------------------ the switch

        [Fact]
        public void TheSwitchIsOffByDefaultAndIsRefusedOnWithContactEatingOffWithAMessageThatNamesIt()
        {
            Assert.False(new RunConfig().KillSwallowsBody);

            RunConfig refused = ContactEatingTests.Stage(on: false);
            refused.KillSwallowsBody = true;
            ArgumentException error = Assert.Throws<ArgumentException>(() => new World(refused, seed: 3));
            Assert.Contains("KillSwallowsBody", error.Message);

            Assert.True(new World(GutStage(), seed: 3).Config.KillSwallowsBody);
            Assert.NotEqual(ContactEatingTests.Stage(on: true).Hash(), GutStage().Hash());
        }

        // ------------------------------------------------------------------------ the kill

        [Fact]
        public void AKillPutsTheDeadBodysTissueAndStoreInTheKillersGutAndLeavesNoRemains()
        {
            RunConfig config = GutStage();
            var world = new World(config, seed: 3);
            world.Inoculate(ContactEatingTests.Branching(), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake * 0.001f), 1, -1f);
            Organism tree = world.Living[0];
            Organism hunter = world.Living[1];

            // The root is bitten whole: the body dies, and the four parts hanging from it are what is left to swallow.
            double tissueOfTheRest = 0d;
            for (int p = 1; p < 5; p++) tissueOfTheRest += ContactEatingTests.TissueOf(tree, p, config);
            double reserve = tree.Energy;

            world.SetContacts(ContactEatingTests.Touching(hunter, 0, tree, 0));
            world.ApplyMouth(100f);

            Assert.Equal(1L, world.BodiesEaten);
            Assert.DoesNotContain(world.Living, c => c.Id == tree.Id);

            // No remains of any kind: the whole body is in the gut.
            Assert.Empty(world.Remains);
            Assert.Empty(world.DrainRemainsFoundings());
            Assert.Equal(0L, world.RemainsFounded);
            Assert.Equal(0L, world.RemainsClosed);
            Assert.Empty(world.Corpses);

            Assert.Equal(1L, world.KillsSwallowed);
            ContactEatingTests.AssertRelative(tissueOfTheRest + reserve, world.SwallowedJoules, 1e-9, "the body's tissue and store");
            Assert.Equal(0d, tree.TissueJoules);
            Assert.Equal(0d, tree.Energy);

            // The killer holds it, less what its mouth digested in the same pass.
            ContactEatingTests.AssertRelative(
                world.SwallowedJoules, hunter.GutJoules + world.GutDigestedJoules, 1e-12, "swallowed = held + digested");
            Assert.True(hunter.GutJoules > 0d, "the small mouth should have left something in the gut");

            Assert.Contains(
                world.DrainLineageEvents(), e => e.Kind == LineageEventKind.Death && e.Id == tree.Id && e.Cause == DeathCause.Eaten);
            ContactEatingTests.AssertBooksClose(world, "after the swallow");
            _output.WriteLine($"swallowed {world.SwallowedJoules:0.###} J; gut {hunter.GutJoules:0.###} J, digested {world.GutDigestedJoules:0.###} J");
        }

        [Fact]
        public void ADeathByAgeOrStarvationStillLeavesRemainsWithTheSwitchOn()
        {
            // Age: the body is the remains, its reserve the store.
            RunConfig aged = ContactEatingTests.RemainsStage();
            aged.KillSwallowsBody = true;
            aged.AgeHazardBaseSeconds = 2f;
            var ageing = new World(aged, seed: 3);
            ageing.Inoculate(ContactEatingTests.Branching(), 1, -1f);
            for (int step = 0; step < 400 && ageing.Remains.Count == 0; step++) ageing.Step(0.5f);

            Assert.Single(ageing.Remains);
            Assert.Equal(1L, ageing.DeathsByAge);
            Assert.Equal(0L, ageing.KillsSwallowed);
            Assert.Equal(0d, ageing.SwallowedJoules);

            // Starvation: the same.
            RunConfig starved = GutStage();
            starved.FounderEnergyJoules = 0.001f;
            var starving = new World(starved, seed: 3);
            starving.Inoculate(Fixtures.ArmedBox(), 1, -1f);
            Organism body = starving.Living[0];
            for (int step = 0; step < 400 && starving.Remains.Count == 0; step++) starving.Step(0.5f);

            Assert.Single(starving.Remains);
            Assert.Same(body, starving.Remains[0]);
            Assert.Equal(0L, starving.KillsSwallowed);
            ContactEatingTests.AssertBooksClose(starving, "after the starvation");
        }

        [Fact]
        public void TwoMouthsOnOneBodyInOneStepGiveTheBodyToTheOneWhoseBlowWasLastInTheContactList()
        {
            // Wound records, for the part that died, the attacker of the last blow that reached it in this pass, in the
            // order of the contact list, whether or not an earlier blow had already taken the last of its health.
            // The killer is that attacker, and it swallows if it is a living body.
            foreach (bool reversed in new[] { false, true })
            {
                RunConfig config = GutStage();
                var world = new World(config, seed: 3);
                world.Inoculate(ContactEatingTests.Leaf(1), 1, -1f);
                world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake * 0.001f), 2, -1f);
                Organism leaf = world.Living[0];
                Organism first = world.Living[1];
                Organism second = world.Living[2];

                ContactEatingTests.Poke(leaf, "Energy", 500d);

                var contacts = new List<CreatureContact>
                {
                    new CreatureContact(first.Id, 0, leaf.Id, 0),
                    new CreatureContact(second.Id, 0, leaf.Id, 0),
                };
                if (reversed) contacts.Reverse();

                Organism expected = reversed ? first : second;
                Organism other = reversed ? second : first;

                world.SetContacts(contacts);
                world.ApplyMouth(100f);

                Assert.Equal(1L, world.KillsSwallowed);
                Assert.Equal(0d, other.GutJoules);
                Assert.True(expected.GutJoules > 0d, "the last blow's owner should hold the body");
                ContactEatingTests.AssertRelative(500d, world.SwallowedJoules, 1e-9, "the store swallowed");
                ContactEatingTests.AssertRelative(
                    500d, expected.GutJoules + world.GutDigestedJoules, 1e-12, "held + digested");
                Assert.Empty(world.Remains);
            }
        }

        [Fact]
        public void AKillerWithNoMouthSwallowsNothingAndLeavesRemainsAsWithTheSwitchOff()
        {
            // The coordinator's reading of D232: swallowing is a mouth's act, so a claw alone kills as it does with the
            // switch off. The same kill is made with the switch off and on, and the two worlds must agree.
            World Kill(bool gut, out Organism leaf, out Organism claw)
            {
                RunConfig config = GutStage();
                config.KillSwallowsBody = gut;
                var world = new World(config, seed: 3);
                world.Inoculate(ContactEatingTests.Leaf(1), 1, -1f);
                world.Inoculate(ContactEatingTests.Claw(), 1, -1f);
                leaf = world.Living[0];
                claw = world.Living[1];
                ContactEatingTests.Poke(leaf, "Energy", 500d);
                world.SetContacts(ContactEatingTests.Touching(claw, 0, leaf, 0));
                world.ApplyMouth(100f);
                return world;
            }

            World on = Kill(true, out Organism leafOn, out Organism clawOn);
            World off = Kill(false, out Organism leafOff, out _);

            Assert.DoesNotContain(off.Living, c => c.Id == leafOff.Id);
            Assert.DoesNotContain(on.Living, c => c.Id == leafOn.Id);
            Assert.Equal(0L, on.KillsSwallowed);
            Assert.Equal(0d, clawOn.GutJoules);
            Assert.Equal(off.Remains.Count, on.Remains.Count);
            Assert.Equal(off.StandingJoules, on.StandingJoules);
        }

        // ------------------------------------------------------------------------ the digestion

        [Fact]
        public void DigestionTakesAtMostTheCapAStepAndSplitsItByTheWasteAndTheGutIsCountedInTheBooks()
        {
            RunConfig config = GutStage();
            var world = new World(config, seed: 3);
            world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake), 1, -1f);
            Organism hunter = world.Living[0];

            double cap = CapOf(hunter, config, 0.5);
            Assert.True(cap > 0d);

            double standing = world.StandingJoules;
            double inBodies = world.StandingJoulesInBodies;
            ContactEatingTests.Poke(hunter, "GutJoules", 10d * cap);

            // A joule in a gut is counted wherever the books count the living bodies' joules.
            ContactEatingTests.AssertRelative(standing + 10d * cap, world.StandingJoules, 1e-12, "StandingJoules");
            ContactEatingTests.AssertRelative(inBodies + 10d * cap, world.StandingJoulesInBodies, 1e-12, "StandingJoulesInBodies");

            double reserve = hunter.Energy;
            double water = world.Nutrients.TotalJoules;
            double waste = config.IntakeWasteFraction;

            world.ApplyMouth(0.5f);

            ContactEatingTests.AssertRelative(cap, world.GutDigestedJoules, 1e-6, "digested in a step");
            ContactEatingTests.AssertRelative(9d * cap, hunter.GutJoules, 1e-6, "left in the gut");
            ContactEatingTests.AssertRelative((1d - waste) * cap, hunter.Energy - reserve, 1e-5, "the reserve takes 1 - waste");
            ContactEatingTests.AssertRelative(waste * cap, world.Nutrients.TotalJoules - water, 1e-4, "the waste goes to the water");
            ContactEatingTests.AssertRelative(waste * cap, world.GutWasteJoules, 1e-5, "the waste total");
            ContactEatingTests.AssertRelative(9d * cap, world.GutCapCutJoules, 1e-6, "what the cap held back");
            Assert.Equal(1L, world.GutCapBound);

            // And a gut smaller than the cap is emptied in one step.
            ContactEatingTests.Poke(hunter, "GutJoules", 0.25d * cap);
            double digestedBefore = world.GutDigestedJoules;
            world.ApplyMouth(0.5f);
            ContactEatingTests.AssertRelative(0.25d * cap, world.GutDigestedJoules - digestedBefore, 1e-6, "a small gut digested whole");
            Assert.Equal(0d, hunter.GutJoules);
            Assert.Equal(1L, world.GutCapBound);
        }

        [Fact]
        public void BitesTakeTheCapFirstAndTheGutFillsWhatTheyLeave()
        {
            RunConfig config = GutStage();
            double waste = config.IntakeWasteFraction;

            // A bite that offers less than the cap: the gut gets the rest of it.
            {
                var world = new World(config, seed: 3);
                world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
                world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake), 1, -1f);
                Organism leaf = world.Living[0];
                Organism hunter = world.Living[1];
                double cap = CapOf(hunter, config, 1d);
                ContactEatingTests.Poke(hunter, "GutJoules", 10d * cap);

                double reserve = hunter.Energy;
                double water = world.Nutrients.TotalJoules;
                world.SetContacts(ContactEatingTests.Touching(hunter, 0, leaf, 1));
                world.ApplyMouth(1f);

                double share = 1d - leaf.PartHealth[1];
                Assert.True(share > 0d && share < 1d, $"the fixture should wound and not kill: {share}");
                double offered = share * ContactEatingTests.TissueOf(leaf, 1, config);
                Assert.True(offered < cap, $"the fixture should offer less than the mouth can carry: {offered} against {cap}");

                ContactEatingTests.AssertRelative(cap - offered, world.GutDigestedJoules, 1e-6, "the gut fills what the bite left");
                ContactEatingTests.AssertRelative(
                    (1d - waste) * cap, hunter.Energy - reserve, 1e-5, "bite and gut together keep 1 - waste of the cap");
                ContactEatingTests.AssertRelative(waste * cap, world.Nutrients.TotalJoules - water, 1e-4, "and waste the rest of it");
                Assert.Equal(0d, world.BiteCapCutJoules);
            }

            // A bite that offers more than the cap: the bite takes all of it and the gut gets none.
            {
                var world = new World(config, seed: 3);
                world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
                world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake * 0.001f), 1, -1f);
                Organism leaf = world.Living[0];
                Organism hunter = world.Living[1];
                double cap = CapOf(hunter, config, 1d);
                ContactEatingTests.Poke(hunter, "GutJoules", 50d);

                double reserve = hunter.Energy;
                world.SetContacts(ContactEatingTests.Touching(hunter, 0, leaf, 1));
                world.ApplyMouth(1f);

                double share = 1d - leaf.PartHealth[1];
                double offered = share * ContactEatingTests.TissueOf(leaf, 1, config);
                Assert.True(offered > cap, $"the fixture should offer more than the mouth can carry: {offered} against {cap}");

                Assert.True(world.GutDigestedJoules < 1e-9, $"the gut digested {world.GutDigestedJoules:R} against a cap the bite used");
                ContactEatingTests.AssertRelative(50d, hunter.GutJoules, 1e-9, "the gut is untouched");
                ContactEatingTests.AssertRelative((1d - waste) * cap, hunter.Energy - reserve, 1e-5, "the bite keeps 1 - waste of the cap");
                ContactEatingTests.AssertRelative(offered - cap, world.BiteCapCutJoules, 1e-6, "what the cap cut off the bite");
                ContactEatingTests.AssertRelative(50d, world.GutCapCutJoules, 1e-6, "what the cap held back in the gut");
                Assert.Equal(1L, world.GutCapBound);
            }
        }

        // ------------------------------------------------------------------------ the killer's death

        [Fact]
        public void WhatIsInTheGutWhenABodyDiesGoesIntoItsOwnRemainsWithItsStore()
        {
            RunConfig config = ContactEatingTests.RemainsStage();
            config.KillSwallowsBody = true;
            config.AgeHazardBaseSeconds = 2f;
            var world = new World(config, seed: 3);
            world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake * 0.001f), 1, -1f);
            Organism hunter = world.Living[0];
            ContactEatingTests.Poke(hunter, "GutJoules", 500d);

            double gutBeforeTheStep = hunter.GutJoules;
            for (int step = 0; step < 400 && world.Remains.Count == 0; step++)
            {
                gutBeforeTheStep = hunter.GutJoules;
                world.Step(0.5f);
            }

            Assert.Single(world.Remains);
            Organism remains = world.Remains[0];
            Assert.Same(hunter, remains);
            Assert.Equal(0d, remains.GutJoules);
            Assert.True(gutBeforeTheStep > 100d, $"the gut should still have held most of its 500 J: {gutBeforeTheStep}");

            // The death happens in the metabolic pass, before the mouth's: the gut it died with is the gut the step began with.
            LineageEvent death = world.DrainLineageEvents().Single(e => e.Kind == LineageEventKind.Death);
            ContactEatingTests.AssertRelative(
                Math.Max(0d, death.ReserveJoulesAtDeath) + gutBeforeTheStep, remains.Energy, 1e-9, "the remains' store holds the reserve and the gut");
            Assert.Equal(0L, world.KillsSwallowed);
        }

        [Fact]
        public void ASwallowedKillerHandsItsGutOnToItsKiller()
        {
            RunConfig config = GutStage();
            var world = new World(config, seed: 3);
            world.Inoculate(ContactEatingTests.Leaf(1), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ConsumerIntake * 0.001f), 1, -1f);
            // Armour is a rate over the defender's area and a blow one over the attacker's (DamageOf), and the consumer's
            // protection is capped at 0.5. So the armoured body is the larger: at a half extent of 0.4 against the
            // hunter's 0.25 its armour is 0.5 x 2.56 of the hunter's blow and stops it. With equal sizes each would kill
            // the other in the pass, and the walk would hand the armoured body to the hunter instead.
            world.Inoculate(
                Fixtures.ArmedBox(
                    attack: 1f, protection: 0.5f, intake: ConsumerIntake * 0.001f, half: 0.4f,
                    cellTypeId: CellTypeIds.Consumer),
                1, -1f);
            Organism leaf = world.Living[0];
            Organism hunter = world.Living[1];
            Organism armoured = world.Living[2];

            ContactEatingTests.Poke(leaf, "Energy", 500d);

            // The hunter kills the leaf and holds it.
            world.SetContacts(ContactEatingTests.Touching(hunter, 0, leaf, 0));
            world.ApplyMouth(100f);
            Assert.Equal(1L, world.KillsSwallowed);
            double gut = hunter.GutJoules;
            Assert.True(gut > 100d, $"the hunter should hold most of the leaf: {gut}");

            // Then the armoured body, with a small mouth, kills the hunter: the hunter's blow cannot get through its
            // armour. It digests a little of its gut in the same step, so what it holds and what it digested add up.
            double reserve = hunter.Energy;
            double digestedBefore = world.GutDigestedJoules;
            double swallowedBefore = world.SwallowedJoules;
            double hunterTissue = hunter.TissueJoules;
            world.SetContacts(ContactEatingTests.Touching(armoured, 0, hunter, 0));
            world.ApplyMouth(100f);

            _output.WriteLine(
                $"hunter alive {world.Living.Any(c => c.Id == hunter.Id)}, armoured alive {world.Living.Any(c => c.Id == armoured.Id)}, " +
                $"remains {world.Remains.Count}, kills swallowed {world.KillsSwallowed}, guts: hunter {hunter.GutJoules:R} armoured {armoured.GutJoules:R}; " +
                $"hunter gut before {gut:R}, reserve before {reserve:R}, tissue before {hunterTissue:R}; digested this step " +
                $"{world.GutDigestedJoules - digestedBefore:R}; armoured reserve {armoured.Energy:R}, mouth rate cap per step present: {armoured.Phenotype.Parts[0].Intake}");

            Assert.Contains(world.Living, c => c.Id == armoured.Id);
            Assert.DoesNotContain(world.Living, c => c.Id == hunter.Id);
            Assert.Empty(world.Remains);
            Assert.Equal(2L, world.KillsSwallowed);
            Assert.Equal(0d, hunter.GutJoules);
            // What the armoured body swallowed is the hunter's tissue and store at its death, and its store then held
            // its own gut (Bury adds it). The live bite of this step took its share first, so the swallow is at least
            // the hunter's old gut and at most everything it held. All of it is in the new gut or was digested.
            double swallowed = world.SwallowedJoules - swallowedBefore;
            Assert.True(swallowed >= gut, $"the swallow {swallowed} should hold at least the hunter's gut {gut}");
            Assert.True(swallowed <= gut + reserve + hunterTissue + 1e-9, $"the swallow {swallowed} is more than the hunter held");
            ContactEatingTests.AssertRelative(
                swallowed, armoured.GutJoules + (world.GutDigestedJoules - digestedBefore), 1e-9,
                "the new killer's gut and its digestion hold the swallow");
        }

        // ------------------------------------------------------------------------ the books and the checkpoint

        [Fact]
        public void TheMatterAndEnergyBooksCloseEveryStepOverTwoThousandSecondsOfAWorldWithManyKills()
        {
            World world = KillGutCrowd.Build(gut: true);
            double mostHeld = 0d;

            KillGutCrowd.Run(world, 4000, step =>
            {
                ContactEatingTests.AssertBooksClose(world, $"step {step}");
                mostHeld = Math.Max(mostHeld, world.GutJoulesHeld);
                world.DrainRemainsFoundings();
            });

            _output.WriteLine(
                $"alive {world.Living.Count}, remains {world.RemainsStanding}, deaths {world.Deaths}, bodies eaten {world.BodiesEaten}, parts killed {world.PartsKilled}; " +
                $"kills swallowed {world.KillsSwallowed} ({world.SwallowedJoules:0.###} J), digested {world.GutDigestedJoules:0.###} J, " +
                $"held {world.GutJoulesHeld:0.###} J (most {mostHeld:0.###}), bite cap cut {world.BiteCapCutJoules:0.###} J, " +
                $"gut cap cut {world.GutCapCutJoules:0.###} J over {world.GutCapBound} body-steps; " +
                $"audit {world.AuditResidual:R}, matter {world.MatterResidual:R}");

            Assert.True(world.KillsSwallowed >= 5L, $"too few kills swallowed to read the books over: {world.KillsSwallowed}");
            Assert.True(world.GutDigestedJoules > 0d, "nothing was digested");
            Assert.True(mostHeld > 0d, "no gut ever held anything");
            Assert.True(world.GutWasteJoules > 0d, "no digestion paid its waste");
            Assert.Empty(world.Corpses);
        }

        /// <summary>
        /// The gut's path against recorded bits: the kill crowd's state stream over 2,000 steps, pinned on the build that
        /// made the gut (integration branch 1, 2026-10-25), so a later change to the swallow or the digestion shows as a
        /// changed digest. <see cref="ContactEatingTests.TheDialOnBitingWorldsStateStreamIsTheBytesPinnedBeforeTheKillGut"/>
        /// pins the gut off.
        /// </summary>
        [Fact]
        public void TheKillCrowdsStateStreamWithTheGutOnIsTheBytesPinnedOnTheGutsBuild()
        {
            World world = KillGutCrowd.Build(gut: true);
            string digest = ContactEatingTests.StreamDigest(world, 2000, 200, KillGutCrowd.Bites);
            _output.WriteLine(
                $"gut digest {digest}; kills swallowed {world.KillsSwallowed} ({world.SwallowedJoules:0.###} J), " +
                $"digested {world.GutDigestedJoules:0.###} J, held {world.GutJoulesHeld:0.###} J, deaths {world.Deaths}");

            Assert.True(world.KillsSwallowed >= 2L, $"too few kills swallowed for the digest to cover the gut: {world.KillsSwallowed}");
            Assert.True(world.GutDigestedJoules > 0d, "nothing was digested");
            Assert.Equal(PinnedGutDigest, digest);
        }

        private const string PinnedGutDigest = "AxMZD2FlL+3lrF1DrjF+MAjIX/NrlBatm/VfFvWbpgU=";

        [Fact]
        public void ACheckpointWithGutsHeldRoundTripsAndTheResumeEqualsTheUnbrokenRun()
        {
            World world = KillGutCrowd.Build(gut: true);

            // Until a gut holds something, so the save has one in it.
            int step = 0;
            for (; step < 3000 && !(world.GutJoulesHeld > 0d); step++)
            {
                world.SetContacts(KillGutCrowd.Bites(world, step));
                world.Step(0.5f);
            }

            Assert.True(world.GutJoulesHeld > 0d, "the fixture swallowed nothing before the checkpoint");

            byte[] state = ContactEatingTests.StateOf(world);
            World restored = ContactEatingTests.Restored(world.Config, 31, state);

            Assert.Equal(world.Living.Select(c => c.GutJoules).ToList(), restored.Living.Select(c => c.GutJoules).ToList());
            Assert.Equal(world.KillsSwallowed, restored.KillsSwallowed);
            Assert.Equal(world.SwallowedJoules, restored.SwallowedJoules);
            Assert.Equal(world.GutDigestedJoules, restored.GutDigestedJoules);
            Assert.Equal(world.GutWasteJoules, restored.GutWasteJoules);
            Assert.Equal(world.BiteCapCutJoules, restored.BiteCapCutJoules);
            Assert.Equal(world.GutCapCutJoules, restored.GutCapCutJoules);
            Assert.Equal(world.GutCapBound, restored.GutCapBound);
            Assert.Equal(ContactEatingTests.StateOf(world), ContactEatingTests.StateOf(restored));

            for (int more = 0; more < 600; more++)
            {
                world.SetContacts(KillGutCrowd.Bites(world, step + more));
                restored.SetContacts(KillGutCrowd.Bites(restored, step + more));
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(world.KillsSwallowed, restored.KillsSwallowed);
            Assert.Equal(world.GutDigestedJoules, restored.GutDigestedJoules);
            Assert.Equal(world.GutJoulesHeld, restored.GutJoulesHeld);
            Assert.Equal(ContactEatingTests.StateOf(world), ContactEatingTests.StateOf(restored));
            _output.WriteLine($"saved at step {step} with the gut holding; {world.KillsSwallowed} kills swallowed by the end");
        }

        [Fact]
        public void TheSwitchAddsSevenTotalsAndOneDoublePerBodyToTheCheckpointOnlyWhenOn()
        {
            World off = KillGutCrowd.Build(gut: false);
            World on = KillGutCrowd.Build(gut: true);
            Assert.Equal(off.Living.Count, on.Living.Count);

            int extra = sizeof(long) + 6 * sizeof(double) + sizeof(double) * (on.Living.Count + on.Remains.Count);
            Assert.Equal(ContactEatingTests.StateOf(off).Length + extra, ContactEatingTests.StateOf(on).Length);

            // A world with the switch off reads back with the totals at zero and writes the bytes it was read from.
            World back = ContactEatingTests.Restored(off.Config, 31, ContactEatingTests.StateOf(off));
            Assert.Equal(0L, back.KillsSwallowed);
            Assert.Equal(ContactEatingTests.StateOf(off), ContactEatingTests.StateOf(back));
        }

        [Fact]
        public void TheSwitchOffWritesNothingOfTheGutAndLeavesTheBooksTheNumbersTheyWere()
        {
            World world = KillGutCrowd.Build(gut: false);
            KillGutCrowd.Run(world, 600);

            Assert.Equal(0L, world.KillsSwallowed);
            Assert.Equal(0d, world.SwallowedJoules);
            Assert.Equal(0d, world.GutDigestedJoules);
            Assert.Equal(0d, world.BiteCapCutJoules);
            Assert.Equal(0d, world.GutJoulesHeld);
            Assert.True(world.BodiesEaten > 0L, "nothing was eaten whole, so the death path was not read");
            Assert.True(world.RemainsFounded + world.RemainsClosed > 0L || world.RemainsStanding > 0, "no remains were made with the switch off");
        }
    }

    /// <summary>
    /// A small crowd with many kills, shared by the books test, the checkpoint test and the thread-count test (D232).
    /// Hunters bite prey for a few steps at a time, one prey each, in rotation; when no prey is left they bite each
    /// other, which kills in pairs and in turns and so reaches a killer that is itself killed.
    /// </summary>
    internal static class KillGutCrowd
    {
        internal static RunConfig Config(bool gut)
        {
            RunConfig config = ContactEatingTests.Stage(on: true);
            config.SenescenceDoublingSeconds = 0f;
            config.RemainsWindowSeconds = 25f;
            config.KillSwallowsBody = gut;
            return config;
        }

        internal static World Build(bool gut)
        {
            var world = new World(Config(gut), seed: 31);
            world.Inoculate(ContactEatingTests.Leaf(1), 8, -1f);
            world.Inoculate(ContactEatingTests.Leaf(3, 0.18f), 3, -1f);
            world.Inoculate(ContactEatingTests.Branching(), 2, -1f);
            world.Inoculate(Fixtures.ArmedBox(half: 0.3f), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake * 0.3f), 3, -1f);
            world.Inoculate(ContactEatingTests.Claw(), 1, -1f);
            return world;
        }

        internal static List<CreatureContact> Bites(World world, int step)
        {
            var biters = new List<Organism>();
            var prey = new List<Organism>();
            foreach (Organism body in world.Living)
            {
                if (body.HasAttack) biters.Add(body);
                else prey.Add(body);
            }

            var contacts = new List<CreatureContact>();
            for (int i = 0; i < biters.Count; i++)
            {
                Organism victim;
                if (prey.Count > 0) victim = prey[(step / 6 + i) % prey.Count];
                else if (biters.Count > 1) victim = biters[(i + 1) % biters.Count];
                else continue;

                int parts = victim.Phenotype.PartCount;
                int part = parts > 1 ? (step / 6 + i) % parts : 0;
                contacts.Add(new CreatureContact(biters[i].Id, 0, victim.Id, part));
            }

            return contacts;
        }

        internal static void Run(World world, int steps, Action<int> after = null)
        {
            for (int step = 0; step < steps; step++)
            {
                world.SetContacts(Bites(world, step));
                world.Step(0.5f);
                after?.Invoke(step);
            }
        }
    }

    /// <summary>D232: the thread count does not change a world with the switch on.</summary>
    // Parallelism.Threads is a static: every test class that sets it shares this collection, so xUnit runs them one at a time.
    [Collection("Parallelism.Threads")]
    public class KillGutThreadTests
    {
        private readonly ITestOutputHelper _output;

        public KillGutThreadTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public void AWorldWithTheSwitchOnIsTheSameAtEveryThreadCount()
        {
            int was = Parallelism.Threads;

            try
            {
                string one = Run(1);
                string four = Run(4);
                string sixteen = Run(16);

                _output.WriteLine("threads  1: " + one);
                _output.WriteLine("threads  4: " + four);
                _output.WriteLine("threads 16: " + sixteen);

                Assert.Equal(one, four);
                Assert.Equal(one, sixteen);
            }
            finally
            {
                Parallelism.Threads = was;
            }
        }

        private static string Run(int threads)
        {
            Parallelism.Threads = threads;

            World world = KillGutCrowd.Build(gut: true);
            KillGutCrowd.Run(world, 1200);
            Assert.True(world.KillsSwallowed > 0L, "nothing was swallowed, so the gut was not tested");

            using (var sha = SHA256.Create())
            {
                var all = new StringBuilder();
                all.Append(Convert.ToBase64String(sha.ComputeHash(ContactEatingTests.StateOf(world)))).Append('|');
                all.Append(world.KillsSwallowed).Append('|').Append(world.GutDigestedJoules.ToString("R")).Append('|').Append(world.GutJoulesHeld.ToString("R"));
                return all.ToString();
            }
        }
    }
}

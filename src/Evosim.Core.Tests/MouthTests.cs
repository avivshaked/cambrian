using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The mouth: four priced attributes, health as state, the kill and the meal — D106 items 1,
    /// 3, 4 and 5, and <c>logbook/specs/mouth-spec.md</c> rules 1 to 7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A fight is driven by hand here, and that is the point of the seam.</b> Core has no
    /// physics and its bodies never touch; what reaches <see cref="World.ApplyMouth"/> is a list
    /// of <see cref="CreatureContact"/> the farm builds from the overlap census. So these tests
    /// build the list themselves and hand it over, which measures the rule rather than the
    /// solver — and is the only way to ask "how many steps does this claw need" and get an
    /// answer that is arithmetic instead of a realisation.
    /// </para>
    /// <para>
    /// <b>Both books are the subject, as they were for the module gene.</b> A kill is a transfer
    /// between accounts §5A.2's audit and D098's matter identity already sum, and intake is
    /// another; healing is the one term that leaves, and it leaves the way every other
    /// expenditure in D098 leaves. The way this mechanism fails is not that the wrong creature
    /// wins — it is that a joule appears from nowhere while every column reads normally.
    /// </para>
    /// </remarks>
    public class MouthTests
    {
        private readonly ITestOutputHelper _output;

        public MouthTests(ITestOutputHelper output) => _output = output;

        // ------------------------------------------------------------------------ the fixtures

        /// <summary>
        /// A spine of <paramref name="segments"/> photosynthetic boxes, fixed-jointed and born
        /// adult: the smallest body on which a part can be bitten off and leave a body behind.
        /// </summary>
        private static Genome Spine(int segments, float half = 0.2f) =>
            Fixtures.MouthSpine(segments, half);

        /// <summary>One structural box carrying an attribute at its cell type's cap.</summary>
        private static Genome Armed(
            float attack = 0f, float protection = 0f, float intake = 0f, float toughness = 1f,
            float half = 0.25f, string cellTypeId = CellTypeIds.Structural) =>
            Fixtures.ArmedBox(attack, protection, intake, toughness, half, cellTypeId);

        /// <summary>
        /// A bright, childless world with health worth measuring. The overhead is what makes it
        /// childless — no reserve a body here can reach clears a gate of a billion joules — so the
        /// population stays what the test inoculated and every reading is a named body's.
        /// </summary>
        private static RunConfig Stage(float healthPerCubicMetre = 100f)
        {
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(400f, 12f),
                InitialMatterPerCubicMetre = 50f,
                PerOffspringOverheadJoules = 1e9f,
                HealthPerCubicMetre = healthPerCubicMetre,
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

        /// <summary>Both identities, at the scale each is kept in — ModuleGeneTests' rule.</summary>
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

        /// <summary>The contact the physics would report: one body's part against another's.</summary>
        private static List<CreatureContact> Touching(
            Organism a, int partA, Organism b, int partB) =>
            new List<CreatureContact> { new CreatureContact(a.Id, partA, b.Id, partB) };

        /// <summary>
        /// Bites until the named part comes off, and returns the steps it took. The contact list is
        /// renewed every step because <see cref="World.ApplyMouth"/> clears it — a list that is not
        /// renewed cannot be applied twice, which is what stops a stale contact killing anything.
        /// </summary>
        private static int BiteUntilAPartComesOff(
            World world, Organism attacker, Organism victim, int part, float seconds, int limit)
        {
            int parts = victim.Phenotype.PartCount;

            for (int step = 1; step <= limit; step++)
            {
                world.SetContacts(Touching(attacker, 0, victim, part));
                world.ApplyMouth(seconds);
                AssertBooksClose(world, $"after bite {step}");

                if (world.PartsKilled + world.BodiesEaten > 0L ||
                    victim.Phenotype.PartCount != parts)
                {
                    return step;
                }
            }

            return -1;
        }

        // ----------------------------------------------------------------- rule 1, the founders

        [Fact]
        public void AFounderDrawsAttackAndProtectionAtZeroAndIntakeAtItsCellTypesCap()
        {
            // Rule 1, and the half of it that matters for the record: a world founded by the
            // lottery starts with nothing armed and nothing armoured, so the first claw in any run
            // is a mutation and can be dated. Intake is the exception the spec makes — a consumer
            // cell that cannot eat is a cell type with no function at all — and it is drawn at the
            // cap rather than sampled, so no draw is taken for any of the four.
            CellTypeRegistry types = CellTypeRegistry.Standard;

            for (ulong seed = 1; seed <= 40; seed++)
            {
                Genome genome = GenomeFactory.Founder(new Rng(seed), cellTypes: types);

                foreach (MorphNode node in genome.Nodes)
                {
                    CellType type = types.Resolve(node.CellTypeId);

                    Assert.Equal(0f, node.Attack);
                    Assert.Equal(0f, node.Protection);
                    Assert.Equal(1f, node.Toughness);
                    Assert.Equal(type.IntakeMax, node.Intake);
                }

                Assert.Empty(genome.Validate(types));
            }
        }

        [Fact]
        public void TheFoundingDrawTakesNoRandomnessForTheAttributes()
        {
            // The property the regress against mt0 rests on, stated as something a test can see.
            // Two founders off the same seed, one drawn through the registry that carries D106's
            // caps and one through the parameterless call — if any of the four took a draw, the
            // generators would part and the two bodies with them.
            var withTypes = new Rng(4242);
            var without = new Rng(4242);

            Genome a = GenomeFactory.Founder(withTypes, cellTypes: CellTypeRegistry.Standard);
            Genome b = GenomeFactory.Founder(without);

            _output.WriteLine(
                $"{a.Nodes.Count} nodes against {b.Nodes.Count}, " +
                $"generator at {withTypes.State} against {without.State}");

            Assert.Equal(withTypes.State, without.State);
            Assert.Equal(a.Nodes.Count, b.Nodes.Count);
        }

        // -------------------------------------------------------------------- rule 2, the caps

        [Fact]
        public void AGenomeAboveACapIsRefusedRatherThanClamped()
        {
            // §9's rule, applied to an attribute: a clamped genome is a different creature wearing
            // the stored one's identity. A photosynthetic cell's attack cap is zero, so an armed
            // leaf is a body this world does not build.
            Genome leaf = Spine(1);
            leaf.Nodes[0].Attack = 0.5f;

            Assert.Contains(leaf.Validate(), i => i.Contains("Attack") && i.Contains("cap"));

            ArgumentException refused =
                Assert.Throws<ArgumentException>(() => Developer.Develop(leaf));

            _output.WriteLine(refused.Message);
            Assert.Contains("Attack", refused.Message);
        }

        [Fact]
        public void ToughnessIsRefusedBelowOneAndAboveTheCap()
        {
            // The one attribute with a floor that is not zero: toughness is a multiplier on a
            // health pool and 1 is the neutral value every price in D106 is written against, so
            // below it is not a weaker body but a body the economy has no price for.
            Genome soft = Spine(1);
            soft.Nodes[0].Toughness = 0.5f;
            Assert.Contains(soft.Validate(), i => i.Contains("Toughness") && i.Contains("floor"));

            // A structural cell's cap is 4 (the spec's table), so 5 is over it and 4 is not.
            Genome hard = Armed(toughness: 5f);
            Assert.Contains(hard.Validate(), i => i.Contains("Toughness") && i.Contains("cap"));

            Assert.Empty(Armed(toughness: 4f).Validate());
        }

        [Fact]
        public void TheMutatorNeverExceedsACapAndTakesNoDrawAtAChanceOfZero()
        {
            // Both halves of rule 2's mutation, which is the same argument the module gene's own
            // test makes. At zero the operator does not touch the stream, so the recorded worlds
            // replay; above zero it moves the attributes and stops at the ceiling.
            var quiet = new MutationRates { AttributeMutationChance = 0f };

            var a = new Rng(99);
            var b = new Rng(99);

            Genome armed = GenomeFactory.Random(new Rng(7), cellTypes: CellTypeRegistry.Standard);
            Genome plain = armed.Clone();

            foreach (MorphNode node in armed.Nodes)
            {
                CellType type = CellTypeRegistry.Standard.Resolve(node.CellTypeId);
                node.Attack = type.AttackMax;
                node.Protection = type.ProtectionMax;
            }

            for (int i = 0; i < 100; i++)
            {
                armed = Mutator.Mutate(armed, a, quiet);
                plain = Mutator.Mutate(plain, b, quiet);
            }

            _output.WriteLine($"generator at {a.State} against {b.State} after 100 births");
            Assert.Equal(a.State, b.State);

            // And above zero the operator moves them, and every node stays under its own ceiling
            // — Validate is the check, since Mutate throws on a genome it broke.
            var loud = new MutationRates { AttributeMutationChance = 0.2f };
            var rng = new Rng(11);

            Genome g = GenomeFactory.Random(new Rng(7), cellTypes: CellTypeRegistry.Standard);
            int moved = 0;

            for (int i = 0; i < 300; i++)
            {
                g = Mutator.Mutate(g, rng, loud, cellTypes: CellTypeRegistry.Standard);

                foreach (MorphNode node in g.Nodes)
                {
                    CellType type = CellTypeRegistry.Standard.Resolve(node.CellTypeId);

                    Assert.True(
                        node.Attack <= type.AttackMax,
                        $"attack {node.Attack} over the '{node.CellTypeId}' cap {type.AttackMax}");
                    Assert.True(node.Protection <= type.ProtectionMax);
                    Assert.True(node.Intake <= type.IntakeMax);
                    Assert.True(node.Toughness >= 1f && node.Toughness <= type.ToughnessMax);

                    if (node.Attack > 0f || node.Protection > 0f) moved++;
                }
            }

            _output.WriteLine($"{moved} armed node-readings over 300 births at chance 0.2");
            Assert.True(moved > 0, "no attribute ever moved off its founder value");
        }

        // -------------------------------------------------- rules 3 to 5, the wound and the kill

        [Fact]
        public void AClawTakesAPartOffALeafInThePredictedNumberOfSteps()
        {
            RunConfig config = Stage();

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(2), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);

            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];

            Assert.Equal(2, leaf.Phenotype.PartCount);

            // The prediction, out of rule 3 and rule 5 and nothing else: a pool of volume times
            // toughness times the health price, drained by the claw's attack over its own area net
            // of the leaf's armour over its.
            PhenotypePart bitten = leaf.Phenotype.Parts[1];
            PhenotypePart tooth = claw.Phenotype.Parts[0];

            float pool = Metabolism.HealthPool(bitten, config);
            float perStep = tooth.Attack * tooth.SurfaceArea - bitten.Protection * bitten.SurfaceArea;
            int predicted = (int)Math.Ceiling(pool / perStep);

            _output.WriteLine(
                $"pool {pool:0.####} health against {perStep:0.####} a second: {predicted} steps");

            Assert.True(predicted > 1, "the fixture kills in one step and measures nothing");

            int took = BiteUntilAPartComesOff(world, claw, leaf, part: 1, seconds: 1f, limit: 100);

            Assert.Equal(predicted, took);
            Assert.Equal(1L, world.PartsKilled);
            Assert.Equal(1, leaf.Phenotype.PartCount);
            Assert.Equal(0L, world.BodiesEaten);
            Assert.Contains(world.Living, c => c.Id == leaf.Id);
        }

        [Fact]
        public void TheLostPartBecomesACorpseCarryingItsTissueAndItsShareOfTheReserve()
        {
            // Rule 4's parcel. The killer gains nothing: what the part was worth goes into the
            // water as a corpse at the place the body stands, and whether anybody eats it is rule
            // 6's business and a separate event.
            RunConfig config = Stage();
            config.CorpseDecayPerSecond = 0.001f;

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(2), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);

            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];

            float wholeVolume = leaf.Phenotype.TotalVolume;
            float lostVolume = leaf.Phenotype.Parts[1].Volume;

            double tissueBefore = leaf.TissueJoules;
            double reserveBefore = leaf.Energy;
            double standingBefore = world.StandingJoules;

            Assert.True(BiteUntilAPartComesOff(world, claw, leaf, 1, 1f, 100) > 0);

            Assert.Single(world.Corpses);
            Corpse corpse = world.Corpses[0];

            double lostTissue = tissueBefore - leaf.TissueJoules;
            double lostReserve = reserveBefore - leaf.Energy;
            double share = lostVolume / wholeVolume;

            _output.WriteLine(
                $"corpse {corpse.Joules:0.######} J = tissue {lostTissue:0.######} + " +
                $"reserve {lostReserve:0.######} (share {share:0.####} of {reserveBefore:0.####})");

            Assert.True(corpse.Joules > 0d, "the kill founded an empty corpse");
            Fixtures.AssertClose(
                lostTissue + lostReserve, corpse.Joules, Math.Max(1e-9, 1e-6 * corpse.Joules));

            // The share is by volume, which is D106's ruling — "its volume over the body's".
            Fixtures.AssertClose(reserveBefore * share, lostReserve, 1e-4 * reserveBefore);

            // And nothing left the world: what the body lost the corpse holds, and the corpse is
            // standing in both books until it leaks.
            Fixtures.AssertClose(standingBefore, world.StandingJoules, 1e-9 * standingBefore);
            Assert.Equal(1L, world.CorpsesFromKills);
            AssertBooksClose(world, "after the kill");
        }

        [Fact]
        public void TheRootsDeathIsTheBodysAndTheWholeBodyIsTheCorpse()
        {
            RunConfig config = Stage();
            config.CorpseDecayPerSecond = 0.001f;

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(1), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);

            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];

            Assert.Equal(1, leaf.Phenotype.PartCount);

            double whole = leaf.Energy + leaf.TissueJoules;
            double standingBefore = world.StandingJoules;

            int took = BiteUntilAPartComesOff(world, claw, leaf, part: 0, seconds: 1f, limit: 100);

            _output.WriteLine(
                $"eaten in {took} steps; {world.Living.Count} alive, " +
                $"{world.Corpses.Count} corpse(s) holding {world.Corpses[0].Joules:0.####} J " +
                $"against a body worth {whole:0.####} J");

            Assert.True(took > 0);
            Assert.Equal(1L, world.BodiesEaten);
            Assert.DoesNotContain(world.Living, c => c.Id == leaf.Id);
            Assert.Single(world.Corpses);

            Fixtures.AssertClose(whole, world.Corpses[0].Joules, Math.Max(1e-9, 1e-6 * whole));
            Fixtures.AssertClose(standingBefore, world.StandingJoules, 1e-9 * standingBefore);
            AssertBooksClose(world, "after the body died");
        }

        [Fact]
        public void ArmourAtItsCapHoldsForLongerAndAtTheClawsOwnStrengthHoldsOutright()
        {
            // Rule 5's net: armour is a rate over the defending part's area and comes off the blow
            // before it reaches the pool. Two readings — one where it buys steps and one where it
            // buys the fight, which is the shape a cuticle has to have to be worth paying for.
            RunConfig config = Stage();

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(2), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);

            Organism bare = world.Living[0];
            Organism claw = world.Living[1];

            int bareSteps = BiteUntilAPartComesOff(world, claw, bare, 1, 1f, 200);

            // The same fight against a cuticle at the photosynthetic cap.
            var armoured = new World(Stage(), seed: 3);
            Genome cuticle = Spine(2);
            cuticle.Nodes[0].Protection =
                CellTypeRegistry.Standard.Resolve(CellTypeIds.Photosynthetic).ProtectionMax;

            armoured.Inoculate(cuticle, 1, -1f);
            armoured.Inoculate(Armed(attack: 1f), 1, -1f);

            int armouredSteps = BiteUntilAPartComesOff(
                armoured, armoured.Living[1], armoured.Living[0], 1, 1f, 200);

            _output.WriteLine($"{bareSteps} steps bare against {armouredSteps} behind a cuticle");

            Assert.True(bareSteps > 0 && armouredSteps > 0);
            Assert.True(
                armouredSteps > bareSteps,
                $"the cuticle bought nothing: {armouredSteps} steps against {bareSteps}");

            // And armour that matches the blow stops it outright. A structural flank at the
            // protection cap over the same area as the claw's tooth takes nothing at all, for a
            // hundred steps — floored at zero rather than allowed to heal the defender.
            var stalemate = new World(Stage(), seed: 3);
            stalemate.Inoculate(Armed(protection: 1f), 1, -1f);
            stalemate.Inoculate(Armed(attack: 1f), 1, -1f);

            Assert.Equal(
                -1,
                BiteUntilAPartComesOff(
                    stalemate, stalemate.Living[1], stalemate.Living[0], 0, 1f, 100));

            Assert.Null(stalemate.Living[0].PartHealth);
            Assert.Equal(0L, stalemate.PartsKilled);
        }

        [Fact]
        public void HealingDrainsTheReserveAtItsPriceAndStopsWhenItIsEmpty()
        {
            // Rule 3's repair, isolated: one wound, no fight, and a body whose reserve is set by
            // hand so the reading is the price and not the body's income.
            RunConfig config = Stage();
            config.HealingPerSecond = 0.1f;
            config.HealingJoulesPerHealth = 2f;

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(1), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);

            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];

            // One bite, well short of the pool, and then a step of repair on its own.
            world.SetContacts(Touching(claw, 0, leaf, 0));
            world.ApplyMouth(0.2f);

            float hurt = leaf.PartHealth[0];
            Assert.True(hurt > 0f && hurt < 1f, $"the bite left health at {hurt}");

            float pool = Metabolism.HealthPool(leaf.Phenotype.Parts[0], config);
            double reserveBefore = leaf.Energy;

            // Cumulative, like every other counter on the world: the bite's own call already
            // bought a step of repair, so what this call spent is a difference and not a total.
            double burntBefore = world.HealingJoules;

            world.ApplyMouth(1f);

            double spent = reserveBefore - leaf.Energy;
            float gained = leaf.PartHealth[0] - hurt;

            _output.WriteLine(
                $"healed {gained:0.######} of a pool of {pool:0.####} for {spent:0.######} J " +
                $"at {config.HealingJoulesPerHealth} J a health");

            Assert.True(gained > 0f, "nothing healed");
            Fixtures.AssertClose(
                gained * pool * config.HealingJoulesPerHealth, spent, Math.Max(1e-9, 1e-6 * spent));
            Fixtures.AssertClose(
                spent, world.HealingJoules - burntBefore, Math.Max(1e-9, 1e-6 * spent));
            AssertBooksClose(world, "after a repair");

            // And a body buys what it can afford and no more. The price is put out of reach
            // rather than the reserve emptied by hand — the two are the same comparison, and this
            // is the one a test can make without reaching into a body's account. The body spends
            // everything, heals a sliver, and the step after that buys nothing at all.
            double left = leaf.Energy;
            config.HealingJoulesPerHealth = 1e9f;

            world.SetContacts(Touching(claw, 0, leaf, 0));
            world.ApplyMouth(0.2f);

            _output.WriteLine(
                $"at a price of {config.HealingJoulesPerHealth:0.#e0} J a health the body spent " +
                $"its whole {left:0.####} J and holds {leaf.Energy:0.##e0} J, " +
                $"health at {leaf.PartHealth[0]:0.######}");

            Assert.True(leaf.Energy <= 1e-9 * left, $"{leaf.Energy:R} J was left unspent");
            Assert.True(leaf.PartHealth[0] < 1f, "the wound was repaired at a price it could not pay");
            AssertBooksClose(world, "after a repair the body could barely pay for");

            // And the step after that buys nothing at all, because there is nothing to buy it
            // with: rule 3's "not at all when the reserve is empty", which is what makes a wound
            // something a poor body carries rather than something it works off.
            float held = leaf.PartHealth[0];
            world.ApplyMouth(1f);

            Fixtures.AssertClose(held, leaf.PartHealth[0], 1e-6f);
            AssertBooksClose(world, "after a repair nobody could pay for");
        }

        // -------------------------------------------------------------- rule 4's row, the kill

        /// <summary>The kill rows in a drained queue, in the order they were queued.</summary>
        private static List<LineageEvent> KillsIn(IReadOnlyList<LineageEvent> events)
        {
            var kills = new List<LineageEvent>();
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == LineageEventKind.Kill) kills.Add(events[i]);
            }
            return kills;
        }

        [Fact]
        public void ALimbLostWritesOneKillRowNamingTheVictimTheAttackerAndWhatWentWithIt()
        {
            // The instrument round 45's J3 asks for and nothing in the record had: a body that
            // loses a part and lives writes no death row, so before this row a grazed body was
            // indistinguishable from an untouched one in every file a run produces.
            RunConfig config = Stage();
            config.CorpseDecayPerSecond = 0.001f;

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(2), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);

            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];

            // The two inoculation births, taken off the queue so what is left is the bite's.
            world.DrainLineageEvents();

            Assert.True(BiteUntilAPartComesOff(world, claw, leaf, part: 1, seconds: 1f, limit: 100) > 0);

            IReadOnlyList<LineageEvent> events = world.DrainLineageEvents();
            List<LineageEvent> kills = KillsIn(events);

            LineageEvent kill = Assert.Single(kills);
            Assert.DoesNotContain(events, e => e.Kind == LineageEventKind.Death);
            Assert.Contains(world.Living, c => c.Id == leaf.Id);

            Corpse corpse = Assert.Single(world.Corpses);

            _output.WriteLine(kill.ToJson());

            Assert.Equal(leaf.Id, kill.Id);
            Assert.Equal(claw.Id, kill.AttackerId);
            Assert.False(kill.RootLost);
            Assert.Equal(1, kill.PartsLost);
            Assert.Equal(leaf.IndeterminateNodes, kill.IndeterminateNodes);
            Assert.Equal(world.ElapsedSeconds, kill.ElapsedSeconds);

            // tj and rj are the two halves of exactly what the corpse holds — the kill's one
            // quantisation, which is what makes the row readable as an energy statement and not
            // only as an event.
            Fixtures.AssertClose(
                kill.TissueJoulesLost + kill.ReserveJoulesLost, corpse.Joules,
                Math.Max(1e-9, 1e-6 * corpse.Joules));
            Assert.True(kill.TissueJoulesLost > 0d, "the limb carried no tissue");

            // One row, one line, and the fields a reader is promised.
            string json = kill.ToJson();
            Assert.DoesNotContain('\n', json);
            Assert.DoesNotContain('\r', json);

            JsonNode row = Json.Parse(json);
            Assert.Equal("k", row["e"].AsString());
            Assert.Equal(leaf.Id, (long)row["id"].AsDouble());
            Assert.Equal(claw.Id, (long)row["by"].AsDouble());
            Assert.Equal(0, row["root"].AsInt());
            Assert.Equal(1, row["parts"].AsInt());
            Fixtures.AssertClose(
                kill.TissueJoulesLost, row["tj"].AsDouble(), Math.Max(1e-9, 1e-6 * corpse.Joules));
            Fixtures.AssertClose(
                kill.ReserveJoulesLost, row["rj"].AsDouble(), Math.Max(1e-9, 1e-6 * corpse.Joules));
            Assert.Equal(leaf.IndeterminateNodes, row["ind"].AsInt());

            // A birth's fields are a birth's: nothing on this row pretends to be one.
            Assert.DoesNotContain("\"abs\"", json);
            Assert.DoesNotContain("\"bf\"", json);
            Assert.DoesNotContain("\"c\"", json);
        }

        [Fact]
        public void AKillRowNamesThePartThatCameOffAndTheAttackersTouchingPart()
        {
            // Round 46's K9b: which part came off, and which of the attacker's parts was on it.
            // The attacker is a two-part armed spine touching with its second part, so a byPart
            // of 1 can only have come from the contact record and not from a default of 0.
            RunConfig config = Stage();
            config.CorpseDecayPerSecond = 0.001f;

            Genome armedSpine = Spine(2, half: 0.25f);
            armedSpine.Nodes[0].CellTypeId = CellTypeIds.Structural;
            armedSpine.Nodes[0].Attack = 1f;

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(2), 1, -1f);
            world.Inoculate(armedSpine, 1, -1f);

            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];
            Assert.Equal(2, claw.Phenotype.PartCount);

            world.DrainLineageEvents();

            int took = -1;
            for (int step = 1; step <= 100 && took < 0; step++)
            {
                world.SetContacts(Touching(claw, 1, leaf, 1));
                world.ApplyMouth(1f);
                if (world.PartsKilled > 0L) took = step;
            }

            Assert.True(took > 0, "the limb never came off");

            LineageEvent kill = Assert.Single(KillsIn(world.DrainLineageEvents()));
            _output.WriteLine(kill.ToJson());

            Assert.Equal(claw.Id, kill.AttackerId);
            Assert.Equal(1, kill.PartIndex);
            Assert.Equal(1, kill.AttackerPartIndex);

            JsonNode row = Json.Parse(kill.ToJson());
            Assert.Equal(1, row["part"].AsInt());
            Assert.Equal(1, row["byPart"].AsInt());

            // A kill row built without the two, which is what a restored checkpoint's is, says
            // it cannot tell rather than naming the root.
            LineageEvent bare = LineageEvent.Kill(1d, 7L, -1L, false, 1, 0d, 0d, 0);
            Assert.Equal(-1, bare.PartIndex);
            Assert.Equal(-1, bare.AttackerPartIndex);
            Assert.Equal(-1, Json.Parse(bare.ToJson())["byPart"].AsInt());
        }

        [Fact]
        public void ARootKillWritesAKillRowAndTheEatenDeathRowAfterIt()
        {
            // Both rows, in that order: the kill is the event and the death is what it was. A
            // reader watching a grazed body for a thousand seconds needs the first to find the
            // victim and the second to know it did not survive.
            RunConfig config = Stage();
            config.CorpseDecayPerSecond = 0.001f;

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(1), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);

            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];

            double whole = leaf.Energy + leaf.TissueJoules;
            world.DrainLineageEvents();

            Assert.True(BiteUntilAPartComesOff(world, claw, leaf, part: 0, seconds: 1f, limit: 100) > 0);

            IReadOnlyList<LineageEvent> events = world.DrainLineageEvents();

            int killAt = -1;
            int deathAt = -1;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == LineageEventKind.Kill && killAt < 0) killAt = i;
                if (events[i].Kind == LineageEventKind.Death && deathAt < 0) deathAt = i;
            }

            Assert.True(killAt >= 0, "the root's loss wrote no kill row");
            Assert.True(deathAt > killAt, "the death row did not follow the kill row");

            LineageEvent kill = events[killAt];
            LineageEvent death = events[deathAt];

            _output.WriteLine($"{kill.ToJson()}\n{death.ToJson()}");

            Assert.True(kill.RootLost);
            Assert.Equal(leaf.Id, kill.Id);
            Assert.Equal(claw.Id, kill.AttackerId);
            Assert.Equal(1, kill.PartsLost);

            // Round 46's K9b on the root's row: the part is the root, the claw's only part hit it.
            Assert.Equal(0, kill.PartIndex);
            Assert.Equal(0, kill.AttackerPartIndex);
            Fixtures.AssertClose(
                kill.TissueJoulesLost + kill.ReserveJoulesLost, whole,
                Math.Max(1e-9, 1e-6 * whole));

            Assert.Equal(leaf.Id, death.Id);
            Assert.Equal(DeathCause.Eaten, death.Cause);
            Assert.Equal(1, Json.Parse(kill.ToJson())["root"].AsInt());
            Assert.Equal("eaten", Json.Parse(death.ToJson())["c"].AsString());
        }

        [Fact]
        public void AWorldWhereNothingIsBittenWritesNoKillRows()
        {
            // The other half of the recording argument: the row exists only where a part came
            // off, so a world of plants — every world in the record — writes a lineage file of
            // exactly the births and deaths it always wrote.
            RunConfig config = Stage();

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(2), 1, -1f);

            for (int second = 0; second < 60; second++) world.Step(1f);

            Assert.Empty(KillsIn(world.DrainLineageEvents()));
            Assert.Equal(0L, world.PartsKilled);
        }

        // ------------------------------------------------------------------- rule 6, the intake

        [Fact]
        public void AMouthInReachEmptiesACorpseAtItsRateAndTheWasteGoesToSnow()
        {
            RunConfig config = Stage();
            config.CorpseDecayPerSecond = 0.0001f;
            config.IntakeReachMetres = 5f;
            config.IntakeWasteFraction = 0.25f;

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(1), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);
            world.Inoculate(
                Armed(intake: CellTypeRegistry.Standard.Resolve(CellTypeIds.Consumer).IntakeMax,
                      cellTypeId: CellTypeIds.Consumer),
                1, -1f);

            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];
            Organism mouth = world.Living[2];

            Assert.True(mouth.HasIntake, "the consumer was built without a mouth");

            // A corpse is only an object above a decay rate of zero (CLAUDE.md's corpse note), and
            // nothing here steps the corpse pass, so what the mouth finds is what the kill left.
            Assert.True(BiteUntilAPartComesOff(world, claw, leaf, 0, 1f, 100) > 0);
            Assert.Single(world.Corpses);

            Corpse corpse = world.Corpses[0];
            double held = corpse.Joules;
            double snowBefore = world.Nutrients.TotalJoules;
            double reserveBefore = mouth.Energy;

            // What one step should move, out of rule 6 and nothing else.
            PhenotypePart jaw = mouth.Phenotype.Parts[0];
            double wanted = (double)jaw.Intake * jaw.SurfaceArea * 0.5d * config.JoulesPerUnit;
            double expected = Math.Min(wanted, held);

            world.ApplyMouth(0.5f);

            double taken = held - corpse.Joules;
            double waste = world.Nutrients.TotalJoules - snowBefore;
            double kept = mouth.Energy - reserveBefore;

            _output.WriteLine(
                $"took {taken:0.######} J of {held:0.####} (wanted {wanted:0.######}), " +
                $"kept {kept:0.######} and wasted {waste:0.######} to snow");

            Fixtures.AssertClose(expected, taken, Math.Max(1e-9, 1e-6 * expected));
            Fixtures.AssertClose(
                taken * config.IntakeWasteFraction, waste, Math.Max(1e-9, 1e-5 * taken));
            Fixtures.AssertClose(taken - waste, kept, Math.Max(1e-9, 1e-5 * taken));
            Fixtures.AssertClose(
                taken / config.JoulesPerUnit, world.UnitsEaten,
                Math.Max(1e-9, 1e-6 * world.UnitsEaten));

            AssertBooksClose(world, "after a mouthful");

            // A corpse is finite. Enough steps and it is gone, counted once and never a source of
            // more than it held.
            for (int step = 0; step < 500 && world.Corpses.Count > 0; step++)
            {
                world.ApplyMouth(0.5f);
                AssertBooksClose(world, "while the corpse was eaten down");
            }

            _output.WriteLine(
                $"{world.UnitsEaten:0.####} units eaten of {held / config.JoulesPerUnit:0.####} " +
                $"offered; {world.CorpsesEaten} corpse(s) emptied");

            Assert.True(world.CorpsesEaten >= 1L, "the corpse was never emptied");
            Assert.True(
                world.UnitsEaten <= held / config.JoulesPerUnit + 1e-6,
                "more was eaten than the corpse ever held");
        }

        [Fact]
        public void AMouthOutOfReachTakesNothing()
        {
            // The distance test is a test. Reach is measured from the corpse's own surface to the
            // body's centre, so a reach of nothing puts every corpse out of it — which is also the
            // recorded world's setting, and the reason this whole pass costs it one walk.
            RunConfig config = Stage();
            config.CorpseDecayPerSecond = 0.0001f;
            config.IntakeReachMetres = 0f;

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(1), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);
            world.Inoculate(
                Armed(intake: 1f, cellTypeId: CellTypeIds.Consumer), 1, -1f);

            Assert.True(BiteUntilAPartComesOff(world, world.Living[1], world.Living[0], 0, 1f, 100) > 0);

            double held = world.Corpses[0].Joules;
            for (int step = 0; step < 20; step++) world.ApplyMouth(1f);

            Assert.Equal(held, world.Corpses[0].Joules);
            Assert.Equal(0d, world.UnitsEaten);
            Assert.Equal(0L, world.CorpsesEaten);
        }

        // ------------------------------------------------------------------- rule 7, the prices

        [Fact]
        public void EveryDefaultReproducesTheUpkeepToTheBit()
        {
            // The default-neutrality argument, as a number. At the recorded world's prices — all
            // four at zero — a body carrying every attribute at its cap costs exactly what the
            // same body costs with none of them, because the term is not reached at all.
            var config = new RunConfig();

            Assert.Equal(0f, config.AttackWattsPerUnit);
            Assert.Equal(0f, config.IntakeWattsPerUnit);
            Assert.Equal(0f, config.ProtectionWattsPerUnit);
            Assert.Equal(0f, config.ToughnessWattsPerUnit);

            Phenotype plain = Developer.Develop(Armed());
            Phenotype loaded = Developer.Develop(Armed(attack: 1f, protection: 1f, toughness: 4f));

            float bare = Metabolism.StandingWatts(plain, config);
            float armed = Metabolism.StandingWatts(loaded, config);

            _output.WriteLine($"{bare:R} W plain against {armed:R} W at every cap");
            Assert.Equal(bare, armed);

            // And with a price set the term appears, exactly as rule 7 writes it: a rate per unit
            // per square metre for the three that are surface, and per cubic metre above 1 for
            // toughness, which is a property of the flesh and not of the skin.
            config.AttackWattsPerUnit = 0.5f;
            config.ProtectionWattsPerUnit = 0.25f;
            config.ToughnessWattsPerUnit = 2f;

            PhenotypePart part = loaded.Parts[0];
            float predicted =
                0.5f * part.Attack * part.SurfaceArea +
                0.25f * part.Protection * part.SurfaceArea +
                2f * (part.Toughness - 1f) * part.Volume;

            Fixtures.AssertClose(predicted, Metabolism.AttributeWatts(part, config), 1e-6f);
            Fixtures.AssertClose(
                bare + predicted, Metabolism.StandingWatts(loaded, config), 1e-5f);
            Assert.Equal(0f, Metabolism.AttributeWatts(plain.Parts[0], config));
        }

        [Fact]
        public void AWorldWithNothingArmedPaysNothingAndMovesNothing()
        {
            // The whole of the regress, stated in one world: plants, no mouths, no armour, the two
            // senses off and the prices at zero. The pass runs on every step and changes no
            // account, which is what the field-by-field comparison against mt0 asserts at scale.
            var config = new RunConfig
            {
                MinimumPopulation = 0,
                MaximumPopulation = 100_000,
                Light = new LightModel(400f, 12f),
                InitialMatterPerCubicMetre = 50f,
                PerOffspringOverheadJoules = 1e9f,
            };

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(2), 1, -1f);

            Organism leaf = world.Living[0];

            for (int second = 0; second < 300; second++)
            {
                world.Step(1f);
                AssertBooksClose(world, "in a world with no mouth in it");
            }

            _output.WriteLine(
                $"{leaf.Phenotype.PartCount} parts, health {(leaf.PartHealth == null ? "untouched" : "allocated")}, " +
                $"{world.PartsKilled} killed, {world.HealingJoules:0.##} J healed");

            // Nothing was allocated, which is the cost argument: a body that is never wounded and
            // never watched never grows a health array at all.
            Assert.Null(leaf.PartHealth);
            Assert.Null(leaf.PartDamage);
            Assert.Null(leaf.PartContact);

            Assert.Equal(0L, world.PartsKilled);
            Assert.Equal(0L, world.BodiesEaten);
            Assert.Equal(0d, world.UnitsEaten);
            Assert.Equal(0d, world.HealingJoules);
            Assert.Equal(0f, world.AttackShare);
            Assert.Equal(0f, world.IntakeShare);
            Assert.Equal(0f, world.ProtectionShare);
        }

        // ------------------------------------------------------------- rule 5's two new senses

        [Fact]
        public void ContactAndDamageAreOffUntilTheirDialsAreOnAndThenTheyMoveWithTheFight()
        {
            // The append-order argument: the two channels go on the end of the pool, so a run with
            // them off draws the same channel for the same neuron as the record did. The pool is
            // the readable half of that; the other half is that nothing here takes a draw at all.
            var off = new RunConfig();
            SensorChannel[] plain = off.SensorPool();

            Assert.DoesNotContain(SensorChannel.Contact, plain);
            Assert.DoesNotContain(SensorChannel.Damage, plain);

            var on = new RunConfig { SenseContact = true, SenseDamage = true };
            SensorChannel[] wider = on.SensorPool();

            _output.WriteLine(
                $"{plain.Length} channels off against {wider.Length} on: " +
                string.Join(", ", wider));

            Assert.Equal(plain.Length + 2, wider.Length);
            for (int i = 0; i < plain.Length; i++) Assert.Equal(plain[i], wider[i]);

            Assert.Equal(SensorChannel.Contact, wider[plain.Length]);
            Assert.Equal(SensorChannel.Damage, wider[plain.Length + 1]);

            // And with the dials on, a fight writes what the senses read.
            RunConfig config = Stage();
            config.SenseContact = true;
            config.SenseDamage = true;

            var world = new World(config, seed: 3);
            world.Inoculate(Spine(2), 1, -1f);
            world.Inoculate(Armed(attack: 1f), 1, -1f);

            Organism leaf = world.Living[0];
            Organism claw = world.Living[1];

            world.SetContacts(Touching(claw, 0, leaf, 1));
            world.ApplyMouth(0.1f);

            Assert.NotNull(leaf.PartContact);
            Assert.True(leaf.PartContact[1], "the bitten part felt nothing");
            Assert.False(leaf.PartContact[0], "a part nothing touched felt a touch");

            Assert.NotNull(leaf.PartDamage);
            Assert.True(leaf.PartDamage[1] > 0f, "the bitten part took no readable damage");
            Assert.Equal(0f, leaf.PartDamage[0]);
        }
    }
}

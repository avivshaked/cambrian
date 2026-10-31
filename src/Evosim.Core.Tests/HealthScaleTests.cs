using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D264, phase 1: <see cref="RunConfig.AnimalHealthScale"/> and <see cref="RunConfig.PlantHealthScale"/> scale a
    /// part's health pool by its cell type, and <see cref="RunConfig.DeathRowIncome"/> puts a body's lifetime bite
    /// income by source on its death row. Driven by hand as <see cref="ContactEatingTests"/> is, and built on its fixtures.
    /// </summary>
    public class HealthScaleTests
    {
        private readonly ITestOutputHelper _output;

        public HealthScaleTests(ITestOutputHelper output) => _output = output;

        // ------------------------------------------------------------------------ the health pool

        private static readonly string[] AllCellTypes = typeof(CellTypeIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue())
            .Where(id => CellTypeRegistry.Standard.Contains(id))
            .ToArray();

        /// <summary>One part of the given cell type, developed under the config, at a toughness that is not the neutral 1.</summary>
        private static PhenotypePart PartOf(string cellTypeId, RunConfig config)
        {
            Genome genome = Fixtures.ArmedBox(toughness: 1f, cellTypeId: cellTypeId);
            Phenotype body = Developer.Develop(genome, config.Development, null, config.Shapes);
            return body.Parts[0];
        }

        /// <summary>The pool as it was written before D264: volume times toughness times the base, and nothing else.</summary>
        private static float Recorded(PhenotypePart part, RunConfig config) =>
            Math.Max(0f, part.Volume) * part.Toughness * config.HealthPerCubicMetre;

        [Fact]
        public void TheCoverageOfTheCellTypesIsEveryBuiltInOne()
        {
            Assert.Contains(CellTypeIds.Photosynthetic, AllCellTypes);
            Assert.Contains(CellTypeIds.Consumer, AllCellTypes);
            Assert.Contains(CellTypeIds.Structural, AllCellTypes);
            Assert.True(AllCellTypes.Length >= 7, string.Join(",", AllCellTypes));
        }

        [Fact]
        public void BothScalesAreOneByDefaultAndRefuseZeroNegativeAndNonFiniteValues()
        {
            var config = new RunConfig();
            Assert.Equal(1f, config.AnimalHealthScale);
            Assert.Equal(1f, config.PlantHealthScale);
            Assert.False(config.DeathRowIncome);

            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => config.AnimalHealthScale = bad);
                Assert.Throws<ArgumentOutOfRangeException>(() => config.PlantHealthScale = bad);
            }

            Assert.Equal(1f, config.AnimalHealthScale);
            Assert.Equal(1f, config.PlantHealthScale);

            string recorded = new RunConfig().Hash();
            Assert.NotEqual(recorded, new RunConfig { AnimalHealthScale = 10f }.Hash());
            Assert.NotEqual(recorded, new RunConfig { PlantHealthScale = 0.2f }.Hash());
            Assert.NotEqual(recorded, new RunConfig { DeathRowIncome = true }.Hash());
        }

        [Fact]
        public void AtOneTheHealthPoolIsTodaysValueForEveryCellType()
        {
            var config = new RunConfig { HealthPerCubicMetre = 7f };
            foreach (string id in AllCellTypes)
            {
                PhenotypePart part = PartOf(id, config);
                Assert.Equal(id, part.CellTypeId);
                Assert.True(Recorded(part, config) > 0f, id);
                Assert.Equal(Recorded(part, config), Metabolism.HealthPool(part, config));
            }
        }

        [Fact]
        public void AtTenANonPhotosyntheticPartsPoolIsTenTimesTodaysAndAPhotosyntheticPartsIsUnchanged()
        {
            var config = new RunConfig { HealthPerCubicMetre = 7f, AnimalHealthScale = 10f };
            int animals = 0;
            foreach (string id in AllCellTypes)
            {
                PhenotypePart part = PartOf(id, config);
                float recorded = Recorded(part, config);
                float pool = Metabolism.HealthPool(part, config);

                if (id == CellTypeIds.Photosynthetic)
                {
                    Assert.Equal(recorded, pool);
                }
                else
                {
                    animals++;
                    Assert.Equal(recorded * 10f, pool);
                }
            }

            Assert.Equal(AllCellTypes.Length - 1, animals);
        }

        [Fact]
        public void AtPlantScaleTwoTenthsALeafsPoolIsTwoTenthsOfTodaysAndAnAnimalPartsIsUnchanged()
        {
            var config = new RunConfig { HealthPerCubicMetre = 7f, PlantHealthScale = 0.2f };
            foreach (string id in AllCellTypes)
            {
                PhenotypePart part = PartOf(id, config);
                float recorded = Recorded(part, config);
                float pool = Metabolism.HealthPool(part, config);

                Assert.Equal(id == CellTypeIds.Photosynthetic ? recorded * 0.2f : recorded, pool);
            }
        }

        [Fact]
        public void BothScalesTogetherApplyEachToItsOwnKindOnly()
        {
            var config = new RunConfig { HealthPerCubicMetre = 7f, AnimalHealthScale = 10f, PlantHealthScale = 0.2f };
            foreach (string id in AllCellTypes)
            {
                PhenotypePart part = PartOf(id, config);
                float recorded = Recorded(part, config);
                float expected = id == CellTypeIds.Photosynthetic ? recorded * 0.2f : recorded * 10f;
                Assert.Equal(expected, Metabolism.HealthPool(part, config));
            }
        }

        [Fact]
        public void TheScaleGoesByTheBodySoAPlantsStalkTakesThePlantScale()
        {
            RunConfig config = new RunConfig { AnimalHealthScale = 10f, PlantHealthScale = 0.2f };
            PhenotypePart stalk = PartOf(CellTypeIds.Structural, config);
            PhenotypePart blade = PartOf(CellTypeIds.Photosynthetic, config);

            Assert.Equal(Recorded(stalk, config) * 0.2f, Metabolism.HealthPool(stalk, config, plantBody: true));
            Assert.Equal(Recorded(stalk, config) * 10f, Metabolism.HealthPool(stalk, config, plantBody: false));
            Assert.Equal(Recorded(blade, config) * 0.2f, Metabolism.HealthPool(blade, config, plantBody: true));

            Phenotype leafBody = Developer.Develop(Fixtures.ArmedBox(toughness: 1f, cellTypeId: CellTypeIds.Photosynthetic),
                config.Development, null, config.Shapes);
            Phenotype animalBody = Developer.Develop(Fixtures.ArmedBox(toughness: 1f, cellTypeId: CellTypeIds.Structural),
                config.Development, null, config.Shapes);
            Assert.True(Metabolism.IsPlant(leafBody));
            Assert.False(Metabolism.IsPlant(animalBody));
        }

        [Fact]
        public void ABiteOfOneStrengthWoundsALeafFiveTimesDeeperAndAnAnimalPartTenTimesShallowerWithTheScalesSet()
        {
            // The wound is the blow's damage as a share of the part's pool, so a pool ten times larger takes a tenth
            // of the share: the scale reaches the place a hit is priced, and not only the helper.
            (double Leaf, double Animal) Shares(float animalScale, float plantScale)
            {
                RunConfig config = ContactEatingTests.Stage(on: true);
                config.AnimalHealthScale = animalScale;
                config.PlantHealthScale = plantScale;
                var world = new World(config, seed: 3);
                world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
                world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake), 1, -1f);
                world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake), 1, -1f);
                Organism leaf = world.Living[0];

                world.SetContacts(ContactEatingTests.Touching(world.Living[1], 0, leaf, 1));
                world.ApplyMouth(0.05f);
                double leafShare = 1d - leaf.PartHealth[1];

                Organism prey = world.Living[2];
                world.SetContacts(ContactEatingTests.Touching(world.Living[1], 0, prey, 0));
                world.ApplyMouth(0.05f);
                double animalShare = 1d - prey.PartHealth[0];
                return (leafShare, animalShare);
            }

            var plain = Shares(1f, 1f);
            var scaled = Shares(10f, 0.2f);
            _output.WriteLine($"leaf {plain.Leaf:0.#####} -> {scaled.Leaf:0.#####}; animal {plain.Animal:0.#####} -> {scaled.Animal:0.#####}");

            Assert.True(plain.Leaf > 0d && scaled.Leaf < 1d && plain.Animal > 0d);
            ContactEatingTests.AssertRelative(5d * plain.Leaf, scaled.Leaf, 1e-3, "the leaf's share at plant scale 0.2");
            ContactEatingTests.AssertRelative(plain.Animal / 10d, scaled.Animal, 1e-3, "the animal part's share at animal scale 10");
        }

        // ------------------------------------------------------------------------ the death row

        private static RunConfig IncomeStage(bool gut, bool income)
        {
            RunConfig config = ContactEatingTests.RemainsStage();
            config.KillSwallowsBody = gut;
            config.DeathRowIncome = income;
            return config;
        }

        private sealed class Scene
        {
            public World World;
            public Organism Eater, Prey, Killer;
            public double KeptLeaves, KeptOthers, KeptRemains, Swallowed;
        }

        /// <summary>
        /// The eater kills a branching tree (whole, with the gut on, or cutting remains off, with it off), bites a living
        /// tree part or the remains, and bites a living animal body. Its income is then read off the world's totals, as it
        /// is the only body that has bitten anything yet.
        /// </summary>
        private static Scene Eat(bool gut, bool income)
        {
            RunConfig config = IncomeStage(gut, income);
            var world = new World(config, seed: 3);
            world.Inoculate(ContactEatingTests.Branching(), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake), 1, -1f);
            world.Inoculate(ContactEatingTests.Hunter(ContactEatingTests.ConsumerIntake), 1, -1f);
            world.Inoculate(ContactEatingTests.Leaf(2), 1, -1f);
            Organism tree = world.Living[0];
            Organism eater = world.Living[1];
            Organism prey = world.Living[2];
            Organism killer = world.Living[3];
            Organism leaf = world.Living[4];

            // A kill. With the gut off, part 1 of the tree is eaten whole and two limbs are left as remains.
            world.SetContacts(ContactEatingTests.Touching(eater, 0, tree, gut ? 0 : 1));
            world.ApplyMouth(100f);

            if (gut)
            {
                Assert.Equal(1L, world.KillsSwallowed);
                Assert.True(world.SwallowedJoules > 0d);
            }
            else
            {
                Assert.Equal(2, world.Remains.Count);
                world.SetContacts(ContactEatingTests.Touching(eater, 0, world.Remains[0], 0));
                world.ApplyMouth(0.3f);
            }

            // A living leaf and a living animal body, each wounded and not killed.
            world.SetContacts(ContactEatingTests.Touching(eater, 0, leaf, 1));
            world.ApplyMouth(0.1f);
            world.SetContacts(ContactEatingTests.Touching(eater, 0, prey, 0));
            world.ApplyMouth(0.1f);
            Assert.True(prey.PartHealth[0] > 0f && prey.PartHealth[0] < 1f, "the prey should be wounded and alive");

            return new Scene
            {
                World = world,
                Eater = eater,
                Prey = prey,
                Killer = killer,
                KeptLeaves = world.BiteKeptFromLeavesJoules,
                KeptOthers = world.BiteKeptFromOthersJoules,
                KeptRemains = world.BiteKeptFromRemainsJoules,
                Swallowed = world.SwallowedJoules,
            };
        }

        /// <summary>The killer bites the eater's root whole, and the eater's death row is the one that comes back.</summary>
        private static LineageEvent EatersDeathRow(Scene scene)
        {
            scene.World.DrainLineageEvents();
            scene.World.SetContacts(ContactEatingTests.Touching(scene.Killer, 0, scene.Eater, 0));
            scene.World.ApplyMouth(100f);

            Assert.DoesNotContain(scene.World.Living, c => c.Id == scene.Eater.Id);
            return scene.World.DrainLineageEvents().Single(e => e.Kind == LineageEventKind.Death && e.Id == scene.Eater.Id);
        }

        [Fact]
        public void WithTheSwitchOnADeathRowCarriesTheFourFieldsAndTheThreeBiteFieldsSumToWhatTheBodyWasCredited()
        {
            Scene scene = Eat(gut: false, income: true);
            double credited = scene.Eater.BiteKeptJoules;
            LineageEvent row = EatersDeathRow(scene);

            Assert.True(row.HasBiteIncome);
            Assert.True(row.BiteKeptLeavesJoules > 0d, "leaf income");
            Assert.True(row.BiteKeptOthersJoules > 0d, "animal income");
            Assert.True(row.BiteKeptRemainsJoules > 0d, "remains income");
            Assert.Equal(0d, row.GutSwallowedJoules);

            // The eater bites back in the step that kills it, so each field is at least the world's total before
            // that step, and exactly the eater's own account at its death.
            Assert.True(row.BiteKeptLeavesJoules >= scene.KeptLeaves * (1d - 1e-12), "kl at least the scene's leaf total");
            Assert.True(row.BiteKeptOthersJoules >= scene.KeptOthers * (1d - 1e-12), "ko at least the scene's others total");
            Assert.True(row.BiteKeptRemainsJoules >= scene.KeptRemains * (1d - 1e-12), "kr at least the scene's remains total");
            Assert.Equal(scene.Eater.BiteKeptFromLeavesJoules, row.BiteKeptLeavesJoules);
            Assert.Equal(scene.Eater.BiteKeptFromOthersJoules, row.BiteKeptOthersJoules);
            Assert.Equal(scene.Eater.BiteKeptFromRemainsJoules, row.BiteKeptRemainsJoules);

            // And the three are what the existing per-body count, the row's bk, was credited.
            ContactEatingTests.AssertRelative(
                row.BiteKeptJoules, row.BiteKeptLeavesJoules + row.BiteKeptOthersJoules + row.BiteKeptRemainsJoules, 1e-12,
                "kl + ko + kr against bk");
            Assert.True(row.BiteKeptJoules >= credited * (1d - 1e-12), "the row's bk at least the scene's");

            string json = row.ToJson();
            _output.WriteLine(json);
            foreach (string field in new[] { "\"kl\":", "\"ko\":", "\"kr\":", "\"kg\":" }) Assert.Contains(field, json);
        }

        [Fact]
        public void WithTheGutOnADeathRowsGutFieldIsTheJoulesItsKillsPutInItsGut()
        {
            Scene scene = Eat(gut: true, income: true);
            LineageEvent row = EatersDeathRow(scene);

            Assert.True(row.HasBiteIncome);
            Assert.True(scene.Swallowed > 0d);
            // The eater can still bite and swallow in the step that kills it, so the row is held to the eater's own
            // accounts at its death, and to at least what the scene saw before it.
            Assert.True(row.GutSwallowedJoules >= scene.Swallowed, "kg at least the scene's swallowed total");
            Assert.Equal(scene.Eater.GutSwallowedLifetimeJoules, row.GutSwallowedJoules);
            Assert.Equal(scene.Eater.BiteKeptFromLeavesJoules, row.BiteKeptLeavesJoules);
            Assert.Equal(scene.Eater.BiteKeptFromOthersJoules, row.BiteKeptOthersJoules);
            Assert.Equal(scene.Eater.BiteKeptFromRemainsJoules, row.BiteKeptRemainsJoules);
            ContactEatingTests.AssertRelative(
                scene.Eater.BiteKeptJoules, row.BiteKeptLeavesJoules + row.BiteKeptOthersJoules + row.BiteKeptRemainsJoules, 1e-12,
                "kl + ko + kr against bk");
        }

        [Fact]
        public void WithTheSwitchOffADeathRowHasNoneOfTheFourFieldsAndIsTheRowItAlwaysWas()
        {
            foreach (bool gut in new[] { false, true })
            {
                Scene scene = Eat(gut, income: false);
                Assert.True(scene.Eater.BiteKeptFromLeavesJoules > 0d, "the accumulator still counts, as an add");
                LineageEvent row = EatersDeathRow(scene);

                Assert.False(row.HasBiteIncome);
                Assert.Equal(0d, row.BiteKeptLeavesJoules);
                string json = row.ToJson();
                _output.WriteLine(json);
                foreach (string field in new[] { "\"kl\"", "\"ko\"", "\"kr\"", "\"kg\"" }) Assert.DoesNotContain(field, json);
                Assert.Contains("\"bk\":", json);
            }
        }

        [Fact]
        public void TheSwitchIsRefusedOnWithContactEatingOffAndNamedInTheMessage()
        {
            RunConfig refused = ContactEatingTests.Stage(on: false);
            refused.DeathRowIncome = true;
            ArgumentException error = Assert.Throws<ArgumentException>(() => new World(refused, seed: 3));
            Assert.Contains("DeathRowIncome", error.Message);

            Assert.True(new World(IncomeStage(gut: false, income: true), seed: 3).Config.DeathRowIncome);
        }

        [Fact]
        public void ACheckpointWithTheSwitchOnKeepsTheAccumulatorsAndTheQueuedRowAndTheResumeWritesTheSameDeathRow()
        {
            Scene scene = Eat(gut: false, income: true);
            World world = scene.World;

            // Taken with the body still alive, so the death row is made after the resume.
            byte[] state = ContactEatingTests.StateOf(world);
            World restored = ContactEatingTests.Restored(world.Config, 3, state);

            Organism before = scene.Eater;
            Organism after = restored.Living.Single(c => c.Id == before.Id);
            Assert.Equal(before.BiteKeptFromLeavesJoules, after.BiteKeptFromLeavesJoules);
            Assert.Equal(before.BiteKeptFromOthersJoules, after.BiteKeptFromOthersJoules);
            Assert.Equal(before.BiteKeptFromRemainsJoules, after.BiteKeptFromRemainsJoules);
            Assert.Equal(before.GutSwallowedLifetimeJoules, after.GutSwallowedLifetimeJoules);

            var restoredScene = new Scene
            {
                World = restored,
                Eater = after,
                Killer = restored.Living.Single(c => c.Id == scene.Killer.Id),
            };

            string original = EatersDeathRow(scene).ToJson();
            string resumed = EatersDeathRow(restoredScene).ToJson();
            Assert.Equal(original, resumed);
        }

        [Fact]
        public void ADeathRowStillQueuedAcrossACheckpointKeepsItsIncome()
        {
            Scene scene = Eat(gut: false, income: true);
            World world = scene.World;
            world.DrainLineageEvents();

            world.SetContacts(ContactEatingTests.Touching(scene.Killer, 0, scene.Eater, 0));
            world.ApplyMouth(100f);
            Assert.DoesNotContain(world.Living, c => c.Id == scene.Eater.Id);

            // The row is made and not yet drained: it goes through the state with the other queued rows.
            World restored = ContactEatingTests.Restored(world.Config, 3, ContactEatingTests.StateOf(world));
            LineageEvent wrote = world.DrainLineageEvents().Single(e => e.Kind == LineageEventKind.Death && e.Id == scene.Eater.Id);
            LineageEvent read = restored.DrainLineageEvents().Single(e => e.Kind == LineageEventKind.Death && e.Id == scene.Eater.Id);

            Assert.True(read.HasBiteIncome);
            Assert.True(read.BiteKeptLeavesJoules > 0d);
            Assert.Equal(wrote.ToJson(), read.ToJson());
        }

        [Fact]
        public void ACheckpointWithTheSwitchOffIsNotLongerForTheFields()
        {
            Scene off = Eat(gut: false, income: false);
            Scene on = Eat(gut: false, income: true);
            int offLength = ContactEatingTests.StateOf(off.World).Length;
            int onLength = ContactEatingTests.StateOf(on.World).Length;

            // Four doubles on each of the living and remains bodies, and nothing at all when off.
            int bodies = on.World.Living.Count + on.World.Remains.Count;
            Assert.Equal(offLength + bodies * 4 * sizeof(double), onLength);
        }
    }
}

using System;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The consumption pass shares a cell out as the demand pass promised, credits a creature
    /// only what the water gave, and charges no matter for a body that never exists. Each test
    /// is one of the Astra review's reproduced faults of 2026-09-07 (R1 twice, R2), turned
    /// into the property it broke.
    /// </summary>
    public class AllocationTests
    {
        private readonly ITestOutputHelper _output;

        public AllocationTests(ITestOutputHelper output) => _output = output;

        private static Genome Cube(string type, float half = 0.5f)
        {
            var g = new Genome
            {
                RootIndex = 0,
                Reproduction = new ReproductionTraits { BroodSize = 1, OffspringEndowment = 10000f },
            };
            g.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(half, half, half), CellTypeId = type,
                JointType = JointType.Fixed, RecursiveLimit = 1,
            });
            return g;
        }

        // One cubic metre of water, one layer, no floor spawns, no sinking or mixing, dark:
        // the only thing that moves is what the two feeders take.
        private static RunConfig OneCell() => new RunConfig
        {
            MinimumPopulation = 0, MaximumPopulation = 100, FounderEnergyJoules = 100f,
            WorldAreaSquareMetres = 1f, WorldDepthMetres = 1f, LightLayerMetres = 1f,
            NutrientSinkMetresPerSecond = 0f, NutrientMixingDiffusivity = 0f,
            Light = new LightModel(1e-6f, 12f),
        };

        private static CellTypeRegistry Registry(float clearance) => new CellTypeRegistry(
            new StructuralCell(), new LinkCell(), new NeuralCell(), new PhotosyntheticCell(),
            new AbsorptiveCell(clearanceRate: clearance), new ConsumerCell(), new BuoyancyCell());

        [Fact]
        public void IdenticalFeedersGetIdenticalMealsWhateverTheirOrder()
        {
            // Two identical stomachs, each wanting 10 J of a 10 J cell, get 5 J each. Before
            // availability was frozen for the pass they got 5 J and 1.25 J in admission order,
            // and 3.75 J stayed in the water with both still hungry.
            var config = OneCell();
            config.CellTypes = Registry(clearance: 2f);
            var world = new World(config);
            world.Inoculate(Cube(CellTypeIds.Absorptive), 2, -0.5f);
            world.Nutrients.Deposit(-0.5f, 10f);
            double auditBefore = world.AuditResidual;

            world.Step(0.5f);

            float[] meals = world.Living.Select(o => o.Lifetime.FoodIncome).ToArray();
            _output.WriteLine(
                $"meals {string.Join(", ", meals)} J, left {world.Nutrients.TotalJoules:R} J, " +
                $"short takes {world.PoolShortTakes}");

            Assert.Equal(2, meals.Length);
            Assert.Equal(meals[0], meals[1], 5);
            Assert.Equal(10.0, world.DetritusTakenTotal, 5);
            Assert.Equal(0.0, world.Nutrients.TotalJoules, 5);
            Assert.Equal(0L, world.PoolShortTakes);
            Assert.Equal(auditBefore, world.AuditResidual, 5);
        }

        [Fact]
        public void ACappedMouthIsCreditedOnlyWhatTheWaterGave()
        {
            // High clearance under the satiation cap (D062): each stomach demands 5 J of a 6 J
            // cell. Scaling the density does not ration a saturating mouth, which re-saturates
            // and asks for 5 J again; the allowance caps each at its 3 J share, the cell is
            // drawn to exactly zero, and the audit does not move. Before the fix both were
            // credited 5 J while the water lost 6, and the audit opened by 4 J.
            var config = OneCell();
            config.CellTypes = Registry(clearance: 100f);
            config.SatiationWattsPerCubicMetre = 10f;
            var world = new World(config);
            world.Inoculate(Cube(CellTypeIds.Absorptive), 2, -0.5f);
            world.Nutrients.Deposit(-0.5f, 6f);
            double auditBefore = world.AuditResidual;

            world.Step(0.5f);

            float[] draws = world.Living.Select(o => o.Lifetime.PoolDrawn).ToArray();
            _output.WriteLine(
                $"draws {string.Join(", ", draws)} J, taken {world.DetritusTakenTotal:R} J, " +
                $"left {world.Nutrients.TotalJoules:R} J, audit moved {world.AuditResidual - auditBefore:R}");

            Assert.Equal(draws[0], draws[1], 5);
            Assert.Equal(6.0, draws.Sum(), 5);
            Assert.Equal(6.0, world.DetritusTakenTotal, 5);
            Assert.Equal(0L, world.PoolShortTakes);
            Assert.Equal(auditBefore, world.AuditResidual, 5);
        }

        [Fact]
        public void AStillbirthLocksNoMatterInABodyThatDoesNotExist()
        {
            // A root just above the minimum volume, one scalar mutation, and a child that
            // develops into nothing. D065's fixed term was charged for it before Admit refused
            // it, and stayed in MatterInBodies forever with no death to return it, invisible to
            // StandingMatter because that total adds the same counter.
            World world = null;
            for (ulong seed = 1; seed <= 200 && world == null; seed++)
            {
                var config = OneCell();
                config.FounderEnergyJoules = 1000f;
                config.InitialMatterPerCubicMetre = 100f;
                config.MatterPerCreature = 2f;
                config.MatterPerTissueJoule = 0f;
                foreach (var prop in typeof(MutationRates).GetProperties())
                {
                    if (prop.PropertyType == typeof(float) && prop.Name.EndsWith("Chance"))
                        prop.SetValue(config.Mutation, 0f);
                }
                config.Mutation.ScalarChance = 1f;

                Genome g = Cube(CellTypeIds.Photosynthetic, 0.0234f);
                g.Reproduction = new ReproductionTraits { BroodSize = 1, OffspringEndowment = 10f };

                var candidate = new World(config, seed);
                candidate.Inoculate(g, 1, -0.5f);
                candidate.Step(0.5f);
                if (candidate.Stillbirths > 0) world = candidate;
            }

            Assert.NotNull(world);
            _output.WriteLine(
                $"stillbirths {world.Stillbirths}, births {world.Births}, living {world.Living.Count}, " +
                $"free {world.Matter.TotalJoules:R}, in bodies {world.MatterInBodies:R}, " +
                $"held by the living {world.MatterInLivingBodies:R}, standing {world.StandingMatter:R}");

            Assert.Equal(world.MatterInLivingBodies, world.MatterInBodies, 9);
            Assert.Equal(world.MatterInitialTotal - world.MatterInLivingBodies, world.Matter.TotalJoules, 9);
            Assert.Equal(world.MatterInitialTotal, world.StandingMatter, 9);
        }
    }
}

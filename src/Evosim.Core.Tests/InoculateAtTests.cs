using System;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="World.InoculateAt"/> and <see cref="World.LaySpentMatter"/>, the leaf tank's two doors
    /// (logbook/specs/nursery-leaf-tank-plan.md, step 2): a body planted at a named point or refused
    /// with its reason and the world untouched, and spent matter laid through the water with both
    /// books kept.
    /// </summary>
    public class InoculateAtTests
    {
        private static RunConfig Tank(float budget = 100f)
        {
            var config = new RunConfig
            {
                Light = new LightModel(1e-6f, 1f),
                MinimumPopulation = 0,
                WorldDepthMetres = 10f,
                RemineralisationPerSecond = 0f,
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 100f,
                HorizontalPatches = 1,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = 0.02f,
                MatterMixingDiffusivity = 0.02f,
                MatterBudgetUnits = budget,
            };
            if (!(budget > 0f)) config.InitialMatterPerCubicMetre = 0f;
            return config;
        }

        private static Genome Cube(string cellType = CellTypeIds.Structural)
        {
            var g = new Genome();
            g.Nodes.Add(new MorphNode
            {
                CellTypeId = cellType,
                ShapeId = ShapeIds.Box,
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
                Neurons = Array.Empty<NeuronDef>(),
            });
            g.RootIndex = 0;
            return g;
        }

        /// <summary>A placer that says yes or no to a point and remembers what it was asked; the default members for everything else.</summary>
        private sealed class PointPlacement : IBodyPlacement
        {
            public bool Room = true;
            public Float3 Asked;
            public long Committed = -1, Released;

            public bool TryReserveAt(Phenotype adult, Float3 point, out int patch, out string refusal)
            {
                Asked = point;
                patch = 0;
                refusal = Room ? null : "the spot is crowded";
                return Room;
            }

            public bool TryReserveOffspring(Organism parent, Phenotype adult, ref float heightY, out int patch) { patch = 0; return false; }
            public bool TryReserveFounder(Phenotype adult, ref float heightY, out int patch) { patch = 0; return false; }
            public int PatchOf(Organism creature) => creature.Patch;
            public void Commit(long creatureId) => Committed = creatureId;
            public void Release() => Released++;
        }

        /// <summary>A placer that does not implement the point door: the interface's default refuses.</summary>
        private sealed class OldPlacement : IBodyPlacement
        {
            public bool TryReserveOffspring(Organism parent, Phenotype adult, ref float heightY, out int patch) { patch = 0; return false; }
            public bool TryReserveFounder(Phenotype adult, ref float heightY, out int patch) { patch = 0; return false; }
            public int PatchOf(Organism creature) => creature.Patch;
            public void Commit(long creatureId) { }
            public void Release() { }
        }

        [Fact]
        public void ABodyIsPlantedAtThePointAndBookedAsAnInoculant()
        {
            var world = new World(Tank(), seed: 1);
            float r = world.TankRadiusMetres;
            double inBefore = world.EnergyIn, matterBefore = world.MatterInfluxedTotal;

            Organism o = world.InoculateAt(Cube(), new Float3(r + 1.5f, -4f, r - 2f));

            Assert.Single(world.Living);
            Assert.Same(o, world.Living[0]);
            Assert.Equal(r + 1.5f, o.X);
            Assert.Equal(-4f, o.HeightY);
            Assert.Equal(r - 2f, o.Z);
            Assert.Equal(1, world.Inoculated);
            Assert.Equal(o.Energy + o.TissueJoules, world.EnergyIn - inBefore, 9);
            Assert.Equal((o.Energy + o.TissueJoules) / world.Config.JoulesPerUnit, world.MatterInfluxedTotal - matterBefore, 9);

            for (int i = 0; i < 40; i++) world.Step(0.5f);
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"matter residual {world.MatterResidual:R}");
        }

        [Theory]
        [InlineData(3f, -4f, 1f, "outside the tank's glass")]       // x past the glass (in multiples of R, below)
        [InlineData(1f, 0.5f, 1f, "not in the water")]              // above the surface
        [InlineData(1f, -10.5f, 1f, "not in the water")]            // below the bed
        [InlineData(float.NaN, -4f, 1f, "not in the water")]
        public void APointOutsideTheWaterIsRefusedAndTheWorldIsUntouched(float xInRadii, float y, float zInRadii, string said)
        {
            var world = new World(Tank(), seed: 1);
            float r = world.TankRadiusMetres;
            double inBefore = world.EnergyIn;

            var e = Assert.Throws<ArgumentOutOfRangeException>(() => world.InoculateAt(Cube(), new Float3(xInRadii * r, y, zInRadii * r)));

            Assert.Contains(said, e.Message);
            Assert.Empty(world.Living);
            Assert.Equal(0, world.Inoculated);
            Assert.Equal(inBefore, world.EnergyIn);
        }

        [Fact]
        public void ThePlacerIsAskedForThePointAloneAndItsRefusalIsTheWorlds()
        {
            var world = new World(Tank(), seed: 1);
            float r = world.TankRadiusMetres;
            var placer = new PointPlacement { Room = false };
            world.Placement = placer;
            double inBefore = world.EnergyIn;
            var point = new Float3(r, -5f, r);

            var e = Assert.Throws<ArgumentException>(() => world.InoculateAt(Cube(), point));
            Assert.Contains("the spot is crowded", e.Message);
            Assert.Equal(point, placer.Asked);
            Assert.Empty(world.Living);
            Assert.Equal(0, world.Inoculated);
            Assert.Equal(inBefore, world.EnergyIn);
            Assert.Equal(-1, placer.Committed);

            placer.Room = true;
            Organism o = world.InoculateAt(Cube(), point);
            Assert.Equal(o.Id, placer.Committed);
            Assert.Equal(-5f, o.HeightY);
            Assert.Equal(0, placer.Released);
        }

        [Fact]
        public void APlacerThatKeepsNoCoordinatesRefusesAndATiledWorldHasNone()
        {
            var world = new World(Tank(), seed: 1);
            world.Placement = new OldPlacement();
            float r = world.TankRadiusMetres;
            var e = Assert.Throws<ArgumentException>(() => world.InoculateAt(Cube(), new Float3(r, -5f, r)));
            Assert.Contains("keeps no coordinates", e.Message);
            Assert.Empty(world.Living);

            RunConfig tiled = new RunConfig { MinimumPopulation = 0, WorldDepthMetres = 10f };
            var flat = new World(tiled, seed: 1);
            Assert.Throws<InvalidOperationException>(() => flat.InoculateAt(Cube(), new Float3(1f, -5f, 1f)));
        }

        [Fact]
        public void SpentMatterIsLaidThroughEveryLiveCellAndBookedAsInflux()
        {
            var world = new World(Tank(), seed: 1);
            var grid = (GridField)world.Matter;
            var before = new double[grid.CellsX, grid.CellsY, grid.CellsZ];
            for (int ix = 0; ix < grid.CellsX; ix++)
                for (int iy = 0; iy < grid.CellsY; iy++)
                    for (int iz = 0; iz < grid.CellsZ; iz++)
                        before[ix, iy, iz] = grid.JoulesAt(ix, iy, iz);
            double inBefore = world.EnergyIn, influxBefore = world.MatterInfluxedTotal, heldBefore = grid.TotalJoules;

            double laid = world.LaySpentMatter(0.25f);

            Assert.Equal(0.25 * grid.LiveVolumeCubicMetres, laid, 6);
            Assert.Equal(laid, grid.TotalJoules - heldBefore, 9);
            Assert.Equal(laid, world.MatterInfluxedTotal - influxBefore, 12);
            Assert.Equal(inBefore, world.EnergyIn);
            int live = 0;
            for (int ix = 0; ix < grid.CellsX; ix++)
                for (int iy = 0; iy < grid.CellsY; iy++)
                    for (int iz = 0; iz < grid.CellsZ; iz++)
                    {
                        double added = grid.JoulesAt(ix, iy, iz) - before[ix, iy, iz];
                        if (grid.IsLive(ix, iy, iz)) { live++; Assert.Equal(0.25 * grid.CellVolume, added, 9); }
                        else Assert.Equal(0d, added);
                    }
            Assert.Equal(grid.LiveCellCount, live);
            Assert.True(Math.Abs(world.MatterResidual) <= 1e-9 * Math.Max(1d, world.MatterInitialTotal), $"matter residual {world.MatterResidual:R}");

            for (int i = 0; i < 200; i++) world.Step(0.5f);
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal + laid),
                $"matter residual {world.MatterResidual:R}");
        }

        [Fact]
        public void WaterThatHeldNoMatterHoldsTheDensityAsked()
        {
            var world = new World(Tank(budget: 0f), seed: 1);
            var grid = (GridField)world.Matter;
            Assert.Equal(0d, grid.TotalJoules);

            world.LaySpentMatter(0.4f);

            for (int ix = 0; ix < grid.CellsX; ix++)
                for (int iy = 0; iy < grid.CellsY; iy++)
                    for (int iz = 0; iz < grid.CellsZ; iz++)
                        if (grid.IsLive(ix, iy, iz)) Assert.Equal((double)0.4f, grid.JoulesAt(ix, iy, iz) / grid.CellVolume, 6);
            Assert.Equal((double)0.4f, grid.TotalJoules / grid.LiveVolumeCubicMetres, 9);
        }

        [Fact]
        public void NoSpentMatterIsLaidAtZeroANegativeDensityIsRefusedAndAWorldOffAGridRefuses()
        {
            var world = new World(Tank(), seed: 1);
            double before = world.MatterInfluxedTotal;
            Assert.Equal(0d, world.LaySpentMatter(0f));
            Assert.Equal(before, world.MatterInfluxedTotal);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.LaySpentMatter(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.LaySpentMatter(float.PositiveInfinity));

            var cells = new World(new RunConfig { MinimumPopulation = 0, WorldDepthMetres = 10f }, seed: 1);
            Assert.Throws<InvalidOperationException>(() => cells.LaySpentMatter(1f));
        }
    }
}

using System;
using Evosim.Core;
using Evosim.Dynamics.Placement;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// <see cref="SharedVolume.TryReserveAt"/> through <see cref="World.InoculateAt"/>, wired as <c>Evosim.Farm.Simulation</c> wires the
    /// placer (logbook/specs/nursery-leaf-tank-plan.md, step 2): a body is promised exactly the point it was given, and a point on another
    /// body, in the bed or at the glass is refused, never moved.
    /// </summary>
    public sealed class ReserveAtTests
    {
        private static (World World, SharedVolume Volume) Tank()
        {
            var config = new RunConfig
            {
                Light = new LightModel(1e-6f, 1f),
                MinimumPopulation = 0,
                WorldDepthMetres = 10f,
                SharedSpace = true,
                WorldShape = WorldShape.Tank,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 100f,
                HorizontalPatches = 1,
                FieldCellMetres = 1f,
                FieldMatterCellMetres = 2f,
                MatterBudgetUnits = 100f,
            };
            var world = new World(config, 1UL);
            var volume = new SharedVolume(
                1, world.Nutrients.PatchWidthMetres, config.WorldDepthMetres, 1UL, config.OffspringDispersalMetres,
                1, config.WorldShape, world.TankRadiusMetres);
            world.Placement = volume;
            volume.Floor = new PlacementFloor(config.WorldDepthMetres, world.Bed, world.Reefs);
            return (world, volume);
        }

        private static Genome Cube()
        {
            var g = new Genome();
            g.Nodes.Add(new MorphNode
            {
                CellTypeId = CellTypeIds.Structural,
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

        [Fact]
        public void ABodyIsPromisedThePointAndAnotherBodyThereIsRefused()
        {
            (World world, SharedVolume volume) = Tank();
            float r = world.TankRadiusMetres;
            var point = new Float3(r + 1f, -5f, r - 1f);

            Organism o = world.InoculateAt(Cube(), point);
            Assert.True(volume.TryTakePlacement(o.Id, out Float3 at));
            Assert.Equal(point, at);

            var e = Assert.Throws<ArgumentException>(() => world.InoculateAt(Cube(), new Float3(r + 1.1f, -5f, r - 1f)));
            Assert.Contains("overlap", e.Message);
            Assert.Single(world.Living);

            // Clear of the first, it is placed.
            Organism next = world.InoculateAt(Cube(), new Float3(r - 2f, -5f, r));
            Assert.Equal(2, world.Living.Count);
            Assert.True(volume.TryTakePlacement(next.Id, out Float3 there));
            Assert.Equal(new Float3(r - 2f, -5f, r), there);
        }

        [Fact]
        public void APointInTheBedOrAtTheGlassIsRefusedAndNotMoved()
        {
            (World world, SharedVolume volume) = Tank();
            float r = world.TankRadiusMetres;

            var bed = Assert.Throws<ArgumentException>(() => world.InoculateAt(Cube(), new Float3(r, -9.99f, r)));
            Assert.Contains("in the bed", bed.Message);

            var glass = Assert.Throws<ArgumentException>(() => world.InoculateAt(Cube(), new Float3(2f * r - 0.05f, -5f, r)));
            Assert.Contains("past the glass", glass.Message);

            Assert.Empty(world.Living);
            Assert.Equal(0, world.Inoculated);
            Assert.Equal(0, volume.Occupants);
        }
    }
}

using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics.Placement;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The round 48 founding ruling's depth (owner, 2026-09-24,
    /// <see cref="RunConfig.FoundersFollowFoodDepth"/>): a founder accepted in a column is set at
    /// the richest cell of its food there, through the farm's own placer.
    /// </summary>
    /// <remarks>
    /// A grid box 24 × 6 × 24 m (1 m snow cells, 3 m matter cells) with its food in one 3 × 3 m
    /// region: snow in the 1 m layer at 10 to 11 m, with a tenth as much at 2 to 3 m, and
    /// dissolved matter in the 3 m layer at 6 to 9 m, with a tenth as much at 0 to 3 m. The world
    /// and the placer are wired as <c>Evosim.Farm.Simulation</c> wires them, flat bed included.
    /// </remarks>
    public sealed class FounderDepthPlacementTests
    {
        private readonly ITestOutputHelper _output;

        public FounderDepthPlacementTests(ITestOutputHelper output) => _output = output;

        private const float Drawn = -2f;
        private const int Copies = 8;

        private static RunConfig GridBox(bool atDepth) => new RunConfig
        {
            Light = new LightModel(100f, 12f),
            SharedSpace = true,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = 144f,
            HorizontalPatches = 4,
            WorldDepthMetres = 24f,
            NutrientMixingDiffusivity = 0.2f,
            HorizontalMixingDiffusivity = 0.2f,
            MatterMixingDiffusivity = 0.2f,
            InitialMatterPerCubicMetre = 0f,
            MinimumPopulation = 0,
            MaximumPopulation = 100_000,
            FoundersFollowFood = true,
            FoundersFollowFoodDepth = atDepth,
        };

        private static (World World, SharedVolume Volume) Build(bool atDepth, ulong seed)
        {
            RunConfig config = GridBox(atDepth);
            var world = new World(config, seed);

            var volume = new SharedVolume(
                Math.Max(1, (int)config.HorizontalPatches), world.Nutrients.PatchWidthMetres,
                config.WorldDepthMetres, seed, config.OffspringDispersalMetres,
                Math.Max(1, (int)config.PatchesAcross), config.WorldShape, world.TankRadiusMetres);

            world.Placement = volume;
            volume.Floor = new PlacementFloor(config.WorldDepthMetres, world.Bed, world.Reefs);

            for (int ix = 0; ix < 3; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    world.Nutrients.Deposit(new FieldPoint(new Float3(ix + 0.5f, -10.5f, iz + 0.5f), 0), 10f);
                    world.Nutrients.Deposit(new FieldPoint(new Float3(ix + 0.5f, -2.5f, iz + 0.5f), 0), 1f);
                }
            }

            world.Matter.Deposit(new FieldPoint(new Float3(1.5f, -7.5f, 1.5f), 0), 10f);
            world.Matter.Deposit(new FieldPoint(new Float3(1.5f, -1.5f, 1.5f), 0), 1f);

            return (world, volume);
        }

        private static Genome Cube(string cellType)
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

        /// <summary>Plants <see cref="Copies"/> of a body and returns where each landed.</summary>
        private List<Float3> Plant(string cellType, bool atDepth, ulong seed)
        {
            (World world, SharedVolume volume) = Build(atDepth, seed);

            world.Inoculate(Cube(cellType), Copies, heightY: Drawn);
            Assert.Equal(Copies, world.Living.Count);

            var landed = new List<Float3>();
            foreach (Organism o in world.Living)
            {
                Assert.True(volume.TryTakePlacement(o.Id, out Float3 at), $"no placement for {o.Id}");

                // The height the world admitted the body at is the one it was placed at.
                Assert.Equal(at.Y, o.HeightY);
                landed.Add(at);
            }

            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Float3 p in landed) { lo = Math.Min(lo, p.Y); hi = Math.Max(hi, p.Y); }
            _output.WriteLine($"{cellType}, depth rule {(atDepth ? "on" : "off")}: heights {lo:0.###} to {hi:0.###} m");

            return landed;
        }

        [Fact]
        public void AStomachLandsInTheRichestSnowLayer()
        {
            foreach (Float3 p in Plant(CellTypeIds.Absorptive, atDepth: true, seed: 41UL))
            {
                Assert.True(p.X < 3f && p.Z < 3f, $"outside the snow region at ({p.X}, {p.Z})");
                Assert.InRange(p.Y, -11f, -10f);
            }
        }

        [Fact]
        public void ALeafLandsInTheRichestMatterLayer()
        {
            foreach (Float3 p in Plant(CellTypeIds.Photosynthetic, atDepth: true, seed: 42UL))
            {
                Assert.True(p.X < 3f && p.Z < 3f, $"outside the matter region at ({p.X}, {p.Z})");
                Assert.InRange(p.Y, -9f, -6f);
            }
        }

        [Fact]
        public void AMixotrophTakesTheFieldWithTheLargerShareTheSnowOnATie()
        {
            // Both fields hold all their stock in the one region, so both shares are 1 there and
            // the tie goes to the snow, as D116's acceptance reads it.
            (World world, SharedVolume volume) = Build(atDepth: true, seed: 46UL);

            Genome g = Cube(CellTypeIds.Absorptive);
            g.Nodes.Add(Cube(CellTypeIds.Photosynthetic).Nodes[0]);
            g.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            world.Inoculate(g, 4, heightY: Drawn);
            Assert.Equal(4, world.Living.Count);

            foreach (Organism o in world.Living)
            {
                Assert.True(volume.TryTakePlacement(o.Id, out Float3 at));
                Assert.InRange(at.Y, -11f, -10f);
            }
        }

        [Fact]
        public void WithTheRuleOffEveryFounderKeepsTheDrawnDepth()
        {
            foreach (Float3 p in Plant(CellTypeIds.Absorptive, atDepth: false, seed: 43UL))
            {
                Assert.True(p.X < 3f && p.Z < 3f, $"D116 still chooses the column: ({p.X}, {p.Z})");
                Assert.Equal(Drawn, p.Y);
            }
        }

        [Fact]
        public void ABodyThatEatsNothingKeepsTheDrawnDepth()
        {
            foreach (Float3 p in Plant(CellTypeIds.Structural, atDepth: true, seed: 44UL))
            {
                Assert.Equal(Drawn, p.Y);
            }
        }

        [Fact]
        public void WithTheRuleOffThePlacerIsHandedNoDepth()
        {
            (World world, SharedVolume volume) = Build(atDepth: false, seed: 45UL);
            world.Inoculate(Cube(CellTypeIds.Absorptive), 0, heightY: Drawn);

            Assert.NotNull(volume.FounderAcceptance);
            Assert.Null(volume.FounderDepth);
        }

        [Fact]
        public void TheDepthRuleWithoutD116IsRefused()
        {
            RunConfig config = GridBox(atDepth: true);
            config.FoundersFollowFood = false;

            Assert.Throws<ArgumentException>(() => new World(config, 1UL));
        }
    }
}

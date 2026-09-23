using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D116, founders follow their food — <c>logbook/specs/founding-trickle-spec.md</c> §2 and
    /// §3's test 4.
    /// </summary>
    /// <remarks>
    /// The world here is a grid box 24 × 6 × 24 m with two marked regions of 3 × 3 m, each the
    /// footprint of one 3 m matter column and nine 1 m snow columns: region A holds ten times the
    /// snow of region B, and B ten times the dissolved matter of A, with nothing anywhere else.
    /// A placer that draws candidates uniformly over the box and keeps one with the world's
    /// acceptance, as <c>SharedVolume</c> does, then plants a stomach ten to one in A, a leaf ten
    /// to one in B and a mixotroph evenly in both.
    /// </remarks>
    public class FoundersFollowFoodTests
    {
        private readonly ITestOutputHelper _output;

        public FoundersFollowFoodTests(ITestOutputHelper output) => _output = output;

        private const float Length = 24f;
        private const float Width = 6f;
        private const int Draws = 1_000;

        /// <summary>
        /// Draws a spot uniformly over the box and keeps it with the acceptance's probability,
        /// the loop <c>SharedVolume.TryReserveFounder</c> runs with its own stream and without
        /// the crowd, and records where each founder landed.
        /// </summary>
        private sealed class SamplingPlacement : IBodyPlacement
        {
            private readonly Rng _rng;

            public SamplingPlacement(ulong seed) => _rng = new Rng(seed);

            public Func<Phenotype, float, float, float> FounderAcceptance { get; set; }

            public readonly List<(float X, float Z)> Planted = new List<(float X, float Z)>();

            public bool TryReserveOffspring(Organism parent, Phenotype adult, ref float heightY, out int patch)
            {
                patch = 0;
                return true;
            }

            public bool TryReserveFounder(Phenotype body, ref float heightY, out int patch)
            {
                patch = 0;
                Func<Phenotype, float, float, float> accept = FounderAcceptance;

                for (int attempt = 0; attempt < 100_000; attempt++)
                {
                    float x = _rng.Range(0f, Length);
                    float z = _rng.Range(0f, Width);

                    if (accept != null)
                    {
                        float p = accept(body, x, z);
                        if (!(p > 0f) || _rng.NextFloat() >= p) continue;
                    }

                    Planted.Add((x, z));
                    return true;
                }

                return false;
            }

            public int PatchOf(Organism creature) => creature.Patch;

            public void Commit(long creatureId) { }

            public void Release() { }
        }

        private static RunConfig GridBox(bool followFood, bool followMatter) => new RunConfig
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
            FoundersFollowFood = followFood,
            FoundersFollowMatter = followMatter,
        };

        /// <summary>The snow in one region and the dissolved matter in the other, as the remarks say.</summary>
        private static void Stock(World world)
        {
            for (int ix = 0; ix < 3; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    world.Nutrients.Deposit(new FieldPoint(new Float3(ix + 0.5f, -0.5f, iz + 0.5f), 0), 10f);
                    world.Nutrients.Deposit(new FieldPoint(new Float3(12f + ix + 0.5f, -0.5f, 3f + iz + 0.5f), 0), 1f);
                }
            }

            world.Matter.Deposit(new FieldPoint(new Float3(1.5f, -1.5f, 1.5f), 0), 1f);
            world.Matter.Deposit(new FieldPoint(new Float3(13.5f, -1.5f, 4.5f), 0), 10f);
        }

        private static bool InA(float x, float z) => x < 3f && z < 3f;
        private static bool InB(float x, float z) => x >= 12f && x < 15f && z >= 3f;

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

        /// <summary>An absorptive box with a photosynthetic box on its +X face.</summary>
        private static Genome Mixotroph()
        {
            Genome g = Cube(CellTypeIds.Absorptive);
            g.Nodes.Add(Cube(CellTypeIds.Photosynthetic).Nodes[0]);
            g.Nodes[0].Edges.Add(Fixtures.FaceToFace(1));
            return g;
        }

        private (int A, int B, int Elsewhere) Plant(Genome genome, ulong seed)
        {
            var placer = new SamplingPlacement(seed);
            var world = new World(GridBox(followFood: true, followMatter: false), seed) { Placement = placer };
            Stock(world);

            world.Inoculate(genome, Draws, heightY: -2f);
            Assert.Equal(Draws, placer.Planted.Count);

            int a = 0, b = 0, elsewhere = 0;
            foreach ((float x, float z) in placer.Planted)
            {
                if (InA(x, z)) a++;
                else if (InB(x, z)) b++;
                else elsewhere++;
            }

            return (a, b, elsewhere);
        }

        [Fact]
        public void AStomachLandsOnTheSnowALeafOnTheMatterAndAMixotrophOnBoth()
        {
            (int A, int B, int Elsewhere) stomach = Plant(Cube(CellTypeIds.Absorptive), 21UL);
            (int A, int B, int Elsewhere) leaf = Plant(Cube(CellTypeIds.Photosynthetic), 22UL);
            (int A, int B, int Elsewhere) mixed = Plant(Mixotroph(), 23UL);

            _output.WriteLine($"stomach: A (snow) {stomach.A}, B (matter) {stomach.B}, elsewhere {stomach.Elsewhere}");
            _output.WriteLine($"leaf:    A (snow) {leaf.A}, B (matter) {leaf.B}, elsewhere {leaf.Elsewhere}");
            _output.WriteLine($"mixed:   A (snow) {mixed.A}, B (matter) {mixed.B}, elsewhere {mixed.Elsewhere}");

            // Nothing lands where neither field holds anything.
            Assert.Equal(0, stomach.Elsewhere);
            Assert.Equal(0, leaf.Elsewhere);
            Assert.Equal(0, mixed.Elsewhere);

            // The stomach and the leaf, each over 5:1 toward its own food (10:1 expected).
            Assert.True(stomach.A > 5 * stomach.B, $"stomach {stomach.A}:{stomach.B}");
            Assert.True(leaf.B > 5 * leaf.A, $"leaf {leaf.B}:{leaf.A}");

            // The mixotroph takes the larger share in both places, 1 in each: an even split.
            Assert.InRange(mixed.A, 400, 600);
            Assert.InRange(mixed.B, 400, 600);
        }

        [Fact]
        public void ABodyThatEatsNeitherLandsAnywhere()
        {
            (int A, int B, int Elsewhere) bare = Plant(Cube(CellTypeIds.Structural), 24UL);

            _output.WriteLine($"bare tissue: A {bare.A}, B {bare.B}, elsewhere {bare.Elsewhere}");

            // Two 9 m² regions of a 144 m² box: about one draw in eight in either.
            Assert.True(bare.Elsewhere > 750, $"elsewhere {bare.Elsewhere}");
        }

        /// <summary>
        /// With <see cref="RunConfig.FoundersFollowMatter"/> on and D116 off, the acceptance is
        /// D109's arithmetic to the bit, for every kind of body.
        /// </summary>
        [Fact]
        public void UnderD109TheAcceptanceIsD109sForEveryBody()
        {
            var placer = new SamplingPlacement(31UL);
            var world = new World(GridBox(followFood: false, followMatter: true), 31UL) { Placement = placer };
            Stock(world);

            // Inoculating nobody hands the placer the world's rule and does nothing else.
            world.Inoculate(Cube(CellTypeIds.Absorptive), 0, heightY: -2f);
            Assert.NotNull(placer.FounderAcceptance);

            var matter = (GridField)world.Matter;
            Phenotype[] bodies =
            {
                null,
                Developer.Develop(Cube(CellTypeIds.Absorptive), world.Config.Development, null, world.Config.Shapes),
                Developer.Develop(Cube(CellTypeIds.Photosynthetic), world.Config.Development, null, world.Config.Shapes),
                Developer.Develop(Mixotroph(), world.Config.Development, null, world.Config.Shapes),
            };

            int compared = 0;
            for (float x = 0.25f; x < Length; x += 0.5f)
            {
                for (float z = 0.25f; z < Width; z += 0.5f)
                {
                    // D109's delegate as it was written, before the body was an argument.
                    double max = matter.MaxColumnStock();
                    float d109 = max > 0d ? (float)(matter.ColumnStockAt(x, z) / max) : 1f;

                    foreach (Phenotype body in bodies)
                    {
                        Assert.Equal(d109, placer.FounderAcceptance(body, x, z));
                        compared++;
                    }
                }
            }

            _output.WriteLine($"{compared} acceptances compared with D109's arithmetic");
        }

        [Fact]
        public void BothFounderRulesAtOnceAreRefused()
        {
            Assert.Throws<ArgumentException>(
                () => new World(GridBox(followFood: true, followMatter: true), 1UL));
        }

        [Fact]
        public void FollowingFoodWithoutAGridIsRefused()
        {
            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                FoundersFollowFood = true,
            };

            Assert.Throws<ArgumentException>(() => new World(config, 1UL));
        }
    }
}

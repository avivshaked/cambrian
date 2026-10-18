using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D129, a founder with a mouth follows the corpses: <see cref="RunConfig.FoundersFollowCorpses"/>.
    /// </summary>
    /// <remarks>
    /// The world is <see cref="FoundersFollowFoodTests"/>' grid box, 24 × 6 × 24 m with 1 m snow
    /// columns, with corpses as objects and a mouth's reach. Corpses are made the way a run makes
    /// them: bare boxes are inoculated, set where the test wants them by <c>Observe</c>, and killed.
    /// Column A (x 1 to 2, z 1 to 2) then holds ten corpses and column B (x 13 to 14, z 4 to 5)
    /// one, all worth the same, and a placer drawing uniformly over the box keeps each spot with
    /// the world's acceptance, as <c>SharedVolume</c> does.
    /// </remarks>
    public class FoundersFollowCorpsesTests
    {
        private readonly ITestOutputHelper _output;

        public FoundersFollowCorpsesTests(ITestOutputHelper output) => _output = output;

        private const float Length = 24f;
        private const float Width = 6f;
        private const int Draws = 1_000;
        private const float Reach = 0.5f;

        private sealed class SamplingPlacement : IBodyPlacement
        {
            private readonly Rng _rng;

            public SamplingPlacement(ulong seed) => _rng = new Rng(seed);

            public Func<Phenotype, float, float, float> FounderAcceptance { get; set; }

            public Func<Phenotype, float, float, (float Top, float Bottom)?> FounderDepth { get; set; }

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

        private static RunConfig GridBox(bool followCorpses, bool depth = false) => new RunConfig
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
            CorpseDecayPerSecond = 0.001f,
            IntakeReachMetres = Reach,
            FoundersFollowFood = true,
            FoundersFollowFoodDepth = depth,
            FoundersFollowCorpses = followCorpses,
        };

        private static Genome Mouth() => Fixtures.ArmedBox(intake: 1f, half: 0.2f, cellTypeId: CellTypeIds.Consumer);

        private static Genome Bare(float half = 0.2f) => Fixtures.ArmedBox(half: half);

        /// <summary>An absorptive box with a mouth on its +X face.</summary>
        private static Genome StomachAndMouth()
        {
            Genome g = Fixtures.ArmedBox(half: 0.2f, cellTypeId: CellTypeIds.Absorptive);
            g.Nodes.Add(Mouth().Nodes[0]);
            g.Nodes[0].Edges.Add(Fixtures.FaceToFace(1));
            return g;
        }

        /// <summary>Leaves a corpse of a bare box at each place, in order.</summary>
        private static void Corpses(World world, Genome genome, params Float3[] places)
        {
            var before = new HashSet<long>(world.Living.Select(c => c.Id));
            world.Inoculate(genome, places.Length, heightY: -2f);

            List<Organism> made = world.Living.Where(c => !before.Contains(c.Id)).ToList();
            Assert.Equal(places.Length, made.Count);

            for (int i = 0; i < made.Count; i++)
            {
                world.Observe(made[i], places[i], 0f);
                world.KillDiverged(made[i]);
            }
        }

        private static Float3[] Repeat(Float3 at, int n) => Enumerable.Repeat(at, n).ToArray();

        private static readonly Float3 AtA = new Float3(1.5f, -2f, 1.5f);
        private static readonly Float3 AtB = new Float3(13.5f, -2f, 4.5f);

        private static bool InA(float x, float z) => x >= 1f && x < 2f && z >= 1f && z < 2f;
        private static bool InB(float x, float z) => x >= 13f && x < 14f && z >= 4f && z < 5f;

        private (World World, SamplingPlacement Placer) Stage(RunConfig config, ulong seed)
        {
            var placer = new SamplingPlacement(seed);
            var world = new World(config, seed) { Placement = placer };
            Corpses(world, Bare(), Repeat(AtA, 10));
            Corpses(world, Bare(), AtB);
            Assert.Equal(11, world.Corpses.Count);
            return (world, placer);
        }

        private static (int A, int B, int Elsewhere) Count(SamplingPlacement placer, int from)
        {
            int a = 0, b = 0, elsewhere = 0;
            for (int i = from; i < placer.Planted.Count; i++)
            {
                (float x, float z) = placer.Planted[i];
                if (InA(x, z)) a++;
                else if (InB(x, z)) b++;
                else elsewhere++;
            }

            return (a, b, elsewhere);
        }

        [Fact]
        public void AMouthLandsInTheCorpsesColumnsByTheirJoules()
        {
            (World world, SamplingPlacement placer) = Stage(GridBox(followCorpses: true), 41UL);
            int from = placer.Planted.Count;

            world.Inoculate(Mouth(), Draws, heightY: -2f);
            (int a, int b, int elsewhere) = Count(placer, from);

            _output.WriteLine($"mouth: A (ten corpses) {a}, B (one) {b}, elsewhere {elsewhere}");

            Assert.Equal(0, elsewhere);
            Assert.True(a > 5 * b, $"mouth {a}:{b}");
            Assert.True(b > 0, "the one-corpse column is a tenth of the richest, not nothing");
        }

        [Fact]
        public void OffTheMouthLandsAnywhereAsBefore()
        {
            (World world, SamplingPlacement placer) = Stage(GridBox(followCorpses: false), 42UL);

            Phenotype mouth = Developer.Develop(Mouth(), world.Config.Development, null, world.Config.Shapes);
            for (float x = 0.25f; x < Length; x += 0.5f)
            {
                for (float z = 0.25f; z < Width; z += 0.5f)
                {
                    Assert.Equal(1f, placer.FounderAcceptance(mouth, x, z));
                }
            }
        }

        [Fact]
        public void AWorldWithNoCorpseAcceptsAMouthAnywhere()
        {
            var placer = new SamplingPlacement(43UL);
            var world = new World(GridBox(followCorpses: true), 43UL) { Placement = placer };

            world.Inoculate(Mouth(), Draws, heightY: -2f);
            (int a, int b, int elsewhere) = Count(placer, 0);

            _output.WriteLine($"no corpses: A {a}, B {b}, elsewhere {elsewhere}");
            Assert.True(elsewhere > 950, $"elsewhere {elsewhere}");
        }

        [Fact]
        public void TheDepthIsFromTheRichestCorpseToTheReachAboveIt()
        {
            (World world, SamplingPlacement placer) = Stage(GridBox(followCorpses: true, depth: true), 44UL);

            // Column D: a small corpse first, high, then a larger one lower down.
            var smallAt = new Float3(20.5f, -1f, 2.5f);
            var largeAt = new Float3(20.4f, -3f, 2.6f);
            Corpses(world, Bare(0.2f), smallAt);
            Corpses(world, Bare(0.3f), largeAt);
            Assert.True(world.Corpses[12].Joules > world.Corpses[11].Joules);

            RunConfig c = world.Config;
            Phenotype mouth = Developer.Develop(Mouth(), c.Development, null, c.Shapes);
            Phenotype bare = Developer.Develop(Bare(), c.Development, null, c.Shapes);

            Assert.Equal(((float Top, float Bottom)?)(-2f + Reach, -2f), placer.FounderDepth(mouth, 1.2f, 1.7f));
            Assert.Equal(((float Top, float Bottom)?)(-3f + Reach, -3f), placer.FounderDepth(mouth, 20.9f, 2.1f));
            Assert.Null(placer.FounderDepth(mouth, 7.5f, 3.5f));
            Assert.Null(placer.FounderDepth(bare, 1.5f, 1.5f));
        }

        [Fact]
        public void AStomachWithAMouthTakesTheLargerShareAndItsDepth()
        {
            var placer = new SamplingPlacement(45UL);
            var world = new World(GridBox(followCorpses: true, depth: true), 45UL) { Placement = placer };

            // Snow in nine columns at x 20 to 23, z 0 to 3, equal; the corpses in A alone.
            for (int ix = 0; ix < 3; ix++)
            {
                for (int iz = 0; iz < 3; iz++)
                {
                    world.Nutrients.Deposit(new FieldPoint(new Float3(20f + ix + 0.5f, -4.5f, iz + 0.5f), 0), 10f);
                }
            }

            Corpses(world, Bare(), Repeat(AtA, 3));
            int from = placer.Planted.Count;

            world.Inoculate(StomachAndMouth(), Draws, heightY: -2f);

            int a = 0, snow = 0, elsewhere = 0;
            for (int i = from; i < placer.Planted.Count; i++)
            {
                (float x, float z) = placer.Planted[i];
                if (InA(x, z)) a++;
                else if (x >= 20f && x < 23f && z < 3f) snow++;
                else elsewhere++;
            }

            _output.WriteLine($"stomach and mouth: corpses {a}, snow {snow}, elsewhere {elsewhere}");

            // One corpse column and nine snow columns, each a share of 1: about one in ten.
            Assert.Equal(0, elsewhere);
            Assert.InRange(a, 60, 145);

            RunConfig c = world.Config;
            Phenotype both = Developer.Develop(StomachAndMouth(), c.Development, null, c.Shapes);
            Assert.Equal(((float Top, float Bottom)?)(-2f + Reach, -2f), placer.FounderDepth(both, 1.5f, 1.5f));
            Assert.Equal(((float Top, float Bottom)?)(-4f, -5f), placer.FounderDepth(both, 21.5f, 1.5f));
        }

        [Fact]
        public void ANewCorpseIsReadAtTheNextQuestion()
        {
            (World world, SamplingPlacement placer) = Stage(GridBox(followCorpses: true), 46UL);
            Phenotype mouth = Developer.Develop(Mouth(), world.Config.Development, null, world.Config.Shapes);

            Assert.Equal(1f, placer.FounderAcceptance(mouth, 1.5f, 1.5f));
            Fixtures.AssertClose(0.1f, placer.FounderAcceptance(mouth, 13.5f, 4.5f), 1e-6f);

            // Twenty more in B: B holds 21 and A 10, and the next question reads it.
            Corpses(world, Bare(), Repeat(AtB, 20));

            Assert.Equal(1f, placer.FounderAcceptance(mouth, 13.5f, 4.5f));
            Fixtures.AssertClose(10f / 21f, placer.FounderAcceptance(mouth, 1.5f, 1.5f), 1e-6f);
        }

        [Fact]
        public void WithoutFoodCorpsesOrAReachItIsRefused()
        {
            RunConfig noFood = GridBox(followCorpses: true);
            noFood.FoundersFollowFood = false;
            Assert.Throws<ArgumentException>(() => new World(noFood, 1UL));

            RunConfig noCorpses = GridBox(followCorpses: true);
            noCorpses.CorpseDecayPerSecond = 0f;
            Assert.Throws<ArgumentException>(() => new World(noCorpses, 1UL));

            RunConfig noReach = GridBox(followCorpses: true);
            noReach.IntakeReachMetres = 0f;
            Assert.Throws<ArgumentException>(() => new World(noReach, 1UL));
        }
    }
}
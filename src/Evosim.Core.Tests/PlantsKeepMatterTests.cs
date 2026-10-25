using System;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D206: a plant keeps its matter. What it burns leaves as heat and its units stay in the body,
    /// for fixation to charge again before it draws on the water (logbook/specs/plants-keep-matter-build.md).
    /// </summary>
    public class PlantsKeepMatterTests
    {
        private readonly ITestOutputHelper _output;

        public PlantsKeepMatterTests(ITestOutputHelper output) => _output = output;

        private static RunConfig Lit(bool keeps) => new RunConfig
        {
            // No floor: its founders are random bodies, and their burns would reach the water.
            MinimumPopulation = 0,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(64f, 12f),
            PlantsKeepMatter = keeps,
        };

        private static RunConfig Dark(bool keeps) => new RunConfig
        {
            // No floor: its founders are random bodies, and their burns would reach the water.
            MinimumPopulation = 0,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(1e-6f, 1f),
            RemineralisationPerSecond = 0f,
            CorpseDecayPerSecond = 0.01f,
            PlantsKeepMatter = keeps,
        };

        private static Genome Leaf() => Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic);

        /// <summary>A leaf with a stomach behind it: a photosynthetic root and a consumer box on its +X face.</summary>
        private static Genome LeafWithStomach() => LeafWith(CellTypeIds.Consumer, intake: 0f);

        /// <summary>A photosynthetic root with a box of <paramref name="cell"/> on its +X face, the box's mouth at <paramref name="intake"/>.</summary>
        private static Genome LeafWith(string cell, float intake)
        {
            var g = new Genome();
            MorphNode leaf = Fixtures.Box(0.25f);
            leaf.CellTypeId = CellTypeIds.Photosynthetic;
            leaf.Edges.Add(Fixtures.FaceToFace(1));
            MorphNode stomach = Fixtures.Box(0.2f);
            stomach.CellTypeId = cell;
            stomach.Intake = intake;
            g.Nodes.Add(leaf);
            g.Nodes.Add(stomach);
            g.RootIndex = 0;
            g.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            return g;
        }

        private static void AssertBooksClose(World world)
        {
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"matter residual {world.MatterResidual:R}");

            // The store's own books: what it holds is what it kept, less what fixation took back
            // and what the corpses and the deaths let go.
            double store = world.HeldTotal - world.RechargedTotal - world.HeldReleasedTotal;
            Assert.True(
                Math.Abs(world.HeldUnits - store) <= 1e-9 * Math.Max(1d, world.HeldTotal),
                $"held {world.HeldUnits:R} against the counters' {store:R}");
        }

        // ------------------------------------------------------------------ the share

        [Fact]
        public void APlantHoldsItAllAnEaterItsLeavesShareAndANonPlantNothing()
        {
            RunConfig config = Lit(keeps: true);

            Phenotype leaf = Developer.Develop(Leaf(), config.Development, null, config.Shapes);
            Assert.Equal(1f, Metabolism.HeldShare(leaf, config));

            // A mouth makes an eater of a leaf: the leaf's share, not the whole.
            Phenotype mouthed = Developer.Develop(LeafWith(CellTypeIds.Consumer, intake: 1f), config.Development, null, config.Shapes);
            float mouthedShare = Metabolism.HeldShare(mouthed, config);
            Assert.InRange(mouthedShare, 0.01f, 0.99f);

            // A part of another tissue with no mouth leaves it a plant.
            Phenotype stalked = Developer.Develop(LeafWith(CellTypeIds.Structural, intake: 0f), config.Development, null, config.Shapes);
            Assert.Equal(1f, Metabolism.HeldShare(stalked, config));

            Phenotype box = Developer.Develop(Fixtures.SingleBox(), config.Development, null, config.Shapes);
            Assert.Equal(0f, Metabolism.HeldShare(box, config));

            Phenotype mixed = Developer.Develop(LeafWithStomach(), config.Development, null, config.Shapes);
            double plant = 0d, all = 0d;
            foreach (PhenotypePart part in mixed.Parts)
            {
                double watts = config.CellTypes.Resolve(part.CellTypeId).Upkeep(part.Volume, 1f);
                all += watts;
                if (part.CellTypeId == CellTypeIds.Photosynthetic) plant += watts;
            }

            float share = Metabolism.HeldShare(mixed, config);
            _output.WriteLine($"leaf with a stomach: share {share:R}, expected {plant / all:R}");
            Assert.InRange(share, 0.01f, 0.99f);
            Assert.Equal((float)(plant / all), share);
        }

        // ------------------------------------------------------------------ off

        [Fact]
        public void OffNothingIsHeldAndTheBurnReachesTheWater()
        {
            Assert.False(new RunConfig().PlantsKeepMatter);

            var world = new World(Lit(keeps: false), seed: 3);
            world.Inoculate(Leaf(), count: 6, heightY: -5f);
            for (int i = 0; i < 200; i++) world.Step(0.5f);

            Assert.All(world.Living, c => Assert.Equal(0d, c.SpentUnits));
            Assert.Equal(0d, world.HeldTotal);
            Assert.Equal(0d, world.HeldUnits);
            Assert.True(world.BurntTotal > 0d);
            AssertBooksClose(world);
        }

        // ------------------------------------------------------------------ on

        [Fact]
        public void InTheDarkALeafsBurnStaysInItAndGoesWithItsCorpse()
        {
            var world = new World(Dark(keeps: true), seed: 5);
            world.Inoculate(Leaf(), count: 8, heightY: -5f);

            double held = 0d;
            for (int i = 0; i < 400; i++)
            {
                world.Step(0.5f);
                held = Math.Max(held, world.Living.Sum(c => c.SpentUnits));
                AssertBooksClose(world);
            }

            _output.WriteLine(
                $"kept {world.HeldTotal:R}, recharged {world.RechargedTotal:R}, released {world.HeldReleasedTotal:R}, " +
                $"burnt to the water {world.BurntTotal:R}, deaths {world.Deaths}");

            Assert.True(held > 0d, "no leaf held anything");
            Assert.True(world.HeldTotal > 0d);

            // The dark is a millionth of the light, not none: a trace is charged back.
            Assert.True(world.RechargedTotal < 1e-6 * world.HeldTotal, $"recharged {world.RechargedTotal:R}");
            Assert.Equal(0d, world.BurntTotal);
            Assert.True(world.Deaths > 0, "nothing starved in the dark");
            Assert.True(world.HeldReleasedTotal > 0d, "no corpse let its units go");
        }

        [Fact]
        public void InTheLightALeafChargesItsOwnUnitsAndTheBooksClose()
        {
            var world = new World(Lit(keeps: true), seed: 7);
            world.Inoculate(Leaf(), count: 6, heightY: -5f);
            for (int i = 0; i < 600; i++)
            {
                world.Step(0.5f);
                AssertBooksClose(world);
            }

            _output.WriteLine(
                $"kept {world.HeldTotal:R}, recharged {world.RechargedTotal:R}, held now {world.HeldUnits:R}, " +
                $"burnt to the water {world.BurntTotal:R}, living {world.Living.Count}");

            Assert.True(world.HeldTotal > 0d);
            Assert.True(world.RechargedTotal > 0d, "fixation never charged a body's own units");
            Assert.Equal(0d, world.BurntTotal);
        }

        [Fact]
        public void ALeafWithAStomachHoldsItsLeavesShareAndReleasesTheRest()
        {
            RunConfig config = Lit(keeps: true);
            var world = new World(config, seed: 11);
            Genome genome = LeafWithStomach();
            world.Inoculate(genome, count: 6, heightY: -5f);
            float share = Metabolism.HeldShare(
                Developer.Develop(genome, config.Development, null, config.Shapes), config);

            for (int i = 0; i < 200; i++)
            {
                world.Step(0.5f);
                AssertBooksClose(world);
            }

            double kept = world.HeldTotal / (world.HeldTotal + world.BurntTotal);
            _output.WriteLine($"share {share:R}, kept {kept:R} of the burn");
            Assert.True(world.BurntTotal > 0d);
            Assert.True(Math.Abs(kept - share) <= 1e-4, $"kept {kept:R} of the burn against a share of {share:R}");
        }

        // ------------------------------------------------------------------ the checkpoint

        /// <summary>The world at a layout, through the internal writer the tests use (ImmigrationTests' shape).</summary>
        private static byte[] StateOf(World world, int layout = World.StateVersion)
        {
            System.Reflection.MethodInfo writer = typeof(World).GetMethod(
                "WriteState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(BinaryWriter), typeof(int) }, null);
            Assert.NotNull(writer);

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    try
                    {
                        writer.Invoke(world, new object[] { w, layout });
                    }
                    catch (System.Reflection.TargetInvocationException e) when (e.InnerException != null)
                    {
                        throw e.InnerException;
                    }

                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        private static World Restored(RunConfig config, ulong seed, byte[] state)
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

        [Fact]
        public void ACheckpointCarriesTheStoreAndTheResumeIsTheSameWorld()
        {
            RunConfig config = Dark(keeps: true);
            var world = new World(config, seed: 13);
            world.Inoculate(Leaf(), count: 8, heightY: -5f);
            for (int i = 0; i < 150; i++) world.Step(0.5f);
            Assert.True(world.HeldUnits > 0d);

            World restored = Restored(config, 13, StateOf(world));
            Assert.Equal(World.StateVersion, restored.StateVersionRead);
            Assert.Equal(world.HeldUnits, restored.HeldUnits);
            Assert.Equal(world.HeldTotal, restored.HeldTotal);

            for (int i = 0; i < 150; i++)
            {
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(world.HeldUnits, restored.HeldUnits);
            Assert.Equal(world.HeldReleasedTotal, restored.HeldReleasedTotal);
            Assert.Equal(world.StandingMatterUnits, restored.StandingMatterUnits);
        }

        [Fact]
        public void VersionEighteenIsReadAsAWorldThatHeldNothingAndIsNotWrittenWithTheRuleOn()
        {
            Assert.Equal(20, World.StateVersion);
            Assert.Equal(18, World.PreHeldStateVersion);
            Assert.Equal(18, World.CardPairStateVersion);

            // Round 61's world wrote 18 with the rule off; a world with it on reads that stream as
            // one in which nothing was held.
            var before = new World(Lit(keeps: false), seed: 17);
            before.Inoculate(Leaf(), count: 6, heightY: -5f);
            for (int i = 0; i < 100; i++) before.Step(0.5f);
            byte[] eighteen = StateOf(before, World.PreHeldStateVersion);

            World resumed = Restored(Lit(keeps: true), 17, eighteen);
            Assert.Equal(World.PreHeldStateVersion, resumed.StateVersionRead);
            Assert.Equal(0d, resumed.HeldUnits);
            for (int i = 0; i < 100; i++) resumed.Step(0.5f);
            Assert.True(resumed.HeldTotal > 0d);
            AssertBooksClose(resumed);

            // And version 18 has no place for the store.
            var keeping = new World(Lit(keeps: true), seed: 17);
            Assert.Throws<InvalidOperationException>(() => StateOf(keeping, World.PreHeldStateVersion));
        }
    }
}

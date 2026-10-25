using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D207: D206's form made smooth, and a body's own leaves take its spent matter first, behind
    /// <see cref="RunConfig.PlantsKeepOwnFirst"/> (the owner's ruling of 2026-10-19).
    /// </summary>
    public class PlantsKeepOwnFirstTests
    {
        private readonly ITestOutputHelper _output;

        public PlantsKeepOwnFirstTests(ITestOutputHelper output) => _output = output;

        private static RunConfig Lit(bool ownFirst, bool keeps = true) => new RunConfig
        {
            // No floor: its founders are random bodies, and their burns would reach the water.
            MinimumPopulation = 0,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(64f, 12f),
            PlantsKeepMatter = keeps,
            PlantsKeepOwnFirst = ownFirst,
        };

        private static RunConfig Dark(bool ownFirst) => new RunConfig
        {
            MinimumPopulation = 0,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = new LightModel(1e-6f, 1f),
            RemineralisationPerSecond = 0f,
            CorpseDecayPerSecond = 0.01f,
            PlantsKeepMatter = true,
            PlantsKeepOwnFirst = ownFirst,
        };

        private static Genome Leaf() => Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic);

        /// <summary>A photosynthetic root, then a chain of boxes of half-size 0.2 m on each one's +X face, of these cells and mouths.</summary>
        private static Genome LeafThen(params (string Cell, float Intake)[] boxes) =>
            Sized(boxes.Select(b => (b.Cell, b.Intake, 0.2f)).ToArray());

        /// <summary>A photosynthetic root, then a chain of boxes on each one's +X face, of these cells, mouths and half-sizes.</summary>
        private static Genome Sized(params (string Cell, float Intake, float Half)[] boxes)
        {
            var g = new Genome();
            MorphNode leaf = Fixtures.Box(0.25f);
            leaf.CellTypeId = CellTypeIds.Photosynthetic;
            g.Nodes.Add(leaf);
            MorphNode last = leaf;
            foreach ((string cell, float intake, float half) in boxes)
            {
                last.Edges.Add(Fixtures.FaceToFace(g.Nodes.Count));
                MorphNode box = Fixtures.Box(half);
                box.CellTypeId = cell;
                box.Intake = intake;
                g.Nodes.Add(box);
                last = box;
            }

            g.RootIndex = 0;
            g.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            return g;
        }

        private static Phenotype Body(Genome g, RunConfig config) => Developer.Develop(g, config.Development, null, config.Shapes);

        private static void AssertBooksClose(World world)
        {
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"matter residual {world.MatterResidual:R}");
            double store = world.HeldTotal - world.RechargedTotal - world.HeldReleasedTotal;
            Assert.True(
                Math.Abs(world.HeldUnits - store) <= 1e-9 * Math.Max(1d, world.HeldTotal),
                $"held {world.HeldUnits:R} against the counters' {store:R}");
        }

        // ------------------------------------------------------------------ the smooth share

        [Fact]
        public void TheSmoothShareCountsEatingAgainstLeavesAndLeavesTheRestOut()
        {
            RunConfig config = Lit(ownFirst: true);

            Assert.Equal(1f, Metabolism.SmoothHeldShare(Body(Leaf(), config), config));
            Assert.Equal(0f, Metabolism.SmoothHeldShare(Body(Fixtures.SingleBox(), config), config));
            Assert.Equal(1f, Metabolism.SmoothHeldShare(Body(LeafThen((CellTypeIds.Structural, 0f)), config), config));

            // Two parts, a leaf and a stomach: the same as D206's share, since nothing else is in the body.
            Phenotype stomach = Body(LeafThen((CellTypeIds.Consumer, 0f)), config);
            Assert.Equal(Metabolism.HeldShare(stomach, config), Metabolism.SmoothHeldShare(stomach, config));

            // A stalk between them counts on neither side: the leaf's upkeep over the leaf's and the stomach's.
            Phenotype stalked = Body(LeafThen((CellTypeIds.Structural, 0f), (CellTypeIds.Consumer, 0f)), config);
            double plant = 0d, eating = 0d, all = 0d;
            foreach (PhenotypePart part in stalked.Parts)
            {
                double watts = config.CellTypes.Resolve(part.CellTypeId).Upkeep(part.Volume, 1f);
                all += watts;
                if (part.CellTypeId == CellTypeIds.Photosynthetic) plant += watts;
                if (part.CellTypeId == CellTypeIds.Consumer) eating += watts;
            }

            float smooth = Metabolism.SmoothHeldShare(stalked, config);
            _output.WriteLine($"leaf, stalk, stomach: smooth {smooth:R} against {plant / (plant + eating):R}; D206's {Metabolism.HeldShare(stalked, config):R}");
            Assert.Equal((float)(plant / (plant + eating)), smooth);
            Assert.Equal((float)(plant / all), Metabolism.HeldShare(stalked, config));
            Assert.True(smooth > Metabolism.HeldShare(stalked, config));
        }

        [Fact]
        public void ATinyMouthMovesTheShareByATinyAmount()
        {
            // Only a consumer cell may carry a mouth (D106's caps), so a mouth's size is its consumer part's. A
            // stalk between the leaf and the mouth counts on neither side, and under D206 as first built it put
            // the plant at its leaf's share of the whole the moment any mouth appeared.
            RunConfig config = Lit(ownFirst: true);
            config.IntakeWattsPerUnit = 0.1f;

            float last = 1f, smallest = float.NaN;
            foreach (float half in new[] { 0.05f, 0.08f, 0.12f, 0.2f })
            {
                Phenotype mouthed = Body(Sized((CellTypeIds.Structural, 0f, 0.2f), (CellTypeIds.Consumer, 1f, half)), config);
                // Each mouth is in the body: development drops a part too small for it.
                Assert.Contains(mouthed.Parts, p => p.CellTypeId == CellTypeIds.Consumer);
                float smooth = Metabolism.SmoothHeldShare(mouthed, config);
                float cliff = Metabolism.HeldShare(mouthed, config);
                _output.WriteLine($"a mouth {half:R} m across the half: smooth {smooth:R}, D206's {cliff:R}");
                Assert.True(smooth <= last, $"the share rose from {last:R} to {smooth:R} as the mouth grew");
                last = smooth;
                if (half <= 0.05f)
                {
                    smallest = smooth;
                    Assert.True(1f - smooth <= 0.05f, $"a mouth of half {half:R} m took the share to {smooth:R}");
                    Assert.True(cliff < smooth - 0.01f, $"D206's share {cliff:R} against the smooth {smooth:R}");
                }
            }

            Assert.True(last < 0.99f, $"a mouth as large as the stalk still left the share at {last:R}");
            // The share's fall follows the mouth's size: a mouth of a quarter the side is well under a quarter of it.
            Assert.True(1f - smallest < 0.25f * (1f - last), $"{smallest:R} at the smallest mouth against {last:R} at the largest");
        }

        // ------------------------------------------------------------------ the switch

        [Fact]
        public void OffByDefaultAndRefusedWithoutD206()
        {
            Assert.False(new RunConfig().PlantsKeepOwnFirst);
            ArgumentException e = Assert.Throws<ArgumentException>(() => new World(Lit(ownFirst: true, keeps: false), seed: 1));
            Assert.Contains("PlantsKeepOwnFirst", e.Message);
        }

        [Fact]
        public void APurePlantIsD206sToTheBit()
        {
            World on = new World(Lit(ownFirst: true), seed: 7), off = new World(Lit(ownFirst: false), seed: 7);
            on.Inoculate(Leaf(), count: 6, heightY: -5f);
            off.Inoculate(Leaf(), count: 6, heightY: -5f);
            for (int i = 0; i < 400; i++)
            {
                on.Step(0.5f);
                off.Step(0.5f);
            }

            Assert.True(on.HeldTotal > 0d);
            Assert.Equal(off.HeldTotal, on.HeldTotal);
            Assert.Equal(off.HeldUnits, on.HeldUnits);
            Assert.Equal(off.RechargedTotal, on.RechargedTotal);
            Assert.Equal(off.BurntTotal, on.BurntTotal);
            Assert.Equal(off.StandingMatterUnits, on.StandingMatterUnits);
            Assert.Equal(off.Living.Count, on.Living.Count);
            AssertBooksClose(on);
        }

        // ------------------------------------------------------------------ own leaves first

        [Fact]
        public void InTheLightAHybridsLeavesTakeItsBurnFirstAndItReleasesLess()
        {
            Genome hybrid = LeafThen((CellTypeIds.Consumer, 0f));
            double Kept(bool ownFirst)
            {
                var world = new World(Lit(ownFirst), seed: 11);
                world.Inoculate(hybrid, count: 6, heightY: -5f);
                for (int i = 0; i < 200; i++)
                {
                    world.Step(0.5f);
                    AssertBooksClose(world);
                }

                Assert.True(world.BurntTotal > 0d || ownFirst);
                return world.HeldTotal / (world.HeldTotal + world.BurntTotal);
            }

            double before = Kept(false), after = Kept(true);
            _output.WriteLine($"a leaf with a stomach in the light keeps {before:R} of its burn under D206 and {after:R} with own leaves first");
            Assert.True(after > before + 0.01, $"own leaves first kept {after:R} against {before:R}");
        }

        [Fact]
        public void InTheDarkTheLeavesTakeNothingAndTheShareRules()
        {
            RunConfig config = Dark(ownFirst: true);
            Genome hybrid = LeafThen((CellTypeIds.Consumer, 0f));
            float share = Metabolism.SmoothHeldShare(Body(hybrid, config), config);

            var world = new World(config, seed: 5);
            world.Inoculate(hybrid, count: 6, heightY: -5f);
            for (int i = 0; i < 200; i++)
            {
                world.Step(0.5f);
                AssertBooksClose(world);
            }

            double kept = world.HeldTotal / (world.HeldTotal + world.BurntTotal);
            _output.WriteLine($"in the dark: kept {kept:R} of the burn, the smooth share {share:R}");
            Assert.True(world.BurntTotal > 0d);
            Assert.True(Math.Abs(kept - share) <= 1e-4, $"kept {kept:R} against a share of {share:R}");
        }

        private static readonly MethodInfo ReturnSpentMethod = typeof(World).GetMethod(
            "ReturnSpent", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Organism), typeof(double) }, null);

        private static readonly MethodInfo SpentSetter = typeof(Organism).GetProperty("SpentUnits").GetSetMethod(nonPublic: true);

        [Fact]
        public void EveryBurnReadsTheLeavesCapacityFromTheLastStep()
        {
            Assert.NotNull(ReturnSpentMethod);
            Assert.NotNull(SpentSetter);

            // A leaf with a mouth: an eater with no absorptive cell, whose last ledger only D207 keeps.
            RunConfig config = Lit(ownFirst: true);
            config.IntakeWattsPerUnit = 0.1f;
            Genome hybrid = LeafThen((CellTypeIds.Consumer, 1f));

            var off = new World(Lit(ownFirst: false), seed: 3);
            off.Inoculate(hybrid, count: 1, heightY: -2f);
            off.Step(0.5f);
            Assert.Equal(0f, off.Living[0].LastLedger.LightCapacity);

            var world = new World(config, seed: 3);
            world.Inoculate(hybrid, count: 1, heightY: -2f);
            world.Step(0.5f);
            Organism body = world.Living[0];
            Assert.False(body.HasAbsorptiveTissue);
            double capacity = body.LastLedger.LightCapacity / (double)config.JoulesPerUnit;
            Assert.True(capacity > 0d, "the leaf's capacity was not kept");
            float share = Metabolism.SmoothHeldShare(body.Phenotype, config);
            Assert.InRange(share, 0.01f, 0.99f);

            // An empty store: a burn under the capacity is held whole, and nothing reaches the water.
            SpentSetter.Invoke(body, new object[] { 0d });
            double burnt = world.BurntTotal;
            ReturnSpentMethod.Invoke(world, new object[] { body, 0.25 * capacity });
            Assert.Equal(0.25 * capacity, body.SpentUnits);
            Assert.Equal(burnt, world.BurntTotal);

            // A burn over the room: the room is held, then the share of the rest.
            double held = body.SpentUnits, burn = 2d * capacity, room = capacity - held;
            ReturnSpentMethod.Invoke(world, new object[] { body, burn });
            double expected = held + room + (burn - room) * share;
            _output.WriteLine($"capacity {capacity:R} units, share {share:R}: held {body.SpentUnits:R} against {expected:R}");
            Assert.True(Math.Abs(body.SpentUnits - expected) <= 1e-12 * Math.Max(1d, expected));
            Assert.True(Math.Abs(world.BurntTotal - burnt - (burn - room) * (1d - share)) <= 1e-12 * Math.Max(1d, burn));

            // A full store: the leaves take nothing more, and the share rules.
            held = body.SpentUnits;
            burnt = world.BurntTotal;
            ReturnSpentMethod.Invoke(world, new object[] { body, capacity });
            Assert.True(Math.Abs(body.SpentUnits - held - capacity * share) <= 1e-12 * Math.Max(1d, held));
        }

        // ------------------------------------------------------------------ the checkpoint

        private static byte[] StateOf(World world)
        {
            MethodInfo writer = typeof(World).GetMethod(
                "WriteState", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(BinaryWriter), typeof(int) }, null);
            Assert.NotNull(writer);
            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Invoke(world, new object[] { w, World.StateVersion });
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        [Fact]
        public void ACheckpointCarriesTheLeavesCapacityAndTheResumeIsTheSameWorld()
        {
            RunConfig config = Lit(ownFirst: true);
            config.IntakeWattsPerUnit = 0.1f;
            Genome hybrid = LeafThen((CellTypeIds.Consumer, 1f));
            var world = new World(config, seed: 13);
            world.Inoculate(hybrid, count: 8, heightY: -5f);
            for (int i = 0; i < 150; i++) world.Step(0.5f);
            Assert.True(world.HeldUnits > 0d);

            byte[] state = StateOf(world);
            var restored = new World(config, 13);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                restored.ReadState(r);
            }

            Assert.Equal(World.StateVersion, restored.StateVersionRead);
            Assert.Equal(StateOf(world), StateOf(restored));
            for (int i = 0; i < 150; i++)
            {
                world.Step(0.5f);
                restored.Step(0.5f);
            }

            Assert.Equal(world.HeldUnits, restored.HeldUnits);
            Assert.Equal(world.HeldTotal, restored.HeldTotal);
            Assert.Equal(world.BurntTotal, restored.BurntTotal);
            Assert.Equal(world.StandingMatterUnits, restored.StandingMatterUnits);
            Assert.Equal(StateOf(world), StateOf(restored));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D162 and D165's anchoring cell in Core: its numbers, its place at the end of a world's
    /// table, and its bud weight. The grip itself is the solver's.
    /// </summary>
    public sealed class AnchoringCellTests
    {
        private readonly ITestOutputHelper _out;

        public AnchoringCellTests(ITestOutputHelper output) => _out = output;

        private static readonly MutationRates BudOnly = new MutationRates
        {
            CellTypeChance = 1f, AddNodeChance = 0f, ScalarChance = 0f, JointTypeChance = 0f,
            AddEdgeChance = 0f, RemoveEdgeChance = 0f, RecursiveLimitChance = 0f,
            FlagChance = 0f, ShapeChance = 0f,
        };

        private static CellTypeRegistry WithAnchoring(float budWeight = 1f) =>
            CellTypeRegistry.Standard.Appending(new AnchoringCell { BudWeight = budWeight });

        /// <summary>The bud types of seeds 1 to 300 from a leaf, joined.</summary>
        private static string BudTypes(CellTypeRegistry cellTypes, int seeds = 300)
        {
            var drawn = new List<string>();
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                Genome parent = Fixtures.SingleBox();
                parent.Nodes[0].CellTypeId = CellTypeIds.Photosynthetic;
                Genome child = Mutator.Mutate(parent, new Rng(seed), BudOnly, cellTypes);
                drawn.Add(child.Nodes.Count > 1 ? child.Nodes[1].CellTypeId : "-");
            }

            return string.Join(",", drawn);
        }

        // ------------------------------------------------------------------ off

        [Fact]
        public void TheStandardTableIsUnchangedAndDrawsAsBefore()
        {
            Assert.Equal(7, CellTypeRegistry.Standard.Count);
            Assert.DoesNotContain(CellTypeIds.Anchoring, CellTypeRegistry.Standard.Ids());
            Assert.DoesNotContain(",bud=", CellTypeRegistry.Standard.HashContribution());
            Assert.Equal(PinnedHash, CellTypeRegistry.Standard.HashContribution());

            // Recorded on the build before bud weights existed (r59 step 2b).
            Assert.Equal(PinnedBudTypes, Digest(BudTypes(CellTypeRegistry.Standard)));
        }

        private const string PinnedHash = 
            "cells[structural:upkeep=1,joint=False,tissue=500,atkMax=1,inkMax=0,prtMax=1,tghMax=4|" +
            "link:upkeep=2.5,joint=True,idle=0.02,photo=0,photoUpkeep=3,tissue=500,atkMax=0.5,inkMax=0,prtMax=0.5,tghMax=2|" +
            "neural:upkeep=5,supported=400,discount=0.2,tissue=500,atkMax=0,inkMax=0,prtMax=0,tghMax=1|" +
            "photosynthetic:upkeep=3,joint=False,eff=0.05,tissue=500,atkMax=0,inkMax=0,prtMax=0.5,tghMax=1|" +
            "absorptive:upkeep=4,joint=False,clearance=1,yield=1,tissue=500,atkMax=0,inkMax=0,prtMax=0.5,tghMax=1|" +
            "consumer:upkeep=6,joint=False,bite=20,scavenge=1,yield=0.8/0.5/0.2,tissue=500,atkMax=1,inkMax=1,prtMax=0.5,tghMax=2|" +
            "buoyancy:upkeep=2.5,joint=False,lift=0.05,maxLift=3,tissue=500,atkMax=0,inkMax=0,prtMax=0,tghMax=1]";
        private const string PinnedBudTypes = "fa7f8004ddbbae07";

        private static string Digest(string text)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
        }

        // ------------------------------------------------------------------ on

        [Fact]
        public void TheCellIsAppendedLastAndSurvivesTheRoundTrip()
        {
            var anchoring = new AnchoringCell(
                tenacityNewtonsPerSquareMetre: 3e4f, buildJoulesPerNewton: 2e-4f, decaySeconds: 900f,
                reserveThresholdSeconds: 45f, reserveLagSeconds: 15f, upkeepWattsPerCubicMetre: 3f)
            {
                BudWeight = 0.5f,
            };
            var config = new RunConfig { CellTypes = CellTypeRegistry.Standard.Appending(anchoring) };

            Assert.Equal(8, config.CellTypes.Count);
            Assert.Equal(7, config.CellTypes.IndexOf(CellTypeIds.Anchoring));
            for (int i = 0; i < 7; i++)
                Assert.Equal(CellTypeRegistry.Standard.At(i).Id, config.CellTypes.At(i).Id);
            Assert.NotEqual(new RunConfig().Hash(), config.Hash());

            RunConfig back = RunConfigJson.Read(RunConfigJson.Write(config), out string mismatch);
            Assert.Null(mismatch);
            Assert.Equal(config.Hash(), back.Hash());

            var read = (AnchoringCell)back.CellTypes.Resolve(CellTypeIds.Anchoring);
            Assert.Equal(3e4f, read.TenacityNewtonsPerSquareMetre);
            Assert.Equal(2e-4f, read.BuildJoulesPerNewton);
            Assert.Equal(900f, read.DecaySeconds);
            Assert.Equal(45f, read.ReserveThresholdSeconds);
            Assert.Equal(15f, read.ReserveLagSeconds);
            Assert.Equal(3f, read.UpkeepWattsPerCubicMetre);
            Assert.Equal(0.5f, read.BudWeight);
        }

        [Theory]
        [InlineData("\"budWeight\": 1,", "budWeight")]
        [InlineData("\"decaySeconds\": 3600,", "decaySeconds")]
        [InlineData("\"tenacityNewtonsPerSquareMetre\": 58000,", "tenacityNewtonsPerSquareMetre")]
        public void AMissingFieldIsRefused(string line, string field)
        {
            var config = new RunConfig { CellTypes = WithAnchoring() };
            string text = RunConfigJson.Write(config);

            // The anchoring cell is the table's last entry; take the field out of it alone.
            int at = text.LastIndexOf(line, StringComparison.Ordinal);
            Assert.True(at >= 0, $"no '{line}' in the written config");
            string cut = text.Remove(at, line.Length);

            Exception e = Assert.ThrowsAny<Exception>(() => RunConfigJson.Read(cut));
            _out.WriteLine(e.Message);
            Assert.Contains(field, e.Message);
        }

        [Fact]
        public void AWeightOfOneStaysOutOfTheHashAndAnyOtherEntersIt()
        {
            string one = WithAnchoring(1f).HashContribution();
            Assert.DoesNotContain(",bud=", one);
            Assert.StartsWith(CellTypeRegistry.Standard.HashContribution().TrimEnd(']'), one);
            Assert.NotEqual(one, WithAnchoring(2f).HashContribution());
            Assert.NotEqual(WithAnchoring(0f).HashContribution(), WithAnchoring(2f).HashContribution());
        }

        // ------------------------------------------------------------------ budding

        [Fact]
        public void ABudDrawsTheCellByItsWeight()
        {
            const int seeds = 3_000;
            int Count(float weight) =>
                BudTypes(WithAnchoring(weight), seeds).Split(',').Count(t => t == CellTypeIds.Anchoring);

            // A leaf buds one of the seven other types; at weight w the cell is w / (6 + w) of them.
            int equal = Count(1f), never = Count(0f), triple = Count(3f);
            _out.WriteLine($"of {seeds} buds from a leaf: {equal} at weight 1 (expect {seeds / 7}), " +
                           $"{never} at 0, {triple} at 3 (expect {seeds / 3})");

            Assert.Equal(0, never);
            Assert.InRange(equal, seeds / 7 - 60, seeds / 7 + 60);
            Assert.InRange(triple, seeds / 3 - 90, seeds / 3 + 90);
        }

        [Fact]
        public void AnAnchoringBudIsAValidGenomeAndDevelops()
        {
            CellTypeRegistry table = WithAnchoring(1000f);
            Genome parent = Fixtures.SingleBox();
            parent.Nodes[0].CellTypeId = CellTypeIds.Photosynthetic;

            Genome child = Mutator.Mutate(parent, new Rng(5), BudOnly, table);
            Assert.Equal(CellTypeIds.Anchoring, child.Nodes[1].CellTypeId);
            Assert.Empty(child.Validate(cellTypes: table));
            Assert.Equal(JointType.Fixed, child.Nodes[1].JointType);
            Assert.Equal(0f, child.Nodes[1].Lift);
            Developer.Develop(child, DevelopmentLimits.Default, cellTypes: table);

            // A table without the cell refuses the genome rather than standing another type in.
            Assert.Contains(child.Validate(), issue => issue.Contains("unknown cell type 'anchoring'"));
            Assert.Throws<ArgumentException>(() => Developer.Develop(child, DevelopmentLimits.Default));

            // A world whose table has the cell grows the body. The bud is grown to 10 cm first, past
            // the size floor below which a world prunes a part.
            child.Nodes[1].Dimensions = new Float3(0.1f, 0.1f, 0.1f);
            var config = new RunConfig { CellTypes = table, MinimumPopulation = 0, ContactPerPart = true };
            var world = new World(config, seed: 7);
            world.Inoculate(child, count: 1, heightY: -5f);
            Organism body = world.Living[0];
            Assert.Contains(body.Phenotype.Parts, part => part.CellTypeId == CellTypeIds.Anchoring);
            for (int i = 0; i < 10; i++) world.Step(0.5f);
            Assert.Single(world.Living);
        }

        // ------------------------------------------------------------------ the price (step 4)

        private static World PricedWorld(ulong seed = 11)
        {
            var config = new RunConfig
            {
                CellTypes = WithAnchoring(), MinimumPopulation = 0, ContactPerPart = true,
                RemineralisationPerSecond = 0f,
            };
            var world = new World(config, seed);
            world.Inoculate(Fixtures.SingleLeaf(cellTypeId: CellTypeIds.Photosynthetic), count: 1, heightY: -5f);
            return world;
        }

        [Fact]
        public void AGripsPriceIsChargedAsABurnAndBothBooksClose()
        {
            World plain = PricedWorld(), owing = PricedWorld();
            const double price = 0.01;

            owing.OweGrip(owing.Living[0], price);
            plain.Step(0.5f);
            owing.Step(0.5f);

            Organism a = plain.Living[0], b = owing.Living[0];
            _out.WriteLine($"reserve {a.Energy:R} without the grip, {b.Energy:R} with it");

            Assert.Equal(a.Energy - price, b.Energy, 12);
            Assert.Equal(price, owing.EnergyOut - plain.EnergyOut, 12);
            Assert.Equal(price, owing.GripJoulesTotal, 15);
            Assert.Equal(price, b.GripJoules, 15);
            Assert.Equal(0d, b.PendingGripJoules);
            Assert.True(Math.Abs(owing.AuditResidual) <= 1e-9 * Math.Max(1d, owing.StandingJoules), $"audit {owing.AuditResidual:R}");
            Assert.True(Math.Abs(owing.MatterResidual) <= 1e-9 * Math.Max(1d, owing.MatterInitialTotal), $"matter {owing.MatterResidual:R}");

            // A body that cannot pay is left at nothing and dies, and the books still close.
            World broke = PricedWorld();
            broke.OweGrip(broke.Living[0], 1e9);
            broke.Step(0.5f);
            Assert.Empty(broke.Living);
            Assert.True(Math.Abs(broke.AuditResidual) <= 1e-9 * Math.Max(1d, broke.StandingJoules), $"audit {broke.AuditResidual:R}");
            Assert.True(Math.Abs(broke.MatterResidual) <= 1e-9 * Math.Max(1d, broke.MatterInitialTotal), $"matter {broke.MatterResidual:R}");

            Assert.Throws<ArgumentOutOfRangeException>(() => plain.OweGrip(a, -1));
        }

        [Fact]
        public void AWorldWithTheCellNeedsPerPartContact()
        {
            var config = new RunConfig { CellTypes = WithAnchoring() };
            Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
        }

        // ------------------------------------------------------------------ the numbers

        [Fact]
        public void ItsUpkeepIsByVolumeAndItsGripByArea()
        {
            var cell = new AnchoringCell();

            Assert.Equal(2.5f * 0.002f * 4f, cell.Upkeep(0.002f, 4f), 6);
            Assert.Equal(CellIntake.None, cell.Acquire(new CellContext(1f, 0.002f)));

            // Eight times the volume is four times the area, so four times the force.
            float small = cell.BreakingForce(2.16e-4f), large = cell.BreakingForce(8 * 2.16e-4f);
            _out.WriteLine($"a 6 cm bud breaks free at {small:0.#} N, at a price of {cell.BuildPrice(small):0.######} J");
            Assert.Equal(4f, large / small, 4);
            Assert.Equal(5.8e4f * MathF.Pow(2.16e-4f, 2f / 3f), small, 1);
            Assert.Equal(cell.BuildJoulesPerNewton * small, cell.BuildPrice(small), 6);

            Assert.Throws<ArgumentOutOfRangeException>(() => new AnchoringCell(buildJoulesPerNewton: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnchoringCell(tenacityNewtonsPerSquareMetre: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnchoringCell { BudWeight = -1f });
        }
    }
}

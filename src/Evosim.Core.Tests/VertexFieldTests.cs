using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The water as vertices — D083. What the cell field guaranteed, the vertex field must too:
    /// conservation under every operator, exact sharing under the frozen-availability rule,
    /// determinism from the seed. And the one thing it exists to add: a still body eats a hole
    /// and a moving one does not.
    /// </summary>
    public class VertexFieldTests
    {
        private readonly ITestOutputHelper _output;

        public VertexFieldTests(ITestOutputHelper output) => _output = output;

        // A 100 m² box of four patches — 20 m round the ring, 5 m across — 20 m deep, so a
        // seeded lattice at the default quantum is sixteen thousand vertices and a test runs in
        // well under a second.
        private const float Area = 100f;
        private const int Patches = 4;
        private const float Depth = 20f;

        private static VertexField Field(
            float sink = 0f, float refuge = 0f, float refugeFraction = 0f, int cap = 200_000,
            float merge = 0.25f, ulong seed = 7UL) =>
            new VertexField(
                Area, 1f, sink, Depth, refuge, refugeFraction, Patches,
                kernelMetres: 1f, mergeMetres: merge, vertexCap: cap, vertexJoules: 0.125f, seed: seed);

        private static FieldPoint P(float x, float y, float z) =>
            new FieldPoint(new Float3(x, y, z), (int)Math.Floor(x / 5f) % Patches);

        private static double MinMass(VertexField field)
        {
            double min = double.MaxValue;
            for (int i = 0; i < field.StoreLength; i++)
            {
                if (field.IsAlive(i) && field.JoulesOf(i) < min) min = field.JoulesOf(i);
            }

            return min;
        }

        [Fact]
        public void ASeededLatticeReadsBackItsDensity()
        {
            // The kernel is normalised to unit integral, so a lattice of vertices holding a
            // uniform density reads that density back anywhere inside — the quadrature of a
            // smooth kernel at half its reach is good to a few percent. If this fails, every
            // price in the world is off by the same factor.
            VertexField field = Field();
            field.SeedUniform(1f);

            Assert.Equal(1.0 * 20 * 5 * 20, field.TotalJoules, 3);
            Assert.Equal(16_000, field.Count);

            foreach (var (x, y, z) in new[] { (10f, -10f, 2.5f), (3.3f, -4.1f, 1.2f), (17.9f, -15.5f, 4.4f) })
            {
                float density = field.DensityAt(P(x, y, z));
                _output.WriteLine($"({x}, {y}, {z}): {density:0.0000} J/m³");
                Assert.InRange(density, 0.85f, 1.15f);
                Assert.Equal(density, field.EdibleDensityAt(P(x, y, z)));
            }
        }

        [Fact]
        public void TwoMouthsAtOneVertexShareItExactly()
        {
            // The cell field's guarantee, asked of a single vertex: two feeders wanting sixteen
            // joules of ten get five each, the vertex is empty, and nothing went negative.
            VertexField field = Field();
            FieldPoint p = P(10f, -10f, 2.5f);
            field.Deposit(p, 10f);

            field.ClearDemand();
            field.Demand(p, 8f);
            field.Demand(p, 8f);
            field.FreezeAvailability();

            float share = field.ShareAt(p);
            Assert.Equal(0.625f, share, 5);

            float first = field.Take(p, 8f * share);
            float second = field.Take(p, 8f * share);

            Assert.Equal(5f, first, 5);
            Assert.Equal(5f, second, 5);
            Assert.Equal(0.0, field.TotalJoules, 9);
        }

        [Fact]
        public void OverlappingMouthsNeverOverdrawAVertex()
        {
            // Thirty feeders a third of a metre apart, each wanting more than a whole kernel
            // holds. Their neighbourhoods overlap heavily; the takes must sum to exactly what
            // the shares promised, and no vertex may go below zero.
            VertexField field = Field();
            field.SeedUniform(1f);
            double before = field.TotalJoules;

            const int Feeders = 30;
            const float Wanted = 5f;
            var points = new FieldPoint[Feeders];
            for (int i = 0; i < Feeders; i++) points[i] = P(5f + 0.3f * i, -5f, 2.5f);

            field.ClearDemand();
            for (int i = 0; i < Feeders; i++) field.Demand(points[i], Wanted);
            field.FreezeAvailability();

            double promised = 0.0;
            double taken = 0.0;
            for (int i = 0; i < Feeders; i++)
            {
                float share = field.ShareAt(points[i]);
                Assert.InRange(share, 0f, 1f);
                promised += Wanted * share;
                taken += field.Take(points[i], Wanted * share);
            }

            _output.WriteLine($"promised {promised:0.000} J, taken {taken:0.000} J, min vertex {MinMass(field):R} J");

            Assert.True(promised < Feeders * Wanted, "the water was not short, so the test asked too little");
            Assert.Equal(promised, taken, 4);
            Assert.Equal(before - taken, field.TotalJoules, 6);
            Assert.True(MinMass(field) >= -1e-9, $"a vertex went to {MinMass(field):R} J");
        }

        [Fact]
        public void ATakeDeliversWhatTheGatePromised()
        {
            // The second screen's fault: a mouth at one vertex of a lattice asks for nearly all
            // that is in reach, the weight-proportional spread caps the near vertex and leaves
            // the far ones untouched, and the take falls short of ReachableStock. The world had
            // booked the full price. A take must deliver min(asked, reachable) to a rounding.
            VertexField field = Field();
            field.SeedUniform(1f);
            FieldPoint p = P(10f, -10f, 2.5f);

            double reachable = field.ReachableStock(p);
            Assert.InRange(reachable, 3.5, 5.0);

            float taken = field.Take(p, (float)(reachable * 0.95));
            Assert.Equal(reachable * 0.95, taken, 4);

            // Asking for more than there is delivers everything there is, and no more.
            float rest = field.Take(p, 100f);
            Assert.Equal(reachable * 0.05, rest, 3);
            Assert.Equal(0f, field.Take(p, 1f));
            Assert.True(MinMass(field) >= 0.0);
        }

        [Fact]
        public void ATakeOfTheWholeReachableStockIsNotRefusedForWantOfPasses()
        {
            // The fourth screen's fault: the fill stopped after eight passes, and a conception
            // priced close to everything in reach across dozens of vertices of very unequal
            // weight ran out of passes and was refused as short. A dense cloud of small quanta
            // in a box round the mouth, asked for all but a rounding of what the gate reads.
            VertexField field = Field();
            field.Emit(60.0, new Float3(10f, -10f, 2.5f), new Float3(1.5f, 1.5f, 1.5f));
            FieldPoint p = P(10f, -10f, 2.5f);
            Assert.True(field.Count > 60, "the cloud should be many vertices");

            double reachable = field.ReachableStock(p);
            Assert.True(reachable > 5.0);

            float asked = (float)(reachable * (1.0 - 1e-6));
            float taken = field.Take(p, asked);
            Assert.True(taken >= asked * (1f - 1e-4f), $"took {taken} of {asked}");
            Assert.True(MinMass(field) >= 0.0);
        }

        [Fact]
        public void EveryOperatorConservesTheTotal()
        {
            VertexField field = Field(sink: 0.01f);
            field.SeedUniform(1f);
            field.Deposit(P(4f, -3f, 1f), 3f);
            double expected = field.TotalJoules;

            var current = new CurrentField
            {
                Speed = 0.3f, CellMetres = 10f, PeriodSeconds = 600f, Rolls = true,
                AdvectFields = true, RollBlinkSeconds = 300f,
            };

            for (int step = 0; step < 20; step++)
            {
                field.Settle(1f);
                field.Remineralise(1d, 0.01f);
                field.Mix(1f, 0.2f, 0.05f);
                field.Advect(current, 100d + step, 1f, field.PatchWidthMetres);
                field.Cull();
            }

            Assert.Equal(expected, field.TotalJoules, 6);
            Assert.True(MinMass(field) >= 0.0);

            // An influx founds whole quanta and banks the rest; what is counted is what exists.
            float emitted = field.Emit(1.0, new Float3(10f, 0f, 2.5f), new Float3(10f, 0f, 2.5f));
            Assert.Equal(1f, emitted, 6);
            Assert.Equal(expected + 1.0, field.TotalJoules, 6);
            Assert.Equal(0f, field.Emit(0.1, new Float3(10f, 0f, 2.5f), Float3.Zero));
            Assert.Equal(0.1, field.Bank, 9);
            Assert.Equal(expected + 1.0, field.TotalJoules, 6);
        }

        [Fact]
        public void BurialRemovesWholeVerticesRestingOnTheFloor()
        {
            VertexField field = Field(sink: 1f);
            field.Deposit(P(2f, -19.5f, 2f), 5f);
            field.Deposit(P(8f, -2f, 2f), 4f);

            field.Settle(1f);
            Assert.Equal(-Depth, field.PositionOf(0).Y);
            Assert.Equal(-3f, field.PositionOf(1).Y);

            double buried = field.BuryFloor(1.0);
            field.Cull();

            Assert.Equal(5.0, buried, 9);
            Assert.Equal(4.0, field.TotalJoules, 9);
            Assert.Equal(1, field.Count);
        }

        [Fact]
        public void DepositsJoinTheNearestVertexInReachAndFoundOneBeyondIt()
        {
            VertexField field = Field();
            field.Deposit(P(10f, -10f, 2.5f), 1f);
            Assert.Equal(1, field.Count);

            field.Deposit(P(10.6f, -10f, 2.5f), 2f);
            Assert.Equal(1, field.Count);
            Assert.Equal(3.0, field.JoulesOf(0), 9);
            Assert.Equal(new Float3(10f, -10f, 2.5f), field.PositionOf(0));

            field.Deposit(P(11.5f, -10f, 2.5f), 1f);
            Assert.Equal(2, field.Count);
            Assert.Equal(4.0, field.TotalJoules, 9);
        }

        [Fact]
        public void MixingDoesNotCoarsenTheField()
        {
            // The smoke's failure, as a test: a seeded lattice under a strong mixing walk kept
            // its count in the first build for exactly one step. Below the cap nothing merges.
            VertexField field = Field();
            field.SeedUniform(1f);
            int seeded = field.Count;

            for (int step = 0; step < 20; step++)
            {
                field.Mix(0.5f, 2f, 0f);
                field.Cull();
            }

            Assert.Equal(seeded, field.Count);
            Assert.Equal(0L, field.Merged);
        }

        [Fact]
        public void CullingMergesTowardTheCapAndKeepsTheMass()
        {
            // Sixty-four founded vertices in a two-metre box against a cap of eight: the merge
            // radius widens from a quarter metre to the kernel, each pass merges what it
            // reaches, and the field says when it could not get under the cap rather than
            // looping forever.
            VertexField field = Field(cap: 8);
            field.Emit(8.0, new Float3(10f, -10f, 2.5f), new Float3(1f, 1f, 1f));
            Assert.Equal(64, field.Count);
            double mass = field.TotalJoules;

            field.Cull();

            _output.WriteLine($"64 founded, {field.Count} after culling, over-cap steps {field.OverCapSteps}");
            Assert.True(field.Count <= 8 || field.OverCapSteps > 0);
            Assert.True(field.Count < 32);
            Assert.Equal(mass, field.TotalJoules, 6);
        }

        [Fact]
        public void AStillMouthEatsAHoleAndAMovingOneDoesNot()
        {
            // The reason the field exists. Two identical mouths in identical water, one fixed and
            // one walking half a metre a step. Neither senses anything. The walker eats more,
            // and the water where the sitter sits is emptier than the water where the walker is.
            VertexField field = Field();
            field.SeedUniform(1f);

            FieldPoint sitter = P(5f, -5f, 2.5f);
            double sitterAte = 0.0;
            double walkerAte = 0.0;
            FieldPoint walker = P(15f, -5f, 2.5f);

            for (int step = 0; step < 40; step++)
            {
                walker = P((15f + 0.5f * step) % 20f, -5f, 2.5f);

                field.ClearDemand();
                field.Demand(sitter, 0.5f);
                field.Demand(walker, 0.5f);
                field.FreezeAvailability();

                sitterAte += field.Take(sitter, 0.5f * field.ShareAt(sitter));
                walkerAte += field.Take(walker, 0.5f * field.ShareAt(walker));
            }

            float atSitter = field.EdibleDensityAt(sitter);
            float atWalker = field.EdibleDensityAt(walker);
            float nearby = field.EdibleDensityAt(P(5f, -8f, 2.5f));

            _output.WriteLine(
                $"sitter ate {sitterAte:0.00} J and sits in {atSitter:0.000} J/m³; " +
                $"walker ate {walkerAte:0.00} J and stands in {atWalker:0.000} J/m³; " +
                $"three metres below the sitter: {nearby:0.000} J/m³");

            Assert.True(walkerAte > sitterAte * 1.5, "moving blindly did not out-eat sitting still");
            Assert.True(atSitter < 0.25f * atWalker, "the sitter's water is not a hole");
            Assert.True(atSitter < 0.5f * nearby, "the hole has no edge, so there is no gradient to read");
        }

        [Fact]
        public void TheRefugeHidesWhatSinksIntoIt()
        {
            VertexField field = Field(refuge: 2f, refugeFraction: 0f);
            FieldPoint deep = P(10f, -19f, 2.5f);
            field.Deposit(deep, 5f);

            Assert.True(field.DensityAt(deep) > 0f);
            Assert.Equal(0f, field.EdibleDensityAt(deep));
            Assert.Equal(0.0, field.ReachableStock(deep));

            field.ClearDemand();
            field.Demand(deep, 1f);
            field.FreezeAvailability();
            Assert.Equal(0f, field.Take(deep, 1f));
            Assert.Equal(5.0, field.TotalJoules, 9);
        }

        [Fact]
        public void TheSameSeedGivesTheSameWater()
        {
            VertexField a = Field(seed: 11UL);
            VertexField b = Field(seed: 11UL);

            foreach (VertexField field in new[] { a, b })
            {
                field.SeedUniform(0.5f);
                field.Emit(2.0, new Float3(10f, -1f, 2.5f), new Float3(2f, 0.5f, 2f));
                for (int step = 0; step < 5; step++)
                {
                    field.Mix(1f, 0.2f, 0.05f);
                    field.Cull();
                }
            }

            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a.PositionOf(i), b.PositionOf(i));
                Assert.Equal(a.JoulesOf(i), b.JoulesOf(i));
            }
        }

        [Fact]
        public void APointWithoutAPositionIsRefused()
        {
            VertexField field = Field();
            Assert.Throws<InvalidOperationException>(() => field.Deposit(FieldPoint.At(-5f, 0), 1f));
            Assert.Throws<InvalidOperationException>(() => field.DensityAt(FieldPoint.At(-5f, 0)));
        }

        [Fact]
        public void AVertexWorldNeedsSharedSpace()
        {
            var config = new RunConfig { Light = new LightModel(100f, 12f), FieldModel = MatterField.Vertices };
            Assert.Throws<ArgumentException>(() => new World(config, seed: 1));
        }

        [Fact]
        public void AVertexWorldClosesItsAuditAndItsMatterIdentity()
        {
            // The whole economy on the new water: founders, feeding, exudation, death, matter
            // drawn at conception and returned at death, an influx founding vertices, burial
            // removing them, sinking, mixing and a rolling current carrying them. §5A.2's audit
            // is a hard equality and D074's matter identity is another; both must close.
            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Vertices,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Patches,
                WorldDepthMetres = Depth,
                MatterInfluxPerSecond = 0.05f,
                MatterBurialPerSecond = 0.001f,
                NutrientMixingDiffusivity = 0.2f,
                MatterMixingDiffusivity = 0.2f,
                Current = new CurrentField
                {
                    Speed = 0.1f, CellMetres = 10f, PeriodSeconds = 600f, Rolls = true,
                    AdvectFields = true, RollBlinkSeconds = 300f, VentDepthMetres = Depth,
                },
            };

            var world = new World(config, seed: 3);
            Assert.IsType<VertexField>(world.Nutrients);
            Assert.IsType<VertexField>(world.Matter);

            for (int step = 0; step < 3_000; step++) world.Step(1f);

            var detritus = (VertexField)world.Nutrients;
            var matter = (VertexField)world.Matter;
            double identity =
                world.MatterInitialTotal + world.MatterInfluxedTotal - world.MatterBuriedTotal - world.StandingMatter;

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}; " +
                $"detritus {detritus}; matter {matter}; " +
                $"audit residual {world.AuditResidual:R} of {world.EnergyIn:0} J in; " +
                $"matter identity {identity:R} of {world.MatterInitialTotal:0}");

            Assert.True(world.Births > 0, "nothing was born, so the economy was not exercised");
            Assert.Equal(0L, world.ConceptionsShortOfMatter);
            Assert.True(matter.Count > 0);
            Assert.True(Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn), $"audit residual {world.AuditResidual:R}");
            Assert.True(Math.Abs(identity) <= 1e-6 * world.MatterInitialTotal, $"matter identity {identity:R}");
        }
    }
}

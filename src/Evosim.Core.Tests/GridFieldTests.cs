using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The water as a 3D grid of cells, <c>fable-propose-grid.md</c>. Held to the contracts
    /// <see cref="VertexFieldTests"/> holds the vertices to: conservation under every operator,
    /// exact sharing under the frozen-availability rule, a take that delivers what the gate
    /// promised, a refuge that hides, and the property both fields exist for, that a still mouth
    /// eats a hole and a moving one does not.
    /// </summary>
    public class GridFieldTests
    {
        private readonly ITestOutputHelper _output;

        public GridFieldTests(ITestOutputHelper output) => _output = output;

        // A 144 m² box of four patches: 24 m round the x ring, 6 m across z, 24 m deep. Cells of
        // 1 m and of 3 m both divide it, which is what lets one geometry carry the unit tests and
        // the world-closure test alike.
        private const float Area = 144f;
        private const int Patches = 4;
        private const float Depth = 24f;
        private const float Width = 6f;

        private static GridField Field(
            float sink = 0f, float refuge = 0f, float refugeFraction = 0f, float cell = 1f) =>
            new GridField(Area, sink, Depth, refuge, refugeFraction, Patches, cell);

        private static FieldPoint P(float x, float y, float z) =>
            new FieldPoint(new Float3(x, y, z), (int)Math.Floor(x / Width) % Patches);

        /// <summary>
        /// A tube one cell tall and one cell wide, for the advection tests: with no vertical and
        /// no z faces the only transport left is along x, so a spread measured there is the
        /// scheme's own and not the roll's vertical leg leaking into the reading.
        /// </summary>
        private static GridField Tube(int cells) =>
            new GridField(cells, 0f, 1f, 0f, 0f, cells, 1f);

        private static double MinCell(GridField field)
        {
            double min = double.MaxValue;
            for (int ix = 0; ix < field.CellsX; ix++)
            for (int iy = 0; iy < field.CellsY; iy++)
            for (int iz = 0; iz < field.CellsZ; iz++)
            {
                double m = field.JoulesAt(ix, iy, iz);
                if (m < min) min = m;
            }

            return min;
        }

        [Fact]
        public void ASeededGridReadsBackItsDensityAndItsTotal()
        {
            // A grid read is exact, not a quadrature: a uniform seed reads back its own density
            // anywhere inside, to the last bit the division allows. If this fails, every price in
            // the world is off by the same factor.
            GridField field = Field();
            field.SeedUniform(1f);

            Assert.Equal(24, field.CellsX);
            Assert.Equal(24, field.CellsY);
            Assert.Equal(6, field.CellsZ);
            Assert.Equal(24 * 24 * 6, field.CellCount);
            Assert.Equal(1.0 * 24 * 6 * 24, field.TotalJoules, 6);
            Assert.Equal(field.TotalJoules, field.Recount(), 9);

            foreach (var (x, y, z) in new[] { (12f, -12f, 3f), (3.3f, -4.1f, 1.2f), (21.9f, -19.5f, 5.4f) })
            {
                float density = field.DensityAt(P(x, y, z));
                _output.WriteLine($"({x}, {y}, {z}): {density:0.0000} J/m³");
                Assert.Equal(1f, density, 5);
                Assert.Equal(density, field.EdibleDensityAt(P(x, y, z)));
            }
        }

        [Fact]
        public void TwoMouthsInOneCellShareItExactly()
        {
            // The cell field's guarantee, asked of one grid cell: two feeders wanting sixteen
            // joules of ten get five each, the cell is empty, and nothing went negative.
            GridField field = Field();
            FieldPoint p = P(10.2f, -10.3f, 2.5f);
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
            Assert.True(MinCell(field) >= 0.0);
        }

        [Fact]
        public void ThirtyMouthsSpreadOverCellsNeverOverdraw()
        {
            // Thirty feeders a third of a metre apart, three or four to a cell, each wanting far
            // more than a cell holds. The takes must sum to exactly what the shares promised, and
            // no cell may go below zero.
            GridField field = Field();
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

            _output.WriteLine($"promised {promised:0.000} J, taken {taken:0.000} J, min cell {MinCell(field):R} J");

            Assert.True(promised < Feeders * Wanted, "the water was not short, so the test asked too little");
            Assert.Equal(promised, taken, 4);
            Assert.Equal(before - taken, field.TotalJoules, 6);
            Assert.Equal(field.TotalJoules, field.Recount(), 6);
            Assert.True(MinCell(field) >= -1e-9, $"a cell went to {MinCell(field):R} J");
        }

        [Fact]
        public void ATakeDeliversWhatTheGatePromised()
        {
            // The vertex field had to fill in several passes to deliver what ReachableStock
            // promised (logbook/0074). A grid cell is one number, so the promise is the cell and
            // the delivery is a subtraction, and a take is min(asked, reachable) exactly.
            GridField field = Field();
            field.SeedUniform(1f);
            FieldPoint p = P(10.5f, -10.5f, 2.5f);

            double reachable = field.ReachableStock(p);
            Assert.Equal(1.0, reachable, 9);

            float taken = field.Take(p, (float)(reachable * 0.95));
            Assert.Equal(reachable * 0.95, taken, 6);

            // Asking for more than there is delivers everything there is, and no more.
            float rest = field.Take(p, 100f);
            Assert.Equal(reachable * 0.05, rest, 5);
            Assert.Equal(0f, field.Take(p, 1f));
            Assert.Equal(0.0, field.ReachableStock(p), 9);
            Assert.True(MinCell(field) >= 0.0);
        }

        [Fact]
        public void EveryOperatorConservesTheTotal()
        {
            GridField field = Field(sink: 0.01f);
            field.SeedUniform(1f);
            field.Deposit(P(4.5f, -3.5f, 1.5f), 3f);
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
                field.Mix(1f, 0.15f, 0.15f);
                field.Advect(current, 100d + step, 1f, field.PatchWidthMetres);
                field.Cull();
            }

            // The running total is untouched by the transport passes, because each is a flux out
            // of one cell and into another; Recount is the array's own sum, and the two agreeing
            // is the check that no pass quietly created or dropped anything.
            Assert.Equal(expected, field.TotalJoules, 9);
            Assert.Equal(expected, field.Recount(), 6);
            Assert.True(MinCell(field) >= -1e-12, $"a cell went to {MinCell(field):R} J");
        }

        [Fact]
        public void FickIsLinearInTheDifferenceAndLeavesAUniformFieldAlone()
        {
            GridField uniform = Field();
            uniform.SeedUniform(2f);
            uniform.Mix(1f, 0.125f);

            for (int ix = 0; ix < uniform.CellsX; ix += 7)
            for (int iy = 0; iy < uniform.CellsY; iy += 7)
            for (int iz = 0; iz < uniform.CellsZ; iz += 5)
            {
                Assert.Equal(2.0, uniform.JoulesAt(ix, iy, iz), 12);
            }

            // A cell twice as far above its neighbours loses twice as fast: six faces, each
            // carrying the fraction times the difference, and nothing else.
            FieldPoint p = P(10.5f, -10.5f, 2.5f);
            GridField one = Field();
            GridField two = Field();
            one.Deposit(p, 1f);
            two.Deposit(p, 2f);

            // 0.125 is exactly representable, so the six faces carry exactly three quarters of the
            // difference and the reading is the scheme rather than a float's rounding.
            one.Mix(1f, 0.125f);
            two.Mix(1f, 0.125f);

            double lostOne = 1.0 - one.ReachableStock(p);
            double lostTwo = 2.0 - two.ReachableStock(p);

            _output.WriteLine($"a 1 J cell lost {lostOne:0.000000} J, a 2 J cell lost {lostTwo:0.000000} J");

            Assert.Equal(0.75, lostOne, 12);
            Assert.Equal(2.0 * lostOne, lostTwo, 12);
        }

        [Fact]
        public void MixingIsRefusedAboveTheLimitAndWithAnUnequalHorizontalRate()
        {
            GridField field = Field();
            field.SeedUniform(1f);

            // 0.2 m²/s over 1 s in a 1 m cell is 0.2, above the 1/6 a six-faced cell allows.
            ArgumentException tooFast = Assert.Throws<ArgumentException>(() => field.Mix(1f, 0.2f));
            Assert.Contains("1/6", tooFast.Message);
            _output.WriteLine(tooFast.Message);

            // The same diffusivity over half a second is 0.1, and passes.
            field.Mix(0.5f, 0.2f);

            ArgumentException uneven = Assert.Throws<ArgumentException>(() => field.Mix(0.5f, 0.2f, 0.05f));
            Assert.Contains("D084", uneven.Message);
            _output.WriteLine(uneven.Message);

            // Equal, or absent, are the two readings a grid can honour.
            field.Mix(0.5f, 0.2f, 0.2f);
            field.Mix(0.5f, 0.2f, 0f);
        }

        [Fact]
        public void AdvectionCarriesAPatchDownstreamAndNotUpstream()
        {
            // A tube of forty cells under a steady horizontal flow of 0.3 m/s. The world clock is
            // held fixed so the current does not turn over during the reading: what is being
            // measured is the scheme, not the roll.
            // Sixty cells and a start in the middle, so that twenty hops of one cell cannot reach
            // the seam: the tail of a binomial is small but it is not zero, and 0.15^20 of the
            // deposit arriving back round the ring would read as flow going upstream.
            const int Cells = 60;
            const float Dt = 0.5f;
            const int Steps = 20;
            const double Clock = 137d;

            GridField field = Tube(Cells);
            var current = new CurrentField
            {
                Speed = 1f, CellMetres = 10f, PeriodSeconds = 600f, AdvectFields = true,
            };

            float faceY = -0.5f;
            float unit = current.VelocityAt(faceY, Clock, 0, Cells).X;
            Assert.True(Math.Abs(unit) > 1e-3f, "the sample time gives no horizontal flow to measure");
            current.Speed = 0.3f / Math.Abs(unit);

            double u = current.VelocityAt(faceY, Clock, 0, Cells).X;
            Assert.Equal(0.3, Math.Abs(u), 4);

            const int Start = 30;
            field.Deposit(P(Start + 0.5f, -0.5f, 0.5f), 1f);
            double before = field.TotalJoules;

            (double mean, double variance) start = Moments(field);

            for (int step = 0; step < Steps; step++) field.Advect(current, Clock, Dt, field.PatchWidthMetres);

            (double mean, double variance) end = Moments(field);

            double elapsed = Steps * Dt;
            double courant = Math.Abs(u) * Dt / 1.0;
            double spread = (end.variance - start.variance) / elapsed;
            double effectiveDiffusivity = 0.5 * spread;

            _output.WriteLine(
                $"u {u:0.0000} m/s, cell 1 m, dt {Dt} s, Courant {courant:0.000}: " +
                $"centre of mass {start.mean:0.000} -> {end.mean:0.000} m over {elapsed:0.#} s; " +
                $"variance {start.variance:0.0000} -> {end.variance:0.0000} m², " +
                $"spread {spread:0.0000} m²/s, effective diffusivity {effectiveDiffusivity:0.0000} m²/s");

            // Upwind advection moves the first moment at exactly the flow speed, whatever the
            // Courant number, so this is an equality and not an approximation.
            Assert.Equal(start.mean + u * elapsed, end.mean, 6);
            Assert.Equal(before, field.Recount(), 9);
            Assert.True(effectiveDiffusivity > 0d && !double.IsInfinity(effectiveDiffusivity));

            // Nothing goes the other way: a one-sided scheme has no upstream face to give through.
            for (int i = 0; i < Cells; i++)
            {
                bool upstream = u > 0d ? i < Start : i > Start;
                if (upstream) Assert.Equal(0.0, field.JoulesAt(i, 0, 0));
            }
        }

        [Fact]
        public void AdvectionWrapsAtTheSeam()
        {
            const int Cells = 8;
            const float Dt = 1f;
            const double Clock = 137d;

            GridField field = Tube(Cells);
            var current = new CurrentField
            {
                Speed = 1f, CellMetres = 10f, PeriodSeconds = 600f, AdvectFields = true,
            };

            float unit = current.VelocityAt(-0.5f, Clock, 0, Cells).X;
            current.Speed = 0.5f / Math.Abs(unit);
            double u = current.VelocityAt(-0.5f, Clock, 0, Cells).X;

            int last = u > 0d ? Cells - 1 : 0;
            int across = u > 0d ? 0 : Cells - 1;

            field.Deposit(P(last + 0.5f, -0.5f, 0.5f), 1f);
            field.Advect(current, Clock, Dt, field.PatchWidthMetres);

            _output.WriteLine(
                $"u {u:0.000} m/s: cell {last} kept {field.JoulesAt(last, 0, 0):0.0000} J, " +
                $"cell {across} across the seam received {field.JoulesAt(across, 0, 0):0.0000} J");

            Assert.True(field.JoulesAt(across, 0, 0) > 0.0, "nothing crossed the seam");
            Assert.Equal(1.0, field.Recount(), 12);
        }

        [Fact]
        public void TheRefugeHidesWhatSinksIntoIt()
        {
            GridField field = Field(refuge: 2f, refugeFraction: 0f);
            FieldPoint deep = P(10.5f, -23.5f, 2.5f);
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
        public void AStillMouthEatsAHoleAndAMovingOneDoesNot()
        {
            // The reason the field exists, ported from VertexFieldTests. Two identical blind
            // mouths in identical water, one fixed and one crossing a cell a step. Neither senses
            // anything. The walker eats far more, and the water where the sitter sits is a hole
            // with an edge on it.
            GridField field = Field();
            field.SeedUniform(1f);

            FieldPoint sitter = P(1.5f, -5f, 2.5f);
            double sitterAte = 0.0;
            double walkerAte = 0.0;
            FieldPoint walker = P(4.5f, -5f, 2.5f);

            for (int step = 0; step < 18; step++)
            {
                walker = P(4.5f + step, -5f, 2.5f);

                field.ClearDemand();
                field.Demand(sitter, 0.5f);
                field.Demand(walker, 0.5f);
                field.FreezeAvailability();

                sitterAte += field.Take(sitter, 0.5f * field.ShareAt(sitter));
                walkerAte += field.Take(walker, 0.5f * field.ShareAt(walker));
            }

            float atSitter = field.EdibleDensityAt(sitter);
            float atWalker = field.EdibleDensityAt(walker);
            float nearby = field.EdibleDensityAt(P(1.5f, -8f, 2.5f));

            _output.WriteLine(
                $"sitter ate {sitterAte:0.00} J and sits in {atSitter:0.000} J/m³; " +
                $"walker ate {walkerAte:0.00} J and stands in {atWalker:0.000} J/m³; " +
                $"three metres below the sitter: {nearby:0.000} J/m³");

            Assert.True(walkerAte > sitterAte * 1.5, "moving blindly did not out-eat sitting still");
            Assert.True(atSitter < 0.25f * atWalker, "the sitter's water is not a hole");
            Assert.True(atSitter < 0.5f * nearby, "the hole has no edge, so there is no gradient to read");
        }

        [Fact]
        public void ACrowdInOneCellTakesNothingFromTheNeighbouringCells()
        {
            // The owner's geometry, 2026-09-08. Five mouths packed into one cell and five spread
            // one to a neighbouring cell. With mixing off the crowd's feeding must not reach the
            // neighbours at all: a grid cell has walls, and that is the whole difference from a
            // kernel, which would have let the crowd draw on water its neighbours were pricing.
            GridField field = Field();
            field.SeedUniform(1f);

            FieldPoint[] crowd =
            {
                P(10.1f, -10.1f, 2.1f), P(10.3f, -10.3f, 2.3f), P(10.5f, -10.5f, 2.5f),
                P(10.7f, -10.7f, 2.7f), P(10.9f, -10.9f, 2.9f),
            };

            FieldPoint[] neighbours =
            {
                P(9.5f, -10.5f, 2.5f), P(11.5f, -10.5f, 2.5f), P(10.5f, -9.5f, 2.5f),
                P(10.5f, -11.5f, 2.5f), P(10.5f, -10.5f, 3.5f),
            };

            var beforeNeighbour = new double[neighbours.Length];
            for (int i = 0; i < neighbours.Length; i++) beforeNeighbour[i] = field.ReachableStock(neighbours[i]);

            // Part one: only the crowd feeds, and each of them wants the whole cell.
            field.ClearDemand();
            foreach (FieldPoint p in crowd) field.Demand(p, 1f);
            field.FreezeAvailability();

            double crowdAte = 0.0;
            foreach (FieldPoint p in crowd) crowdAte += field.Take(p, 1f * field.ShareAt(p));

            Assert.Equal(1.0, crowdAte, 6);
            Assert.Equal(0.0, field.ReachableStock(crowd[0]), 9);
            for (int i = 0; i < neighbours.Length; i++)
            {
                Assert.Equal(beforeNeighbour[i], field.ReachableStock(neighbours[i]), 12);
            }

            // Part two: all ten feed in fresh water, and the reading is what each group gets.
            GridField second = Field();
            second.SeedUniform(1f);

            second.ClearDemand();
            foreach (FieldPoint p in crowd) second.Demand(p, 1f);
            foreach (FieldPoint p in neighbours) second.Demand(p, 1f);
            second.FreezeAvailability();

            double packed = 0.0;
            foreach (FieldPoint p in crowd) packed += second.Take(p, 1f * second.ShareAt(p));

            double spread = 0.0;
            foreach (FieldPoint p in neighbours) spread += second.Take(p, 1f * second.ShareAt(p));

            _output.WriteLine(
                $"five mouths in one cell took {packed:0.0000} J between them ({packed / crowd.Length:0.0000} J each); " +
                $"five mouths one to a cell took {spread:0.0000} J ({spread / neighbours.Length:0.0000} J each)");

            Assert.Equal(1.0, packed, 6);
            Assert.Equal(5.0, spread, 6);
        }

        [Fact]
        public void APointWithoutAPositionIsRefused()
        {
            GridField field = Field();
            Assert.Throws<InvalidOperationException>(() => field.Deposit(FieldPoint.At(-5f, 0), 1f));
            Assert.Throws<InvalidOperationException>(() => field.DensityAt(FieldPoint.At(-5f, 0)));
        }

        /// <summary>
        /// A NaN coordinate is a diverged body, and the refusal has to say so.
        /// </summary>
        /// <remarks>
        /// <c>r35old-s3</c> died at 618.5 s on 2026-09-10 with the field reporting a point "made
        /// with FieldPoint.At" that no caller had made that way: <c>CreatureSensors.Sample</c>
        /// had handed over a part's own transform and the transform was NaN, and the old
        /// <c>Validated</c> asked <c>HasHorizontal</c> first, which is false for NaN either way.
        /// The reader was sent to look for a call that did not exist while the real fault, a
        /// diverged link the harness had not killed, stood untouched. Both fields refuse both
        /// shapes; the test asserts the messages do not swap.
        /// </remarks>
        [Fact]
        public void ANonFinitePositionIsRefusedAsOneAndNotAsAMissingPosition()
        {
            GridField field = Field();

            foreach (Float3 lost in new[]
            {
                new Float3(float.NaN, float.NaN, float.NaN),          // a part the solver lost
                new Float3(float.NaN, -5f, 2.5f),                     // x alone
                new Float3(10.5f, -5f, float.NaN),                    // z alone
                new Float3(float.PositiveInfinity, -5f, 2.5f),
                new Float3(10.5f, float.NaN, 2.5f),                   // the depth alone
            })
            {
                var at = new FieldPoint(lost, 0);

                ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(
                    () => field.DensityAt(at));

                _output.WriteLine($"{lost.X}, {lost.Y}, {lost.Z} -> {refused.Message.Split('\n')[0]}");

                Assert.Contains("A non-finite position", refused.Message);
                Assert.DoesNotContain("FieldPoint.At, which carries no x or z", refused.Message);
            }

            // And the genuine article still reads as itself, with the other message and the other
            // type, so neither refusal has been widened into the other.
            InvalidOperationException missing = Assert.Throws<InvalidOperationException>(
                () => field.DensityAt(FieldPoint.At(-5f, 0)));

            Assert.Contains("FieldPoint.At, which carries no x or z", missing.Message);
            Assert.DoesNotContain("A non-finite position", missing.Message);
        }

        /// <summary>The same two refusals from <see cref="VertexField"/>, which shares the rule.</summary>
        [Fact]
        public void TheVertexFieldMakesTheSameTwoRefusals()
        {
            var field = new VertexField(
                Area, 1f, 0f, Depth, 0f, 0f, Patches,
                kernelMetres: 1f, mergeMetres: 0.25f, vertexCap: 100_000, vertexJoules: 0.125f, seed: 7UL);

            ArgumentOutOfRangeException lost = Assert.Throws<ArgumentOutOfRangeException>(
                () => field.DensityAt(new FieldPoint(new Float3(float.NaN, -5f, 2.5f), 0)));

            Assert.Contains("A non-finite position", lost.Message);
            Assert.DoesNotContain("FieldPoint.At, which carries no x or z", lost.Message);

            InvalidOperationException missing = Assert.Throws<InvalidOperationException>(
                () => field.DensityAt(FieldPoint.At(-5f, 0)));

            Assert.Contains("FieldPoint.At, which carries no x or z", missing.Message);
        }

        [Fact]
        public void ACellThatDoesNotDivideTheBoxIsRefused()
        {
            // 24 m long, 24 m deep, 6 m wide: 5 m divides none of the three, and the message has
            // to say all three so the fix is arithmetic rather than a guess.
            ArgumentException refused = Assert.Throws<ArgumentException>(
                () => new GridField(Area, 0f, Depth, 0f, 0f, Patches, 5f));

            _output.WriteLine(refused.Message);
            Assert.Contains("A cell of 5 m does not divide the box", refused.Message);
            Assert.Contains("24 m long, 24 m deep and 6 m wide", refused.Message);

            // Both of the world's defaults divide this box.
            _ = new GridField(Area, 0f, Depth, 0f, 0f, Patches, 1f);
            _ = new GridField(Area, 0f, Depth, 0f, 0f, Patches, 3f);
        }

        [Fact]
        public void AGridWorldNeedsSharedSpace()
        {
            var config = new RunConfig { Light = new LightModel(100f, 12f), FieldModel = MatterField.Grid };
            ArgumentException refused = Assert.Throws<ArgumentException>(() => new World(config, seed: 1));

            // Named rather than merely typed: a RunConfig can be refused for half a dozen reasons
            // and a bare ArgumentException would pass on any of them.
            Assert.Contains("FieldModel is Grid but SharedSpace is false", refused.Message);
        }

        [Fact]
        public void TheSameSequenceGivesTheSameGrid()
        {
            // Trivially true of a field with no RNG of its own, and asserted anyway: the moment a
            // grid grows a stochastic term this is the test that notices.
            GridField a = Field(sink: 0.01f);
            GridField b = Field(sink: 0.01f);

            var current = new CurrentField
            {
                Speed = 0.2f, CellMetres = 10f, PeriodSeconds = 600f, Rolls = true,
                AdvectFields = true, RollBlinkSeconds = 300f,
            };

            foreach (GridField field in new[] { a, b })
            {
                field.SeedUniform(0.5f);
                field.Deposit(P(7.5f, -2.5f, 1.5f), 4f);
                for (int step = 0; step < 5; step++)
                {
                    field.Settle(0.5f);
                    field.Mix(0.5f, 0.2f, 0.2f);
                    field.Advect(current, 50d + step, 0.5f, field.PatchWidthMetres);
                }
            }

            for (int ix = 0; ix < a.CellsX; ix++)
            for (int iy = 0; iy < a.CellsY; iy++)
            for (int iz = 0; iz < a.CellsZ; iz++)
            {
                Assert.Equal(a.JoulesAt(ix, iy, iz), b.JoulesAt(ix, iy, iz));
            }
        }

        [Fact]
        public void AGridWorldClosesItsAuditAndItsMatterIdentity()
        {
            // The whole economy on the new water, ported from the vertex field's own closure test:
            // founders, feeding, exudation, death, matter drawn at conception and returned at
            // death, an influx landing on the plume's cells, burial, sinking, mixing and a rolling
            // current. §5A.2's audit is a hard equality and D074's matter identity is another.
            //
            // The step is half a second rather than the vertex test's whole one, because explicit
            // diffusion on a 1 m cell at 0.2 m²/s is stable only to 0.83 s and the field refuses a
            // longer step rather than clamping. That is the grid's own constraint, stated here so
            // the number is not mistaken for a choice about the ecology.
            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Patches,
                WorldDepthMetres = Depth,
                MatterInfluxPerSecond = 0.05f,
                MatterBurialPerSecond = 0.001f,
                NutrientMixingDiffusivity = 0.2f,

                // A grid world must state its sideways detritus rate as the vertical one, or
                // World refuses it: the header's h-mix has to name what the cubes actually do.
                HorizontalMixingDiffusivity = 0.2f,
                MatterMixingDiffusivity = 0.2f,
                Current = new CurrentField
                {
                    Speed = 0.1f, CellMetres = 10f, PeriodSeconds = 600f, Rolls = true,
                    AdvectFields = true, RollBlinkSeconds = 300f, VentDepthMetres = Depth,
                },
            };

            var world = new World(config, seed: 3);
            Assert.IsType<GridField>(world.Nutrients);
            Assert.IsType<GridField>(world.Matter);

            for (int step = 0; step < 3_000; step++) world.Step(0.5f);

            var detritus = (GridField)world.Nutrients;
            var matter = (GridField)world.Matter;
            double identity =
                world.MatterInitialTotal + world.MatterInfluxedTotal - world.MatterBuriedTotal - world.StandingMatter;

            _output.WriteLine(
                $"alive {world.Living.Count}, births {world.Births}, deaths {world.Deaths}; " +
                $"detritus {detritus}; matter {matter}; " +
                $"audit residual {world.AuditResidual:R} of {world.EnergyIn:0} J in; " +
                $"matter identity {identity:R} of {world.MatterInitialTotal:0}");

            Assert.True(world.Births > 0, "nothing was born, so the economy was not exercised");
            Assert.Equal(0L, world.ConceptionsShortOfMatter);
            Assert.True(Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.EnergyIn), $"audit residual {world.AuditResidual:R}");
            Assert.True(Math.Abs(identity) <= 1e-6 * world.MatterInitialTotal, $"matter identity {identity:R}");

            // The running totals both fields report must still be the sum of their own cells.
            Assert.Equal(detritus.TotalJoules, detritus.Recount(), 6);
            Assert.Equal(matter.TotalJoules, matter.Recount(), 6);
        }

        [Fact]
        public void AVentInfluxLandsInThePlumeAndTheIdentityStillCloses()
        {
            // The grid's own influx branch. The surface route is shared with the cells, but the
            // vent route is the plume box, and DepositBox spreads it over the cells the box covers
            // where the vertex field would have founded quanta inside it. What the identity says
            // is that every joule handed in is standing somewhere in the world.
            var config = new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Patches,
                WorldDepthMetres = Depth,
                MatterInfluxPerSecond = 0.5f,
                MatterInfluxAt = MatterInflux.Vent,
                MatterBurialPerSecond = 0.001f,

                // Mixing off, so what the floor of the vent's patch holds above its neighbours is
                // the plume and not a stir that has not finished. RunConfig's default matter
                // diffusivity is 2 m²/s, which spreads the plume over the whole box inside a
                // hundred seconds and leaves an excess of one joule to read. The identity below
                // does not care either way; the excess is what says the box landed where it was
                // aimed.
                MatterMixingDiffusivity = 0f,
                Current = new CurrentField { VentDepthMetres = Depth, VentPatch = 1 },
            };

            var world = new World(config, seed: 5);
            var matter = (GridField)world.Matter;

            double seeded = world.MatterInitialTotal;
            for (int step = 0; step < 200; step++) world.Step(0.5f);

            // The plume's own floor layer in the vent's patch, and no other patch's.
            int floor = matter.LayerCount - 1;
            double inVent = matter.StockInLayer(floor, 1);
            double elsewhere =
                (matter.StockInLayer(floor, 0) + matter.StockInLayer(floor, 2) + matter.StockInLayer(floor, 3)) / 3d;
            double identity =
                world.MatterInitialTotal + world.MatterInfluxedTotal - world.MatterBuriedTotal - world.StandingMatter;

            _output.WriteLine(
                $"influxed {world.MatterInfluxedTotal:0.000} J into patch 1's floor; " +
                $"that floor holds {inVent:0.000} J against {elsewhere:0.000} J in an average other patch, " +
                $"an excess of {inVent - elsewhere:0.000} J; matter identity {identity:R} of {seeded:0}");

            Assert.True(world.MatterInfluxedTotal > 0d, "the influx deposited nothing");
            Assert.True(
                inVent - elsewhere >= 0.5 * world.MatterInfluxedTotal,
                "the plume did not land in the vent's patch");
            Assert.True(Math.Abs(identity) <= 1e-6 * seeded, $"matter identity {identity:R}");
            Assert.Equal(matter.TotalJoules, matter.Recount(), 6);
        }

        [Fact]
        public void AGridWorldRunsTheCampaignsMixingAndRefusesAnUnequalOne()
        {
            // The isotropy trap. World.Step hands one HorizontalMixingDiffusivity to both fields,
            // and a grid matter field at 2 m2/s would be refused by GridField.Mix on the first
            // step against a sideways 0.02. Worse, the only value that got past it was 0, which
            // the grid then reads as "stir sideways at the vertical rate": a header saying h-mix 0
            // for a world mixing sideways at 2. So the matter grid now takes its own rate on every
            // axis, and a world whose h-mix disagrees with its vertical detritus rate is refused
            // at construction, which keeps the header's h-mix equal to what the detritus does.
            RunConfig Campaign(float horizontal) => new RunConfig
            {
                Light = new LightModel(100f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = Area,
                HorizontalPatches = Patches,
                WorldDepthMetres = Depth,
                NutrientMixingDiffusivity = 0.02f,
                HorizontalMixingDiffusivity = horizontal,
                MatterMixingDiffusivity = 2f,
            };

            var world = new World(Campaign(0.02f), seed: 9);
            for (int step = 0; step < 40; step++) world.Step(0.5f);

            var detritus = (GridField)world.Nutrients;
            var matter = (GridField)world.Matter;
            _output.WriteLine(
                $"forty steps at nutrient 0.02, h-mix 0.02, matter 2: detritus {detritus}; matter {matter}");

            Assert.Equal(detritus.TotalJoules, detritus.Recount(), 6);
            Assert.Equal(matter.TotalJoules, matter.Recount(), 6);

            ArgumentException refused = Assert.Throws<ArgumentException>(() => new World(Campaign(0.2f), seed: 9));
            _output.WriteLine(refused.Message);
            Assert.Contains("HorizontalMixingDiffusivity", refused.Message);
            Assert.Contains("D084", refused.Message);
        }

        [Fact]
        public void TakeFromLayerOnTheCellFieldIsTheOldTakeExactly()
        {
            // The one edit in this build that could break every cell-world replay. BuryMatter used
            // to cast to NutrientField and call Take(floorY, wanted, patch) with floorY derived
            // from the layer; it now calls TakeFromLayer(layer, patch, wanted) through the
            // interface. Two identical fields, the same draws down the two routes, and every cell
            // must be left holding the same number. The sweep includes a request larger than the
            // stock, a non-positive request, and a refuge layer at both fractions, since those are
            // the branches where the two could diverge.
            foreach (float refugeMetres in new[] { 0f, 2f })
            foreach (float refugeFraction in new[] { 0f, 0.3f })
            {
                var oldWay = new NutrientField(Area, 1f, 0f, Depth, refugeMetres, refugeFraction, Patches);
                var newWay = new NutrientField(Area, 1f, 0f, Depth, refugeMetres, refugeFraction, Patches);

                for (int layer = 0; layer < oldWay.LayerCount; layer++)
                for (int patch = 0; patch < Patches; patch++)
                {
                    float at = -((layer + 0.5f) * oldWay.LayerMetres);
                    float joules = 1f + 0.5f * layer + patch;
                    oldWay.Deposit(at, joules, patch);
                    newWay.Deposit(at, joules, patch);
                }

                int[] layers = { 0, 1, 7, oldWay.LayerCount - 2, oldWay.LayerCount - 1 };
                float[] amounts = { 0.25f, 3f, 1000f, 0f, -1f };

                foreach (int layer in layers)
                foreach (int patch in new[] { 0, 2, 3 })
                foreach (float wanted in amounts)
                {
                    float floorY = -((layer + 0.5f) * oldWay.LayerMetres);
                    float before = (float)oldWay.StockInLayer(layer, patch);
                    float was = oldWay.Take(floorY, wanted, patch);
                    double now = newWay.TakeFromLayer(layer, patch, wanted);

                    Assert.Equal(was, (float)now);
                    Assert.Equal(
                        oldWay.StockInLayer(layer, patch), newWay.StockInLayer(layer, patch));
                    Assert.True(newWay.StockInLayer(layer, patch) >= 0.0, $"{before} J went negative");
                }

                for (int layer = 0; layer < oldWay.LayerCount; layer++)
                for (int patch = 0; patch < Patches; patch++)
                {
                    Assert.Equal(oldWay.StockInLayer(layer, patch), newWay.StockInLayer(layer, patch));
                }

                Assert.Equal(oldWay.TotalJoules, newWay.TotalJoules);
            }
        }

        /// <summary>Mass, mean x and variance in x of a tube, read from the cells themselves.</summary>
        private static (double Mean, double Variance) Moments(GridField field)
        {
            double mass = 0d, first = 0d, second = 0d;
            for (int ix = 0; ix < field.CellsX; ix++)
            {
                double m = field.JoulesAt(ix, 0, 0);
                if (m == 0d) continue;
                double x = ix + 0.5;
                mass += m;
                first += m * x;
                second += m * x * x;
            }

            double mean = first / mass;
            return (mean, second / mass - mean * mean);
        }
    }
}

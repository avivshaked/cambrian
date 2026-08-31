using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>Moving water and the energy it returns — DESIGN.md §5A.4, D036.</summary>
    public class CurrentAndMixingTests
    {
        private readonly ITestOutputHelper _output;

        public CurrentAndMixingTests(ITestOutputHelper output) => _output = output;

        private static CurrentField Flowing(float speed = 0.05f) =>
            new CurrentField { Speed = speed, CellMetres = 25f, PeriodSeconds = 300f };

        [Fact]
        public void StillWaterIsExactlyStill()
        {
            // Default off, and off has to mean bit-identical rather than nearly. Every result on
            // file was measured in still water, and a default that perturbed anything would mean
            // none of them described a world that still exists — which is what §5A.2b turned out
            // to be (D031) and is not a thing to do twice deliberately.
            var still = new CurrentField();

            for (float y = 0f; y > -60f; y -= 0.37f)
            {
                for (double t = 0d; t < 500d; t += 7.3d)
                {
                    Assert.Equal(Float3.Zero, still.VelocityAt(y, t));
                }
            }
        }

        [Fact]
        public void TheTimeAverageAtAFixedDepthGoesToZero()
        {
            // A field with a nonzero time-mean is a conveyor belt: it carries every creature and
            // every particle steadily one way and piles the world against a boundary, and it does
            // so slowly enough to read as an ecological result for a very long time.
            //
            // It is never exactly zero over a finite window, and that is not a defect. The two
            // terms have incommensurate periods on purpose, so no interval holds a whole number of
            // both cycles and there is always a partial one left over. What separates that residual
            // from a real bias is that it shrinks as the window grows — so the test is that
            // quadrupling the window shrinks the mean, not that the mean is small, which a slow
            // conveyor would also satisfy.
            CurrentField current = Flowing();

            foreach (float y in new[] { -0.5f, -3.7f, -12.4f, -25f, -31.9f, -58.2f })
            {
                double near = Math.Abs(current.MeanVerticalOver(y, periods: 16));
                double far = Math.Abs(current.MeanVerticalOver(y, periods: 256));

                _output.WriteLine($"{y,7:0.#} m: {near:0.000000} -> {far:0.000000} m/s");

                Assert.True(
                    far < near * 0.5 || far < 1e-7,
                    $"at {y} m the mean was {near:R} over 16 periods and {far:R} over 256 — " +
                    "it is not converging to zero, which makes it a drift rather than a remainder");

                // And it is small in absolute terms either way, so this cannot pass by starting
                // from something enormous and merely halving.
                Assert.True(far < 1e-3 * current.Speed, $"mean {far:R} m/s at {y} m");
            }
        }

        [Fact]
        public void TheFlowDispersesWithoutCarryingAnythingAway()
        {
            // The test that should have existed first, and whose absence put a whole population six
            // metres into the air (logbook/0022). TheTimeAverageIsZeroAtEveryDepth asks what the
            // water does at a fixed point; this asks what happens to something the water carries.
            // A travelling wave has zero of the first and a great deal of the second, because a
            // particle rides along with the phase — so the earlier test passed while the field was
            // acting as a conveyor belt.
            //
            // Two requirements in tension, which is why they are asserted together:
            //
            //   Individual particles MUST wander, or nothing is mixed and a creature born deep
            //   stays deep — the determinism this field exists to break. A single standing wave
            //   returns every particle exactly home every cycle and would fail this.
            //
            //   The POPULATION must not go anywhere, or the field is a conveyor and the world
            //   empties into a boundary while looking like ecology the whole way down.
            //
            // So: large mean absolute displacement, near-zero mean signed displacement. Individual
            // drifts of a few metres are the mechanism working, not a fault.
            CurrentField current = Flowing();
            const double Seconds = 6000d;   // twenty periods, far longer than anything lives

            var depths = new[]
            {
                -2.3f, -5.7f, -9.1f, -13.8f, -17.6f, -22.9f, -28.4f, -35.1f, -41.2f, -48.6f,
            };

            double signed = 0d, absolute = 0d;

            foreach (float y in depths)
            {
                double drift = current.DriftOf(y, Seconds);
                signed += drift;
                absolute += Math.Abs(drift);
            }

            signed /= depths.Length;
            absolute /= depths.Length;

            _output.WriteLine(
                $"over {Seconds} s: mean signed {signed:0.###} m, mean absolute {absolute:0.###} m");

            Assert.True(
                absolute > 0.5,
                $"particles moved {absolute:0.###} m on average — the water is mixing nothing");

            Assert.True(
                Math.Abs(signed) < absolute * 0.5,
                $"mean signed displacement {signed:0.###} m against {absolute:0.###} m of " +
                "wandering — the population is being carried somewhere");
        }

        [Fact]
        public void DepthsShearPastEachOther()
        {
            // The property that makes a current break birth-depth determinism rather than merely
            // move everybody together. A uniform drift is divergence-free and useless here: every
            // creature keeps its rank, and a creature born deep is still deep. What is needed is
            // for neighbouring depths to move differently, and this asserts it.
            CurrentField current = Flowing();

            // Sampled away from t=0, where every term of the field is exactly zero by
            // construction and any two depths agree trivially.
            const double When = 71d;

            float top = current.VelocityAt(-5f, When).Y;
            float bottom = current.VelocityAt(-5f - 12.5f, When).Y;

            Assert.True(
                Math.Abs(top - bottom) > 0.2f * current.Speed,
                $"half a cell apart at t={When} s the water moved at {top:R} and {bottom:R} m/s");
        }

        [Fact]
        public void MixingConservesEveryJoule()
        {
            // §5A.2's audit is a hard equality, not a plausibility check, and this runs inside it.
            // Fluxes are computed across interfaces rather than as per-layer averages precisely so
            // that conservation cannot depend on the timestep being small.
            var field = new NutrientField(400f, 1f, 0f, 60f);

            field.Deposit(-59.5f, 10000d > 0 ? 10000f : 0f);
            field.Deposit(-30.5f, 250f);
            field.Deposit(-0.5f, 40f);

            double before = field.TotalJoules;

            for (int i = 0; i < 5000; i++) field.Mix(0.5f, 2f);

            Assert.Equal(before, field.TotalJoules, 6);
        }

        [Fact]
        public void MixingNeverDrivesALayerNegative()
        {
            // Explicit diffusion goes unstable above a Courant number of one half and oscillates a
            // layer below zero — which conservation would faithfully preserve, leaving a world
            // with a debt of detritus in one layer and a surplus in the next. The step is clamped
            // there rather than sub-stepped, so an absurd diffusivity is a slower stir than asked
            // for instead of a different physics.
            var field = new NutrientField(400f, 1f, 0f, 60f);
            field.Deposit(-30.5f, 1000f);

            for (int i = 0; i < 200; i++) field.Mix(10f, 1e6f);

            for (int layer = 0; layer < field.LayerCount; layer++)
            {
                Assert.True(
                    field.StockInLayer(layer) >= -1e-9,
                    $"layer {layer} holds {field.StockInLayer(layer):R} J");
            }

            Assert.Equal(1000d, field.TotalJoules, 6);
        }

        [Fact]
        public void MixingTurnsAStepIntoAGradient()
        {
            // The whole point, stated as a measurement. Without mixing, detritus sinks to the floor
            // and a creature that dives one metre gains nothing, thirty metres gains nothing, and
            // fifty-nine gains everything — a step function approached downhill through failing
            // light, which is a deceptive task [K12] rather than a hard one. With mixing the same
            // detritus is spread through the column and one metre of diving is worth one metre of
            // food.
            // Run to steady state, not through a transient. At 0.02 m/s a plume needs 3000 s to
            // fall sixty metres, so a shorter run compares two clouds still in mid-descent and the
            // un-mixed one looks *better* because its slug happens to be passing the sample depth.
            // That is what the first version of this test measured, and it is the same mistake as
            // reading a population mid-doubling (logbook/0017).
            const int Seconds = 12000;
            const float Deposited = 12000f;

            var sinking = new NutrientField(400f, 1f, 0.02f, 60f);
            var stirred = new NutrientField(400f, 1f, 0.02f, 60f);

            for (int i = 0; i < Seconds; i++)
            {
                // Deposited at the surface, because that is where things die: the population sits
                // in the light and its corpses start their journey there.
                sinking.Deposit(-1.5f, Deposited / Seconds);
                stirred.Deposit(-1.5f, Deposited / Seconds);

                sinking.Settle(1f);
                stirred.Settle(1f);
                stirred.Mix(1f, 2f);
            }

            // Floor density against column density, which is the number the word "step" actually
            // means. It is not that the column is empty — with things dying continuously there is
            // always material in transit — it is that the floor is two orders of magnitude denser
            // than anywhere a creature could reach, so all the reward sits past a cliff.
            float columnMetres = sinking.LayerMetres;
            double sinkingFloor = sinking.StockInLayer(sinking.LayerCount - 1) /
                                  (sinking.WorldArea * columnMetres);
            double stirredFloor = stirred.StockInLayer(stirred.LayerCount - 1) /
                                  (stirred.WorldArea * columnMetres);

            double sinkingStep = sinkingFloor / Math.Max(1e-12, sinking.DensityAt(-10.5f));
            double stirredStep = stirredFloor / Math.Max(1e-12, stirred.DensityAt(-10.5f));

            _output.WriteLine(
                $"floor share: sinking {100d * sinking.StockInLayer(sinking.LayerCount - 1) / sinking.TotalJoules:0.#}%, " +
                $"stirred {100d * stirred.StockInLayer(stirred.LayerCount - 1) / stirred.TotalJoules:0.#}%");
            _output.WriteLine(
                $"floor:column density ratio — sinking {sinkingStep:0.#}x, stirred {stirredStep:0.#}x");

            // The cliff flattens by more than an order of magnitude. That is the difference
            // between a reward reachable only by crossing fifty-nine metres in one lifetime and a
            // reward that pays a little for every metre.
            Assert.True(
                stirredStep < sinkingStep / 10d,
                $"the step is still {stirredStep:0.#}x against {sinkingStep:0.#}x unmixed");

            // And the floor stops being where nearly everything ends up.
            Assert.True(
                stirred.StockInLayer(stirred.LayerCount - 1) <
                sinking.StockInLayer(sinking.LayerCount - 1) / 10d,
                "stirring did not keep detritus out of the sediment");
        }

        [Fact]
        public void MixingWithNoDiffusivityChangesNothing()
        {
            var a = new NutrientField(400f, 1f, 0.02f, 60f);
            var b = new NutrientField(400f, 1f, 0.02f, 60f);

            a.Deposit(-5.5f, 700f);
            b.Deposit(-5.5f, 700f);

            for (int i = 0; i < 500; i++)
            {
                a.Settle(1f);
                b.Settle(1f);
                b.Mix(1f, 0f);
            }

            for (int layer = 0; layer < a.LayerCount; layer++)
            {
                Assert.Equal(a.StockInLayer(layer), b.StockInLayer(layer), 12);
            }
        }

        [Fact]
        public void RemineraliseMovesAnExactFirstOrderFractionOfTheFloorUpOneLayer()
        {
            // D051: the return leg Settle lacks. The moved amount is the closed-form solution of
            // dN/dt = -rate*N, taken from nowhere else and arriving nowhere else — not a capped
            // forward-Euler step, so no min(1, ...) appears here.
            var field = new NutrientField(400f, 1f, 0f, 60f);
            field.Deposit(-59.5f, 1000f);
            field.Deposit(-30.5f, 250f);

            int floor = field.LayerCount - 1;
            double floorBefore = field.StockInLayer(floor);
            double aboveBefore = field.StockInLayer(floor - 1);
            double totalBefore = field.TotalJoules;

            const float rate = 0.01f;
            const double seconds = 2.0;
            field.Remineralise(seconds, rate);

            // rate is float-promoted before the multiply, exactly as Remineralise does it — a
            // double literal here would differ from the source by ~1e-10 in the exponent, which
            // is small but not smaller than this test's own tolerance.
            double expectedMoved = floorBefore * (1.0 - Math.Exp(-(double)rate * seconds));

            Assert.Equal(floorBefore - expectedMoved, field.StockInLayer(floor), 6);
            Assert.Equal(aboveBefore + expectedMoved, field.StockInLayer(floor - 1), 6);
            Assert.Equal(totalBefore, field.TotalJoules, 6);
        }

        [Fact]
        public void RemineraliseIsStepSizeIndependent()
        {
            // The whole point of the exact decay law: one long call and many short calls at the
            // same rate must agree, because the fraction moved depends only on rate * elapsed
            // time, not on how that time was divided into steps. A capped forward-Euler step
            // (min(1, rate * seconds) applied ten times) would not have this property.
            var oneCall = new NutrientField(400f, 1f, 0f, 60f);
            oneCall.Deposit(-59.5f, 1000f);
            oneCall.Deposit(-30.5f, 250f);

            var tenCalls = new NutrientField(400f, 1f, 0f, 60f);
            tenCalls.Deposit(-59.5f, 1000f);
            tenCalls.Deposit(-30.5f, 250f);

            oneCall.Remineralise(10.0, 0.05f);
            for (int i = 0; i < 10; i++) tenCalls.Remineralise(1.0, 0.05f);

            for (int layer = 0; layer < oneCall.LayerCount; layer++)
            {
                double expected = oneCall.StockInLayer(layer);
                double actual = tenCalls.StockInLayer(layer);
                double tolerance = Math.Max(1e-9, Math.Abs(expected) * 1e-9);
                Assert.True(
                    Math.Abs(expected - actual) <= tolerance,
                    $"Layer {layer}: expected {expected}, got {actual}, diff {Math.Abs(expected - actual)}");
            }
        }

        [Fact]
        public void RemineraliseWithZeroRateChangesNothing()
        {
            var field = new NutrientField(400f, 1f, 0f, 60f);
            field.Deposit(-59.5f, 1000f);
            field.Deposit(-30.5f, 250f);

            var stocksBefore = new double[field.LayerCount];
            for (int layer = 0; layer < field.LayerCount; layer++)
                stocksBefore[layer] = field.StockInLayer(layer);

            field.Remineralise(500.0, 0f);

            for (int layer = 0; layer < field.LayerCount; layer++)
                Assert.Equal(stocksBefore[layer], field.StockInLayer(layer), 12);
        }

        [Fact]
        public void RemineraliseAtAVeryLargeRateDtMovesNearlyAllTheFloorAndNeverGoesNegative()
        {
            // Unlike Settle and Mix, this is the exact law 1 - exp(-rate*seconds), which
            // approaches but never reaches 1 — so a huge rate*dt empties the floor almost
            // entirely rather than exactly, and there is no cap to test.
            var field = new NutrientField(400f, 1f, 0f, 60f);
            field.Deposit(-59.5f, 1000f);

            int floor = field.LayerCount - 1;
            double floorBefore = field.StockInLayer(floor);
            double totalBefore = field.TotalJoules;

            field.Remineralise(100.0, 1f);

            Assert.True(field.StockInLayer(floor) >= 0d);
            Assert.True(field.StockInLayer(floor) < floorBefore * 0.0001);
            Assert.Equal(totalBefore, field.TotalJoules, 6);
        }

        [Fact]
        public void RemineraliseOnASingleLayerFieldIsANoOp()
        {
            // LayerCount < 2 means there is no layer above the floor to receive anything —
            // the same guard Settle and Mix both apply for the same reason.
            var field = new NutrientField(400f, 100f, 0f, 50f);
            Assert.Equal(1, field.LayerCount);

            field.Deposit(-25f, 500f);
            double before = field.StockInLayer(0);

            field.Remineralise(1000.0, 0.5f);

            Assert.Equal(before, field.StockInLayer(0), 12);
        }

        // ------------------------------------------------------------ D055: seabed refuge

        private const float FloorDepth = -4.5f; // within layer 4 of a 5-layer, 1 m field — the floor

        [Fact]
        public void ARefugeOfZeroLayersIsRefugeNowhere()
        {
            // The care note in D055 itself: RefugeLayerCount 0 must not make every layer "at or
            // past the floor minus zero" read as refuge.
            var field = new NutrientField(400f, 1f, 0f, 5f); // no refugeMetres argument at all
            Assert.Equal(0, field.RefugeLayerCount);

            for (int layer = 0; layer < field.LayerCount; layer++)
                Assert.False(field.IsRefuge(layer), $"layer {layer} read as refuge with the knob unset");
        }

        [Fact]
        public void TheRefugeRefusesTheMouthButNotTheInstrument()
        {
            // A 5-layer, 1 m-per-layer field with a 1 m refuge buries exactly the floor layer.
            var field = new NutrientField(400f, 1f, 0f, 5f, refugeMetres: 1f);
            Assert.Equal(1, field.RefugeLayerCount);
            Assert.True(field.IsRefuge(field.LayerCount - 1));
            Assert.False(field.IsRefuge(field.LayerCount - 2), "the layer above the floor must stay edible");

            field.Deposit(FloorDepth, 1000f);
            float trueDensity = field.DensityAt(FloorDepth);

            // DensityAt keeps reporting what the water actually holds...
            Assert.True(trueDensity > 0f, "the deposit did not land where the test thinks it did");

            // ...but EdibleDensityAt, which is what a mouth prices, reads zero there.
            Assert.Equal(0f, field.EdibleDensityAt(FloorDepth));

            // Take refuses outright and leaves the stock untouched.
            double stockBefore = field.StockInLayer(field.LayerCount - 1);
            float taken = field.Take(FloorDepth, 500f);
            Assert.Equal(0f, taken);
            Assert.Equal(stockBefore, field.StockInLayer(field.LayerCount - 1), 12);

            // Demand never registers, so a competitor sharing the same layer sees no scarcity —
            // ShareAt returns 1 regardless of how much stock the floor holds or how much was asked.
            field.ClearDemand();
            field.Demand(FloorDepth, 10_000f);
            Assert.Equal(1f, field.ShareAt(FloorDepth));
        }

        [Fact]
        public void DepositStillLandsInTheRefuge()
        {
            // Deposit, Settle, Mix and Remineralise are untouched by D055 — the refuge is only a
            // feeding-side rule. A deposit at floor depth must still increase the floor's stock.
            var field = new NutrientField(400f, 1f, 0f, 5f, refugeMetres: 1f);
            int floor = field.LayerCount - 1;
            Assert.True(field.IsRefuge(floor));

            double before = field.StockInLayer(floor);
            field.Deposit(FloorDepth, 250f);

            Assert.Equal(before + 250f, field.StockInLayer(floor), 6);
        }

        [Fact]
        public void MixingStillMovesStockOutOfTheRefugeAndConservesIt()
        {
            // The refuge's only exit is physics, not feeding: Mix must still carry detritus from
            // the buried floor layer into the water above it, exactly as it would with no refuge
            // at all — D055 changes what Demand/Take see, not what Mix does.
            var field = new NutrientField(400f, 1f, 0f, 5f, refugeMetres: 1f);
            int floor = field.LayerCount - 1;
            int above = floor - 1;
            Assert.True(field.IsRefuge(floor));
            Assert.False(field.IsRefuge(above));

            field.Deposit(FloorDepth, 1000f);
            double floorBefore = field.StockInLayer(floor);
            double aboveBefore = field.StockInLayer(above);
            double totalBefore = field.TotalJoules;

            for (int i = 0; i < 2000; i++) field.Mix(0.5f, 2f);

            double floorAfter = field.StockInLayer(floor);
            double aboveAfter = field.StockInLayer(above);

            _output.WriteLine(
                $"floor {floorBefore:0.#} -> {floorAfter:0.#}, above {aboveBefore:0.#} -> {aboveAfter:0.#}");

            Assert.True(floorAfter < floorBefore, "mixing never drained the refuge layer");
            Assert.True(aboveAfter > aboveBefore, "mixing never fed the layer above the refuge");
            Assert.Equal(totalBefore, field.TotalJoules, 6);
        }
    }
}

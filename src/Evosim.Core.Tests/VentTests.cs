using System;
using System.Collections.Generic;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The vent — an upwelling plume that closes the loop a roll leaves open, D067.
    /// </summary>
    public class VentTests
    {
        private readonly ITestOutputHelper _output;

        public VentTests(ITestOutputHelper output) => _output = output;

        private const float Speed = 0.1f;
        private const float Depth = 60f;
        private const float Leg = 1f;

        /// <summary>A vent and nothing else: no roll, still water, the fields carried.</summary>
        private static CurrentField Vent(int patch, float speed = Speed) =>
            new CurrentField
            {
                Speed = 0f,
                CellMetres = 25f,
                PeriodSeconds = 300f,
                AdvectFields = true,
                VentSpeed = speed,
                VentPatch = patch,
                VentDepthMetres = Depth,
                VentLegMetres = Leg,
            };

        // ------------------------------------------------------------ off is off

        [Fact]
        public void WithTheVentOffTheFieldIsThePreD067OneExactly()
        {
            // Every number on file was measured without a vent, and three of D067's four knobs
            // carry non-zero defaults. So the guarantee has to be that the other three are *unread*
            // while the speed is zero — not that they happen to be harmless at their defaults.
            // Same rule as D031/D052/D055/D061/D066, applied to D067.
            foreach (bool rolls in new[] { false, true })
            {
                var plain = new CurrentField
                {
                    Speed = 0.05f, CellMetres = 25f, PeriodSeconds = 300f,
                    Rolls = rolls, RollBlinkSeconds = 137f, AdvectFields = true,
                };

                var quiet = new CurrentField
                {
                    Speed = 0.05f, CellMetres = 25f, PeriodSeconds = 300f,
                    Rolls = rolls, RollBlinkSeconds = 137f, AdvectFields = true,
                    // Every vent knob moved off its default, and the vent still off.
                    VentSpeed = 0f, VentPatch = 3, VentDepthMetres = 17f, VentLegMetres = 5f,
                };

                quiet.SetPatchWidth(10f);

                foreach (int patches in new[] { 1, 2, 3, 4, 8 })
                {
                    for (int patch = 0; patch < patches; patch++)
                    {
                        for (float y = 2f; y > -70f; y -= 0.83f)
                        {
                            for (double t = 0d; t < 900d; t += 13.7d)
                            {
                                Assert.Equal(
                                    plain.VelocityAt(y, t, patch, patches),
                                    quiet.VelocityAt(y, t, patch, patches));

                                Assert.Equal(
                                    plain.CrossingDirection(y, t, patch, patches),
                                    quiet.CrossingDirection(y, t, patch, patches));

                                Assert.Equal(
                                    plain.HorizontalCrossingFraction(y, t, patch, patches, 0.5d, 10f),
                                    quiet.HorizontalCrossingFraction(y, t, patch, patches, 0.5d, 10f));
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void AWorldWithTheVentOffIsUntouchedByItsOtherThreeKnobs()
        {
            // The strongest form, and the one that catches an accidental RNG draw: a world with
            // rolls and advection running must produce the same trajectory, bit for bit, whether or
            // not the vent's remaining knobs are set. If AdvectBodies drew a single extra float the
            // two would diverge within a step and every replay on file would be invalid. It is also
            // the proof that the three construction-time validations are skipped while the vent is
            // off — the second config names a depth and a leg this world would refuse.
            string Trajectory(bool configureVent)
            {
                var current = new CurrentField
                {
                    Speed = 0.05f, CellMetres = 25f, PeriodSeconds = 300f,
                    Rolls = true, RollBlinkSeconds = 137f, AdvectFields = true,
                };

                if (configureVent)
                {
                    current.VentSpeed = 0f;
                    current.VentPatch = 3;
                    current.VentDepthMetres = 17f;
                    current.VentLegMetres = 5f;
                }

                var config = new RunConfig
                {
                    Light = new LightModel(300f, 12f),
                    MinimumPopulation = 30,
                    MaximumPopulation = 300,
                    HorizontalPatches = 4f,
                    DispersalChancePerStep = 0.05f,
                    HorizontalMixingDiffusivity = 0.05f,
                    NutrientMixingDiffusivity = 0.2f,
                    Current = current,
                };

                var world = new World(config, seed: 5);
                var sb = new StringBuilder();

                for (int i = 0; i < 200; i++)
                {
                    world.Step(1f);
                    sb.AppendLine(WorldStats.Sample(world).ToJson());
                    sb.AppendLine(
                        $"{world.Nutrients.TotalJoules:R}|{world.Matter.TotalJoules:R}|" +
                        $"{world.AuditResidual:R}|{world.StandingMatter:R}");

                    foreach (Organism c in world.Living) sb.Append(c.Patch).Append(',');
                    sb.AppendLine();
                }

                return sb.ToString();
            }

            Assert.Equal(Trajectory(false), Trajectory(true));
        }

        [Fact]
        public void AVentNeedsAReturnPatchSoOnePatchIsNoVent()
        {
            // A plume with nowhere to return through is a source, not a circulation — the same
            // statement the roll makes about needing a neighbour.
            CurrentField vent = Vent(0);

            Assert.False(vent.VentActive(1));
            Assert.True(vent.VentActive(2));

            for (float y = 0f; y > -70f; y -= 0.61f)
            {
                Assert.Equal(Float3.Zero, vent.VelocityAt(y, 13d, 0, 1));
                Assert.Equal(0, vent.CrossingDirection(y, 13d, 0, 1));
                Assert.Equal(0d, vent.HorizontalCrossingFraction(y, 13d, 0, 1, 0.5d, 10f));
            }
        }

        // ------------------------------------------------------------ the plume and the return

        [Fact]
        public void ThePlumeRisesAndEveryOtherPatchSinksToMatchIt()
        {
            CurrentField vent = Vent(patch: 2);

            float rise = Speed;
            var fall = (float)(-(double)Speed / 3d);   // K - 1 = 3 return patches share it

            foreach (float d in new[] { 0.5f, 10f, 30f, 59.5f })
            {
                Assert.Equal(rise, vent.VelocityAt(-d, 7d, 2, 4).Y);

                foreach (int patch in new[] { 0, 1, 3 })
                {
                    Assert.Equal(fall, vent.VelocityAt(-d, 7d, patch, 4).Y);
                }
            }

            // Exactly zero at both ends and below, special-cased rather than computed — a residual
            // vertical velocity at the waterline is the slow invisible lift of logbook/0022, and
            // below the floor is water the world does not have (logbook/0040).
            foreach (float y in new[] { 0f, -60f, -60.5f, -200f })
            {
                for (int patch = 0; patch < 4; patch++)
                {
                    Assert.Equal(0f, vent.VelocityAt(y, 7d, patch, 4).Y);
                }
            }

            // And the column balances: what rises in the plume sinks in the others, at every depth.
            for (float d = 0.5f; d < 60f; d += 3.3f)
            {
                double sum = 0d;
                for (int patch = 0; patch < 4; patch++) sum += vent.VelocityAt(-d, 7d, patch, 4).Y;

                Assert.True(Math.Abs(sum) < 1e-7d, $"net vertical flux {sum:R} at {d} m");
            }
        }

        [Fact]
        public void TheHorizontalDragIsZeroUntilTheWorldSaysHowWideAPatchIs()
        {
            // PatchWidthMetres is geometry rather than a knob, and it buys exactly one thing: the
            // velocity a creature feels in a leg. Everything else about the vent is a volume flux
            // and is width-free, so an unset width must change the transport not at all.
            CurrentField bare = Vent(patch: 0);
            CurrentField wide = Vent(patch: 0);
            wide.SetPatchWidth(10f);

            Assert.Equal(0f, bare.VelocityAt(-0.5f, 3d, 0, 4).X);

            // u = c_j·s·W/L, and Z = -X as everywhere else in this class.
            Assert.Equal(0.5d * Speed * 10d / Leg, (double)wide.VelocityAt(-0.5f, 3d, 0, 4).X, 5);
            Assert.Equal(-wide.VelocityAt(-0.5f, 3d, 0, 4).X, wide.VelocityAt(-0.5f, 3d, 0, 4).Z);

            // Same transport either way.
            for (int patch = 0; patch < 4; patch++)
            {
                for (float d = 0.5f; d < 60f; d += 1f)
                {
                    Assert.Equal(
                        bare.CrossingDirection(-d, 3d, patch, 4),
                        wide.CrossingDirection(-d, 3d, patch, 4));

                    Assert.Equal(
                        bare.HorizontalCrossingFraction(-d, 3d, patch, 4, 0.5d, 10f),
                        wide.HorizontalCrossingFraction(-d, 3d, patch, 4, 0.5d, 10f));
                }
            }
        }

        // ------------------------------------------------------------ the legs

        [Fact]
        public void TheLegsRunOutAlongTheSurfaceAndBackAlongTheFloor()
        {
            const int k = 4;
            const int v = 2;

            CurrentField vent = Vent(patch: v);

            const float surface = -0.5f;                 // in the surface leg
            const float floor = -(Depth - 0.5f);         // in the floor leg
            const float between = -30.5f;                // in neither

            // Water leaves the plume both ways at the surface: through its own right-hand face
            // toward V+1, and through the face behind it — which patch V-1 owns — back toward V-1.
            Assert.Equal(1, vent.CrossingDirection(surface, 5d, v, k));
            Assert.Equal(-1, vent.CrossingDirection(surface, 5d, (v - 1 + k) % k, k));

            // Along the floor the same two faces run the other way, which is what makes the loop
            // close rather than leaving the deep sealed off (logbook/0048's trapdoor).
            Assert.Equal(-1, vent.CrossingDirection(floor, 5d, v, k));
            Assert.Equal(1, vent.CrossingDirection(floor, 5d, (v - 1 + k) % k, k));

            for (int patch = 0; patch < k; patch++)
            {
                Assert.Equal(0, vent.CrossingDirection(between, 5d, patch, k));
                Assert.Equal(0d, vent.HorizontalCrossingFraction(between, 5d, patch, k, 0.5d, 10f));
            }

            // The fractions are |c_j|·s·dt/L — the flux divided by a cell's volume, with the patch
            // area cancelling. c_0 = 1/2 at the plume's own face; c_1 = 1/2 - 1/3 at the next.
            const double dt = 0.5d;

            Assert.Equal(
                0.5d * Speed * dt / Leg,
                vent.HorizontalCrossingFraction(surface, 5d, v, k, dt, 10f), 12);

            Assert.Equal(
                (0.5d - 1d / 3d) * Speed * dt / Leg,
                vent.HorizontalCrossingFraction(surface, 5d, (v + 1) % k, k, dt, 10f), 12);

            // And the far face runs the other way with the same magnitude as the near one, which is
            // the statement "half the plume's water goes each way".
            Assert.Equal(
                0.5d * Speed * dt / Leg,
                vent.HorizontalCrossingFraction(surface, 5d, (v - 1 + k) % k, k, dt, 12f), 12);
        }

        [Fact]
        public void TheLegFractionIsClampedAtAHalfLikeEveryOtherTransferHere()
        {
            CurrentField vent = Vent(patch: 0);

            // A step long enough to empty a leg cell several times over moves half of it, not
            // several times over — the same Courant clamp Mix and Advect use, and the reason no
            // cell can be asked for more than it holds.
            Assert.Equal(0.5d, vent.HorizontalCrossingFraction(-0.5f, 0d, 0, 4, 10000d, 10f));
            Assert.Equal(0d, vent.HorizontalCrossingFraction(-0.5f, 0d, 0, 4, 0d, 10f));
        }

        // ------------------------------------------------------------ discrete continuity

        [Theory]
        [InlineData(4, 0)]
        [InlineData(4, 2)]
        [InlineData(2, 1)]
        // An odd patch count is fine for the vent, unlike the roll: the return is shared equally
        // over every patch that is not the plume, so there is no parity to alternate and no seam.
        [InlineData(5, 3)]
        public void EveryCellTakesInExactlyWhatItGivesOutIncludingAtOddPatchCounts(int patches, int ventPatch)
        {
            // Discrete continuity itself, cell by cell, rather than a property downstream of it.
            // The staggered grid is w at layer interfaces per patch and u at patch faces per layer,
            // and this walks every face of every cell and adds up the fractions the transfer would
            // move through them: the plume's Q up and Q/2 out each way at the top, each return
            // patch keeping the c_{j-1} - c_j = 1/(K-1) of the surface leg that it then sinks, and
            // the floor leg the same in reverse. If any of those coefficients were wrong this is
            // where it shows, at the cell where it is wrong.
            const double dt = 0.5d;

            var field = new NutrientField(400f, 1f, 0f, Depth, patchCount: patches);
            CurrentField vent = Vent(ventPatch);
            vent.SetPatchWidth(field.PatchWidthMetres);

            float layerMetres = field.LayerMetres;
            float width = field.PatchWidthMetres;

            double Vertical(float interfaceY, int patch)
            {
                double w = vent.VelocityAt(interfaceY, 0d, patch, patches).Y;
                double fraction = Math.Abs(w) * dt / layerMetres;
                return w > 0d ? (fraction > 0.5d ? 0.5d : fraction)
                     : w < 0d ? -(fraction > 0.5d ? 0.5d : fraction)
                     : 0d;
            }

            for (int layer = 0; layer < field.LayerCount; layer++)
            {
                float midY = -((layer + 0.5f) * layerMetres);

                for (int patch = 0; patch < patches; patch++)
                {
                    double inflow = 0d;
                    double outflow = 0d;

                    // The two vertical faces. Positive w is upward, so the interface below this
                    // cell brings water in and the one above takes it out — and vice versa. The
                    // surface and the floor are closed: Advect only ever walks interfaces between
                    // two real layers, which is why nothing leaves the world here.
                    if (layer < field.LayerCount - 1)
                    {
                        double below = Vertical(-((layer + 1) * layerMetres), patch);
                        if (below > 0d) inflow += below; else outflow += -below;
                    }

                    if (layer > 0)
                    {
                        double above = Vertical(-(layer * layerMetres), patch);
                        if (above > 0d) outflow += above; else inflow += -above;
                    }

                    // The two horizontal faces: this patch's own right-hand one, and the one behind
                    // it that its neighbour owns.
                    int behind = (patch - 1 + patches) % patches;

                    int right = vent.CrossingDirection(midY, 0d, patch, patches);
                    if (right != 0)
                    {
                        double f = vent.HorizontalCrossingFraction(midY, 0d, patch, patches, dt, width);
                        if (right > 0) outflow += f; else inflow += f;
                    }

                    int left = vent.CrossingDirection(midY, 0d, behind, patches);
                    if (left != 0)
                    {
                        double f = vent.HorizontalCrossingFraction(midY, 0d, behind, patches, dt, width);
                        if (left > 0) inflow += f; else outflow += f;
                    }

                    // To single precision, not to double, and the reason is worth stating: the
                    // vertical half of the balance travels through a Float3 — VelocityAt rounds w
                    // to a float, exactly as it does for every creature — while the horizontal half
                    // is a coefficient computed in double and never becomes a velocity at all. So
                    // the two sides of an identity that is exact in algebra meet at about 2e-8
                    // here. A wrong coefficient would be wrong by a fraction of itself, not by an
                    // ulp, so this is still the test.
                    Assert.True(
                        Math.Abs(inflow - outflow) <= 1e-6d * (inflow + outflow + 1e-30d),
                        $"K={patches} V={ventPatch} layer {layer} patch {patch}: " +
                        $"{inflow:R} in against {outflow:R} out");
                }
            }
        }

        [Theory]
        [InlineData(4, 0)]
        [InlineData(4, 3)]
        [InlineData(2, 0)]
        [InlineData(2, 1)]
        [InlineData(5, 0)]
        [InlineData(5, 2)]
        public void AUniformFieldStaysUniformUnderTheVentIncludingAtOddPatchCounts(int patches, int ventPatch)
        {
            // Discrete continuity, stated as the property it buys. Under an upwind transfer a cell
            // loses `fraction x its own stock` through every face it exports through and gains
            // `fraction x the neighbour's` through every face it imports through, so a uniform
            // field is preserved exactly when the fractions balance — which is what the plume, the
            // return and the two legs are constructed to do.
            var field = new NutrientField(400f, 1f, 0f, Depth, patchCount: patches);

            const double perCell = 1000d;
            for (int layer = 0; layer < field.LayerCount; layer++)
            {
                for (int patch = 0; patch < patches; patch++)
                {
                    field.Deposit(-(layer + 0.5f), (float)perCell, patch);
                }
            }

            double before = field.TotalJoules;
            CurrentField vent = Vent(ventPatch);
            vent.SetPatchWidth(field.PatchWidthMetres);

            const double dt = 0.5d;
            double Worst()
            {
                double worst = 0d;
                for (int layer = 0; layer < field.LayerCount; layer++)
                {
                    for (int patch = 0; patch < patches; patch++)
                    {
                        double stock = field.StockInLayer(layer, patch);
                        Assert.True(stock >= 0d, $"layer {layer} patch {patch} went negative");
                        worst = Math.Max(worst, Math.Abs(stock - perCell) / perCell);
                    }
                }

                return worst;
            }

            int step = 0;
            for (; step < 2000; step++) field.Advect(vent, step * dt, (float)dt, field.PatchWidthMetres);
            double settled = Worst();

            for (; step < 6000; step++) field.Advect(vent, step * dt, (float)dt, field.PatchWidthMetres);
            double later = Worst();

            double drift = Math.Abs(field.TotalJoules - before) / before;
            double courant = Speed * dt / field.LayerMetres;

            _output.WriteLine(
                $"K={patches} V={ventPatch}: worst cell {settled:0.000e+0} at 2,000 steps, " +
                $"{later:0.000e+0} at 6,000, Courant {courant:0.000e+0}, total {drift:0.0e+0}");

            // The total is exact — every move is a flux between two named cells, so §5A.2's audit
            // never has to trust this.
            Assert.True(drift <= 1e-9, $"the vent moved {drift:0.0e+0} of the total that it should not have");

            // Per cell it is exact only up to the operator splitting, and that is a property of
            // NutrientField.Advect rather than of the vent. Advect runs its vertical pass to
            // completion and then its horizontal one on the result — which is what keeps a cell
            // from being asked for more than it holds and is therefore not negotiable — so the two
            // half-steps act on slightly different fields. The fluxes balance exactly — that is the
            // test above this one — but the order they are applied in does not, and the field
            // settles into a standing pattern of order the Courant number s.dt/LayerMetres. It is
            // first order in dt and measured so: 4.9e-2, 2.5e-2, 1.2e-2, 6.1e-3 at dt 0.5, 0.25,
            // 0.125, 0.0625, which is what says it is the splitting and not the vent.
            Assert.True(
                settled < 1.5d * courant,
                $"a cell sits {settled:0.0e+0} from uniform, well past the {courant:0.0e+0} splitting residual");

            // Bounded rather than accumulating, which is the part that matters: a scheme leaking
            // into one cell would keep leaking, and three more times as many steps would say so.
            Assert.True(
                later <= settled * 1.001d + 1e-12d,
                $"the residual grew from {settled:0.0e+0} to {later:0.0e+0} — it is accumulating, not settling");
        }

        [Fact]
        public void TheVentLiftsTheLarderOffTheFloorAndIntoTheLight()
        {
            // The claim D067 is built on, at its coarsest: detritus that reached the floor of a
            // patch that is not the vent gets back into the light. Not a rate — that belongs to a
            // run — but the difference between a mechanism with a return path and D061's sealed
            // pools, which is what a roll that stops above the floor leaves behind.
            const int k = 4;
            const int v = 2;
            const double dt = 0.5d;

            var field = new NutrientField(400f, 1f, 0f, Depth, patchCount: k);
            field.Deposit(-(Depth - 0.5f), 1000f, patch: 0);

            CurrentField vent = Vent(v);
            vent.SetPatchWidth(field.PatchWidthMetres);

            // Along the floor at |c_j|.s per patch, then up the plume at s. Doubled, because the
            // parcel spreads as it goes and the leading edge is not the whole of it.
            double late = 2d * (Depth / Speed + k * Leg / (0.5d * Speed));
            double early = Depth / (4d * Speed);

            double atEarly = double.NaN;
            double best = 0d;

            for (int i = 0; i * dt < late; i++)
            {
                field.Advect(vent, i * dt, (float)dt, field.PatchWidthMetres);

                for (int layer = 0; layer < field.LayerCount; layer++)
                {
                    for (int patch = 0; patch < k; patch++)
                    {
                        Assert.True(
                            field.StockInLayer(layer, patch) >= 0d,
                            $"layer {layer} patch {patch} went negative at step {i}");
                    }
                }

                double surface = field.StockInLayer(0, v) / 1000d;
                if (surface > best) best = surface;
                if (double.IsNaN(atEarly) && (i + 1) * dt >= early) atEarly = surface;
            }

            _output.WriteLine(
                $"plume surface layer: {atEarly:0.000e+0} of the parcel at t={early} s, " +
                $"peak {best:0.000} by t={late} s");

            // It has not teleported: at a quarter of the plume's own transit time the surface of
            // the plume has effectively none of it. Upwind advection is numerically diffusive and
            // this is what says the arrival is transport rather than smearing.
            Assert.True(atEarly <= 1e-4, $"{atEarly:0.0e+0} of the parcel was already at the surface at t={early} s");

            // And it arrives as a parcel rather than as a smear. One cell in 240 holds 0.4% of a
            // well-mixed world, and the plume's surface cell peaks at about 4.6% — an order of
            // magnitude more, which is what says the deep larder was carried up rather than
            // gradually shared out. It is not more than that because upwind advection spreads the
            // pulse as it climbs: sixty metres at Courant 0.05 is a numerical diffusion length of
            // several metres, so what arrives is a broad front and no single layer holds much of
            // it at once.
            Assert.True(best >= 0.04d, $"only {best:0.000} of the parcel ever reached the plume's surface layer");
            Assert.True(best >= 8d / field.LayerCount / k, $"{best:0.000} is barely above a well-mixed share");
        }

        [Fact]
        public void ConservationHoldsWithTheRollAndTheVentRunningTogether()
        {
            // Two prescribed flows at the same faces. They can oppose each other, and what crosses
            // is the net of them — so this is the test that the sign and the magnitude of the
            // transfer are taken from one number rather than from two that can disagree.
            var field = new NutrientField(400f, 1f, 0f, Depth, patchCount: 4);
            var rng = new Rng(29);

            for (int layer = 0; layer < field.LayerCount; layer++)
            {
                for (int patch = 0; patch < 4; patch++)
                {
                    field.Deposit(-(layer + 0.5f), rng.NextFloat() * 500f, patch);
                }
            }

            double before = field.TotalJoules;

            var both = new CurrentField
            {
                Speed = 0.3f, CellMetres = 30f, PeriodSeconds = 300f,
                Rolls = true, RollBlinkSeconds = 300f, AdvectFields = true,
                VentSpeed = Speed, VentPatch = 1, VentDepthMetres = Depth, VentLegMetres = Leg,
            };

            both.SetPatchWidth(field.PatchWidthMetres);

            for (int i = 0; i < 4000; i++)
            {
                field.Advect(both, i * 0.5, 0.5f, field.PatchWidthMetres);

                for (int layer = 0; layer < field.LayerCount; layer++)
                {
                    for (int patch = 0; patch < 4; patch++)
                    {
                        Assert.True(
                            field.StockInLayer(layer, patch) >= 0d,
                            $"layer {layer} patch {patch} went negative at step {i}");
                    }
                }
            }

            double drift = Math.Abs(field.TotalJoules - before) / before;
            _output.WriteLine($"{before:R} -> {field.TotalJoules:R} ({drift:0.0e+0} relative)");

            Assert.True(drift <= 1e-9, $"the two flows together moved {drift:0.0e+0} of the total");
        }

        // ------------------------------------------------------------ bodies

        [Fact]
        public void BodiesRideTheFloorLegTowardThePlumeAndSitStillBetweenTheLegs()
        {
            const int k = 4;
            const int v = 2;

            var config = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                MinimumPopulation = 40,
                MaximumPopulation = 400,
                HorizontalPatches = k,
                Current = new CurrentField
                {
                    Speed = 0f, CellMetres = 60f, PeriodSeconds = 300f,
                    VentSpeed = 0.5f, VentPatch = v, VentDepthMetres = Depth, VentLegMetres = Leg,
                },
            };

            var world = new World(config, seed: 7);
            CurrentField vent = config.Current;

            // In the floor leg, where the water is on its way back to the plume.
            int moved = Run(world, vent, -(Depth - 0.5f), k, v, steps: 80);
            _output.WriteLine($"{moved} creatures crossed a boundary in the floor leg");
            Assert.True(moved > 0, "nothing was carried along the floor leg");

            // Between the legs the vent is purely vertical, so nothing changes patch at all.
            var still = new World(config, seed: 7);
            int drifted = Run(still, vent, -30.5f, k, v, steps: 80);
            Assert.Equal(0, drifted);
        }

        /// <summary>
        /// Holds every creature at one depth, steps, and checks that every patch change is the one
        /// the water at that depth says it should be. Returns how many crossings happened.
        /// </summary>
        private static int Run(World world, CurrentField vent, float heightY, int patches, int ventPatch, int steps)
        {
            int crossings = 0;

            for (int i = 0; i < steps; i++)
            {
                var before = new Dictionary<long, int>();

                foreach (Organism c in world.Living)
                {
                    // Observe is the only way in from outside the assembly, and it is the honest
                    // one: this is exactly what the physics does every step.
                    world.Observe(c, heightY, 0f);
                    before[c.Id] = c.Patch;
                }

                double clock = world.ElapsedSeconds;
                world.Step(1f);

                foreach (Organism c in world.Living)
                {
                    if (!before.TryGetValue(c.Id, out int was) || was == c.Patch) continue;

                    crossings++;

                    int behind = (was - 1 + patches) % patches;
                    bool ahead = c.Patch == (was + 1) % patches &&
                                 vent.CrossingDirection(heightY, clock, was, patches) > 0;
                    bool back = c.Patch == behind &&
                                vent.CrossingDirection(heightY, clock, behind, patches) < 0;

                    Assert.True(
                        ahead || back,
                        $"a creature went from patch {was} to {c.Patch}, which is not where the " +
                        $"water at {heightY} m is going");

                    // And the water at the floor is going to the plume, whichever way round the
                    // ring is shorter: nothing ever leaves the vent patch along this leg.
                    Assert.NotEqual(ventPatch, was);
                }
            }

            return crossings;
        }

        // ------------------------------------------------------------ validation

        [Fact]
        public void AWorldRefusesAVentItsOwnGeometryContradicts()
        {
            // The field is handed heights and a patch index and nothing else, so it cannot see the
            // floor, the patch count or the layer thickness. Each of the three is load-bearing, so
            // the config states it and the world refuses when they disagree — rather than running
            // a different experiment from the one the file names.
            static RunConfig Config(Action<CurrentField> tweak)
            {
                var current = new CurrentField
                {
                    VentSpeed = Speed, VentPatch = 0,
                    VentDepthMetres = Depth, VentLegMetres = Leg,
                };

                tweak(current);

                return new RunConfig
                {
                    Light = new LightModel(300f, 12f),
                    HorizontalPatches = 4f,
                    WorldDepthMetres = Depth,
                    LightLayerMetres = 1f,
                    Current = current,
                };
            }

            ArgumentException patch = Assert.Throws<ArgumentException>(
                () => new World(Config(c => c.VentPatch = 4)));
            Assert.Contains("VentPatch", patch.Message);

            ArgumentException depth = Assert.Throws<ArgumentException>(
                () => new World(Config(c => c.VentDepthMetres = 45f)));
            Assert.Contains("VentDepthMetres", depth.Message);

            ArgumentException leg = Assert.Throws<ArgumentException>(
                () => new World(Config(c => c.VentLegMetres = 1.5f)));
            Assert.Contains("VentLegMetres", leg.Message);

            // Two legal vents, so the guard is not simply refusing everything.
            _ = new World(Config(c => c.VentPatch = 3));
            _ = new World(Config(c => c.VentLegMetres = 3f));

            // And with the vent off none of the three is read at all — which is what makes every
            // pre-D067 config still a legal one whatever the defaults happen to say.
            _ = new World(Config(c =>
            {
                c.VentSpeed = 0f;
                c.VentPatch = 9;
                c.VentDepthMetres = 17f;
                c.VentLegMetres = 1.5f;
            }));
        }

        [Fact]
        public void TheVentKnobsRefuseNonsenseAtTheSetter()
        {
            var vent = new CurrentField();

            Assert.Throws<ArgumentOutOfRangeException>(() => vent.VentSpeed = -0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => vent.VentSpeed = float.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => vent.VentPatch = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => vent.VentDepthMetres = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => vent.VentLegMetres = float.NaN);

            // The defaults are the pre-D067 world: no vent, and a depth and a leg that agree with
            // RunConfig's own defaults so that turning the speed up is all a run has to do.
            Assert.Equal(0f, vent.VentSpeed);
            Assert.Equal(0, vent.VentPatch);
            Assert.Equal(new RunConfig().WorldDepthMetres, vent.VentDepthMetres);
            Assert.Equal(new RunConfig().LightLayerMetres, vent.VentLegMetres);
            Assert.DoesNotContain("vent", vent.ToString());

            vent.VentSpeed = 0.1f;
            vent.VentPatch = 2;
            Assert.Contains("vent 0.1 m/s in patch 2", vent.ToString());
        }
    }
}

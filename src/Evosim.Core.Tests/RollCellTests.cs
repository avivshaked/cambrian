using System;
using System.Collections.Generic;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Roll cells — water that moves everything, and stirs — D066.
    /// </summary>
    public class RollCellTests
    {
        private readonly ITestOutputHelper _output;

        public RollCellTests(ITestOutputHelper output) => _output = output;

        private static CurrentField Steady(float speed = 0.05f) =>
            new CurrentField { Speed = speed, CellMetres = 25f, PeriodSeconds = 300f };

        private static CurrentField Rolling(float speed = 0.05f, float blink = 0f) =>
            new CurrentField
            {
                Speed = speed,
                CellMetres = 25f,
                PeriodSeconds = 300f,
                Rolls = true,
                RollBlinkSeconds = blink,
                AdvectFields = true,
            };

        // ------------------------------------------------------------ off is off

        [Fact]
        public void WithRollsOffThePatchOverloadIsTheOldFieldExactly()
        {
            // Every result on file was measured in the depth-only field. A default that perturbed
            // it by one bit would mean none of them describe a world that still exists — the
            // D031/D052/D055/D061 rule, applied to D066.
            CurrentField field = Steady();

            Assert.False(field.Rolls);
            Assert.Equal(0f, field.RollBlinkSeconds);
            Assert.False(field.AdvectFields);

            foreach (int patches in new[] { 1, 2, 3, 4, 8 })
            {
                for (int patch = 0; patch < patches; patch++)
                {
                    for (float y = 2f; y > -70f; y -= 0.83f)
                    {
                        for (double t = 0d; t < 900d; t += 13.7d)
                        {
                            Assert.Equal(field.VelocityAt(y, t), field.VelocityAt(y, t, patch, patches));
                        }
                    }
                }
            }
        }

        [Fact]
        public void RollsWithOnlyOnePatchAreTheOldFieldExactly()
        {
            // A roll needs a neighbour to sink while it rises. With K=1 there is none, so the
            // field degenerates rather than inventing a half-roll — and it degenerates to exactly
            // the old one, not to something close to it.
            CurrentField rolling = Rolling();
            CurrentField steady = Steady();

            for (float y = 0f; y > -70f; y -= 0.61f)
            {
                for (double t = 0d; t < 900d; t += 11.3d)
                {
                    Assert.Equal(steady.VelocityAt(y, t), rolling.VelocityAt(y, t, 0, 1));
                }
            }
        }

        [Fact]
        public void AdvectDoesNothingWhenTheFieldsAreNotAdvected()
        {
            var field = new NutrientField(400f, 1f, 0.02f, 60f, patchCount: 4);
            var rng = new Rng(11);

            for (int layer = 0; layer < field.LayerCount; layer++)
            {
                for (int patch = 0; patch < 4; patch++)
                {
                    field.Deposit(-(layer + 0.5f), rng.NextFloat() * 1000f, patch);
                }
            }

            double[] before = Snapshot(field);

            // Rolls on, advection off: the bodies feel the water and the larder does not.
            var current = new CurrentField
            {
                Speed = 0.05f, CellMetres = 60f, PeriodSeconds = 300f, Rolls = true,
            };

            for (int i = 0; i < 200; i++) field.Advect(current, i, 1f, field.PatchWidthMetres);

            Assert.Equal(before, Snapshot(field));

            // And nothing at all when there is no current object to speak of.
            field.Advect(null, 0d, 1f, field.PatchWidthMetres);
            Assert.Equal(before, Snapshot(field));
        }

        [Fact]
        public void AWorldWithRollsOffIsUntouchedByAMovingCurrent()
        {
            // The strongest form of the guarantee, and the one that catches an accidental RNG
            // draw: a world given a current at 0.05 m/s with rolls and advection off must produce
            // the same trajectory, bit for bit, as the same world in still water. If AdvectBodies
            // drew a single float the two would diverge within a step, and every replay on file
            // would be invalid.
            string Trajectory(float speed, bool rolls, bool advect)
            {
                var config = new RunConfig
                {
                    Light = new LightModel(300f, 12f),
                    MinimumPopulation = 30,
                    MaximumPopulation = 300,
                    HorizontalPatches = 4f,
                    DispersalChancePerStep = 0.05f,
                    HorizontalMixingDiffusivity = 0.05f,
                    NutrientMixingDiffusivity = 0.2f,
                    Current = new CurrentField
                    {
                        Speed = speed, CellMetres = 25f, PeriodSeconds = 300f,
                        Rolls = rolls, AdvectFields = advect,
                    },
                };

                var world = new World(config, seed: 5);
                var sb = new StringBuilder();

                for (int i = 0; i < 300; i++)
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

            Assert.Equal(Trajectory(0f, false, false), Trajectory(0.05f, false, false));
        }

        // ------------------------------------------------------------ the roll itself

        [Fact]
        public void TheVerticalFlowIsExactlyZeroAtTheWaterlineAndAtTheFloorOfTheCell()
        {
            // Not "small". Math.Sin(Math.PI) is 1.2e-16, and 1.2e-16 m/s integrated over a run is
            // a slow, invisible, one-directional lift — which is exactly the fault logbook/0022
            // paid for once, arriving through a rounding error instead of through a travelling
            // wave. Zero at both ends is what makes a roll a closed cell.
            CurrentField field = Rolling();

            for (double t = 0d; t < 1200d; t += 7.1d)
            {
                for (int patch = 0; patch < 4; patch++)
                {
                    Assert.Equal(0f, field.VelocityAt(0f, t, patch, 4).Y);
                    Assert.Equal(0f, field.VelocityAt(-25f, t, patch, 4).Y);

                    // And nothing at all below the cell — the roll is a surface phenomenon.
                    Assert.Equal(Float3.Zero, field.VelocityAt(-25.5f, t, patch, 4));
                    Assert.Equal(Float3.Zero, field.VelocityAt(-60f, t, patch, 4));
                }
            }
        }

        [Fact]
        public void AdjacentPatchesRunOppositeWays()
        {
            CurrentField field = Rolling();

            bool anyFlow = false;

            for (double t = 3d; t < 600d; t += 9.3d)
            {
                for (float y = -1f; y > -24f; y -= 3.7f)
                {
                    Float3 even = field.VelocityAt(y, t, 0, 4);
                    Float3 odd = field.VelocityAt(y, t, 1, 4);

                    Assert.Equal(-even.Y, odd.Y);
                    Assert.Equal(-even.X, odd.X);
                    Assert.Equal(even.Y, field.VelocityAt(y, t, 2, 4).Y);

                    if (Math.Abs(even.Y) > 1e-6f) anyFlow = true;
                }
            }

            Assert.True(anyFlow, "the roll never moved at all, so the alternation proved nothing");
        }

        [Fact]
        public void BlinkingReversesTheRollAndSteadyRollsNeverDo()
        {
            CurrentField steady = Rolling(blink: 0f);
            CurrentField blinking = Rolling(blink: 100f);

            for (double t = 0d; t < 100d; t += 6.1d)
            {
                // First interval: the blink has not fired, so the two fields agree exactly.
                Assert.Equal(steady.VelocityAt(-5f, t, 0, 4), blinking.VelocityAt(-5f, t, 0, 4));
            }

            for (double t = 100d; t < 200d; t += 6.1d)
            {
                // Second interval: same water, opposite parity.
                Float3 s = steady.VelocityAt(-5f, t, 0, 4);
                Float3 b = blinking.VelocityAt(-5f, t, 0, 4);

                Assert.Equal(-s.Y, b.Y);
                Assert.Equal(-s.X, b.X);
            }

            for (double t = 200d; t < 300d; t += 6.1d)
            {
                Assert.Equal(steady.VelocityAt(-5f, t, 0, 4), blinking.VelocityAt(-5f, t, 0, 4));
            }
        }

        [Fact]
        public void AtTheSurfaceTheFlowRunsFromTheUpLegToTheDownLegAndAtTheFloorItRunsBack()
        {
            // D066's convention, and the one thing a reader of the code has to be able to trust:
            // an overturning cell hands its water sideways at the top and takes it back at the
            // bottom. Everything downstream — which patch a body crosses into, which way a joule
            // of detritus goes — is that sign.
            CurrentField field = Rolling();

            int checkedTimes = 0;

            for (double t = 1d; t < 900d; t += 3.7d)
            {
                float wNearSurface = field.VelocityAt(-1f, t, 0, 4).Y;
                if (Math.Abs(wNearSurface) < 1e-4f) continue;

                checkedTimes++;

                // Whichever way patch 0 happens to be running this instant, patch 1 runs the other
                // way, and the surface flow leaves whichever is rising.
                int upLeg = wNearSurface > 0f ? 0 : 1;
                int downLeg = 1 - upLeg;

                // The boundary between them is boundary 0 — indexed by the left-hand patch — so
                // "from the up-leg to the down-leg" is +1 when the up-leg is patch 0.
                int expectedSurface = upLeg == 0 ? 1 : -1;

                Assert.Equal(expectedSurface, field.CrossingDirection(-1f, t, 0, 4));
                Assert.Equal(-expectedSurface, field.CrossingDirection(-24f, t, 0, 4));

                // The same statement read at the next boundary along, between patch 1 and patch 2.
                // Parity alternates, so that pair's up-leg is whichever of them patch 0 is not,
                // and the surface flow there runs the other way round the ring — which is what
                // makes the boundaries between rolls carry flow too, in the direction the two
                // patches' parities dictate.
                Assert.Equal(-expectedSurface, field.CrossingDirection(-1f, t, 1, 4));
                Assert.Equal(downLeg, upLeg == 0 ? 1 : 0);
            }

            Assert.True(checkedTimes > 20, $"only {checkedTimes} instants had any flow to check");
        }

        [Fact]
        public void TheCrossingFractionIsTheCourantNumberAndIsClampedAtAHalf()
        {
            CurrentField field = Rolling();

            double f = field.HorizontalCrossingFraction(-1f, 37d, 0, 4, 1d, 10f);
            double u = Math.Abs(field.VelocityAt(-1f, 37d, 0, 4).X);

            Assert.Equal(u * 1d / 10f, f, 12);

            // A step long enough to cross ten patches moves half of one, not ten.
            Assert.Equal(0.5d, field.HorizontalCrossingFraction(-1f, 37d, 0, 4, 10000d, 10f));

            // And off is off.
            Assert.Equal(0d, Steady().HorizontalCrossingFraction(-1f, 37d, 0, 4, 1d, 10f));
            Assert.Equal(0, Steady().CrossingDirection(-1f, 37d, 0, 4));
        }

        // ------------------------------------------------------------ advection of the fields

        [Fact]
        public void AdvectionConservesEveryJouleAndNeverDrivesACellNegative()
        {
            // The whole reason the transfer is written as upwind moves between two named cells
            // rather than as a per-cell divergence: §5A.2's audit is a hard equality across the
            // food web, and a transport scheme that leaked would show up there as biology.
            var field = new NutrientField(400f, 1f, 0f, 60f, patchCount: 4);
            var rng = new Rng(23);

            for (int layer = 0; layer < field.LayerCount; layer++)
            {
                for (int patch = 0; patch < 4; patch++)
                {
                    field.Deposit(-(layer + 0.5f), rng.NextFloat() * 500f, patch);
                }
            }

            double before = field.TotalJoules;
            CurrentField current = Rolling(blink: 137f);
            current.CellMetres = 60f;

            for (int i = 0; i < 1000; i++)
            {
                field.Advect(current, i * 0.5, 0.5f, field.PatchWidthMetres);

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

            double after = field.TotalJoules;
            _output.WriteLine($"{before:R} -> {after:R} ({Math.Abs(after - before) / before:0.0e+0} relative)");

            Assert.True(
                Math.Abs(after - before) <= 1e-6 * before,
                $"advection moved {Math.Abs(after - before):R} J of {before:R} that it should not have");
        }

        [Fact]
        public void StillWaterAdvectsNothing()
        {
            var field = new NutrientField(400f, 1f, 0f, 60f, patchCount: 4);
            field.Deposit(-10.5f, 1000f, 2);

            double[] before = Snapshot(field);

            CurrentField current = Rolling(speed: 0f);
            current.CellMetres = 60f;

            for (int i = 0; i < 500; i++) field.Advect(current, i, 1f, field.PatchWidthMetres);

            Assert.Equal(before, Snapshot(field));
        }

        [Fact]
        public void BlinkingRollsStirAPuddleAcrossTheWorld()
        {
            // The claim D066 is built on, stated as coarsely as it can be: start with everything
            // in one cell and the flow spreads it. Not a mixing-rate measurement — that belongs to
            // a run — but the difference between a mechanism that stirs and one that oscillates in
            // place, which is the difference D061's sealed pools were the last version of.
            Assert.True(SpreadCells(blink: 137f) >= 120, "blinking rolls did not stir the world");
        }

        [Fact]
        public void SteadyRollsStirTooButLess()
        {
            // Both reach every cell, and that is worth being honest about: an upwind scheme is
            // numerically diffusive, so some of this spreading is the discretisation rather than
            // the flow, and this test cannot tell the two apart. It is a floor — a mechanism that
            // moved nothing would fail it — and not a measurement of mixing. Whether blinking
            // rolls mix *faster* than steady ones is a question for a run with a tracer in it,
            // where the answer would be a rate rather than a count of occupied cells.
            int blinking = SpreadCells(blink: 137f);
            int steady = SpreadCells(blink: 0f);

            _output.WriteLine($"blinking reached {blinking} cells of 240, steady {steady}");

            Assert.True(steady >= 60, $"steady rolls reached only {steady} cells of 240");
        }

        /// <summary>Cells holding a non-negligible share after a stirring run. 240 in total.</summary>
        private static int SpreadCells(float blink)
        {
            var field = new NutrientField(400f, 1f, 0f, 60f, patchCount: 4);
            field.Deposit(-30.5f, 10000f, 0);

            CurrentField current = Rolling(blink: blink);
            current.CellMetres = 60f;

            for (int i = 0; i < 4000; i++) field.Advect(current, i, 1f, field.PatchWidthMetres);

            int occupied = 0;
            for (int layer = 0; layer < field.LayerCount; layer++)
            {
                for (int patch = 0; patch < 4; patch++)
                {
                    if (field.StockInLayer(layer, patch) > 1e-6) occupied++;
                }
            }

            return occupied;
        }

        // ------------------------------------------------------------ bodies

        [Fact]
        public void RollsCarryCreaturesBetweenPatches()
        {
            var config = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                MinimumPopulation = 40,
                MaximumPopulation = 400,
                HorizontalPatches = 4f,
                FounderDepthSpread = 20f,
                Current = new CurrentField
                {
                    Speed = 0.5f, CellMetres = 60f, PeriodSeconds = 300f,
                    Rolls = true, RollBlinkSeconds = 137f,
                },
            };

            var world = new World(config, seed: 7);
            for (int i = 0; i < 40; i++) world.Step(1f);

            var before = new Dictionary<long, int>();
            foreach (Organism c in world.Living) before[c.Id] = c.Patch;

            bool moved = false;
            for (int i = 0; i < 60 && !moved; i++)
            {
                world.Step(1f);

                foreach (Organism c in world.Living)
                {
                    if (before.TryGetValue(c.Id, out int p) && p != c.Patch) { moved = true; break; }
                }
            }

            Assert.True(moved, "no creature was carried across a patch boundary by the rolls");
        }

        [Fact]
        public void ConservationHoldsInALiveWorldUnderRollsAndAdvection()
        {
            var config = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                MinimumPopulation = 40,
                MaximumPopulation = 500,
                HorizontalPatches = 4f,
                NutrientMixingDiffusivity = 0.2f,
                InitialMatterPerCubicMetre = 1f,
                MatterPerTissueJoule = 0.01f,
                Current = new CurrentField
                {
                    Speed = 0.2f, CellMetres = 60f, PeriodSeconds = 300f,
                    Rolls = true, RollBlinkSeconds = 137f, AdvectFields = true,
                },
            };

            var world = new World(config, seed: 3);

            // StandingMatter is already the whole ledger — the field plus what is locked in
            // bodies — so this is the world's entire stock of matter, which nothing may create or
            // destroy and advection least of all.
            double matter = world.StandingMatter;

            for (int i = 0; i < 400; i++) world.Step(1f);

            double residual = Math.Abs(world.AuditResidual) / Math.Max(1d, world.EnergyIn);
            double matterNow = world.StandingMatter;

            _output.WriteLine($"energy residual {residual:0.0e+0}, matter {matter:R} -> {matterNow:R}");

            Assert.True(residual < 1e-6, $"the energy audit drifted to {residual:0.0e+0}");
            Assert.True(
                Math.Abs(matterNow - matter) <= 1e-6 * Math.Max(1d, matter),
                $"matter went from {matter:R} to {matterNow:R} under advection");
        }

        private static double[] Snapshot(NutrientField field)
        {
            var stocks = new double[field.LayerCount * field.PatchCount];

            for (int layer = 0; layer < field.LayerCount; layer++)
            {
                for (int patch = 0; patch < field.PatchCount; patch++)
                {
                    stocks[layer * field.PatchCount + patch] = field.StockInLayer(layer, patch);
                }
            }

            return stocks;
        }
    }
}

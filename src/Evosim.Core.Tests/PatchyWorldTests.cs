using System;
using System.Collections.Generic;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// The patchy world — horizontal structure, throttled exchange, endogenous inequality — D061.
    /// </summary>
    public class PatchyWorldTests
    {
        private readonly ITestOutputHelper _output;

        public PatchyWorldTests(ITestOutputHelper output) => _output = output;

        // ------------------------------------------------------------ K=1 bit-identity

        [Fact]
        public void KEqualsOneIsBitIdenticalToAWorldThatNeverHeardOfPatches()
        {
            // Default 1/0/0/0, and that has to mean bit-identical rather than nearly — every
            // result on file was measured in a world with one patch per layer, and a default
            // that perturbed anything would mean none of them describe a world that still exists
            // (the D031/D052/D055 rule, applied here). "Never heard of the knobs" and
            // "explicitly told their off values" are the same RunConfig values, so any divergence
            // is a bug in how patches reach the field, the organism or the lineage machinery, not
            // in the biology.
            string Trajectory(RunConfig config)
            {
                var world = new World(config, seed: 5);
                var sb = new StringBuilder();
                for (int i = 0; i < 400; i++)
                {
                    world.Step(1f);
                    sb.AppendLine(WorldStats.Sample(world).ToJson());
                    sb.AppendLine(
                        $"{world.Nutrients.TotalJoules:R}|{world.Matter.TotalJoules:R}|" +
                        $"{world.AuditResidual:R}|{world.StandingMatter:R}");
                }
                return sb.ToString();
            }

            var unset = new RunConfig { Light = new LightModel(300f, 12f) };
            var explicitOff = new RunConfig
            {
                Light = new LightModel(300f, 12f),
                HorizontalPatches = 1f,
                HorizontalMixingDiffusivity = 0f,
                DispersalChancePerStep = 0f,
                PerPatchShading = 0f,
            };

            Assert.Equal(1f, unset.HorizontalPatches);
            Assert.Equal(0f, unset.HorizontalMixingDiffusivity);
            Assert.Equal(0f, unset.DispersalChancePerStep);
            Assert.Equal(0f, unset.PerPatchShading);

            Assert.Equal(Trajectory(unset), Trajectory(explicitOff));
        }

        // ------------------------------------------------------------ conservation

        [Fact]
        public void ConservationHoldsUnderKFourWithMixingSettlingAndFeeding()
        {
            // Same shape as D055's EnergyIsConservedWithAFloorRefugeRunning: a live, feeding
            // population exercises Settle, both Mix passes and Take together, and §5A.2's audit
            // must still close exactly as it does at K=1.
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 600,
                HorizontalPatches = 4f,
                HorizontalMixingDiffusivity = 0.05f,
                NutrientMixingDiffusivity = 0.5f,
            };
            config.Light = new LightModel(400f, 12f);
            var world = new World(config, seed: 1);

            try { for (int i = 0; i < 400; i++) world.Step(1f); }
            catch (PopulationRunawayException e) { _output.WriteLine($"stopped: {e.Population} living"); }

            double residual = world.AuditResidual;
            double scale = Math.Max(1.0, world.EnergyIn);

            _output.WriteLine(
                $"in {world.EnergyIn:0.###} out {world.EnergyOut:0.###} " +
                $"standing {world.StandingJoules:0.###} residual {residual:0.######} ({residual / scale:P4})");

            Assert.True(
                Math.Abs(residual) / scale < 1e-4,
                $"K=4 with horizontal mixing opened a hole in the energy audit: {residual:0.###} J unaccounted for");
        }

        // ------------------------------------------------------------ horizontal mixing

        [Fact]
        public void HorizontalMixingMovesExactlyTheDocumentedFractionAndWrapsAtTheRing()
        {
            // K=3, so every pair of adjacent patches is a single, undoubled edge — a K=2 ring
            // doubles the same pair (both "sides" of a 2-wedge circle touch), which is a real
            // geometric feature of that case rather than a bug, but it complicates stating the
            // exact form cleanly. K=3 does not have that wrinkle.
            var field = new NutrientField(400f, 1f, 0f, 60f, patchCount: 3);
            field.Deposit(-0.5f, 1000f, patch: 0);

            const float diffusivity = 0.02f;
            const float seconds = 1f;
            field.Mix(seconds, 0f, diffusivity);

            double patchWidth = field.PatchWidthMetres; // sqrt(400 / 3), the documented geometry
            double fraction = diffusivity * seconds / (patchWidth * patchWidth);
            Assert.True(fraction < 0.5, "fraction must stay under Mix's own clamp for this exact check to hold");

            // Patch 0 is adjacent to both patch 1 ("next") and patch 2 ("previous", found only
            // by wrapping past the end of the ring) — so it gives fraction*1000 to each, and
            // patches 1 and 2 each receive exactly that much (their own mutual flux is
            // (0-0)*fraction = 0, since neither held anything before this step).
            Assert.Equal(1000.0 - 2.0 * fraction * 1000.0, field.StockInLayer(0, patch: 0), 6);
            Assert.Equal(fraction * 1000.0, field.StockInLayer(0, patch: 1), 6);
            Assert.Equal(fraction * 1000.0, field.StockInLayer(0, patch: 2), 6);
            Assert.Equal(1000.0, field.TotalJoules, 6);
        }

        [Fact]
        public void HorizontalMixingWithNoDiffusivityChangesNothing()
        {
            var field = new NutrientField(400f, 1f, 0f, 60f, patchCount: 4);
            field.Deposit(-0.5f, 1000f, patch: 0);

            for (int i = 0; i < 200; i++) field.Mix(1f, 0f, horizontalDiffusivity: 0f);

            Assert.Equal(1000.0, field.StockInLayer(0, patch: 0), 6);
            for (int patch = 1; patch < 4; patch++)
            {
                Assert.Equal(0.0, field.StockInLayer(0, patch), 12);
            }
        }

        // ------------------------------------------------------------ dispersal

        [Fact]
        public void DispersalIsInertWhenOff()
        {
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 200,
                HorizontalPatches = 4f,
                Light = new LightModel(300f, 12f),
            };
            var world = new World(config, seed: 7);
            for (int i = 0; i < 30; i++) world.Step(1f);

            var before = new Dictionary<long, int>();
            foreach (Organism c in world.Living) before[c.Id] = c.Patch;

            for (int i = 0; i < 30; i++) world.Step(1f);

            foreach (Organism c in world.Living)
            {
                if (before.TryGetValue(c.Id, out int p)) Assert.Equal(p, c.Patch);
            }
        }

        [Fact]
        public void DispersalMovesAtLeastOneCreatureWhenOnAtAHighChance()
        {
            var config = new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 200,
                HorizontalPatches = 4f,
                DispersalChancePerStep = 0.9f,
                Light = new LightModel(300f, 12f),
            };
            var world = new World(config, seed: 7);
            for (int i = 0; i < 20; i++) world.Step(1f);

            var before = new Dictionary<long, int>();
            foreach (Organism c in world.Living) before[c.Id] = c.Patch;

            world.Step(1f);

            bool anyMoved = false;
            foreach (Organism c in world.Living)
            {
                if (before.TryGetValue(c.Id, out int p) && p != c.Patch) { anyMoved = true; break; }
            }

            Assert.True(anyMoved, "no creature moved patch in one step at a 90% dispersal chance");
        }

        [Fact]
        public void OffspringInheritTheParentsPatch()
        {
            var config = new RunConfig
            {
                MinimumPopulation = 20,
                MaximumPopulation = 400,
                HorizontalPatches = 4f,
            };
            config.Light = new LightModel(4000f, 40f);
            var world = new World(config, seed: 3);

            bool checkedOne = false;

            for (int i = 0; i < 2000 && !checkedOne; i++)
            {
                world.Step(1f);

                foreach (LineageEvent evt in world.DrainLineageEvents())
                {
                    if (evt.Kind != LineageEventKind.Birth || evt.BirthKind != BirthKind.Reproduction)
                    {
                        continue;
                    }

                    Organism parent = null, child = null;
                    foreach (Organism c in world.Living)
                    {
                        if (c.Id == evt.ParentId) parent = c;
                        if (c.Id == evt.Id) child = c;
                    }

                    if (parent == null || child == null) continue;

                    Assert.Equal(parent.Patch, child.Patch);
                    Assert.Equal(parent.Patch, evt.Patch);
                    checkedOne = true;
                    break;
                }
            }

            Assert.True(checkedOne, "no reproduction with both parent and child still alive to check");
        }

        // ------------------------------------------------------------ per-patch shading

        [Fact]
        public void PerPatchShadingDarkensACrowdedPatchAndNotItsNeighbour()
        {
            var model = new LightModel(100f, 12f);
            var field = new LightField(model, worldArea: 400f, layerMetres: 1f, patchCount: 2, perPatchShading: true);

            field.Clear();
            field.Contribute(-0.5f, 100000f, patch: 0); // a dense mat, patch 0 only
            field.Solve();

            float shadedPatch = field.IrradianceAt(-10.5f, patch: 0);
            float openPatch = field.IrradianceAt(-10.5f, patch: 1);

            Assert.True(
                shadedPatch < openPatch / 100f,
                $"shaded patch {shadedPatch:0.###}, open neighbour {openPatch:0.######}");

            // With per-patch shading off (the default), the same contribution darkens both
            // patches equally — the pooled canopy D061 keeps as the off state.
            var pooled = new LightField(model, 400f, 1f, patchCount: 2, perPatchShading: false);
            pooled.Clear();
            pooled.Contribute(-0.5f, 100000f, patch: 0);
            pooled.Solve();

            Assert.Equal(pooled.IrradianceAt(-10.5f, patch: 0), pooled.IrradianceAt(-10.5f, patch: 1));
        }

        // ------------------------------------------------------------ pre-D061 signatures

        [Fact]
        public void PerDepthOldSignatureApisThrowWhenPatchCountIsAboveOneAndWorkAtOne()
        {
            var multi = new NutrientField(400f, 1f, 0.02f, 60f, patchCount: 3);

            Assert.Throws<InvalidOperationException>(() => multi.Deposit(-5f, 10f));
            Assert.Throws<InvalidOperationException>(() => multi.DensityAt(-5f));
            Assert.Throws<InvalidOperationException>(() => multi.EdibleDensityAt(-5f));
            Assert.Throws<InvalidOperationException>(() => multi.Demand(-5f, 10f));
            Assert.Throws<InvalidOperationException>(() => multi.ShareAt(-5f));
            Assert.Throws<InvalidOperationException>(() => multi.Take(-5f, 10f));
            Assert.Throws<InvalidOperationException>(() => multi.StockInLayer(0));

            var single = new NutrientField(400f, 1f, 0.02f, 60f);
            single.Deposit(-5f, 10f);
            Assert.Equal(10f, single.DensityAt(-5f) * single.LayerVolume, 3);
        }

        // ------------------------------------------------------------ determinism

        [Fact]
        public void SameSeedAndConfigAtKFourReplaysIdentically()
        {
            RunConfig Config() => new RunConfig
            {
                MinimumPopulation = 30,
                MaximumPopulation = 2000,
                HorizontalPatches = 4f,
                HorizontalMixingDiffusivity = 0.05f,
                DispersalChancePerStep = 0.1f,
                PerPatchShading = 1f,
                Light = new LightModel(300f, 12f),
            };

            string Trajectory()
            {
                var world = new World(Config(), seed: 11);
                var sb = new StringBuilder();

                for (int i = 0; i < 500; i++)
                {
                    world.Step(1f);
                    sb.AppendLine(WorldStats.Sample(world).ToJson());
                    sb.AppendLine($"{world.Nutrients.TotalJoules:R}|{world.AuditResidual:R}");

                    // Which patch every living creature is in is D061's own state — not covered
                    // by WorldStats — so it has to be part of the trajectory too.
                    foreach (Organism c in world.Living) sb.Append(c.Id).Append(':').Append(c.Patch).Append(' ');
                    sb.AppendLine();
                }

                return sb.ToString();
            }

            Assert.Equal(Trajectory(), Trajectory());
        }

        // ------------------------------------------------------------ refuge per patch

        [Fact]
        public void RefugeFractionAppliesIndependentlyToEachPatch()
        {
            var field = new NutrientField(
                400f, 1f, 0f, 5f, refugeMetres: 1f, refugeEdibleFraction: 0.5f, patchCount: 2);

            int floor = field.LayerCount - 1;
            const float floorDepth = -4.5f;
            Assert.True(field.IsRefuge(floor));

            field.Deposit(floorDepth, 1000f, patch: 0);
            field.Deposit(floorDepth, 2000f, patch: 1);

            Assert.Equal(500f, field.EdibleDensityAt(floorDepth, patch: 0) * field.LayerVolume, 2);
            Assert.Equal(1000f, field.EdibleDensityAt(floorDepth, patch: 1) * field.LayerVolume, 2);

            // Taking everything patch 0 will give up must not touch patch 1's stock at all.
            float taken = field.Take(floorDepth, 10000f, patch: 0);
            Assert.Equal(500f, taken, 2);
            Assert.Equal(2000.0, field.StockInLayer(floor, patch: 1), 6);
        }

        // ------------------------------------------------------------ lineage

        [Fact]
        public void LineageBirthEventsCarryThePatch()
        {
            var config = new RunConfig { HorizontalPatches = 4f };
            config.Light = new LightModel(300f, 12f);
            var world = new World(config, seed: 3);

            // Enough founding steps that the floor's trickle (2 per step, MinimumPopulation's
            // default of 40) has produced a real sample of patch draws to check.
            var events = new List<LineageEvent>();
            for (int i = 0; i < 25; i++)
            {
                world.Step(1f);
                events.AddRange(world.DrainLineageEvents());
            }

            Assert.NotEmpty(events);

            bool sawNonZeroPatch = false;
            foreach (LineageEvent evt in events)
            {
                Assert.Equal(LineageEventKind.Birth, evt.Kind);
                Assert.InRange(evt.Patch, 0, 3);

                Organism match = null;
                foreach (Organism c in world.Living)
                {
                    if (c.Id == evt.Id) { match = c; break; }
                }
                if (match != null) Assert.Equal(match.Patch, evt.Patch);

                if (evt.Patch != 0) sawNonZeroPatch = true;
            }

            Assert.True(
                sawNonZeroPatch,
                "every founder landed in patch 0 across 4 patches and many draws — suspiciously uniform");
        }
    }
}

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

            // 90 W/m2 rather than 300 since fable-propose-growth.md (2026-09-08): cheaper
            // reproduction carries several times the head-count at the same light, and both
            // worlds met the ceiling before the trajectories could be compared.
            var unset = new RunConfig { Light = new LightModel(90f, 12f) };
            var explicitOff = new RunConfig
            {
                Light = new LightModel(90f, 12f),
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

                // 90 W/m2 rather than 300 — see KEqualsOneIsBitIdenticalToAWorldThatNeverHeardOfPatches.
                Light = new LightModel(90f, 12f),
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
            bool sawBirth = false;
            foreach (LineageEvent evt in events)
            {
                // Deaths are in the drain too since fable-propose-growth.md (2026-09-08): a
                // founder is born at its own genome's birth fraction with the floor's purse
                // scaled the same way, so the poorest of them starve inside this window where
                // they used to start on the whole 200 J. The subject here is the patch a birth
                // row carries, so a death row is skipped rather than asserted about.
                if (evt.Kind != LineageEventKind.Birth) continue;

                sawBirth = true;
                Assert.InRange(evt.Patch, 0, 3);

                Organism match = null;
                foreach (Organism c in world.Living)
                {
                    if (c.Id == evt.Id) { match = c; break; }
                }
                if (match != null) Assert.Equal(match.Patch, evt.Patch);

                if (evt.Patch != 0) sawNonZeroPatch = true;
            }

            Assert.True(sawBirth, "no birth rows at all, so nothing was checked");
            Assert.True(
                sawNonZeroPatch,
                "every founder landed in patch 0 across 4 patches and many draws — suspiciously uniform");
        }

        // ------------------------------------------------------------ the layout (fable-propose-box.md)

        [Fact]
        public void ALayoutOfOneIsTheRowEveryRunOnFileWasMeasuredIn()
        {
            // 100 m² over four patches is W = 5 m, so a row of them is 20 m long and 5 m
            // across — the campaign's own box. Clause 2: at A = 1 the index is
            // floor(x / W) mod K term for term, whatever z is, because there is nowhere else
            // for a body to be.
            var field = new GridField(100f, 0f, 60f, 0f, 0f, 4, 1f);

            Assert.Equal(1, field.PatchesAcross);
            Assert.Equal(4, field.PatchesAlong);
            Assert.Equal(20f, field.LengthMetres);
            Assert.Equal(5f, field.WidthMetres);

            var rng = new Rng(101UL);
            for (int i = 0; i < 500; i++)
            {
                // Well outside the box as well as inside it, so the wrap is under test too.
                float x = (rng.NextFloat() * 3f - 1f) * field.LengthMetres;
                float z = (rng.NextFloat() * 3f - 1f) * field.WidthMetres;

                Assert.Equal(Ring(x, 20f, 5f, 4), field.PatchOf(x, z));
            }
        }

        [Fact]
        public void QuadrantCentresAndSeamCornersIndexAsTheLayoutSays()
        {
            // The same area and the same four patches, laid two by two: a 10 by 10 m
            // footprint of quadrants numbered along x first (clause 3).
            var field = new GridField(100f, 0f, 60f, 0f, 0f, 4, 1f, patchesAcross: 2);

            Assert.Equal(2, field.PatchesAcross);
            Assert.Equal(2, field.PatchesAlong);
            Assert.Equal(10f, field.LengthMetres);
            Assert.Equal(10f, field.WidthMetres);

            // The four centres.
            Assert.Equal(0, field.PatchOf(2.5f, 2.5f));
            Assert.Equal(1, field.PatchOf(7.5f, 2.5f));
            Assert.Equal(2, field.PatchOf(2.5f, 7.5f));
            Assert.Equal(3, field.PatchOf(7.5f, 7.5f));

            // The four seam corners. A seam belongs to the patch it opens, not to the one it
            // closes, which is the half-open interval D077 already wrote the ring on.
            Assert.Equal(0, field.PatchOf(0f, 0f));
            Assert.Equal(1, field.PatchOf(5f, 0f));
            Assert.Equal(2, field.PatchOf(0f, 5f));
            Assert.Equal(3, field.PatchOf(5f, 5f));

            // Both axes are rings, so the far corner is the near one and a step back from it
            // is the last quadrant.
            Assert.Equal(0, field.PatchOf(10f, 10f));
            Assert.Equal(3, field.PatchOf(9.999f, 9.999f));
            Assert.Equal(3, field.PatchOf(-0.001f, -0.001f));

            // The vertex field answers the same question the same way, because a world may
            // run either and the per-patch bins have to mean one thing.
            var vertices = new VertexField(
                100f, 12f, 0f, 60f, 0f, 0f, 4, 1f, 0.5f, 1000, 1f, 5UL, patchesAcross: 2);

            Assert.Equal(10f, vertices.LengthMetres);
            Assert.Equal(10f, vertices.WidthMetres);
            Assert.Equal(0, vertices.PatchOf(2.5f, 2.5f));
            Assert.Equal(1, vertices.PatchOf(7.5f, 2.5f));
            Assert.Equal(2, vertices.PatchOf(2.5f, 7.5f));
            Assert.Equal(3, vertices.PatchOf(7.5f, 7.5f));
        }

        [Fact]
        public void ALayoutOfOneChangesNothingAboutAWorld()
        {
            // Clause 2, asked of a whole world rather than of an index: the default has to be
            // the world every run on file was measured in, bit for bit, or none of them
            // describes a world that still exists. The same demand the K = 1 test above makes
            // of D061's own knob, for the same reason.
            string Trajectory(RunConfig config)
            {
                var world = new World(config, seed: 5);
                var sb = new StringBuilder();
                for (int i = 0; i < 200; i++)
                {
                    world.Step(1f);
                    sb.AppendLine(WorldStats.Sample(world).ToJson());
                    sb.AppendLine(
                        $"{world.Nutrients.TotalJoules:R}|{world.Matter.TotalJoules:R}|" +
                        $"{world.AuditResidual:R}|{world.StandingMatter:R}");
                }
                return sb.ToString();
            }

            var unset = new RunConfig { Light = new LightModel(90f, 12f) };
            var explicitOne = new RunConfig
            {
                Light = new LightModel(90f, 12f),
                HorizontalPatches = 4f,
                PatchesAcross = 1f,
            };

            Assert.Equal(1f, unset.PatchesAcross);

            var row = new RunConfig { Light = new LightModel(90f, 12f), HorizontalPatches = 4f };
            Assert.Equal(Trajectory(row), Trajectory(explicitOne));
        }

        [Fact]
        public void ALayoutThatDoesNotDivideThePatchesIsRefused()
        {
            // Clause 1. Three across four leaves a part row, and a part row is not a box.
            var config = new RunConfig
            {
                Light = new LightModel(90f, 12f),
                HorizontalPatches = 4f,
                PatchesAcross = 3f,
            };

            ArgumentException refused = Assert.Throws<ArgumentException>(() => new World(config, seed: 1));

            _output.WriteLine(refused.Message);
            Assert.Contains("PatchesAcross is 3 and HorizontalPatches is 4", refused.Message);
            Assert.Contains("1 patches over", refused.Message);
        }

        [Fact]
        public void TheCellFieldIsRefusedAboveOne()
        {
            // Clause 5. NutrientField mixes and advects across a one-dimensional ring, so at
            // A > 1 it would stir a geometry the world does not have.
            var config = new RunConfig
            {
                Light = new LightModel(90f, 12f),
                HorizontalPatches = 4f,
                PatchesAcross = 2f,
            };

            Assert.Equal(MatterField.Cells, config.FieldModel);

            ArgumentException refused = Assert.Throws<ArgumentException>(() => new World(config, seed: 1));

            _output.WriteLine(refused.Message);
            Assert.Contains("PatchesAcross is 2 and FieldModel is Cells", refused.Message);
        }

        [Fact]
        public void TheDispersalLotteryIsRefusedAboveOne()
        {
            // Clause 5's other half: D061's lottery walks the same ring, one patch ahead or
            // one behind modulo K, which on a two-by-two layout is a diagonal as often as a
            // sideways step.
            var config = new RunConfig
            {
                Light = new LightModel(90f, 12f),
                SharedSpace = true,
                FieldModel = MatterField.Grid,
                WorldAreaSquareMetres = 100f,
                WorldDepthMetres = 60f,
                HorizontalPatches = 4f,
                PatchesAcross = 2f,
                DispersalChancePerStep = 0.1f,
            };

            ArgumentException refused = Assert.Throws<ArgumentException>(() => new World(config, seed: 1));

            _output.WriteLine(refused.Message);
            Assert.Contains("PatchesAcross is 2 and DispersalChancePerStep is 0.1", refused.Message);
        }

        /// <summary>D077's index on a row of patches: <c>floor(x / W) mod K</c>, wrapped first.</summary>
        private static int Ring(float x, float length, float width, int patches)
        {
            float wrapped = x - length * (float)Math.Floor(x / length);
            if (wrapped >= length || wrapped < 0f) wrapped = 0f;

            int patch = (int)Math.Floor(wrapped / width) % patches;
            return patch < 0 ? patch + patches : patch;
        }
    }
}

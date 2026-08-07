using System;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>Competition for a finite sun — DESIGN.md §5A.2b, D023.</summary>
    public class LightFieldTests
    {
        private static LightField Field(float surface = 100f, float area = 400f, float layer = 1f) =>
            new LightField(new LightModel(surface, 12f), area, layer);

        [Fact]
        public void AnEmptyFieldIsExactlyTheUnshadedModel()
        {
            // The property that makes this safe to introduce: it can only take light away from a
            // crowd, never add any to anyone, and an early world behaves precisely as before.
            var model = new LightModel(137f, 9f);
            var field = new LightField(model, 400f, 1f);
            field.Clear();
            field.Solve();

            foreach (float y in new[] { 0f, 5f, -0.5f, -1f, -7.25f, -30f, -100f })
            {
                Assert.Equal(model.IrradianceAt(y), field.IrradianceAt(y), 3);
            }
        }

        [Fact]
        public void ShadingIsContinuousThroughAnEmptyWorld()
        {
            // A min(1, L/A) form would jump at the density where competition starts to matter.
            // The exponential does not: irradiance approaches the unshaded value smoothly as the
            // shadow vanishes, so nothing can sit on a discontinuity we introduced.
            var field = Field();
            float unshaded = field.Model.IrradianceAt(-0.5f);

            float previous = float.MaxValue;
            foreach (float shadow in new[] { 1e-4f, 1e-2f, 1f, 100f, 10000f })
            {
                field.Clear();
                field.Contribute(-0.5f, shadow);
                field.Solve();

                float lit = field.IrradianceAt(-0.5f);
                Assert.True(lit <= unshaded, "shading may never hand out more light than the sun");
                Assert.True(
                    lit < previous,
                    $"shadow {shadow:R} gave {lit:R} W/m², up from {previous:R} (unshaded {unshaded:R})");
                previous = lit;
            }

            // Vanishing shadow, and the answer converges on the unshaded model — a shadow of
            // 10⁻⁶ m² in a 400 m² world costs nothing float can represent, which is the right
            // answer rather than a rounding failure.
            field.Clear();
            field.Contribute(-0.5f, 1e-6f);
            field.Solve();
            Assert.Equal(unshaded, field.IrradianceAt(-0.5f), 4);
        }

        [Fact]
        public void TotalCapturedPowerNeverExceedsWhatTheSunDelivers()
        {
            // §5A.2's audit as a hard bound rather than a plausibility check. Whatever evolution
            // discovers, it cannot capture more light than falls on the world.
            var field = Field(surface: 100f, area: 400f);

            foreach (float perCreature in new[] { 0.1f, 1f, 10f, 1000f })
            {
                field.Clear();
                for (int i = 0; i < 500; i++) field.Contribute(-(i % 20) - 0.5f, perCreature);
                field.Solve();

                float captured = 0f;
                for (int i = 0; i < 500; i++)
                {
                    captured += field.IrradianceAt(-(i % 20) - 0.5f) * perCreature;
                }

                Assert.True(
                    captured <= field.IncidentWatts * 1.0001f,
                    $"captured {captured:0} W of an incident {field.IncidentWatts:0} W");
            }
        }

        [Fact]
        public void CrowdingDrivesIncomePerCreatureToZero()
        {
            // The density dependence the model was missing. Without it a population above
            // break-even is a linear birth process and grows without bound at every calibration.
            var field = Field();

            field.Clear();
            field.Contribute(-0.5f, 1f);
            field.Solve();
            float alone = field.IrradianceAt(-0.5f);

            const int Crowd = 100000;
            field.Clear();
            for (int i = 0; i < Crowd; i++) field.Contribute(-0.5f, 1f);
            field.Solve();
            float crowded = field.IrradianceAt(-0.5f);

            Assert.True(crowded < alone / 100f, $"alone {alone:0.###}, crowded {crowded:0.######}");

            // And it goes to the right place, not merely downwards. Once the layer intercepts
            // essentially all the light, the whole incident power is divided between all the
            // shadow present — so total income is pinned at the sun's output and the per-creature
            // share falls as 1/N. That is the negative feedback the population dynamics were
            // missing entirely. Slightly under, never over: the layer's occupants sit half a metre
            // down and the water above them takes its cut first.
            float captured = crowded * Crowd;
            Assert.InRange(captured, field.IncidentWatts * 0.9f, field.IncidentWatts);
        }

        [Fact]
        public void ACanopyDarkensWhatIsBeneathIt()
        {
            // Shading is directional: light absorbed above never reaches below. This is what makes
            // depth an economic gradient rather than only an optical one.
            var field = Field();

            field.Clear();
            field.Solve();
            float openWater = field.IrradianceAt(-10.5f);

            field.Clear();
            field.Contribute(-0.5f, 100000f);   // a dense mat at the surface
            field.Solve();
            float shaded = field.IrradianceAt(-10.5f);

            Assert.True(shaded < openWater / 100f, $"open {openWater:0.###}, shaded {shaded:0.######}");
        }

        [Fact]
        public void ShadingDoesNotReachSideways()
        {
            // A creature shades what is below it, not what is beside it. Layers are the whole
            // reason the field is more than a single scalar.
            var field = Field(layer: 1f);

            field.Clear();
            field.Contribute(-0.5f, 100000f);
            field.Contribute(-5.5f, 1f);
            field.Solve();

            float atTheMat = field.IrradianceAt(-0.5f);

            field.Clear();
            field.Contribute(-0.5f, 100000f);
            field.Solve();

            // Adding one creature five layers down changed nothing at the surface.
            Assert.Equal(atTheMat, field.IrradianceAt(-0.5f), 4);
        }

        [Fact]
        public void AWorldWithNoAreaIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new LightField(new LightModel(), 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new LightField(new LightModel(), float.PositiveInfinity, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new LightField(new LightModel(), 400f, 0f));
        }

        [Fact]
        public void TheWorldFeedsEveryoneThroughTheField()
        {
            // Guards against the field being built, solved, and then ignored — the shape of fault
            // this project has hit twice (logbook/0007, logbook/0008). Two worlds differing only
            // in aperture must reach different states.
            // Both just above the transition (§5A.2b). Aperture is the only difference, and it
            // must be the only difference: at the 400 W/m² default the wide world is a hundred
            // times the standard one and legitimately reaches fifty thousand creatures, which
            // stops the run for a reason that has nothing to do with what is being tested.
            var narrow = new RunConfig
            {
                MinimumPopulation = 30, MaximumPopulation = 50000,
                FloorSpawnsPerStep = 2, WorldAreaSquareMetres = 25f,
                Light = new LightModel(48f, 12f),
            };

            var wide = new RunConfig
            {
                MinimumPopulation = 30, MaximumPopulation = 50000,
                FloorSpawnsPerStep = 2, WorldAreaSquareMetres = 40000f,
                Light = new LightModel(48f, 12f),
            };

            var a = new World(narrow, 1);
            var b = new World(wide, 1);

            for (int i = 0; i < 4000; i++) { a.Step(1f); b.Step(1f); }

            Assert.NotEqual(a.Living.Count, b.Living.Count);
            Assert.True(
                b.Births > a.Births,
                $"a 1600x wider world produced {b.Births} births against {a.Births} — " +
                "aperture is not reaching the metabolic path");
        }
    }
}

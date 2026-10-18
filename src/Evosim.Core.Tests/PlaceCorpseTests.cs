using System;
using System.Linq;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// <see cref="World.PlaceCorpse"/>, the nursery's door (propose-nursery.md): an experimenter's
    /// corpse is booked in both books as an inoculant is, and is then any other corpse.
    /// </summary>
    public class PlaceCorpseTests
    {
        private static RunConfig Dark(float decay) => new RunConfig
        {
            Light = new LightModel(1e-6f, 1f),
            MinimumPopulation = 0,
            WorldDepthMetres = 20f,
            RemineralisationPerSecond = 0f,
            CorpseDecayPerSecond = decay,
            CorpseSinkMetresPerSecond = 0.05f,
            CorpseSettles = true,
        };

        [Fact]
        public void AWorldWhoseDeathsDepositAtOnceRefusesAPlacedCorpse()
        {
            var world = new World(Dark(0f), seed: 1);
            Assert.Throws<InvalidOperationException>(() => world.PlaceCorpse(new Float3(1f, -5f, 1f), 100d));
            Assert.Equal(0, world.Corpses.Count);
        }

        [Fact]
        public void APlacedCorpseIsBookedAsInfluxAndDecaysWithBothBooksClosed()
        {
            var world = new World(Dark(0.001f), seed: 1);
            double inBefore = world.EnergyIn;

            Corpse a = world.PlaceCorpse(new Float3(1f, -5f, 1f), 100d);
            Corpse b = world.PlaceCorpse(new Float3(3f, -8f, 2f), 50d);

            Assert.Equal(2, world.CorpsesPlaced);
            Assert.Equal(-1, a.CreatureId);
            Assert.Equal(-2, b.CreatureId);
            Assert.Equal(150d, world.EnergyIn - inBefore, 9);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.PlaceCorpse(new Float3(1f, -5f, 1f), 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => world.PlaceCorpse(new Float3(1f, -500f, 1f), 1d));

            for (int i = 0; i < 1200; i++) world.Step(0.5f);

            Assert.Equal(2, world.Corpses.Count);
            Assert.True(world.Corpses.All(c => c.Joules < c.FirstJoules), "a placed corpse did not decay");
            Assert.True(
                Math.Abs(world.AuditResidual) <= 1e-6 * Math.Max(1d, world.StandingJoules),
                $"audit residual {world.AuditResidual:R}");
            Assert.True(
                Math.Abs(world.MatterResidual) <= 1e-6 * Math.Max(1d, world.MatterInitialTotal),
                $"matter residual {world.MatterResidual:R}");
        }

        [Fact]
        public void ATankTakesACorpseOnItsAxisAndRefusesOnePastItsGlass()
        {
            // The tank's axis is at (R, R) (World.KeepInTheWater). The nursery's first runs clamped
            // their corpses about the origin and laid most of them far from the body.
            RunConfig config = Dark(0.001f);
            config.SharedSpace = true;
            config.WorldShape = WorldShape.Tank;
            config.FieldModel = MatterField.Grid;
            config.WorldAreaSquareMetres = 100f;
            config.HorizontalPatches = 1;
            config.FieldCellMetres = 1f;
            config.FieldMatterCellMetres = 2f;
            config.NutrientMixingDiffusivity = 0.02f;
            config.HorizontalMixingDiffusivity = 0.02f;
            config.MatterMixingDiffusivity = 0.02f;
            config.MatterBudgetUnits = 100f;
            var world = new World(config, seed: 1);
            float r = world.TankRadiusMetres;
            Assert.True(r > 5f, $"tank radius {r}");

            world.PlaceCorpse(new Float3(r, -5f, r), 10d);
            world.PlaceCorpse(new Float3(r + 0.9f * r, -5f, r), 10d);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.PlaceCorpse(new Float3(0.5f, -5f, 0.5f), 10d));
            Assert.Equal(2, world.Corpses.Count);
        }
    }
}
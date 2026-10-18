using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Round 52's exact door (<see cref="IMatterField.DepositExact"/>): a double handed to a grid
    /// is held as that double, where the float door rounds it first. Round 51's B1 residual was
    /// that rounding, kept to one sign at the burn (<c>logbook/specs/r51-read/b1-probe.txt</c>).
    /// </summary>
    public class ExactDepositTests
    {
        private static World GridWorld() => new World(new RunConfig
        {
            Light = new LightModel(100f, 12f),
            SharedSpace = true,
            FieldModel = MatterField.Grid,
            WorldAreaSquareMetres = 144f,
            HorizontalPatches = 4,
            WorldDepthMetres = 24f,
            NutrientMixingDiffusivity = 0.2f,
            HorizontalMixingDiffusivity = 0.2f,
            MatterMixingDiffusivity = 0.2f,
            InitialMatterPerCubicMetre = 0f,
            MinimumPopulation = 0,
            MaximumPopulation = 100_000,
        }, 1UL);

        private static readonly FieldPoint At = new FieldPoint(new Float3(1.5f, -2.5f, 1.5f), 0);

        [Fact]
        public void ADoubleThroughTheExactDoorIsHeldExactly()
        {
            World world = GridWorld();
            double tenth = 0.1d;

            world.Matter.DepositExact(At, tenth);
            Assert.Equal(tenth, world.Matter.TotalJoules);

            world.Nutrients.Deposit(At, (float)tenth);
            Assert.Equal((double)(float)tenth, world.Nutrients.TotalJoules);
            Assert.NotEqual(tenth, world.Nutrients.TotalJoules);
        }

        [Fact]
        public void AFloatThroughEitherDoorLandsTheSame()
        {
            World a = GridWorld();
            World b = GridWorld();
            float x = 0.3f;

            a.Matter.Deposit(At, x);
            b.Matter.DepositExact(At, x);

            Assert.Equal(a.Matter.TotalJoules, b.Matter.TotalJoules);
            Assert.Equal(a.Matter.DensityAt(At), b.Matter.DensityAt(At));
        }

        [Fact]
        public void NothingOrLessIsNoDeposit()
        {
            World world = GridWorld();

            world.Matter.DepositExact(At, 0d);
            world.Matter.DepositExact(At, -1d);
            world.Matter.DepositExact(At, double.NaN);

            Assert.Equal(0d, world.Matter.TotalJoules);
        }
    }
}
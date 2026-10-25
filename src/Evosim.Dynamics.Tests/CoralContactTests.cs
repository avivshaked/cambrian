using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

using PortFloor = Evosim.Dynamics.Placement.PlacementFloor;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D194's coral to the solver and the placer (<c>logbook/specs/coral-build.md</c>, step 2), on
    /// <see cref="ReefContactTests"/>' terms: a sphere sinking onto a plate rests on it, one against the
    /// trunk is pushed out, a body in the rock is a diverged body, and no body is placed in it.
    /// </summary>
    public sealed class CoralContactTests
    {
        private readonly ITestOutputHelper _out;

        public CoralContactTests(ITestOutputHelper output) => _out = output;

        private const double PlateY = -7d;

        /// <summary>One colony at (10, 10): a trunk 0.5 m in radius from −12 m to −7 m, a plate 1.5 m in radius on it.</summary>
        private static CoralRock OneColony()
        {
            var trunk = new CoralGeometry.Piece
            {
                Kind = CoralGeometry.PieceKind.Capsule, Parent = -1, AX = 10, AY = -12, AZ = 10, BX = 10, BY = PlateY, BZ = 10, Radius = 0.5,
            };
            var plate = new CoralGeometry.Piece
            {
                Kind = CoralGeometry.PieceKind.Plate, Parent = 0, Depth = 1, AX = 10, AY = PlateY, AZ = 10, BX = 10, BY = PlateY, BZ = 10,
                Radius = 1.5, HalfThickness = 0.125,
            };
            var colony = new CoralGeometry.Colony { X = 10, Z = 10, BaseY = -12, Island = -1, FirstPiece = 0, PieceCount = 2 };
            var coral = new CoralGeometry(0.15f, new[] { colony }, new[] { trunk, plate });
            return new CoralRock(coral, CoralFootprint.Tank(10f), 2f, 0.25f, 0.3f, 4f);
        }

        private static SolverConfig Water(double excessDensity)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.WorldDepthMetres = 12;
            config.FloorIsSolid = true;
            config.TissueExcessDensity = excessDensity;
            config.CreatureContact = true;
            config.ContactInstrument = true;
            config.Coral = OneColony();
            return config;
        }

        private static Creature Ball(SolverConfig config, Vec3 at)
        {
            Creature body = TestBodies.Build(TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f)), config);
            body.PlaceAt(at, QuatD.Identity);
            return body;
        }

        private static (double Y, double Lowest) Run(SolverConfig config, Creature body, int steps, int settle)
        {
            var world = new DynamicsWorld(config);
            world.Add(body);
            double lowest = double.MaxValue;

            for (int step = 0; step < steps; step++)
            {
                world.Step();
                Assert.True(body.Alive, $"the body was lost at step {step}");
                if (step >= settle) lowest = Math.Min(lowest, body.Position[1]);
            }

            return (body.Position[1], lowest);
        }

        [Fact]
        public void ASphereSinkingOntoAPlateRestsOnIt()
        {
            SolverConfig config = Water(excessDensity: 100);
            Creature body = Ball(config, new Vec3(10.8, -4.0, 10.3));
            double r = body.ContactRadius;

            (double y, double lowest) = Run(config, body, steps: 2000, settle: 1500);

            double rest = PlateY + 0.125 + r;
            _out.WriteLine($"radius {r:0.###} m: rests at y {y:0.####} against {rest:0.####} (plate top + radius), lowest {lowest:0.####}; touched {body.TouchedBedOrGlass}");
            Assert.True(Math.Abs(y - rest) < 0.02, $"rests at {y}, expected {rest}");
            Assert.True(body.TouchedBedOrGlass);
        }

        [Fact]
        public void ASphereAgainstTheTrunkIsPushedOut()
        {
            SolverConfig config = Water(excessDensity: 0);
            double r = Ball(config, Vec3.Zero).ContactRadius;

            double start = 0.5 + 0.5 * r;
            Creature body = Ball(config, new Vec3(10 + start, -10.0, 10));
            Run(config, body, steps: 500, settle: 0);

            double dx = body.Position[0] - 10, dz = body.Position[2] - 10;
            double from = Math.Sqrt(dx * dx + dz * dz);
            _out.WriteLine($"started {start:0.###} m from the trunk's axis; ended {from:0.####} m out");
            Assert.True(from >= 0.5 + r - 0.01, $"only {from} m from the axis");
        }

        [Fact]
        public void ABodyInTheRockIsDiverged()
        {
            SolverConfig config = Water(excessDensity: 0);
            Creature body = Ball(config, new Vec3(10, -10.0, 10));
            Assert.True(Divergence.Diverged(body, config, out string reason));
            _out.WriteLine(reason);
            Assert.Contains("inside coral colony 0", reason);

            Creature resting = Ball(config, new Vec3(10.5, PlateY + 0.125 + 0.9 * body.ContactRadius, 10));
            Assert.False(Divergence.Diverged(resting, config, out string none), none);
        }

        [Fact]
        public void ThePlacerRefusesTheRockAndLandsNothingOnIt()
        {
            var floor = new PortFloor(12f, null, null, OneColony());
            Assert.True(floor.HasReefs);
            const float r = 0.35f;

            float y = -10f;
            Assert.False(floor.ClearOfReefs(10.2f, ref y, 10f, r));   // in the trunk
            y = (float)PlateY + 0.2f;
            Assert.False(floor.ClearOfReefs(10.5f, ref y, 10f, r));   // on the plate: no table to land on
            Assert.Equal((float)PlateY + 0.2f, y);
            y = -10f;
            Assert.True(floor.ClearOfReefs(14f, ref y, 10f, r));      // well away: unchanged
            Assert.Equal(-10f, y);
        }
    }
}

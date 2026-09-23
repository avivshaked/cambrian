using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

using PortVolume = Evosim.Dynamics.Placement.SharedVolume;
using PortFloor = Evosim.Dynamics.Placement.PlacementFloor;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The rock to the solver and the placer — <c>logbook/specs/reef-spec.md</c> §4, test 6: a
    /// sphere released above a cap settles on its top; one rising under it rests against the
    /// underside; one against the stem is pushed out; a body in the rock is a diverged body; and a
    /// body cannot be placed in the rock, a founder over a cap landing on its table.
    /// </summary>
    public sealed class ReefContactTests
    {
        private readonly ITestOutputHelper _out;

        public ReefContactTests(ITestOutputHelper output) => _out = output;

        /// <summary>One reef on the axis: a cap 3 m in radius, 2 m thick at 3 m, a 1 m stem.</summary>
        private static ReefGeometry OneReef() =>
            new ReefGeometry(3f, 3f, 2f, 1f, 3f, new[] { 0d }, new[] { 0d });

        private static SolverConfig Water(double excessDensity)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.WorldDepthMetres = 12;
            config.FloorIsSolid = true;
            config.TissueExcessDensity = excessDensity;
            config.CreatureContact = true;
            config.ContactInstrument = true;
            config.Reefs = OneReef();
            return config;
        }

        private static Creature Ball(SolverConfig config, Vec3 at)
        {
            Creature body = TestBodies.Build(TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f)), config);
            body.PlaceAt(at, QuatD.Identity);
            return body;
        }

        private (double Y, double Lowest, double Highest) Run(SolverConfig config, Creature body, int steps, int settle)
        {
            var world = new DynamicsWorld(config);
            world.Add(body);

            double lowest = double.MaxValue, highest = double.MinValue;

            for (int step = 0; step < steps; step++)
            {
                world.Step();
                Assert.True(body.Alive, $"the body was lost at step {step}");

                if (step >= settle)
                {
                    lowest = Math.Min(lowest, body.Position[1]);
                    highest = Math.Max(highest, body.Position[1]);
                }
            }

            return (body.Position[1], lowest, highest);
        }

        [Fact]
        public void ASphereReleasedAboveACapSettlesOnItsTop()
        {
            SolverConfig config = Water(excessDensity: 100);
            Creature body = Ball(config, new Vec3(1.2, -1.0, 0.5));
            double r = body.ContactRadius;

            (double y, double lowest, double highest) = Run(config, body, steps: 2000, settle: 1500);

            double rest = config.Reefs.CapTopY(0) + r;
            _out.WriteLine(
                $"radius {r:0.###} m: rests at y {y:0.####} against {rest:0.####} (cap top + radius), " +
                $"{lowest:0.####} to {highest:0.####} over the last 5 s; touched rock {body.TouchedBedOrGlass}");

            Assert.True(Math.Abs(y - rest) < 0.02, $"rests at {y}, expected {rest}");
            Assert.True(lowest > rest - 0.02);
        }

        [Fact]
        public void ASphereReleasedOverALobedCapSettlesOnItsTop()
        {
            // An irregular cap (r0 3 m, lobes of up to 0.3 of it) at 3 m on a 0.75 m stem: a ball
            // dropped over a lobe well inside the outline rests on the table at the cap's top.
            var lobed = new ReefGeometry(2f, 3f, new[]
            {
                new ReefGeometry.Reef
                {
                    X = 0d, Z = 0d, CapRadius = 3d, CapDepth = 3d, StemRadius = 0.75d,
                    A2 = 0.12d, A3 = 0.1d, A4 = 0.08d, P2 = 0.4d, P3 = 2.1d, P4 = 5.0d,
                },
            });

            SolverConfig config = Water(excessDensity: 100);
            config.Reefs = lobed;

            // Over the outline's widest lobe, halfway out.
            double best = 0d, at = 0d;
            for (int k = 0; k < 360; k++)
            {
                double theta = 2d * Math.PI * k / 360d;
                double o = lobed.OutlineRadius(0, theta);
                if (o > best) { best = o; at = theta; }
            }

            double reach = 0.5d * best;
            Creature body = Ball(config, new Vec3(reach * Math.Cos(at), -1.0, reach * Math.Sin(at)));
            double r = body.ContactRadius;

            (double y, double lowest, _) = Run(config, body, steps: 2000, settle: 1500);

            double rest = lobed.CapTopY(0) + r;
            _out.WriteLine(
                $"over a lobe {best:0.###} m wide at {at * 180d / Math.PI:0} degrees: rests at y {y:0.####} against " +
                $"{rest:0.####} (cap top + radius)");

            Assert.True(Math.Abs(y - rest) < 0.02, $"rests at {y}, expected {rest}");
            Assert.True(lowest > rest - 0.02);
        }

        [Fact]
        public void PerPartContactReadsTheSameRock()
        {
            // A three-link chain dropped on the cap with contact per part: every link rests on the
            // table and none is inside the rock.
            SolverConfig config = Water(excessDensity: 100);
            config.ContactPerPart = true;

            Creature body = TestBodies.Build(TestBodies.Chain(links: 3), config);
            body.PlaceAt(new Vec3(0.8, -1.5, 0.3), QuatD.Identity);
            Assert.True(body.LinkContactActive, "the chain was built without per-link spheres");

            Run(config, body, steps: 2000, settle: 1500);

            double deepest = double.MaxValue;
            for (int link = 0; link < body.Links; link++)
            {
                double x = body.LinkContactCentre[3 * link];
                double y = body.LinkContactCentre[3 * link + 1];
                double z = body.LinkContactCentre[3 * link + 2];
                deepest = Math.Min(deepest, config.Reefs.SignedDistance(x, y, z) - body.LinkContactRadius[link]);
            }

            _out.WriteLine($"per part: the deepest link sphere reaches {-deepest:0.####} m into the rock; touched {body.TouchedBedOrGlass}");
            Assert.True(deepest > -0.02, $"a link sphere is {-deepest} m inside the rock");
            Assert.True(body.Position[1] > config.Reefs.CapUndersideY(0), "the chain fell past the cap");
        }

        [Fact]
        public void ASphereUnderACapIsPushedDownAndABuoyantOneRestsAgainstTheUnderside()
        {
            // A wide thin cap, so the underside is flat well clear of the stem's fillet: under the
            // test reef's cap (3 m, 2 m thick, a 1 m stem) the disc's flat is 2 m across and the
            // fillet tilts all of it, and a buoyant ball slides out along it and round the rim,
            // which is the rock doing what it should and not the reading this test wants.
            var wide = new ReefGeometry(5f, 3f, 1f, 1f, 3f, new[] { 0d }, new[] { 0d });

            // Neutral, overlapping the underside by half its radius: pushed down and out of it.
            SolverConfig still = Water(excessDensity: 0);
            still.Reefs = wide;
            Creature probe = Ball(still, Vec3.Zero);
            double r = probe.ContactRadius;
            double underside = wide.CapUndersideY(0);

            Creature pushed = Ball(still, new Vec3(2.5, underside - 0.5 * r, 0.3));
            Run(still, pushed, steps: 300, settle: 0);
            _out.WriteLine(
                $"started {0.5 * r:0.###} m into the underside at {underside:0.##} m; ended at y {pushed.Position[1]:0.####}, " +
                $"{underside - r - pushed.Position[1]:0.####} m clear of touching");
            Assert.True(pushed.Position[1] <= underside - r + 0.005, $"ended at {pushed.Position[1]}");

            // Buoyant, released a metre and a half below: rises and is held at the underside.
            SolverConfig rising = Water(excessDensity: -100);
            rising.Reefs = wide;
            Creature body = Ball(rising, new Vec3(2.5, underside - 1.5, 0.3));

            (double y, double lowest, double highest) = Run(rising, body, steps: 2000, settle: 1500);

            double rest = underside - r;
            _out.WriteLine(
                $"radius {r:0.###} m: a buoyant ball held at y {y:0.####} against {rest:0.####} (underside − radius), " +
                $"{lowest:0.####} to {highest:0.####} over the last 5 s");

            Assert.True(Math.Abs(y - rest) < 0.02, $"held at {y}, expected {rest}");
            Assert.True(highest < rest + 0.02);
        }

        [Fact]
        public void ASphereAgainstTheStemIsPushedOut()
        {
            SolverConfig config = Water(excessDensity: 0);
            Creature probe = Ball(config, Vec3.Zero);
            double r = probe.ContactRadius;

            double start = 1.0 + 0.5 * r;
            Creature body = Ball(config, new Vec3(start, -8.0, 0));

            Run(config, body, steps: 500, settle: 0);

            double x = body.Position[0], z = body.Position[2];
            double from = Math.Sqrt(x * x + z * z);

            _out.WriteLine($"started {start:0.###} m from the axis, overlapping the stem by {0.5 * r:0.###} m; ended {from:0.####} m out");
            Assert.True(from >= 1.0 + r - 0.01, $"only {from} m from the axis");
        }

        [Fact]
        public void ABodyInTheRockIsDiverged()
        {
            SolverConfig config = Water(excessDensity: 0);
            Creature body = Ball(config, new Vec3(0, -8.0, 0));

            Assert.True(Divergence.Diverged(body, config, out string reason));
            _out.WriteLine(reason);
            Assert.Contains("inside reef 0", reason);

            // Resting on the cap is touching the rock, not in it.
            Creature resting = Ball(config, new Vec3(1.0, config.Reefs.CapTopY(0) + 0.9 * body.ContactRadius, 0));
            Assert.False(Divergence.Diverged(resting, config, out string none), none);
        }

        [Fact]
        public void TheFloorRuleRefusesTheRockAndLandsAFounderOnTheTable()
        {
            var floor = new PortFloor(12f, null, OneReef());
            const float r = 0.35f;
            float need = r + PortFloor.ClearanceMetres;

            float y = -8f;
            Assert.False(floor.ClearOfReefs(0.5f, ref y, 0f, r)); // in the stem

            y = -5.2f;
            Assert.False(floor.ClearOfReefs(1.5f, ref y, 0f, r)); // against the underside

            y = -3.2f;
            Assert.True(floor.ClearOfReefs(1.5f, ref y, 0f, r)); // in the cap's upper half: landed
            Assert.Equal(-3f + need, y, 5);

            y = -8f;
            Assert.True(floor.ClearOfReefs(6f, ref y, 0f, r)); // well away: unchanged
            Assert.Equal(-8f, y);

            // With no reefs the rule is not asked and changes nothing.
            var plain = new PortFloor(12f);
            Assert.False(plain.HasReefs);
            y = -8f;
            Assert.True(plain.ClearOfReefs(0f, ref y, 0f, r));
        }

        [Fact]
        public void TheShortestLandingRefusesABodyThatWouldBreakTheSurface()
        {
            // A cap at 0.5 m, the shallowest the refusals allow on a stem: a body a metre across
            // would stand out of the water on its table, so it is drawn again.
            var floor = new PortFloor(12f, null, new ReefGeometry(3f, 0.5f, 1f, 1f, 3f, new[] { 0d }, new[] { 0d }));
            float y = -0.6f;
            Assert.False(floor.ClearOfReefs(1f, ref y, 0f, 0.5f));
        }

        [Fact]
        public void NoFounderIsPlacedInTheRock()
        {
            // A tank of 1,600 m² with three reefs, 400 founders at the caps' upper half.
            float area = 1600f;
            float radius = TankGeometry.RadiusFor(area);
            var xs = new double[3];
            var zs = new double[3];
            for (int k = 0; k < 3; k++)
            {
                double a = 0.3 + 2 * Math.PI * k / 3;
                xs[k] = radius + 8 * Math.Cos(a);
                zs[k] = radius + 8 * Math.Sin(a);
            }

            var reefs = new ReefGeometry(3f, 3f, 2f, 1f, 3f, xs, zs);
            var volume = new PortVolume(4, 20f, 12f, 7UL, 0f, 1, WorldShape.Tank, radius)
            {
                Floor = new PortFloor(12f, null, reefs),
            };

            Phenotype body = Developer.Develop(TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f)));
            float bodyRadius = PortVolume.BoundingRadius(body);

            int landed = 0, placed = 0;

            for (int i = 0; i < 400; i++)
            {
                float height = -3.6f;
                if (!volume.TryReserveFounder(body, ref height, out _)) continue;

                volume.Commit(i);
                Assert.True(volume.TryTakePlacement(i, out Float3 at));
                placed++;

                double distance = reefs.SignedDistance(at.X, at.Y, at.Z);
                Assert.True(
                    distance >= bodyRadius + PortFloor.ClearanceMetres - 1e-4,
                    $"founder {i} at ({at.X:0.##}, {at.Y:0.##}, {at.Z:0.##}) is {distance:0.###} m from the rock");

                if (Math.Abs(at.Y - (reefs.CapTopY(0) + bodyRadius + PortFloor.ClearanceMetres)) < 1e-4) landed++;
            }

            _out.WriteLine($"{placed} founders placed, {landed} landed on a cap's table, {volume.Rejections} candidates refused");
            Assert.True(placed > 300);
            Assert.True(landed > 0);
        }
    }
}

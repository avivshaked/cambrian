using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D199's drag limiter: <c>RunConfig.DragLimitByWeldedBody</c>, which caps each welded body's
    /// drag by its own mass and inertia, and <c>RunConfig.DragLimitAtEveryStep</c>, which opens
    /// the limiter's gate at the 0.01 s step.
    /// </summary>
    public sealed class WeldedDragLimiterTests
    {
        private readonly ITestOutputHelper _out;

        public WeldedDragLimiterTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        /// A blade of <paramref name="parts"/> thin plates welded end to end by a self-edge, each
        /// 5 m long, 0.5 m wide and 4 cm thick by default: a 40 m rigid chain at eight.
        /// </summary>
        private static Genome Blade(int parts, Float3? half = null)
        {
            Genome g = TestBodies.Box(half ?? new Float3(2.5f, 0.02f, 0.25f));
            g.Nodes[0].RecursiveLimit = parts;
            g.Nodes[0].Edges.Add(new MorphEdge
            {
                Child = 0,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });
            return g;
        }

        [Fact]
        public void TheGateOpensAtTheRoundsStepOnlyWhenAsked()
        {
            Assert.False(TestBodies.Water(0.01).DragLimiterEngages);
            Assert.True(TestBodies.Water(0.02).DragLimiterEngages);

            SolverConfig always = TestBodies.Water(0.01);
            always.DragLimitAtEveryStep = true;
            Assert.True(always.DragLimiterEngages);

            // The drive's gate is its own.
            Assert.False(always.LimitersEngage);
        }

        [Fact]
        public void TheConfigCarriesBothSwitchesToTheSolverAndBothAreOffByDefault()
        {
            var config = new RunConfig();
            Assert.False(config.DragLimitByWeldedBody);
            Assert.False(config.DragLimitAtEveryStep);

            config.DragLimitByWeldedBody = true;
            config.DragLimitAtEveryStep = true;
            SolverConfig s = SolverConfig.From(config, 0.01);
            Assert.True(s.DragLimitByWeldedBody);
            Assert.True(s.DragLimitAtEveryStep);
            Assert.True(s.DragLimiterEngages);
        }

        [Fact]
        public void TheEigenSolveReturnsOrthonormalAxesThatDiagonaliseTheMatrix()
        {
            var rng = new Random(7);
            for (int trial = 0; trial < 200; trial++)
            {
                double a00 = rng.NextDouble() * 10, a11 = rng.NextDouble() * 10, a22 = rng.NextDouble() * 10;
                double a01 = rng.NextDouble() - 0.5, a02 = rng.NextDouble() - 0.5, a12 = rng.NextDouble() - 0.5;
                if (trial % 4 == 0) { a01 = 0; a02 = 0; a12 = 0; }

                Creature.SymmetricEigen(a00, a11, a22, a01, a02, a12, out Vec3 values, out Mat3 v);
                var m = new Mat3(a00, a01, a02, a01, a11, a12, a02, a12, a22);

                for (int k = 0; k < 3; k++)
                {
                    var axis = new Vec3(
                        k == 0 ? v.M00 : k == 1 ? v.M01 : v.M02,
                        k == 0 ? v.M10 : k == 1 ? v.M11 : v.M12,
                        k == 0 ? v.M20 : k == 1 ? v.M21 : v.M22);
                    double value = k == 0 ? values.X : k == 1 ? values.Y : values.Z;

                    Assert.True(Math.Abs(axis.Magnitude - 1) < 1e-12);
                    Assert.True(((m * axis) - axis * value).Magnitude < 1e-9);
                }

                Mat3 product = v.Transposed * v;
                Assert.True(Math.Abs(product.M01) + Math.Abs(product.M02) + Math.Abs(product.M12) < 1e-12);
            }
        }

        [Fact]
        public void TheForceIsCappedLinkByLinkUnderEitherLaw()
        {
            // A box driven through the water far faster than one 0.02 s step's drag can allow,
            // with no spin: the force cap is the per-link law's under both.
            Genome genome = TestBodies.Box(new Float3(0.3f, 0.1f, 0.05f));

            Vec3 Force(bool welded, out long limited)
            {
                SolverConfig config = TestBodies.Water(0.02);
                config.DragLimitByWeldedBody = welded;
                Creature body = TestBodies.Build(genome, config);
                body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
                Vec3.Write(body.Velocity, 0, new Vec3(400, 0, 0));
                Array.Clear(body.Fext, 0, body.Fext.Length);
                Fluid.Apply(body, config);
                limited = body.DragImpulsesLimited;
                return Vec3.Read(body.Fext, 3);
            }

            Vec3 perLink = Force(false, out long perLinkLimited);
            Vec3 weld = Force(true, out long weldLimited);

            _out.WriteLine($"per link {perLink.X:E6} ({perLinkLimited}), welded {weld.X:E6} ({weldLimited})");
            Assert.Equal(1, perLinkLimited);
            Assert.Equal(1, weldLimited);
            Assert.True((perLink - weld).Magnitude <= 1e-9 * perLink.Magnitude);
        }

        [Fact]
        public void TheWeldedLawHoldsASpinningFortyMetreBladeAtTheRoundsStep()
        {
            // logbook/specs/r62-blades.md: the per-link law, engaged at the 0.01 s step, ran a 42 m
            // rigid kelp blade away from a tenth of a radian a second. The welded law must hold this
            // blade from every axis and leave it turning slower than it started.
            Genome genome = Blade(8);

            foreach (double start in new[] { 0.1, 1.0, 10.0 })
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    SolverConfig config = TestBodies.Water(0.01);
                    config.DragLimitAtEveryStep = true;
                    config.DragLimitByWeldedBody = true;

                    Creature body = TestBodies.Build(genome, config);
                    Assert.Equal(8, body.Links);
                    body.PlaceAt(new Vec3(0, -500, 0), QuatD.Identity);
                    var w0 = new Vec3(axis == 0 ? start : 0, axis == 1 ? start : 0, axis == 2 ? start : 0);
                    Vec3.Write(body.Spin, 0, w0);

                    var world = new DynamicsWorld(config);
                    world.Add(body);

                    double peak = 0;
                    for (int s = 0; s < 300; s++)
                    {
                        world.Step();
                        double rate = Vec3.Read(body.Spin, 0).Magnitude;
                        Assert.True(rate < 1e6, $"ran away from {start} rad/s about axis {axis} at step {s + 1}");
                        peak = Math.Max(peak, rate);
                    }

                    double end = Vec3.Read(body.Spin, 0).Magnitude;
                    _out.WriteLine($"from {start} about {"xyz"[axis]}: peak {peak:F4}, after 3 s {end:F4}, limited {body.DragImpulsesLimited}");
                    Assert.True(peak <= start * 1.01, $"spun up from {start} about axis {axis} to {peak}");
                    Assert.True(end < start, $"not slowed from {start} about axis {axis}");
                }
            }
        }

        [Fact]
        public void AShortBladeRollingFastAtTheCoarseStepIsNotFlung()
        {
            // logbook/specs/r62-blades.md: capped by the whole body's mass against its centre's
            // velocity, the force let a roll at 100 rad/s about a short blade's long axis throw the
            // blade's centre to 182 m/s in one 0.02 s step, and the blade ran away. Capped link by
            // link, as the law now caps it, the roll is stopped and the blade stays where it was.
            Genome genome = Blade(8, new Float3(0.35f, 0.02f, 0.15f));

            SolverConfig config = TestBodies.Water(0.02);
            config.DragLimitByWeldedBody = true;
            Assert.True(config.DragLimiterEngages);

            Creature body = TestBodies.Build(genome, config);
            Assert.Equal(8, body.Links);
            body.PlaceAt(new Vec3(0, -500, 0), QuatD.Identity);
            Vec3.Write(body.Spin, 0, new Vec3(100, 0, 0));

            var world = new DynamicsWorld(config);
            world.Add(body);

            double fastest = 0, peak = 0;
            for (int s = 0; s < 150; s++)
            {
                world.Step();
                double rate = Vec3.Read(body.Spin, 0).Magnitude;
                double speed = Vec3.Read(body.Velocity, 0).Magnitude;
                Assert.True(rate < 1e6, $"ran away at step {s + 1}");
                peak = Math.Max(peak, rate);
                fastest = Math.Max(fastest, speed);
            }

            _out.WriteLine($"peak spin {peak:F3} rad/s, fastest root {fastest:F3} m/s, limited {body.DragImpulsesLimited}");
            Assert.True(peak <= 100.0 * 1.01, $"spun up to {peak}");
            Assert.True(fastest < 50.0, $"the root reached {fastest} m/s");
        }
    }
}

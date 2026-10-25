using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D229's fin law: a stroking limb's added mass follows its shape and the direction of its
    /// motion (<c>SolverConfig.ShapedAddedMass</c>, <see cref="Fluid.AddedMassOf"/>).
    /// </summary>
    public sealed class FinLawTests
    {
        private readonly ITestOutputHelper _out;

        public FinLawTests(ITestOutputHelper output) => _out = output;

        private const double Rho = 1000.0;
        private const double Omega = 6.0;
        private const double Push = 0.3;

        private static readonly Vec3 UnitX = new Vec3(1, 0, 0);
        private static readonly Vec3 UnitY = new Vec3(0, 1, 0);
        private static readonly Vec3 UnitZ = new Vec3(0, 0, 1);

        /// <summary>Equal to a part in a million: the half-extents are floats.</summary>
        private static void Near(double expected, double actual) =>
            Assert.True(Math.Abs(actual - expected) <= 1e-6 * Math.Abs(expected), $"expected {expected}, got {actual}");

        // ---- the function

        [Fact]
        public void ASphereTakesTheUnshapedValueExactly()
        {
            var h = new Float3(0.1f, 0.1f, 0.1f);
            double volume = 0.0123;
            double got = Fluid.AddedMassOf(ShapeIds.Sphere, h, Mat3.Identity, UnitY, UnitZ, Rho, 0.5, volume);
            Assert.Equal(0.5 * Rho * volume, got);
        }

        [Fact]
        public void ABoxTakesThePlateFormulaOnItsWidthAcrossTheMotionAndTheLimb()
        {
            var h = new Float3(0.05f, 0.3f, 0.02f);

            // Length along local Y, moving along local Z: the width is along X, 0.1.
            double broad = Fluid.AddedMassOf(ShapeIds.Box, h, Mat3.Identity, UnitY, UnitZ, Rho, 0.5, 1.0);
            Near(Rho * Math.PI / 4.0 * 0.1 * 0.1 * 0.6, broad);

            // Moving along local X: the width is along Z, 0.04.
            double edge = Fluid.AddedMassOf(ShapeIds.Box, h, Mat3.Identity, UnitY, UnitX, Rho, 0.5, 1.0);
            Near(Rho * Math.PI / 4.0 * 0.04 * 0.04 * 0.6, edge);

            Assert.True(broad > edge);
        }

        [Fact]
        public void ABoxMovingAlongTheLimbOrRotatedIntoTheFrameIsRead()
        {
            var h = new Float3(0.05f, 0.3f, 0.02f);

            // No transverse motion: nothing to push.
            Assert.Equal(0.0, Fluid.AddedMassOf(ShapeIds.Box, h, Mat3.Identity, UnitY, UnitY, Rho, 0.5, 1.0));

            // The same box turned a quarter about Z: its local Y now lies along world -X and its
            // local X along world Y. Limb along world X, moving along world Z, is the first case.
            Mat3 quarter = new Mat3(0, -1, 0, 1, 0, 0, 0, 0, 1);
            double turned = Fluid.AddedMassOf(ShapeIds.Box, h, quarter, UnitX, UnitZ, Rho, 0.5, 1.0);
            Near(Rho * Math.PI / 4.0 * 0.1 * 0.1 * 0.6, turned);
        }

        [Fact]
        public void ABoxWhoseLimbRunsObliquelyIsReadByItsCalipers()
        {
            // The limb along the diagonal of local X and Y, moving along local Z. n = axis x motion
            // = (s, -s, 0), so both calipers are 2 (s * 0.1 + s * 0.05).
            var h = new Float3(0.1f, 0.05f, 0.03f);
            double s = 1.0 / Math.Sqrt(2.0);
            var axis = new Vec3(s, s, 0);
            double caliper = 2.0 * (s * 0.1 + s * 0.05);
            double expected = Rho * Math.PI / 4.0 * caliper * caliper * caliper;

            double got = Fluid.AddedMassOf(ShapeIds.Box, h, Mat3.Identity, axis, UnitZ, Rho, 0.5, 1.0);
            _out.WriteLine($"L = w = {caliper:0.######} m, added mass {got:0.#####} kg, expected {expected:0.#####} kg");
            Near(expected, got);

            // A motion with a part along the limb is read by its transverse part alone.
            Vec3 slanted = new Vec3(s * 0.5, s * 0.5, 1.0);
            double lean = Fluid.AddedMassOf(ShapeIds.Box, h, Mat3.Identity, axis, slanted * (1.0 / slanted.Magnitude), Rho, 0.5, 1.0);
            Near(expected, lean);
        }

        [Fact]
        public void ACapsuleTakesItsStripPlusItsEndsInEveryDirection()
        {
            var h = new Float3(0.05f, 0.3f, 0.03f);
            double r = 0.04;
            double halfSpan = 0.3 - r;
            double expected = Rho * Math.PI * r * r * (2.0 * halfSpan) + 0.5 * Rho * 4.0 / 3.0 * Math.PI * r * r * r;

            foreach (Vec3 direction in new[] { UnitX, UnitZ, new Vec3(0.6, 0, 0.8) })
            {
                double got = Fluid.AddedMassOf(ShapeIds.Capsule, h, Mat3.Identity, UnitY, direction, Rho, 0.5, 1.0);
                Near(expected, got);
            }
        }

        [Fact]
        public void AnUnknownShapeIsRefusedByName()
        {
            ArgumentException refused = Assert.Throws<ArgumentException>(() =>
                Fluid.AddedMassOf("torus", new Float3(1, 1, 1), Mat3.Identity, UnitY, UnitZ, Rho, 0.5, 1.0));
            Assert.Contains("torus", refused.Message);
        }

        [Fact]
        public void TheLeverIsTheReachToday()
        {
            Assert.Equal(0.2371, Fluid.LeverOf(0.2371, UnitX, UnitY));
        }

        // ---- the pushes

        private static SolverConfig Water(bool shaped, bool powered, double push = Push)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = push;
            config.PowerPushRadiansPerSecond = powered ? Omega : 0;
            config.PoseHertz = 2.0;
            config.ShapedAddedMass = shaped;
            return config;
        }

        private static Creature Limb(SolverConfig config, Float3 half, string shape = null)
        {
            Genome g = TestBodies.Chain(links: 2, power: 10f, linkHalf: half);
            if (shape != null) g.Nodes[1].ShapeId = shape;
            Creature body = TestBodies.Build(g, config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            return body;
        }

        private static Vec3 ForceOn(Creature body, SolverConfig config, int link)
        {
            Array.Clear(body.Fext, 0, body.Fext.Length);
            Fluid.Apply(body, config);
            return Vec3.Read(body.Fext, 6 * link + 3);
        }

        /// <summary>The powered stroke's push at s = 0.5 on link 1, drag and all removed.</summary>
        private static Vec3 PoweredPush(bool shaped, Float3 half, string shape = null)
        {
            SolverConfig config = Water(shaped, powered: true);
            Creature body = Limb(config, half, shape);
            Vec3 still = ForceOn(body, config, 1);
            body.Drive.Stroke[0] = 0.5;
            return ForceOn(body, config, 1) - still;
        }

        /// <summary>The swinging limb's push on link 1 turning about Z at 2 rad/s.</summary>
        private static Vec3 SwingPush(bool shaped, Float3 half, string shape = null)
        {
            SolverConfig config = Water(shaped, powered: false);
            Creature body = Limb(config, half, shape);
            Vec3.Write(body.Spin, 3, new Vec3(0, 0, 2));
            Vec3 pushed = ForceOn(body, config, 1);

            SolverConfig none = Water(shaped, powered: false, push: 0);
            Creature other = Limb(none, half, shape);
            Vec3.Write(other.Spin, 3, new Vec3(0, 0, 2));
            return pushed - ForceOn(other, none, 1);
        }

        // Equal volume: 8 * 0.2 * 0.01 * 0.2 = 8 * 0.2 * s * s with s = sqrt(0.002).
        private static readonly Float3 Paddle = new Float3(0.2f, 0.01f, 0.2f);
        private static readonly Float3 Stick = new Float3(0.2f, (float)Math.Sqrt(0.002), (float)Math.Sqrt(0.002));

        [Fact]
        public void WithTheSwitchOnAPaddleIsPushedHarderThanAStickOfEqualVolume()
        {
            Creature paddle = Limb(Water(true, true), Paddle);
            Creature stick = Limb(Water(true, true), Stick);
            Assert.Equal(paddle.Volume[1], stick.Volume[1], 6);

            // Off, a push depends on the volume alone.
            Near(PoweredPush(false, Paddle).X, PoweredPush(false, Stick).X);
            Near(SwingPush(false, Paddle).X, SwingPush(false, Stick).X);

            // On, the paddle moves its broad face through the water (hinge stroke along Y, the
            // paddle thin in Y and wide in Z) and the stick does not.
            double paddleStroke = -PoweredPush(true, Paddle).X;
            double stickStroke = -PoweredPush(true, Stick).X;
            double paddleSwing = -SwingPush(true, Paddle).X;
            double stickSwing = -SwingPush(true, Stick).X;
            _out.WriteLine($"stroke: paddle {paddleStroke:0.#####} N, stick {stickStroke:0.#####} N; swing: paddle {paddleSwing:0.#####} N, stick {stickSwing:0.#####} N");
            Assert.True(paddleStroke > 2 * stickStroke);
            Assert.True(paddleSwing > 2 * stickSwing);

            // The powered push is the plate formula, at the reach, for the stroke s * Omega.
            double wide = 2 * 0.2, length = 2 * 0.2;
            double addedMass = Rho * Math.PI / 4.0 * wide * wide * length;
            double expected = Push * addedMass * 0.2 * (0.5 * Omega) * (0.5 * Omega);
            Near(expected, paddleStroke);

            double expectedSwing = Push * addedMass * 0.2 * 4.0;
            Near(expectedSwing, paddleSwing);
        }

        [Fact]
        public void ASphereLimbsPushIsTheSameBitsOnAndOff()
        {
            var half = new Float3(0.1f, 0.1f, 0.1f);
            Vec3 on = PoweredPush(true, half, ShapeIds.Sphere);
            Vec3 off = PoweredPush(false, half, ShapeIds.Sphere);
            Assert.NotEqual(0.0, off.X);
            Assert.Equal(off.X, on.X);
            Assert.Equal(off.Y, on.Y);
            Assert.Equal(off.Z, on.Z);

            Vec3 swingOn = SwingPush(true, half, ShapeIds.Sphere);
            Vec3 swingOff = SwingPush(false, half, ShapeIds.Sphere);
            Assert.NotEqual(0.0, swingOff.X);
            Assert.Equal(swingOff.X, swingOn.X);
            Assert.Equal(swingOff.Y, swingOn.Y);
            Assert.Equal(swingOff.Z, swingOn.Z);
        }

        [Fact]
        public void ACapsuleLimbIsPushedByItsStripAndEnds()
        {
            var half = new Float3(0.2f, 0.05f, 0.05f);
            double stroke = -PoweredPush(true, half, ShapeIds.Capsule).X;

            // A capsule's long axis is local Y, so this one has radius (0.2 + 0.05) / 2 and a half-span
            // of 0.05 - r < 0, which is zero: two ends and no strip. Its surface is r from its centre,
            // so the lever is r.
            double r = (0.2 + 0.05) / 2.0;
            double addedMass = 0.5 * Rho * 4.0 / 3.0 * Math.PI * r * r * r;
            double expected = Push * addedMass * r * (0.5 * Omega) * (0.5 * Omega);
            _out.WriteLine($"capsule stroke {stroke:0.#####} N, expected {expected:0.#####} N");
            Near(expected, stroke);
        }
    }
}

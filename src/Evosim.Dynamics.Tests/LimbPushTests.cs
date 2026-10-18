using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// propose-foraging.md's first rule: a link swinging about its joint is pushed along its own
    /// axis toward the joint by C_T * m_a * r * (the swing across the axis) squared, and nothing
    /// else is.
    /// </summary>
    public sealed class LimbPushTests
    {
        private readonly ITestOutputHelper _out;

        public LimbPushTests(ITestOutputHelper output) => _out = output;

        private static SolverConfig Water(double push)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = push;
            return config;
        }

        private static Vec3 ForceOn(Creature body, SolverConfig config, int link)
        {
            Array.Clear(body.Fext, 0, body.Fext.Length);
            Fluid.Apply(body, config);
            return Vec3.Read(body.Fext, 6 * link + 3);
        }

        private static Creature Chain(SolverConfig config)
        {
            Creature body = TestBodies.Build(TestBodies.Chain(links: 2), config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            return body;
        }

        [Fact]
        public void ALinkSwingingAcrossItsAxisIsPushedTowardItsJoint()
        {
            SolverConfig on = Water(0.3), off = Water(0);
            Creature body = Chain(on);

            // The link hangs off the root's +x face, so its axis from the joint is +x; a swing
            // about y at 2 rad/s is wholly across it.
            Vec3.Write(body.Spin, 3, new Vec3(0, 2, 0));

            Vec3 without = ForceOn(body, off, 1);
            Vec3 with = ForceOn(body, on, 1);
            Vec3 push = with - without;

            double addedMass = 0.5 * 1000 * body.Volume[1];
            double expected = 0.3 * addedMass * 0.2 * 4.0;
            _out.WriteLine($"volume {body.Volume[1]:0.#####} m3, push ({push.X:0.####}, {push.Y:0.####}, {push.Z:0.####}) N, expected {-expected:0.####} along x");

            Assert.InRange(push.X, -expected * (1 + 1e-6), -expected * (1 - 1e-6));
            Assert.InRange(push.Y, -1e-9, 1e-9);
            Assert.InRange(push.Z, -1e-9, 1e-9);

            // The root is never pushed: it has no joint to swing about.
            Vec3 rootWithout = ForceOn(body, off, 0);
            Vec3 rootWith = ForceOn(body, on, 0);
            Assert.Equal(rootWithout.X, rootWith.X);
            Assert.Equal(rootWithout.Y, rootWith.Y);
            Assert.Equal(rootWithout.Z, rootWith.Z);
        }

        [Fact]
        public void ATwistAboutTheAxisAndASharedTurnPushNothing()
        {
            SolverConfig on = Water(0.3), off = Water(0);
            Creature body = Chain(on);

            // A twist about the link's own axis.
            Vec3.Write(body.Spin, 3, new Vec3(3, 0, 0));
            Vec3 twist = ForceOn(body, on, 1) - ForceOn(body, off, 1);
            Assert.Equal(0.0, twist.Magnitude, 12);

            // The whole body turning as one: the link does not swing relative to its parent.
            Vec3.Write(body.Spin, 0, new Vec3(0, 2, 0));
            Vec3.Write(body.Spin, 3, new Vec3(0, 2, 0));
            Vec3 shared = ForceOn(body, on, 1) - ForceOn(body, off, 1);
            Assert.Equal(0.0, shared.Magnitude, 12);
        }

        [Fact]
        public void ARigidBodyAndALinkAboveTheWaterAreNotPushed()
        {
            SolverConfig on = Water(0.3), off = Water(0);

            Creature box = TestBodies.Build(TestBodies.Box(new Float3(0.2f, 0.2f, 0.2f)), on);
            box.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            Vec3.Write(box.Spin, 0, new Vec3(1, 2, 3));
            Assert.Equal(ForceOn(box, off, 0).X, ForceOn(box, on, 0).X);

            Creature body = Chain(on);
            body.PlaceAt(new Vec3(0, 0.5, 0), QuatD.Identity);
            Vec3.Write(body.Spin, 3, new Vec3(0, 2, 0));
            Assert.Equal(ForceOn(body, off, 1).X, ForceOn(body, on, 1).X);
        }

        [Fact]
        public void AWaggingTailDrivesItsBodyForward()
        {
            double on = Travel(0.3, out double sideOn);
            double off = Travel(0, out double sideOff);

            _out.WriteLine($"20 s wagging: push on {on:0.###} m toward the head ({sideOn:0.###} m aside), off {off:0.###} m ({sideOff:0.###} m aside)");

            // The tail is at +x, so the push drives the body toward -x.
            Assert.True(on > 0.05, $"the push moved the body only {on} m");
            Assert.True(on > Math.Abs(off) + 0.05, $"the push added too little: {on} m against {off} m");
        }

        [Fact]
        public void AFasterStrokeSwimsFaster()
        {
            // Bench item 3: the push goes as the swing rate squared and points along the link, so
            // the forward thrust goes as the swing rate squared times the cosine of the link's angle
            // off the body's axis, and the speed as that thrust's square root. The command's
            // frequency is neither: the drive's power caps the swing, so a faster command can make
            // a smaller swing, and a slow one holds the link at its limits, where most of the push
            // is sideways. So the speeds are ordered by the forward thrust's measure.
            var runs = new System.Collections.Generic.List<(double rate, double speed)>();
            foreach (float hz in new[] { 0.25f, 0.5f, 1f, 2f })
            {
                double on = Travel(0.3, out _, hz, out double swing, out double forward);
                double off = Travel(0, out _, hz, out _, out _);
                _out.WriteLine($"{hz} Hz: rms swing rate {swing:0.###} rad/s, forward measure {forward:0.###} rad2/s2, {on / 20:0.####} m/s with the push, {off / 20:0.####} m/s without");
                Assert.True(on / 20 > off / 20 + 0.01, $"at {hz} Hz the push added under 1 cm/s");
                runs.Add((forward, on / 20));
            }

            // The strongest forward measure swims fastest and the weakest slowest. Between them the
            // order is not exact (2026-09-29: the 0.25 Hz stroke, which drag alone drives backward
            // at 1.4 cm/s, swam slower than the 1 Hz one with a larger measure).
            runs.Sort((a, b) => a.rate.CompareTo(b.rate));
            double slowest = double.MaxValue, fastest = 0;
            foreach (var r in runs) { slowest = Math.Min(slowest, r.speed); fastest = Math.Max(fastest, r.speed); }
            Assert.Equal(slowest, runs[0].speed);
            Assert.Equal(fastest, runs[runs.Count - 1].speed);
        }

        [Fact]
        public void AWagsSidewaysPushIsSmallAgainstItsForwardPush()
        {
            // Bench item 2's second half: over whole cycles of a symmetric wag the push's sideways
            // part, in the root's own frame, averages to under 5% of its forward part. A joint is
            // driven by a torque with nothing centring it, so a sine started from rest leaves the
            // stroke off-centre by an amount set by its phase (2026-09-29: the chain's own phase
            // stroked from 0 to 1.2 rad and pushed 77% as hard sideways as forward). So the phase
            // is swept, and the question is asked of the most centred stroke. At power 10 none is
            // centred: the link rails to one side and strokes between the axis and a limit, a mean
            // angle of +-0.58 to 0.67 rad at every phase, and pushes 63 to 77% as hard sideways as
            // forward. So on this drive a lone tail swims in a curve, and a straight course takes a
            // mirrored pair (OneSideFinTurnsItsBodyAndAMirroredPairDrivesItStraight). What is held:
            // the forward part is always positive, and the sideways part never exceeds it.
            double bestMean = double.MaxValue, bestShare = double.NaN;
            for (int k = 0; k < 16; k++)
            {
                float phase = (float)(2 * Math.PI * k / 16);
                (double meanAngle, double forward, double sideways) = Wag(phase);
                _out.WriteLine($"phase {phase:0.##}: mean hinge angle {meanAngle:0.###} rad, forward {forward:0.####} N, sideways {sideways:0.####} N ({100 * sideways / forward:0.#}%)");
                Assert.True(forward > 0, $"at phase {phase} the mean push points backward");
                if (Math.Abs(meanAngle) < Math.Abs(bestMean)) { bestMean = meanAngle; bestShare = sideways / forward; }
            }
            _out.WriteLine($"most centred stroke: mean angle {bestMean:0.###} rad, sideways {100 * bestShare:0.#}% of forward");
            Assert.True(bestShare < 1.0, $"the most centred stroke pushes {100 * bestShare:0.#}% as hard sideways as forward");
        }

        /// <summary>The wagging chain at one phase for 20 s: its mean hinge angle, and the push's mean forward and sideways parts in the root's frame.</summary>
        private static (double meanAngle, double forward, double sideways) Wag(float phase)
        {
            SolverConfig config = Water(0.3);
            Creature body = TestBodies.Build(TestBodies.Chain(links: 2, power: 10f, hz: 1f, phase: phase), config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
            var world = new DynamicsWorld(config);
            world.Add(body);

            double addedMass = config.AddedMassCoefficient * config.Density * body.Volume[1];
            Vec3 sum = Vec3.Zero;
            double qSum = 0;
            const int Steps = 2000;   // 20 s, twenty cycles at 1 Hz
            for (int step = 0; step < Steps; step++)
            {
                world.Step();
                Mat3 root = Mat3.Read(body.RotationMatrix, 0);
                Vec3 toOrigin = Mat3.Read(body.RotationMatrix, 9) * (-Vec3.Read(body.ChildAnchor, 3));
                double reach = toOrigin.Magnitude;
                Vec3 axis = toOrigin * (1.0 / reach);
                Vec3 swing = Vec3.Read(body.Spin, 3) - Vec3.Read(body.Spin, 0);
                Vec3 across = swing - axis * Vec3.Dot(swing, axis);
                Vec3 push = axis * (-0.3 * addedMass * reach * Vec3.Dot(across, across));
                sum += root.TransposedTimes(push);
                qSum += body.Q[0];
            }

            // The tail hangs at +x, so forward is the root's -x.
            return (qSum / Steps, -sum.X / Steps, Math.Sqrt(sum.Y * sum.Y + sum.Z * sum.Z) / Steps);
        }

        [Fact]
        public void ALimbSpunThroughAFullTurnGainsNoNetPush()
        {
            // Bench item 2's last line: a link swung through a whole turn about its hinge at a steady
            // rate is pushed toward its joint at every angle, and the pushes cancel over the turn.
            SolverConfig on = Water(0.3), off = Water(0);
            Creature body = Chain(on);
            const int N = 360;
            Vec3 sum = Vec3.Zero;
            double one = 0;
            for (int k = 0; k < N; k++)
            {
                double phi = 2 * Math.PI * k / N;
                double c = Math.Cos(phi), s = Math.Sin(phi);
                Mat3.Write(body.RotationMatrix, 9, new Mat3(c, 0, s, 0, 1, 0, -s, 0, c));
                Vec3.Write(body.Spin, 3, new Vec3(0, 2, 0));
                Vec3 push = ForceOn(body, on, 1) - ForceOn(body, off, 1);
                one = Math.Max(one, push.Magnitude);
                sum += push;
            }
            _out.WriteLine($"one angle's push {one:0.####} N; the sum over a turn of {N} angles {sum.Magnitude:0.###e0} N");
            Assert.True(one > 0.1, $"no push at all: {one} N");
            Assert.True(sum.Magnitude < 1e-9 * N * one, $"a full turn left a net push of {sum.Magnitude} N");
        }

        /// <summary>How far toward -x the root travels in 20 s of an oscillating hinge.</summary>
        private static double Travel(double push, out double aside, float hz = 1f) => Travel(push, out aside, hz, out _, out _);

        private static double Travel(double push, out double aside, float hz, out double rmsSwing, out double forward)
        {
            SolverConfig config = Water(push);
            Creature body = TestBodies.Build(TestBodies.Chain(links: 2, power: 10f, hz: hz), config);
            body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);

            var world = new DynamicsWorld(config);
            world.Add(body);
            double sum = 0, ahead = 0;
            for (int step = 0; step < 2000; step++)
            {
                world.Step();
                Vec3 swing = Vec3.Read(body.Spin, 3) - Vec3.Read(body.Spin, 0);
                double w2 = Vec3.Dot(swing, swing);
                sum += w2;

                // The link's axis from its joint, and the root's +x, both in the world.
                Vec3 axis = Mat3.Read(body.RotationMatrix, 9) * (-Vec3.Read(body.ChildAnchor, 3));
                Vec3 back = Mat3.Read(body.RotationMatrix, 0) * new Vec3(1, 0, 0);
                ahead += w2 * Vec3.Dot(axis, back) / axis.Magnitude;
            }
            rmsSwing = Math.Sqrt(sum / 2000);
            forward = ahead / 2000;

            aside = Math.Sqrt((body.Position[1] + 20) * (body.Position[1] + 20) + body.Position[2] * body.Position[2]);
            return -body.Position[0];
        }
    }
}
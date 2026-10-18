using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D161's drag on turning, in both of the forms the nursery check chooses between: the skin
    /// form's shear on each panel's velocity along its face, and the spin form's torque against a
    /// part's own spin.
    /// </summary>
    public sealed class TurningDragTests
    {
        private readonly ITestOutputHelper _out;

        public TurningDragTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        /// Water with the turning drag alone when <paramref name="pressure"/> is false, so that what
        /// a spinning box feels is the form's own law and nothing else.
        /// </summary>
        private static SolverConfig Water(TurningDragForm form, double coefficient, bool pressure = true)
        {
            SolverConfig config = TestBodies.Water(0.01);
            if (!pressure) config.DragCoefficient = 0;
            config.TurningDrag = form;
            config.SkinFrictionCoefficient = form == TurningDragForm.Skin ? coefficient : 0;
            config.SpinDragCoefficient = form == TurningDragForm.Spin ? coefficient : 0;
            return config;
        }

        private static (Vec3 torque, Vec3 force) Apply(Creature body, SolverConfig config, int link = 0)
        {
            Array.Clear(body.Fext, 0, body.Fext.Length);
            Fluid.Apply(body, config);
            return (Vec3.Read(body.Fext, 6 * link), Vec3.Read(body.Fext, 6 * link + 3));
        }

        private static Creature Box(SolverConfig config, double height = -20)
        {
            Creature body = TestBodies.Build(TestBodies.Box(new Float3(0.3f, 0.1f, 0.05f)), config);
            body.PlaceAt(new Vec3(0, height, 0), QuatD.Identity);
            return body;
        }

        /// <summary>
        /// The spin form's moment about each of the link's axes, taken from the panel arrays here
        /// rather than from <see cref="Creature.SpinMoment"/>.
        /// </summary>
        private static Vec3 MomentFromPanels(Creature body, int link)
        {
            double mx = 0, my = 0, mz = 0;
            for (int p = body.PanelStart[link]; p < body.PanelStart[link + 1]; p++)
            {
                Vec3 c = Vec3.Read(body.PanelCentre, 3 * p);
                double a = body.PanelArea[p];
                mx += a * Math.Pow(c.Y * c.Y + c.Z * c.Z, 1.5);
                my += a * Math.Pow(c.Z * c.Z + c.X * c.X, 1.5);
                mz += a * Math.Pow(c.X * c.X + c.Y * c.Y, 1.5);
            }
            return new Vec3(mx, my, mz);
        }

        /// <summary>
        /// The skin form's moment for a spin about one of the link's own axes: each panel's
        /// velocity along its face is the spin's velocity less its normal part, and its torque
        /// about the axis is the shear times its lever.
        /// </summary>
        private static double SkinMomentFromPanels(Creature body, int link, Vec3 axis)
        {
            double sum = 0;
            for (int p = body.PanelStart[link]; p < body.PanelStart[link + 1]; p++)
            {
                Vec3 c = Vec3.Read(body.PanelCentre, 3 * p);
                Vec3 n = Vec3.Read(body.PanelNormal, 3 * p);
                Vec3 v = Vec3.Cross(axis, c);
                Vec3 along = v - n * Vec3.Dot(v, n);
                double slide = along.Magnitude;
                Vec3 torque = Vec3.Cross(c, along * slide);
                sum += body.PanelArea[p] * Vec3.Dot(torque, axis);
            }
            return sum;
        }

        [Fact]
        public void AtNoneTheCoefficientsAreNotReadAndAboveTheWaterNeitherFormActs()
        {
            SolverConfig off = TestBodies.Water(0.01);
            SolverConfig skinUnset = TestBodies.Water(0.01);
            skinUnset.SkinFrictionCoefficient = 0.5;
            skinUnset.SpinDragCoefficient = 0.5;

            Creature body = Box(off);
            Vec3.Write(body.Spin, 0, new Vec3(1, -2, 3));
            Vec3.Write(body.Velocity, 0, new Vec3(0.2, 0.1, -0.3));
            var a = Apply(body, off);
            var b = Apply(body, skinUnset);
            Assert.Equal(a.torque, b.torque);
            Assert.Equal(a.force, b.force);

            // Out of the water: the pressure drag still acts there, the turning drag does not.
            foreach (TurningDragForm form in new[] { TurningDragForm.Skin, TurningDragForm.Spin })
            {
                Creature high = Box(off, height: 0.5);
                Vec3.Write(high.Spin, 0, new Vec3(1, -2, 3));
                Vec3.Write(high.Velocity, 0, new Vec3(0.2, 0.1, -0.3));
                var without = Apply(high, off);
                var with = Apply(high, Water(form, 0.5));
                Assert.Equal(without.torque, with.torque);
                Assert.Equal(without.force, with.force);
            }
        }

        [Fact]
        public void TheSpinFormBrakesEachAxisByItsMomentAndPushesNothing()
        {
            const double Cs = 0.4;
            SolverConfig on = Water(TurningDragForm.Spin, Cs, pressure: false);
            Creature body = Box(on);

            Vec3 moment = MomentFromPanels(body, 0);
            Assert.Equal(moment.X, body.SpinMoment[0], 12);
            Assert.Equal(moment.Y, body.SpinMoment[1], 12);
            Assert.Equal(moment.Z, body.SpinMoment[2], 12);

            Vec3 spin = new Vec3(1, -2, 3);
            Vec3.Write(body.Spin, 0, spin);
            Vec3.Write(body.Velocity, 0, new Vec3(0.2, 0.1, -0.3));
            var (torque, force) = Apply(body, on);

            double k = 0.5 * 1000 * Cs * spin.Magnitude;
            Vec3 expected = new Vec3(-k * moment.X * spin.X, -k * moment.Y * spin.Y, -k * moment.Z * spin.Z);
            _out.WriteLine($"moment ({moment.X:0.#####e0}, {moment.Y:0.#####e0}, {moment.Z:0.#####e0}) m5; torque ({torque.X:0.#####}, {torque.Y:0.#####}, {torque.Z:0.#####}) N m");

            Assert.Equal(expected.X, torque.X, 9);
            Assert.Equal(expected.Y, torque.Y, 9);
            Assert.Equal(expected.Z, torque.Z, 9);
            Assert.Equal(0.0, force.Magnitude, 12);
        }

        [Fact]
        public void TheSkinFormBrakesASpinAsItsPanelsSay()
        {
            const double Cf = 0.4;
            SolverConfig on = Water(TurningDragForm.Skin, Cf, pressure: false);
            Creature body = Box(on);

            foreach (Vec3 axis in new[] { new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1) })
            {
                const double Rate = 2.0;
                Vec3.Write(body.Spin, 0, axis * Rate);
                var (torque, force) = Apply(body, on);

                double expected = 0.5 * 1000 * Cf * Rate * Rate * SkinMomentFromPanels(body, 0, axis);
                double about = Vec3.Dot(torque, axis);
                _out.WriteLine($"axis ({axis.X}, {axis.Y}, {axis.Z}): torque {about:0.######} N m, expected {-expected:0.######}");

                Assert.Equal(-expected, about, 9);
                Assert.True(expected > 0);
                Assert.Equal(0.0, (torque - axis * about).Magnitude, 9);

                // The box is symmetric, so a pure spin slides opposite panels oppositely.
                Assert.Equal(0.0, force.Magnitude, 9);
            }
        }

        [Fact]
        public void TheSkinFormAlsoBrakesASlideAlongAFace()
        {
            SolverConfig on = Water(TurningDragForm.Skin, 0.4, pressure: false);
            Creature body = Box(on);
            Vec3.Write(body.Velocity, 0, new Vec3(0.5, 0, 0));
            var (_, force) = Apply(body, on);
            Assert.True(force.X < 0, $"a slide along x met {force.X} N along x");

            SolverConfig spin = Water(TurningDragForm.Spin, 0.4, pressure: false);
            var (_, none) = Apply(body, spin);
            Assert.Equal(0.0, none.Magnitude, 12);
        }

        /// <summary>
        /// A box spun about one of its own axes and left alone, with the turning drag the only
        /// thing in the water: I·dω/dt = −½ρ·C·M·ω², so ω(t) = ω0 / (1 + ½ρ·C·M·ω0·t / I).
        /// </summary>
        [Theory]
        [InlineData(TurningDragForm.Spin, 0)]
        [InlineData(TurningDragForm.Spin, 1)]
        [InlineData(TurningDragForm.Skin, 0)]
        [InlineData(TurningDragForm.Skin, 2)]
        public void AFreeSpinDecaysAtTheRateTheFormPredicts(TurningDragForm form, int axisIndex)
        {
            const double C = 0.05, W0 = 4.0, Seconds = 3.0;
            SolverConfig config = Water(form, C, pressure: false);
            Creature body = Box(config);

            Vec3 axis = axisIndex == 0 ? new Vec3(1, 0, 0) : axisIndex == 1 ? new Vec3(0, 1, 0) : new Vec3(0, 0, 1);
            double moment = form == TurningDragForm.Spin
                ? Vec3.Dot(MomentFromPanels(body, 0), axis)
                : SkinMomentFromPanels(body, 0, axis);
            double inertia = Vec3.Dot(Vec3.Read(body.InertiaLocal, 0), axis);

            Vec3.Write(body.Spin, 0, axis * W0);
            var world = new DynamicsWorld(config);
            world.Add(body);
            int steps = (int)Math.Round(Seconds / config.StepSeconds);
            for (int s = 0; s < steps; s++) world.Step();

            double rate = Vec3.Dot(Vec3.Read(body.Spin, 0), axis);
            double predicted = W0 / (1 + 0.5 * 1000 * C * moment * W0 * Seconds / inertia);
            _out.WriteLine($"{form} about axis {axisIndex}: {W0} rad/s fell to {rate:0.#####} in {Seconds} s, predicted {predicted:0.#####} (inertia {inertia:0.####e0}, moment {moment:0.####e0})");

            Assert.True(predicted < 0.9 * W0, "the drag is too weak here to test anything");
            Assert.InRange(rate, predicted * 0.99, predicted * 1.01);
        }

        /// <summary>
        /// The energy audit closes with the turning drag on, as the ledger tests close it without:
        /// the work the drive put in is the kinetic energy gained plus what the water took.
        /// </summary>
        [Theory]
        [InlineData(TurningDragForm.Skin)]
        [InlineData(TurningDragForm.Spin)]
        public void TheEnergyAuditClosesOnASwimmer(TurningDragForm form)
        {
            (double signed, double kinetic, double dissipated, double passive) Swim(TurningDragForm f)
            {
                SolverConfig config = Water(f, 0.05);
                config.AddedMassCoefficient = 0.5;   // LedgerTests' own water
                Genome genome = TestBodies.Chain(
                    2, JointType.Hinge, power: 15f, hz: 1.2f,
                    rootHalf: new Float3(0.15f, 0.15f, 0.15f),
                    linkHalf: new Float3(0.3f, 0.02f, 0.3f),
                    limit: 3.0f);
                Creature body = TestBodies.Build(genome, config);
                body.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);
                var world = new DynamicsWorld(config);
                world.Add(body);
                double before = body.KineticEnergy();
                for (int s = 0; s < 6000; s++) world.Step();
                return (body.SignedWorkJoules, body.KineticEnergy() - before, body.DissipatedJoules, body.PassiveJointWorkJoules);
            }

            var on = Swim(form);
            var off = Swim(TurningDragForm.None);
            double residual = on.signed - on.kinetic - on.dissipated + on.passive;
            double offResidual = off.signed - off.kinetic - off.dissipated + off.passive;
            _out.WriteLine($"{form}: drive {on.signed:0.####} J, kinetic {on.kinetic:0.####} J, drag {on.dissipated:0.####} J (off {off.dissipated:0.####} J), passive {on.passive:0.####} J, residual {residual:0.###e0} J (off {offResidual:0.###e0} J)");

            Assert.True(on.dissipated > 0, $"the water added {-on.dissipated} J");
            Assert.True(Math.Abs(residual) / Math.Abs(on.signed) < 1e-3,
                $"the audit is off by {residual} J of {on.signed} J");

            // What is left is the integrator's, which the water without the turning drag leaves
            // too (-0.23 J both ways on 2026-10-12); the new term adds next to nothing to it.
            Assert.True(on.dissipated > off.dissipated, "the turning drag took nothing");
            Assert.True(Math.Abs(residual - offResidual) < 0.01 * (on.dissipated - off.dissipated),
                $"the turning drag moved the residual from {offResidual} J to {residual} J");
        }

        [Theory]
        [InlineData(TurningDragForm.Skin)]
        [InlineData(TurningDragForm.Spin)]
        public void TheThreadCountDoesNotChangeTheTrajectory(TurningDragForm form)
        {
            ulong Run(int threads)
            {
                SolverConfig config = Water(form, 0.05);
                config.AddedMassCoefficient = 0.5;
                config.TissueExcessDensity = 0.02;
                config.WorldDepthMetres = 15;
                config.CreatureContact = true;
                config.FloorIsSolid = true;
                config.LimbPushCoefficient = 0.3;
                var world = new DynamicsWorld(config) { Threads = threads };
                var rng = new Rng(20261012);
                for (int i = 0; i < 60; i++)
                {
                    JointType joint = (i % 3) == 0 ? JointType.Hinge
                        : (i % 3) == 1 ? JointType.Universal : JointType.Spherical;
                    Creature body = TestBodies.Build(
                        TestBodies.Chain(2 + i % 3, joint, power: 8f + i % 5, hz: 0.5f + 0.1f * (i % 7)), config, i);
                    body.PlaceAt(
                        new Vec3((rng.NextFloat() - 0.5) * 3.0, -3.0 - rng.NextFloat() * 2.0, (rng.NextFloat() - 0.5) * 3.0),
                        QuatD.FromAxisAngle(
                            new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5),
                            rng.NextFloat() * Math.PI));
                    world.Add(body);
                }
                for (int s = 0; s < 1000; s++) world.Step();
                return world.Digest();
            }

            ulong one = Run(1), sixteen = Run(16);
            _out.WriteLine($"{form}: 1 thread {one:x16}, 16 threads {sixteen:x16}");
            Assert.Equal(one, sixteen);
        }

        /// <summary>
        /// The coordinator's steering check (2026-10-12): under D135 one limb on a hinge with a
        /// vertical axis, its bend held at the top of its range and its power full, turns its body;
        /// centred, it swims straight. Reported with the turning drag off, skin and spin at 0.05.
        /// A sanity check and not a size, like the wagging tail.
        /// </summary>
        [Fact]
        public void ABentFinOnAVerticalHingeSteersItsBody()
        {
            (double yaw, double tilt, double travel, double axisY) Steer(TurningDragForm form, float bend)
            {
                SolverConfig config = Water(form, 0.05);
                config.AddedMassCoefficient = 0.5;
                config.LimbPushCoefficient = 0.3;
                config.PowerPushRadiansPerSecond = 6.0;
                config.PoseHertz = 2.0;

                // The chain's hinge bends about its frame's z, which is horizontal; turned a
                // quarter about the limb's own length, the hinge stands vertical and the limb is
                // an upright fin.
                Genome g = TestBodies.Chain(links: 2, power: 10f);
                g.Nodes[0].Edges[0].Orientation = Quat.FromAxisAngle(new Float3(1f, 0f, 0f), (float)(Math.PI / 2));
                MorphNode fin = g.Nodes[1];
                fin.Drive = new DriveGenes(0f, 0.05f, 0.05f, 0.05f);
                fin.Neurons = new[]
                {
                    new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 1f, Inputs = Array.Empty<NeuronInput>() },
                    new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = bend, Inputs = Array.Empty<NeuronInput>() },
                };

                Creature body = TestBodies.Build(g, config);
                body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
                Vec3 hinge = Mat3.Read(body.RotationMatrix, 9) * QuatD.Read(body.JointFrame, 4).Rotate(new Vec3(1, 0, 0));

                var world = new DynamicsWorld(config);
                world.Add(body);

                // The heading is the root's +x in the horizontal plane, unwrapped step by step.
                double Heading() { Vec3 x = Mat3.Read(body.RotationMatrix, 0) * new Vec3(1, 0, 0); return Math.Atan2(-x.Z, x.X); }
                double last = Heading(), turned = 0;
                for (int s = 0; s < 1000; s++)
                {
                    world.Step();
                    double now = Heading();
                    double step = now - last;
                    if (step > Math.PI) step -= 2 * Math.PI;
                    if (step < -Math.PI) step += 2 * Math.PI;
                    turned += step;
                    last = now;
                }

                Vec3 up = Mat3.Read(body.RotationMatrix, 0) * new Vec3(0, 1, 0);
                double moved = Math.Sqrt(body.Position[0] * body.Position[0] + body.Position[2] * body.Position[2]);
                return (turned, Math.Acos(Math.Max(-1, Math.Min(1, up.Y))), moved, hinge.Y);
            }

            double straightOff = 0, bentOff = 0;
            foreach (TurningDragForm form in new[] { TurningDragForm.None, TurningDragForm.Skin, TurningDragForm.Spin })
            {
                var straight = Steer(form, 0f);
                var bent = Steer(form, 1f);
                _out.WriteLine(
                    $"{form}: hinge axis y {bent.axisY:0.###}; 10 s at full power, bent: turned {bent.yaw * 180 / Math.PI:0.#} deg, " +
                    $"tilted {bent.tilt * 180 / Math.PI:0.#} deg, {bent.travel:0.###} m from the start; " +
                    $"centred: turned {straight.yaw * 180 / Math.PI:0.#} deg, tilted {straight.tilt * 180 / Math.PI:0.#} deg, {straight.travel:0.###} m");

                Assert.True(Math.Abs(bent.axisY) > 0.999, $"the hinge axis is not vertical: y {bent.axisY}");
                Assert.True(Math.Abs(straight.yaw) < 0.05, $"{form}: a centred fin turned its body {straight.yaw} rad");
                Assert.True(Math.Abs(bent.yaw) > Math.Abs(straight.yaw) + 0.2, $"{form}: a bent fin turned its body only {bent.yaw} rad");
                if (form == TurningDragForm.None) { straightOff = straight.yaw; bentOff = bent.yaw; }
            }

            Assert.True(Math.Abs(bentOff) > 0.5, $"with no turning drag a bent fin turned its body only {bentOff} rad");
        }

        /// <summary>
        /// The coordinator's second steering check (2026-10-12): one limb on a universal joint,
        /// each of its two bends held at the top of its range in turn and then both, power full,
        /// in still water; does one limb on one joint steer in three dimensions under D135? The
        /// body's turn is the root's spin integrated in its own frame: yaw about its y, pitch about
        /// its z, roll about its x. Reported with the turning drag off, skin and spin at 0.05.
        /// </summary>
        [Fact]
        public void AUniversalLimbsTwoBendsSteerItsBody()
        {
            (Vec3 turn, Vec3 axis0, Vec3 axis1, double q0, double q1) Steer(TurningDragForm form, float bend0, float bend1)
            {
                SolverConfig config = Water(form, 0.05);
                config.AddedMassCoefficient = 0.5;
                config.LimbPushCoefficient = 0.3;
                config.PowerPushRadiansPerSecond = 6.0;
                config.PoseHertz = 2.0;

                Genome g = TestBodies.Chain(links: 2, joint: JointType.Universal, power: 10f);
                MorphNode limb = g.Nodes[1];
                limb.Drive = new DriveGenes(0f, 0.05f, 0.05f, 0.05f);
                NeuronDef Held(float bias) => new NeuronDef
                {
                    Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = bias, Inputs = Array.Empty<NeuronInput>(),
                };
                limb.Neurons = new[] { Held(1f), Held(1f), Held(bend0), Held(bend1) };

                Creature body = TestBodies.Build(g, config);
                body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
                var world = new DynamicsWorld(config);
                world.Add(body);

                Vec3 turned = Vec3.Zero;
                for (int s = 0; s < 1000; s++)
                {
                    world.Step();
                    Mat3 root = Mat3.Read(body.RotationMatrix, 0);
                    turned += root.TransposedTimes(Vec3.Read(body.Spin, 0)) * config.StepSeconds;
                }

                Mat3 link = Mat3.Read(body.RotationMatrix, 9);
                QuatD frame = QuatD.Read(body.JointFrame, 4);
                Mat3 back = Mat3.Read(body.RotationMatrix, 0);
                return (turned,
                        back.TransposedTimes(link * frame.Rotate(new Vec3(1, 0, 0))),
                        back.TransposedTimes(link * frame.Rotate(new Vec3(0, 1, 0))),
                        body.Q[body.DofStart[1]], body.Q[body.DofStart[1] + 1]);
            }

            string Deg(double r) => (r * 180 / Math.PI).ToString("0.#");
            foreach (TurningDragForm form in new[] { TurningDragForm.None, TurningDragForm.Skin, TurningDragForm.Spin })
            {
                foreach ((string name, float b0, float b1) in new[] { ("centred", 0f, 0f), ("dof 0 held", 1f, 0f), ("dof 1 held", 0f, 1f), ("both held", 1f, 1f) })
                {
                    var r = Steer(form, b0, b1);
                    double yaw = Math.Abs(r.turn.Y), pitch = Math.Abs(r.turn.Z), roll = Math.Abs(r.turn.X);
                    _out.WriteLine(
                        $"{form}, {name}: q ({r.q0:0.##}, {r.q1:0.##}) rad; dof axes in the root's frame " +
                        $"({r.axis0.X:0.##}, {r.axis0.Y:0.##}, {r.axis0.Z:0.##}) and ({r.axis1.X:0.##}, {r.axis1.Y:0.##}, {r.axis1.Z:0.##}); " +
                        $"10 s turned yaw {Deg(r.turn.Y)} deg, pitch {Deg(r.turn.Z)} deg, roll {Deg(r.turn.X)} deg");

                    // The universal's first degree of freedom twists about the limb and its second
                    // bends about the frame's y. Centred, nothing turns. The twist alone pushes
                    // nothing and only rolls the body against the limb. The bend alone yaws. Both
                    // together tilt the bend's axis by the twist, toward a pitch.
                    if (b0 == 0f && b1 == 0f) Assert.True(yaw + pitch + roll < 0.05, $"{form}: a centred limb turned its body");
                    if (b0 == 1f && b1 == 0f) Assert.True(yaw + pitch < 0.05 && roll > 0.2, $"{form}: the twist alone did more than roll");
                    if (b0 == 0f && b1 == 1f) Assert.True(yaw > 1.0 && pitch + roll < 0.05, $"{form}: the bend alone did not yaw");
                    if (b0 == 1f && b1 == 1f) Assert.True(pitch > 1.0 && pitch > yaw, $"{form}: both held did not pitch");
                }
            }
        }

        /// <summary>
        /// A sanity check and not a size: the wagging tail still drives its body with the drag on.
        /// The nursery check sets the size (D161).
        /// </summary>
        [Theory]
        [InlineData(TurningDragForm.Skin)]
        [InlineData(TurningDragForm.Spin)]
        public void AWaggingTailStillDrivesItsBody(TurningDragForm form)
        {
            double Travel(TurningDragForm f)
            {
                SolverConfig config = Water(f, 0.05);
                config.AddedMassCoefficient = 0.5;
                config.LimbPushCoefficient = 0.3;
                Creature body = TestBodies.Build(TestBodies.Chain(links: 2, power: 10f, hz: 1f), config);
                body.PlaceAt(new Vec3(0, -20, 0), QuatD.Identity);
                var world = new DynamicsWorld(config);
                world.Add(body);
                for (int s = 0; s < 2000; s++) world.Step();
                return -body.Position[0];
            }

            double on = Travel(form), off = Travel(TurningDragForm.None);
            _out.WriteLine($"{form}: 20 s of wagging moved the body {on:0.###} m with the turning drag, {off:0.###} m without");
            Assert.True(on > 0.05, $"with the turning drag the body moved only {on} m");
        }
    }
}

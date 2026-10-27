using System;
using System.Reflection;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D231: a stroke pushes its whole assembly (<c>SolverConfig.StrokePushesAssembly</c>,
    /// <c>Creature.StrokeAddedMass</c> and <c>StrokeFirstMoment</c>, <see cref="Fluid.AssemblyLeverOf"/>).
    /// </summary>
    /// <remarks>
    /// In every body here the hinge's stroke runs along local Y and the width the water sees runs along
    /// local Z, so a blade is "flat across its stroke" when it is thin in Y and wide in Z, as D229's tests
    /// have a paddle.
    /// </remarks>
    public sealed class StrokeAssemblyTests
    {
        private readonly ITestOutputHelper _out;

        public StrokeAssemblyTests(ITestOutputHelper output) => _out = output;

        private const double Rho = 1000.0;
        private const double Omega = 6.0;
        private const double Push = 0.3;
        private const double QuarterPi = Math.PI / 4.0;

        private static readonly MethodInfo JointTorques =
            typeof(DynamicsWorld).GetMethod("JointTorques", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>Equal to a part in a million: the half-extents are floats.</summary>
        private static void Near(double expected, double actual, double relative = 1e-6) =>
            Assert.True(Math.Abs(actual - expected) <= relative * Math.Abs(expected), $"expected {expected}, got {actual}");

        private static SolverConfig Water(bool assembly = true, bool shaped = true, double push = Push, double omega = Omega)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.AddedMassCoefficient = 0.5;
            config.LimbPushCoefficient = push;
            config.PowerPushRadiansPerSecond = omega;
            config.PoseHertz = 2.0;
            config.ShapedAddedMass = shaped;
            config.StrokePushesAssembly = assembly;
            return config;
        }

        /// <summary>A structural root and one hinged box link, as D229's tests have it.</summary>
        private static Genome Limb(Float3 linkHalf) => TestBodies.Chain(links: 2, power: 10f, linkHalf: linkHalf);

        /// <summary>A box welded to <paramref name="parent"/>, joined at the given anchors; returns its node.</summary>
        private static int Weld(Genome g, int parent, Float3 half, Float3 parentAnchor, Float3 childAnchor)
        {
            int index = g.Nodes.Count;
            g.Nodes.Add(new MorphNode
            {
                Dimensions = half,
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            g.Nodes[parent].Edges.Add(new MorphEdge
            {
                Child = index,
                ParentAnchor = parentAnchor,
                ChildAnchor = childAnchor,
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            return index;
        }

        /// <summary>A flat blade welded at the tip of node 1, in line with it.</summary>
        private static Genome Bladed(Float3 link, Float3 blade)
        {
            Genome g = Limb(link);
            Weld(g, 1, blade, new Float3(1f, 0f, 0f), new Float3(-1f, 0f, 0f));
            return g;
        }

        private static Creature Build(Genome g, SolverConfig config)
        {
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

        private static readonly Float3 LinkHalf = new Float3(0.1f, 0.05f, 0.04f);
        private static readonly Float3 BladeHalf = new Float3(0.15f, 0.01f, 0.12f);

        /// <summary>The hand formulas for LinkHalf with BladeHalf welded in line beyond it.</summary>
        private static void InLineByHand(out double mass, out double moment)
        {
            double a1 = 0.1, c1 = 0.04, a2 = 0.15, c2 = 0.12;
            double linkPiece = Rho * QuarterPi * (2 * c1) * (2 * c1) * (2 * a1);
            double bladePiece = Rho * QuarterPi * (2 * c2) * (2 * c2) * (2 * a2);
            mass = linkPiece + bladePiece;
            moment = linkPiece * a1 + bladePiece * (2 * a1 + a2);
        }

        // ---- the assembly and its constants

        [Fact]
        public void AWeldedBladeBeyondTheLinkIsItsAssemblyAndTheConstantsAreTheHandFormulas()
        {
            Creature body = Build(Bladed(LinkHalf, BladeHalf), Water());

            Assert.Equal(3, body.Links);
            Assert.Equal(2, body.StrokeAssemblyParts[1]);
            Assert.Equal(0, body.StrokeAssemblyParts[0]);   // the root has no joint to stroke about
            Assert.Equal(0, body.StrokeAssemblyParts[2]);   // a weld anchors nothing

            InLineByHand(out double mass, out double moment);
            _out.WriteLine($"M {body.StrokeAddedMass[0]:0.########} kg (hand {mass:0.########}), I1 {body.StrokeFirstMoment[0]:0.########} kg m (hand {moment:0.########})");
            Near(mass, body.StrokeAddedMass[0]);
            Near(moment, body.StrokeFirstMoment[0]);
            Assert.True(Math.Abs(body.StrokeAlong[0]) < 1e-12);
        }

        [Fact]
        public void TwoBladesSideBySideAcrossTheStrokeTakeTheEnvelopeOfTheirWidthsAndTheGap()
        {
            // The link's width across the stroke is 0.06; two blades 0.1 wide each stand either side of its
            // tip with a gap of 0.06 between them, so beyond the link the chord is 0.1 + 0.1 + 0.06.
            var link = new Float3(0.1f, 0.05f, 0.03f);
            var blade = new Float3(0.2f, 0.01f, 0.05f);
            Genome g = Limb(link);
            Weld(g, 1, blade, new Float3(1f, 0f, 1f), new Float3(-1f, 0f, -1f));
            Weld(g, 1, blade, new Float3(1f, 0f, -1f), new Float3(-1f, 0f, 1f));

            Creature body = Build(g, Water());
            Assert.Equal(3, body.StrokeAssemblyParts[1]);

            double chord = 0.1 + 0.1 + 0.06;
            double expectedMass = Rho * QuarterPi * (0.06 * 0.06 * 0.2 + chord * chord * 0.4);
            double expectedMoment = Rho * QuarterPi * (0.06 * 0.06 * 0.2 * 0.1 + chord * chord * 0.4 * 0.4);
            _out.WriteLine($"M {body.StrokeAddedMass[0]:0.########} kg (hand {expectedMass:0.########}), I1 {body.StrokeFirstMoment[0]:0.########} kg m (hand {expectedMoment:0.########})");
            Near(expectedMass, body.StrokeAddedMass[0], 1e-5);
            Near(expectedMoment, body.StrokeFirstMoment[0], 1e-5);
        }

        [Fact]
        public void OneBoxAloneThroughTheStripIntegralIsD229sPlateFormulaAndItsFirstMomentIsTheMassTimesItsReach()
        {
            // An oblique limb: the axis along the diagonal of local X and Y, moving along local Z, as D229's
            // own oblique test has it.
            var h = new Float3(0.1f, 0.05f, 0.03f);
            double s = 1.0 / Math.Sqrt(2.0);
            var axis = new Vec3(s, s, 0);
            Vec3 stroke = new Vec3(0, 0, 1);
            Vec3 side = Vec3.Cross(axis, stroke);
            side = side * (1.0 / side.Magnitude);
            double reach = 0.3;

            double alongHalf = Creature.SupportHalfWidth(ShapeIds.Box, h, QuatD.Identity, axis);
            double sideHalf = Creature.SupportHalfWidth(ShapeIds.Box, h, QuatD.Identity, side);

            Creature.StripIntegrals(
                new[] { reach - alongHalf }, new[] { reach + alongHalf },
                new[] { -sideHalf }, new[] { sideHalf },
                Rho, out double mass, out double moment);

            double plate = Fluid.AddedMassOf(ShapeIds.Box, h, Mat3.Identity, axis, stroke, Rho, 0.5, 1.0);
            _out.WriteLine($"strip M {mass:0.##########}, D229 {plate:0.##########}; I1 {moment:0.##########}, M x reach {plate * reach:0.##########}");

            Near(plate, mass, 1e-12);
            Near(plate * reach, moment, 1e-12);
        }

        [Fact]
        public void ACapsuleAndASphereAreProjectedByTheirSupportFunctions()
        {
            var h = new Float3(0.05f, 0.3f, 0.03f);
            double r = CapsuleShape.Radius(h);
            double halfSpan = CapsuleShape.HalfSpan(h);

            // A capsule's long axis is local Y: along it the half-width is the span and the cap, across it the radius.
            Near(halfSpan + r, Creature.SupportHalfWidth(ShapeIds.Capsule, h, QuatD.Identity, Vec3.UnitY), 1e-12);
            Near(r, Creature.SupportHalfWidth(ShapeIds.Capsule, h, QuatD.Identity, Vec3.UnitX), 1e-12);
            Near(SphereShape.Radius(h), Creature.SupportHalfWidth(ShapeIds.Sphere, h, QuatD.Identity, new Vec3(0.6, 0, 0.8)), 1e-12);
        }

        // ---- the push

        [Fact]
        public void TheBladeGetsNoPushOfItsOwnAndTheWholeAssemblysPushLandsOnTheJointedLink()
        {
            SolverConfig config = Water();
            SolverConfig none = Water(push: 0);

            Creature body = Build(Bladed(LinkHalf, BladeHalf), config);
            Creature still = Build(Bladed(LinkHalf, BladeHalf), none);
            body.Drive.Stroke[0] = 0.5;
            still.Drive.Stroke[0] = 0.5;

            Vec3 link = ForceOn(body, config, 1) - ForceOn(still, none, 1);
            Vec3 blade = ForceOn(body, config, 2) - ForceOn(still, none, 2);
            Vec3 root = ForceOn(body, config, 0) - ForceOn(still, none, 0);

            Assert.Equal(0.0, blade.Magnitude);
            Assert.Equal(0.0, root.Magnitude);

            // C_T times q times I1, with q = (s Omega)^2 for a hinge across the limb, along -axis (+X is the axis).
            InLineByHand(out _, out double moment);
            double expected = Push * (0.5 * Omega) * (0.5 * Omega) * moment;
            _out.WriteLine($"push on the link ({link.X:0.#####}, {link.Y:0.#####}, {link.Z:0.#####}) N, expected {-expected:0.#####} N along x");
            Near(-expected, link.X);
            Assert.True(Math.Abs(link.Y) < 1e-9 && Math.Abs(link.Z) < 1e-9);

            // The push is the plate formula's of D229 for the link alone, now much larger.
            Creature lone = Build(Limb(LinkHalf), config);
            lone.Drive.Stroke[0] = 0.5;
            Creature loneStill = Build(Limb(LinkHalf), none);
            loneStill.Drive.Stroke[0] = 0.5;
            Vec3 alone = ForceOn(lone, config, 1) - ForceOn(loneStill, none, 1);
            Assert.True(link.X < alone.X, "the blade added no push");
        }

        [Fact]
        public void TheBilledWorkIsThePushTimesTheMovedWatersLeverTimesTheSwingSpeedTimesTheStep()
        {
            SolverConfig config = Water();
            Creature body = Build(Bladed(LinkHalf, BladeHalf), config);
            body.Drive.Stroke[0] = 0.5;

            double before = body.MechanicalWorkJoules;
            Vec3 pushed = ForceOn(body, config, 1);
            double billed = body.MechanicalWorkJoules - before;

            SolverConfig none = Water(push: 0);
            Creature still = Build(Bladed(LinkHalf, BladeHalf), none);
            still.Drive.Stroke[0] = 0.5;
            double push = -(pushed - ForceOn(still, none, 1)).X;

            InLineByHand(out double mass, out double moment);
            double lever = moment / mass;
            double speed = 0.5 * Omega;
            double expected = push * lever * speed * config.StepSeconds;
            _out.WriteLine($"billed {billed:0.##########} J, push {push:0.#####} N x lever {lever:0.#####} m x {speed} m/s... x {config.StepSeconds} s = {expected:0.##########} J");
            Near(expected, billed);

            // And the lever is not the reach: the blade moves water far beyond the link's centre.
            Assert.True(lever > 0.1 + 0.05);
        }

        [Fact]
        public void ALinkWithNothingWeldedBeyondItTakesD229sLinesAndBillsD229sWork()
        {
            SolverConfig assembly = Water(assembly: true);
            SolverConfig plain = Water(assembly: false);

            Creature a = Build(Limb(LinkHalf), assembly);
            Creature b = Build(Limb(LinkHalf), plain);
            Assert.Equal(1, a.StrokeAssemblyParts[1]);   // the link alone: one part, so D229's own lines
            a.Drive.Stroke[0] = 0.5;
            b.Drive.Stroke[0] = 0.5;

            Vec3 fa = ForceOn(a, assembly, 1);
            Vec3 fb = ForceOn(b, plain, 1);
            Assert.Equal(BitConverter.DoubleToInt64Bits(fb.X), BitConverter.DoubleToInt64Bits(fa.X));
            Assert.Equal(BitConverter.DoubleToInt64Bits(fb.Y), BitConverter.DoubleToInt64Bits(fa.Y));
            Assert.Equal(BitConverter.DoubleToInt64Bits(fb.Z), BitConverter.DoubleToInt64Bits(fa.Z));
            Assert.Equal(BitConverter.DoubleToInt64Bits(b.MechanicalWorkJoules), BitConverter.DoubleToInt64Bits(a.MechanicalWorkJoules));
        }

        [Fact]
        public void TheLeverHookKeepsTheReachForAPartAndTheSiblingTakesTheMovedWater()
        {
            Assert.Equal(0.2371, Fluid.LeverOf(0.2371, Vec3.UnitX, Vec3.UnitY));
            Assert.Equal(3.0, Fluid.AssemblyLeverOf(6.0, 2.0, 0.2));
            Assert.Equal(0.2, Fluid.AssemblyLeverOf(0.0, 0.0, 0.2));
        }

        // ---- no welds is D229

        /// <summary>
        /// A hand-made stroke in still water, the fin bench's: |sin| at 1 Hz peaking at 0.8, stepped by hand.
        /// </summary>
        private static Creature Swim(SolverConfig solver, Genome genome, double seconds)
        {
            Creature body = Build(genome, solver);
            var world = new DynamicsWorld(solver);
            world.Add(body);
            var jointArgs = new object[] { body };

            double dt = solver.StepSeconds;
            int steps = (int)Math.Round(seconds / dt);
            for (int step = 0; step < steps; step++)
            {
                body.Drive.Stroke[0] = 0.8 * Math.Abs(Math.Sin(2 * Math.PI * 1.0 * step * dt));

                Array.Clear(body.Fext, 0, body.Fext.Length);
                Array.Clear(body.Tau, 0, body.Tau.Length);
                Fluid.Apply(body, solver);
                JointTorques.Invoke(world, jointArgs);
                Aba.Solve(body);
                Aba.Integrate(body, dt);
                Kinematics.Poses(body);
                Kinematics.Velocities(body);
                body.Settle(dt);
                Assert.True(body.IsFinite(), "the body went non-finite at step " + step);
            }

            return body;
        }

        private static void SameBits(double[] expected, double[] actual, string what)
        {
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(
                    BitConverter.DoubleToInt64Bits(expected[i]) == BitConverter.DoubleToInt64Bits(actual[i]),
                    $"{what}[{i}] differs: {expected[i]:R} against {actual[i]:R}");
            }
        }

        [Fact]
        public void WithTheSwitchOnAndNoWeldsASwimmersSixtySecondsAreD229sToTheBit()
        {
            Creature on = Swim(Water(assembly: true), Limb(LinkHalf), 60.0);
            Creature d229 = Swim(Water(assembly: false), Limb(LinkHalf), 60.0);

            Assert.True(d229.MechanicalWorkJoules > 0, "the stroke billed nothing: the test measures nothing");

            SameBits(d229.Position, on.Position, "Position");
            SameBits(d229.Rotation, on.Rotation, "Rotation");
            SameBits(d229.Velocity, on.Velocity, "Velocity");
            SameBits(d229.Q, on.Q, "Q");
            Assert.Equal(BitConverter.DoubleToInt64Bits(d229.MechanicalWorkJoules), BitConverter.DoubleToInt64Bits(on.MechanicalWorkJoules));
        }

        [Fact]
        public void WithAWeldedBladeTheSwimmerPartsFromD229()
        {
            Creature on = Swim(Water(assembly: true), Bladed(LinkHalf, BladeHalf), 20.0);
            Creature d229 = Swim(Water(assembly: false), Bladed(LinkHalf, BladeHalf), 20.0);

            Assert.True(on.MechanicalWorkJoules > d229.MechanicalWorkJoules);
            Assert.True((on.CentreOfMass() - d229.CentreOfMass()).Magnitude > 1e-4);
        }

        // ---- a resize

        [Fact]
        public void AResizeRecomputesTheConstantsAsABuildAtThatSizeWould()
        {
            SolverConfig config = Water();
            Phenotype adult = Developer.Develop(Bladed(LinkHalf, BladeHalf), DevelopmentLimits.Default);
            Phenotype half = adult.Scaled(0.5f);

            var grown = new Creature(0, adult, config);
            double massBefore = grown.StrokeAddedMass[0];
            double momentBefore = grown.StrokeFirstMoment[0];

            grown.Resize(half);
            var built = new Creature(1, half, config);

            SameBits(built.StrokeAddedMass, grown.StrokeAddedMass, "StrokeAddedMass");
            SameBits(built.StrokeFirstMoment, grown.StrokeFirstMoment, "StrokeFirstMoment");
            SameBits(built.StrokeAlong, grown.StrokeAlong, "StrokeAlong");
            Assert.Equal(built.StrokeAssemblyParts, grown.StrokeAssemblyParts);

            // Half the length is an eighth of the mass and a sixteenth of its moment.
            Near(massBefore / 8.0, grown.StrokeAddedMass[0], 1e-9);
            Near(momentBefore / 16.0, grown.StrokeFirstMoment[0], 1e-9);
        }

        // ---- the refusals

        [Fact]
        public void TheSwitchIsRefusedAtBuildWithoutTheFinLawNamingBoth()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(
                () => TestBodies.Build(Bladed(LinkHalf, BladeHalf), Water(assembly: true, shaped: false)));
            Assert.Contains("StrokePushesAssembly", e.Message);
            Assert.Contains("ShapedAddedMass", e.Message);
        }

        [Fact]
        public void TheSwitchIsRefusedAtBuildUnderTheSwingPushNamingIt()
        {
            SolverConfig swing = Water(assembly: true, shaped: true, push: Push, omega: 0);
            ArgumentException e = Assert.Throws<ArgumentException>(() => TestBodies.Build(Bladed(LinkHalf, BladeHalf), swing));
            Assert.Contains("StrokePushesAssembly", e.Message);
            Assert.Contains("swing push", e.Message);

            // With no limb push at all nothing pushes, and the switch is not in anyone's way.
            Assert.NotNull(TestBodies.Build(Bladed(LinkHalf, BladeHalf), Water(assembly: true, shaped: true, push: 0, omega: 0)));
        }

        [Fact]
        public void ABodyBuiltWithTheSwitchOffIsNotSteppedUnderItOn()
        {
            SolverConfig off = Water(assembly: false);
            Creature body = Build(Bladed(LinkHalf, BladeHalf), off);
            body.Drive.Stroke[0] = 0.5;
            Assert.Null(body.StrokeAssemblyParts);

            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ForceOn(body, Water(assembly: true), 1));
            Assert.Contains("StrokePushesAssembly", e.Message);
        }

        [Fact]
        public void WithTheSwitchOffNothingIsAllocated()
        {
            Creature body = Build(Bladed(LinkHalf, BladeHalf), Water(assembly: false));
            Assert.Null(body.StrokeAssemblyParts);
            Assert.Null(body.StrokeAlong);
            Assert.Null(body.StrokeAddedMass);
            Assert.Null(body.StrokeFirstMoment);
        }

        // ---- thread identity

        private static ulong Crowd(int threads, bool assembly, out double work, out int welded)
        {
            SolverConfig config = Water(assembly: assembly);
            config.CreatureContact = true;
            config.WorldDepthMetres = 15;
            config.FloorIsSolid = true;

            var world = new DynamicsWorld(config) { Threads = threads };
            var rng = new Rng(20261025);
            welded = 0;

            for (int i = 0; i < 40; i++)
            {
                // A root, a hinged link and a hinged tip with a flat blade welded beyond it.
                Genome genome = TestBodies.Chain(3, JointType.Hinge, power: 8f + i % 5, linkHalf: LinkHalf);
                for (int n = 1; n <= 2; n++)
                {
                    genome.Nodes[n].Neurons = new[]
                    {
                        new NeuronDef { Op = NeuronOp.OscillateWave, Frequency = 0.5f + 0.1f * (i % 7), Phase = 0.6f * n, Amplitude = 1f, Bias = 0.5f, Inputs = Array.Empty<NeuronInput>() },
                        new NeuronDef { Op = NeuronOp.Sum, Frequency = 1f, Amplitude = 1f, Bias = 0f, Inputs = Array.Empty<NeuronInput>() },
                    };
                }

                Weld(genome, 2, BladeHalf, new Float3(1f, 0f, 0f), new Float3(-1f, 0f, 0f));

                Creature body = TestBodies.Build(genome, config, i);
                if (body.StrokeAssemblyParts != null && body.StrokeAssemblyParts[2] > 1) welded++;

                body.PlaceAt(
                    new Vec3((rng.NextFloat() - 0.5) * 3.0, -3.0 - rng.NextFloat() * 2.0, (rng.NextFloat() - 0.5) * 3.0),
                    QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5),
                        rng.NextFloat() * 6.28));

                world.Add(body);
            }

            for (int step = 0; step < 600; step++) world.Step();

            work = 0;
            foreach (Creature body in world.Creatures) work += body.MechanicalWorkJoules;
            return world.Digest();
        }

        [Fact]
        public void TheThreadCountDoesNotChangeTheTrajectoryWithTheSwitchOn()
        {
            ulong one = Crowd(1, true, out double work, out int welded);
            ulong eight = Crowd(8, true, out _, out _);
            ulong twentyFour = Crowd(24, true, out _, out _);

            // The same crowd on D229's lines is another trajectory, so the rule did something.
            ulong d229 = Crowd(1, false, out double d229Work, out _);

            _out.WriteLine($"assembly  1 thread   {one:x16}");
            _out.WriteLine($"assembly  8 threads  {eight:x16}");
            _out.WriteLine($"assembly 24 threads  {twentyFour:x16}");
            _out.WriteLine($"D229      1 thread   {d229:x16}");
            _out.WriteLine($"bodies with a blade beyond a joint: {welded}; work booked {work:0.####} J against D229's {d229Work:0.####} J");

            Assert.Equal(one, eight);
            Assert.Equal(one, twentyFour);
            Assert.NotEqual(one, d229);
            Assert.True(welded > 0, "no body carried an assembly: the test measures nothing");
            Assert.True(work > 0, "no stroke was billed: the test measures nothing");
        }
    }
}

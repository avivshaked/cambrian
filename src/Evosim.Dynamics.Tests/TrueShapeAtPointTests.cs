using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D228's contact on the true shape, level (b), on the host: <see cref="ContactShapeForm.TrueShapeAtPoint"/>.
    /// The force acts at the contact point with the torque <c>r x F</c> (each touch's capped by
    /// <see cref="Anchor.CapTorque"/>), and the damper reads the point's velocity. What a body carries, the lever of
    /// each touch in both orders of the canonical pair, the damper, the stability of a plate and a thin link
    /// against the recorded step and contact constants, the thread count, and that the level at the centre is
    /// what it was.
    /// </summary>
    public sealed class TrueShapeAtPointTests
    {
        private readonly ITestOutputHelper _out;

        public TrueShapeAtPointTests(ITestOutputHelper output) => _out = output;

        private static readonly Float3 Plate = new Float3(0.5f, 0.05f, 0.5f);

        /// <summary>
        /// The recorded step and contact constants are <see cref="SolverConfig"/>'s own: a step of 0.01 s,
        /// <c>ContactOmega</c> 20, <c>ContactDampingRatio</c> 1, <c>MaxSeparationSpeed</c> 1 and
        /// <c>MaxContactSpeedChange</c> 1. They are set here by name so that a change of a default shows.
        /// </summary>
        private static SolverConfig Config(ContactShapeForm form, double depth = 1e6, double excess = 0, bool water = false)
        {
            SolverConfig config = water ? TestBodies.Water(0.01) : TestBodies.Vacuum(0.01);
            config.StepSeconds = 0.01;
            config.ContactOmega = 20.0;
            config.ContactDampingRatio = 1.0;
            config.MaxSeparationSpeed = 1.0;
            config.MaxContactSpeedChange = 1.0;

            config.CreatureContact = true;
            config.ContactPerPart = true;
            config.ContactShape = form;
            config.WorldDepthMetres = depth;
            config.FloorIsSolid = true;
            config.TissueExcessDensity = excess;
            config.NeutralBodyVolume = 0;
            config.ContactInstrument = true;
            return config;
        }

        private static Genome Shaped(string shape, Float3 halfExtents)
        {
            Genome genome = TestBodies.Box(halfExtents);
            genome.Nodes[0].ShapeId = shape;
            return genome;
        }

        private static Creature Part(SolverConfig config, string shape, Float3 h, int id, Vec3 at, QuatD turn)
        {
            Creature body = TestBodies.Build(Shaped(shape, h), config, id);
            body.PlaceAt(at, turn);
            return body;
        }

        private static void Pass(SolverConfig config, params Creature[] bodies)
        {
            var list = new List<Creature>(bodies);
            var grid = new ContactGrid();
            grid.BuildLinks(list);

            int[] scratch = new int[8];
            for (int i = 0; i < list.Count; i++)
            {
                Array.Clear(list[i].Fext, 0, list[i].Fext.Length);
                Contacts.Apply(list[i], i, grid, config, ref scratch);
            }
        }

        private static Vec3 Force(Creature body, int link = 0) => Vec3.Read(body.Fext, 6 * link + 3);

        private static Vec3 Torque(Creature body, int link = 0) => Vec3.Read(body.Fext, 6 * link);

        /// <summary>The mass the link's first part meets along <paramref name="normal"/> at <paramref name="point"/>, from the body's own inertia and committed turn.</summary>
        private static double MassAt(Creature body, Vec3 point, Vec3 centre, Vec3 normal) =>
            ContactTrueShape.EffectiveMass(
                point - centre, normal, Vec3.Read(body.InertiaLocal, 0), Mat3.Read(body.LinkContactRotation, 0), body.Mass[0]);

        private static void AssertNear(Vec3 expected, Vec3 actual, double relative, string what)
        {
            double scale = Math.Max(expected.Magnitude, actual.Magnitude);
            double error = (expected - actual).Magnitude;
            Assert.True(error <= relative * scale + 1e-300, $"{what}: expected {expected}, got {actual} (error {error:E3})");
        }

        // ------------------------------------------------------------ what a body carries

        [Fact]
        public void OnlyTheLevelAtThePointCarriesASpinAndBothTrueShapeLevelsCarryTheTurnsAndCores()
        {
            Genome chain = TestBodies.Chain(3, JointType.Hinge);

            foreach (ContactShapeForm form in new[] { ContactShapeForm.Spheres, ContactShapeForm.TrueShape })
            {
                Creature built = TestBodies.Build(chain, Config(form), 1);
                Assert.Null(built.LinkContactSpin);
                Assert.Equal(form == ContactShapeForm.TrueShape, built.LinkContactRotation != null);
            }

            Creature on = TestBodies.Build(chain, Config(ContactShapeForm.TrueShapeAtPoint), 1);
            Assert.Equal(3 * 3, on.LinkContactSpin.Length);
            Assert.Equal(9 * 3, on.LinkContactRotation.Length);
            Assert.Equal(3 * 3, on.LinkCoreHalf.Length);
            Assert.Equal(3, on.LinkMargin.Length);
            Assert.True(ContactShapeForm.TrueShapeAtPoint.IsTrueShape());
            Assert.True(ContactShapeForm.TrueShape.IsTrueShape());
            Assert.False(ContactShapeForm.Spheres.IsTrueShape());
            Assert.True(ContactShapeForm.TrueShapeAtPoint.AtPoint());
            Assert.False(ContactShapeForm.TrueShape.AtPoint());

            // The level needs per-part contact, as the other.
            SolverConfig body = Config(ContactShapeForm.TrueShapeAtPoint);
            body.ContactPerPart = false;
            Assert.Throws<ArgumentException>(() => TestBodies.Build(chain, body, 1));
        }

        [Fact]
        public void TheCommittedSpinIsTheSnapshotOfTheLastCommitForEveryLinkAndTheOneLinkBody()
        {
            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint);

            foreach (Genome genome in new[] { TestBodies.Chain(3, JointType.Hinge), Shaped(ShapeIds.Capsule, new Float3(0.1f, 0.4f, 0.1f)) })
            {
                Creature body = TestBodies.Build(genome, config, 1);
                body.PlaceAt(new Vec3(1, -2, 3), QuatD.FromAxisAngle(new Vec3(0.3, 1, -0.4).Normalized, 0.9));

                // Placed at rest: the snapshot is the rest.
                Assert.All(body.LinkContactSpin, s => Assert.Equal(0.0, s));

                // A new spin, refreshed and not yet committed: the snapshot keeps the old one.
                for (int i = 0; i < body.Links; i++)
                {
                    Vec3.Write(body.Spin, 3 * i, new Vec3(0.1 * (i + 1), -0.2, 0.3 - 0.05 * i));
                }

                body.RefreshContactSphere();
                Assert.All(body.LinkContactSpin, s => Assert.Equal(0.0, s));

                // Committed: the spin the solver integrates.
                body.CommitContactSphere();
                for (int k = 0; k < 3 * body.Links; k++) Assert.Equal(body.Spin[k], body.LinkContactSpin[k]);

                // A restore rebuilds it from the state.
                Array.Clear(body.LinkContactSpin, 0, body.LinkContactSpin.Length);
                body.RestoreLinkSpheres();
                for (int k = 0; k < 3 * body.Links; k++) Assert.Equal(body.Spin[k], body.LinkContactSpin[k]);
            }
        }

        // ------------------------------------------------------------ the damper reads the point's velocity

        [Fact]
        public void TheBedsDamperReadsTheContactPointsVelocityAtThePointAndTheCentresAtTheCentre()
        {
            const double depth = 10;
            const double stiffness = 1000, damping = 100;

            // Turned 30 degrees about z: the lowest corner is at -x of the centre.
            QuatD turn = QuatD.FromAxisAngle(Vec3.UnitZ, Math.PI / 6);
            double hold = 0.45 * Math.Sin(Math.PI / 6) + 0.05;
            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth);
            Creature plate = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -depth + hold - 0.005, 0), turn);
            Vec3 centre = Vec3.Read(plate.LinkContactCentre, 0);

            // Turned about z alone, the plate's two lowest corners (the two at -x) tie for the lowest point, so at the
            // level at the point two vertices push, with the same depth and the same lever x. The single support point
            // is one of them.
            Vec3 single = ContactTrueShape.FlatBed(
                plate, 0, centre, plate.LinkContactRadius[0], plate.LinkMargin[0], Vec3.Zero, plate.Mass[0],
                config, stiffness, damping, out Vec3 cornerSupport, out Vec3 cornerPoint);
            Vec3 still = ContactTrueShape.FlatBed(
                plate, 0, centre, plate.LinkContactRadius[0], plate.LinkMargin[0], Vec3.Zero, plate.Mass[0],
                config, stiffness, damping, out _, out Vec3 point, atPoint: true);
            Assert.True(single.Y > 0);

            // Each vertex meets the mass at its corner, not the link's: the single support point's routine called at
            // that mass (the two corners are mirror images across the plate's middle, so they meet the same one).
            double effective = MassAt(plate, cornerPoint, centre, Vec3.UnitY);
            double scale = effective / plate.Mass[0];
            Assert.True(scale < 0.9, $"the corner was meant to be lighter than the plate: {scale}");
            Vec3 singleAtEffective = ContactTrueShape.SphereAt(
                ContactTrueShape.KindFlatBed, cornerSupport, cornerPoint, centre, plate.LinkMargin[0], Vec3.Zero, Vec3.Zero, false,
                effective, config, stiffness * scale, damping * scale);
            AssertNear(new Vec3(0, 2.0 * singleAtEffective.Y, 0), still, 1e-12, "two vertices at the same depth push twice one");

            // Turning about z at 1 rad/s: the point at r moves at spin x r, whose y is spin * r.x, and r.x < 0.
            Vec3 spin = new Vec3(0, 0, 1);
            Vec3 lever = point - centre;
            Assert.True(lever.X < 0);
            double pointSpeedY = Vec3.Cross(spin, lever).Y;
            Assert.True(pointSpeedY < 0);

            Vec3 atPoint = ContactTrueShape.FlatBed(
                plate, 0, centre, plate.LinkContactRadius[0], plate.LinkMargin[0], Vec3.Zero, plate.Mass[0],
                config, stiffness, damping, out _, out _, spin, atPoint: true);

            // The points are closing on the bed, so the damper adds to the spring, at each of the two vertices, each
            // damper at the corner's mass.
            Assert.Equal(still.Y - 2.0 * damping * scale * pointSpeedY, atPoint.Y, 9);
            Assert.True(atPoint.Y > still.Y);

            // At the level at the centre the spin is not read, whatever it is.
            Vec3 atCentre = ContactTrueShape.FlatBed(
                plate, 0, centre, plate.LinkContactRadius[0], plate.LinkMargin[0], Vec3.Zero, plate.Mass[0],
                config, stiffness, damping, out _, out _, spin, atPoint: false);
            Assert.Equal(single.Y, atCentre.Y);

            // The velocity helper itself: the centre's, plus spin x r.
            Vec3 v = new Vec3(0.3, -0.1, 0.2);
            Assert.Equal(v, ContactTrueShape.VelocityAt(v, spin, lever, atPoint: false));
            AssertNear(v + Vec3.Cross(spin, lever), ContactTrueShape.VelocityAt(v, spin, lever, atPoint: true), 1e-15, "v + spin x r");
        }

        [Fact]
        public void ABodyAtRestOnTheBedFeelsTheSameForceAtThePointAsAtTheCentreAndOnlyTheFirstHasATorque()
        {
            const double depth = 10;
            QuatD turn = QuatD.FromAxisAngle(Vec3.UnitZ, Math.PI / 6);
            double hold = 0.45 * Math.Sin(Math.PI / 6) + 0.05;
            var at = new Vec3(0, -depth + hold - 0.005, 0);

            SolverConfig centreConfig = Config(ContactShapeForm.TrueShape, depth);
            Creature atCentre = Part(centreConfig, ShapeIds.Box, Plate, 1, at, turn);
            Pass(centreConfig, atCentre);

            SolverConfig pointConfig = Config(ContactShapeForm.TrueShapeAtPoint, depth);
            Creature atPoint = Part(pointConfig, ShapeIds.Box, Plate, 1, at, turn);
            Pass(pointConfig, atPoint);

            // A still body: the damper reads nothing either way. Two corners tie for the lowest point, so the level at
            // the point pushes with both and the level at the centre with the one support point: twice the force, each
            // at the mass its corner meets (the two corners are mirror images across the plate's middle).
            Vec3 centre = Vec3.Read(atPoint.LinkContactCentre, 0);
            ContactTrueShape.FlatBed(
                atPoint, 0, centre, atPoint.LinkContactRadius[0], atPoint.LinkMargin[0], Vec3.Zero, atPoint.Mass[0],
                pointConfig, 1000, 0, out _, out Vec3 cornerPoint);
            double scale = MassAt(atPoint, cornerPoint, centre, Vec3.UnitY) / atPoint.Mass[0];
            Assert.True(scale < 0.9, $"the corner was meant to be lighter than the plate: {scale}");

            Assert.True(Force(atCentre).Y > 0);
            AssertNear(new Vec3(0, 2.0 * scale * Force(atCentre).Y, 0), Force(atPoint), 1e-12, "two corners push twice one, each at its mass");

            // At the centre there is no torque. At the point the torque is the sum of r x F over the two corners, which
            // is the push-weighted point's r x the summed F, and the push tips the plate back toward flat.
            Assert.Equal(Vec3.Zero, Torque(atCentre));

            ContactTrueShape.FlatBed(
                atPoint, 0, centre, atPoint.LinkContactRadius[0], atPoint.LinkMargin[0], Vec3.Zero, atPoint.Mass[0],
                pointConfig, 1000, 0, out _, out Vec3 point, atPoint: true);

            // A one-link body's force and torque are both spread by m (1/M); r x F is taken on what was applied.
            Vec3 expected = Vec3.Cross(point - centre, Force(atPoint));
            AssertNear(expected, Torque(atPoint), 1e-12, "the bed's torque");
            Assert.True(Torque(atPoint).Z < 0, "the corners at -x are pushed up: the plate turns back toward flat");
            Assert.True(Math.Abs(Torque(atPoint).X) <= 1e-9 * Torque(atPoint).Magnitude, "the two corners lie either side of the centre in z: no turn about x");
        }

        [Fact]
        public void TheGlassAndTheReefsTorqueIsTheLeverTimesTheirForceToo()
        {
            QuatD edgeOn = QuatD.FromAxisAngle(Vec3.UnitX, Math.PI / 2);

            // The glass: a plate on its edge, turned about the vertical so that its corner reaches further.
            SolverConfig glass = Config(ContactShapeForm.TrueShapeAtPoint, 100);
            glass.TankRadiusMetres = 5;
            QuatD skew = QuatD.FromAxisAngle(Vec3.UnitY, 0.4) * edgeOn;
            // The core reaches 0.45 cos 0.4 = 0.41 toward the glass and the margin 0.05 more: 0.46, so the plate
            // sits 0.43 from the glass and presses 0.03 into it.
            Creature atGlass = Part(glass, ShapeIds.Box, Plate, 1, new Vec3(10 - 0.43, -5, 5), skew);
            Pass(glass, atGlass);
            Assert.True(Force(atGlass).X < 0, "the plate was meant to touch the glass");

            Vec3 centre = Vec3.Read(atGlass.LinkContactCentre, 0);
            ContactTrueShape.Glass(
                atGlass, 0, centre, atGlass.LinkContactRadius[0], atGlass.LinkMargin[0], Vec3.Zero, atGlass.Mass[0],
                glass, 1000, 0, out _, out Vec3 point, atPoint: true);
            AssertNear(Vec3.Cross(point - centre, Force(atGlass)), Torque(atGlass), 1e-12, "the glass's torque");
            Assert.True(Torque(atGlass).Magnitude > 0, "the glass pushed through the centre: the test measures nothing");

            // The reefs: a tilted plate on the cap.
            SolverConfig reefs = Config(ContactShapeForm.TrueShapeAtPoint, 12);
            reefs.Reefs = new ReefGeometry(3f, 3f, 2f, 1f, 3f, new[] { 0d }, new[] { 0d });
            double top = reefs.Reefs.CapTopY(0);
            QuatD tilt = QuatD.FromAxisAngle(new Vec3(0.2, 0, 1).Normalized, 0.3);

            // Lowered from above until the cap first pushes it.
            Creature onCap = null;
            for (double y = top + 0.5; y > top - 0.2; y -= 0.01)
            {
                onCap = Part(reefs, ShapeIds.Box, Plate, 2, new Vec3(0.8, y, 0.3), tilt);
                Pass(reefs, onCap);
                if (Force(onCap).Magnitude > 0) break;
            }

            Assert.True(Force(onCap).Magnitude > 0, "the plate was meant to rest on the cap and was not pushed");

            centre = Vec3.Read(onCap.LinkContactCentre, 0);
            ContactTrueShape.Reef(
                onCap, 0, centre, onCap.LinkContactRadius[0], onCap.LinkMargin[0], Vec3.Zero, onCap.Mass[0],
                reefs, onCap.Mass[0] * 400.0, 2.0 * onCap.Mass[0] * 20.0, out _, out point, atPoint: true);
            AssertNear(Vec3.Cross(point - centre, Force(onCap)), Torque(onCap), 1e-12, "the reef's torque");
            Assert.True(Torque(onCap).Magnitude > 0);
        }

        // ------------------------------------------------------------ pairs: levers in both orders, opposite forces, moments

        /// <summary>
        /// Two two-link bodies, B lowered onto A until the first pair of parts touches; spinning and moving, with the
        /// ids as given. Returns the touched link of each.
        /// </summary>
        private static void TouchingPair(
            SolverConfig config, int idA, int idB, out Creature a, out Creature b, out int touchedA, out int touchedB)
        {
            Genome genome = TestBodies.Chain(2, JointType.Hinge, power: 10f, neurons: false);
            genome.Nodes[0].ShapeId = ShapeIds.Capsule;
            genome.Nodes[1].ShapeId = ShapeIds.Box;

            a = TestBodies.Build(genome, config, idA);
            b = TestBodies.Build(genome, config, idB);
            a.PlaceAt(new Vec3(0, -5, 0), QuatD.FromAxisAngle(new Vec3(0.3, 1.0, -0.2).Normalized, 0.7));
            Vec3 tipA = Vec3.Read(a.Position, 3);

            for (double dy = 1.6; dy > 0 && Force(a, 0).Magnitude == 0 && Force(a, 1).Magnitude == 0; dy -= 0.01)
            {
                b.PlaceAt(tipA + new Vec3(0.1, dy, 0.2), QuatD.FromAxisAngle(new Vec3(-0.6, 0.2, 1.0).Normalized, 1.9));

                for (int i = 0; i < a.Links; i++)
                {
                    Vec3.Write(a.Velocity, 3 * i, new Vec3(0.3, -0.2, 0.1));
                    Vec3.Write(b.Velocity, 3 * i, new Vec3(-0.1, 0.05, 0.4));
                    Vec3.Write(a.Spin, 3 * i, new Vec3(0.4, -0.7, 0.2));
                    Vec3.Write(b.Spin, 3 * i, new Vec3(-0.3, 0.2, 0.9));
                }

                a.RefreshContactSphere(); a.CommitContactSphere();
                b.RefreshContactSphere(); b.CommitContactSphere();

                Pass(config, a, b);
            }

            touchedA = -1;
            touchedB = -1;
            for (int i = 0; i < 2; i++)
            {
                if (Force(a, i).Magnitude > 0) { Assert.Equal(-1, touchedA); touchedA = i; }
                if (Force(b, i).Magnitude > 0) { Assert.Equal(-1, touchedB); touchedB = i; }
            }

            Assert.True(touchedA >= 0 && touchedB >= 0, $"ids ({idA}, {idB}): the bodies were meant to touch and did not");
        }

        [Fact]
        public void ThePairsForcesAreOppositeBitForBitEachTorqueIsItsOwnLeverTimesItsForceAndTheMomentAboutAFixedPointVanishes()
        {
            // Both orders of the canonical pair: the body with the lower id is the first core, and it is A in the
            // first run and B in the second.
            foreach ((int idA, int idB) in new[] { (3, 8), (8, 3) })
            {
                SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint);
                TouchingPair(config, idA, idB, out Creature a, out Creature b, out int linkA, out int linkB);

                Vec3 fa = Force(a, linkA), fb = Force(b, linkB);
                Assert.Equal(BitConverter.DoubleToInt64Bits(-fa.X), BitConverter.DoubleToInt64Bits(fb.X));
                Assert.Equal(BitConverter.DoubleToInt64Bits(-fa.Y), BitConverter.DoubleToInt64Bits(fb.Y));
                Assert.Equal(BitConverter.DoubleToInt64Bits(-fa.Z), BitConverter.DoubleToInt64Bits(fb.Z));

                // The pair's own frame: the first core at the origin, the second at minus the centres' difference.
                bool aFirst = a.Id < b.Id;
                Creature first = aFirst ? a : b, second = aFirst ? b : a;
                int lf = aFirst ? linkA : linkB, ls = aFirst ? linkB : linkA;

                Vec3 between = Vec3.Read(first.LinkContactCentre, 3 * lf) - Vec3.Read(second.LinkContactCentre, 3 * ls);
                Vec3 minusBetween = Vec3.Zero - between;

                Assert.True(ContactTrueShape.Pair(
                    Vec3.Zero, Mat3.Read(first.LinkContactRotation, 9 * lf),
                    Vec3.Read(first.LinkCoreHalf, 3 * lf), first.LinkMargin[lf], first.LinkContactRadius[lf],
                    minusBetween, Mat3.Read(second.LinkContactRotation, 9 * ls),
                    Vec3.Read(second.LinkCoreHalf, 3 * ls), second.LinkMargin[ls], second.LinkContactRadius[ls],
                    out _, out _, out Vec3 point));

                Vec3 leverFirst = point;
                Vec3 leverSecond = point - minusBetween;

                // Both levers reach the one contact point in the world.
                Vec3 worldFirst = Vec3.Read(first.LinkContactCentre, 3 * lf) + leverFirst;
                Vec3 worldSecond = Vec3.Read(second.LinkContactCentre, 3 * ls) + leverSecond;
                AssertNear(worldFirst, worldSecond, 1e-12, $"ids ({idA}, {idB}): the two levers meet at one point");

                // Each torque is its own lever times its own force, taken before the cap, which is slack here.
                Vec3 firstForce = Force(first, lf), secondForce = Force(second, ls);
                Vec3 firstTorque = Torque(first, lf), secondTorque = Torque(second, ls);
                AssertNear(Vec3.Cross(leverFirst, firstForce), firstTorque, 1e-12, $"ids ({idA}, {idB}): the first body's torque");
                AssertNear(Vec3.Cross(leverSecond, secondForce), secondTorque, 1e-12, $"ids ({idA}, {idB}): the second body's torque");
                Assert.True(firstTorque.Magnitude > 0 && secondTorque.Magnitude > 0, "a torque through the centre measures nothing");
                Assert.NotEqual(leverFirst, leverSecond);

                double cap = Anchor.CapTorque(new Vec3(1e9, 0, 0), first.SmallestInertia[lf], first.LinkContactRadius[lf], config).Magnitude;
                Assert.True(firstTorque.Magnitude < cap, "the cap was meant to be slack");

                // The moment of the pair's forces about a fixed point: each body's torque and its force at its
                // centre. The forces are equal and opposite at one point, so the whole is nothing.
                var origin = new Vec3(0.7, -3.3, 1.1);
                Vec3 moment = Vec3.Zero;
                double scale = 0;
                foreach (Creature body in new[] { a, b })
                {
                    for (int i = 0; i < body.Links; i++)
                    {
                        Vec3 c = Vec3.Read(body.LinkContactCentre, 3 * i);
                        Vec3 arm = Vec3.Cross(c - origin, Force(body, i));
                        moment += Torque(body, i) + arm;
                        scale += Torque(body, i).Magnitude + arm.Magnitude;
                    }
                }

                Assert.True(scale > 0);
                Assert.True(moment.Magnitude <= 1e-9 * scale, $"ids ({idA}, {idB}): the moment {moment.Magnitude:E3} against {scale:E3}");
                _out.WriteLine($"ids ({idA}, {idB}): torques {firstTorque.Magnitude:0.####} and {secondTorque.Magnitude:0.####} N m, moment {moment.Magnitude:E2} of {scale:0.###}");
            }
        }

        [Fact]
        public void ThePairsDamperReadsTheOtherBodysVelocityAtTheSamePointNotItsCentre()
        {
            // The same pair with and without a spin on one body: only the point's velocity differs, so the
            // pair's push differs by the damper's term along the normal, and the forces stay opposite.
            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint);
            TouchingPair(config, 3, 8, out Creature a, out Creature b, out int linkA, out int linkB);
            Vec3 withSpin = Force(a, linkA);

            // Take the spin off every link and refresh: the same positions and velocities.
            for (int i = 0; i < a.Links; i++) Vec3.Write(a.Spin, 3 * i, Vec3.Zero);
            for (int i = 0; i < b.Links; i++) Vec3.Write(b.Spin, 3 * i, Vec3.Zero);
            a.RefreshContactSphere(); a.CommitContactSphere();
            b.RefreshContactSphere(); b.CommitContactSphere();
            Pass(config, a, b);
            Vec3 withoutSpin = Force(a, linkA);

            Assert.NotEqual(withSpin, withoutSpin);

            // The level at the centre reads the spin nowhere: the two runs are the same force.
            SolverConfig centre = Config(ContactShapeForm.TrueShape);
            TouchingPair(centre, 3, 8, out Creature c, out _, out int linkC, out _);
            Vec3 centreWithSpin = Force(c, linkC);

            TouchingPair(centre, 3, 8, out Creature d, out Creature e, out int linkD, out int linkE);
            for (int i = 0; i < d.Links; i++) Vec3.Write(d.Spin, 3 * i, Vec3.Zero);
            for (int i = 0; i < e.Links; i++) Vec3.Write(e.Spin, 3 * i, Vec3.Zero);
            d.RefreshContactSphere(); d.CommitContactSphere();
            e.RefreshContactSphere(); e.CommitContactSphere();
            Pass(centre, d, e);
            Assert.Equal(centreWithSpin, Force(d, linkD));
            Assert.Equal(0.0, Torque(d, linkD).Magnitude);
        }

        // ------------------------------------------------------------ stability

        private sealed class PlateRun
        {
            public bool Touched;
            public double FlatSeconds;      // the last time the plate stood more than 5 degrees off flat
            public double FinalTiltDegrees;
            public double MaxTiltLastTenSecondsDegrees;
            public double FinalHeightAboveRest;
            public double LargestKinetic;
            public double BoundAtThatStep;
            public double LargestExcess;    // the most the kinetic energy stood above the bound
            public double Slack;
            public double FirstTouchKinetic;
            public int NonFinite;
        }

        /// <summary>
        /// The plate dropped onto the flat bed, resting on one edge (tilted 25 degrees about z), for 60 s of simulated
        /// time, with the recorded step and contact constants. The kinetic energy, linear and turning, is compared at
        /// every step after the first touch with its value at the first touch plus the work gravity has done since.
        /// </summary>
        private static PlateRun DropPlate(ContactShapeForm form)
        {
            const double depth = 5;
            const double tilt0 = 25.0 * Math.PI / 180.0;
            const double excess = 200;

            SolverConfig config = Config(form, depth, excess, water: true);
            var world = new DynamicsWorld(config);
            Creature plate = TestBodies.Build(TestBodies.Box(Plate), config, 1);
            plate.PlaceAt(
                new Vec3(0, -depth + 0.45 * Math.Sin(tilt0) + 0.05 + 0.01, 0),
                QuatD.FromAxisAngle(Vec3.UnitZ, tilt0));
            world.Add(plate);

            double mass = plate.Mass[0];
            double weight = excess * (1.0 - plate.Lift[0]) * mass * config.GravityMetresPerSecondSquared / config.TissueDensity;

            // The box's inertia about its own axes, from the half-extents (0.5, 0.05, 0.5).
            double ix = mass / 3.0 * (0.05 * 0.05 + 0.5 * 0.5);
            double iy = mass / 3.0 * (0.5 * 0.5 + 0.5 * 0.5);
            double iz = mass / 3.0 * (0.5 * 0.5 + 0.05 * 0.05);

            double Kinetic()
            {
                Vec3 v = Vec3.Read(plate.Velocity, 0);
                Vec3 local = Mat3.Read(plate.RotationMatrix, 0).TransposedTimes(Vec3.Read(plate.Spin, 0));
                return 0.5 * mass * v.SqrMagnitude +
                       0.5 * (ix * local.X * local.X + iy * local.Y * local.Y + iz * local.Z * local.Z);
            }

            double Tilt() => Math.Acos(Math.Max(-1.0, Math.Min(1.0, Mat3.Read(plate.RotationMatrix, 0).M11))) * 180.0 / Math.PI;

            var run = new PlateRun();
            double baseKinetic = 0, baseHeight = 0;
            int steps = (int)Math.Round(60.0 / config.StepSeconds);

            for (int step = 0; step < steps; step++)
            {
                double before = Kinetic();
                double heightBefore = plate.Position[1];

                world.Step();
                double time = (step + 1) * config.StepSeconds;

                if (!run.Touched && plate.TouchedBedOrGlass)
                {
                    run.Touched = true;
                    baseKinetic = before;
                    baseHeight = heightBefore;
                    run.FirstTouchKinetic = before;
                    run.Slack = 1e-3 * (before + 0.25 * weight) + 1e-9;
                }

                double tilt = Tilt();
                if (tilt >= 5.0) run.FlatSeconds = time;
                if (time > 50.0 && tilt > run.MaxTiltLastTenSecondsDegrees) run.MaxTiltLastTenSecondsDegrees = tilt;

                if (run.Touched)
                {
                    double kinetic = Kinetic();
                    double bound = baseKinetic + weight * (baseHeight - plate.Position[1]);
                    if (kinetic > run.LargestKinetic) { run.LargestKinetic = kinetic; run.BoundAtThatStep = bound; }
                    if (kinetic - bound > run.LargestExcess) run.LargestExcess = kinetic - bound;
                }
            }

            run.FinalTiltDegrees = Tilt();
            run.FinalHeightAboveRest = plate.Position[1] - (-depth + 0.05);
            run.NonFinite = world.NonFiniteBodies();
            return run;
        }

        [Fact]
        public void APlateDroppedOnItsEdgeTipsAndLiesFlatAtThePointAndDoesNotAtTheCentre()
        {
            PlateRun point = DropPlate(ContactShapeForm.TrueShapeAtPoint);
            PlateRun centre = DropPlate(ContactShapeForm.TrueShape);

            _out.WriteLine(
                $"at the point: touched {point.Touched}, last more than 5 degrees off flat at {point.FlatSeconds:0.00} s, " +
                $"final tilt {point.FinalTiltDegrees:0.###} degrees, largest tilt in the last 10 s {point.MaxTiltLastTenSecondsDegrees:0.###} degrees, " +
                $"centre {point.FinalHeightAboveRest * 1000:0.##} mm above rest");
            _out.WriteLine(
                $"at the centre: touched {centre.Touched}, final tilt {centre.FinalTiltDegrees:0.###} degrees " +
                $"(25 at the start), centre {centre.FinalHeightAboveRest * 1000:0.##} mm above rest");
            _out.WriteLine(PlateNumbers());

            Assert.True(point.Touched && centre.Touched, "the plate was meant to reach the bed");
            Assert.Equal(0, point.NonFinite);
            Assert.Equal(0, centre.NonFinite);

            // At the point: lies flat, and stays flat, within 60 s.
            Assert.True(point.FlatSeconds < 60.0, $"the plate was still off flat at {point.FlatSeconds} s");
            Assert.True(point.FinalTiltDegrees < 5.0, $"the plate ended {point.FinalTiltDegrees} degrees off flat");
            Assert.True(point.MaxTiltLastTenSecondsDegrees < 5.0, $"the plate rocked to {point.MaxTiltLastTenSecondsDegrees} degrees in the last 10 s");

            // At the centre the force has no torque: the plate stays on its edge.
            Assert.True(centre.FinalTiltDegrees > 15.0, $"the plate tipped to {centre.FinalTiltDegrees} degrees at the centre; it should not");
        }

        [Fact]
        public void ThePlatesKineticEnergyNeverRisesAboveItsValueAtTheFirstTouchPlusTheWorkGravityHasDone()
        {
            PlateRun run = DropPlate(ContactShapeForm.TrueShapeAtPoint);

            _out.WriteLine(
                $"kinetic energy at the first touch {run.FirstTouchKinetic:0.####} J; largest {run.LargestKinetic:0.####} J against a bound of " +
                $"{run.BoundAtThatStep:0.####} J there; the most it stood above its bound {run.LargestExcess:E3} J (allowed {run.Slack:E3} J)");
            _out.WriteLine(PlateNumbers());

            Assert.True(run.Touched, "the plate was meant to reach the bed");
            Assert.Equal(0, run.NonFinite);
            Assert.True(run.LargestExcess <= run.Slack, $"the contact fed the plate {run.LargestExcess} J; the allowance is {run.Slack} J");
        }

        /// <summary>
        /// A plate set down flat and still on the flat bed stays still. Its four bottom corners tie for the deepest
        /// point, and a single support point puts the whole push on one of them, which would rock the plate from
        /// corner to corner for as long as it rests. After 5 s to settle, its spin and tilt over the next 55 s are
        /// read against small bounds.
        /// </summary>
        /// <summary>The plate set down flat for 60 s: the largest spin and tilt after the first 5 s, and how far below its margin contact it rests.</summary>
        private static double FlatPlate(ContactShapeForm form, out double largestSpin, out double largestTilt, out int nonFinite)
        {
            const double depth = 5;
            SolverConfig config = Config(form, depth, excess: 200, water: true);
            var world = new DynamicsWorld(config);
            Creature plate = TestBodies.Build(TestBodies.Box(Plate), config, 1);
            plate.PlaceAt(new Vec3(0, -depth + 0.05, 0), QuatD.Identity);
            world.Add(plate);

            largestSpin = 0;
            largestTilt = 0;
            int steps = (int)Math.Round(60.0 / config.StepSeconds);
            int settle = (int)Math.Round(5.0 / config.StepSeconds);
            for (int step = 0; step < steps; step++)
            {
                world.Step();
                if (step < settle) continue;
                double spin = Vec3.Read(plate.Spin, 0).Magnitude;
                double tilt = Math.Acos(Math.Max(-1.0, Math.Min(1.0, Mat3.Read(plate.RotationMatrix, 0).M11))) * 180.0 / Math.PI;
                if (spin > largestSpin) largestSpin = spin;
                if (tilt > largestTilt) largestTilt = tilt;
            }

            nonFinite = world.NonFiniteBodies();
            return (-depth + 0.05) - plate.Position[1];
        }

        [Fact]
        public void APlateSetDownFlatOnTheBedStaysStill()
        {
            double sink = FlatPlate(ContactShapeForm.TrueShapeAtPoint, out double largestSpin, out double largestTilt, out int nonFinite);

            // The same plate at the level at the centre, whose single support point carries the whole weight on one spring.
            double singleSink = FlatPlate(ContactShapeForm.TrueShape, out double singleSpin, out double singleTilt, out int singleNonFinite);

            _out.WriteLine($"flat plate after 5 s: largest spin {largestSpin:E3} rad/s, largest tilt {largestTilt:0.####} degrees");
            _out.WriteLine($"resting depth below the margin contact: {sink * 1000:0.####} mm on four vertices; {singleSink * 1000:0.####} mm on the one support point (level at the centre; spin {singleSpin:E2}, tilt {singleTilt:0.###} degrees)");
            _out.WriteLine(PlateNumbers());

            Assert.Equal(0, nonFinite);
            Assert.Equal(0, singleNonFinite);
            Assert.True(largestSpin < 0.02, $"the resting plate spun at {largestSpin} rad/s");
            Assert.True(largestTilt < 0.5, $"the resting plate rocked to {largestTilt} degrees");

            // Four springs share the weight, each at the mass its corner meets. Worked by hand for the test plate
            // (half-extents 0.5, 0.05, 0.5 m, so the core is 0.45 x 0 x 0.45 m and the margin 0.05 m): flat, a
            // vertex point is at (+-0.45, -0.05, +-0.45) from the centre and n is up, so r x n = (-rz, 0, rx) and
            // m q = (rz^2 + rx^2) / ((0.05^2 + 0.5^2) / 3) = 0.405 / 0.0841667 = 4.8119. So m_eff = m / 5.8119 =
            // 0.17206 m, the four of them 0.68825 m, and the plate sinks 1 / 0.68825 = 1.4530 times as far as the single
            // sphere.
            Assert.True(sink > 0 && singleSink > 0);
            double q = (2.0 * 0.45 * 0.45) / ((0.05 * 0.05 + 0.5 * 0.5) / 3.0);
            double predicted = 1.0 / (4.0 / (1.0 + q));
            _out.WriteLine($"resting depth ratio: measured {sink / singleSink:0.####}, predicted m / sum(m_eff) = {predicted:0.####} (m_eff = {1.0 / (1.0 + q):0.####} m)");
            Assert.Equal(1.45, predicted, 2);
            Assert.Equal(predicted, sink / singleSink, 2);
        }

        // ------------------------------------------------------------ the mass at a vertex

        [Fact]
        public void TheMassAtAVertexIsTheClosedFormForAPlateAndTheLinksMassWhenTheLeverIsAlongTheNormal()
        {
            const double depth = 10;
            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth);

            // A plate turned 30 degrees about z, its vertex (-0.45, 0, +0.45) (the pattern with a minus on x), the
            // normal up. Worked by hand: R c = (-0.45 cos t, -0.45 sin t, 0.45), so the lever to the vertex point, a
            // margin (0.05 m) below it, is r = (-0.45 cos t, -0.45 sin t - 0.05, 0.45), and r x n for n = (0,1,0) is
            // (-rz, 0, rx) = (-0.45, 0, -0.45 cos t). In the plate's own axes, R^T (r x n) = (-0.45 cos t, 0.45 sin t,
            // -0.45 cos t). The box's inertia is m/3 (hy^2 + hz^2), m/3 (hx^2 + hz^2), m/3 (hx^2 + hy^2) with
            // (0.5, 0.05, 0.5), so the inverse-inertia form is
            //   m q = 0.45^2 cos^2 t / ((0.05^2 + 0.5^2) / 3) + 0.45^2 sin^2 t / ((0.5^2 + 0.5^2) / 3)
            //       + 0.45^2 cos^2 t / ((0.5^2 + 0.05^2) / 3)
            // and m_eff = m / (1 + m q).
            double t = Math.PI / 6;
            Creature plate = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0.3, -depth, -0.2), QuatD.FromAxisAngle(Vec3.UnitZ, t));
            Vec3 centre = Vec3.Read(plate.LinkContactCentre, 0);
            double m = plate.Mass[0];

            // The inertia convention: about the link's origin, in the link's own axes, the box's tensor.
            // (The half-extent 0.05 is a float, so the comparison is to 1e-6, not to the last bit.)
            Assert.True(Math.Abs(plate.InertiaLocal[0] - m * (0.05 * 0.05 + 0.5 * 0.5) / 3.0) <= 1e-6 * plate.InertiaLocal[0]);
            Assert.True(Math.Abs(plate.InertiaLocal[1] - m * (0.5 * 0.5 + 0.5 * 0.5) / 3.0) <= 1e-6 * plate.InertiaLocal[1]);
            Assert.True(Math.Abs(plate.InertiaLocal[2] - m * (0.5 * 0.5 + 0.05 * 0.05) / 3.0) <= 1e-6 * plate.InertiaLocal[2]);

            Assert.True(ContactTrueShape.CoreVertex(1, Vec3.Read(plate.LinkCoreHalf, 0), Mat3.Read(plate.LinkContactRotation, 0), centre, out Vec3 vertex));
            Vec3 point = vertex + new Vec3(0, -1, 0) * plate.LinkMargin[0];

            double c2 = Math.Cos(t) * Math.Cos(t), s2 = Math.Sin(t) * Math.Sin(t);
            double mq = 0.45 * 0.45 * c2 / ((0.05 * 0.05 + 0.5 * 0.5) / 3.0)
                      + 0.45 * 0.45 * s2 / ((0.5 * 0.5 + 0.5 * 0.5) / 3.0)
                      + 0.45 * 0.45 * c2 / ((0.5 * 0.5 + 0.05 * 0.05) / 3.0);
            double closedForm = m / (1.0 + mq);

            double effective = MassAt(plate, point, centre, Vec3.UnitY);
            _out.WriteLine($"plate at 30 degrees, vertex (-x, +z): m q = {mq:0.####}, m_eff = {effective / m:0.####} m (closed form {closedForm / m:0.####} m)");
            Assert.True(Math.Abs(effective - closedForm) <= 1e-6 * m, $"m_eff {effective} against the closed form {closedForm}");
            Assert.True(effective < m);

            // Flat: m q = 0.405 / 0.0841667, m_eff = 0.17206 m.
            Creature flat = Part(config, ShapeIds.Box, Plate, 2, new Vec3(0, -depth, 0), QuatD.Identity);
            Vec3 flatCentre = Vec3.Read(flat.LinkContactCentre, 0);
            double flatMq = 2.0 * 0.45 * 0.45 / ((0.05 * 0.05 + 0.5 * 0.5) / 3.0);
            Assert.True(ContactTrueShape.CoreVertex(0, Vec3.Read(flat.LinkCoreHalf, 0), Mat3.Read(flat.LinkContactRotation, 0), flatCentre, out Vec3 flatVertex));
            double flatEffective = MassAt(flat, flatVertex + new Vec3(0, -1, 0) * flat.LinkMargin[0], flatCentre, Vec3.UnitY);
            _out.WriteLine($"flat plate, a corner: m q = {flatMq:0.####}, m_eff = {flatEffective / flat.Mass[0]:0.####} m");
            Assert.True(Math.Abs(flatEffective - flat.Mass[0] / (1.0 + flatMq)) <= 1e-6 * flat.Mass[0]);

            // A lever along the normal: r x n is zero, and the mass is the link's own, to the bit.
            Assert.Equal(
                BitConverter.DoubleToInt64Bits(m),
                BitConverter.DoubleToInt64Bits(MassAt(plate, centre + new Vec3(0, -0.37, 0), centre, Vec3.UnitY)));

            // A sphere's single vertex is the centre, so its vertex point is a margin along the direction to the
            // surface and the lever is along the normal: the mass is the link's own, for the flat bed's up and for a
            // slanted direction, with an isotropic inertia in either.
            Creature sphere = Part(config, ShapeIds.Sphere, new Float3(0.2f, 0.2f, 0.2f), 3, new Vec3(1, -depth, 2), QuatD.FromAxisAngle(new Vec3(0.3, 1, -0.4).Normalized, 0.9));
            Vec3 sphereCentre = Vec3.Read(sphere.LinkContactCentre, 0);
            Assert.Equal(1, ContactTrueShape.CoreVertexCount(Vec3.Read(sphere.LinkCoreHalf, 0)));
            Assert.True(ContactTrueShape.CoreVertex(0, Vec3.Read(sphere.LinkCoreHalf, 0), Mat3.Read(sphere.LinkContactRotation, 0), sphereCentre, out Vec3 sphereVertex));

            foreach (Vec3 direction in new[] { new Vec3(0, -1, 0), new Vec3(0.3, -0.8, 0.2).Normalized })
            {
                Vec3 spherePoint = sphereVertex + direction * sphere.LinkMargin[0];
                double sphereEffective = MassAt(sphere, spherePoint, sphereCentre, -direction);
                _out.WriteLine($"sphere, direction {direction}: m_eff / m - 1 = {sphereEffective / sphere.Mass[0] - 1.0:E2}");
                Assert.True(Math.Abs(sphereEffective - sphere.Mass[0]) <= 1e-12 * sphere.Mass[0]);
            }

            // And through the vertex routine: a sphere's one vertex pushes as the single support point at the link's m.
            Creature ball = Part(config, ShapeIds.Sphere, new Float3(0.2f, 0.2f, 0.2f), 4, new Vec3(0, -depth + 0.2 - 0.003, 0), QuatD.Identity);
            Vec3 ballCentre = Vec3.Read(ball.LinkContactCentre, 0);
            Vec3 single = ContactTrueShape.FlatBedTouch(
                ball, 0, ballCentre, ball.LinkContactRadius[0], ball.LinkMargin[0], Vec3.Zero, ball.Mass[0],
                config, 1000, 100, out _, out _, out _);
            Vec3 viaVertices = ContactTrueShape.FlatBedTouch(
                ball, 0, ballCentre, ball.LinkContactRadius[0], ball.LinkMargin[0], Vec3.Zero, ball.Mass[0],
                config, 1000, 100, out _, out _, out _, atPoint: true);
            Assert.True(single.Y > 0);
            Assert.Equal(BitConverter.DoubleToInt64Bits(single.Y), BitConverter.DoubleToInt64Bits(viaVertices.Y));
        }

        // ------------------------------------------------------------ every vertex of a core

        [Fact]
        public void ACoresDistinctVerticesAreOneForASphereTwoForACapsuleAndFourForAPlate()
        {
            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, 10);
            var turn = QuatD.FromAxisAngle(new Vec3(0.3, 1, -0.4).Normalized, 0.9);
            var at = new Vec3(1, -2, 3);

            (string Shape, Float3 Half, int Count)[] cases =
            {
                (ShapeIds.Sphere, new Float3(0.2f, 0.2f, 0.2f), 1),
                (ShapeIds.Capsule, new Float3(0.1f, 0.4f, 0.1f), 2),
                (ShapeIds.Box, Plate, 4),
            };

            foreach (var c in cases)
            {
                Creature body = Part(config, c.Shape, c.Half, 1, at, turn);
                Vec3 half = Vec3.Read(body.LinkCoreHalf, 0);
                Mat3 rotation = Mat3.Read(body.LinkContactRotation, 0);
                Vec3 centre = Vec3.Read(body.LinkContactCentre, 0);

                Assert.Equal(c.Count, ContactTrueShape.CoreVertexCount(half));

                var seen = new List<Vec3>();
                for (int pattern = 0; pattern < 8; pattern++)
                {
                    if (!ContactTrueShape.CoreVertex(pattern, half, rotation, centre, out Vec3 vertex)) continue;

                    // Each is the core's support point along some direction, and none repeats another.
                    foreach (Vec3 other in seen) Assert.True((other - vertex).Magnitude > 1e-9, $"{c.Shape}: a vertex repeats");
                    seen.Add(vertex);

                    Vec3 local = new Vec3(
                        (pattern & 1) != 0 ? -1 : 1, (pattern & 2) != 0 ? -1 : 1, (pattern & 4) != 0 ? -1 : 1);
                    Assert.Equal(vertex, ConvexCore.Support(half, rotation, centre, rotation * local));
                }

                Assert.Equal(c.Count, seen.Count);
            }
        }

        [Fact]
        public void OneCornerTouchingGivesExactlyTheSingleSupportPointsPushPointAndTorqueBitForBit()
        {
            const double depth = 10;
            const double stiffness = 1000, damping = 100;

            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth);

            // Tipped about two axes, so that one corner is the lowest by a long way.
            QuatD turn = QuatD.FromAxisAngle(new Vec3(1, 0, 0.6).Normalized, 0.6);
            Creature probe = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -depth, 0), turn);
            Vec3 support = ContactTrueShape.CoreSupport(probe, 0, Vec3.Read(probe.LinkContactCentre, 0), new Vec3(0, -1, 0));
            double margin = probe.LinkMargin[0];

            // Sunk 5 mm: the support point's margin sphere is 5 mm into the slab. Lift the plate from where it was placed.
            double lift = (-depth + margin - 0.005) - support.Y;
            Creature plate = Part(config, ShapeIds.Box, Plate, 2, new Vec3(0, -depth + lift, 0), turn);
            Vec3 centre = Vec3.Read(plate.LinkContactCentre, 0);

            Vec3 velocity = new Vec3(0.1, -0.3, 0.05);

            Vec3 single = ContactTrueShape.FlatBedTouch(
                plate, 0, centre, plate.LinkContactRadius[0], margin, velocity, plate.Mass[0],
                config, stiffness, damping, out Vec3 singleSupport, out Vec3 singlePoint, out Vec3 singleMoment);
            Vec3 vertices = ContactTrueShape.FlatBedTouch(
                plate, 0, centre, plate.LinkContactRadius[0], margin, velocity, plate.Mass[0],
                config, stiffness, damping, out Vec3 vertexSupport, out Vec3 vertexPoint, out Vec3 vertexMoment,
                atPoint: true);

            Assert.True(single.Y > 0, "the corner was meant to be in the slab");
            Assert.Equal(Vec3.Zero, singleMoment);

            // What changed with the mass at the vertex: the vertex's push is the single support point's routine called
            // at the mass the corner meets, m_eff, with the spring and damper scaled to it, and not at the link's m.
            double effective = MassAt(plate, singlePoint, centre, Vec3.UnitY);
            double scale = effective / plate.Mass[0];
            Assert.True(scale < 0.9, $"the corner was meant to be lighter than the plate: {scale}");
            Vec3 singleAtEffective = ContactTrueShape.SphereAt(
                ContactTrueShape.KindFlatBed, singleSupport, singlePoint, centre, margin, velocity, Vec3.Zero, false,
                effective, config, stiffness * scale, damping * scale);
            Assert.True(singleAtEffective.Y > 0 && singleAtEffective.Y != single.Y);

            Assert.Equal(BitConverter.DoubleToInt64Bits(singleAtEffective.X), BitConverter.DoubleToInt64Bits(vertices.X));
            Assert.Equal(BitConverter.DoubleToInt64Bits(singleAtEffective.Y), BitConverter.DoubleToInt64Bits(vertices.Y));
            Assert.Equal(BitConverter.DoubleToInt64Bits(singleAtEffective.Z), BitConverter.DoubleToInt64Bits(vertices.Z));
            Assert.Equal(singleSupport, vertexSupport);
            Assert.Equal(singlePoint, vertexPoint);

            // The moment is that one corner's lever times its push.
            Vec3 expected = Vec3.Cross(singlePoint - centre, singleAtEffective);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.X), BitConverter.DoubleToInt64Bits(vertexMoment.X));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Y), BitConverter.DoubleToInt64Bits(vertexMoment.Y));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Z), BitConverter.DoubleToInt64Bits(vertexMoment.Z));
            Assert.True(vertexMoment.Magnitude > 0);

            // The same through the contact pass, at the body's own stiffness and damping and its committed velocity
            // (at rest, so a corner touch reads the same velocity at its point as at the centre).
            double bedStiffness = plate.Mass[0] * config.ContactOmega * config.ContactOmega;
            double bedDamping = 2.0 * config.ContactDampingRatio * plate.Mass[0] * config.ContactOmega;
            Vec3 atRest = Vec3.Read(plate.LinkContactVelocity, 0);

            Vec3 bodyAtMass = ContactTrueShape.FlatBed(
                plate, 0, centre, plate.LinkContactRadius[0], margin, atRest, plate.Mass[0],
                config, bedStiffness, bedDamping, out Vec3 bodySupport, out Vec3 bodyPoint);
            Assert.True(bodyAtMass.Y > 0);
            Vec3 body = ContactTrueShape.SphereAt(
                ContactTrueShape.KindFlatBed, bodySupport, bodyPoint, centre, margin, atRest, Vec3.Zero, false,
                effective, config, bedStiffness * scale, bedDamping * scale);
            Assert.True(body.Y > 0);

            Pass(config, plate);
            Assert.Equal(BitConverter.DoubleToInt64Bits(body.Y), BitConverter.DoubleToInt64Bits(Force(plate).Y));
            Vec3 bodyTorque = Anchor.CapTorque(
                Vec3.Cross(bodyPoint - centre, body), plate.SmallestInertia[0], plate.LinkContactRadius[0], config);
            Assert.Equal(BitConverter.DoubleToInt64Bits(bodyTorque.X), BitConverter.DoubleToInt64Bits(Torque(plate).X));
            Assert.Equal(BitConverter.DoubleToInt64Bits(bodyTorque.Y), BitConverter.DoubleToInt64Bits(Torque(plate).Y));
            Assert.Equal(BitConverter.DoubleToInt64Bits(bodyTorque.Z), BitConverter.DoubleToInt64Bits(Torque(plate).Z));
        }

        [Fact]
        public void APlateHeldFlatAndStillAndSunkOneMillimetreIntoTheBedGetsFourEqualPushesAndNoTorque()
        {
            const double depth = 10;
            const double stiffness = 1000, damping = 100;

            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth);
            double margin = 0.05;
            Creature plate = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -depth + margin - 0.001, 0), QuatD.Identity);
            Vec3 centre = Vec3.Read(plate.LinkContactCentre, 0);
            Vec3 half = Vec3.Read(plate.LinkCoreHalf, 0);
            Mat3 rotation = Mat3.Read(plate.LinkContactRotation, 0);
            Assert.Equal(4, ContactTrueShape.CoreVertexCount(half));

            // One corner at the same depth: every vertex alone, at the mass its corner meets (a flat plate's four
            // corners meet the same one, by the plate's symmetry).
            double one = 0, oneScale = 0;
            int pushing = 0;
            for (int pattern = 0; pattern < 8; pattern++)
            {
                if (!ContactTrueShape.CoreVertex(pattern, half, rotation, centre, out Vec3 vertex)) continue;
                Vec3 vertexPoint = vertex + new Vec3(0, -margin, 0);
                double effective = MassAt(plate, vertexPoint, centre, Vec3.UnitY);
                double scale = effective / plate.Mass[0];
                Vec3 push = ContactTrueShape.SphereAt(
                    ContactTrueShape.KindFlatBed, vertex, vertexPoint, centre, margin,
                    Vec3.Zero, Vec3.Zero, true, effective, config, stiffness * scale, damping * scale);
                Assert.True(push.Y > 0);
                if (pushing++ == 0) { one = push.Y; oneScale = scale; }
                Assert.Equal(one, push.Y, 12);
            }

            Assert.Equal(4, pushing);
            Assert.True(oneScale < 0.9);
            Assert.Equal(stiffness * oneScale * 0.001, one, 9);

            Vec3 total = ContactTrueShape.FlatBedTouch(
                plate, 0, centre, plate.LinkContactRadius[0], margin, Vec3.Zero, plate.Mass[0],
                config, stiffness, damping, out _, out Vec3 point, out Vec3 moment, atPoint: true);

            Assert.Equal(4.0 * one, total.Y, 9);
            Assert.Equal(0.0, total.X);
            Assert.Equal(0.0, total.Z);

            // The weighted point is under the centre, and the four levers cancel.
            Assert.True(Math.Abs(point.X - centre.X) < 1e-12 && Math.Abs(point.Z - centre.Z) < 1e-12, $"the point {point} is not under the centre {centre}");
            Assert.True(moment.Magnitude <= 1e-9 * total.Y * 0.5, $"the torque {moment.Magnitude:E3} is not zero");

            // Through the whole contact pass: the force is the vertices' sum, the torque nothing.
            Pass(config, plate);
            Assert.True(Force(plate).Y > 0);
            Assert.True(Torque(plate).Magnitude <= 1e-9 * Force(plate).Y * 0.5, $"the torque {Torque(plate).Magnitude:E3} is not zero");
        }

        [Fact]
        public void AThinLightLinkPressedOnTheBedAndOnASecondBodysPartSpinsBelowTheCapsSpeedAndStaysFinite()
        {
            const double depth = 5;
            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth, excess: 200, water: true);
            var world = new DynamicsWorld(config);

            // 0.2 x 0.005 x 0.05 half-extents, one part; slightly turned so the corner takes the load.
            var thinHalf = new Float3(0.2f, 0.005f, 0.05f);
            Creature thin = TestBodies.Build(TestBodies.Box(thinHalf), config, 1);
            thin.PlaceAt(new Vec3(0, -depth + 0.004, 0), QuatD.FromAxisAngle(Vec3.UnitZ, 0.05));

            // A cube on the link's top face, off to one side so it loads the link unevenly.
            Creature cube = TestBodies.Build(TestBodies.Box(new Float3(0.1f, 0.1f, 0.1f)), config, 2);
            cube.PlaceAt(new Vec3(0.12, -depth + 0.004 + 0.005 + 0.1 - 0.002, 0), QuatD.Identity);

            world.Add(thin);
            world.Add(cube);

            // What CapTorque bounds: one step of one touch's torque turns the link's rim by MaxContactSpeedChange,
            // so the link's spin changes by at most cap / radius, which is its torque bound over its inertia, times dt.
            double radius = thin.LinkContactRadius[0];
            double capSpeed = config.MaxContactSpeedChange / radius;
            double torqueCap = thin.SmallestInertia[0] * capSpeed / config.StepSeconds;
            Assert.Equal(torqueCap, Anchor.CapTorque(new Vec3(1e9, 0, 0), thin.SmallestInertia[0], radius, config).Magnitude, 9);

            // Two touches act on the link (the bed and the cube), each capped, summed.
            double bound = 2.0 * capSpeed;

            double largest = 0;
            int steps = (int)Math.Round(60.0 / config.StepSeconds);
            for (int step = 0; step < steps; step++)
            {
                world.Step();
                double spin = Vec3.Read(thin.Spin, 0).Magnitude;
                Assert.False(double.IsNaN(spin), $"the link's spin is not a number at step {step}");
                if (spin > largest) largest = spin;
            }

            _out.WriteLine(
                $"thin link: mass {thin.Mass[0]:0.####} kg, smallest inertia {thin.SmallestInertia[0]:E3}, radius {radius:0.####} m; " +
                $"the cap is {capSpeed:0.###} rad/s a step and {torqueCap:0.####} N m; the largest spin in 60 s was {largest:0.####} rad/s " +
                $"(allowed {bound:0.###} for two touches)");

            Assert.Equal(0, world.NonFiniteBodies());
            Assert.True(thin.IsFinite() && cube.IsFinite());
            Assert.True(largest < bound, $"the link spun at {largest} rad/s; the caps allow {bound}");
        }

        // ------------------------------------------------------------ the explicit damper's step factor

        /// <summary>
        /// A plate pressed 5 mm into the flat bed at the given turn, with the recorded constants. Returns, for each
        /// touching vertex j, the explicit damper's step factor on it:
        /// <c>sum_k 2 z w dt m_eff,k (1/m + (r_k x n) . I^-1 (r_j x n))</c> over the touching vertices k, which is the
        /// change of vertex j's normal speed in one step per unit of its approach speed, with the coupling through the
        /// turn included. It is stable below 2.
        /// </summary>
        private static double[] StepFactors(QuatD turn, out int touching)
        {
            const double depth = 10;
            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth);

            Creature probe = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -depth, 0), turn);
            Vec3 probeCentre = Vec3.Read(probe.LinkContactCentre, 0);
            Vec3 half = Vec3.Read(probe.LinkCoreHalf, 0);
            Mat3 rotation = Mat3.Read(probe.LinkContactRotation, 0);
            double margin = probe.LinkMargin[0];

            double lowest = double.MaxValue;
            for (int pattern = 0; pattern < 8; pattern++)
            {
                if (ContactTrueShape.CoreVertex(pattern, half, rotation, probeCentre, out Vec3 v) && v.Y < lowest) lowest = v.Y;
            }

            // The lowest vertex's margin sphere 5 mm into the slab.
            double lift = (-depth + margin - 0.005) - lowest;
            Creature plate = Part(config, ShapeIds.Box, Plate, 2, new Vec3(0, -depth + lift, 0), turn);
            Vec3 centre = Vec3.Read(plate.LinkContactCentre, 0);
            double m = plate.Mass[0];
            Vec3 inertia = Vec3.Read(plate.InertiaLocal, 0);
            Vec3 up = Vec3.UnitY;

            var arm = new List<Vec3>();      // r x n of each touching vertex
            var effective = new List<double>();
            for (int pattern = 0; pattern < 8; pattern++)
            {
                if (!ContactTrueShape.CoreVertex(pattern, half, rotation, centre, out Vec3 vertex)) continue;

                Vec3 point = vertex + new Vec3(0, -1, 0) * margin;
                double meff = MassAt(plate, point, centre, up);
                double scale = meff / m;
                Vec3 push = ContactTrueShape.SphereAt(
                    ContactTrueShape.KindFlatBed, vertex, point, centre, margin, Vec3.Zero, Vec3.Zero, true, meff, config,
                    m * config.ContactOmega * config.ContactOmega * scale,
                    2.0 * config.ContactDampingRatio * m * config.ContactOmega * scale);
                if (!(push.Y > 0)) continue;

                arm.Add(Vec3.Cross(point - centre, up));
                effective.Add(meff);
            }

            touching = arm.Count;

            // I^-1 v = R diag(1/I) R^T v.
            Vec3 Inverse(Vec3 v)
            {
                Vec3 local = rotation.TransposedTimes(v);
                return rotation * new Vec3(local.X / inertia.X, local.Y / inertia.Y, local.Z / inertia.Z);
            }

            double rate = 2.0 * config.ContactDampingRatio * config.ContactOmega * config.StepSeconds;
            var factors = new double[arm.Count];
            for (int j = 0; j < arm.Count; j++)
            {
                Vec3 turned = Inverse(arm[j]);
                double sum = 0;
                for (int k = 0; k < arm.Count; k++) sum += effective[k] * (1.0 / m + Vec3.Dot(arm[k], turned));
                factors[j] = rate * sum;
            }

            return factors;
        }

        private static string StepFactorLine(string label, QuatD turn, out double largest)
        {
            double[] factors = StepFactors(turn, out int touching);
            largest = 0;
            foreach (double f in factors) if (f > largest) largest = f;
            return $"{label}: {touching} vertices touch, step factors {string.Join(", ", Array.ConvertAll(factors, f => f.ToString("0.####")))}, largest {largest:0.####}";
        }

        private static QuatD OnAnEdge => QuatD.FromAxisAngle(Vec3.UnitZ, 25.0 * Math.PI / 180.0);
        private static QuatD OnACorner => QuatD.FromAxisAngle(new Vec3(1, 0, 0.6).Normalized, 0.6);

        [Fact]
        public void TheExplicitDampersStepFactorIsBelowTwoForAPlateOnAnEdgeOnACornerAndFlat()
        {
            double worst = 0;
            foreach ((string label, QuatD turn, int expected) in new[]
            {
                ("on an edge (25 degrees about z)", OnAnEdge, 2),
                ("on a corner", OnACorner, 1),
                ("flat", QuatD.Identity, 4),
            })
            {
                string line = StepFactorLine(label, turn, out double largest);
                _out.WriteLine(line);

                Assert.Equal(expected, StepFactors(turn, out _).Length);
                Assert.True(largest < 2.0, $"{line}: the explicit damper is past its limit of 2");
                if (largest > worst) worst = largest;
            }

            _out.WriteLine($"the largest step factor over the three poses: {worst:0.####} (limit 2) at omega 20, zeta 1, dt 0.01");
            _out.WriteLine(PlateNumbers());
        }

        // ------------------------------------------------------------ the plate's numbers, printed by every plate test

        private static string _plateNumbers;

        /// <summary>
        /// The numbers the plate tests share, for the reviewer: the flat plate's spin and tilt after 5 s and its two
        /// resting depths, the edge-drop's time to lie flat, the energy excess against its allowance, and the damper's
        /// step factors. Computed once.
        /// </summary>
        private static string PlateNumbers()
        {
            if (_plateNumbers != null) return _plateNumbers;

            double sink = FlatPlate(ContactShapeForm.TrueShapeAtPoint, out double spin, out double tilt, out _);
            double singleSink = FlatPlate(ContactShapeForm.TrueShape, out _, out _, out _);
            PlateRun drop = DropPlate(ContactShapeForm.TrueShapeAtPoint);
            StepFactorLine("edge", OnAnEdge, out double edge);
            StepFactorLine("corner", OnACorner, out double corner);
            StepFactorLine("flat", QuatD.Identity, out double flat);

            _plateNumbers =
                $"plate numbers: flat plate largest spin {spin:E3} rad/s, largest tilt {tilt:0.####} degrees (after 5 s); " +
                $"resting depth {sink * 1000:0.####} mm on four vertices, {singleSink * 1000:0.####} mm on the one support point, ratio {sink / singleSink:0.####}; " +
                $"edge-drop: touched {drop.Touched}, off flat last at {drop.FlatSeconds:0.00} s, final tilt {drop.FinalTiltDegrees:0.###} degrees, " +
                $"energy {drop.LargestKinetic:0.####} J, the most above its bound {drop.LargestExcess:E3} J against an allowance of {drop.Slack:E3} J; " +
                $"step factors, largest: edge {edge:0.####}, corner {corner:0.####}, flat {flat:0.####}";
            return _plateNumbers;
        }

        // ------------------------------------------------------------ the thread count

        private static ulong CrowdDigest(ContactShapeForm form, int threads, out long touches, out long surface)
        {
            SolverConfig config = Config(form, depth: 12, excess: 200, water: true);
            config.Reefs = new ReefGeometry(3f, 3f, 2f, 1f, 3f, new[] { 0d }, new[] { 0d });

            var world = new DynamicsWorld(config) { Threads = threads };
            var rng = new Rng(20261026);
            string[] shapes = { ShapeIds.Box, ShapeIds.Capsule, ShapeIds.Sphere };
            double top = config.Reefs.CapTopY(0);

            // Two-link bodies over the reef's cap and the bed, close enough to touch each other.
            for (int i = 0; i < 48; i++)
            {
                Genome genome = TestBodies.Chain(2, i % 2 == 0 ? JointType.Hinge : JointType.Universal, power: 8f + i % 5, hz: 0.5f + 0.1f * (i % 7));
                for (int n = 0; n < genome.Nodes.Count; n++) genome.Nodes[n].ShapeId = shapes[(i + n) % 3];

                Creature body = TestBodies.Build(genome, config, i);
                body.PlaceAt(
                    new Vec3((rng.NextFloat() - 0.5) * 3.0, top + 0.1 + rng.NextFloat() * 1.2, (rng.NextFloat() - 0.5) * 3.0),
                    QuatD.FromAxisAngle(
                        new Vec3(rng.NextFloat() - 0.5, rng.NextFloat() - 0.5, rng.NextFloat() - 0.5),
                        rng.NextFloat() * 6.28));
                world.Add(body);
            }

            touches = 0;
            surface = 0;
            for (int step = 0; step < 500; step++)
            {
                world.Step();

                if (step % 25 == 24)
                {
                    foreach (Creature c in world.Creatures)
                    {
                        touches += c.OverlapCount;
                        if (c.TouchedBedOrGlass) surface++;
                    }
                }
            }

            return world.Digest();
        }

        [Fact]
        public void TwoLinkBodiesOnTheBedAndTheReefGiveTheSameBitsAtOneEightAndTwentyFourThreads()
        {
            ulong one = CrowdDigest(ContactShapeForm.TrueShapeAtPoint, 1, out long touches, out long surface);
            ulong eight = CrowdDigest(ContactShapeForm.TrueShapeAtPoint, 8, out _, out _);
            ulong twentyFour = CrowdDigest(ContactShapeForm.TrueShapeAtPoint, 24, out _, out _);

            // The same world with the force at the centre is another trajectory, so the lever did something.
            ulong centre = CrowdDigest(ContactShapeForm.TrueShape, 1, out _, out _);

            _out.WriteLine($"at the point  1 thread   {one:x16}");
            _out.WriteLine($"at the point  8 threads  {eight:x16}");
            _out.WriteLine($"at the point 24 threads  {twentyFour:x16}");
            _out.WriteLine($"at the centre 1 thread   {centre:x16}");
            _out.WriteLine($"pair touches summed over the samples {touches}; samples with a body on the bed or reef {surface}");

            Assert.Equal(one, eight);
            Assert.Equal(one, twentyFour);
            Assert.NotEqual(one, centre);
            Assert.True(touches > 0, "no pair of parts touched in the sampled steps: the test measures nothing");
            Assert.True(surface > 0, "no body touched the bed or the reef in the sampled steps: the test measures nothing");
        }
    }
}

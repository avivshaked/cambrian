using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D228's contact on the true shape, on the host: what a body carries with the switch on and off, the
    /// pair and the world's surfaces against hand-worked geometry, the exact opposition of a pair's forces,
    /// and the contact point each touch keeps.
    /// </summary>
    public sealed class TrueShapeContactTests
    {
        private static readonly Float3 Plate = new Float3(0.5f, 0.05f, 0.5f);

        private static SolverConfig Config(bool trueShape, double depth = 1e6)
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.CreatureContact = true;
            config.ContactPerPart = true;
            config.ContactShape = trueShape ? ContactShapeForm.TrueShape : ContactShapeForm.Spheres;
            config.WorldDepthMetres = depth;
            return config;
        }

        /// <summary>One part of the named shape.</summary>
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

        // ------------------------------------------------------------ what a body carries

        [Fact]
        public void WithTheSwitchOffNothingIsAllocatedAndWithItOnEveryLinkCarriesATurnACoreAndAMargin()
        {
            Genome chain = TestBodies.Chain(3, JointType.Hinge);

            Creature off = TestBodies.Build(chain, Config(trueShape: false), 1);
            Assert.True(off.LinkContactActive);
            Assert.Null(off.LinkContactRotation);
            Assert.Null(off.LinkCoreHalf);
            Assert.Null(off.LinkMargin);

            Creature on = TestBodies.Build(chain, Config(trueShape: true), 1);
            Assert.Equal(9 * 3, on.LinkContactRotation.Length);
            Assert.Equal(3 * 3, on.LinkCoreHalf.Length);
            Assert.Equal(3, on.LinkMargin.Length);

            // Per-body contact has no link spheres and no turns either.
            SolverConfig body = Config(trueShape: false);
            body.ContactPerPart = false;
            Assert.Null(TestBodies.Build(chain, body, 1).LinkContactRotation);
        }

        [Fact]
        public void TheCommittedTurnIsTheSnapshotOfTheLastCommitForEveryLinkAndTheOneLinkBody()
        {
            SolverConfig config = Config(trueShape: true);

            foreach (Genome genome in new[] { TestBodies.Chain(3, JointType.Hinge), Shaped(ShapeIds.Capsule, new Float3(0.1f, 0.4f, 0.1f)) })
            {
                Creature body = TestBodies.Build(genome, config, 1);
                body.PlaceAt(new Vec3(1, -2, 3), QuatD.FromAxisAngle(new Vec3(0.3, 1, -0.4).Normalized, 0.9));

                for (int k = 0; k < 9 * body.Links; k++)
                {
                    Assert.Equal(body.RotationMatrix[k], body.LinkContactRotation[k]);
                }

                // A new pose, refreshed and not yet committed: the snapshot keeps the old turn.
                double[] before = (double[])body.LinkContactRotation.Clone();
                body.BaseRotation = QuatD.FromAxisAngle(Vec3.UnitZ, 1.7);
                Kinematics.Refresh(body);
                body.RefreshContactSphere();
                Assert.Equal(before, body.LinkContactRotation);

                // Committed: the new one.
                body.CommitContactSphere();
                for (int k = 0; k < 9 * body.Links; k++)
                {
                    Assert.Equal(body.RotationMatrix[k], body.LinkContactRotation[k]);
                }
                Assert.NotEqual(before, body.LinkContactRotation);

                // A restore rebuilds it from the pose.
                Array.Clear(body.LinkContactRotation, 0, body.LinkContactRotation.Length);
                body.RestoreLinkSpheres();
                for (int k = 0; k < 9 * body.Links; k++)
                {
                    Assert.Equal(body.RotationMatrix[k], body.LinkContactRotation[k]);
                }
            }
        }

        [Fact]
        public void AGrowthResizeRewritesTheCoresAndMargins()
        {
            SolverConfig config = Config(trueShape: true);
            Creature body = TestBodies.Build(Shaped(ShapeIds.Box, new Float3(0.5f, 0.1f, 0.3f)), config, 1);

            Assert.Equal(0.1, body.LinkMargin[0], 6);
            Assert.Equal(0.4, body.LinkCoreHalf[0], 6);

            body.Resize(body.Phenotype.Scaled(0.5f));

            Assert.Equal(0.05, body.LinkMargin[0], 6);
            Assert.Equal(0.2, body.LinkCoreHalf[0], 6);
            Assert.Equal(0.0, body.LinkCoreHalf[1], 6);
            Assert.Equal(0.1, body.LinkCoreHalf[2], 6);
        }

        [Fact]
        public void TheSolverRefusesTheTrueShapeWithoutPerPartContact()
        {
            SolverConfig config = Config(trueShape: true);
            config.ContactPerPart = false;

            var e = Assert.Throws<ArgumentException>(() => TestBodies.Build(Shaped(ShapeIds.Box, Plate), config, 1));
            Assert.Contains("ContactShape", e.Message);
            Assert.Contains("ContactPerPart", e.Message);
        }

        // ------------------------------------------------------------ pairs

        [Fact]
        public void AFlatPlateAboveAnotherTouchesItOnlyWhenTheirFacesMeet()
        {
            // Two plates 0.1 thick and a metre across, lying flat, one above the other. Their half-diagonals
            // are 0.7 each, so the spheres' rule touches them from 1.4 m apart.
            foreach (double gap in new[] { 0.15, 0.11 })
            {
                SolverConfig config = Config(trueShape: true);
                Creature low = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -5, 0), QuatD.Identity);
                Creature high = Part(config, ShapeIds.Box, Plate, 2, new Vec3(0.1, -5 + 0.1 + gap, 0.2), QuatD.Identity);
                Pass(config, low, high);
                Assert.Equal(Vec3.Zero, Force(low));
                Assert.Equal(Vec3.Zero, Force(high));
            }

            // Their faces meet and overlap by 0.01: the spring pushes the upper one up with the margins' overlap.
            {
                SolverConfig config = Config(trueShape: true);
                Creature low = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -5, 0), QuatD.Identity);
                Creature high = Part(config, ShapeIds.Box, Plate, 2, new Vec3(0.1, -5 + 0.09, 0.2), QuatD.Identity);
                Pass(config, low, high);

                double reduced = low.Mass[0] * high.Mass[0] / (low.Mass[0] + high.Mass[0]);
                double expected = reduced * config.ContactOmega * config.ContactOmega * 0.01;
                Assert.True(Force(high).Y > 0 && Force(low).Y < 0);
                Assert.Equal(expected, Force(high).Y, 3);
                Assert.Equal(0.0, Force(high).X, 12);
                Assert.Equal(0.0, Force(high).Z, 12);
            }

            // The same pair under the spheres' rule at 0.15: pushed, from a half-diagonal's reach.
            {
                SolverConfig config = Config(trueShape: false);
                Creature low = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -5, 0), QuatD.Identity);
                Creature high = Part(config, ShapeIds.Box, Plate, 2, new Vec3(0.1, -5 + 0.25, 0.2), QuatD.Identity);
                Pass(config, low, high);
                Assert.True(Force(high).Magnitude > 0);
            }
        }

        [Fact]
        public void TwoSpheresTouchWhereTheirOwnRadiiMeetAndNotAtTheirHalfDiagonals()
        {
            // Half-extents 0.2: a sphere of radius 0.2 (the mean), a half-diagonal of 0.3464.
            var h = new Float3(0.2f, 0.2f, 0.2f);

            foreach (double distance in new[] { 0.41, 0.6 })
            {
                SolverConfig config = Config(trueShape: true);
                Creature a = Part(config, ShapeIds.Sphere, h, 1, new Vec3(0, -5, 0), QuatD.Identity);
                Creature b = Part(config, ShapeIds.Sphere, h, 2, new Vec3(distance, -5, 0), QuatD.Identity);
                Pass(config, a, b);
                Assert.Equal(Vec3.Zero, Force(a));
            }

            // Inside the radii by 0.01: the spring, along the line of centres.
            {
                SolverConfig config = Config(trueShape: true);
                Creature a = Part(config, ShapeIds.Sphere, h, 1, new Vec3(0, -5, 0), QuatD.Identity);
                Creature b = Part(config, ShapeIds.Sphere, h, 2, new Vec3(0.39, -5, 0), QuatD.Identity);
                Pass(config, a, b);

                double reduced = a.Mass[0] * b.Mass[0] / (a.Mass[0] + b.Mass[0]);
                double expected = reduced * config.ContactOmega * config.ContactOmega * 0.01;
                Assert.Equal(-expected, Force(a).X, 3);
                Assert.Equal(expected, Force(b).X, 3);
            }

            // The spheres' rule has them pushing at 0.6, inside the half-diagonals' sum of 0.69.
            {
                SolverConfig config = Config(trueShape: false);
                Creature a = Part(config, ShapeIds.Sphere, h, 1, new Vec3(0, -5, 0), QuatD.Identity);
                Creature b = Part(config, ShapeIds.Sphere, h, 2, new Vec3(0.6, -5, 0), QuatD.Identity);
                Pass(config, a, b);
                Assert.True(Force(a).Magnitude > 0);
            }
        }

        [Fact]
        public void ThePairForcesAreExactlyOppositeBitForBitWhicheverBodyHasTheLowerId()
        {
            // Two-link bodies, so neither side's force is spread over a one-link body by m (1/m). Placed so
            // that one link of each reaches one link of the other and the rest are clear.
            foreach ((int idA, int idB) in new[] { (3, 8), (8, 3) })
            {
                SolverConfig config = Config(trueShape: true);
                Genome genome = TestBodies.Chain(2, JointType.Hinge, power: 10f, neurons: false);
                genome.Nodes[0].ShapeId = ShapeIds.Capsule;
                genome.Nodes[1].ShapeId = ShapeIds.Box;

                Creature a = TestBodies.Build(genome, config, idA);
                Creature b = TestBodies.Build(genome, config, idB);
                a.PlaceAt(new Vec3(0, -5, 0), QuatD.FromAxisAngle(new Vec3(0.3, 1.0, -0.2).Normalized, 0.7));
                Vec3 tipA = Vec3.Read(a.Position, 3);

                // B is lowered onto A's second link until the first part of either touches: that is a first
                // touch of one pair of parts, with the other parts clear.
                for (double dy = 1.6; dy > 0 && Force(a, 0).Magnitude == 0 && Force(a, 1).Magnitude == 0; dy -= 0.01)
                {
                    b.PlaceAt(tipA + new Vec3(0.1, dy, 0.2), QuatD.FromAxisAngle(new Vec3(-0.6, 0.2, 1.0).Normalized, 1.9));

                    for (int i = 0; i < a.Links; i++)
                    {
                        Vec3.Write(a.Velocity, 3 * i, new Vec3(0.3, -0.2, 0.1));
                        Vec3.Write(b.Velocity, 3 * i, new Vec3(-0.1, 0.05, 0.4));
                    }
                    a.RefreshContactSphere(); a.CommitContactSphere();
                    b.RefreshContactSphere(); b.CommitContactSphere();

                    Pass(config, a, b);
                }

                // Sum the rows of each body: whichever links touched, the pair's forces are opposite exactly
                // when one pair of links is the only contact.
                int touchedA = -1, touchedB = -1;
                for (int i = 0; i < 2; i++)
                {
                    if (Force(a, i).Magnitude > 0) { Assert.Equal(-1, touchedA); touchedA = i; }
                    if (Force(b, i).Magnitude > 0) { Assert.Equal(-1, touchedB); touchedB = i; }
                }

                Assert.True(touchedA >= 0 && touchedB >= 0, $"ids ({idA}, {idB}): the bodies were meant to touch and did not");

                Vec3 fa = Force(a, touchedA), fb = Force(b, touchedB);
                Assert.Equal(BitConverter.DoubleToInt64Bits(-fa.X), BitConverter.DoubleToInt64Bits(fb.X));
                Assert.Equal(BitConverter.DoubleToInt64Bits(-fa.Y), BitConverter.DoubleToInt64Bits(fb.Y));
                Assert.Equal(BitConverter.DoubleToInt64Bits(-fa.Z), BitConverter.DoubleToInt64Bits(fb.Z));
            }
        }

        [Fact]
        public void TheReportedPairIsTheFirstOneFoundAndOnlyWhenTheTrueShapeTouches()
        {
            SolverConfig config = Config(trueShape: true);
            config.ContactInstrument = true;

            // Within each other's half-diagonals and clear on the true shapes.
            Creature low = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -5, 0), QuatD.Identity);
            Creature near = Part(config, ShapeIds.Box, Plate, 2, new Vec3(0, -4.8, 0), QuatD.Identity);
            Pass(config, low, near);
            Assert.Equal(0, low.OverlapCount);
            Assert.Equal(0, near.OverlapCount);

            // Touching on the true shapes.
            Creature touching = Part(config, ShapeIds.Box, Plate, 3, new Vec3(0, -4.91, 0), QuatD.Identity);
            Pass(config, low, touching);
            Assert.Equal(1, low.OverlapCount);
            Assert.Equal(3, low.OverlapId(0));
            Assert.Equal(0, low.OverlapPart(0));
            Assert.Equal(0, low.OverlapOtherPart(0));
        }

        // ------------------------------------------------------------ the bed and the glass

        [Fact]
        public void APlateOnItsEdgeOnTheFlatBedIsHeldAtItsHalfWidthAndNotItsHalfDiagonal()
        {
            const double depth = 10;
            QuatD edgeOn = QuatD.FromAxisAngle(Vec3.UnitX, Math.PI / 2);   // thin axis horizontal, width vertical

            // 0.01 inside its half-width (0.5): pushed. 0.01 outside it: free, though inside the half-diagonal (0.707).
            SolverConfig config = Config(trueShape: true, depth);
            Creature low = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -depth + 0.49, 0), edgeOn);
            Creature high = Part(config, ShapeIds.Box, Plate, 2, new Vec3(0, -depth + 0.51, 0), edgeOn);
            Pass(config, low);
            Pass(config, high);
            Assert.True(Force(low).Y > 0);
            Assert.Equal(Vec3.Zero, Force(high));

            // The spring on that 0.01 of penetration.
            double k = low.Mass[0] * config.ContactOmega * config.ContactOmega;
            Assert.Equal(k * 0.01, Force(low).Y, 3);

            // The spheres' rule holds the same plate up to 0.7 off the bed.
            SolverConfig spheres = Config(trueShape: false, depth);
            Creature ball = Part(spheres, ShapeIds.Box, Plate, 3, new Vec3(0, -depth + 0.6, 0), edgeOn);
            Pass(spheres, ball);
            Assert.True(Force(ball).Y > 0);
        }

        [Fact]
        public void ACapsuleLyingOnTheBedIsHeldAtItsRadius()
        {
            const double depth = 10;
            QuatD lying = QuatD.FromAxisAngle(Vec3.UnitZ, Math.PI / 2);   // long axis along x

            SolverConfig config = Config(trueShape: true, depth);
            var h = new Float3(0.1f, 0.6f, 0.1f);   // radius 0.1, half-span 0.5
            Creature low = Part(config, ShapeIds.Capsule, h, 1, new Vec3(0, -depth + 0.09, 0), lying);
            Creature high = Part(config, ShapeIds.Capsule, h, 2, new Vec3(0, -depth + 0.11, 0), lying);
            Pass(config, low);
            Pass(config, high);
            Assert.True(Force(low).Y > 0);
            Assert.Equal(Vec3.Zero, Force(high));
            Assert.Equal(low.Mass[0] * config.ContactOmega * config.ContactOmega * 0.01, Force(low).Y, 3);
        }

        [Fact]
        public void ATiltedPlateIsHeldByItsLowestCorner()
        {
            const double depth = 10;
            SolverConfig config = Config(trueShape: true, depth);

            // Turned 30 degrees about z: the lowest point of the core is a corner at x = -0.45, below the
            // centre by 0.45 sin 30 = 0.225 and the margin's 0.05 on top.
            QuatD turn = QuatD.FromAxisAngle(Vec3.UnitZ, Math.PI / 6);
            double hold = 0.45 * Math.Sin(Math.PI / 6) + 0.05 * 1.0;
            Creature in_ = Part(config, ShapeIds.Box, Plate, 1, new Vec3(0, -depth + hold - 0.005, 0), turn);
            Creature out_ = Part(config, ShapeIds.Box, Plate, 2, new Vec3(0, -depth + hold + 0.005, 0), turn);
            Pass(config, in_);
            Pass(config, out_);
            Assert.True(Force(in_).Y > 0);
            Assert.Equal(Vec3.Zero, Force(out_));
        }

        [Fact]
        public void APlateAtTheGlassIsHeldAtItsHalfWidth()
        {
            SolverConfig config = Config(trueShape: true, 100);
            config.TankRadiusMetres = 5;

            // On its edge against the glass at +x: the plate's width (1 m) runs along x, so it reaches 0.5.
            QuatD edgeOn = QuatD.FromAxisAngle(Vec3.UnitX, Math.PI / 2);
            Creature touching = Part(config, ShapeIds.Box, Plate, 1, new Vec3(5 + 5 - 0.49, -5, 5), edgeOn);
            Creature clear = Part(config, ShapeIds.Box, Plate, 2, new Vec3(5 + 5 - 0.51, -5, 5), edgeOn);
            Pass(config, touching);
            Pass(config, clear);

            Assert.True(Force(touching).X < 0);
            Assert.Equal(Vec3.Zero, Force(clear));
            Assert.Equal(-touching.Mass[0] * config.ContactOmega * config.ContactOmega * 0.01, Force(touching).X, 3);
        }

        [Fact]
        public void APlateOnTheReefsCapIsHeldAtItsHalfThickness()
        {
            SolverConfig config = Config(trueShape: true, 12);
            config.FloorIsSolid = true;
            config.Reefs = new ReefGeometry(3f, 3f, 2f, 1f, 3f, new[] { 0d }, new[] { 0d });

            double top = config.Reefs.CapTopY(0);
            Creature in_ = Part(config, ShapeIds.Box, Plate, 1, new Vec3(1, top + 0.04, 0), QuatD.Identity);
            Creature out_ = Part(config, ShapeIds.Box, Plate, 2, new Vec3(1, top + 0.06, 0), QuatD.Identity);
            Pass(config, in_);
            Pass(config, out_);

            Assert.True(Force(in_).Y > 0, "the plate was meant to rest on the cap and was not pushed");
            Assert.Equal(Vec3.Zero, Force(out_));

            // The spheres' rule holds it a half-diagonal up.
            SolverConfig spheres = Config(trueShape: false, 12);
            spheres.FloorIsSolid = true;
            spheres.Reefs = config.Reefs;
            Creature ball = Part(spheres, ShapeIds.Box, Plate, 3, new Vec3(1, top + 0.3, 0), QuatD.Identity);
            Pass(spheres, ball);
            Assert.True(Force(ball).Y > 0);
        }

        [Fact]
        public void APlateOnAGentleHeightMapIsHeldByItsFaceAndASphereByItsOwnRadius()
        {
            SolverConfig config = Config(trueShape: true, 45);
            config.Bed = new BedShape(26f, 45f, 0f, 20f, 0f, Rng.SeedFor(5UL, World.BedShapeIndex));

            // Where the bed is, under a point on the tank's axis.
            double x = 26, z = 26;
            config.Bed.HeightAndGradient(x, z, out double height, out double gx, out double gz);
            double floor = -45 + height;
            double slope = Math.Sqrt(1 + gx * gx + gz * gz);

            // A sphere of radius 0.2 (half-diagonal 0.35): held at 0.2 off the surface, the perpendicular distance.
            var ball = new Float3(0.2f, 0.2f, 0.2f);
            Creature in_ = Part(config, ShapeIds.Sphere, ball, 1, new Vec3(x, floor + 0.19 * slope, z), QuatD.Identity);
            Creature out_ = Part(config, ShapeIds.Sphere, ball, 2, new Vec3(x, floor + 0.21 * slope, z), QuatD.Identity);
            Pass(config, in_);
            Pass(config, out_);
            Assert.True(Force(in_).Magnitude > 0);
            Assert.Equal(Vec3.Zero, Force(out_));

            // The push runs along the bed's normal.
            Vec3 normal = new Vec3(-gx, 1, -gz) * (1.0 / slope);
            Vec3 push = Force(in_);
            Assert.Equal(1.0, Vec3.Dot(push, normal) / push.Magnitude, 9);
        }

        // ------------------------------------------------------------ the contact point each touch keeps

        [Fact]
        public void TheContactPointOfAPairLiesOnTheTouchingSurfaces()
        {
            // The pair routine at a penetration of 1e-7, so that the two surfaces and their midpoint are within
            // 1e-6 of one another.
            const double eps = 1e-7;
            Vec3 half = new Vec3(0.45, 0, 0.45);

            // Two flat plates: the point on the plane between them.
            Assert.True(ContactTrueShape.Pair(
                new Vec3(0, 0, 0), Mat3.Identity, half, 0.05, 0.7,
                new Vec3(0.1, 0.1 - eps, 0.2), Mat3.Identity, half, 0.05, 0.7,
                out Vec3 normal, out double penetration, out Vec3 point));
            Assert.Equal(eps, penetration, 9);
            Assert.Equal(-1.0, normal.Y, 9);   // from B (above) toward A (below)... A is below B
            Assert.Equal(0.05, point.Y, 6);
            Assert.InRange(point.X, 0.1 - 0.45, 0.45);

            // Two spheres: on the line of centres at the touching surface.
            Assert.True(ContactTrueShape.Pair(
                new Vec3(0, 0, 0), Mat3.Identity, Vec3.Zero, 0.3, 0.52,
                new Vec3(0.5 - eps, 0, 0), Mat3.Identity, Vec3.Zero, 0.2, 0.35,
                out normal, out penetration, out point));
            Assert.Equal(-1.0, normal.X, 9);
            Assert.Equal(0.3, point.X, 6);
            Assert.Equal(0.0, point.Y, 9);

            // A capsule against a sphere, mid-segment: the sphere's surface.
            Assert.True(ContactTrueShape.Pair(
                new Vec3(0, 0, 0), Mat3.Identity, new Vec3(0, 0.5, 0), 0.1, 0.62,
                new Vec3(0.35 - eps, 0.2, 0), Mat3.Identity, Vec3.Zero, 0.25, 0.43,
                out normal, out penetration, out point));
            Assert.Equal(0.1, point.X, 6);
            Assert.Equal(0.2, point.Y, 6);

            // Cores that meet: the point between the centres at the ratio of the margins, and a push of at
            // least the margins' sum.
            Assert.True(ContactTrueShape.Pair(
                new Vec3(0, 0, 0), Mat3.Identity, half, 0.05, 0.7,
                new Vec3(0.2, 0, 0.1), Mat3.Identity, half, 0.15, 0.8,
                out normal, out penetration, out point));
            Assert.True(penetration >= 0.2 - 1e-12);
            Assert.Equal(0.2 * (0.05 / 0.2), point.X, 9);
        }

        [Fact]
        public void TheContactPointOfEachSurfaceLiesOnTheSurfaceItTouches()
        {
            const double eps = 1e-7;
            const double depth = 10;
            QuatD edgeOn = QuatD.FromAxisAngle(Vec3.UnitX, Math.PI / 2);

            // The flat bed under a plate on its edge: the point is on the slab's top face.
            SolverConfig config = Config(trueShape: true, depth);
            Creature plate = Part(config, ShapeIds.Box, Plate, 1, new Vec3(2, -depth + 0.5 - eps, 1), edgeOn);
            Vec3 centre = Vec3.Read(plate.LinkContactCentre, 0);
            Vec3 push = ContactTrueShape.FlatBed(
                plate, 0, centre, plate.LinkContactRadius[0], plate.LinkMargin[0], Vec3.Zero, plate.Mass[0],
                config, 1000, 0, out Vec3 support, out Vec3 point);
            Assert.True(push.Y > 0);
            Assert.Equal(-depth, point.Y, 6);
            Assert.Equal(-depth + 0.05 - eps, support.Y, 6);

            // The glass under a plate on its edge: the point is on the cylinder.
            config.TankRadiusMetres = 5;
            Creature atGlass = Part(config, ShapeIds.Box, Plate, 2, new Vec3(10 - 0.5 + eps, -5, 5), edgeOn);
            centre = Vec3.Read(atGlass.LinkContactCentre, 0);
            push = ContactTrueShape.Glass(
                atGlass, 0, centre, atGlass.LinkContactRadius[0], atGlass.LinkMargin[0], Vec3.Zero, atGlass.Mass[0],
                config, 1000, 0, out _, out point);
            Assert.True(push.X < 0);
            Assert.Equal(config.TankRadiusMetres, Math.Sqrt(
                (point.X - config.TankAxisX) * (point.X - config.TankAxisX) +
                (point.Z - config.TankAxisZ) * (point.Z - config.TankAxisZ)), 6);

            // The reef under a plate lying on the cap: the point is on the table.
            SolverConfig reefs = Config(trueShape: true, 12);
            reefs.FloorIsSolid = true;
            reefs.Reefs = new ReefGeometry(3f, 3f, 2f, 1f, 3f, new[] { 0d }, new[] { 0d });
            double top = reefs.Reefs.CapTopY(0);
            Creature onCap = Part(reefs, ShapeIds.Box, Plate, 3, new Vec3(1, top + 0.05 - eps, 0), QuatD.Identity);
            centre = Vec3.Read(onCap.LinkContactCentre, 0);
            push = ContactTrueShape.Reef(
                onCap, 0, centre, onCap.LinkContactRadius[0], onCap.LinkMargin[0], Vec3.Zero, onCap.Mass[0],
                reefs, 1000, 0, out _, out point);
            Assert.True(push.Y > 0);
            Assert.Equal(top, point.Y, 6);
        }
    }
}

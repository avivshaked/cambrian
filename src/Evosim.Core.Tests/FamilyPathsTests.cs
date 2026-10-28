using System;
using System.Collections.Generic;
using System.Text;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D255, step 1: the paths that used to read a family part (D233) as its box. The mouth point, the
    /// silhouette's hull and the self-overlap box now follow the true surface, and the buoyancy arm's point is
    /// checked against the true body.
    /// </summary>
    public class FamilyPathsTests
    {
        private readonly ITestOutputHelper _output;

        public FamilyPathsTests(ITestOutputHelper output) => _output = output;

        private static readonly PartShapeRegistry Registry = new PartShapeRegistry(
            new BoxShape(), new SphereShape(), new CapsuleShape(), new SuperellipsoidShape());

        // The fin bench's paddle and stick (src/Evosim.SwimProbe/FinBench.cs), copied: the test projects do not
        // reference SwimProbe.
        private static readonly Float3 Paddle = new Float3(0.2f, 0.01f, 0.2f);
        private static readonly Float3 Stick = new Float3(0.2f, (float)Math.Sqrt(0.002), (float)Math.Sqrt(0.002));
        private static readonly Float3 Fat = new Float3(0.2f, 0.45f, 0.15f);

        /// <summary>The eight gene corners and the three nearest named members (box, sphere, capsule).</summary>
        internal static List<(string Name, PartForm Form)> BenchForms()
        {
            var forms = new List<(string, PartForm)>();
            foreach (float profile in new[] { 0.1f, 1f })
            {
                foreach (float section in new[] { 0.1f, 2f })
                {
                    foreach (float taper in new[] { 0.25f, 4f })
                    {
                        forms.Add(($"profile {profile} section {section} taper {taper}", new PartForm(profile, section, taper)));
                    }
                }
            }

            forms.Add(("box-like", new PartForm(0.1f, 0.1f, 1f)));
            forms.Add(("sphere-like", new PartForm(1f, 1f, 1f)));
            forms.Add(("capsule-like", new PartForm(0.5f, 1f, 1f)));
            return forms;
        }

        private static readonly Float3[] Halves = { Paddle, Stick, Fat };

        // A box root and one hinged family child hung by the given anchor and turned by the given orientation.
        private static Phenotype RootAndFamilyChild(
            PartForm form, Float3 dimensions, Float3 childAnchor, Quat? orientation = null)
        {
            var genome = new Genome();
            MorphNode root = Fixtures.Box(0.5f);
            MorphNode limb = Fixtures.Box(0.25f, JointType.Hinge);
            limb.ShapeId = ShapeIds.Superellipsoid;
            limb.Form = form;
            limb.Dimensions = dimensions;

            MorphEdge edge = Fixtures.FaceToFace(1);
            edge.ChildAnchor = childAnchor;
            if (orientation.HasValue) edge.Orientation = orientation.Value;
            root.Edges.Add(edge);

            genome.Nodes.Add(root);
            genome.Nodes.Add(limb);
            genome.RootIndex = 0;

            Phenotype body = Developer.Develop(genome, shapes: Registry);
            Assert.Equal(2, body.PartCount);
            Assert.NotNull(body.Parts[1].Family);
            return body;
        }

        private static Phenotype SingleFamilyPart(PartForm form, Float3 dimensions)
        {
            var genome = new Genome();
            MorphNode root = Fixtures.Box(0.5f);
            root.ShapeId = ShapeIds.Superellipsoid;
            root.Form = form;
            root.Dimensions = dimensions;
            genome.Nodes.Add(root);
            genome.RootIndex = 0;

            Phenotype body = Developer.Develop(genome, shapes: Registry);
            Assert.Equal(1, body.PartCount);
            return body;
        }

        // ---------------------------------------------------------------- the mouth point

        [Fact]
        public void TheAxisReachIsOnTheSurfaceForEveryBenchFormAndEveryEnd()
        {
            double worst = 0.0;
            string worstAt = "";

            foreach ((string name, PartForm form) in BenchForms())
            {
                var geometry = new Superellipsoid(form);
                foreach (Float3 h in Halves)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        foreach (bool negative in new[] { false, true })
                        {
                            float reach = geometry.AxisReach(h, axis, negative);

                            // A hair inside (two parts in ten million): the inside function is infinite beyond a
                            // pole, and a float point at the pole itself can round to either side of it.
                            float inner = reach * (1f - 2e-7f);
                            var p = new Float3(axis == 0 ? inner : 0f, axis == 1 ? inner : 0f, axis == 2 ? inner : 0f);
                            double inside = geometry.InsideFunction(p, h);
                            double error = Math.Abs(inside - 1.0);

                            Assert.False(double.IsInfinity(inside), $"{name}, {h}, axis {axis}, negative {negative}: the point is beyond the end");
                            Assert.True(error <= 1e-5, $"{name}, {h}, axis {axis}, negative {negative}: F = {inside}");
                            Assert.True(negative ? reach < 0f : reach > 0f, "the sign follows the end");

                            if (error > worst)
                            {
                                worst = error;
                                worstAt = $"{name}, {h}, axis {axis}, negative {negative}";
                            }
                        }
                    }
                }
            }

            _output.WriteLine($"worst |F - 1| over the bench forms: {worst:E2} at {worstAt}");
        }

        [Fact]
        public void TheAxisReachOfATaperedPartIsNotTheBoxFacesCentre()
        {
            // Taper 4 moves the centre of mass toward the wide end, so the +Y pole is nearer the origin than b and
            // the -Y pole farther.
            var form = new PartForm(0.6f, 1f, 4f);
            var geometry = new Superellipsoid(form);
            Float3 h = Fat;
            float shift = geometry.CentreOffset(h);

            Assert.True(shift > 0.01f);
            Assert.Equal(h.Y - shift, geometry.AxisReach(h, 1, false), 5);
            Assert.Equal(-(h.Y + shift), geometry.AxisReach(h, 1, true), 5);
            Assert.Throws<ArgumentOutOfRangeException>(() => geometry.AxisReach(h, 3, false));
        }

        [Fact]
        public void AFamilyPartsMouthIsOnItsTrueSurfaceAlongTheAxisTheDirectionPointsThrough()
        {
            var form = new PartForm(0.6f, 1f, 4f);
            Float3 h = Fat;

            // Hung by its -Y end: the mouth looks along +Y, to the far pole.
            Phenotype alongY = RootAndFamilyChild(form, h, new Float3(0f, -1f, 0f));
            Float3 mouthY = MouthPoint.Local(alongY, 1);
            Superellipsoid geometry = alongY.Parts[1].Family.Geometry;
            Assert.Equal(0f, mouthY.X);
            Assert.Equal(0f, mouthY.Z);
            Assert.Equal(geometry.AxisReach(h, 1, false), mouthY.Y);
            Assert.Equal(1.0, geometry.InsideFunction(mouthY * (1f - 2e-7f), h), 4);   // a hair inside: float rounding at a pole
            Assert.True(mouthY.Y < h.Y, "the box's face would stand " + (h.Y - mouthY.Y) + " m beyond the pole");

            // Hung by its -X end: the mouth looks along +X, to the slice at the origin's height.
            Phenotype alongX = RootAndFamilyChild(form, h, new Float3(-1f, 0f, 0f));
            Float3 mouthX = MouthPoint.Local(alongX, 1);
            Assert.Equal(geometry.AxisReach(h, 0, false), mouthX.X);
            Assert.Equal(0f, mouthX.Y);
            Assert.Equal(1.0, geometry.InsideFunction(mouthX * (1f - 2e-7f), h), 4);   // a hair inside: float rounding at a pole

            // Hung by its +Z end: the mouth looks along -Z.
            Phenotype alongZ = RootAndFamilyChild(form, h, new Float3(0f, 0f, 1f));
            Float3 mouthZ = MouthPoint.Local(alongZ, 1);
            Assert.Equal(geometry.AxisReach(h, 2, true), mouthZ.Z);
            Assert.Equal(1.0, geometry.InsideFunction(mouthZ * (1f - 2e-7f), h), 4);   // a hair inside: float rounding at a pole
        }

        [Fact]
        public void AFamilyPartsMouthTakesTheBoxsAxisRuleAndANamedPartIsUnchanged()
        {
            // A tilted anchor: the direction is (0.6, 0.8, 0) normalised after the sign, so Y is the larger and
            // the mouth is on Y. A tie goes to the lower axis.
            Phenotype tilted = RootAndFamilyChild(new PartForm(0.6f, 1f, 1.5f), Fat, new Float3(-0.6f, -0.8f, 0f));
            Float3 mouth = MouthPoint.Local(tilted, 1);
            Assert.Equal(0f, mouth.X);
            Assert.True(mouth.Y > 0f);
            Assert.Equal(0f, mouth.Z);

            // A box child is the face centre, to the bit, as it was.
            var genome = new Genome();
            MorphNode root = Fixtures.Box(0.5f);
            MorphNode limb = Fixtures.Box(0.25f, JointType.Hinge);
            limb.Dimensions = Fat;
            MorphEdge edge = Fixtures.FaceToFace(1);
            edge.ChildAnchor = new Float3(0f, -1f, 0f);
            root.Edges.Add(edge);
            genome.Nodes.Add(root);
            genome.Nodes.Add(limb);
            genome.RootIndex = 0;
            Phenotype box = Developer.Develop(genome);
            Float3 face = MouthPoint.Local(box, 1);
            Assert.Equal(new Float3(0f, Fat.Y, 0f), face);
        }

        // ---------------------------------------------------------------- the silhouette

        [Fact]
        public void AFamilyPartAppendsItsNinetyEightSurfacePointsPosedByItsRotation()
        {
            // A part turned a third of a turn about a skew axis, so that the pose is not the identity.
            Quat turn = Quat.FromAxisAngle(new Float3(1f, 2f, 3f).Normalized, 2.1f);
            Phenotype body = RootAndFamilyChild(new PartForm(0.6f, 1f, 4f), Fat, new Float3(-1f, 0f, 0f), turn);
            PhenotypePart part = body.Parts[1];

            var points = new List<Double3>();
            ConvexHull.AppendPartCorners(part, points);
            Assert.Equal(Superellipsoid.ContactCorePoints, points.Count);

            double worst = 0.0;
            for (int i = 0; i < points.Count; i++)
            {
                Float3 world = new Float3((float)points[i].X, (float)points[i].Y, (float)points[i].Z);
                Float3 local = part.Rotation.Conjugate.Rotate(world - part.Position) * (1f - 1e-6f);   // a hair inside, as above
                double inside = part.Family.Geometry.InsideFunction(local, part.HalfExtents);
                worst = Math.Max(worst, Math.Abs(inside - 1.0));
            }

            _output.WriteLine($"98 points of a taper-4 part, posed: worst |F - 1| = {worst:E2}");
            Assert.True(worst < 1e-3, $"worst |F - 1| = {worst}");
        }

        [Fact]
        public void AFamilyPartsHullLiesInsideItsBoxsHullAndTheBoxLikeMemberIsCloseToTheBoxes()
        {
            foreach ((string name, PartForm form) in BenchForms())
            {
                Phenotype body = SingleFamilyPart(form, Fat);
                PhenotypePart part = body.Parts[0];

                var family = new List<Double3>();
                ConvexHull.AppendPartCorners(part, family);
                double familyArea = ConvexHull.SurfaceArea(family, out bool fellBack, out _);
                Assert.False(fellBack, name);

                // The box's corners at the same half-extents and centre, for the comparison.
                var corners = new List<Double3>();
                Float3 h = part.HalfExtents;
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            corners.Add(new Double3(part.Position.X + h.X * sx, part.Position.Y + h.Y * sy, part.Position.Z + h.Z * sz));
                        }
                    }
                }

                double boxArea = ConvexHull.SurfaceArea(corners, out _, out _);
                _output.WriteLine($"{name}: hull of the true surface points {familyArea:0.#####} m2, the box's {boxArea:0.#####} m2, ratio {familyArea / boxArea:0.####}");

                // Every named form but a strong taper sits inside the box, and so does its hull.
                if (form.Taper == 1f) Assert.True(familyArea <= boxArea * (1.0 + 1e-9), name);
            }
        }

        // ---------------------------------------------------------------- the self-overlap box

        [Fact]
        public void AFamilyPartsOverlapBoxHoldsEverySurfacePointAndIsCentredOnTheTrueBody()
        {
            Quat turn = Quat.FromAxisAngle(new Float3(1f, 2f, 3f).Normalized, 0.9f);
            double worstOutside = 0.0;

            foreach ((string name, PartForm form) in BenchForms())
            {
                foreach (Float3 h in Halves)
                {
                    Phenotype body = RootAndFamilyChild(form, h, new Float3(-1f, 0f, 0f), turn);
                    PhenotypePart part = body.Parts[1];
                    BoxOverlap.Obb box = BoxOverlap.Obb.From(part);

                    Superellipsoid geometry = part.Family.Geometry;
                    foreach (Double3 local in geometry.SurfaceLattice(part.HalfExtents, 24))
                    {
                        // The point in the world frame, then in the box's axes.
                        Float3 world = part.Position + part.Rotation.Rotate(
                            new Float3((float)local.X, (float)local.Y, (float)local.Z));
                        Float3 d = world - box.C;
                        double outside = Math.Max(
                            Math.Abs(Float3.Dot(d, box.U0)) - box.E.X,
                            Math.Max(Math.Abs(Float3.Dot(d, box.U1)) - box.E.Y, Math.Abs(Float3.Dot(d, box.U2)) - box.E.Z));
                        worstOutside = Math.Max(worstOutside, outside);
                        Assert.True(outside <= 1e-5, $"{name}, {h}: a surface point stands {outside} m outside the box");
                    }

                    // The centre is the untapered solid's, CentreOffset from the origin toward -Y.
                    Float3 expected = part.Position + part.Rotation.Rotate(new Float3(0f, -geometry.CentreOffset(part.HalfExtents), 0f));
                    Assert.Equal(expected, box.C);
                }
            }

            _output.WriteLine($"the box holds every lattice point; the worst stands {worstOutside:E2} m beyond it");
        }

        [Fact]
        public void ABoxPartsOverlapBoxIsUnchanged()
        {
            Phenotype body = Developer.Develop(Fixtures.SingleBox());
            PhenotypePart part = body.Parts[0];
            BoxOverlap.Obb box = BoxOverlap.Obb.From(part);

            Assert.Equal(part.Position, box.C);
            Assert.Equal(Float3.Abs(part.HalfExtents), box.E);
        }

        // ---------------------------------------------------------------- the reach

        [Fact]
        public void TheSolidReachHoldsEveryPointOfTheNinetyEightPointLatticeForEveryBenchForm()
        {
            double tightest = double.MaxValue;
            string tightestAt = "";

            foreach ((string name, PartForm form) in BenchForms())
            {
                foreach (Float3 h in Halves)
                {
                    Phenotype body = SingleFamilyPart(form, h);
                    PhenotypePart part = body.Parts[0];
                    FamilyGeometry family = part.Family;
                    double reach = family.ReferenceSolidReach;
                    Assert.Equal(family.Geometry.Reach(part.HalfExtents), reach);

                    double farthest = 0.0;
                    for (int k = 0; k < Superellipsoid.ContactCorePoints; k++)
                    {
                        Double3 p = family.Geometry.SurfacePointAndNormal(Superellipsoid.ContactCoreCube(k), part.HalfExtents, out _);
                        farthest = Math.Max(farthest, p.Length);
                    }

                    foreach (Double3 p in family.Geometry.SurfaceLattice(part.HalfExtents, 24)) farthest = Math.Max(farthest, p.Length);

                    Assert.True(farthest <= reach * (1.0 + 1e-9), $"{name}, {h}: a surface point stands {farthest}, the reach is {reach}");
                    if (reach / farthest < tightest)
                    {
                        tightest = reach / farthest;
                        tightestAt = $"{name}, {h}";
                    }
                }
            }

            _output.WriteLine($"the reach holds every point; its slackest-to-tightest ratio over the farthest point is down to {tightest:0.####} at {tightestAt}");
        }

        [Fact]
        public void ADeveloperPrunesAFamilyPartByItsReachAndNotByTheBoxsHalfDiagonal()
        {
            var form = new PartForm(1f, 1f, 4f);
            Float3 h = Fat;
            PartForm none = default;

            Phenotype open = RootAndFamilyChild(form, h, new Float3(-1f, 0f, 0f));
            PhenotypePart child = open.Parts[1];
            double centre = child.Position.Magnitude;
            double diagonal = h.Magnitude;
            double reach = child.Family.ReferenceSolidReach;
            Assert.True(reach > diagonal, $"reach {reach} against the half diagonal {diagonal}");

            // A bound between the two: the old half diagonal let the part in, the reach does not.
            var limits = DevelopmentLimits.Default.Clone();
            limits.MaxBodyReachMetres = (float)(centre + 0.5 * (diagonal + reach));

            var genome = new Genome();
            MorphNode root = Fixtures.Box(0.5f);
            MorphNode limb = Fixtures.Box(0.25f, JointType.Hinge);
            limb.ShapeId = ShapeIds.Superellipsoid;
            limb.Form = form;
            limb.Dimensions = h;
            root.Edges.Add(Fixtures.FaceToFace(1));
            genome.Nodes.Add(root);
            genome.Nodes.Add(limb);
            genome.RootIndex = 0;

            Phenotype bounded = Developer.Develop(genome, limits, shapes: Registry);
            Assert.Equal(1, bounded.PartCount);
            Assert.Equal(1, bounded.PrunedForReach);

            // The same limit lets a box of the same half-extents in.
            limb.ShapeId = ShapeIds.Box;
            limb.Form = none;
            Assert.Equal(2, Developer.Develop(genome, limits, shapes: Registry).PartCount);
        }
    }
}

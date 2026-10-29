using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D233's contact on the family's true shape: a family part's core is the hull of its 98 contact-core points,
    /// found by GJK through the hull's support function; the surfaces touch it at its eight corner points and, when
    /// none pushes, at its support point; and the broadphase sphere holds every point of core plus margin and of the
    /// solid. A link of a named shape takes none of these paths.
    /// </summary>
    public sealed class FamilyContactTests
    {
        private readonly ITestOutputHelper _out;

        public FamilyContactTests(ITestOutputHelper output) => _out = output;

        private static readonly PartShapeRegistry Family = new PartShapeRegistry(
            new BoxShape(), new SphereShape(), new CapsuleShape(), new SuperellipsoidShape());

        private static readonly Vec3 Down = new Vec3(0, -1, 0);

        /// <summary>
        /// The recorded step and contact constants, as <c>TrueShapeAtPointTests</c> sets them: a step of 0.01 s,
        /// <c>ContactOmega</c> 20, <c>ContactDampingRatio</c> 1, <c>MaxSeparationSpeed</c> 1 and
        /// <c>MaxContactSpeedChange</c> 1.
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

        private static Genome FamilyGenome(PartForm form, Float3 half)
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits
            {
                BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f,
            };

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = half,
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Superellipsoid,
                Form = form,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            return genome;
        }

        private static Phenotype Adult(PartForm form, Float3 half) =>
            Developer.Develop(FamilyGenome(form, half), shapes: Family);

        private static Creature FamilyBody(SolverConfig config, PartForm form, Float3 half, int id, Vec3 at, QuatD turn)
        {
            var creature = new Creature(id, Adult(form, half), config, Family);
            creature.PlaceAt(at, turn);
            return creature;
        }

        private static Creature BoxBody(SolverConfig config, Float3 half, int id, Vec3 at, QuatD turn)
        {
            Creature creature = TestBodies.Build(TestBodies.Box(half), config, id);
            creature.PlaceAt(at, turn);
            return creature;
        }

        /// <summary>The pair routine on two bodies' first links, A at the origin and B at <paramref name="b"/>, both unturned.</summary>
        private static bool PairOf(
            Creature a, Creature other, Vec3 b, out Vec3 normal, out double penetration, out Vec3 point) =>
            ContactTrueShape.Pair(
                Vec3.Zero, Mat3.Identity, Vec3.Read(a.LinkCoreHalf, 0), ContactTrueShape.HullOf(a, 0), a.LinkMargin[0], a.LinkContactRadius[0],
                b, Mat3.Identity, Vec3.Read(other.LinkCoreHalf, 0), ContactTrueShape.HullOf(other, 0), other.LinkMargin[0], other.LinkContactRadius[0],
                out normal, out penetration, out point);

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        private static void SameBits(Vec3 expected, Vec3 actual, string what)
        {
            Assert.True(
                Bits(expected.X) == Bits(actual.X) && Bits(expected.Y) == Bits(actual.Y) && Bits(expected.Z) == Bits(actual.Z),
                $"{what}: expected {expected}, got {actual}");
        }

        private static Mat3 RandomTurn(Rng rng)
        {
            Vec3 axis = new Vec3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f)).Normalized;
            return QuatD.FromAxisAngle(axis, rng.Range(0f, 6.28f)).ToMatrix();
        }

        /// <summary>A hull of a box's 8 corners, 6 face centres and centre: 15 points, the box as a hull.</summary>
        private static double[] BoxHull(Vec3 half)
        {
            var points = new List<Vec3>();
            for (int k = 0; k < 8; k++)
            {
                points.Add(new Vec3(
                    (k & 1) == 0 ? half.X : -half.X,
                    (k & 2) == 0 ? half.Y : -half.Y,
                    (k & 4) == 0 ? half.Z : -half.Z));
            }

            points.Add(new Vec3(half.X, 0, 0)); points.Add(new Vec3(-half.X, 0, 0));
            points.Add(new Vec3(0, half.Y, 0)); points.Add(new Vec3(0, -half.Y, 0));
            points.Add(new Vec3(0, 0, half.Z)); points.Add(new Vec3(0, 0, -half.Z));
            points.Add(Vec3.Zero);

            var hull = new double[3 * points.Count];
            for (int i = 0; i < points.Count; i++) Vec3.Write(hull, 3 * i, points[i]);
            return hull;
        }

        // ------------------------------------------------------------ the support function

        [Fact]
        public void ASupportWithNoHullGivesTheBoxOverloadsBitsOverRandomInputs()
        {
            var rng = new Rng(233_01);

            for (int n = 0; n < 2000; n++)
            {
                Vec3 half = new Vec3(
                    rng.Chance(0.3f) ? 0 : rng.Range(0.01f, 1f),
                    rng.Chance(0.3f) ? 0 : rng.Range(0.01f, 1f),
                    rng.Chance(0.3f) ? 0 : rng.Range(0.01f, 1f));
                Mat3 turn = RandomTurn(rng);
                Vec3 centre = new Vec3(rng.Range(-2f, 2f), rng.Range(-2f, 2f), rng.Range(-2f, 2f));
                Vec3 direction = new Vec3(
                    rng.Chance(0.1f) ? 0 : rng.Range(-1f, 1f),
                    rng.Chance(0.1f) ? 0 : rng.Range(-1f, 1f),
                    rng.Chance(0.1f) ? 0 : rng.Range(-1f, 1f));

                SameBits(
                    ConvexCore.Support(half, turn, centre, direction),
                    ConvexCore.Support(half, null, turn, centre, direction),
                    $"support {n}");
            }
        }

        [Fact]
        public void ADistanceWithNoHullsGivesTheBoxOverloadsBitsOverRandomInputs()
        {
            var rng = new Rng(233_02);

            for (int n = 0; n < 1000; n++)
            {
                Vec3 halfA = new Vec3(rng.Range(0f, 0.7f), rng.Chance(0.3f) ? 0 : rng.Range(0f, 0.7f), rng.Range(0f, 0.7f));
                Vec3 halfB = new Vec3(rng.Range(0f, 0.7f), rng.Range(0f, 0.7f), rng.Chance(0.3f) ? 0 : rng.Range(0f, 0.7f));
                Mat3 turnA = RandomTurn(rng), turnB = RandomTurn(rng);
                Vec3 centreB = new Vec3(rng.Range(-1.5f, 1.5f), rng.Range(-1.5f, 1.5f), rng.Range(-1.5f, 1.5f));

                bool old = ConvexCore.Distance(
                    halfA, turnA, Vec3.Zero, halfB, turnB, centreB, out double d0, out Vec3 pa0, out Vec3 pb0);
                bool viaNulls = ConvexCore.Distance(
                    halfA, null, turnA, Vec3.Zero, halfB, null, turnB, centreB, out double d1, out Vec3 pa1, out Vec3 pb1);

                Assert.Equal(old, viaNulls);
                Assert.Equal(Bits(d0), Bits(d1));
                SameBits(pa0, pa1, $"pair {n} point A");
                SameBits(pb0, pb1, $"pair {n} point B");
            }
        }

        [Fact]
        public void AHullsSupportIsTheLargestDotProductAndTheFirstPointWinsATie()
        {
            var rng = new Rng(233_03);

            for (int n = 0; n < 300; n++)
            {
                var hull = new double[3 * Superellipsoid.ContactCorePoints];
                for (int k = 0; k < hull.Length; k++) hull[k] = rng.Range(-1f, 1f);

                Mat3 turn = RandomTurn(rng);
                Vec3 centre = new Vec3(rng.Range(-2f, 2f), rng.Range(-2f, 2f), rng.Range(-2f, 2f));
                Vec3 direction = new Vec3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f));

                double best = double.NegativeInfinity;
                for (int i = 0; i < hull.Length; i += 3)
                {
                    best = Math.Max(best, Vec3.Dot(direction, turn * new Vec3(hull[i], hull[i + 1], hull[i + 2])));
                }

                Vec3 support = ConvexCore.Support(Vec3.Zero, hull, turn, centre, direction);
                Assert.Equal(best, Vec3.Dot(direction, support - centre), 11);
            }

            // Two points with the same dot product along +x: the first of the array wins, whichever it is.
            var a = new Vec3(1, 0, 0);
            var b = new Vec3(1, 5, 0);
            var dir = new Vec3(1, 0, 0);
            var ab = new double[] { a.X, a.Y, a.Z, b.X, b.Y, b.Z };
            var ba = new double[] { b.X, b.Y, b.Z, a.X, a.Y, a.Z };
            SameBits(new Vec3(1, 0, 0), ConvexCore.Support(Vec3.Zero, ab, Mat3.Identity, Vec3.Zero, dir), "tie, a first");
            SameBits(new Vec3(1, 5, 0), ConvexCore.Support(Vec3.Zero, ba, Mat3.Identity, Vec3.Zero, dir), "tie, b first");

            // A direction of zero reads the first point.
            SameBits(new Vec3(1, 5, 0), ConvexCore.Support(Vec3.Zero, ba, Mat3.Identity, Vec3.Zero, Vec3.Zero), "zero direction");
        }

        [Fact]
        public void GjkOnHullsOfBoxesGivesTheBoxPathsDistanceAndEachNearestPointLiesInItsHull()
        {
            var rng = new Rng(233_04);
            int apart = 0, meeting = 0;

            for (int n = 0; n < 600; n++)
            {
                Vec3 halfA = new Vec3(rng.Range(0.05f, 0.7f), rng.Range(0.05f, 0.7f), rng.Chance(0.3f) ? 0 : rng.Range(0.05f, 0.7f));
                Vec3 halfB = new Vec3(rng.Chance(0.3f) ? 0 : rng.Range(0.05f, 0.7f), rng.Range(0.05f, 0.7f), rng.Range(0.05f, 0.7f));
                Mat3 turnA = RandomTurn(rng), turnB = RandomTurn(rng);
                Vec3 direction = new Vec3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f)).Normalized;
                Vec3 centreB = direction * (0.25 + 2.0 * rng.NextFloat());

                bool boxApart = ConvexCore.Distance(
                    halfA, turnA, Vec3.Zero, halfB, turnB, centreB, out double boxDistance, out _, out _);
                bool hullApart = ConvexCore.Distance(
                    Vec3.Zero, BoxHull(halfA), turnA, Vec3.Zero, Vec3.Zero, BoxHull(halfB), turnB, centreB,
                    out double hullDistance, out Vec3 pa, out Vec3 pb);

                if (boxApart && hullApart)
                {
                    Assert.True(Math.Abs(boxDistance - hullDistance) <= 1e-9, $"pair {n}: box {boxDistance:R}, hull {hullDistance:R}");
                    Assert.Equal(hullDistance, (pa - pb).Magnitude, 9);
                    apart++;
                }
                else if (!boxApart && !hullApart)
                {
                    meeting++;
                }
                else
                {
                    // One routine says apart and the other says they meet: only a distance at the touching tolerance may.
                    Assert.True(
                        Math.Max(boxDistance, hullDistance) <= 1e-6,
                        $"pair {n}: the box path says {(boxApart ? "apart" : "meeting")} ({boxDistance:R}), the hull path {(hullApart ? "apart" : "meeting")} ({hullDistance:R})");
                }
            }

            _out.WriteLine($"{apart} pairs apart and {meeting} meeting");
            Assert.True(apart > 100 && meeting > 20, $"the draw gave {apart} apart and {meeting} meeting");
        }

        // ------------------------------------------------------------ what a body carries

        [Fact]
        public void AFamilyLinkCarriesAHullAndZeroHalfExtentsAndANamedLinkCarriesNoHullAndTheSpheresWorldCarriesNoArray()
        {
            var form = new PartForm(0.6f, 1f, 1.5f);
            var half = new Float3(0.2f, 0.45f, 0.15f);

            Creature family = FamilyBody(Config(ContactShapeForm.TrueShape), form, half, 1, Vec3.Zero, QuatD.Identity);
            Assert.NotNull(family.LinkCoreHull);
            Assert.Single(family.LinkCoreHull);
            Assert.Equal(3 * Superellipsoid.ContactCorePoints, family.LinkCoreHull[0].Length);
            Assert.Equal(Vec3.Zero, Vec3.Read(family.LinkCoreHalf, 0));
            Assert.True(family.LinkMargin[0] > 0 && family.LinkMargin[0] <= 0.15f + 1e-9);

            // The hull is the geometry's, at the part's size (the part is the reference: a scale of 1).
            PhenotypePart part = family.Phenotype.Parts[0];
            Assert.Equal(1f, part.FamilyScale);
            for (int k = 0; k < family.LinkCoreHull[0].Length; k++)
            {
                Assert.Equal(Bits(part.Family.ReferenceCorePoints[k]), Bits(family.LinkCoreHull[0][k]));
            }

            // A chain of boxes has the array with a null for each link; a world on the spheres has no array.
            Creature chain = TestBodies.Build(TestBodies.Chain(3, JointType.Hinge), Config(ContactShapeForm.TrueShape), 2);
            Assert.NotNull(chain.LinkCoreHull);
            Assert.Equal(3, chain.LinkCoreHull.Length);
            Assert.All(chain.LinkCoreHull, h => Assert.Null(h));

            Creature spheres = TestBodies.Build(TestBodies.Chain(3, JointType.Hinge), Config(ContactShapeForm.Spheres), 3);
            Assert.Null(spheres.LinkCoreHull);
            Assert.Null(spheres.LinkCoreHalf);

            // The three named shapes' cores are built as ever; the family's is refused by Of, which is for those three.
            Assert.Throws<ArgumentException>(() => ConvexCore.Of(new SuperellipsoidShape(), half, out _, out _));
        }

        // ------------------------------------------------------------ the pair test

        [Fact]
        public void TwoFamilyBallsPenetrateByTheirRadiiLessTheirDistanceAsTwoSpheresDo()
        {
            SolverConfig config = Config(ContactShapeForm.TrueShape);
            var ball = new PartForm(1f, 1f, 1f);
            var half = new Float3(0.2f, 0.2f, 0.2f);
            double r = half.X;

            Creature a = FamilyBody(config, ball, half, 1, Vec3.Zero, QuatD.Identity);
            Creature b = FamilyBody(config, ball, half, 2, Vec3.Zero, QuatD.Identity);

            // The even ball's margin is 0.98 of its radius (Superellipsoid.ContactMarginBackoff) and its hull lies in the
            // small ball left at its centre, to the sampling's error: the core is the ball eroded by the margin, read off
            // its sampled supports, which reach a little short between samples. Core and margin together are the ball.
            double tolerance = 2e-3 * r;
            double margin = Superellipsoid.ContactMarginBackoff * r;
            Assert.True(Math.Abs(margin - a.LinkMargin[0]) <= tolerance, $"the margin {a.LinkMargin[0]} is not 0.98 of the radius {r}");
            foreach (double v in a.LinkCoreHull[0]) Assert.True(Math.Abs(v) <= r - margin + tolerance);

            foreach (double d in new[] { 0.05, 0.2, 0.3, 0.39 })
            {
                Assert.True(PairOf(a, b, new Vec3(d, 0, 0), out Vec3 normal, out double penetration, out Vec3 point), $"d = {d}");
                Assert.True(Math.Abs(2.0 * r - d - penetration) <= 2.0 * tolerance, $"d = {d}: penetration {penetration}");

                // The normal runs from B's core toward A's; the contact point is halfway between the two surfaces.
                Assert.True((normal - new Vec3(-1, 0, 0)).Magnitude <= 5e-3, $"d = {d}: normal {normal}");
                Assert.True(Math.Abs(0.5 * d - point.X) <= 2.0 * tolerance, $"d = {d}: point {point}");
            }

            // Apart by more than their radii: no push.
            Assert.False(PairOf(a, b, new Vec3(0.41, 0, 0), out _, out double apart, out _));
            Assert.True(apart <= 0);

            // Turned, along a diagonal: the same.
            Vec3 diagonal = new Vec3(1, 2, -2).Normalized * 0.3;
            Assert.True(PairOf(a, b, diagonal, out Vec3 slanted, out double slantedPenetration, out _));
            Assert.True(Math.Abs(2.0 * r - 0.3 - slantedPenetration) <= 2.0 * tolerance, $"diagonal: penetration {slantedPenetration}");
            Assert.True((slanted + diagonal.Normalized).Magnitude < 5e-3, $"diagonal: normal {slanted}");
        }

        [Fact]
        public void ABoxLimitFamilyPartAndABoxOfTheSameHalfExtentsPressedFaceOnIntoTheSameBoxAgree()
        {
            SolverConfig config = Config(ContactShapeForm.TrueShape);
            var half = new Float3(0.15f, 0.1f, 0.12f);

            // The least boxy form in the ranges: both exponents at their smallest, taper 1. The geometry's own box
            // limit (0.001) is outside the shape's ranges and cannot be a part.
            var boxy = new PartForm(PartForm.ProfileMin, PartForm.SectionMin, 1f);

            Creature wall = BoxBody(config, new Float3(0.4f, 0.1f, 0.4f), 1, Vec3.Zero, QuatD.Identity);
            Creature box = BoxBody(config, half, 2, Vec3.Zero, QuatD.Identity);
            Creature part = FamilyBody(config, boxy, half, 3, Vec3.Zero, QuatD.Identity);

            double boxMargin = box.LinkMargin[0];
            _out.WriteLine(
                $"box margin {boxMargin:0.####} m, family margin {part.LinkMargin[0]:0.######} m " +
                $"(bulge {part.Phenotype.Parts[0].Family.ReferenceCoreBulge:E2} m, shortfall {part.Phenotype.Parts[0].Family.ReferenceCoreShortfall:E2} m)");

            // The wall's top is at y = 0.1 and the part's half height is 0.1, so a centre at 0.2 less the press is a
            // press into the wall. Sideways offsets keep the faces overlapping.
            foreach (double press in new[] { 0.002, 0.005, 0.02 })
            {
                var at = new Vec3(0.03, 0.2 - press, -0.05);

                Assert.True(PairOf(wall, box, at, out Vec3 boxNormal, out double boxPenetration, out _), $"box, press {press}");
                Assert.True(PairOf(wall, part, at, out Vec3 partNormal, out double partPenetration, out _), $"family, press {press}");

                _out.WriteLine(
                    $"press {press}: box penetration {boxPenetration:0.######}, family {partPenetration:0.######}; " +
                    $"normals {boxNormal} and {partNormal}");

                // Within 2% of the margin (the box's, which is the larger of the two).
                Assert.True(Math.Abs(boxPenetration - partPenetration) <= 0.02 * boxMargin, $"press {press}: penetrations {boxPenetration} and {partPenetration}");
                Assert.True((boxNormal - partNormal).Magnitude <= 0.02, $"press {press}: normals {boxNormal} and {partNormal}");
            }
        }

        // ------------------------------------------------------------ the broadphase sphere

        private static IEnumerable<PartForm> Forms()
        {
            foreach (object[] row in SuperellipsoidFormsRows())
            {
                yield return new PartForm((float)row[0], (float)row[1], (float)row[2]);
            }
        }

        // The forms of the Core shape tests, copied: that class is in another test assembly.
        private static IEnumerable<object[]> SuperellipsoidFormsRows()
        {
            yield return new object[] { 1f, 1f, 1f };
            yield return new object[] { 0.1f, 0.1f, 1f };
            yield return new object[] { 0.3f, 1.7f, 3f };
            yield return new object[] { 0.7f, 0.4f, 0.25f };
            yield return new object[] { 1f, 2f, 4f };
            yield return new object[] { 0.1f, 2f, 0.25f };
            yield return new object[] { 0.5f, 1f, 1.6f };
        }

        [Fact]
        public void TheLinkReachHoldsEveryCorePointPlusMarginAndEverySurfacePointOfTheSolidAtThreeSizesAndTheHullIsRewrittenInPlace()
        {
            SolverConfig config = Config(ContactShapeForm.TrueShape);
            var half = new Float3(0.3f, 0.7f, 0.2f);
            double worstSlack = double.PositiveInfinity;
            string worstForm = null;

            foreach (PartForm form in Forms())
            {
                Phenotype adult = Adult(form, half);
                var creature = new Creature(1, adult, config, Family);
                var geometry = new Superellipsoid(form);

                // Built before the resizes, so that a resize has a cache to refresh.
                double builtReach = creature.LinkReach[0];
                Assert.True(builtReach > 0);

                double[] hull = creature.LinkCoreHull[0];

                foreach (float s in new[] { 0.4f, 0.8f, 1.25f })
                {
                    Phenotype grown = adult.Scaled(s, Family);
                    creature.Resize(grown);
                    PhenotypePart part = grown.Parts[0];

                    // Rewritten in place: the same array, holding the reference times the scale.
                    Assert.Same(hull, creature.LinkCoreHull[0]);
                    double scale = part.FamilyScale;
                    for (int k = 0; k < hull.Length; k++)
                    {
                        Assert.Equal(Bits(part.Family.ReferenceCorePoints[k] * scale), Bits(hull[k]));
                    }
                    Assert.Equal(Bits(part.Family.ReferenceCoreMargin * scale), Bits(creature.LinkMargin[0]));

                    double reach = creature.LinkReach[0];
                    double margin = creature.LinkMargin[0];

                    // Every core point plus the margin.
                    for (int i = 0; i < hull.Length; i += 3)
                    {
                        double length = new Vec3(hull[i], hull[i + 1], hull[i + 2]).Magnitude + margin;
                        Assert.True(length <= reach * (1.0 + 1e-12), $"{form} at {s}: core point {i / 3} plus margin is {length}, over the reach {reach}");
                        if (reach - length < worstSlack) { worstSlack = reach - length; worstForm = form + " at " + s; }
                    }

                    // Every point of the solid, on a grid of each cube face.
                    for (int axis = 0; axis < 3; axis++)
                    {
                        for (int side = 0; side < 2; side++)
                        {
                            for (int p = 0; p <= 16; p++)
                            {
                                for (int q = 0; q <= 16; q++)
                                {
                                    double sign = side == 0 ? 1.0 : -1.0;
                                    double u = -1.0 + 2.0 * p / 16, v = -1.0 + 2.0 * q / 16;
                                    Double3 cube = axis == 0 ? new Double3(sign, u, v) : axis == 1 ? new Double3(u, sign, v) : new Double3(u, v, sign);

                                    Double3 surface = geometry.SurfacePointAndNormal(cube, part.HalfExtents, out _);
                                    Assert.True(
                                        surface.Length <= reach * (1.0 + 1e-12),
                                        $"{form} at {s}: a surface point {surface} is {surface.Length} from the origin, over the reach {reach}");
                                    if (reach - surface.Length < worstSlack) { worstSlack = reach - surface.Length; worstForm = form + " at " + s; }
                                }
                            }
                        }
                    }

                    // A body built at that size is the grown body to the bit: its hull, its margin, its reach.
                    var fresh = new Creature(2, grown, config, Family);
                    for (int k = 0; k < hull.Length; k++) Assert.Equal(Bits(hull[k]), Bits(fresh.LinkCoreHull[0][k]));
                    Assert.Equal(Bits(creature.LinkMargin[0]), Bits(fresh.LinkMargin[0]));
                    Assert.Equal(Bits(reach), Bits(fresh.LinkReach[0]));
                }
            }

            // The reach is always a bound; this is how tight the tightest case is.
            _out.WriteLine($"the least slack between a reach and the farthest point it holds: {worstSlack:E3} m, at {worstForm}");
        }

        [Fact]
        public void ANamedPartsReachIsStillItsHalfDiagonalAndAFamilyPartInASpheresWorldTakesTheSolidsReach()
        {
            var half = new Float3(0.2f, 0.45f, 0.15f);
            double diagonal = Math.Sqrt((double)half.X * half.X + (double)half.Y * half.Y + (double)half.Z * half.Z);

            Creature named = BoxBody(Config(ContactShapeForm.TrueShape), half, 1, Vec3.Zero, QuatD.Identity);
            Assert.Equal(Bits(diagonal), Bits(named.LinkReach[0]));

            // D255. A world on the spheres has no hull core, but its family part's sphere still holds the part: the
            // solid's reach about its origin, which the half-diagonal does not.
            Phenotype adult = Adult(new PartForm(0.6f, 1f, 1.5f), half);
            var spheres = new Creature(2, adult, Config(ContactShapeForm.Spheres), Family);
            Assert.Equal(Bits(adult.Parts[0].Family.ReferenceSolidReach), Bits(spheres.LinkReach[0]));
            Assert.True(spheres.LinkReach[0] > diagonal);
        }

        // ------------------------------------------------------------ the surfaces

        private static SolverConfig PlateWorld(out double depth)
        {
            depth = 5;
            return Config(ContactShapeForm.TrueShapeAtPoint, depth, excess: 200, water: true);
        }

        [Fact]
        public void AFlatFamilyPlateSetDownFlatOnTheBedStaysStillAndTouchesAtFourCorners()
        {
            SolverConfig config = PlateWorld(out double depth);
            var world = new DynamicsWorld(config);

            var form = new PartForm(0.1f, 0.1f, 1f);
            var half = new Float3(0.2f, 0.02f, 0.15f);
            Creature plate = FamilyBody(config, form, half, 1, new Vec3(0, -depth + 0.02, 0), QuatD.Identity);
            world.Add(plate);

            double largestSpin = 0, largestTilt = 0;
            int steps = (int)Math.Round(10.0 / config.StepSeconds);
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

            plate.EnsureContactCommitted();
            Assert.Equal(0, world.NonFiniteBodies());

            // Which of the distinct corner points are in the bed: those whose margin sphere reaches below its top. A
            // flat core's top and bottom corners stand 4% of the margin apart and are merged, the deeper one staying
            // (HullCornerYields), as a box's core with no thickness gives each vertex once.
            double[] hull = ContactTrueShape.HullOf(plate, 0);
            Mat3 rotation = Mat3.Read(plate.LinkContactRotation, 0);
            Vec3 centre = Vec3.Read(plate.LinkContactCentre, 0);
            double margin = plate.LinkMargin[0];

            var touching = new List<int>();
            for (int pattern = 0; pattern < 8; pattern++)
            {
                if (ContactTrueShape.HullCornerYields(hull, pattern, Superellipsoid.ContactCornerMerge * margin, rotation, centre, Down)) continue;

                Vec3 corner = ContactTrueShape.HullCorner(hull, pattern, rotation, centre);
                if (corner.Y - margin < -depth) touching.Add(pattern);
            }

            _out.WriteLine(
                $"family plate after 5 s: largest spin {largestSpin:E3} rad/s, largest tilt {largestTilt:0.####} degrees; " +
                $"margin {margin:0.######} m; corners in the bed: {string.Join(", ", touching)}");

            Assert.True(largestSpin < 0.01, $"the resting plate spun at {largestSpin} rad/s");
            Assert.True(largestTilt < 0.5, $"the resting plate rocked to {largestTilt} degrees");

            // Four corners, one from each merged pair: every sign of x and z once (bits 0 and 2), so the four springs
            // stand at the plate's four rim corners as the box's do. Which of a pair stays is the deeper, and on this
            // rounded plate a pair can stand at one depth, so which y it carries is not asserted.
            Assert.Equal(4, touching.Count);
            var rims = new HashSet<int>();
            foreach (int pattern in touching) rims.Add(pattern & 5);
            Assert.Equal(4, rims.Count);
        }

        [Fact]
        public void AnEggRestingOnTheBedPushesAsOneSupportPoint()
        {
            // The coordinator's ruling of 2026-10-25: on the eroded core an egg's corner points crowd its lowest point,
            // so several may reach the slab at once. Those within ContactCornerMerge of the margin of a lower one are
            // one corner, and what is left must push as the single support point would, not several times as hard.
            const double depth = 10;
            const double stiffness = 1000, damping = 100;

            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth);
            var form = new PartForm(1f, 1f, 1.5f);
            var half = new Float3(0.15f, 0.2f, 0.12f);

            // Lowered until the support point's margin sphere is 3 mm into the slab, as the vertex tests do.
            Creature probe = FamilyBody(config, form, half, 1, new Vec3(0, -depth, 0), QuatD.Identity);
            Vec3 probeSupport = ContactTrueShape.CoreSupport(probe, 0, Vec3.Read(probe.LinkContactCentre, 0), Down);
            double margin = probe.LinkMargin[0];
            double lift = (-depth + margin - 0.003) - probeSupport.Y;

            Creature egg = FamilyBody(config, form, half, 2, new Vec3(0, -depth + lift, 0), QuatD.Identity);
            Vec3 centre = Vec3.Read(egg.LinkContactCentre, 0);
            Mat3 rotation = Mat3.Read(egg.LinkContactRotation, 0);
            double[] hull = ContactTrueShape.HullOf(egg, 0);

            int distinct = 0, inSlab = 0;
            for (int pattern = 0; pattern < 8; pattern++)
            {
                if (ContactTrueShape.HullCornerYields(hull, pattern, Superellipsoid.ContactCornerMerge * margin, rotation, centre, Down)) continue;
                distinct++;
                if (ContactTrueShape.HullCorner(hull, pattern, rotation, centre).Y - margin < -depth) inSlab++;
            }

            Vec3 pushed = ContactTrueShape.FlatBedTouch(
                egg, 0, centre, egg.LinkContactRadius[0], margin, Vec3.Zero, egg.Mass[0],
                config, stiffness, damping, out _, out Vec3 point, out _, atPoint: true);

            // The single support point's push, at the mass that point meets.
            Vec3 support = ContactTrueShape.CoreSupport(egg, 0, centre, Down);
            Vec3 supportPoint = support + Down * margin;
            double effective = ContactTrueShape.EffectiveMass(
                supportPoint - centre, Vec3.UnitY, Vec3.Read(egg.InertiaLocal, 0), rotation, egg.Mass[0]);
            double scale = effective / egg.Mass[0];
            Vec3 single = ContactTrueShape.SphereAt(
                ContactTrueShape.KindFlatBed, support, supportPoint, centre, margin, Vec3.Zero, Vec3.Zero, false,
                effective, config, stiffness * scale, damping * scale);

            double ratio = pushed.Y / single.Y;
            _out.WriteLine(
                $"egg: margin {margin:0.######} m, {distinct} distinct corners, {inSlab} in the slab; push {pushed.Y:E4} N " +
                $"against the single support point's {single.Y:E4} N, ratio {ratio:0.####}; contact point {point}");

            Assert.True(single.Y > 0, "the support point was meant to be in the slab");
            Assert.True(Math.Abs(ratio - 1.0) <= 0.05, $"the egg pushes {ratio} times as hard as its one support point");
            Assert.True(Math.Abs(point.Y - supportPoint.Y) <= 0.02 * margin, $"the contact point {point} is not at the support point {supportPoint}");
        }

        [Theory]
        [InlineData(0.3, 0.0, 0.4, 1)]
        [InlineData(0.0, 0.0, 0.3, 2)]
        public void ATiltedBoxyPartOnOneCornerOrTwoKeepsItsCornerTouchesAndNeverSwitches(
            double turnX, double turnY, double turnZ, int corners)
        {
            // The coordinator's ruling of 2026-10-25: the support point takes the deepest corner's place only when it
            // stands clearly deeper. A tilted box-like part's support point is its deepest corner, so its touch is the
            // corners' own, on one corner or rocking on two, and the count of springs is the corners'.
            const double depth = 10;
            const double stiffness = 1000, damping = 100;

            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth);
            var boxy = new PartForm(PartForm.ProfileMin, PartForm.SectionMin, 1f);
            var half = new Float3(0.15f, 0.1f, 0.12f);
            QuatD turn = QuatD.FromEuler(turnX, turnY, turnZ);

            // Lowered until the lowest point's margin sphere is 3 mm into the slab.
            Creature probe = FamilyBody(config, boxy, half, 1, new Vec3(0, -depth, 0), turn);
            Vec3 probeSupport = ContactTrueShape.CoreSupport(probe, 0, Vec3.Read(probe.LinkContactCentre, 0), Down);
            double margin = probe.LinkMargin[0];
            double lift = (-depth + margin - 0.003) - probeSupport.Y;

            Creature part = FamilyBody(config, boxy, half, 2, new Vec3(0, -depth + lift, 0), turn);
            Vec3 centre = Vec3.Read(part.LinkContactCentre, 0);
            Mat3 rotation = Mat3.Read(part.LinkContactRotation, 0);
            double[] hull = ContactTrueShape.HullOf(part, 0);
            Vec3 inertia = Vec3.Read(part.InertiaLocal, 0);
            double mass = part.Mass[0];

            // The corners' own touch, as the rule reads it with no switch: each corner that stays and pushes, summed in
            // pattern order.
            Vec3 expected = Vec3.Zero;
            int pushing = 0;
            double deepestCorner = double.NegativeInfinity;
            for (int pattern = 0; pattern < 8; pattern++)
            {
                if (ContactTrueShape.HullCornerYields(hull, pattern, Superellipsoid.ContactCornerMerge * margin, rotation, centre, Down)) continue;

                Vec3 vertex = ContactTrueShape.HullCorner(hull, pattern, rotation, centre);
                Vec3 vertexPoint = vertex + Down * margin;
                double effective = ContactTrueShape.EffectiveMass(vertexPoint - centre, Vec3.UnitY, inertia, rotation, mass);
                double scale = effective / mass;
                Vec3 push = ContactTrueShape.SphereAt(
                    ContactTrueShape.KindFlatBed, vertex, vertexPoint, centre, margin, Vec3.Zero, Vec3.Zero, true,
                    effective, config, stiffness * scale, damping * scale);
                if (!(push.Magnitude > 0)) continue;

                pushing++;
                expected += push;
                deepestCorner = Math.Max(deepestCorner, Vec3.Dot(vertex, Down));
            }

            Vec3 support = ContactTrueShape.CoreSupport(part, 0, centre, Down);
            double above = Vec3.Dot(support, Down) - deepestCorner;

            Vec3 pushed = ContactTrueShape.FlatBedTouch(
                part, 0, centre, part.LinkContactRadius[0], margin, Vec3.Zero, mass,
                config, stiffness, damping, out _, out _, out _, atPoint: true);

            _out.WriteLine(
                $"turn ({turnX}, {turnY}, {turnZ}): {pushing} corners push; the support point stands {above:E3} m below the deepest " +
                $"corner (the switch is {Superellipsoid.ContactSupportSwitch * margin:E3} m); push {pushed} against the corners' {expected}");

            Assert.Equal(corners, pushing);
            Assert.True(above <= Superellipsoid.ContactSupportSwitch * margin, $"the support point stands {above} below the deepest corner");
            Assert.True(
                (pushed - expected).Magnitude <= 1e-12 * expected.Magnitude,
                $"the touch {pushed} is not the corners' own {expected}");
        }

        public static IEnumerable<object[]> DescentCases()
        {
            yield return new object[] { 1f, 1f, 1.5f, 0.0, 0.0, 0.0 };
            yield return new object[] { 1f, 1f, 1.5f, 0.0, 0.0, 0.785 };
            yield return new object[] { 1f, 1f, 1.5f, 0.6, 0.0, 0.3 };
            yield return new object[] { 0.5f, 1f, 3f, 0.0, 0.0, 0.785 };
            yield return new object[] { 0.5f, 1f, 3f, 0.0, 0.0, 3.1416 };
            yield return new object[] { 1f, 0.4f, 4f, 0.0, 0.0, 1.2 };
            yield return new object[] { PartForm.ProfileMin, PartForm.SectionMin, 1f, 0.3, 0.0, 0.4 };
        }

        [Fact]
        public void LoweredOntoTheBedAFamilyLinksTouchAlwaysReachesItsLowestPoint()
        {
            // The coordinator's rulings of 2026-10-25. At every height that pushes, the deepest contact point is the
            // support point to within the switch: the corners when one of them is that low, the support point in the
            // deepest corner's place when it stands clearly deeper, or alone when no corner pushes. The touch is the
            // rule's own (its push equals the reconstruction here), and the descents, over several forms and turns,
            // see both a height on corners alone and a height switched, so the test cannot pass on one kind only.
            const double depth = 10;
            const double stiffness = 1000, damping = 100;
            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth);
            var half = new Float3(0.15f, 0.2f, 0.12f);

            int cornerOnly = 0, switched = 0, alone = 0, id = 1;
            foreach (object[] c in DescentCases())
            {
                var form = new PartForm((float)c[0], (float)c[1], (float)c[2]);
                QuatD turn = QuatD.FromEuler((double)c[3], (double)c[4], (double)c[5]);

                Creature body = FamilyBody(config, form, half, id++, new Vec3(0, -depth, 0), turn);
                double margin = body.LinkMargin[0];
                double threshold = Superellipsoid.ContactSupportSwitch * margin;
                double merge = Superellipsoid.ContactCornerMerge * margin;
                Vec3 lowestAtStart = ContactTrueShape.CoreSupport(body, 0, Vec3.Read(body.LinkContactCentre, 0), Down);

                // From the lowest point's sphere 1 mm above the slab to 40 mm into it, in steps of 0.25 mm.
                double start = (-depth + margin + 0.001) - lowestAtStart.Y;
                int caseCorner = 0, caseSwitched = 0;
                for (int step = 0; step <= 164; step++)
                {
                    body.PlaceAt(new Vec3(0, -depth + start - 0.00025 * step, 0), turn);
                    Vec3 centre = Vec3.Read(body.LinkContactCentre, 0);
                    Mat3 rotation = Mat3.Read(body.LinkContactRotation, 0);
                    double[] hull = ContactTrueShape.HullOf(body, 0);
                    Vec3 inertia = Vec3.Read(body.InertiaLocal, 0);
                    double mass = body.Mass[0];

                    Vec3 pushed = ContactTrueShape.FlatBedTouch(
                        body, 0, centre, body.LinkContactRadius[0], margin, Vec3.Zero, mass,
                        config, stiffness, damping, out _, out _, out _, atPoint: true);

                    // The rule's touch, rebuilt: the corners that stay and push, the deepest of them, and the support point.
                    Vec3 corners = Vec3.Zero, deepestPush = Vec3.Zero;
                    double deepestCorner = double.NegativeInfinity;
                    for (int pattern = 0; pattern < 8; pattern++)
                    {
                        if (ContactTrueShape.HullCornerYields(hull, pattern, merge, rotation, centre, Down)) continue;

                        Vec3 vertex = ContactTrueShape.HullCorner(hull, pattern, rotation, centre);
                        Vec3 push = PushAt(vertex, centre, margin, inertia, rotation, mass, config, stiffness, damping);
                        if (!(push.Magnitude > 0)) continue;

                        corners += push;
                        double cornerDepth = Vec3.Dot(vertex, Down);
                        if (cornerDepth > deepestCorner)
                        {
                            deepestCorner = cornerDepth;
                            deepestPush = push;
                        }
                    }

                    Vec3 lowest = ContactTrueShape.CoreSupport(body, 0, centre, Down);
                    double lowestDepth = Vec3.Dot(lowest, Down);
                    Vec3 lowestPush = PushAt(lowest, centre, margin, inertia, rotation, mass, config, stiffness, damping);

                    Vec3 expected;
                    double reached;
                    if (double.IsNegativeInfinity(deepestCorner))
                    {
                        if (!(lowestPush.Magnitude > 0))
                        {
                            Assert.False(pushed.Magnitude > 0, $"{form}: nothing should push yet");
                            continue;
                        }

                        alone++;
                        expected = lowestPush;
                        reached = lowestDepth;
                    }
                    else if (lowestDepth > deepestCorner + threshold)
                    {
                        switched++;
                        caseSwitched++;
                        expected = corners - deepestPush + lowestPush;
                        reached = lowestDepth;
                    }
                    else
                    {
                        cornerOnly++;
                        caseCorner++;
                        expected = corners;
                        reached = deepestCorner;
                    }

                    Assert.True(
                        lowestDepth - reached <= threshold + 1e-12,
                        $"{form}: the touch reaches {lowestDepth - reached} short of the lowest point");
                    Assert.True(
                        (pushed - expected).Magnitude <= 1e-12 * Math.Max(1.0, expected.Magnitude),
                        $"{form}: the touch {pushed} is not the rule's {expected}");
                }

                _out.WriteLine($"{form}, turn ({c[3]}, {c[4]}, {c[5]}): {caseCorner} heights on corners alone, {caseSwitched} switched");
            }

            _out.WriteLine($"all descents: {cornerOnly} on corners alone, {switched} switched, {alone} on the support point alone");
            Assert.True(cornerOnly > 0, "no height touched on corners alone");
            Assert.True(switched > 0, "no height switched the support point in");
        }

        private static Vec3 PushAt(
            Vec3 vertex, Vec3 centre, double margin, Vec3 inertia, Mat3 rotation, double mass,
            SolverConfig config, double stiffness, double damping)
        {
            Vec3 vertexPoint = vertex + Down * margin;
            double effective = ContactTrueShape.EffectiveMass(vertexPoint - centre, Vec3.UnitY, inertia, rotation, mass);
            double scale = effective / mass;
            return ContactTrueShape.SphereAt(
                ContactTrueShape.KindFlatBed, vertex, vertexPoint, centre, margin, Vec3.Zero, Vec3.Zero, true,
                effective, config, stiffness * scale, damping * scale);
        }

        // ------------------------------------------------------------ the rolling bench

        private sealed class RollRun
        {
            public double LargestRise;       // J, over every step
            public double PeakTilt;          // degrees, while the spin is on
            public double SettleSwing;       // degrees, largest less smallest tilt over the last ten seconds
            public int CornerSteps;          // steps with a corner in the bed
            public int SupportSteps;         // steps with the support point in the bed and no corner
            public int ModeSwitches;         // changes between the two
            public int NonFinite;
        }

        /// <summary>
        /// A body on the flat bed, given a spin about a horizontal axis (z) for six seconds and then none, and
        /// watched for sixteen more. Each step's mechanical energy is its kinetic energy (linear and turning) plus
        /// the weight times its height; the largest rise over a step, the tilt of its long axis from the vertical
        /// and, for the family part, which kind of touch it is in are recorded.
        /// </summary>
        private static RollRun Roll(bool family, PartForm form, Float3 half)
        {
            const double depth = 5;
            const double spinRate = 3.0;
            const double excess = 200;

            SolverConfig config = Config(ContactShapeForm.TrueShapeAtPoint, depth, excess, water: true);
            var world = new DynamicsWorld(config);

            // One centimetre above where the body's long axis (Y) would rest on its face.
            var start = new Vec3(0, -depth + half.Y + 0.01, 0);
            Creature body = family
                ? FamilyBody(config, form, half, 1, start, QuatD.Identity)
                : BoxBody(config, half, 1, start, QuatD.Identity);
            world.Add(body);

            Vec3.Write(body.Spin, 0, new Vec3(0, 0, spinRate));
            body.RefreshContactSphere();
            body.CommitContactSphere();

            double mass = body.Mass[0];
            Vec3 inertia = Vec3.Read(body.InertiaLocal, 0);
            double weight = excess * (1.0 - body.Lift[0]) * mass * config.GravityMetresPerSecondSquared / config.TissueDensity;
            double margin = body.LinkMargin[0];
            double[] hull = ContactTrueShape.HullOf(body, 0);

            double Energy()
            {
                Vec3 v = Vec3.Read(body.Velocity, 0);
                Vec3 local = Mat3.Read(body.RotationMatrix, 0).TransposedTimes(Vec3.Read(body.Spin, 0));
                double kinetic = 0.5 * mass * v.SqrMagnitude +
                                 0.5 * (inertia.X * local.X * local.X + inertia.Y * local.Y * local.Y + inertia.Z * local.Z * local.Z);
                return kinetic + weight * body.Position[1];
            }

            double Tilt() =>
                Math.Acos(Math.Max(-1.0, Math.Min(1.0, Mat3.Read(body.RotationMatrix, 0).M11))) * 180.0 / Math.PI;

            int mode()
            {
                body.EnsureContactCommitted();
                Vec3 centre = Vec3.Read(body.LinkContactCentre, 0);
                Mat3 rotation = Mat3.Read(body.LinkContactRotation, 0);

                for (int pattern = 0; pattern < 8; pattern++)
                {
                    if (ContactTrueShape.HullCorner(hull, pattern, rotation, centre).Y - margin < -depth) return 1;
                }

                return ContactTrueShape.CoreSupport(body, 0, centre, Down).Y - margin < -depth ? 2 : 0;
            }

            var run = new RollRun();
            int spinSteps = (int)Math.Round(6.0 / config.StepSeconds);
            int settleSteps = (int)Math.Round(16.0 / config.StepSeconds);
            int lastTen = (int)Math.Round(10.0 / config.StepSeconds);

            double lowest = double.PositiveInfinity, highest = double.NegativeInfinity;
            int previousMode = 0;

            for (int step = 0; step < spinSteps + settleSteps; step++)
            {
                if (step == spinSteps)
                {
                    // The spin is taken away, and the body's motion with it.
                    Array.Clear(body.Spin, 0, body.Spin.Length);
                    Array.Clear(body.Velocity, 0, body.Velocity.Length);
                    body.RefreshContactSphere();
                    body.CommitContactSphere();
                }

                double before = Energy();
                world.Step();
                double rise = Energy() - before;
                if (rise > run.LargestRise) run.LargestRise = rise;

                double tilt = Tilt();
                if (step < spinSteps && tilt > run.PeakTilt) run.PeakTilt = tilt;

                if (step >= spinSteps + settleSteps - lastTen)
                {
                    if (tilt < lowest) lowest = tilt;
                    if (tilt > highest) highest = tilt;
                }

                if (family)
                {
                    int now = mode();
                    if (now == 1) run.CornerSteps++;
                    if (now == 2) run.SupportSteps++;
                    if (now != 0)
                    {
                        if (previousMode != 0 && previousMode != now) run.ModeSwitches++;
                        previousMode = now;
                    }
                }
            }

            run.SettleSwing = highest - lowest;
            run.NonFinite = world.NonFiniteBodies();
            return run;
        }

        public static IEnumerable<object[]> BenchCases()
        {
            // The coordinator's own form and half-extents, between a box and a ball.
            yield return new object[] { 0.4f, 0.6f, 1f, 0.15f, 0.1f, 0.12f, true };

            // A rounder, taller one: with a margin ball as large as its smallest half-extent, a flat-bottomed form's
            // corner points stand as low as its lowest point, and the support point alone never touches. This one's
            // do not, so the bench passes through both kinds of touch. An ellipsoid rocks on a flat bed, which is its
            // physics and not the switch's, so it is held to condition one only (the coordinator's ruling of 2026-10-25).
            yield return new object[] { 1f, 1f, 1f, 0.15f, 0.2f, 0.12f, false };
        }

        [Theory]
        [MemberData(nameof(BenchCases))]
        public void TheRollingBenchTheFamilyPartGainsNoMoreEnergyAStepThanTheBoxPlusTheAllowanceAndSettlesNoLessSteadily(
            float profile, float section, float taper, float hx, float hy, float hz, bool settles)
        {
            // The coordinator's two conditions, read as follows. One: over every step of the run, a step's rise in
            // mechanical energy (kinetic, linear and turning, plus weight times height) is at most the box run's
            // largest rise plus 0.05 J, the allowance TrueShapeAtPointTests uses. Two: after the spin is taken away
            // and the body has had ten seconds to settle, the swing of its tilt over the next ten is at most the
            // box's, plus 0.01 degree for the rounding of a body that is truly still (the box's swing may be 0, and a
            // swing of 1e-9 degree is not rocking). Condition two binds only a case marked to settle: a
            // curved body rocks, and its swing is written out but not held to the box's.
            // The contact springs store energy that this sum does not count, so the rise in this sum is not zero even
            // for the box; that is why the box run is the yardstick and the family part is held to it.
            var form = new PartForm(profile, section, taper);
            var half = new Float3(hx, hy, hz);

            RollRun family = Roll(true, form, half);
            RollRun box = Roll(false, form, half);

            _out.WriteLine($"form {form}, half-extents {half}");
            _out.WriteLine(
                $"family: largest rise in one step {family.LargestRise:E3} J, peak tilt while spinning {family.PeakTilt:0.###} degrees, " +
                $"swing over the last 10 s {family.SettleSwing:E3} degrees, {family.CornerSteps} steps on corners, {family.SupportSteps} on the support point, " +
                $"{family.ModeSwitches} switches");
            _out.WriteLine(
                $"box:    largest rise in one step {box.LargestRise:E3} J, peak tilt while spinning {box.PeakTilt:0.###} degrees, " +
                $"swing over the last 10 s {box.SettleSwing:E3} degrees");
            _out.WriteLine($"condition one: family {family.LargestRise:E3} J against the box's {box.LargestRise:E3} J plus 0.05 J");
            _out.WriteLine($"condition two: family swing {family.SettleSwing:E3} degrees against the box's {box.SettleSwing:E3} degrees");

            Assert.Equal(0, family.NonFinite);
            Assert.Equal(0, box.NonFinite);

            // The bench is meant to pass through both kinds of touch. Whether a form does is a property of its
            // geometry, so it is written to the output for the reader and not asserted.
            _out.WriteLine(
                family.CornerSteps > 0 && family.SupportSteps > 0
                    ? "the family part visited both kinds of touch"
                    : "the family part did NOT visit both kinds of touch: this case says nothing about the switch");

            Assert.True(
                family.LargestRise <= box.LargestRise + 0.05,
                $"condition one: the family part gained {family.LargestRise} J in a step; the box's largest was {box.LargestRise} J");
            if (settles)
            {
                Assert.True(
                    family.SettleSwing <= box.SettleSwing + 0.01,
                    $"condition two: the family part's tilt swung {family.SettleSwing} degrees; the box's swung {box.SettleSwing}");
            }
        }
    }
}

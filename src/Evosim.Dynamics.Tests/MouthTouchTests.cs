using System;
using Evosim.Core;
using Xunit;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// D275's gap from a mouth to a part, on the true shape: closed forms for each core, a point inside the core, the
    /// short way across a seam, and the pair <see cref="MouthTouch.Reach"/> chooses against a brute force.
    /// </summary>
    public sealed class MouthTouchTests
    {
        private static readonly PartShapeRegistry Family = new PartShapeRegistry(
            new BoxShape(), new SphereShape(), new CapsuleShape(), new SuperellipsoidShape());

        private static SolverConfig Config(double seam = 0)
        {
            SolverConfig config = TestBodies.Vacuum(0.01);
            config.CreatureContact = true;
            config.ContactPerPart = true;
            config.ContactShape = ContactShapeForm.TrueShape;
            config.SeamLengthMetres = seam;
            config.SeamWidthMetres = seam;
            return config;
        }

        private static Creature Part(SolverConfig config, string shape, Float3 half, Vec3 at, QuatD? turn = null)
        {
            Genome genome = TestBodies.Box(half);
            genome.Nodes[0].ShapeId = shape;
            Creature body = TestBodies.Build(genome, config, 1);
            body.PlaceAt(at, turn ?? QuatD.Identity);
            return body;
        }

        private static void Gap(double expected, Creature body, Vec3 point, SolverConfig config, string what)
        {
            Assert.True(
                Math.Abs(expected - MouthTouch.Gap(body, 0, point, config)) < 1e-6,
                $"{what}: expected {expected:R}, got {MouthTouch.Gap(body, 0, point, config):R}");
        }

        // ------------------------------------------------------------ the gap

        [Fact]
        public void GapMatchesClosedForms()
        {
            SolverConfig config = Config();

            // A box of half-extents (0.5, 0.3, 0.2) is its core (0.3, 0.1, 0) swept by a ball of 0.2.
            Creature box = Part(config, ShapeIds.Box, new Float3(0.5f, 0.3f, 0.2f), Vec3.Zero);
            Gap(0.5, box, new Vec3(1.0, 0, 0), config, "a box, off its face");
            Gap(0.5, box, new Vec3(0, 0.8, 0), config, "a box, off another face");

            // Off an edge of the core: the nearest core point is the edge's corner point, 5 away.
            Gap(5.0 - 0.2, box, new Vec3(0.3 + 3.0, 0.1 + 4.0, 0), config, "a box, off an edge");

            // Off a corner of the core (its z extent is 0, so every corner has z = 0).
            Gap(Math.Sqrt(12.0) - 0.2, box, new Vec3(0.3 + 2.0, 0.1 + 2.0, 2.0), config, "a box, off a corner");

            // Inside the swept ball but outside the core: a negative gap, the depth in the margin.
            Gap(-0.1, box, new Vec3(0.3 + 0.1, 0, 0), config, "a box, in the margin");

            // Inside the core, and at its centre: minus the margin.
            Gap(-0.2, box, new Vec3(0.1, 0, 0), config, "a box, inside the core");
            Gap(-0.2, box, Vec3.Zero, config, "a box, at its centre");

            // A capsule: radius the mean of |hx| and |hz|, a segment along y.
            Creature capsule = Part(config, ShapeIds.Capsule, new Float3(0.2f, 0.5f, 0.2f), Vec3.Zero);
            Gap(1.0 - 0.3 - 0.2, capsule, new Vec3(0, 1.0, 0), config, "a capsule, off its tip");
            Gap(1.0 - 0.2, capsule, new Vec3(1.0, 0, 0), config, "a capsule, off its side");
            Gap(Math.Sqrt(0.5 * 0.5 + 0.4 * 0.4) - 0.2, capsule, new Vec3(0.5, 0.7, 0), config, "a capsule, off its cap");

            // A sphere: the point at the centre swept by the mean half-extent.
            Creature sphere = Part(config, ShapeIds.Sphere, new Float3(0.3f, 0.3f, 0.3f), new Vec3(1, 2, 3));
            Gap(1.0 - 0.3, sphere, new Vec3(2, 2, 3), config, "a sphere");
            Gap(0.5 - 0.3, sphere, new Vec3(1, 2 + 0.5, 3), config, "a sphere, off its top");

            // A turned body: the core is turned with the link, from the solver's own rotation matrix.
            QuatD quarter = QuatD.FromAxisAngle(new Vec3(0, 0, 1), Math.PI / 2.0);
            Creature turned = Part(config, ShapeIds.Box, new Float3(0.5f, 0.1f, 0.1f), Vec3.Zero, quarter);
            Gap(1.0 - 0.1 - 0.4, turned, new Vec3(0, 1.0, 0), config, "a box turned a quarter, off the end that now faces +y");
            Gap(1.0 - 0.1, turned, new Vec3(1.0, 0, 0), config, "a box turned a quarter, off its side");
        }

        [Fact]
        public void GapOfAHullIsTheDistanceToTheHullLessTheMargin()
        {
            SolverConfig config = Config();
            var ball = new PartForm(0.6f, 1f, 1.5f);
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f };
            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.3f, 0.3f, 0.3f),
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Superellipsoid,
                Form = ball,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            var body = new Creature(1, Developer.Develop(genome, shapes: Family), config, Family);
            body.PlaceAt(new Vec3(0.2, -0.1, 0.4), QuatD.Identity);
            Assert.NotNull(body.LinkCoreHull[0]);

            // The hull's farthest vertex from its centre is the support point along its own direction, so a point beyond
            // it on that line is nearest that vertex, and the distance is the step taken beyond.
            double[] hull = body.LinkCoreHull[0];
            double far = -1;
            Vec3 vertex = Vec3.Zero;
            for (int k = 0; k < hull.Length; k += 3)
            {
                var v = new Vec3(hull[k], hull[k + 1], hull[k + 2]);
                if (v.Magnitude > far) { far = v.Magnitude; vertex = v; }
            }

            Assert.True(far > 0.01, "the hull is a point");

            double margin = body.LinkMargin[0];
            Vec3 centre = Vec3.Read(body.Position, 0);
            Vec3 point = centre + vertex * (1.0 + 1.5 / far);
            Gap(1.5 - margin, body, point, config, "a hull, off its x pole");
            Gap(-margin, body, centre, config, "a hull, at its centre");
        }

        [Fact]
        public void AGapAcrossASeamIsTheShortWayRound()
        {
            SolverConfig seamed = Config(seam: 10.0);
            Creature box = Part(seamed, ShapeIds.Box, new Float3(0.5f, 0.5f, 0.5f), new Vec3(4.5, 0, 0));

            // The point is 8.9 away the long way and 1.1 the short way; the box is its centre swept by 0.5.
            Gap(1.1 - 0.5, box, new Vec3(-4.4, 0, 0), seamed, "across the x seam");
            Gap(1.1 - 0.5, box, new Vec3(4.5, 0, 1.1), seamed, "a point 1.1 off in z, no seam in play");

            SolverConfig open = Config();
            Creature same = Part(open, ShapeIds.Box, new Float3(0.5f, 0.5f, 0.5f), new Vec3(4.5, 0, 0));
            Gap(8.9 - 0.5, same, new Vec3(-4.4, 0, 0), open, "without the seam");

            // And across z.
            Creature zbox = Part(seamed, ShapeIds.Box, new Float3(0.5f, 0.5f, 0.5f), new Vec3(0, 0, -4.5));
            Gap(1.1 - 0.5, zbox, new Vec3(0, 0, 4.4), seamed, "across the z seam");
        }

        [Fact]
        public void GapRefusesABodyWithNoCores()
        {
            SolverConfig spheres = Config();
            spheres.ContactShape = ContactShapeForm.Spheres;
            Creature body = Part(spheres, ShapeIds.Box, new Float3(0.5f, 0.5f, 0.5f), Vec3.Zero);
            Assert.Throws<InvalidOperationException>(() => MouthTouch.Gap(body, 0, new Vec3(2, 0, 0), spheres));
        }

        // ------------------------------------------------------------ the reach

        private static Genome Eater(Float3 half)
        {
            Genome genome = TestBodies.Box(half);
            MorphNode node = genome.Nodes[0];
            node.CellTypeId = CellTypeIds.Consumer;
            node.Intake = 1f;
            node.Attack = 1f;
            return genome;
        }

        [Fact]
        public void ReachIsTheGapOverTheMouthsRadiusAndNamesTheMouthAndThePart()
        {
            SolverConfig config = Config();

            Creature Rebuilt(double x) => Part(config, ShapeIds.Box, new Float3(0.5f, 0.5f, 0.5f), new Vec3(x, 0, 0));

            // A single box with its mouth at the centre of its +x face, (0.5, 0, 0), and a radius of 0.1.
            Creature eater = TestBodies.Build(Eater(new Float3(0.5f, 0.2f, 0.2f)), config, 1);
            eater.PlaceAt(Vec3.Zero, QuatD.Identity);
            Assert.Equal(0.1f, MouthPoint.Radius(eater.Phenotype, 0), 6);

            // A cube of half-extent 0.5 centred at 1.05 has its near face 0.05 from the mouth: the ratio is 0.5.
            Creature food = Rebuilt(1.05);

            float near = MouthTouch.Reach(eater, food, config, out int link, out int part);
            Assert.Equal(0.5f, near, 4);
            Assert.Equal(0, link);
            Assert.Equal(0, part);

            // At 1.1 the gap is the radius and the ratio 1; at 1.6 the gap is 0.6 and the ratio 6.
            food.PlaceAt(new Vec3(1.1, 0, 0), QuatD.Identity);
            Assert.Equal(1.0f, MouthTouch.Reach(eater, food, config, out _, out _), 4);

            food.PlaceAt(new Vec3(1.4, 0, 0), QuatD.Identity);
            Assert.Equal(4.0f, MouthTouch.Reach(eater, food, config, out _, out _), 3);

            // At 1.6 the cube's bounding ball, grown by the radius, cannot reach the mouth: it is not measured, and the
            // answer is above 1 all the same.
            food.PlaceAt(new Vec3(1.6, 0, 0), QuatD.Identity);
            Assert.True(MouthTouch.Reach(eater, food, config, out _, out _) > 1f);

            // Far enough that the bounding ball cannot reach: positive infinity, and no pair named.
            food.PlaceAt(new Vec3(50, 0, 0), QuatD.Identity);
            float far = MouthTouch.Reach(eater, food, config, out link, out part);
            Assert.True(float.IsPositiveInfinity(far));
            Assert.Equal(-1, link);
            Assert.Equal(-1, part);

            // The mouth buried in the food: a negative ratio.
            food.PlaceAt(new Vec3(0.5, 0, 0), QuatD.Identity);
            Assert.True(MouthTouch.Reach(eater, food, config, out _, out _) < 0f);
        }


        [Fact]
        public void ReachIsNotMeasuredForABodyWithoutAMouthOrAnotherWithoutCores()
        {
            SolverConfig config = Config();
            Creature plain = Part(config, ShapeIds.Box, new Float3(0.5f, 0.2f, 0.2f), Vec3.Zero);
            Creature food = Part(config, ShapeIds.Box, new Float3(0.5f, 0.5f, 0.5f), new Vec3(1.55, 0, 0));
            Assert.True(float.IsNaN(MouthTouch.Reach(plain, food, config, out int link, out int part)));
            Assert.Equal(-1, link);
            Assert.Equal(-1, part);

            SolverConfig spheres = Config();
            spheres.ContactShape = ContactShapeForm.Spheres;
            Creature coreless = Part(spheres, ShapeIds.Box, new Float3(0.5f, 0.5f, 0.5f), new Vec3(1.55, 0, 0));
            Creature eater = TestBodies.Build(Eater(new Float3(0.5f, 0.2f, 0.2f)), config, 1);
            eater.PlaceAt(Vec3.Zero, QuatD.Identity);
            Assert.True(float.IsNaN(MouthTouch.Reach(eater, coreless, config, out _, out _)));
        }

        [Fact]
        public void ReachChoosesTheSmallestRatioOverEveryMouthAndPartAsABruteForceDoes()
        {
            SolverConfig config = Config();

            Genome chain = TestBodies.Chain(4, JointType.Fixed, power: 0f, neurons: false);
            foreach (MorphNode node in chain.Nodes)
            {
                node.CellTypeId = CellTypeIds.Consumer;
                node.Intake = 1f;
            }

            Genome food = TestBodies.Chain(3, JointType.Fixed, power: 0f, neurons: false);

            var rng = new Random(12345);
            int nearest = 0;
            for (int trial = 0; trial < 40; trial++)
            {
                Creature eater = TestBodies.Build(chain, config, 1);
                Creature target = TestBodies.Build(food, config, 2);

                QuatD turnA = QuatD.FromAxisAngle(Unit(rng), rng.NextDouble() * 6.0);
                QuatD turnB = QuatD.FromAxisAngle(Unit(rng), rng.NextDouble() * 6.0);
                eater.PlaceAt(Vec3.Zero, turnA);
                target.PlaceAt(new Vec3(1.2 * rng.NextDouble() - 0.2, 1.0 * rng.NextDouble() - 0.5, 1.0 * rng.NextDouble() - 0.5), turnB);

                float best = float.PositiveInfinity;
                int bestLink = -1, bestPart = -1;
                for (int i = 0; i < eater.Links; i++)
                {
                    float radius = MouthPoint.Radius(eater.Phenotype, i);
                    if (!(radius > 0f)) continue;

                    Float3 local = MouthPoint.Local(eater.Phenotype, i);
                    Vec3 point = Vec3.Read(eater.Position, 3 * i) +
                                 Mat3.Read(eater.RotationMatrix, 9 * i) * new Vec3(local.X, local.Y, local.Z);

                    for (int q = 0; q < target.Links; q++)
                    {
                        float ratio = (float)(MouthTouch.Gap(target, q, point, config) / radius);
                        if (ratio < best) { best = ratio; bestLink = i; bestPart = q; }
                    }
                }

                float got = MouthTouch.Reach(eater, target, config, out int link, out int part);

                // A pair the screen skipped has a ratio above 1, so below 1 the answer is the brute force's exactly.
                if (best <= 1f)
                {
                    nearest++;
                    Assert.Equal(best, got);
                    Assert.Equal(bestLink, link);
                    Assert.Equal(bestPart, part);
                }
                else
                {
                    Assert.True(got > 1f, $"trial {trial}: brute force {best}, reach {got}");
                }
            }

            Assert.True(nearest >= 5, "the trials should include some mouths that touch");
        }

        private static Vec3 Unit(Random rng)
        {
            var v = new Vec3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5);
            return v.Magnitude > 1e-6 ? v * (1.0 / v.Magnitude) : new Vec3(0, 0, 1);
        }
    }
}

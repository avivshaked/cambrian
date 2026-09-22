using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The two things the mouth needs from the solver — D106 items 4 and 5, and
    /// <c>logbook/specs/mouth-spec.md</c> rules 4 and 5: which part of one body is against which
    /// part of another, and a body that survives losing a limb.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The census is not extended, it is asked.</b> <see cref="DynamicsWorld.NearestParts"/>
    /// answers off the overlap list the instrument already builds, once per metabolic step rather
    /// than once per physics step, so no recorded number moves and the pace of the solver's own
    /// loop is untouched. What is tested here is that the answer names the parts a reader would
    /// name by looking at the two bodies.
    /// </para>
    /// <para>
    /// <b>A kill is a plan change, and the plan change is the module gene's.</b>
    /// <see cref="ModuleRebuildTests"/> makes the argument for a module; this asks the same of the
    /// one case a module cannot make — a subtree removed from the middle of a body, which is what
    /// a bite does and what no count can express.
    /// </para>
    /// </remarks>
    public sealed class MouthContactTests
    {
        private readonly ITestOutputHelper _out;

        public MouthContactTests(ITestOutputHelper output) => _out = output;

        // ------------------------------------------------------ rule 5: which parts are touching

        [Fact]
        public void TheNearestPairNamesTheFacingPartsOfTwoOverlappingBodies()
        {
            SolverConfig config = TestBodies.Water(0.01);
            config.CreatureContact = true;
            config.ContactInstrument = true;
            config.ContactEvents = true;

            // Two three-part chains laid end to end along +X, close enough that their bounding
            // spheres overlap. Which parts face each other is then a fact about the picture and
            // not about the implementation: the last part of the first body and the first part of
            // the second.
            var world = new DynamicsWorld(config) { Threads = 1 };

            Creature left = TestBodies.Build(TestBodies.Chain(3), config, id: 4);
            Creature right = TestBodies.Build(TestBodies.Chain(3), config, id: 9);

            left.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);
            right.PlaceAt(new Vec3(1.0, -10, 0), QuatD.Identity);

            world.AddInIdOrder(left);
            world.AddInIdOrder(right);

            world.Step();

            Assert.Single(world.Overlaps);
            OverlapPair pair = world.Overlaps[0];

            Assert.Equal(4L, pair.A);
            Assert.Equal(9L, pair.B);

            Assert.True(world.NearestParts(pair, out int partA, out int partB));

            _out.WriteLine(
                $"{Xs(left)} against {Xs(right)}: parts {partA} and {partB}");

            // Stated independently of the search: the part of the left body that reaches furthest
            // towards the right one, and the part of the right body that reaches furthest back.
            Assert.Equal(Furthest(left, +1), partA);
            Assert.Equal(Furthest(right, -1), partB);

            // And the pair is unambiguous rather than the first index by default.
            Assert.NotEqual(partA, partB);
        }

        [Fact]
        public void APairNamingABodyThatHasLeftTheWorldIsRefusedRatherThanGuessed()
        {
            // The list is taken during a step and consumed between steps, and a body can die in
            // between. Returning false is what lets the farm drop that contact; returning part 0
            // would put a bite on whatever body next held the id.
            SolverConfig config = TestBodies.Water(0.01);
            config.CreatureContact = true;
            config.ContactInstrument = true;
            config.ContactEvents = true;

            var world = new DynamicsWorld(config) { Threads = 1 };

            Creature left = TestBodies.Build(TestBodies.Chain(3), config, id: 4);
            Creature right = TestBodies.Build(TestBodies.Chain(3), config, id: 9);

            left.PlaceAt(new Vec3(0, -10, 0), QuatD.Identity);
            right.PlaceAt(new Vec3(1.0, -10, 0), QuatD.Identity);

            world.AddInIdOrder(left);
            world.AddInIdOrder(right);

            world.Step();
            Assert.Single(world.Overlaps);

            OverlapPair pair = world.Overlaps[0];
            Assert.True(world.Remove(9L));

            Assert.False(world.NearestParts(pair, out int partA, out int partB));
            Assert.Equal(-1, partA);
            Assert.Equal(-1, partB);
        }

        /// <summary>The index of the part reaching furthest along <paramref name="sign"/>·X.</summary>
        private static int Furthest(Creature body, int sign)
        {
            int at = 0;
            double best = sign * body.Position[0];

            for (int i = 1; i < body.Links; i++)
            {
                double x = sign * body.Position[3 * i];
                if (x <= best) continue;

                best = x;
                at = i;
            }

            return at;
        }

        private static string Xs(Creature body)
        {
            var parts = new List<string>(body.Links);
            for (int i = 0; i < body.Links; i++)
            {
                parts.Add(FormattableString.Invariant($"{body.Position[3 * i]:0.###}"));
            }

            return "x " + string.Join("/", parts);
        }

        // ----------------------------------------------------- rule 4: the body that survives it

        [Fact]
        public void ABodyThatLostASubtreeCarriesEverySurvivingJoint()
        {
            // The kill's rebuild, on the shape no module count can produce: the arm is taken off
            // at its shoulder and the branch — which never changed and whose index did — has to
            // come through with its angle and its rate. This is the route World.KillPart takes,
            // primitive for primitive: prune by subtree, match the survivors' paths, adopt.
            SolverConfig config = TestBodies.Water(0.01);
            Genome genome = ForkedArm();

            var paths = new List<int[]>();
            Phenotype whole = Developer.Develop(
                genome, DevelopmentLimits.Default, null, PartShapeRegistry.Standard,
                new[] { 1, 2, 1 }, paths);

            Assert.Equal(4, whole.PartCount);

            // Part 1 is the arm's shoulder, and part 2 hangs off it: one bite, two parts gone.
            var drop = new bool[whole.PartCount];
            drop[1] = true;

            Phenotype cut = whole.WithoutSubtrees(drop, out int[] kept);

            var survived = new List<int[]>(kept.Length);
            for (int i = 0; i < kept.Length; i++) survived.Add(paths[kept[i]]);

            int[] map = Developer.MatchParts(paths, survived);

            _out.WriteLine(
                $"{whole.PartCount} parts -> {cut.PartCount}; kept {string.Join(",", kept)}, " +
                $"map {string.Join(",", map)}");

            Assert.Equal(2, cut.PartCount);
            Assert.Equal(new[] { 0, 3 }, kept);

            // The two routes agree. They have to: the pruning renumbers the body and the path
            // match is what the rebuild carries state on, and a disagreement would hand the
            // branch the arm's stroke on a body that still looks right.
            Assert.Equal(kept, map);

            var old = new Creature(11, whole, config, PartShapeRegistry.Standard);
            Pose(old);

            var water = new DynamicsWorld(config) { Threads = 1 };
            water.Add(old);
            for (int step = 0; step < 400; step++) water.Step();

            double[] oldQ = (double[])old.Q.Clone();
            double[] oldQd = (double[])old.Qd.Clone();
            Vec3 rootPosition = Vec3.Read(old.Position, 0);
            Vec3 rootVelocity = Vec3.Read(old.Velocity, 0);
            double oldClock = old.Brain.ElapsedSeconds;

            var rebuilt = new Creature(11, cut, config, PartShapeRegistry.Standard);
            rebuilt.AdoptStateFrom(old, map);

            Assert.Equal(0.0, (rebuilt.BasePosition - old.BasePosition).Magnitude);
            Assert.Equal(0.0, (Vec3.Read(rebuilt.Position, 0) - rootPosition).Magnitude);
            Assert.Equal(0.0, (Vec3.Read(rebuilt.Velocity, 0) - rootVelocity).Magnitude);

            int branch = rebuilt.DofStart[1];
            int wasBranch = old.DofStart[3];

            _out.WriteLine(
                $"branch angle {oldQ[wasBranch]:0.######} rad and rate {oldQd[wasBranch]:0.######} " +
                $"carried from dof {wasBranch} to {branch}; brain clock {oldClock:0.##} s");

            Assert.True(Math.Abs(oldQ[wasBranch]) > 1e-3, "the branch barely moved");

            Assert.Equal(
                BitConverter.DoubleToInt64Bits(oldQ[wasBranch]),
                BitConverter.DoubleToInt64Bits(rebuilt.Q[branch]));

            Assert.Equal(
                BitConverter.DoubleToInt64Bits(oldQd[wasBranch]),
                BitConverter.DoubleToInt64Bits(rebuilt.Qd[branch]));

            Assert.Equal(oldClock, rebuilt.Brain.ElapsedSeconds);
            Assert.True(rebuilt.IsFinite(), "the rebuilt body is not a number");
            Assert.True(rebuilt.ContactRadius > 0, "the rebuilt body has no contact sphere");
        }

        /// <summary>
        /// <see cref="ModuleRebuildTests.ForkedArm"/>'s body, rebuilt here rather than shared: a
        /// root with an indeterminate arm on its +X face and a plain branch on its −X face.
        /// </summary>
        private static Genome ForkedArm()
        {
            var genome = new Genome { RootIndex = 0, AdultScale = 1f };
            genome.Reproduction = new ReproductionTraits
            {
                BroodSize = 1, BirthInvestment = 0.5f, ReserveMargin = 0f,
            };

            var limits = new Float2[] { new Float2(-1.2f, 1.2f) };

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                CellTypeId = CellTypeIds.Structural,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Fixed,
                JointLimits = Array.Empty<Float2>(),
                RecursiveLimit = 1,
            });

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.2f, 0.05f, 0.2f),
                CellTypeId = CellTypeIds.Link,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Hinge,
                JointLimits = (Float2[])limits.Clone(),
                Power = 10f,
                RecursiveLimit = 1,
                Growth = ModuleGrowth.Indeterminate,
                MaxModules = 4,
            });

            genome.Nodes.Add(new MorphNode
            {
                Dimensions = new Float3(0.15f, 0.06f, 0.15f),
                CellTypeId = CellTypeIds.Link,
                ShapeId = ShapeIds.Box,
                JointType = JointType.Hinge,
                JointLimits = (Float2[])limits.Clone(),
                Power = 10f,
                RecursiveLimit = 1,
            });

            genome.Nodes[0].Edges.Add(Edge(1, new Float3(1f, 0f, 0f), new Float3(-1f, 0f, 0f)));
            genome.Nodes[0].Edges.Add(Edge(2, new Float3(-1f, 0f, 0f), new Float3(1f, 0f, 0f)));
            genome.Nodes[1].Edges.Add(Edge(1, new Float3(1f, 0f, 0f), new Float3(-1f, 0f, 0f)));

            return genome;
        }

        private static MorphEdge Edge(int child, Float3 parentAnchor, Float3 childAnchor) =>
            new MorphEdge
            {
                Child = child,
                ParentAnchor = parentAnchor,
                ChildAnchor = childAnchor,
                Orientation = Quat.Identity,
                Scale = Float3.One,
            };

        /// <summary>A root off the origin and turned, moving and spinning — never a rest pose.</summary>
        private static void Pose(Creature body)
        {
            body.PlaceAt(
                new Vec3(1.5, -7.25, -2.0),
                QuatD.FromAxisAngle(new Vec3(0.3, 0.5, -0.2).Normalized, 0.7));

            Vec3.Write(body.Velocity, 0, new Vec3(0.4, -0.15, 0.25));
            Vec3.Write(body.Spin, 0, new Vec3(-0.2, 0.35, 0.1));

            Kinematics.Refresh(body);
            body.RefreshContactSphere();
            body.CommitContactSphere();
        }
    }
}

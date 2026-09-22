using System;
using System.Collections.Generic;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Dynamics.Tests
{
    /// <summary>
    /// The module gene's rebuild — rule 7 of <c>logbook/specs/module-gene-spec.md</c>, D106 item 2.
    /// A body whose part count changed is built afresh and then takes over what the old body was
    /// doing: where it stood, how fast it was going, and every surviving joint's angle and rate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <c>GrowthTests.AResizedBodyIsTheBodyBuiltAtThatSize</c>'s shape asked of the one
    /// case <see cref="Creature.Resize(Phenotype)"/> refuses. Growth keeps the plan and changes the
    /// size, so its acceptance is bit-equality against a fresh build. A module keeps the size and
    /// changes the plan, so the fresh build <i>is</i> the body and the acceptance is what crossed
    /// into it.
    /// </para>
    /// <para>
    /// <b>The branch is the whole point.</b> A module lands in the middle of a depth-first walk, so
    /// every part after it in the traversal shifts index — the branch that was link 2 becomes link
    /// 3 and nothing about it changed. A rebuild that carried state by index would hand that branch
    /// the new module's rest state and hand the new module the branch's stroke, silently, on a body
    /// that still looks right. So the map comes from the developer's own part paths
    /// (<c>Developer.MatchParts</c>), and the assertion below is the case an index map gets wrong.
    /// </para>
    /// </remarks>
    public sealed class ModuleRebuildTests
    {
        private readonly ITestOutputHelper _out;

        public ModuleRebuildTests(ITestOutputHelper output) => _out = output;

        /// <summary>
        /// A root with two children: an indeterminate self-looping arm on its +X face, and a plain
        /// link on its −X face. Growing the arm pushes the plain link one index down the body.
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

            // 1: the arm, one segment per module, each on the last one's +X face.
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

            // 2: the branch, which never changes and whose index does.
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

        private static Phenotype Develop(Genome genome, int armCount, out List<int[]> paths)
        {
            paths = new List<int[]>();
            return Developer.Develop(
                genome, DevelopmentLimits.Default, null, PartShapeRegistry.Standard,
                new[] { 1, armCount, 1 }, paths);
        }

        [Fact]
        public void ARebuiltBodyKeepsItsPlaceItsMotionAndEverySurvivingJoint()
        {
            SolverConfig config = TestBodies.Water(0.01);
            Genome genome = ForkedArm();

            Phenotype before = Develop(genome, 1, out List<int[]> beforePaths);
            Phenotype after = Develop(genome, 2, out List<int[]> afterPaths);

            Assert.Equal(3, before.PartCount);
            Assert.Equal(4, after.PartCount);

            int[] map = Developer.MatchParts(beforePaths, afterPaths);

            _out.WriteLine("new part -> old part: " + string.Join(", ", map));

            // The developer's own answer, and the reason the map is not the identity: the branch
            // moved from 2 to 3 and the part at 2 is the one that did not exist before.
            Assert.Equal(new[] { 0, 1, -1, 2 }, map);

            var old = new Creature(7, before, config, PartShapeRegistry.Standard);
            Pose(old);

            // Something to lose: a few seconds of driven swimming, so the joints are bent and
            // turning and the brain has a recurrent state that is not its founding one.
            var water = new DynamicsWorld(config) { Threads = 1 };
            water.Add(old);
            for (int step = 0; step < 400; step++) water.Step();

            double[] oldQ = (double[])old.Q.Clone();
            double[] oldQd = (double[])old.Qd.Clone();
            Vec3 rootPosition = Vec3.Read(old.Position, 0);
            Vec3 rootVelocity = Vec3.Read(old.Velocity, 0);
            Vec3 rootSpin = Vec3.Read(old.Spin, 0);
            double oldClock = old.Brain.ElapsedSeconds;

            var rebuilt = new Creature(7, after, config, PartShapeRegistry.Standard);
            rebuilt.AdoptStateFrom(old, map);

            // (1) The body stands where it stood and is going where it was going. The root is the
            // only link whose motion is state; the rest is re-derived from it and the joint rates,
            // which is what an articulation's velocity is.
            Assert.Equal(0.0, (rebuilt.BasePosition - old.BasePosition).Magnitude);
            Assert.Equal(0.0, (Vec3.Read(rebuilt.Position, 0) - rootPosition).Magnitude);
            Assert.Equal(0.0, (Vec3.Read(rebuilt.Velocity, 0) - rootVelocity).Magnitude);
            Assert.Equal(0.0, (Vec3.Read(rebuilt.Spin, 0) - rootSpin).Magnitude);

            // (2) Every surviving joint, to the bit — including the branch, which an index map
            // would have handed the new module's zeros.
            for (int link = 0; link < rebuilt.Links; link++)
            {
                int from = map[link];
                int dof = rebuilt.DofCount[link];
                if (dof == 0) continue;

                int to = rebuilt.DofStart[link];

                if (from < 0)
                {
                    // (3) A new module starts at rest relative to its parent, which is rule 7's
                    // own wording: the zeros the constructor left, and no velocity of its own.
                    for (int d = 0; d < dof; d++)
                    {
                        Assert.Equal(0.0, rebuilt.Q[to + d]);
                        Assert.Equal(0.0, rebuilt.Qd[to + d]);
                    }

                    continue;
                }

                int at = old.DofStart[from];
                for (int d = 0; d < dof; d++)
                {
                    Assert.Equal(
                        BitConverter.DoubleToInt64Bits(oldQ[at + d]),
                        BitConverter.DoubleToInt64Bits(rebuilt.Q[to + d]));

                    Assert.Equal(
                        BitConverter.DoubleToInt64Bits(oldQd[at + d]),
                        BitConverter.DoubleToInt64Bits(rebuilt.Qd[to + d]));
                }
            }

            // Vacuous unless the joints had somewhere to be: a body that never moved cannot have
            // its motion dropped.
            double bent = 0;
            for (int d = 0; d < oldQ.Length; d++) bent = Math.Max(bent, Math.Abs(oldQ[d]));
            Assert.True(bent > 1e-3, $"the old body's joints barely moved ({bent} rad)");

            // (4) The brain is the same brain at the same moment, which is what stops a plant
            // that grew a leaf forgetting how to swim.
            Assert.Equal(oldClock, rebuilt.Brain.ElapsedSeconds);

            // (5) And the new body is a body: finite, and its kinematics walked outward from the
            // root rather than being left at the constructor's straight pose.
            Assert.True(rebuilt.IsFinite(), "the rebuilt body is not a number");
            Assert.True(rebuilt.ContactRadius > 0, "the rebuilt body has no contact sphere");

            _out.WriteLine(
                $"{old.Links} links -> {rebuilt.Links}; worst old angle {bent:0.####} rad; " +
                $"root at {rootPosition.Y:0.###} m, brain clock {oldClock:0.##} s");
        }

        [Fact]
        public void ADroppedModuleCarriesTheSurvivorsTheSameWay()
        {
            // The drop is the add read backwards, and the map is not its inverse: going from two
            // arm segments to one, the branch moves from 3 to 2 and the part that vanishes has no
            // row at all. The same call answers both, which is the point of matching by path.
            SolverConfig config = TestBodies.Water(0.01);
            Genome genome = ForkedArm();

            Phenotype before = Develop(genome, 2, out List<int[]> beforePaths);
            Phenotype after = Develop(genome, 1, out List<int[]> afterPaths);

            int[] map = Developer.MatchParts(beforePaths, afterPaths);
            Assert.Equal(new[] { 0, 1, 3 }, map);

            var old = new Creature(3, before, config, PartShapeRegistry.Standard);
            Pose(old);

            var water = new DynamicsWorld(config) { Threads = 1 };
            water.Add(old);
            for (int step = 0; step < 400; step++) water.Step();

            double[] oldQ = (double[])old.Q.Clone();

            var rebuilt = new Creature(3, after, config, PartShapeRegistry.Standard);
            rebuilt.AdoptStateFrom(old, map);

            Assert.Equal(0.0, (rebuilt.BasePosition - old.BasePosition).Magnitude);

            // The branch's angle, which sat at old dof index 2 and now sits at 1.
            int branch = rebuilt.DofStart[2];
            int wasBranch = old.DofStart[3];

            _out.WriteLine(
                $"{old.Links} links -> {rebuilt.Links}; branch angle " +
                $"{oldQ[wasBranch]:0.######} rad carried from dof {wasBranch} to {branch}");

            Assert.Equal(
                BitConverter.DoubleToInt64Bits(oldQ[wasBranch]),
                BitConverter.DoubleToInt64Bits(rebuilt.Q[branch]));

            Assert.True(rebuilt.IsFinite(), "the rebuilt body is not a number");
        }

        [Fact]
        public void AMapThatDoesNotCoverTheBodyIsRefused()
        {
            // A short map is the fault this cannot be allowed to absorb: it would leave the tail
            // of a body at rest and report that its state had been carried, which is a creature
            // that quietly stops swimming with half of itself.
            SolverConfig config = TestBodies.Water(0.01);
            Genome genome = ForkedArm();

            Phenotype before = Develop(genome, 1, out _);
            Phenotype after = Develop(genome, 2, out _);

            var old = new Creature(1, before, config, PartShapeRegistry.Standard);
            var rebuilt = new Creature(1, after, config, PartShapeRegistry.Standard);

            Assert.Throws<ArgumentException>(
                () => rebuilt.AdoptStateFrom(old, new[] { 0, 1, -1 }));
        }

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

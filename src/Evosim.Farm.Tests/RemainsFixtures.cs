using System;
using System.Collections.Generic;
using Evosim.Core;
using Evosim.Dynamics;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The bodies and the worlds the remains tests share (D220), and the dial-off digest test: forked, jointed victims,
    /// armed claws and a hunter whose mouth eats, in the touching world <c>BiteRebuildTests</c> uses.
    /// </summary>
    /// <remarks>
    /// Written against what the tree held before the remains reached the solver (the dial, the window, the world, the
    /// harness), so that <c>DialOffDigestTests</c> can be built and run on that tree as it stood.
    /// </remarks>
    internal static class RemainsFixtures
    {
        /// <summary>The touching world with no floor spawns, so a test knows every body in it.</summary>
        internal static RunConfig Config(bool contactEating, float windowSeconds = 60f)
        {
            RunConfig config = CheckpointRestoreTests.TouchingWorld();
            config.MinimumPopulation = 0;
            config.ContactEating = contactEating;
            config.RemainsWindowSeconds = windowSeconds;
            return config;
        }

        internal sealed class Pair : IDisposable
        {
            public World World;
            public Simulation Sim;

            public void Dispose() => Sim?.Dispose();
        }

        internal static Pair Found(RunConfig config)
        {
            var world = new World(config, 1UL);

            return new Pair
            {
                World = world,
                Sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: null),
            };
        }

        /// <summary>A world holding one of each genome given, built into the solver and not yet stepped.</summary>
        internal static Pair Arena(RunConfig config, params Genome[] genomes)
        {
            Pair pair = Found(config);
            foreach (Genome genome in genomes) pair.World.Inoculate(genome, count: 1, heightY: -2.5f);
            pair.Sim.SettleBeforeCheckpoint();
            return pair;
        }

        /// <summary>
        /// <see cref="Arena"/> with the first genome put in with a reserve it cannot live on, so that the first
        /// economy step starves it: <see cref="Starve"/> turns it into whole-body remains (D220), keeping its id.
        /// </summary>
        internal static Pair StarvedArena(RunConfig config, Genome starving, params Genome[] others)
        {
            Pair pair = Found(config);

            float reserve = config.FounderEnergyJoules;
            config.FounderEnergyJoules = 0.001f;
            pair.World.Inoculate(starving, count: 1, heightY: -2.5f);
            config.FounderEnergyJoules = reserve;

            foreach (Genome genome in others) pair.World.Inoculate(genome, count: 1, heightY: -2.5f);
            pair.Sim.SettleBeforeCheckpoint();
            return pair;
        }

        /// <summary>
        /// Steps Core's economy alone, half a second at a time, until the starved body has died into remains; the
        /// solver is not stepped, so every body stands as the test left it.
        /// </summary>
        internal static void Starve(Pair pair)
        {
            for (int i = 0; i < 20 && pair.World.Remains.Count == 0; i++) pair.World.Step(0.5f);
        }

        /// <summary>One structural box with a claw at its cell type's cap, and no brain.</summary>
        internal static Genome Claw()
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(0.25f, 0.25f, 0.25f),
                JointType = JointType.Fixed,
                RecursiveLimit = 1,
                CellTypeId = CellTypeIds.Structural,
                Power = 0f,
                Attack = 1f,
            };
            node.ResampleJointLimits(new Float2(-1f, 1f));

            var genome = new Genome { RootIndex = 0 };
            genome.Nodes.Add(node);
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            genome.AdultScale = 1f;
            return genome;
        }

        /// <summary>One consumer box, a claw and a mouth at their cell type's caps, and no brain.</summary>
        internal static Genome Hunter()
        {
            Genome genome = Claw();
            MorphNode node = genome.Nodes[0];
            node.CellTypeId = CellTypeIds.Consumer;
            node.Intake = CellTypeRegistry.Standard.Resolve(CellTypeIds.Consumer).IntakeMax;
            return genome;
        }

        /// <summary>
        /// A structural root with two hinged spines of <paramref name="spine"/> links each (three by default), one
        /// off its +X face and one off its +Y face: seven parts, the second branch indexed after the first (parts 1
        /// to 3 and 4 to 6).
        /// </summary>
        internal static Genome Fork(int spine = 3)
        {
            var root = new MorphNode
            {
                Dimensions = new Float3(0.1f, 0.1f, 0.1f),
                JointType = JointType.Fixed,
                RecursiveLimit = 1,
                CellTypeId = CellTypeIds.Structural,
                Power = 0f,
            };
            root.ResampleJointLimits(new Float2(-1f, 1f));

            var link = new MorphNode
            {
                Dimensions = new Float3(0.08f, 0.08f, 0.08f),
                JointType = JointType.Hinge,
                RecursiveLimit = spine,
                CellTypeId = CellTypeIds.Link,
                Power = 50f,
            };
            link.ResampleJointLimits(new Float2(-1f, 1f));

            link.Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            root.Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            root.Edges.Add(new MorphEdge
            {
                Child = 1,
                ParentAnchor = new Float3(0f, 1f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.FromAxisAngle(new Float3(0f, 0f, 1f), (float)(Math.PI / 2d)),
                Scale = Float3.One,
            });

            var genome = new Genome { RootIndex = 0 };
            genome.Nodes.Add(root);
            genome.Nodes.Add(link);
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            genome.AdultScale = 1f;
            return genome;
        }

        /// <summary>Puts a body's root at a place, as the seam wrap moves one: poses and contact sphere together.</summary>
        internal static void Place(Creature body, Vec3 at)
        {
            body.BasePosition = at;
            Kinematics.Poses(body);
            body.RefreshContactSphere();
            body.CommitContactSphere();
        }

        /// <summary>
        /// Moves the claw (or hunter) <paramref name="biter"/> against link <paramref name="link"/> of
        /// <paramref name="target"/>, just beyond it along the way the limb points, as <c>BiteRebuildTests</c> does.
        /// </summary>
        internal static void Press(Simulation sim, long biter, long target, int link)
        {
            if (!sim.TryPose(biter, out Creature claw) || !sim.TryPose(target, out Creature victim)) return;
            if (link >= victim.Links) link = victim.Links - 1;

            Vec3 root = Vec3.Read(victim.Position, 0);
            Vec3 limb = Vec3.Read(victim.Position, 3 * link);
            Vec3 outward = limb - root;
            Vec3 along = outward.Magnitude > 1e-9 ? outward.Normalized : new Vec3(1d, 0d, 0d);

            Place(claw, limb + along * 0.2);
        }

        /// <summary>
        /// Gives a body motion: a root velocity and spin, and every joint an angle and a rate, with the
        /// kinematics refreshed so every link moves.
        /// </summary>
        internal static void Excite(Creature body)
        {
            Vec3.Write(body.Velocity, 0, new Vec3(0.3, 0.1, -0.2));
            Vec3.Write(body.Spin, 0, new Vec3(0.05, 0.4, 0.1));

            for (int i = 0; i < body.Q.Length; i++)
            {
                body.Q[i] = 0.2 + 0.05 * i;
                body.Qd[i] = 0.3 - 0.07 * i;
            }

            Kinematics.Poses(body);
            Kinematics.Velocities(body);
        }

        /// <summary>The contact list the harness handed Core on the last metabolic step.</summary>
        internal static List<CreatureContact> Handed(Simulation sim) =>
            (List<CreatureContact>)typeof(Simulation)
                .GetField("_contacts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(sim);
    }
}

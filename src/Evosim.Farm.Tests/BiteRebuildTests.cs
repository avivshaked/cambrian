using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A body that loses a part to a bite steps on its new plan from the next physics step
    /// (round 49). The harness rebuilds it straight after the metabolic step that changed the
    /// plan, before any physics step, and not at the next growth step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The window these tests close.</b> Core's mouth takes a part off inside
    /// <c>World.Step</c>, and until round 49 the farm rebuilt the body's solver only at the next
    /// growth step, up to ten simulated seconds later. For those seconds the solver stepped the
    /// old plan while the organism held the new one. The contact list named the old solver's
    /// links and Core applied the indices to the new plan's parts, so a bite could land on the
    /// wrong part. The contact and damage senses the brain read were indexed by the new plan and
    /// read by the old solver's links. A checkpoint taken in the window could not be restored.
    /// </para>
    /// <para>
    /// <b>The bite world</b> is <c>CheckpointRestoreTests</c>' crowded box with eight forked,
    /// jointed victims and eight claws put in after the founding, and each claw held against a
    /// victim before every contact census until metabolic step 150 (<see cref="StepBiting"/>
    /// says why and how). Health is left at its default of 1 per cubic metre, so a capped claw
    /// takes any part it touches in one metabolic step, and a victim loses one part on one step
    /// and another on the next. A fork's first branch is indexed before its second, so a part
    /// lost from the first moves every index of the second.
    /// </para>
    /// <para>
    /// <b>And the worlds with no bite</b> step exactly as they did before the rebuild moved.
    /// Their digests were pinned on the tree the fix was built on (<c>1f06a67</c>'s code, the
    /// test added in <c>72b3087</c>), where the four bite tests fail. The module world's state
    /// then moved with the ruling that a plan change carries the step's records, and its
    /// trajectory did not (<see cref="AWorldWithNoBiteStepsAsItDidBefore"/>).
    /// </para>
    /// </remarks>
    public class BiteRebuildTests
    {
        private readonly ITestOutputHelper _out;

        public BiteRebuildTests(ITestOutputHelper output) => _out = output;

        // ------------------------------------------------------------------ the worlds

        /// <summary>The metabolic step after which the inoculants are put in.</summary>
        private const int InoculateAt = 30;

        private sealed class Pair : IDisposable
        {
            public World World;
            public Simulation Sim;

            public void Dispose() => Sim?.Dispose();
        }

        private static Pair Found(RunConfig config)
        {
            var world = new World(config, 1UL);

            return new Pair
            {
                World = world,
                Sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: null),
            };
        }

        /// <summary>
        /// One physics step of a world with no bite, with its inoculants put in after metabolic
        /// step <see cref="InoculateAt"/>. True on the steps the economy ran.
        /// </summary>
        private static bool Step(Pair pair, ref int metabolic, Action<Pair> inoculate)
        {
            if (!pair.Sim.Step()) return false;

            metabolic++;
            if (metabolic == InoculateAt) inoculate?.Invoke(pair);

            return true;
        }

        /// <summary>
        /// One physics step of the bite world: the forks and the claws put in after metabolic
        /// step <see cref="InoculateAt"/>, and from then until <see cref="LastAttack"/> every
        /// claw held against a victim for each metabolic step's contact census. True on the steps
        /// the economy ran.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why the claws are moved.</b> The placer keeps newborns apart and the box is full,
        /// and the soft push parts two bodies within the half-second between two censuses, so
        /// left to the current a claw bites something a few times a minute and almost never on
        /// two steps running. So on the last physics step before each metabolic step, each claw
        /// is moved against a living body of two or more parts, as the seam wrap moves a body:
        /// its root and its poses, with its contact sphere committed.
        /// </para>
        /// <para>
        /// <b>Where.</b> Just beyond the body's link 1 (a fork's first branch, a founder's first
        /// limb), read off the solver, so the bite takes a part whose loss moves the indices of
        /// the parts after it. The same claw goes against the same body on the next step, which
        /// is the step after a bite: on the tree before round 49 the solver still held the old
        /// plan then, and the claw was put against, and named, one of the old plan's links.
        /// </para>
        /// </remarks>
        private static bool StepBiting(Pair pair, ref int metabolic) =>
            StepBiting(pair, ref metabolic, Fork, Attack);

        /// <summary>
        /// <see cref="StepBiting(Pair, ref int)"/> with the victims' genome and the attack named:
        /// the tough forks and the paired claws of the records test.
        /// </summary>
        private static bool StepBiting(
            Pair pair, ref int metabolic, Func<Genome> victim, Action<Pair> attack)
        {
            bool censusNext = (pair.Sim.Steps + 1) % pair.Sim.StepsPerMetabolicStep == 0;
            if (censusNext && metabolic >= InoculateAt && metabolic < LastAttack) attack(pair);

            if (!pair.Sim.Step()) return false;

            metabolic++;

            if (metabolic == InoculateAt)
            {
                pair.World.Inoculate(victim(), count: 8, heightY: -2.5f);
                pair.World.Inoculate(Claw(), count: 8, heightY: -2.5f);
            }

            return true;
        }

        /// <summary>The metabolic step after which the claws are left to the current.</summary>
        private const int LastAttack = 150;

        private static void Attack(Pair pair)
        {
            var claws = new List<Creature>();
            var victims = new List<Creature>();

            foreach (Organism o in pair.World.Living)
            {
                if (!pair.Sim.TryPose(o.Id, out Creature body)) continue;

                if (o.HasAttack) claws.Add(body);
                else if (body.Links >= 2) victims.Add(body);
            }

            for (int i = 0; i < claws.Count && i < victims.Count; i++)
            {
                Creature claw = claws[i];
                Creature victim = victims[i];

                Vec3 root = Vec3.Read(victim.Position, 0);
                Vec3 limb = Vec3.Read(victim.Position, 3);
                Vec3 outward = limb - root;
                Vec3 along = outward.Magnitude > 1e-9 ? outward.Normalized : new Vec3(1d, 0d, 0d);

                claw.BasePosition = limb + along * 0.2;
                Kinematics.Poses(claw);
                claw.RefreshContactSphere();
                claw.CommitContactSphere();
            }
        }

        /// <summary>
        /// Two claws to a victim: one just beyond its link 1, as <see cref="Attack"/> puts one, and
        /// one on the far side of its root, so that on the step a limb comes off the root takes a
        /// blow it survives (<see cref="WoundedWorld"/> and <see cref="ToughFork"/> size the two).
        /// </summary>
        private static void AttackInPairs(Pair pair)
        {
            var claws = new List<Creature>();
            var victims = new List<Creature>();

            foreach (Organism o in pair.World.Living)
            {
                if (!pair.Sim.TryPose(o.Id, out Creature body)) continue;

                if (o.HasAttack) claws.Add(body);
                else if (body.Links >= 2) victims.Add(body);
            }

            for (int i = 0; 2 * i + 1 < claws.Count && i < victims.Count; i++)
            {
                Creature victim = victims[i];

                Vec3 root = Vec3.Read(victim.Position, 0);
                Vec3 limb = Vec3.Read(victim.Position, 3);
                Vec3 outward = limb - root;
                Vec3 along = outward.Magnitude > 1e-9 ? outward.Normalized : new Vec3(1d, 0d, 0d);

                Place(claws[2 * i], limb + along * 0.2);
                Place(claws[2 * i + 1], root - along * 0.3);
            }
        }

        private static void Place(Creature claw, Vec3 at)
        {
            claw.BasePosition = at;
            Kinematics.Poses(claw);
            claw.RefreshContactSphere();
            claw.CommitContactSphere();
        }

        /// <summary>
        /// The touching world with health three hundred times as deep, so a claw needs two
        /// metabolic steps to take a fork's limb and many to take a tough root. Nothing heals.
        /// </summary>
        private static RunConfig WoundedWorld()
        {
            RunConfig config = CheckpointRestoreTests.TouchingWorld();
            config.HealthPerCubicMetre = 300f;
            return config;
        }

        private static void Leaves(Pair pair)
        {
            pair.World.Inoculate(IndeterminateLeaf(), count: 8, heightY: -2.5f);
        }

        /// <summary>The touching world with the module rule switched on.</summary>
        private static RunConfig ModuleWorld()
        {
            RunConfig config = CheckpointRestoreTests.TouchingWorld();
            config.ModuleAddReserveSeconds = 1f;
            return config;
        }

        /// <summary>One structural box with a claw at its cell type's cap, and no brain.</summary>
        private static Genome Claw()
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

        /// <summary>
        /// A structural root with two hinged spines of three links each, one off its +X face and
        /// one off its +Y face: seven parts, the second branch indexed after the first.
        /// </summary>
        private static Genome Fork()
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
                RecursiveLimit = 3,
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

        /// <summary>
        /// <see cref="Fork"/> with its root at structural tissue's toughest, four times the pool of
        /// its own volume, so the root outlasts a claw that takes a limb in two steps.
        /// </summary>
        private static Genome ToughFork()
        {
            Genome genome = Fork();
            genome.Nodes[0].Toughness = 4f;
            return genome;
        }

        /// <summary>
        /// One photosynthetic box whose count is a rule: room for three more if the body can pay
        /// for them (D106 item 2).
        /// </summary>
        private static Genome IndeterminateLeaf()
        {
            var node = new MorphNode
            {
                Dimensions = new Float3(0.2f, 0.2f, 0.2f),
                JointType = JointType.Fixed,
                RecursiveLimit = 1,
                CellTypeId = CellTypeIds.Photosynthetic,
                Power = 0f,
                Growth = ModuleGrowth.Indeterminate,
                MaxModules = 4,
            };

            node.Edges.Add(new MorphEdge
            {
                Child = 0,
                ParentAnchor = new Float3(1f, 0f, 0f),
                ChildAnchor = new Float3(-1f, 0f, 0f),
                Orientation = Quat.Identity,
                Scale = Float3.One,
            });

            var genome = new Genome { RootIndex = 0 };
            genome.Nodes.Add(node);
            genome.Reproduction = new ReproductionTraits { BroodSize = 1, BirthInvestment = 2f };
            genome.AdultScale = 1f;
            return genome;
        }

        // ------------------------------------------------------------------ the readings

        private static Dictionary<long, Organism> LivingById(World world)
        {
            var byId = new Dictionary<long, Organism>();
            foreach (Organism o in world.Living) byId[o.Id] = o;
            return byId;
        }

        private static Dictionary<long, int> Revisions(World world)
        {
            var revisions = new Dictionary<long, int>();
            foreach (Organism o in world.Living) revisions[o.Id] = o.PlanRevision;
            return revisions;
        }

        /// <summary>Whether this metabolic step ran the growth step, read off the harness's clock.</summary>
        private static bool GrewThisStep(Simulation sim) =>
            (float)typeof(Simulation)
                .GetField("_sinceGrowthStep", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(sim) == 0f;

        /// <summary>The contact list the harness handed Core on the last metabolic step.</summary>
        private static List<CreatureContact> Handed(Simulation sim) =>
            (List<CreatureContact>)typeof(Simulation)
                .GetField("_contacts", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(sim);

        /// <summary>
        /// Null when part <paramref name="index"/> of the solver's plan is part
        /// <paramref name="index"/> of <paramref name="plan"/>: the same node, at the same place
        /// in the tree, on the same side of a mirror. Otherwise what differs.
        /// </summary>
        private static string SamePart(Phenotype built, Phenotype plan, int index)
        {
            if (index < 0 || index >= built.PartCount || index >= plan.PartCount)
            {
                return FormattableString.Invariant(
                    $"part {index} of a solver plan of {built.PartCount} and an organism plan of {plan.PartCount}");
            }

            PhenotypePart a = built.Parts[index];
            PhenotypePart b = plan.Parts[index];

            if (a.SourceNode != b.SourceNode || a.ParentIndex != b.ParentIndex ||
                a.Depth != b.Depth || a.Mirrored != b.Mirrored || a.CellTypeId != b.CellTypeId)
            {
                return FormattableString.Invariant(
                    $"part {index}: node {a.SourceNode} under {a.ParentIndex} in the solver, ") +
                    FormattableString.Invariant($"node {b.SourceNode} under {b.ParentIndex} in the organism");
            }

            return null;
        }

        /// <summary>What a body stood on and had lost before a metabolic step.</summary>
        private sealed class Stood
        {
            public int Revision;
            public Phenotype Plan;
            public float[] Health;
            public List<int[]> Lost;
            public int[] Counts;
        }

        private static Dictionary<long, Stood> Capture(World world)
        {
            var stood = new Dictionary<long, Stood>();

            foreach (Organism o in world.Living)
            {
                stood[o.Id] = new Stood
                {
                    Revision = o.PlanRevision,
                    Plan = o.Phenotype,
                    Health = o.PartHealth == null ? null : (float[])o.PartHealth.Clone(),
                    Lost = o.LostPartPaths == null ? null : new List<int[]>(o.LostPartPaths),
                    Counts = (int[])CountsOf(o).Clone(),
                };
            }

            return stood;
        }

        /// <summary>A body's module counts, at its genome's minimum where it has none: Core's rule.</summary>
        private static int[] CountsOf(Organism o)
        {
            List<MorphNode> nodes = o.Genome.Nodes;
            int[] counts = o.ModuleCounts;

            if (counts != null && counts.Length == nodes.Count) return counts;

            var fresh = new int[nodes.Count];
            for (int n = 0; n < nodes.Count; n++)
            {
                fresh[n] = nodes[n].RecursiveLimit;
                if (counts != null && n < counts.Length && counts[n] > fresh[n]) fresh[n] = counts[n];
            }

            return fresh;
        }

        /// <summary>
        /// The developer's path to each part of a plan, with the lost parts' subtrees taken out:
        /// what Core matches two plans on, derived here from the public developer because the
        /// harness takes Core's own map.
        /// </summary>
        private static List<int[]> PlanPaths(
            Organism o, RunConfig config, int[] counts, List<int[]> lost)
        {
            var paths = new List<int[]>();
            Phenotype whole = Developer.Develop(
                o.Genome, config.Development, null, config.Shapes, counts, paths);

            if (lost == null || lost.Count == 0) return paths;

            var drop = new bool[whole.PartCount];
            for (int i = 0; i < drop.Length; i++)
            {
                foreach (int[] gone in lost)
                {
                    if (gone.Length != paths[i].Length) continue;

                    bool same = true;
                    for (int k = 0; k < gone.Length && same; k++) same = gone[k] == paths[i][k];
                    if (same) drop[i] = true;
                }
            }

            whole.WithoutSubtrees(drop, out int[] kept);

            var survived = new List<int[]>(kept.Length);
            foreach (int k in kept) survived.Add(paths[k]);
            return survived;
        }

        /// <summary>Where each part of a body's new plan stood in the plan it had before the step.</summary>
        private static int[] MapAcross(Organism o, Stood was, RunConfig config)
        {
            List<int[]> from = PlanPaths(o, config, was.Counts, was.Lost);
            List<int[]> to = PlanPaths(o, config, CountsOf(o), o.LostPartPaths);

            Assert.Equal(was.Plan.PartCount, from.Count);
            Assert.Equal(o.Phenotype.PartCount, to.Count);

            return Developer.MatchParts(from, to);
        }

        /// <summary>
        /// The parts of body <paramref name="id"/> the list names, by the index it gives them;
        /// only against a partner still living when <paramref name="livingPartner"/> is set.
        /// </summary>
        private static HashSet<int> Named(
            List<CreatureContact> list, long id, Dictionary<long, Organism> living, bool livingPartner)
        {
            var parts = new HashSet<int>();

            foreach (CreatureContact c in list)
            {
                if (c.BodyA == id && (!livingPartner || living.ContainsKey(c.BodyB))) parts.Add(c.PartA);
                if (c.BodyB == id && (!livingPartner || living.ContainsKey(c.BodyA))) parts.Add(c.PartB);
            }

            return parts;
        }

        /// <summary>
        /// Asserts that a changed body's contact record is the step's contacts carried through the
        /// map: a flag only where the list named the part it stood as, and every such part
        /// flagged whose partner lived through the step. Returns how many flags were carried.
        /// </summary>
        private static int AssertContactCarried(
            Organism o, int[] map, List<CreatureContact> list, Dictionary<long, Organism> living,
            string where)
        {
            HashSet<int> named = Named(list, o.Id, living, livingPartner: false);
            HashSet<int> namedLiving = Named(list, o.Id, living, livingPartner: true);
            bool[] contact = o.PartContact;
            int carried = 0;

            if (contact == null)
            {
                for (int i = 0; i < map.Length; i++)
                {
                    Assert.False(
                        map[i] >= 0 && namedLiving.Contains(map[i]),
                        where + FormattableString.Invariant(
                            $": body {o.Id} holds no contact record and its part {i} was named as {map[i]}"));
                }

                return 0;
            }

            Assert.Equal(o.Phenotype.PartCount, contact.Length);

            for (int i = 0; i < contact.Length; i++)
            {
                bool wasNamed = map[i] >= 0 && named.Contains(map[i]);
                bool mustBe = map[i] >= 0 && namedLiving.Contains(map[i]);

                Assert.True(
                    !contact[i] || wasNamed,
                    where + FormattableString.Invariant(
                        $": body {o.Id} part {i} (was {map[i]}) reads a contact the list never named"));
                Assert.True(
                    contact[i] || !mustBe,
                    where + FormattableString.Invariant(
                        $": body {o.Id} part {i} (was {map[i]}) was named and reads no contact"));

                if (contact[i]) carried++;
            }

            return carried;
        }

        /// <summary>Null when a body's solver is built on its organism's plan; otherwise how not.</summary>
        private static string OffPlan(Creature solver, Organism creature)
        {
            if (solver.AppliedPlanRevision != creature.PlanRevision)
            {
                return FormattableString.Invariant(
                    $"solver on plan revision {solver.AppliedPlanRevision}, organism on {creature.PlanRevision}");
            }

            if (solver.Links != creature.Phenotype.PartCount)
            {
                return FormattableString.Invariant(
                    $"{solver.Links} links against {creature.Phenotype.PartCount} parts");
            }

            for (int p = 0; p < solver.Links; p++)
            {
                string differs = SamePart(solver.Phenotype, creature.Phenotype, p);
                if (differs != null) return differs;
            }

            return null;
        }

        // ------------------------------------------------------------------ the rebuild

        /// <summary>
        /// After every metabolic step, every living body's solver is built on its organism's plan,
        /// and a body that lost a part on a step that ran no growth step is among them.
        /// </summary>
        [Fact]
        public void ABittenBodyStepsOnItsNewPlanFromTheNextPhysicsStep()
        {
            using (Pair pair = Found(CheckpointRestoreTests.TouchingWorld()))
            {
                int metabolic = 0;
                int bodiesChecked = 0;
                int changedOffTheGrowthStep = 0;
                Dictionary<long, int> before = null;

                for (int step = 0; step < 12_000 && metabolic < 240; step++)
                {
                    if ((pair.Sim.Steps + 1) % pair.Sim.StepsPerMetabolicStep == 0)
                    {
                        before = Revisions(pair.World);
                    }

                    if (!StepBiting(pair, ref metabolic)) continue;

                    bool grew = GrewThisStep(pair.Sim);

                    foreach (Organism o in pair.World.Living)
                    {
                        if (!pair.Sim.TryPose(o.Id, out Creature solver)) continue;   // a newborn

                        bodiesChecked++;

                        if (!grew && before.TryGetValue(o.Id, out int was) && was != o.PlanRevision)
                        {
                            changedOffTheGrowthStep++;
                        }

                        string off = OffPlan(solver, o);
                        Assert.True(
                            off == null,
                            FormattableString.Invariant(
                                $"t={pair.World.ElapsedSeconds}: body {o.Id} is not on its plan after the ") +
                            "metabolic step: " + off);
                    }
                }

                _out.WriteLine(
                    FormattableString.Invariant(
                        $"{metabolic} metabolic steps to t={pair.World.ElapsedSeconds} s: {bodiesChecked} ") +
                    FormattableString.Invariant(
                        $"body-steps on their plans, {changedOffTheGrowthStep} plan changes off the ") +
                    FormattableString.Invariant(
                        $"growth step, {pair.World.PartsKilled} parts killed, {pair.Sim.ModuleRebuilds} rebuilds"));

                Assert.True(
                    changedOffTheGrowthStep > 0,
                    "no body lost a part on a step without a growth step; the fixture tests nothing");
            }
        }

        /// <summary>
        /// On the metabolic step after a body lost a part, every contact the harness hands Core
        /// for it names, by index, the part the organism holds at that index.
        /// </summary>
        /// <remarks>
        /// The contact list is built from the solver's links and Core reads each index as a part
        /// of the organism's plan, so the two plans agreeing at the named index is what lands the
        /// bite on the part the physics touched. The plans are read at the close of the step
        /// before, which is what the physics steps between the two and the hand-over see.
        /// </remarks>
        [Fact]
        public void ABiteOnTheStepAfterABiteLandsOnThePartTheContactNamed()
        {
            using (Pair pair = Found(CheckpointRestoreTests.TouchingWorld()))
            {
                int metabolic = 0;
                int contactsChecked = 0;
                int afterAPlanChange = 0;

                Dictionary<long, int> before = null;
                var solverPlans = new Dictionary<long, Phenotype>();
                var organismPlans = new Dictionary<long, Phenotype>();
                var changedLastStep = new HashSet<long>();

                for (int step = 0; step < 12_000 && metabolic < 240; step++)
                {
                    if ((pair.Sim.Steps + 1) % pair.Sim.StepsPerMetabolicStep == 0)
                    {
                        before = Revisions(pair.World);
                    }

                    if (!StepBiting(pair, ref metabolic)) continue;

                    // The list this step handed Core, against the plans that stood when it was
                    // built: the ones read at the close of the last step.
                    foreach (CreatureContact c in Handed(pair.Sim))
                    {
                        foreach ((long id, int part) in new[] { (c.BodyA, c.PartA), (c.BodyB, c.PartB) })
                        {
                            if (!solverPlans.TryGetValue(id, out Phenotype built)) continue;
                            if (!organismPlans.TryGetValue(id, out Phenotype plan)) continue;

                            contactsChecked++;
                            if (changedLastStep.Contains(id)) afterAPlanChange++;

                            string differs = SamePart(built, plan, part);
                            Assert.True(
                                differs == null,
                                FormattableString.Invariant(
                                    $"t={pair.World.ElapsedSeconds}: a contact on body {id} names ") +
                                (changedLastStep.Contains(id) ? "(one step after its plan changed) " : string.Empty) +
                                differs);
                        }
                    }

                    // And the plans for the next step's list.
                    solverPlans.Clear();
                    organismPlans.Clear();
                    changedLastStep.Clear();

                    foreach (Organism o in pair.World.Living)
                    {
                        if (!pair.Sim.TryPose(o.Id, out Creature solver)) continue;

                        solverPlans[o.Id] = solver.Phenotype;
                        organismPlans[o.Id] = o.Phenotype;

                        if (before.TryGetValue(o.Id, out int was) && was != o.PlanRevision)
                        {
                            changedLastStep.Add(o.Id);
                        }
                    }
                }

                _out.WriteLine(
                    FormattableString.Invariant(
                        $"{metabolic} metabolic steps to t={pair.World.ElapsedSeconds} s: {contactsChecked} ") +
                    FormattableString.Invariant(
                        $"contact sides checked, {afterAPlanChange} on a body whose plan changed the step ") +
                    FormattableString.Invariant(
                        $"before, {pair.World.PartsKilled} parts killed"));

                Assert.True(
                    afterAPlanChange > 0,
                    "no contact was handed for a body on the step after its plan changed; the " +
                    "fixture tests nothing");
            }
        }

        /// <summary>
        /// After every metabolic step, a body's contact and damage senses are its organism's
        /// arrays and are as long as its solver has links, including a body bitten a step ago.
        /// </summary>
        [Fact]
        public void TheSensesABrainReadsAreIndexedByItsSolversPlan()
        {
            using (Pair pair = Found(CheckpointRestoreTests.TouchingWorld()))
            {
                int metabolic = 0;
                int sensesChecked = 0;
                int onANewPlan = 0;

                Dictionary<long, int> before = null;
                var changedRecently = new Dictionary<long, int>();

                for (int step = 0; step < 12_000 && metabolic < 240; step++)
                {
                    if ((pair.Sim.Steps + 1) % pair.Sim.StepsPerMetabolicStep == 0)
                    {
                        before = Revisions(pair.World);
                    }

                    if (!StepBiting(pair, ref metabolic)) continue;

                    foreach (Organism o in pair.World.Living)
                    {
                        if (!pair.Sim.TryPose(o.Id, out Creature solver)) continue;

                        if (before.TryGetValue(o.Id, out int was) && was != o.PlanRevision)
                        {
                            changedRecently[o.Id] = metabolic;
                        }

                        Assert.Same(o.PartContact, solver.Senses.Contact);
                        Assert.Same(o.PartDamage, solver.Senses.Damage);

                        bool[] contact = solver.Senses.Contact;
                        float[] damage = solver.Senses.Damage;

                        if (contact != null)
                        {
                            sensesChecked++;
                            Assert.True(
                                contact.Length == solver.Links,
                                FormattableString.Invariant(
                                    $"t={pair.World.ElapsedSeconds}: body {o.Id} senses a contact array of ") +
                                FormattableString.Invariant($"{contact.Length} parts with {solver.Links} links"));
                        }

                        if (damage != null)
                        {
                            sensesChecked++;
                            Assert.True(
                                damage.Length == solver.Links,
                                FormattableString.Invariant(
                                    $"t={pair.World.ElapsedSeconds}: body {o.Id} senses a damage array of ") +
                                FormattableString.Invariant($"{damage.Length} parts with {solver.Links} links"));
                        }

                        // A body whose plan changed within the last few steps and has sensed
                        // something since: the case the window got wrong.
                        if ((contact != null || damage != null) &&
                            changedRecently.TryGetValue(o.Id, out int at) && at < metabolic &&
                            metabolic - at <= 4)
                        {
                            onANewPlan++;
                        }
                    }
                }

                _out.WriteLine(
                    FormattableString.Invariant(
                        $"{metabolic} metabolic steps to t={pair.World.ElapsedSeconds} s: {sensesChecked} ") +
                    FormattableString.Invariant(
                        $"sense arrays checked, {onANewPlan} held by a body within four steps of a plan change"));

                Assert.True(
                    onANewPlan > 0,
                    "no body sensed anything within four steps of losing a part; the fixture tests nothing");
            }
        }

        // ------------------------------------------------------------------ the records carried

        private static float Clamp(float v) => v < -1f ? -1f : v > 1f ? 1f : v;

        /// <summary>
        /// A body bitten on metabolic step n reads, on every physics step of step n+1, what its
        /// surviving parts felt on step n, on the new plan's indices: the loss on each, and contact
        /// where the list named it. The lost part's entries are gone. And on step n+1 the list the
        /// harness hands over, built from the rebuilt solver, agrees with the records it writes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The ruling (round 49).</b> D123 says the damage channel reads the share of its pool
        /// a part lost on the last metabolic step, and the contact channel whether it touched on
        /// that step. Until this change a plan change dropped both, so a body read nothing on
        /// exactly the step a part came off. Core now carries both through the part map, and the
        /// harness hands the rebuilt body the carried arrays.
        /// </para>
        /// <para>
        /// <b>The world.</b> <see cref="WoundedWorld"/> with <see cref="ToughFork"/>s and two claws
        /// to each (<see cref="AttackInPairs"/>). A limb takes two steps to come off, and on the
        /// step it does the root takes a blow it survives, so a bitten body has a surviving part
        /// with a loss and a contact to carry. The map is derived from the public developer
        /// (<see cref="MapAcross"/>). The loss is the health before the step less the health after
        /// it, since nothing heals.
        /// </para>
        /// </remarks>
        [Fact]
        public void ABittenBodyReadsWhatItsSurvivingPartsFeltOnTheNextPhysicsSteps()
        {
            RunConfig config = WoundedWorld();
            Assert.Equal(0f, config.HealingPerSecond);

            using (Pair pair = Found(config))
            {
                int metabolic = 0;
                int bitten = 0, wounded = 0, touched = 0, reads = 0, agreed = 0;

                Dictionary<long, Stood> before = null;

                // The bodies bitten on the last metabolic step, with what their records read.
                var watching = new Dictionary<long, (bool[] Contact, float[] Damage)>();

                for (int step = 0; step < 12_000 && metabolic < 240; step++)
                {
                    if ((pair.Sim.Steps + 1) % pair.Sim.StepsPerMetabolicStep == 0)
                    {
                        before = Capture(pair.World);
                    }

                    if (!StepBiting(pair, ref metabolic, ToughFork, AttackInPairs))
                    {
                        // A physics step of step n+1: what each bitten body's brain reads.
                        foreach (KeyValuePair<long, (bool[] Contact, float[] Damage)> w in watching)
                        {
                            if (!pair.Sim.TryPose(w.Key, out Creature solver)) continue;

                            for (int p = 0; p < solver.Links; p++)
                            {
                                float touch = w.Value.Contact != null && w.Value.Contact[p] ? 1f : 0f;
                                float hurt = Clamp(w.Value.Damage[p]);

                                Assert.Equal(touch, solver.Senses.Read(p, SensorChannel.Contact, 0));
                                Assert.Equal(hurt, solver.Senses.Read(p, SensorChannel.Damage, 0));
                                reads++;
                            }
                        }

                        continue;
                    }

                    Dictionary<long, Organism> living = LivingById(pair.World);
                    List<CreatureContact> handed = Handed(pair.Sim);
                    string where = FormattableString.Invariant($"t={pair.World.ElapsedSeconds}");

                    // Step n+1's list, built from the rebuilt solvers, against the records it wrote,
                    // for each body bitten on step n that kept its plan through this step. One that
                    // was bitten again is read below, through its new map.
                    foreach (long id in watching.Keys)
                    {
                        if (!living.TryGetValue(id, out Organism o)) continue;
                        if (!before.TryGetValue(id, out Stood stood) || stood.Revision != o.PlanRevision) continue;

                        var same = new int[o.Phenotype.PartCount];
                        for (int i = 0; i < same.Length; i++) same[i] = i;

                        foreach (int part in Named(handed, id, living, livingPartner: false))
                        {
                            Assert.True(
                                part >= 0 && part < o.Phenotype.PartCount,
                                where + FormattableString.Invariant(
                                    $": the list names part {part} of body {id}, which has {o.Phenotype.PartCount}"));
                        }

                        AssertContactCarried(o, same, handed, living, where + " (the step after a bite)");
                        agreed++;
                    }

                    watching.Clear();

                    foreach (Organism o in pair.World.Living)
                    {
                        if (!before.TryGetValue(o.Id, out Stood was)) continue;
                        if (was.Revision == o.PlanRevision) continue;
                        if (!pair.Sim.TryPose(o.Id, out Creature solver)) continue;

                        bitten++;
                        int[] map = MapAcross(o, was, config);

                        // Contact: the step's list, carried through the map.
                        if (AssertContactCarried(o, map, handed, living, where) > 0) touched++;

                        // Damage: a survivor's loss is its health before the step less its health
                        // after, on the part it stood as; a part new to the plan was not there to
                        // be hurt.
                        float[] damage = o.PartDamage;
                        Assert.NotNull(damage);
                        Assert.Equal(o.Phenotype.PartCount, damage.Length);

                        bool anyLoss = false;

                        for (int i = 0; i < damage.Length; i++)
                        {
                            float expected = 0f;

                            if (map[i] >= 0)
                            {
                                float then = was.Health != null ? was.Health[map[i]] : 1f;
                                float now = o.PartHealth != null ? o.PartHealth[i] : 1f;
                                expected = then - now;
                            }

                            Assert.True(
                                Math.Abs(expected - damage[i]) <= 1e-6f,
                                where + FormattableString.Invariant(
                                    $": body {o.Id} part {i} (was {map[i]}) reads a loss of {damage[i]:R} ") +
                                FormattableString.Invariant($"where its health says {expected:R}"));

                            if (damage[i] > 0f) anyLoss = true;
                        }

                        if (anyLoss) wounded++;

                        // What the brain reads is these arrays, by reference, on the plan its solver
                        // was built on.
                        Assert.Null(OffPlan(solver, o));
                        Assert.Same(o.PartContact, solver.Senses.Contact);
                        Assert.Same(o.PartDamage, solver.Senses.Damage);

                        watching[o.Id] = (
                            o.PartContact == null ? null : (bool[])o.PartContact.Clone(),
                            (float[])damage.Clone());
                    }
                }

                _out.WriteLine(
                    FormattableString.Invariant(
                        $"{metabolic} metabolic steps to t={pair.World.ElapsedSeconds} s, {pair.World.PartsKilled} parts ") +
                    FormattableString.Invariant(
                        $"killed: {bitten} plan changes read, {wounded} carrying a loss and {touched} a contact on a ") +
                    FormattableString.Invariant(
                        $"surviving part; {reads} part-readings on the physics steps after; {agreed} lists read the step after"));

                Assert.True(bitten > 0, "no body lost a part; the fixture tests nothing");
                Assert.True(wounded > 0, "no bitten body carried a loss on a surviving part; the fixture tests nothing");
                Assert.True(touched > 0, "no bitten body carried a contact on a surviving part; the fixture tests nothing");
                Assert.True(reads > 0, "no physics step read a bitten body's senses; the fixture tests nothing");
                Assert.True(agreed > 0, "no list was read on the step after a bite; the fixture tests nothing");
            }
        }

        /// <summary>
        /// The module rule's half of the ruling: a body rebuilt on a new plan at the growth step is
        /// handed the records Core carried onto that plan and reads them on the physics steps that
        /// follow, a standing part's contact where the step's list named it and a new part reading
        /// nothing.
        /// </summary>
        /// <remarks>
        /// The growth step's rebuild comes after the step's hand-back, so until round 49 a body
        /// rebuilt there sensed nothing until the next metabolic step, and Core had dropped its
        /// records anyway. The module world's leaves have no brain, so what they read moves
        /// nothing, which is why the world's trajectory is the one it was
        /// (<see cref="AWorldWithNoBiteStepsAsItDidBefore"/>). A body with a brain and a module
        /// gene reads the carried contact from this build on.
        /// </remarks>
        [Fact]
        public void AModuleRebuildHandsTheBodyTheRecordsCarriedOntoItsNewPlan()
        {
            RunConfig config = ModuleWorld();

            using (Pair pair = Found(config))
            {
                int metabolic = 0;
                int rebuilt = 0, carried = 0, newParts = 0, reads = 0;

                Dictionary<long, Stood> before = null;
                var watching = new Dictionary<long, bool[]>();

                for (int step = 0; step < 4_000; step++)
                {
                    if ((pair.Sim.Steps + 1) % pair.Sim.StepsPerMetabolicStep == 0)
                    {
                        before = Capture(pair.World);
                    }

                    if (!Step(pair, ref metabolic, Leaves))
                    {
                        foreach (KeyValuePair<long, bool[]> w in watching)
                        {
                            if (!pair.Sim.TryPose(w.Key, out Creature solver)) continue;

                            for (int p = 0; p < solver.Links; p++)
                            {
                                float touch = w.Value != null && w.Value[p] ? 1f : 0f;
                                Assert.Equal(touch, solver.Senses.Read(p, SensorChannel.Contact, 0));
                                reads++;
                            }
                        }

                        continue;
                    }

                    watching.Clear();

                    Dictionary<long, Organism> living = LivingById(pair.World);
                    List<CreatureContact> handed = Handed(pair.Sim);
                    string where = FormattableString.Invariant($"t={pair.World.ElapsedSeconds}");

                    foreach (Organism o in pair.World.Living)
                    {
                        if (!before.TryGetValue(o.Id, out Stood was)) continue;
                        if (was.Revision == o.PlanRevision) continue;
                        if (!pair.Sim.TryPose(o.Id, out Creature solver)) continue;

                        rebuilt++;
                        int[] map = MapAcross(o, was, config);

                        for (int i = 0; i < map.Length; i++) if (map[i] < 0) newParts++;

                        // A new part is never named as a part it stood as, so this also asserts that
                        // it reads no contact.
                        if (AssertContactCarried(o, map, handed, living, where) > 0) carried++;

                        Assert.Null(OffPlan(solver, o));
                        Assert.Same(o.PartContact, solver.Senses.Contact);
                        Assert.Same(o.PartDamage, solver.Senses.Damage);

                        watching[o.Id] = o.PartContact == null ? null : (bool[])o.PartContact.Clone();
                    }
                }

                _out.WriteLine(
                    FormattableString.Invariant(
                        $"{metabolic} metabolic steps to t={pair.World.ElapsedSeconds} s: {rebuilt} module rebuilds read, ") +
                    FormattableString.Invariant(
                        $"{carried} carrying a contact, {newParts} new parts; {reads} part-readings on the physics steps after"));

                Assert.True(rebuilt > 0, "no plan changed; the fixture tests nothing");
                Assert.True(carried > 0, "no rebuilt body carried a contact; the fixture tests nothing");
                Assert.True(newParts > 0, "no module was added; the fixture tests nothing");
                Assert.True(reads > 0, "no physics step read a rebuilt body's senses; the fixture tests nothing");
            }
        }

        // ------------------------------------------------------------------ the checkpoint

        private static byte[] Checkpoint(Pair pair)
        {
            pair.Sim.SettleBeforeCheckpoint();

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    pair.World.WriteState(w);
                    pair.Sim.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        private static Pair Restore(byte[] state, RunConfig config)
        {
            Pair pair = Found(config);

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                pair.World.ReadState(r);
                pair.Sim.ReadState(r);

                Assert.Equal(state.Length, buffer.Position);
            }

            return pair;
        }

        private static string WorldDigest(World world)
        {
            using (var buffer = new MemoryStream())
            using (var w = new BinaryWriter(buffer))
            using (SHA256 sha = SHA256.Create())
            {
                world.WriteState(w);
                w.Flush();
                return Convert.ToBase64String(sha.ComputeHash(buffer.ToArray()));
            }
        }

        /// <summary>
        /// A checkpoint taken on the metabolic step a body lost a part, with no growth step on it,
        /// restores; the restored pair steps as the live one does; and the fidelity check passes on
        /// the file.
        /// </summary>
        [Fact]
        public void ACheckpointTakenRightAfterABiteRestores()
        {
            RunConfig config = CheckpointRestoreTests.TouchingWorld();

            using (Pair live = Found(config))
            {
                int metabolic = 0;
                long bitten = -1;
                Dictionary<long, int> before = null;

                for (int step = 0; step < 12_000 && bitten < 0; step++)
                {
                    if ((live.Sim.Steps + 1) % live.Sim.StepsPerMetabolicStep == 0)
                    {
                        before = Revisions(live.World);
                    }

                    if (!StepBiting(live, ref metabolic)) continue;
                    if (GrewThisStep(live.Sim)) continue;

                    foreach (Organism o in live.World.Living)
                    {
                        if (before.TryGetValue(o.Id, out int was) && was != o.PlanRevision)
                        {
                            bitten = o.Id;
                            break;
                        }
                    }
                }

                Assert.True(bitten >= 0, "no body lost a part off the growth step; the fixture tests nothing");

                _out.WriteLine(
                    FormattableString.Invariant(
                        $"body {bitten} changed plan at t={live.World.ElapsedSeconds} s, metabolic step {metabolic}, ") +
                    FormattableString.Invariant($"with no growth step on it; checkpointing there"));

                byte[] state = Checkpoint(live);

                // The fidelity check's own file, written from the same moment.
                string root = SimulationTests.Scratch("bite-checkpoint");
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);

                string run = Path.Combine(root, "run");
                Directory.CreateDirectory(Path.Combine(run, "checkpoints"));
                File.WriteAllText(Path.Combine(run, "config.json"), RunConfigJson.Write(config));
                File.WriteAllText(Path.Combine(run, "run.json"), "{\"seed\": 1}\n");

                string file = WriteCheckpointFile(live, config, run, metabolic);

                using (Pair restored = Restore(state, config))
                {
                    Organism a = LivingById(live.World)[bitten];
                    Organism b = LivingById(restored.World)[bitten];
                    Assert.Equal(a.PlanRevision, b.PlanRevision);

                    Assert.True(restored.Sim.TryPose(bitten, out Creature solver));
                    Assert.Null(OffPlan(solver, b));

                    // The records the bite carried onto the new plan come through the restore, and
                    // the restored body senses them by reference as the live one does.
                    Assert.True(live.Sim.TryPose(bitten, out Creature liveSolver));
                    Assert.NotNull(a.PartContact);
                    Assert.NotNull(a.PartDamage);
                    Assert.Same(a.PartContact, liveSolver.Senses.Contact);
                    Assert.Same(a.PartDamage, liveSolver.Senses.Damage);
                    Assert.Equal(a.PartContact, b.PartContact);
                    Assert.Equal(a.PartDamage, b.PartDamage);
                    Assert.Same(b.PartContact, solver.Senses.Contact);
                    Assert.Same(b.PartDamage, solver.Senses.Damage);

                    Assert.Equal(live.Sim.Dynamics.Digest(), restored.Sim.Dynamics.Digest());

                    int twinMetabolic = 0;

                    for (int i = 0; i < 4 * live.Sim.StepsPerMetabolicStep; i++)
                    {
                        bool ma = live.Sim.Step();
                        bool mb = restored.Sim.Step();

                        Assert.Equal(ma, mb);
                        Assert.True(
                            live.Sim.Dynamics.Digest() == restored.Sim.Dynamics.Digest(),
                            "the solver's digest parted at physics step " + (i + 1) + " after the restore");

                        if (!ma) continue;

                        twinMetabolic++;
                        Assert.True(
                            WorldDigest(live.World) == WorldDigest(restored.World),
                            "the world's state parted at metabolic step " + twinMetabolic + " after the restore");
                    }
                }

                var console = new StringWriter();
                TextWriter saved = Console.Out;
                int result;

                try
                {
                    Console.SetOut(console);
                    result = CheckpointFidelity.Run(file, 1.0, Path.Combine(root, "check"), threads: 2);
                }
                finally
                {
                    Console.SetOut(saved);
                }

                _out.WriteLine(console.ToString());
                Assert.Equal(0, result);
            }
        }

        /// <summary>The run loop's checkpoint file, world, harness, sampler and loop.</summary>
        private static string WriteCheckpointFile(Pair pair, RunConfig config, string run, int metabolic)
        {
            pair.Sim.SettleBeforeCheckpoint();

            var header = new CheckpointHeader
            {
                Version = Evosim.Farm.Checkpoint.Version,
                Seconds = pair.World.ElapsedSeconds,
                Seed = 1UL,
                PhysicsSteps = pair.Sim.Steps,
                PhysicsStepSeconds = pair.Sim.PhysicsDt,
                StepsPerMetabolicStep = pair.Sim.StepsPerMetabolicStep,
                ConfigHash = config.Hash(),
                EngineVersion = "bite-rebuild test",
                SourceArm = "bite",
                SourceRun = "run",
                ReportEvery = 20,
            };

            string path = Evosim.Farm.Checkpoint.PathFor(run, pair.World.ElapsedSeconds);

            var sampler = new Sampler();

            CheckpointWriter.Write(
                path,
                header,
                w =>
                {
                    StateIo.Tag(w, "PAYL");
                    pair.World.WriteState(w);
                    pair.Sim.WriteState(w);
                    sampler.WriteState(w);

                    StateIo.Tag(w, "LOOP");
                    w.Write(metabolic);
                    w.Write(0d);
                    w.Write(0d);
                    w.Write(false);

                    StateIo.Tag(w, "PEND");
                });

            return path;
        }

        // ------------------------------------------------------------------ the worlds with no bite

        /// <summary>
        /// A world in which nothing bites steps exactly as it did before the rebuild moved. Two
        /// hashes are pinned: the solver's digest at every physics step alone, which is the
        /// trajectory, and that folded with the world's state at every metabolic step.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Both were pinned from <c>1f06a67</c></b>, the tree the rebuild fix was built on, and
        /// the fix left both worlds' hashes where they were.
        /// </para>
        /// <para>
        /// <b>The module world's folded hash then moved, and by design.</b> From the ruling that
        /// a plan change carries the step's contact and damage records through the part map
        /// rather than dropping them (round 49), a leaf that gains a module keeps its contact
        /// record, and the world's state holds that array where it held null. The folded hash
        /// went from <c>91576b1700773a1aa2990de3d6de3e78</c> to the one pinned here. Its solver
        /// hash did not move, because the leaves have no brain to read what they carry: the
        /// trajectory is the one <c>1f06a67</c> stepped. The touching world changes no plan, and
        /// both its hashes are <c>1f06a67</c>'s.
        /// </para>
        /// </remarks>
        /// <param name="world">
        /// <c>touching</c>, the crowded box with the contact and damage senses open and no claw, so
        /// that the contact list is handed and the records written at every step; or
        /// <c>modules</c>, the same box with indeterminate leaves put in and the module rule on, so
        /// that plans change and are rebuilt at the growth step.
        /// </param>
        /// <param name="pinned">The solver's digests folded with the world's states.</param>
        /// <param name="trajectory">The solver's digests alone.</param>
        [Theory]
        [InlineData("touching", "ebbc4299be5adeb889a1d478458deb99", "1de02f3f7ab7cd2fd2b5b4b83922d153")]
        [InlineData("modules", "ff558cf1332d129005b2a2e8988137a2", "c2072f14f54654181d54d218124c4dd3")]
        public void AWorldWithNoBiteStepsAsItDidBefore(string world, string pinned, string trajectory)
        {
            bool modules = world == "modules";
            RunConfig config = modules ? ModuleWorld() : CheckpointRestoreTests.TouchingWorld();

            using (Pair pair = Found(config))
            using (SHA256 sha = SHA256.Create())
            {
                int metabolic = 0;
                var fold = new MemoryStream();
                var into = new BinaryWriter(fold);
                var solverFold = new MemoryStream();
                var intoSolver = new BinaryWriter(solverFold);

                for (int step = 0; step < 4_000; step++)
                {
                    bool ran = Step(pair, ref metabolic, modules ? Leaves : (Action<Pair>)null);

                    into.Write(pair.Sim.Dynamics.Digest());
                    intoSolver.Write(pair.Sim.Dynamics.Digest());
                    if (!ran) continue;

                    into.Write(WorldDigest(pair.World));
                    into.Write(pair.Sim.ModuleRebuilds);
                    into.Write(pair.Sim.Resizes);
                    into.Write(pair.World.Births);
                    into.Write(pair.World.Deaths);
                }

                into.Flush();
                intoSolver.Flush();
                string digest = BitConverter.ToString(sha.ComputeHash(fold.ToArray()), 0, 16)
                    .Replace("-", string.Empty).ToLowerInvariant();
                string solverDigest = BitConverter.ToString(sha.ComputeHash(solverFold.ToArray()), 0, 16)
                    .Replace("-", string.Empty).ToLowerInvariant();

                _out.WriteLine(
                    FormattableString.Invariant(
                        $"{world}: {metabolic} metabolic steps to t={pair.World.ElapsedSeconds} s, ") +
                    FormattableString.Invariant(
                        $"{pair.World.Living.Count} living, {pair.World.PartsKilled} parts killed, ") +
                    FormattableString.Invariant(
                        $"{pair.World.ModuleAdds} module adds, {pair.Sim.ModuleRebuilds} rebuilds; digest {digest}, ") +
                    FormattableString.Invariant($"solver {solverDigest}"));

                Assert.Equal(0L, pair.World.PartsKilled);
                Assert.Equal(0L, pair.World.BodiesEaten);
                if (modules) Assert.True(pair.Sim.ModuleRebuilds > 0, "no plan changed; the fixture tests nothing");

                Assert.Equal(trajectory, solverDigest);
                Assert.Equal(pinned, digest);
            }
        }
    }
}

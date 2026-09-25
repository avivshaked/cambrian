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
    /// test added in <c>72b3087</c>), where the four bite tests fail.
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
        private static bool StepBiting(Pair pair, ref int metabolic)
        {
            bool censusNext = (pair.Sim.Steps + 1) % pair.Sim.StepsPerMetabolicStep == 0;
            if (censusNext && metabolic >= InoculateAt && metabolic < LastAttack) Attack(pair);

            if (!pair.Sim.Step()) return false;

            metabolic++;

            if (metabolic == InoculateAt)
            {
                pair.World.Inoculate(Fork(), count: 8, heightY: -2.5f);
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
        /// A world in which nothing bites steps exactly as it did before the rebuild moved: the
        /// solver's digest at every physics step and the world's state at every metabolic step,
        /// folded into one hash and pinned from <c>1f06a67</c>.
        /// </summary>
        /// <param name="world">
        /// <c>touching</c>, the crowded box with the contact and damage senses open and no claw, so
        /// that the contact list is handed and the records written at every step; or
        /// <c>modules</c>, the same box with indeterminate leaves put in and the module rule on, so
        /// that plans change and are rebuilt at the growth step.
        /// </param>
        [Theory]
        [InlineData("touching", "ebbc4299be5adeb889a1d478458deb99")]
        [InlineData("modules", "91576b1700773a1aa2990de3d6de3e78")]
        public void AWorldWithNoBiteStepsAsItDidBefore(string world, string pinned)
        {
            bool modules = world == "modules";
            RunConfig config = modules ? ModuleWorld() : CheckpointRestoreTests.TouchingWorld();

            using (Pair pair = Found(config))
            using (SHA256 sha = SHA256.Create())
            {
                int metabolic = 0;
                var fold = new MemoryStream();
                var into = new BinaryWriter(fold);

                for (int step = 0; step < 4_000; step++)
                {
                    bool ran = Step(pair, ref metabolic, modules ? Leaves : (Action<Pair>)null);

                    into.Write(pair.Sim.Dynamics.Digest());
                    if (!ran) continue;

                    into.Write(WorldDigest(pair.World));
                    into.Write(pair.Sim.ModuleRebuilds);
                    into.Write(pair.Sim.Resizes);
                    into.Write(pair.World.Births);
                    into.Write(pair.World.Deaths);
                }

                into.Flush();
                string digest = BitConverter.ToString(sha.ComputeHash(fold.ToArray()), 0, 16)
                    .Replace("-", string.Empty).ToLowerInvariant();

                _out.WriteLine(
                    FormattableString.Invariant(
                        $"{world}: {metabolic} metabolic steps to t={pair.World.ElapsedSeconds} s, ") +
                    FormattableString.Invariant(
                        $"{pair.World.Living.Count} living, {pair.World.PartsKilled} parts killed, ") +
                    FormattableString.Invariant(
                        $"{pair.World.ModuleAdds} module adds, {pair.Sim.ModuleRebuilds} rebuilds; digest {digest}"));

                Assert.Equal(0L, pair.World.PartsKilled);
                Assert.Equal(0L, pair.World.BodiesEaten);
                if (modules) Assert.True(pair.Sim.ModuleRebuilds > 0, "no plan changed; the fixture tests nothing");

                Assert.Equal(pinned, digest);
            }
        }
    }
}

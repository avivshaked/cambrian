using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D220 on the farm's CPU solver: a body that dies keeps its solver body and carries on from its pose and velocity
    /// as remains, a cut-off limb gets a body posed from the links it was cut from, remains that shed a part are
    /// rebuilt like any body, remains close and leave the solver, a mouth bites them through the contact list, the
    /// records and the checkpoint carry them, and a resume equals the run it parted from.
    /// </summary>
    /// <remarks>
    /// The tests that need an exact moment (a founding before its source is rebuilt, two cuts in one step) hand Core
    /// its contacts and run its mouth pass themselves, then call the harness's own two steps in the order the
    /// metabolic step calls them. The tests that need the real flow (the contact list, the records, a resume) press a
    /// claw against a victim and step.
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class RemainsSolverTests
    {
        private readonly ITestOutputHelper _out;

        public RemainsSolverTests(ITestOutputHelper output) => _out = output;

        // ------------------------------------------------------------------ the readings

        /// <summary>One bite by contact, and Core's mouth pass over it, as one metabolic step would run it.</summary>
        private static void Bite(RemainsFixtures.Pair pair, long biter, long target, int part)
        {
            pair.World.SetContacts(new List<CreatureContact> { new CreatureContact(biter, 0, target, part) });
            pair.World.ApplyMouth(100f);
        }

        /// <summary>
        /// <see cref="Bite"/> and then what <c>Metabolise</c> does straight after <c>World.Step</c>: the foundings posed
        /// from their sources, and the changed plans rebuilt.
        /// </summary>
        private static void BiteAndSettle(RemainsFixtures.Pair pair, long biter, long target, int part)
        {
            Bite(pair, biter, target, part);
            pair.Sim.PoseFoundings();
            pair.Sim.RebuildChangedPlans();
        }

        private static Creature BodyOf(RemainsFixtures.Pair pair, long id)
        {
            Assert.True(pair.Sim.TryPose(id, out Creature body), "no solver body for " + id);
            return body;
        }

        /// <summary>Asserts a new body's link <paramref name="k"/> stands where, and moves as, link <paramref name="from"/> of its source.</summary>
        private static void AssertPosedFrom(Creature source, int from, Creature body, int k, string what)
        {
            for (int d = 0; d < 3; d++)
            {
                Assert.True(
                    Math.Abs(source.Position[3 * from + d] - body.Position[3 * k + d]) < 1e-9,
                    what + ": link " + k + " position " + d);
                Assert.True(
                    Math.Abs(source.Velocity[3 * from + d] - body.Velocity[3 * k + d]) < 1e-9,
                    what + ": link " + k + " velocity " + d);
                Assert.True(
                    Math.Abs(source.Spin[3 * from + d] - body.Spin[3 * k + d]) < 1e-9,
                    what + ": link " + k + " spin " + d);
            }

            for (int d = 0; d < 4; d++)
            {
                Assert.True(
                    Math.Abs(source.Rotation[4 * from + d] - body.Rotation[4 * k + d]) < 1e-9,
                    what + ": link " + k + " rotation " + d);
            }
        }

        /// <summary>The checkpoint payload the run loop writes, world then harness, without the file.</summary>
        private static byte[] CheckpointOf(RemainsFixtures.Pair pair)
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

        private static RemainsFixtures.Pair Restore(RunConfig config, byte[] state)
        {
            RemainsFixtures.Pair pair = RemainsFixtures.Found(config);

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                pair.World.ReadState(r);
                pair.Sim.ReadState(r);

                Assert.Equal(state.Length, buffer.Position);
            }

            return pair;
        }

        // ------------------------------------------------------------------ a body that dies

        [Fact]
        public void ABodyThatDiesKeepsItsSolverBodyWithTheSamePoseAndVelocityFlaggedAsRemains()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(RemainsFixtures.Config(true), RemainsFixtures.Fork(), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                Organism claw = p.World.Living[1];
                Creature before = BodyOf(p, victim.Id);
                RemainsFixtures.Excite(before);

                Assert.False(before.Remains);

                double[] position = (double[])before.Position.Clone();
                double[] rotation = (double[])before.Rotation.Clone();
                double[] velocity = (double[])before.Velocity.Clone();
                double[] spin = (double[])before.Spin.Clone();
                double[] q = (double[])before.Q.Clone();
                double[] qd = (double[])before.Qd.Clone();

                // The body starves: it dies, and with the dial on it lies in the water as itself.
                RemainsFixtures.Starve(p);

                Assert.True(victim.IsRemains);
                Assert.Contains(victim, p.World.Remains);
                Assert.DoesNotContain(victim, p.World.Living);
                Assert.Equal(1L, p.World.Deaths);

                p.Sim.SettleBeforeCheckpoint();

                Creature after = BodyOf(p, victim.Id);
                Assert.Same(before, after);
                Assert.True(after.Remains);
                Assert.Contains(p.Sim.Dynamics.Creatures, c => ReferenceEquals(c, before));

                Assert.Equal(position, after.Position);
                Assert.Equal(rotation, after.Rotation);
                Assert.Equal(velocity, after.Velocity);
                Assert.Equal(spin, after.Spin);
                Assert.Equal(q, after.Q);
                Assert.Equal(qd, after.Qd);

                // The claw is living and unflagged, and the solver holds the two in ascending id order.
                Assert.False(BodyOf(p, claw.Id).Remains);
                Assert.Equal(
                    p.Sim.Dynamics.Creatures.Select(c => c.Id).OrderBy(i => i).ToArray(),
                    p.Sim.Dynamics.Creatures.Select(c => c.Id).ToArray());
            }
        }

        [Fact]
        public void RemainsAreSteppedAsPhysicalBodiesAndSinkWhileTheLivingStayWhereTheyAreHeld()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(RemainsFixtures.Config(true), RemainsFixtures.Fork(), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                Creature body = BodyOf(p, victim.Id);
                RemainsFixtures.Excite(body);

                RemainsFixtures.Starve(p);
                p.Sim.SettleBeforeCheckpoint();
                Assert.True(body.Remains);

                var signals = new float[body.DriveSignal.Length];
                double y0 = body.Position[1];
                for (int i = 0; i < 100; i++) p.Sim.Step();

                // Moved by the physics (it had a velocity and weighs more than the water), and its brain ran nothing.
                Assert.NotEqual(y0, body.Position[1]);
                Assert.Equal(signals, body.DriveSignal);
                Assert.All(body.Drive.Stroke, s => Assert.Equal(0.0, s));
                Assert.Equal(0.0, body.DrainMechanicalWork());
            }
        }

        // ------------------------------------------------------------------ a founding

        [Fact]
        public void AFoundingBuildsABodyPosedFromTheLinksOfTheBodyItWasCutFromBeforeThatBodyIsRebuilt()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.Arena(RemainsFixtures.Config(true), RemainsFixtures.Fork(), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                Organism claw = p.World.Living[1];
                Creature source = BodyOf(p, victim.Id);
                RemainsFixtures.Excite(source);
                Assert.Equal(7, source.Links);

                // Part 1 goes, and the limb beyond it, parts 2 and 3, is cut off.
                Bite(p, claw.Id, victim.Id, 1);

                Assert.Single(p.World.Remains);
                Organism limb = p.World.Remains[0];
                Assert.Equal(victim.Id, limb.ParentId);
                Assert.Equal(2, limb.Phenotype.PartCount);
                Assert.False(p.Sim.TryPose(limb.Id, out _), "the limb has a solver body before it was founded");

                // The harness's two steps after World.Step, in the order Metabolise runs them.
                p.Sim.PoseFoundings();

                Creature body = BodyOf(p, limb.Id);
                Assert.True(body.Remains);
                Assert.Equal(2, body.Links);
                Assert.Equal(7, source.Links);                      // the source is not rebuilt yet
                AssertPosedFrom(source, 2, body, 0, "limb");
                AssertPosedFrom(source, 3, body, 1, "limb");
                Assert.True(Vec3.Read(body.Velocity, 0).Magnitude > 1e-3, "the fixture never moved the limb");

                // The joint between the limb's two links moves as it did in the source.
                Assert.Equal(source.Q[source.DofStart[3]], body.Q[body.DofStart[1]]);
                Assert.Equal(source.Qd[source.DofStart[3]], body.Qd[body.DofStart[1]]);

                p.Sim.RebuildChangedPlans();

                // And the bitten body is on its new plan, carrying its surviving links on.
                Creature rebuilt = BodyOf(p, victim.Id);
                Assert.NotSame(source, rebuilt);
                Assert.Equal(4, rebuilt.Links);
                Assert.False(rebuilt.Remains);
                Assert.Equal(victim.PlanRevision, rebuilt.AppliedPlanRevision);
                AssertPosedFrom(source, 0, rebuilt, 0, "the root");
                AssertPosedFrom(source, 4, rebuilt, 1, "the second spine");

                p.Sim.SettleBeforeCheckpoint();
                Assert.Equal(3, p.Sim.Dynamics.Creatures.Count);
                Assert.Equal(
                    p.Sim.Dynamics.Creatures.Select(c => c.Id).OrderBy(i => i).ToArray(),
                    p.Sim.Dynamics.Creatures.Select(c => c.Id).ToArray());
            }
        }

        [Fact]
        public void ASecondCutInTheSameStepIsPosedThroughTheFirstCutsMapAndStillStandsOnTheSolversLinks()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.Arena(RemainsFixtures.Config(true), RemainsFixtures.Fork(), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                Organism claw = p.World.Living[1];
                Creature source = BodyOf(p, victim.Id);
                RemainsFixtures.Excite(source);

                // Two cuts before the harness looks. The first takes parts 1 to 3 (the body is then root and parts
                // 4 to 6, indexed 0 to 3); the second takes index 2, the second spine's middle link, and the last
                // link beyond it is cut off: index 3 of the plan after the first cut, link 6 of the solver's.
                Bite(p, claw.Id, victim.Id, 1);
                Bite(p, claw.Id, victim.Id, 2);

                Assert.Equal(2, p.World.Remains.Count);
                Organism first = p.World.Remains[0];
                Organism second = p.World.Remains[1];
                Assert.Equal(2, first.Phenotype.PartCount);
                Assert.Equal(1, second.Phenotype.PartCount);

                p.Sim.PoseFoundings();

                AssertPosedFrom(source, 2, BodyOf(p, first.Id), 0, "first cut");
                AssertPosedFrom(source, 3, BodyOf(p, first.Id), 1, "first cut");
                AssertPosedFrom(source, 6, BodyOf(p, second.Id), 0, "second cut");

                p.Sim.RebuildChangedPlans();

                // The body is root and the first link of the second spine, and its joint is the source's link 4.
                Creature rebuilt = BodyOf(p, victim.Id);
                Assert.Equal(2, rebuilt.Links);
                AssertPosedFrom(source, 0, rebuilt, 0, "the root");
                AssertPosedFrom(source, 4, rebuilt, 1, "the first link left");
            }
        }

        [Fact]
        public void RemainsFoundedAndThenShedInOneStepAreBuiltOnThePlanTheyHoldNowFromTheFirstSourcesLinks()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.Arena(RemainsFixtures.Config(true), RemainsFixtures.Fork(4), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                Organism claw = p.World.Living[1];
                Creature source = BodyOf(p, victim.Id);
                RemainsFixtures.Excite(source);
                Assert.Equal(9, source.Links);

                // The first cut takes parts 1 to 4 and cuts off parts 2 to 4 as remains A. The mouth then takes A's
                // second part (the source's part 3) in the same step: A keeps its root (the source's part 2), and the
                // part beyond (the source's part 4) is cut off A as remains B.
                Bite(p, claw.Id, victim.Id, 1);
                Assert.Single(p.World.Remains);
                Organism a = p.World.Remains[0];
                Assert.Equal(3, a.Phenotype.PartCount);

                Bite(p, claw.Id, a.Id, 1);
                Assert.Equal(2, p.World.Remains.Count);
                Organism b = p.World.Remains.First(r => r.Id != a.Id);
                Assert.Equal(1, a.Phenotype.PartCount);
                Assert.Equal(1, b.Phenotype.PartCount);

                p.Sim.PoseFoundings();

                // A is built on the one part it holds now, posed from the source's link 2; B from the source's link 4.
                Creature bodyA = BodyOf(p, a.Id);
                Assert.Equal(1, bodyA.Links);
                Assert.Equal(a.PlanRevision, bodyA.AppliedPlanRevision);
                AssertPosedFrom(source, 2, bodyA, 0, "remains A");
                AssertPosedFrom(source, 4, BodyOf(p, b.Id), 0, "remains B");

                p.Sim.RebuildChangedPlans();
                Assert.Same(bodyA, BodyOf(p, a.Id));        // already on its plan: nothing to rebuild
            }
        }

        // ------------------------------------------------------------------ a remains body that sheds a part

        [Fact]
        public void ARemainsBodyThatShedsAPartIsRebuiltOnItsNewPlanKeepingTheStateOfItsSurvivingLinksAndStaysRemains()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(RemainsFixtures.Config(true), RemainsFixtures.Fork(4), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                Organism claw = p.World.Living[1];
                Creature before = BodyOf(p, victim.Id);
                RemainsFixtures.Excite(before);

                RemainsFixtures.Starve(p);                      // dies: remains, parts 0 to 8
                p.Sim.SettleBeforeCheckpoint();
                Assert.True(before.Remains);
                Assert.Equal(9, before.Links);
                int revision = victim.PlanRevision;

                // The first spine's first link is eaten whole: the spine beyond it is cut off, the second spine stays.
                Bite(p, claw.Id, victim.Id, 1);

                Assert.Equal(5, victim.Phenotype.PartCount);
                Assert.True(victim.PlanRevision > revision);
                Assert.Equal(2, p.World.Remains.Count);
                Organism limb = p.World.Remains.First(r => r.Id != victim.Id);
                Assert.Equal(3, limb.Phenotype.PartCount);

                p.Sim.PoseFoundings();
                p.Sim.RebuildChangedPlans();

                Creature after = BodyOf(p, victim.Id);
                Assert.NotSame(before, after);
                Assert.Equal(5, after.Links);
                Assert.True(after.Remains);
                Assert.Equal(victim.PlanRevision, after.AppliedPlanRevision);

                // Parts 0 and 5 to 8 survive, as links 0 to 4: pose, velocity and joint state carried over.
                int[] kept = { 0, 5, 6, 7, 8 };
                for (int k = 0; k < kept.Length; k++)
                {
                    AssertPosedFrom(before, kept[k], after, k, "surviving link");
                    if (k == 0) continue;

                    Assert.Equal(before.Q[before.DofStart[kept[k]]], after.Q[after.DofStart[k]]);
                    Assert.Equal(before.Qd[before.DofStart[kept[k]]], after.Qd[after.DofStart[k]]);
                }

                // And the limb that was cut off is a flagged body posed from the links it was.
                Creature limbBody = BodyOf(p, limb.Id);
                Assert.True(limbBody.Remains);
                AssertPosedFrom(before, 2, limbBody, 0, "cut-off limb");
                AssertPosedFrom(before, 4, limbBody, 2, "cut-off limb");

                Assert.Equal(0.0, after.Drive.Stroke.Sum());
            }
        }

        // ------------------------------------------------------------------ closing

        [Fact]
        public void RemainsThatCloseLeaveTheSolverAndTheirJoulesAreBurntWithTheBooksClosed()
        {
            const float window = 6f;

            using (RemainsFixtures.Pair p = RemainsFixtures.Arena(RemainsFixtures.Config(true, window), RemainsFixtures.Fork(), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                Organism claw = p.World.Living[1];

                Bite(p, claw.Id, victim.Id, 1);
                Organism limb = p.World.Remains[0];
                p.Sim.PoseFoundings();
                p.Sim.RebuildChangedPlans();
                p.Sim.SettleBeforeCheckpoint();

                Assert.True(p.Sim.TryPose(limb.Id, out _));
                double standing = p.World.RemainsStandingJoules;
                Assert.True(standing > 0d);
                p.World.DrainLineageEvents();

                // Until its window has passed the remains stand, and the solver holds them.
                for (int i = 0; i < 20 * 25 && p.World.Remains.Count > 0; i++) p.Sim.Step();

                _out.WriteLine($"remains standing {p.World.Remains.Count} at t={p.World.ElapsedSeconds:0.#} s, closed {p.World.RemainsClosed}");
                Assert.Empty(p.World.Remains);
                Assert.True(p.World.RemainsClosed >= 1L);
                Assert.True(p.World.RemainsClosedJoules > 0d);

                // Left the solver: the next reconcile took the body out of the table and the list.
                p.Sim.SettleBeforeCheckpoint();
                Assert.False(p.Sim.TryPose(limb.Id, out _));
                Assert.DoesNotContain(p.Sim.Dynamics.Creatures, c => c.Id == limb.Id);

                // The close is in the lineage queue as a row of its own.
                LineageEvent closed = p.World.DrainLineageEvents().First(e => e.Kind == LineageEventKind.RemainsClosed && e.Id == limb.Id);
                Assert.True(closed.RemainsJoules > 0d);
                Assert.Contains("\"e\":\"rc\"", closed.ToJson());

                Assert.True(
                    Math.Abs(p.World.AuditResidual) <= 1e-6 * Math.Max(1d, p.World.StandingJoules),
                    $"audit residual {p.World.AuditResidual}");
                Assert.True(
                    Math.Abs(p.World.MatterResidual) <= 1e-6 * Math.Max(1d, p.World.MatterInitialTotal),
                    $"matter residual {p.World.MatterResidual}");
            }
        }

        [Fact]
        public void RemainsWhoseSolverBodyDivergedAreClosedAtOnceAndLeaveNoRemains()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(RemainsFixtures.Config(true), RemainsFixtures.Fork(), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];

                RemainsFixtures.Starve(p);
                p.Sim.SettleBeforeCheckpoint();
                Creature body = BodyOf(p, victim.Id);
                Assert.True(body.Remains);

                // The solver loses the body: a non-finite pose, as an exploded one has.
                body.BasePosition = new Vec3(double.NaN, 0, 0);
                Kinematics.Poses(body);

                long deaths = p.World.Deaths;
                long diverged = p.World.Diverged;

                while (!p.Sim.Step()) { }       // the divergence check reads every body, remains included

                Assert.Empty(p.World.Remains);
                Assert.Equal(deaths, p.World.Deaths);       // the body died when it became remains; this is no second death
                Assert.Equal(diverged, p.World.Diverged);
                Assert.True(p.World.RemainsClosed >= 1L);

                p.Sim.SettleBeforeCheckpoint();
                Assert.False(p.Sim.TryPose(victim.Id, out _));
            }
        }

        // ------------------------------------------------------------------ contact with remains

        [Fact]
        public void ContactsWithRemainsAreHandedToCoreAndABiteOnThemMovesJoulesWithTheBooksClosed()
        {
            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(
                RemainsFixtures.Config(true), RemainsFixtures.Fork(), RemainsFixtures.Claw(), RemainsFixtures.Hunter()))
            {
                Organism victim = p.World.Living[0];
                Organism hunter = p.World.Living[2];

                RemainsFixtures.Starve(p);                      // the victim dies where it stands
                p.Sim.SettleBeforeCheckpoint();
                Assert.True(victim.IsRemains);
                Assert.True(hunter.HasIntake);

                double standing = p.World.RemainsStandingJoules;
                double kept = p.World.BiteKeptFromRemainsJoules;
                bool handed = false;

                // The hunter is held against the remains' first link before each census, as the claws are held in
                // the bite tests, until the list has named the pair and the mouth has eaten.
                for (int step = 0; step < 25 * 40 && !(handed && p.World.BiteKeptFromRemainsJoules > kept); step++)
                {
                    if ((p.Sim.Steps + 1) % p.Sim.StepsPerMetabolicStep == 0)
                    {
                        RemainsFixtures.Press(p.Sim, hunter.Id, victim.Id, 0);
                    }

                    if (!p.Sim.Step()) continue;

                    foreach (CreatureContact c in RemainsFixtures.Handed(p.Sim))
                    {
                        if ((c.BodyA == victim.Id && c.BodyB == hunter.Id) || (c.BodyB == victim.Id && c.BodyA == hunter.Id)) handed = true;
                    }
                }

                _out.WriteLine(
                    $"the list named the hunter and the remains: {handed}; kept from remains {p.World.BiteKeptFromRemainsJoules:0.###} J, " +
                    $"remains hold {p.World.RemainsStandingJoules:0.###} J of {standing:0.###}");

                Assert.True(handed, "the contact list never named the hunter and the remains");
                Assert.True(p.World.BiteKeptFromRemainsJoules > kept, "no joules moved from the remains to the biter");

                Assert.True(
                    Math.Abs(p.World.AuditResidual) <= 1e-6 * Math.Max(1d, p.World.StandingJoules),
                    $"audit residual {p.World.AuditResidual}");
                Assert.True(
                    Math.Abs(p.World.MatterResidual) <= 1e-6 * Math.Max(1d, p.World.MatterInitialTotal),
                    $"matter residual {p.World.MatterResidual}");
            }
        }

        // ------------------------------------------------------------------ the records

        [Fact]
        public void TheLineageGainsRowsForFoundingAndClosingAndThePositionsAndTheStateStreamFlagTheRemains()
        {
            string scratch = SimulationTests.Scratch("remains-records");
            if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
            Directory.CreateDirectory(scratch);

            RunConfig config = RemainsFixtures.Config(true, windowSeconds: 8f);
            var world = new World(config, 1UL);

            string runPath;
            long victimId, limbId;
            string poseFile = Path.Combine(scratch, "remains-poses.bin");

            using (RunDirectory dir = RunDirectory.Create(scratch, config, DateTime.UtcNow, RunRecordFormat.Compact))
            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path))
            using (PoseRecorder poses = PoseRecorder.AtPath(poseFile, 0.5f, config.Hash()))
            {
                runPath = dir.Path;
                world.QueueAdmittedGenomes = dir.Genomes != null;

                // The victim goes in with a reserve it cannot live on, and starves into whole-body remains.
                float reserve = config.FounderEnergyJoules;
                config.FounderEnergyJoules = 0.001f;
                world.Inoculate(RemainsFixtures.Fork(), count: 1, heightY: -2.5f);
                config.FounderEnergyJoules = reserve;
                world.Inoculate(RemainsFixtures.Claw(), count: 1, heightY: -2.5f);
                sim.SettleBeforeCheckpoint();

                var pair = new RemainsFixtures.Pair { World = world, Sim = sim };
                Organism victim = world.Living[0];
                Organism claw = world.Living[1];
                victimId = victim.Id;

                var sampler = new Sampler();
                IReadOnlyList<string> columns = Sampler.Columns(sim.Config);

                // The victim starves: it lies in the water as itself, and no founding row is written for it.
                RemainsFixtures.Starve(pair);
                Assert.Single(world.Remains);
                sim.SettleBeforeCheckpoint();

                // Then part 1 of the dead body is eaten: the limb beyond is founded as remains of its own.
                BiteAndSettle(pair, claw.Id, victim.Id, 1);
                Assert.Equal(2, world.Remains.Count);
                limbId = world.Remains.First(r => r.Id != victim.Id).Id;

                while (!sim.Step()) { }
                sampler.Write(sim, dir, columns);
                poses.WriteFrame(sim, world.ElapsedSeconds);

                while (!sim.Step()) { }
                sampler.Write(sim, dir, columns);
                poses.WriteFrame(sim, world.ElapsedSeconds);

                // Then the window passes and the remains close.
                for (int i = 0; i < 2 * 30 && world.Remains.Count > 0; i++)
                {
                    while (!sim.Step()) { }
                    if (i % 4 == 3) sampler.Write(sim, dir, columns);
                }

                Assert.Empty(world.Remains);
                sampler.Write(sim, dir, columns);
                poses.WriteFrame(sim, world.ElapsedSeconds);
                sampler.Close();
            }

            // The lineage: one row for the founding, naming the body it was cut from, and a row for each close.
            var founded = new List<JsonNode>();
            var closed = new List<JsonNode>();

            foreach (string row in JsonlWriter.ReadRows(Path.Combine(runPath, "lineage.jsonl")))
            {
                JsonNode node = Json.Parse(row);
                string e = node["e"].AsString();
                if (e == "r") founded.Add(node);
                else if (e == "rc") closed.Add(node);
            }

            _out.WriteLine($"{founded.Count} founding rows, {closed.Count} closing rows");
            Assert.Single(founded);
            Assert.Equal(limbId, (long)founded[0]["id"].AsDouble());
            Assert.Equal(victimId, (long)founded[0]["from"].AsDouble());
            Assert.True(founded[0]["t"].AsDouble() >= 0d);

            Assert.Equal(2, closed.Count);
            Assert.Contains(closed, c => (long)c["id"].AsDouble() == limbId);
            Assert.Contains(closed, c => (long)c["id"].AsDouble() == victimId);
            Assert.All(closed, c => Assert.True(c["j"].AsDouble() > 0d));
            Assert.True(
                closed.All(c => c["t"].AsDouble() > founded[0]["t"].AsDouble()),
                "a close is written after the founding");

            // The positions record: remains carry the new bit beside their own guild flags, the living never do.
            bool sawLimb = false, sawVictim = false, sawLiving = false;

            foreach (string line in RecordFiles.PositionLines(runPath, RunRecordFormat.Compact))
            {
                JsonNode row = Json.Parse(line);
                JsonNode bodies = row["b"];

                for (int i = 0; i < bodies.Count; i++)
                {
                    long id = (long)bodies[i][0].AsDouble();
                    int flags = (int)bodies[i][4].AsDouble();
                    bool remains = (flags & PositionsRow.RemainsBit) != 0;

                    if (id == limbId) { Assert.True(remains); sawLimb = true; }
                    else if (id == victimId && remains) sawVictim = true;
                    else if (!remains) sawLiving = true;
                }
            }

            Assert.True(sawLimb, "the limb never appears in the positions record");
            Assert.True(sawVictim, "the victim never appears in the positions record as remains");
            Assert.True(sawLiving, "no living body appears without the bit");

            // The pose stream records remains bodies like any other, with the bit in their flags.
            var flagsSeen = new Dictionary<long, int>();
            int largest = 0;

            using (PoseStreamReader reader = PoseStreamReader.Open(poseFile))
            {
                for (int f = 0; f < reader.Frames.Length; f++)
                {
                    PoseBody[] bodies = reader.Read(f).Bodies;
                    if (bodies.Length > largest) largest = bodies.Length;

                    foreach (PoseBody b in bodies)
                    {
                        Assert.InRange(b.Flags, 0, PoseStream.AllFlagBits);
                        if ((b.Flags & PoseStream.RemainsBit) != 0) flagsSeen[b.Id] = b.Flags;
                    }
                }
            }

            Assert.Contains((int)limbId, flagsSeen.Keys.Select(k => (int)k));
            Assert.Contains((int)victimId, flagsSeen.Keys.Select(k => (int)k));
            Assert.Equal(3, largest);          // the claw, the victim as remains and the limb in one frame
        }

        [Fact]
        public void ThePositionBitsAndTheStreamsBitsAreOneDefinition()
        {
            Assert.Equal(PositionsRow.RemainsBit, PoseStream.RemainsBit);
            Assert.Equal(PositionsRow.AllBits, PoseStream.AllFlagBits);
            Assert.Equal(8, PositionsRow.RemainsBit);

            using (RemainsFixtures.Pair p = RemainsFixtures.StarvedArena(RemainsFixtures.Config(true), RemainsFixtures.Fork(), RemainsFixtures.Claw()))
            {
                Organism victim = p.World.Living[0];
                int living = PoseRecorder.GuildFlags(victim);
                Assert.Equal(0, living & PositionsRow.RemainsBit);

                RemainsFixtures.Starve(p);
                int dead = PoseRecorder.GuildFlags(victim);
                Assert.Equal(living | PositionsRow.RemainsBit, dead);
            }
        }

        // ------------------------------------------------------------------ the checkpoint

        [Fact]
        public void ADialOnCheckpointWithRemainsInItResumesEqualToTheUnbrokenRunOverFiveHundredPhysicsSteps()
        {
            RunConfig config = RemainsFixtures.Config(true);

            using (RemainsFixtures.Pair run = RemainsFixtures.StarvedArena(
                config, RemainsFixtures.Fork(4), RemainsFixtures.Fork(), RemainsFixtures.Claw()))
            {
                Organism first = run.World.Living[0];
                Organism second = run.World.Living[1];
                Organism claw = run.World.Living[2];

                RemainsFixtures.Excite(BodyOf(run, first.Id));
                RemainsFixtures.Excite(BodyOf(run, second.Id));

                // One fork starves into whole-body remains and is then cut twice; the other, living, is cut once:
                // whole-body remains that have shed a part, limbs founded from living and from dead bodies, all in the
                // water, moving.
                RemainsFixtures.Starve(run);
                run.Sim.SettleBeforeCheckpoint();
                Assert.True(first.IsRemains);

                Bite(run, claw.Id, second.Id, 2);
                Bite(run, claw.Id, first.Id, 1);
                run.Sim.PoseFoundings();
                run.Sim.RebuildChangedPlans();
                Bite(run, claw.Id, first.Id, 1);
                run.Sim.PoseFoundings();
                run.Sim.RebuildChangedPlans();

                while (!run.Sim.Step()) { }
                for (int i = 0; i < 50; i++) run.Sim.Step();
                while (!run.Sim.Step()) { }

                int standing = run.World.Remains.Count;
                _out.WriteLine($"{standing} remains standing at t={run.World.ElapsedSeconds:0.#} s, {run.Sim.Dynamics.Creatures.Count} bodies");
                Assert.True(standing >= 3, "the fixture left too few remains to test a checkpoint on");
                Assert.Contains(run.World.Remains, r => r.Id == first.Id);
                Assert.Contains(run.World.Remains, r => r.ParentId >= 0 && r.Id != first.Id);

                byte[] state = CheckpointOf(run);

                using (RemainsFixtures.Pair resumed = Restore(config, state))
                {
                    Assert.Equal(standing, resumed.World.Remains.Count);
                    Assert.Equal(run.Sim.Dynamics.Creatures.Count, resumed.Sim.Dynamics.Creatures.Count);
                    Assert.All(resumed.World.Remains, r => Assert.True(BodyOf(resumed, r.Id).Remains));
                    Assert.All(resumed.World.Living, l => Assert.False(BodyOf(resumed, l.Id).Remains));
                    Assert.Equal(run.Sim.Dynamics.Digest(), resumed.Sim.Dynamics.Digest());
                    Assert.Equal(state, CheckpointOf(resumed));

                    for (int step = 1; step <= 500; step++)
                    {
                        run.Sim.Step();
                        resumed.Sim.Step();

                        if (step % 25 == 0)
                        {
                            Assert.True(
                                run.Sim.Dynamics.Digest() == resumed.Sim.Dynamics.Digest(),
                                $"the resume parted from the run at physics step {step}");
                        }
                    }

                    Assert.Equal(run.World.Remains.Count, resumed.World.Remains.Count);
                    Assert.Equal(CheckpointOf(run), CheckpointOf(resumed));
                    _out.WriteLine($"after 500 steps: {resumed.World.Remains.Count} remains, {resumed.World.RemainsClosed} closed");
                }
            }
        }

        /// <summary>
        /// <c>--verify-checkpoint</c> on a dial-on world with remains standing: the file is restored, compared member
        /// by member, and the restored pair and a pair rebuilt from the file are stepped side by side.
        /// </summary>
        [Fact]
        public void TheFidelityCheckPassesOnADialOnWorldWithRemainsStandingInIt()
        {
            RunConfig config = RemainsFixtures.Config(true);

            string root = SimulationTests.Scratch("remains-fidelity");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);

            string run = Path.Combine(root, "run");
            Directory.CreateDirectory(Path.Combine(run, "checkpoints"));
            File.WriteAllText(Path.Combine(run, "config.json"), RunConfigJson.Write(config));
            File.WriteAllText(Path.Combine(run, "run.json"), "{\"seed\": 1}\n");

            string file;

            using (RemainsFixtures.Pair live = RemainsFixtures.StarvedArena(
                config, RemainsFixtures.Fork(4), RemainsFixtures.Fork(), RemainsFixtures.Claw()))
            {
                Organism first = live.World.Living[0];
                Organism second = live.World.Living[1];
                Organism claw = live.World.Living[2];

                RemainsFixtures.Excite(BodyOf(live, first.Id));
                RemainsFixtures.Excite(BodyOf(live, second.Id));
                RemainsFixtures.Starve(live);
                live.Sim.SettleBeforeCheckpoint();

                Bite(live, claw.Id, second.Id, 2);
                Bite(live, claw.Id, first.Id, 1);
                live.Sim.PoseFoundings();
                live.Sim.RebuildChangedPlans();

                while (!live.Sim.Step()) { }
                for (int i = 0; i < 50; i++) live.Sim.Step();
                while (!live.Sim.Step()) { }

                int standing = live.World.Remains.Count;
                _out.WriteLine($"{standing} remains standing, {live.Sim.Dynamics.Creatures.Count} bodies; checkpointing");
                Assert.True(standing >= 3, "the fixture left too few remains to test a checkpoint on");

                file = WriteCheckpointFile(live, config, run);
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

        /// <summary>The run loop's checkpoint file for a pair: world, harness, sampler and loop.</summary>
        private static string WriteCheckpointFile(RemainsFixtures.Pair pair, RunConfig config, string run)
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
                EngineVersion = "remains test",
                SourceArm = "remains",
                SourceRun = "run",
                ReportEvery = 20,
            };

            string path = Evosim.Farm.Checkpoint.PathFor(run, pair.World.ElapsedSeconds);
            var sampler = new Sampler();
            int metabolic = (int)(pair.Sim.Steps / pair.Sim.StepsPerMetabolicStep);

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
    }
}

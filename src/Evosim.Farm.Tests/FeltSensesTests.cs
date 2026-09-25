using System;
using System.Collections.Generic;
using System.Reflection;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D123 in the farm: what a brain reads on the contact and damage channels, on the physics
    /// steps after a metabolic step, is that step's record and nothing older.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The record is Core's and the reading is the solver's</b>, and the farm's part is the
    /// hand-back between them (<c>Metabolise.HandBackWhatWasFelt</c>): a reference per body per
    /// metabolic step. So after every metabolic step this asks three things of every living body.
    /// Its two senses are its own organism's two arrays, by reference. Every part flagged as
    /// touched is named in the contact list the farm handed Core for that step, and every part
    /// the list names is flagged. And what <c>CreatureSenses.Read</c> returns on the two channels
    /// is the record.
    /// </para>
    /// <para>
    /// <b>The touching world, with claws.</b> <c>CheckpointRestoreTests</c>' crowded box, with
    /// eight armed boxes put in after the founding so that some parts are bitten, and health set
    /// deep enough that a bite takes several steps to kill a part. Until round 49 a flag stayed
    /// set until a plan change, so the second assertion failed on the first body that touched
    /// something and then moved off it.
    /// </para>
    /// </remarks>
    public class FeltSensesTests
    {
        private readonly ITestOutputHelper _out;

        public FeltSensesTests(ITestOutputHelper output) => _out = output;

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

        private static Organism OrganismOf(World world, long id)
        {
            IReadOnlyList<Organism> living = world.Living;
            for (int i = 0; i < living.Count; i++) if (living[i].Id == id) return living[i];
            return null;
        }

        [Fact]
        public void TheSensesABrainReadsAreTheLastStepsRecords()
        {
            RunConfig config = CheckpointRestoreTests.TouchingWorld();
            config.HealthPerCubicMetre = 1000f;

            var world = new World(config, 1UL);

            using (var sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: null))
            {
                // The list the farm hands Core at each metabolic step, read where it is kept: it
                // is refilled at the top of the next hand-over and nothing empties it in between.
                var handed = (List<CreatureContact>)typeof(Simulation)
                    .GetField("_contacts", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(sim);

                int metabolic = 0;
                int checkedBodies = 0;
                int flagsSeen = 0;
                int flagsCleared = 0;
                int woundsSeen = 0;
                int woundsCleared = 0;

                // What each body read after the step before, to see a reading go away.
                var touchedBefore = new HashSet<(long, int)>();
                var woundedBefore = new HashSet<(long, int)>();
                var revisions = new Dictionary<long, int>();

                for (int step = 0; step < 6_000 && metabolic < 160; step++)
                {
                    bool metabolicNext = (sim.Steps + 1) % sim.StepsPerMetabolicStep == 0;

                    if (metabolicNext)
                    {
                        revisions.Clear();
                        foreach (Organism o in world.Living) revisions[o.Id] = o.PlanRevision;
                    }

                    if (!sim.Step()) continue;

                    metabolic++;

                    // After the founding, the claws go in, and the harness builds their bodies at
                    // the top of the next physics step.
                    if (metabolic == 30) world.Inoculate(Claw(), count: 8, heightY: -2.5f);

                    var named = new HashSet<(long, int)>();
                    var namedAlive = new HashSet<(long, int)>();

                    foreach (CreatureContact c in handed)
                    {
                        named.Add((c.BodyA, c.PartA));
                        named.Add((c.BodyB, c.PartB));

                        Organism a = OrganismOf(world, c.BodyA);
                        Organism b = OrganismOf(world, c.BodyB);
                        if (a == null || b == null) continue;

                        // A body whose plan changed on this step had its records dropped with the
                        // old plan's indices, and Core asked the list against the plan it had
                        // before the change; neither side of such a pair is asked here.
                        if (!revisions.TryGetValue(a.Id, out int ra) || ra != a.PlanRevision) continue;
                        if (!revisions.TryGetValue(b.Id, out int rb) || rb != b.PlanRevision) continue;

                        if (c.PartA >= a.Phenotype.PartCount || c.PartB >= b.Phenotype.PartCount) continue;

                        namedAlive.Add((c.BodyA, c.PartA));
                        namedAlive.Add((c.BodyB, c.PartB));
                    }

                    var touchedNow = new HashSet<(long, int)>();
                    var woundedNow = new HashSet<(long, int)>();

                    foreach (Creature body in sim.Dynamics.Creatures)
                    {
                        Organism o = OrganismOf(world, body.Id);
                        if (o == null) continue;   // died this step; the reconcile has not run

                        checkedBodies++;

                        Assert.Same(o.PartContact, body.Senses.Contact);
                        Assert.Same(o.PartDamage, body.Senses.Damage);

                        bool[] contact = o.PartContact;
                        float[] damage = o.PartDamage;

                        if (contact != null)
                        {
                            for (int p = 0; p < contact.Length; p++)
                            {
                                if (!contact[p]) continue;

                                touchedNow.Add((o.Id, p));
                                Assert.True(
                                    named.Contains((o.Id, p)),
                                    $"t={world.ElapsedSeconds}: body {o.Id} part {p} reads a touch " +
                                    "that this step's contact list does not name");
                            }
                        }

                        if (damage != null)
                        {
                            for (int p = 0; p < damage.Length; p++)
                            {
                                if (!(damage[p] > 0f)) continue;

                                woundedNow.Add((o.Id, p));
                                Assert.True(
                                    named.Contains((o.Id, p)),
                                    $"t={world.ElapsedSeconds}: body {o.Id} part {p} reads a loss " +
                                    "on a part this step's contact list does not name");
                            }
                        }

                        // What a brain gets when it asks, on every link the solver has.
                        for (int p = 0; p < body.Links; p++)
                        {
                            float touch = contact != null && p < contact.Length && contact[p] ? 1f : 0f;
                            float hurt = damage != null && p < damage.Length
                                ? Math.Min(1f, Math.Max(-1f, damage[p]))
                                : 0f;

                            Assert.Equal(touch, body.Senses.Read(p, SensorChannel.Contact, 0));
                            Assert.Equal(hurt, body.Senses.Read(p, SensorChannel.Damage, 0));
                        }
                    }

                    foreach ((long, int) part in namedAlive)
                    {
                        Assert.True(
                            touchedNow.Contains(part),
                            $"t={world.ElapsedSeconds}: body {part.Item1} part {part.Item2} is named " +
                            "in this step's contact list and reads no touch");
                    }

                    flagsSeen += touchedNow.Count;
                    woundsSeen += woundedNow.Count;

                    foreach ((long, int) part in touchedBefore)
                    {
                        if (!touchedNow.Contains(part) && OrganismOf(world, part.Item1) != null) flagsCleared++;
                    }

                    foreach ((long, int) part in woundedBefore)
                    {
                        if (!woundedNow.Contains(part) && OrganismOf(world, part.Item1) != null) woundsCleared++;
                    }

                    touchedBefore = touchedNow;
                    woundedBefore = woundedNow;
                }

                _out.WriteLine(
                    $"{metabolic} metabolic steps to t={world.ElapsedSeconds} s, {world.Living.Count} " +
                    $"living; {checkedBodies} body-steps checked; {flagsSeen} touches read and " +
                    $"{flagsCleared} gone a step later; {woundsSeen} losses read and {woundsCleared} " +
                    $"gone a step later; {world.PartsKilled} parts killed");

                Assert.True(flagsSeen > 0, "no part ever read a touch; the fixture tests nothing");
                Assert.True(flagsCleared > 0, "no touch ever went away; the fixture tests nothing");
                Assert.True(woundsSeen > 0, "no part ever read a loss; the claws bit nothing");
                Assert.True(woundsCleared > 0, "no loss ever went away; the fixture tests nothing");
            }
        }
    }
}

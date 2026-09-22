using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// The four things work package G's loop has to get right that a run's numbers would not show
    /// until much too late: the order bodies are added to the solver in, the narrowing rule, the
    /// body fraction at build, and the stop file.
    /// </summary>
    /// <remarks>
    /// <b>A small world and a short run, because these are not about the ecology.</b> Each of
    /// these is an invariant of the harness rather than a reading of a world, so the fixture is
    /// the cheapest world Core will build and every test is seconds.
    /// </remarks>
    public class SimulationTests
    {
        /// <summary>
        /// A world small enough to step in a test and shaped enough to exercise the placer.
        /// </summary>
        /// <remarks>
        /// A box rather than a tank, at one patch, with the floor open so the population floor
        /// keeps founding — which is what gives the reconcile something to do on every step.
        /// </remarks>
        private static RunConfig SmallWorld() =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "100" },
                { "EVOSIM_DEPTH", "20" },
                { "EVOSIM_FOUNDER_DEPTH", "20" },
                { "EVOSIM_PATCHES", "1" },
                { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FIELD", "grid" },
                { "EVOSIM_FIELD_CELL", "2" },
                { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_IRRADIANCE", "200" },
                { "EVOSIM_MATTER_BUDGET", "300" },
                { "EVOSIM_TISSUE_ENERGY", "500" },
                { "EVOSIM_RHO", "100" },
                { "EVOSIM_OVERHEAD", "100" },
                { "EVOSIM_DT", "0.02" },
                { "EVOSIM_SENSE_CHEMICAL", "1" },
                { "EVOSIM_SENSE_ENERGY", "1" },
            })));

        private static string Scratch(string name)
        {
            string path = Path.Combine(
                Path.GetDirectoryName(typeof(SimulationTests).Assembly.Location), "sim-tests", name);

            Directory.CreateDirectory(path);
            return path;
        }

        private static Simulation Build(out RunDirectory dir, string scratch)
        {
            RunConfig config = SmallWorld();
            var world = new World(config, 1UL);

            dir = RunDirectory.Create(scratch, config, DateTime.UtcNow);

            return new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: dir.Path);
        }

        /// <summary>
        /// The solver's creature list is in ascending id order at every step, whatever order
        /// births and deaths arrived in.
        /// </summary>
        /// <remarks>
        /// <b>This is the whole of why a run of this engine is reproducible.</b> The list is
        /// stepped in list order and every cross-body sum is taken in list order, so the
        /// trajectory depends on the order the list is in; ascending id is the one order that is a
        /// function of the population rather than of its history. <c>AddInIdOrder</c> keeps it and
        /// the contact instrument checks it — this asserts that the loop calls the former, by
        /// stepping a world that founds, breeds and kills for long enough that the list has lost
        /// bodies from the middle.
        /// </remarks>
        [Fact]
        public void TheSolverListStaysInAscendingIdOrder()
        {
            string scratch = Scratch("id-order");
            using (Simulation sim = Build(out RunDirectory dir, scratch))
            using (dir)
            {
                bool sawARemoval = false;
                int mostBodies = 0;

                for (int i = 0; i < 400; i++)
                {
                    sim.Step();

                    IReadOnlyList<Creature> bodies = sim.Dynamics.Creatures;

                    for (int b = 1; b < bodies.Count; b++)
                    {
                        Assert.True(
                            bodies[b - 1].Id < bodies[b].Id,
                            "the solver's list is out of id order at " + (b - 1) + ": " +
                            bodies[b - 1].Id + " then " + bodies[b].Id);
                    }

                    if (bodies.Count > mostBodies) mostBodies = bodies.Count;
                    if (bodies.Count < mostBodies) sawARemoval = true;
                }

                // The assertion above is vacuous on a list that only ever grew, so the test says
                // out loud that the list actually lost bodies from the middle.
                Assert.True(sawARemoval, "no body was ever removed; the ordering was not tested");
                Assert.True(mostBodies > 1, "nothing was ever built");
            }
        }

        /// <summary>
        /// A newborn's body carries the fraction Core built it at, from the moment it is built.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Left at the default of 1, every newborn would differ from its ledger on the very first
        /// growth step and be resized for the whole of its life — the resize count would be the
        /// population and the cost would be silent, which is the exact mismatch
        /// fable-propose-growth.md rule 8 exists to close.
        /// </para>
        /// <para>
        /// <b>Asked the first time a body exists and not at every step</b>, because the two are
        /// meant to part between growth steps: the ledger grows a creature every metabolic step
        /// and the harness resizes at <c>GrowthStepSeconds</c>, so a living body's applied
        /// fraction lags its organism's by up to one cadence by design. What must never lag is a
        /// newborn's, and that is what this asks.
        /// </para>
        /// </remarks>
        [Fact]
        public void ANewbornsBodyFractionIsSetAtBuild()
        {
            string scratch = Scratch("body-fraction");
            using (Simulation sim = Build(out RunDirectory dir, scratch))
            using (dir)
            {
                var seen = new HashSet<long>();
                int checkedBodies = 0;
                int juveniles = 0;

                for (int i = 0; i < 4000; i++)
                {
                    sim.Step();

                    IReadOnlyList<Organism> living = sim.World.Living;

                    for (int c = 0; c < living.Count; c++)
                    {
                        Creature body = sim.Dynamics.ById(living[c].Id);
                        if (body == null || !seen.Add(living[c].Id)) continue;

                        // Equal to the bit: the applied fraction is written from the organism's
                        // own, and a resize only ever happens on a difference.
                        Assert.Equal(living[c].BodyFraction, body.AppliedBodyFraction);

                        checkedBodies++;
                        if (living[c].BodyFraction < 1f) juveniles++;
                    }
                }

                Assert.True(checkedBodies > 0, "no body was ever built");

                // And the assertion is not vacuous: a world of adults would pass it with every
                // fraction at 1.
                Assert.True(juveniles > 0, "every body was born adult; growth was not exercised");
            }
        }

        /// <summary>
        /// The narrowing rule: what Core is handed is the float of the solver's double, exactly,
        /// and nothing is rounded on the way.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Asserted against the organism's own record of where it was fed.</b>
        /// <c>World.Observe</c> writes the centre it is handed into <c>Organism.X</c>,
        /// <c>HeightY</c> and <c>Z</c>, so after a metabolic step those three are the narrowed
        /// centre of mass of the body the solver holds. If anything on the way rounded, clamped
        /// or re-widened, the two would differ by more than the cast does.
        /// </para>
        /// <para>
        /// The centre is recomputed here from the same public arrays the loop reads, so the test
        /// checks the rule rather than a copy of the number.
        /// </para>
        /// </remarks>
        [Fact]
        public void WhatCoreIsHandedIsThePlainFloatOfTheSolversDouble()
        {
            string scratch = Scratch("narrowing");
            using (Simulation sim = Build(out RunDirectory dir, scratch))
            using (dir)
            {
                int checkedBodies = 0;

                for (int i = 0; i < 200; i++)
                {
                    if (!sim.Step()) continue;

                    IReadOnlyList<Organism> living = sim.World.Living;

                    for (int c = 0; c < living.Count; c++)
                    {
                        Organism creature = living[c];
                        Creature body = sim.Dynamics.ById(creature.Id);
                        if (body == null) continue;

                        // A body admitted during this step has not been observed yet: its place
                        // is the one it was admitted at rather than one this loop handed over.
                        if (creature.Age <= 0f) continue;

                        Vec3 centre = body.CentreOfMass();

                        Assert.Equal((float)centre.X, creature.X);
                        Assert.Equal((float)centre.Y, creature.HeightY);
                        Assert.Equal((float)centre.Z, creature.Z);

                        checkedBodies++;
                    }

                    if (checkedBodies > 200) break;
                }

                Assert.True(checkedBodies > 0, "nothing was ever observed");
            }
        }

        /// <summary>
        /// A <c>STOP</c> file ends the run with <c>status: "stopped"</c> and its first line as the
        /// reason.
        /// </summary>
        /// <remarks>
        /// <b>Write first and kill second, as <c>stop-arm.ps1</c> does</b> — except that there is
        /// nothing to kill. A killed process cannot rewrite its own manifest, so a bare kill leaves
        /// one reading <c>running</c> forever and reads exactly like a crash or a live arm; a
        /// console program can simply be asked to stop, and this is the asking.
        /// </remarks>
        [Fact]
        public void AStopFileEndsTheRunWithItsFirstLineAsTheReason()
        {
            string root = Scratch("stop");
            string runsRoot = Path.Combine(root, "runs");

            if (Directory.Exists(runsRoot)) Directory.Delete(runsRoot, recursive: true);

            string[] settings =
            {
                "EVOSIM_RUNS_ROOT=" + runsRoot,
                "EVOSIM_OUT=stopped.md",
                "EVOSIM_SECONDS=100000",
                "EVOSIM_WALL_MINUTES=2",
                "EVOSIM_REPORT_EVERY=1",
                "EVOSIM_AREA=100", "EVOSIM_DEPTH=20", "EVOSIM_FOUNDER_DEPTH=20",
                "EVOSIM_PATCHES=1", "EVOSIM_SHARED_SPACE=1",
                "EVOSIM_FIELD=grid", "EVOSIM_FIELD_CELL=2", "EVOSIM_FIELD_MATTER_CELL=5",
                "EVOSIM_IRRADIANCE=200", "EVOSIM_MATTER_BUDGET=300",
                "EVOSIM_TISSUE_ENERGY=500", "EVOSIM_RHO=100", "EVOSIM_OVERHEAD=100",
                "EVOSIM_DT=0.02", "EVOSIM_THREADS=2",
            };

            // The run directory's name carries the instant it was made, so the STOP file cannot
            // be written before the run starts. It is dropped in by a watcher thread the moment
            // the directory appears, which is what a stop-arm would do from outside the process.
            var stopper = new System.Threading.Thread(() =>
            {
                var deadline = DateTime.UtcNow.AddMinutes(1);

                while (DateTime.UtcNow < deadline)
                {
                    if (Directory.Exists(runsRoot))
                    {
                        string[] dirs = Directory.GetDirectories(
                            Path.Combine(runsRoot, "stopped"), "*", SearchOption.TopDirectoryOnly);

                        if (dirs.Length > 0)
                        {
                            File.WriteAllText(
                                Path.Combine(dirs[0], "STOP"), "manual-futility\nand a second line\n");

                            return;
                        }
                    }

                    System.Threading.Thread.Sleep(20);
                }
            });

            stopper.IsBackground = true;

            Directory.CreateDirectory(Path.Combine(runsRoot, "stopped"));
            stopper.Start();

            int exit = Program.Main(settings);
            stopper.Join(TimeSpan.FromSeconds(5));

            Assert.Equal(0, exit);

            string[] runDirs = Directory.GetDirectories(Path.Combine(runsRoot, "stopped"));
            Assert.Single(runDirs);

            JsonNode manifest = Json.Parse(File.ReadAllText(Path.Combine(runDirs[0], "run.json")));

            Assert.Equal("stopped", manifest["status"].AsString());
            Assert.Equal("manual-futility", manifest["reason"].AsString());
            Assert.Contains("manual-futility", manifest["ending"].AsString());

            // And the run that was stopped is a run that ran: the manifest carries the facts
            // rather than the zeros a killed process would have left standing.
            Assert.True(manifest["physicsSteps"].AsDouble() > 0d);
        }
    }
}

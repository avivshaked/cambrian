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
    /// The divergence check's second bracket, narrowed to the touched bodies (card-next-r57.md
    /// Part 8): the same run as the whole check, and a whole check wherever the solver has stepped
    /// since the last one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every comparison is against the whole check</b>, which is what every run before the
    /// change did: the same digest after every physics step and the same lineage rows in the same
    /// order. The two rare paths are also run with the narrowing that ignores the physics, which
    /// Part 8 shows would part the run, so each test shows it would catch that.
    /// </para>
    /// <para>
    /// <b>A body is thrown by hand between two steps</b>, as the stray test throws one: placed
    /// where the physics might have thrown it, without the harness being told. A throw just past
    /// the glass stays inside the contact grid's reach, so the glass guard and not the grid is
    /// what kills it.
    /// </para>
    /// </remarks>
    public sealed class SecondCheckTests
    {
        private readonly ITestOutputHelper _out;

        public SecondCheckTests(ITestOutputHelper output) => _out = output;

        // What each test saw, kept where a reader of the run can find it: the test log hides the
        // output helper at its verbosity, and a count is what says a test compared something.
        private void Note(string line)
        {
            _out.WriteLine(line);
            File.AppendAllText(Path.Combine(SimulationTests.Scratch("second-check"), "counts.txt"),
                DateTime.Now.ToString("HH:mm:ss") + " " + line + Environment.NewLine);
        }

        private const int StepsPerMetabolic = 25;

        // SimulationTests.SmallWorld in a tank, so a body can be thrown through the glass.
        private static RunConfig SmallTank() =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
            {
                { "EVOSIM_SHAPE", "tank" },
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

        private sealed class Run : IDisposable
        {
            public World World;
            public Simulation Sim;
            public readonly List<ulong> Digests = new List<ulong>();
            public readonly StringBuilder Lineage = new StringBuilder();

            public Run(RunConfig config, Simulation.SecondCheck mode)
            {
                World = new World(config, 1UL);
                Sim = new Simulation(World, 1UL, 0.02f, StepsPerMetabolic, threads: 2, runDirectory: null)
                {
                    SecondCheckMode = mode,
                };
            }

            public bool Step()
            {
                bool metabolic = Sim.Step();
                Digests.Add(Sim.Dynamics.Digest());
                foreach (LineageEvent e in World.DrainLineageEvents()) Lineage.AppendLine(e.ToJson());
                return metabolic;
            }

            public bool Alive(long id) => World.Living.Any(o => o.Id == id);

            public void Dispose() => Sim.Dispose();
        }

        private static void Throw(Simulation sim, long id, double x, double z)
        {
            Creature body = sim.Dynamics.Creatures.First(c => c.Id == id);
            body.PlaceAt(new Vec3(x, body.Position[1], z), QuatD.Identity);
        }

        // Just past the glass and its one-metre margin, along x from the tank's axis.
        private static void ThroughTheGlass(Simulation sim, long id)
        {
            SolverConfig s = sim.Solver;
            Throw(sim, id, s.TankAxisX + s.TankRadiusMetres + 1.5, s.TankAxisZ);
        }

        private static void SameRun(Run whole, Run narrowed, string what)
        {
            Assert.Equal(whole.Digests.Count, narrowed.Digests.Count);
            for (int i = 0; i < whole.Digests.Count; i++)
            {
                Assert.True(whole.Digests[i] == narrowed.Digests[i],
                    what + ": the digests part at physics step " + (i + 1));
            }

            Assert.Equal(whole.Lineage.ToString(), narrowed.Lineage.ToString());
        }

        /// <summary>Steps until the next step to run is <paramref name="phase"/> steps past a metabolic step.</summary>
        private static void StepTo(Run run, int atLeast, int phase)
        {
            while (run.Sim.Steps < atLeast || run.Sim.Steps % StepsPerMetabolic != phase) run.Step();
        }

        // ------------------------------------------------------------------ the stray sphere's kill

        private sealed class StrayOutcome
        {
            public long Stray, Thrown;
            public bool ThrownAliveAfterNextStep;
            public long Whole;
        }

        /// <summary>
        /// A stray sphere's kill on a step that is not a metabolic one, then a body thrown through
        /// the glass after that step's physics; the next step's second bracket must read it.
        /// </summary>
        private static StrayOutcome AfterAStray(Run run, int after)
        {
            StepTo(run, 300, 3);
            Assert.Empty(run.Sim.TouchedIds());
            Assert.True(run.Sim.Dynamics.Creatures.Count >= 3, "too few bodies");

            var outcome = new StrayOutcome
            {
                Stray = run.Sim.Dynamics.Creatures[1].Id,
                Thrown = run.Sim.Dynamics.Creatures[2].Id,
            };

            Creature stray = run.Sim.Dynamics.Creatures[1];
            stray.PlaceAt(new Vec3(1e12, stray.Position[1], stray.Position[2]), QuatD.Identity);

            Assert.False(run.Step());
            Assert.False(run.Alive(outcome.Stray));

            ThroughTheGlass(run.Sim, outcome.Thrown);
            long wholeBefore = run.Sim.WholeSecondChecks;

            Assert.False(run.Step());
            outcome.ThrownAliveAfterNextStep = run.Alive(outcome.Thrown);
            outcome.Whole = run.Sim.WholeSecondChecks - wholeBefore;

            for (int i = 0; i < after; i++) run.Step();
            return outcome;
        }

        [Fact]
        public void AfterAStraySpheresKillTheNextCheckReadsEveryBody()
        {
            RunConfig config = SmallTank();

            using (var whole = new Run(config, Simulation.SecondCheck.Whole))
            using (var narrowed = new Run(config, Simulation.SecondCheck.Narrowed))
            using (var careless = new Run(config, Simulation.SecondCheck.NarrowedIgnoringThePhysics))
            {
                StrayOutcome w = AfterAStray(whole, 300);
                StrayOutcome n = AfterAStray(narrowed, 300);
                StrayOutcome c = AfterAStray(careless, 0);

                Note($"stray {n.Stray}, thrown {n.Thrown}: alive after the next step " +
                               $"whole {w.ThrownAliveAfterNextStep}, narrowed {n.ThrownAliveAfterNextStep}, " +
                               $"careless {c.ThrownAliveAfterNextStep}; narrowed checks {narrowed.Sim.NarrowedSecondChecks}");

                Assert.Equal(w.Thrown, n.Thrown);
                Assert.False(w.ThrownAliveAfterNextStep);
                Assert.False(n.ThrownAliveAfterNextStep);
                Assert.Equal(1, n.Whole);
                Assert.True(narrowed.Sim.NarrowedSecondChecks > 0, "the narrowed bracket never ran");
                SameRun(whole, narrowed, "after a stray sphere's kill");

                // The careless narrowing leaves the thrown body in the physics: the test can tell.
                Assert.True(c.ThrownAliveAfterNextStep, "the careless narrowing killed the thrown body too, so this proves nothing");
            }
        }

        // ------------------------------------------------------------------ the bracket's own kills

        private sealed class OwnKillOutcome
        {
            public long Touched, Thrown;
            public bool TouchedAliveAfterFirst, ThrownAliveAfterSecond;
            public long Whole;
        }

        /// <summary>
        /// A touched body thrown through the glass after a metabolic step, which the next step's
        /// narrowed bracket kills; then an untouched body thrown after that step's physics, which
        /// the step after must read.
        /// </summary>
        private static OwnKillOutcome AfterOwnKills(Run run, int after)
        {
            List<long> touched = null;

            // A metabolic step that leaves some bodies touched and some not: early on every body
            // grows at every growth step, and a step that touched them all has no bystander.
            for (int guard = 0; guard < 40 * StepsPerMetabolic * 20; guard++)
            {
                if (run.Step() && run.Sim.Steps > 300)
                {
                    touched = run.Sim.TouchedIds();
                    List<long> seen = touched;
                    if (touched.Count > 0 && run.Sim.Dynamics.Creatures.Any(c => !seen.Contains(c.Id) && c.Links > 0)) break;
                }

                touched = null;
            }

            Assert.True(touched != null, "no metabolic step left some bodies touched and some not");

            var outcome = new OwnKillOutcome
            {
                Touched = touched[0],
                Thrown = run.Sim.Dynamics.Creatures.First(c => !touched.Contains(c.Id) && c.Links > 0).Id,
            };

            ThroughTheGlass(run.Sim, outcome.Touched);
            Assert.False(run.Step());
            outcome.TouchedAliveAfterFirst = run.Alive(outcome.Touched);

            ThroughTheGlass(run.Sim, outcome.Thrown);
            long wholeBefore = run.Sim.WholeSecondChecks;

            Assert.False(run.Step());
            outcome.ThrownAliveAfterSecond = run.Alive(outcome.Thrown);
            outcome.Whole = run.Sim.WholeSecondChecks - wholeBefore;

            for (int i = 0; i < after; i++) run.Step();
            return outcome;
        }

        [Fact]
        public void AfterTheSecondChecksOwnKillsTheNextCheckReadsEveryBody()
        {
            RunConfig config = SmallTank();

            using (var whole = new Run(config, Simulation.SecondCheck.Whole))
            using (var narrowed = new Run(config, Simulation.SecondCheck.Narrowed))
            using (var careless = new Run(config, Simulation.SecondCheck.NarrowedIgnoringThePhysics))
            {
                OwnKillOutcome w = AfterOwnKills(whole, 300);
                OwnKillOutcome n = AfterOwnKills(narrowed, 300);
                OwnKillOutcome c = AfterOwnKills(careless, 0);

                Note($"touched {n.Touched}, thrown {n.Thrown}; at step {narrowed.Sim.Steps - 300}: " +
                               $"thrown alive whole {w.ThrownAliveAfterSecond}, narrowed {n.ThrownAliveAfterSecond}, " +
                               $"careless {c.ThrownAliveAfterSecond}");

                Assert.Equal(w.Touched, n.Touched);
                Assert.Equal(w.Thrown, n.Thrown);
                Assert.False(w.TouchedAliveAfterFirst);
                Assert.False(n.TouchedAliveAfterFirst);
                Assert.False(c.TouchedAliveAfterFirst);
                Assert.False(w.ThrownAliveAfterSecond);
                Assert.False(n.ThrownAliveAfterSecond);
                Assert.Equal(1, n.Whole);
                SameRun(whole, narrowed, "after the second bracket's own kills");

                Assert.True(c.ThrownAliveAfterSecond, "the careless narrowing killed the thrown body too, so this proves nothing");
            }
        }

        // ------------------------------------------------------------------ the restore

        private static byte[] Checkpoint(Run run)
        {
            run.Sim.SettleBeforeCheckpoint();

            using (var buffer = new MemoryStream())
            {
                using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    run.World.WriteState(w);
                    run.Sim.WriteState(w);
                    w.Flush();
                }

                return buffer.ToArray();
            }
        }

        [Fact]
        public void ARestoreAsksForAWholeCheckAndGoesOnAsTheUninterruptedRun()
        {
            RunConfig config = CheckpointRestoreTests.TouchingWorld();

            using (var live = new Run(config, Simulation.SecondCheck.Narrowed))
            {
                // A metabolic step that leaves bodies touched, so the next step's bracket runs.
                bool found = false;
                for (int guard = 0; guard < 40 * StepsPerMetabolic * 20 && !found; guard++)
                {
                    found = live.Step() && live.Sim.Steps > 300 && live.Sim.TouchedIds().Count > 0;
                }

                Assert.True(found, "no metabolic step left a body touched");

                byte[] state = Checkpoint(live);

                using (var restored = new Run(config, Simulation.SecondCheck.Narrowed))
                {
                    using (var buffer = new MemoryStream(state, writable: false))
                    using (var r = new BinaryReader(buffer, Encoding.UTF8))
                    {
                        restored.World.ReadState(r);
                        restored.Sim.ReadState(r);
                    }

                    live.Digests.Clear();
                    live.Lineage.Clear();
                    long liveNarrowed = live.Sim.NarrowedSecondChecks, liveWhole = live.Sim.WholeSecondChecks;

                    live.Step();
                    restored.Step();

                    Note($"at physics step {live.Sim.Steps}: live narrowed +{live.Sim.NarrowedSecondChecks - liveNarrowed}, " +
                                   $"whole +{live.Sim.WholeSecondChecks - liveWhole}; restored whole {restored.Sim.WholeSecondChecks}, " +
                                   $"narrowed {restored.Sim.NarrowedSecondChecks}");

                    Assert.Equal(1, live.Sim.NarrowedSecondChecks - liveNarrowed);
                    Assert.Equal(1, restored.Sim.WholeSecondChecks);
                    Assert.Equal(0, restored.Sim.NarrowedSecondChecks);

                    for (int i = 0; i < 20 * StepsPerMetabolic; i++)
                    {
                        live.Step();
                        restored.Step();
                    }

                    Assert.True(restored.Sim.NarrowedSecondChecks > 0, "the restored run never narrowed");
                    SameRun(live, restored, "restored against uninterrupted");
                }
            }
        }

        // ------------------------------------------------------------------ the untouched bodies

        /// <summary>
        /// The economy between the two brackets changes nothing an untouched body's verdict reads:
        /// at every narrowed bracket each untouched body is judged too, and every verdict is the
        /// healthy one with the same last root to the bit.
        /// </summary>
        [Theory]
        [InlineData("small")]
        [InlineData("touching")]
        [InlineData("tank")]
        public void TheEconomyChangesNothingAnUntouchedBodysVerdictReads(string which)
        {
            RunConfig config = which == "small" ? SimulationTests.SmallWorld()
                : which == "touching" ? CheckpointRestoreTests.TouchingWorld()
                : SmallTank();

            using (var run = new Run(config, Simulation.SecondCheck.Narrowed))
            {
                run.Sim.AuditTheNarrowedCheck = true;
                for (int i = 0; i < 2500; i++) run.Step();

                Note($"{which}: narrowed {run.Sim.NarrowedSecondChecks}, whole {run.Sim.WholeSecondChecks}, " +
                               $"untouched bodies judged {run.Sim.AuditedBodies}, faults {run.Sim.AuditFaults}, resizes {run.Sim.Resizes}");

                Assert.True(run.Sim.NarrowedSecondChecks > 0, "the narrowed bracket never ran");
                Assert.True(run.Sim.AuditedBodies > 0, "no untouched body was judged");
                Assert.Equal(0, run.Sim.AuditFaults);
            }
        }

        // ------------------------------------------------------------------ the ordinary run

        [Theory]
        [InlineData("small")]
        [InlineData("touching")]
        [InlineData("tank")]
        public void TheNarrowedRunIsTheWholeRun(string which)
        {
            RunConfig config = which == "small" ? SimulationTests.SmallWorld()
                : which == "touching" ? CheckpointRestoreTests.TouchingWorld()
                : SmallTank();

            using (var whole = new Run(config, Simulation.SecondCheck.Whole))
            using (var narrowed = new Run(config, Simulation.SecondCheck.Narrowed))
            {
                for (int i = 0; i < 2500; i++)
                {
                    whole.Step();
                    narrowed.Step();
                }

                Note($"{which}: {narrowed.World.Living.Count} living, births {narrowed.World.Births}, " +
                               $"narrowed {narrowed.Sim.NarrowedSecondChecks}, whole {narrowed.Sim.WholeSecondChecks}");

                Assert.Equal(0, whole.Sim.NarrowedSecondChecks);
                Assert.True(narrowed.Sim.NarrowedSecondChecks > 0, "the narrowed bracket never ran");
                SameRun(whole, narrowed, which);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A world and its harness written to bytes and read back into a fresh pair are the same
    /// world, down to what each body is sensing, and go on stepping the same bodies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What Core's round trip cannot see.</b> <c>WorldStateTests</c> restores the economy and
    /// has no solver; <c>CreatureStateTests</c> restores a body and has no economy. What sits
    /// between them is the harness's wiring of a body's senses to its organism's arrays, and
    /// until <c>Checkpoint.Version</c> 5 a restore wired two of the four: every restored body
    /// read the contact and damage channels as 0 until its first metabolic step, and a hinge
    /// driven by a neuron reading contact swung stop to stop at the first sample of round 48's
    /// resume (2026-09-25).
    /// </para>
    /// <para>
    /// <b>A small box crowded enough that bodies touch</b>: twenty-five square metres by five
    /// deep and the floor's forty founders, the contact and damage senses open and the chemical
    /// and energy channels closed, so that a founder's sensor neuron that draws an optional
    /// channel draws one of the two this is about. Seeded, and the solver replays at any thread
    /// count, so every count the test searches for is the same on every run.
    /// </para>
    /// </remarks>
    public class CheckpointRestoreTests
    {
        private readonly ITestOutputHelper _out;

        public CheckpointRestoreTests(ITestOutputHelper output) => _out = output;

        internal static RunConfig TouchingWorld() =>
            EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "25" },
                { "EVOSIM_DEPTH", "5" },
                { "EVOSIM_FOUNDER_DEPTH", "5" },
                { "EVOSIM_PATCHES", "1" },
                { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FIELD", "grid" },
                { "EVOSIM_FIELD_CELL", "1" },
                { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_IRRADIANCE", "200" },
                { "EVOSIM_MATTER_BUDGET", "300" },
                { "EVOSIM_TISSUE_ENERGY", "500" },
                { "EVOSIM_RHO", "100" },
                { "EVOSIM_OVERHEAD", "100" },
                { "EVOSIM_DT", "0.02" },
                { "EVOSIM_SENSE_CONTACT", "1" },
                { "EVOSIM_SENSE_DAMAGE", "1" },
            })));

        private sealed class Pair : IDisposable
        {
            public World World;
            public Simulation Sim;

            public void Dispose() => Sim?.Dispose();
        }

        /// <summary>
        /// A world and its harness, with no run directory: nothing here writes a row, and the
        /// harness's only use for one is the divergence dumps, which it skips without.
        /// </summary>
        private static Pair Found()
        {
            var world = new World(TouchingWorld(), 1UL);

            return new Pair
            {
                World = world,
                Sim = new Simulation(world, 1UL, 0.02f, 25, threads: 2, runDirectory: null),
            };
        }

        /// <summary>The run loop's checkpoint payload, world then harness, without the file.</summary>
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

        private static Pair Restore(byte[] state)
        {
            Pair pair = Found();

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                pair.World.ReadState(r);
                pair.Sim.ReadState(r);

                Assert.Equal(state.Length, buffer.Position);
            }

            return pair;
        }

        private static Organism OrganismOf(World world, long id)
        {
            IReadOnlyList<Organism> living = world.Living;
            for (int i = 0; i < living.Count; i++) if (living[i].Id == id) return living[i];
            return null;
        }

        private static int BodiesTouched(Simulation sim)
        {
            int touched = 0;
            IReadOnlyList<Creature> bodies = sim.Dynamics.Creatures;
            for (int i = 0; i < bodies.Count; i++) if (bodies[i].Senses.Contact != null) touched++;
            return touched;
        }

        /// <summary>
        /// The physics step at which a restore of <paramref name="state"/> with the contact and
        /// damage senses unwired, as every restore was until <c>Checkpoint.Version</c> 5, parts
        /// from one wired as the checkpoint says; 0 when the two agree for a whole metabolic step.
        /// </summary>
        private static int UnwiredPartsAt(byte[] state, int steps)
        {
            using (Pair wired = Restore(state))
            using (Pair unwired = Restore(state))
            {
                foreach (Creature body in unwired.Sim.Dynamics.Creatures)
                {
                    body.Senses.Contact = null;
                    body.Senses.Damage = null;
                }

                for (int i = 0; i < steps; i++)
                {
                    wired.Sim.Step();
                    unwired.Sim.Step();

                    if (wired.Sim.Dynamics.Digest() != unwired.Sim.Dynamics.Digest()) return i + 1;
                }

                return 0;
            }
        }

        /// <summary>
        /// A restored body senses what the stepped one sensed — the same arrays, by reference to
        /// its own organism's — and the two worlds step the same bodies for fifty physics steps.
        /// </summary>
        /// <remarks>
        /// <b>The moment is searched for rather than assumed</b>: the world is stepped a metabolic
        /// step at a time until a copy restored with both senses unwired moves differently from
        /// one restored as the checkpoint says. At that moment some brain is steering on a touch,
        /// so the fifty-step comparison below it is the one the old restore failed; at a moment
        /// with no such brain it would pass on the old build too and prove nothing.
        /// </remarks>
        [Fact]
        public void ARestoredBodySensesWhatTheSteppedOneSensed()
        {
            using (Pair live = Found())
            {
                int steps = 0;
                int metabolic = 0;
                int tried = 0;
                int partedAt = 0;
                byte[] state = null;

                // The cap is a guard on the fixture, not a tolerance.
                while (steps < 50_000 && tried < 60)
                {
                    steps++;
                    if (!live.Sim.Step()) continue;

                    metabolic++;
                    if (metabolic < 40 || BodiesTouched(live.Sim) == 0) continue;

                    tried++;
                    state = Checkpoint(live);
                    partedAt = UnwiredPartsAt(state, live.Sim.StepsPerMetabolicStep);
                    if (partedAt > 0) break;
                }

                int touched = BodiesTouched(live.Sim);

                _out.WriteLine(
                    "stepped " + steps + " physics steps to t=" + live.World.ElapsedSeconds +
                    " s: " + live.World.Living.Count + " living, " + touched +
                    " bodies holding a contact array; " + tried + " moments tried, and a restore " +
                    "with the senses unwired parted at physics step " + partedAt);

                Assert.True(touched > 0, "no body was ever handed a contact array; the fixture tests nothing");
                Assert.True(
                    partedAt > 0,
                    "no moment was found at which dropping the contact and damage senses changes " +
                    "what a body does, so no brain here steers on them and the test proves nothing");

                using (Pair restored = Restore(state))
                {
                    IReadOnlyList<Creature> bodies = live.Sim.Dynamics.Creatures;

                    Assert.Equal(bodies.Count, restored.Sim.Dynamics.Creatures.Count);
                    Assert.Equal(live.Sim.ModuleRebuilds, restored.Sim.ModuleRebuilds);

                    for (int i = 0; i < bodies.Count; i++)
                    {
                        Creature a = bodies[i];
                        Creature b = restored.Sim.Dynamics.ById(a.Id);
                        Assert.NotNull(b);

                        Organism oa = OrganismOf(live.World, a.Id);
                        Organism ob = OrganismOf(restored.World, a.Id);
                        Assert.NotNull(oa);
                        Assert.NotNull(ob);

                        // Nullness first: a body the live run had not yet handed an array to (a
                        // newborn the checkpoint's own reconcile built, a body rebuilt at this
                        // growth step) must not be handed one by the restore either.
                        Assert.True(
                            (a.Senses.Contact == null) == (b.Senses.Contact == null),
                            "body " + a.Id + ": contact sense " +
                            (a.Senses.Contact == null ? "unwired" : "wired") + " live and " +
                            (b.Senses.Contact == null ? "unwired" : "wired") + " restored");

                        Assert.True(
                            (a.Senses.Damage == null) == (b.Senses.Damage == null),
                            "body " + a.Id + ": damage sense " +
                            (a.Senses.Damage == null ? "unwired" : "wired") + " live and " +
                            (b.Senses.Damage == null ? "unwired" : "wired") + " restored");

                        if (a.Senses.Contact != null)
                        {
                            Assert.Same(oa.PartContact, a.Senses.Contact);
                            Assert.Same(ob.PartContact, b.Senses.Contact);
                            Assert.Equal(a.Senses.Contact, b.Senses.Contact);
                        }

                        if (a.Senses.Damage != null)
                        {
                            Assert.Same(oa.PartDamage, a.Senses.Damage);
                            Assert.Same(ob.PartDamage, b.Senses.Damage);
                            Assert.Equal(a.Senses.Damage, b.Senses.Damage);
                        }

                        Assert.Same(ob, b.Senses.Reserve);
                    }

                    // And the two go on as one world. After every physics step rather than at the
                    // end: a difference that closes again would be missed at the end.
                    Assert.Equal(live.Sim.Dynamics.Digest(), restored.Sim.Dynamics.Digest());

                    for (int i = 0; i < 50; i++)
                    {
                        live.Sim.Step();
                        restored.Sim.Step();

                        Assert.True(
                            live.Sim.Dynamics.Digest() == restored.Sim.Dynamics.Digest(),
                            "the solver's digest parted at physics step " + (i + 1) +
                            " after the restore");
                    }
                }
            }
        }

        /// <summary>
        /// A harness written in round 48's layout is read as round 48's build read it: every body
        /// with its contact and damage senses unwired and the rebuild count at zero.
        /// </summary>
        /// <remarks>
        /// Both halves are written by this build's own writers at the older layout, which leave
        /// out what <c>World.StateVersion</c> 11 and <c>Checkpoint.Version</c> 5 added; the
        /// harness knows which half it holds from the version the world has just read.
        /// </remarks>
        [Fact]
        public void ARound48HarnessIsReadWithItsSensesUnwired()
        {
            using (Pair live = Found())
            {
                for (int i = 0; i < 2_000; i++) live.Sim.Step();
                while (!live.Sim.Step()) { }

                Assert.True(BodiesTouched(live.Sim) > 0, "no body holds a contact array; the fixture tests nothing");

                live.Sim.SettleBeforeCheckpoint();

                byte[] lossy;

                using (var buffer = new MemoryStream())
                {
                    using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                    {
                        Invoke(live.World, "WriteState", w, World.LossyStateVersion);
                        Invoke(live.Sim, "WriteState", w, World.LossyStateVersion);
                        w.Flush();
                    }

                    lossy = buffer.ToArray();
                }

                using (Pair restored = Restore(lossy))
                {
                    Assert.Equal(World.LossyStateVersion, restored.World.StateVersionRead);
                    Assert.Equal(0L, restored.Sim.ModuleRebuilds);
                    Assert.Equal(live.Sim.Dynamics.Creatures.Count, restored.Sim.Dynamics.Creatures.Count);

                    foreach (Creature body in restored.Sim.Dynamics.Creatures)
                    {
                        Assert.Null(body.Senses.Contact);
                        Assert.Null(body.Senses.Damage);
                        Assert.NotNull(body.Senses.Reserve);
                        Assert.Null(OrganismOf(restored.World, body.Id).PartContact);
                    }
                }
            }
        }

        /// <summary>
        /// <c>--verify-checkpoint</c>'s founding mode on the touching world: stepped live, written,
        /// restored, compared member by member, senses and harness included, and stepped side by
        /// side. Returns 0 when the check passes.
        /// </summary>
        /// <remarks>
        /// In process, so the check itself is under test on a world where the senses matter; the
        /// farm program runs the same method on a real run's directory.
        /// </remarks>
        [Theory]
        [InlineData(20.0)]
        [InlineData(24.5)]
        public void TheFidelityCheckPassesOnATouchingWorld(double seconds)
        {
            string root = SimulationTests.Scratch("fidelity-" + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string run = Path.Combine(root, "run");
            string scratch = Path.Combine(root, "check");

            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(run);

            RunConfig config = TouchingWorld();
            File.WriteAllText(Path.Combine(run, "config.json"), RunConfigJson.Write(config));
            File.WriteAllText(Path.Combine(run, "run.json"), "{\"seed\": 1}\n");

            var console = new StringWriter();
            TextWriter was = Console.Out;
            int result;

            try
            {
                Console.SetOut(console);
                result = CheckpointFidelity.Run(run, seconds, scratch, threads: 2);
            }
            finally
            {
                Console.SetOut(was);
            }

            _out.WriteLine(console.ToString());
            Assert.Equal(0, result);
        }

        private static void Invoke(object target, string method, BinaryWriter w, int layout)
        {
            System.Reflection.MethodInfo writer = target.GetType().GetMethod(
                method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(BinaryWriter), typeof(int) }, null);

            Assert.NotNull(writer);
            writer.Invoke(target, new object[] { w, layout });
        }
    }
}

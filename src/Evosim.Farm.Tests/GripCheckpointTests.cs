using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A checkpoint taken while bodies hold the rock restores to the same world (r59 step 5,
    /// <c>World.StateVersion</c> 17): each body's touch of the rock and its grips, each
    /// organism's grip price owed and paid, and the world's grip total.
    /// </summary>
    /// <remarks>
    /// <b>A small crowded box whose founders carry the anchoring cell.</b> CheckpointRestoreTests'
    /// touching world, with the cell and the touch of the rock on and two founders in three
    /// drawn as anchoring parts, and tissue 50 kg/m3 denser than the water, as GripTests' is, so
    /// that they sink to the bed and grip there. Seeded, and the solver
    /// replays at any thread count, so the moment the test searches for is the same on every run.
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class GripCheckpointTests
    {
        private readonly ITestOutputHelper _out;

        public GripCheckpointTests(ITestOutputHelper output) => _out = output;

        private const int StepsPerMetabolic = 25;

        internal static RunConfig GrippingWorld()
        {
            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
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
                { "EVOSIM_CONTACT_PER_PART", "1" },
                { "EVOSIM_SENSE_BED_TOUCH", "1" },
                { "EVOSIM_ANCHORING", "1" },
                { "EVOSIM_EXCESS_DENSITY", "50" },
            })));

            config.Genome.FounderCellTypes = new[]
            {
                CellTypeIds.Anchoring, CellTypeIds.Anchoring, CellTypeIds.Photosynthetic,
            };

            return config;
        }

        private sealed class Pair : IDisposable
        {
            public World World;
            public Simulation Sim;

            public void Dispose() => Sim?.Dispose();
        }

        private static Pair Found()
        {
            var world = new World(GrippingWorld(), 1UL);

            return new Pair
            {
                World = world,
                Sim = new Simulation(world, 1UL, 0.02f, StepsPerMetabolic, threads: 2, runDirectory: null),
            };
        }

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

        private static int GripsHeld(Simulation sim)
        {
            int held = 0;
            foreach (Creature body in sim.Dynamics.Creatures) held += body.GripsHeld;
            return held;
        }

        private static int LinksTouching(Simulation sim)
        {
            int touching = 0;
            foreach (Creature body in sim.Dynamics.Creatures)
            {
                if (body.RockTouched == null) continue;
                for (int i = 0; i < body.Links; i++) if (body.RockTouched[i]) touching++;
            }

            return touching;
        }

        /// <summary>
        /// Steps a metabolic step at a time until three grips are held, three links touch the
        /// rock and a grip has been paid for, so every block step 5 writes holds something, and returns the checkpoint taken there and how many metabolic steps that took.
        /// </summary>
        private byte[] MidGrip(out int metabolicSteps)
        {
            using (Pair live = Found())
            {
                for (metabolicSteps = 1; metabolicSteps <= 120; metabolicSteps++)
                {
                    for (int i = 0; i < StepsPerMetabolic; i++) live.Sim.Step();

                    if (GripsHeld(live.Sim) >= 3 && LinksTouching(live.Sim) >= 3 && live.World.GripJoulesTotal > 0)
                    {
                        _out.WriteLine(
                            metabolicSteps + " metabolic steps: " + GripsHeld(live.Sim) + " grips held, " +
                            LinksTouching(live.Sim) + " links touching the rock, " +
                            live.World.GripJoulesTotal.ToString("R") + " J paid for grips");
                        return Checkpoint(live);
                    }
                }

                int anchored = 0;
                foreach (Creature body in live.Sim.Dynamics.Creatures) if (body.CanGrip) anchored++;

                throw new Xunit.Sdk.XunitException(
                    "No body gripped within 120 metabolic steps, so the world cannot test the grip's checkpoint: " +
                    live.World.Living.Count + " living, " + live.Sim.Dynamics.Creatures.Count + " bodies, " +
                    anchored + " with an anchoring part, " + LinksTouching(live.Sim) + " links touching the rock, " +
                    GripsHeld(live.Sim) + " grips held, " + live.World.GripJoulesTotal.ToString("R") + " J paid.");
            }
        }

        [Fact]
        public void ACheckpointTakenMidGripRestoresAndStepsAsTheWorldItWasTakenFrom()
        {
            byte[] state = MidGrip(out _);

            using (Pair first = Restore(state))
            using (Pair second = Restore(state))
            {
                // Written again at once, a restored world is the bytes it was read from.
                Assert.Equal(state, Checkpoint(first));
                Assert.True(GripsHeld(first.Sim) > 0);
                Assert.True(LinksTouching(first.Sim) > 0);

                // And two restores step the same bodies, over four metabolic steps, so through
                // grip charges, breaks and new grips, and end in the same state.
                for (int i = 0; i < 4 * StepsPerMetabolic; i++)
                {
                    first.Sim.Step();
                    second.Sim.Step();
                    Assert.Equal(first.Sim.Dynamics.Digest(), second.Sim.Dynamics.Digest());
                }

                Assert.Equal(Checkpoint(first), Checkpoint(second));
            }
        }

        /// <summary>
        /// The comparison above can see a grip: a restore whose grips are let go parts from one
        /// restored as the checkpoint says. Without this it could pass on a reader that dropped
        /// them, if no grip in the world happened to matter over the steps compared.
        /// </summary>
        [Fact]
        public void ARestoreWhoseGripsAreLetGoPartsFromTheRun()
        {
            byte[] state = MidGrip(out _);

            using (Pair kept = Restore(state))
            using (Pair dropped = Restore(state))
            {
                foreach (Creature body in dropped.Sim.Dynamics.Creatures)
                {
                    if (body.GripHeld != null) Array.Clear(body.GripHeld, 0, body.GripHeld.Length);
                }

                int partedAt = 0;
                for (int i = 0; i < 2 * StepsPerMetabolic && partedAt == 0; i++)
                {
                    kept.Sim.Step();
                    dropped.Sim.Step();
                    if (kept.Sim.Dynamics.Digest() != dropped.Sim.Dynamics.Digest()) partedAt = i + 1;
                }

                _out.WriteLine("parted at physics step " + partedAt);
                Assert.True(partedAt > 0);
            }
        }

        /// <summary>
        /// The farm's own check, on a run directory, at the second the search above found:
        /// a fresh run to that second, checkpointed, restored and stepped on, against the run.
        /// </summary>
        [Fact]
        public void TheFidelityCheckPassesMidGrip()
        {
            MidGrip(out int metabolicSteps);
            double seconds = metabolicSteps * StepsPerMetabolic * 0.02;

            string root = SimulationTests.Scratch("grip-fidelity");
            string run = Path.Combine(root, "run");
            string scratch = Path.Combine(root, "check");

            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(run);

            File.WriteAllText(Path.Combine(run, "config.json"), RunConfigJson.Write(GrippingWorld()));
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

        /// <summary>
        /// Version 16, round 59's layout before this step, is refused by name; version 15 is
        /// refused in a world that touches or grips, on writing and on reading.
        /// </summary>
        [Fact]
        public void VersionSixteenIsRefusedByNameAndFifteenInAWorldThatGrips()
        {
            using (Pair pair = Found())
            {
                byte[] world;
                using (var buffer = new MemoryStream())
                {
                    using (var w = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true)) pair.World.WriteState(w);
                    world = buffer.ToArray();
                }

                Assert.Equal(World.StateVersion, BitConverter.ToInt32(world, 4));
                Assert.Equal(20, World.StateVersion);
                Assert.False(World.ReadsStateVersion(World.GriplessStateVersion));

                byte[] sixteen = (byte[])world.Clone();
                sixteen[4] = (byte)World.GriplessStateVersion;
                var refused = Assert.Throws<InvalidDataException>(() => ReadWorld(sixteen));
                Assert.Contains("version 16", refused.Message);
                Assert.Contains("misread", refused.Message);

                byte[] fifteen = (byte[])world.Clone();
                fifteen[4] = (byte)World.PreLeafStateVersion;
                var gripless = Assert.Throws<InvalidDataException>(() => ReadWorld(fifteen));
                Assert.Contains("anchoring cell", gripless.Message);

                MethodInfo writer = typeof(World).GetMethod(
                    "WriteState", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(BinaryWriter), typeof(int) }, null);
                Assert.NotNull(writer);

                using (var buffer = new MemoryStream())
                using (var w = new BinaryWriter(buffer))
                {
                    var thrown = Assert.Throws<TargetInvocationException>(
                        () => writer.Invoke(pair.World, new object[] { w, World.PreLeafStateVersion }));
                    Assert.IsType<InvalidOperationException>(thrown.InnerException);
                }
            }
        }

        private static void ReadWorld(byte[] state)
        {
            var world = new World(GrippingWorld(), 1UL);
            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                world.ReadState(r);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D220's remains on the card engine, on ILGPU's CPU accelerator: a body flagged as remains runs no senses, no
    /// brain, no drive, no push and no grip on the card, takes no float lift, and keeps drag, buoyancy, gravity,
    /// contact and the joints' limits, as the farm's CPU solver steps it (<c>RemainsSolverTests</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three layers. The flag and its transitions are tested on the solver level, with the bodies and the tank of
    /// <c>GpuCpuAcceleratorTests</c>: a body flagged from the start, a body that becomes remains between two blocks
    /// (so the card holds a drive and a grip the host has just silenced), and the lift. The whole dial-on flow, deaths
    /// and cuts and remains, is tested through the farm's <c>Simulation</c>. And a checkpoint of that world resumes on
    /// a fresh engine.
    /// </para>
    /// <para>
    /// Every comparison with the CPU solver is to the bit, in double. Single is a cousin of the CPU by construction,
    /// so its test asserts what the flag takes away and nothing against the CPU.
    /// </para>
    /// </remarks>
    [Collection(FarmProcessCollection.Name)]
    public class GpuRemainsTests
    {
        private readonly ITestOutputHelper _out;

        public GpuRemainsTests(ITestOutputHelper output) => _out = output;

        // ------------------------------------------------------------------ the worlds

        private static Dictionary<string, string> Settings(string kind)
        {
            switch (kind)
            {
                case "torque":
                    return null;

                // The bend-and-power drive with the power's push and the swing's, as round 57 runs them.
                case "power":
                    return new Dictionary<string, string>
                    {
                        { "EVOSIM_POWER_PUSH", "2" }, { "EVOSIM_POSE_HZ", "2" }, { "EVOSIM_LIMB_PUSH", "0.3" },
                    };

                // Sinking tissue on the bed with the anchoring cell, a grip weak and quick to fade (the grip test's).
                case "grip":
                    return new Dictionary<string, string>
                    {
                        { "EVOSIM_ANCHORING", "1" }, { "EVOSIM_EXCESS_DENSITY", "500" }, { "EVOSIM_NEUTRAL_VOLUME", "0" },
                        { "EVOSIM_ANCHOR_DECAY", "1" },
                        { "EVOSIM_ANCHOR_RESERVE_THRESHOLD", "50" }, { "EVOSIM_ANCHOR_RESERVE_LAG", "0.2" },
                    };

                // Tissue far heavier than the water (and a lift past 1 lighter), so a part sinks or floats at once.
                case "sink":
                    return new Dictionary<string, string>
                    {
                        { "EVOSIM_EXCESS_DENSITY", "500" }, { "EVOSIM_NEUTRAL_VOLUME", "0" },
                    };

                default:
                    throw new ArgumentException(kind);
            }
        }

        private static (DynamicsWorld world, World core) Make(string kind, int count = 6)
        {
            (World core, SolverConfig solver) = GpuTank.Build(perPart: true, reefs: kind == "grip", extra: Settings(kind));
            var world = new DynamicsWorld(solver) { Threads = 2 };
            Fill(kind, world, core, count, 11UL, 1);
            return (world, core);
        }

        private static void Fill(string kind, DynamicsWorld world, World core, int count, ulong seed, int firstId)
        {
            var rng = new Rng(seed);
            Vec3 axis = GpuTank.Axis(core);

            for (int k = 0; k < count; k++)
            {
                Vec3 at;
                string cell = null;

                if (kind == "grip")
                {
                    double x = axis.X + 1.5 * (k % 3) - 1.5;
                    double z = axis.Z + 1.5 * (k / 3) - 0.75;
                    at = new Vec3(x, core.Bed.FloorY(x, z) + 0.1, z);
                    cell = CellTypeIds.Anchoring;
                }
                else
                {
                    // A cluster a couple of metres across at mid-depth, one at the glass and one on the bed.
                    at = new Vec3(axis.X + 0.7 * (k % 3) - 0.7, -9.0 - 0.6 * (k / 3), axis.Z + 0.5 * (k % 2));
                    if (k == 4) at = new Vec3(axis.X + core.TankRadiusMetres - 0.4, -6.0, axis.Z);
                    if (k == 5) at = new Vec3(axis.X - 2.0, -core.Config.WorldDepthMetres + 0.6, axis.Z - 2.0);
                }

                world.AddInIdOrder(GpuTank.Body(firstId + k, world.Config, core, rng, at, bodyCell: cell));
            }
        }

        private static void AssertSame(DynamicsWorld cpu, DynamicsWorld gpu, string when)
        {
            byte[] a = GpuTank.Snapshot(cpu), b = GpuTank.Snapshot(gpu);
            if (a.Length == b.Length && a.AsSpan().SequenceEqual(b)) return;

            Assert.Fail("The engines part " + when + ": " + GpuTank.FirstDifference(cpu, gpu));
        }

        private static Creature ById(DynamicsWorld world, long id) => world.Creatures.Single(c => c.Id == id);

        private static bool SameBits(double[] a, double[] b)
        {
            for (int k = 0; k < a.Length; k++)
            {
                if (BitConverter.DoubleToInt64Bits(a[k]) != BitConverter.DoubleToInt64Bits(b[k])) return false;
            }

            return true;
        }

        private static bool SameBody(Creature a, Creature b) =>
            SameBits(a.Position, b.Position) && SameBits(a.Velocity, b.Velocity) && SameBits(a.Spin, b.Spin) &&
            SameBits(a.Q, b.Q) && SameBits(a.Qd, b.Qd) && SameBits(a.Fext, b.Fext);

        /// <summary>No signal, no stroke, no torque applied, no grip held and none owed.</summary>
        private static bool Quiet(Creature c) =>
            c.DriveSignal.All(v => v == 0f) && c.Drive.Stroke.All(v => v == 0.0) &&
            c.Drive.AppliedTorque.All(v => v == 0.0) && c.GripsHeld == 0 && c.GripOwedJoules == 0.0;

        /// <summary><see cref="Quiet"/>, and a body that has never done work or been limited in a drive.</summary>
        private static bool Silent(Creature c) => Quiet(c) && c.MechanicalWorkJoules == 0.0 && c.DriveImpulsesLimited == 0;

        private static bool Moving(Creature c) =>
            c.DriveSignal.Any(v => v != 0f) || c.Drive.Stroke.Any(v => v != 0.0) || c.Drive.AppliedTorque.Any(v => v != 0.0);

        /// <summary>Both worlds' grip price drained between blocks, as the farm drains it; the two must agree.</summary>
        private static double Drain(DynamicsWorld cpu, DynamicsWorld gpu)
        {
            double a = 0, b = 0;
            foreach (Creature body in cpu.Creatures) a += body.DrainGripOwed();
            foreach (Creature body in gpu.Creatures) b += body.DrainGripOwed();
            Assert.Equal(a, b);
            return a;
        }

        // ------------------------------------------------------------------ a flagged body

        /// <summary>
        /// Two bodies of six flagged from the start: after every block, a death and a birth included, the card's world
        /// is the CPU solver's to the bit. The flagged bodies are shown silent on both, the living shown not to be, and
        /// the CPU's flagged world is shown to part from the world with no flag, so a flag that never reached a solver
        /// cannot pass as a match.
        /// </summary>
        [Theory]
        [InlineData("torque")]
        [InlineData("power")]
        [InlineData("grip")]
        public void FlaggedBodiesOnTheAcceleratorAreTheSolver(string kind)
        {
            (DynamicsWorld plain, World core0) = Make(kind);
            (DynamicsWorld cpu, World coreA) = Make(kind);
            (DynamicsWorld gpu, World coreB) = Make(kind);

            foreach (DynamicsWorld w in new[] { cpu, gpu })
            {
                w.Creatures[1].BecomeRemains();
                w.Creatures[3].BecomeRemains();
            }

            long[] flagged = { cpu.Creatures[1].Id, cpu.Creatures[3].Id };

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            bool livingMoved = false;
            double paid = 0;

            for (int block = 0; block < 4; block++)
            {
                plain.StepBlock(25);
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, kind + ", after block " + block);

                foreach (long id in flagged)
                {
                    Assert.True(ById(gpu, id).Remains);
                    Assert.True(Silent(ById(cpu, id)), kind + ": the CPU's flagged body " + id + " is not silent");
                    Assert.True(Silent(ById(gpu, id)), kind + ": the card's flagged body " + id + " is not silent");
                }

                livingMoved |= gpu.Creatures.Any(c => !c.Remains && Moving(c));
                paid += Drain(cpu, gpu);

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (plain, core0), (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[0].Id);
                        Fill(kind, w, core, 1, 99UL, 50);
                    }
                }
            }

            bool parted = false;
            foreach (long id in flagged) parted |= !SameBody(ById(plain, id), ById(cpu, id));

            _out.WriteLine(kind + ": living bodies moved " + livingMoved + ", grip paid " + paid.ToString("0.######") + " J");
            Assert.True(parted, kind + ": the flagged world stepped as the one without a flag");
            Assert.True(livingMoved, kind + ": no living body drove, so the silence of the flagged proves nothing");
            if (kind == "grip") Assert.True(gpu.Creatures.Where(c => !c.Remains).Sum(c => c.GripsFormed) > 0, "no living body gripped");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// The same flags in single precision: the card is a cousin of the CPU there, so the test asks only what the
        /// flag takes away. A flagged body's brain, drive, stroke, torque, work and grip stay at zero over the blocks,
        /// while a living body's do not.
        /// </summary>
        [Theory]
        [InlineData("torque")]
        [InlineData("power")]
        [InlineData("grip")]
        public void FlaggedBodiesOnTheAcceleratorInSingleRunNothing(string kind)
        {
            (DynamicsWorld gpu, _) = Make(kind);
            gpu.Creatures[1].BecomeRemains();
            gpu.Creatures[3].BecomeRemains();
            long[] flagged = { gpu.Creatures[1].Id, gpu.Creatures[3].Id };

            var options = new GpuOptions { Device = "cpu", Precision = "single", CpuThreads = 4 };
            using var backend = new GpuBackend(gpu.Config, options);
            gpu.UseBackend(backend);

            bool livingMoved = false;
            for (int block = 0; block < 4; block++)
            {
                gpu.StepBlock(25);
                foreach (long id in flagged) Assert.True(Silent(ById(gpu, id)), kind + ": flagged body " + id + " after block " + block);
                livingMoved |= gpu.Creatures.Any(c => !c.Remains && Moving(c));
            }

            Assert.True(livingMoved, kind + ": no living body drove");
            Assert.Equal(0, backend.RefusedForClass);
        }

        /// <summary>
        /// A flagged body takes no lift and still drags and sinks. The tissue is heavy (500 over the water) and one
        /// body's lift is set to 2, which floats a part that takes it. The flag is shown to take the lift on the card
        /// exactly as zeroing the lift on the host does, and as the CPU solver's rule does. The lift is first shown
        /// to matter to a body that is not remains: it rises, where the flagged body sinks.
        /// </summary>
        [Fact]
        public void AFlaggedBodyOnTheAcceleratorTakesNoLiftAndStillDragsAndSinks()
        {
            const int Body = 1;

            Creature Lifted(DynamicsWorld w, double lift)
            {
                Creature c = w.Creatures[Body];
                for (int l = 0; l < c.Links; l++) c.Lift[l] = lift;
                return c;
            }

            (DynamicsWorld unflaggedHigh, _) = Make("sink");
            (DynamicsWorld unflaggedZero, _) = Make("sink");
            (DynamicsWorld cpuHigh, _) = Make("sink");
            (DynamicsWorld cpuZero, _) = Make("sink");
            (DynamicsWorld gpuHigh, _) = Make("sink");
            (DynamicsWorld gpuZero, _) = Make("sink");

            Lifted(unflaggedHigh, 2.0);
            Lifted(unflaggedZero, 0.0);
            foreach ((DynamicsWorld w, double lift) in new[] { (cpuHigh, 2.0), (cpuZero, 0.0), (gpuHigh, 2.0), (gpuZero, 0.0) })
            {
                Lifted(w, lift).BecomeRemains();
            }

            double y0 = gpuHigh.Creatures[Body].Position[1];

            using var backend = new GpuBackend(gpuHigh.Config, GpuTank.CpuDouble());
            gpuHigh.UseBackend(backend);
            using var backendZero = new GpuBackend(gpuZero.Config, GpuTank.CpuDouble());
            gpuZero.UseBackend(backendZero);

            for (int block = 0; block < 2; block++)
            {
                unflaggedHigh.StepBlock(25);
                unflaggedZero.StepBlock(25);
                cpuHigh.StepBlock(25);
                cpuZero.StepBlock(25);
                gpuHigh.StepBlock(25);
                gpuZero.StepBlock(25);
            }

            double yRisen = unflaggedHigh.Creatures[Body].Position[1];
            double ySunk = gpuHigh.Creatures[Body].Position[1];
            _out.WriteLine("from " + y0.ToString("0.###") + " m: a body taking lift 2 is at " + yRisen.ToString("0.###") +
                           " m, remains with the same lift at " + ySunk.ToString("0.###") + " m");

            Assert.False(SameBody(unflaggedHigh.Creatures[Body], unflaggedZero.Creatures[Body]), "the lift made no difference to a body that is not remains");
            Assert.True(yRisen > y0, "a body taking lift 2 did not rise");
            Assert.True(SameBody(cpuHigh.Creatures[Body], cpuZero.Creatures[Body]), "the CPU's remains took lift");
            Assert.True(SameBody(gpuHigh.Creatures[Body], gpuZero.Creatures[Body]), "the card's remains took lift");
            AssertSame(cpuHigh, gpuHigh, "with the lift raised");
            AssertSame(cpuZero, gpuZero, "with the lift zeroed");

            // Gravity and the water: the flagged body falls, and the drag it meets is counted as for any body.
            Creature flagged = gpuHigh.Creatures[Body];
            Assert.True(Silent(flagged));
            Assert.True(ySunk < y0, "the flagged body did not sink: " + y0 + " to " + ySunk);
            Assert.True(flagged.DissipatedJoules > 0, "the water took nothing from the flagged body");
            Assert.True(flagged.DissipatedJoules == cpuHigh.Creatures[Body].DissipatedJoules);
        }

        // ------------------------------------------------------------------ a body that becomes remains on the card

        /// <summary>
        /// Two bodies turn to remains between blocks while the card holds their drive, their envelope and their grips,
        /// and a third one block later. <c>BecomeRemains</c> silences the host's copy; the engine must pack the card's
        /// copy again before the next block steps the body. After each block the card's world is the CPU's to the bit,
        /// and the bodies are silent on both. Before the change a body is shown driving (or gripping) on the CPU, so
        /// the change has something to silence.
        /// </summary>
        [Theory]
        [InlineData("torque")]
        [InlineData("power")]
        [InlineData("grip")]
        public void ABodyThatBecomesRemainsWhileOnTheAcceleratorIsTheSolver(string kind)
        {
            (DynamicsWorld cpu, World coreA) = Make(kind);
            (DynamicsWorld gpu, World coreB) = Make(kind);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            long[] first = { cpu.Creatures[0].Id, cpu.Creatures[2].Id };
            long second = cpu.Creatures[4].Id;
            var silenced = new Dictionary<long, (double Work, long Limited)>();

            double paid = 0;
            bool drivingBefore = false, grippingBefore = false;

            for (int block = 0; block < 6; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, kind + ", after block " + block);
                paid += Drain(cpu, gpu);

                // Nothing new from a body since it turned to remains: no signal, stroke, torque or grip, and the work
                // and the drive's limiter count are what they were when it turned.
                foreach (KeyValuePair<long, (double Work, long Limited)> held in silenced)
                {
                    foreach ((string who, DynamicsWorld w) in new[] { ("CPU's", cpu), ("card's", gpu) })
                    {
                        Creature c = ById(w, held.Key);
                        Assert.True(Quiet(c), kind + ": the " + who + " body " + held.Key + " is not quiet after block " + block);
                        Assert.True(c.MechanicalWorkJoules == held.Value.Work && c.DriveImpulsesLimited == held.Value.Limited,
                                    kind + ": the " + who + " body " + held.Key + " did work after it turned to remains");
                    }
                }

                if (block == 1)
                {
                    foreach (long id in first)
                    {
                        drivingBefore |= Moving(ById(gpu, id));
                        grippingBefore |= ById(gpu, id).GripsHeld > 0;
                    }

                    foreach (DynamicsWorld w in new[] { cpu, gpu })
                    {
                        foreach (long id in first) ById(w, id).BecomeRemains();
                    }

                    foreach (long id in first) silenced[id] = (ById(gpu, id).MechanicalWorkJoules, ById(gpu, id).DriveImpulsesLimited);

                    // The host copies agree at once: nothing waits for the next block to be quiet.
                    foreach (long id in first) Assert.True(Quiet(ById(gpu, id)));
                }

                if (block == 2)
                {
                    foreach (DynamicsWorld w in new[] { cpu, gpu }) ById(w, second).BecomeRemains();
                    silenced[second] = (ById(gpu, second).MechanicalWorkJoules, ById(gpu, second).DriveImpulsesLimited);
                }

                if (block == 3)
                {
                    foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) })
                    {
                        w.Remove(w.Creatures[1].Id);
                        Fill(kind, w, core, 1, 99UL, 50);
                    }
                }
            }

            _out.WriteLine(kind + ": driving before " + drivingBefore + ", gripping before " + grippingBefore + ", paid " + paid.ToString("0.######") + " J");
            Assert.True(drivingBefore || grippingBefore, kind + ": the bodies were doing nothing when they turned to remains");
            if (kind == "grip") Assert.True(grippingBefore, "no body held a grip when it turned to remains");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        // ------------------------------------------------------------------ the dial-on flow

        /// <summary>
        /// <c>RemainsFixtures.Config</c>'s touching world as a tank with a moving current and no water hold, which is
        /// what the card engine will step (a box needs its seams read across, and a still current is refused).
        /// </summary>
        private static RunConfig DialOn()
        {
            RunConfig config = EnvBinding.BuildConfig(EnvBinding.Read(EnvBinding.Of(new Dictionary<string, string>
            {
                { "EVOSIM_AREA", "100" }, { "EVOSIM_DEPTH", "20" }, { "EVOSIM_FOUNDER_DEPTH", "20" },
                { "EVOSIM_PATCHES", "1" }, { "EVOSIM_SHARED_SPACE", "1" },
                { "EVOSIM_FIELD", "grid" }, { "EVOSIM_FIELD_CELL", "2" }, { "EVOSIM_FIELD_MATTER_CELL", "5" },
                { "EVOSIM_IRRADIANCE", "200" }, { "EVOSIM_MATTER_BUDGET", "300" },
                { "EVOSIM_TISSUE_ENERGY", "500" }, { "EVOSIM_RHO", "100" }, { "EVOSIM_OVERHEAD", "100" },
                { "EVOSIM_DT", "0.02" }, { "EVOSIM_SENSE_CONTACT", "1" }, { "EVOSIM_SENSE_DAMAGE", "1" },
                { "EVOSIM_SHAPE", "tank" }, { "EVOSIM_CURRENT_MODE", "Transport" }, { "EVOSIM_CURRENT", "0.05" },
                { "EVOSIM_WATER_HOLD", "0" },
            })));

            config.MinimumPopulation = 0;
            config.ContactEating = true;
            config.RemainsWindowSeconds = 60f;
            return config;
        }

        private static RemainsFixtures.Pair Arena() =>
            RemainsFixtures.StarvedArena(DialOn(), RemainsFixtures.Fork(4), RemainsFixtures.Fork(), RemainsFixtures.Claw());

        private static GpuBackend OnTheAccelerator(RemainsFixtures.Pair pair, string precision = "double")
        {
            var backend = new GpuBackend(
                pair.Sim.Solver, new GpuOptions { Device = "cpu", Precision = precision, CpuThreads = 4 });
            pair.Sim.Dynamics.UseBackend(backend);
            return backend;
        }

        private static void Bite(RemainsFixtures.Pair pair, long biter, long target, int part)
        {
            pair.World.SetContacts(new List<CreatureContact> { new CreatureContact(biter, 0, target, part) });
            pair.World.ApplyMouth(100f);
            pair.Sim.PoseFoundings();
            pair.Sim.RebuildChangedPlans();
        }

        /// <summary>
        /// A death into whole-body remains, then three cuts (one into the living, two into the dead, so that remains
        /// shed a part and are rebuilt), all with the bodies moving; the same script on any pair.
        /// </summary>
        private static void Script(RemainsFixtures.Pair pair, out long first, out long second, out long claw)
        {
            long firstId = pair.World.Living[0].Id;
            long secondId = pair.World.Living[1].Id;
            long clawId = pair.World.Living[2].Id;
            first = firstId;
            second = secondId;
            claw = clawId;

            pair.Sim.TryPose(firstId, out Creature a);
            pair.Sim.TryPose(secondId, out Creature b);
            RemainsFixtures.Excite(a);
            RemainsFixtures.Excite(b);

            RemainsFixtures.Starve(pair);
            pair.Sim.SettleBeforeCheckpoint();
            Assert.True(pair.World.Remains.Any(r => r.Id == firstId), "the first fork did not die into remains");

            Bite(pair, clawId, secondId, 2);
            Bite(pair, clawId, firstId, 1);
        }

        private static void AssertSameWorld(RemainsFixtures.Pair cpu, RemainsFixtures.Pair gpu, string when)
        {
            Assert.Equal(cpu.World.Remains.Count, gpu.World.Remains.Count);
            Assert.Equal(cpu.World.Living.Count, gpu.World.Living.Count);
            Assert.Equal(cpu.Sim.Dynamics.Creatures.Count, gpu.Sim.Dynamics.Creatures.Count);
            AssertSame(cpu.Sim.Dynamics, gpu.Sim.Dynamics, when);
        }

        /// <summary>
        /// The dial-on flow through the farm's own steps: a fork starves into remains, a claw cuts the dead and the
        /// living, limbs are founded and remains are rebuilt on their new plans, all stepped on the accelerator and on
        /// the CPU solver from one start. After every metabolic step the two worlds are the same to the bit.
        /// </summary>
        [Fact]
        public void ADialOnWorldWithDeathsCutsAndRemainsOnTheAcceleratorIsTheCpuSolversWorld()
        {
            using RemainsFixtures.Pair cpu = Arena();
            using RemainsFixtures.Pair gpu = Arena();
            using GpuBackend backend = OnTheAccelerator(gpu);

            Script(cpu, out long first, out long second, out long claw);
            Script(gpu, out _, out _, out _);

            AssertSameWorld(cpu, gpu, "before the first step");

            bool livingDrove = false;
            for (int boundary = 0; boundary < 10; boundary++)
            {
                while (!cpu.Sim.Step()) { }
                while (!gpu.Sim.Step()) { }
                AssertSameWorld(cpu, gpu, "after metabolic step " + boundary);

                livingDrove |= gpu.Sim.Dynamics.Creatures.Any(c => !c.Remains && Moving(c));

                // The standing remains are silent on the card.
                foreach (Creature c in gpu.Sim.Dynamics.Creatures.Where(c => c.Remains))
                {
                    Assert.True(Silent(c), "remains " + c.Id + " is not silent after metabolic step " + boundary);
                }

                // Another cut into the remains, mid-run, on both.
                if (boundary == 3)
                {
                    foreach (RemainsFixtures.Pair p in new[] { cpu, gpu })
                    {
                        if (p.World.Remains.Any(r => r.Id == first) && p.World.Living.Any(l => l.Id == claw)) Bite(p, claw, first, 1);
                    }
                }
            }

            int standing = gpu.World.Remains.Count;
            _out.WriteLine(standing + " remains standing on the accelerator at t=" + gpu.World.ElapsedSeconds.ToString("0.#") +
                           " s, living bodies drove: " + livingDrove);
            Assert.True(standing >= 2, "the script left too few remains to test");
            Assert.True(gpu.World.Remains.Any(r => r.ParentId >= 0), "no limb was cut off and left as remains");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        // ------------------------------------------------------------------ the checkpoint

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

        private static RemainsFixtures.Pair Restore(byte[] state)
        {
            RemainsFixtures.Pair pair = RemainsFixtures.Found(DialOn());

            using (var buffer = new MemoryStream(state, writable: false))
            using (var r = new BinaryReader(buffer, Encoding.UTF8))
            {
                pair.World.ReadState(r);
                pair.Sim.ReadState(r);
                Assert.Equal(state.Length, buffer.Position);
            }

            return pair;
        }

        /// <summary>
        /// A dial-on checkpoint with remains standing in it, taken from a run on the accelerator, resumes on a fresh
        /// engine equal to the run it was taken from: a state digest every 25 physics steps and the checkpoint itself
        /// after 500 steps.
        /// </summary>
        [Fact]
        public void ADialOnCheckpointWithRemainsResumesOnTheAcceleratorEqualToTheUnbrokenRun()
        {
            using RemainsFixtures.Pair run = Arena();
            using GpuBackend runBackend = OnTheAccelerator(run);

            Script(run, out long first, out _, out _);

            // Into the water, moving, with the dead cut once more.
            while (!run.Sim.Step()) { }
            for (int i = 0; i < 50; i++) run.Sim.Step();
            while (!run.Sim.Step()) { }

            int standing = run.World.Remains.Count;
            _out.WriteLine(standing + " remains standing at t=" + run.World.ElapsedSeconds.ToString("0.#") + " s, " +
                           run.Sim.Dynamics.Creatures.Count + " bodies");
            Assert.True(standing >= 3, "the fixture left too few remains to test a checkpoint on");
            Assert.Contains(run.World.Remains, r => r.Id == first);
            Assert.Contains(run.World.Remains, r => r.ParentId >= 0 && r.Id != first);

            byte[] state = CheckpointOf(run);

            using RemainsFixtures.Pair resumed = Restore(state);
            using GpuBackend resumedBackend = OnTheAccelerator(resumed);

            Assert.Equal(standing, resumed.World.Remains.Count);
            Assert.All(resumed.World.Remains, r => Assert.True(
                resumed.Sim.TryPose(r.Id, out Creature c) && c.Remains, "restored remains " + r.Id + " is not flagged"));
            Assert.Equal(state, CheckpointOf(resumed));

            for (int step = 1; step <= 500; step++)
            {
                run.Sim.Step();
                resumed.Sim.Step();

                if (step % 25 == 0)
                {
                    AssertSameWorld(run, resumed, "the resume parted from the run at physics step " + step);
                }
            }

            Assert.Equal(run.World.Remains.Count, resumed.World.Remains.Count);
            Assert.Equal(CheckpointOf(run), CheckpointOf(resumed));
            Assert.Equal(0L, runBackend.OverflowTotal);
            Assert.Equal(0L, resumedBackend.OverflowTotal);
            _out.WriteLine("after 500 steps: " + resumed.World.Remains.Count + " remains, " + resumed.World.RemainsClosed + " closed");
        }
    }
}

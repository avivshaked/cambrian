using System;
using System.Collections.Generic;
using System.IO;
using Evosim.Core;
using Evosim.Dynamics;
using Evosim.Farm.Gpu;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// D270's carried leaf smell and current sense on the gpu engine, on ILGPU's CPU accelerator in double against the
    /// solver. The card copies each link's held readings (<c>CreatureSenses.HeldFar</c>) into two columns and reads them in
    /// the brain's sensor read; it computes nothing, and the plume's grid stays on the host.
    /// </summary>
    /// <remarks>
    /// <b>In GpuSquare's box, since the plume is refused in a tank.</b> The bodies stand in the middle of the 60 m box, clear
    /// of the seams, and the plume is laid into by hand (the same deposits in every world) because these tests step bodies
    /// and not the ecology. The held readings are taken before each block, as the farm takes them at a metabolic step.
    /// </remarks>
    public class GpuFarSmellTests
    {
        private readonly ITestOutputHelper _out;

        public GpuFarSmellTests(ITestOutputHelper output) => _out = output;

        private static readonly SensorChannel[] FarPool =
        {
            SensorChannel.LeafPlume, SensorChannel.LeafPlumeGradient, SensorChannel.GroundCurrent,
        };

        private static Dictionary<string, string> Settings(bool plume, bool current, bool central)
        {
            var settings = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in GpuSquare.Settings) settings[kv.Key] = kv.Value;
            if (plume)
            {
                settings["EVOSIM_LEAF_PLUME"] = "gradient";
                settings["EVOSIM_LEAF_PLUME_CELL"] = "5";
                settings["EVOSIM_LEAF_PLUME_HALF"] = "0.5";
            }

            if (current)
            {
                settings["EVOSIM_CURRENT_SENSE"] = "1";
                settings["EVOSIM_CURRENT_SENSE_HALF"] = "0.05";
            }

            if (central)
            {
                settings["EVOSIM_BRAIN_MODEL"] = "central";
                settings["EVOSIM_NEURON_COST"] = "0";
                settings["EVOSIM_CONNECTION_COST"] = "0";
            }

            return settings;
        }

        private static (DynamicsWorld world, World core) Pair(Dictionary<string, string> settings, bool nose)
        {
            (World core, SolverConfig solver) = GpuTank.Build(
                true, false, extra: settings,
                tune: nose ? (Action<RunConfig>)(config => config.CellTypes = config.CellTypes.Appending(new NoseCell())) : null);
            return (new DynamicsWorld(solver) { Threads = 2 }, core);
        }

        private static Vec3 Place(World core, int k)
        {
            double length = core.BoxLengthMetres, width = core.BoxWidthMetres;
            return new Vec3(length / 2 + 2.5 * (k % 3) - 2.5, -9.0 - 0.6 * (k / 3), width / 2 + 2.5 * ((k / 3) % 2));
        }

        private static void Populate(DynamicsWorld world, World core, int count, ulong seed, int firstId, bool nose, bool central)
        {
            var rng = new Rng(seed);
            for (int k = 0; k < count; k++)
            {
                Vec3 at = Place(core, k);
                Creature body = nose
                    ? GpuTank.NoseBody(firstId + k, world.Config, core, rng, at, FarPool, central, 99UL)
                    : GpuTank.Body(firstId + k, world.Config, core, rng, at, sensorPool: FarPool, central: central);
                body.Senses.HeldSniff = true;
                world.AddInIdOrder(body);
            }
        }

        /// <summary>Leaf smell laid into the plume round the bodies' cluster, moved a little each round; the same in every world it is put in.</summary>
        private static void Lay(World core, int round)
        {
            if (core.LeafPlume == null) return;

            double x = core.BoxLengthMetres / 2, z = core.BoxWidthMetres / 2, d = 1.5 * round;
            core.LeafPlume.Deposit(new Float3((float)(x + 2.0 + d), -9f, (float)(z - 1.5)), 250.0);
            core.LeafPlume.Deposit(new Float3((float)(x - 3.0), (float)(-10.0 - d), (float)(z + 2.0)), 400.0);
            core.LeafPlume.Deposit(new Float3((float)(x + 0.5), -8f, (float)(z + 3.5 - d)), 150.0);
            core.LeafPlume.RebuildGradient();
        }

        private static void Hold(DynamicsWorld world, long round, bool nose)
        {
            foreach (Creature body in world.Creatures)
            {
                if (nose) body.Senses.Sniff(round);
                body.Senses.HoldFar();
            }
        }

        private static bool Reads(DynamicsWorld world, SensorChannel channel)
        {
            foreach (Creature c in world.Creatures)
            {
                if (!c.Alive || !Brain.MaskReads(c.Brain.SensorMask, channel)) continue;
                for (int part = 0; part < c.Links; part++)
                {
                    for (int index = 0; index < channel.IndexCount(); index++)
                    {
                        if (c.Senses.Read(part, channel, index) != 0f) return true;
                    }
                }
            }

            return false;
        }

        private static byte[] BrainState(Creature body)
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream)) body.Brain.WriteState(w);
            return stream.ToArray();
        }

        private static void AssertSame(DynamicsWorld cpu, DynamicsWorld gpu, string when)
        {
            byte[] a = GpuTank.Snapshot(cpu), b = GpuTank.Snapshot(gpu);
            if (a.Length == b.Length && a.AsSpan().SequenceEqual(b)) return;

            Assert.Fail("The engines part " + when + ": " + GpuTank.FirstDifference(cpu, gpu));
        }

        /// <summary>
        /// The carried leaf smell (strength and gradient) and the ground current, with and without the nose and under the
        /// per-part and the one brain. Before every block each body of the CPU's world and the card's holds its far readings
        /// (the nose's sniff as well, in a nose world), and the two worlds are the solver's to the bit after every block, a
        /// death and a birth included. A third world, on the CPU, never holds anything: it is shown at the end to have parted
        /// from the first, so that readings that never reached the trajectory cannot pass.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TheFarSmellOnTheCpuAcceleratorIsTheSolver(bool nose, bool central)
        {
            string label = (nose ? "nose" : "plain") + "/" + (central ? "central" : "perpart");
            Dictionary<string, string> settings = Settings(plume: true, current: true, central: central);

            (DynamicsWorld blind, World core0) = Pair(settings, nose);
            (DynamicsWorld cpu, World coreA) = Pair(settings, nose);
            (DynamicsWorld gpu, World coreB) = Pair(settings, nose);
            Assert.Equal(LeafPlumeForm.Gradient, gpu.Config.LeafPlume);
            Assert.True(gpu.Config.CurrentSense);
            Assert.Equal(nose, gpu.Config.Nose != null);
            Assert.NotNull(coreA.LeafPlume);

            var worlds = new[] { (blind, core0), (cpu, coreA), (gpu, coreB) };
            foreach ((DynamicsWorld w, World core) in worlds) Populate(w, core, 6, 11UL, 1, nose, central);

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            var sensed = new Dictionary<SensorChannel, bool>();
            foreach (SensorChannel channel in FarPool) sensed[channel] = false;

            for (int block = 0; block < 5; block++)
            {
                foreach ((DynamicsWorld w, World core) in worlds) Lay(core, block);
                Hold(cpu, block, nose);
                Hold(gpu, block, nose);

                blind.StepBlock(25);
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, label + ", after block " + block);
                foreach (SensorChannel channel in FarPool) sensed[channel] |= Reads(cpu, channel);

                if (block == 1)
                {
                    foreach ((DynamicsWorld w, World core) in worlds)
                    {
                        w.Remove(w.Creatures[0].Id);
                        Populate(w, core, 1, 99UL, 50, nose, central);
                    }
                }
            }

            foreach (SensorChannel channel in FarPool)
            {
                _out.WriteLine(label + ": " + channel + (sensed[channel] ? " read as not 0" : " never read as not 0"));
            }

            Assert.True(sensed[SensorChannel.GroundCurrent], label + ": no brain read a ground current that was not 0");
            Assert.True(sensed[SensorChannel.LeafPlume] || sensed[SensorChannel.LeafPlumeGradient],
                label + ": no brain read a carried smell that was not 0");

            // Parted in a brain's neurons, a position or the drive the brain sent its joints, all of which AssertSame
            // compares: the card matched the world that held its readings to the bit, so it read them.
            bool parted = false;
            for (int i = 0; i < cpu.Creatures.Count && !parted; i++)
            {
                parted = !BrainState(blind.Creatures[i]).AsSpan().SequenceEqual(BrainState(cpu.Creatures[i]));
            }

            for (int i = 0; i < cpu.Creatures.Count && !parted; i++)
            {
                double[] a = blind.Creatures[i].Position, b = cpu.Creatures[i].Position;
                for (int k = 0; k < a.Length && !parted; k++)
                {
                    parted = BitConverter.DoubleToInt64Bits(a[k]) != BitConverter.DoubleToInt64Bits(b[k]);
                }

                float[] da = blind.Creatures[i].DriveSignal, db = cpu.Creatures[i].DriveSignal;
                for (int k = 0; k < da.Length && !parted; k++)
                {
                    parted = BitConverter.SingleToInt32Bits(da[k]) != BitConverter.SingleToInt32Bits(db[k]);
                }
            }

            Assert.True(parted, label + ": the world that held nothing stepped and drove as the one that held its readings");

            // Both columns were sized to the links and uploaded with the blocks.
            Assert.True(backend.FarHeldCells > 8, "the far-sense held columns are stubs with both switches on");
            Assert.True(backend.FarHeldBytesUp > 0, "no block uploaded the far-sense held columns");
            Assert.Equal(0, backend.RefusedForClass);
            Assert.Equal(0L, backend.OverflowTotal);
        }

        /// <summary>
        /// The card reads only what <c>HoldFar</c> held, so a body whose far readings are taken afresh on every step (a bench's,
        /// <c>HeldSniff</c> off) would part from the solver; the card refuses it when it packs the body.
        /// </summary>
        [Fact]
        public void ABodyWhoseFarSensesAreNotHeldIsRefused()
        {
            Dictionary<string, string> settings = Settings(plume: true, current: true, central: false);
            (DynamicsWorld gpu, World core) = Pair(settings, nose: false);
            Populate(gpu, core, 2, 11UL, 1, nose: false, central: false);
            gpu.Creatures[1].Senses.HeldSniff = false;

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            Exception thrown = Assert.ThrowsAny<Exception>(() => gpu.StepBlock(1));
            Exception refused = thrown is AggregateException all ? all.Flatten().InnerExceptions[0] : thrown;
            Assert.IsType<InvalidOperationException>(refused);
            Assert.Contains("HeldSniff", refused.Message);
        }

        /// <summary>
        /// With both switches off the two columns are one-cell stubs, one each a size class (there are four), and no block
        /// moves a byte of either; the world is the solver's to the bit.
        /// </summary>
        [Fact]
        public void AWorldWithBothSwitchesOffHoldsStubsAndUploadsNoHeldBytes()
        {
            Dictionary<string, string> settings = Settings(plume: false, current: false, central: false);
            SensorChannel[] pool = { SensorChannel.Depth, SensorChannel.Flow };

            (DynamicsWorld cpu, World coreA) = Pair(settings, nose: false);
            (DynamicsWorld gpu, World coreB) = Pair(settings, nose: false);
            Assert.Equal(LeafPlumeForm.None, gpu.Config.LeafPlume);
            Assert.False(gpu.Config.CurrentSense);
            Assert.Null(coreB.LeafPlume);

            foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) })
            {
                var rng = new Rng(11UL);
                for (int k = 0; k < 6; k++) w.AddInIdOrder(GpuTank.Body(1 + k, w.Config, core, rng, Place(core, k), sensorPool: pool));
            }
            // Neither switch is on, so the farm's HeldSniff is not needed and the bench's bodies are the card's to step.

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            for (int block = 0; block < 3; block++)
            {
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "with both far senses off, after block " + block);
            }

            Assert.Equal(8L, backend.FarHeldCells);
            Assert.Equal(0L, backend.FarHeldBytesUp);
            Assert.Equal(0L, backend.HeldBytesUp);
            Assert.Equal(0, backend.RefusedForClass);
        }

        /// <summary>
        /// Each switch sizes only its own column: the current sense alone leaves the plume's column a stub and uploads bytes
        /// for the current's, and the plume alone the other way about.
        /// </summary>
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void EachSwitchSizesOnlyItsOwnColumn(bool plume, bool current)
        {
            Dictionary<string, string> settings = Settings(plume, current, central: false);
            SensorChannel[] pool = plume
                ? new[] { SensorChannel.LeafPlume, SensorChannel.LeafPlumeGradient }
                : new[] { SensorChannel.GroundCurrent };

            (DynamicsWorld cpu, World coreA) = Pair(settings, nose: false);
            (DynamicsWorld gpu, World coreB) = Pair(settings, nose: false);

            foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) })
            {
                var rng = new Rng(11UL);
                for (int k = 0; k < 4; k++)
                {
                    Creature body = GpuTank.Body(1 + k, w.Config, core, rng, Place(core, k), sensorPool: pool);
                    body.Senses.HeldSniff = true;
                    w.AddInIdOrder(body);
                }
            }

            using var backend = new GpuBackend(gpu.Config, GpuTank.CpuDouble());
            gpu.UseBackend(backend);

            for (int block = 0; block < 3; block++)
            {
                foreach ((DynamicsWorld w, World core) in new[] { (cpu, coreA), (gpu, coreB) }) Lay(core, block);
                Hold(cpu, block, false);
                Hold(gpu, block, false);
                cpu.StepBlock(25);
                gpu.StepBlock(25);
                AssertSame(cpu, gpu, "plume " + plume + ", current " + current + ", after block " + block);
            }

            // Eight stubs' cells (a plume and a current column in each of four classes) with one of them filled out.
            Assert.True(backend.FarHeldCells > 8);
            Assert.True(backend.FarHeldBytesUp > 0);
            Assert.Equal(0, backend.RefusedForClass);
        }
    }
}

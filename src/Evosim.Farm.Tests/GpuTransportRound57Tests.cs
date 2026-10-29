using System;
using System.IO;
using Evosim.Core;
using Evosim.Farm.Gpu;
using Evosim.TestSupport;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// T1 (card-single-plan.md, stage 2a): the snow's transport on the card carries round 57's own
    /// world, read from r57-s2's config (in round 59's format, fixtures/r57-s2-config.json) and founded on its seed, rather than the engine's test
    /// tank. Round 57's tank has a tilted, shaped bed with hollows and ridges, a shore with its fade,
    /// reefs at a quarter of the floor and elongated streams, and the grid must hand the device its
    /// tables for all of them (<see cref="GridField.OfferTransportDevice"/>) and the device must give
    /// the grid's own numbers to the bit, on ILGPU's CPU accelerator, as
    /// <see cref="GpuTransportTests"/> checks in the test tank.
    /// </summary>
    /// <remarks>
    /// The run directory is gitignored and belongs to the main working tree, as
    /// <c>Evosim.Dynamics.Tests.RunFixture</c>'s is; on a machine without it the test is skipped, naming the path
    /// (<see cref="LocalDataFactAttribute"/>).
    /// </remarks>
    public class GpuTransportRound57Tests
    {
        internal const string Relative = "runs/r57-s2/2026-10-06-065827-dfc0e881";

        /// <summary>The run's folder, found in this tree or above it; the folder it would have in this tree where it is nowhere.</summary>
        internal static string RunDirectory =>
            Path.Combine(LocalData.FindRoot(Relative + "/pool") ?? LocalData.RepoRoot ?? "", Relative.Replace('/', Path.DirectorySeparatorChar));

        // r57-s2's own seed (its run.json), so the bed and the reefs are the round's.
        private const ulong Seed = 2UL;

        private readonly ITestOutputHelper _out;

        public GpuTransportRound57Tests(ITestOutputHelper output) => _out = output;

        // r57-s2's config in round 59's format (fixtures/r57-s2-config.json): its own settings, with
        // the keys round 59 added at the defaults that leave the world as round 57's. The run's own
        // config.json predates them, and the loader refuses a config that lacks one.
        private static RunConfig Config() => Round57Config();

        internal static RunConfig Round57Config()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "r57-s2-config.json");
            RunConfig config = RunConfigJson.Read(File.ReadAllText(path), out string mismatch);
            Assert.Null(mismatch);
            return config;
        }

        // Snow in boxes over the whole tank at three depths, so the faces near the shore, over the
        // hollows and ridges and around the reefs all carry a gradient. A box the bed covers entirely
        // lands in the cell at its centre (GridField.DepositBox) on both worlds alike.
        private static World Seeded(RunConfig config)
        {
            // Round 57's config pins a founding trickle pool of four genomes (D117), and the world refuses a
            // config whose pool it is not handed; the pool is r57-s2's own, loaded as the farm's resume loads it.
            TricklePoolFiles.Pool pool = TricklePoolFiles.Load(RunDirectory, config);
            var world = new World(config, Seed, pool?.Genomes, pool?.Weights);
            var snow = (GridField)world.Nutrients;
            float r = world.TankRadiusMetres;
            int k = 0;

            for (float x = 0.15f; x < 1f; x += 0.14f)
            {
                for (float z = 0.15f; z < 1f; z += 0.14f)
                {
                    foreach (float y in new[] { -1.5f, -5f, -12f })
                    {
                        var centre = new Float3(2f * r * x, y, 2f * r * z);
                        snow.DepositBox(100.0 * (1 + k % 7), centre, new Float3(3f, 1f, 3f));
                        k++;
                    }
                }
            }

            return world;
        }

        private static byte[] State(World world)
        {
            using var bytes = new MemoryStream();
            using (var w = new BinaryWriter(bytes)) world.WriteState(w);
            return bytes.ToArray();
        }

        [LocalDataFact("runs/r57-s2/2026-10-06-065827-dfc0e881/pool")]
        public void TheTransportOnTheAcceleratorCarriesRound57sWorld()
        {
            using Context context = Context.Create(b => b.CPU(new CPUDevice(4, 1, 1)).EnableAlgorithms());
            using Accelerator accelerator = context.GetCPUDevice(0).CreateCPUAccelerator(context, CPUAcceleratorMode.Parallel);
            using var device = new GpuTransport(accelerator);
            Check(device);
        }

        /// <summary>
        /// The same check on the card itself, where a product is the explicitly rounded one and a
        /// contracted multiply-add would part the two. It touches the card, so it runs only with
        /// EVOSIM_GPU_T1_CARD=1, on the coordinator's go, and returns at once otherwise.
        /// </summary>
        [LocalDataFact("runs/r57-s2/2026-10-06-065827-dfc0e881/pool")]
        [Trait("Category", "Card")]
        public void TheTransportOnTheCardCarriesRound57sWorld()
        {
            if (Environment.GetEnvironmentVariable("EVOSIM_GPU_T1_CARD") != "1")
            {
                _out.WriteLine("not run: T1 on the card needs EVOSIM_GPU_T1_CARD=1 and the coordinator's go");
                return;
            }

            using GpuTransport device = GpuTransport.OpenOnCard();
            _out.WriteLine("on " + device.DeviceName);
            Check(device);
        }

        private static int FirstDifference(byte[] a, byte[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) if (a[i] != b[i]) return i;
            return a.Length == b.Length ? -1 : n;
        }

        private void Check(GpuTransport device)
        {
            RunConfig config = Config();
            Assert.Equal(WorldShape.Tank, config.WorldShape);

            World cpu = Seeded(config);
            World dev = Seeded(config);
            Assert.Equal(State(cpu), State(dev));

            CurrentField current = dev.Config.Current;
            Assert.True(current.Bed != null, "round 57's world has a shaped bed, and this one has none");
            Assert.True(current.Reefs != null && current.Reefs.Count > 0, "round 57's world has reefs, and this one has none");
            Assert.True(config.BedShoreFadeMetres > 0f, "round 57's world fades the current at its shore, and this config does not");

            var cpuSnow = (GridField)cpu.Nutrients;
            var devSnow = (GridField)dev.Nutrients;
            Assert.True(devSnow.OfferTransportDevice(device, current), "the grid declined round 57's world to the device");

            // A late instant of the round's year, then steps short and long: the long ones split
            // into substeps, each sampled at its own clock, and any refusal must be the CPU's.
            double seconds = 47000.25;
            int steps = 0;
            byte[] before = State(cpu);

            foreach (float dt in new[] { 0.5f, 0.5f, 2f, 8f, 16f })
            {
                Exception cpuRefusal = null, devRefusal = null;
                try { cpuSnow.Advect(cpu.Config.Current, seconds, dt, cpuSnow.PatchWidthMetres); }
                catch (ArgumentException e) { cpuRefusal = e; }
                try { devSnow.Advect(dev.Config.Current, seconds, dt, devSnow.PatchWidthMetres); }
                catch (ArgumentException e) { devRefusal = e; }

                Assert.True(device.Accepted, "the device dropped round 57's tables");
                Assert.Equal(cpuRefusal?.Message, devRefusal?.Message);
                byte[] host = State(cpu), card = State(dev);
                int at = FirstDifference(host, card);
                Assert.True(at < 0, "the stocks part after the step at " + seconds + " s of " + dt + " s, first at byte " +
                                    at + " of " + host.Length + " in the world's state");

                if (cpuRefusal == null) steps++;
                seconds += dt;
            }

            Assert.True(steps >= 3, "too few steps were carried to check anything");
            Assert.False(before.AsSpan().SequenceEqual(State(cpu)), "the water moved no snow, so the check checked nothing");
            _out.WriteLine(steps + " steps carried, the last at " + seconds.ToString("R") + " s; snow " +
                           cpuSnow.Recount().ToString("R") + " J; tank radius " + cpu.TankRadiusMetres.ToString("R") +
                           " m; " + current.Reefs.Count + " reefs");
        }
    }

    /// <summary>
    /// A fact that restores r57-s2 at 48,000 s: its checkpoint and the trickle pool beside it. Skipped, naming the path, where either is not on the
    /// machine (<see cref="LocalDataFactAttribute"/>). The paths repeat <see cref="GpuTransportRound57Tests.Relative"/> as literals, so that a search for them finds this.
    /// </summary>
    internal sealed class Round57CheckpointFactAttribute : LocalDataFactAttribute
    {
        public Round57CheckpointFactAttribute() : base(
            "runs/r57-s2/2026-10-06-065827-dfc0e881/checkpoints/000048000.ckpt",
            "runs/r57-s2/2026-10-06-065827-dfc0e881/pool") { }
    }
}

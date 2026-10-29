using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Evosim.Core;
using Evosim.Farm.Gpu;
using Evosim.TestSupport;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// C3 (card-next-r57.md Part 7): the scent's fill on the card (<see cref="GpuScent"/>) gives the
    /// host's grid (<see cref="ScentField.Fill(IReadOnlyList{Corpse}, int)"/>) cell for cell, to the
    /// bit, on ILGPU's CPU accelerator, where a product is the plain one, as on the host.
    /// </summary>
    /// <remarks>
    /// The synthetic case plants what the bins and the order must survive: corpses outside the grid
    /// on every side, corpses with no joules or negative ones, terms that narrow to a subnormal float,
    /// a cell at exactly the range from its corpse, and a crowd in one cell whose float sum depends on
    /// the order of its terms. Round 57's case is r57-s2's own corpses at 48,000 s, restored from its
    /// checkpoint; the run directory is gitignored and belongs to the main working tree, as
    /// <see cref="GpuTransportRound57Tests"/>'s is.
    /// </remarks>
    public class GpuScentTests
    {
        private static string RunDirectory => GpuTransportRound57Tests.RunDirectory;

        private readonly ITestOutputHelper _out;

        public GpuScentTests(ITestOutputHelper output) => _out = output;

        private static Corpse NewCorpse(long id, Float3 at, double joules) =>
            (Corpse)Activator.CreateInstance(
                typeof(Corpse), BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { id, at, 0, joules }, null);

        private static (Context, Accelerator) Cpu()
        {
            Context context = Context.Create(b => b.CPU(new CPUDevice(4, 1, 1)).EnableAlgorithms());
            Accelerator accelerator = context.GetCPUDevice(0).CreateCPUAccelerator(context, CPUAcceleratorMode.Parallel);
            return (context, accelerator);
        }

        // The card's grid for the first `count` corpses, through the snapshot and the kernel.
        private static float[] OnTheAccelerator(Accelerator accelerator, ScentField shape, IReadOnlyList<Corpse> corpses, int count)
        {
            using var grid = new Col<float>(accelerator, accelerator.DefaultStream, 1, 1);
            using var scent = new GpuScent(accelerator, accelerator.DefaultStream, shape, grid);
            scent.Snapshot(corpses, count);
            Assert.True(scent.Join());
            Assert.False(scent.Join());
            grid.Down();
            return (float[])grid.Host.Clone();
        }

        private static void SameBits(float[] host, float[] card, ITestOutputHelper output, string what, int leastWithScent)
        {
            Assert.Equal(host.Length, card.Length);
            int differ = 0, first = -1, nonzero = 0;
            for (int c = 0; c < host.Length; c++)
            {
                if (host[c] != 0f) nonzero++;
                if (BitConverter.SingleToInt32Bits(host[c]) == BitConverter.SingleToInt32Bits(card[c])) continue;
                if (first < 0) first = c;
                differ++;
            }

            output.WriteLine(what + ": " + host.Length + " cells, " + nonzero + " with scent, " + differ + " differing");
            Assert.True(nonzero >= leastWithScent, what + ": only " + nonzero + " cells hold scent, so the check compared little");
            Assert.True(differ == 0,
                what + ": " + differ + " cells differ, the first at " + first + " (host " +
                (first >= 0 ? host[first].ToString("R") : "") + ", card " + (first >= 0 ? card[first].ToString("R") : "") + ")");
        }

        [Fact]
        public void TheCardsFillIsTheHostsOnPlantedCorpses()
        {
            // 60 m by 44 m by 30 m deep at a 10 m range: 30 × 15 × 22 cells, bins of 5 cells.
            var host = new ScentField(60f, 44f, 30f, 10f);
            var rng = new Random(57);
            var corpses = new List<Corpse>();
            long id = 0;

            // Scattered, inside the grid and up to 15 m outside it on every side.
            for (int n = 0; n < 1500; n++)
            {
                var at = new Float3(
                    (float)(rng.NextDouble() * 90 - 15),
                    (float)(5 - rng.NextDouble() * 50),
                    (float)(rng.NextDouble() * 74 - 15));
                double joules = n % 17 == 0 ? 0 : n % 23 == 0 ? -5 : rng.NextDouble() * 2000;
                corpses.Add(NewCorpse(id++, at, joules));
            }

            // Terms that narrow to a subnormal float, and a double subnormal.
            corpses.Add(NewCorpse(id++, new Float3(31f, -15f, 23f), 1e-39));
            corpses.Add(NewCorpse(id++, new Float3(31.3f, -15.2f, 22.9f), 3e-41));
            corpses.Add(NewCorpse(id++, new Float3(30.7f, -14.8f, 23.1f), 4.9e-324));

            // A cell at exactly the range: the cell centred at (11, -11, 11) and a corpse 10 m along x.
            corpses.Add(NewCorpse(id++, new Float3(21f, -11f, 11f), 777));

            // A crowd in one cell, large and small terms mixed, whose float sum the order decides.
            for (int n = 0; n < 300; n++)
            {
                var at = new Float3(40f + (float)(rng.NextDouble() * 2), -20f - (float)(rng.NextDouble() * 2), 30f + (float)(rng.NextDouble() * 2));
                corpses.Add(NewCorpse(id++, at, n % 2 == 0 ? 1e7 * rng.NextDouble() : 1e-3 * rng.NextDouble()));
            }

            // The whole list, then a prefix, as a restore fills it.
            var (context, accelerator) = Cpu();
            using (context)
            using (accelerator)
            {
                host.Fill(corpses, corpses.Count);
                SameBits(host.CopyCells(), OnTheAccelerator(accelerator, host, corpses, corpses.Count), _out, "planted, the whole list", 2000);

                int prefix = corpses.Count - 137;
                host.Fill(corpses, prefix);
                SameBits(host.CopyCells(), OnTheAccelerator(accelerator, host, corpses, prefix), _out, "planted, a prefix", 2000);
            }
        }

        [Round57CheckpointFact]
        public void TheCardsFillIsTheHostsOnRound57sCorpses()
        {
            var (context, accelerator) = Cpu();
            using (context)
            using (accelerator)
            {
                Round57(accelerator);
            }
        }

        /// <summary>
        /// The same check on the card itself (Part 7's second proof), where a product is the explicitly
        /// rounded one and a contracted multiply-add or a flushed subnormal would part the two. It
        /// touches the card, so it runs only with EVOSIM_GPU_C3_CARD=1, in a window the coordinator
        /// holds, and returns at once otherwise.
        /// </summary>
        [Round57CheckpointFact]
        [Trait("Category", "Card")]
        public void TheCardsFillIsTheHostsOnRound57sCorpsesOnTheCard()
        {
            if (Environment.GetEnvironmentVariable("EVOSIM_GPU_C3_CARD") != "1")
            {
                _out.WriteLine("not run: C3 on the card needs EVOSIM_GPU_C3_CARD=1 and the coordinator's window");
                return;
            }

            using Context context = Context.Create(b => b.Cuda().EnableAlgorithms());
            Assert.True(context.GetCudaDevices().Count > 0, "EVOSIM_GPU_C3_CARD is set and ILGPU finds no CUDA card");
            using Accelerator accelerator = context.GetCudaDevice(0).CreateAccelerator(context);
            _out.WriteLine("on " + accelerator.Name);
            Round57(accelerator);
        }

        /// <summary>
        /// The early fill's restore path (Part 7, the coordinator's addition of 2026-10-09): a
        /// restore's snapshot and the step's own straight after it, before any block. The second must
        /// wait for the first's uploads before it writes the arrays they read, and the grid the next
        /// block reads must be the second's. On the CPU accelerator a stream finishes its work at every
        /// call, so there is nothing to wait for and only the grid is checked.
        /// </summary>
        [Round57CheckpointFact]
        public void ARestoresSnapshotThenTheStepsOwnFillsTheSecond()
        {
            var (context, accelerator) = Cpu();
            using (context)
            using (accelerator)
            {
                RestoreThenStep(accelerator, false);
            }
        }

        /// <summary>The same on the card, where the second snapshot must be seen to wait.</summary>
        [Round57CheckpointFact]
        [Trait("Category", "Card")]
        public void ARestoresSnapshotThenTheStepsOwnFillsTheSecondOnTheCard()
        {
            if (Environment.GetEnvironmentVariable("EVOSIM_GPU_C3_CARD") != "1")
            {
                _out.WriteLine("not run: the restore's path on the card needs EVOSIM_GPU_C3_CARD=1 and the coordinator's window");
                return;
            }

            using Context context = Context.Create(b => b.Cuda().EnableAlgorithms());
            Assert.True(context.GetCudaDevices().Count > 0, "EVOSIM_GPU_C3_CARD is set and ILGPU finds no CUDA card");
            using Accelerator accelerator = context.GetCudaDevice(0).CreateAccelerator(context);
            _out.WriteLine("on " + accelerator.Name);
            RestoreThenStep(accelerator, true);
        }

        private void RestoreThenStep(Accelerator accelerator, bool mustWait)
        {
            World world = Round57World();
            int second = world.ScentFilledCorpses;
            int first = second - 5000;
            Assert.True(first > 10000, "r57-s2 at 48,000 s filled from " + second + " corpses; the test wants tens of thousands");

            world.Scent.Fill(world.Corpses, first);
            float[] hostFirst = world.Scent.CopyCells();
            world.Scent.Fill(world.Corpses, second);
            float[] hostSecond = world.Scent.CopyCells();

            using var grid = new Col<float>(accelerator, accelerator.DefaultStream, 1, 1);
            using var scent = new GpuScent(accelerator, accelerator.DefaultStream, world.Scent, grid);
            scent.Snapshot(world.Corpses, first);
            scent.Snapshot(world.Corpses, second);
            double waited = scent.LastSnapshotWaitMs;
            _out.WriteLine("the second snapshot waited " + waited.ToString("0.000") + " ms for the first's fill");

            // The next block: it joins the fill's stream, then reads the grid on the engine's.
            Assert.True(scent.Join());
            grid.Down();
            float[] card = (float[])grid.Host.Clone();
            SameBits(hostSecond, card, _out, "the grid after a restore's snapshot and the step's own", 10000);

            int fromFirst = 0;
            for (int c = 0; c < card.Length; c++)
            {
                if (BitConverter.SingleToInt32Bits(card[c]) != BitConverter.SingleToInt32Bits(hostFirst[c])) fromFirst++;
            }

            _out.WriteLine("cells where the second snapshot's grid differs from the first's: " + fromFirst);
            Assert.True(fromFirst > 0, "the two snapshots fill the same grid, so the check cannot tell which the block read");

            if (mustWait)
            {
                Assert.True(waited > 0.5,
                    "the second snapshot waited " + waited + " ms; on the card the first's fill of tens of thousands of corpses takes milliseconds, so it did not wait for it");
            }
        }

        private static World Round57World()
        {
            string path = Path.Combine(RunDirectory, "checkpoints", "000048000.ckpt");
            Assert.True(File.Exists(path),
                "r57-s2's checkpoint at 48,000 s is not on this machine (run directories are gitignored and belong to the main working tree): " + path);

            RunConfig config = GpuTransportRound57Tests.Round57Config();
            TricklePoolFiles.Pool pool = TricklePoolFiles.Load(RunDirectory, config);
            var world = new World(config, 2UL, pool?.Genomes, pool?.Weights);
            using (CheckpointReader reader = CheckpointReader.Open(path))
            {
                StateIo.Tag(reader.Reader, "PAYL");
                world.ReadState(reader.Reader);
            }

            Assert.NotNull(world.Scent);
            return world;
        }

        private void Round57(Accelerator accelerator)
        {
            World world = Round57World();
            float[] host = world.Scent.CopyCells();
            _out.WriteLine("r57-s2 at " + world.ElapsedSeconds + " s: " + world.Corpses.Count + " corpses, the fill's prefix " +
                           world.ScentFilledCorpses);

            // Through the world's own hand-over, as the farm wires it: the snapshot at the hand-over,
            // the kernel, and the grid brought home into the world's field.
            using var grid = new Col<float>(accelerator, accelerator.DefaultStream, 1, 1);
            using var scent = new GpuScent(accelerator, accelerator.DefaultStream, world.Scent, grid);
            world.FillScentElsewhere(scent.Snapshot, scent.BringHome);
            Assert.True(world.ScentFilledElsewhere);
            _out.WriteLine("corpses with joules in the snapshot: " + scent.Count);
            Assert.True(scent.Count > 10000, "r57-s2 at 48,000 s held " + scent.Count + " corpses with joules; round 57 held tens of thousands");

            world.Scent.Clear();
            world.BringScentHome();
            SameBits(host, world.Scent.CopyCells(), _out, "round 57 at 48,000 s", 10000);
        }

        [Round57CheckpointFact]
        public void AWorldWithTheFillHandedOverDoesNotFillItsOwnField()
        {
            // The hand-over takes the step's fill: the world calls the engine and leaves its field as
            // the engine last brought it home.
            string path = Path.Combine(RunDirectory, "checkpoints", "000048000.ckpt");
            Assert.True(File.Exists(path), "r57-s2's checkpoint at 48,000 s is not on this machine: " + path);

            RunConfig config = GpuTransportRound57Tests.Round57Config();
            TricklePoolFiles.Pool pool = TricklePoolFiles.Load(RunDirectory, config);
            var world = new World(config, 2UL, pool?.Genomes, pool?.Weights);
            using (CheckpointReader reader = CheckpointReader.Open(path))
            {
                StateIo.Tag(reader.Reader, "PAYL");
                world.ReadState(reader.Reader);
            }

            int calls = 0, lastCount = -1, broughtHome = 0;
            world.FillScentElsewhere((corpses, count) => { calls++; lastCount = count; }, field => broughtHome++);
            Assert.Equal(1, calls);
            Assert.Equal(world.ScentFilledCorpses, lastCount);

            world.Scent.Clear();
            world.Step(0.5f);
            Assert.Equal(2, calls);
            Assert.Equal(world.ScentFilledCorpses, lastCount);
            Assert.Equal(0, broughtHome);
            foreach (float v in world.Scent.CopyCells()) Assert.Equal(0f, v);

            world.BringScentHome();
            Assert.Equal(1, broughtHome);
        }
    }
}

using System;
using System.Linq;
using Evosim.Farm.Gpu;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Farm.Tests
{
    /// <summary>
    /// A column's copies stopped at the used slots (W1's second change, card-next-r57.md Part 8). On
    /// the card a one-row column moves its used run with a flat copy and a wider one with one pitched
    /// copy (Pitched): every slot under the used count must arrive, and every slot past it must stay
    /// as it was on the receiving side. The first upload after an allocation is whole. The CPU
    /// accelerator holds no page lock, so there every copy stays whole. A size class's arena (Seg) is
    /// a wide column whose rows are its columns', so its copies are a wide column's. The packed slots'
    /// upload (1b) gathers, copies and scatters on either accelerator, since it needs no page lock: only
    /// the named slots arrive, and a column not yet copied whole goes up whole.
    /// </summary>
    public class GpuUsedCopyTests
    {
        private readonly ITestOutputHelper _out;

        public GpuUsedCopyTests(ITestOutputHelper output) => _out = output;

        private static void Shapes(Accelerator acc)
        {
            // A one-row column, a narrow one, and class 0's ring at round 57's crowd.
            Check(acc, 1, 37, 11, i => (float)(0.5 * i + 0.25));
            Check(acc, 5, 37, 11, i => (float)(0.5 * i + 0.25));
            Check(acc, 5, 37, 11, i => 0.125 * i - 3.0);
            Check(acc, 3, 37, 11, i => 7 * i - 5);
            Check(acc, 3, 37, 11, i => 9L * i + 1);
            Check(acc, 126, 12774, 6928, i => (float)(0.001 * i));
            // None used, and every slot used.
            Check(acc, 5, 37, 0, i => (float)i);
            Check(acc, 5, 37, 37, i => (float)i);
            // An arena of three columns with fills of their own.
            Check(acc, 0, 37, 11, i => 7 * i - 5, rowFill: new[] { -1, -1, 0, 7, 7, 7 });
        }

        // A packed class's columns go up by the packed slots alone (1b): gathered, copied, scattered.
        private static void SlotShapes(Accelerator acc)
        {
            Slots(acc, 1, 37, 30, new[] { 3, 17, 29 }, i => 7 * i - 5);
            Slots(acc, 6, 37, 30, new[] { 0, 5, 28 }, i => (float)(0.5 * i + 0.25));
            Slots(acc, 3, 37, 30, new[] { 2, 1 }, i => 0.125 * i - 3.0);
            Slots(acc, 2, 37, 30, new[] { 4, 9, 11 }, i => 9L * i + 1);
            Slots(acc, 126, 12774, 6928, Enumerable.Range(0, 40).Select(k => 6927 - 97 * k).ToArray(), i => (float)(0.001 * i));
        }

        private static void Slots<T>(Accelerator acc, int width, int cap, int used, int[] packed, Func<int, T> value)
            where T : unmanaged, IEquatable<T>
        {
            using var col = new Col<T>(acc, acc.DefaultStream, width, cap);
            using var slots = new Col<int>(acc, acc.DefaultStream, 1, packed.Length);
            int n = col.Host.Length;
            Array.Copy(packed, slots.Host, packed.Length);
            slots.Up();

            // A column never copied whole goes up whole, whatever the slots named.
            for (int i = 0; i < n; i++) col.Host[i] = value(i);
            col.UpSlotsQueued(slots, packed.Length, used);
            acc.DefaultStream.Synchronize();
            col.Down();
            for (int i = 0; i < n; i++) Assert.Equal(value(i), col.Host[i]);

            // Every slot changed on the host: only the named ones arrive, every row of each.
            for (int i = 0; i < n; i++) col.Host[i] = value(i + 1_000_000);
            col.UpSlotsQueued(slots, packed.Length, used);
            acc.DefaultStream.Synchronize();
            col.Down();
            for (int i = 0; i < n; i++)
            {
                T want = packed.Contains(i % cap) ? value(i + 1_000_000) : value(i);
                if (!col.Host[i].Equals(want))
                {
                    Assert.Fail($"slots, width {width}, cap {cap}: element {i} (row {i / cap}, slot {i % cap}) is {col.Host[i]}, not {want}");
                }
            }

            // A resize re-lays the column, so its next upload is whole again.
            col.Resize(cap + 3);
            for (int i = 0; i < col.Host.Length; i++) col.Host[i] = value(i + 2_000_000);
            col.UpSlotsQueued(slots, packed.Length, used);
            acc.DefaultStream.Synchronize();
            var after = (T[])col.Host.Clone();
            col.Down();
            Assert.Equal(after, col.Host);
        }

        private static void Check<T>(Accelerator acc, int width, int cap, int used, Func<int, T> value, T[] rowFill = null)
            where T : unmanaged, IEquatable<T>
        {
            using var col = rowFill == null
                ? new Col<T>(acc, acc.DefaultStream, width, cap)
                : new Col<T>(acc, acc.DefaultStream, cap, rowFill);
            width = col.Width;
            int n = col.Host.Length;
            bool cut = col.Locked;

            // The first upload is whole, whatever the used count.
            for (int i = 0; i < n; i++) col.Host[i] = value(i);
            col.UpUsedQueued(used);
            acc.DefaultStream.Synchronize();

            // The second goes up over the used slots only.
            for (int i = 0; i < n; i++) col.Host[i] = value(i + 1_000_000);
            col.UpUsedQueued(used);
            acc.DefaultStream.Synchronize();

            var onCard = new T[n];
            for (int i = 0; i < n; i++)
            {
                bool inUse = i % cap < used;
                onCard[i] = !cut || inUse ? value(i + 1_000_000) : value(i);
            }

            col.Down();
            for (int i = 0; i < n; i++)
            {
                if (!col.Host[i].Equals(onCard[i]))
                {
                    Assert.Fail($"up, width {width}, cap {cap}, used {used}: element {i} (row {i / cap}, slot {i % cap}) is {col.Host[i]}, not {onCard[i]}");
                }
            }

            // The download brings the used slots and leaves the host's own past them.
            for (int i = 0; i < n; i++) col.Host[i] = value(i + 2_000_000);
            col.DownUsedQueued(used);
            acc.DefaultStream.Synchronize();
            for (int i = 0; i < n; i++)
            {
                bool inUse = i % cap < used;
                T want = !cut || inUse ? onCard[i] : value(i + 2_000_000);
                if (!col.Host[i].Equals(want))
                {
                    Assert.Fail($"down, width {width}, cap {cap}, used {used}: element {i} (row {i / cap}, slot {i % cap}) is {col.Host[i]}, not {want}");
                }
            }

            // A resize makes the next upload whole again.
            col.Resize(cap + 3);
            for (int i = 0; i < col.Host.Length; i++) col.Host[i] = value(i + 3_000_000);
            col.UpUsedQueued(used);
            acc.DefaultStream.Synchronize();
            var after = (T[])col.Host.Clone();
            col.Down();
            Assert.Equal(after, col.Host);
        }

        /// <summary>
        /// An arena's rows start at their column's fill and keep it through a resize, and each segment
        /// is its own rows on the host and on the device, at the arena's capacity before and after.
        /// </summary>
        [Fact]
        public void AnArenaHoldsEachColumnInItsOwnRows()
        {
            using Context context = Context.Create(b => b.CPU(new CPUDevice(4, 1, 1)));
            using Accelerator acc = context.GetCPUDevice(0).CreateCPUAccelerator(context, CPUAcceleratorMode.Parallel);

            // Three columns of widths 2, 1 and 3, filled -1, 0 and 7.
            var fills = new[] { -1, -1, 0, 7, 7, 7 };
            using var arena = new Col<int>(acc, acc.DefaultStream, 5, fills);
            var segs = new[] { new Seg<int>(0, 2) { Arena = arena }, new Seg<int>(2, 1) { Arena = arena }, new Seg<int>(3, 3) { Arena = arena } };
            Assert.Equal(6, arena.Width);
            for (int i = 0; i < arena.Host.Length; i++) Assert.Equal(fills[i / 5], arena.Host[i]);

            for (int k = 0; k < segs.Length; k++)
            {
                Span<int> host = segs[k].Host;
                Assert.Equal(segs[k].Width * 5, host.Length);
                for (int i = 0; i < host.Length; i++) host[i] = 1000 * (k + 1) + i;
            }

            arena.Up();
            Assert.Equal(new[] { 1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009 }, arena.Host.AsSpan(0, 10).ToArray());
            for (int k = 0; k < segs.Length; k++)
            {
                var back = new int[segs[k].Width * 5];
                segs[k].View.CopyToCPU(acc.DefaultStream, ref back[0], back.Length);
                Assert.Equal(segs[k].Host.ToArray(), back);
            }

            // A resize re-lays every row: the kept slots keep their values, the new ones their row's fill.
            arena.Resize(8);
            Assert.Equal(8, segs[2].Cap);
            for (int k = 0; k < segs.Length; k++)
            {
                Span<int> host = segs[k].Host;
                for (int r = 0; r < segs[k].Width; r++)
                {
                    for (int slot = 0; slot < 8; slot++)
                    {
                        int want = slot < 5 ? 1000 * (k + 1) + r * 5 + slot : fills[segs[k].Row + r];
                        Assert.Equal(want, host[r * 8 + slot]);
                    }
                }
            }

            arena.Up();
            var whole = new int[3 * 8];
            segs[2].View.CopyToCPU(acc.DefaultStream, ref whole[0], whole.Length);
            Assert.Equal(segs[2].Host.ToArray(), whole);

            Assert.Throws<InvalidOperationException>(() => arena.Widen(7));
        }

        [Fact]
        public void OnTheCpuAcceleratorEveryCopyIsWhole()
        {
            using Context context = Context.Create(b => b.CPU(new CPUDevice(4, 1, 1)));
            using Accelerator acc = context.GetCPUDevice(0).CreateCPUAccelerator(context, CPUAcceleratorMode.Parallel);
            Shapes(acc);
            SlotShapes(acc);
        }

        /// <summary>The same shapes on the card, through the flat and the pitched copies. It runs only with EVOSIM_GPU_USED_CARD=1.</summary>
        [Fact]
        [Trait("Category", "Card")]
        public void OnTheCardTheUsedSlotsAloneMove()
        {
            if (Environment.GetEnvironmentVariable("EVOSIM_GPU_USED_CARD") != "1")
            {
                _out.WriteLine("not run: the used-slot copies on the card need EVOSIM_GPU_USED_CARD=1 and the coordinator's go");
                return;
            }

            using Context context = Context.Create(b => b.Cuda());
            Assert.True(context.GetCudaDevices().Count > 0, "EVOSIM_GPU_USED_CARD is set and ILGPU finds no CUDA card");
            using Accelerator acc = context.GetCudaDevice(0).CreateAccelerator(context);
            _out.WriteLine("on " + acc.Name);
            Shapes(acc);
            SlotShapes(acc);
            Assert.False(Pitched.Refused, "the driver refused a pitched copy, so the wide columns were copied whole");
        }
    }
}

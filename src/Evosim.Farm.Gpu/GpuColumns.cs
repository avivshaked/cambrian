using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace Evosim.Farm.Gpu
{
    /// <summary>One array the card holds and its copy on the host, moved as a whole.</summary>
    internal interface IColumn : IDisposable
    {
        void Resize(int cap);

        void Up();

        void Down();

        /// <summary>
        /// <see cref="Up"/> queued on the stream without waiting for it, where the host array is
        /// page-locked; the waiting copy otherwise. The caller joins the stream before the host
        /// writes the array again or another stream reads the column (card-next-r57.md Part 8).
        /// </summary>
        void UpQueued();

        /// <summary>
        /// <see cref="Down"/> queued likewise. The caller joins the stream before the host reads the
        /// array.
        /// </summary>
        void DownQueued();

        /// <summary>
        /// <see cref="UpQueued"/> over the first <paramref name="used"/> slots of every row only
        /// (W1's second change, card-next-r57.md Part 8): the card reads no slot past them. A column's
        /// first upload after it is allocated is whole, so the slots past them hold on the card what
        /// a whole copy would have left there.
        /// </summary>
        void UpUsedQueued(int used);

        /// <summary><see cref="DownQueued"/> over the first <paramref name="used"/> slots of every row only.</summary>
        void DownUsedQueued(int used);

        /// <summary>
        /// The slots named in the first <paramref name="n"/> entries of <paramref name="slots"/> up,
        /// every row of each (1b, card-next-r57.md Part 9): gathered on the host into a staging column,
        /// copied up at once and scattered into place on the card. Sound only where the card already
        /// holds what the host does in every other used slot, and <paramref name="slots"/> must be
        /// queued up on the same stream before this. A column not yet copied whole since it was
        /// allocated, or with half its used slots named, goes up over its used slots instead
        /// (<see cref="UpUsedQueued"/>).
        /// </summary>
        void UpSlotsQueued(Col<int> slots, int n, int used);

        long Bytes { get; }

        /// <summary>The slots a row holds, which each of the column's rows is strided by.</summary>
        int Cap { get; }

        /// <summary>The bytes of the first <paramref name="used"/> slots of every row: what a copy of the used part would move.</summary>
        long BytesFor(int used);

        /// <summary>Whether the host copy is registered with the driver as page-locked.</summary>
        bool Locked { get; }
    }

    /// <summary>
    /// The copies a thread's columns have made and the bytes they moved, for the time probe (W0,
    /// card-next-r57.md Part 8). An engine's block runs its copies on the one thread that called
    /// it, and two worlds in one process run on two threads, so a count a thread is a count an
    /// engine. A waiting copy (ILGPU 1.5.3's CopyFromCPU and CopyToCPU, which wait for their
    /// stream) is counted in <see cref="Waits"/> as well; a queued one is not.
    /// </summary>
    internal static class CopyTally
    {
        [ThreadStatic] public static long Copies;
        [ThreadStatic] public static long Bytes;
        [ThreadStatic] public static long Waits;

        public static void Note(long bytes, bool waits = true)
        {
            Copies++;
            Bytes += bytes;
            if (waits) Waits++;
        }
    }

    /// <summary>
    /// The used part of a wide column in one queued copy: <c>Width</c> rows of <c>used</c>
    /// elements each, <c>Cap</c> apart on both sides, through the driver's
    /// <c>cuMemcpy2DAsync_v2</c>, since ILGPU 1.5.3 offers only flat copies on a CUDA card. The
    /// copy goes on the column's own stream, so it keeps that stream's order like ILGPU's queued
    /// copies. The host array is a page-locked one (Col's HostArray) on the pinned heap, so its
    /// address holds while the copy is in flight.
    /// </summary>
    /// <remarks>
    /// The driver may refuse a pitch it did not choose itself (cuMemcpy2D's documentation). A
    /// refusal is said once on stderr and the caller makes the whole copy instead, so it costs pace
    /// and never a number.
    /// </remarks>
    internal static class Pitched
    {
        private const int HostMemory = 1, DeviceMemory = 2;   // CUmemorytype (cuda.h)

        // CUDA_MEMCPY2D (cuda.h), the _v2 layout: size_t and pointers are 8 bytes, the memory type
        // a 4-byte enum padded to the pointer after it, as LayoutKind.Sequential pads it.
        [StructLayout(LayoutKind.Sequential)]
        private struct Memcpy2D
        {
            public UIntPtr SrcXInBytes, SrcY;
            public int SrcMemoryType;
            public IntPtr SrcHost;
            public ulong SrcDevice;
            public IntPtr SrcArray;
            public UIntPtr SrcPitch;
            public UIntPtr DstXInBytes, DstY;
            public int DstMemoryType;
            public IntPtr DstHost;
            public ulong DstDevice;
            public IntPtr DstArray;
            public UIntPtr DstPitch;
            public UIntPtr WidthInBytes, Height;
        }

        [DllImport("nvcuda", EntryPoint = "cuMemcpy2DAsync_v2")]
        private static extern int CuMemcpy2DAsync(ref Memcpy2D copy, IntPtr stream);

        private static int _refused;

        /// <summary>Whether the driver has refused a pitched copy in this process (and every one since was whole).</summary>
        public static bool Refused => _refused != 0;

        /// <summary>Queues the copy; false if the driver refused it, and nothing was queued.</summary>
        public static bool Copy<T>(AcceleratorStream stream, IntPtr device, T[] host, bool toDevice,
            int width, int cap, int used) where T : unmanaged
        {
            if (_refused != 0 || !(stream is CudaStream cuda)) return false;

            ulong size = (ulong)System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var pitch = (UIntPtr)(size * (ulong)cap);
            IntPtr hostPtr = Marshal.UnsafeAddrOfPinnedArrayElement(host, 0);
            var copy = new Memcpy2D
            {
                SrcMemoryType = toDevice ? HostMemory : DeviceMemory,
                SrcHost = toDevice ? hostPtr : IntPtr.Zero,
                SrcDevice = toDevice ? 0UL : (ulong)device.ToInt64(),
                SrcPitch = pitch,
                DstMemoryType = toDevice ? DeviceMemory : HostMemory,
                DstHost = toDevice ? IntPtr.Zero : hostPtr,
                DstDevice = toDevice ? (ulong)device.ToInt64() : 0UL,
                DstPitch = pitch,
                WidthInBytes = (UIntPtr)(size * (ulong)used),
                Height = (UIntPtr)(ulong)width,
            };

            int result = CuMemcpy2DAsync(ref copy, cuda.StreamPtr);
            if (result == 0) return true;

            if (System.Threading.Interlocked.Exchange(ref _refused, 1) == 0)
            {
                Console.Error.WriteLine("gpu: cuMemcpy2DAsync refused a pitched copy (CUresult " + result +
                    ", pitch " + pitch + " bytes, " + used + " of " + cap + " slots, " + width +
                    " rows); such copies are made whole from here on. Said once.");
            }

            return false;
        }
    }

    /// <summary>
    /// A column of <typeparamref name="T"/> laid out <c>[k·Cap + slot]</c> for <c>k</c> below
    /// <see cref="Width"/>: the kernel's structure-of-arrays stride, which keeps a warp's reads of
    /// one quantity for neighbouring bodies adjacent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host copy is the mirror's staging area and is kept equal to the card's after every
    /// download, so an upload of the whole column carries every slot the host did not touch
    /// back as it was. The one exception is a column moved by its first rows only
    /// (<see cref="UpRows"/>, <see cref="DownRows"/>): the overlap lists, whose rows past every
    /// body's count are read nowhere, so the two copies may differ there. Growing the capacity
    /// re-lays every row; growing the width appends rows, which leaves every existing index
    /// where it was.
    /// </para>
    /// <para>
    /// On a CUDA card the host copy lives on the pinned heap and is registered with the driver
    /// as page-locked for as long as it lives, so every copy of it is a direct transfer: 1.9 ms
    /// for 42 MB up and back against 6.1 to 6.4 ms from a pageable array on this machine's card
    /// (logbook/specs/transport-probe). The plain copy calls wait for their stream; the driver sees
    /// that the range is registered. A block's own uploads and downloads are queued without the
    /// wait (<see cref="UpQueued"/>, <see cref="DownQueued"/>), and the runner joins the stream once
    /// after each set. The bytes moved are the same bytes, so nothing a run computes moves.
    /// </para>
    /// </remarks>
    internal sealed class Col<T> : IColumn where T : unmanaged
    {
        private readonly Accelerator _acc;
        private readonly AcceleratorStream _stream;
        private readonly T _fill;

        // An arena's fill, one a row (ClassSet's arenas, Seg); null for a column of one fill.
        private readonly T[] _rowFill;
        private PageLockScope<T> _lock;

        // The device buffer has not had a whole upload since it was allocated: its slots past the
        // used ones are not yet what the host holds.
        private bool _whole = true;

        // The named slots' rows on their way up (UpSlotsQueued): one row a row of this column, the
        // slots one after another. Made at the first such upload, and again when the column widens.
        private Col<T> _staging;

        public int Width { get; private set; }

        public int Cap { get; private set; }

        public T[] Host;

        public MemoryBuffer1D<T, Stride1D.Dense> Dev;

        /// <param name="stream">The stream every copy of the column goes on: its engine's (Runner's _stream).</param>
        public Col(Accelerator acc, AcceleratorStream stream, int width, int cap, T fill = default)
        {
            _acc = acc;
            _stream = stream ?? acc.DefaultStream;
            _fill = fill;
            Width = Math.Max(1, width);
            Cap = Math.Max(1, cap);
            Host = HostArray(Width * Cap);
            Fill(Host, Cap);
            Dev = _acc.Allocate1D<T>(Host.Length);
        }

        /// <summary>
        /// An arena: one row a fill in <paramref name="rowFill"/>, each row <paramref name="cap"/>
        /// slots, the columns of one size class that share an element type and a copy kind laid end
        /// to end (<see cref="Seg{T}"/>), so that one copy moves them all.
        /// </summary>
        public Col(Accelerator acc, AcceleratorStream stream, int cap, T[] rowFill)
        {
            _acc = acc;
            _stream = stream ?? acc.DefaultStream;
            _rowFill = rowFill;
            Width = Math.Max(1, rowFill.Length);
            Cap = Math.Max(1, cap);
            Host = HostArray(Width * Cap);
            Fill(Host, Cap);
            Dev = _acc.Allocate1D<T>(Host.Length);
        }

        // Every row of a new array at its fill, rows cap apart.
        private void Fill(T[] array, int cap)
        {
            if (_rowFill == null)
            {
                if (!EqualityComparer<T>.Default.Equals(_fill, default)) Array.Fill(array, _fill);
                return;
            }

            for (int k = 0; k < _rowFill.Length; k++)
            {
                if (!EqualityComparer<T>.Default.Equals(_rowFill[k], default)) Array.Fill(array, _rowFill[k], k * cap, cap);
            }
        }

        public ArrayView<T> View => Dev.View;

        public long Bytes => (long)Host.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

        public long BytesFor(int used) =>
            (long)Width * Math.Clamp(used, 0, Cap) * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

        public bool Locked => _lock != null;

        public void Resize(int cap)
        {
            cap = Math.Max(1, cap);
            if (cap == Cap) return;

            var next = HostArray(Width * cap);
            Fill(next, cap);

            int keep = Math.Min(Cap, cap);
            for (int k = 0; k < Width; k++) Array.Copy(Host, k * Cap, next, k * cap, keep);

            Host = next;
            Cap = cap;
            Dev.Dispose();
            Dev = _acc.Allocate1D<T>(Host.Length);
            _whole = true;
        }

        /// <summary>More rows at the end; every existing index keeps its place.</summary>
        public void Widen(int width)
        {
            if (width <= Width) return;
            if (_rowFill != null) throw new InvalidOperationException("An arena's rows are its columns'; it does not widen.");

            var next = HostArray(width * Cap);
            if (!EqualityComparer<T>.Default.Equals(_fill, default)) Array.Fill(next, _fill);
            Array.Copy(Host, next, Host.Length);

            Host = next;
            Width = width;
            Dev.Dispose();
            Dev = _acc.Allocate1D<T>(Host.Length);
            _whole = true;
        }

        public void Up()
        {
            Dev.View.CopyFromCPU(_stream, Host);
            CopyTally.Note(Bytes);
            _whole = false;
        }

        public void Down()
        {
            Dev.View.CopyToCPU(_stream, Host);
            CopyTally.Note(Bytes);
        }

        // The queued copies read and write the host array by its address while the copy is in
        // flight. That is sound only for an array that cannot move and is registered with the
        // driver: the page-locked one (HostArray), which lives on the pinned heap. Any other array
        // takes the waiting copy.
        public void UpQueued()
        {
            if (_lock == null) { Up(); return; }
            View.CopyFromCPUUnsafeAsync(_stream, ref Host[0], Host.Length);
            CopyTally.Note(Bytes, waits: false);
            _whole = false;
        }

        public void DownQueued()
        {
            if (_lock == null) { Down(); return; }
            View.CopyToCPUUnsafeAsync(_stream, ref Host[0], Host.Length);
            CopyTally.Note(Bytes, waits: false);
        }

        /// <summary><see cref="UpRows"/> queued, as <see cref="UpQueued"/>.</summary>
        public void UpRowsQueued(int rows)
        {
            if (_lock == null) { UpRows(rows); return; }
            long n = Math.Min(Host.Length, (long)rows * Cap);
            if (n <= 0) return;
            View.SubView(0, n).CopyFromCPUUnsafeAsync(_stream, ref Host[0], n);
            CopyTally.Note(n * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), waits: false);
        }

        /// <summary><see cref="DownRows"/> queued, as <see cref="DownQueued"/>.</summary>
        public void DownRowsQueued(int rows)
        {
            if (_lock == null) { DownRows(rows); return; }
            long n = Math.Min(Host.Length, (long)rows * Cap);
            if (n <= 0) return;
            View.SubView(0, n).CopyToCPUUnsafeAsync(_stream, ref Host[0], n);
            CopyTally.Note(n * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), waits: false);
        }

        // A one-row column's used part is one run, a plain copy; a wider one's is a run a row, one
        // pitched copy (Pitched). Off a CUDA card, or with every slot used, the copy is whole.
        public void UpUsedQueued(int used)
        {
            if (_lock == null || _whole || used >= Cap) { UpQueued(); return; }
            if (used <= 0) return;
            if (Width == 1)
            {
                View.SubView(0, used).CopyFromCPUUnsafeAsync(_stream, ref Host[0], used);
            }
            else if (!Pitched.Copy(_stream, Dev.NativePtr, Host, toDevice: true, Width, Cap, used))
            {
                UpQueued();
                return;
            }

            CopyTally.Note(BytesFor(used), waits: false);
        }

        public void DownUsedQueued(int used)
        {
            if (_lock == null || used >= Cap) { DownQueued(); return; }
            if (used <= 0) return;
            if (Width == 1)
            {
                View.SubView(0, used).CopyToCPUUnsafeAsync(_stream, ref Host[0], used);
            }
            else if (!Pitched.Copy(_stream, Dev.NativePtr, Host, toDevice: false, Width, Cap, used))
            {
                DownQueued();
                return;
            }

            CopyTally.Note(BytesFor(used), waits: false);
        }

        public void UpSlotsQueued(Col<int> slots, int n, int used)
        {
            if (n <= 0) return;
            if (_whole || 2 * n >= used) { UpUsedQueued(used); return; }

            if (_staging == null || _staging.Width != Width)
            {
                _staging?.Dispose();
                _staging = new Col<T>(_acc, _stream, Width, Math.Max(16, n));
            }
            else if (_staging.Cap < n)
            {
                _staging.Resize(Math.Max(n, 2 * _staging.Cap));
            }

            int pitch = _staging.Cap;
            T[] host = Host, stage = _staging.Host;
            int[] slot = slots.Host;
            for (int r = 0; r < Width; r++)
            {
                int from = r * Cap, to = r * pitch;
                for (int j = 0; j < n; j++) stage[to + j] = host[from + slot[j]];
            }

            _staging.UpUsedQueued(n);
            Scatter.Run(_acc, _stream, View, _staging.View, slots.View, n, Width, pitch, Cap);
        }

        /// <summary>The first <paramref name="rows"/> rows up, <c>rows · Cap</c> elements.</summary>
        public void UpRows(int rows)
        {
            long n = Math.Min(Host.Length, (long)rows * Cap);
            if (n <= 0) return;
            Dev.View.SubView(0, n).CopyFromCPU(_stream, ref Host[0], n);
            CopyTally.Note(n * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        }

        /// <summary>The first <paramref name="rows"/> rows down, <c>rows · Cap</c> elements.</summary>
        public void DownRows(int rows)
        {
            long n = Math.Min(Host.Length, (long)rows * Cap);
            if (n <= 0) return;
            Dev.View.SubView(0, n).CopyToCPU(_stream, ref Host[0], n);
            CopyTally.Note(n * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        }

        public void Dispose()
        {
            _staging?.Dispose();
            _staging = null;
            Dev?.Dispose();
            _lock?.Dispose();
            _lock = null;
        }

        // A new host array, page-locked on a CUDA card (the remarks). The old one's lock goes
        // first; the old array stays a valid managed array to copy from.
        private T[] HostArray(int length)
        {
            _lock?.Dispose();
            _lock = null;
            if (_acc.AcceleratorType != AcceleratorType.Cuda) return new T[length];

            var host = GC.AllocateArray<T>(length, pinned: true);
            _lock = _acc.CreatePageLockFromPinned(host);
            return host;
        }
    }

    /// <summary>
    /// The card's half of <see cref="Col{T}.UpSlotsQueued"/>: element <c>r·pitch + j</c> of the staging
    /// column to element <c>r·cap + slots[j]</c> of the column, one thread an element, on the column's
    /// stream. A copy, so nothing a run computes moves. One kernel a type and accelerator, loaded at
    /// its first use.
    /// </summary>
    internal static class Scatter
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Accelerator, Dictionary<Type, Delegate>> Kernels =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Accelerator, Dictionary<Type, Delegate>>();

        private static void Kernel<T>(Index1D index, ArrayView<T> column, ArrayView<T> staging, ArrayView<int> slots,
            int n, int pitch, int cap) where T : unmanaged
        {
            int i = index;
            int r = i / n;
            int j = i - r * n;
            column[(long)r * cap + slots[j]] = staging[(long)r * pitch + j];
        }

        public static void Run<T>(Accelerator acc, AcceleratorStream stream, ArrayView<T> column, ArrayView<T> staging,
            ArrayView<int> slots, int n, int rows, int pitch, int cap) where T : unmanaged
        {
            Action<AcceleratorStream, Index1D, ArrayView<T>, ArrayView<T>, ArrayView<int>, int, int, int> kernel;
            Dictionary<Type, Delegate> loaded = Kernels.GetValue(acc, _ => new Dictionary<Type, Delegate>());
            lock (loaded)
            {
                if (loaded.TryGetValue(typeof(T), out Delegate d))
                {
                    kernel = (Action<AcceleratorStream, Index1D, ArrayView<T>, ArrayView<T>, ArrayView<int>, int, int, int>)d;
                }
                else
                {
                    kernel = acc.LoadAutoGroupedKernel<Index1D, ArrayView<T>, ArrayView<T>, ArrayView<int>, int, int, int>(Kernel<T>);
                    loaded[typeof(T)] = kernel;
                }
            }

            kernel(stream, new Index1D(rows * n), column, staging, slots, n, pitch, cap);
        }
    }

    /// <summary>
    /// One column of a size class as rows <see cref="Row"/> to <c>Row + Width</c> of its arena (a
    /// <see cref="Col{T}"/> built with a fill a row): laid out <c>[k·Cap + slot]</c> from its first
    /// row as a column of its own is, so the kernels and the host index it as they would one.
    /// </summary>
    /// <remarks>
    /// The class's columns of one element type and one copy kind (constant, block, down) share an
    /// arena, so a class's upload and download are one copy an arena rather than one a column: the
    /// copies are bound by their count at about 10 µs each, not by their bytes
    /// (card-next-r57.md Part 9). The arena holds the host array and the device buffer; a segment
    /// is a window on both, taken afresh at each use because a resize replaces them.
    /// </remarks>
    internal sealed class Seg<T> where T : unmanaged
    {
        public readonly int Row, Width;

        internal Col<T> Arena;

        public Seg(int row, int width)
        {
            Row = row;
            Width = width;
        }

        public int Cap => Arena.Cap;

        public Span<T> Host => Arena.Host.AsSpan(Row * Arena.Cap, Width * Arena.Cap);

        public ArrayView<T> View => Arena.View.SubView((long)Row * Arena.Cap, (long)Width * Arena.Cap);
    }

    /// <summary>A flat device buffer with no host copy of its own: the grid's scratch.</summary>
    internal sealed class Scratch<T> : IDisposable where T : unmanaged
    {
        private readonly Accelerator _acc;

        public MemoryBuffer1D<T, Stride1D.Dense> Dev;

        public long Length { get; private set; }

        public Scratch(Accelerator acc, long length)
        {
            _acc = acc;
            Length = Math.Max(1, length);
            Dev = _acc.Allocate1D<T>(Length);
        }

        public ArrayView<T> View => Dev.View;

        /// <summary>At least <paramref name="length"/>, grown by half again so a growing crowd reallocates rarely.</summary>
        public bool Ensure(long length)
        {
            if (length <= Length) return false;

            Length = Math.Max(length, Length + Length / 2);
            Dev.Dispose();
            Dev = _acc.Allocate1D<T>(Length);
            return true;
        }

        public void Dispose() => Dev?.Dispose();
    }
}

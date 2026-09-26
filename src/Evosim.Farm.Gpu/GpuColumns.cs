using System;
using System.Collections.Generic;
using ILGPU;
using ILGPU.Runtime;

namespace Evosim.Farm.Gpu
{
    /// <summary>One array the card holds and its copy on the host, moved as a whole.</summary>
    internal interface IColumn : IDisposable
    {
        void Resize(int cap);

        void Up();

        void Down();

        long Bytes { get; }
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
    /// (logbook/specs/transport-probe). The copy calls are the plain ones; the driver sees that the
    /// range is registered. The bytes moved are the same bytes, so nothing a run computes moves.
    /// </para>
    /// </remarks>
    internal sealed class Col<T> : IColumn where T : unmanaged
    {
        private readonly Accelerator _acc;
        private readonly T _fill;
        private PageLockScope<T> _lock;

        public int Width { get; private set; }

        public int Cap { get; private set; }

        public T[] Host;

        public MemoryBuffer1D<T, Stride1D.Dense> Dev;

        public Col(Accelerator acc, int width, int cap, T fill = default)
        {
            _acc = acc;
            _fill = fill;
            Width = Math.Max(1, width);
            Cap = Math.Max(1, cap);
            Host = HostArray(Width * Cap);
            if (!EqualityComparer<T>.Default.Equals(fill, default)) Array.Fill(Host, fill);
            Dev = _acc.Allocate1D<T>(Host.Length);
        }

        public ArrayView<T> View => Dev.View;

        public long Bytes => (long)Host.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

        public void Resize(int cap)
        {
            cap = Math.Max(1, cap);
            if (cap == Cap) return;

            var next = HostArray(Width * cap);
            if (!EqualityComparer<T>.Default.Equals(_fill, default)) Array.Fill(next, _fill);

            int keep = Math.Min(Cap, cap);
            for (int k = 0; k < Width; k++) Array.Copy(Host, k * Cap, next, k * cap, keep);

            Host = next;
            Cap = cap;
            Dev.Dispose();
            Dev = _acc.Allocate1D<T>(Host.Length);
        }

        /// <summary>More rows at the end; every existing index keeps its place.</summary>
        public void Widen(int width)
        {
            if (width <= Width) return;

            var next = HostArray(width * Cap);
            if (!EqualityComparer<T>.Default.Equals(_fill, default)) Array.Fill(next, _fill);
            Array.Copy(Host, next, Host.Length);

            Host = next;
            Width = width;
            Dev.Dispose();
            Dev = _acc.Allocate1D<T>(Host.Length);
        }

        public void Up() => Dev.View.CopyFromCPU(Host);

        public void Down() => Dev.View.CopyToCPU(Host);

        /// <summary>The first <paramref name="rows"/> rows up, <c>rows · Cap</c> elements.</summary>
        public void UpRows(int rows)
        {
            long n = Math.Min(Host.Length, (long)rows * Cap);
            if (n > 0) Dev.View.SubView(0, n).CopyFromCPU(ref Host[0], n);
        }

        /// <summary>The first <paramref name="rows"/> rows down, <c>rows · Cap</c> elements.</summary>
        public void DownRows(int rows)
        {
            long n = Math.Min(Host.Length, (long)rows * Cap);
            if (n > 0) Dev.View.SubView(0, n).CopyToCPU(ref Host[0], n);
        }

        public void Dispose()
        {
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

using System;
using System.Threading.Tasks;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Fixed contiguous slabs of an index range, run across threads: the split the phases
    /// between the parallel step use (the contact grid's build and fill, the commit, the
    /// census).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A pace setting and never a realisation.</b> Every loop that runs on this writes only
    /// what its own slab owns, and every sum whose rounding could depend on its order is taken
    /// afterwards, serially, in the order it always was; an integer count is summed per slab,
    /// which is exact in any order. So a step's numbers are the same bits at any thread count,
    /// and the digest at one and at N threads is the check of that.
    /// </para>
    /// <para>
    /// <b>A fixed partition, not a work-stealing one</b>, so that the slab an item falls in is a
    /// function of the item count and the thread count alone and a failure reproduces.
    /// </para>
    /// </remarks>
    internal static class Slabs
    {
        /// <summary>The fewest items a slab is worth: under it, a thread costs more than it saves.</summary>
        public const int MinimumPerSlab = 512;

        /// <summary>How many slabs <paramref name="items"/> are split into at <paramref name="threads"/>.</summary>
        public static int CountFor(int items, int threads)
        {
            if (items <= 0) return 0;
            int n = threads < 1 ? 1 : threads;
            int most = (items + MinimumPerSlab - 1) / MinimumPerSlab;
            return n < most ? n : most;
        }

        /// <summary>The first item of a slab; the slab after the last starts at <paramref name="items"/>.</summary>
        public static int Start(int slab, int slabs, int items) => (int)((long)slab * items / slabs);

        /// <summary>
        /// Runs <paramref name="body"/> once for each slab, with the slab's index and its
        /// half-open range, and returns when all are done. One code path at every slab count,
        /// including one, for <c>StepOnCpu</c>'s reason.
        /// </summary>
        public static void Run(int items, int slabs, Action<int, int, int> body)
        {
            if (slabs <= 0) return;

            var options = new ParallelOptions { MaxDegreeOfParallelism = slabs };
            Parallel.For(0, slabs, options,
                s => body(s, Start(s, slabs, items), Start(s + 1, slabs, items)));
        }
    }
}

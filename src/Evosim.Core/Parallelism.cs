using System;

namespace Evosim.Core
{
    /// <summary>
    /// How many threads Core's own per-cell loops may run on. One, unless a host says otherwise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A pace setting and never a realisation.</b> Every loop that reads this is one whose
    /// iterations are independent — each reads a snapshot buffer and writes its own cell, or
    /// writes cells no other iteration touches — and every reduction over such a loop is summed
    /// afterwards in the serial order it always had. So the numbers a world produces are the
    /// same bits at any setting, including this one, and the only thing the knob moves is the
    /// wall clock. That is the same contract <c>Evosim.Dynamics</c>' thread count carries, and it
    /// is what lets a recorded run replay on a machine with a different core count.
    /// </para>
    /// <para>
    /// <b>The default is one, so the Editor is untouched.</b> Unity's farm never sets it, so the
    /// Unity build takes the same serial path it always has and no recorded run's replay depends
    /// on a scheduler. The console farm sets it from <c>EVOSIM_THREADS</c>, beside the solver's.
    /// </para>
    /// <para>
    /// <b>Static because the loops that read it are deep inside the fields.</b> A grid is
    /// constructed by <see cref="World"/> out of a <see cref="RunConfig"/>, and a thread count is
    /// not a tunable: it is not hashed, it is not written to a config, and two runs that differ
    /// only in it are the same run. Threading it through the constructors would put a pace
    /// setting in the middle of the description of a world.
    /// </para>
    /// </remarks>
    public static class Parallelism
    {
        private static int _threads = 1;

        /// <summary>
        /// Threads Core's independent per-cell loops may use. At or below 1 every such loop takes
        /// the plain serial path it took before this existed — not a one-thread
        /// <c>Parallel.For</c>, but the original loop.
        /// </summary>
        public static int Threads
        {
            get => _threads;
            set => _threads = value < 1 ? 1 : value;
        }

        /// <summary>Whether a loop should split at all — <c>false</c> at one thread.</summary>
        public static bool On => _threads > 1;

        /// <summary>
        /// Splits <paramref name="count"/> items into at most <see cref="Threads"/> contiguous
        /// slabs and runs <paramref name="slab"/> on each, blocking until all are done.
        /// </summary>
        /// <param name="count">How many items there are — rows, slabs, cells.</param>
        /// <param name="slab">Called with a half-open range. Must touch nothing another range does.</param>
        /// <remarks>
        /// <para>
        /// <b>A fixed partition, not a work-stealing one.</b> The ranges are the same for a given
        /// count and thread count on every step and every machine, which makes a run's behaviour
        /// independent of how the scheduler happens to feel — and since every range writes only
        /// its own cells, the partition cannot change a number anyway. It is fixed so that a
        /// failure is reproducible, not so that the arithmetic is.
        /// </para>
        /// <para>
        /// <b>Falls through to one call at one thread</b>, so the serial path has no task, no
        /// delegate invocation per item and no allocation.
        /// </para>
        /// </remarks>
        public static void ForRanges(int count, Action<int, int> slab)
        {
            if (slab == null) throw new ArgumentNullException(nameof(slab));
            if (count <= 0) return;

            int threads = _threads;

            if (threads <= 1 || count < 2)
            {
                slab(0, count);
                return;
            }

            int slabs = threads < count ? threads : count;
            int size = count / slabs;
            int remainder = count - size * slabs;

            var options = new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = slabs,
            };

            System.Threading.Tasks.Parallel.For(0, slabs, options, s =>
            {
                // The first `remainder` slabs take one extra item, which keeps the ranges
                // contiguous and covering without a partitioner.
                int from = s * size + (s < remainder ? s : remainder);
                int to = from + size + (s < remainder ? 1 : 0);
                slab(from, to);
            });
        }
    }
}

using System.Threading;

namespace Evosim.Dynamics
{
    /// <summary>
    /// The contact query's extremes over one stats row's window (proposals/propose-farm-pace-guard.md,
    /// item 1): the most candidates any query kept and the queries that reached the card's cap, the most
    /// overlaps any body listed and those past the card's cap, and the row whose sphere covered the most
    /// grid cells, by its body. Round 61's r61-p6103 slowed tenfold on 2026-10-17 because one part's
    /// sphere covered 81,000 cells; these are the numbers that found it, kept in every row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An instrument, read by nothing the world steps.</b> Every value is a maximum or a count over a
    /// set the step already decides, so it is the same at any thread count and at any giants threshold,
    /// and a resumed run writes the same rows as the unbroken one. A tie for the most cells goes to the
    /// body earliest in the creature list, which a checkpoint restores, and never to a card slot, which it
    /// does not.
    /// </para>
    /// <para>
    /// <b>The window closes with the row</b> (<see cref="Reset"/>), and a checkpoint is written after the
    /// row that closes a window, so a checkpoint never holds an open one and its format does not change.
    /// </para>
    /// <para>
    /// The CPU solver has no caps, so on it the two counts at the caps stay 0 and the most candidates is
    /// the whole list.
    /// </para>
    /// </remarks>
    public sealed class ContactWatch
    {
        private long _candidatesMost;
        private long _overlapsMost;

        /// <summary>The most candidates one query kept in the window.</summary>
        public long CandidatesMost => Volatile.Read(ref _candidatesMost);

        /// <summary>Queries in the window that met more distinct rows than the card's cap and dropped one.</summary>
        public long QueriesAtCap { get; set; }

        /// <summary>The most overlaps one body listed in a step of the window.</summary>
        public long OverlapsMost => Volatile.Read(ref _overlapsMost);

        /// <summary>That body, or -1.</summary>
        public long OverlapsMostBody { get; private set; } = -1;

        /// <summary>That body's links.</summary>
        public int OverlapsMostLinks { get; private set; }

        private long _overlapsMostRank = long.MaxValue;
        private readonly object _overlapsGate = new object();

        /// <summary>Overlaps in the window that one body met past the card's cap of its list.</summary>
        public long OverlapsPastCap { get; set; }

        /// <summary>The most grid cells one row's sphere covered in a step of the window.</summary>
        public long CellsMost { get; private set; }

        /// <summary>The body that row belongs to, or -1.</summary>
        public long CellsMostBody { get; private set; } = -1;

        /// <summary>That body's links.</summary>
        public int CellsMostLinks { get; private set; }

        private long _cellsMostRank = long.MaxValue;

        /// <summary>The window the last row closed, for a reader after the row (the farm's pace guard); null before.</summary>
        public ContactWatch LastWindow { get; private set; }

        public void NoteCandidates(long n) => Max(ref _candidatesMost, n);

        /// <summary>
        /// A body's overlaps in a step, its place in the creature list, its id and links; called from any thread.
        /// The most wins, and of equal counts the earliest body in the list.
        /// </summary>
        public void NoteOverlaps(long n, long rank, long bodyId, int links)
        {
            if (n <= 0 || n < Volatile.Read(ref _overlapsMost)) return;

            lock (_overlapsGate)
            {
                if (n < _overlapsMost || (n == _overlapsMost && rank >= _overlapsMostRank)) return;
                OverlapsMostBody = bodyId;
                OverlapsMostLinks = links;
                _overlapsMostRank = rank;
                Volatile.Write(ref _overlapsMost, n);
            }
        }

        /// <summary>
        /// A row's cells, its body's place in the creature list, its id and links; called serially. The
        /// most cells wins, and of equal counts the earliest body in the list.
        /// </summary>
        public void NoteCells(long cells, long rank, long bodyId, int links)
        {
            if (cells < CellsMost || (cells == CellsMost && rank >= _cellsMostRank)) return;
            CellsMost = cells;
            _cellsMostRank = rank;
            CellsMostBody = bodyId;
            CellsMostLinks = links;
        }

        /// <summary>Closes the window: called after the row that reports it.</summary>
        public void Reset()
        {
            LastWindow = new ContactWatch
            {
                _candidatesMost = CandidatesMost,
                _overlapsMost = OverlapsMost,
                OverlapsMostBody = OverlapsMostBody,
                OverlapsMostLinks = OverlapsMostLinks,
                _overlapsMostRank = _overlapsMostRank,
                QueriesAtCap = QueriesAtCap,
                OverlapsPastCap = OverlapsPastCap,
                CellsMost = CellsMost,
                CellsMostBody = CellsMostBody,
                CellsMostLinks = CellsMostLinks,
                _cellsMostRank = _cellsMostRank,
            };
            Volatile.Write(ref _candidatesMost, 0);
            Volatile.Write(ref _overlapsMost, 0);
            OverlapsMostBody = -1;
            OverlapsMostLinks = 0;
            _overlapsMostRank = long.MaxValue;
            QueriesAtCap = 0;
            OverlapsPastCap = 0;
            CellsMost = 0;
            CellsMostBody = -1;
            CellsMostLinks = 0;
            _cellsMostRank = long.MaxValue;
        }

        private static void Max(ref long at, long n)
        {
            long seen = Volatile.Read(ref at);
            while (n > seen)
            {
                long was = Interlocked.CompareExchange(ref at, n, seen);
                if (was == seen) return;
                seen = was;
            }
        }
    }
}

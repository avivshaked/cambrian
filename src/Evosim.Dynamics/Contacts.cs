using System;
using System.Collections.Generic;

namespace Evosim.Dynamics
{
    /// <summary>
    /// A uniform hash grid over the creatures' bounding spheres, rebuilt once per step from the
    /// places they held at the end of the previous one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole point is that a creature's step never reads another creature's current
    /// state.</b> The grid is built serially before anything is stepped, from state that is by
    /// then frozen, and each creature then reads it. Nothing is written into it during the
    /// parallel phase, so a body's trajectory cannot depend on which thread reached it first.
    /// </para>
    /// <para>
    /// <b>Every sphere is entered in every cell it covers, and a query walks the cells its own
    /// sphere covers.</b> Two spheres that touch share a point, and the cell that point is in is
    /// in both sphere's ranges, so the query is exact at any cell size. Until 2026-09-22 the grid
    /// entered each sphere in one cell and made the cell two of the <i>largest</i> radius so
    /// that the twenty-seven-cell neighbourhood was exact; round 44 seed 1 then grew module
    /// chains with bounding radii of up to twenty metres in a tank fifty-three metres across,
    /// the cell became the tank, every body was a neighbour of every other, and the solver's
    /// cost per body-step rose seven- to thirtyfold with the neighbour lists and their sort
    /// (logbook/0113's read; the bench's record mode is the measurement). The cell is now two of
    /// the <i>mean</i> radius: a body of ordinary size covers one to eight cells, and a giant
    /// covers as many as its size warrants and pays for its own reach rather than charging it to
    /// the crowd.
    /// </para>
    /// <para>
    /// <b>The pairs are the same pairs, in the same order.</b> A query's candidates are sorted
    /// ascending and deduplicated, the caller's overlap test is unchanged, and the sum it takes
    /// is over the same overlapping bodies in the same index order, so the force is the same
    /// bits as the one-cell grid produced: every recording replays and the digest is unmoved at
    /// every thread count.
    /// </para>
    /// <para>
    /// <b>The build commits the spheres it reads</b> (2026-10-03,
    /// <c>logbook/specs/farm-physics-speed-read.md</c> P1). A step leaves each body's new sphere
    /// pending and no longer commits it in a pass of its own; the build commits each body's as it
    /// first touches it (<see cref="Creature.EnsureContactCommitted"/>), after the parallel phase
    /// has ended and before anything reads it, which is where the commit pass sat. A body built or
    /// moved between steps was committed by whoever did it, and is left as it is.
    /// </para>
    /// <para>
    /// <b>Buckets are a compressed row, not a dictionary of lists.</b> Two counting passes and a
    /// prefix sum fill flat arrays that are reused from step to step, so the grid allocates only
    /// when the population, or the number of cells its spheres cover, grows.
    /// </para>
    /// </remarks>
    public sealed class ContactGrid
    {
        private int[] _bucketStart = new int[1];
        private int[] _cursor = new int[1];
        private int[] _items = Array.Empty<int>();
        private int[] _lo = Array.Empty<int>();      // 3 per creature: the lowest cell its sphere covers
        private int[] _hi = Array.Empty<int>();      // 3 per creature: the highest; hi < lo is not entered
        private int _mask;
        private double _cellSize = 1.0;

        /// <summary>The contact query's extremes over the stats row's window, noted by every build and query.</summary>
        public ContactWatch Watch { get; } = new ContactWatch();

        // The row whose sphere covers the most cells, in row order so that equal counts go to the earliest
        // body in the list (ContactWatch.NoteCells). Serial, one pass over the ranges already built.
        private void NoteCells(int rows, bool perPart)
        {
            for (int i = 0; i < rows; i++)
            {
                if (_lo[3 * i] > _hi[3 * i]) continue;
                long cells = (long)(_hi[3 * i] - _lo[3 * i] + 1) * (_hi[3 * i + 1] - _lo[3 * i + 1] + 1) *
                             (_hi[3 * i + 2] - _lo[3 * i + 2] + 1);
                if (cells < Watch.CellsMost) continue;
                int b = perPart ? _rowBody[i] : i;
                Creature c = _creatures[b];
                Watch.NoteCells(cells, b, c.Id, c.Links);
            }
        }
        private int _count;

        private IReadOnlyList<Creature> _creatures = Array.Empty<Creature>();

        public double CellSize => _cellSize;

        /// <summary>The largest active bounding radius the last build saw, metres.</summary>
        public double LargestRadius { get; private set; }

        /// <summary>The mean active bounding radius the last build saw, metres; the cell is twice it.</summary>
        public double MeanRadius { get; private set; }

        /// <summary>Cell entries the last build made, summed over every sphere.</summary>
        public int Entries { get; private set; }

        /// <param name="cellOverride">
        /// A cell size to use in place of the rule, metres; 0 is the rule. The bench's probe of
        /// what the cell costs (logbook/0113's read of round 44 seed 1); no farm sets it.
        /// </param>
        public void Build(IReadOnlyList<Creature> creatures, double cellOverride = 0.0)
        {
            _creatures = creatures;
            _count = creatures.Count;
            _perPart = false;
            _strays.Clear();

            if (_count == 0) return;

            // The last step's spheres, committed across threads before the serial reads below:
            // a body's commit touches its own slots alone, so this is the pass the step used to
            // end with, moved here.
            Slabs.Run(_count, Slabs.CountFor(_count, Threads), (s, from, to) =>
            {
                for (int i = from; i < to; i++) creatures[i].EnsureContactCommitted();
            });

            double largest = 0, sum = 0;
            int active = 0;
            for (int i = 0; i < _count; i++)
            {
                Creature body = creatures[i];
                if (!body.ContactActive) continue;

                // A sphere that is not a number is a stray, below, and is left out of the cell's
                // mean so that it cannot make every other sphere one too. Never true of a body
                // the solver is still stepping, so no recorded world's cell moves.
                if (!FiniteSphere(body.ContactCentre.X, body.ContactCentre.Y, body.ContactCentre.Z, body.ContactRadius)) continue;

                if (body.ContactRadius > largest) largest = body.ContactRadius;
                sum += body.ContactRadius;
                active++;
            }

            LargestRadius = largest;
            MeanRadius = active > 0 ? sum / active : 0;

            // Two of the mean radius, never under a quarter of a metre: the campaign's bodies
            // are half a metre to two metres across the radius, so an ordinary body covers one
            // to eight cells and a query reads a few dozen candidates.
            double rule = MeanRadius > 0.125 ? 2.0 * MeanRadius : 0.25;
            _cellSize = cellOverride > 0 ? cellOverride : rule;
            JoinSeams();

            if (_lo.Length < 3 * _count)
            {
                _lo = new int[3 * _count];
                _hi = new int[3 * _count];
            }

            long entries = 0;
            for (int i = 0; i < _count; i++)
            {
                Creature body = creatures[i];

                if (!body.ContactActive)
                {
                    // Not entered and never a candidate: lo above hi on every axis.
                    _lo[3 * i] = 1; _hi[3 * i] = 0;
                    _lo[3 * i + 1] = 1; _hi[3 * i + 1] = 0;
                    _lo[3 * i + 2] = 1; _hi[3 * i + 2] = 0;
                    continue;
                }

                double r = body.ContactRadius;
                Vec3 c = body.ContactCentre;

                // Out of the grid's reach: entered nowhere and named, for the world to take out
                // before anything reads the grid (DynamicsWorld.LoseStrays).
                if (OutOfReach(c.X, c.Y, c.Z, r, _cellSize))
                {
                    _lo[3 * i] = 1; _hi[3 * i] = 0;
                    _lo[3 * i + 1] = 1; _hi[3 * i + 1] = 0;
                    _lo[3 * i + 2] = 1; _hi[3 * i + 2] = 0;
                    _strays.Add(new ContactStray(i, -1, c.X, c.Y, c.Z, r));
                    continue;
                }

                int lx = Floor((c.X - r) / _cellX), hx = Floor((c.X + r) / _cellX);
                int ly = Floor((c.Y - r) / _cellSize), hy = Floor((c.Y + r) / _cellSize);
                int lz = Floor((c.Z - r) / _cellZ), hz = Floor((c.Z + r) / _cellZ);
                if (_wrapX > 0) ClampToSeams(ref hx, lx, ref hz, lz);

                _lo[3 * i] = lx; _hi[3 * i] = hx;
                _lo[3 * i + 1] = ly; _hi[3 * i + 1] = hy;
                _lo[3 * i + 2] = lz; _hi[3 * i + 2] = hz;

                entries += (long)(hx - lx + 1) * (hy - ly + 1) * (hz - lz + 1);
            }

            if (entries > int.MaxValue / 2)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"The contact grid would hold {entries} cell entries for {_count} bodies ") +
                    FormattableString.Invariant($"at a cell of {_cellSize:0.###} m: a body's sphere is ") +
                    "so far out of scale with the crowd's that the grid cannot hold it. A radius " +
                    "that size is a diverged body, which the divergence check should have taken " +
                    "out before this step.");
            }

            Entries = (int)entries;

            Fill(_count);
            NoteCells(_count, perPart: false);
        }

        /// <summary>
        /// The buckets, from the ranges already in <c>_lo</c> and <c>_hi</c> for rows 0 to
        /// <paramref name="rows"/>: a row is a body under the body sphere and a link under
        /// per-part contact, and the bucketing does not care which.
        /// </summary>
        /// <param name="slabCounts">
        /// Whether <c>_slabEntries</c> already holds each row slab's entry count at this
        /// <see cref="Threads"/>: <see cref="BuildLinks"/> sums it over the same slabs, so the
        /// count pass is not repeated (P10, logbook/specs/speed-plan.md).
        /// </param>
        private void Fill(int rows, bool slabCounts = false)
        {
            int buckets = 1;
            while (buckets < rows * 2) buckets <<= 1;
            _mask = buckets - 1;

            if (_bucketStart.Length < buckets + 1)
            {
                _bucketStart = new int[buckets + 1];
                _cursor = new int[buckets + 1];
            }
            if (_items.Length < Entries) _items = new int[System.Math.Max(Entries, 2 * _items.Length)];

            // Each row slab's first entry, the integer sum of the entries in the slabs before it,
            // so that every entry is written below at its place in the serial order (row by row,
            // then x, y and z) whichever thread writes it.
            int rowSlabs = Slabs.CountFor(rows, Threads);
            if (_slabFirst.Length < rowSlabs + 1) _slabFirst = new int[rowSlabs + 1];
            if (_entryKey.Length < Entries)
            {
                int size = System.Math.Max(Entries, 2 * _entryKey.Length);
                _entryKey = new int[size];
                _entryRow = new int[size];
            }

            if (slabCounts)
            {
                // Each count is at most Entries, which the caller has bounded by int.MaxValue / 2.
                for (int s = 0; s < rowSlabs; s++) _slabFirst[s + 1] = (int)_slabEntries[s];
            }
            else
            {
                Slabs.Run(rows, rowSlabs, (s, from, to) =>
                {
                    int mine = 0;
                    for (int i = from; i < to; i++)
                    {
                        mine += (_hi[3 * i] - _lo[3 * i] + 1) *
                                (_hi[3 * i + 1] - _lo[3 * i + 1] + 1) *
                                (_hi[3 * i + 2] - _lo[3 * i + 2] + 1);
                    }
                    _slabFirst[s + 1] = mine;
                });
            }

            _slabFirst[0] = 0;
            for (int s = 0; s < rowSlabs; s++) _slabFirst[s + 1] += _slabFirst[s];

            if (_slabFirst[rowSlabs] != Entries)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"The contact grid counted {Entries} cell entries ") +
                    FormattableString.Invariant($"and its rows cover {_slabFirst[rowSlabs]}."));
            }

            // The counting sort, split by bucket. Each owner takes a power-of-two range of buckets,
            // and every bucket is counted and filled with its entries in ascending entry order,
            // which is the order the single loop filled it in, so _bucketStart and _items come out
            // the same arrays at any thread count. The entries reach their owners through one
            // stable partition first, so each owner reads its own entries and no others: the
            // owners used to read every entry twice each, sixteen times over at sixteen threads
            // (logbook/specs/cpu-profile-2026-09-26.txt).
            int mask = _mask;
            int entries = Entries;
            int owners = 1;
            while (owners * 2 <= Threads && buckets / (owners * 2) >= Slabs.MinimumPerSlab) owners *= 2;
            int shift = 0;
            while ((owners << shift) < buckets) shift++;

            if (entries == 0)
            {
                Array.Clear(_bucketStart, 0, buckets + 1);
                return;
            }

            // The partition is cut at the row slabs, whose entries are the contiguous ranges
            // _slabFirst gives, in ascending order: an owner's entries still lie together and in
            // ascending entry order, which is all the stage below needs, so the staged arrays are
            // the ones entry slabs gave. Cut there, each entry's key is counted for its owner in
            // the pass that writes it, and the separate count pass over the entries goes (P10).
            int cells = rowSlabs * owners;
            if (_partition.Length < cells) _partition = new int[cells];
            if (_ownerFirst.Length < owners + 1) _ownerFirst = new int[owners + 1];
            if (_stageKey.Length < entries)
            {
                int size = System.Math.Max(entries, 2 * _stageKey.Length);
                _stageKey = new int[size];
                _stageRow = new int[size];
            }

            // Every entry's bucket and row at its place in the serial order, and how many of each
            // row slab's entries each owner takes.
            Slabs.Run(rows, rowSlabs, (s, from, to) =>
            {
                int row0 = s * owners;
                for (int o = 0; o < owners; o++) _partition[row0 + o] = 0;

                int e = _slabFirst[s];
                for (int i = from; i < to; i++)
                {
                    for (int x = _lo[3 * i]; x <= _hi[3 * i]; x++)
                    for (int y = _lo[3 * i + 1]; y <= _hi[3 * i + 1]; y++)
                    for (int z = _lo[3 * i + 2]; z <= _hi[3 * i + 2]; z++)
                    {
                        int key = Hash(SeamX(x), y, SeamZ(z)) & mask;
                        _entryKey[e] = key;
                        _entryRow[e] = i;
                        _partition[row0 + (key >> shift)]++;
                        e++;
                    }
                }
            });

            // Where each (owner, slab) part starts, owner by owner and within an owner slab by
            // slab, so an owner's entries lie together and in ascending entry order.
            int placed = 0;
            for (int o = 0; o < owners; o++)
            {
                _ownerFirst[o] = placed;
                for (int s = 0; s < rowSlabs; s++)
                {
                    int n = _partition[s * owners + o];
                    _partition[s * owners + o] = placed;
                    placed += n;
                }
            }
            _ownerFirst[owners] = placed;

            Slabs.Run(rows, rowSlabs, (s, from, to) =>
            {
                int row0 = s * owners;
                int last = _slabFirst[s + 1];
                for (int e = _slabFirst[s]; e < last; e++)
                {
                    int key = _entryKey[e];
                    int at = _partition[row0 + (key >> shift)]++;
                    _stageKey[at] = key;
                    _stageRow[at] = _entryRow[e];
                }
            });

            // Each owner counts its buckets, offsets them from its first entry (every key below
            // its range belongs to an owner before it) and fills them in its entries' order.
            Slabs.Run(owners, owners, (o, oFrom, oTo) =>
            {
                int low = o << shift, high = (o + 1) << shift;
                int first = _ownerFirst[o], last = _ownerFirst[o + 1];

                for (int k = low; k < high; k++) _bucketStart[k] = 0;
                for (int at = first; at < last; at++) _bucketStart[_stageKey[at]]++;

                int running = first;
                for (int k = low; k < high; k++)
                {
                    int n = _bucketStart[k];
                    _bucketStart[k] = running;
                    _cursor[k] = running;
                    running += n;
                }

                for (int at = first; at < last; at++) _items[_cursor[_stageKey[at]]++] = _stageRow[at];
            });

            _bucketStart[buckets] = entries;
        }

        // ------------------------------------------------------------ per-part contact, D114

        private bool _perPart;
        private int _rows;
        private int[] _rowStart = new int[1];          // a body's first row; count + 1 entries
        private int[] _rowBody = Array.Empty<int>();   // the body a row belongs to
        private int[] _rowLink = Array.Empty<int>();   // and which of its links
        private int[] _bodyLinks = Array.Empty<int>(); // each body's link count, gathered
        private bool[] _rowActive = Array.Empty<bool>();     // whether a row's body is in contact
        private double[] _rowRadius = Array.Empty<double>(); // a row's sphere, gathered flat
        private double[] _rowCentre = Array.Empty<double>(); // 3 per row
        private long[] _slabEntries = Array.Empty<long>();   // a row slab's cell entries
        private int[] _slabStray = Array.Empty<int>();       // a row slab's first row out of reach, or -1
        private int[] _slabFirst = Array.Empty<int>();       // a row slab's first entry
        private int[] _entryKey = Array.Empty<int>();        // an entry's bucket
        private int[] _entryRow = Array.Empty<int>();        // and its row
        private int[] _partition = Array.Empty<int>();       // an (entry slab, owner) part's count, then its start
        private int[] _ownerFirst = Array.Empty<int>();      // an owner's first staged entry; owners + 1
        private int[] _stageKey = Array.Empty<int>();        // the entries partitioned by owner: bucket
        private int[] _stageRow = Array.Empty<int>();        // and row

        /// <summary>
        /// Threads the build and the fill may split across (<see cref="Slabs"/>); a pace setting
        /// that moves no number. The world sets it from its own before every build.
        /// </summary>
        public int Threads { get; set; } = 1;

        /// <summary>Whether the last build entered links rather than bodies.</summary>
        public bool PerPart => _perPart;

        /// <summary>
        /// The grid over every active body's link spheres rather than its body sphere — D114,
        /// <c>logbook/specs/per-part-contact-spec.md</c> section 2.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A row per link, numbered body by body and link by link</b>, so that ascending row
        /// order is ascending <c>(body, link)</c> order and <see cref="NeighbourLinks"/>'s sort is
        /// the fixed order the spec asks the sum to be taken in. Every body gets its rows whether
        /// or not it is active, so a body's first row is a prefix sum of the links before it and
        /// needs no search; an inactive body's rows are not entered and are never a candidate.
        /// </para>
        /// <para>
        /// <b>The cell is two of the mean link radius</b>, <see cref="Build"/>'s rule read on
        /// links, with the same floor: a link is smaller than the body it belongs to, so a body's
        /// queries read fewer cells each and the giant pays for its own parts.
        /// </para>
        /// </remarks>
        public void BuildLinks(IReadOnlyList<Creature> creatures, double cellOverride = 0.0)
        {
            _creatures = creatures;
            _count = creatures.Count;
            _perPart = true;
            _strays.Clear();

            if (_rowStart.Length < _count + 1) _rowStart = new int[_count + 1];
            if (_bodyLinks.Length < _count) _bodyLinks = new int[_count];

            // Every read of a body is across threads, into flat arrays that each body writes only
            // its own slots of; everything summed over them is summed serially, in the order the
            // single loop this replaced summed it, so the cell and every row are the same bits.
            int bodySlabs = Slabs.CountFor(_count, Threads);
            Slabs.Run(_count, bodySlabs, (s, from, to) =>
            {
                for (int b = from; b < to; b++) _bodyLinks[b] = creatures[b].Links;
            });

            int rows = 0;
            for (int b = 0; b < _count; b++)
            {
                _rowStart[b] = rows;
                rows += _bodyLinks[b];
            }
            _rowStart[_count] = rows;
            _rows = rows;

            if (_count == 0) return;

            if (_rowBody.Length < rows)
            {
                _rowBody = new int[rows];
                _rowLink = new int[rows];
            }
            if (_rowActive.Length < rows)
            {
                _rowActive = new bool[rows];
                _rowRadius = new double[rows];
                _rowCentre = new double[3 * rows];
            }

            Slabs.Run(_count, bodySlabs, (s, from, to) =>
            {
                for (int b = from; b < to; b++)
                {
                    Creature body = creatures[b];

                    // The last step's sphere, committed by the pass that reads it: the commit
                    // pass the step used to end with, folded in (P1). Its own slots only.
                    body.EnsureContactCommitted();

                    int first = _rowStart[b];
                    bool live = body.ContactActive;

                    for (int i = 0; i < body.Links; i++)
                    {
                        int row = first + i;
                        _rowBody[row] = b;
                        _rowLink[row] = i;
                        _rowActive[row] = live;
                        if (!live) continue;

                        _rowRadius[row] = body.LinkContactRadius[i];
                        _rowCentre[3 * row] = body.LinkContactCentre[3 * i];
                        _rowCentre[3 * row + 1] = body.LinkContactCentre[3 * i + 1];
                        _rowCentre[3 * row + 2] = body.LinkContactCentre[3 * i + 2];
                    }
                }
            });

            // Body by body and link by link, inactive bodies skipped: the order of the loop this
            // replaced, so the mean radius, and the cell made of it, keep their bits.
            double largest = 0, sum = 0;
            int active = 0;
            for (int row = 0; row < rows; row++)
            {
                if (!_rowActive[row]) continue;

                // Build's rule: a sphere that is not a number is a stray and stays out of the mean.
                if (!FiniteSphere(_rowCentre[3 * row], _rowCentre[3 * row + 1], _rowCentre[3 * row + 2], _rowRadius[row])) continue;

                double r = _rowRadius[row];
                if (r > largest) largest = r;
                sum += r;
                active++;
            }

            LargestRadius = largest;
            MeanRadius = active > 0 ? sum / active : 0;

            double rule = MeanRadius > 0.125 ? 2.0 * MeanRadius : 0.25;
            _cellSize = cellOverride > 0 ? cellOverride : rule;
            JoinSeams();

            if (_lo.Length < 3 * rows)
            {
                _lo = new int[3 * rows];
                _hi = new int[3 * rows];
            }

            // The cells each row covers, across threads over the flat arrays; the entry count is
            // an integer, summed per slab and then over the slabs in order.
            int rowSlabs = Slabs.CountFor(rows, Threads);
            if (_slabEntries.Length < rowSlabs) _slabEntries = new long[rowSlabs];
            if (_slabStray.Length < rowSlabs) _slabStray = new int[rowSlabs];
            double cell = _cellSize, cellX = _cellX, cellZ = _cellZ;

            Slabs.Run(rows, rowSlabs, (s, from, to) =>
            {
                long mine = 0;
                int stray = -1;

                for (int row = from; row < to; row++)
                {
                    if (!_rowActive[row])
                    {
                        _lo[3 * row] = 1; _hi[3 * row] = 0;
                        _lo[3 * row + 1] = 1; _hi[3 * row + 1] = 0;
                        _lo[3 * row + 2] = 1; _hi[3 * row + 2] = 0;
                        continue;
                    }

                    double r = _rowRadius[row];
                    double cx = _rowCentre[3 * row];
                    double cy = _rowCentre[3 * row + 1];
                    double cz = _rowCentre[3 * row + 2];

                    // Out of the grid's reach: entered nowhere, and the slab's first such row
                    // noted for the serial scan below.
                    if (OutOfReach(cx, cy, cz, r, cell))
                    {
                        if (stray < 0) stray = row;
                        _lo[3 * row] = 1; _hi[3 * row] = 0;
                        _lo[3 * row + 1] = 1; _hi[3 * row + 1] = 0;
                        _lo[3 * row + 2] = 1; _hi[3 * row + 2] = 0;
                        continue;
                    }

                    int lx = Floor((cx - r) / cellX), hx = Floor((cx + r) / cellX);
                    int ly = Floor((cy - r) / cell), hy = Floor((cy + r) / cell);
                    int lz = Floor((cz - r) / cellZ), hz = Floor((cz + r) / cellZ);
                    if (_wrapX > 0) ClampToSeams(ref hx, lx, ref hz, lz);

                    _lo[3 * row] = lx; _hi[3 * row] = hx;
                    _lo[3 * row + 1] = ly; _hi[3 * row + 1] = hy;
                    _lo[3 * row + 2] = lz; _hi[3 * row + 2] = hz;

                    mine += (long)(hx - lx + 1) * (hy - ly + 1) * (hz - lz + 1);
                }

                _slabEntries[s] = mine;
                _slabStray[s] = stray;
            });

            // The strays in row order, whichever thread found them: from the first slab's first
            // stray, a serial scan of every row after it, so the list is the same list at any
            // thread count. Rows are body by body and link by link, so the list is in body order.
            // Runs only when a sphere has strayed.
            int firstStray = -1;
            for (int s = 0; s < rowSlabs && firstStray < 0; s++) firstStray = _slabStray[s];

            if (firstStray >= 0)
            {
                for (int row = firstStray; row < rows; row++)
                {
                    if (!_rowActive[row]) continue;

                    double r = _rowRadius[row];
                    double cx = _rowCentre[3 * row], cy = _rowCentre[3 * row + 1], cz = _rowCentre[3 * row + 2];
                    if (!OutOfReach(cx, cy, cz, r, cell)) continue;

                    _strays.Add(new ContactStray(_rowBody[row], _rowLink[row], cx, cy, cz, r));
                }
            }

            long entries = 0;
            for (int s = 0; s < rowSlabs; s++) entries += _slabEntries[s];

            if (entries > int.MaxValue / 2)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"The contact grid would hold {entries} cell entries for {rows} links ") +
                    FormattableString.Invariant($"at a cell of {_cellSize:0.###} m: a part's sphere is ") +
                    "so far out of scale with the crowd's that the grid cannot hold it. A radius " +
                    "that size is a diverged body, which the divergence check should have taken " +
                    "out before this step.");
            }

            Entries = (int)entries;

            Fill(rows, slabCounts: true);
            NoteCells(rows, perPart: true);
        }

        /// <summary>A body's first row under per-part contact; its links follow in order.</summary>
        public int FirstRow(int body) => _rowStart[body];

        /// <summary>The body a row belongs to, by its index in the world's list.</summary>
        public int RowBody(int row) => _rowBody[row];

        /// <summary>Which of that body's links a row is.</summary>
        public int RowLink(int row) => _rowLink[row];

        /// <summary>
        /// Fills <paramref name="into"/> with every row whose link sphere covers a cell that row
        /// <paramref name="self"/>'s covers, the rows of <paramref name="self"/>'s own body
        /// excluded, sorted ascending and each once: the (body, link) candidates of one link.
        /// </summary>
        /// <remarks>
        /// A body's own links never pair: self-contact is not modelled, and D101's stillbirth rule
        /// is what keeps a folded body out of the world (the spec, section 1).
        /// </remarks>
        public int NeighbourLinks(int self, ref int[] into)
        {
            if (_rows == 0) return 0;

            int owner = _rowBody[self];
            int lx = _lo[3 * self], hx = _hi[3 * self];
            int ly = _lo[3 * self + 1], hy = _hi[3 * self + 1];
            int lz = _lo[3 * self + 2], hz = _hi[3 * self + 2];
            int found = 0;

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                int h = Hash(SeamX(x), y, SeamZ(z)) & _mask;

                for (int k = _bucketStart[h]; k < _bucketStart[h + 1]; k++)
                {
                    int other = _items[k];
                    if (_rowBody[other] == owner) continue;

                    if (!Covers(other, x, y, z)) continue;

                    if (found == into.Length) Array.Resize(ref into, into.Length * 2);
                    into[found++] = other;
                }
            }

            if (found < 2) return found;

            Array.Sort(into, 0, found);

            int unique = 1;
            for (int i = 1; i < found; i++)
            {
                if (into[i] == into[unique - 1]) continue;
                into[unique++] = into[i];
            }

            return unique;
        }

        /// <summary>
        /// Fills <paramref name="into"/> with the indices of every creature whose sphere covers
        /// a cell that <paramref name="self"/>'s sphere covers, itself excluded, sorted ascending
        /// and each once. Every body whose sphere touches <paramref name="self"/>'s is among them.
        /// </summary>
        public int Neighbours(int self, ref int[] into)
        {
            if (_count == 0) return 0;

            int lx = _lo[3 * self], hx = _hi[3 * self];
            int ly = _lo[3 * self + 1], hy = _hi[3 * self + 1];
            int lz = _lo[3 * self + 2], hz = _hi[3 * self + 2];
            int found = 0;

            for (int x = lx; x <= hx; x++)
            for (int y = ly; y <= hy; y++)
            for (int z = lz; z <= hz; z++)
            {
                int h = Hash(SeamX(x), y, SeamZ(z)) & _mask;

                for (int k = _bucketStart[h]; k < _bucketStart[h + 1]; k++)
                {
                    int other = _items[k];
                    if (other == self) continue;

                    // A hash collision puts another cell's entries in this bucket; an entry is
                    // this cell's only if the cell is inside that body's range.
                    if (!Covers(other, x, y, z)) continue;

                    if (found == into.Length) Array.Resize(ref into, into.Length * 2);
                    into[found++] = other;
                }
            }

            if (found < 2) return found;

            // Ascending, so the caller's sum is taken in index order however the buckets were
            // laid out; and once each, since a body covering several of self's cells was found
            // in each of them.
            Array.Sort(into, 0, found);

            int unique = 1;
            for (int i = 1; i < found; i++)
            {
                if (into[i] == into[unique - 1]) continue;
                into[unique++] = into[i];
            }

            return unique;
        }

        public Creature At(int index) => _creatures[index];

        // ------------------------------------------------------------ the grid's reach

        private readonly List<ContactStray> _strays = new List<ContactStray>();

        /// <summary>
        /// The spheres the last build found out of the grid's reach, in row order (body order,
        /// and link order within a body), each entered in no cell. Empty in every step of a run
        /// in which no body has diverged.
        /// </summary>
        /// <remarks>
        /// A body can blow up between two divergence checks, half a second apart, far enough that
        /// a cell index cast to an int wraps, and the cell loop then runs past the entry arrays:
        /// round 56 seed 2 under one drag panel a side, body 86464 at 28,179.5 s (2026-10-05). The
        /// build names such a sphere instead, and <see cref="DynamicsWorld"/> takes its body out
        /// before anything reads the grid (<c>logbook/specs/r57-build/build-brief.md</c>).
        /// </remarks>
        public IReadOnlyList<ContactStray> Strays => _strays;

        /// <summary>
        /// The most cells a sphere may lie from the origin on any axis: well inside an int, whose
        /// cast in <see cref="Floor"/> overflows past 2.1e9 and, below it, wraps to int.MaxValue,
        /// which a cell loop never passes. Further out, or not finite, is a body diverged between
        /// two divergence checks.
        /// </summary>
        public const double ReachCells = 1e9;

        /// <summary>
        /// Whether a sphere lies out of the grid's reach on any axis: <c>!(|c| + r &lt; 1e9·cell)</c>,
        /// which a NaN or an infinity in the centre or the radius fails as well.
        /// </summary>
        public static bool OutOfReach(double cx, double cy, double cz, double r, double cell)
        {
            double reach = ReachCells * cell;
            return !(Math.Abs(cx) + r < reach && Math.Abs(cy) + r < reach && Math.Abs(cz) + r < reach);
        }

        private static bool FiniteSphere(double cx, double cy, double cz, double r) =>
            double.IsFinite(cx) && double.IsFinite(cy) && double.IsFinite(cz) && double.IsFinite(r);

        private static int Floor(double v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        private static int Hash(int x, int y, int z) =>
            (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);

        // ------------------------------------------------------------ the seams, D204

        /// <summary>
        /// The box's extent along x and z when bodies touch across its seams, m
        /// (<see cref="SolverConfig.SeamLengthMetres"/>); 0 is no seam. Set by the world before
        /// every build, as <see cref="Threads"/> is.
        /// </summary>
        public double SeamLength { get; set; }

        /// <inheritdoc cref="SeamLength"/>
        public double SeamWidth { get; set; }

        private double _cellX = 1.0, _cellZ = 1.0; // the cell along x and z
        private int _wrapX, _wrapZ;                 // the cells round each seam; 0 without one

        /// <summary>
        /// The cell along the two seamed axes: <see cref="_cellSize"/> without a seam, and with
        /// one the whole fraction of the box nearest above it, so that a whole number of cells
        /// goes round each seam and a cell index can be taken modulo that number.
        /// </summary>
        /// <remarks>
        /// <b>The cell moves no number</b> (the pairs are sorted and the overlap test is the
        /// caller's), so widening it along x and z changes the pace and not the world. Without a
        /// seam the two are <see cref="_cellSize"/> itself, and every division below is the one
        /// the recorded grid took.
        /// </remarks>
        private void JoinSeams()
        {
            if (SeamLength > 0 && SeamWidth > 0)
            {
                _wrapX = Math.Max(1, (int)(SeamLength / _cellSize));
                _wrapZ = Math.Max(1, (int)(SeamWidth / _cellSize));
                _cellX = SeamLength / _wrapX;
                _cellZ = SeamWidth / _wrapZ;
            }
            else
            {
                _wrapX = 0;
                _wrapZ = 0;
                _cellX = _cellSize;
                _cellZ = _cellSize;
            }
        }

        /// <summary>
        /// Holds a sphere's span to one turn of each seam: a sphere wider than the box would
        /// otherwise enter some cells twice. Never true of a body the world can hold, whose
        /// sphere is metres across in a box of hundreds.
        /// </summary>
        private void ClampToSeams(ref int hx, int lx, ref int hz, int lz)
        {
            if (hx - lx >= _wrapX) hx = lx + _wrapX - 1;
            if (hz - lz >= _wrapZ) hz = lz + _wrapZ - 1;
        }

        /// <summary>A cell index along x as the hash reads it: itself, or taken round the seam.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int SeamX(int x) => _wrapX == 0 ? x : Modulo(x, _wrapX);

        /// <summary>A cell index along z as the hash reads it: itself, or taken round the seam.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int SeamZ(int z) => _wrapZ == 0 ? z : Modulo(z, _wrapZ);

        private static int Modulo(int v, int n)
        {
            int m = v % n;
            return m < 0 ? m + n : m;
        }

        /// <summary>
        /// Whether a row's span covers cell <c>(x, y, z)</c>, with the cell on x and z taken
        /// round the seam when there is one: the span is unwrapped, so the cell's distance past
        /// its low end, modulo the cells round, must be within its length.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool Covers(int row, int x, int y, int z)
        {
            if (y < _lo[3 * row + 1] || y > _hi[3 * row + 1]) return false;

            if (_wrapX == 0)
            {
                return x >= _lo[3 * row] && x <= _hi[3 * row] &&
                       z >= _lo[3 * row + 2] && z <= _hi[3 * row + 2];
            }

            return Modulo(x - _lo[3 * row], _wrapX) <= _hi[3 * row] - _lo[3 * row] &&
                   Modulo(z - _lo[3 * row + 2], _wrapZ) <= _hi[3 * row + 2] - _lo[3 * row + 2];
        }
    }

    /// <summary>
    /// A contact sphere out of the grid's reach (<see cref="ContactGrid.Strays"/>): the body by its
    /// index in the list the grid was built from, the link (−1 for the body's own sphere), and
    /// the sphere's centre and radius as the build read them.
    /// </summary>
    public readonly struct ContactStray
    {
        public readonly int Body;
        public readonly int Link;
        public readonly double X, Y, Z, Radius;

        public ContactStray(int body, int link, double x, double y, double z, double radius)
        {
            Body = body;
            Link = link;
            X = x;
            Y = y;
            Z = z;
            Radius = radius;
        }

        /// <summary>The sphere in words, for a divergence reason: which sphere, where and how big.</summary>
        public string Describe() =>
            (Link < 0 ? "its contact sphere" : FormattableString.Invariant($"link {Link}'s contact sphere")) +
            FormattableString.Invariant($" at ({X:R}, {Y:R}, {Z:R}), radius {Radius:R}, ") +
            "out of the contact grid's reach";
    }

    /// <summary>
    /// The soft pushes: creature against creature, creature against the bed, creature against
    /// the glass. Each is a spring-damper on the penetration of two bounding spheres, or of one
    /// sphere and a plane or a cylinder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not a contact solve, and it is the largest departure from PhysX in the spike.</b> The
    /// farm gives the engine every collider and gets a joint solve with friction and
    /// depenetration back. This gives one force per creature pair, applied at each body's root
    /// link, computed from state that is already frozen. It cannot jam, it cannot farm
    /// depenetration (<c>MaxDepenetrationVelocity</c> exists in the farm to stop exactly that),
    /// and it has no friction at all.
    /// </para>
    /// <para>
    /// <b>Antisymmetric by construction, so no shared write is needed.</b> The reduced mass and
    /// the normal both flip when the pair is read from the other side, so each creature computes
    /// its own half and the pair is equal and opposite without either touching the other.
    /// </para>
    /// </remarks>
    public static class Contacts
    {
        /// <summary>
        /// The difference between two centres the short way round the box's seams — D204.
        /// </summary>
        /// <remarks>
        /// A component within half the box is returned as it came, so a pair that does not reach
        /// across a seam takes no new float; one further than half is moved by the box's whole
        /// extent, once, which is all a pair of centres inside the box and a few metres past it
        /// ever needs. <c>y</c> has no seam.
        /// </remarks>
        public static Vec3 ShortWay(Vec3 between, SolverConfig config)
        {
            double x = between.X, z = between.Z;
            double length = config.SeamLengthMetres, width = config.SeamWidthMetres;

            if (x > 0.5 * length) x -= length;
            else if (x < -0.5 * length) x += length;

            if (z > 0.5 * width) z -= width;
            else if (z < -0.5 * width) z += width;

            return new Vec3(x, between.Y, z);
        }

        public static void Apply(
            Creature body, int index, ContactGrid grid, SolverConfig config, ref int[] scratch)
        {
            // D114. A branch and nothing else on the recorded path: below it is the body sphere
            // exactly as every recorded world ran it.
            if (config.ContactPerPart)
            {
                // D228. A branch and nothing else on the recorded path: the spheres below are untouched.
                if (config.ContactShape.IsTrueShape())
                    ContactTrueShape.Apply(body, index, grid, config, ref scratch);
                else
                    ApplyPerPart(body, index, grid, config, ref scratch);
                return;
            }

            Vec3 force = Vec3.Zero;

            bool instrument = config.ContactInstrument;
            if (instrument) body.BeginContactStep();

            if (config.CreatureContact)
            {
                int n = grid.Neighbours(index, ref scratch);
                grid.Watch.NoteCandidates(n);
                for (int k = 0; k < n; k++)
                {
                    Creature other = grid.At(scratch[k]);
                    if (!other.ContactActive) continue;

                    Vec3 between = body.ContactCentre - other.ContactCentre;
                    if (config.Seamed) between = ShortWay(between, config);
                    double distance = between.Magnitude;
                    double penetration = body.ContactRadius + other.ContactRadius - distance;
                    if (penetration <= 0) continue;

                    if (instrument) body.NoteOverlap(other.Id);

                    Vec3 normal = distance > 1e-9
                        ? between * (1.0 / distance)
                        : Vec3.UnitY;

                    double reduced =
                        body.TotalMass * other.TotalMass / (body.TotalMass + other.TotalMass);

                    double stiffness = reduced * config.ContactOmega * config.ContactOmega;
                    double damping = 2.0 * config.ContactDampingRatio * reduced * config.ContactOmega;

                    double approach = Vec3.Dot(body.ContactVelocity - other.ContactVelocity, normal);

                    force += normal * ContactLaw.PairPush(
                        stiffness * penetration - damping * approach,
                        reduced, approach, config);
                }
            }

            double mass = body.TotalMass;
            double bedStiffness = mass * config.ContactOmega * config.ContactOmega;
            double bedDamping = 2.0 * config.ContactDampingRatio * mass * config.ContactOmega;

            // The bed. Flat at -depth, or the height map's own surface when the world has one:
            // two branches rather than one general path with a zero in it, so a flat world's
            // arithmetic is identical and not merely equal — PlacementFloor's own gate, for its
            // reason.
            if (config.Bed == null)
            {
                // The bed, flat at -depth.
                double below = -config.WorldDepthMetres - (body.ContactCentre.Y - body.ContactRadius);
                if (below > 0)
                {
                    double up = ContactLaw.PairPush(
                        bedStiffness * below - bedDamping * body.ContactVelocity.Y,
                        mass, body.ContactVelocity.Y, config);

                    force += new Vec3(0, up, 0);

                    // D137. Not entered at 0, the recorded contact's bits.
                    if (config.BedFriction > 0)
                        force += ContactLaw.Friction(new Vec3(0, up, 0), body.ContactVelocity, mass, config);

                    if (instrument) body.TouchedBedOrGlass = true;
                }
            }
            else
            {
                Vec3 rock = ContactBed.Push(body, config, bedStiffness, bedDamping);

                if (rock.X != 0 || rock.Y != 0 || rock.Z != 0)
                {
                    force += rock;
                    if (config.BedFriction > 0)
                        force += ContactLaw.Friction(rock, body.ContactVelocity, mass, config);
                    if (instrument) body.TouchedBedOrGlass = true;
                }
            }

            // The reefs (logbook/specs/reef-spec.md §2): the bed's law and material against the
            // rock's signed distance, counted with the bed and the glass. No branch is taken in a
            // world without reefs, so its arithmetic is the one above to the bit.
            if (config.Reefs != null || config.Coral != null)
            {
                Vec3 reef = ContactReef.Push(
                    body.ContactCentre, body.ContactRadius, body.ContactVelocity, mass,
                    config, bedStiffness, bedDamping);

                if (reef.X != 0 || reef.Y != 0 || reef.Z != 0)
                {
                    force += reef;
                    if (config.BedFriction > 0)
                        force += ContactLaw.Friction(reef, body.ContactVelocity, mass, config);
                    if (instrument) body.TouchedBedOrGlass = true;
                }
            }

            // The glass, a cylinder about the vertical axis.
            if (config.TankRadiusMetres > 0)
            {
                double x = body.ContactCentre.X - config.TankAxisX;
                double z = body.ContactCentre.Z - config.TankAxisZ;
                double radial = System.Math.Sqrt(x * x + z * z);
                double outside = radial + body.ContactRadius - config.TankRadiusMetres;

                if (outside > 0)
                {
                    Vec3 inward = radial > 1e-9
                        ? new Vec3(-x / radial, 0, -z / radial)
                        : Vec3.UnitX;

                    double approach = Vec3.Dot(body.ContactVelocity, inward);

                    Vec3 glass = inward * ContactLaw.PairPush(
                        bedStiffness * outside - bedDamping * approach, mass, approach, config);
                    force += glass;
                    if (config.BedFriction > 0)
                        force += ContactLaw.Friction(glass, body.ContactVelocity, mass, config);

                    if (instrument) body.TouchedBedOrGlass = true;
                }
            }

            force = ContactLaw.BodyPush(force, mass, config);

            if (force.X != 0 || force.Y != 0 || force.Z != 0)
            {
                // Spread over the links by mass, so the push accelerates the whole body and
                // strains none of its joints — the same way a uniform field does.
                //
                // Putting it all on the root instead, which is where the bounding sphere is
                // centred, was the first build and it threw bodies: a deep first-step overlap
                // in the bench's scatter is kilonewtons, and a root link with heavy children on
                // compliant joints is whipped by it. Six of a thousand bodies were lost to that
                // and none to anything else (the isolation is in scratch/solver-spike/logs/).
                // The sphere is a whole-body approximation in the first place, so a whole-body
                // force is the honest way to hand it over.
                double scale = 1.0 / body.TotalMass;
                for (int i = 0; i < body.Links; i++)
                {
                    Vec3.Add(body.Fext, 6 * i + 3, force * (body.Mass[i] * scale));
                }
            }
        }

        /// <summary>
        /// Per-part contact, D114 (<c>logbook/specs/per-part-contact-spec.md</c> section 1): each
        /// link its own sphere, pushed by each other body's links that overlap it and by the bed
        /// and the glass, and the push put on that link.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The body sphere's law, read on a link.</b> The same spring and damper, the reduced
        /// mass taken from the two links' masses rather than the two bodies', and the same two
        /// bounds: <see cref="ContactLaw.PairPush"/> on each pair and
        /// <see cref="ContactLaw.BodyPush"/> on each link's total against that link's mass, so no
        /// pile may change a link's speed by more than the world allows in a step. The spec is
        /// silent on the second bound; a link is the unit the push now lands on, so it is the unit
        /// the bound is read on.
        /// </para>
        /// <para>
        /// <b>Applied at the link's origin, into its own <c>Fext</c> row, with no torque</b>,
        /// because the sphere is centred there. The solver carries it through the joints from
        /// there, which is the point: a leaf struck at its tip turns the body about its root
        /// rather than sliding the whole body sideways. The body sphere spread its force over the
        /// links by mass for the opposite reason, that a whole-body approximation should strain no
        /// joint (the remarks in <see cref="Apply"/>). A one-link body keeps that spread's
        /// arithmetic, <c>F (m (1/m))</c>, which is 1 to within an ulp, so that it is the recorded
        /// body to the bit.
        /// </para>
        /// <para>
        /// <b>A fixed order, so the thread count stays invisible.</b> Links in index order, and
        /// for each the grid's candidates in ascending (body, link) order; everything read is the
        /// committed spheres, frozen before the parallel phase, and everything written is this
        /// body's own.
        /// </para>
        /// </remarks>
        /// <summary>
        /// Records one rock contact on link <paramref name="i"/>: the touch, the push's direction
        /// as the rock's normal (a push from the rock never pulls, so it points out of the rock),
        /// and the sphere's deepest point along it. Where the bed and a reef both push in one step,
        /// the larger push is kept.
        /// </summary>
        private static void NoteRock(Creature body, int i, Vec3 centre, double radius, Vec3 push, ref double kept)
        {
            double size = push.Magnitude;
            if (!(size > kept)) return;

            kept = size;
            Vec3 normal = push * (1.0 / size);
            body.RockTouched[i] = true;
            Vec3.Write(body.RockNormal, 3 * i, normal);
            Vec3.Write(body.RockPoint, 3 * i, centre - normal * radius);
        }

        private static void ApplyPerPart(
            Creature body, int index, ContactGrid grid, SolverConfig config, ref int[] scratch)
        {
            if (!grid.PerPart || !body.LinkContactActive)
            {
                throw new InvalidOperationException(
                    "Per-part contact was asked of a grid built over body spheres, or of a body " +
                    "built without link spheres. Both are set from SolverConfig.ContactPerPart, " +
                    "which has to be set before the first creature is built and not changed after.");
            }

            bool instrument = config.ContactInstrument;
            if (instrument) body.BeginContactStep();

            // The touch of the rock (SolverConfig.RockTouch): what the bed and reef blocks below
            // already compute, kept per link. It adds no arithmetic to any push.
            bool touch = body.RockTouched != null;

            int first = grid.FirstRow(index);

            for (int i = 0; i < body.Links; i++)
            {
                Vec3 centre = Vec3.Read(body.LinkContactCentre, 3 * i);
                double radius = body.LinkContactRadius[i];
                Vec3 velocity = Vec3.Read(body.LinkContactVelocity, 3 * i);
                double mass = body.Mass[i];

                Vec3 force = Vec3.Zero;
                double rockPush = 0;
                if (touch) body.RockTouched[i] = false;

                if (config.CreatureContact)
                {
                    int n = grid.NeighbourLinks(first + i, ref scratch);
                    grid.Watch.NoteCandidates(n);
                    for (int k = 0; k < n; k++)
                    {
                        int row = scratch[k];
                        Creature other = grid.At(grid.RowBody(row));
                        if (!other.ContactActive) continue;

                        int j = grid.RowLink(row);

                        Vec3 between = centre - Vec3.Read(other.LinkContactCentre, 3 * j);
                        if (config.Seamed) between = ShortWay(between, config);
                        double distance = between.Magnitude;
                        double penetration = radius + other.LinkContactRadius[j] - distance;
                        if (penetration <= 0) continue;

                        if (instrument) body.NoteOverlapPart(other.Id, i, j);

                        Vec3 normal = distance > 1e-9
                            ? between * (1.0 / distance)
                            : Vec3.UnitY;

                        double otherMass = other.Mass[j];
                        double reduced = mass * otherMass / (mass + otherMass);

                        double stiffness = reduced * config.ContactOmega * config.ContactOmega;
                        double damping = 2.0 * config.ContactDampingRatio * reduced * config.ContactOmega;

                        double approach = Vec3.Dot(
                            velocity - Vec3.Read(other.LinkContactVelocity, 3 * j), normal);

                        force += normal * ContactLaw.PairPush(
                            stiffness * penetration - damping * approach,
                            reduced, approach, config);
                    }
                }

                double bedStiffness = mass * config.ContactOmega * config.ContactOmega;
                double bedDamping = 2.0 * config.ContactDampingRatio * mass * config.ContactOmega;

                // The bed, on this link's sphere: the flat slab, or the height map's surface.
                if (config.Bed == null)
                {
                    double below = -config.WorldDepthMetres - (centre.Y - radius);
                    if (below > 0)
                    {
                        double up = ContactLaw.PairPush(
                            bedStiffness * below - bedDamping * velocity.Y,
                            mass, velocity.Y, config);

                        force += new Vec3(0, up, 0);

                        // D137. Not entered at 0, the recorded contact's bits.
                        if (config.BedFriction > 0)
                            force += ContactLaw.Friction(new Vec3(0, up, 0), velocity, mass, config);

                        if (instrument) body.TouchedBedOrGlass = true;
                        if (touch && up > 0) NoteRock(body, i, centre, radius, new Vec3(0, up, 0), ref rockPush);
                    }
                }
                else
                {
                    Vec3 rock = ContactBed.Push(
                        centre, radius, velocity, mass, config, bedStiffness, bedDamping);

                    if (rock.X != 0 || rock.Y != 0 || rock.Z != 0)
                    {
                        force += rock;
                        if (config.BedFriction > 0)
                            force += ContactLaw.Friction(rock, velocity, mass, config);
                        if (instrument) body.TouchedBedOrGlass = true;
                        if (touch) NoteRock(body, i, centre, radius, rock, ref rockPush);
                    }
                }

                // The reefs, on this link's sphere — the body sphere's rule, read on a link.
                if (config.Reefs != null || config.Coral != null)
                {
                    Vec3 reef = ContactReef.Push(
                        centre, radius, velocity, mass, config, bedStiffness, bedDamping);

                    if (reef.X != 0 || reef.Y != 0 || reef.Z != 0)
                    {
                        force += reef;
                        if (config.BedFriction > 0)
                            force += ContactLaw.Friction(reef, velocity, mass, config);
                        if (instrument) body.TouchedBedOrGlass = true;
                        if (touch) NoteRock(body, i, centre, radius, reef, ref rockPush);
                    }
                }


                // The glass, on this link's sphere. Not rock: it sets no touch.
                if (config.TankRadiusMetres > 0)
                {
                    double x = centre.X - config.TankAxisX;
                    double z = centre.Z - config.TankAxisZ;
                    double radial = System.Math.Sqrt(x * x + z * z);
                    double outside = radial + radius - config.TankRadiusMetres;

                    if (outside > 0)
                    {
                        Vec3 inward = radial > 1e-9
                            ? new Vec3(-x / radial, 0, -z / radial)
                            : Vec3.UnitX;

                        double approach = Vec3.Dot(velocity, inward);

                        Vec3 glass = inward * ContactLaw.PairPush(
                            bedStiffness * outside - bedDamping * approach, mass, approach, config);
                        force += glass;
                        if (config.BedFriction > 0)
                            force += ContactLaw.Friction(glass, velocity, mass, config);

                        if (instrument) body.TouchedBedOrGlass = true;
                    }
                }

                force = ContactLaw.BodyPush(force, mass, config);

                if (force.X != 0 || force.Y != 0 || force.Z != 0)
                {
                    Vec3 applied = body.Links == 1
                        ? force * (body.Mass[0] * (1.0 / body.TotalMass))
                        : force;

                    Vec3.Add(body.Fext, 6 * i + 3, applied);
                }
            }
        }
    }
}

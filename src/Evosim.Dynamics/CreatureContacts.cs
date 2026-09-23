using System;

namespace Evosim.Dynamics
{
    /// <summary>
    /// What one body noticed of the world while it was being stepped: which other bodies its
    /// sphere overlapped, and whether it was against the bed or the glass.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Written inside the parallel region by its owner and by nobody else.</b> Every field
    /// here belongs to one creature and is touched only on the thread stepping that creature, so
    /// the instrument adds no shared write to a step whose whole design is that there are none.
    /// The sums across bodies are taken afterwards, serially, in list order —
    /// <see cref="DynamicsWorld.CloseContactStep"/>.
    /// </para>
    /// <para>
    /// <b>Neighbours are remembered by id, not by index.</b> A birth or a death moves every
    /// index after it, so a held pair keyed on position would be a held pair with a different
    /// animal. The id is the creature's for as long as it lives, which is exactly the life of
    /// the question "did these two also touch last step".
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        private long[] _overlapIds = Array.Empty<long>();
        private int _overlapCount;

        private long[] _heldIds = Array.Empty<long>();
        private int _heldCount;

        /// <summary>Whether this body has a movable joint — the report's <c>jointed</c> test.</summary>
        /// <remarks>
        /// <c>Dof &gt; 0</c>, which is <c>CreatureInstance.TotalDof &gt; 0</c>: the same test the
        /// report's column and the contact instrument's jointed share use in the farm, so
        /// <c>pairs jnt %</c> cannot be a percentage of a different population from the column
        /// beside it.
        /// </remarks>
        public bool Jointed => Dof > 0;

        /// <summary>Whether this body's sphere was against the bed or the glass this step.</summary>
        public bool TouchedBedOrGlass { get; internal set; }

        /// <summary>How many other bodies this one's sphere overlapped this step.</summary>
        public int OverlapCount => _overlapCount;

        /// <summary>The id of one of them, <c>0 &lt;= at &lt; <see cref="OverlapCount"/></c>.</summary>
        /// <remarks>
        /// Ascending when the world's list is in ascending id order, which
        /// <see cref="DynamicsWorld.AddInIdOrder"/> keeps and
        /// <see cref="DynamicsWorld.CloseContactStep"/> checks: the contact grid returns
        /// neighbours by ascending index, and under that rule index order is id order.
        /// </remarks>
        public long OverlapId(int at) => _overlapIds[at];

        /// <summary>Empties the step's list. Called at the top of the body's own contact pass.</summary>
        internal void BeginContactStep()
        {
            _overlapCount = 0;
            TouchedBedOrGlass = false;
        }

        /// <summary>Records one overlapping neighbour.</summary>
        internal void NoteOverlap(long id)
        {
            if (_overlapCount == _overlapIds.Length)
            {
                Array.Resize(ref _overlapIds, _overlapIds.Length == 0 ? 8 : _overlapIds.Length * 2);
            }

            _overlapIds[_overlapCount++] = id;
        }

        // D114's link pair for each overlapping body: this body's part, then the other's.
        // Filled only by NoteOverlapPart, so a per-body world never allocates it.
        private int[] _overlapParts = Array.Empty<int>();

        /// <summary>
        /// This body's part in the first overlapping link pair with neighbour <paramref name="at"/>,
        /// under per-part contact; -1 under the body sphere.
        /// </summary>
        public int OverlapPart(int at) => _overlapParts.Length > 2 * at ? _overlapParts[2 * at] : -1;

        /// <summary>The neighbour's part in that pair; -1 under the body sphere.</summary>
        public int OverlapOtherPart(int at) =>
            _overlapParts.Length > 2 * at + 1 ? _overlapParts[2 * at + 1] : -1;

        /// <summary>
        /// Records one overlapping link pair under per-part contact (D114): the neighbour once,
        /// however many of its parts touch, with the first link pair seen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The list stays one entry a body and ascending by id</b>, which is what the census
        /// counts and what <see cref="DynamicsWorld.CloseContactStep"/>'s held test and event list
        /// assume: a pair of bodies touching at three pairs of parts is one overlapping pair, so
        /// <c>overlaps</c> and <c>ovl/body</c> keep their meaning across the two models.
        /// </para>
        /// <para>
        /// <b>First seen is a fixed order.</b> The contact pass walks this body's links in index
        /// order and, for each, the grid's candidates in ascending (body, link) order, so the pair
        /// kept is the same at any thread count. A neighbour first met through a later link can
        /// have a lower id than one already listed, so it is inserted in its place rather than
        /// appended; the list is tens of entries at most, for <see cref="HeldWith"/>'s reason.
        /// </para>
        /// </remarks>
        internal void NoteOverlapPart(long id, int part, int otherPart)
        {
            int at = _overlapCount;
            while (at > 0 && _overlapIds[at - 1] > id) at--;
            if (at > 0 && _overlapIds[at - 1] == id) return;

            if (_overlapCount == _overlapIds.Length)
            {
                Array.Resize(ref _overlapIds, _overlapIds.Length == 0 ? 8 : _overlapIds.Length * 2);
            }
            if (_overlapParts.Length < 2 * _overlapIds.Length)
            {
                Array.Resize(ref _overlapParts, 2 * _overlapIds.Length);
            }

            for (int k = _overlapCount; k > at; k--)
            {
                _overlapIds[k] = _overlapIds[k - 1];
                _overlapParts[2 * k] = _overlapParts[2 * k - 2];
                _overlapParts[2 * k + 1] = _overlapParts[2 * k - 1];
            }

            _overlapIds[at] = id;
            _overlapParts[2 * at] = part;
            _overlapParts[2 * at + 1] = otherPart;
            _overlapCount++;
        }

        /// <summary>Whether this body overlapped <paramref name="id"/> on the <i>previous</i> step.</summary>
        /// <remarks>
        /// A linear walk of a list that is tens of entries at most, for the reason
        /// <c>ContactGrid.Neighbours</c> sorts by insertion: a body's neighbourhood is small and
        /// a binary search over it buys nothing but a chance to be wrong about the ordering.
        /// </remarks>
        internal bool HeldWith(long id)
        {
            for (int i = 0; i < _heldCount; i++) if (_heldIds[i] == id) return true;
            return false;
        }

        /// <summary>
        /// Makes this step's overlaps the ones the next step will call held. Serial, between
        /// steps, for <see cref="CommitContactSphere"/>'s reason.
        /// </summary>
        internal void CommitOverlaps()
        {
            if (_heldIds.Length < _overlapCount) _heldIds = new long[_overlapIds.Length];
            Array.Copy(_overlapIds, _heldIds, _overlapCount);
            _heldCount = _overlapCount;
        }
    }
}

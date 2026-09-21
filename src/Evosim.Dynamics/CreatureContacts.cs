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

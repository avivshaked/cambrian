using System;
using System.Collections.Generic;

namespace Evosim.Dynamics
{
    /// <summary>One overlapping pair of bodies on one step, lower id first.</summary>
    /// <remarks>
    /// <b>An observation, not a design.</b> The bite, when it is built, will consume a list of
    /// these on the main thread between steps; what it does with one is its own business and
    /// nothing here anticipates it. The pair carries what the instrument already knew — whether
    /// either side is jointed, and whether the two were overlapping on the previous step — so a
    /// consumer does not have to ask the world again for state that has already moved.
    /// </remarks>
    public readonly struct OverlapPair
    {
        public OverlapPair(long a, long b, bool jointed, bool held)
        {
            A = a;
            B = b;
            Jointed = jointed;
            Held = held;
        }

        /// <summary>The lower of the two creature ids.</summary>
        public readonly long A;

        /// <summary>The higher of the two creature ids.</summary>
        public readonly long B;

        /// <summary>Whether either side has a movable joint.</summary>
        public readonly bool Jointed;

        /// <summary>Whether the same two were overlapping on the previous step.</summary>
        public readonly bool Held;

        public override string ToString() =>
            System.FormattableString.Invariant($"({A},{B}){(Jointed ? " jointed" : "")}{(Held ? " held" : "")}");
    }

    /// <summary>
    /// The contact instrument, redefined on sphere overlaps, and the two list operations a farm
    /// loop needs between steps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The names are new because the numbers are not comparable.</b> The farm's
    /// <c>contactPairs</c>, <c>contactPairsJointed</c>, <c>contactPairsPersistent</c>,
    /// <c>contactBodies</c> and <c>floor con</c> count what PhysX reported: contact <i>manifolds
    /// between colliders</i>, so a four-part animal lying across another can report a dozen
    /// pairs and a body folded on itself reports twenty of its own. This counts overlapping
    /// <i>bounding spheres between creatures</i>, one per pair, and a body cannot overlap itself
    /// at all. A run's <c>pairs/body</c> under the two engines is a different measurement wearing
    /// the same clothes, which is why <c>overlapPairs</c> is not called <c>contactPairs</c>.
    /// </para>
    /// <para>
    /// <b>Summed serially in list order, after the parallel phase.</b> Each body writes only its
    /// own overlap list while it is being stepped; the totals, the held test and the event list
    /// are all built here, in one pass over the creatures in ascending id order, so the numbers
    /// and the list are identical at any thread count.
    /// </para>
    /// <para>
    /// <b>A pair is counted from its lower id.</b> Both sides record the overlap — the test is
    /// symmetric to the bit, the two centres differing only in sign — so counting from one side
    /// only is exact, and it is what makes the event list come out sorted without a sort.
    /// </para>
    /// </remarks>
    public sealed partial class DynamicsWorld
    {
        private readonly List<OverlapPair> _overlaps = new List<OverlapPair>();

        /// <summary>Overlapping creature pairs, summed over every step so far.</summary>
        public long OverlapPairs { get; private set; }

        /// <summary>Of those, the ones with a jointed body on at least one side.</summary>
        public long OverlapPairsJointed { get; private set; }

        /// <summary>Of those, the ones that were also overlapping on the previous step.</summary>
        public long OverlapPairsHeld { get; private set; }

        /// <summary>Distinct bodies overlapping another body, summed over every step so far.</summary>
        public long OverlapBodies { get; private set; }

        /// <summary>Distinct bodies against the bed or the glass, summed over every step so far.</summary>
        public long BedOrGlassBodies { get; private set; }

        /// <summary>The same five, for the step just taken.</summary>
        public long OverlapPairsThisStep { get; private set; }

        /// <summary>The same five, for the step just taken.</summary>
        public long OverlapPairsJointedThisStep { get; private set; }

        /// <summary>The same five, for the step just taken.</summary>
        public long OverlapPairsHeldThisStep { get; private set; }

        /// <summary>The same five, for the step just taken.</summary>
        public long OverlapBodiesThisStep { get; private set; }

        /// <summary>The same five, for the step just taken.</summary>
        public long BedOrGlassBodiesThisStep { get; private set; }

        /// <summary>
        /// The step's overlapping pairs, lower id first and ascending — the list a bite would
        /// consume. Empty unless <see cref="SolverConfig.ContactEvents"/> is set.
        /// </summary>
        /// <remarks>
        /// Valid until the next <see cref="Step"/>, which clears it. It is the world's own
        /// buffer and not a copy, so a consumer that wants to keep it past the next step copies
        /// it.
        /// </remarks>
        public IReadOnlyList<OverlapPair> Overlaps => _overlaps;

        /// <summary>
        /// Adds a body, keeping the list in ascending id order.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The rule that keeps identity across births and deaths.</b> The list is stepped in
        /// list order and every sum across bodies is taken in list order, so the trajectory
        /// depends on the order the list is in — not on the thread count, but on the order. Two
        /// runs that add and remove the same creatures in the same sequence therefore agree only
        /// if the list order is a function of the population and not of its history. Ascending
        /// id is that function: whatever sequence of births and deaths produced a set of
        /// creatures, the list holding them is the same list.
        /// </para>
        /// <para>
        /// <b><see cref="Add"/> is the same operation when ids ascend</b>, which is every caller
        /// on file — the bench numbers its bodies 0 upward and the tests do the same. It is kept
        /// as it is so that nothing recorded moves; a farm loop, whose ids ascend but whose list
        /// loses bodies from the middle, calls this.
        /// </para>
        /// </remarks>
        public Creature AddInIdOrder(Creature body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            int at = IndexOrInsertionPoint(body.Id, out bool present);

            if (present)
            {
                throw new ArgumentException(
                    FormattableString.Invariant(
                        $"Creature {body.Id} is already in this world. An id names one animal ") +
                    "for as long as it lives, and the contact instrument's held pairs are keyed " +
                    "on it.",
                    nameof(body));
            }

            Add(body);

            if (at != _creatures.Count - 1)
            {
                _creatures.RemoveAt(_creatures.Count - 1);
                _creatures.Insert(at, body);
            }

            return body;
        }

        /// <summary>Takes a body out of the world. False when no body has that id.</summary>
        /// <remarks>
        /// The per-index scratch the contact grid writes into is not moved with it: it holds
        /// nothing between steps — <c>ContactGrid.Neighbours</c> refills it from the first
        /// element every time it is asked — so after a removal slot <i>i</i> simply belongs to
        /// the body that has moved down into it.
        /// </remarks>
        public bool Remove(long id)
        {
            int at = IndexOrInsertionPoint(id, out bool present);
            if (!present) return false;

            _creatures.RemoveAt(at);
            return true;
        }

        /// <summary>Takes a body out of the world by identity.</summary>
        public bool Remove(Creature body) =>
            body != null && Remove(body.Id);

        /// <summary>The body with an id, or null.</summary>
        public Creature ById(long id)
        {
            int at = IndexOrInsertionPoint(id, out bool present);
            return present ? _creatures[at] : null;
        }

        /// <summary>
        /// Where an id is, or where it would go. Binary search, which is exact only while the
        /// list is in ascending id order — <see cref="AddInIdOrder"/>'s rule.
        /// </summary>
        private int IndexOrInsertionPoint(long id, out bool present)
        {
            int lo = 0, hi = _creatures.Count - 1;

            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                long at = _creatures[mid].Id;

                if (at == id) { present = true; return mid; }
                if (at < id) lo = mid + 1; else hi = mid - 1;
            }

            present = false;
            return lo;
        }

        /// <summary>
        /// The step's five counters and its event list, taken serially once every body has
        /// been stepped and every sphere committed.
        /// </summary>
        /// <remarks>
        /// <b>A body the solver has lost is not in the instrument.</b> It stopped being stepped,
        /// so it never emptied its own list and what it holds is the last step it survived; and
        /// counting a pair from the living side only, while the dead side keeps a stale list,
        /// would make the count depend on which of the two had the lower id. Both sides are
        /// tested for <see cref="Creature.Alive"/> instead, which is settled by the time this
        /// runs.
        /// </remarks>
        internal void CloseContactStep()
        {
            _overlaps.Clear();

            OverlapPairsThisStep = 0;
            OverlapPairsJointedThisStep = 0;
            OverlapPairsHeldThisStep = 0;
            OverlapBodiesThisStep = 0;
            BedOrGlassBodiesThisStep = 0;

            if (!Config.ContactInstrument) return;

            int count = _creatures.Count;
            bool events = Config.ContactEvents;

            for (int i = 1; i < count; i++)
            {
                if (_creatures[i - 1].Id < _creatures[i].Id) continue;

                throw new InvalidOperationException(
                    FormattableString.Invariant(
                        $"The world's creature list is not in ascending id order at {i - 1}: ") +
                    FormattableString.Invariant(
                        $"{_creatures[i - 1].Id} then {_creatures[i].Id}. ") +
                    "The contact instrument counts a pair from its lower id and finds the other " +
                    "side by binary search, and the farm's identity rests on the list order " +
                    "being a function of the population — add with AddInIdOrder and remove with " +
                    "Remove.");
            }

            for (int i = 0; i < count; i++)
            {
                Creature a = _creatures[i];
                if (!a.Alive) continue;

                if (a.TouchedBedOrGlass) BedOrGlassBodiesThisStep++;

                bool touching = false;

                for (int k = 0; k < a.OverlapCount; k++)
                {
                    long id = a.OverlapId(k);

                    Creature b = ById(id);
                    if (b == null || !b.Alive) continue;

                    touching = true;
                    if (id <= a.Id) continue;   // counted from the lower side

                    bool jointed = a.Jointed || b.Jointed;
                    bool held = a.HeldWith(id);

                    OverlapPairsThisStep++;
                    if (jointed) OverlapPairsJointedThisStep++;
                    if (held) OverlapPairsHeldThisStep++;

                    if (events) _overlaps.Add(new OverlapPair(a.Id, id, jointed, held));
                }

                if (touching) OverlapBodiesThisStep++;
            }

            for (int i = 0; i < count; i++) _creatures[i].CommitOverlaps();

            OverlapPairs += OverlapPairsThisStep;
            OverlapPairsJointed += OverlapPairsJointedThisStep;
            OverlapPairsHeld += OverlapPairsHeldThisStep;
            OverlapBodies += OverlapBodiesThisStep;
            BedOrGlassBodies += BedOrGlassBodiesThisStep;
        }
    }
}

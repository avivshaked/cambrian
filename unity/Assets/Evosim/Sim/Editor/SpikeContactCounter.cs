using UnityEngine;

namespace Evosim.Sim.EditorTools
{
    /// <summary>
    /// Counts contact callbacks on one part — the contact instrument for
    /// <see cref="SharedSpaceSpike"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One component per part, one static counter for the whole scene.</b> The question the
    /// spike asks is how many contacts a population generates per step, not which part had them,
    /// so a per-instance field would be 4,000 objects of bookkeeping for a number that is summed
    /// immediately. The statics are reset by the harness before each measured phase.
    /// </para>
    /// <para>
    /// <b>These read zero in a non-playing editor, and that is a measured fact, not a
    /// suspicion.</b> The spike steps physics by hand from an <c>-executeMethod</c> entry with the
    /// Editor not in play mode, and Unity does not dispatch MonoBehaviour collision messages
    /// there — <see cref="ExecuteAlways"/> is on the class and does not change it. The spike's
    /// <c>contact-check</c> cell is what settled it: at 41.9 bodies per cubic metre the solver
    /// spent 1.57 ms a step against 0.19 ms for the same population spread out, which is contact
    /// work by definition, while these counters reported nothing and <c>Physics.ContactEvent</c>
    /// reported 767 pairs a step.
    /// </para>
    /// <para>
    /// <b>Kept anyway, because the counter that is asked for and reads zero is worth more than a
    /// missing one.</b> The spec for this spike asked for a per-part <c>OnCollisionStay</c>
    /// counter; it is here, it is attached, it is reported in its own column, and the fact that
    /// the column is empty is the finding. If the spike is ever moved into play mode the
    /// cross-check comes back for free.
    /// </para>
    /// <para>
    /// <b>Both Enter and Stay are counted.</b> The first step of a contact reports Enter and not
    /// Stay, so a Stay-only counter under-reports by exactly the number of new contacts each step
    /// — which in a dense, fast-moving population is not a rounding error. They are also counted
    /// separately, because Enter per step is the churn rate and Stay per step is the standing
    /// load, and those answer different halves of the density question.
    /// </para>
    /// <para>
    /// <b>Every callback is one collider's view of one pair</b>, so both parts of a touching pair
    /// report and the callback count is twice the pair count. The harness reports pairs; this
    /// counts callbacks, and the halving happens in one place there.
    /// </para>
    /// </remarks>
    [ExecuteAlways]
    public sealed class SpikeContactCounter : MonoBehaviour
    {
        /// <summary>OnCollisionEnter + OnCollisionStay calls since the last <see cref="Reset"/>.</summary>
        public static long Callbacks;

        /// <summary>OnCollisionEnter calls alone — new contacts, the churn rate.</summary>
        public static long Enters;

        /// <summary>Contact points reported across every callback.</summary>
        public static long ContactPoints;

        public static void Reset()
        {
            Callbacks = 0;
            Enters = 0;
            ContactPoints = 0;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Enters++;
            Callbacks++;
            ContactPoints += collision.contactCount;
        }

        private void OnCollisionStay(Collision collision)
        {
            Callbacks++;
            ContactPoints += collision.contactCount;
        }
    }
}

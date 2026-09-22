using System;

namespace Evosim.Dynamics
{
    /// <summary>
    /// A body whose <i>plan</i> changed: the module gene's rebuild — D106 item 2, rule 7 of
    /// <c>logbook/specs/module-gene-spec.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is not <c>Resize</c>.</b> <see cref="Resize(Evosim.Core.Phenotype)"/> refuses a
    /// changed part count by design — "growth changes a body's size and never its plan" — and
    /// everything it writes is indexed by link, so there is nowhere for a link that has just
    /// appeared to come from. A module add or drop therefore builds a new <see cref="Creature"/>
    /// the way a birth does and then moves the state across; this method is the second half.
    /// </para>
    /// <para>
    /// <b>What is carried, and what is not.</b> The root's pose and velocity, because the body is
    /// the same body standing where it stood; every surviving part's joint coordinates, rates and
    /// ball rotation, because those are what the animal was doing; and the brain's recurrent
    /// state and clock, because a plant that grew a leaf has not forgotten how to swim. Not
    /// carried: the drive's smoothing history, which is a ten-sample window indexed by degree of
    /// freedom and would have to be remapped through a second ordering for a tenth of a second of
    /// torque — it refills within a metabolic step, and the two limiter counters are drained by
    /// the harness before the rebuild rather than lost.
    /// </para>
    /// <para>
    /// <b>The new parts start at rest relative to their parent</b>, which is rule 7's own
    /// wording: their joint coordinates are the zeros the constructor left, so the module hangs
    /// where the developer put it and the kinematics walk it outward from the root.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>
        /// The plan revision the harness has built this body at — <c>Organism.PlanRevision</c>.
        /// </summary>
        /// <remarks>
        /// <see cref="AppliedBodyFraction"/>'s twin, and for its reason: the growth step compares
        /// the organism's revision with this, rebuilds on a difference, and writes the new value
        /// back. It lives on the solver's body because the port has no <c>Body</c> class to hang
        /// it on. 0 is a body that has never changed its plan, which is every body in the record.
        /// </remarks>
        public int AppliedPlanRevision { get; set; }

        /// <summary>
        /// Takes over the state of the body this one replaces, part by part.
        /// </summary>
        /// <param name="previous">The body built at the old plan. Left untouched.</param>
        /// <param name="previousPartOfPart">
        /// For each of this body's links, the index of the same part in
        /// <paramref name="previous"/>, or -1 where there is none — the developer's own match
        /// (<c>Developer.Develop</c>'s part paths). A length that does not cover this body is
        /// refused rather than read short, because a short map would silently leave the tail of a
        /// body at rest while claiming to have carried it.
        /// </param>
        /// <remarks>
        /// <b>Call it between steps</b>, never inside the parallel phase: it commits the contact
        /// sphere, which is the one piece of a creature other creatures read, and committing
        /// inside the parallel phase is what made the spike's digest depend on the thread count.
        /// </remarks>
        public void AdoptStateFrom(Creature previous, int[] previousPartOfPart)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (previousPartOfPart == null) throw new ArgumentNullException(nameof(previousPartOfPart));

            if (previousPartOfPart.Length < Links)
            {
                throw new ArgumentException(
                    $"The map covers {previousPartOfPart.Length} parts and this body has {Links}. " +
                    "A short map would leave the end of the body at rest and report that its " +
                    "state had been carried.",
                    nameof(previousPartOfPart));
            }

            BasePosition = previous.BasePosition;
            BaseRotation = previous.BaseRotation;

            // The root's own motion. Every other link's is re-derived by the kinematics pass from
            // this and from the joint rates, which is what an articulation's velocity is.
            Vec3.Write(Velocity, 0, Vec3.Read(previous.Velocity, 0));
            Vec3.Write(Spin, 0, Vec3.Read(previous.Spin, 0));

            for (int i = 0; i < Links; i++)
            {
                int from = previousPartOfPart[i];
                if (from < 0 || from >= previous.Links) continue;

                QuatD.Write(BallRotation, 4 * i, QuatD.Read(previous.BallRotation, 4 * from));

                int dof = DofCount[i];
                if (dof == 0 || previous.DofCount[from] != dof) continue;

                int to = DofStart[i];
                int at = previous.DofStart[from];
                if (to < 0 || at < 0) continue;

                Array.Copy(previous.Q, at, Q, to, dof);
                Array.Copy(previous.Qd, at, Qd, to, dof);
            }

            Brain.CarryStateFrom(previous.Brain, previousPartOfPart);

            Kinematics.Refresh(this);
            RefreshContactSphere();
            CommitContactSphere();
        }
    }
}

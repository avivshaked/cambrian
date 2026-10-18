using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Package B at the world's level: the ledger totals, summed in creature order.
    /// </summary>
    /// <remarks>
    /// <b>The water pass that stood here is inside each body's step from 2026-10-03</b>
    /// (<c>DynamicsWorld.StepOne</c>, <c>logbook/specs/farm-physics-speed-read.md</c> P2). It was a
    /// <c>Parallel.For</c> of its own between the grid build and the bodies' phase, under the same
    /// pin; a body's sample reads that body and the field alone and only its own drag pass reads
    /// the result, so moving it into the body's step moves no number and saves a fork-join and a
    /// cold pass over every body's arrays.
    /// </remarks>
    public sealed partial class DynamicsWorld
    {
        /// <summary>
        /// Unsigned mechanical work over the whole population, joules — the sum the report's
        /// work column is built from. Added in creature order, never inside the step.
        /// </summary>
        public double MechanicalWorkJoules()
        {
            double total = 0;
            for (int i = 0; i < _creatures.Count; i++) total += _creatures[i].MechanicalWorkJoules;
            return total;
        }

        /// <summary>Signed joint work over the population, joules — the energy balance's term.</summary>
        public double SignedWorkJoules()
        {
            double total = 0;
            for (int i = 0; i < _creatures.Count; i++) total += _creatures[i].SignedWorkJoules;
            return total;
        }

        /// <summary>
        /// Energy the water has taken from the population, joules — what the energy audit reads.
        /// </summary>
        public double DissipatedJoules()
        {
            double total = 0;
            for (int i = 0; i < _creatures.Count; i++) total += _creatures[i].DissipatedJoules;
            return total;
        }

        /// <summary>The passive joint torques' work, joules. Diagnostic; see the ledger's remarks.</summary>
        public double PassiveJointWorkJoules()
        {
            double total = 0;
            for (int i = 0; i < _creatures.Count; i++) total += _creatures[i].PassiveJointWorkJoules;
            return total;
        }

        /// <summary>
        /// Drains every body's dissipation and returns the total since the last drain, joules,
        /// added in creature order.
        /// </summary>
        public double DrainDissipated()
        {
            double total = 0;
            for (int i = 0; i < _creatures.Count; i++) total += _creatures[i].DrainDissipated();
            return total;
        }
    }
}

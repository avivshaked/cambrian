namespace Evosim.Dynamics
{
    /// <summary>
    /// Packages B and C at the world's level: the serial water pass that runs before the
    /// parallel phase, and the ledger totals, summed in creature order.
    /// </summary>
    public sealed partial class DynamicsWorld
    {
        /// <summary>
        /// Samples the current into every living body, in creature order, on one thread.
        /// </summary>
        /// <remarks>
        /// Called from <see cref="Step"/> between the contact grid's build and the parallel
        /// phase, so the poses it reads are the ones the step starts from — the farm's gather
        /// phase, in the same place in the step. <see cref="Water.Sample"/>'s remarks say why it
        /// is not inside the parallel region: <c>CurrentField</c> memoises its instants.
        /// </remarks>
        private void SampleWater()
        {
            if (Config.Current == null) return;

            for (int i = 0; i < _creatures.Count; i++)
            {
                Creature body = _creatures[i];
                if (!body.Alive) continue;
                Water.Sample(body, Config, ElapsedSeconds);
            }
        }

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

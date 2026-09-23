using System.Threading.Tasks;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Packages B and C at the world's level: the water pass that runs before the parallel
    /// phase, and the ledger totals, summed in creature order.
    /// </summary>
    public sealed partial class DynamicsWorld
    {
        /// <summary>
        /// Samples the current into every living body, across the world's threads, with the field
        /// pinned at the step's clock.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Called from <see cref="Step"/> between the contact grid's build and the parallel
        /// phase, so the poses it reads are the ones the step starts from — the farm's gather
        /// phase, in the same place in the step. Each body's sample reads that body alone and the
        /// field, so which thread takes it changes nothing.
        /// </para>
        /// <para>
        /// <b>Until 2026-09-23 this loop was serial</b>, because <see cref="CurrentField"/>
        /// memoises the instants a call touches and a hit reassigned fields the samplers read:
        /// two threads sampling one field would have seen each other's slot. At 1,200 bodies on
        /// five threads the pass was a third of a step's wall, more than the solver's own parallel
        /// phase. The field now hands a sampler its instant as a value, a pin
        /// (<see cref="CurrentField.PinInstant"/>) fills the slots a step's clock needs and makes
        /// every later lookup a read, and the bed's one-entry memo is a thread's own, so the pass
        /// splits across the same threads as the bodies. The bits are unchanged: a slot's
        /// contents are a pure function of the clock, and <c>WaterThreadIdentityTests</c> holds
        /// the digest across thread counts with the water on.
        /// </para>
        /// </remarks>
        private void SampleWater(ParallelOptions options)
        {
            CurrentField current = Config.Current;
            if (current == null) return;

            // A no-op for water the pin has nothing to fill for (still, or the rolls, whose
            // sampler is plain trigonometry with no memo); a refusal if something else has the
            // field pinned at another clock, which nothing between steps should.
            current.PinInstant(ElapsedSeconds);

            try
            {
                double seconds = ElapsedSeconds;
                SolverConfig config = Config;

                Parallel.For(0, _creatures.Count, options, i =>
                {
                    Creature body = _creatures[i];
                    if (!body.Alive) return;
                    Water.Sample(body, config, seconds);
                });
            }
            finally
            {
                current.UnpinInstant();
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

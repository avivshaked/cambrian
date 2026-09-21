namespace Evosim.Dynamics
{
    /// <summary>
    /// The two bounds that turn the soft push from a spring that can throw a body into one that
    /// cannot: a contact never pulls, and neither one pair nor a whole pile may change a body's
    /// motion by more than a stated speed in one step.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the plain spring-damper was not enough, in three parts.</b>
    /// </para>
    /// <para>
    /// <i>It could pull.</i> <c>k p − c a</c> goes negative whenever a pair is separating faster
    /// than <c>k p / c</c>, which at critical damping is <c>omega p / 2</c> — a centimetre of
    /// overlap and a tenth of a metre a second was enough. An attractive force computed from the
    /// previous step's velocity is a textbook energy pump: it pulls the pair back, the pair
    /// arrives faster, it pulls harder. A contact between two solid bodies has no business
    /// pulling at all, and clamping at zero costs one comparison.
    /// </para>
    /// <para>
    /// <i>One deep overlap is kilonewtons.</i> The spring answers a half-metre overlap between a
    /// 19 kg body and a 260 kg one with about seven kilonewtons, which is four hundred times the
    /// lighter body's weight in water. The farm never sees this because PhysX has
    /// <c>maxDepenetrationVelocity</c> and because the placer refuses a crowded spot; a solver
    /// that is handed one anyway should push the pair apart at a walking pace and take as many
    /// steps as that takes.
    /// </para>
    /// <para>
    /// <i>Twenty stable springs are not a stable spring.</i> Each pair is integrated explicitly
    /// and the bound is on the sum: a body with <c>N</c> neighbours feels <c>N</c> times the
    /// stiffness and <c>N</c> times the damping, so <c>c dt / m &lt; 2</c> fails at a crowd
    /// however comfortable one pair is. Round 42's worlds are crowds — about nine hundred bodies
    /// in the top ten metres of a 26 m tank, every newborn dropped within five metres of its
    /// parent — so this is the normal case and not an edge.
    /// </para>
    /// <para>
    /// <b>What the bounds cost.</b> Nothing at all while they are slack, which is every contact
    /// between bodies a placer admitted: a body resting on the bed penetrates it by half a
    /// millimetre and is pushed at a fraction of a millimetre a second. Where they bind, the
    /// per-body cap is the one place a pair stops being exactly equal and opposite, because two
    /// bodies in different crowds cap differently. Momentum is exact everywhere else.
    /// </para>
    /// <para>
    /// <b>Both read only what the step already has.</b> The pair bound is a function of the
    /// reduced mass, the approach speed and the step, all of them from state frozen before the
    /// parallel phase; the body bound is a function of the body's own total and its own mass. No
    /// creature reads another creature's current state and no sum is shared, so the thread count
    /// is as invisible here as everywhere else.
    /// </para>
    /// </remarks>
    public static class ContactLaw
    {
        /// <summary>
        /// One contact's push along its normal, N — never negative, and never more than the
        /// separation speed the world allows.
        /// </summary>
        /// <param name="raw">The spring-damper's own answer, <c>k p − c a</c>.</param>
        /// <param name="reducedMass">
        /// The pair's reduced mass, kg, or the body's own mass against the bed or the glass,
        /// which is the same quantity with the world's mass taken as infinite.
        /// </param>
        /// <param name="approach">
        /// The pair's normal velocity, m/s, positive when they are separating — the same sign
        /// convention the damper's term is written in.
        /// </param>
        public static double PairPush(
            double raw, double reducedMass, double approach, SolverConfig config)
        {
            if (!(raw > 0)) return 0;

            double cap = config.MaxSeparationSpeed;
            if (!(cap > 0)) return raw;

            // What the step may add to the pair's separating speed: the cap, plus back whatever
            // it is closing at. A pair already flying apart is allowed the cap and no more; a
            // pair closing at 3 m/s may be stopped and then separated at the cap.
            double allowed = cap - (approach < 0 ? approach : 0);
            double most = reducedMass * allowed / config.StepSeconds;

            return raw < most ? raw : most;
        }

        /// <summary>
        /// A body's whole contact force, N, bounded so that one step of it cannot change the
        /// body's speed by more than the world allows.
        /// </summary>
        public static Vec3 BodyPush(Vec3 force, double mass, SolverConfig config)
        {
            double cap = config.MaxContactSpeedChange;
            if (!(cap > 0)) return force;

            double most = mass * cap / config.StepSeconds;
            double magnitude = force.Magnitude;

            return magnitude > most ? force * (most / magnitude) : force;
        }
    }
}

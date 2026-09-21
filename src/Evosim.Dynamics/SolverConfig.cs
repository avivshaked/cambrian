using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Everything the solver reads about the world, taken from a run's <see cref="RunConfig"/>
    /// and held in double precision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A copy rather than a reference, and deliberately a narrow one.</b> The spike has no
    /// economy, no field and no run directory (the spec's "what it is not"), so only the terms
    /// that move a body are carried across. Anything the farm reads and this does not is listed
    /// in the report as not ported rather than silently defaulted.
    /// </para>
    /// </remarks>
    public sealed partial class SolverConfig
    {
        // ---- fluid, term for term from FluidConfig

        public double Density = 1000.0;
        public double DragCoefficient = 1.5;
        public int PanelsPerAxis = 2;
        public double AddedMassCoefficient;
        public double FluidAccelerationCoefficient;
        public double TissueExcessDensity;
        public double NeutralBodyVolume;
        public double SurfaceRestoringFraction;

        /// <summary>
        /// Tissue density, kg/m3 — <c>PhenotypeBuilder.DensityKgPerM3</c>. Not a tunable in the
        /// farm either: the buoyancy term divides by the same constant the mass was assigned
        /// with, and passing the water's density in its place would rescale every body's weight.
        /// </summary>
        public double TissueDensity = 1000.0;

        /// <summary>The minimum mass a link may carry, kg — <c>Mathf.Max(0.001f, ...)</c>.</summary>
        public double MinimumLinkMass = 0.001;

        public double GravityMetresPerSecondSquared = 9.81;

        // ---- world shape

        public double WorldDepthMetres = 60.0;

        /// <summary>Whether the world has a solid bed, which switches off D077's bottom half.</summary>
        public bool FloorIsSolid = true;

        /// <summary>Radius of the cylindrical glass wall, metres. 0 is no wall.</summary>
        public double TankRadiusMetres;

        // ---- the current, package C

        /// <summary>
        /// Water that moves, or null for still water — <c>RunConfig.Current</c>, the same
        /// instance the world holds.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A reference and not a copy</b>, unlike everything else here, because the field is
        /// not a number: it carries the box, the seed and the bed that <c>World</c>'s constructor
        /// gave it through <c>SetBox</c>, and a second copy would be a second draw of the water.
        /// The caller therefore hands over a field that has already been told its box — a field
        /// that has not been refuses to answer, which is the failure the farm wants.
        /// </para>
        /// <para>
        /// <b>It is sampled on one thread</b>, in <c>DynamicsWorld.SampleWater</c>. The field
        /// memoises the instants a call touches, so it is not safe to share across the parallel
        /// phase; <see cref="Water.Sample"/> says so at length.
        /// </para>
        /// </remarks>
        public CurrentField Current;

        /// <summary>
        /// D061's horizontal patches, K — what <see cref="CurrentMode.Rolls"/> reads in place of
        /// a horizontal position. 1 is a world with no horizontal structure.
        /// </summary>
        public int PatchCount = 1;

        /// <summary>
        /// D100's hold, seconds — <c>FluidConfig.WaterHoldSeconds</c>. 0 is the per-link
        /// sampling every run before D100 recorded, and is what a config at 0 replays.
        /// </summary>
        public double WaterHoldSeconds;

        // ---- drive and limiter, from EffectorDriver

        /// <summary>The step the drivers are conditioned at. Gates both stabilisers.</summary>
        public double StepSeconds = 0.01;

        /// <summary><c>RunConfig.DriveLimitAtEveryStep</c>.</summary>
        public bool DriveLimitAtEveryStep;

        public double MaxJointAngularVelocity = 30.0;

        /// <summary>
        /// <c>ArticulationDrive.damping</c>, 1 N·m·s/rad in <c>PhenotypeBuilder.MakeDrive</c> —
        /// "small and non-zero so undriven joints settle instead of ringing".
        /// </summary>
        public double JointDriveDamping = 1.0;

        // ---- joint limits, which PhysX solves as hard constraints and this does not

        /// <summary>
        /// Undamped natural frequency of the joint-limit spring, as a fraction of 1/dt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The spring's stiffness per degree of freedom is <c>I * omega^2</c> and its damping
        /// <c>2 * zeta * I * omega</c>, where <c>I</c> is the two-body reduced inertia about the
        /// joint axis through the anchor — so a limit feels the same on a heavy link and a light
        /// one, which a fixed stiffness would not.
        /// </para>
        /// <para>
        /// <b>1.5, which is three times what an explicit step carried and not six.</b> A penalty
        /// spring integrated explicitly is stable only while <c>omega * dt &lt; 2</c>, and the
        /// first build sat at 0.5 for that reason. What a penalty stop is worth, though, is set
        /// by how far it lets a joint past the stop under a constant drive — <c>m / k</c> — and
        /// 0.5 let round 42's genome 1 stand 0.0907 rad past a limit PhysX holds exactly. The
        /// limit term is now carried implicitly in the inward pass
        /// (<see cref="Creature.LimitImplicit"/>), which is unconditionally stable in <c>k</c>,
        /// so this is chosen for the overshoot it allows: 150 rad/s at dt 0.01 is nine times the
        /// stiffness and leaves that joint 0.0101 rad past its stop.
        /// </para>
        /// <para>
        /// <b>Why not stiffer, when nothing stops it.</b> At 3.0 the overshoot is better again
        /// (0.0034 rad) and the body is worse: a joint that stiff chatters across its stop
        /// instead of resting on it, the limit damper bleeds the chatter on every step, and what
        /// it takes comes out of the body's own motion. Genome 1's attitude after 60 s went from
        /// 100 degrees of turn at 0.5, to 110 at 1.5, to 10 at 3.0 against PhysX's 136 — and at
        /// 3.0 it also became sensitive to <see cref="JointLimitDampingRatio"/>, which swung it
        /// from 10 degrees to 166. A setting whose answer depends that much on a number with no
        /// physical meaning is the wrong setting, whatever its overshoot reads.
        /// </para>
        /// </remarks>
        public double JointLimitOmegaTimesStep = 1.5;

        public double JointLimitDampingRatio = 1.0;

        // ---- contact, which the farm gives to PhysX and this approximates

        /// <summary>Undamped natural frequency of the soft contact spring, rad/s.</summary>
        /// <remarks>
        /// Absolute rather than a fraction of 1/dt, so a screen at dt 0.02 and a confirmation at
        /// 0.01 feel the same contact. Stiffness is <c>m * omega^2</c> against the creature's own
        /// mass, for the reason the joint limit scales by inertia.
        /// </remarks>
        public double ContactOmega = 20.0;

        public double ContactDampingRatio = 1.0;

        /// <summary>Whether creature-creature pushes act at all.</summary>
        public bool CreatureContact = true;

        // ---- sensors

        public double FlowFullScaleMetresPerSecond = 0.3;
        public double JointRateFullScale = 10.0;

        /// <summary>
        /// What <c>SensorChannel.Chemical</c> and <c>SensorChannel.Energy</c> read. The spike has
        /// no field and no ledger, so a constant stands in — see the spec's "what it is not".
        /// </summary>
        public float ConstantChemicalAndEnergy = 0.5f;

        /// <summary>
        /// Everything the solver reads, taken off a run's config.
        /// </summary>
        /// <param name="config">The run's own config, as <c>RunDirectory</c> loaded it.</param>
        /// <param name="stepSeconds">
        /// The physics step. Passed rather than read from
        /// <c>RunConfig.PhysicsStepSeconds</c> so that a survey or a screen can step a recorded
        /// world at another rate without editing its config — <c>EffectorDriver</c>'s own rule
        /// about demanding its timestep.
        /// </param>
        /// <param name="world">
        /// The world the current belongs to, or null. With one, the tank's radius comes from
        /// <c>World.TankRadiusMetres</c>, so the water, the fields and the solver share one
        /// geometry rather than deriving three; without one, a tank's radius is derived here the
        /// way <c>World</c> derives it, and <see cref="Current"/> is whatever the config carries
        /// — which has not been told its box, and will refuse to answer until something does.
        /// </param>
        public static SolverConfig From(RunConfig config, double stepSeconds, World world = null)
        {
            FluidConfig fluid = config.Fluid;

            return new SolverConfig
            {
                Density = fluid.Density,
                DragCoefficient = fluid.DragCoefficient,
                PanelsPerAxis = fluid.PanelsPerAxis,
                AddedMassCoefficient = fluid.AddedMassCoefficient,
                FluidAccelerationCoefficient = fluid.FluidAccelerationCoefficient,
                TissueExcessDensity = fluid.TissueExcessDensity,
                NeutralBodyVolume = fluid.NeutralBodyVolume,
                SurfaceRestoringFraction = fluid.SurfaceRestoringFraction,
                WaterHoldSeconds = fluid.WaterHoldSeconds,

                WorldDepthMetres = config.WorldDepthMetres,
                FloorIsSolid = config.SharedSpace,

                TankRadiusMetres = config.WorldShape == WorldShape.Tank
                    ? world != null
                        ? world.TankRadiusMetres
                        : TankGeometry.RadiusFor(config.WorldAreaSquareMetres)
                    : 0.0,

                // A field that no world has told its box refuses to answer, so without a world
                // the water is still; the caller that builds its own field sets Current itself.
                Current = world != null ? config.Current : null,
                PatchCount = (int)config.HorizontalPatches,

                StepSeconds = stepSeconds,
                DriveLimitAtEveryStep = config.DriveLimitAtEveryStep,

                FlowFullScaleMetresPerSecond = config.FlowFullScaleMetresPerSecond,
            };
        }

        /// <summary>
        /// Whether the two stabilisers engage: the same threshold, to the same bit, as
        /// <c>FluidEnvironment</c>'s drag limiter and <c>EffectorDriver</c>'s drive cap.
        /// </summary>
        public bool LimitersEngage => StepSeconds > 0.0100001 || DriveLimitAtEveryStep;

        /// <summary>The drag limiter's own gate, which has no "at every step" tunable.</summary>
        public bool DragLimiterEngages => StepSeconds > 0.0100001;
    }
}

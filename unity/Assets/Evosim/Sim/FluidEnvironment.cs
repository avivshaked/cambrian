using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// Applies water to a creature: quadratic drag per part, every fixed step — DESIGN.md §5.2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Buoyancy is neutral and implemented by having no gravity at all, following
    /// [K12 §2.1, p.3] — <i>"Water environment was simulated by applying a drag force opposing
    /// the movement of each body part and disabling gravity"</i> — and [C18 §2.2, p.5], which
    /// does it by "simply setting the gravity acceleration to zero." A creature neither sinks
    /// nor floats, so nothing has to be balanced.
    /// </para>
    /// <para>
    /// The force calculation itself lives in <see cref="FluidModel"/> in Evosim.Core, where it
    /// is unit-tested without a physics engine — including the property that matters most and
    /// is hardest to notice: drag can never deliver energy <i>into</i> a body. A fluid model
    /// that can is a free-energy source, and [U07 §2, p.3] documents a published search
    /// discovering exactly that kind of flaw and building its gait on it.
    /// </para>
    /// </remarks>
    public sealed class FluidEnvironment
    {
        public FluidConfig Config { get; }

        /// <summary>
        /// Geometry part shape ids resolve against. Must match what the creature was built and
        /// developed with — the collider, the mesh and these panels all describe one part.
        /// </summary>
        public PartShapeRegistry Shapes { get; }

        public FluidEnvironment(FluidConfig config = null, PartShapeRegistry shapes = null)
        {
            Config = config ?? FluidConfig.DragOnly;
            Shapes = shapes ?? PartShapeRegistry.Standard;
        }

        /// <summary>
        /// Sets up the scene for water. Call once, before stepping.
        /// </summary>
        /// <param name="selfCollision">
        /// Whether a creature's own parts collide with each other.
        /// </param>
        /// <remarks>
        /// <para>
        /// Spike 01 disabled collision entirely and Milestone 1 copied that, which meant parts
        /// swept through each other freely once joints started moving. Development produces
        /// creatures whose parts barely overlap — measured mean 0.3% of volume — so everything
        /// visibly passing through everything else was happening at <i>runtime</i>, not at
        /// growth. The static check could never have found it.
        /// </para>
        /// <para>
        /// DESIGN.md is in two minds about this. §4.2 permits overlap at joints, following
        /// Sims, because forbidding it rejects too many viable genomes. But §11.2 lists
        /// <i>self-collision vibration</i> as an exploit to guard against, quoting
        /// [C18 Fig. 13, p.19] on robots that "exploit self-collisions resulting in fast
        /// vibrations to produce thrust" — and you cannot exploit a collision that never
        /// happens. The second only makes sense if self-collision is on.
        /// </para>
        /// <para>
        /// PhysX articulations do not collide directly-jointed links, so enabling this gives
        /// exactly what §4.2 asks for: a part may overlap its own parent at the joint, but
        /// distant parts cannot occupy the same space.
        /// </para>
        /// <para>
        /// Creatures are kept apart by tiling at 100 m (§6.3) rather than by layer, since a
        /// layer cannot distinguish "my parts" from "another creature's".
        /// </para>
        /// </remarks>
        public static void ConfigureScene(bool selfCollision = true)
        {
            Physics.gravity = Vector3.zero;
            Physics.IgnoreLayerCollision(
                PhenotypeBuilder.CreatureLayer, PhenotypeBuilder.CreatureLayer, !selfCollision);
            Physics.defaultMaxDepenetrationVelocity = MaxDepenetrationVelocity;
        }

        /// <summary>
        /// How fast PhysX may push two overlapping bodies apart, in m/s. Unity's default is 10.
        /// </summary>
        /// <remarks>
        /// Depenetration is a correction, not a force: the solver assigns separating velocity
        /// to resolve an overlap and does not have to conserve momentum doing it. That makes it
        /// a free-energy source, and one a creature can reach deliberately — fold a limb into
        /// your own body and the solver pays you to unfold it.
        ///
        /// Measured, not assumed. With the default of 10, a creature whose joints had seized
        /// almost completely (3% of its free-swinging range) still reached 0.254 m/s of
        /// centre-of-mass velocity under purely internal forces, 119x the same creature with
        /// self-collision off, and travelled further in water than any creature that actually
        /// swam. Fitness is displacement (§5.5), so search would have found this immediately.
        ///
        /// The cost of a low cap is that genuine overlaps resolve slowly and can look soft.
        /// That is the better failure: a creature that separates sluggishly is ugly, a creature
        /// paid to jam is a corrupt fitness function (DESIGN.md §11.2).
        /// </remarks>
        /// <remarks>
        /// Lowered from 0.5 after zeroing PhysX's own body damping (see PhenotypeBuilder). That
        /// damping had been quietly bleeding off the momentum contact injects, so removing it —
        /// correct in itself, since it was an unmodelled second drag — exposed a leak it had
        /// been hiding: one creature went from 0.006 to 0.045 m²/s of specific angular momentum
        /// when self-collision was enabled, growing 1.7x over 2x the time, which is injection
        /// accumulating rather than solver error random-walking.
        /// </remarks>
        public const float MaxDepenetrationVelocity = 0.02f;

        /// <summary>
        /// Adds the water a creature drags along with it to each part's mass. Call once, after
        /// building; <see cref="FluidModel.EffectiveMass"/> explains why it is mass and not a
        /// force.
        /// </summary>
        public void ApplyAddedMass(CreatureInstance creature)
        {
            if (Config.AddedMassCoefficient <= 0f) return;

            for (int i = 0; i < creature.Bodies.Length; i++)
            {
                ArticulationBody body = creature.Bodies[i];
                body.mass = FluidModel.EffectiveMass(
                    body.mass, creature.Phenotype.Parts[i].Volume, Config);
            }
        }

        /// <summary>
        /// Energy drag has removed from creatures passed to <see cref="Apply"/>, in joules.
        /// </summary>
        /// <remarks>
        /// Positive means energy left the creature, which is the only direction drag is allowed
        /// to move it — <see cref="FluidModel"/>'s tests assert that per force, and this
        /// accumulates the consequence over a run so the energy balance can be closed.
        /// Accumulated across every creature this instance is applied to.
        /// </remarks>
        public double DissipatedJoules { get; private set; }

        /// <summary>Applies drag to every part. Call once per fixed step, before simulating.</summary>
        /// <param name="stepSeconds">
        /// The step about to be simulated. Only used for <see cref="DissipatedJoules"/>; pass 0
        /// to skip the accounting. Power is evaluated at the pre-step velocity, so the integral
        /// is a first-order estimate and will not close a balance to better than a percent or so.
        /// </param>
        public void Apply(CreatureInstance creature, float stepSeconds = 0f)
        {
            for (int i = 0; i < creature.Bodies.Length; i++)
            {
                ArticulationBody body = creature.Bodies[i];
                PhenotypePart part = creature.Phenotype.Parts[i];

                FluidModel.Drag(
                    Shapes.Resolve(part.ShapeId),
                    part.HalfExtents,
                    body.transform.rotation.ToQuat(),
                    body.linearVelocity.ToFloat3(),
                    body.angularVelocity.ToFloat3(),
                    Config,
                    _panels,
                    out Float3 force,
                    out Float3 torque);

                Vector3 f = force.ToVector3();
                Vector3 t = torque.ToVector3();

                body.AddForce(f);
                body.AddTorque(t);

                if (stepSeconds > 0f)
                {
                    EnsurePendingCapacity(creature.Bodies.Length);
                    _pendingForce[i] = f;
                    _pendingTorque[i] = t;
                    _pendingV[i] = body.linearVelocity;
                    _pendingW[i] = body.angularVelocity;
                    _pendingStep = stepSeconds;
                }
            }
        }

        /// <summary>
        /// Integrates the energy the last <see cref="Apply"/> removed. Call immediately after
        /// <c>Physics.Simulate</c>; only needed when <see cref="DissipatedJoules"/> is wanted.
        /// </summary>
        /// <remarks>
        /// Midpoint, for the reason given on <see cref="EffectorDriver.Settle"/>: drag is
        /// quadratic in speed, so evaluating its power at the pre-step velocity over-counts
        /// whenever the body is decelerating — which, under drag, is most of the time.
        /// </remarks>
        public void Settle(CreatureInstance creature)
        {
            if (_pendingStep <= 0f) return;

            for (int i = 0; i < creature.Bodies.Length && i < _pendingForce.Length; i++)
            {
                ArticulationBody body = creature.Bodies[i];

                Vector3 v = (_pendingV[i] + body.linearVelocity) * 0.5f;
                Vector3 w = (_pendingW[i] + body.angularVelocity) * 0.5f;

                float power = Vector3.Dot(_pendingForce[i], v) + Vector3.Dot(_pendingTorque[i], w);
                DissipatedJoules -= power * _pendingStep;   // power is negative: drag opposes
            }

            _pendingStep = 0f;
        }

        /// <summary>
        /// Panel scratch, reused across every part of every creature for the life of this
        /// environment.
        /// </summary>
        /// <remarks>
        /// A fresh list per part per step is thousands of allocations a second once a population
        /// is running, and the collection pause that eventually follows presents as a physics
        /// hitch — something that looks like the simulation, not like the allocator. Reused here
        /// because <see cref="Apply"/> is single-threaded and the panels do not outlive the call.
        /// </remarks>
        private readonly System.Collections.Generic.List<DragPanel> _panels =
            new System.Collections.Generic.List<DragPanel>(64);

        private Vector3[] _pendingForce = System.Array.Empty<Vector3>();
        private Vector3[] _pendingTorque = System.Array.Empty<Vector3>();
        private Vector3[] _pendingV = System.Array.Empty<Vector3>();
        private Vector3[] _pendingW = System.Array.Empty<Vector3>();
        private float _pendingStep;

        private void EnsurePendingCapacity(int n)
        {
            if (_pendingForce.Length >= n) return;

            _pendingForce = new Vector3[n];
            _pendingTorque = new Vector3[n];
            _pendingV = new Vector3[n];
            _pendingW = new Vector3[n];
        }

        /// <summary>
        /// Centre of mass of a creature, in world space. The quantity DESIGN.md §5.5 measures
        /// fitness on.
        /// </summary>
        public static Vector3 CentreOfMass(CreatureInstance creature)
        {
            Vector3 sum = Vector3.zero;
            float mass = 0f;

            for (int i = 0; i < creature.Bodies.Length; i++)
            {
                ArticulationBody body = creature.Bodies[i];
                sum += body.worldCenterOfMass * body.mass;
                mass += body.mass;
            }

            return mass > 1e-6f ? sum / mass : Vector3.zero;
        }
    }
}

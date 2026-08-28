using System.Collections.Generic;
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

        public FluidEnvironment(
            FluidConfig config = null,
            PartShapeRegistry shapes = null,
            CurrentField current = null)
        {
            Config = config ?? FluidConfig.DragOnly;
            Shapes = shapes ?? PartShapeRegistry.Standard;
            Current = current;
        }

        /// <summary>
        /// Water that moves, or null for still water — DESIGN.md §5A.4, D036.
        /// </summary>
        /// <remarks>
        /// <b>It enters the model at exactly one point</b>, which §5A.4 predicted it would: drag is
        /// computed against a velocity, so subtracting the water's velocity from the body's turns
        /// drag into advection for the price of one evaluation per part. Nothing else changes —
        /// same panels, same coefficients, same parallel path — and a creature standing still in
        /// moving water now feels exactly the force it would feel swimming through still water at
        /// the same relative speed, which is the whole of the physics.
        /// </remarks>
        public CurrentField Current { get; set; }

        /// <summary>Seconds the world has been running, for <see cref="Current"/>.</summary>
        /// <remarks>
        /// Set by the caller each step rather than accumulated here. This class is stepped by
        /// several harnesses at several timesteps, and a clock that counted its own calls would
        /// read differently in each of them while looking identical — the same argument that makes
        /// <c>EffectorDriver</c> demand its timestep rather than defaulting it.
        /// </remarks>
        public double ElapsedSeconds { get; set; }

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
            _one[0] = creature;
            Apply(_one, stepSeconds);
        }

        /// <summary>
        /// Applies drag to a whole population in one pass — DESIGN.md §5A.9.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Gather, compute in parallel, apply serially.</b> The middle phase is pure arithmetic
        /// over cached geometry and is the term §5A.9 measured at 88% of the step, so it is the
        /// one worth spreading over cores. The other two cannot be: reading
        /// <c>body.transform.rotation</c> touches a <see cref="Transform"/> and
        /// <see cref="ArticulationBody.AddForce"/> mutates solver state, and Unity permits
        /// neither off the main thread.
        /// </para>
        /// <para>
        /// <b>Determinism survives</b> (§7). Each body owns one slot in the flat arrays and writes
        /// only its own, so no result depends on which thread got there first, and the forces are
        /// applied afterwards in index order. Floating-point summation order — the thing that
        /// actually breaks reproducibility when work is spread out — never changes, because
        /// nothing is summed across bodies here.
        /// </para>
        /// </remarks>
        public void Apply(IReadOnlyList<CreatureInstance> creatures, float stepSeconds = 0f)
        {
            int bodies = Layout(creatures);
            if (bodies == 0) return;

            // ---- gather (main thread): everything the solver owns, copied out
            for (int c = 0; c < creatures.Count; c++)
            {
                CreatureInstance creature = creatures[c];
                if (creature?.Bodies == null) continue;

                EnsurePanels(creature);
                int at = _offset[c];

                for (int i = 0; i < creature.Bodies.Length; i++)
                {
                    ArticulationBody body = creature.Bodies[i];

                    _rotation[at + i] = body.transform.rotation.ToQuat();
                    _spin[at + i] = body.angularVelocity.ToFloat3();
                    _panelsAt[at + i] = creature.DragPanels[i];

                    // Relative to the water, not to the world. Sampled here in the gather phase
                    // because it needs the body's position, which is a Transform read and so main
                    // thread only; the compute phase past this point touches no Unity type.
                    Float3 water = Current != null
                        ? Current.VelocityAt(body.transform.position.y, ElapsedSeconds)
                        : Float3.Zero;

                    _velocity[at + i] = body.linearVelocity.ToFloat3() - water;
                }
            }

            // ---- compute (any thread): no Unity types touched past this point
            if (bodies >= ParallelThreshold)
            {
                System.Threading.Tasks.Parallel.For(0, bodies, Compute);
            }
            else
            {
                for (int i = 0; i < bodies; i++) Compute(i);
            }

            // ---- apply (main thread)
            float excessDensity = Config.TissueExcessDensity;

            for (int c = 0; c < creatures.Count; c++)
            {
                CreatureInstance creature = creatures[c];
                if (creature?.Bodies == null) continue;

                int at = _offset[c];

                for (int i = 0; i < creature.Bodies.Length; i++)
                {
                    ArticulationBody body = creature.Bodies[i];

                    body.AddForce(_force[at + i]);
                    body.AddTorque(_torque[at + i]);

                    // Excess weight, applied here rather than in Compute because it does not
                    // depend on velocity — drag does, and the balance of the two is what sets the
                    // terminal sink rate without anyone choosing it. mass is volume x water
                    // density, so dividing by the *same* constant PhenotypeBuilder used recovers
                    // the volume the buoyancy acts on. Not FluidConfig.Density: that is a
                    // [Tunable] and a sweep of it would silently rescale every creature's weight
                    // through a term that has nothing to do with how its mass was assigned. Gravity itself stays off (§5.2): this is the *difference*
                    // between weight and upthrust, which is all that a neutrally buoyant world was
                    // ever missing.
                    if (excessDensity > 0f)
                    {
                        body.AddForce(
                            new Vector3(
                                0f,
                                -excessDensity * body.mass * GravityMetresPerSecondSquared /
                                    PhenotypeBuilder.DensityKgPerM3,
                                0f));
                    }

                    if (stepSeconds > 0f)
                    {
                        _preV[at + i] = body.linearVelocity;
                        _preW[at + i] = body.angularVelocity;
                    }
                }
            }

            _pendingStep = stepSeconds > 0f ? stepSeconds : 0f;
        }

        /// <summary>
        /// Bodies below which spreading the work costs more than it saves.
        /// </summary>
        /// <remarks>
        /// A <c>Parallel.For</c> has a fixed cost of a few microseconds in scheduling, and one
        /// creature is around eight bodies of a few microseconds each. The sandbox scene and the
        /// single-creature harnesses sit well under this and take the serial path, so they are
        /// unaffected by any of it.
        /// </remarks>
        private const int ParallelThreshold = 64;

        /// <summary>
        /// Standard gravity, m/s². Used only to weigh <see cref="FluidConfig.TissueExcessDensity"/>
        /// — <see cref="UnityEngine.Physics.gravity"/> stays at zero (§5.2), so this is a constant
        /// in a buoyancy term rather than a field acting on the scene.
        /// </summary>
        private const float GravityMetresPerSecondSquared = 9.81f;

        private void Compute(int i)
        {
            FluidModel.Drag(
                _panelsAt[i], _rotation[i], _velocity[i], _spin[i], Config,
                out Float3 force, out Float3 torque);

            _force[i] = force.ToVector3();
            _torque[i] = torque.ToVector3();
        }

        /// <summary>
        /// Builds this creature's panels if they are missing or were built at another resolution.
        /// </summary>
        /// <remarks>
        /// The resolution check is not defensive padding. This project's recurring fault is a
        /// parameter that never reaches what it configures (logbook/0007, 0008, 0013), and a
        /// cached panel set is exactly the shape that fault takes next: change
        /// <see cref="FluidConfig.PanelsPerAxis"/>, re-run, and get byte-identical results because
        /// every creature was still carrying panels built at the old value.
        /// </remarks>
        private void EnsurePanels(CreatureInstance creature)
        {
            int resolution = Config.PanelsPerAxis < 1 ? 1 : Config.PanelsPerAxis;

            if (creature.DragPanels != null &&
                creature.DragPanels.Length == creature.Bodies.Length &&
                creature.DragPanelsPerAxis == resolution)
            {
                return;
            }

            var sets = new DragPanelSet[creature.Bodies.Length];
            for (int i = 0; i < sets.Length; i++)
            {
                PhenotypePart part = creature.Phenotype.Parts[i];
                sets[i] = DragPanelSet.For(
                    Shapes.Resolve(part.ShapeId), part.HalfExtents, resolution, _panels);
            }

            creature.DragPanels = sets;
            creature.DragPanelsPerAxis = resolution;
        }

        /// <summary>Assigns each body a slot in the flat arrays. Returns the total.</summary>
        private int Layout(IReadOnlyList<CreatureInstance> creatures)
        {
            if (_offset.Length < creatures.Count) _offset = new int[creatures.Count * 2];

            int total = 0;
            for (int c = 0; c < creatures.Count; c++)
            {
                _offset[c] = total;
                CreatureInstance creature = creatures[c];
                if (creature?.Bodies != null) total += creature.Bodies.Length;
            }

            EnsurePendingCapacity(total);
            return total;
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
            _one[0] = creature;
            Settle(_one);
        }

        /// <summary>
        /// Integrates the energy the last <see cref="Apply"/> removed, for a whole population.
        /// Pass the same list, in the same order.
        /// </summary>
        /// <remarks>
        /// Summed serially in index order rather than alongside the parallel compute phase.
        /// <see cref="DissipatedJoules"/> is one accumulator over every part of every creature, so
        /// spreading the addition would make the total depend on thread scheduling — a run whose
        /// energy audit differs slightly on each replay, which is precisely the reproducibility
        /// §7 promises. The addition is cheap; the parallel phase was never this.
        /// </remarks>
        public void Settle(IReadOnlyList<CreatureInstance> creatures)
        {
            if (_pendingStep <= 0f) return;

            for (int c = 0; c < creatures.Count; c++)
            {
                CreatureInstance creature = creatures[c];
                if (creature?.Bodies == null) continue;

                int at = _offset[c];

                for (int i = 0; i < creature.Bodies.Length; i++)
                {
                    ArticulationBody body = creature.Bodies[i];
                    int j = at + i;
                    if (j >= _force.Length) break;

                    Vector3 v = (_preV[j] + body.linearVelocity) * 0.5f;
                    Vector3 w = (_preW[j] + body.angularVelocity) * 0.5f;

                    float power = Vector3.Dot(_force[j], v) + Vector3.Dot(_torque[j], w);
                    DissipatedJoules -= power * _pendingStep;   // power is negative: drag opposes
                }
            }

            _pendingStep = 0f;
        }

        /// <summary>
        /// Panel scratch, used only while <i>building</i> a creature's panels.
        /// </summary>
        /// <remarks>
        /// This used to be refilled for every part on every step. It is now touched once per
        /// creature, at the first step of its life, and never again.
        /// </remarks>
        private readonly List<DragPanel> _panels = new List<DragPanel>(64);

        /// <summary>Backing list for the single-creature overloads, so they allocate nothing.</summary>
        private readonly CreatureInstance[] _one = new CreatureInstance[1];

        private int[] _offset = new int[64];

        // One slot per body across the whole population. Written by the parallel phase (each
        // index by exactly one iteration), read by the serial apply and settle phases.
        private Quat[] _rotation = System.Array.Empty<Quat>();
        private Float3[] _velocity = System.Array.Empty<Float3>();
        private Float3[] _spin = System.Array.Empty<Float3>();
        private DragPanelSet[] _panelsAt = System.Array.Empty<DragPanelSet>();
        private Vector3[] _force = System.Array.Empty<Vector3>();
        private Vector3[] _torque = System.Array.Empty<Vector3>();
        private Vector3[] _preV = System.Array.Empty<Vector3>();
        private Vector3[] _preW = System.Array.Empty<Vector3>();
        private float _pendingStep;

        private void EnsurePendingCapacity(int n)
        {
            if (_force.Length >= n) return;

            int size = n * 2;   // grown with slack: population changes every birth and death

            _rotation = new Quat[size];
            _velocity = new Float3[size];
            _spin = new Float3[size];
            _panelsAt = new DragPanelSet[size];
            _force = new Vector3[size];
            _torque = new Vector3[size];
            _preV = new Vector3[size];
            _preW = new Vector3[size];
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

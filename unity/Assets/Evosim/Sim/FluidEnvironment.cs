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

        public FluidEnvironment(FluidConfig config = null)
        {
            Config = config ?? FluidConfig.DragOnly;
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
        }

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

        /// <summary>Applies drag to every part. Call once per fixed step, before simulating.</summary>
        public void Apply(CreatureInstance creature)
        {
            for (int i = 0; i < creature.Bodies.Length; i++)
            {
                ArticulationBody body = creature.Bodies[i];
                PhenotypePart part = creature.Phenotype.Parts[i];

                FluidModel.BoxDrag(
                    part.HalfExtents,
                    body.transform.rotation.ToQuat(),
                    body.linearVelocity.ToFloat3(),
                    body.angularVelocity.ToFloat3(),
                    Config,
                    out Float3 force,
                    out Float3 torque);

                body.AddForce(force.ToVector3());
                body.AddTorque(torque.ToVector3());
            }
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

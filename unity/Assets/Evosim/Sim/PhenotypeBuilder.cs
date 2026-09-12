using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// A built creature: the GameObjects and articulation bodies for one phenotype.
    /// </summary>
    public sealed class CreatureInstance
    {
        public GameObject Root { get; internal set; }
        public ArticulationBody[] Bodies { get; internal set; }
        public Phenotype Phenotype { get; internal set; }

        /// <summary>Index of the first actuated DOF for each body, or -1 where the joint is fixed.</summary>
        public int[] DofOffset { get; internal set; }

        /// <summary>Every renderer in the creature, and the part each one belongs to.</summary>
        /// <remarks>
        /// Kept so the cell-type view can be toggled without rebuilding — a capsule draws with
        /// three renderers, so there is no one-to-one mapping to recover afterwards.
        /// </remarks>
        public MeshRenderer[] Renderers { get; internal set; }
        public int[] RendererPart { get; internal set; }

        /// <summary>Total actuated degrees of freedom across the creature.</summary>
        public int TotalDof { get; internal set; }

        /// <summary>
        /// The mass PhysX carries for each link, kg, as of the last build or resize —
        /// <c>logbook/specs/throw-trace-spec.md</c> step 1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Read back off the <see cref="ArticulationBody"/> rather than recomputed from the
        /// phenotype</b>, because the two are not the same number: <see cref="PhenotypeBuilder.Build"/>
        /// writes the plain mass and then <c>FluidEnvironment.ApplyAddedMass</c> inflates it, and
        /// <see cref="PhenotypeBuilder.Resize"/> writes the inflated one directly. The question
        /// this instrument exists for is what the solver was given, so it is the solver's own
        /// value that is kept. Non-finite is possible and is recorded as it stands; the ratio
        /// below is what refuses it.
        /// </para>
        /// <para>
        /// Allocated once at build and refilled in place by every resize, so growth costs no
        /// garbage.
        /// </para>
        /// </remarks>
        public float[] LinkMasses { get; internal set; }

        /// <summary>
        /// Each link's mass ratio against its own parent — <c>max / min</c>, so it is never
        /// below 1. 0 for the root, which has no joint.
        /// </summary>
        /// <remarks>
        /// PhysX's joint documentation puts the practical ceiling at about 10: an impulse applied
        /// to a heavy link becomes a very large velocity when it is propagated into a light one,
        /// and the solver stops converging. Whether this world's divergences sit above that line
        /// is the question <c>scratch/research-throws/notes.txt</c> ranked first, and nothing
        /// measured it until this build — see <c>logbook/specs/throw-trace-spec.md</c>.
        /// </remarks>
        public float[] JointMassRatios { get; internal set; }

        /// <summary>
        /// The largest of <see cref="JointMassRatios"/>, or 0 for a body with no joint.
        /// </summary>
        /// <remarks>
        /// Kept as one number per body so the hot loop that counts bodies over the threshold does
        /// not walk the links. Finite by construction: a ratio computed from a non-finite or
        /// non-positive mass is not a ratio, and is dropped rather than propagated into a run
        /// maximum that a JSON writer would then refuse.
        /// </remarks>
        public float MaxJointMassRatio { get; internal set; }

        /// <summary>
        /// Drag panels for each part, built once. Owned by <see cref="FluidEnvironment"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A part's local geometry is fixed for as long as the body is one size, but panels were
        /// rebuilt from the <see cref="PartShape"/> on every part on every step — which §5A.9
        /// measured as the largest single term in the simulation. Cached here rather than in the
        /// environment because they belong to the creature: they describe its body, and they die
        /// with it. <see cref="DragPanelsPerAxis"/> records the resolution they were built at so
        /// that an environment configured differently rebuilds instead of silently using someone
        /// else's.
        /// </para>
        /// <para>
        /// <b>A body can change size now</b> (fable-propose-growth.md rule 8, 2026-09-08), and
        /// <see cref="PhenotypeBuilder.Resize"/> nulls this rather than refilling it. The
        /// environment owns the one place panels are built, so a grown body that kept its old
        /// panels would be the cached-parameter fault this project has hit three times: drag
        /// priced against a body the creature no longer has, with nothing reporting it
        /// (logbook/0007, 0008, 0013).
        /// </para>
        /// </remarks>
        public DragPanelSet[] DragPanels { get; internal set; }

        /// <summary><see cref="FluidConfig.PanelsPerAxis"/> that <see cref="DragPanels"/> was built at.</summary>
        public int DragPanelsPerAxis { get; internal set; }

        /// <summary>
        /// Which of D061's horizontal patches the creature is in — D066. 0 in a world with one
        /// patch, which is every world before D061.
        /// </summary>
        /// <remarks>
        /// <b>Carried here because the fluid needs it and knows nothing about organisms.</b> With
        /// D066's rolls the water at a given depth is not the same water everywhere: patch <i>k</i>
        /// is rising while <i>k+1</i> sinks, so <see cref="FluidEnvironment"/> cannot sample the
        /// current from a depth alone. The identity lives on the instance rather than on the
        /// <see cref="Phenotype"/> because a phenotype is a developed body and is shared by every
        /// creature that develops the same genome. Set when the body is built and refreshed each
        /// metabolic step by <c>Ecosystem</c>, because a creature's patch changes underneath it —
        /// dispersal (D061) and advection (D066) both move it.
        /// </remarks>
        public int Patch { get; internal set; }

        /// <summary>
        /// Water velocity relative to each part, world axes, m/s — what
        /// <see cref="SensorChannel.Flow"/> reads. Null unless something asked for it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Written by the drag pass, which computes this quantity anyway.</b>
        /// <see cref="FluidEnvironment.Apply(System.Collections.Generic.IReadOnlyList{CreatureInstance}, float)"/>
        /// needs the body's velocity relative to the water to compute a drag force, and used to
        /// throw it away afterwards. A lateral line is that same number, so recomputing it would
        /// be a second <c>CurrentField.VelocityAt</c> and a second Transform read per part per
        /// step for a value already in hand.
        /// </para>
        /// <para>
        /// <b>On the creature rather than in an array indexed by slot.</b> The environment's own
        /// flat arrays are indexed by the creature's position in the list it was last stepped
        /// with, and <c>Ecosystem.Reconcile</c> rebuilds that order on every birth and death — so
        /// slot <i>i</i> is a different creature from one step to the next, and a sampler reading
        /// by slot would occasionally read another animal's water. This dies with the body it
        /// describes.
        /// </para>
        /// <para>
        /// <b>Null by default and allocated only by a creature that reads the channel</b>
        /// (<c>CreatureSensors</c>, from its brain's requirement mask), so a world where nothing
        /// senses flow pays one null test per part per step and no memory at all.
        /// </para>
        /// </remarks>
        public Float3[] RelativeVelocity { get; internal set; }

        /// <summary>Takes the body out of the water, on the line that kills it.</summary>
        /// <remarks>
        /// <para>
        /// <b>Immediate in both modes, and that is the fix rather than the tidiness.</b> This used
        /// to call <c>Object.Destroy</c> whenever <c>Application.isPlaying</c>, which Unity defers
        /// to the end of the frame. In the farm a frame is one physics step and nothing can tell
        /// the difference; in the theatre a frame is tens of steps, so a creature killed inside a
        /// step stayed in the scene as an undriven collider for the rest of the frame, and a
        /// newborn placed into the spot it was still standing in was shoved out of it by the
        /// depenetration. That is an impulse the recording never felt: the theatre's replay of
        /// round 33 seed 3 matched its recording exactly at 100 s and had parted from it by 200 s
        /// (logbook/0083). The farm and the theatre must run the same step, so the body leaves the
        /// world at the moment the world says it is dead.
        /// </para>
        /// <para>
        /// The one caller that cannot have this is <see cref="DestroyAtEndOfFrame"/>.
        /// </para>
        /// </remarks>
        public void Destroy()
        {
            if (Root == null) return;

            Object.DestroyImmediate(Root);
            Root = null;
        }

        /// <summary>
        /// Takes the body out at the end of the frame, for a caller that is already inside Unity's
        /// own teardown.
        /// </summary>
        /// <remarks>
        /// The single exception to <see cref="Destroy"/>, and it is not a simulation path.
        /// <c>CreatureSpawner.OnDestroy</c> runs while Unity is destroying the object this body
        /// hangs from, and destroying a GameObject immediately from inside a destruction callback
        /// is the case Unity leaves undefined. Nothing steps afterwards, so the frame this waits
        /// for simulates nothing and the fidelity argument above does not arise.
        /// </remarks>
        public void DestroyAtEndOfFrame()
        {
            if (Root == null) return;

            if (Application.isPlaying) Object.Destroy(Root);
            else Object.DestroyImmediate(Root);

            Root = null;
        }
    }

    /// <summary>
    /// Turns a developed <see cref="Phenotype"/> into a PhysX articulation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ArticulationBody, not Rigidbody + ConfigurableJoint</b> (DESIGN.md §6.2) — creatures
    /// are articulated kinematic trees, which is what PhysX articulations exist for, and they
    /// are far more stable under the high joint torques evolution will discover.
    /// Spike 01 measured build + teardown of a 10-part creature at 0.335 ms against a 15 ms
    /// budget, so rebuilding per evaluation is affordable and no pooling is needed
    /// (DECISIONS.md D010).
    /// </para>
    /// <para>
    /// The articulation must be constructed parent-first: an ArticulationBody added to a
    /// GameObject whose ancestor already has one becomes a child link, and the ordering is
    /// fixed at add time. <see cref="Developer"/> emits parts in depth-first pre-order for
    /// exactly this reason, so a single forward pass over <see cref="Phenotype.Parts"/> is
    /// correct by construction.
    /// </para>
    /// </remarks>
    public static class PhenotypeBuilder
    {
        /// <summary>
        /// Layer creatures are placed on. Spike 01 disabled collisions entirely via
        /// <c>Physics.IgnoreLayerCollision</c>; tiled creatures in open water never touch,
        /// and self-collision is deliberately not enforced — Sims permitted overlap at
        /// joints, and forbidding it kills too many otherwise viable genomes
        /// (DESIGN.md §4.2). This changes at Milestone 5, when land needs contact.
        /// </summary>
        public const int CreatureLayer = 8;

        /// <summary>
        /// Part density in kg/m³. Water is 1000, and DESIGN.md §5 puts creatures in water
        /// first, so a neutrally buoyant body is the honest default. Until fluid forces land
        /// at Milestone 2 this only sets inertia — but it also sets the mass that §4.4's
        /// effector scaling divides by, so it is not cosmetic.
        /// </summary>
        public const float DensityKgPerM3 = 1000f;

        /// <param name="shapes">
        /// Geometry each part's shape id resolves against. Must be the same registry the
        /// phenotype was developed with and the same one the fluid model is given, since all
        /// three have to agree on how large a part is.
        /// </param>
        public static CreatureInstance Build(
            Phenotype phenotype,
            Vector3 origin,
            Transform parent = null,
            PartShapeRegistry shapes = null)
        {
            if (phenotype == null || phenotype.PartCount == 0)
            {
                throw new System.ArgumentException("Cannot build an empty phenotype.", nameof(phenotype));
            }

            shapes = shapes ?? PartShapeRegistry.Standard;

            var root = new GameObject("Creature");
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.position = origin;

            var bodies = new ArticulationBody[phenotype.PartCount];
            var transforms = new Transform[phenotype.PartCount];
            var dofOffset = new int[phenotype.PartCount];
            int dofCursor = 0;

            _renderers.Clear();
            _rendererPart.Clear();

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                PhenotypePart part = phenotype.Parts[i];

                var go = new GameObject($"Part{i:00}_n{part.SourceNode}") { layer = CreatureLayer };

                // EVERY part transform stays at unit scale. Parts are parented to each other
                // so PhysX sees an articulation chain, and Unity compounds a parent's scale
                // into its children — for a child rotated relative to a non-uniformly scaled
                // parent that compounding SHEARS, and no componentwise division undoes it.
                // Size therefore lives on the collider and on a separate visual child, never
                // on the transform that positions the body.
                go.transform.SetParent(part.IsRoot ? root.transform : transforms[part.ParentIndex], false);

                // Development produces poses in creature space; each part is parented to its
                // own parent part, so convert into that parent's frame.
                if (part.IsRoot)
                {
                    go.transform.localPosition = part.Position.ToVector3();
                    go.transform.localRotation = part.Rotation.ToQuaternion();
                }
                else
                {
                    PhenotypePart parentPart = phenotype.Parts[part.ParentIndex];
                    Quaternion inverseParent = Quaternion.Inverse(parentPart.Rotation.ToQuaternion());

                    go.transform.localPosition =
                        inverseParent * (part.Position - parentPart.Position).ToVector3();
                    go.transform.localRotation =
                        inverseParent * part.Rotation.ToQuaternion();
                }

                _partBeingBuilt = i;
                AddColliderAndVisual(go, part, shapes.Resolve(part.ShapeId));

                transforms[i] = go.transform;

                var body = go.AddComponent<ArticulationBody>();
                body.mass = Mathf.Max(0.001f, part.Volume * DensityKgPerM3);

                // PhysX's own damping, zeroed deliberately. Unity defaults angularDamping and
                // jointFriction to 0.05, which is a second velocity-proportional drag acting on
                // top of the fluid model in §5.2 — energy leaving the creature through a channel
                // the design never specified and no fitness or energy figure accounts for.
                //
                // Found by an energy audit: joints were doing ~10x more work than drag was
                // removing, at a ratio that stayed near-constant across a 40x sweep of drive
                // strength. A scale-invariant loss is the signature of a linear damping term,
                // not of anything the creature is doing.
                //
                // §5.2's drag is the only resistance a creature should feel. If the solver needs
                // damping for stability, that is a fluid-model parameter and belongs in
                // FluidConfig where it can be measured, not a hidden engine default.
                body.linearDamping = 0f;
                body.angularDamping = 0f;
                body.jointFriction = 0f;
                bodies[i] = body;

                if (part.IsRoot)
                {
                    body.immovable = false;
                    dofOffset[i] = -1;
                }
                else
                {
                    ConfigureJoint(body, part, phenotype.Parts[part.ParentIndex]);
                    int dof = part.JointType.DofCount();
                    dofOffset[i] = dof > 0 ? dofCursor : -1;
                    dofCursor += dof;
                }
            }

            var instance = new CreatureInstance
            {
                Root = root,
                Bodies = bodies,
                Phenotype = phenotype,
                DofOffset = dofOffset,
                TotalDof = dofCursor,
                Renderers = _renderers.ToArray(),
                RendererPart = _rendererPart.ToArray(),
            };

            // logbook/specs/throw-trace-spec.md step 1: the masses as written, before anything
            // has stepped. Added mass is applied by the caller a moment later and moves every one
            // of them, which is why Ecosystem asks again after that — see the method's remarks.
            MeasureJointMassRatios(instance);

            return instance;
        }

        /// <summary>
        /// Reads every link's mass off the articulation and fills in the body's joint mass ratios
        /// — <c>logbook/specs/throw-trace-spec.md</c> step 1.
        /// </summary>
        /// <returns><see cref="CreatureInstance.MaxJointMassRatio"/>, for a caller that wants it
        /// without reading the instance back.</returns>
        /// <remarks>
        /// <para>
        /// <b>A diagnostic and nothing else.</b> It reads <c>ArticulationBody.mass</c> and writes
        /// three fields on the instance; it sets no force, no mass and no clamp, and it runs only
        /// where a body is built or resized — never inside the physics loop. A state digest under
        /// this build must therefore be identical to one without it.
        /// </para>
        /// <para>
        /// <b>Called three times rather than once, and the third is the one that matters when the
        /// fluid is on.</b> <see cref="Build"/> writes the plain mass, <c>Ecosystem.Build</c> then
        /// calls <c>FluidEnvironment.ApplyAddedMass</c> which multiplies it, and
        /// <see cref="Resize"/> writes the inflated value itself. So the ratio is taken at the end
        /// of each of those, and what a run reports is the effective mass whenever
        /// <c>FluidConfig.AddedMassCoefficient</c> is above 0 — which is no recorded run, where
        /// the coefficient is 0 and the three readings agree.
        /// </para>
        /// <para>
        /// <b>Parentage from the phenotype, not from the articulation.</b> A link's parent is a
        /// fact about the developed body, and <see cref="PhenotypePart.ParentIndex"/> is the same
        /// index <see cref="Build"/> parented the GameObjects by, so the two cannot drift. A part
        /// list shorter than the body's links is treated as unknown parentage and scores 0 rather
        /// than indexing off the end: that mismatch is impossible from
        /// <see cref="Resize"/>, which refuses a count change outright, but the dump path must
        /// never be the thing that throws.
        /// </para>
        /// </remarks>
        public static float MeasureJointMassRatios(CreatureInstance instance)
        {
            if (instance == null) return 0f;

            ArticulationBody[] bodies = instance.Bodies;
            if (bodies == null || bodies.Length == 0)
            {
                instance.MaxJointMassRatio = 0f;
                return 0f;
            }

            if (instance.LinkMasses == null || instance.LinkMasses.Length != bodies.Length)
            {
                instance.LinkMasses = new float[bodies.Length];
                instance.JointMassRatios = new float[bodies.Length];
            }

            for (int i = 0; i < bodies.Length; i++)
            {
                instance.LinkMasses[i] = bodies[i] != null ? bodies[i].mass : 0f;
            }

            Phenotype phenotype = instance.Phenotype;
            int parts = phenotype?.Parts != null ? phenotype.Parts.Count : 0;
            float worst = 0f;

            for (int i = 0; i < bodies.Length; i++)
            {
                float ratio = 0f;

                if (i < parts)
                {
                    PhenotypePart part = phenotype.Parts[i];

                    if (part != null && !part.IsRoot &&
                        part.ParentIndex >= 0 && part.ParentIndex < bodies.Length)
                    {
                        ratio = MassRatio(
                            instance.LinkMasses[part.ParentIndex], instance.LinkMasses[i]);
                    }
                }

                instance.JointMassRatios[i] = ratio;
                if (ratio > worst) worst = ratio;
            }

            instance.MaxJointMassRatio = worst;
            return worst;
        }

        /// <summary>
        /// The heavier of two link masses over the lighter, or 0 where that is not a number.
        /// </summary>
        /// <remarks>
        /// 0 rather than infinity for a zero or non-finite mass, because this value is summarised
        /// into a run maximum that <see cref="Json.Writer"/> would refuse to write — and a
        /// divergence is exactly when a mass can stop being a number. The per-link masses in the
        /// trace dump carry the raw value, so nothing is hidden: only the summary declines to
        /// make a ratio out of it. In practice the floor in <see cref="Build"/> keeps every mass
        /// at or above a gram, so this cannot fire on a healthy body.
        /// </remarks>
        private static float MassRatio(float parentMass, float childMass)
        {
            float heavier = parentMass > childMass ? parentMass : childMass;
            float lighter = parentMass > childMass ? childMass : parentMass;

            if (!(lighter > 0f) || float.IsNaN(heavier) || float.IsInfinity(heavier)) return 0f;

            float ratio = heavier / lighter;

            return float.IsNaN(ratio) || float.IsInfinity(ratio) ? 0f : ratio;
        }

        /// <summary>
        /// Puts a creature's live articulation at the size <paramref name="scaled"/> says it is,
        /// without rebuilding it. fable-propose-growth.md rule 8 (2026-09-08).
        /// </summary>
        /// <param name="instance">The built creature. Its <see cref="CreatureInstance.Phenotype"/> becomes <paramref name="scaled"/>.</param>
        /// <param name="scaled">
        /// <c>Organism.Phenotype</c>, which Core has already scaled from the adult. Its parts are
        /// in the adult's order and count, so part <i>i</i> here is body <i>i</i> there.
        /// </param>
        /// <param name="fluid">
        /// The fluid the creature swims in, for added mass. Mass is set from the new volume and
        /// then inflated once, so added mass is never applied twice: the plain mass is recomputed
        /// on every resize rather than being multiplied into whatever the body was carrying.
        /// </param>
        /// <param name="shapes">The registry the phenotype was developed with, as <see cref="Build"/> demands.</param>
        /// <remarks>
        /// <para>
        /// <b>Rebuilding the articulation instead would throw the creature away.</b> A body's
        /// velocities, its joint state and its brain's loop through the solver all live in the
        /// articulation, and a creature that grew by being destroyed and rebuilt would be reset to
        /// rest every ten seconds. So the four things that carry size are written in place:
        /// collider extents, mass, both joint anchors, and the visual.
        /// </para>
        /// <para>
        /// <b>The transforms are deliberately not touched.</b> PhysX drives every link's pose from
        /// the articulation once the solver has run, so writing a link's local position here would
        /// be overwritten at best and would fight the solver at worst. The anchors are what say
        /// where a joint is, and they are lengths, so they scale.
        /// </para>
        /// <para>
        /// <b>PhysX keeps the joint state across an anchor change, and a smoke is what proves
        /// it.</b> The failure mode this has is a body that teleports when its constraint frame
        /// moves: r20q-s1 diverged because one newborn link was spun up by thousands of rad/s in a
        /// single step (logbook/0059), and a resize that jumps a body is the same accident from a
        /// different direction. <c>Ecosystem</c> therefore measures the largest root displacement
        /// across a resize and reports it, rather than anyone asserting from the documentation
        /// that it is zero.
        /// </para>
        /// <para>
        /// <b>Cost, and why it is off the physics path.</b> One pass over the parts: a collider
        /// write, a mass write, two anchor writes and up to three transform writes each, plus the
        /// panel rebuild the fluid does lazily on its next step. That is roughly what
        /// <see cref="Build"/> costs less the GameObject allocation, which spike 01 measured at
        /// 0.335 ms for a ten-part creature. Run once per <c>RunConfig.GrowthStepSeconds</c> of
        /// simulated time, which is one metabolic step in twenty at the default, and never per
        /// physics step.
        /// </para>
        /// </remarks>
        public static void Resize(
            CreatureInstance instance,
            Phenotype scaled,
            FluidConfig fluid,
            PartShapeRegistry shapes = null)
        {
            if (instance == null) throw new System.ArgumentNullException(nameof(instance));
            if (scaled == null) throw new System.ArgumentNullException(nameof(scaled));

            // Not defensive padding: the whole method indexes one list by the other's position,
            // and a mismatch would silently give part i the size of some other part. Core
            // guarantees the count, so this can only fire if that guarantee breaks.
            if (scaled.PartCount != instance.Bodies.Length)
            {
                throw new System.ArgumentException(
                    $"A body of {instance.Bodies.Length} links cannot be resized to a phenotype of " +
                    $"{scaled.PartCount} parts. Growth changes a body's size and never its plan.",
                    nameof(scaled));
            }

            shapes = shapes ?? PartShapeRegistry.Standard;

            for (int i = 0; i < scaled.PartCount; i++)
            {
                PhenotypePart part = scaled.Parts[i];
                ArticulationBody body = instance.Bodies[i];
                if (body == null) continue;

                ResizeColliderAndVisual(body.gameObject, part, shapes.Resolve(part.ShapeId));

                // The same two lines Build uses, in the same order. FluidModel.EffectiveMass is
                // the identity at coefficient 0, which is every run through round 28, so this is
                // called unconditionally rather than branching on the coefficient.
                float mass = Mathf.Max(0.001f, part.Volume * DensityKgPerM3);
                body.mass = fluid == null ? mass : FluidModel.EffectiveMass(mass, part.Volume, fluid);

                // Anchors only, and the rotations are left alone: a joint frame's orientation is
                // a property of the plan and growth does not touch the plan. The root has no
                // joint, so it has no anchors to move.
                if (!part.IsRoot)
                {
                    body.anchorPosition = part.ChildAnchorLocal.ToVector3();
                    body.parentAnchorPosition = part.ParentAnchorLocal.ToVector3();
                }
            }

            instance.Phenotype = scaled;

            // logbook/specs/throw-trace-spec.md step 1, after the new phenotype is in place
            // because the parentage this reads is the phenotype's. Growth scales every part by
            // one length, so a ratio is ordinarily invariant across a resize — the reading is
            // taken anyway, because the mass floor in Build and the effective-mass inflation
            // above are both size-dependent and neither is invariant.
            MeasureJointMassRatios(instance);

            // Dropped rather than rebuilt here, so that FluidEnvironment.EnsurePanels stays the
            // one place a panel set is made. Two places building panels is how a resolution
            // change stops reaching the thing it configures, which is this project's oldest
            // recurring fault (logbook/0007, 0008, 0013).
            instance.DragPanels = null;
            instance.DragPanelsPerAxis = 0;
        }

        /// <summary>
        /// Paints each part by what it is made of, or restores the plain look — DESIGN.md §5A.1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>A view mode, not the creature's appearance, and the distinction is load-bearing.</b>
        /// §5A.5 makes part colour an <i>evolvable genome field</i> — inert until creatures can
        /// see, and then a channel for camouflage, warning colouration, mimicry and display.
        /// Painting parts by cell type in the ordinary view would spend exactly the channel that
        /// trait needs, and would have to be taken away again when it lands. So it is offered as
        /// a mode that can be turned off, and it is never what a creature looks like.
        /// </para>
        /// <para>
        /// Applied through a <see cref="MaterialPropertyBlock"/> so every part still shares one
        /// material and one draw-call batch. Instancing a material per part would break batching
        /// for a debug view, which is the wrong trade at any population worth watching.
        /// </para>
        /// </remarks>
        public static void ApplyCellTypeColours(
            CreatureInstance creature, bool on, CellTypeRegistry cellTypes = null)
        {
            if (creature?.Renderers == null) return;
            cellTypes = cellTypes ?? CellTypeRegistry.Standard;

            var block = new MaterialPropertyBlock();

            for (int i = 0; i < creature.Renderers.Length; i++)
            {
                MeshRenderer renderer = creature.Renderers[i];
                if (renderer == null) continue;

                Color colour = Color.white;
                if (on)
                {
                    Float3 rgb = cellTypes
                        .Resolve(creature.Phenotype.Parts[creature.RendererPart[i]].CellTypeId)
                        .InspectionColour;

                    colour = new Color(rgb.X, rgb.Y, rgb.Z, 1f);
                }

                renderer.GetPropertyBlock(block);

                // URP Lit reads _BaseColor and the built-in Standard shader reads _Color. Setting
                // both costs nothing and means the view works whichever pipeline resolved.
                block.SetColor(BaseColorId, colour);
                block.SetColor(ColorId, colour);
                renderer.SetPropertyBlock(block);
            }
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        private static readonly List<int> _rendererPart = new List<int>();
        private static int _partBeingBuilt;

        /// <summary>
        /// Gives the part a collider matching its shape, and a renderable child that draws the
        /// same solid — DESIGN.md §4.1.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every dimension here comes from the <see cref="PartShape"/> rather than from a formula
        /// repeated locally. Three things have to agree about how large a part is — the collider
        /// PhysX pushes with, the mesh a viewer sees, and the panels
        /// <see cref="FluidModel"/> pushes on — and only the first two are visible. A collider
        /// sized from an independently-derived radius that drifted would give a creature whose
        /// body and whose hydrodynamics were different objects, and nothing would report it.
        /// </para>
        /// <para>
        /// The visual is a CHILD, so its scale never reaches the transform PhysX positions the
        /// link by — see the note in <see cref="Build"/> on shear.
        /// </para>
        /// </remarks>
        private static void AddColliderAndVisual(GameObject go, PhenotypePart part, PartShape shape)
        {
            SizeCollider(go, part, shape, create: true);

            int count = VisualPlan(part, shape);
            for (int v = 0; v < count; v++)
            {
                AddMesh(go.transform, _visualMesh[v], _visualOffset[v], _visualScale[v]);
            }
        }

        /// <summary>
        /// Puts an existing part's collider and visual at the size <paramref name="part"/> now
        /// says it is. The geometry half of <see cref="Resize"/>.
        /// </summary>
        /// <remarks>
        /// <b>Shares <see cref="SizeCollider"/> and <see cref="VisualPlan"/> with the build, and
        /// that is the point.</b> Three things have to agree about how large a part is (see
        /// <see cref="AddColliderAndVisual"/>'s note), and a second copy of the arithmetic here
        /// would let a grown creature's collider and its drawing drift apart with nothing
        /// reporting it. So the build creates what this one edits, from one description.
        /// <para>
        /// The visuals are found by walking the part's own children and taking the ones that draw
        /// something. A part's children are its visuals and its child <i>parts</i>, and only the
        /// visuals carry a <see cref="MeshFilter"/>; they were added before any child part was
        /// parented, so they come first and in the order <see cref="VisualPlan"/> lists them.
        /// </para>
        /// </remarks>
        private static void ResizeColliderAndVisual(GameObject go, PhenotypePart part, PartShape shape)
        {
            SizeCollider(go, part, shape, create: false);

            int count = VisualPlan(part, shape);
            Transform t = go.transform;
            int v = 0;

            for (int k = 0; k < t.childCount && v < count; k++)
            {
                Transform child = t.GetChild(k);
                if (child.GetComponent<MeshFilter>() == null) continue;

                child.localPosition = _visualOffset[v];
                child.localScale = _visualScale[v];
                v++;
            }
        }

        /// <summary>Sizes the part's collider, adding it when the part is being built.</summary>
        private static void SizeCollider(GameObject go, PhenotypePart part, PartShape shape, bool create)
        {
            Float3 h = part.HalfExtents;

            switch (shape)
            {
                case SphereShape _:
                {
                    SphereCollider sphere =
                        create ? go.AddComponent<SphereCollider>() : go.GetComponent<SphereCollider>();
                    if (sphere != null) sphere.radius = SphereShape.Radius(h);
                    break;
                }

                case CapsuleShape _:
                {
                    CapsuleCollider capsule =
                        create ? go.AddComponent<CapsuleCollider>() : go.GetComponent<CapsuleCollider>();
                    if (capsule == null) break;

                    capsule.direction = 1;                 // Y, matching CapsuleShape
                    capsule.radius = CapsuleShape.Radius(h);
                    capsule.height =                       // Unity's height includes the caps
                        2f * (CapsuleShape.HalfSpan(h) + CapsuleShape.Radius(h));
                    break;
                }

                default:
                {
                    BoxCollider box =
                        create ? go.AddComponent<BoxCollider>() : go.GetComponent<BoxCollider>();
                    if (box != null)
                    {
                        box.size = new Vector3(
                            2f * Mathf.Abs(h.X), 2f * Mathf.Abs(h.Y), 2f * Mathf.Abs(h.Z));
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// The meshes one part draws with, and where each sits, filled into the scratch arrays
        /// and returned as a count.
        /// </summary>
        /// <remarks>
        /// A capsule is drawn as a cylinder plus two spheres rather than as Unity's capsule
        /// primitive. That primitive is a fixed 1 wide by 2 tall, so making it the right length
        /// and the right width needs a non-uniform scale, which stretches the hemispherical caps
        /// into ellipsoids — the rendered part stops matching its own collider, by an amount that
        /// grows the further the capsule is from twice-as-long-as-wide. Three uniformly-scaled
        /// primitives are exact, and cost two extra renderers on a quarter of parts.
        /// <para>
        /// Static scratch rather than a returned array, because this is called once per part on
        /// every resize of every growing body and a new array each time would be garbage in a
        /// loop that runs over thousands of creatures.
        /// </para>
        /// </remarks>
        private static int VisualPlan(PhenotypePart part, PartShape shape)
        {
            EnsureAssets();

            Float3 h = part.HalfExtents;

            switch (shape)
            {
                case SphereShape _:
                {
                    float r = SphereShape.Radius(h);
                    _visualMesh[0] = _sphereMesh;
                    _visualOffset[0] = Vector3.zero;
                    _visualScale[0] = Vector3.one * (2f * r);
                    return 1;
                }

                case CapsuleShape _:
                {
                    float r = CapsuleShape.Radius(h);
                    float span = CapsuleShape.HalfSpan(h);
                    int n = 0;

                    if (span > 0f)
                    {
                        _visualMesh[n] = _cylinderMesh;
                        _visualOffset[n] = Vector3.zero;
                        _visualScale[n] = new Vector3(2f * r, span, 2f * r);
                        n++;
                    }

                    _visualMesh[n] = _sphereMesh;
                    _visualOffset[n] = new Vector3(0f, span, 0f);
                    _visualScale[n] = Vector3.one * (2f * r);
                    n++;

                    _visualMesh[n] = _sphereMesh;
                    _visualOffset[n] = new Vector3(0f, -span, 0f);
                    _visualScale[n] = Vector3.one * (2f * r);
                    n++;

                    return n;
                }

                default:
                {
                    var full = new Vector3(
                        2f * Mathf.Abs(h.X), 2f * Mathf.Abs(h.Y), 2f * Mathf.Abs(h.Z));

                    _visualMesh[0] = _cubeMesh;
                    _visualOffset[0] = Vector3.zero;
                    _visualScale[0] = full;
                    return 1;
                }
            }
        }

        private static readonly Mesh[] _visualMesh = new Mesh[3];
        private static readonly Vector3[] _visualOffset = new Vector3[3];
        private static readonly Vector3[] _visualScale = new Vector3[3];

        private static Mesh _cubeMesh;
        private static Mesh _sphereMesh;
        private static Mesh _cylinderMesh;
        private static Material _partMaterial;

        private static void AddMesh(Transform parent, Mesh mesh, Vector3 offset, Vector3 scale)
        {
            var visual = new GameObject("Visual") { layer = CreatureLayer };
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = offset;
            visual.transform.localScale = scale;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _partMaterial;

            _renderers.Add(renderer);
            _rendererPart.Add(_partBeingBuilt);
        }

        private static void EnsureAssets()
        {
            if (_cubeMesh != null) return;

            _cubeMesh = PrimitiveMesh(PrimitiveType.Cube);
            _sphereMesh = PrimitiveMesh(PrimitiveType.Sphere);
            _cylinderMesh = PrimitiveMesh(PrimitiveType.Cylinder);

            // Resolve the lit shader by name rather than taking the primitive's material.
            // CreatePrimitive hands back the built-in Standard material, which renders as
            // magenta under URP — the classic "everything is pink" symptom. Looking the
            // shader up keeps Evosim.Sim from depending on the URP assemblies at all.
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");

            _partMaterial = new Material(shader) { name = "Evosim Part" };
        }

        /// <summary>Borrows a primitive's mesh and takes the primitive straight back out.</summary>
        /// <remarks>
        /// Immediate in both modes, for <see cref="PhenotypeInstance.Destroy"/>'s reason and one
        /// sharper. <c>CreatePrimitive</c> hands back a GameObject with a collider on it at the
        /// origin, and this runs the first time anything builds a body, which in the theatre is
        /// inside the first physics step: a deferred destroy would leave a cube, a sphere and a
        /// cylinder standing in the water for the rest of that frame, in a box the creatures are
        /// swimming in. The mesh itself is a built-in asset and outlives the object it came from.
        /// </remarks>
        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;

            Object.DestroyImmediate(temp);

            return mesh;
        }

        /// <summary>
        /// Maps a genome joint type onto an <see cref="ArticulationJointType"/> and its DOF locks.
        /// </summary>
        /// <remarks>
        /// PhysX articulations offer fixed, prismatic, revolute and spherical joints, so the
        /// seven types from [K12 §2.1, p.3] map onto three: fixed, 1-DOF revolute, and
        /// spherical with the unwanted swings locked. Spike 01's M4 confirmed articulations
        /// hold under full-amplitude actuation on all DOF without joint separation.
        ///
        /// KNOWN GAP: a revolute joint rotates about the anchor frame's X axis, so Hinge and
        /// Twist differ only in how that frame is oriented, and mirrored parts
        /// (<see cref="PhenotypePart.Mirrored"/>) need their drive axis flipped to move as a
        /// mirror image rather than in parallel. Neither matters until actuation is driven by
        /// the brain graph rather than a test signal; both are resolved at Milestone 3.
        /// </remarks>
        private static void ConfigureJoint(ArticulationBody body, PhenotypePart part, PhenotypePart parentPart)
        {
            // Express one physical joint frame in both bodies' local coordinates. The child's
            // own frame is used, so its anchor rotation is identity and the parent's carries
            // the relative rotation between the two parts.
            Quaternion relative =
                Quaternion.Inverse(parentPart.Rotation.ToQuaternion()) * part.Rotation.ToQuaternion();

            Quaternion frame = JointFrameRotation(part.JointType);

            body.matchAnchors = false;
            body.anchorPosition = part.ChildAnchorLocal.ToVector3();
            body.anchorRotation = frame;
            body.parentAnchorPosition = part.ParentAnchorLocal.ToVector3();
            body.parentAnchorRotation = relative * frame;

            switch (part.JointType)
            {
                case JointType.Fixed:
                    body.jointType = ArticulationJointType.FixedJoint;
                    break;

                case JointType.Hinge:
                case JointType.Twist:
                    body.jointType = ArticulationJointType.RevoluteJoint;
                    body.twistLock = ArticulationDofLock.LimitedMotion;
                    body.xDrive = MakeDrive(Limit(part, 0));
                    break;

                case JointType.HingeTwist:
                case JointType.TwistHinge:
                case JointType.Universal:
                    body.jointType = ArticulationJointType.SphericalJoint;
                    body.swingYLock = ArticulationDofLock.LimitedMotion;
                    body.swingZLock = ArticulationDofLock.LockedMotion;
                    body.twistLock = ArticulationDofLock.LimitedMotion;
                    body.xDrive = MakeDrive(Limit(part, 0));
                    body.yDrive = MakeDrive(Limit(part, 1));
                    break;

                case JointType.Spherical:
                    body.jointType = ArticulationJointType.SphericalJoint;
                    body.swingYLock = ArticulationDofLock.LimitedMotion;
                    body.swingZLock = ArticulationDofLock.LimitedMotion;
                    body.twistLock = ArticulationDofLock.LimitedMotion;
                    body.xDrive = MakeDrive(Limit(part, 0));
                    body.yDrive = MakeDrive(Limit(part, 1));
                    body.zDrive = MakeDrive(Limit(part, 2));
                    break;
            }
        }

        private static Quaternion JointFrameRotation(JointType type)
        {
            // Revolute rotates about the frame's X axis. Attachment is face-to-face along
            // some axis, so a "twist" should spin about that axis (identity frame) while a
            // "hinge" should bend across it.
            return type == JointType.Hinge ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
        }

        private static Float2 Limit(PhenotypePart part, int dof) =>
            dof < part.JointLimits.Length ? part.JointLimits[dof] : new Float2(-1f, 1f);

        private static ArticulationDrive MakeDrive(Float2 limit) => new ArticulationDrive
        {
            lowerLimit = limit.X * Mathf.Rad2Deg,
            upperLimit = limit.Y * Mathf.Rad2Deg,

            // Zero stiffness: DESIGN.md §4.4 applies effector output as TORQUE, not as a
            // position target. Damping is small and non-zero so undriven joints settle
            // instead of ringing.
            stiffness = 0f,
            damping = 1f,
            forceLimit = float.MaxValue,
            target = 0f,
        };
    }
}

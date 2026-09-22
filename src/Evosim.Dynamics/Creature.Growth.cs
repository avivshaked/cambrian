using System;
using System.Collections.Generic;
using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// Growth: a living body put at the size Core says it now is, without being rebuilt —
    /// the port of <c>PhenotypeBuilder.Resize</c> and of what <c>Ecosystem.ApplyGrowth</c>
    /// does around it (fable-propose-growth.md rule 8, D087, logbook/0081).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How Core produces a body at a fraction.</b> A creature develops <i>once</i>, at its
    /// genome's <c>AdultScale</c>, and keeps that <see cref="Phenotype"/> as its
    /// <c>Organism.AdultPhenotype</c> for life. <c>World.Grow</c> then spends reserve into
    /// tissue, takes the volume fraction <c>f = TissueJoules / AdultTissueJoules</c>, and sets
    /// <c>Organism.Phenotype = AdultPhenotype.Scaled(f^(1/3))</c> — a fresh copy of the adult
    /// with every length (half-extents, position, both anchors) multiplied by one scalar, every
    /// volume and surface re-measured by the part's own shape, and the plan, the cell types, the
    /// joints, the limits, the power, the lift and the neurons carried across untouched. The
    /// developer is never re-run, so a part cannot appear or disappear as a body grows, and the
    /// part count and the joint types are invariants of the creature's whole life. This method
    /// consumes that scaled phenotype; it never scales anything itself.
    /// </para>
    /// <para>
    /// <b>What is held fixed in world space: the root, and nothing else.</b> The farm keeps the
    /// root link's transform where it is and lets PhysX re-derive every other link from the
    /// anchors it has just moved, which is why <c>Ecosystem</c> measures the root's displacement
    /// across a resize (<c>MaxResizeJumpMetres</c>) rather than asserting it. Here the same
    /// choice is structural: <see cref="BasePosition"/> and <see cref="BaseRotation"/> are not
    /// written at all, so the jump is exactly zero rather than merely small, and
    /// <see cref="Kinematics.Poses"/> walks the new anchors outward from the root. Every
    /// non-root link therefore does move — it must, since the body is a different size — and the
    /// centre of mass moves with it.
    /// </para>
    /// <para>
    /// <b>Momentum jumps, deliberately.</b> An articulation's state is the root's pose and
    /// velocity plus the joint coordinates and their rates, and PhysX keeps all four across a
    /// mass and anchor change; the link velocities and the momentum are derived, so they change.
    /// This mirrors that: <see cref="Q"/>, <see cref="Qd"/>, <see cref="BallRotation"/> and the
    /// root's own <see cref="Velocity"/> and <see cref="Spin"/> are preserved to the bit, and
    /// <see cref="Kinematics.Velocities"/> re-derives the rest, so linear momentum jumps with the
    /// mass and angular momentum with the geometry. Nothing here conserves either, and nothing in
    /// the farm ever did: growth adds tissue that was bought from the energy ledger, and the
    /// ledger — not the solver — is where that transfer is accounted for.
    /// </para>
    /// <para>
    /// <b>What is preserved.</b> Everything that is not a function of size: the base pose and
    /// base velocity, the joint coordinates and rates, a ball joint's rotation, the brain and its
    /// internal state, the drive's ten-sample smoothing history and cursor, both limiter counters,
    /// <see cref="Alive"/>, and the identity. Rebuilding the creature instead would reset a
    /// growing body to rest every growth step, which is the whole reason this method exists.
    /// </para>
    /// </remarks>
    public sealed partial class Creature
    {
        /// <summary>
        /// The world and the shape registry this body was built with, so that a resize cannot be
        /// handed a different one. Assigned once, in the constructor.
        /// </summary>
        private SolverConfig _config;

        private PartShapeRegistry _shapes;

        /// <summary>Times this body has been resized. Diagnostic; never read by the step.</summary>
        public long Resizes { get; private set; }

        /// <summary>
        /// The body fraction the harness has applied to this creature — <c>Body.AppliedBodyFraction</c>
        /// in the Unity farm.
        /// </summary>
        /// <remarks>
        /// Bookkeeping for the farm loop and nothing the solver reads: a growth step compares
        /// <c>Organism.BodyFraction</c> with this, resizes only on a difference, and writes the new
        /// value back. It lives here because the port has no <c>Body</c> class to hang it on. The
        /// default of 1 is the adult, which is what a creature built from an unscaled phenotype is.
        /// </remarks>
        public float AppliedBodyFraction { get; set; } = 1f;

        /// <summary>
        /// One reusable panel list per thread. A resize is serial in the farm — it runs between
        /// steps, after the world has stepped — and a build may happen on any thread, so the
        /// scratch is thread-static rather than shared or per-creature.
        /// </summary>
        [ThreadStatic] private static List<DragPanel> _panelScratch;

        private static List<DragPanel> PanelScratch =>
            _panelScratch ?? (_panelScratch = new List<DragPanel>(64));

        /// <summary>
        /// Puts this living body at the size <paramref name="scaled"/> says it is, writing every
        /// quantity a fresh build at that size would write and leaving every quantity a fresh
        /// build would destroy.
        /// </summary>
        /// <param name="scaled">
        /// <c>Organism.Phenotype</c> — the adult already scaled by Core. Its parts are in the
        /// adult's order and count, so part <i>i</i> here is link <i>i</i> in this body.
        /// </param>
        /// <remarks>
        /// <b>Call it between steps, never inside the parallel phase.</b> It commits the contact
        /// sphere, which is the one piece of a creature other creatures read, and committing
        /// inside the parallel phase is exactly what made the spike's digest depend on the thread
        /// count. The farm's order is the harness's: step the world's economy, then resize every
        /// body whose fraction has moved, in <c>World.Living</c> order, then take the next physics
        /// step.
        /// </remarks>
        public void Resize(Phenotype scaled)
        {
            if (scaled == null) throw new ArgumentNullException(nameof(scaled));

            // Not defensive padding: every method below indexes one list by the other's position,
            // and a mismatch would silently give link i the size of some other part. Core
            // guarantees the count — a body develops once — so this can only fire if that
            // guarantee breaks. PhenotypeBuilder.Resize refuses the same thing in the same words.
            if (scaled.PartCount != Links)
            {
                throw new ArgumentException(
                    $"A body of {Links} links cannot be resized to a phenotype of " +
                    $"{scaled.PartCount} parts. Growth changes a body's size and never its plan.",
                    nameof(scaled));
            }

            Phenotype = scaled;

            var panelSets = new DragPanelSet[Links];
            int panels = WriteSizedProperties(scaled, _config, _shapes, panelSets, PanelScratch);
            WriteJointLimits(_config);
            WritePanels(panelSets, panels);
            RefreshLinkReach();

            // Torque per unit of signal is read from Power, which growth does not scale — so this
            // is the identity today. It is called anyway, because "what a fresh build would
            // produce" is the acceptance, not "what happens to be invariant this month".
            Drive.Refresh();

            // The root stays where it is and everything else follows the new anchors outward.
            // Velocities: the root's are left alone and every child's is re-derived from Qd, which
            // is PhysX's own treatment of an articulation whose anchors have moved.
            Kinematics.Refresh(this);

            // A resize runs between steps, so the new sphere is committed at once rather than
            // being left pending for the next step's commit: a body that has just doubled in size
            // must be the size it is when the next step's contact grid is built.
            RefreshContactSphere();
            CommitContactSphere();

            Resizes++;
        }

        /// <summary>
        /// The same resize, recording the fraction the harness has now applied.
        /// </summary>
        /// <remarks>
        /// Offered so that the two cannot come apart. <c>Ecosystem.ApplyGrowth</c> writes
        /// <c>body.AppliedBodyFraction = creature.BodyFraction</c> on the line after its resize
        /// call, and a growth step that resized without recording it would resize the same body
        /// again on every step for the rest of its life.
        /// </remarks>
        public void Resize(Phenotype scaled, float bodyFraction)
        {
            Resize(scaled);
            AppliedBodyFraction = bodyFraction;
        }

        /// <summary>
        /// Everything about a link that is a function of its size: volume, lift, power, both
        /// masses, the inertia tensor, both joint anchors, both joint frames, and the link's drag
        /// panels. Returns the body's total panel count.
        /// </summary>
        /// <remarks>
        /// Moved here verbatim from the constructor so that the build and the resize are one
        /// derivation rather than two. Three things have to agree about how large a part is (the
        /// mass, the drag panels and the anchors the kinematics walk), and a second copy of the
        /// arithmetic is how they stop agreeing — this project's oldest recurring fault
        /// (logbook/0007, 0008, 0013).
        /// </remarks>
        private int WriteSizedProperties(
            Phenotype phenotype,
            SolverConfig config,
            PartShapeRegistry shapes,
            DragPanelSet[] panelSets,
            List<DragPanel> scratch)
        {
            double totalVolume = 0, totalMass = 0;
            int panels = 0;

            for (int i = 0; i < Links; i++)
            {
                PhenotypePart part = phenotype.Parts[i];
                PartShape shape = shapes.Resolve(part.ShapeId);

                double volume = part.Volume;
                Volume[i] = volume;
                Lift[i] = part.Lift;
                Power[i] = part.Power;
                totalVolume += volume;

                // The builder's own two lines: the plain mass with its floor, then the effective
                // mass once the water a part drags along is folded in.
                double plain = System.Math.Max(config.MinimumLinkMass, volume * config.TissueDensity);
                double effective = plain + config.AddedMassCoefficient * config.Density * volume;
                PlainMass[i] = plain;
                Mass[i] = effective;
                totalMass += effective;

                // PhysX recomputes a link's tensor from its collider whenever the mass is written,
                // so an inflated mass inflates the tensor in proportion. The same is done here,
                // from an analytic tensor rather than from the engine's — see the report.
                Vec3 diag = InertiaOf(shape, part.HalfExtents, plain) * (effective / plain);
                Vec3.Write(InertiaLocal, 3 * i, diag);
                SmallestInertia[i] = System.Math.Min(diag.X, System.Math.Min(diag.Y, diag.Z));

                Vec3.Write(ChildAnchor, 3 * i, ToVec(part.ChildAnchorLocal));
                Vec3.Write(ParentAnchor, 3 * i, ToVec(part.ParentAnchorLocal));

                QuatD frame = FrameOf(part.JointType);
                QuatD rest = QuatD.Identity;

                if (!part.IsRoot)
                {
                    QuatD parentRotation = QuatD.From(phenotype.Parts[part.ParentIndex].Rotation);
                    QuatD own = QuatD.From(part.Rotation);
                    QuatD relative = parentRotation.Conjugate * own;
                    rest = relative * frame;
                }

                QuatD.Write(JointFrame, 4 * i, frame);
                QuatD.Write(RestFrame, 4 * i, rest);

                panelSets[i] = DragPanelSet.For(shape, part.HalfExtents, config.PanelsPerAxis, scratch);
                panels += panelSets[i].Count;
            }

            TotalVolume = totalVolume;
            TotalMass = totalMass;

            return panels;
        }

        /// <summary>
        /// The joint stops and the springs that hold them. Call after
        /// <see cref="WriteSizedProperties"/>: the spring constants are a function of the inertia
        /// and the anchors it has just written, which is the whole reason a resize has to redo them.
        /// </summary>
        private void WriteJointLimits(SolverConfig config)
        {
            for (int i = 0; i < Links; i++)
            {
                int n = DofCount[i];
                if (n == 0) continue;

                PhenotypePart part = Phenotype.Parts[i];
                int parent = Parent[i];
                int at = DofStart[i];
                QuatD frame = QuatD.Read(JointFrame, 4 * i);
                QuatD rest = QuatD.Read(RestFrame, 4 * i);
                Vec3 childAnchor = Vec3.Read(ChildAnchor, 3 * i);
                Vec3 parentAnchor = Vec3.Read(ParentAnchor, 3 * i);

                double omega = config.JointLimitOmegaTimesStep / config.StepSeconds;

                for (int d = 0; d < n; d++)
                {
                    // PhenotypeBuilder.Limit: a missing entry is [-1, 1] radians. The stops
                    // themselves are angles, not lengths, so growth does not move them — but they
                    // are written again anyway, for the same reason the drive's torque is.
                    Float2 limit = d < part.JointLimits.Length
                        ? part.JointLimits[d]
                        : new Float2(-1f, 1f);

                    LimitLo[at + d] = limit.X;
                    LimitHi[at + d] = limit.Y;

                    // The two-body reduced inertia about this axis through the anchor, taken at
                    // the rest configuration. It is a LOWER bound on the inertia the relative
                    // coordinate actually carries — anything hung off either side raises it —
                    // and a penalty spring scaled by a lower bound can only be softer than the
                    // step allows, never stiffer. Scaling by the child's own inertia instead,
                    // which is the obvious thing and what this did first, makes the spring too
                    // stiff whenever the parent is light: the damping term's explicit stability
                    // condition is violated and a driven chain rings itself to infinity within
                    // two seconds.
                    double child = AxisInertia(
                        frame.Rotate(AxisOf(d)), childAnchor,
                        Vec3.Read(InertiaLocal, 3 * i), Mass[i]);

                    double above = AxisInertia(
                        rest.Rotate(AxisOf(d)), parentAnchor,
                        Vec3.Read(InertiaLocal, 3 * parent), Mass[parent]);

                    double reduced = child + above > 0 ? child * above / (child + above) : child;

                    LimitStiffness[at + d] = reduced * omega * omega;
                    LimitDamping[at + d] = 2.0 * config.JointLimitDampingRatio * reduced * omega;
                }
            }
        }

        /// <summary>
        /// Flattens the per-link panel sets into the body's three panel arrays and its index.
        /// </summary>
        /// <remarks>
        /// <b>In place whenever the count is unchanged, which is every resize a healthy body
        /// takes.</b> A panel's area is the part's own area scaled by the square of one length, so
        /// a panel that had area has area at any size: the count is invariant under a uniform
        /// scaling for all three standard shapes, and the branches that could change it
        /// (<c>r &lt;= 0</c>, <c>span &gt; 0</c>, <c>area &lt;= 0</c>) are all comparisons that a
        /// positive scale preserves. All of that is an argument, not a proof — a float product can
        /// underflow where the real number does not — so the arrays are reallocated rather than
        /// overrun when the count moves, and the farm keeps running. <see cref="PanelStart"/> is
        /// always rewritten in place: its length is the link count, which growth cannot change.
        /// </remarks>
        private void WritePanels(DragPanelSet[] panelSets, int panels)
        {
            if (PanelArea == null || PanelArea.Length != panels)
            {
                PanelCentre = new double[3 * panels];
                PanelNormal = new double[3 * panels];
                PanelArea = new double[panels];
            }

            int cursor = 0;
            for (int i = 0; i < Links; i++)
            {
                PanelStart[i] = cursor;
                DragPanelSet set = panelSets[i];
                for (int p = 0; p < set.Count; p++)
                {
                    Vec3.Write(PanelCentre, 3 * cursor, ToVec(set.Centres[p]));
                    Vec3.Write(PanelNormal, 3 * cursor, ToVec(set.Normals[p]));
                    PanelArea[cursor] = set.Areas[p];
                    cursor++;
                }
            }
            PanelStart[Links] = cursor;
        }

        /// <summary>
        /// The contact sphere's per-link padding, rewritten from the new half-extents.
        /// </summary>
        /// <remarks>
        /// Nothing to do when the cache has never been built: <see cref="LinkReach"/> builds it
        /// from <see cref="Phenotype"/>, which is already the scaled one by the time this runs.
        /// </remarks>
        private void RefreshLinkReach()
        {
            if (_linkReach == null) return;

            for (int i = 0; i < Links; i++)
            {
                Float3 h = Phenotype.Parts[i].HalfExtents;
                _linkReach[i] = System.Math.Sqrt(
                    (double)h.X * h.X + (double)h.Y * h.Y + (double)h.Z * h.Z);
            }
        }
    }
}

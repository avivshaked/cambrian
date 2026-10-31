using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// One developed body part: a box with a joint to its parent. DESIGN.md §4.2.
    /// </summary>
    /// <remarks>
    /// Parts are emitted in depth-first pre-order, so <see cref="ParentIndex"/> is always
    /// less than <see cref="Index"/>. Evosim.Sim relies on that when building an
    /// <c>ArticulationBody</c> chain, which must be constructed parent-first.
    /// </remarks>
    public sealed class PhenotypePart
    {
        /// <summary>Position in this part list.</summary>
        public int Index { get; internal set; }

        /// <summary>Parent's index, or -1 for the root.</summary>
        public int ParentIndex { get; internal set; }

        /// <summary>Index of the <see cref="MorphNode"/> this part grew from. Many parts may share one node.</summary>
        public int SourceNode { get; internal set; }

        /// <summary>Tree depth, root at 0.</summary>
        public int Depth { get; internal set; }

        /// <summary>Box half-extents in metres, with cumulative scale applied. Always positive.</summary>
        public Float3 HalfExtents { get; internal set; }

        /// <summary>Position in creature-local space.</summary>
        public Float3 Position { get; internal set; }

        /// <summary>
        /// How far this part's centre sits from the root's, in metres, in the developed body's own
        /// frame — what D113's support cost is priced on (<c>logbook/specs/support-cost-spec.md</c>).
        /// 0 at the root.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Stored rather than derived, for <see cref="Volume"/>'s reason</b>: the metabolic step
        /// reads it on every part on every step with the price on, and a square root a part a step
        /// is a cost the bill does not need. <see cref="Developer.Develop"/> sets it once the body
        /// is whole, from the root part's position rather than the frame's origin, so a body
        /// developed under a root transform pays the same as one developed at the origin.
        /// <see cref="Phenotype.Scaled"/> scales it with the half-extents, so a growing body pays
        /// on the distance its grown parts stand at; a cut (<see cref="Phenotype.WithoutSubtrees"/>)
        /// moves no survivor and carries it as it is.
        /// </para>
        /// <para>
        /// The rest pose's distance, not the pose the solver holds: a joint that folds a limb
        /// towards the root does not make the limb cheaper to grow and keep. A mirrored part reads
        /// its twin's distance, since a reflection preserves lengths.
        /// </para>
        /// </remarks>
        public float DistanceFromRoot { get; internal set; }

        /// <summary>Orientation in creature-local space. Always a proper rotation.</summary>
        public Quat Rotation { get; internal set; }

        /// <summary>
        /// True when an odd number of reflections was applied on the path to this part.
        /// </summary>
        /// <remarks>
        /// A box is symmetric under the axis flip used to recover a proper rotation, so
        /// geometry is unaffected — but joint axis conventions are not, and a mirrored limb
        /// needs its hinge axis flipped to move as a mirror image rather than in parallel.
        /// Evosim.Sim consumes this. In the dynamics solver, <see cref="ReflectedDrive"/> says
        /// whether that flip is on.
        /// </remarks>
        public bool Mirrored { get; internal set; }

        /// <summary>
        /// Whether an engine reflects this part's joint drive with its geometry (D234): true for a
        /// <see cref="Mirrored"/> part grown with <see cref="DevelopmentLimits.ReflectMirroredAnchors"/> on,
        /// false for every other part. A reflected part's signed joint quantities (angle, rate, target,
        /// torque) are read negated, on the degrees of freedom whose axis the reflection reverses, so the
        /// same brain signal moves a twin as the mirror image of its original rather than against it.
        /// </summary>
        /// <remarks>
        /// Set where the anchors are set, from the same switch, so that the part's pose and its drive are
        /// reflected together or not at all.
        /// </remarks>
        public bool ReflectedDrive { get; internal set; }

        /// <summary>What this part is made of — <see cref="CellType.Id"/>, DESIGN.md §5A.1.</summary>
        public string CellTypeId { get; internal set; } = CellTypeIds.Structural;

        /// <summary>Geometry — <see cref="PartShape.Id"/>, DESIGN.md §4.1.</summary>
        public string ShapeId { get; internal set; } = ShapeIds.Box;

        /// <summary>
        /// The genes the shape reads beside the half-extents — <see cref="MorphNode.Form"/>, D233.
        /// <see cref="PartForm.None"/> on a part of a shape that takes none.
        /// </summary>
        public PartForm Form { get; internal set; }

        /// <summary>
        /// The superellipsoid and its panels at one size, shared by this part and every scaled copy of
        /// it, or null on a part of a named shape. Set exactly when the shape takes a form.
        /// </summary>
        public FamilyGeometry Family { get; internal set; }

        /// <summary>
        /// This part's half-extents over <see cref="FamilyGeometry.ReferenceHalfExtents"/>: 1 on the
        /// part the developer built, the length ratio of <see cref="Phenotype.Scaled"/> on a copy.
        /// Left at 1 on a part of a named shape, which has no family to scale.
        /// </summary>
        public float FamilyScale { get; internal set; } = 1f;

        /// <summary>Joint to the parent. Meaningless at the root, where it is <see cref="JointType.Fixed"/>.</summary>
        public JointType JointType { get; internal set; } = JointType.Fixed;

        /// <summary>Peak joint torque in newton-metres — links only. See <see cref="MorphNode.Power"/>.</summary>
        public float Power { get; internal set; }

        /// <summary>The joint's threshold and command envelope, links only. See <see cref="MorphNode.Drive"/>.</summary>
        public DriveGenes Drive { get; internal set; }

        /// <summary>
        /// Weight this part cancels, kg/m³ of displaced water — buoyancy cells only. See
        /// <see cref="MorphNode.Lift"/>.
        /// </summary>
        public float Lift { get; internal set; }

        /// <summary>
        /// Damage this part does per second per square metre of its own area to a part of another
        /// body in contact — <see cref="MorphNode.Attack"/>, D106 item 3.
        /// </summary>
        /// <remarks>
        /// <b>Carried onto the part the way <see cref="Power"/> and <see cref="Lift"/> are, and
        /// for their reason.</b> Everything that prices or applies an attribute walks a developed
        /// body rather than a genome — the metabolic step, the mouth's damage pass, the ledger —
        /// and asking the genome would mean carrying a node index and a genome reference into
        /// every one of them. The four are not scaled by <see cref="Phenotype.Scaled"/>: they are
        /// rates per square metre and per cubic metre, so they already mean the same thing at any
        /// size, exactly as <see cref="Power"/> and <see cref="Lift"/> do.
        /// </remarks>
        public float Attack { get; internal set; }

        /// <summary>
        /// Charged units this part takes per second per square metre from a corpse in reach —
        /// <see cref="MorphNode.Intake"/>. See <see cref="Attack"/>.
        /// </summary>
        public float Intake { get; internal set; }

        /// <summary>
        /// Damage per second per square metre this part absorbs before its health suffers —
        /// <see cref="MorphNode.Protection"/>. See <see cref="Attack"/>.
        /// </summary>
        public float Protection { get; internal set; }

        /// <summary>
        /// Health per cubic metre, relative to the neutral 1 — <see cref="MorphNode.Toughness"/>.
        /// A part's health pool is its volume times this times
        /// <see cref="RunConfig.HealthPerCubicMetre"/>. See <see cref="Attack"/>.
        /// </summary>
        public float Toughness { get; internal set; } = 1f;

        /// <summary>
        /// Where this part floats from, along its thinnest axis, as a fraction of that
        /// half-extent — <see cref="MorphNode.BuoyancyOffset"/>, D111.
        /// </summary>
        /// <remarks>
        /// A fraction and not metres, so growth and <see cref="Phenotype.Scaled"/> leave it as it
        /// is; the solver multiplies it by the half-extent the part has now. A mirrored part keeps
        /// the value: a reflection carries the thin axis with the part, and the offset face with it.
        /// </remarks>
        public float BuoyancyOffset { get; internal set; }

        /// <summary>
        /// The part's thinnest axis in its own frame, 0 for x, 1 for y, 2 for z, or -1 for a
        /// sphere, which has none — D111's lever arm.
        /// </summary>
        /// <remarks>
        /// The first of a tie, so a cube takes x: a cube has no thinnest face either, but it is
        /// a box and its offset still turns it, and an arbitrary axis chosen the same way every
        /// time is a body the genome describes. A capsule takes its box's axes, as D099's hull
        /// and D110's exposure do.
        /// </remarks>
        public int ThinAxis
        {
            get
            {
                if (ShapeId == ShapeIds.Sphere) return -1;

                float x = Math.Abs(HalfExtents.X), y = Math.Abs(HalfExtents.Y), z = Math.Abs(HalfExtents.Z);
                if (x <= y && x <= z) return 0;
                return y <= z ? 1 : 2;
            }
        }

        /// <summary>Min/max per DOF, in radians.</summary>
        public Float2[] JointLimits { get; internal set; } = Array.Empty<Float2>();

        /// <summary>
        /// Joint anchor in the parent's local space, in metres. Zero at the root. With
        /// <see cref="DevelopmentLimits.ReflectMirroredAnchors"/> on, in the parent's proper frame
        /// (the frame <see cref="Rotation"/> poses), reflected for a mirrored copy.
        /// </summary>
        public Float3 ParentAnchorLocal { get; internal set; }

        /// <summary>
        /// Joint anchor in this part's local space, in metres. Zero at the root. With
        /// <see cref="DevelopmentLimits.ReflectMirroredAnchors"/> on, in the part's proper frame,
        /// flipped on x for a <see cref="Mirrored"/> part.
        /// </summary>
        public Float3 ChildAnchorLocal { get; internal set; }

        /// <summary>
        /// This part's local brain — the neuron definitions of <see cref="SourceNode"/>.
        /// Shared, not copied: every part grown from one node runs the same controller
        /// definition over its own state, which is what makes a recursive chain a CPG
        /// (DESIGN.md §4.3).
        /// </summary>
        public NeuronDef[] Neurons { get; internal set; } = Array.Empty<NeuronDef>();

        /// <summary>
        /// This part's identity for the one brain of D146: its developer path from the root,
        /// hashed (<see cref="BrainIds.Child"/>). The same part of the same genome has the same
        /// id in every development, at any size and any module count; the old model never reads it.
        /// </summary>
        public ulong PathId { get; internal set; }

        /// <summary>
        /// Whether this part may smell: false on every part that is not a nose, in a body
        /// developed against a table that lists the nose (D235). True everywhere else, which is
        /// every body of every world before the nose.
        /// </summary>
        /// <remarks>
        /// Set by <see cref="Developer.Develop"/> from the table it is handed, and carried by every
        /// copy of the part, so the one brain's ports (<see cref="CentralWiring.IndicesOn"/>) and the
        /// senses read the same rule. The smells are the channels <see cref="SensorChannels.IsSmell"/> names.
        /// </remarks>
        public bool Smells { get; internal set; } = true;

        /// <summary>
        /// Whether the brain holds this part's grip (D268): true on an anchoring part developed against a table
        /// whose anchoring cell has <c>BrainHold</c>, false everywhere else, which is every body of every world
        /// before the build.
        /// </summary>
        /// <remarks>
        /// Set by <see cref="Developer.Develop"/> from the table it is handed, as <see cref="Smells"/> is, and
        /// carried by every copy of the part. The one brain's hold output and the two grip senses follow it
        /// (<see cref="CentralWiring"/>).
        /// </remarks>
        public bool Grips { get; internal set; }

        /// <summary>Volume in m³, computed by this part's shape when the part was built.</summary>
        /// <remarks>
        /// Stored rather than derived, because deriving it needs the shape registry and this is
        /// read on every part on every step by the metabolic accounting. Since
        /// fable-propose-growth.md rule 5 (2026-09-08) a growing body is rebuilt at its new size
        /// rather than edited in place (<see cref="Phenotype.Scaled"/>), so this is still written
        /// once for any one part and still measured by the shape rather than scaled arithmetically.
        /// </remarks>
        public float Volume { get; internal set; }

        /// <summary>Total surface area, m². Stored, because a part never changes size.</summary>
        /// <remarks>
        /// Recomputing it means asking the shape for its panels and summing them, which allocates
        /// and costs more than everything else in an energy step put together — the same fault
        /// the fluid model documents and avoids.
        /// <para>
        /// A <i>part</i>'s geometry is still fixed from the moment it is built to the moment the
        /// body holding it is replaced, which is what makes computing this once safe. A
        /// <i>body</i>'s is not, since fable-propose-growth.md rule 5: a creature below its adult
        /// size grows every step, and growth builds a whole new phenotype at the new scale rather
        /// than mutating these fields. Measuring the new part from its shape is the reason the
        /// energy audit does not notice growth happening.
        /// </para>
        /// </remarks>
        public float SurfaceArea { get; internal set; }

        /// <summary>
        /// Area light can fall on, m² — a quarter of the surface. DESIGN.md §5A.1.
        /// </summary>
        /// <remarks>
        /// The quarter is not a fudge: for any convex body the average projected area over all
        /// orientations is exactly one quarter of its surface area (Cauchy's formula), so this is
        /// the orientation-averaged answer rather than an estimate. Using the full surface area
        /// would let a creature collect light on faces pointing away from it, which is free energy
        /// of the kind §11.2 exists to catch.
        /// </remarks>
        public float LitArea => SurfaceArea * 0.25f;

        /// <summary>
        /// This part's exposure factor in a pose: its projected area onto the horizontal over its
        /// orientation average, <see cref="LitArea"/>. D110, <c>logbook/specs/light-exposure-spec.md</c> §1.
        /// </summary>
        /// <param name="upAlongX">The world's up on the part's own x axis, <c>Rx · up</c>; the sign is ignored.</param>
        /// <param name="upAlongY">The same on its y axis.</param>
        /// <param name="upAlongZ">The same on its z axis.</param>
        /// <remarks>
        /// <para>
        /// <b>2 for a thin sheet lying flat, about 0 on edge, 1 averaged over every pose</b>: a
        /// box's shadow is <c>4·(hy·hz·|ux| + hx·hz·|uy| + hx·hy·|uz|)</c>, the mean of each
        /// <c>|u|</c> over the sphere is a half, and so the mean shadow is a quarter of the surface,
        /// which is Cauchy's formula and what <see cref="LitArea"/> already is.
        /// </para>
        /// <para>
        /// <b>A ratio of the box's own two quantities</b>, so growth, which scales every
        /// half-extent, leaves it unchanged, and a capsule takes the box formula on its
        /// half-extents with the mean still 1 — consistent with D099's hull, which takes a capsule
        /// as its box, rather than exact. A sphere shades the same at every angle and reads 1.
        /// </para>
        /// </remarks>
        public float ExposureFactor(double upAlongX, double upAlongY, double upAlongZ)
        {
            // D233. A family part's shadow is read off its own panels, at resolution 2 and at the
            // reference size: the factor is a ratio of two areas that scale together, so it is the
            // same at every size and a scaled copy reads the panels unscaled. No build per call.
            if (Family != null)
            {
                return Superellipsoid.ExposureFactor(
                    Family.ReferencePanels(FamilyGeometry.MeasureResolution),
                    upAlongX, upAlongY, upAlongZ);
            }

            if (ShapeId == ShapeIds.Sphere) return 1f;

            double hx = Math.Abs(HalfExtents.X);
            double hy = Math.Abs(HalfExtents.Y);
            double hz = Math.Abs(HalfExtents.Z);

            double faces = hx * hy + hy * hz + hx * hz;
            if (!(faces > 0.0)) return 1f;

            double shadow =
                hy * hz * Math.Abs(upAlongX) +
                hx * hz * Math.Abs(upAlongY) +
                hx * hy * Math.Abs(upAlongZ);

            return (float)(2.0 * shadow / faces);
        }

        /// <summary>
        /// <see cref="ExposureFactor(double, double, double)"/> for a part whose own frame stands
        /// at <paramref name="worldRotation"/> — what a test or a Core-only world asks with.
        /// </summary>
        public float ExposureFactor(Quat worldRotation)
        {
            Float3 x = worldRotation.Rotate(new Float3(1f, 0f, 0f));
            Float3 y = worldRotation.Rotate(new Float3(0f, 1f, 0f));
            Float3 z = worldRotation.Rotate(new Float3(0f, 0f, 1f));
            return ExposureFactor(x.Y, y.Y, z.Y);
        }

        /// <summary>
        /// A family part's drag panels at its own size: the shared reference panels, each centre times
        /// <see cref="FamilyScale"/> and each area times its square, normals as they are — D233.
        /// </summary>
        /// <param name="resolution">Panels per axis, as <see cref="PartShape.AddPanels(Float3, int, PartForm, List{DragPanel})"/> takes it.</param>
        /// <param name="into">Appended to, not cleared.</param>
        /// <remarks>
        /// A uniform scaling by s moves a panel's centre by s and its area by s² and leaves its
        /// normal, so this is the surface at the part's size without measuring it. At a scale of 1
        /// the product is exact and the panels are the reference's to the bit.
        /// </remarks>
        public void AddFamilyPanels(int resolution, List<DragPanel> into)
        {
            if (Family == null)
            {
                throw new InvalidOperationException(
                    $"Part {Index} is a '{ShapeId}' part and has no family geometry. A named shape " +
                    "builds its panels from the shape.");
            }

            float scale = FamilyScale;
            float areaScale = scale * scale;
            IReadOnlyList<DragPanel> reference = Family.ReferencePanels(resolution);

            for (int i = 0; i < reference.Count; i++)
            {
                DragPanel panel = reference[i];
                into.Add(new DragPanel(panel.Centre * scale, panel.Normal, panel.Area * areaScale));
            }
        }

        public bool IsRoot => ParentIndex < 0;

        public override string ToString() =>
            $"#{Index} node={SourceNode} d={Depth} half={HalfExtents} {JointType}{(Mirrored ? " mirrored" : "")}";
    }
}

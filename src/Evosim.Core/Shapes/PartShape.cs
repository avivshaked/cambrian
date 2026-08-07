using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// One flat element of a part's surface, used by the drag model — DESIGN.md §5.2.
    /// </summary>
    /// <remarks>
    /// A shape is presented to the fluid as a set of outward-facing panels. That is the only
    /// thing <see cref="FluidModel"/> needs to know about geometry, which is what lets a new
    /// shape be added without touching it: whatever the surface is, it is a bag of panels.
    /// </remarks>
    public readonly struct DragPanel
    {
        /// <summary>Panel centre in the part's local space, metres.</summary>
        public readonly Float3 Centre;

        /// <summary>Outward unit normal in the part's local space.</summary>
        public readonly Float3 Normal;

        /// <summary>Panel area, m². The panels of a shape sum to its surface area.</summary>
        public readonly float Area;

        public DragPanel(Float3 centre, Float3 normal, float area)
        {
            Centre = centre;
            Normal = normal;
            Area = area;
        }
    }

    /// <summary>
    /// The geometry of a part — DESIGN.md §4.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why shapes are worth the cost.</b> Everything was a box, and a population of boxes
    /// looks like a population of boxes. Collision detection against spheres and capsules is
    /// cheaper than against boxes, not dearer, so the price is paid in the drag model — which
    /// now integrates over a panel set instead of six flat faces.
    /// </para>
    /// <para>
    /// <b>Shape decides whether a part can paddle.</b> A box or a capsule presents a different
    /// area broadside than end-on, so sweeping it through water produces sideways force — that
    /// anisotropy is what a paddle is. A sphere has none: every orientation looks identical to
    /// the fluid. A sphere can still contribute thrust, because drag is quadratic in speed and
    /// a fast stroke one way beats a slow return the other, but it cannot paddle, and a creature
    /// made only of spheres is restricted to that weaker mechanism. This is a real prediction of
    /// the model rather than an implementation limit, and it is worth watching for.
    /// </para>
    /// <para>
    /// <b>Half-extents mean different things per shape.</b> The genome carries three numbers and
    /// each shape reads them as it likes — a sphere as one radius, a capsule as a radius and a
    /// length. Every shape must use all three somehow, or mutations along the ignored axes would
    /// be silent, and the extinction-by-shrinking rule in §4.5 (which reads the mean) would
    /// behave differently depending on which shape a node happened to be.
    /// </para>
    /// </remarks>
    public abstract class PartShape
    {
        /// <summary>Stable identifier, serialized into genomes (§9) and the config hash (§7).</summary>
        /// <remarks>A string rather than an enum, for the reason given on <see cref="CellType.Id"/>.</remarks>
        public abstract string Id { get; }

        /// <summary>Volume in m³. Drives mass, upkeep and the minimum-part-volume limit.</summary>
        public abstract float Volume(Float3 halfExtents);

        /// <summary>
        /// Where a child attaches, given an anchor direction with components in [-1, 1].
        /// </summary>
        /// <remarks>
        /// Development places a child so that its anchor meets its parent's (§4.2). For a box
        /// that is a point on a face; for a sphere, a point on the surface in that direction.
        /// The anchor is a <i>direction</i>, and each shape decides where its surface is.
        /// </remarks>
        public abstract Float3 SurfacePoint(Float3 anchor, Float3 halfExtents);

        /// <summary>Whether a point in the part's local space is inside the solid.</summary>
        /// <remarks>
        /// Used by <see cref="PhenotypeGeometry"/> to measure how much of a creature is inside
        /// itself. Overlap between parts that are not jointed is what jams a creature and what
        /// lets it farm depenetration (logbook/0007), so this has to follow the real shape
        /// rather than a bounding box.
        /// </remarks>
        public abstract bool ContainsPoint(Float3 local, Float3 halfExtents);

        /// <summary>
        /// The surface, as outward-facing panels in local space.
        /// </summary>
        /// <param name="halfExtents">Half-extents, in metres. Read differently per shape.</param>
        /// <param name="resolution">
        /// How finely to subdivide. Higher is more faithful and costs linearly; see
        /// <see cref="FluidConfig.PanelsPerAxis"/>.
        /// </param>
        /// <param name="into">Appended to, not cleared. The caller owns the list.</param>
        /// <remarks>
        /// Panel areas must sum to the shape's surface area. They do not have to tile it
        /// exactly — the drag model samples, it does not integrate analytically — but a set that
        /// sums to the wrong total scales every force on that shape, which would make shape
        /// choice a fitness lever rather than a morphological one.
        /// </remarks>
        public abstract void AddPanels(Float3 halfExtents, int resolution, List<DragPanel> into);

        /// <summary>Total surface area for the given half-extents, m².</summary>
        /// <remarks>
        /// Derived from <see cref="AddPanels"/> rather than from a second, analytic formula per
        /// shape. Two independent expressions of the same quantity is how they come to disagree,
        /// and the disagreement would be silent — a capsule whose panels already summed to 79% of
        /// its true area shipped exactly that way (logbook/0009). This cannot drift, because it
        /// is the same sum <c>ShapeTests</c> checks against the analytic answer.
        ///
        /// Allocating and only called once per part, at development. The per-step path reads
        /// <see cref="PhenotypePart.SurfaceArea"/>.
        /// </remarks>
        public float SurfaceArea(Float3 halfExtents, int resolution = 4)
        {
            var panels = new List<DragPanel>();
            AddPanels(halfExtents, resolution < 1 ? 1 : resolution, panels);

            float total = 0f;
            for (int i = 0; i < panels.Count; i++) total += panels[i].Area;
            return total;
        }

        /// <summary>Everything about this shape that changes behaviour, for the config hash (§7).</summary>
        public virtual string HashContribution() => Id;

        public override string ToString() => Id;

        // ---------------------------------------------------------------- helpers

        /// <summary>Mean of the three half-extents. The scale a shape reads when it needs one number.</summary>
        protected static float Mean(Float3 h) =>
            (System.Math.Abs(h.X) + System.Math.Abs(h.Y) + System.Math.Abs(h.Z)) / 3f;

        protected static Float3 Normalize(Float3 v)
        {
            float length = (float)System.Math.Sqrt(Float3.Dot(v, v));
            return length > 1e-9f ? v * (1f / length) : new Float3(1f, 0f, 0f);
        }

        /// <summary>
        /// <paramref name="count"/> roughly-even directions on the unit sphere.
        /// </summary>
        /// <remarks>
        /// A Fibonacci lattice: deterministic, needs no tables, and has no clustering at the
        /// poles the way a latitude/longitude grid does. Pole clustering would put more panels
        /// where the shape has less area and quietly bias drag along that axis — on a shape whose
        /// whole point is that it has <i>no</i> preferred axis.
        /// </remarks>
        protected static Float3 SphereDirection(int index, int count)
        {
            const float golden = 2.39996323f;   // pi * (3 - sqrt 5)

            float z = 1f - 2f * (index + 0.5f) / count;
            float r = (float)System.Math.Sqrt(System.Math.Max(0f, 1f - z * z));
            float theta = golden * index;

            return new Float3(
                r * (float)System.Math.Cos(theta), r * (float)System.Math.Sin(theta), z);
        }
    }
}

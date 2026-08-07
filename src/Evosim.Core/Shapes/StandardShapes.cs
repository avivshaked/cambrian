using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>Ids of the built-in shapes, so callers need not spell strings.</summary>
    public static class ShapeIds
    {
        public const string Box = "box";
        public const string Sphere = "sphere";
        public const string Capsule = "capsule";
    }

    /// <summary>
    /// A rectangular block — the original and still the only shape that can be flat.
    /// </summary>
    /// <remarks>
    /// Flatness is what makes a fin. A box can have one half-extent far smaller than the other
    /// two, so it presents a large area one way and almost none the other, and that ratio is the
    /// strongest paddle available here. Neither of the other shapes can do it: a sphere is
    /// isotropic and a capsule is round in cross-section.
    /// </remarks>
    public sealed class BoxShape : PartShape
    {
        public override string Id => ShapeIds.Box;

        public override float Volume(Float3 h) => 8f * Abs(h.X) * Abs(h.Y) * Abs(h.Z);

        /// <remarks>
        /// The anchor is clamped to the box rather than projected onto it, so an anchor of
        /// (1, 0, 0) lands at the centre of the +X face and (1, 1, 1) at a corner. Development
        /// draws face-centred anchors (§4.1), and mutation may move them anywhere on the surface.
        /// </remarks>
        public override Float3 SurfacePoint(Float3 anchor, Float3 h) =>
            new Float3(
                Clamp(anchor.X) * Abs(h.X),
                Clamp(anchor.Y) * Abs(h.Y),
                Clamp(anchor.Z) * Abs(h.Z));

        public override bool ContainsPoint(Float3 p, Float3 h) =>
            Abs(p.X) <= Abs(h.X) && Abs(p.Y) <= Abs(h.Y) && Abs(p.Z) <= Abs(h.Z);

        public override void AddPanels(Float3 h, int resolution, List<DragPanel> into)
        {
            int n = resolution < 1 ? 1 : resolution;

            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3;
                int v = (axis + 2) % 3;

                float area = 4f * Abs(h[u]) * Abs(h[v]) / (n * n);
                if (area <= 0f) continue;

                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? 1f : -1f;
                    Float3 normal = Axis(axis, sign);

                    for (int i = 0; i < n; i++)
                    {
                        float du = (2f * (i + 0.5f) / n - 1f) * Abs(h[u]);

                        for (int j = 0; j < n; j++)
                        {
                            float dv = (2f * (j + 0.5f) / n - 1f) * Abs(h[v]);

                            into.Add(new DragPanel(
                                normal * Abs(h[axis]) + Axis(u, du) + Axis(v, dv), normal, area));
                        }
                    }
                }
            }
        }

        private static Float3 Axis(int axis, float value) =>
            axis == 0 ? new Float3(value, 0f, 0f)
          : axis == 1 ? new Float3(0f, value, 0f)
          : new Float3(0f, 0f, value);

        private static float Clamp(float v) => v < -1f ? -1f : v > 1f ? 1f : v;
        private static float Abs(float v) => Math.Abs(v);
    }

    /// <summary>
    /// A ball. Radius is the mean of the three half-extents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A sphere cannot paddle.</b> Every orientation presents the same area to the fluid, so
    /// sweeping one sideways produces drag along its path and nothing perpendicular to it. It
    /// can still contribute to swimming — drag goes as speed squared, so a fast stroke one way
    /// beats a slow return the other — but that is a weaker and less controllable mechanism than
    /// a flat surface catching water.
    /// </para>
    /// <para>
    /// Kept anyway, for two reasons. It is the cheapest collider there is, so a body built from
    /// spheres costs the least to simulate; and heads, floats and bodies that are not meant to
    /// propel are a real part of a morphology. If populations converge on spheres and stop
    /// swimming, that is a finding about the cost model, not a reason to remove the shape.
    /// </para>
    /// <para>
    /// The radius is the <i>mean</i> of all three half-extents rather than one of them, so that
    /// mutating any axis still does something. Reading a single axis would make the other two
    /// silent, and extinction-by-shrinking (§4.5) reads the mean.
    /// </para>
    /// </remarks>
    public sealed class SphereShape : PartShape
    {
        public override string Id => ShapeIds.Sphere;

        /// <summary>Radius for the given half-extents. Public so a collider can read it.</summary>
        /// <remarks>
        /// Every consumer must derive its dimensions from here rather than repeating the formula.
        /// A collider sized from a re-derived radius that later drifts from this one gives a
        /// creature whose physical body and whose drag model are different objects — and nothing
        /// in either would report the disagreement.
        /// </remarks>
        public static float Radius(Float3 h) => Mean(h);

        public override float Volume(Float3 h)
        {
            float r = Mean(h);
            return (4f / 3f) * (float)Math.PI * r * r * r;
        }

        public override Float3 SurfacePoint(Float3 anchor, Float3 h) =>
            Normalize(anchor) * Mean(h);

        public override bool ContainsPoint(Float3 p, Float3 h)
        {
            float r = Mean(h);
            return Float3.Dot(p, p) <= r * r;
        }

        public override void AddPanels(Float3 h, int resolution, List<DragPanel> into)
        {
            float r = Mean(h);
            if (r <= 0f) return;

            // Matched to the box's panel count at the same resolution, so changing shape does
            // not change how finely the fluid is sampled — which would make one shape's forces
            // systematically smoother than another's for reasons having nothing to do with
            // geometry.
            int n = resolution < 1 ? 1 : resolution;
            int count = 6 * n * n;

            float area = 4f * (float)Math.PI * r * r / count;

            for (int i = 0; i < count; i++)
            {
                Float3 dir = SphereDirection(i, count);
                into.Add(new DragPanel(dir * r, dir, area));
            }
        }
    }

    /// <summary>
    /// A cylinder with hemispherical caps, along the local Y axis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The middle shape, and the useful one: round in cross-section like a sphere but elongated
    /// like a box, so it has one axis of anisotropy. That makes it a limb — it presents a long
    /// side broadside and a small circle end-on, which is what a rowing stroke needs — while
    /// keeping the cheap collider and the smooth surface that stops parts catching on each other.
    /// </para>
    /// <para>
    /// Y is the long axis, matching Unity's capsule collider default, so the builder needs no
    /// reorientation. Radius comes from the X and Z half-extents and the length from Y; a
    /// capsule whose Y is smaller than its radius degenerates to a sphere, which is handled
    /// rather than forbidden — it is the natural limit of the shape and a genome should be
    /// allowed to walk through it.
    /// </para>
    /// </remarks>
    public sealed class CapsuleShape : PartShape
    {
        public override string Id => ShapeIds.Capsule;

        /// <summary>Cross-sectional radius. Public so a collider and a mesh can read it.</summary>
        /// <remarks>
        /// See <see cref="SphereShape.Radius"/> — the collider, the render mesh and the drag
        /// panels must all come from here, or a creature's physical body and its hydrodynamic
        /// body quietly become different objects.
        /// </remarks>
        public static float Radius(Float3 h) => (Math.Abs(h.X) + Math.Abs(h.Z)) / 2f;

        /// <summary>Half-length of the straight section. Zero when the capsule is a sphere.</summary>
        public static float HalfSpan(Float3 h) => Math.Max(0f, Math.Abs(h.Y) - Radius(h));

        public override float Volume(Float3 h)
        {
            float r = Radius(h);
            float span = HalfSpan(h);

            float cylinder = (float)Math.PI * r * r * 2f * span;
            float caps = (4f / 3f) * (float)Math.PI * r * r * r;

            return cylinder + caps;
        }

        public override Float3 SurfacePoint(Float3 anchor, Float3 h)
        {
            float r = Radius(h);
            float span = HalfSpan(h);

            Float3 dir = Normalize(anchor);

            // Project onto the capsule: take a point out along the anchor, find the spine point
            // nearest it, and step one radius from the spine towards it.
            //
            // The outward direction must be derived from that displacement rather than from the
            // anchor's radial part. Taking the radial part alone reads (0, +-1, 0) as having no
            // radial component at all — which it does not — and the normalize of a zero vector
            // falls back to +X, silently putting the pole of a capsule on its side. A child
            // anchored along Y then attaches a radius off-axis and sits inside its parent. That
            // is exactly how a founder is attached, and it is 25% of every part in every
            // population.
            Float3 target = dir * (span + r);

            float y = Math.Max(-span, Math.Min(span, target.Y));
            Float3 spine = new Float3(0f, y, 0f);

            Float3 outward = target - spine;
            float length = (float)Math.Sqrt(Float3.Dot(outward, outward));

            return spine + (length > 1e-9f ? outward * (r / length) : dir * r);
        }

        public override bool ContainsPoint(Float3 p, Float3 h)
        {
            float r = Radius(h);
            float span = HalfSpan(h);

            float y = Math.Max(-span, Math.Min(span, p.Y));
            Float3 d = p - new Float3(0f, y, 0f);

            return Float3.Dot(d, d) <= r * r;
        }

        public override void AddPanels(Float3 h, int resolution, List<DragPanel> into)
        {
            float r = Radius(h);
            if (r <= 0f) return;

            float span = HalfSpan(h);
            int n = resolution < 1 ? 1 : resolution;

            // Straight section: rings of panels around the circumference. Their normals are
            // perpendicular to the long axis, which is where the anisotropy comes from.
            int around = Math.Max(4, 4 * n);
            int along = Math.Max(1, 2 * n);

            if (span > 0f)
            {
                float sideArea = 2f * (float)Math.PI * r * (2f * span) / (around * along);

                for (int i = 0; i < around; i++)
                {
                    float theta = 2f * (float)Math.PI * (i + 0.5f) / around;
                    var normal = new Float3((float)Math.Cos(theta), 0f, (float)Math.Sin(theta));

                    for (int j = 0; j < along; j++)
                    {
                        float y = (2f * (j + 0.5f) / along - 1f) * span;
                        into.Add(new DragPanel(normal * r + new Float3(0f, y, 0f), normal, sideArea));
                    }
                }
            }

            // Caps: two hemispheres, sampled the same way a sphere is and offset to the ends.
            // The directions cover a whole sphere and are sorted onto the two ends by the sign
            // of their Y, so the set as a whole carries one sphere's surface — 4*pi*r^2 across
            // capCount panels, not across twice that.
            int capCount = Math.Max(2, 2 * n * n);
            float capArea = 4f * (float)Math.PI * r * r / capCount;

            for (int i = 0; i < capCount; i++)
            {
                Float3 dir = SphereDirection(i, capCount);

                float end = dir.Y >= 0f ? span : -span;
                into.Add(new DragPanel(
                    new Float3(0f, end, 0f) + dir * r, dir, capArea));
            }
        }
    }
}

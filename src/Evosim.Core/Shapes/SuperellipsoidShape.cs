using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// The tapered superellipsoid as a part shape — D233's fourth shape, which takes a
    /// <see cref="PartForm"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A family, not one solid.</b> The box, the sphere and the capsule each read the half-extents
    /// and are the same solid whatever a genome says; this shape is a whole family, from a block to an
    /// egg to a diamond, named by the three genes of its form. So every method that measures it needs
    /// the form, and the form-less methods the interface requires <b>throw</b>
    /// (<see cref="InvalidOperationException"/>): a family part measured without its genes would be
    /// some other solid wearing the family's name, and nothing downstream could tell.
    /// </para>
    /// <para>
    /// The form overloads refuse <see cref="PartForm.None"/> and any form outside the ranges
    /// (<see cref="ArgumentOutOfRangeException"/>), then hand the form to a <see cref="Superellipsoid"/>.
    /// The ranges are D233's convexity bounds and not the widest set the geometry accepts, since the
    /// solver and the drag model assume a convex body.
    /// </para>
    /// <para>
    /// <b>In <see cref="PartShapeRegistry.Catalogue"/>, not in <see cref="PartShapeRegistry.Standard"/>.</b>
    /// A genome file carries a family part's form from format 13 on, but a world allows the shape only
    /// when <see cref="RunConfig.ShapeIds"/> names it, so the default world's list and its mutation
    /// stream stay as they were.
    /// </para>
    /// </remarks>
    public sealed class SuperellipsoidShape : PartShape
    {
        /// <summary>How many forms' geometries each thread keeps.</summary>
        private const int CacheSize = 4;

        // Building a Superellipsoid costs a few beta functions, and a development asks for the same
        // form many times (a volume, a surface point for every edge, a panel build). The last four
        // forms a thread asked for, replaced round-robin. A cache of a pure function: a result never
        // depends on whether it hit, and a thread's cache is its own, so nothing is shared.
        [ThreadStatic] private static Superellipsoid[] _recent;
        [ThreadStatic] private static int _next;

        public override string Id => ShapeIds.Superellipsoid;

        public override bool TakesForm => true;

        // ---------------------------------------------------------------- without a form: refused

        public override float Volume(Float3 halfExtents) => throw NeedsForm();

        public override Float3 SurfacePoint(Float3 anchor, Float3 halfExtents) => throw NeedsForm();

        public override bool ContainsPoint(Float3 local, Float3 halfExtents) => throw NeedsForm();

        public override void AddPanels(Float3 halfExtents, int resolution, List<DragPanel> into) =>
            throw NeedsForm();

        // ---------------------------------------------------------------- with a form

        public override float Volume(Float3 halfExtents, PartForm form) =>
            GeometryFor(form).Volume(halfExtents);

        public override Float3 SurfacePoint(Float3 anchor, Float3 halfExtents, PartForm form) =>
            GeometryFor(form).SurfacePoint(anchor, halfExtents);

        public override bool ContainsPoint(Float3 local, Float3 halfExtents, PartForm form) =>
            GeometryFor(form).ContainsPoint(local, halfExtents);

        public override void AddPanels(
            Float3 halfExtents, int resolution, PartForm form, List<DragPanel> into) =>
            GeometryFor(form).AddPanels(halfExtents, resolution, into);

        /// <summary>
        /// The geometry for a form: the thread's cached one, or a new one. Refuses a form outside
        /// the ranges. Public so a part's <see cref="FamilyGeometry"/> is built from the same cache and
        /// the same refusal as every other call.
        /// </summary>
        public static Superellipsoid GeometryFor(PartForm form)
        {
            if (!form.InRange)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(form), form.ToString(),
                    (form.IsNone ? "A superellipsoid part needs a form, and got none. " : "") +
                    FormattableString.Invariant(
                        $"Its genes must lie in profile [{PartForm.ProfileMin}, {PartForm.ProfileMax}]") +
                    FormattableString.Invariant(
                        $", section [{PartForm.SectionMin}, {PartForm.SectionMax}]") +
                    FormattableString.Invariant(
                        $" and taper [{PartForm.TaperMin}, {PartForm.TaperMax}]; got {form}."));
            }

            Superellipsoid[] recent = _recent ?? (_recent = new Superellipsoid[CacheSize]);

            for (int i = 0; i < recent.Length; i++)
            {
                if (recent[i] != null && recent[i].Form.Equals(form)) return recent[i];
            }

            var built = new Superellipsoid(form);
            recent[_next] = built;
            _next = (_next + 1) % CacheSize;
            return built;
        }

        /// <remarks>
        /// The id and the three ranges, in invariant culture, so a later change of range changes a
        /// config hash that names this shape: a range is part of what the shape means.
        /// </remarks>
        public override string HashContribution() =>
            FormattableString.Invariant(
                $"{Id}[profile={PartForm.ProfileMin}..{PartForm.ProfileMax},section={PartForm.SectionMin}..{PartForm.SectionMax},taper={PartForm.TaperMin}..{PartForm.TaperMax}]");

        private InvalidOperationException NeedsForm() =>
            new InvalidOperationException(
                $"Shape '{Id}' is measured with a PartForm and never without one: call the overload " +
                "that takes the form. A family part measured without its genes would be another solid.");
    }
}

using Evosim.Core;

namespace Evosim.Dynamics
{
    /// <summary>
    /// What each value of <see cref="ContactShapeForm"/> asks of the solver (D228). One place, so that a
    /// test of "the true shape" is written the same way wherever the arrays, the cores or the dispatch
    /// are chosen, and the third value cannot be missed by one of them.
    /// </summary>
    public static class ContactShapeForms
    {
        /// <summary>
        /// Whether a part touches as its convex core with a margin: <see cref="ContactShapeForm.TrueShape"/>
        /// and <see cref="ContactShapeForm.TrueShapeAtPoint"/>. Both carry the committed turns, the cores and
        /// the margins, and take <see cref="ContactTrueShape"/>'s path.
        /// </summary>
        public static bool IsTrueShape(this ContactShapeForm form) =>
            form == ContactShapeForm.TrueShape || form == ContactShapeForm.TrueShapeAtPoint;

        /// <summary>
        /// Whether the force acts at the contact point, with the torque <c>r x F</c> and the damper on the
        /// point's velocity: <see cref="ContactShapeForm.TrueShapeAtPoint"/> only. Also carries each link's
        /// committed spin.
        /// </summary>
        public static bool AtPoint(this ContactShapeForm form) =>
            form == ContactShapeForm.TrueShapeAtPoint;

        /// <summary>
        /// The normal a body takes against another whose contact centre lies within 1e-9 m of its own, where the
        /// line between the centres has no direction. <see cref="ContactShapeForm.SpheresApart"/> orders the pair
        /// by organism id: the lower id takes <c>-X</c> and the higher <c>+X</c>, so the two pushes are equal and
        /// opposite. Every other form, and a pair of equal ids (which two bodies never are), keeps the recorded
        /// <see cref="Vec3.UnitY"/>. An integer comparison and a sign, no arithmetic.
        /// </summary>
        public static Vec3 CoincidentNormal(this ContactShapeForm form, int ownId, int otherId)
        {
            if (form != ContactShapeForm.SpheresApart || ownId == otherId) return Vec3.UnitY;
            return ownId < otherId ? new Vec3(-1, 0, 0) : new Vec3(1, 0, 0);
        }
    }
}

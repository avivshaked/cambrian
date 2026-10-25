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
    }
}

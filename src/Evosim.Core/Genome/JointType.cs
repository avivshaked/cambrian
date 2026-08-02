namespace Evosim.Core
{
    /// <summary>
    /// Joint types, taken verbatim from [K12 §2.1, p.3] — DESIGN.md §4.1. A working
    /// system's list rather than a guess.
    /// </summary>
    public enum JointType
    {
        Fixed = 0,
        Hinge = 1,
        Twist = 2,
        HingeTwist = 3,
        TwistHinge = 4,
        Universal = 5,
        Spherical = 6,
    }

    public static class JointTypeExtensions
    {
        /// <summary>
        /// Degrees of freedom. Drives the length of <c>MorphNode.JointLimits</c> and the
        /// number of effectors on the joint (DESIGN.md §4.4 — one effector per DOF).
        /// </summary>
        public static int DofCount(this JointType type)
        {
            switch (type)
            {
                case JointType.Fixed: return 0;
                case JointType.Hinge: return 1;
                case JointType.Twist: return 1;
                case JointType.HingeTwist: return 2;
                case JointType.TwistHinge: return 2;
                case JointType.Universal: return 2;
                case JointType.Spherical: return 3;
                default: return 0;
            }
        }
    }
}

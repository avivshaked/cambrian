using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// The boundary between Evosim.Core's own maths types and UnityEngine's.
    /// </summary>
    /// <remarks>
    /// Core carries no UnityEngine dependency (DESIGN.md §6.1) so it can be tested outside
    /// the Editor, which means it cannot use <see cref="Vector3"/>. Conversion lives here,
    /// on the Unity side of the line, rather than being smeared through the builder.
    /// Both use metres, radians internally, and a left-handed Y-up frame, so conversion is
    /// componentwise.
    /// </remarks>
    public static class UnityConvert
    {
        public static Vector3 ToVector3(this Float3 v) => new Vector3(v.X, v.Y, v.Z);

        public static Float3 ToFloat3(this Vector3 v) => new Float3(v.x, v.y, v.z);

        public static Quaternion ToQuaternion(this Quat q) => new Quaternion(q.X, q.Y, q.Z, q.W);

        public static Quat ToQuat(this Quaternion q) => new Quat(q.x, q.y, q.z, q.w);
    }
}

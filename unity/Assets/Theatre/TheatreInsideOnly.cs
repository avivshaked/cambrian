using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// Marks a piece of the theatre's furniture that exists to be seen from inside the water.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a component and not a name.</b> The fourth day hung a sea on the world's ceiling and
    /// a handful of light shafts under it, and its one hard requirement about the outside is that
    /// the snapshot's four census views are unchanged: the top view still sees the world, and
    /// nothing new stands in front of the box in the side, the end or the iso
    /// (logbook/specs/skin-spec-4.md). Two of those four cameras sit under the waterline, so the
    /// shader's own test cannot refuse them, and the Play mode viewer starts outside the box as
    /// well, so a test on whether the eye is inside it would have taken the sea away from the one
    /// person it was built for. What actually separates the two cases is which camera is
    /// rendering, and that is a question only <see cref="SnapshotCamera"/> can answer. So it turns
    /// these renderers off for the length of one render, the way it already silences
    /// <see cref="WaterBounds"/>, and it finds them by this rather than by a name a rename would
    /// break silently.
    /// </para>
    /// <para>
    /// It carries no state and does nothing. A component that is only ever looked for is the
    /// cheapest way Unity has of saying what an object is.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TheatreInsideOnly : MonoBehaviour
    {
    }
}

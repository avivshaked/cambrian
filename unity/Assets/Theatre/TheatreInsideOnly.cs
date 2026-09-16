using System.Collections.Generic;
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
    /// <b>Why it keeps a list of itself.</b> Until 2026-09-16 the snapshot found these with
    /// <c>FindObjectsByType</c>, and found none: every piece of the skin's furniture is created
    /// with <c>HideFlags.DontSave</c>, and the engine's finders leave such objects out. The shafts
    /// had therefore stood in every side view since the fourth day, and the glass (logbook/0104)
    /// stood in the census views on its first picture. The marker now registers itself when it
    /// is enabled and leaves when it is disabled, and the snapshot reads the list.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TheatreInsideOnly : MonoBehaviour
    {
        private static readonly List<TheatreInsideOnly> Registry = new List<TheatreInsideOnly>();

        /// <summary>Every enabled marker in the scene, in the order they were enabled.</summary>
        public static IReadOnlyList<TheatreInsideOnly> All => Registry;

        private void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }
    }
}

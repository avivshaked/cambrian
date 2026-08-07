using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// One part's panels, built once and read every step — DESIGN.md §5.2, §5A.9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A part's local geometry does not change after development.</b> Panels were regenerated
    /// from the <see cref="PartShape"/> on every part on every step regardless, through a virtual
    /// call, into a list that was cleared and refilled — for a shape whose half-extents were fixed
    /// the moment the phenotype was built. §5A.9 measured the consequence: at 512 creatures the
    /// drag loop was 88% of the step and PhysX was 12%, so the wall was our own code rather than
    /// the engine.
    /// </para>
    /// <para>
    /// <b>Stored as parallel arrays rather than an array of <see cref="DragPanel"/>.</b> The inner
    /// loop reads all three fields of every panel in order, so the layout costs nothing here and
    /// leaves the door open to a vectorised pass later. It also means the loop touches three flat
    /// arrays with no indirection, which is the shape a JIT can do something with.
    /// </para>
    /// <para>
    /// Immutable once built. It describes geometry, and geometry is the one thing about a creature
    /// that is settled before it ever moves.
    /// </para>
    /// </remarks>
    public sealed class DragPanelSet
    {
        /// <summary>Panel centres in the part's local space, metres.</summary>
        public readonly Float3[] Centres;

        /// <summary>Outward unit normals in the part's local space.</summary>
        public readonly Float3[] Normals;

        /// <summary>Panel areas, m². Sums to the shape's surface area.</summary>
        public readonly float[] Areas;

        public int Count => Areas.Length;

        public DragPanelSet(Float3[] centres, Float3[] normals, float[] areas)
        {
            Centres = centres;
            Normals = normals;
            Areas = areas;
        }

        /// <summary>
        /// Builds the panels for one part. Call once, at build; never on the step path.
        /// </summary>
        /// <param name="scratch">
        /// Optional reusable list. Building a whole population allocates one list per part
        /// without it, which is harmless at build time and wasteful when it need not be.
        /// </param>
        public static DragPanelSet For(
            PartShape shape, Float3 halfExtents, int panelsPerAxis, List<DragPanel> scratch = null)
        {
            List<DragPanel> panels = scratch ?? new List<DragPanel>(64);
            panels.Clear();

            shape.AddPanels(halfExtents, panelsPerAxis < 1 ? 1 : panelsPerAxis, panels);

            // Panels with no area contribute nothing and would be tested every step forever.
            // Dropped here, once, rather than branched around a few billion times.
            int kept = 0;
            for (int i = 0; i < panels.Count; i++)
            {
                if (panels[i].Area > 0f) kept++;
            }

            var centres = new Float3[kept];
            var normals = new Float3[kept];
            var areas = new float[kept];

            int n = 0;
            for (int i = 0; i < panels.Count; i++)
            {
                DragPanel panel = panels[i];
                if (panel.Area <= 0f) continue;

                centres[n] = panel.Centre;
                normals[n] = panel.Normal;
                areas[n] = panel.Area;
                n++;
            }

            return new DragPanelSet(centres, normals, areas);
        }
    }
}

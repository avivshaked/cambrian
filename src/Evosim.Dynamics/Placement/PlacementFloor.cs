using Evosim.Core;

namespace Evosim.Dynamics.Placement
{
    /// <summary>
    /// The sea bed as the placer sees it: a height under every column and the clearance a body
    /// keeps off it. <c>Evosim.Sim.SeaFloor</c>'s geometry with none of its scenery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What was left behind.</b> <c>SeaFloor</c> is mostly a <c>GameObject</c>: a static
    /// <c>BoxCollider</c> for the flat slab, a <c>MeshCollider</c> over a half-metre lattice for
    /// the shaped one, a backstop box under that, contact reporting, a physics material, a
    /// <c>Physics.SyncTransforms</c>. Not one of those is read by <c>SharedVolume</c>. The
    /// placer asks the floor exactly three questions — does it have relief, how high is the rock
    /// at <c>(x, z)</c>, and how high may a sphere of radius <c>r</c> be put over that point —
    /// and all three are answered from <see cref="BedShape"/> and a scalar, both of which are
    /// already engine-free and already in <c>Evosim.Core</c>.
    /// </para>
    /// <para>
    /// <b>Why the numbers are the same.</b> <c>SeaFloor.FloorYAt</c> is
    /// <c>_bed != null ? (float)_bed.FloorY(x, z) : _topY</c>, where <c>_topY</c> is set once in
    /// <c>SeaFloor.Build</c> to <c>-volume.DepthMetres</c> and <c>_bed</c> is the world's
    /// <see cref="BedShape"/> — but only when that shape has relief, because <c>Build</c> sends
    /// a bed with none down the flat path (<c>logbook/specs/bed-spec.md</c> item 12). The two
    /// gates are reproduced here in the constructor, so a bed with no relief is a flat floor in
    /// this class too rather than a general path with a zero in it. The colliders never entered
    /// the arithmetic: the slab's <i>top face</i> is at <c>-D</c> by construction and the mesh's
    /// vertices are <c>bed.FloorY</c> evaluated on a lattice, so the collider a body would rest
    /// on is a piecewise-linear drawing of the same map the placer reads exactly.
    /// </para>
    /// </remarks>
    public sealed class PlacementFloor
    {
        /// <summary>
        /// Clearance a placed body keeps between its bounding sphere and the bed, metres —
        /// <c>SeaFloor.ClearanceMetres</c>, unchanged.
        /// </summary>
        public const float ClearanceMetres = 0.05f;

        private readonly BedShape _bed;
        private readonly float _topY;

        /// <summary>
        /// The bed under a world <paramref name="depthMetres"/> deep, with
        /// <paramref name="bed"/>'s shape or flat when it has none.
        /// </summary>
        /// <param name="depthMetres">
        /// The water's depth, <c>SharedVolume.DepthMetres</c>. <c>SeaFloor.Build</c> takes
        /// <c>topY = -volume.DepthMetres</c> from the placer it is built under, and the negation
        /// is taken here for the same reason: one depth, read once.
        /// </param>
        /// <param name="bed">
        /// The world's height map — <c>World.Bed</c> — or null for the flat slab, which is every
        /// recorded world.
        /// </param>
        public PlacementFloor(float depthMetres, BedShape bed = null)
        {
            // SeaFloor.Build's own gate: `bed == null || !bed.HasRelief` goes to Flat, which
            // stores no bed at all. So HasRelief below reads false for a shape with no relief,
            // exactly as SeaFloor's does.
            _bed = bed != null && bed.HasRelief ? bed : null;
            _topY = -depthMetres;
        }

        /// <summary>Whether this floor has a shape — <c>SeaFloor.HasRelief</c>.</summary>
        public bool HasRelief => _bed != null;

        /// <summary>The floor's own height at a place, m — <c>SeaFloor.FloorYAt</c>.</summary>
        public float FloorYAt(float x, float z) =>
            _bed != null ? (float)_bed.FloorY(x, z) : _topY;

        /// <summary>
        /// The shallowest y a body of <paramref name="boundingRadius"/> may be placed at over
        /// <c>(x, z)</c> — <c>SeaFloor.MinimumPlacementY</c>, term for term.
        /// </summary>
        public float MinimumPlacementY(float x, float z, float boundingRadius) =>
            FloorYAt(x, z) + UnityFloatMath.Max(0f, boundingRadius) + ClearanceMetres;
    }
}

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
        public PlacementFloor(float depthMetres, BedShape bed = null, ReefGeometry reefs = null)
        {
            // SeaFloor.Build's own gate: `bed == null || !bed.HasRelief` goes to Flat, which
            // stores no bed at all. So HasRelief below reads false for a shape with no relief,
            // exactly as SeaFloor's does.
            _bed = bed != null && bed.HasRelief ? bed : null;
            _topY = -depthMetres;
            _reefs = reefs != null && reefs.Count > 0 ? reefs : null;
        }

        private readonly ReefGeometry _reefs;

        /// <summary>
        /// The reefs' rock the placer keeps bodies out of, or null — every recorded world.
        /// <c>logbook/specs/reef-spec.md</c> §2.
        /// </summary>
        public ReefGeometry Reefs => _reefs;

        /// <summary>Whether there is rock besides the bed to keep a body out of.</summary>
        public bool HasReefs => _reefs != null;

        /// <summary>
        /// The reefs' rule for a candidate spot: true and the height unchanged when a sphere of
        /// <paramref name="boundingRadius"/> there clears the rock by <see cref="ClearanceMetres"/>;
        /// true and the height raised onto a cap's table when the spot is over a cap and in or
        /// against its upper half; false otherwise — <c>logbook/specs/reef-spec.md</c> §2.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The floor's rule, extended with the rock's signed distance.</b> The floor raises a
        /// body that would be in the sand; the rock refuses one that would be in the stone, as the
        /// glass does, except over a cap, where "a founder over a cap lands on it": a spot in the
        /// cap's upper half, or touching it from above, is set on the table at the cap's top plus
        /// the radius and the clearance. A spot in the cap's lower half, against its underside or in
        /// a stem is refused and drawn again; a spot on the island's table, which is the surface,
        /// is refused because a body there would stand out of the water.
        /// </para>
        /// <para>
        /// <b>No draw.</b> The rule reads the rock and nothing else, so it takes nothing from the
        /// placer's stream; a refusal costs the candidate the draws it already took.
        /// </para>
        /// </remarks>
        public bool ClearOfReefs(float x, ref float y, float z, float boundingRadius)
        {
            if (_reefs == null) return true;

            double need = System.Math.Max(0f, boundingRadius) + ClearanceMetres;
            double distance = _reefs.SignedDistance(x, y, z, out int reef, out _);
            if (distance >= need) return true;

            double dx = x - _reefs.CentreX(reef);
            double dz = z - _reefs.CentreZ(reef);
            double capRadius = _reefs.CapRadiusMetres;
            double middle = 0.5d * (_reefs.CapTopY + _reefs.CapUndersideY);

            if (dx * dx + dz * dz > capRadius * capRadius || !(y > middle)) return false;

            float landed = (float)(_reefs.CapTopY + need);
            if (landed + System.Math.Max(0f, boundingRadius) > 0f) return false;

            // The rim is rounded, so a spot near it lands lower than the table's top would put it;
            // the rock is asked again rather than trusted.
            if (_reefs.SignedDistance(x, landed, z) < need - 1e-4) return false;

            if (landed > y) y = landed;
            return true;
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

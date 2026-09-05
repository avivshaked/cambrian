using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// The water, as a box — D077's footprint world: where a body may be, where it goes when it
    /// leaves, and which patch it is in while it is there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The box is literal, and it is the one the ecology was already priced against.</b>
    /// <c>RunConfig.WorldAreaSquareMetres</c> is the sun's aperture and the denominator of every
    /// density the world reads (<c>scratch/footprint-survey.md</c>), and
    /// <c>RunConfig.HorizontalPatches</c> already divides it into K columns of
    /// <c>sqrt(area / K)</c> metres — the width <c>NutrientField.PatchWidthMetres</c> computes and
    /// the vent's drag term already uses. So the box is K of those columns side by side on a ring:
    /// x ∈ [0, K·W), y ∈ [−D, 0], z ∈ [0, W). Nothing new is chosen here; a geometry that was
    /// inert becomes load-bearing.
    /// </para>
    /// <para>
    /// <b>A patch is a place, not an index.</b> Until D077 a creature carried a patch number it
    /// inherited at birth and changed only by lottery, while its body sat on a lattice a hundred
    /// metres from its neighbours and unrelated to either. Here the number is read from where the
    /// root actually is — <c>floor(x / W) mod K</c> — so a creature is in the water it is swimming
    /// in, and it changes patch because something moved it.
    /// </para>
    /// <para>
    /// <b>The horizontal boundary is periodic</b> (<see cref="TryWrap"/>): a root that leaves by
    /// one face is translated back in at the opposite one, whole articulation, rotation and
    /// velocities untouched. ⚠ <b>Contacts across a seam are not seen.</b> A body one centimetre
    /// inside the x = 0 face and a body one centimetre inside x = K·W are neighbours in the world
    /// the ring describes, and are K·W metres apart to PhysX and to the placement below — so they
    /// neither collide nor block each other's births. The alternative is ghost bodies at every
    /// face, which is six copies of a population that already sets the throughput ceiling.
    /// </para>
    /// <para>
    /// <b>Placement is rejection-sampled on bounding spheres and never overlaps</b>, because two
    /// overlapping articulations depenetrate and depenetration is a force — logbook/0007 measured
    /// a creature learning to farm one. The spheres live in a spatial hash rebuilt once per
    /// metabolic step from positions <c>Ecosystem.CheckFinite</c> has already read, so the
    /// instrument costs no Transform reads of its own. The method is the shared-space spike's
    /// (logbook/0064, <c>SharedSpaceSpike.Place</c>), which packed 1,000 founders into
    /// 10 × 10 × 60 m at 17.7 rejections per body; the spike's lattice fallback is not carried
    /// over, because a world that cannot place a newborn beside its parent has told us something
    /// — a crowded stillbirth — rather than needing a second-best answer.
    /// </para>
    /// <para>
    /// <b>One RNG stream, its own.</b> Seeded at <see cref="World.PlacementIndex"/>, so where a
    /// body lands changes no other draw in the world — the discipline <c>World</c> keeps for
    /// D072's conception walk, and for the same reason: a knob that perturbs a shared stream
    /// invalidates every result on file.
    /// </para>
    /// </remarks>
    public sealed class SharedVolume : IBodyPlacement
    {
        /// <summary>Placements tried before a birth is called crowded — D077's budget.</summary>
        /// <remarks>
        /// A newborn re-draws its direction each attempt at a fixed distance from the parent, so
        /// 64 attempts sample the parent's own shell densely; they do not search further afield,
        /// which is deliberate. "Beside the parent" is the rule, and a birth that had to be flung
        /// across the patch to fit would not be that.
        /// </remarks>
        public const int AttemptBudget = 64;

        /// <summary>Where a living body is and how big it is — one entry per creature.</summary>
        private struct Occupant
        {
            public Vector3 Position;
            public float Radius;
        }

        private readonly Rng _rng;

        private readonly List<Occupant> _occupied = new List<Occupant>();
        private readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();

        /// <summary>Where each living creature is, so a child of it can be placed beside it.</summary>
        private readonly Dictionary<long, Occupant> _known = new Dictionary<long, Occupant>();

        /// <summary>Where each reserved-but-not-yet-built body goes, by creature id.</summary>
        private readonly Dictionary<long, Vector3> _pending = new Dictionary<long, Vector3>();

        private bool _gridBuilt;
        private float _cellSize = 1f;
        private float _maxRadius;

        private bool _reserved;
        private Occupant _reservation;

        public SharedVolume(int patchCount, float patchWidthMetres, float depthMetres, ulong seed)
        {
            PatchCount = Mathf.Max(1, patchCount);
            PatchWidthMetres = Mathf.Max(0.01f, patchWidthMetres);
            DepthMetres = Mathf.Max(0.01f, depthMetres);

            _rng = new Rng(Rng.SeedFor(seed, World.PlacementIndex));
        }

        /// <summary>K — <see cref="RunConfig.HorizontalPatches"/>, floored at 1.</summary>
        public int PatchCount { get; }

        /// <summary>W = sqrt(area / K), metres. One patch's side, and the box's z extent.</summary>
        public float PatchWidthMetres { get; }

        /// <summary>D — <see cref="RunConfig.WorldDepthMetres"/>.</summary>
        public float DepthMetres { get; }

        /// <summary>K·W: the box's x extent, the way around the ring.</summary>
        public float LengthMetres => PatchWidthMetres * PatchCount;

        /// <summary>Bodies translated at a seam, running total.</summary>
        public long Wraps { get; private set; }

        /// <summary>Placement attempts that hit an occupied sphere, running total.</summary>
        public long Rejections { get; private set; }

        /// <summary>Births refused for want of room, running total — the crowded stillbirths.</summary>
        public long Refusals { get; private set; }

        /// <summary>The spatial hash's cell side, metres: 2× the largest body, floored at 1.</summary>
        public float CellMetres => _cellSize;

        /// <summary>Living bodies the hash currently holds.</summary>
        public int Occupants => _occupied.Count;

        // ------------------------------------------------------------------------- the geometry

        /// <summary>The patch a horizontal position falls in — <c>floor(x / W) mod K</c>.</summary>
        /// <remarks>
        /// The wrap is applied first, so a body that has drifted past a face between the last wrap
        /// and this read still reads a patch that exists. The second fold is there because C#'s
        /// <c>%</c> keeps the sign of its left operand.
        /// </remarks>
        public int PatchOf(float x)
        {
            float wrapped = WrapAxis(x, LengthMetres);
            int patch = (int)Mathf.Floor(wrapped / PatchWidthMetres);

            patch %= PatchCount;
            if (patch < 0) patch += PatchCount;

            return patch;
        }

        /// <summary>
        /// How far apart two points are on the ring — the shortest way round, not through.
        /// </summary>
        /// <remarks>
        /// <b>A wrap is not a swim.</b> The speed instruments difference a body's position across
        /// a metabolic step, and a body translated at a seam moves K·W metres in one step of it:
        /// the first shared-space arm reported a fastest creature of 82.5 m/s, which is 40 m over
        /// a 0.5 s metabolic step to three significant figures, in a world whose fastest animal
        /// has ever managed 0.5. The minimum-image convention is the standard fix and the only one
        /// that keeps a periodic box's kinematics honest: on a ring the distance between two points
        /// is the shorter of the two arcs.
        /// </remarks>
        public float ShortestDistance(Vector3 a, Vector3 b)
        {
            float dx = Shortest(a.x - b.x, LengthMetres);
            float dy = a.y - b.y;
            float dz = Shortest(a.z - b.z, PatchWidthMetres);

            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>One axis of the minimum image: the separation folded into [−extent/2, extent/2].</summary>
        private static float Shortest(float d, float extent)
        {
            if (!(extent > 0f)) return d;

            return d - extent * Mathf.Round(d / extent);
        }

        /// <summary>Where a root that has left the box belongs, or false if it is still inside.</summary>
        public bool TryWrap(Vector3 p, out Vector3 wrapped)
        {
            wrapped = p;

            float length = LengthMetres;
            float width = PatchWidthMetres;

            if (p.x >= 0f && p.x < length && p.z >= 0f && p.z < width) return false;

            // A non-finite coordinate is not a body out of the box, it is a diverged body, and
            // Ecosystem.CheckFinite kills it at the next metabolic step. Teleporting it first
            // would replace the evidence with a number of our own.
            float sum = p.x + p.z;
            if (float.IsNaN(sum) || float.IsInfinity(sum)) return false;

            wrapped = new Vector3(WrapAxis(p.x, length), p.y, WrapAxis(p.z, width));
            Wraps++;
            return true;
        }

        private static float WrapAxis(float v, float extent)
        {
            if (v >= 0f && v < extent) return v;

            float folded = v - extent * Mathf.Floor(v / extent);

            // Floating point can land the fold exactly on the far face for a v just under zero,
            // which is outside the half-open interval the box is defined on.
            if (folded >= extent || folded < 0f) folded = 0f;

            return folded;
        }

        // ------------------------------------------------------------------ the occupied volume

        /// <summary>
        /// Empties the hash for a fresh metabolic step. Reservations already made for bodies not
        /// yet built are kept — a creature promised a spot last step is still going to be there.
        /// </summary>
        public void Begin()
        {
            _occupied.Clear();
            _grid.Clear();
            _known.Clear();
            _gridBuilt = false;
            _maxRadius = 0f;
        }

        /// <summary>
        /// Records where a living creature is: it occupies space, and it can be bred beside.
        /// </summary>
        public void Note(long creatureId, Vector3 position, float radius)
        {
            var occupant = new Occupant { Position = position, Radius = radius };
            _known[creatureId] = occupant;
            Occupy(occupant);
        }

        private void Occupy(Occupant occupant)
        {
            int index = _occupied.Count;
            _occupied.Add(occupant);
            if (occupant.Radius > _maxRadius) _maxRadius = occupant.Radius;

            // Before the first query the grid does not exist yet: Build inserts everything at
            // once, with a cell size it can only choose once it has seen the largest body.
            if (_gridBuilt) Insert(index);
        }

        private void Build()
        {
            // Two of the largest bounding radius in the world, so a query for a body no larger
            // than that reaches every sphere that could touch it within one cell in each
            // direction. Floored at 1 m, so a world of tiny bodies does not build a hash with
            // millions of cells across a 60 m box.
            _cellSize = Mathf.Max(1f, 2f * _maxRadius);
            _grid.Clear();

            for (int i = 0; i < _occupied.Count; i++) Insert(i);

            _gridBuilt = true;
        }

        private void Insert(int index)
        {
            long key = Key(_occupied[index].Position);

            if (!_grid.TryGetValue(key, out List<int> bucket))
            {
                bucket = new List<int>();
                _grid[key] = bucket;
            }

            bucket.Add(index);
        }

        private long Key(Vector3 p) => Key(
            Mathf.FloorToInt(p.x / _cellSize),
            Mathf.FloorToInt(p.y / _cellSize),
            Mathf.FloorToInt(p.z / _cellSize));

        /// <summary>
        /// A spatial hash rather than a dense grid: the box is 40 × 10 × 60 m at the screen's
        /// settings and a dense array of it would be mostly empty. A hash collision costs a
        /// distance test and can never produce a wrong answer, because every candidate is checked
        /// against the sphere it actually is.
        /// </summary>
        private static long Key(int ix, int iy, int iz) =>
            ((long)ix * 73856093L) ^ ((long)iy * 19349663L) ^ ((long)iz * 83492791L);

        /// <summary>Whether a sphere of <paramref name="radius"/> at <paramref name="p"/> is clear.</summary>
        private bool Free(Vector3 p, float radius)
        {
            if (!_gridBuilt) Build();

            // How far the search has to reach: the largest thing in the hash plus the thing being
            // placed. One cell in each direction whenever the newcomer is no bigger than the
            // biggest body already there, which is the ordinary case.
            int reach = Mathf.Max(1, Mathf.CeilToInt((radius + _maxRadius) / _cellSize));

            int cx = Mathf.FloorToInt(p.x / _cellSize);
            int cy = Mathf.FloorToInt(p.y / _cellSize);
            int cz = Mathf.FloorToInt(p.z / _cellSize);

            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dy = -reach; dy <= reach; dy++)
                {
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        if (!_grid.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out List<int> bucket))
                        {
                            continue;
                        }

                        for (int k = 0; k < bucket.Count; k++)
                        {
                            Occupant other = _occupied[bucket[k]];
                            float span = radius + other.Radius;

                            if ((other.Position - p).sqrMagnitude < span * span) return false;
                        }
                    }
                }
            }

            return true;
        }

        // ----------------------------------------------------------------------- IBodyPlacement

        public int PatchOf(Organism creature)
        {
            if (creature == null) return 0;

            return _known.TryGetValue(creature.Id, out Occupant at)
                ? PatchOf(at.Position.x)
                : creature.Patch;
        }

        public bool TryReserveOffspring(Organism parent, Phenotype child, out int patch)
        {
            patch = parent != null ? parent.Patch : 0;
            if (parent == null || child == null) return false;

            float radius = BoundingRadius(child);

            // A parent nothing has told us about cannot be bred beside. It should not happen —
            // every living creature is Noted at the start of the metabolic step in which it can
            // conceive — so the fallback is the founder rule (somewhere in the box, at the
            // parent's own depth) rather than a refusal, which would file a bookkeeping gap as
            // an ecological fact.
            if (!_known.TryGetValue(parent.Id, out Occupant at))
            {
                return TryReserveFounder(child, parent.HeightY, out patch);
            }

            float distance = at.Radius + radius;

            for (int attempt = 0; attempt < AttemptBudget; attempt++)
            {
                // A direction in the horizontal plane and nowhere else: a newborn beside its
                // parent is at its parent's depth, which is the depth the parent's income was
                // earned at and the one the lineage is selected on. A free vertical metre would
                // be the world choosing where a child should live.
                float angle = _rng.Range(0f, 2f * Mathf.PI);

                var candidate = new Vector3(
                    WrapAxis(at.Position.x + distance * Mathf.Cos(angle), LengthMetres),
                    at.Position.y,
                    WrapAxis(at.Position.z + distance * Mathf.Sin(angle), PatchWidthMetres));

                if (!Free(candidate, radius)) { Rejections++; continue; }

                Reserve(candidate, radius);
                patch = PatchOf(candidate.x);
                return true;
            }

            Refusals++;
            return false;
        }

        public bool TryReserveFounder(Phenotype body, float heightY, out int patch)
        {
            patch = 0;
            if (body == null) return false;

            float radius = BoundingRadius(body);

            for (int attempt = 0; attempt < AttemptBudget; attempt++)
            {
                // Uniform in the box at the depth the world drew, per D077: the depth is the
                // founder lottery's business (RunConfig.FounderDepthSpread) and the horizontal
                // position is this one's.
                var candidate = new Vector3(
                    _rng.Range(0f, LengthMetres), heightY, _rng.Range(0f, PatchWidthMetres));

                if (!Free(candidate, radius)) { Rejections++; continue; }

                Reserve(candidate, radius);
                patch = PatchOf(candidate.x);
                return true;
            }

            Refusals++;
            return false;
        }

        private void Reserve(Vector3 position, float radius)
        {
            _reserved = true;
            _reservation = new Occupant { Position = position, Radius = radius };
        }

        public void Commit(long creatureId)
        {
            if (!_reserved) return;

            _pending[creatureId] = _reservation.Position;

            // Occupied the moment it is committed rather than when the articulation is built:
            // every conception after this one in the same step has to see it, or a step that
            // produced four hundred births would place every one of them as if the world were
            // empty and they would all be built overlapping.
            Occupy(_reservation);
            _reserved = false;
        }

        public void Release() => _reserved = false;

        /// <summary>
        /// Hands over — and forgets — where a newborn was promised it could be.
        /// </summary>
        /// <remarks>
        /// Read by <c>Ecosystem.Build</c>, which runs at the start of the physics step after the
        /// conception with no <c>Physics.Simulate</c> in between: nothing has moved since the spot
        /// was chosen. False for a creature that has no reservation, which within one shared-space
        /// run should never happen and is handled by falling back to the lattice rather than by
        /// refusing to build a body.
        /// </remarks>
        public bool TryTakePlacement(long creatureId, out Vector3 position)
        {
            if (!_pending.TryGetValue(creatureId, out position)) return false;

            _pending.Remove(creatureId);
            return true;
        }

        /// <summary>
        /// Radius of the sphere that contains a whole developed body, about its root part.
        /// </summary>
        /// <remarks>
        /// The shared-space spike's own measure (<c>SharedSpaceSpike.BoundingRadius</c>), and the
        /// one logbook/0064's packing fact is denominated in — 0.63 m mean over founders. Spheres,
        /// not solids, so this over-reserves for a long thin body, which is the safe direction:
        /// the cost of over-reserving is a rejection and the cost of under-reserving is a
        /// depenetration impulse. Measured about the root part, which sits at the phenotype's
        /// origin by construction (<c>Developer</c> expands the root at the identity transform)
        /// and is therefore the point <c>PhenotypeBuilder.Build</c> is handed.
        /// </remarks>
        public static float BoundingRadius(Phenotype phenotype)
        {
            float radius = 0f;

            for (int i = 0; i < phenotype.Parts.Count; i++)
            {
                PhenotypePart part = phenotype.Parts[i];
                Float3 p = part.Position;
                Float3 h = part.HalfExtents;

                float centre = Mathf.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
                float corner = Mathf.Sqrt(h.X * h.X + h.Y * h.Y + h.Z * h.Z);

                radius = Mathf.Max(radius, centre + corner);
            }

            return Mathf.Max(0.01f, radius);
        }
    }
}

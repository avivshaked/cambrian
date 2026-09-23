using System;
using System.Collections.Generic;
using Evosim.Core;

namespace Evosim.Dynamics.Placement
{
    /// <summary>
    /// The water, as a box or as a tank — where a body may be, where it goes when it leaves, and
    /// which patch it is in while it is there. An engine-free port of
    /// <c>unity/Assets/Evosim/Sim/SharedVolume.cs</c> (work package H,
    /// <c>fable-propose-own-solver.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here is new.</b> The world rules this class implements — D077's periodic
    /// footprint, D088's dispersal disc, D089's tank, D092's shaped bed, the adult-radius
    /// reservation, the attempt budget, the spatial hash — are stated where they were ruled and
    /// are not restated here; the Unity class carries the rationale and
    /// <c>DECISIONS.md</c> carries the rulings. What is written out below is only what a port
    /// has to be able to prove: which expression each line evaluates, and why it evaluates the
    /// same number the engine's did.
    /// </para>
    /// <para>
    /// <b>Three substitutions, and no fourth.</b> <c>UnityEngine.Vector3</c> becomes Core's
    /// <see cref="Float3"/>, which is the same three floats with the same componentwise
    /// arithmetic in the same order; <c>UnityEngine.Mathf</c> becomes
    /// <see cref="UnityFloatMath"/>, which is Unity's <c>Mathf</c> transcribed rather than the
    /// framework's nearest equivalent (that file's remarks say why the difference is real); and
    /// <c>Evosim.Sim.SeaFloor</c> becomes <see cref="PlacementFloor"/>, which is the three
    /// arithmetic members of the bed with the colliders left behind. The one structural
    /// difference is that <see cref="Float3"/> is readonly, so where the original assigned
    /// <c>candidate.y</c> in place this rebuilds the vector from two unchanged components and
    /// the new one — the same value, and it is marked at the site.
    /// </para>
    /// <para>
    /// <b>The draw sequence is the contract.</b> One changed draw is a different world from the
    /// first birth on, so every branch below takes the same number of draws in the same order
    /// as the Unity class does, including the branches that look like they could be folded
    /// together. <c>src/Evosim.Dynamics.Tests/PlacementTests.cs</c> drives this class and a
    /// verbatim copy of the Unity one through the same five thousand reservations and asserts
    /// the positions, the counters and the RNG state all agree.
    /// </para>
    /// </remarks>
    public sealed class SharedVolume : IBodyPlacement
    {
        /// <summary>Placements tried before a birth is called crowded — D077's budget.</summary>
        public const int AttemptBudget = 64;

        /// <summary>Where a living body is and how big it is — one entry per creature.</summary>
        private struct Occupant
        {
            public Float3 Position;
            public float Radius;
        }

        private readonly Rng _rng;

        private readonly List<Occupant> _occupied = new List<Occupant>();
        private readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();

        /// <summary>Where each living creature is, so a child of it can be placed beside it.</summary>
        private readonly Dictionary<long, Occupant> _known = new Dictionary<long, Occupant>();

        /// <summary>Where each reserved-but-not-yet-built body goes, by creature id.</summary>
        private readonly Dictionary<long, Float3> _pending = new Dictionary<long, Float3>();

        private bool _gridBuilt;
        private float _cellSize = 1f;
        private float _maxRadius;

        private bool _reserved;
        private Occupant _reservation;

        public SharedVolume(
            int patchCount,
            float patchWidthMetres,
            float depthMetres,
            ulong seed,
            float offspringDispersalMetres = 0f,
            int patchesAcross = 1,
            WorldShape shape = WorldShape.Box,
            float tankRadiusMetres = 0f)
        {
            PatchCount = UnityFloatMath.Max(1, patchCount);
            PatchWidthMetres = UnityFloatMath.Max(0.01f, patchWidthMetres);
            DepthMetres = UnityFloatMath.Max(0.01f, depthMetres);

            // Clamped rather than refused: World has already refused every tank it cannot build,
            // and a placer that threw on a shape the world accepted would be a second opinion
            // about the same geometry.
            Shape = tankRadiusMetres > 0f ? shape : WorldShape.Box;
            TankRadiusMetres = Shape == WorldShape.Tank ? tankRadiusMetres : 0f;

            PatchesAcross = UnityFloatMath.Clamp(patchesAcross, 1, PatchCount);
            PatchesAlong = UnityFloatMath.Max(1, PatchCount / PatchesAcross);

            // Floored at 0 rather than trusted: a negative radius would pass Sqrt as NaN and put
            // a newborn nowhere, which the free test would then reject 64 times over and file as
            // a crowded world.
            OffspringDispersalMetres = UnityFloatMath.Max(0f, offspringDispersalMetres);

            _rng = new Rng(Rng.SeedFor(seed, World.PlacementIndex));
        }

        /// <summary>
        /// The disc a newborn is set down in, about its parent, metres.
        /// <see cref="RunConfig.OffspringDispersalMetres"/>. 0 is D077's touching rule.
        /// </summary>
        public float OffspringDispersalMetres { get; }

        /// <summary>
        /// The sea bed, once there is one. Null leaves the depth a body is offered untouched,
        /// which is the pre-floor behaviour.
        /// </summary>
        /// <remarks>
        /// Settable after construction, as the Unity class's <c>Floor</c> is: the bed is built
        /// from the placer's own extents, so it cannot be a constructor argument without the two
        /// of them arguing about which came first.
        /// </remarks>
        public PlacementFloor Floor { get; set; }

        /// <summary>
        /// The shallowest y a body of <paramref name="radius"/> may be placed at, given the bed.
        /// </summary>
        private float LowestPlacement(float x, float z, float radius) =>
            Floor != null ? Floor.MinimumPlacementY(x, z, radius) : float.NegativeInfinity;

        /// <summary>
        /// The floor's clamp before any candidate exists — the flat path's one answer, and the
        /// shaped path's starting point at the middle of the footprint.
        /// </summary>
        private float LowestPlacement(float radius) =>
            LowestPlacement(0.5f * LengthMetres, 0.5f * WidthMetres, radius);

        /// <summary>Whether the floor under this world has a shape.</summary>
        private bool ShapedFloor => Floor != null && Floor.HasRelief;

        /// <summary>K — <see cref="RunConfig.HorizontalPatches"/>, floored at 1.</summary>
        public int PatchCount { get; }

        /// <summary>W = sqrt(area / K), metres. One patch's side, on both horizontal axes.</summary>
        public float PatchWidthMetres { get; }

        /// <summary>D — <see cref="RunConfig.WorldDepthMetres"/>.</summary>
        public float DepthMetres { get; }

        /// <summary>A — <see cref="RunConfig.PatchesAcross"/>, the patches across z.</summary>
        public int PatchesAcross { get; }

        /// <summary>K / A — the patches along x.</summary>
        public int PatchesAlong { get; }

        /// <summary>The container — <c>RunConfig.WorldShape</c>.</summary>
        public WorldShape Shape { get; }

        /// <summary>The tank's radius, m — 0 in a box. <c>World.TankRadiusMetres</c>.</summary>
        public float TankRadiusMetres { get; }

        /// <summary>Where the axis stands in the bounding square: <c>(R, R)</c>.</summary>
        private float Axis => TankRadiusMetres;

        /// <summary>W·K/A: the box's x extent, the way around the ring. 2R in a tank.</summary>
        public float LengthMetres =>
            Shape == WorldShape.Tank ? 2f * TankRadiusMetres : PatchWidthMetres * PatchesAlong;

        /// <summary>W·A: the box's z extent, the way around the other ring. 2R in a tank.</summary>
        public float WidthMetres =>
            Shape == WorldShape.Tank ? 2f * TankRadiusMetres : PatchWidthMetres * PatchesAcross;

        /// <summary>
        /// Whether a horizontal position is in the water, with <paramref name="clearance"/> held
        /// back from the glass. Always true in a box.
        /// </summary>
        public bool InTheWater(float x, float z, float clearance = 0f) =>
            Shape != WorldShape.Tank || TankGeometry.Inside(x, z, TankRadiusMetres, clearance);

        /// <summary>Bodies translated at a seam, running total.</summary>
        public long Wraps { get; private set; }

        /// <summary>Placement attempts that hit an occupied sphere, running total.</summary>
        public long Rejections { get; private set; }

        /// <summary>Births refused for want of room, running total — the crowded stillbirths.</summary>
        public long Refusals { get; private set; }

        /// <summary>
        /// Founder candidates turned down by <see cref="FounderAcceptance"/> — spots drawn in a
        /// desert (D109). Zero with no rule.
        /// </summary>
        public long DesertRefusals { get; private set; }

        /// <summary>
        /// D109's founder rule, or null: see <see cref="IBodyPlacement.FounderAcceptance"/>. Set
        /// by the world once it has a field to read it from.
        /// </summary>
        /// <remarks>
        /// <b>One more draw of the stream per candidate, only when set.</b> A candidate at
        /// probability p is kept when the next float is under p, so a spot in a desert (p = 0)
        /// is never kept and one in the fullest column (p = 1) always is. Off, no draw is made
        /// and the recorded world's stream is untouched. The attempt budget is eight times the
        /// usual with the rule on, because a tenth of the water at the islands' density means
        /// most candidates are refused and a founder that fails the budget is a stillbirth the
        /// floor then draws again.
        /// </remarks>
        public Func<Phenotype, float, float, float> FounderAcceptance { get; set; }

        /// <summary>The spatial hash's cell side, metres: 2× the largest body, floored at 1.</summary>
        public float CellMetres => _cellSize;

        /// <summary>Living bodies the hash currently holds.</summary>
        public int Occupants => _occupied.Count;

        // ------------------------------------------------------------------------- the geometry

        /// <summary>
        /// The patch a horizontal position falls in — <c>iz·(K/A) + ix</c>, numbered along x
        /// first, and a ring of equal area in a tank.
        /// </summary>
        public int PatchOf(float x, float z)
        {
            if (Shape == WorldShape.Tank)
            {
                return TankGeometry.RingOf(x, z, TankRadiusMetres, PatchCount);
            }

            int along = PatchesAlong;

            // A float-to-int cast of a floored float, not FloorToInt: the original's expression,
            // kept because the two differ on a value outside int's range and this one is the one
            // the record was made with.
            int ix = (int)UnityFloatMath.Floor(WrapAxis(x, LengthMetres) / PatchWidthMetres);
            ix %= along;
            if (ix < 0) ix += along;

            if (PatchesAcross == 1) return ix;

            int iz = (int)UnityFloatMath.Floor(WrapAxis(z, WidthMetres) / PatchWidthMetres);
            iz %= PatchesAcross;
            if (iz < 0) iz += PatchesAcross;

            return iz * along + ix;
        }

        /// <summary>
        /// How far apart two points are on the ring — the shortest way round, not through. The
        /// plain distance in a tank, where there is no shorter way round.
        /// </summary>
        public float ShortestDistance(Float3 a, Float3 b)
        {
            // UnityEngine.Vector3.Distance, transcribed — see UnityFloatMath.Distance.
            if (Shape == WorldShape.Tank) return UnityFloatMath.Distance(a, b);

            float dx = Shortest(a.X - b.X, LengthMetres);
            float dy = a.Y - b.Y;
            float dz = Shortest(a.Z - b.Z, WidthMetres);

            return UnityFloatMath.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>One axis of the minimum image: the separation folded into [−extent/2, extent/2].</summary>
        private static float Shortest(float d, float extent)
        {
            if (!(extent > 0f)) return d;

            return d - extent * UnityFloatMath.Round(d / extent);
        }

        /// <summary>Where a root that has left the box belongs, or false if it is still inside.</summary>
        /// <remarks>Always false in a tank, where the boundary is a collider.</remarks>
        public bool TryWrap(Float3 p, out Float3 wrapped)
        {
            wrapped = p;

            if (Shape == WorldShape.Tank) return false;

            float length = LengthMetres;
            float width = WidthMetres;

            if (p.X >= 0f && p.X < length && p.Z >= 0f && p.Z < width) return false;

            // A non-finite coordinate is not a body out of the box, it is a diverged body, and
            // the harness kills it at the next metabolic step. Teleporting it first would
            // replace the evidence with a number of our own.
            float sum = p.X + p.Z;
            if (float.IsNaN(sum) || float.IsInfinity(sum)) return false;

            wrapped = new Float3(WrapAxis(p.X, length), p.Y, WrapAxis(p.Z, width));
            Wraps++;
            return true;
        }

        private static float WrapAxis(float v, float extent)
        {
            if (v >= 0f && v < extent) return v;

            float folded = v - extent * UnityFloatMath.Floor(v / extent);

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
        public void Note(long creatureId, Float3 position, float radius)
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
            // Two of the largest bounding radius in the world, floored at 1 m.
            _cellSize = UnityFloatMath.Max(1f, 2f * _maxRadius);
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

        private long Key(Float3 p) => Key(
            UnityFloatMath.FloorToInt(p.X / _cellSize),
            UnityFloatMath.FloorToInt(p.Y / _cellSize),
            UnityFloatMath.FloorToInt(p.Z / _cellSize));

        /// <summary>
        /// A spatial hash rather than a dense grid. A collision costs a distance test and can
        /// never produce a wrong answer, because every candidate is checked against the sphere
        /// it actually is.
        /// </summary>
        private static long Key(int ix, int iy, int iz) =>
            ((long)ix * 73856093L) ^ ((long)iy * 19349663L) ^ ((long)iz * 83492791L);

        /// <summary>Whether a sphere of <paramref name="radius"/> at <paramref name="p"/> is clear.</summary>
        /// <remarks>
        /// And in the water, the whole sphere and not the centre — the water test takes
        /// <paramref name="radius"/> as a clearance (<c>logbook/specs/wall-clearance-spec.md</c>,
        /// Astra review F2). A refusal at the glass counts as a rejection exactly as a draw onto
        /// an occupied spot does.
        /// </remarks>
        private bool Free(Float3 p, float radius)
        {
            if (!InTheWater(p.X, p.Z, radius)) return false;

            if (!_gridBuilt) Build();

            // How far the search has to reach: the largest thing in the hash plus the thing being
            // placed. One cell in each direction in the ordinary case.
            int reach = UnityFloatMath.Max(
                1, UnityFloatMath.CeilToInt((radius + _maxRadius) / _cellSize));

            int cx = UnityFloatMath.FloorToInt(p.X / _cellSize);
            int cy = UnityFloatMath.FloorToInt(p.Y / _cellSize);
            int cz = UnityFloatMath.FloorToInt(p.Z / _cellSize);

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

                            // Float3's operator- and SqrMagnitude are componentwise in the same
                            // order as Vector3's, so this is the same float.
                            if ((other.Position - p).SqrMagnitude < span * span) return false;
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
                ? PatchOf(at.Position.X, at.Position.Z)
                : creature.Patch;
        }

        /// <summary>
        /// Reserves a newborn's spot at the size of the adult it grows into.
        /// </summary>
        public bool TryReserveOffspring(Organism parent, Phenotype adult, ref float heightY, out int patch)
        {
            patch = parent != null ? parent.Patch : 0;
            if (parent == null || adult == null) return false;

            float radius = BoundingRadius(adult);

            // A parent nothing has told us about cannot be bred beside. The fallback is the
            // founder rule rather than a refusal, which would file a bookkeeping gap as an
            // ecological fact.
            if (!_known.TryGetValue(parent.Id, out Occupant at))
            {
                return TryReserveFounder(adult, ref heightY, out patch);
            }

            // The two radii: the parent as it is now, and the child as it will be.
            float distance = at.Radius + radius;

            // Raised only, and only as far as it takes.
            float y = UnityFloatMath.Max(at.Position.Y, LowestPlacement(radius));

            // D092. Read once for the whole draw: false for every recorded world.
            bool shaped = ShapedFloor;

            for (int attempt = 0; attempt < AttemptBudget; attempt++)
            {
                // A direction in the horizontal plane and nowhere else.
                float angle = _rng.Range(0f, 2f * UnityFloatMath.PI);

                // How far out, and the only thing dispersal changes. At 0 this is `distance`,
                // the touching rule, with no draw taken and therefore no perturbation of the
                // stream: the branch is the old code, and a config at 0 replays the record.
                float reach = OffspringDispersalMetres > 0f
                    ? UnityFloatMath.Max(
                        distance, OffspringDispersalMetres * UnityFloatMath.Sqrt(_rng.NextFloat()))
                    : distance;

                // The box folds the draw back in at the far face; the tank has a wall there, so
                // the draw stands as it fell and Free refuses it if it landed past the glass.
                float candidateX = at.Position.X + reach * UnityFloatMath.Cos(angle);
                float candidateZ = at.Position.Z + reach * UnityFloatMath.Sin(angle);

                var candidate = Shape == WorldShape.Tank
                    ? new Float3(candidateX, y, candidateZ)
                    : new Float3(
                        WrapAxis(candidateX, LengthMetres), y, WrapAxis(candidateZ, WidthMetres));

                // The floor under this candidate rather than under the middle of the footprint —
                // D092. Never lowered.
                float placedY = y;

                if (shaped)
                {
                    placedY = UnityFloatMath.Max(y, LowestPlacement(candidate.X, candidate.Z, radius));

                    // The original wrote `candidate.y = placedY` in place; Float3 is readonly,
                    // so the other two components are carried across unchanged. Same value.
                    candidate = new Float3(candidate.X, placedY, candidate.Z);
                }

                if (!Free(candidate, radius)) { Rejections++; continue; }

                Reserve(candidate, radius);
                patch = PatchOf(candidate.X, candidate.Z);

                // Only when the bed actually moved it. The height the world admits the child at
                // has to be the height its body is built at.
                if (placedY > heightY) heightY = placedY;

                return true;
            }

            Refusals++;
            return false;
        }

        public bool TryReserveFounder(Phenotype body, ref float heightY, out int patch)
        {
            patch = 0;
            if (body == null) return false;

            float radius = BoundingRadius(body);

            // The founder draw runs to RunConfig.FounderDepthSpread, so the lottery offers depths
            // inside the rock. Clamped rather than redrawn: a redraw would change the depth
            // distribution founding is calibrated on.
            float y = UnityFloatMath.Max(heightY, LowestPlacement(radius));

            // D092, as in TryReserveOffspring above and for the same reason.
            bool shaped = ShapedFloor;

            Func<Phenotype, float, float, float> accept = FounderAcceptance;
            int budget = accept == null ? AttemptBudget : AttemptBudget * 8;

            for (int attempt = 0; attempt < budget; attempt++)
            {
                // Uniform in the box at the depth the world drew; uniform over the disc in a
                // tank, which is r = R·sqrt(u) and not R·u. Two draws either way, so the stream
                // advances by the same amount in both shapes.
                Float3 candidate;

                if (Shape == WorldShape.Tank)
                {
                    float r = TankRadiusMetres * UnityFloatMath.Sqrt(_rng.NextFloat());
                    float theta = _rng.Range(0f, 2f * UnityFloatMath.PI);

                    candidate = new Float3(
                        Axis + r * UnityFloatMath.Cos(theta), y, Axis + r * UnityFloatMath.Sin(theta));
                }
                else
                {
                    candidate = new Float3(
                        _rng.Range(0f, LengthMetres), y, _rng.Range(0f, WidthMetres));
                }

                // D109: planted where the matter is; D116: where the body's own food is. Asked
                // of the column before the floor and the crowd are, so a refused spot costs one
                // draw and no reservation.
                if (accept != null)
                {
                    float p = accept(body, candidate.X, candidate.Z);
                    if (!(p > 0f) || _rng.NextFloat() >= p) { DesertRefusals++; continue; }
                }

                // The floor under the point — D092. The disc draw above is untouched.
                float placedY = y;

                if (shaped)
                {
                    placedY = UnityFloatMath.Max(y, LowestPlacement(candidate.X, candidate.Z, radius));

                    // See TryReserveOffspring: the readonly rebuild of `candidate.y = placedY`.
                    candidate = new Float3(candidate.X, placedY, candidate.Z);
                }

                if (!Free(candidate, radius)) { Rejections++; continue; }

                Reserve(candidate, radius);
                patch = PatchOf(candidate.X, candidate.Z);

                if (placedY > heightY) heightY = placedY;

                return true;
            }

            Refusals++;
            return false;
        }

        private void Reserve(Float3 position, float radius)
        {
            _reserved = true;
            _reservation = new Occupant { Position = position, Radius = radius };
        }

        public void Commit(long creatureId)
        {
            if (!_reserved) return;

            _pending[creatureId] = _reservation.Position;

            // Occupied the moment it is committed rather than when the body is built: every
            // conception after this one in the same step has to see it.
            Occupy(_reservation);
            _reserved = false;
        }

        public void Release() => _reserved = false;

        /// <summary>
        /// Hands over — and forgets — where a newborn was promised it could be.
        /// </summary>
        public bool TryTakePlacement(long creatureId, out Float3 position)
        {
            if (!_pending.TryGetValue(creatureId, out position)) return false;

            _pending.Remove(creatureId);
            return true;
        }

        // ------------------------------------------------------------------ the whole state
        //
        // Three things here outlive a metabolic step and nothing else does. The generator, whose
        // draw sequence is the contract: a placer restored from the seed would put every body
        // born after the checkpoint somewhere else. The pending reservations, which are spots
        // promised to creatures the world has admitted and the harness has not yet built a body
        // for — dropped, each of those would be built at the height the world admitted it at,
        // which is an overlapping spawn and therefore a force in the physics. And the three
        // running counters, which the report differences per window.
        //
        // The occupied set, the spatial hash and the known positions are emptied by Begin and
        // refilled by Note at the top of every metabolic step, before anything reads them.

        /// <summary>Writes the generator, the promised spots and the three counters.</summary>
        public void WriteState(System.IO.BinaryWriter w)
        {
            Evosim.Core.StateIo.Tag(w, "PLAC");

            w.Write(_rng.State);
            w.Write(_rng.Increment);
            w.Write(_rng.HasSpareGaussian);
            w.Write(_rng.SpareGaussian);

            w.Write(Wraps);
            w.Write(Rejections);
            w.Write(Refusals);

            w.Write(_pending.Count);
            foreach (KeyValuePair<long, Float3> entry in _pending)
            {
                w.Write(entry.Key);
                Evosim.Core.StateIo.WriteFloat3(w, entry.Value);
            }

            // Held for completeness rather than for need: a reservation is committed or released
            // inside the world's own conception loop, so it is never standing between steps.
            w.Write(_reserved);
            Evosim.Core.StateIo.WriteFloat3(w, _reservation.Position);
            w.Write(_reservation.Radius);
        }

        /// <summary>Puts the placer back.</summary>
        public void ReadState(System.IO.BinaryReader r)
        {
            Evosim.Core.StateIo.Tag(r, "PLAC");

            ulong state = r.ReadUInt64();
            ulong increment = r.ReadUInt64();
            bool hasSpare = r.ReadBoolean();
            float spare = r.ReadSingle();
            _rng.RestoreState(state, increment, hasSpare, spare);

            Wraps = r.ReadInt64();
            Rejections = r.ReadInt64();
            Refusals = r.ReadInt64();

            int pending = r.ReadInt32();
            _pending.Clear();
            for (int i = 0; i < pending; i++)
            {
                long id = r.ReadInt64();
                _pending[id] = Evosim.Core.StateIo.ReadFloat3(r);
            }

            _reserved = r.ReadBoolean();
            _reservation = new Occupant
            {
                Position = Evosim.Core.StateIo.ReadFloat3(r),
                Radius = r.ReadSingle(),
            };

            Begin();
        }

        /// <summary>
        /// Radius of the sphere that contains a whole developed body, about its root part.
        /// </summary>
        /// <remarks>
        /// Spheres, not solids, so this over-reserves for a long thin body, which is the safe
        /// direction: the cost of over-reserving is a rejection and the cost of under-reserving
        /// is a depenetration impulse. Measured about the root part, which sits at the
        /// phenotype's origin by construction.
        /// </remarks>
        public static float BoundingRadius(Phenotype phenotype)
        {
            float radius = 0f;

            for (int i = 0; i < phenotype.Parts.Count; i++)
            {
                PhenotypePart part = phenotype.Parts[i];
                Float3 p = part.Position;
                Float3 h = part.HalfExtents;

                float centre = UnityFloatMath.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
                float corner = UnityFloatMath.Sqrt(h.X * h.X + h.Y * h.Y + h.Z * h.Z);

                radius = UnityFloatMath.Max(radius, centre + corner);
            }

            return UnityFloatMath.Max(0.01f, radius);
        }
    }
}

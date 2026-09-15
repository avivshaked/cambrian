using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// The sea bed — a solid floor under D077's box, made of rock rather than of arithmetic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it replaces.</b> D077 rule 4 gave the world a bottom by mirroring the surface: a
    /// body under y = −D was pushed up by its whole weight (<c>FluidConfig.SurfaceRestoringFraction</c>),
    /// which kept it in the world and was named "a placeholder for a real sea bed" in the rule's
    /// own amendment. A spring is not a bed, and it charged for the difference twice.
    /// A founder drawn on the floor bounced off it at about 1 m/s in the first seconds of a run,
    /// so <c>bestSpeed</c> during founding was a bounce and not locomotion (logbook/0065); and
    /// three newborns at 60 m in <c>r25q-s2</c> — ages 1, 8 and 13 s, all jointed, all within
    /// 0.8 m of the floor — went non-finite in a single step and were killed as <c>Diverged</c>
    /// (<c>runs/r25q-s2/*/diverged/</c>). A newborn is placed at its parent's depth; a parent
    /// sitting on the mirror is at a place where the buoyancy term flips sign across a plane, and
    /// a body straddling that plane is driven by a discontinuity every step it stays there.
    /// </para>
    /// <para>
    /// <b>What it is.</b> One static <see cref="BoxCollider"/> — no <c>Rigidbody</c> and no
    /// <c>ArticulationBody</c>, so PhysX treats it as immovable scenery and it costs the solver
    /// nothing but the contacts it generates. Its <i>top face</i> is at y = −D exactly, so the
    /// water's depth is unchanged and every field, light curve and layer index still reads the
    /// world it always read. It spans the whole ring with <see cref="SeamMarginMetres"/> of
    /// overhang past each face, because a root is wrapped at a seam while its limbs may still
    /// hang over the edge, and a body half over the end of the floor would fall past it.
    /// </para>
    /// <para>
    /// <b>Or the floor has a shape</b> — D092, <c>logbook/specs/bed-spec.md</c> item 10. When the
    /// world carries a <see cref="BedShape"/> with relief in it, the flat slab is replaced by one
    /// static <see cref="MeshCollider"/> built once at world start: a lattice at
    /// <see cref="LatticeMetres"/> over exactly the square the slab covered, each vertex at
    /// <c>Bed.FloorY(x, z)</c>. The map is defined past the rim (<see cref="BedShape.Height"/>'s
    /// own remark), so the mesh runs on under the glass with no clamp and therefore no crease at
    /// the wall. It is <c>convex</c> false — a height field is not convex and a convex hull of one
    /// would fill every hollow with rock — and is cooked by PhysX at construction, which is a cost
    /// paid once per world rather than once per step.
    /// </para>
    /// <para>
    /// <b>And the slab stays under it, as a backstop.</b> A thin triangle mesh is the one shape a
    /// fast body can pass through between two steps, and there is no sea bed below it to catch
    /// what does. So the box is kept, its top face <see cref="BackstopClearanceMetres"/> below the
    /// lowest vertex of the mesh: in ordinary play nothing ever touches it — a body resting in the
    /// deepest hollow is a metre above it — and a body that tunnels the mesh still meets rock
    /// rather than falling out of the world. It hangs on a child object of its own because two
    /// colliders on one transform cannot stand in two places.
    /// </para>
    /// <para>
    /// <b>The default physics material</b>, deliberately: <c>sharedMaterial</c> is left null, which
    /// is Unity's built-in default (friction 0.6, <b>bounciness 0</b>) unless the project overrides
    /// it, and <c>ProjectSettings/DynamicsManager.asset</c> does not. Bounce is the whole thing
    /// this replaces; a bed that returned any of a body's momentum would be the mirror again, only
    /// harder to see. Nothing here sets a material of its own, so a future project-wide default
    /// reaches the sea bed like everything else.
    /// </para>
    /// <para>
    /// <b>On the creatures' own layer</b> (<see cref="PhenotypeBuilder.CreatureLayer"/>), which is
    /// what makes bodies land on it: the collision matrix is left alone entirely, so the floor
    /// collides with exactly what creatures collide with. ⚠ That ties it to self-collision —
    /// <c>FluidEnvironment.ConfigureScene(selfCollision: false)</c> ignores layer 8 against layer 8
    /// and would switch the floor off with it. Every harness that has a box passes true; a harness
    /// that wants one and not the other needs a second layer, and should say so here.
    /// </para>
    /// <para>
    /// <b>It reports contacts</b> (<c>Collider.providesContacts</c>), so a benthic crowd is
    /// visible as a number rather than inferred from the depth column. The contacts it generates
    /// are counted separately from creature–creature pairs — see <c>Ecosystem.FloorContactPairs</c>
    /// — because a floor pair is a body resting and a creature pair is two animals meeting, and
    /// summing them would bury the second under the first.
    /// </para>
    /// </remarks>
    public sealed class SeaFloor
    {
        /// <summary>
        /// How far the floor reaches past each face of the box, metres.
        /// </summary>
        /// <remarks>
        /// The wrap moves a body when its <i>root</i> leaves the box (<c>SharedVolume.TryWrap</c>),
        /// and the rest of the articulation follows it — so at the instant of a crossing, and for
        /// a body whose root is inside but whose limbs are not, parts exist outside the ring.
        /// Five metres is several times the largest bounding radius this world has produced
        /// (0.63 m mean over founders, logbook/0064) and costs nothing: it is one static box either
        /// way. Under-reaching would be a hole at the seam, and a hole in the floor is exactly the
        /// failure this class exists to remove.
        /// </remarks>
        public const float SeamMarginMetres = 5f;

        /// <summary>How thick the slab is, metres.</summary>
        /// <remarks>
        /// Thick enough that nothing tunnels through it in one step — the fastest animal on record
        /// here manages 0.5 m/s, which is a centimetre per 0.02 s step — and thin enough that a
        /// body somehow inside it is nearer the top face than the bottom one, so PhysX's
        /// depenetration pushes it <i>up</i>. Two metres satisfies both by three orders of
        /// magnitude in one direction and by a metre in the other.
        /// </remarks>
        public const float ThicknessMetres = 2f;

        /// <summary>
        /// Clearance a placed body keeps between its bounding sphere and the bed, metres.
        /// </summary>
        /// <remarks>
        /// Five centimetres, on top of the body's own radius. The sphere over-reserves already
        /// (<c>SharedVolume.BoundingRadius</c> is a sphere about the root, not the solid), so this
        /// is not a size estimate — it is the gap that keeps a newborn from being <i>built</i>
        /// interpenetrating the rock, which is the one initial condition a solver cannot resolve
        /// without an impulse. The default contact offset is 0.01 m, so five is five of those.
        /// </remarks>
        public const float ClearanceMetres = 0.05f;

        /// <summary>The shaped bed's lattice, metres — <c>logbook/specs/bed-spec.md</c> item 10.</summary>
        /// <remarks>
        /// <b>Half a metre, which is the map's own column.</b> <see cref="BedShape"/> measures its
        /// range, its slope and its hollows on a half-metre lattice
        /// (<see cref="BedShape.MeasureStepMetres"/>), and the grid's own columns are a metre, so a
        /// finer collider would be resolving a floor nothing else in the world can see. At the
        /// campaign's 400 m² it is 8,712 triangles and at 100 m² it is 3,698 — a fifth of the
        /// spec's estimate, because the tank's footprint is small and the seam margin is most of
        /// what is being meshed.
        /// </remarks>
        public const float LatticeMetres = 0.5f;

        /// <summary>How far under the mesh's lowest point the backstop's top face sits, metres.</summary>
        /// <remarks>
        /// One metre: far enough that a body lying in the deepest hollow never touches it — the
        /// clearance a placed body keeps is five centimetres, and no body this world has grown is
        /// a metre across — and near enough that a body which has passed through the mesh is
        /// caught before it has left the water. It is a net and not a floor, and the class remarks
        /// say why there is one.
        /// </remarks>
        public const float BackstopClearanceMetres = 1f;

        private readonly BedShape _bed;
        private readonly float _topY;
        private readonly Mesh _mesh;

        private SeaFloor(
            GameObject root, BoxCollider collider, MeshCollider surface, Mesh mesh,
            BedShape bed, float topY, float lowestTopY)
        {
            Root = root;
            Collider = collider;
            Surface = surface;
            _mesh = mesh;
            _bed = bed;
            _topY = topY;
            LowestTopY = lowestTopY;

            // The one bodies actually land on: the mesh where there is one, the slab otherwise.
            // The backstop is deliberately not in this: it makes no contact in ordinary play, and
            // a world in which it does has a fault the `diverged` column will be saying more about
            // than the contact counter would.
            ColliderEntityId = surface != null ? surface.GetEntityId() : collider.GetEntityId();
        }

        /// <summary>The GameObject the collider hangs on, so the harness can find and destroy it.</summary>
        public GameObject Root { get; }

        /// <summary>
        /// The box: the whole floor on the flat path, and the backstop under the mesh on the
        /// shaped one.
        /// </summary>
        public BoxCollider Collider { get; }

        /// <summary>The shaped floor's mesh, or null on the flat path where the box is the floor.</summary>
        public MeshCollider Surface { get; }

        /// <summary>
        /// The collider's engine id, cached: contact reports arrive on a worker thread and
        /// <c>Object.GetEntityId()</c> is a main-thread call.
        /// </summary>
        public EntityId ColliderEntityId { get; }

        /// <summary>Whether this floor has a shape — <c>World.Bed.HasRelief</c>.</summary>
        public bool HasRelief => _bed != null;

        /// <summary>
        /// The lowest rock in the world, m: −<c>WorldDepthMetres</c> on the flat path and the
        /// mesh's own deepest vertex on the shaped one.
        /// </summary>
        /// <remarks>
        /// Measured off the mesh that was built rather than taken from
        /// <see cref="BedShape.LowestMetres"/>, which is the extreme over the <i>disc</i>: the
        /// mesh runs a seam margin past the rim, where the map carries on and a tilt keeps
        /// falling. <see cref="TankWall"/> starts the glass below this, so a hollow deeper than
        /// the old five-metre margin can never open a gap at the rim.
        /// </remarks>
        public float LowestTopY { get; }

        /// <summary>
        /// Builds the bed under <paramref name="volume"/>'s box.
        /// </summary>
        /// <param name="volume">The box, for its ring length, width and depth.</param>
        /// <param name="parent">
        /// The transform creatures are built under, so the floor moves with them if a harness ever
        /// offsets the world. Null puts it at the scene root, where the creatures also are.
        /// </param>
        /// <param name="bed">
        /// The floor's shape — <c>World.Bed</c> — or null for the flat slab, which is every
        /// recorded world. A bed with no relief is treated as none: the flat path has to be the
        /// code it always was, not the general one with a zero in it
        /// (<c>logbook/specs/bed-spec.md</c> item 12).
        /// </param>
        public static SeaFloor Build(SharedVolume volume, Transform parent = null, BedShape bed = null)
        {
            if (volume == null) return null;

            float length = volume.LengthMetres;
            float width = volume.WidthMetres;
            float topY = -volume.DepthMetres;

            var go = new GameObject("SeaFloor") { layer = PhenotypeBuilder.CreatureLayer };
            go.transform.SetParent(parent, worldPositionStays: false);

            if (bed == null || !bed.HasRelief) return Flat(go, length, width, topY);

            return Shaped(go, bed, length, width, topY);
        }

        /// <summary>The flat slab — D077's bed, unchanged to the character.</summary>
        private static SeaFloor Flat(GameObject go, float length, float width, float topY)
        {
            // The slab's centre: horizontally the middle of the ring, vertically half a thickness
            // below the top face, so that the *face* lands on −D rather than the centre.
            go.transform.localPosition = new Vector3(
                0.5f * length, topY - 0.5f * ThicknessMetres, 0.5f * width);
            go.transform.localRotation = Quaternion.identity;

            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                length + 2f * SeamMarginMetres,
                ThicknessMetres,
                width + 2f * SeamMarginMetres);

            // See the class remarks: the project default, not a material of our own.
            collider.sharedMaterial = null;

            // Without this PhysX resolves every contact with the bed and tells nobody — the same
            // opt-in that made the spike's first contact cell read zero pairs (logbook/0064).
            collider.providesContacts = true;

            // Transform writes do not reach PhysX until the next simulate or query when
            // Physics.autoSyncTransforms is off, and this project's DynamicsManager turns it off.
            // The floor is built before any body exists, so pushing it now is free and removes the
            // question of whether the first step saw the bed where we put it or at the origin.
            Physics.SyncTransforms();

            return new SeaFloor(go, collider, null, null, null, topY, topY);
        }

        /// <summary>The shaped floor — D092, <c>logbook/specs/bed-spec.md</c> item 10.</summary>
        private static SeaFloor Shaped(
            GameObject go, BedShape bed, float length, float width, float topY)
        {
            // The mesh carries world coordinates in its vertices, so the object itself stands at
            // the origin: one place where a height means what the map says it means, rather than a
            // height plus an offset that a reader of either has to remember.
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            Mesh mesh = HeightMesh(bed, length, width, out float lowestVertexY);

            var surface = go.AddComponent<MeshCollider>();
            surface.sharedMesh = mesh;

            // A height field is not convex, and the convex hull of one is a lid over every hollow
            // the round exists to make.
            surface.convex = false;

            // The project default and the contact report, both for the flat slab's reasons.
            surface.sharedMaterial = null;
            surface.providesContacts = true;

            // The net under the mesh — see the class remarks. Its own object, because a second
            // collider on this transform would have to stand where the mesh stands.
            float backstopTopY = lowestVertexY - BackstopClearanceMetres;

            var backstopGo = new GameObject("SeaFloorBackstop")
            {
                layer = PhenotypeBuilder.CreatureLayer,
            };

            backstopGo.transform.SetParent(go.transform, worldPositionStays: false);
            backstopGo.transform.localPosition = new Vector3(
                0.5f * length, backstopTopY - 0.5f * ThicknessMetres, 0.5f * width);
            backstopGo.transform.localRotation = Quaternion.identity;

            var backstop = backstopGo.AddComponent<BoxCollider>();
            backstop.size = new Vector3(
                length + 2f * SeamMarginMetres,
                ThicknessMetres,
                width + 2f * SeamMarginMetres);

            backstop.sharedMaterial = null;
            backstop.providesContacts = true;

            Physics.SyncTransforms();

            return new SeaFloor(go, backstop, surface, mesh, bed, topY, lowestVertexY);
        }

        /// <summary>
        /// The height map as a triangle mesh over the square the flat slab covers, facing up.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>No clamp at the rim.</b> The map is defined past the disc and deliberately not
        /// clamped there (<see cref="BedShape.Height"/>), so the lattice simply carries on under
        /// the glass. Clamping would put a crease exactly where the glass stands and exactly where
        /// a body is most likely to be pressed against something.
        /// </para>
        /// <para>
        /// <b>Wound so the normals point up</b>, which for Unity's left-handed clockwise-front
        /// convention is <c>(a, c, b)</c> and <c>(b, c, d)</c> with <c>a</c> the near-x, near-z
        /// corner, <c>b</c> the next along x and <c>c</c> the next along z — the same winding
        /// <c>TheatreSkin.Grid</c> uses for the sand it draws, so a collider and a drawing of it
        /// face the same way. Normals are not written at all: a <see cref="MeshCollider"/> takes
        /// its faces from the triangles, and a normal array on a collision mesh is bytes PhysX
        /// never reads.
        /// </para>
        /// </remarks>
        private static Mesh HeightMesh(
            BedShape bed, float length, float width, out float lowestVertexY)
        {
            float x0 = -SeamMarginMetres;
            float z0 = -SeamMarginMetres;
            float spanX = length + 2f * SeamMarginMetres;
            float spanZ = width + 2f * SeamMarginMetres;

            int nx = Mathf.Max(1, Mathf.CeilToInt(spanX / LatticeMetres));
            int nz = Mathf.Max(1, Mathf.CeilToInt(spanZ / LatticeMetres));

            int stride = nx + 1;
            var vertices = new Vector3[stride * (nz + 1)];
            var triangles = new int[nx * nz * 6];

            float lowest = float.MaxValue;

            for (int j = 0; j <= nz; j++)
            {
                float z = z0 + spanZ * j / nz;

                for (int i = 0; i <= nx; i++)
                {
                    float x = x0 + spanX * i / nx;
                    var y = (float)bed.FloorY(x, z);

                    vertices[j * stride + i] = new Vector3(x, y, z);
                    if (y < lowest) lowest = y;
                }
            }

            int t = 0;

            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int a = j * stride + i;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;

                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = b;

                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            var mesh = new Mesh { name = "SeaFloor Height Map", hideFlags = HideFlags.DontSave };

            // A 400 m² tank is 4,489 vertices and a larger footprint scales as the area, so the
            // 16-bit index format would wrap silently rather than refuse at about five times this.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            lowestVertexY = lowest;
            return mesh;
        }

        /// <summary>
        /// The floor's own height at a place, m — the box's top face on the flat path and the
        /// map's height on the shaped one.
        /// </summary>
        /// <remarks>
        /// The scalar <c>TopY</c> this replaced was the same number everywhere, which is exactly
        /// what stopped being true (<c>logbook/specs/bed-spec.md</c> item 11). Every reader of the
        /// floor's height now names the place it is asking about, and on the flat path every place
        /// gives the same answer, so the arithmetic is unchanged.
        /// </remarks>
        public float FloorYAt(float x, float z) =>
            _bed != null ? (float)_bed.FloorY(x, z) : _topY;

        /// <summary>
        /// The shallowest y a body of <paramref name="boundingRadius"/> may be placed at over
        /// <c>(x, z)</c> without its sphere reaching into the rock — D077's floor rule 2.
        /// </summary>
        public float MinimumPlacementY(float x, float z, float boundingRadius) =>
            FloorYAt(x, z) + Mathf.Max(0f, boundingRadius) + ClearanceMetres;

        /// <summary>Takes the bed out of the scene, immediately and in either mode.</summary>
        /// <remarks>
        /// <c>PhenotypeInstance.Destroy</c>'s reason, applied to the largest collider in the
        /// world: a bed deferred to the end of the frame is a rock still standing in the water
        /// while the next world is built into the same space, and in Play mode that frame is tens
        /// of physics steps (logbook/0083).
        /// </remarks>
        public void Destroy()
        {
            if (Root == null) return;

            Object.DestroyImmediate(Root);

            // The mesh is an asset of our own making rather than a component, so destroying the
            // object it hung on leaves it behind: a run that rebuilds its world would leak one
            // height map per world.
            if (_mesh != null) Object.DestroyImmediate(_mesh);
        }
    }
}

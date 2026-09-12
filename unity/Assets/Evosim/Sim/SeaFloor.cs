using UnityEngine;

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

        private SeaFloor(GameObject root, BoxCollider collider, float topY)
        {
            Root = root;
            Collider = collider;
            ColliderEntityId = collider.GetEntityId();
            TopY = topY;
        }

        /// <summary>The GameObject the collider hangs on, so the harness can find and destroy it.</summary>
        public GameObject Root { get; }

        public BoxCollider Collider { get; }

        /// <summary>
        /// The collider's engine id, cached: contact reports arrive on a worker thread and
        /// <c>Object.GetEntityId()</c> is a main-thread call.
        /// </summary>
        public EntityId ColliderEntityId { get; }

        /// <summary>y of the top face — −<c>WorldDepthMetres</c>.</summary>
        public float TopY { get; }

        /// <summary>
        /// Builds the bed under <paramref name="volume"/>'s box.
        /// </summary>
        /// <param name="volume">The box, for its ring length, width and depth.</param>
        /// <param name="parent">
        /// The transform creatures are built under, so the floor moves with them if a harness ever
        /// offsets the world. Null puts it at the scene root, where the creatures also are.
        /// </param>
        public static SeaFloor Build(SharedVolume volume, Transform parent = null)
        {
            if (volume == null) return null;

            float length = volume.LengthMetres;
            float width = volume.WidthMetres;
            float topY = -volume.DepthMetres;

            var go = new GameObject("SeaFloor") { layer = PhenotypeBuilder.CreatureLayer };
            go.transform.SetParent(parent, worldPositionStays: false);

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

            return new SeaFloor(go, collider, topY);
        }

        /// <summary>
        /// The shallowest y a body of <paramref name="boundingRadius"/> may be placed at without
        /// its sphere reaching into the rock — D077's floor rule 2.
        /// </summary>
        public float MinimumPlacementY(float boundingRadius) =>
            TopY + Mathf.Max(0f, boundingRadius) + ClearanceMetres;

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
        }
    }
}

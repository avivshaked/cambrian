using UnityEngine;
using Evosim.Core;

namespace Evosim.Sim
{
    /// <summary>
    /// The glass — a ring of static colliders round the tank, made of rock rather than of
    /// arithmetic. <c>fable-propose-aquarium.md</c> ruling 1,
    /// <c>logbook/specs/tank-spec.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it replaces.</b> D077 made the footprint periodic: a body that left by one face was
    /// translated back in at the opposite one, whole articulation, rotation and velocities
    /// untouched. The rule was chosen while nothing crossed a seam, and D088's transport field
    /// made it bite — <c>r36-s1</c> wrapped 1,143 bodies in a window at 1,154 alive, about once
    /// every hundred seconds each. On 2026-09-11 the owner watched that arm in the theatre and saw
    /// the consequence directly: bodies displaced, a jump rather than a death and a birth. A wall
    /// is the honest boundary for water that carries.
    /// </para>
    /// <para>
    /// <b>What it is.</b> <see cref="Segments"/> thin static <see cref="BoxCollider"/> slabs, each
    /// tangent to the circle at its own segment's midpoint and rotated to face the axis — a
    /// forty-eight-sided prism, not a circle. PhysX has a cylinder primitive for nothing and a
    /// mesh collider would be a mesh to keep in step with a radius; a prism of flat slabs is the
    /// same shape <see cref="SeaFloor"/> is made of, and its error against the true circle is
    /// <c>R(sec(π/48) − 1)</c>, which is 1.2 mm in a 5.6 m tank and 2.4 mm in an 11.3 m one.
    /// <b>The glass circumscribes the circle</b> — each slab's inner face is tangent at its own
    /// segment's midpoint, so the water is the circle everywhere plus those two millimetres at the
    /// corners, and never less. That is the direction that matters: the fields, the placer and the
    /// gyre all agree that the water is the circle, and a wall cutting inside it would be a wall
    /// standing in cells the grid calls live.
    /// </para>
    /// <para>
    /// <b>It runs from inside the bed to well above the waterline.</b> The bottom is the floor's
    /// top face less <see cref="SeaFloor.SeamMarginMetres"/>, so the wall and the bed overlap by
    /// metres and there is no crack at the join for a body to be squeezed into; the top is
    /// <see cref="FreeboardMetres"/> above y = 0, because the surface is a restoring force rather
    /// than a lid (D050) and a body that rides above the waterline must not be over the rim.
    /// </para>
    /// <para>
    /// <b>The default physics material and the creatures' own layer</b>, both for
    /// <see cref="SeaFloor"/>'s reasons: bounce is exactly what this must not have, and the
    /// collision matrix is left alone so the glass collides with whatever creatures collide with.
    /// ⚠ That ties it to self-collision, as the bed is tied
    /// (<c>FluidEnvironment.ConfigureScene</c>). It reports contacts, so a crowd against the glass
    /// is a number rather than an inference — they are counted with the bed's, since both are a
    /// body resting on the world rather than two animals meeting.
    /// </para>
    /// <para>
    /// <b>It is not the only thing keeping bodies in.</b> The gyre has no radial flow at the wall
    /// by construction, the placer never puts a body outside the disc, and
    /// <c>Ecosystem.CheckFinite</c> kills a root more than a metre past the glass as a counted
    /// <c>Diverged</c> death. This is the one of the four that acts every physics step; the others
    /// exist so that it never has to do anything dramatic.
    /// </para>
    /// </remarks>
    public sealed class TankWall
    {
        /// <summary>
        /// How many flat slabs stand in for the circle.
        /// </summary>
        /// <remarks>
        /// Forty-eight, the spec's number. How far a corner of the prism stands outside the circle
        /// is <c>R(sec(π/n) − 1)</c>, which at n = 48 is 0.0012 m in a 5.64 m tank and 0.0024 m in
        /// an 11.3 m one: under a quarter of the default contact offset either way, so the prism
        /// is a circle as far as the solver is concerned. Doubling n would quarter that and double
        /// the static colliders; halving it would put the error at half a centimetre, which a body
        /// could feel.
        /// </remarks>
        public const int Segments = 48;

        /// <summary>How thick the glass is, metres.</summary>
        /// <remarks>
        /// Half a metre, which is a quarter of the bed's thickness and the same argument: thick
        /// enough that nothing tunnels through it in one step — the fastest animal on record here
        /// manages 0.5 m/s, a centimetre per 0.02 s step — and thin enough that a body somehow
        /// inside it is nearer the inner face than the outer one, so PhysX's depenetration pushes
        /// it back into the water rather than out of the world.
        /// </remarks>
        public const float ThicknessMetres = 0.5f;

        /// <summary>How far the glass stands above the waterline, metres.</summary>
        /// <remarks>
        /// The surface is not a lid: <c>FluidConfig.SurfaceRestoringFraction</c> pushes a body
        /// that rises above y = 0 back down, and D050 stops upward net force there, but a body
        /// carrying momentum still overshoots. Five metres is several times anything seen, and it
        /// costs nothing — the slabs are static and their height is one number in a size.
        /// </remarks>
        public const float FreeboardMetres = 5f;

        private TankWall(GameObject root, BoxCollider[] colliders, EntityId[] ids, float radius)
        {
            Root = root;
            Colliders = colliders;
            ColliderEntityIds = ids;
            RadiusMetres = radius;
        }

        /// <summary>The GameObject the slabs hang under, so the harness can find and destroy them.</summary>
        public GameObject Root { get; }

        /// <summary>The slabs themselves, in order round the circle.</summary>
        public BoxCollider[] Colliders { get; }

        /// <summary>
        /// The slabs' engine ids, cached: contact reports arrive on a worker thread and
        /// <c>Object.GetEntityId()</c> is a main-thread call. <see cref="SeaFloor"/>'s reason.
        /// </summary>
        public EntityId[] ColliderEntityIds { get; }

        /// <summary>The tank's radius, m — the circle the slabs are tangent to.</summary>
        public float RadiusMetres { get; }

        /// <summary>
        /// Builds the glass round <paramref name="volume"/>'s tank, or nothing if it is a box.
        /// </summary>
        /// <param name="volume">The water, for its radius, its axis and its depth.</param>
        /// <param name="parent">
        /// The transform creatures are built under, so the glass moves with them if a harness ever
        /// offsets the world. Null puts it at the scene root, where the creatures also are.
        /// </param>
        public static TankWall Build(SharedVolume volume, Transform parent = null)
        {
            if (volume == null || volume.Shape != WorldShape.Tank) return null;

            float radius = volume.TankRadiusMetres;
            if (!(radius > 0f)) return null;

            // The axis stands at (R, R) of the bounding square, which is the same point every
            // field, the water and the placer measure their radius from.
            float axis = radius;

            // From inside the bed to above the surface — see the class remarks. The bed's top face
            // is at −depth and it overhangs by its seam margin, so starting the glass that far
            // below the rock leaves no crack at the join.
            float bottom = -volume.DepthMetres - SeaFloor.SeamMarginMetres;
            float top = FreeboardMetres;
            float height = top - bottom;

            var root = new GameObject("TankWall");
            root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            var colliders = new BoxCollider[Segments];
            var ids = new EntityId[Segments];

            // One slab per segment, its inner face tangent at the segment's midpoint. A
            // circumscribed n-gon's side is 2R·tan(π/n); the slab is made a tenth longer so that
            // neighbours overlap at their ends and there is no seam between two pieces of glass
            // for a limb to catch in.
            float chord = 2f * radius * Mathf.Tan(Mathf.PI / Segments) * 1.1f;

            for (int i = 0; i < Segments; i++)
            {
                float angle = 2f * Mathf.PI * (i + 0.5f) / Segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                var slab = new GameObject("TankWall" + i) { layer = PhenotypeBuilder.CreatureLayer };
                slab.transform.SetParent(root.transform, worldPositionStays: false);

                // The slab's centre sits half a thickness outside the circle, so its inner face —
                // not its middle — is the tangent line at the midpoint. Water inside the radius
                // stays water.
                float centre = radius + 0.5f * ThicknessMetres;

                slab.transform.localPosition = new Vector3(
                    axis + centre * cos, bottom + 0.5f * height, axis + centre * sin);

                // Rotated about y so that the slab's local x runs along the chord and its local z
                // points at the axis. Mathf's angle runs anticlockwise from +x in the x–z plane
                // and Unity's y rotation runs clockwise from +z, which is why this is a difference
                // rather than the angle itself.
                slab.transform.localRotation = Quaternion.Euler(0f, 90f - angle * Mathf.Rad2Deg, 0f);

                var collider = slab.AddComponent<BoxCollider>();
                collider.size = new Vector3(chord, height, ThicknessMetres);

                // See the class remarks: the project default, not a material of our own.
                collider.sharedMaterial = null;

                // Without this PhysX resolves every contact with the glass and tells nobody — the
                // same opt-in that made the spike's first contact cell read zero pairs
                // (logbook/0064).
                collider.providesContacts = true;

                colliders[i] = collider;
                ids[i] = collider.GetEntityId();
            }

            // Transform writes do not reach PhysX until the next simulate or query when
            // Physics.autoSyncTransforms is off, and this project's DynamicsManager turns it off.
            // The glass is built before any body exists, so pushing it now is free and removes the
            // question of whether the first step saw the wall where we put it or at the origin.
            Physics.SyncTransforms();

            return new TankWall(root, colliders, ids, radius);
        }

        /// <summary>Whether this collider id is one of the slabs.</summary>
        /// <remarks>
        /// A linear scan over forty-eight ids, called once per contact pair that is not a
        /// creature's — which is the same place <c>Ecosystem</c> compares one id against the bed's.
        /// A set would cost an allocation and a hash per contact to save twenty-four comparisons
        /// of an integer.
        /// </remarks>
        public bool Owns(EntityId id)
        {
            for (int i = 0; i < ColliderEntityIds.Length; i++)
            {
                if (ColliderEntityIds[i].Equals(id)) return true;
            }

            return false;
        }

        /// <summary>Takes the glass out of the scene, immediately and in either mode.</summary>
        /// <remarks>
        /// <see cref="SeaFloor.Destroy"/>'s reason: a wall deferred to the end of the frame is a
        /// wall still standing while the next world is built into the same space, and in Play mode
        /// that frame is tens of physics steps (logbook/0083).
        /// </remarks>
        public void Destroy()
        {
            if (Root == null) return;

            Object.DestroyImmediate(Root);
        }
    }
}

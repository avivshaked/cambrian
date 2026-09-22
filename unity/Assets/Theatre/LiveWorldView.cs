using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Core;
using Evosim.Farm;
using Evosim.Sim;
using Solver = Evosim.Dynamics.Creature;

namespace Evosim.Theatre
{
    /// <summary>
    /// The world the new engine is stepping, drawn as it steps — one transform per part per
    /// living body, moved every rendered frame from the solver's own state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it is, and what it is not.</b> <see cref="TheatreDynamicsReplay"/> runs the farm's
    /// harness inside the Editor on <c>Evosim.Dynamics</c>, and until this class existed it drew
    /// nothing: the skin dresses the <c>GameObject</c>s <c>Ecosystem</c> builds and this engine
    /// builds none. So the bodies here are built the way <see cref="SnapshotWorld"/> builds a
    /// reconstruction's — a transform per part, its visuals as children, no
    /// <c>ArticulationBody</c>, no collider, no rigidbody — and then, unlike a reconstruction,
    /// they are moved. Nothing in this class can decide a trajectory: it reads
    /// <c>Creature.Position</c> and <c>Creature.Rotation</c> after the solver has written them and
    /// writes transforms. The world would run identically with the view switched off, which is the
    /// only arrangement in which watching a run is safe.
    /// </para>
    /// <para>
    /// <b>The tree is flat, and the PhysX path's is not.</b> <c>PhenotypeBuilder</c> parents each
    /// part under its parent part because an articulation is a chain and the engine needs it to
    /// be; the solver here reports every link's <i>world</i> pose already, so nesting would mean
    /// undoing a parent's rotation on every child on every frame to arrive back at the number the
    /// solver handed over. The body's root object carries the root link's place and no rotation,
    /// and every part hangs off it directly, so a part's local rotation <i>is</i> its world
    /// rotation and a frame is two writes per part. What the skin needs is kept exactly:
    /// <see cref="TheatrePalette"/> reads a part index off the name <c>PartNN_nK</c> and finds the
    /// renderers by walking the root, and a neck is placed in its parent part's own local frame,
    /// none of which asks the parts to be nested.
    /// </para>
    /// <para>
    /// <b>Growth is a new phenotype, not a new body.</b> <c>Creature.Resize</c> replaces
    /// <c>Creature.Phenotype</c> with the adult scaled to the body fraction and keeps the link
    /// count, so a grown body is caught by comparing the object reference and answered by
    /// rewriting the visuals' local scales — the same thing
    /// <c>PhenotypeBuilder.ResizeColliderAndVisual</c> does on the other engine, and the same
    /// thing <see cref="TheatrePalette"/>'s aspect squash then expects to find. A resize that
    /// changed the part count, which nothing does today, rebuilds the body rather than guessing.
    /// </para>
    /// <para>
    /// <b>Painting is on a budget and posing is not.</b> A pose is two transform writes; a paint
    /// walks a body's renderers, rewrites a property block on each and refits every neck. So every
    /// body is posed every frame and the population is repainted in rotation, which is the
    /// arrangement <c>TheatreRunner.Repaint</c> already uses for the PhysX path and for the same
    /// reason. The tint therefore lags in a large world, exactly as it does there.
    /// </para>
    /// <para>
    /// <b>A body the solver has not built yet is not drawn.</b> A creature is conceived in the
    /// world's books before the harness reconciles a solver body for it, so
    /// <c>Simulation.TryPose</c> answers false for it for a step; it is skipped rather than drawn
    /// at the origin. A body whose links have gone non-finite is skipped too, counted in
    /// <see cref="SkippedNonFinite"/>, and left standing where it last was — a NaN written into a
    /// transform poisons the hierarchy and takes the camera with it, and the world is about to
    /// kill that body as a counted <c>Diverged</c> death anyway.
    /// </para>
    /// </remarks>
    public sealed class LiveWorldView : IDisposable
    {
        /// <summary>One body on screen.</summary>
        private sealed class LiveBody
        {
            public long Id;

            /// <summary>The solver body this was built from. A different one is a rebuild.</summary>
            public Solver Body;

            public Transform Root;

            /// <summary>One transform per link, in the solver's own link order.</summary>
            public Transform[] Parts;

            /// <summary>The visuals hanging off each part, in the order <see cref="Plan"/> lays them.</summary>
            public Transform[][] Visuals;

            /// <summary>The phenotype the visuals are currently sized for.</summary>
            public Phenotype Sized;

            /// <summary>The sweep this body was last seen alive in.</summary>
            public long Seen;

            /// <summary>
            /// The reserve tint as of that sweep, read while the living list was already in hand.
            /// </summary>
            /// <remarks>
            /// Kept here rather than looked up at paint time, because the only way to find an
            /// organism by id is a walk of <c>World.Living</c> and the paint budget would turn
            /// that into ninety-six walks of a population of thousands every frame.
            /// </remarks>
            public float Tint;
        }

        private readonly Simulation _sim;
        private readonly Dictionary<long, LiveBody> _bodies = new Dictionary<long, LiveBody>();
        private readonly List<long> _gone = new List<long>();
        private readonly List<LiveBody> _order = new List<LiveBody>();

        private GameObject _holder;
        private Material _plain;
        private long _sweep;
        private long _paintCursor;

        /// <summary>The skin's palette, or null for bare transforms and the plain material.</summary>
        public TheatrePalette Palette;

        /// <summary>Whether the palette paints the guilds. The runner's own switch.</summary>
        public bool ColourByCellType = true;

        /// <summary>Bodies repainted per frame, as <c>TheatreRunner.RepaintsPerFrame</c>.</summary>
        public int RepaintsPerFrame = 96;

        /// <summary>Bodies on screen.</summary>
        public int BodyCount => _bodies.Count;

        /// <summary>Part transforms on screen, summed over the bodies.</summary>
        public int PartCount { get; private set; }

        /// <summary>Bodies built since the view opened — one per birth it drew.</summary>
        public long Built { get; private set; }

        /// <summary>Bodies destroyed since the view opened — one per death it drew.</summary>
        public long Removed { get; private set; }

        /// <summary>Bodies rebuilt because the solver handed back a different body for the id.</summary>
        public long Rebuilt { get; private set; }

        /// <summary>Bodies resized in place because they grew.</summary>
        public long Resized { get; private set; }

        /// <summary>Poses refused this session because a link was not finite.</summary>
        public long SkippedNonFinite { get; private set; }

        public LiveWorldView(Simulation sim)
        {
            _sim = sim ?? throw new ArgumentNullException(nameof(sim));
            _holder = new GameObject("Live World");
        }

        /// <summary>Whether a body with this id is on screen.</summary>
        public bool Holds(long id) => _bodies.ContainsKey(id);

        /// <summary>The body's root transform, or null — what a camera would follow.</summary>
        public Transform RootOf(long id) =>
            _bodies.TryGetValue(id, out LiveBody body) ? body.Root : null;

        /// <summary>
        /// The creature a ray strikes first, or false when it strikes none — a click, answered.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It is arithmetic and not a raycast, because there is nothing here to raycast
        /// against.</b> A live body is transforms and renderers with no collider (see the class
        /// remarks), and giving each part one would put a static collider per link into a scene
        /// whose physics is not this world's, moved by hand every frame. So the ray is taken into
        /// each part's own frame by that transform's inverse and tested against the half-extents
        /// the phenotype carries, which is the slab test and nothing more.
        /// </para>
        /// <para>
        /// <b>A round part is picked as its box.</b> A sphere and a capsule are drawn inside the
        /// same half-extents, so a click at the very corner of one lands on it. That is an error
        /// of a few centimetres on a part a few centimetres across, and the alternative is three
        /// intersection routines kept in step with <see cref="Plan"/> forever.
        /// </para>
        /// <para>
        /// The part transforms carry no scale — <see cref="Build"/> puts every dimension on the
        /// visuals hanging off them — so the inverse-transformed direction keeps its length and
        /// the distance that comes back is metres along the ray.
        /// </para>
        /// </remarks>
        public bool Pick(Ray ray, out long id)
        {
            id = -1L;
            float nearest = float.PositiveInfinity;

            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                LiveBody live = entry.Value;
                Phenotype phenotype = live.Body?.Phenotype;
                if (phenotype == null) continue;

                int parts = System.Math.Min(live.Parts.Length, phenotype.PartCount);

                for (int i = 0; i < parts; i++)
                {
                    Transform part = live.Parts[i];
                    if (part == null) continue;

                    if (!Strikes(part, phenotype.Parts[i].HalfExtents, ray, out float at)) continue;
                    if (at >= nearest) continue;

                    nearest = at;
                    id = entry.Key;
                }
            }

            return id >= 0L;
        }

        /// <summary>The slab test, in the part's own frame.</summary>
        private static bool Strikes(Transform part, Float3 half, Ray ray, out float at)
        {
            at = 0f;

            Vector3 origin = part.InverseTransformPoint(ray.origin);
            Vector3 direction = part.InverseTransformDirection(ray.direction);

            float near = 0f;
            float far = float.PositiveInfinity;

            if (!Slab(origin.x, direction.x, Mathf.Abs(half.X), ref near, ref far)) return false;
            if (!Slab(origin.y, direction.y, Mathf.Abs(half.Y), ref near, ref far)) return false;
            if (!Slab(origin.z, direction.z, Mathf.Abs(half.Z), ref near, ref far)) return false;

            at = near;
            return true;
        }

        /// <summary>One axis of it: the interval the ray is inside this pair of planes.</summary>
        private static bool Slab(float origin, float direction, float half, ref float near, ref float far)
        {
            // Parallel to the slab: inside it for the whole ray, or outside it for the whole ray.
            if (Mathf.Abs(direction) < 1e-9f) return Mathf.Abs(origin) <= half;

            float one = (-half - origin) / direction;
            float other = (half - origin) / direction;

            if (one > other) { float swap = one; one = other; other = swap; }

            near = Mathf.Max(near, one);
            far = Mathf.Min(far, other);

            return near <= far;
        }

        /// <summary>Every part transform on screen, for a check that wants to scan them.</summary>
        public void CollectParts(List<Transform> into)
        {
            if (into == null) return;

            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                Transform[] parts = entry.Value.Parts;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] != null) into.Add(parts[i]);
                }
            }
        }

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Brings the scene up to the world: builds what was born, drops what died, and poses
        /// everything that is standing.
        /// </summary>
        /// <remarks>
        /// Called once per rendered frame and never per physics step. The solver takes tens of
        /// steps between two frames and a viewer sees the last of them; drawing the ones in
        /// between would cost the frames and show nothing.
        /// </remarks>
        public void Sync()
        {
            if (_holder == null) return;

            World world = _sim.World;
            IReadOnlyList<Organism> living = world.Living;

            _sweep++;
            int parts = 0;
            float reserveScale = _sim.Config.EnergyFullScaleSeconds;

            for (int i = 0; i < living.Count; i++)
            {
                Organism creature = living[i];

                // Conceived, not yet reconciled into a solver body: nothing to draw, and a body
                // drawn at the origin would be a creature the world does not have there.
                if (!_sim.TryPose(creature.Id, out Solver body) || body == null) continue;

                if (!_bodies.TryGetValue(creature.Id, out LiveBody live))
                {
                    live = Build(creature.Id, body);
                    _bodies[creature.Id] = live;
                    Built++;
                }
                else if (!ReferenceEquals(live.Body, body) ||
                         live.Parts.Length != body.Phenotype.PartCount)
                {
                    Destroy(live);
                    live = Build(creature.Id, body);
                    _bodies[creature.Id] = live;
                    Rebuilt++;
                }

                live.Seen = _sweep;
                live.Tint = TheatrePalette.Tint(creature.SecondsOfReserve, reserveScale);

                if (!ReferenceEquals(live.Sized, body.Phenotype))
                {
                    Resize(live, body.Phenotype);
                    Resized++;
                }

                Pose(live, body);
                parts += live.Parts.Length;
            }

            PartCount = parts;

            Sweep();
            Paint();
        }

        /// <summary>Destroys the bodies that were not in the living list this sweep.</summary>
        private void Sweep()
        {
            _gone.Clear();

            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                if (entry.Value.Seen != _sweep) _gone.Add(entry.Key);
            }

            for (int i = 0; i < _gone.Count; i++)
            {
                if (!_bodies.TryGetValue(_gone[i], out LiveBody body)) continue;

                Destroy(body);
                _bodies.Remove(_gone[i]);
                Removed++;
            }

            if (_gone.Count > 0) Palette?.PurgeDead();
        }

        /// <summary>
        /// Writes one body's links into its transforms.
        /// </summary>
        /// <remarks>
        /// The root object takes the root link's place and no rotation, so every part's local
        /// pose is its world pose less that one translation. Read in one pass and written in a
        /// second, so a body with one bad link is left alone whole rather than half moved.
        /// </remarks>
        private void Pose(LiveBody live, Solver body)
        {
            int links = System.Math.Min(live.Parts.Length, body.Links);
            if (links == 0) return;

            double[] position = body.Position;
            double[] rotation = body.Rotation;

            Vector3 root = default;

            for (int i = 0; i < links; i++)
            {
                double px = position[3 * i], py = position[3 * i + 1], pz = position[3 * i + 2];
                double rx = rotation[4 * i], ry = rotation[4 * i + 1];
                double rz = rotation[4 * i + 2], rw = rotation[4 * i + 3];

                if (!Finite(px) || !Finite(py) || !Finite(pz) ||
                    !Finite(rx) || !Finite(ry) || !Finite(rz) || !Finite(rw))
                {
                    SkippedNonFinite++;
                    return;
                }

                if (i == 0) root = new Vector3((float)px, (float)py, (float)pz);
            }

            live.Root.position = root;

            for (int i = 0; i < links; i++)
            {
                Transform part = live.Parts[i];
                if (part == null) continue;

                part.localPosition = new Vector3(
                    (float)position[3 * i] - root.x,
                    (float)position[3 * i + 1] - root.y,
                    (float)position[3 * i + 2] - root.z);

                // The writer's order is (x, y, z, w) — QuatD.Write's — and Unity's constructor's
                // is the same, component for component, as RecordedPoses reads a recorded row.
                part.localRotation = new Quaternion(
                    (float)rotation[4 * i],
                    (float)rotation[4 * i + 1],
                    (float)rotation[4 * i + 2],
                    (float)rotation[4 * i + 3]);
            }
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>Repaints a slice of the population, in rotation — <c>TheatreRunner.Repaint</c>.</summary>
        private void Paint()
        {
            if (Palette == null || _bodies.Count == 0) return;

            _order.Clear();
            foreach (KeyValuePair<long, LiveBody> entry in _bodies) _order.Add(entry.Value);

            int budget = Mathf.Clamp(RepaintsPerFrame, 1, _order.Count);

            for (int i = 0; i < budget; i++)
            {
                LiveBody live = _order[(int)(_paintCursor++ % _order.Count)];
                if (live.Root == null || live.Body == null) continue;

                Palette.Paint(live.Id, live.Root, live.Body.Phenotype, live.Tint, ColourByCellType);
            }
        }

        // ---------------------------------------------------------------- the bodies

        /// <summary>
        /// One body as transforms and renderers — <see cref="SnapshotWorld.Assemble"/>'s tree,
        /// flattened, and without the pose: <see cref="Pose"/> puts it where it is.
        /// </summary>
        private LiveBody Build(long id, Solver body)
        {
            Phenotype phenotype = body.Phenotype;
            int count = phenotype.PartCount;

            var root = new GameObject(
                string.Format(CultureInfo.InvariantCulture, "Creature {0}", id))
            {
                layer = PhenotypeBuilder.CreatureLayer,
            };

            root.transform.SetParent(_holder.transform, false);
            root.transform.localRotation = Quaternion.identity;

            var live = new LiveBody
            {
                Id = id,
                Body = body,
                Root = root.transform,
                Parts = new Transform[count],
                Visuals = new Transform[count][],
                Sized = phenotype,
                Seen = _sweep,
            };

            for (int i = 0; i < count; i++)
            {
                PhenotypePart part = phenotype.Parts[i];

                var go = new GameObject(
                    string.Format(CultureInfo.InvariantCulture, "Part{0:00}_n{1}", i, part.SourceNode))
                {
                    layer = PhenotypeBuilder.CreatureLayer,
                };

                go.transform.SetParent(root.transform, false);
                live.Parts[i] = go.transform;

                Plan(part, _sim.Config.Shapes.Resolve(part.ShapeId));

                var visuals = new Transform[_meshes.Count];

                for (int v = 0; v < _meshes.Count; v++)
                {
                    var visual = new GameObject("Visual") { layer = PhenotypeBuilder.CreatureLayer };
                    visual.transform.SetParent(go.transform, false);
                    visual.transform.localPosition = _offsets[v];
                    visual.transform.localScale = _scales[v];

                    if (_meshes[v] != null)
                    {
                        visual.AddComponent<MeshFilter>().sharedMesh = _meshes[v];

                        MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = PartMaterial;
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                    }

                    visuals[v] = visual.transform;
                }

                live.Visuals[i] = visuals;
            }

            return live;
        }

        /// <summary>
        /// Rewrites the visuals' offsets and scales for a body that has grown.
        /// </summary>
        /// <remarks>
        /// The transforms are kept and only their numbers change, so the palette's cache of this
        /// body's renderers stays good and its aspect squash reapplies itself on the next paint
        /// (<c>TheatrePalette.Reshape</c> watches for exactly this write). A plan that came back a
        /// different length would mean the shape itself had changed, which a uniform scale cannot
        /// do; the body is left as it was and the next sweep's reference check rebuilds it.
        /// </remarks>
        private void Resize(LiveBody live, Phenotype grown)
        {
            if (grown == null || grown.PartCount != live.Parts.Length) return;

            for (int i = 0; i < live.Parts.Length; i++)
            {
                PhenotypePart part = grown.Parts[i];
                Plan(part, _sim.Config.Shapes.Resolve(part.ShapeId));

                Transform[] visuals = live.Visuals[i];
                if (visuals == null || visuals.Length != _meshes.Count) continue;

                for (int v = 0; v < visuals.Length; v++)
                {
                    if (visuals[v] == null) continue;

                    visuals[v].localPosition = _offsets[v];
                    visuals[v].localScale = _scales[v];
                }
            }

            live.Sized = grown;
        }

        private void Destroy(LiveBody live)
        {
            if (live?.Root == null) return;

            UnityEngine.Object.DestroyImmediate(live.Root.gameObject);
            live.Root = null;
        }

        // ---------------------------------------------------------------- the visual plan

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Vector3> _offsets = new List<Vector3>();
        private readonly List<Vector3> _scales = new List<Vector3>();

        /// <summary>
        /// What a part is drawn with: <c>PhenotypeBuilder.VisualPlan</c>'s answer, in the same
        /// order <see cref="SnapshotWorld"/> lays it.
        /// </summary>
        /// <remarks>
        /// Duplicated rather than reached for, under the rule <see cref="SnapshotWorld"/> and
        /// <see cref="SoloCreature"/> are written under: <c>VisualPlan</c> is private to
        /// <c>Evosim.Sim</c> and widening that seam for the theatre's convenience would be
        /// changing the simulation's source for a viewer. Every dimension comes from the shape's
        /// own statics, so the copies cannot disagree about how large a part is.
        /// </remarks>
        private void Plan(PhenotypePart what, PartShape shape)
        {
            _meshes.Clear();
            _offsets.Clear();
            _scales.Clear();

            Float3 h = what.HalfExtents;

            switch (shape)
            {
                case SphereShape _:
                {
                    float r = SphereShape.Radius(h);
                    Add(Sphere, Vector3.zero, Vector3.one * (2f * r));
                    break;
                }

                case CapsuleShape _:
                {
                    float r = CapsuleShape.Radius(h);
                    float span = CapsuleShape.HalfSpan(h);

                    if (span > 0f)
                    {
                        Add(Cylinder, Vector3.zero, new Vector3(2f * r, span, 2f * r));
                    }

                    Add(Sphere, new Vector3(0f, span, 0f), Vector3.one * (2f * r));
                    Add(Sphere, new Vector3(0f, -span, 0f), Vector3.one * (2f * r));
                    break;
                }

                default:
                {
                    Add(Cube, Vector3.zero, new Vector3(
                        2f * Mathf.Abs(h.X), 2f * Mathf.Abs(h.Y), 2f * Mathf.Abs(h.Z)));
                    break;
                }
            }
        }

        private void Add(Mesh mesh, Vector3 offset, Vector3 scale)
        {
            _meshes.Add(mesh);
            _offsets.Add(offset);
            _scales.Add(scale);
        }

        // ---------------------------------------------------------------- the meshes

        private static Mesh _cube;
        private static Mesh _sphere;
        private static Mesh _cylinder;

        /// <summary>
        /// The engine's own primitives, by the names <c>TheatreMeshes.RoundedFor</c> reads, so the
        /// skin swaps a live body's meshes exactly as it swaps a simulated one's.
        /// </summary>
        private static Mesh Cube =>
            _cube != null ? _cube : (_cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx"));

        private static Mesh Sphere =>
            _sphere != null ? _sphere : (_sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));

        private static Mesh Cylinder =>
            _cylinder != null ? _cylinder : (_cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"));

        /// <summary>The material a part is born with, which the skin replaces on the first paint.</summary>
        private Material PartMaterial
        {
            get
            {
                if (_plain != null) return _plain;

                Shader shader =
                    Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

                if (shader == null) return null;

                _plain = new Material(shader) { name = "Theatre Live Part" };
                return _plain;
            }
        }

        // ---------------------------------------------------------------- housekeeping

        public void Dispose()
        {
            foreach (KeyValuePair<long, LiveBody> entry in _bodies) Destroy(entry.Value);

            _bodies.Clear();
            _order.Clear();
            _gone.Clear();
            PartCount = 0;

            Palette?.Clear();

            if (_holder != null)
            {
                UnityEngine.Object.DestroyImmediate(_holder);
                _holder = null;
            }

            if (_plain != null)
            {
                UnityEngine.Object.DestroyImmediate(_plain);
                _plain = null;
            }
        }
    }
}

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
    /// <para>
    /// <b>Fed, it draws what it is handed.</b> Built with a config and no simulation, the view
    /// steps nothing and reads no solver: a caller hands it each frame's bodies between
    /// <see cref="BeginFrame"/> and <see cref="EndFrame"/>, each with a phenotype and its parts'
    /// places and rotations, and the view builds, resizes, poses, sweeps and paints them exactly
    /// as <see cref="Sync"/> does a solver's. That is the film window's player
    /// (<see cref="FilmWindowWorld"/>): the farm stepped the world and wrote its poses, and this
    /// draws them. One body tree, one paint budget and one pick serve both.
    /// </para>
    /// </remarks>
    public sealed class LiveWorldView : IDisposable
    {
        /// <summary>One body on screen.</summary>
        private sealed class LiveBody
        {
            public long Id;

            /// <summary>
            /// What this was built from: the solver body when the view syncs a simulation, or the
            /// caller's key (the body's unscaled phenotype) when it is fed. A different one is a
            /// rebuild.
            /// </summary>
            public object Source;

            public Transform Root;

            /// <summary>One transform per link, in the solver's own link order.</summary>
            public Transform[] Parts;

            /// <summary>The visuals hanging off each part, in the order <see cref="Plan"/> lays them.</summary>
            public Transform[][] Visuals;

            /// <summary>The phenotype the visuals are currently sized for.</summary>
            public Phenotype Sized;

            /// <summary>The sweep this body was last seen alive in.</summary>
            public long Seen;

            /// <summary>Whether the palette has dressed this body at least once since it was built.</summary>
            public bool Dressed;

            /// <summary>
            /// The reserve tint as of that sweep, read while the living list was already in hand.
            /// </summary>
            /// <remarks>
            /// Kept here rather than looked up at paint time, because the only way to find an
            /// organism by id is a walk of <c>World.Living</c> and the paint budget would turn
            /// that into ninety-six walks of a population of thousands every frame.
            /// </remarks>
            public float Tint;

            /// <summary>How much of the body is drawn this frame, 1 whole: a birth fading in, a corpse fading out.</summary>
            public float Fade = 1f;

            /// <summary>
            /// The fade last written, and the renderers it was written to. A paint clears the
            /// renderers, because it puts the opaque material back and may add a neck.
            /// </summary>
            public float FadeApplied = 1f;
            public Renderer[] FadeRenderers;
            public Phenotype FadeFor;

            /// <summary>D277's crumble progress as of the last <see cref="Place"/>, 0 whole and 1 gone; always 0 with the dial off.</summary>
            public float Crumble;

            /// <summary>The crumble the palette last painted this body with, to tell when it has moved far enough to repaint.</summary>
            public float CrumblePainted;

            /// <summary>The death cue's drain as of the last <see cref="Place"/>, 0 at the death row to 1 drained; -1, none, always with the dial off.</summary>
            public float Drain = -1f;

            /// <summary>The drain the palette last painted this body with, to tell when it has moved far enough to repaint.</summary>
            public float DrainPainted = -1f;

            /// <summary>The one skin over the body's tissues (EVOSIM_THEATRE_ONE_SKIN), or null: always null with the dial off.</summary>
            public TheatreOneSkin Skin;

            /// <summary>True while the one skin is left unbuilt (EVOSIM_THEATRE_DRESS_NEAR): the body is built whole but for it. Never true with the dial off.</summary>
            public bool SkinPending;

            /// <summary>The phenotype the body was built from while its skin was left pending: the skin is built from it later, as it would have been then. Null with the dial off.</summary>
            public Phenotype SkinFrom;
        }

        /// <summary>The simulation synced, or null when the view is fed.</summary>
        private readonly Simulation _sim;

        /// <summary>The world's rules: the part shapes a body is drawn with.</summary>
        private readonly RunConfig _config;

        private readonly Dictionary<long, LiveBody> _bodies = new Dictionary<long, LiveBody>();
        private readonly List<long> _gone = new List<long>();
        private readonly List<LiveBody> _order = new List<LiveBody>();

        private GameObject _holder;
        private Material _plain;
        private long _sweep;
        private long _paintCursor;

        /// <summary>Whether the one skins are left unbuilt until asked for (<see cref="DeferSkins"/>). False unless a film turns it on.</summary>
        private bool _defer;

        /// <summary>The bodies a still close film can show (<see cref="ApplySet"/>), or null before the film has swept them. Read only while deferring.</summary>
        private HashSet<long> _nearSet;

        /// <summary>The skin's palette, or null for bare transforms and the plain material.</summary>
        public TheatrePalette Palette;

        /// <summary>Whether the palette paints the guilds. The runner's own switch.</summary>
        public bool ColourByCellType = true;

        /// <summary>Bodies repainted per frame, as <c>TheatreRunner.RepaintsPerFrame</c>.</summary>
        public int RepaintsPerFrame = 96;

        /// <summary>
        /// Whether this view draws corpses (D127): the palette paints its bodies as dead tissue, grey
        /// and dim, whatever their reserve. The film window keeps its corpses in a view of their own.
        /// </summary>
        public bool Corpses;

        /// <summary>
        /// Whether this view draws remains (D220): the palette paints its bodies dulled and part-grey, in their own
        /// colours, with no reserve. The film window keeps its remains in a view of their own, behind
        /// <c>EVOSIM_THEATRE_REMAINS</c>; false paints as every view did.
        /// </summary>
        public bool Remains;

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
            _config = sim.Config;
            _holder = new GameObject("Live World");
            if (TheatreBoxLog.On) TheatreBoxLog.Register(this); // BOXLOG
        }

        /// <summary>
        /// A fed view: no simulation, and every frame's bodies handed in by the caller
        /// (<see cref="BeginFrame"/>, <see cref="Place"/>, <see cref="EndFrame"/>).
        /// </summary>
        public LiveWorldView(RunConfig config, string name = "Fed World")
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _holder = new GameObject(string.IsNullOrEmpty(name) ? "Fed World" : name);
            if (TheatreBoxLog.On) TheatreBoxLog.Register(this); // BOXLOG
        }

        /// <summary>Whether this view is fed rather than synced to a simulation.</summary>
        public bool Fed => _sim == null;

        /// <summary>Whether a body with this id is on screen.</summary>
        public bool Holds(long id) => _bodies.ContainsKey(id);

        /// <summary>The body's root transform, or null — what a camera would follow.</summary>
        public Transform RootOf(long id) =>
            _bodies.TryGetValue(id, out LiveBody body) ? body.Root : null;

        // portraitfrom:begin
        /// <summary>
        /// A drawn body's phenotype at the size it is drawn at, and its part transforms (unit scale, at each part's centre, in the
        /// part's own axes; <paramref name="parts"/> indexes like the phenotype's parts), or false when the body is not on screen.
        /// It reads only; the portrait from a body point (<c>SafariPlans.PortraitFromPoint</c>) is its one caller.
        /// </summary>
        public bool TryBody(long id, out Phenotype sized, out Transform[] parts)
        {
            sized = null;
            parts = null;

            if (!_bodies.TryGetValue(id, out LiveBody body) || body.Sized == null || body.Parts == null) return false;

            sized = body.Sized;
            parts = body.Parts;
            return true;
        }
        // portraitfrom:end

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
                Phenotype phenotype = live.Sized;
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

            if (_sim == null)
            {
                throw new InvalidOperationException(
                    "A fed view has no simulation to sync: hand it each frame through BeginFrame, " +
                    "Place and EndFrame.");
            }

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

                // The skin pattern's key, from the genome (EVOSIM_THEATRE_PATTERN; returns at once with it off).
                TheatrePattern.Register(creature.Id, creature.Genome);
                TheatreSkinMap.Register(creature.Id, creature.Genome, _config); // EVOSIM_THEATRE_SKIN_MAP; returns at once with it off

                LiveBody live = Ensure(
                    creature.Id, body, body.Phenotype,
                    TheatrePalette.Tint(creature.SecondsOfReserve, reserveScale));

                Pose(live, body);
                parts += live.Parts.Length;
            }

            PartCount = parts;

            Sweep();
            Paint();

            // The marine producers' bend with the current: each blade's flow, after the pose and the paint. Not entered with that dial off.
            if (TheatrePalette.AlgaBending) PushFlow();
        }

        // ---------------------------------------------------------------- the flow past the blades

        /// <summary>
        /// A body's velocity in the world's frame, m/s, for a fed view, or null for none: the root's move between
        /// the recorded poses either side of the frame on screen (<c>FilmWindowWorld.VelocityOf</c> is exactly this).
        /// Read only under the bend with the current (<c>EVOSIM_THEATRE_ALGA_BEND</c>), to take the blade's own
        /// motion out of the water's. A synced view needs none, since the solver carries every link's velocity.
        /// </summary>
        public Func<long, Vector3> VelocityOf;

        /// <summary>
        /// The run's second of the frame just placed, for a fed view's reading of the current, or null to take the
        /// second the skin was last pushed (<see cref="TheatreSkin.WaterSecond"/>), which trails a frame placed before
        /// its render pushes it. Read only under the bend with the current.
        /// </summary>
        public Func<double> SecondOf;

        private Vector3[] _flow = new Vector3[0];

        /// <summary>
        /// Hands the palette, for every body on screen, the water's flow past each of its parts
        /// (<see cref="TheatrePalette.Flow"/>), for the bend with the current. The flow is the world's current
        /// where the part is less the part's own velocity: from the solver's own arrays in a synced view
        /// (<c>Creature.Water</c>, <c>Creature.Velocity</c>, what its drag reads), and in a fed view from the
        /// run's current as the snow's drift reads it (<see cref="TheatreSkin.WaterAt"/>, at the part's place) less
        /// <see cref="VelocityOf"/>. Zero where neither source is reachable, which bends nothing.
        /// </summary>
        private void PushFlow()
        {
            if (Palette == null) return;

            TheatreSkin skin = TheatreSkin.Current;

            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                LiveBody live = entry.Value;
                if (live.Root == null || live.Parts == null) continue;
                if (_nearSet != null && !_nearSet.Contains(live.Id)) continue; // a body outside the near set is never painted

                int count = live.Parts.Length;
                if (_flow.Length < count) _flow = new Vector3[count];

                if (_sim != null)
                {
                    var solver = live.Source as Solver;
                    int links = solver != null ? System.Math.Min(count, solver.Links) : 0;

                    for (int i = 0; i < count; i++)
                    {
                        _flow[i] = i < links
                            ? new Vector3(
                                (float)(solver.Water[3 * i] - solver.Velocity[3 * i]),
                                (float)(solver.Water[3 * i + 1] - solver.Velocity[3 * i + 1]),
                                (float)(solver.Water[3 * i + 2] - solver.Velocity[3 * i + 2]))
                            : Vector3.zero;
                    }
                }
                else
                {
                    Vector3 own = VelocityOf != null ? VelocityOf(live.Id) : Vector3.zero;
                    bool water = skin != null && skin.HasRunWater;
                    double second = water ? (SecondOf != null ? SecondOf() : skin.WaterSecond) : double.NaN;

                    for (int i = 0; i < count; i++)
                    {
                        Vector3 current = water && live.Parts[i] != null ? skin.WaterAt(live.Parts[i].position, second) : Vector3.zero;
                        _flow[i] = current - own;
                    }
                }

                Palette.Flow(live.Id, live.Root, _flow, count);
            }
        }

        /// <summary>
        /// The body on screen for an id, built, rebuilt or resized so that it draws
        /// <paramref name="phenotype"/>: the one path both <see cref="Sync"/> and a fed frame take.
        /// </summary>
        /// <remarks>
        /// A different <paramref name="source"/>, or a part count that no longer matches, is a
        /// rebuild; the same source with a different phenotype object is a growth, answered by a
        /// resize in place.
        /// </remarks>
        private LiveBody Ensure(long id, object source, Phenotype phenotype, float tint)
        {
            if (!_bodies.TryGetValue(id, out LiveBody live))
            {
                live = Build(id, source, phenotype);
                _bodies[id] = live;
                Built++;
            }
            else if (!ReferenceEquals(live.Source, source) ||
                     live.Parts.Length != phenotype.PartCount)
            {
                Destroy(live);
                live = Build(id, source, phenotype);
                _bodies[id] = live;
                Rebuilt++;
            }

            live.Seen = _sweep;
            live.Tint = tint;

            if (!ReferenceEquals(live.Sized, phenotype))
            {
                Resize(live, phenotype);
                Resized++;

                // A resize writes the plan's raw scales, and only the palette's reshape squashes
                // them to the genome's three half-extents. Left to the rotation's budget, a crowd
                // of 1,800 at 96 a frame waited up to 19 frames for it, and every growth step
                // (15 frames apart at 30 fps) drew a growing body large and then small again: the
                // body growing back and forth that the owner saw in round 48's window films (2026-09-25). Marked
                // undressed, it is reshaped by the next DressUndressed, which runs before every
                // captured frame.
                live.Dressed = false;
            }

            return live;
        }

        // ---------------------------------------------------------------- a fed frame

        private int _fedParts;
        private bool _inFrame;

        /// <summary>Opens a fed frame: every body not placed before <see cref="EndFrame"/> is removed.</summary>
        public void BeginFrame()
        {
            if (_sim != null)
            {
                throw new InvalidOperationException("A synced view takes its frames from Sync.");
            }

            _sweep++;
            _fedParts = 0;
            _inFrame = true;
        }

        /// <summary>
        /// One body in a fed frame: <paramref name="phenotype"/> drawn with its root link at
        /// <paramref name="root"/>, each part at <paramref name="root"/> plus its entry in
        /// <paramref name="positions"/> and turned by its entry in <paramref name="rotations"/>,
        /// which is what <see cref="RecordedPoses.Apply"/> hands back. False, and the body left
        /// where it last stood, when a number is not finite or the arrays are short.
        /// </summary>
        /// <param name="source">
        /// What the body is, for the rebuild test: a different object is a different body (a plan
        /// that moved), the same one with a different <paramref name="phenotype"/> is a growth.
        /// </param>
        /// <param name="scale">
        /// One scale on the whole body about its root, 1 for a body drawn as it is: a corpse's shrink,
        /// which moves every part toward the root with it and so leaves the phenotype, and the
        /// palette's dressing of it, alone.
        /// </param>
        /// <param name="crumble">
        /// D277's progress of a crumbling remains body, 0 whole to 1 gone (<c>EVOSIM_THEATRE_CRUMBLE</c>): the palette dulls it
        /// further as it grows, and the body is painted again once it has moved more than 0.05 since its last paint. 0 for
        /// every caller but the film window's crumble.
        /// </param>
        /// <param name="drain">
        /// The death cue's drain of a dead body's remains, 0 at its death row to 1 drained (<c>EVOSIM_THEATRE_DEATH_CUE</c>,
        /// <see cref="TheatreDeathCue.DrainOf"/>): the palette and the body shader fade it toward a dull grey and take its sheen by that
        /// share, and the body is painted again whenever it changes. -1, none, for every other caller.
        /// </param>
        public bool Place(
            long id, object source, Phenotype phenotype, Vector3 root,
            Vector3[] positions, Quaternion[] rotations, float tint = 1f, float scale = 1f, float fade = 1f, float crumble = 0f,
            float drain = -1f)
        {
            if (!_inFrame) throw new InvalidOperationException("Place outside BeginFrame and EndFrame.");
            if (_holder == null || phenotype == null || source == null) return false;

            int count = phenotype.PartCount;
            if (count == 0 || positions == null || rotations == null ||
                positions.Length < count || rotations.Length < count)
            {
                return false;
            }

            // Checked before anything is built, so a body whose first pose is not finite is never
            // drawn at the origin, and one already standing stays where it last stood (the
            // synced path's rule for a link gone non-finite).
            bool finite = Finite(root.x) && Finite(root.y) && Finite(root.z) && Finite(scale) && scale > 0f;

            for (int i = 0; finite && i < count; i++)
            {
                Vector3 p = positions[i];
                Quaternion r = rotations[i];

                finite =
                    Finite(p.x) && Finite(p.y) && Finite(p.z) &&
                    Finite(r.x) && Finite(r.y) && Finite(r.z) && Finite(r.w);
            }

            if (!finite)
            {
                SkippedNonFinite++;

                if (_bodies.TryGetValue(id, out LiveBody standing))
                {
                    standing.Seen = _sweep;
                    _fedParts += standing.Parts.Length;
                }

                return false;
            }

            LiveBody live = Ensure(id, source, phenotype, tint);
            live.Fade = Finite(fade) ? Mathf.Clamp01(fade) : 1f;
            _fedParts += live.Parts.Length;

            // D277: the crumble's dulling is repainted before the next capture once it has moved far enough to see. A corpse is
            // already grey, so its view never repaints for it. Not entered while both this and the last value are 0.
            if (crumble > 0f || live.Crumble > 0f)
            {
                if (!Corpses && Mathf.Abs(crumble - live.CrumblePainted) > 0.05f) live.Dressed = false;
                live.Crumble = crumble;
            }

            // The death cue's drain, repainted at every change, so its second has no step (it holds at 1 after, and is not repainted
            // again). Not entered while both this and the last value are -1, which they always are with the dial off.
            if (drain >= 0f || live.Drain >= 0f)
            {
                if (drain != live.DrainPainted) live.Dressed = false;
                live.Drain = drain;
            }

            live.Root.position = root;

            if (live.Root.localScale.x != scale)
            {
                live.Root.localScale = new Vector3(scale, scale, scale);

                // The skin's light through its tissue reads its thickness in metres, so it hears of the shrink.
                live.Skin?.Rescale();
            }

            for (int i = 0; i < count && i < live.Parts.Length; i++)
            {
                Transform part = live.Parts[i];
                if (part == null) continue;

                part.localPosition = positions[i];
                part.localRotation = rotations[i];
            }

            // The skin's bounds from the bones as just placed (null with the dial off).
            live.Skin?.Posed();

            return true;
        }

        /// <summary>
        /// Keeps a body standing where it last stood through a fed frame that could not place it,
        /// rather than letting the frame's sweep remove it. False when there is no such body.
        /// </summary>
        /// <remarks>
        /// For a frame whose pose does not fit the body the caller can develop, which a film
        /// window has for one frame where the farm rebuilt a body after the frame was written: a
        /// body that vanished for a thirtieth of a second would be a flicker nothing happened in.
        /// </remarks>
        public bool Keep(long id)
        {
            if (!_inFrame) throw new InvalidOperationException("Keep outside BeginFrame and EndFrame.");
            if (!_bodies.TryGetValue(id, out LiveBody standing)) return false;

            standing.Seen = _sweep;
            _fedParts += standing.Parts.Length;
            return true;
        }

        /// <summary>Closes a fed frame: the bodies not placed in it are removed, and a slice is painted.</summary>
        public void EndFrame()
        {
            if (!_inFrame) return;

            _inFrame = false;
            PartCount = _fedParts;

            Sweep();
            Paint();
            ApplyFades();

            if (TheatrePalette.AlgaBending) PushFlow();
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>
        /// Writes the fade of each body into its renderers when it has moved: a newborn fading in,
        /// a corpse fading out. Outside the paint budget, because a fade moves every frame and a body
        /// repainted once in several frames would step through it. A body whole and written whole
        /// costs nothing; a rebuild or a paint mid-fade gathers the renderers again and writes.
        /// </summary>
        private void ApplyFades()
        {
            if (Palette == null) return;

            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                LiveBody live = entry.Value;
                if (live.Root == null) continue;
                if (live.Fade >= 1f && live.FadeApplied >= 1f) continue;

                bool stale = live.FadeRenderers == null || live.FadeFor != live.Sized;

                // A fade that has barely moved is not written again, except at its ends.
                if (!stale && live.Fade > 0f && live.Fade < 1f &&
                    Mathf.Abs(live.Fade - live.FadeApplied) < 1f / 512f)
                {
                    continue;
                }

                if (stale)
                {
                    live.FadeRenderers = live.Root.GetComponentsInChildren<Renderer>(true);
                    live.FadeFor = live.Sized;
                }

                Palette.SetFade(live.FadeRenderers, live.Fade);
                live.FadeApplied = live.Fade;
            }
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

            // The skin's bounds from the bones as just placed (null with the dial off).
            live.Skin?.Posed();
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
                if (live.Root == null || live.Sized == null) continue;

                // The near set (EVOSIM_THEATRE_DRESS_NEAR): the cursor has walked over the body as it always does, so every body in
                // the set is refreshed on the frame it would have been; one outside it is passed over unpainted.
                if (_nearSet != null && !_nearSet.Contains(live.Id)) continue;

                Palette.Paint(live.Id, live.Root, live.Sized, live.Tint, ColourByCellType, Corpses, Remains, live.Crumble, live.Drain);
                live.CrumblePainted = live.Crumble;
                live.DrainPainted = live.Drain;
                live.Skin?.Dress(Palette, live.Id);
                live.Dressed = true;
                live.FadeRenderers = null;
            }
        }

        /// <summary>
        /// How many drawn bodies the palette has not yet dressed: still the plain material and the
        /// raw shapes, because painting is on a budget of <see cref="RepaintsPerFrame"/> a frame.
        /// </summary>
        /// <remarks>
        /// A body is built plain and the rotation reaches it within <c>count / budget</c> frames,
        /// so a restored crowd of seven hundred is partly raw for eight frames, which a person
        /// never notices and a film's first frame did (r46-s1 at 5,000 s, 2026-09-23).
        /// </remarks>
        public int UndressedCount
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<long, LiveBody> entry in _bodies)
                {
                    if (_nearSet != null && !_nearSet.Contains(entry.Key)) continue; // the near set is the film's whole crowd
                    if (!entry.Value.Dressed) n++;
                }

                return n;
            }
        }

        /// <summary>
        /// How many part renderers still carry the plain material a body is built with: the
        /// renderer-level check behind <see cref="UndressedCount"/>, which is only a flag.
        /// </summary>
        public int PlainRendererCount()
        {
            if (_plain == null) return 0;

            int n = 0;
            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                if (_nearSet != null && !_nearSet.Contains(entry.Key)) continue;

                Transform[][] visuals = entry.Value.Visuals;
                if (visuals == null) continue;

                foreach (Transform[] part in visuals)
                {
                    if (part == null) continue;
                    foreach (Transform visual in part)
                    {
                        if (visual == null) continue;
                        var renderer = visual.GetComponent<MeshRenderer>();
                        if (renderer != null && renderer.sharedMaterial == _plain) n++;
                    }
                }
            }

            return n;
        }

        /// <summary>
        /// Marks every body undressed, so the next <see cref="DressUndressed"/> paints the whole
        /// crowd again: what a change of <see cref="TheatrePalette.InFocus"/> needs to reach every
        /// body at once rather than a slice a frame.
        /// </summary>
        public void Redress()
        {
            foreach (KeyValuePair<long, LiveBody> entry in _bodies) entry.Value.Dressed = false;
        }

        /// <summary>Dresses every body the palette has not reached yet, off budget. Returns how many.</summary>
        public int DressUndressed()
        {
            if (Palette == null) return 0;

            int n = 0;
            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                LiveBody live = entry.Value;
                if (live.Dressed || live.Root == null || live.Sized == null) continue;
                if (_nearSet != null && !_nearSet.Contains(live.Id)) continue; // EVOSIM_THEATRE_DRESS_NEAR: outside the set, never painted

                long paintStart = DressTimers.Now(); // the film's timing: dress line
                Palette.Paint(live.Id, live.Root, live.Sized, live.Tint, ColourByCellType, Corpses, Remains, live.Crumble, live.Drain);
                live.CrumblePainted = live.Crumble;
                live.DrainPainted = live.Drain;
                long skinStart = DressTimers.Now();
                DressTimers.PaintMs += (skinStart - paintStart) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                DressTimers.Paints++;
                live.Skin?.Dress(Palette, live.Id);
                DressTimers.SkinDressMs += DressTimers.Since(skinStart);
                live.Dressed = true;
                live.FadeRenderers = null;
                n++;
            }

            // The film dresses after the frame's fades were written, so a newborn dressed here
            // would be captured on the opaque material for one frame: the pop at its first frame.
            if (n > 0) ApplyFades();

            // A body dressed here for the first time takes its flow now, so its first captured frame leans as its second does.
            if (n > 0 && TheatrePalette.AlgaBending) PushFlow();

            return n;
        }

        // ---------------------------------------------------------------- the bodies

        /// <summary>
        /// One body as transforms and renderers — <see cref="SnapshotWorld.Assemble"/>'s tree,
        /// flattened, and without the pose: <see cref="Pose"/> puts it where it is.
        /// </summary>
        private LiveBody Build(long id, object source, Phenotype phenotype)
        {
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
                Source = source,
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

                Plan(part, _config.Shapes.Resolve(part.ShapeId));

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

                if (TheatreMouthMarks.Enabled) TheatreMouthMarks.Attach(go.transform, phenotype, i);
            }

            if (_defer) NoteOrgans(phenotype); // EVOSIM_THEATRE_DRESS_NEAR; not entered with the dial off

            // The one skin (EVOSIM_THEATRE_ONE_SKIN), meshed once here from the adult and never per
            // frame. With the dial off nothing is made.
            if (TheatreOneSkin.Enabled)
            {
                if (_defer && SkinDeferred(id, phenotype))
                {
                    // Left unbuilt: the body is whole but for its skin, and keeps its root, which the plan reads for every body.
                    live.SkinPending = true;
                    live.SkinFrom = phenotype;
                    TheatreOneSkin.EnsureBuildKeywords();
                }
                else
                {
                    live.Skin = TheatreOneSkin.Build(
                        id, live.Root, live.Parts, live.Visuals, phenotype, source, _config.Shapes, PartMaterial, Palette);
                }
            }

            return live;
        }

        // ---------------------------------------------------------------- dressing only the near set

        /// <summary>
        /// Leaves every body's one skin unbuilt until it is asked for (EVOSIM_THEATRE_DRESS_NEAR): the film's first frame builds
        /// the bodies whole but for the skin. Called by the film window's opening, before the first frame is shown, and only
        /// for a film the dial can serve. Not entered with the dial off.
        /// </summary>
        public void DeferSkins()
        {
            _defer = true;
            _nearSet = null;
        }

        /// <summary>True while skins are being left unbuilt, or the near set is in force.</summary>
        public bool Deferring => _defer;

        /// <summary>True once the near set is in force: bodies outside it are not painted, built or counted.</summary>
        public bool NearActive => _nearSet != null;

        /// <summary>
        /// Builds every pending skin and lets the view paint every body again, as it did before <see cref="DeferSkins"/>: the way
        /// out when the film turns out to be one the dial cannot serve (it follows its subject, or has nothing to frame).
        /// </summary>
        public int EndDeferral()
        {
            _defer = false;
            _nearSet = null;
            return BuildPending(null);
        }

        /// <summary>
        /// Builds one body's pending skin and brings it to the dress state it has when the skin is built at the body's first
        /// frame: the subject, just before its drawn bounds are read. False when the body has no pending skin.
        /// </summary>
        public bool BuildSkinOf(long id)
        {
            if (!_defer || !_bodies.TryGetValue(id, out LiveBody live) || !live.SkinPending) return false;

            BuildPendingSkin(live);
            return true;
        }

        /// <summary>
        /// Puts the near set in force: the pending skins of the bodies in it are built, in the order the view built the bodies
        /// (the window's body order), and from here a body outside it is neither painted nor counted. Returns the skins built.
        /// </summary>
        public int ApplySet(HashSet<long> set)
        {
            _nearSet = set;
            return BuildPending(set);
        }

        /// <summary>The bodies on screen inside the near set, with the skins built and the skins still pending, over all bodies.</summary>
        public void NearCounts(out int inSet, out int built, out int pending)
        {
            inSet = 0;
            built = 0;
            pending = 0;

            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                if (_nearSet == null || _nearSet.Contains(entry.Key)) inSet++;
                if (entry.Value.Skin != null) built++;
                if (entry.Value.SkinPending) pending++;
            }
        }

        /// <summary>
        /// A diagnostic (EVOSIM_THEATRE_DRESS_NEAR_AUDIT): the bodies outside the near set with an enabled renderer whose bounds meet
        /// the frustum of a camera at this eye, rotation and lens. Reads only. Empty unless the near set is in force.
        /// </summary>
        public void OutsideSetInView(Vector3 eye, Quaternion rotation, float fieldOfView, float aspect, float far, List<long> hits)
        {
            hits.Clear();
            if (_nearSet == null) return;

            Matrix4x4 view = Matrix4x4.TRS(eye, rotation, new Vector3(1f, 1f, -1f)).inverse;
            Matrix4x4 lens = Matrix4x4.Perspective(Mathf.Clamp(fieldOfView, 5f, 120f), aspect, 0.05f, far);
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(lens * view);

            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                if (_nearSet.Contains(entry.Key) || entry.Value.Root == null) continue;

                foreach (Renderer r in entry.Value.Root.GetComponentsInChildren<Renderer>())
                {
                    if (r.enabled && GeometryUtility.TestPlanesAABB(planes, r.bounds))
                    {
                        hits.Add(entry.Key);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// A diagnostic (EVOSIM_THEATRE_DRESS_NEAR_PICK): every enabled renderer whose bounds, projected by this view-projection,
        /// cover a screen pixel (from the bottom left, Unity's), as "id:renderer(set Y/N/-, dressed, skin built/pending/none)".
        /// Reads only.
        /// </summary>
        public void PickAt(Matrix4x4 viewProjection, float x, float y, int width, int height, string tag, List<string> hits)
        {
            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                LiveBody live = entry.Value;
                if (live.Root == null) continue;

                foreach (Renderer r in live.Root.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;

                    Bounds b = r.bounds;
                    float x0 = float.PositiveInfinity, y0 = float.PositiveInfinity, x1 = float.NegativeInfinity, y1 = float.NegativeInfinity;
                    bool front = false;

                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) != 0 ? 1f : -1f, (c & 2) != 0 ? 1f : -1f, (c & 4) != 0 ? 1f : -1f));
                        Vector4 clip = viewProjection * new Vector4(corner.x, corner.y, corner.z, 1f);
                        if (clip.w <= 1e-4f) continue;
                        front = true;
                        float sx = (clip.x / clip.w * 0.5f + 0.5f) * width, sy = (clip.y / clip.w * 0.5f + 0.5f) * height;
                        x0 = Mathf.Min(x0, sx); x1 = Mathf.Max(x1, sx); y0 = Mathf.Min(y0, sy); y1 = Mathf.Max(y1, sy);
                    }

                    if (!front || x < x0 || x > x1 || y < y0 || y > y1) continue;

                    string set = _nearSet == null ? "-" : _nearSet.Contains(entry.Key) ? "Y" : "N";
                    string skin = live.Skin != null ? "built" : live.SkinPending ? "pending" : "none";
                    hits.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}:{2}(set {3}, dressed {4}, skin {5}, {6})",
                        tag, entry.Key, r.name, set, live.Dressed ? "Y" : "N", skin, r.sharedMaterial != null ? r.sharedMaterial.name : "no material"));
                }
            }
        }

        private int BuildPending(HashSet<long> only)
        {
            int n = 0;

            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                LiveBody live = entry.Value;
                if (!live.SkinPending || live.Root == null) continue;
                if (only != null && !only.Contains(entry.Key)) continue;

                BuildPendingSkin(live);
                n++;
            }

            return n;
        }

        /// <summary>
        /// The skin the body would have had at its first frame, built now from its adult key and the size it is drawn at, then
        /// dressed if the palette has painted the body since (<c>Paint</c> then <c>Dress</c>, as ever). A body mid-fade is
        /// painted again first, which puts the opaque material back for the skin to copy, and its fade written again after.
        /// </summary>
        private void BuildPendingSkin(LiveBody live)
        {
            live.SkinPending = false;

            // Built from the phenotype the body had when it was built, as the skin would have been then: a body that has grown
            // since works its adult out from another scale, and the rounding moved a leaf's edge by a pixel (64705's proof,
            // 2026-11-01). The growth since is then one resize, which sets every bone's scale outright, as each step did.
            Phenotype from = live.SkinFrom ?? live.Sized;
            live.SkinFrom = null;

            live.Skin = TheatreOneSkin.Build(
                live.Id, live.Root, live.Parts, live.Visuals, from, live.Source, _config.Shapes, PartMaterial, Palette);

            if (live.Skin != null && !ReferenceEquals(from, live.Sized)) live.Skin.Resize(live.Sized);

            // The fade's list of renderers gathers again, now that the skin's is among them.
            live.FadeRenderers = null;

            if (live.Skin == null || !live.Dressed || Palette == null) return;

            bool fading = live.Fade < 1f || live.FadeApplied < 1f;
            if (fading)
            {
                Palette.Paint(live.Id, live.Root, live.Sized, live.Tint, ColourByCellType, Corpses, Remains, live.Crumble, live.Drain);
                live.CrumblePainted = live.Crumble;
                live.DrainPainted = live.Drain;
            }

            live.Skin.Dress(Palette, live.Id);
            if (fading) ApplyFades();
        }

        /// <summary>
        /// Whether a body's skin is built at once rather than left pending: inside the near set, or when it carries a nose and the
        /// nose skin's shader keyword (set only by a skin built with a nose merged into it) is not yet on.
        /// </summary>
        private bool SkinDeferred(long id, Phenotype phenotype)
        {
            if (_nearSet != null && _nearSet.Contains(id)) return false;

            // EVOSIM_THEATRE_DRESS_NEAR_EAGER=1, a diagnostic: every skin built at the body's first frame as without the dial, so
            // only the paint is left to the near set; it separates a late skin build from the paint in a proof that fails.
            if (Environment.GetEnvironmentVariable("EVOSIM_THEATRE_DRESS_NEAR_EAGER") == "1") return false;

            return !(TheatreNosePit.OnSkin && !TheatreNosePit.SkinKeywordOn && TheatreNosePit.Carries(phenotype));
        }

        /// <summary>
        /// The organ keywords a body would switch on by being painted (the mouth's, the nose pit's), switched on when the body is
        /// built, whether or not it is ever painted, so a body left outside the near set still counts as it does without the dial.
        /// The nose skin's keyword is set by building a skin with a nose in it, which <see cref="SkinDeferred"/> lets happen.
        /// </summary>
        private static void NoteOrgans(Phenotype phenotype)
        {
            TheatreMouth2.EnsureKeywordFor(phenotype);
            TheatreNosePit.EnsureKeywordFor(phenotype);
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
                Plan(part, _config.Shapes.Resolve(part.ShapeId));

                Transform[] visuals = live.Visuals[i];
                if (visuals == null || visuals.Length != _meshes.Count) continue;

                for (int v = 0; v < visuals.Length; v++)
                {
                    if (visuals[v] == null) continue;

                    visuals[v].localPosition = _offsets[v];
                    visuals[v].localScale = _scales[v];
                }
            }

            if (TheatreMouthMarks.Enabled)
            {
                for (int i = 0; i < live.Parts.Length; i++) TheatreMouthMarks.Attach(live.Parts[i], grown, i);
            }

            live.Sized = grown;

            // The skin's bones take the growth as a scale; the skin is not rebuilt.
            live.Skin?.Resize(grown);
        }

        private void Destroy(LiveBody live)
        {
            if (live == null) return;

            live.Skin?.Release();
            live.Skin = null;

            if (live.Root == null) return;

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

                // FAMILYMESH begin
                case SuperellipsoidShape _ when TheatreFamilyMesh.Wants(what):
                {
                    Add(TheatreFamilyMesh.For(what.Family), Vector3.zero, TheatreFamilyMesh.ScaleOf(what));
                    break;
                }
                // FAMILYMESH end

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

        // BOXLOG begin
        /// <summary>
        /// The box counter's look at this view (<c>EVOSIM_THEATRE_BOX_LOG</c>, <see cref="TheatreBoxLog"/>): for every body, every
        /// part with an enabled renderer in the frustum whose mesh is the engine's cube or a rounded cube is a box; the
        /// superellipsoid is counted apart. It reads transforms, renderers and meshes and writes nothing but the tally and the log.
        /// </summary>
        internal void ScanBoxes(Plane[] planes, TheatreBoxLog.Tally tally)
        {
            foreach (KeyValuePair<long, LiveBody> entry in _bodies)
            {
                LiveBody live = entry.Value;
                if (live.Root == null || live.Sized == null || live.Visuals == null) continue;

                bool inView = false;
                bool boxBody = false;

                Renderer skinned = live.Skin != null ? live.Skin.DrawnRenderer : null;
                if (skinned != null && skinned.enabled && GeometryUtility.TestPlanesAABB(planes, skinned.bounds)) inView = true;

                int parts = Mathf.Min(live.Visuals.Length, live.Sized.PartCount);

                for (int p = 0; p < parts; p++)
                {
                    Transform[] visuals = live.Visuals[p];
                    if (visuals == null) continue;

                    bool box = false;
                    bool rounded = false;

                    for (int v = 0; v < visuals.Length; v++)
                    {
                        if (visuals[v] == null) continue;

                        var renderer = visuals[v].GetComponent<MeshRenderer>();
                        if (renderer == null || !renderer.enabled) continue;
                        if (!GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;

                        inView = true;

                        var filter = visuals[v].GetComponent<MeshFilter>();
                        Mesh mesh = filter != null ? filter.sharedMesh : null;

                        if (TheatreMeshes.IsPrimitiveCube(mesh) || TheatreMeshes.IsRoundedCube(mesh)) box = true;
                        else if (TheatreNoBox.Is(mesh)) rounded = true;
                    }

                    if (rounded) tally.RoundedParts++;
                    if (!box) continue;

                    boxBody = true;
                    tally.BoxParts++;
                    TheatreBoxLog.PartSeen(live.Id, p, live.Sized.Parts[p], live.Skin != null);
                }

                if (inView) tally.Bodies++;
                if (boxBody) tally.BoxBodies++;
            }
        }
        // BOXLOG end

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
            if (TheatreBoxLog.On) TheatreBoxLog.Unregister(this); // BOXLOG

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

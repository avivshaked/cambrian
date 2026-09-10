using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// What a creature looks like in the theatre — three colours for what it is made of, and a
    /// brightness for how much reserve it is holding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a view mode, not the creature's appearance.</b> DESIGN.md §5A.5 reserves part
    /// colour as an evolvable trait — camouflage, warning colouration, display — and painting
    /// bodies by cell type in the ordinary view would spend exactly the channel that trait needs.
    /// So it is a mode, it can be turned off, and it is never what a creature looks like.
    /// <c>PhenotypeBuilder.ApplyCellTypeColours</c> makes the same distinction for the sandbox.
    /// </para>
    /// <para>
    /// <b>Three colours, not seven.</b> The question a viewer asks of a world at a glance is who
    /// eats what: producers, stomachs, everything else. A seven-colour key is a legend to
    /// memorise, and the guilds it would distinguish (link, neural, buoyancy) are not the ones
    /// the ecology turns on.
    /// </para>
    /// <para>
    /// <b>The guild is carried on the rim, not on the face.</b> Since the skin landed
    /// (<see cref="TheatreSkin"/>) the body colour is a dark, desaturated version of the guild's
    /// hue and the guild's own colour goes to the shader's Fresnel rim, where a grazing edge
    /// catches it. That is the dark field arrangement the technique reading argues for, and it is
    /// also more legible: a small body against near black water is mostly edge.
    /// </para>
    /// <para>
    /// <b>Muted in HSL, not in RGB.</b> Species: ALRE found raw RGB colours drift garish and moved
    /// its creature colouring to HSL for that reason (research/theatre-look, [SP1] [SP2]). The
    /// caps here are the same idea: the hue is the guild's and is never touched, saturation and
    /// lightness are capped, so a guild stays recognisable without shouting.
    /// </para>
    /// <para>
    /// <b>Applied through one <see cref="MaterialPropertyBlock"/>.</b> Every part shares one
    /// material and therefore one draw-call batch; instancing a material per part would break
    /// batching for a debug view, which is the wrong trade at any population worth watching. The
    /// block is reused across calls, so painting allocates nothing after a body's first pass.
    /// A block drops those renderers out of the SRP Batcher, which is the documented cost of
    /// per-instance properties [U3]; that strategy was already this class's and it is kept.
    /// </para>
    /// <para>
    /// <b>Why the renderers are found by hierarchy and not asked for.</b>
    /// <c>CreatureInstance</c> carries a renderer-to-part table, and <c>Ecosystem</c> keeps every
    /// instance private — the theatre must not reach into that file. So each body's renderers are
    /// gathered once, from the root the id map paired it with, and the part each one belongs to is
    /// read from the name <c>PhenotypeBuilder</c> gives it (<c>Part07_n2</c>). Gathered once per
    /// body and cached: <c>GetComponentsInChildren</c> per creature per frame would be the most
    /// expensive thing in the viewer.
    /// </para>
    /// </remarks>
    public sealed class TheatrePalette
    {
        public Color Photosynthetic = new Color(0.34f, 0.72f, 0.36f);
        public Color Absorptive = new Color(0.90f, 0.55f, 0.22f);
        public Color Structural = new Color(0.72f, 0.74f, 0.78f);

        /// <summary>
        /// The colour a joint's neck is drawn in.
        /// </summary>
        /// <remarks>
        /// Deliberately off the guild wheel. Green, orange and grey are what a body is made of;
        /// this is what a body can do, and reading the two off one scale would mean a jointed
        /// producer and a jointless stomach could not be told apart at a glance. Framsticks
        /// colours function the same way and keeps muscle on its own colour [FR].
        /// </remarks>
        public Color Jointed = new Color(0.95f, 0.35f, 0.70f);

        /// <summary>Brightness of a body with nothing left, as a fraction of a sated one.</summary>
        public float Starving = 0.22f;

        /// <summary>Saturation cap for the rim, in HSL. See the class remarks.</summary>
        public float RimSaturation = 0.62f;

        /// <summary>Lightness cap for the rim, in HSL.</summary>
        public float RimLightness = 0.58f;

        /// <summary>Saturation cap for the body under the rim.</summary>
        public float BodySaturation = 0.45f;

        /// <summary>
        /// Lightness of the body under the rim.
        /// </summary>
        /// <remarks>
        /// Dark, because the rim is what is meant to be seen, but not as dark as the first cut's
        /// 0.10. A Fresnel rim is nearly nothing on a surface facing the camera square on, and the
        /// top view looks straight down at the flat top face of every body in the world: at 0.10
        /// the whole footprint read as black boxes and the guild could not be told from the
        /// picture at all. Legibility before prettiness, which is the second of the two rules
        /// these pictures are made under.
        /// </remarks>
        public float BodyLightness = 0.18f;

        /// <summary>
        /// The skin, or null for the plain look.
        /// </summary>
        /// <remarks>
        /// Set by <c>TheatreRunner</c> before the first paint. When it is null every body keeps
        /// the material and the meshes <c>PhenotypeBuilder</c> gave it and only the colours are
        /// set, which is what this class did before the skin existed and is what the headless
        /// checks that never build a runner still get.
        /// </remarks>
        public TheatreSkin Skin;

        private sealed class Body
        {
            public Transform Root;
            public MeshRenderer[] Renderers;
            public int[] Part;

            /// <summary>This body's carve seed, so no two bodies wear the same impressions.</summary>
            public float Seed;

            /// <summary>
            /// Per renderer, the two joint anchors nearest it, in that visual's own object units,
            /// with a reach in w, or a zero reach for none. See <see cref="Pinches"/>.
            /// </summary>
            public Vector4[] PinchA;
            public Vector4[] PinchB;

            /// <summary>
            /// Per renderer, what the visual's local scale has to be multiplied by to draw the
            /// genome's three half-extents rather than the collider's one radius. See
            /// <see cref="Aspect"/>. Never above one on any axis.
            /// </summary>
            public Vector3[] Shape;

            /// <summary>Per renderer, the local scale this class last wrote. See <see cref="Reshape"/>.</summary>
            public Vector3[] Wrote;

            /// <summary>One neck per jointed part, or null when this body has no joint.</summary>
            public Transform[] Necks;

            /// <summary>The part each neck's joint attaches. Parallel to <see cref="Necks"/>.</summary>
            public int[] NeckPart;
        }

        private readonly Dictionary<long, Body> _bodies = new Dictionary<long, Body>();
        /// <summary>
        /// Made on first paint, never in a field initializer — see the remarks below.
        /// </summary>
        /// <remarks>
        /// A <see cref="MaterialPropertyBlock"/> is an engine object, and Unity refuses to create
        /// one while it is deserializing a MonoBehaviour, which is exactly when that
        /// MonoBehaviour's field initializers run. <c>TheatreRunner</c> holds its palette as
        /// <c>private readonly TheatrePalette _palette = new TheatrePalette()</c>, so building the
        /// block here threw <c>CreateImpl is not allowed…</c> before <c>Start</c> ran, left
        /// <c>_palette</c> null, and killed the viewer on Play with a bare
        /// <c>NullReferenceException</c> two frames later. The headless checks never saw it: they
        /// build the world directly and never instantiate the component, so the interactive path
        /// went unexercised from D075 until someone first pressed Play (2026-09-10).
        /// </remarks>
        private MaterialPropertyBlock _block;
        private readonly List<MeshRenderer> _scratch = new List<MeshRenderer>();
        private readonly List<Transform> _necks = new List<Transform>();
        private readonly List<int> _neckPart = new List<int>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int ReserveId = Shader.PropertyToID("_Reserve");
        private static readonly int CarveId = Shader.PropertyToID("_Carve");
        private static readonly int PinchAId = Shader.PropertyToID("_PinchA");
        private static readonly int PinchBId = Shader.PropertyToID("_PinchB");

        public int Painted => _bodies.Count;

        /// <summary>
        /// The reserve tint, 0 (at the gate) to 1 (sated) — the same squash
        /// <c>CreatureSensors</c> puts on <see cref="SensorChannel.Energy"/>, so what a viewer
        /// sees and what a creature feels are the same number.
        /// </summary>
        /// <param name="secondsOfReserve"><c>Organism.SecondsOfReserve</c>; infinity reads 1.</param>
        /// <param name="fullScaleSeconds"><c>RunConfig.EnergyFullScaleSeconds</c>.</param>
        public static float Tint(float secondsOfReserve, float fullScaleSeconds)
        {
            if (float.IsPositiveInfinity(secondsOfReserve)) return 1f;
            if (!(secondsOfReserve > 0f)) return 0f;

            float scale = Mathf.Max(1e-6f, fullScaleSeconds);
            return Mathf.Clamp01((float)System.Math.Tanh(secondsOfReserve / scale));
        }

        /// <summary>Paints one body: cell type per part, brightness by reserve.</summary>
        /// <param name="id">The creature, so its renderer table can be cached.</param>
        /// <param name="root">The body's root, from <see cref="CreatureIdMap"/>.</param>
        /// <param name="phenotype">Its developed body, for the cell type of each part.</param>
        /// <param name="tint">0–1 from <see cref="Tint"/>.</param>
        /// <param name="on">False restores the plain look.</param>
        public void Paint(long id, Transform root, Phenotype phenotype, float tint, bool on)
        {
            if (root == null || phenotype == null) return;

            if (!_bodies.TryGetValue(id, out Body body) || body.Root != root)
            {
                body = Gather(id, root, phenotype);
                _bodies[id] = body;
            }

            float reserve = Mathf.Clamp01(tint);
            float brightness = on ? Mathf.Lerp(Starving, 1f, reserve) : 1f;

            if (_block == null) _block = new MaterialPropertyBlock();

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                MeshRenderer renderer = body.Renderers[i];
                if (renderer == null) continue;

                int part = body.Part[i];
                bool known = part >= 0 && part < phenotype.PartCount;

                Color guild = Color.white;
                if (on) guild = known ? ColourOf(phenotype.Parts[part].CellTypeId) : Structural;

                Reshape(body, i, renderer.transform);

                string cellType = known ? phenotype.Parts[part].CellTypeId : CellTypeIds.Structural;

                float seed = body.Seed + 0.61803399f * Mathf.Max(0, part);
                seed -= Mathf.Floor(seed);

                Vector4 carve = Character(cellType, seed);

                Set(renderer, guild, brightness, on ? reserve : 1f, on,
                    carve, body.PinchA[i], body.PinchB[i]);
            }

            RefreshNecks(body, phenotype, brightness, on ? reserve : 1f, on);
        }

        /// <summary>Writes one renderer's per-body properties into the shared block.</summary>
        /// <remarks>
        /// <c>_BaseColor</c> and <c>_Color</c> are both set: URP's Lit reads the first and the
        /// built-in Standard shader the second, and this class has to work whichever shader the
        /// skin resolved (or failed to). <c>_RimColor</c> and <c>_Reserve</c> exist only on the
        /// theatre's own shader and are ignored by the others, which is why the body colour still
        /// carries the reserve brightness rather than leaving it all to the glow.
        /// </remarks>
        private void Set(
            Renderer renderer, Color guild, float brightness, float reserve, bool on,
            Vector4 carve, Vector4 pinchA, Vector4 pinchB)
        {
            Color rim = on
                ? Muted(guild, RimSaturation, RimLightness)
                : Color.white;

            Color body = on
                ? Muted(guild, BodySaturation, BodyLightness)
                : Color.white;

            rim *= brightness;
            body *= brightness;
            rim.a = 1f;
            body.a = 1f;

            renderer.GetPropertyBlock(_block);

            _block.SetColor(BaseColorId, body);
            _block.SetColor(ColorId, body);
            _block.SetColor(RimColorId, rim);
            _block.SetFloat(ReserveId, reserve);
            _block.SetVector(CarveId, carve);
            _block.SetVector(PinchAId, pinchA);
            _block.SetVector(PinchBId, pinchB);

            renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// The guild's hue at a capped saturation and a fixed lightness.
        /// </summary>
        /// <remarks>
        /// Lightness is set rather than capped, because the point is a body dark enough for a rim
        /// to read against; saturation is capped rather than set, so a colour that was already
        /// muted stays muted instead of being pushed up to the cap.
        /// </remarks>
        public static Color Muted(Color colour, float saturationCap, float lightness)
        {
            Color.RGBToHSV(colour, out float h, out float s, out float v);

            // Unity gives HSV, and HSL's lightness is what the reading asks for. The two agree on
            // hue, and for the fully saturated end of a guild colour the conversion below is the
            // standard one: L = V (1 - S/2), and the saturation that keeps that lightness.
            float l = v * (1f - 0.5f * s);
            float sl = l <= 0f || l >= 1f ? 0f : (v - l) / Mathf.Min(l, 1f - l);

            sl = Mathf.Min(sl, saturationCap);
            l = Mathf.Clamp01(lightness);

            float vOut = l + sl * Mathf.Min(l, 1f - l);
            float sOut = vOut <= 0f ? 0f : 2f * (1f - l / vOut);

            return Color.HSVToRGB(h, Mathf.Clamp01(sOut), Mathf.Clamp01(vOut));
        }

        /// <summary>
        /// One creature's carve seed, in [0, 1).
        /// </summary>
        /// <remarks>
        /// Mixed rather than taken raw, and fractional rather than large, and both for the same
        /// reason: the shader turns this into a lattice offset with a <c>frac</c>, and a float
        /// carrying four digits before the point has only three left after it. Consecutive ids
        /// would then land on the same offset and a cohort of siblings would wear one carving.
        /// The multiplier is Knuth's, the odd integer nearest 2^32 over the golden ratio.
        /// </remarks>
        private static float SeedOf(long id)
        {
            long mixed = unchecked(id * 2654435761L);

            return ((mixed >> 13) & 0xFFFF) / 65536f;
        }

        private Body Gather(long id, Transform root, Phenotype phenotype)
        {
            _scratch.Clear();
            root.GetComponentsInChildren<MeshRenderer>(true, _scratch);

            var body = new Body
            {
                Root = root,
                Renderers = _scratch.ToArray(),
                Part = new int[_scratch.Count],
                Seed = SeedOf(id),
                PinchA = new Vector4[_scratch.Count],
                PinchB = new Vector4[_scratch.Count],
                Shape = new Vector3[_scratch.Count],
                Wrote = new Vector3[_scratch.Count],
            };

            for (int i = 0; i < _scratch.Count; i++)
            {
                body.Part[i] = PartIndexOf(_scratch[i].transform.parent);
                body.Shape[i] = Vector3.one;
            }

            Dress(body, phenotype);

            return body;
        }

        /// <summary>
        /// The carve's character for one part: its seed, how lobed it is, how wrinkled, and how
        /// much of the dial it takes at all.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Lightly by guild, as the shape asks.</b> A producer is more lobed, because a leaf
        /// is a thing with a shape rather than a thing with a texture; a stomach is more
        /// wrinkled; a structural part is smoother, because it is the strut and not the organ.
        /// The differences are small and none of them is what tells a viewer which guild a part
        /// is: that is the rim's colour, and giving the shape a second vote would make a badly
        /// lit producer read as a stomach.
        /// </para>
        /// <para>
        /// <b>The seed carries the part index as well as the body's id.</b> The field is read in
        /// each part's own object units, so two parts of one body with the same size and one seed
        /// would come out as two copies of one carving. An irrational step, wrapped back into
        /// [0, 1), keeps them apart while leaving the whole body drawn from one creature's
        /// number; the golden ratio is the step that fills the interval most evenly for any
        /// number of parts.
        /// </para>
        /// </remarks>
        private static Vector4 Character(string cellTypeId, float seed)
        {
            if (cellTypeId == CellTypeIds.Photosynthetic)
                return new Vector4(seed, 1.00f, 0.42f, 1.05f);

            if (cellTypeId == CellTypeIds.Absorptive)
                return new Vector4(seed, 0.60f, 0.78f, 1.00f);

            return new Vector4(seed, 0.85f, 0.32f, 0.62f);
        }

        /// <summary>
        /// Puts the skin on a body the first time it is seen: the theatre's material on every
        /// renderer, the rounded mesh in place of the primitive, and a neck at every joint.
        /// </summary>
        /// <remarks>
        /// <b>The mesh is swapped, the transform is not.</b> <c>PhenotypeBuilder.VisualPlan</c>
        /// sizes each visual by its local scale and <c>ResizeColliderAndVisual</c> rewrites that
        /// scale every time a body grows; the generated meshes are unit-sized on exactly the
        /// conventions the engine's primitives use (a cube of side one, a sphere of diameter one,
        /// a cylinder of diameter one and height two), so replacing <c>sharedMesh</c> and touching
        /// nothing else means growth keeps working with no second copy of the arithmetic. That is
        /// also why the swap is by mesh name and not by part shape: the theatre must not build a
        /// second opinion about which primitive a shape draws with.
        /// </remarks>
        private void Dress(Body body, Phenotype phenotype)
        {
            if (Skin == null || !Skin.Ready) return;

            Material material = Skin.BodyMaterial;

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                MeshRenderer renderer = body.Renderers[i];
                if (renderer == null) continue;

                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null) continue;

                Mesh mesh = filter.sharedMesh;
                Mesh rounded = TheatreMeshes.RoundedFor(mesh);

                if (rounded != null) { filter.sharedMesh = rounded; mesh = rounded; }

                // Which solid this visual draws, by reference and not by name, so that dressing a
                // body twice reads the same answer the second time: after the swap the mesh is
                // one of the three this theatre generated, and RoundedFor would no longer
                // recognise it by the engine's name for the primitive it replaced.
                bool isSphere = mesh == TheatreMeshes.Sphere();
                bool isCylinder = mesh == TheatreMeshes.Cylinder();

                int part = body.Part[i];
                if (part < 0 || part >= phenotype.PartCount) continue;

                body.Shape[i] = Aspect(phenotype.Parts[part], isSphere, isCylinder);

                // Squashed before the anchors are measured, not after: Pinches divides by the
                // visual's scale to reach object units, and the scale it has to divide by is the
                // one the mesh is finally drawn at.
                Reshape(body, i, renderer.transform);

                Pinches(body, i, phenotype, part, renderer.transform);
            }

            BuildNecks(body, phenotype);
        }

        // ---------------------------------------------------------------- shape

        /// <summary>
        /// What a visual's local scale has to be multiplied by to draw the genome's three
        /// half-extents rather than the one radius the simulation reduced them to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This shows the genome's intent and not the body's physics, and the collider is the
        /// sphere.</b> A node carries three half-extents whatever shape it takes, and
        /// <c>SphereShape.Radius</c> is their mean, so a genome that has evolved a part twice as
        /// long as it is wide collides, displaces and drags as a ball of the average size and the
        /// two other numbers are silent in the world. Drawing the ball back as a ball hides a
        /// trait the genome is carrying; drawing the three half-extents shows what the genome
        /// says while the collider goes on being the sphere. A viewer reading a swimming stroke
        /// off an ellipsoid's long axis is reading something the physics does not have.
        /// <c>CapsuleShape.Radius</c> is the mean of the X and Z half-extents in the same way, so
        /// a capsule's cross-section is drawn elliptical on the same argument and its length,
        /// which the shape does use, is left alone.
        /// </para>
        /// <para>
        /// <b>Why it is a multiplier and not a size.</b> <c>PhenotypeBuilder.VisualPlan</c> owns
        /// the arithmetic that turns half-extents into a visual's scale, and
        /// <c>ResizeColliderAndVisual</c> rewrites it every time a body grows. A second copy of
        /// that arithmetic here would drift from it silently, which is the fault that file spends
        /// its remarks warning about. So this class never computes a size: it takes whatever
        /// scale the simulation last wrote and squashes it.
        /// </para>
        /// <para>
        /// <b>The size bound.</b> Every component is the half-extent divided by the largest of
        /// them, so the longest semi-axis keeps the collider's radius exactly and the two others
        /// come in. An ellipsoid whose longest semi-axis is the sphere's radius is inside the
        /// sphere; for a capsule the same argument holds against the swept sphere, since a cap
        /// squashed on X and Z and left alone on Y is inside the hemisphere it replaces. So this
        /// under-reports and never over-reports, which is the direction the two constraints
        /// allow.
        /// </para>
        /// </remarks>
        private static Vector3 Aspect(PhenotypePart part, bool isSphere, bool isCylinder)
        {
            if (!isSphere && !isCylinder) return Vector3.one;

            Vector3 h = Abs(part.HalfExtents.ToVector3());

            if (part.ShapeId == ShapeIds.Sphere && isSphere)
            {
                float longest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
                if (longest <= 0f) return Vector3.one;

                return new Vector3(h.x / longest, h.y / longest, h.z / longest);
            }

            if (part.ShapeId == ShapeIds.Capsule)
            {
                float longest = Mathf.Max(h.x, h.z);
                if (longest <= 0f) return Vector3.one;

                // Y is left at one on both the shaft and the caps: the shaft's Y is the span the
                // shape really has, and a cap squashed on Y would pull the end of the drawn body
                // in from the end of the capsule collider for no reason.
                return new Vector3(h.x / longest, 1f, h.z / longest);
            }

            return Vector3.one;
        }

        /// <summary>
        /// Applies <see cref="Aspect"/> to whatever local scale the simulation last wrote.
        /// </summary>
        /// <remarks>
        /// Checked against what this class wrote rather than applied every paint, because the
        /// multiplier would compound: a body squashed to nine tenths on every frame of a run is
        /// gone inside a minute. An exact float comparison is the right one here and not a
        /// sloppy one, because an untouched transform holds the identical bits and a growth
        /// resize writes a different number however small the step
        /// (<c>PhenotypeBuilder.ResizeColliderAndVisual</c>).
        /// </remarks>
        private static void Reshape(Body body, int i, Transform visual)
        {
            if (visual == null) return;

            Vector3 current = visual.localScale;
            Vector3 wrote = body.Wrote[i];

            if (current.x == wrote.x && current.y == wrote.y && current.z == wrote.z) return;

            Vector3 aspect = body.Shape[i];
            var next = new Vector3(
                current.x * aspect.x, current.y * aspect.y, current.z * aspect.z);

            visual.localScale = next;
            body.Wrote[i] = next;
        }

        /// <summary>
        /// The joint anchors that touch one visual, in that visual's own object units.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why the shader needs them.</b> Two parts meet by one solid entering another, and
        /// nothing on a surface hides that. The carve deepens near an anchor on both sides of a
        /// junction, so the tissue draws in where the hinge is and the join reads as a waist; it
        /// also widens the gap the neck is drawn in (<see cref="BuildNecks"/>).
        /// </para>
        /// <para>
        /// <b>Two of them.</b> A part is touched by its own attachment to its parent and by every
        /// child hanging off it, and a fixed pair of shader properties is the arrangement that
        /// does not need an array through a property block. The two nearest the visual are the
        /// two that would be seen; a third anchor on the far side of a long part is carved by
        /// whichever of its own pair it is nearest.
        /// </para>
        /// <para>
        /// <b>In object units, and computed once.</b> The anchors are metres in the part's frame,
        /// so they are moved into the visual's frame by its offset and divided by its scale,
        /// which is what makes them mean the same thing to a mesh built as a unit solid. Growth
        /// rebuilds a phenotype at a new scale rather than reshaping it
        /// (<c>Phenotype.Scaled</c>), so a point given as a fraction of a part's own size stays
        /// where it was and this does not have to be redone as a body grows.
        /// </para>
        /// </remarks>
        private static void Pinches(
            Body body, int i, Phenotype phenotype, int part, Transform visual)
        {
            if (visual == null) return;

            // How far a pinch reaches, in object units, where a part's own size is one. Under
            // half, so it is a waist at the joint and not a general thinning of the part.
            const float reach = 0.45f;

            Vector3 offset = visual.localPosition;
            Vector3 scale = visual.localScale;

            if (Mathf.Abs(scale.x) < 1e-6f || Mathf.Abs(scale.y) < 1e-6f ||
                Mathf.Abs(scale.z) < 1e-6f)
            {
                return;
            }

            var best = new Vector4(0f, 0f, 0f, 0f);
            var second = new Vector4(0f, 0f, 0f, 0f);
            float bestAway = float.MaxValue;
            float secondAway = float.MaxValue;

            void Consider(Vector3 anchorPartLocal)
            {
                Vector3 local = anchorPartLocal - offset;
                var here = new Vector3(local.x / scale.x, local.y / scale.y, local.z / scale.z);

                float away = here.magnitude;
                var point = new Vector4(here.x, here.y, here.z, reach);

                if (away < bestAway)
                {
                    secondAway = bestAway; second = best;
                    bestAway = away; best = point;
                }
                else if (away < secondAway)
                {
                    secondAway = away; second = point;
                }
            }

            PhenotypePart mine = phenotype.Parts[part];

            if (mine.ParentIndex >= 0) Consider(mine.ChildAnchorLocal.ToVector3());

            for (int c = 0; c < phenotype.PartCount; c++)
            {
                if (phenotype.Parts[c].ParentIndex != part) continue;

                Consider(phenotype.Parts[c].ParentAnchorLocal.ToVector3());
            }

            body.PinchA[i] = best;
            body.PinchB[i] = second;
        }

        // ---------------------------------------------------------------- joints

        /// <summary>
        /// A short neck at every joint, so that a jointed body can be told from a rigid one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Presentation only.</b> It is a renderer with no collider and no rigid body, drawn by
        /// the theatre from the phenotype the run already carries; nothing about the simulation
        /// changes and nothing under <c>Assets/Evosim</c> is touched.
        /// </para>
        /// <para>
        /// <b>Where it goes, and why it stays inside.</b> The neck lies on the axis from the
        /// parent part's centre out through the joint anchor, and its outer face stops exactly at
        /// the anchor. The anchor is on the parent part's half-extent box, so the whole neck is
        /// inside that box: <see cref="Fit"/> shrinks the radius until the cylinder's own footprint
        /// fits on the two axes across the neck, and clips the length so its inner end cannot pass
        /// the far face. For a box part that box is the collider exactly; for a sphere or a capsule
        /// the collider is inscribed in it, and the box is the extent the rest of the theatre
        /// already measures a body by (<c>SnapshotCamera.Radius</c>).
        /// </para>
        /// <para>
        /// <b>What makes it visible.</b> The bodies are drawn inset and with rounded edges
        /// (<see cref="TheatreMeshes"/>), so at a joint the two drawn surfaces pull away from the
        /// contact and leave a gap the neck shows through. It is a narrow gap on a small part, and
        /// it is the honest one: a neck drawn long enough to be unmissable would be a visual
        /// outside its collider, which is the one thing these pictures must not do.
        /// </para>
        /// </remarks>
        private void BuildNecks(Body body, Phenotype phenotype)
        {
            _necks.Clear();
            _neckPart.Clear();

            Mesh mesh = TheatreMeshes.Cylinder();
            Material material = Skin != null ? Skin.NeckMaterial : null;
            if (mesh == null || material == null) return;

            var partTransform = new Transform[phenotype.PartCount];

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                int part = body.Part[i];
                if (part < 0 || part >= partTransform.Length) continue;
                if (partTransform[part] == null && body.Renderers[i] != null)
                {
                    partTransform[part] = body.Renderers[i].transform.parent;
                }
            }

            for (int p = 0; p < phenotype.PartCount; p++)
            {
                PhenotypePart part = phenotype.Parts[p];

                if (part.ParentIndex < 0 || part.JointType == JointType.Fixed) continue;

                Transform host = partTransform[part.ParentIndex];
                if (host == null) continue;

                var go = new GameObject("Neck")
                {
                    layer = host.gameObject.layer,
                };

                go.transform.SetParent(host, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;

                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _necks.Add(go.transform);
                _neckPart.Add(p);
            }

            if (_necks.Count == 0) return;

            body.Necks = _necks.ToArray();
            body.NeckPart = _neckPart.ToArray();
        }

        /// <summary>
        /// Sizes and colours every neck from the phenotype as it is now.
        /// </summary>
        /// <remarks>
        /// Recomputed on each paint rather than once, because a body grows: D087 rebuilds a
        /// creature's phenotype at its new size and <c>ResizeColliderAndVisual</c> moves the
        /// visuals to match, so a neck placed once would keep a newborn's dimensions on an adult
        /// and would end up outside the part it is supposed to sit in. Three transform writes on
        /// a jointed part, inside a repaint budget that is already capped per frame.
        /// </remarks>
        private void RefreshNecks(Body body, Phenotype phenotype, float brightness, float reserve, bool on)
        {
            if (body.Necks == null) return;

            for (int i = 0; i < body.Necks.Length; i++)
            {
                Transform neck = body.Necks[i];
                if (neck == null) continue;

                int p = body.NeckPart[i];

                if (p < 0 || p >= phenotype.PartCount)
                {
                    neck.gameObject.SetActive(false);
                    continue;
                }

                PhenotypePart part = phenotype.Parts[p];
                PhenotypePart parent = phenotype.Parts[part.ParentIndex];

                bool fitted = Fit(part, parent, out Vector3 centre, out Quaternion rotation, out Vector3 scale);

                neck.gameObject.SetActive(fitted && on);
                if (!fitted || !on) continue;

                neck.localPosition = centre;
                neck.localRotation = rotation;
                neck.localScale = scale;

                var renderer = neck.GetComponent<MeshRenderer>();

                // A neck takes no carve: its character's fourth component is zero, so whatever
                // the dial says the depth comes out zero. It is a marker, and a marker with
                // impressions in it is a marker that lies about being tissue.
                if (renderer != null)
                {
                    Set(renderer, Jointed, brightness, reserve, true,
                        new Vector4(0f, 1f, 0f, 0f), Vector4.zero, Vector4.zero);
                }
            }
        }

        /// <summary>
        /// Where one neck sits in its parent part's local frame, and how big it is allowed to be.
        /// </summary>
        /// <remarks>
        /// The bound is enforced here and nowhere else. The cylinder mesh is a unit solid of
        /// diameter one and height two already inset by <see cref="TheatreMeshes.Inset"/>, so a
        /// local scale of (2r, L/2, 2r) makes a cylinder of radius at most r and length at most L.
        /// The radius is then the smallest clearance from the anchor to the box's faces on the two
        /// axes across the neck, capped so a neck is never wider than the part it joins, and the
        /// length is clipped so the inner end cannot pass the far face. Returns false when nothing
        /// visible fits, in which case the neck is hidden rather than drawn at a size that lies.
        /// </remarks>
        private static bool Fit(
            PhenotypePart part, PhenotypePart parent,
            out Vector3 centre, out Quaternion rotation, out Vector3 scale)
        {
            centre = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;

            Vector3 h = Abs(parent.HalfExtents.ToVector3());
            Vector3 anchor = part.ParentAnchorLocal.ToVector3();

            if (h.x <= 0f || h.y <= 0f || h.z <= 0f) return false;

            // Which face the joint sits on: the axis the anchor reaches furthest along, measured
            // against that axis's own half extent so a long thin part is not read as an end-on
            // joint just because the box is long.
            Vector3 towards = Direction(part, parent, anchor);
            int axis = 0;
            float best = -1f;

            for (int j = 0; j < 3; j++)
            {
                float reach = Mathf.Abs(towards[j]) / h[j];
                if (reach > best) { best = reach; axis = j; }
            }

            float sign = towards[axis] >= 0f ? 1f : -1f;

            int a = (axis + 1) % 3;
            int b = (axis + 2) % 3;

            float radius = Mathf.Min(h[a] - Mathf.Abs(anchor[a]), h[b] - Mathf.Abs(anchor[b]));

            // Never wider than the part it joins, and never more than a third of the parent
            // across: a collar wider than the limb it holds reads as a part rather than a joint.
            Vector3 childHalf = Abs(part.HalfExtents.ToVector3());

            radius = Mathf.Min(radius, 0.34f * Mathf.Min(h[a], h[b]));
            radius = Mathf.Min(radius, 0.9f * Mathf.Min(childHalf.x, Mathf.Min(childHalf.y, childHalf.z)));

            if (radius <= 1e-4f) return false;

            // Short: three radii, or as much of the box as is left in front of the far face.
            float outer = anchor[axis];
            float room = sign > 0f ? outer + h[axis] : h[axis] - outer;
            float length = Mathf.Min(3f * radius, room);

            if (length <= 1e-4f) return false;

            Vector3 up = Vector3.zero;
            up[axis] = sign;

            Vector3 tip = anchor;
            centre = tip - up * (0.5f * length);
            rotation = Quaternion.FromToRotation(Vector3.up, up);
            scale = new Vector3(2f * radius, 0.5f * length, 2f * radius);

            return true;
        }

        /// <summary>
        /// The direction from the parent's centre to the child's, in the parent's local frame.
        /// </summary>
        /// <remarks>
        /// The anchor alone would do for a joint on a face, and it is used when it is not
        /// degenerate; a joint whose anchor sits at the parent's centre has no face to point at,
        /// and there the child's own position is what says which way the limb leaves.
        /// </remarks>
        private static Vector3 Direction(PhenotypePart part, PhenotypePart parent, Vector3 anchor)
        {
            if (anchor.sqrMagnitude > 1e-8f) return anchor;

            Quaternion parentRotation = parent.Rotation.ToQuaternion();
            Vector3 offset = part.Position.ToVector3() - parent.Position.ToVector3();
            Vector3 local = Quaternion.Inverse(parentRotation) * offset;

            return local.sqrMagnitude > 1e-8f ? local : Vector3.up;
        }

        private static Vector3 Abs(Vector3 v) =>
            new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>
        /// The part index in <c>PhenotypeBuilder</c>'s name for a part — <c>Part07_n2</c> — or -1.
        /// </summary>
        private static int PartIndexOf(Transform partTransform)
        {
            if (partTransform == null) return -1;

            string name = partTransform.name;
            if (!name.StartsWith("Part", System.StringComparison.Ordinal)) return -1;

            int end = name.IndexOf('_');
            if (end < 0) end = name.Length;

            return int.TryParse(
                name.Substring(4, end - 4),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out int index)
                ? index
                : -1;
        }

        public Color ColourOf(string cellTypeId)
        {
            if (cellTypeId == CellTypeIds.Photosynthetic) return Photosynthetic;
            if (cellTypeId == CellTypeIds.Absorptive) return Absorptive;
            return Structural;
        }

        /// <summary>Drops cached bodies whose root Unity has destroyed.</summary>
        /// <remarks>
        /// Called on a cadence rather than per death: the cache is one small array per creature,
        /// and a dead entry costs a dictionary slot until it is swept.
        /// </remarks>
        public void PurgeDead()
        {
            var gone = new List<long>();

            foreach (KeyValuePair<long, Body> entry in _bodies)
            {
                if (entry.Value.Root == null) gone.Add(entry.Key);
            }

            for (int i = 0; i < gone.Count; i++) _bodies.Remove(gone[i]);
        }

        public void Clear() => _bodies.Clear();
    }
}

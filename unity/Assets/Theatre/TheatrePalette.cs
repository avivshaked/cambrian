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

        /// <summary>
        /// Brightness of a body with nothing left, as a fraction of a sated one. Raised from
        /// 0.22 to 0.45 on 2026-09-16 (the look's one-day pass): at 0.22 a starving world fell
        /// off the bottom of the display range, so the reserve now also carries saturation
        /// (<see cref="StarvingSaturation"/>) and a starving body reads as pale, not as absent.
        /// </summary>
        public float Starving = 0.45f;

        /// <summary>Saturation of a body with nothing left, as a fraction of a sated one's.</summary>
        public float StarvingSaturation = 0.35f;

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

        /// <summary>
        /// Draw every body as the physics has it: the engine's primitives, no rounding, no
        /// carve, taper or bend. The key X in the theatre (the owner's ask of 2026-09-13, built
        /// 2026-09-16): a viewer who doubts a picture can see the collider under it. Bodies are
        /// dressed again on the next paint after it changes (<see cref="Clear"/>).
        /// </summary>
        public bool RawShapes;

        private sealed class Body
        {
            public Transform Root;
            public MeshRenderer[] Renderers;
            public int[] Part;

            /// <summary>This body's carve seed, so no two bodies wear the same impressions.</summary>
            public float Seed;

            /// <summary>
            /// This creature's own seed, from its id and not its plan. <see cref="Seed"/> comes from
            /// the body plan (the skin genes), so every blade of a clade that shares a plan would be
            /// drawn with one outline; this one perturbs a blade's outline, curl and tone a little,
            /// so siblings are similar and never identical (the owner, 2026-09-24: "something that
            /// doesn't look exactly the same for every leaf, just similar").
            /// </summary>
            public float Own;

            /// <summary>
            /// Per renderer, the two joint anchors nearest it, in that visual's own object units,
            /// with a reach in w, or a zero reach for none. See <see cref="Pinches"/>.
            /// </summary>
            public Vector4[] PinchA;
            public Vector4[] PinchB;

            /// <summary>
            /// Per renderer, what the visual's local scale has to be multiplied by to draw the
            /// genome's three half-extents rather than the collider's one radius. See
            /// <see cref="Aspect"/>. Never above one on any axis, except the stretch along the
            /// shaft of a merged cap (<see cref="Merged"/>), which stays inside the capsule.
            /// </summary>
            public Vector3[] Shape;

            /// <summary>Per renderer, the local scale this class last wrote. See <see cref="Reshape"/>.</summary>
            public Vector3[] Wrote;

            /// <summary>
            /// Per renderer, true for the one cap that draws a near-spherical capsule alone, at
            /// the part's centre; its shaft and other cap are switched off. See <see cref="Dress"/>.
            /// </summary>
            public bool[] Merged;

            /// <summary>
            /// Per renderer, the part's own joint anchor in that visual's object units, w one when
            /// it hangs from a parent: a leaf's base is drawn at that end (<c>ShapeLamina</c>).
            /// </summary>
            public Vector4[] Leaf;

            /// <summary>One neck per jointed part, or null when this body has no joint.</summary>
            public Transform[] Necks;

            /// <summary>The part each neck's joint attaches. Parallel to <see cref="Necks"/>.</summary>
            public int[] NeckPart;

            /// <summary>
            /// True where the knuckle sits in the child part's frame, false in the parent's.
            /// Parallel to <see cref="Necks"/>.
            /// </summary>
            public bool[] NeckChildSide;
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
        private readonly List<bool> _neckSide = new List<bool>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int ReserveId = Shader.PropertyToID("_Reserve");
        private static readonly int TransTintId = Shader.PropertyToID("_TransTint");
        private static readonly int TransGainId = Shader.PropertyToID("_TransGain");
        private static readonly int SheenId = Shader.PropertyToID("_Sheen");
        private static readonly int CarveId = Shader.PropertyToID("_Carve");
        private static readonly int IndividualId = Shader.PropertyToID("_Individual");
        private static readonly int PinchAId = Shader.PropertyToID("_PinchA");
        private static readonly int PinchBId = Shader.PropertyToID("_PinchB");
        private static readonly int LeafId = Shader.PropertyToID("_Leaf");

        public int Painted => _bodies.Count;

        /// <summary>
        /// The safari's colony tint (its call-outs, <c>EVOSIM_THEATRE_SAFARI_CALLOUTS=1</c>): when
        /// set, a body it answers false for is painted grey and darker, so the subject clade stands
        /// in full colour among the rest. Null paints every body as it always was.
        /// </summary>
        public System.Func<long, bool> InFocus;

        /// <summary>How much of a body's brightness a body out of focus keeps.</summary>
        public float OutOfFocusBrightness = 0.5f;

        /// <summary>
        /// A body's lineage, for its hue (item 4 of the owner's leaf ruling, 2026-09-24): the
        /// safari sets it to the clade its guide names, so a clade reads as one kind of plant
        /// among others of its guild. -1, or null, paints the guild's own colour.
        /// </summary>
        public System.Func<long, long> LineageOf;

        /// <summary>
        /// How far a lineage's hue turns from its guild's, either way, as a fraction of the
        /// colour wheel (<c>EVOSIM_THEATRE_LINEAGE_HUE</c>, 0 to 0.2, default 0.06: about twenty
        /// degrees, so a leaf is still green and a stomach still amber).
        /// </summary>
        public float LineageHue = TheatreSkin.Dial("EVOSIM_THEATRE_LINEAGE_HUE", 0.06f, 0f, 0.2f);

        /// <summary>A lineage's turn of the hue, from its key: the same key, the same turn.</summary>
        private float TurnOf(long id)
        {
            if (LineageOf == null || LineageHue <= 0f) return 0f;

            long key = LineageOf(id);
            if (key < 0) return 0f;

            ulong h = unchecked((ulong)key * 0x9E3779B97F4A7C15UL);
            h ^= h >> 29;
            h = unchecked(h * 0xBF58476D1CE4E5B9UL);
            h ^= h >> 32;

            float signed = (h & 0xFFFFF) / (float)0x80000 - 1f;
            return LineageHue * signed;
        }

        /// <summary>A colour with its hue turned and its saturation nudged the same way.</summary>
        public static Color Turned(Color c, float turn)
        {
            if (turn == 0f) return c;

            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + turn, 1f);
            s = Mathf.Clamp01(s * (1f + 1.5f * turn));

            Color turned = Color.HSVToRGB(h, s, v);
            turned.a = c.a;
            return turned;
        }

        /// <summary>A colour's grey of the same luminance.</summary>
        public static Color Grey(Color c)
        {
            float y = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return new Color(y, y, y, c.a);
        }

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
            bool dim = on && InFocus != null && !InFocus(id);
            if (dim) brightness *= OutOfFocusBrightness;

            float turn = on ? TurnOf(id) : 0f;

            if (_block == null) _block = new MaterialPropertyBlock();

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                MeshRenderer renderer = body.Renderers[i];
                if (renderer == null) continue;

                int part = body.Part[i];
                bool known = part >= 0 && part < phenotype.PartCount;

                Color guild = Color.white;
                if (on) guild = known ? ColourOf(phenotype.Parts[part].CellTypeId) : Structural;
                if (on) guild = Turned(guild, turn);
                if (dim) guild = Grey(guild);

                Reshape(body, i, renderer.transform);

                string cellType = known ? phenotype.Parts[part].CellTypeId : CellTypeIds.Structural;

                float seed = body.Seed + 0.61803399f * Mathf.Max(0, part);
                seed -= Mathf.Floor(seed);

                Vector4 carve = Character(cellType, seed);

                float own = OwnOf(body, part);

                Set(renderer, guild, brightness, on ? reserve : 1f, on,
                    carve, body.PinchA[i], body.PinchB[i], on ? TransmissionOf(cellType) : 0.4f,
                    on ? SheenOf(cellType) : 0.1f, body.Leaf[i], own);
            }

            RefreshNecks(body, phenotype, brightness, on ? reserve : 1f, on, dim, turn);
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
            Vector4 carve, Vector4 pinchA, Vector4 pinchB, float transmission, float sheen = 0.1f,
            Vector4 leaf = default, float individual = 0f)
        {
            Color rim = on
                ? Muted(guild, RimSaturation, RimLightness)
                : Color.white;

            // The light that comes through the tissue is the guild's hue, a little lighter than
            // the rim so that a backlit leaf reads as lit rather than as edged.
            Color through = on
                ? Muted(guild, RimSaturation, Mathf.Min(0.6f, RimLightness + 0.12f))
                : Color.white;

            Color body = on
                ? Muted(guild, BodySaturation * Mathf.Lerp(StarvingSaturation, 1f, reserve), BodyLightness)
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
            _block.SetColor(TransTintId, through);
            _block.SetFloat(TransGainId, transmission);
            _block.SetFloat(SheenId, sheen);
            _block.SetVector(CarveId, carve);
            _block.SetVector(PinchAId, pinchA);
            _block.SetVector(PinchBId, pinchB);
            _block.SetVector(LeafId, leaf);
            _block.SetFloat(IndividualId, individual);

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
        /// <summary>A part's own seed: the creature's, turned by the part's index.</summary>
        private static float OwnOf(Body body, int part)
        {
            float own = body.Own + 0.38196601f * Mathf.Max(0, part);
            return own - Mathf.Floor(own);
        }

        private static float SeedOf(long id)
        {
            long mixed = unchecked(id * 2654435761L);

            return ((mixed >> 13) & 0xFFFF) / 65536f;
        }

        /// <summary>
        /// The skin genes, theatre-only (the owner's ruling of 2026-09-16, the first of the two
        /// versions): the seed every per-body choice is drawn from (the carve's offsets, which
        /// end tapers, which way a part leans, TheatrePalette.Character and TheatreBody.shader's
        /// ShapeBox) comes from the body plan rather than from the creature's id, so a child
        /// that inherits its parent's plan wears its parent's skin and a cousin two mutations
        /// away wears a cousin's. Hashed from what a plan is: the part count, and each part's
        /// guild, parent, shape, mirroring and its shape's proportions in coarse steps, so a
        /// growth or a small size mutation keeps the family look and a new part or a new guild
        /// changes it. Nothing here reaches the simulation; the genome's own version, a few
        /// neutral numbers the shader reads, is queued for a round boundary.
        /// </summary>
        /// <summary>A string's hash that is the same in every process, which string.GetHashCode is not.</summary>
        private static long Stable(string text)
        {
            unchecked
            {
                long h = (long)14695981039346656037UL;
                if (text != null) foreach (char c in text) h = (h ^ c) * 1099511628211L;
                return h;
            }
        }

        private static float SeedOf(Phenotype phenotype, long id)
        {
            if (phenotype == null || phenotype.PartCount == 0) return SeedOf(id);

            unchecked
            {
                long h = 1469598103934665603L ^ phenotype.PartCount;

                for (int i = 0; i < phenotype.PartCount; i++)
                {
                    PhenotypePart part = phenotype.Parts[i];
                    Float3 he = part.HalfExtents;

                    float longest = Mathf.Max(he.X, Mathf.Max(he.Y, he.Z));
                    float shortest = Mathf.Max(1e-4f, Mathf.Min(he.X, Mathf.Min(he.Y, he.Z)));
                    int proportion = Mathf.RoundToInt(2f * Mathf.Log(longest / shortest, 2f));

                    h = (h ^ part.ParentIndex) * 1099511628211L;
                    h = (h ^ Stable(part.CellTypeId)) * 1099511628211L;
                    h = (h ^ Stable(part.ShapeId)) * 1099511628211L;
                    h = (h ^ (part.Mirrored ? 1 : 0)) * 1099511628211L;
                    h = (h ^ proportion) * 1099511628211L;
                }

                return ((h >> 17) & 0xFFFF) / 65536f;
            }
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
                Seed = SeedOf(phenotype, id),
                Own = SeedOf(id),
                PinchA = new Vector4[_scratch.Count],
                PinchB = new Vector4[_scratch.Count],
                Leaf = new Vector4[_scratch.Count],
                Shape = new Vector3[_scratch.Count],
                Wrote = new Vector3[_scratch.Count],
                Merged = new bool[_scratch.Count],
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
        /// <summary>
        /// How much light a guild's tissue lets through when backlit (the design pass's R2,
        /// 2026-09-16): a producer is a leaf and glows, an eater is a stomach wall and does not,
        /// a strut is between. The one place the guild reaches a body's face, and it reaches it
        /// as light, not as paint.
        /// </summary>
        /// <summary>
        /// The wet sheen by guild: a gut wall glossy, a leaf matte, a strut between. The
        /// absorptive tissue's own surface (the owner, 2026-09-16), a highlight and never a
        /// drawn organ.
        /// </summary>
        private static float SheenOf(string cellTypeId)
        {
            if (cellTypeId == CellTypeIds.Absorptive) return 0.35f;
            if (cellTypeId == CellTypeIds.Photosynthetic) return 0.04f;
            return 0.08f;
        }

        private static float TransmissionOf(string cellTypeId)
        {
            if (cellTypeId == CellTypeIds.Photosynthetic) return 1.0f;
            if (cellTypeId == CellTypeIds.Absorptive) return 0.12f;
            return 0.4f;
        }

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
            _capTaken.Clear();

            for (int i = 0; i < body.Renderers.Length; i++)
            {
                MeshRenderer renderer = body.Renderers[i];
                if (renderer == null) continue;

                // Dressed again after the raw shapes were shown: whatever a merge switched off
                // is switched on before this pass decides afresh.
                renderer.enabled = true;
                body.Merged[i] = false;

                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null) continue;

                Mesh mesh = filter.sharedMesh;

                if (RawShapes)
                {
                    Mesh primitive = TheatreMeshes.PrimitiveFor(mesh);
                    if (primitive != null) { filter.sharedMesh = primitive; mesh = primitive; }
                }
                else
                {
                    // How cubic the part is, from the visual's own scale: the plan sizes a unit
                    // mesh by its local scale, so the three components are the part's sides.
                    Vector3 sides = renderer.transform.localScale;
                    float smallest = Mathf.Min(Mathf.Abs(sides.x), Mathf.Min(Mathf.Abs(sides.y), Mathf.Abs(sides.z)));
                    float largest = Mathf.Max(Mathf.Abs(sides.x), Mathf.Max(Mathf.Abs(sides.y), Mathf.Abs(sides.z)));
                    float cubicness = largest > 1e-6f ? smallest / largest : 1f;

                    // A leaf (TheatreMeshes.Lamina) is a box that is flat or photosynthetic
                    // (DrawnAsLeaf). Asked of the genome's part and the visual's mesh together: the
                    // engine's cube stands for a box only.
                    int leafPart = body.Part[i];
                    bool leaf = leafPart >= 0 && leafPart < phenotype.PartCount &&
                                (TheatreMeshes.IsPrimitiveCube(mesh) || TheatreMeshes.IsRoundedCube(mesh) ||
                                 TheatreMeshes.IsLamina(mesh)) &&
                                DrawnAsLeaf(phenotype.Parts[leafPart], sides);

                    Mesh rounded = leaf ? TheatreMeshes.Lamina() : TheatreMeshes.RoundedFor(mesh, cubicness);
                    if (rounded != null) { filter.sharedMesh = rounded; mesh = rounded; }
                }

                // A leaf's curl is drawn outside its box, so its bounds carry it; anything else
                // keeps the mesh's own.
                if (TheatreMeshes.IsLamina(mesh))
                {
                    renderer.localBounds = TheatreMeshes.LeafBounds(
                        renderer.transform.localScale, Skin != null ? Skin.CurlFraction : 0.1f);
                }
                else
                {
                    renderer.ResetLocalBounds();
                }

                // Which solid this visual draws, by reference and not by name, so that dressing a
                // body twice reads the same answer the second time: after the swap the mesh is
                // one of the solids this theatre generated, and RoundedFor would no longer
                // recognise it by the engine's name for the primitive it replaced. By name as
                // well, for the raw shapes.
                bool isSphere = mesh == TheatreMeshes.Sphere() || TheatreMeshes.IsPrimitiveSphere(mesh);
                bool isCylinder = mesh == TheatreMeshes.Cylinder() || TheatreMeshes.IsPrimitiveCylinder(mesh);

                int part = body.Part[i];
                if (part < 0 || part >= phenotype.PartCount) continue;

                body.Shape[i] = Aspect(phenotype.Parts[part], isSphere, isCylinder);

                // A capsule whose shaft is shorter than its radius is a ball to any eye, and the
                // three-piece plan draws it badly: the shaft's carve is bounded by its own
                // thickness while the caps are cut a quarter of the way in, so the shaft's rim
                // stands out of the carved caps as a thin disc, and a thin disc flashes wide for
                // one frame when a tumbling body carries it through edge-on (round 46's first
                // films, 2026-09-23). So one cap draws the whole part, at the part's centre,
                // stretched along the shaft to the genome's own half-extent there; the shaft and
                // the other cap are switched off. The ellipsoid with semi-axes (r, r + span, r)
                // is inside the capsule of radius r and half-span span (the distance from any
                // of its points to the axis segment is at most r, by a two-line argument on the
                // convex remainder), so the size bound holds. Not under the raw shapes, which
                // draw the collider as the physics has it.
                if (!RawShapes && (isSphere || isCylinder) && NearlyASphere(phenotype.Parts[part]))
                {
                    if (isSphere && _capTaken.Add(part))
                    {
                        Vector3 h = Abs(phenotype.Parts[part].HalfExtents.ToVector3());
                        float r = CapsuleShape.Radius(phenotype.Parts[part].HalfExtents);
                        Vector3 stretched = body.Shape[i];
                        stretched.y = r > 0f ? Mathf.Max(h.y, r) / r : 1f;
                        body.Shape[i] = stretched;
                        body.Merged[i] = true;
                    }
                    else
                    {
                        renderer.enabled = false;
                        continue;
                    }
                }

                // Squashed before the anchors are measured, not after: Pinches divides by the
                // visual's scale to reach object units, and the scale it has to divide by is the
                // one the mesh is finally drawn at.
                Reshape(body, i, renderer.transform);

                Pinches(body, i, phenotype, part, renderer.transform);
                body.Leaf[i] = OwnAnchor(phenotype, part, renderer.transform);
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

            // The merged cap draws from the part's centre, and a growth resize puts it back at
            // the plan's offset along the shaft together with the scale this just corrected.
            if (body.Merged != null && body.Merged[i]) visual.localPosition = Vector3.zero;
        }

        /// <summary>A capsule whose half-span is under its radius: drawn as one ellipsoid.</summary>
        private static bool NearlyASphere(PhenotypePart part) =>
            part.ShapeId == ShapeIds.Capsule &&
            CapsuleShape.HalfSpan(part.HalfExtents) < CapsuleShape.Radius(part.HalfExtents);

        private readonly HashSet<int> _capTaken = new HashSet<int>();

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

        /// <summary>
        /// The part's own joint anchor in its visual's object units, with w one, or zero for a
        /// root: which end of a leaf is its base.
        /// </summary>
        private static Vector4 OwnAnchor(Phenotype phenotype, int part, Transform visual)
        {
            if (visual == null || part < 0 || part >= phenotype.PartCount) return Vector4.zero;

            PhenotypePart mine = phenotype.Parts[part];
            if (mine.ParentIndex < 0) return Vector4.zero;

            Vector3 scale = visual.localScale;
            if (Mathf.Abs(scale.x) < 1e-6f || Mathf.Abs(scale.y) < 1e-6f || Mathf.Abs(scale.z) < 1e-6f)
            {
                return Vector4.zero;
            }

            Vector3 local = mine.ChildAnchorLocal.ToVector3() - visual.localPosition;
            return new Vector4(local.x / scale.x, local.y / scale.y, local.z / scale.z, 1f);
        }

        // ---------------------------------------------------------------- joints

        /// <summary>
        /// The joint skin: two knuckles of tissue at every free joint, one in each part.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Presentation only.</b> Renderers with no collider and no rigid body, drawn by the
        /// theatre from the phenotype the run already carries; nothing about the simulation changes
        /// and nothing under <c>Assets/Evosim</c> is touched.
        /// </para>
        /// <para>
        /// <b>What it replaces, and why.</b> Until 2026-09-24 a joint was marked by a short
        /// cylinder in the jointed colour, sunk inside the parent part so it could never leave the
        /// collider. In the safari films that read as two solids meeting at a crease with a bead
        /// of paint in it; the owner asked for a joint skin that looks organic. So each side of a
        /// joint now carries an ellipsoid of its own tissue (<see cref="TheatreSkin.JointMaterial"/>,
        /// the part's own guild colour, mottle and rim), centred on the joint anchor in that part's
        /// frame and moving rigidly with the part. With the limb straight, the half of each knuckle
        /// that leaves its own part lies inside the other part and is hidden. As the joint bends,
        /// the two knuckles round over the opening side of the crease, like skin over a knuckle,
        /// and the inset rounded meshes and the pinch at each anchor read as a waist into it.
        /// </para>
        /// <para>
        /// <b>The size bound, and the one place it is spent.</b> Every other visual is drawn inside
        /// its collider. A knuckle's inner half is inside its own part's box (<see cref="Knuckle"/>:
        /// its cross radii are the anchor's clearance to the box's side faces and its length is
        /// clipped to the room behind the face), and its outer half is inside the other part while
        /// the joint is straight. When the joint bends, the outer half shows in the wedge that opens
        /// between the two parts, which is outside both colliders by at most the knuckle's length.
        /// That length is capped at the smaller of the two cross radii, which the other part bounds
        /// too, so on a thin leaf it is the leaf's own thickness. It is the one deliberate exception,
        /// made because a joint drawn without it shows the crease the owner saw.
        /// </para>
        /// <para>
        /// <b>Hidden under the raw shapes</b> (<see cref="RawShapes"/>), which draw the colliders
        /// and nothing else, and under the plain look. Built afresh at every dressing, and the
        /// previous set destroyed first: the raw shapes' toggle dresses a body again, and each
        /// toggle used to leave another set of necks behind.
        /// </para>
        /// </remarks>
        private void BuildNecks(Body body, Phenotype phenotype)
        {
            if (body.Necks != null)
            {
                foreach (Transform old in body.Necks)
                {
                    if (old == null) continue;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(old.gameObject);
                    else UnityEngine.Object.DestroyImmediate(old.gameObject);
                }

                body.Necks = null;
                body.NeckPart = null;
                body.NeckChildSide = null;
            }

            _necks.Clear();
            _neckPart.Clear();
            _neckSide.Clear();

            Mesh mesh = TheatreMeshes.Sphere();
            Material material = Skin != null ? (Skin.JointMaterial ?? Skin.BodyMaterial) : null;
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

            void Add(Transform host, int p, bool childSide)
            {
                if (host == null) return;

                var go = new GameObject(childSide ? "Knuckle (child)" : "Knuckle (parent)")
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
                _neckSide.Add(childSide);
            }

            for (int p = 0; p < phenotype.PartCount; p++)
            {
                PhenotypePart part = phenotype.Parts[p];

                if (part.ParentIndex < 0 || part.JointType == JointType.Fixed) continue;

                Add(partTransform[part.ParentIndex], p, false);
                Add(partTransform[p], p, true);
            }

            if (_necks.Count == 0) return;

            body.Necks = _necks.ToArray();
            body.NeckPart = _neckPart.ToArray();
            body.NeckChildSide = _neckSide.ToArray();
        }

        /// <summary>
        /// Sizes and paints every knuckle from the phenotype as it is now.
        /// </summary>
        /// <remarks>
        /// Recomputed on each paint rather than once, because a body grows: D087 rebuilds a
        /// creature's phenotype at its new size and <c>ResizeColliderAndVisual</c> moves the
        /// visuals to match, so a knuckle placed once would keep a newborn's dimensions on an
        /// adult. Three transform writes a knuckle, inside a repaint budget already capped per
        /// frame. Each knuckle takes its own part's colour, carve character and light through the
        /// tissue, a little darker than the part, so a joint between a leaf and a stomach shades
        /// from one into the other across the crease.
        /// </remarks>
        private void RefreshNecks(Body body, Phenotype phenotype, float brightness, float reserve, bool on, bool dim = false, float turn = 0f)
        {
            if (body.Necks == null) return;

            for (int i = 0; i < body.Necks.Length; i++)
            {
                Transform neck = body.Necks[i];
                if (neck == null) continue;

                int p = body.NeckPart[i];
                bool childSide = body.NeckChildSide != null && body.NeckChildSide[i];

                if (p < 0 || p >= phenotype.PartCount || phenotype.Parts[p].ParentIndex < 0)
                {
                    neck.gameObject.SetActive(false);
                    continue;
                }

                PhenotypePart part = phenotype.Parts[p];
                PhenotypePart parent = phenotype.Parts[part.ParentIndex];

                bool fitted = Knuckle(part, parent, childSide, out Vector3 centre, out Vector3 scale);
                bool show = fitted && on && !RawShapes;

                neck.gameObject.SetActive(show);
                if (!show) continue;

                neck.localPosition = centre;
                neck.localRotation = Quaternion.identity;
                neck.localScale = scale;

                var renderer = neck.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                int own = childSide ? p : part.ParentIndex;
                string cellType = phenotype.Parts[own].CellTypeId;

                Color guild = Turned(ColourOf(cellType), turn);
                if (dim) guild = Grey(guild);

                float seed = body.Seed + 0.61803399f * own + 0.5f;
                seed -= Mathf.Floor(seed);

                Set(renderer, guild, KnuckleShade * brightness, reserve, true,
                    Character(cellType, seed), Vector4.zero, Vector4.zero,
                    TransmissionOf(cellType), SheenOf(cellType), default, OwnOf(body, own));
            }
        }

        /// <summary>How much of its part's brightness a knuckle keeps: a crease reads a shade darker.</summary>
        private const float KnuckleShade = 0.85f;

        /// <summary>
        /// One knuckle's centre and scale in its own part's frame, or false when nothing fits.
        /// </summary>
        /// <remarks>
        /// The mesh is <see cref="TheatreMeshes.Sphere"/>, a unit sphere of diameter one, so the
        /// scale is the ellipsoid's three diameters on the part's own box axes and no rotation is
        /// needed. The joint's face is the box axis the anchor reaches furthest along (the axis whose reach, over that
        /// axis's own half extent, is largest); the two cross radii are the anchor's clearance to the side
        /// faces on the other two axes, each capped at the other part's smaller clearance so the
        /// knuckle is never wider than the limb across the joint, and taken at nine tenths so its
        /// widest ring stays under the inset rounded edge; the half-length along the face's axis is
        /// the smaller cross radius, clipped to the room behind the face in both parts.
        /// </remarks>
        /// <summary>
        /// Whether a part is drawn as a leaf. A box that is flat is one whatever its tissue, and
        /// a photosynthetic box is one whatever its thickness: round 47's leaves are not sheets
        /// (smallest side over the next, median 0.79 in seed 1's 5,000 s snapshot, none under
        /// 0.4), so a thick one is drawn as a plump leaf, a succulent's, inside its box.
        /// </summary>
        public static bool DrawnAsLeaf(PhenotypePart part, Vector3 sides)
        {
            if (!TheatreMeshes.Leaves || part.ShapeId != ShapeIds.Box) return false;
            return TheatreMeshes.LeafShaped(sides) || part.CellTypeId == CellTypeIds.Photosynthetic;
        }

        private static bool Knuckle(
            PhenotypePart part, PhenotypePart parent, bool childSide,
            out Vector3 centre, out Vector3 scale)
        {
            centre = Vector3.zero;
            scale = Vector3.one;

            PhenotypePart own = childSide ? part : parent;
            PhenotypePart other = childSide ? parent : part;
            Vector3 ownAnchor = (childSide ? part.ChildAnchorLocal : part.ParentAnchorLocal).ToVector3();
            Vector3 otherAnchor = (childSide ? part.ParentAnchorLocal : part.ChildAnchorLocal).ToVector3();

            if (!Face(own, other, ownAnchor, out int axis, out float ca, out float cb, out float room))
            {
                return false;
            }

            if (!Face(other, own, otherAnchor, out _, out float oa, out float ob, out float otherRoom))
            {
                return false;
            }

            float across = Mathf.Min(oa, ob);
            float ra = 0.9f * Mathf.Min(ca, across);
            float rb = 0.9f * Mathf.Min(cb, across);

            // A leaf narrows to a point at its base (TheatreBody.shader, ShapeLamina), so a
            // knuckle as wide as its box would stand out of the drawn leaf as a flat lozenge.
            // On a leaf-shaped part it is a swelling at the stalk: a fifth of the half width.
            Vector3 ownHalf = Abs(own.HalfExtents.ToVector3());
            if (DrawnAsLeaf(own, ownHalf))
            {
                float halfWidth = ownHalf.x + ownHalf.y + ownHalf.z
                    - Mathf.Min(ownHalf.x, Mathf.Min(ownHalf.y, ownHalf.z))
                    - Mathf.Max(ownHalf.x, Mathf.Max(ownHalf.y, ownHalf.z));
                ra = Mathf.Min(ra, 0.2f * halfWidth);
                rb = Mathf.Min(rb, 0.2f * halfWidth);
            }
            float length = Mathf.Min(Mathf.Min(ra, rb), Mathf.Min(room, otherRoom));

            if (ra <= 1e-4f || rb <= 1e-4f || length <= 1e-4f) return false;

            int a = (axis + 1) % 3;
            int b = (axis + 2) % 3;

            centre = ownAnchor;
            scale = Vector3.zero;
            scale[axis] = 2f * length;
            scale[a] = 2f * ra;
            scale[b] = 2f * rb;

            return true;
        }

        /// <summary>
        /// The face of <paramref name="host"/>'s box a joint anchor sits on, the anchor's clearance
        /// to the side faces across it, and the room behind it.
        /// </summary>
        private static bool Face(
            PhenotypePart host, PhenotypePart across, Vector3 anchor,
            out int axis, out float clearA, out float clearB, out float room)
        {
            axis = 0; clearA = 0f; clearB = 0f; room = 0f;

            Vector3 h = Abs(host.HalfExtents.ToVector3());
            if (h.x <= 0f || h.y <= 0f || h.z <= 0f) return false;

            // The direction from the host's centre towards the other part, in the host's frame:
            // Direction's second argument is the part whose frame it measures in.
            Vector3 towards = Direction(across, host, anchor);
            float best = -1f;

            for (int j = 0; j < 3; j++)
            {
                float reach = Mathf.Abs(towards[j]) / h[j];
                if (reach > best) { best = reach; axis = j; }
            }

            float sign = towards[axis] >= 0f ? 1f : -1f;
            int a = (axis + 1) % 3;
            int b = (axis + 2) % 3;

            clearA = h[a] - Mathf.Abs(anchor[a]);
            clearB = h[b] - Mathf.Abs(anchor[b]);
            room = sign > 0f ? anchor[axis] + h[axis] : h[axis] - anchor[axis];

            return clearA > 0f && clearB > 0f && room > 0f;
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

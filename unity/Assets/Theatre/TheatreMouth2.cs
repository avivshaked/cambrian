using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// A view dial that draws each eating part with an opening and a lip on its face, where the world reads its food
    /// (<see cref="MouthPoint.Local"/>): a cup cut into the surface, a dark throat, and a ring round it that reads as
    /// raised by its shading alone. On a part, and on the one skin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and off means nothing happens.</b> <c>EVOSIM_THEATRE_MOUTH2</c> is 0 unless set to 0.5 or more.
    /// The palette's one hook is <c>if (TheatreMouth2.Enabled)</c>, and the one skin's two are the same test, so with the
    /// dial at 0 no property, keyword, mesh channel or object is touched. The shader's side is compiled only into the
    /// <c>_EVO_MOUTH2</c> variant, a <c>multi_compile</c> keyword that <see cref="EnsureKeyword"/> turns on the first time
    /// an eating part is painted or a skin is built with one, and nothing turns off. With the dial on,
    /// <see cref="TheatreMouthMarks"/> draws nothing: this replaces the sphere.
    /// </para>
    /// <para>
    /// <b>Which parts.</b> A part for which <see cref="MouthPoint.IsEatingPart"/> is true, and no other. The point is
    /// Core's, as the sphere's and the solver's are, asked of the phenotype the body is drawn at.
    /// </para>
    /// <para>
    /// <b>Where and how big.</b> The opening is centred on <see cref="MouthPoint.Local"/>. Its radius is
    /// <see cref="RadiusShare"/> of the eating face's smaller half extent (the face the point is the centre of; a sphere
    /// takes its smallest half extent). Its depth is <see cref="DepthShare"/> of the radius, at most
    /// <see cref="DepthHalfLengthShare"/> of the part's half length along the mouth's axis, so a thin plate is not pierced.
    /// The cup moves vertices inward only, so the outline stays inside the collider (D242). The lip is shading and colour
    /// alone.
    /// </para>
    /// <para>
    /// <b>On a part</b> the shader gets <c>_Mouth</c> (the point in the visual's object units, the depth in radii in w) and
    /// <c>_MouthRadii</c> (the radius along each object axis), written into the renderer's property block after the
    /// palette's own <c>Set</c>, as the nose pit's are, so a growth step and a corpse's shrink leave them right.
    /// </para>
    /// <para>
    /// <b>On the one skin</b> (<see cref="TheatreOneSkin"/>) the shader gets up to <see cref="SkinSlots"/> mouths as
    /// <c>_MouthSkin0</c> to <c>_MouthSkin3</c>, each the centre in the body's rest metres in xyz and the radius in rest
    /// metres in w, built once with the mesh from the eating part's rest pose (the part's centre plus its rotation times
    /// Core's point), and written on the skin's block at every dress (<see cref="DressSkin"/>) because the block is copied
    /// from the largest part's each time. Each vertex also carries, in the mesh's UV2, the skinning weight of the 1st to
    /// 4th mouth's part, so the carve is weighted by it and moves with that part. A body with more than
    /// <see cref="SkinSlots"/> eating parts merged into its skin gives the first four, in part order, an opening; the rest
    /// get none. A part left out of the skin (too thin) is drawn as its own part and takes its own opening.
    /// </para>
    /// <para>
    /// <b>The oral disc</b> (<c>EVOSIM_THEATRE_MOUTH2_DISC</c>, 0 by default, read once, any effect only with the dial above on): the
    /// lip ring takes the mark colour (<c>_FamilyColor</c>, where it has a share), and a ring from 1.25 to 1.9 radii is tinted toward a
    /// disc colour, the body's colour at another OKLab lightness (<see cref="TheatreSkinMap.MouthDiscColour"/>), in radial stripes
    /// that alternate with the body's colour. Colour only: the geometry and the outline are today's, so nothing is drawn outside the
    /// collider (D242). The colour reaches the shader as <c>_MouthDisc</c> (rgb the disc colour in the shader's linear space, a the
    /// share, <see cref="DiscShare"/>, zero off), read back from the block's <c>_BaseColor</c> so a growth step, a grey and a corpse
    /// carry it. The stripes' axis and phase are <c>_MouthAxis</c> on a part and <c>_MouthAxisSkin0</c> to <c>_MouthAxisSkin3</c> on the
    /// skin, and their count is <c>_MouthRadii.w</c>, from the body's skin-map entry (<see cref="TheatreSkinMap.MouthStripes"/>), 8 without one.
    /// </para>
    /// <para>
    /// <b>No opening</b> is written (both vectors zero) on a raw shape (the runner's X key), on a blade and on any mesh the
    /// theatre did not make, whose few vertices cannot hold a cup.
    /// </para>
    /// </remarks>
    public static class TheatreMouth2
    {
        /// <summary>The dial: 0, the default, draws nothing.</summary>
        public static readonly bool Enabled =
            TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2", 0f, 0f, 2f) >= 0.5f;

        /// <summary>
        /// The oral-disc dial (<c>EVOSIM_THEATRE_MOUTH2_DISC</c>, 0 or 1, 0 off by default), read once, with any effect only while
        /// <see cref="Enabled"/>. With it at 0 no disc property is written and every pixel is the one without it.
        /// </summary>
        public static readonly bool Disc =
            Enabled && TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_DISC", 0f, 0f, 1f) >= 0.5f;

        /// <summary>The disc's tint share (<c>_MouthDisc.a</c>) with the dial on: the stripes' strength before the disc's outer fade.</summary>
        public const float DiscShare = 0.9f;

        /// <summary>The stripe count where the body has no skin-map entry, and the shader's where none is written.</summary>
        public const int DefaultStripes = 8;

        /// <summary>The shader keyword the opening code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string Keyword = "_EVO_MOUTH2";

        /// <summary>The opening's radius, as a share of the eating face's smaller half extent.</summary>
        public const float RadiusShare = 0.5f;

        /// <summary>The opening's depth, as a share of its radius.</summary>
        public const float DepthShare = 0.4f;

        /// <summary>The most the depth can be, as a share of the part's half length along the mouth's axis.</summary>
        public const float DepthHalfLengthShare = 0.5f;

        /// <summary>The mouths the one skin can carry: four, one per component of the per-vertex weights.</summary>
        public const int SkinSlots = 4;

        private static readonly int MouthId = Shader.PropertyToID("_Mouth");
        private static readonly int MouthRadiiId = Shader.PropertyToID("_MouthRadii");
        private static readonly int MouthDiscId = Shader.PropertyToID("_MouthDisc");
        private static readonly int MouthAxisId = Shader.PropertyToID("_MouthAxis");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static readonly int[] SkinAxisIds =
        {
            Shader.PropertyToID("_MouthAxisSkin0"),
            Shader.PropertyToID("_MouthAxisSkin1"),
            Shader.PropertyToID("_MouthAxisSkin2"),
            Shader.PropertyToID("_MouthAxisSkin3"),
        };

        private static readonly int[] SkinIds =
        {
            Shader.PropertyToID("_MouthSkin0"),
            Shader.PropertyToID("_MouthSkin1"),
            Shader.PropertyToID("_MouthSkin2"),
            Shader.PropertyToID("_MouthSkin3"),
        };

        private static bool _keywordOn;
        private static MaterialPropertyBlock _block;

        /// <summary>An eating part's opening in the part's own frame, metres.</summary>
        public struct Measured
        {
            /// <summary>False for a part that does not eat, or has no usable face or length.</summary>
            public bool Valid;

            /// <summary>The mouth, <see cref="MouthPoint.Local"/>.</summary>
            public Vector3 Point;

            public float RadiusMetres;
            public float DepthMetres;
        }

        /// <summary>
        /// The opening of part <paramref name="part"/> of a body at the size it is drawn at; not valid for a part that
        /// does not eat. Pure: no scene, no renderer.
        /// </summary>
        public static Measured Measure(Phenotype phenotype, int part)
        {
            if (phenotype == null || part < 0 || part >= phenotype.PartCount) return default;

            PhenotypePart p = phenotype.Parts[part];
            if (!MouthPoint.IsEatingPart(p)) return default;

            var half = new Vector3(Mathf.Abs(p.HalfExtents.X), Mathf.Abs(p.HalfExtents.Y), Mathf.Abs(p.HalfExtents.Z));

            Vector3 point = MouthPoint.Local(phenotype, part).ToVector3();
            float halfLength = point.magnitude;

            // The face is the one the point is the centre of: perpendicular to the axis the point lies on. A sphere's
            // point is along any direction, so its face is taken as its smallest half extent.
            float face;
            if (p.ShapeId == ShapeIds.Sphere)
            {
                face = Mathf.Min(half.x, Mathf.Min(half.y, half.z));
            }
            else
            {
                float ax = Mathf.Abs(point.x), ay = Mathf.Abs(point.y), az = Mathf.Abs(point.z);
                if (ax >= ay && ax >= az) face = Mathf.Min(half.y, half.z);
                else if (ay >= az) face = Mathf.Min(half.x, half.z);
                else face = Mathf.Min(half.x, half.y);
            }

            if (!(face > 1e-6f) || !(halfLength > 1e-6f)) return default;

            float radius = RadiusShare * face;

            return new Measured
            {
                Valid = true,
                Point = point,
                RadiusMetres = radius,
                DepthMetres = Mathf.Min(DepthShare * radius, DepthHalfLengthShare * halfLength),
            };
        }

        /// <summary>
        /// Writes the opening of one renderer of an eating part into its property block, or zeroes it. Called by
        /// <see cref="TheatrePalette.Paint"/> for every renderer, after the palette's own <c>Set</c>, and only with the
        /// dial on. A part that does not eat is left alone, except with <see cref="Disc"/> on, where it carries the body's stripe
        /// count in <c>_MouthRadii.w</c> (<paramref name="skin"/>, the body's skin-map entry, if it has one) for a one skin to copy.
        /// </summary>
        internal static void Paint(MeshRenderer renderer, Phenotype phenotype, int part, bool rawShapes, TheatreSkinMap.Entry skin = null)
        {
            if (renderer == null || phenotype == null || part < 0 || part >= phenotype.PartCount) return;

            if (!MouthPoint.IsEatingPart(phenotype.Parts[part]))
            {
                if (Disc) CarryStripes(renderer, phenotype, skin);
                return;
            }

            EnsureKeyword();
            // mouth2b:begin
            LogPart(renderer, phenotype, part);
            // mouth2b:end

            var mouth = Vector4.zero;
            var radii = Vector4.zero;

            if (!rawShapes && MadeByTheatre(renderer))
            {
                Measured m = Measure(phenotype, part);
                Transform visual = renderer.transform;
                Vector3 offset = visual.localPosition;
                Vector3 scale = visual.localScale;

                if (m.Valid &&
                    Mathf.Abs(scale.x) >= 1e-6f && Mathf.Abs(scale.y) >= 1e-6f && Mathf.Abs(scale.z) >= 1e-6f)
                {
                    // The point in the visual's object units, as the joint anchors are (TheatrePalette.Pinches) and the nose
                    // pit's is, and the radius along each object axis: metres over the visual's size on that axis.
                    mouth = new Vector4(
                        (m.Point.x - offset.x) / scale.x,
                        (m.Point.y - offset.y) / scale.y,
                        (m.Point.z - offset.z) / scale.z,
                        m.DepthMetres / m.RadiusMetres);

                    radii = new Vector4(
                        m.RadiusMetres / Mathf.Abs(scale.x),
                        m.RadiusMetres / Mathf.Abs(scale.y),
                        m.RadiusMetres / Mathf.Abs(scale.z),
                        0f);
                }
            }

            if (_block == null) _block = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(_block);

            if (Disc)
            {
                // The oral disc: the stripe count in the radii's w, the colour from the block's own base colour, the axis in the part's
                // frame (the radii are the object units over the part's, so a direction in it is the same in both). No opening, no disc.
                radii.w = CodedStripes(phenotype, skin);

                Vector4 disc = Vector4.zero, axis = Vector4.zero;

                if (mouth.w > 0f)
                {
                    Color colour = TheatreSkinMap.MouthDiscColour(_block.GetVector(BaseColorId));
                    disc = new Vector4(colour.r, colour.g, colour.b, DiscShare);

                    Vector3 local = Measure(phenotype, part).Point.normalized;
                    axis = new Vector4(local.x, local.y, local.z, 0f);
                }

                _block.SetVector(MouthDiscId, disc);
                _block.SetVector(MouthAxisId, axis);
                SetFlat(_block);
                SetRelief(_block);
            }

            _block.SetVector(MouthId, mouth);
            _block.SetVector(MouthRadiiId, radii);
            SetBite(_block);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>A part that does not eat, with <see cref="Disc"/> on: the body's stripe count in <c>_MouthRadii.w</c>, nothing else.</summary>
        private static void CarryStripes(MeshRenderer renderer, Phenotype phenotype, TheatreSkinMap.Entry skin)
        {
            if (_block == null) _block = new MaterialPropertyBlock();

            renderer.GetPropertyBlock(_block);
            _block.SetVector(MouthRadiiId, new Vector4(0f, 0f, 0f, CodedStripes(phenotype, skin)));
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// The oral-disc dial for varied mouth kinds (<c>EVOSIM_THEATRE_MOUTH2_KINDS</c>, 0 or 1, 0 off by default), read once, with any
        /// effect only while <see cref="Disc"/> is on. At 0 the value carried in <c>_MouthRadii.w</c> is the stripe count alone, as before.
        /// </summary>
        public static readonly bool Kinds =
            Disc && TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_KINDS", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// The flat-mouth dial (<c>EVOSIM_THEATRE_MOUTH2_FLAT</c>, 0 to 1, 0 off by default), read once, with any effect only while
        /// <see cref="Disc"/> is on, since it needs the disc's axis: the share of the way from the mouth's distance in space to its distance
        /// taken flat along its axis (<c>_MouthFlat</c>), so a mouth on a sharp rim is round seen along its axis rather than squashed
        /// across the rim. At 0 nothing is written and every pixel is the one without it.
        /// </summary>
        public static readonly float Flat =
            Disc ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_FLAT", 0f, 0f, 1f) : 0f;

        /// <summary>
        /// How far behind the mouth's plane, in radii, the flat distance reaches before the far side of the part is pushed back out
        /// (<c>EVOSIM_THEATRE_MOUTH2_FLAT_SLACK</c>, 0 to 10, 0 the shader's own 2.5), written only with <see cref="Flat"/> on.
        /// </summary>
        public static readonly float FlatSlack =
            Flat > 0f ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_FLAT_SLACK", 0f, 0f, 10f) : 0f;

        private static readonly int MouthFlatId = Shader.PropertyToID("_MouthFlat");
        private static readonly int MouthFlatSlackId = Shader.PropertyToID("_MouthFlatSlack");

        /// <summary>
        /// The mouth relief dial (<c>EVOSIM_THEATRE_MOUTH2_RELIEF</c>, 0 to 1, 0 off by default), read once, with any effect only while
        /// <see cref="Kinds"/> or <see cref="Disc"/> is on, since it shapes the oral disc (kind 0, every mouth while the kinds are off), the
        /// toothed ring (1) and the gape (5): they read as openings, with a raised lip, an inner wall in a lining colour that the light shapes,
        /// and teeth as ridged cones of enamel rather than flat pale petals (<c>_MouthRelief</c>, the strength). Shading and colour only, as
        /// the lip is: no vertex moves for it (D242). At 0 nothing is written and every pixel is the one without it.
        /// </summary>
        public static readonly float Relief =
            Kinds || Disc ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_RELIEF", 0f, 0f, 1f) : 0f;

        /// <summary>
        /// The small-mouth dial (<c>EVOSIM_THEATRE_MOUTH2_SMALL</c>, 0 to 1, 0 off by default), read once, with any effect only while
        /// <see cref="Disc"/> is on: the disc and every kind's marks fade out as the mouth shrinks on screen (full from a radius of 28 px, gone
        /// by 10), and the disc's stripes are soft-edged at every size, so a mouth 15 to 25 px across in a world shot is its opening and lip
        /// rather than a cog's teeth (<c>_MouthSmall</c>, the strength). At 0 nothing is written and every pixel is the one without it.
        /// </summary>
        public static readonly float Small =
            Disc ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_SMALL", 0f, 0f, 1f) : 0f;

        /// <summary>
        /// The plain-mouth dial (<c>EVOSIM_THEATRE_MOUTH2_PLAIN</c>, 0 to 1, 0 off by default), read once, with any effect only while
        /// <see cref="Disc"/> is on: out to the lip's outer edge the mouth is drawn on the body's colour before its chromatophores and
        /// pattern, so no mark of the skin runs across the lip, the wall or the throat, and the lip takes the body's own hue (its plain
        /// colour lifted) rather than the mark's, at half its step from the body, so a ring round a dark throat reads as a shaded rim
        /// rather than an eye (<c>_MouthPlain</c>, the strength). At 0 nothing is written and every pixel is the one without it.
        /// </summary>
        public static readonly float Plain =
            Disc ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_PLAIN", 0f, 0f, 1f) : 0f;

        private static readonly int MouthReliefId = Shader.PropertyToID("_MouthRelief");
        private static readonly int MouthSmallId = Shader.PropertyToID("_MouthSmall");
        private static readonly int MouthPlainId = Shader.PropertyToID("_MouthPlain");

        private static void SetRelief(MaterialPropertyBlock block)
        {
            if (Relief > 0f) block.SetFloat(MouthReliefId, Relief);
            if (Small > 0f) block.SetFloat(MouthSmallId, Small);
            if (Plain > 0f) block.SetFloat(MouthPlainId, Plain);
        }

        /// <summary>
        /// The face-on dial (<c>EVOSIM_THEATRE_MOUTH2_FACE</c>, 0 or 1, 0 off by default), read once, with any effect only while
        /// <see cref="Kinds"/> is on: a sucker (kind 2) on the body's rim (<see cref="FacesOnRim"/>) is looked at along its own axis,
        /// where its rows read as rings, rather than <c>TheatreSoloStill</c>'s 0.3 turn toward the usual bearing or the portrait's
        /// <c>EVOSIM_THEATRE_PORTRAIT_FROM_ANGLE</c> off the axis, which see a rim sucker across the rim's two slopes. Camera only. At 0
        /// every still and every portrait is the one without it.
        /// </summary>
        public static readonly bool Face =
            Kinds && TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_FACE", 0f, 0f, 1f) >= 0.5f;

        /// <summary>The eye's angle off a face-on sucker's axis, degrees.</summary>
        public const float FaceAngle = 0f;

        /// <summary>
        /// The kind a renderer was painted with (<c>_MouthRadii.w</c> over <see cref="KindCodeStep"/>), or -1 when it carries none.
        /// </summary>
        public static int PaintedKind(Renderer renderer)
        {
            if (renderer == null) return -1;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            float w = block.GetVector(MouthRadiiId).w;
            return w >= KindCodeStep ? Mathf.FloorToInt(w / KindCodeStep) : (w > 0.5f ? 0 : -1);
        }

        /// <summary>The kind the first of a body's renderers that carries one was painted with (every part carries the body's), or -1.</summary>
        public static int PaintedKind(Renderer[] renderers)
        {
            if (renderers == null) return -1;
            for (int r = 0; r < renderers.Length; r++)
            {
                int kind = PaintedKind(renderers[r]);
                if (kind >= 0) return kind;
            }

            return -1;
        }

        /// <summary>
        /// Whether a part's mouth sits on the body's rim: the part is flat (its middle half extent over its smallest at least
        /// <see cref="KindRatio"/>, as <see cref="MouthKind"/> calls it) and the mouth's axis (<paramref name="axisLocal"/>, in the part's
        /// frame) lies mostly across the thin axis, so the opening is on a thin edge and not on a broad face.
        /// </summary>
        public static bool FacesOnRim(PhenotypePart part, Vector3 axisLocal)
        {
            if (part == null || !(axisLocal.sqrMagnitude > 1e-8f)) return false;

            var h = new Vector3(Mathf.Abs(part.HalfExtents.X), Mathf.Abs(part.HalfExtents.Y), Mathf.Abs(part.HalfExtents.Z));
            int thin = h.x <= h.y && h.x <= h.z ? 0 : (h.y <= h.z ? 1 : 2);
            float smallest = h[thin];
            float middle = thin == 0 ? Mathf.Min(h.y, h.z) : (thin == 1 ? Mathf.Min(h.x, h.z) : Mathf.Min(h.x, h.y));
            if (!(smallest > 1e-6f) || middle / smallest < KindRatio) return false;

            return Mathf.Abs(axisLocal.normalized[thin]) < 0.5f;
        }

        private static void SetFlat(MaterialPropertyBlock block)
        {
            if (!(Flat > 0f)) return;
            block.SetFloat(MouthFlatId, Flat);
            if (FlatSlack > 0f) block.SetFloat(MouthFlatSlackId, FlatSlack);
        }

        /// <summary>
        /// The bite-pulse dial (<c>EVOSIM_THEATRE_MOUTH2_BITE</c>, 0 to 1, 0 off by default), read once, with any effect only while
        /// <see cref="Enabled"/>: the strength of the flash and swell a mouth gets on a bite (<c>_MouthBiteParams.x</c>,
        /// <c>TheatreMouthBite</c>). At 0 no timer is read, no property is written and every pixel is the one without it.
        /// </summary>
        public static readonly float Bite =
            Enabled ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_BITE", 0f, 0f, 1f) : 0f;

        /// <summary>
        /// A fake onset every this many film seconds on every mouth (<c>EVOSIM_THEATRE_MOUTH2_BITE_TEST</c>, 0 off by default), read
        /// only with <see cref="Bite"/> on.
        /// </summary>
        public static readonly float BiteTest =
            Bite > 0f ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_BITE_TEST", 0f, 0f, 3600f) : 0f;

        /// <summary>
        /// Every mouth's timer pinned to this many seconds (<c>EVOSIM_THEATRE_MOUTH2_BITE_AT</c>, NaN, unset, by default), for stills;
        /// no onset is read then. Read only with <see cref="Bite"/> on.
        /// </summary>
        public static readonly float BiteAt =
            Bite > 0f ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_BITE_AT", float.NaN, 0f, 100f) : float.NaN;

        /// <summary>
        /// One pulse a meal (<c>EVOSIM_THEATRE_MOUTH2_BITE_MEAL</c>, world seconds, 0 off by default; the owner's choice of
        /// 2026-11-01): a mouth whose ate bit was last set at most this long ago is still at the same meal, and its bit starts no
        /// pulse. The farm feeds at its half-second step, so a meal sets the bit once every 0.5 s; 0.55, the step and a pose
        /// step of a 30 Hz window, ends a meal at the first missed step. Off, each half-second intake is an onset of its own and a
        /// sustained meal strobes at 2 Hz. Read only with <see cref="Bite"/> on.
        /// </summary>
        public static readonly float BiteMeal =
            Bite > 0f ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_BITE_MEAL", 0f, 0f, 60f) : 0f;

        /// <summary>
        /// The flash at a share of the old one (<c>EVOSIM_THEATRE_MOUTH2_BITE_FLASH</c>, 0 to 1, 0 by default since the owner ruled the flash
        /// off on 2026-11-02), read only with <see cref="Bite"/> on: a multiplier on the strength the shader's pulse reads
        /// (<c>_MouthBiteParams.x</c>), so 0 is no flash, 0.33 a third of it and 1 the flash as it was.
        /// The swell goes with the flash and not with the gape (<see cref="Gape"/>): the gape shuts on the onset, and a mouth that swells
        /// just after it has shut reads as opening again, a second motion where there should be one. The onsets, the timers and every log
        /// that reads <see cref="Bite"/> are untouched, so the crumbs (<see cref="BiteCrumbs"/>) and the gape still have their onsets with
        /// the flash at 0. At 1 the value written is the old flash's to the bit (the strength times one). With <see cref="Bite"/> off it is
        /// 1 and unread, as it always was.
        /// </summary>
        public static readonly float BiteFlash =
            Bite > 0f ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_BITE_FLASH", 0f, 0f, 1f) : 1f;

        /// <summary>
        /// The gape (<c>EVOSIM_THEATRE_MOUTH2_GAPE</c>, film seconds of lead, 0 off by default, 0.3 to 0.5 sensible), read only with
        /// <see cref="Bite"/> on, whose onsets it reads: the drawn mouth opens over this long before each onset and shuts on it
        /// (<c>TheatreMouthBite.GapeOf</c>, <c>_MouthBiteGape</c>, the flag in <c>_MouthBiteParams.w</c>). The opening is the shader's
        /// alone, a widening of the drawn mouth and its throat; no vertex moves and no part is moved or scaled. At 0 nothing is written
        /// and every pixel is the one without it.
        /// </summary>
        public static readonly float Gape =
            Bite > 0f ? TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2_GAPE", 0f, 0f, 2f) : 0f;

        /// <summary>
        /// The bite's crumbs (<c>EVOSIM_THEATRE_BITE_CRUMBS</c>, 0 or 1, 0 off by default), read only with <see cref="Bite"/> on, whose
        /// onsets they read: at each recorded onset a few motes torn from the prey at the mouth, drifting off with the water and
        /// sinking, gone in a few seconds (<c>TheatreMouthBite</c>'s crumbs, drawn with the crumble's flake shader). Off, nothing is
        /// built and nothing is drawn.
        /// </summary>
        public static readonly bool BiteCrumbs =
            Bite > 0f && TheatreSkin.Dial("EVOSIM_THEATRE_BITE_CRUMBS", 0f, 0f, 1f) >= 0.5f;

        /// <summary>The flash share (<c>_MouthBiteParams.y</c>): the lip and disc lift toward white by this times the strength times e(t).</summary>
        public const float BiteFlashShare = 0.6f;

        /// <summary>The value of a timer that is idle: no onset, or the last one over.</summary>
        public const float BiteIdle = 1e4f;

        internal static readonly int BiteId = Shader.PropertyToID("_MouthBite");
        private static readonly int BiteParamsId = Shader.PropertyToID("_MouthBiteParams");

        /// <summary>The gape's share open, 0 to 1: a part's in x, the one skin's four slots in x to w. Written only with <see cref="Gape"/> on.</summary>
        internal static readonly int GapeId = Shader.PropertyToID("_MouthBiteGape");

        /// <summary>The four timers idle: a part's (x only) or the one skin's four slots.</summary>
        internal static readonly Vector4 BiteIdleVector = new Vector4(BiteIdle, BiteIdle, BiteIdle, BiteIdle);

        // x the strength the pulse reads (the dial times the flash's share of today's, BiteFlash; today's to the bit at 1), y the flash
        // share, z the test period, w 1 with the gape on (the shader's gate for _MouthBiteGape), 0 as before with it off.
        private static void SetBite(MaterialPropertyBlock block)
        {
            if (!(Bite > 0f)) return;
            block.SetVector(BiteParamsId, new Vector4(Bite * BiteFlash, BiteFlashShare, BiteTest, Gape > 0f ? 1f : 0f));
        }

        /// <summary>
        /// The ratio at which a part is long or flat: the leading one of its two ratios (largest half extent over the middle, middle over
        /// the smallest) is at least this. It was 1.8 with a sphere always round, and every eater of the v7e5k stills came out round: the
        /// mouth parts are stretched spheres and boxes whose ratios run 1.0 to 2.1 (scratch/skin-map/full-colour/v7d/mouth_shapes.py).
        /// </summary>
        public const float KindRatio = 1.4f;

        /// <summary>The kind is carried in <c>_MouthRadii.w</c> as stripes plus this times the kind (stripes are at most 12).</summary>
        public const int KindCodeStep = 16;

        /// <summary>The value for <c>_MouthRadii.w</c>: the stripe count, plus <see cref="KindCodeStep"/> times the mouth kind with <see cref="Kinds"/> on.</summary>
        private static float CodedStripes(Phenotype phenotype, TheatreSkinMap.Entry skin)
        {
            int stripes = TheatreSkinMap.MouthStripes(skin);

            return Kinds ? stripes + KindCodeStep * MouthKind(phenotype, stripes) : stripes;
        }

        /// <summary>
        /// The oral disc's kind for a body, from its mouth parts (the parts <see cref="MouthPoint.IsEatingPart"/> is true for), so kin share it.
        /// The main mouth part is the largest of them by the product of its half extents; its three half extents sorted a &gt;= b &gt;= c
        /// make it long when a / b leads b / c and is at least <see cref="KindRatio"/>, flat when b / c leads and is at least it, and
        /// round otherwise, whatever its shape (the theatre draws a stretched sphere as an ellipsoid). The count is the number of mouth
        /// parts. Most mouth parts are flat (about 80 % of the skin map's eaters, since the body plan is flattened), so a flat part's kind
        /// is chosen by the body's stripe count, heritable, among four kinds a flat oral disc carries. The kind:
        /// <list type="table">
        /// <item><term>2, sucker</term><description>three or more mouth parts, any shape; or flat with <paramref name="stripes"/> 12.</description></item>
        /// <item><term>1, toothed ring</term><description>round; or flat with <paramref name="stripes"/> 8.</description></item>
        /// <item><term>4, beak</term><description>a box that is long and also has b / c at least the ratio.</description></item>
        /// <item><term>3, crown of tentacles</term><description>long, any other part.</description></item>
        /// <item><term>0, the radial-stripe disc</term><description>flat, with <paramref name="stripes"/> 6.</description></item>
        /// <item><term>5, gape</term><description>flat, with <paramref name="stripes"/> 10.</description></item>
        /// </list>
        /// 0 for a body with no mouth part or none usable.
        /// </summary>
        internal static int MouthKind(Phenotype phenotype, int stripes)
        {
            if (phenotype == null) return 0;

            int count = 0;
            int main = -1;
            float mainVolume = -1f;

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                PhenotypePart p = phenotype.Parts[i];
                if (!MouthPoint.IsEatingPart(p)) continue;

                count++;

                float volume = Mathf.Abs(p.HalfExtents.X) * Mathf.Abs(p.HalfExtents.Y) * Mathf.Abs(p.HalfExtents.Z);
                if (volume > mainVolume) { mainVolume = volume; main = i; }
            }

            if (main < 0) return 0;
            if (count >= 3) return 2;

            PhenotypePart m = phenotype.Parts[main];

            float a = Mathf.Abs(m.HalfExtents.X), b = Mathf.Abs(m.HalfExtents.Y), c = Mathf.Abs(m.HalfExtents.Z);
            float t;
            if (a < b) { t = a; a = b; b = t; }
            if (b < c) { t = b; b = c; c = t; }
            if (a < b) { t = a; a = b; b = t; }

            if (!(c > 1e-6f)) return 0;

            float along = a / b, across = b / c;
            bool isLong = along >= across && along >= KindRatio;
            bool isFlat = !isLong && across >= KindRatio;

            if (isLong) return m.ShapeId == ShapeIds.Box && across >= KindRatio ? 4 : 3;
            if (isFlat) return stripes >= 12 ? 2 : stripes >= 10 ? 5 : stripes >= 8 ? 1 : 0;

            return 1;
        }

        /// <summary>
        /// The one skin's mouths into its block, at every dress (<paramref name="axes"/>, the oral disc's axis and phase per mouth from
        /// <see cref="SkinMouth.AxisPhase"/>, null with <see cref="Disc"/> off): <c>_Mouth</c> carries the depth in radii in w and nothing
        /// else (the largest part's own values, copied into the block, are in that part's object units and meaningless on
        /// the skin), <c>_MouthRadii</c> is cleared, and <c>_MouthSkin0</c> to <c>_MouthSkin3</c> are the mouths in rest
        /// metres, or zero where the skin has fewer. Only with the dial on.
        /// </summary>
        public static void DressSkin(MaterialPropertyBlock block, Vector4[] mouths, Vector4[] axes = null, Vector4? bite = null, Vector4? gape = null)
        {
            if (block == null) return;

            // The oral disc, read before the radii are cleared: the stripe count the largest part carries, and the colour from its base colour.
            float stripes = Disc ? block.GetVector(MouthRadiiId).w : 0f;

            block.SetVector(MouthId, new Vector4(0f, 0f, 0f, DepthShare));
            block.SetVector(MouthRadiiId, Disc ? new Vector4(0f, 0f, 0f, stripes) : Vector4.zero);

            for (int k = 0; k < SkinIds.Length; k++)
            {
                block.SetVector(SkinIds[k], mouths != null && k < mouths.Length ? mouths[k] : Vector4.zero);
            }

            // The bite pulse's strength and the skin's own four timers (<paramref name="bite"/>, idle where TheatreMouthBite has written none),
            // over the largest part's, which the block was copied from. Only with the dial on.
            if (Bite > 0f)
            {
                SetBite(block);
                block.SetVector(BiteId, bite ?? BiteIdleVector);

                // The gape's four shares (<paramref name="gape"/>, shut where TheatreMouthBite has written none), over the largest part's.
                if (Gape > 0f) block.SetVector(GapeId, gape ?? Vector4.zero);
            }

            if (!Disc) return;

            Color colour = TheatreSkinMap.MouthDiscColour(block.GetVector(BaseColorId));

            block.SetVector(MouthDiscId, mouths != null && mouths.Length > 0
                ? new Vector4(colour.r, colour.g, colour.b, DiscShare)
                : Vector4.zero);
            block.SetVector(MouthAxisId, Vector4.zero);

            for (int k = 0; k < SkinAxisIds.Length; k++)
            {
                block.SetVector(SkinAxisIds[k], axes != null && k < axes.Length ? axes[k] : Vector4.zero);
            }

            SetFlat(block);
            SetRelief(block);
        }

        /// <summary>
        /// The stripes' tangent basis on the skin, as a vector: the mouth's axis in the rest frame (<paramref name="rotation"/> times the part
        /// frame's axis <paramref name="pointRest"/> points along) in xyz, and in w the angle, about that axis, between the shader's reference
        /// direction (the rest frame's up, or its x where the axis is within 0.9 of up) and the part frame's own (the part's up, or x), so
        /// the stripes stand where they stand on the part drawn alone. The shader builds its basis from the axis by the same rule.
        /// </summary>
        public static Vector4 AxisPhase(Vector3 pointRest, Quaternion rotation)
        {
            if (!(pointRest.magnitude > 1e-9f)) return Vector4.zero;

            Vector3 local = pointRest.normalized;
            Vector3 axis = rotation * local;

            Vector3 helperLocal = Mathf.Abs(local.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 partBasis = rotation * Vector3.Cross(local, helperLocal).normalized;

            Vector3 helper = Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 basis1 = Vector3.Cross(axis, helper).normalized;
            Vector3 basis2 = Vector3.Cross(axis, basis1);

            float phase = Mathf.Atan2(Vector3.Dot(partBasis, basis2), Vector3.Dot(partBasis, basis1));

            return new Vector4(axis.x, axis.y, axis.z, phase);
        }

        // mouth2b:begin
        /// <summary>
        /// Level 2 (<c>EVOSIM_THEATRE_MOUTH2</c> at 1.5 or more, read once): on the one skin the mouth point is moved inward
        /// along the part's mouth axis onto the skin that is drawn, and the radius is taken from the drawn section there
        /// where that is smaller (<see cref="PlaceOnSkin"/>). Level 1 is unchanged. The diagnostics below run at either level.
        /// </summary>
        public static readonly bool Level2 = TheatreSkin.Dial("EVOSIM_THEATRE_MOUTH2", 0f, 0f, 2f) >= 1.5f;

        /// <summary>The most the point moves inward, as a share of the part's half length along the mouth's axis.</summary>
        public const float MoveHalfLengthShare = 0.5f;

        /// <summary>How far along the axis, either side of the moved point, the drawn section is read, as a share of the collider radius.</summary>
        public const float SectionDepthShare = 1.0f;

        /// <summary>The fewest vertices a drawn section is read from; with fewer the collider's radius stands.</summary>
        public const int SectionMinVertices = 6;

        /// <summary>One skin mouth, placed: its centre and radius in the body's rest metres, and what the diagnostics report.</summary>
        public struct SkinMouth
        {
            /// <summary>The mouth as the skin carries it: rest metres in xyz, the radius in w. The input's, unless level 2 moved it.</summary>
            public Vector4 Rest;

            /// <summary>The mouth as Core's point gave it (the input).</summary>
            public Vector4 Before;

            /// <summary>The metres the point moved inward (zero below level 2).</summary>
            public float Moved;

            /// <summary>The distance from the input point to the nearest vertex whose heaviest bone is the part's; minus one if none.</summary>
            public float NearestBefore;

            /// <summary>The same from the placed point (equal to <see cref="NearestBefore"/> below level 2).</summary>
            public float NearestAfter;

            /// <summary>The vertices whose heaviest bone is the part's.</summary>
            public int Dominant;

            /// <summary>The vertices the drawn section's radius was read from (zero below level 2).</summary>
            public int Section;

            /// <summary>The oral disc's axis in the rest frame (xyz) and stripe phase (w), <see cref="AxisPhase(Vector3, Quaternion)"/>; zero with <see cref="Disc"/> off.</summary>
            public Vector4 AxisPhase;
        }

        private static int HeaviestBone(BoneWeight bw)
        {
            int bone = bw.boneIndex0;
            float top = bw.weight0;

            if (bw.weight1 > top) { top = bw.weight1; bone = bw.boneIndex1; }
            if (bw.weight2 > top) { top = bw.weight2; bone = bw.boneIndex2; }
            if (bw.weight3 > top) { bone = bw.boneIndex3; }

            return bone;
        }

        /// <summary>
        /// A merged eating part's mouth on the one skin. <paramref name="bone"/> is the part's merged index, which is its bone
        /// index (the weights, the bind poses and the bones are all indexed by the merged order);
        /// <paramref name="pointRest"/> is Core's point in the part's frame over the one scale the skin is built at, and
        /// <paramref name="rest"/> is the mouth as the skin would carry it at level 1 (the point in rest metres, the radius
        /// in w). Always measures the nearest vertex whose heaviest bone is the part's. At level 2 it moves the point inward
        /// along the mouth axis to that vertex's axial coordinate (at most <see cref="MoveHalfLengthShare"/> of the half
        /// length, never outward), and takes the radius from the drawn section there where that is smaller: the half widths
        /// across the axis of the part's vertices within <see cref="SectionDepthShare"/> radii of the moved point along it,
        /// at <see cref="RadiusShare"/>. The move is inward on the line from the part's centre to its face and the radius
        /// never grows, so nothing is drawn outside the collider (D242).
        /// </summary>
        public static SkinMouth PlaceOnSkin(
            Vector3[] positions, BoneWeight[] weights, int bone, Vector3 centre, Quaternion rotation, Vector3 pointRest, Vector4 rest)
        {
            SkinMouth placed = PlaceOnSkinCore(positions, weights, bone, centre, rotation, pointRest, rest);

            // The oral disc's stripes need the mouth's axis: handed back for the skin to keep beside its mouths.
            if (Disc) placed.AxisPhase = AxisPhase(pointRest, rotation);

            return placed;
        }

        /// <summary>The placement itself (see <see cref="PlaceOnSkin"/>); the axis is noted by the caller.</summary>
        private static SkinMouth PlaceOnSkinCore(
            Vector3[] positions, BoneWeight[] weights, int bone, Vector3 centre, Quaternion rotation, Vector3 pointRest, Vector4 rest)
        {
            var result = new SkinMouth { Rest = rest, Before = rest, NearestBefore = -1f, NearestAfter = -1f };
            var at = new Vector3(rest.x, rest.y, rest.z);

            int nearest = -1;
            float best = float.PositiveInfinity;

            for (int v = 0; v < positions.Length; v++)
            {
                if (HeaviestBone(weights[v]) != bone) continue;

                result.Dominant++;

                float d = (positions[v] - at).sqrMagnitude;
                if (d < best) { best = d; nearest = v; }
            }

            if (nearest < 0) return result;

            result.NearestBefore = Mathf.Sqrt(best);
            result.NearestAfter = result.NearestBefore;

            float halfLength = pointRest.magnitude;
            if (!Level2 || !(halfLength > 1e-9f)) return result;

            Vector3 local = pointRest / halfLength;
            Vector3 axis = rotation * local;

            float along = Mathf.Clamp(Vector3.Dot(positions[nearest] - centre, axis), halfLength * (1f - MoveHalfLengthShare), halfLength);
            float moved = halfLength - along;
            Vector3 placed = at - axis * moved;

            // The two directions across the axis, in the part's frame turned to the rest frame.
            Vector3 helper = Mathf.Abs(local.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 across1Local = Vector3.Cross(local, helper).normalized;
            Vector3 across1 = rotation * across1Local;
            Vector3 across2 = rotation * Vector3.Cross(local, across1Local).normalized;

            float reach = SectionDepthShare * rest.w;
            float half1 = 0f, half2 = 0f;
            int seen = 0;
            float nearAfter = float.PositiveInfinity;

            for (int v = 0; v < positions.Length; v++)
            {
                if (HeaviestBone(weights[v]) != bone) continue;

                Vector3 offset = positions[v] - centre;
                nearAfter = Mathf.Min(nearAfter, (positions[v] - placed).sqrMagnitude);

                if (Mathf.Abs(Vector3.Dot(offset, axis) - along) > reach) continue;

                half1 = Mathf.Max(half1, Mathf.Abs(Vector3.Dot(offset, across1)));
                half2 = Mathf.Max(half2, Mathf.Abs(Vector3.Dot(offset, across2)));
                seen++;
            }

            float radius = rest.w;

            if (seen >= SectionMinVertices)
            {
                float drawn = RadiusShare * Mathf.Min(half1, half2);
                if (drawn > 1e-6f && drawn < radius) radius = drawn;
                result.Section = seen;
            }

            result.Moved = moved;
            result.NearestAfter = Mathf.Sqrt(nearAfter);
            result.Rest = new Vector4(placed.x, placed.y, placed.z, radius);
            return result;
        }

        /// <summary>Adds one mouth to a skin's log line: the merged index, the bone it skins to, the point, the radius and the distances.</summary>
        public static void AppendSkinMouth(System.Text.StringBuilder line, int merged, int part, SkinMouth m)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            float radius = m.Before.w;

            line.Append(string.Format(inv,
                "; merged {0} -> bone {0} (part {1}), {2} dominant v, point ({3:0.0000} {4:0.0000} {5:0.0000}) r {6:0.0000} m, nearest {7:0.0000} m ({8:0.00} r)",
                merged, part, m.Dominant, m.Before.x, m.Before.y, m.Before.z, radius, m.NearestBefore,
                radius > 0f && m.NearestBefore >= 0f ? m.NearestBefore / radius : -1f));

            if (Level2)
            {
                line.Append(string.Format(inv, ", level 2 moved {0:0.0000} m, nearest {1:0.0000} m, r {2:0.0000} m ({3} v in the section)",
                    m.Moved, m.NearestAfter, m.Rest.w, m.Section));
            }
        }

        /// <summary>The one line per one skin, whenever the dial is on: <c>[Theatre] mouth2 skin: body ID, N merged, K mouths</c> and each mouth.</summary>
        public static void LogSkin(long id, int merged, int mouths, System.Text.StringBuilder line)
        {
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[Theatre] mouth2 skin: body {0}, {1} merged, {2} mouth{3}{4}", id, merged, mouths, mouths == 1 ? "" : "s", line));
        }

        private static readonly System.Collections.Generic.HashSet<Transform> LoggedParts = new System.Collections.Generic.HashSet<Transform>();

        /// <summary>
        /// The one line per eating part drawn alone, once per part object: <c>[Theatre] mouth2 part: body ID, part N, point
        /// (x y z) m, r R m</c>, the point in the part's frame. The body id is read from the root object's name
        /// (<c>Creature ID</c>), or <c>?</c>. A renderer a skin has switched off is not drawn alone and logs nothing.
        /// </summary>
        private static void LogPart(MeshRenderer renderer, Phenotype phenotype, int part)
        {
            if (!renderer.enabled) return;

            Transform host = renderer.transform.parent != null ? renderer.transform.parent : renderer.transform;
            if (!LoggedParts.Add(host)) return;

            Measured m = Measure(phenotype, part);
            if (!m.Valid) return;

            string body = "?";
            for (Transform t = renderer.transform; t != null; t = t.parent)
            {
                if (t.name.StartsWith("Creature", System.StringComparison.Ordinal))
                {
                    string rest = t.name.Substring("Creature".Length).Trim();
                    body = rest.Length > 0 ? rest : "-";
                }
            }

            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[Theatre] mouth2 part: body {0}, part {1}, point ({2:0.0000} {3:0.0000} {4:0.0000}) m, r {5:0.0000} m",
                body, part, m.Point.x, m.Point.y, m.Point.z, m.RadiusMetres));
        }

        // mouth2b:end
        /// <summary>The share of the vertex's weight that a bone carries, over its four influences.</summary>
        public static float BoneShare(BoneWeight bw, int bone) =>
            (bw.boneIndex0 == bone ? bw.weight0 : 0f) + (bw.boneIndex1 == bone ? bw.weight1 : 0f) +
            (bw.boneIndex2 == bone ? bw.weight2 : 0f) + (bw.boneIndex3 == bone ? bw.weight3 : 0f);

        /// <summary>
        /// Turns the keyword on if the body has an eating part, as painting it would (<see cref="Paint"/> switches it on at the
        /// first eating part it reaches). Called for a body left unpainted (EVOSIM_THEATRE_DRESS_NEAR); does nothing with the dial off.
        /// </summary>
        public static void EnsureKeywordFor(Phenotype phenotype)
        {
            if (!Enabled || _keywordOn || phenotype == null) return;

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                if (!MouthPoint.IsEatingPart(phenotype.Parts[i])) continue;

                EnsureKeyword();
                return;
            }
        }

        /// <summary>Turns the shader keyword on, once, for every material that has it. Nothing turns it off.</summary>
        public static void EnsureKeyword()
        {
            if (_keywordOn) return;

            Shader.EnableKeyword(Keyword);
            _keywordOn = true;
            Debug.Log("[Theatre] mouth 2 on: an eating part is drawn with an opening and a lip at its mouth, in place of the " +
                      "sphere mark (shader keyword " + Keyword + ").");

            if (Bite > 0f)
            {
                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[Theatre] mouth 2 bite on: strength {0:0.###}, flash {1:0.###}, swell {2:0.###}; {3}; the timers are written per frame by TheatreMouthBite.",
                    Bite, BiteFlashShare * Bite, 0.25f * Bite,
                    !float.IsNaN(BiteAt) ? "every timer pinned at " + BiteAt.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " s" :
                    BiteTest > 0f ? "a fake onset every " + BiteTest.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " film s on every mouth, and the kill rows of a film window" :
                    "the kill rows of a film window drive the onsets"));

                // The preview's dials (EVOSIM_THEATRE_MOUTH2_BITE_FLASH, _MOUTH2_GAPE, EVOSIM_THEATRE_BITE_CRUMBS): a line of their own,
                // only when one is off its default, so the line above is the bytes it was.
                if (BiteFlash != 1f || Gape > 0f || BiteCrumbs)
                {
                    Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[Theatre] mouth 2 bite look: flash x{0:0.###} (flash {1:0.###}, swell {2:0.###}), gape {3}, crumbs {4}.",
                        BiteFlash, BiteFlashShare * Bite * BiteFlash, 0.25f * Bite * BiteFlash,
                        Gape > 0f ? "open over " + Gape.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " film s before each onset, shut on it" : "off",
                        BiteCrumbs ? "on" : "off"));
                }
            }
            if (Flat > 0f || Relief > 0f || Face || Small > 0f || Plain > 0f)
                Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture, "[Theatre] mouth2 dials: flat {0:0.###} slack {1:0.###}, relief {2:0.###}, face {3}, small {4:0.###}, plain {5:0.###}",
                    Flat, FlatSlack, Relief, Face ? 1 : 0, Small, Plain));
        }

        /// <summary>
        /// True for a mesh with the vertices to hold a cup: the theatre's rounded cube, sphere or cylinder. Not a blade
        /// and not an engine primitive (the raw shapes, a cube of eight vertices).
        /// </summary>
        private static bool MadeByTheatre(MeshRenderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return false;

            // NOBOX: the superellipsoid holds a mouth as the rounded cube does (EVOSIM_THEATRE_NO_BOX).
            return TheatreMeshes.IsRoundedCube(mesh) || mesh == TheatreMeshes.Sphere() || mesh == TheatreMeshes.Cylinder() ||
                   (TheatreNoBox.Enabled && TheatreNoBox.Is(mesh));
        }
    }
}

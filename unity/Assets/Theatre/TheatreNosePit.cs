using UnityEngine;
using Evosim.Core;
using Evosim.Sim;

namespace Evosim.Theatre
{
    /// <summary>
    /// A view dial that draws a nose part with an olfactory pit in the face at its sensing tip (D242, N2: a short
    /// snout with a nostril).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default, and off means nothing happens.</b> <c>EVOSIM_THEATRE_NOSE_PIT</c> is 0 unless set to 0.5 or
    /// more. The palette's one hook is <c>if (TheatreNosePit.Enabled)</c>, so with the dial at 0 no property, keyword or
    /// object is touched. The shader's side is compiled only into the <c>_EVO_NOSE_PIT</c> variant, a
    /// <c>multi_compile</c> keyword that <see cref="Paint"/> turns on the first time it paints a nose and nothing turns
    /// off, so a world with no nose never leaves the shader it had.
    /// </para>
    /// <para>
    /// <b>Which parts.</b> A part whose cell type is <see cref="CellTypeIds.Nose"/>. The nose is drawn at its true size,
    /// as its own part, on its own renderer: the one skin does not merge a nose by default
    /// (<c>EVOSIM_THEATRE_SKIN_BODY</c>), and a nose merged into it is drawn by the skin, which carries no pit below level 2 (<see cref="OnSkin"/>).
    /// </para>
    /// <para>
    /// <b>Where and how big.</b> The pit is centred on <see cref="MouthPoint.Local"/>, the point the world reads the smell
    /// at, so what is drawn is the point the sense is measured from. Its radius is <see cref="RadiusShare"/> of the tip
    /// face's smaller half extent. Its depth is <see cref="DepthLengthShare"/> of the nose's length (twice the tip's
    /// distance from the part's centre), at most <see cref="DepthThicknessShare"/> of that half extent. A sphere takes the
    /// smallest of its three half extents for the face, and a sphere's or a capsule's centre is the tip mapped into the
    /// visual's units, which the drawn ellipsoid can leave a little off its surface; a nose is a box in every design
    /// so far.
    /// </para>
    /// <para>
    /// <b>How it reaches the shader.</b> As <c>_Pit</c> (the tip in the visual's object units, the depth in radii in w)
    /// and <c>_PitRadii</c> (the radius along each object axis), written into the renderer's property block after the
    /// palette's own <c>Set</c>, which reads a block before it writes it and so keeps them. They are the visual's own
    /// units, as <c>TheatrePalette.Pinches</c> maps a joint anchor, divided by its scale: a growth step and a corpse's
    /// shrink move neither. They are written afresh at every paint, for the visual's scale at that paint.
    /// </para>
    /// <para>
    /// <b>No pit</b> is written (both vectors zero) on a raw shape (the runner's X key), on a blade, and on any mesh the
    /// theatre did not make, whose few vertices cannot hold a cup.
    /// </para>
    /// <para>
    /// <b>On the one skin (level 2).</b> With the dial at 2 on <c>EVOSIM_THEATRE_ONE_SKIN=4</c> (<see cref="OnSkin"/>) the nose
    /// is merged into the skin at its true size, and the pit is carved in the skin's vertex stage by the same rules: centred on
    /// <see cref="MouthPoint.Local"/>, <see cref="RadiusShare"/> and the depth rule above, weighted by the nose bone's skinning
    /// weight. The shader's side is under <c>_EVO_NOSE_SKIN</c>, a second keyword that <see cref="EnsureSkinKeyword"/> turns
    /// on when a skin is built with a nose, so level 1 compiles what it did without this level.
    /// </para>
    /// </remarks>
    public static class TheatreNosePit
    {
        // noseskin:begin
        /// <summary>
        /// The dial, read once: 0, the default, draws nothing; 1 draws the pit on a nose part; 2 also joins the nose to the one
        /// skin and carves the pit in the skin (<see cref="OnSkin"/>).
        /// </summary>
        public static readonly float Level = TheatreSkin.Dial("EVOSIM_THEATRE_NOSE_PIT", 0f, 0f, 2f);

        /// <summary>The dial at 1 or 2: the pit on a nose part. Zero draws nothing.</summary>
        public static readonly bool Enabled = Level >= 0.5f;

        /// <summary>
        /// The dial at 2 on a rigged one skin (<c>EVOSIM_THEATRE_ONE_SKIN=4</c>): the nose is merged into the skin
        /// (<c>TheatreOneSkin.RigMerges</c>), its pit is carved there, and the nose has its own tissue (<c>TheatreTissue</c>).
        /// Below that none of it runs and a body is built as it was.
        /// </summary>
        public static readonly bool OnSkin = Level >= 1.5f && TheatreOneSkin.Rigged;

        /// <summary>The shader keyword the pit on the one skin is compiled under, with <see cref="Keyword"/>.</summary>
        public const string SkinKeyword = "_EVO_NOSE_SKIN";
        // noseskin:end

        /// <summary>The shader keyword the pit code in <c>TheatreBody.shader</c> is compiled under.</summary>
        public const string Keyword = "_EVO_NOSE_PIT";

        /// <summary>The pit's radius, as a share of the tip face's smaller half extent.</summary>
        public const float RadiusShare = 0.6f;

        /// <summary>The pit's depth, as a share of the nose's length.</summary>
        public const float DepthLengthShare = 0.5f;

        /// <summary>The most the pit's depth can be, as a share of the tip face's smaller half extent.</summary>
        public const float DepthThicknessShare = 0.8f;

        private static readonly int PitId = Shader.PropertyToID("_Pit");
        private static readonly int PitRadiiId = Shader.PropertyToID("_PitRadii");

        // noseskin:begin
        private static readonly int PitSkinId = Shader.PropertyToID("_PitSkin");
        private static readonly int PitSkinDepthId = Shader.PropertyToID("_PitSkinDepth");

        private static bool _skinKeywordOn;
        // noseskin:end

        private static bool _keywordOn;
        private static MaterialPropertyBlock _block;

        /// <summary>A nose's pit in the part's own frame, metres.</summary>
        public struct Measured
        {
            /// <summary>False for a part with no usable face or length.</summary>
            public bool Valid;

            /// <summary>The sensing tip, <see cref="MouthPoint.Local"/>.</summary>
            public Vector3 Tip;

            public float RadiusMetres;
            public float DepthMetres;
            public float FaceHalfMetres;
            public float LengthMetres;
        }

        /// <summary>
        /// The pit of part <paramref name="part"/> of a body at the size it is drawn at. Pure: no scene, no renderer.
        /// </summary>
        public static Measured Measure(Phenotype phenotype, int part)
        {
            if (phenotype == null || part < 0 || part >= phenotype.PartCount) return default;

            PhenotypePart p = phenotype.Parts[part];
            var half = new Vector3(Mathf.Abs(p.HalfExtents.X), Mathf.Abs(p.HalfExtents.Y), Mathf.Abs(p.HalfExtents.Z));

            Vector3 tip = MouthPoint.Local(phenotype, part).ToVector3();
            float length = 2f * tip.magnitude;

            // The face is the one the tip is the centre of: perpendicular to the axis the tip lies on. A sphere's tip is
            // along any direction, so its face is taken as its smallest half extent.
            float face;
            if (p.ShapeId == ShapeIds.Sphere)
            {
                face = Mathf.Min(half.x, Mathf.Min(half.y, half.z));
            }
            else
            {
                float ax = Mathf.Abs(tip.x), ay = Mathf.Abs(tip.y), az = Mathf.Abs(tip.z);
                if (ax >= ay && ax >= az) face = Mathf.Min(half.y, half.z);
                else if (ay >= az) face = Mathf.Min(half.x, half.z);
                else face = Mathf.Min(half.x, half.y);
            }

            if (!(face > 1e-6f) || !(length > 1e-6f)) return default;

            return new Measured
            {
                Valid = true,
                Tip = tip,
                FaceHalfMetres = face,
                LengthMetres = length,
                RadiusMetres = RadiusShare * face,
                DepthMetres = Mathf.Min(DepthLengthShare * length, DepthThicknessShare * face),
            };
        }

        /// <summary>
        /// Writes the pit of one renderer of a nose part into its property block, or zeroes it. Called by
        /// <see cref="TheatrePalette.Paint"/> for every renderer, after the palette's own <c>Set</c>, and only with the
        /// dial on. Anything but a nose part is left alone.
        /// </summary>
        public static void Paint(MeshRenderer renderer, Phenotype phenotype, int part, string cellType, bool rawShapes)
        {
            if (cellType != CellTypeIds.Nose || renderer == null) return;

            EnsureKeyword();

            var pit = Vector4.zero;
            var radii = Vector4.zero;

            if (!rawShapes && MadeByTheatre(renderer))
            {
                Measured m = Measure(phenotype, part);
                // SNOUT begin
                if (SnoutOn) m = Snouted(m, renderer, phenotype, part);
                // SNOUT end
                Transform visual = renderer.transform;
                Vector3 offset = visual.localPosition;
                Vector3 scale = visual.localScale;

                if (m.Valid &&
                    Mathf.Abs(scale.x) >= 1e-6f && Mathf.Abs(scale.y) >= 1e-6f && Mathf.Abs(scale.z) >= 1e-6f)
                {
                    // The tip in the visual's object units, as the joint anchors are (TheatrePalette.Pinches), and the
                    // radius along each object axis: metres over the visual's size on that axis.
                    pit = new Vector4(
                        (m.Tip.x - offset.x) / scale.x,
                        (m.Tip.y - offset.y) / scale.y,
                        (m.Tip.z - offset.z) / scale.z,
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
            _block.SetVector(PitId, pit);
            _block.SetVector(PitRadiiId, radii);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// True for a mesh with the vertices to hold a cup: the theatre's rounded cube, sphere or cylinder. Not a blade
        /// (a nose drawn flat is a leaf, which gets none) and not an engine primitive (the raw shapes, a cube of eight
        /// vertices).
        /// </summary>
        private static bool MadeByTheatre(MeshRenderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return false;

            // SNOUT begin
            if (SnoutOn && TheatreMeshes.IsSnout(mesh)) return true;
            // SNOUT end
            // NOBOX: the superellipsoid holds a pit as the rounded cube does (EVOSIM_THEATRE_NO_BOX).
            return TheatreMeshes.IsRoundedCube(mesh) || mesh == TheatreMeshes.Sphere() || mesh == TheatreMeshes.Cylinder() ||
                   (TheatreNoBox.Enabled && TheatreNoBox.Is(mesh));
        }

        // noseskin:begin
        /// <summary>
        /// The one skin's pit into its block, at every dress: <c>_PitSkin</c> is the tip in the body's rest metres with the
        /// radius there in w, and <c>_PitSkinDepth</c> the depth in rest metres. The largest part's own <c>_Pit</c>, copied
        /// into the block, is in that part's object units and is read on a part alone. Only with <see cref="OnSkin"/>, and
        /// only for a skin built with a nose.
        /// </summary>
        public static void DressSkin(MaterialPropertyBlock block, Vector4 nose, float depthRestMetres)
        {
            if (block == null) return;

            block.SetVector(PitSkinId, nose);
            block.SetFloat(PitSkinDepthId, depthRestMetres);
        }

        /// <summary>
        /// Turns the pit's keyword and the skin's on, once, for every material that has them. Called when a skin is built with
        /// a nose in it. Nothing turns them off.
        /// </summary>
        public static void EnsureSkinKeyword()
        {
            EnsureKeyword();

            if (_skinKeywordOn) return;

            Shader.EnableKeyword(SkinKeyword);
            _skinKeywordOn = true;
            Debug.Log("[Theatre] nose pit on the one skin: a nose merged into the skin is carved with its pit there " +
                      "(shader keyword " + SkinKeyword + ").");
        }

        /// <summary>Whether the nose skin's keyword is on: set only by a skin built with a nose merged into it.</summary>
        public static bool SkinKeywordOn => _skinKeywordOn;

        /// <summary>Whether the body has a nose part: what <see cref="Paint"/> switches the pit's keyword on at, and a skin may merge.</summary>
        public static bool Carries(Phenotype phenotype)
        {
            if (phenotype == null) return false;

            for (int i = 0; i < phenotype.PartCount; i++)
            {
                if (phenotype.Parts[i].CellTypeId == CellTypeIds.Nose) return true;
            }

            return false;
        }

        /// <summary>
        /// Turns the pit's keyword on if the body has a nose part, as painting it would (<see cref="Paint"/>). Called for a body
        /// left unpainted (EVOSIM_THEATRE_DRESS_NEAR); does nothing with the dial off. The skin's keyword is not set here.
        /// </summary>
        public static void EnsureKeywordFor(Phenotype phenotype)
        {
            if (!Enabled || _keywordOn || !Carries(phenotype)) return;

            EnsureKeyword();
        }
        // noseskin:end

        // SNOUT begin
        /// <summary>
        /// The dial, read once: 0, the default, draws a nose part as it is drawn without it; 1 draws a nose part that stands
        /// apart from the one skin as a snout (<see cref="SnoutOf"/>): narrowing to its tip, flaring into the body, in the
        /// body's own colour. It does not need the pit's dial; with that on too, the pit sits on the narrowed tip.
        /// </summary>
        public static readonly bool SnoutOn = TheatreSkin.Dial("EVOSIM_THEATRE_NOSE_SNOUT", 0f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// How far the skirt's bottom edge reaches past the collider on each cross axis, as a share of that axis's half
        /// width. The skirt lies below the base face, inside the parent (D242: tissue across a hinge).
        /// </summary>
        public const float SnoutFlareShare = 0.5f;

        /// <summary>How deep the skirt runs below the collider's base face, as a share of the nose's smaller half width.</summary>
        public const float SnoutBuryShare = 1f;

        /// <summary>
        /// The most the skirt runs into the parent, as a share of the parent's thickness along its face normal at the joint,
        /// so it never reaches the far side.
        /// </summary>
        public const float SnoutParentShare = 0.4f;

        /// <summary>Where the lightening toward the tip starts, as a share of the collider's length from the base face.</summary>
        public const float SnoutLightenFrom = 0.35f;

        /// <summary>
        /// How much lighter the body colour is at the tip: the shader's colour ramp multiplies it, in linear light, by one
        /// plus this, keeping its hue, along a smoothstep from <see cref="SnoutLightenFrom"/> to the tip.
        /// </summary>
        public const float SnoutLighten = 0.3f;

        /// <summary>
        /// The share of the nose's usual carve depth a snout keeps: the tip is narrow, and the carve is a share of the
        /// part's whole smallest half extent.
        /// </summary>
        public const float SnoutCarveTake = 0.5f;

        private static readonly int SnoutBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SnoutColorId = Shader.PropertyToID("_Color");
        private static readonly int SnoutRimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int SnoutTransTintId = Shader.PropertyToID("_TransTint");
        private static readonly int SnoutPinchAId = Shader.PropertyToID("_PinchA");
        private static readonly int SnoutPinchBId = Shader.PropertyToID("_PinchB");
        private static readonly int SnoutCarveId = Shader.PropertyToID("_Carve");
        private static readonly int SnoutRampAxisId = Shader.PropertyToID("_RampAxis");
        private static readonly int SnoutRampTipId = Shader.PropertyToID("_RampTip");

        private static readonly System.Collections.Generic.HashSet<long> _snoutSaid = new System.Collections.Generic.HashSet<long>();
        private static MaterialPropertyBlock _snoutRead;

        /// <summary>A nose part drawn as a snout, in the part's own frame, metres.</summary>
        public struct SnoutShape
        {
            /// <summary>False for a part that is not a box nose, or has no usable face or length.</summary>
            public bool Valid;

            /// <summary>The cached snout mesh.</summary>
            public Mesh Solid;

            /// <summary>The tip's axis, 0 to 2, and whether the tip is at its high end.</summary>
            public int Axis;
            public bool High;

            public float LengthMetres;

            /// <summary>The collider's smaller cross half width, which is the base half width the snout narrows from.</summary>
            public float BaseHalfMetres;

            /// <summary>The tip's smaller half width.</summary>
            public float TipHalfMetres;

            /// <summary>The skirt's depth below the base face, and the flare past the collider on the smaller side, as drawn.</summary>
            public float SkirtMetres;
            public float FlareMetres;
            public float FlareShare;

            /// <summary>The pit on the narrowed tip.</summary>
            public float PitRadiusMetres;
            public float PitDepthMetres;
        }

        /// <summary>
        /// A box nose as a snout: the tip's axis and end from <see cref="MouthPoint.Local"/> (the face the world reads the
        /// smell through), the cross section from the other two half extents, the skirt and the flare from the parent's
        /// collider. Pure: no scene, no renderer.
        /// </summary>
        /// <remarks>
        /// <b>Where the body's surface is.</b> From the parent's collider face, which is the joint's: the child's
        /// <c>ParentAnchorLocal</c> sits on the parent's face, the dominant axis of the anchor over the parent's half extents
        /// names it, and the parent's thickness along that axis, <c>half + |anchor|</c>, is the room to bury in. The skirt
        /// runs <see cref="SnoutBuryShare"/> of the nose's smaller half width, at most <see cref="SnoutParentShare"/> of that
        /// thickness. The flare is cut so that the larger cross half width times one plus the flare stays inside the parent's
        /// clearance from the anchor to its side faces, so the skirt is inside the parent's collider. A sphere or capsule
        /// parent gets a skirt of at most that share of its diameter and no flare. A root nose has nothing to bury in. The
        /// parent's drawn surface is its carve below its collider face, which this does not know.
        /// </remarks>
        public static SnoutShape SnoutOf(Phenotype phenotype, int part)
        {
            Measured m = Measure(phenotype, part);
            if (!m.Valid) return default;

            PhenotypePart p = phenotype.Parts[part];
            if (p.ShapeId == ShapeIds.Sphere || p.ShapeId == ShapeIds.Capsule) return default;

            var half = new Vector3(Mathf.Abs(p.HalfExtents.X), Mathf.Abs(p.HalfExtents.Y), Mathf.Abs(p.HalfExtents.Z));

            // The face the tip is the centre of, by MouthPoint's rule for a box.
            float ax = Mathf.Abs(m.Tip.x), ay = Mathf.Abs(m.Tip.y), az = Mathf.Abs(m.Tip.z);
            int axis = ax >= ay && ax >= az ? 0 : (ay >= az ? 1 : 2);
            bool high = m.Tip[axis] > 0f;

            float b = half[(axis + 1) % 3], c = half[(axis + 2) % 3];
            float baseHalf = Mathf.Min(b, c), baseWide = Mathf.Max(b, c);
            float length = m.LengthMetres;

            if (!(baseHalf > 1e-6f) || !(length > 1e-6f)) return default;

            float skirt = 0f, flare = 0f;

            if (p.ParentIndex >= 0 && p.ParentIndex < phenotype.PartCount)
            {
                ParentRoom(phenotype.Parts[p.ParentIndex], p.ParentAnchorLocal.ToVector3(), out float room, out float clearance);

                skirt = Mathf.Min(SnoutBuryShare * baseHalf, SnoutParentShare * room);
                flare = Mathf.Clamp(Mathf.Min(SnoutFlareShare, clearance / baseWide - 1f), 0f, SnoutFlareShare);
            }

            float cubicness = Mathf.Min(half.x, Mathf.Min(half.y, half.z)) / Mathf.Max(half.x, Mathf.Max(half.y, half.z));
            Mesh solid = TheatreMeshes.Snout(
                axis, high, TheatreMeshes.PillowFor(cubicness), skirt / length, flare, out float skirtDrawn, out float flareDrawn);

            if (solid == null) return default;

            float tipHalf = TheatreMeshes.SnoutTipShare * baseHalf;

            return new SnoutShape
            {
                Valid = true,
                Solid = solid,
                Axis = axis,
                High = high,
                LengthMetres = length,
                BaseHalfMetres = baseHalf,
                TipHalfMetres = tipHalf,
                SkirtMetres = skirtDrawn * length,
                FlareMetres = flareDrawn * baseHalf,
                FlareShare = flareDrawn,
                PitRadiusMetres = RadiusShare * tipHalf,
                PitDepthMetres = Mathf.Min(DepthLengthShare * length, DepthThicknessShare * tipHalf),
            };
        }

        /// <summary>
        /// The room a parent gives a skirt at a joint anchor: its thickness along the face normal at the anchor
        /// (<paramref name="depth"/>), and the clearance from the anchor to its side faces (<paramref name="clearance"/>, zero
        /// where the parent is not a box, which has no flat face to flare across).
        /// </summary>
        private static void ParentRoom(PhenotypePart parent, Vector3 anchor, out float depth, out float clearance)
        {
            if (parent.ShapeId == ShapeIds.Sphere)
            {
                depth = 2f * SphereShape.Radius(parent.HalfExtents);
                clearance = 0f;
                return;
            }

            if (parent.ShapeId == ShapeIds.Capsule)
            {
                depth = 2f * CapsuleShape.Radius(parent.HalfExtents);
                clearance = 0f;
                return;
            }

            var h = new Vector3(Mathf.Abs(parent.HalfExtents.X), Mathf.Abs(parent.HalfExtents.Y), Mathf.Abs(parent.HalfExtents.Z));

            int face = 0;
            float best = -1f;

            for (int k = 0; k < 3; k++)
            {
                float share = h[k] > 1e-6f ? Mathf.Abs(anchor[k]) / h[k] : 0f;
                if (share > best) { best = share; face = k; }
            }

            depth = h[face] + Mathf.Abs(anchor[face]);
            clearance = float.MaxValue;

            for (int k = 0; k < 3; k++)
            {
                if (k != face) clearance = Mathf.Min(clearance, Mathf.Max(0f, h[k] - Mathf.Abs(anchor[k])));
            }
        }

        /// <summary>True when the renderer draws a snout mesh.</summary>
        public static bool IsSnout(MeshRenderer renderer)
        {
            if (renderer == null) return false;

            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null && TheatreMeshes.IsSnout(filter.sharedMesh);
        }

        /// <summary>
        /// Puts the snout mesh on a nose part's renderer, or puts the rounded cube back when the part no longer takes one.
        /// Called by <see cref="TheatrePalette.Paint"/> for every renderer before the pit's own call, and only with the dial
        /// on. A part that is not a nose, a renderer the skin switched off (a nose merged into it), the raw shapes, and a mesh
        /// that is neither the theatre's rounded cube nor a snout are left alone.
        /// </summary>
        public static void Snout(MeshRenderer renderer, Phenotype phenotype, int part, string cellType, bool rawShapes)
        {
            if (cellType != CellTypeIds.Nose || renderer == null || rawShapes || !renderer.enabled) return;

            var filter = renderer.GetComponent<MeshFilter>();
            Mesh current = filter != null ? filter.sharedMesh : null;

            // NOBOX: a nose part dressed as the superellipsoid still takes its snout (EVOSIM_THEATRE_NO_BOX).
            if (current == null || !(TheatreMeshes.IsRoundedCube(current) || TheatreMeshes.IsSnout(current) ||
                                     (TheatreNoBox.Enabled && TheatreNoBox.Is(current)))) return;

            SnoutShape shape = SnoutOf(phenotype, part);

            if (shape.Valid)
            {
                if (current != shape.Solid)
                {
                    filter.sharedMesh = shape.Solid;
                    renderer.ResetLocalBounds();
                }

                return;
            }

            if (!TheatreMeshes.IsSnout(current)) return;

            Vector3 sides = renderer.transform.localScale;
            float smallest = Mathf.Min(Mathf.Abs(sides.x), Mathf.Min(Mathf.Abs(sides.y), Mathf.Abs(sides.z)));
            float largest = Mathf.Max(Mathf.Abs(sides.x), Mathf.Max(Mathf.Abs(sides.y), Mathf.Abs(sides.z)));

            filter.sharedMesh = TheatreNoBox.Enabled
                ? TheatreNoBox.Mesh()
                : TheatreMeshes.RoundedFor(TheatreMeshes.BuiltinCube(), largest > 1e-6f ? smallest / largest : 1f);
            renderer.ResetLocalBounds();
        }

        /// <summary>
        /// The pit's measure on a snout: the radius, depth and face of the narrowed tip, in place of the collider's face's.
        /// A renderer that draws anything but a snout keeps the measure it was given.
        /// </summary>
        private static Measured Snouted(Measured m, MeshRenderer renderer, Phenotype phenotype, int part)
        {
            if (!IsSnout(renderer)) return m;

            SnoutShape shape = SnoutOf(phenotype, part);
            if (!shape.Valid) return m;

            m.FaceHalfMetres = shape.TipHalfMetres;
            m.RadiusMetres = shape.PitRadiusMetres;
            m.DepthMetres = shape.PitDepthMetres;

            return m;
        }

        /// <summary>
        /// Writes a snout's look into its property block, after the whole body is painted: the joint pinch zero and the carve
        /// at <see cref="SnoutCarveTake"/>; the root's painted colours (body, rim, light through) in place of the nose
        /// tissue's own; and the colour ramp, lighter toward the tip. <paramref name="look"/> is 0 for a body in colour, 1
        /// for a greyed or dead one (the colours follow the root's, no ramp) and 2 for the plain look (neither). Logs one line
        /// per nose.
        /// </summary>
        public static void PaintSnout(
            MeshRenderer nose, MeshRenderer root, Phenotype phenotype, int part, int rootPart, long key, int look)
        {
            if (!IsSnout(nose)) return;

            SnoutShape shape = SnoutOf(phenotype, part);
            if (!shape.Valid) return;

            if (_block == null) _block = new MaterialPropertyBlock();
            if (_snoutRead == null) _snoutRead = new MaterialPropertyBlock();

            nose.GetPropertyBlock(_block);

            // The base is not waisted where it meets the body: the skirt does that work.
            _block.SetVector(SnoutPinchAId, Vector4.zero);
            _block.SetVector(SnoutPinchBId, Vector4.zero);

            Vector4 carve = _block.GetVector(SnoutCarveId);
            carve.w *= SnoutCarveTake;
            _block.SetVector(SnoutCarveId, carve);

            Vector4 hue = Vector4.zero;
            bool taken = false;

            if (look < 2 && root != null)
            {
                root.GetPropertyBlock(_snoutRead);

                if (!_snoutRead.isEmpty)
                {
                    hue = _snoutRead.GetVector(SnoutBaseColorId);

                    if (hue.w > 0.5f)
                    {
                        _block.SetVector(SnoutBaseColorId, hue);
                        _block.SetVector(SnoutColorId, hue);
                        _block.SetVector(SnoutRimColorId, _snoutRead.GetVector(SnoutRimColorId));
                        _block.SetVector(SnoutTransTintId, _snoutRead.GetVector(SnoutTransTintId));
                        taken = true;
                    }
                }
            }

            if (look == 0)
            {
                // The ramp coordinate of an object position p is dot(p, xyz) + w: zero where the lightening starts, one at
                // the tip. The mesh is in the collider's unit coordinates, so the base face is a half from the centre.
                float k = 1f / (1f - SnoutLightenFrom);
                var axis = new Vector4(0f, 0f, 0f, (0.5f - SnoutLightenFrom) * k);
                axis[shape.Axis] = (shape.High ? 1f : -1f) * k;

                float lighter = 1f + SnoutLighten;

                _block.SetVector(SnoutRampAxisId, axis);
                _block.SetVector(SnoutRampTipId, new Vector4(lighter, lighter, lighter, 1f));
            }
            else
            {
                _block.SetVector(SnoutRampAxisId, Vector4.zero);
                _block.SetVector(SnoutRampTipId, Vector4.zero);
            }

            nose.SetPropertyBlock(_block);

            if (look != 0 || !_snoutSaid.Add(key)) return;

            string source;
            string colour = "-";

            if (taken)
            {
                Color.RGBToHSV(((Color)hue).gamma, out float hueTurn, out float saturation, out float value);
                colour = SnoutF(hueTurn * 360f, "0") + " deg (saturation " + SnoutF(saturation, "0.00") + ", value " +
                         SnoutF(value, "0.00") + ")";
                source = "the painted colour of root part " + rootPart + " (" + phenotype.Parts[rootPart].CellTypeId + ")";
            }
            else
            {
                source = "its own tissue, as the root had no paint to read";
            }

            Debug.Log("[Theatre] nose snout: part " + part + ", base half width " + SnoutF(shape.BaseHalfMetres, "0.0000") +
                      " m, tip half width " + SnoutF(shape.TipHalfMetres, "0.0000") + " m, flare past the collider " +
                      SnoutF(shape.FlareMetres, "0.0000") + " m (" + SnoutF(shape.FlareShare, "0.00") +
                      " of the half width) on a skirt " + SnoutF(shape.SkirtMetres, "0.0000") + " m deep, pit radius " +
                      SnoutF(shape.PitRadiusMetres, "0.0000") + " m, hue " + colour + " taken from " + source + ".");
        }

        private static string SnoutF(float value, string format) =>
            value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

        // SNOUT end
        private static void EnsureKeyword()
        {
            if (_keywordOn) return;

            Shader.EnableKeyword(Keyword);
            _keywordOn = true;
            Debug.Log("[Theatre] nose pit on: a nose part is drawn with an olfactory pit at its sensing tip " +
                      "(shader keyword " + Keyword + ").");
        }
    }
}

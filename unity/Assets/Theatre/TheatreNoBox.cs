using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// No creature is drawn as a box (<c>EVOSIM_THEATRE_NO_BOX</c>, the owner's rule of 2026-10-31): the physics stays boxes, and
    /// the drawing of a box part that no skin carries is a superellipsoid instead of the rounded cube.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The dial.</b> 0 (the default), 1 or 2, read once through <see cref="TheatreSkin.Dial"/>. At 0 nothing here runs but the
    /// read: every guard in the palette, the meshes, the nose and mouth, the one skin and the two stills is a test of a static
    /// false, no mesh is made, and every pixel is what it was. At 1: (1) a box part the palette dresses (<c>TheatrePalette.Dress</c>)
    /// takes <see cref="Mesh"/> where it took <c>TheatreMeshes.RoundedFor</c>'s cube, unless it is drawn as a leaf; (2) the one
    /// skin builds for a body of exactly one merged part (<c>TheatreOneSkin.Build</c>); (3) the stills route through the skin
    /// (<c>TheatreSoloStill</c>, <c>SnapshotWorld</c>). The leaf lamina, the sphere, the capsule and the family meshes are not
    /// touched. At 2 (<see cref="EllipsoidAll"/>) also: (4) every box of a skin of any size is the solid inside it, a flat box
    /// that <c>TheatrePalette.DrawnAsLeaf</c> names included unless it is photosynthetic; (5) a nose's snout
    /// (<c>TheatreMeshes.Snout</c>) is taken from <see cref="Mesh"/> in place of the rounded cube. <see cref="SkinExponent"/>
    /// squares the solid inside a skin at levels 1 and 2. At level 2 alone <see cref="JoinShare"/>
    /// (<c>EVOSIM_THEATRE_NO_BOX_JOIN</c>) can also join the ellipsoids across each joint, so that a body of several boxes is
    /// one skin and not ovals that touch at their tips.
    /// </para>
    /// <para>
    /// <b>The solid.</b> The unit superellipsoid <c>|x|^p + |y|^p + |z|^p = 1</c> scaled to the cube of half side
    /// <c>0.5 * TheatreMeshes.Inset</c>, so it drops in where the rounded cube goes and sits inside it as the rounded cube does.
    /// <c>p</c> is <c>EVOSIM_THEATRE_NO_BOX_EXPONENT</c> (2 to 6, default 2.6; 2 is an ellipsoid). The points come from a
    /// latitude and longitude grid through the signed-power parametrisation, which lands on the surface exactly for any
    /// <c>p</c>; the normals are the implicit function's gradient at each point, not an average of faces, so the shading is
    /// smooth across the grid's seam and at its poles. The mesh carries the channels the rounded cube carries and no others:
    /// positions, normals, and the shape channel (<c>TheatreMeshes.Finish</c>) with the box flag one and the half extent
    /// <c>0.5 * Inset</c>, so <c>TheatreBody.shader</c> shapes it as it shapes the box (taper, bend, organic silhouette, soft
    /// tissue), all of it inward, and its final clamp into the mesh's box still has the last word.
    /// </para>
    /// </remarks>
    public static class TheatreNoBox
    {
        /// <summary>
        /// The dial's level: 0 off (the default); 1 the superellipsoid for every box drawn as a part, a skin for one part, and that
        /// skin's box as the ellipsoid inside it; 2 also every box in a skin of several parts as its ellipsoid. Read once.
        /// </summary>
        public static readonly float Level = TheatreSkin.Dial("EVOSIM_THEATRE_NO_BOX", 0f, 0f, 2f);

        /// <summary>True from level 1.</summary>
        public static readonly bool Enabled = Level >= 0.5f;

        /// <summary>True at level 2: a box inside a skin of any size is the ellipsoid inside it, not the drawn rounded box.</summary>
        public static readonly bool EllipsoidAll = Level >= 1.5f;

        /// <summary>
        /// The solid's exponent <c>p</c> (<c>EVOSIM_THEATRE_NO_BOX_EXPONENT</c>, 2 to 6, default 2.6). Read once; 2 is an
        /// ellipsoid, larger is squarer. Only read while <see cref="Enabled"/>.
        /// </summary>
        public static readonly float Exponent = TheatreSkin.Dial("EVOSIM_THEATRE_NO_BOX_EXPONENT", 2.6f, 2f, 6f);

        /// <summary>
        /// The exponent <c>p</c> of the solid a box is inside a skin (<c>EVOSIM_THEATRE_NO_BOX_SKIN_EXPONENT</c>, 2 to 6, default
        /// 2). Read once. 2 is the ellipsoid the skin's field has always held there; larger is squarer.
        /// </summary>
        public static readonly float SkinExponent = TheatreSkin.Dial("EVOSIM_THEATRE_NO_BOX_SKIN_EXPONENT", 2f, 2f, 6f);

        /// <summary>
        /// True from level 1 with <see cref="SkinExponent"/> over 2: a box that is the solid inside a skin
        /// (<c>TheatreOneSkin.NoBoxSolid</c>) is the superellipsoid of that exponent, not the ellipsoid. False at exactly 2, where
        /// the skin takes the ellipsoid path it takes without the dial.
        /// </summary>
        public static readonly bool SkinSuperellipsoid = Enabled && SkinExponent > 2f;

        /// <summary>
        /// <c>EVOSIM_THEATRE_NO_BOX_JOIN</c> (0 to 2, default 0), read once and only at level 2 (<see cref="EllipsoidAll"/>); 0 below
        /// level 2. At every joint between two merged parts of a skin, at least one of which is a box drawn as an ellipsoid by the
        /// no-box rule, the two parts' fields are joined by a smooth union of their own whose radius is this many of the thinner
        /// part's smallest half extent, so that the lobes of a body of several boxes run into one another across the joint, and a
        /// ball of <see cref="JoinBallShare"/> of that radius at the joint's anchor keeps a thin neck from pinching to nothing
        /// (<c>TheatreOneSkin.Field.AddNbJoin</c>). 1 is the strong first take; 2 is the dial's top, for overshoot. At 0 nothing of
        /// it runs: no join is made, no routing changes, and every vertex of every skin is what it is without the dial.
        /// </summary>
        public static readonly float JoinShare = EllipsoidAll ? TheatreSkin.Dial("EVOSIM_THEATRE_NO_BOX_JOIN", 0f, 0f, 2f) : 0f;

        /// <summary>True when <see cref="JoinShare"/> is above 0, which needs level 2.</summary>
        public static readonly bool JoinOn = JoinShare > 0f;

        /// <summary>
        /// The joint ball's radius as a share of the join's blend radius: a half, so that at the dial's top (2) the ball is the
        /// thinner part's smallest half extent, the widest a ball at a face's middle can be and still lie inside both colliders.
        /// </summary>
        public const float JoinBallShare = 0.5f;

        /// <summary>Latitude rings: 49 vertex rows pole to pole.</summary>
        private const int Rings = 48;

        /// <summary>Longitude steps round: 97 vertices a row, the first and last at one place.</summary>
        private const int Sectors = 96;

        private static UnityEngine.Mesh _mesh;

        static TheatreNoBox()
        {
            if (!Enabled) return;

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] EVOSIM_THEATRE_NO_BOX=1: a box part is drawn as a superellipsoid of exponent {0:0.00} " +
                "({1} rings by {2} sectors), and the one skin builds for a body of one merged part",
                Exponent, Rings, Sectors));

            if (SkinSuperellipsoid)
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[Theatre] EVOSIM_THEATRE_NO_BOX_SKIN_EXPONENT={0:0.00}: a box that is the solid inside a skin is a superellipsoid " +
                    "of that exponent", SkinExponent));
            }
        }

        /// <summary>
        /// The shared solid, built on first use: a unit mesh of side one (half extent <c>0.5 * Inset</c>), centred, for the
        /// place the rounded cube of side one goes.
        /// </summary>
        public static UnityEngine.Mesh Mesh() => _mesh != null ? _mesh : (_mesh = Build());

        /// <summary>True for the mesh <see cref="Mesh"/> returns. False for every mesh while the dial is off.</summary>
        public static bool Is(UnityEngine.Mesh mesh) => mesh != null && _mesh != null && mesh == _mesh;

        /// <summary>Drops the mesh. Called when the theatre closes (<see cref="TheatreMeshes.Release"/>).</summary>
        public static void Release()
        {
            if (_mesh == null) { _mesh = null; return; }

            if (Application.isPlaying) UnityEngine.Object.Destroy(_mesh);
            else UnityEngine.Object.DestroyImmediate(_mesh);

            _mesh = null;
        }

        /// <summary>
        /// The visuals hanging off each part transform, by the name every builder here gives them ("Visual"), in child order: the
        /// array <c>TheatreOneSkin.Build</c> takes. For the two reconstructions that never built a skin
        /// (<c>SnapshotWorld</c>, <c>TheatreSoloStill</c>), whose trees do not keep it.
        /// </summary>
        public static Transform[][] VisualsByPart(Transform[] parts)
        {
            var all = new Transform[parts.Length][];
            var found = new List<Transform>();

            for (int i = 0; i < parts.Length; i++)
            {
                found.Clear();

                if (parts[i] != null)
                {
                    foreach (Transform child in parts[i])
                    {
                        if (child != null && child.name == "Visual") found.Add(child);
                    }
                }

                all[i] = found.ToArray();
            }

            return all;
        }

        /// <summary>The sign of v times |v| to the power e (zero for zero).</summary>
        private static double SignedPower(double v, double e) =>
            v == 0.0 ? 0.0 : Math.Sign(v) * Math.Pow(Math.Abs(v), e);

        private static UnityEngine.Mesh Build()
        {
            double p = Exponent;
            double m = 2.0 / p;
            float half = 0.5f * TheatreMeshes.Inset;
            int row = Sectors + 1;

            var vertices = new List<Vector3>((Rings + 1) * row);
            var normals = new List<Vector3>((Rings + 1) * row);
            var triangles = new List<int>(Rings * Sectors * 6);

            for (int i = 0; i <= Rings; i++)
            {
                double phi = -0.5 * Math.PI + Math.PI * i / Rings;
                double cp = SignedPower(Math.Cos(phi), m);
                double sp = SignedPower(Math.Sin(phi), m);

                for (int j = 0; j <= Sectors; j++)
                {
                    double theta = 2.0 * Math.PI * j / Sectors;
                    double x = cp * SignedPower(Math.Cos(theta), m);
                    double y = sp;
                    double z = cp * SignedPower(Math.Sin(theta), m);

                    vertices.Add(new Vector3((float)(half * x), (float)(half * y), (float)(half * z)));

                    // The implicit function's gradient at the point: sign(c) |c|^(p-1) on each axis. Uniform scaling by
                    // half leaves the direction alone, and the visual's own non-uniform scale is Unity's inverse transpose.
                    var gradient = new Vector3(
                        (float)SignedPower(x, p - 1.0), (float)SignedPower(y, p - 1.0), (float)SignedPower(z, p - 1.0));
                    normals.Add(gradient.sqrMagnitude > 1e-20f ? gradient.normalized : Vector3.up);
                }
            }

            for (int i = 0; i < Rings; i++)
            {
                for (int j = 0; j < Sectors; j++)
                {
                    int a = i * row + j;
                    int b = a + 1;
                    int c = a + row;
                    int d = c + 1;

                    AddTriangle(vertices, normals, triangles, a, c, b);
                    AddTriangle(vertices, normals, triangles, b, c, d);
                }
            }

            return TheatreMeshes.Finish("Theatre No-Box Solid", vertices, normals, triangles, 1f);
        }

        /// <summary>
        /// One triangle, wound so that Unity's face normal (the cross of the second and third edges, as the other generated
        /// meshes here are wound) agrees with its vertices' normals. A triangle with no area, which the pole rows make, is left out.
        /// </summary>
        private static void AddTriangle(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, int i0, int i1, int i2)
        {
            Vector3 facing = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
            if (facing.sqrMagnitude < 1e-14f) return;

            if (Vector3.Dot(facing, normals[i0] + normals[i1] + normals[i2]) < 0f)
            {
                int swap = i1; i1 = i2; i2 = swap;
            }

            triangles.Add(i0);
            triangles.Add(i1);
            triangles.Add(i2);
        }
    }

    /// <summary>
    /// The box counter (<c>EVOSIM_THEATRE_BOX_LOG</c>, 0 off by default, 1 on): a log of what a captured frame draws as a box,
    /// and nothing else. It changes no pixel and no state the drawing reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Once per placed capture</b> (<c>SnapshotCamera.CapturePlaced</c> with a path, so a warm-up render is not counted),
    /// for every live view open (<see cref="Register"/>: the fed or synced view, the corpse view, the remains view), one line
    /// <c>box count: frame F (sec S) bodies in view B, bodies with a box N, box parts P, rounded (no-box) parts R</c>. A box
    /// part is a part with an enabled renderer in the frustum whose mesh is the engine's cube or a rounded cube
    /// (<c>TheatreMeshes.IsPrimitiveCube</c>, <c>IsRoundedCube</c>); R counts the parts drawn as the superellipsoid
    /// (<see cref="TheatreNoBox.Mesh"/>), so a run with the rule on and one with it off compare. A body is in view when any
    /// part renderer, or its skin, has bounds in the frustum. A part renderer the one skin switched off is not counted, which is
    /// the point: it is not drawn.
    /// </para>
    /// <para>
    /// <b>Once per boxed part per run</b>: <c>box part: id I part K cell C shape S half-extents (x, y, z) skin ...</c>, with
    /// <c>built</c>, <c>none: reason</c> (the body has no skin) or <c>left out: reason</c> (the part stands apart from one). The
    /// reason is the one skin's own, written to a side record by <c>TheatreOneSkin.Build</c> only while this log is on
    /// (<see cref="Begin"/>).
    /// </para>
    /// </remarks>
    public static class TheatreBoxLog
    {
        /// <summary>The dial: 0 off (the default), 1 on. Read once.</summary>
        public static readonly bool On = TheatreSkin.Dial("EVOSIM_THEATRE_BOX_LOG", 0f, 0f, 1f) >= 0.5f;

        /// <summary>One frame's counts over every view.</summary>
        internal sealed class Tally
        {
            public int Bodies, BoxBodies, BoxParts, RoundedParts;
        }

        /// <summary>Why the one skin left a body or its parts out, as the last build of the body recorded.</summary>
        internal sealed class SkinNote
        {
            /// <summary>Why the body has no skin; null when the skin was built.</summary>
            public string Body;

            /// <summary>Per part: why it stands apart from the skin; null when it is in it (or the body has none).</summary>
            public string[] Part;

            /// <summary>Whether the build returned a skin.</summary>
            public bool Built;
        }

        private static readonly List<LiveWorldView> _views = new List<LiveWorldView>();
        private static readonly Dictionary<long, SkinNote> _notes = new Dictionary<long, SkinNote>();
        private static readonly HashSet<(long, int)> _seen = new HashSet<(long, int)>();
        private static long _frame;

        /// <summary>A view opens: it is scanned at every capture until it is disposed. Only called while <see cref="On"/>.</summary>
        internal static void Register(LiveWorldView view)
        {
            if (view != null && !_views.Contains(view)) _views.Add(view);
        }

        /// <summary>A view closes. Only called while <see cref="On"/>.</summary>
        internal static void Unregister(LiveWorldView view) => _views.Remove(view);

        /// <summary>
        /// The one skin starts a build of a body: a fresh record for it, replacing the last build's. Only called while
        /// <see cref="On"/>.
        /// </summary>
        internal static SkinNote Begin(long id, int parts)
        {
            var note = new SkinNote { Part = new string[parts] };
            _notes[id] = note;
            return note;
        }

        /// <summary>
        /// Counts the frame the camera is about to draw. Called by <c>SnapshotCamera.CapturePlaced</c> after the camera is
        /// placed and before it renders, with a path only. <paramref name="second"/> is NaN when no second is known.
        /// </summary>
        internal static void Frame(Camera camera, double second)
        {
            if (camera == null || _views.Count == 0) return;

            _frame++;

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var tally = new Tally();

            for (int v = 0; v < _views.Count; v++)
            {
                if (_views[v] != null) _views[v].ScanBoxes(planes, tally);
            }

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] box count: frame {0} (sec {1}) bodies in view {2}, bodies with a box {3}, box parts {4}, " +
                "rounded (no-box) parts {5}",
                _frame, double.IsNaN(second) ? "n/a" : second.ToString("0.###", CultureInfo.InvariantCulture),
                tally.Bodies, tally.BoxBodies, tally.BoxParts, tally.RoundedParts));
        }

        /// <summary>
        /// A boxed part is in a frame. The first time this body and part are seen in the run, one detail line.
        /// </summary>
        internal static void PartSeen(long id, int part, PhenotypePart what, bool bodyHasSkin)
        {
            if (!_seen.Add((id, part))) return;

            Float3 h = what.HalfExtents;

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Theatre] box part: id {0} part {1} cell {2} shape {3} half-extents ({4:0.000}, {5:0.000}, {6:0.000}) skin {7}",
                id, part, what.CellTypeId, what.ShapeId, Math.Abs(h.X), Math.Abs(h.Y), Math.Abs(h.Z),
                SkinStatus(id, part, bodyHasSkin)));
        }

        private static string SkinStatus(long id, int part, bool bodyHasSkin)
        {
            if (!TheatreOneSkin.Enabled) return "none: dial off (EVOSIM_THEATRE_ONE_SKIN=0)";
            if (!_notes.TryGetValue(id, out SkinNote note)) return "none: no record (the build was refused before it read the parts)";

            string own = part >= 0 && part < note.Part.Length ? note.Part[part] : null;
            if (own != null) return "left out: " + own;
            if (note.Built && bodyHasSkin) return "built";

            return "none: " + (note.Body ?? "no reason recorded");
        }
    }
}

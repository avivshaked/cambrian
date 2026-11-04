using System.Collections.Generic;
using UnityEngine;

namespace Evosim.Theatre
{
    /// <summary>
    /// The rounded solids the theatre draws bodies with, generated once at start and swapped in
    /// for Unity's primitives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why generated and not baked.</b> Nothing in this repository is a binary asset that
    /// cannot be reviewed in a diff, and a mesh is the easiest thing in a renderer to get subtly
    /// wrong: a rounded cube that is a millimetre larger than the box it stands for is a picture
    /// that lies about size, and a file cannot be argued with. Written as arithmetic, the size
    /// bound is a line anyone can read.
    /// </para>
    /// <para>
    /// <b>The size bound, and it is the reason this class exists.</b> Every mesh here is built
    /// inside a unit solid scaled down by <see cref="Inset"/>, and
    /// <c>PhenotypeBuilder.VisualPlan</c> gives each visual a local scale equal to the part's full
    /// size, so a vertex at <c>Inset * 0.5</c> in object space lands at <c>Inset * h</c> in world
    /// space on that axis. That is the whole of the bound for everything the shader does along a
    /// normal: the first day's outward puff is gone and the carve that replaced it is
    /// <c>-normal * depth * noise</c> with the noise in [0, 1], so no vertex can reach the
    /// collider from inside, let alone pass it. A carved body is a little smaller than its box,
    /// which under-reports rather than over-reports, which is the direction the two constraints
    /// allow.
    /// </para>
    /// <para>
    /// <b>The third day added a deformation that is not along a normal</b>, so the bound is
    /// stated twice now. A box part is tapered and bent in its own object space
    /// (<c>TheatreBody.shader</c>, <c>ShapeBox</c>), and both of those are written to be inward
    /// by construction: the taper multiplies a cross-section by a number at most one, and the
    /// bend shrinks the cross-section by its own amplitude before it displaces by it. The shader
    /// then clamps the object position into the box these meshes were built inside anyway, so
    /// inside-ness does not depend on anyone having checked the arithmetic. The flag that says
    /// which mesh may be deformed is baked here, in <see cref="Finish"/>.
    /// </para>
    /// <para>
    /// <b>Dense on purpose.</b> The meshes are three to twenty times the first day's, because a
    /// displacement is only as fine as the vertices it has to move and the silhouette is the
    /// thing the second day is about. They are shared and generated once, so the cost is per
    /// vertex in the shader rather than per body in memory.
    /// </para>
    /// <para>
    /// <b>Why the rounding is inward.</b> Catlike Coding's rounded cube [CC1] builds the shape by
    /// clamping each vertex into a smaller box and pushing it back out by the roundness, which
    /// keeps every rounded face flush with the original box's faces. That construction is used
    /// here on a box already reduced by <see cref="Inset"/>, so the flat faces sit inside the
    /// collider rather than on it and the corners sit further in still. A rounded cube that bulged
    /// outward, or metaballs, would be the other way round (research/theatre-look, "Skin as
    /// shape").
    /// </para>
    /// </remarks>
    public static class TheatreMeshes
    {
        /// <summary>
        /// How much of the collider a generated mesh is allowed to fill.
        /// </summary>
        /// <remarks>
        /// A hair under one, and the hair is float slack rather than a margin. The first day left
        /// three percent of the half extent unused because the shader pushed each vertex outward
        /// by that much; the carve replaced the puff with a displacement that is never positive
        /// (<c>TheatreBody.shader</c>), so there is nothing left to leave room for and the
        /// remaining half percent is only there so that a face lying exactly on the collider
        /// plane cannot round outward.
        /// </remarks>
        public const float Inset = 0.995f;

        /// <summary>
        /// How much of a half extent the cube's corner rounding eats, as [CC1] measures it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A third rather than the first day's quarter. The carve makes a box's faces wavy and
        /// its edges wander, and in the first carved pictures the thing still saying "built" was
        /// the corner: three planes meeting at a right angle survives any amount of surface
        /// relief. The rounding is inward, so a wider fillet only makes the drawn body smaller,
        /// and it does not touch the ratio between the half extents, which is what makes a box a
        /// fin (StandardShapes' BoxShape remarks).
        /// </para>
        /// <para>
        /// A dial from the third day, <c>EVOSIM_THEATRE_PILLOW</c>, because the owner's reading of
        /// the close views was that the boxes still looked manufactured and the pillowing is the
        /// half of that answer which belongs to the mesh rather than to the shader
        /// (logbook/specs/skin-spec-3.md). Clamped to a half, where the construction below turns
        /// the cube into the ball inscribed in it: that is the roundest a box can be and still be
        /// a box's mesh, and beyond it the arithmetic would start eating the flat faces from the
        /// outside rather than the corners from the inside.
        /// </para>
        /// <para>
        /// The radius is a fraction of the object-space half, which is one number for all three
        /// axes, so on the drawn body it comes out as that fraction of the half extent on each
        /// axis: exactly the fraction of the smallest half extent on the smallest axis, and a
        /// corner that is inside the box on every axis by construction.
        /// </para>
        /// </remarks>
        public static readonly float Pillow =
            TheatreSkin.Dial("EVOSIM_THEATRE_PILLOW", 0.34f, 0f, 0.5f);

        /// <summary>
        /// The cylinder's rim fillet, the second day's 0.34, and it is a constant on purpose.
        /// </summary>
        /// <remarks>
        /// The third day is about the box. A capsule reads as grown already, and the fillet is
        /// load-bearing rather than cosmetic: the carve moves each vertex along its own normal, so
        /// a hard crease tears the mesh open along the rim (BuildCylinder's remarks). Tying it to
        /// a dial aimed at the box would let a picture of a new pillow setting change a capsule
        /// that nobody asked to change.
        /// </remarks>
        public const float Fillet = 0.34f;

        /// <summary>
        /// Vertices along one edge of one cube face.
        /// </summary>
        /// <remarks>
        /// Twenty four, where the first day had eight, and the reason is the carve. A displaced
        /// vertex is the only thing that changes a silhouette, so the impressions can only be as
        /// fine as the mesh they are cut into: at eight segments the finer octave falls below one
        /// sample per wrinkle and comes out as noise on the normals with a straight edge behind
        /// it. Twenty four puts about five vertices across a wrinkle and about thirty across a
        /// lobe. It is shared, generated once and used by every body in the world, so the cost is
        /// per vertex in the shader and not per body in memory.
        /// </remarks>
        private const int CubeSegments = 24;

        /// <summary>
        /// How many times the icosahedron is subdivided. Four gives 5,120 faces.
        /// </summary>
        /// <remarks>
        /// Chosen to match the cube's density rather than by eye: 24 segments a face is 6,912
        /// triangles, and four subdivisions is 5,120, so a sphere and a box of the same size
        /// carry impressions of about the same size. Two subdivisions, the first day's, is 320.
        /// </remarks>
        private const int SphereSubdivisions = 4;

        /// <summary>Sides around a cylinder.</summary>
        private const int CylinderSides = 28;

        /// <summary>
        /// Rings along the cylinder's straight section, and out along each cap.
        /// </summary>
        /// <remarks>
        /// The first day's cylinder had one quad from end to end, which carves to nothing: a
        /// displacement field can only move the vertices it is given, and two rings of them
        /// describe a straight tube whatever the field says. The caps are fans and are given
        /// rings for the same reason.
        /// </remarks>
        private const int CylinderRings = 14;

        private const int CylinderCapRings = 5;

        /// <summary>
        /// The rounding a near-cubic box keeps, as a fraction of <see cref="Pillow"/>. A slab or a
        /// rod is rounded by the whole pillow; a box whose three sides are nearly equal is
        /// rounded by this much of it, because a cube with a third of each half eaten by fillets
        /// reads as a ball, and a ball is a different part (the owner's observation of
        /// 2026-09-13, HANDOFF item 11; built in the look's pass, 2026-09-16).
        /// <c>EVOSIM_THEATRE_CUBE_PILLOW</c>, 0 to 1, default 0.35.
        /// </summary>
        public static readonly float CubePillowFraction =
            TheatreSkin.Dial("EVOSIM_THEATRE_CUBE_PILLOW", 0.35f, 0f, 1f);

        /// <summary>The cubes, one per rounding bucket (<see cref="PillowFor"/>).</summary>
        private static readonly Dictionary<int, Mesh> _cubes = new Dictionary<int, Mesh>();
        private static Mesh _sphere;
        private static Mesh _cylinder;
        private static Mesh _lamina;

        /// <summary>
        /// Whether a flat box part is drawn as a leaf (<see cref="Lamina"/>).
        /// <c>EVOSIM_THEATRE_LEAVES</c>, 1 by default; 0 draws every box as the rounded cube, as
        /// before 2026-09-24.
        /// </summary>
        public static readonly bool Leaves = TheatreSkin.Dial("EVOSIM_THEATRE_LEAVES", 1f, 0f, 1f) >= 0.5f;

        /// <summary>
        /// How thin a box has to be to be drawn as a leaf: its smallest side over the next one.
        /// A slab is a leaf; a rod, whose two small sides are alike, is not.
        /// </summary>
        public const float LeafThinness = 0.4f;

        /// <summary>Stations along a leaf, base to tip.</summary>
        private const int LeafStations = 96;

        /// <summary>Rows across one face of a leaf, rim to rim.</summary>
        private const int LeafRows = 16;

        /// <summary>
        /// A unit cube of side one, rounded by the whole pillow, inset, centred on the origin.
        /// Replaces Unity's Cube for a part that is not near-cubic.
        /// </summary>
        public static Mesh RoundedCube() => RoundedCube(Pillow);

        /// <summary>The same cube rounded by a given fraction of its half, built once per bucket.</summary>
        public static Mesh RoundedCube(float pillow)
        {
            int bucket = Bucket(pillow);
            if (_cubes.TryGetValue(bucket, out Mesh cube) && cube != null) return cube;

            cube = BuildRoundedCube(bucket / 100f);
            _cubes[bucket] = cube;
            return cube;
        }

        /// <summary>
        /// The rounding for a box by how cubic it is: its smallest half extent over its largest,
        /// one for a cube and towards zero for a slab or a rod. The full pillow up to 0.55, the
        /// capped one from 0.85, a straight blend between. The ratio does not change as a body
        /// grows, so the mesh chosen at birth is the mesh for life.
        /// </summary>
        public static float PillowFor(float cubicness)
        {
            float t = Mathf.Clamp01((cubicness - 0.55f) / 0.30f);
            return Pillow * Mathf.Lerp(1f, CubePillowFraction, t);
        }

        /// <summary>Whole percents, so the blend above makes a handful of meshes and not one per body.</summary>
        private static int Bucket(float pillow) => Mathf.Clamp(Mathf.RoundToInt(pillow * 100f / 4f) * 4, 0, 50);

        private static Mesh _builtinCube, _builtinSphere, _builtinCylinder;

        /// <summary>The engine's own cube, sphere and cylinder meshes, as the views borrow them.</summary>
        public static Mesh BuiltinCube() => _builtinCube != null ? _builtinCube : (_builtinCube = Resources.GetBuiltinResource<Mesh>("Cube.fbx"));
        public static Mesh BuiltinSphere() => _builtinSphere != null ? _builtinSphere : (_builtinSphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx"));
        public static Mesh BuiltinCylinder() => _builtinCylinder != null ? _builtinCylinder : (_builtinCylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"));

        /// <summary>
        /// True for the engine's cube, sphere or cylinder, by reference or by either name the
        /// engine gives it (Cube, Sphere and Cylinder from <c>CreatePrimitive</c>; Cube, pSphere1
        /// and pCylinder1 from <c>GetBuiltinResource</c>). See <see cref="RoundedFor(Mesh, float)"/>.
        /// </summary>
        public static bool IsPrimitiveCube(Mesh mesh) =>
            mesh != null && (mesh == BuiltinCube() || mesh.name == "Cube" || mesh.name == "pCube1");

        public static bool IsPrimitiveSphere(Mesh mesh) =>
            mesh != null && (mesh == BuiltinSphere() || mesh.name == "Sphere" || mesh.name == "pSphere1");

        public static bool IsPrimitiveCylinder(Mesh mesh) =>
            mesh != null && (mesh == BuiltinCylinder() || mesh.name == "Cylinder" || mesh.name == "pCylinder1");

        /// <summary>True for any cube this class built, whatever its rounding.</summary>
        public static bool IsRoundedCube(Mesh mesh)
        {
            if (mesh == null) return false;
            foreach (Mesh cube in _cubes.Values) if (cube == mesh) return true;
            return false;
        }

        /// <summary>A unit sphere of diameter one, smooth, inset. Replaces Unity's Sphere.</summary>
        public static Mesh Sphere() => _sphere != null ? _sphere : (_sphere = BuildSphere());

        /// <summary>
        /// A cylinder of diameter one and height two, inset, matching Unity's Cylinder so that
        /// <c>VisualPlan</c>'s scale for a capsule shaft still means what it meant.
        /// </summary>
        public static Mesh Cylinder() => _cylinder != null ? _cylinder : (_cylinder = BuildCylinder());

        /// <summary>
        /// A leaf: a sheet of vertices laid out by where they are on a leaf rather than where
        /// they are in space, which <c>TheatreBody.shader</c> shapes per body. See
        /// <see cref="BuildLamina"/>.
        /// </summary>
        public static Mesh Lamina() => _lamina != null ? _lamina : (_lamina = BuildLamina());

        /// <summary>True for the leaf mesh.</summary>
        public static bool IsLamina(Mesh mesh) => mesh != null && mesh == _lamina;

        /// <summary>
        /// True when a box of these three sides is drawn as a leaf: the thinnest side under
        /// <see cref="LeafThinness"/> of the next.
        /// </summary>
        public static bool LeafShaped(Vector3 sides)
        {
            float a = Mathf.Abs(sides.x), b = Mathf.Abs(sides.y), c = Mathf.Abs(sides.z);
            float small = Mathf.Min(a, Mathf.Min(b, c));
            float large = Mathf.Max(a, Mathf.Max(b, c));
            float middle = a + b + c - small - large;
            return middle > 1e-6f && small <= LeafThinness * middle;
        }

        /// <summary>
        /// A leaf's bounds in its own object units: the unit cube, and on the thinnest axis the
        /// curl as well, which is the one thing drawn outside the collider
        /// (<c>TheatreBody.shader</c>, <c>ShapeLamina</c>). Without it the engine would cull a leaf
        /// whose box is off screen and whose curl is not.
        /// </summary>
        public static Bounds LeafBounds(Vector3 sides, float curlFraction)
        {
            var abs = new Vector3(Mathf.Abs(sides.x), Mathf.Abs(sides.y), Mathf.Abs(sides.z));

            // The thinnest axis the way the shader picks it: x first on a tie, then y.
            int thin = abs.x <= abs.y && abs.x <= abs.z ? 0 : (abs.y <= abs.z ? 1 : 2);
            float width = Mathf.Min(abs[(thin + 1) % 3], abs[(thin + 2) % 3]);

            Vector3 extents = new Vector3(0.5f, 0.5f, 0.5f);
            if (abs[thin] > 1e-6f) extents[thin] += Mathf.Max(0f, curlFraction) * width / abs[thin];

            // Above 1 the bend with the current leans a blade's free end by up to half its half length, in metres and
            // on two axes, outside its box (TheatreBody.shader, AlgaBendReach), so the bounds take that margin too.
            if (TheatreSkin.AlgaBendStrength > 1f) extents += new Vector3(0.3f, 0.3f, 0.3f);

            // ARCROOM begin
            // The arc's room (EVOSIM_THEATRE_ALGA_ARC_ROOM): the bend can stand that share of the blade's length outside the box on the
            // thin axis, so the bounds grow by it each side (object units: a share of the longest side over the thin side).
            if (TheatreSkin.AlgaArcRoom > 0f && abs[thin] > 1e-6f)
                extents[thin] += TheatreSkin.AlgaArcRoom * Mathf.Max(abs.x, Mathf.Max(abs.y, abs.z)) / abs[thin];
            // ARCROOM end
            return new Bounds(Vector3.zero, 2f * extents);
        }

        /// <summary>
        /// The rounded stand-in for one of Unity's primitives, matched by name, or null when the
        /// mesh is not one this class replaces.
        /// </summary>
        /// <remarks>
        /// By reference to the engine's built in Cube, Sphere and Cylinder, and by the names they
        /// carry, because the theatre must not reach into <c>Evosim.Sim</c> to be told which is
        /// which. <c>PhenotypeBuilder.PrimitiveMesh</c> takes them from <c>CreatePrimitive</c>,
        /// where they are called Cube, Sphere and Cylinder; the snapshot and live views take them
        /// from <c>Resources.GetBuiltinResource</c>, where the sphere and the cylinder are called
        /// pSphere1 and pCylinder1 (Unity 6000.5). Matched by name alone, from the look pass of
        /// 2026-09-16 until 2026-09-23, every sphere and capsule in a snapshot or a live render
        /// kept the engine's mesh, was never squashed to its half-extents and never got its joint
        /// pinch, and a capsule's shaft stood out of its carved caps as a thin disc that flashed
        /// when the body tumbled (the bulge in round 46's first films). A mesh that is none of
        /// the three is left alone, so a part drawn with something new goes on being drawn with
        /// it rather than being silently replaced by a cube.
        /// </remarks>
        public static Mesh RoundedFor(Mesh primitive) => RoundedFor(primitive, 0f);

        /// <summary>
        /// The same, with the part's cubicness (<see cref="PillowFor"/>) choosing the cube's
        /// rounding. A cube this class already built is re-bucketed, so a body dressed again
        /// after the raw shapes were shown gets the right cube back.
        /// </summary>
        public static Mesh RoundedFor(Mesh primitive, float cubicness)
        {
            if (primitive == null) return null;
            if (IsRoundedCube(primitive)) return RoundedCube(PillowFor(cubicness));

            if (IsPrimitiveCube(primitive)) return RoundedCube(PillowFor(cubicness));
            if (IsPrimitiveSphere(primitive)) return Sphere();
            if (IsPrimitiveCylinder(primitive)) return Cylinder();
            return null;
        }

        /// <summary>
        /// The engine's own primitive for one of this class's meshes, for the raw-shapes key:
        /// the collider's shape, drawn as the physics has it, with no rounding, carve, taper or
        /// bend. Null for a mesh this class did not make.
        /// </summary>
        public static Mesh PrimitiveFor(Mesh rounded)
        {
            if (rounded == null) return null;
            if (IsRoundedCube(rounded) || IsPrimitiveCube(rounded)) return BuiltinCube();
            if (rounded == _sphere || IsPrimitiveSphere(rounded)) return BuiltinSphere();
            if (rounded == _cylinder || IsPrimitiveCylinder(rounded)) return BuiltinCylinder();
            if (rounded == _lamina) return BuiltinCube();
            // NOBOX begin
            if (TheatreNoBox.Enabled && TheatreNoBox.Is(rounded)) return BuiltinCube();
            // NOBOX end
            // THALLI begin
            if (IsThallus(rounded)) return BuiltinCube();
            // THALLI end
            // SNOUT begin
            if (IsSnout(rounded)) return BuiltinCube();
            // SNOUT end
            return null;
        }

        /// <summary>Drops the generated meshes. Called when the theatre closes.</summary>
        public static void Release()
        {
            foreach (int bucket in new List<int>(_cubes.Keys))
            {
                Mesh cube = _cubes[bucket];
                Discard(ref cube);
            }

            _cubes.Clear();
            Discard(ref _sphere);
            Discard(ref _cylinder);
            Discard(ref _lamina);
            // THALLI begin
            ReleaseThalli();
            // THALLI end
            // SNOUT begin
            ReleaseSnouts();
            // SNOUT end
            // FAMILYMESH begin
            if (TheatreFamilyMesh.Enabled) TheatreFamilyMesh.Release();
            // FAMILYMESH end
            // NOBOX begin
            if (TheatreNoBox.Enabled) TheatreNoBox.Release();
            // NOBOX end
        }

        private static void Discard(ref Mesh mesh)
        {
            if (mesh == null) { mesh = null; return; }

            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);

            mesh = null;
        }

        // ---------------------------------------------------------------- the cube

        private static Mesh BuildRoundedCube(float pillow)
        {
            float half = 0.5f * Inset;
            float radius = Mathf.Clamp(pillow, 0f, 0.5f) * half;
            float flat = half - radius;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Six faces, each a grid. The shared edges carry duplicate vertices, which costs a
            // few hundred bytes and cannot show as a seam: the rounding below is a function of
            // position alone, so two vertices at one place are given one normal.
            AddCubeFace(vertices, normals, triangles, Vector3.right, Vector3.up, Vector3.forward, half);
            AddCubeFace(vertices, normals, triangles, Vector3.left, Vector3.up, Vector3.back, half);
            AddCubeFace(vertices, normals, triangles, Vector3.up, Vector3.forward, Vector3.right, half);
            AddCubeFace(vertices, normals, triangles, Vector3.down, Vector3.back, Vector3.right, half);
            AddCubeFace(vertices, normals, triangles, Vector3.forward, Vector3.up, Vector3.left, half);
            AddCubeFace(vertices, normals, triangles, Vector3.back, Vector3.up, Vector3.right, half);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];

                // [CC1]'s construction: clamp the vertex into the box the rounding leaves behind,
                // then push it back out by the radius along the direction it was clamped from.
                var inner = new Vector3(
                    Mathf.Clamp(v.x, -flat, flat),
                    Mathf.Clamp(v.y, -flat, flat),
                    Mathf.Clamp(v.z, -flat, flat));

                Vector3 outward = v - inner;
                float length = outward.magnitude;

                Vector3 normal = length > 1e-6f ? outward / length : Vector3.up;

                vertices[i] = inner + normal * radius;
                normals[i] = normal;
            }

            return Finish("Theatre Rounded Cube", vertices, normals, triangles, 1f);
        }

        private static void AddCubeFace(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 normal, Vector3 up, Vector3 right, float half)
        {
            int start = vertices.Count;
            const int n = CubeSegments;

            for (int y = 0; y <= n; y++)
            {
                for (int x = 0; x <= n; x++)
                {
                    float u = 2f * x / n - 1f;
                    float v = 2f * y / n - 1f;

                    vertices.Add(half * (normal + right * u + up * v));
                    normals.Add(normal);
                }
            }

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int a = start + y * (n + 1) + x;
                    int b = a + 1;
                    int c = a + (n + 1);
                    int d = c + 1;

                    // Wound so that Unity's own face normal, the cross of the second and third
                    // edges, comes out along the face's outward direction. It depends on the
                    // handedness of (right, up, normal), which is why the six calls above do not
                    // all pass the same pair of axes.
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
        }

        // ---------------------------------------------------------------- the leaf

        /// <summary>
        /// The leaf's sheet: two faces of <see cref="LeafStations"/> by <see cref="LeafRows"/>,
        /// each vertex carrying where it is on a leaf in its fifth UV channel.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why the shape is not in the mesh.</b> A leaf's outline, its cross-section and its
        /// curl differ from body to body (drawn from the carve's seed), and which of the box's
        /// three axes is its length, its width and its thickness differs from part to part. One
        /// mesh per combination would be thousands, so the mesh carries (s, t, side): s from the
        /// base (0) to the tip (1), t from rim (-1) to rim (1), and which face. The shader builds
        /// the leaf from those (<c>TheatreBody.shader</c>, <c>ShapeLamina</c>). The positions baked
        /// here are a plain oval, for the bounds and for a shader that does not know the flag.
        /// </para>
        /// <para>
        /// <b>Rows crowd the rim</b>: t is the sine of an even step, so the rows close up where
        /// the cross-section thins to the edge and the silhouette is drawn from most of them.
        /// The two faces meet at the rim, where both have no thickness; they share no vertex,
        /// and a leaf's edge is a crease, which is what a leaf's edge is.
        /// </para>
        /// <para>
        /// <b>Winding.</b> The canonical frame is x the length (s runs towards -x), y the
        /// thickness and z the width. For a triangle (p0, p1, p2) the cube's faces pass the test
        /// <c>Cross(p1 - p0, p2 - p0)</c> outward; with s decreasing x and t increasing z, the
        /// order (a, c, b), (b, c, d) is outward on the +y face and (a, b, c), (b, d, c) on the
        /// -y face. The shader keeps the frame a rotation of this one (its width axis is the
        /// cross of the length and the thickness), so the winding holds on every part.
        /// </para>
        /// </remarks>
        private static Mesh BuildLamina()
        {
            float half = 0.5f * Inset;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var leaf = new List<Vector4>();
            var triangles = new List<int>();

            int stride = LeafRows + 1;

            for (int face = 0; face < 2; face++)
            {
                float side = face == 0 ? 1f : -1f;
                int start = vertices.Count;

                for (int i = 0; i <= LeafStations; i++)
                {
                    float s = (float)i / LeafStations;
                    float w = Mathf.Sin(Mathf.PI * s);

                    for (int j = 0; j <= LeafRows; j++)
                    {
                        float step = 2f * j / LeafRows - 1f;
                        float t = Mathf.Sin(0.5f * Mathf.PI * step);
                        float thick = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * Mathf.Sqrt(w);

                        vertices.Add(new Vector3(half * (1f - 2f * s), side * half * thick, half * t * w));
                        normals.Add(new Vector3(0f, side, 0f));
                        leaf.Add(new Vector4(s, t, side, 0f));
                    }
                }

                for (int i = 0; i < LeafStations; i++)
                {
                    for (int j = 0; j < LeafRows; j++)
                    {
                        int a = start + i * stride + j;
                        int b = a + 1;
                        int c = a + stride;
                        int d = c + 1;

                        if (side > 0f)
                        {
                            triangles.Add(a); triangles.Add(c); triangles.Add(b);
                            triangles.Add(b); triangles.Add(c); triangles.Add(d);
                        }
                        else
                        {
                            triangles.Add(a); triangles.Add(b); triangles.Add(c);
                            triangles.Add(b); triangles.Add(d); triangles.Add(c);
                        }
                    }
                }
            }

            Mesh mesh = Finish("Theatre Lamina", vertices, normals, triangles, 2f);
            mesh.SetUVs(4, leaf);

            // The shader turns the sheet onto whichever axes the part has, so any axis may carry
            // the length: the bounds are the whole unit cube, and a part's curl is added to them
            // per renderer (LeafBounds).
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);

            return mesh;
        }

        // ---------------------------------------------------------------- the sphere

        /// <summary>
        /// A subdivided icosahedron rather than Unity's latitude and longitude sphere.
        /// </summary>
        /// <remarks>
        /// Even triangles, no pole, and no seam where the wrap meets. A creature's part is a
        /// sphere at any orientation, so a sphere with a pole shows its pole about as often as
        /// not, and a rim shader draws every crowded pole triangle as a bright spot.
        /// </remarks>
        private static Mesh BuildSphere()
        {
            float radius = 0.5f * Inset;
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;

            var vertices = new List<Vector3>
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f),
                new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t),
                new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f),
                new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f),
            };

            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;

            for (int pass = 0; pass < SphereSubdivisions; pass++)
            {
                var next = new List<int>(triangles.Count * 4);
                var midpoints = new Dictionary<long, int>();

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];

                    int ab = Midpoint(vertices, midpoints, a, b);
                    int bc = Midpoint(vertices, midpoints, b, c);
                    int ca = Midpoint(vertices, midpoints, c, a);

                    next.Add(a); next.Add(ab); next.Add(ca);
                    next.Add(b); next.Add(bc); next.Add(ab);
                    next.Add(c); next.Add(ca); next.Add(bc);
                    next.Add(ab); next.Add(bc); next.Add(ca);
                }

                triangles = next;
            }

            var normals = new List<Vector3>(vertices.Count);

            for (int i = 0; i < vertices.Count; i++)
            {
                normals.Add(vertices[i]);
                vertices[i] = vertices[i] * radius;
            }

            return Finish("Theatre Sphere", vertices, normals, triangles, 0f);
        }

        private static int Midpoint(List<Vector3> vertices, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

            if (cache.TryGetValue(key, out int found)) return found;

            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            cache[key] = vertices.Count - 1;

            return vertices.Count - 1;
        }

        // ---------------------------------------------------------------- the cylinder

        /// <summary>
        /// A cylinder built as a lathe of a rounded profile, so its rim is a fillet and not a
        /// crease.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why the rim is rounded, and why that is not decoration.</b> The carve moves every
        /// vertex along its own normal. At a hard crease the two surfaces meeting there have two
        /// different normals, so one vertex of the pair goes sideways and the other goes down and
        /// the mesh tears open along the rim. The rounded cube has never had this problem because
        /// [CC1]'s construction gives a position, and therefore a normal, that is continuous
        /// everywhere; a lathe of the same kind of profile does it for the cylinder.
        /// </para>
        /// <para>
        /// <b>Why a lathe rather than a grid.</b> The rim needs several rings inside a fillet a
        /// tenth of the height, and the flat parts do not; a profile walked in three pieces puts
        /// the vertices where the curvature is, which a uniform grid of the same count does not.
        /// </para>
        /// <para>
        /// Everything stays inside the cylinder of radius <c>0.5 * Inset</c> and half height
        /// <c>Inset</c> that <c>VisualPlan</c> sized the visual for: the fillet is cut from the
        /// solid, never added to it.
        /// </para>
        /// </remarks>
        private static Mesh BuildCylinder()
        {
            float radius = 0.5f * Inset;
            float half = Inset;
            float fillet = Fillet * Mathf.Min(radius, half);

            float flatR = radius - fillet;
            float flatY = half - fillet;

            // The profile, from the top pole down the outside to the bottom pole, as position in
            // (r, y) and the surface normal there in the same plane.
            var profile = new List<Vector4>();

            void At(float r, float y, float nr, float ny)
            {
                var n = new Vector2(nr, ny).normalized;
                profile.Add(new Vector4(r, y, n.x, n.y));
            }

            for (int i = 0; i <= CylinderCapRings; i++)
            {
                At(flatR * i / CylinderCapRings, half, 0f, 1f);
            }

            // The top fillet, a quarter arc from facing up to facing out.
            for (int i = 1; i <= CylinderCapRings; i++)
            {
                float angle = 0.5f * Mathf.PI * i / CylinderCapRings;
                float nr = Mathf.Sin(angle);
                float ny = Mathf.Cos(angle);

                At(flatR + fillet * nr, flatY + fillet * ny, nr, ny);
            }

            for (int i = 1; i <= CylinderRings; i++)
            {
                At(radius, Mathf.Lerp(flatY, -flatY, (float)i / CylinderRings), 1f, 0f);
            }

            for (int i = 1; i <= CylinderCapRings; i++)
            {
                float angle = 0.5f * Mathf.PI * i / CylinderCapRings;
                float nr = Mathf.Cos(angle);
                float ny = -Mathf.Sin(angle);

                At(flatR + fillet * nr, -flatY + fillet * ny, nr, ny);
            }

            for (int i = 1; i <= CylinderCapRings; i++)
            {
                At(flatR * (1f - (float)i / CylinderCapRings), -half, 0f, -1f);
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            int stride = CylinderSides + 1;

            for (int p = 0; p < profile.Count; p++)
            {
                Vector4 point = profile[p];

                for (int i = 0; i <= CylinderSides; i++)
                {
                    float angle = 2f * Mathf.PI * i / CylinderSides;
                    float c = Mathf.Cos(angle);
                    float s = Mathf.Sin(angle);

                    vertices.Add(new Vector3(c * point.x, point.y, s * point.x));
                    normals.Add(new Vector3(c * point.z, point.w, s * point.z));
                }
            }

            for (int p = 0; p < profile.Count - 1; p++)
            {
                for (int i = 0; i < CylinderSides; i++)
                {
                    int a = p * stride + i;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;

                    // Wound so the outward face is the one seen: the same order the first day's
                    // cylinder side used, with the profile's next ring standing in for its
                    // bottom vertex.
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(c); triangles.Add(b); triangles.Add(d);
                }
            }

            return Finish("Theatre Cylinder", vertices, normals, triangles, 0f);
        }

        // THALLI begin
        // ---------------------------------------------------------------- the thallus

        /// <summary>
        /// The three thalli a compact producer is drawn as under <c>EVOSIM_THEATRE_THALLI</c> (<see cref="TheatreSkin.ThalliOn"/>):
        /// sea lettuce, peacock's tail and dead man's fingers.
        /// </summary>
        public enum ThallusForm { Ulva = 0, Padina = 1, Codium = 2 }

        /// <summary>
        /// The solid flag a thallus mesh carries in UV channel three (<see cref="Finish"/>): 0.2, 0.25 or 0.3. Under a half, so
        /// every test the shader had before this dial reads a thallus as neither the box, the blade nor the skin, and only the
        /// dial's own lines tell the three apart (<c>TheatreBody.shader</c>, <c>ThalliFlag*</c>; the two must move together).
        /// </summary>
        public static float ThallusFlag(ThallusForm form) => 0.2f + 0.05f * (int)form;

        /// <summary>
        /// The compact test (<see cref="ThallusShaped"/>): the smallest side over the middle one above this. At or under it the
        /// box keeps the blade path; the blade's own flat test (<see cref="LeafThinness"/>, 0.4) is stricter, so a plump slab
        /// between the two stays a plump blade.
        /// </summary>
        public const float ThalliFlattest = 0.5f;

        /// <summary>The compact test's other half: the middle side over the largest at least this. Under it a box is a strap and keeps the blade.</summary>
        public const float ThalliSlenderest = 0.3f;

        /// <summary>
        /// The form rule (<see cref="ThallusFormOf"/>): the middle side over the largest under this is a long box, drawn as
        /// Codium; otherwise the smallest over the middle under <see cref="ThalliSquat"/> is a squat box with one short axis,
        /// drawn as Padina; otherwise the box is near-cubic, drawn as Ulva.
        /// </summary>
        public const float ThalliLong = 0.65f;

        /// <summary>See <see cref="ThalliLong"/>.</summary>
        public const float ThalliSquat = 0.75f;

        /// <summary>
        /// Within this of either threshold the part's hash moves the line, so a population near it mixes two forms rather than
        /// splitting on the last digit of a side. Fixed per part, so a body keeps its form from frame to frame and run to run.
        /// </summary>
        public const float ThalliBand = 0.05f;

        /// <summary>How far an anchor must sit off the box's middle, in object units (the face is at 0.5), to put the base there.</summary>
        private const float ThalliAnchorFace = 0.2f;

        /// <summary>How much of the box's half extent, on each axis, a thallus reaches at most.</summary>
        private const float ThalliFill = 0.97f;

        /// <summary>
        /// A size class is the box's three sides over its largest, each rounded to this step (at least one step): one cached
        /// mesh per form, variant, growth axis, base end and size class.
        /// </summary>
        private const float ThalliAspectStep = 0.1f;

        /// <summary>At most this many thallus meshes are cached; a part that would need one more keeps the blade (counted in the log).</summary>
        public const int ThalliMeshCap = 96;

        /// <summary>The most a thallus's free end may sway, in object units, before the room each vertex has cuts it.</summary>
        private const float ThalliSwayWant = 0.04f;

        /// <summary>The sway's weight up the thallus: its height share to this power, so the base holds still.</summary>
        private const float ThalliSwayBend = 1.5f;

        /// <summary>The sway's lag from base to tip, in cycles, so the thallus waves rather than tilts.</summary>
        private const float ThalliWaveLag = 0.15f;

        // Ulva: sheets from one base, each a grid of stations base to tip and rows rim to rim, two faces.
        private const int UlvaStations = 16;
        private const int UlvaRows = 18;
        private const float UlvaLeanLeast = 0.35f;     // each sheet's lean off the growth axis, radians
        private const float UlvaLeanMost = 0.8f;
        private const float UlvaWidth = 0.55f;         // the half width at the widest, over the sheet's length
        private const float UlvaWidthBase = 0.15f;     // the half width at the base, over the widest
        private const float UlvaWidest = 0.65f;        // where along the sheet it is widest
        private const float UlvaTipRound = 0.3f;       // how much shorter the sheet is at its rims than on its midline
        private const float UlvaRuffle = 0.22f;        // the margin's ruffle out of the sheet, over the half width
        private const float UlvaCrumple = 0.08f;       // the whole sheet's crumple, over the half width
        private const float UlvaCup = 0.25f;           // the sheet's cup across, over the half width
        private const float UlvaHalfThick = 0.03f;     // the half thickness on the midline, over the sheet's length
        private const float UlvaBaseDark = 0.2f;       // the tone lost at the base
        private const float UlvaThinMiddle = 0.45f;    // the thinness on the midline; one at the margin

        // Padina: one funnel slit down one side, or two fans, from one base; rings from the stalk out, columns across.
        private const int PadinaRings = 28;
        private const int PadinaColumnsFunnel = 40;
        private const int PadinaColumnsFan = 22;
        private const float PadinaStalk = 0.05f;       // the radius the sheet starts at, over the funnel's
        private const float PadinaFunnelSpan = 2.75f;  // half the funnel's sweep round its axis, radians (the slit is the rest)
        private const float PadinaFanSpan = 1.25f;     // half a fan's sweep
        private const float PadinaLobeDepth = 0.08f;   // the margin's lobes, over the radius
        private const float PadinaRollFrom = 0.8f;     // where the inrolled rim starts, over the radius
        private const float PadinaRoll = 0.1f;         // how far the rim rolls in toward the axis, over the radius
        private const float PadinaRollLift = 0.06f;    // and up
        private const float PadinaRuffle = 0.035f;     // the margin's wave, over the radius
        private const float PadinaHalfThick = 0.02f;   // the half thickness at the stalk, over the radius
        private const int PadinaBands = 6;             // the concentric growth bands, stalk to rim
        private const float PadinaBandDepth = 0.14f;   // the tone a band's dark ring loses
        private const float PadinaLimeBase = 0.12f;    // the lime over the whole sheet
        private const float PadinaLimeRim = 0.6f;      // and at the rim
        private const float PadinaThin = 0.3f;         // the sheet's thinness: little light through a limed sheet

        // Codium: even cylinders forking in twos, each a capsule.
        private const float CodiumFork = 0.5f;         // half the angle between two daughters, radians
        private const float CodiumShorten = 0.8f;      // a daughter's length over its mother's
        private const float CodiumBow = 0.12f;         // a segment's bow, over its length
        private const float CodiumRadiusFew = 0.16f;   // the radius over the box's smaller cross half extent, three levels
        private const float CodiumRadiusMany = 0.12f;  // and four
        private const float CodiumToneBase = 0.85f;    // the tone at the base; one at the tips

        private static readonly Dictionary<int, Mesh> _thalli = new Dictionary<int, Mesh>();
        private static readonly HashSet<Mesh> _thallusMeshes = new HashSet<Mesh>();
        private static readonly HashSet<long> _thalliSeen = new HashSet<long>();
        private static readonly int[] _thalliForms = new int[3];
        private static int _thalliVertices;
        private static int _thalliOverCap;
        private static bool _thalliHooked;
        private static bool _thalliSaid;

        /// <summary>True for a thallus mesh.</summary>
        public static bool IsThallus(Mesh mesh) => mesh != null && _thallusMeshes.Contains(mesh);

        /// <summary>
        /// The compact test: a box of these sides is drawn as a thallus when its smallest side is over
        /// <see cref="ThalliFlattest"/> of the middle one (not flat) and its middle side at least <see cref="ThalliSlenderest"/>
        /// of the largest (not a strap).
        /// </summary>
        public static bool ThallusShaped(Vector3 sides)
        {
            Sorted(sides, out float large, out float middle, out float small, out _, out _);
            return large > 1e-6f && middle > 1e-6f && small > ThalliFlattest * middle && middle >= ThalliSlenderest * large;
        }

        /// <summary>The form rule (<see cref="ThalliLong"/>), its thresholds moved within <see cref="ThalliBand"/> by the part's hash.</summary>
        public static ThallusForm ThallusFormOf(Vector3 sides, uint hash)
        {
            Sorted(sides, out float large, out float middle, out float small, out _, out _);
            float shift = ThalliBand * (2f * Unit(hash, 1) - 1f);

            if (middle < (ThalliLong + shift) * large) return ThallusForm.Codium;
            if (small < (ThalliSquat + shift) * middle) return ThallusForm.Padina;
            return ThallusForm.Ulva;
        }

        /// <summary>
        /// A part's hash, from things stable on it: the body plan's carve seed, the part's index and its genome half extents.
        /// The same on every frame and in every run, and the same for every body of one plan.
        /// </summary>
        public static uint ThallusHash(float planSeed, int part, Vector3 halfExtents)
        {
            uint h = Mix(0x9E3779B9u ^ (uint)part);
            h = Mix(h ^ Bits(planSeed));
            h = Mix(h ^ Bits(halfExtents.x));
            h = Mix(h ^ Bits(halfExtents.y));
            return Mix(h ^ Bits(halfExtents.z));
        }

        /// <summary>
        /// The thallus for a part, or null when it is not compact or the cache is full: its form by the form rule, its growth
        /// axis (Codium's the longest, Padina's the shortest, Ulva's the anchor's or the hash's), its base at the anchor's end
        /// when the anchor sits off the middle on that axis and at the hash's end otherwise, and one of two variants by the hash.
        /// </summary>
        /// <param name="sides">The visual's local scale, the part's full sides.</param>
        /// <param name="anchor">The part's own joint anchor in object units, w one when it hangs from a parent.</param>
        public static Mesh Thallus(Vector3 sides, Vector4 anchor, uint hash, out ThallusForm form)
        {
            form = ThallusForm.Ulva;
            if (!ThallusShaped(sides)) return null;

            form = ThallusFormOf(sides, hash);
            Sorted(sides, out float large, out _, out _, out int longest, out int shortest);

            int axis;
            if (form == ThallusForm.Codium) axis = longest;
            else if (form == ThallusForm.Padina) axis = shortest;
            else if (anchor.w > 0.5f) axis = Mathf.Abs(anchor.x) >= Mathf.Abs(anchor.y) && Mathf.Abs(anchor.x) >= Mathf.Abs(anchor.z) ? 0 : (Mathf.Abs(anchor.y) >= Mathf.Abs(anchor.z) ? 1 : 2);
            else axis = (int)((hash >> 20) % 3u);

            float at = anchor.w > 0.5f ? anchor[axis] : 0f;
            bool baseHigh = Mathf.Abs(at) > ThalliAnchorFace ? at > 0f : ((hash >> 24) & 1u) == 1u;
            int variant = (int)((hash >> 16) & 1u);

            int qx = Quantise(Mathf.Abs(sides.x) / large);
            int qy = Quantise(Mathf.Abs(sides.y) / large);
            int qz = Quantise(Mathf.Abs(sides.z) / large);

            int key = (int)form | (variant << 2) | (axis << 3) | ((baseHigh ? 1 : 0) << 5) | (qx << 6) | (qy << 10) | (qz << 14);

            if (_thalli.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;

            if (_thalli.Count >= ThalliMeshCap)
            {
                _thalliOverCap++;
                return null;
            }

            var e = new Vector3(qx, qy, qz) * ThalliAspectStep;
            mesh = BuildThallus(form, e, axis, baseHigh, variant, Mix((uint)key ^ 0x5bd1e995u));

            _thalli[key] = mesh;
            _thallusMeshes.Add(mesh);
            _thalliVertices += mesh.vertexCount;

            return mesh;
        }

        /// <summary>Counts one part drawn as a thallus, once per body and part, for the run's line (<see cref="SayThalli"/>).</summary>
        public static void CountThallus(int body, int part, ThallusForm form)
        {
            if (!_thalliHooked)
            {
                _thalliHooked = true;
                Application.quitting += SayThalli;
            }

            if (_thalliSeen.Add(((long)body << 32) | (uint)part)) _thalliForms[(int)form]++;
        }

        /// <summary>
        /// The run's one line under the dial: how many parts took each form, the meshes cached and their vertices, and how many
        /// parts kept the blade because the cache was full. Said once, when the theatre closes or the application quits.
        /// </summary>
        public static void SayThalli()
        {
            if (_thalliSaid || !TheatreSkin.ThalliOn) return;
            _thalliSaid = true;

            Debug.Log("[Theatre] thalli: " + (_thalliForms[0] + _thalliForms[1] + _thalliForms[2]) + " parts drawn as thalli, " +
                      _thalliForms[0] + " Ulva, " + _thalliForms[1] + " Padina, " + _thalliForms[2] + " Codium; " +
                      _thallusMeshes.Count + " meshes cached, " + _thalliVertices + " vertices; " +
                      _thalliOverCap + " kept the blade with the cache full.");
        }

        private static void ReleaseThalli()
        {
            SayThalli();

            foreach (Mesh thallus in new List<Mesh>(_thallusMeshes))
            {
                Mesh m = thallus;
                Discard(ref m);
            }

            _thallusMeshes.Clear();
            _thalli.Clear();
            _thalliVertices = 0;
        }

        private static int Quantise(float share) => Mathf.Clamp(Mathf.RoundToInt(share / ThalliAspectStep), 1, Mathf.RoundToInt(1f / ThalliAspectStep));

        private static void Sorted(Vector3 sides, out float large, out float middle, out float small, out int longest, out int shortest)
        {
            float a = Mathf.Abs(sides.x), b = Mathf.Abs(sides.y), c = Mathf.Abs(sides.z);
            large = Mathf.Max(a, Mathf.Max(b, c));
            small = Mathf.Min(a, Mathf.Min(b, c));
            middle = a + b + c - large - small;
            longest = a >= b && a >= c ? 0 : (b >= c ? 1 : 2);
            shortest = a <= b && a <= c ? 0 : (b <= c ? 1 : 2);
        }

        private static uint Bits(float f) => System.BitConverter.ToUInt32(System.BitConverter.GetBytes(f), 0);

        private static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }

        /// <summary>A number in [0, 1] from a hash and a salt.</summary>
        private static float Unit(uint hash, int salt) => Mix(hash ^ ((uint)salt * 0x9E3779B9u)) / 4294967295f;

        private static float Smooth(float low, float high, float x)
        {
            float t = Mathf.Clamp01((x - low) / (high - low));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// A thallus mesh, built in a canonical frame (x and z across, y the growth axis, the base at the bottom) inside a box of
        /// half extents <paramref name="e"/> on the object axes (the largest one), then turned onto the object axes and scaled
        /// into the unit box. Each form fits its centre lines or mid-surfaces into the box less its own thickness on every axis,
        /// so every vertex is inside <see cref="ThalliFill"/> of the box before the clamp in <see cref="FinishThallus"/>.
        /// </summary>
        private static Mesh BuildThallus(ThallusForm form, Vector3 e, int axis, bool baseHigh, int variant, uint seed)
        {
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            var box = new Vector3(e[axisA], e[axis], e[axisB]);
            var build = new ThallusBuild();

            if (form == ThallusForm.Ulva) BuildUlva(build, box, variant, seed);
            else if (form == ThallusForm.Padina) BuildPadina(build, box, variant, seed);
            else BuildCodium(build, box, variant, seed);

            return FinishThallus(build, form, e, axis, baseHigh);
        }

        private sealed class ThallusBuild
        {
            public readonly List<Vector3> Position = new List<Vector3>();
            public readonly List<Vector3> Normal = new List<Vector3>();
            public readonly List<Color> Tissue = new List<Color>();
            public readonly List<float> Height = new List<float>();
            public readonly List<int> Triangles = new List<int>();
        }

        /// <summary>
        /// The per-axis scale and centre that fit points into the canonical box less a margin on every side, so a surface within
        /// the margin of the fitted points is inside the box's <see cref="ThalliFill"/>.
        /// </summary>
        private static Vector3 FitScale(List<Vector3> points, Vector3 box, float margin, out Vector3 centre)
        {
            Vector3 min = points[0], max = points[0];
            foreach (Vector3 p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }

            centre = 0.5f * (min + max);
            Vector3 half = 0.5f * (max - min);
            var scale = Vector3.one;

            for (int i = 0; i < 3; i++)
            {
                float room = Mathf.Max(0f, box[i] * ThalliFill - margin);
                scale[i] = half[i] > 1e-5f ? room / half[i] : 0f;
            }

            return scale;
        }

        /// <summary>
        /// Fits sheets' mid-surfaces into the box: a first fit with no margin gives the thickness's scale (the cube root of the
        /// three axis scales), and the second keeps that much thickness clear of every face. The mid-surfaces are mapped in place;
        /// the scale the half thicknesses take is returned.
        /// </summary>
        private static float FitSheets(List<Vector3[,]> mids, float maxHalf, Vector3 box)
        {
            var all = new List<Vector3>();
            foreach (Vector3[,] mid in mids) foreach (Vector3 p in mid) all.Add(p);

            Vector3 s0 = FitScale(all, box, 0f, out _);
            float thick = Mathf.Pow(Mathf.Max(1e-9f, s0.x * s0.y * s0.z), 1f / 3f);
            Vector3 s = FitScale(all, box, maxHalf * thick, out Vector3 centre);

            foreach (Vector3[,] mid in mids)
            {
                for (int i = 0; i < mid.GetLength(0); i++)
                    for (int j = 0; j < mid.GetLength(1); j++)
                        mid[i, j] = Vector3.Scale(mid[i, j] - centre, s);
            }

            return thick;
        }

        /// <summary>
        /// Two faces on a mapped mid-surface, each offset along the surface normal by the half thickness, which is zero on every
        /// rim so the two faces meet there. Both faces of a point carry the mid-point's height, so they sway together.
        /// </summary>
        private static void AddSheet(ThallusBuild b, Vector3[,] mid, float[,] half, Color[,] tissue)
        {
            int nu = mid.GetLength(0), nv = mid.GetLength(1);
            var normal = new Vector3[nu, nv];

            for (int i = 0; i < nu; i++)
            {
                for (int j = 0; j < nv; j++)
                {
                    Vector3 du = mid[Mathf.Min(i + 1, nu - 1), j] - mid[Mathf.Max(i - 1, 0), j];
                    Vector3 dv = mid[i, Mathf.Min(j + 1, nv - 1)] - mid[i, Mathf.Max(j - 1, 0)];
                    Vector3 n = Vector3.Cross(du, dv);
                    normal[i, j] = n.sqrMagnitude > 1e-14f ? n.normalized : (j > 0 ? normal[i, j - 1] : Vector3.up);
                }
            }

            for (int face = 0; face < 2; face++)
            {
                float side = face == 0 ? 1f : -1f;
                int start = b.Position.Count;

                for (int i = 0; i < nu; i++)
                {
                    for (int j = 0; j < nv; j++)
                    {
                        b.Position.Add(mid[i, j] + normal[i, j] * (side * half[i, j]));
                        b.Normal.Add(normal[i, j] * side);
                        b.Tissue.Add(tissue[i, j]);
                        b.Height.Add(mid[i, j].y);
                    }
                }

                for (int i = 0; i < nu - 1; i++)
                {
                    for (int j = 0; j < nv - 1; j++)
                    {
                        int a = start + i * nv + j;
                        Quad(b, a, a + 1, a + nv, a + nv + 1);
                    }
                }
            }
        }

        /// <summary>Two triangles of a quad; <see cref="FinishThallus"/> winds every triangle by its normals.</summary>
        private static void Quad(ThallusBuild b, int a, int bb, int c, int d)
        {
            b.Triangles.Add(a); b.Triangles.Add(c); b.Triangles.Add(bb);
            b.Triangles.Add(bb); b.Triangles.Add(c); b.Triangles.Add(d);
        }

        /// <summary>
        /// Ulva: three or four broad sheets from one base, each leaning out its own way, widest two thirds up and rounded at the
        /// tip, cupped, crumpled and ruffled hardest at the margin, thin to nothing at every rim.
        /// </summary>
        private static void BuildUlva(ThallusBuild b, Vector3 box, int variant, uint seed)
        {
            int sheets = variant == 0 ? 3 : 4;
            float turn = 2f * Mathf.PI * Unit(seed, 1);

            var mids = new List<Vector3[,]>();
            var halves = new List<float[,]>();
            var tissues = new List<Color[,]>();
            float maxHalf = 0f;

            for (int k = 0; k < sheets; k++)
            {
                float psi = turn + 2f * Mathf.PI * k / sheets + 0.5f * (Unit(seed, 10 + k) - 0.5f);
                float beta = Mathf.Lerp(UlvaLeanLeast, UlvaLeanMost, Unit(seed, 20 + k));
                float length = Mathf.Lerp(0.8f, 1f, Unit(seed, 30 + k));
                float p1 = 2f * Mathf.PI * Unit(seed, 40 + k);
                float p2 = 2f * Mathf.PI * Unit(seed, 50 + k);
                float p3 = 2f * Mathf.PI * Unit(seed, 60 + k);

                var radial = new Vector3(Mathf.Cos(psi), 0f, Mathf.Sin(psi));
                var across = new Vector3(-Mathf.Sin(psi), 0f, Mathf.Cos(psi));
                float sinb = Mathf.Sin(beta), cosb = Mathf.Cos(beta);

                var mid = new Vector3[UlvaStations + 1, UlvaRows + 1];
                var half = new float[UlvaStations + 1, UlvaRows + 1];
                var tissue = new Color[UlvaStations + 1, UlvaRows + 1];

                for (int i = 0; i <= UlvaStations; i++)
                {
                    float u = (float)i / UlvaStations;

                    for (int j = 0; j <= UlvaRows; j++)
                    {
                        float v = Mathf.Sin(0.5f * Mathf.PI * (2f * j / UlvaRows - 1f));
                        float s = u * (1f - UlvaTipRound * v * v);

                        Vector3 spine = length * (cosb * s * Vector3.up + sinb * Mathf.Pow(s, 1.4f) * radial);
                        Vector3 tangent = (cosb * Vector3.up + sinb * 1.4f * Mathf.Pow(Mathf.Max(s, 1e-3f), 0.4f) * radial).normalized;
                        Vector3 face = Vector3.Cross(tangent, across).normalized;

                        float w = length * UlvaWidth * (UlvaWidthBase + (1f - UlvaWidthBase) * Mathf.Sin(0.5f * Mathf.PI * Mathf.Min(1f, s / UlvaWidest)));
                        float edge = Mathf.Max(Mathf.Abs(v), Smooth(0.55f, 1f, s));

                        float ruffle = w * edge * edge * UlvaRuffle *
                                       (Mathf.Sin(2f * Mathf.PI * (3.1f * s + 1.7f * v) + p1) + 0.5f * Mathf.Sin(2f * Mathf.PI * (5.3f * s - 2.9f * v) + p2)) +
                                       w * UlvaCrumple * s * Mathf.Sin(2f * Mathf.PI * (1.6f * s + 1.1f * v) + p3);
                        float cup = UlvaCup * w * v * v;

                        mid[i, j] = spine + across * (v * w) + face * (ruffle + cup);
                        half[i, j] = length * UlvaHalfThick * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)));
                        maxHalf = Mathf.Max(maxHalf, half[i, j]);

                        float tone = 1f - UlvaBaseDark * (1f - s) * (1f - s);
                        tissue[i, j] = new Color(tone, 0f, UlvaThinMiddle + (1f - UlvaThinMiddle) * edge * edge, 1f);
                    }
                }

                mids.Add(mid);
                halves.Add(half);
                tissues.Add(tissue);
            }

            float thick = FitSheets(mids, maxHalf, box);

            for (int k = 0; k < sheets; k++)
            {
                float[,] half = halves[k];
                for (int i = 0; i < half.GetLength(0); i++)
                    for (int j = 0; j < half.GetLength(1); j++)
                        half[i, j] *= thick;

                AddSheet(b, mids[k], half, tissues[k]);
            }
        }

        /// <summary>
        /// Padina: a trumpet funnel slit down one side, or two fans, opening from one stalk, with lobes and a wave at the margin,
        /// the rim rolled in toward the axis, concentric growth bands in the tone and lime toward the rim.
        /// </summary>
        private static void BuildPadina(ThallusBuild b, Vector3 box, int variant, uint seed)
        {
            int fans = variant == 0 ? 1 : 2;
            int columns = variant == 0 ? PadinaColumnsFunnel : PadinaColumnsFan;
            float span = variant == 0 ? PadinaFunnelSpan : PadinaFanSpan;
            float open0 = variant == 0 ? 0.45f : 0.75f;
            float open1 = variant == 0 ? 1.0f : 1.25f;
            float turn = 2f * Mathf.PI * Unit(seed, 1);

            var mids = new List<Vector3[,]>();
            var halves = new List<float[,]>();
            var tissues = new List<Color[,]>();
            float maxHalf = 0f;

            for (int f = 0; f < fans; f++)
            {
                float centre = turn + Mathf.PI * f + 0.3f * (Unit(seed, 10 + f) - 0.5f);
                int lobes = 5 + Mathf.FloorToInt(3f * Unit(seed, 20 + f) - 1e-4f);
                float phaseL = 2f * Mathf.PI * Unit(seed, 30 + f);
                float phaseR = 2f * Mathf.PI * Unit(seed, 40 + f);

                var mid = new Vector3[PadinaRings + 1, columns + 1];
                var half = new float[PadinaRings + 1, columns + 1];
                var tissue = new Color[PadinaRings + 1, columns + 1];

                for (int i = 0; i <= PadinaRings; i++)
                {
                    float rho = PadinaStalk + (1f - PadinaStalk) * i / PadinaRings;

                    for (int j = 0; j <= columns; j++)
                    {
                        float tau = 2f * j / columns - 1f;
                        float theta = centre + span * tau;
                        float reach = 1f - PadinaLobeDepth * (0.5f + 0.5f * Mathf.Cos(lobes * (theta - centre) + phaseL));
                        float r = rho * reach;
                        float open = open0 + (open1 - open0) * rho;
                        var radial = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));

                        Vector3 p = r * (Mathf.Cos(open) * Vector3.up + Mathf.Sin(open) * radial);
                        float q = Smooth(PadinaRollFrom, 1f, rho);
                        p += reach * q * q * (PadinaRoll * -radial + PadinaRollLift * Vector3.up);
                        p += Vector3.up * (PadinaRuffle * rho * rho * Mathf.Sin(7f * theta + phaseR));

                        mid[i, j] = p;
                        half[i, j] = PadinaHalfThick * Mathf.Sqrt(Mathf.Max(0f, 1f - rho * rho * rho * rho)) *
                                     Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(tau, 8f))) *
                                     Mathf.Sqrt(Mathf.Clamp01((rho - PadinaStalk) / 0.1f));
                        maxHalf = Mathf.Max(maxHalf, half[i, j]);

                        float band = 1f - PadinaBandDepth * (0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * PadinaBands * rho));
                        float lime = PadinaLimeBase + (PadinaLimeRim - PadinaLimeBase) * Smooth(0.84f, 1f, rho);
                        tissue[i, j] = new Color(band, lime, PadinaThin, 1f);
                    }
                }

                mids.Add(mid);
                halves.Add(half);
                tissues.Add(tissue);
            }

            float thick = FitSheets(mids, maxHalf, box);

            for (int f = 0; f < fans; f++)
            {
                float[,] half = halves[f];
                for (int i = 0; i < half.GetLength(0); i++)
                    for (int j = 0; j < half.GetLength(1); j++)
                        half[i, j] *= thick;

                AddSheet(b, mids[f], half, tissues[f]);
            }
        }

        private struct CodiumSegment
        {
            public Vector3 From, Bow, To;
            public int Level;
        }

        /// <summary>
        /// Codium: a trunk that forks in two, three or four times, each fork in a plane a quarter turn from its mother's, every
        /// segment a capsule of one radius along a bowed centre line. The centre lines are fitted into the box less the radius,
        /// so every capsule is inside.
        /// </summary>
        private static void BuildCodium(ThallusBuild b, Vector3 box, int variant, uint seed)
        {
            int levels = variant == 0 ? 3 : 4;
            int rings = variant == 0 ? 8 : 7;
            int sides = variant == 0 ? 12 : 10;
            float radiusShare = variant == 0 ? CodiumRadiusFew : CodiumRadiusMany;

            var segments = new List<CodiumSegment>();
            int salt = 0;

            void Grow(Vector3 from, Vector3 dir, Vector3 plane, int level, float length)
            {
                salt++;
                Vector3 to = from + dir * length;
                Vector3 bow = from + dir * (0.5f * length) + plane * (length * CodiumBow * (2f * Unit(seed, 100 + salt) - 1f));
                segments.Add(new CodiumSegment { From = from, Bow = bow, To = to, Level = level });

                if (level + 1 >= levels) return;

                float fork = Mathf.Rad2Deg * (CodiumFork + 0.15f * (Unit(seed, 200 + salt) - 0.5f));
                Vector3 left = (Quaternion.AngleAxis(fork, plane) * dir).normalized;
                Vector3 right = (Quaternion.AngleAxis(-fork, plane) * dir).normalized;

                // Each daughter forks in the plane through it at right angles to its mother's fork.
                Grow(to, left, Vector3.Cross(left, plane).normalized, level + 1, length * CodiumShorten);
                Grow(to, right, Vector3.Cross(right, plane).normalized, level + 1, length * CodiumShorten);
            }

            // The trunk leans a little off the growth axis in the plane of its first fork; z is across that plane.
            float lean = 0.25f * (Unit(seed, 1) - 0.5f);
            Grow(Vector3.zero, new Vector3(Mathf.Sin(lean), Mathf.Cos(lean), 0f), Vector3.forward, 0, 1f);

            var centres = new List<Vector3[]>();
            var all = new List<Vector3>();
            foreach (CodiumSegment segment in segments)
            {
                var line = new Vector3[rings];
                for (int k = 0; k < rings; k++)
                {
                    float t = (float)k / (rings - 1);
                    line[k] = (1f - t) * (1f - t) * segment.From + 2f * (1f - t) * t * segment.Bow + t * t * segment.To;
                    all.Add(line[k]);
                }
                centres.Add(line);
            }

            float radius = Mathf.Min(radiusShare * Mathf.Min(box.x, box.z), 0.3f * box.y) * ThalliFill;
            Vector3 scale = FitScale(all, box, radius, out Vector3 centre);

            for (int s = 0; s < segments.Count; s++)
            {
                Vector3[] line = centres[s];
                for (int k = 0; k < line.Length; k++) line[k] = Vector3.Scale(line[k] - centre, scale);

                float tone = CodiumToneBase + (1f - CodiumToneBase) * segments[s].Level / Mathf.Max(1, levels - 1);
                AddCapsule(b, line, radius, sides, new Color(tone, 0f, 0f, 1f));
            }
        }

        /// <summary>
        /// A capsule of one radius along a centre line: rings at the line's points, framed by parallel transport, and a
        /// hemisphere at each end. Every vertex is within the radius of a point of the line. A ring's vertices carry its centre's
        /// height, and a cap's its end's, so a ring sways as one.
        /// </summary>
        private static void AddCapsule(ThallusBuild b, Vector3[] line, float radius, int sides, Color tissue)
        {
            const int capRings = 4;
            int count = line.Length;

            var tangents = new Vector3[count];
            for (int k = 0; k < count; k++)
            {
                Vector3 t = line[Mathf.Min(k + 1, count - 1)] - line[Mathf.Max(k - 1, 0)];
                tangents[k] = t.sqrMagnitude > 1e-14f ? t.normalized : Vector3.up;
            }

            Vector3 n0 = Vector3.Cross(tangents[0], Mathf.Abs(tangents[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var across = new Vector3[count];
            across[0] = n0;
            for (int k = 1; k < count; k++)
            {
                Vector3 n = across[k - 1] - tangents[k] * Vector3.Dot(across[k - 1], tangents[k]);
                across[k] = n.sqrMagnitude > 1e-14f ? n.normalized : across[k - 1];
            }

            // The rings, base pole to tip pole: (centre, radius, axis, across, normal tilt along the axis, height).
            var ringCentre = new List<Vector3>();
            var ringRadius = new List<float>();
            var ringAxis = new List<Vector3>();
            var ringAcross = new List<Vector3>();
            var ringTilt = new List<float>();
            var ringHeight = new List<float>();

            for (int c = 1; c < capRings; c++)
            {
                float angle = 0.5f * Mathf.PI * c / capRings;
                ringCentre.Add(line[0] - tangents[0] * (radius * Mathf.Cos(angle)));
                ringRadius.Add(radius * Mathf.Sin(angle));
                ringAxis.Add(tangents[0]); ringAcross.Add(across[0]);
                ringTilt.Add(-Mathf.Cos(angle)); ringHeight.Add(line[0].y);
            }

            for (int k = 0; k < count; k++)
            {
                ringCentre.Add(line[k]); ringRadius.Add(radius);
                ringAxis.Add(tangents[k]); ringAcross.Add(across[k]);
                ringTilt.Add(0f); ringHeight.Add(line[k].y);
            }

            for (int c = capRings - 1; c >= 1; c--)
            {
                float angle = 0.5f * Mathf.PI * c / capRings;
                ringCentre.Add(line[count - 1] + tangents[count - 1] * (radius * Mathf.Cos(angle)));
                ringRadius.Add(radius * Mathf.Sin(angle));
                ringAxis.Add(tangents[count - 1]); ringAcross.Add(across[count - 1]);
                ringTilt.Add(Mathf.Cos(angle)); ringHeight.Add(line[count - 1].y);
            }

            int basePole = b.Position.Count;
            b.Position.Add(line[0] - tangents[0] * radius);
            b.Normal.Add(-tangents[0]);
            b.Tissue.Add(tissue);
            b.Height.Add(line[0].y);

            int first = b.Position.Count;
            for (int r = 0; r < ringCentre.Count; r++)
            {
                Vector3 axis = ringAxis[r];
                Vector3 x = ringAcross[r];
                Vector3 y = Vector3.Cross(axis, x);
                float tilt = ringTilt[r];
                float flat = Mathf.Sqrt(Mathf.Max(0f, 1f - tilt * tilt));

                for (int i = 0; i < sides; i++)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    Vector3 out_ = Mathf.Cos(a) * x + Mathf.Sin(a) * y;
                    b.Position.Add(ringCentre[r] + out_ * ringRadius[r]);
                    b.Normal.Add((out_ * flat + axis * tilt).normalized);
                    b.Tissue.Add(tissue);
                    b.Height.Add(ringHeight[r]);
                }
            }

            int tipPole = b.Position.Count;
            b.Position.Add(line[count - 1] + tangents[count - 1] * radius);
            b.Normal.Add(tangents[count - 1]);
            b.Tissue.Add(tissue);
            b.Height.Add(line[count - 1].y);

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                b.Triangles.Add(basePole); b.Triangles.Add(first + i); b.Triangles.Add(first + j);
            }

            for (int r = 0; r < ringCentre.Count - 1; r++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Quad(b, first + r * sides + i, first + r * sides + j, first + (r + 1) * sides + i, first + (r + 1) * sides + j);
                }
            }

            int last = first + (ringCentre.Count - 1) * sides;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                b.Triangles.Add(tipPole); b.Triangles.Add(last + i); b.Triangles.Add(last + j);
            }
        }

        /// <summary>
        /// Turns a built thallus onto the object axes and into the unit box: the growth axis is <paramref name="axis"/>, the base
        /// at its high end when <paramref name="baseHigh"/>. A canonical point at its box's face lands at <c>0.5 * Inset</c>,
        /// and every vertex is clamped there besides. The sway's reach is baked per vertex in UV channel four: the most a
        /// thallus may sway (<see cref="ThalliSwayWant"/>, cut so that no vertex's share exceeds its room on the two axes the
        /// sway moves along) times the vertex's height share to <see cref="ThalliSwayBend"/>; then its lag, and the axis. Every
        /// triangle is wound so that its face normal agrees with its vertices' normals.
        /// </summary>
        private static Mesh FinishThallus(ThallusBuild b, ThallusForm form, Vector3 e, int axis, bool baseHigh)
        {
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            float half = 0.5f * Inset;
            float flip = baseHigh ? -1f : 1f;  // the canonical bottom (the base) lands at the low end, or at the high end when flipped
            var box = new Vector3(e[axisA], e[axis], e[axisB]);

            int n = b.Position.Count;
            var vertices = new List<Vector3>(n);
            var normals = new List<Vector3>(n);
            var colours = new List<Color>(n);
            var reach = new List<Vector4>(n);

            float low = float.MaxValue, high = float.MinValue;
            foreach (float h in b.Height) { low = Mathf.Min(low, h); high = Mathf.Max(high, h); }
            float span = Mathf.Max(1e-6f, high - low);

            var weights = new float[n];
            float amplitude = ThalliSwayWant;

            for (int i = 0; i < n; i++)
            {
                Vector3 p = b.Position[i];
                Vector3 q = b.Normal[i];

                var o = Vector3.zero;
                o[axisA] = Mathf.Clamp(half * p.x / box.x, -half, half);
                o[axis] = Mathf.Clamp(flip * half * p.y / box.y, -half, half);
                o[axisB] = Mathf.Clamp(half * p.z / box.z, -half, half);

                var m = Vector3.zero;
                m[axisA] = q.x * box.x;
                m[axis] = flip * q.y * box.y;
                m[axisB] = q.z * box.z;

                vertices.Add(o);
                normals.Add(m.sqrMagnitude > 1e-14f ? m.normalized : Vector3.up);
                colours.Add(b.Tissue[i]);

                float share = (b.Height[i] - low) / span;
                weights[i] = Mathf.Pow(Mathf.Clamp01(share), ThalliSwayBend);

                float room = Mathf.Min(half - Mathf.Abs(o[axisA]), half - Mathf.Abs(o[axisB]));
                if (weights[i] > 1e-4f) amplitude = Mathf.Min(amplitude, Mathf.Max(0f, room) / weights[i]);
            }

            amplitude = Mathf.Max(0f, amplitude);

            for (int i = 0; i < n; i++)
            {
                float share = Mathf.Clamp01((b.Height[i] - low) / span);
                reach.Add(new Vector4(amplitude * weights[i], ThalliWaveLag * share, 0f, axis));
            }

            var triangles = new List<int>(b.Triangles.Count);
            for (int t = 0; t < b.Triangles.Count; t += 3)
            {
                int i0 = b.Triangles[t], i1 = b.Triangles[t + 1], i2 = b.Triangles[t + 2];
                Vector3 facing = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);

                if (Vector3.Dot(facing, normals[i0] + normals[i1] + normals[i2]) < 0f)
                {
                    int swap = i1; i1 = i2; i2 = swap;
                }

                triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
            }

            Mesh mesh = Finish("Theatre Thallus " + form, vertices, normals, triangles, ThallusFlag(form));
            mesh.SetUVs(4, reach);
            mesh.SetColors(colours);

            return mesh;
        }

        // THALLI end
        // SNOUT begin
        // ---------------------------------------------------------------- the snout (EVOSIM_THEATRE_NOSE_SNOUT)

        /// <summary>
        /// The snout's tip half width over its base half width: a nose narrows to this share of the collider's cross
        /// section by its free end, where the olfactory pit sits (<c>TheatreNosePit.SnoutOf</c>).
        /// </summary>
        public const float SnoutTipShare = 0.5f;

        /// <summary>
        /// The taper's power: the cross section is <c>1 - (1 - SnoutTipShare) * u^SnoutTaperPower</c> of the collider's, u
        /// the distance from the base face over the length. Above one the slope is zero at the base, where the skirt's
        /// flare meets it, and the snout narrows fastest toward the tip.
        /// </summary>
        public const float SnoutTaperPower = 1.4f;

        /// <summary>
        /// The steps the skirt's depth (over the nose's length) and the flare's share are rounded to, so that a few
        /// meshes serve every nose.
        /// </summary>
        public const int SnoutSkirtSteps = 32;
        public const int SnoutFlareSteps = 8;

        /// <summary>The most snout meshes kept; a nose that would need one more keeps its box.</summary>
        private const int SnoutMeshCap = 48;

        private static readonly Dictionary<int, Mesh> _snouts = new Dictionary<int, Mesh>();
        private static readonly HashSet<Mesh> _snoutMeshes = new HashSet<Mesh>();

        /// <summary>True for a snout mesh.</summary>
        public static bool IsSnout(Mesh mesh) => mesh != null && _snoutMeshes.Contains(mesh);

        /// <summary>
        /// A nose's rounded cube made into a snout, cached by class. Its tip is at the high end of <paramref name="axis"/>
        /// when <paramref name="high"/>, else at the low end. In the cube's unit coordinates (the collider is the cube of
        /// side one) the cross section is the collider's at the base face and narrows to <see cref="SnoutTipShare"/> of it
        /// at the tip, so that part is inside the collider; below the base face a skirt of depth
        /// <paramref name="skirt"/> (over the length) widens from the collider's cross section to
        /// <c>1 + flare</c> times it, tissue across the joint that lies inside the parent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>How.</b> The cube's own vertices are moved, not rebuilt: the axial coordinate is stretched so the cube's base
        /// end lands at the bottom of the skirt, and the two cross coordinates are multiplied by the profile at the new
        /// axial position. The cube's 24 segments to a face edge are kept at the tip face, so the pit has the vertices it
        /// has on the box. The normal is the inverse transpose of the map's Jacobian on the cube's normal, as the link's
        /// taper does it in the shader. The map is a rotation of the same cyclic order in every class, so no winding turns.
        /// </para>
        /// <para>
        /// <b>Not a box to the shader.</b> It carries the shape flag of zero (<see cref="Finish"/>), so the shader's box
        /// taper, bend and clamp do not act on it: the clamp would cut the skirt back into the collider. The carve still
        /// acts, and only inward.
        /// </para>
        /// <para>
        /// The skirt and the flare are rounded to <see cref="SnoutSkirtSteps"/> and <see cref="SnoutFlareSteps"/> and returned
        /// as drawn. Null once <see cref="SnoutMeshCap"/> meshes are kept.
        /// </para>
        /// <para>
        /// <b>At EVOSIM_THEATRE_NO_BOX 2</b> the map acts on <see cref="TheatreNoBox.Mesh"/> in place of the rounded cube, so the
        /// tip is a rounded end with no flat face and the pillow plays no part in the class (the key's bucket is zero).
        /// </para>
        /// </remarks>
        public static Mesh Snout(
            int axis, bool high, float pillow, float skirt, float flare, out float skirtDrawn, out float flareDrawn)
        {
            int qs = Mathf.Clamp(Mathf.RoundToInt(skirt * SnoutSkirtSteps), 0, SnoutSkirtSteps);
            int qf = qs == 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(flare * SnoutFlareSteps), 0, SnoutFlareSteps);

            skirtDrawn = qs / (float)SnoutSkirtSteps;
            flareDrawn = qf / (float)SnoutFlareSteps;

            int bucket = Bucket(pillow);
            // NOBOX begin
            // Level 2: the snout is cut from the no-box solid, which has no pillow, so one class serves every cubicness.
            if (TheatreNoBox.EllipsoidAll) bucket = 0;
            // NOBOX end
            int key = axis | ((high ? 1 : 0) << 2) | ((bucket / 4) << 3) | (qs << 7) | (qf << 13);

            if (_snouts.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;
            if (_snouts.Count >= SnoutMeshCap) return null;

            mesh = BuildSnout(axis, high, bucket / 100f, skirtDrawn, flareDrawn);
            _snouts[key] = mesh;
            _snoutMeshes.Add(mesh);

            return mesh;
        }

        /// <summary>
        /// The cross section's multiplier <paramref name="s"/> and its slope <paramref name="ds"/> along the axis at the axial
        /// coordinate <paramref name="t"/> (the tip at <paramref name="h"/>, the collider's base face at minus a half).
        /// </summary>
        private static void SnoutProfile(float t, float skirt, float flare, float h, out float s, out float ds)
        {
            if (t >= -0.5f || skirt <= 1e-6f)
            {
                float u = Mathf.Clamp01((t + 0.5f) / (h + 0.5f));

                s = 1f - (1f - SnoutTipShare) * Mathf.Pow(u, SnoutTaperPower);
                ds = -(1f - SnoutTipShare) * SnoutTaperPower * Mathf.Pow(u, SnoutTaperPower - 1f) / (h + 0.5f);
            }
            else
            {
                float d = Mathf.Clamp01((-0.5f - t) / skirt);

                s = 1f + flare * d * d;
                ds = -2f * flare * d / skirt;
            }
        }

        private static Mesh BuildSnout(int axis, bool high, float pillow, float skirt, float flare)
        {
            // NOBOX begin
            // Level 2: the no-box solid, of the rounded cube's box, takes the rounded cube's place: the same map on its vertices,
            // a tapered rounded solid with no flat tip face.
            Mesh source = TheatreNoBox.EllipsoidAll ? TheatreNoBox.Mesh() : RoundedCube(pillow);
            // NOBOX end
            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;

            int a = axis, b = (axis + 1) % 3, c = (axis + 2) % 3;
            float h = 0.5f * Inset;
            float low = -0.5f - skirt;
            float stretch = (h - low) / (2f * h);
            float sign = high ? 1f : -1f;

            var vertices = new List<Vector3>(sourceVertices.Length);
            var normals = new List<Vector3>(sourceVertices.Length);

            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 v = sourceVertices[i];
                Vector3 n = sourceNormals[i];
                float pb = v[b], pc = v[c];

                float t = low + (v[a] + h) / (2f * h) * (h - low);
                SnoutProfile(t, skirt, flare, h, out float s, out float ds);

                // In the frame whose first axis runs base to tip. The normal: the inverse transpose of the Jacobian of
                // (t, pb, pc) -> (t', pb s, pc s), s a function of t' alone.
                var turned = new Vector3(
                    n[a] / stretch - ds * (pb * n[b] + pc * n[c]) / s,
                    n[b] / s,
                    n[c] / s).normalized;

                var position = Vector3.zero;
                var normal = Vector3.zero;

                // A tip at the low end is a half turn about the third axis: the first two components change sign, and the
                // handedness, so the winding, is kept.
                position[a] = sign * t;
                position[b] = sign * pb * s;
                position[c] = pc * s;

                normal[a] = sign * turned.x;
                normal[b] = sign * turned.y;
                normal[c] = turned.z;

                vertices.Add(position);
                normals.Add(normal);
            }

            return Finish("Theatre Snout", vertices, normals, new List<int>(source.triangles), 0f);
        }

        private static void ReleaseSnouts()
        {
            foreach (int key in new List<int>(_snouts.Keys))
            {
                Mesh snout = _snouts[key];
                Discard(ref snout);
            }

            _snouts.Clear();
            _snoutMeshes.Clear();
        }

        // SNOUT end
        // ---------------------------------------------------------------- shared

        /// <summary>
        /// Closes one mesh, and tells the shader which solid it is.
        /// </summary>
        /// <param name="boxness">
        /// One for the cube and zero for anything else. The third day's taper and bend are for
        /// boxes only, and the shader has no other way to know what it is drawing.
        /// </param>
        /// <remarks>
        /// <para>
        /// <b>Why the flag is a vertex attribute and not a material property.</b> Every body in
        /// the theatre is drawn with one shared material and painted through one
        /// <c>MaterialPropertyBlock</c> that <c>TheatrePalette</c> owns; which primitive a part
        /// draws with is known there, but the flag has to reach the vertex stage of a mesh that
        /// the palette has already swapped, and a mesh knows what it is. Baked into UV channel
        /// three, which nothing else in the theatre uses and which the engine's own primitives do
        /// not carry, so a mesh this class did not build reads zero and is left alone.
        /// </para>
        /// <para>
        /// <b>Why zero is the safe answer, and it is a size bound.</b> A sphere part is drawn as
        /// the genome's three half extents scaled so that the longest semi-axis is the collider's
        /// radius (TheatrePalette.Aspect), so its collider is the ball and not the box: clamping a
        /// bent sphere into the box of its half extents would keep it inside a box the physics
        /// does not have while letting it out of the ball the physics does. So the deformation the
        /// clamp guards is offered to the cube alone, and anything unrecognised gets the second
        /// day's carve, whose bound is the sign of a displacement rather than a shape.
        /// </para>
        /// <para>
        /// The channel's second number is the mesh's own object space half extent, so the shader
        /// clamps against the mesh's bound rather than against a constant copied out of this file.
        /// It is only read when the flag is one.
        /// </para>
        /// </remarks>
        internal static Mesh Finish(
            string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            float boxness)
        {
            var mesh = new Mesh
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var shape = new List<Vector2>(vertices.Count);
            var flag = new Vector2(boxness, 0.5f * Inset);

            for (int i = 0; i < vertices.Count; i++) shape.Add(flag);

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(3, shape);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}

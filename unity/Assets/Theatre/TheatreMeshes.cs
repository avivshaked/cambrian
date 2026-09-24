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
        private static Mesh Finish(
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

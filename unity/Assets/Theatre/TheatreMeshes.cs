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
    /// space on that axis. That is the whole of the bound now, because the only thing the body
    /// shader does to a vertex is move it <i>inward</i>: the first day's outward puff is gone and
    /// the carve that replaced it is <c>-normal * depth * noise</c> with the noise in [0, 1], so
    /// no vertex can reach the collider from inside, let alone pass it. A carved body is a little
    /// smaller than its box, which under-reports rather than over-reports, which is the direction
    /// the two constraints allow.
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
        /// A third rather than the first day's quarter. The carve makes a box's faces wavy and
        /// its edges wander, and in the first carved pictures the thing still saying "built" was
        /// the corner: three planes meeting at a right angle survives any amount of surface
        /// relief. The rounding is inward, so a wider fillet only makes the drawn body smaller,
        /// and it does not touch the ratio between the half extents, which is what makes a box a
        /// fin (StandardShapes' BoxShape remarks).
        /// </remarks>
        public const float Roundness = 0.34f;

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

        private static Mesh _cube;
        private static Mesh _sphere;
        private static Mesh _cylinder;

        /// <summary>
        /// A unit cube of side one, rounded, inset, centred on the origin. Replaces Unity's Cube.
        /// </summary>
        public static Mesh RoundedCube() => _cube != null ? _cube : (_cube = BuildRoundedCube());

        /// <summary>A unit sphere of diameter one, smooth, inset. Replaces Unity's Sphere.</summary>
        public static Mesh Sphere() => _sphere != null ? _sphere : (_sphere = BuildSphere());

        /// <summary>
        /// A cylinder of diameter one and height two, inset, matching Unity's Cylinder so that
        /// <c>VisualPlan</c>'s scale for a capsule shaft still means what it meant.
        /// </summary>
        public static Mesh Cylinder() => _cylinder != null ? _cylinder : (_cylinder = BuildCylinder());

        /// <summary>
        /// The rounded stand-in for one of Unity's primitives, matched by name, or null when the
        /// mesh is not one this class replaces.
        /// </summary>
        /// <remarks>
        /// By name because that is the only handle the theatre has.
        /// <c>PhenotypeBuilder.PrimitiveMesh</c> borrows the engine's built in Cube, Sphere and
        /// Cylinder meshes, which are shared assets called exactly that, and the theatre must not
        /// reach into <c>Evosim.Sim</c> to be told which is which. A mesh whose name is anything
        /// else is left alone, so a part drawn with something new goes on being drawn with it
        /// rather than being silently replaced by a cube.
        /// </remarks>
        public static Mesh RoundedFor(Mesh primitive)
        {
            if (primitive == null) return null;

            switch (primitive.name)
            {
                case "Cube": return RoundedCube();
                case "Sphere": return Sphere();
                case "Cylinder": return Cylinder();
                default: return null;
            }
        }

        /// <summary>Drops the generated meshes. Called when the theatre closes.</summary>
        public static void Release()
        {
            Discard(ref _cube);
            Discard(ref _sphere);
            Discard(ref _cylinder);
        }

        private static void Discard(ref Mesh mesh)
        {
            if (mesh == null) { mesh = null; return; }

            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);

            mesh = null;
        }

        // ---------------------------------------------------------------- the cube

        private static Mesh BuildRoundedCube()
        {
            float half = 0.5f * Inset;
            float radius = Roundness * half;
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

            return Finish("Theatre Rounded Cube", vertices, normals, triangles);
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

            return Finish("Theatre Sphere", vertices, normals, triangles);
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
            float fillet = Roundness * Mathf.Min(radius, half);

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

            return Finish("Theatre Cylinder", vertices, normals, triangles);
        }

        // ---------------------------------------------------------------- shared

        private static Mesh Finish(
            string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}

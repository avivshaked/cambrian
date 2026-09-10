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
    /// space on that axis. The body shader then pushes each vertex outward along its normal by at
    /// most <see cref="PuffFraction"/> of the part's <i>smallest</i> half extent, which is at most
    /// that fraction of the half extent on any axis. Inset plus puff is one, so the puffed surface
    /// lands on the collider and never outside it. Change either number and the other has to move
    /// with it, which is why <see cref="TheatreSkin"/> clamps the shader's puff to
    /// <c>1 - Inset</c> rather than trusting the material.
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
        /// How much of the collider a generated mesh is allowed to fill before the shader's puff.
        /// </summary>
        public const float Inset = 0.97f;

        /// <summary>
        /// The largest outward push the body shader may add, as a fraction of the smallest half
        /// extent. <c>Inset + PuffFraction == 1</c> is the invariant; see the class remarks.
        /// </summary>
        public const float PuffFraction = 1f - Inset;

        /// <summary>How much of a half extent the cube's corner rounding eats, as [CC1] measures it.</summary>
        public const float Roundness = 0.25f;

        /// <summary>Vertices along one edge of one cube face.</summary>
        private const int CubeSegments = 8;

        /// <summary>How many times the icosahedron is subdivided. Two gives 320 faces.</summary>
        private const int SphereSubdivisions = 2;

        /// <summary>Sides around a cylinder.</summary>
        private const int CylinderSides = 20;

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

        private static Mesh BuildCylinder()
        {
            float radius = 0.5f * Inset;
            float half = Inset;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // The side, with the seam duplicated so both ends of the ring get the same normal.
            for (int i = 0; i <= CylinderSides; i++)
            {
                float angle = 2f * Mathf.PI * i / CylinderSides;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                vertices.Add(outward * radius + Vector3.up * half);
                normals.Add(outward);
                vertices.Add(outward * radius - Vector3.up * half);
                normals.Add(outward);
            }

            for (int i = 0; i < CylinderSides; i++)
            {
                int a = 2 * i, b = a + 1, c = a + 2, d = a + 3;

                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }

            AddCylinderCap(vertices, normals, triangles, half, Vector3.up, radius);
            AddCylinderCap(vertices, normals, triangles, -half, Vector3.down, radius);

            return Finish("Theatre Cylinder", vertices, normals, triangles);
        }

        private static void AddCylinderCap(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            float y, Vector3 normal, float radius)
        {
            int centre = vertices.Count;

            vertices.Add(new Vector3(0f, y, 0f));
            normals.Add(normal);

            for (int i = 0; i <= CylinderSides; i++)
            {
                float angle = 2f * Mathf.PI * i / CylinderSides;

                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
                normals.Add(normal);
            }

            for (int i = 0; i < CylinderSides; i++)
            {
                int a = centre + 1 + i;
                int b = centre + 2 + i;

                if (normal.y > 0f)
                {
                    triangles.Add(centre); triangles.Add(b); triangles.Add(a);
                }
                else
                {
                    triangles.Add(centre); triangles.Add(a); triangles.Add(b);
                }
            }
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

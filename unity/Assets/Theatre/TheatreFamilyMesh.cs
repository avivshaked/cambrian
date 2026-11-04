using System;
using System.Collections.Generic;
using UnityEngine;
using Evosim.Core;

namespace Evosim.Theatre
{
    /// <summary>
    /// A superellipsoid part (D233) drawn as its own solid, under <c>EVOSIM_THEATRE_FAMILY_MESH</c>, in place of the box the
    /// theatre falls back to for a shape it has no case for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The dial.</b> 0 (the default) or 1, read once through <see cref="TheatreSkin.Dial"/>. At 0 nothing here runs but the
    /// read: <see cref="Wants"/> answers false, the two switches (<c>SnapshotWorld.AddVisuals</c> and
    /// <c>LiveWorldView.Plan</c>) take their <c>default</c> as they always did, and no mesh is made. At 1 a part whose resolved
    /// shape is a <see cref="SuperellipsoidShape"/> and whose <see cref="PhenotypePart.Family"/> is set is drawn as one mesh
    /// of <see cref="Resolution"/> by <see cref="Resolution"/> quads on each of the cube's six faces, every vertex pushed onto the
    /// surface by Core's own map (<see cref="Superellipsoid.SurfacePointAndNormal"/>) and carrying Core's normal there: not one
    /// recomputed from the triangles, and nothing welded across a face, so a seam keeps a vertex on each side with the normal
    /// the surface has at it.
    /// </para>
    /// <para>
    /// <b>One mesh to a form, in the unit solid's units.</b> The map is linear in the half-extents:
    /// <c>(a*x*f, b*(y - c), c*z*f)</c>, with x, y, z, the taper <c>f</c> and the centre shift <c>c</c> functions of the form
    /// alone. So the surface at <c>ReferenceHalfExtents</c> divided by twice those half-extents, axis by axis, is a solid that
    /// depends on the form and on nothing else, and the part is drawn by scaling it by twice its reference half-extents times
    /// <see cref="PhenotypePart.FamilyScale"/> (<see cref="ScaleOf(FamilyGeometry, float)"/>), which is the box's own
    /// <c>2*|h|</c> per axis to within a rounding. That is why the mesh is in the unit solid's units and not in metres: every
    /// consumer of a part's visual (the palette's pinches, ramps and anchors, the shader's <c>HalfExtentsWS</c>, its carve
    /// depth and its carve's wavelength, all in "object units where the part's own size is one") reads the visual's scale as
    /// the part's size, and a mesh in metres drawn at a scale near one would be read as a part half a metre across. The
    /// normals are carried into the same axes: a normal is covariant, so Core's normal <c>n</c> becomes
    /// <c>normalize(D*n)</c> with <c>D = diag(2a, 2b, 2c)</c>, and Unity's inverse transpose of the visual's scale takes it
    /// back to <c>n</c> in the world. The cache is keyed by the form for the same reason: two family geometries of one form
    /// give one mesh, where one mesh per geometry would hold a mesh for every creature born.
    /// </para>
    /// <para>
    /// <b>A plain surface to the shader.</b> The mesh carries <c>(0, 0.5)</c> in UV channel three (<see cref="TheatreMeshes"/>,
    /// <c>Finish</c>): a solid flag of zero, as the sphere, the cylinder and the snout carry. <c>TheatreBody.shader</c> reads
    /// the flag as <c>skin = x &gt; 2.5</c>, <c>lamina = x &gt; 1.5</c>, <c>box = x &gt; 0.5</c> and a thallus by
    /// <c>ThalliIs</c> (within 0.02 of 0.2, 0.25 or 0.3), so zero is none of them: no box taper, bend, soft tissue or clamp,
    /// and the carve alone, which only cuts inward. The second number is the solid's half extent in its own units, read for a
    /// box, a skin and a blade alone, and 0.5 as every unit solid has it.
    /// </para>
    /// <para>
    /// <b>Memory.</b> A mesh is 3,750 vertices and 6,912 triangles, about 160 kB, and a form is a continuous gene, so a long
    /// live world holds one mesh for each distinct form it has drawn until <see cref="Release"/> (called from
    /// <see cref="TheatreMeshes.Release"/>).
    /// </para>
    /// </remarks>
    public static class TheatreFamilyMesh
    {
        /// <summary>The dial: 0 off (the default), 1 on. Read once.</summary>
        public static readonly bool Enabled = TheatreSkin.Dial("EVOSIM_THEATRE_FAMILY_MESH", 0f, 0f, 1f) >= 0.5f;

        /// <summary>Quads along a cube face's edge. Six faces of this many squared: 3,750 vertices at 24.</summary>
        public const int Resolution = 24;

        /// <summary>The shape channel's second number: the solid's half extent in its own units, as a unit solid has it.</summary>
        private const float MeshHalf = 0.5f;

        /// <summary>A reference half-extent under this is no solid to draw; the part keeps the box.</summary>
        private const float Smallest = 1e-6f;

        private static readonly Dictionary<PartForm, Mesh> _meshes = new Dictionary<PartForm, Mesh>();

        static TheatreFamilyMesh()
        {
            if (!Enabled) return;

            int vertices = 6 * (Resolution + 1) * (Resolution + 1);
            int triangles = 6 * Resolution * Resolution * 2;

            Debug.Log(
                "[Theatre] EVOSIM_THEATRE_FAMILY_MESH=1: a superellipsoid part is drawn as its own solid, " +
                Resolution + " quads to a cube face's edge (" + vertices + " vertices, " + triangles + " triangles), one mesh to a form");
        }

        /// <summary>
        /// Whether this part is drawn as a family mesh: the dial is on, the part has a family, and its reference half-extents and
        /// scale are positive.
        /// </summary>
        public static bool Wants(PhenotypePart part)
        {
            if (!Enabled || part == null) return false;

            FamilyGeometry family = part.Family;
            if (family == null) return false;

            Float3 r = family.ReferenceHalfExtents;

            return Mathf.Abs(r.X) > Smallest && Mathf.Abs(r.Y) > Smallest && Mathf.Abs(r.Z) > Smallest
                   && part.FamilyScale > 0f;
        }

        /// <summary>The visual's local scale for a part: twice its reference half-extents times its family scale, per axis.</summary>
        public static Vector3 ScaleOf(PhenotypePart part) => ScaleOf(part.Family, part.FamilyScale);

        /// <summary>The visual's local scale for a family at a scale: twice its reference half-extents times the scale, per axis.</summary>
        public static Vector3 ScaleOf(FamilyGeometry family, float familyScale)
        {
            Float3 r = family.ReferenceHalfExtents;

            return new Vector3(
                2f * Mathf.Abs(r.X) * familyScale,
                2f * Mathf.Abs(r.Y) * familyScale,
                2f * Mathf.Abs(r.Z) * familyScale);
        }

        /// <summary>
        /// The mesh of a family's form, in the unit solid's units, built on first use and shared by every part of that form.
        /// </summary>
        public static Mesh For(FamilyGeometry family)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));

            PartForm form = family.Geometry.Form;

            if (_meshes.TryGetValue(form, out Mesh held) && held != null) return held;

            Mesh built = Build(family.Geometry, family.ReferenceHalfExtents, form);
            _meshes[form] = built;
            return built;
        }

        /// <summary>Drops the meshes. Called when the theatre closes (<see cref="TheatreMeshes.Release"/>).</summary>
        public static void Release()
        {
            foreach (Mesh mesh in _meshes.Values)
            {
                if (mesh == null) continue;

                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }

            _meshes.Clear();
        }

        /// <summary>
        /// Six faces of <see cref="Resolution"/> by <see cref="Resolution"/> quads. A face's vertices run along the axis after the
        /// face's own and then the one after that, so on a face of axis a the first step crossed with the second points along +a,
        /// and a triangle wound (v00, v10, v01) on the +a face faces outward; the -a face swaps the last two.
        /// </summary>
        private static Mesh Build(Superellipsoid solid, Float3 reference, PartForm form)
        {
            int n = Resolution;
            int row = n + 1;

            var vertices = new List<Vector3>(6 * row * row);
            var normals = new List<Vector3>(6 * row * row);
            var triangles = new List<int>(6 * n * n * 6);

            double dx = 2.0 * Math.Abs((double)reference.X);
            double dy = 2.0 * Math.Abs((double)reference.Y);
            double dz = 2.0 * Math.Abs((double)reference.Z);

            for (int axis = 0; axis < 3; axis++)
            {
                for (int side = 0; side < 2; side++)
                {
                    double sign = side == 0 ? 1.0 : -1.0;
                    int first = vertices.Count;

                    for (int i = 0; i <= n; i++)
                    {
                        for (int j = 0; j <= n; j++)
                        {
                            Double3 cube = CubePoint(axis, sign, -1.0 + 2.0 * i / n, -1.0 + 2.0 * j / n);
                            Double3 p = solid.SurfacePointAndNormal(cube, reference, out Double3 normal);

                            vertices.Add(new Vector3((float)(p.X / dx), (float)(p.Y / dy), (float)(p.Z / dz)));

                            // Core's normal in the unit solid's axes: covariant, so scaled by the same D the points are divided by.
                            normals.Add(new Vector3(
                                (float)(dx * normal.X), (float)(dy * normal.Y), (float)(dz * normal.Z)).normalized);
                        }
                    }

                    for (int i = 0; i < n; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            int a = first + i * row + j;
                            int b = a + row;
                            int c = a + 1;
                            int d = b + 1;

                            if (side == 0)
                            {
                                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                                triangles.Add(b); triangles.Add(d); triangles.Add(c);
                            }
                            else
                            {
                                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                                triangles.Add(b); triangles.Add(c); triangles.Add(d);
                            }
                        }
                    }
                }
            }

            // The shape channel the other body meshes carry (TheatreMeshes.Finish): flag zero, a plain surface.
            var shape = new List<Vector2>(vertices.Count);
            var flag = new Vector2(0f, MeshHalf);

            for (int i = 0; i < vertices.Count; i++) shape.Add(flag);

            var mesh = new Mesh
            {
                name = "Theatre Family Mesh " + form,
                hideFlags = HideFlags.HideAndDontSave,
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(3, shape);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>The point of the cube's surface on a face: <paramref name="axis"/> at <paramref name="sign"/>, the next two axes at u and v.</summary>
        private static Double3 CubePoint(int axis, double sign, double u, double v)
        {
            double x = 0.0, y = 0.0, z = 0.0;

            Set(ref x, ref y, ref z, axis, sign);
            Set(ref x, ref y, ref z, (axis + 1) % 3, u);
            Set(ref x, ref y, ref z, (axis + 2) % 3, v);

            return new Double3(x, y, z);
        }

        private static void Set(ref double x, ref double y, ref double z, int axis, double value)
        {
            if (axis == 0) x = value;
            else if (axis == 1) y = value;
            else z = value;
        }
    }
}

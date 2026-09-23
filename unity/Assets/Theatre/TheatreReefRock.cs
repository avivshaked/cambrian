using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Evosim.Theatre
{
    /// <summary>
    /// The rock a mushroom reef is drawn with: its cap and its stem as lathe meshes cut into by
    /// noise in the reef's own frame, seeded per reef (the owner, 2026-09-23 night: "an
    /// appropriate skin to the reefs, so they look more like a natural rocky structure ... on the
    /// surface as well as the stem").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Inward only, and that is the size argument.</b> The physics pushes a body by the rock's
    /// analytic signed distance (<c>ReefGeometry.SignedDistance</c>), so a drawn rock that stood
    /// outside that surface would show bodies sunk into stone. Every vertex here starts on the
    /// analytic surface and moves along the surface's inward direction by a depth that is never
    /// negative, the same rule the bodies' carve and the bed's carve keep. The cap's top moves
    /// straight down, its underside straight up, its rim toward the rim's own centre circle, and
    /// the stem toward its axis; at a fixed height or a fixed radius the solid contains every point
    /// on that path as long as the depth is under the rock's thickness there, and the amplitudes
    /// below are bounded by fractions of the thickness, the rim's radius and the stem's radius
    /// that keep it so (top plus underside at most 0.6 of the cap's thickness, the rim at most
    /// 0.45 of its half-thickness, the stem at most 0.4 of its radius).
    /// </para>
    /// <para>
    /// <b>The fillet is drawn, inside the rock.</b> The analytic rock joins stem and cap by a cubic
    /// smooth minimum with a fillet of half the cap's thickness (<c>ReefGeometry.SmoothUnion</c>).
    /// The stem's profile here follows that fillet's boundary, solved by bisection on the same
    /// formula and pulled in by 3%, so the stem flares into the cap the way the physics' rock does
    /// rather than meeting it at a crease. It assumes the fillet stays half the cap's thickness;
    /// if D118's geometry changes that rule, <see cref="FilletFlare"/> is the one place to follow it.
    /// </para>
    /// <para>
    /// <b>Vertex colours carry two readings for the shader.</b> Red is how deep the vertex was cut,
    /// as a fraction of the most it could be there, which the shader darkens as a cheap cavity
    /// term; green is the analytic surface's upward component mapped to [0, 1] (1 the table, 0.5
    /// the rim's equator and the stem, 0 the underside), so the crust follows the rock's shape and
    /// not the cut's; blue is 1 on the cap and 0 on the stem.
    /// </para>
    /// <para>
    /// <b>Not drawn yet: the marine snow on the table.</b> The snow that settles on a cap's top
    /// would attach here as a second, thin mesh draped on the carved table (the top rows of
    /// <see cref="Cap"/>, whose vertices are the first <c>nTop + 1</c> rows of every column), or
    /// as a term in the rock shader keyed on the green channel; neither is built.
    /// </para>
    /// </remarks>
    public static class TheatreReefRock
    {

        /// <summary>The cap of one reef in its own frame (origin on the axis at the cap's mid-plane).</summary>
        public static Mesh Cap(ReefLook look)
        {
            float t = Mathf.Max(0.05f, look.CapThickness);
            float half = 0.5f * t;
            float maxRadius = Mathf.Max(half + 0.05f, look.MaxRadius());

            // Resolution: a metre of relief wants a sample every quarter metre or so; a small cap
            // is held at a floor so the rim still reads round.
            float spacing = Mathf.Max(0.15f, maxRadius / 48f);
            int segments = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * maxRadius / 0.2f), 96, 256);
            float maxFlat = Mathf.Max(0f, maxRadius - half);
            int nTop = Mathf.Max(6, Mathf.CeilToInt(maxFlat / spacing));
            int nBottom = Mathf.Max(5, Mathf.CeilToInt(maxFlat / (1.4f * spacing)));
            const int nArc = 16;

            int rows = (nTop + 1) + (nArc - 1) + (nBottom + 1);
            int stride = segments + 1;

            var vertices = new Vector3[rows * stride];
            var colours = new Color32[vertices.Length];

            // The amplitudes, each bounded as the class remarks say.
            float topDepth = Mathf.Min(0.32f * t, 1.6f);
            float bottomDepth = Mathf.Min(0.26f * t, 1.3f);
            float rimDepth = 0.45f * half;

            Vector3 offset = SeedOffset(look.Seed);

            // The underside is left uncut where the stem's flare meets it, so the flare's top
            // ring sits on the plane it was solved against and no sliver of the stem's buried
            // part shows between the two.
            float stemClear = look.StemRadius > 0f ? look.StemRadius + half : -1f;

            for (int s = 0; s <= segments; s++)
            {
                float theta = 2f * Mathf.PI * (s % segments) / segments;
                float c = Mathf.Cos(theta);
                float si = Mathf.Sin(theta);

                float radius = Mathf.Max(half + 0.01f, look.Outline(theta));
                float flat = radius - half;

                int row = 0;

                // The table, from the axis out to the rim's start.
                for (int j = 0; j <= nTop; j++, row++)
                {
                    float rho = flat * j / nTop;
                    Place(vertices, colours, row * stride + s, rho, half, 0f, 1f, c, si,
                          offset, topDepth, rimDepth, bottomDepth, maxRadius, stemClear, isCap: true);
                }

                // Round the rim, from the table's edge to the underside's.
                for (int k = 1; k < nArc; k++, row++)
                {
                    float a = 0.5f * Mathf.PI - Mathf.PI * k / nArc;
                    float nr = Mathf.Cos(a);
                    float ny = Mathf.Sin(a);
                    Place(vertices, colours, row * stride + s, flat + half * nr, half * ny, nr, ny, c, si,
                          offset, topDepth, rimDepth, bottomDepth, maxRadius, stemClear, isCap: true);
                }

                // The underside, from the rim back to the axis.
                for (int j = nBottom; j >= 0; j--, row++)
                {
                    float rho = flat * j / nBottom;
                    Place(vertices, colours, row * stride + s, rho, -half, 0f, -1f, c, si,
                          offset, topDepth, rimDepth, bottomDepth, maxRadius, stemClear, isCap: true);
                }
            }

            // The two poles: every column's first and last row is the same point on the axis.
            int[] canonical = Canonical(rows, segments, poleRows: new[] { 0, rows - 1 });
            return Build("Theatre Reef Cap", vertices, colours, rows, segments, canonical);
        }

        /// <summary>
        /// The stem of one reef in its own frame, from <paramref name="bottom"/> (local y, under
        /// the floor) up to the cap's mid-plane, flaring into the cap along the fillet.
        /// </summary>
        public static Mesh Stem(ReefLook look, float bottom)
        {
            float t = Mathf.Max(0.05f, look.CapThickness);
            float half = 0.5f * t;
            float fillet = half;
            float stemRadius = look.StemRadius;

            float spacing = 0.22f;
            int segments = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * (stemRadius + fillet) / 0.2f), 48, 96);
            int stride = segments + 1;

            // The profile's heights: dense through the flare, a row every spacing below it.
            var heights = new List<float>();
            heights.Add(0f);
            heights.Add(-0.5f * half);
            for (int k = 0; k <= 16; k++)
            {
                float v = fillet * k / 16f;
                heights.Add(-half - v);
            }
            // Twice as coarse past eight metres under the cap: that far down the stem is seen from
            // across the tank or not at all, and a long stem at the fine spacing is most of a
            // reef's triangles.
            float y = -half - fillet - spacing;
            while (y > bottom)
            {
                heights.Add(y);
                y -= (-half - y) < 8f ? spacing : 2f * spacing;
            }
            heights.Add(bottom);

            int rows = heights.Count;
            var vertices = new Vector3[rows * stride];
            var colours = new Color32[vertices.Length];

            Vector3 offset = SeedOffset(look.Seed) + new Vector3(17.3f, 0f, -9.1f);
            float depthMax = 0.4f * stemRadius;

            for (int s = 0; s <= segments; s++)
            {
                float theta = 2f * Mathf.PI * (s % segments) / segments;
                float c = Mathf.Cos(theta);
                float si = Mathf.Sin(theta);

                for (int r = 0; r < rows; r++)
                {
                    float h = heights[r];
                    float below = -half - h;                       // under the underside, m
                    float flare = below <= 0f ? fillet : FilletFlare(below, fillet);
                    float radius = stemRadius + 0.97f * flare;

                    Vector3 onSurface = new Vector3(radius * c, h, radius * si);
                    Vector3 q = onSurface + offset;

                    // A stem that bulges and pinches over metres, knobbed at about one, and grained.
                    float bulge = Noise(new Vector3(q.x * 0.35f, q.y / 4.5f, q.z * 0.35f));
                    float knobs = Cells(q / 1.1f);
                    float crag = Ridged(Noise(q / 1.6f));
                    float fine = Noise(q / 0.45f);
                    float cut = Mathf.Clamp01(0.35f * bulge + 0.25f * knobs + 0.15f * crag + 0.15f * Strata(q) + 0.1f * fine);

                    // Where the stem is inside the cap, no cut: the underside's own cut meets it.
                    float fade = Mathf.Clamp01(below / Mathf.Max(0.01f, 0.35f * fillet));
                    float depth = depthMax * cut * fade;

                    int index = r * stride + s;
                    vertices[index] = new Vector3((radius - depth) * c, h, (radius - depth) * si);
                    colours[index] = new Color32(
                        (byte)Mathf.RoundToInt(255f * cut * fade), 128, 0, 255);
                }
            }

            int[] canonical = Canonical(rows, segments, poleRows: Array.Empty<int>());
            return Build("Theatre Reef Stem", vertices, colours, rows, segments, canonical);
        }

        /// <summary>
        /// How far past the stem's radius the rock reaches at <paramref name="below"/> metres under
        /// the cap's underside: the zero of <c>ReefGeometry.SmoothUnion</c>'s cubic smooth minimum
        /// of the stem's distance <c>u</c> and the cap's <c>v</c>, found by bisection on <c>u</c>
        /// (the minimum rises with <c>u</c>, so the zero is unique).
        /// </summary>
        public static float FilletFlare(float below, float fillet)
        {
            if (below >= fillet || fillet <= 0f) return 0f;

            float lo = 0f, hi = fillet;
            for (int i = 0; i < 40; i++)
            {
                float u = 0.5f * (lo + hi);
                float spread = Mathf.Abs(u - below);
                float h = Mathf.Max(fillet - spread, 0f) / fillet;
                float d = Mathf.Min(u, below) - fillet * h * h * h / 6f;
                if (d < 0f) lo = u; else hi = u;
            }
            return lo;
        }

        // ------------------------------------------------------------------ the cap's cut

        private static void Place(
            Vector3[] vertices, Color32[] colours, int index,
            float rho, float y, float nr, float ny, float c, float si, Vector3 offset,
            float topDepth, float rimDepth, float bottomDepth, float maxRadius, float stemClear, bool isCap)
        {
            var onSurface = new Vector3(rho * c, y, rho * si);
            var normal = new Vector3(nr * c, ny, nr * si);
            Vector3 q = onSurface + offset;

            // The three characters, blended by the analytic normal's height so the cut is
            // continuous round the rim: boulders and ledges on the table, a ragged rim, a pitted
            // underside.
            float up = Mathf.Clamp01(ny);
            float down = Mathf.Clamp01(-ny);
            float side = 1f - Mathf.Max(up, down);

            float cells = Cells(q / 2.1f);              // 0 on a boulder's crown, 1 between
            float fine = Noise(q / 0.6f);

            float cut = 0f;
            float amplitude = 0f;

            if (up > 0f)
            {
                // Ledges: a slow field in plan, terraced into three steps with rounded risers.
                float slow = Noise(new Vector3(q.x / 7f, 0.37f, q.z / 7f));
                float steps = slow * 3f;
                float tread = Mathf.Floor(steps);
                float riser = Smooth(0.55f, 1f, steps - tread);
                float ledge = Mathf.Clamp01((tread + riser) / 3f);

                // Blocks with narrow joints between them: the cell field flattened on each
                // block's crown and steep at its joint, at two sizes.
                float blocks = Smooth(0.3f, 0.85f, Cells(q / 1.25f));
                float top = 0.28f * Smooth(0.2f, 0.9f, cells) + 0.24f * blocks + 0.22f * ledge
                          + 0.14f * Ridged(Noise(q / 0.8f)) + 0.12f * fine;
                cut += up * top;
                amplitude += up * topDepth;
            }

            if (side > 0f)
            {
                // A rim broken back unevenly round the cap, not a lathe's ring.
                // Craggy rather than pillowed: a ridged field puts the rock's outermost points on
                // sharp crests, and strata cut level grooves across the rim's face.
                float along = Ridged(Noise(q / Mathf.Clamp(0.25f * maxRadius, 1.2f, 4f)));
                float chips = Ridged(Noise(q / 0.45f));
                float blocks = Smooth(0.3f, 0.85f, Cells(q / 0.9f));
                float rim = 0.26f * along + 0.22f * Strata(q) + 0.2f * blocks
                          + 0.2f * chips + 0.12f * fine;
                cut += side * rim;
                amplitude += side * rimDepth;
            }

            if (down > 0f)
            {
                // Pits: sharp holes where the cell distance is small, over a soft swell.
                float pits = 1f - Smooth(0f, 0.38f, Cells(q / 0.9f));
                float swell = Noise(q / 2.6f);
                float under = Mathf.Clamp01(0.5f * swell + 0.38f * pits + 0.12f * fine + 0.12f);
                if (stemClear > 0f) under *= Smooth(stemClear, stemClear + 1.2f, rho);
                cut += down * under;
                amplitude += down * bottomDepth;
            }

            cut = Mathf.Clamp01(cut);
            float depth = amplitude * cut;                 // never negative

            vertices[index] = onSurface - normal * depth;
            colours[index] = new Color32(
                (byte)Mathf.RoundToInt(255f * cut),
                (byte)Mathf.RoundToInt(255f * (0.5f + 0.5f * ny)),
                isCap ? (byte)255 : (byte)0,
                255);
        }

        // ------------------------------------------------------------------ the mesh

        /// <summary>
        /// Which vertex each vertex's normal is accumulated on: the seam's last column on the
        /// first, and every column of a pole row on that row's first vertex, so the lathe has no
        /// seam in its shading and no star at its poles.
        /// </summary>
        private static int[] Canonical(int rows, int segments, int[] poleRows)
        {
            int stride = segments + 1;
            var map = new int[rows * stride];
            var poles = new HashSet<int>(poleRows);

            for (int r = 0; r < rows; r++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    int index = r * stride + s;
                    if (poles.Contains(r)) map[index] = r * stride;
                    else if (s == segments) map[index] = r * stride;
                    else map[index] = index;
                }
            }
            return map;
        }

        private static Mesh Build(string name, Vector3[] vertices, Color32[] colours, int rows, int segments, int[] canonical)
        {
            int stride = segments + 1;
            var triangles = new int[(rows - 1) * segments * 6];
            int t = 0;

            for (int r = 0; r < rows - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s;
                    int b = a + 1;
                    int d = a + stride;
                    int e = d + 1;

                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = d;
                    triangles[t++] = b; triangles[t++] = e; triangles[t++] = d;
                }
            }

            // Normals from the cut geometry, area weighted, welded across the seam and the poles.
            // Oriented outward afterwards by the vertex's position against the axis's, since the
            // lathe's winding is not guaranteed to face out and the shader draws both sides.
            var sum = new Vector3[vertices.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i], i1 = triangles[i + 1], i2 = triangles[i + 2];
                Vector3 n = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
                sum[canonical[i0]] += n;
                sum[canonical[i1]] += n;
                sum[canonical[i2]] += n;
            }

            var normals = new Vector3[vertices.Length];
            float sign = OutwardSign(vertices, sum, canonical);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = sum[canonical[i]];
                normals[i] = n.sqrMagnitude > 1e-20f ? sign * n.normalized : Vector3.up;
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors32 = colours;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>+1 when the accumulated normals point away from the axis on the whole, else −1.</summary>
        private static float OutwardSign(Vector3[] vertices, Vector3[] sum, int[] canonical)
        {
            double score = 0d;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 radial = new Vector3(vertices[i].x, 0f, vertices[i].z);
                Vector3 n = sum[canonical[i]];
                score += Vector3.Dot(n, radial) + n.y * vertices[i].y;
            }
            return score >= 0d ? 1f : -1f;
        }

        // ------------------------------------------------------------------ noise

        private static Vector3 SeedOffset(int seed)
        {
            uint h = (uint)seed;
            float Next()
            {
                h ^= h << 13; h ^= h >> 17; h ^= h << 5;
                return (h & 0xffffff) / 16777216f;
            }
            if (h == 0u) h = 0x9e3779b9u;
            return new Vector3(Next() * 500f, Next() * 500f, Next() * 500f);
        }

        /// <summary>A ridged fold of a [0, 1] field: 0 on the crest where the field crosses a half, 1 at its extremes.</summary>
        private static float Ridged(float n) => Mathf.Abs(2f * n - 1f);

        /// <summary>
        /// Bedding: a level groove about every half metre of height, wandering a little so the
        /// beds do not read as a lathe's rings, in [0, 1] (1 in a groove).
        /// </summary>
        private static float Strata(Vector3 q)
        {
            float warp = 0.9f * Noise(q / 3.2f);
            float phase = q.y / 0.55f + warp;
            float frac = phase - Mathf.Floor(phase);
            return Smooth(0.62f, 0.9f, frac) * (1f - Smooth(0.9f, 1f, frac));
        }

        private static float Smooth(float a, float b, float x)
        {
            float u = Mathf.Clamp01((x - a) / (b - a));
            return u * u * (3f - 2f * u);
        }

        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)z * 0xcb1ab31fu;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }

        /// <summary>Value noise in three dimensions, in [0, 1], with the quintic fade the shader's has.</summary>
        private static float Noise(Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
            float ux = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float uy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            float uz = fz * fz * fz * (fz * (fz * 6f - 15f) + 10f);

            float a = Hash(ix, iy, iz), b = Hash(ix + 1, iy, iz);
            float c = Hash(ix, iy + 1, iz), d = Hash(ix + 1, iy + 1, iz);
            float e = Hash(ix, iy, iz + 1), f = Hash(ix + 1, iy, iz + 1);
            float g = Hash(ix, iy + 1, iz + 1), h = Hash(ix + 1, iy + 1, iz + 1);

            float x0 = Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
            float x1 = Mathf.Lerp(Mathf.Lerp(e, f, ux), Mathf.Lerp(g, h, ux), uy);
            return Mathf.Lerp(x0, x1, uz);
        }

        /// <summary>
        /// The distance to the nearest of one jittered feature point per unit cell, clamped to
        /// [0, 1]: zero at a point, rising between them, so one minus it is a field of rounded
        /// boulders and the small values are pits.
        /// </summary>
        private static float Cells(Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float best = 8f;

            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = ix + dx, cy = iy + dy, cz = iz + dz;
                var feature = new Vector3(
                    cx + 0.15f + 0.7f * Hash(cx, cy, cz),
                    cy + 0.15f + 0.7f * Hash(cx + 71, cy - 13, cz + 5),
                    cz + 0.15f + 0.7f * Hash(cx - 29, cy + 43, cz - 17));
                float d = (feature - p).sqrMagnitude;
                if (d < best) best = d;
            }

            return Mathf.Clamp01(Mathf.Sqrt(best));
        }
    }
}

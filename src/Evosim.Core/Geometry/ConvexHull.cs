using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// The 3D convex hull of a small point set, built incrementally, and the surface areas that
    /// come off a body's corner cloud. D099, 2026-09-19.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it is for.</b> A body's silhouette — its orientation-averaged projected area, which
    /// is a quarter of a convex surface by Cauchy's formula — is what DESIGN.md §5A.2b caps a
    /// creature's claim on the light at. Summing the parts' own quarter-surfaces instead counts
    /// the same square metre once per part that occupies it, which is what a folded knot of
    /// sixteen overlapping links did in round 41c (logbook/0107).
    /// </para>
    /// <para>
    /// <b>No library, and none wanted.</b> At a few hundred corners the O(n²) shape of this costs
    /// nothing, and <c>Evosim.Core</c> has no dependencies at all, which is what keeps its tests
    /// at a second and its arithmetic reproducible across machines. Ported here from
    /// <c>scripts/overlap/</c>, the probe that sized D099; the probe now calls this, so the two
    /// cannot disagree.
    /// </para>
    /// <para>
    /// <b>Deterministic.</b> Every loop is over indices in the order the caller supplied, the
    /// arithmetic is IEEE double throughout, and no hash set, dictionary or sort decides
    /// anything. Two runs over the same cloud give the same bits; the same cloud in another
    /// order gives the same area.
    /// </para>
    /// </remarks>
    public static class ConvexHull
    {
        /// <summary>Surface area of the axis-aligned bounding box of the points, m².</summary>
        public static double AabbSurfaceArea(List<Double3> pts)
        {
            if (pts == null) throw new ArgumentNullException(nameof(pts));
            if (pts.Count == 0) return 0.0;

            double minX = pts[0].X, maxX = pts[0].X;
            double minY = pts[0].Y, maxY = pts[0].Y;
            double minZ = pts[0].Z, maxZ = pts[0].Z;
            for (int i = 1; i < pts.Count; i++)
            {
                Double3 p = pts[i];
                if (p.X < minX) minX = p.X; else if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y; else if (p.Y > maxY) maxY = p.Y;
                if (p.Z < minZ) minZ = p.Z; else if (p.Z > maxZ) maxZ = p.Z;
            }

            double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
            return 2.0 * (dx * dy + dy * dz + dz * dx);
        }

        /// <summary>
        /// The hull's surface area when there is a hull, and the bounding box's when there is
        /// not — the rule the probe and the phenotype both take, in one place so they cannot
        /// differ. Metres squared.
        /// </summary>
        /// <param name="pts">The cloud, in one frame. Not modified.</param>
        /// <param name="fellBackToBox">
        /// True when the cloud was collinear, coplanar or numerically broken and
        /// <paramref name="boxSurfaceArea"/> stood in. A caller that reports an area reports this
        /// beside it rather than handing on a silently substituted number.
        /// </param>
        /// <param name="boxSurfaceArea">The axis-aligned box's surface area, m², always computed.</param>
        public static double SurfaceArea(
            List<Double3> pts, out bool fellBackToBox, out double boxSurfaceArea) =>
            SurfaceArea(pts, out fellBackToBox, out boxSurfaceArea, null);

        /// <summary>
        /// <see cref="SurfaceArea(List{Double3}, out bool, out double)"/>, and the faces the area
        /// was summed over as area vectors — D110's shadow in a pose.
        /// </summary>
        /// <param name="pts">The cloud, in one frame. Not modified.</param>
        /// <param name="fellBackToBox">As the overload without the faces says.</param>
        /// <param name="boxSurfaceArea">The axis-aligned box's surface area, m², always computed.</param>
        /// <param name="faceAreas">
        /// Cleared and filled with one outward vector a face, its length the face's area, m². When
        /// the box stood in, its six faces. Null asks for the area alone.
        /// </param>
        /// <remarks>
        /// <b>The area is the same number with or without the faces</b>, to the bit: the faces are
        /// collected beside the sum, never summed from, so asking for them moves no recorded
        /// silhouette (the Slow snapshot scan is the check).
        /// </remarks>
        public static double SurfaceArea(
            List<Double3> pts, out bool fellBackToBox, out double boxSurfaceArea,
            List<Double3> faceAreas)
        {
            boxSurfaceArea = AabbSurfaceArea(pts);

            // The 1.001 is not a tolerance on the hull, it is a sanity check on it: a convex hull
            // can never have more surface than the box that contains it, so a larger answer means
            // the build went wrong and the box is the honest number.
            if (TryBuild(pts, out double hull, faceAreas) && hull > 0.0 && hull <= boxSurfaceArea * 1.001)
            {
                fellBackToBox = false;
                return hull;
            }

            if (faceAreas != null) AppendAabbFaces(pts, faceAreas);

            fellBackToBox = true;
            return boxSurfaceArea;
        }

        /// <summary>
        /// The axis-aligned box's six faces as outward area vectors, replacing whatever the list
        /// held — the stand-in for a hull that did not build.
        /// </summary>
        private static void AppendAabbFaces(List<Double3> pts, List<Double3> into)
        {
            into.Clear();
            if (pts.Count == 0) return;

            double minX = pts[0].X, maxX = pts[0].X;
            double minY = pts[0].Y, maxY = pts[0].Y;
            double minZ = pts[0].Z, maxZ = pts[0].Z;
            for (int i = 1; i < pts.Count; i++)
            {
                Double3 p = pts[i];
                if (p.X < minX) minX = p.X; else if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y; else if (p.Y > maxY) maxY = p.Y;
                if (p.Z < minZ) minZ = p.Z; else if (p.Z > maxZ) maxZ = p.Z;
            }

            double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
            into.Add(new Double3(dy * dz, 0.0, 0.0));
            into.Add(new Double3(-dy * dz, 0.0, 0.0));
            into.Add(new Double3(0.0, dx * dz, 0.0));
            into.Add(new Double3(0.0, -dx * dz, 0.0));
            into.Add(new Double3(0.0, 0.0, dx * dy));
            into.Add(new Double3(0.0, 0.0, -dx * dy));
        }

        /// <summary>
        /// The area a set of outward face vectors projects onto the plane normal to
        /// <paramref name="up"/>: the one-sided sum of each face's area times the cosine of its
        /// normal with up, faces turned away counting nothing. For a closed convex surface this is
        /// its shadow along <paramref name="up"/>, which must be a unit vector.
        /// </summary>
        public static double ProjectedArea(IReadOnlyList<Double3> faceAreas, Double3 up)
        {
            if (faceAreas == null) throw new ArgumentNullException(nameof(faceAreas));

            double sum = 0.0;
            for (int f = 0; f < faceAreas.Count; f++)
            {
                double d = Double3.Dot(faceAreas[f], up);
                if (d > 0.0) sum += d;
            }
            return sum;
        }

        private struct Face
        {
            public int A, B, C;
            public Double3 N;   // outward, not normalised
            public bool Dead;
        }

        /// <summary>
        /// Surface area of the convex hull, m². Returns false when the cloud is collinear or
        /// coplanar — there is no hull with an interior and the caller must say so.
        /// </summary>
        public static bool TrySurfaceArea(List<Double3> pts, out double area) =>
            TryBuild(pts, out area, null);

        /// <summary>
        /// <see cref="TrySurfaceArea"/>, and the hull's faces as outward area vectors, each half
        /// the unnormalised cross product the area is summed from. Returns false, with the list
        /// emptied, where <see cref="TrySurfaceArea"/> does.
        /// </summary>
        public static bool TryFaces(List<Double3> pts, out double area, List<Double3> faceAreas)
        {
            if (faceAreas == null) throw new ArgumentNullException(nameof(faceAreas));
            return TryBuild(pts, out area, faceAreas);
        }

        private static bool TryBuild(List<Double3> pts, out double area, List<Double3> faceAreas)
        {
            if (pts == null) throw new ArgumentNullException(nameof(pts));
            faceAreas?.Clear();

            area = 0.0;
            int n = pts.Count;
            if (n < 4) return false;

            double scale = 0.0;
            for (int i = 0; i < n; i++)
            {
                double m = Math.Abs(pts[i].X) + Math.Abs(pts[i].Y) + Math.Abs(pts[i].Z);
                if (m > scale) scale = m;
            }
            if (!(scale > 0.0)) return false;

            double tol = 1e-9 * scale;   // degeneracy
            double vis = 1e-12 * scale;  // face visibility

            // Four points spanning three dimensions, chosen for spread rather than order.
            int i0 = 0, i1 = 1;
            double best = 0.0;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double d = (pts[i] - pts[j]).Length;
                    if (d > best) { best = d; i0 = i; i1 = j; }
                }
            }
            if (best <= tol) return false;

            Double3 axis = pts[i1] - pts[i0];
            int i2 = -1; best = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = Double3.Cross(axis, pts[i] - pts[i0]).Length / axis.Length;
                if (d > best) { best = d; i2 = i; }
            }
            if (i2 < 0 || best <= tol) return false;

            Double3 nrm = Double3.Cross(pts[i1] - pts[i0], pts[i2] - pts[i0]);
            double nlen = nrm.Length;
            if (nlen <= tol) return false;

            int i3 = -1; best = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = Math.Abs(Double3.Dot(nrm, pts[i] - pts[i0])) / nlen;
                if (d > best) { best = d; i3 = i; }
            }
            if (i3 < 0 || best <= tol) return false;   // coplanar

            // A point strictly inside the hull for the whole build, so every face can be
            // oriented outward by one dot product rather than by tracking winding.
            Double3 inside = (pts[i0] + pts[i1] + pts[i2] + pts[i3]) * 0.25;

            // A convex hull of n points carries at most 2n - 4 faces. An incremental build can
            // be driven off that bound by a cloud with duplicate or near-coplanar points: the
            // visible set stops being one connected patch, the horizon stops being one cycle,
            // and a point then adds more faces than it removes. Unchecked that compounds, and
            // the O(edges²) horizon pairing compounds with it — a real body of sixteen parts
            // drawn from GenomeFactory.Random hung this for minutes before the ceiling existed
            // (2026-09-19, found by BrainTests, which develops sixty random genomes).
            //
            // The ceiling is four times the theoretical maximum, so an honest cloud can never
            // reach it and a cloud that does is reported degenerate rather than ground on. It
            // is a bound on the build, not a tolerance on the geometry: nothing that finished
            // before finishes differently now.
            int faceCeiling = 8 * n + 16;

            var faces = new List<Face>(4 * n);
            AddFace(faces, pts, i0, i1, i2, inside);
            AddFace(faces, pts, i0, i1, i3, inside);
            AddFace(faces, pts, i0, i2, i3, inside);
            AddFace(faces, pts, i1, i2, i3, inside);

            var horizon = new List<(int U, int V)>();

            for (int p = 0; p < n; p++)
            {
                if (p == i0 || p == i1 || p == i2 || p == i3) continue;

                bool any = false;
                for (int f = 0; f < faces.Count; f++)
                {
                    if (faces[f].Dead) continue;
                    if (Double3.Dot(faces[f].N, pts[p] - pts[faces[f].A]) > vis)
                    {
                        Face face = faces[f];
                        face.Dead = true;
                        faces[f] = face;
                        any = true;
                    }
                }
                if (!any) continue;

                // Every edge of the newly dead faces; an edge whose reverse is not also on a
                // newly dead face is on the horizon. Dead-from-an-earlier-round faces cannot
                // confuse this, because they were removed before this point was considered.
                horizon.Clear();
                var edges = new List<(int U, int V)>();
                for (int f = 0; f < faces.Count; f++)
                {
                    if (!faces[f].Dead) continue;
                    // Only the faces killed in this round are still in the list; killed faces
                    // are compacted out at the end of the round, so this is exactly them.
                    edges.Add((faces[f].A, faces[f].B));
                    edges.Add((faces[f].B, faces[f].C));
                    edges.Add((faces[f].C, faces[f].A));
                }

                for (int e = 0; e < edges.Count; e++)
                {
                    bool paired = false;
                    for (int g = 0; g < edges.Count; g++)
                    {
                        if (edges[g].U == edges[e].V && edges[g].V == edges[e].U) { paired = true; break; }
                    }
                    if (!paired) horizon.Add(edges[e]);
                }

                faces.RemoveAll(f => f.Dead);
                for (int e = 0; e < horizon.Count; e++)
                {
                    AddFace(faces, pts, horizon[e].U, horizon[e].V, p, inside);
                }

                if (faces.Count > faceCeiling) return false;
            }

            double sum = 0.0;
            for (int f = 0; f < faces.Count; f++)
            {
                if (faces[f].Dead) continue;
                sum += 0.5 * faces[f].N.Length;
                faceAreas?.Add(faces[f].N * 0.5);
            }

            area = sum;
            bool finite = !double.IsNaN(sum) && !double.IsInfinity(sum);
            if (!finite) faceAreas?.Clear();
            return finite;
        }

        private static void AddFace(List<Face> faces, List<Double3> pts, int a, int b, int c, Double3 inside)
        {
            Double3 n = Double3.Cross(pts[b] - pts[a], pts[c] - pts[a]);
            if (Double3.Dot(n, inside - pts[a]) > 0.0)
            {
                int t = b; b = c; c = t;
                n = Double3.Cross(pts[b] - pts[a], pts[c] - pts[a]);
            }
            faces.Add(new Face { A = a, B = b, C = c, N = n, Dead = false });
        }

        /// <summary>
        /// The eight corners of one developed part's oriented box, appended in the creature's
        /// own frame.
        /// </summary>
        /// <remarks>
        /// <b>Every shape is taken as a box of the part's half-extents</b> — a sphere and a
        /// capsule included, which over-states them, since both are inscribed in that box. That
        /// is the safe side for a cap: a hull drawn round the boxes can only be larger than one
        /// drawn round the true surfaces, so the cap can only be looser for a round body and
        /// never bites a shape it should not.
        /// </remarks>
        public static void AppendPartCorners(PhenotypePart part, List<Double3> into)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (into == null) throw new ArgumentNullException(nameof(into));

            Quat q = part.Rotation;
            Float3 c = part.Position;
            Float3 u0 = q.Rotate(new Float3(1f, 0f, 0f));
            Float3 u1 = q.Rotate(new Float3(0f, 1f, 0f));
            Float3 u2 = q.Rotate(new Float3(0f, 0f, 1f));
            var e = new Float3(
                Math.Abs(part.HalfExtents.X),
                Math.Abs(part.HalfExtents.Y),
                Math.Abs(part.HalfExtents.Z));

            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Float3 p = c + u0 * (e.X * sx) + u1 * (e.Y * sy) + u2 * (e.Z * sz);
                        into.Add(new Double3(p.X, p.Y, p.Z));
                    }
                }
            }
        }
    }
}

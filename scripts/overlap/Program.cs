using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;

namespace Evosim.Scratch.Overlap
{
    /// <summary>
    /// Develops every genome in a run's snapshot and reports, per creature, how many pairs of
    /// developed parts overlap in space — a part's own parent excluded — and three areas: the
    /// lit area Core bills light on, and the orientation-averaged projected area of the body's
    /// convex hull and of its axis-aligned box. Nothing is written anywhere; the report goes to
    /// stdout.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var snapshots = new List<string>();
            string configOverride = null;
            int top = 5;
            long dumpId = -1;
            bool silhouette = false;
            var onlyIds = new HashSet<long>();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--config") { configOverride = args[++i]; continue; }
                if (args[i] == "--top") { top = int.Parse(args[++i], CultureInfo.InvariantCulture); continue; }
                if (args[i] == "--dump") { dumpId = long.Parse(args[++i], CultureInfo.InvariantCulture); continue; }
                if (args[i] == "--silhouette") { silhouette = true; continue; }
                if (args[i] == "--ids")
                {
                    foreach (string s in args[++i].Split(','))
                    {
                        if (s.Length > 0) onlyIds.Add(long.Parse(s, CultureInfo.InvariantCulture));
                    }
                    continue;
                }
                snapshots.Add(args[i]);
            }

            if (snapshots.Count == 0)
            {
                Console.Error.WriteLine(
                    "usage: Overlap <snapshot.jsonl> [more...] [--config path] [--top N] " +
                    "[--dump <id>] [--silhouette [--ids a,b,c]]");
                return 2;
            }

            foreach (string path in snapshots)
            {
                try
                {
                    if (dumpId >= 0) Dump(path, configOverride, dumpId);
                    else if (silhouette) Silhouette(path, configOverride, onlyIds);
                    else Report(path, configOverride, top);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("=== " + path);
                    Console.WriteLine("  REFUSED: " + ex.GetType().Name + ": " + FirstLine(ex.Message));
                    Console.WriteLine();
                }
            }

            return 0;
        }

        private static string FirstLine(string s)
        {
            int n = s.IndexOf('\n');
            return n < 0 ? s : s.Substring(0, n).TrimEnd('\r');
        }

        private static string F(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        private static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        private static string V(Float3 v) => F(v.X) + " " + F(v.Y) + " " + F(v.Z);

        private sealed class Row
        {
            public long Id;
            public int Parts;
            public int Pairs;         // loose rule: boxes intersect at all
            public int PairsDeep;     // strict rule: depth > 10% of smaller box's smallest half-extent
            public int ParentChildOverlaps;  // context only: parent-child pairs whose boxes intersect
            public bool Articulated;  // any non-Fixed joint

            public double LitArea;    // Phenotype.TotalLitArea, m2
            public double HullArea4;  // convex hull surface / 4, m2 — Cauchy's orientation average
            public double AabbArea4;  // axis-aligned box surface / 4, m2, in the creature's frame
            public bool HullFellBack; // the hull degenerated and AabbArea4 was used for HullArea4

            public double HullOverLit => LitArea > 0 ? HullArea4 / LitArea : double.NaN;
        }

        // ---------------------------------------------------------------- measurement

        private static Row Measure(Phenotype body, long id)
        {
            var row = new Row { Id = id, Parts = body.PartCount, LitArea = body.TotalLitArea };

            var boxes = new Obb[body.PartCount];
            var corners = new List<V3>(body.PartCount * 8);

            for (int i = 0; i < body.PartCount; i++)
            {
                PhenotypePart p = body.Parts[i];
                boxes[i] = Obb.From(p);
                boxes[i].AppendCorners(corners);
                if (!p.IsRoot && p.JointType != JointType.Fixed) row.Articulated = true;
            }

            for (int i = 0; i < body.PartCount; i++)
            {
                for (int j = i + 1; j < body.PartCount; j++)
                {
                    if (body.Parts[j].ParentIndex == i || body.Parts[i].ParentIndex == j)
                    {
                        // Not counted in the answer: PhysX disables collision between adjacent
                        // links of one articulation, so these can never be contact pairs.
                        // Reported separately because it says how tight the plan packs.
                        if (Obb.Intersect(boxes[i], boxes[j], out _)) row.ParentChildOverlaps++;
                        continue;
                    }

                    if (!Obb.Intersect(boxes[i], boxes[j], out float depth)) continue;
                    row.Pairs++;

                    Float3 ha = boxes[i].E, hb = boxes[j].E;
                    Float3 smaller = ha.BoxVolume <= hb.BoxVolume ? ha : hb;
                    float thinnest = Math.Min(Math.Abs(smaller.X), Math.Min(Math.Abs(smaller.Y), Math.Abs(smaller.Z)));
                    if (depth > 0.10f * thinnest) row.PairsDeep++;
                }
            }

            double aabb = Hull.AabbSurfaceArea(corners);
            row.AabbArea4 = aabb / 4.0;

            if (Hull.TrySurfaceArea(corners, out double hull) && hull > 0.0 && hull <= aabb * 1.001)
            {
                row.HullArea4 = hull / 4.0;
            }
            else
            {
                // Coplanar, collinear or numerically broken: the axis-aligned box stands in, and
                // the caller is told so rather than handed a silently substituted number.
                row.HullArea4 = row.AabbArea4;
                row.HullFellBack = true;
            }

            return row;
        }

        private static bool LoadConfig(
            string snapshotFullPath, string configOverride,
            out DevelopmentLimits limits, out PartShapeRegistry shapes, out string note)
        {
            limits = DevelopmentLimits.Default;
            shapes = PartShapeRegistry.Standard;

            string configPath = configOverride ??
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(snapshotFullPath)), "config.json");
            try
            {
                RunConfig config = RunConfigJson.Read(File.ReadAllText(configPath), out string mismatch);
                limits = config.Development;
                shapes = config.Shapes;
                note = "config " + configPath + (mismatch != null ? " (hash: " + mismatch + ")" : "");
                return true;
            }
            catch (Exception ex)
            {
                note = "config REFUSED (" + ex.GetType().Name + ": " + FirstLine(ex.Message) +
                       ") -- using DevelopmentLimits.Default and PartShapeRegistry.Standard";
                return false;
            }
        }

        private static List<Row> Walk(
            string full, DevelopmentLimits limits, PartShapeRegistry shapes,
            out int refused, out string firstRefusal, SortedDictionary<string, int> shapeCounts)
        {
            var rows = new List<Row>();
            refused = 0;
            firstRefusal = null;

            foreach (string line in File.ReadLines(full))
            {
                if (line.Length == 0) continue;

                try
                {
                    Genome genome = GenomeJson.Read(line);
                    long id = GenomeJson.ReadId(line);
                    // Adult size: Developer.Develop builds the adult plan, which is what a
                    // newborn is a Scaled() copy of. No growth fraction is applied here.
                    Phenotype body = Developer.Develop(genome, limits, null, shapes);

                    if (shapeCounts != null)
                    {
                        for (int i = 0; i < body.PartCount; i++)
                        {
                            string s = body.Parts[i].ShapeId;
                            if (!shapeCounts.TryGetValue(s, out int c)) c = 0;
                            shapeCounts[s] = c + 1;
                        }
                    }

                    rows.Add(Measure(body, id));
                }
                catch (Exception ex)
                {
                    refused++;
                    if (firstRefusal == null) firstRefusal = ex.GetType().Name + ": " + FirstLine(ex.Message);
                }
            }

            return rows;
        }

        // ---------------------------------------------------------------- silhouette mode

        private static void Silhouette(string snapshotPath, string configOverride, HashSet<long> onlyIds)
        {
            string full = Path.GetFullPath(snapshotPath);
            Console.WriteLine("=== " + full + "   (silhouette)");

            LoadConfig(full, configOverride, out DevelopmentLimits limits, out PartShapeRegistry shapes, out string note);
            Console.WriteLine("  " + note);

            List<Row> rows = Walk(full, limits, shapes, out int refused, out string firstRefusal, null);
            if (rows.Count == 0)
            {
                Console.WriteLine("  creatures 0 (refused " + refused +
                                  (firstRefusal != null ? ": " + firstRefusal : "") + ")");
                Console.WriteLine();
                return;
            }

            Console.WriteLine("  creatures " + rows.Count + ", refused " + refused +
                              (firstRefusal != null ? " (" + firstRefusal + ")" : ""));
            Console.WriteLine("  hull fell back to the AABB on " + rows.Count(r => r.HullFellBack) + " creature(s)");
            Console.WriteLine("  per creature: id parts articulated litArea hullArea/4 aabbArea/4 hull/lit  (m2)");

            foreach (Row r in rows)
            {
                if (onlyIds.Count > 0 && !onlyIds.Contains(r.Id)) continue;
                Console.WriteLine(
                    "    id=" + r.Id +
                    " parts=" + r.Parts +
                    " articulated=" + (r.Articulated ? "yes" : "no") +
                    " litArea=" + F(r.LitArea) +
                    " hullArea/4=" + F(r.HullArea4) +
                    " aabbArea/4=" + F(r.AabbArea4) +
                    " hull/lit=" + F(r.HullOverLit) +
                    (r.HullFellBack ? "  [hull degenerate, AABB used]" : ""));
            }

            Summarise(rows, "1 part", r => r.Parts == 1);
            Summarise(rows, "9 or 16 parts", r => r.Parts == 9 || r.Parts == 16);
            Summarise(rows, "all", r => true);
            Console.WriteLine();
        }

        private static void Summarise(List<Row> rows, string label, Func<Row, bool> pick)
        {
            double[] v = rows.Where(pick).Select(r => r.HullOverLit)
                             .Where(x => !double.IsNaN(x) && !double.IsInfinity(x))
                             .OrderBy(x => x).ToArray();
            if (v.Length == 0)
            {
                Console.WriteLine("  hull/lit, " + label + ": n=0");
                return;
            }

            Console.WriteLine("  hull/lit, " + label + ": n=" + v.Length +
                              "  median " + F(Percentile(v, 0.50)) +
                              "  p90 " + F(Percentile(v, 0.90)) +
                              "  min " + F(v[0]) + "  max " + F(v[v.Length - 1]));
        }

        /// <summary>Linear interpolation between order statistics at rank p*(n-1).</summary>
        private static double Percentile(double[] sorted, double p)
        {
            if (sorted.Length == 1) return sorted[0];
            double pos = p * (sorted.Length - 1);
            int lo = (int)Math.Floor(pos);
            int hi = (int)Math.Ceiling(pos);
            return lo == hi ? sorted[lo] : sorted[lo] + (pos - lo) * (sorted[hi] - sorted[lo]);
        }

        // ---------------------------------------------------------------- dump mode

        /// <summary>
        /// Every developed part of one genome, in the developer's own depth-first order, plus
        /// what development did to the plan and the limits it ran under.
        /// </summary>
        private static void Dump(string snapshotPath, string configOverride, long wantId)
        {
            string full = Path.GetFullPath(snapshotPath);
            Console.WriteLine("=== " + full + "   id " + wantId);

            LoadConfig(full, configOverride, out DevelopmentLimits limits, out PartShapeRegistry shapes, out string note);
            Console.WriteLine("  " + note);

            Console.WriteLine("  limits: maxParts=" + limits.MaxParts + " maxDepth=" + limits.MaxDepth +
                              " minPartVolume=" + F(limits.MinPartVolume) +
                              " maxPartVolume=" + F(limits.MaxPartVolume) +
                              " minPartHalfExtent=" + F(limits.MinPartHalfExtent));

            string found = null;
            foreach (string line in File.ReadLines(full))
            {
                if (line.Length == 0) continue;
                if (GenomeJson.ReadId(line) == wantId) { found = line; break; }
            }

            if (found == null)
            {
                Console.WriteLine("  no row with that id");
                Console.WriteLine();
                return;
            }

            Genome genome = GenomeJson.Read(found);
            Phenotype body = Developer.Develop(genome, limits, null, shapes);

            Console.WriteLine("  genome: root=" + genome.RootIndex + " adultScale=" + F(genome.AdultScale) +
                              " nodes=" + genome.Nodes.Count);
            for (int n = 0; n < genome.Nodes.Count; n++)
            {
                MorphNode node = genome.Nodes[n];
                Console.WriteLine("    node " + n + " cell=" + node.CellTypeId + " shape=" + node.ShapeId +
                                  " joint=" + node.JointType + " recursiveLimit=" + node.RecursiveLimit +
                                  " dims=(" + V(node.Dimensions) + ") edges=" + node.Edges.Count);
                for (int e = 0; e < node.Edges.Count; e++)
                {
                    MorphEdge edge = node.Edges[e];
                    Console.WriteLine("      edge " + e + " -> node " + edge.Child +
                                      " terminalOnly=" + edge.TerminalOnly +
                                      " scale=(" + V(edge.Scale) + ")" +
                                      " parentAnchor=(" + V(edge.ParentAnchor) + ")" +
                                      " childAnchor=(" + V(edge.ChildAnchor) + ")" +
                                      " reflect=(" + edge.Reflect.X + " " + edge.Reflect.Y + " " + edge.Reflect.Z + ")" +
                                      " orientation=(" + F(edge.Orientation.X) + " " + F(edge.Orientation.Y) +
                                      " " + F(edge.Orientation.Z) + " " + F(edge.Orientation.W) + ")");
                }
            }

            Console.WriteLine("  developed: parts=" + body.PartCount +
                              " maxDepthReached=" + body.MaxDepthReached +
                              " prunedForParts=" + body.PrunedForParts +
                              " prunedForDepth=" + body.PrunedForDepth +
                              " prunedForVolume=" + body.PrunedForVolume +
                              " truncated=" + body.WasTruncated);

            Row row = Measure(body, wantId);
            Console.WriteLine("  areas (m2): litArea=" + F(row.LitArea) +
                              " hullArea/4=" + F(row.HullArea4) +
                              " aabbArea/4=" + F(row.AabbArea4) +
                              " hull/lit=" + F(row.HullOverLit) +
                              (row.HullFellBack ? "  [hull degenerate, AABB used]" : ""));

            Console.WriteLine("  parts (idx parent node depth shape cell joint | position | halfExtents | " +
                              "rotation quat xyzw | local axes X, Y, Z):");
            for (int i = 0; i < body.PartCount; i++)
            {
                PhenotypePart p = body.Parts[i];
                Quat q = p.Rotation;
                Float3 ax = q.Rotate(new Float3(1f, 0f, 0f));
                Float3 ay = q.Rotate(new Float3(0f, 1f, 0f));
                Float3 az = q.Rotate(new Float3(0f, 0f, 1f));
                Console.WriteLine(
                    "    " + i.ToString().PadLeft(2) +
                    " parent=" + p.ParentIndex.ToString().PadLeft(2) +
                    " node=" + p.SourceNode +
                    " depth=" + p.Depth +
                    " " + p.ShapeId +
                    " " + p.CellTypeId +
                    " " + p.JointType +
                    (p.Mirrored ? " mirrored" : "") +
                    " | pos " + V(p.Position) +
                    " | half " + V(p.HalfExtents) +
                    " | quat " + F(q.X) + " " + F(q.Y) + " " + F(q.Z) + " " + F(q.W) +
                    " | X(" + V(ax) + ") Y(" + V(ay) + ") Z(" + V(az) + ")");
            }

            Console.WriteLine();
        }

        // ---------------------------------------------------------------- overlap report

        private static void Report(string snapshotPath, string configOverride, int top)
        {
            string full = Path.GetFullPath(snapshotPath);
            Console.WriteLine("=== " + full);

            LoadConfig(full, configOverride, out DevelopmentLimits limits, out PartShapeRegistry shapes, out string note);
            Console.WriteLine("  " + note);

            var shapeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            List<Row> rows = Walk(full, limits, shapes, out int refused, out string firstRefusal, shapeCounts);

            if (rows.Count == 0)
            {
                Console.WriteLine("  creatures 0 (refused " + refused +
                                  (firstRefusal != null ? ": " + firstRefusal : "") + ")");
                Console.WriteLine();
                return;
            }

            var hist = new SortedDictionary<int, int>();
            foreach (Row r in rows)
            {
                hist.TryGetValue(r.Parts, out int c);
                hist[r.Parts] = c + 1;
            }

            int withAny = rows.Count(r => r.Pairs > 0);
            int totalPairs = rows.Sum(r => r.Pairs);
            int totalDeep = rows.Sum(r => r.PairsDeep);
            int art = rows.Count(r => r.Articulated);
            int artWithAny = rows.Count(r => r.Articulated && r.Pairs > 0);
            int rigidWithAny = rows.Count(r => !r.Articulated && r.Pairs > 0);
            int artPairs = rows.Where(r => r.Articulated).Sum(r => r.Pairs);
            int rigidPairs = rows.Where(r => !r.Articulated).Sum(r => r.Pairs);
            int artDeep = rows.Where(r => r.Articulated).Sum(r => r.PairsDeep);
            int rigidDeep = rows.Where(r => !r.Articulated).Sum(r => r.PairsDeep);

            Console.WriteLine("  creatures " + rows.Count + ", refused " + refused +
                              (firstRefusal != null ? " (" + firstRefusal + ")" : ""));
            Console.WriteLine("  shapes: " + string.Join(", ", shapeCounts.Select(kv => kv.Key + "=" + kv.Value)));
            Console.WriteLine("  parts histogram: " + string.Join(", ", hist.Select(kv => kv.Key + "->" + kv.Value)));
            Console.WriteLine("  articulated (any non-Fixed joint): " + art + " of " + rows.Count);
            Console.WriteLine("  creatures with >=1 self-overlapping pair: " + withAny +
                              "  (articulated " + artWithAny + " of " + art +
                              ", rigid " + rigidWithAny + " of " + (rows.Count - art) + ")");
            Console.WriteLine("  total self-overlapping pairs: " + totalPairs +
                              "  (articulated " + artPairs + ", rigid " + rigidPairs + ")");
            Console.WriteLine("  strict rule (depth > 10% of smaller box's smallest half-extent): " + totalDeep +
                              " pairs  (articulated " + artDeep + ", rigid " + rigidDeep + ")");

            int pcPairs = rows.Sum(r => Math.Max(0, r.Parts - 1));
            int pcOver = rows.Sum(r => r.ParentChildOverlaps);
            Console.WriteLine("  context, parent-child pairs (excluded from the count above, PhysX " +
                              "does not collide adjacent links): " + pcOver + " of " + pcPairs + " overlap");
            Console.WriteLine("  hull fell back to the AABB on " + rows.Count(r => r.HullFellBack) + " creature(s)");

            Console.WriteLine("  top " + top + " by overlapping pairs:");
            foreach (Row r in rows.OrderByDescending(r => r.Pairs).ThenByDescending(r => r.Parts).Take(top))
            {
                Console.WriteLine("    id=" + r.Id + " parts=" + r.Parts + " pairs=" + r.Pairs +
                                  " deep=" + r.PairsDeep + " " + (r.Articulated ? "articulated" : "rigid") +
                                  " litArea=" + F(r.LitArea) + " hullArea/4=" + F(r.HullArea4) +
                                  " aabbArea/4=" + F(r.AabbArea4) + " hull/lit=" + F(r.HullOverLit) +
                                  (r.HullFellBack ? " [hull degenerate, AABB used]" : ""));
            }

            Summarise(rows, "1 part", r => r.Parts == 1);
            Summarise(rows, "9 or 16 parts", r => r.Parts == 9 || r.Parts == 16);
            Summarise(rows, "all", r => true);
            Console.WriteLine();
        }

        // ---------------------------------------------------------------- geometry

        /// <summary>A point in double precision — the hull is built away from Core's floats.</summary>
        private readonly struct V3
        {
            public readonly double X, Y, Z;
            public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
            public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static V3 operator *(V3 a, double s) => new V3(a.X * s, a.Y * s, a.Z * s);
            public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            public static V3 Cross(V3 a, V3 b) => new V3(
                a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
            public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        /// <summary>
        /// The 3D convex hull of a small point set, built incrementally, and the two surface
        /// areas that come off a body's corner cloud. No library: at 128 points the O(n^2) shape
        /// of this is nothing, and a dependency in a probe is a dependency in the record.
        /// </summary>
        private static class Hull
        {
            /// <summary>Surface area of the axis-aligned bounding box of the points, m².</summary>
            public static double AabbSurfaceArea(List<V3> pts)
            {
                if (pts.Count == 0) return 0.0;
                double minX = pts[0].X, maxX = pts[0].X;
                double minY = pts[0].Y, maxY = pts[0].Y;
                double minZ = pts[0].Z, maxZ = pts[0].Z;
                for (int i = 1; i < pts.Count; i++)
                {
                    V3 p = pts[i];
                    if (p.X < minX) minX = p.X; else if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y; else if (p.Y > maxY) maxY = p.Y;
                    if (p.Z < minZ) minZ = p.Z; else if (p.Z > maxZ) maxZ = p.Z;
                }
                double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
                return 2.0 * (dx * dy + dy * dz + dz * dx);
            }

            private struct Face
            {
                public int A, B, C;
                public V3 N;      // outward, not normalised
                public bool Dead;
            }

            /// <summary>
            /// Surface area of the convex hull, m². Returns false when the cloud is collinear or
            /// coplanar — there is no hull with an interior and the caller must say so.
            /// </summary>
            public static bool TrySurfaceArea(List<V3> pts, out double area)
            {
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

                V3 axis = pts[i1] - pts[i0];
                int i2 = -1; best = 0.0;
                for (int i = 0; i < n; i++)
                {
                    double d = V3.Cross(axis, pts[i] - pts[i0]).Length / axis.Length;
                    if (d > best) { best = d; i2 = i; }
                }
                if (i2 < 0 || best <= tol) return false;

                V3 nrm = V3.Cross(pts[i1] - pts[i0], pts[i2] - pts[i0]);
                double nlen = nrm.Length;
                if (nlen <= tol) return false;

                int i3 = -1; best = 0.0;
                for (int i = 0; i < n; i++)
                {
                    double d = Math.Abs(V3.Dot(nrm, pts[i] - pts[i0])) / nlen;
                    if (d > best) { best = d; i3 = i; }
                }
                if (i3 < 0 || best <= tol) return false;   // coplanar

                // A point strictly inside the hull for the whole build, so every face can be
                // oriented outward by one dot product rather than by tracking winding.
                V3 inside = (pts[i0] + pts[i1] + pts[i2] + pts[i3]) * 0.25;

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
                        if (V3.Dot(faces[f].N, pts[p] - pts[faces[f].A]) > vis)
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
                }

                double sum = 0.0;
                for (int f = 0; f < faces.Count; f++)
                {
                    if (faces[f].Dead) continue;
                    sum += 0.5 * faces[f].N.Length;
                }

                area = sum;
                return !double.IsNaN(sum) && !double.IsInfinity(sum);
            }

            private static void AddFace(List<Face> faces, List<V3> pts, int a, int b, int c, V3 inside)
            {
                V3 n = V3.Cross(pts[b] - pts[a], pts[c] - pts[a]);
                if (V3.Dot(n, inside - pts[a]) > 0.0)
                {
                    int t = b; b = c; c = t;
                    n = V3.Cross(pts[b] - pts[a], pts[c] - pts[a]);
                }
                faces.Add(new Face { A = a, B = b, C = c, N = n, Dead = false });
            }
        }

        /// <summary>An oriented box: centre, three unit axes, half-extents.</summary>
        private readonly struct Obb
        {
            public readonly Float3 C;
            public readonly Float3 U0, U1, U2;
            public readonly Float3 E;

            private Obb(Float3 c, Float3 u0, Float3 u1, Float3 u2, Float3 e)
            {
                C = c; U0 = u0; U1 = u1; U2 = u2; E = e;
            }

            /// <summary>
            /// Every shape is taken as a box of the part's half-extents — a sphere and a capsule
            /// included, which over-states them (both are inscribed in that box).
            /// </summary>
            public static Obb From(PhenotypePart p)
            {
                Quat q = p.Rotation;
                return new Obb(
                    p.Position,
                    q.Rotate(new Float3(1f, 0f, 0f)),
                    q.Rotate(new Float3(0f, 1f, 0f)),
                    q.Rotate(new Float3(0f, 0f, 1f)),
                    new Float3(Math.Abs(p.HalfExtents.X), Math.Abs(p.HalfExtents.Y), Math.Abs(p.HalfExtents.Z)));
            }

            /// <summary>This box's eight corners, in the creature's frame.</summary>
            public void AppendCorners(List<V3> into)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            Float3 p = C + U0 * (E.X * sx) + U1 * (E.Y * sy) + U2 * (E.Z * sz);
                            into.Add(new V3(p.X, p.Y, p.Z));
                        }
                    }
                }
            }

            private Float3 Axis(int i) => i == 0 ? U0 : (i == 1 ? U1 : U2);

            /// <summary>
            /// Separating-axis test over the 15 candidate axes. <paramref name="depth"/> is the
            /// least overlap found, in metres — the minimum-translation penetration.
            /// </summary>
            public static bool Intersect(in Obb a, in Obb b, out float depth)
            {
                const float Eps = 1e-6f;
                depth = float.MaxValue;

                var r = new float[3, 3];
                var absR = new float[3, 3];
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        r[i, j] = Float3.Dot(a.Axis(i), b.Axis(j));
                        absR[i, j] = Math.Abs(r[i, j]) + Eps;
                    }
                }

                Float3 d = b.C - a.C;
                var t = new float[3] { Float3.Dot(d, a.U0), Float3.Dot(d, a.U1), Float3.Dot(d, a.U2) };
                var ea = new float[3] { a.E.X, a.E.Y, a.E.Z };
                var eb = new float[3] { b.E.X, b.E.Y, b.E.Z };

                // A's three face normals.
                for (int i = 0; i < 3; i++)
                {
                    float ra = ea[i];
                    float rb = eb[0] * absR[i, 0] + eb[1] * absR[i, 1] + eb[2] * absR[i, 2];
                    float over = ra + rb - Math.Abs(t[i]);
                    if (over <= 0f) return false;
                    if (over < depth) depth = over;
                }

                // B's three face normals.
                for (int j = 0; j < 3; j++)
                {
                    float ra = ea[0] * absR[0, j] + ea[1] * absR[1, j] + ea[2] * absR[2, j];
                    float rb = eb[j];
                    float tj = Math.Abs(t[0] * r[0, j] + t[1] * r[1, j] + t[2] * r[2, j]);
                    float over = ra + rb - tj;
                    if (over <= 0f) return false;
                    if (over < depth) depth = over;
                }

                // The nine edge-edge cross products. Their ra, rb and distance all carry a factor
                // of the axis length, so the overlap is divided by it to come back to metres; a
                // near-parallel pair is skipped, which is what the face axes above already cover.
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        int i1 = (i + 1) % 3, i2 = (i + 2) % 3;
                        int j1 = (j + 1) % 3, j2 = (j + 2) % 3;

                        float ra = ea[i1] * absR[i2, j] + ea[i2] * absR[i1, j];
                        float rb = eb[j1] * absR[i, j2] + eb[j2] * absR[i, j1];
                        float tt = Math.Abs(t[i2] * r[i1, j] - t[i1] * r[i2, j]);
                        float over = ra + rb - tt;
                        if (over <= 0f) return false;

                        float len = (float)Math.Sqrt(Math.Max(0f, 1f - r[i, j] * r[i, j]));
                        if (len < 1e-4f) continue;
                        float scaled = over / len;
                        if (scaled < depth) depth = scaled;
                    }
                }

                return true;
            }
        }
    }
}

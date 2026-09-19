using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;

// The geometry this probe was written around lives in Core now, so that the pairs it counts of
// a snapshot and the pairs the world refuses a birth on are the same arithmetic. Aliased rather
// than spelled out at every use, which keeps the measurement below the code it always was.
using Obb = Evosim.Core.BoxOverlap.Obb;

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
            var corners = new List<Double3>(body.PartCount * 8);

            for (int i = 0; i < body.PartCount; i++)
            {
                PhenotypePart p = body.Parts[i];
                boxes[i] = Obb.From(p);
                ConvexHull.AppendPartCorners(p, corners);
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

                    if (depth > BoxOverlap.DepthThreshold(boxes[i], boxes[j], 0.10f)) row.PairsDeep++;
                }
            }

            // Core's hull, not the probe's: D099 moved this arithmetic into Evosim.Core so that
            // the number a body earns on and the number this probe reports cannot drift apart.
            // A coplanar, collinear or numerically broken cloud falls back to the axis-aligned
            // box, and the caller is told so rather than handed a silently substituted number.
            double hull = ConvexHull.SurfaceArea(corners, out bool fellBack, out double aabb);
            row.AabbArea4 = aabb / 4.0;
            row.HullArea4 = hull / 4.0;
            row.HullFellBack = fellBack;

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
    }
}

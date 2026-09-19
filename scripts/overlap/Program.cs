using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Evosim.Core;

namespace Evosim.Scratch.Overlap
{
    /// <summary>
    /// Develops every genome in a run's snapshot and counts, per creature, how many pairs of
    /// developed parts overlap in space — a part's own parent excluded. Nothing is written
    /// anywhere; the report goes to stdout.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var snapshots = new List<string>();
            string configOverride = null;
            int top = 5;
            long dumpId = -1;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--config") { configOverride = args[++i]; continue; }
                if (args[i] == "--top") { top = int.Parse(args[++i], CultureInfo.InvariantCulture); continue; }
                if (args[i] == "--dump") { dumpId = long.Parse(args[++i], CultureInfo.InvariantCulture); continue; }
                snapshots.Add(args[i]);
            }

            if (snapshots.Count == 0)
            {
                Console.Error.WriteLine("usage: Overlap <snapshot.jsonl> [more...] [--config path] [--top N]");
                return 2;
            }

            foreach (string path in snapshots)
            {
                try
                {
                    if (dumpId >= 0) Dump(path, configOverride, dumpId);
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

        private sealed class Row
        {
            public long Id;
            public int Parts;
            public int Pairs;         // loose rule: boxes intersect at all
            public int PairsDeep;     // strict rule: depth > 10% of smaller box's smallest half-extent
            public int ParentChildOverlaps;  // context only: parent-child pairs whose boxes intersect
            public bool Articulated;  // any non-Fixed joint
        }

        private static string F(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);

        private static string V(Float3 v) => F(v.X) + " " + F(v.Y) + " " + F(v.Z);

        /// <summary>
        /// Every developed part of one genome, in the developer's own depth-first order, plus
        /// what development did to the plan and the limits it ran under.
        /// </summary>
        private static void Dump(string snapshotPath, string configOverride, long wantId)
        {
            string full = Path.GetFullPath(snapshotPath);
            Console.WriteLine("=== " + full + "   id " + wantId);

            DevelopmentLimits limits = DevelopmentLimits.Default;
            PartShapeRegistry shapes = PartShapeRegistry.Standard;

            string configPath = configOverride ??
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(full)), "config.json");
            try
            {
                RunConfig config = RunConfigJson.Read(File.ReadAllText(configPath), out _);
                limits = config.Development;
                shapes = config.Shapes;
                Console.WriteLine("  config " + configPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("  config REFUSED (" + ex.GetType().Name + ": " + FirstLine(ex.Message) +
                                  ") -- using DevelopmentLimits.Default and PartShapeRegistry.Standard");
            }

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

        private static void Report(string snapshotPath, string configOverride, int top)
        {
            string full = Path.GetFullPath(snapshotPath);
            Console.WriteLine("=== " + full);

            // The run's own development limits and shape registry, when this build can read the
            // config; otherwise the defaults, said out loud rather than assumed.
            DevelopmentLimits limits = DevelopmentLimits.Default;
            PartShapeRegistry shapes = PartShapeRegistry.Standard;
            string configNote;

            string configPath = configOverride ??
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(full)), "config.json");
            try
            {
                RunConfig config = RunConfigJson.Read(File.ReadAllText(configPath), out string mismatch);
                limits = config.Development;
                shapes = config.Shapes;
                configNote = "config " + configPath + (mismatch != null ? " (hash: " + mismatch + ")" : "");
            }
            catch (Exception ex)
            {
                configNote = "config REFUSED (" + ex.GetType().Name + ": " + FirstLine(ex.Message) +
                             ") -- using DevelopmentLimits.Default and PartShapeRegistry.Standard";
            }

            Console.WriteLine("  " + configNote);

            var rows = new List<Row>();
            int refused = 0;
            string firstRefusal = null;
            var shapeCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (string line in File.ReadLines(full))
            {
                if (line.Length == 0) continue;

                Genome genome;
                long id;
                Phenotype body;
                try
                {
                    genome = GenomeJson.Read(line);
                    id = GenomeJson.ReadId(line);
                    // Adult size: Developer.Develop builds the adult plan, which is what a
                    // newborn is a Scaled() copy of. No growth fraction is applied here.
                    body = Developer.Develop(genome, limits, null, shapes);
                }
                catch (Exception ex)
                {
                    refused++;
                    if (firstRefusal == null) firstRefusal = ex.GetType().Name + ": " + FirstLine(ex.Message);
                    continue;
                }

                var row = new Row { Id = id, Parts = body.PartCount };
                var boxes = new Obb[body.PartCount];
                for (int i = 0; i < body.PartCount; i++)
                {
                    PhenotypePart p = body.Parts[i];
                    boxes[i] = Obb.From(p);
                    if (!shapeCounts.TryGetValue(p.ShapeId, out int c)) c = 0;
                    shapeCounts[p.ShapeId] = c + 1;
                    if (!p.IsRoot && p.JointType != JointType.Fixed) row.Articulated = true;
                }

                for (int i = 0; i < body.PartCount; i++)
                {
                    for (int j = i + 1; j < body.PartCount; j++)
                    {
                        // A part's own parent is excluded; every other pair counts, siblings and
                        // grandparents included.
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

                rows.Add(row);
            }

            if (rows.Count == 0)
            {
                Console.WriteLine("  creatures 0 (refused " + refused + (firstRefusal != null ? ": " + firstRefusal : "") + ")");
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

            Console.WriteLine("  top " + top + " by overlapping pairs:");
            foreach (Row r in rows.OrderByDescending(r => r.Pairs).ThenByDescending(r => r.Parts).Take(top))
            {
                Console.WriteLine("    id=" + r.Id + " parts=" + r.Parts + " pairs=" + r.Pairs +
                                  " deep=" + r.PairsDeep + " " + (r.Articulated ? "articulated" : "rigid"));
            }

            Console.WriteLine();
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
